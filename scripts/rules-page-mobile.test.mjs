// scripts/rules-page-mobile.test.mjs
//
// Wächter für `categorization-cgq35`: die Regelseite am Handy und per Tastatur. Gemessen am
// echten Fable-Client gegen eine gemockte API. Verhindert: Prioritätspfeile mit 24 mal 20 px,
// ein Menü, das nur über Fokus aufgeht und dessen Einträge nicht auf Enter reagieren, und den
// Regel-Dialog als zentriertes Modal mit Dropdown am Handy. Für `categorization-f955d`: Zeilen
// per Tastatur, der Ghost-Click nach dem Menü im Sheet, der echte Dateiinhalt beim Import, die
// Namen der Icon-Knöpfe und die Texte im gerenderten Dialog.
//
// Ausführen: npm run test:scripts -- scripts/rules-page-mobile.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";

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

test("Handy: die Prioritätspfeile sind mindestens 48 mal 48 px, ein Tipp verschiebt die Regel und öffnet den Dialog nicht", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    const row = visibleRow(page, "Regel eins");
    for (const label of ["Priorität erhöhen", "Priorität senken"]) {
      const box = await row.getByRole("button", { name: label }).boundingBox();
      assert.ok(box && box.width >= 48 && box.height >= 48, `${label}: ${box?.width} mal ${box?.height}`);
    }

    // Die erste Regel kann nicht höher: auch der gesperrte Pfeil darf nicht zur Zeile durchfallen.
    await row.getByRole("button", { name: "Priorität erhöhen" }).click({ force: true });
    assert.equal(await dialog(page).count(), 0, "Tipp auf den gesperrten Pfeil öffnet nichts");

    await Promise.all([
      page.waitForResponse("**/api/RulesApi/reorderRules"),
      row.getByRole("button", { name: "Priorität senken" }).click(),
    ]);
    await page.waitForFunction(() =>
      [...document.querySelectorAll('[data-testid="rule-row"]')]
        .filter((el) => el.offsetParent !== null)[0]
        ?.textContent.includes("Regel zwei")
    );
    assert.equal(calls.reorder.length, 1);
    assert.ok(JSON.stringify(calls.reorder[0]).indexOf(guid(2)) < JSON.stringify(calls.reorder[0]).indexOf(guid(1)), "neue Reihenfolge");
    assert.equal(await dialog(page).count(), 0, "Tipp auf den Pfeil öffnet den Dialog nicht");
  } finally {
    await context.close();
  }
});

test("Handy: der Overflow-Knopf ist mindestens 48 px groß, das Menü liegt im Sheet und löst Export aus", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    assert.equal(await menuButton(page).getAttribute("aria-label"), "Weitere Aktionen");
    const box = await menuButton(page).boundingBox();
    assert.ok(box && box.width >= 48 && box.height >= 48, `Knopf ${box?.width} mal ${box?.height}`);

    await menuButton(page).click();
    await openSheets(page).first().waitFor();
    assert.deepEqual(await menuItems(page).allInnerTexts(), ["Regeln neu laden", "Regeln exportieren", "Regeln importieren"]);
    for (const item of await menuItems(page).all()) {
      const itemBox = await item.boundingBox();
      assert.ok(itemBox && itemBox.height >= 48, `Eintrag ${itemBox?.height} px hoch`);
    }
    // Styleguide §7.1.3: Autofokus nur bei feinem Pointer.
    assert.equal(await page.evaluate(() => matchMedia("(pointer: fine)").matches), false, "Touch-Kontext erwartet");
    assert.equal(await page.evaluate(() => document.activeElement?.getAttribute("role") ?? null), null, "Eintrag hat beim Öffnen den Fokus");

    await page.keyboard.press("Escape");
    await openSheets(page).first().waitFor({ state: "detached" });

    await menuButton(page).click();
    await Promise.all([
      page.waitForResponse("**/api/RulesApi/exportRules"),
      menuItems(page).filter({ hasText: "Regeln exportieren" }).click(),
    ]);
    await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
    assert.equal(calls.exportRules, 1);
  } finally {
    await context.close();
  }
});

