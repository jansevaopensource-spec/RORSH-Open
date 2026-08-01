#!/bin/bash
# RORSH Client Shell (RCS) Linux Installer

set -e

INSTALL_DIR="${HOME}/.local/bin"
CONFIG_DIR="${HOME}/.config/rorsh/rcs"
SERVICE_DIR="${HOME}/.config/systemd/user"
BINARY_URL="https://github.com/jansevaopensource-spec/RORSH-Open/releases/latest/download/RCS"

echo "========================================"
echo "  RORSH Client Shell (RCS) Installer"
echo "========================================"
echo ""

# Create directories
mkdir -p "$INSTALL_DIR"
mkdir -p "$CONFIG_DIR"
mkdir -p "$SERVICE_DIR"

echo "Downloading RCS binary..."
if command -v curl &> /dev/null; then
    curl -L -o "$INSTALL_DIR/RCS" "$BINARY_URL"
elif command -v wget &> /dev/null; then
    wget -O "$INSTALL_DIR/RCS" "$BINARY_URL"
else
    echo "Error: curl or wget required for download."
    exit 1
fi

chmod +x "$INSTALL_DIR/RCS"
echo "Installed: $INSTALL_DIR/RCS"

# Create systemd user service (if systemd is available)
if command -v systemctl &> /dev/null && [ -d "$SERVICE_DIR" ]; then
    cat > "$SERVICE_DIR/rcs.service" << EOF
[Unit]
Description=RORSH Client Shell Background Service
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=$INSTALL_DIR/RCS
Restart=always
RestartSec=10
WorkingDirectory=$CONFIG_DIR

[Install]
WantedBy=default.target
EOF

    systemctl --user daemon-reload
    systemctl --user enable rcs.service
    systemctl --user start rcs.service
    echo "Created and started systemd user service."
else
    # Fallback: create a simple background starter script
    cat > "$CONFIG_DIR/start-rcs.sh" << 'EOF'
#!/bin/bash
nohup "$HOME/.local/bin/RCS" > "$HOME/.config/rorsh/rcs/rcs.log" 2>&1 &
echo $! > "$HOME/.config/rorsh/rcs/rcs.pid"
EOF
    chmod +x "$CONFIG_DIR/start-rcs.sh"

    # Add to crontab for auto-start
    if command -v crontab &> /dev/null; then
        (crontab -l 2>/dev/null; echo "@reboot $CONFIG_DIR/start-rcs.sh") | crontab -
        echo "Added to crontab for auto-start."
    fi

    # Start now
    "$CONFIG_DIR/start-rcs.sh"
    echo "RCS started in background."
fi

# Create uninstall script
cat > "$CONFIG_DIR/uninstall.sh" << 'EOF'
#!/bin/bash
if command -v systemctl &> /dev/null; then
    systemctl --user stop rcs.service 2>/dev/null || true
    systemctl --user disable rcs.service 2>/dev/null || true
    rm -f "$HOME/.config/systemd/user/rcs.service"
    systemctl --user daemon-reload 2>/dev/null || true
fi

# Kill any running RCS process
if [ -f "$HOME/.config/rorsh/rcs/rcs.pid" ]; then
    kill $(cat "$HOME/.config/rorsh/rcs/rcs.pid") 2>/dev/null || true
fi

# Remove crontab entry
if command -v crontab &> /dev/null; then
    crontab -l 2>/dev/null | grep -v "start-rcs.sh" | crontab - 2>/dev/null || true
fi

rm -f "$HOME/.local/bin/RCS"
rm -rf "$HOME/.config/rorsh/rcs"
echo "RCS uninstalled successfully."
EOF
chmod +x "$CONFIG_DIR/uninstall.sh"
echo "Created uninstall script: $CONFIG_DIR/uninstall.sh"

echo ""
echo "========================================"
echo "  RCS Installation Complete!"
echo "========================================"
echo "Binary: $INSTALL_DIR/RCS"
echo ""
echo "RCS is running in the background."
echo "It will auto-start on next login."
echo ""
echo "To uninstall: $CONFIG_DIR/uninstall.sh"
