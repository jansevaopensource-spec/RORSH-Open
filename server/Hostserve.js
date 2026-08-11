// RORSH-Gate Server - Hostserve.js
// Secure file distribution over WSS with TLS 1.2+

const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const http = require("http");
const https = require("https");
const WebSocket = require("ws");

// Configuration
const CONFIG = {
  HTTP_PORT: process.env.PORT || 8080,
  WSS_PORT: process.env.WSS_PORT || 8443,
  FILEBASE_DIR: path.join(__dirname, "filebase"),
  LOG_LEVEL: process.env.LOG_LEVEL || "info"
};

// Ensure filebase directory exists
if (!fs.existsSync(CONFIG.FILEBASE_DIR)) {
  fs.mkdirSync(CONFIG.FILEBASE_DIR, { recursive: true });
  console.log("[INIT] Created filebase directory");
}

// Logger
function log(level, message) {
  const timestamp = new Date().toISOString();
  const entry = `[${timestamp}] [${level.toUpperCase()}] ${message}`;
  console.log(entry);
}

// Compute SHA-256 of a file
function computeSha256(filePath) {
  return new Promise((resolve, reject) => {
    const hash = crypto.createHash("sha256");
    const stream = fs.createReadStream(filePath);
    stream.on("error", reject);
    stream.on("data", chunk => hash.update(chunk));
    stream.on("end", () => resolve(hash.digest("hex")));
  });
}

// Generate manifest of all files in filebase/
async function generateManifest() {
  const manifest = { files: [], generatedAt: new Date().toISOString() };

  try {
    const entries = fs.readdirSync(CONFIG.FILEBASE_DIR);
    for (const entry of entries) {
      const filePath = path.join(CONFIG.FILEBASE_DIR, entry);
      const stats = fs.statSync(filePath);
      if (stats.isFile()) {
        const sha256 = await computeSha256(filePath);
        manifest.files.push({
          name: entry,
          size: stats.size,
          sha256: sha256,
          modified: stats.mtime.toISOString()
        });
      }
    }
  } catch (err) {
    log("error", `Manifest generation failed: ${err.message}`);
  }

  return manifest;
}

// HTTP Server for file downloads
const httpServer = http.createServer(async (req, res) => {
  // CORS headers
  res.setHeader("Access-Control-Allow-Origin", "*");
  res.setHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type");

  if (req.method === "OPTIONS") {
    res.writeHead(200);
    res.end();
    return;
  }

  const url = new URL(req.url, `http://${req.headers.host}`);

  // Endpoint: /manifest - returns JSON manifest
  if (url.pathname === "/manifest" && req.method === "GET") {
    const manifest = await generateManifest();
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify(manifest, null, 2));
    log("info", `Manifest requested by ${req.socket.remoteAddress}`);
    return;
  }

  // Endpoint: /download/:filename - download a file
  if (url.pathname.startsWith("/download/") && req.method === "GET") {
    const filename = decodeURIComponent(url.pathname.replace("/download/", ""));
    const filePath = path.join(CONFIG.FILEBASE_DIR, filename);

    // Security: prevent directory traversal
    if (!filePath.startsWith(CONFIG.FILEBASE_DIR)) {
      res.writeHead(403, { "Content-Type": "text/plain" });
      res.end("Forbidden");
      return;
    }

    if (!fs.existsSync(filePath) || !fs.statSync(filePath).isFile()) {
      res.writeHead(404, { "Content-Type": "text/plain" });
      res.end("File not found");
      return;
    }

    const stats = fs.statSync(filePath);
    res.writeHead(200, {
      "Content-Type": "application/octet-stream",
      "Content-Disposition": `attachment; filename="${filename}"`,
      "Content-Length": stats.size,
      "X-File-SHA256": await computeSha256(filePath)
    });

    const stream = fs.createReadStream(filePath);
    stream.pipe(res);
    log("info", `Download: ${filename} by ${req.socket.remoteAddress}`);
    return;
  }

  // Endpoint: /sha256/:filename - get SHA-256 hash
  if (url.pathname.startsWith("/sha256/") && req.method === "GET") {
    const filename = decodeURIComponent(url.pathname.replace("/sha256/", ""));
    const filePath = path.join(CONFIG.FILEBASE_DIR, filename);

    if (!filePath.startsWith(CONFIG.FILEBASE_DIR)) {
      res.writeHead(403);
      res.end("Forbidden");
      return;
    }

    if (!fs.existsSync(filePath) || !fs.statSync(filePath).isFile()) {
      res.writeHead(404);
      res.end("File not found");
      return;
    }

    const sha256 = await computeSha256(filePath);
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ filename, sha256 }));
    return;
  }

  // Default: 404
  res.writeHead(404, { "Content-Type": "text/plain" });
  res.end("Not Found");
});

