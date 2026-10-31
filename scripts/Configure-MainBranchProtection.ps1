[CmdletBinding()]
param(
    [string]$Repository = 'Md-gsm/MD-Gsm1',
    [string]$Branch = 'main',
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$RequiredChecks = @(
    'foundation-build-test',
    'persons-spec-lint',
    'persons-architecture-boundary',
    'persons-consumer-registry',
    'persons-api-contract',
    'persons-temporary-data-retention',
    'persons-merge-contract',
    'persons-postgres-migrations',
    'persons-security-tests',
    'persons-e2e-disposable',
    'persons-ci-enforcement-test',
    'persons-ci-evidence'
)

function Invoke-GhJson {
    param([Parameter(Mandatory=$true)][string[]]$Arguments)
    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI command failed (exit $LASTEXITCODE): gh $($Arguments -join ' ')"
    }
    if ([string]::IsNullOrWhiteSpace(($output -join [Environment]::NewLine))) {
        return $null
    }
    return (($output -join [Environment]::NewLine) | ConvertFrom-Json)
}

Write-Host 'MD-Gsm main branch protection — preflight' -ForegroundColor Cyan
Write-Host "Repository : $Repository"
Write-Host "Branch     : $Branch"
Write-Host "Mode       : $(if ($Apply) { 'APPLY' } else { 'PREVIEW ONLY' })"
Write-Host ''

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) was not found. Install GitHub CLI and reopen PowerShell.'
}

& gh auth status
if ($LASTEXITCODE -ne 0) {
    throw 'GitHub CLI is not authenticated. Run: gh auth login'
}

$branchInfo = Invoke-GhJson -Arguments @('api', "repos/$Repository/branches/$Branch")
$headSha = [string]$branchInfo.commit.sha
if ([string]::IsNullOrWhiteSpace($headSha)) {
    throw "Could not resolve HEAD SHA for $Repository/$Branch."
}

Write-Host "Remote HEAD: $headSha"
Write-Host "GitHub reports protected: $($branchInfo.protected)"

$checksResponse = Invoke-GhJson -Arguments @('api', "repos/$Repository/commits/$headSha/check-runs?per_page=100")
$checkRuns = @($checksResponse.check_runs)
$missing = @()
$notGreen = @()
foreach ($required in $RequiredChecks) {
    $matchingChecks = @($checkRuns | Where-Object { $_.name -eq $required })
    if ($matchingChecks.Count -eq 0) {
        $missing += $required
        continue
    }
    $latest = $matchingChecks | Sort-Object { [DateTime]$_.completed_at } -Descending | Select-Object -First 1
    if ($latest.status -ne 'completed' -or $latest.conclusion -ne 'success') {
        $notGreen += "$required [$($latest.status)/$($latest.conclusion)]"
    }
}

Write-Host ''
Write-Host 'Required CI checks at current remote HEAD:' -ForegroundColor Cyan
foreach ($required in $RequiredChecks) {
    $match = @($checkRuns | Where-Object { $_.name -eq $required } | Select-Object -First 1)
    if ($match.Count -eq 0) {
        Write-Host "  MISSING  $required" -ForegroundColor Red
    } else {
        $state = "$($match[0].status)/$($match[0].conclusion)"
        $color = if ($state -eq 'completed/success') { 'Green' } else { 'Yellow' }
        Write-Host "  $state  $required" -ForegroundColor $color
    }
}

$currentProtection = $null
try {
    $currentProtection = Invoke-GhJson -Arguments @('api', "repos/$Repository/branches/$Branch/protection")
} catch {
    Write-Warning "Could not read current detailed branch protection: $($_.Exception.Message)"
    throw 'Cannot safely continue without reading the current protection configuration.'
}

$currentContexts = @()
if ($null -ne $currentProtection.required_status_checks) {
    $currentContexts = @($currentProtection.required_status_checks.contexts)
}
Write-Host ''
Write-Host 'Current required checks configured on branch:' -ForegroundColor Cyan
if ($currentContexts.Count -eq 0) {
    Write-Host '  NONE' -ForegroundColor Yellow
} else {
    $currentContexts | ForEach-Object { Write-Host "  $_" }
}

