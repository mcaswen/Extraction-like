Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.Contracts.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.ClusterCommands.Contracts.psm1') -Force
function Get-SceneRaidFrameStatistics {
    param([object[]]$Frames, [ValidateRange(1,1000)][double]$TargetFps = 60)
    $frameBudgetMs = 1000.0 / $TargetFps
    $maximumBudgetMs = 2 * $frameBudgetMs
    # CSV uses six decimal places for time; allow only serialization-scale rounding at the boundary.
    $timeToleranceMs = 0.001
    if ($Frames.Count -lt 4) { throw 'Too few frame samples.' }
    $intervals = [Collections.Generic.List[double]]::new()
    $times = [Collections.Generic.List[double]]::new()
    $slow = [Collections.Generic.List[object]]::new()
    $lastNumber = -1
    $lastWall = -1.0
    $lastGame = -1.0
    foreach ($frame in $Frames) {
        $number = [int]$frame.frame
        $wall = [double]::Parse($frame.wallSeconds, [Globalization.CultureInfo]::InvariantCulture)
        $game = [double]::Parse($frame.gameSeconds, [Globalization.CultureInfo]::InvariantCulture)
        $ms = [double]::Parse($frame.intervalMs, [Globalization.CultureInfo]::InvariantCulture)
        $scale = [double]::Parse($frame.timeScale, [Globalization.CultureInfo]::InvariantCulture)
        foreach ($value in @($wall,$game,$ms,$scale)) { if ([double]::IsNaN($value) -or [double]::IsInfinity($value)) { throw 'Non-finite frame value.' } }
        if ($number -le $lastNumber -or ($lastNumber -ge 0 -and $number -ne $lastNumber + 1) -or $wall -le $lastWall -or $game -lt $lastGame -or $scale -lt 0) { throw 'Non-monotonic or missing frame/time values.' }
        if ($lastNumber -ge 0) {
            if ($ms -le 0 -or [Math]::Abs(($wall - $lastWall) * 1000 - $ms) -gt 0.02) { throw 'Inconsistent frame interval.' }
            $intervals.Add($ms)
            $times.Add($wall)
            if ($ms -gt $frameBudgetMs + $timeToleranceMs) { $slow.Add(@{frame=$number;wallSeconds=$wall;ms=$ms}) }
        } elseif ($ms -ne -1) { throw 'First frame has no preceding interval.' }
        $lastNumber=$number; $lastWall=$wall; $lastGame=$game
    }
    $ordered = @($intervals | Sort-Object)
    $total = ($intervals | Measure-Object -Sum).Sum
    $tailCount = [Math]::Max(1,[Math]::Ceiling($ordered.Count * 0.01))
    $tailMean = ($ordered | Select-Object -Last $tailCount | Measure-Object -Average).Average
    $minWindow = $null
    $windows = 0
    $left = 0; $right = 0
    for ($start = $times[0]; $start + 1 -le $times[-1]; $start += 0.25) {
        # Both ends use the same half-microsecond offset, matching the CSV's rounding precision.
        while ($left -lt $times.Count -and $times[$left] -lt $start - 0.0000005) { $left++ }
        if ($right -lt $left) { $right=$left }
        while ($right -lt $times.Count -and $times[$right] -lt $start + 1 - 0.0000005) { $right++ }
        $fps = $right-$left
        if ($null -eq $minWindow -or $fps -lt $minWindow) { $minWindow=$fps }
        $windows++
    }
    $averageFps = $intervals.Count * 1000.0 / $total
    $p99 = $ordered[[Math]::Ceiling($ordered.Count * .99) - 1]
    $maximum = $ordered[-1]
    $low = 1000.0/$tailMean
    return [pscustomobject]@{targetFps=$TargetFps;frameBudgetMs=$frameBudgetMs;maximumBudgetMs=$maximumBudgetMs;
        validIntervals=$intervals.Count;averageFps=$averageFps;p99Ms=$p99;maxMs=$maximum;onePercentLowFps=$low;
        minimumOneSecondFps=$minWindow;windowCount=$windows;startupIncluded=$true;slowFrameCount=$slow.Count;slowFrames=$slow.ToArray();
        acceptancePolicy='AverageFpsGreaterThanTarget';tailMetricsDiagnosticOnly=$true;
        # Round floating point summation noise so exactly 60 FPS is not mislabeled as greater than 60.
        thresholdsMet=([Math]::Round($averageFps,6) -gt $TargetFps)}
}

