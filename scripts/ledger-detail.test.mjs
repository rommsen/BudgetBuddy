// scripts/ledger-detail.test.mjs
//
// Wächter für `ledger-q1hw2` (ADR 0053): das Buchungs-Detail im Kassenbuch. Ein Klick auf die
// Zeile (auch auf Payee oder Kategorie) markiert sie und öffnet das Detail: Seitenpanel am PC,
// Sheet am Handy und in der Kompaktliste. Das Zellmenü aus `ledger-rfq5p` gibt es nicht mehr;
// die Filter-Knöpfe sitzen an den Feldern im Detail und lassen es offen. Gemessen wird am
// echten, per Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten (Muster wie
// `ledger-filter-bar.test.mjs`, nie die echte Entwicklungsdatenbank).
//
// Ausführen: node --test scripts/ledger-detail.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded, pollUntil } from "./screenshot-route.mjs";
import {
  CATEGORY_IDS,
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;
let page;
let mobilePage;
const queries = [];
const historyRequests = [];

const PAYEE = "REWE Mitte";

function entries(accountId) {
  return [
    buildEntryFixture(accountId, 1, {
      Payee: PAYEE,
      CategoryName: "Lebensmittel",
      CategoryId: CATEGORY_IDS.lebensmittel,
      LinkedToYnab: true,
      NeedsReview: true,
      Memo: "Wocheneinkauf",
    }),
    // Nichts zu filtern: kein Payee, keine Kategorie.
    buildEntryFixture(accountId, 2, { Payee: null, CategoryName: null, CategoryId: null, LinkedToYnab: true }),
    // Noch nicht in YNAB, Push gescheitert.
    buildEntryFixture(accountId, 3, {
      Payee: "Deutsche Bahn",
      CategoryName: "Bahn",
      CategoryId: CATEGORY_IDS.bahn,
      LinkedToYnab: false,
      PushToYnabFailure: { Reason: "Netzwerkfehler", FailedAt: "2026-09-10T18:02:00Z" },
    }),
    // In YNAB gelöscht: Lesezustand.
    buildEntryFixture(accountId, 4, {
      Payee: "Café Lotte",
      CategoryName: "Restaurants",
      CategoryId: CATEGORY_IDS.nebenkosten,
      LinkedToYnab: true,
      DeletedInYnab: true,
    }),
    // Aufgeteilt (ledger-b7qmx, ADR 0045): eine Zeile mit bekannter Gruppe, eine ohne,
    // dazu ein Übertrag und eine offene Zeile.
    buildEntryFixture(accountId, 5, {
      Payee: "Kaufland Split",
      CategoryName: null,
      CategoryId: null,
      CategoryGroupName: null,
      LinkedToYnab: true,
      Amount: { Amount: -50, Currency: "EUR" },
      SplitCategoryGroupNames: { [JSON.stringify(CATEGORY_IDS.lebensmittel)]: "Alltag" },
      Splits: [
        { Target: { ToCategory: [{ YnabCategoryId: CATEGORY_IDS.lebensmittel }, "Lebensmittel"] }, Amount: { Amount: -30, Currency: "EUR" }, Memo: null },
        { Target: { ToCategory: [{ YnabCategoryId: CATEGORY_IDS.bahn }, "Bahn"] }, Amount: { Amount: -10, Currency: "EUR" }, Memo: null },
        { Target: { ToTransfer: [{ YnabAccountId: CATEGORY_IDS.nebenkosten }, "Tagesgeld"] }, Amount: { Amount: -5, Currency: "EUR" }, Memo: null },
        { Target: "Uncategorized", Amount: { Amount: -5, Currency: "EUR" }, Memo: null },
      ],
    }),
  ];
}

const HISTORY = [
  { Sequence: 1, OccurredAt: "2026-09-10T17:55:00Z", Origin: "ImportedFromComdirect", Description: "Comdirect-Import: Buchung erfasst" },
  { Sequence: 2, OccurredAt: "2026-09-10T17:56:00Z", Origin: "DecidedByRule", Description: "Regel: Kategorie Lebensmittel gesetzt" },
  { Sequence: 3, OccurredAt: "2026-09-11T08:12:00Z", Origin: "MirroredFromYnab", Description: "Kategorie in YNAB geändert" },
];

async function open(context) {
  const target = await context.newPage();
  target.setDefaultTimeout(30000);
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  await target.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(target, {
    accountsFixture,
    balancesFixture,
    resolveEntries: (query) => {
      queries.push(query);
      return entries(accountsFixture.DefaultAccountId);
    },
  });
  await target.route("**/api/LedgerApi/getEntryHistory", (route) => {
    const [id] = route.request().postDataJSON();
    historyRequests.push(id);
    route.fulfill({ contentType: "application/json", body: JSON.stringify(HISTORY) });
  });
  // Offen lassen: sonst startet die Seite den Spiegel-Lauf in einer Schleife.
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  page = await open(await browser.newContext({ viewport: { width: 1440, height: 900 } }));
  mobilePage = await open(
    await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true })
  );
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

