#Requires -Version 5.1
<#
.SYNOPSIS
  Packs the sparse external-content MSIX for Project Pinner's modern context menu.

.DESCRIPTION
  External-content pattern (the only one that works on Win11 24H2): the MSIX payload is
  ONLY AppxManifest.xml + Assets. The real ProjectPinner.exe and ProjectPinner.ShellExt.dll
  live in the install folder and are referenced via uap10:AllowExternalContent +
  Add-AppxPackage -ExternalLocation at install time.

  The manifest Publisher is hardcoded to the signing cert subject, so this does NOT need a
  signed exe to run — only the Identity Version is injected here. Signing happens AFTER
  (Azure Trusted Signing in CI, or signtool locally).

.PARAMETER OutDir    Where to write ProjectPinner.msix.
.PARAMETER Version   Package version a.b.c.d (or a.b.c — ".0" is appended).
#>
param(
    [string]$OutDir  = "$PSScriptRoot\..\dist",
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$Manifest  = Resolve-Path "$PSScriptRoot\Package.appxmanifest"
$AssetsDir = Resolve-Path "$PSScriptRoot\Assets"

if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    if ($Version -match '^\d+\.\d+\.\d+$') { $Version = "$Version.0" }
    else { throw "Version must be a.b.c or a.b.c.d, got '$Version'." }
}
Write-Host "MSIX version: $Version"

# ---- Locate MakeAppx -----------------------------------------------------
$makeAppx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like '*\x64\makeappx.exe' } |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeAppx) { $makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue }
if (-not $makeAppx) { throw "makeappx.exe not found. Install the Windows SDK." }
$makeAppxPath = if ($makeAppx.FullName) { $makeAppx.FullName } else { $makeAppx.Source }
Write-Host "MakeAppx: $makeAppxPath"

# ---- Stage manifest + assets (NO exe/dll payload) ------------------------
$stage = Join-Path $env:TEMP "ProjectPinnerMsix_$([System.IO.Path]::GetRandomFileName())"
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item -LiteralPath $AssetsDir -Destination (Join-Path $stage 'Assets') -Recurse -Force

$xml = Get-Content -LiteralPath $Manifest -Raw
# Negative lookbehind so this only hits the Identity <Version="...">, NOT the
# "Version=" tail inside TargetDeviceFamily MinVersion="10.0.22000.0".
$xml = $xml -replace '(?<![A-Za-z])Version="\d+\.\d+\.\d+\.\d+"', "Version=`"$Version`""
if ($xml -notmatch 'AllowExternalContent>true<') {
    throw "Manifest missing <uap10:AllowExternalContent>true</...>; external-content install will fail."
}
if ($xml -match 'PUBLISHER_PLACEHOLDER') {
    throw "Manifest still has PUBLISHER_PLACEHOLDER; set the real Publisher (must match the signing cert)."
}
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $xml, $utf8NoBom)

# ---- Pack (/nv = no payload validation; binaries are external) -----------
$outMsix = Join-Path (Resolve-Path $OutDir) "ProjectPinner.msix"
if (Test-Path $outMsix) { Remove-Item $outMsix -Force }

Write-Host "Running MakeAppx (external-content, /nv)..."
& $makeAppxPath pack /nv /d $stage /p $outMsix /overwrite
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed with exit code $LASTEXITCODE" }

Remove-Item $stage -Recurse -Force
Write-Host "Created: $outMsix ($([math]::Round((Get-Item $outMsix).Length / 1KB, 1)) KB)"
