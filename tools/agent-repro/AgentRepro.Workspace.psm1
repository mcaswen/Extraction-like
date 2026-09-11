Set-StrictMode -Version Latest

function Get-AgentReproUnity {
    param([string]$SourceRoot, [string]$UnityPath)
    $version = (Get-Content (Join-Path $SourceRoot 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion: ').Line.Split(':')[1].Trim()
    $candidates = @($UnityPath, "D:\UnityEditors\Unity $version\Editor\Unity.exe", "D:\UnityEditors\$version\Editor\Unity.exe", "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe")
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            $productVersion = (Get-Item -LiteralPath $candidate).VersionInfo.ProductVersion
            if ($productVersion.StartsWith($version)) { return (Resolve-Path -LiteralPath $candidate).Path }
        }
    }
    throw "No matching Unity $version editor found."
}

function Get-AgentReproSourceManifest {
    param([string]$SourceRoot)
    $entries = [System.Collections.Generic.List[object]]::new()
    foreach ($directory in @('Assets','Packages','ProjectSettings','tools/agent-repro')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $SourceRoot $directory) -File -Recurse | Sort-Object FullName) {
            $relative = $file.FullName.Substring($SourceRoot.Length + 1).Replace('\','/')
            if ($file.Length -lt 1024 -and $file.Extension -ne '.meta') {
                $first = Get-Content -LiteralPath $file.FullName -TotalCount 1 -ErrorAction SilentlyContinue
                if ($first -eq 'version https://git-lfs.github.com/spec/v1') { throw "Missing LFS resource: $relative" }
            }
            $entries.Add([ordered]@{path=$relative; bytes=$file.Length; sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash})
        }
    }
    return ,$entries.ToArray()
}

function Initialize-AgentReproWorkspace {
    param([string]$SourceRoot, [string]$WorkspaceRoot, [string]$RunId, [int]$AllowedEditorPid = 0)
    $source = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
    $workspace = [IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd('\')
    if ($workspace -eq $source -or $source.StartsWith($workspace + '\') -or $workspace.StartsWith($source + '\')) { throw 'Workspace must be outside the source project.' }
    $marker = Join-Path $workspace '.agent-repro-owner.json'
    if (Test-Path -LiteralPath $workspace) {
        if (!(Test-Path -LiteralPath $marker)) { throw "Refusing unowned workspace: $workspace" }
        $owner = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
        if ($owner.source -ne $source) { throw 'Workspace belongs to a different source project.' }
    } else {
        New-Item -ItemType Directory -Path $workspace -Force | Out-Null
        @{source=$source} | ConvertTo-Json | Set-Content -LiteralPath $marker -Encoding UTF8
    }
    $lockPath = Join-Path $workspace '.agent-repro.lock'
    try { $lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
    catch { throw 'Another agent-repro run owns this workspace.' }
    try {
        $project = Join-Path $workspace 'Project'
        $sessionPath = Join-Path $workspace '.scene-raid-session.json'
        if (Test-Path -LiteralPath $sessionPath) {
            $session = Get-Content -LiteralPath $sessionPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $retained = Get-Process -Id $session.pid -ErrorAction SilentlyContinue
            if ($retained) {
                try {
                    if ($retained.StartTime.ToUniversalTime().Ticks.ToString() -eq $session.startTicks -and $retained.Id -ne $AllowedEditorPid) {
                        throw 'This workspace has a retained SceneRaid Editor. Reuse its session or use another regression workspace.'
                    }
                } finally { $retained.Dispose() }
            }
        }
        foreach ($directory in @('Assets','Packages','ProjectSettings','tools/agent-repro')) {
            $destination = [IO.Path]::GetFullPath((Join-Path $project $directory))
            if (!$destination.StartsWith($workspace + '\') -or $destination -eq $source) { throw "Unsafe mirror destination: $destination" }
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            & robocopy (Join-Path $source $directory) $destination /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
            if ($LASTEXITCODE -ge 8) { throw "Copy failed: $directory ($LASTEXITCODE)" }
        }
        $settingsPath = Join-Path $project 'ProjectSettings/ProjectSettings.asset'
        $settings = [IO.File]::ReadAllText($settingsPath)
        $settings = [regex]::Replace($settings, '(?m)^  companyName:.*$', '  companyName: AnomalySearch.Automation')
        $settings = [regex]::Replace($settings, '(?m)^  productName:.*$', "  productName: AgentRepro_$RunId")
        [IO.File]::WriteAllText($settingsPath, $settings, [Text.UTF8Encoding]::new($false))
        return @{Project=$project; Lock=$lock; Root=$workspace}
    } catch { $lock.Dispose(); throw }
}

Export-ModuleMember -Function Get-AgentReproUnity,Get-AgentReproSourceManifest,Initialize-AgentReproWorkspace
