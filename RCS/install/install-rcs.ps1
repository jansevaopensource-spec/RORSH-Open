# RORSH Client Shell (RCS) Installer for Windows
# Installs RCS as a background service with registry autostart
# Usage: Run as Administrator: .\install-rcs.ps1

param(
    [string]$BinaryPath = $null
)

$AppName = "RORSHClient"
$ServiceName = "RORSHClientService"
$InstallDir = "$env:ProgramFiles\RORSHClient"
$RegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

function Write-Status {
    param([string]$Message, [string]$Type = "Info")
    $timestamp = Get-Date -Format "HH:mm:ss"
    switch ($Type) {
        "Success" { Write-Host "[$timestamp] [OK] $Message" -ForegroundColor Green }
        "Error"   { Write-Host "[$timestamp] [ERROR] $Message" -ForegroundColor Red }
        "Warning" { Write-Host "[$timestamp] [WARN] $Message" -ForegroundColor Yellow }
        default   { Write-Host "[$timestamp] [INFO] $Message" }
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RORSH Client Shell (RCS) Installer" -ForegroundColor Cyan
Write-Host "  Windows Edition" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Status "Please run this installer as Administrator" "Error"
    exit 1
}

$arch = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }
Write-Status "Architecture: $arch"

if (-not $BinaryPath) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $candidates = @(
        "$scriptDir\RORSHClient-win-$arch.exe",
        "$scriptDir\RORSHClient.exe",
        "$scriptDir\RORSHClient-win-x64.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            $BinaryPath = $candidate
            break
        }
    }
}

if (-not $BinaryPath -or -not (Test-Path $BinaryPath)) {
    Write-Status "Binary not found. Please provide path with -BinaryPath parameter" "Error"
    exit 1
}

Write-Status "Binary: $BinaryPath"
Write-Host

Write-Status "Step 1/5: Creating installation directory..."
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}
Copy-Item -Path $BinaryPath -Destination "$InstallDir\RORSHClient.exe" -Force
Write-Status "Application directory created at $InstallDir" "Success"

Write-Status "Step 2/5: Creating Windows service..."
$scResult = sc.exe create $ServiceName binPath= "$InstallDir\RORSHClient.exe" start= auto obj= "LocalSystem" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Status "Service created using sc.exe" "Success"
} else {
    Write-Status "Service creation failed, using registry autostart fallback" "Warning"
    $regValue = "$InstallDir\RORSHClient.exe"
    Set-ItemProperty -Path $RegistryPath -Name $AppName -Value $regValue -Force -ErrorAction SilentlyContinue
    if (-not $?) {
        New-Item -Path $RegistryPath -Force | Out-Null
        Set-ItemProperty -Path $RegistryPath -Name $AppName -Value $regValue -Force
    }
    Write-Status "Registry autostart configured for current user" "Success"
}

Write-Status "Step 3/5: Configuring service..."
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
    $svc = Get-Service -Name $ServiceName
    if ($svc.Status -eq "Running") {
        Write-Status "Service is running" "Success"
    } else {
        Write-Status "Service status: $($svc.Status)" "Warning"
    }
}

Write-Status "Step 4/5: Configuring firewall..."
$fwRuleName = "RORSH Client Outbound"
$existingRule = Get-NetFirewallRule -DisplayName $fwRuleName -ErrorAction SilentlyContinue
if (-not $existingRule) {
    New-NetFirewallRule -DisplayName $fwRuleName -Direction Outbound -Program "$InstallDir\RORSHClient.exe" -Action Allow -Profile Any | Out-Null
    Write-Status "Firewall rule created" "Success"
} else {
    Write-Status "Firewall rule already exists" "Success"
}

Write-Status "Step 5/5: Creating uninstall script..."
$uninstallScript = @'
# RORSH Client Shell Uninstaller
Write-Host "Uninstalling RORSH Client Shell..."
$svc = Get-Service -Name "RORSHClientService" -ErrorAction SilentlyContinue
if ($svc) {
    Stop-Service -Name "RORSHClientService" -Force -ErrorAction SilentlyContinue
    sc.exe delete "RORSHClientService" | Out-Null
}
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "RORSHClient" -Force -ErrorAction SilentlyContinue
Remove-NetFirewallRule -DisplayName "RORSH Client Outbound" -ErrorAction SilentlyContinue
Remove-Item -Path "$env:ProgramFiles\RORSHClient" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "RORSH Client Shell has been removed."
pause
'@

$uninstallPath = "$InstallDir\uninstall.ps1"
$uninstallScript | Out-File -FilePath $uninstallPath -Encoding UTF8
Write-Status "Uninstall script created" "Success"

$batchWrapper = "@echo off" + "`r`n" + "powershell.exe -ExecutionPolicy Bypass -File \"$env:ProgramFiles\RORSHClient\uninstall.ps1\""
$batchWrapper | Out-File -FilePath "$InstallDir\uninstall.bat" -Encoding ASCII

Write-Host
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Installation Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host
Write-Status "Installation Directory: $InstallDir"
Write-Status "Service Name: $ServiceName"
Write-Status "Registry Autostart: Configured for current user"
Write-Host
Write-Host "Management Commands:"
Write-Host "  Start:   Start-Service -Name $ServiceName"
Write-Host "  Stop:    Stop-Service -Name $ServiceName"
Write-Host "  Restart: Restart-Service -Name $ServiceName"
Write-Host "  Status:  Get-Service -Name $ServiceName"
Write-Host
Write-Host "Uninstall: Run $InstallDir\uninstall.bat as Administrator"
Write-Host
Write-Status "RCS will auto-reconnect to the SecureCom server."
Write-Status "Check server for client RorshKey."
Write-Host
pause
