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
const OUT_DIR = path.join(ROOT, "dist", "design-system");
const PREVIEW_DIR = path.join(OUT_DIR, "preview");
const ASSETS_DIR = path.join(OUT_DIR, "assets");

// Sektionen, die per `[data-ds-card="…"]` direkt im DOM-Baum der Route stehen.
const COMPONENT_CARDS = [
  { id: "components-card", title: "Card" },
  { id: "components-money", title: "Money" },
  { id: "components-badge", title: "Badge" },
  { id: "components-table", title: "Table" },
  { id: "components-navigation", title: "Navigation" },
];
// BottomSheet portalt nach document.body (BottomSheet.fs: renderToBody) und braucht
// daher eine eigene Behandlung (Trigger klicken, Overlay+Sheet separat einsammeln).
const BOTTOMSHEET_CARD = { id: "components-bottomsheet", title: "BottomSheet" };

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
  await copyFile(path.join(assetsDir, largest), path.join(OUT_DIR, "styles.css"));
  log(`styles.css kopiert (aus ${largest}).`);
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

async function writePreview(id, group, title, innerHtml) {
  const doc = `<!-- @dsCard group="${group}" -->
<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>BudgetBuddy Design System: ${title}</title>
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

async function extractPreviews(baseUrl) {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    page.setDefaultTimeout(30000);
    await page.goto(`${baseUrl}/#/styleguide`, { waitUntil: "networkidle" });
    // Letzte statisch eingehängte Sektion als Bereitschafts-Signal (Fable-Hydration
    // abgeschlossen, alle data-ds-card-Marker im DOM).
    await page.waitForSelector('[data-ds-card="components-navigation"]');

    for (const card of COMPONENT_CARDS) {
      const html = await page
        .locator(`[data-ds-card="${card.id}"]`)
        .evaluate((el) => el.outerHTML);
      await writePreview(card.id, "Components", card.title, html);
    }

    // BottomSheet: BottomSheet.view rendert per createPortal nach document.body,
    // ist also kein Kind des data-ds-card-Wrappers in View.fs. Trigger (stabiler
    // data-ds-card-trigger-Selektor) klicken, dann Overlay + Sheet vom Body
    // einsammeln und in einen synthetischen data-ds-card-Wrapper hüllen.
    await page.click('[data-ds-card-trigger="components-bottomsheet"]');
    await page.waitForSelector(".bottom-sheet.active");
    const sheetHtml = await page.evaluate(() => {
      const wrap = document.createElement("div");
      wrap.setAttribute("data-ds-card", "components-bottomsheet");
      wrap.setAttribute("data-ds-group", "Components");
      const overlay = document.querySelector(".overlay.active");
      const sheet = document.querySelector(".bottom-sheet.active");
      if (overlay) wrap.appendChild(overlay.cloneNode(true));
      if (sheet) wrap.appendChild(sheet.cloneNode(true));
      return wrap.outerHTML;
    });
    await writePreview(
      BOTTOMSHEET_CARD.id,
      "Components",
      BOTTOMSHEET_CARD.title,
      sheetHtml
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
  await writePreview("color-neon", "Colors", "Neon-Palette", buildColorNeonHtml(tokens));
  await writePreview(
    "color-surfaces",
    "Colors",
    "Flächen, Ränder, Text",
    buildColorSurfacesHtml(tokens)
  );
  await writePreview("type-fonts", "Type", "Schriften", buildTypeFontsHtml(tokens));
  await writePreview("brand-icon", "Brand", "App-Icon", buildBrandIconHtml());
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
aufgelöst, keine Platzhalter. \`styles.css\` ist das gebaute App-CSS (Tailwind 4 +
DaisyUI), damit die Vorschauen exakt so aussehen wie in der App.
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
gerendert, nicht von Hand nachgebaut. Aktuell im Bundle: Card, Money, Badge, Table
(Listenzeile), BottomSheet (mobiles Picker-Muster), Navigation. Der Umfang ist bewusst
klein gehalten: Tokens plus die Komponenten, die das Kassenbuch-Mockup braucht; weitere
Komponenten kommen dazu, sobald ein Mockup sie braucht.

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

async function writeDocs() {
  await writeFile(path.join(OUT_DIR, "README.md"), README_MD, "utf8");
  await writeFile(path.join(OUT_DIR, "SKILL.md"), SKILL_MD, "utf8");
  log("README.md und SKILL.md geschrieben.");
}

async function main() {
  // AC1: Bundle wird von Grund auf neu erzeugt, alter Inhalt ersetzt.
  await rm(OUT_DIR, { recursive: true, force: true });
  await mkdir(PREVIEW_DIR, { recursive: true });
  await mkdir(ASSETS_DIR, { recursive: true });

  const tokens = await buildColorsAndTypeCss();
  await writeDocs();
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

  await verifyFontsLoaded();

  log(`Fertig: ${OUT_DIR}`);
}

main().catch((err) => {
  console.error("[export-design-system] FEHLER:", err.message);
  process.exitCode = 1;
});
