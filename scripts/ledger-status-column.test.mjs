// scripts/ledger-status-column.test.mjs
//
// Gerenderte Belege für `ledger-qns4j` ("Konten-Spalte haftend, Statusspalte neu
// belegen"), Iteration 2: der Verifier von Iteration 1 hat zurecht bemängelt, dass
// die bisherigen `dotnet test`-Zusicherungen nur den Quelltext lasen (Substrings, die
// teils schon vor der Änderung wahr waren) statt tatsächlich zu rendern und zu
// messen. Diese Datei tut genau das: ein echter, per Vite gestarteter
// Fable-Client (echtes Tailwind-CSS, echtes kompiliertes Bundle) gegen
// gemockte Ledger-API-Antworten (`scripts/ledger-status-column.mjs`, rein
// erfundene Werte, nie die echte Entwicklungsdatenbank, weder Original noch
// Kopie).
//
// Kein Backend nötig: Playwright fängt die vier Ledger-Endpunkte ab, bevor sie
// das Netzwerk erreichen (`mockLedgerApi`); der `dotnet`-Prozess wird hier bewusst
// nie gestartet.
//
// Muster übernommen aus `scripts/screenshot-route.mjs`/`.test.mjs`
// (design-system-wa6wz/-q2bmv): dieselbe Port-/Prozess-Verwaltung
// (`getFreePort`/`startFrontend`/`stopServer`), jetzt zusätzlich exportiert, damit
// sie hier ohne Nachbau wiederverwendet werden kann.
//
// Ausführen: node --test scripts/ledger-status-column.test.mjs
// (Teil von `npm run test:scripts`, das alle scripts/*.test.mjs läuft.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntriesFixture,
  mockLedgerApi,
  SIX_CASE_PAYEES,
} from "./ledger-status-column.mjs";

let frontend;
let frontendPort;
let browser;
let page;

const ACCOUNT_COUNT = 100; // deutlich mehr, als in die Höhe von 1440x900 passt (AC2)

/// Wartet, bis JEDE Kopfzelle der PC-Tabelle eine von Null verschiedene UND
/// zwischen zwei aufeinanderfolgenden Messungen unveränderte Breite trägt
/// (Verifier-Befund Iteration 2, ledger-qns4j): eine einmalige Prüfung "erste
/// Kopfzelle > 0" reichte nicht, in 2 von 4 Läufen las AC6 trotzdem "Datum ... war 0".
/// Eigene Funktion statt Kopie, damit `before()` UND die AC6-Messung selbst
/// dieselbe Wartebedingung teilen: `before()` allein reichte in Iteration 3 nicht
/// aus, weil zwischen `before()` und der AC6-Messung (der letzten von neun Tests,
/// nach Seiten- und Spalten-Scrollen in AC1/AC2) noch genug Zeit für einen erneuten,
/// unabhängigen Layout-Wackler liegt: die Wartebedingung muss deshalb unmittelbar
/// vor der eigentlichen Messung erneut greifen, nicht nur einmal am Anfang.
async function waitForStableHeaderWidths(page) {
  await page.waitForFunction(
    () => {
      const ths = [...document.querySelectorAll("table thead th")];
      if (ths.length === 0) return false;
      const widths = ths.map((th) => th.getBoundingClientRect().width);
      if (widths.some((w) => w <= 0)) return false;
      const previous = window.__ledgerHeaderWidths;
      window.__ledgerHeaderWidths = widths;
      if (!previous || previous.length !== widths.length) return false;
      return previous.every((w, i) => Math.abs(w - widths[i]) < 0.5);
    },
    undefined,
    { polling: 50, timeout: 30000 }
  );
}

