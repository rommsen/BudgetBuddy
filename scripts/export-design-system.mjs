#!/usr/bin/env node
// scripts/export-design-system.mjs
//
// Wiederholbarer Export des Design-Systems als Claude-Design-Bundle
// (design-system-t82sg). Rendert die echte /#/styleguide-Route headless
// (Playwright) und schneidet die markierten Sektionen heraus (`data-ds-card` in
// src/Client/Components/Styleguide/View.fs): kein von Hand nachgebautes
// Komponenten-HTML. Tokens kommen aus src/Client/styles.css (@theme-Block +
// referenzierte Basis-Variablen); Token- und Type-Vorschauen werden aus denselben
// aufgelösten Werten generiert. Läuft ohne Backend (rein präsentationale Route).
//
// Nutzung: npm run export:design-system
//
// Ablauf:
//   1. `vite build` in ein temporäres Verzeichnis (liefert das gebaute App-CSS).
//   2. `vite preview` auf einem freien, vorab reservierten Port.
//   3. Playwright öffnet /#/styleguide, schneidet je `[data-ds-card]` das Markup
//      heraus und schreibt es als preview/components-*.html.
//   4. colors_and_type.css, styles.css, fonts/ (Font-Dateien + LICENSE.md),
//      preview/color-*.html, preview/type-*.html, preview/brand-*.html, assets/,
//      README.md, SKILL.md schreiben.
//   5. Headless prüfen, dass die Marken-Fonts in den Vorschauen tatsächlich laden
//      (document.fonts).
//   6. Preview-Server stoppen und temporäres Build-Verzeichnis löschen: immer,
//      auch bei Fehlern.

import { spawn } from "node:child_process";
import { createServer as createNetServer } from "node:net";
import {
  readFile,
  writeFile,
  rm,
  mkdir,
  mkdtemp,
  readdir,
  copyFile,
  stat,
} from "node:fs/promises";
import path from "node:path";
import os from "node:os";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");

/// Reine, exportierte Auflösung des Zielverzeichnisses (design-system-w4n9r):
/// `DS_EXPORT_OUT_DIR` (absoluter Pfad) überschreibt die Vorgabe
/// `<root>/dist/design-system`. Ohne Build/Playwright testbar; der Vollauf-Test
/// nutzt sie, um das Bundle in ein `mkdtemp`-Verzeichnis statt in den Repo-Baum
/// zu schreiben, ohne das Skript selbst zu verzweigen.
export function resolveOutDir(env, root) {
  const override = env.DS_EXPORT_OUT_DIR;
  if (override === undefined || override === "") {
    return path.join(root, "dist", "design-system");
  }
  if (!path.isAbsolute(override)) {
    throw new Error(
      `DS_EXPORT_OUT_DIR muss ein absoluter Pfad sein, erhalten: ${override}`
    );
  }
  return override;
}

const OUT_DIR = resolveOutDir(process.env, ROOT);
const PREVIEW_DIR = path.join(OUT_DIR, "preview");
const ASSETS_DIR = path.join(OUT_DIR, "assets");

// Sektionen, die per `[data-ds-card="…"]` direkt im DOM-Baum der Route stehen
// (design-system-y6gkd: alle Sektionen von /#/styleguide außer Colors/Type/Brand
// (aus Tokens generiert, s. writeTokenAndBrandPreviews) und Voice (reiner Text,
// steht in README/SKILL statt in einer Karte)).
export const COMPONENT_CARDS = [
  { id: "components-card", group: "Components" },
  { id: "components-money", group: "Components" },
  { id: "components-badge", group: "Components" },
  { id: "components-table", group: "Components" },
  { id: "components-navigation", group: "Components" },
  { id: "components-button", group: "Components" },
  { id: "components-stats", group: "Components" },
  { id: "components-icons", group: "Components" },
  { id: "components-pageheader", group: "Components" },
  { id: "components-primitives", group: "Components" },
  { id: "components-swipe", group: "Components" },
  { id: "components-chipbar", group: "Components" },
  { id: "components-filterpopover", group: "Components" },
  { id: "components-categorymultiselect", group: "Components" },
  { id: "components-loading", group: "Feedback" },
  { id: "components-errordisplay", group: "Feedback" },
  { id: "components-input", group: "Forms" },
  { id: "components-form", group: "Forms" },
  // Toast rendert (anders als BottomSheet/Modal) nicht per Portal, braucht aber
  // einen Klick, bevor ein Toast sichtbar ist ("Erfolg" per `data-ds-card-trigger`,
  // vor dieser Schleife ausgelöst, s. extractPreviews); danach wird die Karte wie
  // jede andere hier per `[data-ds-card="…"]` erfasst.
  { id: "components-toast", group: "Feedback" },
];
// BottomSheet, Kategorienauswahl und Modal portalen nach document.body
// (BottomSheet.fs/Modal.fs: renderToBody) und brauchen daher eine eigene Behandlung
// (Trigger klicken, geöffnetes Markup separat einsammeln, danach wieder schließen,
// damit der nächste Trigger nicht das falsche offene Overlay erfasst).
const BOTTOMSHEET_CARD = { id: "components-bottomsheet", group: "Components" };
const CATEGORY_PICKER_CARD = { id: "components-categorypicker", group: "Components" };
const MODAL_CARD = { id: "components-modal", group: "Components" };

