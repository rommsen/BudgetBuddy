// Kleiner statischer Server nur mit Node-Standardbibliothek, für die
// Playwright-Wächter (infrastructure-eq27s). Aufruf: node static-serve.mjs <dir> <port>
//
// Liefert Dateien aus <dir>, fällt für unbekannte Pfade ohne Dateiendung auf
// index.html zurück (SPA) und antwortet für den Service Worker (/sw.js und
// registerSW-Varianten) mit 404. Playwright fängt Anfragen eines Service Workers
// nicht ab; ohne diesen 404 würden die page.route-Mocks der Wächter umgangen.
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".webmanifest": "application/manifest+json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".ico": "image/x-icon",
  ".woff": "font/woff",
  ".woff2": "font/woff2",
  ".map": "application/json; charset=utf-8",
};

const SERVICE_WORKER_PATHS = /^\/(sw\.js|registerSW\.js|workbox-[^/]*\.js)$/;

export function createStaticServer(dir) {
  const root = path.resolve(dir);
  return http.createServer((req, res) => {
    let pathname;
    try {
      pathname = decodeURIComponent(new URL(req.url, "http://localhost").pathname);
    } catch {
      res.writeHead(400).end();
      return;
    }
    if (SERVICE_WORKER_PATHS.test(pathname)) {
      res.writeHead(404).end("Service Worker in Wächter-Läufen abgeschaltet");
      return;
    }
    let file = path.join(root, pathname === "/" ? "index.html" : pathname);
    if (file !== root && !file.startsWith(root + path.sep)) {
      res.writeHead(403).end();
      return;
    }
    let exists = fs.existsSync(file) && fs.statSync(file).isFile();
    if (!exists && path.extname(pathname) === "") {
      file = path.join(root, "index.html");
      exists = fs.existsSync(file);
    }
    if (!exists) {
      res.writeHead(404).end();
      return;
    }
    res.writeHead(200, {
      "Content-Type": MIME[path.extname(file)] ?? "application/octet-stream",
    });
    fs.createReadStream(file).pipe(res);
  });
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const [dir, port] = process.argv.slice(2);
  createStaticServer(dir).listen(Number(port), "127.0.0.1");
}
