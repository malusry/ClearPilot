param(
    [string]$SourcePng = (Join-Path $PSScriptRoot "..\assets\icon\clearpilot-icon.png"),
    [string]$OutputIco = (Join-Path $PSScriptRoot "..\assets\icon\clearpilot.ico")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $SourcePng)) {
    throw "Source PNG was not found: $SourcePng"
}

$outputDirectory = Split-Path -Parent $OutputIco
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

function New-ResizedPngBytes {
    param(
        [System.Drawing.Image]$Source,
        [int]$Size
    )

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $sourceSize = [Math]::Min($Source.Width, $Source.Height)
    $sourceX = [Math]::Floor(($Source.Width - $sourceSize) / 2)
    $sourceY = [Math]::Floor(($Source.Height - $sourceSize) / 2)
    $sourceRect = New-Object System.Drawing.Rectangle $sourceX, $sourceY, $sourceSize, $sourceSize
    $targetRect = New-Object System.Drawing.Rectangle 0, 0, $Size, $Size

    $graphics.DrawImage($Source, $targetRect, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    $bitmap.Dispose()

    return ,$bytes
}

$source = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $SourcePng))
$sizes = @(256, 128, 64, 48, 32, 16)
$pngImages = New-Object System.Collections.Generic.List[byte[]]

try {
    foreach ($size in $sizes) {
        $pngImages.Add((New-ResizedPngBytes -Source $source -Size $size))
    }
}
finally {
    $source.Dispose()
}

$writer = New-Object System.IO.BinaryWriter ([System.IO.File]::Open($OutputIco, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write))
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$sizes.Count)

    $imageOffset = 6 + (16 * $sizes.Count)
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $bytes = $pngImages[$index]
        $iconSizeByte = if ($size -eq 256) { 0 } else { $size }

        $writer.Write([byte]$iconSizeByte)
        $writer.Write([byte]$iconSizeByte)
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

Write-Host "Generated $OutputIco from $SourcePng"
