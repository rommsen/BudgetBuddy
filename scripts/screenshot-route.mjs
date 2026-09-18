#!/usr/bin/env node
// scripts/screenshot-route.mjs
//
// Werkzeug für das visuelle Gate (design-system-wa6wz): nimmt eine beliebige
// Route der laufenden App headless in beiden Viewports auf, ohne dass jemand
// von Hand Server, Frontend und Playwright hochziehen muss. Anlass war
// `ledger-b1xwe`: alle Akzeptanzkriterien erfüllt, Verifikation bestanden,
// und das Ergebnis sah trotzdem nicht wie das Zielbild aus, weil niemand das
// Bild angesehen hat (README: "Mockups gehören ins Repo, und Frontend-Arbeit
// hat ein visuelles Gate").
//
// Nutzung:
//   node scripts/screenshot-route.mjs '#/ledger' --out docs/ux/kassenbuch/screenshots/
//   npm run screenshot:route -- '#/ledger'
//
// Anders als scripts/export-design-system.mjs (rein präsentationale
// /#/styleguide-Route, kommt mit `vite preview` aus) braucht das hier den
// echten Server: die meisten Routen laden Daten über den Vite-Proxy /api ->
// localhost:5081 (vite.config.js). Muster für Aufbau/Aufräumen übernommen
// aus export-design-system.mjs (getFreePort, startPreviewServer/stopServer,
// der finally-Block), hier auf Server + Frontend verdoppelt.
//
// Festgelegt mit Roman am 2026-09-18 (s. Task-Datei):
//   - Nur aufnehmen, nicht vergleichen. Der Abgleich mit dem Mockup bleibt
//     Augenarbeit.
//   - Die Datenbank ist eine Kopie in einem temporären Verzeichnis: das
//     Skript kopiert die Entwicklungsdatenbank (DATA_DIR, Vorgabe
//     ~/my_apps/budgetbuddy-dev/budgetbuddy.db) nach os.tmpdir() und startet
//     den Server ausschließlich mit DATA_DIR auf dieser Kopie. "Lesend
//     gegen die Entwicklungsdatenbank" gibt es nicht: der Server legt beim
//     Start Tabellen an und migriert (ADR 0030 ist genau an dieser Stelle
//     entstanden, als ein Testlauf ohne diese Vorsicht in die echte Datei
//     schrieb).
//
// Bekannte Grenze: der Backend-Port 5081 ist in src/Server/Program.fs fest
// verdrahtet (app.Run("http://0.0.0.0:5081")), nicht über ASPNETCORE_URLS
// konfigurierbar. Das Skript kann sich hier keinen freien Port reservieren
// (anders als beim Frontend); ist 5081 belegt, scheitert der Lauf laut,
// statt einen falschen Port zu erraten.
//
// Bekannte Grenze: eine unbekannte Route in Feliz.Router fällt in der App
// selbst still auf SyncFlow zurück (Routing.parseUrl in
// src/Client/Types.fs: "_ -> SyncFlow // Fallback to SyncFlow for unknown
// routes"), ohne die URL zu korrigieren oder einen Fehler zu zeigen. Ein
// leeres/falsches Bild wäre die Folge, ohne dass irgendetwas im DOM das
// verrät. Deshalb prüft dieses Skript die Route VOR jeder Navigation gegen
// KNOWN_ROUTES (Spiegel von Routing.parseUrl) und scheitert laut, wenn sie
// dort nicht auftaucht, statt sich auf das Rendering zu verlassen.
//
// Bekannte Grenze beim Aufräumen: `dotnet run` und `npx vite` starten je
// einen weiteren Kindprozess (den eigentlichen App- bzw. vite-Prozess).
// Beide werden hier `detached` gestartet, damit sie eine eigene
// Prozessgruppe bilden; stopServer() killt die ganze Gruppe (negative PID)
// statt nur des direkten Kindes. Setzt einer dieser Prozesse selbst
// `setsid`/löst sich aus der Gruppe, greift dieser Mechanismus nicht mehr.
// Dafür prüft stopServer() am Ende zusätzlich, ob der Port wirklich frei
// ist, und scheitert laut, wenn nicht.

