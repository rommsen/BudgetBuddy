// scripts/screenshot-route-capture.test.mjs
//
// Der eine Browser-Test für scripts/screenshot-route.mjs (infrastructure-t5m2b): die Aufnahme im
// echten Chromium. Die übrigen Tests des Werkzeugs stehen browserfrei in
// scripts/screenshot-route.test.mjs, die Rechenregel in computeCaptureViewport.

import { test } from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import os from "node:os";
import { readFileSync } from "node:fs";
import { mkdtemp, rm } from "node:fs/promises";
import { chromium } from "playwright";
import { captureAtDocumentHeight } from "./screenshot-route.mjs";

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

// AC1/AC2/AC3 (design-system-q2bmv), der eigentliche Kern dieser Aufgabe:
// baut per page.setContent eine Seite mit demselben Aufbau wie ADR 0005
// (`.bottom-sheet` in src/Client/styles.css:1556, `top: calc(var(--vv-top,
// 0px) + var(--vvh, 100dvh))`) und stellt zuerst per Gegenprobe sicher, dass
// `fullPage: true` den Fehler aus ledger-p9k3w hier wirklich zeigt (sonst
// wäre die zweite Zusicherung nicht falsifizierend). Erst danach prüft der
// Test die echte Funktion captureAtDocumentHeight gegen dieselbe Seite.
test("captureAtDocumentHeight nimmt die volle Dokumenthöhe auf und hält die .bottom-sheet-Leiste außerhalb des Bilds (ADR 0005, ledger-p9k3w)", async () => {
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

