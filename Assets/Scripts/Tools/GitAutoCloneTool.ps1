$ErrorActionPreference = "Stop"

function Show-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "===== $Message =====" -ForegroundColor Cyan
}

function Fail-WithMessage {
    param([string]$Message)

    Write-Host ""
    Write-Host "----- 报错信息 -----" -ForegroundColor Red
    Write-Host $Message -ForegroundColor Red
    Write-Host ""
    Write-Host "[提示] 请把以上完整输出发给程序组" -ForegroundColor Yellow
    exit 1
}

function Refresh-Path {
    $machinePath = [System.Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath = [System.Environment]::GetEnvironmentVariable("Path", "User")
    $env:Path = "$machinePath;$userPath"
}

function Get-CommandPath {
    param([string]$Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command -and $command.Source) {
        return $command.Source
    }

    return $null
}

function Get-GitExecutable {
    $gitExe = Get-CommandPath "git"
    if ($gitExe) {
        return $gitExe
    }

    $candidates = @(
        "$Env:ProgramFiles\Git\cmd\git.exe",
        "$Env:ProgramFiles\Git\bin\git.exe",
        "${Env:ProgramFiles(x86)}\Git\cmd\git.exe",
        "${Env:ProgramFiles(x86)}\Git\bin\git.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    return $null
}

function Run-External {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        $joined = $Arguments -join " "
        throw "命令执行失败：$FilePath $joined"
    }
}

function Start-Installer {
    param(
        [string]$InstallerPath,
        [string[]]$Arguments
    )

    $process = Start-Process -FilePath $InstallerPath -ArgumentList $Arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        $joined = $Arguments -join " "
        throw "安装器执行失败：$InstallerPath $joined (ExitCode=$($process.ExitCode))"
    }
}

function Get-RepoNameFromUrl {
    param([string]$RepoUrl)

    $trimmed = $RepoUrl.Trim().TrimEnd("/")
    $lastSegment = ($trimmed -split "[:/]" | Select-Object -Last 1)

    if ([string]::IsNullOrWhiteSpace($lastSegment)) {
        throw "无法从仓库地址推断本地文件夹名，请手动输入"
    }

    if ($lastSegment.EndsWith(".git")) {
        return $lastSegment.Substring(0, $lastSegment.Length - 4)
    }

    return $lastSegment
}

function Ensure-GitInstalled {
    $gitExe = Get-GitExecutable
    if ($gitExe) {
        return $gitExe
    }

    $wingetExe = Get-CommandPath "winget"

    if (-not $wingetExe) {
        Show-Step "尝试注册 App Installer（用于恢复 winget）"
        try {
            Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe
        }
        catch {
            Write-Host "[提示] App Installer 自动注册未成功，继续使用直接下载安装方案"
        }

        Refresh-Path
        $wingetExe = Get-CommandPath "winget"
    }

    if ($wingetExe) {
        Show-Step "通过 winget 安装 Git for Windows"
        Run-External $wingetExe @(
            "install",
            "--id", "Git.Git",
            "-e",
            "--source", "winget",
            "--accept-source-agreements",
            "--accept-package-agreements"
        )

        Refresh-Path
        $gitExe = Get-GitExecutable
        if ($gitExe) {
            return $gitExe
        }
    }

    Show-Step "下载并静默安装 Git for Windows"
    $headers = @{ "User-Agent" = "bootstrap-clone-script" }
    $release = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/git-for-windows/git/releases/latest"

    $asset = $release.assets |
        Where-Object { $_.name -match '^Git-.*-64-bit\.exe$' } |
        Select-Object -First 1

    if (-not $asset) {
        throw "未找到适合当前机器的 Git for Windows 安装包"
    }

    $tempDir = Join-Path $env:TEMP "git-bootstrap"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    $installerPath = Join-Path $tempDir $asset.name
    Invoke-WebRequest -Headers $headers -Uri $asset.browser_download_url -OutFile $installerPath

    Start-Installer $installerPath @(
        "/VERYSILENT",
        "/NORESTART",
        "/NOCANCEL",
        "/SP-",
        "/CLOSEAPPLICATIONS",
        "/RESTARTAPPLICATIONS"
    )

    Refresh-Path
    $gitExe = Get-GitExecutable
    if (-not $gitExe) {
        throw "Git 安装完成后仍未找到 git.exe"
    }

    return $gitExe
}

