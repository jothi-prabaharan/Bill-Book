const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = process.env.PORT || 5200;
const ROOT_CANDIDATES = [
  process.env.STATIC_ROOT,
  '/home/prabha/deploy/Stating/dist/frontend/web/browser',
  '/home/prabha/deploy/Stating/dist/frontend/web',
  path.join(__dirname, 'dist', 'frontend', 'web', 'browser'),
  path.join(__dirname, 'dist', 'frontend', 'web')
].filter(Boolean);

let WEB_ROOT = ROOT_CANDIDATES.find(p => fs.existsSync(path.join(p, 'index.html')));

if (!WEB_ROOT) {
  WEB_ROOT = ROOT_CANDIDATES[0];
  console.warn(`[WARN] index.html not yet found. Defaulting WEB_ROOT to: ${WEB_ROOT}`);
} else {
  console.log(`[INFO] Serving frontend from: ${WEB_ROOT}`);
}

const MIME_TYPES = {
  '.html': 'text/html; charset=UTF-8',
  '.js': 'application/javascript; charset=UTF-8',
  '.mjs': 'application/javascript; charset=UTF-8',
  '.css': 'text/css; charset=UTF-8',
  '.json': 'application/json; charset=UTF-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.gif': 'image/gif',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ttf': 'font/ttf',
  '.eot': 'application/vnd.ms-fontobject'
};

const server = http.createServer((req, res) => {
  // Strip query string
  const cleanUrl = req.url.split('?')[0];
  let filePath = path.join(WEB_ROOT, cleanUrl);

  // Security check: ensure path is within WEB_ROOT
  if (!filePath.startsWith(WEB_ROOT)) {
    res.writeHead(403);
    res.end('Forbidden');
    return;
  }

  fs.stat(filePath, (err, stats) => {
    if (!err && stats.isFile()) {
      const ext = path.extname(filePath).toLowerCase();
      const contentType = MIME_TYPES[ext] || 'application/octet-stream';
      res.writeHead(200, {
        'Content-Type': contentType,
        'Cache-Control': ext === '.html' ? 'no-cache' : 'public, max-age=31536000, immutable'
      });
      fs.createReadStream(filePath).pipe(res);
    } else {
      // SPA Fallback: serve index.html for unknown routes (e.g., /portal, /admin, /dashboard)
      const indexPath = path.join(WEB_ROOT, 'index.html');
      fs.readFile(indexPath, (readErr, content) => {
        if (readErr) {
          res.writeHead(404, { 'Content-Type': 'text/plain' });
          res.end('Frontend application index.html not found. Please build the frontend first.');
        } else {
          res.writeHead(200, { 'Content-Type': 'text/html; charset=UTF-8' });
          res.end(content);
        }
      });
    }
  });
});

server.listen(PORT, '0.0.0.0', () => {
  console.log(`[INFO] Staging Frontend SPA running on http://localhost:${PORT}`);
});
