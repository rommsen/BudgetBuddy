// scripts/category-picker.test.mjs
//
// Wächter für `design-system-gffps`: der Kategorie-Picker mit Einfachauswahl und die Karte
// "Budget nach dem Wechsel", gemessen am echten, von der App gerenderten Element in der
// Galerie `/styleguide` (Sektion 26). Popover am PC, Sheet am Handy, derselbe Inhalt.
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

test("PC: Popover mit Suche, Ohne Kategorie, Vorgeschlagen, Beträgen rechts und Stand-Zeile", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  await page.locator('[data-ds-demo="category-picker-popover-trigger"] button').click();
  const sec = section(page);
  const listbox = sec.locator('[role="listbox"]');
  await listbox.waitFor();
  assert.equal(await sec.locator('input[placeholder="Kategorie suchen…"]').evaluate((el) => el === document.activeElement), true, "Fokus im Suchfeld");
  assert.equal(await options(sec).first().textContent(), "Ohne Kategorie");
  assert.match(await listbox.textContent(), /Vorgeschlagen/i);
  assert.equal(await sec.getByText("Alle Kategorien", { exact: true }).count(), 1, "Kopf Alle Kategorien über der Liste");
  assert.equal(await sec.getByText("Stand 14:02").count(), 1, "Stand-Zeile");
  assert.equal(await sec.getByRole("button", { name: /Übernehmen/ }).count(), 0, "kein Übernehmen-Knopf");
  // Beträge stehen bündig rechts: alle Betragsslots enden an derselben Kante.
  const rights = await sec.evaluate((el) => {
    const slots = [...el.querySelectorAll('[role="option"] .w-24')];
    return slots.map((s) => Math.round(s.getBoundingClientRect().right));
  });
  assert.ok(rights.length > 3, "Beträge vorhanden");
  assert.equal(new Set(rights).size, 1, `Betragsslots rechtsbündig: ${rights}`);
  // Aktuelle Kategorie trägt aria-selected.
  assert.equal(await sec.locator('[role="option"][aria-selected="true"]').count() >= 1, true);
  await page.close();
});

test("PC: Klick übernimmt sofort und schließt; Pfeile, Enter und Esc", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  const sec = section(page);
  const trigger = page.locator('[data-ds-demo="category-picker-popover-trigger"] button');
  const listbox = sec.locator('[role="listbox"]');
  await trigger.click();
  await listbox.waitFor();
  await options(sec).filter({ hasText: "Versicherung" }).last().click();
  assert.equal(await listbox.count(), 0, "Popover geschlossen");
  assert.match(await sec.textContent(), /Versicherung/, "Auswahl übernommen");

  // Pfeil runter, Enter wählt die markierte Zeile (die erste ist "Ohne Kategorie").
  await trigger.click();
  await listbox.waitFor();
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  assert.equal(await listbox.count(), 0, "Enter wählt und schließt");
  await sec.getByText("Ohne Kategorie", { exact: true }).waitFor();

  // Esc schließt nur den Picker.
  await trigger.click();
  await listbox.waitFor();
  await page.keyboard.press("Escape");
  assert.equal(await listbox.count(), 0, "Esc schließt");
  await page.close();
});

const selectCount = async (sec) =>
  Number(await sec.locator('[data-ds-demo="category-picker-select-count"]').textContent());

test("PC: Betrag trägt die Farbklasse nach Vorzeichen (positiv, negativ, null)", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  const sec = section(page);
  await sec.locator('[data-ds-demo="category-picker-popover-trigger"] button').click();
  await sec.locator('[role="listbox"]').waitFor();
  const amountClass = (name) =>
    options(sec)
      .filter({ has: page.locator("span.truncate", { hasText: new RegExp(`^${name}$`) }) })
      .last()
      .locator(".w-24 span")
      .getAttribute("class");
  assert.match(await amountClass("Lebensmittel"), /text-text-primary/, "positiv");
  assert.match(await amountClass("Freizeit"), /text-neon-red/, "negativ");
  assert.match(await amountClass("Sonstiges"), /text-text-muted/, "null");
  await page.close();
});

