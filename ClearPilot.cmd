@echo off
setlocal
title ClearPilot
color 0B
mode con: cols=92 lines=32 >nul 2>nul
chcp 65001 >nul 2>nul
set "ROOT=%~dp0"
cd /d "%ROOT%"
set "DOTNET_ROOT=%ROOT%.dotnet"
set "DOTNET_ROOT_X64=%ROOT%.dotnet"
set "PATH=%ROOT%.dotnet;%PATH%"

if not exist "%ROOT%.dotnet\dotnet.exe" (
  echo ClearPilot could not find the local .NET SDK at:
  echo %ROOT%.dotnet\dotnet.exe
  echo.
  echo Please ask Codex to restore the local .NET SDK for this project.
  pause
  exit /b 1
)

set "APP=%ROOT%src\ClearPilot.Cli\bin\Debug\net10.0\ClearPilot.exe"
set "APP_DLL=%ROOT%src\ClearPilot.Cli\bin\Debug\net10.0\ClearPilot.dll"
set "CORE_DLL=%ROOT%src\ClearPilot.Core\bin\Debug\net10.0\ClearPilot.Core.dll"
set "NEED_BUILD=0"

if not exist "%APP%" (
  set "NEED_BUILD=1"
)

if "%NEED_BUILD%"=="0" (
  for /f %%I in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "$outputs = @(); $outputs += Get-Item -LiteralPath '%APP%' -ErrorAction SilentlyContinue; $outputs += Get-Item -LiteralPath '%APP_DLL%' -ErrorAction SilentlyContinue; $outputs += Get-Item -LiteralPath '%CORE_DLL%' -ErrorAction SilentlyContinue; if (-not $outputs) { '1'; exit }; $appTime = ($outputs | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc; $files = @(); $files += Get-Item -LiteralPath '%ROOT%ClearPilot.sln' -ErrorAction SilentlyContinue; $files += Get-ChildItem -LiteralPath '%ROOT%src' -Recurse -File -Include *.cs,*.csproj -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }; $latest = $files | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1; if ($latest -and $latest.LastWriteTimeUtc -gt $appTime) { '1' } else { '0' }"') do set "NEED_BUILD=%%I"
)

if "%NEED_BUILD%"=="1" (
  echo Building ClearPilot development version...
  "%ROOT%.dotnet\dotnet.exe" build "%ROOT%ClearPilot.sln" --no-restore /p:UseSharedCompilation=false
  if errorlevel 1 (
    echo.
    echo Build failed. Please ask Codex to rebuild the project.
    pause
    exit /b 1
  )
)

"%APP%"
echo.
pause
