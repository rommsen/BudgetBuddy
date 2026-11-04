// scripts/screenshot-route.test.mjs
//
// Unit-Tests für die reinen Helfer-Funktionen aus screenshot-route.mjs
// (design-system-wa6wz). Kein E2E-Harness im Projekt (dotnet test deckt nur
// F# ab); dies läuft über Node's eingebauten Test-Runner, ohne neue
// Abhängigkeit:
//
//   node --test scripts/screenshot-route.test.mjs
//
// Die Orchestrierung selbst (Server/Frontend starten, Playwright, Aufräumen)
// ist nicht sinnvoll unit-testbar (braucht echten Prozess-Start); dafür
// dienen tatsächliche Läufe des Skripts als Verifikation (s. Worker-Notizen
// im Verifikationsprotokoll dieser Aufgabe).

import { test } from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import os from "node:os";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
// Diese Datei startet weder Browser noch Server (infrastructure-t5m2b). Die eine Aufnahme im echten
// Browser steht in scripts/screenshot-route-capture.test.mjs.
import {
  DEFAULT_OUT_DIR,
  KNOWN_ROUTES,
  ROUTES_WITHOUT_BACKEND,
  routeNeedsBackend,
  normalizeRoute,
  routeSegments,
  slugifyRoute,
  assertKnownRoute,
  screenshotFilenames,
  parseArgs,
  assertDataDirIsolated,
  startBackend,
  computeCaptureViewport,
  frontendServerKind,
} from "./screenshot-route.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

test("normalizeRoute: Hash-Präfix wird entfernt, fehlender Slash ergänzt", () => {
  assert.equal(normalizeRoute("#/ledger"), "/ledger");
  assert.equal(normalizeRoute("ledger"), "/ledger");
  assert.equal(normalizeRoute("#/"), "/");
  assert.equal(normalizeRoute("/"), "/");
});

test("normalizeRoute: leere Route ist ein Fehler (kein stiller Fallback)", () => {
  assert.throws(() => normalizeRoute(""), /Route/);
  assert.throws(() => normalizeRoute(undefined), /Route/);
});

test("routeSegments: zerlegt einen normalisierten Pfad in Segmente", () => {
  assert.deepEqual(routeSegments("/ledger"), ["ledger"]);
  assert.deepEqual(routeSegments("/"), []);
  assert.deepEqual(routeSegments("/quickadd"), ["quickadd"]);
});

test("slugifyRoute: Segmente werden zum Dateinamens-Bestandteil, Wurzelroute wird 'start'", () => {
  assert.equal(slugifyRoute("/ledger"), "ledger");
  assert.equal(slugifyRoute("/"), "start");
  assert.equal(slugifyRoute("/quickadd"), "quickadd");
});

