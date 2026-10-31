[CmdletBinding()]
param(
    [switch] $Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

# This helper changes only the protection settings for Md-gsm/MD-Gsm1:main.
# It requires GitHub CLI authentication by a repository Admin with Administration:write.
$repository = 'Md-gsm/MD-Gsm1'
$branch = 'main'
$requiredChecks = @(
    'foundation-build-test',
    'persons-spec-lint',
    'persons-architecture-boundary',
    'persons-consumer-registry',
    'persons-api-contract',
    'persons-temporary-data-retention',
    'persons-merge-contract',
    'persons-postgres-migrations',
    'persons-postgres-windows-migrations',
    'persons-security-tests',
    'persons-e2e-disposable',
    'persons-ci-enforcement-test',
    'persons-ci-evidence'
)

$gh = Get-Command 'gh.exe' -ErrorAction SilentlyContinue
if (-not $gh) { $gh = Get-Command 'gh' -ErrorAction SilentlyContinue }
if (-not $gh) { throw 'GitHub CLI (gh) is required. Install it and authenticate the repository Admin account first.' }

& $gh.Source auth status --hostname github.com
if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI authentication is unavailable. No repository settings were changed.' }

$repositoryJson = & $gh.Source api "repos/$repository" 2>&1
if ($LASTEXITCODE -ne 0) { throw "Cannot read repository metadata: $($repositoryJson -join ' ')" }
$repositoryInfo = ($repositoryJson -join "`n") | ConvertFrom-Json
if ($repositoryInfo.full_name -ne $repository) { throw "Unexpected repository identity: $($repositoryInfo.full_name)" }
if ($repositoryInfo.default_branch -ne $branch) { throw "Expected default branch '$branch', got '$($repositoryInfo.default_branch)'." }
if ($repositoryInfo.permissions.admin -ne $true) {
    throw 'The authenticated GitHub identity is not a repository Admin. No repository settings were changed.'
}

# Read the existing full rule before PUT because the endpoint replaces the branch protection object.
$currentJson = & $gh.Source api "repos/$repository/branches/$branch/protection" 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Cannot read existing branch protection. Ensure the CLI token has Administration:read/write. No settings were changed. $($currentJson -join ' ')"
}
$current = ($currentJson -join "`n") | ConvertFrom-Json

$currentContexts = @()
if ($null -ne $current.required_status_checks -and $null -ne $current.required_status_checks.contexts) {
    $currentContexts = @($current.required_status_checks.contexts)
}

$reviewCount = 1
$requireCodeOwnerReviews = $false
$dismissalRestrictions = @{ users = @(); teams = @(); apps = @() }
if ($null -ne $current.required_pull_request_reviews) {
    $reviewCount = [Math]::Max(1, [int]$current.required_pull_request_reviews.required_approving_review_count)
    $requireCodeOwnerReviews = [bool]$current.required_pull_request_reviews.require_code_owner_reviews
    $currentDismissal = $current.required_pull_request_reviews.dismissal_restrictions
    if ($null -ne $currentDismissal) {
        $dismissalRestrictions = @{
            users = @($currentDismissal.users | ForEach-Object { $_.login })
            teams = @($currentDismissal.teams | ForEach-Object { $_.slug })
            apps = @($currentDismissal.apps | ForEach-Object { $_.slug })
        }
    }
}

$pushRestrictions = $null
if ($null -ne $current.restrictions) {
    $users = @($current.restrictions.users | ForEach-Object { $_.login })
    $teams = @($current.restrictions.teams | ForEach-Object { $_.slug })
    $apps = @($current.restrictions.apps | ForEach-Object { $_.slug })
    if ($users.Count + $teams.Count + $apps.Count -gt 0) {
        $pushRestrictions = @{ users = $users; teams = $teams; apps = $apps }
    }
}

$payload = @{
    required_status_checks = @{
        strict = $true
        contexts = $requiredChecks
    }
    enforce_admins = $true
    required_pull_request_reviews = @{
        dismissal_restrictions = $dismissalRestrictions
        bypass_pull_request_allowances = @{ users = @(); teams = @(); apps = @() }
        dismiss_stale_reviews = $true
        require_code_owner_reviews = $requireCodeOwnerReviews
        required_approving_review_count = $reviewCount
        require_last_push_approval = $true
    }
    restrictions = $pushRestrictions
    required_linear_history = [bool]$current.required_linear_history.enabled
    allow_force_pushes = $false
    allow_deletions = $false
    block_creations = [bool]$current.block_creations.enabled
    required_conversation_resolution = $true
    lock_branch = [bool]$current.lock_branch.enabled
    allow_fork_syncing = [bool]$current.allow_fork_syncing.enabled
}

Write-Host "Repository: $repository"
Write-Host "Branch: $branch"
Write-Host 'Required checks to apply:'
$requiredChecks | ForEach-Object { Write-Host "  - $_" }
Write-Host "Existing required-check contexts: $($currentContexts -join ', ')"
Write-Host "Required approving reviews: $reviewCount"
Write-Host 'Strict/up-to-date: true; enforce administrators: true; bypass allowances: none; force-push: false; deletion: false.'
Write-Host 'Existing push restrictions and unrelated protection options are preserved where present.'

if (-not $Apply) {
    Write-Host 'DRY RUN ONLY. No repository settings were changed. Re-run with -Apply to perform the update.'
    return
}

$confirmation = Read-Host "To apply these settings, type exactly '$repository/$branch'"
if ($confirmation -cne "$repository/$branch") {
    throw 'Confirmation text did not match. No repository settings were changed.'
}

$body = $payload | ConvertTo-Json -Depth 12 -Compress
$response = $body | & $gh.Source api --method PUT "repos/$repository/branches/$branch/protection" --input -
if ($LASTEXITCODE -ne 0) { throw 'GitHub rejected the branch-protection update. Review the API output above.' }

$verifyJson = & $gh.Source api "repos/$repository/branches/$branch/protection" 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Settings were sent, but verification could not read them back. Do not assume success.' }
$verified = ($verifyJson -join "`n") | ConvertFrom-Json
$actualContexts = @($verified.required_status_checks.contexts)
$missing = @($requiredChecks | Where-Object { $_ -notin $actualContexts })
if ($missing.Count -gt 0) { throw "Branch protection is missing required checks: $($missing -join ', ')" }
if ($verified.required_status_checks.strict -ne $true) { throw 'Require branches to be up to date was not enabled.' }
if ($verified.enforce_admins.enabled -ne $true) { throw 'Administrator enforcement was not enabled.' }
if ($verified.required_pull_request_reviews.required_approving_review_count -lt 1) { throw 'A pull-request approval is not required.' }
if ($verified.required_pull_request_reviews.require_last_push_approval -ne $true) { throw 'Approval of the latest push is not required.' }
if ($verified.allow_force_pushes.enabled -ne $false) { throw 'Force-push is not blocked.' }
if ($verified.allow_deletions.enabled -ne $false) { throw 'Branch deletion is not blocked.' }
if ($verified.required_conversation_resolution.enabled -ne $true) { throw 'Conversation resolution is not required.' }

Write-Host 'PASS: GitHub main branch protection updated and verified.' -ForegroundColor Green
Write-Host 'Next required evidence: run a new negative PR after this update and prove the failed required check blocks merge.'
