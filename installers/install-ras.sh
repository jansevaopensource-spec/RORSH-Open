#!/bin/bash
# RORSH Admin Shell (RAS) Linux Installer

set -e

INSTALL_DIR="${HOME}/.local/bin"
CONFIG_DIR="${HOME}/.config/rorsh/ras"
BINARY_URL="https://github.com/jansevaopensource-spec/RORSH-Open/releases/latest/download/RAS"

echo "========================================"
echo "  RORSH Admin Shell (RAS) Installer"
echo "========================================"
echo ""

# Create directories
mkdir -p "$INSTALL_DIR"
mkdir -p "$CONFIG_DIR"

echo "Downloading RAS binary..."
if command -v curl &> /dev/null; then
    curl -L -o "$INSTALL_DIR/RAS" "$BINARY_URL"
elif command -v wget &> /dev/null; then
    wget -O "$INSTALL_DIR/RAS" "$BINARY_URL"
else
    echo "Error: curl or wget required for download."
    exit 1
fi

chmod +x "$INSTALL_DIR/RAS"
echo "Installed: $INSTALL_DIR/RAS"

# Add to PATH if not already there
if [[ ":$PATH:" != *":$INSTALL_DIR:"* ]]; then
    echo 'export PATH="$HOME/.local/bin:$PATH"' >> "$HOME/.bashrc"
    echo "Added $INSTALL_DIR to PATH in .bashrc"
fi

# Create uninstall script
cat > "$CONFIG_DIR/uninstall.sh" << 'EOF'
#!/bin/bash
rm -f "$HOME/.local/bin/RAS"
rm -rf "$HOME/.config/rorsh/ras"
echo "RAS uninstalled successfully."
EOF
chmod +x "$CONFIG_DIR/uninstall.sh"
echo "Created uninstall script: $CONFIG_DIR/uninstall.sh"

echo ""
echo "========================================"
echo "  RAS Installation Complete!"
echo "========================================"
echo "Binary: $INSTALL_DIR/RAS"
echo ""
echo "Run: RAS"
echo "Or: $INSTALL_DIR/RAS"
echo ""
echo "To uninstall: $CONFIG_DIR/uninstall.sh"
