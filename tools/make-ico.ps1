<#
.SYNOPSIS
    Sinh app.ico tu anh nhan vat PNG nen trong suot.

.DESCRIPTION
    Anh goc la nhan vat full-body trong khung vuong nhieu khoang trong, thu nho xuong
    16x16 se khong nhan ra duoc. Script tu do bounding box theo kenh alpha, lay dai
    26% tren cung (vung dau + vai), vuong hoa roi xuat ICO nhieu kich thuoc
    (16/20/24/32/40/48/64/128/256) voi tung frame ma hoa PNG.

.EXAMPLE
    .\tools\make-ico.ps1
    .\tools\make-ico.ps1 -Source .\anh-moi.png
#>
param(
    [string]$Source,
    [string]$OutIco,
    [string]$OutPreview
)

$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $Source)      { $Source      = Join-Path $repoRoot 'icon-transparent.png' }
if (-not $OutIco)      { $OutIco      = Join-Path $repoRoot 'app.ico' }
if (-not $OutPreview)  { $OutPreview  = Join-Path $env:TEMP 'app-icon-preview.png' }

Add-Type -AssemblyName System.Drawing

$code = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class IconMaker
{
    static byte[] GetBuffer(Bitmap bmp, out int stride)
    {
        BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        stride = data.Stride;
        byte[] buf = new byte[stride * bmp.Height];
        Marshal.Copy(data.Scan0, buf, 0, buf.Length);
        bmp.UnlockBits(data);
        return buf;
    }

    public static int[] AlphaBounds(Bitmap bmp, int threshold, int top, int bottom)
    {
        int stride;
        byte[] buf = GetBuffer(bmp, out stride);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = top; y < bottom; y++)
        {
            int row = y * stride;
            for (int x = 0; x < bmp.Width; x++)
            {
                if (buf[row + x * 4 + 3] > threshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX < 0) return new int[] { 0, 0, bmp.Width, bmp.Height };
        return new int[] { minX, minY, maxX + 1, maxY + 1 };
    }

    public static void WriteIco(Bitmap square, int[] sizes, string path)
    {
        List<byte[]> blobs = new List<byte[]>();
        foreach (int s in sizes)
        {
            using (Bitmap resized = new Bitmap(s, s, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(resized))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.DrawImage(square, new Rectangle(0, 0, s, s));
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    resized.Save(ms, ImageFormat.Png);
                    blobs.Add(ms.ToArray());
                }
            }
        }

        using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)sizes.Length);

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte dim = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
                w.Write(dim);
                w.Write(dim);
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((ushort)1);
                w.Write((ushort)32);
                w.Write((uint)blobs[i].Length);
                w.Write((uint)offset);
                offset += blobs[i].Length;
            }
            foreach (byte[] b in blobs) w.Write(b);
        }
    }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing

$src = New-Object System.Drawing.Bitmap($Source)
Write-Host "Source: $($src.Width)x$($src.Height)"

# 1. Bounding box toan bo nhan vat
$full = [IconMaker]::AlphaBounds($src, 30, 0, $src.Height)
Write-Host "Full bbox: L=$($full[0]) T=$($full[1]) R=$($full[2]) B=$($full[3])"

$fullH = $full[3] - $full[1]

# 2. Bounding box cua dai 26% tren cung (vung dau + vai)
$bandBottom = [int]($full[1] + $fullH * 0.26)
$head = [IconMaker]::AlphaBounds($src, 30, $full[1], $bandBottom)
Write-Host "Head bbox: L=$($head[0]) T=$($head[1]) R=$($head[2]) B=$($head[3])"

$hw = $head[2] - $head[0]
$hh = $head[3] - $head[1]
$cx = ($head[0] + $head[2]) / 2.0
$cy = ($head[1] + $head[3]) / 2.0

# 3. Vuong hoa + padding 12%
$side = [Math]::Max($hw, $hh) * 1.12
$left = [int]($cx - $side / 2.0)
$top  = [int]($cy - $side / 2.0)
$side = [int]$side
Write-Host "Crop square: X=$left Y=$top Side=$side"

$square = New-Object System.Drawing.Bitmap($side, $side, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($square)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$nl = 0 - $left
$nt = 0 - $top
$destRect = New-Object System.Drawing.Rectangle $nl, $nt, $src.Width, $src.Height
$g.DrawImage($src, $destRect)
$g.Dispose()

$square.Save($OutPreview, [System.Drawing.Imaging.ImageFormat]::Png)

$sizes = [int[]]@(16, 20, 24, 32, 40, 48, 64, 128, 256)
[IconMaker]::WriteIco($square, $sizes, $OutIco)

$square.Dispose()
$src.Dispose()
Write-Host "DONE -> $OutIco"
