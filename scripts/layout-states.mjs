// scripts/layout-states.mjs
//
// Die Zustände des Layout-Sweeps (infrastructure-nx14s): jede Route und jedes Sheet, Popover oder jeder Dialog,
// der schon einmal eine Layout-Regression hatte. Ein neuer Layoutbug bekommt hier einen Zustand (oder in
// `layout-invariants.mjs` eine Invariante), keinen eigenen Test je Komponente (Doktrin Punkt 5 und 8).
//
// Ein Zustand: `name`, `route`, `widths`, `mock(page)` (rein synthetische Antworten per `page.route`),
// `open(page, { width, mobile })` stellt den Zustand her und wartet auf ihn. Optional: `primary(page)` (der
// Primärknopf einer Ebene, Invarianten 5 und 7, je Breite auch `null`), `reach(page)` (was ohne Scrollen
// sichtbar bleiben muss), `keyboard: { focus, reach }` (Invariante 7 mit offener Tastatur: `focus` bekommt den
// Fokus, `reach` bleibt im Bild), `deferred: { api, body, loaded }` (eine verzögerte Antwort, Invariante 9),
// `height` (Höhe des Viewports), `checks` (nur diese Invarianten). Die Fixtures tragen lange Namen und große
// Beträge (Invariante 4).

import { HANDY_WIDTHS, PAGE_WIDTHS, TABLE_WIDTHS, settle } from "./layout-invariants.mjs";
import { mockCapture } from "./ledger-capture-fixture.mjs";
import { buildAccountsFixture, buildBalancesFixture, buildCategoriesFixture, buildEntryFixture, CATEGORY_IDS, fixtureGuid, mockFilterBarApi, mockRunningMirror } from "./ledger-filter-bar.mjs";

