# Nén file nghe (MP3/M4A/WAV) thành AAC mono .m4a bằng bộ chuyển mã có sẵn của Windows (không cần ffmpeg).
# Đo 08/11/2026: file nghe 68,4MB → 11,6MB (17%), chất lượng giọng nói người dùng chấp nhận được.
#
# An toàn: chỉ xóa bản gốc khi file .m4a đã tạo xong VÀ thời lượng khớp (lệch < 2% hoặc < 1 giây).
# Mỗi file xóa được ghi vào XOA_LOG.md ở thư mục dự án. File lỗi thì giữ nguyên bản gốc.
#
# Chạy:
#   powershell -ExecutionPolicy Bypass -File scripts\nen_audio.ps1 -Folder "<thư mục>"          (nén mọi file trong thư mục, đệ quy)
#   powershell -ExecutionPolicy Bypass -File scripts\nen_audio.ps1 -Rar "<file.rar>" -Dest "<thư mục đích>"   (giải nén rar rồi nén)
#   Thêm -KeepOriginal để không xóa bản gốc.
param([string]$Folder, [string]$Rar, [string]$Dest, [switch]$KeepOriginal,
      [string]$Log = (Join-Path $PSScriptRoot '..\..\XOA_LOG.md'))
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Media.Transcoding.MediaTranscoder, Windows.Media.Transcoding, ContentType = WindowsRuntime]
$null = [Windows.Media.MediaProperties.MediaEncodingProfile, Windows.Media.MediaProperties, ContentType = WindowsRuntime]
$ext = [System.WindowsRuntimeSystemExtensions].GetMethods()
$asOp = $ext | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
$asProg = $ext | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncActionWithProgress`1' } | Select-Object -First 1
function Await($op, [Type]$t) { $task = $asOp.MakeGenericMethod($t).Invoke($null, @($op)); $task.Wait(); $task.Result }

$shell = New-Object -ComObject Shell.Application
function Duration([string]$path) {
  # Cột 27 của Explorer là thời lượng media (hh:mm:ss).
  $dir = $shell.Namespace((Split-Path $path)); $item = $dir.ParseName((Split-Path $path -Leaf))
  $s = $dir.GetDetailsOf($item, 27)
  if ($s -match '(\d+):(\d+):(\d+)') { return [int]$Matches[1] * 3600 + [int]$Matches[2] * 60 + [int]$Matches[3] }
  return -1
}

function Transcode([string]$in, [string]$out) {
  $src = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($in)) ([Windows.Storage.StorageFile])
  New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
  $folder = Await ([Windows.Storage.StorageFolder]::GetFolderFromPathAsync((Split-Path $out))) ([Windows.Storage.StorageFolder])
  $dst = Await ($folder.CreateFileAsync((Split-Path $out -Leaf), [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])
  $profile = [Windows.Media.MediaProperties.MediaEncodingProfile]::CreateM4a([Windows.Media.MediaProperties.AudioEncodingQuality]::Low)
  # Đúng cấu hình bản thử người dùng đã nghe và chấp nhận (08/11): mono, 24kHz, 32kbps.
  # Thiếu SampleRate/Bitrate thì Windows dùng mặc định ~96kbps, to hơn nhiều file gốc (đã dính một lần).
  $profile.Audio.ChannelCount = 1
  $profile.Audio.SampleRate = 24000
  $profile.Audio.Bitrate = 32000
  $tc = New-Object Windows.Media.Transcoding.MediaTranscoder
  $prep = Await ($tc.PrepareFileTranscodeAsync($src, $dst, $profile)) ([Windows.Media.Transcoding.PrepareTranscodeResult])
  if (-not $prep.CanTranscode) { throw "không chuyển mã được: $($prep.FailureReason)" }
  $t = $asProg.MakeGenericMethod([double]).Invoke($null, @($prep.TranscodeAsync())); $t.Wait()
}

$stats = @{ ok = 0; fail = 0; skip = 0; before = 0.0; after = 0.0 }
function Process-One([string]$in, [string]$out, [string]$label) {
  # Bản gốc còn nghĩa là chưa xong (file gốc chỉ bị xóa sau khi nén và kiểm thành công):
  # .m4a đang có có thể là bản dở do lần chạy trước bị ngắt, nên nén lại đè lên.
  $tmp = if ($in -eq $out) { [IO.Path]::ChangeExtension($out, '.tmp.m4a') } else { $out }
  try {
    Transcode $in $tmp
    $d1 = Duration $in; $d2 = Duration $tmp
    $okDur = $d1 -gt 0 -and $d2 -gt 0 -and ([Math]::Abs($d1 - $d2) -le [Math]::Max(1, $d1 * 0.02))
    if (-not $okDur) { throw "thời lượng lệch ($d1 s → $d2 s)" }
    $mb1 = (Get-Item -LiteralPath $in).Length / 1MB; $mb2 = (Get-Item -LiteralPath $tmp).Length / 1MB
    # Bản nén không nhỏ hơn rõ rệt thì vô ích (gốc đã nhẹ sẵn): bỏ bản nén, giữ gốc.
    if ($mb2 -gt $mb1 * 0.8) { Remove-Item -LiteralPath $tmp -Force; $stats.skip++; "BỎ  {0:N1}→{1:N1}MB không nhỏ hơn đủ, giữ gốc: {2}" -f $mb1, $mb2, $label; return }
    $stats.before += $mb1; $stats.after += $mb2; $stats.ok++
    if (-not $KeepOriginal) {
      # Bộ chuyển mã và Shell (đọc thời lượng) có thể còn giữ file gốc một lúc: giải phóng rồi thử xóa lại.
      # Lần chạy 08/11 17:51 vì lỗi này mà bản nén tốt bị xóa theo (file gốc vẫn còn).
      $deleted = $false
      for ($i = 0; $i -lt 15 -and -not $deleted; $i++) {
        [GC]::Collect(); [GC]::WaitForPendingFinalizers()
        try { Remove-Item -LiteralPath $in -Force -ErrorAction Stop; $deleted = $true } catch { Start-Sleep -Seconds 2 }
      }
      if (-not $deleted) {
        # Giữ cả hai: bản nén đã kiểm thì không bỏ. Lần chạy sau thấy .m4a đủ nhỏ sẽ không nén lại; xóa gốc bằng tay.
        if ($tmp -ne $out) { Move-Item -LiteralPath $tmp $out -Force }
        "GIỮ {0}: nén xong nhưng file gốc đang bị khóa, chưa xóa gốc" -f $label
        return
      }
      if ($tmp -ne $out) { Move-Item -LiteralPath $tmp $out }
      Add-Content -LiteralPath $Log -Encoding UTF8 -Value ("| nén audio | {0} → .m4a | {1:N1} → {2:N1} |" -f $label, $mb1, $mb2)
    }
    "OK   {0:N1}→{1:N1}MB  {2}" -f $mb1, $mb2, $label
  }
  catch {
    $stats.fail++
    if ((Test-Path -LiteralPath $tmp) -and $tmp -ne $in) { Remove-Item -LiteralPath $tmp -Force }
    "LỖI {0}: {1} (giữ bản gốc)" -f $label, $_.Exception.Message
  }
}

if (-not (Test-Path -LiteralPath $Log)) { Set-Content -LiteralPath $Log -Encoding UTF8 -Value "# Nhật ký xóa" }
Add-Content -LiteralPath $Log -Encoding UTF8 -Value ("`n## Nén audio {0:dd/MM/yyyy HH:mm}`n`n| Nhóm | File | MB trước → sau |`n|---|---|---|" -f (Get-Date))

