// Wächter für scripts/static-serve.mjs (infrastructure-eq27s). Startet den Server direkt über
// startStaticFrontend gegen ein Verzeichnis mit drei Dateien, ohne Browser und ohne App-Build
// (infrastructure-t5m2b). Welcher Server startFrontend wählt, prüft frontendServerKind in
// scripts/screenshot-route.test.mjs.
import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { getFreePort, startStaticFrontend, stopServer } from "./screenshot-route.mjs";

let dir;
let port;
let child;

before(async () => {
  dir = fs.mkdtempSync(path.join(os.tmpdir(), "bb-static-serve-"));
  fs.writeFileSync(path.join(dir, "index.html"), "<html>INDEX</html>");
  fs.mkdirSync(path.join(dir, "assets"));
  fs.writeFileSync(path.join(dir, "assets", "app.js"), "console.log(1)");
  fs.writeFileSync(path.join(dir, "sw.js"), "// sw");
  port = await getFreePort();
  child = await startStaticFrontend(dir, port);
});

after(async () => {
  if (child) await stopServer(child, port);
  if (dir) fs.rmSync(dir, { recursive: true, force: true });
});

const get = (p) => fetch(`http://127.0.0.1:${port}${p}`);

test("liefert index.html für /", async () => {
  const r = await get("/");
  assert.equal(r.status, 200);
  assert.match(await r.text(), /INDEX/);
});

test("liefert Assets aus dem Build-Verzeichnis mit passendem Typ", async () => {
  const r = await get("/assets/app.js");
  assert.equal(r.status, 200);
  assert.match(r.headers.get("content-type"), /javascript/);
});

test("fällt für Pfade ohne Endung auf index.html zurück", async () => {
  const r = await get("/ledger/abc");
  assert.equal(r.status, 200);
  assert.match(await r.text(), /INDEX/);
});

test("antwortet für unbekannte Dateien mit 404", async () => {
  assert.equal((await get("/assets/fehlt.js")).status, 404);
});

test("antwortet für /sw.js mit 404, auch wenn die Datei existiert", async () => {
  assert.equal((await get("/sw.js")).status, 404);
  assert.equal((await get("/registerSW.js")).status, 404);
});

test("verlässt das Verzeichnis nicht", async () => {
  const r = await get("/..%2f..%2fetc/passwd");
  assert.notEqual(r.status, 200);
});
