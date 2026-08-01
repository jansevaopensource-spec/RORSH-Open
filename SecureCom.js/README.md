# SecureCom.js

Central relay server for RORSH (Remote Operations and Remote Shell Handler).

## Overview

SecureCom.js acts as the secure intermediary between RAS (RORSH Admin Shell) and RCS (RORSH Client Shell). All communications are encrypted using AES-256-GCM.

## Features

- WebSocket Secure (WSS) communication
- AES-256-GCM encryption for all messages
- Admin authentication via environment variables
- One-time 10-digit RorshKey generation for clients
- Real-time client listing for admins
- Command relay between admin and client
- Session management

## Installation

```bash
cd SecureCom.js
cp .env.example .env
# Edit .env with your credentials
npm install
npm start
```

## Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| ADMIN_ID | Admin username | Yes |
| ADMIN_PASSWORD | Admin password | Yes |
| ENCRYPTION_KEY | 64-char hex AES key | Yes |
| PORT | Server port (default: 10000) | No |

## Generating Encryption Key

```bash
node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"
```

## API Protocol

All messages are JSON objects encrypted with AES-256-GCM.

### Message Types

#### Client -> Server
- `register_client` - Register new client
- `command_output` - Send command output to admin
- `command_error` - Send command error to admin
- `command_exit` - Notify command completion

#### Admin -> Server
- `auth_admin` - Authenticate as admin
- `command: c-list` - List connected clients
- `get-connect` - Connect to client by rorshKey
- `get-disconnect` - Disconnect from client
- `relay_command` - Send command to connected client

#### Server -> Client/Admin
- `registered` - Client registration confirmation
- `auth_success` / `auth_failed` - Auth result
- `client_list` - List of connected clients
- `client_connected` / `client_disconnected` - Client events
- `connected` / `disconnected` - Session events
- `session_start` / `session_end` - Session notifications
- `execute` - Execute command (to client)
- `output` / `error_output` / `command_exit` - Command results

## License

MIT License - See LICENSE file for details.