/// Liest src/Client/Types.fs und extrahiert die Match-Arme von
/// Routing.parseUrl tatsächlich (Regex auf den Quelltext), statt eine
/// zweite, hartkodierte Kopie der Routenliste zu pflegen. So bemerkt der
/// Test Drift zwischen KNOWN_ROUTES und Types.fs wirklich: pinnte die
/// frühere Fassung nur eine Kopie an eine andere Kopie, ohne Types.fs je zu
/// lesen, kann diese Fassung nicht mehr unbemerkt auseinanderlaufen.
///
/// Bekannte Grenze dieses Parsers: er erkennt nur Match-Arme aus lauter
/// String-Literal-Segmenten (`| ["segment"] -> Page`, `| ["budget"; "history"] -> Page`)
/// plus den Wurzel-Arm `| [] -> Page`. Ein Arm mit einer Variable
/// (z. B. `| ["ledger"; txId] -> ...`) ist keine feste Route und wird nicht erfasst. Das ist ein
/// bewusster, benannter Kompromiss (ein vollständiger F#-Parser wäre für
/// dieses Skript unverhältnismäßig), kein stiller blinder Fleck: fehlt ein
/// Segment-Typ, den KNOWN_ROUTES aber kennt, schlägt der Vergleich unten an.
function extractKnownRoutesFromParseUrl(source) {
  const startIdx = source.indexOf("let parseUrl");
  if (startIdx === -1) {
    throw new Error("parseUrl nicht gefunden in Types.fs; die Drift-Prüfung kann die Routing-Fälle nicht lesen.");
  }
  const matchIdx = source.indexOf("match segments with", startIdx);
  const endIdx = source.indexOf("let toUrlSegments", matchIdx);
  const block = source.slice(matchIdx, endIdx === -1 ? source.length : endIdx);

  const routes = new Set();
  if (/\|\s*\[\s*\]\s*->/.test(block)) {
    routes.add("/");
  }
  // Ein Arm aus lauter String-Literalen, auch mehreren (`| ["budget"; "history"] -> ...`, budgeting-c6vn4).
  // Ein Arm mit einer Variable (`| ["ledger"; "entry"; id] -> ...`) ist keine feste Route und fällt heraus.
  const armPattern = /\|\s*\[\s*("[^"]+"(?:\s*;\s*"[^"]+")*)\s*\]\s*->/g;
  let match;
  while ((match = armPattern.exec(block)) !== null) {
    const segments = [...match[1].matchAll(/"([^"]+)"/g)].map((m) => m[1]);
    routes.add(`/${segments.join("/")}`);
  }
  return routes;
}

test("KNOWN_ROUTES spiegelt Routing.parseUrl aus src/Client/Types.fs (echter Parse, kein Duplikat)", () => {
  const typesFsPath = path.join(__dirname, "..", "src", "Client", "Types.fs");
  const source = readFileSync(typesFsPath, "utf8");
  const routesFromSource = extractKnownRoutesFromParseUrl(source);
  assert.deepEqual([...KNOWN_ROUTES].sort(), [...routesFromSource].sort());
});

test("assertKnownRoute: bekannte Routen werfen nicht", () => {
  for (const route of KNOWN_ROUTES) {
    assert.doesNotThrow(() => assertKnownRoute(route));
  }
});

test("assertKnownRoute: unbekannte Route wirft eine Fehlermeldung, die die Route nennt", () => {
  assert.throws(() => assertKnownRoute("/frobnicate"), (err) => {
    assert.match(err.message, /frobnicate/);
    return true;
  });
});

// design-system-m4kp7 (AC4): eine Route ohne Kennzeichen in
// ROUTES_WITHOUT_BACKEND gilt als datenbedürftig, nicht als präsentational —
// die sichere Annahme bleibt die Voreinstellung. Geprüft an einer bekannten
// Route ohne Kennzeichen (/ledger, taucht bewusst NICHT in
// ROUTES_WITHOUT_BACKEND auf) und an einer völlig unbekannten Route, die auch
// in keiner der beiden Listen steht.
test("routeNeedsBackend: eine Route ohne Kennzeichen in ROUTES_WITHOUT_BACKEND gilt als datenbedürftig", () => {
  assert.equal(ROUTES_WITHOUT_BACKEND.has("/ledger"), false);
  assert.equal(routeNeedsBackend("/ledger"), true);
  assert.equal(routeNeedsBackend("/frobnicate"), true);
});

test("routeNeedsBackend: /styleguide ist als einzige Route als präsentational gekennzeichnet", () => {
  assert.equal(routeNeedsBackend("/styleguide"), false);
  for (const route of KNOWN_ROUTES) {
    if (route === "/styleguide") continue;
    assert.equal(routeNeedsBackend(route), true, `${route} sollte datenbedürftig sein`);
  }
});

test("screenshotFilenames: folgt der Konvention handy-390x844-<name>/pc-1440x900-<name>", () => {
  const { mobile, desktop } = screenshotFilenames("ledger", "/tmp/out");
  assert.equal(mobile, path.join("/tmp/out", "handy-390x844-ledger.png"));
  assert.equal(desktop, path.join("/tmp/out", "pc-1440x900-ledger.png"));
});