test("PC: Enter öffnet das Menü, Tab erreicht jeden Eintrag, Enter löst aus, Esc und Klick außerhalb schließen", async () => {
  const { context, page, calls } = await openPage(1440, 900);
  try {
    assert.equal(await openSheets(page).count(), 0, "kein Sheet am PC");
    await menuButton(page).focus();
    await page.keyboard.press("Enter");
    await menuItems(page).first().waitFor();
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
  } finally {
    await context.close();
  }
});

test("PC: Enter auf einem Prioritätspfeil verschiebt die Regel und öffnet den Dialog nicht", async () => {
  const { context, page, calls } = await openPage(1440, 900);
  try {
    await visibleRow(page, "Regel eins").getByRole("button", { name: "Priorität senken" }).focus();
    await Promise.all([page.waitForResponse("**/api/RulesApi/reorderRules"), page.keyboard.press("Enter")]);
    await page.waitForFunction(() => document.querySelector('[data-testid="rule-row"]')?.textContent.includes("Regel zwei"));
    assert.equal(calls.reorder.length, 1);
    assert.equal(await dialog(page).count(), 0);
  } finally {
    await context.close();
  }
});

test("Handy: der Regel-Dialog ist ein BottomSheet, die Kategorie kommt aus dem Picker mit Suche, Anlegen speichert die Werte", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    await page.getByRole("button", { name: "Neue Regel" }).locator("visible=true").click();
    await dialog(page).waitFor();
    assert.equal(await page.locator('.bottom-sheet.active [data-testid="rule-dialog"]').count(), 1, "Dialog liegt im Sheet");
    assert.equal(await page.locator(".fixed.inset-0.z-50").count(), 0, "kein zentriertes Modal");

    await dialog(page).locator("input").first().fill("Bäcker");
    await dialog(page).getByPlaceholder("z. B. amazon", { exact: true }).fill("baeckerei");
    await page.locator('[data-testid="rule-category"] button').click();
    const search = page.locator(".bottom-sheet.layer-2.active input");
    await search.fill("Kategorie 7");
    await page.locator('.bottom-sheet.layer-2 [role="option"]').filter({ hasText: "Kategorie 7" }).click();
    await page.locator(".bottom-sheet.layer-2.active").waitFor({ state: "detached" });
    assert.match(await page.locator('[data-testid="rule-category"]').innerText(), /Alltag: Kategorie 7/);

    // Das Picker-Sheet schluckt nach der Wahl 350 ms lang den nächsten Klick (ADR 0005).
    await page.waitForTimeout(400);
    await page.getByRole("button", { name: "Regel erstellen" }).click();
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

test("PC: der Regel-Dialog bleibt ein Modal, der Picker hängt als Popover im Bild, Ändern speichert die neue Kategorie", async () => {
  const { context, page, calls } = await openPage(1440, 900);
  try {
    await visibleRow(page, "Regel eins").getByText("Regel eins").click();
    await dialog(page).waitFor();
    assert.equal(await openSheets(page).count(), 0, "kein Sheet am PC");
    await page.locator('[data-testid="rule-category"]', { hasText: "Alltag: Kategorie 1" }).waitFor();

    await page.locator('[data-testid="rule-category"] button').click();
    const options = page.locator('[role="option"]');
    await options.first().waitFor();
    assert.equal(await options.count(), 12);
    // Das Popover liegt im Scrollbereich des Dialogs: es rückt beim Öffnen ins Bild.
    const footer = await page.getByRole("button", { name: "Abbrechen" }).boundingBox();
    const hint = await page.getByText("Esc schließt ohne zu ändern").boundingBox();
    assert.ok(hint && footer && hint.y + hint.height <= footer.y, "das Popover wird nicht vom Dialogfuß abgeschnitten");

    await page.locator('[role="option"]').filter({ hasText: "Kategorie 3" }).click();
    await options.first().waitFor({ state: "detached" });
    await page.getByRole("button", { name: "Speichern" }).click();
    await dialog(page).waitFor({ state: "detached" });
    assert.equal(calls.update.length, 1);
    const [change] = calls.update[0];
    assert.ok(JSON.stringify(change.CategoryId).includes(guid(3)));
    assert.match(JSON.stringify(change.Name), /Regel eins/);
  } finally {
    await context.close();
  }
});

test("PC: Enter öffnet die Dateiauswahl, der Dateiinhalt geht unverändert an die API", async () => {
  const { context, page, calls } = await openPage(1440, 900);
  try {
    // Das Abfangen schaltet `waitForEvent` an, ohne auf Chromes Bestätigung zu warten. Direkt vor
    // der Taste kam die Bestätigung unter Last erst nach dem keyDown: der Klick erreichte das
    // Input, aber `filechooser` blieb aus. Deshalb liegen die Schritte bis zum Menü dazwischen.
    const chooserOpened = page.waitForEvent("filechooser");
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

// Bug categorization-f955d: das Menü im Sheet schloss ohne Schutz vor dem Ghost-Click. Der
// Schutz darf aber auch den programmatischen Klick auf das Datei-Input nicht schlucken, sonst
// geht am Handy die Dateiauswahl nie auf.
test("Handy: Importieren aus dem Sheet öffnet die Dateiauswahl", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    const chooserOpened = page.waitForEvent("filechooser");
    await menuButton(page).tap();
    await menuItems(page).first().waitFor();
    await menuItems(page).filter({ hasText: "Regeln importieren" }).tap();
    const chooser = await chooserOpened;
    await chooser.setFiles({ name: "regeln.json", mimeType: "application/json", buffer: Buffer.from(importFile) });
    await page.getByText(/Imported 2 rule/).first().waitFor();
    assert.deepEqual(calls.importRules.map((body) => JSON.parse(body)), [[importFile]]);
  } finally {
    await context.close();
  }
});

test("Handy: das Menü im Sheet bleibt nach dem Tipp offen", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    await menuButton(page).tap();
    await openSheets(page).first().waitFor();
    // Länger als das Fenster des Ghost-Click-Schutzes (350 ms).
    await page.evaluate(() => new Promise((done) => setTimeout(done, 400)));
    assert.equal(await menuItems(page).count(), 3, "das Menü ist noch offen");
  } finally {
    await context.close();
  }
});

