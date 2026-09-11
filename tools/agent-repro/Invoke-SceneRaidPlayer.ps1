param(
    [Parameter(Mandatory)][string]$BuildRunPath,
    [ValidateRange(120,1200)][int]$TimeoutSeconds = 600,
    [switch]$ObserveOnly,
    [switch]$ShowWindow,
    [string]$ScenarioId,
    [ValidateRange(5,600)][Nullable[float]]$ObserveSeconds
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'AgentRepro.Workspace.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Report.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.CommandConfig.psm1') -Force
$source = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$buildRoot = (Resolve-Path -LiteralPath $BuildRunPath).Path
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $source 'Logs/SceneRaid')).TrimEnd('\')
if (!$buildRoot.StartsWith($allowedRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Build must belong to this project SceneRaid output.' }
$output = Join-Path $buildRoot 'PlayerRun'
$process = $null
$lock = $null
try {
    $lock = [IO.File]::Open((Join-Path $buildRoot '.player-run.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    if (Test-Path -LiteralPath $output) { throw 'This build product already has a PlayerRun; build a new runId to keep saves isolated.' }
    $config = Get-Content -LiteralPath (Join-Path $buildRoot 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $ready = Get-Content -LiteralPath (Join-Path $buildRoot 'editor-ready.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $buildReport = Test-SceneRaidEvidence $buildRoot $config $null $true -EditorRetained -EditorProcessId $ready.pid
    if (!$config.buildPlayer -or $buildReport.evidenceStatus -ne 'PASS') { throw 'Player requires a verified successful SC07 build.' }
    $buildManifest = Get-Content -LiteralPath (Join-Path $buildRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $before = Get-AgentReproSourceManifest $source
    if (($before | ConvertTo-Json -Depth 6 -Compress) -ne ($buildManifest.files | ConvertTo-Json -Depth 6 -Compress)) {
        throw 'Current source differs from the build input; build again before Player validation.'
    }
    New-Item -ItemType Directory -Path $output | Out-Null
    Copy-Item -LiteralPath (Join-Path $buildRoot 'scene-audit.json') -Destination (Join-Path $output 'scene-audit.json')
    $executable = Join-Path $buildRoot 'Player/SceneRaid.exe'
    $exeHash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    $assembly = Join-Path $buildRoot 'Player/SceneRaid_Data/Managed/Assembly-CSharp.dll'
    $assemblyHash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    $config.mode = if ($ObserveOnly) { 'Observe' } else { 'Autonomous' }
    if ($ScenarioId) {
        if ($ObserveOnly) { throw 'ManualCluster and ObserveOnly are mutually exclusive.' }
        $scenario = Get-SceneRaidCommandScenario $ScenarioId
        $config.mode='ManualCluster'; $config.schemaVersion=2
        $config | Add-Member -NotePropertyName scenarioJson -NotePropertyValue $scenario.json -Force
        $config | Add-Member -NotePropertyName scenarioSha256 -NotePropertyValue $scenario.sha256 -Force
        [IO.File]::WriteAllText((Join-Path $output 'command-scenario.json'), $scenario.json, [Text.UTF8Encoding]::new($false))
    }
    $config.outputPath = $output; $config.enabled = $true
    if ($null -ne $ObserveSeconds) { $config.observeSeconds = $ObserveSeconds }
    $config.buildPlayer = $false; $config.simulationSpeed = 1; $config.profile = $false; $config.binaryProfile = $false
    $config | Add-Member -NotePropertyName quitPlayerWhenComplete -NotePropertyValue $true -Force
    $configPath = Join-Path $output 'config.json'
    $config | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $configPath -Encoding UTF8
    @{runId=$config.runId;buildRunPath=$buildRoot;buildManifestSha256=(Get-FileHash -LiteralPath (Join-Path $buildRoot 'manifest.json')).Hash;
        executableSha256=$exeHash;assemblySha256=$assemblyHash;files=$before;
        competingUnity=@(Get-Process Unity -ErrorAction SilentlyContinue | Select-Object Id,CPU,Path)} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
    $arguments = @('-sceneRaidConfig',$configPath,'-logFile',(Join-Path $output 'Player.log'),
        '-screen-width',$config.width.ToString(),'-screen-height',$config.height.ToString(),'-screen-fullscreen','1')
    $arguments | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'arguments.json') -Encoding UTF8
    $quoted = @($arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' })
    $windowStyle = if ($ShowWindow) { 'Normal' } else { 'Hidden' }
    $process = Start-Process -FilePath $executable -ArgumentList $quoted -PassThru -WindowStyle $windowStyle
    @{pid=$process.Id;startedUtc=$process.StartTime.ToUniversalTime().ToString('o');executable=$executable;windowStyle=$windowStyle} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process-start.json') -Encoding UTF8
    Write-Output "SceneRaid Player starting: $output"
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $shutdownDeadline = $null
    $terminationReason = ''
    while (!$process.HasExited) {
        if (!$shutdownDeadline -and (Test-Path -LiteralPath (Join-Path $output 'result.json'))) { $shutdownDeadline = [DateTime]::UtcNow.AddSeconds(30) }
        if ([DateTime]::UtcNow -gt $deadline) { $terminationReason = 'player_run_timeout' }
        if ($shutdownDeadline -and [DateTime]::UtcNow -gt $shutdownDeadline) { $terminationReason = 'player_shutdown_timeout' }
        if ($terminationReason) { Stop-Process -Id $process.Id; break }
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    }
    if (!$process.WaitForExit(10000)) { throw 'Owned validation Player did not exit.' }
    $code = $process.ExitCode
    $after = Get-AgentReproSourceManifest $source
    $unchanged = ($before | ConvertTo-Json -Depth 6 -Compress) -eq ($after | ConvertTo-Json -Depth 6 -Compress)
    $binaryUnchanged = $exeHash -eq (Get-FileHash -LiteralPath $executable).Hash -and $assemblyHash -eq (Get-FileHash -LiteralPath $assembly).Hash
    @{pid=$process.Id;exitCode=$code;sourceUnchanged=$unchanged;binaryUnchanged=$binaryUnchanged;terminationReason=$terminationReason;
        completedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process.json') -Encoding UTF8
    $report = Test-SceneRaidEvidence $output $config $code ($unchanged -and $binaryUnchanged) -PlayerRun
    $passed = !$terminationReason -and $report.evidenceStatus -eq 'PASS' -and
        ($ObserveOnly -or ($report.gameStatus -in @('PASS','EXPECTED_DEATH') -and $report.diagnosticTiming.thresholdsMet))
    $report | Add-Member -NotePropertyName runAcceptance -NotePropertyValue $(if (!$passed) {'FAIL'} elseif ($ObserveOnly) {'OBSERVATION_COMPLETE'} else {'PASS'})
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding UTF8
    $report | Select-Object runId,runAcceptance,evidenceStatus,gameStatus,gameErrors,behaviorFailures,stagnationSuspicions,issues,
        @{n='averageFps';e={$_.diagnosticTiming.averageFps}} | ConvertTo-Json -Depth 4 | Write-Output
    if (!$passed) { throw "Player validation failed; inspect $output" }
} catch {
    if (Test-Path -LiteralPath $output) { $_.Exception.ToString() | Set-Content -LiteralPath (Join-Path $output 'launcher-error.txt') -Encoding UTF8 }
    Write-Error $_ -ErrorAction Continue
    exit 1
} finally {
    if ($process) { if (!$process.HasExited) { Stop-Process -Id $process.Id }; $process.Dispose() }
    if ($lock) { $lock.Dispose() }
}
