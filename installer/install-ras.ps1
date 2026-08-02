# RORSH Admin Shell (RAS) One-Line Installer for Windows
# Usage: powershell -Command "iex (irm https://raw.githubusercontent.com/jansevaopensource-spec/RORSH-Open/RORSH-Com/installer/install-ras.ps1)"

param(
    [string]$Version = "latest",
    [string]$InstallDir = "$env:ProgramFiles\RORSHTerminal",
    [switch]$Silent = $false
)

$ErrorActionPreference = "Stop"
$RepoOwner = "jansevaopensource-spec"
$RepoName = "RORSH-Open"
$Branch = "RORSH-Com"
$AppName = "RORSHTerminal"

function Write-Status {
    param([string]$Message, [string]$Type = "Info")
    if ($Silent) { return }
    $timestamp = Get-Date -Format "HH:mm:ss"
    switch ($Type) {
        "Success" { Write-Host "[$timestamp] [OK] $Message" -ForegroundColor Green }
        "Error"   { Write-Host "[$timestamp] [ERROR] $Message" -ForegroundColor Red }
        "Warning" { Write-Host "[$timestamp] [WARN] $Message" -ForegroundColor Yellow }
        "Step"    { Write-Host "[$timestamp] [STEP] $Message" -ForegroundColor Cyan }
        default   { Write-Host "[$timestamp] [INFO] $Message" }
    }
}

function Get-LatestReleaseUrl {
    try {
        $apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
        $release = Invoke-RestMethod -Uri $apiUrl -Headers @{ "User-Agent" = "RORSH-Installer" } -TimeoutSec 30
        $asset = $release.assets | Where-Object { $_.name -match "RORSHTerminal-win-x64\.exe" } | Select-Object -First 1
        if ($asset) { return $asset.browser_download_url }
    } catch {
        Write-Status "Failed to fetch latest release, falling back" "Warning"
    }
    return "https://github.com/$RepoOwner/$RepoName/raw/$Branch/RAS/RORSHTerminal/bin/Release/net8.0/win-x64/publish/RORSHTerminal.exe"
}

if (-not $Silent) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  RORSH Admin Shell (RAS) Installer" -ForegroundColor Cyan
    Write-Host "  Windows Edition" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host
}

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Status "Please run as Administrator" "Error"
    exit 1
}

$arch = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }
Write-Status "Architecture: $arch"

Write-Status "Step 1/3: Creating installation directory..." "Step"
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Write-Status "Step 2/3: Downloading RAS binary..." "Step"
$binaryUrl = Get-LatestReleaseUrl
$binaryPath = "$InstallDir\RORSHTerminal.exe"

try {
    Invoke-WebRequest -Uri $binaryUrl -OutFile $binaryPath -UseBasicParsing -TimeoutSec 120
    Write-Status "Downloaded to $binaryPath" "Success"
} catch {
    Write-Status "Download failed: $($_.Exception.Message)" "Error"
    exit 1
}

Write-Status "Step 3/3: Configuring environment..." "Step"

# Add to PATH
$currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($currentPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$currentPath;$InstallDir", "Machine")
    Write-Status "Added to system PATH" "Success"
}

# Create uninstaller
$uninstallScript = @'
# RORSH Admin Shell Uninstaller
$InstallDir = "$env:ProgramFiles\RORSHTerminal"
Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "RORSH Admin Shell has been removed."
'@
$uninstallScript | Out-File -FilePath "$InstallDir\uninstall.ps1" -Encoding UTF8

# Create shortcut script
$shortcutScript = @"
# RORSH Admin Shell Launcher
Write-Host "Starting RORSH Admin Shell..." -ForegroundColor Cyan
Write-Host "Type 'Start-RAS' to connect to server" -ForegroundColor Green
& "$InstallDir\RORSHTerminal.exe"
"@
$shortcutScript | Out-File -FilePath "$InstallDir\ras.ps1" -Encoding UTF8

Write-Status "Installation complete!" "Success"
if (-not $Silent) {
    Write-Host
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Installation Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host
    Write-Status "Install Directory: $InstallDir"
    Write-Status "Binary: $binaryPath"
    Write-Host
    Write-Host "Usage:"
    Write-Host "  RORSHTerminal    - Start RAS (after restarting terminal)"
    Write-Host "  powershell -File `"$InstallDir\ras.ps1`"  - Launch with helper"
    Write-Host
    Write-Host "Then type 'Start-RAS' and enter your admin credentials"
    Write-Host
}
