# RORSH Admin Shell (RAS) Windows Installer
# Run as Administrator for best results

param(
    [string]$InstallDir = "$env:LOCALAPPDATA\RORSH\RAS",
    [string]$BinaryUrl = "https://github.com/jansevaopensource-spec/RORSH-Open/releases/latest/download/RAS.exe"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RORSH Admin Shell (RAS) Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create installation directory
if (!(Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Write-Host "Created directory: $InstallDir" -ForegroundColor Green
}

# Download binary
$BinaryPath = Join-Path $InstallDir "RAS.exe"
Write-Host "Downloading RAS binary..." -ForegroundColor Yellow

try {
    Invoke-WebRequest -Uri $BinaryUrl -OutFile $BinaryPath -UseBasicParsing
    Write-Host "Downloaded: $BinaryPath" -ForegroundColor Green
} catch {
    Write-Host "Download failed. Please download RAS.exe manually and place it in: $InstallDir" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

# Create startup registry entry for current user
$RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$RegName = "RORSH-RAS"

if (Test-Path $RegPath) {
    Set-ItemProperty -Path $RegPath -Name $RegName -Value $BinaryPath -ErrorAction SilentlyContinue
    if ($?) {
        Write-Host "Added to startup registry (Current User)." -ForegroundColor Green
    } else {
        New-ItemProperty -Path $RegPath -Name $RegName -Value $BinaryPath -PropertyType String -Force | Out-Null
        Write-Host "Created startup registry entry (Current User)." -ForegroundColor Green
    }
} else {
    Write-Host "Warning: Could not access registry. Manual startup configuration required." -ForegroundColor Yellow
}

# Create shortcut on Desktop
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\RAS.lnk")
$Shortcut.TargetPath = $BinaryPath
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "RORSH Admin Shell"
$Shortcut.Save()
Write-Host "Created desktop shortcut." -ForegroundColor Green

# Create uninstall script
$UninstallScript = @"
# RORSH Admin Shell Uninstaller
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'RORSH-RAS' -ErrorAction SilentlyContinue
Remove-Item -Path '$InstallDir' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path '$env:USERPROFILE\Desktop\RAS.lnk' -Force -ErrorAction SilentlyContinue
Write-Host 'RAS uninstalled successfully.'
"@

$UninstallPath = Join-Path $InstallDir "uninstall.ps1"
$UninstallScript | Out-File -FilePath $UninstallPath -Encoding UTF8
Write-Host "Created uninstall script: $UninstallPath" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RAS Installation Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Install Directory: $InstallDir" -ForegroundColor White
Write-Host "Binary: $BinaryPath" -ForegroundColor White
Write-Host ""
Write-Host "To start RAS, run: $BinaryPath" -ForegroundColor Yellow
Write-Host "Or double-click the Desktop shortcut." -ForegroundColor Yellow
Write-Host ""
Write-Host "To uninstall, run: $UninstallPath" -ForegroundColor Yellow