const eur = (amount) => ({ Amount: amount, Currency: "EUR" });
const guid = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, "0")}`;
const W = (c, v) => ({ [c]: v });
const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
const frames = (page) => page.evaluate(() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))));

// ============================================================
// Was jede Seite lädt
// ============================================================

const NAV_ACCOUNTS = {
  Accounts: [
    { Id: fixtureGuid(8001), Name: "Girokonto", Kind: "LedgerBankAccount", YnabAccountId: null, Closed: false },
    { Id: fixtureGuid(8002), Name: "Kreditkarte mit einem sehr langen Namen für die Seitenleiste", Kind: "LedgerBankAccount", YnabAccountId: null, Closed: false },
    { Id: fixtureGuid(8003), Name: "Bar", Kind: "LedgerCashAccount", YnabAccountId: null, Closed: false },
  ],
  DefaultAccountId: fixtureGuid(8001),
};

/// Die Aufrufe, die die App auf jeder Seite macht (Navigation, Sync-Stand, Einstellungen). Spätere Routen
/// überschreiben sie (Playwright lässt die zuletzt registrierte gewinnen).
async function mockApp(page) {
  await page.route("**/api/SettingsApi/getSettings", (r) =>
    json(r, { Comdirect: null, Ynab: { HasToken: true, DefaultBudgetId: W("YnabBudgetId", "fake-budget-id"), DefaultAccountId: null, QuickAddAccountId: null }, Sync: { DaysToFetch: 14, AutoFetchEnabled: true, AutoFetchIntervalMinutes: 360 } })
  );
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => json(r, { SavedColor: "Purple", SuggestedColor: null }));
  await page.route("**/api/SyncApi/getBankConnectionStatus", (r) => json(r, "BankConnected"));
  await page.route("**/api/SyncApi/getCurrentSession", (r) => json(r, null));
  // budgeting-w9yt4: ohne Pins; der Pin-Knopf im Kategorie-Detail steht damit in jedem Zustand des Budgets.
  await page.route("**/api/BudgetApi/getPinnedCategories", (r) => json(r, { Ok: [] }));
  await page.route("**/api/BudgetApi/getTargetCheck", (r) => json(r, { Ok: { Month: "2026-11-01", Today: "2026-11-06", Categories: [] } }));
  await page.route("**/api/YnabApi/getPayees", (r) => json(r, { Ok: [] }));
  await page.route("**/api/YnabApi/getCategories", (r) =>
    json(r, { Ok: [...buildCategoriesFixture(), { Id: { YnabCategoryId: guid(2) }, Name: LONG_CATEGORY, GroupName: "Alltag", Available: eur(12345.67), Hidden: false }] })
  );
  // Die Seitenleiste lädt Konten und Stände beim Start auf jeder Seite (design-system-k6kfw): ein langer Name
  // und ein negativer Stand für Invariante 4.
  await page.route("**/api/LedgerApi/getLedgerAccounts", (r) => json(r, NAV_ACCOUNTS));
  // Die Zahl an der Übersicht in Rail und Seitenleiste lädt beim Start auf jeder Seite (ledger-s1weh): keine offene
  // Buchung, also kein Zähler. Seiten mit eigener Liste überschreiben die Antwort.
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (r) =>
    json(r, { Entries: [], TotalCount: 0, TotalAmount: eur(0), DeletedCount: 0, NeedsReviewCount: 0, AskLenaCount: 0 })
  );
  await page.route("**/api/LedgerApi/getLedgerAccountBalances", (r) =>
    json(r, NAV_ACCOUNTS.Accounts.map((a, i) => ({ AccountId: a.Id, Balance: eur(i === 1 ? -31240.4 : 123456.78) })))
  );
  await page.route("**/api/LedgerApi/getBudgetAccounts", (r) =>
    json(r, {
      Ok: [
        { Id: { YnabAccountId: fixtureGuid(7001) }, Name: "Tagesgeld", Balance: eur(0), OnBudget: true, Closed: false },
        { Id: { YnabAccountId: fixtureGuid(7002) }, Name: "Bargeld", Balance: eur(0), OnBudget: true, Closed: false },
      ],
    })
  );
}

// ============================================================
// Budget
// ============================================================

export const LONG_CATEGORY = "Eine Kategorie mit einem sehr langen Namen, der am Handy abgeschnitten werden muss";
const CURRENT = "2026-11-01";
const MONTHS = ["2026-10-01", CURRENT, "2026-12-01"];
const monthly = (goal) => ({ Goal: eur(goal), Rhythm: { Months: 1 }, Kind: "Need", Date: null, NeedsWholeAmount: true, Day: 15, CreatedIn: null });
const category = (n, name, assigned, activity, available, extra = {}) => ({
  Id: guid(n),
  Name: name,
  Assigned: eur(assigned),
  Activity: eur(activity),
  Available: eur(available),
  Underfunded: null,
  Target: null,
  Pause: null,
  OpenWriteBack: null,
  ...extra,
});

const budgetMonth = (key) => ({
  Month: key,
  ToBeBudgeted: eur(-12345.67),
  Groups: [
    {
      Name: "Alltag",
      Categories: [
        category(1, "Essen", 0, -921.1, -12.5, { Target: monthly(1600), Underfunded: eur(1600) }),
        category(2, LONG_CATEGORY, 12345.67, -9876.54, 12345.67, {
          Target: { Goal: eur(12160), Rhythm: { Months: 11 }, Kind: "Need", Date: null, NeedsWholeAmount: true, Day: null, CreatedIn: null },
          Underfunded: eur(12160),
        }),
        // budgeting-dgrp7: die Markierung "noch nicht in YNAB" neben vierstelligen Beträgen.
        category(3, "Kleidung", 1600, 0, 1600, { Target: monthly(1600), Underfunded: eur(0), OpenWriteBack: guid(77) }),
        category(4, "Reise", 0, 0, 0, { Target: monthly(1600), Underfunded: eur(1600), Pause: "PausedInBb" }),
        category(5, "Urlaub", 0, 0, 0),
      ],
    },
    { Name: "Rücklagen mit einem langen Gruppennamen", Categories: Array.from({ length: 30 }, (_, i) => category(10 + i, `Kategorie ${i + 1}`, 10, 0, 10)) },
  ],
});
const BUDGET_ORDER = budgetMonth(CURRENT).Groups.flatMap((g) => g.Categories.map((c) => c.Id));
/// Derselbe Monat ohne Kategorie im Minus: die Übersicht zeigt dann kein „Im Minus“.
const budgetMonthWithoutOverdrawn = (key) => {
  const month = budgetMonth(key);
  return {
    ...month,
    Groups: month.Groups.map((g) => ({
      ...g,
      Categories: g.Categories.map((c) => (c.Available.Amount < 0 ? { ...c, Available: eur(0) } : c)),
    })),
  };
};

const categoryEntries = Array.from({ length: 12 }, (_, i) => ({
  EntryId: `00000000-0000-0000-0001-${String(i + 1).padStart(12, "0")}`,
  AccountId: "00000000-0000-0000-0002-000000000001",
  AccountName: "Gemeinsames Girokonto mit langem Namen",
  Date: "2026-11-05T00:00:00.0000000",
  Payee: i === 0 ? "Ein Empfänger mit einem sehr langen Namen, der nicht in eine Zeile passt" : `Empfänger ${i + 1}`,
  Amount: eur(i === 0 ? -12345.67 : -1 - i),
  IsSplitShare: i === 1,
}));

const historyMovement = (n, origin, latest, extra = {}) => ({
  Movement: {
    MovementId: guid(300 + n),
    ApplicationId: guid(400 + n),
    OccurredAt: "2026-11-03T09:30:00.0000000Z",
    Origin: { Decision: origin },
    Month: CURRENT,
    CategoryId: guid(1),
    CategoryName: "Essen",
    OldAssigned: eur(1550),
    NewAssigned: eur(12345.67),
    Counterpart: null,
    Latest: latest,
    ReplacedBy: null,
    ...extra,
  },
});
const categoryHistory = {
  Entries: [
    historyMovement(1, "MoneyAssigned", { Failed: "YNAB hat die Zuweisung abgelehnt, weil das Budget gerade von einer anderen Sitzung geändert wird" }),
    historyMovement(2, "MoneyMoved", "Written", { Counterpart: LONG_CATEGORY }),
    historyMovement(3, "TargetsApplied", "Written"),
  ],
  Spending: ["2026-06-01", "2026-07-01", "2026-08-01", "2026-09-01", "2026-10-01", CURRENT].map((month, i) => ({
    Month: month,
    Activity: eur(i === 3 ? 85 : -12345.67 * (i + 1)),
  })),
};

const previewLine = (n, name) => ({
  CategoryId: guid(n),
  CategoryName: name,
  GroupName: "Alltag",
  OldAssigned: eur(12345.67),
  Underfunded: eur(12160),
  NewAssigned: eur(24505.67),
  Target: monthly(12160),
});
const targetsPreview = {
  Month: CURRENT,
  Lines: [previewLine(2, LONG_CATEGORY), ...Array.from({ length: 39 }, (_, i) => previewLine(10 + i, `Kategorie ${i + 1}`))],
  Skipped: [{ CategoryId: guid(4), CategoryName: "Reise, pausiert bis zum nächsten Frühjahr", Underfunded: eur(1600) }],
  Total: eur(474240),
  ToBeBudgeted: eur(5000),
  ToBeBudgetedAfter: eur(-469240),
  CommandId: null,
};

// Fix 5850313 (budgeting-p4n6r, ADR 0075): die Antworten auf Anwenden. Vorher standen dort `ErrorDisplay.warning`
// und `Badge.warning`, die Pink zeichneten. Eine Zeile steht in YNAB anders, Zu verteilen weicht ab.
const targetsReport = {
  ApplicationId: guid(90),
  Month: CURRENT,
  Lines: targetsPreview.Lines.map((l, i) => ({
    CategoryId: l.CategoryId,
    CategoryName: l.CategoryName,
    OldAssigned: l.OldAssigned,
    NewAssigned: l.NewAssigned,
    Outcome: i === 0 ? "ChangedInYnab" : "Written",
  })),
  ActualToBeBudgeted: eur(-469000),
};
const mockTargetsAnswer = (answer) => async (page) => {
  await mockBudget(page);
  await page.route("**/api/BudgetApi/applyTargets", (r) => json(r, { Ok: answer }));
};
const openTargetsAndApply = (shown) => async (page) => {
  await openBudget(page);
  await page.locator('[data-testid="month-start-apply"]:visible').click();
  await page.locator('[data-testid="targets-total"]:visible').waitFor();
  await page.getByRole("button", { name: "Anwenden", exact: true }).locator("visible=true").click();
  await page.locator(`[data-testid="${shown}"]:visible`).first().waitFor();
};

const generalMovement = (n, group, origin, name, latest, counterpart = null) => ({
  Movement: {
    MovementId: guid(500 + n),
    ApplicationId: guid(group),
    OccurredAt: "2026-11-03T09:30:00.0000000Z",
    Origin: { Decision: origin },
    Month: CURRENT,
    CategoryId: guid(600 + n),
    CategoryName: name,
    OldAssigned: eur(1500),
    NewAssigned: eur(12345.67),
    Counterpart: counterpart,
    Latest: latest,
    ReplacedBy: null,
  },
});
const budgetHistory = [
  generalMovement(1, 11, "MoneyMoved", LONG_CATEGORY, "Written", "Essen/Einkaufen"),
  generalMovement(2, 11, "MoneyMoved", "Essen/Einkaufen", "Written", LONG_CATEGORY),
  generalMovement(3, 12, "TargetsApplied", "Strom", { Failed: "Rate-Limit" }),
  generalMovement(4, 12, "TargetsApplied", "Miete", "Written"),
  ...Array.from({ length: 30 }, (_, i) => generalMovement(10 + i, 20 + i, "MoneyAssigned", `Kategorie ${i + 1}`, "Written")),
];

const emptyReport = (key) => ({ ApplicationId: guid(9), Month: key, Lines: [], ActualToBeBudgeted: null });

async function mockBudget(page) {
  await mockApp(page);
  await page.route("**/api/BudgetApi/getBudgetFrame", (r) => json(r, { Ok: { CurrentMonth: CURRENT, Months: MONTHS, CategoryOrder: BUDGET_ORDER, MonthStartHidden: [] } }));
  await page.route("**/api/BudgetApi/getBudgetMonth", (r) => {
    const [key] = JSON.parse(r.request().postData() ?? "[]");
    return json(r, { Ok: budgetMonth(key) });
  });
  await page.route("**/api/BudgetApi/getRecentMoveCategories", (r) => json(r, { Ok: [guid(2), guid(1)] }));
  await page.route("**/api/LedgerApi/getCategoryMonthEntries", (r) => json(r, { Ok: categoryEntries }));
  await page.route("**/api/BudgetApi/getCategoryHistory", (r) => json(r, { Ok: categoryHistory }));
  await page.route("**/api/BudgetApi/previewTargets", (r) => json(r, { Ok: targetsPreview }));
  await page.route("**/api/BudgetApi/getBudgetHistory", (r) => json(r, { Ok: budgetHistory }));
  await page.route("**/api/BudgetApi/assignMoney", (r) => json(r, { Ok: emptyReport(CURRENT) }));
  await page.route("**/api/BudgetApi/moveMoney", (r) => json(r, { Ok: emptyReport(CURRENT) }));
}

// Targets prüfen: je Gruppe eine Kategorie, lange Namen und große Beträge, zwölf volle Monate Ausgaben.
const checkSpending = (amount) =>
  Array.from({ length: 12 }, (_, i) => {
    const d = new Date(Date.UTC(2025, 10 + i, 1));
    return { Month: `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, "0")}-01`, Activity: eur(-amount) };
  });
const checkTarget = (goal, rhythm) => ({ Goal: eur(goal), Rhythm: rhythm, Kind: "Need", Date: null, NeedsWholeAmount: true, Day: null, CreatedIn: null });
const checkSource = (n, name, goal, spent, rhythm = { Months: 1 }) => ({
  CategoryId: guid(n),
  Name: name,
  GroupName: "Alltag",
  Target: goal === null ? null : checkTarget(goal, rhythm),
  Spending: checkSpending(spent),
  LastChange: null,
  FirstBooking: null,
});
const targetCheck = {
  Month: CURRENT,
  Today: "2026-11-06",
  Categories: [
    checkSource(1, "Essen", 100, 150),
    checkSource(2, LONG_CATEGORY, 600, 1050, { Years: 1 }),
    checkSource(3, "Kleidung", 100, 100),
    checkSource(4, "Reise", 50, 100, { Weeks: 1 }),
    checkSource(5, "Urlaub", null, 12345.67),
    checkSource(10, "Kategorie 1", 60, 0),
  ],
};

async function mockTargetCheck(page) {
  await mockBudget(page);
  await page.route("**/api/BudgetApi/getTargetCheck", (r) => json(r, { Ok: targetCheck }));
}

// budgeting-pscgb: die Seite Kategorien mit langen Namen von Gruppe und Kategorie.
// budgeting-sn2sn: je Kategorie `Active`, zwei deaktivierte (eine mit langem Namen) für den Abschnitt "Deaktiviert".
const categoryGroups = [
  {
    Id: guid(901),
    Name: "Alltag",
    Categories: [
      { Id: guid(1), Name: "Essen", Active: true },
      { Id: guid(2), Name: LONG_CATEGORY, Active: true },
      { Id: guid(4), Name: "Fitnessstudio", Active: false },
    ],
  },
  {
    Id: guid(902),
    Name: "Eine Gruppe mit einem langen Namen bis an die Grenze",
    Categories: [
      { Id: guid(3), Name: "Kinderkram", Active: true },
      { Id: guid(5), Name: `Alter Kredit ${LONG_CATEGORY}`, Active: false },
    ],
  },
  { Id: guid(903), Name: "Leer", Categories: [] },
];

async function mockCategoryAdmin(page) {
  await mockBudget(page);
  await page.route("**/api/CategoryApi/getCategoryGroups", (r) => json(r, { Ok: categoryGroups }));
}

const budgetRow = (page, name) => page.locator('[data-testid="budget-category-row"]:visible').filter({ has: page.getByText(name, { exact: true }) });
const openBudget = async (page) => page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
const inspector = (page, mobile) => (mobile ? page.locator(".bottom-sheet.fixed-height.active") : page.locator('[data-testid="budget-inspector"]'));
const openInspector = async (page, mobile, name, tab) => {
  await openBudget(page);
  await budgetRow(page, name).click();
  const scope = inspector(page, mobile);
  await scope.waitFor();
  if (tab) await scope.locator('[data-testid="inspector-tabs"]').getByRole("tab", { name: tab }).click();
  return scope;
};
const moneyMode = (page, name) => page.locator('[data-testid="money-mode"]:visible').filter({ hasText: name });

// ============================================================
// Kassenbuch
// ============================================================

const TODAY = new Date().toISOString().slice(0, 10) + "T00:00:00.0000000";
export const LONG_PAYEE = "Ein Zahlungsempfänger mit einem sehr langen Namen, der in keine Spalte passt";

/// Eine Buchung, die als unsicheres Duplikat wartet (banking-import-d0fey, ADR 0093): „doppelt?“ in der Zeile.
export const HELD_PAYEE = "Vielleicht doppelt mit einem langen Zahlungsempfänger";
export const POSSIBLE_DUPLICATE = {
  Reason: "In YNAB steht eine Buchung mit gleichem Betrag am 10.11.2026 bei Markt.",
  MatchDate: "2026-11-10T00:00:00.0000000",
  MatchAmount: eur(-12345.67),
  MatchPayee: "Markt",
  MatchCategoryName: "Lebensmittel",
  MatchEntryId: null,
};

function ledgerEntries(accountId, { held = false } = {}) {
  const entry = (n, extra) => buildEntryFixture(accountId, n, { Date: TODAY, LinkedToYnab: true, CategorizationWriteBackFailure: null, ...extra });
  const lebensmittel = { CategoryId: CATEGORY_IDS.lebensmittel, CategoryName: "Lebensmittel", CategoryGroupName: "Alltag" };
  return [
    ...(held
      ? [entry(10, { Payee: HELD_PAYEE, NeedsReview: true, LinkedToYnab: false, Amount: eur(-12345.67), PossibleDuplicate: POSSIBLE_DUPLICATE, ...lebensmittel })]
      : []),
    entry(1, { Payee: "Bauhaus", NeedsReview: true, Amount: eur(-81.9), ...lebensmittel }),
    entry(2, {
      Payee: "AMAZON PAYMENTS",
      Memo: "304-1234567-7654321 Lampe und ein sehr langes Memo, das weit über die Spalte hinausgeht",
      ExternalLinks: [{ Label: "Bestellung 304-1234567-7654321", Url: "https://www.amazon.de/gp/your-account/order-details?orderID=304-1234567-7654321" }],
      ...lebensmittel,
    }),
    entry(3, { Payee: "PayPal Shop", ExternalLinks: [{ Label: "PayPal Activity", Url: "https://www.paypal.com/activities" }] }),
    entry(4, { Payee: LONG_PAYEE, Amount: eur(-12345.67), CategoryName: LONG_CATEGORY, CategoryGroupName: "Rücklagen mit einem langen Gruppennamen" }),
    entry(5, { Payee: "Rückfrage und Prüfung", NeedsReview: true, Flag: "AskLena" }),
    entry(6, { Payee: "Gelöscht und zu prüfen", NeedsReview: true, DeletedInYnab: true }),
    entry(7, { Payee: "Markiert und zu prüfen", NeedsReview: true, Flag: "Marked" }),
    entry(8, { Payee: "Nicht in YNAB und zu prüfen", NeedsReview: true, LinkedToYnab: false }),
    entry(9, {
      Payee: "Kaufland Split",
      CategoryId: null,
      CategoryName: null,
      CategoryGroupName: null,
      Splits: [
        { Target: { ToCategory: [{ YnabCategoryId: CATEGORY_IDS.lebensmittel }, "Lebensmittel"] }, Amount: eur(-10), Memo: null },
        { Target: "Uncategorized", Amount: eur(-5), Memo: null },
      ],
    }),
    ...Array.from({ length: 40 }, (_, i) => entry(20 + i, { Payee: `Füllbuchung ${i + 1}`, ...lebensmittel })),
  ];
}

async function mockLedger(page, { accounts = 3, held = false } = {}) {
  await mockApp(page);
  const accountsFixture = buildAccountsFixture(accounts);
  accountsFixture.Accounts[2].Name = "Gemeinschaftskonto mit einem sehr langen Kontonamen";
  await mockFilterBarApi(page, {
    accountsFixture,
    balancesFixture: buildBalancesFixture(accountsFixture.Accounts).map((b, i) => (i === 2 ? { ...b, Balance: eur(-12345.67) } : b)),
    resolveEntries: () => ledgerEntries(accountsFixture.DefaultAccountId, { held }),
  });
  for (const [name, body] of [
    ["getEntryHistory", []],
    ["getLedgerEntry", null],
    ["getPayeeCategorySuggestions", [{ YnabCategoryId: CATEGORY_IDS.bahn }]],
  ]) {
    await page.route(`**/api/LedgerApi/${name}`, (r) => json(r, body));
  }
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  // ledger-hg5m5: nach einer Kategorie-Änderung fragt das Detail, ob eine Regel sie schon gibt; keine heißt Angebot.
  await page.route("**/api/RulesApi/ruleCategoryFor", (r) => json(r, null));
  await page.route("**/api/LedgerApi/setEntryCategory", (r) =>
    json(r, { Ok: { Previous: { CategoryId: { YnabCategoryId: CATEGORY_IDS.lebensmittel }, CategoryName: "Lebensmittel" }, Change: "CategoryChanged", WriteBack: "CategorySent" } })
  );
}

const openLedger = async (page) => page.getByText("Bauhaus", { exact: true }).locator("visible=true").first().waitFor();
const ledgerSheet = (page) => page.locator(".bottom-sheet.active").first();
const openLedgerDetail = async (page, payee) => {
  await page.getByText(payee, { exact: true }).first().waitFor();
  await page.getByText(payee, { exact: true }).first().tap();
  const sheet = ledgerSheet(page);
  await sheet.waitFor();
  return sheet;
};

/// Kategorie-Filter „Lebensmittel“, dann Bauhaus im Detail auf „Nebenkosten“: die Zeile passt nicht mehr zum
/// Filter und bleibt ausgegraut stehen (ledger-dn4vy, Vorlage scripts/ledger-category-change.test.mjs).
const openFilteredOut = async (page, { mobile }) => {
  await openLedger(page);
  await page.getByRole("button", { name: /^Kategorie/ }).locator("visible=true").first().click();
  const layer = page.locator('.bottom-sheet.active, [data-layer="popover"]').first();
  await layer.getByText("Lebensmittel", { exact: true }).last().click();
  await layer.getByRole("button", { name: "Übernehmen (1)" }).click();
  await page.getByRole("button", { name: "Kategorie: 1" }).locator("visible=true").first().waitFor();
  await openLedger(page);
  const payee = page.getByText("Bauhaus", { exact: true }).locator("visible=true").first();
  if (mobile) await payee.tap();
  else await payee.click();
  const arrow = page.locator('[data-testid="detail-category-arrow"]').filter({ visible: true }).first();
  await arrow.click();
  await page.locator('[role="option"]', { hasText: "Nebenkosten" }).filter({ visible: true }).last().click();
  await page.locator('[data-toast-action="true"]').first().waitFor();
  await page.locator('[data-testid="filtered-out-note"]').filter({ visible: true }).first().waitFor();
  // Erst wenn der Picker zu ist und das Detail steht, schließt „Schließen“ es.
  await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 1);
  await settle(page, { where: "ausgegraute Zeile, Detail offen" });
  await ledgerSheet(page).getByRole("button", { name: "Schließen" }).click();
  await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
};

/// Die Gesamtsicht aus der Konten-Spalte: gewartet wird auf die Liste aller Konten.
const openAllAccounts = async (page) => {
  await openLedger(page);
  const loaded = page.waitForResponse("**/api/LedgerApi/getLedgerEntriesPage");
  await page.locator(".ledger-account-strip, aside").getByText("Alle Konten", { exact: true }).filter({ visible: true }).first().click();
  await loaded;
  await frames(page);
};

/// Die Auswahl am Handy: langes Drücken wie in ledger-selection.test.mjs, gehalten, bis die Leiste erscheint.
const openMobileSelection = async (page) => {
  await openLedger(page);
  const row = page.locator("button", { hasText: "Bauhaus" }).filter({ visible: true }).first();
  const b = await row.boundingBox();
  const init = { pointerType: "touch", pointerId: 7, isPrimary: true, bubbles: true, clientX: b.x + b.width / 2, clientY: b.y + b.height / 2 };
  await row.dispatchEvent("pointerdown", init);
  const bar = page.locator(".selection-bar").filter({ visible: true });
  await bar.waitFor();
  await row.dispatchEvent("pointerup", init);
  await row.dispatchEvent("click", { bubbles: true, clientX: init.clientX, clientY: init.clientY });
  await bar.getByRole("button", { name: /^Lena fragen/ }).waitFor();
};

// ============================================================
// Regeln
// ============================================================

const RULE_TIME = "2026-01-01T00:00:00.0000000Z";
const rule = (n, name, categoryNumber) => ({
  Id: W("RuleId", guid(n)),
  Name: name,
  Pattern: `muster${n}`,
  PatternType: "Contains",
  TargetField: "Combined",
  CategoryId: W("YnabCategoryId", guid(categoryNumber)),
  CategoryName: categoryNumber === 2 ? LONG_CATEGORY : `Kategorie ${categoryNumber}`,
  PayeeOverride: null,
  Priority: 10 - n,
  Enabled: true,
  CreatedAt: RULE_TIME,
  UpdatedAt: RULE_TIME,
});

async function mockRules(page) {
  await mockApp(page);
  await page.route("**/api/YnabApi/getCategories", (r) =>
    json(r, { Ok: Array.from({ length: 12 }, (_, i) => ({ Id: W("YnabCategoryId", guid(i + 1)), Name: i === 1 ? LONG_CATEGORY : `Kategorie ${i + 1}`, GroupName: "Alltag", Available: null, Hidden: false })) })
  );
  await page.route("**/api/RulesApi/getAllRules", (r) => json(r, [rule(1, "Regel eins", 1), rule(2, "Eine Regel mit einem sehr langen Namen für die Zeile", 2), rule(3, "Regel drei", 3)]));
  await page.route("**/api/RulesApi/testRule", (r) => json(r, { Ok: true }));
}
const openRules = async (page) => page.getByText("Regel eins").locator("visible=true").first().waitFor();
const openRule = async (page, name) => {
  await openRules(page);
  await page.locator('[data-testid="rule-row"]:visible').filter({ hasText: name }).first().click();
  await page.locator('[data-testid="rule-form"]').waitFor();
};

// ============================================================
// Einstellungen (banking-import-t4raw)
// ============================================================

/// Comdirect eingerichtet und verbunden: die Karte zeigt Schalter „Automatisch abrufen“ und das Auswahlfeld „Alle“.
async function mockSettings(page) {
  await mockApp(page);
  await page.route("**/api/SettingsApi/getSettings", (r) =>
    json(r, {
      Comdirect: { HasClientId: true, HasClientSecret: true, HasUsername: true, HasPassword: true, AccountId: "kuenstliches-konto" },
      Ynab: { HasToken: true, DefaultBudgetId: W("YnabBudgetId", "fake-budget-id"), DefaultAccountId: null, QuickAddAccountId: null },
      Sync: { DaysToFetch: 14, AutoFetchEnabled: true, AutoFetchIntervalMinutes: 360 },
    })
  );
}
const openSettings = async (page) => page.locator('[data-testid="auto-fetch-switch"]').waitFor();

// ============================================================
// Abrufen (banking-import-d0fey)
// ============================================================

const SYNC_ID = { SyncSessionId: "00000000-0000-4000-8000-0000000a5a01" };
/// Eine Sitzung, wie der Server sie liefert (`SyncSession`).
export const syncSession = (Status, Counts = {}) => ({
  Id: SYNC_ID,
  StartedAt: TODAY,
  CompletedAt: null,
  Status,
  Trigger: "Manual",
  Counts: { NewEntries: 0, ByRule: 0, ByOrderId: 0, WithoutCategory: 0, SafeDuplicatesSkipped: 0, Held: 0, NotInYnab: 0, FutureDated: 0, ...Counts },
});
/// Das Ergebnis des Prototyps, mit Bestellnummer und nicht in YNAB (Abweichung (b)), damit der Satz am Handy umbricht.
export const FETCH_RESULT = { NewEntries: 6, ByRule: 2, ByOrderId: 1, WithoutCategory: 3, SafeDuplicatesSkipped: 2, Held: 1, NotInYnab: 1, FutureDated: 0 };

/// Die Übersicht mit dem Sheet „Abrufen“: `startSync` antwortet mit `start`, `confirmTan` mit dem fertigen Abruf.
const mockFetch = (start) => async (page) => {
  await mockOverview(page);
  await page.route("**/api/SyncApi/startSync", (r) => json(r, { Ok: start }));
  await page.route("**/api/SyncApi/confirmTan", (r) => json(r, { Ok: syncSession("Completed", FETCH_RESULT) }));
};
const fetchLayer = (page) => page.locator('[data-layer="dialog"]').or(page.locator(".bottom-sheet.active").first());
const openFetch = (ready) => async (page) => {
  await openOverview(page);
  await page.getByRole("button", { name: "Abrufen" }).locator("visible=true").first().click();
  await fetchLayer(page).locator(`[data-testid="${ready}"]`).waitFor();
};

// ============================================================
// Übersicht (design-system-k6kfw)
// ============================================================

/// Monat und neueste Buchungen der Übersicht: lange Payees, eine Buchung ohne Kategorie, große Beträge.
async function mockOverview(page) {
  await mockBudget(page);
  const accountId = NAV_ACCOUNTS.DefaultAccountId;
  const entries = Array.from({ length: 6 }, (_, n) =>
    buildEntryFixture(accountId, n, n === 0 ? { Payee: LONG_PAYEE, CategoryName: null, Amount: eur(-123456.78) } : {})
  );
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (r) =>
    json(r, { Entries: entries, TotalCount: 6, TotalAmount: eur(-1000), DeletedCount: 0, NeedsReviewCount: 0, AskLenaCount: 0 })
  );
  // „Zu tun“ (ledger-s1weh): keine Buchung außerhalb von YNAB, kein bekannter Import. Die Einträge kommen aus dem Monat.
  await page.route("**/api/LedgerApi/getEntriesNotInYnab", (r) => json(r, []));
  await page.route("**/api/SyncApi/getSyncHistory", (r) => json(r, []));
}
/// Die Übersicht steht, sobald der Monat da ist oder sein Platzhalter (beim verzögerten Monat, Invariante 9).
const openOverview = async (page) =>
  page.locator('[data-testid="overview-month"], [data-testid="overview-month-loading"]').first().waitFor();

// ============================================================
// Durchgehen (ledger-v6s7c)
// ============================================================

/// Die Karten der Schlange: lange Namen und große Beträge (Invariante 4). `REVIEW_QUEUE.<art>` ist je eine Buchung.
export const REVIEW_QUEUE = (() => {
  const accountId = NAV_ACCOUNTS.DefaultAccountId;
  const lebensmittel = { CategoryId: CATEGORY_IDS.lebensmittel, CategoryName: "Lebensmittel", CategoryGroupName: "Alltag" };
  const base = { Date: TODAY, NeedsReview: true, LinkedToYnab: true, Origin: "ImportedFromComdirect", CategorizationWriteBackFailure: null };
  return {
    held: buildEntryFixture(accountId, 61, {
      ...base,
      Payee: HELD_PAYEE,
      LinkedToYnab: false,
      Amount: eur(-12345.67),
      CategoryId: null,
      CategoryName: null,
      CategoryGroupName: null,
      PossibleDuplicate: { ...POSSIBLE_DUPLICATE, MatchDate: "2025-03-10T00:00:00.0000000", Reason: "Gleicher Betrag, gleicher Tag, gleicher Payee, aber eine andere Bank-Referenz." },
    }),
    plain: buildEntryFixture(accountId, 62, { ...base, Payee: LONG_PAYEE, Amount: eur(-123456.78), ...lebensmittel }),
    amazon: buildEntryFixture(accountId, 63, {
      ...base,
      Payee: "AMAZON PAYMENTS",
      Origin: "MirroredFromYnab",
      Amount: eur(-21.5),
      Flag: "AskLena",
      CategoryFromOrderId: true,
      ...lebensmittel,
      ExternalLinks: [
        { Label: "Bestellung 302-7654321-2222222", Url: "https://www.amazon.de/gp/your-account/order-details?orderID=302-7654321-2222222" },
      ],
    }),
    open: buildEntryFixture(accountId, 64, { ...base, Payee: "Markt ohne Kategorie", Amount: eur(-42.8), CategoryId: null, CategoryName: null, CategoryGroupName: null }),
  };
})();

/// Die Seite „Durchgehen“ mit dieser Schlange. Das Neulesen einer Karte liefert ihren Stand aus `state`, damit
/// Befehle (Kategorie, Prüfvermerk, Verwerfen) ihn ändern können; `state.calls` sammelt die Befehle.
export async function mockReview(page, queue) {
  await mockApp(page);
  const state = { byId: new Map(queue.map((e) => [e.Id, { ...e }])), calls: [] };
  const answer = (r, body) => json(r, body);
  await page.route("**/api/LedgerApi/getReviewQueue", (r) => answer(r, queue.map((e) => state.byId.get(e.Id))));
  await page.route("**/api/LedgerApi/getLedgerEntry", (r) => {
    const [id] = r.request().postDataJSON();
    return answer(r, state.byId.get(id) ?? null);
  });
  await page.route("**/api/LedgerApi/getPayeeCategorySuggestions", (r) =>
    answer(r, [{ YnabCategoryId: CATEGORY_IDS.bahn }, { YnabCategoryId: CATEGORY_IDS.nebenkosten }])
  );
  await page.route("**/api/LedgerApi/getEntryHistory", (r) => answer(r, []));
  await page.route("**/api/LedgerApi/getLedgerPayeeSuggestions", (r) => answer(r, []));
  await page.route("**/api/LedgerApi/setEntryCategory", (r) => {
    const [id, category] = r.request().postDataJSON();
    state.calls.push({ endpoint: "setEntryCategory", id, category });
    const entry = state.byId.get(id);
    const categoryId = category && category.YnabCategoryId;
    state.byId.set(id, { ...entry, CategoryId: categoryId, CategoryName: "Lebensmittel", CategoryGroupName: "Alltag" });
    return answer(r, { Ok: { Previous: null, Change: "CategoryChanged", WriteBack: "CategorySent", PreviousTransferAccountId: null } });
  });
  await page.route("**/api/LedgerApi/setReviewMark", (r) => {
    const [id] = r.request().postDataJSON();
    state.calls.push({ endpoint: "setReviewMark", id });
    state.byId.set(id, { ...state.byId.get(id), NeedsReview: false });
    return answer(r, { Ok: { Previous: { IsApproved: false, ClearedState: "ReviewUncleared" }, Change: "ReviewMarkChanged", WriteBack: "ReviewMarkSent" } });
  });
  await page.route("**/api/LedgerApi/setLenaFlag", (r) => {
    const [id, target] = r.request().postDataJSON();
    state.calls.push({ endpoint: "setLenaFlag", id, target });
    return answer(r, { Ok: { Previous: { Color: null, Name: null }, Change: "FlagChanged", WriteBack: "FlagSent" } });
  });
  await page.route("**/api/LedgerApi/discardDuplicateEntry", (r) => {
    const [id] = r.request().postDataJSON();
    state.calls.push({ endpoint: "discardDuplicateEntry", id });
    state.byId.set(id, { ...state.byId.get(id), DeletedInYnab: true });
    return answer(r, { Ok: null });
  });
  return state;
}

const reviewOpen = (testId) => async (page) => page.locator(`[data-testid="${testId}"]`).first().waitFor();
const reviewPrimary = (name) => (page) => page.getByRole("button", { name, exact: true }).locator("visible=true").first();

// ============================================================
// Die Zustände
// ============================================================

export const STATES = [
  // Bug F3 (budgeting-kjsht), Bug D3, budgeting-c6vn4: Zeilen mit Target am Handy, sticky Kopf, Breiten.
  {
    name: "budget",
    route: "/budget",
    widths: PAGE_WIDTHS,
    mock: mockBudget,
    open: openBudget,
  },
  // budgeting-c5w2n: Tab Geld im Sheet, Feld und Knopf ohne Scrollen, auch bei offener Tastatur.
  {
    name: "budget-geld-zuweisen",
    route: "/budget",
    widths: HANDY_WIDTHS,
    mock: mockBudget,
    open: async (page, { mobile }) => {
      await openInspector(page, mobile, "Essen");
      await moneyMode(page, "Zuweisen").click();
      await page.locator('[data-testid="assign-field"] input:visible').waitFor();
    },
    primary: (page) => page.locator('.bottom-sheet.active [data-testid="assign-missing"] button'),
    // Die einzige Ausnahme im Sweep: bei 320 px messen die sechs Schritte 44,7 × 48 px. Sechs Spalten passen
    // dort nicht auf 48 px, zwei Zeilen schieben den Knopf aus dem 500-px-Sheet (budgeting-c5w2n). Steht in der
    // Kann-Sammlung budgeting (infrastructure-nx14s).
    exceptions: [{ width: 320, inv: 2, selector: 'button[data-testid="assign-step"]' }],
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="assign-field"] input'),
      reach: (page) => page.locator('.bottom-sheet.active [data-testid="assign-field"] input, .bottom-sheet.active [data-testid="assign-available-after"]'),
    },
  },
  // Lange Kategorienamen in von, nach und im Knopf (budget-money-tab), am Handy und im Panel.
  {
    name: "budget-geld-abgeben",
    route: "/budget",
    widths: [...HANDY_WIDTHS, 1280],
    mock: mockBudget,
    open: async (page, { mobile }) => {
      await openInspector(page, mobile, "Essen");
      await moneyMode(page, "Abgeben").click();
      await page.locator('[data-testid="transfer-amount"] input:visible').fill("5");
      await page.locator('[data-testid="transfer-to"]:visible').click();
      await page.locator('[role="option"]:visible').filter({ hasText: LONG_CATEGORY }).last().click();
      await page.locator('[data-testid="transfer-to"]:visible').getByText(LONG_CATEGORY).waitFor();
      // Die Auswahl ersetzt den Inhalt des Sheets (§7.15, ledger-ea70s): warten, bis der Inhalt zurück ist.
      await page.locator('[data-testid="transfer-amount"] input:visible').waitFor();
    },
  },
  // budgeting-gffps, budgeting-nfrds: der Kategorie-Picker als Sheet am Handy (16 px Suche) und als Popover
  // im Panel (ganz sichtbar, vom Panel nicht abgeschnitten, Fuß nicht verdeckt).
  {
    name: "budget-picker",
    route: "/budget",
    widths: [...HANDY_WIDTHS, 1280],
    height: 800,
    mock: mockBudget,
    open: async (page, { mobile }) => {
      await openInspector(page, mobile, "Essen");
      await moneyMode(page, "Abgeben").click();
      await page.locator('[data-testid="transfer-amount"] input:visible').waitFor();
      // Fest am Seitenanfang: der Klick auf die Zeile scrollte die Seite sonst je Lauf verschieden weit.
      await page.evaluate(() => window.scrollTo({ top: 0, behavior: "instant" }));
      await page.locator('[data-testid="transfer-to"]:visible').click();
      await page.locator('[role="option"]:visible').first().waitFor();
      // ledger-ea70s: am Handy ersetzt die Auswahl den Inhalt des Inspektor-Sheets (§7.15), kein zweites Sheet.
      if (mobile && (await page.locator(".bottom-sheet.active").count()) !== 1)
        throw new Error("Die Auswahl beim Verschieben liegt als zweites Sheet über dem Inspektor");
    },
    // Am PC hat das Popover einen Fuß, am Handy wählt ein Tipp im Sheet ohne Knopf.
    primary: (page, { mobile }) => (mobile ? null : page.locator('[data-testid="category-picker-footer-hint"]:visible')),
  },
  // budgeting-m1bce: Buchungen im Sheet, ein großer Betrag überlappt bei schmaler Breite nicht.
  {
    name: "budget-buchungen",
    route: "/budget",
    widths: [...HANDY_WIDTHS, 1440],
    mock: mockBudget,
    open: async (page, { mobile }) => {
      await openInspector(page, mobile, "Essen", "Buchungen");
      await page.locator('[data-testid="detail-entry"]:visible').first().waitFor();
    },
  },
  // budgeting-x4k7p, budgeting-v6m3z: Tab Historie mit langem Fehlgrund und Ausgaben-Balken.
  {
    name: "budget-historie-tab",
    route: "/budget",
    widths: [...HANDY_WIDTHS, 1440],
    mock: mockBudget,
    open: async (page, { mobile }) => {
      await openInspector(page, mobile, LONG_CATEGORY, "Historie");
      await page.locator('[data-testid="month-bar"]:visible').first().waitFor();
      await page.locator('[data-testid="month-bar"]:visible').last().click();
    },
  },
  // budgeting-v6m3z: die Wertzeile des ersten Balkens bleibt am Handy im Sheet (der letzte steht oben).
  {
    name: "budget-historie-erster-balken",
    route: "/budget",
    widths: HANDY_WIDTHS,
    mock: mockBudget,
    open: async (page, { mobile }) => {
      await openInspector(page, mobile, LONG_CATEGORY, "Historie");
      await page.locator('[data-testid="month-bar"]:visible').first().waitFor();
      await page.locator('[data-testid="month-bar"]:visible').first().click();
    },
  },
  // Aus dem früheren Layout-Test der Budget-Seite: bei 20 px Grundschrift haftet der Kopf am PC, ohne Panel
  // oder Spaltenkopf zu überdecken.
  {
    name: "budget-grosse-schrift",
    route: "/budget",
    widths: [1440],
    mock: mockBudget,
    open: async (page) => {
      await openBudget(page);
      await page.addStyleTag({ content: "html { font-size: 20px; }" });
      await openInspector(page, false, "Kategorie 1");
    },
  },
  // budgeting-k8wq3: Tab Target einer Kategorie mit Target, Target speichern ohne Scrollen im 500-px-Sheet.
  {
    name: "budget-target",
    route: "/budget",
    widths: [...HANDY_WIDTHS, 1440],
    mock: mockBudget,
    open: async (page, { mobile }) => {
      await openInspector(page, mobile, "Essen", "Target");
      await page.locator('[data-testid="target-amount"] input:visible').waitFor();
    },
    primary: (page) => page.locator('[data-testid="target-save"] button:visible'),
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="target-amount"] input'),
      reach: (page) => page.locator('.bottom-sheet.active [data-testid="target-amount"] input'),
    },
  },
  // budgeting-xp46a, budgeting-g2ty9: Target anlegen ohne Scrollen, Platzhalter des Datums ganz im Feld.
  {
    name: "budget-target-anlegen",
    route: "/budget",
    widths: [...HANDY_WIDTHS, 1440],
    // Invariante 10 (ledger-dn4vy): „Verfügbar“ 0,00 € in `text-muted` hat auf seiner Fläche 4,44:1. Kann-Sammlung
    // budgeting. Auf den Text des Treffers verengt, ein anderer Betrag in der Spalte fällt weiter auf.
    exceptions: [
      { width: 1440, inv: 10, selector: '[data-testid="budget-available"] span "0,00 €"' },
    ],
    mock: mockBudget,
    open: async (page, { mobile }) => {
      await openInspector(page, mobile, "Urlaub", "Target");
      await page.locator('[data-testid="target-amount"] input:visible').waitFor();
    },
    primary: (page) => page.locator('[data-testid="target-save"] button:visible'),
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="target-amount"] input'),
      reach: (page) => page.locator('.bottom-sheet.active [data-testid="target-amount"] input'),
    },
  },
  // budgeting-xp46a: die Warnung vor dem Wechsel der Art im Sheet ohne Scrollen.
  {
    name: "budget-target-warnung",
    route: "/budget",
    widths: HANDY_WIDTHS,
    mock: mockBudget,
    open: async (page, { mobile }) => {
      const scope = await openInspector(page, mobile, "Essen", "Target");
      await scope.getByRole("button", { name: "Jahr" }).click();
      await scope.getByRole("button", { name: "Target speichern" }).click();
      await scope.getByRole("alertdialog").waitFor();
    },
    primary: (page) => page.locator(".bottom-sheet.active").getByRole("alertdialog").getByRole("button").last(),
  },
  // budgeting-h2zfn, budgeting-kjsht: Targets anwenden, Summe und Anwenden ohne Scrollen bei langer Liste.
  {
    name: "budget-targets-anwenden",
    route: "/budget",
    widths: [...HANDY_WIDTHS, 1440],
    mock: mockBudget,
    open: async (page) => {
      await openBudget(page);
      await page.locator('[data-testid="month-start-apply"]:visible').click();
      await page.locator('[data-testid="targets-total"]:visible').waitFor();
    },
    primary: (page) => page.getByRole("button", { name: "Anwenden", exact: true }).locator("visible=true"),
    reach: (page) => page.locator('[data-testid="targets-total"]:visible, [data-testid="targets-after"]:visible'),
  },
  // Fix 5850313 (ADR 0075): Anwenden meldet, dass sich der Stand geändert hat. Der Hinweis zeichnete vorher Pink
  // (`ErrorDisplay.warning`), die Farbregel des Sweeps hält ihn gelb.
  {
    name: "budget-targets-stand-geaendert",
    route: "/budget",
    widths: [...HANDY_WIDTHS, 1440],
    mock: mockTargetsAnswer({ PreviewChanged: targetsPreview }),
    open: openTargetsAndApply("targets-changed"),
  },
  // Fix 5850313 (ADR 0075): der Bericht mit einer Zeile „in YNAB geändert“ (vorher `Badge.warning`) und dem Hinweis,
  // dass Zu verteilen von der Vorschau abweicht (vorher `ErrorDisplay.warning`). Beides zeichnete Pink.
  {
    name: "budget-targets-bericht",
    route: "/budget",
    widths: [...HANDY_WIDTHS, 1440],
    mock: mockTargetsAnswer({ Applied: targetsReport }),
    open: async (page) => {
      await openTargetsAndApply("targets-differs")(page);
      await page.getByText("in YNAB geändert").locator("visible=true").first().waitFor();
    },
  },
  // budgeting-c6vn4, budgeting-a3k7m: die allgemeine Historie, Kopf einer Verschiebung bricht um statt abzuschneiden.
  {
    name: "budget-historie",
    route: "/budget/history",
    widths: PAGE_WIDTHS,
    // Invariante 10 (ledger-dn4vy): der rote Satz „1 von 2 fehlgeschlagen“ hat 4,46:1 auf seiner Fläche. Kann-Sammlung
    // budgeting.
    exceptions: [
      { width: 768, inv: 10, selector: '[data-testid="general-history-partly-failed"] span' },
      { width: 900, inv: 10, selector: '[data-testid="general-history-partly-failed"] span' },
      { width: 1024, inv: 10, selector: '[data-testid="general-history-partly-failed"] span' },
      { width: 1280, inv: 10, selector: '[data-testid="general-history-partly-failed"] span' },
      { width: 1440, inv: 10, selector: '[data-testid="general-history-partly-failed"] span' },
      { width: 1920, inv: 10, selector: '[data-testid="general-history-partly-failed"] span' },
    ],
    mock: mockBudget,
    open: async (page) => {
      const run = page.locator('[data-testid="general-history-run"]').first();
      await run.waitFor();
      await page.locator('[data-testid="general-history-toggle"]:visible, [data-testid="general-history-run"] button[aria-expanded]:visible').first().click();
    },
  },
  // budgeting-q4v8n, budgeting-c4ve4: die Seite Targets prüfen mit langen Namen, Knöpfen je Zeile und Mini-Balken.
  {
    name: "targets-pruefen",
    route: "/budget/target-check",
    widths: PAGE_WIDTHS,
    mock: mockTargetCheck,
    open: async (page) => page.locator('[data-testid="target-check-row"]').first().waitFor(),
  },
  // budgeting-c4ve4: Anpassen öffnet am PC das Panel rechts, am Handy ein Sheet.
  {
    name: "targets-pruefen-anpassen",
    route: "/budget/target-check",
    widths: [...HANDY_WIDTHS, 1280],
    mock: mockTargetCheck,
    open: async (page) => {
      const row = page.locator('[data-testid="target-check-row"]').filter({ hasText: "Essen" });
      await row.getByRole("button", { name: "Anpassen …" }).click();
      await page.locator('[data-testid="target-check-form"]:visible').first().waitFor();
    },
  },
  // budgeting-pscgb: die Seite Kategorien (Budget, Menü "Kategorien verwalten").
  {
    name: "kategorien",
    route: "/budget/categories",
    widths: PAGE_WIDTHS,
    mock: mockCategoryAdmin,
    open: async (page) => page.locator('[data-testid="categories-row"]').first().waitFor(),
  },
  // budgeting-sn2sn: der Abschnitt "Deaktiviert (n)" aufgeklappt, mit "Aktivieren" je Kategorie.
  {
    name: "kategorien-deaktiviert",
    route: "/budget/categories",
    widths: PAGE_WIDTHS,
    mock: mockCategoryAdmin,
    open: async (page) => {
      await page.locator('[data-testid="categories-inactive-toggle"]').click();
      await page.locator('[data-testid="categories-inactive-row"]').first().waitFor();
    },
  },
  // budgeting-pscgb: Tipp auf eine Kategorie öffnet das Formular, am PC als Panel rechts, am Handy im Sheet.
  {
    name: "kategorien-bearbeiten",
    route: "/budget/categories",
    widths: [...HANDY_WIDTHS, 1280],
    mock: mockCategoryAdmin,
    open: async (page) => {
      await page.locator('[data-testid="categories-row"]').filter({ hasText: "Essen" }).click();
      await page.locator('[data-testid="categories-form"]:visible').first().waitFor();
    },
  },
  // ledger-qh9qx, ledger-qns4j (AC5), Regression ledger-filter-bar: Spaltenköpfe, Statuszelle mit zwei
  // Kennzeichen, die Filterleiste haftet ohne den Tabellenkopf zu verdecken.
  {
    name: "kassenbuch",
    route: "/ledger",
    widths: [...new Set([...PAGE_WIDTHS, ...TABLE_WIDTHS])].sort((a, b) => a - b),
    mock: mockLedger,
    open: openLedger,
  },
  // ledger-qh9qx (Fix 38f2118): in der Gesamtsicht mit Konto-Spalte blendet die Tabelle in schmalen Fenstern
  // stufenweise Spalten aus (Memo, dann Konto), statt Payee, Kategorie und Memo zusammenzudrücken. Der Zustand
  // `kassenbuch` zeigt das Standardkonto, also die Ansicht eines Kontos ohne Konto-Spalte.
  {
    name: "kassenbuch-alle-konten",
    route: "/ledger",
    widths: TABLE_WIDTHS,
    mock: mockLedger,
    open: openAllAccounts,
  },
  // Die Navbar ist `h-16` (4rem): bei 20 px Grundschrift wächst sie, Filterleiste, Tabellenkopf und
  // Konten-Spalte haften trotzdem darunter (`--nav-height` in styles.css).
  {
    name: "kassenbuch-grosse-schrift",
    route: "/ledger",
    widths: [1440],
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await page.addStyleTag({ content: "html { font-size: 20px; }" });
    },
  },
  // design-system-btwhg: Datumsfeld und Kategorie-Suche der Filterleiste am Handy mit 16 px.
  {
    name: "kassenbuch-filter-kategorie",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await page.getByRole("button", { name: /^Kategorie/ }).locator("visible=true").first().click();
      await page.locator(".bottom-sheet.active").waitFor();
    },
  },
  {
    name: "kassenbuch-filter-zeitraum",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await page.getByRole("button", { name: /^Zeitraum/ }).locator("visible=true").first().click();
      await page.locator(".bottom-sheet.active").waitFor();
    },
  },
  // ledger-dn4vy: Betrag Von/Bis im Filter-Sheet. Vorher ohne Namen (der Zustand fand das Feld nicht), ohne
  // Zifferntastatur und 36 px hoch; mit unlesbarem Bis steht der Grund am Feld.
  {
    name: "kassenbuch-filter-betrag",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await page.getByRole("button", { name: "Betrag", exact: true }).locator("visible=true").first().click();
      const sheet = page.locator(".bottom-sheet.active");
      await sheet.waitFor();
      const from = sheet.getByRole("textbox", { name: "Betrag von" });
      const to = sheet.getByRole("textbox", { name: "Betrag bis" });
      // Beide unlesbar: ein gültiger Wert setzte den Filter, Chip und „Alle Filter löschen“ hinter dem Sheet
      // gehören nicht zu diesem Zustand.
      await from.fill("abc");
      await to.fill("50x");
      for (const field of [from, to]) {
        const mode = await field.getAttribute("inputmode");
        if (mode !== "decimal") throw new Error(`Betragsfeld ohne Zifferntastatur: inputmode ${mode}`);
      }
      await sheet.getByText("Betrag nicht lesbar, etwa 12,50").nth(1).waitFor();
    },
  },
  // ledger-dn4vy: die Payee-Vorschläge im Filter-Sheet. Vorher `py-2.5` ohne Mindesthöhe (rund 40 px).
  {
    name: "kassenbuch-filter-payee",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/getLedgerPayeeSuggestions", (r) =>
        json(r, [{ Payee: "Bauhaus", Count: 9 }, { Payee: LONG_PAYEE, Count: 4 }, { Payee: "Baumarkt", Count: 2 }])
      );
    },
    open: async (page) => {
      await openLedger(page);
      await page.getByRole("button", { name: "Payee", exact: true }).locator("visible=true").first().click();
      await page.locator('.bottom-sheet.active [role="option"]').first().waitFor();
    },
  },
  // ledger-dn4vy: die ausgegraute Zeile („passt nicht mehr zum Filter“) am PC ohne Panel und am Handy. Vorher
  // `opacity-50`: alle Texte der Zeile fielen unter 4,5:1 (Invariante 10).
  {
    name: "kassenbuch-ausgegraut",
    route: "/ledger",
    widths: [1280],
    mock: mockLedger,
    open: (page) => openFilteredOut(page, { mobile: false }),
  },
  {
    name: "kassenbuch-ausgegraut-handy",
    route: "/ledger",
    widths: [412],
    // Mit aktivem Filter: Chip-„×“ (24 px) und „Alle Filter löschen“ (44 px) sind zu klein. Kann-Sammlung ledger
    // (ledger-k4n2s, Punkt 1).
    exceptions: [
      { width: 412, inv: 2, selector: 'button "Kategorie: 1 entfernen"' },
      { width: 412, inv: 2, selector: 'button "Alle Filter löschen"' },
    ],
    mock: mockLedger,
    open: (page) => openFilteredOut(page, { mobile: true }),
  },
  // ledger-external-links: das Detail-Sheet mit Bestell-Link.
  {
    name: "kassenbuch-detail",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "AMAZON PAYMENTS");
      await sheet.locator('[data-testid="detail-external-link"]').waitFor();
    },
  },
  // ledger-split-editor, design-system-j6dnj: Aufteilen im Sheet, Speichern klebt unten, Zeile hinzufügen und
  // Barabhebung mit voller Trefferfläche.
  {
    name: "kassenbuch-aufteilen",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-split-action"]').tap();
      await sheet.locator('[data-testid="split-summary"]').waitFor();
    },
    primary: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
    // Der Fuß mit Speichern steht außerhalb des Scrollbereichs und fährt mit dem Sheet über die Tastatur.
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="split-amount-0"] input'),
      reach: (page) => page.locator('.bottom-sheet.active [data-testid="split-amount-0"] input').or(page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true })),
    },
  },
  // ledger-ng110: ein unlesbarer Zeilenbetrag zeigt rote Ziffern (`input-invalid`) und trägt `aria-invalid`. Die
  // Invarianten 2 und 3 laufen auf den Feldern aus `Input.text` und dem Knopf "Zeile entfernen" (`removeIconWhen`).
  // Review a3f7eb1d..982bdf79: die Zeile hatte nur rote Ziffern, keinen Satz. Jetzt steht „Betrag nicht lesbar, etwa
  // 12,50“ unter der Zeile (`ErrorDisplay.inline'`), das Feld verweist per `aria-describedby` darauf.
  {
    name: "kassenbuch-aufteilen-ungueltig",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-split-action"]').tap();
      await sheet.locator('[data-testid="split-summary"]').waitFor();
      const field = sheet.locator('[data-testid="split-amount-1"] input');
      await field.fill("abc");
      await page.waitForFunction((el) => el.getAttribute("aria-invalid") === "true", await field.elementHandle());
      if (!(await field.evaluate((el) => el.classList.contains("input-invalid"))))
        throw new Error("Ungültiger Zeilenbetrag ohne input-invalid");
      const describedBy = await field.getAttribute("aria-describedby");
      if (!describedBy) throw new Error("Ungültiger Zeilenbetrag ohne aria-describedby");
      const message = sheet.locator(`[id="${describedBy}"]`);
      await message.getByText("Betrag nicht lesbar, etwa 12,50").waitFor();
      const valid = sheet.locator('[data-testid="split-amount-0"] input');
      if ((await valid.getAttribute("aria-invalid")) !== null) throw new Error("Leerer Zeilenbetrag gilt als ungültig");
      await field.blur();
    },
    primary: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
  },
  // ledger-ng110: ein abgelehntes Speichern steht bei offenem Editor über "Speichern" im Fuß des Sheets
  // (`ErrorDisplay.inline'`), ohne Scrollen sichtbar und ohne Toast. Der Entwurf: "Zeile entfernen" an der leeren
  // Zeile, dann "Rest" an der Kategorie-Zeile, das hebt die Aufteilung auf und lässt Speichern zu.
  {
    name: "kassenbuch-aufteilen-speicherfehler",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/setEntrySplits", (r) => json(r, { Error: "YNAB nicht erreichbar" }));
    },
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-split-action"]').tap();
      await sheet.locator('[data-testid="split-summary"]').waitFor();
      await sheet.getByRole("button", { name: "Zeile entfernen" }).nth(1).tap();
      await sheet.locator('[data-testid="split-line-1"]').waitFor({ state: "detached" });
      await sheet.locator('[data-testid="split-line-0"]').getByRole("button", { name: "Rest", exact: true }).tap();
      const save = sheet.getByRole("button", { name: "Speichern", exact: true });
      await save.tap();
      await page.locator('.bottom-sheet.active [data-testid="split-save-error"]').waitFor();
      const shown = await page.getByText("Aufteilung nicht gespeichert: YNAB nicht erreichbar").count();
      if (shown !== 1) throw new Error(`Speicherfehler ${shown}-mal statt einmal im Editor (Toast dazu?)`);
      await page.waitForFunction((el) => !el.matches(":active"), await save.elementHandle());
    },
    primary: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
    reach: (page) => page.locator('.bottom-sheet.active [data-testid="split-save-error"]'),
  },
  // ledger-m7mye: das Sheet "Betrag" am Handy ersetzt den Inhalt des Detail-Sheets. "Speichern" klebt unten und
  // bleibt bei offener Zifferntastatur im Betragsfeld erreichbar.
  {
    name: "kassenbuch-betrag",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-amount-edit"]').tap();
      await page.locator('.bottom-sheet.active [data-testid="amount-form"]').waitFor();
    },
    primary: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="amount-input"] input'),
      reach: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
    },
  },
  // ledger-q42pg: Speichern mit leerem Betrag zeigt den Feldfehler unter dem Feld. Das Feld verliert beim Speichern den
  // Fokus nicht, auch nicht bei "Einnahme" und "Ausgabe" (design-system-ak97z); hat es ihn verloren, bekommt es ihn
  // beim Speichern zurück, und es verweist per
  // `aria-describedby` auf die Meldung. Geht die Tastatur danach auf, rollt die Meldung ins Bild, auch bei 320 px,
  // wo sie in zwei Zeilen umbricht.
  {
    name: "kassenbuch-betrag-feldfehler",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-amount-edit"]').tap();
      const field = page.locator('.bottom-sheet.active [data-testid="amount-input"] input');
      const save = page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true });
      await field.fill("");
      await field.evaluate((el) => {
        window.__amountBlurs = 0;
        el.addEventListener("blur", () => window.__amountBlurs++);
      });
      await save.tap();
      await page.locator('.bottom-sheet.active [data-testid="amount-input-error"]').waitFor();
      if ((await page.evaluate(() => window.__amountBlurs)) > 0) throw new Error("Betragsfeld verlor beim Speichern den Fokus");
      // design-system-ak97z: ein Tipp auf "Einnahme" nimmt dem Feld den Fokus nicht (`Segment.KeepsFocus`).
      if (!(await field.evaluate((el) => document.activeElement === el)))
        throw new Error("Betragsfeld hat vor dem Tipp auf Einnahme keinen Fokus");
      await page.locator('.bottom-sheet.active [data-testid="amount-direction"]', { hasText: "Einnahme" }).tap();
      if ((await page.evaluate(() => window.__amountBlurs)) > 0) throw new Error("Betragsfeld verlor bei Einnahme den Fokus");
      await page.locator('.bottom-sheet.active [data-testid="amount-direction"]', { hasText: "Ausgabe" }).tap();
      if ((await page.evaluate(() => window.__amountBlurs)) > 0) throw new Error("Betragsfeld verlor bei Ausgabe den Fokus");
      // Hat das Feld den Fokus doch verloren (etwa ein Tipp daneben), holt Speichern ihn zurück (ledger-q42pg).
      await field.evaluate((el) => el.blur());
      await save.tap();
      await frames(page);
      const state = await field.evaluate((el) => ({
        focused: document.activeElement === el,
        describedBy: el.getAttribute("aria-describedby"),
        invalid: el.getAttribute("aria-invalid"),
      }));
      if (!state.focused) throw new Error("Betragsfeld hat nach Blur und Speichern keinen Fokus");
      if (state.describedBy !== "amount-input-error" || state.invalid !== "true")
        throw new Error(`Feldfehler nicht am Feld: ${JSON.stringify(state)}`);
      // Chromium hält `:active` nach einem Tipp noch einen Moment fest; der Knopf misst dann mit
      // `active:scale-[0.98]` nur 47 px. Der Zustand wartet, bis "Speichern" losgelassen ist.
      await page.waitForFunction((el) => !el.matches(":active"), await save.elementHandle());
    },
    primary: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="amount-input"] input'),
      reach: (page) =>
        page
          .locator('.bottom-sheet.active [data-testid="amount-input-error"]')
          .or(page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true })),
    },
  },
  // ledger-m7mye: am PC öffnet "Betrag" als Dialog über der Liste, das Detail-Panel bleibt rechts.
  {
    name: "kassenbuch-betrag-dialog",
    route: "/ledger",
    widths: [1440],
    mock: mockLedger,
    open: async (page) => {
      const row = page.locator("table tbody tr", { hasText: "Bauhaus" }).first();
      await row.waitFor();
      await row.locator("td").nth(2).click();
      const panel = page.locator('[data-testid="ledger-detail-panel"]');
      await panel.locator('[data-testid="detail-amount-edit"]').click();
      await page.locator('[data-layer="dialog"] [data-testid="amount-form"]').waitFor();
    },
    primary: (page) => page.locator('[data-layer="dialog"]').getByRole("button", { name: "Speichern", exact: true }),
  },
  // ledger-fxt09: die Payee-Auswahl am Handy ersetzt den Inhalt des Detail-Sheets (§7.15), mit einem langen Payee,
  // der in der Liste mit Ellipse abschneidet.
  {
    name: "kassenbuch-payee-auswahl",
    route: "/ledger",
    widths: [412],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/getLedgerPayeeSuggestions", (r) =>
        json(r, [{ Payee: "Bauhaus", Count: 9 }, { Payee: LONG_PAYEE, Count: 4 }, { Payee: "Baumarkt", Count: 2 }])
      );
    },
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-payee-arrow"]').tap();
      await page.locator('.bottom-sheet.active [data-testid="payee-picker-option"]').first().waitFor();
    },
  },
  // ledger-fxt09: am PC die Payee-Auswahl. Mit Panel (1440) als Dialog über der Liste, das Detail-Panel bleibt
  // rechts; ohne Panel (1280, Detail als Sheet) ersetzt sie den Inhalt des Sheets.
  {
    name: "kassenbuch-payee-auswahl-pc",
    route: "/ledger",
    widths: [1280, 1440],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/getLedgerPayeeSuggestions", (r) =>
        json(r, [{ Payee: "Bauhaus", Count: 9 }, { Payee: LONG_PAYEE, Count: 4 }, { Payee: "Baumarkt", Count: 2 }])
      );
    },
    open: async (page) => {
      const row = page.locator("table tbody tr", { hasText: "Bauhaus" }).first();
      await row.waitFor();
      await row.locator("td").nth(2).click();
      await page.locator('[data-testid="detail-payee-arrow"]').filter({ visible: true }).first().click();
      await page.locator('[data-testid="payee-picker-option"]').filter({ visible: true }).first().waitFor();
    },
  },
  // ledger-at63w: "Neue Buchung" am Handy als Sheet. "Buchung anlegen" klebt unten und bleibt auch bei
  // offener Tastatur im Betragsfeld erreichbar.
  {
    name: "kassenbuch-erfassen",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.locator(".ledger-capture-fab").getByRole("button").waitFor();
      await page.locator(".ledger-capture-fab").getByRole("button").tap();
      await page.locator('.bottom-sheet.active [data-testid="capture-recent"]').waitFor();
      // design-system-ak97z: Fokus im Betragsfeld, ein Tipp auf "Einnahme" nimmt ihn nicht (`Segment.KeepsFocus`).
      const field = page.locator('.bottom-sheet.active [data-testid="capture-amount"] input');
      await field.tap();
      await field.evaluate((el) => {
        window.__captureAmountBlurs = 0;
        el.addEventListener("blur", () => window.__captureAmountBlurs++);
      });
      if (!(await field.evaluate((el) => document.activeElement === el)))
        throw new Error("Betragsfeld in Neue Buchung hat vor dem Tipp auf Einnahme keinen Fokus");
      await page.locator('.bottom-sheet.active [data-testid="capture-direction"]', { hasText: "Einnahme" }).tap();
      await frames(page);
      if ((await page.evaluate(() => window.__captureAmountBlurs)) > 0)
        throw new Error("Betragsfeld in Neue Buchung verlor bei Einnahme den Fokus");
      await page.locator('.bottom-sheet.active [data-testid="capture-direction"]', { hasText: "Ausgabe" }).tap();
      await frames(page);
      if ((await page.evaluate(() => window.__captureAmountBlurs)) > 0)
        throw new Error("Betragsfeld in Neue Buchung verlor bei Ausgabe den Fokus");
      // Zurück in den Startzustand (Ausgabe, kein Fokus): die Tastaturprüfung setzt den Fokus selbst ins Feld.
      await field.evaluate((el) => el.blur());
    },
    primary: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Buchung anlegen", exact: true }),
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="capture-amount"] input'),
      reach: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Buchung anlegen", exact: true }),
    },
  },
  // Bug ledger-yhemh (in ledger-ea70s): am Handy standen Datum (mit „Heute“, „Gestern“ und Kalender) und Memo
  // nebeneinander im festen Zwei-Spalten-Raster, das Memo war zu schmal. Jetzt steht Memo unter dem Datum; der
  // Zustand bricht ab, wenn das Memo-Feld wieder neben dem Datum beginnt.
  {
    name: "kassenbuch-erfassen-memo-unter-datum",
    route: "/ledger",
    widths: [412],
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.locator(".ledger-capture-fab").getByRole("button").waitFor();
      await page.locator(".ledger-capture-fab").getByRole("button").tap();
      const sheet = page.locator(".bottom-sheet.active");
      await sheet.locator('[data-testid="capture-date"]').waitFor();
      // Beide Rechtecke im selben Frame: das Sheet fährt noch ein, getrennte Messungen lägen verschoben.
      const pos = await page.evaluate(() => {
        const sheet = document.querySelector(".bottom-sheet.active");
        const date = sheet?.querySelector('[data-testid="capture-date"]')?.getBoundingClientRect();
        const memo = sheet?.querySelector('input[placeholder="optional"]')?.getBoundingClientRect();
        return date && memo ? { dateBottom: date.bottom, memoTop: memo.top } : null;
      });
      if (!pos) throw new Error("Datum oder Memo in Neue Buchung nicht gefunden");
      if (pos.memoTop < pos.dateBottom)
        throw new Error(`Memo steht am Handy neben dem Datum statt darunter (Memo oben ${pos.memoTop}, Datum unten ${pos.dateBottom})`);
    },
    primary: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Buchung anlegen", exact: true }),
  },
  // ledger-ea70s: die Kategorie-Auswahl der Neuen Buchung ersetzt am Handy den Inhalt des Sheets (§7.15), kein
  // zweites Sheet darüber. Jede Zeile antippbar, die Suche mit 16 px.
  {
    name: "kassenbuch-erfassen-kategorie",
    route: "/ledger",
    widths: [412],
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.locator(".ledger-capture-fab").getByRole("button").waitFor();
      await page.locator(".ledger-capture-fab").getByRole("button").tap();
      await page.locator('.bottom-sheet.active [data-testid="capture-target"]').tap();
      await page.locator('.bottom-sheet.active [role="option"]').first().waitFor();
      if ((await page.locator(".bottom-sheet.active").count()) !== 1)
        throw new Error("Die Kategorie-Auswahl der Neuen Buchung liegt als zweites Sheet über dem Formular");
    },
  },
  // ledger-ea70s: die Konto-Auswahl der Neuen Buchung ersetzt am Handy ebenso den Inhalt (Skizze `capAcct`).
  {
    name: "kassenbuch-erfassen-konto",
    route: "/ledger",
    widths: [412],
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.locator(".ledger-capture-fab").getByRole("button").waitFor();
      await page.locator(".ledger-capture-fab").getByRole("button").tap();
      await page.locator('.bottom-sheet.active [data-testid="capture-account"]').tap();
      await page.locator('.bottom-sheet.active [data-testid="capture-account-list"]').getByText("Tagesgeld ING").waitFor();
      if ((await page.locator(".bottom-sheet.active").count()) !== 1)
        throw new Error("Die Konto-Auswahl der Neuen Buchung liegt als zweites Sheet über dem Formular");
    },
  },
  // Bug ledger-vf6z5: "Buchung anlegen" mit leerem Betrag sammelte den Fehler am Formularende, das Betragsfeld bekam
  // weder Fokus noch `aria-invalid`. Jetzt steht die Meldung unter dem Feld, das Feld hat den Fokus und verweist per
  // `aria-describedby` darauf; geht die Tastatur danach auf, rollt die Meldung ins Bild (Muster
  // `kassenbuch-betrag-feldfehler`).
  {
    name: "kassenbuch-erfassen-feldfehler",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.locator(".ledger-capture-fab").getByRole("button").waitFor();
      await page.locator(".ledger-capture-fab").getByRole("button").tap();
      await page.locator('.bottom-sheet.active [data-testid="capture-recent"]').waitFor();
      const field = page.locator('.bottom-sheet.active [data-testid="capture-amount"] input');
      const submit = page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Buchung anlegen", exact: true });
      await field.fill("");
      await field.evaluate((el) => el.blur());
      await submit.tap();
      await page.locator('.bottom-sheet.active [data-testid="capture-amount-error"]').waitFor();
      await frames(page);
      const state = await field.evaluate((el) => ({
        focused: document.activeElement === el,
        describedBy: el.getAttribute("aria-describedby"),
        invalid: el.getAttribute("aria-invalid"),
      }));
      if (!state.focused) throw new Error("Betragsfeld in Neue Buchung hat nach abgelehntem Absenden keinen Fokus");
      if (state.describedBy !== "capture-amount-error" || state.invalid !== "true")
        throw new Error(`Feldfehler nicht am Betragsfeld: ${JSON.stringify(state)}`);
      if ((await page.locator('.bottom-sheet.active [data-testid="capture-errors"]').count()) > 0)
        throw new Error("Betragsfehler steht noch gesammelt am Formularende");
      // Chromium hält `:active` nach einem Tipp noch einen Moment fest (siehe `kassenbuch-betrag-feldfehler`).
      await page.waitForFunction((el) => !el.matches(":active"), await submit.elementHandle());
    },
    primary: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Buchung anlegen", exact: true }),
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="capture-amount"] input'),
      reach: (page) =>
        page
          .locator('.bottom-sheet.active [data-testid="capture-amount-error"]')
          .or(page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Buchung anlegen", exact: true })),
    },
  },
  // Bug ledger-vf6z5 (Review): die Konto-Auswahl in "Neue Buchung" verwarf ihre Zustände ohne Konto. Das `->` in
  // `for account in accounts -> …` schaltete die impliziten Yields der Liste ab, der Spinner mit "Konten werden
  // geladen", der Ladefehler und "Kein Konto zum Buchen" kamen nie ins DOM (FS0020). Die Budget-Konten aus YNAB
  // laden hier nie (`State.captureAccountChoice` wartet auf Vorbelegung, Konten und Budget-Konten; hängt
  // `getLedgerAccounts`, öffnet das Kassenbuch gar nicht), die Auswahl muss den Ladetext zeigen.
  {
    name: "kassenbuch-erfassen-konten-laden",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    // Seit ledger-ea70s ersetzt die Konto-Auswahl den Inhalt des Sheets; der Schleier der zweiten Ebene, der die
    // Ausnahme für Invariante 5 brauchte, ist weg.
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
      await page.route("**/api/LedgerApi/getBudgetAccounts", () => {});
    },
    open: async (page) => {
      await page.locator(".ledger-capture-fab").getByRole("button").waitFor();
      await page.locator(".ledger-capture-fab").getByRole("button").tap();
      await page.locator('.bottom-sheet.active [data-testid="capture-account"]').tap();
      await page
        .locator('.bottom-sheet.active [data-testid="capture-account-list"] [role="status"]', { hasText: "Konten werden geladen" })
        .waitFor();
    },
  },
  // ledger-at63w: "Neue Buchung" am PC im Panel rechts neben der Liste, "Buchung anlegen" ohne Scrollen im Bild.
  {
    name: "kassenbuch-erfassen-panel",
    route: "/ledger",
    // Unter der Panel-Breite der Tabelle öffnet das Formular als Sheet wie das Detail (`detailFormFor`).
    widths: [1440],
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.getByRole("button", { name: "Neue Buchung" }).locator("visible=true").first().click();
      const panel = page.locator('[data-testid="ledger-detail-panel"]');
      await panel.locator('[data-testid="capture-recent"]').waitFor();
    },
    primary: (page) => page.locator('[data-testid="ledger-detail-panel"]').getByRole("button", { name: "Buchung anlegen", exact: true }),
    // Das DS-Panel (`Detail.panel`, unverändert, gilt auch für das Buchungs-Detail) überlappt nach dem Scrollen ans
    // Ende den haftenden Tabellenkopf um 4 px und die feste Tastatur-Leiste um 9 px. Erster Zustand mit dem Panel am
    // PC; Kann-Sammlung ledger (ledger-at63w).
    exceptions: [{ width: 1440, inv: 6, selector: '[data-testid="ledger-detail-panel"] und div' }],
  },
  // ledger-ea70s: die Kategorie-Auswahl im Buchungs-Detail ersetzt am Handy den Inhalt des Sheets (§7.15).
  {
    name: "kassenbuch-detail-kategorie",
    route: "/ledger",
    widths: [412],
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-category-arrow"]').tap();
      await page.locator(".bottom-sheet.active", { hasText: "Ziel wählen" }).locator('[role="option"]').first().waitFor();
      if ((await page.locator(".bottom-sheet.active").count()) !== 1)
        throw new Error("Die Kategorie-Auswahl im Detail liegt als zweites Sheet über dem Detail");
    },
  },
  // Regression c819eeb (Toast hinter dem Detail-Sheet): nach dem Kategoriewechsel liegt der Toast oben.
  {
    name: "kassenbuch-kategorie-toast",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-category-arrow"]').tap();
      // ledger-qvjvs: der Picker im Detail bietet auch die Konten an und heißt dann "Ziel wählen".
      const picker = page.locator(".bottom-sheet.active", { hasText: "Ziel wählen" });
      await picker.waitFor();
      await picker.locator('[role="option"]', { hasText: "Nebenkosten" }).last().tap();
      await page.locator('[data-toast-action="true"]').first().waitFor();
    },
  },
  // ledger-hg5m5: nach der Kategorie-Änderung steht unter der Kategorie das Angebot „Regel anlegen?“; der Toast mit
  // „Rückgängig“ verdeckt es nicht und es verdeckt ihn nicht (§7.13).
  {
    name: "kassenbuch-regel-angebot",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-category-arrow"]').tap();
      const picker = page.locator(".bottom-sheet.active", { hasText: "Ziel wählen" });
      await picker.waitFor();
      await picker.locator('[role="option"]', { hasText: "Nebenkosten" }).last().tap();
      await page.locator('[data-toast-action="true"]').first().waitFor();
      await page.locator('.bottom-sheet.active [data-testid="rule-offer"]').waitFor();
    },
  },
  // ledger-hg5m5: „Regel anlegen“ im Detail ersetzt den Inhalt des Sheets (§7.15), „Regel anlegen“ klebt unten.
  {
    name: "kassenbuch-regel-formular",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-rule"]').tap();
      await page.locator('.bottom-sheet.active [data-testid="rule-form"]').waitFor();
    },
    primary: (page) => page.locator(".bottom-sheet.active").getByRole("button", { name: "Regel anlegen", exact: true }),
  },
  // ledger-hg5m5: am PC ersetzt das Formular den Inhalt des Panels, mit „Zurück“ zur Buchung.
  {
    name: "kassenbuch-regel-panel",
    route: "/ledger",
    widths: [1440],
    // Schon vor ledger-hg5m5 bei jedem offenen Detail-Panel: nach dem Scrollen ragt die haftende Filterleiste 4 px
    // unter den Kopf des Panels (Kann-Sammlung ledger).
    exceptions: [{ width: 1440, inv: 6, selector: '[data-testid="ledger-detail-panel"] und div' }],
    mock: mockLedger,
    open: async (page) => {
      const row = page.locator("table tbody tr", { hasText: "Bauhaus" }).first();
      await row.waitFor();
      await row.locator("td").nth(2).click();
      const panel = page.locator('[data-testid="ledger-detail-panel"]');
      await panel.locator('[data-testid="detail-rule"]').click();
      await panel.locator('[data-testid="rule-form"]').waitFor();
    },
    primary: (page) => page.locator('[data-testid="ledger-detail-panel"]').getByRole("button", { name: "Regel anlegen", exact: true }),
  },
  // ledger-w29y6: Kategorie setzen für eine Auswahl öffnet das Popover ganz im Bild.
  {
    name: "kassenbuch-auswahl-popover",
    route: "/ledger",
    widths: [1440],
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      for (const payee of ["Bauhaus", "PayPal Shop"]) {
        await page.locator("table tbody tr", { hasText: payee }).first().getByRole("checkbox").click();
      }
      await page.getByRole("button", { name: "Kategorie setzen" }).click();
      await page.locator('[data-layer] [role="option"]').first().waitFor();
    },
  },
  // ledger-skpcx: das Sortier-Sheet am Handy mit allen Optionen.
  {
    name: "kassenbuch-sortieren",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await page.locator(".ledger-filter-bar button").filter({ hasText: /^(Datum|Betrag|Payee|Kategorie|Konto) [↑↓]$/ }).click();
      await page.locator(".bottom-sheet.active button.category-item").first().waitFor();
    },
  },
  // ledger-qns4j (AC1, AC2): bei vielen Konten haftet die Konten-Spalte und scrollt in sich, späte Konten
  // werden nicht abgeschnitten.
  {
    name: "kassenbuch-viele-konten",
    route: "/ledger",
    widths: [1024, 1440],
    mock: (page) => mockLedger(page, { accounts: 30 }),
    open: openLedger,
  },
  // Mit Auswahl steht am PC die Auswahl-Leiste statt des Tastatur-Hinweises: das Ende der Konten-Spalte bleibt
  // über ihr sichtbar (Invariante 5, am Ende gescrollt).
  {
    name: "kassenbuch-viele-konten-auswahl",
    route: "/ledger",
    widths: [1024, 1440],
    mock: (page) => mockLedger(page, { accounts: 30 }),
    open: async (page) => {
      await openLedger(page);
      await page.locator("table tbody tr", { hasText: "Bauhaus" }).first().getByRole("checkbox").click();
      await page.locator(".selection-bar").filter({ visible: true }).waitFor();
    },
  },
  // Regression ledger-filter-bar (feste Höhenschätzung): bei 1024 px bricht die Leiste mit zwei Kategorien um,
  // der Tabellenkopf haftet trotzdem darunter.
  {
    name: "kassenbuch-gefiltert",
    route: "/ledger",
    widths: [1024],
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await page.getByRole("button", { name: /^Kategorie/ }).first().click();
      const popover = page.locator('[data-layer="popover"]');
      await popover.getByText("Lebensmittel", { exact: true }).click();
      await popover.getByText("Bahn", { exact: true }).click();
      await page.getByRole("button", { name: "Übernehmen (2)" }).click();
      await popover.waitFor({ state: "detached" });
    },
  },
  // Regression ledger-filter-bar: lief der Spiegel, maß der Observer den Hinweis statt der Leiste. Während
  // und nach dem Lauf haftet die Leiste, ohne den Tabellenkopf zu überdecken.
  {
    name: "kassenbuch-spiegel-laeuft",
    route: "/ledger",
    widths: [1024, 1440],
    mock: async (page) => {
      await mockLedger(page);
      await mockRunningMirror(page);
    },
    open: async (page) => page.getByText("Spiegel läuft. Die Liste bleibt so lange stehen.").waitFor(),
  },
  {
    name: "kassenbuch-nach-spiegel",
    route: "/ledger",
    widths: [1024],
    mock: async (page) => {
      await mockLedger(page);
      page.sweepMirror = await mockRunningMirror(page);
    },
    open: async (page) => {
      const hint = page.getByText("Spiegel läuft. Die Liste bleibt so lange stehen.");
      await hint.waitFor();
      await page.sweepMirror.finishMirror();
      await hint.waitFor({ state: "detached" });
    },
  },
  // ledger-y54gr: die Auswahl-Leiste am Handy mit beiden Lena-Aktionen und dem Grund als Zeile. Bei 320 px bricht
  // sie um und wird höher: am Ende der Liste liegt die letzte Buchung trotzdem über ihr (Invariante 5, am Ende
  // gescrollt).
  {
    name: "kassenbuch-auswahl-handy",
    route: "/ledger",
    widths: HANDY_WIDTHS,
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => json(r, { SavedColor: null, SuggestedColor: null }));
    },
    open: openMobileSelection,
  },
  // banking-import-t4raw: die Karte Comdirect mit Schalter „Automatisch abrufen“ und Auswahlfeld „Alle“.
  {
    name: "einstellungen",
    route: "/settings",
    widths: PAGE_WIDTHS,
    mock: mockSettings,
    open: openSettings,
  },
  // categorization-cgq35, categorization-n3sxw: der Tab Regeln im Kassenbuch, Prioritätspfeile am Handy, lange Namen.
  {
    name: "regeln",
    route: "/ledger/rules",
    widths: PAGE_WIDTHS,
    mock: mockRules,
    open: openRules,
  },
  // categorization-n3sxw: am PC steht eine Regel rechts im Panel, der Picker hängt als Popover ganz im Bild. Ab 1024 px
  // steht die Karte neben dem Panel, von 1024 bis 1279 px schmaler (Abweichung (f) der Task).
  {
    name: "regeln-panel",
    route: "/ledger/rules",
    widths: [1024, 1280, 1440],
    mock: mockRules,
    open: async (page) => {
      await openRule(page, "Regel eins");
      await page.locator('[data-testid="rule-category"]').click();
      await page.locator('[role="option"]').first().waitFor();
      await frames(page);
    },
    primary: (page) => page.locator('[data-testid="category-picker-footer-hint"]:visible'),
  },
  // categorization-n3sxw: am Handy ersetzt die Auswahl der Kategorie den Inhalt des Sheets (§7.15), jede Zeile antippbar.
  {
    name: "regeln-sheet-picker",
    route: "/ledger/rules",
    widths: HANDY_WIDTHS,
    mock: mockRules,
    open: async (page) => {
      await openRule(page, "Regel eins");
      await settle(page, { where: "regeln-sheet-picker, Sheet offen" });
      await page.locator('[data-testid="rule-category"]').click();
      await page.locator('.bottom-sheet.active [role="option"]').first().waitFor();
    },
  },
  // categorization-n3sxw: nach „Regel löschen“ steht der Toast „Regel gelöscht“ mit Rückgängig und verdeckt keine
  // Bedienelemente (ADR 0087 Punkt 22). Am Handy schließt dabei das Sheet, am PC das Panel.
  {
    name: "regeln-geloescht-toast",
    route: "/ledger/rules",
    widths: [...HANDY_WIDTHS, 1024, 1440],
    mock: async (page) => {
      await mockRules(page);
      await page.route("**/api/RulesApi/deleteRule", (r) => json(r, { Ok: null }));
    },
    open: async (page, { mobile }) => {
      await openRule(page, "Regel eins");
      const remove = page.locator('[data-testid="rule-form"]').getByRole("button", { name: "Regel löschen", exact: true });
      await (mobile ? remove.tap() : remove.click());
      await page.locator('[data-toast-action="true"]').first().waitFor();
      await page.locator('[data-testid="rule-form"]').waitFor({ state: "detached" });
    },
  },
  // design-system-k6kfw: die Übersicht als Einstieg, mit Leiste unten, Rail und Seitenleiste je nach Breite.
  {
    name: "uebersicht",
    route: "/",
    widths: PAGE_WIDTHS,
    mock: mockOverview,
    open: openOverview,
    // design-system-k6kfw, Verifier iteration 1: die Karte „Monat“ hat eine feste Höhe, beim Laden rutschen Konten
    // (Handy) und Neueste Buchungen (PC) nicht nach unten. Der Monat kommt mit „Im Minus“.
    deferred: {
      api: "**/api/BudgetApi/getBudgetMonth",
      body: { Ok: budgetMonth(CURRENT) },
      loaded: (page) => page.locator('[data-testid="overview-month"]'),
    },
    // Rest-Sprung nach Vorgabe (§7.4): „Zu tun“ steht unter 1024 px über dem Monat und wächst mit seinem Inhalt. Der
    // Platzhalter hat die Form des Ziels (großer erster Eintrag mit Knopf und eine Zeile), bei 320 und 390 px bricht
    // der Satz des ersten Eintrags aber auf zwei Zeilen um, die Konten rutschen um diese Zeile. Kann-Sammlung ledger.
    // Die Leer-Karte „Angepinnt“ (budgeting-w9yt4) steht zwischen Monat und Konten und rutscht aus demselben Grund mit.
    exceptions: [
      { width: 320, inv: 9, selector: '[data-testid="overview-account"]' },
      { width: 390, inv: 9, selector: '[data-testid="overview-account"]' },
      { width: 320, inv: 9, selector: '[data-testid="overview-pins-empty"]' },
      { width: 390, inv: 9, selector: '[data-testid="overview-pins-empty"]' },
    ],
  },
  // ledger-s1weh: „Zu tun“ mit allen Arten von Einträgen, ein langer Payee in „Buchung nicht in YNAB“, der Zähler an der
  // Übersicht in Rail und Seitenleiste und „+ Buchung“ ruhig über der Leiste.
  {
    name: "uebersicht-zu-tun",
    route: "/",
    widths: [320, 412, 900, 1280],
    // Invariante 10 (ledger-dn4vy): der Leertext „Buchungen erscheinen hier …“ steht in `text-muted/70` (3,23:1).
    // Kann-Sammlung der Übersicht.
    exceptions: [
      { width: 900, inv: 10, selector: '[data-testid="overview-page"] p "Buchungen erscheinen hier' },
      { width: 1280, inv: 10, selector: '[data-testid="overview-page"] p "Buchungen erscheinen hier' },
    ],
    mock: async (page) => {
      await mockOverview(page);
      await page.route("**/api/LedgerApi/getLedgerEntriesPage", (r) =>
        json(r, { Entries: [], TotalCount: 0, TotalAmount: eur(0), DeletedCount: 0, NeedsReviewCount: 12, AskLenaCount: 2 })
      );
      await page.route("**/api/LedgerApi/getEntriesNotInYnab", (r) =>
        json(r, [
          {
            EntryId: guid(9101),
            AccountId: NAV_ACCOUNTS.DefaultAccountId,
            Payee: LONG_PAYEE,
            Amount: eur(-123456.78),
            Date: "2026-11-06T00:00:00",
            Reason: { PushFailed: { Reason: "YNAB antwortet nicht", FailedAt: "2026-11-07T08:00:00Z" } },
          },
        ])
      );
    },
    open: async (page) => page.locator('[data-testid="overview-todos"]').waitFor(),
  },
  // ledger-v6s7c: das Durchgehen. Karte mit langem Payee und großem Betrag, Karte mit Bestell-Link, Vorschlägen und
  // Lena-Hinweis, Duplikat-Karte und Endansicht; „Passt, weiter“ (bzw. der Hauptknopf der Karte) ohne Scrollen.
  {
    name: "durchgehen-karte",
    route: "/review",
    widths: [320, 412, 1280],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.plain]),
    open: reviewOpen("review-card"),
    primary: reviewPrimary("Passt, weiter"),
  },
  {
    name: "durchgehen-amazon",
    route: "/review",
    widths: [320, 412, 1280],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.amazon]),
    open: async (page) => page.locator('[data-testid="review-suggestions"]').first().waitFor(),
    primary: reviewPrimary("Passt, weiter"),
  },
  {
    name: "durchgehen-duplikat",
    route: "/review",
    widths: [320, 412, 1280],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.held]),
    open: reviewOpen("review-duplicate-card"),
    primary: reviewPrimary("Ist doppelt, verwerfen"),
  },
  {
    name: "durchgehen-ende",
    route: "/review",
    widths: [320, 412, 1280],
    mock: (page) => mockReview(page, []),
    open: reviewOpen("review-end"),
    primary: reviewPrimary("Zur Übersicht"),
  },
  // ledger-hg5m5: „Regel anlegen“ auf der Karte öffnet das Formular am PC als Dialog, am Handy als Sheet.
  {
    name: "durchgehen-regel",
    route: "/review",
    widths: [412, 1280],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.plain]),
    open: async (page) => {
      // Erst wenn die Vorschläge stehen, steht die Zeile still; sonst trifft der Klick das Memo darüber.
      await page.locator('[data-testid="review-suggestions"]').first().waitFor();
      await page.locator('[data-testid="review-rule"]').first().click();
      await page.locator('[data-testid="rule-form"]').waitFor();
    },
    primary: (page) => page.getByRole("button", { name: "Regel anlegen", exact: true }).locator("visible=true").last(),
  },
  // Das Memo-Sheet der Karte am Handy: „Speichern“ bleibt bei offener Tastatur im Bild.
  {
    name: "durchgehen-memo",
    route: "/review",
    widths: [412],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.plain]),
    open: async (page) => {
      await page.locator('[data-testid="review-memo"]').first().waitFor();
      await page.locator('[data-testid="review-memo"]').first().click();
      await page.locator('.bottom-sheet.active [data-testid="detail-memo-editor"]').waitFor();
    },
    primary: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
    keyboard: {
      focus: (page) => page.locator(".bottom-sheet.active textarea, .bottom-sheet.active input").first(),
      reach: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
    },
  },
  // ledger-s1weh: „Neue Buchung“ auf der Übersicht, am Handy als Sheet über „+ Buchung“, ab 1024 px als Dialog in der
  // Mitte über „Buchung“ im Kopf. „Buchung anlegen“ ist ohne Scrollen erreichbar, am Handy auch bei offener Tastatur.
  {
    name: "uebersicht-erfassen",
    route: "/",
    widths: [412, 1280],
    mock: async (page) => {
      await mockOverview(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page, { width }) => {
      await openOverview(page);
      if (width < 1024) {
        await page.locator('[data-testid="overview-capture-fab"]').tap();
        await page.locator('.bottom-sheet.active [data-testid="capture-recent"]').waitFor();
      } else {
        await page.getByRole("button", { name: "Neue Buchung" }).locator("visible=true").first().click();
        await page.locator('[data-layer="dialog"] [data-testid="capture-recent"]').waitFor();
      }
    },
    primary: (page) =>
      page
        .locator('[data-layer="dialog"]')
        .or(page.locator(".bottom-sheet.active").first())
        .getByRole("button", { name: "Buchung anlegen", exact: true }),
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="capture-amount"] input'),
      reach: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Buchung anlegen", exact: true }),
    },
  },
  // Bei 20 px Grundschrift wächst die Karte „Monat“ mit (Höhen in rem); Platzhalter und Karte bleiben gleich hoch.
  {
    name: "uebersicht-grosse-schrift",
    route: "/",
    widths: [390, 1280],
    mock: mockOverview,
    open: async (page) => {
      await openOverview(page);
      await page.addStyleTag({ content: "html { font-size: 20px; }" });
    },
    deferred: {
      api: "**/api/BudgetApi/getBudgetMonth",
      body: { Ok: budgetMonth(CURRENT) },
      loaded: (page) => page.locator('[data-testid="overview-month"]'),
    },
    // Wie `uebersicht`: bei 390 px bricht der Satz des ersten Eintrags um (§7.4, „Zu tun“ wächst mit seinem Inhalt).
    // Die Leer-Karte „Angepinnt“ (budgeting-w9yt4) rutscht aus demselben Grund mit.
    exceptions: [
      { width: 390, inv: 9, selector: '[data-testid="overview-account"]' },
      { width: 390, inv: 9, selector: '[data-testid="overview-pins-empty"]' },
    ],
  },
  // Gegenstück zu `uebersicht`: der Monat ohne Kategorie im Minus. Die Karte ist gleich hoch mit oder ohne „Im Minus“.
  {
    name: "uebersicht-ohne-minus",
    route: "/",
    widths: [390, 900, 1280],
    mock: mockOverview,
    open: openOverview,
    deferred: {
      api: "**/api/BudgetApi/getBudgetMonth",
      body: { Ok: budgetMonthWithoutOverdrawn(CURRENT) },
      loaded: (page) => page.locator('[data-testid="overview-month"]'),
    },
    // Ohne „Im Minus“ steht in „Zu tun“ nur ein Eintrag, die Liste ist eine Zeile kürzer als der Platzhalter (§7.4,
    // „Zu tun“ wächst mit seinem Inhalt). Unter 1024 px rutschen Konten und neueste Buchungen nach oben. Kann-Sammlung ledger.
    exceptions: [
      { width: 390, inv: 9, selector: '[data-testid="overview-account"]' },
      { width: 900, inv: 9, selector: '[data-testid="overview-account"]' },
      { width: 900, inv: 9, selector: '[data-testid="overview-recent"]' },
      // Die Leer-Karte „Angepinnt“ (budgeting-w9yt4) steht über beiden und rutscht aus demselben Grund mit.
      { width: 390, inv: 9, selector: '[data-testid="overview-pins-empty"]' },
      { width: 900, inv: 9, selector: '[data-testid="overview-pins-empty"]' },
    ],
  },
  // budgeting-w9yt4: drei angepinnte Kategorien, eine im Minus mit „fehlt“, eine mit langem Namen. Der Platzhalter hat
  // je Pin eine Zeile in Zielform, beim Laden des Monats springt darunter nichts.
  {
    name: "uebersicht-angepinnt",
    route: "/",
    widths: [320, 412, 900, 1280],
    mock: async (page) => {
      await mockOverview(page);
      await page.route("**/api/BudgetApi/getPinnedCategories", (r) => json(r, { Ok: [guid(1), guid(2), guid(3)] }));
    },
    open: openOverview,
    deferred: {
      api: "**/api/BudgetApi/getBudgetMonth",
      body: { Ok: budgetMonth(CURRENT) },
      loaded: (page) => page.locator('[data-testid="overview-pinned"]').first(),
    },
    // Wie `uebersicht`: bei 320 px bricht der Satz des ersten Eintrags in „Zu tun“ um (§7.4), die Konten rutschen mit.
    exceptions: [{ width: 320, inv: 9, selector: '[data-testid="overview-account"]' }],
  },
  // budgeting-w9yt4: das Sheet „Angepinnt“, am Handy als Sheet, ab 1024 px als Dialog; „Fertig“ ohne Scrollen erreichbar.
  {
    name: "uebersicht-angepinnt-sheet",
    route: "/",
    widths: [412, 1280],
    mock: async (page) => {
      await mockOverview(page);
      await page.route("**/api/BudgetApi/getPinnedCategories", (r) => json(r, { Ok: [guid(1), guid(2), guid(3)] }));
    },
    open: async (page) => {
      await page.locator('[data-testid="overview-pins-edit"]').waitFor();
      await page.locator('[data-testid="overview-pins-edit"]').click();
      await page.locator('[data-testid="pin-sheet-row"]').first().waitFor();
    },
    primary: (page) =>
      page.locator('[data-layer="dialog"]').or(page.locator(".bottom-sheet.active").first()).getByRole("button", { name: "Fertig", exact: true }),
  },
  // design-system-k6kfw, Review-Fund: ein Ladefehler des Monats stand als große Fehlerkarte da, Konten (Handy) und
  // Neueste Buchungen (PC) rutschten nach unten. Der Fehler steht jetzt in der festen Höhe der Karte.
  {
    name: "uebersicht-monat-fehler",
    route: "/",
    widths: [390, 900, 1280],
    mock: mockOverview,
    open: openOverview,
    deferred: {
      api: "**/api/BudgetApi/getBudgetMonth",
      body: { Error: "YNAB antwortet nicht" },
      loaded: (page) => page.locator('[data-testid="overview-month-error"]'),
    },
    // Ohne Monat zeigt „Zu tun“ die kleine Fehlerkarte statt der Liste, kürzer als der Platzhalter (§7.4, „Zu tun“
    // wächst mit seinem Inhalt). Unter 1024 px rutschen Konten und neueste Buchungen nach oben. Kann-Sammlung ledger.
    exceptions: [
      { width: 390, inv: 9, selector: '[data-testid="overview-account"]' },
      { width: 900, inv: 9, selector: '[data-testid="overview-account"]' },
      { width: 900, inv: 9, selector: '[data-testid="overview-recent"]' },
      // Die Leer-Karte „Angepinnt“ (budgeting-w9yt4) steht über beiden und rutscht aus demselben Grund mit.
      { width: 390, inv: 9, selector: '[data-testid="overview-pins-empty"]' },
      { width: 900, inv: 9, selector: '[data-testid="overview-pins-empty"]' },
    ],
  },
  // banking-import-d0fey: das Sheet „Abrufen“ (Prototyp Version 17, Sheet `import`) am Handy als Sheet, am PC als
  // Dialog über der Übersicht. „Ich habe bestätigt“ und „Jetzt durchgehen“ sind ohne Scrollen erreichbar.
  {
    name: "abrufen-tan",
    route: "/",
    widths: [320, 412, 1280],
    mock: mockFetch({ TanRequired: syncSession("AwaitingTan") }),
    open: openFetch("fetch-tan"),
    primary: (page) => fetchLayer(page).getByRole("button", { name: "Ich habe bestätigt" }),
  },
  {
    name: "abrufen-fertig",
    route: "/",
    widths: [320, 412, 1280],
    mock: mockFetch({ Fetched: syncSession("Completed", FETCH_RESULT) }),
    open: openFetch("fetch-done"),
    primary: (page) => fetchLayer(page).getByRole("button", { name: "Jetzt durchgehen" }),
  },
  // banking-import-d0fey: eine wartende Buchung trägt „doppelt?“ statt „Nicht in YNAB“ (Zeile am Handy, Statusspalte
  // am PC), mit langem Payee und großem Betrag.
  {
    name: "kassenbuch-wartend",
    route: "/ledger",
    widths: [320, 412, 1280],
    mock: (page) => mockLedger(page, { held: true }),
    open: async (page) => page.getByText(HELD_PAYEE, { exact: true }).locator("visible=true").first().waitFor(),
  },
];

export const stateNamed = (name) => {
  const found = STATES.find((s) => s.name === name);
  if (!found) throw new Error(`kein Zustand ${name}`);
  return found;
};
