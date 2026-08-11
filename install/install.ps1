# RORSH-Gate Windows Installer
# Downloads and installs rorsh-gate.exe from GitHub Releases

param(
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"
$InstallDir = "$env:LOCALAPPDATA\RORSH-Gate"
$BinDir = "$InstallDir\bin"
$ExePath = "$BinDir\rorsh-gate.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "     RORSH-Gate Installer (Windows)     " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Determine version
if ($Version -eq "latest") {
    Write-Host "Fetching latest release..."
    try {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/jansevaopensource-spec/RORSH-Open/releases/latest" -Method Get
        $Version = $release.tag_name
        Write-Host "Latest version: $Version" -ForegroundColor Green
    }
    catch {
        Write-Host "Failed to fetch latest release. Using default version." -ForegroundColor Yellow
        $Version = "latest"
    }
}

# Create directories
Write-Host "Creating installation directory: $InstallDir"
New-Item -ItemType Directory -Force -Path $BinDir | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallDir\downloads" | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallDir\logs" | Out-Null

# Download URL
$DownloadUrl = "https://github.com/jansevaopensource-spec/RORSH-Open/releases/download/$Version/rorsh-gate.exe"
$Sha256Url = "https://github.com/jansevaopensource-spec/RORSH-Open/releases/download/$Version/rorsh-gate.exe.sha256"

Write-Host "Downloading rorsh-gate.exe ($Version)..."
try {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile "$ExePath.tmp" -UseBasicParsing
    Write-Host "Download complete." -ForegroundColor Green
}
catch {
    Write-Host "Download failed: $_" -ForegroundColor Red
    exit 1
}

# Verify SHA-256
Write-Host "Verifying SHA-256..."
try {
    $expectedHash = (Invoke-WebRequest -Uri $Sha256Url -UseBasicParsing).Content.Trim()
    $actualHash = (Get-FileHash -Path "$ExePath.tmp" -Algorithm SHA256).Hash

    if ($expectedHash -ne $actualHash) {
        Write-Host "SHA-256 verification failed!" -ForegroundColor Red
        Write-Host "Expected: $expectedHash" -ForegroundColor Red
        Write-Host "Actual:   $actualHash" -ForegroundColor Red
        Remove-Item "$ExePath.tmp" -Force
        exit 1
    }
    Write-Host "SHA-256 verified." -ForegroundColor Green
}
catch {
    Write-Host "SHA-256 verification skipped (hash file not found)." -ForegroundColor Yellow
}

# Move to final location
Move-Item -Path "$ExePath.tmp" -Destination $ExePath -Force

# Add to PATH
Write-Host "Adding to PATH..."
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($currentPath -notlike "*$BinDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$currentPath;$BinDir", "User")
    Write-Host "Added $BinDir to user PATH." -ForegroundColor Green
}
else {
    Write-Host "Already in PATH." -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "     Installation Complete!             " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To get started, run: rorsh-gate get-serve" -ForegroundColor Yellow
Write-Host ""
Write-Host "Installation directory: $InstallDir" -ForegroundColor Gray
Write-Host "Executable: $ExePath" -ForegroundColor Gray
Write-Host ""
