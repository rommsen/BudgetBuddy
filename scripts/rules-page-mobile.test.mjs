// scripts/rules-page-mobile.test.mjs
//
// Wächter für `categorization-cgq35`: die Regelseite am Handy und per Tastatur. Gemessen am
// echten Fable-Client gegen eine gemockte API. Verhindert: Prioritätspfeile mit 24 mal 20 px,
// ein Menü, das nur über Fokus aufgeht und dessen Einträge nicht auf Enter reagieren, und den
// Regel-Dialog als zentriertes Modal mit Dropdown am Handy. Für `categorization-f955d`: Zeilen
// per Tastatur, der Ghost-Click nach dem Menü im Sheet, der echte Dateiinhalt beim Import, die
// Namen der Icon-Knöpfe und die Texte im gerenderten Dialog.
//
// infrastructure-t5m2b: hier bleibt, was nur der Browser misst (Tastatur und Fokus im echten DOM,
// Ghost-Click, Dateiauswahl, gerenderte Texte) und je Fluss ein Happy Path. Trefferflächen, Menü im
// Sheet und Popover im Dialog prüft der Layout-Sweep (Zustände regeln, regeln-menue, regeln-dialog-picker). Die
// Reihenfolge nach einem Pfeil prüft `RulesReorderTests.fs`, Menü und Formular `RulesListTests.fs`,
// die Rückmeldung des Mustertests samt Netzfehler `RulesListTests.fs` ("Muster testen im Regel-Dialog").
//
// Ausführen: npm run test:scripts -- scripts/rules-page-mobile.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import { settle } from "./layout-invariants.mjs";

let frontend;
let frontendPort;
let browser;

const guid = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, "0")}`;
const W = (c, v) => ({ [c]: v });
const FIXED = "2026-01-01T00:00:00.0000000Z";

const rule = (n, name, categoryNumber) => ({
  Id: W("RuleId", guid(n)),
  Name: name,
  Pattern: `muster${n}`,
  PatternType: "Contains",
  TargetField: "Combined",
  CategoryId: W("YnabCategoryId", guid(categoryNumber)),
  CategoryName: `Kategorie ${categoryNumber}`,
  PayeeOverride: null,
  Priority: 10 - n,
  Enabled: true,
  CreatedAt: FIXED,
  UpdatedAt: FIXED,
});

const category = (n) => ({
  Id: W("YnabCategoryId", guid(n)),
  Name: `Kategorie ${n}`,
  GroupName: "Alltag",
  Available: null,
  Hidden: false,
});

const settings = {
  Comdirect: null,
  Ynab: { HasToken: true, DefaultBudgetId: W("YnabBudgetId", "b1"), DefaultAccountId: null, QuickAddAccountId: null },
  Sync: { DaysToFetch: 14 },
};

async function openPage(width, height) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = { getAllRules: 0, reorder: [], create: [], update: [], exportRules: 0, importRules: [] };
  const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/SettingsApi/getSettings", (r) => json(r, settings));
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => json(r, { SavedColor: null, SuggestedColor: null }));
  await page.route("**/api/YnabApi/getCategories", (r) => json(r, { Ok: Array.from({ length: 12 }, (_, i) => category(i + 1)) }));
  await page.route("**/api/RulesApi/getAllRules", (r) => {
    calls.getAllRules += 1;
    json(r, [rule(1, "Regel eins", 1), rule(2, "Regel zwei", 2)]);
  });
  await page.route("**/api/RulesApi/reorderRules", (r) => {
    calls.reorder.push(JSON.parse(r.request().postData()));
    json(r, { Ok: null });
  });
  await page.route("**/api/RulesApi/createRule", (r) => {
    calls.create.push(JSON.parse(r.request().postData()));
    json(r, { Ok: rule(3, "Neue Regel", 3) });
  });
  await page.route("**/api/RulesApi/updateRule", (r) => {
    calls.update.push(JSON.parse(r.request().postData()));
    json(r, { Ok: rule(1, "Regel eins", 3) });
  });
  await page.route("**/api/RulesApi/exportRules", (r) => {
    calls.exportRules += 1;
    json(r, "[]");
  });
  await page.route("**/api/RulesApi/importRules", (r) => {
    calls.importRules.push(r.request().postData());
    json(r, { Ok: 2 });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/rules`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/rules");
  await page.getByText("Regel eins").locator("visible=true").first().waitFor();
  // Gemessen wird erst nach dem Seiteneintritt (`pageEnter` verschiebt die Seite per transform). Während der
  // Animation rechnet Chromium die Box in float32: liegen Ober- und Unterkante eines 48-px-Knopfs beiderseits
  // von 128 oder 256 px, meldet boundingBox 47,99998 (infrastructure-nx14s, Messregel des Layout-Sweeps).
  await settle(page);
  return { context, page, calls };
}

