// scripts/regeln-tab.test.mjs
//
// categorization-n3sxw: der Tab „Regeln“ im Kassenbuch. Hier steht nur, was der Browser misst: der Happy Path
// „Regel anlegen“ am Handy mit Touch, der Fokus beim Tipp auf einen Umschalter, der Scrollstand der Buchungen beim
// Wechsel der Tabs und drei Bug-Regressionen aus der früheren Regelseite (`rules-page-mobile.test.mjs`), die nur im
// echten DOM auftreten: Klick und Enter auf einem Prioritätspfeil, Leertaste auf der Zeile, Mausdruck auf den Auslöser
// des Kategorie-Popovers. Liste, Formular, Muster-Test, Löschen mit Rückgängig und Tasten im Modell prüfen die
// .NET-Tests (`src/Tests/RulesListTests.fs`), Trefferflächen, Picker im Sheet und die Zeile „gelöscht“ am Platz der
// Layout-Sweep (Zustände `regeln`, `regeln-panel`, `regeln-sheet-picker`, `regeln-geloescht`, `regeln-geloescht-fehler`).
//
// Ausführen: npm run test:scripts -- scripts/regeln-tab.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";
import { stateNamed } from "./layout-states.mjs";
import { settle } from "./layout-invariants.mjs";

let frontend;
let port;
let browser;

before(async () => {
  port = await getFreePort();
  frontend = await startFrontend(port);
  browser = await chromium.launch();
});
after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, port);
});

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
const W = (c, v) => ({ [c]: v });
const guid = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, "0")}`;
const TIME = "2026-01-01T00:00:00.0000000Z";
const rule = (n, name, category) => ({
  Id: W("RuleId", guid(9000 + n)),
  Name: name,
  Pattern: `muster${n}`,
  PatternType: "Contains",
  TargetField: "Payee",
  CategoryId: W("YnabCategoryId", guid(category)),
  CategoryName: `Kategorie ${category}`,
  PayeeOverride: null,
  Priority: 10 - n,
  Enabled: true,
  CreatedAt: TIME,
  UpdatedAt: TIME,
});

/// Das Kassenbuch aus dem Layout-Sweep, dazu Regeln und Anlegen.
async function openLedger(width, height, touch) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: touch, isMobile: touch });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  await page.route("**/api/**", (r) => r.fulfill({ status: 500, contentType: "application/json", body: '{"error":"nicht gemockt"}' }));
  await stateNamed("kassenbuch").mock(page);
  const created = [];
  let rules = [rule(1, "Regel eins", 1), rule(2, "Regel zwei", 2)];
  await page.route("**/api/RulesApi/getAllRules", (r) => json(r, rules));
  await page.route("**/api/RulesApi/testRule", (r) => json(r, { Ok: false }));
  const reordered = [];
  await page.route("**/api/RulesApi/reorderRules", (r) => {
    reordered.push(JSON.parse(r.request().postData()));
    json(r, { Ok: null });
  });
  await page.route("**/api/RulesApi/createRule", (r) => {
    const [request] = JSON.parse(r.request().postData());
    created.push(request);
    const saved = { ...rule(3, request.Name, 0), Pattern: request.Pattern, CategoryId: request.CategoryId, CategoryName: "Neu", Enabled: request.Enabled };
    rules = [...rules, saved];
    json(r, { Ok: saved });
  });
  await page.goto(`http://127.0.0.1:${port}/#/ledger`, { waitUntil: "load" });
  await page.getByText("Bauhaus", { exact: true }).locator("visible=true").first().waitFor();
  await settle(page);
  return { context, page, created, reordered };
}

const tab = (page, name) => page.getByRole("tab", { name, exact: true });
const sheet = (page) => page.locator(".bottom-sheet.active");

