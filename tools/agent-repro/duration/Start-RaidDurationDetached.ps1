# 以 WMI 新建独立进程启动时长批跑，远程会话断开不会终止批跑；进度写入 Logs/RaidDuration/_launch/<批次>.log。
# 参数与 Invoke-RaidDuration.ps1 相同（Seeds 用逗号分隔的字符串）。
param(
    [Parameter(Mandatory = $true)][string]$Label,
    [Parameter(Mandatory = $true)][string]$WorkspaceRoot,
    [string]$Seeds = '',
    [ValidateSet(1, 2, 4)][int]$Speed = 4,
    [int]$ObserveSeconds = 480,
    [string]$EditsPath = '',
    [string]$UpmCacheRoot = ''
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$batch = 'dur-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $Label
$stage = Join-Path $source 'Logs\RaidDuration\_launch'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$progress = Join-Path $stage ($batch + '.log')
$runner = Join-Path $PSScriptRoot 'Invoke-RaidDuration.ps1'
$arguments = '-Label "' + $Label + '" -Batch "' + $batch + '" -WorkspaceRoot "' + $WorkspaceRoot + '" -Speed ' + $Speed + ' -ObserveSeconds ' + $ObserveSeconds
if ($Seeds) { $arguments += ' -Seeds "' + (($Seeds.Split(',') | ForEach-Object { [int]$_.Trim() }) -join ',') + '"' }
if ($EditsPath) { $arguments += ' -EditsPath "' + $EditsPath + '"' }
if ($UpmCacheRoot) { $arguments += ' -UpmCacheRoot "' + $UpmCacheRoot + '"' }
$command = 'cmd.exe /c powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + $runner + '" ' + $arguments + ' > "' + $progress + '" 2>&1'
$result = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $command; CurrentDirectory = $source }
if ($result.ReturnValue -ne 0) { throw "Win32_Process.Create failed: $($result.ReturnValue)" }
'LAUNCHED ' + $batch + ' pid=' + $result.ProcessId + ' log=' + $progress