const row = (p, text) => p.locator("table tbody tr", { hasText: text }).first();
const panel = (p) => p.locator('[data-testid="ledger-detail-panel"]');
const sheet = (p) => p.locator(".bottom-sheet.active");
const title = (p) => p.locator('[data-testid="detail-title"]');

test("PC (1440): Klick auf die Payee-Zelle öffnet das Panel, kein Menü; Liste bleibt, Memo fehlt, Zeile markiert", async () => {
  await row(page, PAYEE).waitFor();
  await row(page, PAYEE).locator("td").nth(2).click();
  await panel(page).waitFor();
  assert.equal(await page.locator('[role="menuitem"]').count(), 0, "kein Zellmenü");
  assert.equal(await title(page).textContent(), PAYEE);
  assert.equal(await page.locator("table thead th", { hasText: "Memo" }).count(), 0, "die Memo-Spalte fehlt");
  assert.equal(await row(page, PAYEE).getAttribute("data-keyboard-focus"), "true", "die Zeile ist markiert");
  const geometry = await page.evaluate(() => {
    const table = document.querySelector("table").getBoundingClientRect();
    const aside = document.querySelector('[data-testid="ledger-detail-panel"]').getBoundingClientRect();
    return { tableRight: table.right, panelLeft: aside.left, panelWidth: aside.width, rows: document.querySelectorAll("table tbody tr").length };
  });
  assert.ok(geometry.rows >= 4, "die Liste bleibt sichtbar");
  assert.ok(geometry.tableRight <= geometry.panelLeft + 1, `Tabelle (${geometry.tableRight}) überlappt das Panel (${geometry.panelLeft}) nicht`);
  assert.equal(Math.round(geometry.panelWidth), 420, "das Panel ist 420 px breit");
  assert.equal(await page.locator('table tbody tr button[aria-haspopup="menu"]').count(), 0, "keine Auslöser in den Zellen");
});

test("PC: Das Panel zeigt Felder, Herkunft und die Zeitleiste, neueste Zeile oben, mit Punktfarbe nach Urheber", async () => {
  await page.locator('[data-testid="detail-timeline"]').waitFor();
  const texts = await page.locator('[data-testid="detail-timeline"] li p:first-child').allTextContents();
  assert.deepEqual(texts, ["Kategorie in YNAB geändert", "Regel: Kategorie Lebensmittel gesetzt", "Comdirect-Import: Buchung erfasst"]);
  const dots = await page.locator('[data-testid="detail-timeline"] [data-dot]').evaluateAll((els) => els.map((e) => e.getAttribute("data-dot")));
  assert.deepEqual(dots, ["grey", "purple", "teal"]);
  assert.equal(await page.locator('[data-testid="detail-origin"]').textContent(), "HERKUNFTYNAB-Spiegel");
  assert.match(await page.locator('[data-testid="detail-category"]').textContent(), /Alltag: Lebensmittel/);
  assert.match(await page.locator('[data-testid="detail-split"]').textContent(), /Kein Split/);
  assert.match(await page.locator('[data-testid="detail-memo"]').textContent(), /Wocheneinkauf/);
  assert.equal(await page.locator('[data-testid="detail-reviewed"] [role="switch"]').getAttribute("aria-checked"), "false", "zu prüfen: Schalter aus");
  assert.equal(await page.locator('[data-testid="detail-notice"]').count(), 0, "verknüpft, kein Fehlschlag: keine Leiste");
});

