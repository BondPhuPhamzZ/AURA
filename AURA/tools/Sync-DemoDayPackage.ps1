param()

$ErrorActionPreference = 'Stop'

$AppRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$RepoRoot = (Resolve-Path -LiteralPath (Join-Path $AppRoot '..')).Path
$DemoRoot = 'D:\aura\demo_Day'
$PrivateRoot = 'D:\aura\phan_bien'
$EvidenceRoot = 'D:\aura\demo_evidence'

if (-not $DemoRoot.StartsWith('D:\aura\demo_Day', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe DemoRoot: $DemoRoot"
}

function Ensure-Directory {
    param([Parameter(Mandatory)][string]$Path)
    if (-not $Path.StartsWith($DemoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to create path outside Demo Day root: $Path"
    }
    [System.IO.Directory]::CreateDirectory($Path) | Out-Null
}

function Copy-Required {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Required source is missing: $Source"
    }
    if (-not $Destination.StartsWith($DemoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to copy outside Demo Day root: $Destination"
    }
    Ensure-Directory -Path ([System.IO.Path]::GetDirectoryName($Destination))
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

$directories = @(
    "$DemoRoot\Evidence\00_INDEX_AND_SCOPE",
    "$DemoRoot\Evidence\01_RELEASE_GATES",
    "$DemoRoot\Evidence\02_LIVE_HEALTH_AND_VERIFY",
    "$DemoRoot\Evidence\03_MODEL_AND_PROVIDER",
    "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT",
    "$DemoRoot\Evidence\05_REGRESSION_AND_CONCURRENCY",
    "$DemoRoot\Evidence\06_HUMAN_WORKFLOW_AND_MOBILE",
    "$DemoRoot\Evidence\07_EVIDENCE_LOCALIZATION",
    "$DemoRoot\Evidence\08_USER_STUDY_PENDING",
    "$DemoRoot\Evidence\09_SUBMISSION_INTEGRITY",
    "$DemoRoot\Gia_Phu\OnTap",
    "$DemoRoot\Gia_Phu\Pitching_QnA",
    "$DemoRoot\kich_ban_video",
    "$DemoRoot\kich_ban_thuyettrinh_slide",
    "$DemoRoot\Nop\00_READ_FIRST",
    "$DemoRoot\Nop\01_SLIDES",
    "$DemoRoot\Nop\02_PROPOSAL",
    "$DemoRoot\Nop\03_VIDEO",
    "$DemoRoot\Nop\04_BUILD_LOG",
    "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK",
    "$DemoRoot\Nop\06_USER_EVIDENCE",
    "$DemoRoot\Nop\07_EVIDENCE_SUMMARY"
)
$directories | ForEach-Object { Ensure-Directory -Path $_ }

$copies = @(
    # Evidence index and scope
    @("$AppRoot\docs\DEMO_DAY_EVIDENCE_INDEX_2026-10-13.md", "$DemoRoot\Evidence\00_INDEX_AND_SCOPE\00_EVIDENCE_INDEX.md"),
    @("$AppRoot\docs\CURRENT_PROJECT_STATUS_2026-10-13.md", "$DemoRoot\Evidence\00_INDEX_AND_SCOPE\CURRENT_PROJECT_STATUS_2026-10-13.md"),
    @("$AppRoot\docs\FINAL_READINESS_GAPS_2026-10-13.md", "$DemoRoot\Evidence\00_INDEX_AND_SCOPE\FINAL_READINESS_GAPS_2026-10-13.md"),
    @("$AppRoot\docs\FINAL_LIVE_HEALTH_RECHECK_2026-10-14.md", "$DemoRoot\Evidence\00_INDEX_AND_SCOPE\FINAL_LIVE_HEALTH_RECHECK_2026-10-14.md"),
    @("$AppRoot\submission\AURA_DEMO_DAY_PACKAGE_README_2026-10-13.md", "$DemoRoot\Evidence\00_INDEX_AND_SCOPE\README_FIRST.md"),

    # Release gates
    @("$AppRoot\docs\BUILD_LOG.md", "$DemoRoot\Evidence\01_RELEASE_GATES\BUILD_LOG.md"),
    @("$AppRoot\docs\FINAL_LOCAL_GATE_2026-10-14.md", "$DemoRoot\Evidence\01_RELEASE_GATES\FINAL_LOCAL_GATE_2026-10-14.md"),
    @("$AppRoot\docs\SUBMISSION_CHECKLIST.md", "$DemoRoot\Evidence\01_RELEASE_GATES\SUBMISSION_CHECKLIST.md"),
    @("$AppRoot\submission\AURA_BUILD_LOG_1_PAGE_2026-10-13.pdf", "$DemoRoot\Evidence\01_RELEASE_GATES\AURA_BUILD_LOG_1_PAGE_2026-10-13.pdf"),

    # Live health and Verify
    @("$AppRoot\docs\QWEN38_FLASH_LIVE_VERIFY_2026-10-12.md", "$DemoRoot\Evidence\02_LIVE_HEALTH_AND_VERIFY\QWEN38_FLASH_LIVE_VERIFY_2026-10-12.md"),
    @("$EvidenceRoot\31_qwen38_flash_live_verify_2026-10-12\healthz-before-verify.json", "$DemoRoot\Evidence\02_LIVE_HEALTH_AND_VERIFY\healthz-before-verify.json"),
    @("$EvidenceRoot\31_qwen38_flash_live_verify_2026-10-12\run-01\concurrent-verify-summary.corrected.json", "$DemoRoot\Evidence\02_LIVE_HEALTH_AND_VERIFY\concurrent-verify-summary.corrected.json"),
    @("$EvidenceRoot\31_qwen38_flash_live_verify_2026-10-12\evidence-sha256.json", "$DemoRoot\Evidence\02_LIVE_HEALTH_AND_VERIFY\evidence-sha256.json"),

    # Model and provider
    @("$AppRoot\docs\QWEN38_FLASH_BENCHMARK_2026-10-12.md", "$DemoRoot\Evidence\03_MODEL_AND_PROVIDER\QWEN38_FLASH_BENCHMARK_2026-10-12.md"),
    @("$AppRoot\docs\PROVIDER_CAPACITY_HARDENING_2026-10-12.md", "$DemoRoot\Evidence\03_MODEL_AND_PROVIDER\PROVIDER_CAPACITY_HARDENING_2026-10-12.md"),
    @("$EvidenceRoot\30_qwen38_flash_benchmark_2026-10-12\04_comparison\comparison-summary.json", "$DemoRoot\Evidence\03_MODEL_AND_PROVIDER\comparison-summary.json"),
    @("$EvidenceRoot\30_qwen38_flash_benchmark_2026-10-12\04_comparison\evidence-sha256.json", "$DemoRoot\Evidence\03_MODEL_AND_PROVIDER\evidence-sha256.json"),

    # Prospective holdout: summaries only, no receipt images/raw provider payloads
    @("$AppRoot\docs\OFFICIAL_HOLDOUT10_2026-10-13.md", "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT\OFFICIAL_HOLDOUT10_2026-10-13.md"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\holdout10_evidence_v3_2026-10-13\09_documentation_update\official-holdout10-summary.corrected-utf8.md", "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT\official-holdout10-summary.corrected-utf8.md"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\holdout10_evidence_v3_2026-10-13\07_metrics\official-holdout10-summary.json", "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT\official-holdout10-summary.json"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\holdout10_evidence_v3_2026-10-13\07_metrics\decision-confusion-matrix.csv", "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT\decision-confusion-matrix.csv"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\holdout10_evidence_v3_2026-10-13\07_metrics\critical-field-comparison.csv", "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT\critical-field-comparison.csv"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\holdout10_evidence_v3_2026-10-13\07_metrics\latency-and-provider-per-case.csv", "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT\latency-and-provider-per-case.csv"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\holdout10_evidence_v3_2026-10-13\09_documentation_update\mismatch-register.corrected-utf8.json", "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT\mismatch-register.corrected-utf8.json"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\holdout10_evidence_v3_2026-10-13\09_documentation_update\package-final.sha256", "$DemoRoot\Evidence\04_PROSPECTIVE_HOLDOUT\package-final.sha256"),

    # Regression and concurrency
    @("$AppRoot\docs\REGRESSION12_ORIGINAL_BYTES_RESULT_2026-10-13.md", "$DemoRoot\Evidence\05_REGRESSION_AND_CONCURRENCY\REGRESSION12_ORIGINAL_BYTES_RESULT_2026-10-13.md"),
    @("$AppRoot\docs\REGRESSION12_SEQUENTIAL_REPEAT_RESULT_2026-10-13.md", "$DemoRoot\Evidence\05_REGRESSION_AND_CONCURRENCY\REGRESSION12_SEQUENTIAL_REPEAT_RESULT_2026-10-13.md"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\regression12_original_bytes_v1_2026-10-13\06_run\regression-results.csv", "$DemoRoot\Evidence\05_REGRESSION_AND_CONCURRENCY\original-bytes-regression-results.csv"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\regression12_original_bytes_v1_2026-10-13\08_integrity\post-run-evidence-hashes.json", "$DemoRoot\Evidence\05_REGRESSION_AND_CONCURRENCY\original-bytes-post-run-evidence-hashes.json"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\regression12_sequential_repeat_v2_2026-10-13\06_run\regression-results.csv", "$DemoRoot\Evidence\05_REGRESSION_AND_CONCURRENCY\sequential-repeat-regression-results.csv"),
    @("$EvidenceRoot\22_NewRealReceipt_ForHoldout\regression12_sequential_repeat_v2_2026-10-13\08_integrity\post-run-evidence-hashes.json", "$DemoRoot\Evidence\05_REGRESSION_AND_CONCURRENCY\sequential-repeat-post-run-evidence-hashes.json"),

    # Human workflow and localization boundary
    @("$AppRoot\docs\LIVE_POST_HOLDOUT_VALIDATION_2026-10-06.md", "$DemoRoot\Evidence\06_HUMAN_WORKFLOW_AND_MOBILE\LIVE_POST_HOLDOUT_VALIDATION_2026-10-06.md"),
    @("$AppRoot\submission\AURA_WORKFLOW_SPEC.md", "$DemoRoot\Evidence\06_HUMAN_WORKFLOW_AND_MOBILE\AURA_WORKFLOW_SPEC.md"),
    @("$AppRoot\docs\EVIDENCE_LOCALIZATION_PLAN_2026-10-13.md", "$DemoRoot\Evidence\07_EVIDENCE_LOCALIZATION\EVIDENCE_LOCALIZATION_PLAN_2026-10-13.md"),
    @("$PrivateRoot\28_EVIDENCE_LOCALIZATION_IMPLEMENTATION_DECISION_2026-10-13.md", "$DemoRoot\Evidence\07_EVIDENCE_LOCALIZATION\IMPLEMENTATION_DECISION_2026-10-13.md"),

    # User study is explicitly pending
    @("$AppRoot\docs\USER_STUDY_PROTOCOL_2026-10-13.md", "$DemoRoot\Evidence\08_USER_STUDY_PENDING\USER_STUDY_PROTOCOL_2026-10-13.md"),
    @("$AppRoot\submission\AURA_USER_FEEDBACK_FORM_2026-10-13.docx", "$DemoRoot\Evidence\08_USER_STUDY_PENDING\AURA_USER_FEEDBACK_FORM_2026-10-13.docx"),
    @("$AppRoot\submission\AURA_USER_FEEDBACK_FORM_2026-10-13.pdf", "$DemoRoot\Evidence\08_USER_STUDY_PENDING\AURA_USER_FEEDBACK_FORM_2026-10-13.pdf"),
    @("$AppRoot\submission\USER_FEEDBACK_EVIDENCE_PENDING.md", "$DemoRoot\Evidence\08_USER_STUDY_PENDING\PENDING.md"),
    @("$AppRoot\.tmp\slides-v2\AURA_5_SLIDES_FINAL_2026-10-13_V2.validation.json", "$DemoRoot\Evidence\09_SUBMISSION_INTEGRITY\slide-validation.json"),

    # Personal study set
    @("$AppRoot\submission\AURA_PERSONAL_STUDY_INDEX_2026-10-13.md", "$DemoRoot\Gia_Phu\OnTap\00_READ_ME_FIRST.md"),
    @("$PrivateRoot\00_AURA_PERSONAL_MASTER_INDEX.md", "$DemoRoot\Gia_Phu\OnTap\01_PERSONAL_MASTER_INDEX.md"),
    @("$PrivateRoot\16_AURA_FINAL_STUDY_GUIDE_2026-10-07.md", "$DemoRoot\Gia_Phu\OnTap\02_FINAL_STUDY_GUIDE.md"),
    @("$PrivateRoot\Project_Folder_Architecture\explain\01_FINAL_REQUEST_FLOW_AND_PROMPT_LAYERS.md", "$DemoRoot\Gia_Phu\OnTap\03_REQUEST_FLOW_AND_PROMPT_LAYERS.md"),
    @("$PrivateRoot\Project_Folder_Architecture\explain\02_SOURCE_CODE_AND_FOLDER_REFERENCE.md", "$DemoRoot\Gia_Phu\OnTap\04_SOURCE_CODE_AND_FOLDER_REFERENCE.md"),
    @("$PrivateRoot\explain\AURA_PROJECT_ARCHITECTURE_GUIDE.md", "$DemoRoot\Gia_Phu\OnTap\05_PROJECT_ARCHITECTURE_GUIDE.md"),
    @("$PrivateRoot\explain\AURA_TESTING_DATA_AND_IMPLEMENTATION_WORKFLOW_GUIDE.md", "$DemoRoot\Gia_Phu\OnTap\06_TESTING_DATA_AND_WORKFLOW_GUIDE.md"),
    @("$PrivateRoot\20_FALLBACK_AND_429_FINAL_STRATEGY_2026-10-12.md", "$DemoRoot\Gia_Phu\OnTap\07_FALLBACK_AND_429_STRATEGY.md"),
    @("$PrivateRoot\21_LIVE_CAPACITY_AND_MULTI_JUDGE_DEFENSE_2026-10-12.md", "$DemoRoot\Gia_Phu\OnTap\08_CAPACITY_AND_MULTI_JUDGE_DEFENSE.md"),
    @("$PrivateRoot\23_OFFICIAL_HOLDOUT10_AND_DEMO_DECISION_2026-10-13.md", "$DemoRoot\Gia_Phu\OnTap\09_OFFICIAL_HOLDOUT_AND_DEMO_DECISION.md"),
    @("$PrivateRoot\25_REGRESSION12_ORIGINAL_BYTES_AND_CONCURRENCY_2026-10-13.md", "$DemoRoot\Gia_Phu\OnTap\10_REGRESSION12_ORIGINAL_BYTES_AND_CONCURRENCY.md"),
    @("$PrivateRoot\26_REGRESSION12_SEQUENTIAL_REPEAT_2026-10-13.md", "$DemoRoot\Gia_Phu\OnTap\11_REGRESSION12_SEQUENTIAL_REPEAT.md"),
    @("$PrivateRoot\28_EVIDENCE_LOCALIZATION_IMPLEMENTATION_DECISION_2026-10-13.md", "$DemoRoot\Gia_Phu\OnTap\12_EVIDENCE_LOCALIZATION_DECISION.md"),
    @("$PrivateRoot\07_ONSITE_FINAL_PLAYBOOK_2026-10-06.md", "$DemoRoot\Gia_Phu\OnTap\13_ONSITE_FINAL_PLAYBOOK.md"),

    # Pitching and Q&A
    @("$AppRoot\submission\AURA_FINAL_PITCH_SCRIPT_AND_QA_2026-10-13.md", "$DemoRoot\Gia_Phu\Pitching_QnA\00_FINAL_PITCH_SCRIPT_AND_QA.md"),
    @("$PrivateRoot\17_AURA_FINAL_PITCH_AND_JUDGE_QA_2026-10-07.md", "$DemoRoot\Gia_Phu\Pitching_QnA\01_JUDGE_QA_REFERENCE.md"),
    @("$PrivateRoot\18_AURA_FINAL_DEFENSE_SOURCE_CODE_QA_2026-10-07.md", "$DemoRoot\Gia_Phu\Pitching_QnA\02_SOURCE_CODE_QA.md"),
    @("$PrivateRoot\27_FINAL_15_MINUTE_PRESENTATION_AND_DEMO_2026-10-13.md", "$DemoRoot\Gia_Phu\Pitching_QnA\03_PRESENTATION_AND_DEMO_REFERENCE.md"),

    # Video and presentation scripts
    @("$AppRoot\submission\AURA_OFFICIAL_VIDEO_SCRIPT_3_MIN_2026-10-13.md", "$DemoRoot\kich_ban_video\00_KICH_BAN_VIDEO_3_PHUT.md"),
    @("$AppRoot\submission\VIDEO_FILE_PENDING.md", "$DemoRoot\kich_ban_video\01_VIDEO_FILE_PENDING.md"),
    @("$AppRoot\submission\AURA_5_SLIDES_FINAL_2026-10-13_V2.pptx", "$DemoRoot\kich_ban_thuyettrinh_slide\AURA_5_SLIDES_FINAL_2026-10-13_V2.pptx"),
    @("$AppRoot\submission\AURA_FINAL_PITCH_SCRIPT_AND_QA_2026-10-13.md", "$DemoRoot\kich_ban_thuyettrinh_slide\00_KICH_BAN_THUYET_TRINH_VA_QA.md"),
    @("$AppRoot\submission\AURA_FINAL_15_MINUTE_PRESENTATION_GUIDE_2026-10-13.md", "$DemoRoot\kich_ban_thuyettrinh_slide\01_HUONG_DAN_15_PHUT.md"),

    # Upload-ready package
    @("$AppRoot\submission\AURA_DEMO_DAY_PACKAGE_README_2026-10-13.md", "$DemoRoot\Nop\00_READ_FIRST\README_FIRST.md"),
    @("$AppRoot\submission\AURA_SUBMISSION_LINKS_2026-10-13.md", "$DemoRoot\Nop\00_READ_FIRST\AURA_SUBMISSION_LINKS.md"),
    @("$AppRoot\submission\AURA_SUBMISSION_LINKS_2026-10-13.docx", "$DemoRoot\Nop\00_READ_FIRST\AURA_SUBMISSION_LINKS.docx"),
    @("$AppRoot\submission\AURA_SUBMISSION_LINKS_2026-10-13.pdf", "$DemoRoot\Nop\00_READ_FIRST\AURA_SUBMISSION_LINKS.pdf"),
    @("$AppRoot\docs\FINAL_DRIVE_SUBMISSION_PACKAGE_2026-10-13.md", "$DemoRoot\Nop\00_READ_FIRST\FINAL_DRIVE_SUBMISSION_CHECKLIST.md"),
    @("$AppRoot\submission\AURA_5_SLIDES_FINAL_2026-10-13_V2.pptx", "$DemoRoot\Nop\01_SLIDES\AURA_5_SLIDES_FINAL.pptx"),
    @("$AppRoot\submission\AURA_PROPOSAL_VI_2026-10-13.docx", "$DemoRoot\Nop\02_PROPOSAL\AURA_PROPOSAL_VI.docx"),
    @("$AppRoot\submission\AURA_PROPOSAL_VI_2026-10-13.pdf", "$DemoRoot\Nop\02_PROPOSAL\AURA_PROPOSAL_VI.pdf"),
    @("$AppRoot\submission\AURA_PROPOSAL_EN_2026-10-13.docx", "$DemoRoot\Nop\02_PROPOSAL\AURA_PROPOSAL_EN.docx"),
    @("$AppRoot\submission\AURA_PROPOSAL_EN_2026-10-13.pdf", "$DemoRoot\Nop\02_PROPOSAL\AURA_PROPOSAL_EN.pdf"),
    @("$AppRoot\submission\AURA_OFFICIAL_VIDEO_SCRIPT_3_MIN_2026-10-13.md", "$DemoRoot\Nop\03_VIDEO\AURA_OFFICIAL_VIDEO_SCRIPT_3_MIN.md"),
    @("$AppRoot\submission\VIDEO_FILE_PENDING.md", "$DemoRoot\Nop\03_VIDEO\VIDEO_FILE_PENDING.md"),
    @("$AppRoot\submission\AURA_BUILD_LOG_1_PAGE_2026-10-13.md", "$DemoRoot\Nop\04_BUILD_LOG\AURA_BUILD_LOG_1_PAGE.md"),
    @("$AppRoot\submission\AURA_BUILD_LOG_1_PAGE_2026-10-13.docx", "$DemoRoot\Nop\04_BUILD_LOG\AURA_BUILD_LOG_1_PAGE.docx"),
    @("$AppRoot\submission\AURA_BUILD_LOG_1_PAGE_2026-10-13.pdf", "$DemoRoot\Nop\04_BUILD_LOG\AURA_BUILD_LOG_1_PAGE.pdf"),
    @("$RepoRoot\README.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\README.md"),
    @("$AppRoot\docs\TEST_CASES.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\TEST_CASES.md"),
    @("$AppRoot\docs\RUNBOOK.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\RUNBOOK.md"),
    @("$AppRoot\docs\FINAL_LOCAL_GATE_2026-10-14.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\FINAL_LOCAL_GATE.md"),
    @("$AppRoot\BUSINESS_RULES.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\BUSINESS_RULES.md"),
    @("$AppRoot\submission\AURA_WORKFLOW_SPEC.md", "$DemoRoot\Nop\05_GITHUB_AND_RUNBOOK\AURA_WORKFLOW_SPEC.md"),
    @("$AppRoot\docs\USER_STUDY_PROTOCOL_2026-10-13.md", "$DemoRoot\Nop\06_USER_EVIDENCE\USER_STUDY_PROTOCOL.md"),
    @("$AppRoot\submission\AURA_USER_FEEDBACK_FORM_2026-10-13.docx", "$DemoRoot\Nop\06_USER_EVIDENCE\AURA_USER_FEEDBACK_FORM.docx"),
    @("$AppRoot\submission\AURA_USER_FEEDBACK_FORM_2026-10-13.pdf", "$DemoRoot\Nop\06_USER_EVIDENCE\AURA_USER_FEEDBACK_FORM.pdf"),
    @("$AppRoot\submission\USER_FEEDBACK_EVIDENCE_PENDING.md", "$DemoRoot\Nop\06_USER_EVIDENCE\USER_FEEDBACK_EVIDENCE_PENDING.md"),
    @("$AppRoot\docs\DEMO_DAY_EVIDENCE_INDEX_2026-10-13.md", "$DemoRoot\Nop\07_EVIDENCE_SUMMARY\EVIDENCE_INDEX.md"),
    @("$AppRoot\docs\CURRENT_PROJECT_STATUS_2026-10-13.md", "$DemoRoot\Nop\07_EVIDENCE_SUMMARY\CURRENT_PROJECT_STATUS.md"),
    @("$AppRoot\docs\FINAL_LIVE_HEALTH_RECHECK_2026-10-14.md", "$DemoRoot\Nop\07_EVIDENCE_SUMMARY\FINAL_LIVE_HEALTH_RECHECK.md"),
    @("$AppRoot\docs\OFFICIAL_HOLDOUT10_2026-10-13.md", "$DemoRoot\Nop\07_EVIDENCE_SUMMARY\OFFICIAL_HOLDOUT10.md")
)

foreach ($entry in $copies) {
    Copy-Required -Source $entry[0] -Destination $entry[1]
}

# Add the new private personal handoff without modifying historical notes.
Copy-Item -LiteralPath "$AppRoot\submission\AURA_PERSONAL_STUDY_INDEX_2026-10-13.md" -Destination "$PrivateRoot\29_FINAL_SUBMISSION_STUDY_INDEX_2026-10-13.md" -Force

# Generate deterministic package hashes after all copies. Raw receipt images are intentionally absent.
$hashRoot = "$DemoRoot\Evidence"
$hashFiles = Get-ChildItem -LiteralPath $hashRoot -Recurse -File |
    Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
    Sort-Object FullName
$hashLines = foreach ($file in $hashFiles) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
    $relative = $file.FullName.Substring($hashRoot.Length + 1).Replace('\', '/')
    "$hash  $relative"
}
[System.IO.File]::WriteAllLines("$DemoRoot\Evidence\09_SUBMISSION_INTEGRITY\SHA256SUMS.txt", $hashLines, [System.Text.UTF8Encoding]::new($false))

$frozenCommit = (& git -C $RepoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($frozenCommit)) {
    throw 'Unable to resolve the repository HEAD for PACKAGE_STATUS.json.'
}

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString('o')
    demoRoot = $DemoRoot
    releaseTag = 'aura-demo-day-2026-10-14'
    frozenCommit = $frozenCommit
    copiedFileCount = $copies.Count
    evidenceHashedFileCount = $hashLines.Count
    rawReceiptsCopied = $false
    officialVideoPending = $true
    threeUserEvidencePending = $true
}
[System.IO.File]::WriteAllText(
    "$DemoRoot\Nop\00_READ_FIRST\PACKAGE_STATUS.json",
    ($summary | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false)
)

Write-Output ($summary | ConvertTo-Json -Depth 4)
