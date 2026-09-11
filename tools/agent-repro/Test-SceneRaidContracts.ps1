$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Contracts.psm1') -Force
$root=Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path ('Logs/SceneRaidContractProbes/'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
function Write-Json($Path,$Value) { $Value | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $Path -Encoding UTF8 }
function New-Fixture([string]$Name, [switch]$EmptySource) {
    $path=Join-Path $root $Name
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    $row=@{itemId='probe';type='Junk';parentIndex=-1;amount=1;width=1;height=1}
    $catalog=@{schemaVersion=1;runId='probe';columns=6;rows=10;items=@($row)}
    $empty=@{Version=1;Players=@()}
    $final=@{Version=1;Players=@(@{AgentId='default_player';Pages=@(@{PageIndex=0;CellStates=@();Items=@(
        @{ItemID='probe';Amount=1;X=0;Y=0;IsRotated=$false},@{ItemID='probe';Amount=1;X=1;Y=0;IsRotated=$false})})})}
    Write-Json "$path/warehouse-initial.json" $empty
    Write-Json "$path/warehouse-final.json" $final
    Write-Json "$path/item-definitions.json" $catalog
    $events=[Collections.Generic.List[object]]::new()
    foreach ($phase in 0,1,2) {
        $agents=@(foreach ($id in '1','2') { @{id=$id;position=@{x=($phase*2);z=0};hasEnemy=$true;enemy=@{identity='enemy';health=(100-$phase*20)}} })
        $snapshot=@{agents=$agents;inventoryOpen=($phase -eq 1);timeScale=$(if($phase -eq 1){0}else{1})}
        $events.Add([pscustomobject]@{kind='snapshot';detail=($snapshot|ConvertTo-Json -Depth 10 -Compress)})
    }
    foreach ($id in '1','2') {
        foreach ($stage in 'opened','transferred','beforeClose','afterClose') {
            $source=@($row); $backpack=@($row)
            if ($stage -eq 'opened') { $source=@($row,$row); $backpack=@() }
            if ($EmptySource) {
                if ($stage -eq 'opened') { $source=@($row) }
                else { $source=@() }
            }
            $entry=@{schemaVersion=2;agent=$id;resource='box';stage=$stage;equipmentCaptured=$true;equipped=@();source=$source;backpack=$backpack}
            $events.Add([pscustomobject]@{kind='inventory.ledger';detail=($entry|ConvertTo-Json -Depth 10 -Compress)})
        }
        foreach ($kind in 'focus','opened','transferred','closed') {
            $events.Add([pscustomobject]@{kind="inventory.$kind";detail=(@{agent=$id;commandId='auto';resource='box';reason='capacity_requires_extraction'}|ConvertTo-Json -Compress)})
        }
    }
    $last=@{requiredAgents=@('1','2');extractedAgents=@('1','2');settledAgents=@('1','2');missionCompleted=$true;missionFailed=$false;agents=@();inventoryOpen=$false;timeScale=0}
    $events.Add([pscustomobject]@{kind='snapshot';detail=($last|ConvertTo-Json -Depth 10 -Compress)})
    return @{path=$path;events=$events;config=[pscustomobject]@{runId='probe';simulationSpeed=1};result=[pscustomobject]@{status='RAID_OBSERVED_COMPLETE'};final=$final;catalog=$catalog}
}
function Check($Fixture) { Test-SceneRaidCompletion $Fixture.path $Fixture.config $Fixture.events.ToArray() $Fixture.result }
$passed=0
$f=New-Fixture 'valid'
$result=Check $f
if ($result.status -ne 'PASS') { throw ($result|ConvertTo-Json -Depth 8) }; $passed++
$f=New-Fixture 'valid_empty_box' -EmptySource
$result=Check $f
if ($result.status -ne 'PASS') { throw ($result|ConvertTo-Json -Depth 8) }; $passed++
foreach ($kind in 'closed','canceled') {
    $f=New-Fixture "valid_focus_cancel_$kind"
    $detail=@{agent='2';commandId='auto';resource='box';reason='interaction_invalidated'}|ConvertTo-Json -Compress
    $f.events.Insert(0,[pscustomobject]@{kind="inventory.$kind";detail=$detail})
    $f.events.Insert(0,[pscustomobject]@{kind='inventory.focus';detail=$detail})
    $result=Check $f
    if ($result.status -ne 'PASS') { throw ($result|ConvertTo-Json -Depth 8) }; $passed++
}
$faults=@('missing','wrongRun','loss','duplication','overlap','outOfBounds','unknown','zeroAmount','wrongOwner','blockedCell','missingAgent','falseSuccess','noDamage','noMovement','noPause','noTransfer','noOpened','noAfterClose','writebackChanged','noEquipment','badParent','manual','orphanClose','wrongCloseIdentity','noClose','duplicateOpen','cancelWhileOpen')
foreach ($fault in $faults) {
    $f=New-Fixture $fault
    switch ($fault) {
        missing { Remove-Item -LiteralPath "$($f.path)/warehouse-final.json" }
        wrongRun { $f.catalog.runId='other'; Write-Json "$($f.path)/item-definitions.json" $f.catalog }
        loss { $f.final.Players[0].Pages[0].Items=@($f.final.Players[0].Pages[0].Items[0]) }
        duplication { $f.final.Players[0].Pages[0].Items[0].Amount=2 }
        overlap { $f.final.Players[0].Pages[0].Items[1].X=0 }
        outOfBounds { $f.final.Players[0].Pages[0].Items[1].X=6 }
        unknown { $f.final.Players[0].Pages[0].Items[0].ItemID='unknown' }
        zeroAmount { $f.final.Players[0].Pages[0].Items[0].Amount=0 }
        wrongOwner { $f.final.Players[0].AgentId='other_agent' }
        blockedCell { $f.final.Players[0].Pages[0].CellStates=@(@{X=0;Y=0;State=3}) }
        missingAgent { $last=$f.events[-1].detail|ConvertFrom-Json; $last.settledAgents=@('1'); $f.events[-1].detail=$last|ConvertTo-Json -Depth 8 -Compress }
        falseSuccess { $last=$f.events[-1].detail|ConvertFrom-Json; $last.missionFailed=$true; $f.events[-1].detail=$last|ConvertTo-Json -Depth 8 -Compress }
        noDamage { for($i=0;$i -lt 3;$i++) {$s=$f.events[$i].detail|ConvertFrom-Json;foreach($a in $s.agents){$a.enemy.health=100};$f.events[$i].detail=$s|ConvertTo-Json -Depth 8 -Compress} }
        noMovement { for($i=0;$i -lt 3;$i++) {$s=$f.events[$i].detail|ConvertFrom-Json;foreach($a in $s.agents){$a.position.x=0};$f.events[$i].detail=$s|ConvertTo-Json -Depth 8 -Compress} }
        noPause { $s=$f.events[1].detail|ConvertFrom-Json;$s.timeScale=1;$f.events[1].detail=$s|ConvertTo-Json -Depth 8 -Compress }
        noTransfer { [void]$f.events.RemoveAll([Predicate[object]]{param($e) $e.kind -eq 'inventory.transferred'}) }
        noOpened { [void]$f.events.RemoveAll([Predicate[object]]{param($e) $e.kind -eq 'inventory.opened'}) }
        orphanClose { $f.events.Add([pscustomobject]@{kind='inventory.closed';detail='{"agent":"2","commandId":"auto","resource":"box","reason":"interaction_invalidated"}'}) }
        wrongCloseIdentity { $e=$f.events|Where-Object kind -eq 'inventory.closed'|Select-Object -First 1;$d=$e.detail|ConvertFrom-Json;$d.resource='wrong';$e.detail=$d|ConvertTo-Json -Compress }
        noClose { [void]$f.events.RemoveAll([Predicate[object]]{param($e) $e.kind -eq 'inventory.closed'}) }
        duplicateOpen { $i=$f.events.FindIndex([Predicate[object]]{param($e) $e.kind -eq 'inventory.opened'});$f.events.Insert($i,$f.events[$i]) }
        cancelWhileOpen { $e=$f.events|Where-Object kind -eq 'inventory.closed'|Select-Object -First 1;$e.kind='inventory.canceled' }
        noAfterClose { [void]$f.events.RemoveAll([Predicate[object]]{param($e) $e.kind -eq 'inventory.ledger' -and ($e.detail|ConvertFrom-Json).stage -eq 'afterClose'}) }
        writebackChanged { $e=$f.events[6].detail|ConvertFrom-Json;$e.source[0].amount=2;$f.events[6].detail=$e|ConvertTo-Json -Depth 8 -Compress }
        noEquipment { foreach($e in $f.events){if($e.kind -eq 'inventory.ledger'){$d=$e.detail|ConvertFrom-Json;$d.equipmentCaptured=$false;$e.detail=$d|ConvertTo-Json -Depth 8 -Compress}} }
        badParent { $e=$f.events[6].detail|ConvertFrom-Json;$e.backpack[0].parentIndex=8;$f.events[6].detail=$e|ConvertTo-Json -Depth 8 -Compress }
        manual { $f.events.Add([pscustomobject]@{kind='directive.Accepted';detail='{"commandId":"ManualTargetClick_probe"}'}) }
    }
    if ($fault -in @('loss','duplication','overlap','outOfBounds','unknown','zeroAmount','wrongOwner','blockedCell')) { Write-Json "$($f.path)/warehouse-final.json" $f.final }
    $result=Check $f
    Write-Json "$($f.path)/contracts.json" $result
    if ($result.status -eq 'PASS') { throw "Invalid completion accepted: $fault" }
    $passed++
}
function New-DeathFixture([string]$Name, [switch]$Both, [switch]$BeforeInventory) {
    $fixture=New-Fixture $Name
    $fixture.result.status='RAID_OBSERVED_FAILURE'
    $last=$fixture.events[-1].detail | ConvertFrom-Json
    $last.missionCompleted=$false; $last.missionFailed=$true
    $last.extractedAgents=if ($Both) {@()} else {@('2')}
    $last.settledAgents=@($last.extractedAgents)
    $deadIds=if ($Both) {@('1','2')} else {@('1')}
    $last.agents=@(foreach ($id in $deadIds) { @{id=$id;health=0;position=@{x=6;z=0};hasEnemy=$false} })
    $fixture.events[-1].detail=$last | ConvertTo-Json -Depth 10 -Compress
    if ($Both) { $fixture.final.Players=@() }
    else { $fixture.final.Players[0].Pages[0].Items=@($fixture.final.Players[0].Pages[0].Items[1]) }
    Write-Json "$($fixture.path)/warehouse-final.json" $fixture.final
    if ($BeforeInventory) {
        $terminal=$fixture.events[-1]
        $fixture.events.Clear(); $fixture.events.Add($terminal)
    }
    return $fixture
}
foreach ($scenario in 'one_death','both_death','early_both_death') {
    $f=New-DeathFixture $scenario -Both:($scenario -ne 'one_death') -BeforeInventory:($scenario -eq 'early_both_death')
    $result=Check $f
    Write-Json "$($f.path)/contracts.json" $result
    if ($result.status -ne 'PASS' -or $result.outcome -ne 'EXPECTED_DEATH') { throw ($result|ConvertTo-Json -Depth 8) }; $passed++
}
foreach ($fault in 'alive','missing_dead','duplicate_dead','dead_also_extracted','settlement_missing','false_failed_flag','death_loot_saved','survivor_loot_lost','unclosed_session','missing_ledger') {
    $f=New-DeathFixture "death_$fault"
    $last=$f.events[-1].detail | ConvertFrom-Json
    switch ($fault) {
        alive { $last.agents[0].health=1 }
        missing_dead { $last.agents=@() }
        duplicate_dead { $last.agents=@($last.agents[0],$last.agents[0]) }
        dead_also_extracted { $last.extractedAgents=@('1','2'); $last.settledAgents=@('1','2') }
        settlement_missing { $last.settledAgents=@() }
        false_failed_flag { $last.missionFailed=$false }
        death_loot_saved { $f.final.Players[0].Pages[0].Items[0].Amount=2 }
        survivor_loot_lost { $f.final.Players=@() }
        unclosed_session { [void]$f.events.RemoveAll([Predicate[object]]{param($e) $e.kind -eq 'inventory.closed'}) }
        missing_ledger { [void]$f.events.RemoveAll([Predicate[object]]{param($e) $e.kind -eq 'inventory.ledger'}) }
    }
    $f.events[-1].detail=$last | ConvertTo-Json -Depth 10 -Compress
    Write-Json "$($f.path)/warehouse-final.json" $f.final
    $result=Check $f
    Write-Json "$($f.path)/contracts.json" $result
    if ($result.status -ne 'FAIL') { throw "Invalid death accepted: $fault" }; $passed++
}
Write-Json "$root/result.json" @{status='PASS';probes=$passed}
Write-Output "PASS $passed completion contract probes: $root"
