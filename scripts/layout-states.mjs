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

import { HANDY_WIDTH, PC_WIDTH, settle } from "./layout-invariants.mjs";
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
    json(r, { Entries: [], TotalCount: 0, TotalAmount: eur(0), DeletedCount: 0, NeedsReviewCount: 0, AskLenaCount: 0, WithoutCategoryCount: 0 })
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
  ActivityInYnab: null,
  ...extra,
});

const budgetMonth = (key) => ({
  Month: key,
  ToBeBudgeted: eur(-12345.67),
  CopiedAt: null,
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

// budgeting-gtrgk: eine Verschiebung von Essen nach Reise, geschrieben oder mit fehlgeschlagener Zeile.
const moveReport = (outcome) => ({
  ApplicationId: guid(88),
  Month: CURRENT,
  Lines: [
    { CategoryId: guid(1), CategoryName: "Essen", OldAssigned: eur(0), NewAssigned: eur(-5), Outcome: "Written" },
    { CategoryId: guid(4), CategoryName: "Reise", OldAssigned: eur(0), NewAssigned: eur(5), Outcome: outcome },
  ],
  ActualToBeBudgeted: null,
});

const mockBudgetMove = (outcome) => async (page) => {
  await mockBudget(page);
  await page.route("**/api/BudgetApi/moveMoney", (r) => json(r, { Ok: moveReport(outcome) }));
};

/// Der Monat liest beim ersten Mal, danach scheitert jedes Lesen (Hinweis „Beträge nicht neu gelesen“).
async function mockBudgetRereadFails(page) {
  await mockBudget(page);
  let reads = 0;
  await page.route("**/api/BudgetApi/getBudgetMonth", (r) => {
    reads += 1;
    const [key] = JSON.parse(r.request().postData() ?? "[]");
    return reads === 1 ? json(r, { Ok: budgetMonth(key) }) : json(r, { Error: "YNAB antwortet nicht (503)" });
  });
}

/// Abgeben von Essen nach Reise im Tab Geld, bis die Zeile über dem Knopf steht.
const giveToTrip = async (page, mobile, expected) => {
  await openInspector(page, mobile, "Essen");
  await moneyMode(page, "Abgeben").click();
  await page.locator('[data-testid="transfer-amount"] input:visible').fill("5");
  await page.locator('[data-testid="transfer-to"]:visible').click();
  await page.locator('[role="option"]:visible').filter({ hasText: "Reise" }).last().click();
  await page.locator('[data-testid="transfer-to"]:visible').getByText("Reise").waitFor();
  await page.locator('[data-testid="transfer-amount"] input:visible').waitFor();
  // Das Picker-Sheet schluckt nach der Wahl den nächsten Klick (ADR 0005): ein Klick ohne Wirkung nimmt die Sperre.
  await page.locator('[data-testid="detail-activity"]:visible').click();
  await page.locator('[data-testid="transfer-submit"]:visible button').click();
  await page.locator('[data-testid="feedback-sheet-line"]:visible').filter({ hasText: expected }).waitFor();
};

async function mockTargetCheck(page) {
  await mockBudget(page);
  await page.route("**/api/BudgetApi/getTargetCheck", (r) => json(r, { Ok: targetCheck }));
}

// budgeting-sx9v5: „übernehmen“ schreibt das Target (Bestätigung am bisherigen Platz), oder der Aufruf bricht ab und
// das stille Neuladen der Prüfung scheitert (Hinweis „Prüfung nicht neu gelesen“).
const mockTargetCheckTake = (answer) => async (page) => {
  await mockTargetCheck(page);
  if (answer === "abort") {
    let reads = 0;
    await page.route("**/api/BudgetApi/getTargetCheck", (r) => {
      reads += 1;
      return reads === 1 ? json(r, { Ok: targetCheck }) : json(r, { Error: "YNAB antwortet nicht (503)" });
    });
  }
  await page.route("**/api/BudgetApi/changeCategoryTarget", (r) => {
    if (answer === "abort") return r.abort("connectionreset");
    const [, request] = JSON.parse(r.request().postData() ?? "[]");
    return json(r, { Ok: { TargetWritten: { ...request.Shown, Goal: request.Wanted.NewGoal } } });
  });
};
const takeEssen = async (page) => {
  const row = page.locator('[data-testid="target-check-row"]').filter({ hasText: "Essen" });
  await row.getByRole("button", { name: /übernehmen$/ }).click();
};

// budgeting-pscgb: die Seite Kategorien mit langen Namen von Gruppe und Kategorie.
// budgeting-sn2sn: je Kategorie `Active`, zwei deaktivierte (eine mit langem Namen) für den Abschnitt "Deaktiviert".
// budgeting-nyx75: je Gruppe `Active` (Pflichtfeld in `ManagedCategoryGroup`).
const categoryGroups = [
  {
    Id: guid(901),
    Name: "Alltag",
    Active: true,
    Categories: [
      { Id: guid(1), Name: "Essen", Active: true },
      { Id: guid(2), Name: LONG_CATEGORY, Active: true },
      { Id: guid(4), Name: "Fitnessstudio", Active: false },
    ],
  },
  {
    Id: guid(902),
    Name: "Eine Gruppe mit einem langen Namen bis an die Grenze",
    Active: true,
    Categories: [
      { Id: guid(3), Name: "Kinderkram", Active: true },
      { Id: guid(5), Name: `Alter Kredit ${LONG_CATEGORY}`, Active: false },
    ],
  },
  { Id: guid(903), Name: "Leer", Active: true, Categories: [] },
];

// budgeting-nyx75: eine deaktivierte Gruppe mit langem Namen (ein Eintrag im Abschnitt "Deaktiviert") und eine einzeln
// deaktivierte Kategorie einer aktiven Gruppe.
const categoryGroupsWithInactiveGroup = [
  {
    Id: guid(901),
    Name: "Alltag",
    Active: true,
    Categories: [
      { Id: guid(1), Name: "Essen", Active: true },
      { Id: guid(4), Name: "Fitnessstudio", Active: false },
    ],
  },
  {
    Id: guid(902),
    Name: "Eine Gruppe mit einem langen Namen bis an die Grenze",
    Active: false,
    Categories: [
      { Id: guid(3), Name: "Kinderkram", Active: true },
      { Id: guid(5), Name: "Spielzeug", Active: true },
    ],
  },
];

async function mockCategoryAdmin(page) {
  await mockBudget(page);
  await page.route("**/api/CategoryApi/getCategoryGroups", (r) => json(r, { Ok: categoryGroups }));
}
async function mockCategoryAdminInactiveGroup(page) {
  await mockBudget(page);
  await page.route("**/api/CategoryApi/getCategoryGroups", (r) => json(r, { Ok: categoryGroupsWithInactiveGroup }));
}

// budgeting-sx9v5: Speichern gelingt, das stille Neuladen danach scheitert; die Reihenfolge wird abgelehnt.
async function mockCategoryAdminReloadFails(page) {
  await mockBudget(page);
  let reads = 0;
  await page.route("**/api/CategoryApi/getCategoryGroups", (r) => {
    reads += 1;
    return reads === 1 ? json(r, { Ok: categoryGroups }) : json(r, { Error: "YNAB antwortet nicht (503)" });
  });
  await page.route("**/api/CategoryApi/saveCategory", (r) => json(r, { Ok: guid(1) }));
}
async function mockCategoryAdminOrderFails(page) {
  await mockCategoryAdmin(page);
  await page.route("**/api/CategoryApi/setCategoryOrder", (r) => json(r, { Error: "BB hat die Reihenfolge nicht gespeichert" }));
}
const noToast = async (page, where) => {
  if ((await page.locator(".toast-layer > *").count()) > 0) throw new Error(`Toast neben ${where}`);
};

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

function ledgerEntries(accountId, { held = false, withoutPayee = false } = {}) {
  const entry = (n, extra) => buildEntryFixture(accountId, n, { Date: TODAY, LinkedToYnab: true, CategorizationWriteBackFailure: null, ...extra });
  const lebensmittel = { CategoryId: CATEGORY_IDS.lebensmittel, CategoryName: "Lebensmittel", CategoryGroupName: "Alltag" };
  return [
    ...(held
      ? [entry(10, { Payee: HELD_PAYEE, NeedsReview: true, LinkedToYnab: false, Amount: eur(-12345.67), PossibleDuplicate: POSSIBLE_DUPLICATE, ...lebensmittel })]
      : []),
    entry(1, { Payee: "Bauhaus", NeedsReview: true, Amount: eur(-81.9), ...lebensmittel }),
    // ledger-pkrwd: eine Buchung ohne Payee (aus YNAB oder nach „Payee entfernen“).
    ...(withoutPayee ? [entry(11, { Payee: null, Amount: eur(-4.5), ...lebensmittel })] : []),
    entry(2, {
      Payee: "AMAZON PAYMENTS",
      // design-system-x045q: so lang, dass der Memo-Block im Detail bei 412 px über mindestens drei Zeilen umbricht.
      Memo: "304-1234567-7654321 Lampe, Glühbirnen und Kabel und ein sehr langes Memo, das weit über die Spalte hinausgeht und im Detail über mehrere Zeilen umbricht",
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

/// Drei Einträge der Historie (ledger-zfj78), ältester zuerst wie `getEntryHistory`; einer mit langem Satz.
const DETAIL_HISTORY = [
  { Sequence: 1, OccurredAt: "2026-11-06T18:39:00.0000000", Origin: "MirroredFromYnab", Description: "In YNAB gesehen: AMAZON PAYMENTS, -12,34 €", Amount: eur(-12.34) },
  { Sequence: 2, OccurredAt: "2026-11-06T18:40:00.0000000", Origin: "DecidedByRule", Description: "Regel „Amazon“: Kategorie Lebensmittel gesetzt, ein langer Satz, der in der schmalen Spalte umbrechen muss", Amount: null },
  { Sequence: 3, OccurredAt: "2026-11-07T09:20:00.0000000", Origin: "DecidedInBb", Description: "In BB als geprüft markiert", Amount: null },
];

async function mockLedger(page, { accounts = 3, held = false, withoutPayee = false, hiddenPayees = null } = {}) {
  await mockApp(page);
  const accountsFixture = buildAccountsFixture(accounts);
  accountsFixture.Accounts[2].Name = "Gemeinschaftskonto mit einem sehr langen Kontonamen";
  await mockFilterBarApi(page, {
    accountsFixture,
    balancesFixture: buildBalancesFixture(accountsFixture.Accounts).map((b, i) => (i === 2 ? { ...b, Balance: eur(-12345.67) } : b)),
    // ledger-k68sd: `hiddenPayees` (ein Set) nimmt Buchungen aus jeder späteren Antwort, etwa nach dem Löschen.
    resolveEntries: () =>
      ledgerEntries(accountsFixture.DefaultAccountId, { held, withoutPayee }).filter((e) => !(hiddenPayees && hiddenPayees.has(e.Payee))),
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

/// Der Knopf der Rückmeldung (design-system-gkded): unter 1024 px in der Meldungsleiste, ab 1024 px im einen Toast.
const feedbackAction = (page) =>
  page
    .locator(page.viewportSize().width < 1024 ? '[data-testid="feedback-bar"] [data-feedback-action="true"]' : '[data-toast-action="true"]')
    .first();

/// „Rückgängig“ an der Bestätigung unter einem Feld des offenen Details (ledger-p5y9s): im Sheet am Handy und im
/// Panel am PC, nie in Leiste oder Toast.
const fieldUndo = (page) => page.locator('[data-testid="detail-note"] [data-feedback-action="true"]').filter({ visible: true }).first();

/// Kein Toast im DOM (ab 1024 px der Behälter `Toast.single`, darunter `hidden`): die Meldung steht am Feld.
const expectNoToast = async (page, where) => {
  const toasts = await page.locator(".toast-layer > *").count();
  if (toasts !== 0) throw new Error(`${where}: ${toasts} Toast statt der Bestätigung am Feld`);
};

/// Öffnet das Detail einer Buchung (ledger-enjex): am Handy das Sheet, am PC das Panel neben der Liste.
const openDetailScope = async (page, mobile, payee = "Bauhaus") => {
  if (mobile) return openLedgerDetail(page, payee);
  const row = page.locator("table tbody tr", { hasText: payee }).first();
  await row.waitFor();
  await row.locator("td").nth(2).click();
  const panel = page.locator('[data-testid="ledger-detail-panel"]');
  await panel.waitFor();
  return panel;
};

/// Tipp am Handy, Klick am PC.
const press = (locator, mobile) => (mobile ? locator.tap() : locator.click());

/// Wischt per Touch nach rechts über die Zeile eines Payees (CDP, wie `scripts/ledger-review-mark.test.mjs`): der
/// Wisch prüft die Buchung.
export const swipeReviewed = async (page, payee) => {
  const row = page.locator("button", { hasText: payee }).filter({ visible: true }).first();
  await row.waitFor();
  // Mitten ins Bild, damit keine feste Leiste unten über der Zeile liegt.
  await row.evaluate((el) => el.scrollIntoView({ block: "center", behavior: "instant" }));
  await frames(page);
  const box = await row.boundingBox();
  const cdp = await page.context().newCDPSession(page);
  const y = box.y + box.height / 2;
  const x0 = box.x + 24;
  const point = (x) => [{ x, y, id: 1 }];
  await cdp.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: point(x0) });
  for (let i = 1; i <= 8; i++) await cdp.send("Input.dispatchTouchEvent", { type: "touchMove", touchPoints: point(x0 + (260 * i) / 8) });
  await cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
  await cdp.detach();
};

/// Der Prüfvermerk antwortet „gesetzt und zu YNAB geschickt“, vorher war die Buchung ungeprüft.
const mockReviewMark = (page) =>
  page.route("**/api/LedgerApi/setReviewMark", (r) =>
    json(r, { Ok: { Previous: { IsApproved: false, ClearedState: "ReviewUncleared" }, Change: "ReviewMarkChanged", WriteBack: "ReviewMarkSent" } })
  );

const openLedger = async (page) => page.getByText("Bauhaus", { exact: true }).locator("visible=true").first().waitFor();
const ledgerSheet = (page) => page.locator(".bottom-sheet.active").first();

/// Schließt das offene Sheet über „Schließen“. Nach einer Wahl im Picker schluckt `Viewport.swallowNextClick` den
/// nächsten Klick innerhalb von 350 ms (Schutz vor dem Geisterklick, ADR 0005); seit am Handy kein Toast mehr
/// einblendet (design-system-gkded), ist die Seite davor schon ruhig. Deshalb wird erneut geklickt, bis das Sheet zu ist.
export const closeLedgerSheet = async (page) => {
  for (let attempt = 0; attempt < 5; attempt++) {
    await ledgerSheet(page).getByRole("button", { name: "Schließen" }).click();
    const closed = await page
      .waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0, null, { timeout: 1000 })
      .then(() => true, () => false);
    if (closed) return;
  }
  throw new Error("Das Sheet schließt nicht");
};
const openLedgerDetail = async (page, payee) => {
  await page.getByText(payee, { exact: true }).first().waitFor();
  await page.getByText(payee, { exact: true }).first().tap();
  const sheet = ledgerSheet(page);
  await sheet.waitFor();
  return sheet;
};

/// Unter 1024 px: der Knopf „Filter“ öffnet das Filter-Sheet (ledger-d9z3n).
const openHandyFilterSheet = async (page) => {
  await page.getByRole("button", { name: /^Filter( \(\d+\))?$/ }).locator("visible=true").first().click();
  const sheet = page.locator('.bottom-sheet.active [data-testid="ledger-filter-sheet"]');
  await sheet.waitFor();
  return sheet;
};

/// Unter 1024 px eine Zeile des Filter-Sheets antippen (Kategorie, Zeitraum, Sortieren): die Auswahl ersetzt den Inhalt
/// des Sheets, oben steht „Zurück“ (ledger-npqcb). Ein zweites Sheet darüber bricht den Zustand ab.
const openHandyFilterRow = async (page, testId, waitForText) => {
  await openHandyFilterSheet(page);
  await page.locator(`.bottom-sheet.active [data-testid="${testId}"]`).click();
  await page.locator(".bottom-sheet.active").getByText(waitForText, { exact: true }).first().waitFor();
  await page.locator('.bottom-sheet.active [data-testid="filter-sheet-back"]').waitFor();
  const layers = await page.locator(".bottom-sheet.active").count();
  if (layers !== 1) throw new Error(`Auswahl im Filter-Sheet: ${layers} Sheets offen statt einem`);
  if ((await page.locator('.bottom-sheet.active [data-testid="ledger-filter-sheet"]').count()) !== 0)
    throw new Error("Auswahl im Filter-Sheet: die Filter stehen noch darunter");
};

/// Kategorie-Filter „Lebensmittel“, dann Bauhaus im Detail auf „Nebenkosten“: die Zeile passt nicht mehr zum
/// Filter und bleibt ausgegraut stehen (ledger-dn4vy, Vorlage scripts/ledger-category-change.test.mjs).
const openFilteredOut = async (page, { mobile }) => {
  await openLedger(page);
  if (mobile) {
    await openHandyFilterRow(page, "filter-category", "Lebensmittel");
    const layer = page.locator(".bottom-sheet.active").first();
    await layer.getByText("Lebensmittel", { exact: true }).last().click();
    await layer.getByRole("button", { name: "Übernehmen (1)" }).click();
    await page.locator('.bottom-sheet.active [data-testid="ledger-filter-sheet"]').waitFor();
    await page.locator(".bottom-sheet.active").getByRole("button", { name: /zeigen$/ }).click();
    await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
    await page.getByRole("button", { name: "Kategorie: Lebensmittel entfernen" }).waitFor();
  } else {
    await page.getByRole("button", { name: /^Kategorie/ }).locator("visible=true").first().click();
    const layer = page.locator('[data-layer="popover"]').first();
    await layer.getByText("Lebensmittel", { exact: true }).last().click();
    await layer.getByRole("button", { name: "Übernehmen (1)" }).click();
    // Seit ledger-cnaz3 nennt der Chip am PC die Kategorie beim Namen.
    await page.getByRole("button", { name: "Kategorie: Lebensmittel", exact: true }).locator("visible=true").first().waitFor();
  }
  await openLedger(page);
  const payee = page.getByText("Bauhaus", { exact: true }).locator("visible=true").first();
  if (mobile) await payee.tap();
  else await payee.click();
  const arrow = page.locator('[data-testid="detail-category-arrow"]').filter({ visible: true }).first();
  await arrow.click();
  // Ohne die Optionen des Kategorie-Filters: dessen geschlossenes Sheet steht weiter im DOM (role="option" seit
  // design-system-gcpws).
  await page.locator('[role="option"]:not([id^="category-multi-select-option-"])', { hasText: "Nebenkosten" }).filter({ visible: true }).last().click();
  await fieldUndo(page).waitFor();
  await page.locator('[data-testid="filtered-out-note"]').filter({ visible: true }).first().waitFor();
  if (mobile) {
    // Erst wenn der Picker zu ist und das Detail steht, schließt „Schließen“ es.
    await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 1);
    await settle(page, { where: "ausgegraute Zeile, Detail offen" });
    await closeLedgerSheet(page);
  } else {
    // Ab 1024 px steht das Detail im Panel, das immer da ist (ledger-cnaz3): es schließt zum leeren Panel.
    await settle(page, { where: "ausgegraute Zeile, Detail offen" });
    await page.getByTestId("detail-close").filter({ visible: true }).first().click();
    await page.getByTestId("ledger-panel-empty").filter({ visible: true }).waitFor();
  }
};

/// Die Gesamtsicht aus der Seitenleiste, unter 1024 px aus dem Auswahlfeld „Konto“ (ledger-d9z3n): gewartet wird
/// auf die Liste aller Konten.
const openAllAccounts = async (page) => {
  await openLedger(page);
  const loaded = page.waitForResponse("**/api/LedgerApi/getLedgerEntriesPage");
  if (page.viewportSize().width < 1024) {
    await page.getByTestId("ledger-account-field").click();
    await page.locator('.bottom-sheet.active [data-testid="ledger-account-sheet"]').getByText("Alle Konten", { exact: true }).click();
  } else {
    await page.locator("aside").getByText("Alle Konten", { exact: true }).filter({ visible: true }).first().click();
  }
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
/// Öffnet die Regel mit dem langen Namen und löscht sie; das Formular schließt (categorization-t6j00).
const deleteLongRule = async (page, mobile) => {
  await openRule(page, "Eine Regel mit einem sehr langen Namen");
  const remove = page.locator('[data-testid="rule-form"]').getByRole("button", { name: "Regel löschen", exact: true });
  await (mobile ? remove.tap() : remove.click());
  await page.locator('[data-testid="rule-form"]').waitFor({ state: "detached" });
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

/// banking-import-vwhfm: YNAB „Verbindung testen“ liefert ein Budget mit zwei Konten, so stehen im Formular die
/// Auswahlfelder. Lena-Farbe speichern gelingt, das Quick-Add-Konto lehnt der Server ab.
async function mockSettingsFeedback(page) {
  await mockSettings(page);
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => json(r, { SavedColor: null, SuggestedColor: "purple" }));
  await page.route("**/api/SettingsApi/saveLenaFlagColor", (r) => json(r, { Ok: null }));
  await page.route("**/api/SettingsApi/testYnabConnection", (r) =>
    json(r, {
      Ok: [
        {
          Budget: { Id: W("YnabBudgetId", "fake-budget-id"), Name: "Familie" },
          Accounts: [
            { Id: W("YnabAccountId", guid(9101)), Name: "Girokonto", Balance: eur(1234.5), OnBudget: true, Closed: false },
            { Id: W("YnabAccountId", guid(9102)), Name: "Barkasse", Balance: eur(84.2), OnBudget: true, Closed: false },
          ],
          Categories: [],
        },
      ],
    })
  );
  await page.route("**/api/YnabApi/setQuickAddAccount", (r) => json(r, { Error: { NetworkError: "YNAB antwortet nicht" } }));
}

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
    json(r, { Entries: entries, TotalCount: 6, TotalAmount: eur(-1000), DeletedCount: 0, NeedsReviewCount: 0, AskLenaCount: 0, WithoutCategoryCount: 0 })
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

/// Ein Memo mit 500 Zeichen (die Grenze von YNAB, ADR 0062) für die Karte im Durchgehen (ledger-wg42w).
const LONG_MEMO = "Sammelbestellung für das Sommerfest im Garten: Getränke, Kohle, Servietten, Pappteller, Grillanzünder, Salate und die Torte für den Geburtstag von Oma. ".repeat(4).slice(0, 500);

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
    // ledger-gy77x: eine gefragte Buchung mit langem Payee, der Knopf heißt „Rückfrage zurücknehmen“.
    lena: buildEntryFixture(accountId, 65, { ...base, Payee: LONG_PAYEE, Amount: eur(-123456.78), Flag: "AskLena", ...lebensmittel }),
    // design-system-x045q: ein Memo über drei Zeilen für den mehrzeiligen Editor im Detail.
    memo: buildEntryFixture(accountId, 66, {
      ...base,
      Payee: "AMAZON PAYMENTS",
      Amount: eur(-21.5),
      Memo: "Wocheneinkauf fürs Grillfest am Samstag, dazu Getränke, Kohle, Servietten und die Torte für den Geburtstag von Oma am Sonntagnachmittag",
      ...lebensmittel,
    }),
    // ledger-wg42w: das Memo auf der Karte nur zum Lesen, so lang, wie Platz ist. Kurz, mittel (Skizze ledger-vf5h6
    // Bild 01, ohne Bestell-Link und Lena-Hinweis) und lang (500 Zeichen mit langem Payee, gekürzt).
    memoShort: buildEntryFixture(accountId, 67, { ...base, Payee: "Stadtwerke", Amount: eur(-86), Memo: "Abschlag November", ...lebensmittel }),
    memoMedium: buildEntryFixture(accountId, 68, {
      ...base,
      Payee: "AMAZON PAYMENTS",
      Origin: "MirroredFromYnab",
      Amount: eur(-21.5),
      Memo: "Wocheneinkauf fürs Grillfest am Samstag, dazu Getränke, Kohle und die Torte für den Geburtstag",
      ...lebensmittel,
    }),
    memoLong: buildEntryFixture(accountId, 69, { ...base, Payee: LONG_PAYEE, Amount: eur(-123456.78), Memo: LONG_MEMO, ...lebensmittel }),
    // ledger-cyd1t: „Aufteilen“ gesperrt (Tracking-Konto, Grund unter dem Link) und eine aufgeteilte Buchung („Aufgeteilt“
    // mit Pfeil).
    splitLocked: buildEntryFixture(accountId, 70, {
      ...base,
      Payee: "Sparplan ETF",
      Amount: eur(-150),
      ...lebensmittel,
      SplitLocks: {
        CannotSplitReason: "Auf einem Tracking-Konto lassen sich Buchungen nicht aufteilen",
        CannotChangeReason: "Auf einem Tracking-Konto lassen sich Buchungen nicht aufteilen",
        ReplacementPending: false,
      },
    }),
    split: buildEntryFixture(accountId, 71, {
      ...base,
      Payee: "Frischemarkt Nord",
      Amount: eur(-86.4),
      CategoryId: null,
      CategoryName: null,
      CategoryGroupName: null,
      Splits: [
        { Target: { ToCategory: [{ YnabCategoryId: CATEGORY_IDS.lebensmittel }, "Lebensmittel"] }, Amount: eur(-61.4), Memo: null },
        { Target: "Uncategorized", Amount: eur(-25), Memo: null },
      ],
    }),
  };
})();

/// Die Seite „Durchgehen“ mit dieser Schlange. Das Neulesen einer Karte liefert ihren Stand aus `state`, damit
/// Befehle (Kategorie, Prüfvermerk, Verwerfen) ihn ändern können; `state.calls` sammelt die Befehle.
/// `failReviewMark`: der Prüfvermerk scheitert mit diesem Grund (ledger-pz5wv, Zustand `durchgehen-fehler`).
export async function mockReview(page, queue, { failReviewMark = null } = {}) {
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
    if (failReviewMark) return answer(r, { Error: failReviewMark });
    state.byId.set(id, { ...state.byId.get(id), NeedsReview: false });
    return answer(r, { Ok: { Previous: { IsApproved: false, ClearedState: "ReviewUncleared" }, Change: "ReviewMarkChanged", WriteBack: "ReviewMarkSent" } });
  });
  // „Rückgängig“ nach „Passt“ (ledger-pz5wv): der Vorzustand aus der Antwort kommt zurück.
  await page.route("**/api/LedgerApi/restoreReviewMark", (r) => {
    const [id, previous] = r.request().postDataJSON();
    state.calls.push({ endpoint: "restoreReviewMark", id, previous });
    state.byId.set(id, { ...state.byId.get(id), NeedsReview: true });
    return answer(r, { Ok: { Previous: { IsApproved: true, ClearedState: "ReviewUncleared" }, Change: "ReviewMarkChanged", WriteBack: "ReviewMarkSent" } });
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
/// Das Durchgehen mit offenen „Details“ (ledger-hme9k): am Handy das Detail-Sheet, am PC das Panel neben der Karte.
const openReviewDetails = async (page) => {
  // Erst wenn die Vorschläge stehen, steht die Karte still.
  await page.locator('[data-testid="review-suggestions"]').first().waitFor();
  await page.locator('[data-testid="review-details"]').first().click();
  await page.locator('[data-testid="detail-payee"]').filter({ visible: true }).first().waitFor();
};
/// Die Zeile unter dem Hauptknopf am Handy (ledger-s2cg3): ohne Scrollen im Bild. Am PC ist sie ohne letzte Aktion leer
/// (ledger-pz5wv) und wird hier nicht geprüft.
const reviewFoot = (page, { mobile }) => (mobile ? page.locator('[data-testid="review-foot"]') : null);
/// Der Memo-Text auf der Karte (ledger-wg42w), nachdem die Vorschläge stehen: gekürzt (`-webkit-line-clamp`), Zahl der
/// sichtbaren Zeilen, `title`, und ob der Scrollbereich der Karte überläuft.
const reviewMemoText = async (page) => {
  await page.locator('[data-testid="review-suggestions"]').first().waitFor();
  await frames(page);
  return page.locator('[data-testid="review-memo-text"]').first().evaluate((el) => {
    const s = getComputedStyle(el);
    const area = el.closest("[data-review-scroll]");
    return {
      clamped: s.webkitLineClamp !== "none" && s.webkitLineClamp !== "",
      lines: Math.round(el.clientHeight / parseFloat(s.lineHeight)),
      title: (el.getAttribute("title") ?? "").length > 0,
      overflow: area.scrollHeight > area.clientHeight + 1,
      // Gekürzt wird nur das Memo: keine andere Zeile der Karte ist gestaucht (eine Zeile mit fester Mindesthöhe,
      // etwa der Payee-Knopf, schrumpfte sonst im Flex-Layout und überlappte den Betrag).
      squeezed: [...area.children]
        .filter((c) => !c.contains(el) && c.scrollHeight > c.clientHeight + 1)
        .map((c) => c.getAttribute("data-testid") ?? c.tagName),
    };
  });
};
const reviewPrimary = (name) => (page) => page.getByRole("button", { name, exact: true }).locator("visible=true").first();

// ============================================================
// Die Zustände
// ============================================================

export const STATES = [
  // Bug F3 (budgeting-kjsht), Bug D3, budgeting-c6vn4: Zeilen mit Target am Handy, sticky Kopf, Breiten.
  {
    name: "budget",
    route: "/budget",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockBudget,
    open: openBudget,
  },
  // budgeting-c5w2n: Tab Geld im Sheet, Feld und Knopf ohne Scrollen, auch bei offener Tastatur.
  {
    name: "budget-geld-zuweisen",
    route: "/budget",
    widths: [HANDY_WIDTH],
    mock: mockBudget,
    open: async (page, { mobile }) => {
      await openInspector(page, mobile, "Essen");
      await moneyMode(page, "Zuweisen").click();
      await page.locator('[data-testid="assign-field"] input:visible').waitFor();
    },
    primary: (page) => page.locator('.bottom-sheet.active [data-testid="assign-missing"] button'),
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="assign-field"] input'),
      reach: (page) => page.locator('.bottom-sheet.active [data-testid="assign-field"] input, .bottom-sheet.active [data-testid="assign-available-after"]'),
    },
  },
  // Lange Kategorienamen in von, nach und im Knopf (budget-money-tab), am Handy und im Panel.
  {
    name: "budget-geld-abgeben",
    route: "/budget",
    widths: [HANDY_WIDTH, PC_WIDTH],
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
  // budgeting-gtrgk (Regel R3): nach dem Abgeben steht „✓ 5,00 € nach Reise“ über dem Knopf, der Knopf bleibt ohne
  // Scrollen erreichbar, auch bei offener Tastatur; im Panel am PC ohne Toast. Rechts in der Zeile steht
  // „Rückgängig“ (budgeting-y1kkr), seine Trefferfläche prüft Invariante 2.
  {
    name: "budget-geld-ergebnis",
    route: "/budget",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockBudgetMove("Written"),
    open: async (page, { mobile }) => {
      await giveToTrip(page, mobile, "5,00 € nach Reise");
      await page.locator('[data-testid="feedback-sheet-line"]:visible [data-feedback-action]', { hasText: "Rückgängig" }).waitFor();
      if ((await page.locator(".toast-layer > *").count()) > 0) throw new Error("Toast neben der Zeile im Sheet");
    },
    primary: (page) => page.locator('[data-testid="transfer-submit"]:visible button'),
    keyboard: {
      focus: (page) => page.locator('[data-testid="transfer-amount"]:visible input'),
      reach: (page) => page.locator('[data-testid="transfer-amount"]:visible input'),
    },
  },
  // budgeting-gtrgk: die rote Zeile mit „Erneut senden“ über dem Knopf, dazu der Hinweis der offenen Verschiebung.
  {
    name: "budget-geld-fehler",
    route: "/budget",
    widths: [HANDY_WIDTH],
    mock: mockBudgetMove({ Failed: "YNAB hat zu viele Anfragen gemeldet (429), später erneut senden" }),
    open: async (page, { mobile }) => {
      await giveToTrip(page, mobile, "Nicht vollständig verschoben");
      await page.locator('[data-testid="feedback-sheet-line"]:visible [data-feedback-action]').waitFor();
    },
    primary: (page) => page.locator('[data-testid="transfer-submit"]:visible button'),
  },
  // budgeting-gtrgk: der Hinweis „Beträge nicht neu gelesen“ unter „Zu verteilen“, nach der Rückkehr aus der
  // allgemeinen Historie, deren stilles Neu-Lesen scheitert. Am PC genauso, ohne Toast.
  {
    name: "budget-nicht-neu-gelesen",
    route: "/budget",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockBudgetRereadFails,
    open: async (page) => {
      await openBudget(page);
      await page.locator('[data-testid="budget-history-button"] button').click();
      await page.getByRole("button", { name: "Zurück zum Budget" }).click();
      await page.locator('[data-testid="budget-stale"]').waitFor();
      if ((await page.locator(".toast-layer > *").count()) > 0) throw new Error("Toast neben dem Hinweis");
    },
    primary: (page) => page.locator('[data-testid="budget-stale"] button'),
  },
  // budgeting-gffps, budgeting-nfrds: der Kategorie-Picker als Sheet am Handy (16 px Suche) und als Popover
  // im Panel (ganz sichtbar, vom Panel nicht abgeschnitten, Fuß nicht verdeckt).
  {
    name: "budget-picker",
    route: "/budget",
    widths: [HANDY_WIDTH, PC_WIDTH],
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
    // design-system-gcpws, Abweichung (a) vom Prototyp (G11): das Panel nimmt 30 % des Fensters; neben ihm steht die
    // Budget-Tabelle (rund 640 px Liste, Schwelle `@min-[40rem]`) erst ab rund 1340 px, darunter die Zeilen wie am
    // Handy. Bei PC_WIDTH steht die Tabelle neben dem Panel. Die Regression budgeting-wnkgj (bei 1280 px ragte die
    // Tabelle 40 px unter das Panel) entfällt mit den Breiten unter 1340 px, der Sweep besucht sie nicht mehr.
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockBudget,
    open: async (page, { mobile, width }) => {
      await openInspector(page, mobile, "Essen", "Buchungen");
      await page.locator('[data-testid="detail-entry"]:visible').first().waitFor();
      if (!mobile) {
        const table = await page.locator('[data-testid="budget-page"] table').filter({ visible: true }).count();
        if (table === 0) throw new Error(`Budget-Tabelle bei ${width} px nicht sichtbar`);
      }
    },
  },
  // budgeting-x4k7p, budgeting-v6m3z: Tab Historie mit langem Fehlgrund und Ausgaben-Balken.
  {
    name: "budget-historie-tab",
    route: "/budget",
    widths: [HANDY_WIDTH, PC_WIDTH],
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
    widths: [HANDY_WIDTH],
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
    widths: [PC_WIDTH],
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
    widths: [HANDY_WIDTH, PC_WIDTH],
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
    widths: [HANDY_WIDTH, PC_WIDTH],
    // Invariante 10 (ledger-dn4vy): „Verfügbar“ 0,00 € in `text-muted` hat auf seiner Fläche 4,44:1. Kann-Sammlung
    // budgeting. Auf den Text des Treffers verengt, ein anderer Betrag in der Spalte fällt weiter auf.
    exceptions: [
      { width: PC_WIDTH, inv: 10, selector: '[data-testid="budget-available"] span "0,00 €"' },
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
  // budgeting-h2brj: die kurze Auswahl des Wochentags, am PC ein Dialog über dem Panel, am Handy anstelle des Tabs im
  // Sheet mit „Zurück“ oben.
  {
    name: "budget-target-wochentag-auswahl",
    route: "/budget",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockBudget,
    open: async (page, { mobile }) => {
      const scope = await openInspector(page, mobile, "Essen", "Target");
      await scope.getByRole("button", { name: "Woche", exact: true }).click();
      await scope.locator('[data-testid="target-weekday-field"]').click();
      await page.locator('[data-testid="target-weekday-options"]:visible button').first().waitFor();
    },
  },
  // budgeting-xp46a: die Warnung vor dem Wechsel der Art im Sheet ohne Scrollen.
  {
    name: "budget-target-warnung",
    route: "/budget",
    widths: [HANDY_WIDTH],
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
    widths: [HANDY_WIDTH, PC_WIDTH],
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
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockTargetsAnswer({ PreviewChanged: targetsPreview }),
    open: openTargetsAndApply("targets-changed"),
  },
  // Fix 5850313 (ADR 0075): der Bericht mit einer Zeile „in YNAB geändert“ (vorher `Badge.warning`) und dem Hinweis,
  // dass Zu verteilen von der Vorschau abweicht (vorher `ErrorDisplay.warning`). Beides zeichnete Pink.
  {
    name: "budget-targets-bericht",
    route: "/budget",
    widths: [HANDY_WIDTH, PC_WIDTH],
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
    widths: [HANDY_WIDTH, PC_WIDTH],
    // Invariante 10 (ledger-dn4vy): der rote Satz „1 von 2 fehlgeschlagen“ hat 4,46:1 auf seiner Fläche. Kann-Sammlung
    // budgeting.
    exceptions: [
      { width: PC_WIDTH, inv: 10, selector: '[data-testid="general-history-partly-failed"] span' },
    ],
    mock: mockBudget,
    open: async (page) => {
      await page.locator('[data-testid="general-history-rows"] [data-testid="history-row"]').first().waitFor();
      await page.locator('[data-testid="general-history-rows"] [data-testid="history-toggle"]:visible').first().click();
    },
  },
  // budgeting-yarty: die offene Auswahl „Historie zeigen“ hinter dem Feld „Zeigen“, am Handy im Sheet, am PC als Dialog.
  {
    name: "budget-historie-zeigen",
    route: "/budget/history",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockBudget,
    open: async (page) => {
      await page.locator('[data-testid="general-history-rows"] [data-testid="history-row"]').first().waitFor();
      await page.locator('[data-testid="general-history-filter-field"]').click();
      await page.locator('[data-testid="general-history-filter-options"]:visible button').first().waitFor();
    },
  },
  // budgeting-q4v8n, budgeting-c4ve4: die Seite Targets prüfen mit langen Namen, Knöpfen je Zeile und Mini-Balken.
  {
    name: "targets-pruefen",
    route: "/budget/target-check",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockTargetCheck,
    open: async (page) => page.locator('[data-testid="target-check-row"]').first().waitFor(),
  },
  // budgeting-c4ve4: Anpassen öffnet am PC das Panel rechts, am Handy ein Sheet.
  {
    name: "targets-pruefen-anpassen",
    route: "/budget/target-check",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockTargetCheck,
    open: async (page) => {
      const row = page.locator('[data-testid="target-check-row"]').filter({ hasText: "Essen" });
      await row.getByRole("button", { name: "Anpassen …" }).click();
      await page.locator('[data-testid="target-check-form"]:visible').first().waitFor();
    },
  },
  // budgeting-sx9v5: nach „übernehmen“ verlässt Essen die Gruppe „Gibst du mehr aus?“; die Bestätigung steht als schmale
  // Zeile an ihrem bisherigen Platz, am PC genauso, ohne Toast.
  {
    name: "targets-pruefen-uebernommen",
    route: "/budget/target-check",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockTargetCheckTake("written"),
    open: async (page) => {
      await takeEssen(page);
      await page.locator('[data-testid="target-check-moved-line"]').filter({ hasText: "Essen: Target jetzt 150 € monatlich" }).waitFor();
      await noToast(page, "der Bestätigung");
    },
  },
  // budgeting-sx9v5: der Aufruf bricht ab, das stille Neuladen scheitert: rot in der Zeile, oben der Hinweis mit „Neu laden“.
  {
    name: "targets-pruefen-nicht-neu-gelesen",
    route: "/budget/target-check",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockTargetCheckTake("abort"),
    open: async (page) => {
      await takeEssen(page);
      await page.locator('[data-testid="target-check-stale"]').waitFor();
      await page.locator('[data-testid="target-check-row-line"]').waitFor();
      await noToast(page, "dem Hinweis");
      // Der Klick auf „übernehmen“ scrollt zur Zeile; ob die Seite danach oben steht, hing davon ab, ob die Liste während
      // des gescheiterten Neu-Lesens kurz kürzer gerendert wurde (Wettlauf, Gate-Flake 2026-11-21). Geprüft wird das
      // Layout des Hinweises oben, deshalb von dort. Erst den Endzustand abwarten: während „liest neu“ (aria-busy)
      // folgt noch ein Rendern, nach dem die Zeile „Essen“ sich wieder ins Bild rollt. Es scrollt ein Container der
      // Seite, nicht das Fenster: jeden scrollenden Vorfahren des Hinweises nach oben.
      await page.locator('[data-testid="target-check-stale"] button:not([aria-busy="true"])').waitFor();
      await page.locator('[data-testid="target-check-stale"]').evaluate(async (el) => {
        // Zwei Frames: die Effekte des letzten Renderns (auch das Einrollen der Zeile) sind dann gelaufen.
        await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
        for (let n = el.parentElement; n; n = n.parentElement) if (n.scrollTop > 0) n.scrollTop = 0;
        window.scrollTo(0, 0);
      });
    },
    primary: (page) => page.locator('[data-testid="target-check-stale"] button'),
  },
  // budgeting-pscgb: die Seite Kategorien (Budget, Menü "Kategorien verwalten").
  {
    name: "kategorien",
    route: "/budget/categories",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockCategoryAdmin,
    open: async (page) => page.locator('[data-testid="categories-row"]').first().waitFor(),
  },
  // budgeting-sn2sn: der Abschnitt "Deaktiviert (n)" aufgeklappt, mit "Aktivieren" je Kategorie.
  {
    name: "kategorien-deaktiviert",
    route: "/budget/categories",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockCategoryAdmin,
    open: async (page) => {
      await page.locator('[data-testid="categories-inactive-toggle"]').click();
      await page.locator('[data-testid="categories-inactive-row"]').first().waitFor();
    },
  },
  // budgeting-nyx75: eine deaktivierte Gruppe als ein Eintrag (langer Name) vor einer einzeln deaktivierten Kategorie,
  // der Abschnitt aufgeklappt.
  {
    name: "kategorien-gruppe-deaktiviert",
    route: "/budget/categories",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockCategoryAdminInactiveGroup,
    open: async (page) => {
      await page.locator('[data-testid="categories-inactive-toggle"]').click();
      await page.locator('[data-testid="categories-inactive-group"]').first().waitFor();
    },
  },
  // budgeting-nyx75: „Bearbeiten“ am Gruppenkopf öffnet „Gruppe bearbeiten“ mit dem Schalter „Aktiv“.
  {
    name: "kategorien-gruppe-bearbeiten",
    route: "/budget/categories",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockCategoryAdmin,
    open: async (page) => {
      await page.locator('[data-testid="categories-edit-group"]').first().click();
      await page.locator('[data-testid="categories-group-active"]:visible').first().waitFor();
    },
  },
  // budgeting-pscgb: Tipp auf eine Kategorie öffnet das Formular, am PC als Panel rechts, am Handy im Sheet.
  {
    name: "kategorien-bearbeiten",
    route: "/budget/categories",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockCategoryAdmin,
    open: async (page) => {
      await page.locator('[data-testid="categories-row"]').filter({ hasText: "Essen" }).click();
      await page.locator('[data-testid="categories-form"]:visible').first().waitFor();
    },
  },
  // design-system-gcpws (G13): die Auswahl der Gruppe, am PC als Dialog über dem Panel, am Handy im Sheet.
  {
    name: "kategorien-gruppe-auswahl",
    route: "/budget/categories",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockCategoryAdmin,
    open: async (page) => {
      await page.locator('[data-testid="categories-row"]').filter({ hasText: "Essen" }).click();
      await page.locator('[data-testid="categories-group-field"]:visible').first().click();
      await page.locator('[data-testid="categories-group-choice"]:visible button').first().waitFor();
    },
  },
  // budgeting-sx9v5: nach dem Speichern scheitert das stille Neuladen: Hinweis oben mit „Neu laden“, die gespeicherte Zeile
  // leuchtet, am PC genauso, ohne Toast.
  {
    name: "kategorien-nicht-neu-geladen",
    route: "/budget/categories",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockCategoryAdminReloadFails,
    open: async (page) => {
      await page.locator('[data-testid="categories-row"]').filter({ hasText: "Essen" }).click();
      const form = page.locator('[data-testid="categories-form"]:visible').first();
      await form.getByRole("textbox", { name: "Name" }).fill("Essen gehen");
      await page.getByRole("button", { name: "Speichern" }).locator("visible=true").click();
      await page.locator('[data-testid="categories-stale"]').waitFor();
      await page.locator('[data-testid="categories-form"]:visible').waitFor({ state: "detached" });
      await noToast(page, "dem Hinweis");
    },
    primary: (page) => page.locator('[data-testid="categories-stale"] button'),
  },
  // budgeting-sx9v5: die Reihenfolge wird abgelehnt: rot unter dem Kopf der Gruppe, am PC genauso, ohne Toast.
  {
    name: "kategorien-fehler-reihenfolge",
    route: "/budget/categories",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockCategoryAdminOrderFails,
    open: async (page) => {
      await page.getByRole("button", { name: "Essen nach unten" }).click();
      await page.locator('[data-testid="categories-order-error"]').waitFor();
      await noToast(page, "dem Fehler");
    },
  },
  // ledger-qh9qx, ledger-qns4j (AC5), Regression ledger-filter-bar: Spaltenköpfe, Statuszelle mit zwei
  // Kennzeichen, die Filterleiste haftet ohne den Tabellenkopf zu verdecken. Unter 1024 px die Handy-Form ohne
  // Konten-Spalte (ledger-d9z3n).
  {
    name: "kassenbuch",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockLedger,
    open: openLedger,
  },
  // ledger-qh9qx (Fix 38f2118): in der Gesamtsicht mit Konto-Spalte blendet die Tabelle in schmalen Fenstern
  // stufenweise Spalten aus (Memo, dann Konto), statt Payee, Kategorie und Memo zusammenzudrücken. Der Zustand
  // `kassenbuch` zeigt das Standardkonto, also die Ansicht eines Kontos ohne Konto-Spalte.
  {
    name: "kassenbuch-alle-konten",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockLedger,
    open: openAllAccounts,
  },
  // Die Navbar ist `h-16` (4rem): bei 20 px Grundschrift wächst sie, Filterleiste, Tabellenkopf und
  // Konten-Spalte haften trotzdem darunter (`--nav-height` in styles.css).
  {
    name: "kassenbuch-grosse-schrift",
    route: "/ledger",
    widths: [PC_WIDTH],
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await page.addStyleTag({ content: "html { font-size: 20px; }" });
    },
  },
  // design-system-btwhg: Datumsfeld und Kategorie-Suche am Handy mit 16 px; seit ledger-d9z3n aus dem Filter-Sheet, seit
  // ledger-npqcb an Stelle der Filter im selben Sheet. Beim Tippen in der Suche bleibt der Fokus im Feld (die Tastatur
  // bleibt offen); mit offener Tastatur steht der erste Treffer im Bild. „Übernehmen“ lag dann bei 320 und
  // 390 px unter der Tastatur (wie vorher im zweiten Sheet), Kann-Sammlung ledger; diese Breiten misst der Sweep nicht mehr.
  {
    name: "kassenbuch-filter-kategorie",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await openHandyFilterRow(page, "filter-category", "Lebensmittel");
      const search = page.locator('.bottom-sheet.active input[placeholder="Kategorie suchen…"]');
      await search.focus();
      await search.pressSequentially("leb");
      await page.locator(".bottom-sheet.active").getByText("Lebensmittel", { exact: true }).first().waitFor();
      if (!(await search.evaluate((el) => el === document.activeElement)))
        throw new Error("Kategorie-Suche im Filter-Sheet verlor beim Tippen den Fokus");
      await search.fill("");
      await search.evaluate((el) => el.blur());
    },
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active input[placeholder="Kategorie suchen…"]'),
      reach: (page) => page.locator('.bottom-sheet.active [role="option"]').first(),
    },
  },
  {
    name: "kassenbuch-filter-zeitraum",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await openHandyFilterRow(page, "filter-date", "Schnellwahl");
    },
  },
  // ledger-dn4vy: Betrag Von/Bis im Filter-Sheet. Vorher ohne Namen (der Zustand fand das Feld nicht), ohne
  // Zifferntastatur und 36 px hoch; mit unlesbarem Bis steht der Grund am Feld.
  {
    name: "kassenbuch-filter-betrag",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await openHandyFilterSheet(page);
      const sheet = page.locator(".bottom-sheet.active");
      const from = sheet.getByRole("textbox", { name: "Betrag von" });
      const to = sheet.getByRole("textbox", { name: "Betrag bis" });
      // Beide unlesbar: ein gültiger Wert setzte den Filter, der Chip hinter dem Sheet gehört nicht zu diesem
      // Zustand.
      await from.fill("abc");
      await to.fill("50x");
      for (const field of [from, to]) {
        const mode = await field.getAttribute("inputmode");
        if (mode !== "decimal") throw new Error(`Betragsfeld ohne Zifferntastatur: inputmode ${mode}`);
      }
      await sheet.getByText("Betrag nicht lesbar, etwa 12,50").nth(1).waitFor();
    },
  },
  // ledger-d9z3n: das offene Filter-Sheet unter 1024 px (Schalter mit Anzahl, Zeilen, Beträge, Fuß), unter dem
  // Betrag der Umschalter „Alle | Ausgaben | Einnahmen“ (ADR 0095), „Ausgaben“ gewählt.
  {
    name: "kassenbuch-filter-sheet",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    // Hinter dem Sheet steht der Chip „Ausgaben“; sein „×“ ist 24 px breit (ChipBar), Kann-Sammlung ledger
    // (ledger-k4n2s, Punkt 1), wie bei `kassenbuch-filter-gesetzt`.
    exceptions: [HANDY_WIDTH].map((width) => ({
      width,
      inv: 2,
      selector: '[data-testid="ledger-active-filters"] button "Ausgaben entfernen"',
    })),
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await openHandyFilterSheet(page);
      const direction = page.locator(".bottom-sheet.active").getByRole("group", { name: "Richtung" });
      await direction.getByRole("button", { name: "Ausgaben" }).click();
      await direction.locator('button[aria-pressed="true"]', { hasText: "Ausgaben" }).waitFor();
    },
  },
  // ledger-d9z3n: drei gesetzte Filter als Chips unter Suche und Filter-Knopf; sie brechen um und wischen nicht.
  {
    name: "kassenbuch-filter-gesetzt",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    // Das „×“ eines Chips ist 24 px breit (ChipBar), Kann-Sammlung ledger (ledger-k4n2s, Punkt 1).
    exceptions: [HANDY_WIDTH].flatMap((width) =>
      ["Zu prüfen", "Lena", "Ohne Kategorie"].map((name) => ({
        width,
        inv: 2,
        selector: `[data-testid="ledger-active-filters"] button "${name} entfernen"`,
      }))
    ),
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      await openHandyFilterSheet(page);
      for (const id of ["filter-needs-review", "filter-ask-lena", "filter-without-category"]) {
        await page.locator(`.bottom-sheet.active [data-testid="${id}"]`).click();
        await page.locator(`.bottom-sheet.active [data-testid="${id}"][aria-checked="true"]`).waitFor();
      }
      await page.locator(".bottom-sheet.active").getByRole("button", { name: /zeigen$/ }).click();
      await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
      await page.getByTestId("ledger-active-filters").getByRole("button", { name: "Ohne Kategorie entfernen" }).waitFor();
    },
  },
  // ledger-dn4vy: die ausgegraute Zeile („passt nicht mehr zum Filter“) am PC mit leerem Panel und am Handy. Vorher
  // `opacity-50`: alle Texte der Zeile fielen unter 4,5:1 (Invariante 10).
  {
    name: "kassenbuch-ausgegraut",
    route: "/ledger",
    widths: [PC_WIDTH],
    mock: mockLedger,
    open: (page) => openFilteredOut(page, { mobile: false }),
  },
  {
    name: "kassenbuch-ausgegraut-handy",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    // Mit aktivem Filter: das Chip-„×“ (24 px) ist zu klein. Kann-Sammlung ledger
    // (ledger-k4n2s, Punkt 1).
    exceptions: [{ width: HANDY_WIDTH, inv: 2, selector: '[data-testid="ledger-active-filters"] button "Kategorie: Lebensmittel entfernen"' }],
    mock: mockLedger,
    open: (page) => openFilteredOut(page, { mobile: true }),
  },
  // ledger-external-links: das Detail-Sheet mit Bestell-Link. ledger-zfj78: das Detail nach Prototyp Version 17 mit
  // aufgeklappter Historie (§7.11), am Handy als Sheet, bei 1440 px im Panel.
  {
    name: "kassenbuch-detail",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/getEntryHistory", (r) => json(r, DETAIL_HISTORY));
    },
    open: async (page, { mobile }) => {
      let scope;
      if (mobile) {
        scope = await openLedgerDetail(page, "AMAZON PAYMENTS");
      } else {
        // Bei PC_WIDTH steht neben dem Panel die Tabelle (design-system-gcpws).
        await openLedger(page);
        await page.locator("table tbody tr", { hasText: "AMAZON PAYMENTS" }).locator("visible=true").first().locator("td").nth(2).click();
        scope = page.locator('[data-testid="ledger-detail-panel"]');
        await scope.waitFor();
      }
      await scope.locator('[data-testid="detail-external-link"]').waitFor();
      await scope.getByRole("button", { name: "Historie (3)" }).click();
      await scope.locator('[data-testid="history-row"]').first().waitFor();
    },
  },
  // design-system-x045q: der Memo-Editor mit 500 Zeichen. Am Handy ersetzt er den Inhalt des Detail-Sheets (Titel
  // „Memo“, keine Payee-Zeile mehr), das Feld scrollt über seiner Höchsthöhe in sich und „Speichern“ bleibt bei offener
  // Tastatur im Bild; am PC steht er im Panel an der Stelle des Blocks.
  {
    name: "kassenbuch-detail-memo",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockLedger,
    open: async (page, { mobile }) => {
      let scope;
      if (mobile) {
        scope = await openLedgerDetail(page, "AMAZON PAYMENTS");
      } else {
        await openLedger(page);
        const tableRow = page.locator("table tbody tr", { hasText: "AMAZON PAYMENTS" }).locator("visible=true");
        if ((await tableRow.count()) > 0) await tableRow.first().locator("td").nth(2).click();
        else await page.getByText("AMAZON PAYMENTS", { exact: true }).locator("visible=true").first().click();
        scope = page.locator('[data-testid="ledger-detail-panel"]');
        await scope.waitFor();
      }
      await scope.locator('[data-testid="detail-memo-action"]').click();
      const field = scope.locator('[data-testid="detail-memo-editor"] textarea');
      await field.waitFor();
      await field.fill("M".repeat(20) + " " + "Lampe Kabel ".repeat(39) + "x".repeat(11));
      await scope.locator('[data-testid="detail-memo-counter"]', { hasText: "500 / 500" }).waitFor();
      await frames(page);
      if (mobile) {
        const title = (await page.locator(".bottom-sheet.active .sheet-header h3").first().textContent())?.trim();
        if (title !== "Memo") throw new Error(`Sheet-Titel ist „${title}“, nicht „Memo“`);
        if (await scope.locator('[data-testid="detail-payee"]').isVisible()) throw new Error("Detail steht noch neben dem Memo-Editor");
      }
    },
    primary: (page) => page.getByRole("button", { name: "Speichern", exact: true }).locator("visible=true").first(),
    keyboard: {
      focus: (page) => page.locator(".bottom-sheet.active textarea").first(),
      reach: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
    },
  },
  // ledger-split-editor, design-system-j6dnj: Aufteilen im Sheet, Speichern klebt unten, Zeile hinzufügen und
  // Barabhebung mit voller Trefferfläche.
  {
    name: "kassenbuch-aufteilen",
    route: "/ledger",
    widths: [HANDY_WIDTH],
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
    widths: [HANDY_WIDTH],
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
    widths: [HANDY_WIDTH],
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
    widths: [HANDY_WIDTH],
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
  // `aria-describedby` auf die Meldung. Geht die Tastatur danach auf, rollt die Meldung ins Bild.
  // ledger-enjex (R3 Platz 2): lehnt der Server das Speichern ab, steht der Grund rot im Sheet über dem Fuß, das Sheet
  // bleibt mit dem Entwurf offen, kein Toast, nichts in der Leiste.
  {
    name: "kassenbuch-betrag-feldfehler",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/setEntryAmount", (r) => json(r, { Error: "YNAB nicht erreichbar" }));
    },
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-amount-edit"]').tap();
      const field = page.locator('.bottom-sheet.active [data-testid="amount-input"] input');
      const save = page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true });
      await field.fill("45");
      await save.tap();
      const line = page.locator('.bottom-sheet.active [data-testid="feedback-sheet-line"][data-tone="error"]');
      await line.getByText("YNAB nicht erreichbar").waitFor();
      if ((await field.inputValue()) !== "45") throw new Error("der Entwurf ging verloren");
      const lineBox = await line.boundingBox();
      const saveBox = await save.boundingBox();
      if (lineBox.y + lineBox.height > saveBox.y) throw new Error("der Fehler steht nicht über dem Fuß");
      await expectNoToast(page, "Betrag nicht geändert");
      const inBar = await page.locator('[data-testid="feedback-bar"] [data-testid="feedback-action"]').filter({ visible: true }).count();
      if (inBar !== 0) throw new Error("Fehler im Sheet steht zusätzlich in der Leiste");
      await page.waitForFunction((el) => !el.matches(":active"), await save.elementHandle());
    },
    primary: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
    reach: (page) => page.locator('.bottom-sheet.active [data-testid="feedback-sheet-line"]'),
    keyboard: {
      focus: (page) => page.locator('.bottom-sheet.active [data-testid="amount-input"] input'),
      reach: (page) =>
        page
          .locator('.bottom-sheet.active [data-testid="feedback-sheet-line"]')
          .or(page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true })),
    },
  },
  // ledger-q42pg (bis ledger-enjex `kassenbuch-betrag-feldfehler`): Speichern mit leerem Betrag zeigt den Feldfehler
  // unter dem Feld.
  {
    name: "kassenbuch-betrag-eingabefehler",
    route: "/ledger",
    widths: [HANDY_WIDTH],
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
    widths: [PC_WIDTH],
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
  // ledger-gy77x: am PC der Split-Editor im Panel, der Fuß mit „Abbrechen“ ohne Füllung und „Speichern“ über die volle
  // Breite, ohne Scrollen erreichbar.
  {
    name: "kassenbuch-aufteilen-panel",
    route: "/ledger",
    widths: [PC_WIDTH],
    mock: mockLedger,
    open: async (page) => {
      const row = page.locator("table tbody tr", { hasText: "Bauhaus" }).first();
      await row.waitFor();
      await row.locator("td").nth(2).click();
      const panel = page.locator('[data-testid="ledger-detail-panel"]');
      await panel.locator('[data-testid="detail-split-action"]').click();
      await panel.locator('[data-testid="split-footer"]').waitFor();
    },
    primary: (page) => page.locator('[data-testid="split-footer"]').getByRole("button", { name: "Speichern", exact: true }),
  },
  // ledger-fxt09: die Payee-Auswahl am Handy ersetzt den Inhalt des Detail-Sheets (§7.15), mit einem langen Payee,
  // der in der Liste mit Ellipse abschneidet.
  {
    name: "kassenbuch-payee-auswahl",
    route: "/ledger",
    widths: [HANDY_WIDTH],
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
  // ledger-fxt09: am PC die Payee-Auswahl als Dialog über der Liste, das Detail-Panel bleibt rechts.
  {
    name: "kassenbuch-payee-auswahl-pc",
    route: "/ledger",
    widths: [PC_WIDTH],
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
  // ledger-pkrwd: das Detail einer Buchung ohne Payee. Die Payee-Zeile zeigt „ohne Payee“ gedämpft, ohne
  // „alle zeigen“, der Pfeil bleibt. Am Handy als Sheet, am PC im Panel.
  {
    name: "kassenbuch-detail-ohne-payee",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockLedger(page, { withoutPayee: true }),
    open: async (page) => {
      const width = page.viewportSize().width;
      if (width < 1024) {
        const sheet = await openLedgerDetail(page, "ohne Payee");
        await sheet.locator('[data-testid="detail-payee-arrow"]').waitFor();
      } else {
        const row = page.locator("table tbody tr", { hasText: "ohne Payee" }).first();
        await row.waitFor();
        await row.locator("td").nth(2).click();
        await page.locator('[data-testid="ledger-detail-panel"] [data-testid="detail-payee-arrow"]').waitFor();
      }
    },
  },
  // ledger-at63w: "Neue Buchung" am Handy als Sheet. "Buchung anlegen" klebt unten und bleibt auch bei
  // offener Tastatur im Betragsfeld erreichbar.
  {
    name: "kassenbuch-erfassen",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.getByTestId("ledger-capture-fab").waitFor();
      await page.getByTestId("ledger-capture-fab").tap();
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
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.getByTestId("ledger-capture-fab").waitFor();
      await page.getByTestId("ledger-capture-fab").tap();
      const sheet = page.locator(".bottom-sheet.active");
      await sheet.locator('[data-testid="capture-date"]').waitFor();
      // Beide Rechtecke im selben Frame: das Sheet fährt noch ein, getrennte Messungen lägen verschoben.
      const pos = await page.evaluate(() => {
        const sheet = document.querySelector(".bottom-sheet.active");
        const date = sheet?.querySelector('[data-testid="capture-date"]')?.getBoundingClientRect();
        // Das Memo über seine Beschriftung: seit ledger-zf6zg sagt auch das Payee-Feld „optional“.
        const memoLabel = [...(sheet?.querySelectorAll("label") ?? [])].find((l) => l.textContent.trim().startsWith("Memo"));
        const memo = memoLabel?.querySelector("input")?.getBoundingClientRect();
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
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.getByTestId("ledger-capture-fab").waitFor();
      await page.getByTestId("ledger-capture-fab").tap();
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
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.getByTestId("ledger-capture-fab").waitFor();
      await page.getByTestId("ledger-capture-fab").tap();
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
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.getByTestId("ledger-capture-fab").waitFor();
      await page.getByTestId("ledger-capture-fab").tap();
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
    widths: [HANDY_WIDTH],
    // Seit ledger-ea70s ersetzt die Konto-Auswahl den Inhalt des Sheets; der Schleier der zweiten Ebene, der die
    // Ausnahme für Invariante 5 brauchte, ist weg.
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
      await page.route("**/api/LedgerApi/getBudgetAccounts", () => {});
    },
    open: async (page) => {
      await page.getByTestId("ledger-capture-fab").waitFor();
      await page.getByTestId("ledger-capture-fab").tap();
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
    widths: [PC_WIDTH],
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
  },
  // ledger-ea70s: die Kategorie-Auswahl im Buchungs-Detail ersetzt am Handy den Inhalt des Sheets (§7.15).
  {
    name: "kassenbuch-detail-kategorie",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-category-arrow"]').tap();
      await page.locator(".bottom-sheet.active", { hasText: "Ziel wählen" }).locator('[role="option"]').first().waitFor();
      if ((await page.locator(".bottom-sheet.active").count()) !== 1)
        throw new Error("Die Kategorie-Auswahl im Detail liegt als zweites Sheet über dem Detail");
    },
  },
  // ledger-p5y9s (R3 Platz 1, Entscheidung Roman 2026-11-19): nach dem Kategoriewechsel steht „✓ Geändert“ mit
  // „Rückgängig“ unter „Kategorie“, im Sheet am Handy wie im Panel am PC, ohne Toast. Vorher
  // `kassenbuch-kategorie-toast`: am PC stand der Toast 5 s über „Historie“ (Ausnahme der Invariante 5, entfällt).
  // Regression c819eeb (Toast über dem Panel) prüft seit design-system-gkded `uebersicht-meldung` am PC.
  {
    name: "kassenbuch-kategorie-rueckgaengig",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockLedger,
    open: async (page, { mobile }) => {
      if (mobile) {
        const sheet = await openLedgerDetail(page, "Bauhaus");
        await sheet.locator('[data-testid="detail-category-arrow"]').tap();
        // ledger-qvjvs: der Picker im Detail bietet auch die Konten an und heißt dann "Ziel wählen".
        const picker = page.locator(".bottom-sheet.active", { hasText: "Ziel wählen" });
        await picker.waitFor();
        await picker.locator('[role="option"]', { hasText: "Nebenkosten" }).last().tap();
        await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 1);
      } else {
        await openLedger(page);
        await page.getByText("Bauhaus", { exact: true }).locator("visible=true").first().click();
        await page.locator('[data-testid="detail-category-arrow"]').filter({ visible: true }).first().click();
        await page.locator('[role="option"]', { hasText: "Nebenkosten" }).filter({ visible: true }).last().click();
      }
      await fieldUndo(page).waitFor();
      const note = page.locator('[data-testid="detail-note"][data-note-field="category"]').filter({ visible: true });
      if (!/Geändert/.test(await note.textContent())) throw new Error("Unter „Kategorie“ fehlt „Geändert“");
      await expectNoToast(page, "Kategorie geändert");
    },
    reach: (page) => fieldUndo(page),
  },
  // ledger-p5y9s: nach „Speichern“ im Split-Editor steht wieder das Detail, „✓ Aufgeteilt in 2 Zeilen“ mit
  // „Rückgängig“ unter den Split-Zeilen, ohne Toast (Abweichung vom Anhang der Skizze: das Sheet deckt die Leiste zu).
  {
    name: "kassenbuch-aufgeteilt-rueckgaengig",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/setEntrySplits", (r) =>
        json(r, { Ok: { Previous: null, Change: "CategoryChanged", WriteBack: "CategorySent" } })
      );
    },
    open: async (page, { mobile }) => {
      const scope = mobile
        ? await openLedgerDetail(page, "Bauhaus")
        : await (async () => {
            const row = page.locator("table tbody tr", { hasText: "Bauhaus" }).first();
            await row.waitFor();
            await row.locator("td").nth(2).click();
            return page.locator('[data-testid="ledger-detail-panel"]');
          })();
      const press = (locator) => (mobile ? locator.tap() : locator.click());
      await press(scope.locator('[data-testid="detail-split-action"]'));
      await scope.locator('[data-testid="split-summary"]').waitFor();
      // Bauhaus: 81,90 €, aufgeteilt in 10,00 € Lebensmittel und 71,90 € Nebenkosten.
      await scope.locator('[data-testid="split-amount-0"] input').fill("10");
      await press(scope.locator('[data-testid="split-target-1"]'));
      await press(page.locator('[role="option"]', { hasText: "Nebenkosten" }).filter({ visible: true }).last());
      // Der Betrag statt „Rest“: nach der Wahl im Picker schluckt `Viewport.swallowNextClick` 350 ms lang den Tipp.
      await page.locator('[data-testid="split-amount-1"] input').filter({ visible: true }).fill("71,90");
      // „Speichern“ wie `closeLedgerSheet` erneut, bis der Editor zu ist: `Viewport.swallowNextClick` schluckt den
      // Tipp, wenn er innerhalb von 350 ms nach der Wahl im Picker kommt.
      const summary = page.locator('[data-testid="split-summary"]').filter({ visible: true });
      for (let attempt = 0; attempt < 5 && (await summary.count()) > 0; attempt++) {
        const footer = mobile ? page.locator(".bottom-sheet.active").first() : page.locator('[data-testid="split-footer"]');
        await press(footer.getByRole("button", { name: "Speichern", exact: true }));
        await summary.first().waitFor({ state: "detached", timeout: 1000 }).catch(() => {});
      }
      const note = page.locator('[data-testid="detail-note"][data-note-field="splits"]').filter({ visible: true });
      await note.getByText("Aufgeteilt in 2 Zeilen", { exact: true }).waitFor();
      await fieldUndo(page).waitFor();
      await expectNoToast(page, "Aufgeteilt");
    },
    reach: (page) => fieldUndo(page),
  },
  // ledger-p5y9s (Styleguide-Regel `task-ui-vorgaben`): ein abgelehnter Kategoriewechsel steht rot unter „Kategorie“,
  // nicht als Toast und nicht in der Leiste.
  {
    name: "kassenbuch-kategorie-fehler-feld",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/setEntryCategory", (r) => json(r, { Error: "YNAB nicht erreichbar" }));
    },
    open: async (page, { mobile }) => {
      if (mobile) {
        const sheet = await openLedgerDetail(page, "Bauhaus");
        await sheet.locator('[data-testid="detail-category-arrow"]').tap();
        const picker = page.locator(".bottom-sheet.active", { hasText: "Ziel wählen" });
        await picker.waitFor();
        await picker.locator('[role="option"]', { hasText: "Nebenkosten" }).last().tap();
        await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 1);
      } else {
        await openLedger(page);
        await page.getByText("Bauhaus", { exact: true }).locator("visible=true").first().click();
        await page.locator('[data-testid="detail-category-arrow"]').filter({ visible: true }).first().click();
        await page.locator('[role="option"]', { hasText: "Nebenkosten" }).filter({ visible: true }).last().click();
      }
      const note = page.locator('[data-testid="detail-note"][data-note-field="category"] [data-tone="error"]').filter({ visible: true });
      await note.getByText("Nicht geändert: YNAB nicht erreichbar").waitFor();
      await expectNoToast(page, "Kategorie nicht geändert");
      const inBar = await page.locator('[data-testid="feedback-bar"] [data-testid="feedback-action"]').filter({ visible: true }).count();
      if (inBar !== 0) throw new Error("Fehler am Feld steht zusätzlich in der Leiste");
    },
    reach: (page) => page.locator('[data-testid="detail-note"] [data-tone="error"]').filter({ visible: true }).first(),
  },
  // ledger-enjex (R3 Platz 1, Entscheidung Roman 2): nach dem Speichern des Memos steht „✓ Gesetzt“ mit „Rückgängig“
  // unter dem Memo-Block, im Sheet am Handy wie im Panel am PC, ohne Toast.
  {
    name: "kassenbuch-memo-rueckgaengig",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/setEntryMemo", (r) =>
        json(r, { Ok: { PreviousMemo: null, CurrentMemo: "Schrauben und Dübel", MemoChange: "MemoChanged", MemoWriteBack: "MemoSent" } })
      );
    },
    open: async (page, { mobile }) => {
      const scope = await openDetailScope(page, mobile);
      await press(scope.locator('[data-testid="detail-memo-action"]'), mobile);
      const editor = page.locator('[data-testid="detail-memo-editor"]').filter({ visible: true });
      await editor.locator("textarea").fill("Schrauben und Dübel");
      await press(editor.getByRole("button", { name: "Speichern", exact: true }), mobile);
      const note = page.locator('[data-testid="detail-note"][data-note-field="memo"]').filter({ visible: true });
      await note.getByText("Gesetzt", { exact: true }).waitFor();
      await fieldUndo(page).waitFor();
      await expectNoToast(page, "Memo gesetzt");
    },
    reach: (page) => fieldUndo(page),
  },
  // ledger-enjex: das Sheet „Betrag“ (am PC der Dialog) schließt nach dem Speichern, unter dem Betrag oben im Detail
  // steht „✓ Geändert“ mit „Rückgängig“, ohne Toast.
  {
    name: "kassenbuch-betrag-rueckgaengig",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/setEntryAmount", (r) =>
        json(r, { Ok: { PreviousAmount: eur(-81.9), CurrentAmount: eur(-45), AmountChange: "AmountChanged", AmountWriteBack: "AmountSent" } })
      );
    },
    open: async (page, { mobile }) => {
      const scope = await openDetailScope(page, mobile);
      await press(scope.locator('[data-testid="detail-amount-edit"]'), mobile);
      const form = page.locator('[data-testid="amount-form"]').filter({ visible: true });
      await form.locator('[data-testid="amount-input"] input').fill("45");
      const footer = mobile ? page.locator(".bottom-sheet.active").first() : page.locator('[data-layer="dialog"]');
      await press(footer.getByRole("button", { name: "Speichern", exact: true }), mobile);
      const note = page.locator('[data-testid="detail-note"][data-note-field="amount"]').filter({ visible: true });
      await note.getByText("Geändert", { exact: true }).waitFor();
      await fieldUndo(page).waitFor();
      await expectNoToast(page, "Betrag geändert");
    },
    reach: (page) => fieldUndo(page),
  },
  // ledger-enjex (Entscheidung Roman 3, ADR 0098): nach „Lena fragen“ wechselt nur der Knopf und leuchtet kurz, kein
  // Text darunter, kein Toast. Er ist selbst das Rückgängig.
  {
    name: "kassenbuch-lena-gewechselt",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/setLenaFlag", (r) =>
        json(r, { Ok: { Previous: { Color: null, Name: null }, Change: "FlagChanged", WriteBack: "FlagSent" } })
      );
    },
    open: async (page, { mobile }) => {
      const scope = await openDetailScope(page, mobile);
      const knob = scope.locator('[data-testid="detail-flag-action"]');
      if ((await knob.textContent()).trim() !== "Lena fragen") throw new Error("vorher steht nicht „Lena fragen“");
      await press(knob, mobile);
      await page.waitForFunction(
        () =>
          [...document.querySelectorAll('[data-testid="detail-flag-action"]')].some(
            (el) => el.offsetParent !== null && el.textContent.trim() === "Rückfrage zurücknehmen"
          )
      );
      const notes = await page.locator('[data-testid="detail-note"]').filter({ visible: true }).count();
      if (notes !== 0) throw new Error(`${notes} Text unter dem Knopf, erwartet nur den Wechsel`);
      await expectNoToast(page, "Lena gefragt");
    },
    reach: (page) => page.locator('[data-testid="detail-flag-action"]').filter({ visible: true }).first(),
  },
  // ledger-enjex (Entscheidung Roman 1): hatte die Buchung ein fremdes Flag, steht unter dem Knopf „✓ Lena gefragt“ mit
  // „Rückgängig“, das das alte Flag wiederherstellt.
  {
    name: "kassenbuch-lena-fremdes-flag",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/setLenaFlag", (r) =>
        json(r, { Ok: { Previous: { Color: "red", Name: null }, Change: "FlagChanged", WriteBack: "FlagSent" } })
      );
    },
    open: async (page, { mobile }) => {
      const scope = await openDetailScope(page, mobile, "Markiert und zu prüfen");
      await press(scope.locator('[data-testid="detail-flag-action"]'), mobile);
      const note = page.locator('[data-testid="detail-note"][data-note-field="lena"]').filter({ visible: true });
      await note.getByText("Lena gefragt", { exact: true }).waitFor();
      await fieldUndo(page).waitFor();
      await expectNoToast(page, "Lena gefragt");
    },
    reach: (page) => fieldUndo(page),
  },
  // design-system-gkded (Regel R3, ADR 0098): nach einem Wisch steht die Meldung in der Leiste über der Navigation,
  // „+ Buchung“ wird rund daneben. Regression design-system-vf94m: der Toast mit Aktion stand unten über dem
  // schwebenden Knopf und verdeckte ihn (Invariante 12 und Gegenbeweis (c) in layout-invariants.test.mjs).
  // ledger-gwr9j: am Handy „✓ Geprüft“, darunter der Payee, rechts „Rückgängig“ (Skizze 22).
  {
    name: "kassenbuch-leiste-geprueft",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await mockReviewMark(page);
    },
    open: async (page) => {
      await openLedger(page);
      await swipeReviewed(page, "Bauhaus");
      await feedbackAction(page).waitFor();
      await page.locator('[data-testid="ledger-capture-fab"][data-shape="round"]').waitFor();
      const bar = page.getByTestId("feedback-bar");
      await bar.getByTestId("feedback-action").filter({ hasText: /^Geprüft$/ }).waitFor();
      await bar.getByTestId("feedback-detail").filter({ hasText: "Bauhaus" }).waitFor();
    },
  },
  // ledger-k68sd: nach „Buchung löschen“ steht an ihrem Platz die Zeile „„…“ gelöscht · Rückgängig“ (R3), am Handy und
  // am PC, ohne Leiste und ohne Toast. Das Nachladen nimmt die Buchung aus der Liste, die Zeile bleibt. Gelöscht wird die
  // Buchung mit dem langen Payee: der Name wird gekürzt, „gelöscht“ und „Rückgängig“ nie (Invariante 4).
  {
    name: "kassenbuch-geloescht-zeile",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      const hiddenPayees = new Set();
      await mockLedger(page, { hiddenPayees });
      await page.route("**/api/LedgerApi/deleteEntry", (r) => {
        hiddenPayees.add(LONG_PAYEE);
        return json(r, { Ok: null });
      });
    },
    open: async (page, { mobile }) => {
      await openLedger(page);
      const row = page.getByText(LONG_PAYEE, { exact: true }).locator("visible=true").first();
      await (mobile ? row.tap() : row.click());
      const remove = page.getByRole("button", { name: "Buchung löschen" }).locator("visible=true").first();
      await remove.scrollIntoViewIfNeeded();
      await (mobile ? remove.tap() : remove.click());
      await page.locator('[data-testid="ledger-deleted-row"]:visible [data-phase="removed"]').waitFor();
      await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
      // Gegenstück zur Leiste: die Zeile am Platz meldet, es gibt keine Meldung (R3).
      const bar = await page.locator('[data-testid="feedback-bar"] [data-testid="feedback-action"]').count();
      if (bar > 0) throw new Error("Nach dem Löschen steht eine Meldung in der Leiste, erwartet ist nur die Zeile am Platz");
      const toast = await page.locator(".toast-layer:visible").count();
      if (toast > 0) throw new Error("Nach dem Löschen steht ein Toast, erwartet ist nur die Zeile am Platz");
      await settle(page, { where: "Buchung gelöscht, Zeile am Platz" });
    },
  },
  // design-system-gkded, ledger-gwr9j: drei Wische nacheinander werden eine Meldung „✓ 3 geprüft“ ohne zweite Zeile mit
  // einem Rückgängig (Skizze 23).
  {
    name: "kassenbuch-leiste-3-geprueft",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await mockReviewMark(page);
    },
    open: async (page) => {
      await openLedger(page);
      await swipeReviewed(page, "Bauhaus");
      await page.locator('[data-testid="feedback-detail"]', { hasText: "Bauhaus" }).waitFor();
      await swipeReviewed(page, "Rückfrage und Prüfung");
      await page.locator('[data-testid="feedback-action"]', { hasText: "2 geprüft" }).waitFor();
      await swipeReviewed(page, "Markiert und zu prüfen");
      await page.locator('[data-testid="feedback-action"]', { hasText: "3 geprüft" }).waitFor();
      const bars = await page.locator('[data-testid="feedback-message"]').count();
      if (bars !== 1) throw new Error(`3 geprüft: ${bars} Meldungen statt einer`);
      if ((await page.locator('[data-testid="feedback-bar"] [data-testid="feedback-detail"]').count()) !== 0)
        throw new Error("3 geprüft: die Meldung hat eine zweite Zeile");
    },
  },
  // ledger-gwr9j (Skizze 26): scheitert der Spiegel-Lauf, steht „Spiegeln gescheitert“ rot mit Grund und „Erneut“ in
  // der Leiste.
  {
    name: "kassenbuch-leiste-spiegel-fehler",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await page.route("**/api/LedgerApi/mirrorYnabBudget", (r) => json(r, { Error: "YNAB antwortet nicht" }));
    },
    open: async (page) => {
      await openLedger(page);
      await page.getByRole("button", { name: "Jetzt spiegeln", exact: true }).filter({ visible: true }).first().tap();
      const bar = page.getByTestId("feedback-bar");
      await bar.getByTestId("feedback-action").filter({ hasText: "Spiegeln gescheitert" }).waitFor();
      await bar.getByTestId("feedback-detail").filter({ hasText: "YNAB antwortet nicht" }).waitFor();
      await bar.locator('[data-feedback-action="true"]', { hasText: "Erneut" }).waitFor();
    },
  },
  // ledger-gwr9j (Skizze 27): nach „Buchung anlegen“ steht „✓ Angelegt“, darunter „Payee · Betrag · Konto“ (gebucht
  // auf Bar, die Liste zeigt das Girokonto), rechts „Rückgängig“; „+ Buchung“ wird rund.
  {
    name: "kassenbuch-leiste-angelegt",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockApp(page);
      await mockCapture(page, { catchAll: false });
    },
    open: async (page) => {
      await page.getByTestId("ledger-capture-fab").waitFor();
      await page.getByTestId("ledger-capture-fab").tap();
      const sheet = page.locator(".bottom-sheet.active", { hasText: "Neue Buchung" });
      await sheet.getByRole("button", { name: "Bäckerei Lange · 8,60" }).tap();
      await sheet.getByRole("button", { name: "Buchung anlegen" }).tap();
      await page.getByTestId("feedback-bar").getByTestId("feedback-detail").filter({ hasText: "Bäckerei Lange · -8,60 € · Bar" }).waitFor();
      const bar = page.getByTestId("feedback-bar");
      await bar.getByTestId("feedback-action").filter({ hasText: /^Angelegt$/ }).waitFor();
      await bar.locator('[data-feedback-action="true"]', { hasText: "Rückgängig" }).waitFor();
      await page.locator('[data-testid="ledger-capture-fab"][data-shape="round"]').waitFor();
    },
  },
  // ledger-gwr9j (Entscheidung Roman): im Auswahl-Modus steht eine Meldung ohne Knopf direkt über der Auswahl-Leiste,
  // die Liste endet über beiden.
  {
    name: "kassenbuch-auswahl-meldung",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await mockReviewMark(page);
      await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => json(r, { SavedColor: null, SuggestedColor: null }));
    },
    open: async (page) => {
      await openLedger(page);
      await swipeReviewed(page, "Rückfrage und Prüfung");
      await feedbackAction(page).waitFor();
      await openMobileSelection(page);
      const bar = page.getByTestId("feedback-bar");
      await bar.getByTestId("feedback-action").filter({ hasText: /^Geprüft$/ }).waitFor();
      if ((await page.getByTestId("ledger-capture-fab").count()) !== 0) throw new Error("Auswahl-Modus: „+ Buchung“ in der Leiste");
    },
  },
  // ledger-hg5m5: nach der Kategorie-Änderung steht unter der Kategorie das Angebot „Regel anlegen?“; der Toast mit
  // „Rückgängig“ verdeckt es nicht und es verdeckt ihn nicht (§7.13).
  {
    name: "kassenbuch-regel-angebot",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: mockLedger,
    open: async (page) => {
      const sheet = await openLedgerDetail(page, "Bauhaus");
      await sheet.locator('[data-testid="detail-category-arrow"]').tap();
      const picker = page.locator(".bottom-sheet.active", { hasText: "Ziel wählen" });
      await picker.waitFor();
      await picker.locator('[role="option"]', { hasText: "Nebenkosten" }).last().tap();
      await fieldUndo(page).waitFor();
      await page.locator('.bottom-sheet.active [data-testid="rule-offer"]').waitFor();
    },
  },
  // ledger-hg5m5: „Regel anlegen“ im Detail ersetzt den Inhalt des Sheets (§7.15), „Regel anlegen“ klebt unten.
  {
    name: "kassenbuch-regel-formular",
    route: "/ledger",
    widths: [HANDY_WIDTH],
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
    widths: [PC_WIDTH],
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
    widths: [PC_WIDTH],
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
  // ledger-cnaz3: drei gewählte Buchungen am PC, das Panel zeigt die Zusammenfassung (Summe, Ausgaben, Einnahmen,
  // nach Kategorie, Sammelaktionen), die Leiste unten nur Anzahl, Summe und „Auswahl aufheben“.
  {
    name: "kassenbuch-auswahl-pc",
    route: "/ledger",
    widths: [PC_WIDTH],
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      // Die Kästchen der ersten drei Buchungen in der Tabelle.
      for (const payee of ["Bauhaus", "AMAZON PAYMENTS", "PayPal Shop"]) {
        await page.getByRole("checkbox", { name: `${payee} auswählen` }).locator("visible=true").first().click();
      }
      await page.getByTestId("selection-summary").filter({ visible: true }).waitFor();
    },
    primary: (page) => page.getByTestId("selection-summary").getByRole("button", { name: "Alle geprüft", exact: true }),
  },
  // ledger-skpcx: das Sortier-Sheet am Handy mit allen Optionen.
  {
    name: "kassenbuch-sortieren",
    route: "/ledger",
    widths: [HANDY_WIDTH],
    mock: mockLedger,
    open: async (page) => {
      await openLedger(page);
      // Seit ledger-d9z3n aus der Zeile „Sortieren“ im Filter-Sheet.
      await openHandyFilterRow(page, "filter-sort", "Neueste zuerst");
      await page.locator(".bottom-sheet.active button.category-item").first().waitFor();
    },
  },
  // ledger-qns4j (AC1, AC2): bei vielen Konten haftet die Konten-Spalte und scrollt in sich, späte Konten
  // werden nicht abgeschnitten.
  {
    name: "kassenbuch-viele-konten",
    route: "/ledger",
    widths: [PC_WIDTH],
    mock: (page) => mockLedger(page, { accounts: 30 }),
    open: openLedger,
  },
  // Mit Auswahl steht am PC die Auswahl-Leiste statt des Tastatur-Hinweises: das Ende der Konten-Spalte bleibt
  // über ihr sichtbar (Invariante 5, am Ende gescrollt).
  {
    name: "kassenbuch-viele-konten-auswahl",
    route: "/ledger",
    widths: [PC_WIDTH],
    mock: (page) => mockLedger(page, { accounts: 30 }),
    open: async (page) => {
      await openLedger(page);
      await page.getByRole("checkbox", { name: /^Bauhaus.* auswählen$/ }).locator("visible=true").first().click();
      await page.locator(".selection-bar").filter({ visible: true }).waitFor();
    },
  },
  // Regression ledger-filter-bar (feste Höhenschätzung): die Leiste bricht um, der Tabellenkopf haftet trotzdem
  // darunter. Bei PC_WIDTH ist die Leiste neben der Konten-Spalte und dem Panel rund 730 px breit und zweizeilig
  // (gemessen 88 px): mit zwei Kategorien stehen „Filter löschen“ und die Suche in der zweiten Zeile.
  {
    name: "kassenbuch-gefiltert",
    route: "/ledger",
    widths: [PC_WIDTH],
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
  // und nach dem Lauf haftet die Leiste, ohne den Tabellenkopf zu überdecken. Am Handy (ledger-gy77x) sind „Abrufen“
  // und „Jetzt spiegeln“ mit Spinner Symbolknöpfe im Kopf, 48 × 48 px (Invariante 2).
  {
    name: "kassenbuch-spiegel-laeuft",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      await mockRunningMirror(page);
    },
    // ledger-gwr9j (Skizze 24): am Handy steht der Lauf zusätzlich gedämpft in der Leiste.
    open: async (page) => {
      await page.getByText("Spiegel läuft. Die Liste bleibt so lange stehen.").waitFor();
      if (page.viewportSize().width < 1024)
        await page.getByTestId("feedback-bar").getByTestId("feedback-action").filter({ hasText: "Spiegeln läuft …" }).waitFor();
    },
  },
  {
    name: "kassenbuch-nach-spiegel",
    route: "/ledger",
    widths: [PC_WIDTH],
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
  // ledger-pfhdq: scheitert das Nachladen der Kontostände nach dem Spiegel-Lauf, bleiben die alten Beträge stehen,
  // darüber der Hinweis „Kontostände konnten nicht aktualisiert werden“ mit „Erneut laden“ (Variante A, Roman 2026-11-19).
  {
    name: "kassenbuch-kontostaende-veraltet",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockLedger(page);
      let balanceCalls = 0;
      await page.route("**/api/LedgerApi/getLedgerAccountBalances", (route) => {
        balanceCalls += 1;
        return balanceCalls === 1 ? route.fallback() : route.fulfill({ status: 500, body: "Fehler" });
      });
      page.sweepMirror = await mockRunningMirror(page);
    },
    open: async (page) => {
      await openLedger(page);
      await page.sweepMirror.finishMirror();
      await page.getByText("Kontostände konnten nicht aktualisiert werden").locator("visible=true").first().waitFor();
    },
  },
  // ledger-y54gr: die Auswahl-Leiste am Handy mit beiden Lena-Aktionen und dem Grund als Zeile. Am Ende der Liste
  // liegt die letzte Buchung über ihr (Invariante 5, am Ende gescrollt).
  {
    name: "kassenbuch-auswahl-handy",
    route: "/ledger",
    widths: [HANDY_WIDTH],
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
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockSettings,
    open: openSettings,
  },
  // banking-import-vwhfm (R3, ADR 0098): Lena-Farbe gespeichert, der Abschnitt ist zu, im Kopf der Karte steht
  // „✓ Gespeichert“ statt der Statusanzeige, kein Toast, auch am PC.
  {
    name: "einstellungen-gespeichert",
    route: "/settings",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockSettingsFeedback,
    open: async (page) => {
      await openSettings(page);
      await page.locator('[data-testid="settings-lena-card"]').getByRole("button", { name: "Bearbeiten" }).click();
      const form = page.locator('[data-testid="settings-lena-form"]');
      await form.locator("select").selectOption("purple");
      await form.getByRole("button", { name: "Einstellungen speichern" }).click();
      await page.locator('[data-testid="settings-lena-card"]').waitFor();
      const saved = page.locator('[data-testid="feedback-saved"]');
      await saved.getByText("Gespeichert", { exact: true }).waitFor();
      // Das „✓“ setzt der Baustein, nur fürs Auge (design-system-ytgnc).
      if ((await saved.locator('[aria-hidden="true"]').innerText()) !== "✓") throw new Error("✓ fehlt vor „Gespeichert“");
      await frames(page);
    },
  },
  // banking-import-vwhfm: das Quick-Add-Konto lehnt der Server ab, rot unter dem Feld „Nicht gespeichert: …“, das Feld
  // zeigt den gespeicherten Wert, die Karte bleibt offen.
  {
    name: "einstellungen-fehler",
    route: "/settings",
    widths: [HANDY_WIDTH, PC_WIDTH],
    // Die Hilfezeile von `Input.group` („Gesetzt. Leer lassen …“, `text-text-muted/70`) hat 3,23:1; sie gehört dem
    // DS und steht in der Kann-Sammlung banking-import.
    exceptions: [HANDY_WIDTH, PC_WIDTH].map((width) => ({ width, inv: 10, selector: 'p "Gesetzt. Leer lassen' })),
    mock: mockSettingsFeedback,
    open: async (page) => {
      await openSettings(page);
      await page.getByRole("button", { name: "Verbindung testen" }).first().click();
      await page.getByText("Verbunden, 1 Budget gefunden", { exact: true }).waitFor();
      await page.getByRole("button", { name: "Bearbeiten" }).first().click();
      const quickAdd = page.getByText("Quick-Add-Konto (z. B. Bar)", { exact: true }).locator("xpath=..").locator("select");
      await quickAdd.selectOption(guid(9102));
      const fault = page.getByText("Nicht gespeichert: Netzwerkfehler: YNAB antwortet nicht");
      await fault.waitFor();
      await frames(page);
      // Regression banking-import-vwhfm (Verifier, Iteration 1): `.select-field` ohne Layer schlug `border-error`, das
      // abgelehnte Feld blieb grau umrandet. Der Zustand gilt erst als hergestellt, wenn der Rand die Farbe des
      // Fehlertexts hat (nach dem Übergang der Randfarbe).
      const error = await fault.evaluate((el) => getComputedStyle(el).color);
      await quickAdd
        .evaluate((el, want) => new Promise((done, fail) => {
          const until = performance.now() + 2000;
          const check = () => {
            const got = getComputedStyle(el).borderTopColor;
            if (got === want) done();
            else if (performance.now() > until) fail(new Error(`Rand des abgelehnten Felds ${got}, Fehlerfarbe ${want}`));
            else requestAnimationFrame(check);
          };
          check();
        }), error);
    },
  },
  // categorization-cgq35, categorization-n3sxw: der Tab Regeln im Kassenbuch, Prioritätspfeile am Handy, lange Namen.
  {
    name: "regeln",
    route: "/ledger/rules",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockRules,
    open: openRules,
  },
  // categorization-n3sxw: am PC steht eine Regel rechts im Panel, der Picker hängt als Popover ganz im Bild.
  {
    name: "regeln-panel",
    route: "/ledger/rules",
    widths: [PC_WIDTH],
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
    widths: [HANDY_WIDTH],
    mock: mockRules,
    open: async (page) => {
      await openRule(page, "Regel eins");
      await settle(page, { where: "regeln-sheet-picker, Sheet offen" });
      await page.locator('[data-testid="rule-category"]').click();
      await page.locator('.bottom-sheet.active [role="option"]').first().waitFor();
    },
  },
  // categorization-t6j00 (R3, ADR 0098): nach „Regel löschen“ steht an ihrem Platz die Zeile „„Name“ gelöscht“ mit
  // „Rückgängig“, ohne Nummer, kein Toast. Gelöscht wird die Regel mit dem langen Namen: der Name wird gekürzt,
  // „gelöscht“ und „Rückgängig“ bleiben ganz. Am PC in der Tabelle.
  {
    name: "regeln-geloescht",
    route: "/ledger/rules",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockRules(page);
      await page.route("**/api/RulesApi/deleteRule", (r) => json(r, { Ok: null }));
    },
    open: async (page, { mobile }) => {
      await deleteLongRule(page, mobile);
      await page.locator('[data-testid="rule-deleted-row"]:visible [data-phase="removed"]').waitFor();
    },
  },
  // categorization-t6j00: scheitert „Rückgängig“, wird die Zeile rot mit Grund und „Erneut“; sie verdeckt nichts und
  // bricht nicht.
  {
    name: "regeln-geloescht-fehler",
    route: "/ledger/rules",
    widths: [HANDY_WIDTH],
    mock: async (page) => {
      await mockRules(page);
      await page.route("**/api/RulesApi/deleteRule", (r) => json(r, { Ok: null }));
      await page.route("**/api/RulesApi/restoreRule", (r) =>
        json(r, { Error: { DatabaseError: ["restore_rule", "Datenbank gesperrt, Wiederherstellen später erneut versuchen"] } })
      );
    },
    open: async (page, { mobile }) => {
      await deleteLongRule(page, mobile);
      const undo = page.locator('[data-testid="rule-deleted-row"]:visible [data-feedback-action]');
      await undo.waitFor();
      await (mobile ? undo.tap() : undo.click());
      await page.locator('[data-testid="rule-deleted-row"]:visible [data-phase="failed"]').waitFor();
    },
  },
  // design-system-k6kfw: die Übersicht als Einstieg, mit Leiste unten, Rail und Seitenleiste je nach Breite.
  {
    name: "uebersicht",
    route: "/",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockOverview,
    open: openOverview,
    // design-system-k6kfw, Verifier iteration 1: die Karte „Monat“ hat eine feste Höhe, beim Laden rutschen Konten
    // (Handy) und Neueste Buchungen (PC) nicht nach unten. Der Monat kommt mit „Im Minus“.
    deferred: {
      api: "**/api/BudgetApi/getBudgetMonth",
      body: { Ok: budgetMonth(CURRENT) },
      loaded: (page) => page.locator('[data-testid="overview-month"]'),
    },
    // „Zu tun“ steht unter 1024 px über dem Monat und wächst mit seinem Inhalt (§7.4). Bei 412 px bricht der Satz des
    // ersten Eintrags nicht um, die Konten bleiben stehen; die Ausnahmen für 320 und 390 px fielen mit diesen Breiten.
  },
  // ledger-s1weh: „Zu tun“ mit allen Arten von Einträgen, ein langer Payee in „Buchung nicht in YNAB“, der Zähler an der
  // Übersicht in Rail und Seitenleiste und „+ Buchung“ ruhig über der Leiste.
  {
    name: "uebersicht-zu-tun",
    route: "/",
    widths: [HANDY_WIDTH, PC_WIDTH],
    // Invariante 10 (ledger-dn4vy): der Leertext „Buchungen erscheinen hier …“ steht in `text-muted/70` (3,23:1).
    // Kann-Sammlung der Übersicht.
    exceptions: [{ width: PC_WIDTH, inv: 10, selector: '[data-testid="overview-page"] p "Buchungen erscheinen hier' }],
    mock: async (page) => {
      await mockOverview(page);
      await page.route("**/api/LedgerApi/getLedgerEntriesPage", (r) =>
        json(r, { Entries: [], TotalCount: 0, TotalAmount: eur(0), DeletedCount: 0, NeedsReviewCount: 12, AskLenaCount: 2, WithoutCategoryCount: 0 })
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
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.plain]),
    open: reviewOpen("review-card"),
    primary: reviewPrimary("Passt, weiter"),
    reach: reviewFoot,
  },
  // ledger-gy77x: die Karte einer gefragten Buchung. „Rückfrage zurücknehmen“ (Text aus `lenaActionFor`, statt des
  // gekürzten „Rückfrage zurück“) bricht in der halben Breite um und wird nicht abgeschnitten (Invarianten 3 und 4),
  // „Passt, weiter“ bleibt ohne Scrollen erreichbar.
  {
    name: "durchgehen-karte-lena",
    route: "/review",
    widths: [HANDY_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.lena]),
    open: async (page) => page.getByRole("button", { name: "Rückfrage zurücknehmen", exact: true }).first().waitFor(),
    primary: reviewPrimary("Passt, weiter"),
    reach: reviewFoot,
  },
  {
    name: "durchgehen-amazon",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.amazon]),
    open: async (page) => page.locator('[data-testid="review-suggestions"]').first().waitFor(),
    primary: reviewPrimary("Passt, weiter"),
    // ledger-s2cg3: die Seite passt ohne Scrollen, langer Inhalt scrollt in der Karte. Die Ausnahme aus ledger-gy77x
    // („Passt, weiter“ bei 320 px 8 px unter dem Rand, Kann-Sammlung ledger Punkt 150) ist gestrichen.
    reach: reviewFoot,
  },
  // ledger-s2cg3: ein niedriger Viewport (412 × 600). Die Karte scrollt innen, „Passt, weiter“ und die Zeile darunter
  // bleiben im Bild.
  {
    name: "durchgehen-amazon-niedrig",
    route: "/review",
    widths: [HANDY_WIDTH],
    height: 600,
    mock: (page) => mockReview(page, [REVIEW_QUEUE.amazon]),
    open: async (page) => page.locator('[data-testid="review-suggestions"]').first().waitFor(),
    primary: reviewPrimary("Passt, weiter"),
    reach: reviewFoot,
  },
  // ledger-wg42w: das Memo auf der Karte nur zum Lesen, „Memo“ über dem Text, so lang, wie Platz ist. Kurz und mittel
  // stehen ganz (keine feste Grenze von zwei Zeilen); lang wird nur am Handy gekürzt, mit `title`, und die Karte
  // scrollt dann nicht. Ein niedrigerer Viewport kürzt weiter (`ResizeObserver`). Am PC ist die Karte nicht in der
  // Höhe begrenzt: das Memo steht ganz.
  {
    name: "durchgehen-memo-kurz",
    route: "/review",
    widths: [HANDY_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.memoShort]),
    open: async (page) => {
      const memo = await reviewMemoText(page);
      if (memo.clamped) throw new Error("kurzes Memo ist gekürzt");
    },
    primary: reviewPrimary("Passt, weiter"),
    reach: reviewFoot,
  },
  {
    name: "durchgehen-memo-mittel",
    route: "/review",
    widths: [HANDY_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.memoMedium]),
    open: async (page) => {
      const memo = await reviewMemoText(page);
      if (memo.clamped) throw new Error("mittleres Memo ist gekürzt");
      if (memo.lines < 3) throw new Error(`mittleres Memo steht auf ${memo.lines} statt mindestens drei Zeilen`);
    },
    primary: reviewPrimary("Passt, weiter"),
    reach: reviewFoot,
  },
  {
    name: "durchgehen-memo-lang",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.memoLong]),
    open: async (page, { mobile }) => {
      const memo = await reviewMemoText(page);
      if (memo.squeezed.length > 0) throw new Error(`Zeilen der Karte gestaucht: ${memo.squeezed.join(", ")}`);
      if (!mobile) {
        if (memo.clamped) throw new Error("langes Memo ist am PC gekürzt");
        return;
      }
      // Reicht auch eine Zeile nicht, scrollt der Rest der Karte in sich (Regel aus ledger-s2cg3): Überlauf ist nur
      // bei einer Zeile erlaubt. Bei 844 px darf die Karte nicht überlaufen; bei drei Vierteln davon reicht mit dem
      // langen Payee auch eine Zeile nicht.
      const expectFitted = (m, where, { scrollAllowed = false } = {}) => {
        if (!m.clamped) throw new Error(`langes Memo ist nicht gekürzt (${where})`);
        if (!m.title) throw new Error(`gekürztes Memo trägt keinen title (${where})`);
        if (m.overflow && !(scrollAllowed && m.lines === 1)) throw new Error(`Karte läuft trotz Kürzung über (${where}, ${m.lines} Zeilen)`);
      };
      expectFitted(memo, "volle Höhe");
      const size = page.viewportSize();
      await page.setViewportSize({ width: size.width, height: Math.round(size.height * 0.75) });
      await frames(page);
      const low = await reviewMemoText(page);
      expectFitted(low, "Höhe −25 %", { scrollAllowed: true });
      if (low.lines >= memo.lines) throw new Error(`niedriger Viewport kürzt nicht weiter (${low.lines} statt weniger als ${memo.lines} Zeilen)`);
      await page.setViewportSize(size);
      await frames(page);
      expectFitted(await reviewMemoText(page), "Höhe zurück");
    },
    primary: reviewPrimary("Passt, weiter"),
    reach: reviewFoot,
  },
  // ledger-cyd1t: „Aufteilen“ unter dem Feld „Kategorie“. Gesperrt steht der Grund darunter, eine aufgeteilte Buchung
  // zeigt „Aufgeteilt“ mit Pfeil; der Link hat am Handy 48 px Trefferfläche, „Passt, weiter“ bleibt ohne Scrollen
  // erreichbar. Die freie Karte steht in `durchgehen-karte`, das Detail mit offenem Editor in `durchgehen-aufteilen-editor`.
  {
    name: "durchgehen-aufteilen-gesperrt",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.splitLocked]),
    open: reviewOpen("review-split-reason"),
    primary: reviewPrimary("Passt, weiter"),
    reach: reviewFoot,
  },
  {
    name: "durchgehen-aufgeteilt",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.split]),
    open: reviewOpen("review-split"),
    primary: reviewPrimary("Passt, weiter"),
    reach: reviewFoot,
  },
  // Nach dem Tipp auf „Aufteilen“: am Handy das Detail-Sheet mit dem Editor als Inhalt, am PC das Panel daneben.
  {
    name: "durchgehen-aufteilen-editor",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.open]),
    open: async (page) => {
      await page.locator('[data-testid="review-split"]').first().click();
      await page.locator('[data-testid="split-summary"]').filter({ visible: true }).first().waitFor();
    },
    primary: (page) => page.getByRole("button", { name: "Speichern", exact: true }).locator("visible=true").first(),
  },
  {
    name: "durchgehen-duplikat",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.held]),
    open: reviewOpen("review-duplicate-card"),
    primary: reviewPrimary("Ist doppelt, verwerfen"),
    reach: reviewFoot,
  },
  // ledger-pz5wv: nach „Passt“ steht die letzte Aktion unter „Passt, weiter“ (Karte mit langem Payee: die Aktion bleibt
  // ganz, der Payee endet mit „…“), „Rückgängig“ mit 48 px erreichbar; am PC dieselbe Zeile in der Spalte.
  {
    name: "durchgehen-letzte-aktion",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.plain, REVIEW_QUEUE.memoShort]),
    open: async (page) => {
      await reviewPrimary("Passt, weiter")(page).click();
      await page.locator('[data-testid="feedback-line"]').getByText("Passt", { exact: true }).waitFor();
      await page.getByText("Stadtwerke").first().waitFor();
    },
    primary: reviewPrimary("Passt, weiter"),
    reach: (page) => page.locator('[data-testid="review-foot"]').getByRole("button", { name: "Rückgängig" }),
  },
  // ledger-mf2bg: Kategorie aus „Vorgeschlagen“ auf der Karte. Das Feld zeigt den Wert, darunter „Bahn · <Betrag>
  // verfügbar“ mit „Rückgängig“ am Feld; kein Toast und keine Meldung in der Leiste, am Handy wie am PC.
  {
    name: "durchgehen-kategorie-feld",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockReview(page, [REVIEW_QUEUE.amazon]);
      // „Bahn“ mit Verfügbarem (im gemeinsamen Fixture ohne), damit die Zeile am Feld den Betrag zeigt.
      await page.route("**/api/YnabApi/getCategories", (r) =>
        json(r, {
          Ok: buildCategoriesFixture().map((c) => (c.Id.YnabCategoryId === CATEGORY_IDS.bahn ? { ...c, Available: eur(105.7) } : c)),
        })
      );
    },
    open: async (page) => {
      const chips = page.locator('[data-testid="review-suggestions"]').first();
      await chips.waitFor();
      await chips.getByRole("button", { name: "Bahn", exact: true }).click();
      const note = page.locator('[data-testid="review-field-note"]').filter({ visible: true }).first();
      await note.waitFor();
      const text = await note.textContent();
      if (!/^Bahn · .+ verfügbar/.test(text)) throw new Error(`Unter „Kategorie“ steht „${text}“ statt „Bahn · … verfügbar“`);
      await expectNoToast(page, "Kategorie auf der Karte");
      const bars = await page.locator('[data-testid="feedback-bar"]').filter({ visible: true }).allInnerTexts();
      if (bars.some((t) => /Kategorie|Bahn/.test(t))) throw new Error(`Meldung in der Leiste: ${bars.join(" | ")}`);
    },
    primary: reviewPrimary("Passt, weiter"),
    reach: (page) => page.locator('[data-testid="review-field-note"]').getByRole("button", { name: "Rückgängig" }),
  },
  // ledger-pz5wv: scheitert „Passt“, bleibt die Karte und die Zeile zeigt rot den Grund mit „Erneut“.
  {
    name: "durchgehen-fehler",
    route: "/review",
    widths: [HANDY_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.memoShort], { failReviewMark: "YNAB antwortet nicht (503)" }),
    open: async (page) => {
      await reviewPrimary("Passt, weiter")(page).click();
      await page.locator('[data-testid="feedback-line"]').getByText("Nicht geprüft").waitFor();
    },
    primary: reviewPrimary("Passt, weiter"),
    reach: (page) => page.locator('[data-testid="review-foot"]').getByRole("button", { name: "Erneut" }),
  },
  {
    name: "durchgehen-ende",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, []),
    open: reviewOpen("review-end"),
    primary: reviewPrimary("Zur Übersicht"),
  },
  // ledger-hg5m5: „Regel anlegen“ auf der Karte öffnet das Formular am PC als Dialog, am Handy als Sheet.
  {
    name: "durchgehen-regel",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.plain]),
    open: async (page) => {
      // Erst wenn die Vorschläge stehen, steht die Zeile still; sonst rutscht sie unter dem Klick weg.
      await page.locator('[data-testid="review-suggestions"]').first().waitFor();
      await page.locator('[data-testid="review-rule"]').first().click();
      await page.locator('[data-testid="rule-form"]').waitFor();
    },
    primary: (page) => page.getByRole("button", { name: "Regel anlegen", exact: true }).locator("visible=true").last(),
  },
  // ledger-hme9k Befund 1: im Durchgehen mit offenen „Details“ legte ein Tipp auf den Payee eine zweite Ebene über das
  // Detail (am Handy ein zweites Sheet, am PC einen zweiten Dialog). Invariante 11 meldet sie. Soll: am Handy ersetzt
  // die Payee-Auswahl den Inhalt des Detail-Sheets, am PC öffnet Payee genau einen Dialog. Das Memo öffnet seit
  // ledger-wg42w nur in „Details“ (`durchgehen-details-memo`), auf der Karte steht es nur zum Lesen.
  {
    name: "durchgehen-details-payee",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.plain]),
    open: async (page) => {
      await openReviewDetails(page);
      await page.locator('[data-testid="detail-payee-arrow"]').filter({ visible: true }).first().click();
      await page.locator('[data-testid="payee-picker"]').filter({ visible: true }).first().waitFor();
    },
  },
  // design-system-x045q: der Memo-Editor ist mehrzeilig und zeigt den ganzen Text; am Handy ersetzt er den Inhalt des
  // Detail-Sheets, „Speichern“ bleibt bei offener Tastatur im Bild (übernimmt die Prüfung von `durchgehen-memo`).
  {
    name: "durchgehen-details-memo",
    route: "/review",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.memo]),
    open: async (page) => {
      await openReviewDetails(page);
      await page.locator('[data-testid="detail-memo-action"]').filter({ visible: true }).first().click();
      const field = page.locator('[data-testid="detail-memo-editor"] textarea').filter({ visible: true }).first();
      await field.waitFor();
      await frames(page);
      const clipped = await field.evaluate((el) => el.scrollHeight > el.clientHeight + 1);
      if (clipped) throw new Error("Memo-Feld zeigt nicht den ganzen Text");
    },
    keyboard: {
      focus: (page) => page.locator(".bottom-sheet.active textarea").first(),
      reach: (page) => page.locator(".bottom-sheet.active").first().getByRole("button", { name: "Speichern", exact: true }),
    },
  },
  // Am PC steht der Payee der Karte neben dem Panel weiter zum Antippen: auch von dort keine zweite Ebene.
  {
    name: "durchgehen-details-payee-karte",
    route: "/review",
    widths: [PC_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.plain]),
    open: async (page) => {
      await openReviewDetails(page);
      await page.locator('[data-testid="review-payee"]').first().click();
      await page.locator('[data-testid="payee-picker"]').filter({ visible: true }).first().waitFor();
    },
  },
  // ledger-hme9k Befund 2: „Regel anlegen“ aus dem Detail im Durchgehen ersetzt am Handy den Inhalt des Detail-Sheets.
  {
    name: "durchgehen-details-regel",
    route: "/review",
    widths: [HANDY_WIDTH],
    mock: (page) => mockReview(page, [REVIEW_QUEUE.plain]),
    open: async (page) => {
      await openReviewDetails(page);
      await page.locator('.bottom-sheet.active [data-testid="detail-rule"]').click();
      await page.locator('.bottom-sheet.active [data-testid="rule-form"]').waitFor();
    },
    primary: (page) => page.locator(".bottom-sheet.active").getByRole("button", { name: "Regel anlegen", exact: true }),
  },
  // ledger-s1weh: „Neue Buchung“ auf der Übersicht, am Handy als Sheet über „+ Buchung“, ab 1024 px als Dialog in der
  // Mitte über „Buchung“ im Kopf. „Buchung anlegen“ ist ohne Scrollen erreichbar, am Handy auch bei offener Tastatur.
  {
    name: "uebersicht-erfassen",
    route: "/",
    widths: [HANDY_WIDTH, PC_WIDTH],
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
    widths: [HANDY_WIDTH, PC_WIDTH],
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
    // Bei 20 px Grundschrift bricht bei HANDY_WIDTH der Satz des ersten Eintrags in „Zu tun“ auf zwei Zeilen um
    // (gemessen: „-12,50 € · Geld aus einer anderen Kategorie holen“), „Zu tun“ wächst mit seinem Inhalt (§7.4) und
    // die Konten rutschen 20 px nach unten. Die Leer-Karte „Angepinnt“ (budgeting-w9yt4) rutscht aus demselben Grund mit.
    exceptions: [
      { width: HANDY_WIDTH, inv: 9, selector: '[data-testid="overview-account"]' },
      { width: HANDY_WIDTH, inv: 9, selector: '[data-testid="overview-pins-empty"]' },
    ],
  },
  // Gegenstück zu `uebersicht`: der Monat ohne Kategorie im Minus. Die Karte ist gleich hoch mit oder ohne „Im Minus“.
  {
    name: "uebersicht-ohne-minus",
    route: "/",
    widths: [HANDY_WIDTH, PC_WIDTH],
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
      { width: HANDY_WIDTH, inv: 9, selector: '[data-testid="overview-account"]' },
      // Die Leer-Karte „Angepinnt“ (budgeting-w9yt4) steht über beiden und rutscht aus demselben Grund mit.
      { width: HANDY_WIDTH, inv: 9, selector: '[data-testid="overview-pins-empty"]' },
    ],
  },
  // budgeting-w9yt4: drei angepinnte Kategorien, eine im Minus mit „fehlt“, eine mit langem Namen. Der Platzhalter hat
  // je Pin eine Zeile in Zielform, beim Laden des Monats springt darunter nichts.
  {
    name: "uebersicht-angepinnt",
    route: "/",
    widths: [HANDY_WIDTH, PC_WIDTH],
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
    // Wie `uebersicht`: bei HANDY_WIDTH bricht der Satz des ersten Eintrags in „Zu tun“ nicht um, nichts rutscht.
  },
  // design-system-gkded: auf der Übersicht steht eine Meldung ohne Ort in der Leiste, „+ Buchung“ rund und ruhig daneben
  // (§7.14, „Zu tun“ hat Einträge). Am PC ist der Dialog „Angepinnt“ offen und der Toast liegt über ihm (Regression
  // c819eeb am PC: der Toast lag hinter der offenen Ebene, Gegenbeweis in layout-invariants.test.mjs). Seit budgeting-p3sk0
  // steht ein Speicherfehler der Pins am Ort; die Meldung kommt deshalb vom Abbrechen eines Abrufs, das scheitert
  // (`cancelSync` ohne Mock, am PC mit Verzögerung, damit der Dialog vorher offen ist).
  {
    name: "uebersicht-meldung",
    route: "/",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockFetch({ TanRequired: syncSession("AwaitingTan") })(page);
      await page.route("**/api/BudgetApi/getPinnedCategories", (r) => json(r, { Ok: [guid(1), guid(2), guid(3)] }));
      await page.route("**/api/SyncApi/cancelSync", async (r) => {
        await new Promise((resolve) => setTimeout(resolve, 1500));
        await r.fulfill({ status: 500, contentType: "application/json", body: '{"error":"nicht erreichbar"}' });
      });
    },
    open: async (page, { mobile }) => {
      await openFetch("fetch-tan")(page);
      await page.locator('[data-testid="overview-pins-edit"]').waitFor();
      if (!mobile) {
        await fetchLayer(page).getByRole("button", { name: "Abbrechen" }).click();
        await page.locator('[data-layer="dialog"]').waitFor({ state: "detached" });
        await page.locator('[data-testid="overview-pins-edit"]').click();
        await page.locator('[data-testid="pin-sheet-row"]').first().waitFor();
        await page.locator(".toast-layer").getByRole("button", { name: "Schließen" }).waitFor();
        return;
      }
      await fetchLayer(page).getByRole("button", { name: "Abbrechen" }).tap();
      await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
      await page.locator('[data-testid="feedback-bar"] [data-testid="feedback-message"]').waitFor();
      await page.locator('[data-testid="overview-capture-fab"][data-shape="round"]').waitFor();
    },
  },
  // budgeting-p3sk0 (R3, ADR 0098): das Speichern der Pins scheitert, nachdem das Sheet „Angepinnt“ zu ist. Der Satz
  // „Nicht gespeichert: …“ steht rot im Bereich „Angepinnt“, keine Meldung in der Leiste und kein Toast. Die Antwort kommt
  // verzögert, damit „Fertig“ vorher schließt (bei offenem Sheet stünde der Satz im Sheet).
  {
    name: "uebersicht-angepinnt-fehler",
    route: "/",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: async (page) => {
      await mockOverview(page);
      await page.route("**/api/BudgetApi/getPinnedCategories", (r) => json(r, { Ok: [guid(1), guid(2), guid(3)] }));
      await page.route("**/api/BudgetApi/setPinnedCategories", async (r) => {
        await new Promise((resolve) => setTimeout(resolve, 1000));
        await json(r, { Error: "YNAB antwortet nicht" });
      });
    },
    open: async (page) => {
      await page.locator('[data-testid="overview-pins-edit"]').waitFor();
      await page.locator('[data-testid="overview-pins-edit"]').click();
      await page.locator('[data-testid="pin-sheet-row"]').first().click();
      await page.locator('[data-layer="dialog"]').or(page.locator(".bottom-sheet.active").first()).getByRole("button", { name: "Fertig", exact: true }).click();
      await page.locator('[data-testid="overview-pins-not-saved"]').waitFor();
      await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
    },
  },
  // budgeting-w9yt4: das Sheet „Angepinnt“, am Handy als Sheet, ab 1024 px als Dialog; „Fertig“ ohne Scrollen erreichbar.
  {
    name: "uebersicht-angepinnt-sheet",
    route: "/",
    widths: [HANDY_WIDTH, PC_WIDTH],
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
    widths: [HANDY_WIDTH, PC_WIDTH],
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
      { width: HANDY_WIDTH, inv: 9, selector: '[data-testid="overview-account"]' },
      // Die Leer-Karte „Angepinnt“ (budgeting-w9yt4) steht über beiden und rutscht aus demselben Grund mit.
      { width: HANDY_WIDTH, inv: 9, selector: '[data-testid="overview-pins-empty"]' },
    ],
  },
  // banking-import-d0fey: das Sheet „Abrufen“ (Prototyp Version 17, Sheet `import`) am Handy als Sheet, am PC als
  // Dialog über der Übersicht. „Ich habe bestätigt“ und „Jetzt durchgehen“ sind ohne Scrollen erreichbar.
  {
    name: "abrufen-tan",
    route: "/",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockFetch({ TanRequired: syncSession("AwaitingTan") }),
    open: openFetch("fetch-tan"),
    primary: (page) => fetchLayer(page).getByRole("button", { name: "Ich habe bestätigt" }),
  },
  {
    name: "abrufen-fertig",
    route: "/",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: mockFetch({ Fetched: syncSession("Completed", FETCH_RESULT) }),
    open: openFetch("fetch-done"),
    primary: (page) => fetchLayer(page).getByRole("button", { name: "Jetzt durchgehen" }),
  },
  // banking-import-ns4re (R3): Roman schließt das Sheet, während der Abruf läuft. Das Ergebnis steht danach am Handy
  // in der Leiste („✓ Abgerufen“ / „6 neue Buchungen“, kein Knopf rechts), am PC als Toast unten rechts. `startSync`
  // antwortet erst, wenn das Sheet zu ist.
  (() => {
    // Je Seite ein eigenes Tor: der Sweep misst die Breiten parallel.
    const releases = new WeakMap();
    return {
      name: "abrufen-fertig-leiste",
      route: "/",
      widths: [HANDY_WIDTH, PC_WIDTH],
      mock: async (page) => {
        await mockOverview(page);
        const answered = new Promise((resolve) => releases.set(page, resolve));
        await page.route("**/api/SyncApi/startSync", async (r) => {
          await answered;
          await json(r, { Ok: { Fetched: syncSession("Completed", FETCH_RESULT) } });
        });
      },
      open: async (page, { width }) => {
        const sheet = width < 1024;
        await openFetch("fetch-running")(page);
        await fetchLayer(page).getByRole("button", { name: sheet ? "Schließen" : "Close modal" }).first().click();
        if (sheet) await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
        else await page.locator('[data-layer="dialog"]').waitFor({ state: "detached" });
        releases.get(page)();
        if (sheet) {
          await page.locator('[data-testid="feedback-bar"] [data-testid="feedback-message"]', { hasText: "Abgerufen" }).waitFor();
        } else {
          await page.locator(".toast-layer", { hasText: "Abgerufen" }).getByRole("button", { name: "Schließen" }).waitFor();
        }
      },
    };
  })(),
  // banking-import-d0fey: eine wartende Buchung trägt „doppelt?“ statt „Nicht in YNAB“ (Zeile am Handy, Statusspalte
  // am PC), mit langem Payee und großem Betrag.
  {
    name: "kassenbuch-wartend",
    route: "/ledger",
    widths: [HANDY_WIDTH, PC_WIDTH],
    mock: (page) => mockLedger(page, { held: true }),
    open: async (page) => page.getByText(HELD_PAYEE, { exact: true }).locator("visible=true").first().waitFor(),
  },
  // design-system-ytgnc (ADR 0098): der Abschnitt „Rückmeldung“ auf /styleguide mit den echten Bausteinen aus
  // `Feedback`. Die Aktion einer Meldung bricht um (lange Aktion), der Name steht auf einer Zeile mit „…“ und
  // `title`, bei Fehler und Warnung bricht der Grund um; Knöpfe 48 px, nichts überdeckt sich. Die übrigen Abschnitte
  // der Galerie werden ausgeblendet, gemessen wird nur dieser.
  {
    name: "styleguide-rueckmeldung",
    route: "/styleguide",
    widths: [HANDY_WIDTH],
    mock: mockApp,
    open: async (page) => {
      await page.getByTestId("styleguide-feedback").waitFor();
      await page.evaluate(() => {
        let node = document.querySelector('[data-testid="styleguide-feedback"]').closest("section");
        for (let i = 0; i < 2; i++) {
          for (const sibling of node.parentElement.children) if (sibling !== node) sibling.style.display = "none";
          node = node.parentElement;
        }
      });
    },
    // Der Hinweistext der Galerie (`note` in Components/Styleguide/View.fs, `text-text-muted/80`) hat 3,99:1, in
    // jedem Abschnitt der Galerie, nicht nur in diesem. Kann-Sammlung design-system.
    exceptions: [HANDY_WIDTH].map((width) => ({ width, inv: 10, selector: 'p "Am Handy (unter 1024 px) kein Toast.' })),
  },
];

export const stateNamed = (name) => {
  const found = STATES.find((s) => s.name === name);
  if (!found) throw new Error(`kein Zustand ${name}`);
  return found;
};
