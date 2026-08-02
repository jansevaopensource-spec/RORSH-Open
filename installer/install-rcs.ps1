# RORSH Client Shell (RCS) One-Line Installer for Windows
# Usage: Run in PowerShell as Administrator:
#   powershell -Command "iex (irm https://raw.githubusercontent.com/jansevaopensource-spec/RORSH-Open/RORSH-Com/installer/install-rcs.ps1)"
# Or download and run: .\install-rcs.ps1

param(
    [string]$Version = "latest",
    [string]$InstallDir = "$env:ProgramFiles\RORSHClient",
    [switch]$Silent = $false
)

$ErrorActionPreference = "Stop"
$RepoOwner = "jansevaopensource-spec"
$RepoName = "RORSH-Open"
$Branch = "RORSH-Com"
$AppName = "RORSHClient"
$ServiceName = "RORSHClientService"
$RegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

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

function Test-Admin {
    return ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
}

function Get-LatestReleaseUrl {
    try {
        $apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
        $release = Invoke-RestMethod -Uri $apiUrl -Headers @{ "User-Agent" = "RORSH-Installer" } -TimeoutSec 30
        $asset = $release.assets | Where-Object { $_.name -match "RORSHClient-win-x64\.exe" } | Select-Object -First 1
        if ($asset) {
            return $asset.browser_download_url
        }
    } catch {
        Write-Status "Failed to fetch latest release, falling back to GitHub raw" "Warning"
    }
    # Fallback to raw GitHub URL
    return "https://github.com/$RepoOwner/$RepoName/raw/$Branch/RCS/RORSHClient/bin/Release/net8.0/win-x64/publish/RORSHClient.exe"
}

# ========== MAIN ==========

if (-not $Silent) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  RORSH Client Shell (RCS) Installer" -ForegroundColor Cyan
    Write-Host "  One-Line Windows Installer" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host
}

# Check admin
if (-not (Test-Admin)) {
    Write-Status "Please run as Administrator" "Error"
    exit 1
}

$arch = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }
Write-Status "Architecture: $arch"

# Create install directory
Write-Status "Step 1/6: Creating installation directory..." "Step"
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Download binary
Write-Status "Step 2/6: Downloading RCS binary..." "Step"
$binaryUrl = Get-LatestReleaseUrl
$binaryPath = "$InstallDir\RORSHClient.exe"

try {
    Invoke-WebRequest -Uri $binaryUrl -OutFile $binaryPath -UseBasicParsing -TimeoutSec 120
    Write-Status "Downloaded to $binaryPath" "Success"
} catch {
    Write-Status "Download failed: $($_.Exception.Message)" "Error"
    exit 1
}

# Verify download
if (-not (Test-Path $binaryPath)) {
    Write-Status "Binary not found after download" "Error"
    exit 1
}

