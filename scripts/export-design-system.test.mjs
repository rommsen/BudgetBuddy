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

test("expectedBundleFiles: enthält die fünf festen Dateien plus genau einen preview-Eintrag je Karte", () => {
  const files = expectedBundleFiles(COMPONENT_CARDS);
  const fixed = [
    "colors_and_type.css",
    "styles.css",
    "README.md",
    "SKILL.md",
    "fonts/LICENSE.md",
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

test("expectedBundleFiles: mit weniger Karten schrumpft nur die preview-Liste, die festen Dateien bleiben", () => {
  const subset = COMPONENT_CARDS.slice(0, 3);
  const files = expectedBundleFiles(subset);
  assert.equal(files.length, 5 + subset.length);
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

      for (const card of COMPONENT_CARDS) {
        const html = await readFile(
          path.join(exportOutDir, "preview", `${card.id}.html`),
          "utf8"
        );
        assert.ok(
          html.startsWith("<!-- @dsCard"),
          `preview/${card.id}.html beginnt nicht mit <!-- @dsCard`
        );
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
