// scripts/ledger-account-choice.test.mjs
//
// Wächter für `ledger-ybmqc`: das Kassenbuch öffnet im zuletzt gewählten Konto (auch nach
// komplettem Neuladen, gemerkt je Gerät in localStorage), und am Handy (390x844) steht der
// Kontoname als Titel mit Chevron, der ein BottomSheet "Konto wechseln" mit "Alle Konten" und
// allen Konten öffnet. Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte
// Ledger-Antworten (Muster wie `ledger-sort-chip.test.mjs`).
//
// Ausführen: node --test scripts/ledger-account-choice.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntriesFixture,
  mockLedgerApi,
} from "./ledger-status-column.mjs";

let frontend;
let frontendPort;
let browser;
const accountsFixture = buildAccountsFixture(3);
const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
const entriesFixture = buildEntriesFixture(accountsFixture.DefaultAccountId, 12);
const requestedAccounts = [];

async function openPage(width, height) {
  const context = await browser.newContext({ viewport: { width, height } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await mockLedgerApi(page, { accountsFixture, balancesFixture, entriesFixture });
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (route) => {
    requestedAccounts.push(route.request().postData() ?? "");
    route.fulfill({ contentType: "application/json", body: JSON.stringify(entriesFixture) });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  return { context, page };
}

const second = () => accountsFixture.Accounts[1];
const stored = (page) => page.evaluate(() => window.localStorage.getItem("ledger.selectedAccountFilter"));

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("PC: gewähltes Konto überlebt ein komplettes Neuladen", async () => {
  const { context, page } = await openPage(1440, 900);
  const row = (name) => page.locator(".ledger-account-strip button").filter({ hasText: name }).filter({ visible: true }).first();
  await row(second().Name).click();
  await page.waitForFunction(
    (key) => window.localStorage.getItem(key) !== null,
    "ledger.selectedAccountFilter"
  );
  assert.equal(await stored(page), `account:${second().Id}`);
  await page.reload({ waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await page.waitForFunction(
    (name) => {
      const btn = [...document.querySelectorAll(".ledger-account-strip button")].find(
        (b) => b.textContent.includes(name) && b.offsetParent !== null
      );
      return btn && btn.className.includes("border-l-neon-teal");
    },
    second().Name
  );
  await context.close();
});

test("Handy: Titel zeigt den Kontonamen, Sheet listet alle Konten, Tipp wechselt und schließt", async () => {
  const { context, page } = await openPage(390, 844);
  const title = page.getByTestId("ledger-account-switcher");
  await title.waitFor();
  assert.match(await title.innerText(), new RegExp(accountsFixture.Accounts[0].Name));
  assert.ok(await title.locator("svg").count() > 0, "Chevron fehlt");
  await title.click();
  const sheet = page.locator(".bottom-sheet.active").getByTestId("ledger-account-sheet");
  await sheet.waitFor();
  const text = await sheet.innerText();
  assert.ok(text.includes("Alle Konten"), "Alle Konten fehlt im Sheet");
  for (const a of accountsFixture.Accounts) assert.ok(text.includes(a.Name), `${a.Name} fehlt im Sheet`);
  assert.ok(
    text.indexOf("Alle Konten") < text.indexOf(accountsFixture.Accounts[0].Name),
    "Alle Konten steht nicht zuoberst"
  );
  const before = requestedAccounts.length;
  await sheet.getByText(second().Name).click();
  // Das Sheet bleibt im DOM und gleitet aus; offen heißt `.bottom-sheet.active`.
  await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
  assert.match(await title.innerText(), new RegExp(second().Name));
  assert.equal(await stored(page), `account:${second().Id}`);
  assert.ok(requestedAccounts.length > before, "Liste wurde nicht neu geladen");
  // Neuladen: das Handy öffnet im gemerkten Konto.
  await page.reload({ waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await page.getByTestId("ledger-account-switcher").waitFor();
  assert.match(await page.getByTestId("ledger-account-switcher").innerText(), new RegExp(second().Name));
  await context.close();
});
