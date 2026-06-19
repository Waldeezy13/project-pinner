#Requires -Version 5.1
<#
  Installs Project Pinner with the Windows 11 top-level right-click menu.

  Per-user, NO admin required (the package is signed by a trusted cert, so
  Add-AppxPackage installs for the current user without elevation).

  What it does:
   1. Copies ProjectPinner.exe + ProjectPinner.ShellExt.dll + app.ico into
      %LOCALAPPDATA%\ProjectPinner  (external content — the DLL must live
      OUTSIDE the package, or Win11 24H2 refuses to load it).
   2. Registers the sparse MSIX pointing at that folder via -ExternalLocation.
   3. Restarts Explorer so the new menu shows immediately.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'ProjectPinner')
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Definition

$exe  = Join-Path $here 'ProjectPinner.exe'
$dll  = Join-Path $here 'ProjectPinner.ShellExt.dll'
$ico  = Join-Path $here 'app.ico'
$msix = Join-Path $here 'ProjectPinner.msix'

foreach ($f in @($exe, $dll, $msix)) {
    if (-not (Test-Path -LiteralPath $f)) { throw "Missing file next to this script: $f" }
}

Write-Host "Installing Project Pinner to $InstallDir ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

# Release any prior load of the shell-ext DLL so we can overwrite it.
Get-CimInstance Win32_Process -Filter "name='dllhost.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match 'A3D8F1E2' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Get-Process ProjectPinner -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Copy-Item -LiteralPath $exe -Destination (Join-Path $InstallDir 'ProjectPinner.exe') -Force
Copy-Item -LiteralPath $dll -Destination (Join-Path $InstallDir 'ProjectPinner.ShellExt.dll') -Force
if (Test-Path -LiteralPath $ico) {
    Copy-Item -LiteralPath $ico -Destination (Join-Path $InstallDir 'app.ico') -Force
}

# Remove any previously installed copy of the package, then register the new one.
Get-AppxPackage -Name 'WaldoDevelopment.ProjectPinnerShellExt*' -ErrorAction SilentlyContinue |
    Remove-AppxPackage -ErrorAction SilentlyContinue

Write-Host "Registering context-menu package ..." -ForegroundColor Cyan
try {
    Add-AppxPackage -Path $msix -ExternalLocation $InstallDir -ForceApplicationShutdown -ErrorAction Stop
} catch {
    Write-Host ""
    Write-Host "Add-AppxPackage failed:" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "If this mentions a certificate/trust error, the .msix is not signed by a" -ForegroundColor Yellow
    Write-Host "trusted cert on this PC. A signed release build is required." -ForegroundColor Yellow
    throw
}

$pkg = Get-AppxPackage -Name 'WaldoDevelopment.ProjectPinnerShellExt*' -ErrorAction SilentlyContinue
if (-not $pkg) { throw "Package did not register." }
Write-Host ""
Write-Host "Registered: $($pkg.PackageFullName)" -ForegroundColor Green
Write-Host "Signature : $($pkg.SignatureKind)   Status: $($pkg.Status)" -ForegroundColor Green

# Refresh Explorer so the new top-level menu item appears right away.
Write-Host ""
Write-Host "Restarting Explorer to refresh the right-click menu..." -ForegroundColor Cyan
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }

Write-Host ""
Write-Host "Done. Right-click any folder -> 'Pin with alias to Quick Access'." -ForegroundColor Green
Write-Host "If it does not show, see $InstallDir\shellext-activation.log" -ForegroundColor DarkGray
