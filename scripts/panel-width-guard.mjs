// scripts/panel-width-guard.mjs
//
// Wächter gegen feste Panelbreiten (design-system-gcpws, Styleguide §7.6): die Breite des Panels neben einer Liste
// kommt aus einer reinen Funktion (`Tokens.PanelWidths.panelWidthFor`, CSS-Variable `--panel-w`). Eine Klasse
// `w-[NNNpx]`, `pr-[NNNpx]` oder `md:`/`lg:pr-[NNNpx]` mit 300 bis 600 px in `src/Client/` ist eine feste Panelbreite
// oder ein fester Panel-Abstand, wie es sie vor diesem Schnitt gab (`lg:pr-[444px]`, `md:pr-[420px]`, `w-[388px]`).
// `max-w-[…]` und `min-w-[…]` zählen nicht: sie begrenzen, sie setzen keine Spalte.
//
// Ausnahmen stehen mit Grund in `EXCEPTIONS` (Datei und Klasse). Gedacht für Popover- und Navigationsbreiten; eine
// Ausnahme ohne Treffer ist veraltet und macht den Wächter ebenfalls rot.
//
// Ausführen: npm run test:scripts -- scripts/panel-width-guard.test.mjs

import fs from "node:fs";
import path from "node:path";

/// Kleinste und größte Breite, die als Panelbreite gilt (das Panel liegt zwischen 380 und 520 px).
export const MIN_PX = 300;
export const MAX_PX = 600;

/// Erlaubte Treffer: `file` relativ zu `src/Client/`, `cls` die ganze Klasse, `reason` warum es keine Panelbreite ist.
export const EXCEPTIONS = [
  {
    file: "Components/Styleguide/View.fs",
    cls: "w-[340px]",
    reason: "Vorschau im Styleguide: zeigt die sieben Wochentag-Segmente in einer schmalen Spalte, keine Spalte der App",
  },
];

// Eine Klasse beginnt am Anfang, nach Leerzeichen, Anführungszeichen, `{`, `(` oder `$"`; davor stehen höchstens
// Varianten wie `md:` oder `lg:`. So zählt `max-w-[400px]` nicht als `w-[400px]`.
const CLASS = /(?<=^|[\s"'`{(])((?:[a-z0-9-]+:)*)(w|pr)-\[(\d+)px\]/g;

/// Die festen Panelbreiten in einem Text: je Treffer Zeile und Klasse.
export function findPanelWidths(text) {
  const found = [];
  text.split("\n").forEach((line, i) => {
    for (const m of line.matchAll(CLASS)) {
      const px = Number(m[3]);
      if (px >= MIN_PX && px <= MAX_PX) found.push({ line: i + 1, cls: m[0] });
    }
  });
  return found;
}

function* sourceFiles(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === "node_modules" || entry.name === "bin" || entry.name === "obj" || entry.name.startsWith(".")) continue;
      yield* sourceFiles(full);
    } else if (/\.(fs|css|html)$/.test(entry.name)) {
      yield full;
    }
  }
}

/// Alle Treffer unter `clientDir` ohne die Ausnahmen, dazu die Ausnahmen ohne Treffer (veraltet).
export function scanClient(clientDir, exceptions = EXCEPTIONS) {
  const violations = [];
  const used = new Set();
  for (const full of sourceFiles(clientDir)) {
    const file = path.relative(clientDir, full).split(path.sep).join("/");
    for (const hit of findPanelWidths(fs.readFileSync(full, "utf8"))) {
      const ex = exceptions.findIndex((e) => e.file === file && e.cls === hit.cls);
      if (ex >= 0) used.add(ex);
      else violations.push(`${file}:${hit.line}: ${hit.cls}`);
    }
  }
  const stale = exceptions.filter((_, i) => !used.has(i)).map((e) => `${e.file}: ${e.cls} (Ausnahme ohne Treffer)`);
  return { violations, stale };
}
