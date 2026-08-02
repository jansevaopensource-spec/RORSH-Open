#!/bin/bash
# RORSH Admin Shell (RAS) One-Line Installer for Linux
# Usage: curl -sSL https://raw.githubusercontent.com/jansevaopensource-spec/RORSH-Open/RORSH-Com/installer/install-ras.sh | sudo bash

set -e

REPO_OWNER="jansevaopensource-spec"
REPO_NAME="RORSH-Open"
BRANCH="RORSH-Com"
APP_DIR="/opt/rorsh-admin"
BIN_NAME="RORSHTerminal"

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
NC='\033[0m'

log() {
    local level="$1"
    local msg="$2"
    local color="$NC"
    case "$level" in
        "OK") color="$GREEN" ;;
        "ERROR") color="$RED" ;;
        "STEP") color="$CYAN" ;;
    esac
    echo -e "${color}[$(date '+%H:%M:%S')] [$level] $msg${NC}"
}

echo "========================================"
echo -e "${CYAN}  RORSH Admin Shell (RAS) Installer${NC}"
echo -e "${CYAN}  Linux Edition${NC}"
echo "========================================"
echo

if [ "$EUID" -ne 0 ]; then
    log "ERROR" "Please run as root (use sudo)"
    exit 1
fi

ARCH=$(uname -m)
if [ "$ARCH" = "x86_64" ]; then
    BINARY_NAME="RORSHTerminal-linux-x64"
    RELEASE_ASSET="RORSHTerminal-linux-x64"
elif [ "$ARCH" = "aarch64" ]; then
    BINARY_NAME="RORSHTerminal-linux-arm64"
    RELEASE_ASSET="RORSHTerminal-linux-arm64"
else
    log "ERROR" "Unsupported architecture: $ARCH"
    exit 1
fi

log "INFO" "Architecture: $ARCH"

TMP_DIR=$(mktemp -d)
trap "rm -rf $TMP_DIR" EXIT

log "STEP" "Step 1/2: Downloading RAS binary..."

RELEASE_URL="https://github.com/${REPO_OWNER}/${REPO_NAME}/releases/latest/download/${RELEASE_ASSET}"
RAW_URL="https://github.com/${REPO_OWNER}/${REPO_NAME}/raw/${BRANCH}/RAS/RORSHTerminal/bin/Release/net8.0/linux-x64/publish/RORSHTerminal"

if command -v curl &> /dev/null; then
    DOWNLOADER="curl -sSL -o"
elif command -v wget &> /dev/null; then
    DOWNLOADER="wget -q -O"
else
    log "ERROR" "Neither curl nor wget found"
    exit 1
fi

if $DOWNLOADER "$TMP_DIR/$BINARY_NAME" "$RELEASE_URL" 2>/dev/null && [ -s "$TMP_DIR/$BINARY_NAME" ]; then
    log "OK" "Downloaded from GitHub releases"
else
    $DOWNLOADER "$TMP_DIR/$BINARY_NAME" "$RAW_URL"
    log "OK" "Downloaded from GitHub raw"
fi

chmod +x "$TMP_DIR/$BINARY_NAME"

log "STEP" "Step 2/2: Installing..."
mkdir -p "$APP_DIR"
cp "$TMP_DIR/$BINARY_NAME" "$APP_DIR/$BIN_NAME"
chmod +x "$APP_DIR/$BIN_NAME"

# Create symlink
ln -sf "$APP_DIR/$BIN_NAME" "/usr/local/bin/ras"

# Create uninstaller
cat > "$APP_DIR/uninstall.sh" << 'EOF'
#!/bin/bash
APP_DIR="/opt/rorsh-admin"
rm -f "/usr/local/bin/ras"
rm -rf "$APP_DIR"
echo "RORSH Admin Shell has been removed."
EOF
chmod +x "$APP_DIR/uninstall.sh"

echo
echo "========================================"
echo -e "${GREEN}  Installation Complete!${NC}"
echo "========================================"
echo
log "INFO" "Install Directory: $APP_DIR"
log "INFO" "Command: ras"
echo
echo "Usage:"
echo "  ras                Start RORSH Admin Shell"
echo "  ras --help         Show help"
echo
echo "Then type 'Start-RAS' to connect to server"
echo
echo "Uninstall: sudo bash $APP_DIR/uninstall.sh"
echo
