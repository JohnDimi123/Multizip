#requires -Version 5.0
<#
.SYNOPSIS
    Builds Multizip in Release and stages the plugin + native engine next to the exe.

.DESCRIPTION
    Run from a Developer PowerShell (so 'dotnet' is on PATH). This restores NuGet
    packages, builds the solution, copies the reference plugin into the app's
    Plugins folder and copies 7z.dll / 7z.sfx from build\redist if you placed them
    there. After this you can run the app from the output folder or compile the
    installer (build\Multizip.iss) with Inno Setup.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root    = Split-Path -Parent $PSScriptRoot
$appOut  = Join-Path $root "src\Multizip.App\bin\$Configuration\net48"
$plugOut = Join-Path $root "src\Multizip.Plugins.Sample\bin\$Configuration\net48"
$redist  = Join-Path $PSScriptRoot "redist"

Write-Host "==> Restoring and building ($Configuration)..." -ForegroundColor Cyan
dotnet build (Join-Path $root "Multizip.sln") -c $Configuration

Write-Host "==> Staging plugin..." -ForegroundColor Cyan
$pluginsDir = Join-Path $appOut "Plugins"
New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null
$pluginDll = Join-Path $plugOut "Multizip.Plugins.Sample.dll"
if (Test-Path $pluginDll) {
    Copy-Item $pluginDll $pluginsDir -Force
    Write-Host "    copied $(Split-Path $pluginDll -Leaf)"
}

Write-Host "==> Staging native 7-Zip engine (optional)..." -ForegroundColor Cyan
foreach ($name in @("7z.dll", "7z.sfx")) {
    $src = Join-Path $redist $name
    if (Test-Path $src) {
        Copy-Item $src $appOut -Force
        Write-Host "    copied $name"
    } else {
        Write-Host "    $name not found in build\redist (the managed fallback will be used)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Build complete. App output:" -ForegroundColor Green
Write-Host "    $appOut"
Write-Host "Run:    $appOut\Multizip.exe"
Write-Host "Installer: compile build\Multizip.iss with Inno Setup 6."
