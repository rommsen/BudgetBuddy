// scripts/key-invariants.mjs
//
// Der Tasten-Sweep (design-system-gcpws, Styleguide §7.8 und §7.9): jede Liste mit Panel bei 1024 und 1440 px, für
// jede dieselben Invarianten. Die Regel selbst prüft .NET als Aufzählung (`src/Tests/ListKeysTests.fs`); hier steht nur,
// was der echte DOM zeigt: ob die Taste am Dokument ankommt, ob ein Druck genau einen Schritt macht, ob Fokus,
// Ebenen und Scrollen stimmen.
//
// Invarianten (Bestandsaufnahme 2026-11-15, Abschnitt 5):
//   T1 Ohne Auswahl wählt ↓ die erste und ↑ die letzte Zeile; nach einem Mausklick auf eine Zeile (Fokus auf `body`)
//      wählt ↓ die nächste, ↑ die vorige, und das Panel zeigt sie.
//   T2 Ein Druck, ein Schritt, auch wenn `Tabs` den Fokus hat.
//   T3 Aus der Suche wirken ↑↓ auf die Liste, im Textfeld nicht; mit Alt, Strg oder Cmd nichts.
//   T4 Ein Zeichen ohne Feldfokus landet in der Suche, wo die Liste es so will; im Kassenbuch nicht (F9).
//   T5 Oberste Ebene zuerst: bei offenem Picker (mit der Tastatur geöffnet) ändert ↓ die Liste nicht; Esc schließt nur
//      den Picker, der Fokus steht danach auf dem Auslöser.
//   T6 Esc ohne Ebene: erst das Panel, die Auswahl bleibt; dann die Auswahl (ADR 0096).
//   T7 Die gewählte Zeile steht nach dem Schritt im Bild.
//   T8 Unter 1024 px gibt es keine Seitentasten (§7.8 „Wann nicht“): ↓ wählt nichts und öffnet kein Sheet.
//
// Auswahl-Dialoge (§7.9, eigener [fokus]-Test):
//   P1 Beim Öffnen mit feinem Zeiger steht der Fokus in der Suche (kurze Auswahl ohne Suche: auf der gewählten Option).
//   P2 ↓ markiert eine Option (`aria-activedescendant` zeigt auf sie, bzw. der Fokus wandert).
//   P3 Enter wählt genau einmal.

import { heightFor, settle } from "./layout-invariants.mjs";
import { stateNamed } from "./layout-states.mjs";

/// Breiten des Sweeps: 1024 (Kompaktliste, Karten) und 1440 (Tabellen).
export const KEY_WIDTHS = [1024, 1440];
/// Unter 1024 px (Rail, ohne Panel) für T8.
export const NO_PANEL_WIDTH = 900;

const frames = (page) => page.evaluate(() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))));

/// Wartet, bis `read()` etwas anderes als `before` liefert, höchstens kurz; liefert den letzten Wert. Eine Taste ohne
/// Wirkung ändert nichts, dann zählt der Wert nach der Wartezeit.
async function after(page, read, before, { ms = 600 } = {}) {
  const until = Date.now() + ms;
  let value = await read(page);
  while (value === before && Date.now() < until) {
    await frames(page);
    value = await read(page);
  }
  // Noch zwei Bilder: ein zweiter Schritt aus demselben Druck käme jetzt.
  await frames(page);
  return read(page);
}

const textOf = (locator) => locator.evaluateAll((els) => els.map((el) => (el.getAttribute("aria-label") || el.textContent || "").replace(/\s+/g, " ").trim()));
const plainTextOf = (locator) => locator.evaluateAll((els) => els.map((el) => (el.textContent || "").replace(/\s+/g, " ").trim()));
const focusInside = (page, selector) => page.evaluate((s) => !!document.activeElement?.closest(s), selector);

// ============================================================
// Die Listen
// ============================================================

