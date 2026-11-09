# Vẽ icon app: chữ 日 (Yu Mincho đậm) trắng trên ô vuông bo góc màu son (đỏ cờ Nhật), xuất .ico nhiều cỡ.
# Chạy lại khi muốn đổi icon: powershell -ExecutionPolicy Bypass -File scripts\tao_icon.ps1
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\Nihongo.Desktop\Assets\app.ico'
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
  $bmp = New-Object Drawing.Bitmap $s, $s
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.InterpolationMode = 'HighQualityBicubic'
  $r = [Math]::Max(2, $s * 0.22); $d = $r * 2; $w = $s - 1
  $path = New-Object Drawing.Drawing2D.GraphicsPath
  $path.AddArc(0, 0, $d, $d, 180, 90); $path.AddArc($w - $d, 0, $d, $d, 270, 90)
  $path.AddArc($w - $d, $w - $d, $d, $d, 0, 90); $path.AddArc(0, $w - $d, $d, $d, 90, 90); $path.CloseFigure()
  $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 188, 0, 45))), $path)
  # Cỡ nhỏ dùng Yu Gothic (nét đều, đọc rõ); cỡ lớn dùng Yu Mincho cho đẹp.
  $fontName = if ($s -ge 48) { 'Yu Mincho' } else { 'Yu Gothic UI' }
  $font = New-Object Drawing.Font($fontName, [single]($s * 0.62), [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
  $fmt = New-Object Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
  $g.DrawString('日', $font, [Drawing.Brushes]::White, (New-Object Drawing.RectangleF(0, [single]($s * 0.03), $s, $s)), $fmt)
  $g.Dispose()
  $ms = New-Object IO.MemoryStream; $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  , $ms.ToArray()
}
# Ghép ICO: header 6 byte, mỗi ảnh 16 byte mục lục, rồi dữ liệu PNG.
$fs = [IO.File]::Create((Resolve-Path (Split-Path $out)).Path + '\app.ico')
$bw = New-Object IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
  $bw.Write([byte]$b); $bw.Write([byte]$b); $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
  $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
"Đã tạo $out"
