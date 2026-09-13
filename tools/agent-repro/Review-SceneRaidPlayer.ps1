param([Parameter(Mandatory)][string]$RunPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Report.psm1') -Force
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$run = (Resolve-Path -LiteralPath $RunPath).Path
$allowed = [IO.Path]::GetFullPath((Join-Path $project 'Logs/SceneRaid')).TrimEnd('\')
if (!$run.StartsWith($allowed + '\', [StringComparison]::OrdinalIgnoreCase) -or
    (Split-Path $run -Leaf) -ne 'PlayerRun') { throw 'Review requires an owned SceneRaid PlayerRun directory.' }
$destination = Join-Path $run 'report-reviewed.json'
if (Test-Path -LiteralPath $destination) { throw 'Reviewed report already exists; it will not be overwritten.' }
$lock = [IO.File]::Open((Join-Path (Split-Path $run -Parent) '.player-run.lock'),
    [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    # Hash every pre-existing file. ReadOnly evaluation must not rewrite original derived reports either.
    $inputs = @(Get-ChildItem -LiteralPath $run -File | Sort-Object Name | ForEach-Object {
        [pscustomobject]@{name=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
    function Read-Json([string]$Name) { Get-Content -LiteralPath (Join-Path $run $Name) -Raw -Encoding UTF8 | ConvertFrom-Json }
    $config = Read-Json 'config.json'
    $process = Read-Json 'process.json'
    $manifest = Read-Json 'manifest.json'
    $original = Read-Json 'report.json'
    if ($config.mode -ne 'Autonomous' -or $config.simulationSpeed -ne 1 -or $config.buildPlayer -or
        $process.exitCode -ne 0 -or !$process.sourceUnchanged -or !$process.binaryUnchanged -or $process.terminationReason -or
        $config.runId -cne $manifest.runId -or $config.runId -cne $original.runId) { throw 'Not a frozen completed 1x autonomous Player run.' }
    $build = Split-Path $run -Parent
    foreach ($binary in @(
        @{path='Player/SceneRaid.exe';hash=$manifest.executableSha256},
        @{path='Player/SceneRaid_Data/Managed/Assembly-CSharp.dll';hash=$manifest.assemblySha256},
        @{path='manifest.json';hash=$manifest.buildManifestSha256})) {
        if ((Get-FileHash -LiteralPath (Join-Path $build $binary.path) -Algorithm SHA256).Hash -cne $binary.hash) {
            throw ('Build evidence changed: ' + $binary.path)
        }
    }
    Write-Output 'Recomputing frame, inventory, route and recovery contracts from frozen Player evidence...'
    $report = Test-SceneRaidEvidence $run $config $process.exitCode ($process.sourceUnchanged -and $process.binaryUnchanged) -PlayerRun -ReadOnly
    $viewport = @{required=[bool]$config.exerciseMapViewport;passed=$true;switches=0;expandedSamples=0}
    if ($viewport.required) {
        $switches = [Collections.Generic.List[object]]::new()
        Get-Content -LiteralPath (Join-Path $run 'events.jsonl') -Encoding UTF8 | ForEach-Object {
            $event = $_ | ConvertFrom-Json
            if ($event.kind -eq 'map.viewport') { $switches.Add(($event.detail | ConvertFrom-Json)) }
            elseif ($event.kind -eq 'route.frame') {
                if (($event.detail | ConvertFrom-Json).expanded) { $viewport.expandedSamples++ }
            }
        }
        $viewport.switches = $switches.Count
        $viewport.passed = $switches.Count -eq 2 -and $switches[0].expanded -and !$switches[1].expanded -and $viewport.expandedSamples -ge 20
        foreach ($row in $switches) {
            if (($row.before -join '|') -cne ($row.after -join '|') -or $row.submittedCommands -ne 0) { $viewport.passed=$false }
        }
    }
    $unchanged = @()
    foreach ($inputFile in $inputs) {
        if ((Get-FileHash -LiteralPath (Join-Path $run $inputFile.name) -Algorithm SHA256).Hash -cne $inputFile.sha256) {
            $unchanged += $inputFile.name
        }
    }
    if ($unchanged.Count -gt 0) { throw ('Review changed original input: ' + ($unchanged -join ', ')) }
    $passed = $report.evidenceStatus -eq 'PASS' -and $report.gameStatus -in @('PASS','EXPECTED_DEATH') -and
        $report.diagnosticTiming.thresholdsMet -and $viewport.passed
    $report | Add-Member -NotePropertyName mapViewportValidation -NotePropertyValue $viewport
    $report | Add-Member -NotePropertyName runAcceptance -NotePropertyValue $(if ($passed) {'PASS'} else {'FAIL'})
    $toolHashes = @(Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object Extension -in @('.psm1','.ps1') |
        Sort-Object Name | ForEach-Object { @{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
    $report | Add-Member -NotePropertyName review -NotePropertyValue @{
        schemaVersion=1;reviewedUtc=[DateTime]::UtcNow.ToString('o');originalRunAcceptance=$original.runAcceptance;
        originalGameStatus=$original.gameStatus;originalInputsUnchanged=$true;inputs=$inputs;tools=$toolHashes;
        sourceScope='Frozen original build/run manifest and process hashes; current reporting tools are separately hashed.';
        reason='Independently prove bounded same-root child recovery while preserving raw failure counts and original report.'}
    $json = $report | ConvertTo-Json -Depth 30
    $stream = [IO.File]::Open($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $bytes=[Text.UTF8Encoding]::new($false).GetBytes($json); $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
    $report | Select-Object runId,runAcceptance,evidenceStatus,gameStatus,gameErrors,behaviorFailures,recoveredRouteFailures,
        unexpectedBehaviorFailures,stagnationSuspicions,issues,@{n='averageFps';e={$_.diagnosticTiming.averageFps}} | ConvertTo-Json -Depth 4
    if (!$passed) { exit 1 }
} finally { $lock.Dispose() }