/// Reine, exportierte Soll-Liste des Bundles relativ zur Bundle-Wurzel
/// (design-system-w4n9r): die sechs festen Dateien (inkl. `thumbnail.html`) plus genau ein
/// `preview/<id>.html` je übergebener Karte. Der Vollauf-Test bezieht seine
/// Erwartung aus dieser Funktion (mit `COMPONENT_CARDS`), statt die Liste ein
/// zweites Mal von Hand zu pflegen; ändert sich `COMPONENT_CARDS`, wandert der
/// Test mit. Bewusst außen vor: die aus Tokens generierten Karten
/// (`color-neon`, `color-surfaces`, `type-fonts`, `brand-icon`) sowie die
/// Portal-Karten (BottomSheet, Kategorienauswahl, Modal): die Soll-Liste
/// deckt nur die per `COMPONENT_CARDS` deklarierten Komponenten-Karten ab.
export function expectedBundleFiles(cards) {
  const fixed = [
    "colors_and_type.css",
    "styles.css",
    "README.md",
    "SKILL.md",
    "fonts/LICENSE.md",
    "thumbnail.html",
  ];
  const previews = cards.map((card) => `preview/${card.id}.html`);
  return [...fixed, ...previews];
}

/// Prüft, ob jede Datei aus `expectedBundleFiles(cards)` unter `outDir` existiert
/// und nicht leer ist (design-system-w4n9r, Verifier-Fund Iteration 1: das Skript
/// muss seine eigene Soll-Liste tatsächlich benutzen statt nur der Test). `statFn`
/// ist injizierbar (Vorgabe: `node:fs/promises`' `stat`), damit die Prüfung ohne
/// echten Export gegen ein präpariertes Verzeichnis getestet werden kann. Liefert
/// die fehlenden oder leeren Einträge relativ zu `outDir`, leeres Array bei
/// vollständigem Bundle.
export async function findMissingBundleFiles(outDir, cards, statFn = stat) {
  const missing = [];
  for (const rel of expectedBundleFiles(cards)) {
    try {
      const info = await statFn(path.join(outDir, rel));
      if (!info.isFile() || info.size === 0) missing.push(rel);
    } catch {
      missing.push(rel);
    }
  }
  return missing;
}

function log(msg) {
  console.log(`[export-design-system] ${msg}`);
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

function run(cmd, args, opts = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, { stdio: "inherit", ...opts });
    child.on("exit", (code) =>
      code === 0
        ? resolve()
        : reject(new Error(`${cmd} ${args.join(" ")} exited with ${code}`))
    );
    child.on("error", reject);
  });
}

/// Startet `vite preview` gegen einen bereits gebauten outDir auf einem vorab
/// reservierten (freien) Port. Löst auf, sobald der Server "Local:" loggt, spätestens
/// nach 5s (defensiv, falls sich das Log-Format ändert).
async function startPreviewServer(buildDir, port) {
  const child = spawn(
    "npx",
    [
      "vite",
      "preview",
      "--outDir",
      buildDir,
      "--port",
      String(port),
      "--strictPort",
      "--host",
      "127.0.0.1",
    ],
    { cwd: ROOT, stdio: ["ignore", "pipe", "pipe"] }
  );

  let resolveReady;
  const readyPromise = new Promise((resolve, reject) => {
    resolveReady = resolve;
    child.on("error", reject);
    child.on("exit", (code) => {
      if (code !== null && code !== 0) {
        reject(new Error(`vite preview beendete sich mit Code ${code}`));
      }
    });
  });
  let stderrOutput = "";
  child.stdout.on("data", (buf) => {
    if (buf.toString().includes("Local:")) resolveReady();
  });
  child.stderr.on("data", (buf) => {
    stderrOutput += buf.toString();
  });

  const timeout = new Promise((resolve) => setTimeout(resolve, 5000));
  await Promise.race([readyPromise, timeout]).catch((err) => {
    throw new Error(`vite preview konnte nicht starten: ${err.message}\n${stderrOutput}`);
  });

  return child;
}

async function stopServer(child) {
  if (!child || child.killed || child.exitCode !== null) return;
  child.kill();
  await new Promise((resolve) => {
    child.once("exit", resolve);
    setTimeout(resolve, 3000);
  });
}

/// Font-Dateinamen (unter src/Client/public/fonts/), die die App per @font-face in
/// styles.css einbindet (design-system-f7k2p/ADR 0026: selbst ausgeliefert, kein
/// Google-Fonts-Import mehr). Das Bundle kopiert dieselben Dateien nach
/// dist/design-system/fonts/ und bindet sie per @font-face in colors_and_type.css
/// ein, damit die Vorschauen ohne Netzzugriff laden.
const FONT_FILES = [
  "inter-latin-wght-normal.woff2",
  "jetbrains-mono-latin-wght-normal.woff2",
  "space-grotesk-latin-wght-normal.woff2",
  "LICENSE.md",
];

/// @font-face-Regeln für das Bundle: dieselben drei Familien/Gewichtsbereiche wie
/// in src/Client/styles.css, Pfad relativ zur Bundle-Wurzel (fonts/...), damit
/// colors_and_type.css unabhängig davon funktioniert, wo das Bundle abgelegt wird.
const FONT_FACE_CSS = `@font-face {
  font-family: 'Inter';
  font-style: normal;
  font-weight: 100 900;
  font-display: swap;
  src: url('fonts/inter-latin-wght-normal.woff2') format('woff2-variations');
}

@font-face {
  font-family: 'JetBrains Mono';
  font-style: normal;
  font-weight: 100 800;
  font-display: swap;
  src: url('fonts/jetbrains-mono-latin-wght-normal.woff2') format('woff2-variations');
}

@font-face {
  font-family: 'Space Grotesk';
  font-style: normal;
  font-weight: 300 700;
  font-display: swap;
  src: url('fonts/space-grotesk-latin-wght-normal.woff2') format('woff2-variations');
}`;

