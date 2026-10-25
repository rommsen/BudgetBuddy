#!/usr/bin/env node
// scripts/ledger-status-column.mjs
//
// Fixture-Bausteine für `scripts/ledger-status-column.test.mjs` (ledger-qns4j,
// Iteration 2 des Verifier-Rückläufers): rein synthetische, erfundene
// Fable.Remoting-Antworten für die Kassenbuch-API (`ILedgerApi`/`LedgerApi`), damit
// der Test die echte, per Vite gebaute Client-App (echtes Tailwind-CSS, echtes
// Fable-Bundle) gegen genau die Randfälle rendern kann, die der Verifier von
// Iteration 1 vermisst hat, ohne jemals ein echtes Backend oder die
// Entwicklungsdatenbank anzufassen (weder eine Kopie noch das Original).
//
// Der Grund für "erfunden, nicht aus der echten DB kopiert": ein Blick in echte
// Kontostände/Buchungen wäre ein Blick in Romans echte Finanzdaten. Die exakte
// Wire-Form dieser Fixtures (Fable.JsonConverter-Konventionen: Discriminated Unions
// ohne Felder als reiner String, `decimal` als JSON-Zahl, `option` als `null`/Wert
// direkt, ISO-Datum im "o"-Format) wurde einmalig gegen den echten Server geprüft,
// aber ausschließlich mit selbst erfundenen Werten (Konto "Testkonto Fake", Payees
// wie "Fake Payee Eins") auf einer frisch angelegten, leeren SQLite-Datei in einem
// Temp-Verzeichnis, nie gegen `~/my_apps/budgetbuddy-dev`. Diese Datei selbst startet
// keinen Server mehr; sie liefert nur noch die vorab geprüften JSON-Formen.

import { randomUUID } from "node:crypto";

/// Eine feste, wiederholbare GUID je laufender Nummer, lesbarer als `randomUUID()`
/// bei jedem Lauf, und stabil genug, um sie in Testerwartungen wiederzuerkennen.
export function fixtureGuid(n) {
  const hex = n.toString(16).padStart(4, "0");
  return `00000000-0000-4000-8000-${hex}00000000`.slice(0, 36);
}

/// `LedgerAccountsView` (Shared/Domain.fs): `count` frei erfundene Konten, das erste
/// ist die Vorauswahl (`DefaultAccountId`, wie es der echte Server für das
/// YNAB-Standardkonto oder sonst das erste Konto liefern würde). Für AC2 braucht es
/// mehr Konten, als in die Höhe eines 1440x900-Viewports passen (fünf reichen dafür
/// laut Task-Notes nicht), Vorgabe hier: 40.
export function buildAccountsFixture(count = 40) {
  const accounts = Array.from({ length: count }, (_, i) => ({
    Id: fixtureGuid(i + 1),
    Name: `Testkonto ${i + 1}`,
    Kind: "LedgerBankAccount",
    YnabAccountId: null,
    Closed: false,
  }));
  return { Accounts: accounts, DefaultAccountId: accounts[0].Id };
}

/// `LedgerAccountBalanceView list`: ein kleiner, frei erfundener Betrag je Konto,
/// nur damit die Kontostände-Leiste nicht überall "0,00 €" zeigt.
export function buildBalancesFixture(accounts) {
  return accounts.map((a, i) => ({
    AccountId: a.Id,
    Balance: { Amount: 100 + i, Currency: "EUR" },
  }));
}

/// Eine einzelne `LedgerEntryView` (Shared/Domain.fs) mit frei erfundenen Werten.
/// `overrides` setzt nur die für den jeweiligen Testfall wichtigen Felder.
export function buildEntryFixture(accountId, n, overrides = {}) {
  return {
    Id: fixtureGuid(1000 + n),
    AccountId: accountId,
    Date: `2026-03-${String((n % 27) + 1).padStart(2, "0")}T00:00:00.0000000`,
    Amount: { Amount: -(10 + n), Currency: "EUR" },
    Payee: `Testfixtur ${n}`,
    Memo: null,
    CategoryName: "Testkategorie",
    SplitCategoryGroupNames: [],
    Splits: [],
    Origin: "MirroredFromYnab",
    DeletedInYnab: false,
    NeedsReview: false,
    Flag: "NoFlag",
    IsTransfer: false,
    ReviewMarkWriteBackFailure: null,
    LinkedToYnab: true,
    PushToYnabFailure: null,
    ...overrides,
  };
}