import { spawn } from "node:child_process";
import { createServer as createNetServer, connect as netConnect } from "node:net";
import { rm, mkdir, mkdtemp, copyFile, access } from "node:fs/promises";
import path from "node:path";
import os from "node:os";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");

// Dokumentierte Vorgabe für --out (AC2): kein bereichsspezifischer Pfad,
// weil das Skript für beliebige Routen gilt, nicht nur für das Kassenbuch
// (dessen eigene Konvention docs/ux/kassenbuch/screenshots/ ist). Auch
// dokumentiert im README-Delta dieser Aufgabe und im Kommentar zu
// "screenshot:route" in package.json.
export const DEFAULT_OUT_DIR = path.join(ROOT, "docs", "ux", "screenshots");

// Fester Backend-Port (s. Kommentar oben, Program.fs).
const BACKEND_PORT = 5081;

// Spiegel von `Routing.parseUrl` (src/Client/Types.fs). Bewusst dupliziert:
// das Skript darf laut Task nur scripts/, package.json und das README
// ändern, ein Import aus dem Fable-Quellbaum ist hier nicht möglich. Wird
// eine Route in Types.fs ergänzt, muss sie hier nachgezogen werden;
// screenshot-route.test.mjs liest Types.fs tatsächlich und schlägt fehl,
// wenn diese Liste von dort abweicht.
export const KNOWN_ROUTES = new Set(["/", "/rules", "/settings", "/styleguide", "/quickadd", "/ledger"]);

function log(msg) {
  console.log(`[screenshot-route] ${msg}`);
}

/// Normalisiert eine Routen-Eingabe ('#/ledger', 'ledger', '#/', '/') auf ein
/// führendes '/', ohne '#'. Eine leere Route ist ein Fehler statt eines
/// stillen Fallbacks auf die Wurzel: wer nichts angibt, hat sich vertan.
export function normalizeRoute(route) {
  if (!route || typeof route !== "string" || route.trim() === "") {
    throw new Error("Route fehlt: node scripts/screenshot-route.mjs '#/ledger' [--out <pfad>]");
  }
  let r = route.trim();
  if (r.startsWith("#")) r = r.slice(1);
  if (!r.startsWith("/")) r = `/${r}`;
  return r;
}

/// Zerlegt eine normalisierte Route ("/ledger") in Segmente (["ledger"]);
/// die Wurzel ("/") liefert [].
export function routeSegments(normalizedRoute) {
  return normalizedRoute.split("/").filter((s) => s.length > 0);
}

/// Dateinamens-Bestandteil aus einer normalisierten Route. Die Wurzelroute
/// hat keinen Segmentnamen, deshalb der feste Name "start".
export function slugifyRoute(normalizedRoute) {
  const segments = routeSegments(normalizedRoute);
  return segments.length === 0 ? "start" : segments.join("-");
}

/// AC8: eine unbekannte Route scheitert laut, statt ein leeres/falsches Bild
/// abzulegen (s. Kommentar oben zum stillen SyncFlow-Fallback der App).
export function assertKnownRoute(normalizedRoute) {
  if (!KNOWN_ROUTES.has(normalizedRoute)) {
    const known = [...KNOWN_ROUTES].sort().join(", ");
    throw new Error(
      `Unbekannte Route '${normalizedRoute}': nicht in Routing.parseUrl ` +
        `(src/Client/Types.fs) enthalten, die App würde still auf SyncFlow ` +
        `zurückfallen. Bekannte Routen: ${known}.`
    );
  }
}

/// Zielpfade nach der Konvention aus docs/ux/kassenbuch/screenshots/:
/// handy-390x844-<name>.png und pc-1440x900-<name>.png.
export function screenshotFilenames(name, outDir) {
  return {
    mobile: path.join(outDir, `handy-390x844-${name}.png`),
    desktop: path.join(outDir, `pc-1440x900-${name}.png`),
  };
}

