# RORSH - Remote Operations & Remote Shell

RORSH is a secure, open-source remote administration framework consisting of three components: an Admin Shell (RAS), a Client Shell (RCS), and a central SecureCom server. It provides encrypted, real-time terminal access to managed clients without requiring authentication databases or complex infrastructure.

## Architecture

```
+---------------+     WSS/TLS      +------------------+     WSS/TLS      +---------------+
|   RAS (Admin) | <--------------> |  SecureCom.js    | <--------------> |  RCS (Client) |
|   C# CLI      |   AES-256-GCM    |  Node.js Server  |   AES-256-GCM    |   C# Service  |
|   Win/Linux   |                  |  Render.com      |                  |   Win/Linux   |
+---------------+                  +------------------+                  +---------------+
```

## Security Features

| Feature | Implementation |
|---------|---------------|
| Transport Security | WSS (WebSocket Secure) with TLS 1.3 |
| Payload Encryption | AES-256-GCM with per-session keys |
| Key Exchange | ECDH (secp256k1) ephemeral keys |
| Authentication | Single admin via environment variables |
| Client Identity | 10-digit RorshKey (regenerated per connection) |

## Components

### 1. SecureCom.js (Server)
- **Language**: Node.js
- **Location**: `SecureCom.js/`
- **Deployment**: Render.com (or self-hosted)
- **Features**: Client registry, session relay, encrypted message routing

### 2. RAS - RORSH Admin Shell
- **Language**: C# (.NET 8)
- **Location**: `RAS/RORSHTerminal/`
- **Platforms**: Windows, Linux
- **Features**: Native terminal interface, client listing, SSH-like sessions

### 3. RCS - RORSH Client Shell
- **Language**: C# (.NET 8)
- **Location**: `RCS/RORSHClient/`
- **Platforms**: Windows, Linux
- **Features**: Background service, auto-reconnect, hidden shell execution

## Quick Start

### Server Setup (Render.com)

1. Fork this repository
2. Create new Web Service on [Render.com](https://render.com)
3. Set environment variables:
   ```
   ADMIN_ID=your_admin_id
   ADMIN_PASSWORD=your_secure_password
   ```
4. Deploy - server URL will be `wss://your-app.onrender.com`

### Admin Installation

**Windows:**
```powershell
# Download from GitHub Releases
.\install-ras.ps1
# Start terminal, type: ras
# Then: Start-RAS
```

**Linux:**
```bash
# Download from GitHub Releases
sudo bash install-ras.sh
# Start terminal, type: ras
# Then: Start-RAS
```

### Client Installation

**Windows (as Administrator):**
```powershell
.\install-rcs.ps1
```

**Linux:**
```bash
sudo bash install-rcs.sh
```

## Admin Commands

| Command | Description |
|---------|-------------|
| `Start-RAS` | Connect and authenticate to server |
| `c-list` | List all connected clients |
| `get-connect @<key>` | Connect to client by RorshKey |
| `get-disconnect` | Disconnect from current client |
| `<any command>` | Execute command on connected client |
| `help` | Show help |
| `exit` | Quit RAS |

## Building from Source

### Prerequisites
- .NET 8.0 SDK
- Node.js 18+
- Git

### Build Admin Shell
```bash
cd RAS/RORSHTerminal
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
```

### Build Client Shell
```bash
cd RCS/RORSHClient
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
```

### Run Server Locally
```bash
cd SecureCom.js
cp .env.example .env
# Edit .env with your credentials
npm install
npm start
```

## Project Structure

```
rorsh/
├── SecureCom.js/          # Node.js server
│   ├── server.js          # Main server
│   ├── crypto.js          # Encryption utilities
│   ├── package.json
│   └── .env.example
├── RAS/                   # Admin shell
│   ├── RORSHTerminal/     # C# source
│   │   ├── Program.cs
│   │   ├── WssClient.cs
│   │   ├── CommandHandler.cs
│   │   └── Crypto.cs
│   └── install/           # Installers
│       ├── install-ras.sh
│       └── install-ras.ps1
├── RCS/                   # Client shell
│   ├── RORSHClient/       # C# source
│   │   ├── Program.cs
│   │   ├── WssClient.cs
│   │   ├── ShellRelay.cs
│   │   └── Crypto.cs
│   └── install/           # Installers
│       ├── install-rcs.sh
│       └── install-rcs.ps1
└── .github/workflows/     # CI/CD
    ├── build-ras.yml
    ├── build-rcs.yml
    ├── release.yml
    └── deploy-server.yml
```

## License

MIT License - See LICENSE file

## Contributing

1. Fork the repository
2. Create feature branch
3. Submit pull request

## Security Disclosure

Please report security issues via GitHub Security Advisories.
