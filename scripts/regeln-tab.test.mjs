// scripts/regeln-tab.test.mjs
//
// categorization-n3sxw: der Tab „Regeln“ im Kassenbuch. Hier steht nur, was der Browser misst: der Happy Path
// „Regel anlegen“ am Handy mit Touch, der Fokus beim Tipp auf einen Umschalter und der Scrollstand der Buchungen beim
// Wechsel der Tabs. Liste, Formular, Muster-Test, Löschen mit Rückgängig und Tasten prüfen die .NET-Tests
// (`src/Tests/RulesListTests.fs`), Trefferflächen und Picker im Sheet der Layout-Sweep (Zustände `regeln`,
// `regeln-panel`, `regeln-sheet-picker`).
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
  await page.route("**/api/RulesApi/createRule", (r) => {
    const [request] = JSON.parse(r.request().postData());
    created.push(request);
    const saved = { ...rule(3, request.Name, 0), Pattern: request.Pattern, CategoryId: request.CategoryId, CategoryName: "Neu", Enabled: request.Enabled };
    rules = [saved, ...rules];
    json(r, { Ok: saved });
  });
  await page.goto(`http://127.0.0.1:${port}/#/ledger`, { waitUntil: "load" });
  await page.getByText("Bauhaus", { exact: true }).locator("visible=true").first().waitFor();
  await settle(page);
  return { context, page, created };
}

const tab = (page, name) => page.getByRole("tab", { name, exact: true });
const sheet = (page) => page.locator(".bottom-sheet.active");

test("[flow:regel-anlegen] Am Handy auf dem Tab Regeln eine neue Regel mit Kategorie anlegen, sie steht vorn in der Liste", async () => {
  const { context, page, created } = await openLedger(412, 915, true);
  try {
    await tab(page, "Regeln").tap();
    await page.locator('[data-testid="rule-row"]:visible').first().waitFor();
    assert.equal(await page.getByRole("button", { name: /Buchung/ }).locator("visible=true").count(), 0, "kein „+ Buchung“ auf dem Tab Regeln");

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

    // Nach der Wahl schluckt das Sheet 350 ms lang den nächsten Klick (ADR 0005): tippen, bis die Anfrage hinausgeht.
    const submit = sheet(page).getByRole("button", { name: "Regel anlegen" });
    for (let attempt = 0; attempt < 20 && created.length === 0; attempt += 1) {
      await submit.tap();
      await page.waitForResponse("**/api/RulesApi/createRule", { timeout: 200 }).catch(() => {});
    }
    await page.locator(".bottom-sheet.active").waitFor({ state: "detached" });
    assert.equal(created.length, 1, "angelegt");
    assert.equal(created[0].Name, "baeckerei", "leerer Name wird zum Muster");
    assert.equal(created[0].Enabled, true, "aktiv");
    assert.ok(chosen.length > 0, "Kategorie gewählt");
    const first = page.locator('[data-testid="rule-row"]:visible').first();
    assert.match(await first.innerText(), /baeckerei/, "vorn in der Liste");
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

// GIVEN gescrollte Buchungen WHEN Roman zu „Regeln“ und zurück zu „Buchungen“ wechselt THEN steht der Scrollstand wie
// vorher. Der Klick läuft per Skript, sonst rollte Playwright den Tab erst ins Bild.
test("[scroll] Nach Regeln und zurück stehen die Buchungen am selben Scrollstand", async () => {
  const { context, page } = await openLedger(1440, 900, false);
  try {
    await page.evaluate(() => window.scrollTo({ top: 700, behavior: "instant" }));
    await page.waitForFunction(() => window.scrollY === 700);
    const before = await page.evaluate(() => window.scrollY);
    await page.evaluate(() => [...document.querySelectorAll('[role="tab"]')].find((t) => t.textContent === "Regeln").click());
    await page.locator('[data-testid="rules-table"]').waitFor();
    await page.goBack();
    await page.getByText("Bauhaus", { exact: true }).locator("visible=true").first().waitFor();
    const after = await page.evaluate(() => window.scrollY);
    assert.ok(Math.abs(after - before) <= 1, `vorher ${before}, nachher ${after}`);
  } finally {
    await context.close();
  }
});