const importFile = '{"quelle":"regeln-f955d"}';
const visibleRow = (page, name) => page.locator('[data-testid="rule-row"]:visible').filter({ hasText: name });
const menuButton = (page) => page.locator('[data-testid="rules-menu-button"] button');
const menuItems = (page) => page.locator('[role="menuitem"]');
const dialog = (page) => page.locator('[data-testid="rule-dialog"]');
const openSheets = (page) => page.locator(".bottom-sheet.active");

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

/// Jeder sichtbare Knopf ohne Text trägt ein aria-label (Bug categorization-f955d).
async function expectIconButtonsLabelled(page, width) {
  const iconButtons = await page.evaluate(() =>
    [...document.querySelectorAll("button")]
      .filter((el) => el.offsetParent !== null && el.textContent.trim() === "")
      .map((el) => ({ label: el.getAttribute("aria-label"), html: el.outerHTML.slice(0, 120) }))
  );
  assert.ok(iconButtons.length >= 3, `${width} px: Icon-Knöpfe gefunden: ${iconButtons.length}`);
  assert.deepEqual(iconButtons.filter((button) => !button.label), [], `${width} px`);
}

/// Die Texte im offenen Regel-Dialog sind deutsch, je Mustertyp. Am Ende steht wieder "Contains".
async function expectGermanDialogTexts(page) {
  const renderedTexts = () =>
    dialog(page).evaluate((el) => [
      el.innerText,
      ...[...el.querySelectorAll("input")].map((input) => input.placeholder),
      ...[...el.querySelectorAll("option")].map((option) => option.textContent),
    ]);
  const seen = [];
  for (const patternType of ["Exact", "Regex", "Contains"]) {
    await dialog(page).locator("select").first().selectOption(patternType);
    seen.push(...(await renderedTexts()));
  }
  const all = seen.join("\n");
  assert.ok(!all.includes("Override payee"), "englischer Platzhalter");
  assert.ok(!all.includes("z.B."), 'schmale Schreibweise "z.B."');
  assert.ok(!all.includes("..."), "drei Punkte statt Auslassungszeichen");
  for (const placeholder of ["z. B. amazon", "z. B. AMAZON MARKETPLACE", "z. B. AMAZON\\.DE", "z. B. Amazon Einkäufe", "Beispieltext eingeben…", "Empfänger in YNAB ersetzen"]) {
    assert.ok(seen.includes(placeholder), `Platzhalter fehlt: ${placeholder}`);
  }
  assert.equal(await dialog(page).getByRole("button", { name: "Testen", exact: true }).count(), 1, 'Knopf "Testen"');
  assert.equal(await dialog(page).getByRole("button", { name: "Test", exact: true }).count(), 0, 'Knopf "Test"');
}

