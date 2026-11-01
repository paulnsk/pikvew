@echo off
setlocal
title Pikvew Release
pushd "%~dp0"
if errorlevel 1 goto :failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\release.ps1"
if errorlevel 1 goto :failed
color 2F
popd
pause
exit /b 0
:failed
color 4F
echo Release failed. See the error above.
pause
exit /b 1
