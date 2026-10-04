[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://')]
    [string]$BaseUrl = 'http://localhost:5000',

    [Parameter()]
    [ValidateRange(1, 100)]
    [int]$MaxFileSizeMb = 5,

    [Parameter()]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

$normalizedBaseUrl = $BaseUrl.TrimEnd('/')
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.CookieContainer = [System.Net.CookieContainer]::new()
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromMinutes(2)

function Invoke-RejectedUpload {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [string]$FileName,
        [Parameter(Mandatory)] [string]$ContentType,
        [Parameter(Mandatory)] [byte[]]$Bytes,
        [Parameter(Mandatory)] [string]$ExpectedErrorFragment
    )

    $homeHtml = $client.GetStringAsync("$normalizedBaseUrl/").GetAwaiter().GetResult()
    $tokenMatch = [regex]::Match($homeHtml, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $tokenMatch.Success) {
        throw 'Antiforgery token was not found on the home page.'
    }

    $multipart = [System.Net.Http.MultipartFormDataContent]::new()
    try {
        $fileContent = [System.Net.Http.ByteArrayContent]::new($Bytes)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse($ContentType)
        $multipart.Add($fileContent, 'receiptFile', $FileName)
        $multipart.Add([System.Net.Http.StringContent]::new('100000'), 'claimedAmount')
        $multipart.Add([System.Net.Http.StringContent]::new('SECURITY-NEGATIVE'), 'submitterCode')
        $multipart.Add([System.Net.Http.StringContent]::new('Upload validation smoke'), 'submitterDisplayName')
        $multipart.Add([System.Net.Http.StringContent]::new('Security QA'), 'submitterDepartment')
        $multipart.Add([System.Net.Http.StringContent]::new($tokenMatch.Groups[1].Value), '__RequestVerificationToken')

        $clock = [System.Diagnostics.Stopwatch]::StartNew()
        $response = $client.PostAsync("$normalizedBaseUrl/Applicant/UploadReceipt", $multipart).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $clock.Stop()

        $passed = [int]$response.StatusCode -eq 400 -and $body.Contains($ExpectedErrorFragment)
        [pscustomobject]@{
            test = $Name
            passed = $passed
            httpStatus = [int]$response.StatusCode
            expectedHttpStatus = 400
            expectedErrorFragment = $ExpectedErrorFragment
            responseBody = $body
            payloadBytes = $Bytes.Length
            elapsedMs = $clock.ElapsedMilliseconds
            testedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
    }
    finally {
        $multipart.Dispose()
    }
}

try {
    # A .jpg filename and MIME type are not sufficient; the byte signature must
    # also be a real JPEG. This payload deliberately has no JPEG magic bytes.
    $fakeJpeg = [Text.Encoding]::UTF8.GetBytes('This is not a JPEG image.')
    $invalidSignature = Invoke-RejectedUpload `
        -Name 'invalid-jpeg-signature' `
        -FileName 'invalid-signature.jpg' `
        -ContentType 'image/jpeg' `
        -Bytes $fakeJpeg `
        -ExpectedErrorFragment 'JPG/PNG'

    # The size check runs before signature validation, storage and queueing.
    $oversizeBytes = [byte[]]::new(($MaxFileSizeMb * 1MB) + 1)
    $oversizeBytes[0] = 0xFF
    $oversizeBytes[1] = 0xD8
    $oversizeBytes[2] = 0xFF
    $oversize = Invoke-RejectedUpload `
        -Name 'oversize-jpeg' `
        -FileName 'oversize.jpg' `
        -ContentType 'image/jpeg' `
        -Bytes $oversizeBytes `
        -ExpectedErrorFragment 'MB'

    $report = [pscustomobject]@{
        schemaVersion = 1
        baseUrl = $normalizedBaseUrl
        note = 'Rejected before receipt storage, database insertion and AI queue signaling.'
        passed = $invalidSignature.passed -and $oversize.passed
        tests = @($invalidSignature, $oversize)
    }

    $json = $report | ConvertTo-Json -Depth 6
    $json

    if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
        New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
        $outputPath = Join-Path $OutputDirectory 'upload-validation-negative.json'
        [IO.File]::WriteAllText($outputPath, $json, [Text.UTF8Encoding]::new($false))
        Write-Host "Saved: $outputPath"
    }

    if (-not $report.passed) {
        exit 1
    }
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
