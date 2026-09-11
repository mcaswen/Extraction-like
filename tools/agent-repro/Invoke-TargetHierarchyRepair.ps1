param(
    [string]$UnityPath,
    [string]$WorkspaceRoot,
    [ValidateRange(60,3600)][int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'AgentRepro.Workspace.psm1') -Force
$source = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$WorkspaceRoot) { $WorkspaceRoot = Join-Path (Split-Path $source -Parent) '.agent-repro/AnomalySearch' }
$runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$output = Join-Path $source "Logs/TargetHierarchyRepair/$runId"
$sceneRelative = 'Assets/Scenes/Scene_DB/Scenezl_Final 1.unity'
$materialRelative = 'Assets/Art/Materials/Raid/M_TargetRangeLine.mat'
$scenePath = Join-Path $source $sceneRelative
$protectedPaths = @($sceneRelative, 'Assets/Scenes/Scene_DB/New Terrain 1.asset', 'Assets/Scenes/Scene_DB/Scenezl_Final 1/NavMesh-NavMesh Surface.asset')
$workspace = $null
$process = $null
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Invoke-OwnedUnity([string[]]$Arguments, [string]$Label) {
    $quoted = @($Arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' })
    Write-Output "Starting ${Label}: $output"
    $script:process = Start-Process -FilePath $editor -ArgumentList $quoted -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (!$script:process.HasExited) {
        if ([DateTime]::UtcNow -gt $deadline) { throw "$Label timed out (owned PID $($script:process.Id))." }
        Start-Sleep -Milliseconds 500
        $script:process.Refresh()
    }
    $exitCode = $script:process.ExitCode
    $script:process.Dispose()
    $script:process = $null
    if ($exitCode -ne 0) { throw "$Label failed with exit code $exitCode. See $output" }
}
try {
    $editor = Get-AgentReproUnity $source $UnityPath
    $before = @{}
    foreach ($relative in $protectedPaths) { $before[$relative] = (Get-FileHash -LiteralPath (Join-Path $source $relative) -Algorithm SHA256).Hash }
    foreach ($relative in @($materialRelative, "$materialRelative.meta")) {
        $path = Join-Path $source $relative
        $before[$relative] = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { $null }
    }
    Copy-Item -LiteralPath $scenePath -Destination (Join-Path $output 'scene-before.unity')
    $sourceFiles = Get-AgentReproSourceManifest $source
    @{runId=$runId; commit=(& git -C $source rev-parse HEAD); sourceBefore=$before; files=$sourceFiles} | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
    $workspace = Initialize-AgentReproWorkspace $source $WorkspaceRoot $runId
    $testsPath = Join-Path $output 'test-results.xml'
    Invoke-OwnedUnity @('-batchmode','-projectPath',$workspace.Project,'-runTests','-testPlatform','EditMode',
        '-testFilter','AgentReproduction.Tests.TargetHierarchyRepairTests','-testResults',$testsPath,
        '-targetHierarchyOutput',$output,'-logFile',(Join-Path $output 'tests-Editor.log')) 'hierarchy tests'
    if (!(Test-Path -LiteralPath $testsPath)) { throw 'Missing NUnit results.' }
    [xml]$tests = Get-Content -LiteralPath $testsPath -Raw
    if ($tests.'test-run'.result -ne 'Passed' -or [int]$tests.'test-run'.total -ne 7) { throw 'Expected all 7 hierarchy tests to pass.' }
    Invoke-OwnedUnity @('-batchmode','-projectPath',$workspace.Project,'-executeMethod',
        'AnomalySearch.Editor.GameplayTargets.TargetHierarchyRepairEntry.RunBatch',
        '-targetHierarchyOutput',$output,'-logFile',(Join-Path $output 'repair-Editor.log')) 'scene repair'
    $result = Get-Content -LiteralPath (Join-Path $output 'repair-result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($result.status -ne 'PASS') { throw 'Scene validation did not pass.' }
    if (!$result.repair.PSObject.Properties['changes'] -or !$result.repair.PSObject.Properties['warnings']) {
        throw 'Repair evidence is missing serialized changes or warnings.'
    }
    $sourceAfterTests = Get-AgentReproSourceManifest $source
    if (($sourceFiles | ConvertTo-Json -Compress -Depth 5) -ne ($sourceAfterTests | ConvertTo-Json -Compress -Depth 5)) {
        throw 'Source inputs changed during validation; preserving latest user files.'
    }
    foreach ($relative in $before.Keys) {
        $path = Join-Path $source $relative
        $current = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { $null }
        if ($current -ne $before[$relative]) { throw "Source changed during repair; preserving latest user file: $relative" }
    }
    foreach ($relative in $protectedPaths | Where-Object { $_ -ne $sceneRelative }) {
        if ((Get-FileHash -LiteralPath (Join-Path $workspace.Project $relative) -Algorithm SHA256).Hash -ne $before[$relative]) {
            throw "Repair unexpectedly modified terrain or NavMesh: $relative"
        }
    }
    # 先让新引用的材质落盘，再替换场景；使用同目录临时文件和 Replace，避免半写入场景。
    foreach ($relative in @($materialRelative, "$materialRelative.meta")) {
        Copy-Item -LiteralPath (Join-Path $workspace.Project $relative) -Destination (Join-Path $source $relative) -Force
    }
    $stagedScene = Join-Path $output 'scene-staged.unity'
    Copy-Item -LiteralPath (Join-Path $workspace.Project $sceneRelative) -Destination $stagedScene
    if ((Get-FileHash -LiteralPath $scenePath -Algorithm SHA256).Hash -ne $before[$sceneRelative]) {
        Remove-Item -LiteralPath $stagedScene
        throw 'Source scene changed before atomic replacement; latest scene was preserved.'
    }
    [IO.File]::Replace($stagedScene, $scenePath, (Join-Path $output 'scene-replace-backup.unity'))
    @{status='PASS';runId=$runId;sceneBefore=$before[$sceneRelative];sceneAfter=(Get-FileHash -LiteralPath $scenePath -Algorithm SHA256).Hash;tests=7} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'applied.json') -Encoding UTF8
    Write-Output "Applied and verified: $sceneRelative"
    Write-Output "Evidence: $output"
} catch {
    $_.Exception.ToString() | Set-Content -LiteralPath (Join-Path $output 'error.txt') -Encoding UTF8
    Write-Error $_ -ErrorAction Continue
    exit 1
} finally {
    if ($process -and !$process.HasExited) { & taskkill /PID $process.Id /T /F | Out-Null }
    if ($workspace) { $workspace.Lock.Dispose() }
}
