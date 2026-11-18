[CmdletBinding()]
param(
    [string] $ManifestPath = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $root 'SHA256SUMS.txt'
}
if (-not (Test-Path -LiteralPath $ManifestPath)) { throw "Checksum manifest not found: $ManifestPath" }
$manifestRoot = Split-Path -Parent $ManifestPath
$checked = 0
foreach ($line in Get-Content -LiteralPath $ManifestPath) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) { continue }
    if ($line -notmatch '^([0-9a-fA-F]{64})\s{2}(.+)$') { throw "Invalid checksum line: $line" }
    $expected = $Matches[1].ToLowerInvariant()
    $relativePath = $Matches[2].Trim()
    $fullPath = Join-Path $manifestRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "File listed in checksum manifest is missing: $relativePath" }
    $actual = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) { throw "Checksum mismatch: $relativePath" }
    $checked++
}
if ($checked -lt 1) { throw 'Checksum manifest contains no file entries.' }
Write-Host "PASS: verified $checked source file checksums." -ForegroundColor Green
