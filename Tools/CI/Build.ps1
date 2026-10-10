param([string]$Tag = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location -LiteralPath $root
try {
    python Tools/GenerateDefs.py --check
    if ($LASTEXITCODE) { throw 'Generated data is stale.' }
    python Tools/Validate.py
    if ($LASTEXITCODE) { throw 'Repository validation failed.' }
    dotnet restore Tests/Engine/Probe.csproj -p:UseReferencePackages=true --locked-mode
    if ($LASTEXITCODE) { throw 'Locked engine/mod restore failed.' }
    dotnet build Tests/Engine/Probe.csproj -c Release -p:UseReferencePackages=true --no-restore -warnaserror
    if ($LASTEXITCODE) { throw 'Mod/probe build failed.' }
    python Tools/Package.py --tag $Tag
    if ($LASTEXITCODE) { throw 'Packaging failed.' }
    python Tools/Validate.py --package artifacts/ci
    if ($LASTEXITCODE) { throw 'Package verification failed.' }
    if ($env:GITHUB_STEP_SUMMARY) { 'Built and validated RimCBM. Steam publishing is not configured.' >> $env:GITHUB_STEP_SUMMARY }
} finally { Pop-Location }
