#Requires -Version 7.0
<#
.SYNOPSIS
    Regression and Negative Validation Suite for 6657 Danmaku Annotation Infrastructure (Taxonomy v1)
.DESCRIPTION
    Validates:
    1. Existence of core schema, taxonomy, manifest, validator, and calibration samples.
    2. Python validator CLI execution on valid infrastructure.
    3. Python validator CLI execution on calibration samples.
    4. Negative tests:
       - Extra properties rejection (additionalProperties: false)
       - Raw text leakage rejection (text/content field forbidden)
       - Batch range incompleteness rejection (broken index continuity)
       - Duplicate tag rejection in array dimensions
       - Safety logical contradiction rejection (profanity with safe severity)
       - Safety flags illegal combination rejection (none mixed with risk flag)
       - Pending review reviewer anti-spoofing rejection
       - Invalid entity type / extra entity fields rejection
       - Default check-coverage exit code matches current completion state
       - Check-coverage exit code 0 when --allow-incomplete specified
#>

$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$annotationRoot = Join-Path $RepositoryRoot 'Widget/Danmaku/Annotation'

Write-Host "=== 1. Checking Infrastructure Files ===" -ForegroundColor Cyan
$requiredFiles = @(
    'taxonomy.v1.json',
    'schema.v1.json',
    'entity_aliases.v1.json',
    'annotation_guidelines.v1.md',
    'manifest.json',
    'validate_annotations.py',
    'calibration_samples/calibration_sample_batch.json'
)

foreach ($file in $requiredFiles) {
    $fullPath = Join-Path $annotationRoot $file
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Danmaku annotation infrastructure file missing: $file"
    }
}
Write-Host "  -> All infrastructure files exist." -ForegroundColor Green

Write-Host "=== 2. Running Positive Infrastructure Validation ===" -ForegroundColor Cyan
$pythonScript = Join-Path $annotationRoot 'validate_annotations.py'
$res = & python $pythonScript --repo-root $RepositoryRoot --verify-infra 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Danmaku annotation infrastructure --verify-infra failed:`n$res"
}
Write-Host "  -> --verify-infra passed successfully." -ForegroundColor Green

Write-Host "=== 3. Running Positive Calibration Sample Validation ===" -ForegroundColor Cyan
$res = & python $pythonScript --repo-root $RepositoryRoot --validate-calibration 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Calibration sample validation failed:`n$res"
}
Write-Host "  -> --validate-calibration passed successfully." -ForegroundColor Green

Write-Host "=== 4. Running Coverage Option Checks ===" -ForegroundColor Cyan
# Default coverage succeeds only when all manifest batches are present; otherwise it must fail.
$res = & python $pythonScript --repo-root $RepositoryRoot --check-coverage 2>&1
$coverageExitCode = $LASTEXITCODE
$coverageOutput = $res -join "`n"
$isComplete = $coverageOutput -match 'Coverage Progress:\s*48/48 batches'
if ($isComplete -and $coverageExitCode -ne 0) {
    throw "Expected --check-coverage to succeed on complete coverage, but got:`n$coverageOutput"
}
if (-not $isComplete -and $coverageExitCode -eq 0) {
    throw "Expected --check-coverage to fail on incomplete coverage, but it succeeded:`n$coverageOutput"
}
Write-Host "  -> --check-coverage exit code matches the current completion state." -ForegroundColor Green

