Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Settlement.psm1')
function Test-SceneRaidCompletion {
    param([string]$OutputPath, $Config, [object[]]$Events, $Result)
    $assessment = Test-SceneRaidSettlement $OutputPath $Config $Events $Result
    if ($assessment.status -ne 'PASS') { return $assessment }
    $failures = [Collections.Generic.List[string]]::new()
    $deathTerminal = $Result.status -eq 'RAID_OBSERVED_FAILURE'
    foreach ($id in @('1','2')) {
        if (!$deathTerminal -and (!$assessment.coverage["movement:$id"] -or !$assessment.coverage["inventory:$id"])) { $failures.Add("missing_agent_activity:$id") }
        if ($assessment.coverage["ledger:$id"] -and !$assessment.coverage["verifiedTransfer:$id"]) { $failures.Add("inventory_transfer_evidence_missing:$id") }
    }
    if (!$deathTerminal -and !$assessment.coverage['combatDamage']) { $failures.Add('missing_real_combat_damage') }
    if (!$deathTerminal -and !$assessment.coverage['pauseResume']) { $failures.Add('missing_inventory_pause_resume') }
    foreach ($event in $Events) {
        if ($event.kind -like 'directive.*') {
            $directive = $event.detail | ConvertFrom-Json
            if ($directive.commandId -like 'ManualTargetClick*') { $failures.Add('manual_directive_observed'); break }
        }
    }
    if ($failures.Count -gt 0) { $assessment.status='FAIL'; $assessment.outcome='UNVERIFIED'; $assessment.failures=$failures.ToArray() }
    return $assessment
}
Export-ModuleMember -Function Test-SceneRaidCompletion