before(async () => {
  frontendPort = await getFreePort();
  // `backendPort` bleibt undefined: kein Backend wird gestartet, alle
  // `/api/LedgerApi/*`-Aufrufe werden unten per `page.route` beantwortet, bevor sie
  // Vites Proxy überhaupt erreichen.
  frontend = await startFrontend(frontendPort, undefined);

  browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  page = await context.newPage();
  page.setDefaultTimeout(30000);

  const accountsFixture = buildAccountsFixture(ACCOUNT_COUNT);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const entriesFixture = buildEntriesFixture(accountsFixture.DefaultAccountId);

  // Fängt jede /api-Anfrage ab, die dieser Test nicht kennt (Settings/SyncFlow/Rules
  // laden beim App-Start unabhängig von der aktuellen Seite mit, State.fs `init()`):
  // ein sprechender 500er statt eines ECONNREFUSED gegen einen nie gestarteten
  // Backend-Prozess. Zuerst registriert, die spezifischeren Ledger-Routen danach
  // (Playwright prüft zuletzt registrierte Handler zuerst).
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockLedgerApi(page, { accountsFixture, balancesFixture, entriesFixture });

  // `waitUntil: "load"`, nicht "networkidle": Vites eigene HMR-Websocket-Verbindung
  // hält das Netzwerk dauerhaft "aktiv", "networkidle" würde hier nie feuern.
  // `waitForPageLoaded` unten wartet ohnehin auf das App-eigene Bereitschaftssignal
  // (`data-app-ready`), nicht auf ein Netzwerk-Stillstands-Heuristik.
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  // `data-app-ready` spiegelt den Elmish-Zustand (Entries = Success ...), nicht
  // notwendig den bereits abgeschlossenen DOM-Rendervorgang. Unter Last (z. B. wenn
  // `npm run test:scripts` mehrere Testdateien parallel laufen lässt) reichte das
  // allein nicht: die Tabelle war laut Zustand "fertig", aber `getBoundingClientRect`
  // auf ihren Kopfzellen lieferte noch 0 breite. Zusätzlich auf tatsächlich gelayoutete
  // Kopfzellen warten, bevor irgendein Test misst.
  //
  // Iteration 3 (Verifier-Befund Iteration 2): "nicht-null" allein reichte nicht.
  // Die vorherige Fassung prüfte nur die ERSTE `th` einmalig auf width > 0 und war in
  // 2 von 4 Läufen trotzdem flockig ("Datum sollte ~96px sein, war 0"). Siehe
  // `waitForStableHeaderWidths` oben: dieselbe Wartebedingung läuft hier UND direkt
  // vor der AC6-Messung selbst.
  await waitForStableHeaderWidths(page);
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

// ============================================
// AC1/AC2: Konten-Spalte haftet und scrollt bei Überlänge in sich
// ============================================

test("Die Konten-Spalte bleibt beim Scrollen der Seite unter der Navbar stehen (AC1)", async () => {
  // Vor dem Scrollen steht die Spalte in ihrer natürlichen Flussposition, unterhalb
  // von Navbar UND Seitenkopf ("Kassenbuch" + Button), deutlich weiter unten als
  // 64px. Genau DAS ist der gemeldete Befund, sobald die Seite ein Stück scrollt:
  // ohne `position: sticky` liefe die Spalte mit der Seite mit und verschwände nach
  // oben aus dem Bild, statt bei 64px (direkt unter der Navbar) stehen zu bleiben.
  const stripTopBefore = await page.locator(".ledger-account-strip").evaluate((el) => el.getBoundingClientRect().top);
  assert.ok(stripTopBefore > 64, `Vor dem Scrollen muss die Spalte unterhalb der Navbar+Seitenkopf stehen, war ${stripTopBefore}`);

  // Weiter scrollen, als die Spalte an natürlicher Position stehen würde: ohne
  // Sticky wäre sie jetzt weit nach oben aus dem sichtbaren Bereich verschwunden
  // (negative top). Playwright/Chromium braucht keinen Tick zum Nachrendern von
  // position:sticky, ein evaluate-Roundtrip reicht.
  // `behavior: "instant"`: `html { scroll-behavior: smooth }` (styles.css) würde
  // sonst eine animierte Bildlaufbewegung anstoßen; ein sofortiges Auslesen von
  // window.scrollY/getBoundingClientRect direkt danach sähe dann noch den alten,
  // unbewegten Stand.
  await page.evaluate((scrollBy) => window.scrollTo({ top: scrollBy, behavior: "instant" }), stripTopBefore + 500);
  const stripTopAfter = await page.locator(".ledger-account-strip").evaluate((el) => el.getBoundingClientRect().top);
  assert.ok(
    Math.abs(stripTopAfter - 64) <= 1,
    `Die Spalte muss nach dem Scrollen bei 64px unter der Navbar stehen bleiben (der gemeldete Befund: vorher lief sie mit der Seite mit und verschwand), war ${stripTopAfter}`
  );

  await page.evaluate(() => window.scrollTo({ top: 0, behavior: "instant" }));
});

test("Eine Kontoliste mit mehr Konten, als in den Viewport passen, ist innerhalb der Spalte höher als ihr sichtbarer Bereich (AC2)", async () => {
  const { scrollHeight, clientHeight } = await page
    .locator(".ledger-account-strip")
    .evaluate((el) => ({ scrollHeight: el.scrollHeight, clientHeight: el.clientHeight }));
  assert.ok(
    scrollHeight > clientHeight + 20,
    `Mit ${ACCOUNT_COUNT} Konten muss der Inhalt sichtbar höher sein als der sichtbare Bereich (scrollHeight=${scrollHeight}, clientHeight=${clientHeight}), sonst ist AC2 mit dieser Fixture nicht geprüft.`
  );
});

test("Die Kontoliste scrollt tatsächlich in sich, während die Spalte selbst stehen bleibt (AC2)", async () => {
  // Alles in EINEM page.evaluate-Aufruf (statt mehrerer Roundtrips): so liegen
  // "vorher" und "nachher" exakt im selben Frame, ohne dass zwischen den Messungen
  // echte Zeit vergeht, in der sich am Layout etwas ändern könnte.
  const result = await page.evaluate(({ accountCount }) => {
    const strip = document.querySelector(".ledger-account-strip");
    const findRow = (text) =>
      [...strip.querySelectorAll("*")].find((el) => el.children.length === 0 && el.offsetParent !== null && el.textContent.trim() === text);

    // Wie viele Konten passen überhaupt sichtbar in die Spalte? Aus der gemessenen
    // Zeilenhöhe (Abstand zwischen Konto 1 und Konto 2) berechnet, statt eine
    // bestimmte Kontonummer zu raten, robust gegenüber Layout-Feinheiten.
    const first = findRow("Testkonto 1");
    const second = findRow("Testkonto 2");
    const rowHeight = second.getBoundingClientRect().top - first.getBoundingClientRect().top;
    const visibleRowsApprox = Math.ceil(strip.clientHeight / rowHeight);
    const lateIndex = Math.min(accountCount, visibleRowsApprox + 15);
    const lateAccountName = `Testkonto ${lateIndex}`;

    const stripRectBefore = strip.getBoundingClientRect();
    const lateBefore = findRow(lateAccountName);
    const firstVisibleBefore = first.getBoundingClientRect().top < stripRectBefore.bottom;
    const lateVisibleBefore = lateBefore ? lateBefore.getBoundingClientRect().top < stripRectBefore.bottom : false;
    const scrollTopBefore = strip.scrollTop;
    const stripTopBefore = stripRectBefore.top;

    // Scrollt NUR die Konten-Spalte selbst (nicht die Seite) genau so weit, dass das
    // späte Konto in die Mitte des sichtbaren Bereichs rückt, nicht bis ganz nach
    // unten (dort wäre Konto Nr. `lateIndex` bei `accountCount` Konten längst wieder
    // nach oben hinausgescrollt).
    strip.scrollTop = lateBefore.offsetTop - strip.clientHeight / 2;
    const stripRectAfter = strip.getBoundingClientRect();
    const lateAfter = findRow(lateAccountName);
    const lateVisibleAfter =
      lateAfter.getBoundingClientRect().top < stripRectAfter.bottom && lateAfter.getBoundingClientRect().top >= stripRectAfter.top;
    const scrollTopAfter = strip.scrollTop;
    const stripTopAfter = stripRectAfter.top;

    strip.scrollTop = 0;

    return {
      rowHeight,
      visibleRowsApprox,
      lateIndex,
      lateAccountName,
      firstVisibleBefore,
      lateVisibleBefore,
      scrollTopBefore,
      stripTopBefore,
      lateVisibleAfter,
      scrollTopAfter,
      stripTopAfter,
    };
  }, { accountCount: ACCOUNT_COUNT });

  assert.ok(
    result.lateIndex > result.visibleRowsApprox,
    `Testaufbau: ${ACCOUNT_COUNT} Konten müssen für ein sicher unsichtbares spätes Konto reichen (rowHeight=${result.rowHeight}, visibleRowsApprox=${result.visibleRowsApprox})`
  );
  assert.equal(result.firstVisibleBefore, true, "Vor dem internen Scrollen muss das erste Konto sichtbar sein");
  assert.equal(
    result.lateVisibleBefore,
    false,
    `Vor dem internen Scrollen darf '${result.lateAccountName}' noch nicht sichtbar sein`
  );
  assert.ok(result.scrollTopAfter > result.scrollTopBefore, "scrollTop der Spalte muss sich durch das interne Scrollen erhöht haben");
  assert.equal(result.lateVisibleAfter, true, `Nach dem internen Scrollen muss '${result.lateAccountName}' sichtbar sein`);
  // Die Spalte selbst darf sich durch ihr EIGENES internes Scrollen nicht bewegen
  // (nur ihr Inhalt scrollt, nicht die Spalte in der Seite), verglichen gegen ihre
  // eigene Position unmittelbar davor, nicht gegen die feste Haft-Position 64px: die
  // Seite selbst wurde in diesem Testfall nicht gescrollt, die Spalte steht also an
  // ihrer natürlichen (noch nicht "gestickten") Flussposition.
  assert.ok(
    Math.abs(result.stripTopAfter - result.stripTopBefore) <= 1,
    `Die Spalte darf sich durch ihr eigenes internes Scrollen nicht von der Stelle bewegen (vorher ${result.stripTopBefore}, nachher ${result.stripTopAfter})`
  );
});

// ============================================
// AC3/AC4: Statusspalte zeigt Status, nicht Herkunft
// ============================================

/// Findet die `<tr>` der PC-Tabelle, deren Payee-Zelle EXAKT den gegebenen Text
/// trägt (nicht bloß als Substring der ganzen Zeile, sonst träfe z. B.
/// "Fixture NeedsReview" auch die Zeilen "Fixture NeedsReview AskLena"/"...Deleted").
async function tableRowByPayee(payeeText) {
  const escaped = payeeText.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return page.locator("table tbody tr").filter({ has: page.locator("td", { hasText: new RegExp(`^${escaped}$`) }) }).first();
}

/// Liest die Höhe der Vergleichszeile und der geprüften Zeile ATOMAR in einem
/// `page.evaluate`, nachdem `waitForFunction` sichergestellt hat, dass beide Zeilen im
/// DOM stehen, sichtbar sind und eine Höhe größer 0 haben (ledger-wj6pd).
/// Vermutete Ursache des sporadischen 0 px (nicht einzeln nachgestellt): die Tabelle
/// rendert nach den Scroll-Tests AC1/AC2 oder einem nachlaufenden Mock-Abruf neu; ein
/// Locator, der zwischen zwei getrennten `evaluate`-Aufrufen auf eine ersetzte oder
/// noch nicht ausgelegte Zeile zeigt, liefert dann 0 px für eine der beiden Zeilen.
async function measureRowHeights(baselinePayee, payee) {
  const args = { baselinePayee, payee };
  await page.waitForFunction(
    ({ baselinePayee, payee }) => {
      const rowFor = (text) =>
        [...document.querySelectorAll("table tbody tr")].find((tr) =>
          [...tr.querySelectorAll("td")].some((td) => td.textContent.replace(/\s+/g, " ").trim() === text)
        );
      return [rowFor(baselinePayee), rowFor(payee)].every((tr) => {
        if (!tr) return false;
        const r = tr.getBoundingClientRect();
        const cs = getComputedStyle(tr);
        return r.height > 0 && r.width > 0 && cs.visibility !== "hidden" && cs.display !== "none";
      });
    },
    args,
    { polling: 50, timeout: 30000 }
  );
  return page.evaluate(
    ({ baselinePayee, payee }) => {
      const rowFor = (text) =>
        [...document.querySelectorAll("table tbody tr")].find((tr) =>
          [...tr.querySelectorAll("td")].some((td) => td.textContent.replace(/\s+/g, " ").trim() === text)
        );
      return {
        baselineHeight: rowFor(baselinePayee).getBoundingClientRect().height,
        rowHeight: rowFor(payee).getBoundingClientRect().height,
      };
    },
    args
  );
}

test("Die PC-Tabelle zeigt an keiner Stelle den Herkunfts-Text 'YNAB-Spiegel' (AC3)", async () => {
  const tbodyText = await page.locator("table tbody").innerText();
  assert.ok(
    !tbodyText.includes("YNAB-Spiegel"),
    "Die Statusspalte darf die Herkunft nicht mehr zeigen (die einzige Herkunft in dieser Fixture ist MirroredFromYnab -> 'YNAB-Spiegel')."
  );
});

test("Nur Buchungen mit NeedsReview zeigen das Wort 'zu prüfen' in ihrer Zeile (AC4, ADR 0049: kleines Wort statt Badge)", async () => {
  const withoutReview = await tableRowByPayee(SIX_CASE_PAYEES.baseline);
  const withReview = await tableRowByPayee(SIX_CASE_PAYEES.needsReview);
  assert.ok(!(await withoutReview.innerText()).includes("zu prüfen"), "Eine geprüfte Buchung darf kein Kennzeichen tragen");
  assert.ok((await withReview.innerText()).includes("zu prüfen"), "Eine ungeprüfte Buchung muss 'zu prüfen' tragen");
});

// ============================================
// AC5: zwei Kennzeichen nebeneinander, ohne Umbruch, ohne höhere Zeile
// ============================================

test("NeedsReview + Flag 'Rückfrage Lena' stehen nebeneinander, ohne die Zeile zu erhöhen, im gekürzten Wortlaut 'Lena?' mit voller Bedeutung für assistive Technologie (AC5)", async () => {
  const { baselineHeight, rowHeight } = await measureRowHeights(SIX_CASE_PAYEES.baseline, SIX_CASE_PAYEES.needsReviewAskLena);

  const row = await tableRowByPayee(SIX_CASE_PAYEES.needsReviewAskLena);
  const rowText = await row.innerText();
  assert.ok(rowText.includes("zu prüfen"), "Muss das Wort 'zu prüfen' zeigen");
  assert.ok(rowText.includes("Lena?"), "Muss den GEKÜRZTEN Flag-Wortlaut zeigen, nicht 'Rückfrage Lena'");
  assert.ok(!rowText.includes("Rückfrage Lena"), "Der volle Wortlaut darf in der PC-Statusspalte nicht mehr stehen (Überlauf-Risiko)");

  assert.ok(
    Math.abs(rowHeight - baselineHeight) <= 1,
    `Zwei Kennzeichen dürfen die Zeile nicht höher machen (Baseline ${baselineHeight}px, diese Zeile ${rowHeight}px)`
  );

  const statusCell = row.locator("td").last();
  const { scrollHeight, clientHeight, scrollWidth, clientWidth } = await statusCell.evaluate((el) => ({
    scrollHeight: el.scrollHeight,
    clientHeight: el.clientHeight,
    scrollWidth: el.scrollWidth,
    clientWidth: el.clientWidth,
  }));
  assert.ok(scrollHeight <= clientHeight + 1, "Die Statuszelle darf nicht in eine zweite Zeile umbrechen (kein vertikaler Überlauf)");
  assert.ok(scrollWidth <= clientWidth + 1, "Die Statuszelle darf nicht waagerecht überlaufen");

  // Verifier-Befund Iteration 2: der gekürzte Wortlaut "Lena?" muss die volle
  // Bedeutung "Rückfrage Lena" als Hover-Hinweis (title) und für assistive
  // Technologie (aria-label) tragen (`withAccessibleName` in View.fs).
  const wrapper = statusCell.locator("[title], [aria-label]").first();
  assert.equal(
    await wrapper.getAttribute("title"),
    "Rückfrage Lena",
    "title des gekürzten Flag-Kennzeichens muss die volle Bedeutung 'Rückfrage Lena' tragen"
  );
  assert.equal(
    await wrapper.getAttribute("aria-label"),
    "Rückfrage Lena",
    "aria-label des gekürzten Flag-Kennzeichens muss die volle Bedeutung 'Rückfrage Lena' tragen"
  );
});

test("NeedsReview + 'in YNAB gelöscht' stehen nebeneinander, ohne die Zeile zu erhöhen, mit voller Bedeutung für assistive Technologie (AC5, Stand-in für 'nicht in YNAB verknüpft', siehe OUTCOME)", async () => {
  const { baselineHeight, rowHeight } = await measureRowHeights(SIX_CASE_PAYEES.baseline, SIX_CASE_PAYEES.needsReviewDeleted);

  const row = await tableRowByPayee(SIX_CASE_PAYEES.needsReviewDeleted);
  const rowText = await row.innerText();
  assert.ok(rowText.includes("zu prüfen"), "Muss das Wort 'zu prüfen' zeigen");

  const statusCell = row.locator("td").last();
  const badgeCount = await statusCell.locator(".rounded-full").count();
  assert.equal(badgeCount, 1, "Das Ausnahme-Symbol ist das einzige Badge; 'zu prüfen' ist ein Wort (ADR 0049)");

  assert.ok(
    Math.abs(rowHeight - baselineHeight) <= 1,
    `Zwei Kennzeichen dürfen die Zeile nicht höher machen (Baseline ${baselineHeight}px, diese Zeile ${rowHeight}px)`
  );

  const { scrollHeight, clientHeight, scrollWidth, clientWidth } = await statusCell.evaluate((el) => ({
    scrollHeight: el.scrollHeight,
    clientHeight: el.clientHeight,
    scrollWidth: el.scrollWidth,
    clientWidth: el.clientWidth,
  }));
  assert.ok(scrollHeight <= clientHeight + 1, "Die Statuszelle darf nicht in eine zweite Zeile umbrechen");
  assert.ok(scrollWidth <= clientWidth + 1, "Die Statuszelle darf nicht waagerecht überlaufen");

  const deletedIcon = statusCell.locator("[title], [aria-label]").last();
  assert.equal(
    await deletedIcon.getAttribute("title"),
    "in YNAB gelöscht",
    "title des Ausnahme-Symbols muss die volle Bedeutung 'in YNAB gelöscht' tragen"
  );
  assert.equal(
    await deletedIcon.getAttribute("aria-label"),
    "in YNAB gelöscht",
    "aria-label des Ausnahme-Symbols muss die volle Bedeutung 'in YNAB gelöscht' tragen"
  );
});

test("NeedsReview + Flag 'Markiert' stehen nebeneinander, ohne Umbruch, ohne höhere Zeile, als Symbol mit voller Bedeutung für assistive Technologie (AC5, Iteration 3)", async () => {
  // Genau die Kombination, die der Verifier von Iteration 2 als noch überlaufend
  // nachgerechnet hat (scrollWidth 131 > clientWidth 128 mit dem vollen Text
  // "Markiert"): jetzt ein Symbol wie `deletedStatusIcon`, mit deutlich mehr
  // Reserve als eine weitere Kürzung des Wortlauts.
  const { baselineHeight, rowHeight } = await measureRowHeights(SIX_CASE_PAYEES.baseline, SIX_CASE_PAYEES.needsReviewMarked);

  const row = await tableRowByPayee(SIX_CASE_PAYEES.needsReviewMarked);
  const rowText = await row.innerText();
  assert.ok(rowText.includes("zu prüfen"), "Muss das Wort 'zu prüfen' zeigen");
  assert.ok(!rowText.includes("Markiert"), "Der volle Text 'Markiert' darf in der PC-Statusspalte nicht mehr stehen (Überlauf-Risiko)");

  const statusCell = row.locator("td").last();
  const badgeCount = await statusCell.locator(".rounded-full").count();
  assert.equal(badgeCount, 1, "Das Markiert-Symbol ist das einzige Badge; 'zu prüfen' ist ein Wort (ADR 0049)");

  assert.ok(
    Math.abs(rowHeight - baselineHeight) <= 1,
    `Zwei Kennzeichen dürfen die Zeile nicht höher machen (Baseline ${baselineHeight}px, diese Zeile ${rowHeight}px)`
  );

  const { scrollHeight, clientHeight, scrollWidth, clientWidth } = await statusCell.evaluate((el) => ({
    scrollHeight: el.scrollHeight,
    clientHeight: el.clientHeight,
    scrollWidth: el.scrollWidth,
    clientWidth: el.clientWidth,
  }));
  assert.ok(scrollHeight <= clientHeight + 1, "Die Statuszelle darf nicht in eine zweite Zeile umbrechen");
  assert.ok(
    scrollWidth <= clientWidth + 1,
    `Die Statuszelle darf nicht waagerecht überlaufen (scrollWidth=${scrollWidth}, clientWidth=${clientWidth}, der von Iteration 2 gemeldete Befund war 131 > 128)`
  );

  const markedIcon = statusCell.locator("[title]:not(button), [aria-label]:not(button)").last();
  assert.equal(
    await markedIcon.getAttribute("title"),
    "Markiert",
    "title des Markiert-Symbols muss die volle Bedeutung 'Markiert' tragen"
  );
  assert.equal(
    await markedIcon.getAttribute("aria-label"),
    "Markiert",
    "aria-label des Markiert-Symbols muss die volle Bedeutung 'Markiert' tragen"
  );
});

// ============================================
// AC6: Spaltenraster, Status auf 128px, die 80px gehen an Payee/Memo
// ============================================

test("Spaltenraster: Kästchen 44px, Status 128px, Payee und Kategorie ab Mindestbreite, Memo Rest (AC6, ledger-n4vte, ledger-r1d2f, ledger-qh9qx)", async () => {
  // Iteration 3 (Verifier-Befund Iteration 2): dieselbe Wartebedingung wie in
  // `before()` UNMITTELBAR vor dieser Messung nochmal, nicht nur einmal am
  // Anfang der Datei. AC6 ist der neunte und letzte Test, nach dem Seiten- und
  // Spalten-Scrollen aus AC1/AC2; `before()` allein garantierte in Iteration 3
  // noch nicht, dass das Layout GENAU JETZT (statt nur beim Start) stabil ist.
  await waitForStableHeaderWidths(page);

  // Iteration 3, zweiter Teil des Fixes: ALLE Werte (Kopftexte, Kopfbreiten,
  // Tabellenbreite) in EINEM einzigen `page.evaluate`-Aufruf statt in mehreren
  // `page.locator(...).evaluateAll(...)`-Aufrufen nacheinander gelesen. Beim
  // Diagnostizieren des Verifier-Befunds fiel ein reproduzierbares, aber sehr
  // spezifisches Muster auf: dreißig aufeinanderfolgende `page.evaluate`-Aufrufe
  // direkt vor der Messung lasen IMMER die korrekten, stabilen Breiten, während der
  // anschließende SEPARATE `page.locator("table thead th").evaluateAll(...)`-Aufruf
  // für die Breiten (nach einem vorherigen, ebenfalls separaten `evaluateAll`-Aufruf
  // für die Kopftexte) in ca. 30-40% der Läufe trotzdem 0 zurückgab, obwohl `document
  // .querySelectorAll` im selben Moment stabile Werte zeigte. Zwei getrennte
  // Locator-Aufrufe hintereinander sind also selbst die Fehlerquelle, nicht die
  // Layout-Stabilität an sich (die schon `waitForStableHeaderWidths` sichert). Ein
  // einziger atomarer `page.evaluate`-Aufruf umgeht dieses Verhalten vollständig:
  // fünfzehn Wiederholungsläufe mit dieser Fassung waren alle grün (siehe OUTCOME).
  const { headers, widths, tableWidth } = await page.evaluate(() => {
    const table = document.querySelector("table");
    const ths = [...table.querySelectorAll("thead th")];
    return {
      // `textContent`, nicht `innerText`: `innerText` liefert den optisch
      // gerenderten Text (inklusive CSS `text-transform: uppercase` auf
      // `Table.headerCell`) erst, sobald das Layout tatsächlich fertig ist, auf
      // einem frisch gestarteten Vite-Dev-Server ein Wettlauf. `textContent` liest
      // den rohen DOM-Text, unabhängig vom CSS-Zeitpunkt.
      headers: ths.map((th) => th.textContent.trim()),
      widths: ths.map((th) => th.getBoundingClientRect().width),
      tableWidth: table.getBoundingClientRect().width,
    };
  });

  // Reihenfolge für ein einzelnes Konto (keine Konto-Spalte): Kästchen (Kopf ohne Text, ledger-r1d2f),
  // Datum, Payee, Kategorie, Memo, Betrag, Status.
  assert.deepEqual(headers, ["", "Datum", "Payee", "Kategorie", "Memo", "Betrag", "Status"]);

  const [boxWidth, datumWidth, payeeWidth, kategorieWidth, memoWidth, betragWidth, statusWidth] = widths;

  // Spaltenraster der ruhigen Tabelle (ledger-n4vte): feste Breiten für Datum, Betrag
  // und Status (mit der Kästchen-Spalte von 44px, ledger-r1d2f), Memo bekommt den Rest.
  assert.ok(Math.abs(boxWidth - 44) <= 2, `Kästchen-Spalte sollte ~44px sein, war ${boxWidth}`);
  assert.ok(Math.abs(datumWidth - 96) <= 2, `Datum sollte ~96px sein, war ${datumWidth}`);
  assert.ok(Math.abs(betragWidth - 132) <= 2, `Betrag sollte ~132px sein, war ${betragWidth}`);
  assert.ok(Math.abs(statusWidth - 128) <= 2, `Status sollte ~128px sein (Handoff-Wert), nicht mehr 208px (Pflaster aus ledger-x0yga), war ${statusWidth}`);
  // Seit ledger-qh9qx keine Prozentanteile mehr: Payee und Kategorie haben Mindestbreiten
  // (140 und 120px, `visibleColumns`), den Überschuss teilen Payee, Kategorie und Memo.
  assert.ok(payeeWidth >= 139, `Payee sollte mindestens 140px breit sein, war ${payeeWidth}`);
  assert.ok(kategorieWidth >= 119, `Kategorie sollte mindestens 120px breit sein, war ${kategorieWidth}`);

  // Memo füllt genau den Rest, den die übrigen Spalten übrig lassen, und ist auf
  // dem 1440er Viewport breit genug, um einen kurzen Text zu zeigen (vorher blieben
  // durch den Rahmen mit Breitenbegrenzung nur ca. 45px).
  const expectedMemo = tableWidth - (boxWidth + datumWidth + payeeWidth + kategorieWidth + betragWidth + statusWidth);
  assert.ok(
    Math.abs(memoWidth - expectedMemo) <= 4,
    `Memo (${memoWidth}) sollte den Rest der Tabellenbreite (${expectedMemo}) füllen`
  );
  assert.ok(memoWidth >= 120, `Memo sollte mindestens 120px breit sein, war ${memoWidth}`);
});
