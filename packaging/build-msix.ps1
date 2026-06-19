#Requires -Version 5.1
<#
.SYNOPSIS
  Packs the sparse external-content MSIX for Project Pinner's modern context menu.

.DESCRIPTION
  External-content pattern (the only one that works on Win11 24H2): the MSIX
  payload is ONLY AppxManifest.xml + Assets. The real ProjectPinner.exe and
  ProjectPinner.ShellExt.dll live in the install folder and are referenced via
  uap10:AllowExternalContent + Add-AppxPackage -ExternalLocation at install time.

  Steps:
   1. Extract Publisher (cert Subject) from the already-signed exe.
   2. Inject Publisher + Version into a staged copy of Package.appxmanifest.
   3. makeappx pack /nv (no payload validation — binaries are external).

  Signing happens AFTER this script (Azure Trusted Signing in CI, or signtool
  locally). The Publisher injected here MUST equal the MSIX signing cert subject.

.PARAMETER ExePath   Path to the signed ProjectPinner.exe (source of Publisher).
.PARAMETER OutDir    Where to write ProjectPinner.msix.
.PARAMETER Version   Package version a.b.c.d (default: derived from exe).
#>
param(
    [string]$ExePath = "$PSScriptRoot\..\dist\ProjectPinner.exe",
    [string]$OutDir  = "$PSScriptRoot\..\dist",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ExePath   = Resolve-Path $ExePath
$Manifest  = Resolve-Path "$PSScriptRoot\Package.appxmanifest"
$AssetsDir = Resolve-Path "$PSScriptRoot\Assets"

# ---- Publisher from the signed exe ---------------------------------------
Write-Host "Reading Publisher from Authenticode cert..."
$sig = Get-AuthenticodeSignature $ExePath
if ($sig.SignerCertificate) {
    $publisher = $sig.SignerCertificate.Subject
} else {
    Write-Warning "Exe is unsigned — using fallback publisher (package will not load until signed to match)."
    $publisher = "CN=Waldo Development LLC, O=Waldo Development LLC, L=Lewisville, S=Texas, C=US"
}
Write-Host "Publisher: $publisher"

# ---- Version -------------------------------------------------------------
if (-not $Version) {
    $fv = (Get-Item $ExePath).VersionInfo.FileVersion
    if ($fv -match '^\d+\.\d+\.\d+') { $Version = "$($Matches[0]).0" } else { $Version = "1.0.4.0" }
}
if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { $Version = "$Version.0" }
Write-Host "Version: $Version"

# ---- Locate MakeAppx -----------------------------------------------------
$makeAppx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like '*\x64\makeappx.exe' } |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeAppx) {
    $makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue
}
if (-not $makeAppx) { throw "makeappx.exe not found. Install the Windows SDK." }
$makeAppxPath = if ($makeAppx.FullName) { $makeAppx.FullName } else { $makeAppx.Source }
Write-Host "MakeAppx: $makeAppxPath"

# ---- Stage manifest + assets (NO exe/dll payload) ------------------------
$stage = Join-Path $env:TEMP "ProjectPinnerMsix_$([System.IO.Path]::GetRandomFileName())"
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item -LiteralPath $AssetsDir -Destination (Join-Path $stage 'Assets') -Recurse -Force

$xml = Get-Content -LiteralPath $Manifest -Raw
$xml = $xml -replace 'Publisher="PUBLISHER_PLACEHOLDER"', "Publisher=`"$publisher`""
# Negative lookbehind so this only hits the Identity <Version="...">, NOT the
# "Version=" tail inside TargetDeviceFamily MinVersion="10.0.22000.0".
$xml = $xml -replace '(?<![A-Za-z])Version="\d+\.\d+\.\d+\.\d+"', "Version=`"$Version`""
if ($xml -notmatch 'AllowExternalContent>true<') {
    throw "Manifest missing <uap10:AllowExternalContent>true</...>; external-content install will fail."
}
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $xml, $utf8NoBom)

Write-Host "`nStaged package contents:"
Get-ChildItem $stage -Recurse | ForEach-Object { Write-Host "  $($_.FullName.Substring($stage.Length))" }

# ---- Pack (/nv = no payload validation; binaries are external) -----------
$outMsix = Join-Path (Resolve-Path $OutDir) "ProjectPinner.msix"
if (Test-Path $outMsix) { Remove-Item $outMsix -Force }

Write-Host "`nRunning MakeAppx (external-content, /nv)..."
& $makeAppxPath pack /nv /d $stage /p $outMsix /overwrite
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed with exit code $LASTEXITCODE" }

Remove-Item $stage -Recurse -Force
Write-Host "`nCreated: $outMsix ($([math]::Round((Get-Item $outMsix).Length / 1KB, 1)) KB)"
