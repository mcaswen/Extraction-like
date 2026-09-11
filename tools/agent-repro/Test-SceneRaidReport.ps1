$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Report.psm1') -Force
$root = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path ('Logs/SceneRaidReportProbes/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$config = [pscustomobject]@{runId='probe';mode='Observe';scenePath='scene.unity';width=3840;height=2160;observeSeconds=60}
function Write-Fixture([string]$Name) {
    $path = Join-Path $root $Name
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    @{runId='probe';scene='scene.unity';status='AUDITED'} | ConvertTo-Json | Set-Content "$path/scene-audit.json" -Encoding UTF8
    @{schemaVersion=1;runId='probe';mode='Observe';scenePath='scene.unity';status='OBSERVED';frames=4;renderedFrames=4;batchMode=$false;
        screenWidth=3840;screenHeight=2160;cameraWidth=3840;cameraHeight=2160;lostEvents=0;elapsedWallSeconds=60;errors=0;warnings=0;
        events=2;observedAgents=@('1','2')} | ConvertTo-Json | Set-Content "$path/result.json" -Encoding UTF8
    @('{"sequence":1,"kind":"bootstrap.beforeSceneLoad"}','{"sequence":2,"kind":"run.completed"}') | Set-Content "$path/events.jsonl" -Encoding UTF8
    @('frame,intervalMs','1,-1','2,16','3,16','4,16') | Set-Content "$path/frames.csv" -Encoding UTF8
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
    @{name='truncated_duration';act={param($p) Edit-Result $p 'elapsedWallSeconds' 2}},
    @{name='missing_agent';act={param($p) Edit-Result $p 'observedAgents' @('1')}},
    @{name='event_gap';act={param($p) @('{"sequence":1,"kind":"bootstrap.beforeSceneLoad"}','{"sequence":3,"kind":"run.completed"}') | Set-Content "$p/events.jsonl"}},
    @{name='no_completion';act={param($p) '{"sequence":1,"kind":"bootstrap.beforeSceneLoad"}' | Set-Content "$p/events.jsonl"}},
    @{name='truncated_frames';act={param($p) 'frame,intervalMs' | Set-Content "$p/frames.csv"}}
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
@{status='PASS';probes=$passed} | ConvertTo-Json | Set-Content "$root/result.json" -Encoding UTF8
Write-Output "PASS $passed report probes: $root"
