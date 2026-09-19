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
import {
  DEFAULT_OUT_DIR,
  KNOWN_ROUTES,
  normalizeRoute,
  routeSegments,
  slugifyRoute,
  assertKnownRoute,
  screenshotFilenames,
  parseArgs,
  assertDataDirIsolated,
  startBackend,
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

test("startBackend: scheitert laut, BEVOR ein Prozess gestartet wird, wenn dataDir auf das Original zeigt", async () => {
  // Das ist der eigentliche Wachtposten aus AC6, nicht nur die reine
  // Helfer-Funktion: startBackend() ruft assertDataDirIsolated() mit genau
  // dem Wert auf, der unten in env.DATA_DIR landen würde. assertDataDirIsolated()
  // wirft synchron als erste Anweisung, vor jedem await. Hier läuft also
  // nie ein echter dotnet-Prozess, obwohl startBackend() selbst geprüft wird.
  const originalDir = path.join(os.homedir(), "my_apps", "budgetbuddy-dev");
  await assert.rejects(() => startBackend(originalDir, originalDir), /Original/);
});
