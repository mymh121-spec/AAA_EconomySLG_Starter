@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0CREATE_DESKTOP_SHORTCUT.ps1"
if errorlevel 1 (
    echo Shortcut creation failed.
    pause
    exit /b 1
)
echo Shortcut creation completed.
pause
endlocal
