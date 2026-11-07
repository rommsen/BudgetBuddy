// scripts/category-picker.test.mjs
//
// Wächter für `design-system-gffps`: der Kategorie-Picker mit Einfachauswahl und die Karte
// "Budget nach dem Wechsel", gemessen am echten, von der App gerenderten Element in der
// Galerie `/styleguide` (Sektion 26): Tastatur und Fokus im Popover am PC. Inhalt, Auswahl und Abschnitte
// prüft `CategoryPickerTests.fs`, das Sheet am Handy der Layout-Sweep (Zustand budget-picker).
//
// Ausführen: node --test scripts/category-picker.test.mjs
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

async function openGallery(contextOptions) {
  const page = await (await browser.newContext(contextOptions)).newPage();
  await page.goto(`http://127.0.0.1:${frontendPort}/#/styleguide`, { waitUntil: "load" });
  await page.locator('[data-ds-demo="category-picker-popover-trigger"]').waitFor();
  return page;
}

const section = (page) => page.locator("section", { hasText: "26. Kategorie-Picker (Einfachauswahl)" });
const options = (scope) => scope.locator('[role="option"]');

const selectCount = async (sec) =>
  Number(await sec.locator('[data-ds-demo="category-picker-select-count"]').textContent());

test("[tastatur] PC: beim Öffnen steht der Fokus im Suchfeld, ein Mausklick wählt genau einmal und schließt; Pfeile, Enter und Esc", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  const sec = section(page);
  const trigger = page.locator('[data-ds-demo="category-picker-popover-trigger"] button');
  const listbox = sec.locator('[role="listbox"]');
  assert.equal(await selectCount(sec), 0);
  await trigger.click();
  await listbox.waitFor();
  assert.equal(await sec.locator('input[placeholder="Kategorie suchen…"]').evaluate((el) => el === document.activeElement), true, "Fokus im Suchfeld");
  await options(sec).filter({ hasText: "Versicherung" }).last().click();
  assert.equal(await listbox.count(), 0, "Popover geschlossen");
  assert.match(await sec.textContent(), /Versicherung/, "Auswahl übernommen");
  assert.equal(await selectCount(sec), 1, "ein OnSelect");

  // Pfeil runter, Enter wählt die markierte Zeile (die erste ist "Ohne Kategorie").
  await trigger.click();
  await listbox.waitFor();
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  assert.equal(await listbox.count(), 0, "Enter wählt und schließt");
  await sec.getByText("Ohne Kategorie", { exact: true }).waitFor();
  assert.equal(await selectCount(sec), 2, "Ohne Kategorie: ein OnSelect");

  // Esc schließt nur den Picker.
  await trigger.click();
  await listbox.waitFor();
  await page.keyboard.press("Escape");
  assert.equal(await listbox.count(), 0, "Esc schließt");
  await page.close();
});

// ledger-dmw83: Befund vom 2026-10-24: ein Ladefehler sah aus wie "es gibt keine Kategorien" und
// es gab keinen Weg, es erneut zu versuchen. Die Zustände selbst liegen in CategoryPickerTests.fs.

test("[flow:kategorien-neu-laden] PC: Zustand fehlgeschlagen zeigt Fehlertext und Erneut versuchen, der Knopf ruft OnRetry", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  await page.locator('[data-ds-demo="category-picker-failed-trigger"] button').click();
  const box = page.locator('[data-ds-demo="category-picker-failed"]');
  const alert = box.locator('[role="alert"]');
  await alert.waitFor();
  assert.match(await alert.textContent(), /Kategorien konnten nicht geladen werden/);
  assert.equal(await box.locator('[role="option"]').count(), 0, "keine Zeilen");
  assert.equal(await box.getByText("Ohne Kategorie").count(), 0, "kein Ohne Kategorie");
  const count = page.locator('[data-ds-demo="category-picker-retry-count"]');
  assert.equal(await count.textContent(), "0");
  await alert.getByRole("button", { name: "Erneut versuchen" }).click();
  assert.equal(await count.textContent(), "1", "OnRetry genau einmal");
  await page.close();
});

