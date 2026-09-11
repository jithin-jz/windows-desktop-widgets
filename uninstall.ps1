<#
    Removes the desktop widgets.

    By default your layout, notes and weather settings are kept, so a reinstall
    puts the cards back exactly where you had them. Pass -Purge to delete those
    too.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'DesktopWidgets'),
    [switch]$Purge
)

$ErrorActionPreference = 'Continue'

function Good ($m) { Write-Host "  OK    $m" -ForegroundColor Green }
function Warn ($m) { Write-Host "  WARN  $m" -ForegroundColor Yellow }

Write-Host ''
Write-Host 'Desktop Widgets - uninstaller'
Write-Host '============================'

# Stop the process first; its exe cannot be deleted while it is loaded.
$running = Get-Process DesktopWidgets -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 700
    Good 'stopped the running instance'
}
else {
    Warn 'nothing was running'
}

$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'DesktopWidgets.lnk'
if (Test-Path $lnk) {
    Remove-Item $lnk -Force
    Good 'removed the startup entry'
}
else {
    Warn 'no startup entry found'
}

if (Test-Path $InstallDir) {
    Remove-Item $InstallDir -Recurse -Force
    Good "deleted $InstallDir"
}
else {
    Warn "no install found at $InstallDir"
}

$stateDir = Join-Path $env:LOCALAPPDATA 'KiroDesktopWidgets'
if ($Purge) {
    if (Test-Path $stateDir) {
        Remove-Item $stateDir -Recurse -Force
        Good "deleted settings and notes ($stateDir)"
    }
}
elseif (Test-Path $stateDir) {
    Write-Host ''
    Write-Host "  Your layout, notes and weather settings were kept in:"
    Write-Host "      $stateDir"
    Write-Host '  Re-run with -Purge to delete them as well.'
}

Write-Host ''
Write-Host 'Done.'
Write-Host ''