/// Parst die CLI-Argumente: erstes nicht-Flag-Token ist die Route,
/// `--out <pfad>` steuert das Zielverzeichnis (AC1, AC2). Ein `--out` ohne
/// folgenden Wert und jedes unbekannte Flag sind ein sofortiger, benannter
/// Fehler statt eines stillen Rateversuchs (früherer Bug: `--out` ohne Wert
/// nahm `undefined` und scheiterte erst später in path.resolve mit einer
/// nicht sprechenden TypeError).
export function parseArgs(argv) {
  let route;
  let outDir = DEFAULT_OUT_DIR;
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--out") {
      const value = argv[i + 1];
      if (value === undefined) {
        throw new Error("--out braucht einen Pfad: node scripts/screenshot-route.mjs '#/ledger' --out <pfad>");
      }
      outDir = value;
      i++;
    } else if (arg.startsWith("-")) {
      throw new Error(`Unbekanntes Flag '${arg}': dieses Skript kennt nur --out <pfad>.`);
    } else if (route === undefined) {
      route = arg;
    } else {
      throw new Error(`Unerwartetes Argument '${arg}': die Route ist bereits '${route}'.`);
    }
  }
  if (route === undefined) {
    throw new Error("Route fehlt: node scripts/screenshot-route.mjs '#/ledger' [--out <pfad>]");
  }
  return { route, outDir };
}

/// AC6: der Server darf niemals gegen das Original-DATA_DIR laufen. Reine,
/// synchrone Absicherung (per Pfadvergleich, aufgelöst), die auch greift,
/// wenn der Kandidat innerhalb des Original-Verzeichnisses liegt (nicht nur
/// bei exakter Gleichheit). Wird von startBackend() direkt mit dem Wert
/// aufgerufen, der tatsächlich an den Server-Prozess weitergegeben wird
/// (nicht mit einer separaten, immer-sicheren Kopie): nur so kann die
/// Zusicherung im echten Lauf jemals scheitern, statt eine Attrappe zu sein.
export function assertDataDirIsolated(candidateDataDir, sourceDataDir) {
  const resolvedCandidate = path.resolve(candidateDataDir);
  const resolvedSource = path.resolve(sourceDataDir);
  const relative = path.relative(resolvedSource, resolvedCandidate);
  const candidateIsSourceOrInsideIt = relative === "" || (!relative.startsWith("..") && !path.isAbsolute(relative));
  if (candidateIsSourceOrInsideIt) {
    throw new Error(
      `DATA_DIR (${resolvedCandidate}) zeigt auf das Original oder liegt darin ` +
        `(${resolvedSource}): das Skript startet den Server ausschließlich gegen ` +
        `eine Kopie in einem Temp-Verzeichnis, niemals gegen die echte ` +
        `Entwicklungsdatenbank. Abgebrochen, bevor der Server gestartet wurde.`
    );
  }
}

/// Quelle der Entwicklungsdatenbank: DATA_DIR-Env oder die Vorgabe aus
/// Persistence.fs (~/my_apps/budgetbuddy-dev). Gespiegelt, damit dieselbe
/// Datei kopiert wird, die der Server ohne dieses Skript lesen würde.
export function resolveSourceDataDir(env = process.env) {
  return env.DATA_DIR && env.DATA_DIR !== "" ? env.DATA_DIR : path.join(os.homedir(), "my_apps", "budgetbuddy-dev");
}

async function getFreePort() {
  return new Promise((resolve, reject) => {
    const srv = createNetServer();
    srv.unref();
    srv.on("error", reject);
    srv.listen(0, "127.0.0.1", () => {
      const { port } = srv.address();
      srv.close(() => resolve(port));
    });
  });
}

/// Wartet, bis ein TCP-Connect auf 127.0.0.1:port gelingt (echter
/// Bereitschafts-Check, keine feste Zeitspanne als Warte-Ersatz); scheitert
/// laut nach `timeoutMs`, statt so weiterzulaufen, als wäre `whatFor`
/// bereit.
async function waitForPort(port, timeoutMs, whatFor) {
  const deadline = Date.now() + timeoutMs;
  // eslint-disable-next-line no-constant-condition
  while (true) {
    const reachable = await new Promise((resolve) => {
      const socket = netConnect({ port, host: "127.0.0.1" });
      socket.once("connect", () => {
        socket.end();
        resolve(true);
      });
      socket.once("error", () => resolve(false));
    });
    if (reachable) return;
    if (Date.now() > deadline) {
      throw new Error(`${whatFor} antwortet nach ${timeoutMs}ms nicht auf Port ${port}.`);
    }
    await new Promise((r) => setTimeout(r, 200));
  }
}