// Bug categorization-f955d: ein später synthetischer Klick nach dem Eintrag traf die Zeile hinter
// dem Sheet und öffnete den Dialog. Der Klick läuft im selben Tick, damit das Fenster von 350 ms
// nicht von der Testlast abhängt.
test("Handy: nach einem Eintrag im Sheet trifft ein später Klick die Zeile dahinter nicht", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    await menuButton(page).click();
    await menuItems(page).first().waitFor();
    await page.evaluate(() => {
      const item = [...document.querySelectorAll('[role="menuitem"]')].find((el) => el.textContent === "Regeln exportieren");
      const row = [...document.querySelectorAll('[data-testid="rule-row"]')].find((el) => el.offsetParent !== null);
      item.click();
      row.click();
    });
    await openSheets(page).first().waitFor({ state: "detached" });
    await page.evaluate(() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))));
    assert.equal(await dialog(page).count(), 0, "der Dialog bleibt zu");
  } finally {
    await context.close();
  }
});

// Bug categorization-f955d: die Zeile reagierte nur auf Enter, und ein Screenreader erfuhr nicht,
// dass sie bedienbar ist.
for (const [name, width, height] of [["PC", 1440, 900], ["Handy", 390, 844]]) {
  for (const [keyName, key] of [["Enter", "Enter"], ["Leertaste", "Space"]]) {
    test(`${name}: ${keyName} auf der Regelzeile öffnet den Dialog zum Bearbeiten`, async () => {
      const { context, page } = await openPage(width, height);
      try {
        const row = visibleRow(page, "Regel eins");
        assert.equal(await row.getAttribute("role"), "button");
        await row.focus();
        await page.keyboard.press(key);
        await dialog(page).waitFor();
        assert.equal(await dialog(page).locator("input").first().inputValue(), "Regel eins");
      } finally {
        await context.close();
      }
    });
  }
}

test("PC: Leertaste auf einem Prioritätspfeil verschiebt die Regel und öffnet den Dialog nicht", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await visibleRow(page, "Regel eins").getByRole("button", { name: "Priorität senken" }).focus();
    await Promise.all([page.waitForResponse("**/api/RulesApi/reorderRules"), page.keyboard.press("Space")]);
    assert.equal(await dialog(page).count(), 0);
  } finally {
    await context.close();
  }
});

