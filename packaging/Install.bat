@echo off
REM Double-click to install Project Pinner with the Windows 11 top-level menu.
REM No admin needed. Runs the PowerShell installer with execution policy bypass.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
echo.
pause
