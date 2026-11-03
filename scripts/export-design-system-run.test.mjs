// scripts/export-design-system-run.test.mjs
//
// Der eine Browser-Test für den Design-System-Export (design-system-w4n9r, infrastructure-t5m2b):
// das Skript läuft einmal ganz, mit vite preview und Chromium, in ein temporäres Verzeichnis.
// Die reinen Teile prüft scripts/export-design-system.test.mjs.
//
// Muster (Playwright per dynamischem Import, benannter Skip ohne Browser) nach
// design-system-q2bmv.

import { test } from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import os from "node:os";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { mkdtemp, rm, stat, readdir, readFile } from "node:fs/promises";
import { expectedBundleFiles, COMPONENT_CARDS } from "./export-design-system.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const EXPORT_SCRIPT = path.join(__dirname, "export-design-system.mjs");
const REPO_DIST_DIR = path.join(ROOT, "dist", "design-system");

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