/// Kopiert die Font-Dateien plus Lizenz-Notiz nach dist/design-system/fonts/
/// (wie beim Vorbild-Projekt "Smolder Design System").
async function copyFontFiles() {
  const fontsSourceDir = path.join(ROOT, "src/Client/public/fonts");
  const fontsOutDir = path.join(OUT_DIR, "fonts");
  await mkdir(fontsOutDir, { recursive: true });
  for (const file of FONT_FILES) {
    await copyFile(path.join(fontsSourceDir, file), path.join(fontsOutDir, file));
  }
  log(`fonts/ kopiert (${FONT_FILES.length} Dateien, inkl. LICENSE.md).`);
}

/// Baut die aufgelösten Tokens aus dem @theme-Block von src/Client/styles.css plus
/// den Basis-Variablen, auf die er per var() verweist. Bricht ab, wenn eine
/// referenzierte Variable nicht im :root-Block gefunden wird (keine unaufgelösten
/// Variablen). Liefert die geparsten Tokens zurück, damit color-*/type-*-Karten aus
/// denselben Werten gebaut werden, die in colors_and_type.css landen (eine Quelle,
/// kein zweites Parsen mit Drift-Risiko).
async function buildColorsAndTypeCss() {
  const sourcePath = path.join(ROOT, "src/Client/styles.css");
  const raw = await readFile(sourcePath, "utf8");

  const themeMatch = raw.match(/@theme\s*\{[\s\S]*?\n\}/);
  if (!themeMatch) throw new Error(`Kein @theme-Block in ${sourcePath} gefunden.`);
  const themeBlock = themeMatch[0];

  const rootMatch = raw.match(/:root\s*\{[\s\S]*?\n\}/);
  if (!rootMatch) throw new Error(`Kein :root-Block in ${sourcePath} gefunden.`);
  const rootBlock = rootMatch[0];

  const varNames = [...themeBlock.matchAll(/var\(--([a-zA-Z0-9-]+)\)/g)].map(
    (m) => m[1]
  );
  // font-sans wird von @theme nicht per var() referenziert (--font-display/-mono
  // stehen dort als eigene Literalwerte), ist aber die Basis-Schrift für Fließtext
  // und wird für die Type-Vorschau gebraucht: deshalb zusätzlich aufgenommen.
  const uniqueNames = [...new Set([...varNames, "font-sans"])];

  function resolveVar(name) {
    const lineMatch = rootBlock.match(new RegExp(`^\\s*--${name}:\\s*([^;]+);`, "m"));
    if (!lineMatch) {
      throw new Error(
        `Basis-Variable --${name} fehlt im :root-Block von ${sourcePath}: Export abgebrochen.`
      );
    }
    return lineMatch[1].trim();
  }

  const resolvedLines = uniqueNames.map((name) => `  --${name}: ${resolveVar(name)};`);

  const output = `/* Generiert von scripts/export-design-system.mjs (design-system-t82sg). Nicht von
   Hand editieren. Quelle: src/Client/styles.css (@theme-Block + die Basis-Variablen,
   auf die er per var() verweist, aus :root, plus --font-sans). Re-Sync:
   npm run export:design-system. */

${themeBlock}

:root {
  /* Basis-Variablen, auf die der @theme-Block oben verweist (plus --font-sans, s.o.). */
${resolvedLines.join("\n")}
}

/* Fonts selbst ausgeliefert (design-system-f7k2p/ADR 0026), Dateien liegen unter
   fonts/ neben dieser CSS-Datei, Lizenz-Notiz dort in fonts/LICENSE.md. */
${FONT_FACE_CSS}
`;
  await writeFile(path.join(OUT_DIR, "colors_and_type.css"), output, "utf8");
  log(`colors_and_type.css geschrieben (${uniqueNames.length} Basis-Variablen aufgelöst).`);

  // Neon-Farben (Name + Hex) direkt aus dem @theme-Block parsen.
  const neonColors = [...themeBlock.matchAll(/--color-neon-([a-z]+):\s*(#[0-9a-fA-F]{3,8});/g)].map(
    ([, name, hex]) => ({ name, hex })
  );

  return {
    neonColors,
    backgroundVars: ["bg-app", "bg-card", "bg-elevated", "bg-input", "bg-hover"].map((n) => ({
      name: n,
      value: resolveVar(n),
    })),
    borderVars: ["border", "border-subtle"].map((n) => ({ name: n, value: resolveVar(n) })),
    textVars: ["text-primary", "text-secondary", "text-muted"].map((n) => ({
      name: n,
      value: resolveVar(n),
    })),
    fontSans: resolveVar("font-sans"),
    fontDisplay: themeBlock.match(/--font-display:\s*([^;]+);/)[1].trim(),
    fontMono: themeBlock.match(/--font-mono:\s*([^;]+);/)[1].trim(),
  };
}

async function buildApp() {
  const buildDir = await mkdtemp(path.join(os.tmpdir(), "bb-design-export-"));
  log(`vite build -> ${buildDir}`);
  await run("npx", ["vite", "build", "--outDir", buildDir, "--emptyOutDir"], {
    cwd: ROOT,
  });
  return buildDir;
}

/// Schreibt absolute Asset-Pfade (`url(/...)`) im kopierten App-CSS relativ zur
/// Bundle-Wurzel um (design-system-hadjy). Das gebaute styles.css bindet
/// @font-face mit `url(/fonts/...)` ein (absolut, passend für die App, die von
/// der Origin-Wurzel ausgeliefert wird). Im Claude-Design-Bundle liegen die
/// Previews aber unter `.../serve/preview/...`; die absolute Regel gewinnt dort
/// gegenüber der relativen aus FONT_FACE_CSS (`url('fonts/...')` in
/// colors_and_type.css), die Font-Anfrage geht an die Origin-Wurzel und liefert
/// 404. Schneidet daher den führenden "/" direkt nach "url(" (optional mit
/// Anführungszeichen) weg, außer bei protokoll-relativen URLs (`url(//...)`) -
/// generisch für das ganze Bundle, nicht nur für die drei Marken-Fonts, falls
/// das gebaute CSS künftig weitere root-relative Asset-Pfade referenziert.
export function rewriteAbsoluteAssetUrls(css) {
  return css.replace(/url\((['"]?)\/(?!\/)/g, "url($1");
}

async function copyBuiltCss(buildDir) {
  const assetsDir = path.join(buildDir, "assets");
  const files = await readdir(assetsDir);
  const cssFiles = files.filter((f) => f.endsWith(".css"));
  if (cssFiles.length === 0) {
    throw new Error(`Kein gebautes CSS in ${assetsDir} gefunden.`);
  }
  // Die größte Datei ist das konsolidierte App-CSS (Tailwind-Output); etwaige
  // kleinere Chunks gehören nicht zum globalen Stylesheet.
  const sized = await Promise.all(
    cssFiles.map(async (f) => ({ f, size: (await stat(path.join(assetsDir, f))).size }))
  );
  sized.sort((a, b) => b.size - a.size);
  const largest = sized[0].f;
  const rawCss = await readFile(path.join(assetsDir, largest), "utf8");
  const css = rewriteAbsoluteAssetUrls(rawCss);
  await writeFile(path.join(OUT_DIR, "styles.css"), css, "utf8");
  log(`styles.css kopiert und umgeschrieben (aus ${largest}, absolute url(/...)-Pfade relativ zur Bundle-Wurzel).`);
}

async function copyAssets() {
  const pairs = [
    ["src/Client/public/icon-master.svg", "icon-master.svg"],
    ["src/Client/public/favicon.svg", "favicon.svg"],
  ];
  for (const [from, to] of pairs) {
    await copyFile(path.join(ROOT, from), path.join(ASSETS_DIR, to));
  }
  log("assets/ kopiert (icon-master.svg, favicon.svg).");
}

/// Formatiert die erste Zeile jeder `preview/*.html`: den `@dsCard`-Marker mit
/// `group`, `name`, `subtitle` und (optional) `viewport` (design-system-an22r).
/// Die einzige Stelle, die das Marker-Format kennt: writePreview schreibt ihn ohne
/// `viewport`, measureAndPatchViewports ersetzt ihn später mit gemessenem Wert.
/// Doppelte Anführungszeichen und spitze Klammern werden maskiert, damit weder das
/// Attribut noch der HTML-Kommentar vorzeitig endet.
export function buildDsCardMarker({ id, group, name, subtitle, viewport }) {
  for (const [field, value] of [["name", name], ["subtitle", subtitle]]) {
    if (typeof value !== "string" || value.trim() === "") {
      throw new Error(
        `Karte "${id ?? "?"}": data-ds-card-${field} fehlt oder ist leer ` +
          `(in View.fs am data-ds-card/data-ds-card-trigger-Element setzen).`
      );
    }
  }
  const esc = (v) => String(v).replaceAll('"', "&quot;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");
  const vp = viewport ? ` viewport="${esc(viewport)}"` : "";
  return `<!-- @dsCard group="${esc(group)}" name="${esc(name)}" subtitle="${esc(subtitle)}"${vp} -->`;
}

/// Breite der Vorschau-Viewports in den `@dsCard`-Markern; die Höhe wird je Karte gemessen.
const PREVIEW_VIEWPORT_WIDTH = 880;

/// Marker-Daten je geschriebener Karte (id -> group/name/subtitle), damit der
/// Viewport-Nachtrag den Marker über buildDsCardMarker neu aufbauen kann.
const writtenCards = new Map();

async function writePreview(id, group, name, subtitle, innerHtml) {
  writtenCards.set(id, { group, name, subtitle });
  const doc = `${buildDsCardMarker({ id, group, name, subtitle })}
<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>BudgetBuddy Design System: ${name}</title>
<link rel="stylesheet" href="../colors_and_type.css">
<link rel="stylesheet" href="../styles.css">
</head>
<body class="bg-surface-app text-text-primary font-sans min-h-screen">
<div class="max-w-3xl mx-auto p-8">
${innerHtml}
</div>
</body>
</html>
`;
  await writeFile(path.join(PREVIEW_DIR, `${id}.html`), doc, "utf8");
  log(`preview/${id}.html geschrieben.`);
}

/// Misst je geschriebener Karte die Inhaltshöhe bei 880 px Breite und trägt sie als
/// `viewport="880x<Höhe>"` in Zeile 1 nach (design-system-an22r).
async function measureAndPatchViewports() {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage({ viewport: { width: PREVIEW_VIEWPORT_WIDTH, height: 1200 } });
    for (const [id, meta] of writtenCards) {
      const file = path.join(PREVIEW_DIR, `${id}.html`);
      await page.setViewportSize({ width: PREVIEW_VIEWPORT_WIDTH, height: 1200 });
      await page.goto(`file://${file}`);
      await page.evaluate(() => document.fonts.ready);
      // Fixed-Positionierte Inhalte (BottomSheet, Kategorienauswahl, Modal, Toast)
      // belegen im Dokumentfluss nichts, der Wrapper misst dort nur sein Padding.
      // Deshalb zusätzlich die Vereinigung der Rechtecke aller Nachfahren, die nicht
      // die ganze Viewport-Höhe füllen (Backdrop/Overlay ausgenommen), plus das
      // Wrapper-Padding von 2 x 32 px.
      const height = await page.evaluate(() => {
        const wrap = document.querySelector(".max-w-3xl");
        let h = wrap.getBoundingClientRect().height;
        let top = Infinity;
        let bottom = -Infinity;
        for (const el of wrap.querySelectorAll("*")) {
          const r = el.getBoundingClientRect();
          if (r.height <= 0 || r.height >= window.innerHeight) continue;
          top = Math.min(top, r.top);
          bottom = Math.max(bottom, r.bottom);
        }
        if (bottom > top) h = Math.max(h, bottom - top + 64);
        return Math.ceil(h);
      });
      const marker = buildDsCardMarker({ id, ...meta, viewport: `${PREVIEW_VIEWPORT_WIDTH}x${height}` });
      const html = await readFile(file, "utf8");
      const nl = html.indexOf("\n");
      await writeFile(file, marker + html.slice(nl), "utf8");
    }
    log(`Viewport-Höhen gemessen und nachgetragen (${writtenCards.size} Karten).`);
  } finally {
    await browser.close();
  }
}

/// Öffnet ein per Portal (createPortal nach document.body) gerendertes Sheet/Modal
/// über den stabilen `data-ds-card-trigger`-Selektor, wartet auf `activeSelector`,
/// sammelt die per `matchSelectors` gefundenen Elemente in einen synthetischen
/// `data-ds-card`-Wrapper und schreibt die Karte. Schließt danach über
/// `closeSelector` wieder, damit der nächste Trigger nicht das noch offene Overlay
/// dieser Karte erfasst (BottomSheet/Kategorienauswahl teilen sich die Klassen
/// `.overlay`/`.bottom-sheet`; Modal hat eine eigene Fixed-Wrapper-Klasse).
async function extractPortalCard(page, card, activeSelector, matchSelectors, closeSelector) {
  // name/subtitle stehen am Trigger (View.fs), der Inhalt entsteht erst zur Laufzeit.
  const trigger = page.locator(`[data-ds-card-trigger="${card.id}"]`);
  const name = await trigger.getAttribute("data-ds-card-name");
  const subtitle = await trigger.getAttribute("data-ds-card-subtitle");
  await page.click(`[data-ds-card-trigger="${card.id}"]`);
  await page.waitForSelector(activeSelector);
  const html = await page.evaluate(
    ({ cardId, group, selectors }) => {
      const wrap = document.createElement("div");
      wrap.setAttribute("data-ds-card", cardId);
      wrap.setAttribute("data-ds-group", group);
      for (const sel of selectors) {
        const el = document.querySelector(sel);
        if (el) wrap.appendChild(el.cloneNode(true));
      }
      return wrap.outerHTML;
    },
    { cardId: card.id, group: card.group, selectors: matchSelectors }
  );
  await writePreview(card.id, card.group, name, subtitle, html);
  await page.click(closeSelector);
  await page.waitForFunction(
    (sel) => !document.querySelector(sel),
    activeSelector
  );
}

async function extractPreviews(baseUrl) {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    page.setDefaultTimeout(30000);
    await page.goto(`${baseUrl}/#/styleguide`, { waitUntil: "networkidle" });
    // Letzte statisch eingehängte Sektion als Bereitschafts-Signal (Fable-Hydration
    // abgeschlossen, alle data-ds-card-Marker im DOM).
    await page.waitForSelector('[data-ds-card="components-navigation"]');

    // Toast rendert nicht per Portal, braucht aber einen Klick, bevor ein Toast im
    // data-ds-card-Container sichtbar ist (s. COMPONENT_CARDS oben).
    await page.click('[data-ds-card-trigger="components-toast"]');
    // Kein stabiler ".toast"-Klassenname (Toast.fs); der Dismiss-Button jedes
    // Toasts hat ein festes aria-label und dient als Bereitschafts-Signal.
    await page.waitForSelector('[data-ds-card="components-toast"] button[aria-label="Dismiss"]');

    for (const card of COMPONENT_CARDS) {
      const { html, name, subtitle } = await page
        .locator(`[data-ds-card="${card.id}"]`)
        .evaluate((el) => ({
          html: el.outerHTML,
          name: el.getAttribute("data-ds-card-name"),
          subtitle: el.getAttribute("data-ds-card-subtitle"),
        }));
      await writePreview(card.id, card.group, name, subtitle, html);
    }

    // BottomSheet, Kategorienauswahl: teilen sich `.overlay`/`.bottom-sheet`, daher
    // nacheinander öffnen/einsammeln/schließen (sonst erfasst querySelector das
    // falsche, noch offene Sheet).
    await extractPortalCard(
      page,
      BOTTOMSHEET_CARD,
      ".bottom-sheet.active",
      [".overlay.active", ".bottom-sheet.active"],
      ".bottom-sheet.active .sheet-close"
    );
    await extractPortalCard(
      page,
      CATEGORY_PICKER_CARD,
      ".bottom-sheet.active",
      [".overlay.active", ".bottom-sheet.active"],
      ".bottom-sheet.active .sheet-close"
    );

    // Modal: eigener Fixed-Wrapper (Modal.fs: `.fixed.inset-0.z-50.overflow-hidden`),
    // schließt über den "Abbrechen"-Button im Demo-Footer. Selektor auf den
    // Fixed-Wrapper eingeschränkt: der statische Button-Sektion hat weiter oben in
    // der Route einen eigenen (nicht-modalen) "Abbrechen"-Button.
    await extractPortalCard(
      page,
      MODAL_CARD,
      ".fixed.inset-0.z-50.overflow-hidden",
      [".fixed.inset-0.z-50.overflow-hidden"],
      '.fixed.inset-0.z-50.overflow-hidden button:has-text("Abbrechen")'
    );
  } finally {
    await browser.close();
  }
}

function swatch(bgClass, label, caption) {
  return `<div class="flex flex-col gap-1">
  <div class="${bgClass} h-16 w-full rounded-lg border border-border-subtle"></div>
  <span class="text-sm font-semibold text-text-primary">${label}</span>
  <span class="text-xs text-text-muted font-mono">${caption}</span>
</div>`;
}

/// Token-Karte "Colors/Neon": Name + Hex je Neon-Farbe, aus denselben Werten wie
/// colors_and_type.css (kein von Hand gepflegtes zweites Set).
function buildColorNeonHtml(tokens) {
  const cards = tokens.neonColors
    .map((c) => swatch(`bg-neon-${c.name}`, c.name[0].toUpperCase() + c.name.slice(1), c.hex))
    .join("\n");
  return `<div class="space-y-4">
  <h2 class="text-lg font-semibold text-neon-teal">Neon-Palette</h2>
  <div class="grid grid-cols-3 gap-4">
${cards}
  </div>
</div>`;
}

/// Token-Karte "Colors/Surfaces": Flächen, Ränder, Textfarben aus colors_and_type.css.
function buildColorSurfacesHtml(tokens) {
  const bg = tokens.backgroundVars
    .map((v) => swatch(`bg-surface-${v.name.replace("bg-", "")}`, v.name, v.value))
    .join("\n");
  // Tailwind-Klassenname je Basis-Variable: --border -> border-border-default,
  // --border-subtle -> border-border-subtle (Namensschema aus Tokens.fs/styles.css).
  const borderClassFor = { border: "border-border-default", "border-subtle": "border-border-subtle" };
  const border = tokens.borderVars
    .map(
      (v) =>
        `<div class="flex flex-col gap-1">
  <div class="bg-surface-card h-16 w-full rounded-lg border-2 ${borderClassFor[v.name]}"></div>
  <span class="text-sm font-semibold text-text-primary">${v.name}</span>
  <span class="text-xs text-text-muted font-mono">${v.value}</span>
</div>`
    )
    .join("\n");
  const text = tokens.textVars
    .map(
      (v) =>
        `<div class="flex flex-col gap-1">
  <div class="bg-surface-card h-16 w-full rounded-lg border border-border-subtle flex items-center justify-center">
    <span class="text-${v.name} text-lg font-semibold">Aa</span>
  </div>
  <span class="text-sm font-semibold text-text-primary">${v.name}</span>
  <span class="text-xs text-text-muted font-mono">${v.value}</span>
</div>`
    )
    .join("\n");
  return `<div class="space-y-6">
  <div class="space-y-4">
    <h2 class="text-lg font-semibold text-neon-teal">Flächen</h2>
    <div class="grid grid-cols-3 gap-4">
${bg}
    </div>
  </div>
  <div class="space-y-4">
    <h2 class="text-lg font-semibold text-neon-teal">Ränder</h2>
    <div class="grid grid-cols-3 gap-4">
${border}
    </div>
  </div>
  <div class="space-y-4">
    <h2 class="text-lg font-semibold text-neon-teal">Text</h2>
    <div class="grid grid-cols-3 gap-4">
${text}
    </div>
  </div>
</div>`;
}

/// Token-Karte "Type": die drei Marken-Fonts mit Proben, Werte aus colors_and_type.css.
function buildTypeFontsHtml(tokens) {
  return `<div class="space-y-6">
  <div class="space-y-1">
    <p class="font-sans text-lg text-text-primary">font-sans: Inter, UI-Fließtext, direkt und knapp.</p>
    <span class="text-xs text-text-muted font-mono">${tokens.fontSans}</span>
  </div>
  <div class="space-y-1">
    <p class="font-display text-lg text-text-primary">font-display: Space Grotesk, Hero/Header.</p>
    <span class="text-xs text-text-muted font-mono">${tokens.fontDisplay}</span>
  </div>
  <div class="space-y-1">
    <p class="font-mono text-lg text-text-primary">font-mono: JetBrains Mono, 1234,56 Euro.</p>
    <span class="text-xs text-text-muted font-mono">${tokens.fontMono}</span>
  </div>
</div>`;
}

function buildBrandIconHtml() {
  return `<div class="flex items-center gap-6">
  <img src="../assets/icon-master.svg" alt="BudgetBuddy App-Icon" class="w-24 h-24" />
  <div class="space-y-1">
    <p class="font-display text-lg font-bold text-text-primary">BudgetBuddy App-Icon</p>
    <p class="text-sm text-text-secondary">"B" im Sync-Ring, Signatur-Gradient (design-system-008).</p>
  </div>
</div>`;
}

async function writeTokenAndBrandPreviews(tokens) {
  await writePreview(
    "color-neon",
    "Colors",
    "Neon-Palette",
    "Die vier Neon-Akzentfarben mit Hex-Wert",
    buildColorNeonHtml(tokens)
  );
  await writePreview(
    "color-surfaces",
    "Colors",
    "Flächen, Ränder, Text",
    "App-, Karten- und Eingabe-Flächen, Rahmen und Textfarben",
    buildColorSurfacesHtml(tokens)
  );
  await writePreview(
    "type-fonts",
    "Type",
    "Schriften",
    "Inter, Space Grotesk und JetBrains Mono mit Proben",
    buildTypeFontsHtml(tokens)
  );
  await writePreview(
    "brand-icon",
    "Brand",
    "App-Icon",
    "Das B im Sync-Ring mit Signatur-Gradient",
    buildBrandIconHtml()
  );
}

/// Headless-Check: laden die drei Marken-Fonts in den Vorschauen tatsächlich?
/// (Ohne die @font-face-Regeln aus copyFontFiles/FONT_FACE_CSS in colors_and_type.css
/// würde document.fonts leer bleiben, design-system-f7k2p/ADR 0026.)
async function verifyFontsLoaded() {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.goto(`file://${path.join(PREVIEW_DIR, "type-fonts.html")}`);
    await page.evaluate(() => document.fonts.ready);
    const families = await page.evaluate(() => [...document.fonts].map((f) => f.family));
    const required = ["Inter", "Space Grotesk", "JetBrains Mono"];
    const missing = required.filter((name) => !families.some((f) => f.includes(name)));
    if (missing.length > 0) {
      throw new Error(
        `Fonts nicht geladen in preview/type-fonts.html: ${missing.join(", ")} fehlen in ` +
          `document.fonts (geladen: ${families.join(", ") || "keine"}).`
      );
    }
    log(`Fonts verifiziert (document.fonts): ${families.join(", ")}.`);
  } finally {
    await browser.close();
  }
}

const README_MD = `# BudgetBuddy: Design System

Neon-on-dark Design System für BudgetBuddy (YNAB-Companion: Comdirect zu YNAB Sync).
Dieses Projekt ist ein **abgeleitetes** Bundle, nur zum Lesen. Die Quelle der Wahrheit
bleibt das Repo (\`src/Client/styles.css\`, \`src/Client/DesignSystem/*.fs\`,
\`standards/frontend/styleguide.md\`); Änderungen entstehen dort und werden per
\`npm run export:design-system\` neu hierher exportiert. Mockups aus diesem Projekt
fließen nie zurück in den Code.

## Visuelle Sprache

Dunkler Grund (\`surface-app\`/\`surface-card\`/\`surface-elevated\`/\`surface-input\`,
gestaffelt nach Tiefe) mit Neon-Akzenten und dezentem Glow. Glow markiert das
Aktive/Wichtige (primäre CTAs, positive Beträge, fokussierte Karten): sparsam, nicht
dekorativ. Zahlen und Daten stehen in JetBrains Mono mit tabular-nums, Fließtext in
Inter, große Header optional in Space Grotesk.

## Farbsemantik

| Farbe | Bedeutung |
|---|---|
| Orange (\`--color-neon-orange\`) | primär / CTA / Action |
| Teal (\`--color-neon-teal\`) | sekundär / aktiv / Navigation / Info |
| Green (\`--color-neon-green\`) | Erfolg / positiv |
| Red (\`--color-neon-red\`) | Fehler / negativ |
| Purple (\`--color-neon-purple\`) | Akzent / Kategorie |
| Pink (\`--color-neon-pink\`) | Akzent / attention |

Geld: positiv immer Green (mit Glow), negativ immer Red. Primäre Aktion pro Screen:
höchstens eine, Orange. Sekundär/Abbrechen/Navigation: Teal. Löschen/Zerstörendes: Red.

## Tokens

\`colors_and_type.css\` enthält den vollständigen \`@theme\`-Block aus
\`src/Client/styles.css\` plus die Basis-Variablen, auf die er verweist: alle Werte
aufgelöst, keine Platzhalter. \`styles.css\` ist das gebaute App-CSS (Tailwind 4, kein
DaisyUI, ADR 0031), damit die Vorschauen exakt so aussehen wie in der App.
\`preview/color-neon.html\`, \`preview/color-surfaces.html\` und \`preview/type-fonts.html\`
zeigen dieselben Tokens als Karten, generiert aus denselben Werten.

## Fonts

Inter, Space Grotesk und JetBrains Mono liegen als eigene Dateien im Ordner
\`fonts/\` (Variable Fonts, Subset "latin", SIL Open Font License 1.1, Lizenz-Notiz
in \`fonts/LICENSE.md\`). \`colors_and_type.css\` bindet sie per \`@font-face\` ein;
die Vorschauen laden damit ohne Netzzugriff, genauso wie die BudgetBuddy-App selbst
(\`src/Client/styles.css\`, ADR 0026).

## Komponenten

\`preview/components-*.html\` sind aus der lebenden Galerie (\`/#/styleguide\` im Repo)
gerendert, nicht von Hand nachgebaut. Aktuell im Bundle (Gruppe "Components"): Card,
Money, Badge, Table (Listenzeile), Navigation, Button, Stats, Icons, PageHeader,
Primitives, Swipe, BottomSheet (mobiles Picker-Muster), Kategorienauswahl (Kategorie
ändern, derselbe Picker wie im Kassenbuch), Modal. Gruppe "Feedback": Loading,
ErrorDisplay, Toast. Gruppe "Forms": Input, Form. Nicht als eigene Karte: Typografie
und Colors sind schon in den "Colors"-/"Type"-Karten (aus denselben Tokens generiert,
siehe oben), Voice ist reiner Fließtext und steht nur hier im README/SKILL.

## Voice

Deutsch, direkt, knapp, ohne Füllwörter. Buttons als Verb oder Verb+Objekt
("Speichern", "Sync starten"). Fehlertexte benennen, was passiert ist. Beträge
sprechen für sich (Vorzeichen + Farbe). Empty States sagen, was als Nächstes zu tun ist.

## Vollständige Regeln

Dieses README ist eine Kurzfassung. Vollständig (inkl. ADR-Begründungen, Motion,
Muster wie visual-viewport-Sheets): \`standards/frontend/styleguide.md\` im Repo
(nicht Teil dieses Bundles).

## Re-Sync

\`npm run export:design-system\` im Repo ausführen, danach dieses Projekt in Claude
Design von Hand aktualisieren (Upload macht ein Mensch, kein automatischer
Schreibzugriff, s. design-system-h58y4).
`;

const SKILL_MD = `---
name: budgetbuddy-design-system
description: Mockups und UI-Exploration für BudgetBuddy im BudgetBuddy-Look (neon-on-dark) bauen.
user-invocable: true
---

Lies zuerst README.md in diesem Projekt.

## Kurzorientierung

- Marke: BudgetBuddy, YNAB-Companion (Comdirect zu YNAB Sync).
- Look: neon-on-dark. Dunkler Grund, Neon-Akzente (Orange/Teal/Green/Red/Purple/Pink),
  Glow als Hervorhebung (sparsam, nicht dekorativ).
- Farbsemantik: Orange = primär/CTA, Teal = sekundär/aktiv, Green = Erfolg,
  Red = Fehler, Purple/Pink = Akzent.
- Schriften: Inter (Fließtext), Space Grotesk (Header, optional), JetBrains Mono
  (Zahlen/Beträge, tabular-nums).
- Tokens liegen in \`colors_and_type.css\` (auch als Karten: \`preview/color-neon.html\`,
  \`preview/color-surfaces.html\`, \`preview/type-fonts.html\`); Komponenten-Vorschauen in
  \`preview/components-*.html\`.
- Signatur-Muster: Beträge immer mit Vorzeichen-Farbe (positiv grün, negativ rot);
  primäre Aktion pro Screen höchstens eine (Orange); \`Card\` für Inhaltsblöcke,
  BottomSheet-Muster für mobile Auswahl-Flows statt Freitext.

## Typische Aufgaben

- Neues Mockup für einen BudgetBuddy-Screen: Tokens aus \`colors_and_type.css\`
  verwenden, \`preview/\` als Vorbild für Karten, Beträge, Badges, Tabellenzeilen,
  mobile Picker, Navigation.
- Farbentscheidung: Farbsemantik-Tabelle im README nachschlagen, nicht raten.

## What to avoid

- Kein Inline-Hex, keine Standard-Farben (z. B. \`red-500\`): nur die Tokens aus
  \`colors_and_type.css\`.
- Keine neuen Neon-Farben erfinden: die sechs (Orange/Teal/Green/Red/Purple/Pink)
  sind fest mit Bedeutung belegt.
- Keine Höflichkeitsfloskeln in Microcopy ("Bitte klicken Sie ..."): direkt, knapp,
  Deutsch.
- Änderungen nie hier vornehmen und erwarten, dass sie zurück ins Repo fließen: dieses
  Projekt ist eine abgeleitete Kopie, nur lesend.
`;

/// `thumbnail.html` an der Bundle-Wurzel (design-system-an22r): Wortmarke in
/// Space Grotesk auf App-Schwarz, rechts ein Streifen aus den vier Neon-Farben.
/// Farben kommen aus denselben Tokens wie colors_and_type.css.
export function buildThumbnailHtml(tokens) {
  const bg = tokens.backgroundVars.find((v) => v.name === "bg-app").value;
  const order = ["green", "teal", "orange", "purple"];
  const blocks = order
    .map((n) => {
      const c = tokens.neonColors.find((x) => x.name === n);
      if (!c) throw new Error(`Neon-Farbe "${n}" fehlt in den Tokens (thumbnail.html).`);
      return `    <div class="block" style="background: ${c.hex}"></div>`;
    })
    .join("\n");
  return `<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<title>BudgetBuddy Design System</title>
<style>
${FONT_FACE_CSS}
:root { --bg-app: ${bg}; }
html, body { margin: 0; height: 100%; }
body { background: var(--bg-app); display: flex; align-items: center; justify-content: space-between; padding: 0 64px; box-sizing: border-box; }
.wordmark { font-family: 'Space Grotesk', sans-serif; font-weight: 700; font-size: 72px; color: #fff; letter-spacing: -0.02em; }
.stripe { display: flex; gap: 12px; }
.block { width: 48px; height: 120px; border-radius: 8px; }
</style>
</head>
<body>
  <span class="wordmark">BudgetBuddy</span>
  <div class="stripe">
${blocks}
  </div>
</body>
</html>
`;
}

async function writeDocs(tokens) {
  await writeFile(path.join(OUT_DIR, "README.md"), README_MD, "utf8");
  await writeFile(path.join(OUT_DIR, "SKILL.md"), SKILL_MD, "utf8");
  await writeFile(path.join(OUT_DIR, "thumbnail.html"), buildThumbnailHtml(tokens), "utf8");
  log("README.md, SKILL.md und thumbnail.html geschrieben.");
}

async function main() {
  // AC1: Bundle wird von Grund auf neu erzeugt, alter Inhalt ersetzt.
  await rm(OUT_DIR, { recursive: true, force: true });
  await mkdir(PREVIEW_DIR, { recursive: true });
  await mkdir(ASSETS_DIR, { recursive: true });

  const tokens = await buildColorsAndTypeCss();
  await writeDocs(tokens);
  await copyAssets();
  await copyFontFiles();
  await writeTokenAndBrandPreviews(tokens);

  let buildDir;
  let server;
  try {
    buildDir = await buildApp();
    await copyBuiltCss(buildDir);

    const port = await getFreePort();
    log(`vite preview auf Port ${port} ...`);
    server = await startPreviewServer(buildDir, port);

    await extractPreviews(`http://127.0.0.1:${port}`);
  } finally {
    await stopServer(server);
    if (buildDir) await rm(buildDir, { recursive: true, force: true });
  }

  await measureAndPatchViewports();
  await verifyFontsLoaded();

  // AC1: das Skript benutzt seine eigene Soll-Liste (expectedBundleFiles) auch
  // selbst, statt sie nur dem Test zu überlassen (Verifier-Fund Iteration 1).
  const missing = await findMissingBundleFiles(OUT_DIR, COMPONENT_CARDS);
  if (missing.length > 0) {
    throw new Error(
      `Export unvollständig, folgende erwartete Dateien fehlen oder sind leer unter ` +
        `${OUT_DIR}: ${missing.join(", ")}`
    );
  }

  log(`Fertig: ${OUT_DIR}`);
}

// Nur ausführen, wenn das Skript direkt gestartet wurde (npm run export:design-system),
// nicht beim Import als Modul (design-system-hadjy: der Regressionstest importiert
// rewriteAbsoluteAssetUrls aus diesem Skript per Node-Subprozess und darf dabei nicht
// den ganzen Export - vite build + Playwright - mit anstoßen).
const isMainModule = process.argv[1] && import.meta.url === `file://${path.resolve(process.argv[1])}`;
if (isMainModule) {
  main().catch((err) => {
    console.error("[export-design-system] FEHLER:", err.message);
    process.exitCode = 1;
  });
}
