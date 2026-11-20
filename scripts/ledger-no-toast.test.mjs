// scripts/ledger-no-toast.test.mjs
//
// Schlussprüfung der Reihe „Kassenbuch ohne Toast“ (ledger-enjex, Regel R3, ADR 0098): am Handy kommt im Kassenbuch
// kein Toast mehr. Ein Lauf löst nacheinander Wisch, Spiegeln, Neue Buchung, Kategorie, Memo und Löschen aus. Ein
// Beobachter im Browser hält fest, ob je ein Element im Behälter des Toasts sichtbar wurde. Dazu prüft der Lauf je
// Schritt den Platz der Rückmeldung: Leiste (Wisch, Spiegeln, Neue Buchung), Feld im Detail (Kategorie, Memo, ohne
// Meldung in Leiste oder Toast-Behälter), Zeile am Platz (Löschen). Gemessen am echten, per Vite gestarteten
// Fable-Client gegen gemockte Antworten, nie die echte Entwicklungsdatenbank.
//
// Ausführen: node --test scripts/ledger-no-toast.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import { CATEGORY_IDS, buildEntryFixture, entriesPage, fixtureGuid } from "./ledger-filter-bar.mjs";
import { mockCapture } from "./ledger-capture-fixture.mjs";
import { closeLedgerSheet, swipeReviewed } from "./layout-states.mjs";

let frontend;
let frontendPort;
let browser;

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
const TODAY = new Date().toLocaleDateString("sv-SE", { timeZone: "Europe/Berlin" }) + "T00:00:00.0000000";
/// Das Girokonto aus `mockCapture` (Standardkonto der Liste).
const GIRO = fixtureGuid(8003);
const lebensmittel = { CategoryId: CATEGORY_IDS.lebensmittel, CategoryName: "Lebensmittel", CategoryGroupName: "Alltag" };

