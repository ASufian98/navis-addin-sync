# BINA Navis Sync — Build release artifacts
#
# Usage:
#   .\build-release.ps1 -Version 0.0.1 [-Configuration Release]
#
# Produces:
#   - BinaNavisSync-<ver>.zip (OTA payload)
#   - version.json (OTA feed)
#   - BinaNavisSync-<ver>-setup.exe (installer, if ISCC available)

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [ValidateSet('Debug','Release','Staging')]
    [string]$Configuration = 'Release',

    [string]$Notes = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$proj = Join-Path $root 'NavisWebAppSync.csproj'
$outDir = Join-Path $root 'artifacts'

Write-Host "Building BINA Navis Sync v$Version ($Configuration)..." -ForegroundColor Cyan

# Clean + build
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path $outDir | Out-Null

dotnet build $proj -c $Configuration -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$binDir = Join-Path $root "bin\$Configuration\net48"

# Create OTA zip
$zipDir = Join-Path $outDir "NavisWebAppSync"
New-Item -ItemType Directory -Path $zipDir | Out-Null
Copy-Item (Join-Path $binDir 'NavisWebAppSync.dll') $zipDir
Copy-Item (Join-Path $binDir 'BinaRibbon.xaml') $zipDir
Copy-Item (Join-Path $binDir 'Newtonsoft.Json.dll') $zipDir
Copy-Item (Join-Path $binDir 'Resources') $zipDir -Recurse

$zipPath = Join-Path $outDir "BinaNavisSync-$Version.zip"
Compress-Archive -Path "$zipDir\*" -DestinationPath $zipPath -Force

# Generate SHA256
$sha256 = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLower()

# Generate version.json
$versionJson = @{
    version = $Version
    url = "https://github.com/binacloudmy/navis-addin-sync/releases/download/v$Version/BinaNavisSync-$Version.zip"
    sha256 = $sha256
    notes = $Notes
    mandatory = $true
} | ConvertTo-Json -Depth 2

$versionJson | Out-File (Join-Path $outDir 'version.json') -Encoding utf8

Write-Host ""
Write-Host "Artifacts created in $outDir" -ForegroundColor Green
Write-Host "  - BinaNavisSync-$Version.zip (sha256: $sha256)"
Write-Host "  - version.json"

# Build installer if ISCC available
$iscc = Get-Command 'ISCC' -ErrorAction SilentlyContinue
if ($iscc) {
    Write-Host ""
    Write-Host "Building installer..." -ForegroundColor Cyan
    $issPath = Join-Path $PSScriptRoot 'BinaNavis.iss'
    & ISCC $issPath /DAppVersion=$Version /DPluginDir=$binDir /O$outDir
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  - BinaNavisSync-$Version-setup.exe" -ForegroundColor Green
    }
} else {
    Write-Host ""
    Write-Host "ISCC not found — skipping installer build" -ForegroundColor Yellow
    Write-Host "Install Inno Setup and add to PATH to build the installer"
}

Write-Host ""
Write-Host "Done! Upload artifacts to GitHub Release v$Version" -ForegroundColor Cyan