test("[tastatur] PC: Enter öffnet das Menü mit sichtbarem Fokus-Ring, Tab erreicht jeden Eintrag, Enter löst aus, Esc und Klick außerhalb schließen", async () => {
  const { context, page, calls } = await openPage(1440, 900);
  try {
    assert.equal(await openSheets(page).count(), 0, "kein Sheet am PC");
    // Das Abfangen der Dateiauswahl steht vorn: direkt vor der Taste kam Chromes Bestätigung unter
    // Last erst nach dem keyDown, `filechooser` blieb dann aus.
    const chooserOpened = page.waitForEvent("filechooser");
    await menuButton(page).focus();
    assert.equal(await menuItems(page).count(), 0, "das Menü geht nicht allein über den Fokus auf");
    await page.keyboard.press("Enter");
    await menuItems(page).first().waitFor();
    const outline = await page.evaluate(() => {
      const style = getComputedStyle(document.activeElement);
      return { role: document.activeElement.getAttribute("role"), ring: style.outlineStyle !== "none" && document.activeElement.matches(":focus-visible") };
    });
    assert.deepEqual(outline, { role: "menuitem", ring: true }, "der Fokus-Ring ist sichtbar");
    const panel = await menuItems(page).last().boundingBox();
    assert.ok(panel && panel.x + panel.width <= 1440, "das Menü bleibt im Bild");

    // Esc schließt, ein zweiter Klick auf den Knopf ebenfalls, Klick außerhalb auch.
    await page.keyboard.press("Escape");
    await menuItems(page).first().waitFor({ state: "detached" });
    await menuButton(page).click();
    await menuItems(page).first().waitFor();
    await menuButton(page).click();
    await menuItems(page).first().waitFor({ state: "detached" });
    await menuButton(page).click();
    await menuItems(page).first().waitFor();
    await page.getByRole("heading", { name: "Kategorisierungsregeln" }).click();
    await menuItems(page).first().waitFor({ state: "detached" });

    // Neu laden: erster Eintrag hat den Fokus.
    await menuButton(page).focus();
    await page.keyboard.press("Enter");
    await menuItems(page).first().waitFor();
    assert.equal(await page.evaluate(() => document.activeElement?.textContent), "Regeln neu laden");
    const before = calls.getAllRules;
    await page.keyboard.press("Enter");
    await menuItems(page).first().waitFor({ state: "detached" });
    await page.waitForFunction(() => document.body.textContent.includes("Regel eins"));
    assert.equal(calls.getAllRules, before + 1);

    // Exportieren: ein Tab weiter.
    await menuButton(page).focus();
    await page.keyboard.press("Enter");
    await menuItems(page).first().waitFor();
    await page.keyboard.press("Tab");
    await Promise.all([page.waitForResponse("**/api/RulesApi/exportRules"), page.keyboard.press("Enter")]);
    await menuItems(page).first().waitFor({ state: "detached" });
    assert.equal(calls.exportRules, 1);

    // Importieren: noch ein Tab weiter, der Dateiinhalt geht unverändert an die API.
    await menuButton(page).focus();
    await page.keyboard.press("Enter");
    await menuItems(page).first().waitFor();
    await page.keyboard.press("Tab");
    await page.keyboard.press("Tab");
    await page.keyboard.press("Enter");
    const chooser = await chooserOpened;
    await chooser.setFiles({ name: "regeln.json", mimeType: "application/json", buffer: Buffer.from(importFile) });
    await page.getByText(/Imported 2 rule/).first().waitFor();
    assert.deepEqual(calls.importRules.map((body) => JSON.parse(body)), [[importFile]]);
  } finally {
    await context.close();
  }
});

test("[tastatur] PC: Enter und Leertaste auf einem Prioritätspfeil verschieben die Regel, weder sie noch ein Klick auf den gesperrten Pfeil öffnen den Dialog", async () => {
  const { context, page, calls } = await openPage(1440, 900);
  try {
    await visibleRow(page, "Regel eins").getByRole("button", { name: "Priorität senken" }).focus();
    await Promise.all([page.waitForResponse("**/api/RulesApi/reorderRules"), page.keyboard.press("Enter")]);
    await page.waitForFunction(() => document.querySelector('[data-testid="rule-row"]')?.textContent.includes("Regel zwei"));
    assert.equal(calls.reorder.length, 1);
    assert.equal(await dialog(page).count(), 0, "Enter öffnet den Dialog nicht");

    await visibleRow(page, "Regel eins").getByRole("button", { name: "Priorität erhöhen" }).focus();
    await Promise.all([page.waitForResponse("**/api/RulesApi/reorderRules"), page.keyboard.press("Space")]);
    assert.equal(calls.reorder.length, 2);
    assert.equal(await dialog(page).count(), 0, "die Leertaste öffnet den Dialog nicht");

    // Bug categorization-cgq35: auch ein Klick auf den gesperrten Pfeil fiel zur Zeile durch und
    // öffnete den Dialog. Ein gesperrter Knopf bekommt kein click, gewartet wird darum auf das
    // pointerup des Klicks und zwei Bilder danach, in denen ein durchgefallener Klick gerendert wäre.
    const first = visibleRow(page, "Regel eins");
    await page.waitForFunction(() => document.querySelector('[data-testid="rule-row"]')?.textContent.includes("Regel eins"));
    const clicked = page.evaluate(() => new Promise((done) => window.addEventListener("pointerup", () => requestAnimationFrame(() => requestAnimationFrame(done)), { once: true })));
    await first.getByRole("button", { name: "Priorität erhöhen" }).click({ force: true });
    await clicked;
    assert.equal(await dialog(page).count(), 0, "ein Klick auf den gesperrten Pfeil öffnet den Dialog nicht");
  } finally {
    await context.close();
  }
});

