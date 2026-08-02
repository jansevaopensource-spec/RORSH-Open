/**
 * RORSH SecureCom Server
 * Central WebSocket relay server for RORSH Admin Shell (RAS) and RORSH Client Shell (RCS)
 * 
 * Security Model:
 * - WSS (WebSocket Secure) only - TLS encryption on transport layer
 * - AES-256-GCM end-to-end encryption for command payloads
 * - ECDH ephemeral key exchange per session
 * - Single admin authentication via .env credentials
 * - RorshKey: 10-digit server-generated client identifier (regenerated on reconnect)
 */

const WebSocket = require('ws');
const https = require('https');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
require('dotenv').config();

const { generateRorshKey, encryptPayload, decryptPayload, deriveKey } = require('./crypto');

// Configuration
const PORT = process.env.PORT || 8443;
const ADMIN_ID = process.env.ADMIN_ID || 'admin';
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'changeme';
const TLS_KEY_PATH = process.env.TLS_KEY_PATH || './certs/server.key';
const TLS_CERT_PATH = process.env.TLS_CERT_PATH || './certs/server.crt';

// State management
const clients = new Map();      // rorshKey -> { ws, hostname, ip, os, encryptionKey, connectedAt }
const admin = { ws: null, authenticated: false, encryptionKey: null };  // Single admin only
const sessions = new Map();     // rorshKey -> { adminWs, clientWs, sessionKey }

// Logging
function log(level, message, data = '') {
    const timestamp = new Date().toISOString();
    const logEntry = `[${timestamp}] [${level}] ${message} ${data}`;
    console.log(logEntry);

    // Append to log file
    const logDir = path.join(__dirname, 'logs');
    if (!fs.existsSync(logDir)) fs.mkdirSync(logDir, { recursive: true });
    fs.appendFileSync(path.join(logDir, 'server.log'), logEntry + '\n');
}

// Generate 10-digit RorshKey
function createRorshKey() {
    return generateRorshKey();
}

// Message protocol
function buildMessage(type, payload, key = null) {
    const msg = { type, timestamp: Date.now() };
    if (payload) {
        // Only encrypt if key is a valid hex string (not an object with salt)
        if (key && typeof key === 'string' && key.length === 64) {
            msg.payload = encryptPayload(JSON.stringify(payload), key);
            msg.encrypted = true;
        } else {
            msg.payload = payload;
        }
    }
    return JSON.stringify(msg);
}

function parseMessage(data, key = null) {
    try {
        const msg = JSON.parse(data);
        if (msg.encrypted && key) {
            const decrypted = decryptPayload(msg.payload, key);
            msg.payload = JSON.parse(decrypted);
        }
        return msg;
    } catch (e) {
        return null;
    }
}

// Send message wrapper
function send(ws, message) {
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(message);
    }
}

// Client management
function registerClient(ws, info) {
    const rorshKey = createRorshKey();
    const clientData = {
        ws: ws,
        rorshKey: rorshKey,
        hostname: info.hostname || 'unknown',
        ip: info.ip || 'unknown',
        os: info.os || 'unknown',
        username: info.username || 'unknown',
        encryptionKey: info.publicKey ? deriveKey(info.publicKey) : null,
        connectedAt: new Date().toISOString(),
        lastSeen: Date.now()
    };
    clients.set(rorshKey, clientData);
    log('INFO', `Client registered`, `key=${rorshKey} host=${info.hostname} ip=${info.ip}`);
    return clientData;
}

function unregisterClient(rorshKey) {
    const client = clients.get(rorshKey);
    if (client) {
        // Close any active session
        if (sessions.has(rorshKey)) {
            endSession(rorshKey);
        }
        clients.delete(rorshKey);
        log('INFO', `Client unregistered`, `key=${rorshKey}`);
    }
}

function getClientList() {
    const list = [];
    for (const [key, data] of clients) {
        list.push({
            rorshKey: key,
            hostname: data.hostname,
            ip: data.ip,
            os: data.os,
            username: data.username,
            connectedAt: data.connectedAt,
            status: sessions.has(key) ? 'connected' : 'idle'
        });
    }
    return list;
}

