// scripts/key-invariants.test.mjs
//
// Der Tasten-Sweep (design-system-gcpws): jede Liste mit Panel aus `key-invariants.mjs` bei 1024 und 1440 px, für jede
// dieselben Invarianten T1 bis T7 (Styleguide §7.8), dazu der Fokus der Auswahl-Dialoge P1 bis P3 (§7.9). Die Regel
// selbst prüft .NET als Aufzählung (`src/Tests/ListKeysTests.fs`); hier steht nur, was der echte DOM zeigt.
//
// Je Invariante ein Gegenbeweis: ein Skript vor dem Laden bricht die Verdrahtung (Handler am Dokument abgefangen, eine
// Taste doppelt, Scrollen aus, Fokus weg), und der Sweep meldet die Verletzung mit Liste, Breite, Invariante und Taste.
//
// Ausführen: npm run test:scripts -- scripts/key-invariants.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";
import { formatKeyViolation, keySweep, LISTS, listNamed, runList, runWithoutPanel } from "./key-invariants.mjs";
import { settle } from "./layout-invariants.mjs";
import { stateNamed } from "./layout-states.mjs";

let frontend;
let frontendPort;
let browser;
let baseUrl;

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  baseUrl = `http://127.0.0.1:${frontendPort}`;
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("[tastatur] Tasten-Sweep: jede Liste mit Panel hält die Tastenregeln bei 1024 und 1440 px", async () => {
  const { violations, errors, runs } = await keySweep(browser, baseUrl, LISTS);
  assert.deepEqual(errors, [], "jede Liste ließ sich herstellen");
  assert.equal(runs, LISTS.length * 2 + LISTS.filter((l) => l.arrows !== false && !l.keysWithoutPanel).length, "jede Liste bei zwei Breiten, dazu unter 1024 px");
  assert.deepEqual(violations.map(formatKeyViolation), [], `${violations.length} Verletzungen in ${runs} Läufen`);
});

/// Ein Lauf mit gebrochener Verdrahtung: die Verletzung trägt Liste, Breite, Invariante und Taste; ohne das Skript ist
/// dieselbe Prüfung grün.
async function brokenFinds(listName, width, inv, initScript) {
  const list = listNamed(listName);
  const hits = (await runList(browser, baseUrl, list, width, { initScript, checks: [inv] })).filter((v) => v.inv === inv);
  assert.ok(hits.length > 0, `${inv} meldet die gebrochene Seite (${listName} @ ${width})`);
  for (const v of hits) {
    assert.equal(v.list, listName);
    assert.equal(v.width, width);
    assert.ok(v.key && v.value, `Taste und Befund: ${formatKeyViolation(v)}`);
  }
  const clean = await runList(browser, baseUrl, list, width, { checks: [inv] });
  assert.deepEqual(clean.map(formatKeyViolation), [], "ohne das Skript keine Meldung");
  return hits;
}

/// Fängt die Tasten ab, bevor sie das Dokument erreichen (außer in Feldern): so, als hinge kein Handler am Dokument.
const noDocumentKeys = `window.addEventListener("keydown", (e) => {
  if (!(e.target instanceof Element) || !e.target.closest("input, textarea, [role=listbox]")) e.stopImmediatePropagation();
}, true);`;

test("[tastatur] Gegenbeweis T1 und T6: ohne Tasten am Dokument meldet der Sweep jede Liste", async () => {
  const { violations, errors } = await keySweep(browser, baseUrl, LISTS, { initScript: noDocumentKeys, checks: ["T1", "T6"] });
  assert.deepEqual(errors, [], "jede Liste ließ sich herstellen");
  for (const list of LISTS) {
    for (const width of [1024, 1440]) {
      const hits = violations.filter((v) => v.list === list.name && v.width === width);
      const expected = list.arrows === false ? "T6" : "T1";
      assert.ok(hits.some((v) => v.inv === expected), `${list.name} @ ${width}: ${expected} gemeldet\n${hits.map(formatKeyViolation).join("\n")}`);
    }
  }
});

test("[tastatur] Gegenbeweis T2: ein Druck, der zwei Schritte macht, meldet den Doppelschritt", async () => {
  const twice = `document.addEventListener("keydown", (e) => {
    if (e.isTrusted && e.key === "ArrowDown") setTimeout(() => document.body.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowDown", bubbles: true })), 0);
  });`;
  const hits = await brokenFinds("budget", 1440, "T2", twice);
  assert.ok(hits.some((v) => v.key === "ArrowDown"), hits.map(formatKeyViolation).join("\n"));
});

