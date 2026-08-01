const WebSocket = require('ws');
const crypto = require('crypto');
const dotenv = require('dotenv');
const { v4: uuidv4 } = require('uuid');

// Load environment variables
dotenv.config();

// Configuration
const PORT = process.env.PORT || 10000;
const ADMIN_ID = process.env.ADMIN_ID;
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD;
const ENCRYPTION_KEY = process.env.ENCRYPTION_KEY;

if (!ADMIN_ID || !ADMIN_PASSWORD || !ENCRYPTION_KEY) {
    console.error('FATAL: Missing required environment variables. Check .env file.');
    console.error('Required: ADMIN_ID, ADMIN_PASSWORD, ENCRYPTION_KEY');
    process.exit(1);
}

if (ENCRYPTION_KEY.length !== 64) {
    console.error('FATAL: ENCRYPTION_KEY must be 64 hex characters (32 bytes).');
    process.exit(1);
}

// Convert hex key to Buffer
const KEY_BUFFER = Buffer.from(ENCRYPTION_KEY, 'hex');

// Encryption utilities
function encrypt(text) {
    const iv = crypto.randomBytes(16);
    const cipher = crypto.createCipheriv('aes-256-gcm', KEY_BUFFER, iv);
    let encrypted = cipher.update(text, 'utf8', 'hex');
    encrypted += cipher.final('hex');
    const authTag = cipher.getAuthTag();
    return iv.toString('hex') + ':' + authTag.toString('hex') + ':' + encrypted;
}

function decrypt(encryptedData) {
    const parts = encryptedData.split(':');
    if (parts.length !== 3) return null;
    const iv = Buffer.from(parts[0], 'hex');
    const authTag = Buffer.from(parts[1], 'hex');
    const encrypted = parts[2];
    const decipher = crypto.createDecipheriv('aes-256-gcm', KEY_BUFFER, iv);
    decipher.setAuthTag(authTag);
    let decrypted = decipher.update(encrypted, 'hex', 'utf8');
    decrypted += decipher.final('utf8');
    return decrypted;
}

// Client and Admin registries
const clients = new Map();      // rorshKey -> { ws, hostname, ip, platform, connectedAt }
const admins = new Map();       // ws -> { id, connectedAt }
const sessions = new Map();       // rorshKey -> { adminWs, clientWs }

// Generate 10-digit rorshKey
function generateRorshKey() {
    return Math.floor(1000000000 + Math.random() * 9000000000).toString();
}

// Send encrypted message
function sendEncrypted(ws, data) {
    if (ws.readyState === WebSocket.OPEN) {
        const payload = JSON.stringify(data);
        const encrypted = encrypt(payload);
        ws.send(encrypted);
    }
}

// Broadcast to all admins
function broadcastToAdmins(data) {
    const encrypted = encrypt(JSON.stringify(data));
    admins.forEach((admin, ws) => {
        if (ws.readyState === WebSocket.OPEN) {
            ws.send(encrypted);
        }
    });
}

const http = require('http');

// Health check HTTP server for Render
const healthServer = http.createServer((req, res) => {
    if (req.url === '/') {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ status: 'ok', service: 'SecureCom.js', timestamp: new Date().toISOString() }));
    } else {
        res.writeHead(404);
        res.end('Not Found');
    }
});

// Create WebSocket server
const wss = new WebSocket.Server({ port: PORT });

console.log('SecureCom.js server started on port ' + PORT);
console.log('Waiting for connections...');

