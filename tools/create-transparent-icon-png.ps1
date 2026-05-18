param(
    [string]$SourcePng = (Join-Path $PSScriptRoot "..\assets\icon\clearpilot-icon-source.png"),
    [string]$OutputPng = (Join-Path $PSScriptRoot "..\assets\icon\clearpilot-icon.png")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $SourcePng)) {
    throw "Source PNG was not found: $SourcePng"
}

$outputDirectory = Split-Path -Parent $OutputPng
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$code = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class ClearPilotTransparentIcon
{
    public static void Create(string sourcePath, string outputPath)
    {
        using (var loaded = Image.FromFile(sourcePath))
        using (var source = new Bitmap(loaded.Width, loaded.Height, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(source))
            {
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(loaded, 0, 0, loaded.Width, loaded.Height);
            }

            var background = EstimateBackground(source);
            using (var output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                var rect = new Rectangle(0, 0, source.Width, source.Height);
                var sourceData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var outputData = output.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                try
                {
                    var sourceBytes = new byte[Math.Abs(sourceData.Stride) * source.Height];
                    var outputBytes = new byte[Math.Abs(outputData.Stride) * output.Height];
                    Marshal.Copy(sourceData.Scan0, sourceBytes, 0, sourceBytes.Length);

                    for (var y = 0; y < source.Height; y++)
                    {
                        for (var x = 0; x < source.Width; x++)
                        {
                            var sourceIndex = y * sourceData.Stride + x * 4;
                            var outputIndex = y * outputData.Stride + x * 4;

                            var b = sourceBytes[sourceIndex + 0];
                            var g = sourceBytes[sourceIndex + 1];
                            var r = sourceBytes[sourceIndex + 2];

                            var alpha = CalculateAlpha(r, g, b, background, x, y, source.Width, source.Height);

                            outputBytes[outputIndex + 0] = b;
                            outputBytes[outputIndex + 1] = g;
                            outputBytes[outputIndex + 2] = r;
                            outputBytes[outputIndex + 3] = alpha;
                        }
                    }

                    Marshal.Copy(outputBytes, 0, outputData.Scan0, outputBytes.Length);
                }
                finally
                {
                    source.UnlockBits(sourceData);
                    output.UnlockBits(outputData);
                }

                output.Save(outputPath, ImageFormat.Png);
            }
        }
    }

    private static Color EstimateBackground(Bitmap source)
    {
        const int patch = 96;
        long red = 0;
        long green = 0;
        long blue = 0;
        var count = 0;

        SamplePatch(source, 0, 0, patch, ref red, ref green, ref blue, ref count);
        SamplePatch(source, source.Width - patch, 0, patch, ref red, ref green, ref blue, ref count);
        SamplePatch(source, 0, source.Height - patch, patch, ref red, ref green, ref blue, ref count);
        SamplePatch(source, source.Width - patch, source.Height - patch, patch, ref red, ref green, ref blue, ref count);

        return Color.FromArgb(
            (int)(red / count),
            (int)(green / count),
            (int)(blue / count));
    }

    private static void SamplePatch(Bitmap source, int startX, int startY, int size, ref long red, ref long green, ref long blue, ref int count)
    {
        for (var y = Math.Max(0, startY); y < Math.Min(source.Height, startY + size); y += 3)
        {
            for (var x = Math.Max(0, startX); x < Math.Min(source.Width, startX + size); x += 3)
            {
                var color = source.GetPixel(x, y);
                red += color.R;
                green += color.G;
                blue += color.B;
                count++;
            }
        }
    }

    private static byte CalculateAlpha(byte r, byte g, byte b, Color background, int x, int y, int width, int height)
    {
        var luma = (0.299 * r) + (0.587 * g) + (0.114 * b);
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var saturation = max - min;
        var colorDistance = Math.Sqrt(
            ((r - background.R) * (r - background.R)) +
            ((g - background.G) * (g - background.G)) +
            ((b - background.B) * (b - background.B)));

        var darkness = Clamp01((228.0 - luma) / 145.0);
        var edgeContrast = Clamp01((colorDistance - 16.0) / 76.0);
        var inkTone = Clamp01(saturation / 120.0);
        var subject = Math.Max(edgeContrast, Math.Max(darkness * 0.92, Math.Min(edgeContrast, inkTone) * 0.72));

        var nx = (x + 0.5) / width;
        var ny = (y + 0.5) / height;
        var dx = nx - 0.50;
        var dy = ny - 0.52;
        var radius = Math.Sqrt((dx * dx) + (dy * dy));
        var ringMask = SmoothStep(0.17, 0.24, radius) * (1.0 - SmoothStep(0.41, 0.54, radius));
        var centerMarkMask = 1.0 - SmoothStep(0.13, 0.19, radius);

        var tailMask = 1.0 - SmoothStep(1.0, 1.25, EllipseDistance(nx, ny, 0.52, 0.69, 0.47, 0.29));
        var splashMask = 1.0 - SmoothStep(1.0, 1.35, EllipseDistance(nx, ny, 0.34, 0.70, 0.34, 0.33));
        var spatialMask = Math.Max(Math.Max(ringMask, centerMarkMask), Math.Max(tailMask, splashMask * 0.78));
        if (nx > 0.76 && ny > 0.36 && ny < 0.64)
        {
            spatialMask *= 0.12;
        }

        var edge = Math.Min(Math.Min(nx, 1.0 - nx), Math.Min(ny, 1.0 - ny));
        var edgeFade = SmoothStep(0.025, 0.075, edge);

        var alpha = subject * spatialMask * edgeFade;
        alpha = SmoothStep(0.30, 0.78, alpha);

        if (alpha < 0.035)
        {
            return 0;
        }

        return (byte)Math.Round(255.0 * Clamp01(alpha));
    }

    private static double EllipseDistance(double x, double y, double centerX, double centerY, double radiusX, double radiusY)
    {
        var dx = (x - centerX) / radiusX;
        var dy = (y - centerY) / radiusY;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var t = Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3.0 - (2.0 * t));
    }

    private static double Clamp01(double value)
    {
        if (value < 0.0)
        {
            return 0.0;
        }

        return value > 1.0 ? 1.0 : value;
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[ClearPilotTransparentIcon]::Create((Resolve-Path -LiteralPath $SourcePng), $OutputPng)

Write-Host "Generated transparent icon PNG: $OutputPng"