test("[flow:regel-anlegen] Am Handy auf dem Tab Regeln eine neue Regel mit Kategorie anlegen, sie steht am Ende der Liste", async () => {
  const { context, page, created } = await openLedger(412, 915, true);
  try {
    await tab(page, "Regeln").tap();
    await page.locator('[data-testid="rule-row"]:visible').first().waitFor();

    await page.locator('[data-testid="rule-new-block"] button').tap();
    await sheet(page).getByText("Neue Regel", { exact: true }).waitFor();
    await sheet(page).getByLabel("Muster", { exact: true }).fill("baeckerei");
    await sheet(page).locator('[data-testid="rule-category"]').tap();
    // Die Auswahl ersetzt den Inhalt des Sheets (§7.15): kein zweites Sheet darüber.
    const option = sheet(page).locator('[role="option"]').first();
    await option.waitFor();
    assert.equal(await page.locator(".bottom-sheet.active").count(), 1, "ein Sheet");
    const chosen = (await option.innerText()).split("\n")[0].trim();
    await option.tap();
    await sheet(page).locator('[data-testid="rule-form"]').waitFor();

    // Nach der Wahl schluckt das Sheet bis zu 350 ms lang den nächsten Klick (ADR 0005, `swallowNextClick`). Gewartet
    // wird, bis ein Probeklick wieder ankommt: ein geschluckter Probeklick beendet die Sperre, danach ist sie weg.
    await page.waitForFunction(() => {
      const probe = document.createElement("span");
      document.body.append(probe);
      let arrived = false;
      probe.addEventListener("click", () => (arrived = true));
      probe.click();
      probe.remove();
      return arrived;
    });
    await Promise.all([
      page.waitForResponse("**/api/RulesApi/createRule"),
      sheet(page).getByRole("button", { name: "Regel anlegen" }).tap(),
    ]);
    await page.locator(".bottom-sheet.active").waitFor({ state: "detached" });
    assert.equal(created.length, 1, "angelegt");
    assert.ok(chosen.length > 0, "Kategorie gewählt");
    const last = page.locator('[data-testid="rule-row"]:visible').last();
    assert.match(await last.innerText(), /baeckerei/, "am Ende der Liste");
  } finally {
    await context.close();
  }
});

// ADR 0005 Punkt 2: am Handy schließt ein Tipp auf einen Umschalter die Tastatur nicht (`Segment.KeepsFocus`), der
// Wechsel zwischen den Textfeldern lässt den Fokus in einem Feld.
test("[fokus] Am Handy bleibt der Fokus im Feld beim Tipp auf Wenn und Suchen in und beim Wechsel zwischen Name, Muster und Muster testen", async () => {
  const { context, page } = await openLedger(412, 915, true);
  try {
    await tab(page, "Regeln").tap();
    await page.locator('[data-testid="rule-new-block"] button').tap();
    const form = sheet(page).locator('[data-testid="rule-form"]');
    await form.waitFor();
    const focused = () => page.evaluate(() => document.activeElement?.getAttribute("aria-label") ?? document.activeElement?.tagName);
    await form.getByLabel("Name", { exact: true }).tap();
    assert.equal(await focused(), "Name");
    await form.getByRole("button", { name: "Regex", exact: true }).tap();
    await form.getByRole("button", { name: "Memo", exact: true }).tap();
    assert.equal(await focused(), "Name", "Umschalter nimmt den Fokus nicht");
    await form.getByLabel("Muster", { exact: true }).tap();
    assert.equal(await focused(), "Muster");
    await form.getByLabel("Muster testen", { exact: true }).tap();
    assert.equal(await focused(), "Muster testen");
  } finally {
    await context.close();
  }
});

// GIVEN gescrollte Buchungen WHEN Roman per Tab zu „Regeln“ und per Tab zurück zu „Buchungen“ wechselt THEN steht der
// Scrollstand wie vorher. Die Klicks laufen per Skript, sonst rollte Playwright den Tab erst ins Bild.
test("[scroll] Nach Regeln und zurück stehen die Buchungen am selben Scrollstand", async () => {
  const { context, page } = await openLedger(1440, 900, false);
  try {
    await page.evaluate(() => window.scrollTo({ top: 700, behavior: "instant" }));
    await page.waitForFunction(() => window.scrollY === 700);
    const before = await page.evaluate(() => window.scrollY);
    const clickTab = (name) => page.evaluate((n) => [...document.querySelectorAll('[role="tab"]')].find((t) => t.textContent === n).click(), name);
    await clickTab("Regeln");
    await page.locator('[data-testid="rules-table"]').waitFor();
    await clickTab("Buchungen");
    await page.getByText("Bauhaus", { exact: true }).locator("visible=true").first().waitFor();
    const after = await page.evaluate(() => window.scrollY);
    assert.ok(Math.abs(after - before) <= 1, `vorher ${before}, nachher ${after}`);
  } finally {
    await context.close();
  }
});

