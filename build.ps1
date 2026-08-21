<#
.SYNOPSIS
    Builds MK20Box and stages a ready-to-copy folder for SimHub.

.DESCRIPTION
    Produces dist\Mk20Box laid out exactly as it must sit inside the SimHub
    installation, so an end user only has to copy its contents across:

        Mk20Box.dll            the plugin (SimHub scans its own root)
        Mk20Box\*.dll          private dependencies
        Mk20Box\Mk20Assets\    icon library
        Languages\Mk20Box.resx translations

    Nothing is written to the SimHub installation. Use deploy.ps1 for that.

.PARAMETER Configuration
    Release (default) or Debug.

.PARAMETER Zip
    Also pack the staged folder into dist\Mk20Box-<version>.zip for release.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Zip
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [switch] $Zip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src\Mk20Box\Mk20Box.csproj'
$staging = Join-Path $root 'dist\Mk20Box'

function Write-Step($message) {
    Write-Host "==> $message" -ForegroundColor Cyan
}

# SimHub's own assemblies are referenced from the installation, so the build
# cannot run without knowing where it is.
if (-not $env:SIMHUB_INSTALL_PATH) {
    throw "SIMHUB_INSTALL_PATH is not set. Point it at your SimHub folder, e.g. 'C:\Program Files (x86)\SimHub\'."
}

if (-not (Test-Path (Join-Path $env:SIMHUB_INSTALL_PATH 'SimHub.Plugins.dll'))) {
    throw "SimHub.Plugins.dll was not found in '$env:SIMHUB_INSTALL_PATH'. Check SIMHUB_INSTALL_PATH."
}

# The device protocol library is a submodule; without it the build fails with
# a confusing missing-project error.
$submodule = Join-Path $root 'external\MK20Control\src\Mk20Control.Protocol\Mk20Control.Protocol.csproj'
if (-not (Test-Path $submodule)) {
    throw "The MK20Control submodule is missing. Run: git submodule update --init --recursive"
}

Write-Step "Building $Configuration"

# DeployToSimHub stays false so building never touches the installation.
& dotnet build $project --configuration $Configuration --nologo -p:DeployToSimHub=false

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$output = Join-Path $root "src\Mk20Box\bin\$Configuration"
$pluginDll = Join-Path $output 'Mk20Box.dll'

if (-not (Test-Path $pluginDll)) {
    throw "Expected '$pluginDll' after the build, but it is missing."
}

Write-Step "Staging $staging"

if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}

$pluginSubDir = Join-Path $staging 'Mk20Box'
$languageDir = Join-Path $staging 'Languages'

New-Item -ItemType Directory -Path $pluginSubDir -Force | Out-Null
New-Item -ItemType Directory -Path $languageDir -Force | Out-Null

# The plugin itself sits in the SimHub root; everything else is private.
Copy-Item $pluginDll $staging -Force

$pdb = Join-Path $output 'Mk20Box.pdb'
if ($Configuration -eq 'Debug' -and (Test-Path $pdb)) {
    Copy-Item $pdb $staging -Force
}

$dependencies = Get-ChildItem (Join-Path $output '*.dll') |
    Where-Object { $_.Name -ne 'Mk20Box.dll' }

foreach ($dependency in $dependencies) {
    Copy-Item $dependency.FullName $pluginSubDir -Force
}

$assets = Join-Path $output 'Mk20Assets'
if (Test-Path $assets) {
    Copy-Item $assets $pluginSubDir -Recurse -Force
}

$resx = Join-Path $root 'src\Mk20Box\Languages\Mk20Box.resx'
if (Test-Path $resx) {
    Copy-Item $resx $languageDir -Force
}

$assetCount = 0
if (Test-Path $assets) {
    $assetCount = (Get-ChildItem $assets -Recurse -File).Count
}

Write-Host ""
Write-Host "  plugin       : Mk20Box.dll" -ForegroundColor Gray
Write-Host "  dependencies : $($dependencies.Count)" -ForegroundColor Gray
Write-Host "  assets       : $assetCount" -ForegroundColor Gray

if ($Zip) {
    Write-Step 'Packing'

    $version = (Get-Item $pluginDll).VersionInfo.FileVersion
    if (-not $version) {
        $version = '0.0.0.0'
    }

    $archive = Join-Path $root "dist\Mk20Box-$version.zip"
    if (Test-Path $archive) {
        Remove-Item $archive -Force
    }

    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive
    Write-Host "  archive      : $archive" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Done. Copy the contents of '$staging' into your SimHub folder." -ForegroundColor Green
