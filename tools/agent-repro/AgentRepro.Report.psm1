Set-StrictMode -Version Latest

function Write-AgentReproReport {
    param([string]$OutputPath, [string]$Mode, [object[]]$Groups)
    $runs = [System.Collections.Generic.List[object]]::new()
    $infrastructureFailed = $false
    $businessFailed = $false
    foreach ($group in $Groups) {
        $xmlPath = Join-Path $group.output 'test-results.xml'
        $tests = @()
        if (Test-Path -LiteralPath $xmlPath) {
            try {
                [xml]$xml = Get-Content -LiteralPath $xmlPath -Raw
                $tests = @($xml.SelectNodes('//test-case'))
            } catch { $infrastructureFailed = $true }
        }
        $expected = @($group.expected)
        $missing = @($expected | Where-Object { $id = $_; !($tests | Where-Object { $_.fullname -eq $id -or $_.name -eq $id }) })
        $failed = @($tests | Where-Object { $_.result -ne 'Passed' })
        foreach ($missingId in $missing) {
            $missingDirectory = Join-Path $OutputPath "missing/$($group.name)-$($group.repeat)"
            New-Item -ItemType Directory -Path $missingDirectory -Force | Out-Null
            $safeId = $missingId -replace '[^A-Za-z0-9_.-]', '_'
            @{caseId=$missingId;execution=$(if ($group.timedOut) {'TIMED_OUT'} else {'NOT_RUN'});contract='UNSPECIFIED';reason='NUnit result missing; never count as passing.'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $missingDirectory "$safeId.json") -Encoding UTF8
        }
        foreach ($test in $tests) {
            $caseId = [string]$test.fullname
            $algorithm = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($caseId))).Replace('-','').Substring(0,16) }
            finally { $algorithm.Dispose() }
            $caseDirectory = Join-Path $OutputPath "cases/$hash/$($group.repeat)"
            New-Item -ItemType Directory -Path $caseDirectory -Force | Out-Null
            $failureNode = $test.SelectSingleNode('failure/message')
            $message = if ($failureNode) {$failureNode.InnerText} else {''}
            $execution = if ($message -match '^SetUp\s*:') {'SETUP_FAILED'} elseif ($message -match 'TearDown') {'CLEANUP_FAILED'} else {'COMPLETED'}
            if ($execution -ne 'COMPLETED') { $infrastructureFailed = $true }
            $contract = if ($test.result -eq 'Passed') {'PASS'} else {'FAIL'}
            @{caseId=$caseId;execution=$execution;contract=$contract;message=$message;nunitResult=[string]$test.result} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $caseDirectory 'nunit-final.json') -Encoding UTF8
        }
        if ($group.timedOut -or $tests.Count -eq 0 -or $missing.Count -gt 0) { $infrastructureFailed = $true }
        if ($group.forcedShutdown) { $infrastructureFailed = $true }
        if ($group.exitCode -ne 0 -and $failed.Count -eq 0) { $infrastructureFailed = $true }
        if ($failed.Count -gt 0) { $businessFailed = $true }
        $runs.Add([ordered]@{group=$group.name; repeat=$group.repeat; exitCode=$group.exitCode; timedOut=$group.timedOut; total=$tests.Count; failed=$failed.Count; missing=$missing; output=$group.output})
    }
    $exitCode = if ($infrastructureFailed) {2} elseif ($businessFailed -and $Mode -eq 'Regression') {1} else {0}
    $summary = [ordered]@{mode=$Mode; infrastructureFailed=$infrastructureFailed; businessFailed=$businessFailed; exitCode=$exitCode; groups=$runs.ToArray()}
    $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputPath 'summary.json') -Encoding UTF8
    $lines = @('# Agent reproduction run', '', "Mode: $Mode. Exit: $exitCode. Infrastructure failure: $infrastructureFailed. Behavior failure: $businessFailed.", '', '| Group | Repeat | Executed | Failed | Missing | Timeout |', '| --- | --- | --- | --- | --- | --- |')
    foreach ($run in $runs) { $lines += "| $($run.group) | $($run.repeat) | $($run.total) | $($run.failed) | $(@($run.missing).Count) | $($run.timedOut) |" }
    $lines += @('', 'Diagnose exit 0 means evidence collection completed, not that gameplay passed.', 'Original NUnit XML and Editor logs are retained in groups/. Each test writes independent JSON/JSONL evidence in cases/.')
    $lines | Set-Content -LiteralPath (Join-Path $OutputPath 'report.md') -Encoding UTF8
    return $exitCode
}

Export-ModuleMember -Function Write-AgentReproReport
