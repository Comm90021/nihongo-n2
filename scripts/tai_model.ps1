# Tải model cho "Phụ đề máy" (Services/LiveCaption.cs) vào data\models (khoảng 230MB sau khi giải nén).
#   - SenseVoice int8 bản 2024-07-17 (sherpa-onnx). KHÔNG dùng bản 2025-09-09: thử 08/11/2026 ra toàn chữ Hán kiểu Trung, mất kana.
#   - Silero VAD.
# Chạy: powershell -ExecutionPolicy Bypass -File scripts\tai_model.ps1
$ErrorActionPreference = 'Stop'
$dir = Join-Path $PSScriptRoot '..\data\models'
New-Item -ItemType Directory -Force $dir | Out-Null
$base = 'https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models'

$vad = Join-Path $dir 'silero_vad.onnx'
if (-not (Test-Path $vad)) { Invoke-WebRequest -UseBasicParsing "$base/silero_vad.onnx" -OutFile $vad }

$sv = Join-Path $dir 'sensevoice'
if (-not (Test-Path (Join-Path $sv 'model.int8.onnx'))) {
  $name = 'sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17'
  $tmp = Join-Path $dir "$name.tar.bz2"
  Invoke-WebRequest -UseBasicParsing "$base/$name.tar.bz2" -OutFile $tmp   # ~1GB vì có cả bản fp32
  Push-Location $dir
  tar -xjf "$name.tar.bz2" --exclude "*/model.onnx" --exclude "*/test_wavs/*"
  Pop-Location
  Remove-Item $tmp
  if (Test-Path $sv) { Remove-Item $sv -Recurse -Force }
  Rename-Item (Join-Path $dir $name) 'sensevoice'
}
Get-ChildItem $dir -Recurse -File | ForEach-Object { '{0,-40} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
