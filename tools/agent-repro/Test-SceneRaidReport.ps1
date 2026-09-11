$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Report.psm1') -Force
$root = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path ('Logs/SceneRaidReportProbes/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$config = [pscustomobject]@{runId='probe';mode='Observe';scenePath='scene.unity';width=3840;height=2160;observeSeconds=0.058}
$fixtureCounters = @('PlayerLoop','GC Allocated In Frame','Anomaly.Discovery.Update','Anomaly.Pawn.Update','Anomaly.Zone.Update',
    'Anomaly.Discovery.ScanAgent','Anomaly.Pawn.Facts','Anomaly.Pawn.Lifecycle','Anomaly.Pawn.Brain',
    'Anomaly.Zone.State','Anomaly.Zone.Shape','Anomaly.Navigation.Check')
function Write-Fixture([string]$Name) {
    $path = Join-Path $root $Name
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    @{runId='probe';scene='scene.unity';status='AUDITED'} | ConvertTo-Json | Set-Content "$path/scene-audit.json" -Encoding UTF8
    @{schemaVersion=1;runId='probe';mode='Observe';scenePath='scene.unity';status='OBSERVED';frames=4;renderedFrames=4;batchMode=$false;
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
$faults = @(
    @{name='missing_result';act={param($p) Remove-Item -LiteralPath "$p/result.json"}},
    @{name='invalid_json';act={param($p) '{' | Set-Content "$p/result.json"}},
    @{name='wrong_run';act={param($p) Edit-Result $p 'runId' 'wrong'}},
    @{name='wrong_mode';act={param($p) Edit-Result $p 'mode' 'AutonomousRaid'}},
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
if ((Get-SceneRaidFrameStatistics @(New-FrameSeries 5 100)).thresholdsMet) { throw 'Averaging hid 40 ms spike.' }; $passed++
if ((Get-SceneRaidFrameStatistics @(New-FrameSeries 12.5)).thresholdsMet) { throw '80 FPS accepted.' }; $passed++
@{status='PASS';probes=$passed} | ConvertTo-Json | Set-Content "$root/result.json" -Encoding UTF8
Write-Output "PASS $passed report probes: $root"
