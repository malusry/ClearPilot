@echo off
setlocal
title ClearPilot
color 0B
mode con: cols=92 lines=32 >nul 2>nul
chcp 65001 >nul 2>nul

set "ROOT=%~dp0"
cd /d "%ROOT%"
set "APP=%ROOT%ClearPilot.exe"

if not exist "%APP%" (
  echo ClearPilot.exe was not found in:
  echo %ROOT%
  echo.
  pause
  exit /b 1
)

"%APP%"
echo.
pause
