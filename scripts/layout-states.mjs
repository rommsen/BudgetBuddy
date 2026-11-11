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
    json(r, { Comdirect: null, Ynab: { HasToken: true, DefaultBudgetId: W("YnabBudgetId", "fake-budget-id"), DefaultAccountId: null, QuickAddAccountId: null }, Sync: { DaysToFetch: 14 } })
  );
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => json(r, { SavedColor: "Purple", SuggestedColor: null }));
  await page.route("**/api/SyncApi/getBankConnectionStatus", (r) => json(r, "BankConnected"));
  await page.route("**/api/SyncApi/getCurrentSession", (r) => json(r, null));
  await page.route("**/api/BudgetApi/getTargetCheck", (r) => json(r, { Ok: { Month: "2026-11-01", Today: "2026-11-06", Categories: [] } }));
  await page.route("**/api/YnabApi/getPayees", (r) => json(r, { Ok: [] }));
  await page.route("**/api/YnabApi/getCategories", (r) =>
    json(r, { Ok: [...buildCategoriesFixture(), { Id: { YnabCategoryId: guid(2) }, Name: LONG_CATEGORY, GroupName: "Alltag", Available: eur(12345.67), Hidden: false }] })
  );
  // Die Seitenleiste lädt Konten und Stände beim Start auf jeder Seite (design-system-k6kfw): ein langer Name
  // und ein negativer Stand für Invariante 4.
  await page.route("**/api/LedgerApi/getLedgerAccounts", (r) => json(r, NAV_ACCOUNTS));
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

function ledgerEntries(accountId) {
  const entry = (n, extra) => buildEntryFixture(accountId, n, { Date: TODAY, LinkedToYnab: true, CategorizationWriteBackFailure: null, ...extra });
  const lebensmittel = { CategoryId: CATEGORY_IDS.lebensmittel, CategoryName: "Lebensmittel", CategoryGroupName: "Alltag" };
  return [
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

async function mockLedger(page, { accounts = 3 } = {}) {
  await mockApp(page);
  const accountsFixture = buildAccountsFixture(accounts);
  accountsFixture.Accounts[2].Name = "Gemeinschaftskonto mit einem sehr langen Kontonamen";
  await mockFilterBarApi(page, {
    accountsFixture,
    balancesFixture: buildBalancesFixture(accountsFixture.Accounts).map((b, i) => (i === 2 ? { ...b, Balance: eur(-12345.67) } : b)),
    resolveEntries: () => ledgerEntries(accountsFixture.DefaultAccountId),
  });
  for (const [name, body] of [
    ["getEntryHistory", []],
    ["getLedgerEntry", null],
    ["getPayeeCategorySuggestions", [{ YnabCategoryId: CATEGORY_IDS.bahn }]],
  ]) {
    await page.route(`**/api/LedgerApi/${name}`, (r) => json(r, body));
  }
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
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
}
const openRules = async (page) => page.getByText("Regel eins").locator("visible=true").first().waitFor();

// ============================================================
// Sync
// ============================================================

const mockSync = mockApp;

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
}
/// Die Übersicht steht, sobald der Monat da ist oder sein Platzhalter (beim verzögerten Monat, Invariante 9).
const openOverview = async (page) =>
  page.locator('[data-testid="overview-month"], [data-testid="overview-month-loading"]').first().waitFor();

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
      // Das Picker-Sheet schluckt den nächsten Klick (ADR 0005): warten, bis es zu ist.
      await page.locator(".bottom-sheet.layer-2.active").waitFor({ state: "detached" }).catch(() => {});
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
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="split-amount-0"]'),
      reach: (page) => page.locator('.bottom-sheet.active [data-testid="split-amount-0"]').or(page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true })),
    },
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
    open: async (page) => page.getByText("Spiegel läuft — die Liste bleibt so lange stehen.").waitFor(),
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
      const hint = page.getByText("Spiegel läuft — die Liste bleibt so lange stehen.");
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
  // categorization-cgq35: Prioritätspfeile und Overflow-Knopf am Handy, Regeln mit langen Namen.
  {
    name: "regeln",
    route: "/rules",
    widths: PAGE_WIDTHS,
    mock: mockRules,
    open: openRules,
  },
  // categorization-cgq35, categorization-f955d: das Menü im Sheet, jeder Eintrag voll antippbar.
  {
    name: "regeln-menue",
    route: "/rules",
    widths: HANDY_WIDTHS,
    mock: mockRules,
    open: async (page) => {
      await openRules(page);
      await page.locator('[data-testid="rules-menu-button"] button').click();
      await page.locator('[role="menuitem"]').first().waitFor();
    },
  },
  // categorization-cgq35: am PC bleibt der Regel-Dialog ein Modal, der Picker hängt als Popover ganz im Bild.
  {
    name: "regeln-dialog-picker",
    route: "/rules",
    widths: [1440],
    mock: mockRules,
    open: async (page) => {
      await openRules(page);
      await page.locator('[data-testid="rule-row"]:visible').filter({ hasText: "Regel eins" }).getByText("Regel eins").click();
      await page.locator('[data-testid="rule-dialog"]').waitFor();
      // Erst wenn der Dialog eingefahren ist: öffnet das Popover mitten in der Einfahrt, rechnet sein
      // scrollIntoView mit der noch skalierten Lage, und unter Last stand es danach halb unter dem Rand.
      await settle(page, { where: "regeln-dialog-picker, Dialog offen" });
      await page.locator('[data-testid="rule-category"] button').click();
      await page.locator('[role="option"]').first().waitFor();
      await frames(page);
    },
    primary: (page) => page.locator('[data-testid="category-picker-footer-hint"]:visible'),
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
  },
  // Fix 31f15e0 (banking-import-v8n3k): die Zeile zum Bankstand nimmt beim Laden schon ihre Höhe ein.
  {
    name: "sync-start",
    route: "/sync",
    widths: [...HANDY_WIDTHS, 1440],
    mock: mockSync,
    open: async (page) => page.getByRole("button", { name: "Sync starten" }).waitFor(),
    deferred: {
      api: "**/api/SyncApi/getBankConnectionStatus",
      body: "BankConnected",
      loaded: (page) => page.getByText("Nächster Sync ohne TAN"),
    },
  },
];

export const stateNamed = (name) => {
  const found = STATES.find((s) => s.name === name);
  if (!found) throw new Error(`kein Zustand ${name}`);
  return found;
};
