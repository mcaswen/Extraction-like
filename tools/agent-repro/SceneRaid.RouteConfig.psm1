Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.CommandConfig.psm1')
function Test-SceneRaidRouteScenario($Scenario) {
    $fields=@('schemaVersion','id','agent','nearMinimum','farMinimum','movementBeforeReplacement','wallDeadline','operations')
    if ((@($Scenario.PSObject.Properties.Name | Sort-Object) -join '|') -cne (@($fields | Sort-Object) -join '|')) { throw 'invalid_route_scenario_fields' }
    if ($Scenario.schemaVersion -ne 1 -or [string]::IsNullOrWhiteSpace($Scenario.id) -or $Scenario.agent -cnotin @('1','2') -or
        (@($Scenario.operations) -join '|') -cne 'Near|FarCrossZone|InvalidPreserves') { throw 'invalid_route_scenario' }
    foreach ($name in @('nearMinimum','farMinimum','movementBeforeReplacement','wallDeadline')) {
        $v=$Scenario.$name
        if ($v -is [string] -or $v -is [bool] -or $v -le 0 -or [double]::IsNaN($v) -or [double]::IsInfinity($v)) { throw "invalid_route_number:$name" }
    }
    if ($Scenario.farMinimum -le $Scenario.nearMinimum -or $Scenario.wallDeadline -gt 120) { throw 'invalid_route_limits' }
}
function Get-SceneRaidRouteScenario([string]$Id) {
    $catalog=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'map-command-scenarios.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($catalog.schemaVersion -ne 1) { throw 'unknown_route_catalog_version' }
    $matches=@($catalog.scenarios | Where-Object id -CEQ $Id)
    if ($matches.Count -ne 1) { throw "unknown_route_scenario:$Id" }
    Test-SceneRaidRouteScenario $matches[0]
    $json=$matches[0] | ConvertTo-Json -Depth 8 -Compress
    return [pscustomobject]@{scenario=$matches[0];json=$json;sha256=(Get-SceneRaidScenarioHash $json)}
}
Export-ModuleMember -Function Get-SceneRaidRouteScenario,Test-SceneRaidRouteScenario
