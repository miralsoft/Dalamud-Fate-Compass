<#
.SYNOPSIS
  Generates the standalone Dalamud repository index (pluginmaster.json) for Fate Compass.
.DESCRIPTION
  Builds a one-entry pluginmaster **array** from the *built* plugin manifest, the one
  DalamudPackager emits into bin/Release, which already carries DalamudApiLevel, AssemblyVersion,
  Punchline and the tags. The download links point at the stable
  `releases/latest/download/latest.zip` redirect, so the address never changes from release to
  release.

  Emitting from the built manifest guarantees the entry carries `DalamudApiLevel`; without it
  Dalamud hides the plugin as "outdated". Wrapping in an array is equally load-bearing: Dalamud
  rejects a bare object.

  This file is for anyone who wants Fate Compass on its own, without the aggregate index at
  miralsoft/Dalamud-Plugins. That index builds its own entry from the release asset and does not
  read this file.
#>
param(
    [string]$Tag = "",
    [string]$Repository = $env:GITHUB_REPOSITORY,
    [string]$Manifest = "src/FateCompass/bin/Release/FateCompass/FateCompass.json",
    [string]$Output = "pluginmaster.json"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Repository)) {
    $Repository = "miralsoft/Dalamud-Fate-Compass"
}

if (-not (Test-Path $Manifest)) {
    throw "Built manifest not found at '$Manifest'. Run 'dotnet build -c Release' first."
}

# Start from the built manifest so every field DalamudPackager produced carries through unchanged.
$m = Get-Content $Manifest -Raw | ConvertFrom-Json

# Stable redirect to the newest published release's asset, independent of the tag name.
$download = "https://github.com/$Repository/releases/latest/download/latest.zip"
$m | Add-Member -NotePropertyName DownloadLinkInstall -NotePropertyValue $download -Force
$m | Add-Member -NotePropertyName DownloadLinkUpdate  -NotePropertyValue $download -Force
$m | Add-Member -NotePropertyName DownloadLinkTesting -NotePropertyValue $download -Force

# Dalamud's plugin master must be a JSON array, even with a single entry. Windows PowerShell 5.1
# has no -AsArray and collapses a one-element array into a bare object, so wrap it by hand when
# that happened. Written as UTF-8 *without* a byte order mark, because Dalamud's parser rejects
# one and Set-Content -Encoding utf8 adds it on 5.1.
$json = @($m) | ConvertTo-Json -Depth 10
if ($json.TrimStart().StartsWith('{')) {
    $json = "[$([Environment]::NewLine)$json$([Environment]::NewLine)]"
}

[System.IO.File]::WriteAllText(
    (Join-Path (Get-Location) $Output),
    $json,
    (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Wrote $Output for tag '$Tag' (AssemblyVersion=$($m.AssemblyVersion), DalamudApiLevel=$($m.DalamudApiLevel))."
