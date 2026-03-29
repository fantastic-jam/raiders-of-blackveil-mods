@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%.github\scripts\package-mod.ps1"

if not exist "%PS_SCRIPT%" (
  echo ERROR: Script not found: %PS_SCRIPT%
  exit /b 1
)

if "%~1"=="" goto :usage
if /I "%~1"=="-h" goto :usage
if /I "%~1"=="--help" goto :usage

where pwsh.exe >nul 2>nul
if %ERRORLEVEL%==0 (
  pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" %*
  exit /b %ERRORLEVEL%
)

where powershell.exe >nul 2>nul
if %ERRORLEVEL%==0 (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" %*
  exit /b %ERRORLEVEL%
)

echo ERROR: Neither pwsh.exe nor powershell.exe was found in PATH.
exit /b 1

:usage
echo Usage:
echo   package-mod.bat -ModName ^<DisableSkillsBar^|HandyPurse^>
echo.
echo Example:
echo   package-mod.bat -ModName HandyPurse
exit /b 1