async function open(context) {
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const removed = new Set();
  await mockCapture(page);
  const entries = () =>
    [
      buildEntryFixture(GIRO, 1, { Date: TODAY, LinkedToYnab: true, Payee: "Bauhaus Wisch", NeedsReview: true, ...lebensmittel }),
      buildEntryFixture(GIRO, 2, { Date: TODAY, LinkedToYnab: true, Payee: "Kiosk Kategorie", ...lebensmittel }),
      buildEntryFixture(GIRO, 3, { Date: TODAY, LinkedToYnab: true, Payee: "Laden Memo", ...lebensmittel }),
      buildEntryFixture(GIRO, 4, { Date: TODAY, LinkedToYnab: true, Payee: "Markt Löschen", ...lebensmittel }),
    ].filter((e) => !removed.has(e.Payee));
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (r) => json(r, entriesPage(entries())));
  await page.route("**/api/LedgerApi/setReviewMark", (r) =>
    json(r, { Ok: { Previous: { IsApproved: false, ClearedState: "ReviewUncleared" }, Change: "ReviewMarkChanged", WriteBack: "ReviewMarkSent" } })
  );
  await page.route("**/api/LedgerApi/mirrorYnabBudget", (r) =>
    json(r, { Ok: { NewEntries: 0, ChangedEntries: 1, DeletedEntries: 0, ServerKnowledge: 2 } })
  );
  await page.route("**/api/LedgerApi/setEntryCategory", (r) =>
    json(r, {
      Ok: {
        Previous: { CategoryId: { YnabCategoryId: CATEGORY_IDS.lebensmittel }, CategoryName: "Lebensmittel" },
        Change: "CategoryChanged",
        WriteBack: "CategorySent",
        PreviousTransferAccountId: null,
      },
    })
  );
  await page.route("**/api/RulesApi/ruleCategoryFor", (r) => json(r, null));
  await page.route("**/api/LedgerApi/setEntryMemo", (r) =>
    json(r, { Ok: { PreviousMemo: null, CurrentMemo: "Zeitung", MemoChange: "MemoChanged", MemoWriteBack: "MemoSent" } })
  );
  await page.route("**/api/LedgerApi/deleteEntry", (r) => {
    removed.add("Markt Löschen");
    return json(r, { Ok: null });
  });
  // Hält fest, ob je ein Element im Behälter des Toasts sichtbar wurde (auch kurz, zwischen zwei Prüfungen).
  await page.addInitScript(() => {
    window.__visibleToasts = [];
    const check = () => {
      for (const el of document.querySelectorAll(".toast-layer > *")) {
        if (el.checkVisibility()) window.__visibleToasts.push(el.textContent.trim());
      }
    };
    new MutationObserver(check).observe(document, { childList: true, subtree: true, attributes: true });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  return page;
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

const barAction = (page, text) => page.getByTestId("feedback-bar").getByTestId("feedback-action").filter({ hasText: text });

/// Im Behälter des Toasts steht keine Meldung mit diesem Text (auch nicht verborgen): sie ging nicht an den Platz der
/// Meldungen, sie steht am Feld.
const notInToastSlot = async (page, text, where) => {
  const slot = (await page.locator(".toast-layer").textContent()) ?? "";
  assert.ok(!slot.includes(text), `${where}: „${text}“ steht im Toast-Behälter`);
  assert.equal(await barAction(page, text).count(), 0, `${where}: „${text}“ steht in der Leiste`);
};

const openSheet = async (page, payee) => {
  await page.getByText(payee, { exact: true }).first().tap();
  const sheet = page.locator(".bottom-sheet.active").first();
  await sheet.locator('[data-testid="detail-payee"]').waitFor();
  return sheet;
};

test("[layout] Handy (412): Wisch, Spiegeln, Neue Buchung, Kategorie, Memo und Löschen nacheinander, kein Toast sichtbar", async () => {
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });
  const page = await open(context);
  await page.getByText("Bauhaus Wisch", { exact: true }).first().waitFor();

  // Wisch: „✓ Geprüft“ in der Leiste.
  await swipeReviewed(page, "Bauhaus Wisch");
  await barAction(page, "Geprüft").waitFor();

  // Spiegeln: „✓ Gespiegelt“ in der Leiste.
  await page.getByRole("button", { name: "Jetzt spiegeln", exact: true }).filter({ visible: true }).first().tap();
  await barAction(page, "Gespiegelt").waitFor();

  // Neue Buchung: „✓ Angelegt“ in der Leiste.
  await page.getByTestId("ledger-capture-fab").tap();
  const capture = page.locator(".bottom-sheet.active", { hasText: "Neue Buchung" });
  await capture.getByRole("button", { name: "Bäckerei Lange · 8,60" }).tap();
  await capture.getByRole("button", { name: "Buchung anlegen" }).tap();
  await barAction(page, /^Angelegt$/).waitFor();

  // Kategorie: „✓ Geändert“ unter „Kategorie“ im Sheet, nicht im Platz der Meldungen.
  let sheet = await openSheet(page, "Kiosk Kategorie");
  await sheet.locator('[data-testid="detail-category-arrow"]').tap();
  const picker = page.locator(".bottom-sheet.active", { hasText: "Ziel wählen" });
  await picker.locator('[role="option"]', { hasText: "Nebenkosten" }).last().tap();
  await page.locator('[data-testid="detail-note"][data-note-field="category"]').getByText("Geändert", { exact: true }).waitFor();
  await notInToastSlot(page, "Kategorie geändert", "Kategorie");
  await closeLedgerSheet(page);

  // Memo: „✓ Gesetzt“ unter „Memo“ im Sheet.
  sheet = await openSheet(page, "Laden Memo");
  await sheet.locator('[data-testid="detail-memo-action"]').tap();
  const editor = page.locator('.bottom-sheet.active [data-testid="detail-memo-editor"]');
  await editor.locator("textarea").fill("Zeitung");
  await editor.getByRole("button", { name: "Speichern", exact: true }).tap();
  await page.locator('[data-testid="detail-note"][data-note-field="memo"]').getByText("Gesetzt", { exact: true }).waitFor();
  await notInToastSlot(page, "Memo gesetzt", "Memo");
  await closeLedgerSheet(page);

  // Löschen: die Zeile am Platz mit „Rückgängig“, keine Meldung.
  sheet = await openSheet(page, "Markt Löschen");
  const remove = sheet.getByRole("button", { name: "Buchung löschen" });
  await remove.scrollIntoViewIfNeeded();
  await remove.tap();
  await page.locator('[data-testid="ledger-deleted-row"]:visible [data-phase="removed"]').waitFor();
  await notInToastSlot(page, "gelöscht", "Löschen");

  const seen = await page.evaluate(() => window.__visibleToasts);
  assert.deepEqual(seen, [], `sichtbare Toasts: ${seen.join(" | ")}`);
  await context.close();
});
