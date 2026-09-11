param(
    [Parameter(Mandatory)][string]$RunPath,
    [ValidateRange(0,10)][double]$WarmupSeconds = 10,
    [string]$OutputPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Report.psm1') -Force
$RunPath=(Resolve-Path -LiteralPath $RunPath).Path
if (!$OutputPath) { $OutputPath=Join-Path $RunPath "performance-warm$WarmupSeconds.json" }
if (Test-Path -LiteralPath $OutputPath) { throw 'Analysis output already exists; choose a new filename to preserve evidence.' }
$all=@(Import-Csv -LiteralPath (Join-Path $RunPath 'frames.csv'))
# Validate the complete source before taking a contiguous suffix. Keep the preceding row as its boundary.
$original=Get-SceneRaidFrameStatistics $all
$first=0
while ($first -lt $all.Count -and [double]$all[$first].wallSeconds -lt $WarmupSeconds) { $first++ }
if ($first -ge $all.Count-3) { throw 'Too few frames after the fixed warmup.' }
$begin=[Math]::Max(0,$first-1)
$frames=@($all[$begin..($all.Count-1)] | ForEach-Object { $_.PSObject.Copy() })
$frames[0].intervalMs='-1'
$timing=Get-SceneRaidFrameStatistics $frames
$timing.startupIncluded=($WarmupSeconds -eq 0)
$slow=[Collections.Generic.HashSet[int]]::new()
foreach ($entry in $timing.slowFrames) { [void]$slow.Add([int]$entry.frame) }
$groups=@{}
foreach ($entry in (Import-Csv -LiteralPath (Join-Path $RunPath 'counters.csv'))) {
    if ([int]$entry.observedAtFrame -lt [int]$frames[1].frame) { continue }
    if (!$groups.ContainsKey($entry.name)) { $groups[$entry.name]=[Collections.Generic.List[object]]::new() }
    $groups[$entry.name].Add($entry)
}
$counterStats=@(foreach ($name in ($groups.Keys | Sort-Object)) {
    $valid=@($groups[$name] | Where-Object { [long]$_.previousValue -ge 0 })
    $values=@($valid | ForEach-Object { [double]$_.previousValue } | Sort-Object)
    $slowValues=@($valid | Where-Object { $slow.Contains([int]$_.observedAtFrame) } | ForEach-Object { [double]$_.previousValue })
    [pscustomobject]@{name=$name;unit=$(if($name -eq 'GC Allocated In Frame'){'bytes'}else{'nanoseconds'});
        valid=$valid.Count;missing=($groups[$name].Count-$valid.Count);
        mean=$(if($values.Count){($values|Measure-Object -Average).Average}else{$null});
        p99=$(if($values.Count){$values[[Math]::Ceiling($values.Count*.99)-1]}else{$null});
        maximum=$(if($values.Count){$values[-1]}else{$null});
        meanAtSlowIntervals=$(if($slowValues.Count){($slowValues|Measure-Object -Average).Average}else{$null})}
})
$result=Get-Content -LiteralPath (Join-Path $RunPath 'result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$config=Get-Content -LiteralPath (Join-Path $RunPath 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$events=@(Get-Content -LiteralPath (Join-Path $RunPath 'events.jsonl') -Encoding UTF8 | ForEach-Object { $_|ConvertFrom-Json } |
    Where-Object { $_.kind -like 'inventory.*' -and $_.kind -ne 'inventory.ledger' -or $_.kind -like 'directive.*' })
$worst=@($timing.slowFrames | Sort-Object ms -Descending | Select-Object -First 20 | ForEach-Object {
    $frame=$_
    [pscustomobject]@{frame=$frame;nearbyEvents=@($events | Where-Object { [Math]::Abs($_.wallSeconds-$frame.wallSeconds) -lt .1 })}
})
$analysis=[pscustomobject]@{runId=$config.runId;warmupSeconds=$WarmupSeconds;performanceAcceptance=$false;
    status=$(if ($config.simulationSpeed -ne 1 -or $result.profilerEnabled) {'DIAGNOSTIC_ONLY'} elseif ($timing.thresholdsMet) {'TIMING_THRESHOLDS_MET'} else {'PERFORMANCE_FAIL'});
    note='Fixed warmup only; includes first UI/combat interactions after that boundary. Counters are previous completed profiler samples, inclusive Total, not additive Self. Full matrix/Player acceptance is separate.';
    sourceFramesSha256=(Get-FileHash -LiteralPath (Join-Path $RunPath 'frames.csv') -Algorithm SHA256).Hash;
    timing=$timing;counterStatistics=$counterStats;worstIntervals=$worst}
$analysis | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$analysis | Select-Object runId,status,warmupSeconds,@{n='averageFps';e={$_.timing.averageFps}},@{n='p99Ms';e={$_.timing.p99Ms}},@{n='onePercentLowFps';e={$_.timing.onePercentLowFps}},@{n='maxMs';e={$_.timing.maxMs}},@{n='minimumOneSecondFps';e={$_.timing.minimumOneSecondFps}} | ConvertTo-Json
Write-Output "Analysis: $OutputPath"