function Test-SceneRaidEvidence {
    param([string]$OutputPath, [object]$Config, [Nullable[int]]$ExitCode, [bool]$SourceUnchanged,
        [switch]$EditorRetained, [int]$EditorProcessId, [switch]$PlayerRun)
    $issues = [Collections.Generic.List[string]]::new()
    if ($PlayerRun -and $EditorRetained) { $issues.Add('player_cannot_be_retained_editor') }
    if ($EditorRetained) {
        try {
            $ready = Get-Content -LiteralPath (Join-Path $OutputPath 'editor-ready.json') -Raw -Encoding UTF8 -ErrorAction Stop | ConvertFrom-Json
            if ($ready.runId -ne $Config.runId -or $ready.pid -ne $EditorProcessId -or !$ready.idle -or
                $ready.phase -ne 'idle' -or $ready.exitCode -ne 0 -or $null -ne $ExitCode) { $issues.Add('retained_editor_not_ready') }
        } catch { $issues.Add('retained_editor_ready_missing_or_invalid') }
    } elseif ($null -eq $ExitCode -or $ExitCode -ne 0) { $issues.Add("process_exit:$ExitCode") }
    if (!$SourceUnchanged) { $issues.Add('source_changed') }
    $auditPath = Join-Path $OutputPath 'scene-audit.json'
    if (!(Test-Path -LiteralPath $auditPath)) { $issues.Add('missing_audit') }
    else {
        try {
            $audit = Get-Content -LiteralPath $auditPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($audit.runId -ne $Config.runId -or $audit.scene -ne $Config.scenePath -or $audit.status -ne 'AUDITED') { $issues.Add('audit_mismatch') }
        } catch { $issues.Add('invalid_audit') }
    }
    $gameErrors = 0
    $behaviorFailures = 0
    $expectedRejections = 0
    $warnings = 0
    $stagnations = 0
    $timing = $null
    $counterSummaries = @()
    $result = $null
    $completion = $null
    if ($Config.PSObject.Properties['buildPlayer'] -and $Config.buildPlayer) {
        try {
            $build = Get-Content -LiteralPath (Join-Path $OutputPath 'build-result.json') -Raw -Encoding UTF8 -ErrorAction Stop | ConvertFrom-Json
            $expectedExe = [IO.Path]::GetFullPath((Join-Path $OutputPath 'Player/SceneRaid.exe'))
            if ($build.schemaVersion -ne 1 -or $build.runId -ne $Config.runId -or $build.scenePath -ne $Config.scenePath -or
                $build.status -ne 'BUILT' -or $build.errors -ne 0 -or $build.bytes -le 0 -or $build.seconds -le 0 -or
                $build.backend -ne 'Mono2x' -or $build.target -ne 'StandaloneWindows64' -or $build.optionsBits -ne 1 -or
                $build.effectiveOptionsBits -ne 1 -or $build.quality -ne 'High Fidelity' -or
                (@($build.defines) -join ',') -ne 'ANOMALY_SCENE_AUTOMATION') { $issues.Add('build_mismatch_or_failed') }
            if ([IO.Path]::GetFullPath($build.executable) -ne $expectedExe -or !(Test-Path -LiteralPath $expectedExe -PathType Leaf) -or
                (Get-Item -LiteralPath $expectedExe -ErrorAction SilentlyContinue).Length -le 0) { $issues.Add('build_executable_missing_or_mismatch') }
        } catch { $issues.Add('invalid_or_missing_build:' + $_.Exception.Message) }
    }
    if ($Config.mode -in @('Observe','Autonomous','ManualCluster')) {
        try {
            $result = Get-Content -LiteralPath (Join-Path $OutputPath 'result.json') -Raw -Encoding UTF8 -ErrorAction Stop | ConvertFrom-Json
            if ($result.schemaVersion -ne 1 -or $result.runId -ne $Config.runId -or $result.mode -ne $Config.mode -or
                $result.scenePath -ne $Config.scenePath) { $issues.Add('result_mismatch') }
            if ($PlayerRun -and ($result.runtime -ne 'Player' -or !$result.developmentBuild -or
                $result.quality -ne 'High Fidelity' -or $result.profilerEnabled -or $result.vSyncCount -ne 0 -or
                $result.targetFrameRate -ne -1 -or [string]::IsNullOrWhiteSpace($result.graphicsApi) -or
                $result.graphicsApi -eq 'Null')) { $issues.Add('player_environment_mismatch') }
            $allowedStatus = if ($Config.mode -eq 'Observe') { @('OBSERVED') } else { @('BEHAVIOR_BLOCKED','RAID_OBSERVED_COMPLETE','RAID_OBSERVED_FAILURE') }
            if ($result.status -notin $allowedStatus) { $issues.Add('run_status_invalid') }
            if ($result.frames -lt 4 -or $result.renderedFrames -lt $result.frames - 2 -or $result.renderedFrames -gt $result.frames -or $result.batchMode) { $issues.Add('no_graphical_frames') }
            if ($result.screenWidth -ne $Config.width -or $result.screenHeight -ne $Config.height -or
                $result.cameraWidth -ne $Config.width -or $result.cameraHeight -ne $Config.height) { $issues.Add('resolution_mismatch') }
            if ($result.lostEvents -ne 0) { $issues.Add('lost_events') }
            if ($Config.mode -eq 'Observe' -and $result.elapsedWallSeconds -lt $Config.observeSeconds) { $issues.Add('observation_truncated') }
            $gameErrors = $result.errors
            $warnings = $result.warnings
            $events = @(Get-Content -LiteralPath (Join-Path $OutputPath 'events.jsonl') -Encoding UTF8 -ErrorAction Stop | ForEach-Object { $_ | ConvertFrom-Json })
            if ($events.Count -ne $result.events -or $events.Count -lt 2 -or $events[0].kind -ne 'bootstrap.beforeSceneLoad' -or $events[-1].kind -ne 'run.completed') { $issues.Add('event_stream_incomplete') }
            for ($i = 0; $i -lt $events.Count; $i++) { if ($events[$i].sequence -ne $i + 1) { $issues.Add('event_sequence_gap'); break } }
            $behaviorFailures = @($events | Where-Object { $_.kind -eq 'directive.Failed' -or $_.kind -eq 'directive.Rejected' }).Count
            $stagnations = @($events | Where-Object kind -eq 'contract.movementStagnationSuspected').Count
            if ($issues.Contains('no_graphical_frames')) {
                throw 'Rendering evidence is invalid; raw counters are retained but timing aggregation is not applicable.'
            }
            $frames = @(Import-Csv -LiteralPath (Join-Path $OutputPath 'frames.csv') -ErrorAction Stop)
            if ($frames.Count -ne $result.frames) { $issues.Add('frame_stream_incomplete') }
            $timing = Get-SceneRaidFrameStatistics $frames
            if ([Math]::Abs([double]$frames[-1].wallSeconds - $result.elapsedWallSeconds) -gt 1) { $issues.Add('frame_duration_mismatch') }
            if ($result.counters.Count -eq 0 -or @($result.counters | Where-Object { $_ -like '*unavailable*' }).Count -gt 0) { $issues.Add('counter_unavailable') }
            foreach ($description in $result.counters) {
                if ($description -notlike '* | TimeNanoseconds' -and $description -ne 'GC Allocated In Frame | Memory | Bytes') { $issues.Add('counter_unit_unknown') }
            }
            $counterRows = @(Import-Csv -LiteralPath (Join-Path $OutputPath 'counters.csv') -ErrorAction Stop)
            if ($counterRows.Count -ne $frames.Count * $result.counters.Count) { $issues.Add('counter_stream_incomplete') }
            $describedNames = @($result.counters | ForEach-Object { ($_ -split ' \| ')[0] } | Sort-Object)
            $requiredNames = @('PlayerLoop','GC Allocated In Frame','Anomaly.Discovery.Update','Anomaly.Pawn.Update','Anomaly.Zone.Update',
                'Anomaly.Discovery.ScanAgent','Anomaly.Pawn.Facts','Anomaly.Pawn.Lifecycle','Anomaly.Pawn.Brain',
                'Anomaly.Zone.State','Anomaly.Zone.Shape','Anomaly.Navigation.Check',
                'Anomaly.Cluster.LateUpdate','Anomaly.Cluster.State','Anomaly.Cluster.Input','Anomaly.Cluster.Range',
                'Anomaly.Range.Geometry','Anomaly.Range.Projection','Anomaly.Range.Line','Anomaly.Ground.Raycast','Anomaly.Ground.Filter') | Sort-Object
            if (($describedNames -join ',') -ne ($requiredNames -join ',')) { $issues.Add('required_counters_missing_or_duplicated') }
            $sampledNames = @($counterRows.name | Sort-Object -Unique)
            if (($describedNames -join ',') -ne ($sampledNames -join ',')) { $issues.Add('counter_name_mismatch') }
            $counterSummaries = @($counterRows | Group-Object name | ForEach-Object {
                if (($_.Group.observedAtFrame -join ',') -ne ($frames.frame -join ',')) { $issues.Add('counter_frame_mismatch:' + $_.Name) }
                $valid = @($_.Group | Where-Object { [long]$_.previousValue -ge 0 })
                if ($valid.Count -eq 0 -or @($valid | Where-Object { [long]$_.previousCalls -gt 0 }).Count -eq 0) { $issues.Add('counter_no_samples:' + $_.Name) }
                [pscustomobject]@{name=$_.Name;validSamples=$valid.Count;
                    averageRaw=$(if ($valid.Count -gt 0) {($valid | Measure-Object previousValue -Average).Average} else {$null});
                    maxRaw=$(if ($valid.Count -gt 0) {($valid | Measure-Object previousValue -Maximum).Maximum} else {$null});
                    averageCalls=$(if ($valid.Count -gt 0) {($valid | Measure-Object previousCalls -Average).Average} else {$null})}
            })
            if ((@($result.observedAgents | Sort-Object) -join ',') -ne '1,2') { $issues.Add('expected_agents_missing') }
            if ($Config.mode -eq 'Autonomous') {
                $completion = Test-SceneRaidCompletion $OutputPath $Config $events $result
                $completion | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputPath 'contracts.json') -Encoding UTF8
            }
            elseif ($Config.mode -eq 'ManualCluster') {
                $completion = Test-SceneRaidClusterCompletion $OutputPath $Config $events $result
                $expectedRejections = $completion.expectedRejections
                if (@($completion.failures | Where-Object { $_ -like 'invalid_*' }).Count) { $issues.Add('invalid_command_or_carried_evidence') }
                $completion | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath (Join-Path $OutputPath 'contracts.json') -Encoding UTF8
            }
        } catch { $issues.Add('invalid_or_missing_evidence:' + $_.Exception.Message) }
    }
    return [pscustomobject]@{
        schemaVersion=1; runId=$Config.runId; mode=$Config.mode
        editorLifecycle=$(if ($PlayerRun) {'PLAYER_EXITED'} elseif ($EditorRetained) {'EDITOR_RETAINED'} else {'PROCESS_EXITED'});processExitCode=$ExitCode
        evidenceStatus=$(if ($issues.Count -eq 0) {'PASS'} else {'FAIL'})
        gameStatus=$(if (($result -and $result.status -eq 'BEHAVIOR_BLOCKED') -or $gameErrors -gt 0 -or ($behaviorFailures-$expectedRejections) -gt 0 -or $stagnations -gt 0 -or ($completion -and $completion.status -eq 'FAIL')) {'ISSUES_OBSERVED'} elseif ($issues.Count -eq 0 -and $completion -and $completion.status -eq 'PASS') { if ($completion.outcome -eq 'EXPECTED_DEATH') {'EXPECTED_DEATH'} else {'PASS'} } else {'NOT_FULL_RAID_VALIDATED'})
        completionContracts=$completion
        expectedRejections=$expectedRejections; unexpectedBehaviorFailures=($behaviorFailures-$expectedRejections)
        coverageStatus=$(if($Config.mode -eq 'ManualCluster' -and $completion){$completion.coverageStatus}else{'NOT_APPLICABLE'})
        gameErrors=$gameErrors; behaviorFailures=$behaviorFailures; warnings=$warnings;stagnationSuspicions=$stagnations;diagnosticTiming=$timing;counterSummaries=$counterSummaries
        performanceAcceptance=$false; issues=$issues.ToArray()
    }
}
Export-ModuleMember -Function Test-SceneRaidEvidence,Get-SceneRaidFrameStatistics
