# RORSH Client Shell (RCS) Windows Installer
# Run as Administrator for service installation

param(
    [string]$InstallDir = "$env:LOCALAPPDATA\RORSH\RCS",
    [string]$BinaryUrl = "https://github.com/jansevaopensource-spec/RORSH-Open/releases/latest/download/RCS.exe"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RORSH Client Shell (RCS) Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create installation directory
if (!(Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Write-Host "Created directory: $InstallDir" -ForegroundColor Green
}

# Download binary
$BinaryPath = Join-Path $InstallDir "RCS.exe"
Write-Host "Downloading RCS binary..." -ForegroundColor Yellow

try {
    Invoke-WebRequest -Uri $BinaryUrl -OutFile $BinaryPath -UseBasicParsing
    Write-Host "Downloaded: $BinaryPath" -ForegroundColor Green
} catch {
    Write-Host "Download failed. Please download RCS.exe manually and place it in: $InstallDir" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

# Create startup registry entry for current user (background service)
$RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$RegName = "RORSH-RCS"

if (Test-Path $RegPath) {
    Set-ItemProperty -Path $RegPath -Name $RegName -Value "`"$BinaryPath`"" -ErrorAction SilentlyContinue
    if ($?) {
        Write-Host "Added to startup registry (Current User)." -ForegroundColor Green
    } else {
        New-ItemProperty -Path $RegPath -Name $RegName -Value "`"$BinaryPath`"" -PropertyType String -Force | Out-Null
        Write-Host "Created startup registry entry (Current User)." -ForegroundColor Green
    }
} else {
    Write-Host "Warning: Could not access registry. Manual startup configuration required." -ForegroundColor Yellow
}

# Create scheduled task for background running (alternative method)
try {
    $TaskName = "RORSH-RCS-Background"
    $Action = New-ScheduledTaskAction -Execute $BinaryPath -WorkingDirectory $InstallDir
    $Trigger = New-ScheduledTaskTrigger -AtLogOn
    $Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -RunOnlyIfNetworkAvailable
    $Principal = New-ScheduledTaskPrincipal -UserId "$env:USERNAME" -LogonType Interactive

    Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Settings $Settings -Principal $Principal -Force | Out-Null
    Write-Host "Created scheduled task for background execution." -ForegroundColor Green
} catch {
    Write-Host "Warning: Could not create scheduled task. Using registry startup only." -ForegroundColor Yellow
}

# Create uninstall script
$UninstallScript = @"
# RORSH Client Shell Uninstaller
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'RORSH-RCS' -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName 'RORSH-RCS-Background' -Confirm:`$false -ErrorAction SilentlyContinue
Remove-Item -Path '$InstallDir' -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'RCS uninstalled successfully.'
"@

$UninstallPath = Join-Path $InstallDir "uninstall.ps1"
$UninstallScript | Out-File -FilePath $UninstallPath -Encoding UTF8
Write-Host "Created uninstall script: $UninstallPath" -ForegroundColor Green

# Start RCS immediately (hidden window)
Write-Host "Starting RCS in background..." -ForegroundColor Yellow
Start-Process -FilePath $BinaryPath -WorkingDirectory $InstallDir -WindowStyle Hidden
Write-Host "RCS started." -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RCS Installation Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Install Directory: $InstallDir" -ForegroundColor White
Write-Host "Binary: $BinaryPath" -ForegroundColor White
Write-Host ""
Write-Host "RCS is running in the background." -ForegroundColor Yellow
Write-Host "It will auto-start on next login." -ForegroundColor Yellow
Write-Host ""
Write-Host "To uninstall, run: $UninstallPath" -ForegroundColor Yellow
