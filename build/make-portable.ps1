#requires -Version 5.0
<#
.SYNOPSIS
    Produces a portable, no-install build of Multizip as a ZIP under dist\.

.DESCRIPTION
    The portable build is just the application folder plus an empty marker file
    named "multizip.portable". When that marker is present next to the exe, the app
    keeps all of its settings and logs in a local "Data" folder instead of %APPDATA%
    and never writes to the registry.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root   = Split-Path -Parent $PSScriptRoot
$appOut = Join-Path $root "src\Multizip.App\bin\$Configuration\net48"

if (-not (Test-Path $appOut)) {
    Write-Host "Build first: build\build.ps1 -Configuration $Configuration" -ForegroundColor Red
    exit 1
}

$dist    = Join-Path $root "dist"
$staging = Join-Path $dist "Multizip-Portable"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $staging | Out-Null

Write-Host "==> Copying application..." -ForegroundColor Cyan
Copy-Item (Join-Path $appOut "*") $staging -Recurse -Force

# Portable marker - this is what flips AppPaths into portable mode.
Set-Content -Path (Join-Path $staging "multizip.portable") -Value "" -NoNewline

$zip = Join-Path $dist "Multizip-Portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Write-Host "==> Zipping..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zip

Write-Host ""
Write-Host "Portable build ready:" -ForegroundColor Green
Write-Host "    $zip"
