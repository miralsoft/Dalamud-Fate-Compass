# Builds the plugin and reports the path to register as a Dalamud dev plugin.
# Runs the same gates the rules require before a build counts as good (C-03, T-03, S-08).

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$SkipChecks
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not $SkipChecks) {
    Write-Host '--- format ---' -ForegroundColor Cyan
    dotnet format FateCompass.slnx --verify-no-changes
    if ($LASTEXITCODE -ne 0) { throw 'Formatting check failed. Run: dotnet format FateCompass.slnx' }
}

Write-Host '--- build ---' -ForegroundColor Cyan
dotnet build FateCompass.slnx -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if (-not $SkipChecks) {
    Write-Host '--- tests ---' -ForegroundColor Cyan
    dotnet test FateCompass.slnx -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

$output = Join-Path $PSScriptRoot "src\FateCompass\bin\$Configuration"
$dll = Join-Path $output 'FateCompass.dll'

if (-not (Test-Path $dll)) { throw "Expected plugin assembly not found at $dll" }

# --- Publish to a folder Dalamud watches -----------------------------------------
#
# Dalamud reloads a dev plugin as soon as its main assembly changes on disk. This plugin
# ships two assemblies, and MSBuild writes them one after another straight into bin/.
# Pointing Dalamud at bin/ therefore lets a reload fire while FateCompass.dll is new and
# FateCompass.Core.dll is still the old one, or still being written, which loads a plugin
# against a dependency that does not match it and takes the game down.
#
# Staging fixes the ordering: everything is copied here with the main assembly LAST, so by
# the time Dalamud notices it, every file it depends on is already complete.

$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$mainAssembly = 'FateCompass.dll'
$supporting = Get-ChildItem $output -File | Where-Object { $_.Name -ne $mainAssembly }

foreach ($file in $supporting) {
    Copy-Item $file.FullName (Join-Path $dist $file.Name) -Force
}

# The icon, if there is one. A dev plugin gets its icon from its own directory rather than from
# the manifest's IconUrl, so without this the locally installed build shows the default picture
# while the plugin list shows the real one, and the two never agree.
$images = Join-Path $output 'images'
if (Test-Path $images) {
    $target = Join-Path $dist 'images'
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item (Join-Path $images '*') $target -Force -Recurse
}

# Last, and only once everything else is in place.
Copy-Item $dll (Join-Path $dist $mainAssembly) -Force

$devPath = Join-Path $dist $mainAssembly

Write-Host ''
Write-Host 'Build complete.' -ForegroundColor Green
Write-Host ''
Write-Host 'Register this path in Dalamud:' -ForegroundColor Yellow
Write-Host "  /xlsettings  ->  Experimental  ->  Dev Plugin Locations" -ForegroundColor Gray
Write-Host ''
Write-Host "  $devPath" -ForegroundColor White
Write-Host ''
Write-Host 'The main assembly is copied last on purpose, so the auto-reload never sees a' -ForegroundColor Gray
Write-Host 'half-updated set of files. Update the path above if you registered bin\ before.' -ForegroundColor Gray
