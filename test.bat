@echo off
setlocal
title Pikvew Tests
if /I "%~1"=="--worker" goto worker
rem /k keeps the console open even if a command unexpectedly terminates this batch.
%ComSpec% /d /k call "%~f0" --worker
exit /b
:worker
pushd "%~dp0"
if errorlevel 1 goto location_failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-tests.ps1"
set "test_result=%ERRORLEVEL%"
if "%test_result%"=="0" (color 2F) else (color 4F)
echo.
echo Logs: %CD%\artifacts\test-results
popd
exit /b %test_result%
:location_failed
color 4F
echo Cannot access the Pikvew repository directory.
exit /b 1
