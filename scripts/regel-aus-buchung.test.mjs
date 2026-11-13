// scripts/regel-aus-buchung.test.mjs
//
// ledger-hg5m5: Regel aus der Buchung. Hier steht nur der Happy Path am Handy mit Touch: Kassenbuch, Buchung öffnen,
// „Regel anlegen“, die Vorbelegung steht im Formular, anlegen, zurück im Detail derselben Buchung. Vorbelegung,
// Angebot, Urteil „passt schon“ und das Schließen prüfen die .NET-Tests (`src/Tests/LedgerRuleOfferClientTests.fs`,
// `src/Tests/RulesListTests.fs`, `src/Tests/RulesEngineTests.fs`), Trefferflächen und Verdecken der Layout-Sweep
// (Zustände `kassenbuch-regel-formular`, `kassenbuch-regel-angebot`).
//
// Ausführen: npm run test:scripts -- scripts/regel-aus-buchung.test.mjs

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
const sheet = (page) => page.locator(".bottom-sheet.active");

test("[flow:regel-aus-buchung] Am Handy aus einer Buchung im Kassenbuch eine Regel anlegen, vorbelegt mit Payee und Kategorie, danach steht wieder die Buchung da", async () => {
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  try {
    await page.route("**/api/**", (r) => r.fulfill({ status: 500, contentType: "application/json", body: '{"error":"nicht gemockt"}' }));
    await stateNamed("kassenbuch").mock(page);
    const created = [];
    await page.route("**/api/RulesApi/createRule", (r) => {
      const [request] = JSON.parse(r.request().postData());
      created.push(request);
      json(r, {
        Ok: {
          Id: { RuleId: "00000000-0000-0000-0000-000000009001" },
          Name: request.Name,
          Pattern: request.Pattern,
          PatternType: request.PatternType,
          TargetField: request.TargetField,
          CategoryId: request.CategoryId,
          CategoryName: "Lebensmittel",
          PayeeOverride: null,
          Priority: 0,
          Enabled: request.Enabled,
          CreatedAt: "2026-01-01T00:00:00.0000000Z",
          UpdatedAt: "2026-01-01T00:00:00.0000000Z",
        },
      });
    });
    await page.goto(`http://127.0.0.1:${port}/#/ledger`, { waitUntil: "load" });
    const row = page.getByText("Bauhaus", { exact: true }).locator("visible=true").first();
    await row.waitFor();
    await settle(page);

    await row.tap();
    await sheet(page).locator('[data-testid="detail-title"]').waitFor();
    await sheet(page).locator('[data-testid="detail-rule"]').tap();

    // Das Formular ersetzt den Inhalt des Sheets (§7.15): kein zweites Sheet, vorbelegt aus der Buchung.
    const form = sheet(page).locator('[data-testid="rule-form"]');
    await form.waitFor();
    assert.equal(await page.locator(".bottom-sheet.active").count(), 1, "ein Sheet");
    assert.equal(await form.getByLabel("Name", { exact: true }).inputValue(), "Bauhaus", "Name");
    assert.equal(await form.getByLabel("Muster", { exact: true }).inputValue(), "Bauhaus", "Muster");
    await form.locator('[data-testid="rule-category"]').getByText("Lebensmittel").waitFor();

    await Promise.all([
      page.waitForResponse("**/api/RulesApi/createRule"),
      sheet(page).getByRole("button", { name: "Regel anlegen" }).tap(),
    ]);
    // Zurück im Detail derselben Buchung.
    await sheet(page).locator('[data-testid="detail-title"]').getByText("Bauhaus").waitFor();
    await page.getByText("Regel angelegt").first().waitFor();
    assert.equal(created.length, 1, "angelegt");
  } finally {
    await context.close();
  }
});
