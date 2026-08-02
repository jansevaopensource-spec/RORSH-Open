# SecureCom.js - RORSH Communication Server

Central relay server for RORSH framework. Handles encrypted message routing between admin and client shells.

## Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `PORT` | No | 8443 | Server port |
| `ADMIN_ID` | Yes | - | Admin username |
| `ADMIN_PASSWORD` | Yes | - | Admin password |
| `TLS_KEY_PATH` | No | - | TLS private key path |
| `TLS_CERT_PATH` | No | - | TLS certificate path |

## API Protocol

### Client Messages

| Type | Direction | Description |
|------|-----------|-------------|
| `client_hello` | C -> S | Register new client |
| `heartbeat` | C -> S | Keepalive ping |
| `cmd_output` | C -> S | Command execution output |

### Admin Messages

| Type | Direction | Description |
|------|-----------|-------------|
| `admin_auth` | A -> S | Authenticate admin |
| `list_clients` | A -> S | Request client list |
| `connect_client` | A -> S | Start session with client |
| `disconnect_client` | A -> S | End session |
| `admin_command` | A -> S | Send command to client |
| `shell_resize` | A -> S | Terminal resize event |

### Server Messages

| Type | Direction | Description |
|------|-----------|-------------|
| `client_registered` | S -> C | Registration confirmation with RorshKey |
| `auth_success` | S -> A | Authentication confirmed |
| `auth_failed` | S -> A | Authentication rejected |
| `client_list` | S -> A | List of connected clients |
| `session_started` | S -> A/B | Session established |
| `session_ended` | S -> A/B | Session closed |
| `shell_open` | S -> C | Open shell for commands |
| `shell_close` | S -> C | Close shell |
| `cmd_exec` | S -> C | Execute command |
| `cmd_output` | S -> A | Relay command output |

## Deployment

### Render.com (Recommended)

1. Create Web Service
2. Connect GitHub repository
3. Set environment variables
4. Deploy automatically on push

### Self-Hosted with Docker

```dockerfile
FROM node:20-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production
COPY . .
EXPOSE 8443
CMD ["node", "server.js"]
```

### Self-Hosted with TLS

```bash
# Generate certificates
openssl req -x509 -newkey rsa:4096 -keyout certs/server.key -out certs/server.crt -days 365 -nodes

# Set paths in .env
TLS_KEY_PATH=./certs/server.key
TLS_CERT_PATH=./certs/server.crt
```

## Logging

Logs are written to `logs/server.log` with rotation support.
