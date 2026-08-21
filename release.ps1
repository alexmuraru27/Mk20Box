<#
.SYNOPSIS
    Builds, checks and packs a release of MK20Box.

.DESCRIPTION
    A release is more than a zip, so this does what build.ps1 deliberately does
    not:

      * rebuilds from clean, so nothing stale is shipped
      * optionally stamps a version
      * refuses to ship SimHub's own assemblies, which are referenced only
      * includes the licence and documentation
      * writes a SHA256 checksum beside the archive

    The result lands in release\, both as a folder you can copy straight into
    SimHub and as a zip to publish.

.PARAMETER Version
    Version to stamp, e.g. 1.2.0. Defaults to whatever AssemblyInfo already says.

.PARAMETER Strict
    Treat provenance warnings as errors: refuse to pack from a dirty working
    tree, a modified submodule, or a tag that disagrees with the version. Use
    this for a release you are actually going to publish.

.EXAMPLE
    .\release.ps1
    .\release.ps1 -Version 1.1.0
    .\release.ps1 -Version 1.1.0 -Strict
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string] $Version,

    [switch] $Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src\Mk20Box'
$staging = Join-Path $root 'dist\Mk20Box'
$releaseDir = Join-Path $root 'release'
$assemblyInfo = Join-Path $project 'Properties\AssemblyInfo.cs'

function Write-Step($message) {
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Fail($message) {
    throw $message
}

# Provenance problems do not make the build wrong, only harder to trace, so by
# default they are reported and the pack continues. -Strict is for a release
# being published, where they do matter.
$script:warned = $false

function Warn($message) {
    $script:warned = $true

    if ($Strict) {
        Fail "$message (reported as an error because -Strict was given)"
    }

    Write-Host "    warning: $message" -ForegroundColor DarkYellow
}

# SimHub's own assemblies are referenced, never redistributed. Shipping them
# would risk loading a second copy beside SimHub's, and is not ours to give away.
# Anything referenced out of the SimHub folder belongs to SimHub and is never
# redistributed. Read from the project file rather than listed here, so adding
# or removing a reference - or SimHub gaining new assemblies - needs no edit to
# this script.
function Get-SimHubOwnedAssemblies {
    $xml = [xml](Get-Content (Join-Path $project 'Mk20Box.csproj') -Raw)

    $names = @(
        $xml.SelectNodes('//Reference[HintPath]') |
            Where-Object { $_.HintPath -like '*SIMHUB_INSTALL_PATH*' } |
            ForEach-Object { $_.GetAttribute('Include') }
    )

    if ($names.Count -eq 0) {
        Fail 'No SimHub references were found in the project file. The payload check would be meaningless, so refusing to pack.'
    }

    return $names
}

# What the payload is allowed to look like. Expressed as shapes rather than file
# names, so a new dependency needs no edit here either, while anything landing
# somewhere unexpected is still caught. Note that -like treats * as matching
# separators too, so the asset patterns name the extensions rather than relying
# on the folder alone.
$allowedPayload = @(
    'Mk20Box.dll'
    'Mk20Box\*.dll'
    'Mk20Box\Mk20Assets\*.png'
    'Mk20Box\Mk20Assets\*.jpg'
    'Mk20Box\Mk20Assets\*.gif'
    'Mk20Box\Mk20Assets\*.svg'
    'Languages\Mk20Box.resx'
)

# Quoted in the install notes. The plugin API changes between SimHub releases,
# and "it does not load" is almost always a version mismatch.
$MinimumSimHubVersion = '9.12.1'

# ---- pre-flight -------------------------------------------------------------

Write-Step 'Checking the source'

# Recorded in INSTALL.txt so a published build can be traced back. A dirty tree
# only makes that record approximate, which is worth saying but not worth
# refusing a local build over.
$commit = (& git -C $root rev-parse --short HEAD).Trim()
$dirty = @(& git -C $root status --porcelain)

if ($dirty.Count -gt 0) {
    Warn "$($dirty.Count) uncommitted change(s); this build cannot be reproduced from commit $commit alone"
    $commit = "$commit+local"
}

$submodule = @(& git -C $root submodule status | Where-Object { $_ -match '^\s*[-+]' })

if ($submodule.Count -gt 0) {
    $submodule | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkYellow }
    Warn 'the MK20Control submodule is missing or modified; run git submodule update --init --recursive'
}

Write-Host "    commit $commit" -ForegroundColor Gray

# ---- version ----------------------------------------------------------------