test("parseArgs: Route ohne --out verwendet die dokumentierte Vorgabe", () => {
  const result = parseArgs(["#/ledger"]);
  assert.equal(result.route, "#/ledger");
  assert.equal(result.outDir, DEFAULT_OUT_DIR);
});

test("parseArgs: --out überschreibt das Zielverzeichnis", () => {
  const result = parseArgs(["#/ledger", "--out", "docs/ux/kassenbuch/screenshots/"]);
  assert.equal(result.route, "#/ledger");
  assert.equal(result.outDir, "docs/ux/kassenbuch/screenshots/");
});

test("parseArgs: fehlende Route ist ein Fehler", () => {
  assert.throws(() => parseArgs([]), /Route/);
  assert.throws(() => parseArgs(["--out", "foo/"]), /Route/);
});

test("parseArgs: --out ohne Wert ist ein sprechender Fehler, keine spätere TypeError", () => {
  // Regressionstest: der frühere Code nahm bei `--out` ohne Wert `undefined`
  // als outDir und scheiterte erst deutlich später in path.resolve() mit
  // einer nicht sprechenden TypeError statt hier, sofort, mit einer
  // Meldung, die auf --out zeigt.
  assert.throws(() => parseArgs(["#/ledger", "--out"]), /--out/);
});

test("parseArgs: ein unbekanntes Flag wird benannt abgelehnt, nicht als Route gelesen", () => {
  // Regressionstest: der frühere Code las jedes unbekannte Flag als Route,
  // z. B. wurde aus `--unbekannt` eine Route '--unbekannt'.
  assert.throws(() => parseArgs(["--unbekannt", "#/ledger"]), /Unbekanntes Flag/);
  assert.throws(() => parseArgs(["--unbekannt", "#/ledger"]), /--unbekannt/);
});

test("assertDataDirIsolated: wirft laut, wenn das Kandidaten-Verzeichnis auf das Original zeigt", () => {
  const real = "/Users/roman/my_apps/budgetbuddy-dev";
  assert.throws(() => assertDataDirIsolated(real, real), /Original/);
  // Auch bei äquivalenten, aber unterschiedlich geschriebenen Pfaden (das
  // Original selbst über einen Umweg getroffen):
  assert.throws(
    () => assertDataDirIsolated(path.join(real, "."), real),
    /Original/
  );
});

test("assertDataDirIsolated: wirft laut, wenn das Kandidaten-Verzeichnis INNERHALB des Originals liegt", () => {
  // Nicht nur Gleichheit: eine Unterordner-Kopie ist genauso ein Original-
  // Zugriff (z. B. würde eine Kopie nach budgetbuddy-dev/tmp/ trotzdem die
  // -wal/-shm-Dateien des echten Servers im selben Verzeichnisbaum sehen).
  const real = "/Users/roman/my_apps/budgetbuddy-dev";
  assert.throws(
    () => assertDataDirIsolated(path.join(real, "unterordner"), real),
    /Original/
  );
});

test("assertDataDirIsolated: ein Geschwister-Pfad mit gemeinsamem String-Präfix ist KEIN Original-Zugriff", () => {
  // Grenzfall für eine naive String-Prüfung (startsWith statt echter
  // Pfad-Containment): "budgetbuddy-dev-backup" beginnt als String mit
  // "budgetbuddy-dev", liegt aber nicht darin.
  const real = "/Users/roman/my_apps/budgetbuddy-dev";
  const sibling = "/Users/roman/my_apps/budgetbuddy-dev-backup";
  assert.doesNotThrow(() => assertDataDirIsolated(sibling, real));
});

test("assertDataDirIsolated: lässt ein echtes Temp-Verzeichnis unbeanstandet", () => {
  assert.doesNotThrow(() =>
    assertDataDirIsolated("/tmp/bb-screenshot-route-abc123", "/Users/roman/my_apps/budgetbuddy-dev")
  );
});

