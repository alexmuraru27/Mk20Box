<#
.SYNOPSIS
    Builds MK20Box and copies it into the SimHub installation. For development.

.DESCRIPTION
    Runs build.ps1, then mirrors the staged folder into SIMHUB_INSTALL_PATH:

        <SimHub>\Mk20Box.dll
        <SimHub>\Mk20Box\*.dll
        <SimHub>\Mk20Box\Mk20Assets\
        <SimHub>\Languages\Mk20Box.resx

    Only files the plugin owns are written; nothing SimHub ships is touched.

    SimHub locks Mk20Box.dll while it is running, so the script checks for it
    first and stops with a clear message instead of a confusing copy error.

.PARAMETER Configuration
    Release (default) or Debug. Debug also copies the .pdb for breakpoints.

.PARAMETER SkipBuild
    Deploy whatever is already staged in dist\Mk20Box.

.PARAMETER Force
    Close SimHub automatically instead of stopping.

.EXAMPLE
    .\deploy.ps1
    .\deploy.ps1 -Configuration Debug
    .\deploy.ps1 -Force
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [switch] $SkipBuild,

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$staging = Join-Path $root 'dist\Mk20Box'

function Write-Step($message) {
    Write-Host "==> $message" -ForegroundColor Cyan
}

if (-not $env:SIMHUB_INSTALL_PATH) {
    throw "SIMHUB_INSTALL_PATH is not set. Point it at your SimHub folder, e.g. 'C:\Program Files (x86)\SimHub\'."
}

$simHub = $env:SIMHUB_INSTALL_PATH.TrimEnd('\')

if (-not (Test-Path (Join-Path $simHub 'SimHub.Plugins.dll'))) {
    throw "'$simHub' does not look like a SimHub installation. Check SIMHUB_INSTALL_PATH."
}

# A running SimHub holds the DLL open, so the copy would fail halfway and
# leave a half-updated plugin behind. Only the instance running from the
# target folder can lock it, so a copy elsewhere is left alone.
$running = @(
    Get-Process -Name 'SimHubWPF' -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                $path = $_.MainModule.FileName
                $path -and (Split-Path $path -Parent).TrimEnd('\') -ieq $simHub
            }
            catch {
                # Access denied reading the module: assume it is the one we mean.
                $true
            }
        }
)

if ($running.Count -gt 0) {
    if (-not $Force) {
        throw "SimHub is running and holds Mk20Box.dll open. Close it, or re-run with -Force."
    }

    Write-Step 'Closing SimHub'

    foreach ($process in $running) {
        Stop-Process -Id $process.Id -Force
    }

    # Give Windows time to release the file handles before copying.
    Start-Sleep -Seconds 3
}

if (-not $SkipBuild) {
    & (Join-Path $root 'build.ps1') -Configuration $Configuration

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path $staging)) {
    throw "Nothing staged in '$staging'. Run build.ps1 first, or drop -SkipBuild."
}

Write-Step "Deploying to $simHub"

# The asset library is ours alone, so it is replaced rather than merged: a file
# renamed, moved or dropped since the last deploy would otherwise linger and be
# picked up by the icon browser alongside its replacement.
$deployedAssets = Join-Path $simHub 'Mk20Box\Mk20Assets'
if (Test-Path $deployedAssets) {
    Remove-Item $deployedAssets -Recurse -Force
}

$copied = 0

foreach ($file in Get-ChildItem $staging -Recurse -File) {
    $relative = $file.FullName.Substring($staging.Length).TrimStart('\')
    $target = Join-Path $simHub $relative
    $targetDir = Split-Path $target -Parent

    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    Copy-Item $file.FullName $target -Force
    $copied++
}

# Proves the running plugin is the one just built, which a stale copy would not.
$sourceHash = (Get-FileHash (Join-Path $staging 'Mk20Box.dll')).Hash
$deployedHash = (Get-FileHash (Join-Path $simHub 'Mk20Box.dll')).Hash

if ($sourceHash -ne $deployedHash) {
    throw "Mk20Box.dll does not match after copying. Deployment is incomplete."
}

Write-Host ""
Write-Host "  files    : $copied" -ForegroundColor Gray
Write-Host "  verified : $($deployedHash.Substring(0, 16))..." -ForegroundColor Gray
Write-Host ""
Write-Host 'Done. Restart SimHub to load the plugin.' -ForegroundColor Green
