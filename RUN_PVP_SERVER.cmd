@echo off
setlocal
cd /d "%~dp0"

set "DOTNET_EXE=D:\dotnet\dotnet.exe"
if not exist "%DOTNET_EXE%" set "DOTNET_EXE=dotnet"

set "DOTNET_CLI_HOME=D:\dotnet\cli-home"
set "NUGET_PACKAGES=D:\dotnet\packages"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "PVP_DATA_DIR=D:\AAA_EconomySLG\ServerData"
set "PVP_URLS=http://127.0.0.1:5100"
set "PVP_MAX_ROOMS=16"

if not exist "D:\AAA_EconomySLG\ServerData" mkdir "D:\AAA_EconomySLG\ServerData"

"%DOTNET_EXE%" run --project "Server\Game.Server\Game.Server.csproj" --no-launch-profile
set "EXIT_CODE=%ERRORLEVEL%"

echo.
echo PvP server exited with code %EXIT_CODE%.
pause
exit /b %EXIT_CODE%
