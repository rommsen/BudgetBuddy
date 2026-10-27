// scripts/pwa-update.test.mjs
//
// Wächter für `infrastructure-bnbdc` (ADR 0057): die installierte PWA übernimmt einen neuen
// Deploy, ohne dass der Cache von Hand gelöscht wird. Der Build läuft mit
// `registerType: 'prompt'` und `injectRegister: false`:
//   - `index.html` lädt kein `registerSW.js` (der Client registriert selbst, Pwa.fs),
//   - der Service Worker ruft `skipWaiting` nur als Antwort auf die Nachricht SKIP_WAITING
//     (also wenn `Pwa.applyUpdate` ihn freigibt), nie von allein,
//   - `clientsClaim` bleibt, damit die erste Installation die Seite übernimmt.
// Gebaut wird in ein Wegwerf-Verzeichnis, `dist/public` bleibt unberührt.
//
// Ausführen: node --test scripts/pwa-update.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
let outDir;
let sw;
let html;

// Mit BB_GUARD_DIST (scripts/run-guards.mjs) gibt es schon ein gebautes Bundle:
// kein zweiter Build. Direkt gestartet baut der Wächter selbst.
const sharedDist = process.env.BB_GUARD_DIST;

before(() => {
  if (sharedDist) {
    outDir = sharedDist;
  } else {
    outDir = fs.mkdtempSync(path.join(os.tmpdir(), "bb-pwa-update-"));
    execFileSync("npx", ["vite", "build", "--outDir", outDir, "--emptyOutDir"], {
      cwd: repoRoot,
      stdio: "pipe",
      timeout: 240000,
    });
  }
  sw = fs.readFileSync(path.join(outDir, "sw.js"), "utf8");
  html = fs.readFileSync(path.join(outDir, "index.html"), "utf8");
});

after(() => {
  if (outDir && !sharedDist) fs.rmSync(outDir, { recursive: true, force: true });
});

test("vite.config.js: registerType prompt, kein injizierter Registrierungs-Script", () => {
  const config = fs.readFileSync(path.join(repoRoot, "vite.config.js"), "utf8");
  assert.match(config, /registerType:\s*'prompt'/);
  assert.match(config, /injectRegister:\s*false/);
  assert.match(config, /clientsClaim:\s*true/);
});

test("index.html lädt kein registerSW.js", () => {
  assert.ok(!html.includes("registerSW"), "index.html darf kein registerSW.js einbinden");
});

test("sw.js ruft skipWaiting nur als Antwort auf SKIP_WAITING", () => {
  const calls = sw.match(/skipWaiting\(\)/g) ?? [];
  assert.equal(calls.length, 1, "genau ein skipWaiting-Aufruf");
  assert.match(sw, /"SKIP_WAITING"===\w+\.data\.type&&self\.skipWaiting\(\)/);
});

test("sw.js übernimmt die Seite bei der ersten Installation (clientsClaim)", () => {
  assert.match(sw, /clientsClaim\(\)/);
});
