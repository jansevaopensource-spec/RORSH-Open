#!/bin/bash
# RORSH-Gate Linux Installer
# Downloads and installs rorsh-gate from GitHub Releases

set -e

VERSION="${1:-latest}"
INSTALL_DIR="$HOME/.rorsh-gate"
BIN_DIR="$INSTALL_DIR/bin"
EXE_PATH="$BIN_DIR/rorsh-gate"
REPO_OWNER="jansevaopensource-spec"
REPO_NAME="RORSH-Open"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}     RORSH-Gate Installer (Linux)       ${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

# Determine version with asset validation and fallback
if [ "$VERSION" = "latest" ]; then
    echo "Fetching latest release..."

    # Try the 'releases/latest' endpoint first (most recent by date)
    LATEST_JSON=""
    if command -v curl &> /dev/null; then
        LATEST_JSON=$(curl -s "https://api.github.com/repos/$REPO_OWNER/$REPO_NAME/releases/latest" 2>/dev/null || true)
    elif command -v wget &> /dev/null; then
        LATEST_JSON=$(wget -qO- "https://api.github.com/repos/$REPO_OWNER/$REPO_NAME/releases/latest" 2>/dev/null || true)
    fi

    if [ -n "$LATEST_JSON" ]; then
        # Check if the expected asset exists in the recent release
        if echo "$LATEST_JSON" | grep -q '"name": *"rorsh-gate"'; then
            VERSION=$(echo "$LATEST_JSON" | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/' | head -n 1)
            echo -e "${GREEN}Latest version (from recent release): $VERSION${NC}"
        else
            echo -e "${YELLOW}Recent release does not contain rorsh-gate. Falling back to 'latest' tag...${NC}"
            # Fallback: explicitly fetch the release tagged 'latest'
            if command -v curl &> /dev/null; then
                LATEST_JSON=$(curl -s "https://api.github.com/repos/$REPO_OWNER/$REPO_NAME/releases/tags/latest" 2>/dev/null || true)
            elif command -v wget &> /dev/null; then
                LATEST_JSON=$(wget -qO- "https://api.github.com/repos/$REPO_OWNER/$REPO_NAME/releases/tags/latest" 2>/dev/null || true)
            fi

            if [ -n "$LATEST_JSON" ]; then
                VERSION=$(echo "$LATEST_JSON" | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/' | head -n 1)
                echo -e "${GREEN}Using stable release: $VERSION${NC}"
            else
                echo -e "${YELLOW}Failed to fetch release info. Using default version.${NC}"
                VERSION="latest"
            fi
        fi
    else
        echo -e "${YELLOW}Failed to fetch latest release. Using default version.${NC}"
        VERSION="latest"
    fi
fi

# Create directories
echo "Creating installation directory: $INSTALL_DIR"
mkdir -p "$BIN_DIR"
mkdir -p "$INSTALL_DIR/downloads"
mkdir -p "$INSTALL_DIR/logs"

# Download URL
DOWNLOAD_URL="https://github.com/$REPO_OWNER/$REPO_NAME/releases/download/$VERSION/rorsh-gate"
SHA256_URL="https://github.com/$REPO_OWNER/$REPO_NAME/releases/download/$VERSION/rorsh-gate.sha256"

# Download
echo "Downloading rorsh-gate ($VERSION)..."
if command -v curl &> /dev/null; then
    curl -fsSL "$DOWNLOAD_URL" -o "$EXE_PATH.tmp"
elif command -v wget &> /dev/null; then
    wget -q "$DOWNLOAD_URL" -O "$EXE_PATH.tmp"
else
    echo -e "${RED}Error: curl or wget is required.${NC}"
    exit 1
fi
echo -e "${GREEN}Download complete.${NC}"

# Verify SHA-256
echo "Verifying SHA-256..."
EXPECTED_HASH=""
if command -v curl &> /dev/null; then
    EXPECTED_HASH=$(curl -fsSL "$SHA256_URL" 2>/dev/null | awk '{print $1}' || true)
elif command -v wget &> /dev/null; then
    EXPECTED_HASH=$(wget -qO- "$SHA256_URL" 2>/dev/null | awk '{print $1}' || true)
fi

if [ -n "$EXPECTED_HASH" ]; then
    ACTUAL_HASH=$(sha256sum "$EXE_PATH.tmp" | awk '{print $1}')
    if [ "$EXPECTED_HASH" != "$ACTUAL_HASH" ]; then
        echo -e "${RED}SHA-256 verification failed!${NC}"
        echo -e "${RED}Expected: $EXPECTED_HASH${NC}"
        echo -e "${RED}Actual:   $ACTUAL_HASH${NC}"
        rm -f "$EXE_PATH.tmp"
        exit 1
    fi
    echo -e "${GREEN}SHA-256 verified.${NC}"
else
    echo -e "${YELLOW}SHA-256 verification skipped (hash file not found).${NC}"
fi

# Move to final location
mv "$EXE_PATH.tmp" "$EXE_PATH"
chmod +x "$EXE_PATH"

# Add to PATH
echo "Adding to PATH..."
SHELL_RC=""
if [ -n "$ZSH_VERSION" ]; then
    SHELL_RC="$HOME/.zshrc"
elif [ -n "$BASH_VERSION" ]; then
    SHELL_RC="$HOME/.bashrc"
else
    SHELL_RC="$HOME/.profile"
fi

if [ -f "$SHELL_RC" ]; then
    if ! grep -q "$BIN_DIR" "$SHELL_RC"; then
        echo "export PATH=\"$BIN_DIR:\$PATH\"" >> "$SHELL_RC"
        echo -e "${GREEN}Added $BIN_DIR to PATH in $SHELL_RC${NC}"
    else
        echo -e "${GREEN}Already in PATH.${NC}"
    fi
fi

# Also add for current session
export PATH="$BIN_DIR:$PATH"

echo ""
echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}     Installation Complete!             ${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""
echo -e "${YELLOW}To get started, run: rorsh-gate get-serve${NC}"
echo ""
echo -e "${GREEN}Installation directory: $INSTALL_DIR${NC}"
echo -e "${GREEN}Executable: $EXE_PATH${NC}"
echo ""
echo "Note: Restart your terminal or run 'source $SHELL_RC' to use rorsh-gate."
echo ""
