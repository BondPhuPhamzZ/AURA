[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://')]
    [string]$BaseUrl = 'http://localhost:5000',

    [Parameter()]
    [ValidateRange(1, 10)]
    [int]$Copies = 5,

    [Parameter()]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ImagePath = (Join-Path $PSScriptRoot '..\wwwroot\test_data\images\HoaDon1.jpg'),

    [Parameter()]
    [ValidateRange(1, 1000000000)]
    [decimal]$ClaimedAmount = 295199
)

$resolvedImage = (Resolve-Path -LiteralPath $ImagePath).Path
$normalizedBaseUrl = $BaseUrl.TrimEnd('/')
$jobs = 1..$Copies | ForEach-Object {
    Start-Job -ArgumentList $_, $normalizedBaseUrl, $resolvedImage, $ClaimedAmount -ScriptBlock {
        param($Sequence, $RootUrl, $ReceiptPath, $Amount)

        Add-Type -AssemblyName System.Net.Http
        $handler = [System.Net.Http.HttpClientHandler]::new()
        $handler.CookieContainer = [System.Net.CookieContainer]::new()
        $client = [System.Net.Http.HttpClient]::new($handler)
        $client.Timeout = [TimeSpan]::FromMinutes(20)
        $clock = [System.Diagnostics.Stopwatch]::StartNew()

        try {
            $html = $client.GetStringAsync("$RootUrl/").GetAwaiter().GetResult()
            $tokenMatch = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
            if (-not $tokenMatch.Success) { throw 'Không tìm thấy antiforgery token trên trang chủ.' }

            $multipart = [System.Net.Http.MultipartFormDataContent]::new()
            $bytes = [System.IO.File]::ReadAllBytes($ReceiptPath)
            $fileContent = [System.Net.Http.ByteArrayContent]::new($bytes)
            $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/jpeg')
            $multipart.Add($fileContent, 'receiptFile', [System.IO.Path]::GetFileName($ReceiptPath))
            $multipart.Add([System.Net.Http.StringContent]::new([string]$Amount), 'claimedAmount')
            $multipart.Add([System.Net.Http.StringContent]::new("LOAD-$Sequence"), 'submitterCode')
            $multipart.Add([System.Net.Http.StringContent]::new("Người kiểm thử $Sequence"), 'submitterDisplayName')
            $multipart.Add([System.Net.Http.StringContent]::new('Concurrent smoke'), 'submitterDepartment')
            $multipart.Add([System.Net.Http.StringContent]::new($tokenMatch.Groups[1].Value), '__RequestVerificationToken')

            $response = $client.PostAsync("$RootUrl/Applicant/UploadReceipt", $multipart).GetAwaiter().GetResult()
            $acceptedMs = $clock.ElapsedMilliseconds
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if (-not $response.IsSuccessStatusCode) { throw "HTTP $([int]$response.StatusCode): $body" }
            $accepted = $body | ConvertFrom-Json

            do {
                Start-Sleep -Milliseconds 500
                $statusJson = $client.GetStringAsync("$RootUrl$($accepted.statusUrl)").GetAwaiter().GetResult()
                $status = $statusJson | ConvertFrom-Json
            } while ($status.processingState -in @('PENDING', 'PROCESSING'))

            $clock.Stop()
            [pscustomobject]@{
                Sequence = $Sequence
                CaseId = $status.caseId
                HttpAcceptedMs = $acceptedMs
                EndToEndMs = $clock.ElapsedMilliseconds
                ProcessingState = $status.processingState
                Actual = $status.actual
                Provider = $status.servedProvider
                FallbackUsed = $status.fallbackUsed
                ErrorCode = $status.providerErrorCode
            }
        }
        finally {
            $client.Dispose()
            $handler.Dispose()
        }
    }
}

try {
    $results = $jobs | Wait-Job | Receive-Job | Sort-Object Sequence
    $results | Format-Table -AutoSize
    $acceptedP95 = ($results.HttpAcceptedMs | Sort-Object)[[Math]::Ceiling($results.Count * 0.95) - 1]
    $endToEndP95 = ($results.EndToEndMs | Sort-Object)[[Math]::Ceiling($results.Count * 0.95) - 1]
    "Accepted P95: $acceptedP95 ms"
    "End-to-end P95: $endToEndP95 ms"
    "Completed: $(@($results | Where-Object ProcessingState -eq 'COMPLETED').Count)/$($results.Count)"
}
finally {
    $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
}
