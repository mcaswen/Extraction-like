$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Report.psm1') -Force
$root = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path ('Logs/SceneRaidReportProbes/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$config = [pscustomobject]@{runId='probe';mode='Observe';scenePath='scene.unity';width=3840;height=2160;observeSeconds=0.058}
$fixtureCounters = @('PlayerLoop','GC Allocated In Frame','Anomaly.Discovery.Update','Anomaly.Pawn.Update','Anomaly.Zone.Update',
    'Anomaly.Discovery.ScanAgent','Anomaly.Pawn.Facts','Anomaly.Pawn.Lifecycle','Anomaly.Pawn.Brain',
    'Anomaly.Zone.State','Anomaly.Zone.Shape','Anomaly.Navigation.Check',
    'Anomaly.Cluster.LateUpdate','Anomaly.Cluster.State','Anomaly.Cluster.Input','Anomaly.Cluster.Range',
    'Anomaly.Range.Geometry','Anomaly.Range.Projection','Anomaly.Range.Line','Anomaly.Ground.Raycast','Anomaly.Ground.Filter')
function Write-Fixture([string]$Name) {
    $path = Join-Path $root $Name
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    @{runId='probe';scene='scene.unity';status='AUDITED'} | ConvertTo-Json | Set-Content "$path/scene-audit.json" -Encoding UTF8
    @{schemaVersion=1;runId='probe';mode='Observe';scenePath='scene.unity';status='OBSERVED';frames=4;renderedFrames=4;batchMode=$false;
        runtime='Editor';developmentBuild=$true;quality='High Fidelity';profilerEnabled=$false;vSyncCount=0;targetFrameRate=-1;graphicsApi='Direct3D11';
        screenWidth=3840;screenHeight=2160;cameraWidth=3840;cameraHeight=2160;lostEvents=0;elapsedWallSeconds=0.058;errors=0;warnings=0;
        events=2;observedAgents=@('1','2');counters=@($fixtureCounters | ForEach-Object { if($_ -eq 'GC Allocated In Frame') {'GC Allocated In Frame | Memory | Bytes'} else {$_+' | Scripts | TimeNanoseconds'} })} | ConvertTo-Json | Set-Content "$path/result.json" -Encoding UTF8
    @('{"sequence":1,"kind":"bootstrap.beforeSceneLoad"}','{"sequence":2,"kind":"run.completed"}') | Set-Content "$path/events.jsonl" -Encoding UTF8
    @('frame,wallSeconds,intervalMs,gameSeconds,timeScale','1,0.01,-1,0.01,1','2,0.026,16,0.026,1','3,0.042,16,0.042,1','4,0.058,16,0.058,1') | Set-Content "$path/frames.csv" -Encoding UTF8
    @('observedAtFrame,name,previousValue,previousCalls'; foreach($counter in $fixtureCounters) { "1,$counter,-1,-1"; "2,$counter,1000,1"; "3,$counter,1000,1"; "4,$counter,1000,1" }) | Set-Content "$path/counters.csv" -Encoding UTF8
    return $path
}
function Edit-Result([string]$Path, [string]$Property, $Value) {
    $valueObject = Get-Content "$Path/result.json" -Raw -Encoding UTF8 | ConvertFrom-Json
    $valueObject.$Property = $Value
    $valueObject | ConvertTo-Json | Set-Content "$Path/result.json" -Encoding UTF8
}
$passed = 0
$path = Write-Fixture 'valid'
$report = Test-SceneRaidEvidence $path $config 0 $true
if ($report.evidenceStatus -ne 'PASS' -or $report.gameStatus -ne 'NOT_FULL_RAID_VALIDATED' -or $report.performanceAcceptance) { throw 'Valid observe result incorrectly classified.' }
$passed++
$path = Write-Fixture 'retained_editor'
$ready = @{runId='probe';pid=731;idle=$true;phase='idle';exitCode=0}
$ready | ConvertTo-Json | Set-Content "$path/editor-ready.json" -Encoding UTF8
$retained = Test-SceneRaidEvidence $path $config $null $true -EditorRetained -EditorProcessId 731
if ($retained.evidenceStatus -ne 'PASS' -or $retained.editorLifecycle -ne 'EDITOR_RETAINED' -or $null -ne $retained.processExitCode) { throw 'Retained Editor was confused with an exited process.' }; $passed++
foreach ($field in @('runId','pid','idle','phase','exitCode')) {
    $badReady = $ready.Clone()
    $badReady[$field] = switch ($field) { 'runId' {'wrong'} 'pid' {732} 'idle' {$false} 'phase' {'busy'} 'exitCode' {1} }
    $badReady | ConvertTo-Json | Set-Content "$path/editor-ready.json" -Encoding UTF8
    if ((Test-SceneRaidEvidence $path $config $null $true -EditorRetained -EditorProcessId 731).evidenceStatus -ne 'FAIL') { throw "Invalid retained state accepted: $field" }; $passed++
}
$ready | ConvertTo-Json | Set-Content "$path/editor-ready.json" -Encoding UTF8
if ((Test-SceneRaidEvidence $path $config 0 $true -EditorRetained -EditorProcessId 731).evidenceStatus -ne 'FAIL') { throw 'Retained Editor accepted a fabricated exit code.' }; $passed++
Remove-Item -LiteralPath "$path/editor-ready.json"
if ((Test-SceneRaidEvidence $path $config $null $true -EditorRetained -EditorProcessId 731).evidenceStatus -ne 'FAIL') { throw 'Missing retained ready state accepted.' }; $passed++
$path = Write-Fixture 'one_counter_without_samples'
(Get-Content "$path/counters.csv" -Raw).Replace('Anomaly.Navigation.Check,1000,1','Anomaly.Navigation.Check,-1,-1') | Set-Content "$path/counters.csv" -Encoding UTF8
$report = Test-SceneRaidEvidence $path $config 0 $true
$missingCounter = @($report.counterSummaries | Where-Object name -eq 'Anomaly.Navigation.Check')
if ($report.evidenceStatus -ne 'FAIL' -or $report.issues -notcontains 'counter_no_samples:Anomaly.Navigation.Check' -or
    @($report.issues | Where-Object {$_ -like 'invalid_or_missing_evidence:*'}).Count -gt 0 -or
    $report.counterSummaries.Count -ne $fixtureCounters.Count -or $missingCounter[0].averageRaw -ne $null) {
    throw 'A counter with no samples must stay unavailable without discarding all other summaries.'
}
$passed++
$faults = @(
    @{name='missing_result';act={param($p) Remove-Item -LiteralPath "$p/result.json"}},
    @{name='invalid_json';act={param($p) '{' | Set-Content "$p/result.json"}},
    @{name='wrong_run';act={param($p) Edit-Result $p 'runId' 'wrong'}},
    @{name='wrong_mode';act={param($p) Edit-Result $p 'mode' 'AutonomousRaid'}},
    @{name='sampler_overflow';act={param($p) Edit-Result $p 'status' 'HARNESS_FAILED'}},
    @{name='zero_frames';act={param($p) Edit-Result $p 'frames' 0}},
    @{name='zero_render';act={param($p) Edit-Result $p 'renderedFrames' 0}},
    @{name='wrong_resolution';act={param($p) Edit-Result $p 'cameraWidth' 1920}},
    @{name='events_lost';act={param($p) Edit-Result $p 'lostEvents' 1}},
    @{name='truncated_duration';act={param($p) Edit-Result $p 'elapsedWallSeconds' 0.002}},
    @{name='false_duration';act={param($p) Edit-Result $p 'elapsedWallSeconds' 60}},
    @{name='missing_agent';act={param($p) Edit-Result $p 'observedAgents' @('1')}},
    @{name='event_gap';act={param($p) @('{"sequence":1,"kind":"bootstrap.beforeSceneLoad"}','{"sequence":3,"kind":"run.completed"}') | Set-Content "$p/events.jsonl"}},
    @{name='no_completion';act={param($p) '{"sequence":1,"kind":"bootstrap.beforeSceneLoad"}' | Set-Content "$p/events.jsonl"}},
    @{name='truncated_frames';act={param($p) 'frame,intervalMs' | Set-Content "$p/frames.csv"}},
    @{name='counter_unavailable';act={param($p) Edit-Result $p 'counters' @('Anomaly.Discovery.Update: unavailable')}},
    @{name='counter_unit';act={param($p) Edit-Result $p 'counters' @('Anomaly.Discovery.Update | Scripts | Unknown')}},
    @{name='counter_truncated';act={param($p) 'observedAtFrame,name,previousValue,previousCalls' | Set-Content "$p/counters.csv"}},
    @{name='nonfinite';act={param($p) (Get-Content "$p/frames.csv" -Raw).Replace('2,0.026,16','2,0.026,NaN') | Set-Content "$p/frames.csv"}},
    @{name='backward_time';act={param($p) (Get-Content "$p/frames.csv" -Raw).Replace('3,0.042','3,0.022') | Set-Content "$p/frames.csv"}},
    @{name='false_fast_interval';act={param($p) (Get-Content "$p/frames.csv" -Raw).Replace(',16,',',1,') | Set-Content "$p/frames.csv"}},
    @{name='missing_frame';act={param($p) (Get-Content "$p/frames.csv" -Raw).Replace('4,0.058','5,0.058') | Set-Content "$p/frames.csv"}},
    @{name='wrong_counter_name';act={param($p) (Get-Content "$p/counters.csv" -Raw).Replace('Anomaly.Discovery.Update','FakeCounter') | Set-Content "$p/counters.csv"}},
    @{name='zero_counter_calls';act={param($p) (Get-Content "$p/counters.csv" -Raw).Replace(',1000,1',',0,0') | Set-Content "$p/counters.csv"}},
    @{name='counter_frame_gap';act={param($p) (Get-Content "$p/counters.csv" -Raw).Replace('4,Anomaly.Discovery.Update,','3,Anomaly.Discovery.Update,') | Set-Content "$p/counters.csv"}},
    @{name='counter_family_omitted';act={param($p)
        $r=Get-Content "$p/result.json" -Raw | ConvertFrom-Json
        Edit-Result $p 'counters' @($r.counters | Where-Object {$_ -notlike 'Anomaly.Pawn.*'})
        @(Get-Content "$p/counters.csv" | Where-Object {$_ -notlike '*,Anomaly.Pawn.*'}) | Set-Content "$p/counters.csv"
    }}
)
foreach ($fault in $faults) {
    $path = Write-Fixture $fault.name
    & $fault.act $path
    if ((Test-SceneRaidEvidence $path $config 0 $true).evidenceStatus -ne 'FAIL') { throw "Fault accepted: $($fault.name)" }
    $passed++
}
$path = Write-Fixture 'process_exit'
if ((Test-SceneRaidEvidence $path $config 1 $true).evidenceStatus -ne 'FAIL') { throw 'Exit failure accepted.' }; $passed++
if ((Test-SceneRaidEvidence $path $config 0 $false).evidenceStatus -ne 'FAIL') { throw 'Changed source accepted.' }; $passed++
Edit-Result $path 'errors' 1
$report = Test-SceneRaidEvidence $path $config 0 $true
if ($report.evidenceStatus -ne 'PASS' -or $report.gameStatus -ne 'ISSUES_OBSERVED') { throw 'Game errors hidden or confused with harness failures.' }; $passed++
$path = Write-Fixture 'autonomous_blocked'
$autoConfig = [pscustomobject]@{runId='probe';mode='Autonomous';scenePath='scene.unity';width=3840;height=2160;observeSeconds=600}
Edit-Result $path 'mode' 'Autonomous'
Edit-Result $path 'status' 'BEHAVIOR_BLOCKED'
$report = Test-SceneRaidEvidence $path $autoConfig 0 $true
if ($report.evidenceStatus -ne 'PASS' -or $report.gameStatus -ne 'ISSUES_OBSERVED' -or $report.performanceAcceptance) { throw 'Autonomous blockage mislabeled.' }; $passed++
Edit-Result $path 'status' 'RAID_OBSERVED_COMPLETE'
$report = Test-SceneRaidEvidence $path $autoConfig 0 $true
if ($report.evidenceStatus -ne 'PASS' -or $report.gameStatus -ne 'NOT_FULL_RAID_VALIDATED' -or $report.performanceAcceptance) { throw 'Observed ending confused with final acceptance.' }; $passed++
Edit-Result $path 'status' 'PASSED'
if ((Test-SceneRaidEvidence $path $autoConfig 0 $true).evidenceStatus -ne 'FAIL') { throw 'Unverified autonomous success accepted.' }; $passed++
function New-FrameSeries([double]$StepMs, [int]$SpikeAt = -1) {
    $clock = 0.0
    for ($i = 0; $i -lt 1000; $i++) {
        $ms = if ($i -eq $SpikeAt) { 40.0 } else { $StepMs }
        $clock += $ms/1000
        [pscustomobject]@{frame=($i+1);wallSeconds=$clock.ToString('F6',[Globalization.CultureInfo]::InvariantCulture);
            gameSeconds=$clock.ToString('F6',[Globalization.CultureInfo]::InvariantCulture);timeScale='1';intervalMs=$(if ($i -eq 0) {'-1'} else {$ms.ToString([Globalization.CultureInfo]::InvariantCulture)})}
    }
}
if (!(Get-SceneRaidFrameStatistics @(New-FrameSeries 5)).thresholdsMet) { throw 'Stable 200 FPS rejected.' }; $passed++
$spike = Get-SceneRaidFrameStatistics @(New-FrameSeries 5 100)
if (!$spike.thresholdsMet -or $spike.maxMs -ne 40 -or $spike.slowFrameCount -ne 1 -or !$spike.tailMetricsDiagnosticOnly) { throw 'Average-only acceptance must retain the slow frame without rejecting it.' }; $passed++
if (!(Get-SceneRaidFrameStatistics @(New-FrameSeries 12.5)).thresholdsMet) { throw 'Stable 80 FPS rejected by the 60 FPS target.' }; $passed++
if ((Get-SceneRaidFrameStatistics @(New-FrameSeries 20)).thresholdsMet) { throw '50 FPS accepted.' }; $passed++
$boundary = Get-SceneRaidFrameStatistics @(New-FrameSeries (1000.0/60))
if ($boundary.thresholdsMet -or $boundary.targetFps -ne 60 -or $boundary.acceptancePolicy -ne 'AverageFpsGreaterThanTarget') { throw 'Exactly 60 FPS must not satisfy strictly greater than 60.' }; $passed++
if (!(Get-SceneRaidFrameStatistics @(New-FrameSeries (1000.0/60.01))).thresholdsMet) { throw 'Average above 60 FPS rejected.' }; $passed++
if ((Get-SceneRaidFrameStatistics @(New-FrameSeries (1000.0/59.99))).thresholdsMet) { throw 'Average below 60 FPS accepted.' }; $passed++
if ((Get-SceneRaidFrameStatistics @(New-FrameSeries 12.5) -TargetFps 120).thresholdsMet) { throw 'Explicit historical 120 FPS comparison ignored.' }; $passed++
$path = Write-Fixture 'player'
Edit-Result $path 'runtime' 'Player'
$playerReport = Test-SceneRaidEvidence $path $config 0 $true -PlayerRun
if ($playerReport.evidenceStatus -ne 'PASS' -or $playerReport.editorLifecycle -ne 'PLAYER_EXITED') { throw 'Valid Player result rejected.' }; $passed++
foreach ($field in @('runtime','developmentBuild','quality','profilerEnabled','vSyncCount','targetFrameRate','graphicsApi')) {
    $original = (Get-Content "$path/result.json" -Raw -Encoding UTF8 | ConvertFrom-Json).$field
    $bad = switch ($field) { 'runtime' {'Editor'} 'developmentBuild' {$false} 'quality' {'Low'} 'profilerEnabled' {$true} 'vSyncCount' {1} 'targetFrameRate' {60} 'graphicsApi' {'Null'} }
    Edit-Result $path $field $bad
    if ((Test-SceneRaidEvidence $path $config 0 $true -PlayerRun).issues -notcontains 'player_environment_mismatch') { throw "Player environment mismatch hidden: $field" }; $passed++
    Edit-Result $path $field $original
}
if ((Test-SceneRaidEvidence $path $config $null $true -PlayerRun).evidenceStatus -ne 'FAIL') { throw 'Player without exit code accepted.' }; $passed++
$missingPlatform = Get-Content "$path/result.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$missingPlatform.PSObject.Properties.Remove('runtime')
$missingPlatform | ConvertTo-Json | Set-Content "$path/result.json" -Encoding UTF8
if ((Test-SceneRaidEvidence $path $config 0 $true -PlayerRun).evidenceStatus -ne 'FAIL') { throw 'Player without platform evidence accepted.' }; $passed++
$buildConfig = [pscustomobject]@{runId='probe';mode='Audit';scenePath='scene.unity';buildPlayer=$true}
$path = Write-Fixture 'build'
New-Item -ItemType Directory -Path "$path/Player" | Out-Null
[IO.File]::WriteAllBytes("$path/Player/SceneRaid.exe", [byte[]](1,2,3))
$build = @{schemaVersion=1;runId='probe';scenePath='scene.unity';status='BUILT';bytes=3;seconds=1;errors=0;
    backend='Mono2x';target='StandaloneWindows64';options='Development';optionsBits=1;effectiveOptionsBits=1;quality='High Fidelity';
    defines=@('ANOMALY_SCENE_AUTOMATION');executable="$path/Player/SceneRaid.exe"}
$build | ConvertTo-Json | Set-Content "$path/build-result.json" -Encoding UTF8
$buildReport = Test-SceneRaidEvidence $path $buildConfig 0 $true
if ($buildReport.evidenceStatus -ne 'PASS' -or $buildReport.gameStatus -ne 'NOT_FULL_RAID_VALIDATED') { throw 'Build result incorrectly classified.' }; $passed++
foreach ($field in @('runId','scenePath','status','bytes','errors','backend','target','optionsBits','effectiveOptionsBits','defines','executable')) {
    $invalid = $build.Clone()
    $invalid[$field] = switch ($field) { 'bytes' {0} 'errors' {1} default {'wrong'} }
    $invalid | ConvertTo-Json | Set-Content "$path/build-result.json" -Encoding UTF8
    if ((Test-SceneRaidEvidence $path $buildConfig 0 $true).evidenceStatus -ne 'FAIL') { throw "Invalid build accepted: $field" }; $passed++
}
$build | ConvertTo-Json | Set-Content "$path/build-result.json" -Encoding UTF8
Remove-Item -LiteralPath "$path/Player/SceneRaid.exe"
if ((Test-SceneRaidEvidence $path $buildConfig 0 $true).evidenceStatus -ne 'FAIL') { throw 'Missing executable accepted.' }; $passed++
Remove-Item -LiteralPath "$path/build-result.json"
if ((Test-SceneRaidEvidence $path $buildConfig 0 $true).evidenceStatus -ne 'FAIL') { throw 'Missing build result accepted.' }; $passed++
foreach ($scenario in 'valid','runtime_error','live_agent','missing_warehouse','failed_directive') {
    $path=Write-Fixture "death_$scenario"
    Edit-Result $path 'mode' 'Autonomous'
    Edit-Result $path 'status' 'RAID_OBSERVED_FAILURE'
    $last=@{requiredAgents=@('1','2');extractedAgents=@();settledAgents=@();missionCompleted=$false;missionFailed=$true;
        inventoryOpen=$false;timeScale=0;agents=@(@{id='1';health=0;hasEnemy=$false},@{id='2';health=0;hasEnemy=$false})}
    if ($scenario -eq 'live_agent') { $last.agents[0].health=1 }
    $stream=[Collections.Generic.List[object]]::new()
    $stream.Add(@{sequence=1;kind='bootstrap.beforeSceneLoad'})
    $stream.Add(@{sequence=2;kind='snapshot';detail=($last|ConvertTo-Json -Depth 8 -Compress)})
    if ($scenario -eq 'failed_directive') { $stream.Add(@{sequence=3;kind='directive.Failed';detail='{"commandId":"Auto_probe"}'}) }
    $stream.Add(@{sequence=($stream.Count+1);kind='run.completed'})
    $stream | ForEach-Object { $_ | ConvertTo-Json -Compress } | Set-Content "$path/events.jsonl" -Encoding UTF8
    Edit-Result $path 'events' $stream.Count
    foreach ($stage in 'initial','final') { @{Version=1;Players=@()} | ConvertTo-Json | Set-Content "$path/warehouse-$stage.json" -Encoding UTF8 }
    @{schemaVersion=1;runId='probe';columns=6;rows=10;items=@()} | ConvertTo-Json | Set-Content "$path/item-definitions.json" -Encoding UTF8
    if ($scenario -eq 'runtime_error') { Edit-Result $path 'errors' 1 }
    if ($scenario -eq 'missing_warehouse') { Remove-Item -LiteralPath "$path/warehouse-final.json" }
    $report=Test-SceneRaidEvidence $path $autoConfig 0 $true
    $expected=if ($scenario -eq 'valid') {'EXPECTED_DEATH'} elseif ($scenario -eq 'missing_warehouse') {'NOT_FULL_RAID_VALIDATED'} else {'ISSUES_OBSERVED'}
    if ($report.evidenceStatus -ne 'PASS' -or $report.gameStatus -ne $expected) { throw "Death classification incorrect: $scenario / $($report|ConvertTo-Json -Depth 8)" }; $passed++
}
@{status='PASS';probes=$passed} | ConvertTo-Json | Set-Content "$root/result.json" -Encoding UTF8
Write-Output "PASS $passed report probes: $root"