async function isPortFree(port) {
  return new Promise((resolve) => {
    const srv = createNetServer();
    srv.once("error", () => resolve(false));
    srv.listen(port, "127.0.0.1", () => srv.close(() => resolve(true)));
  });
}

/// Startet den Giraffe-Server gegen `dataDir`. `sourceDataDir` ist ein
/// optionaler Test-Haken (Vorgabe: die echte Quelle aus
/// resolveSourceDataDir()); im echten Lauf ist es immer die tatsächliche
/// Quelle. assertDataDirIsolated() prüft hier, am Ort der Wahrheit, bevor
/// irgendein Prozess startet: genau `dataDir`, so wie es unten in
/// `env.DATA_DIR` landet (AC6).
///
/// USE_MEMORY_DB wird explizit auf "false" gesetzt, damit ein vom Aufrufer
/// vererbtes USE_MEMORY_DB=true (z. B. aus einer Testumgebung) nicht die
/// Kopie umgeht und eine leere In-Memory-DB rendert.
export async function startBackend(dataDir, sourceDataDir = resolveSourceDataDir()) {
  assertDataDirIsolated(dataDir, sourceDataDir);

  const portFree = await isPortFree(BACKEND_PORT);
  if (!portFree) {
    throw new Error(
      `Port ${BACKEND_PORT} ist belegt. Der Backend-Port ist in ` +
        `src/Server/Program.fs fest verdrahtet, das Skript kann keinen ` +
        `anderen wählen. Port freimachen und erneut versuchen.`
    );
  }
  const child = spawn("dotnet", ["run", "--project", "src/Server/Server.fsproj"], {
    cwd: ROOT,
    env: { ...process.env, DATA_DIR: dataDir, USE_MEMORY_DB: "false" },
    stdio: ["ignore", "pipe", "pipe"],
    detached: true,
  });
  let stderrOutput = "";
  child.stderr.on("data", (buf) => {
    stderrOutput += buf.toString();
  });
  child.on("exit", (code) => {
    if (code !== null && code !== 0) {
      log(`Server-Prozess beendete sich mit Code ${code}.\n${stderrOutput}`);
    }
  });
  await waitForPort(BACKEND_PORT, 30000, "Server").catch((err) => {
    throw new Error(`${err.message}\n${stderrOutput}`);
  });
  return child;
}

/// Startet den Vite-Dev-Server auf einem vorab reservierten freien Port
/// (der Backend-Port ist fest, s.o.; hier lohnt sich getFreePort wirklich).
/// Wartet auf einen echten TCP-Connect (waitForPort, dieselbe Technik wie
/// beim Backend), nicht auf ein unbedingtes setTimeout: der frühere Code
/// rannte einen 20-Sekunden-Timer per Promise.race gegen die Bereitschaft,
/// aber der Timer LÖSTE AUF statt abzulehnen, das .catch griff also nie.
/// Nach 20 Sekunden lief das Skript weiter, als wäre Vite bereit, selbst
/// wenn ein kalter Fable-Build (üblich rund 14 s, unter Last locker mehr)
/// noch nicht fertig war. waitForPort() dagegen WIRFT nach Ablauf (AC7).
/// Zusätzlich: läuft der vite-Prozess vorher mit einem Fehlercode ab (z. B.
/// weil der reservierte Port inzwischen doch belegt ist), rennt das gegen
/// dieselbe Wette und gewinnt, statt bis zum Timeout zu warten.
async function startFrontend(port) {
  const child = spawn(
    "npx",
    ["vite", "--port", String(port), "--strictPort", "--host", "127.0.0.1"],
    { cwd: ROOT, stdio: ["ignore", "pipe", "pipe"], detached: true }
  );

  let stderrOutput = "";
  child.stderr.on("data", (buf) => {
    stderrOutput += buf.toString();
  });

  const earlyExit = new Promise((_, reject) => {
    child.on("error", reject);
    child.on("exit", (code) => {
      if (code !== null && code !== 0) {
        reject(new Error(`vite beendete sich mit Code ${code}`));
      }
    });
  });

  try {
    // 60 s statt der 30 s beim Backend: ein kalter Fable-Build braucht
    // erfahrungsgemäß rund 14 s, kann unter Last aber deutlich länger
    // dauern; die 20 s des früheren, kaputten Timers waren schon knapp.
    await Promise.race([waitForPort(port, 60000, "Vite-Entwicklungsserver"), earlyExit]);
  } catch (err) {
    throw new Error(`vite konnte nicht starten: ${err.message}\n${stderrOutput}`);
  }

  return child;
}

