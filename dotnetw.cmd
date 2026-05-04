@echo off
setlocal

set "RepoRoot=%~dp0"
if "%RepoRoot:~-1%"=="\" set "RepoRoot=%RepoRoot:~0,-1%"

set "DOTNET_CLI_HOME=%RepoRoot%\.dotnet-home"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_NOLOGO=1"
set "NUGET_PACKAGES=%RepoRoot%\.nuget\packages"

if not exist "%DOTNET_CLI_HOME%" mkdir "%DOTNET_CLI_HOME%"
if not exist "%NUGET_PACKAGES%" mkdir "%NUGET_PACKAGES%"

dotnet %*
exit /b %ERRORLEVEL%
