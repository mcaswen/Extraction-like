@echo off
setlocal EnableExtensions EnableDelayedExpansion

for /f "delims=" %%I in ('git rev-parse --show-toplevel 2^>nul') do set "REPO_ROOT=%%I"
if not defined REPO_ROOT (
    echo [错误] 当前目录不在 Git 仓库中，或 Git 未正确安装
    echo [提示] 请将以上完整输出截图或复制，联系程序组处理
    exit /b 1
)

pushd "%REPO_ROOT%" >nul

set "CURRENT_BRANCH="
for /f "delims=" %%I in ('git branch --show-current 2^>nul') do set "CURRENT_BRANCH=%%I"

if not defined CURRENT_BRANCH (
    echo [错误] 当前未检测到有效分支，可能处于 detached HEAD 状态
    echo [提示] 请将以上完整输出截图或复制，联系程序组处理
    goto :fail
)

echo.
echo ===== 当前仓库 =====
echo %REPO_ROOT%

echo.
echo ===== 当前分支 =====
echo %CURRENT_BRANCH%

call :run_command "拉取最新版本" git pull --rebase --autostash

echo.
echo [完成] 拉取成功
goto :success

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
    echo [提示] 请将以上完整报错信息截图或复制，联系程序组处理。
    del /f /q "%STDOUT_FILE%" >nul 2>nul
    del /f /q "%STDERR_FILE%" >nul 2>nul
    goto :fail
)

del /f /q "%STDOUT_FILE%" >nul 2>nul
del /f /q "%STDERR_FILE%" >nul 2>nul
goto :eof

:fail
popd >nul
pause
exit /b 1

:success
popd >nul
pause
exit /b 0