test("PC: Versteckt aufklappen, Kategorie ohne Available hat einen leeren Betragsslot", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  const sec = section(page);
  await sec.locator('[data-ds-demo="category-picker-popover-trigger"] button').click();
  await sec.locator('[role="listbox"]').waitFor();
  assert.equal(await options(sec).filter({ hasText: "Altes Sparziel" }).count(), 0, "zunächst eingeklappt");
  await sec.getByRole("button", { name: "Versteckt (1)" }).click();
  const hiddenRow = options(sec).filter({ hasText: "Altes Sparziel" });
  await hiddenRow.waitFor();
  assert.equal(await hiddenRow.locator(".w-24").count(), 0, "kein Betragsslot");
  assert.equal(await hiddenRow.locator("span.font-mono").count(), 0, "kein Betrag");
  // Gegenprobe: eine Zeile mit Available hat den Slot.
  assert.equal(await options(sec).filter({ hasText: "Miete" }).last().locator(".w-24").count(), 1);
  await page.close();
});

test("PC: Mausklick wählt genau einmal, auch auf Ohne Kategorie", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  const sec = section(page);
  const trigger = sec.locator('[data-ds-demo="category-picker-popover-trigger"] button');
  const listbox = sec.locator('[role="listbox"]');
  assert.equal(await selectCount(sec), 0);

  await trigger.click();
  await listbox.waitFor();
  await options(sec).filter({ hasText: "Miete" }).last().click();
  await listbox.waitFor({ state: "detached" });
  assert.equal(await selectCount(sec), 1, "Kategorie: ein OnSelect");

  await trigger.click();
  await listbox.waitFor();
  await options(sec).filter({ hasText: "Ohne Kategorie" }).first().click();
  await listbox.waitFor({ state: "detached" });
  assert.equal(await selectCount(sec), 2, "Ohne Kategorie: ein OnSelect");
  assert.match(await sec.textContent(), /Ohne Kategorie/, "Ergebnis steht als Badge");

  // Danach ist Ohne Kategorie die aktuelle Zeile.
  await trigger.click();
  await listbox.waitFor();
  assert.equal(await options(sec).first().getAttribute("aria-selected"), "true");
  await page.close();
});

test("PC: Variante ohne Beschriftung hat keine Stand-Zeile, die mit Beschriftung hat sie", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  const sec = section(page);
  await sec.locator('[data-ds-demo="category-picker-plain-trigger"] button').click();
  const plain = sec.locator('[data-ds-demo="category-picker-plain"]');
  await plain.locator('[role="listbox"]').waitFor();
  assert.equal(await plain.getByText(/^Stand /).count(), 0, "ohne Beschriftung keine Stand-Zeile");
  await sec.locator('[data-ds-demo="category-picker-plain-trigger"] button').click();
  await sec.locator('[data-ds-demo="category-picker-popover-trigger"] button').click();
  const withStand = sec.locator('[data-ds-demo="category-picker-with-stand"]');
  await withStand.locator('[role="listbox"]').waitFor();
  assert.equal(await withStand.getByText("Stand 14:02").count(), 1, "mit Beschriftung eine Stand-Zeile");
  await page.close();
});

test("PC: Suche filtert nach Name und Gruppe und blendet Vorgeschlagen aus", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  await page.locator('[data-ds-demo="category-picker-popover-trigger"] button').click();
  const sec = section(page);
  await sec.locator('[role="listbox"]').waitFor();
  const before = await options(sec).count();
  await page.keyboard.type("MIETE");
  const after = await options(sec).count();
  assert.ok(after >= 1 && after < before, `gefiltert: ${before} auf ${after}`);
  assert.equal(await sec.getByText("Vorgeschlagen", { exact: true }).count(), 0, "Vorgeschlagen entfällt bei Suche");
  assert.equal(await sec.getByText("Alle Kategorien", { exact: true }).count(), 0, "Kopf entfällt bei Suche");
  await page.close();
});