/// Beendet `child` und dessen ganze Prozessgruppe (negative PID; child wird
/// oben `detached` gestartet, ist also Gruppen-Leader). Erst SIGTERM mit
/// begrenzter Gnadenfrist, dann SIGKILL, danach eine laute Prüfung, ob
/// `portToVerify` wirklich frei ist. Der frühere Code gab nach 5 s
/// stillschweigend auf, ohne SIGKILL-Eskalation und ohne diese Prüfung,
/// obwohl Server/Frontend als Enkelprozesse unter dotnet/npx laufen und ein
/// Listener auf dem Port so überleben konnte.
///
/// Die setTimeout-Aufrufe hier sind KEIN Warte-Ersatz für Bereitschaft
/// (das verbietet AC7): sie sind eine begrenzte Gnadenfrist vor der
/// Eskalation zu SIGKILL, und am Ende steht eine echte, laute Prüfung statt
/// eines stillen Weiterlaufens.
async function stopServer(child, portToVerify) {
  if (!child || child.killed || child.exitCode !== null) return;
  const pid = child.pid;
  const exited = new Promise((resolve) => child.once("exit", resolve));

  const tryKill = (signal) => {
    try {
      process.kill(-pid, signal);
    } catch {
      // Prozess(gruppe) bereits weg, nichts zu tun.
    }
  };

  tryKill("SIGTERM");
  const exitedGracefully = await Promise.race([
    exited.then(() => true),
    new Promise((resolve) => setTimeout(() => resolve(false), 5000)),
  ]);

  if (!exitedGracefully) {
    log(`PID ${pid} antwortet nach 5 s nicht auf SIGTERM, eskaliere zu SIGKILL.`);
    tryKill("SIGKILL");
    await Promise.race([exited, new Promise((resolve) => setTimeout(resolve, 3000))]);
  }

  if (portToVerify !== undefined) {
    const free = await isPortFree(portToVerify);
    if (!free) {
      throw new Error(
        `Port ${portToVerify} ist nach dem Beenden von PID ${pid} weiterhin belegt. ` +
          `Aufräumen unvollständig, ein Kindprozess läuft vermutlich noch.`
      );
    }
  }
}

/// Wartet auf einen echten Ladezustand statt auf eine feste Zeitspanne:
/// Netzwerk-Ruhe (page.goto waitUntil networkidle) plus das Verschwinden
/// jedes Loading-Spinners (Klasse "proto-spinner", src/Client/DesignSystem/
/// Loading.fs) UND jedes Skeleton-Platzhalters (Klasse "animate-pulse",
/// dieselbe Datei, Loading.txListSkeleton/tableSkeleton). Die Zielrouten
/// zeigen ihren Ladezustand überwiegend als Skeleton, nicht als Spinner
/// (z. B. src/Client/Components/Ledger/View.fs: Loading.txListSkeleton /
/// Loading.tableSkeleton für den Fall RemoteData.Loading). Nur
/// Loading.centered nutzt proto-spinner. Ohne die zweite Bedingung konnte
/// dieser Wartepunkt zurückkehren, während die Tabelle noch
/// Platzhalter-Balken zeigte.
/// AC7 (kein unbedingtes setTimeout als Warte-Ersatz) und die "nicht
/// ladende Route"-Hälfte von AC8: läuft dieser Wartepunkt in den Timeout
/// (page.setDefaultTimeout, s. captureScreenshot), scheitert der Lauf laut
/// mit der Route im Fehlertext.
async function waitForPageLoaded(page, normalizedRoute) {
  try {
    await page.waitForFunction(
      () =>
        document.querySelectorAll(".proto-spinner").length === 0 &&
        document.querySelectorAll(".animate-pulse").length === 0
    );
  } catch (err) {
    throw new Error(
      `Route '${normalizedRoute}' lädt nicht (Spinner oder Skeleton-Platzhalter hängen fest): ${err.message}`
    );
  }
}