if ($Version) {
    Write-Step "Stamping version $Version"

    $stamped = if (($Version -split '\.').Count -eq 3) { "$Version.0" } else { $Version }
    $text = Get-Content $assemblyInfo -Raw

    $text = [regex]::Replace(
        $text,
        '(?m)^\[assembly: AssemblyVersion\("[^"]*"\)\]',
        "[assembly: AssemblyVersion(""$stamped"")]")

    $text = [regex]::Replace(
        $text,
        '(?m)^\[assembly: AssemblyFileVersion\("[^"]*"\)\]',
        "[assembly: AssemblyFileVersion(""$stamped"")]")

    Set-Content $assemblyInfo $text -NoNewline
    Write-Host "    AssemblyInfo.cs updated - remember to commit it" -ForegroundColor Gray
}

# ---- clean build ------------------------------------------------------------

Write-Step 'Rebuilding from clean'

# Build intermediates only. The previous release is left alone until a new one
# is ready to replace it, so a failed build cannot destroy a good release.
foreach ($stale in @((Join-Path $project 'bin'), (Join-Path $project 'obj'), $staging)) {
    if (Test-Path $stale) {
        Remove-Item $stale -Recurse -Force
    }
}

& (Join-Path $root 'build.ps1') -Configuration Release | Out-Null

if ($LASTEXITCODE -ne 0) {
    Fail "Build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path (Join-Path $staging 'Mk20Box.dll'))) {
    Fail 'The build produced no plugin assembly.'
}

# ---- tests ------------------------------------------------------------------

Write-Step 'Running the tests'

# A release is the last place a regression should be discovered, so a failing
# test stops the pack outright rather than warning. Unlike the provenance
# checks, this is never downgraded by -Strict being absent.
$testProject = Join-Path $root 'src\Mk20Box.Tests\Mk20Box.Tests.csproj'

if (-not (Test-Path $testProject)) {
    Fail "The test project is missing from '$testProject'. Refusing to pack an unverified build."
}

$testOutput = & dotnet test $testProject --configuration Release --nologo 2>&1
$testExit = $LASTEXITCODE

# The summary line carries the counts; the rest is build noise nobody needs
# unless something failed.
$summary = $testOutput | Select-String -Pattern '^(Passed|Failed)!\s+-\s+Failed:' | Select-Object -Last 1

if ($testExit -ne 0) {
    $testOutput | Select-String -Pattern 'error|Failed ' | Select-Object -First 20 |
        ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    Fail 'The tests failed. Fix them before packing a release.'
}

if ($summary) {
    Write-Host "    $($summary.Line.Trim())" -ForegroundColor Gray
}
else {
    Warn 'the test run reported no summary; check that the suite actually ran'
}

# ---- checks -----------------------------------------------------------------

Write-Step 'Checking the payload'

$doNotShip = Get-SimHubOwnedAssemblies
Write-Host "    $($doNotShip.Count) SimHub assemblies must not ship (read from the project file)" -ForegroundColor Gray

$shipped = @(Get-ChildItem $staging -Recurse -File -Filter *.dll)
$offenders = @($shipped | Where-Object { $doNotShip -contains $_.BaseName })

if ($offenders.Count -gt 0) {
    $offenders | ForEach-Object { Write-Host "    $($_.Name)" -ForegroundColor Red }
    Fail 'The payload contains assemblies that belong to SimHub. Check that their references are marked <Private>False</Private>.'
}

# Fail closed: anything not matching an expected shape is treated as a mistake,
# so a stray file cannot quietly reach users.
$unexpected = @(
    Get-ChildItem $staging -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($staging.Length).TrimStart('\')
        $known = $false

        foreach ($pattern in $allowedPayload) {
            if ($relative -like $pattern) {
                $known = $true
                break
            }
        }

        if (-not $known) { $relative }
    }
)

if ($unexpected.Count -gt 0) {
    $unexpected | Select-Object -First 10 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    Fail "$($unexpected.Count) file(s) in the payload do not match anything MK20Box ships. Add them to `$allowedPayload if they are intentional."
}

# A missing icon library still builds and still runs, so it has to be checked
# rather than assumed.
$assets = Join-Path $staging 'Mk20Box\Mk20Assets'
if (-not (Test-Path $assets)) {
    Fail 'The icon library is missing from the payload.'
}

$assetCount = @(Get-ChildItem $assets -Recurse -File).Count
if ($assetCount -lt 100) {
    Fail "Only $assetCount icons were staged, which suggests an incomplete build."
}

if (-not (Test-Path (Join-Path $staging 'Languages\Mk20Box.resx'))) {
    Fail 'The language file is missing from the payload.'
}

# Debug symbols are not shipped: they are only useful with matching sources.
$symbols = @(Get-ChildItem $staging -Recurse -File -Filter *.pdb)
if ($symbols.Count -gt 0) {
    $symbols | ForEach-Object { Write-Host "    $($_.Name)" -ForegroundColor Red }
    Fail 'The payload contains debug symbols. Release builds should not stage them.'
}

$pluginDll = Get-Item (Join-Path $staging 'Mk20Box.dll')
$releaseVersion = $pluginDll.VersionInfo.FileVersion