test("[tastatur] Gegenbeweis T3: ein Pfeil mit Alt, der die Zeile wechselt, meldet die Zusatztaste", async () => {
  const altAsPlain = `window.addEventListener("keydown", (e) => {
    if (e.isTrusted && e.altKey && e.key === "ArrowDown") { e.stopImmediatePropagation(); document.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowDown", bubbles: true })); }
  }, true);`;
  const hits = await brokenFinds("budget", 1440, "T3", altAsPlain);
  assert.ok(hits.some((v) => v.key === "Alt+ArrowDown"), hits.map(formatKeyViolation).join("\n"));
});

test("[tastatur] Gegenbeweis T4: ein Zeichen, das die Suche nicht erreicht, meldet das Tippen", async () => {
  const noTyping = `window.addEventListener("keydown", (e) => { if (e.key.length === 1 && e.target === document.body) e.stopImmediatePropagation(); }, true);`;
  await brokenFinds("budget", 1024, "T4", noTyping);
});

test("[tastatur] Gegenbeweis T5: ein Fokus, der nach Esc nicht zum Auslöser zurückkehrt, meldet die Ebene", async () => {
  const blurAfterEsc = `window.addEventListener("keydown", (e) => { if (e.key === "Escape") setTimeout(() => document.activeElement?.blur(), 0); }, true);`;
  const hits = await brokenFinds("regeln", 1440, "T5", blurAfterEsc);
  assert.ok(hits.some((v) => /Auslöser/.test(v.value)), hits.map(formatKeyViolation).join("\n"));
});

test("[tastatur] Gegenbeweis T7: ohne Scrollen ins Bild meldet der Sweep die verdeckte Zeile", async () => {
  const noScroll = `Element.prototype.scrollIntoView = function () {};`;
  await brokenFinds("kassenbuch", 1440, "T7", noScroll);
});

test("[tastatur] Gegenbeweis T8: eine Taste, die unter 1024 px die Zeile wählt, meldet die Seitentaste", async () => {
  // Ein Klick per Skript auf die erste Zeile, sobald ↓ kommt: so, als hinge ein Handler am Dokument.
  const clicksRow = `document.addEventListener("keydown", (e) => {
    if (e.key === "ArrowDown") document.querySelector('[data-testid="rule-row"] button, [data-testid="rule-row"]')?.click();
  });`;
  const hits = await runWithoutPanel(browser, baseUrl, listNamed("regeln"), { initScript: clicksRow });
  assert.ok(hits.some((v) => v.inv === "T8" && v.width === 900), hits.map(formatKeyViolation).join("\n"));
  assert.deepEqual((await runWithoutPanel(browser, baseUrl, listNamed("regeln"))).map(formatKeyViolation), [], "ohne das Skript keine Meldung");
});

// ============================================================
// Auswahl-Dialoge (§7.9)
// ============================================================

const frames = (page) => page.evaluate(() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))));

