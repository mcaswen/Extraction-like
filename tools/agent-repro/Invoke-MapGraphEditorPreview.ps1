param([string]$WorkspaceRoot = 'D:/Unity-Projects/.agent-repro/AnomalySearchRegression', [int]$TimeoutSeconds = 300)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AgentRepro.Workspace.psm1') -Force
$source = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$output = Join-Path $source "Logs/MapGraphEditor/$runId"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$workspace = $null
$process = $null
try {
    $unity = Get-AgentReproUnity $source
    $before = Get-AgentReproSourceManifest $source
    @{runId=$runId; commit=(& git -C $source rev-parse HEAD); files=$before; visible=$true} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
    $workspace = Initialize-AgentReproWorkspace $source $WorkspaceRoot $runId
    $arguments = @('-projectPath',$workspace.Project,'-executeMethod','AgentReproduction.MapGraphEditorPreviewEntry.Run','-mapGraphPreviewOutput',$output,'-logFile',(Join-Path $output 'Editor.log'))
    $quoted = @($arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' })
    # The user authorized visible automated validation windows. This tool owns only this Editor.
    $process = Start-Process -FilePath $unity -ArgumentList $quoted -PassThru -WindowStyle Normal -WorkingDirectory $workspace.Project -RedirectStandardOutput (Join-Path $output 'stdout.log') -RedirectStandardError (Join-Path $output 'stderr.log')
    if (!$process) { throw 'Unity launch did not return an owned process handle.' }
    $ownedHandle = $process.Handle
    Write-Output "Visible editor preview PID $($process.Id): $output"
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $shutdownDeadline = $null
    while (!$process.HasExited) {
        if (!$shutdownDeadline -and ((Test-Path -LiteralPath (Join-Path $output 'result.json')) -or (Test-Path -LiteralPath (Join-Path $output 'error.txt')))) { $shutdownDeadline = [DateTime]::UtcNow.AddSeconds(60) }
        if ([DateTime]::UtcNow -gt $deadline -or ($shutdownDeadline -and [DateTime]::UtcNow -gt $shutdownDeadline)) { throw "Preview process timeout, owned PID $($process.Id)" }
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    }
    $process.WaitForExit()
    @{processId=$process.Id; exitCode=$process.ExitCode} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process-result.json') -Encoding UTF8
    if ($null -eq $process.ExitCode -or $process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $output 'result.json'))) { throw "Preview failed. See $output" }
    $after = Get-AgentReproSourceManifest $source
    if (($before | ConvertTo-Json -Compress -Depth 5) -ne ($after | ConvertTo-Json -Compress -Depth 5)) { throw 'Source inputs changed during preview.' }
    Write-Output "Preview completed: $output"
} finally {
    if ($process -and !$process.HasExited) { & taskkill /PID $process.Id /T /F | Out-Null }
    if ($workspace) { $workspace.Lock.Dispose() }
}
