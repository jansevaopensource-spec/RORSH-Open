# RORSH Admin Shell (RAS) Installer for Windows
# Usage: Run as Administrator: .\install-ras.ps1

param(
    [string]$BinaryPath = $null
)

$AppName = "RORSHTerminal"
$InstallDir = "$env:ProgramFiles\RORSHTerminal"

function Write-Status {
    param([string]$Message, [string]$Type = "Info")
    $timestamp = Get-Date -Format "HH:mm:ss"
    switch ($Type) {
        "Success" { Write-Host "[$timestamp] [OK] $Message" -ForegroundColor Green }
        "Error"   { Write-Host "[$timestamp] [ERROR] $Message" -ForegroundColor Red }
        default   { Write-Host "[$timestamp] [INFO] $Message" }
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RORSH Admin Shell (RAS) Installer" -ForegroundColor Cyan
Write-Host "  Windows Edition" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Status "Please run as Administrator" "Error"
    exit 1
}

$arch = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }

if (-not $BinaryPath) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $candidates = @(
        "$scriptDir\RORSHTerminal-win-$arch.exe",
        "$scriptDir\RORSHTerminal.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            $BinaryPath = $candidate
            break
        }
    }
}

if (-not $BinaryPath -or -not (Test-Path $BinaryPath)) {
    Write-Status "Binary not found" "Error"
    exit 1
}

Write-Status "Binary: $BinaryPath"

Write-Status "Creating installation directory..."
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}
Copy-Item -Path $BinaryPath -Destination "$InstallDir\RORSHTerminal.exe" -Force
Write-Status "Installed at $InstallDir" "Success"

$currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($currentPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$currentPath;$InstallDir", "Machine")
    Write-Status "Added to system PATH" "Success"
}

Write-Host
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Installation Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host
Write-Host "Usage:"
Write-Host "  RORSHTerminal        Start RAS from any terminal"
Write-Host "  or: ras              (after restarting terminal)"
Write-Host
Write-Host "Then type 'Start-RAS' to connect to server"
Write-Host
pause