/// Eine Liste mit Panel: `state` aus `layout-states.mjs` (Route und Mocks), `rows(page)` die sichtbaren Zeilen in
/// Reihenfolge, `current(page)` die gewählte Zeile (oder null), `click(page, name)` ein Mausklick auf die Zeile,
/// `panelShows(page)` was das Panel zeigt (null ohne Ding). Optional: `search` (Selektor des Suchfelds),
/// `typesIntoSearch`, `textField` (öffnet ein Feld, in dem die Pfeile dem Cursor gehören), `tabs` (Tab-Leiste im
/// Panel), `layer` (öffnet einen Picker, liefert den Auslöser), `selection` (Mehrfachauswahl: wählt eine Zeile per
/// Kästchen, zählt die gewählten), `arrows: false` (die Liste belegt die Pfeile nicht, F5).
export const LISTS = [
  {
    name: "kassenbuch",
    state: "kassenbuch",
    // Die Kompaktliste mit Maus hat auch unter 1024 px Tasten (ledger-compact-mouse.test.mjs), T8 gilt hier nicht.
    keysWithoutPanel: true,
    currentRow: '[data-keyboard-focus="true"]',
    rows: (page) => textOf(page.getByRole("checkbox", { name: /auswählen$/ }).locator("visible=true")).then((names) => names.filter((n) => !n.startsWith("Alle")).map((n) => n.replace(/ auswählen$/, ""))),
    current: (page) =>
      page.evaluate(() => {
        const row = [...document.querySelectorAll('[data-keyboard-focus="true"]')].find((el) => el.offsetParent !== null);
        const box = row?.querySelector('[role="checkbox"]');
        return box ? box.getAttribute("aria-label").replace(/ auswählen$/, "") : null;
      }),
    click: async (page, name) => {
      const tableRow = page.locator("table tbody tr", { hasText: name }).locator("visible=true");
      if ((await tableRow.count()) > 0) await tableRow.first().locator("td").nth(2).click();
      else await page.getByText(name, { exact: true }).locator("visible=true").first().click();
    },
    panelShows: (page) =>
      page.evaluate(() => {
        const panel = document.querySelector('[data-testid="ledger-detail-panel"]');
        const label = panel?.getAttribute("aria-label") ?? "";
        return label.startsWith("Detail ") ? label.slice("Detail ".length) : null;
      }),
    // Ohne offenes Detail markiert ↓ nur die Zeile, das Panel folgt erst einem geöffneten Detail (ADR 0096).
    panelFollowsArrows: "after-click",
    search: '[data-testid="ledger-search"] input',
    typesIntoSearch: false,
    // Die Leertaste wählt die markierte Zeile, deren Detail offen ist (§7.7); die Leiste steht fest unten.
    selection: {
      choose: (page) => page.keyboard.press(" "),
      count: (page) =>
        page.evaluate(() => {
          const bar = [...document.querySelectorAll(".selection-bar")].find((el) => el.checkVisibility());
          const match = bar?.innerText.match(/(\d+) ausgewählt/);
          return match ? Number(match[1]) : 0;
        }),
    },
  },
  {
    name: "regeln",
    state: "regeln",
    currentRow: '[data-testid="rule-row"][aria-current="true"]',
    rows: (page) => textOf(page.locator('[data-testid="rule-row"]:visible button[aria-label^="Regel bearbeiten: "], [data-testid="rule-row"]:visible[aria-label^="Regel bearbeiten: "]')).then((names) => names.map((n) => n.replace(/^Regel bearbeiten: /, ""))),
    current: (page) =>
      page.evaluate(() => {
        const row = [...document.querySelectorAll('[data-testid="rule-row"][aria-current="true"]')].find((el) => el.offsetParent !== null);
        const label = (row?.querySelector('[aria-label^="Regel bearbeiten: "]') ?? row)?.getAttribute("aria-label");
        return label ? label.replace(/^Regel bearbeiten: /, "") : null;
      }),
    click: (page, name) => page.locator('[data-testid="rule-row"]:visible').filter({ hasText: name }).first().click(),
    panelShows: (page) => page.evaluate(() => (document.querySelector('[data-testid="rule-form"]') ? "Regel" : null)),
    panelFollowsArrows: "always",
    textField: async (page) => {
      const field = page.locator('[data-testid="rule-form"] input[type="text"], [data-testid="rule-form"] input:not([type])').first();
      await field.focus();
      return field;
    },
    layer: {
      open: async (page) => {
        const trigger = page.locator('[data-testid="rule-category"]');
        await trigger.focus();
        await page.keyboard.press("Enter");
        await page.locator('[role="option"]').first().waitFor();
        return trigger;
      },
      isOpen: (page) => page.locator('[role="listbox"]').filter({ visible: true }).count().then((n) => n > 0),
    },
  },
  {
    name: "budget",
    state: "budget",
    currentRow: '[data-testid="budget-category-row"][aria-current="true"]',
    rows: (page) => plainTextOf(page.locator('[data-testid="budget-category-row"]:visible [data-testid="budget-category-name"]')),
    current: (page) =>
      page.evaluate(() => {
        const row = [...document.querySelectorAll('[data-testid="budget-category-row"][aria-current="true"]')].find((el) => el.offsetParent !== null);
        return row?.querySelector('[data-testid="budget-category-name"]')?.textContent.replace(/\s+/g, " ").trim() ?? null;
      }),
    click: (page, name) => page.locator('[data-testid="budget-category-row"]:visible').filter({ has: page.locator('[data-testid="budget-category-name"]', { hasText: name }) }).first().click(),
    panelShows: (page) => page.locator('aside[data-testid="budget-inspector"] h2').first().textContent({ timeout: 300 }).catch(() => null).then((t) => t?.trim() ?? null),
    panelFollowsArrows: "always",
    search: '[data-testid="budget-category-search"] input',
    typesIntoSearch: true,
    tabs: {
      tablist: '[data-testid="budget-inspector"] [role="tablist"]',
      selected: (page) => page.locator('[data-testid="budget-inspector"] [role="tab"][aria-selected="true"]').first().textContent().then((t) => t.trim()),
    },
    textField: async (page) => {
      const field = page.locator('[data-testid="assign-field"] input:visible').first();
      await field.focus();
      return field;
    },
    // Ein Klick auf einen Filter lässt den Fokus auf dem Knopf: ↓ wählt trotzdem die erste Zeile.
    pageButton: (page) => page.locator('[data-testid="budget-filter"] button').first(),
    layers: [
      {
        name: "Targets anwenden",
        open: async (page) => {
          const trigger = page.locator('[data-testid="month-start-apply"]:visible');
          await trigger.click();
          await page.locator('[data-testid="targets-total"]:visible').waitFor();
          return trigger;
        },
        isOpen: (page) => page.locator('[data-testid="targets-total"]').filter({ visible: true }).count().then((n) => n > 0),
        // Geprüft wird hier nur, dass die Liste darunter ruht: `Modal.fs` schließt bei Esc ohne Fokus im Dialog nicht
        // (`CloseOnEscape` ohne Wirkung) und führt den Fokus nicht zurück, Follow-up design-system-m4e7q.
        escape: false,
      },
    ],
    layer: {
      open: async (page) => {
        await page.locator('aside[data-testid="budget-inspector"]').getByRole("button", { name: "Abgeben", exact: true }).click();
        const trigger = page.locator('[data-testid="transfer-to"]:visible');
        await trigger.focus();
        await page.keyboard.press("Enter");
        await page.locator('[role="option"]:visible').first().waitFor();
        return trigger;
      },
      isOpen: (page) => page.locator('[role="listbox"]').filter({ visible: true }).count().then((n) => n > 0),
    },
  },
  {
    name: "kategorien",
    state: "kategorien",
    rows: (page) => textOf(page.locator('[data-testid="categories-row-name"]:visible')).then((names) => names.map((n) => n.replace(/ bearbeiten$/, ""))),
    current: (page) => page.locator('[data-testid="categories-panel"] input[type="text"], [data-testid="categories-panel"] input:not([type])').first().inputValue({ timeout: 300 }).catch(() => null),
    click: (page, name) => page.locator('[data-testid="categories-row-name"]:visible').filter({ hasText: name }).first().click(),
    panelShows: (page) => page.locator('[data-testid="categories-panel"]').count().then((n) => (n > 0 ? "Kategorie" : null)),
    panelFollowsArrows: "always",
    // G13: die Auswahl der Gruppe ist die oberste Ebene über dem Panel; Esc schließt nur sie, der Fokus geht aufs Feld.
    layer: {
      name: "Gruppe",
      open: async (page) => {
        const trigger = page.locator('[data-testid="categories-panel"] [data-testid="categories-group-field"]');
        await trigger.focus();
        await page.keyboard.press("Enter");
        await page.locator('[data-testid="categories-group-choice"] button').first().waitFor();
        return trigger;
      },
      isOpen: (page) => page.locator('[data-testid="categories-group-choice"]').filter({ visible: true }).count().then((n) => n > 0),
    },
    textField: async (page) => {
      const field = page.locator('[data-testid="categories-panel"] input[type="text"], [data-testid="categories-panel"] input:not([type])').first();
      await field.focus();
      return field;
    },
  },
  {
    name: "targets-pruefen",
    state: "targets-pruefen",
    // F5: die Seite belegt nur Esc; ein Panel öffnet „Anpassen …“.
    arrows: false,
    rows: (page) => textOf(page.locator('[data-testid="target-check-row"]:visible')),
    current: () => Promise.resolve(null),
    open: async (page) => {
      await page.locator('[data-testid="target-check-row"]').filter({ hasText: "Essen" }).getByRole("button", { name: "Anpassen …" }).click();
      await page.locator('[data-testid="target-check-panel"]').waitFor();
    },
    panelShows: (page) => page.locator('[data-testid="target-check-panel"]').count().then((n) => (n > 0 ? "Anpassen" : null)),
  },
];

