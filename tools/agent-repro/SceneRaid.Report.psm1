Set-StrictMode -Version Latest
function Test-SceneRaidEvidence {
    param([string]$OutputPath, [object]$Config, [int]$ExitCode, [bool]$SourceUnchanged)
    $issues = [Collections.Generic.List[string]]::new()
    if ($ExitCode -ne 0) { $issues.Add("process_exit:$ExitCode") }
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
    $warnings = 0
    $result = $null
    if ($Config.mode -eq 'Observe') {
        try {
            $result = Get-Content -LiteralPath (Join-Path $OutputPath 'result.json') -Raw -Encoding UTF8 -ErrorAction Stop | ConvertFrom-Json
            if ($result.schemaVersion -ne 1 -or $result.runId -ne $Config.runId -or $result.mode -ne $Config.mode -or
                $result.scenePath -ne $Config.scenePath -or $result.status -ne 'OBSERVED') { $issues.Add('result_mismatch') }
            if ($result.frames -lt 4 -or $result.renderedFrames -lt 2 -or $result.batchMode) { $issues.Add('no_graphical_frames') }
            if ($result.screenWidth -ne $Config.width -or $result.screenHeight -ne $Config.height -or
                $result.cameraWidth -ne $Config.width -or $result.cameraHeight -ne $Config.height) { $issues.Add('resolution_mismatch') }
            if ($result.lostEvents -ne 0) { $issues.Add('lost_events') }
            if ($result.elapsedWallSeconds -lt $Config.observeSeconds) { $issues.Add('observation_truncated') }
            $gameErrors = $result.errors
            $warnings = $result.warnings
            $events = @(Get-Content -LiteralPath (Join-Path $OutputPath 'events.jsonl') -Encoding UTF8 -ErrorAction Stop | ForEach-Object { $_ | ConvertFrom-Json })
            if ($events.Count -ne $result.events -or $events.Count -lt 2 -or $events[0].kind -ne 'bootstrap.beforeSceneLoad' -or $events[-1].kind -ne 'run.completed') { $issues.Add('event_stream_incomplete') }
            for ($i = 0; $i -lt $events.Count; $i++) { if ($events[$i].sequence -ne $i + 1) { $issues.Add('event_sequence_gap'); break } }
            $behaviorFailures = @($events | Where-Object { $_.kind -eq 'directive.Failed' -or $_.kind -eq 'directive.Rejected' }).Count
            $frames = @(Import-Csv -LiteralPath (Join-Path $OutputPath 'frames.csv') -ErrorAction Stop)
            if ($frames.Count -ne $result.frames) { $issues.Add('frame_stream_incomplete') }
            if ((@($result.observedAgents | Sort-Object) -join ',') -ne '1,2') { $issues.Add('expected_agents_missing') }
        } catch { $issues.Add('invalid_or_missing_evidence:' + $_.Exception.Message) }
    }
    return [pscustomobject]@{
        schemaVersion=1; runId=$Config.runId; mode=$Config.mode
        evidenceStatus=$(if ($issues.Count -eq 0) {'PASS'} else {'FAIL'})
        gameStatus=$(if ($gameErrors -gt 0 -or $behaviorFailures -gt 0) {'ISSUES_OBSERVED'} else {'NOT_FULL_RAID_VALIDATED'})
        gameErrors=$gameErrors; behaviorFailures=$behaviorFailures; warnings=$warnings
        performanceAcceptance=$false; issues=$issues.ToArray()
    }
}
Export-ModuleMember -Function Test-SceneRaidEvidence