/// Ein Auswahl-Dialog: `state` liefert Route und Mocks, `open(page)` öffnet ihn mit der Maus, `short` heißt kurze
/// Auswahl ohne Suche (F7, erkannt an `[data-short-choice]`), `chosen(page)` liest, was gewählt ist (Text oder Zahl der
/// Häkchen). Eine neue Auswahl bekommt hier einen Eintrag (prose-only, kein Test merkt einen fehlenden).
const DIALOGS = [
  {
    name: "Kategorie in der Regel",
    state: "regeln",
    width: 1440,
    open: async (page) => {
      await page.locator('[data-testid="rule-row"]:visible').first().click();
      await page.locator('[data-testid="rule-category"]').click();
      await page.locator('[role="option"]').first().waitFor();
    },
    chosen: (page) => page.locator('[data-testid="rule-category"]').innerText(),
    closes: true,
  },
  {
    name: "Kategorie-Filter im Kassenbuch",
    state: "kassenbuch",
    width: 1440,
    open: async (page) => {
      await page.getByRole("button", { name: /^Kategorie/ }).locator("visible=true").first().click();
      await page.locator('#category-multi-select-listbox [id^="category-multi-select-option-"]').locator("visible=true").first().waitFor();
    },
    chosen: (page) => page.evaluate(() => [...document.querySelectorAll('#category-multi-select-listbox input[type="checkbox"]')].filter((b) => b.checked).length),
    toggles: true,
  },
  {
    name: "Payee im Detail",
    state: "kassenbuch-payee-auswahl-pc",
    width: 1440,
    open: (page, state) => state.open(page, { width: 1440, mobile: false }),
    chosen: (page) => page.locator('[data-testid="payee-picker-option"]').count(),
    closes: true,
  },
  {
    name: "Payee im Durchgehen",
    state: "durchgehen-karte",
    width: 900,
    mock: async (page) => {
      await page.route("**/api/LedgerApi/getLedgerPayeeSuggestions", (r) =>
        r.fulfill({ contentType: "application/json", body: JSON.stringify([{ Payee: "Bauhaus", Count: 9 }, { Payee: "Baumarkt", Count: 2 }]) })
      );
    },
    open: async (page) => {
      await page.locator('[data-testid="review-payee"]').locator("visible=true").first().click();
      // Die Suche trägt den Payee der Karte, darunter steht „… übernehmen“.
      await page.locator('[data-testid="payee-picker-take"], [data-testid="payee-picker-option"]').locator("visible=true").first().waitFor();
    },
    chosen: (page) => page.locator('[data-testid="payee-picker"]').locator("visible=true").count(),
    closes: true,
  },
  {
    name: "Konto in „Neue Buchung“",
    state: "kassenbuch-erfassen-panel",
    width: 1440,
    short: true,
    open: async (page, state) => {
      await state.open(page, { width: 1440, mobile: false });
      await page.locator('[data-testid="ledger-detail-panel"] [data-testid="capture-account"]').click();
      await page.locator('[data-testid="capture-account-list"] button').first().waitFor();
    },
    chosen: (page) => page.locator('[data-testid="ledger-detail-panel"] [data-testid="capture-account"]').innerText(),
    closes: true,
  },
  {
    // G13: die Gruppe in „Kategorie bearbeiten“, kurze Auswahl ohne Suche im Dialog.
    name: "Gruppe in „Kategorie bearbeiten“",
    state: "kategorien-bearbeiten",
    width: 1440,
    short: true,
    open: async (page) => {
      await page.locator('[data-testid="categories-panel"] [data-testid="categories-group-field"]').click();
      await page.locator('[data-testid="categories-group-choice"] button').first().waitFor();
    },
    chosen: (page) => page.locator('[data-testid="categories-panel"] [data-testid="categories-group-field"]').innerText(),
    closes: true,
  },
  {
    // budgeting-h2brj: der Wochentag im Tab Target, kurze Auswahl ohne Suche im Dialog. Erst ein Tag gewählt, damit beim
    // Öffnen eine Option `aria-current` trägt.
    name: "Wochentag im Tab Target",
    state: "budget-target",
    width: 1440,
    short: true,
    open: async (page) => {
      const panel = page.locator('aside[data-testid="budget-inspector"]');
      const field = panel.locator('[data-testid="target-weekday-field"]');
      await panel.getByRole("button", { name: "Woche", exact: true }).click();
      await field.click();
      await page.locator('[data-testid="target-weekday-options"]').getByRole("button", { name: "Mittwoch" }).click();
      await field.click();
      await page.locator('[data-testid="target-weekday-options"] button').first().waitFor();
    },
    chosen: (page) => page.locator('aside[data-testid="budget-inspector"] [data-testid="target-weekday-field"]').innerText(),
    closes: true,
  },
  {
    // budgeting-yarty: „Historie zeigen“ hinter dem Feld „Zeigen“ der Budget-Historie, kurze Auswahl ohne Suche im
    // Dialog. „Alle“ ist beim Öffnen gewählt und trägt `aria-current`.
    name: "Historie zeigen",
    state: "budget-historie",
    width: 1440,
    short: true,
    open: async (page) => {
      await page.locator('[data-testid="general-history-filter-field"]').click();
      await page.locator('[data-testid="general-history-filter-options"] button').first().waitFor();
    },
    chosen: (page) => page.locator('[data-testid="general-history-filter-field"]').innerText(),
    closes: true,
  },
];

