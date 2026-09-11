Set-StrictMode -Version Latest
function Assert-CommandFields($Value, [string[]]$Names) {
    if ($null -eq $Value) { throw 'missing_command_object' }
    $allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $Names) { [void]$allowed.Add($name) }
    $actual = @($Value.PSObject.Properties.Name)
    if ($actual.Count -ne $Names.Count) { throw 'missing_or_extra_command_fields' }
    foreach ($name in $actual) { if (!$allowed.Contains($name)) { throw "unknown_command_field:$name" } }
}
function Test-SceneRaidCommandScenario($Scenario) {
    Assert-CommandFields $Scenario @('schemaVersion','id','steps')
    if ($Scenario.schemaVersion -ne 1 -or [string]::IsNullOrWhiteSpace($Scenario.id) -or
        @($Scenario.steps).Count -lt 1 -or @($Scenario.steps).Count -gt 32) { throw 'invalid_command_scenario' }
    $known = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($step in $Scenario.steps) {
        Assert-CommandFields $step @('id','agent','route','focusAgent','target','gate','accepted','reason','gameDeadline','wallDeadline')
        Assert-CommandFields $step.target @('kind','distance','sameAsStep','excludeStep','singleton')
        Assert-CommandFields $step.gate @('kind','referenceStep')
        if ([string]::IsNullOrWhiteSpace($step.id) -or $known.Contains($step.id) -or
            ($step.agent -cnotin @('1','2') -and !($step.agent -ceq 'Focused' -and $step.route -ceq 'Focused')) -or
            $step.route -cnotin @('Explicit','Focused') -or $step.focusAgent -cnotin @('','1','2') -or
            $step.target.kind -cnotin @('Resource','ActiveEnemy','Extraction') -or $step.target.distance -cnotin @('Any','Near','Far') -or
            $step.target.singleton -isnot [bool] -or $step.accepted -isnot [bool] -or
            $step.gate.kind -cnotin @('Ready','Moving','InventoryClosed','CombatCompleted','InventoryOpen','Retaliating','Near','Completed') -or
            ($step.accepted -and $step.reason -cne 'None') -or
            (!$step.accepted -and $step.reason -cnotin @('TargetCompleted','InvalidTarget','Unreachable','NoAgent','AgentUnavailable'))) { throw "invalid_command_step:$($step.id)" }
        foreach ($value in @($step.gameDeadline,$step.wallDeadline)) {
            if ($value -is [string] -or $value -is [bool] -or $value -le 0 -or [double]::IsNaN($value) -or [double]::IsInfinity($value)) { throw 'invalid_command_deadline' }
        }
        if ($step.wallDeadline -gt 600) { throw 'command_wall_deadline_exceeded' }
        foreach ($reference in @($step.target.sameAsStep,$step.target.excludeStep,$step.gate.referenceStep)) {
            if ($reference -and !$known.Contains($reference)) { throw "unknown_command_reference:$reference" }
        }
        if ($step.gate.kind -cnotin @('Ready','Retaliating','InventoryOpen') -and !$step.gate.referenceStep) { throw 'missing_command_gate_reference' }
        if ($step.target.sameAsStep -and $step.target.excludeStep) { throw 'conflicting_command_selection' }
        [void]$known.Add($step.id)
    }
}
function Get-SceneRaidScenarioHash([string]$Json) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Json)))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
function Get-SceneRaidCommandScenario([string]$Id) {
    $catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'cluster-command-scenarios.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-CommandFields $catalog @('schemaVersion','scenarios')
    if ($catalog.schemaVersion -ne 1) { throw 'unknown_command_catalog_version' }
    $matches = @($catalog.scenarios | Where-Object { $_.id -ceq $Id })
    if ($matches.Count -ne 1) { throw "unknown_or_duplicate_command_scenario:$Id" }
    Test-SceneRaidCommandScenario $matches[0]
    $json = $matches[0] | ConvertTo-Json -Depth 16 -Compress
    Test-SceneRaidCommandScenario ($json | ConvertFrom-Json)
    return [pscustomobject]@{scenario=$matches[0];json=$json;sha256=(Get-SceneRaidScenarioHash $json)}
}
Export-ModuleMember -Function Get-SceneRaidCommandScenario,Test-SceneRaidCommandScenario,Get-SceneRaidScenarioHash
