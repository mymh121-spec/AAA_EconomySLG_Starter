@echo off
setlocal
set "SOURCE_PROJECT=%~dp0"
set "TARGET_PROJECT=C:\Users\andrew\My project"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SOURCE_PROJECT%SYNC_FIXES_TO_MY_PROJECT.ps1" -TargetProject "%TARGET_PROJECT%"
if errorlevel 1 (
    echo Project synchronization failed.
    pause
    exit /b 1
)

if not exist "%TARGET_PROJECT%\RUN_SINGLE_PLAYER.cmd" (
    echo The destination launcher was not created.
    pause
    exit /b 2
)

start "Economy SLG" "%TARGET_PROJECT%\RUN_SINGLE_PLAYER.cmd"
endlocal