# check-coverage WITH --allow-incomplete MUST succeed with exit code 0
$res = & python $pythonScript --repo-root $RepositoryRoot --check-coverage --allow-incomplete 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Expected --check-coverage --allow-incomplete to succeed, but got:`n$res"
}
Write-Host "  -> --check-coverage --allow-incomplete succeeded cleanly." -ForegroundColor Green

Write-Host "=== 5. Running Negative Test Cases ===" -ForegroundColor Cyan

# Helper to run python script on a test JSON string
$tempTestFile = [System.IO.Path]::GetTempFileName() + ".json"

function Assert-ValidationFails($jsonContent, $testName) {
    Set-Content -LiteralPath $tempTestFile -Value $jsonContent -Encoding utf8
    $output = & python $pythonScript --repo-root $RepositoryRoot --validate-file $tempTestFile 2>&1
    if ($LASTEXITCODE -eq 0) {
        Remove-Item -LiteralPath $tempTestFile -Force -ErrorAction SilentlyContinue
        throw "Negative test failed! Expected failure for '$testName', but validator PASSED.`nOutput:`n$output"
    }
    Write-Host "  -> [NEGATIVE PASS] '$testName' was correctly rejected." -ForegroundColor Green
}

function Assert-ValidationSucceeds($jsonContent, $testName) {
    Set-Content -LiteralPath $tempTestFile -Value $jsonContent -Encoding utf8
    $output = & python $pythonScript --repo-root $RepositoryRoot --validate-file $tempTestFile 2>&1
    if ($LASTEXITCODE -ne 0) {
        Remove-Item -LiteralPath $tempTestFile -Force -ErrorAction SilentlyContinue
        throw "Positive test failed! Expected success for '$testName', but validator FAILED.`nOutput:`n$output"
    }
    Write-Host "  -> [POSITIVE PASS] '$testName' succeeded cleanly." -ForegroundColor Green
}

try {
    # 5.0 Positive check: coach entity type and pro_personal_appearance/pro_personal_relationships topics
    Assert-ValidationSucceeds '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["pro_player"],
                "stances": ["tease_playful"],
                "topics": ["pro_personal_appearance", "pro_personal_relationships"],
                "formats": ["plain_statement"],
                "culture": ["origin_cs_community"],
                "entities": [{"name": "zonic", "type": "coach"}],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "Coach entity type and pro_personal_* topics positive validation"

    # 5.0b Positive check: flame_caster_host and external_sports_competition
    Assert-ValidationSucceeds '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["caster_host"],
                "stances": ["flame_caster_host"],
                "topics": ["external_sports_competition"],
                "formats": ["plain_statement"],
                "culture": ["origin_gaming_general"],
                "entities": [{"name": "冬瓜强", "type": "caster"}],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 0.95,
                "review": {"status": "pending"}
            }
        ]
    }' "flame_caster_host stance and external_sports_competition positive validation"

    # 5.0c Positive check: flame_external_figure and external_figure_personal_life
    Assert-ValidationSucceeds '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["external_figure"],
                "stances": ["flame_external_figure"],
                "topics": ["external_figure_personal_life"],
                "formats": ["plain_statement"],
                "culture": ["origin_internet_folklore"],
                "entities": [{"name": "C罗", "type": "other"}],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 0.95,
                "review": {"status": "pending"}
            }
        ]
    }' "flame_external_figure stance and external_figure_personal_life positive validation"

    # 5.1 Unknown field in root (additionalProperties)
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [],
        "extra_unknown_field": 123
    }' "Root additionalProperties violation"

    # 5.2 Raw text leakage in annotation entry
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["streamer"],
                "stances": ["tease_playful"],
                "topics": ["streamer_skill_gameplay"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"},
                "text": "玩机器今天又下饭了"
            }
        ]
    }' "Forbidden raw text leakage ('text' field)"

    # 5.3 Batch range incompleteness (gap in index continuity)
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "batch_001",
        "total_items": 3,
        "range": {"start_index": 1, "end_index": 3},
        "annotations": [
            {
                "index": 1,
                "targets": ["streamer"],
                "stances": ["tease_playful"],
                "topics": ["streamer_skill_gameplay"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            },
            {
                "index": 3,
                "targets": ["streamer"],
                "stances": ["tease_playful"],
                "topics": ["streamer_skill_gameplay"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "Batch range incompleteness (missing index 2)"

    # 5.4 Duplicate tags in stances
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["streamer"],
                "stances": ["tease_playful", "tease_playful"],
                "topics": ["streamer_skill_gameplay"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "Duplicate tag in stances array"

    # 5.5 Safety contradiction: profanity flag with safe severity
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["streamer"],
                "stances": ["flame_streamer"],
                "topics": ["streamer_skill_gameplay"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["profanity"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "Safety contradiction (profanity with safe severity)"

    # 5.6 Safety flags mixed 'none' with risk flags
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["streamer"],
                "stances": ["flame_streamer"],
                "topics": ["streamer_skill_gameplay"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "sensitive_flame", "flags": ["none", "profanity"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "Safety flags mixed 'none' with 'profanity'"

    # 5.7 Anti-spoofing: pending status with reviewer assigned
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["streamer"],
                "stances": ["tease_playful"],
                "topics": ["streamer_skill_gameplay"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending", "reviewer": "GPT-5.6-Sol"}
            }
        ]
    }' "Anti-spoofing (reviewer assigned while status is pending)"

    # 5.8 Entity with invalid type
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["streamer"],
                "stances": ["tease_playful"],
                "topics": ["streamer_skill_gameplay"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [{"name": "fake", "type": "invalid_entity_type"}],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "Invalid entity type enum"

    # 5.9 Streamer topic reservation violation (streamer_* topic without streamer target)
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["pro_player"],
                "stances": ["tease_playful"],
                "topics": ["streamer_skill_gameplay"],
                "formats": ["plain_statement"],
                "culture": ["origin_cs_community"],
                "entities": [{"name": "s1mple", "type": "player"}],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "Streamer topic reservation violation (streamer_* without streamer target)"

    # 5.10 Entity targets coherence violation (streamer entity without streamer target)
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["pro_player"],
                "stances": ["tease_playful"],
                "topics": ["pro_gunplay_aim"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [{"name": "玩机器", "type": "streamer"}],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "Entity targets coherence violation (streamer entity without streamer target)"

    # 5.11 Unnormalized entity alias rejection (e.g. 大表哥 instead of karrigan)
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["pro_player"],
                "stances": ["tease_playful"],
                "topics": ["pro_tactics_igl"],
                "formats": ["plain_statement"],
                "culture": ["origin_cs_community"],
                "entities": [{"name": "大表哥", "type": "player"}],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "Unnormalized entity alias rejection ('大表哥' -> 'karrigan')"

    # 5.12 Canonical entity type mismatch rejection (e.g. zonic tagged as player instead of coach)
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["pro_player"],
                "stances": ["tease_playful"],
                "topics": ["pro_tactics_igl"],
                "formats": ["plain_statement"],
                "culture": ["origin_cs_community"],
                "entities": [{"name": "zonic", "type": "player"}],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "Canonical entity type mismatch ('zonic' must be 'coach')"

    # 5.13 flame_streamer missing 'streamer' target rejection
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["pro_player"],
                "stances": ["flame_streamer"],
                "topics": ["pro_whiff_blunder"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "flame_streamer stance without 'streamer' in targets"

    # 5.14 flame_player missing 'pro_player' target rejection
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["streamer"],
                "stances": ["flame_player"],
                "topics": ["streamer_skill_gameplay"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "flame_player stance without 'pro_player' in targets"

    # 5.15 flame_team missing 'pro_team' target rejection
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["pro_player"],
                "stances": ["flame_team"],
                "topics": ["pro_score_outcome"],
                "formats": ["plain_statement"],
                "culture": ["origin_cs_community"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "flame_team stance without 'pro_team' in targets"

    # 5.16 flame_audience missing 'chat_audience' target rejection
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["streamer"],
                "stances": ["flame_audience"],
                "topics": ["streamer_stubborn_rage"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "flame_audience stance without 'chat_audience' in targets"

    # 5.17 flame_caster_host missing 'caster_host' target rejection
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["streamer"],
                "stances": ["flame_caster_host"],
                "topics": ["pro_scene_drama"],
                "formats": ["plain_statement"],
                "culture": ["origin_6657"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "flame_caster_host stance without 'caster_host' in targets"

    # 5.18 flame_external_figure missing 'external_figure' target rejection
    Assert-ValidationFails '{
        "schema_version": "1.0.0",
        "batch_id": "calibration_sample",
        "total_items": 1,
        "range": {"start_index": 1, "end_index": 1},
        "annotations": [
            {
                "index": 1,
                "targets": ["general_meta"],
                "stances": ["flame_external_figure"],
                "topics": ["external_figure_personal_life"],
                "formats": ["plain_statement"],
                "culture": ["origin_internet_folklore"],
                "entities": [],
                "context": "standalone",
                "safety": {"severity": "safe", "flags": ["none"]},
                "confidence": 1.0,
                "review": {"status": "pending"}
            }
        ]
    }' "flame_external_figure stance without 'external_figure' in targets"

} finally {
    Remove-Item -LiteralPath $tempTestFile -Force -ErrorAction SilentlyContinue
}

Write-Host "`nPASS: All Danmaku Annotation Infrastructure regression and negative tests passed successfully!" -ForegroundColor Green
