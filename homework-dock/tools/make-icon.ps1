# Generates assets/app.ico from a drawn 256px artwork (PNG-compressed ICO entries).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root 'assets'
New-Item -ItemType Directory -Force $outDir | Out-Null

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $s / 256.0

    # rounded app tile with a warm coral gradient
    $rect = New-Object System.Drawing.RectangleF (8 * $k), (8 * $k), (240 * $k), (240 * $k)
    $path = New-RoundedPath $rect.X $rect.Y $rect.Width $rect.Height (58 * $k)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 238, 112, 77)), ([System.Drawing.Color]::FromArgb(255, 190, 65, 43)), 55.0
    $g.FillPath($brush, $path)

    # soft top sheen
    $sheenRect = New-Object System.Drawing.RectangleF (8 * $k), (8 * $k), (240 * $k), (128 * $k)
    $sheenPath = New-RoundedPath $sheenRect.X $sheenRect.Y $sheenRect.Width $sheenRect.Height (58 * $k)
    $sheen = New-Object System.Drawing.Drawing2D.LinearGradientBrush $sheenRect, ([System.Drawing.Color]::FromArgb(48, 255, 255, 255)), ([System.Drawing.Color]::FromArgb(0, 255, 255, 255)), 90.0
    $g.FillPath($sheen, $sheenPath)

    # white check mark
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 250, 242)), ([float](22 * $k))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $pts = @(
        (New-Object System.Drawing.PointF (66 * $k), (132 * $k)),
        (New-Object System.Drawing.PointF (108 * $k), (174 * $k)),
        (New-Object System.Drawing.PointF (190 * $k), (88 * $k))
    )
    $g.DrawLines($pen, $pts)

    # a small cream dot at top-right for character
    $dotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 247, 231))
    $g.FillEllipse($dotBrush, (168 * $k), (44 * $k), (28 * $k), (28 * $k))

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,$ms.ToArray()
    $bmp.Dispose()
    $ms.Dispose()
}

# Build the ICO container.  Each entry carries a PNG payload.
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $data = $pngs[$i]
    $bw.Write([Byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([Byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([Byte]0)
    $bw.Write([Byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
for ($i = 0; $i -lt $pngs.Count; $i++) { $bw.Write($pngs[$i]) }
$bw.Flush()
$ico = Join-Path $outDir 'app.ico'
[System.IO.File]::WriteAllBytes($ico, $ms.ToArray())
$bw.Dispose(); $ms.Dispose()

# Preview at 256 without relying on the ICO parser.
$bmp = New-Object System.Drawing.Bitmap 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)
$k = 1.0
$rect = New-Object System.Drawing.RectangleF 8, 8, 240, 240
$path = New-RoundedPath 8 8 240 240 58
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 238, 112, 77)), ([System.Drawing.Color]::FromArgb(255, 190, 65, 43)), 55.0
$g.FillPath($brush, $path)
$sheenRect = New-Object System.Drawing.RectangleF 8, 8, 240, 128
$sheenPath = New-RoundedPath 8 8 240 128 58
$sheen = New-Object System.Drawing.Drawing2D.LinearGradientBrush $sheenRect, ([System.Drawing.Color]::FromArgb(48, 255, 255, 255)), ([System.Drawing.Color]::FromArgb(0, 255, 255, 255)), 90.0
$g.FillPath($sheen, $sheenPath)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 250, 242)), 22
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.DrawLines($pen, @((New-Object System.Drawing.PointF 66, 132), (New-Object System.Drawing.PointF 108, 174), (New-Object System.Drawing.PointF 190, 88)))
$dotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 247, 231))
$g.FillEllipse($dotBrush, 168, 44, 28, 28)
$g.Dispose()
$bmp.Save((Join-Path $outDir 'icon-preview.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host "ICO written: $ico ($([Math]::Round((Get-Item $ico).Length / 1KB, 1)) KB)"
Write-Host "Preview: $(Join-Path $outDir 'icon-preview.png')"
