[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$RepoPath = ".",

    [Parameter(Position = 1)]
    [string]$OutputPath = "",

    [switch]$CurrentBranchOnly,

    [switch]$Reverse
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-DirectoryPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    $item = Get-Item -LiteralPath $resolved.Path -ErrorAction Stop

    if (-not $item.PSIsContainer) {
        throw "Path is not a directory: $Path"
    }

    return $item.FullName
}

function Resolve-OutputPath {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $false)][string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return Join-Path -Path $RepositoryRoot -ChildPath "git-history-export.txt"
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path -Path (Get-Location) -ChildPath $Path
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$AllowEmptyResult
    )

    function ConvertTo-CommandLineArgument {
        param([AllowEmptyString()][string]$Value)

        if ([string]::IsNullOrEmpty($Value)) {
            return '""'
        }

        if ($Value -notmatch '[\s"]') {
            return $Value
        }

        $escaped = $Value -replace '(\\*)"', '$1$1\"'
        $escaped = $escaped -replace '(\\+)$', '$1$1'
        return '"' + $escaped + '"'
    }

    $gitExecutable = (Get-Command git -ErrorAction Stop).Source
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $gitExecutable
    $startInfo.Arguments = (($Arguments | ForEach-Object { ConvertTo-CommandLineArgument -Value $_ }) -join " ")
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $startInfo.StandardErrorEncoding = [System.Text.Encoding]::UTF8

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo

    [void]$process.Start()
    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        $message = $standardError.Trim()

        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = $standardOutput.Trim()
        }

        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = "git exited with code $($process.ExitCode)"
        }

        throw $message
    }

    if ($AllowEmptyResult) {
        return $standardOutput
    }

    return $standardOutput
}

function Get-RepositoryRoot {
    param([Parameter(Mandatory = $true)][string]$Path)

    $repoRoot = Invoke-Git -Arguments @("-C", $Path, "rev-parse", "--show-toplevel")
    return $repoRoot.Trim()
}

function Get-HistoryRecords {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][bool]$ExportCurrentBranchOnly,
        [Parameter(Mandatory = $true)][bool]$ExportReverse
    )

    $format = "%H%x1f%ad%x1f%an%x1f%ae%x1f%B%x1e"
    $arguments = @("-C", $RepositoryRoot, "log")

    if (-not $ExportCurrentBranchOnly) {
        $arguments += "--all"
    }

    if ($ExportReverse) {
        $arguments += "--reverse"
    }

    $arguments += @("--date=iso-strict", "--encoding=utf-8", "--pretty=format:$format")

    $rawLog = Invoke-Git -Arguments $arguments -AllowEmptyResult
    $records = $rawLog -split [char]0x1e | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    $parsedRecords = New-Object System.Collections.Generic.List[object]

    foreach ($record in $records) {
        $parts = $record.Trim("`r", "`n") -split [char]0x1f, 5

        if ($parts.Count -lt 5) {
            continue
        }

        $parsedRecords.Add([PSCustomObject]@{
                CommitHash = $parts[0]
                Timestamp  = $parts[1]
                AuthorName = $parts[2]
                AuthorEmail = $parts[3]
                Message    = $parts[4].TrimEnd()
            })
    }

    return $parsedRecords
}

function Build-ExportText {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$OutputFullPath,
        [Parameter(Mandatory = $true)][System.Collections.Generic.List[object]]$Records,
        [Parameter(Mandatory = $true)][bool]$ExportCurrentBranchOnly,
        [Parameter(Mandatory = $true)][bool]$ExportReverse
    )

    $builder = New-Object System.Text.StringBuilder

    [void]$builder.AppendLine("Git History Export")
    [void]$builder.AppendLine(("GeneratedAt: {0}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz")))
    [void]$builder.AppendLine(("RepositoryRoot: {0}" -f $RepositoryRoot))
    [void]$builder.AppendLine(("OutputPath: {0}" -f $OutputFullPath))
    [void]$builder.AppendLine(("Scope: {0}" -f $(if ($ExportCurrentBranchOnly) { "Current branch only" } else { "All refs" })))
    [void]$builder.AppendLine(("Order: {0}" -f $(if ($ExportReverse) { "Oldest to newest" } else { "Newest to oldest" })))
    [void]$builder.AppendLine(("TotalCommits: {0}" -f $Records.Count))
    [void]$builder.AppendLine()

    for ($index = 0; $index -lt $Records.Count; $index++) {
        $record = $Records[$index]

        [void]$builder.AppendLine(("===== Commit {0} =====" -f ($index + 1)))
        [void]$builder.AppendLine(("Hash: {0}" -f $record.CommitHash))
        [void]$builder.AppendLine(("Timestamp: {0}" -f $record.Timestamp))
        [void]$builder.AppendLine(("Author: {0} <{1}>" -f $record.AuthorName, $record.AuthorEmail))
        [void]$builder.AppendLine("Message:")

        if ([string]::IsNullOrWhiteSpace($record.Message)) {
            [void]$builder.AppendLine("(empty)")
        }
        else {
            foreach ($line in ($record.Message -split "`r`n|`r|`n")) {
                [void]$builder.AppendLine($line)
            }
        }

        [void]$builder.AppendLine()
    }

    return $builder.ToString()
}

$null = Get-Command git -ErrorAction Stop

$repositoryPath = Resolve-DirectoryPath -Path $RepoPath
$repositoryRoot = Get-RepositoryRoot -Path $repositoryPath
$outputFullPath = Resolve-OutputPath -RepositoryRoot $repositoryRoot -Path $OutputPath
$outputDirectory = Split-Path -Path $outputFullPath -Parent

if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$historyRecords = Get-HistoryRecords -RepositoryRoot $repositoryRoot -ExportCurrentBranchOnly $CurrentBranchOnly.IsPresent -ExportReverse $Reverse.IsPresent
$exportText = Build-ExportText -RepositoryRoot $repositoryRoot -OutputFullPath $outputFullPath -Records $historyRecords -ExportCurrentBranchOnly $CurrentBranchOnly.IsPresent -ExportReverse $Reverse.IsPresent
$utf8WithBom = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText($outputFullPath, $exportText, $utf8WithBom)

Write-Host ("Exported {0} commits to {1}" -f $historyRecords.Count, $outputFullPath)
