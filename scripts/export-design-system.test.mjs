// scripts/export-design-system.test.mjs
//
// Regressionstest für den Design-System-Export (design-system-w4n9r): bislang
// nur manuell verifiziert (design-system-t82sg), ohne automatisierte
// Absicherung gegen einen vergessenen `data-ds-card`-Marker, eine vergessene
// Karte in COMPONENT_CARDS oder einen Bruch im Export-Skript selbst.
//
//   node --test scripts/export-design-system.test.mjs
//
// Muster (Playwright per dynamischem Import, benannter Skip ohne Browser)
// nach scripts/screenshot-route.test.mjs (design-system-q2bmv).

import { test } from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import os from "node:os";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { mkdtemp, rm, stat, readdir, readFile } from "node:fs/promises";
import {
  resolveOutDir,
  expectedBundleFiles,
  findMissingBundleFiles,
  buildDsCardMarker,
  buildThumbnailHtml,
  COMPONENT_CARDS,
} from "./export-design-system.mjs";
import { mkdir, writeFile } from "node:fs/promises";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const EXPORT_SCRIPT = path.join(__dirname, "export-design-system.mjs");
const REPO_DIST_DIR = path.join(ROOT, "dist", "design-system");

// --- Browserfreie Tests -----------------------------------------------

test("resolveOutDir: ohne DS_EXPORT_OUT_DIR bleibt es bei <root>/dist/design-system", () => {
  assert.equal(resolveOutDir({}, ROOT), path.join(ROOT, "dist", "design-system"));
});

test("resolveOutDir: mit DS_EXPORT_OUT_DIR gilt deren Wert", () => {
  const override = path.join(os.tmpdir(), "irgendein-export-ziel");
  assert.equal(resolveOutDir({ DS_EXPORT_OUT_DIR: override }, ROOT), override);
});

test("resolveOutDir: eine leere DS_EXPORT_OUT_DIR verhält sich wie keine gesetzte Variable", () => {
  assert.equal(
    resolveOutDir({ DS_EXPORT_OUT_DIR: "" }, ROOT),
    path.join(ROOT, "dist", "design-system")
  );
});

test("resolveOutDir: ein relativer DS_EXPORT_OUT_DIR ist ein sprechender Fehler, kein stiller Fallback", () => {
  assert.throws(
    () => resolveOutDir({ DS_EXPORT_OUT_DIR: "relativ/pfad" }, ROOT),
    /absoluter Pfad/
  );
});

test("expectedBundleFiles: enthält die sechs festen Dateien plus genau einen preview-Eintrag je Karte", () => {
  const files = expectedBundleFiles(COMPONENT_CARDS);
  const fixed = [
    "colors_and_type.css",
    "styles.css",
    "README.md",
    "SKILL.md",
    "fonts/LICENSE.md",
    "thumbnail.html",
  ];
  for (const f of fixed) {
    assert.ok(files.includes(f), `${f} fehlt in expectedBundleFiles`);
  }
  const previewEntries = files.filter((f) => f.startsWith("preview/"));
  assert.equal(
    previewEntries.length,
    COMPONENT_CARDS.length,
    "je Karte in COMPONENT_CARDS muss genau ein preview/-Eintrag entstehen"
  );
  for (const card of COMPONENT_CARDS) {
    assert.ok(
      previewEntries.includes(`preview/${card.id}.html`),
      `preview/${card.id}.html fehlt in expectedBundleFiles`
    );
  }
  assert.equal(files.length, fixed.length + COMPONENT_CARDS.length);
});

test("buildDsCardMarker: Attribute in fester Reihenfolge, ohne viewport", () => {
  assert.equal(
    buildDsCardMarker({ group: "Components", name: "Button", subtitle: "Alle Varianten" }),
    '<!-- @dsCard group="Components" name="Button" subtitle="Alle Varianten" -->'
  );
});

test("buildDsCardMarker: mit viewport kommt er als letztes Attribut", () => {
  assert.equal(
    buildDsCardMarker({ group: "Forms", name: "Input", subtitle: "Felder", viewport: "880x280" }),
    '<!-- @dsCard group="Forms" name="Input" subtitle="Felder" viewport="880x280" -->'
  );
});

