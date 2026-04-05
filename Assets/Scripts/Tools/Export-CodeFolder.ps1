[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$TargetFolder,

    [Parameter(Position = 1)]
    [string]$OutputPath = ".\code-export.txt",

    [string[]]$IncludeExtensions = @(
        ".cs", ".shader", ".hlsl", ".cginc", ".compute",
        ".ps1", ".psm1", ".psd1",
        ".py",
        ".js", ".jsx", ".ts", ".tsx",
        ".java", ".kt",
        ".c", ".cc", ".cpp", ".h", ".hh", ".hpp",
        ".go", ".rs", ".lua",
        ".php", ".rb", ".swift", ".m", ".mm"
    )
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    return $resolved.Path
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$ChildPath
    )

    $baseFullPath = Resolve-FullPath -Path $BasePath
    $childFullPath = Resolve-FullPath -Path $ChildPath

    $baseUri = New-Object System.Uri(($baseFullPath.TrimEnd("\") + "\"))
    $childUri = New-Object System.Uri($childFullPath)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($childUri).ToString()).Replace("/", "\")
}

function Get-LineCount {
    param([AllowEmptyString()][string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return 0
    }

    return ([regex]::Matches($Text, "(?:`r`n|`n|`r)").Count + 1)
}

function Get-FileText {
    param([Parameter(Mandatory = $true)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)

    if ($bytes.Length -eq 0) {
        return [string]::Empty
    }

    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
    }

    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.Encoding]::Unicode.GetString($bytes, 2, $bytes.Length - 2)
    }

    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        return [System.Text.Encoding]::BigEndianUnicode.GetString($bytes, 2, $bytes.Length - 2)
    }

    if ($bytes.Length -ge 4 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE -and $bytes[2] -eq 0x00 -and $bytes[3] -eq 0x00) {
        return [System.Text.Encoding]::UTF32.GetString($bytes, 4, $bytes.Length - 4)
    }

    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)

    try {
        return $strictUtf8.GetString($bytes)
    }
    catch [System.Text.DecoderFallbackException] {
        return [System.Text.Encoding]::Default.GetString($bytes)
    }
}

function Get-CStyleFunctionCount {
    param([AllowEmptyString()][string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return 0
    }

    $methodPattern = '(?m)^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|async|partial|new|extern|unsafe|readonly|final|inline|constexpr|const|friend|volatile|synchronized)\s+)*[A-Za-z_][\w<>\[\],\.\?:\s]*\s+[A-Za-z_]\w*\s*\([^;\n{}]*\)\s*(?:where[^{\n]+)?(?:\{|=>)'
    $constructorPattern = '(?m)^\s*(?:(?:public|private|protected|internal)\s+)+[A-Z][A-Za-z0-9_]*\s*\([^;\n{}]*\)\s*(?::[^{\n]+)?\{'

    $count = ([regex]::Matches($Text, $methodPattern)).Count
    $count += ([regex]::Matches($Text, $constructorPattern)).Count
    return $count
}

function Get-ApproxFunctionCount {
    param(
        [AllowEmptyString()][string]$Text,
        [Parameter(Mandatory = $true)][string]$Extension
    )

    if ([string]::IsNullOrEmpty($Text)) {
        return 0
    }

    $normalizedExtension = $Extension.ToLowerInvariant()

    switch ($normalizedExtension) {
        ".ps1" { return ([regex]::Matches($Text, '(?im)^\s*function\s+[A-Za-z_][\w-]*\b')).Count }
        ".psm1" { return ([regex]::Matches($Text, '(?im)^\s*function\s+[A-Za-z_][\w-]*\b')).Count }
        ".py" { return ([regex]::Matches($Text, '(?m)^\s*def\s+[A-Za-z_]\w*\s*\(')).Count }
        ".js" { return (([regex]::Matches($Text, '(?m)^\s*(?:export\s+)?(?:async\s+)?function\s+[A-Za-z_$][\w$]*\s*\(')).Count + ([regex]::Matches($Text, '(?m)^\s*(?:export\s+)?(?:const|let|var)\s+[A-Za-z_$][\w$]*\s*=\s*(?:async\s*)?\([^;\n]*\)\s*=>')).Count) }
        ".jsx" { return (([regex]::Matches($Text, '(?m)^\s*(?:export\s+)?(?:async\s+)?function\s+[A-Za-z_$][\w$]*\s*\(')).Count + ([regex]::Matches($Text, '(?m)^\s*(?:export\s+)?(?:const|let|var)\s+[A-Za-z_$][\w$]*\s*=\s*(?:async\s*)?\([^;\n]*\)\s*=>')).Count) }
        ".ts" { return (([regex]::Matches($Text, '(?m)^\s*(?:export\s+)?(?:async\s+)?function\s+[A-Za-z_$][\w$]*\s*\(')).Count + ([regex]::Matches($Text, '(?m)^\s*(?:export\s+)?(?:const|let|var)\s+[A-Za-z_$][\w$]*\s*=\s*(?:async\s*)?\([^;\n]*\)\s*=>')).Count) }
        ".tsx" { return (([regex]::Matches($Text, '(?m)^\s*(?:export\s+)?(?:async\s+)?function\s+[A-Za-z_$][\w$]*\s*\(')).Count + ([regex]::Matches($Text, '(?m)^\s*(?:export\s+)?(?:const|let|var)\s+[A-Za-z_$][\w$]*\s*=\s*(?:async\s*)?\([^;\n]*\)\s*=>')).Count) }
        ".go" { return ([regex]::Matches($Text, '(?m)^\s*func\s+(?:\([^)]+\)\s*)?[A-Za-z_]\w*\s*\(')).Count }
        ".lua" { return ([regex]::Matches($Text, '(?m)^\s*(?:local\s+)?function\s+[A-Za-z_][\w\.:]*\s*\(')).Count }
        default { return Get-CStyleFunctionCount -Text $Text }
    }
}

function Get-CodeFiles {
    param(
        [Parameter(Mandatory = $true)][string]$Folder,
        [Parameter(Mandatory = $true)][string[]]$Extensions
    )

    $normalizedExtensions = $Extensions | ForEach-Object { $_.ToLowerInvariant() }

    return Get-ChildItem -LiteralPath $Folder -File -Recurse |
        Where-Object { $normalizedExtensions -contains $_.Extension.ToLowerInvariant() } |
        Sort-Object FullName
}

$targetFolderPath = Resolve-FullPath -Path $TargetFolder

if (-not (Test-Path -LiteralPath $targetFolderPath -PathType Container)) {
    throw "Target folder does not exist: $TargetFolder"
}

$outputFullPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
}
else {
    Join-Path -Path (Get-Location) -ChildPath $OutputPath
}

