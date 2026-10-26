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
// Wie scripts/export-design-system.mjs kommt dieses Skript für rein
// präsentationale Routen (aktuell nur /#/styleguide, `ROUTES_WITHOUT_BACKEND`
// unten, design-system-m4kp7) ohne Backend aus. Für jede andere Route bleibt
// das echte Backend nötig: sie laden Daten über den Vite-Proxy /api ->
// Backend (vite.config.js, Ziel folgt BACKEND_PORT). Muster für
// Aufbau/Aufräumen übernommen aus export-design-system.mjs (getFreePort,
// startPreviewServer/stopServer, der finally-Block), hier auf Server +
// Frontend verdoppelt.
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
//     schrieb). Gilt nur für datenbedürftige Routen: eine präsentationale
//     Route bekommt gar keine Kopie und gar kein Backend (design-system-m4kp7).
//
// Der Backend-Port ist seit infra-jh7g9 über ASPNETCORE_URLS konfigurierbar
// (src/Server/Program.fs), das Skript reserviert sich hier deshalb, genau wie
// beim Frontend, einen freien Port statt den festen Port 5081 zu belegen: ein
// laufender Entwicklungsserver auf 5081 blockiert den Lauf damit nicht mehr.
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

// Spiegel von `Routing.parseUrl` (src/Client/Types.fs). Bewusst dupliziert:
// das Skript darf laut Task nur scripts/, package.json und das README
// ändern, ein Import aus dem Fable-Quellbaum ist hier nicht möglich. Wird
// eine Route in Types.fs ergänzt, muss sie hier nachgezogen werden;
// screenshot-route.test.mjs liest Types.fs tatsächlich und schlägt fehl,
// wenn diese Liste von dort abweicht.
export const KNOWN_ROUTES = new Set(["/", "/rules", "/settings", "/styleguide", "/quickadd", "/ledger", "/budget"]);

// Routen, die rein präsentational sind und deshalb ohne Backend und ohne
// Datenbankkopie aufgenommen werden können (design-system-m4kp7). Alles, was
// hier NICHT drinsteht, gilt als datenbedürftig — auch eine künftige Route,
// die noch kein Kennzeichen bekommen hat (sichere Voreinstellung, AC4).
// Bewusst eine eigene, kleine Liste statt eines Felds direkt in KNOWN_ROUTES:
// KNOWN_ROUTES bleibt dadurch unverändert ein Set<string>, gegen das der
// bestehende Abgleichs-Test (oben) unverändert weiterprüfen kann.
// /styleguide ist die einzige Route hier, aus demselben Grund, aus dem
// export-design-system.mjs seit Monaten ohne Server auskommt (Kommentar am
// Dateianfang): die Route lädt keine Daten über den Vite-Proxy /api.
export const ROUTES_WITHOUT_BACKEND = new Set(["/styleguide"]);

/// AC4: eine Route ohne Kennzeichen in ROUTES_WITHOUT_BACKEND gilt als
/// datenbedürftig, nicht als präsentational — die sichere Annahme bleibt die
/// Voreinstellung.
export function routeNeedsBackend(normalizedRoute) {
  return !ROUTES_WITHOUT_BACKEND.has(normalizedRoute);
}

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

