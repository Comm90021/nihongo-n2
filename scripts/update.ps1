# Cập nhật app: sao lưu tiến độ, lấy mã mới từ GitHub, đóng gói lại.
# Chạy: powershell -ExecutionPolicy Bypass -File scripts\update.ps1 [-WithData]
#   -WithData  dựng lại cả data\noi_dung.db (khi Importer có thay đổi)
param([switch]$WithData)
$ErrorActionPreference = 'Stop'
if (Test-Path 'D:\tools\git\cmd') { $env:Path = "D:\tools\git\cmd;$env:Path" }
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
if (Test-Path (Join-Path $root 'data\tien_do.db')) { & (Join-Path $PSScriptRoot 'backup.ps1') }
git -C $root pull --ff-only
if ($LASTEXITCODE -ne 0) { throw 'git pull không chạy được (có thay đổi chưa commit?). Không build.' }
if ($WithData) { & (Join-Path $root 'build.ps1') -Clean }
else { & (Join-Path $root 'build.ps1') -SkipData -Clean }
