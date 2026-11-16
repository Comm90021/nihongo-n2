# Phóng một agent Codex cho một việc. Claude điều phối gọi script này (chạy nền).
#   -Task L1          mã việc; brief ở docs\codex\<Task>.md
#   -Model gpt-6.1-terra
#   -ReadOnly         chạy ngay trong repo chính, sandbox read-only (vẽ kế hoạch, duyệt)
#   -Worktree <mã>    chạy trong worktree của việc khác (dùng cho duyệt: -Task R-L1 -Worktree L1)
# Mặc định: tạo worktree D:\nihongo_wt\<Task> nhánh v3/<Task> từ HEAD của repo chính, sandbox workspace-write.
# Kết quả: câu trả lời cuối ở docs\codex\out\<Task>.md, nhật ký sự kiện ở docs\codex\log\<Task>.jsonl.
param(
    [Parameter(Mandatory)][string]$Task,
    [Parameter(Mandatory)][string]$Model,
    [switch]$ReadOnly,
    [string]$Worktree = '',
    [string]$Effort = 'medium'
)
$ErrorActionPreference = 'Stop'
$codex = 'C:\Program Files\WindowsApps\OpenAI.Codex_26.1111.2228.0_x64__2p2nqsd0c76g0\app\resources\codex.exe'
$repo = Split-Path -Parent $PSScriptRoot
$env:PATH = 'D:\tools\dotnet;D:\tools\git\cmd;' + $env:PATH
$env:DOTNET_ROOT = 'D:\tools\dotnet'
$env:NUGET_PACKAGES = 'D:\tools\nuget\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'

if ($Model -in @('gpt-6.1-sol')) { throw "Model $Model bị cấm (user không cho dùng model mạnh)." }
if ($Effort -ne 'medium') { throw 'User chỉ cho dùng mức medium.' }

$brief = Join-Path $repo "docs\codex\$Task.md"
if (-not (Test-Path -LiteralPath $brief)) { throw "Không thấy brief $brief" }
$outDir = Join-Path $repo 'docs\codex\out'
$logDir = Join-Path $repo 'docs\codex\log'
New-Item -ItemType Directory -Force $outDir, $logDir | Out-Null

$sandbox = 'workspace-write'
$extra = @()
if ($ReadOnly) {
    $dir = $repo
    $sandbox = 'read-only'
} else {
    $name = if ($Worktree) { $Worktree } else { $Task }
    $dir = "D:\nihongo_wt\$name"
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Force 'D:\nihongo_wt' | Out-Null
        git -C $repo worktree add $dir -b "v3/$name" | Out-Null
    }
    Copy-Item -LiteralPath (Join-Path $repo 'AGENTS.md') -Destination $dir -Force
    # Sandbox Codex không đọc được %APPDATA%\NuGet\NuGet.Config và không ghi được .git của repo chính:
    # restore sẵn ở đây (ngoài sandbox), Codex chỉ build/test --no-restore; commit do Claude làm sau khi duyệt.
    foreach ($p in 'tests\Nihongo.Tests', 'frontend\Nihongo.App') { dotnet restore (Join-Path $dir $p) -v q | Out-Null }
}

$cargs = @('exec', '-m', $Model, '-c', "model_reasoning_effort=`"$Effort`"", '-s', $sandbox, '-C', $dir,
          '--skip-git-repo-check', '--json', '-o', (Join-Path $outDir "$Task.md")) + $extra + @('-')
$start = Get-Date
Get-Content -LiteralPath $brief -Raw -Encoding UTF8 | & $codex @cargs | Out-File -LiteralPath (Join-Path $logDir "$Task.jsonl") -Encoding utf8
$code = $LASTEXITCODE
"[$Task] model=$Model sandbox=$sandbox dir=$dir exit=$code thoi_gian=$([int]((Get-Date)-$start).TotalMinutes) phut"
$out = Join-Path $outDir "$Task.md"
if (Test-Path -LiteralPath $out) { Get-Content -LiteralPath $out -Raw -Encoding UTF8 }
