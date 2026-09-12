param([string]$RunPath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Routes.Contracts.psm1') -Force
if ($RunPath) {
    $config=Get-Content -LiteralPath (Join-Path $RunPath 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $events=@(Get-Content -LiteralPath (Join-Path $RunPath 'events.jsonl') -Encoding UTF8 | ForEach-Object { $_ | ConvertFrom-Json })
    $result=Test-SceneRaidRoutes $events $config.mode
    if($config.mode -eq 'ManualRoutes') {
        $scriptResult=Test-SceneRaidRouteScript $events ($config.scenarioJson | ConvertFrom-Json)
        $result | Add-Member script $scriptResult
        if($scriptResult.status -ne 'PASS'){$result.status='FAIL';$result.failures+=@($scriptResult.failures)}
    }
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

# 新路线脚本的独立正反例，事件事实和候选数值手工构造，不调用生产选择器。
$scenario=[pscustomobject]@{schemaVersion=1;id='MR02';agent='2';nearMinimum=10;farMinimum=150;movementBeforeReplacement=3;wallDeadline=60;operations=@('Near','FarCrossZone','InvalidPreserves')}
$near=@{node='a';zone='near';kind='Resource';reachable=$true;distance=12}
$far=@{node='c';zone='far';kind='Resource';reachable=$true;distance=180}
$nearRoot=@{agent='2';requestId='near';source='Player';version=1;active=$true;retaliating=$false;nodes=@('a');cursor=0;position=@{x=0;y=0;z=0}}
$farRoot=@{agent='2';requestId='far';source='Player';version=2;active=$true;retaliating=$false;nodes=@('a','b','c');cursor=0;position=@{x=3;y=0;z=0}}
$s1=@{scenario='MR02';agent='2';operation='Near';ordinal=0;uiCommandsBefore=0;uiCommandsAfter=1;node='a';zone='near';selected=$near;requestId='near';stage='Planning';reason='None';before=$nearRoot;after=$nearRoot}
$s2=@{scenario='MR02';agent='2';operation='FarCrossZone';ordinal=1;uiCommandsBefore=1;uiCommandsAfter=2;node='c';zone='far';selected=$far;requestId='far';stage='Planning';reason='None';before=$nearRoot;after=$farRoot}
$s3=@{scenario='MR02';agent='2';operation='InvalidPreserves';ordinal=2;uiCommandsBefore=2;uiCommandsAfter=3;node='missing';zone='';requestId='invalid';stage='Rejected';reason='MissingTarget';before=$farRoot;after=$farRoot}
$farMoving=Copy-RouteFixture $farRoot;$farMoving.position.x=10;$farMoving.cursor=1
function Script-Matrix($Third=$s3,$FarSubmission=$s2,$Moving=$farMoving) {
    @((Event 'routeScript.candidates' @{operation='Near';candidates=@($near,$far)}),
      (Event 'routeScript.candidates' @{operation='FarCrossZone';candidates=@($near,$far)}),
      (Event 'routeScript.submitted' $s1),
      (Event 'route.result' @{agent='2';requestId='near';stage='Accepted';source='Player'}),
      (Event 'routeScript.progress' @{requestId='near';root=$nearRoot;submittedPosition=@{x=0;y=0;z=0};currentPosition=@{x=3;y=0;z=0};distance=3}),
      (Event 'route.result' @{agent='2';requestId='near';stage='Cancelled';source='Player'}),
      (Event 'routeScript.submitted' $FarSubmission),
      (Event 'route.result' @{agent='2';requestId='far';stage='Accepted';source='Player'}),
      (Event 'routeScript.submitted' $Third),
      (Event 'routeScript.finished' @{status='SUBMITTED';submitted=3}),
      (Event 'route.state' @{agent='1';source='Autonomous';active=$true}),
      (Event 'route.frame' @{roots=@($Moving)}))
}
$scriptChecks=0
function Check-Script([string]$Name,$Events,[string]$Expected) {
    $actual=Test-SceneRaidRouteScript $Events $scenario
    if($actual.status -ne $Expected){throw "$Name expected $Expected, got $($actual.status): $($actual.failures -join ',')"};$script:scriptChecks++
}
Check-Script 'near far rejection and real movement' (Script-Matrix) 'PASS'
Check-Script 'unfinished finite script' @((Script-Matrix) | Where-Object kind -ne 'routeScript.finished') 'PARTIAL'
Check-Script 'missing near movement' @((Script-Matrix) | Where-Object kind -ne 'routeScript.progress') 'FAIL'
Check-Script 'missing accepted roots' @((Script-Matrix) | Where-Object kind -ne 'route.result') 'FAIL'
$bad=Copy-RouteFixture $s3;$bad.after.requestId='automatic';Check-Script 'rejected root discarded player request' (Script-Matrix -Third $bad) 'FAIL'
$bad=Copy-RouteFixture $s3;$bad.uiCommandsAfter=5;Check-Script 'handler issued duplicate commands' (Script-Matrix -Third $bad) 'FAIL'
$bad=Copy-RouteFixture $s2;$bad.node='a';Check-Script 'wrong far candidate' (Script-Matrix -FarSubmission $bad) 'FAIL'
$bad=Copy-RouteFixture $farMoving;$bad.position.x=3;Check-Script 'far never moved' (Script-Matrix -Moving $bad) 'FAIL'
Check-Script 'automatic request replaced live player root' (@(Script-Matrix)+(Event 'route.result' @{agent='2';requestId='auto';stage='Accepted';source='Autonomous'})) 'FAIL'
Check-Script 'other agent manually commanded' (@(Script-Matrix)+(Event 'route.state' @{agent='1';requestId='other';version=1;source='Player';active=$true;retaliating=$false})) 'FAIL'
$retaliation=Copy-RouteFixture $farRoot;$retaliation.retaliating=$true
Check-Script 'same player root resumes after retaliation' (@(Script-Matrix)+(Event 'route.state' $retaliation)+(Event 'route.state' $farRoot)) 'PASS'
Write-Output "SceneRaid route script contracts: $scriptChecks checks passed."
