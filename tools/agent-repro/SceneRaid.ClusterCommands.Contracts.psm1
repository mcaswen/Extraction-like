Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.CommandConfig.psm1')
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Settlement.psm1')

function Get-CommandDistanceSquared($A, $B) { return [Math]::Pow($A.x-$B.x,2) + [Math]::Pow($A.z-$B.z,2) }
function Get-CommandActor($Snapshot, [string]$Id) { return @($Snapshot.agents | Where-Object id -CEQ $Id) | Select-Object -First 1 }
function Get-ExpectedCommandCluster($Selection, $Selector, [string]$Actor, [string]$Excluded) {
    $kind=if($Selector.kind -eq 'ActiveEnemy'){'Enemy'}else{$Selector.kind}
    $actorInfo=@($Selection.agents|Where-Object id -CEQ $Actor)[0]
    $best=$null; $bestDistance=[double]::PositiveInfinity
    foreach($cluster in $Selection.clusters) {
        if (!$cluster.active -or $cluster.completed -or $cluster.kind -cne $kind -or $cluster.identity -ceq $Excluded) { continue }
        $members=@($cluster.members|Where-Object{$_.active -and !$_.completed})
        if (!$members.Count -or ($Selector.singleton -and $members.Count -ne 1)) { continue }
        $distance=($members|ForEach-Object{[Math]::Sqrt((Get-CommandDistanceSquared $_.position $actorInfo.position))}|Measure-Object -Minimum).Minimum
        $class=if($actorInfo.discoveryRange -le 0){'UndefinedRange'}elseif($distance -le $actorInfo.interactionRange){'Interaction'}elseif($distance -le $actorInfo.discoveryRange){'Near'}elseif($distance -ge 2*$actorInfo.discoveryRange){'Far'}else{'Middle'}
        if ($Selector.distance -ne 'Any' -and $class -cne $Selector.distance) { continue }
        if ($distance -lt $bestDistance -or ($distance -eq $bestDistance -and [StringComparer]::Ordinal.Compare($cluster.identity,$best.identity) -lt 0)) { $best=$cluster; $bestDistance=$distance }
    }
    return $best
}
function Test-CommandGate($Spec, $Attempt, $ByStep, $Directives, $Events, [string]$Actor) {
    $gate = $Spec.gate
    if ($gate.kind -eq 'Ready') { return $true }
    $current = Get-CommandActor $Attempt.before $Actor
    if (!$current) { return $false }
    if ($gate.kind -eq 'Retaliating') { return $current.commandId -like 'CombatDamage*' -and !!$current.suspendedCommand }
    if ($gate.kind -eq 'InventoryOpen') { return $Attempt.before.inventoryOpen -and $Attempt.before.inventoryAgent -ceq $Actor }
    if (!$ByStep.ContainsKey($gate.referenceStep)) { return $false }
    $prior = $ByStep[$gate.referenceStep]
    if ($gate.kind -eq 'Moving') {
        $origin = Get-CommandActor $prior.before $Actor
        return $origin -and $current.commandId -ceq $prior.commandId -and $current.hasPath -and
            (Get-CommandDistanceSquared $current.position $origin.position) -gt 1
    }
    if ($gate.kind -eq 'Completed' -or $gate.kind -eq 'CombatCompleted') {
        return ($gate.kind -eq 'Completed' -or $prior.directive -eq 'Engage') -and @($Directives | Where-Object {
            $_.data.commandId -ceq $prior.commandId -and $_.data.stage -eq 'Completed' -and $_.source.sequence -lt $Attempt.outcomeSequence }).Count -eq 1
    }
    if ($gate.kind -eq 'InventoryClosed') {
        return @($Events | Where-Object { $_.kind -eq 'inventory.closed' -and $_.sequence -lt $Attempt.outcomeSequence } | ForEach-Object {
            $_.detail | ConvertFrom-Json } | Where-Object { $_.commandId -ceq $prior.commandId -and $_.reason -notin @('interaction_invalidated','runEnded') }).Count -gt 0
    }
    if ($gate.kind -eq 'Near') {
        $actorInfo = @($Attempt.selection.agents | Where-Object id -CEQ $Actor)[0]
        $member = @($Attempt.selection.clusters | Where-Object identity -CEQ $prior.cluster | ForEach-Object { $_.members } | Where-Object identity -CEQ $prior.target)
        if ($member.Count -ne 1 -or $current.commandId -cne $prior.commandId) { return $false }
        $distance = [Math]::Sqrt((Get-CommandDistanceSquared $current.position $member[0].position))
        return $distance -le $actorInfo.discoveryRange -and $distance -gt [Math]::Max(0.1,$actorInfo.interactionRange)
    }
    return $false
}

