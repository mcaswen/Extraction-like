@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem 允许提交的目录
set "ALLOWED_DIRS=Assets/Art Assets/Audio Assets/Scenes Assets/SO Assets/Docs"

for /f "delims=" %%I in ('git rev-parse --show-toplevel 2^>nul') do set "REPO_ROOT=%%I"
if not defined REPO_ROOT (
    echo [错误] 当前目录不在 Git 仓库中，或 Git 未正确安装
    echo 请将以上信息截图或复制，联系程序组处理
    exit /b 1
)

pushd "%REPO_ROOT%" >nul

set "ALL_CHANGES_FILE=%temp%\git_all_changes_%random%_%random%.txt"
set "OUTSIDE_CHANGES_FILE=%temp%\git_outside_changes_%random%_%random%.txt"
set "STAGED_FILES_FILE=%temp%\git_staged_files_%random%_%random%.txt"

break > "%ALL_CHANGES_FILE%"
break > "%OUTSIDE_CHANGES_FILE%"
break > "%STAGED_FILES_FILE%"

call :run_command "拉取最新代码" git pull --rebase --autostash

git diff --name-only >> "%ALL_CHANGES_FILE%"
git diff --cached --name-only >> "%ALL_CHANGES_FILE%"
git ls-files --others --exclude-standard >> "%ALL_CHANGES_FILE%"

set "HAS_ANY_CHANGE=0"
set "HAS_OUTSIDE_CHANGE=0"

for /f "usebackq delims=" %%F in ("%ALL_CHANGES_FILE%") do (
    if not "%%F"=="" (
        set "HAS_ANY_CHANGE=1"
        call :is_allowed "%%F"
        if "!IS_ALLOWED!"=="0" (
            echo %%F>> "%OUTSIDE_CHANGES_FILE%"
            set "HAS_OUTSIDE_CHANGE=1"
        )
    )
)

if "!HAS_ANY_CHANGE!"=="0" (
    echo.
    echo [提示] 当前没有可提交的改动
    goto :success
)

echo.
echo ===== 当前检测到的改动概览 =====
git status --short

echo.
echo ===== 当前检测到的改动文件 =====
type "%ALL_CHANGES_FILE%"

set "STAGE_ALL=0"

if "!HAS_OUTSIDE_CHANGE!"=="1" (
    echo.
    echo ===== 检测到非目标文件夹改动 =====
    type "%OUTSIDE_CHANGES_FILE%"
    echo.
    echo y = 回退这些非目标文件夹改动，只提交目标文件夹
    echo n = 保留这些改动，并一起提交
    set /p "USER_CHOICE=是否回退这些非目标文件夹的改动？(y/n): "

    if /I "!USER_CHOICE!"=="Y" (
        echo.
        echo ===== 正在回退非目标文件夹改动 =====
        for /f "usebackq delims=" %%F in ("%OUTSIDE_CHANGES_FILE%") do (
            if not "%%F"=="" (
                git ls-files --error-unmatch -- "%%F" >nul 2>nul
                if errorlevel 1 (
                    call :run_command "移除未跟踪文件或目录：%%F" git clean -fd -- "%%F"
                ) else (
                    call :run_command "回退文件：%%F" git restore --staged --worktree -- "%%F"
                )
            )
        )
    ) else (
        set "STAGE_ALL=1"
        echo.
        echo [警告] 本次将一并提交非目标文件夹的改动。
        echo [警告] 请在提交信息中说明原因。
    )
)

echo.
echo ===== 暂存改动 =====
if "!STAGE_ALL!"=="1" (
    call :run_command "暂存全部改动" git add -A
) else (
    set "HAS_STAGE_TARGET=0"
    for %%D in (%ALLOWED_DIRS%) do (
        if exist "%%D" (
            call :run_command "暂存目录：%%D" git add -A -- "%%D"
            set "HAS_STAGE_TARGET=1"
        )
    )

    if "!HAS_STAGE_TARGET!"=="0" (
        echo [错误] 没有找到任何允许提交的目录，请检查脚本顶部 ALLOWED_DIRS 配置。
        echo 请将以上信息截图或复制，联系程序组处理
        goto :fail
    )
)

