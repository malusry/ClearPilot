param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\assets\icon")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
}

$pngPath = Join-Path $OutputDirectory "clearpilot-icon.png"
$icoPath = Join-Path $OutputDirectory "clearpilot.ico"

function New-IconBitmap {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $scale = $Size / 1024.0
    function S([double]$Value) { return [single]($Value * $scale) }

    $backgroundRect = New-Object System.Drawing.RectangleF (S 72), (S 72), (S 880), (S 880)
    $backgroundPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $radius = (S 210)
    $diameter = $radius * 2
    $backgroundPath.AddArc($backgroundRect.X, $backgroundRect.Y, $diameter, $diameter, 180, 90)
    $backgroundPath.AddArc($backgroundRect.Right - $diameter, $backgroundRect.Y, $diameter, $diameter, 270, 90)
    $backgroundPath.AddArc($backgroundRect.Right - $diameter, $backgroundRect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $backgroundPath.AddArc($backgroundRect.X, $backgroundRect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $backgroundPath.CloseFigure()

    $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $backgroundRect, ([System.Drawing.Color]::FromArgb(8, 28, 36)), ([System.Drawing.Color]::FromArgb(16, 63, 70)), 45
    $graphics.FillPath($backgroundBrush, $backgroundPath)

    $shadowPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 22, 74, 84)), (S 116)
    $shadowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $shadowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($shadowPen, (S 239), (S 234), (S 552), (S 556), 143, 216)

    $cBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.RectangleF (S 267), (S 261), (S 580), (S 540)), ([System.Drawing.Color]::FromArgb(142, 255, 255)), ([System.Drawing.Color]::FromArgb(59, 36, 24)), 70
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend
    $blend.Positions = [single[]]@(0.0, 0.38, 0.68, 0.82, 1.0)
    $blend.Colors = [System.Drawing.Color[]]@(
        [System.Drawing.Color]::FromArgb(142, 255, 255),
        [System.Drawing.Color]::FromArgb(34, 202, 217),
        [System.Drawing.Color]::FromArgb(11, 155, 165),
        [System.Drawing.Color]::FromArgb(199, 149, 77),
        [System.Drawing.Color]::FromArgb(59, 36, 24)
    )
    $cBrush.InterpolationColors = $blend
    $cyanPen = New-Object System.Drawing.Pen $cBrush, (S 82)
    $cyanPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $cyanPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($cyanPen, (S 267), (S 261), (S 502), (S 502), 145, 208)

    $highlightPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 215, 255, 255)), (S 10)
    $highlightPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $highlightPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($highlightPen, (S 310), (S 276), (S 426), (S 300), 205, 104)
    $graphics.DrawArc($highlightPen, (S 320), (S 556), (S 350), (S 220), 50, 118)

    $tailBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.RectangleF (S 610), (S 690), (S 302), (S 210)), ([System.Drawing.Color]::FromArgb(33, 199, 189)), ([System.Drawing.Color]::FromArgb(44, 26, 18)), 34
    $tailBlend = New-Object System.Drawing.Drawing2D.ColorBlend
    $tailBlend.Positions = [single[]]@(0.0, 0.18, 0.62, 1.0)
    $tailBlend.Colors = [System.Drawing.Color[]]@(
        [System.Drawing.Color]::FromArgb(33, 199, 189),
        [System.Drawing.Color]::FromArgb(217, 170, 95),
        [System.Drawing.Color]::FromArgb(138, 82, 43),
        [System.Drawing.Color]::FromArgb(44, 26, 18)
    )
    $tailBrush.InterpolationColors = $tailBlend
    $tailPen = New-Object System.Drawing.Pen $tailBrush, (S 82)
    $tailPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $tailPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $tailCurve = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tailCurve.AddBezier((S 620), (S 728), (S 654), (S 738), (S 684), (S 750), (S 714), (S 767))
    $graphics.DrawPath($tailPen, $tailCurve)

    $bristles = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bristlePoints = @(
        (New-Object System.Drawing.PointF (S 660), (S 712)),
        (New-Object System.Drawing.PointF (S 912), (S 764)),
        (New-Object System.Drawing.PointF (S 683), (S 848))
    )
    $bristles.AddPolygon($bristlePoints)
    $graphics.FillPath($tailBrush, $bristles)
    $bristlePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(240, 207, 145)), (S 13)
    $graphics.DrawPath($bristlePen, $bristles)

    $linePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(190, 249, 211, 139)), (S 10)
    $linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($linePen, (S 685), (S 742), (S 876), (S 777))
    $linePen.Color = [System.Drawing.Color]::FromArgb(180, 152, 97, 52)
    $graphics.DrawLine($linePen, (S 681), (S 784), (S 855), (S 831))
    $linePen.Color = [System.Drawing.Color]::FromArgb(170, 59, 36, 24)
    $graphics.DrawLine($linePen, (S 675), (S 825), (S 825), (S 884))
    $linePen.Color = [System.Drawing.Color]::FromArgb(130, 93, 55, 31)
    $graphics.DrawLine($linePen, (S 756), (S 728), (S 713), (S 861))

    $outerDiskBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(217, 253, 255))
    $diskBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(121, 223, 232))
    $coreBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(11, 46, 54))
    $graphics.FillEllipse($outerDiskBrush, (S 430), (S 430), (S 164), (S 164))
    $graphics.FillEllipse($diskBrush, (S 457), (S 457), (S 110), (S 110))
    $graphics.FillEllipse($coreBrush, (S 483), (S 483), (S 58), (S 58))

    $slotPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(190, 234, 255, 255)), (S 10)
    $slotPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $slotPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($slotPen, (S 473), (S 486), (S 551), (S 486))
    $slotPen.Color = [System.Drawing.Color]::FromArgb(110, 11, 46, 54)
    $graphics.DrawLine($slotPen, (S 474), (S 538), (S 550), (S 538))

    $graphics.Dispose()
    return $bitmap
}

$preview = New-IconBitmap -Size 1024
$preview.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

$sizes = @(256, 128, 64, 48, 32, 16)
$pngImages = @()
foreach ($size in $sizes) {
    $bitmap = New-IconBitmap -Size $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngImages += ,$stream.ToArray()
    $stream.Dispose()
    $bitmap.Dispose()
}

$writer = New-Object System.IO.BinaryWriter ([System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write))
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$sizes.Count)

    $imageOffset = 6 + (16 * $sizes.Count)
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $bytes = $pngImages[$index]
        $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
        $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$bytes.Length)
        $writer.Write([UInt32]$imageOffset)
        $imageOffset += $bytes.Length
    }

    foreach ($bytes in $pngImages) {
        $writer.Write($bytes)
    }
}
finally {
    $writer.Dispose()
}

Write-Host "Generated $pngPath"
Write-Host "Generated $icoPath"