/// P1 bis P3 für einen Dialog; liefert die Befunde als Text.
async function checkDialog(dialog, { initScript = null } = {}) {
  const state = stateNamed(dialog.state);
  const context = await browser.newContext({ viewport: { width: dialog.width, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  const found = [];
  const report = (inv, value) => found.push(`${dialog.name} @ ${dialog.width} px, ${inv}: ${value}`);
  try {
    await page.route("**/api/**", (route) => route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"nicht gemockt"}' }));
    await state.mock(page);
    if (dialog.mock) await dialog.mock(page);
    if (initScript) await page.addInitScript(initScript);
    await page.goto(`${baseUrl}/#${state.route}`, { waitUntil: "load" });
    if (dialog.state !== "kassenbuch-payee-auswahl-pc" && dialog.state !== "kassenbuch-erfassen-panel") await state.open(page, { width: dialog.width, mobile: false });
    await settle(page, { where: dialog.name });
    await dialog.open(page, state);
    await frames(page);
    const focus = () =>
      page.evaluate(() => {
        const el = document.activeElement;
        return {
          tag: el?.tagName ?? null,
          current: el?.getAttribute("aria-current") === "true",
          active: el?.getAttribute("aria-activedescendant") ?? null,
          activeExists: !!(el?.getAttribute("aria-activedescendant") && document.getElementById(el.getAttribute("aria-activedescendant"))),
          text: (el?.textContent ?? "").replace(/\s+/g, " ").trim(),
        };
      });
    // P1: Fokus in der Suche, bei der kurzen Auswahl auf der gewählten Option.
    const opened = await focus();
    if (dialog.short ? !(opened.tag === "BUTTON" && opened.current) : opened.tag !== "INPUT") report("P1", `der Fokus steht auf ${opened.tag}${dialog.short ? " ohne aria-current" : ""}`);
    // P2: ↓ markiert eine Option.
    await page.keyboard.press("ArrowDown");
    await frames(page);
    const marked = await focus();
    if (dialog.short ? marked.text === opened.text || marked.tag !== "BUTTON" : !marked.activeExists) report("P2", `↓ markiert keine Option (${JSON.stringify(marked)})`);
    // P3: Enter wählt genau einmal.
    const before = await dialog.chosen(page);
    await page.keyboard.press("Enter");
    await frames(page);
    await frames(page);
    if (dialog.toggles) {
      const now = await dialog.chosen(page);
      if (now !== before + 1) report("P3", `Enter schaltete ${now - before} statt genau einer Option`);
    } else if (dialog.closes) {
      const stillOpen = dialog.short
        ? await page.locator("[data-short-choice]").count()
        : await page.locator('[role="listbox"]').locator("visible=true").count();
      if (stillOpen > 0) report("P3", "Enter wählte nichts, der Dialog steht noch");
      // ↓ markierte eine andere Option als die gewählte: nach Enter steht sie im Feld.
      if (dialog.short && (await dialog.chosen(page)) === before) report("P3", `Enter übernahm die markierte Option nicht (${JSON.stringify(before)})`);
    }
  } catch (error) {
    report("Fehler", error.message.split("\n")[0]);
  } finally {
    await context.close();
  }
  return found;
}

test("[fokus] Auswahl-Dialoge: Fokus beim Öffnen in der Suche (kurze Auswahl auf der gewählten Option), ↓ markiert, Enter wählt genau einmal", async () => {
  const found = [];
  for (const dialog of DIALOGS) found.push(...(await checkDialog(dialog)));
  assert.deepEqual(found, []);
});

test("[fokus] Gegenbeweis P1: ein Fokus, der beim Öffnen nicht ankommt, meldet jeden Dialog", async () => {
  const noFocus = `document.addEventListener("focusin", (e) => {
    if (e.target instanceof HTMLElement && e.target.closest('[role="listbox"], [data-short-choice], [role="dialog"], .filter-popover') ) e.target.blur();
    if (e.target instanceof HTMLInputElement && e.target.getAttribute("aria-controls")) e.target.blur();
  }, true);`;
  for (const dialog of DIALOGS) {
    const found = await checkDialog(dialog, { initScript: noFocus });
    assert.ok(found.some((f) => f.includes("P1")), `${dialog.name}: P1 gemeldet\n${found.join("\n")}`);
  }
});