// Die Dialogtexte prüft der Happy Path mit, am gerenderten Dialog statt am Quelltext (Entscheidung
// des Owners, `categorization-f955d`; infrastructure-b3t9k aus einem eigenen Test hierher gezogen).
test("[flow:regel-anlegen] Handy: der Regel-Dialog ist ein BottomSheet mit deutschen Texten je Mustertyp, die Kategorie kommt aus dem Picker mit Suche, Anlegen speichert die Werte", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    await page.getByRole("button", { name: "Neue Regel" }).locator("visible=true").click();
    await dialog(page).waitFor();
    assert.equal(await page.locator('.bottom-sheet.active [data-testid="rule-dialog"]').count(), 1, "Dialog liegt im Sheet");
    assert.equal(await page.locator(".fixed.inset-0.z-50").count(), 0, "kein zentriertes Modal");
    await expectGermanDialogTexts(page);

    await dialog(page).locator("input").first().fill("Bäcker");
    await dialog(page).getByPlaceholder("z. B. amazon", { exact: true }).fill("baeckerei");
    await page.locator('[data-testid="rule-category"] button').click();
    const search = page.locator(".bottom-sheet.layer-2.active input");
    await search.fill("Kategorie 7");
    await page.locator('.bottom-sheet.layer-2 [role="option"]').filter({ hasText: "Kategorie 7" }).click();
    await page.locator(".bottom-sheet.layer-2.active").waitFor({ state: "detached" });
    assert.match(await page.locator('[data-testid="rule-category"]').innerText(), /Alltag: Kategorie 7/);

    // Das Picker-Sheet schluckt nach der Wahl 350 ms lang den nächsten Klick (ADR 0005): erneut
    // tippen, bis die Anfrage hinausgeht, höchstens 20 Mal.
    const create = page.getByRole("button", { name: "Regel erstellen" });
    for (let attempt = 0; attempt < 20 && calls.create.length === 0; attempt += 1) {
      await create.click();
      await page.waitForResponse("**/api/RulesApi/createRule", { timeout: 200 }).catch(() => {});
    }
    await page.locator(".bottom-sheet.active").waitFor({ state: "detached" });
    assert.equal(calls.create.length, 1);
    const [created] = calls.create[0];
    assert.equal(created.Name, "Bäcker");
    assert.equal(created.Pattern, "baeckerei");
    assert.ok(JSON.stringify(created.CategoryId).includes(guid(7)));
  } finally {
    await context.close();
  }
});