/// Die sieben Zeilen, die die im Verifier-Rückläufer genannten Randfälle abdecken
/// (ledger-qns4j, Iteration 3, Zeile 7 neu gegenüber Iteration 2):
///  1. eine ganz gewöhnliche Zeile (Grundlinie: keine Status-Kennzeichen, kein
///     Herkunfts-Text in der Statusspalte, AC3).
///  2. nur "Zu prüfen" (AC4, `NeedsReview = true`).
///  3. "Zu prüfen" + Flag "Rückfrage Lena" nebeneinander (AC5: die eigentlich
///     überlaufgefährdete Kombination, die der Verifier von Iteration 1
///     bemängelt hat).
///  4. "Zu prüfen" + "in YNAB gelöscht" nebeneinander (AC5, die von Iteration 1
///     schon geprüfte Kombination, als Stand-in für "nicht in YNAB verknüpft":
///     `LedgerEntryView` hat dafür noch kein eigenes Feld, siehe OUTCOME).
///  5. nur "in YNAB gelöscht" (Ausnahme-Kennzeichen allein).
///  6. Flag "Markiert" allein (die andere Flag-Variante, ohne "Zu prüfen").
///  7. "Zu prüfen" + Flag "Markiert" nebeneinander (AC5, Iteration 3: genau die
///     Kombination, die der Verifier von Iteration 2 als noch überlaufend
///     nachgerechnet hat, scrollWidth 131 > clientWidth 128).
///
/// Dazu `fillerCount` ganz gewöhnliche weitere Zeilen (Testfixtur 8, 9, ...): allein
/// die sieben Fälle oben ergäben eine sehr kurze Seite, auf der die Seite selbst gar
/// nicht weit genug scrollt, um das Sticky-Verhalten der Konten-Spalte (AC1)
/// überhaupt zu prüfen, das Auffüllen macht die PC-Tabelle (und damit die Seite)
/// zuverlässig höher als der 1440x900-Viewport.
// Eigene, garantiert eindeutige Payee-Namen für die sieben geprüften Fälle
// (statt "Testfixtur N"): mit bis zu 40 zusätzlichen Füll-Zeilen ("Blindgänger N",
// andere Wortwahl mit Absicht) wäre "Testfixtur 1" sonst ein Substring von
// "Testfixtur 10"..."Testfixtur 19" und ein `hasText`-Locator im Test träfe die
// falsche Zeile.
export const SIX_CASE_PAYEES = {
  baseline: "Fixture Baseline",
  needsReview: "Fixture NeedsReview",
  needsReviewAskLena: "Fixture NeedsReview AskLena",
  needsReviewDeleted: "Fixture NeedsReview Deleted",
  deletedOnly: "Fixture DeletedOnly",
  markedOnly: "Fixture MarkedOnly",
  needsReviewMarked: "Fixture NeedsReview Marked",
};

export function buildEntriesFixture(accountId, fillerCount = 40) {
  const entries = [
    buildEntryFixture(accountId, 1, { Payee: SIX_CASE_PAYEES.baseline }),
    buildEntryFixture(accountId, 2, { Payee: SIX_CASE_PAYEES.needsReview, NeedsReview: true }),
    buildEntryFixture(accountId, 3, { Payee: SIX_CASE_PAYEES.needsReviewAskLena, NeedsReview: true, Flag: "AskLena" }),
    buildEntryFixture(accountId, 4, { Payee: SIX_CASE_PAYEES.needsReviewDeleted, NeedsReview: true, DeletedInYnab: true }),
    buildEntryFixture(accountId, 5, { Payee: SIX_CASE_PAYEES.deletedOnly, DeletedInYnab: true }),
    buildEntryFixture(accountId, 6, { Payee: SIX_CASE_PAYEES.markedOnly, Flag: "Marked" }),
    buildEntryFixture(accountId, 7, { Payee: SIX_CASE_PAYEES.needsReviewMarked, NeedsReview: true, Flag: "Marked" }),
    ...Array.from({ length: fillerCount }, (_, i) => buildEntryFixture(accountId, 8 + i, { Payee: `Blindgänger ${8 + i}` })),
  ];
  const needsReviewCount = entries.filter((e) => e.NeedsReview).length;
  const deletedCount = entries.filter((e) => e.DeletedInYnab).length;
  const totalAmount = entries.reduce((sum, e) => sum + e.Amount.Amount, 0);
  return {
    Entries: entries,
    TotalCount: entries.length,
    TotalAmount: { Amount: totalAmount, Currency: "EUR" },
    DeletedCount: deletedCount,
    NeedsReviewCount: needsReviewCount, AskLenaCount: 0,
  };
}

/// Registriert die vier Ledger-Endpunkte, die der Seiteneinstieg
/// (`Components.Ledger.State.pageLoadCmd`) auslöst, als gemockte Antworten auf
/// `page`. Kein Backend nötig: Playwright fängt die Anfrage ab, bevor sie das
/// Netzwerk erreicht (die App spricht sonst über den Vite-Proxy mit einem echten
/// `dotnet`-Prozess, hier bewusst nicht gestartet).
export async function mockLedgerApi(page, { accountsFixture, balancesFixture, entriesFixture }) {
  await page.route("**/api/LedgerApi/getLedgerAccounts", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(accountsFixture) })
  );
  await page.route("**/api/LedgerApi/getLedgerAccountBalances", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(balancesFixture) })
  );
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(entriesFixture) })
  );
  await page.route("**/api/LedgerApi/getLastMirrorRun", (route) =>
    route.fulfill({ contentType: "application/json", body: "null" })
  );
}
