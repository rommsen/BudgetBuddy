// scripts/styleguide-split-line-actions.test.mjs
//
// Wächter für `design-system-y2bdj`: die Split-Editor-Karte in der Galerie `/styleguide` zeigt die
// echte Knopfzeile `SplitEditor.lineActions` mit "Zeile hinzufügen" und "Barabhebung", gemessen am
// von der App gerenderten Element.
//
// Ausführen: node --test scripts/styleguide-split-line-actions.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";

let frontend;
let frontendPort;
let browser;

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("Split-Editor-Karte enthält beide Knöpfe der Knopfzeile", async () => {
  const page = await (await browser.newContext({ viewport: { width: 1440, height: 900 } })).newPage();
  await page.goto(`http://127.0.0.1:${frontendPort}/#/styleguide`, { waitUntil: "load" });
  const editor = page.locator('[data-ds-demo="split-editor"]');
  await editor.waitFor();
  const labels = await editor.locator("button").evaluateAll((els) => els.map((el) => el.textContent.trim()));
  assert.ok(labels.some((t) => t.includes("Zeile hinzufügen")), `Zeile hinzufügen fehlt: ${labels}`);
  assert.ok(labels.some((t) => t.includes("Barabhebung")), `Barabhebung fehlt: ${labels}`);
  await page.close();
});
