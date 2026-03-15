@echo off
setlocal EnableExtensions EnableDelayedExpansion

for /f "delims=" %%I in ('git rev-parse --show-toplevel 2^>nul') do set "REPO_ROOT=%%I"
if not defined REPO_ROOT (
    echo [Error] The current directory is not inside a Git repository, or Git is not installed correctly.
    echo [Hint] Please screenshot or copy the full output above and contact the programming team.
    exit /b 1
)

pushd "%REPO_ROOT%" >nul

set "CURRENT_BRANCH="
for /f "delims=" %%I in ('git branch --show-current 2^>nul') do set "CURRENT_BRANCH=%%I"

if not defined CURRENT_BRANCH (
    echo [Error] No valid branch was detected. The repository may be in a detached HEAD state.
    echo [Hint] Please screenshot or copy the full output above and contact the programming team.
    goto :fail
)

echo.
echo ===== Current repository =====
echo %REPO_ROOT%

echo.
echo ===== Current branch =====
echo %CURRENT_BRANCH%

call :run_command "Pulling latest changes" git pull --rebase --autostash

echo.
echo [Done] Pull completed successfully.
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
    echo ----- Git error output -----
    for %%A in ("%STDERR_FILE%") do (
        if %%~zA gtr 0 (
            type "%STDERR_FILE%"
        ) else (
            echo [No additional stderr output]
        )
    )
    echo.
    echo [Hint] Please screenshot or copy the full Git error output above and contact the programming team.
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