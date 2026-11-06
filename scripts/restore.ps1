# Khôi phục tiến độ học từ bản sao lưu (.zip của backup.ps1, hoặc .db app tự sao lưu trong data\sao_luu).
# Chạy: powershell -ExecutionPolicy Bypass -File scripts\restore.ps1 <file>
# Phải đóng app trước. Bản đang dùng được giữ lại thành data\tien_do.truoc_khoi_phuc_<ngày giờ>.db.
param([Parameter(Mandatory)][string]$From)
$ErrorActionPreference = 'Stop'
if (Get-Process NihongoN2 -ErrorAction SilentlyContinue) { throw 'App đang mở. Đóng NihongoN2 rồi chạy lại.' }
$data = Join-Path $PSScriptRoot '..\data'
$db = Join-Path $data 'tien_do.db'
$stamp = Get-Date -Format yyyyMMdd_HHmmss

$tmp = Join-Path $env:TEMP "nihongo_restore_$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory $tmp | Out-Null
if ($From -like '*.zip') { Expand-Archive -LiteralPath $From -DestinationPath $tmp }
else { Copy-Item -LiteralPath $From (Join-Path $tmp 'tien_do.db') }
if (-not (Test-Path (Join-Path $tmp 'tien_do.db'))) { Remove-Item $tmp -Recurse -Force; throw 'Trong bản sao lưu không có tien_do.db' }

if (Test-Path $db) { Move-Item $db (Join-Path $data "tien_do.truoc_khoi_phuc_$stamp.db") }
foreach ($f in 'tien_do.db-wal', 'tien_do.db-shm') { $p = Join-Path $data $f; if (Test-Path $p) { Move-Item $p (Join-Path $data "tien_do.truoc_khoi_phuc_$stamp.db-$($f.Split('-')[1])") } }
Get-ChildItem $tmp | Copy-Item -Destination $data
Remove-Item $tmp -Recurse -Force
"Đã khôi phục từ $From. Bản cũ: data\tien_do.truoc_khoi_phuc_$stamp.db"
