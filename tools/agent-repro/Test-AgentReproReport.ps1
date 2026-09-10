param([string]$OutputPath = (Join-Path $PSScriptRoot ('../../Logs/AgentReproduction/ReportProbe-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))))
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AgentRepro.Report.psm1') -Force
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
$checks = @(
    @{name='Passed'; mode='Regression'; result='Passed'; processExit=0; expectedExit=0},
    @{name='CrashAfterXml'; mode='Regression'; result='Passed'; processExit=17; expectedExit=2},
    @{name='Assertion'; mode='Regression'; result='Failed'; processExit=2; expectedExit=1},
    @{name='DiagnoseFailure'; mode='Diagnose'; result='Failed'; processExit=2; expectedExit=0},
    @{name='Missing'; mode='Regression'; result='Missing'; processExit=0; expectedExit=2},
    @{name='Timeout'; mode='Regression'; result='Passed'; processExit=0; timedOut=$true; expectedExit=2},
    @{name='Cleanup'; mode='Regression'; result='Failed'; message='TearDown : cleanup failed'; processExit=2; expectedExit=2}
)
$evidence = @()
foreach ($check in $checks) {
    $directory = Join-Path $OutputPath $check.name
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    if ($check.result -ne 'Missing') {
        $failure = ''
        if ($check.result -eq 'Failed') {
            $message = if ($check.message) {$check.message} else {'Deliberate assertion failure'}
            $failure = '<failure><message>' + $message + '</message></failure>'
        }
        $xml = '<test-run><test-case fullname="Probe.Expected" name="Expected" result="' + $check.result + '">' + $failure + '</test-case></test-run>'
        $xml | Set-Content -LiteralPath (Join-Path $directory 'test-results.xml') -Encoding UTF8
    }
    $group = @{name='Probe';repeat=1;output=$directory;expected=@('Expected');exitCode=$check.processExit;timedOut=[bool]$check.timedOut;forcedShutdown=$false}
    $actual = Write-AgentReproReport -OutputPath $directory -Mode $check.mode -Groups @($group)
    if ($actual -ne $check.expectedExit) { throw "$($check.name): expected exit $($check.expectedExit), received $actual" }
    $evidence += @{case=$check.name;expected=$check.expectedExit;actual=$actual;passed=$true}
}
$evidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputPath 'checks.json') -Encoding UTF8
Write-Output "Report contract checks passed: $($checks.Count)/$($checks.Count). Evidence: $OutputPath"