try {
    $gitExe = Ensure-GitInstalled

    Show-Step "验证 Git"
    Run-External $gitExe @("--version")

    Show-Step "验证 Git LFS"
    try {
        Run-External $gitExe @("lfs", "version")
    }
    catch {
        throw "未检测到 Git LFS。Git for Windows 正常情况下会自带 Git LFS，请把这段输出发给程序组"
    }

    $currentName = ""
    $currentEmail = ""

    try { $currentName = (& $gitExe "config" "--global" "--get" "user.name").Trim() } catch {}
    try { $currentEmail = (& $gitExe "config" "--global" "--get" "user.email").Trim() } catch {}

    Write-Host ""
    $userName = Read-Host "请输入 Git 用户名 [当前: $currentName]"
    if ([string]::IsNullOrWhiteSpace($userName)) {
        $userName = $currentName
    }

    $userEmail = Read-Host "请输入 Git 邮箱 [当前: $currentEmail]"
    if ([string]::IsNullOrWhiteSpace($userEmail)) {
        $userEmail = $currentEmail
    }

    if ([string]::IsNullOrWhiteSpace($userName) -or [string]::IsNullOrWhiteSpace($userEmail)) {
        throw "Git 用户名和邮箱不能为空"
    }

    $repoUrl = Read-Host "请输入远程仓库地址"
    if ([string]::IsNullOrWhiteSpace($repoUrl)) {
        throw "远程仓库地址不能为空"
    }

    $defaultParent = (Get-Location).Path
    $parentDirectory = Read-Host "请输入克隆到的父目录 [默认: $defaultParent]"
    if ([string]::IsNullOrWhiteSpace($parentDirectory)) {
        $parentDirectory = $defaultParent
    }

    if (-not (Test-Path $parentDirectory)) {
        Show-Step "创建目标父目录"
        New-Item -ItemType Directory -Path $parentDirectory -Force | Out-Null
    }

    $defaultRepoName = Get-RepoNameFromUrl -RepoUrl $repoUrl
    $repoFolderName = Read-Host "请输入本地文件夹名 [默认: $defaultRepoName]"
    if ([string]::IsNullOrWhiteSpace($repoFolderName)) {
        $repoFolderName = $defaultRepoName
    }

    $clonePath = Join-Path $parentDirectory $repoFolderName
    if (Test-Path $clonePath) {
        throw "目标目录已存在：$clonePath"
    }

    Show-Step "写入 Git 全局配置"
    Run-External $gitExe @("config", "--global", "user.name", $userName)
    Run-External $gitExe @("config", "--global", "user.email", $userEmail)

    Show-Step "初始化 Git LFS（全局）"
    Run-External $gitExe @("lfs", "install")

    $targetBranch = "dev"

    Show-Step "克隆远程仓库"
    Run-External $gitExe @("clone", $repoUrl, $clonePath)

    Show-Step "获取并切换到远程分支：$targetBranch"
    Push-Location $clonePath
    try {
        Run-External $gitExe @("fetch", "origin", $targetBranch)
        Run-External $gitExe @("checkout", "-b", $targetBranch, "origin/$targetBranch")

        Run-External $gitExe @("lfs", "install", "--local")
        Run-External $gitExe @("lfs", "pull")
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    Write-Host "[完成] 仓库已克隆到：$clonePath" -ForegroundColor Green
    Write-Host "[完成] Git / Git LFS / 用户配置已初始化完成" -ForegroundColor Green
}
catch {
    Fail-WithMessage $_.Exception.Message
}