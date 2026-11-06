# Dựng lại dữ liệu và đóng gói app. Chạy: powershell -ExecutionPolicy Bypass -File build.ps1 [-SkipData] [-SkipApp]
param([switch]$SkipData, [switch]$SkipApp)
$ErrorActionPreference = 'Stop'
$env:Path = "D:\tools\dotnet;D:\tools\git\cmd;$env:Path"
$env:DOTNET_ROOT = 'D:\tools\dotnet'
$env:NUGET_PACKAGES = 'D:\tools\nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = 'D:\tools\nuget\http-cache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Set-Location $PSScriptRoot

if (-not $SkipData) {
  dotnet build src\Nihongo.Importer -c Release -v q
  dotnet src\Nihongo.Importer\bin\Release\net10.0\Nihongo.Importer.dll (Resolve-Path ..).Path
}
if (-not $SkipApp) {
  # Không nén và có biên dịch sẵn: file to hơn (~76MB) nhưng mở nhanh gấp 3 so với bản nén.
  dotnet publish src\Nihongo.Desktop -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true `
    -p:PublishTrimmed=true -p:TrimMode=partial -p:DebugType=none -o dist -v q
  "App: $((Resolve-Path dist\NihongoN2.exe).Path)"
}
