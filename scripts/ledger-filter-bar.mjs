#!/usr/bin/env node
// scripts/ledger-filter-bar.mjs
//
// Fixture-Bausteine für `scripts/ledger-filter-bar.test.mjs` (ledger-j8xq4,
// "Kassenbuch-Filterleiste und Summenzeile"): rein synthetische, erfundene
// Fable.Remoting-Antworten, damit der Test die echte, per Vite gebaute Client-App
// (echtes Tailwind-CSS, echtes Fable-Bundle) gegen eine PC-Filterleiste rendern
// kann, ohne jemals ein echtes Backend oder die Entwicklungsdatenbank anzufassen.
// Muster übernommen aus `scripts/ledger-status-column.mjs`.

/// Eine feste, wiederholbare GUID je laufender Nummer.
export function fixtureGuid(n) {
  const hex = n.toString(16).padStart(4, "0");
  return `00000000-0000-4000-8000-${hex}00000000`.slice(0, 36);
}

export function buildAccountsFixture(count = 2) {
  const accounts = Array.from({ length: count }, (_, i) => ({
    Id: fixtureGuid(i + 1),
    Name: `Testkonto ${i + 1}`,
    Kind: "LedgerBankAccount",
    YnabAccountId: null,
    Closed: false,
  }));
  return { Accounts: accounts, DefaultAccountId: accounts[0].Id };
}

export function buildBalancesFixture(accounts) {
  return accounts.map((a, i) => ({ AccountId: a.Id, Balance: { Amount: 100 + i, Currency: "EUR" } }));
}

export function buildEntryFixture(accountId, n, overrides = {}) {
  return {
    Id: fixtureGuid(1000 + n),
    AccountId: accountId,
    Date: `2026-03-${String((n % 27) + 1).padStart(2, "0")}T00:00:00.0000000`,
    Amount: { Amount: -(10 + n), Currency: "EUR" },
    Payee: `Testfixtur ${n}`,
    Memo: null,
    CategoryName: "Testkategorie",
    CategoryGroupName: "Alltag",
    Splits: [],
    Origin: "MirroredFromYnab",
    DeletedInYnab: false,
    NeedsReview: false,
    Flag: "NoFlag",
    ...overrides,
  };
}

/// `LedgerEntryPage` (Shared/Domain.fs) aus einer Liste bereits fertiger
/// `LedgerEntryView`-Fixtures: Trefferzahl und Summe wie der echte Server sie über
/// den GESAMTEN Treffer bilden würde (hier: einfach über die übergebene Liste, weil
/// diese Fixture-Listen absichtlich klein genug für eine Seite bleiben).
export function entriesPage(entries) {
  const totalAmount = entries.reduce((sum, e) => sum + e.Amount.Amount, 0);
  return {
    Entries: entries,
    TotalCount: entries.length,
    TotalAmount: { Amount: totalAmount, Currency: "EUR" },
    DeletedCount: entries.filter((e) => e.DeletedInYnab).length,
    NeedsReviewCount: entries.filter((e) => e.NeedsReview).length,
  };
}

export const CATEGORY_IDS = {
  lebensmittel: fixtureGuid(9001),
  bahn: fixtureGuid(9002),
  nebenkosten: fixtureGuid(9003),
  apotheke: fixtureGuid(9004), // trifft absichtlich keine Buchung (AC "Kein Treffer")
  umzug: fixtureGuid(9005), // versteckt (Hidden: true)
};

/// `YnabCategory list` (Shared/Domain.fs): `Id: YnabCategoryId` ist ein Union-Case
/// mit genau einem Feld: Fable.SimpleJson (`Json.Converter.fs`, `TypeInfo.Union`,
/// `caseTypes.Length = 1`) serialisiert das als `{"YnabCategoryId": "<guid>"}`,
/// nicht als reiner String.
export function buildCategoriesFixture() {
  return [
    {
      Id: { YnabCategoryId: CATEGORY_IDS.lebensmittel },
      Name: "Lebensmittel",
      GroupName: "Alltag",
      Available: { Amount: 120.5, Currency: "EUR" },
      Hidden: false,
    },
    { Id: { YnabCategoryId: CATEGORY_IDS.bahn }, Name: "Bahn", GroupName: "Mobilität", Available: null, Hidden: false },
    {
      Id: { YnabCategoryId: CATEGORY_IDS.nebenkosten },
      Name: "Nebenkosten",
      GroupName: "Wohnen",
      Available: { Amount: -40, Currency: "EUR" },
      Hidden: false,
    },
    { Id: { YnabCategoryId: CATEGORY_IDS.apotheke }, Name: "Apotheke", GroupName: "Gesundheit", Available: null, Hidden: false },
    { Id: { YnabCategoryId: CATEGORY_IDS.umzug }, Name: "Umzug 2023", GroupName: "Alltag", Available: null, Hidden: true },
  ];
}

