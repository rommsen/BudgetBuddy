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
import { mkdtemp, rm } from "node:fs/promises";
import { fileURLToPath } from "node:url";
// Playwright wird hier bewusst NICHT auf Modulebene importiert
// (design-system-q2bmv): nur die browsergebundenen Tests weiter unten laden
// es per dynamischem Import, genau dann, wenn ein Browser gebraucht wird. So
// verlieren die restlichen, browserfreien Tests dieser Datei nicht mit,
// falls Playwright auf einer Maschine fehlt.
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
  captureAtDocumentHeight,
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
/// Bekannte Grenze dieses Parsers: er erkennt nur Match-Arme der Form
/// `| ["segment"] -> Page` (einzelnes String-Literal-Segment) plus den
/// Wurzel-Arm `| [] -> Page`. Ein künftiger Arm mit mehreren Segmenten oder
/// einer Variable (z. B. `| ["ledger"; txId] -> ...`) würde von diesem
/// Regex nicht erfasst und müsste den Parser hier mitziehen. Das ist ein
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
  const armPattern = /\|\s*\[\s*"([^"]+)"\s*\]\s*->/g;
  let match;
  while ((match = armPattern.exec(block)) !== null) {
    routes.add(`/${match[1]}`);
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

// Regressionstest für design-system-yxrxb: waitForPageLoaded wartete früher auf das
// Verschwinden von ".proto-spinner"/".animate-pulse", was an der /styleguide-Route
// scheiterte, weil deren Spinner/Skeletons Demo-Inhalte sind und nie verschwinden
// (s. Kommentar bei waitForPageLoaded in screenshot-route.mjs). Ersetzt durch das
// App-eigene Bereitschaftssignal `data-app-ready` (State.isPageReady, View.fs). Dieser
// Test liest die tatsächliche Quelldatei, nicht eine Kopie der Behauptung, damit ein
// künftiges Wiedereinführen der Klassen-Heuristik hier laut auffällt.
test("screenshot-route.mjs prüft nicht mehr auf .proto-spinner/.animate-pulse, sondern auf data-app-ready", () => {
  // Prüft die tatsächliche DOM-Abfrage, nicht jede Erwähnung der Klassennamen: der
  // Kommentar bei waitForPageLoaded erklärt die frühere Heuristik bewusst weiter
  // (Begründung des gewählten Wegs, AC5), ohne sie wieder auszuführen.
  const scriptPath = path.join(__dirname, "screenshot-route.mjs");
  const source = readFileSync(scriptPath, "utf8");
  assert.doesNotMatch(
    source,
    /querySelectorAll\(["'`]\.(proto-spinner|animate-pulse)["'`]\)/,
    "die Klassen-Heuristik darf nicht mehr tatsächlich abgefragt werden"
  );
  assert.match(source, /data-app-ready/, "das App-eigene Bereitschaftssignal muss abgefragt werden");
});

// Mechanisierte Durchsetzung der zweiten Regel aus infra-jh7g9/ADR 0034: der
// Backend-Port ist seit dieser Aufgabe konfigurierbar (Program.fs respektiert
// ASPNETCORE_URLS), das allein reicht aber nicht: startFrontend() und
// vite.config.js müssen denselben Port über BACKEND_PORT konsistent
// weiterreichen bzw. lesen, sonst redet das Frontend mit dem falschen
// Backend-Port, obwohl der Server selbst korrekt auf einem anderen Port
// läuft. Beide Tests lesen die tatsächliche Quelldatei (wie der
// data-app-ready-Test oben), kein funktionaler Lauf: ein echter
// Vite-Prozess-Start wäre für diesen schmalen Zweck unverhältnismäßig teuer.
// Dokumentierte Grenze: ein Scan über Quelltext kann eine korrekt benannte,
// aber falsch verdrahtete Variable nicht erkennen (z. B. BACKEND_PORT wird
// gesetzt, aber nie gelesen); das deckt vite.config.js-Test unten separat ab,
// indem er speziell auf process.env.BACKEND_PORT im Proxy-Ziel prüft, nicht
// nur auf irgendeine Erwähnung des Namens.
test("startFrontend gibt BACKEND_PORT an den Vite-Kindprozess weiter (infra-jh7g9)", () => {
  const scriptPath = path.join(__dirname, "screenshot-route.mjs");
  const source = readFileSync(scriptPath, "utf8");
  const startFrontendIdx = source.indexOf("async function startFrontend");
  assert.notEqual(startFrontendIdx, -1, "startFrontend nicht gefunden (Funktion umbenannt oder verschoben?)");
  const nextFunctionIdx = source.indexOf("\nasync function", startFrontendIdx + 1);
  const body = source.slice(startFrontendIdx, nextFunctionIdx === -1 ? source.length : nextFunctionIdx);
  assert.match(
    body,
    /BACKEND_PORT:\s*String\(backendPort\)/,
    "startFrontend muss BACKEND_PORT an den Vite-Kindprozess übergeben, sonst proxyt Vite gegen den falschen Backend-Port (siehe ADR 0034)"
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

// AC6 (design-system-q2bmv): die Aufnahme-Mechanik lebt jetzt in der eigenen,
// exportierten Funktion captureAtDocumentHeight, nicht mehr inline in
// captureScreenshot. Die Quelltext-Zusicherung gegen `fullPage: true` zielt
// deshalb ab jetzt auf den Rumpf von captureAtDocumentHeight; ein separater
// Test darunter stellt sicher, dass captureScreenshot diese Funktion
// tatsächlich ruft statt selbst wieder fullPage zu setzen.
test("captureAtDocumentHeight verwendet computeCaptureViewport und nimmt ohne fullPage auf (ledger-p9k3w)", () => {
  // Regressionstest gegen die Quelldatei (wie der data-app-ready-Test oben):
  // captureAtDocumentHeight selbst braucht einen echten Playwright-Kontext
  // gegen eine geladene Seite und ist hier nicht sinnvoll aufzurufen (s.
  // Kopfkommentar dieser Datei). Die eigentliche Rechenregel prüfen die drei
  // Tests oben an der reinen Funktion computeCaptureViewport; die
  // Browser-Tests weiter unten rufen captureAtDocumentHeight tatsächlich auf
  // und prüfen das entstandene PNG. Dieser Test schließt die Lücke
  // dazwischen: er stellt sicher, dass captureAtDocumentHeight die geprüfte
  // Rechenregel tatsächlich verwendet, statt unbedingt mit `fullPage: true`
  // aufzunehmen.
  const scriptPath = path.join(__dirname, "screenshot-route.mjs");
  const source = readFileSync(scriptPath, "utf8");
  const startIdx = source.indexOf("export async function captureAtDocumentHeight");
  assert.notEqual(startIdx, -1, "captureAtDocumentHeight nicht gefunden (Funktion umbenannt oder verschoben?)");
  const nextFunctionIdx = source.indexOf("\nasync function", startIdx + 1);
  const body = source.slice(startIdx, nextFunctionIdx === -1 ? source.length : nextFunctionIdx);
  assert.doesNotMatch(body, /fullPage:\s*true/, "captureAtDocumentHeight darf nicht mehr mit fullPage: true aufnehmen");
  assert.match(body, /computeCaptureViewport\(/, "captureAtDocumentHeight muss computeCaptureViewport verwenden");
  assert.match(body, /setViewportSize\(/, "captureAtDocumentHeight muss den Viewport vor der Aufnahme setzen");
  assert.match(body, /fullPage:\s*false/, "captureAtDocumentHeight muss ohne fullPage aufnehmen (Viewport ist bereits passend)");
});

test("captureScreenshot ruft captureAtDocumentHeight statt selbst fullPage zu setzen (design-system-q2bmv)", () => {
  const scriptPath = path.join(__dirname, "screenshot-route.mjs");
  const source = readFileSync(scriptPath, "utf8");
  const startIdx = source.indexOf("async function captureScreenshot");
  assert.notEqual(startIdx, -1, "captureScreenshot nicht gefunden (Funktion umbenannt oder verschoben?)");
  const nextFunctionIdx = source.indexOf("\nasync function", startIdx + 1);
  const body = source.slice(startIdx, nextFunctionIdx === -1 ? source.length : nextFunctionIdx);
  assert.doesNotMatch(body, /fullPage/, "captureScreenshot darf selbst kein fullPage mehr setzen, das gehört jetzt zu captureAtDocumentHeight");
  assert.match(body, /captureAtDocumentHeight\(/, "captureScreenshot muss die echte Aufnahme-Funktion verwenden, kein Nachbau");
});

/// Liest Breite/Höhe direkt aus den IHDR-Bytes eines PNG-Puffers (big-endian
/// uint32 ab Byte 16 bzw. 20, PNG-Spezifikation). Kein neues Abhängigkeit
/// nötig (kein image-size/sharp im Projekt): der Signatur- und
/// IHDR-Chunk-Aufbau ist fest und einfach genug, um ihn hier direkt zu lesen,
/// statt den Bildinhalt nur per Augenschein zu beurteilen (Testvorgabe AC2:
/// "maschinell belegt").
function pngDimensions(buffer) {
  return { width: buffer.readUInt32BE(16), height: buffer.readUInt32BE(20) };
}

/// Zählt Pixel exakt rgb(255,0,0) in einem PNG (design-system-q2bmv, AC3
/// dieser Aufgabe: die Pixelprüfung kommt ohne neue Abhängigkeit aus). Das
/// PNG wird als Data-URL auf ein `<canvas>` innerhalb des bereits geöffneten
/// Browsers gezeichnet und per `getImageData` gelesen, statt eine
/// PNG-Decoding-Bibliothek einzuführen: dasselbe Motiv wie bei
/// pngDimensions oben, nur für Pixelinhalt statt nur für die Maße.
async function countRedPixels(page, pngPath) {
  const dataUrl = `data:image/png;base64,${readFileSync(pngPath).toString("base64")}`;
  return page.evaluate(async (url) => {
    const img = new Image();
    const loaded = new Promise((resolve, reject) => {
      img.onload = resolve;
      img.onerror = reject;
    });
    img.src = url;
    await loaded;
    const canvas = document.createElement("canvas");
    canvas.width = img.width;
    canvas.height = img.height;
    const ctx = canvas.getContext("2d");
    ctx.drawImage(img, 0, 0);
    const data = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
    let redCount = 0;
    for (let i = 0; i < data.length; i += 4) {
      if (data[i] === 255 && data[i + 1] === 0 && data[i + 2] === 0) {
        redCount++;
      }
    }
    return redCount;
  }, dataUrl);
}

// Die drei folgenden Tests prüfen den echten Browser-Mechanismus hinter dem
// Fix (captureAtDocumentHeight, also setViewportSize + page.screenshot mit
// fullPage:false) gegen eine selbst erzeugte, lokale Seite: ohne App, ohne
// Backend, ohne Datenbankkopie (kein DATA_DIR-Zugriff, ADR 0030 bleibt
// unberührt). Alle drei rufen die echte, aus screenshot-route.mjs
// importierte Funktion auf, kein Nachbau (design-system-q2bmv, AC5/AC6).
test("captureAtDocumentHeight: das Bild einer langen Seite hat exakt die Dokumenthöhe (AC4, echter Browser)", async () => {
  const { chromium } = await import("playwright");
  const browser = await chromium.launch();
  const tmpDir = await mkdtemp(path.join(os.tmpdir(), "bb-screenshot-route-test-"));
  const outPath = path.join(tmpDir, "tall.png");
  let documentHeight;
  try {
    const baseViewport = { width: 390, height: 600 };
    const context = await browser.newContext({ viewport: baseViewport });
    const page = await context.newPage();
    try {
      // Deutlich höher als der Viewport, damit fullPage:true diese Seite
      // früher hätte vergrößern müssen.
      await page.setContent(
        '<html><body style="margin:0"><div style="height:2200px;background:#000"></div></body></html>'
      );
      documentHeight = await page.evaluate(() => document.documentElement.scrollHeight);
      assert.ok(documentHeight > baseViewport.height, "Testaufbau: Inhalt muss höher als der Viewport sein");
      await captureAtDocumentHeight(page, baseViewport, outPath);
    } finally {
      await context.close();
    }
    const dims = pngDimensions(readFileSync(outPath));
    assert.equal(dims.width, baseViewport.width);
    assert.equal(dims.height, documentHeight, "Bildhöhe muss der gemessenen Dokumenthöhe entsprechen (AC4)");
    assert.ok(dims.height > baseViewport.height, "Bildhöhe muss über die ursprüngliche Viewport-Höhe hinausgewachsen sein");
  } finally {
    await browser.close();
    await rm(tmpDir, { recursive: true, force: true });
  }
});

test("captureAtDocumentHeight: das Bild einer kurzen Seite behält die ursprünglichen Abmessungen (AC3, echter Browser)", async () => {
  const { chromium } = await import("playwright");
  const browser = await chromium.launch();
  const tmpDir = await mkdtemp(path.join(os.tmpdir(), "bb-screenshot-route-test-"));
  const outPath = path.join(tmpDir, "short.png");
  try {
    const baseViewport = { width: 1440, height: 900 };
    const context = await browser.newContext({ viewport: baseViewport });
    const page = await context.newPage();
    try {
      // Deutlich niedriger als der Viewport: keine scrollbare Seite.
      await page.setContent(
        '<html><body style="margin:0"><div style="height:100px;background:#000"></div></body></html>'
      );
      // document.documentElement.scrollHeight ist nie kleiner als der
      // Viewport (das <html>-Element füllt ihn per UA-Vorgabe aus, auch bei
      // kürzerem Inhalt): genau der Fall, den AC3 abdeckt. Kein Scrollen, die
      // Rechenregel darf hier nicht künstlich schrumpfen.
      const documentHeight = await page.evaluate(() => document.documentElement.scrollHeight);
      assert.ok(documentHeight <= baseViewport.height, "Testaufbau: Inhalt darf den Viewport nicht überragen");
      await captureAtDocumentHeight(page, baseViewport, outPath);
    } finally {
      await context.close();
    }
    const dims = pngDimensions(readFileSync(outPath));
    assert.equal(dims.width, baseViewport.width);
    assert.equal(dims.height, baseViewport.height, "Bildhöhe muss die bisherige Viewport-Höhe behalten (AC3)");
  } finally {
    await browser.close();
    await rm(tmpDir, { recursive: true, force: true });
  }
});

// AC1/AC2/AC3 (design-system-q2bmv), der eigentliche Kern dieser Aufgabe:
// baut per page.setContent eine Seite mit demselben Aufbau wie ADR 0005
// (`.bottom-sheet` in src/Client/styles.css:1556, `top: calc(var(--vv-top,
// 0px) + var(--vvh, 100dvh))`) und stellt zuerst per Gegenprobe sicher, dass
// `fullPage: true` den Fehler aus ledger-p9k3w hier wirklich zeigt (sonst
// wäre die zweite Zusicherung nicht falsifizierend). Erst danach prüft der
// Test die echte Funktion captureAtDocumentHeight gegen dieselbe Seite.
test("captureAtDocumentHeight hält die .bottom-sheet-Leiste außerhalb des Bilds, wo fullPage:true sie hineinrutschen ließe (ADR 0005, ledger-p9k3w)", async () => {
  const { chromium } = await import("playwright");
  const browser = await chromium.launch();
  const tmpDir = await mkdtemp(path.join(os.tmpdir(), "bb-screenshot-route-test-"));
  const fullPagePath = path.join(tmpDir, "fullpage.png");
  const fixedPath = path.join(tmpDir, "fixed.png");
  try {
    const baseViewport = { width: 390, height: 600 };
    const context = await browser.newContext({ viewport: baseViewport });
    const page = await context.newPage();
    let documentHeight;
    try {
      // Dieselbe Positionierungsregel wie .bottom-sheet: im inaktiven
      // Zustand knapp unterhalb des sichtbaren Bereichs positioniert.
      await page.setContent(
        '<html><body style="margin:0">' +
          '<div style="height:2200px;background:#000"></div>' +
          '<div style="position:fixed;top:calc(var(--vv-top, 0px) + var(--vvh, 100dvh));' +
          'left:0;right:0;height:80px;background:rgb(255,0,0)"></div>' +
          "</body></html>"
      );
      documentHeight = await page.evaluate(() => document.documentElement.scrollHeight);

      // Gegenprobe (AC1): ohne den Fix muss die Leiste im Bild landen, sonst
      // ist der Fehler mit diesem Aufbau nicht nachgestellt.
      await page.screenshot({ path: fullPagePath, fullPage: true });
      const fullPageRedCount = await countRedPixels(page, fullPagePath);
      assert.ok(
        fullPageRedCount > 0,
        "Gegenprobe: fullPage:true muss mindestens ein Pixel rgb(255,0,0) zeigen, sonst ist der Fehler hier nicht nachgestellt"
      );

      // Die echte Funktion (kein Nachbau, AC2): hält die Leiste draußen.
      await captureAtDocumentHeight(page, baseViewport, fixedPath);
      const fixedRedCount = await countRedPixels(page, fixedPath);
      assert.equal(
        fixedRedCount,
        0,
        "captureAtDocumentHeight darf kein einziges Pixel rgb(255,0,0) ins Bild lassen"
      );
    } finally {
      await context.close();
    }
    const dims = pngDimensions(readFileSync(fixedPath));
    assert.equal(dims.height, documentHeight, "Bildhöhe muss der gemessenen Dokumenthöhe entsprechen");
  } finally {
    await browser.close();
    await rm(tmpDir, { recursive: true, force: true });
  }
});

// AC7 (design-system-q2bmv): stellt sicher, dass diese Datei Playwright
// nicht mehr am Modulkopf lädt, sondern erst innerhalb der Tests, die einen
// Browser tatsächlich brauchen. Liest die eigene Quelldatei (wie der
// data-app-ready-Test oben screenshot-route.mjs liest), statt nur zu
// behaupten, dass der Import entfernt wurde.
test("screenshot-route.test.mjs lädt Playwright nicht mehr am Modulkopf, sondern erst wenn ein Browser gebraucht wird", () => {
  const selfPath = fileURLToPath(import.meta.url);
  const source = readFileSync(selfPath, "utf8");
  const firstTestIdx = source.indexOf('test("');
  assert.notEqual(firstTestIdx, -1, "kein test(-Aufruf gefunden, Testdatei umgebaut?");
  const header = source.slice(0, firstTestIdx);
  assert.doesNotMatch(
    header,
    /import\s*\{\s*chromium\s*\}\s*from\s*["']playwright["']/,
    "playwright darf nicht mehr am Modulkopf importiert werden, sonst verlieren auch die browserfreien Tests ihre Fähigkeit, ohne installiertes Playwright zu laufen"
  );
  assert.match(
    source,
    /await import\(\s*["']playwright["']\s*\)/,
    'die browsergebundenen Tests müssen Playwright per await import("playwright") lazy laden'
  );
});
