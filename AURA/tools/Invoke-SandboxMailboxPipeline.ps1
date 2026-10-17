param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$ApiKey,

    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [string]$Sender = "demo.sender@example.test",
    [string]$Folder = "AURA_INVOICE",
    [int]$TimeoutSeconds = 180,
    [switch]$RepeatDelivery
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd('/')
$resolvedImage = (Resolve-Path -LiteralPath $ImagePath).Path
$messageId = "sandbox-message-$([Guid]::NewGuid().ToString('N'))"
$attachmentId = "attachment-1"
$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.DefaultRequestHeaders.Add("X-AURA-Sandbox-Key", $ApiKey)

function Send-Delivery {
    param([string]$CurrentMessageId, [string]$CurrentAttachmentId)

    $form = [System.Net.Http.MultipartFormDataContent]::new()
    try {
        $fields = [ordered]@{
            MessageId = $CurrentMessageId
            ThreadId = "sandbox-thread-$CurrentMessageId"
            AttachmentId = $CurrentAttachmentId
            Sender = $Sender
            Subject = "Synthetic AURA receipt"
            Folder = $Folder
            ReceivedAt = [DateTimeOffset]::UtcNow.ToString("O")
        }
        foreach ($field in $fields.GetEnumerator()) {
            $form.Add([System.Net.Http.StringContent]::new([string]$field.Value), [string]$field.Key)
        }

        $fileBytes = [System.IO.File]::ReadAllBytes($resolvedImage)
        $fileContent = [System.Net.Http.ByteArrayContent]::new($fileBytes)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse(
            $(if ([System.IO.Path]::GetExtension($resolvedImage) -ieq ".png") { "image/png" } else { "image/jpeg" }))
        $form.Add($fileContent, "Attachment", [System.IO.Path]::GetFileName($resolvedImage))

        $response = $httpClient.PostAsync("$base/api/mailbox-sandbox/deliver", $form).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "Delivery returned HTTP $([int]$response.StatusCode): $body"
        }
        return $body | ConvertFrom-Json
    }
    finally {
        $form.Dispose()
    }
}

$status = Invoke-RestMethod -Method Get -Uri "$base/api/mailbox-sandbox/status" -TimeoutSec 15
if (-not $status.enabled) { throw "Mailbox sandbox is disabled at $base." }

$delivery = Send-Delivery -CurrentMessageId $messageId -CurrentAttachmentId $attachmentId
if ($delivery.outcome -ne "ACCEPTED" -or [string]::IsNullOrWhiteSpace($delivery.requestId)) {
    throw "Expected ACCEPTED delivery but received: $($delivery | ConvertTo-Json -Depth 8 -Compress)"
}

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
$terminal = $null
while ([DateTimeOffset]::UtcNow -lt $deadline) {
    $terminal = Invoke-RestMethod -Method Get -Uri "$base$($delivery.statusUrl)" -TimeoutSec 15
    if ($terminal.processingState -in @("COMPLETED", "FAILED")) { break }
    Start-Sleep -Seconds 1
}

if ($null -eq $terminal -or $terminal.processingState -notin @("COMPLETED", "FAILED")) {
    throw "Mailbox job did not reach a terminal state within $TimeoutSeconds seconds."
}

$duplicate = $null
if ($RepeatDelivery) {
    $duplicate = Send-Delivery -CurrentMessageId $messageId -CurrentAttachmentId $attachmentId
    if ($duplicate.outcome -ne "DUPLICATE" -or $duplicate.requestId -ne $delivery.requestId) {
        throw "Idempotency failed: $($duplicate | ConvertTo-Json -Depth 8 -Compress)"
    }
}

[ordered]@{
    schemaVersion = 1
    baseUrl = $base
    testedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    syntheticOnly = $true
    messageId = $messageId
    attachmentId = $attachmentId
    accepted = $delivery
    terminal = $terminal
    duplicateReplay = $duplicate
} | ConvertTo-Json -Depth 12

$httpClient.Dispose()