// Bug categorization-f955d: Icon-Knöpfe trugen nur `title`.
for (const [name, width, height] of [["PC", 1440, 900], ["Handy", 390, 844]]) {
  test(`${name}: jeder Icon-Knopf der Regelseite trägt ein aria-label`, async () => {
    const { context, page } = await openPage(width, height);
    try {
      const iconButtons = await page.evaluate(() =>
        [...document.querySelectorAll("button")]
          .filter((el) => el.offsetParent !== null && el.textContent.trim() === "")
          .map((el) => ({ label: el.getAttribute("aria-label"), html: el.outerHTML.slice(0, 120) }))
      );
      assert.ok(iconButtons.length >= 3, `Icon-Knöpfe gefunden: ${iconButtons.length}`);
      assert.deepEqual(iconButtons.filter((button) => !button.label), []);
    } finally {
      await context.close();
    }
  });
}

test("PC: der Fokus-Ring im Regelmenü ist bei Tastaturbedienung sichtbar", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await menuButton(page).focus();
    await page.keyboard.press("Enter");
    await menuItems(page).first().waitFor();
    const outline = await page.evaluate(() => {
      const style = getComputedStyle(document.activeElement);
      return { role: document.activeElement.getAttribute("role"), style: style.outlineStyle, width: style.outlineWidth };
    });
    assert.deepEqual(outline, { role: "menuitem", style: "solid", width: "2px" });
  } finally {
    await context.close();
  }
});

test("PC: das Menü geht nicht allein über den Fokus auf", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await menuButton(page).focus();
    assert.equal(await menuItems(page).count(), 0);
  } finally {
    await context.close();
  }
});

// Bug budgeting-nfrds, dieselbe Konstruktion: der Mausdruck zählte als Klick außerhalb und
// schloss das Popover, der Klick öffnete es wieder. Das volle Popover scrollt den Dialog, dann
// trifft der Klick den weggesprungenen Auslöser nicht und der Fehler bleibt verdeckt. Der Filter
// kürzt das Popover, der Auslöser bleibt liegen.
test("PC: der Auslöser schließt das offene Kategorie-Popover im Dialog", async () => {
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
    await trigger.click();
    await page.evaluate(() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))));
    assert.equal(await hint.count(), 0, "bleibt zu");
  } finally {
    await context.close();
  }
});

// Ersetzt die frühere Quelltext-Prüfung (Entscheidung des Owners, `categorization-f955d`): geprüft
// wird, was im gerenderten Dialog steht.
test("Dialogtexte: deutsch, z. B. mit Abstand, Auslassungszeichen und Knopf Testen, je Mustertyp", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await page.getByRole("button", { name: "Neue Regel" }).locator("visible=true").click();
    await dialog(page).waitFor();

    const renderedTexts = () =>
      dialog(page).evaluate((el) => [
        el.innerText,
        ...[...el.querySelectorAll("input")].map((input) => input.placeholder),
        ...[...el.querySelectorAll("option")].map((option) => option.textContent),
      ]);

    const seen = [];
    for (const patternType of ["Contains", "Exact", "Regex"]) {
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
  } finally {
    await context.close();
  }
});

// Bug categorization-f955d: ein Netzfehler beim Muster-Test erschien als ungültiges Muster.
test("Dialog: Muster testen trennt Netzfehler und ungültiges Muster", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await page.getByRole("button", { name: "Neue Regel" }).locator("visible=true").click();
    await dialog(page).waitFor();
    await dialog(page).getByPlaceholder("z. B. amazon", { exact: true }).fill("(");
    await dialog(page).getByPlaceholder("Beispieltext eingeben…").fill("REWE");

    await page.route("**/api/RulesApi/testRule", (r) => r.abort());
    await dialog(page).getByRole("button", { name: "Testen", exact: true }).click();
    await dialog(page).getByText("Muster konnte nicht geprüft werden").waitFor();

    await page.unroute("**/api/RulesApi/testRule");
    await page.route("**/api/RulesApi/testRule", (r) =>
      r.fulfill({ contentType: "application/json", body: JSON.stringify({ Error: { InvalidPattern: ["(", "Klammer fehlt"] } }) })
    );
    await dialog(page).getByRole("button", { name: "Testen", exact: true }).click();
    await dialog(page).getByText("Ungültiges Muster").waitFor();
  } finally {
    await context.close();
  }
});