function Test-SceneRaidClusterCommandTrace {
    param($Scenario, [object[]]$Attempts, $StepResults, [object[]]$Events, $RunResult)
    $failures = [Collections.Generic.List[string]]::new()
    $coverage = [ordered]@{}
    $expectedRejects = [Collections.Generic.HashSet[long]]::new()
    $rawFailures = @($Events | Where-Object { $_.kind -in @('directive.Rejected','directive.Failed') })
    try {
        Test-SceneRaidCommandScenario $Scenario
        if ($StepResults.schemaVersion -ne 1 -or $StepResults.scenarioId -cne $Scenario.id -or !$StepResults.stopped -or
            @($StepResults.steps).Count -ne @($Scenario.steps).Count) { throw 'command_steps_incomplete' }
        if (($StepResults.steps.stepId -join '|') -cne ($Scenario.steps.id -join '|')) { throw 'command_step_order_mismatch' }
        $byStep = @{}; $byCommand = @{}; $outcomes = [Collections.Generic.HashSet[long]]::new()
        foreach ($attempt in $Attempts) {
            if ($attempt.schemaVersion -ne 1 -or $attempt.scenarioId -cne $Scenario.id -or
                $attempt.attemptId -cne ($Scenario.id+':'+$attempt.stepId) -or $byStep.ContainsKey($attempt.stepId)) { throw 'duplicate_or_invalid_attempt' }
            $byStep[$attempt.stepId]=$attempt
            if ($attempt.commandId) {
                if ($byCommand.ContainsKey($attempt.commandId)) { throw 'duplicate_command_identity' }
                $byCommand[$attempt.commandId]=$attempt
            }
        }
        $directives = @($Events | Where-Object { $_.kind -like 'directive.*' } | ForEach-Object {
            [pscustomobject]@{source=$_;data=($_.detail|ConvertFrom-Json)} })
        $progress = @($Events | Where-Object kind -EQ 'command.progress' | ForEach-Object {
            [pscustomobject]@{source=$_;data=($_.detail|ConvertFrom-Json)} })
        $snapshots = @($Events | Where-Object kind -EQ 'snapshot' | ForEach-Object { $_.detail|ConvertFrom-Json })
        $last = if ($snapshots.Count) {$snapshots[-1]} else {$null}
        $seenSteps = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($spec in $Scenario.steps) {
            $rows = @($StepResults.steps | Where-Object stepId -CEQ $spec.id)
            if ($rows.Count -ne 1 -or !$seenSteps.Add($spec.id)) { throw 'duplicate_or_missing_step' }
            $row = $rows[0]
            if ($row.status -eq 'COVERAGE_MISSING') {
                if ($byStep.ContainsKey($spec.id)) { throw 'submitted_step_hidden_as_missing' }
                $coverage[$spec.id]=[pscustomobject]@{status='MISSING';reason=$row.reason;movement=$false;damage=$false;terminal=''}
                continue
            }
            if ($row.status -ne 'SUBMITTED' -or !$byStep.ContainsKey($spec.id)) { throw 'step_attempt_missing' }
            $attempt=$byStep[$spec.id]
            if ($attempt.requestedAgent -cne $spec.agent -or $attempt.route -cne $spec.route -or
                $row.attemptId -cne $attempt.attemptId -or $row.cluster -cne $attempt.cluster) { throw 'step_attempt_mismatch' }
            $actor=if ($spec.agent -eq 'Focused') {$attempt.focusBefore} else {$spec.agent}
            if ($spec.route -eq 'Focused' -and $attempt.focusBefore -cne $actor) { $failures.Add("wrong_focus:$($spec.id)") }
            if ($spec.focusAgent -and $attempt.focusBefore -cne $spec.focusAgent) { $failures.Add("focus_change_missing:$($spec.id)") }
            if (!(Test-CommandGate $spec $attempt $byStep $directives $Events $actor)) { $failures.Add("gate_not_observed:$($spec.id)") }
            $outcome=@($directives | Where-Object { $_.source.sequence -eq $attempt.outcomeSequence })
            if ($outcome.Count -ne 1 -or !$outcomes.Add([long]$attempt.outcomeSequence)) { throw 'outcome_not_uniquely_correlated' }
            $actual=$outcome[0].data
            if ($outcome[0].source.kind -cne ('directive.'+$actual.stage) -or
                [string]$actual.commandId -cne [string]$attempt.commandId -or [string]$actual.agent -cne [string]$attempt.resolvedAgent -or
                [string]$actual.target -cne [string]$attempt.target -or [string]$actual.targetId -cne [string]$attempt.targetId -or
                $outcome[0].source.frame -ne $attempt.frame) { throw 'outcome_request_mismatch' }
            $syncOutcome=@($attempt.synchronousFeedback | Where-Object eventSequence -EQ $attempt.outcomeSequence)
            if ($syncOutcome.Count -ne 1 -or $syncOutcome[0].attemptId -cne $attempt.attemptId) { throw 'missing_synchronous_call_correlation' }
            foreach ($sync in $attempt.synchronousFeedback) {
                $source=@($directives | Where-Object { $_.source.sequence -eq $sync.eventSequence })
                if ($source.Count -ne 1 -or [string]$sync.commandId -cne [string]$source[0].data.commandId -or
                    $sync.stage -cne $source[0].data.stage -or $sync.reason -cne $source[0].data.reason) { throw 'synchronous_feedback_corrupted' }
            }
            $expectedStage=if($spec.accepted){'Accepted'}else{'Rejected'}
            $matches=$attempt.accepted -eq $spec.accepted -and $actual.stage -ceq $expectedStage -and $actual.reason -ceq $spec.reason
            if (!$matches) { $failures.Add("unexpected_response:$($spec.id):$($actual.stage):$($actual.reason)") }
            if ($attempt.commandId -and $attempt.resolvedAgent -cne $actor) { $matches=$false; $failures.Add("wrong_agent:$($spec.id)") }
            if ($spec.target.sameAsStep -and (!$byStep.ContainsKey($spec.target.sameAsStep) -or
                $attempt.cluster -cne $byStep[$spec.target.sameAsStep].cluster)) { $matches=$false; $failures.Add("wrong_reused_cluster:$($spec.id)") }
            if (!$spec.target.sameAsStep) {
                $excluded=if($spec.target.excludeStep -and $byStep.ContainsKey($spec.target.excludeStep)){$byStep[$spec.target.excludeStep].cluster}else{''}
                $selected=Get-ExpectedCommandCluster $attempt.selection $spec.target $actor $excluded
                if (!$selected -or $selected.identity -cne $attempt.cluster) { $matches=$false; $failures.Add("wrong_selected_cluster:$($spec.id)") }
            }
            foreach ($before in $attempt.before.agents) {
                $after=Get-CommandActor $attempt.after $before.id
                if (!$after) { throw 'agent_disappeared_during_synchronous_submit' }
                if (!$attempt.accepted -or $before.id -cne $actor) {
                    if ([string]$before.commandId -cne [string]$after.commandId -or [string]$before.suspendedCommand -cne [string]$after.suspendedCommand) {
                        $failures.Add("unrelated_or_rejected_task_changed:$($spec.id):$($before.id)")
                    }
                }
            }
            if (!$attempt.accepted) {
                if ($matches) { [void]$expectedRejects.Add([long]$attempt.outcomeSequence) }
                $coverage[$spec.id]=[pscustomobject]@{status=$(if($matches){'REJECTED_AS_EXPECTED'}else{'FAILED'});reason=$actual.reason;movement=$false;damage=$false;terminal='Rejected'}
                continue
            }
            if (!$attempt.commandId -or $attempt.commandId -notlike 'ManualTargetClick*') { throw 'accepted_command_identity_missing' }
            $after=Get-CommandActor $attempt.after $actor
            if (!$after -or $after.commandId -cne $attempt.commandId -or $after.suspendedCommand) { $failures.Add("active_command_not_installed:$($spec.id)") }
            $clusters=@($attempt.selection.clusters | Where-Object identity -CEQ $attempt.cluster)
            if ($clusters.Count -ne 1) { throw 'selected_cluster_missing' }
            $cluster=$clusters[0]
            $kind=if($spec.target.kind -eq 'ActiveEnemy'){'Enemy'}else{$spec.target.kind}
            $directive=if($kind -eq 'Enemy'){'Engage'}elseif($kind -eq 'Resource'){'Search'}else{'Extract'}
            if ($cluster.kind -cne $kind -or $attempt.directive -cne $directive -or $attempt.targetId -cne $cluster.targetId -or
                ($kind -eq 'Resource' -and $attempt.target -cne $attempt.cluster) -or
                ($kind -ne 'Resource' -and @($cluster.members | Where-Object identity -CEQ $attempt.target).Count -ne 1)) { $failures.Add("wrong_cluster_member:$($spec.id)") }
            $nearest=@($cluster.members | Where-Object { $_.active -and !$_.completed } | ForEach-Object { $_.approaches } |
                Where-Object agent -CEQ $actor | Sort-Object distancePlanar | Select-Object -First 1)
            if ($spec.target.distance -ne 'Any' -and ($nearest.Count -ne 1 -or $nearest[0].distanceClass -cne $spec.target.distance)) { $failures.Add("wrong_distance_class:$($spec.id)") }
            $timeline=@($directives | Where-Object { $_.data.commandId -ceq $attempt.commandId })
            $terminal=@($timeline | Where-Object { $_.data.stage -in @('Completed','Cancelled','Failed') })
            if ($terminal.Count -gt 1) { $failures.Add("duplicate_terminal:$($spec.id)") }
            $samples=@($progress | Where-Object { $_.data.commandId -ceq $attempt.commandId })
            if (!$samples.Count) { $failures.Add("missing_progress_probe:$($spec.id)") }
            foreach($sample in $samples) {
                if ($sample.data.agent -cne $actor -or $sample.data.attemptId -cne $attempt.attemptId -or $sample.source.sequence -le $attempt.outcomeSequence) { $failures.Add("wrong_progress_owner:$($spec.id)") }
            }
            $origin=Get-CommandActor $attempt.before $actor
            $moved=@($samples | Where-Object { $_.data.activeCommand -ceq $attempt.commandId -and
                (Get-CommandDistanceSquared $_.data.position $origin.position) -gt 1 }).Count -gt 0
            $health=@($samples | Where-Object { $_.data.enemyHealth -ge 0 } | ForEach-Object { [double]$_.data.enemyHealth })
            $damaged=$health.Count -gt 1 -and ($health|Measure-Object -Minimum).Minimum -lt $health[0]
            $dead=$last -and @($last.agents|Where-Object { $_.id -ceq $actor -and $_.health -eq 0 }).Count -eq 1
            $extracted=$last -and $last.settledAgents -contains $actor
            $end=if($terminal.Count){$terminal[-1].data.stage}elseif($directive -eq 'Extract' -and $extracted){'Extracted'}elseif($dead){'Died'}else{''}
            if (!$end) { $failures.Add("missing_command_terminal:$($spec.id)") }
            if ($end -eq 'Cancelled') {
                $replacement=@($directives|Where-Object { $_.data.stage -eq 'Accepted' -and $_.data.agent -ceq $actor -and
                    $_.source.sequence -gt $terminal[-1].source.sequence -and $_.source.frame -eq $terminal[-1].source.frame })
                $superseded=$terminal[-1].data.reason -eq 'Superseded' -and $replacement.Count -gt 0
                $removedAfterExtraction=$directive -eq 'Extract' -and $extracted -and $terminal[-1].data.reason -eq 'None'
                if (!$superseded -and !$dead -and !$removedAfterExtraction) { $failures.Add("cancellation_without_replacement:$($spec.id)") }
            }
            if ($end -eq 'Completed' -and $directive -eq 'Engage' -and !$damaged) { $failures.Add("missing_target_damage:$($spec.id)") }
            if ($end -eq 'Completed' -and $directive -eq 'Search') {
                $closed=@($Events|Where-Object kind -EQ 'inventory.closed'|ForEach-Object {$_.detail|ConvertFrom-Json}|Where-Object {
                    $_.commandId -ceq $attempt.commandId -or $_.resource -cin @($cluster.members.identity) })
                if (!$closed.Count) { $failures.Add("missing_resource_close:$($spec.id)") }
            }
            if ($end -notin @('Cancelled','Died') -and !$moved -and !$damaged) { $failures.Add("accepted_without_action:$($spec.id)") }
            $coverage[$spec.id]=[pscustomobject]@{status=$(if($dead -and !$moved -and !$damaged){'MISSING'}else{'OBSERVED'});
                reason=$(if($dead){'NormalDeath'}else{''});movement=$moved;damage=$damaged;terminal=$end;agent=$actor;kind=$kind;distance=$spec.target.distance}
        }
        if ($byStep.Count -ne @($StepResults.steps|Where-Object status -EQ 'SUBMITTED').Count) { throw 'unexpected_attempt' }
        foreach ($entry in $directives) {
            if ($entry.data.commandId -like 'ManualTargetClick*' -and !$byCommand.ContainsKey($entry.data.commandId)) { $failures.Add('unowned_manual_command') }
            if ($byCommand.ContainsKey([string]$entry.data.commandId)) {
                $linked=@($Events|Where-Object kind -EQ 'command.feedback'|ForEach-Object{$_.detail|ConvertFrom-Json}|Where-Object eventSequence -EQ $entry.source.sequence)
                if ($linked.Count -ne 1 -or $linked[0].attemptId -cne $byCommand[$entry.data.commandId].attemptId -or
                    $linked[0].stage -cne $entry.data.stage -or $linked[0].reason -cne $entry.data.reason -or
                    $linked[0].agent -cne $entry.data.agent -or $linked[0].target -cne $entry.data.target -or $linked[0].targetId -cne $entry.data.targetId) { $failures.Add('invalid_command_feedback') }
            }
        }
        foreach ($entry in $Events | Where-Object kind -EQ 'command.feedback') {
            $data=$entry.detail|ConvertFrom-Json
            if ($data.commandId -and $byCommand.ContainsKey($data.commandId) -and $data.attemptId -cne $byCommand[$data.commandId].attemptId) { $failures.Add('wrong_async_attempt') }
        }
    } catch { $failures.Add('invalid_command_evidence:'+$_.Exception.Message) }
    $unexpected=@($rawFailures|Where-Object{!$expectedRejects.Contains([long]$_.sequence)}).Count
    if ($unexpected -gt 0) { $failures.Add("unexpected_directive_failures:$unexpected") }
    return [pscustomobject]@{status=$(if($failures.Count){'FAIL'}else{'PASS'});failures=$failures.ToArray();coverage=$coverage;
        coverageStatus=$(if(@($coverage.Values|Where-Object status -EQ 'MISSING').Count){'PARTIAL'}else{'COMPLETE'});
        behaviorFailures=$rawFailures.Count;expectedRejections=$expectedRejects.Count;unexpectedBehaviorFailures=$unexpected}
}

