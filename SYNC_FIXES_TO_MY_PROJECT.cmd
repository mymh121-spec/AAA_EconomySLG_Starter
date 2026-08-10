@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0SYNC_FIXES_TO_MY_PROJECT.ps1"
if errorlevel 1 (
    echo Synchronization failed.
    pause
    exit /b 1
)
echo Synchronization completed. Restart Unity Hub and reopen the project.
pause
endlocal
