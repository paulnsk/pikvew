@echo off
setlocal
title Pikvew Publish
color 07

rem pushd also supports launching this script from a UNC share.
pushd "%~dp0"
if errorlevel 1 goto :location_failed

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\publish.ps1"
if errorlevel 1 goto :failed

if not exist "artifacts\release\win-x64\Pikvew.exe" goto :missing_exe

goto :success

:success
color 2F
echo.
echo Publish succeeded: %CD%\artifacts\release\win-x64\Pikvew.exe
if exist "C:\tools\" echo Copied to C:\tools\Pikvew.exe
popd
pause
exit /b 0

:missing_exe
echo ERROR: Publish did not produce Pikvew.exe.
goto :failed

:failed
color 4F
echo.
echo Publish or deployment failed. See the error above.
popd
pause
exit /b 1

:location_failed
color 4F
echo ERROR: Cannot access the repository directory.
pause
exit /b 1
