# RAS - RORSH Admin Shell

The admin-side shell for RORSH (Remote Operations and Remote Shell Handler).

## Overview

RAS connects to the SecureCom.js central server and provides terminal access to manage connected RCS clients.

## Features

- Cross-platform (Windows and Linux)
- Secure WebSocket (WSS) communication
- AES-256-GCM encryption
- Admin authentication
- Real-time client listing
- Remote shell access to clients
- Session management

## Building

### Requirements
- .NET 8.0 SDK or later

### Build from source
```bash
cd RAS
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

## Usage

```
RAS> RAS-Start          # Connect to server and authenticate
RAS> c-list             # List all connected clients
RAS> get-connect @key   # Connect to client by rorshKey
RAS> get-disconnect     # Disconnect from current client
RAS> exit               # Exit RAS
```

After connecting to a client with `get-connect`, all typed commands are relayed to the client's terminal.

## Commands

| Command | Description |
|---------|-------------|
| RAS-Start | Connect to SecureCom.js server |
| RAS-Stop | Disconnect from server |
| c-list | Display all connected clients |
| get-connect @rorshkey | Establish session with client |
| get-disconnect | End current client session |
| exit | Quit RAS |

## License

MIT License