// WebSocket Server for live connection
const wss = new WebSocket.Server({ server: httpServer });

wss.on("connection", (ws, req) => {
  const clientIp = req.socket.remoteAddress;
  log("info", `WSS client connected: ${clientIp}`);

  ws.on("message", async (data) => {
    try {
      const message = JSON.parse(data.toString());
      log("info", `Received command: ${message.command} from ${clientIp}`);

      switch (message.command) {
        case "get-help": {
          ws.send(JSON.stringify({
            type: "response",
            command: "get-help",
            data: [
              "get-help           - List all available commands",
              "get-cloud-down <f> - Download file from cloud",
              "get-cloud-down all - Download all files from cloud",
              "get-list-cloud     - List files available on server",
              "get-list-local     - List files downloaded locally",
              "get-run <f>        - Execute a downloaded file",
              "get-end            - Disconnect and exit"
            ]
          }));
          break;
        }

        case "get-list-cloud": {
          const manifest = await generateManifest();
          ws.send(JSON.stringify({
            type: "response",
            command: "get-list-cloud",
            data: manifest.files.map(f => ({
              name: f.name,
              size: f.size,
              sha256: f.sha256
            }))
          }));
          break;
        }

        case "get-cloud-down": {
          const target = message.args || "";

          if (target === "all") {
            const manifest = await generateManifest();
            ws.send(JSON.stringify({
              type: "response",
              command: "get-cloud-down",
              subcommand: "all",
              data: manifest.files
            }));
          } else if (target) {
            const filePath = path.join(CONFIG.FILEBASE_DIR, target);
            if (!filePath.startsWith(CONFIG.FILEBASE_DIR)) {
              ws.send(JSON.stringify({
                type: "error",
                command: "get-cloud-down",
                message: "Invalid filename"
              }));
              break;
            }

            if (!fs.existsSync(filePath) || !fs.statSync(filePath).isFile()) {
              ws.send(JSON.stringify({
                type: "error",
                command: "get-cloud-down",
                message: `File not found: ${target}`
              }));
              break;
            }

            const sha256 = await computeSha256(filePath);
            const stats = fs.statSync(filePath);

            ws.send(JSON.stringify({
              type: "response",
              command: "get-cloud-down",
              subcommand: "single",
              filename: target,
              size: stats.size,
              sha256: sha256,
              downloadUrl: `/download/${encodeURIComponent(target)}`
            }));
          } else {
            ws.send(JSON.stringify({
              type: "error",
              command: "get-cloud-down",
              message: "Usage: get-cloud-down <filename> or get-cloud-down all"
            }));
          }
          break;
        }

        case "get-end": {
          ws.send(JSON.stringify({
            type: "response",
            command: "get-end",
            message: "Goodbye!"
          }));
          ws.close();
          break;
        }

        default: {
          ws.send(JSON.stringify({
            type: "error",
            command: message.command,
            message: `Unknown command: ${message.command}. Type get-help for available commands.`
          }));
        }
      }
    } catch (err) {
      log("error", `Message handling error: ${err.message}`);
      ws.send(JSON.stringify({
        type: "error",
        message: "Invalid message format"
      }));
    }
  });

  ws.on("close", () => {
    log("info", `WSS client disconnected: ${clientIp}`);
  });

  ws.on("error", (err) => {
    log("error", `WSS error from ${clientIp}: ${err.message}`);
  });

  // Send welcome message
  ws.send(JSON.stringify({
    type: "connected",
    message: "RORSH-Gate Server Ready",
    serverTime: new Date().toISOString()
  }));
});

// Start server
httpServer.listen(CONFIG.HTTP_PORT, () => {
  log("info", `RORSH-Gate Server running on port ${CONFIG.HTTP_PORT}`);
  log("info", `Filebase directory: ${CONFIG.FILEBASE_DIR}`);
  log("info", `WSS endpoint: ws://localhost:${CONFIG.HTTP_PORT}`);
  log("info", `HTTP endpoints: /manifest, /download/:file, /sha256/:file`);
});

// Graceful shutdown
process.on("SIGINT", () => {
  log("info", "Shutting down server...");
  wss.close();
  httpServer.close(() => {
    process.exit(0);
  });
});
