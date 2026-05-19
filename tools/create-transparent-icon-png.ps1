param(
    [string]$SourcePng = (Join-Path $PSScriptRoot "..\assets\icon\clearpilot-icon-source.png"),
    [string]$OutputPng = (Join-Path $PSScriptRoot "..\assets\icon\clearpilot-icon.png"),
    [int]$OutputSize = 1024
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

$resolvedSource = (Resolve-Path -LiteralPath $SourcePng).Path

$code = @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

public static class ClearPilotTransparentIcon
{
    private sealed class Bounds
    {
        public int MinX = int.MaxValue;
        public int MinY = int.MaxValue;
        public int MaxX = int.MinValue;
        public int MaxY = int.MinValue;

        public bool IsValid
        {
            get { return MinX <= MaxX && MinY <= MaxY; }
        }

        public void Include(int x, int y)
        {
            if (x < MinX) MinX = x;
            if (x > MaxX) MaxX = x;
            if (y < MinY) MinY = y;
            if (y > MaxY) MaxY = y;
        }
    }

    public static void Create(string sourcePath, string outputPath, int outputSize)
    {
        using (var loaded = Image.FromFile(sourcePath))
        using (var source = new Bitmap(loaded.Width, loaded.Height, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(source))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(loaded, 0, 0, loaded.Width, loaded.Height);
            }

            int width = source.Width;
            int height = source.Height;
            var bgA = source.GetPixel(0, 0);
            var bgB = source.GetPixel(Math.Max(0, width - 1), 0);
            var bgC = source.GetPixel(0, Math.Max(0, height - 1));
            var bgD = source.GetPixel(Math.Max(0, width - 1), Math.Max(0, height - 1));

            using (var masked = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                bool[,] isBackground = DetectBackground(source, bgA, bgB, bgC, bgD);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color c = source.GetPixel(x, y);
                        if (isBackground[x, y])
                        {
                            masked.SetPixel(x, y, Color.FromArgb(0, c.R, c.G, c.B));
                            continue;
                        }
                        masked.SetPixel(x, y, Color.FromArgb(255, c.R, c.G, c.B));
                    }
                }

                Bounds b = FindOpaqueBounds(masked, 6);
                if (!b.IsValid)
                {
                    b = new Bounds { MinX = 0, MinY = 0, MaxX = width - 1, MaxY = height - 1 };
                }

                int cropW = Math.Max(1, b.MaxX - b.MinX + 1);
                int cropH = Math.Max(1, b.MaxY - b.MinY + 1);
                int maxDim = Math.Max(cropW, cropH);
                int pad = (int)Math.Ceiling(maxDim * 0.10);
                int side = maxDim + (pad * 2);

                using (var padded = new Bitmap(side, side, PixelFormat.Format32bppArgb))
                {
                    using (var pg = Graphics.FromImage(padded))
                    {
                        pg.Clear(Color.Transparent);
                        pg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        pg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        pg.SmoothingMode = SmoothingMode.HighQuality;
                        pg.CompositingQuality = CompositingQuality.HighQuality;

                        var srcRect = new Rectangle(b.MinX, b.MinY, cropW, cropH);
                        int dstX = (side - cropW) / 2;
                        int dstY = (side - cropH) / 2;
                        var dstRect = new Rectangle(dstX, dstY, cropW, cropH);
                        pg.DrawImage(masked, dstRect, srcRect, GraphicsUnit.Pixel);
                    }

                    using (var output = new Bitmap(outputSize, outputSize, PixelFormat.Format32bppArgb))
                    using (var og = Graphics.FromImage(output))
                    {
                        og.Clear(Color.Transparent);
                        og.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        og.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        og.SmoothingMode = SmoothingMode.HighQuality;
                        og.CompositingQuality = CompositingQuality.HighQuality;
                        og.DrawImage(padded, new Rectangle(0, 0, outputSize, outputSize));
                        output.Save(outputPath, ImageFormat.Png);
                    }
                }
            }
        }
    }

    private static Bounds FindOpaqueBounds(Bitmap bitmap, int alphaThreshold)
    {
        var bounds = new Bounds();
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A >= alphaThreshold)
                {
                    bounds.Include(x, y);
                }
            }
        }
        return bounds;
    }

    private static bool[,] DetectBackground(Bitmap bitmap, Color bgA, Color bgB, Color bgC, Color bgD)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        var bg = new bool[width, height];
        var visited = new bool[width, height];
        var queue = new Queue<Point>(width + height);

        for (int x = 0; x < width; x++)
        {
            TryEnqueue(bitmap, visited, queue, x, 0, bgA, bgB, bgC, bgD);
            TryEnqueue(bitmap, visited, queue, x, height - 1, bgA, bgB, bgC, bgD);
        }
        for (int y = 0; y < height; y++)
        {
            TryEnqueue(bitmap, visited, queue, 0, y, bgA, bgB, bgC, bgD);
            TryEnqueue(bitmap, visited, queue, width - 1, y, bgA, bgB, bgC, bgD);
        }

        while (queue.Count > 0)
        {
            Point p = queue.Dequeue();
            if (bg[p.X, p.Y])
            {
                continue;
            }

            bg[p.X, p.Y] = true;

            TryEnqueue(bitmap, visited, queue, p.X - 1, p.Y, bgA, bgB, bgC, bgD);
            TryEnqueue(bitmap, visited, queue, p.X + 1, p.Y, bgA, bgB, bgC, bgD);
            TryEnqueue(bitmap, visited, queue, p.X, p.Y - 1, bgA, bgB, bgC, bgD);
            TryEnqueue(bitmap, visited, queue, p.X, p.Y + 1, bgA, bgB, bgC, bgD);
        }

        return bg;
    }

    private static void TryEnqueue(Bitmap bitmap, bool[,] visited, Queue<Point> queue, int x, int y, Color bgA, Color bgB, Color bgC, Color bgD)
    {
        if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
        {
            return;
        }

        if (visited[x, y])
        {
            return;
        }

        Color c = bitmap.GetPixel(x, y);
        if (!IsLikelyBackground(c, bgA, bgB, bgC, bgD))
        {
            return;
        }

        visited[x, y] = true;
        queue.Enqueue(new Point(x, y));
    }

    private static bool IsLikelyBackground(Color c, Color bgA, Color bgB, Color bgC, Color bgD)
    {
        int sat = Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
        if (sat > 26)
        {
            return false;
        }

        double luma = (0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B);
        if (luma < 132.0)
        {
            return false;
        }

        return ColorDistance(c, bgA) <= 42.0
            || ColorDistance(c, bgB) <= 42.0
            || ColorDistance(c, bgC) <= 42.0
            || ColorDistance(c, bgD) <= 42.0;
    }

    private static double ColorDistance(Color a, Color b)
    {
        int dr = a.R - b.R;
        int dg = a.G - b.G;
        int db = a.B - b.B;
        return Math.Sqrt((dr * dr) + (dg * dg) + (db * db));
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[ClearPilotTransparentIcon]::Create($resolvedSource, $OutputPng, $OutputSize)

Write-Host "Generated transparent icon PNG: $OutputPng"
