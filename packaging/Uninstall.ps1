#Requires -Version 5.1
<#
  Removes the Project Pinner context-menu package and its files.
  Per-user, no admin required.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'ProjectPinner'),
    [switch]$KeepFiles
)

$ErrorActionPreference = 'Continue'

Write-Host "Removing Project Pinner context-menu package ..." -ForegroundColor Cyan

# Release the shell-ext DLL so files can be deleted.
Get-CimInstance Win32_Process -Filter "name='dllhost.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match 'A3D8F1E2' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Get-Process ProjectPinner -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Get-AppxPackage -Name 'WaldoDevelopment.ProjectPinnerShellExt*' -ErrorAction SilentlyContinue |
    Remove-AppxPackage -ErrorAction SilentlyContinue

if (-not $KeepFiles) {
    foreach ($f in @('ProjectPinner.ShellExt.dll')) {
        $p = Join-Path $InstallDir $f
        if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force -ErrorAction SilentlyContinue }
    }
    Write-Host "Removed the shell extension. The app exe and your shortcuts were left at:" -ForegroundColor Green
    Write-Host "  $InstallDir" -ForegroundColor Green
    Write-Host "Delete that folder manually to remove everything." -ForegroundColor DarkGray
}

Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }

Write-Host "Done." -ForegroundColor Green
