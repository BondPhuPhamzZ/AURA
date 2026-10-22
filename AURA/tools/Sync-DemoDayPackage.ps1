param(
    [Parameter(Mandatory)][string]$DemoRoot,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [Parameter(Mandatory)][string]$PersonalRoot
)

$ErrorActionPreference = 'Stop'

$AppRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$RepoRoot = (Resolve-Path -LiteralPath (Join-Path $AppRoot '..')).Path
$DemoRoot = [System.IO.Path]::GetFullPath($DemoRoot).TrimEnd('\')
$EvidenceRoot = [System.IO.Path]::GetFullPath($EvidenceRoot)
$PersonalRoot = [System.IO.Path]::GetFullPath($PersonalRoot)

$driveRoot = [System.IO.Path]::GetPathRoot($DemoRoot).TrimEnd('\')
if ($DemoRoot.Equals($driveRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'DemoRoot must be a dedicated subdirectory, not a drive root.'
}

function Ensure-Directory {
    param([Parameter(Mandatory)][string]$Path)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($DemoRoot + '\', [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $fullPath.Equals($DemoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to create path outside Demo Day root: $fullPath"
    }
    [System.IO.Directory]::CreateDirectory($fullPath) | Out-Null
}

function Copy-Required {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Required source is missing: $Source"
    }
    $fullDestination = [System.IO.Path]::GetFullPath($Destination)
    if (-not $fullDestination.StartsWith($DemoRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to copy outside Demo Day root: $fullDestination"
    }
    Ensure-Directory -Path ([System.IO.Path]::GetDirectoryName($fullDestination))
    Copy-Item -LiteralPath $Source -Destination $fullDestination -Force
}

function Copy-Optional {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )
    if (Test-Path -LiteralPath $Source -PathType Leaf) {
        Copy-Required -Source $Source -Destination $Destination
    }
}

$requiredCopies = @(
    # Curated evidence index and current state
    @("$AppRoot\docs\README.md", "$DemoRoot\Evidence\00_INDEX_AND_SCOPE\README.md"),
    @("$AppRoot\docs\CURRENT_PROJECT_STATUS.md", "$DemoRoot\Evidence\00_INDEX_AND_SCOPE\CURRENT_PROJECT_STATUS.md"),
    @("$AppRoot\docs\EVALUATION_EVIDENCE.md", "$DemoRoot\Evidence\00_INDEX_AND_SCOPE\EVALUATION_EVIDENCE.md"),
    @("$AppRoot\docs\SUBMISSION_CHECKLIST.md", "$DemoRoot\Evidence\01_RELEASE_GATES\SUBMISSION_CHECKLIST.md"),
    @("$AppRoot\submission\AURA_BUILD_LOG.pdf", "$DemoRoot\Evidence\01_RELEASE_GATES\AURA_BUILD_LOG.pdf"),
    @("$AppRoot\submission\AURA_WORKFLOW_SPEC.md", "$DemoRoot\Evidence\06_HUMAN_WORKFLOW\AURA_WORKFLOW_SPEC.md"),
    @("$AppRoot\docs\EVIDENCE_LOCALIZATION_PLAN.md", "$DemoRoot\Evidence\07_EVIDENCE_LOCALIZATION\EVIDENCE_LOCALIZATION_PLAN.md"),
    @("$AppRoot\docs\USER_STUDY_PROTOCOL.md", "$DemoRoot\Evidence\08_USER_STUDY_PENDING\USER_STUDY_PROTOCOL.md"),
    @("$AppRoot\submission\AURA_USER_FEEDBACK_FORM.docx", "$DemoRoot\Evidence\08_USER_STUDY_PENDING\AURA_USER_FEEDBACK_FORM.docx"),
    @("$AppRoot\submission\AURA_USER_FEEDBACK_FORM.pdf", "$DemoRoot\Evidence\08_USER_STUDY_PENDING\AURA_USER_FEEDBACK_FORM.pdf"),

    # Personal material stays outside Git; Demo Day receives only the two final guides.
    @("$PersonalRoot\AURA_ON_TAP_TOAN_DIEN_VI.md", "$DemoRoot\Gia_Phu\Final\AURA_ON_TAP_TOAN_DIEN_VI.md"),
    @("$PersonalRoot\AURA_HUONG_DAN_THUYET_TRINH_5_SLIDE_VI.md", "$DemoRoot\Gia_Phu\Presentation\AURA_HUONG_DAN_THUYET_TRINH_5_SLIDE_VI.md"),

    # Video and presentation
    @("$PersonalRoot\AURA_OFFICIAL_VIDEO_SCRIPT_3_MIN_VI.md", "$DemoRoot\kich_ban_video\AURA_OFFICIAL_VIDEO_SCRIPT_3_MIN_VI.md"),
    @("$AppRoot\submission\AURA_5_SLIDES_AFTER_FEEDBACK_FINAL.pptx", "$DemoRoot\kich_ban_thuyettrinh_slide\AURA_5_SLIDES_AFTER_FEEDBACK_FINAL.pptx"),
    @("$PersonalRoot\AURA_HUONG_DAN_THUYET_TRINH_5_SLIDE_VI.md", "$DemoRoot\kich_ban_thuyettrinh_slide\AURA_HUONG_DAN_THUYET_TRINH_5_SLIDE_VI.md"),

    # Upload-ready package
    @("$AppRoot\submission\README.md", "$DemoRoot\Nop\00_READ_FIRST\README.md"),
    @("$AppRoot\submission\AURA_SUBMISSION_LINKS.md", "$DemoRoot\Nop\00_READ_FIRST\AURA_SUBMISSION_LINKS.md"),
    @("$AppRoot\submission\AURA_SUBMISSION_LINKS.docx", "$DemoRoot\Nop\00_READ_FIRST\AURA_SUBMISSION_LINKS.docx"),
    @("$AppRoot\submission\AURA_SUBMISSION_LINKS.pdf", "$DemoRoot\Nop\00_READ_FIRST\AURA_SUBMISSION_LINKS.pdf"),
    @("$AppRoot\submission\AURA_5_SLIDES_AFTER_FEEDBACK_FINAL.pptx", "$DemoRoot\Nop\01_SLIDES\AURA_5_SLIDES_AFTER_FEEDBACK_FINAL.pptx"),
    @("$AppRoot\submission\AURA_PROPOSAL_VI.docx", "$DemoRoot\Nop\02_PROPOSAL\AURA_PROPOSAL_VI.docx"),
    @("$AppRoot\submission\AURA_PROPOSAL_VI.pdf", "$DemoRoot\Nop\02_PROPOSAL\AURA_PROPOSAL_VI.pdf"),
    @("$AppRoot\submission\AURA_PROPOSAL_EN.docx", "$DemoRoot\Nop\02_PROPOSAL\AURA_PROPOSAL_EN.docx"),
    @("$AppRoot\submission\AURA_PROPOSAL_EN.pdf", "$DemoRoot\Nop\02_PROPOSAL\AURA_PROPOSAL_EN.pdf"),
    @("$AppRoot\submission\AURA_BUILD_LOG.md", "$DemoRoot\Nop\04_BUILD_LOG\AURA_BUILD_LOG.md"),
    @("$AppRoot\submission\AURA_BUILD_LOG.docx", "$DemoRoot\Nop\04_BUILD_LOG\AURA_BUILD_LOG.docx"),
    @("$AppRoot\submission\AURA_BUILD_LOG.pdf", "$DemoRoot\Nop\04_BUILD_LOG\AURA_BUILD_LOG.pdf"),
    @("$RepoRoot\README.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\README.md"),
    @("$AppRoot\docs\RUNBOOK.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\RUNBOOK.md"),
    @("$AppRoot\docs\JUDGE_LOCAL_SETUP.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\JUDGE_LOCAL_SETUP.md"),
    @("$AppRoot\docs\TEST_CASES.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\TEST_CASES.md"),
    @("$AppRoot\BUSINESS_RULES.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\BUSINESS_RULES.md"),
    @("$AppRoot\docs\BUSINESS_RULES_VI.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\BUSINESS_RULES_VI.md"),
    @("$AppRoot\submission\AURA_WORKFLOW_SPEC.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\AURA_WORKFLOW_SPEC.md"),
    @("$AppRoot\docs\USER_STUDY_PROTOCOL.md", "$DemoRoot\Nop\06_USER_EVIDENCE\USER_STUDY_PROTOCOL.md"),
    @("$AppRoot\submission\AURA_USER_FEEDBACK_FORM.docx", "$DemoRoot\Nop\06_USER_EVIDENCE\AURA_USER_FEEDBACK_FORM.docx"),
    @("$AppRoot\submission\AURA_USER_FEEDBACK_FORM.pdf", "$DemoRoot\Nop\06_USER_EVIDENCE\AURA_USER_FEEDBACK_FORM.pdf"),
    @("$AppRoot\docs\EVALUATION_EVIDENCE.md", "$DemoRoot\Nop\07_EVIDENCE_SUMMARY\EVALUATION_EVIDENCE.md"),
    @("$AppRoot\docs\CURRENT_PROJECT_STATUS.md", "$DemoRoot\Nop\07_EVIDENCE_SUMMARY\CURRENT_PROJECT_STATUS.md")
)

foreach ($entry in $requiredCopies) {
    Copy-Required -Source $entry[0] -Destination $entry[1]
}

# Remove obsolete personal guidance from the upload-ready package. The source
# remains in the caller-provided private workspace and is copied only to the
# designated rehearsal folders above.
$legacyPersonalTargets = @(
    "$DemoRoot\Nop\00_READ_FIRST\README_FIRST.md",
    "$DemoRoot\Nop\03_VIDEO\AURA_OFFICIAL_VIDEO_SCRIPT_3_MIN_VI.md",
    "$DemoRoot\Nop\03_VIDEO\AURA_OFFICIAL_VIDEO_SCRIPT_3_MIN.md",
    "$DemoRoot\Nop\03_VIDEO\VIDEO_FILE_PENDING.md",
    "$DemoRoot\Nop\04_BUILD_LOG\AURA_BUILD_LOG_1_PAGE.md",
    "$DemoRoot\Nop\04_BUILD_LOG\AURA_BUILD_LOG_1_PAGE.docx",
    "$DemoRoot\Nop\04_BUILD_LOG\AURA_BUILD_LOG_1_PAGE.pdf",

    # Old, duplicated personal rehearsal files superseded by the two final guides.
    "$DemoRoot\Gia_Phu\OnTap\00_DOC_TRUOC_2026-10-14.md",
    "$DemoRoot\Gia_Phu\OnTap\01_SO_TAY_AURA_HIEN_HANH_2026-10-14.md",
    "$DemoRoot\Gia_Phu\OnTap\02_LUONG_XU_LY_VA_MA_NGUON_2026-10-14.md",
    "$DemoRoot\Gia_Phu\OnTap\03_VAN_HANH_DEMO_VA_XU_LY_SU_CO_2026-10-14.md",
    "$DemoRoot\Gia_Phu\OnTap\AURA_CODE_FLOW_VI.md",
    "$DemoRoot\Gia_Phu\OnTap\AURA_DEMO_OPERATIONS_VI.md",
    "$DemoRoot\Gia_Phu\OnTap\AURA_STUDY_GUIDE_VI.md",
    "$DemoRoot\Gia_Phu\OnTap\README.md",
    "$DemoRoot\Gia_Phu\Pitching_QnA\00_KICH_BAN_THUYET_TRINH_VA_QA_VI_2026-10-14.md",
    "$DemoRoot\Gia_Phu\Pitching_QnA\01_QA_KY_THUAT_VA_LUONG_XU_LY_2026-10-14.md",
    "$DemoRoot\Gia_Phu\Pitching_QnA\02_CHECKLIST_DEMO_VA_SU_CO_2026-10-14.md",
    "$DemoRoot\Gia_Phu\Pitching_QnA\AURA_FINAL_PITCH_SCRIPT_AND_QA_VI.md",
    "$DemoRoot\kich_ban_thuyettrinh_slide\00_KICH_BAN_THUYET_TRINH_VA_QA.md",
    "$DemoRoot\kich_ban_thuyettrinh_slide\01_HUONG_DAN_15_PHUT.md",
    "$DemoRoot\kich_ban_thuyettrinh_slide\AURA_5_SLIDES_FINAL_2026-10-13_V2.pptx",
    "$DemoRoot\kich_ban_thuyettrinh_slide\AURA_FINAL_PITCH_SCRIPT_AND_QA_VI.md"
)
foreach ($target in $legacyPersonalTargets) {
    $fullTarget = [System.IO.Path]::GetFullPath($target)
    if (-not $fullTarget.StartsWith($DemoRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside Demo Day root: $fullTarget"
    }
    if (Test-Path -LiteralPath $fullTarget -PathType Leaf) {
        Remove-Item -LiteralPath $fullTarget -Force
    }
}

# Copy only non-sensitive machine-readable summaries when the private evidence workspace is present.
$optionalCopies = @(
    @("$EvidenceRoot\31_qwen38_flash_live_verify_2026-10-12\run-01\concurrent-verify-summary.corrected.json", "$DemoRoot\Evidence\02_LIVE_VERIFY\concurrent-verify-summary.corrected.json"),
    @("$EvidenceRoot\31_qwen38_flash_live_verify_2026-10-12\evidence-sha256.json", "$DemoRoot\Evidence\02_LIVE_VERIFY\evidence-sha256.json"),
    @("$EvidenceRoot\30_qwen38_flash_benchmark_2026-10-12\04_comparison\comparison-summary.json", "$DemoRoot\Evidence\03_MODEL_AND_PROVIDER\comparison-summary.json"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\holdout10_evidence_v3_2026-10-13\07_metrics\official-holdout10-summary.json", "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT\official-holdout10-summary.json"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\holdout10_evidence_v3_2026-10-13\07_metrics\decision-confusion-matrix.csv", "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT\decision-confusion-matrix.csv"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\regression12_original_bytes_v1_2026-10-13\06_run\regression-results.csv", "$DemoRoot\Evidence\05_REGRESSION\original-bytes-regression-results.csv"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\regression12_sequential_repeat_v2_2026-10-13\06_run\regression-results.csv", "$DemoRoot\Evidence\05_REGRESSION\sequential-repeat-regression-results.csv")
)

foreach ($entry in $optionalCopies) {
    Copy-Optional -Source $entry[0] -Destination $entry[1]
}

$hashDirectory = "$DemoRoot\Evidence\09_SUBMISSION_INTEGRITY"
Ensure-Directory -Path $hashDirectory
$hashFiles = Get-ChildItem -LiteralPath "$DemoRoot\Evidence" -Recurse -File |
    Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
    Sort-Object FullName
$hashLines = foreach ($file in $hashFiles) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
    $relative = $file.FullName.Substring(("$DemoRoot\Evidence").Length + 1).Replace('\', '/')
    "$hash  $relative"
}
[System.IO.File]::WriteAllLines("$hashDirectory\SHA256SUMS.txt", $hashLines, [System.Text.UTF8Encoding]::new($false))

$packageCommit = (& git -C $RepoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($packageCommit)) {
    throw 'Unable to resolve repository HEAD.'
}

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString('o')
    packageCommit = $packageCommit
    requiredCopiedFileCount = $requiredCopies.Count
    evidenceHashedFileCount = $hashLines.Count
    rawReceiptsCopied = $false
    officialVideoPending = $false
    threeUserEvidencePending = $true
}
[System.IO.File]::WriteAllText(
    "$DemoRoot\Nop\00_READ_FIRST\PACKAGE_STATUS.json",
    ($summary | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false)
)

Write-Output ($summary | ConvertTo-Json -Depth 4)
