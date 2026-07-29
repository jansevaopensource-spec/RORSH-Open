# RORSH-Gate

Secure file distribution CLI tool with WSS live connection.

## Architecture

```
+----------------+        WSS + TLS 1.2        +------------------+
|  C# Client     | <---------------------------> |  Node.js Server  |
|  (rorsh-gate)  |                               |  (Hostserve.js)  |
+----------------+                               +------------------+
       |                                                  |
       | HTTP Download + SHA-256 Verify                     |
       v                                                  v
  Local Files                                      filebase/ (files)
```

## Commands

| Command | Description |
|---------|-------------|
| `get-serve` | Connect to server and start session |
| `get-help` | List all commands |
| `get-cloud-down <file>` | Download file with SHA-256 verification |
| `get-cloud-down all` | Download all files |
| `get-list-cloud` | List server files |
| `get-list-local` | List downloaded files |
| `get-run <file>` | Execute downloaded file |
| `get-end` | Disconnect and exit |

## Installation

### Windows (PowerShell)
```powershell
irm https://rorsh-gate.github.io/install.ps1 | iex
```

### Linux/macOS (Bash)
```bash
curl -fsSL https://rorsh-gate.github.io/install.sh | sh
```

### Manual
1. Download from [GitHub Releases](https://github.com/YOUR_USERNAME/RORSH-Gate/releases)
2. Place `rorsh-gate.exe` (Windows) or `rorsh-gate` (Linux) in PATH
3. Run `rorsh-gate get-serve`

## Building from Source

### Prerequisites
- .NET 8.0 SDK
- Node.js 18+

### Server
```bash
cd server
npm install
npm start
```

### Client
```bash
cd client
dotnet build
dotnet run -- get-serve
```

### Publish
```bash
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
```

## Security

- TLS 1.2 enforced on all connections
- SHA-256 verification for every file download
- No authentication (open source)
- Client identified by hostname + IPv4

## Project Structure

```
RORSH-Gate/
├── .github/workflows/     # CI/CD for building releases
├── server/
│   ├── Hostserve.js      # WSS + HTTP server
│   ├── package.json
│   └── filebase/         # File storage
├── client/
│   ├── Program.cs        # Entry point
│   ├── RorshGate.csproj
│   ├── Commands/         # CLI commands
│   ├── Core/             # WSS, FileManager, Runner
│   └── Assets/           # ASCII art
└── install/
    ├── install.ps1       # Windows installer
    └── install.sh        # Linux installer
```

## License

MIT
