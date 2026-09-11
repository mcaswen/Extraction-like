Set-StrictMode -Version Latest
function Add-RaidQuantity($Counts, [string]$Id, [long]$Amount) {
    if ([string]::IsNullOrWhiteSpace($Id) -or $Amount -le 0) { throw 'invalid_item_quantity' }
    if (!$Counts.ContainsKey($Id)) { $Counts.Add($Id, 0) }
    $Counts[$Id] += $Amount
}
function New-RaidQuantities { return [Collections.Generic.Dictionary[string,long]]::new([StringComparer]::Ordinal) }
function Get-RaidItemQuantities($Rows, [switch]$IncludeContainers) {
    $counts = New-RaidQuantities
    for ($i=0; $i -lt @($Rows).Count; $i++) {
        $item = $Rows[$i]
        if ($item.parentIndex -lt -1 -or $item.parentIndex -ge $i -or $item.amount -le 0) { throw 'invalid_flat_item_hierarchy' }
        if ($IncludeContainers -or $item.type -notin @('Bag','Rig')) { Add-RaidQuantity $counts $item.itemId $item.amount }
    }
    return $counts
}
function Test-RaidQuantitiesEqual($Expected, $Actual) {
    if ($Expected.Count -ne $Actual.Count) { return $false }
    foreach ($id in $Expected.Keys) { if (!$Actual.ContainsKey($id) -or $Expected[$id] -ne $Actual[$id]) { return $false } }
    return $true
}
function Get-RaidSessionQuantities($Entry) {
    $counts = Get-RaidItemQuantities $Entry.source -IncludeContainers
    $backpack = Get-RaidItemQuantities $Entry.backpack -IncludeContainers
    foreach ($id in $backpack.Keys) { Add-RaidQuantity $counts $id $backpack[$id] }
    return $counts
}
function Test-RaidInventorySessionEvents($Events, [string]$AgentId) {
    $focused=$null; $active=$null; $completed=0; $opened=0
    foreach ($event in $Events) {
        if ($event.kind -notin @('inventory.focus','inventory.opened','inventory.closed','inventory.canceled')) { continue }
        $entry=$event.detail | ConvertFrom-Json
        if ($entry.agent -ne $AgentId) { continue }
        $key=$entry.commandId+'|'+$entry.resource
        switch ($event.kind) {
            'inventory.focus' {
                if ($null -ne $active) { throw 'inventory_focus_during_open_session' }
                $focused=$key
            }
            'inventory.opened' {
                if ($null -ne $active -or $focused -ne $key) { throw 'inventory_open_without_matching_focus' }
                $active=$key; $opened++
            }
            'inventory.canceled' {
                if ($null -ne $active -or $focused -ne $key) { throw 'inventory_invalid_cancel' }
                $focused=$null
            }
            'inventory.closed' {
                if ($null -eq $active -and $focused -eq $key -and $entry.reason -eq 'interaction_invalidated') {
                    # Legacy evidence used closed for a focus canceled before Interact.
                    $focused=$null
                } elseif ($null -ne $active -and $active -eq $key) {
                    $completed++; $active=$null; $focused=$null
                } else { throw 'inventory_close_without_matching_open' }
            }
        }
    }
    if ($null -ne $active -or $opened -ne $completed) { throw 'inventory_session_not_closed' }
    return $completed
}
function Get-RaidWarehouseQuantities($Warehouse, $Definitions, [int]$Columns, [int]$Rows) {
    if ($Warehouse.Version -ne 1 -or $Columns -le 0 -or $Rows -le 0) { throw 'invalid_warehouse_schema' }
    $counts = New-RaidQuantities
    $players = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($player in $Warehouse.Players) {
        if ($player.AgentId -ne 'default_player') { throw 'unexpected_warehouse_owner' }
        if (!$players.Add($player.AgentId)) { throw 'duplicate_warehouse_owner' }
        $pages = [Collections.Generic.HashSet[int]]::new()
        foreach ($page in $player.Pages) {
            if ($page.PageIndex -lt 0 -or !$pages.Add($page.PageIndex)) { throw 'invalid_warehouse_page' }
            $cells = [Collections.Generic.HashSet[string]]::new()
            foreach ($cell in $page.CellStates) {
                if ($cell.X -lt 0 -or $cell.X -ge $Columns -or $cell.Y -lt 0 -or $cell.Y -ge $Rows -or $cell.State -lt 0 -or $cell.State -gt 3) { throw 'invalid_warehouse_cell' }
                if ($cell.State -ne 0) { [void]$cells.Add("$($cell.X),$($cell.Y)") }
            }
            foreach ($item in $page.Items) {
                if (!$Definitions.ContainsKey($item.ItemID)) { throw "unknown_warehouse_item:$($item.ItemID)" }
                Add-RaidQuantity $counts $item.ItemID $item.Amount
                $definition = $Definitions[$item.ItemID]
                $width = if ($item.IsRotated) { [int]$definition.height } else { [int]$definition.width }
                $height = if ($item.IsRotated) { [int]$definition.width } else { [int]$definition.height }
                if ($width -le 0 -or $height -le 0 -or $item.X -lt 0 -or $item.Y -lt 0 -or
                    $item.X+$width -gt $Columns -or $item.Y+$height -gt $Rows) { throw 'warehouse_out_of_bounds' }
                for ($x=$item.X; $x -lt $item.X+$width; $x++) {
                    for ($y=$item.Y; $y -lt $item.Y+$height; $y++) {
                        if (!$cells.Add("$x,$y")) { throw 'warehouse_overlap_or_blocked_cell' }
                    }
                }
            }
        }
    }
    return $counts
}
function Test-SceneRaidCompletion {
    param([string]$OutputPath, $Config, [object[]]$Events, $Result)
    $failures = [Collections.Generic.List[string]]::new()
    $coverage = [ordered]@{}
    $expected = New-RaidQuantities; $actual = New-RaidQuantities
    foreach ($name in @('warehouse-initial.json','warehouse-final.json','item-definitions.json')) {
        if (!(Test-Path -LiteralPath (Join-Path $OutputPath $name))) {
            return [pscustomobject]@{status='COVERAGE_MISSING';failures=@("missing:$name");coverage=$coverage;expected=@{};actual=@{}}
        }
    }
    if (!$Result -or $Result.status -notin @('RAID_OBSERVED_COMPLETE','RAID_OBSERVED_FAILURE')) {
        return [pscustomobject]@{status='NOT_COMPLETE';failures=@('raid_not_completed');coverage=$coverage;expected=@{};actual=@{}}
    }
    $deathTerminal = $Result.status -eq 'RAID_OBSERVED_FAILURE'
    try {
        $catalog = Get-Content -LiteralPath (Join-Path $OutputPath 'item-definitions.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($catalog.schemaVersion -ne 1 -or $catalog.runId -ne $Config.runId) { throw 'definition_manifest_mismatch' }
        $definitions = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
        foreach ($item in $catalog.items) {
            if ([string]::IsNullOrWhiteSpace($item.itemId) -or $definitions.ContainsKey($item.itemId)) { throw 'duplicate_or_empty_item_definition' }
            $definitions.Add($item.itemId, $item)
        }
        $initial = Get-Content -LiteralPath (Join-Path $OutputPath 'warehouse-initial.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $final = Get-Content -LiteralPath (Join-Path $OutputPath 'warehouse-final.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $expected = Get-RaidWarehouseQuantities $initial $definitions $catalog.columns $catalog.rows
        $actual = Get-RaidWarehouseQuantities $final $definitions $catalog.columns $catalog.rows
        $snapshots = @($Events | Where-Object kind -eq 'snapshot' | ForEach-Object { $_.detail | ConvertFrom-Json })
        if ($snapshots.Count -eq 0) { throw 'missing_final_snapshot' }
        $last = $snapshots[-1]
        if ((@($last.requiredAgents | Sort-Object) -join ',') -ne '1,2') { $failures.Add('incomplete:requiredAgents') }
        $extracted = @($last.extractedAgents)
        if ((@($extracted | Sort-Object) -join ',') -ne (@($last.settledAgents | Sort-Object) -join ',')) { $failures.Add('extraction_settlement_mismatch') }
        if ($deathTerminal) {
            $dead = @($last.agents)
            $resolved = @($extracted) + @($dead | ForEach-Object { $_.id })
            if (!$last.missionFailed -or $last.missionCompleted -or $last.timeScale -ne 0 -or $last.inventoryOpen -or
                $dead.Count -eq 0 -or @($dead | Where-Object { $_.health -ne 0 }).Count -gt 0 -or
                (@($resolved | Sort-Object) -join ',') -ne '1,2') { $failures.Add('invalid_death_terminal_state') }
        } else {
            if ((@($extracted | Sort-Object) -join ',') -ne '1,2') { $failures.Add('incomplete:extractedAgents') }
            if (!$last.missionCompleted -or $last.missionFailed -or @($last.agents).Count -ne 0) { $failures.Add('invalid_mission_terminal_state') }
        }
        $ledgers = @($Events | Where-Object kind -eq 'inventory.ledger' | ForEach-Object { $_.detail | ConvertFrom-Json })
        $closed = @($Events | Where-Object kind -eq 'inventory.closed' | ForEach-Object { $_.detail | ConvertFrom-Json })
        $opened = @($Events | Where-Object kind -eq 'inventory.opened' | ForEach-Object { $_.detail | ConvertFrom-Json })
        $transfers = @($Events | Where-Object kind -eq 'inventory.transferred' | ForEach-Object { $_.detail | ConvertFrom-Json })
        foreach ($id in @('1','2')) {
            $completedSessions = Test-RaidInventorySessionEvents $Events $id
            $history = @($snapshots | ForEach-Object { $_.agents } | Where-Object id -eq $id)
            $moved = $false
            if ($history.Count -gt 1) {
                $start = $history[0].position
                foreach ($state in $history) {
                    $p=$state.position
                    if ([Math]::Pow($p.x-$start.x,2)+[Math]::Pow($p.z-$start.z,2) -gt 1) { $moved=$true; break }
                }
            }
            $coverage["movement:$id"]=$moved
            $agentLedgers = @($ledgers | Where-Object agent -eq $id)
            $after = @($agentLedgers | Where-Object stage -eq 'afterClose')
            $coverage["inventory:$id"] = $after.Count -gt 0 -and @($transfers | Where-Object agent -eq $id).Count -gt 0
            if (!$deathTerminal -and (!$moved -or !$coverage["inventory:$id"])) { $failures.Add("missing_agent_activity:$id") }
            if ($after.Count -eq 0) {
                if ($completedSessions -gt 0 -or $agentLedgers.Count -gt 0 -or $extracted -contains $id) { $failures.Add("missing_inventory_ledger:$id") }
                continue
            }
            $lastInventory=$after[-1]
            if ($lastInventory.schemaVersion -ne 2 -or !$lastInventory.equipmentCaptured) { throw 'inventory_schema_or_equipment_missing' }
            $carried = Get-RaidItemQuantities $lastInventory.backpack
            $equipped = Get-RaidItemQuantities $lastInventory.equipped
            foreach ($key in $equipped.Keys) { Add-RaidQuantity $carried $key $equipped[$key] }
            if ($extracted -contains $id) {
                foreach ($key in $carried.Keys) { Add-RaidQuantity $expected $key $carried[$key] }
            }
            $beforeClose=$null; $verified=0; $sessionInitial=$null; $verifiedTransfers=0; $lastSourceAmount=0
            foreach ($entry in $agentLedgers) {
                if ($entry.schemaVersion -ne 2) { throw 'inventory_schema_mismatch' }
                [long]$sourceAmount = 0
                foreach ($item in $entry.source) { $sourceAmount += [long]$item.amount }
                if ($entry.stage -eq 'opened') { $sessionInitial=Get-RaidSessionQuantities $entry }
                elseif ($null -eq $sessionInitial -or !(Test-RaidQuantitiesEqual $sessionInitial (Get-RaidSessionQuantities $entry))) { throw 'session_inventory_conservation_failed' }
                if ($entry.stage -eq 'transferred' -and $sourceAmount -lt $lastSourceAmount) { $verifiedTransfers++ }
                $lastSourceAmount=$sourceAmount
                if ($entry.stage -eq 'beforeClose') { $beforeClose=$entry }
                elseif ($entry.stage -eq 'afterClose') {
                    if (!$beforeClose -or $entry.resource -ne $beforeClose.resource -or
                        !(Test-RaidQuantitiesEqual (Get-RaidItemQuantities $beforeClose.source -IncludeContainers) (Get-RaidItemQuantities $entry.source -IncludeContainers)) -or
                        !(Test-RaidQuantitiesEqual (Get-RaidItemQuantities $beforeClose.backpack -IncludeContainers) (Get-RaidItemQuantities $entry.backpack -IncludeContainers))) { throw 'inventory_close_writeback_mismatch' }
                    $verified++; $beforeClose=$null
                }
            }
            if ($verified -ne $completedSessions -or $verified -ne @($opened | Where-Object agent -eq $id).Count -or $verifiedTransfers -le 0) { throw 'inventory_close_or_transfer_evidence_missing' }
        }
        if (!(Test-RaidQuantitiesEqual $expected $actual)) { $failures.Add('warehouse_quantity_mismatch') }
        $enemyHealth = @{}; $damaged=$false; $paused=$false; $resumed=$false
        foreach ($snapshot in $snapshots) {
            if ($snapshot.inventoryOpen -and $snapshot.timeScale -eq 0) { $paused=$true }
            elseif ($paused -and $snapshot.timeScale -eq $Config.simulationSpeed) { $resumed=$true }
            foreach ($agent in $snapshot.agents) {
                if (!$agent.hasEnemy) { continue }
                $enemy=$agent.enemy
                if ($enemyHealth.ContainsKey($enemy.identity) -and $enemy.health -lt $enemyHealth[$enemy.identity]) { $damaged=$true }
                $enemyHealth[$enemy.identity]=$enemy.health
            }
        }
        $coverage['combatDamage']=$damaged; $coverage['pauseResume']=$resumed
        $coverage['capacityExtraction']=@($closed | Where-Object reason -eq 'capacity_requires_extraction').Count -gt 0
        $coverage['retaliationResume']=@($Events | Where-Object kind -eq 'directive.Resumed').Count -gt 0
        if (!$deathTerminal -and !$damaged) { $failures.Add('missing_real_combat_damage') }
        if (!$deathTerminal -and !$resumed) { $failures.Add('missing_inventory_pause_resume') }
        foreach ($event in $Events) {
            if ($event.kind -like 'directive.*') {
                $directive=$event.detail | ConvertFrom-Json
                if ($directive.commandId -like 'ManualTargetClick*') { $failures.Add('manual_directive_observed'); break }
            }
        }
    } catch { $failures.Add('invalid_completion_evidence:' + $_.Exception.Message) }
    return [pscustomobject]@{status=$(if ($failures.Count -eq 0) {'PASS'} else {'FAIL'});
        outcome=$(if ($failures.Count -gt 0) {'UNVERIFIED'} elseif ($deathTerminal) {'EXPECTED_DEATH'} else {'ALL_EXTRACTED'});
        failures=$failures.ToArray();coverage=$coverage;expected=$expected;actual=$actual}
}
Export-ModuleMember -Function Test-SceneRaidCompletion
