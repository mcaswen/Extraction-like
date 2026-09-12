param([string]$RunPath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Routes.Contracts.psm1') -Force
if ($RunPath) {
    $config=Get-Content -LiteralPath (Join-Path $RunPath 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $events=@(Get-Content -LiteralPath (Join-Path $RunPath 'events.jsonl') -Encoding UTF8 | ForEach-Object { $_ | ConvertFrom-Json })
    $result=Test-SceneRaidRoutes $events $config.mode
    $result | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $RunPath 'route-contracts.json') -Encoding UTF8
    $result | Select-Object status,failures,coverage | ConvertTo-Json -Depth 5
    if ($result.status -ne 'PASS') { exit 1 };exit 0
}
function Event([string]$Kind,$Detail) { [pscustomobject]@{kind=$Kind;detail=($Detail | ConvertTo-Json -Depth 20 -Compress)} }
function Copy-RouteFixture($Value) { $Value | ConvertTo-Json -Depth 20 -Compress | ConvertFrom-Json }
$graph=@{graphId='test';revision=1;nodes=@(@{id='a'},@{id='b'},@{id='c'});edges=@(
    @{id='ab';from='a';to='b';a=@{x=0;y=0};b=@{x=10;y=0}},@{id='bc';from='b';to='c';a=@{x=10;y=0};b=@{x=10;y=10}})}
$root=@{agent='1';requestId='r1';source='Autonomous';version=1;graphRevision=1;installed=$true;hasRoute=$true;active=$true;
    stage='Accepted';stepPhase='Travelling';retaliating=$false;dead=$false;nodes=@('a','b','c');goal='c';cursor=1;currentNode='b';entryFrom=''}
$result=@{agent='1';requestId='r1';source='Autonomous';version=1;stage='Accepted';reason='None'}
$display=@{agent='1';requestId='r1';version=1;cursor=1;mode='Travelling';remaining=@('b','c');onEdge=$true;hasMarker=$true;markerMembers=1;
    markerPosition=@{x=5;y=0};fromPort=@{x=0;y=0};toPort=@{x=10;y=0};progress=.5;validDistance=$true;baselineProgress=0;baselineDistance=10;remainingDistance=5.5;tolerance=1}
function Matrix($Root=$root,$Display=$display,$Result=$result,$Graph=$graph) {
    @((Event 'route.graph' $Graph),(Event 'route.result' $Result),(Event 'route.state' $Root),
      (Event 'route.frame' @{roots=@($Root);displays=@($Display)}))
}
$checks=0
function Check([string]$Name,$Events,[string]$Expected) {
    $actual=Test-SceneRaidRoutes $Events -ExpectedAgents @('1')
    if ($actual.status -ne $Expected) { throw "$Name expected $Expected, got $($actual.status): $($actual.failures -join ',')" }
    $script:checks++
}
Check 'valid actual-distance sample' (Matrix) 'PASS'
$bad=Copy-RouteFixture $display;$bad.markerPosition.y=1;Check 'off-line marker' (Matrix -Display $bad) 'FAIL'
$bad=Copy-RouteFixture $display;$bad.remainingDistance=8;Check 'false distance progress' (Matrix -Display $bad) 'FAIL'
$bad=Copy-RouteFixture $display;$bad.remaining=@('c');Check 'UI skipping actual step' (Matrix -Display $bad) 'FAIL'
$bad=Copy-RouteFixture $root;$bad.nodes=@('a','c');$bad.currentNode='c';Check 'unauthored graph shortcut' (Matrix -Root $bad) 'FAIL'
$bad=Copy-RouteFixture $root;$bad.currentNode='a';Check 'cursor mismatch' (Matrix -Root $bad) 'FAIL'
$bad=Copy-RouteFixture $root;$bad.goal='b';Check 'wrong endpoint' (Matrix -Root $bad) 'FAIL'
$bad=Copy-RouteFixture $result;$bad.source='Player';Check 'manual order in autonomous run' (Matrix -Result $bad) 'FAIL'
$bad=Copy-RouteFixture $result;$bad.stage='Failed';$bad.reason='NoProgress';Check 'real root failure' (Matrix -Result $bad) 'FAIL'
$bad=Copy-RouteFixture $root;$bad.entryFrom='c';Check 'invalid incoming edge' (Matrix -Root $bad) 'FAIL'
$bad=Copy-RouteFixture $graph;$bad.edges[0].b.y=2;Check 'diagonal authored edge' (Matrix -Graph $bad) 'FAIL'
Check 'missing accepted event' @((Matrix) | Where-Object kind -ne 'route.result') 'FAIL'
Check 'missing display evidence' @((Matrix) | Where-Object kind -ne 'route.frame') 'FAIL'
$bad=Copy-RouteFixture $root;$bad.cursor=0;$bad.currentNode='a';Check 'reverse cursor same version' (@(Matrix)+(Event 'route.state' $bad)) 'FAIL'
$bad=Copy-RouteFixture $root;$bad.retaliating=$true;Check 'retaliation keeps identity' (@(Matrix)+(Event 'route.state' $bad)) 'PASS'
$bad=Copy-RouteFixture $root;$bad.stage='Dead';$bad.active=$false;$bad.dead=$true;Check 'expected death is not execution bug' (@(Matrix)+(Event 'route.state' $bad)) 'PASS'
$terminal=Copy-RouteFixture $root;$terminal.stage='Completed';$terminal.active=$false
$lag=Event 'route.frame' @{roots=@($terminal);displays=@($display)}
$lag | Add-Member wallSeconds 10.0
Check 'one throttled terminal transition' (@(Matrix)+$lag) 'PASS'
$late=Copy-RouteFixture $lag;$late.wallSeconds=10.5
Check 'persistent stale display' (@(Matrix)+$lag+$late) 'FAIL'
Write-Output "SceneRaid route contracts: $checks checks passed."
