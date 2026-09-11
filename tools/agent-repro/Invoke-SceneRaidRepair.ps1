param([string]$UnityPath, [string]$WorkspaceRoot, [int]$TimeoutSeconds = 900)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'AgentRepro.Workspace.psm1') -Force
$source = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$WorkspaceRoot) { $WorkspaceRoot = Join-Path (Split-Path $source -Parent) '.agent-repro/AnomalySearch' }
$runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$output = Join-Path $source "Logs/SceneRaidRepair/$runId"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$workspace = $null
$process = $null
try {
    $editor = Get-AgentReproUnity $source $UnityPath
    $sceneRelative = 'Assets/Scenes/Scene_DB/Scenezl_Final 1.unity'
    $scene = Join-Path $source $sceneRelative
    $sceneHash = (Get-FileHash -LiteralPath $scene).Hash
    $before = Get-AgentReproSourceManifest $source
    @{runId=$runId;commit=(& git -C $source rev-parse HEAD);files=$before} | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
    Copy-Item -LiteralPath $scene -Destination (Join-Path $output 'scene-before.unity')
    $workspace = Initialize-AgentReproWorkspace $source $WorkspaceRoot $runId
    $configPath = Join-Path $workspace.Project '.scene-raid-run.json'
    @{schemaVersion=1;runId=$runId;outputPath=$output;scenePath=$sceneRelative;mode='Audit';observeSeconds=60;simulationSpeed=1} |
        ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
    $arguments = @('-batchmode','-projectPath',$workspace.Project,'-executeMethod',
        'AnomalySearch.Editor.SceneRaid.SceneRaidSceneRepairEntry.RunBatch','-sceneRaidConfig',$configPath,'-logFile',(Join-Path $output 'Editor.log'))
    $quoted = @($arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' })
    Write-Output "SceneRaid repair starting: $output"
    $process = Start-Process -FilePath $editor -ArgumentList $quoted -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (!$process.HasExited) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Owned repair process timed out.' }
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    }
    if ($process.ExitCode -ne 0) { throw "Repair process exited $($process.ExitCode)." }
    $result = Get-Content -LiteralPath (Join-Path $output 'repair-result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($result.status -ne 'PASS' -or $result.after -ne 1 -or $result.repeatedChanges -ne 0) { throw 'Repair validation failed.' }
    $after = Get-AgentReproSourceManifest $source
    if (($before | ConvertTo-Json -Depth 5 -Compress) -ne ($after | ConvertTo-Json -Depth 5 -Compress)) { throw 'Source inputs changed; no source write performed.' }
    $staged = Join-Path $output 'scene-staged.unity'
    Copy-Item -LiteralPath (Join-Path $workspace.Project $sceneRelative) -Destination $staged
    if ((Get-FileHash -LiteralPath $scene).Hash -ne $sceneHash) { throw 'Source scene changed before apply.' }
    [IO.File]::Replace($staged, $scene, (Join-Path $output 'scene-replace-backup.unity'))
    @{status='APPLIED';runId=$runId;sceneBefore=$sceneHash;sceneAfter=(Get-FileHash -LiteralPath $scene).Hash} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'applied.json') -Encoding UTF8
    $result | ConvertTo-Json -Depth 5 | Write-Output
} catch {
    $_.Exception.ToString() | Set-Content -LiteralPath (Join-Path $output 'launcher-error.txt') -Encoding UTF8
    Write-Error $_ -ErrorAction Continue
    exit 1
} finally {
    if ($process -and !$process.HasExited) { & taskkill /PID $process.Id /T /F | Out-Null }
    if ($process) { $process.Dispose() }
    if ($workspace) { $workspace.Lock.Dispose() }
}
