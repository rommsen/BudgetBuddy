// scripts/ledger-review-state.test.mjs
//
// Gerenderter Beleg für `ledger-v9rq3`: "Zuletzt verwendet" im Kategorie-Filter übersteht ein
// Neuladen (localStorage gibt es nur im Browser), "Übernehmen" erst mit Änderung, die Karte "Alles
// geprüft" steht ohne "Jetzt spiegeln". Summenzeile, Klartext und den Text der Karte prüft `src/Tests/LedgerReviewStateTests.fs` (infrastructure-t5m2b).
// Echter Fable-Client gegen gemockte Ledger-Antworten mit erfundenen Werten.
//
// Ausführen: node --test scripts/ledger-review-state.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

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

async function openLedger(viewport) {
  const context = await browser.newContext({ viewport });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  const accounts = buildAccountsFixture(2);
  const balances = buildBalancesFixture(accounts.Accounts);
  const state = { reviewed: false };
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked"}' })
  );
  await mockFilterBarApi(page, {
    accountsFixture: accounts,
    balancesFixture: balances,
    resolveEntries: () =>
      state.reviewed
        ? []
        : [buildEntryFixture(accounts.DefaultAccountId, 1, { Payee: "Prüfzustand v9rq3", NeedsReview: true })],
  });
  await page.route("**/api/LedgerApi/setReviewMark", (route) => {
    state.reviewed = true;
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Ok: {
          Previous: { IsApproved: false, ClearedState: "ReviewUncleared" },
          Change: "ReviewMarkChanged",
          WriteBack: "ReviewMarkSent",
        },
      }),
    });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  return { page, context };
}

// Die Karte selbst ist View: ihr Text kommt aus `allReviewedText` (.NET), dass sie ohne "Jetzt
// spiegeln" steht, nur hier.
test("[flow:pruefliste-abarbeiten] PC: nach dem letzten Prüfen unter Zu prüfen steht Alles geprüft ohne Jetzt spiegeln", async () => {
  const { page, context } = await openLedger({ width: 1440, height: 900 });
  try {
    await page.getByRole("button", { name: /^Zu prüfen/ }).first().click();
    await page.locator("tr", { hasText: "Prüfzustand v9rq3" }).getByRole("button", { name: "Geprüft", exact: true }).click();
    await page.getByText("Alles geprüft", { exact: true }).waitFor();
    // Der Kopf des Kassenbuchs hat eigene Spiegel-Knöpfe; geprüft wird nur die Karte des Leerzustands.
    const emptyCard = page.locator("div").filter({ hasText: "Alles geprüft" }).last();
    assert.ok((await emptyCard.innerText()).includes("steht nichts mehr zu prüfen"), "Text der Karte");
    assert.equal(await emptyCard.getByRole("button", { name: "Jetzt spiegeln" }).count(), 0, "kein Jetzt spiegeln");
    assert.equal(await page.getByText("Keine Buchungen", { exact: true }).count(), 0, "kein Leerzustand ohne Daten");
  } finally {
    await context.close();
  }
});

test("[flow:zuletzt-verwendet-merken] PC: der Kategorie-Filter merkt Zuletzt verwendet im localStorage, auch nach Neuladen", async () => {
  const { page, context } = await openLedger({ width: 1440, height: 900 });
  try {
    const popover = page.locator(".shadow-2xl");
    await page.getByRole("button", { name: /^Kategorie/ }).first().click();
    await popover.getByText("Lebensmittel", { exact: true }).waitFor();
    assert.equal(await popover.getByTestId("category-recent").count(), 0, "anfangs nichts gemerkt");
    assert.ok(await popover.getByRole("button", { name: "Übernehmen" }).isDisabled(), "unverändert gesperrt");
    await popover.getByText("Lebensmittel", { exact: true }).click();
    const apply = popover.getByRole("button", { name: "Übernehmen (1)" });
    assert.ok(await apply.isEnabled(), "nach Änderung aktiv");
    await apply.click();
    await popover.waitFor({ state: "detached" });

    await page.reload({ waitUntil: "load" });
    await waitForPageLoaded(page, "/ledger");
    await page.getByRole("button", { name: /^Kategorie/ }).first().click();
    const recent = page.locator(".shadow-2xl").getByTestId("category-recent");
    await recent.waitFor();
    assert.ok((await recent.innerText()).includes("Lebensmittel"), "Lebensmittel steht unter Zuletzt verwendet");
  } finally {
    await context.close();
  }
});
