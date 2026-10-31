@echo off
setlocal
title Pikvew Tests
pushd "%~dp0"
if errorlevel 1 exit /b 1
rem These OS integration tests need a normal Windows user token, outside the agent sandbox.
set PIKVEW_REGISTRY_TESTS=1
set PIKVEW_PIPE_TESTS=1
dotnet test Pikvew.slnx -m:1 --logger "console;verbosity=normal"
if errorlevel 1 goto :failed
color 2F
popd
pause
exit /b 0
:failed
color 4F
popd
pause
exit /b 1
