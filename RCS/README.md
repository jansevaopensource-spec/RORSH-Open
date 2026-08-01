# RCS - RORSH Client Shell

The client-side background service for RORSH (Remote Operations and Remote Shell Handler).

## Overview

RCS runs as a background service on client machines, connecting to the SecureCom.js central server. It awaits admin commands and executes them in a local shell.

## Features

- Cross-platform background service (Windows and Linux)
- Secure WebSocket (WSS) communication
- AES-256-GCM encryption
- One-time 10-digit RorshKey per connection
- Automatic reconnection
- Command execution with output relay
- No GUI or console window

## Building

### Requirements
- .NET 8.0 SDK or later

### Build from source
```bash
cd RCS
dotnet restore
dotnet build -c Release
```

### Publish single file
```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Linux
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

## Installation

Use the provided installers:
- Windows: `install-ras.ps1` or `install-rcs.ps1`
- Linux: `install-ras.sh` or `install-rcs.sh`

## Behavior

- Automatically connects to `wss://rorsh-openweb-ssh.onrender.com`
- Generates a new 10-digit RorshKey on each connection
- Receives commands from admin via SecureCom.js relay
- Executes commands in system shell (cmd.exe on Windows, bash on Linux)
- Streams output back to admin in real-time
- Automatically reconnects if connection drops

## Security

- All communications encrypted with AES-256-GCM
- No persistent credentials stored locally
- Commands execute with current user privileges
- Session isolation per admin connection

## License

MIT License
