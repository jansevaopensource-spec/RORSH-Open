// Health check endpoint for Render
const http = require('http');

const server = http.createServer((req, res) => {
    if (req.url === '/') {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ status: 'ok', service: 'SecureCom.js', timestamp: new Date().toISOString() }));
    } else {
        res.writeHead(404);
        res.end('Not Found');
    }
});

const PORT = process.env.PORT || 10000;
server.listen(PORT, () => {
    console.log('Health check server running on port ' + PORT);
});

module.exports = server;
