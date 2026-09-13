Set-StrictMode -Version Latest

# Offline evidence only. Unproved recovery always remains an ordinary failure.
function Get-RecoveryField($Value, [string]$Name) {
    if ($null -ne $Value -and $Value.PSObject.Properties[$Name]) { return $Value.$Name }
    return $null
}
function Test-RecoveryPosition($Position) {
    foreach ($axis in @('x','y','z')) {
        $value = Get-RecoveryField $Position $axis
        if ($null -eq $value) { return $false }
        $number = 0.0
        if (![double]::TryParse([string]$value, [Globalization.NumberStyles]::Float,
            [Globalization.CultureInfo]::InvariantCulture, [ref]$number) -or
            [double]::IsNaN($number) -or [double]::IsInfinity($number)) { return $false }
    }
    return $true
}
function Get-SceneRaidRecoveredRouteSteps {
    param([object[]]$Events)
    $owners = @{}
    $accepted = @{}
    $results = [Collections.Generic.List[object]]::new()
    $failures = [Collections.Generic.List[object]]::new()
    $previousSequence = -1L
    $previousWall = -1.0
    try {
        foreach ($event in $Events) {
            if ($event.kind -notin @('route.result','route.state','route.frame','directive.Failed','directive.Rejected')) { continue }
            $sequence = Get-RecoveryField $event 'sequence'
            $wall = Get-RecoveryField $event 'wallSeconds'
            if ($null -eq $sequence -or $null -eq $wall -or [long]$sequence -le $previousSequence -or
                [double]$wall -lt $previousWall -or [double]::IsNaN($wall) -or [double]::IsInfinity($wall)) { return }
            $previousSequence = [long]$sequence; $previousWall = [double]$wall
            $detail = $event.detail | ConvertFrom-Json -ErrorAction Stop
            $agent = [string](Get-RecoveryField $detail 'agent')
            $row = [pscustomobject]@{sequence=[long]$sequence;wall=[double]$wall;kind=$event.kind;detail=$detail;owner=$null;wasAccepted=$false}
            if ($event.kind -in @('directive.Failed','directive.Rejected')) {
                if ($owners.ContainsKey($agent)) {
                    $row.owner = $owners[$agent]
                    $key = $agent + '|' + $row.owner.requestId + '|' + $row.owner.version
                    $row.wasAccepted = $accepted.ContainsKey($key)
                }
                $failures.Add($row)
            }
            $snapshots = @()
            if ($event.kind -eq 'route.result') {
                $results.Add($row)
                if ($detail.stage -eq 'Accepted') {
                    $accepted[$agent + '|' + $detail.requestId + '|' + $detail.version] = $true
                }
                $snapshots = @((Get-RecoveryField $detail 'snapshot'))
            } elseif ($event.kind -eq 'route.state') { $snapshots = @($detail) }
            elseif ($event.kind -eq 'route.frame') { $snapshots = @($detail.roots) }
            foreach ($snapshot in $snapshots) {
                if ($null -ne $snapshot) { $owners[[string]$snapshot.agent] = $snapshot }
            }
        }
    } catch { return }

    foreach ($failure in $failures) {
        try {
            $child = $failure.detail
            $owner = $failure.owner
            if ($failure.kind -ne 'directive.Failed' -or $child.directive -ne 'MoveTo' -or
                $child.reason -ne 'NoProgress' -or $child.stage -ne 'Failed' -or !$failure.wasAccepted -or
                $null -eq $owner -or !$owner.active -or $owner.dead -or
                $owner.stage -in @('Completed','Extracted','Failed','Cancelled','Dead') -or
                [string]::IsNullOrWhiteSpace($owner.requestId) -or [string]::IsNullOrWhiteSpace($child.commandId) -or
                $owner.commandId -cne $child.commandId -or $owner.currentNode -cne $child.targetId -or
                $owner.agent -cne $child.agent -or $owner.source -notin @('Player','Autonomous')) { continue }

            # A root with repeated failed children is deliberately not waived, even if it later ends.
            $sameRootFailures = @($failures | Where-Object {
                $_.detail.agent -ceq $child.agent -and $null -ne $_.owner -and $_.owner.requestId -ceq $owner.requestId })
            if ($sameRootFailures.Count -ne 1) { continue }
            $following = @($results | Where-Object { $_.sequence -gt $failure.sequence -and $_.detail.agent -ceq $child.agent })
            if ($following.Count -lt 3) { continue }
            $planning = $following[0]; $acceptance = $following[1]; $terminal = $following[2]
            $plan = $planning.detail; $next = $acceptance.detail; $end = $terminal.detail
            if ($plan.stage -ne 'Planning' -or !$plan.replan -or $plan.reason -ne 'NoProgress' -or
                $planning.wall - $failure.wall -gt 1 -or
                $next.stage -ne 'Accepted' -or !$next.replan -or $next.reason -ne 'None' -or
                $acceptance.wall - $planning.wall -gt 8 -or
                $end.stage -notin @('Completed','Extracted') -or $end.reason -ne 'None' -or $end.replan) { continue }
            $identityMatches = $true
            foreach ($result in @($plan,$next,$end)) {
                if ($result.requestId -cne $owner.requestId -or $result.source -cne $owner.source -or
                    $result.goal -cne $owner.goal -or $result.version -ne $owner.version + 1 -or
                    $result.snapshot.agent -cne $child.agent -or $result.snapshot.requestId -cne $owner.requestId -or
                    $result.snapshot.goal -cne $owner.goal -or $result.snapshot.source -cne $owner.source -or
                    $result.snapshot.dead) { $identityMatches = $false }
            }
            if (!$identityMatches -or $plan.snapshot.version -ne $owner.version -or
                $plan.snapshot.commandId -cne $child.commandId -or $plan.snapshot.stepPhase -ne 'Failed' -or
                $plan.snapshot.currentNode -cne $owner.currentNode -or
                $next.snapshot.version -ne $next.version -or !$next.snapshot.active -or
                $end.snapshot.version -ne $end.version -or $end.snapshot.active -or
                $end.snapshot.stage -cne $end.stage) { continue }
            if (@($failures | Where-Object { $_.detail.agent -ceq $child.agent -and $_.sequence -gt $failure.sequence -and
                    $_.sequence -lt $terminal.sequence }).Count -gt 0) { continue }
            if (@($results | Where-Object { $_.detail.agent -ceq $child.agent -and $_.detail.requestId -ceq $owner.requestId -and
                    $_.detail.stage -in @('Failed','Cancelled','Dead','Rejected') }).Count -gt 0) { continue }
            if (!(Test-RecoveryPosition $plan.snapshot.position) -or !(Test-RecoveryPosition $end.snapshot.position)) { continue }
            $dx = [double]$end.snapshot.position.x - [double]$plan.snapshot.position.x
            $dz = [double]$end.snapshot.position.z - [double]$plan.snapshot.position.z
            $distance = [Math]::Sqrt($dx*$dx + $dz*$dz)
            if ($distance -lt 0.25) { continue }
            [pscustomobject]@{agent=$child.agent;requestId=$owner.requestId;source=$owner.source;goal=$owner.goal;
                commandId=$child.commandId;reason=$child.reason;oldVersion=$owner.version;recoveredVersion=$next.version;
                failureSequence=$failure.sequence;planningSequence=$planning.sequence;acceptedSequence=$acceptance.sequence;terminalSequence=$terminal.sequence;
                failedWallSeconds=$failure.wall;acceptedWallSeconds=$acceptance.wall;terminalWallSeconds=$terminal.wall;
                recoverySeconds=($terminal.wall-$failure.wall);terminalStage=$end.stage;actualPlanarDisplacement=$distance;
                policy='SingleNoProgressChild_SameRootNextVersion_ActualMovement_TerminalSuccess'}
        } catch { continue }
    }
}
Export-ModuleMember -Function Get-SceneRaidRecoveredRouteSteps
