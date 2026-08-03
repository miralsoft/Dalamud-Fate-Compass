<#
.SYNOPSIS
  Brings the plugin icon into the shape Dalamud needs.
.DESCRIPTION
  Run this after dropping in new artwork. It rewrites the file in place, so the repository only
  ever holds a finished image and nobody has to remember the rule.

  Dalamud loads plugin icons with a square requirement and a 512 pixel limit, and discards
  anything else in favour of its default icon rather than scaling it. That is not a preference,
  and it is not visible in an image viewer: a 1024 square icon simply never appears, with nothing
  anywhere saying why.

  Running it twice is harmless.
#>
param(
    [string]$Directory = "src/FateCompass/images"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$size = 512
$iconPath = Join-Path $Directory "icon.png"

if (-not (Test-Path $iconPath)) {
    Write-Host "No icon at $iconPath, nothing to do."
    return
}

# Read through memory rather than from the path. A Bitmap constructed from a file keeps that file
# open for as long as it lives, and writing the result back to the same name then fails inside
# GDI+ with nothing but "a generic error", which says nothing about the actual cause.
$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $iconPath).Path)
$stream = New-Object System.IO.MemoryStream(, $bytes)
$src = [System.Drawing.Bitmap]::new($stream)
$was = "$($src.Width) x $($src.Height)"

$final = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($final)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $size, $size))
$g.Dispose()
$src.Dispose()

$final.Save(((Resolve-Path $Directory).Path + "\icon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$final.Dispose()

Write-Host "icon.png: $was -> $size x $size"
