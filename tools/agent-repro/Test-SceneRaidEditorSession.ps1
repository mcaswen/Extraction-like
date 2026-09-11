$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'SceneRaid.EditorSession.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'AgentRepro.Workspace.psm1') -Force
$root = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path ('Logs/SceneRaidSessionProbes/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$source = Join-Path $root 'source'
$workspace = Join-Path $root 'workspace'
$project = Join-Path $workspace 'Project'
New-Item -ItemType Directory -Path $source,$project -Force | Out-Null
$self = Get-Process -Id $PID
$session = @{pid=$self.Id;startTicks=$self.StartTime.ToUniversalTime().Ticks.ToString();project=$project;logPath=(Join-Path $root 'native.log')}
$session | ConvertTo-Json | Set-Content (Join-Path $workspace '.scene-raid-session.json') -Encoding UTF8
$resolved = Get-SceneRaidEditorSession $workspace $self.Path
if (!$resolved -or $resolved.Process.Id -ne $PID) { throw 'Matching process identity not resolved.' }
$resolved.Process.Dispose()
$passed = 1
$state = @{pid=$PID;idle=$true;phase='idle';utc=[DateTime]::UtcNow.ToString('o')}
$statePath = Join-Path $project '.scene-raid-run.json.editor-state.json'
$state | ConvertTo-Json | Set-Content $statePath -Encoding UTF8
if (!(Test-SceneRaidEditorIdle $project $PID)) { throw 'Fresh idle state rejected.' }; $passed++
foreach ($field in @('pid','idle','phase','utc')) {
    $bad = $state.Clone()
    $bad[$field] = switch ($field) { 'pid' {0} 'idle' {$false} 'phase' {'busy'} 'utc' {[DateTime]::UtcNow.AddMinutes(-1).ToString('o')} }
    $bad | ConvertTo-Json | Set-Content $statePath -Encoding UTF8
    if (Test-SceneRaidEditorIdle $project $PID) { throw "Invalid idle state accepted: $field" }; $passed++
}
@{source=$source} | ConvertTo-Json | Set-Content (Join-Path $workspace '.agent-repro-owner.json') -Encoding UTF8
$blocked = $false
try { $unexpected = Initialize-AgentReproWorkspace $source $workspace 'probe'; $unexpected.Lock.Dispose() }
catch { $blocked = $_.Exception.Message -like '*retained SceneRaid Editor*' }
if (!$blocked -or (Test-Path (Join-Path $project 'Assets'))) { throw 'Busy workspace was not protected before mirroring.' }; $passed++
$session.startTicks = '1'
$session | ConvertTo-Json | Set-Content (Join-Path $workspace '.scene-raid-session.json') -Encoding UTF8
if ($null -ne (Get-SceneRaidEditorSession $workspace $self.Path)) { throw 'Recycled PID accepted.' }; $passed++
[IO.File]::WriteAllText((Join-Path $root 'native.log'),'previous-current')
Copy-SceneRaidEditorLog (Join-Path $root 'native.log') (Join-Path $root 'slice.log') 9
if ([IO.File]::ReadAllText((Join-Path $root 'slice.log')) -ne 'current') { throw 'Per-run log slice includes previous run.' }; $passed++
$self.Dispose()
@{status='PASS';probes=$passed} | ConvertTo-Json | Set-Content (Join-Path $root 'result.json') -Encoding UTF8
Write-Output "PASS $passed editor session probes: $root"
