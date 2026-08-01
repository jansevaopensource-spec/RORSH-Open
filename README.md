# RORSH-Open

**RORSH** - Remote Operations and Remote Shell Handler

An open-source remote administration and shell access system built with security in mind.

## Architecture

```
+--------+     WSS (AES-256-GCM)     +---------------+     WSS (AES-256-GCM)     +--------+
|  RAS   | <-----------------------> |  SecureCom.js | <-----------------------> |  RCS   |
| (Admin)|                           |  (Server)     |                           |(Client)|
+--------+                           +---------------+                           +--------+
```

### Components

| Component | Language | Role |
|-----------|----------|------|
| **RAS** | C# | Admin shell - connects to server, manages clients |
| **RCS** | C# | Client service - runs in background, executes commands |
| **SecureCom.js** | Node.js | Central relay server - handles encryption and routing |

## Features

- Cross-platform support (Windows & Linux)
- AES-256-GCM encryption for all communications
- WebSocket Secure (WSS) protocol
- One-time 10-digit RorshKey per client connection
- Real-time client listing with hostname and IP
- Remote shell access through command relay
- Background service execution (no GUI)
- Auto-start on Windows via registry
- Auto-start on Linux via systemd or crontab

## Quick Start

### Server (SecureCom.js)

```bash
cd SecureCom.js
cp .env.example .env
# Edit .env with your credentials
npm install
npm start
```

### Admin (RAS)

```bash
# Download latest release
# Run RAS.exe (Windows) or ./RAS (Linux)
RAS> RAS-Start
# Enter Admin ID and Password
RAS> c-list
RAS> get-connect @1234567890
```

### Client (RCS)

```bash
# Run installer
# Windows PowerShell:
.\install-rcs.ps1

# Linux Bash:
chmod +x install-rcs.sh
./install-rcs.sh
```

## Commands

### RAS Commands

| Command | Description |
|---------|-------------|
| `RAS-Start` | Connect to SecureCom.js server |
| `RAS-Stop` | Disconnect from server |
| `c-list` | List all connected clients |
| `get-connect @rorshkey` | Connect to client |
| `get-disconnect` | Disconnect from client |
| `exit` | Quit RAS |

After `get-connect`, all typed commands are relayed to the client's terminal.

## Security

- All messages encrypted with AES-256-GCM
- Admin authentication required
- No persistent client credentials
- Session isolation per admin connection
- WSS (WebSocket Secure) transport

## Repository Structure

```
RORSH-Open/
├── SecureCom.js/          # Node.js relay server
│   ├── server.js
│   ├── package.json
│   ├── .env.example
│   └── .github/workflows/
├── RAS/                    # Admin shell (C#)
│   ├── Program.cs
│   ├── RAS.csproj
│   └── .github/workflows/
├── RCS/                    # Client service (C#)
│   ├── Program.cs
│   ├── RcsWorker.cs
│   ├── RCS.csproj
│   └── .github/workflows/
├── installers/             # Installation scripts
│   ├── install-ras.ps1
│   ├── install-rcs.ps1
│   ├── install-ras.sh
│   └── install-rcs.sh
└── docs/                   # Documentation
```

## Building from Source

### Prerequisites
- .NET 8.0 SDK
- Node.js 18+

### Build RAS
```bash
cd RAS
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

### Build RCS
```bash
cd RCS
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

### Build SecureCom.js
```bash
cd SecureCom.js
npm install
```

## GitHub Actions

Automated builds and releases are configured:
- **RAS**: Builds for Windows (win-x64) and Linux (linux-x64)
- **RCS**: Builds for Windows (win-x64) and Linux (linux-x64)
- **SecureCom.js**: Deployment package creation

Releases are published to GitHub Releases with compiled binaries.

## License

MIT License - See LICENSE file

## Author

[jansevaopensource-spec](https://github.com/jansevaopensource-spec)
