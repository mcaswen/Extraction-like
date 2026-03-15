@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem Allowed directories
set "ALLOWED_DIRS=Assets/Art Assets/Audio Assets/Scenes Assets/SO Assets/Docs"

set "REPO_ROOT="
set "INSIDE_WORK_TREE="

for /f "delims=" %%I in ('git rev-parse --is-inside-work-tree 2^>nul') do set "INSIDE_WORK_TREE=%%I"

if /I not "%INSIDE_WORK_TREE%"=="true" (
    echo [Error] The current directory is not inside a Git repository, or Git is not installed correctly.
    echo [Hint] Please screenshot or copy the full output above and contact the programming team.
    pause
    exit /b 1
)

for /f "delims=" %%I in ('git rev-parse --show-toplevel 2^>nul') do set "REPO_ROOT=%%I"

if not defined REPO_ROOT (
    echo [Error] Failed to locate the repository root directory.
    echo [Hint] Please screenshot or copy the full output above and contact the programming team.
    pause
    exit /b 1
)

pushd "%REPO_ROOT%" >nul

set "ALL_CHANGES_FILE=%temp%\git_all_changes_%random%_%random%.txt"
set "OUTSIDE_CHANGES_FILE=%temp%\git_outside_changes_%random%_%random%.txt"
set "STAGED_FILES_FILE=%temp%\git_staged_files_%random%_%random%.txt"

break > "%ALL_CHANGES_FILE%"
break > "%OUTSIDE_CHANGES_FILE%"
break > "%STAGED_FILES_FILE%"

call :run_command "Pulling latest changes" git pull --rebase --autostash

set "AHEAD_COUNT=0"
for /f %%I in ('git rev-list --count @{u}..HEAD 2^>nul') do set "AHEAD_COUNT=%%I"

if not "!AHEAD_COUNT!"=="0" (
    echo.
    echo ===== Unpushed local commits detected =====
    echo [Hint] This branch has !AHEAD_COUNT! local commit(s) that have not been pushed.
    call :run_command "Pushing existing local commits" git push
)

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
    echo [Hint] There are no changes to commit.
    goto :success
)

echo.
echo ===== Detected change summary =====
git status --short

echo.
echo ===== Detected changed files =====
type "%ALL_CHANGES_FILE%"

set "STAGE_ALL=0"

if "!HAS_OUTSIDE_CHANGE!"=="1" (
    echo.
    echo ===== Changes detected outside target folders =====
    type "%OUTSIDE_CHANGES_FILE%"
    echo.
    echo y = Revert these non-target-folder changes and commit only the target folders
    echo n = Keep these changes and commit them together
    set /p "USER_CHOICE=Do you want to revert changes outside the target folders? (y/n): "

    if /I "!USER_CHOICE!"=="Y" (
        echo.
        echo ===== Reverting changes outside target folders =====
        for /f "usebackq delims=" %%F in ("%OUTSIDE_CHANGES_FILE%") do (
            if not "%%F"=="" (
                git ls-files --error-unmatch -- "%%F" >nul 2>nul
                if errorlevel 1 (
                    call :run_command "Removing untracked file or folder: %%F" git clean -fd -- "%%F"
                ) else (
                    call :run_command "Reverting file: %%F" git restore --staged --worktree -- "%%F"
                )
            )
        )
    ) else (
        set "STAGE_ALL=1"
        echo.
        echo [Warning] This commit will include changes outside the target folders.
        echo [Warning] Please explain the reason in the commit message.
    )
)

echo.
echo ===== Staging changes =====
if "!STAGE_ALL!"=="1" (
    call :run_command "Staging all changes" git add -A
) else (
    set "HAS_STAGE_TARGET=0"
    for %%D in (%ALLOWED_DIRS%) do (
        if exist "%%D" (
            call :run_command "Staging folder: %%D" git add -A -- "%%D"
            set "HAS_STAGE_TARGET=1"
        )
    )

    if "!HAS_STAGE_TARGET!"=="0" (
        echo [Error] No allowed directories were found. Please check the ALLOWED_DIRS setting at the top of the script.
        echo Please screenshot or copy the full output above and contact the programming team.
        goto :fail
    )
)

git diff --cached --quiet
if not errorlevel 1 goto :has_staged_changes

echo.
echo [Hint] There is nothing staged to commit.
goto :success

:has_staged_changes
echo.
echo ===== Summary of changes to be committed =====
git status --short

echo.
echo ===== Files to be committed =====
git diff --cached --name-only > "%STAGED_FILES_FILE%"
type "%STAGED_FILES_FILE%"

echo.
if "!STAGE_ALL!"=="1" (
    echo [Hint] This commit includes changes outside the target folders. Please explain the reason in the commit message.
)

set /p "COMMIT_MESSAGE=Enter commit message: "
if "%COMMIT_MESSAGE%"=="" (
    echo [Error] Commit message cannot be empty.
    goto :fail
)

call :run_command "Creating commit" git commit -m "%COMMIT_MESSAGE%"
call :run_command "Pushing to remote" git push

echo.
echo [Done] Commit and push completed successfully.
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

call "%~1" %2 %3 %4 %5 %6 %7 %8 %9 > "%STDOUT_FILE%" 2> "%STDERR_FILE%"
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