// Exportiert (ledger-qns4j, `scripts/ledger-status-column.test.mjs`): dieselbe
// Port-/Prozess-Verwaltung wird dort wiederverwendet, um den echten
// Vite-Entwicklungsserver zu starten (echtes Tailwind-CSS, echtes Fable-Bundle),
// ohne ein zweites Mal ~150 Zeilen Prozess-Handling nachzubauen. Reines
// Sichtbarkeits-`export`, kein Verhaltensunterschied für dieses Skript selbst.
export async function getFreePort() {
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

/// Startet den Giraffe-Server gegen `dataDir` auf `port`. `sourceDataDir` ist
/// ein optionaler Test-Haken (Vorgabe: die echte Quelle aus
/// resolveSourceDataDir()); im echten Lauf ist es immer die tatsächliche
/// Quelle. assertDataDirIsolated() prüft hier, am Ort der Wahrheit, bevor
/// irgendein Prozess startet: genau `dataDir`, so wie es unten in
/// `env.DATA_DIR` landet (AC6).
///
/// `port` geht als ASPNETCORE_URLS an den Server-Prozess (infra-jh7g9:
/// Program.fs respektiert diese Variable seither über app.Run() ohne
/// Argument). Kein isPortFree-Vorabcheck mehr nötig: `port` kommt vom
/// Aufrufer aus getFreePort(), ein laufender Entwicklungsserver auf 5081
/// blockiert diesen Lauf damit nicht.
///
/// USE_MEMORY_DB wird explizit auf "false" gesetzt, damit ein vom Aufrufer
/// vererbtes USE_MEMORY_DB=true (z. B. aus einer Testumgebung) nicht die
/// Kopie umgeht und eine leere In-Memory-DB rendert.
export async function startBackend(dataDir, port, sourceDataDir = resolveSourceDataDir()) {
  assertDataDirIsolated(dataDir, sourceDataDir);

  const child = spawn("dotnet", ["run", "--project", "src/Server/Server.fsproj"], {
    cwd: ROOT,
    env: {
      ...process.env,
      DATA_DIR: dataDir,
      USE_MEMORY_DB: "false",
      ASPNETCORE_URLS: `http://0.0.0.0:${port}`,
    },
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
  await waitForPort(port, 30000, "Server").catch((err) => {
    throw new Error(`${err.message}\n${stderrOutput}`);
  });
  return child;
}

/// Startet den Vite-Dev-Server auf einem vorab reservierten freien Port.
/// `backendPort` geht als BACKEND_PORT an den Vite-Prozess, damit
/// vite.config.js sein Proxy-Ziel auf denselben Backend-Port richtet, den
/// startBackend() oben tatsächlich gebunden hat (sonst redet das Frontend mit
/// dem falschen Port). Ist `backendPort` undefined (präsentationale Route,
/// main() hat startBackend() übersprungen), bleibt BACKEND_PORT unangetastet;
/// vite.config.js fällt dann auf seine eigene Vorgabe zurück, die ohnehin nie
/// kontaktiert wird. Wartet auf einen echten TCP-Connect (waitForPort, dieselbe Technik wie
/// beim Backend), nicht auf ein unbedingtes setTimeout: der frühere Code
/// rannte einen 20-Sekunden-Timer per Promise.race gegen die Bereitschaft,
/// aber der Timer LÖSTE AUF statt abzulehnen, das .catch griff also nie.
/// Nach 20 Sekunden lief das Skript weiter, als wäre Vite bereit, selbst
/// wenn ein kalter Fable-Build (üblich rund 14 s, unter Last locker mehr)
/// noch nicht fertig war. waitForPort() dagegen WIRFT nach Ablauf (AC7).
/// Zusätzlich: läuft der vite-Prozess vorher mit einem Fehlercode ab (z. B.
/// weil der reservierte Port inzwischen doch belegt ist), rennt das gegen
/// dieselbe Wette und gewinnt, statt bis zum Timeout zu warten.
export async function startFrontend(port, backendPort) {
  const child = spawn(
    "npx",
    ["vite", "--port", String(port), "--strictPort", "--host", "127.0.0.1"],
    {
      cwd: ROOT,
      env: {
        ...process.env,
        ...(backendPort !== undefined ? { BACKEND_PORT: String(backendPort) } : {}),
      },
      stdio: ["ignore", "pipe", "pipe"],
      detached: true,
    }
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
export async function stopServer(child, portToVerify) {
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

/// Wartet auf ein Bereitschaftssignal aus der App selbst statt auf eine feste
/// Zeitspanne ODER auf das Verschwinden von CSS-Klassen (design-system-yxrxb).
///
/// Die frühere Fassung wartete auf das Verschwinden von ".proto-spinner" UND
/// ".animate-pulse" (Loading-Spinner- bzw. Skeleton-Klassen aus
/// src/Client/DesignSystem/Loading.fs). Das scheiterte laut an der
/// /styleguide-Route: deren Loading-Sektion zeigt Spinner und Skeletons als
/// DEMO-Inhalte der lebenden Komponenten-Galerie, die naturgemäß nie
/// verschwinden. Eine Klassen-Heuristik kann nicht unterscheiden zwischen
/// "die Seite lädt noch" und "die Seite zeigt absichtlich einen Ladezustand".
///
/// Roman hat entschieden: die App sagt selbst, wann sie fertig ist. `State.fs`
/// (`isPageReady`) trägt den Ladezustand als reine Funktion des Elmish-Modells
/// (RemoteData je Datenstrang der aktuellen Seite, nicht bloß "gemountet"), und
/// `View.fs` rendert das Ergebnis als unsichtbares Element mit dem Attribut
/// `data-app-ready="true"|"false"`. Für /styleguide (kein eigener Ladezustand)
/// ist das Signal sofort "true"; für z. B. /ledger erst, wenn Konten,
/// Kontostände UND Buchungen fertig geladen sind.
///
/// AC7 (kein unbedingtes setTimeout als Warte-Ersatz) und die "nicht ladende
/// Route"-Hälfte von AC8 aus design-system-wa6wz bleiben erfüllt: läuft dieser
/// Wartepunkt in den Timeout (page.setDefaultTimeout, s. captureScreenshot),
/// scheitert der Lauf laut mit der Route im Fehlertext.
export async function waitForPageLoaded(page, normalizedRoute) {
  try {
    await page.waitForSelector('[data-app-ready="true"]', { state: "attached" });
  } catch (err) {
    throw new Error(
      `Route '${normalizedRoute}' lädt nicht (data-app-ready wird nicht "true"): ${err.message}`
    );
  }
}

/// AC2/AC3 (ledger-p9k3w): Zielviewport für die eigentliche Aufnahme. Wächst
/// auf die gemessene Dokumenthöhe, schrumpft aber nie unter die angeforderte
/// Höhe — eine Route ohne scrollbaren Inhalt behält so ihre bisherigen
/// Abmessungen (AC3), eine längere Route bekommt genau ihre Inhaltshöhe
/// (AC2). Reine Funktion, absichtlich von captureScreenshot getrennt: ein
/// echter Playwright-Lauf ist hier nicht nötig, um diese Rechenregel zu
/// prüfen (s. screenshot-route.test.mjs).
export function computeCaptureViewport(baseViewport, documentHeight) {
  return { width: baseViewport.width, height: Math.max(baseViewport.height, documentHeight) };
}

/// Aufnahme mit `fullPage: true` nahm bisher Chromium intern zum Anlass, das
/// Layout-Viewport auf die volle Scroll-Höhe der Seite zu vergrößern, BEVOR
/// es den Screenshot schneidet. `.bottom-sheet` (src/Client/
/// DesignSystem/BottomSheet.fs, src/Client/styles.css:1556ff.) positioniert
/// sich im inaktiven Zustand über `top: calc(var(--vv-top, 0px) +
/// var(--vvh, 100dvh))`, also knapp unterhalb des sichtbaren Bereichs — aber
/// `100dvh` wird bei dieser internen Vergrößerung neu gegen die größere
/// effektive Höhe berechnet, und die eigentlich inaktive Leiste rutscht ins
/// Bild (ledger-p9k3w). Entschieden (Roman, 2026-09-19): der Viewport wird
/// stattdessen VOR der Aufnahme selbst auf die gemessene Inhaltshöhe gesetzt
/// (computeCaptureViewport oben) und ohne `fullPage` aufgenommen. Damit
/// entfällt Chromiums interne Vergrößerung, `100dvh` bleibt unverändert, und
/// die Leiste bleibt außerhalb des Bildes. Verworfen: die Leiste beim
/// Screenshot künstlich auszublenden (zeigt dann nicht mehr den echten
/// DOM-Zustand, für ein Abgleichs-Werkzeug die falsche Richtung) und die
/// App-CSS selbst anzufassen (rührt an die Visual-Viewport-Positionierung,
/// ADR 0005, und riskiert das echte Geräteverhalten). Die App bleibt
/// unangetastet, die Änderung ist auf dieses Skript begrenzt.
///
/// Als eigene, exportierte Funktion herausgezogen (design-system-q2bmv):
/// captureScreenshot bildete diese Mechanik bisher nur inline ab, wodurch die
/// Browser-Tests in screenshot-route.test.mjs sie ein zweites Mal nachbauen
/// mussten, statt die echte Funktion zu prüfen. Kehrte jemand hier zu
/// `fullPage: true` zurück, wird das jetzt an genau dieser Funktion
/// (captureAtDocumentHeight) rot, nicht an einem Nachbau.
export async function captureAtDocumentHeight(page, baseViewport, outPath) {
  const documentHeight = await page.evaluate(() => document.documentElement.scrollHeight);
  const captureViewport = computeCaptureViewport(baseViewport, documentHeight);
  await page.setViewportSize(captureViewport);
  await page.screenshot({ path: outPath, fullPage: false });
}

async function captureScreenshot(browser, baseUrl, normalizedRoute, viewport, outPath) {
  const context = await browser.newContext({ viewport });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  try {
    await page.goto(`${baseUrl}/#${normalizedRoute}`, { waitUntil: "networkidle" });
    await waitForPageLoaded(page, normalizedRoute);
    await captureAtDocumentHeight(page, viewport, outPath);
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
  const needsBackend = routeNeedsBackend(normalizedRoute);

  // Zustand, den cleanup() aufräumen muss. Bewusst VOR dem try-Block und VOR
  // dem Anlegen des Temp-Verzeichnisses deklariert und die Signal-Handler
  // registriert: wirft copyFile() weiter unten, oder kommt SIGINT während
  // der Kopie, muss cleanup() trotzdem wissen, was schon existiert. Der
  // frühere Code registrierte Handler erst nach mkdtemp()/copyFile(). Ein
  // Abbruch genau in diesem Fenster ließ das Temp-Verzeichnis samt ~20 MB
  // DB-Kopie liegen.
  let tempDataDir;
  let backend;
  let backendPort;
  let frontend;
  let frontendPort;
  let cleaningUp = false;

  const cleanup = async () => {
    await stopServer(frontend, frontendPort);
    await stopServer(backend, backendPort);
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
    if (needsBackend) {
      const sourceDataDir = resolveSourceDataDir();
      const sourceDbPath = path.join(sourceDataDir, "budgetbuddy.db");
      await access(sourceDbPath).catch(() => {
        throw new Error(`Keine Entwicklungsdatenbank unter ${sourceDbPath} gefunden.`);
      });

      tempDataDir = await mkdtemp(path.join(os.tmpdir(), "bb-screenshot-route-"));
      await copyFile(sourceDbPath, path.join(tempDataDir, "budgetbuddy.db"));
      log(`Entwicklungsdatenbank nach ${tempDataDir} kopiert (nur lesend gegen das Original).`);

      backendPort = await getFreePort();
      log(`Server startet gegen ${tempDataDir} (Port ${backendPort}) ...`);
      backend = await startBackend(tempDataDir, backendPort, sourceDataDir);
    } else {
      log(`Route '${normalizedRoute}' ist präsentational: kein Backend, keine Datenbankkopie.`);
    }

    frontendPort = await getFreePort();
    log(`Frontend startet auf Port ${frontendPort} ...`);
    frontend = await startFrontend(frontendPort, backendPort);

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