if ($Folder) {
  # .m4a cũng nén lại nếu bitrate còn cao (> 48kbps); bản đã nén 32kbps thì bỏ qua.
  $files = Get-ChildItem -LiteralPath $Folder -Recurse -File | Where-Object { $_.Extension -in '.mp3', '.wav' -or ($_.Extension -eq '.m4a' -and ($sec = Duration $_.FullName) -gt 0 -and $_.Length * 8 / 1000 / $sec -gt 48) }
  foreach ($f in $files) {
    $out = if ($f.Extension -eq '.m4a') { $f.FullName } else { [IO.Path]::ChangeExtension($f.FullName, '.m4a') }
    Process-One $f.FullName $out $f.FullName.Substring($Folder.Length).TrimStart('\')
  }
}

if ($Rar) {
  # Đường dẫn chữ Trung/Việt làm tar.exe lỗi: chép sang thư mục tạm không dấu rồi giải nén.
  $tmpDir = Join-Path ([IO.Path]::GetPathRoot($Dest)) "_nen_tmp_$([guid]::NewGuid().ToString('N').Substring(0,8))"
  New-Item -ItemType Directory $tmpDir | Out-Null
  try {
    Copy-Item -LiteralPath $Rar (Join-Path $tmpDir 'a.rar')
    Push-Location $tmpDir; tar -xf a.rar; Pop-Location
    Remove-Item (Join-Path $tmpDir 'a.rar')
    $audio = Get-ChildItem $tmpDir -Recurse -File | Where-Object Extension -in '.mp3', '.wav', '.m4a'
    foreach ($f in $audio) {
      $rel = $f.FullName.Substring($tmpDir.Length).TrimStart('\')
      Process-One $f.FullName (Join-Path $Dest ([IO.Path]::ChangeExtension($rel, '.m4a'))) ("{0} :: {1}" -f (Split-Path $Rar -Leaf), $rel)
    }
    $made = @(Get-ChildItem -LiteralPath $Dest -Recurse -Filter *.m4a).Count
    if (-not $KeepOriginal -and $stats.fail -eq 0 -and $made -ge $audio.Count -and $audio.Count -gt 0) {
      $mb = (Get-Item -LiteralPath $Rar).Length / 1MB
      Remove-Item -LiteralPath $Rar -Force
      Add-Content -LiteralPath $Log -Encoding UTF8 -Value ("| nén audio | {0} (đã giải nén và nén đủ {1} file vào {2}) | {3:N1} → 0 |" -f $Rar, $audio.Count, $Dest, $mb)
      "Đã xóa file rar gốc ($($audio.Count) file đã nén đủ)."
    } else { "Giữ file rar gốc (lỗi $($stats.fail), đã nén $made/$($audio.Count))." }
  }
  finally { Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue }
}

"Xong: {0} file nén, {1} lỗi, {2} bỏ qua (đã có). {3:N0} MB → {4:N0} MB" -f $stats.ok, $stats.fail, $stats.skip, $stats.before, $stats.after
