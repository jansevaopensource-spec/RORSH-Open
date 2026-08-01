# RORSH Architecture

## System Overview

RORSH (Remote Operations and Remote Shell Handler) is a three-tier remote administration system designed for secure, encrypted shell access across platforms.

## Components

### 1. SecureCom.js (Central Server)
- **Role**: Message relay and session coordinator
- **Protocol**: WebSocket Secure (WSS)
- **Encryption**: AES-256-GCM for payload encryption
- **Authentication**: Environment-based admin credentials
- **Key Generation**: 10-digit one-time RorshKey per client

### 2. RAS (RORSH Admin Shell)
- **Role**: Admin interface for client management
- **Platform**: Windows & Linux
- **Mode**: Interactive console application
- **Features**:
  - Server connection with authentication
  - Real-time client listing
  - Session establishment with clients
  - Command relay to client shells

### 3. RCS (RORSH Client Shell)
- **Role**: Background service executing admin commands
- **Platform**: Windows & Linux
- **Mode**: Background service (no GUI)
- **Features**:
  - Automatic server connection
  - Command execution in system shell
  - Output streaming to admin
  - Auto-reconnection on disconnect

## Data Flow

### Client Registration
```
RCS -> [register_client] -> SecureCom.js
SecureCom.js -> [registered + rorshKey] -> RCS
SecureCom.js -> [client_connected] -> All Admins
```

### Admin Authentication
```
RAS -> [auth_admin] -> SecureCom.js
SecureCom.js -> [auth_success] -> RAS
```

### Command Execution
```
RAS -> [relay_command] -> SecureCom.js
SecureCom.js -> [execute] -> RCS
RCS -> [command_output] -> SecureCom.js
SecureCom.js -> [output] -> RAS
```

## Security Model

### Encryption
- All WebSocket messages are encrypted with AES-256-GCM
- Shared key derivation: SHA-256 of password + salt
- IV is randomly generated per message
- No plaintext data transmitted

### Authentication
- Admin credentials stored in server .env
- Password verified before session establishment
- No client authentication required (RorshKey acts as session token)

### Session Management
- One admin per client session
- Sessions terminated on disconnect
- RorshKey regenerated on reconnection

## Deployment

### Server
- Deployed on Render (https://rorsh-openweb-ssh.onrender.com)
- Node.js runtime
- Environment variables for configuration

### Admin
- Standalone executable
- Runs in terminal/console
- No installation required (portable)

### Client
- Background service
- Auto-start on boot
- Silent operation
