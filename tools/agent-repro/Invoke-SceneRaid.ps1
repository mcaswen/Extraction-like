param(
    [ValidateSet('SC00','SC01')][string]$Case = 'SC01',
    [string]$UnityPath,
    [string]$WorkspaceRoot,
    [ValidateRange(120,3600)][int]$TimeoutSeconds = 900,
    [ValidateRange(15,180)][int]$ShutdownTimeoutSeconds = 60
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'AgentRepro.Workspace.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Report.psm1') -Force
$source = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$WorkspaceRoot) { $WorkspaceRoot = Join-Path (Split-Path $source -Parent) '.agent-repro/AnomalySearch' }
$runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$output = Join-Path $source "Logs/SceneRaid/$runId"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$workspace = $null
$process = $null
try {
    $editor = Get-AgentReproUnity $source $UnityPath
    $caseConfig = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'scene-raid-cases.json') -Raw | ConvertFrom-Json).cases | Where-Object id -eq $Case
    $profile = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'scene-raid-profiles.json') -Raw | ConvertFrom-Json
    $config = [ordered]@{schemaVersion=1;runId=$runId;outputPath=$output;scenePath=$profile.scenePath;mode=$caseConfig.mode;
        seed=$caseConfig.seed;observeSeconds=$caseConfig.observeSeconds;simulationSpeed=$profile.simulationSpeed;
        width=$profile.width;height=$profile.height;profile=$profile.profile;binaryProfile=$profile.binaryProfile}
    $before = Get-AgentReproSourceManifest $source
    @{runId=$runId;case=$Case;commit=(& git -C $source rev-parse HEAD);dirty=(& git -C $source status --porcelain);
        files=$before;competingUnity=@(Get-Process Unity -ErrorAction SilentlyContinue | Select-Object Id,CPU,Path)} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
    $workspace = Initialize-AgentReproWorkspace $source $WorkspaceRoot $runId
    $configPath = Join-Path $workspace.Project '.scene-raid-run.json'
    $config | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
    Copy-Item -LiteralPath $configPath -Destination (Join-Path $output 'config.json')
    $arguments = @('-projectPath',$workspace.Project,'-executeMethod','AnomalySearch.Editor.SceneRaid.SceneRaidEditorEntry.Run',
        '-sceneRaidConfig',$configPath,'-logFile',(Join-Path $output 'Editor.log'))
    $arguments | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'arguments.json') -Encoding UTF8
    $quoted = @($arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' })
    Write-Output "SceneRaid $Case starting: $output"
    $process = Start-Process -FilePath $editor -ArgumentList $quoted -PassThru -WindowStyle Hidden
    @{pid=$process.Id;startedUtc=[DateTime]::UtcNow.ToString('o');editor=$editor} | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $output 'process-start.json') -Encoding UTF8
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $shutdownDeadline = $null
    $terminationReason = ''
    while (!$process.HasExited) {
        if (!$shutdownDeadline -and (Test-Path -LiteralPath (Join-Path $output 'result.json'))) { $shutdownDeadline = [DateTime]::UtcNow.AddSeconds($ShutdownTimeoutSeconds) }
        if ([DateTime]::UtcNow -gt $deadline) { $terminationReason='run_timeout' }
        if ($shutdownDeadline -and [DateTime]::UtcNow -gt $shutdownDeadline) { $terminationReason='shutdown_timeout' }
        if ($terminationReason) {
            @{reason=$terminationReason;pid=$process.Id;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json |
                Set-Content -LiteralPath (Join-Path $output 'termination.json') -Encoding UTF8
            & taskkill /PID $process.Id /T /F | Out-Null
            if (!$process.WaitForExit(10000)) { throw 'Owned Unity process did not terminate.' }
            break
        }
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    }
    $code = $process.ExitCode
    $after = Get-AgentReproSourceManifest $source
    $unchanged = ($before | ConvertTo-Json -Depth 5 -Compress) -eq ($after | ConvertTo-Json -Depth 5 -Compress)
    @{exitCode=$code;sourceUnchanged=$unchanged;terminationReason=$terminationReason;completedUtc=[DateTime]::UtcNow.ToString('o')} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process.json') -Encoding UTF8
    $report = Test-SceneRaidEvidence $output ([pscustomobject]$config) $code $unchanged
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
    if ($process -and !$process.HasExited) { & taskkill /PID $process.Id /T /F | Out-Null }
    if ($process) { $process.Dispose() }
    if ($workspace) { $workspace.Lock.Dispose() }
}