export const listNamed = (name) => LISTS.find((l) => l.name === name);

// ============================================================
// Läufer
// ============================================================

/// Eine Liste bei einer Breite: je Prüfung eine frische Seite. `initScript` nur für Gegenbeweise (bricht etwas, bevor
/// die App lädt). Liefert die Verletzungen `{ list, width, inv, key, value }`.
export async function runList(browser, baseUrl, list, width, { initScript = null, checks = null } = {}) {
  const state = stateNamed(list.state);
  const found = [];
  const want = (inv) => (checks ?? ["T1", "T2", "T3", "T4", "T5", "T6", "T7"]).includes(inv);
  const report = (inv, key, value) => found.push({ list: list.name, width, inv, key, value });

  const fresh = async (fn) => {
    const context = await browser.newContext({ viewport: { width, height: heightFor(width) } });
    const page = await context.newPage();
    page.setDefaultTimeout(15000);
    try {
      await page.route("**/api/**", (route) => route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"im Sweep nicht gemockt"}' }));
      await state.mock(page);
      if (initScript) await page.addInitScript(initScript);
      await page.goto(`${baseUrl}/#${state.route}`, { waitUntil: "load" });
      await state.open(page, { width, mobile: false });
      await settle(page, { where: `${list.name} @ ${width} px` });
      await fn(page);
    } finally {
      await context.close();
    }
  };
  const press = async (page, key, read = list.current) => {
    const before = await read(page);
    await page.keyboard.press(key);
    return after(page, read, before);
  };

  if (list.arrows === false) {
    // Nur Esc (F5): ↓ wählt nichts, Esc schließt das Panel.
    if (want("T6"))
      await fresh(async (page) => {
        await list.open(page);
        await page.keyboard.press("ArrowDown");
        await frames(page);
        if ((await list.panelShows(page)) === null) report("T6", "ArrowDown", "↓ schloss das Panel, die Seite belegt die Pfeile nicht");
        const shown = await press(page, "Escape", list.panelShows);
        if (shown !== null) report("T6", "Escape", `das Panel bleibt offen (${shown})`);
      });
    return found;
  }

  // T1 und T2: ohne Auswahl ↓ die erste, ↑ die letzte; nach Klick ↓ die nächste, ↑ die vorige, je genau ein Schritt.
  if (want("T1") || want("T2"))
    await fresh(async (page) => {
      const rows = await list.rows(page);
      if (rows.length < 3) throw new Error(`${list.name}: zu wenig Zeilen (${rows.length})`);
      const first = await press(page, "ArrowDown");
      if (first !== rows[0]) report("T1", "ArrowDown", `ohne Auswahl ${JSON.stringify(first)} statt der ersten Zeile ${JSON.stringify(rows[0])}`);
      await list.click(page, rows[1]);
      await after(page, list.current, first);
      await page.evaluate(() => document.activeElement?.blur());
      const next = await press(page, "ArrowDown");
      if (next !== rows[2]) report(next === rows[3] ? "T2" : "T1", "ArrowDown", `nach Klick auf ${JSON.stringify(rows[1])} steht ${JSON.stringify(next)} statt ${JSON.stringify(rows[2])}`);
      const shown = await list.panelShows(page);
      if (shown === null) report("T1", "ArrowDown", "das Panel zeigt nach dem Schritt nichts");
      const back = await press(page, "ArrowUp");
      if (back !== rows[1]) report(back === rows[0] ? "T2" : "T1", "ArrowUp", `zurück steht ${JSON.stringify(back)} statt ${JSON.stringify(rows[1])}`);
    });
  if (want("T1"))
    await fresh(async (page) => {
      const rows = await list.rows(page);
      const last = await press(page, "ArrowUp");
      if (last !== rows[rows.length - 1]) report("T1", "ArrowUp", `ohne Auswahl ${JSON.stringify(last)} statt der letzten Zeile ${JSON.stringify(rows[rows.length - 1])}`);
    });

  if (want("T1") && list.pageButton)
    await fresh(async (page) => {
      await list.pageButton(page).click();
      await frames(page);
      const rows = await list.rows(page);
      const first = await press(page, "ArrowDown");
      if (first !== rows[0]) report("T1", "ArrowDown", `mit Fokus auf einem Knopf der Seite ${JSON.stringify(first)} statt ${JSON.stringify(rows[0])}`);
    });

  // T2 mit Fokus auf den Tabs: ein Druck auf → wechselt genau einen Tab, die Zeile bleibt.
  if (want("T2") && list.tabs)
    await fresh(async (page) => {
      const rows = await list.rows(page);
      await list.click(page, rows[0]);
      const tabs = page.locator(list.tabs.tablist).first();
      await tabs.waitFor();
      const tabNames = await textOf(tabs.locator('[role="tab"]'));
      await tabs.locator('[role="tab"]').first().focus();
      const read = list.tabs.selected;
      const before = await read(page);
      const now = await press(page, "ArrowRight", read);
      const expected = tabNames[tabNames.indexOf(before) + 1];
      if (now !== expected) report("T2", "ArrowRight", `mit Fokus auf den Tabs steht ${JSON.stringify(now)} statt ${JSON.stringify(expected)}`);
      if ((await list.current(page)) !== rows[0]) report("T2", "ArrowRight", "→ auf den Tabs wechselte die Zeile");
    });

  // T3: aus der Suche wirken ↑↓, im Textfeld nicht, mit Alt, Strg oder Cmd nichts.
  if (want("T3"))
    await fresh(async (page) => {
      const rows = await list.rows(page);
      if (list.search) {
        await page.locator(list.search).locator("visible=true").first().focus();
        const fromSearch = await press(page, "ArrowDown");
        if (fromSearch !== rows[0]) report("T3", "ArrowDown", `aus der Suche ${JSON.stringify(fromSearch)} statt ${JSON.stringify(rows[0])}`);
        if (!(await focusInside(page, list.search.replace(/ input$/, "")))) report("T3", "ArrowDown", "der Fokus verließ die Suche");
        if (list.tabs) {
          // Aus der Suche wechselt → den Tab, der Cursor steht still (Bug Roman 2026-11-03).
          const field = page.locator(list.search).locator("visible=true").first();
          await field.type("e");
          const caret = await field.evaluate((el) => el.selectionStart);
          const tabNames = await textOf(page.locator(list.tabs.tablist).first().locator('[role="tab"]'));
          const before = await list.tabs.selected(page);
          const now = await press(page, "ArrowRight", list.tabs.selected);
          if (now !== tabNames[tabNames.indexOf(before) + 1]) report("T3", "ArrowRight", `aus der Suche steht der Tab ${JSON.stringify(now)}`);
          if ((await field.evaluate((el) => el.selectionStart)) !== caret) report("T3", "ArrowRight", "der Cursor in der Suche wanderte");
          await press(page, "ArrowLeft", list.tabs.selected);
          await field.fill("");
        }
        if (list.selection) {
          await page.keyboard.press("Shift+ArrowDown");
          await frames(page);
          if ((await list.selection.count(page)) !== 2) report("T3", "Shift+ArrowDown", `aus der Suche sind ${await list.selection.count(page)} statt 2 gewählt`);
        }
        await page.evaluate(() => document.activeElement?.blur());
        if (list.selection) {
          await page.keyboard.press("Escape");
          await page.keyboard.press("Escape");
          await frames(page);
        }
      }
      await list.click(page, rows[1]);
      await after(page, list.current, null);
      await page.evaluate(() => document.activeElement?.blur());
      for (const key of ["Alt+ArrowDown", "Control+ArrowDown", "Meta+ArrowDown"]) {
        const now = await press(page, key);
        if (now !== rows[1]) report("T3", key, `mit Zusatztaste wechselte die Zeile auf ${JSON.stringify(now)}`);
      }
      if (list.tabs) {
        // Alt+← gehört dem Browser (zurück), der Tab bleibt.
        await page.keyboard.press("ArrowRight");
        await frames(page);
        const tab = await list.tabs.selected(page);
        const now = await press(page, "Alt+ArrowLeft", list.tabs.selected);
        if (now !== tab) report("T3", "Alt+ArrowLeft", `mit Alt wechselte der Tab auf ${JSON.stringify(now)}`);
        await press(page, "ArrowLeft", list.tabs.selected);
      }
      if (list.textField) {
        await list.textField(page);
        const now = await press(page, "ArrowDown");
        if (now !== rows[1]) report("T3", "ArrowDown", `im Textfeld wechselte die Zeile auf ${JSON.stringify(now)}`);
      }
    });

  // T4: ein Zeichen ohne Feldfokus geht in die Suche, wo die Liste es so will (im Kassenbuch nicht, F9).
  if (want("T4") && list.search)
    await fresh(async (page) => {
      await page.evaluate(() => document.activeElement?.blur());
      await page.keyboard.press("e");
      await frames(page);
      const field = page.locator(list.search).locator("visible=true").first();
      const value = await field.inputValue();
      const focused = await focusInside(page, list.search.replace(/ input$/, ""));
      if (list.typesIntoSearch && (!focused || value !== "e")) report("T4", "e", `die Suche hat ${focused ? "" : "keinen Fokus und "}den Wert ${JSON.stringify(value)}`);
      if (!list.typesIntoSearch && (focused || value !== "")) report("T4", "e", `das Zeichen landete in der Suche (${JSON.stringify(value)}), F9`);
      if (!list.typesIntoSearch) {
        // „/“ setzt den Fokus in die Suche, das Zeichen steht nicht darin.
        await page.keyboard.press("/");
        await frames(page);
        if (!(await focusInside(page, list.search.replace(/ input$/, ""))) || (await field.inputValue()) !== "") report("T4", "/", "„/“ setzt den Fokus nicht in eine leere Suche");
      }
    });

  // T5: oberste Ebene zuerst, für jede Ebene der Liste.
  // Regressionstest (Test-Doktrin Punkt 8): Esc im CategoryPicker schloss die Auswahl, der Fokus fiel aber auf `body`
  // statt auf den Auslöser zurück (§7.8 Punkt 6). Fix in 86ceeb32 (`returnTo` in `DesignSystem/CategoryPicker.fs`);
  // ohne den Rücksprung meldet die letzte Prüfung unten „der Fokus steht nicht auf dem Auslöser“.
  for (const layer of [list.layer, ...(list.layers ?? [])].filter(Boolean)) {
    if (!want("T5")) break;
    await fresh(async (page) => {
      const rows = await list.rows(page);
      await list.click(page, rows[0]);
      await after(page, list.current, null);
      const trigger = await layer.open(page);
      const now = await press(page, "ArrowDown");
      if (now !== rows[0]) report("T5", "ArrowDown", `bei offener Ebene ${layer.name ?? ""} wechselte die Zeile auf ${JSON.stringify(now)}`);
      if (layer.escape === false) return;
      await page.keyboard.press("Escape");
      await frames(page);
      await frames(page);
      if (await layer.isOpen(page)) report("T5", "Escape", `die Ebene ${layer.name ?? ""} bleibt offen`);
      if ((await list.panelShows(page)) === null) report("T5", "Escape", "Esc schloss mit der Ebene auch das Panel");
      {
        const onTrigger = await trigger.evaluate((el) => el === document.activeElement || el.contains(document.activeElement));
        if (!onTrigger) report("T5", "Escape", `der Fokus steht nicht auf dem Auslöser, sondern auf ${await page.evaluate(() => document.activeElement?.tagName)}`);
      }
    });
  }

  // T6: Esc ohne Ebene schließt erst das Panel (die Auswahl bleibt), dann die Auswahl.
  if (want("T6"))
    await fresh(async (page) => {
      const rows = await list.rows(page);
      await list.click(page, rows[0]);
      await after(page, list.panelShows, null);
      if (list.selection) {
        await page.evaluate(() => document.activeElement?.blur());
        await list.selection.choose(page);
        await frames(page);
        if ((await list.selection.count(page)) !== 1) {
          report("T6", " ", "die Leertaste wählte die Zeile mit offenem Detail nicht");
          return;
        }
      }
      if ((await list.panelShows(page)) === null) throw new Error(`${list.name}: das Panel zeigt nach dem Klick nichts`);
      const shown = await press(page, "Escape", list.panelShows);
      if (shown !== null) report("T6", "Escape", `das Panel zeigt weiter ${JSON.stringify(shown)}`);
      if (list.selection) {
        if ((await list.selection.count(page)) !== 1) report("T6", "Escape", "das erste Esc hob die Auswahl auf, das Panel kommt zuerst (ADR 0096)");
        await page.keyboard.press("Escape");
        await frames(page);
        if ((await list.selection.count(page)) !== 0) report("T6", "Escape", "das zweite Esc hob die Auswahl nicht auf");
      }
    });

  // T7: nach vielen Schritten steht die gewählte Zeile im Bild.
  if (want("T7"))
    await fresh(async (page) => {
      const rows = await list.rows(page);
      let now = null;
      for (let i = 0; i < Math.min(rows.length, 14); i++) now = await press(page, "ArrowDown");
      if (!list.currentRow) return;
      // Das Scrollen darf weich sein: gemessen wird, wenn die Seite fünf Bilder lang steht.
      await page.evaluate(
        () =>
          new Promise((done) => {
            let last = -1;
            let still = 0;
            let left = 120;
            const tick = () => {
              const y = window.scrollY;
              still = y === last ? still + 1 : 0;
              last = y;
              if (still >= 5 || --left <= 0) done();
              else requestAnimationFrame(tick);
            };
            requestAnimationFrame(tick);
          })
      );
      const visible = await page.evaluate((selector) => {
        const row = [...document.querySelectorAll(selector)].find((el) => el.offsetParent !== null);
        if (!row) return null;
        const r = row.getBoundingClientRect();
        return r.top >= 0 && r.bottom <= window.innerHeight;
      }, list.currentRow);
      if (visible === false) report("T7", "ArrowDown", `die Zeile ${JSON.stringify(now)} steht nicht im Bild`);
    });

  return found;
}