const panelForm = (page) => page.locator('[data-testid="rule-form"]');
/// Wartet auf das Ende des nächsten `type`-Ereignisses und zwei Bilder danach: was der Klick auslöst, ist dann gerendert.
const afterNext = (page, type) =>
  page.evaluate((t) => new Promise((done) => window.addEventListener(t, () => requestAnimationFrame(() => requestAnimationFrame(done)), { once: true, capture: true })), type);

// Bug categorization-cgq35: ein Klick auf den gesperrten Prioritätspfeil fiel zur Zeile durch und öffnete die Regel,
// ebenso eine Taste auf dem Pfeil. Abwehr: `stopPropagation` um die Pfeile in der Tabelle (`ruleTable`). Ein gesperrter
// Knopf bekommt kein click, gewartet wird darum auf das pointerup.
test("[tastatur] Am PC verschiebt Enter auf einem Prioritätspfeil die Regel, weder die Taste noch ein Klick auf den gesperrten Pfeil öffnen sie", async () => {
  const { context, page, reordered } = await openLedger(1440, 900, false);
  try {
    await tab(page, "Regeln").click();
    const row = (name) => page.locator('[data-testid="rule-row"]:visible').filter({ hasText: name }).first();
    await row("Regel eins").waitFor();

    const released = afterNext(page, "pointerup");
    await row("Regel eins").getByRole("button", { name: "Priorität erhöhen" }).click({ force: true });
    await released;
    assert.equal(await panelForm(page).count(), 0, "der Klick auf den gesperrten Pfeil öffnet die Regel nicht");

    await row("Regel eins").getByRole("button", { name: "Priorität senken" }).focus();
    await Promise.all([page.waitForResponse("**/api/RulesApi/reorderRules"), page.keyboard.press("Enter")]);
    await page.locator('[data-testid="rule-row"]:visible').first().filter({ hasText: "Regel zwei" }).waitFor();
    assert.equal(reordered.length, 1, "verschoben");
    assert.equal(await panelForm(page).count(), 0, "Enter auf dem Pfeil öffnet die Regel nicht");
  } finally {
    await context.close();
  }
});

// Bug categorization-f955d: die Zeile reagierte nur auf Enter, nicht auf die Leertaste. Heute trägt jede Zeile einen
// echten Knopf „Regel bearbeiten: …“, am PC in der Tabelle, am Handy in der Karte.
test("[tastatur] Enter und Leertaste auf einer Regelzeile öffnen die Regel, am PC und am Handy", async () => {
  for (const [width, height, touch] of [[1440, 900, false], [412, 915, true]]) {
    for (const key of ["Enter", "Space"]) {
      const { context, page } = await openLedger(width, height, touch);
      try {
        await page.evaluate(() => [...document.querySelectorAll('[role="tab"]')].find((t) => t.textContent === "Regeln").click());
        const trigger = page.getByRole("button", { name: "Regel bearbeiten: Regel eins" }).locator("visible=true");
        await trigger.waitFor();
        await trigger.focus();
        await page.keyboard.press(key);
        const name = page.locator('[data-testid="rule-form"]:visible').getByLabel("Name", { exact: true });
        await name.waitFor();
        assert.equal(await name.inputValue(), "Regel eins", `${width} px, ${key}`);
      } finally {
        await context.close();
      }
    }
  }
});

// Bug budgeting-nfrds, dieselbe Konstruktion im Regel-Panel: der Mausdruck zählte als Klick außerhalb und schloss das
// Popover, der Klick öffnete es gleich wieder. Abwehr: `onMouseDown` mit `stopPropagation` um den Auslöser. Der Filter
// kürzt das Popover, damit der Auslöser liegen bleibt und der Klick ihn trifft.
test("[tastatur] Am PC schließt ein Mausklick auf den Auslöser das offene Kategorie-Popover der Regel", async () => {
  const { context, page } = await openLedger(1440, 900, false);
  try {
    await tab(page, "Regeln").click();
    await page.locator('[data-testid="rule-row"]:visible').filter({ hasText: "Regel eins" }).first().click();
    const trigger = page.locator('[data-testid="rule-category"]:visible');
    await trigger.waitFor();
    await trigger.click();
    const hint = page.locator('[data-testid="category-picker-footer-hint"]');
    await hint.waitFor();
    await page.keyboard.type("zzzz");
    await page.locator('[role="option"]').first().waitFor({ state: "detached" });
    const clicked = afterNext(page, "click");
    await trigger.click();
    await clicked;
    assert.equal(await hint.count(), 0, "bleibt zu");
  } finally {
    await context.close();
  }
});
