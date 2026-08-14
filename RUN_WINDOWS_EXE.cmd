@echo off
setlocal

set "GAME_EXE=D:\AAA_EconomySLG\Builds\Windows\AAA_EconomySLG.exe"
if not exist "%GAME_EXE%" set "GAME_EXE=%~dp0Builds\Windows\AAA_EconomySLG.exe"
if not exist "%GAME_EXE%" (
    echo 실행 파일이 아직 없습니다.
    echo D:\AAA_EconomySLG 또는 프로젝트 폴더에서 Windows EXE를 찾지 못했습니다.
    echo Unity에서 Windows EXE 빌드를 먼저 실행하거나 Codex에 빌드를 요청하세요.
    pause
    exit /b 1
)

start "AAA Economy SLG" "%GAME_EXE%"
endlocal