# Create Windows Service
Write-Status "Step 3/6: Creating Windows service..." "Step"
$scResult = sc.exe create $ServiceName binPath= "$binaryPath" start= auto obj= "LocalSystem" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Status "Service created: $ServiceName" "Success"

    # Configure recovery
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

    # Start service
    Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -eq "Running") {
        Write-Status "Service is running" "Success"
    }
} else {
    Write-Status "Service creation failed, using registry autostart fallback" "Warning"

    # Registry autostart for current user (HKCU)
    $regValue = "`"$binaryPath`""
    Set-ItemProperty -Path $RegistryPath -Name $AppName -Value $regValue -Force -ErrorAction SilentlyContinue
    if (-not $?) {
        New-Item -Path $RegistryPath -Force | Out-Null
        Set-ItemProperty -Path $RegistryPath -Name $AppName -Value $regValue -Force
    }
    Write-Status "Registry autostart configured (HKCU)" "Success"

    # Start process immediately
    Start-Process -FilePath $binaryPath -WindowStyle Hidden
    Write-Status "RCS process started" "Success"
}

# Create firewall rule
Write-Status "Step 4/6: Configuring Windows Firewall..." "Step"
$fwRuleName = "RORSH Client Outbound"
try {
    $existingRule = Get-NetFirewallRule -DisplayName $fwRuleName -ErrorAction SilentlyContinue
    if (-not $existingRule) {
        New-NetFirewallRule -DisplayName $fwRuleName -Direction Outbound -Program "$binaryPath" -Action Allow -Profile Any | Out-Null
        Write-Status "Firewall rule created" "Success"
    } else {
        Write-Status "Firewall rule already exists" "Success"
    }
} catch {
    Write-Status "Firewall rule creation skipped: $($_.Exception.Message)" "Warning"
}

# Create uninstaller
Write-Status "Step 5/6: Creating uninstaller..." "Step"
$uninstallPs1 = @'
# RORSH Client Shell Uninstaller
param([switch]$Silent = $false)
$ServiceName = "RORSHClientService"
$AppName = "RORSHClient"
$InstallDir = "$env:ProgramFiles\RORSHClient"
$RegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

if (-not $Silent) {
    Write-Host "Uninstalling RORSH Client Shell..."
}

# Stop and remove service
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
}

# Remove registry autostart
Remove-ItemProperty -Path $RegistryPath -Name $AppName -Force -ErrorAction SilentlyContinue

# Remove firewall rule
Remove-NetFirewallRule -DisplayName "RORSH Client Outbound" -ErrorAction SilentlyContinue

# Kill any running processes
Get-Process -Name "RORSHClient" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# Remove installation directory
Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue

if (-not $Silent) {
    Write-Host "RORSH Client Shell has been removed."
}
'@

$uninstallPath = "$InstallDir\uninstall.ps1"
$uninstallPs1 | Out-File -FilePath $uninstallPath -Encoding UTF8

$batchWrapper = "@echo off" + "`r`n" + "powershell.exe -ExecutionPolicy Bypass -File `"$InstallDir\uninstall.ps1`""
$batchWrapper | Out-File -FilePath "$InstallDir\uninstall.bat" -Encoding ASCII

Write-Status "Uninstaller created" "Success"

# Create status checker
Write-Status "Step 6/6: Creating status utility..." "Step"
$statusScript = @'
# RORSH Client Status
$ServiceName = "RORSHClientService"
$InstallDir = "$env:ProgramFiles\RORSHClient"

Write-Host "RORSH Client Shell Status" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

# Check service
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host "Service Status: $($svc.Status)" -ForegroundColor $(if ($svc.Status -eq 'Running') { 'Green' } else { 'Yellow' })
} else {
    Write-Host "Service Status: Not installed" -ForegroundColor Red
}

# Check process
$proc = Get-Process -Name "RORSHClient" -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "Process Status: Running (PID: $($proc.Id))" -ForegroundColor Green
} else {
    Write-Host "Process Status: Not running" -ForegroundColor Yellow
}

# Check registry
$reg = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "RORSHClient" -ErrorAction SilentlyContinue
if ($reg) {
    Write-Host "Autostart: Configured (HKCU)" -ForegroundColor Green
} else {
    Write-Host "Autostart: Not configured" -ForegroundColor Yellow
}

# Check install dir
if (Test-Path $InstallDir) {
    Write-Host "Install Dir: $InstallDir" -ForegroundColor Green
} else {
    Write-Host "Install Dir: Not found" -ForegroundColor Red
}
'@

$statusScript | Out-File -FilePath "$InstallDir\status.ps1" -Encoding UTF8

# Summary
if (-not $Silent) {
    Write-Host
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Installation Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host
    Write-Status "Install Directory: $InstallDir"
    Write-Status "Binary: $binaryPath"
    Write-Status "Service: $ServiceName"
    Write-Status "Registry Autostart: HKCU\Run\$AppName"
    Write-Host
    Write-Host "Management Commands:"
    Write-Host "  Start:   Start-Service -Name $ServiceName"
    Write-Host "  Stop:    Stop-Service -Name $ServiceName"
    Write-Host "  Restart: Restart-Service -Name $ServiceName"
    Write-Host "  Status:  powershell -File `"$InstallDir\status.ps1`""
    Write-Host "  Logs:    Get-EventLog -LogName Application -Source $ServiceName"
    Write-Host
    Write-Host "Uninstall: Run `"$InstallDir\uninstall.bat`" as Administrator"
    Write-Host
    Write-Status "RCS will auto-reconnect to wss://rorsh-openweb-ssh.onrender.com"
    Write-Host
}
