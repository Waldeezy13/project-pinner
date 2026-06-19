#Requires -Version 5.1
<#
.SYNOPSIS
  Assembles and packages ProjectPinner.msix from the signed exe + assets.

.DESCRIPTION
  1. Extracts the Publisher (cert Subject) from the already-signed exe.
  2. Injects it into a copy of Package.appxmanifest.
  3. Assembles the staging folder: exe + ico + Assets/*.png + manifest.
  4. Calls MakeAppx.exe to produce ProjectPinner.msix.

  Signing is done AFTER this script by azure/trusted-signing-action in CI,
  or by your local signtool if running manually.

.PARAMETER ExePath
  Path to the signed ProjectPinner.exe. Default: dist\ProjectPinner.exe

.PARAMETER OutDir
  Directory to write ProjectPinner.msix into. Default: dist\

.PARAMETER Version
  Package version in a.b.c.d form. Default: reads from the exe's file version.
#>
param(
    [string]$ExePath   = "$PSScriptRoot\..\dist\ProjectPinner.exe",
    [string]$OutDir    = "$PSScriptRoot\..\dist",
    [string]$Version   = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ---- Resolve paths -------------------------------------------------------
$ExePath  = Resolve-Path $ExePath
$Manifest = Resolve-Path "$PSScriptRoot\Package.appxmanifest"
$AssetsDir = Resolve-Path "$PSScriptRoot\Assets"
$IcoSrc   = Resolve-Path "$PSScriptRoot\..\assets\app.ico"

# ---- Derive Publisher from the signed exe --------------------------------
Write-Host "Reading Publisher from Authenticode cert..."
$sig = Get-AuthenticodeSignature $ExePath
if ($sig.Status -ne "Valid" -and $sig.Status -ne "UnknownError") {
    Write-Warning "Exe signature status: $($sig.Status) — using placeholder publisher."
    $publisher = "CN=Waldo Development LLC"
} else {
    $publisher = $sig.SignerCertificate.Subject
}
Write-Host "Publisher: $publisher"

# ---- Derive Version from exe if not supplied -----------------------------
if (-not $Version) {
    $fv = (Get-Item $ExePath).VersionInfo.FileVersion
    if ($fv -match '^\d+\.\d+\.\d+') {
        $Version = "$($Matches[0]).0"
    } else {
        $Version = "1.0.4.0"
    }
}
Write-Host "Version: $Version"

# ---- Find MakeAppx.exe ---------------------------------------------------
$makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue
if (-not $makeAppx) {
    $sdkBases = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "${env:ProgramFiles}\Windows Kits\10\bin"
    )
    foreach ($base in $sdkBases) {
        if (Test-Path $base) {
            $found = Get-ChildItem $base -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
                     Sort-Object FullName -Descending | Select-Object -First 1
            if ($found) { $makeAppx = $found.FullName; break }
        }
    }
}
if (-not $makeAppx) { throw "makeappx.exe not found. Install the Windows SDK." }
Write-Host "MakeAppx: $makeAppx"

# ---- Build staging folder ------------------------------------------------
$stage = Join-Path $env:TEMP "ProjectPinnerMsix_$([System.IO.Path]::GetRandomFileName())"
New-Item -ItemType Directory -Path $stage | Out-Null
New-Item -ItemType Directory -Path "$stage\Assets" | Out-Null

Copy-Item $ExePath           "$stage\ProjectPinner.exe"
Copy-Item $IcoSrc            "$stage\app.ico"
Copy-Item "$AssetsDir\*"     "$stage\Assets\" -Recurse

# Inject Publisher and Version into the manifest
$xml = Get-Content $Manifest -Raw
$xml = $xml -replace 'Publisher="PUBLISHER_PLACEHOLDER"', "Publisher=""$publisher"""
$xml = $xml -replace 'Version="\d+\.\d+\.\d+\.\d+"',     "Version=""$Version"""
$xml | Set-Content "$stage\AppxManifest.xml" -Encoding UTF8

Write-Host "`nStaging folder contents:"
Get-ChildItem $stage -Recurse | Select-Object FullName | Format-Table -HideTableHeaders

# ---- Run MakeAppx --------------------------------------------------------
$outMsix = Join-Path (Resolve-Path $OutDir) "ProjectPinner.msix"
if (Test-Path $outMsix) { Remove-Item $outMsix -Force }

Write-Host "`nRunning MakeAppx..."
& $makeAppx pack /d $stage /p $outMsix /nv
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed with exit code $LASTEXITCODE" }

# ---- Cleanup staging -----------------------------------------------------
Remove-Item $stage -Recurse -Force

Write-Host "`nCreated: $outMsix"
Write-Host "Size:    $([math]::Round((Get-Item $outMsix).Length / 1KB, 1)) KB"