Write-Host ''
if ($missing.Count -gt 0 -or $notGreen.Count -gt 0) {
    if ($missing.Count -gt 0) { Write-Host ("Missing checks: " + ($missing -join ', ')) -ForegroundColor Red }
    if ($notGreen.Count -gt 0) { Write-Host ("Checks not green: " + ($notGreen -join ', ')) -ForegroundColor Red }
    throw 'Preflight blocked. No protection settings were changed.'
}

$payload = @{
    required_status_checks = @{
        strict = $true
        contexts = $RequiredChecks
    }
    enforce_admins = $true
    required_pull_request_reviews = @{
        dismiss_stale_reviews = $true
        require_code_owner_reviews = $false
        required_approving_review_count = 0
        require_last_push_approval = $false
    }
    required_conversation_resolution = $true
    restrictions = $null
    allow_force_pushes = $false
    allow_deletions = $false
}
$json = $payload | ConvertTo-Json -Depth 10
$tempFile = Join-Path ([IO.Path]::GetTempPath()) ("mdgsm-main-protection-" + [Guid]::NewGuid().ToString('N') + '.json')
[IO.File]::WriteAllText($tempFile, $json, (New-Object System.Text.UTF8Encoding($false)))

try {
    if (-not $Apply) {
        Write-Host 'PREVIEW COMPLETE — no GitHub settings were changed.' -ForegroundColor Green
        Write-Host 'Planned policy: PR required; all 12 current CI jobs required; branch must be up to date; admin enforcement enabled; force-push and branch deletion disabled; conversation resolution required.'
        Write-Host 'Approval count is intentionally 0 because no independent reviewer account/team was confirmed. This can be raised after reviewer assignment.'
        Write-Host 'To apply after reviewing this report, run:'
        Write-Host ".\scripts\Configure-MainBranchProtection.ps1 -Apply"
        return
    }

    Write-Warning 'Applying branch protection to the remote main branch.'
    $null = Invoke-GhJson -Arguments @('api', '--method', 'PUT', '--input', $tempFile, "repos/$Repository/branches/$Branch/protection")
    $verified = Invoke-GhJson -Arguments @('api', "repos/$Repository/branches/$Branch/protection")

    $actual = @($verified.required_status_checks.contexts)
    $missingAfter = @($RequiredChecks | Where-Object { $_ -notin $actual })
    $unexpectedAfter = @($actual | Where-Object { $_ -notin $RequiredChecks })
    $prReviewsEnabled = $null -ne $verified.required_pull_request_reviews
    $conversationResolutionEnabled = $verified.required_conversation_resolution -eq $true
    if (
        $missingAfter.Count -gt 0 -or
        $unexpectedAfter.Count -gt 0 -or
        $actual.Count -ne $RequiredChecks.Count -or
        -not $verified.required_status_checks.strict -or
        -not $verified.enforce_admins.enabled -or
        -not $prReviewsEnabled -or
        $verified.required_pull_request_reviews.required_approving_review_count -ne 0 -or
        -not $conversationResolutionEnabled -or
        $verified.allow_force_pushes.enabled -or
        $verified.allow_deletions.enabled
    ) {
        throw 'Post-apply verification failed. Review branch protection in GitHub immediately.'
    }

    Write-Host 'SUCCESS — branch protection applied and read back from GitHub.' -ForegroundColor Green
    Write-Host "Required checks verified: $($actual.Count)"
    Write-Host "Strict/up-to-date: $($verified.required_status_checks.strict)"
    Write-Host "Admin enforcement: $($verified.enforce_admins.enabled)"
    Write-Host "PR required (approvals): $($verified.required_pull_request_reviews.required_approving_review_count)"
    Write-Host "Conversation resolution required: $conversationResolutionEnabled"
    Write-Host "Force pushes allowed: $($verified.allow_force_pushes.enabled)"
    Write-Host "Branch deletion allowed: $($verified.allow_deletions.enabled)"
} finally {
    Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
}
