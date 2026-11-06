# Dựng lại dữ liệu và đóng gói app.
# Chạy: powershell -ExecutionPolicy Bypass -File build.ps1 [-SkipData] [-SkipApp] [-Clean]
#   -SkipData  bỏ qua bước dựng data\noi_dung.db
#   -SkipApp   bỏ qua bước đóng gói dist\NihongoN2.exe
#   -Clean     xong thì xóa bin\ obj\ (khoảng 600MB, lần build sau tự tạo lại)
param([switch]$SkipData, [switch]$SkipApp, [switch]$Clean)
$ErrorActionPreference = 'Stop'
# Máy tác giả để công cụ ở D:\tools (ổ C ít chỗ). Máy khác dùng dotnet đã cài sẵn.
if (Test-Path 'D:\tools\dotnet\dotnet.exe') {
  $env:Path = "D:\tools\dotnet;D:\tools\git\cmd;$env:Path"
  $env:DOTNET_ROOT = 'D:\tools\dotnet'
  $env:NUGET_PACKAGES = 'D:\tools\nuget\packages'
  $env:NUGET_HTTP_CACHE_PATH = 'D:\tools\nuget\http-cache'
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Set-Location $PSScriptRoot

if (-not $SkipData) {
  dotnet build src\Nihongo.Importer -c Release -v q
  # Importer đọc tài liệu ở thư mục cha của repo. Không bao giờ đụng tới data\tien_do.db.
  dotnet src\Nihongo.Importer\bin\Release\net10.0\Nihongo.Importer.dll (Resolve-Path ..).Path
}

if (-not $SkipApp) {
  if (Get-Process NihongoN2 -ErrorAction SilentlyContinue) { throw 'App đang mở. Đóng NihongoN2 rồi build lại.' }
  # Không nén và có biên dịch sẵn: file to hơn (~80MB) nhưng mở nhanh gấp 3 so với bản nén.
  dotnet publish src\Nihongo.Desktop -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true `
    -p:PublishTrimmed=true -p:TrimMode=partial -p:DebugType=none -o dist -v q
  "App: $((Resolve-Path dist\NihongoN2.exe).Path)"
}

if ($Clean) {
  Get-ChildItem src -Recurse -Directory | Where-Object { $_.Name -in 'bin', 'obj' } | Remove-Item -Recurse -Force
  'Đã xóa bin\ và obj\.'
}
