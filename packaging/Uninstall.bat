@echo off
REM Double-click to remove the Project Pinner context-menu integration.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
echo.
pause