/// T8: unter 1024 px wirken die Seitentasten nicht (§7.8 „Wann nicht“), ↓ wählt nichts und öffnet kein Sheet.
export async function runWithoutPanel(browser, baseUrl, list, { initScript = null } = {}) {
  const state = stateNamed(list.state);
  const width = NO_PANEL_WIDTH;
  const context = await browser.newContext({ viewport: { width, height: heightFor(width) } });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  const found = [];
  try {
    await page.route("**/api/**", (route) => route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"im Sweep nicht gemockt"}' }));
    await state.mock(page);
    if (initScript) await page.addInitScript(initScript);
    await page.goto(`${baseUrl}/#${state.route}`, { waitUntil: "load" });
    await state.open(page, { width, mobile: false });
    await settle(page, { where: `${list.name} @ ${width} px` });
    await page.keyboard.press("ArrowDown");
    await frames(page);
    await frames(page);
    const sheets = await page.locator(".bottom-sheet.active").count();
    const current = await list.current(page);
    if (sheets > 0 || current !== null)
      found.push({ list: list.name, width, inv: "T8", key: "ArrowDown", value: sheets > 0 ? "ein Sheet öffnete sich" : `die Zeile ${JSON.stringify(current)} ist gewählt` });
  } finally {
    await context.close();
  }
  return found;
}

/// Alle Listen bei allen Breiten.
export async function keySweep(browser, baseUrl, lists = LISTS, { concurrency = 4, initScript = null, checks = null } = {}) {
  const jobs = lists.flatMap((list) => [...KEY_WIDTHS, ...(list.arrows === false || list.keysWithoutPanel ? [] : [NO_PANEL_WIDTH])].map((width) => ({ list, width })));
  const violations = [];
  const errors = [];
  let next = 0;
  const worker = async () => {
    while (next < jobs.length) {
      const { list, width } = jobs[next++];
      try {
        if (width === NO_PANEL_WIDTH) {
          if (!checks || checks.includes("T8")) violations.push(...(await runWithoutPanel(browser, baseUrl, list, { initScript })));
        } else violations.push(...(await runList(browser, baseUrl, list, width, { initScript, checks })));
      } catch (error) {
        errors.push(`${list.name} @ ${width}: ${error.message.split("\n")[0]}`);
      }
    }
  };
  await Promise.all(Array.from({ length: concurrency }, worker));
  return { violations, errors, runs: jobs.length };
}

export const formatKeyViolation = (v) => `${v.list} @ ${v.width} px, ${v.inv}, Taste ${v.key}: ${v.value}`;