async function captureScreenshot(browser, baseUrl, normalizedRoute, viewport, outPath) {
  const context = await browser.newContext({ viewport });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  try {
    await page.goto(`${baseUrl}/#${normalizedRoute}`, { waitUntil: "networkidle" });
    await waitForPageLoaded(page, normalizedRoute);
    await page.screenshot({ path: outPath, fullPage: true });
    log(`${path.relative(ROOT, outPath)} geschrieben.`);
  } catch (err) {
    throw new Error(`Aufnahme für Route '${normalizedRoute}' fehlgeschlagen: ${err.message}`);
  } finally {
    await context.close();
  }
}

async function main() {
  const { route, outDir: outDirArg } = parseArgs(process.argv.slice(2));
  const normalizedRoute = normalizeRoute(route);
  assertKnownRoute(normalizedRoute);
  const name = slugifyRoute(normalizedRoute);
  const outDir = path.resolve(ROOT, outDirArg);
  await mkdir(outDir, { recursive: true });
  const { mobile, desktop } = screenshotFilenames(name, outDir);

  // Zustand, den cleanup() aufräumen muss. Bewusst VOR dem try-Block und VOR
  // dem Anlegen des Temp-Verzeichnisses deklariert und die Signal-Handler
  // registriert: wirft copyFile() weiter unten, oder kommt SIGINT während
  // der Kopie, muss cleanup() trotzdem wissen, was schon existiert. Der
  // frühere Code registrierte Handler erst nach mkdtemp()/copyFile(). Ein
  // Abbruch genau in diesem Fenster ließ das Temp-Verzeichnis samt ~20 MB
  // DB-Kopie liegen.
  let tempDataDir;
  let backend;
  let frontend;
  let frontendPort;
  let cleaningUp = false;

  const cleanup = async () => {
    await stopServer(frontend, frontendPort);
    await stopServer(backend, BACKEND_PORT);
    if (tempDataDir) {
      await rm(tempDataDir, { recursive: true, force: true });
    }
  };
  const onSignal = async (signal) => {
    if (cleaningUp) return;
    cleaningUp = true;
    log(`${signal} empfangen, räume auf ...`);
    await cleanup();
    process.exit(130);
  };
  process.on("SIGINT", () => onSignal("SIGINT"));
  process.on("SIGTERM", () => onSignal("SIGTERM"));

  try {
    const sourceDataDir = resolveSourceDataDir();
    const sourceDbPath = path.join(sourceDataDir, "budgetbuddy.db");
    await access(sourceDbPath).catch(() => {
      throw new Error(`Keine Entwicklungsdatenbank unter ${sourceDbPath} gefunden.`);
    });

    tempDataDir = await mkdtemp(path.join(os.tmpdir(), "bb-screenshot-route-"));
    await copyFile(sourceDbPath, path.join(tempDataDir, "budgetbuddy.db"));
    log(`Entwicklungsdatenbank nach ${tempDataDir} kopiert (nur lesend gegen das Original).`);

    log(`Server startet gegen ${tempDataDir} (Port ${BACKEND_PORT}) ...`);
    backend = await startBackend(tempDataDir, sourceDataDir);

    frontendPort = await getFreePort();
    log(`Frontend startet auf Port ${frontendPort} ...`);
    frontend = await startFrontend(frontendPort);

    const browser = await chromium.launch();
    try {
      const baseUrl = `http://127.0.0.1:${frontendPort}`;
      await captureScreenshot(browser, baseUrl, normalizedRoute, { width: 390, height: 844 }, mobile);
      await captureScreenshot(browser, baseUrl, normalizedRoute, { width: 1440, height: 900 }, desktop);
    } finally {
      await browser.close();
    }
  } finally {
    if (!cleaningUp) await cleanup();
  }

  log(`Fertig: ${mobile}, ${desktop}`);
}

const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  main().catch((err) => {
    console.error("[screenshot-route] FEHLER:", err.message);
    process.exitCode = 1;
  });
}
