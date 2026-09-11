param(
    [ValidateSet('SC00','SC01','SC02','SC03')][string]$Case = 'SC01',
    [string]$UnityPath,
    [string]$WorkspaceRoot,
    [ValidateRange(120,3600)][int]$TimeoutSeconds = 900,
    [ValidateRange(15,180)][int]$ShutdownTimeoutSeconds = 60,
    [ValidateRange(5,600)][Nullable[float]]$ObserveSeconds,
    [switch]$ExitEditor
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'AgentRepro.Workspace.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Report.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.EditorSession.psm1') -Force
$source = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$WorkspaceRoot) { $WorkspaceRoot = Join-Path (Split-Path $source -Parent) '.agent-repro/AnomalySearch' }
$runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$output = Join-Path $source "Logs/SceneRaid/$runId"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$workspace = $null
$process = $null
$editorLog = $null
$logOffset = 0
try {
    $editor = Get-AgentReproUnity $source $UnityPath
    $session = Get-SceneRaidEditorSession $WorkspaceRoot $editor
    if ($session) {
        $process = $session.Process
        if (!(Test-SceneRaidEditorIdle $session.Info.project $process.Id)) { throw 'Retained Editor is not idle; no files were synchronized.' }
    }
    $caseConfig = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'scene-raid-cases.json') -Raw | ConvertFrom-Json).cases | Where-Object id -eq $Case
    $profile = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'scene-raid-profiles.json') -Raw | ConvertFrom-Json
    $config = [ordered]@{schemaVersion=1;runId=$runId;outputPath=$output;scenePath=$profile.scenePath;mode=$caseConfig.mode;
        seed=$caseConfig.seed;observeSeconds=$(if ($null -ne $ObserveSeconds) {$ObserveSeconds} else {$caseConfig.observeSeconds});simulationSpeed=$caseConfig.simulationSpeed;
        width=$profile.width;height=$profile.height;profile=$profile.profile;binaryProfile=$profile.binaryProfile;
        enabled=$true;keepEditorOpen=(!$ExitEditor)}
    $before = Get-AgentReproSourceManifest $source
    @{runId=$runId;case=$Case;commit=(& git -C $source rev-parse HEAD);dirty=(& git -C $source status --porcelain);
        files=$before;competingUnity=@(Get-Process Unity -ErrorAction SilentlyContinue | Select-Object Id,CPU,Path)} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
    $workspace = Initialize-AgentReproWorkspace $source $WorkspaceRoot $runId -AllowedEditorPid $(if ($process) {$process.Id} else {0})
    $configPath = Join-Path $workspace.Project '.scene-raid-run.json'
    $editorLog = if ($session) { $session.Info.logPath } else { Join-Path $source "Logs/SceneRaidSession/$runId/Editor.log" }
    New-Item -ItemType Directory -Path (Split-Path $editorLog -Parent) -Force | Out-Null
    if ($session -and (Test-Path -LiteralPath $editorLog)) { $logOffset = (Get-Item -LiteralPath $editorLog).Length }
    $config | ConvertTo-Json | Set-Content -LiteralPath ($configPath + '.tmp') -Encoding UTF8
    if (Test-Path -LiteralPath $configPath) { [IO.File]::Replace($configPath + '.tmp',$configPath,[NullString]::Value) }
    else { [IO.File]::Move($configPath + '.tmp',$configPath) }
    Copy-Item -LiteralPath $configPath -Destination (Join-Path $output 'config.json')
    $arguments = @('-projectPath',$workspace.Project,'-executeMethod','AnomalySearch.Editor.SceneRaid.SceneRaidEditorEntry.Run',
        '-sceneRaidConfig',$configPath,'-logFile',$editorLog)
    $arguments | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'arguments.json') -Encoding UTF8
    $quoted = @($arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' })
    Write-Output "SceneRaid $Case starting: $output"
    if (!$process) {
        $process = Start-Process -FilePath $editor -ArgumentList $quoted -PassThru -WindowStyle Hidden
        @{pid=$process.Id;startTicks=$process.StartTime.ToUniversalTime().Ticks.ToString();project=$workspace.Project;logPath=$editorLog} | ConvertTo-Json |
            Set-Content -LiteralPath (Join-Path $workspace.Root '.scene-raid-session.json') -Encoding UTF8
    }
    @{pid=$process.Id;startedUtc=$process.StartTime.ToUniversalTime().ToString('o');editor=$editor;reused=($null -ne $session);logPath=$editorLog;logOffset=$logOffset} | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $output 'process-start.json') -Encoding UTF8
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $shutdownDeadline = $null
    $terminationReason = ''
    while (!$process.HasExited) {
        if (!$ExitEditor -and (Test-Path -LiteralPath (Join-Path $output 'editor-ready.json'))) { break }
        if (!$shutdownDeadline -and (Test-Path -LiteralPath (Join-Path $output 'result.json'))) { $shutdownDeadline = [DateTime]::UtcNow.AddSeconds($ShutdownTimeoutSeconds) }
        if ([DateTime]::UtcNow -gt $deadline) { $terminationReason='run_timeout' }
        if ($shutdownDeadline -and [DateTime]::UtcNow -gt $shutdownDeadline) { $terminationReason='shutdown_timeout' }
        if ($terminationReason) {
            @{reason=$terminationReason;pid=$process.Id;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json |
                Set-Content -LiteralPath (Join-Path $output 'termination.json') -Encoding UTF8
            if ($ExitEditor) {
                & taskkill /PID $process.Id /T /F | Out-Null
                if (!$process.WaitForExit(10000)) { throw 'Owned Unity process did not terminate.' }
            }
            break
        }
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    }
    $process.Refresh()
    $retained = !$process.HasExited
    $code = if ($retained) { $null } else { $process.ExitCode }
    Copy-SceneRaidEditorLog $editorLog (Join-Path $output 'Editor.log') $logOffset
    $after = Get-AgentReproSourceManifest $source
    $unchanged = ($before | ConvertTo-Json -Depth 5 -Compress) -eq ($after | ConvertTo-Json -Depth 5 -Compress)
    @{pid=$process.Id;exitCode=$code;editorRetained=$retained;sourceUnchanged=$unchanged;terminationReason=$terminationReason;completedUtc=[DateTime]::UtcNow.ToString('o')} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process.json') -Encoding UTF8
    $report = Test-SceneRaidEvidence $output ([pscustomobject]$config) $code $unchanged -EditorRetained:$retained -EditorProcessId $process.Id
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding UTF8
    $report | Select-Object runId,evidenceStatus,gameStatus,gameErrors,behaviorFailures,stagnationSuspicions,issues |
        ConvertTo-Json -Depth 4 | Write-Output
    Write-Output "Full evidence: $output"
    if ($report.evidenceStatus -ne 'PASS') { throw "SceneRaid evidence failed. See $output" }
} catch {
    $_.Exception.ToString() | Set-Content -LiteralPath (Join-Path $output 'launcher-error.txt') -Encoding UTF8
    Write-Error $_ -ErrorAction Continue
    exit 1
} finally {
    if ($ExitEditor -and $process -and !$process.HasExited) { & taskkill /PID $process.Id /T /F | Out-Null }
    if ($process) { $process.Dispose() }
    if ($workspace) { $workspace.Lock.Dispose() }
}
