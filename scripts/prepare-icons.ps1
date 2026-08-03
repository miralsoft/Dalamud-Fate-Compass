<#
.SYNOPSIS
  Brings the artwork in src/FateHelper/images into the shape Dalamud and the minimap need.
.DESCRIPTION
  Run this after dropping in new artwork. It rewrites the files in place, so the repository only
  ever holds finished images and nobody has to remember the rules.

  icon.png    goes to exactly 512 x 512. Dalamud loads plugin icons with a square requirement and
              a 512 limit and discards anything else in favour of the default icon rather than
              scaling it, so this is not a preference.

  minimap.png is cropped to what is actually drawn, padded back to a square and scaled to 512.
              Generated artwork tends to sit on a wide canvas with the motif somewhere in the
              middle; drawn into the square button that would squash it. The alpha channel is
              what decides where the motif ends, which is also the check that the image really
              is cut out rather than standing on white.

  Running it twice is harmless. The crop finds the same bounds again, because everything it adds
  is transparent.
#>
param(
    [string]$Directory = "src/FateHelper/images",

    # Air around the motif, as a fraction of its longer side, so the points of a star do not sit
    # flush against the edge of the button.
    [double]$Margin = 0.04,

    # Alpha below this counts as background. Not zero: a glow fades out over many almost invisible
    # pixels, and taking those as motif would find the whole canvas.
    [int]$AlphaThreshold = 8
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$size = 512

<#
  Loads through memory rather than from the path. A Bitmap constructed from a file keeps that file
  open for as long as it lives, and writing the result back to the same name then fails inside GDI+
  with nothing but "a generic error", which says nothing about the actual cause.
#>
function Read-Bitmap {
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $Path).Path)
    $stream = New-Object System.IO.MemoryStream(, $bytes)
    return [System.Drawing.Bitmap]::new($stream)
}

function Save-Square {
    param([System.Drawing.Bitmap]$Source, [System.Drawing.Rectangle]$Crop, [string]$Path)

    $side = [Math]::Max($Crop.Width, $Crop.Height)
    $side = [int]([Math]::Ceiling($side * (1.0 + $Margin)))

    $square = New-Object System.Drawing.Bitmap $side, $side, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($square)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # Centred, so the motif keeps its proportions instead of being stretched to fit.
    $target = New-Object System.Drawing.Rectangle(
        [int](($side - $Crop.Width) / 2), [int](($side - $Crop.Height) / 2), $Crop.Width, $Crop.Height)
    $g.DrawImage($Source, $target, $Crop, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()

    $final = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($final)
    $g2.Clear([System.Drawing.Color]::Transparent)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g2.DrawImage($square, (New-Object System.Drawing.Rectangle 0, 0, $size, $size))
    $g2.Dispose()
    $square.Dispose()

    $final.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $final.Dispose()
}

function Get-MotifBounds {
    param([System.Drawing.Bitmap]$Bitmap)

    $rect = New-Object System.Drawing.Rectangle 0, 0, $Bitmap.Width, $Bitmap.Height
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $Bitmap.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $stride = $data.Stride
    $Bitmap.UnlockBits($data)

    $minX = $Bitmap.Width; $maxX = -1; $minY = $Bitmap.Height; $maxY = -1
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        $row = $y * $stride
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if ($bytes[$row + $x * 4 + 3] -gt $AlphaThreshold) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if ($maxX -lt 0) { throw "No visible pixels found; is the image fully transparent?" }
    return New-Object System.Drawing.Rectangle $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)
}

# --- The framed icon: square already, only the size matters. ------------------------------------
$iconPath = Join-Path $Directory "icon.png"
if (Test-Path $iconPath) {
    $src = Read-Bitmap $iconPath
    $whole = New-Object System.Drawing.Rectangle 0, 0, $src.Width, $src.Height
    $was = "$($src.Width) x $($src.Height)"

    # No margin here. The frame is meant to reach the edge of the tile.
    $keep = $Margin; $script:Margin = 0
    Save-Square -Source $src -Crop $whole -Path ((Resolve-Path $Directory).Path + "\icon.png")
    $script:Margin = $keep

    $src.Dispose()
    Write-Host "icon.png:    $was -> $size x $size"
}

# --- The cut-out: crop to the motif, square it, scale it. ---------------------------------------
$minimapPath = Join-Path $Directory "minimap.png"
if (Test-Path $minimapPath) {
    $src = Read-Bitmap $minimapPath
    $was = "$($src.Width) x $($src.Height)"
    $bounds = Get-MotifBounds -Bitmap $src

    Save-Square -Source $src -Crop $bounds -Path ((Resolve-Path $Directory).Path + "\minimap.png")

    $src.Dispose()
    Write-Host "minimap.png: $was, motif $($bounds.Width) x $($bounds.Height) -> $size x $size"
}
