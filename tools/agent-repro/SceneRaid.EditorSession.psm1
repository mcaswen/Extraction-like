Set-StrictMode -Version Latest

function Get-SceneRaidEditorSession {
    param([string]$WorkspaceRoot, [string]$EditorPath)
    $path = Join-Path $WorkspaceRoot '.scene-raid-session.json'
    if (!(Test-Path -LiteralPath $path)) { return $null }
    $session = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    $process = Get-Process -Id $session.pid -ErrorAction SilentlyContinue
    if (!$process) { return $null }
    if ($process.StartTime.ToUniversalTime().Ticks.ToString() -ne $session.startTicks) { $process.Dispose(); return $null }
    if ($process.Path -ne $EditorPath -or $session.project -ne [IO.Path]::GetFullPath((Join-Path $WorkspaceRoot 'Project'))) {
        $process.Dispose(); throw 'Retained Editor identity does not match this workspace.'
    }
    return @{Process=$process; Info=$session}
}

function Test-SceneRaidEditorIdle {
    param([string]$Project, [int]$EditorProcessId)
    $path = Join-Path $Project '.scene-raid-run.json.editor-state.json'
    if (!(Test-Path -LiteralPath $path)) { return $false }
    try {
        $state = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
        $age = ([DateTime]::UtcNow - [DateTime]::Parse($state.utc).ToUniversalTime()).TotalSeconds
        return $state.pid -eq $EditorProcessId -and $state.idle -and $state.phase -eq 'idle' -and $age -ge 0 -and $age -lt 10
    } catch { return $false }
}

function Copy-SceneRaidEditorLog {
    param([string]$SourcePath, [string]$DestinationPath, [long]$Offset)
    if (!(Test-Path -LiteralPath $SourcePath)) { return }
    $inputStream = [IO.File]::Open($SourcePath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    try {
        $inputStream.Position = [Math]::Min($Offset,$inputStream.Length)
        $outputStream = [IO.File]::Create($DestinationPath)
        try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
    } finally { $inputStream.Dispose() }
}

Export-ModuleMember -Function Get-SceneRaidEditorSession,Test-SceneRaidEditorIdle,Copy-SceneRaidEditorLog