test("Handy (390): Sheet mit denselben Zeilen, 16 px Suchschrift, Betragsslots bündig", async () => {
  const page = await openGallery({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  await page.locator('[data-ds-demo="category-picker-sheet-trigger"] button').evaluate((el) => el.click());
  const sheet = page.locator(".bottom-sheet.active");
  await sheet.waitFor();
  assert.match(await sheet.locator("h3").textContent(), /Kategorie wählen/);
  assert.equal(await sheet.locator('[role="option"]').first().textContent(), "Ohne Kategorie");
  // Das Sheet fährt mit einer 450 ms langen Feder ein. Gemessen wird erst, wenn seine Lage
  // über mehrere Frames unverändert ist.
  await page.waitForFunction(() => {
    const el = document.querySelector(".bottom-sheet.active");
    if (!el) return false;
    const key = () => JSON.stringify(el.getBoundingClientRect());
    window.__sheetRect ??= { key: key(), stable: 0 };
    const now = key();
    if (now === window.__sheetRect.key) window.__sheetRect.stable += 1;
    else window.__sheetRect = { key: now, stable: 0 };
    return window.__sheetRect.stable >= 5;
  }, null, { polling: "raf" });
  const geometry = await sheet.evaluate((el) => {
    const input = el.querySelector("input");
    const slots = [...el.querySelectorAll('[role="option"] .w-24')].map((s) => Math.round(s.getBoundingClientRect().right));
    const rowHeights = [...el.querySelectorAll('[role="option"]')].map((r) => r.getBoundingClientRect().height);
    return { fontSize: getComputedStyle(input).fontSize, slots, minRow: Math.min(...rowHeights) };
  });
  assert.equal(geometry.fontSize, "16px");
  assert.equal(new Set(geometry.slots).size, 1, `rechtsbündig: ${geometry.slots}`);
  // 44 px Trefferfläche; 0,5 px Toleranz für Subpixel-Rundung der Höhe.
  assert.ok(geometry.minRow >= 43.5, `Trefferfläche ${geometry.minRow}`);
  await sheet.locator('[role="option"]').filter({ hasText: "Miete" }).first().tap();
  await page.waitForTimeout(500);
  assert.equal(await page.locator(".bottom-sheet.active").count(), 0, "Sheet geschlossen");
  await page.close();
});

test("Karte Budget nach dem Wechsel: Label, alter Wert durchgestrichen, neuer Wert", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  const card = page.locator('[role="status"]', { hasText: "Budget nach dem Wechsel" });
  await card.waitFor();
  assert.equal(await card.locator(".line-through").count(), 2, "zwei durchgestrichene alte Werte");
  assert.match(await card.textContent(), /Alltag: Haushalt.*412,10 €.*494,00 €/);
  assert.equal(await card.evaluate((el) => getComputedStyle(el.querySelector(".line-through")).textDecorationLine), "line-through");
  await page.close();
});

// ledger-dmw83: Lade- und Fehlerzustand. Befund vom 2026-10-24: Waren die Kategorien nicht
// geladen, zeigte der Picker nur "Ohne Kategorie"; ein Ladefehler sah aus wie "es gibt keine
// Kategorien" und es gab keinen Weg, es erneut zu versuchen.

test("PC: Zustand lädt zeigt Spinner und Hinweis statt der Liste, auch ohne Ohne Kategorie", async () => {
  const page = await openGallery({ viewport: { width: 1440, height: 900 } });
  await page.locator('[data-ds-demo="category-picker-loading-trigger"] button').click();
  const box = page.locator('[data-ds-demo="category-picker-loading"]');
  const status = box.locator('[role="status"]');
  await status.waitFor();
  assert.match(await status.textContent(), /Kategorien werden geladen/);
  assert.equal(await status.locator(".proto-spinner").count(), 1, "Spinner aus dem Design-System");
  assert.equal(await box.locator('[role="option"]').count(), 0, "keine Zeilen");
  assert.equal(await box.getByText("Ohne Kategorie").count(), 0, "kein Ohne Kategorie");
  assert.equal(await box.locator('input ~ * button, [role="listbox"] button').count(), 0, "kein Knopf im Inhalt");
  await page.close();
});

test("PC: Zustand fehlgeschlagen zeigt Fehlertext und Erneut versuchen, der Knopf ruft OnRetry", async () => {
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

test("Styleguide zeigt die Variante mit Zu verteilen als erstem Eintrag (budgeting-afd8h)", async () => {
  const page = await openGallery({ viewport: { width: 1280, height: 800 } });
  try {
    await page.locator('[data-ds-demo="category-picker-to-be-budgeted-trigger"] button').click();
    const first = page.locator('[data-ds-demo="category-picker-to-be-budgeted"] [role="option"]').first();
    await first.waitFor();
    assert.match(await first.innerText(), /Zu verteilen/);
    assert.equal(await first.getAttribute("data-testid"), "category-picker-to-be-budgeted");
  } finally {
    await page.context().close();
  }
});
