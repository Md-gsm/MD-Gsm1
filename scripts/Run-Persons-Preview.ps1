[CmdletBinding()]
param([switch] $Build)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\Host\Desktop\MDGsm.Desktop.csproj'
if ($Build) {
    & dotnet build $project --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
}
$exe = Join-Path $root 'src\Host\Desktop\bin\Release\net10.0-windows\MDGsm.Desktop.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw 'Desktop executable not found. Run .\scripts\Run-Persons-Preview.ps1 -Build first. Requires .NET 10 SDK; Python is not required.'
}
Start-Process -FilePath $exe
