# Dessine src\app.ico (empreinte blanche sur un carre arrondi) en plusieurs tailles.
# A lancer avec Windows PowerShell (mode STA par defaut).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase
$path = 'M12 11 V14 C12 16.5 11.5 18.7 10.6 20.6 M8.2 19.2 C8.9 17.5 9.2 15.7 9.2 14 V11 A2.8 2.8 0 0 1 14.8 11 V13.5 C14.8 16.1 14.4 18.5 13.6 20.7 M5.4 16.6 C5.8 15.6 6 14.5 6 13.4 V11 A6 6 0 0 1 18 11 V13 C18 14.9 17.8 16.7 17.4 18.5 M3.4 8.2 A9.2 9.2 0 0 1 17.2 3.4 M20.4 7.4 C20.9 8.5 21.2 9.7 21.2 11 V13.5'
function New-Color($h) { [Windows.Media.ColorConverter]::ConvertFromString($h) }

$pngs = foreach ($n in 16, 24, 32, 48, 64, 128, 256) {
    $v = New-Object Windows.Media.DrawingVisual
    $d = $v.RenderOpen()
    $m = $n * 0.06; $s = $n - 2 * $m; $r = $s * 0.235
    $rect = New-Object Windows.Rect $m, $m, $s, $s
    $bg = New-Object Windows.Media.LinearGradientBrush (New-Color '#FF8A7CFF'), (New-Color '#FF3D5AFE'), 90
    $rim = New-Object Windows.Media.LinearGradientBrush (New-Color '#AAFFFFFF'), (New-Color '#10FFFFFF'), 75
    $d.DrawRoundedRectangle($bg, $null, $rect, $r, $r)
    $w = [Math]::Max(0.6, $n * 0.012)
    $in = New-Object Windows.Rect ($m + $w / 2), ($m + $w / 2), ($s - $w), ($s - $w)
    $d.DrawRoundedRectangle($null, (New-Object Windows.Media.Pen $rim, $w), $in, $r, $r)
    $k = $s * 0.68 / 24; $o = ($n - 24 * $k) / 2
    $t = New-Object Windows.Media.TransformGroup
    $t.Children.Add((New-Object Windows.Media.ScaleTransform $k, $k))
    $t.Children.Add((New-Object Windows.Media.TranslateTransform $o, $o))
    $d.PushTransform($t)
    $width = 1.7; if ($n -le 24) { $width = 2 }
    $pen = New-Object Windows.Media.Pen ([Windows.Media.Brushes]::White), $width
    $pen.StartLineCap = $pen.EndLineCap = 'Round'
    $d.DrawGeometry($null, $pen, [Windows.Media.Geometry]::Parse($path))
    $d.Pop(); $d.Close()
    $b = New-Object Windows.Media.Imaging.RenderTargetBitmap $n, $n, 96, 96, ([Windows.Media.PixelFormats]::Pbgra32)
    $b.Render($v)
    $e = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $e.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($b))
    $ms = New-Object IO.MemoryStream
    $e.Save($ms)
    [pscustomobject]@{ Size = $n; Data = $ms.ToArray() }
}

$f = [IO.File]::Create((Join-Path $PSScriptRoot 'src\app.ico'))
$bw = New-Object IO.BinaryWriter $f
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($p in $pngs) {
    $dim = [byte]($p.Size % 256)
    $bw.Write($dim); $bw.Write($dim); $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$p.Data.Length); $bw.Write([uint32]$offset)
    $offset += $p.Data.Length
}
foreach ($p in $pngs) { $bw.Write([byte[]]$p.Data) }
$bw.Close()