test("PC: Die Filter-Knöpfe tragen den vollen Namen, filtern und lassen das Panel offen", async () => {
  const payeeButton = panel(page).locator('[data-testid="detail-payee"] [data-testid="detail-filter"]');
  assert.equal(await payeeButton.getAttribute("aria-label"), `Nur Buchungen von ${PAYEE} zeigen`);
  // Roman suchte den Payee-Filter, solange er im Kopf direkt am Namen klebte: der Payee hat
  // eine eigene Feldzeile, und beide Filter-Knöpfe enden an derselben rechten Kante.
  const categoryRight = await panel(page)
    .locator('[data-testid="detail-category"] [data-testid="detail-filter"]')
    .evaluate((el) => el.getBoundingClientRect().right);
  const payeeRight = await payeeButton.evaluate((el) => el.getBoundingClientRect().right);
  assert.ok(Math.abs(payeeRight - categoryRight) < 1, `Filter-Knöpfe bündig rechts (${payeeRight} gegen ${categoryRight})`);
  assert.equal(await payeeButton.getAttribute("title"), `Nur Buchungen von ${PAYEE} zeigen`);
  const before = queries.length;
  await payeeButton.click();
  await page.waitForFunction(() => true);
  await pollUntil(() => queries.length > before);
  assert.ok(queries.length > before, "ein Neuladen lief");
  assert.equal(queries[queries.length - 1].Payee, PAYEE);
  assert.equal(queries[queries.length - 1].Page, 0);
  assert.equal(await panel(page).count(), 1, "das Panel bleibt offen");
  assert.equal(await title(page).textContent(), PAYEE, "es zeigt weiter dieselbe Buchung");

  const categoryButton = panel(page).locator('[data-testid="detail-category"] [data-testid="detail-filter"]');
  assert.equal(await categoryButton.getAttribute("aria-label"), "Nur Buchungen in Alltag: Lebensmittel zeigen");
  const beforeCategory = queries.length;
  await categoryButton.click();
  await page.waitForFunction(() => true);
  await pollUntil(() => queries.length > beforeCategory);
  assert.ok(queries.length > beforeCategory, "ein Neuladen lief");
  const last = queries[queries.length - 1];
  assert.deepEqual(last.CategoryIds, [CATEGORY_IDS.lebensmittel]);
  assert.equal(last.WithoutCategory, false);
  assert.equal(last.Payee, PAYEE, "der Payee-Filter bleibt stehen");
  assert.equal(await panel(page).count(), 1, "das Panel bleibt offen");
});