/// `AppSettings` (Shared/Domain.fs) mit einem konfigurierten YNAB-Budget. Ohne das
/// bleibt `Ledger.Types.LoadCategories` im Haupt-`State.fs` folgenlos (kein Budget,
/// kein Aufruf, siehe `State.fs`).
export function buildSettingsFixture(budgetId = "fake-budget-id") {
  return {
    Comdirect: null,
    Ynab: {
      PersonalAccessToken: "fake-token",
      DefaultBudgetId: { YnabBudgetId: budgetId },
      DefaultAccountId: null,
      QuickAddAccountId: null,
    },
    Sync: { DaysToFetch: 30 },
  };
}

/// Die vier Buchungen der Filterleisten-Fixtures: drei Kategorien mit je einer
/// eigenen Menge an Treffern (Lebensmittel: 2, Bahn: 1, Nebenkosten: 1), plus eine
/// vierte Kategorie ("Apotheke") ohne jede Buchung: der Fall für "Kein Treffer".
const RECIPES = [
  { n: 1, payee: "REWE Mitte", categoryId: CATEGORY_IDS.lebensmittel, categoryName: "Lebensmittel" },
  { n: 2, payee: "Bäckerei Krause", categoryId: CATEGORY_IDS.lebensmittel, categoryName: "Lebensmittel" },
  { n: 3, payee: "Deutsche Bahn", categoryId: CATEGORY_IDS.bahn, categoryName: "Bahn" },
  { n: 4, payee: "Stadtwerke Musterstadt", categoryId: CATEGORY_IDS.nebenkosten, categoryName: "Nebenkosten" },
];

function recipeToEntry(accountId, recipe) {
  return buildEntryFixture(accountId, recipe.n, { Payee: recipe.payee, CategoryName: recipe.categoryName });
}

/// Baut die Buchungsliste für eine gegebene Kategorie-Auswahl (Oder-Verknüpfung,
/// wie der echte Server sie über `CategoryIds` bildet, ADR 0036): keine Auswahl
/// heißt "alle Buchungen", mehrere Kategorien heißt ihre Vereinigung.
export function entriesForCategoryIds(accountId, categoryIds) {
  if (!Array.isArray(categoryIds) || categoryIds.length === 0) {
    return RECIPES.map((r) => recipeToEntry(accountId, r));
  }
  const wanted = new Set(categoryIds);
  return RECIPES.filter((r) => wanted.has(r.categoryId)).map((r) => recipeToEntry(accountId, r));
}

/// Registriert Konten/Kontostände/Spiegel-Status/Kategorien/Settings, plus eine
/// DYNAMISCHE `getLedgerEntriesPage`-Antwort, die tatsächlich auf `CategoryIds` im
/// Request-Body reagiert (anders als `mockLedgerApi` in `ledger-status-column.mjs`,
/// die immer dieselbe statische Fixture liefert).
///
/// `resolveEntries` (optional, ledger-j8xq4 zweite Iteration): ersetzt die
/// Standard-Kategorie-Auflösung `entriesForCategoryIds`, für Tests, die genug
/// Buchungen zum Scrollen brauchen (Sticky-Filterleiste), aber keine
/// Kategorie-Filterung testen. Bekommt dieselbe `query` wie die Standard-Auflösung.
export async function mockFilterBarApi(page, { accountsFixture, balancesFixture, resolveEntries }) {
  const accountId = accountsFixture.DefaultAccountId;
  const resolve = resolveEntries || ((query) => entriesForCategoryIds(accountId, query && query.CategoryIds));

  await page.route("**/api/LedgerApi/getLedgerAccounts", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(accountsFixture) })
  );
  await page.route("**/api/LedgerApi/getLedgerAccountBalances", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(balancesFixture) })
  );
  // Ein FRISCHER Lauf-Status (`RanAt` = jetzt), nicht `null`: sonst greift das
  // Frische-Kriterium (ADR 0014) sofort, stößt `RunMirror` an: ungemockt landet
  // der auf dem 500er-Catch-all, `MirrorFinished (Error ...)` löst erneut
  // `LoadMirrorStatus` aus, wieder `null`/stale, wieder `RunMirror`: eine endlose
  // Re-Render-Schleife, die Playwrights Stabilitäts-Check für `.click()` sprengt
  // ("element was detached from the DOM, retrying" in einer Dauerschleife).
  await page.route("**/api/LedgerApi/getLastMirrorRun", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        RanAt: new Date().toISOString(),
        Outcome: { MirrorRunSucceeded: { NewEntries: 0, ChangedEntries: 0, DeletedEntries: 0, ServerKnowledge: 1 } },
      }),
    })
  );
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (route) => {
    // Fable.Remoting.Client sendet auch einen einzelnen Record-Parameter als
    // EIN-ELEMENTIGES JSON-Array (`[query]`), nicht als das Objekt direkt, leicht zu
    // übersehen, weil `route.request().postDataJSON()` anstandslos ein Array liefert,
    // das wie ein Objekt "aussieht", bis man `.CategoryIds` darauf abfragt und immer
    // `undefined` bekommt (empirisch mit einem Netzwerk-Mitschnitt geprüft).
    const [query] = route.request().postDataJSON();
    const entries = resolve(query);
    route.fulfill({ contentType: "application/json", body: JSON.stringify(entriesPage(entries)) });
  });
  await page.route("**/api/LedgerApi/getLedgerPayeeSuggestions", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await page.route("**/api/SettingsApi/getSettings", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(buildSettingsFixture()) })
  );
  await page.route("**/api/YnabApi/getCategories", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: buildCategoriesFixture() }),
    })
  );
}

