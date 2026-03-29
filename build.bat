@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
set "SOLUTION=%SCRIPT_DIR%raiders-of-blackveil-mods.sln"
set "MSBUILD_EXE="

if defined MSBUILD_EXE_OVERRIDE (
  if exist "%MSBUILD_EXE_OVERRIDE%" set "MSBUILD_EXE=%MSBUILD_EXE_OVERRIDE%"
)

if not defined MSBUILD_EXE (
  if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
    for /f "usebackq delims=" %%I in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
      set "MSBUILD_EXE=%%I"
      goto :haveMsbuild
    )
  )
)

if not defined MSBUILD_EXE if exist "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD_EXE=C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD_EXE if exist "C:\Windows\WinSxS\amd64_msbuild_b03f5f7f11d50a3a_4.0.15920.100_none_d2f55ca1d7992ef6\MSBuild.exe" set "MSBUILD_EXE=C:\Windows\WinSxS\amd64_msbuild_b03f5f7f11d50a3a_4.0.15920.100_none_d2f55ca1d7992ef6\MSBuild.exe"
if not defined MSBUILD_EXE for /f "delims=" %%I in ('where MSBuild.exe 2^>nul') do (
  set "MSBUILD_EXE=%%I"
  goto :haveMsbuild
)

:haveMsbuild
if not defined MSBUILD_EXE (
  echo ERROR: MSBuild.exe was not found.
  echo Run setup.bat first, or set MSBUILD_EXE_OVERRIDE to a valid path.
  exit /b 1
)

if not exist "%SOLUTION%" (
  echo ERROR: Solution not found: %SOLUTION%
  exit /b 1
)

echo Using MSBuild: %MSBUILD_EXE%
"%MSBUILD_EXE%" "%SOLUTION%" /t:Build /p:Configuration=Release /p:Platform="Any CPU" /p:MSBuildWarningsAsMessages=MSB3277 %*
exit /b %errorlevel%
