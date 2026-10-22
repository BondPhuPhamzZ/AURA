param(
    [Parameter(Mandatory = $true)][string]$FolderUrlOrId,
    [Parameter(Mandatory = $true)][string]$ServiceAccountJsonPath,
    [switch]$EnableSyntheticOnly
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$key = (Resolve-Path -LiteralPath $ServiceAccountJsonPath).Path
$repository = (git -C $project rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repository)) {
    throw 'Không xác định được Git root.'
}
$repositoryPrefix = $repository.TrimEnd('\') + '\'
if ($key.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'File khóa phải nằm ngoài repository.'
}
if ($FolderUrlOrId -match '/folders/([A-Za-z0-9_-]+)') {
    $folderId = $Matches[1]
} elseif ($FolderUrlOrId -match '^[A-Za-z0-9_-]+$') {
    $folderId = $FolderUrlOrId
} else {
    throw 'Không đọc được folder ID từ URL.'
}
$credentials = Get-Content -LiteralPath $key -Raw | ConvertFrom-Json
if ($credentials.type -ne 'service_account' -or
    $credentials.token_uri -ne 'https://oauth2.googleapis.com/token' -or
    [string]::IsNullOrWhiteSpace($credentials.client_email) -or
    [string]::IsNullOrWhiteSpace($credentials.private_key)) {
    throw 'JSON không phải Google service-account key hợp lệ.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Không tìm thấy dotnet SDK.'
}

function Set-UserSecret([string]$name, [string]$value) {
    dotnet user-secrets set $name $value --project $project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw ('Không lưu được user-secret: ' + $name) }
}
Set-UserSecret 'DriveSandbox:FolderId' $folderId
Set-UserSecret 'DriveSandbox:ServiceAccountJsonPath' $key
Set-UserSecret 'DriveSandbox:Enabled' ([string][bool]$EnableSyntheticOnly).ToLowerInvariant()
Set-UserSecret 'DriveSandbox:RequireMalwareScanner' ([string](-not [bool]$EnableSyntheticOnly)).ToLowerInvariant()

Write-Output 'Cấu hình nằm trong .NET user-secrets; JSON key vẫn ở đường dẫn ngoài Git, không được copy vào repository.'
Write-Output ('Chỉ chia sẻ thư mục test ở quyền Viewer cho service account: ' + $credentials.client_email)
if ($EnableSyntheticOnly) {
    Write-Warning 'Đã bật connector chỉ cho ảnh synthetic, Development local. Không chạy với hóa đơn thật.'
} else {
    Write-Output 'Connector vẫn tắt. Sau khi chia sẻ thư mục và đặt ảnh synthetic, chạy lại với -EnableSyntheticOnly.'
}
