param([string]$RunPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.RouteRecovery.Contracts.psm1') -Force
if ($RunPath) {
    $events = @(Get-Content -LiteralPath (Join-Path $RunPath 'events.jsonl') -Encoding UTF8 | ForEach-Object { $_ | ConvertFrom-Json })
    $proofs = @(Get-SceneRaidRecoveredRouteSteps $events)
    [pscustomobject]@{recoveredRouteFailures=$proofs.Count;recoveredRouteSteps=$proofs} | ConvertTo-Json -Depth 8
    exit 0
}
function Copy-Fixture($Value) { $Value | ConvertTo-Json -Depth 20 -Compress | ConvertFrom-Json }
function New-Fixture {
    $owner = [pscustomobject]@{agent='2';requestId='r';source='Autonomous';version=4;goal='b';currentNode='b';commandId='child-old';
        active=$true;dead=$false;stage='Accepted';stepPhase='Travelling';position=@{x=0;y=0;z=0}}
    $old = [pscustomobject]@{agent='2';requestId='r';source='Autonomous';version=4;goal='b';stage='Accepted';reason='None';replan=$false;snapshot=$owner}
    $plan = Copy-Fixture $old
    $plan.version=5; $plan.stage='Planning'; $plan.reason='NoProgress'; $plan.replan=$true; $plan.snapshot.stepPhase='Failed'
    $accept = Copy-Fixture $plan
    $accept.stage='Accepted'; $accept.reason='None'; $accept.snapshot.version=5; $accept.snapshot.commandId='child-new'; $accept.snapshot.stepPhase='Travelling'
    $terminal = Copy-Fixture $accept
    $terminal.stage='Completed'; $terminal.replan=$false; $terminal.snapshot.stage='Completed'; $terminal.snapshot.stepPhase='Completed'
    $terminal.snapshot.active=$false; $terminal.snapshot.position.x=3
    $child = @{agent='2';commandId='child-old';targetId='b';directive='MoveTo';stage='Failed';reason='NoProgress'}
    $script:fixtureSequence=0
    function Row($Kind,$Detail,$Wall) {
        $script:fixtureSequence++
        [pscustomobject]@{kind=$Kind;detail=$Detail;sequence=$script:fixtureSequence;wallSeconds=$Wall}
    }
    Copy-Fixture @((Row 'route.result' $old 1),(Row 'route.state' (Copy-Fixture $owner) 2),
      (Row 'directive.Failed' $child 3),(Row 'route.result' $plan 3.02),
      (Row 'route.result' $accept 3.03),(Row 'route.result' $terminal 6.1))
}
$checks = 0
function Check([string]$Name,$Rows,[int]$Expected) {
    $encoded = @($Rows | ForEach-Object { [pscustomobject]@{kind=$_.kind;sequence=$_.sequence;wallSeconds=$_.wallSeconds;
        detail=($_.detail | ConvertTo-Json -Depth 20 -Compress)} })
    $actual = @(Get-SceneRaidRecoveredRouteSteps $encoded)
    if ($actual.Count -ne $Expected) { throw "$Name expected $Expected recoveries, got $($actual.Count)." }
    if ($Expected -eq 1 -and ($actual[0].commandId -ne 'child-old' -or $actual[0].actualPlanarDisplacement -ne 3 -or
        $actual[0].oldVersion -ne 4 -or $actual[0].recoveredVersion -ne 5)) { throw "$Name returned incorrect proof." }
    $script:checks++
}
Check 'autonomous root completed with physical movement' (New-Fixture) 1
$r=New-Fixture;foreach($row in $r){if($row.kind -like 'route.*'){$row.detail.source='Player';if($row.detail.PSObject.Properties['snapshot']){$row.detail.snapshot.source='Player'}}}
Check 'player root uses identical recovery proof' $r 1
$r=New-Fixture;$r[5].detail.stage='Extracted';$r[5].detail.snapshot.stage='Extracted';Check 'same root extracted' $r 1
$r=New-Fixture;$r[5].detail.snapshot.position.x=0;Check 'terminal event without actual movement' $r 0
$r=New-Fixture;$r[5].detail.snapshot.position.x=[double]::NaN;Check 'nonfinite position' $r 0
$r=New-Fixture;$r[5].detail.snapshot.position=$null;Check 'missing position' $r 0
$r=New-Fixture;Check 'missing terminal' @($r[0..4]) 0
$r=New-Fixture;Check 'missing acceptance' @($r[0..3]+$r[5]) 0
$r=New-Fixture;Check 'missing planning' @($r[0..2]+$r[4..5]) 0
$r=New-Fixture;Check 'old root was never accepted' @($r[1..5]) 0
$r=New-Fixture;$r[1].detail.commandId='unowned';Check 'command not owned at failure' $r 0
$r=New-Fixture;$r[2].detail.targetId='other';Check 'wrong target' $r 0
$r=New-Fixture;$r[2].detail.directive='Engage';Check 'non movement failure' $r 0
$r=New-Fixture;$r[2].detail.reason='Unreachable';Check 'other failure reason' $r 0
$r=New-Fixture;$r[2].kind='directive.Rejected';Check 'rejected child' $r 0
$r=New-Fixture;$r[3].detail.snapshot.commandId='other';Check 'planning owns a different child' $r 0
$r=New-Fixture;$r[3].detail.snapshot.stepPhase='Travelling';Check 'planning lacks failed child fact' $r 0
$r=New-Fixture;$r[3].detail.reason='StaleContext';Check 'unrelated replan reason' $r 0
$r=New-Fixture;$r[3].detail.version=7;Check 'skipped version' $r 0
$r=New-Fixture;$r[3].wallSeconds=4.01;$r[4].wallSeconds=4.02;Check 'delayed unrelated planning' $r 0
$r=New-Fixture;$r[4].wallSeconds=11.03;$r[5].wallSeconds=12;Check 'acceptance exceeds finite planning budget' $r 0
$r=New-Fixture;$r[4].wallSeconds=[double]::PositiveInfinity;Check 'nonfinite time' $r 0
$r=New-Fixture;$r[4].sequence=3;Check 'out of order sequence' $r 0
$r=New-Fixture;$r[4].detail.replan=$false;Check 'new route instead of replan' $r 0
foreach($field in @('agent','requestId','source','goal','version')) {
    $r=New-Fixture;$r[4].detail.$field=if($field -eq 'version'){6}else{'other'};Check "accepted wrong $field" $r 0
    $r=New-Fixture;$r[5].detail.$field=if($field -eq 'version'){6}else{'other'};Check "completed wrong $field" $r 0
}
foreach($stage in @('Failed','Cancelled','Dead','Rejected')) {
    $r=New-Fixture;$r[5].detail.stage=$stage;$r[5].detail.snapshot.stage=$stage;Check "root terminal $stage" $r 0
}
$r=New-Fixture;$r[5].detail.snapshot.dead=$true;Check 'dead snapshot with success label' $r 0
$r=New-Fixture;$r[5].detail.snapshot.active=$true;Check 'active snapshot with success label' $r 0
$r=New-Fixture;$duplicate=Copy-Fixture $r[2];$duplicate.sequence=7;$duplicate.wallSeconds=7
Check 'duplicate failure never counted twice' @($r+$duplicate) 0
$r=New-Fixture;$extra=Copy-Fixture $r[5];$extra.sequence=7;$extra.wallSeconds=7;$extra.detail.stage='Failed'
Check 'later same root failure invalidates proof' @($r+$extra) 0
$r=New-Fixture;$r[1].detail.dead=$true;Check 'dead owner' $r 0
Write-Output "SceneRaid route recovery contracts: $checks checks passed."
