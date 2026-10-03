$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assetDir = Join-Path $root "src\AGLauncher\Assets"
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
$pngPath = Join-Path $assetDir "app-icon.png"
$icoPath = Join-Path $assetDir "app-icon.ico"

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

$rect = New-Object System.Drawing.Rectangle(0,0,$size,$size)
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect,
    [System.Drawing.Color]::FromArgb(255,31,33,36),
    [System.Drawing.Color]::FromArgb(255,14,15,17),
    45.0)
$g.FillRectangle($bg,$rect)

$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)

# Atenoct AC monogram, matched to the supplied launcher icon.
$aPts = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(57,172),
    [System.Drawing.PointF]::new(105,86),
    [System.Drawing.PointF]::new(132,86),
    [System.Drawing.PointF]::new(86,172)
)
$cPts = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(136,86),
    [System.Drawing.PointF]::new(187,86),
    [System.Drawing.PointF]::new(199,107),
    [System.Drawing.PointF]::new(153,107),
    [System.Drawing.PointF]::new(137,136),
    [System.Drawing.PointF]::new(153,160),
    [System.Drawing.PointF]::new(199,160),
    [System.Drawing.PointF]::new(187,181),
    [System.Drawing.PointF]::new(136,181),
    [System.Drawing.PointF]::new(111,136)
)
$g.FillPolygon($white,$aPts)
$g.FillPolygon($white,$cPts)

$bmp.Save($pngPath,[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bg.Dispose(); $white.Dispose(); $bmp.Dispose()

# Build a modern single-image ICO containing the 256px PNG.
$png = [System.IO.File]::ReadAllBytes($pngPath)
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]1)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]32)
$bw.Write([UInt32]$png.Length)
$bw.Write([UInt32]22)
$bw.Write($png)
$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath,$ms.ToArray())
$bw.Dispose(); $ms.Dispose()

Write-Host "Generated AG Launcher icon: $icoPath"