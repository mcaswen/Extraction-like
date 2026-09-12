Set-StrictMode -Version Latest

function Test-SceneRaidRoutes {
    param([object[]]$Events, [string]$Mode = 'Autonomous', [string[]]$ExpectedAgents = @('1','2'))
    $failures = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $coverage = [ordered]@{ accepted=0; states=0; frames=0; synchronizedDisplays=0; travelling=0; waiting=0; retaliation=0; completed=0; extracted=0; dead=0; player=0; autonomous=0 }
    $graphs=@{}; $last=@{}; $accepted=@{}; $displayLag=@{}; $actors=[Collections.Generic.HashSet[string]]::new()
    $rejections=[Collections.Generic.List[object]]::new(); $rootFailures=[Collections.Generic.List[object]]::new()
    function Edge-Key([string]$A,[string]$B) { if ([string]::CompareOrdinal($A,$B) -le 0) { "$A|$B" } else { "$B|$A" } }
    # Graph records may follow an initial Accepted event in the same load; index the immutable revisions first.
    foreach ($event in $Events) {
        if ($event.kind -ne 'route.graph') { continue }
        $graph=$event.detail | ConvertFrom-Json
        $nodes=@{}; $edges=[Collections.Generic.HashSet[string]]::new();$edgeIds=@{}
        foreach ($node in $graph.nodes) { $nodes[$node.id]=$node }
        foreach ($edge in $graph.edges) {
            if (!$nodes.ContainsKey($edge.from) -or !$nodes.ContainsKey($edge.to) -or $edge.from -eq $edge.to -or !$edges.Add((Edge-Key $edge.from $edge.to))) { [void]$failures.Add('invalid_graph_edge') }
            if ([Math]::Abs($edge.a.x-$edge.b.x) -gt .01 -and [Math]::Abs($edge.a.y-$edge.b.y) -gt .01) { [void]$failures.Add('diagonal_graph_edge') }
            $edgeIds[$edge.id]=$edge
        }
        $graphs[[string]$graph.revision]=@{nodes=$nodes;edges=$edges;edgeIds=$edgeIds}
    }
    foreach ($event in $Events) {
        if ($event.kind -eq 'route.result') {
            $row=$event.detail | ConvertFrom-Json
            switch ($row.stage) {
                'Accepted' { $accepted["$($row.agent)|$($row.requestId)|$($row.version)"]=$true;$coverage.accepted++ }
                'Rejected' { $rejections.Add($row) }
                'Failed' { $rootFailures.Add($row); if ($row.reason -ne 'CapacityExtraction') { [void]$failures.Add("root_failed:$($row.agent):$($row.reason)") } }
            }
            if ($Mode -eq 'Autonomous' -and $row.source -eq 'Player') { [void]$failures.Add('player_root_in_autonomous_run') }
        }
        if ($event.kind -eq 'route.state') {
            $row=$event.detail | ConvertFrom-Json;$coverage.states++
            if (!$row.installed -or !$row.hasRoute) { continue }
            [void]$actors.Add($row.agent)
            $key="$($row.agent)|$($row.requestId)|$($row.version)"
            if ($row.source -eq 'Player') { $coverage.player++ } else { $coverage.autonomous++ }
            if ($row.retaliating) { $coverage.retaliation++ }
            switch ($row.stage) { 'Completed' { $coverage.completed++ } 'Extracted' { $coverage.extracted++ } 'Dead' { $coverage.dead++ } }
            if ($row.stepPhase -eq 'WaitingForInventory') { $coverage.waiting++ }
            $graph=$graphs[[string]$row.graphRevision]
            if ($null -eq $graph) { [void]$failures.Add('missing_graph_revision');continue }
            if (@($row.nodes).Count -eq 0 -or $row.nodes[-1] -ne $row.goal) { [void]$failures.Add('route_goal_or_sequence_mismatch') }
            for ($i=0;$i -lt @($row.nodes).Count;$i++) {
                if (!$graph.nodes.ContainsKey($row.nodes[$i])) { [void]$failures.Add('route_node_missing') }
                if ($i -gt 0 -and !$graph.edges.Contains((Edge-Key $row.nodes[$i-1] $row.nodes[$i]))) { [void]$failures.Add('route_crosses_unconnected_nodes') }
            }
            if ($row.entryFrom -and @($row.nodes).Count -gt 0 -and !$graph.edges.Contains((Edge-Key $row.entryFrom $row.nodes[0]))) { [void]$failures.Add('invalid_entry_edge') }
            if ($row.active -and ($row.cursor -lt 0 -or $row.cursor -ge @($row.nodes).Count -or $row.currentNode -ne $row.nodes[$row.cursor])) { [void]$failures.Add('active_cursor_mismatch') }
            if ($last.ContainsKey($key)) {
                $previous=$last[$key]
                if ($row.cursor -lt $previous.cursor) { [void]$failures.Add('root_cursor_reversed_without_version_change') }
                if (($row.nodes -join '|') -ne ($previous.nodes -join '|')) { [void]$failures.Add('root_sequence_changed_without_version_change') }
            }
            $last[$key]=$row
            if ($row.active -and !$accepted.ContainsKey($key)) { [void]$failures.Add('active_root_without_accepted_event') }
        }
        if ($event.kind -ne 'route.frame') { continue }
        $frame=$event.detail | ConvertFrom-Json;$coverage.frames++
        $roots=@{};foreach ($root in $frame.roots) { $roots[$root.agent]=$root }
        foreach ($display in $frame.displays) {
            if ($display.onEdge -and $display.hasMarker) {
                $coverage.travelling++
                $dx=$display.toPort.x-$display.fromPort.x;$dy=$display.toPort.y-$display.fromPort.y
                $length2=$dx*$dx+$dy*$dy
                if ($length2 -lt .0001) { [void]$failures.Add('zero_display_edge');continue }
                $px=$display.markerPosition.x-$display.fromPort.x;$py=$display.markerPosition.y-$display.fromPort.y
                $t=($px*$dx+$py*$dy)/$length2
                $cross=[Math]::Abs($px*$dy-$py*$dx)/[Math]::Sqrt($length2)
                if ($cross -gt .05 -or $t -lt -.001 -or $t -gt 1.001) { [void]$failures.Add('agent_marker_off_edge') }
                if ($display.markerMembers -eq 1 -and [Math]::Abs($t-$display.progress) -gt .005) { [void]$failures.Add('marker_progress_mismatch') }
                if ($display.validDistance -and $display.baselineDistance-$display.tolerance -gt .0001) {
                    $expected=$display.baselineProgress+(1-$display.baselineProgress)*($display.baselineDistance-$display.remainingDistance)/($display.baselineDistance-$display.tolerance)
                    $expected=[Math]::Max([double]0,[Math]::Min([double]1,[double]$expected))
                    if ([Math]::Abs($display.progress-$expected) -gt .005) { [void]$failures.Add('distance_projection_mismatch') }
                }
            }
            $root=$roots[$display.agent]
            # Gameplay and 20 Hz presentation can straddle a transition. Allow one observation interval,
            # but reject a mismatch that remains in the next 4 Hz sample instead of silently skipping forever.
            $displayActive=$display.mode -in @('Entering','Travelling','Processing','Waiting','Extracting')
            if ($null -eq $root -or $display.requestId -ne $root.requestId -or $display.version -ne $root.version -or $display.cursor -ne $root.cursor -or $displayActive -ne $root.active) {
                $wall=if($event.PSObject.Properties['wallSeconds']){[double]$event.wallSeconds}else{0.0}
                if (!$displayLag.ContainsKey($display.agent)) { $displayLag[$display.agent]=$wall }
                elseif ($wall-$displayLag[$display.agent] -gt .35) { [void]$failures.Add('display_stale_across_samples') }
                continue
            }
            $displayLag.Remove($display.agent)
            $coverage.synchronizedDisplays++
            $remaining=@();if ($root.active) { for($i=[Math]::Max(0,$root.cursor);$i -lt @($root.nodes).Count;$i++){ $remaining+=$root.nodes[$i] } }
            if (($remaining -join '|') -ne ($display.remaining -join '|')) { [void]$failures.Add('display_sequence_mismatch') }
        }
    }
    if ($coverage.accepted -eq 0 -or $coverage.states -eq 0 -or $coverage.frames -eq 0 -or $coverage.synchronizedDisplays -eq 0) { [void]$failures.Add('missing_route_evidence') }
    foreach ($agent in $ExpectedAgents) { if (!$actors.Contains($agent)) { [void]$failures.Add("missing_route_agent:$agent") } }
    return [pscustomobject]@{schemaVersion=1;status=$(if($failures.Count){'FAIL'}else{'PASS'});failures=@($failures | Sort-Object);
        coverage=$coverage;rejections=$rejections.ToArray();rootFailures=$rootFailures.ToArray();
        terminalAcceptance='RequiresIndependentRaidSettlement';sampledStatesDoNotProveEveryIntermediateFrame=$true}
}
Export-ModuleMember -Function Test-SceneRaidRoutes
