# FairyGUI for Godot - visual comparison: compose step (called by run.ps1, or directly).
# For every name present in both _shots/unity and _shots/godot writes:
#   compare/<name>.png       Unity (left) | Godot (right), with a label bar
#   compare/<name>.diff.png  per-pixel difference, amplified
#   compare/index.md, index.html  pairs sorted by the share of clearly different pixels
param([string]$Shots = (Join-Path (Resolve-Path "$PSScriptRoot\..\..").Path '_shots'))
$ErrorActionPreference = 'Stop'

if (-not ('VisualCompare' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives, System.Runtime.InteropServices -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class VisualCompare
{
    static byte[] Pixels(Bitmap b, out int stride)
    {
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        stride = d.Stride;
        var buf = new byte[d.Stride * b.Height];
        Marshal.Copy(d.Scan0, buf, 0, buf.Length);
        b.UnlockBits(d);
        return buf;
    }

    // Returns { meanAbsDiff (0..255), share of pixels whose max channel diff > 40 (0..1) } and writes the diff image.
    public static double[] Compare(string a, string b, string composite, string diffPath, string label)
    {
        using (var ia = new Bitmap(a))
        using (var ib = new Bitmap(b))
        {
            int w = Math.Min(ia.Width, ib.Width), h = Math.Min(ia.Height, ib.Height);
            int sa, sb;
            byte[] pa = Pixels(ia, out sa), pb = Pixels(ib, out sb);
            double sum = 0; long changed = 0, n = (long)w * h;
            using (var diff = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                var dd = diff.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                var pd = new byte[dd.Stride * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int ka = y * sa + x * 4, kb = y * sb + x * 4, kd = y * dd.Stride + x * 4;
                        int m = 0;
                        for (int c = 0; c < 3; c++)
                        {
                            int v = Math.Abs(pa[ka + c] - pb[kb + c]);
                            sum += v;
                            if (v > m) m = v;
                        }
                        if (m > 40) changed++;
                        byte g = (byte)Math.Min(255, m * 3);
                        pd[kd] = 0; pd[kd + 1] = 0; pd[kd + 2] = g; pd[kd + 3] = 255;
                    }
                Marshal.Copy(pd, 0, dd.Scan0, pd.Length);
                diff.UnlockBits(dd);
                diff.Save(diffPath, ImageFormat.Png);
            }

            const int bar = 26, gap = 8;
            using (var comp = new Bitmap(ia.Width + gap + ib.Width, Math.Max(ia.Height, ib.Height) + bar))
            using (var g = Graphics.FromImage(comp))
            using (var font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.Clear(Color.FromArgb(40, 40, 40));
                g.DrawImage(ia, 0, bar, ia.Width, ia.Height);
                g.DrawImage(ib, ia.Width + gap, bar, ib.Width, ib.Height);
                g.DrawString("Unity  " + label + "  " + ia.Width + "x" + ia.Height, font, Brushes.White, 6, 6);
                g.DrawString("Godot  " + ib.Width + "x" + ib.Height, font, Brushes.White, ia.Width + gap + 6, 6);
                comp.Save(composite, ImageFormat.Png);
            }
            return new double[] { sum / (n * 3.0), changed / (double)n };
        }
    }
}
'@
}

$u = Join-Path $Shots 'unity'; $g = Join-Path $Shots 'godot'; $c = Join-Path $Shots 'compare'
New-Item -ItemType Directory -Force $c | Out-Null
$rows = foreach ($f in Get-ChildItem $u -Filter *.png | Sort-Object Name) {
    $other = Join-Path $g $f.Name
    if (-not (Test-Path $other)) { Write-Warning "no Godot shot for $($f.Name)"; continue }
    $base = [IO.Path]::GetFileNameWithoutExtension($f.Name)
    $r = [VisualCompare]::Compare($f.FullName, $other, (Join-Path $c "$base.png"), (Join-Path $c "$base.diff.png"), $base)
    [pscustomobject]@{ name = $base; changedPct = [Math]::Round($r[1] * 100, 1); meanDiff = [Math]::Round($r[0], 1) }
}
Get-ChildItem $g -Filter *.png | Where-Object { -not (Test-Path (Join-Path $u $_.Name)) } | ForEach-Object { Write-Warning "no Unity shot for $($_.Name)" }

$rows = $rows | Sort-Object changedPct -Descending
$md = @('| name | changed px % | mean diff |', '|---|---:|---:|') + ($rows | ForEach-Object { "| [$($_.name)]($($_.name).png) | $($_.changedPct) | $($_.meanDiff) |" })
Set-Content (Join-Path $c 'index.md') $md
$html = @('<html><body style="background:#222;color:#ddd;font-family:sans-serif">') + ($rows | ForEach-Object {
    "<h3>$($_.name) - changed $($_.changedPct)% - mean $($_.meanDiff)</h3><img src=""$($_.name).png"" style=""max-width:100%""><br><img src=""$($_.name).diff.png"" style=""max-width:50%"">" }) + '</body></html>'
Set-Content (Join-Path $c 'index.html') $html
$rows | Format-Table -AutoSize | Out-String -Width 120