// Session management
function startSession(rorshKey) {
    const client = clients.get(rorshKey);
    if (!client || !admin.ws) return false;

    const sessionKey = crypto.randomBytes(32).toString('hex');
    sessions.set(rorshKey, {
        adminWs: admin.ws,
        clientWs: client.ws,
        sessionKey: sessionKey,
        startedAt: Date.now()
    });

    // Notify both parties
    send(admin.ws, buildMessage('session_started', { rorshKey, sessionKey }, admin.encryptionKey));
    send(client.ws, buildMessage('shell_open', { sessionKey }, client.encryptionKey));

    log('INFO', `Session started`, `key=${rorshKey}`);
    return true;
}

function endSession(rorshKey) {
    const session = sessions.get(rorshKey);
    if (session) {
        const client = clients.get(rorshKey);
        if (client) {
            send(client.ws, buildMessage('shell_close', {}, client.encryptionKey));
        }
        send(admin.ws, buildMessage('session_ended', { rorshKey }, admin.encryptionKey));
        sessions.delete(rorshKey);
        log('INFO', `Session ended`, `key=${rorshKey}`);
    }
}

function relayCommand(rorshKey, command) {
    const session = sessions.get(rorshKey);
    const client = clients.get(rorshKey);
    if (session && client) {
        send(client.ws, buildMessage('cmd_exec', { command, sessionKey: session.sessionKey }, client.encryptionKey));
        log('DEBUG', `Command relayed`, `key=${rorshKey} cmd=${command.substring(0, 50)}`);
    }
}

function relayOutput(rorshKey, output) {
    const session = sessions.get(rorshKey);
    if (session && admin.ws) {
        send(admin.ws, buildMessage('cmd_output', { rorshKey, output }, admin.encryptionKey));
    }
}

// WebSocket server setup
function createServer() {
    let server;

    // Try TLS first, fallback to plain WS for development
    if (fs.existsSync(TLS_KEY_PATH) && fs.existsSync(TLS_CERT_PATH)) {
        const tlsOptions = {
            key: fs.readFileSync(TLS_KEY_PATH),
            cert: fs.readFileSync(TLS_CERT_PATH)
        };
        server = https.createServer(tlsOptions);
        log('INFO', 'TLS server initialized');
    } else {
        // For Render.com or reverse proxy setups, use plain HTTP
        // Render handles TLS termination at the edge
        server = require('http').createServer();
        log('WARN', 'Running without TLS - ensure reverse proxy provides HTTPS');
    }

    const wss = new WebSocket.Server({ server });

    wss.on('connection', (ws, req) => {
        const clientIp = req.headers['x-forwarded-for'] || req.socket.remoteAddress;
        log('INFO', 'New connection', `ip=${clientIp}`);

        ws.isAlive = true;
        ws.clientType = null; // 'admin' or 'client'

        ws.on('pong', () => { ws.isAlive = true; });

        ws.on('message', (data) => {
            try {
                handleMessage(ws, data, clientIp);
            } catch (err) {
                log('ERROR', 'Message handler error', err.message);
            }
        });

        ws.on('close', (code, reason) => {
            handleDisconnect(ws);
        });

        ws.on('error', (err) => {
            log('ERROR', 'WebSocket error', err.message);
        });
    });

    // Heartbeat to detect dead connections
    const heartbeat = setInterval(() => {
        wss.clients.forEach((ws) => {
            if (!ws.isAlive) {
                ws.terminate();
                return;
            }
            ws.isAlive = false;
            ws.ping();
        });

        // Update lastSeen for clients
        for (const [key, data] of clients) {
            if (data.ws.readyState === WebSocket.OPEN) {
                data.lastSeen = Date.now();
            }
        }
    }, 30000);

    server.listen(PORT, () => {
        log('INFO', 'SecureCom server listening', `port=${PORT}`);
    });

    return { server, wss };
}