# A tag that disagrees with the assembly makes the published version a lie.
$tag = (& git -C $root tag --points-at HEAD | Select-Object -First 1)

if ($tag) {
    $tagVersion = ($tag -replace '^v', '').Trim()
    $normalised = if (($tagVersion -split '\.').Count -eq 3) { "$tagVersion.0" } else { $tagVersion }

    if ($normalised -ne $releaseVersion) {
        Warn "tag '$tag' does not match the built version $releaseVersion"
    }
    else {
        Write-Host "    tag $tag matches the build" -ForegroundColor Gray
    }
}

Write-Host "    version $releaseVersion, $($shipped.Count) assemblies, $assetCount icons" -ForegroundColor Gray

# ---- pack -------------------------------------------------------------------

Write-Step 'Packing'

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

# Kept as a folder as well as a zip: the folder is what you copy into SimHub
# while testing, the zip is what you publish.
$payload = Join-Path $releaseDir "Mk20Box-$releaseVersion"

if (Test-Path $payload) {
    Remove-Item $payload -Recurse -Force
}

# Everything that belongs in the SimHub folder goes under SimHub\, and nothing
# else does. Users drag the contents of one folder and cannot accidentally
# litter their installation with the readme and licence.
$simHubPart = Join-Path $payload 'SimHub'
New-Item -ItemType Directory -Path $simHubPart -Force | Out-Null

Copy-Item (Join-Path $staging '*') $simHubPart -Recurse -Force

foreach ($extra in @('LICENSE', 'README.md')) {
    $source = Join-Path $root $extra
    if (Test-Path $source) {
        Copy-Item $source $payload -Force
    }
}

$docs = Join-Path $root 'docs'
if (Test-Path $docs) {
    $docsTarget = Join-Path $payload 'docs'
    New-Item -ItemType Directory -Path $docsTarget -Force | Out-Null
    Copy-Item (Join-Path $docs '*.md') $docsTarget -Force
}

# Tells whoever unzips it exactly where the files go.
@"
MK20Box $releaseVersion
Built from commit $commit, against SimHub $MinimumSimHubVersion.

REQUIREMENTS
    SimHub $MinimumSimHubVersion or later.

INSTALL
    1. Close SimHub. It holds Mk20Box.dll open while running, so copying
       over a running install fails half way.

    2. Copy everything inside the SimHub\ folder into your SimHub folder,
       usually C:\Program Files (x86)\SimHub, keeping the layout intact:

           Mk20Box.dll             the plugin
           Mk20Box\                its dependencies and icon library
           Languages\Mk20Box.resx  translations

       If SimHub is installed elsewhere, its folder is recorded in the
       registry under HKCU\SOFTWARE\SimHub, value InstallDirectory.

    3. Start SimHub. It offers to enable new plugins it has found; enable
       MK20Box. You can also enable it later under Settings -> Plugins.

    4. Plug in the MK20. It is detected automatically.

UNINSTALL
    Close SimHub, then delete from the SimHub folder:

        Mk20Box.dll
        Mk20Box\
        Languages\Mk20Box.resx
        PluginsData\Common\Mk20BoxPlugin.GeneralSettings.json
        PluginsData\Common\_Backups\Mk20BoxPlugin.GeneralSettings_b*.json

    The last two hold your profiles, so keep them if you may reinstall.
    Imported profile artwork lives in %LOCALAPPDATA%\Mk20Box and can be
    deleted too. Nothing SimHub ships is modified.

REFERENCE
    LICENSE, README.md and docs\ sit outside SimHub\ on purpose - they are
    for reading, not for copying.
"@ | Set-Content (Join-Path $payload 'INSTALL.txt')

$archive = Join-Path $releaseDir "Mk20Box-$releaseVersion.zip"

if (Test-Path $archive) {
    Remove-Item $archive -Force
}

Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $archive

$hash = (Get-FileHash $archive -Algorithm SHA256).Hash
"$hash  $(Split-Path $archive -Leaf)" | Set-Content "$archive.sha256"

$size = [math]::Round((Get-Item $archive).Length / 1MB, 2)

Write-Host ''
Write-Host "  folder   : $payload" -ForegroundColor Gray
Write-Host "  archive  : $archive" -ForegroundColor Gray
Write-Host "  size     : $size MB" -ForegroundColor Gray
Write-Host "  sha256   : $($hash.Substring(0, 32))..." -ForegroundColor Gray
Write-Host ''

if ($script:warned) {
    Write-Host "MK20Box $releaseVersion packed, with warnings above." -ForegroundColor Yellow
    Write-Host 'Fine for testing. Before publishing, commit your changes and re-run with -Strict.' -ForegroundColor Yellow
}
else {
    Write-Host "MK20Box $releaseVersion is ready to publish." -ForegroundColor Green
}