function Test-SceneRaidClusterCompletion {
    param([string]$OutputPath, $Config, [object[]]$Events, $RunResult)
    $failures=[Collections.Generic.List[string]]::new(); $trace=$null; $settlement=$null
    try {
        if ($Config.schemaVersion -ne 2 -or $Config.mode -ne 'ManualCluster' -or
            (Get-SceneRaidScenarioHash $Config.scenarioJson) -cne $Config.scenarioSha256) { throw 'command_config_hash_mismatch' }
        $archived=[IO.File]::ReadAllText((Join-Path $OutputPath 'command-scenario.json'))
        if ($archived -cne $Config.scenarioJson) { throw 'archived_scenario_mismatch' }
        $scenario=$Config.scenarioJson|ConvertFrom-Json
        $attempts=@(Get-Content -LiteralPath (Join-Path $OutputPath 'command-attempts.jsonl') -Encoding UTF8|ForEach-Object{$_|ConvertFrom-Json})
        $steps=Get-Content -LiteralPath (Join-Path $OutputPath 'command-steps.json') -Raw -Encoding UTF8|ConvertFrom-Json
        $trace=Test-SceneRaidClusterCommandTrace $scenario $attempts $steps $Events $RunResult
        foreach($failure in $trace.failures){$failures.Add($failure)}
        $carried=@{}; $initial=@{}
        $lines=@(Get-Content -LiteralPath (Join-Path $OutputPath 'carried-inventory.jsonl') -Encoding UTF8)
        $raw=@($Events|Where-Object kind -EQ 'inventory.carried')
        if (!$lines.Count -or $lines.Count -ne $raw.Count) { throw 'carried_stream_incomplete' }
        for($i=0;$i -lt $lines.Count;$i++) {
            if ($lines[$i] -cne $raw[$i].detail) { throw 'carried_stream_mismatch' }
            $capture=$lines[$i]|ConvertFrom-Json
            if ($capture.schemaVersion -ne 1) { throw 'unknown_carried_capture_schema' }
            foreach($row in $capture.agents) {
                if ($row.schemaVersion -ne 1 -or !$row.available -or $row.agent -cnotin @('1','2')) { throw 'carried_snapshot_missing' }
                $carried[$row.agent]=$row
                if ($capture.reason -eq 'initial') { $initial[$row.agent]=$row }
            }
        }
        if ($initial.Count -ne 2) { throw 'initial_carried_inventory_missing' }
        foreach($id in @('1','2')) {
            $ledger=@($Events|Where-Object kind -EQ 'inventory.ledger'|ForEach-Object{$_.detail|ConvertFrom-Json}|Where-Object agent -CEQ $id)
            if (!$ledger.Count) {
                foreach($part in @('backpack','equipped')) {
                    if (!(Test-RaidQuantitiesEqual (Get-RaidItemQuantities $initial[$id].$part) (Get-RaidItemQuantities $carried[$id].$part))) { throw 'carried_changed_without_inventory_action' }
                }
            }
        }
        $settlement=Test-SceneRaidSettlement $OutputPath $Config $Events $RunResult $carried
        foreach($failure in $settlement.failures){$failures.Add($failure)}
    } catch { $failures.Add('invalid_manual_completion_evidence:'+$_.Exception.Message) }
    return [pscustomobject]@{status=$(if($failures.Count){'FAIL'}else{'PASS'});failures=$failures.ToArray();
        outcome=$(if(!$failures.Count -and $settlement){$settlement.outcome}else{'UNVERIFIED'});
        coverage=$(if($trace){$trace.coverage}else{@{}});coverageStatus=$(if($trace){$trace.coverageStatus}else{'MISSING'});
        expectedRejections=$(if($trace){$trace.expectedRejections}else{0});
        unexpectedBehaviorFailures=$(if($trace){$trace.unexpectedBehaviorFailures}else{0});trace=$trace;settlement=$settlement}
}
Export-ModuleMember -Function Test-SceneRaidClusterCommandTrace,Test-SceneRaidClusterCompletion
