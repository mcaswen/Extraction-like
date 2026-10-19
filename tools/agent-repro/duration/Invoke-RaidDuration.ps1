# 正式场景自主对局时长批跑：在隔离副本中按种子依次运行 SceneRaid Autonomous，证据写入 <源仓库>/Logs/RaidDuration/<批次>/。
# 源仓库只读；实验补丁和 -EditsPath 的改动只写入隔离副本。有效时长 = 游戏秒数 + 7 秒 × 打开背包次数（背包打开时游戏暂停）。
param(
    [Parameter(Mandatory = $true)][string]$Label,
    [Parameter(Mandatory = $true)][string]$WorkspaceRoot,
    # 逗号分隔；默认是 2026-10-18 调参基线的 16 个种子（powershell -File 传参时数组会变成单个字符串）
    [string]$Seeds = '5731,6731,7731,8731,9731,10731,11731,12731,13731,14731,15731,16731,17731,18731,19731,20731',
    [ValidateSet(1, 2, 4)][int]$Speed = 4,
    [int]$ObserveSeconds = 480,
    [string]$EditsPath = '',
    [string]$UpmCacheRoot = '',
    [string]$Batch = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# SSH 会话位于 Windows session 0，无法穿过用户 %LOCALAPPDATA%\Unity\cache 的 junction，需要直接指定其目标目录。
if ($UpmCacheRoot) { $env:UPM_CACHE_ROOT = $UpmCacheRoot }
$source = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$seedList = @($Seeds.Split(',') | Where-Object { $_.Trim() } | ForEach-Object { [int]$_.Trim() })
if (!$seedList.Count) { throw 'No seeds.' }
if (!$Batch) { $Batch = 'dur-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $Label }
$batchOutput = Join-Path $source ('Logs\RaidDuration\' + $Batch)
if (Test-Path -LiteralPath $batchOutput) { throw "Batch output already exists: $batchOutput" }
New-Item -ItemType Directory -Path $batchOutput -Force | Out-Null
Import-Module (Join-Path $source 'tools\agent-repro\AgentRepro.Workspace.psm1') -Force
Import-Module (Join-Path $source 'tools\agent-repro\SceneRaid.EditorSession.psm1') -Force

function Stop-AgentEditorTree([int]$EditorPid) {
    Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $EditorPid } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Stop-Process -Id $EditorPid -Force -ErrorAction SilentlyContinue
}
function Write-Json($Value, [string]$Path) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
}

$owned = $null
$editorProcess = $null
try {
    $editor = Get-AgentReproUnity $source ''
    $head = (& git -C $source rev-parse HEAD | Out-String).Trim()
    $statusBefore = (& git -C $source status --porcelain | Out-String).Trim()
    $owned = Initialize-AgentReproWorkspace $source $WorkspaceRoot $Batch
    $applied = @()
    $parsedEdits = @()
    if ($EditsPath) { $parsedEdits = @(Get-Content -LiteralPath $EditsPath -Raw -Encoding UTF8 | ConvertFrom-Json) }
    # 隔离副本专用补丁：SceneRaid 默认拒绝 batchmode；session 0 下只能用 batchmode，且没有 Game View 渲染帧。
    $harnessPatches = @(
        [pscustomobject]@{name='entry-allow-batchmode'; path='Assets\Scripts\Editor\AgentReproduction\SceneRaid\SceneRaidEditorEntry.cs'; count=1
            old='if (Application.isBatchMode) throw new InvalidOperationException("SceneRaid requires a graphical Editor.");'; new='/* duration batch: batchmode allowed */'},
        [pscustomobject]@{name='entry-skip-gameview'; path='Assets\Scripts\Editor\AgentReproduction\SceneRaid\SceneRaidEditorEntry.cs'; count=1
            old='ConfigureGameView(_config.width, _config.height);'; new='if (!Application.isBatchMode) ConfigureGameView(_config.width, _config.height);'},
        [pscustomobject]@{name='controller-skip-render-check'; path='Assets\Scripts\Automation\SceneRaid\SceneRaidRunController.cs'; count=1
            old='if (_writer.WallSeconds >= 10 && _sampler.RenderedFrames == 0)'; new='if (!Application.isBatchMode && _writer.WallSeconds >= 10 && _sampler.RenderedFrames == 0)'}
    )
    foreach ($edit in @(@($parsedEdits) + @($harnessPatches) | Where-Object { $_ })) {
        $file = Join-Path $owned.Project $edit.path
        if ($edit.PSObject.Properties['contentBase64']) {
            [IO.File]::WriteAllBytes($file, [Convert]::FromBase64String($edit.contentBase64))
            $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
            if ($hash -ne $edit.sha256) { throw "File write hash mismatch: $($edit.path)" }
            $applied += [ordered]@{path=$edit.path; file='replaced'; sha256=$hash}
            continue
        }
        $text = [IO.File]::ReadAllText($file)
        if ($edit.PSObject.Properties['old']) {
            $count = ([regex]::Matches($text, [regex]::Escape($edit.old))).Count
            if ($count -ne [int]$edit.count) { throw "Patch $($edit.path): expected $($edit.count) matches, found $count" }
            [IO.File]::WriteAllText($file, $text.Replace($edit.old, $edit.new), [Text.UTF8Encoding]::new($false))
            $applied += [ordered]@{path=$edit.path; patch=$edit.name; matches=$count}
            continue
        }
        # 序列化字段改值：path + field + value + count
        $pattern = '(?m)^([ \t]*' + [regex]::Escape($edit.field) + ': )[^\r\n]*'
        $count = [regex]::Matches($text, $pattern).Count
        if ($count -ne [int]$edit.count) { throw "Edit $($edit.path) $($edit.field): expected $($edit.count) matches, found $count" }
        [IO.File]::WriteAllText($file, [regex]::Replace($text, $pattern, '${1}' + $edit.value), [Text.UTF8Encoding]::new($false))
        $applied += [ordered]@{path=$edit.path; field=$edit.field; value=$edit.value; matches=$count}
    }
    Write-Json ([ordered]@{batch=$Batch; sourceHead=$head; sourceDirty=$statusBefore; seeds=$seedList; simulationSpeed=$Speed;
        observeSeconds=$ObserveSeconds; edits=$applied; boxOverheadSeconds=7}) (Join-Path $batchOutput 'study.json')
    # 隔离副本的 Library 只从本仓库源码导入；不要复制其他工作区的 Library（换行不同会导致脚本 GUID 失配）。
    $library = Join-Path $owned.Project 'Library'
    if (!(Test-Path -LiteralPath (Join-Path $library 'ArtifactDB'))) {
        $importLog = Join-Path $batchOutput 'Import.log'
        Write-Output ('Full import into isolated Library ' + (Get-Date -Format 'HH:mm:ss'))
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $import = Start-Process -FilePath $editor -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-quit',
            '-projectPath',('"' + $owned.Project + '"'),'-logFile',('"' + $importLog + '"'))
        while (!$import.HasExited) {
            if ($sw.Elapsed.TotalMinutes -gt 180) { Stop-Process -Id $import.Id -Force; throw 'Full import exceeded 180 minutes.' }
            Start-Sleep -Seconds 5; $import.Refresh()
        }
        Write-Output ('Full import exit=' + $import.ExitCode + ' minutes=' + [math]::Round($sw.Elapsed.TotalMinutes, 1))
        if ($import.ExitCode -ne 0) { throw ('Full import failed; see ' + $importLog) }
    }
    $configPath = Join-Path $owned.Project '.scene-raid-run.json'
    $editorLog = Join-Path $batchOutput 'Editor.log'
    $round = 0
    foreach ($seed in $seedList) {
        $round++
        $runId = $Batch + '-' + $round + '-seed' + $seed
        $output = Join-Path $batchOutput ('run-' + $round)
        New-Item -ItemType Directory -Path $output -Force | Out-Null
        $config = [ordered]@{schemaVersion=1; routeEvidenceVersion=1; runId=$runId; outputPath=$output;
            scenePath='Assets/Scenes/Scene_DB/Scenezl_Final 1.unity'; mode='Autonomous'; seed=$seed;
            observeSeconds=$ObserveSeconds; simulationSpeed=$Speed; width=1920; height=1080; profile=$false; binaryProfile=$false;
            enabled=$true; keepEditorOpen=$true; buildPlayer=$false; captureCommandCatalog=$false; exerciseMapViewport=$false}
        Write-Json $config (Join-Path $output 'config.json')
        Write-Json $config ($configPath + '.tmp')
        if (Test-Path -LiteralPath $configPath) { [IO.File]::Replace($configPath + '.tmp', $configPath, [NullString]::Value) }
        else { [IO.File]::Move($configPath + '.tmp', $configPath) }
        $offset = if (Test-Path -LiteralPath $editorLog) { (Get-Item -LiteralPath $editorLog).Length } else { 0 }
        if (!$editorProcess) {
            $arguments = @('-batchmode','-projectPath',$owned.Project,'-executeMethod','AnomalySearch.Editor.SceneRaid.SceneRaidEditorEntry.Run',
                '-sceneRaidConfig',$configPath,'-logFile',$editorLog)
            $quoted = @($arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' })
            $editorProcess = Start-Process -FilePath $editor -ArgumentList $quoted -PassThru -WindowStyle Hidden
            Write-Json @{pid=$editorProcess.Id; startTicks=$editorProcess.StartTime.ToUniversalTime().Ticks.ToString();
                project=$owned.Project; logPath=$editorLog} (Join-Path $WorkspaceRoot '.scene-raid-session.json')
        }
        Write-Output ('ROUND ' + $round + '/' + $seedList.Count + ' seed=' + $seed + ' editorPid=' + $editorProcess.Id + ' ' + (Get-Date -Format 'HH:mm:ss'))
        $deadline = [DateTime]::UtcNow.AddMinutes($(if ($round -eq 1) { 40 } else { 15 }))
        $result = Join-Path $output 'result.json'
        while ($true) {
            $editorProcess.Refresh()
            if (Test-Path -LiteralPath (Join-Path $output 'editor-ready.json')) { break }
            if ($editorProcess.HasExited) { throw ('Editor exited early: ' + $editorProcess.ExitCode + ' (' + $output + ')') }
            if ([DateTime]::UtcNow -gt $deadline) { throw ('Round deadline exceeded: ' + $output) }
            Start-Sleep -Milliseconds 500
        }
        Copy-SceneRaidEditorLog $editorLog (Join-Path $output 'Editor.log') $offset
        $r = Get-Content -LiteralPath $result -Raw -Encoding UTF8 | ConvertFrom-Json
        Write-Output ('ROUND ' + $round + ' done: ' + $r.status + ' game=' + [math]::Round([double]$r.elapsedGameSeconds, 1) +
            ' boxes=' + $r.inventorySessions + ' errors=' + $r.errors + ' wall=' + [math]::Round([double]$r.elapsedWallSeconds, 1) + ' gfx=' + $r.graphicsDevice)
        $idleDeadline = [DateTime]::UtcNow.AddSeconds(30)
        while (!(Test-SceneRaidEditorIdle $owned.Project $editorProcess.Id)) {
            if ([DateTime]::UtcNow -gt $idleDeadline) { throw 'Editor not idle after round' }
            Start-Sleep -Milliseconds 500
        }
    }
    # session 0 下 batchmode Editor 会卡在 EditorApplication.Exit；证据写完后结束保留的空闲 Editor。
    Stop-AgentEditorTree $editorProcess.Id
    $statusAfter = (& git -C $source status --porcelain | Out-String).Trim()
    Write-Json @{sourceHead=$head; sourceUnchanged=($statusAfter -eq $statusBefore); completedUtc=[DateTime]::UtcNow.ToString('o')} (Join-Path $batchOutput 'process.json')
    if ($statusAfter -ne $statusBefore) { throw 'Source working tree changed during the batch.' }
    Write-Output ('COMPLETE ' + $batchOutput)
} catch {
    [IO.File]::WriteAllText((Join-Path $batchOutput 'launcher-error.txt'), $_.Exception.ToString())
    Write-Output ('FAILED: ' + $_.Exception.Message)
    if ($editorProcess -and !$editorProcess.HasExited) { Write-Output ('Stopping Editor pid=' + $editorProcess.Id); Stop-AgentEditorTree $editorProcess.Id }
    exit 1
} finally {
    if ($owned) { $owned.Lock.Dispose() }
    if ($editorProcess) { $editorProcess.Dispose() }
}
