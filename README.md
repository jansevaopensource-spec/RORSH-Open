# RORSH - Remote Operations & Remote Shell

[![Build RAS](https://github.com/jansevaopensource-spec/RORSH-Open/actions/workflows/build-ras.yml/badge.svg?branch=RORSH-Com)](https://github.com/jansevaopensource-spec/RORSH-Open/actions/workflows/build-ras.yml)
[![Build RCS](https://github.com/jansevaopensource-spec/RORSH-Open/actions/workflows/build-rcs.yml/badge.svg?branch=RORSH-Com)](https://github.com/jansevaopensource-spec/RORSH-Open/actions/workflows/build-rcs.yml)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

RORSH is a secure, open-source remote administration framework consisting of three components: an Admin Shell (RAS), a Client Shell (RCS), and a central SecureCom server. It provides encrypted, real-time terminal access to managed clients without requiring authentication databases or complex infrastructure.

## Table of Contents

- [Architecture](#architecture)
- [Security Features](#security-features)
- [Quick Start](#quick-start)
- [Installation](#installation)
  - [One-Line Installers](#one-line-installers)
  - [Manual Installation](#manual-installation)
- [Admin Commands](#admin-commands)
- [Building from Source](#building-from-source)
- [Project Structure](#project-structure)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

## Architecture

```
+-----------------+     WSS/TLS      +------------------+     WSS/TLS      +-----------------+
|   RAS (Admin)   | <--------------> |  SecureCom.js    | <--------------> |  RCS (Client)   |
|   C# CLI        |   AES-256-GCM    |  Node.js Server  |   AES-256-GCM    |   C# Service    |
|   Win/Linux     |                  |  Render.com      |                  |   Win/Linux     |
+-----------------+                  +------------------+                  +-----------------+
```

### Components

1. **SecureCom.js** - Central WebSocket relay server (Node.js)
2. **RAS** - RORSH Admin Shell - Terminal interface for administrators (C#)
3. **RCS** - RORSH Client Shell - Background service on managed machines (C#)

## Security Features

| Feature | Implementation |
|---------|---------------|
| Transport Security | WSS (WebSocket Secure) with TLS 1.3 |
| Payload Encryption | AES-256-GCM with per-session keys |
| Key Exchange | ECDH (NIST P-256) ephemeral keys |
| Authentication | Single admin via environment variables |
| Client Identity | 10-digit RorshKey (regenerated per connection) |
| No Database | Zero external dependencies for auth |

## Quick Start

### Server (Render.com)

1. Fork this repository
2. Create a new **Web Service** on [Render.com](https://render.com)
3. Connect your GitHub repository
4. Set environment variables:
   ```
   ADMIN_ID=your_admin_id
   ADMIN_PASSWORD=your_secure_password
   ```
5. Deploy - your server will be at `https://your-app.onrender.com`

### Client (One-Line Install)

**Windows (as Administrator):**
```powershell
powershell -Command "iex (irm https://raw.githubusercontent.com/jansevaopensource-spec/RORSH-Open/RORSH-Com/installer/install-rcs.ps1)"
```

**Linux:**
```bash
curl -sSL https://raw.githubusercontent.com/jansevaopensource-spec/RORSH-Open/RORSH-Com/installer/install-rcs.sh | sudo bash
```

### Admin (One-Line Install)

**Windows (as Administrator):**
```powershell
powershell -Command "iex (irm https://raw.githubusercontent.com/jansevaopensource-spec/RORSH-Open/RORSH-Com/installer/install-ras.ps1)"
```

**Linux:**
```bash
curl -sSL https://raw.githubusercontent.com/jansevaopensource-spec/RORSH-Open/RORSH-Com/installer/install-ras.sh | sudo bash
```

Then run `ras` (Linux) or `RORSHTerminal` (Windows) and type `Start-RAS`.

## Installation

### One-Line Installers

The `installer/` directory contains standalone installers that download the latest binaries from GitHub Releases and configure everything automatically.

| Platform | Component | Command |
|----------|-----------|---------|
| Windows | RCS (Client) | `powershell -Command "iex (irm URL/install-rcs.ps1)"` |
| Windows | RAS (Admin) | `powershell -Command "iex (irm URL/install-ras.ps1)"` |
| Linux | RCS (Client) | `curl -sSL URL/install-rcs.sh \| sudo bash` |
| Linux | RAS (Admin) | `curl -sSL URL/install-ras.sh \| sudo bash` |

> Replace `URL` with the raw GitHub URL to the `installer/` directory.

### What the Installers Do

**RCS Installer:**
- Downloads the latest binary from GitHub Releases
- Creates installation directory (`C:\Program Files\RORSHClient` or `/opt/rorsh-client`)
- Creates Windows Service (Windows) or systemd service (Linux)
- Configures registry autostart for current user (Windows HKCU fallback)
- Creates Windows Firewall outbound rule
- Creates uninstall and status scripts

**RAS Installer:**
- Downloads the latest binary from GitHub Releases
- Creates installation directory
- Adds to system PATH
- Creates launcher and uninstall scripts

### Manual Installation

Download binaries from [GitHub Releases](https://github.com/jansevaopensource-spec/RORSH-Open/releases) and run directly.

## Admin Commands

Once connected with `Start-RAS`:

| Command | Description |
|---------|-------------|
| `Start-RAS` | Connect and authenticate to server |
| `c-list` | List all connected clients with RorshKey, hostname, IP |
| `get-connect @<key>` | Connect to client by RorshKey |
| `get-disconnect` | Disconnect from current client |
| `<any command>` | Execute command on connected client's shell |
| `help` | Show command reference |
| `status` | Show connection status |
| `clear` | Clear terminal screen |
| `exit` / `quit` | Disconnect and exit RAS |

### Example Session

```
RAS> Start-RAS
Admin ID: admin
Password: ********
[RAS] Connected and authenticated successfully.

RAS> c-list
[Clients] Connected clients:
RorshKey     Hostname             IP Address       OS         Status
--------------------------------------------------------------------------------
1234567890   DESKTOP-ABC123       192.168.1.100    windows    idle

RAS> get-connect @1234567890
[WSS] Session started with client: 1234567890

whoami
desktop-abc123\john

hostname
DESKTOP-ABC123

get-disconnect
[RAS] Disconnecting from client...

RAS> exit
[RAS] Goodbye.
```

## Building from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- Git

### Build Admin Shell (RAS)
```bash
cd RAS/RORSHTerminal
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r linux-arm64 --self-contained
```

### Build Client Shell (RCS)
```bash
cd RCS/RORSHClient
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r linux-arm64 --self-contained
```

### Run Server Locally
```bash
cp .env.example .env
# Edit .env with your credentials
npm install
npm start
```

## Project Structure

```
.
├── .github/workflows/         # CI/CD pipelines
│   ├── build-ras.yml          # Build admin shell
│   ├── build-rcs.yml          # Build client shell
│   ├── release.yml            # GitHub releases
│   └── deploy-server.yml      # Render deployment
├── installer/                 # One-line installers
│   ├── install-rcs.ps1        # Windows client installer
│   ├── install-rcs.sh         # Linux client installer
│   ├── install-ras.ps1        # Windows admin installer
│   └── install-ras.sh         # Linux admin installer
├── RAS/                       # Admin shell source
│   ├── RORSHTerminal/
│   │   ├── Program.cs
│   │   ├── WssClient.cs
│   │   ├── CommandHandler.cs
│   │   ├── Crypto.cs
│   │   └── RORSHTerminal.csproj
│   └── install/               # Legacy installers
├── RCS/                       # Client shell source
│   ├── RORSHClient/
│   │   ├── Program.cs
│   │   ├── WssClient.cs
│   │   ├── ShellRelay.cs
│   │   ├── Crypto.cs
│   │   └── RORSHClient.csproj
│   └── install/               # Legacy installers
├── server.js                  # SecureCom server
├── crypto.js                  # Server crypto utilities
├── package.json               # Node dependencies
├── .env.example               # Server config template
├── README.md                  # This file
└── LICENSE                    # Apache 2.0
```

## Configuration

### Server Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `PORT` | No | 8443 | Server port |
| `ADMIN_ID` | Yes | - | Admin username |
| `ADMIN_PASSWORD` | Yes | - | Admin password |
| `TLS_KEY_PATH` | No | - | TLS private key (self-hosted) |
| `TLS_CERT_PATH` | No | - | TLS certificate (self-hosted) |

### Client Configuration

The client binary has the server URL hardcoded. To change it, rebuild from source:
```csharp
private const string ServerUrl = "wss://your-server.onrender.com";
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "Connection refused" | Server asleep? (Render free tier sleeps after 15 min). First connection wakes it up in ~30s |
| "Authentication failed" | Check ADMIN_ID and ADMIN_PASSWORD env vars on server |
| "Client not found" | Is RCS running? Check `journalctl -u rorsh-client -f` (Linux) or Services panel (Windows) |
| No command output | Client shell may need time to initialize. Wait 2-3 seconds after connect |
| Binary won't run | Install .NET 8 runtime, or use self-contained builds from releases |
| Service won't start | Check logs. Windows: Event Viewer. Linux: `journalctl -u rorsh-client` |

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'feat: Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Please read our [Contributing Guide](CONTRIBUTING.md) for details.

## Security

For security issues, please use [GitHub Security Advisories](https://github.com/jansevaopensource-spec/RORSH-Open/security/advisories) instead of public issues.

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [.NET 8](https://dotnet.microsoft.com/)
- WebSocket server powered by [ws](https://github.com/websockets/ws)
- Deployed on [Render](https://render.com/)
