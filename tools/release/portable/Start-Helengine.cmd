@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Start-Helengine.ps1" %*
if errorlevel 1 pause