/// Simuliert einen laufenden Spiegel-Lauf (ADR 0014, `Components.Ledger.State`s
/// `MirrorStatusLoaded`/`RunMirror`, vierte Iteration ledger-j8xq4): ein
/// veralteter `getLastMirrorRun`-Stand (älter als die Fünf-Minuten-Schwelle,
/// `Components.Ledger.Types.freshnessThreshold`) löst beim Laden den
/// automatischen Hintergrund-Lauf aus, ohne einen Klick auf "Jetzt spiegeln".
/// `mirrorYnabBudget` bleibt danach absichtlich unbeantwortet, bis der
/// Aufrufer die zurückgegebene `finishMirror()` ruft: bis dahin bleibt
/// `model.MirrorRunning` wahr, wie ein echter, noch laufender Lauf.
///
/// MUSS nach `mockFilterBarApi` registriert werden: Playwright lässt ohne ein
/// `route.fallback()` die zuletzt registrierte Route für ein Muster gewinnen,
/// diese Funktion überschreibt also bewusst zwei von deren Routen.
export async function mockRunningMirror(page) {
  let mirrorStatusCalls = 0;
  await page.route("**/api/LedgerApi/getLastMirrorRun", async (route) => {
    mirrorStatusCalls += 1;
    const isFirstCall = mirrorStatusCalls === 1;
    if (isFirstCall) {
      // Bewusst verzögert: die Spalte soll ERST mit `MirrorRunning = false`
      // fertig gemountet sein (`FilterBarColumn`s Callback-Ref beobachtet den
      // Leisten-Knoten von diesem ersten Mount an), BEVOR der Status auf
      // "veraltet" umschlägt. Eine sofortige Antwort ließe `MirrorRunning`
      // teils schon beim allerersten Render wahr sein, ohne den Knotenwechsel,
      // den dieser Fund beschreibt.
      await new Promise((resolve) => setTimeout(resolve, 300));
    }
    const ranAt = isFirstCall
      ? new Date(Date.now() - 10 * 60 * 1000).toISOString() // veraltet: über fünf Minuten
      : new Date().toISOString(); // ab dem zweiten Aufruf frisch, sonst ein Neustart-Wettlauf
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        RanAt: ranAt,
        Outcome: { MirrorRunSucceeded: { NewEntries: 0, ChangedEntries: 0, DeletedEntries: 0, ServerKnowledge: 1 } },
      }),
    });
  });

  let pendingRoute = null;
  await page.route("**/api/LedgerApi/mirrorYnabBudget", (route) => {
    pendingRoute = route;
  });

  return {
    finishMirror: async () => {
      // Der Hinweis "Spiegel läuft" wird sichtbar, sobald `MirrorRunning` im
      // Zustand wahr wird; die eigentliche `mirrorYnabBudget`-Anfrage, die
      // dieselbe Reduzierer-Runde per `Cmd.ofMsg RunMirror` auslöst, braucht
      // noch einen echten Netzwerk-Umlauf, bevor Playwright sie abfängt. Kurz
      // darauf warten, statt den Aufrufer selbst raten zu lassen, wie lange.
      const deadline = Date.now() + 5000;
      while (!pendingRoute && Date.now() < deadline) {
        await new Promise((resolve) => setTimeout(resolve, 20));
      }
      if (!pendingRoute) throw new Error("mirrorYnabBudget wurde noch nicht aufgerufen");
      await pendingRoute.fulfill({
        contentType: "application/json",
        body: JSON.stringify({
          Ok: { NewEntries: 0, ChangedEntries: 0, DeletedEntries: 0, ServerKnowledge: 2 },
        }),
      });
    },
  };
}