test("vite.config.js liest BACKEND_PORT aus der Umgebung fürs Proxy-Ziel (infra-jh7g9)", () => {
  const viteConfigPath = path.join(__dirname, "..", "vite.config.js");
  const source = readFileSync(viteConfigPath, "utf8");
  assert.match(
    source,
    /process\.env\.BACKEND_PORT/,
    "vite.config.js muss BACKEND_PORT lesen, sonst bleibt das Proxy-Ziel fest auf 5081 (siehe ADR 0034)"
  );
});

test("startBackend: scheitert laut, BEVOR ein Prozess gestartet wird, wenn dataDir auf das Original zeigt", async () => {
  // Das ist der eigentliche Wachtposten aus AC6, nicht nur die reine
  // Helfer-Funktion: startBackend() ruft assertDataDirIsolated() mit genau
  // dem Wert auf, der unten in env.DATA_DIR landen würde. assertDataDirIsolated()
  // wirft synchron als erste Anweisung, vor jedem await. Hier läuft also
  // nie ein echter dotnet-Prozess, obwohl startBackend() selbst geprüft wird.
  // Der Port (zweites Argument seit infra-jh7g9) ist für diesen Test
  // gleichgültig: er wird nie erreicht, weil assertDataDirIsolated vorher wirft.
  const originalDir = path.join(os.homedir(), "my_apps", "budgetbuddy-dev");
  await assert.rejects(() => startBackend(originalDir, 5081, originalDir), /Original/);
});

// ledger-p9k3w: fullPage-Aufnahmen ließen Chromium den Viewport intern auf
// die volle Scroll-Höhe vergrößern, wobei `100dvh` in .bottom-sheet
// (src/Client/styles.css:1556ff.) neu berechnet wurde und die eigentlich
// inaktive Leiste ins Bild rutschte. computeCaptureViewport() ist die reine
// Rechenregel des Fixes (s. Kommentar bei captureScreenshot in
// screenshot-route.mjs): Viewport wächst auf die gemessene Dokumenthöhe,
// schrumpft aber nie unter die angeforderte Höhe.
test("computeCaptureViewport: wächst auf die Dokumenthöhe, wenn der Inhalt höher als der Viewport ist (AC2)", () => {
  assert.deepEqual(computeCaptureViewport({ width: 390, height: 844 }, 2400), {
    width: 390,
    height: 2400,
  });
});

test("computeCaptureViewport: behält die angeforderte Höhe, wenn der Inhalt kleiner ist (AC3)", () => {
  // Regressionstest für den ursprünglichen Bug: eine Route ohne scrollbaren
  // Inhalt darf durch den Fix NICHT schrumpfen, sonst ändern sich ihre
  // bisherigen Bildabmessungen (AC3), obwohl an ihr nichts auszusetzen war.
  assert.deepEqual(computeCaptureViewport({ width: 1440, height: 900 }, 600), {
    width: 1440,
    height: 900,
  });
});

test("computeCaptureViewport: exakt passender Inhalt behält die angeforderte Höhe", () => {
  assert.deepEqual(computeCaptureViewport({ width: 1440, height: 900 }, 900), {
    width: 1440,
    height: 900,
  });
});

// infrastructure-eq27s: die Wächter laufen gegen einen statischen Build statt je Datei gegen Vite.
test("frontendServerKind: mit BB_GUARD_DIST und ohne Backend statisch, sonst Vite", () => {
  assert.equal(frontendServerKind({ BB_GUARD_DIST: "/tmp/dist" }, undefined), "static");
  assert.equal(frontendServerKind({ BB_GUARD_DIST: "/tmp/dist" }, 5081), "vite", "mit Backend braucht es den Proxy");
  assert.equal(frontendServerKind({}, undefined), "vite");
  assert.equal(frontendServerKind({ BB_GUARD_DIST: "" }, undefined), "vite", "leer zählt wie nicht gesetzt");
});