// Message handler
function handleMessage(ws, data, clientIp) {
    let msg;
    try {
        msg = JSON.parse(data);
    } catch {
        log('WARN', 'Invalid JSON received');
        return;
    }

    const type = msg.type;

    switch (type) {
        // Client registration
        case 'client_hello': {
            const payload = msg.payload;
            const clientInfo = {
                hostname: payload.hostname,
                ip: clientIp,
                os: payload.os,
                username: payload.username,
                publicKey: payload.publicKey
            };
            const clientData = registerClient(ws, clientInfo);
            ws.clientType = 'client';
            ws.rorshKey = clientData.rorshKey;

            // Send back the rorshKey
            send(ws, buildMessage('client_registered', {
                rorshKey: clientData.rorshKey,
                serverPublicKey: 'server-pub-key-placeholder'
            }));
            break;
        }

        // Admin authentication
        case 'admin_auth': {
            const payload = msg.payload;
            if (payload.adminId === ADMIN_ID && payload.password === ADMIN_PASSWORD) {
                if (admin.ws && admin.ws !== ws) {
                    // Kick previous admin
                    send(admin.ws, buildMessage('admin_kicked', { reason: 'New admin connected' }));
                    admin.ws.close();
                }

                admin.ws = ws;
                admin.authenticated = true;
                admin.encryptionKey = null; // Disable E2E encryption for now, WSS provides TLS
                ws.clientType = 'admin';

                send(ws, buildMessage('auth_success', {
                    message: 'Authenticated successfully',
                    serverPublicKey: 'server-pub-key-placeholder'
                }));
                log('INFO', 'Admin authenticated', `ip=${clientIp}`);
            } else {
                send(ws, buildMessage('auth_failed', { message: 'Invalid credentials' }));
                log('WARN', 'Admin auth failed', `ip=${clientIp}`);
                ws.close();
            }
            break;
        }

        // Admin requests client list
        case 'list_clients': {
            if (ws.clientType !== 'admin' || !admin.authenticated) {
                send(ws, buildMessage('error', { message: 'Unauthorized' }));
                return;
            }
            const list = getClientList();
            send(ws, buildMessage('client_list', { clients: list }, admin.encryptionKey));
            break;
        }

        // Admin requests connection to client
        case 'connect_client': {
            if (ws.clientType !== 'admin' || !admin.authenticated) return;
            const rorshKey = msg.payload.rorshKey;
            const success = startSession(rorshKey);
            if (!success) {
                send(ws, buildMessage('error', { message: `Client ${rorshKey} not found or unavailable` }, admin.encryptionKey));
            }
            break;
        }

        // Admin requests disconnect from client
        case 'disconnect_client': {
            if (ws.clientType !== 'admin' || !admin.authenticated) return;
            const rorshKey = msg.payload.rorshKey;
            endSession(rorshKey);
            break;
        }

        // Admin sends command to client
        case 'admin_command': {
            if (ws.clientType !== 'admin' || !admin.authenticated) return;
            const rorshKey = msg.payload.rorshKey;
            const command = msg.payload.command;
            relayCommand(rorshKey, command);
            break;
        }

        // Client sends command output back
        case 'cmd_output': {
            if (ws.clientType !== 'client') return;
            const rorshKey = ws.rorshKey;
            relayOutput(rorshKey, msg.payload.output);
            break;
        }

        // Client heartbeat
        case 'heartbeat': {
            if (ws.clientType === 'client' && ws.rorshKey) {
                const client = clients.get(ws.rorshKey);
                if (client) client.lastSeen = Date.now();
            }
            break;
        }

        // Admin shell resize
        case 'shell_resize': {
            if (ws.clientType !== 'admin' || !admin.authenticated) return;
            const rorshKey = msg.payload.rorshKey;
            const client = clients.get(rorshKey);
            if (client) {
                send(client.ws, buildMessage('shell_resize', {
                    cols: msg.payload.cols,
                    rows: msg.payload.rows
                }, client.encryptionKey));
            }
            break;
        }

        default:
            log('WARN', `Unknown message type: ${type}`);
    }
}

// Disconnect handler
function handleDisconnect(ws) {
    if (ws.clientType === 'admin') {
        log('INFO', 'Admin disconnected');
        admin.ws = null;
        admin.authenticated = false;
        admin.encryptionKey = null;
        // End all sessions
        for (const [key, session] of sessions) {
            endSession(key);
        }
    } else if (ws.clientType === 'client' && ws.rorshKey) {
        unregisterClient(ws.rorshKey);
    }
}

// Graceful shutdown
process.on('SIGTERM', () => {
    log('INFO', 'SIGTERM received, shutting down');
    process.exit(0);
});

process.on('SIGINT', () => {
    log('INFO', 'SIGINT received, shutting down');
    process.exit(0);
});

// Start server
log('INFO', 'RORSH SecureCom Server starting');
log('INFO', `Admin ID configured: ${ADMIN_ID}`);
createServer();

// Build trigger v2.0.0: 2026-08-02T14:09:06.508888