$outputDirectory = Split-Path -Path $outputFullPath -Parent
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$files = @(Get-CodeFiles -Folder $targetFolderPath -Extensions $IncludeExtensions)
$entries = New-Object System.Collections.Generic.List[object]

foreach ($file in $files) {
    $content = Get-FileText -Path $file.FullName
    $relativePath = Get-RelativePath -BasePath $targetFolderPath -ChildPath $file.FullName
    $lineCount = Get-LineCount -Text $content
    $functionCount = Get-ApproxFunctionCount -Text $content -Extension $file.Extension

    $entries.Add([PSCustomObject]@{
            Name          = $file.Name
            RelativePath  = $relativePath
            FullPath      = $file.FullName
            Extension     = $file.Extension
            LineCount     = $lineCount
            FunctionCount = $functionCount
            Content       = $content
        })
}

$totalFiles = $entries.Count
$totalLines = ($entries | Measure-Object -Property LineCount -Sum).Sum
$totalFunctions = ($entries | Measure-Object -Property FunctionCount -Sum).Sum

if ($null -eq $totalLines) {
    $totalLines = 0
}

if ($null -eq $totalFunctions) {
    $totalFunctions = 0
}

$builder = New-Object System.Text.StringBuilder

[void]$builder.AppendLine("Code Export Report")
[void]$builder.AppendLine(("GeneratedAt: {0}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss")))
[void]$builder.AppendLine(("TargetFolder: {0}" -f $targetFolderPath))
[void]$builder.AppendLine(("IncludedExtensions: {0}" -f (($IncludeExtensions | Sort-Object) -join ", ")))
[void]$builder.AppendLine(("TotalFiles: {0}" -f $totalFiles))
[void]$builder.AppendLine(("TotalLines: {0}" -f $totalLines))
[void]$builder.AppendLine(("TotalFunctionsApprox: {0}" -f $totalFunctions))
[void]$builder.AppendLine("")
[void]$builder.AppendLine("FileIndex")

if ($entries.Count -eq 0) {
    [void]$builder.AppendLine("(no matching code files found)")
}
else {
    $index = 1
    foreach ($entry in $entries) {
        [void]$builder.AppendLine((
                "[{0}] {1} | lines={2} | functions={3}" -f
                $index,
                $entry.RelativePath,
                $entry.LineCount,
                $entry.FunctionCount))
        $index++
    }
}

foreach ($entry in $entries) {
    [void]$builder.AppendLine("")
    [void]$builder.AppendLine(("=" * 100))
    [void]$builder.AppendLine(("FileName: {0}" -f $entry.Name))
    [void]$builder.AppendLine(("RelativePath: {0}" -f $entry.RelativePath))
    [void]$builder.AppendLine(("FullPath: {0}" -f $entry.FullPath))
    [void]$builder.AppendLine(("Extension: {0}" -f $entry.Extension))
    [void]$builder.AppendLine(("LineCount: {0}" -f $entry.LineCount))
    [void]$builder.AppendLine(("FunctionCountApprox: {0}" -f $entry.FunctionCount))
    [void]$builder.AppendLine("-" * 100)

    if (-not [string]::IsNullOrEmpty($entry.Content)) {
        [void]$builder.Append($entry.Content)
        if (-not $entry.Content.EndsWith("`n") -and -not $entry.Content.EndsWith("`r")) {
            [void]$builder.AppendLine("")
        }
    }

    [void]$builder.AppendLine("")
}

[System.IO.File]::WriteAllText(
    $outputFullPath,
    $builder.ToString(),
    (New-Object System.Text.UTF8Encoding($true)))

Write-Host ("Export complete: {0}" -f $outputFullPath) -ForegroundColor Green
Write-Host ("Files: {0}" -f $totalFiles)
Write-Host ("Functions (approx): {0}" -f $totalFunctions)
