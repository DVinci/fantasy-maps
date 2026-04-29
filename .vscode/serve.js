const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = 5500;
const ROOT = path.join(__dirname, '..');

const MIME = {
  '.html': 'text/html',
  '.js': 'application/javascript',
  '.css': 'text/css',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.json': 'application/json',
};

const server = http.createServer((req, res) => {
  let filePath = path.join(ROOT, req.url === '/' ? 'index.html' : req.url);
  const ext = path.extname(filePath);
  const contentType = MIME[ext] || 'application/octet-stream';

  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404);
      res.end('Not found');
      return;
    }
    res.writeHead(200, { 'Content-Type': contentType });
    res.end(data);
  });
});

server.listen(PORT, () => {
  process.stdout.write(`Listening on port ${PORT}\n`);
  if (process.env.OPEN_IN_VSCODE) {
    const { exec } = require('child_process');
    exec(`code --open-url "vscode://vscode.simple-browser/open?http://localhost:${PORT}"`, (err) => {
      if (err) process.stderr.write(`Could not open Simple Browser: ${err.message}\n`);
    });
  }
});