git diff --cached --quiet
if not errorlevel 1 goto :has_staged_changes

echo.
echo [提示] 暂存区没有可提交内容
goto :success

:has_staged_changes
echo.
echo ===== 当前将要提交的改动概览 =====
git status --short

echo.
echo ===== 当前将要提交的所有文件 =====
git diff --cached --name-only > "%STAGED_FILES_FILE%"
type "%STAGED_FILES_FILE%"

echo.
if "!STAGE_ALL!"=="1" (
    echo [提示] 本次包含非目标文件夹改动，请在提交信息里写明原因
)

set /p "COMMIT_MESSAGE=请输入提交信息: "
if "%COMMIT_MESSAGE%"=="" (
    echo [错误] 提交信息不能为空
    goto :fail
)

call :run_command "创建提交" git commit -m "%COMMIT_MESSAGE%"
call :run_command "推送到远程" git push

echo.
echo [完成] 提交并推送成功
goto :success

:is_allowed
set "IS_ALLOWED=0"
for %%D in (%ALLOWED_DIRS%) do (
    echo %~1 | findstr /B /I /C:"%%D/" >nul
    if not errorlevel 1 (
        set "IS_ALLOWED=1"
        goto :eof
    )
    echo %~1 | findstr /I /X /C:"%%D" >nul
    if not errorlevel 1 (
        set "IS_ALLOWED=1"
        goto :eof
    )
)
goto :eof

:run_command
set "STEP_NAME=%~1"
shift

set "STDOUT_FILE=%temp%\git_stdout_%random%_%random%.txt"
set "STDERR_FILE=%temp%\git_stderr_%random%_%random%.txt"

echo.
echo ===== %STEP_NAME% =====

call %* > "%STDOUT_FILE%" 2> "%STDERR_FILE%"
set "EXIT_CODE=%ERRORLEVEL%"

for %%A in ("%STDOUT_FILE%") do (
    if %%~zA gtr 0 (
        type "%STDOUT_FILE%"
    )
)

if not "%EXIT_CODE%"=="0" (
    echo.
    echo ----- Git 报错信息 -----
    for %%A in ("%STDERR_FILE%") do (
        if %%~zA gtr 0 (
            type "%STDERR_FILE%"
        ) else (
            echo [无额外 stderr 输出]
        )
    )
    echo.
    echo [提示] 请将以上完整报错信息截图或复制，联系程序组处理
    del /f /q "%STDOUT_FILE%" >nul 2>nul
    del /f /q "%STDERR_FILE%" >nul 2>nul
    goto :fail
)

del /f /q "%STDOUT_FILE%" >nul 2>nul
del /f /q "%STDERR_FILE%" >nul 2>nul
goto :eof

:fail
if exist "%ALL_CHANGES_FILE%" del /f /q "%ALL_CHANGES_FILE%" >nul 2>nul
if exist "%OUTSIDE_CHANGES_FILE%" del /f /q "%OUTSIDE_CHANGES_FILE%" >nul 2>nul
if exist "%STAGED_FILES_FILE%" del /f /q "%STAGED_FILES_FILE%" >nul 2>nul
popd >nul
pause
exit /b 1

:success
if exist "%ALL_CHANGES_FILE%" del /f /q "%ALL_CHANGES_FILE%" >nul 2>nul
if exist "%OUTSIDE_CHANGES_FILE%" del /f /q "%OUTSIDE_CHANGES_FILE%" >nul 2>nul
if exist "%STAGED_FILES_FILE%" del /f /q "%STAGED_FILES_FILE%" >nul 2>nul
popd >nul
pause
exit /b 0