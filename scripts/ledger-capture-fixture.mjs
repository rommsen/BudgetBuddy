// scripts/ledger-capture-fixture.mjs
//
// Gemockte Antworten für "Neue Buchung" im Kassenbuch (ledger-at63w): drei gespiegelte Konten (Bar,
// Tagesgeld, das Import-Konto Girokonto), die Budget-Konten dazu, die Vorbelegung mit drei Zuletzt-Chips
// und die Antwort auf "Buchung anlegen". Rein synthetisch, nie die echte Datenbank. Genutzt vom
// Flow-Test `ledger-capture.test.mjs` und von den Zuständen `kassenbuch-erfassen*` im Layout-Sweep.

import {
  CATEGORY_IDS,
  buildBalancesFixture,
  buildEntryFixture,
  fixtureGuid,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
const ynab = (id) => ({ YnabAccountId: id });

const BAR = fixtureGuid(8001);
const TAGESGELD = fixtureGuid(8002);
const GIRO = fixtureGuid(8003);
const BAR_Y = fixtureGuid(8101);
const TAGESGELD_Y = fixtureGuid(8102);
const GIRO_Y = fixtureGuid(8103);

/// Heute als Berliner Kalendertag (Doktrin Punkt 5), im Format von Fable.Remoting.
function berlinToday() {
  const day = new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Berlin" }).format(new Date());
  return `${day}T00:00:00.0000000`;
}

/// Registriert alle Antworten und gibt die aufgezeichneten Aufrufe zurück. `catchAll`: ungemockte Aufrufe
/// antworten mit 500 (der Flow-Test); der Layout-Sweep registriert vorher seine eigenen App-Antworten.
export async function mockCapture(page, { catchAll = true } = {}) {
  const calls = { add: [], barId: BAR, categoryId: CATEGORY_IDS.lebensmittel };
  if (catchAll) {
    await page.route("**/api/**", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
    );
  }
  const accountsFixture = {
    Accounts: [
      { Id: GIRO, Name: "Girokonto", Kind: "LedgerBankAccount", YnabAccountId: ynab(GIRO_Y), Closed: false },
      { Id: BAR, Name: "Bar", Kind: "LedgerCashAccount", YnabAccountId: ynab(BAR_Y), Closed: false },
      { Id: TAGESGELD, Name: "Tagesgeld ING", Kind: "LedgerBankAccount", YnabAccountId: ynab(TAGESGELD_Y), Closed: false },
    ],
    DefaultAccountId: GIRO,
  };
  const balances = buildBalancesFixture(accountsFixture.Accounts).map((b, i) => ({
    ...b,
    Balance: { Amount: [2418.07, 244.33, 1950][i], Currency: "EUR" },
  }));
  const entries = [
    buildEntryFixture(GIRO, 1, { Payee: "Rewe Markt", LinkedToYnab: true }),
    buildEntryFixture(GIRO, 2, { Payee: "dm Drogeriemarkt", LinkedToYnab: true }),
  ];
  await mockFilterBarApi(page, { accountsFixture, balancesFixture: balances, resolveEntries: () => entries });
  for (const [name, body] of [
    ["getEntryHistory", []],
    ["getLedgerEntry", null],
    ["getPayeeCategorySuggestions", []],
    ["getBookingCategoryForPayee", null],
  ]) {
    await page.route(`**/api/LedgerApi/${name}`, (r) => json(r, body));
  }
  await page.route("**/api/SyncApi/getBankConnectionStatus", (r) => json(r, "BankConnected"));
  await page.route("**/api/SyncApi/getCurrentSession", (r) => json(r, null));
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => json(r, { SavedColor: null, SuggestedColor: null }));
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await page.route("**/api/LedgerApi/getBudgetAccounts", (r) =>
    json(r, {
      Ok: [
        { Id: ynab(GIRO_Y), Name: "Girokonto", Balance: { Amount: 0, Currency: "EUR" }, OnBudget: true, Closed: false },
        { Id: ynab(BAR_Y), Name: "Bar", Balance: { Amount: 0, Currency: "EUR" }, OnBudget: true, Closed: false },
        { Id: ynab(TAGESGELD_Y), Name: "Tagesgeld ING", Balance: { Amount: 0, Currency: "EUR" }, OnBudget: true, Closed: false },
      ],
    })
  );
  const chip = (accountId, payee, amount, target) => ({ AccountId: accountId, Payee: payee, Target: target, Amount: amount, IsOutflow: true });
  await page.route("**/api/LedgerApi/getLedgerBookingDefaults", (r) =>
    json(r, {
      Today: berlinToday(),
      ImportAccountId: GIRO,
      LastUsedAccountId: BAR,
      Recent: [
        chip(BAR, "Bäckerei Lange", 8.6, { BookOnCategory: { YnabCategoryId: CATEGORY_IDS.lebensmittel } }),
        chip(BAR, "Wochenmarkt", 23, { BookOnCategory: { YnabCategoryId: CATEGORY_IDS.lebensmittel } }),
        chip(TAGESGELD, "Tankstelle", 60, null),
      ],
    })
  );
  await page.route("**/api/LedgerApi/addLedgerBooking", (r) => {
    calls.add.push(r.request().postDataJSON());
    json(r, { Ok: { EntryId: fixtureGuid(8999), AccountName: "Bar", PushFailure: null } });
  });
  return calls;
}
