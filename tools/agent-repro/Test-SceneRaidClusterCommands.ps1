$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.ClusterCommands.Contracts.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.CommandConfig.psm1') -Force
$root=Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path ('Logs/SceneRaidCommandProbes/'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $root -Force|Out-Null
function Copy-Value($Value) { return $Value|ConvertTo-Json -Depth 32 -Compress|ConvertFrom-Json }
function Save-Json($Path,$Value) { [IO.File]::WriteAllText($Path,($Value|ConvertTo-Json -Depth 32 -Compress)) }
function Add-Event($Fixture,$Kind,$Value,[int]$Frame=3) {
    $Fixture.events.Add([pscustomobject]@{sequence=$Fixture.events.Count+1;frame=$Frame;gameSeconds=[double]$Frame;kind=$Kind;detail=($Value|ConvertTo-Json -Depth 32 -Compress)})
}
function New-CommandFixture([switch]$Rejected) {
    $script=(Get-SceneRaidCommandScenario 'MC01-X').scenario
    $script.steps=@($script.steps[0]); $script.steps[0].id='step'; $script.id='probe'
    $spec=$script.steps[0]; $spec.target.distance='Near'
    if($Rejected){$spec.accepted=$false;$spec.reason='Unreachable'}
    $actors=@(foreach($id in '1','2'){@{id=$id;position=@{x=0;y=0;z=0};commandId='auto'+$id;suspendedCommand='';hasPath=$true;health=100;hasEnemy=$false}})
    $before=@{agents=$actors;inventoryOpen=$false;inventoryAgent='';timeScale=4}
    $after=Copy-Value $before
    $cmd=if($Rejected){''}else{'ManualTargetClick_probe'}
    if(!$Rejected){$after.agents[0].commandId=$cmd}
    $selection=@{schemaVersion=1;frame=3;focusedAgent='1';scene='scene';agents=@(@{id='1';position=@{x=0;y=0;z=0};discoveryRange=200;interactionRange=1});clusters=@(
        @{identity='cluster';targetId='cluster-id';kind='Extraction';active=$true;completed=$false;members=@(
            @{identity='point';position=@{x=10;y=0;z=0};active=$true;completed=$false;approaches=@(@{agent='1';distancePlanar=10;distanceClass='Near'})})})}
    $stage=if($Rejected){'Rejected'}else{'Accepted'}
    $data=@{agent=$(if($Rejected){''}else{'1'});commandId=$cmd;targetId=$(if($Rejected){''}else{'cluster-id'});target=$(if($Rejected){''}else{'point'});stage=$stage;reason=$spec.reason}
    $feedback=@{eventSequence=1;attemptId='probe:step';agent=$data.agent;commandId=$cmd;targetId=$data.targetId;target=$data.target;stage=$stage;reason=$spec.reason}
    $attempt=@{schemaVersion=1;scenarioId='probe';stepId='step';attemptId='probe:step';requestedAgent='1';route='Explicit';cluster='cluster';
        commandId=$cmd;resolvedAgent=$data.agent;target=$data.target;targetId=$data.targetId;directive='Extract';accepted=(!$Rejected);outcomeSequence=1;
        focusBefore='1';focusAfter='1';frame=3;gameSeconds=1;wallSeconds=1;before=$before;after=$after;selection=$selection;synchronousFeedback=@($feedback)}
    $f=@{scenario=$script;attempts=@((Copy-Value $attempt));steps=(Copy-Value @{schemaVersion=1;scenarioId='probe';stopped=$true;steps=@(@{stepId='step';status='SUBMITTED';reason='';attemptId='probe:step';cluster='cluster'})});
        events=[Collections.Generic.List[object]]::new();result=[pscustomobject]@{status='RAID_OBSERVED_COMPLETE'}}
    Add-Event $f ('directive.'+$stage) $data
    Add-Event $f 'command.feedback' $feedback
    if(!$Rejected){
        Add-Event $f 'command.progress' @{commandId=$cmd;attemptId='probe:step';agent='1';activeCommand=$cmd;position=@{x=0;y=0;z=0};enemyHealth=-1}
        Add-Event $f 'command.progress' @{commandId=$cmd;attemptId='probe:step';agent='1';activeCommand=$cmd;position=@{x=10;y=0;z=0};enemyHealth=-1} 4
        $data.stage='Completed'; Add-Event $f 'directive.Completed' $data 5
        $feedback.stage='Completed';$feedback.eventSequence=5;Add-Event $f 'command.feedback' $feedback 5
    }
    Add-Event $f 'snapshot' @{requiredAgents=@('1','2');extractedAgents=@('1','2');settledAgents=@('1','2');missionCompleted=$true;missionFailed=$false;agents=@();inventoryOpen=$false;timeScale=0} 6
    return $f
}
function Trace($F) { Test-SceneRaidClusterCommandTrace $F.scenario $F.attempts $F.steps $F.events.ToArray() $F.result }
function Change-Event($F,$Index,[scriptblock]$Mutation) { $value=$F.events[$Index].detail|ConvertFrom-Json; & $Mutation $value; $F.events[$Index].detail=$value|ConvertTo-Json -Depth 32 -Compress }
$results=[Collections.Generic.List[object]]::new()
function Assert-Result([string]$Name,$Result,[bool]$Pass) {
    Save-Json (Join-Path $root ($Name+'.json')) $Result
    if (($Result.status -eq 'PASS') -ne $Pass) { throw "$Name unexpected: $($Result|ConvertTo-Json -Depth 16 -Compress)" }
    $results.Add(@{name=$Name;passed=$true})
}
Assert-Result 'valid_trace' (Trace (New-CommandFixture)) $true
$negative=Trace (New-CommandFixture -Rejected)
Assert-Result 'expected_empty_id_rejection' $negative $true
if($negative.behaviorFailures -ne 1 -or $negative.expectedRejections -ne 1 -or $negative.unexpectedBehaviorFailures -ne 0){throw 'Raw rejection count was suppressed.'}
foreach($fault in @('wrong_reason','wrong_agent','wrong_target','unrelated_mutation','wrong_distance','wrong_selection','wrong_sync','missing_sync','missing_async','wrong_async',
    'extra_rejection','extra_failed','duplicate_attempt','duplicate_step','only_accepted','wrong_progress','no_progress','no_action','arbitrary_cancel','gate_not_met','unowned_command','wrong_step_order')) {
    $f=New-CommandFixture
    switch($fault){
        wrong_reason {Change-Event $f 0 {param($v)$v.reason='TargetCompleted'}}
        wrong_agent {$f.attempts[0].resolvedAgent='2'}
        wrong_target {$f.attempts[0].target='other'}
        unrelated_mutation {$f.attempts[0].after.agents[1].commandId='changed'}
        wrong_distance {$f.scenario.steps[0].target.distance='Far'}
        wrong_selection {$other=Copy-Value $f.attempts[0].selection.clusters[0];$other.identity='nearer';$other.members[0].position.x=5;$f.attempts[0].selection.clusters+=@($other)}
        wrong_sync {$f.attempts[0].synchronousFeedback[0].eventSequence=99}
        missing_sync {$f.attempts[0].synchronousFeedback=@()}
        missing_async {$f.events.RemoveAt(5)}
        wrong_async {Change-Event $f 5 {param($v)$v.attemptId='another'}}
        extra_rejection {Add-Event $f 'directive.Rejected' @{commandId='';agent='';stage='Rejected';reason='InvalidTarget'}}
        extra_failed {Add-Event $f 'directive.Failed' @{commandId='Auto';agent='2';stage='Failed';reason='Unreachable'}}
        duplicate_attempt {$f.attempts+=@($f.attempts[0])}
        duplicate_step {$f.steps.steps+=@($f.steps.steps[0])}
        only_accepted {$f.events.RemoveAt(5);$f.events.RemoveAt(4);Change-Event $f ($f.events.Count-1) {param($v)$v.settledAgents=@();$v.extractedAgents=@();$v.missionCompleted=$false}}
        wrong_progress {Change-Event $f 3 {param($v)$v.agent='2'}}
        no_progress {$f.events.RemoveAt(3);$f.events.RemoveAt(2)}
        no_action {Change-Event $f 3 {param($v)$v.position.x=0}}
        arbitrary_cancel {$f.events[4].kind='directive.Cancelled';Change-Event $f 4 {param($v)$v.stage='Cancelled';$v.reason='Superseded'};Change-Event $f 5 {param($v)$v.stage='Cancelled';$v.reason='Superseded'}}
        gate_not_met {$f.scenario.steps[0].gate.kind='Retaliating'}
        unowned_command {Add-Event $f 'directive.Accepted' @{commandId='ManualTargetClick_unknown';agent='1';stage='Accepted';reason='None'}}
        wrong_step_order {$f.steps.steps[0].stepId='other'}
    }
    Assert-Result $fault (Trace $f) $false
}
foreach($fault in @('negative_wrong_reason','negative_mutates_old_task','negative_extra_rejection','negative_same_outcome')) {
    $f=New-CommandFixture -Rejected
    switch($fault){
        negative_wrong_reason {Change-Event $f 0 {param($v)$v.reason='InvalidTarget'};$f.attempts[0].synchronousFeedback[0].reason='InvalidTarget'}
        negative_mutates_old_task {$f.attempts[0].after.agents[0].commandId=''}
        negative_extra_rejection {Add-Event $f 'directive.Rejected' @{commandId='';agent='';stage='Rejected';reason='Unreachable'}}
        negative_same_outcome {$spec=Copy-Value $f.scenario.steps[0];$spec.id='two';$f.scenario.steps+=@($spec);$a=Copy-Value $f.attempts[0];$a.stepId='two';$a.attemptId='probe:two';$f.attempts+=@($a);$row=Copy-Value $f.steps.steps[0];$row.stepId='two';$row.attemptId='probe:two';$f.steps.steps+=@($row)}
    }
    Assert-Result $fault (Trace $f) $false
}
$f=New-CommandFixture
$f.attempts=@();$f.events.Clear();$f.steps.steps[0].status='COVERAGE_MISSING';$f.steps.steps[0].reason='NormalDeath'
$partial=Trace $f;Assert-Result 'death_missing_coverage' $partial $true
if($partial.coverageStatus -ne 'PARTIAL'){throw 'Death fabricated full coverage.'}
function New-LostSightFixture {
    $f=New-CommandFixture
    $f.scenario.steps[0].target.kind='ActiveEnemy'
    $f.attempts[0].directive='Engage';$f.attempts[0].selection.clusters[0].kind='Enemy'
    Change-Event $f 2 {param($v)$v|Add-Member NoteProperty visible $true;$v|Add-Member NoteProperty sightTimeout 2.0}
    Change-Event $f 3 {param($v)$v|Add-Member NoteProperty visible $false;$v|Add-Member NoteProperty sightTimeout 2.0}
    $f.events[4].kind='directive.Failed';$f.events[4].gameSeconds=6.01
    Change-Event $f 4 {param($v)$v.stage='Failed';$v.reason='LostSight'}
    Change-Event $f 5 {param($v)$v.stage='Failed';$v.reason='LostSight'}
    $f.events[5].gameSeconds=6.01
    $final=$f.events[6];$f.events.RemoveAt(6)
    $sample=$f.events[3].detail|ConvertFrom-Json;$sample.activeCommand=''
    Add-Event $f 'command.progress' $sample 5
    $f.events[6].gameSeconds=6.01
    $final.sequence=8;$f.events.Add($final)
    return $f
}
foreach($fault in @('valid_observed_lost_sight','never_seen','too_early','reacquired','lock_retained','missing_timeout','timeout_drift','extra_failure')) {
    $f=New-LostSightFixture
    switch($fault){
        never_seen {Change-Event $f 2 {param($v)$v.visible=$false}}
        too_early {$f.events[4].gameSeconds=5.99}
        reacquired {Change-Event $f 3 {param($v)$v.visible=$true}}
        lock_retained {Change-Event $f 6 {param($v)$v.activeCommand='ManualTargetClick_probe'}}
        missing_timeout {Change-Event $f 3 {param($v)$v.PSObject.Properties.Remove('sightTimeout')}}
        timeout_drift {Change-Event $f 2 {param($v)$v.sightTimeout=3.0}}
        extra_failure {Add-Event $f 'directive.Failed' @{commandId='Auto_extra';agent='2';stage='Failed';reason='LostSight'}}
    }
    $trace=Trace $f;Assert-Result ('lost_sight_'+$fault) $trace ($fault -eq 'valid_observed_lost_sight')
    if($fault -eq 'valid_observed_lost_sight' -and ($trace.behaviorFailures -ne 1 -or $trace.expectedExecutionFailures -ne 1 -or $trace.unexpectedBehaviorFailures -ne 0)){throw 'LostSight raw failure was suppressed.'}
}
foreach($id in @('MC01-R','MC01-E','MC01-X','MC02','MC03','MC04-N')) {
    $value=Get-SceneRaidCommandScenario $id
    if((Get-SceneRaidScenarioHash $value.json) -cne $value.sha256){throw 'Hash round trip failed.'}
    Test-SceneRaidCommandScenario ($value.json|ConvertFrom-Json)
    $results.Add(@{name=('config_'+$id);passed=$true})
}
foreach($fault in @('extra_field','missing_field','unknown_kind','bad_boolean','forward_reference','string_deadline','duplicate_id','depth_truncated')) {
    $scenario=Copy-Value (Get-SceneRaidCommandScenario 'MC03').scenario
    switch($fault){
        extra_field {$scenario.steps[0].target|Add-Member NoteProperty injected 'Execute'}
        missing_field {$scenario.steps[0].PSObject.Properties.Remove('route')}
        unknown_kind {$scenario.steps[0].target.kind='Zone'}
        bad_boolean {$scenario.steps[0].accepted='true'}
        forward_reference {$scenario.steps[0].gate.referenceStep=$scenario.steps[-1].id}
        string_deadline {$scenario.steps[0].wallDeadline='120'}
        duplicate_id {$scenario.steps[1].id=$scenario.steps[0].id}
        depth_truncated {$scenario=$scenario|ConvertTo-Json -Depth 2 -Compress|ConvertFrom-Json}
    }
    $caught=$false;try{Test-SceneRaidCommandScenario $scenario}catch{$caught=$true}
    if(!$caught){throw "Invalid config accepted: $fault"};$results.Add(@{name=('config_'+$fault);passed=$true})
}
function New-CompletionFixture([string]$Name) {
    $f=New-CommandFixture; $f.path=Join-Path $root $Name;New-Item -ItemType Directory -Path $f.path -Force|Out-Null
    $json=$f.scenario|ConvertTo-Json -Depth 32 -Compress
    $f.config=[pscustomobject]@{schemaVersion=2;mode='ManualCluster';runId='probe';simulationSpeed=4;scenarioJson=$json;scenarioSha256=(Get-SceneRaidScenarioHash $json)}
    [IO.File]::WriteAllText((Join-Path $f.path 'command-scenario.json'),$json)
    [IO.File]::WriteAllText((Join-Path $f.path 'command-attempts.jsonl'),($f.attempts[0]|ConvertTo-Json -Depth 32 -Compress))
    Save-Json (Join-Path $f.path 'command-steps.json') $f.steps
    $item=@{itemId='probe';type='Junk';parentIndex=-1;amount=1;width=1;height=1}
    $f.carried=@{schemaVersion=1;reason='initial';agents=@(foreach($id in '1','2'){@{schemaVersion=1;agent=$id;available=$true;backpack=@($item);equipped=@()}})}
    $line=$f.carried|ConvertTo-Json -Depth 32 -Compress
    [IO.File]::WriteAllText((Join-Path $f.path 'carried-inventory.jsonl'),$line)
    Add-Event $f 'inventory.carried' $f.carried
    Save-Json (Join-Path $f.path 'warehouse-initial.json') @{Version=1;Players=@()}
    Save-Json (Join-Path $f.path 'item-definitions.json') @{schemaVersion=1;runId='probe';columns=6;rows=10;items=@($item)}
    $f.final=@{Version=1;Players=@(@{AgentId='default_player';Pages=@(@{PageIndex=0;CellStates=@();Items=@(@{ItemID='probe';Amount=2;X=0;Y=0;IsRotated=$false})})})}
    Save-Json (Join-Path $f.path 'warehouse-final.json') $f.final
    return $f
}
foreach($fault in @('valid_no_box_extraction','missing_carried','missing_initial_actor','stream_mismatch','hash_mismatch','archived_mismatch','missing_settlement','warehouse_loss','warehouse_duplicate','silent_carried_clear')) {
    $f=New-CompletionFixture $fault
    switch($fault){
        missing_carried {Remove-Item -LiteralPath (Join-Path $f.path 'carried-inventory.jsonl')}
        missing_initial_actor {$f.carried.agents=@($f.carried.agents[0]);$line=$f.carried|ConvertTo-Json -Depth 32 -Compress;[IO.File]::WriteAllText((Join-Path $f.path 'carried-inventory.jsonl'),$line);$f.events[-1].detail=$line}
        stream_mismatch {$f.events[-1].detail='{}'}
        hash_mismatch {$f.config.scenarioSha256='bad'}
        archived_mismatch {[IO.File]::WriteAllText((Join-Path $f.path 'command-scenario.json'),'{}')}
        missing_settlement {Change-Event $f 6 {param($v)$v.settledAgents=@()}}
        warehouse_loss {$f.final.Players[0].Pages[0].Items[0].Amount=1;Save-Json (Join-Path $f.path 'warehouse-final.json') $f.final}
        warehouse_duplicate {$f.final.Players[0].Pages[0].Items[0].Amount=3;Save-Json (Join-Path $f.path 'warehouse-final.json') $f.final}
        silent_carried_clear {$f.carried.reason='finalLiveAgents';$f.carried.agents[0].backpack=@();Add-Event $f 'inventory.carried' $f.carried;Add-Content -LiteralPath (Join-Path $f.path 'carried-inventory.jsonl') -Value ("`n"+$f.events[-1].detail) -Encoding UTF8}
    }
    $value=Test-SceneRaidClusterCompletion $f.path $f.config $f.events.ToArray() $f.result
    Assert-Result $fault $value ($fault -eq 'valid_no_box_extraction')
}
Save-Json (Join-Path $root 'summary.json') @{passed=$results.Count;results=$results.ToArray()}
Write-Output "PASS $($results.Count) Cluster command probes: $root"
