param(
    [ValidateSet('Diagnose','Regression')][string]$Mode='Regression',
    [ValidateSet('Smoke','Core','Risks','All')][string]$Suite='Smoke',
    [string]$Group,
    [string]$TestFilter,
    [ValidateRange(1,100)][int]$Repeat=1,
    [string]$UnityPath,
    [string]$WorkspaceRoot,
    [ValidateRange(1,7200)][int]$TimeoutSeconds=2700,
    [switch]$IncludeGraphics,
    [ValidateSet('None','Assertion','Timeout')][string]$FaultProbe='None'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'AgentRepro.Workspace.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'AgentRepro.Report.psm1') -Force
$source = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$WorkspaceRoot) { $WorkspaceRoot = Join-Path (Split-Path $source -Parent) '.agent-repro/AnomalySearch' }
$runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$output = Join-Path $source "Logs/AgentReproduction/$runId"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$workspace = $null
$process = $null
try {
    $editor = Get-AgentReproUnity $source $UnityPath
    $manifest = [ordered]@{runId=$runId; mode=$Mode; source=$source; unity=$editor; commit=(& git -C $source rev-parse HEAD); dirty=@(& git -C $source status --porcelain); files=(Get-AgentReproSourceManifest $source)}
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
    $workspace = Initialize-AgentReproWorkspace $source $WorkspaceRoot $runId
    $catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'cases.json') -Raw | ConvertFrom-Json
    $groups = @($catalog.groups | Where-Object { if ($Group) { $_.name -eq $Group } else { $Suite -eq 'All' -or $_.suite -eq $Suite -or $_.name -eq 'Smoke' } })
    if ($groups.Count -eq 0) { throw "Unknown group/suite: $Group / $Suite" }
    $results = [System.Collections.Generic.List[object]]::new()
    for ($iteration=1; $iteration -le $Repeat; $iteration++) {
        foreach ($entry in $groups) {
            $groupOutput = Join-Path $output "groups/$($entry.name)-$iteration"
            New-Item -ItemType Directory -Path $groupOutput -Force | Out-Null
            $context = @{outputPath=$output; runId=$runId; group=$entry.name; repeat=$iteration; seed=731; graphics=[bool]$IncludeGraphics; faultProbe=$FaultProbe}
            $context | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $workspace.Project '.agent-repro-run.json') -Encoding UTF8
            $filter = if ($TestFilter) {$TestFilter} else {$entry.filter}
            $arguments = @('-batchmode','-projectPath',$workspace.Project,'-runTests','-testPlatform','EditMode','-testFilter',$filter,'-testResults',(Join-Path $groupOutput 'test-results.xml'),'-logFile',(Join-Path $groupOutput 'Editor.log'))
            if (!$IncludeGraphics) { $arguments += '-nographics' }
            $quoted = @($arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' })
            Write-Output "Starting $($entry.name) repeat ${iteration}: $groupOutput"
            $process = Start-Process -FilePath $editor -ArgumentList $quoted -PassThru -WindowStyle Hidden
            $processId = $process.Id
            $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
            $timedOut = $false
            $shutdownDeadline = $null
            $forcedShutdown = $false
            while (!$process.HasExited) {
                if (!$shutdownDeadline -and (Test-Path -LiteralPath (Join-Path $groupOutput 'test-results.xml'))) {
                    $shutdownDeadline = [DateTime]::UtcNow.AddSeconds(60)
                }
                if ($shutdownDeadline -and [DateTime]::UtcNow -gt $shutdownDeadline) {
                    $forcedShutdown = $true
                    & taskkill /PID $processId /T /F | Out-Null
                    $process.WaitForExit(10000) | Out-Null
                    @{execution='SHUTDOWN_TIMEOUT';processId=$processId;reason='NUnit completed but owned editor did not exit within 60 seconds.'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $groupOutput 'shutdown-timeout.json') -Encoding UTF8
                    break
                }
                if ([DateTime]::UtcNow -gt $deadline) {
                    $timedOut = $true
                    # Only this exact owned Unity process tree, never another editor.
                    & taskkill /PID $processId /T /F | Out-Null
                    $process.WaitForExit(10000) | Out-Null
                    break
                }
                Start-Sleep -Milliseconds 500
                $process.Refresh()
            }
            $exitCode = if ($process.HasExited) {$process.ExitCode} else {-1}
            $results.Add(@{name=$entry.name;repeat=$iteration;output=$groupOutput;expected=$(if ($TestFilter) {@()} else {@($entry.expected)});exitCode=$exitCode;timedOut=$timedOut;forcedShutdown=$forcedShutdown})
            $process.Dispose(); $process=$null
        }
    }
    $after = Get-AgentReproSourceManifest $source
    if (($manifest.files | ConvertTo-Json -Compress -Depth 5) -ne ($after | ConvertTo-Json -Compress -Depth 5)) { throw 'Source inputs changed during the test run.' }
    $resultCode = Write-AgentReproReport $output $Mode $results.ToArray()
    Write-Output "Report: $output/report.md"
    exit $resultCode
} catch {
    @{execution='ENVIRONMENT_ERROR';error=$_.Exception.ToString()} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'error.json') -Encoding UTF8
    Write-Error $_ -ErrorAction Continue
    exit 2
} finally {
    if ($process -and !$process.HasExited) { & taskkill /PID $process.Id /T /F | Out-Null }
    if ($workspace) { $workspace.Lock.Dispose() }
}
