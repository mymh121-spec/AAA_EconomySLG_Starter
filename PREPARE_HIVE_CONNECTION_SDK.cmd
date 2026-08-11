@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0PREPARE_HIVE_CONNECTION_SDK.ps1"
if errorlevel 1 (
  echo.
  echo HIVE SDK preparation failed. See the message above.
)
pause
endlocal

