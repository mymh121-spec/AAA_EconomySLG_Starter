@echo off
setlocal
set "UNITY_EDITOR=C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe"
for %%I in ("%~dp0.") do set "GAME_PROJECT=%%~fI"

if not exist "%UNITY_EDITOR%" (
    echo Unity 6.3 LTS was not found.
    echo Expected: %UNITY_EDITOR%
    pause
    exit /b 1
)

if not exist "%GAME_PROJECT%\ProjectSettings\ProjectVersion.txt" (
    echo This folder is not a Unity project:
    echo %GAME_PROJECT%
    pause
    exit /b 2
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%GAME_PROJECT%\PREPARE_UNITY_LAUNCH.ps1"
if errorlevel 10 (
    echo Another Unity Editor is already running.
    echo Close it, then run this shortcut again.
    pause
    exit /b 10
)
if errorlevel 1 (
    echo Unity launch preparation failed.
    pause
    exit /b 3
)

start "Economy SLG" "%UNITY_EDITOR%" -projectPath "%GAME_PROJECT%" -economyLaunchGame
endlocal
