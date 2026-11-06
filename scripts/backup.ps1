# Sao lưu tiến độ học (data\tien_do.db) thành file zip có ngày giờ.
# Chạy: powershell -ExecutionPolicy Bypass -File scripts\backup.ps1 [-To <thư mục>]
# Mặc định lưu vào data\sao_luu. Nên chọn ổ khác hoặc thư mục OneDrive/Google Drive.
param([string]$To = (Join-Path $PSScriptRoot '..\data\sao_luu'))
$ErrorActionPreference = 'Stop'
$data = Join-Path $PSScriptRoot '..\data'
$db = Join-Path $data 'tien_do.db'
if (-not (Test-Path $db)) { throw "Không thấy $db" }
New-Item -ItemType Directory -Force $To | Out-Null
# Chép cả -wal và -shm: khi app đang mở, phần ghi mới nhất có thể còn nằm trong file -wal.
$tmp = Join-Path $env:TEMP "nihongo_backup_$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory $tmp | Out-Null
foreach ($f in 'tien_do.db', 'tien_do.db-wal', 'tien_do.db-shm') {
  $p = Join-Path $data $f
  if (Test-Path $p) { Copy-Item $p $tmp }
}
$zip = Join-Path $To "tien_do_$(Get-Date -Format yyyyMMdd_HHmmss).zip"
Compress-Archive -Path (Join-Path $tmp '*') -DestinationPath $zip
Remove-Item $tmp -Recurse -Force
"Đã sao lưu: $((Resolve-Path $zip).Path)"
