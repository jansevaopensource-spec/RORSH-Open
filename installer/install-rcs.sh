#!/bin/bash
# RORSH Client Shell (RCS) One-Line Installer for Linux
# Usage: curl -sSL https://raw.githubusercontent.com/jansevaopensource-spec/RORSH-Open/RORSH-Com/installer/install-rcs.sh | sudo bash
# Or: sudo bash install-rcs.sh

set -e

REPO_OWNER="jansevaopensource-spec"
REPO_NAME="RORSH-Open"
BRANCH="RORSH-Com"
APP_NAME="rorsh-client"
APP_DIR="/opt/rorsh-client"
BIN_NAME="RORSHClient"
SERVICE_NAME="rorsh-client"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

log() {
    local level="$1"
    local msg="$2"
    local color="$NC"
    case "$level" in
        "OK") color="$GREEN" ;;
        "ERROR") color="$RED" ;;
        "WARN") color="$YELLOW" ;;
        "STEP") color="$CYAN" ;;
    esac
    echo -e "${color}[$(date '+%H:%M:%S')] [$level] $msg${NC}"
}

echo "========================================"
echo -e "${CYAN}  RORSH Client Shell (RCS) Installer${NC}"
echo -e "${CYAN}  Linux Edition${NC}"
echo "========================================"
echo

# Check root
if [ "$EUID" -ne 0 ]; then
    log "ERROR" "Please run as root (use sudo)"
    exit 1
fi

# Detect architecture
ARCH=$(uname -m)
if [ "$ARCH" = "x86_64" ]; then
    BINARY_NAME="RORSHClient-linux-x64"
    RELEASE_ASSET="RORSHClient-linux-x64"
elif [ "$ARCH" = "aarch64" ]; then
    BINARY_NAME="RORSHClient-linux-arm64"
    RELEASE_ASSET="RORSHClient-linux-arm64"
else
    log "ERROR" "Unsupported architecture: $ARCH"
    exit 1
fi

log "INFO" "Architecture: $ARCH"

# Create temp dir
TMP_DIR=$(mktemp -d)
trap "rm -rf $TMP_DIR" EXIT

# Download binary
log "STEP" "Step 1/5: Downloading RCS binary..."

# Try GitHub releases first
RELEASE_URL="https://github.com/${REPO_OWNER}/${REPO_NAME}/releases/latest/download/${RELEASE_ASSET}"
RAW_URL="https://github.com/${REPO_OWNER}/${REPO_NAME}/raw/${BRANCH}/RCS/RORSHClient/bin/Release/net8.0/linux-x64/publish/RORSHClient"

if command -v curl &> /dev/null; then
    DOWNLOADER="curl -sSL -o"
elif command -v wget &> /dev/null; then
    DOWNLOADER="wget -q -O"
else
    log "ERROR" "Neither curl nor wget found. Please install one."
    exit 1
fi

# Try release download
if $DOWNLOADER "$TMP_DIR/$BINARY_NAME" "$RELEASE_URL" 2>/dev/null; then
    if [ -s "$TMP_DIR/$BINARY_NAME" ]; then
        log "OK" "Downloaded from GitHub releases"
    else
        # Fallback to raw
        $DOWNLOADER "$TMP_DIR/$BINARY_NAME" "$RAW_URL"
        log "OK" "Downloaded from GitHub raw"
    fi
else
    $DOWNLOADER "$TMP_DIR/$BINARY_NAME" "$RAW_URL"
    log "OK" "Downloaded from GitHub raw (fallback)"
fi

if [ ! -s "$TMP_DIR/$BINARY_NAME" ]; then
    log "ERROR" "Download failed - binary is empty"
    exit 1
fi

chmod +x "$TMP_DIR/$BINARY_NAME"

# Create application directory
log "STEP" "Step 2/5: Creating application directory..."
mkdir -p "$APP_DIR"
cp "$TMP_DIR/$BINARY_NAME" "$APP_DIR/$BIN_NAME"
chmod +x "$APP_DIR/$BIN_NAME"
log "OK" "Installed to $APP_DIR/$BIN_NAME"

# Create systemd service
log "STEP" "Step 3/5: Creating systemd service..."
cat > "$SERVICE_FILE" << EOF
[Unit]
Description=RORSH Client Shell - Background Remote Access Service
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=$APP_DIR/$BIN_NAME
Restart=always
RestartSec=10
User=root
StandardOutput=journal
StandardError=journal
SyslogIdentifier=$SERVICE_NAME

# Security hardening
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=$APP_DIR

[Install]
WantedBy=multi-user.target
EOF

log "OK" "Service file created at $SERVICE_FILE"

# Enable and start service
log "STEP" "Step 4/5: Enabling and starting service..."
systemctl daemon-reload
systemctl enable "$SERVICE_NAME"
systemctl start "$SERVICE_NAME"

sleep 2
if systemctl is-active --quiet "$SERVICE_NAME"; then
    log "OK" "Service is running"
else
    log "WARN" "Service status check failed, checking logs..."
    journalctl -u "$SERVICE_NAME" --no-pager -n 5 || true
fi

# Create uninstall script
log "STEP" "Step 5/5: Creating uninstall script..."
cat > "$APP_DIR/uninstall.sh" << 'EOF'
#!/bin/bash
# RORSH Client Shell Uninstaller

if [ "$EUID" -ne 0 ]; then
    echo "Please run as root (use sudo)"
    exit 1
fi

SERVICE_NAME="rorsh-client"
APP_DIR="/opt/rorsh-client"

echo "Stopping and removing RORSH Client Shell..."
systemctl stop "$SERVICE_NAME" 2>/dev/null || true
systemctl disable "$SERVICE_NAME" 2>/dev/null || true
rm -f "/etc/systemd/system/${SERVICE_NAME}.service"
systemctl daemon-reload
rm -rf "$APP_DIR"
echo "RORSH Client Shell has been removed."
EOF

chmod +x "$APP_DIR/uninstall.sh"
log "OK" "Uninstall script created"

# Create status script
cat > "$APP_DIR/status.sh" << 'EOF'
#!/bin/bash
SERVICE_NAME="rorsh-client"
echo "RORSH Client Shell Status"
echo "========================="
if systemctl is-active --quiet "$SERVICE_NAME"; then
    echo "Service: Running"
else
    echo "Service: Not running"
fi
if systemctl is-enabled --quiet "$SERVICE_NAME" 2>/dev/null; then
    echo "Autostart: Enabled"
else
    echo "Autostart: Disabled"
fi
echo "Install Dir: /opt/rorsh-client"
echo "Logs: journalctl -u $SERVICE_NAME -f"
EOF

chmod +x "$APP_DIR/status.sh"

# Summary
echo
echo "========================================"
echo -e "${GREEN}  Installation Complete!${NC}"
echo "========================================"
echo
log "INFO" "Installation Directory: $APP_DIR"
log "INFO" "Binary: $APP_DIR/$BIN_NAME"
log "INFO" "Service: $SERVICE_NAME"
echo
echo "Management Commands:"
echo "  Start:   sudo systemctl start $SERVICE_NAME"
echo "  Stop:    sudo systemctl stop $SERVICE_NAME"
echo "  Restart: sudo systemctl restart $SERVICE_NAME"
echo "  Status:  sudo bash $APP_DIR/status.sh"
echo "  Logs:    sudo journalctl -u $SERVICE_NAME -f"
echo
echo "Uninstall: sudo bash $APP_DIR/uninstall.sh"
echo
log "INFO" "RCS will auto-reconnect to wss://rorsh-openweb-ssh.onrender.com"
echo