// Bug categorization-f955d: das Menü im Sheet schloss ohne Schutz vor dem Ghost-Click. Der
// Schutz darf aber auch den programmatischen Klick auf das Datei-Input nicht schlucken, sonst
// geht am Handy die Dateiauswahl nie auf. Ein später Klick nach Exportieren traf die Zeile hinter dem
// Sheet und öffnete den Dialog (aus dem früheren Layout-Test des Overflow-Knopfs hierher gezogen).
test("[flow:regeln-importieren] Handy: das Menü bleibt nach dem Tipp offen, Importieren aus dem Sheet öffnet die Dateiauswahl, Exportieren trifft die Zeile dahinter nicht", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    const chooserOpened = page.waitForEvent("filechooser");
    await menuButton(page).tap();
    await menuItems(page).first().waitFor();
    // Der Ghost-Click des Tipps fällt in `tap()`, zwei Bilder später steht das Menü noch.
    await page.evaluate(() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))));
    assert.equal(await menuItems(page).count(), 3, "das Menü bleibt nach dem Tipp offen");
    // Styleguide §7.1.3: Autofokus nur bei feinem Pointer.
    assert.equal(await page.evaluate(() => document.activeElement?.getAttribute("role") ?? null), null, "am Handy hat kein Eintrag den Fokus");
    await menuItems(page).filter({ hasText: "Regeln importieren" }).tap();
    const chooser = await chooserOpened;
    await chooser.setFiles({ name: "regeln.json", mimeType: "application/json", buffer: Buffer.from(importFile) });
    await page.getByText(/Imported 2 rule/).first().waitFor();
    assert.deepEqual(calls.importRules.map((body) => JSON.parse(body)), [[importFile]]);

    await openSheets(page).first().waitFor({ state: "detached" });
    // Der Schutz nach Importieren schluckt bis zu 350 ms lang einen Klick (ADR 0005). Gewartet wird, bis ein
    // Probeklick wieder ankommt: ein geschluckter Klick beendet den Schutz, danach ist er weg.
    await page.waitForFunction(() => {
      const probe = document.createElement("span");
      document.body.append(probe);
      let arrived = false;
      probe.addEventListener("click", () => (arrived = true));
      probe.click();
      probe.remove();
      return arrived;
    });
    await menuButton(page).click();
    await menuItems(page).first().waitFor();
    const exported = page.waitForResponse("**/api/RulesApi/exportRules");
    const clicked = page.evaluate(() => new Promise((done) => window.addEventListener("click", () => requestAnimationFrame(() => requestAnimationFrame(done)), { once: true, capture: true })));
    // Beide Klicks laufen im selben Tick, damit das Fenster von 350 ms nicht von der Testlast abhängt.
    await page.evaluate(() => {
      const item = [...document.querySelectorAll('[role="menuitem"]')].find((el) => el.textContent === "Regeln exportieren");
      const row = [...document.querySelectorAll('[data-testid="rule-row"]')].find((el) => el.offsetParent !== null);
      item.click();
      row.click();
    });
    await exported;
    await clicked;
    await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
    assert.equal(calls.exportRules, 1);
    assert.equal(await dialog(page).count(), 0, "der späte Klick öffnet den Dialog nicht");
  } finally {
    await context.close();
  }
});

// Bug categorization-f955d: die Zeile reagierte nur auf Enter, und ein Screenreader erfuhr nicht,
// dass sie bedienbar ist. Icon-Knöpfe trugen nur `title`: je Breite trägt jeder sichtbare
// Icon-Knopf ein aria-label (infrastructure-b3t9k aus einem eigenen Test hierher gezogen).
test("[tastatur] Enter und Leertaste auf der Regelzeile öffnen den Dialog zum Bearbeiten, jeder Icon-Knopf trägt ein aria-label, am PC und am Handy", async () => {
  for (const [width, height] of [[1440, 900], [390, 844]]) {
    for (const key of ["Enter", "Space"]) {
      const { context, page } = await openPage(width, height);
      try {
        if (key === "Enter") await expectIconButtonsLabelled(page, width);
        const row = visibleRow(page, "Regel eins");
        assert.equal(await row.getAttribute("role"), "button");
        await row.focus();
        await page.keyboard.press(key);
        await dialog(page).waitFor();
        assert.equal(await dialog(page).locator("input").first().inputValue(), "Regel eins", `${width} px, ${key}`);
      } finally {
        await context.close();
      }
    }
  }
});

// Bug budgeting-nfrds, dieselbe Konstruktion: der Mausdruck zählte als Klick außerhalb und
// schloss das Popover, der Klick öffnete es wieder. Das volle Popover scrollt den Dialog, dann
// trifft der Klick den weggesprungenen Auslöser nicht und der Fehler bleibt verdeckt. Der Filter
// kürzt das Popover, der Auslöser bleibt liegen.
test("[tastatur] PC: ein Mausklick auf den Auslöser schließt das offene Kategorie-Popover im Dialog", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await page.getByRole("button", { name: "Neue Regel" }).locator("visible=true").click();
    await dialog(page).waitFor();
    const trigger = page.locator('[data-testid="rule-category"] button');
    await trigger.click();
    await page.locator('[role="option"]').first().waitFor();
    await page.keyboard.type("zzzz");
    await page.locator('[role="option"]').first().waitFor({ state: "detached" });
    const hint = page.getByText("Esc schließt ohne zu ändern");
    // Gewartet wird auf das Ende des Klicks und zwei Bilder danach: ein Wiederöffnen wäre dann gerendert.
    const clicked = page.evaluate(() => new Promise((done) => window.addEventListener("click", () => requestAnimationFrame(() => requestAnimationFrame(done)), { once: true, capture: true })));
    await trigger.click();
    await clicked;
    assert.equal(await hint.count(), 0, "bleibt zu");
  } finally {
    await context.close();
  }
});