test("PC: Pfeil ab bewegt die Markierung, das Panel folgt und lädt die Historie; Esc schließt", async () => {
  await page.mouse.click(5, 5);
  const before = historyRequests.length;
  await page.keyboard.press("ArrowDown");
  await page.waitForFunction((t) => document.querySelector('[data-testid="detail-title"]')?.textContent !== t, PAYEE);
  assert.equal(await title(page).textContent(), "ohne Payee");
  await page.waitForFunction(() => true);
  for (let i = 0; i < 50 && historyRequests.length <= before; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.ok(historyRequests.length > before, "die Historie der neuen Buchung wurde geholt");
  await page.keyboard.press("Escape");
  await panel(page).waitFor({ state: "detached" });
  assert.equal(await row(page, "ohne Payee").getAttribute("data-keyboard-focus"), "true", "die Markierung bleibt");
  await page.keyboard.press("Enter");
  await panel(page).waitFor();
  assert.equal(await title(page).textContent(), "ohne Payee", "Enter öffnet die markierte Zeile");
});

test("PC: Ein Sortierwechsel lässt das Panel mit seiner Buchung stehen", async () => {
  await page.locator("table thead th", { hasText: "Betrag" }).first().click();
  await page.waitForFunction(() => true);
  assert.equal(await panel(page).count(), 1, "das Panel steht noch");
  assert.equal(await title(page).textContent(), "ohne Payee");
});

test("PC: Noch nicht in YNAB nennt Zeitpunkt und Grund und bietet Erneut senden", async () => {
  await row(page, "Deutsche Bahn").locator("td").nth(2).click();
  const notice = page.locator('[data-testid="detail-notice"]');
  await notice.waitFor();
  const text = await notice.textContent();
  assert.match(text, /Noch nicht in YNAB/);
  assert.match(text, /Push gescheitert am \d{2}\.\d{2}\.2026 \d{2}:\d{2}: Netzwerkfehler\. Erneut senden schickt die Buchung sofort\./);
  assert.equal(await notice.locator("button", { hasText: "Erneut senden" }).count(), 1, "Erneut senden");
});

test("PC: Eine gelöschte Buchung ist ein Lesezustand: rote Leiste, kein Schalter, grauer Betrag, Filter-Knöpfe da", async () => {
  await row(page, "Café Lotte").locator("td").nth(2).click();
  await page.locator('[data-testid="detail-notice"]', { hasText: "In YNAB gelöscht" }).waitFor();
  assert.equal(await page.locator('[data-testid="detail-reviewed"]').count(), 0, "kein Geprüft-Schalter");
  assert.equal(await panel(page).locator('[data-testid="detail-notice"] button').count(), 0, "keine Aktion in der Leiste");
  assert.equal(await panel(page).locator('[data-testid="detail-filter"]').count(), 2, "beide Filter-Knöpfe");
  const muted = await page.locator('[data-testid="detail-amount"]').evaluate((el) => el.className);
  assert.match(muted, /text-text-muted/, "der Betrag ist grau");
});

test("PC: Eine aufgeteilte Buchung zeigt im Detail die Split-Kategorien voll qualifiziert, die Liste bleibt bei N Split-Zeilen", async () => {
  await row(page, "Kaufland Split").locator("td").nth(2).click();
  await page.waitForFunction(() => document.querySelector('[data-testid="detail-title"]')?.textContent === "Kaufland Split");
  const lines = await panel(page).locator('[data-testid="detail-split"]').innerText();
  assert.match(lines, /Alltag: Lebensmittel/, "bekannte Gruppe: qualifiziert");
  assert.match(lines, /(^|\n)Bahn(\n|$)/, "ohne Gruppe: nur der Blattname");
  assert.match(lines, /Übertrag: Tagesgeld/);
  assert.match(lines, /kein Ziel/);
  assert.equal(await row(page, "Kaufland Split").locator("td").filter({ hasText: "4 Split-Zeilen" }).count(), 1, "Listenzeile unverändert");
  assert.match(await panel(page).locator('[data-testid="detail-category"]').textContent(), /4 Split-Zeilen/, "Kategorie-Feldzeile unverändert");
  await page.keyboard.press("Escape");
});

test("PC (900): Die Kompaktliste öffnet das Detail als Sheet statt als Panel", async () => {
  await page.setViewportSize({ width: 900, height: 900 });
  await page.keyboard.press("Escape");
  // Mit Maus ist die Kompaktzeile ein `div` mit Klick (ledger-m6zpd), kein Knopf mehr.
  await page.getByText(/ohne Payee/).first().waitFor();
  await page.getByText("Deutsche Bahn").first().click();
  await sheet(page).waitFor();
  assert.equal(await panel(page).count(), 0, "kein Panel");
  assert.equal(await sheet(page).locator('[data-testid="detail-filter"]').count() >= 1, true, "Filter-Knöpfe im Sheet");
  await page.keyboard.press("Escape");
  await sheet(page).waitFor({ state: "detached" });
  await page.setViewportSize({ width: 1440, height: 900 });
});

test("Handy (390x844): Tipp öffnet das Sheet mit Filter-Knöpfen an den Feldern, kein Block oben", async () => {
  await mobilePage.getByText("Deutsche Bahn").first().tap();
  await sheet(mobilePage).waitFor();
  assert.equal(await mobilePage.locator('[data-testid="row-filter-actions"]').count(), 0, "kein Aktionsblock");
  const buttons = sheet(mobilePage).locator('[data-testid="detail-filter"]');
  assert.deepEqual(
    await buttons.evaluateAll((els) => els.map((e) => e.getAttribute("aria-label"))),
    ["Nur Buchungen von Deutsche Bahn zeigen", "Nur Buchungen in Alltag: Bahn zeigen"]
  );
  // Jeder Knopf sitzt in der Zeile seines Felds.
  assert.equal(await sheet(mobilePage).locator('[data-testid="detail-payee"] [data-testid="detail-filter"]').count(), 1);
  assert.equal(await sheet(mobilePage).locator('[data-testid="detail-category"] [data-testid="detail-filter"]').count(), 1);
  const before = queries.length;
  await buttons.first().tap();
  await mobilePage.waitForFunction(() => true);
  await pollUntil(() => queries.length > before);
  assert.ok(queries.length > before, "ein Neuladen lief");
  assert.equal(queries[queries.length - 1].Payee, "Deutsche Bahn");
  assert.equal(await sheet(mobilePage).count(), 1, "das Sheet bleibt offen");
});

test("/styleguide: jeder neue Baustein hat eine Galerie-Karte", async () => {
  const styleguide = await (await browser.newContext({ viewport: { width: 1440, height: 900 } })).newPage();
  await styleguide.goto(`http://127.0.0.1:${frontendPort}/#/styleguide`, { waitUntil: "load" });
  for (const id of ["components-detail-notice", "components-detail-fields", "components-detail-timeline", "components-detail-panel"]) {
    await styleguide.locator(`[data-ds-card="${id}"]`).waitFor();
  }
  assert.equal(await styleguide.locator('[data-ds-card="components-detail-notice"] [role="status"]').count(), 3);
  assert.equal(await styleguide.locator('[data-ds-card="components-detail-fields"] [role="switch"]').count(), 1);
  assert.equal(await styleguide.locator('[data-ds-card="components-filterpopover-menuitem"] [role="menuitem"]').count() >= 2, true);
});