test("buildDsCardMarker: Anführungszeichen und spitze Klammern beenden weder Attribut noch Kommentar", () => {
  const line = buildDsCardMarker({ group: "G", name: 'a"b', subtitle: "x --> y" });
  assert.ok(line.startsWith("<!-- ") && line.endsWith(" -->"));
  assert.equal(line.indexOf("-->"), line.length - 3, "--> darf nur am Ende stehen");
  assert.ok(!line.includes('a"b'));
});

test("buildDsCardMarker: fehlender oder leerer name/subtitle wird mit der Karten-Id abgelehnt", () => {
  // Verhindert, dass ein vergessenes data-ds-card-name als Text "null" im Marker landet.
  for (const bad of [null, undefined, "", "   "]) {
    assert.throws(
      () => buildDsCardMarker({ id: "components-x", group: "G", name: bad, subtitle: "ok" }),
      /components-x.*name/
    );
    assert.throws(
      () => buildDsCardMarker({ id: "components-x", group: "G", name: "ok", subtitle: bad }),
      /components-x.*subtitle/
    );
  }
});

test("buildThumbnailHtml: Wortmarke in Space Grotesk auf bg-app und Streifen Grün/Teal/Orange/Violett", () => {
  const html = buildThumbnailHtml({
    neonColors: [
      { name: "purple", hex: "#a001" },
      { name: "green", hex: "#a002" },
      { name: "orange", hex: "#a003" },
      { name: "teal", hex: "#a004" },
    ],
    backgroundVars: [{ name: "bg-app", value: "#010203" }],
  });
  assert.ok(html.includes("BudgetBuddy"));
  assert.ok(html.includes("Space Grotesk"));
  assert.ok(html.includes("fonts/space-grotesk-latin-wght-normal.woff2"));
  assert.ok(html.includes("#010203"));
  const order = ["#a002", "#a004", "#a003", "#a001"].map((h) => html.indexOf(`background: ${h}`));
  assert.ok(order.every((i) => i >= 0), "alle vier Farben müssen vorkommen");
  assert.deepEqual([...order].sort((a, b) => a - b), order, "Reihenfolge Grün, Teal, Orange, Violett");
});

test("expectedBundleFiles: mit weniger Karten schrumpft nur die preview-Liste, die festen Dateien bleiben", () => {
  const subset = COMPONENT_CARDS.slice(0, 3);
  const files = expectedBundleFiles(subset);
  assert.equal(files.length, 6 + subset.length);
  assert.ok(!files.includes(`preview/${COMPONENT_CARDS[COMPONENT_CARDS.length - 1].id}.html`));
});

// Regressionsanker gegen ein versehentliches Entfernen einer Karte aus
// COMPONENT_CARDS: expectedBundleFiles(COMPONENT_CARDS) leitet die Soll-Liste
// live aus COMPONENT_CARDS ab (AC1, keine zweite Kopie im Skript) und würde
// eine im Skript entfernte Karte deshalb NICHT von sich aus bemerken (Liste
// und Erwartung schrumpfen zusammen). Dieser Test pinnt deshalb die heute
// bekannten 16 Karten-IDs fest, unabhängig von COMPONENT_CARDS selbst
// gelesen: fehlt eine, schlägt er an, ohne dass ein Browser laufen muss.
const KNOWN_COMPONENT_CARD_IDS = [
  "components-card",
  "components-money",
  "components-badge",
  "components-table",
  "components-navigation",
  "components-button",
  "components-stats",
  "components-icons",
  "components-pageheader",
  "components-primitives",
  "components-swipe",
  "components-loading",
  "components-errordisplay",
  "components-input",
  "components-form",
  "components-toast",
];

test("COMPONENT_CARDS: exakt die heute bekannten 16 Komponenten-Karten (Regressionsanker, s. Kommentar oben)", () => {
  assert.deepEqual(
    [...COMPONENT_CARDS.map((c) => c.id)].sort(),
    [...KNOWN_COMPONENT_CARD_IDS].sort(),
    "COMPONENT_CARDS ist von der gepinnten Liste abgewichen (Karte entfernt/hinzugefügt/umbenannt); " +
      "bei einer bewussten Änderung KNOWN_COMPONENT_CARD_IDS hier mitziehen"
  );
});