wss.on('connection', (ws, req) => {
    const clientIp = req.headers['x-forwarded-for'] || req.socket.remoteAddress;
    console.log('New connection from: ' + clientIp);

    let isAuthenticated = false;
    let connectionType = null; // 'admin' or 'client'
    let currentRorshKey = null;

    ws.on('message', (message) => {
        try {
            const decrypted = decrypt(message.toString());
            if (!decrypted) {
                sendEncrypted(ws, { type: 'error', message: 'Decryption failed' });
                return;
            }

            const data = JSON.parse(decrypted);

            // Handle authentication
            if (data.type === 'auth_admin') {
                if (data.adminId === ADMIN_ID && data.password === ADMIN_PASSWORD) {
                    isAuthenticated = true;
                    connectionType = 'admin';
                    const adminId = uuidv4();
                    admins.set(ws, { id: adminId, connectedAt: new Date() });
                    console.log('Admin authenticated: ' + adminId);
                    sendEncrypted(ws, { type: 'auth_success', message: 'Admin authenticated', adminId: adminId });

                    // Send current client list
                    const clientList = [];
                    clients.forEach((client, key) => {
                        clientList.push({
                            rorshKey: key,
                            hostname: client.hostname,
                            ip: client.ip,
                            platform: client.platform,
                            connectedAt: client.connectedAt
                        });
                    });
                    sendEncrypted(ws, { type: 'client_list', clients: clientList });
                } else {
                    sendEncrypted(ws, { type: 'auth_failed', message: 'Invalid credentials' });
                    ws.close();
                }
                return;
            }

            // Handle client registration
            if (data.type === 'register_client') {
                connectionType = 'client';
                currentRorshKey = generateRorshKey();
                clients.set(currentRorshKey, {
                    ws: ws,
                    hostname: data.hostname || 'unknown',
                    ip: clientIp,
                    platform: data.platform || 'unknown',
                    connectedAt: new Date()
                });
                console.log('Client registered: ' + currentRorshKey + ' (' + data.hostname + ')');
                sendEncrypted(ws, { type: 'registered', rorshKey: currentRorshKey });

                // Notify admins
                broadcastToAdmins({
                    type: 'client_connected',
                    rorshKey: currentRorshKey,
                    hostname: data.hostname,
                    ip: clientIp,
                    platform: data.platform
                });
                return;
            }

            // Admin commands
            if (connectionType === 'admin' && isAuthenticated) {
                if (data.type === 'command' && data.command === 'c-list') {
                    const clientList = [];
                    clients.forEach((client, key) => {
                        clientList.push({
                            rorshKey: key,
                            hostname: client.hostname,
                            ip: client.ip,
                            platform: client.platform,
                            connectedAt: client.connectedAt
                        });
                    });
                    sendEncrypted(ws, { type: 'client_list', clients: clientList });
                    return;
                }

                if (data.type === 'get-connect') {
                    const targetKey = data.rorshKey;
                    const client = clients.get(targetKey);
                    if (!client) {
                        sendEncrypted(ws, { type: 'error', message: 'Client not found: ' + targetKey });
                        return;
                    }

                    // Check if already connected
                    if (sessions.has(targetKey)) {
                        sendEncrypted(ws, { type: 'error', message: 'Client already has active session' });
                        return;
                    }

                    // Establish relay session
                    sessions.set(targetKey, { adminWs: ws, clientWs: client.ws });
                    sendEncrypted(ws, { type: 'connected', rorshKey: targetKey, message: 'Connected to ' + targetKey });
                    sendEncrypted(client.ws, { type: 'session_start', message: 'Admin connected' });
                    console.log('Session established: Admin -> ' + targetKey);
                    return;
                }

                if (data.type === 'get-disconnect') {
                    const targetKey = data.rorshKey;
                    const session = sessions.get(targetKey);
                    if (session && session.adminWs === ws) {
                        const client = clients.get(targetKey);
                        if (client) {
                            sendEncrypted(client.ws, { type: 'session_end', message: 'Admin disconnected' });
                        }
                        sessions.delete(targetKey);
                        sendEncrypted(ws, { type: 'disconnected', rorshKey: targetKey });
                        console.log('Session ended: Admin -> ' + targetKey);
                    } else {
                        sendEncrypted(ws, { type: 'error', message: 'No active session for ' + targetKey });
                    }
                    return;
                }

                // Relay command to client
                if (data.type === 'relay_command') {
                    const targetKey = data.rorshKey;
                    const session = sessions.get(targetKey);
                    if (session && session.adminWs === ws) {
                        const client = clients.get(targetKey);
                        if (client && client.ws.readyState === WebSocket.OPEN) {
                            sendEncrypted(client.ws, {
                                type: 'execute',
                                command: data.command,
                                rorshKey: targetKey
                            });
                        }
                    }
                    return;
                }
            }

            // Client responses
            if (connectionType === 'client') {
                if (data.type === 'command_output') {
                    const session = sessions.get(currentRorshKey);
                    if (session && session.clientWs === ws) {
                        sendEncrypted(session.adminWs, {
                            type: 'output',
                            rorshKey: currentRorshKey,
                            output: data.output
                        });
                    }
                    return;
                }

                if (data.type === 'command_error') {
                    const session = sessions.get(currentRorshKey);
                    if (session && session.clientWs === ws) {
                        sendEncrypted(session.adminWs, {
                            type: 'error_output',
                            rorshKey: currentRorshKey,
                            error: data.error
                        });
                    }
                    return;
                }

                if (data.type === 'command_exit') {
                    const session = sessions.get(currentRorshKey);
                    if (session && session.clientWs === ws) {
                        sendEncrypted(session.adminWs, {
                            type: 'command_exit',
                            rorshKey: currentRorshKey,
                            code: data.code
                        });
                    }
                    return;
                }
            }

        } catch (err) {
            console.error('Error processing message: ' + err.message);
            sendEncrypted(ws, { type: 'error', message: 'Invalid message format' });
        }
    });

    ws.on('close', () => {
        if (connectionType === 'admin') {
            admins.delete(ws);
            // Clean up sessions for this admin
            sessions.forEach((session, key) => {
                if (session.adminWs === ws) {
                    const client = clients.get(key);
                    if (client) {
                        sendEncrypted(client.ws, { type: 'session_end', message: 'Admin disconnected' });
                    }
                    sessions.delete(key);
                }
            });
            console.log('Admin disconnected');
        } else if (connectionType === 'client' && currentRorshKey) {
            clients.delete(currentRorshKey);
            sessions.delete(currentRorshKey);
            broadcastToAdmins({
                type: 'client_disconnected',
                rorshKey: currentRorshKey
            });
            console.log('Client disconnected: ' + currentRorshKey);
        }
    });

    ws.on('error', (err) => {
        console.error('WebSocket error: ' + err.message);
    });
});

// Graceful shutdown
process.on('SIGTERM', () => {
    console.log('SIGTERM received. Closing server...');
    wss.close(() => {
        process.exit(0);
    });
});

process.on('SIGINT', () => {
    console.log('SIGINT received. Closing server...');
    wss.close(() => {
        process.exit(0);
    });
});
// Start health check server
healthServer.listen(PORT, () => {
    console.log('Health check server running on port ' + PORT);
});