// findMissingBundleFiles ist die Funktion, die main() selbst am Ende des
// Exports aufruft (AC1, Verifier-Fund Iteration 1): kein zweiter,
// abweichender Dateiplan, das Skript prüft mit derselben expectedBundleFiles-
// Soll-Liste, die der Test schon kennt. Gegen ein präpariertes Verzeichnis
// getestet, ganz ohne Browser oder echten Export.
test("findMissingBundleFiles: vollständiges Bundle liefert eine leere Liste", async () => {
  const subset = COMPONENT_CARDS.slice(0, 2);
  const dir = await mkdtemp(path.join(os.tmpdir(), "bb-bundle-check-"));
  try {
    await mkdir(path.join(dir, "preview"), { recursive: true });
    await mkdir(path.join(dir, "fonts"), { recursive: true });
    for (const rel of expectedBundleFiles(subset)) {
      await writeFile(path.join(dir, rel), "Inhalt", "utf8");
    }
    const missing = await findMissingBundleFiles(dir, subset);
    assert.deepEqual(missing, []);
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test("findMissingBundleFiles: eine fehlende Datei wird namentlich gemeldet", async () => {
  const subset = COMPONENT_CARDS.slice(0, 2);
  const dir = await mkdtemp(path.join(os.tmpdir(), "bb-bundle-check-"));
  try {
    await mkdir(path.join(dir, "preview"), { recursive: true });
    await mkdir(path.join(dir, "fonts"), { recursive: true });
    const expected = expectedBundleFiles(subset);
    const removed = `preview/${subset[0].id}.html`;
    for (const rel of expected) {
      if (rel === removed) continue;
      await writeFile(path.join(dir, rel), "Inhalt", "utf8");
    }
    const missing = await findMissingBundleFiles(dir, subset);
    assert.deepEqual(missing, [removed]);
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test("findMissingBundleFiles: eine leere Datei zählt ebenfalls als fehlend", async () => {
  const subset = COMPONENT_CARDS.slice(0, 2);
  const dir = await mkdtemp(path.join(os.tmpdir(), "bb-bundle-check-"));
  try {
    await mkdir(path.join(dir, "preview"), { recursive: true });
    await mkdir(path.join(dir, "fonts"), { recursive: true });
    for (const rel of expectedBundleFiles(subset)) {
      await writeFile(path.join(dir, rel), "", "utf8");
    }
    const missing = await findMissingBundleFiles(dir, subset);
    assert.deepEqual(
      [...missing].sort(),
      [...expectedBundleFiles(subset)].sort()
    );
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

// --- Browser-gebundener Vollauf-Test -----------------------------------

/// Prüft, ob ein Chromium tatsächlich startet (Playwright installiert,
/// Browser-Binary vorhanden). Liefert den Fehlertext statt zu werfen, damit
/// der aufrufende Test einen benannten Skip auslösen kann (Muster
/// screenshot-route.test.mjs/design-system-q2bmv).
async function chromiumLaunchError() {
  try {
    const { chromium } = await import("playwright");
    const browser = await chromium.launch();
    await browser.close();
    return null;
  } catch (err) {
    return err.message;
  }
}

test(
  "Vollauf: exportiert das gesamte Bundle in ein temporäres Verzeichnis; dist/design-system im Repo bleibt unberührt",
  { timeout: 300_000 },
  async (t) => {
    const launchError = await chromiumLaunchError();
    if (launchError) {
      t.skip(`Kein startbarer Chromium (Playwright/Browser fehlt): ${launchError}`);
      return;
    }

    let repoDistExistedBefore = true;
    let repoDistMtimeBefore = null;
    try {
      repoDistMtimeBefore = (await stat(REPO_DIST_DIR)).mtimeMs;
    } catch {
      repoDistExistedBefore = false;
    }

    const tmpParent = await mkdtemp(
      path.join(os.tmpdir(), "bb-design-export-test-")
    );
    const exportOutDir = path.join(tmpParent, "design-system");
    try {
      const result = spawnSync(process.execPath, [EXPORT_SCRIPT], {
        cwd: ROOT,
        env: { ...process.env, DS_EXPORT_OUT_DIR: exportOutDir },
        encoding: "utf8",
        timeout: 280_000,
      });
      assert.equal(
        result.status,
        0,
        `Export-Skript endete mit Code ${result.status}:\n${result.stdout}\n${result.stderr}`
      );

      const expected = expectedBundleFiles(COMPONENT_CARDS);
      for (const rel of expected) {
        const abs = path.join(exportOutDir, rel);
        const info = await stat(abs).catch(() => null);
        assert.ok(info, `${rel} fehlt im exportierten Bundle`);
        assert.ok(info.isFile(), `${rel} ist keine Datei`);
        assert.ok(info.size > 0, `${rel} ist leer`);
      }

      // Jede preview/*.html (auch Token-/Brand- und Portal-Karten) trägt den
      // vollständigen Marker mit gemessenem viewport (design-system-an22r).
      const previewFiles = (await readdir(path.join(exportOutDir, "preview"))).filter((f) =>
        f.endsWith(".html")
      );
      for (const card of COMPONENT_CARDS) {
        assert.ok(previewFiles.includes(`${card.id}.html`), `${card.id}.html fehlt`);
      }
      assert.ok(previewFiles.length >= COMPONENT_CARDS.length + 4);
      for (const file of previewFiles) {
        const html = await readFile(path.join(exportOutDir, "preview", file), "utf8");
        const line1 = html.split("\n")[0];
        assert.match(
          line1,
          /^<!-- @dsCard group="[^"]+" name="(?!null")[^"]+" subtitle="(?!null")[^"]+" viewport="880x\d+" -->$/,
          `preview/${file}: Zeile 1 entspricht nicht dem @dsCard-Format`
        );
      }
      // Karten mit fixed-positioniertem Inhalt: der leere Wrapper misst nur 64 px,
      // die gemessene Höhe muss den echten Inhalt abdecken (design-system-an22r).
      for (const id of [
        "components-bottomsheet",
        "components-categorypicker",
        "components-modal",
        "components-toast",
      ]) {
        const html = await readFile(path.join(exportOutDir, "preview", `${id}.html`), "utf8");
        const h = Number(html.split("\n")[0].match(/viewport="880x(\d+)"/)[1]);
        assert.ok(h >= 100, `${id}: gemessene Höhe ${h} deckt den fixed-Inhalt nicht ab`);
      }

      const fontsDir = path.join(exportOutDir, "fonts");
      const fontsFiles = await readdir(fontsDir);
      assert.ok(fontsFiles.includes("LICENSE.md"), "fonts/LICENSE.md fehlt");
      const nonLicenseFontFiles = fontsFiles.filter((f) => f !== "LICENSE.md");
      assert.ok(
        nonLicenseFontFiles.length >= 1,
        "fonts/ enthält außer LICENSE.md keine Font-Datei"
      );

      if (repoDistExistedBefore) {
        const mtimeAfter = (await stat(REPO_DIST_DIR)).mtimeMs;
        assert.equal(
          mtimeAfter,
          repoDistMtimeBefore,
          "dist/design-system im Repo wurde beim Export mit DS_EXPORT_OUT_DIR angefasst"
        );
      } else {
        const existsAfter = await stat(REPO_DIST_DIR)
          .then(() => true)
          .catch(() => false);
        assert.equal(
          existsAfter,
          false,
          "dist/design-system im Repo wurde erzeugt, obwohl DS_EXPORT_OUT_DIR gesetzt war"
        );
      }
    } finally {
      await rm(tmpParent, { recursive: true, force: true });
    }

    const tmpParentStillExists = await stat(tmpParent)
      .then(() => true)
      .catch(() => false);
    assert.equal(
      tmpParentStillExists,
      false,
      "temporäres Exportverzeichnis wurde nach dem Test nicht aufgeräumt"
    );
  }
);
