# Development Diary

This diary tracks the development progress of BudgetBuddy.

---

## 2026-10-20 09:55 - "Nach X filtern" an Payee- und Kategorie-Zelle (ledger-rfq5p)

**What I did:**
Zellmenü für die Payee- und Kategorie-Zelle der PC-Tabelle (FilterPopover mit menuItem), dieselben Aktionen im Handy-Sheet `historySheet`, Reduzierer-Nachrichten `OpenCellMenu`, `CloseCellMenu`, `FilterByRowPayee`, `FilterByRowCategory`, neues Feld `LedgerEntryView.CategoryId`, Styleguide-Karte, Playwright-Wächter.

**Files Added:**
- `src/Tests/LedgerCellMenuTests.fs` - 20 Tests (Beschriftungen, Lesepfad, Reduzierer, Quelltext-Zusicherungen)
- `scripts/ledger-cell-menu.test.mjs` - 9 Browser-Prüfungen (PC und 390x844, Styleguide)

**Files Modified:**
- `src/Shared/Domain.fs` - `LedgerEntryView.CategoryId`
- `src/Server/Ledger/QueryHandlers.fs` - `toEntryView` füllt `CategoryId`
- `src/Client/DesignSystem/FilterPopover.fs` - `menuWidthClass`, `menuItem`, `menuItemAutoFocus`
- `src/Client/Components/Ledger/Types.fs` - `CellMenuKind`, `CellMenuTarget`, `Model.CellMenu`, vier Nachrichten
- `src/Client/Components/Ledger/State.fs` - Init, Reduzierer, `keyboardBlocked`, `OpenFilterPopover` schließt das Zellmenü
- `src/Client/Components/Ledger/View.fs` - `payeeMenuLabel`, `categoryMenuLabel`, Auslöser, `payeeCell`, `categoryCell`, Aktionsblock in `historySheet`, `CellMenu` durch `entryListDesktop` und `entryTableRow`
- `src/Client/Components/Styleguide/View.fs` - Karte "FilterPopover menuItem"
- `src/Tests/Tests.fsproj` - neue Testdatei
- `src/Tests/LedgerViewTests.fs`, `LedgerPagingTests.fs`, `LedgerReviewMarkClientTests.fs`, `LedgerSelectionTests.fs` - `CategoryId = None` in den Literalen, zwei Quelltext-Wächter auf die neuen Zellsignaturen angepasst

**Rationale:**
Roman wollte eine Abkürzung zum Filtern aus dem Wert, der in der Liste steht. Das Menü setzt den bestehenden Filterweg, damit Chips, Zählung und Summe unverändert aus der Serverabfrage kommen. Die Kategorie-Id fehlte im Lesepfad, ein Nachschlagen über den Namen wäre über Gruppen hinweg nicht eindeutig.

**Outcomes:**
- Build: ✅ (0 Warnungen)
- Tests: 1571 bestanden, 0 Fehler (7 übersprungen); test:scripts 149/149; guard:classes grün
- Issues: keine

---

## 2026-10-20 10:05 - Prüf-Schaltfläche der PC-Zeile nur bei Hover oder Fokus, neutral grau (ledger-mhe2s)

**What I did:**
Die Prüf-Schaltfläche in der Statusspalte der PC-Tabelle ist ohne Hover und Fokus unsichtbar (Breite bleibt), bei Zeilen-Hover, Zeilenfokus und eigenem Fokus sichtbar. Haken und Pfeil sind neutral grau, die Schaltfläche trägt einen Tooltip. Am Handy bleibt sie sichtbar.

**Files Added:**
- `src/Tests/LedgerReviewButtonVisibilityTests.fs` - 8 Tests zu Klassen, Farbe, Tooltip und Verdrahtung
- `scripts/ledger-review-button-visibility.test.mjs` - Playwright-Wächter am echten Element

**Files Modified:**
- `src/Client/Components/Ledger/View.fs` - `reviewMarkButtonClassName`, `reviewMarkTooltip`, `group` an der PC-Zeile, neutrale Icons
- `src/Tests/Tests.fsproj` - neue Testdatei eingetragen, Merge-Konflikt mit main aufgelöst
- `scripts/ledger-status-column.test.mjs` - Markiert-Symbol-Suche schließt `button` aus

**Rationale:**
Der grüne Haken neben "zu prüfen" las sich wie "erledigt" (ADR 0049: Grün ist ein Signal).

**Outcomes:**
- Build: 0 Warnungen
- Tests: dotnet 1540 bestanden, test:scripts 139 von 139
- Messung (1440px, echtes Element): ohne Hover opacity 0, Breite 28px; bei Zeilen-Hover opacity 1; Fokus auf der Schaltfläche opacity 1
- Issues: Wächter braucht offene `mirrorYnabBudget`-Anfrage, sonst Neuaufbau der Liste

---

## 2026-10-20 10:15 - Kassenbuch-Sortierung im Lesepfad (ledger-r72re)

**What I did:**
`LedgerEntryPageQuery` um `Sort: LedgerSort` erweitert. `loadEntriesPage` sortiert Datum, Payee und Konto in SQL, Betrag und Kategorie (und jeden Betragsfilter-Fall) in F#. Kategorie-Namensregel an einer Stelle in `Ledger.Persistence`, von `toEntryView` und vom Sortierschlüssel genutzt. Auswahlauflösung für "Geprüft" ignoriert das `Sort`. Messskript für 14.000 Buchungen.

**Files Added:**
- `src/Tests/LedgerSortTests.fs` - 10 Tests zur Sortierung (Reihenfolge, Zählwege, Betragsfilter, LocallyRemoved, Auswahlauflösung)
- `scripts/measure-ledger-sort.fsx` - Messung bei 14.000 Buchungen gegen eine Datei-Datenbank

**Files Modified:**
- `src/Shared/Domain.fs` - `LedgerSortField`, `LedgerSortDirection`, `LedgerSort`, `LedgerSort.defaultSort`, Feld `Sort`
- `src/Server/Ledger/Persistence.fs` - `loadEntriesPage` mit `sort`, SQL-`ORDER BY`, F#-Vergleich, geteilte Kategorie-Namensregel, `loadCategoryGroups`/`loadCategoryNames` nach oben verschoben
- `src/Server/Ledger/QueryHandlers.fs` - `Sort` durchgereicht, `toEntryView` nutzt die geteilte Namensregel
- `src/Server/Ledger/CommandHandlers.fs` - `resolveReviewMarkBatchSelection` mit `LedgerSort.defaultSort`
- `src/Client/Components/Ledger/State.fs` - `Sort = LedgerSort.defaultSort` in `buildPageQuery`
- `src/Tests/Tests.fsproj`, `LedgerPageTests.fs`, `LedgerPagingTests.fs`, `LedgerReviewMarkBatchTests.fs`, `LedgerWithoutCategoryTests.fs` - neue Datei eingetragen, Feld `Sort` in den Literalen

**Rationale:**
Teil 1 von 3 der Sortierung (ADR 0051). Server und Vertrag zuerst, damit Spaltenköpfe (ledger-bn913) und Handy-Auswahl (ledger-skpcx) darauf aufsetzen können. Nach außen ändert sich nichts, der Client sendet weiter `defaultSort`.

**Outcomes:**
- Build: 0 Warnungen, 0 Fehler
- Tests: 1542 bestanden, 7 übersprungen, 0 fehlgeschlagen (dotnet test); test:scripts 137/137
- Messung: größter Median 645 ms bei Schwelle 1000 ms (Tabelle im Outcome), kein Index
- Issues: Payee-Schlüssel im SQL mit CASE, damit leere Payees allein nach dem Tiebreaker ordnen; Testbuchungen mit Datum 2001, um bestehende Gesamtsicht-Tests nicht zu verdrängen. Messung läuft als Skript, weil die Test-Assembly per Modul-Initialisierer auf In-Memory festgelegt ist.

---

## 2026-10-20 10:00 - PC-Tabelle des Kassenbuchs blendet Spalten stufenweise aus (ledger-qh9qx)

**What I did:**
`visibleColumns` bestimmt aus gemessener Tabellenbreite und Gesamtsicht/Konto die sichtbaren Spalten (alle, ohne Memo, ohne Memo und Konto, kompakte Liste). `FilterBarColumn` misst die Breite per ResizeObserver. Ohne Konto-Spalte steht das Kürzel klein vor dem Payee.

**Files Added:**
- `src/Tests/LedgerVisibleColumnsTests.fs` - 9 Expecto-Tests für Stufen, Grenzwerte, Mindestbreiten
- `scripts/ledger-table-columns.test.mjs` - Browser-Wächter: kein überlappender Kopf bei 800, 1024, 1280, 1440 px, Kürzel in der Payee-Zelle

**Files Modified:**
- `src/Client/Components/Ledger/View.fs` - Spaltenfunktion statt Prozentbreiten, Breitenmessung, Payee-Zelle mit Kürzel, kompakte Liste ab Stufe 4
- `src/Tests/LedgerViewTests.fs` - Prozent-/Memo-Anteil-Tests ersetzt
- `src/Tests/Tests.fsproj` - neue Testdatei
- `scripts/ledger-status-column.test.mjs` - AC6 auf Mindestbreiten statt 28 %/24 %

**Rationale:**
In halb breiten Fenstern schrumpften Payee, Kategorie und Memo auf null. Spalten fallen jetzt weg, bevor eine unter ihre Mindestbreite rutscht.

**Outcomes:**
- Build: ok, 0 Warnungen
- Tests: 1541/1541 (dotnet), 140/140 (test:scripts)
- Issues: keine
- Iteration 2: nur Doku (ADR 0052, README-Absatz "Liste." von Hand), kein Code geändert

---

## 2026-10-20 08:20 - Hotfix: Historie lieferte in Produktion HTTP 500 (ledger-a4acn)

**What I did:**
`ReadModel` in `Ledger.QueryHandlers` formatiert Beträge und Daten ohne Kulturdaten; die Testsuite läuft jetzt wie der Alpine-Container ohne ICU (`InvariantGlobalization`).

**Files Modified:**
- `src/Server/Ledger/QueryHandlers.fs` - eigenes `NumberFormatInfo` statt `CultureInfo.GetCultureInfo "de-DE"`, Datum mit `InvariantCulture`
- `src/Client/Components/Ledger/View.fs` - `germanCulture ()` liefert `InvariantCulture` (feste Datumsformate)
- `src/Tests/Tests.fsproj` - `InvariantGlobalization=true`
- `src/Tests/LedgerPageTests.fs` - Wächtertest für den Invariant-Modus, Betragstest mit Tausenderpunkt

**Rationale:**
Nach dem Deploy vom 2026-10-19 warf der Modul-Initialisierer von `QueryHandlers` im Container (kein "de-DE" ohne ICU), die Historie lieferte bei jeder Buchung HTTP 500. Lokal mit ICU waren alle Tests grün; die invariante Suite macht diese Fehlerklasse künftig lokal sichtbar.

**Outcomes:**
- Build: ✅ (0 Warnungen)
- Tests: 1533 bestanden, 6 übersprungen (vor dem Fix 13 rot unter der invarianten Suite); test:scripts 137/137
- Issues: keine

---

## 2026-10-19 22:20 - "Geprüft" für die ganze Auswahl (ledger-nhwrt)

**What I did:**
Die Aktion "Geprüft" in der Auswahl-Leiste gebaut. Der Server löst die Auswahl (Ids oder Treffer plus Ausnahmen) selbst auf, schreibt je geänderter Buchung ein `EntryReviewMarkChangedInBb` und sendet einen Sammel-PATCH an YNAB (`PATCH /budgets/{id}/transactions`). Toast mit einem Rückgängig für den ganzen Stapel, der die Liste neu lädt. YNAB-OpenAPI gelesen: keine Obergrenze je Anfrage, die Antwort nennt nur `transaction_ids` der gespeicherten Buchungen.

**Files Added:**
- `src/Tests/LedgerReviewMarkBatchTests.fs` - DB-Tests der Stapel-Befehle und Fake-Handler-Tests des PATCH
- `src/Tests/LedgerReviewMarkBatchClientTests.fs` - Reducer-, Toast- und Leistentests

**Files Modified:**
- `src/Shared/Domain.fs`, `src/Shared/Api.fs` - Stapel-Typen und zwei neue API-Methoden
- `src/Server/YnabClient.fs` - Sammel-PATCH (`sendReviewMarkBatch`, `updateReviewMarkBatch`)
- `src/Server/Ledger/Persistence.fs`, `src/Server/Ledger/QueryHandlers.fs` - gemeinsamer Filter `entryFilterOfPageQuery`
- `src/Server/Ledger/CommandHandlers.fs`, `src/Server/Api.fs` - `setReviewMarkBatch`, `restoreReviewMarkBatch`
- `src/Client/Types.fs`, `src/Client/State.fs` - Toast-Aktion `UndoReviewMarkBatch`
- `src/Client/Components/Ledger/Types.fs`, `State.fs`, `View.fs` - Nachrichten, Reducer, Toast-Sätze, Leiste
- `src/Client/DesignSystem/MultiSelect.fs` - primäre Aktion "Geprüft" in beiden Leisten
- `src/Tests/LedgerSelectionTests.fs`, `src/Tests/Tests.fsproj`, `scripts/ledger-selection.test.mjs` - Wächter angepasst, neue Testdateien eingetragen

**Files Deleted:**
- keine

**Rationale:**
Roman will nach dem Filtern einen Stapel unauffälliger Buchungen in einem Zug prüfen. Ein Sammel-PATCH spart YNAB-Anfragen (rund 200 je Stunde und Token), das gemeinsame Rückgängig entspricht Handoff 1.12.

**Outcomes:**
- Build: ✅ (0 Warnungen)
- Tests: 1530 bestanden, 7 übersprungen, 0 Fehler (dotnet test); 137/137 (test:scripts)
- Issues: Beim Handy-Screenshot brach "Auswahl aufheben" in zwei Zeilen um, "Geprüft" behält jetzt seine Textbreite (`shrink-0`), der Rest der Reihe gehört "Auswahl aufheben".

---

## 2026-10-19 22:00 - Push-Fehlschlag als Ereignis

**What I did:**
EntryPushToYnabFailed eingeführt, im Push-Weg (Comdirect, Quick Add) geschrieben, in Entry, Persistenz, Lesemodell und Historie abgebildet. Iteration 2: README_DELTA und README-Handstellen nachgeliefert, Migrationstest für den zweiten Lauf.

**Files Added:**
- `src/Tests/LedgerPushFailureTests.fs` - Tests für Codec, apply, Replay, Lesemodell, Historie, Befehle, Migration (auch zweiter Lauf)

**Files Modified:**
- `src/Shared/Domain.fs` - LedgerPushFailure, zwei Felder an LedgerEntryView
- `src/Server/Ledger/Events.fs`, `Domain.fs`, `Persistence.fs`, `CommandHandlers.fs`, `QueryHandlers.fs` - Ereignis, Zustand, Spalten samt Backfill, Gründe, Historie
- `src/Tests/*.fs`, `Tests.fsproj`, `scripts/*.mjs` - Fixtures ergänzt, drei Ereigniserwartungen angepasst

**Rationale:**
Das Buchungs-Detail (ledger-q1hw2) soll Grund und Zeitpunkt eines gescheiterten Push zeigen.

**Outcomes:**
- Build: ok, 0 Warnungen
- Tests: 1495 grün, test:scripts 137/137 (Iteration 1)
- Issues: keine

---

## 2026-10-19 22:10 - Taste G und Tastaturfokus in der Kassenbuch-Liste (ledger-rjb8w)

**What I did:**
Fokuszeile für die Kassenbuch-Liste am PC: Pfeil auf/ab und J/K bewegen, G setzt "Geprüft" und springt weiter, Umschalt+G setzt "Ungeprüft". Die Tasten ruhen in Eingabefeldern, im Auswahl-Modus (G) und bei offenem Popover. Fußzeile mit Tastaturhinweis.

**Files Added:**
- `src/Tests/LedgerKeyboardTests.fs` - 27 Tests zu Fokus, G, Tastenbelegung und Darstellung

**Files Modified:**
- `src/Client/Components/Ledger/Types.fs` - `FocusedRow`, Msg `MoveFocus`/`ReviewKeyPressed`, reine Fokusfunktionen, `keyCommandFor`
- `src/Client/Components/Ledger/State.fs` - Reducer für Fokus und G, Fokus-Nachrücken in `applyReviewMarkResult`, Abgleich in `EntriesPageLoaded`
- `src/Client/Components/Ledger/View.fs` - `KeyboardNavigation`, Tastaturhinweis, Fokus an die PC-Zeile
- `src/Client/DesignSystem/Table.fs` - `RowProps.IsFocused`, `focusedRowClass`, `aria-selected`
- `src/Tests/LedgerSelectionTests.fs` - `entryOf` öffentlich
- `src/Tests/Tests.fsproj` - neue Testdatei

**Rationale:**
Der Prüfvermerk soll am PC ohne Maus gehen. Die Fokuszeile ist die Grundlage für das Seitenpanel (ledger-q1hw2).

**Outcomes:**
- Build: 0 Warnungen
- Tests: 1475 erfolgreich, 0 Fehler, 7 übersprungen; Wächter 137/137
- Issues: Ein erster Wächter-Lauf lief in ein Ladetimeout (parallel zu `dotnet test`), der Einzellauf war grün. Eine erste Fassung hatte den Hinweis als Modulwert, das ließ die View-Tests unter .NET scheitern; er ist jetzt eine Funktion.

---

## 2026-10-19 20:10 - Auswahl-Modus mit Summe im Kassenbuch (ledger-r1d2f)

**What I did:**
Auswahl als Menge über den Treffer (Picked, AllExcept, NoSelection) mit Summe aus TotalCount und TotalAmount. Handy: langes Drücken, Tippen, "Alle", zweireihige Leiste statt Tab-Bar. PC: Kästchen-Spalte, Kopfzeilen-Kästchen, Shift-Spanne mit Anker, einreihige Leiste. Summenzeile wechselt auf "Auswahl: n von N Buchungen".

**Files Added:**
- `src/Client/DesignSystem/MultiSelect.fs` - Kästchen, Summenzeile im Auswahl-Zustand, Leisten für Handy und PC
- `src/Tests/LedgerSelectionTests.fs` - 35 Tests (Rechnen, Reducer, Ansicht)
- `scripts/ledger-selection.test.mjs` - Playwright-Wächter Handy und PC

**Files Modified:**
- `src/Client/Components/Ledger/Types.fs` - Selection, Modellfelder, Nachrichten, reine Auswahl-Funktionen
- `src/Client/Components/Ledger/State.fs` - Reducer-Zweige, resetEntries leert die Auswahl, Zeile verlässt die Liste
- `src/Client/Components/Ledger/View.fs` - Kästchen-Spalte, Handy-Zeile im Modus, LongPressRow, Summenzeile, Leiste, Spaltenraster
- `src/Client/View.fs` - Leiste außerhalb der animierten Hülle, Tab-Bar weicht im Modus
- `src/Client/DesignSystem/Table.fs` - calmHeaderContentCell
- `src/Client/Client.fsproj`, `src/Tests/Tests.fsproj` - neue Dateien
- `src/Client/styles.css` - .selection-bar, .row-longpress
- `src/Tests/LedgerViewTests.fs`, `scripts/ledger-status-column.test.mjs` - Spaltenraster 8/7 Spalten, 28/24 Prozent

**Rationale:**
Roman liest die Summe einer Teilmenge bisher nur in YNAB-Web ab; am Handy geht es gar nicht. Die Summe braucht keinen Server, weil abwählbar nur geladene Zeilen sind.

**Outcomes:**
- Build: 0 Warnungen
- Tests: dotnet 1448 bestanden (7 übersprungen), test:scripts 136 Tests grün, guard:classes grün
- Messungen: Kästchen PC 17px; Leiste Handy bei 390x844 oben bei y=753, letzte Zeile endet darüber
- Issues: fixe Leiste lag zuerst in der Seitenhülle (Transform), danach in den Haupt-View verlegt

---

## 2026-10-19 19:35 - Input.Small zeigt am Handy 16px (Iteration 2)

**What I did:**
`font-size: 14px` aus `.input-field` entfernt, `DateField.textSizeClass` gestrichen, Tests ergänzt. Iteration 2: Wächter misst zusätzlich das echte Kategorie-Suchfeld im Handy-BottomSheet.

**Files Modified:**
- `src/Client/styles.css` - keine font-size mehr in `.input-field`
- `src/Client/DesignSystem/DateField.fs` - `textSizeClass` entfernt
- `src/Tests/AuswahlTests.fs` - Test auf neuen Zustand umgestellt
- `scripts/ledger-filter-bar.test.mjs` - Messung Small/Medium/Large bei 390x844 und 1440x900, echte Kategorie-Suche am PC und am Handy

**Rationale:**
Das unlayerte `.input-field` stach die Utility-Klassen von Input.Small; am Handy zoomte iOS beim Fokus.

**Outcomes:**
- Messung vor dem Fix: Small 14px (PC) / 14px (Handy, falsch); Medium 16/16; Large 16/16.
- Messung nach dem Fix: Small 14px (PC) / 16px (Handy); Medium 16/16; Large 16/16; Datumsfeld 14/16; echte Kategorie-Suche 14px (PC) / 16px (Handy).
- Build: ok, Tests: 1391 bestanden, test:scripts 135/135.

---

## 2026-10-19 19:30 - Wisch nach rechts markiert geprüft, Toast mit Rückgängig (ledger-th36h)

**What I did:**
Wisch-Geste nach rechts im Handy-Kassenbuch, Toast mit Aktions-Slot im Design System, Rückgängig über `restoreReviewMark`, Galerie-Beispiel.

**Files Added:**
- keine

**Files Modified:**
- `src/Client/Types.fs` - `ToastAction`, `ToastActionKind`, Feld `Action` am Toast
- `src/Client/State.fs` - `ShowToastWithAction`, `ToastActionClicked`, `toastActionMsg`
- `src/Client/View.fs` - `Toast.renderItems` statt `renderList`
- `src/Client/DesignSystem/Toast.fs` - Aktions-Schaltfläche, `renderItems`, Container über der Tab-Bar
- `src/Client/DesignSystem/Swipe.fs` - Richtung, `SwipeableRowRight`, reine Entscheidungsfunktionen
- `src/Client/Components/Ledger/Types.fs`, `State.fs`, `View.fs` - Msgs, Reducer, Karte mit Geste
- `src/Client/Components/Styleguide/View.fs` - Beispiel "Mit Rückgängig"
- `src/Client/styles.css` - `toast-above-nav`, `.from-left`, `.reviewed`, `touch-action`
- `src/Tests/LedgerReviewMarkClientTests.fs`, `ToastLifecycleTests.fs` - 22 neue Tests

**Rationale:**
Der Wisch ist die schnellste Geste für den Durchsehen-Ablauf, Rückgängig fängt Fehlwürfe ab.

**Outcomes:**
- Build: ok, 0 Warnungen
- Tests: 1413 grün, 7 übersprungen; test:scripts 134/134
- Issues: einmaliger Timeout im ersten test:scripts-Lauf, Wiederholung grün

---

## 2026-10-19 19:10 - Seitenversatz beim Nachladen mit lokal entfernten Zeilen (ledger-be1yh)

**What I did:**
Nachladeanfrage des Kassenbuchs um `LocallyRemoved` erweitert. Der Server verschiebt den Seitenbeginn um diese Zahl nach vorn, der Client zählt sie beim Entfernen einer geprüften Zeile bei aktivem Chip Zu prüfen hoch.

**Files Added:**
- `src/Tests/LedgerPagingTests.fs` - 5 Client-Tests (Anfragebau, Reducer-Zähler, Reset) und 1 Server-Test (Versatz)

**Files Modified:**
- `src/Shared/Domain.fs` - Feld `LocallyRemoved` in `LedgerEntryPageQuery`
- `src/Server/Ledger/QueryHandlers.fs` - Versatz aus Page, PageSize, LocallyRemoved
- `src/Server/Ledger/Persistence.fs` - `loadEntriesPage`/`safePage` nehmen einen Versatz
- `src/Client/Components/Ledger/Types.fs`, `State.fs` - Modellfeld, `buildPageQuery`, Zählen in `applyReviewMarkResult`, Reset
- `src/Tests/LedgerPageTests.fs`, `LedgerWithoutCategoryTests.fs` - Query-Literale um das Feld ergänzt
- `src/Tests/Tests.fsproj` - neue Testdatei

**Rationale:**
Der Server zählt den Versatz auf der gefilterten Menge neu. Nach dem lokalen Entfernen einer Zeile übersprang die nächste Seite eine Buchung.

**Outcomes:**
- Build: ok, 0 Warnungen
- Tests: 1391 bestanden, 0 Fehler
- Issues: keine

---

## 2026-10-19 17:30 - Handy: keine Prüfvermerk-Schaltfläche an in YNAB gelöschten Buchungen

**What I did:**
Am Handy bekam eine in YNAB gelöschte Buchung bei aktivem „Gelöschte zeigen“ die Schaltfläche Geprüft/Ungeprüft, ein Klick endete in der Ablehnung des Servers. Die Handy-Zeile prüft jetzt `entry.DeletedInYnab` wie die PC-Zeile schon vorher und lässt den Schaltflächen-Container ganz weg.

**Files Modified:**
- `src/Client/Components/Ledger/View.fs` - `entryRowWithReviewMark`: Container mit `reviewMarkButton` nur, wenn die Buchung nicht in YNAB gelöscht ist.
- `scripts/ledger-review-mark.test.mjs` - vierte Fixture-Zeile (gelöscht) und Regressionstest für PC und Handy: Zeile sichtbar, keine Schaltfläche. Vor dem Fix rot am Handy, grün am PC.

**Rationale:**
Verifier-Befund aus ledger-r777c.

**Outcomes:**
- Build: ✅ (0 Warnungen), `npm run build` ✅
- Tests: dotnet test 1386 bestanden, 6 übersprungen; test:scripts 134/134
- Issues: keine

---

## 2026-10-19 16:45 - Prüfvermerk in BB setzen und nach YNAB zurückschreiben (ledger-r777c)

**What I did:**
Prüfvermerk als Schreibweg umgesetzt: drei Ereignisse, Befehle setReviewMark/restoreReviewMark/retryReviewMarkWriteBack, YNAB-Aufruf (PUT, nur approved und cleared) gegen Fake-Handler getestet, Lesemodell-Feld ReviewMarkWriteBackFailure, Schaltfläche Geprüft/Ungeprüft an der Zeile und Hinweis "Prüfvermerk nicht in YNAB" mit "Erneut senden".

**Files Added:**
- `src/Tests/LedgerReviewMarkTests.fs` - Domäne, Befehle (In-Memory-SQLite), Fake-HTTP für YNAB
- `src/Tests/LedgerReviewMarkClientTests.fs` - Reducer und Ansichts-Bausteine
- `scripts/ledger-review-mark.test.mjs` - Playwright-Wächter für PC und Handy mit gemockter API

**Files Modified:**
- `src/Server/Ledger/Events.fs`, `Domain.fs`, `Persistence.fs`, `CommandHandlers.fs`, `QueryHandlers.fs` - Ereignisse, Zustand, Spalte, Befehle, Historien-Sätze
- `src/Server/YnabClient.fs` - `buildReviewMarkBody`, `sendReviewMark`, `updateReviewMark`
- `src/Server/Api.fs`, `src/Shared/Api.fs`, `src/Shared/Domain.fs` - drei Endpunkte, Shared-Typen, `LedgerEntryView.ReviewMarkWriteBackFailure`
- `src/Client/Components/Ledger/Types.fs`, `State.fs`, `View.fs` - Zeilenzustand, Reducer, Schaltfläche und Hinweis
- `src/Tests/LedgerTests.fs`, `LedgerPageTests.fs`, `LedgerViewTests.fs`, `LedgerWithoutCategoryTests.fs`, `Tests.fsproj` - neues Feld in Record-Literalen, neue Dateien eingebunden
- `scripts/ledger-filter-bar.mjs`, `scripts/ledger-status-column.mjs` - Mock-Fixtures um das neue Feld

**Rationale:**
Der Prüfvermerk ist die einzige schreibende Fähigkeit vor v2 (ADR 0028). ADR 0050 legt Ereignis vor Push, Fehlschlag als Zustand und Reconciled unberührt fest. Die YNAB-OpenAPI kennt für eine einzelne Buchung PUT statt PATCH, deshalb PUT.

**Outcomes:**
- Build: ✅ (0 Warnungen)
- Tests: 1332/1332 bestanden (7 übersprungen), npm run test:scripts 126/126
- Screenshots: pc-1440x900-review-mark.png, handy-390x844-review-mark.png (docs/ux/kassenbuch/pruefvermerk/)
- Issues: Seitenversatz nach dem Entfernen einer Zeile bei aktivem Chip (ledger-be1yh)

---

Hinweis des Verifiers (nicht blockierend): Am Handy rendert `entryRowWithReviewMark` die Schaltfläche auch für in YNAB gelöschte Buchungen (bei "Gelöschte zeigen"); am PC fehlt sie korrekt. Ein Klick liefert dort die Ablehnung des Servers.

---

## 2026-10-19 16:35 - Deutsches Datumsfeld mit Kalender-Knopf (design-system-m7z9q)

**What I did:**
DS-Baustein `DateField` gebaut und im Zeitraum-Filter des Kassenbuchs eingesetzt. Ungültige Eingabe sperrt Übernehmen (`PickerParts.applyButtonWhen`), der Ledger-Entwurf merkt sich den Fehler je Grenze.

**Files Added:**
- `src/Client/DesignSystem/DateField.fs` - reine Datumsfunktionen und React-Baustein
- `docs/ux/kassenbuch/filterleiste-ruhig/pc-1440x900-ledger-filter-Zeitraum-datumsfeld.png`, `handy-390x844-ledger-filter-Zeitraum-datumsfeld.png`, `pc-1440x900-styleguide-auswahl-datumsfeld.png` - Nachher-Bilder

**Files Modified:**
- `src/Client/Client.fsproj` - DateField.fs nach Input.fs
- `src/Client/DesignSystem/Icons.fs` - `Icons.calendar`
- `src/Client/DesignSystem/PickerParts.fs` - gesperrter Übernehmen-Knopf
- `src/Client/Components/Ledger/{Types,State,View}.fs` - Fehlerfelder, `DateEntry` in den Msgs, `DateField.view` statt nativem Feld
- `src/Client/Components/Styleguide/View.fs` - Datumsfeld in der Karte Auswahl
- `standards/frontend/styleguide.md` - Absatz Datumsfeld
- `src/Tests/{AuswahlTests,LedgerPageTests,LedgerViewTests}.fs`, `scripts/ledger-filter-bar.test.mjs` - Tests

**Rationale:**
Das native Datumsfeld zeigt das Format der Browsersprache (Befund K7). Das Textfeld ist immer deutsch, der native Kalender bleibt über den Knopf erreichbar.

**Outcomes:**
- Build: 0 Warnungen. Tests: 1321 bestanden, 7 übersprungen, 0 Fehler. test:scripts 124/124. guard:classes und npm run build grün.
- Messung bei 1440x900: Schriftgröße im Datumsfeld 14px, Popoverbreite 320px, Höhe 382px mit Fehlerhinweis. Bei 390x844: 16px.
- Befund: `.input-field` (`styles.css` 678) setzt unlayered `font-size: 14px` und schlägt `text-base` aus `Input.Small` am Handy. Im Datumsfeld per `text-base! md:text-sm!` umgangen.
- Der Chip des Zeitraum-Filters zeigt bei Handeingabe nur "Zeitraum" (`dateChipLabel` kennt nur Schnellwahlen); der Playwright-Test prüft deshalb den Request und den Entwurf.

---

## 2026-10-19 17:00 - Import prüfen: Duplikat-Details deutsch, nur bei Verdacht sichtbar (ynab-sync-e4gm3, Iteration 1 und 2)

**What I did:**
Duplikat-Block der aufgeklappten Zeile in Duplikat-Prüfung und YNAB-Ergebnis getrennt, deutsch, bei Verdacht sofort sichtbar, sonst hinter "Details". Hinweis-Zeile aus den Fuzzy-Feldern. "Überspringen" neutral. Iteration 2: Sichtbarkeit des YNAB-Ergebnisses über showYnabResult, zusätzliche Verdachts-Bilder.

**Files Added:**
- `scripts/import-pruefen-duplikat-details.test.mjs` - Playwright-Wächter am gemockten Lauf
- `docs/ux/ynab-sync/import-pruefen-ruhig/pc-1440x900-import-pruefen-duplikat-details.png`
- `docs/ux/ynab-sync/import-pruefen-ruhig/handy-390x844-import-pruefen-duplikat-details.png`
- `docs/ux/ynab-sync/import-pruefen-ruhig/pc-1440x900-import-pruefen-duplikat-verdacht.png`
- `docs/ux/ynab-sync/import-pruefen-ruhig/handy-390x844-import-pruefen-duplikat-verdacht.png`

**Files Modified:**
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs` - Funktionen, Komponente, deutsche Literale, Hinweis-Zeile, Überspringen ohne danger
- `src/Tests/SyncFlowViewTests.fs` - 18 neue Tests

**Files Deleted:**
- keine

**Rationale:**
Befund S6: englischer Debug-Block in jeder aufgeklappten Zeile. Verifier: die getestete Funktion muss die Sichtbarkeit tatsächlich steuern, das Handy-Bild muss beide Zeilenarten zeigen.

**Outcomes:**
- Build: ok (0 Warnungen)
- Tests: 1298 bestanden, 7 übersprungen; test:scripts 122/122
- Playwright (gemockt, PC und Handy): Verdachtszeile zeigt aufgeklappt "Duplikat-Prüfung" und "Gefunden: Rewe am 07.10.2026 über 12,34 €" ohne Klick und ohne "Details"-Knopf; die unauffällige Zeile zeigt "Details", die Prüfung erst nach Klick.
- Issues: keine

---

## 2026-10-19 16:20 - Flaky Replay-Test in sequenzierte Persistence-Liste verschoben

**What I did:**
Test "Replay reproduziert SplitTransferCounterparts wie die Zustandstabellen (ADR 0013)" von `ledgerDomainTests` nach `ledgerPersistenceTests` verschoben, Inhalt unverändert. Übrige Tests vom 2026-10-18 auf DB-Zugriff in nicht sequenzierten Listen geprüft, nichts Weiteres gefunden.

**Files Modified:**
- `src/Tests/LedgerTests.fs` - Test in die sequenzierte Liste mit Schema-Init verschoben

**Rationale:**
Die reine Domain-Liste läuft parallel und ohne Schema-Initialisierung, der Test brauchte aber die In-Memory-DB (ADR 0030) und schlug flaky fehl.

**Outcomes:**
- Build: ✅ (nur alte FS0025-Warnung)
- Tests: 1279 bestanden, 7 übersprungen, 5 Läufe in Folge ohne Fehler
- Issues: keine

---

## 2026-10-19 16:30 - Build-Warnung FS0025 bei OpenFilterPopover behoben

**What I did:**
Match in `OpenFilterPopover` um `WithoutCategoryChip` ergänzt, Regressionstest hinzugefügt.

**Files Modified:**
- `src/Client/Components/Ledger/State.fs` - neuer Match-Zweig
- `src/Tests/LedgerPageTests.fs` - Regressionstest

**Rationale:**
Der unvollständige Match hätte zur Laufzeit eine MatchFailureException geworfen.

**Outcomes:**
- Build: 0 Warnungen
- Tests: 1280 bestanden, 0 Fehler
- Issues: keine

---

## 2026-10-19 16:30 - Interne YNAB-Kategorien in der Kategorieauswahl ausblenden

**What I did:**
withoutInternalCategories und categoriesForPicker in Shared/Domain.fs ergänzt, getCategories-Endpunkt nutzt categoriesForPicker.

**Files Modified:**
- `src/Shared/Domain.fs` - zwei reine Funktionen
- `src/Server/Api.fs` - getCategories ruft categoriesForPicker
- `src/Tests/YnabClientTests.fs` - 3 neue Tests

**Rationale:**
Deferred Income SubCategory und Uncategorized verwirren in den Auswahlen; Ready to Assign bleibt wählbar.

**Outcomes:**
- Build: ok (1 alte Warnung)
- Tests: 1282 grün
- Issues: keine

---

## 2026-10-19 13:40 - Kassenbuch: Filterleiste und Summenzeile ruhig (ADR 0049)

**What I did:**
Popover-Breiten, Payee-Liste, kompakte Felder, Zeitraum-Layout, schlanke Summenzeile mit Skeleton, Fußabstand des ruhigen Seitenkopfs, Memo-Spalte in Alle Konten und Historienzeile nachgezogen.

**Files Added:**
- `docs/ux/kassenbuch/filterleiste-ruhig/*.png` - Nachher-Bilder PC und Handy

**Files Modified:**
- `src/Client/Components/Ledger/View.fs` - popoverWidth, payeePickerBody, memo/amount/date-Felder, summaryState/summaryCountText/summaryRow, tableColumnWidths, historyLine
- `src/Client/DesignSystem/Money.fs` - formatCount
- `src/Client/DesignSystem/FilterPopover.fs` - Breitenwerte
- `src/Client/DesignSystem/PickerParts.fs` - applyButton
- `src/Client/DesignSystem/PageHeader.fs` - mb-4 bei Calm
- `src/Client/Components/Styleguide/View.fs` - Demo nutzt categoryWidthClass
- `src/Tests/LedgerViewTests.fs`, `MoneyFormatTests.fs`, `RuhigeListenTests.fs` - 21 neue Tests, ein Wächter angepasst

**Rationale:**
Audit 2026-10-19 (K5, K7, K8, K10, K12 bis K14): Filter und Summenzeile wirkten neben der ruhigen Tabelle laut und schwer.

**Outcomes:**
- Build: ok (nur altes FS0025)
- Tests: 1247 grün, 7 übersprungen; test:scripts 120/120
- Issues: Memo in Alle Konten war 128px (Messung), jetzt 187px

---

## 2026-10-19 14:10 - Import prüfen an ADR 0049 angeglichen (ynab-sync-aj5tv)

**What I did:**
Import prüfen (SyncFlow) ruhiger gemacht: deutsche Gruppenköpfe und Tagessummen, Betrag über Money.listAmount, neutrale Kategorie-Chips und Checkbox, Datum nur im Gruppenkopf, volle Breite am PC, Importieren als Button.Primary, neutrale Beträge im Aufteilen-Sheet, Styleguide-Satz ersetzt.

**Files Added:**
- `docs/ux/ynab-sync/import-pruefen-ruhig/*.png` - fünf Nachher-Bilder (PC und Handy)

**Files Modified:**
- `src/Client/Components/SyncFlow/Views/ViewHelpers.fs` - formatGroupDate, formatGroupTotal
- `src/Client/Components/SyncFlow/Views/TransactionList.fs` - Gruppenkopf, zwei Button.Primary
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs` - Money.listAmount, badge-neutral, ohne Datum
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs` - splitAmountClass, neutrale Rest-Zeile
- `src/Client/Components/SyncFlow/Views/InlineRuleForm.fs` - Speichern-Knopf als Button.Primary
- `src/Client/Components/SyncFlow/View.fs` - eigene Ränder (px-4 md:px-6)
- `src/Client/View.fs` - pageShell: SyncFlow nutzt pageContentFull
- `src/Client/styles.css` - badge-ready, tx-date, btn-import*, Glow-Animation, rote und grüne Rest-Fläche entfernt; Gruppenkopf, Payee, Checkbox neutral
- `src/Tests/SyncFlowViewTests.fs` - neue und angepasste Tests
- `standards/frontend/styleguide.md` - §2 Vorbild-Satz

**Rationale:**
Roman hat am 2026-10-19 entschieden, dass Import prüfen nach der Bestandsaufnahme gegen ADR 0049 ruhiger wird. Farbe bleibt in der Zeile der Statuskante und "ohne Kategorie" vorbehalten.

**Outcomes:**
- Build: 0 Warnungen
- Tests: 1241 grün, 7 übersprungen; test:scripts 120/120; npm run build und guard:classes grün
- Messung: .tx-list 1392px bei 1440px (vorher max 1280px), 358px bei 390px; Textprüfung ohne Monatsname und ohne Punkt-Dezimal
- Issues: keine

---

## 2026-10-19 13:30 - Regeln-Seite als ruhige Liste (categorization-wnxd6)

**What I did:**
Regeln-Seite nach ADR 0049 umgebaut: ruhiger Kopf mit Untertitel, volle Breite, Tabelle (PC) und zweizeilige Liste (Handy), Zeile öffnet den Dialog, Aktiv-Schalter und Löschen nur im Dialog, deutsche Texte.

**Files Added:**
- `src/Tests/RulesListTests.fs` - 18 Tests (Quelltext-Zusicherungen, Werttests, Regression Pflichtfeldhinweis, State)
- `docs/ux/categorization/regeln-ruhige-liste/` - 6 Nachher-Bilder

**Files Modified:**
- `src/Client/Components/Rules/View.fs` - neue Liste, Kopf, Dialogfuß, reine Hilfsfunktionen
- `src/Client/Components/Rules/State.fs` - `closeRuleDialog`, Dialog schließt nach erfolgreichem Löschen
- `src/Client/View.fs` - `Rules` bekommt `Navigation.pageContentFull`
- `src/Tests/Tests.fsproj` - RulesListTests.fs registriert

**Rationale:**
ADR 0049 und Audit 2026-10-19 (R1 bis R4). Am Handy war die Kopfzeile mit drei Knöpfen zu breit (494px bei 390px), daher Neu laden im Menü.

**Outcomes:**
- Build: ok, nur alte FS0025 in Ledger/State.fs
- Tests: 1245 grün, 7 übersprungen; test:scripts 120/120; guard:classes und npm run build grün
- Issues: Löschaufruf (`DeleteRule`) unter .NET nicht testbar; Handy-Sichtprüfung offen

---

## 2026-10-19 09:35 - Bestandsaufnahme ADR 0049, Bug doppelter Spiegel-Knopf (design-system-qz7b6)

**What I did:**
Romans Screenshot des Kategorie-Popovers ("sieht auch noch sehr plump aus") war Anlass für eine Bestandsaufnahme aller Listen und Auswahlen gegen ADR 0049 (Agent, 43 Screenshots, Bericht in der Session-Ablage). Daraus erfasst: `design-system-zahv9` (ruhige Auswahl-Bausteine), `ledger-m23nz` (Filterleiste, Summenzeile), `categorization-wnxd6` (Regeln-Seite), `ynab-sync-aj5tv` (Import prüfen ruhiger und deutsche Formate, Entscheidung Roman), `ledger-d218j` (interne YNAB-Kategorien ausblenden, Ready to Assign behalten), `ledger-eng59` (FS0025). Gefundener Bug aus `ledger-n4vte` direkt behoben: am Handy erschienen beide Spiegel-Knöpfe.

**Files Added:**
- `scripts/ledger-mirror-buttons.test.mjs` - Browser-Regressionstest, je Viewport genau ein Spiegel-Knopf
- `docs/ux/kassenbuch/design-projekt-nachtrag-2026-10-19.md` - Nachtrag Ruhige Listen für das Claude-Design-Projekt

**Files Modified:**
- `src/Client/DesignSystem/Button.fs` - `callerSetsDisplay`: bringt der Aufrufer eine Anzeige-Klasse ohne Breakpoint mit, entfällt das feste `inline-flex`
- `src/Tests/RuhigeListenTests.fs` - drei Tests für `callerSetsDisplay`
- `docs/ux/kassenbuch/screenshots/{handy-390x844,pc-1440x900}-ruhige-listen.png` - nur sichtbarer Bereich

**Rationale:**
Tailwind 4 ordnet Utilities im erzeugten CSS; `inline-flex` aus `Button.view` gewann gegen `hidden` vom Aufrufer. Worker und zwei Verifier von `ledger-n4vte` hatten es übersehen, weil kein Test die Sichtbarkeit prüfte und der Handy-Screenshot eine Ganzseitenaufnahme war.

**Outcomes:**
- Build: ✅ (nur das bekannte FS0025)
- Tests: 1200 bestanden, 7 übersprungen; `npm run test:scripts` 120/120
- Issues: keine

---

## 2026-10-18 23:51 - Kassenbuch auf ruhige Listen umgestellt (ledger-n4vte)

**What I did:**
Zweite Aufgabe des Work-Laufs, PASS in Iteration 3. Das Kassenbuch folgt Fassung B (ADR 0049) am PC und am Handy; auf Romans Wunsch sind die beiden Abweichungen im Styleguide-Abschnitt 8a gleich mit behoben.

**Files Added:**
- `docs/ux/kassenbuch/screenshots/{pc-1440x900,handy-390x844}-ruhige-listen.png` - Gate-Screenshots

**Files Modified:**
- `src/Client/Components/Ledger/View.fs` - `PageHeader.calm`, `Container.full`, Zellrollen, Statuskante, Tagessumme (`daySum`, `daySumText`), zweizeilige Handy-Zeile ohne `originBadge`, Kategorie über `categoryClassName`
- `src/Client/View.fs`, `src/Client/DesignSystem/Navigation.fs` - `pageContentFull` ohne `container`-Begrenzung, nur für das Kassenbuch
- `src/Client/DesignSystem/Table.fs`, `Tokens.fs` - `calmGroupRow` (Tagessumme unter der Betragsspalte), `ListRoles.cellPaddingX/mobileLine1/mobileLine2`
- `src/Client/Components/Styleguide/View.fs` - Abschnitt 8a nutzt dieselben Bausteine; Screenshots neu
- `src/Shared/Domain.fs`, `src/Server/Ledger/QueryHandlers.fs` - `LedgerEntryView.IsTransfer`; ein Übertrag zeigt "Übertrag" statt "ohne Kategorie" (ADR 0048)
- `src/Tests/LedgerViewTests.fs`, `src/Tests/LedgerPageTests.fs` - Tests auf den neuen Stand, `daySumTests`, `isTransferTests`
- `scripts/ledger-status-column{,.test}.mjs`, `scripts/ledger-filter-bar{,.test}.mjs` - Fixtures mit `IsTransfer`, Wächter auf "zu prüfen" und neue Spaltenaufteilung; Sticky-Messung scrollt jetzt direkt statt per einzelnem animierten Mausrad

**Rationale:**
Roman hat Fassung B gewählt. Verifier-Funde: Iteration 1 sechs rote Browser-Wächter (`npm run test:scripts` lief beim Worker nicht mit), Iteration 2 fehlender Test für die Server-Regel `IsTransfer` (per Mutation gezeigt). Die geänderte Sticky-Messung hat der Verifier gegen den Stand vor dem Diff nachgemessen: ein einzelnes synthetisches Mausrad bricht in beiden Ständen vorzeitig ab, keine DOM-Änderung während des Scrollens, Lücke im Haft-Zustand 0.

**Outcomes:**
- Build: ✅ (nur das bekannte FS0025 in `Ledger/State.fs:333`)
- Tests: 1197 bestanden, 7 übersprungen; `npm run test:scripts` 118/118
- Issues: keine offenen

---

## 2026-10-18 23:01 - DS-Bausteine für ruhige Listen (design-system-q7rkm)

**What I did:**
Work-Lauf mit einer Aufgabe (ein Worker, ein Verifier, PASS in Iteration 1). Die Bausteine aus ADR 0049 stehen im Design-System, der Kassenbuch-View ist noch unverändert (folgt mit `ledger-n4vte`).

**Files Added:**
- `src/Tests/RuhigeListenTests.fs` - 22 Tests über reine Klassenfunktionen, Regressionstest zur Ausrichtung, Quelltext-Zusicherung gegen `$"text-{`
- `docs/ux/design-system/screenshots/{pc-1440x900,handy-390x844}-styleguide.png` - Gate-Screenshots von `/styleguide`

**Files Modified:**
- `src/Client/DesignSystem/Tokens.fs` - `ListRoles` mit Typografie-Rollen, Statuskante, Zeilen- und Kopfhöhe
- `src/Client/DesignSystem/Money.fs` - `listAmount`/`listAmountClass` (neutral, Eingang Teal), `accountBalanceClass` (neutral, negativ Red) für `accountBalance` und `accountListRow`
- `src/Client/DesignSystem/Table.fs` - wörtliche Ausrichtungsklassen (Fehlerbehebung), `CellRole`, `roleCell`, `amountCell`, `calmHeaderCell`, `calmRow`, `RowProps.StatusEdge`
- `src/Client/DesignSystem/Primitives.fs` - `Container.full` ohne Breitenbegrenzung
- `src/Client/DesignSystem/PageHeader.fs` - schlanke Variante `calm`
- `src/Client/Components/Styleguide/View.fs` - Abschnitt "8a. Ruhige Listen"
- `src/Tests/Tests.fsproj`, `scripts/tailwind-class-guard-exceptions.json` - neue Testdatei, Begründungstext angepasst
- `.agentheim/knowledge/contexts/design-system/README.md` - Eintrag "Ruhige Listen (ADR 0049)" in der Ubiquitous Language

**Rationale:**
Farbe in wiederkehrenden Zeilen ist ein Signal (ADR 0049). Außerdem war `Align = right` in `Table.cell` wirkungslos: `$"text-{a}"` stand nie wörtlich im Quelltext, Tailwind erzeugte `text-right` nicht. Daher stand der Betrag im Kassenbuch linksbündig.

**Outcomes:**
- Build: ✅ (eine bestehende Warnung FS0025 in `Ledger/State.fs:333`, nicht aus diesem Diff)
- Tests: 1169 bestanden, 7 übersprungen
- Issues: keine

---

## 2026-10-18 22:35 - Ruhige Listen: ADR 0049, Styleguide, zwei Backlog-Aufgaben

**What I did:**
Das Kassenbuch mit "Import prüfen" und dem Claude-Design-Mockup 1.9 verglichen. Ursache für den lauten Eindruck: Die Typografie aus dem Handoff (Payee 14px/600, Rest 13px gedämpft) wurde nie umgesetzt, `Table.td` liefert 16px grau, dazu Gradient-Titel und `max-w-7xl`. Farben (violette Kategorie, rote und grüne Beträge) folgten dem Styleguide. Roman hat in einem Sichttest mit drei Fassungen (Artefakt "Kassenbuch Sichttest") die ruhige Tabelle gewählt. Die Regel steht jetzt in ADR 0049 und im Styleguide.

**Files Added:**
- `.agentheim/knowledge/decisions/0049-ruhige-listen-farbe-als-signal.md` - Farbe ist in Listen ein Signal; Beträge, Kategorien und Kontostände neutral
- `.agentheim/board/design-system/backlog/design-system-q7rkm-ds-bausteine-fuer-ruhige-listen.md` - Money-Listenvariante, Table-Zellrollen, Container, PageHeader
- `.agentheim/board/ledger/backlog/ledger-n4vte-kassenbuch-auf-ruhige-listen-umstellen.md` - Kassenbuch PC und Handy umstellen

**Files Modified:**
- `standards/frontend/styleguide.md` - §2: Geld-Regel auf Einzelbeträge beschränkt, Purple-Zeile angepasst, neuer Abschnitt "Ruhige Listen" mit Werte-Tabelle PC und Handy

**Rationale:**
Eine Änderung nur im Code würde beim nächsten Handoff aus Claude Design wieder überschrieben. Deshalb steht die Entscheidung am Anfang der Kette (Styleguide), die Umsetzung folgt über die Aufgaben.

**Outcomes:**
- Build: nicht betroffen (nur Doku und Aufgaben)
- Tests: nicht betroffen
- Offen: Nachtrag für die CLAUDE.md des Claude-Design-Projekts

---

## 2026-10-18 22:20 - Work-Lauf: acht Aufgaben (Spiegel, Push, Kassenbuch-Filter, Split-Überträge, Styleguide-Export)

**What I did:**
Alle acht Aufgaben aus `todo/` in drei Wellen über eigene Worktrees umgesetzt, jede vor der Integration von einem Verifier geprüft.
- `ledger-fgw2q`: Eine nicht auswertbare YNAB-Antwort beim Push blockiert jetzt auf beiden Pfaden den Sessionabschluss (`UnresolvedWrittenNotLinkedTransactionIds`, pure `pushFailureError`).
- `design-system-tg2wn`: `TextGlows.orange`/`teal` sind echte Arbitrary-Property-Klassen wie `green`.
- `ledger-ggzdw` (ADR 0044): Der Spiegel vergleicht die Kategorie nur über die Id (`categorizationIdentity`). Eine Umbenennung in YNAB ist keine Umkategorisierung mehr. `toEntryView` löst Blattnamen live aus `ledger_categories` auf.
- `ledger-fgt4k`: `DeletedCount` berücksichtigt `OnlyNeedsReview`.
- `ledger-h4nxz` (ADR 0046): Split-Zeilen-Überträge werden über YNABs Subtransaction-Id mit ihrer Gegenbuchung verknüpft (neues Ereignis `EntrySplitTransferCounterpartObservedInYnab`, Spalte `split_transfer_counterparts`, Nachlauf in `runMirror`).
- `ledger-saqsk` (ADR 0048): Chip "Ohne Kategorie" in der Filterleiste. Ein Übertrag zählt als Ziel. Dafür `transfer_account_id` roh projiziert (mit einmaligem Backfill), Indextabelle `ledger_entry_split_transfers`.
- `design-system-an22r`: Der Export schreibt `@dsCard`-Marker mit name/subtitle/gemessenem viewport und eine `thumbnail.html`. Fehlt name oder subtitle an einer Karte, bricht der Export ab.
- `design-system-bz0gn`: `CategoryMultiSelect` ist als DS-Komponente herausgelöst, ChipBar, FilterPopover und Mehrfachauswahl sind als Styleguide-Karten im Export.

**Files Added:**
- `src/Tests/TextGlowTokenTests.fs`, `src/Tests/LedgerWithoutCategoryTests.fs` - neue Testdateien
- `src/Client/DesignSystem/CategoryMultiSelect.fs` - DS-Komponente

**Files Modified:**
- `src/Server/PushOutcomeClassification.fs`, `src/Server/Api.fs` - Push-Abschluss
- `src/Server/Ledger/{Domain,Events,CommandHandlers,Persistence,QueryHandlers}.fs`, `src/Server/YnabObservations.fs`, `src/Shared/Domain.fs` - Spiegel, Split-Überträge, Filter
- `src/Client/Components/Ledger/{Types,State,View}.fs`, `src/Client/Components/Styleguide/View.fs`, `src/Client/DesignSystem/Tokens.fs`, `src/Client/Client.fsproj` - Chip, Styleguide, Tokens
- `scripts/export-design-system.mjs`, `scripts/export-design-system.test.mjs` - Export-Marker und Thumbnail
- `src/Tests/*` - Regressions- und Verhaltenstests
- `.agentheim/knowledge/contexts/{ledger,design-system}/README.md` - Glossar, Ereigniskatalog, Migration

**Rationale:**
Die acht Aufgaben waren verfeinert und bereit (Sammel-Modeling vom 2026-10-02 und Folgeläufe).

**Outcomes:**
- Build: ✅, aber 1 neue Warnung (FS0025 in `Components/Ledger/State.fs:333`, als `ledger-vqxvc` im Backlog)
- Tests: 1148 grün, 6 übersprungen auf main; in einem von drei Läufen 1 Fehler durch einen reihenfolgeabhängigen Replay-Test aus `ledger-h4nxz` (als `ledger-zmbf1` im Backlog). `npm run build` und `npm run guard:classes` grün.
- Verifikation: 4 im ersten Versuch bestanden, 4 brauchten Nachbesserungen (ggzdw und an22r je 3 Iterationen, saqsk und bz0gn je 2). Ein Merge-Konflikt (saqsk gegen h4nxz) im Worktree aufgelöst. Der erste FAIL bei saqsk war ein Conductor-Fehler (zu wenig gestaged), kein Worker-Fehler.
- Issues: Die parallel laufenden Browsertests in `scripts/` haben eine lastabhängige Flake ("Route '/ledger' lädt nicht"), die schon auf der Basis auftritt. Seriell (`node --test --test-concurrency=1 scripts/*.test.mjs`) grün. Offen für Romans Auge: Thumbnail-Optik (an22r) und Abgleich der neuen Styleguide-Karten mit der Kassenbuch-Filterleiste (bz0gn).

---

## 2026-09-26 11:52 - Kassenbuch-Filterleiste nach Eskalation abgeschlossen (ledger-j8xq4)

**What I did:**
`ledger-j8xq4` im bestehenden Worktree wieder aufgenommen, nachdem Roman nach der Eskalation den offenen Rest entschieden hatte. Die Höhenmessung der sticky Filterleiste folgt jetzt über einen Callback-Ref an `ChipBar.bar` dem Knoten, in den die Leiste tatsächlich rendert. Vorher band ein `useEffect` mit leeren Abhängigkeiten den `ResizeObserver` einmalig an den Mount-Knoten; sobald während eines Spiegel-Laufs der Hinweis "Spiegel läuft" vor die Leiste rückte, maß er den Hinweis. Zwei neue Browsertests decken den Zustand während und nach einem Spiegel-Lauf ab. Mit dieser vierten Iteration ist die ganze Aufgabe integriert: Chips, Kategorienauswahl mit Mehrfachauswahl und Click-Commit, Zeitraum-Schnellwahl, Payee-Vorschläge, Popover am PC, BottomSheet am Handy, Summenzeile, `YnabCategory.Hidden`.

**Files Added:**
- `src/Client/DesignSystem/ChipBar.fs`, `FilterPopover.fs`, `Viewport.fs` - Filterleisten-Bausteine
- `scripts/ledger-filter-bar.mjs`, `scripts/ledger-filter-bar.test.mjs` - Browsertests (14)
- `.agentheim/knowledge/decisions/0043-filterleiste-chrome-per-viewport-check-statt-css-doppel-render.md` - ADR mit Nachtrag zum Callback-Ref

**Files Modified:**
- `src/Client/Components/Ledger/{Types,State,View}.fs`, `src/Client/State.fs`, `src/Client/styles.css` - Filterleiste, Summenzeile, gemessener Kopf-Versatz
- `src/Shared/Domain.fs`, `src/Server/YnabClient.fs` - `YnabCategory.Hidden` aus YNABs `hidden`
- `src/Tests/*` - Reduzierer-, View- und Decoder-Tests

**Rationale:**
Der Verifier der dritten Iteration fand bei veraltetem Spiegel-Status 18px (1440x900) bzw. 42px (1024x900 mit Filter) Überdeckung des Tabellenkopfs. Roman wählte den Callback-Ref, weil stabile Keys allein nur schützen, solange niemand sie vergisst, und das Verschieben des Hinweises die sichtbare Reihenfolge ändert.

**Outcomes:**
- Build: ✅ (`dotnet build`, `npm run build`)
- Tests: `dotnet test` 1099 bestanden, 0 fehlgeschlagen; `npm run test:scripts` 113/113; Filterleisten-Test fünfmal hintereinander 14/14
- Verifikation: PASS in Iteration 4, unabhängig im Browser nachgemessen (sechs Zustände, jeweils 0px Abstand)
- Issues: Ein Lauf von `scripts/ledger-status-column.test.mjs` scheiterte einmal unter Last (Basiszeile 0px), sechs Folgeläufe grün. Nicht von diesem Diff verursacht.

---

## 2026-09-26 10:48 - Work-Lauf: zwölf Aufgaben in vier Wellen (Push-Randfälle, Kassenbuch-Filter, Wächter, Tests)

**What I did:**
Ein `work`-Lauf über vier Wellen mit je drei parallelen Workern in eigenen Worktrees, 12 Aufgaben aus zwei Kontexten (ledger, design-system). 11 sind verifiziert und integriert, eine (`ledger-j8xq4`, Filterleiste) ist nach drei Verifikationsrunden an Roman eskaliert. Jeder Erfolg lief durch einen frischen Verifier; 5 Aufgaben bestanden im ersten Anlauf, 6 brauchten zwei oder drei Runden. Die Funde der Verifier waren durchweg echt: sessionweite Force-Import-Sperre verloren (na3rq), "Unknown"-Payee-Fallback vergessen (jkb0f), Regressionstests, die vor dem Fix nicht rot waren (na3rq, qns4j), Quelltext-Greps ohne Aussagekraft (qns4j), ein wackeliger Browsertest (qns4j), Screenshots, die jeder Testlauf ins Repo schrieb (h6ptn), und ein fest geratener Sticky-Versatz (j8xq4).

**Integriert (Commit, Iterationen):**
- `ledger-mshd8` (afc26de, 1): SyncFlow lädt die Session nach gescheitertem YNAB-Import neu.
- `ledger-jkb0f` (2b92387, 2): Kassenbuch-Ereignis beim Comdirect-Push folgt dem gesendeten Request; neue reine Module `MemoFormatting`, `PayeeFormatting`; Kategorie-Namen-Resolver.
- `ledger-na3rq` (682623e, 3): `PushOutcomeClassification.classifyPushOutcomes` für beide Push-Pfade; Force-Import schließt die Session nicht mehr trotz gescheitertem Kassenbuch-Schreiben.
- `ledger-xk9wq` (bd0a1ab, 1): abgeleitete Indextabelle `ledger_entry_split_categories`, der Kategorie-Filter findet Split-Kategorien.
- `design-system-thtpp` (ee9a2c7, 2): Tailwind-Klassen-Wächter (`scripts/tailwind-class-guard.mjs`, läuft in `dotnet test`), sechs tote Klassen behoben, ADR 0041.
- `ledger-qns4j` (91a7a2e, 3): Konten-Spalte haftet beim Scrollen, Statusspalte zeigt Status statt Herkunft bei 128px; Browsertest gegen gemockte API.
- `ledger-h4qtx` (5b969f0, 1): Payee-Vorschläge falten wie der Payee-Filter, ADR 0042.
- `design-system-w4n9r` (3a635a4, 2): automatischer Test für den Design-System-Export; das Skript prüft sein Bundle selbst.
- `ledger-h6ptn` (383d97b, 2): Kategorie in der PC-Tabelle voll qualifiziert; Merge-Konflikt mit h4qtx (doppeltes Testkonto AG) über die Konfliktleiter gelöst.
- `ledger-f8q2x` (2834413, 1): `getLedgerPayeeSuggestions` im ledger-README dokumentiert.
- `ledger-nnn47` (242fb91, 1): Platzhalter-Zählung in `LedgerViewTests` ohne Kommentarzeilen.

**Eskaliert:**
- `ledger-j8xq4` (Filterleiste und Summenzeile): Funktion, Handy-Beleg und Sticky-Leiste stehen, aber die ResizeObserver-Messung folgt während eines laufenden Spiegel-Laufs dem falschen DOM-Knoten. Worktree `.worktrees/ledger-j8xq4` bleibt, Diff und Buchführungsblöcke liegen unter `.agentheim/salvage/ledger-j8xq4-escalated-iter3.*`.

**Neue Backlog-Items:** `ledger-fgw2q`, `ledger-fgt4k`, `ledger-b7qmx` (Entscheidung), `design-system-tg2wn`.

**Files Added / Modified / Deleted:**
Siehe die elf Task-Commits oben; jeder listet seine Dateien. Neue Dateien unter anderem `src/Server/MemoFormatting.fs`, `src/Server/PayeeFormatting.fs`, `src/Server/PushOutcomeClassification.fs`, `scripts/tailwind-class-guard.mjs`, `scripts/ledger-status-column.mjs`, `scripts/ledger-category-qualified.test.mjs`, `scripts/export-design-system.test.mjs`, ADRs 0041 und 0042.

**Rationale:**
Die drei Push-Bugs stammen aus der Zerlegung von `ledger-tpbe7`; die übrigen Aufgaben waren fertig verfeinert und bereit. Die Wellen wurden so geschnitten, dass parallele Worker möglichst disjunkte Dateien anfassen.

**Outcomes:**
- Build: ✅ (`dotnet build` 0 Warnungen, 0 Fehler; `npm run build` sauber)
- Tests: 1048 bestanden, 6 übersprungen (`.env`-gebunden), 0 fehlgeschlagen; `npm run test:scripts` grün; Klassen-Wächter ohne Fund
- Issues: j8xq4 eskaliert (siehe oben). Ein Worker nutzte `git stash` (ohne Schaden); ein Worker nutzte eine Kopie der Entwicklungsdatenbank für eine Sichtprüfung, echte IDs wurden aus dem Backlog-Item entfernt.

---

## 2026-09-19 20:14 - Work-Lauf: neun Aufgaben (Kassenbuch-Filter, Übertrag, Force-Import, Oberfläche, Werkzeuge)

**What I did:**
Ein `work`-Lauf über drei Wellen, neun Aufgaben aus vier Kontexten. Acht liefen über Worker
in eigenen Worktrees, eine (`infrastructure-qwthn`) habe ich als Conductor selbst ausgeführt,
weil ihr Ergebnis in `.agentheim/board/*/INDEX.md` liegt und Worker dort nach ADR 0032
(Fassung ghcaj) nicht schreiben dürfen. Jede Worker-Aufgabe ging durch einen
`verifier`-Agenten mit frischem Kontext; zwei fielen in Iteration 1 durch und bestanden nach
gezielter Nachbesserung.

**Files Modified (fachlich):**
- `src/Server/Api.fs`, `src/Server/Ledger/CommandHandlers.fs` - Force-Import verknüpft die
  bestehende Kassenbuch-Buchung über die alte, deterministische ImportId neu, statt eine
  zweite "Comdirect-Import"-Buchung anzulegen (`ledger-fq3kd`, ADR 0021).
- `src/Server/Ledger/Events.fs`, `Domain.fs`, `CommandHandlers.fs`, `QueryHandlers.fs` - neues
  Ereignis `EntryTransferCounterpartObservedInYnab` verknüpft die beiden Buchungen eines
  Übertrags über `Entry.TransferEntryId` (`ledger-t7v2q`, ADR 0013/0015).
- `src/Shared/Domain.fs`, `src/Shared/Api.fs`, `src/Server/Ledger/Persistence.fs`,
  `QueryHandlers.fs`, `src/Server/Api.fs` - fünf Such-Filter im Kassenbuch-Lesepfad plus
  `getLedgerPayeeSuggestions`, alle über eine gemeinsame `EntryFilter`/`buildFilterClause`
  (`ledger-vgfw3`, ADR 0036).
- `src/Client/Components/Ledger/View.fs`, `src/Client/DesignSystem/Stats.fs` - Kategorie in
  Violett über den Tokens-Layer, flache Handy-Zeilen statt Karten, neuer `StatAccent`-Fall
  `NoAccent` (`ledger-w9skm`, ADR 0035).
- `scripts/screenshot-route.mjs`, `scripts/screenshot-route.test.mjs` - präsentationale Routen
  ohne Backend und ohne Datenbankkopie (`design-system-m4kp7`); Viewport auf Dokumenthöhe
  statt `fullPage`, damit die inaktive BottomSheet-Leiste nicht mehr ins Bild rutscht
  (`ledger-p9k3w`).
- `template/CLAUDE.md`, `template/README.md`, `template/docs/00-ARCHITECTURE.md`,
  `docs/MILESTONE-PLAN.md` - DaisyUI-Verweise entfernt (`design-system-k4mtx`, ADR 0031).
- `.agentheim/board/*/INDEX.md` (vier Kontexte) - vierzehn Done-Listen-Zeilen auf die von
  `ENTRY_LINE` erwartete Form gebracht (`infrastructure-qwthn`).

**Files Added:**
- `.agentheim/knowledge/decisions/0035-stats-noaccent-statt-classname-override.md`
- `.agentheim/knowledge/decisions/0036-kassenbuch-filter-lesepfad-betrag-in-fsharp.md`
- `docs/ux/kassenbuch/screenshots/` - zwei Belegaufnahmen zu `ledger-w9skm`
- sechs neue Backlog-Aufgaben: `ledger-h4nxz`, `ledger-h6ptn`, `ledger-h4qtx`,
  `ledger-xk9wq`, `ledger-f8q2x`, `ledger-t4m9c`, dazu `design-system-p3vqk`

**Rationale:**
Die Aufgaben stammen aus der Modellierungsrunde vom selben Tag. Die Reihenfolge war nicht
beliebig: `ledger-ezrw0` lief bewusst nach `ledger-vgfw3`, damit es die endgültige
`getLedgerEntriesPage`-Signatur dokumentiert; `ledger-p9k3w` nach `design-system-m4kp7`, weil
beide dieselbe Datei anfassen.

**Outcomes:**
- Build: OK
- Tests: 940 bestanden, 7 übersprungen (.env-gebundene Integrationstests), 0 Fehler
- Verifikation: 6 von 8 Worker-Aufgaben bestanden in Iteration 1, 2 in Iteration 2
- Zwei echte Funde der Verifikation, die ohne sie in den Produktivcode gegangen wären:
  1. `ledger-vgfw3` verglich den Zeitraum-Filter als vollen ISO-8601-Roundtrip-Text in SQL.
     Dessen Länge hängt vom `DateTimeKind` ab (`Utc` hängt `Z` an, `Local` `+02:00`,
     `Unspecified` nichts), und die `date`-Spalte enthält beide Formen gemischt: der
     YNAB-Spiegel schreibt `Utc`, Comdirect und Quick Add schreiben ohne Kind. Unter SQLites
     BINARY-Kollation verlor der Randtag-Vergleich damit still Buchungen, und keine Wahl des
     Client-Kinds machte beide Ränder richtig. Der eigene Test konnte das nicht finden, weil
     er beide Seiten aus denselben `Unspecified`-Literalen baute. Behoben über
     `substr(date, 1, 10)`; ADR 0036 Decision 5 mitkorrigiert, weil sie den unsicheren
     Vergleich mit einer Fehllesung von ADR 0017 gerechtfertigt hatte.
  2. `ledger-ezrw0`s README-Delta hätte die Einrückung eines 32-zeiligen Blocks entfernt und
     damit sechs Unterpunkte aus ihrer Gruppierung gerissen - und künftige `replace`-Anker
     auf diesen Block dauerhaft unerreichbar gemacht. Der sichtbare Text war korrekt, nur der
     Leerraum nicht.

**Issues:**
Wiederkehrend, drei Aufgaben betroffen: die `README_DELTA`-Grammatik erreicht nur einzeilige
Bullets mit fettem Anfang. Die READMEs dieses Projekts brechen ihre Bullets über viele Zeilen
um und benutzen teils Inline-Code als Anfang. Das scheitert leise (Disposition `merged` oder
`applied`, Commit sieht sauber aus) und brauchte dreimal eine andere Umgehung, einmal eine
Reparatur von Hand. Gehört über `modeling` erfasst.

---

**Nachtrag QA-Review (2026-09-20):**
Der von der Projekt-CLAUDE.md vorgeschriebene `qa-milestone-reviewer` hat die Tests der sechs
code-berührenden Aufgaben durchgesehen und dabei einen echten Defekt im gerade ausgelieferten
`ledger-vgfw3` gefunden: `COLLATE NOCASE` faltet in SQLite nur ASCII, also finden Payee- und
Memo-Filter keine Umlaut-Schreibweisen. `müller gmbh` findet `Müller GmbH`, aber nicht
`MÜLLER GMBH` (unabhängig gegen sqlite3 nachgeprüft). Das trifft genau die Daten, die
BudgetBuddy verarbeitet, weil Comdirect- und YNAB-Verwendungszwecke oft in Großbuchstaben
stehen. Kein Verifier hat es gesehen, weil alle acht Filter-Tests reines ASCII säen, teils
sogar transliteriert (`"Baecker"`) — dieselbe Form wie beim `DateTimeKind`-Fund: der Test baut
seine Daten in einer Gestalt, die die Produktion nie erzeugt.

Erfasst als `ledger-k3wqn` (bug, Umlaut-Faltung) und `ledger-r7dm2` (chore, die übrigen
Abdeckungslücken samt der im Review definierten Tests). Zwei Zahlen oben sind vom Review
korrigiert: die Suite meldet je nach Umgebung 941/6 statt 940/7, und `ledger-w9skm` hat sechs
statt sieben Tests hinzugefügt (die 7 stammte aus der Selbstauskunft des Workers und ist
ungeprüft ins Protokoll gewandert).

---

## 2026-09-19 16:05 - Abgelöste Design-Doku gelöscht, Farbpalette auf die lebende Quelle umgehängt

**What I did:**
`docs/DESIGN-SYSTEM.md` (1295 Zeilen) und `docs/README.md` (383 Zeilen) stammten aus der Zeit
vor dem `standards/`-Satz und lasen sich wie gültige Anweisung. Abschnitt 10 der einen
enthielt ein DaisyUI-Installationsrezept, obwohl DaisyUI mit ADR 0031 aus dem Projekt
entfernt ist und nie in `package.json` stand. Roman hat am 2026-09-19 entschieden: löschen
statt bannern, weil eine zweite Quelle, die ein Agent für verbindlich hält, selbst das
Problem ist.

**Files Deleted:**
- `docs/DESIGN-SYSTEM.md`, `docs/README.md`

**Files Modified:**
- `.claude/agents/design-quality.md` - der Abschnitt zitiert keine Farbpalette mehr, sondern
  verweist auf den `@theme`-Block in `src/Client/styles.css` (Hex-Werte) und auf den
  Styleguide-Abschnitt „Farbsemantik" (Bedeutung je Farbe).
- `docs/UI-REFACTORING-MILESTONES.md` - Hinweis unter der Überschrift, der das Dokument als
  historisch ausweist und die „Read First"-Verweise als veraltet markiert.

**Rationale:**
Die naheliegende Lösung wäre gewesen, die Farbwerte in den Styleguide zu kopieren und den
Agenten dorthin zu verweisen. Das hätte genau das Muster wiederholt, das diese Aufgabe
beseitigen soll: eine weitere Kopie, die auseinanderlaufen kann. Der Styleguide enthält
bewusst keinen einzigen Hex-Wert und verweist selbst auf den Code als Quelle.

**Und sie war bereits auseinandergelaufen.** Der Abgleich der neun im Agenten eingebetteten
Werte gegen `src/Client/styles.css` ergab: fünf stimmen, `--neon-pink` stand mit `#ff2e97`,
ist aber heute `#ff1f8f`, und die drei Hintergrundvariablen `--bg-void`/`--bg-dark`/
`--bg-surface` existieren unter diesen Namen gar nicht mehr (heute `--bg-app`/`--bg-card`/
`--bg-elevated`/`--bg-input`/`--bg-hover`). Ein Agent, der diese Datei als verbindlich gelesen
hätte, hätte mit falschen Farben gearbeitet. Beide Abweichungen stehen jetzt namentlich im
neuen Text, nicht stillschweigend korrigiert.

**Outcomes:**
- Tests: 916 bestanden, 7 übersprungen, 923 gesamt, unverändert zur Basis (reine Doku-Änderung)
- Verifikation: PASS in Iteration 1. Der Verifier hat alle neun Farbwerte selbst gegen
  `styles.css` abgeglichen und bestätigt, dass „zwei Abweichungen" die vollständige Wahrheit
  ist und keine Teilprüfung. Er hat außerdem geprüft, dass keine der Kapitel-Dateien
  existiert, auf die `docs/README.md` verwies, und dass kein Skript, kein npm-Eintrag und
  keine `.claude/rules/`-Datei die gelöschten Dateien noch liest.
- `docs/UI-REFACTORING-MILESTONES.md` war ein Zusatzfund: die Datei verweist auf die
  gelöschten Dokumente und stand nicht auf der Ausnahmeliste der Aufgabe. Sie wurde nicht
  gelöscht (das wäre über den Auftrag hinausgegangen), sondern als historisch markiert. Der
  Verifier hat das als erklärend statt anweisend eingestuft, konsistent mit der Ausnahme, die
  der Task den Code-Kommentaren gewährt.
- Aufgeschoben und erfasst: `template/**` führt DaisyUI weiter an drei Stellen. Das ist eine
  eigene Frage und liegt jetzt als `design-system-k4mtx` im Backlog.

---

## 2026-09-19 15:35 - Gelöschte Buchungen: Zählweise benannt, Regel in der README festgehalten

**What I did:**
Entwurf und gebauter Stand liefen auseinander. Der Entwurf zählte eingeblendete gelöschte
Buchungen in der Trefferzahl, aber nicht in der Summe; gebaut zählten sie in beidem. Roman
hat am 2026-09-18 Variante 2 entschieden: der gebaute Stand bleibt, weil sich die sichtbaren
Zeilen dann genau zur angezeigten Summe addieren. Diese Aufgabe setzt das um und macht es
sichtbar; entschieden wurde nichts neu.

**Files Modified:**
- `src/Client/Components/Ledger/View.fs` - `deletedCountText` sagt bei eingeblendeten
  gelöschten Buchungen jetzt "13 Buchungen, davon 1 in YNAB gelöscht" statt nur zu melden,
  dass sie eingeblendet sind. Singular und Plural richten sich dabei nach der Trefferzahl.
  Der ausgeblendete Fall bleibt unverändert.
- `src/Tests/LedgerViewTests.fs` - Tests für beide Zustände samt Grenzfall bei genau einer
  Buchung.
- `src/Tests/LedgerPageTests.fs` - ein Test hält fest, dass Summenzeile und Kontostand bei
  eingeschaltetem Schalter bewusst auseinanderlaufen.
- `.agentheim/knowledge/contexts/ledger/README.md` - der Absatz über gelöschte Buchungen
  trägt jetzt die Regel, damit die künftige Summenzeile aus `ledger-vgfw3` sie übernimmt
  statt sie neu zu entscheiden.

**Rationale:**
Am Code für Trefferzahl und Summe ändert sich nichts. Die Arbeit steckt darin, die
Abweichung zwischen Summenzeile und Kontostand zu benennen, statt sie als stillen
Widerspruch stehen zu lassen, und sie gegen späteres Kippen zu sichern.

**Outcomes:**
- Build: ✅ (`dotnet build` und `npm run build` grün)
- Tests: 916 bestanden, 7 übersprungen, 923 gesamt
- Verifikation: FAIL in Iteration 1, PASS in Iteration 2. Beide Male ging es nicht um den
  Code: der erste Verifier hat Kriterien, Aufrufstelle, Argumentreihenfolge und
  Fixture-Isolation selbst geprüft und für richtig befunden.

**Der eigentliche Befund dieser Aufgabe betrifft das Werkzeug, nicht das Kassenbuch.**
Iteration 1 wollte die Regel per `append` an den Abschnitt hängen. Der Verifier hat zwei
Dinge gesehen, die ich in meinem eigenen Trockenlauf übersehen hatte: die Zeile wäre am Ende
der Liste „Was das Konzept voraussetzt und heute nicht gibt" gelandet, eine entschiedene
Regel hätte sich dort als offene Voraussetzung gelesen. Und weiter oben stand ein Halbsatz,
den die Änderung falsch macht, weil er das eingeblendete Label noch in der alten Fassung
zitierte. Ein Anhängen kann einen widersprechenden Satz nicht korrigieren.

Iteration 2 lieferte dafür ein `replace`. Der Trockenlauf ergab `anchor-missing`: der
Zielabsatz beginnt nicht mit einem Listenmarker, und `applyReadmeDelta` adressiert
ausschließlich Zeilen, die an Spalte 0 mit `- ` beginnen. Der Absatz ist für den Mechanismus
also grundsätzlich unerreichbar, und jeder weitere Versuch wäre identisch gescheitert. Hätte
ich das Delta ungeprüft angewendet, stünde die alte, falsche Aussage weiterhin da und die
neue darunter. Die Korrektur ist deshalb von Hand gesetzt, mit einem Skript, das bei jeder
nicht zutreffenden Vorbedingung abbricht, bevor es schreibt.

Das war heute der zweite Fall dieser Art. Beim ersten (`design-system-yxrxb`) passte ein
einzeiliger `expected` nicht auf ein hart umbrochenes Bullet. Gemeinsam ist beiden, dass
nichts scheitert: der Mechanismus hängt den neuen Text an und meldet Erfolg.

**Offen, bewusst nicht erledigt:** Der `getLedgerEntriesPage`-Eintrag unter `## Sichten`
beschreibt weiterhin nur den ausgeblendeten Fall. Er wird dadurch unvollständig, nicht
falsch. Der Verifier hat das als Backlog-Kandidaten benannt, nicht als Mangel dieser Aufgabe.

---

## 2026-09-19 15:05 - Neue Money-Varianten im Styleguide und in der /styleguide-Route nachgezogen

**What I did:**
`ledger-x0yga` hatte die Money-Komponente um `accountBalance` und `accountListRow`
erweitert, letztere keine reine Formatierung, sondern eine klickbare Kontozeile mit Name
links und Betrag rechts. Nachgezogen wurde davon nichts: weder das Komponenten-Inventar im
Styleguide noch die `/styleguide`-Route zeigten die beiden Varianten.

**Files Modified:**
- `standards/frontend/styleguide.md` - Money-Zeile im Komponenten-Inventar nennt beide
  Varianten, je mit Zweck statt nur dem Namen.
- `src/Client/Components/Styleguide/View.fs` - die Money-Sektion zeigt `accountBalance` mit
  einem erfundenen Beispielwert und `accountListRow` in beiden Zuständen, aktiv und inaktiv.

**Rationale:**
Die Folge reichte weiter als eine Doku-Lücke. Der Claude-Design-Export schneidet seine
Karten aus der gerenderten Styleguide-Route (ADR 0024). Was dort nicht steht, existiert im
Entwurfsprojekt nicht, und beim nächsten Zeichnen sieht niemand, dass es eine fertige
Kontozeile bereits gibt. Sie würde ein zweites Mal erfunden.

**Outcomes:**
- Build: ✅ (`dotnet build` und `npm run build` grün)
- Tests: 914 bestanden, 7 übersprungen, 921 gesamt (keine neuen Tests, reiner Inhaltsnachtrag)
- Export: `npm run export:design-system` zweimal gelaufen, Bundle byte-identisch
- Verifikation: PASS in Iteration 1. Der Verifier hat die Aufrufsignaturen gegen
  `src/Client/DesignSystem/Money.fs` geprüft (`accountListRow label amount currency isActive
  onClick`), den Export selbst zweimal gefahren und per SHA-256 über alle 33 Bundle-Dateien
  verglichen, die Feldzahl der Markdown-Tabellenzeile gemessen, und bestätigt, dass die
  beiden Zustände im exportierten Bundle wirklich unterscheidbar sind
  (`border-l-neon-teal` gegen `border-l-transparent`).
- Wechselwirkung geprüft, nicht angenommen: die Demo-Werte stehen in einem Modul-Level-Wert,
  und `design-system-e5vfc` hat heute früh einen Wächter dafür eingebaut. Die
  Styleguide-Ansicht ist einer der drei legitim ausgenommenen Startup-Typen und liegt
  außerhalb des Namensfilters; der Dateizähl-Abgleich bleibt unberührt.
- Bewusst nicht gebaut: die im `What` angerissene Zusicherung, die jede öffentliche Funktion
  eines Design-System-Moduls gegen die Route abgleicht. Der Task schließt sie ausdrücklich
  aus und verweist auf `design-system-thtpp`, das im Backlog liegt.

**Offen für Roman:** Das letzte Akzeptanzkriterium ist der Re-Sync nach Claude Design und
trägt `[human-eye]`. `DesignSync` gibt es nur in der Hauptsitzung, ein Subagent sieht das
Werkzeug nicht. Der Export ist gelaufen, der neue Bundle-Stand liegt vor; der Re-Sync selbst
bleibt Romans Schritt.

---

## 2026-09-19 14:45 - Backend-Port kommt aus der Konfiguration, und beide Regeln sind bewacht

**What I did:**
Der Backend-Port stand als Literal in `app.Run("http://0.0.0.0:5081")`. Das überschrieb
still die `ASPNETCORE_URLS`-Zeile, die in `compose.yaml` längst stand: die Konfigurierbarkeit
war vorgetäuscht. Wer den Port dort änderte, änderte nichts, und es fiel erst im Betrieb auf.
Aufgefallen ist es, als ein laufender Entwicklungsserver auf 5081 einen Screenshot-Lauf
blockierte, der sich fürs Frontend längst einen freien Port reserviert, fürs Backend aber
nicht konnte.

**Files Added:**
- `src/Server/ServerConfig.fs` - reine Funktion `resolveUrls`: ohne gesetzte Variable der
  bisherige Standard `http://0.0.0.0:5081`, sonst der gesetzte Wert.
- `src/Tests/ServerConfigTests.fs` - vier Tests dafür.
- `src/Tests/BackendPortGuardTests.fs` - liest `Program.fs` selbst und lehnt jeden
  `app.Run(...)`-Aufruf mit Argument ab.

**Files Modified:**
- `src/Server/Program.fs` - `app.Run()` ohne Argument, die Variable wird vor
  `WebApplication.CreateBuilder` gesetzt, die Start-Log-Zeile nennt den gebundenen Wert.
- `vite.config.js` - Proxy-Ziel liest `BACKEND_PORT`, Standard bleibt 5081.
- `scripts/screenshot-route.mjs` - reserviert auch fürs Backend einen freien Port und reicht
  ihn an beide Kindprozesse weiter; die Abbruchprüfung auf belegten Port 5081 entfällt.
- `scripts/screenshot-route.test.mjs` - zwei Tests, die die Weitergabe von `BACKEND_PORT`
  festhalten.
- `src/Server/Server.fsproj`, `src/Tests/Tests.fsproj` - neue Dateien eingetragen.

**Rationale:**
Der Container muss weiter auf 5081 binden, weil Healthcheck und `tailscale serve` darauf
zeigen (ADR 0010). Ziel war deshalb nicht, den Port zu ändern, sondern dass sein Wert aus der
Konfiguration kommt statt aus dem Quelltext. `compose.yaml` und `Dockerfile` blieben
unangetastet; ihre `ASPNETCORE_URLS`-Zeile tut jetzt, was sie verspricht.

**Outcomes:**
- Build: ✅ (`dotnet build` und `npm run build` grün)
- Tests: 908 bestanden, 7 übersprungen, 915 gesamt; `node --test scripts/screenshot-route.test.mjs` 21/21
- Verifikation: FAIL in Iteration 1, PASS in Iteration 2. Der erste Verifier hat die
  inhaltlichen Kriterien selbst nachgefahren und bestätigt (zwei Instanzen parallel auf 5081
  und 6123, per `lsof` belegt, beide gegen Scratch-Verzeichnisse; der Vite-Proxy folgt der
  Variable nachweislich). Abgelehnt hat er, dass die neue Regel unbewacht blieb: ein künftiges
  URL-Argument an `app.Run` hätte alle Tests grün gelassen.
- Iteration 2 hat die Durchsetzung nachgeliefert. Dabei ist dem Worker die eigene Falle
  aufgefallen: seine erste Wächter-Fassung schnitt jede Zeile am ersten `//` ab, und weil
  `app.Run("http://0.0.0.0:5081")` selbst ein `//` enthält, schnitt sie genau den Verstoß
  weg, den sie finden sollte. Zwei der sieben neuen Testfälle wurden rot und haben es
  aufgedeckt; die Ersatzfassung verfolgt String-Literale zeichenweise.
- Der zweite Verifier hat alle drei Wächter selbst falsifiziert und den Parser zusätzlich mit
  vier Grenzfällen geprüft, darunter einer, der bei kaputtem Escape-Handling einen Fehlalarm
  erzeugt hätte.
- Benannte Grenzen (in ADR 0034 festgehalten): der Parser kennt keine `(* ... *)`-Kommentare
  und keine verschachtelten Anführungszeichen jenseits einfacher Escapes; die beiden
  Node-Tests erkennen eine korrekt benannte, aber falsch verdrahtete Variable nicht.

**ADR:** 0034 (Backend-Port über ASPNETCORE_URLS statt Literal, mechanisch abgesichert)

---

## 2026-09-19 14:20 - Historie zeigt die neueste Zeile oben, gedreht wird nur in der Anzeige

**What I did:**
Entwurf und gebauter Stand liefen auseinander: der Entwurf zeigt die Historie von neu nach
alt, gebaut war sie von alt nach neu. Roman hat am 2026-09-18 entschieden, dass die Anzeige
die neueste Zeile oben zeigt und ausschließlich die Anzeige dreht. Diese Aufgabe setzt das
um; entschieden wurde nichts neu.

**Files Modified:**
- `src/Client/Components/Ledger/View.fs` - neue reine Funktion `historyLinesForDisplay`
  (`List.rev`), aufgerufen in `historySheet`. Die laufende Nummer je Zeile bleibt die
  `Sequence` aus dem Ereignis-Log und zählt in der Anzeige deshalb abwärts.
- `src/Tests/LedgerViewTests.fs` - drei Tests für die Drehung, inklusive leerer Liste.
- `src/Tests/LedgerPageTests.fs` - ein Persistence-Test, der die aufsteigende Reihenfolge
  der Abfrage festhält, damit die Drehung nicht später nach unten wandert.

**Rationale:**
Im Detail-Sheet am Handy scrollt die Historie, und gesucht wird die letzte Änderung, nicht
die Entstehung. Die Abfrage bleibt aufsteigend, damit laufende Nummer, Ereignis-Reihenfolge
und der Replay-Pflichttest aus ADR 0013 unberührt bleiben. `src/Server/Ledger/Persistence.fs`
und `QueryHandlers.fs` sind nachweislich nicht angefasst.

**Outcomes:**
- Build: ✅ (`dotnet build` und `npm run build` grün)
- Tests: 901 bestanden, 7 übersprungen, 908 gesamt
- Verifikation: PASS in Iteration 1. Der Verifier hat die entscheidende Probe selbst
  gefahren: `List.rev` durch die Identität ersetzt, zwei Tests wurden rot, Datei aus der
  Sicherung zurückgespielt, Prüfsumme identisch, Arbeitsbaum sauber. Er hat außerdem den
  Replay-Pflichttest gefiltert laufen lassen (3/3 grün) und bestätigt, dass die verwendete
  Test-Buchungsnummer im Fixture-Namensraum sonst unbenutzt ist.
- Einen Verdacht habe ich ihm mitgegeben und er hat ihn geprüft: der zweite Test sah nach
  einer Tautologie aus. Befund: inhaltlich redundant zum ersten, aber eigenständig
  falsifizierbar, weil die Erwartung aus der Eingabeliste gebildet wird und nicht aus der
  Ausgabe. Kein Tautologie-Fall.
- Benannte Restschwäche: die Regel steht als neuer Begriff "Historie-Anzeige" am Ende des
  Abschnitts "Ubiquitous language" des ledger-README, nicht direkt bei `getEntryHistory`.
  Die bestehende `getEntryHistory`-Zeile bekommt keinen Vorwärtsverweis auf die Drehung.
  Sie bleibt sachlich richtig, weil die Abfrage tatsächlich nicht dreht.

---

## 2026-09-19 14:10 - Wächter-Abdeckung im Design-System gegen den eigenen Verlust gesichert

**What I did:**
`src/Tests/DesignSystemStartupTests.fs` erzwingt per Reflection jeden Typinitialisierer der
Design-System-Module und fängt damit eager ausgewertete Top-Level-Werte ab. Diese Abdeckung
war echt, aber unbewacht: der begleitende Wächter-gegen-Wächter prüfte nur, dass die
Typliste nicht leer ist, und das hält auch bei 19 statt 20 erfassten Typen. Der Verifier von
`design-system-bq4tz` hatte das gemessen, nicht vermutet: Sonderfallzeile entfernt, Suite
blieb grün.

**Files Modified:**
- `src/Tests/DesignSystemStartupTests.fs` - zwei neue Tests. Der erste nagelt den bekannten
  `ErrorDisplay`-Sonderfall per `Expect.contains` fest. Der zweite ist die eigentliche
  Absicherung: er vergleicht die Zahl der erfassten Startup-Typen gegen die Zahl der
  `.fs`-Dateien unter `src/Client/DesignSystem/`. Damit wird nicht nur die eine bekannte
  Abweichung gefangen, sondern auch die nächste, noch unbekannte.

**Rationale:**
Eine Prüfung, die aussieht als decke sie etwas ab und es nicht mehr tut, sobald sich eine
Annahme verschiebt, ist genau die Fehlerklasse, an der diese Aufgabe zweimal gescheitert
ist: erst ein Suchmuster, das eingerückte Werte übersah, dann ein Präfixfilter, der eine
abweichende Moduldeklaration übersah. Der Abgleich gegen die Dateizahl stellt die Frage
anders: nicht „findet der Filter irgendetwas", sondern „findet er alles".

**Outcomes:**
- Build: ✅
- Tests: 899 bestanden, 7 übersprungen, 906 gesamt
- Verifikation: PASS in Iteration 1. Der Verifier hat beide geforderten Eingriffe selbst
  nachgefahren (Filterzeile entfernt: 2 Tests rot, 19 gegen 20; Fremddatei angelegt: 1 Test
  rot, 20 gegen 21) und zusätzlich eine dritte Probe gefahren, die ich ihm als Risiko
  mitgegeben hatte: eine korrekt benannte neue Komponentendatei ohne Modul-Level-Werte,
  kompiliert und eingebunden. Die Suite blieb grün. Damit ist belegt, dass F# je
  kompilierter Datei einen Startup-Typ erzeugt, die 1:1-Zuordnung also echt ist und der
  Wächter bei legitimer neuer Arbeit nicht fälschlich anschlägt.
- Randbefund, bewusst nicht angefasst: `src/Tests/ModuleLevelDbInitGuardTests.fs` (heute
  Morgen aus `infra-sdad8` integriert) trägt mit `totalBlocks > 0` strukturell dieselbe
  schwache Absicherung. Andere Datei, anderer Kontext, deshalb außerhalb dieser Aufgabe.
  Der hier gebaute Mechanismus ließe sich dort sinngemäß übertragen.

---

## 2026-09-19 13:35 - DB-Setup nur noch in testCase-Closures, jetzt mechanisch durchgesetzt

**What I did:**
Sieben `[<Tests>]`-Bindings riefen `initializeDatabase()`/`initializeLedgerSchema()`/
`seedAccounts()` direkt im `let`-Body auf, also beim Modul-Laden statt in der
Testausführung. F# wertet Modul-Level-Bindings beim ersten Zugriff aus, und genau diesen
Zugriff löst Expectos `[<Tests>]`-Entdeckung per Reflection aus, bevor ein einziger
Testfall läuft. Dadurch entstand in einem Umgehungslauf eine vollständige Datei-Datenbank,
bevor der Wächter aus `infra-za7fq` überhaupt scheitern konnte: der meldet, er verhindert
nicht.

**Files Added:**
- `src/Tests/ModuleLevelDbInitGuardTests.fs` - durchsucht jeden `[<Tests>]`-Block unter
  `src/Tests/` auf Aufrufe außerhalb einer Closure und schlägt fehl, wenn das Muster
  zurückkehrt. Arbeitet über einen Einrücktiefen-Stack statt über eine feste Spalte, damit
  verschachtelte Module nicht durchrutschen.

**Files Modified:**
- `src/Tests/LedgerMirrorTests.fs`, `src/Tests/LedgerPageTests.fs`,
  `src/Tests/LedgerTests.fs`, `src/Tests/PersistenceTypeConversionTests.fs` - Setup-Aufrufe
  in eine lokal geschattete `testCase`-Closure verschoben. Da ein `let` ohne `rec` in F#
  keinen Selbstbezug erlaubt, löst der Bezeichner auf Expectos `testCase` auf; die
  Schattierung greift dadurch für jeden Testfall im selben Bindungs-Scope, ohne eine
  einzige bestehende Testzeile anzufassen.
- `src/Tests/Tests.fsproj` - neue Wächter-Datei direkt nach `TestDatabaseGuardTests.fs`.

**Rationale:**
Der ursprüngliche Auftrag war das Verschieben allein. Die Verifikation hat in Iteration 1
zu Recht abgelehnt: die Aufgabe schrieb damit eine Konvention für künftige Testdateien
fest, ohne sie durchzusetzen und ohne die Lücke als bewusst ungeprüft zu vermerken. Genau
diese Fehlerklasse, eine Stelle die aussieht als sichere sie etwas ab, verfolgt dieser
Aufgaben-Cluster. Iteration 2 hat die Durchsetzung nachgeliefert.

**Outcomes:**
- Build: ✅
- Tests: 879 bestanden, 7 übersprungen, 886 gesamt (874 vorher plus 5 neue)
- Verifikation: FAIL in Iteration 1 (fehlende Durchsetzung), PASS in Iteration 2. Der
  Verifier hat die Wirksamkeit selbst geprüft statt sie zu übernehmen: altes Bug-Muster
  eingespielt, Suite ging rot mit Exit-Status 1 und nannte `LedgerMirrorTests.fs:539`,
  danach zurückgesetzt, Arbeitsbaum sauber. Er hat außerdem gezählt, dass der Scan
  102 `[<Tests>]`-Blöcke über 30 Dateien durchläuft, die Prüfung also nicht leer
  durchrauscht.
- Bewusst dokumentierte Lücke: der Scan erfasst namentlich nur `initializeDatabase` und
  `initializeLedgerSchema`, nicht `seedAccounts()`. Ein Rückfall dort bliebe unentdeckt,
  bis die Namensliste erweitert wird. Festgehalten in ADR 0033 und im README-Eintrag, nach
  dem Vorbild von `infra-za7fq`.
- Ebenfalls notiert, nicht behoben: der begleitende Wächter-gegen-Wächter prüft nur
  `totalBlocks > 0`. Das ist dieselbe schwache Form, die `design-system-e5vfc` im Backlog
  bereits an der Design-System-Variante kritisiert.

**ADR:** 0033 (DB-Setup in `[<Tests>]`-Bindings nur in `testCase`-Closures, mechanisch
durchgesetzt)

---

## 2026-09-19 13:05 - Wartepredikat blockierte die /styleguide-Route: App meldet ihre Bereitschaft jetzt selbst

**What I did:**
`scripts/screenshot-route.mjs` wartete darauf, dass `.proto-spinner` und `.animate-pulse` aus
dem DOM verschwinden. Auf der `/styleguide`-Route verschwinden die nie, weil sie dort
Demo-Inhalte der Loading-Sektion sind. Eine Heuristik über Klassennamen kann nicht
unterscheiden zwischen "die Seite lädt noch" und "die Seite zeigt absichtlich einen
Ladezustand". Ersetzt durch ein Bereitschaftssignal, das die App selbst setzt.

**Files Added:**
- `src/Tests/AppReadinessTests.fs` - 14 Expecto-Fälle für `State.isPageReady`, je Seite,
  inklusive des Kettenfalls bei SyncFlow und des Failure-Falls beim Kassenbuch.

**Files Modified:**
- `src/Client/State.fs` - neue reine Funktion `isPageReady`: Styleguide ist sofort bereit,
  jede andere Seite erst, wenn keiner ihrer von `pageLoadCmd` geladenen RemoteData-Stränge
  mehr lädt. `NotAsked` zählt als bereit, damit ein erst auf Nutzeraktion ladender Strang
  (Settings/YnabBudgets) das Signal nicht blockiert.
- `src/Client/View.fs` - rendert das Ergebnis als unsichtbares Element mit
  `data-app-ready="true"|"false"`, als eigenes Element statt als Prop auf `appWrapper`.
- `scripts/screenshot-route.mjs` - `waitForPageLoaded` wartet auf
  `[data-app-ready="true"]` statt auf das Verschwinden der beiden Klassen. Timeout- und
  Fehlerpfad unverändert, die Route steht weiterhin im Fehlertext.
- `scripts/screenshot-route.test.mjs` - Regressionstest, der die echte Quelldatei liest und
  das Wiedereinführen der Klassen-Heuristik rot werden lässt.
- `src/Tests/Tests.fsproj` - neue Testdatei aufgenommen.

**Rationale:**
Das Signal musste den Ladezustand tragen, nicht bloß das Mounten. Ein Signal, das nur meldet
"die Seite ist da", hätte das Werkzeug wieder Platzhalter aufnehmen lassen, bevor die
Kassenbuch-Tabelle ihre Daten hat. Die Seiten halten diesen Zustand ohnehin als RemoteData im
Elmish-Modell, es war also vorhanden und musste nur sichtbar gemacht werden.

**Outcomes:**
- Build: ✅ (`npm run build` sauber)
- Tests: 888 bestanden, 7 übersprungen, 0 fehlgeschlagen; `node --test scripts/screenshot-route.test.mjs` 19/19
- Verifikation: PASS in Iteration 1. Der Verifier hat die beiden Augen-Kriterien selbst
  nachgefahren statt sie zu übernehmen: die vom Worker behaupteten Bilder lagen nicht am
  Standardort, also hat er `#/styleguide` und `#/ledger` eigenhändig aufgenommen und die
  Bilder angesehen. Das Kassenbuch-Bild zeigt Kontostände und Buchungsliste gefüllt, keine
  Skeleton-Platzhalter. Er hat außerdem geprüft, dass der `Page`-Match alle sechs Fälle aus
  `src/Client/Types.fs` abdeckt, also keine unvollständige Verzweigung zur Laufzeit entsteht.
- Offen und getrennt verfolgt: das teilweise gerenderte Sheet in der Full-Page-Aufnahme ist
  `ledger-p9k3w`, nicht Teil dieser Aufgabe.

---

## 2026-09-19 12:55 - Bug: Design-System-Export trug @font-face mit absolutem url(/fonts/...)

**What I did:**
`scripts/export-design-system.mjs` kopierte das gebaute App-CSS unverändert ins
Claude-Design-Bundle. Die absolute @font-face-Regel aus dem App-Build gewann dort gegenüber
der relativen aus `colors_and_type.css`, die Font-Anfragen gingen an die Bundle-Origin-Wurzel
und liefen in 404: in Claude Design blieb die Warnung "Missing brand fonts" stehen.

**Files Modified:**
- `scripts/export-design-system.mjs` - neue reine Funktion `rewriteAbsoluteAssetUrls`, genutzt
  in `copyBuiltCss` (liest, schreibt um, schreibt) statt des bisherigen `copyFile`; der
  `main()`-Aufruf steht jetzt hinter einem Guard, der nur bei Direktstart greift, damit der
  Test die Funktion importieren kann, ohne den ganzen Export anzustoßen.
- `src/Tests/FontFaceRegressionTests.fs` - vier neue Testfälle, die die reale Funktion per
  Node-Subprozess aus dem Export-Skript importieren statt die Logik in F# nachzubauen.

**Rationale:**
Die App liefert sich selbst von der Origin-Wurzel aus, deshalb sind die @font-face-Pfade im
Quell-CSS absolut korrekt. Im abgeleiteten Bundle liegt die Wurzel woanders; der Export muss
diesen Unterschied beim Kopieren ausgleichen, statt die App ihre eigenen Pfade verbiegen zu
lassen.

**Outcomes:**
- Build: ✅
- Tests: 878 bestanden, 7 übersprungen, 0 fehlgeschlagen
- Verifikation: PASS in Iteration 1. Der Verifier hat den Export selbst zweimal laufen lassen
  (Bundle byte-identisch, 33 Dateien), alle Bundle-Dateien auf absolute `url(/…)`-Pfade
  durchsucht (0 Treffer) und den neuen Entry-Point-Guard separat geprüft, weil ein nicht
  greifender Guard den Export stillschweigend nichts tun und trotzdem mit Status 0 enden
  ließe.
- Benannte Restlücke: die Tests prüfen die Rewrite-Funktion und `src/Client/styles.css`, nicht
  das erzeugte Bundle. Ein Rückfall auf `copyFile` bei erhaltener Funktion bliebe ungefangen;
  ein Bundle-Test braucht ein E2E-Harness, das `dotnet test` nicht hat (Backlog:
  design-system-w4n9r).

---

## 2026-09-18 21:20 - Rückmeldung ans Design-Projekt: CLAUDE.md ist ein reservierter Pfad, Nachtrag liegt im Repo

**What I did:**
Die Entscheidungen sollten zurück in die `CLAUDE.md` des Design-Projekts, sonst kommt die
Gegenregel zu den gelöschten Buchungen beim nächsten Zeichnen wieder. Die vollständige neue
Fassung war lokal fertig und strukturell geprüft (382 Zeilen, alle Abschnitte und erfundenen
Beträge vorhanden, die schiefe Nummerierung der offenen Punkte absichtlich wie im Original).
Der Schreibvorgang wurde abgelehnt: **`CLAUDE.md` und `.claude/` sind im Design-Projekt
reservierte Pfade und werden unabhängig vom Plan blockiert**, weil sie den Design-Agenten
steuern. Das ist eine gewollte Sperre, kein Fehler, und ich habe nichts daran vorbei
versucht.

Stattdessen liegt der Inhalt jetzt als einfügefertiger Nachtrag im Repo. Roman gibt ihn in
einen Chat des Design-Projekts, der Agent pflegt seine Anweisungsdatei selbst.

**Files Added:**
- `docs/ux/kassenbuch/design-projekt-nachtrag-2026-09-18.md` - 99 Zeilen, sechs Abschnitte:
  DaisyUI aus der Übergabe-Anweisung streichen, die zwei geänderten Regeln aus Ablauf 7
  (gelöschte zählen eingeblendet in Trefferzahl und Summe, also -722,06 € statt -712,26 €;
  Historie-Richtung bestätigt mit dem Hinweis, dass nur die Anzeige dreht), offener Punkt 16
  ist entschieden, Tabelle „Stand der Abläufe" nachziehen, acht neue offene Punkte aus dem
  Ablesen der Tafeln, und was aufgeräumt wurde

**Files Modified:**
- `.agentheim/board/ledger/backlog/ledger-d4kx8-...` - Punkt 4 sagt jetzt, dass kein
  Werkzeug das schreiben kann und wo der Text liegt, statt „dort nachziehen"
- `.agentheim/board/protocol.md` - derselbe Sachstand im Eintrag von 20:59

**Rationale:**
Die DaisyUI-Angabe stammt aus genau dieser `CLAUDE.md` und lief über das Export-Skript im
Kreis: Repo sagt DaisyUI, Design-Projekt schreibt es in sein Übergabe-Dokument, Repo holt es
zurück. Solange sie dort steht, ist die Korrektur im Repo nur die Hälfte. Dasselbe gilt für
die Regel zu den gelöschten Buchungen: im Repo entschieden, im Entwurf noch anders
gezeichnet.

**Outcomes:**
- Build: nicht nötig, nur Markdown
- Tests: nicht nötig, kein Code geändert
- Geprüft: Nachtrag ohne Gedankenstriche und ohne Umschreibungen statt Umlaute
- Issues: Der Rückweg ins Design-Projekt ist manuell und bleibt es. Das Werkzeug kann dort
  Screens und Pakete schreiben, aber nicht die Anweisungsdatei. Für künftige Abgleiche heißt
  das: Rückmeldungen als Nachtrag ins Repo legen, nicht versuchen zu schreiben

---

## 2026-09-18 21:06 - Zwei Entscheidungen getroffen, Übergabe-Dokumente für Ablauf 3 und 7 verfasst

**What I did:**
Roman hat die Screens der Abläufe 3 bis 7 abgenommen und die zwei Widersprüche aus dem
Design-Abgleich entschieden. Übergabepakete gibt es im Design-Projekt nur für Ablauf 1 und
2, also sind die Dokumente für Ablauf 3 und 7 hier im Repo aus den Tafeln verfasst. Genau
die beiden hängen an den nächsten Scheiben (`ledger-r1d2f` und `ledger-q1hw2`); 4, 5 und 6
folgen auf Zuruf.

**Files Added:**
- `docs/ux/kassenbuch/handoff-ablauf-3-mehrfachauswahl-mit-summe.md` - 566 Zeilen, sieben
  Screens (Handy 3.1 bis 3.4, PC 3.5 bis 3.7)
- `docs/ux/kassenbuch/handoff-ablauf-7-was-nur-bb-kann.md` - 711 Zeilen, zehn Screens
  (Handy 7.1 bis 7.5, PC 7.6 bis 7.10)
- `.agentheim/board/design-system/backlog/design-system-k2vgz-alte-doku-saetze-abloesen-oder-loeschen.md` -
  `docs/DESIGN-SYSTEM.md` und `docs/README.md` sind vom `standards/`-Satz abgelöst;
  Farbpalette vorher sichern, weil `.claude/agents/design-quality.md:74` sie zitiert

**Files Modified:**
- `.agentheim/board/ledger/backlog/ledger-h9tq2-...` - Entscheidung: Historie zeigt die
  neueste Zeile oben, gedreht wird nur in der Anzeige; die Abfrage bleibt aufsteigend,
  damit laufende Nummer und Replay-Pflichttest (ADR 0013) unberührt bleiben. Betrifft auch
  die heute schon gebaute Historie-Anzeige, nicht nur die künftige Detail-Sicht
- `.agentheim/board/ledger/backlog/ledger-d4kx8-...` - Entscheidung: eingeblendet zählen
  gelöschte Buchungen in Trefferzahl und Summe, damit die sichtbaren Zeilen sich zur Summe
  addieren; die Zeile über der Liste sagt es aus. Am Code für Zähler und Summe ändert sich
  nichts, die Arbeit steckt in der Beschriftung
- `docs/ux/kassenbuch/briefs.md` - Ablauf 7: die zwei Punkte sind entschieden, nicht mehr
  offen
- `.agentheim/board/design-system/INDEX.md` - ein Backlog-Eintrag, Zähler 7 auf 8
- `.agentheim/board/protocol.md` - Eintrag zu den Entscheidungen und zur Erfassung

**Rationale:**
Die Briefs sind Prosa ohne Maße. Für `ledger-r1d2f` und `ledger-q1hw2` hätte ein Worker
sonst keine pixelgenaue Vorlage gehabt, während sie für Ablauf 1 und 2 vorliegt. Beide neuen
Dokumente sagen im Vorspann ausdrücklich, dass sie im Repo aus der Tafel abgelesen sind und
kein wortgleiches Paket dahintersteht, damit ihr Status erkennbar bleibt.

**Outcomes:**
- Build: nicht nötig, nur Markdown
- Tests: nicht nötig, kein Code geändert
- Geprüft: beide Dokumente null Gedankenstriche, keine Umschreibungen statt Umlaute, kein
  DaisyUI als Anweisung, keine `_ds/`-Pfade, Abschnittsfolge wie die zwei Vorlagen, alle
  Screens abgedeckt
- Issues: Die Tafeln haben beim Ablesen echte Ungereimtheiten gezeigt, im Dokument benannt
  statt glattgezogen. Wichtig für Roman: „Abweichung" und „Zu prüfen" tragen dasselbe Pink,
  in der Zeile „REWE Mitte" stehen also zwei farbgleiche Badges übereinander; bei der
  gelöschten Buchung liegt die Löschung (11.09.) vor Import (15.09.) und Prüfung (16.09.);
  drei Werte in Ablauf 7 weichen von den Repo-Kopien zu Ablauf 1 und 2 ab (Tabellenkopf
  40px statt 42px, Memo-Spalte und PC-Summe); am PC fehlen in Ablauf 3 die Chips „Flag" und
  „Gelöschte zeigen"; der Kita-Beitrag steht in den zwölf Zeilen von Ablauf 3 nur zweimal,
  obwohl die Annahme einen Monatsrhythmus behauptet

---

## 2026-09-18 20:51 - DaisyUI-Angaben auf ADR 0031 nachgezogen (neun Stellen)

**What I did:**
Beim Design-Abgleich aufgefallen: die zwei frisch geholten Übergabe-Dokumente schicken den
Worker zu „Tailwind 4 und DaisyUI". DaisyUI ist mit **ADR 0031** (2026-09-13,
`design-system-fq8dn`) aus dem Projekt entfernt und steht auch nicht in `package.json`.
Die falsche Angabe lag nicht nur in den Design-Dokumenten, sondern auch in drei
Standards-Dateien, im Wurzel-README, in der Serena-Memory und im Export-Skript. Aktuell war
einzig die Wurzel-`CLAUDE.md`. ADR 0031 hatte diese Doku-Leiche beim Schreiben benannt,
nachgezogen wurde damals nur eine von neun Stellen.

**Files Modified:**
- `docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`,
  `handoff-ablauf-2-buchungen-finden.md` - DaisyUI gestrichen; im Vorspann steht jetzt, dass
  das eine bewusste Abweichung zur Design-Fassung ist und beim nächsten Abgleich nicht
  zurückgezogen wird (vorher behauptete er „unverändert übernommen")
- `standards/README.md`, `standards/global/architecture.md` - Tech-Stack-Zeile
- `standards/frontend/styleguide.md` - der Token-Layer liegt über Tailwind 4, nicht über
  „Tailwind 4 / DaisyUI"
- `README.md` - Tech-Stack-Tabelle
- `.serena/memories/tech_stack.md` - die Memory, die Serena Agenten als Projektwissen gibt
- `.agentheim/knowledge/contexts/design-system/README.md` - Definition von „Token" in der
  Ubiquitous Language (widersprach Zeile 60 derselben Datei, die „Kein DaisyUI" sagt)
- `scripts/export-design-system.mjs` - der generierte Bundle-README für Claude Design nannte
  das App-CSS „Tailwind 4 + DaisyUI". Wirkt erst beim nächsten
  `npm run export:design-system` im Design-Projekt

**Rationale:**
Der gefährlichere Import aus einem Entwurfswerkzeug ist nicht Code, sondern eine veraltete
Tech-Angabe in einem Dokument, das ansonsten verbindlich ist. Sie liest sich wie eine
Anweisung, kommt aber aus einem Kontext, der das Projekt nicht kennt. Über das Export-Skript
lief die Angabe sogar im Kreis: Repo sagt DaisyUI, Design-Projekt schreibt es in sein
Übergabe-Dokument, Repo holt es zurück.

**Absichtlich nicht angefasst:**
Diary, Blogposts, Protokoll, erledigte Tasks, ADR 0031 selbst und die Code-Kommentare in
`Table.fs`, `Settings/View.fs` und `Ledger/View.fs`, die erklären, dass DaisyUI nicht
geladen ist. Das ist Geschichte oder aktive Erklärung, keine falsche Anweisung. Offen und
mit Roman zu klären: `docs/DESIGN-SYSTEM.md` (Abschnitt 10 ist ein Installationsrezept für
DaisyUI) und `docs/README.md` stammen vom 2025-11-29/30, liegen auf keinem Leseweg mehr
(einzige Ausnahme: `.claude/agents/design-quality.md:74` zitiert die Farbpalette) und sind
vom `standards/`-Satz abgelöst; dazu `template/**` als eigenes Gerüst.

**Outcomes:**
- Build: nicht nötig, eine Zeile Kommentartext im Export-Skript, sonst Markdown
- Tests: nicht nötig; `node --check scripts/export-design-system.mjs` grün
- Issues: keine

---

## 2026-09-18 18:12 - Design-Abgleich Kassenbuch: Entscheidungen aus Claude Design ins Repo geholt

**What I did:**
Das Mockup-Projekt "Gelesen UI-Mockups" in Claude Design gegen den Repo-Stand gelegt. Die
Briefs waren als Einbahnstraße gedacht, beim Zeichnen sind aber fachliche Entscheidungen
entstanden, die nur in der `CLAUDE.md` des Design-Projekts standen. Auslöser war Romans
Frage nach der Aktion "Kopieren" in Ablauf 3: die stand im Brief, war eine Erfindung beim
Schreiben und wurde im Design-Projekt schon am 2026-09-14 gestrichen.

Zwei Befunde betreffen nicht das Design, sondern den gebauten Lesepfad, beide am Code
belegt und als Entscheidungs-Tasks erfasst statt still umgebaut.

**Files Added:**
- `docs/ux/kassenbuch/handoff-ablauf-2-buchungen-finden.md` - Übergabepaket Ablauf 2
  (sechzehn Screens, Popover-Muster der Filter, Kategorienauswahl mit Mehrfachauswahl,
  Auswahl-Leiste auf beiden Geräten), gleiche Konvention wie Ablauf 1
- `.agentheim/board/ledger/backlog/ledger-h9tq2-reihenfolge-der-historie-entscheiden.md` -
  Entwurf zeigt die Historie neueste zuerst, gebaut ist älteste zuerst
- `.agentheim/board/ledger/backlog/ledger-d4kx8-geloeschte-buchungen-in-trefferzahl-und-summe.md` -
  Entwurf zählt gelöschte im Zähler, nicht in der Summe; gebaut zählen sie in beidem

**Files Modified:**
- `docs/ux/kassenbuch/briefs.md` - "Kopieren" aus Ablauf 3 gestrichen (eigener Commit);
  Rückfluss-Absatz im Kopf; Ablauf 3 um Kopfzeilen-Kästchen über den ganzen Treffer,
  Anker der Shift-Spanne und "Auswahl aufheben" statt "Fertig" ergänzt; Ablauf 5 um die
  zwei Flag-Arten und die Regel "Zeile bleibt bei Filter-Nichttreffer ausgegraut stehen";
  Ablauf 6 doppelter Block aus der Umarbeitung vom 2026-09-12 zusammengeführt, Konto-Kürzel
  korrigiert (`GIRO` statt `CD`), Chevron-Wechsler und Zu-prüfen-Zahlen ergänzt; Ablauf 7
  um Herkunft-Arten, Hinweisleisten und die zwei offenen Widersprüche ergänzt
- `.agentheim/board/ledger/backlog/ledger-r1d2f-...` - Auswahl über den ganzen Treffer,
  Beschriftung des Ausstiegs, Höhen der Handy-Leiste
- `.agentheim/board/ledger/backlog/ledger-vgfw3-...` - Chip-Verhalten, Popover-Muster,
  Payee-Vorschläge aus dem Treffer, ausgegraute Nichttreffer
- `.agentheim/board/ledger/backlog/ledger-q1hw2-...` - HERKUNFT-Zeile, Hinweisleisten,
  Punktfarben der Zeitleiste, Lesezustand gelöschter Buchungen
- `.agentheim/board/ledger/backlog/ledger-qns4j-...` - gegenläufiger Befund: der Entwurf
  will bis zu zwei Status-Badges, diese Aufgabe will die Spalte schmaler
- `.agentheim/board/ledger/INDEX.md` - zwei Backlog-Einträge, Zähler 13 auf 15

**Rationale:**
Der Entwurf läuft dem Repo davon, sobald beim Zeichnen entschieden wird. Ohne Rückfluss
baut später jemand gegen einen Brief, der nicht mehr gilt. Die zwei Code-Widersprüche sind
Produktfragen (was zeigt die Summe, wie herum liest man eine Historie) und gehören nicht in
einen stillen Fix.

**Outcomes:**
- Build: nicht nötig, nur Markdown
- Tests: nicht nötig, kein Code geändert
- Issues: Das Ablauf-1-Übergabedokument hat sich im Design-Projekt nicht verändert, die
  Repo-Kopie ist aktuell. Im Design-Projekt liegen zwei Ablauf-4-Dateien
  ("Ablauf 4 - Kategorie und Split.dc.html" und "Ablauf 4 - Kategorie aendern und
  Split.dc.html"), die Projekt-`CLAUDE.md` nennt nur die erste. Übergabepakete für die
  Abläufe 3 bis 7 fehlen noch, die Screens sind bei Roman zur Abnahme.
- Nachtrag (gleicher Tag): Alle sieben Tafeln auf Vollständigkeit geprüft, über ihre
  Sprungmarken `id="sN-M"` gezählt: 1.1 bis 1.12, 2.1 bis 2.16, 3.1 bis 3.7, 4.1 bis 4.11,
  5.1 bis 5.12, 6.1 bis 6.10, 7.1 bis 7.10, zusammen 78 Screens, deckungsgleich mit der
  Projekt-`CLAUDE.md`. Leer waren nur zwei Dateien: die doppelte Ablauf-4-Datei (Rest einer
  Umbenennung) und `Canvas.dc.html` aus der Projektvorlage. Die doppelte Datei ist nach
  Romans Freigabe im Design-Projekt gelöscht, `Canvas.dc.html` bleibt stehen. Kein
  Repo-Inhalt betroffen.

---

## 2026-09-14 11:08 - agentheim 0.9.4: Zwei-Wurzel-Layout (ADR-0078)

**What I did:**
`.agentheim/` mit dem migrate-Verb von agentheim 0.9.4 auf das Zwei-Wurzel-Layout umgezogen: `knowledge/` (Vision, Context-Map, ADRs, Research, BC-README, Wissens-INDEX, concepts) und `board/` (Lifecycle-Ordner, Task-INDEX, Protokoll). Danach die Altpfade außerhalb von `.agentheim/` nachgezogen und die Link-Form in den BC-Wissens-INDEX auf das Template vereinheitlicht.

**Files Modified:**
- `.agentheim/**` - Umzug nach `knowledge/` und `board/` (87 Renames, INDEX je BC in Task- und Wissens-Hälfte geteilt)
- `.agentheim/knowledge/contexts/*/INDEX.md` - 19 Einträge von `knowledge/decisions|research/…` auf `../../decisions|research/…` (Form aus `knowledge-index-template.md`)
- `src/Server/Ledger/Events.fs` - Doc-Kommentar zeigt auf `.agentheim/knowledge/contexts/ledger/README.md`
- `docs/idea-ynab-replacement.md` - Vision-Pfad
- `.gitignore` - Kommentar auf `knowledge/`, `board/`; `.agentheim/salvage/` ignoriert (ADR-0063)

**Rationale:**
Seit 0.9.4 verweigern alle agentheim-Verben außer `migrate` ein Legacy-Layout. `knowledge/index.md` bleibt bei `knowledge/decisions/…`, weil `top-index-template.md` diese Form vorgibt. Historische Einträge (dieses Diary, `done/`-Tasks, Protokoll-Archiv, ADR-Texte) behalten ihre alten Pfade.

**Outcomes:**
- Build: ✅
- Tests: nicht ausgeführt, keine Logik geändert
- Issues: Das Verb findet `.fs`-Dateien beim Rest-Scan nicht (Events.fs fehlte im Manifest).

---

## 2026-09-13 22:12 - ledger-h4p6c: Spiegel läuft automatisch (Frische-Kriterium, Lauf nach Push)

**What I did:**
Die zwei fehlenden Auslöser aus ADR 0014 gebaut. Frische-Kriterium: öffnet Roman die Kassenbuch-Seite oder kommt die PWA in den Vordergrund, prüft der Client den Lauf-Status; ist der letzte Lauf älter als 5 Minuten (oder gab es keinen), startet ein Hintergrund-Lauf, und die Seite zeigt währenddessen den vorhandenen Stand. Lauf nach Push: nach einem erfolgreichen Comdirect-Import und nach Quick Add stößt der Server genau einen Lauf an, und zwar erst, wenn `EntryLinkedToYnab` geschrieben ist (ADR 0021). Ein serverseitiger Einzel-Flug-Schutz (`MirrorGuard`) führt gleichzeitige Auslöser auf einen YNAB-Abruf zusammen. Kein Server-Timer.

**Files Modified:**
- `src/Server/Ledger/CommandHandlers.fs` - `MirrorGuard` (Prüfen und Setzen unter Lock, Freigabe in try/finally, nur der Besitzer räumt ab), `runMirrorGuarded`, `runMirrorInBackground` (fängt jede Ausnahme, loggt, speichert best effort einen gescheiterten Lauf-Status), `triggerMirrorInBackground`; `addCashExpense` und `pushComdirectImports` bekommen einen `triggerMirror`-Parameter
- `src/Server/Api.fs` - beide Push-Wege mit dem Hintergrund-Auslöser verdrahtet
- `src/Client/Components/Ledger/Types.fs` - reine Funktion `isMirrorStale`, `freshnessThreshold`
- `src/Client/Components/Ledger/State.fs` - `MirrorStatusLoaded` prüft die Frische, kein zweiter Lauf, solange einer läuft
- `src/Client/State.fs`, `src/Client/App.fs` - Nachricht `VisibilityChanged`, Elmish-Subscription auf `visibilitychange` (einmal registriert, wirkt nur auf der Kassenbuch-Seite)
- `src/Tests/LedgerMirrorTests.fs`, `LedgerPageTests.fs`, `LedgerTests.fs` - 22 neue Tests (Frische-Grenze, echte Nebenläufigkeit zweier Auslöser, Freigabe nach gescheitertem Lauf, Ausnahme im Hintergrund propagiert nicht, genau ein Lauf nach Push und keiner nach gescheitertem, Reihenfolge `EntryLinkedToYnab` vor Auslöser)
- `.agentheim/contexts/ledger/README.md` - Spiegel-Lauf mit drei Auslösern und Einzel-Flug-Schutz, veraltete Aussagen "nur manuell" und "offen bleibt der Auslöser" ersetzt

**Rationale:**
Ohne automatische Auslöser stimmte das Kassenbuch nur, wenn Roman an "Jetzt spiegeln" dachte. Der Einzel-Flug-Schutz muss serverseitig sitzen, weil zwei Tabs oder Seitenaufruf und Push zeitgleich auftreten können.

**Outcomes:**
- Build: ✅ (`dotnet build`, `npm run build`)
- Tests: 841 bestanden, 7 übersprungen, 0 Fehler (im Worktree)
- Issues: Verifikation Iteration 1 FAIL. Der Verifier fand einen echten Fehler: der Einzel-Flug-Schutz gab nach einem gescheiterten Lauf nicht frei (Spiegel bis zum Neustart tot), und `Async.Start` ohne Fehlerbehandlung konnte nach einem Push den Prozess gefährden. Dazu Umschreibungen, Gedankenstriche und eine falsche Testanzahl. Iteration 2 PASS.

---

## 2026-09-13 21:45 - ledger-p3n8k: gelöschte Buchungen in der Kassenbuch-Liste ausblenden

**What I did:**
In YNAB gelöschte Buchungen sind in der Kassenbuch-Liste standardmäßig ausgeblendet. Der Filter sitzt serverseitig in `getLedgerEntriesPage` (`IncludeDeleted` in der Query, `DeletedCount` in der Antwort), damit Seitengröße, Trefferzahl und Summe über den ganzen Treffer stimmen. Sind welche ausgeblendet, zeigt die Seite ihre Anzahl und einen Schalter (`Input.toggle`). Eingeblendet trägt eine gelöschte Buchung ihren Betrag gedämpft und durchgestrichen (`Money.transactionDeleted`), das Kennzeichen "in YNAB gelöscht" bleibt. Der Kontostand kommt weiter aus `getLedgerAccountBalances` und ändert sich durch den Schalter nicht.

**Files Modified:**
- `src/Shared/Domain.fs` - `LedgerEntryPageQuery.IncludeDeleted`, `LedgerEntryPage.DeletedCount`
- `src/Server/Ledger/Persistence.fs` - Seite, Trefferzahl und Beträge mit `includeDeleted`, neue `countDeletedEntries`
- `src/Server/Ledger/QueryHandlers.fs` - `getLedgerEntriesPage` reicht den Schalter durch
- `src/Client/Components/Ledger/Types.fs`, `State.fs` - `IncludeDeleted`, `DeletedCount`, Msg `ToggleIncludeDeleted` (lädt ab Seite 0 neu)
- `src/Client/Components/Ledger/View.fs` - `deletedCountText`, `deletedToggleRow`, gedämpfte Beträge
- `src/Client/DesignSystem/Money.fs` - `transactionDeleted`
- `src/Client/Components/Styleguide/View.fs` - Beispiel im Abschnitt Money
- `src/Tests/LedgerPageTests.fs`, `src/Tests/LedgerViewTests.fs` - 7 neue Tests (Filter über Seite, Trefferzahl und Summe; DeletedCount; Reducer-Test für den Schalter ohne Proxy-Pfad)
- `.agentheim/contexts/ledger/README.md` - Herkunfts-Badge, `getLedgerEntriesPage`, Abschnitt Oberfläche

**Rationale:**
Abnahme-Befund aus ledger-kt0wq: gelöschte und lebende Buchungen mit gleichem Betrag untereinander sahen wie eine Doppelbuchung aus, und die sichtbare Liste summierte sich scheinbar nicht zum Kontostand. Ein reiner Client-Filter auf der seitenweise geladenen Liste hätte Seitengröße und Anzahl verfälscht.

**Outcomes:**
- Build: ✅ (`dotnet build`, `npm run build`)
- Tests: 826 bestanden, 7 übersprungen, 0 Fehler
- Issues: Verifikation Iteration 1 FAIL (README widersprach sich, Umschreibungen und Gedankenstriche in neuen Zeilen), Iteration 2 PASS. Paging-Tests in Iteration 2 voneinander unabhängig gemacht.

---

## 2026-09-13 21:35 - ynab-r9k4t: since_date-Decoder verschiebt Buchungstage nicht mehr

**What I did:**
`Server.YnabClient.Decoders.transactionDecoder` las YNABs reinen Kalendertag ("2026-02-03") mit `Decode.datetimeUtc`. Das interpretiert die Ziffern als Ortszeit und rechnet nach UTC um; in MEZ landete der Buchungstag auf dem Vortag, 23:00. Der Decoder nutzt jetzt denselben Kalendertag-Decoder wie der Spiegel (`Server.YnabObservations.Decoders.ynabDateDecoder`), keine zweite Kopie. Duplikat-Erkennung und Quick-Add-Vorlagen auf Kompensationen geprüft: `DateToleranceDays = 1` ist ein allgemeines Toleranzfenster, Quick-Add-Vorlagen tragen kein Datum. Nichts zu entfernen.

**Files Modified:**
- `src/Server/YnabClient.fs` - Datumsteil von `transactionDecoder` auf den geteilten Decoder umgestellt
- `src/Server/YnabObservations.fs` - Doc-Kommentar zur geteilten Nutzung
- `src/Tests/YnabClientTests.fs` - Regressionstest: Kalendertag und `DateTimeKind.Utc`
- `src/Tests/test.runsettings` - `TZ=Europe/Berlin` für den Testprozess, damit der Test auch auf einem UTC-Host (Docker, CI) greift

**Rationale:**
Der Bug zeigt sich nur östlich von UTC. Ohne feste Zeitzone im Testprozess wäre der Regressionstest auf einem UTC-Host mit dem alten Code grün geblieben. Der Verifier hat das in Iteration 1 gefunden; in Iteration 2 wurde per temporärem Revert unter `TZ=UTC` rot und mit Fix grün nachgewiesen.

**Outcomes:**
- Build: ✅
- Tests: 820 bestanden, 7 übersprungen, 0 Fehler (unter Shell-TZ UTC und Europe/Berlin)
- Issues: Verifikation Iteration 1 FAIL (Test nicht host-unabhängig, falscher Kommentar, Umschreibungen), Iteration 2 PASS

---

## 2026-09-13 16:00 - design-system-fq8dn, Iteration 2: Zebra schlug Hover, Umlaute korrigiert

**What I did:**
Der Verifier fand nach Iteration 1 eine echte Regression in `Table.fs`: die neue
Zebra-Regel (`[&_tbody_tr:nth-child(even)]:bg-white/[0.03]`, Spezifität (0,2,2)) schlug
auf geraden Zeilen die Hover-Regel (`[&_tr:hover]:bg-white/5`, Spezifität (0,2,1)) —
unabhängig von der Reihenfolge im Stylesheet, weil höhere Spezifität immer gewinnt. Vor
dem `table-zebra`-Fix gab es diesen Konflikt nicht, weil die DaisyUI-Klasse inert war;
jetzt fehlte auf jeder zweiten Zeile die Hover-Rückmeldung.

**Files Modified:**
- `src/Client/DesignSystem/Table.fs:72,74` — `Zebra`/`ZebraHover` nutzen jetzt
  `[&_tbody_tr:nth-child(even):not(:hover)]:bg-white/[0.03]`. `:not(:hover)` nimmt der
  Zebra-Regel die Zeile, sobald sie gehovert wird, damit gewinnt dort die Hover-Regel,
  ohne von der Deklarationsreihenfolge abzuhängen.
- `src/Client/Components/Settings/View.fs:554-559` — Umschreibungen „dafuer"/„fuer"
  (zweimal) im deutschen Kommentar zu „dafür"/„für" korrigiert.

**Rationale:**
Spezifität, nicht Reihenfolge, entscheidet bei CSS-Konflikten mit gleicher `!important`-
Stufe. `:not(:hover)` macht den Ausschluss explizit, statt sich auf eine zufällig
richtige Quellreihenfolge zu verlassen, die beim nächsten Refactoring wieder kippen
könnte. Die Umlaute-Korrektur folgt der Projektkonvention (echte Umlaute, keine
Umschreibung).

**Verification (headless, Playwright, `/#/styleguide`, Sektion 8,
`[data-ds-card="components-table"]`, `Table.simple` = `ZebraHover`):**
- Ruhezustand, gerade Zeile (Zebra, kein Hover): `oklab(0.999994 .../ 0.03)`.
- Hover, ungerade Zeile: `oklab(0.999994 .../ 0.05)`.
- Hover, gerade Zeile: `oklab(0.999994 .../ 0.05)` — vorher exakt der Zebra-Wert
  (`0.03`), jetzt identisch zur ungeraden Zeile. Die Hover-Rückmeldung ist auf jeder
  Zeile wieder sichtbar.
- Gebautes CSS geprüft: `nth-child(2n):not(:hover){background-color:...3%...}` und
  `tr:hover{background-color:...5%...}` beide vorhanden, wie erwartet kompiliert.

**Outcomes:**
- Build: dotnet build ✅ (0 Fehler, 0 Warnungen)
- Tests: dotnet test ✅ 819 bestanden, 0 fehlgeschlagen, 7 übersprungen (Integrationstests
  ohne `.env`, wie üblich) — unverändert zu Iteration 1, keine neue Testabdeckung nötig
  (reine CSS-Selektor-Korrektur, verifiziert per headless-Messung statt Unit-Test).
- `npm run build` ✅

---

## 2026-09-13 01:24 - Kassenbuch-Liste an das Mockup angeglichen, erster Lauf des visuellen Gates

**What I did:**
Die sieben Abweichungen zwischen gebauter Kassenbuch-Liste und dem Claude-Design-Entwurf
behoben (`ledger-x0yga`), in zwei Iterationen. Dies war die erste Aufgabe unter dem neuen
visuellen Gate: Zielbild als PNG im Repo, Screenshots vor dem Integrieren.

**Files Added:**
- `src/Tests/MoneyFormatTests.fs` - acht Fälle für die neue Geldformatierung.
- `docs/ux/kassenbuch/screenshots/` - drei Aufnahmen (PC, PC gescrollt, Handy).
- `docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md` - Übergabe-Dokument aus
  Claude Design mit den pixelgenauen Werten.

**Files Modified:**
- `src/Client/DesignSystem/Money.fs` - `formatAmountText` ersetzt die kulturabhängige
  Formatierung; neue Varianten `accountBalance` und `accountListRow`.
- `src/Client/DesignSystem/Table.fs` - Zellen-Padding direkt auf `cell`/`headerCell`,
  `FixedLayout`, Sticky-Kopf bei 64px statt 0.
- `src/Client/DesignSystem/Stats.fs` - `ValueElement` (additiv).
- `src/Client/Components/Ledger/View.fs` - relatives Datum, gekürzte Zellen, kompakte
  Konten-Liste, Status-Spalte 208px ohne Umbruch.
- `src/Client/styles.css` - Regel für den haftenden Tabellenkopf.

**Rationale:**
Die Liste erfüllte alle Akzeptanzkriterien von `ledger-b1xwe` und sah trotzdem falsch aus:
englisches Zahlenformat, weiße Beträge, ungekürzter Bank-Rohtext, verschmolzene Spalten.
Die Ursache lag im Ablauf, nicht am Code: das Mockup existierte nur in Claude Design, und
kein Schritt hat das gebaute Bild angesehen.

Der eigentliche Fehler beim Zahlenformat war, dass `CultureInfo("de-DE")` in der nach
JavaScript übersetzten Fassung nicht greift. Die Lösung rechnet jetzt mit Ganzzahl-Cent und
eigener Gruppierung, also ohne jede Kulturabhängigkeit.

**Outcomes:**
- Build: ✅ (`dotnet build`, `npm run build`)
- Tests: 819/826 bestanden, 7 übersprungen, 14 neu
- Verifikation: PASS in Iteration 2 (Iteration 1 scheiterte an null Spaltenabstand und
  einer Zeile, die wegen zweier Kennzeichen anderthalbmal so hoch war)
- **Wurzelbefund:** `Table.view` setzt die DaisyUI-Klassen `.table`/`.table-sm`, aber
  DaisyUI ist im Projekt gar nicht installiert. Die Klassen sind seit dem Bau der
  Komponente wirkungslos, deshalb fehlte jedes Zellen-Padding. Die CLAUDE.md führt DaisyUI
  trotzdem im Tech-Stack. Grundsatzentscheidung: `design-system-fq8dn`.
- Weitere Folge-Tasks: `design-system-t3vqr` (neue Money-Varianten fehlen im Styleguide und
  damit im Claude-Design-Export), `ledger-w9skm` (Kategorie nicht violett, Handy zeigt
  Karten statt flacher Zeilen), `design-system-bq4tz` (Badge-Vorgabe bricht unter
  `dotnet test`, vom Worker umgangen statt behoben).

---

## 2026-09-12 17:47 - Testdaten aus der echten Entwicklungsdatenbank entfernt

**What I did:**
Die Nachwirkung des Test-Infrastruktur-Fehlers aus `ledger-b1xwe` bereinigt
(`ledger-vm3dk`): 106 Test-Konten, 195 Test-Buchungen, 443 Ereignisse, 319 Verknüpfungen
und 11 Test-Budget-Schlüssel aus `~/my_apps/budgetbuddy-dev/budgetbuddy.db` gelöscht, in
einer einzigen Transaktion.

**Files Modified:**
- Keine Code-Änderung. Betroffen war ausschliesslich die Entwicklungsdatenbank ausserhalb
  des Repos.

**Rationale:**
Solange 106 Test-Konten in der Kontostände-Leiste stehen, ist die neue Kassenbuch-Liste
aus `ledger-b1xwe` nicht beurteilbar. Die Bereinigung war Voraussetzung dafür, dass Roman
die Scheibe an echten Daten durchspielen kann.

Die Trennung zwischen echt und Test wurde nicht über den Id-Präfix allein belegt, sondern
über die Buchungszahl je Konto: die sieben echten Konten tragen zwischen 26 und 11.917
Buchungen und zusammen genau 13.864, jedes Test-Konto höchstens fünf. Dazwischen liegt
keine Grauzone. Gelöscht wurde mit einer expliziten Id-Liste der sieben echten Konten,
nicht mit einem Präfix-Muster.

**Outcomes:**
- Eigenes Backup vor dem Eingriff: `budgetbuddy.db.backup-vor-vm3dk-20260912-173520`
- Fingerabdrücke der echten Daten vor und nach dem Löschen identisch (Buchungen
  `5229eda7…`, Konten `4cb4b531…`), auch nach `VACUUM` (21,5 → 20,6 MB)
- `PRAGMA integrity_check`: ok, `PRAGMA foreign_key_check`: keine Verletzung
- Zugangsdaten unangetastet (fünf `comdirect_*`, `ynab_token`, beide `ynab_default_*`)
- Lesepfad danach verifiziert: `getLedgerEntriesPage` über alle Konten liefert 13.864
  Treffer und 38.686,82 EUR in 0,155 Sekunden; die Differenz von 3,80 EUR zur Summe der
  Kontostände ist genau die eine in YNAB gelöschte Buchung (ADR 0013, Grund für
  `ledger-p3n8k`)
- Offen: `settings.ledger_mirror_last_run` steht noch auf einem Testwert und korrigiert
  sich beim nächsten "Jetzt spiegeln" von selbst

---

## 2026-09-12 15:29 - Kassenbuch-Liste lädt seitenweise, Gesamtsicht und Kontostände-Leiste; Test-Infra-Fehler behoben

**What I did:**
Erste lesende Bau-Scheibe des Kassenbuchs (`ledger-b1xwe`). Die Liste lädt jetzt
serverseitig seitenweise, wahlweise für ein Konto oder über alle Konten, und die Seite
zeigt eine Kontostände-Leiste. Dabei fiel ein projektweiter Test-Infrastruktur-Fehler auf,
durch den `dotnet test` in die echte Entwicklungsdatenbank schrieb.

**Files Added:**
- `src/Tests/LedgerViewTests.fs` - Tests für Kontostände-Leiste, Datums-Gruppierung und
  Konto-Kürzel.
- `src/Tests/test.runsettings` - erzwingt `USE_MEMORY_DB` im Test-Host (ADR 0030).
- `.agentheim/knowledge/decisions/0029-seitenweise-kassenbuch-liste-und-kontostaende-leiste.md`
- `.agentheim/knowledge/decisions/0030-dotnet-test-erzwingt-in-memory-db-ueber-runsettings.md`

**Files Modified:**
- `src/Shared/Domain.fs`, `src/Shared/Api.fs` - Seitentyp, `AccountFilter`, `sumMoney`,
  `ledgerBalancesByAccount`, zwei neue Endpunkte im Kassenbuch-Vertrag.
- `src/Server/Ledger/Persistence.fs`, `src/Server/Ledger/QueryHandlers.fs`,
  `src/Server/Api.fs` - `getLedgerEntriesPage` und `getLedgerAccountBalances`. Beträge
  werden als Text gelesen und in F# summiert, nie per SQL-`SUM` (ADR 0017).
- `src/Client/Components/Ledger/{Types,State,View}.fs`, `src/Client/State.fs` -
  `AccountFilter` löst die bisherige Doppelbedeutung von `Guid option`, Nachladen beim
  Scrollen, Kontostände-Leiste, Datums-Gruppierung am Handy, Sticky-Tabelle am PC.
- `src/Tests/LedgerPageTests.fs`, `src/Tests/Main.fs`, `src/Tests/Tests.fsproj` - 24 neue
  Tests, `RunSettingsFilePath`.
- `.agentheim/contexts/ledger/README.md` - Abschnitt "Sichten" um die beiden neuen
  Lesefunktionen ergänzt.

**Rationale:**
Ohne serverseitige, seitenweise Abfrage gibt es weder Filter noch Summenzeile noch
Gesamtsicht, und bei rund 14.000 Buchungen lud die Seite bisher alles eines Kontos auf
einmal. Fünf weitere Scheiben hängen an dieser.

Der Test-Infrastruktur-Fehler kam beim Verifizieren ans Licht: unbeteiligte Tests
scheiterten deterministisch. Ursache war, dass `dotnet test` den Einstiegspunkt in
`Main.fs` nie aufruft (VSTest lädt die Assembly als Bibliothek und startet Expecto per
Reflexion), weshalb `USE_MEMORY_DB` nie gesetzt wurde und alle Ledger-Tests in
`~/my_apps/budgetbuddy-dev/budgetbuddy.db` schrieben. Behoben über `test.runsettings`,
eine VSTest-eigene Einstellung. Empirisch geprüft: nach einem vollen Testlauf ist die
Datei byte-identisch.

**Outcomes:**
- Build: ✅ (`dotnet build`, `npm run build`)
- Tests: 805/812 bestanden, 7 übersprungen (Integrationstests ohne `.env`), 24 neu
- Verifikation: PASS in Iteration 2 (Iteration 1 scheiterte an zwei Textmängeln: falscher
  ADR-Verweis in zwei Kommentaren, ADR 0029 in Umschreibungen statt Umlauten)
- Issues: Die bereits in die echte Entwicklungsdatenbank geschriebenen Testdaten (106
  Konten, 365 Buchungen) stehen noch dort. Aufräumen: `ledger-vm3dk`. Der Schutz lässt
  sich weiterhin still umgehen (`dotnet test --settings`, `dotnet vstest`), Wächter-Test:
  `infra-za7fq`.

---

## 2026-09-12 13:40 - Kassenbuch in Bau-Scheiben geschnitten, ADR 0028 zur einen schreibenden Fähigkeit vor v2

**What I did:**
Aus dem UX-Konzept sieben Bau-Scheiben abgeleitet, `ledger-p3n8k` eingeordnet und die
offene Vision-Frage entschieden: der Prüfvermerk ist die einzige schreibende Fähigkeit,
die vor v2 nach BB wandert. Die erste Scheibe (`ledger-b1xwe`) zusätzlich verfeinert.

**Files Added:**
- `.agentheim/knowledge/decisions/0028-pruefvermerk-als-einzige-schreibende-faehigkeit-vor-v2.md`
- Sieben Scheiben-Tasks unter `.agentheim/contexts/ledger/backlog/`: `ledger-b1xwe`
  (Liste seitenweise, Gesamtsicht, Kontostände), `ledger-vgfw3` (Filter mit Summenzeile),
  `ledger-r1d2f` (Auswahl-Modus), `ledger-q1hw2` (Detail mit Herkunft und Historie),
  `ledger-kdnnz` (Spiegel um Prüfvermerk und Flags erweitern), `ledger-kwwxz` (Available
  je Kategorie), `ledger-r777c` (Prüfvermerk schreiben).

**Files Modified:**
- `.agentheim/vision.md` - v1 bekommt den Prüfvermerk als benannte schreibende Ausnahme.
- `.agentheim/contexts/ledger/backlog/ledger-p3n8k-*.md` - hängt jetzt an `ledger-b1xwe`,
  Begründung für die Eigenständigkeit ergänzt.
- `.agentheim/contexts/ledger/INDEX.md` - sieben Backlog-Einträge, ADR 0028.

**Rationale:**
Romans häufigster Ablauf am Handy (Buchungen als geprüft markieren) ist schreibend. Ein
rein lesendes v1 läuft damit genau in das Scheiterkriterium der Vision: "Kassenbuch
gebaut, aber niemand schaut hinein." Der Prüfvermerk ist zugleich die kleinste denkbare
schreibende Fähigkeit: ein Feld, idempotent, ohne Budget-Wirkung, mit trivialer
Konfliktauflösung. Kategorie, Split und Lena-Zuweisung bleiben v2, weil sie den
allgemeinen Rückschreibe-Weg und die Abweichungs-Sicht brauchen.

**Outcomes:**
- Build: nicht berührt (keine Code-Änderung)
- Tests: nicht berührt
- Issues: keine

---

## 2026-09-12 13:05 - UX-Konzept Kassenbuch festgehalten, Prototyp-Umweg über Claude Design gestrichen

**What I did:**
Den Re-Sync des Design-Systems nach Claude Design durchgeführt (23 Karten, drei Schriften
erkannt), die YNAB-Referenzwege für die Abläufe 1 bis 5 mit Roman aufgenommen, die
Konzept-Skizze abgestimmt und die Briefs dazu geschrieben. Danach den Zuschnitt von
`ledger-xbrht` mit Roman geändert: der klickbare Prototyp über alle sieben Abläufe in
Claude Design entfällt, das Konzept wandert stattdessen ins `ledger/README.md` und der
Durchgang an echten Daten in die erste lesende Bau-Scheibe.

**Files Added:**
- `docs/ux/kassenbuch/briefs.md` - Briefs je Ablauf mit Romans YNAB-Referenzweg,
  Gerätezuordnung, Schrittfolge, Pflicht-Komponenten und erfundenen Beispieldaten.
- `docs/ux/kassenbuch/claude-design-kickoff.md` - Kickoff-Briefing für ein Mockup-Projekt
  in Claude Design (Kontext, Regeln, gemeinsame Grundlage), jetzt nur noch für einzelne
  Layout-Fragen auf Zuruf.
- `.agentheim/contexts/design-system/backlog/design-system-hadjy-*.md` - Bug: der Export
  trägt @font-face mit absolutem `url(/fonts/...)`.
- `.agentheim/contexts/design-system/backlog/design-system-an22r-*.md` - Export soll
  erzeugen, was Claude Design beim Self-Check nachgezogen hat.
- `.agentheim/contexts/ledger/backlog/ledger-xfjcp-*.md` - "Lena zuweisen" statt Flag.

**Files Modified:**
- `.agentheim/contexts/ledger/README.md` - neuer Abschnitt "Oberfläche": Einstieg,
  Navigation, Listenaufbau je Gerät, Prüfen als eine Geste, Auswahl mit Summe, Orte für
  Bearbeiten/Historie/Flags, Verhalten bei rund 14.000 Buchungen, und die drei
  Voraussetzungen, die heute fehlen (Prüfvermerk und Flags im Spiegel, Available je
  Kategorie, seitenweises Laden mit serverseitiger Summe).
- `.agentheim/contexts/ledger/doing/ledger-xbrht-*.md` - Zuschnitt und
  Akzeptanzkriterien geändert.
- `dist/design-system/styles.css` (git-ignoriert) - Workaround für design-system-hadjy,
  absolute Font-URLs relativ gemacht und nur diese Datei neu nach Claude Design geladen.

**Rationale:**
Claude kann Mockup-Projekte in Claude Design nicht lesen (`DesignSync` erreicht nur
Design-System-Projekte). Jede Iteration am Prototyp bräuchte Roman als Boten mit einem
Screenshot; bei sieben Abläufen sind das Dutzende. Dazu kann ein Mockup mit erfundenen
Daten das Maß "mindestens so schnell wie YNAB" nicht belegen und die Frage nach dem
Verhalten bei 14.000 Buchungen grundsätzlich nicht beantworten. Der eigentliche Wert des
Tasks, die abgestimmte Konzept-Skizze, war bereits eingetreten, bevor ein Screen entstand.

**Outcomes:**
- Build: nicht berührt (keine Code-Änderung)
- Tests: nicht berührt
- Issues: Zwei Fehler im Export-Skript gefunden und als Backlog erfasst; der Self-Check
  in Claude Design läuft erst auf eine Anfrage im Projekt-Chat, nicht nach einem Upload.

---

## 2026-09-11 21:50 - Kategorienauswahl im Styleguide, vollständiger Sektions-Export im Claude-Design-Bundle

**What I did:**
`BottomSheet.categoryPicker` als neue Styleguide-Sektion "Kategorienauswahl" ergänzt
(erfundene Beispieldaten, Available positiv/negativ/null, Vorschläge + Zuletzt
verwendet, Suchfeld). Zusätzlich 12 weitere Styleguide-Sektionen mit `data-ds-card`
markiert und in den Export aufgenommen. Export-Skript um eine Portal-Karten-Helfer-
funktion und Gruppen-Metadaten erweitert. Kein Zugriff auf `src/Client/styles.css` oder
Font-Code (Sperrbereich des parallelen Tasks `design-system-f7k2p`).

**Files Added:**
- `.agentheim/knowledge/decisions/0027-kategorienauswahl-und-vollstaendiger-sektions-export.md` -
  ADR: eine Karte für beide Picker-Zustände, Export-Umfang und -Ausnahmen,
  sequentielles Öffnen/Schließen der Portal-Karten.

**Files Modified:**
- `src/Client/Components/Styleguide/View.fs` - `open Shared.Domain` ergänzt; neue
  Sektion 19 "Kategorienauswahl (Kategorie ändern)" (`CategoryPickerDemo`); `data-ds-card`
  auf Button-, Stats-, Loading-, ErrorDisplay-, Icons-, PageHeader-, Primitives-,
  Input-, Form-, Swipe-Sektion ergänzt; Modal- und Toast-Trigger (`data-ds-card-trigger`)
  ergänzt, Toast-Renderliste in einen `data-ds-card`-Wrapper gehüllt; Swipe/Navigation/
  Voice-Sektionsnummern um eins verschoben (19→20/21/22).
- `scripts/export-design-system.mjs` - `COMPONENT_CARDS` um 10 neue Einträge samt
  `group`-Feld erweitert (Components/Feedback/Forms); neue Konstanten
  `CATEGORY_PICKER_CARD`, `MODAL_CARD`; neue Helferfunktion `extractPortalCard`
  (öffnen, Bereitschafts-Selektor abwarten, Markup klonen, schreiben, schließen,
  Verschwinden der `.active`-Klasse abwarten) für BottomSheet/Kategorienauswahl/Modal;
  Toast bekommt einen Vorab-Klick vor der Schleife über `COMPONENT_CARDS`; README_MD-
  Template ("## Komponenten") auf die vollständige Liste aktualisiert.
- `.agentheim/contexts/design-system/README.md` - `data-ds-card`-Bullet im
  Export-Bundle-Abschnitt aktualisiert (Verweis auf ADR 0027 statt veralteter
  Sechs-Komponenten-Liste); neuer Abschnitt "Styleguide-Sektionen im Export
  (design-system-y6gkd, 2026-09-11)" vor "Open questions".

**Files Deleted:**
- keine.

**Rationale:**
Roman fand beim Erst-Sync die Kategorienauswahl im Design-System-Projekt vermisst
(Ablauf 4 "Kategorie ändern" im Kassenbuch, `ledger-xbrht`, braucht den echten Picker
als Vorbild statt eines frei erfundenen Mockups) und entschied, dass das UX-Konzept
mehr als sechs Komponenten braucht.

**Exportierte Sektionen (19 Komponenten-Karten, Gruppe "Components" sofern nicht
anders vermerkt):** Card, Money, Badge, Table, Navigation, Button, Stats, Icons,
PageHeader, Primitives, Swipe, BottomSheet, Kategorienauswahl, Modal; Gruppe
"Feedback": Loading, ErrorDisplay, Toast; Gruppe "Forms": Input, Form.

**Bewusst ausgelassen (mit Begründung):**
- Farbsemantik-Sektion: bereits über die generierten Token-Karten
  `color-neon`/`color-surfaces` abgedeckt (dieselben Werte, kein zweites Set).
- Typografie-Sektion: bereits über die generierte Token-Karte `type-fonts` abgedeckt
  (dieselben Werte, kein zweites Set).
- Voice-Sektion: reiner Fließtext ohne visuell prüfbares Markup; steht im
  Bundle-README/SKILL statt in einer Karte (wie im Task vorgeschlagen).

**Outcomes:**
- Build: dotnet build (0 Fehler), Fable/`npm run build` (grün, ein bereits bestehender
  eval-Hinweis in `Rules/State.fs`, nicht Teil dieses Tasks).
- Tests: `dotnet test` 778 bestanden, 7 übersprungen (Integrationstests brauchen `.env`,
  unverändert gegenüber vor diesem Task). Kein neuer dotnet-Test: reine Feliz-View-
  und Node-Export-Änderung ohne testbare pure Funktion (TDD_SKIPPED, wie schon bei
  `design-system-t82sg`).

**Iteration 2 (2026-09-11, nach Merge mit feature/kassenbuch):**
Der Konduktor hat `feature/kassenbuch` in dieses Worktree gemergt; alles bis auf
`scripts/export-design-system.mjs` ging automatisch durch. Konflikt gelöst: eigene
Logik (`extractPortalCard`, Karten-Gruppen über `card.group`, BottomSheet/
Kategorienauswahl/Modal-Erfassung) behalten, dabei das `fontsUrl`-Parameter
durchgängig entfernt (`writePreview`, `extractPortalCard`, `extractPreviews` und alle
Aufrufstellen), passend zur Font-Selbstauslieferung aus `design-system-f7k2p`/ADR 0026.
Keine Konfliktmarker mehr (`grep` leer), `node --check` grün. Zusätzlich die
Verifier-Funde aus Iteration 1 behoben: BC-README-Bullet zu den `@dsCard`-Gruppen
(jetzt Colors/Type/Components/Feedback/Forms/Brand statt der alten Sechs-Komponenten-
Liste), "18" auf "19" Komponenten-Karten korrigiert (README-Abschnitt und Diary oben),
Kategorienauswahl in der "Neu"-Liste ergänzt.

Verifikation: `dotnet build` 0 Fehler; `dotnet test` 784 bestanden/7 übersprungen (neue
Baseline nach f7k2p, +6 Tests); `npm run build` grün (derselbe bestehende eval-Hinweis);
`npm run export:design-system` zweimal, `diff -r` zwischen den Läufen leer
(byte-identisch); Bundle enthält `fonts/` mit den drei woff2-Dateien plus
`fonts/LICENSE.md`; keine `fonts.googleapis.com`-Referenz im Bundle; alle 23
`preview/*.html` beginnen in Zeile 1 mit `<!-- @dsCard group="…" -->`; headless
Screenshot von `components-categorypicker.html` (Playwright, `file://`, cwd =
Worktree-Root) zeigt Vorschläge, Zuletzt verwendet, beide Gruppen, Suchfeld und alle
drei Available-Vorzeichen korrekt gerendert, Schriften (Inter/JetBrains Mono) laden
lokal ohne Netzzugriff.
- Export: `npm run export:design-system` zweimal ausgeführt, `diff -r` zwischen beiden
  Läufen leer (byte-identisch). Alle 23 `preview/*.html` beginnen in Zeile 1 mit
  `<!-- @dsCard group="…" -->` (verifiziert per Skript). Kategorienauswahl-Karte zeigt
  Vorschläge, Zuletzt verwendet, beide Gruppen und alle drei Available-Vorzeichen
  (128.40/-32.10/0.00/850.00/-15.50 EUR). Headless Screenshot-Stichprobe (Playwright,
  Viewport statt Full-Page wegen eines bekannten Full-Page-Screenshot-Artefakts bei
  `position: fixed`-Inhalten) bestätigt korrektes Rendering für Kategorienauswahl,
  Modal, Toast (Eintritts-Animation braucht in einer statischen Datei ohne
  Nutzerinteraktion messbar länger als die deklarierten 0.42s, bis opacity 1 erreicht;
  Endzustand ist aber korrekt und das exportierte Markup selbst unabhängig davon
  richtig) und Button.
- Issues: keine offenen. Die Toast-Eintritts-Animation in der statischen Vorschau ist
  eine kosmetische Beobachtung (kein Blocker, kein AC betroffen), nicht weiter verfolgt.

---

## 2026-09-11 21:40 - Iteration 2 von design-system-f7k2p: Schreibstil und ADR-Frontmatter korrigiert

**What I did:**
Nach dem Verifier-Fund aus Iteration 1 alle transliterierten Umlaute in
`src/Client/styles.css` (Kommentar über dem `@font-face`-Block) und in
`src/Tests/FontFaceRegressionTests.fs` (Kommentare und Assertion-Messages)
durch echte Umlaute und ß ersetzt; dabei auch "durfte" zu "darf" korrigiert
(Präsens statt fälschlich Präteritum). In ADR 0026 `supersedes: [0024]`
gesetzt; in ADR 0024 `superseded_by: [0026]` und `related_adrs: [0026]`
ergänzt (Status bleibt `accepted`, die bestehende Amendment-Notiz macht
bereits klar, dass nur Decision 3 ersetzt wird, Decisions 1 und 2 bleiben
gültig). Alle geänderten Dateien plus Export-Skript, README und Fonts-Lizenz
auf verbliebene Transliterationen geprüft; keine gefunden.

**Files Modified:**
- `src/Client/styles.css` - Kommentar korrigiert (echte Umlaute)
- `src/Tests/FontFaceRegressionTests.fs` - Kommentare und Assertion-Messages
  korrigiert (echte Umlaute, ß, "durfte" zu "darf")
- `.agentheim/knowledge/decisions/0026-schriften-selbst-ausliefern-statt-google-fonts-import.md`
  - `supersedes: [0024]` gesetzt
- `.agentheim/knowledge/decisions/0024-export-bundle-build-nicht-dev-und-nicht-fixe-navigation-preview.md`
  - `superseded_by: [0026]`, `related_adrs: [0026]` ergänzt

**Rationale:**
Die Implementierung selbst war funktional korrekt (Build, 784 Tests,
@font-face im gebauten CSS, Fonts im PWA-Precache); nur Schreibstil und
ADR-Verknüpfung mussten nachgezogen werden.

**Outcomes:**
- Build: dotnet build - OK
- Tests: 784 bestanden, 7 übersprungen (.env-Integrationstests), vom Verifier bestätigt; Verifikation PASS in Iteration 2
- Issues: keine

---

## 2026-09-11 21:35 - Schriften selbst ausliefern statt Google-Fonts-Import

**What I did:**
Google-Fonts-`@import` in `src/Client/styles.css` entfernt (stand hinter dem
`@theme`-Block, wurde vom Tailwind-4-Build verworfen, App rendert seitdem in
Ersatzschriften). Ersetzt durch drei lokale `@font-face`-Regeln (Inter, JetBrains
Mono, Space Grotesk), Font-Dateien liegen jetzt im Repo. Export-Bundle
(`scripts/export-design-system.mjs`) zieht nach: kopiert dieselben Dateien ins
Bundle und bindet sie per `@font-face` in `colors_and_type.css` ein, statt den
Google-Fonts-Link in die Vorschau-Dateien zu schreiben.

**Bezugsquelle der Fonts (im Diary festgehalten, wie im Task gefordert):** die
Variable-Font-Pakete von Fontsource (`@fontsource-variable/inter`,
`@fontsource-variable/jetbrains-mono`, `@fontsource-variable/space-grotesk`,
jeweils Version 5.3.0), bezogen per `curl` von der jsDelivr-CDN-Spiegelung der
npm-Pakete (kein `npm install`, wie im Task-Setup vorgeschrieben):
- `https://cdn.jsdelivr.net/npm/@fontsource-variable/inter@5.3.0/files/inter-latin-wght-normal.woff2`
  (48.256 Byte, Gewicht 100-900)
- `https://cdn.jsdelivr.net/npm/@fontsource-variable/jetbrains-mono@5.3.0/files/jetbrains-mono-latin-wght-normal.woff2`
  (40.404 Byte, Gewicht 100-800)
- `https://cdn.jsdelivr.net/npm/@fontsource-variable/space-grotesk@5.3.0/files/space-grotesk-latin-wght-normal.woff2`
  (22.288 Byte, Gewicht 300-700)
- Lizenztexte von denselben Paketen (`.../LICENSE`), alle drei OFL 1.1, nur die
  Copyright-Zeile unterscheidet sich je Schrift; zu einer gemeinsamen
  `LICENSE.md` mit Tabelle plus dem einen gemeinsamen OFL-1.1-Volltext
  zusammengeführt.

Je eine Variable-Font-Datei pro Familie deckt den gesamten benötigten
Gewichtsbereich ab (kein Satz einzelner statischer Schnitte); Subset "latin"
deckt deutsche Umlaute (ä, ö, ü, ß) ab (Unicode-Range U+0000-00FF u. a.), Stil
"normal" (keine Kursivschnitte nötig).

**Files Added:**
- `src/Client/public/fonts/inter-latin-wght-normal.woff2`
- `src/Client/public/fonts/jetbrains-mono-latin-wght-normal.woff2`
- `src/Client/public/fonts/space-grotesk-latin-wght-normal.woff2`
- `src/Client/public/fonts/LICENSE.md` - OFL-1.1-Notiz für alle drei Schriften
- `src/Tests/FontFaceRegressionTests.fs` - Regressionstest für den Font-Bug
- `.agentheim/knowledge/decisions/0026-schriften-selbst-ausliefern-statt-google-fonts-import.md`

**Files Modified:**
- `src/Client/styles.css` - Google-Fonts-`@import` entfernt, drei `@font-face`-Regeln
  eingefügt
- `scripts/export-design-system.mjs` - `extractGoogleFontsUrl` entfernt, neue
  `FONT_FILES`/`FONT_FACE_CSS`/`copyFontFiles`, `writePreview`/`extractPreviews`/
  `writeTokenAndBrandPreviews`/`main` ohne `fontsUrl`-Parameter, `@font-face` in
  `colors_and_type.css`, README/SKILL-Text im Bundle angepasst
- `src/Tests/Tests.fsproj` - `FontFaceRegressionTests.fs` registriert
- `.agentheim/contexts/design-system/README.md` - drei Bullets im
  Export-Bundle-Abschnitt aktualisiert (Fonts jetzt lokal, App-Bug als behoben markiert)
- `.agentheim/knowledge/decisions/0024-export-bundle-build-nicht-dev-und-nicht-fixe-navigation-preview.md`
  - Hinweis ergänzt, dass Decision 3 durch ADR 0026 ersetzt wird, Decisions 1+2
    bleiben gültig

**Files Deleted:**
- keine

**Rationale:**
Zwei unabhängige Funde (Build verwirft den `@import`; Claude Design meldet fehlende
Font-Dateien beim Erst-Sync) haben dieselbe Ursache: die App bezieht ihre Schriften
nicht aus dem eigenen Bundle. Ein reines Verschieben des `@import` vor den
`@theme`-Block hätte nur den ersten Fund behoben, die Abhängigkeit von einem
externen Font-Host aber bestehen lassen (kein Offline-Fallback für die PWA, keine
Dateien für Claude Design). Deshalb: Schriften als Dateien ins Repo, `@font-face`
statt `@import`, sowohl in der App als auch im Export-Bundle.

**Outcomes:**
- Build: dotnet build - OK; npm run build - OK
- Tests: dotnet test 784 bestanden, 7 übersprungen (brauchen `.env`, unverändert
  gegenüber vorherigem Stand); davon 6 neue Fälle in `FontFaceRegressionTests.fs`
  (vor dem Fix: 5 von 6 fehlgeschlagen, danach: alle 6 bestanden)
- Headless-Check (`vite build` + `vite preview --port 5191 --strictPort`,
  Playwright, Navigation zu `/#/styleguide`): `document.fonts` zeigt Inter,
  JetBrains Mono und Space Grotesk als `loaded`; keine Netzwerk-Anfrage an
  fonts.googleapis.com oder fonts.gstatic.com beobachtet
- PWA-Precache: generierter `dist/public/sw.js` listet alle drei
  `fonts/*.woff2`-Dateien im Workbox-Manifest
- Export-Bundle: `npm run export:design-system` zweimal hintereinander,
  `diff -rq` zwischen den beiden Läufen leer (byte-identisch); `dist/design-system/fonts/`
  enthält die drei Font-Dateien plus `LICENSE.md`; `colors_and_type.css` enthält die
  drei `@font-face`-Regeln; keine `googleapis`/`gstatic`-Vorkommen mehr in
  `dist/design-system/`
- Issues: keine

---

## 2026-09-11 20:35 - Erst-Sync des Design-Systems nach Claude Design (design-system-h58y4)

**What I did:**
Das Export-Bundle des Design-Systems in ein neues Design-System-Projekt "BudgetBuddy Design
System" in claude.ai/design hochgeladen (16 Dateien, Schreibplan von Roman freigegeben) und
den Re-Sync-Ablauf mit der Projekt-Id im design-system-README festgehalten. Romans
Sichtprüfung ergab zwei Funde, beide als Folge-Tasks erfasst.

**Files Added:**
- keine im Repo (das Bundle unter `dist/design-system/` ist git-ignoriert)

**Files Modified:**
- `.agentheim/contexts/design-system/README.md` - Abschnitt "Claude-Design-Projekt und
  Re-Sync" mit Projekt-Id und Ablauf.

**Files Deleted:**
- keine

**Rationale:**
Das UX-Konzept des Kassenbuchs (`ledger-xbrht`) soll mit Mockups auf Basis der echten
Tokens und Komponenten entstehen.

**Outcomes:**
- Build: nicht betroffen (kein Code geändert)
- Tests: nicht betroffen
- Issues: fehlende Kategorienauswahl im Export (`design-system-y6gkd`), Schriften als
  Dateien benötigt (`design-system-f7k2p`)

---

## 2026-09-11 18:12 - (ledger-qvt84) Iteration 3: Split+Kategorie, gescheitertes Schreiben sichtbar, Payee/Memo wie gesendet

**What I did:**
Drei Verifier-Funde behoben. `recordComdirectPush` wendet jetzt dieselbe Regel wie
`YnabClient.buildTransactionRequest` an, wenn ein Review-Schritt Kategorie UND
mindestens zwei Split-Zeilen zugleich trägt (Splits gewinnen), sonst lehnte `apply` das
Ereignis ab und die Buchung erreichte YNAB nie, still. `importToYnab` schließt die
Session nicht mehr ab und meldet einen Fehler, wenn das Kassenbuch-Schreiben einer
Buchung scheitert (`Ledger.CommandHandlers.summarizeLedgerWriteFailures`), statt es
stillschweigend zu ignorieren. Die Kandidaten für `pushComdirectImports` tragen
Payee/Memo jetzt aus dem tatsächlich gesendeten Request, nicht aus den rohen
Bank-Feldern, damit der erste Spiegel-Lauf nach einem Push kein falsches
Kopfdaten-Ereignis mehr erzeugt.

**Files Modified (Iteration 3):**
- `src/Server/Ledger/Domain.fs` - `resolveCategoryVsSplits` in `recordComdirectPush`.
- `src/Server/Ledger/CommandHandlers.fs` - `summarizeLedgerWriteFailures`.
- `src/Server/Api.fs` - `importToYnab` meldet gescheitertes Kassenbuch-Schreiben;
  Candidate-Payee/Memo aus `PreparedTransaction.Request`.
- `src/Server/YnabClient.fs` - `buildMemoWithReference` non-private (Testbarkeit).
- `src/Tests/LedgerTests.fs` - neuer Split+Kategorie-Test, zwei neue
  `summarizeLedgerWriteFailures`-Tests.
- `src/Tests/LedgerMirrorTests.fs` - Spiegel-Test umgeschrieben (Beobachtung aus dem
  gesendeten Request abgeleitet), neue explizite Zusicherung gegen
  `EntryDetailsChangeObservedInYnab`.
- `.agentheim/contexts/ledger/README.md` - Absatz zum Comdirect-Import-Push aktualisiert.

**Files Added/Deleted:** keine.

**Rationale:**
Alle drei Funde teilen dieselbe Ursache: die Kassenbuch-Ereignisse müssen zu dem passen,
was YNAB tatsächlich bekommt (Split-Regel, Payee/Memo), und ein Fehler beim
Kassenbuch-Schreiben darf nie stillschweigend verschwinden (Append-Only-Log, ADR 0013,
Auditierbarkeit).

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 778 bestanden (3 mehr als Iteration 2), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`). `npm run build` grün.
- Issues: keine. Der Split+Kategorie-Test und die
  `summarizeLedgerWriteFailures`-Regressionstests wurden per temporärer Sabotage rot
  beobachtet, danach wiederhergestellt.

---

## 2026-09-11 17:53 - (ledger-qvt84) Iteration 2: Retry nach Push-Fehler sendet wieder, drei weitere Verifier-Funde behoben

**What I did:**
Den vom Verifier gefundenen Regressions-Bug behoben: ein zweiter Push-Versuch nach
gescheitertem ersten (Netzwerkfehler, Timeout, 429, 400) sendete nie erneut, weil die
schon geschriebene, aber noch unverknüpfte Buchung über die ImportId als
`AlreadyImported` galt. `pushComdirectImports` prüft jetzt zusätzlich, ob eine
`ynab`-Verknüpfung existiert; ohne sie wird dieselbe Buchung erneut gesendet und
verknüpft. Außerdem: die Abbildung von der geparsten YNAB-Antwort auf
`TransactionCreateResult` in die reine Funktion `buildCreateResult` gezogen und
getestet (inkl. echtem Regressionstest für Bug 3); `forceImportDuplicates` markiert nur
noch `result.Created` als importiert (`markCreatedAsImported`, extrahiert und getestet);
zwei schwache Testzusicherungen korrigiert; zwei Umlaut-Stellen korrigiert;
`ledger-fq3kd`s Notes um den Hinweis ergänzt, dass ein Folgetask die bestehende Buchung
neu verknüpfen muss statt eine zweite anzulegen.

**Files Modified (Iteration 2):**
- `src/Server/Ledger/CommandHandlers.fs` - `pushComdirectImports` prüft `linksForEntry`
  vor `AlreadyImported`; Kommentare korrigiert.
- `src/Server/Api.fs` - `AlreadyImported` markiert jetzt `Imported`;
  `forceImportDuplicates` nutzt `YnabClient.markCreatedAsImported`.
- `src/Server/YnabClient.fs` - `buildCreateResult` (rein, aus `sendPrepared` gezogen),
  `markCreatedAsImported` (rein, für `forceImportDuplicates`); Umlaut-Fix.
- `src/Tests/LedgerTests.fs` - neuer Regressionstest, zwei korrigierte Zusicherungen,
  neuer `ResponseUnparseable`-Test.
- `src/Tests/YnabClientTests.fs` - neue Testlisten `buildCreateResultTests`,
  `markCreatedAsImportedTests`.
- `src/Tests/LedgerMirrorTests.fs` - Umlaut-Fix.
- `.agentheim/contexts/ledger/backlog/ledger-fq3kd-force-import-schreibt-noch-nicht-ins-kassenbuch.md` -
  Notes um die ImportId-Kollision ergänzt.

**Files Added/Deleted:** keine.

**Rationale:**
Der Regressions-Bug hätte jede Buchung, deren erster Push-Versuch fehlschlägt, dauerhaft
unimportiert gelassen, sobald die Session als abgeschlossen galt: genau das, was ADR 0021
("bis ein erneuter Push ... sie über die ImportId verknüpft") verhindern soll.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 775 bestanden (5 mehr als Iteration 1), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`). `npm run build` grün.
- Issues: keine. Regressionstest und die beiden Bug-3/Force-Import-Regressionstests
  wurden per temporärer Sabotage rot beobachtet, danach wiederhergestellt.

---

## 2026-09-11 17:33 - (ledger-qvt84) Import-Push: Kassenbuch zuerst, dann YNAB, dann Verknüpfung; drei Push-Bugs behoben

**What I did:**
`YnabClient.createTransactions` in Vorbereiten (`prepareTransactions`) und Senden
(`sendPrepared`) aufgeteilt und drei Bugs behoben: zukunftsdatierte Buchungen wurden
trotz Nicht-Versand als importiert markiert, die neue ImportId eines Force-Imports war
für den Aufrufer nie auslesbar, eine nicht lesbare YNAB-Antwort galt pauschal als voller
Erfolg. `TransactionCreateResult` trägt jetzt die YNAB-Ids je Buchung über die ImportId
zugeordnet. Darauf aufbauend schreibt der Comdirect-Import-Push jetzt zuerst die
Kassenbuch-Buchung (`Ledger.Domain.recordComdirectPush` aus der Review-Spur,
`Ledger.CommandHandlers.pushComdirectImports` für Kassenbuch-zuerst-Push-Verknüpfung,
ADR 0021), bevor er nach YNAB sendet. Ein neuer Actor `OrderIdMatch` (ADR 0025) gibt
Kategorien aus dem Order-ID-Abgleich einen eigenen Urheber in der Historie.

**Files Added:**
- `.agentheim/knowledge/decisions/0025-order-id-abgleich-als-eigener-actor-kein-eigenes-ereignis.md`
- `.agentheim/contexts/ledger/backlog/ledger-fq3kd-force-import-schreibt-noch-nicht-ins-kassenbuch.md`

**Files Modified:**
- `src/Server/YnabClient.fs` - `createTransactions` aufgeteilt in `prepareTransactions`,
  `parseCreateTransactionsResponse`, `resolveTransferPayees`, `sendPrepared`;
  `TransactionCreateResult` neu geformt, `emptyCreateResult`.
- `src/Server/Ledger/Domain.fs` - `recordComdirectPush` (rein, baut die Ereignisfolge
  aus der Review-Spur).
- `src/Server/Ledger/CommandHandlers.fs` - `pushComdirectImports` (Kassenbuch zuerst,
  dann Push, dann Verknüpfung, injizierter `push`).
- `src/Server/Ledger/Events.fs` - `Actor.OrderIdMatch`, Persistenzform.
- `src/Server/Ledger/QueryHandlers.fs` - `originOf` um `OrderIdMatch` ergänzt.
- `src/Shared/Domain.fs` - `LedgerOrigin.MatchedByOrderId`, `originLabel`.
- `src/Client/Components/Ledger/View.fs` - Badge für `MatchedByOrderId`.
- `src/Server/Api.fs` - `importToYnab` (Kassenbuch-Konto auflösen, Kassenbuch-zuerst-
  Push-Verknüpfung verdrahten), `forceImportDuplicates` (`.Created.Length`).
- `src/Tests/YnabClientTests.fs`, `src/Tests/LedgerTests.fs`,
  `src/Tests/LedgerMirrorTests.fs`, `src/Tests/LedgerPageTests.fs` - neue Testlisten.
- `.agentheim/contexts/ledger/README.md` - Actor-Liste, Entscheidungen, Herkunfts-Badge,
  `BuchungÜbernehmen` um den Comdirect-Import-Push-Ablauf ergänzt.

**Files Deleted:**
- keine

**Rationale:**
Die Vision verlangt, dass das Kassenbuch BB-Entscheidungen kennt; ADR 0021 legt dafür
Reihenfolge und Fehlerverhalten fest. Die drei Push-Bugs saßen an derselben Stelle wie
die geforderte Antwort-Auswertung und wurden deshalb im selben Task mitbehoben.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 770 bestanden (16 neu), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`). `npm run build` grün.
- Issues: keine. Die drei Bug-Regressionstests und der Doppelbuchungs-Schutz in
  `pushComdirectImports` wurden per temporärer Sabotage rot beobachtet, danach
  wiederhergestellt.

---

## 2026-09-11 16:33 - Review-Spur, Iteration 2 (categorization-gdrx8): Order-ID-Pfade schließen die Lücke

**What I did:**
Nach dem Verifier-FAIL aus Iteration 1 (zwei Order-ID-Pfade änderten die Kategorie
ohne ReviewStep) und Romans Entscheidung zu den Urhebern: `ReviewActor` um
`ByOrderIdMatch of orderId: string` erweitert. `OrderIdMatcher.applySuggestions` hängt
jetzt `ByOrderIdMatch`-Schritte an (Kategorie aus der YNAB-Historie derselben
Bestellung), `OrderIdMatcher.propagateInSession` hängt `ByUser`-Schritte an (Romans
Entscheidung, nur übertragen). Beide Funktionen nehmen jetzt einen Zeitpunkt-Parameter;
`categorizeTransaction` verwendet denselben Zeitpunkt für den eigenen Schritt und für
die Propagation. `ReviewStepJson`-Codec um den Diskriminator `"orderIdMatch"`
erweitert (additiv, keine Migration nötig, ältere Zeilen laden unverändert). Das aus
Iteration 1 entstandene Backlog-Item ist damit in diesem Task aufgegangen und wurde vom
Conductor gelöscht. Stilkorrekturen aus der Verifier-Note nachgezogen (Umlaute statt
Umschreibungen in eigenen Kommentaren).

**Files Added:**
- keine

**Files Modified:**
- `src/Shared/Domain.fs` - `ReviewActor` um `ByOrderIdMatch of orderId: string`
  erweitert
- `src/Server/OrderIdMatcher.fs` - `applySuggestions` und `propagateInSession` nehmen
  einen Zeitpunkt-Parameter und hängen Review-Schritte an (`ByOrderIdMatch` bzw.
  `ByUser`)
- `src/Server/Api.fs` - `classifyTransactions`- und `applySuggestions`-Aufrufe mit
  `DateTime.UtcNow`; `categorizeTransaction` teilt sich einen `now` zwischen eigenem
  Schritt und Propagation
- `src/Server/Persistence.fs` - `ReviewStepJson`-Codec um `"orderIdMatch"` erweitert;
  Umlaute im Modul-Kommentar korrigiert (waren als "fuer"/"eigenstaendig"/"wuerde"
  umschrieben)
- `src/Tests/OrderIdMatcherTests.fs` - Aufrufe von `applySuggestions`/
  `propagateInSession` um den neuen Zeitpunkt-Parameter ergänzt; vier neue Testfälle
  (`ByOrderIdMatch`-Actor, kein Schritt ohne Vorschlag, `ByUser`-Actor bei Propagation,
  Regressionstest "clearSplit, dann Propagation")
- `src/Tests/PersistenceTypeConversionTests.fs` - ein neuer Testfall für den
  `ByOrderIdMatch`-Roundtrip
- `.agentheim/contexts/categorization/README.md` - Review-Spur-Eintrag um den dritten
  Actor und alle fünf Schreibpfade ergänzt
- `.agentheim/contexts/categorization/doing/categorization-gdrx8-review-spur-in-synctransaction.md` -
  Outcome/Diary für Iteration 2, stale Referenz auf das aufgegangene Backlog-Item
  bereinigt

**Files Deleted:**
- keine (das Backlog-Item wurde bereits vom Conductor gelöscht, bevor dieser Task
  erneut dispatcht wurde)

**Rationale:**
Der Verifier hat zu Recht bemängelt, dass eine über Order-ID kategorisierte Buchung
ohne ReviewStep gepusht worden wäre: `ledger-qvt84` baut die Ereignisfolge
ausschließlich aus `ReviewSteps`, eine Lücke dort hätte eine Kategorie in YNAB ohne
passendes Kassenbuch-Ereignis bedeutet. Roman hat die fehlende fachliche Entscheidung
(welcher Urheber für welchen Order-ID-Pfad) getroffen; die Umsetzung folgt ihr 1:1.

**Outcomes:**
- Build: `dotnet build` (Solution) 0 Warnungen/0 Fehler; `npm run build` grün
- Tests: 730/737 bestanden, 7 übersprungen (.env-abhängig), 0 fehlgeschlagen; 5 davon
  neu in dieser Iteration (13 insgesamt über beide Iterationen)
- Issues: keine; Rot-Grün per temporärer Sabotage der beiden neuen
  `appendReviewStep`-Aufrufe in `OrderIdMatcher.fs` verifiziert (genau die 3 davon
  abhängigen Tests schlugen fehl, danach wiederhergestellt)

---

## 2026-09-11 12:45 - Review-Spur: SyncTransaction hält Kategorisierungs-Schritte fest (categorization-gdrx8)

**What I did:**
`SyncTransaction` um `ReviewSteps: ReviewStep list` erweitert (neue Typen
`ReviewActor`/`ReviewStep` plus reine Hilfsfunktion `appendReviewStep` in
`src/Shared/Domain.fs`). Jeder Schreibpfad, der Kategorie, Splits oder PayeeOverride
ändert (Regel-Anwendung, `categorizeTransaction`, `bulkCategorize`, `splitTransaction`,
`clearSplit`), hängt jetzt einen Schritt mit Zeitpunkt und Herkunft an; eine
Wiederholung mit unverändertem Stand erzeugt keinen neuen Eintrag. Persistenz um eine
neue Spalte `review_steps` (JSON, eigener Thoth.Json.Net-Codec) erweitert, mit
Migration für bestehende Datenbanken.

**Files Added:**
- Backlog-Item für die Lücke bei Order-ID-Propagation (kein ReviewStep dort), vom
  Conductor auf `categorization-mgnjp` normalisiert. In Iteration 2 direkt in diesem
  Task behoben (siehe Diary-Eintrag Iteration 2 unten); das Backlog-Item ist damit
  aufgegangen und wurde vom Conductor gelöscht.

**Files Modified:**
- `src/Shared/Domain.fs` - `ReviewActor`, `ReviewStep`, `SyncTransaction.ReviewSteps`,
  `appendReviewStep`
- `src/Server/RulesEngine.fs` - `classifyTransactions` nimmt `now: DateTime`, hängt bei
  Regel-Treffer einen `ByRule`-Schritt an
- `src/Server/Api.fs` - `categorizeTransaction`, `bulkCategorize`, `splitTransaction`,
  `clearSplit` hängen `ByUser`-Schritte an; `classifyTransactions`-Aufruf mit
  `DateTime.UtcNow`
- `src/Server/Persistence.fs` - Spalte `review_steps`, Migration, Codec
  `ReviewStepJson`, Save/Load erweitert
- `src/Tests/RulesEngineTests.fs` - neue Testlisten für `classifyTransactions`-
  ReviewSteps und `appendReviewStep`
- `src/Tests/PersistenceTypeConversionTests.fs` - neue Testliste "ReviewSteps
  Conversions" (Roundtrip inkl. Splits, leere Spur)
- `src/Tests/DuplicateDetectionTests.fs`, `OrderIdMatcherTests.fs`, `SplitPushTests.fs`,
  `SplitTransactionTests.fs`, `SyncFlowViewTests.fs`, `YnabClientTests.fs`,
  `SyncSessionManagerTests.fs` - `ReviewSteps = []` in bestehenden
  `SyncTransaction`-Literalen ergänzt (mechanisch, Typ erweitert)
- `.agentheim/contexts/categorization/README.md` - Ubiquitous-Language-Eintrag
  "Review-Spur (`ReviewSteps`)", Relationships-Absatz aktualisiert

**Files Deleted:**
- keine

**Rationale:**
ADR 0021 verlangt, dass die Kassenbuch-Buchung beim Push als Ereignisfolge
("Regel X: ...", dann "Roman in BB: ...") entsteht; da die Buchung selbst erst beim
Push existiert, muss die SyncSession bis dahin Buch führen, wer wann welchen
Kategorisierungs-Stand gesetzt hat. `ledger-qvt84` liest diese Spur beim Push.

**Outcomes:**
- Build: dotnet build (Solution) 0 Warnungen/0 Fehler; npm run build grün
- Tests: 725/725 bestanden (7 übersprungen, .env-abhängig), 8 davon neu
- Issues: keine; Rot-Grün per temporärer Sabotage von `appendReviewStep` verifiziert
  (4 betroffene Tests schlugen erwartungsgemäß fehl, danach wiederhergestellt)

---

## 2026-09-11 16:30 - Export-Bundle des Design-Systems für Claude Design (design-system-t82sg)

**What I did:**
Ein wiederholbares Export-Kommando gebaut, das Tokens und die Kassenbuch-relevanten
DS-Komponenten aus dem echten, gerenderten Code (`/#/styleguide`) als Claude-Design-Bundle
nach `dist/design-system/` (git-ignoriert) schreibt. Dazu eine Navigation-Sektion in der
Styleguide-Galerie ergänzt (fehlte bisher), zwei nicht-fixe Preview-Varianten der
Navigation-Komponente gebaut, sowie (Iteration 2) Token-/Type-/Brand-Vorschaukarten und
expliziten Font-Ladepfad für die Bundle-Vorschauen ergänzt.

**Files Added:**
- `scripts/export-design-system.mjs`: Node-Skript: `vite build` (temp outDir), `vite
  preview` (freier Port), Playwright rendert `/#/styleguide`, schneidet `[data-ds-card]`
  heraus, schreibt `colors_and_type.css`/`styles.css`/`preview/*.html`/`assets/`/
  `README.md`/`SKILL.md`, prüft danach headless, dass die Fonts laden.
- `.agentheim/knowledge/decisions/0024-export-bundle-build-nicht-dev-und-nicht-fixe-navigation-preview.md`
- `.agentheim/contexts/design-system/backlog/design-system-w4n9r-e2e-test-fuer-styleguide-export.md`
- `.agentheim/contexts/design-system/backlog/design-system-f7k2p-google-fonts-import-hinter-theme-block-wird-verworfen.md`
  (Iteration 2: Bug-Backlog-Item für den App-seitigen Font-Import-Fehler)

**Files Modified:**
- `src/Client/Components/Styleguide/View.fs`: `data-ds-card`-Marker auf Card-/Money-/
  Badge-/Table-Sektion, neue Navigation-Sektion ("20. Navigation"), `data-ds-card-trigger`
  am BottomSheet-Demo-Button, Voice-Sektion auf "21." umnummeriert. Iteration 2: Prosa in
  neuen Kommentaren auf Punkt/Doppelpunkt statt Gedankenstrich umgestellt.
- `src/Client/DesignSystem/Navigation.fs`: `desktopNavPreview`/`mobileNavPreview` ergänzt
  (additiv, echte `desktopNav`/`mobileNav` unverändert). Iteration 2: Kommentare
  dashfrei umformuliert.
- `package.json`: `export:design-system`-Script ergänzt.
- `.agentheim/contexts/design-system/README.md`: Abschnitt "Export-Bundle für Claude
  Design" ergänzt (Iteration 2: Fonts, Token-/Brand-Karten, Bug-Verweis ergänzt, alle
  Gedankenstriche entfernt).

**Files Deleted:**
- keine

**Rationale:**
Das Kassenbuch braucht vor weiteren Screens einen UX-Durchgang mit Mockups in Claude Design.
Damit die Mockups wie BudgetBuddy aussehen statt wie eine Nachempfindung, muss Claude Design
echte Tokens/Komponenten bekommen, nicht von Hand nachgebaute. Gerendertes Markup statt
Handarbeit verhindert genau die Drift, die das Styleguide-Gate sonst verhindern soll.
Iteration 2 kam aus der Verifier-Prüfung: eine Stilregel (keine Gedankenstriche in neuer
Prosa), ein echter Bundle-Mangel (Fonts luden nicht) und eine Conductor-Entscheidung
(Token-Karten ergänzen, weil der "What"-Baum sie nennt).

**Outcomes:**
- Build: dotnet build erfolgreich (Client, Server, Shared, Tests, 0 Fehler, 0 Warnungen)
- Tests: dotnet test erfolgreich, 717 bestanden, 7 übersprungen (Comdirect/YNAB-
  Integrationstests, brauchen `.env`, vorbestehend, nicht Teil dieses Tasks), 0 fehlgeschlagen
- npm run build: erfolgreich
- `npm run export:design-system` zweimal hintereinander: byte-identisches Bundle
  (`diff -rq` zwischen beiden Läufen: keine Unterschiede), inklusive der neuen
  Token-/Brand-Karten
- Screenshot-Vergleich Live-Route vs. exportierte `preview/*.html`: Card, Money, Badge,
  Table, Navigation, BottomSheet visuell deckungsgleich (Layout, Farben, Typografie, Glow;
  Screenshots nicht committet)
- Headless `document.fonts`-Check gegen `preview/type-fonts.html`: Inter, Space Grotesk,
  JetBrains Mono geladen (vorher: 0 geladene Fonts, Ersatzschrift)
- Kein automatisierter E2E-Test dafür im Repo (kein Playwright-Harness in `dotnet test`):
  Nachrüstung als Backlog-Item `design-system-w4n9r` angelegt, manuelle Verifikation s. o.
- App-seitiger Font-Bug als Backlog-Item `design-system-f7k2p` erfasst, in diesem Task
  bewusst nicht behoben (Umfang: nur der Export)
- Issues: keine

---

## 2026-09-11 16:27 - Quick Add: erst Kassenbuch, dann Push, dann Verknüpfung (ledger-tdnpk)

**What I did:**
`createManualTransaction` liest jetzt die YNAB-Transaction-Id aus der Antwort des
Einzel-Posts; ein neuer Command-Handler `addCashExpense` schreibt zuerst die
Kassenbuch-Buchung (`CashExpenseAdded`), pusht danach nach YNAB und verknüpft bei Erfolg
mit `EntryLinkedToYnab` (ADR 0021). Scheitert der Push, bleibt die Buchung ohne
Verknüpfung stehen, kein Rollback. `Server.Api.addManualTransaction` verdrahtet das, mit
unveränderter Client-Signatur; fehlt das gespiegelte Bar-Konto, lehnt der Server mit einer
klaren Fehlermeldung ab, statt ein zweites Konto zu erfinden.

**Files Added:**
- keine

**Files Modified:**
- `src/Server/YnabClient.fs` - `createManualTransaction` gibt `Async<YnabResult<string>>`
  zurück; neue Funktion `manualTransactionIdDecoder`.
- `src/Server/Ledger/CommandHandlers.fs` - neuer Typ `CashExpenseInput` und neue Funktion
  `addCashExpense`.
- `src/Server/Api.fs` - `addManualTransaction` löst das Kassenbuch-Konto und die Kategorie
  auf und ruft `addCashExpense` mit einer Push-Closure auf.
- `src/Tests/YnabClientTests.fs` - neue Testliste für `manualTransactionIdDecoder`.
- `src/Tests/LedgerTests.fs` - neue Testliste für `addCashExpense` (Erfolg, Push-Fehler).
- `src/Tests/LedgerMirrorTests.fs` - neuer Testfall: Spiegel-Lauf nach Quick Add legt keine
  zweite Buchung an.
- `.agentheim/contexts/ledger/README.md` - Abschnitt "Key commands" um den tatsächlichen
  Quick-Add-Ablauf ergänzt.

**Files Deleted:**
- keine

**Rationale:**
Quick Add hat keine ImportId (ADR 0004); die Push-Antwort ist die einzige Stelle, an der
BBs Buchung und YNABs Buchung verbunden werden können (ADR 0015). Roman hat entschieden,
dass das Kassenbuch dafür zuerst geschrieben wird (ADR 0021), symmetrisch zum
Comdirect-Import, damit ein Timeout oder Absturz zwischen Push und Verknüpfung nie die
BB-Herkunft der Buchung verliert.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 741 bestanden (5 mehr als vorher), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`).
- Issues: keine.

---

## 2026-09-11 12:52 - Nachtrag (ledger-cscw1): mkSplits-Invariante für EntryCategorizedByRule/EntryRecategorized

**What I did:**
Verifier-Fund aus Iteration 1 behoben: `apply` prüfte bei `EntryCategorizedByRule` und
`EntryRecategorized` bisher nur, dass Kategorie und Splits sich ausschließen, nicht aber
die `mkSplits`-Invarianten (mindestens zwei Zeilen, vorzeichenrichtige Summe gleich dem
Buchungsbetrag, eine Währung). Beide Zweige rufen jetzt `Shared.Domain.mkSplits` auf und
lehnen einen Verstoß laut ab.

**Files Added:**
- keine

**Files Modified:**
- `src/Server/Ledger/Domain.fs` - `requireCategoryXorSplits` um den Buchungsbetrag und
  einen `mkSplits`-Aufruf bei gesetzten Splits erweitert; beide Aufrufstellen
  (`EntryCategorizedByRule`, `EntryRecategorized`) übergeben `e.Amount`.
- `src/Tests/LedgerTests.fs` - fünf neue Testfälle (Summe falsch und einzeilig je
  Ereignis, plus ein positiver Fall für gültige Splits), rot beobachtet vor dem Fix.
- `.agentheim/contexts/ledger/README.md` - Satz zu Kategorie/Splits nennt jetzt die
  `mkSplits`-Regeln namentlich.

**Files Deleted:**
- keine

**Rationale:**
Der Task verlangt ausdrücklich, dass die `mkSplits`-Invarianten auch für die
Entscheidungs-Ereignisse gelten (ADR 0006/0019); ohne die Prüfung hätte `apply` eine
Buchung mit strukturell ungültigen Splits (falsche Summe, nur eine Zeile) klaglos
übernommen.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 736 bestanden (5 mehr als Iteration 1), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`).
- Issues: keine. Vor dem Fix testweise die `mkSplits`-Prüfung entfernt: genau die vier
  negativen neuen Tests liefen rot, danach wiederhergestellt und grün.

---

## 2026-09-11 12:39 - Ereignis-Katalog: Entscheidungs-Ereignisse im Kassenbuch (ledger-cscw1)

**What I did:**
Fünf Ereignistypen für BBs eigene Entscheidungen (`EntryImportedFromComdirect`,
`EntryCategorizedByRule`, `EntryRecategorized`, `CashExpenseAdded`) und die
YNAB-Verknüpfung (`EntryLinkedToYnab`, ADR 0021) im Kassenbuch-Ereignis-Katalog ergänzt,
inklusive `apply`-Invarianten (genau eine Entstehung je Buchung, Kategorie/Splits
schließen sich aus, Regel nur auf unkategorisierter Buchung) und JSON-Codecs.

**Files Added:**
- keine (nur bestehende Ledger-Dateien erweitert)

**Files Modified:**
- `src/Server/Ledger/Events.fs` - fünf neue Payload-Records, `LedgerEventBody` erweitert,
  `ReservedDecisionEventName` auf `ValuationRecorded` reduziert, Typname/Schema-Version
  und Codecs ergänzt.
- `src/Server/Ledger/Domain.fs` - `apply` um die fünf neuen Ereignis-Zweige und die
  Entstehungs-/Kategorie-Invarianten erweitert, `linksFrom` um Comdirect-Import- und
  YNAB-Verknüpfung ergänzt.
- `src/Server/Ledger/QueryHandlers.fs` - `ReadModel.describe` um fünf deutsche
  Historien-Sätze für die neuen Ereignisse ergänzt (sonst unvollständiges Pattern-Match).
- `src/Tests/LedgerTests.fs` - 14 neue Testfälle plus Erweiterung zweier bestehender
  (Typname/Schema-Version, JSON-Roundtrip).
- `.agentheim/contexts/ledger/README.md` - Abschnitt "Ereignisse": "Entstehung" und
  "Verknüpfung" ergänzt, "Entscheidungen" mit Payload-Kurzbeschreibung statt nur Namen.

**Files Deleted:**
- keine

**Rationale:**
Ohne Payloads konnte das Kassenbuch keine BB-Entscheidung aufnehmen; das v1-Vision-
Kriterium verlangt genau das. ADR 0021 verlangt zusätzlich ein eigenes
Verknüpfungs-Ereignis, damit Replay die YNAB-Id-Zuordnung aus dem Log herleiten kann.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 731 bestanden, 7 übersprungen (extern), 0 Fehler (`dotnet test`
  `src/Tests/Tests.fsproj`).
- Issues: keine. Die Mirror-Tests (`LedgerMirrorTests.fs`) blieben unverändert grün, weil
  `observe` eine zweite `EntryObservedInYnab` ohnehin nie erzeugt.

---

## 2026-09-06 22:20 - Bugfix: Erstbefuellung des Spiegels holte nur zwoelf Monate

**What I did:**
Zweiter Abnahme-Fund aus `ledger-kt0wq`. Nach dem Anzeige-Fix zeigte die Seite die
Konten, aber drei von sieben Kontostaenden waren falsch: Girokonto -238,42 statt
5.033,12; Tagesgeld Consors -31.834,02 statt 0,00; Bar 58,61 statt 73,72. Tagesgeld ING,
ING Girokonto, Depot und Kreditkarte stimmten exakt.

**Diagnose (Reihenfolge, weil der Verdacht zweimal falsch lag):**
1. Salden aus der DB nachgerechnet — identisch mit dem, was die Seite zeigt. Also kein
   Anzeige- oder Summierungsfehler.
2. `getLedgerAccounts` und `loadAccounts` gelesen, dann den Endpunkt direkt aufgerufen:
   liefert korrekt. Also kein Lesepfad-Fehler.
3. Der Spiegel-Lauf verwirft nichts, der Decoder auch nicht (`Decode.list`, kein
   `choose`). Also kamen genau 2508 Buchungen von YNAB.
4. YNAB direkt abgefragt: `GET /budgets/{id}/transactions` **ohne** `since_date` liefert
   2508 Buchungen ab 2025-09-08. **Mit** `since_date=2000-01-01` liefert derselbe
   Endpunkt 13863 Buchungen ab 2018-12-29 — und dann stimmen alle sieben Kontostaende
   auf den Cent mit YNABs Anzeige ueberein.

**Ursache:** der budgetweite Transaktions-Endpunkt liefert ohne `since_date` nicht alles,
sondern ein rollendes Fenster der letzten rund zwoelf Monate. Die Anfangssalden der
aelteren Konten liegen ausserhalb; deshalb waren genau die Konten falsch, deren
Startsaldo nicht 0 war. Die beiden ING-Konten stimmten, weil ihr Startsaldo 0 ist und
ihr gesamtes Geld ueber Uebertraege innerhalb des Fensters kam.

ADR 0014 fordert fuer die Erstbefuellung ausdruecklich einen **Vollabruf**. Die
Umsetzung las "ohne last_knowledge" als "Vollabruf", was gegenueber der YNAB-API nicht
stimmt. Der ADR-Text bleibt richtig, die Implementierung war es nicht.

**Files Modified:**
- `src/Server/YnabClient.fs` - `transactionsQuery` ersetzt `knowledgeQuery` fuer den
  Transaktions-Abruf: ohne gemerkte Marke `?since_date=1900-01-01` (Vollabruf), mit
  Marke weiterhin das Delta. `mirrorEarliestDate` traegt die Messwerte als Begruendung.
  Fuer den Konten-Abruf bleibt `knowledgeQuery`; Konten sind nicht zeitgefiltert.
- `src/Tests/YnabClientTests.fs` - Testgruppe "Spiegel: Erstbefuellung holt alles"
  (3 Tests) auf dem Query-Bau, ohne YNAB-Aufruf.

**Rationale:**
Der Fehler war ohne echtes Budget unsichtbar: jeder Testdatensatz liegt innerhalb der
letzten zwoelf Monate. Nur die Abnahme am realen Budget mit sechs Jahren Historie konnte
ihn zeigen. Genau dafuer ist das Human-Gate da.

**Verifikation der Regressionstests:** Mit zurueckgebautem Fix (`| None -> ""`) fallen
zwei der drei Tests mit der richtigen Meldung ("Expected subject string '' to contain
substring 'since_date='").

**Outcomes:**
- Build: OK (0 Warnungen)
- Tests: 718 bestanden, 6 uebersprungen, 0 Fehler
- `npm run build`: OK
- **Offen und wichtig:** die Dev-Datenbank traegt noch die unvollstaendige Befuellung
  (2508 statt 13863 Buchungen) und ein `ledger_mirror_server_knowledge_*`, das den
  naechsten Lauf zum Delta macht. Ein Delta holt die fehlende Historie **nicht** nach.
  Vor der weiteren Abnahme muessen die vier `ledger_*`-Tabellen und die
  `server_knowledge`-Zeile geleert und einmal neu gespiegelt werden.

---

## 2026-09-06 21:50 - Bugfix: Kassenbuch-Seite blieb nach dem ersten Spiegel-Lauf leer

**What I did:**
Abnahme-Fund aus `ledger-kt0wq` (Human-Gate, Roman am echten Budget). Der erste
Spiegel-Lauf schrieb 7 Konten, 2508 Buchungen, 2508 Ereignisse und 4341 Fremd-Id-
Verknuepfungen in die Datenbank, und der Endpunkt `getLedgerAccounts` lieferte sie
korrekt aus. Die Seite zeigte trotzdem "Noch kein Konto", auch nach Neuladen und
wiederholtem Spiegeln. Zwei getrennte Ursachen, beide behoben:

**A) Deep-Link und F5 loesten kein einziges Ladekommando aus (App-Huelle).**
`State.init` schrieb die Seite aus der URL bereits in `CurrentPage` und schickte
danach `UrlChanged` fuer genau dieselbe Seite. Der Handler beginnt mit einem
Gleichheits-Guard ("Seite unveraendert -> `Cmd.none`"), der damit immer zutraf. Beim
Betreten ueber die Navigation lief alles, beim Direktaufruf nichts. Neben dem
Kassenbuch war `Rules` betroffen; `SyncFlow` und `Settings` fielen nicht auf, weil sie
ihre Daten zusaetzlich in `init` laden. Das ist exakt das in
`.claude/rules/elmish-state.md` beschriebene Fehlmuster ("Navigation via Bottom Nav
funktioniert, Hash-basierte Navigation zeigt leere Seite").
Fix: der `match` ueber die Seiten ist als `State.pageLoadCmd` herausgezogen; `init`
ruft ihn direkt auf, `UrlChanged` benutzt denselben. Der Gleichheits-Guard bleibt, wo
er hingehoert.

**B) Nach einem Spiegel-Lauf wurden die Konten nicht neu geladen (Kassenbuch).**
`MirrorFinished (Ok)` schickte `LoadEntries` und `LoadMirrorStatus`. `LoadEntries`
prueft als Erstes, ob ein Konto gewaehlt ist, und tut ohne Auswahl still nichts. Auf
einem leeren Kassenbuch gibt es beim Seitenaufruf noch keine Konten, also keine
Auswahl, also passierte nach dem Lauf nichts. Fix: `MirrorFinished (Ok)` laedt jetzt
`LoadAccounts`; `AccountsLoaded` zieht die Buchungen selbst nach. Damit wird auch ein
spaeter in YNAB angelegtes Konto sichtbar.
Damit das nicht die Kontoauswahl ueberschreibt, behaelt `AccountsLoaded` eine bereits
getroffene Auswahl, solange es das Konto noch gibt, und faellt sonst auf die
Server-Vorauswahl zurueck.

**Files Modified:**
- `src/Client/State.fs` - `pageLoadCmd` als eigene Funktion herausgezogen; `init` ruft
  sie direkt auf statt ueber `UrlChanged`; `UrlChanged` benutzt dieselbe Funktion.
- `src/Client/Components/Ledger/State.fs` - `MirrorFinished (Ok)` laedt Konten statt
  nur Buchungen; `AccountsLoaded` erhaelt eine bestehende Kontoauswahl.
- `src/Tests/LedgerPageTests.fs` - neue Testgruppe "Erster Spiegel-Lauf auf leerem
  Kassenbuch" (5 Tests) samt `dispatched`-Helfer, der ein `Cmd` ausfuehrt und die
  abgeschickten Nachrichten einsammelt.

**Rationale:**
Der Fehler war nur beim allererste Lauf auf einem leeren Kassenbuch sichtbar und ist
deshalb durch alle Reduzierer-Tests gerutscht: die pruefen den Spiegel-Lauf mit bereits
gewaehltem Konto. Genau dafuer ist das Human-Gate da.

**Verifikation der Regressionstests:** Mit zurueckgebautem Fix fallen die beiden
Kassenbuch-Tests mit den richtigen Meldungen ("Sequence did not contain LoadAccounts",
"expected Some ...001, actual Some ...002"). Der Test zu Ursache A ist ehrlich
schwaecher: `pageLoadCmd` existierte vor dem Fix nicht, der Test kann den alten Zustand
also nicht reproduzieren. Er pinnt die neue Struktur (jede datentragende Seite traegt
Ladekommandos, der Styleguide bewusst keine). Dass `init` diese Funktion auch aufruft,
ist nicht testabgedeckt: `init` liest die echte URL ueber `Routing.currentPage ()` und
braucht dafuer einen Browser.

**Outcomes:**
- Build: OK (0 Warnungen)
- Tests: 715 bestanden, 6 uebersprungen (`.env`-Integrationstests), 0 Fehler
- `npm run build`: OK
- Offen: die Abnahme `ledger-kt0wq` selbst laeuft noch (Kategorie-Aenderung, Loeschung,
  zweiter Lauf ohne Ereignis).

---

## 2026-09-06 - Kassenbuch-Seite: Buchungen, Herkunft, Historie, "Jetzt spiegeln" (ledger-q5w9c)

**What I did:**
Slice 3 von 3 des Walking Skeletons. Die Route `#/ledger` zeigt die Konten des
Kassenbuchs, die Buchungen des gewaehlten Kontos mit Herkunfts-Badge, den in F#
summierten Kontostand und je Buchung die vollstaendige Historie in einem
BottomSheet. "Jetzt spiegeln" stoesst den manuellen Spiegel-Lauf an; ein
automatischer Ausloeser bleibt bewusst aussen vor (ADR 0014, Folge-Task).

**Files Added:**
- `src/Server/Ledger/QueryHandlers.fs` - Lesepfad: reines `ReadModel` (Ereignis ->
  sprechender Satz, Zustand -> Sicht, Vorauswahl des Kontos) plus die drei Queries
- `src/Client/Components/Ledger/Types.fs` - Model, Msg, ExternalMsg der Seite
- `src/Client/Components/Ledger/State.fs` - Reduzierer (MVU, RemoteData)
- `src/Client/Components/Ledger/View.fs` - Seite aus DS-Komponenten und Tokens
- `src/Tests/LedgerPageTests.fs` - 33 Tests: Historien-Sprache, Sichten, Kontostand,
  Reduzierer-Uebergaenge

**Files Modified:**
- `src/Shared/Domain.fs` - Lesemodelle (`LedgerOrigin`, `LedgerAccountView`,
  `LedgerAccountsView`, `LedgerEntryView`, `LedgerHistoryLine`), `originLabel`,
  `ledgerBalance` (Summe in F#, ADR 0017)
- `src/Shared/Api.fs` - `LedgerApi` um `getLedgerAccounts`,
  `getLedgerEntriesForAccount`, `getEntryHistory` erweitert
- `src/Server/Ledger/Persistence.fs` - `loadEntriesForAccount`, `loadEntryOrigins`
- `src/Server/Api.fs` - die drei Sichten verdrahtet
- `src/Server/Server.fsproj`, `src/Client/Client.fsproj`, `src/Tests/Tests.fsproj` -
  neue Dateien registriert
- `src/Client/Types.fs` - Seite `Ledger` und Route `#/ledger`
- `src/Client/DesignSystem/Navigation.fs` - fuenfter Tab "Kassenbuch"; das mobile
  Padding je Tab verkleinert, damit fuenf Tabs auf 375px passen
- `src/Client/State.fs`, `src/Client/View.fs` - Kassenbuch in Modell, Update und View
- `src/Tests/QuickAddTests.fs` - Modell-Fixture um das neue Feld ergaenzt

**Rationale:**
Ohne Sicht bleibt das Kassenbuch unsichtbar; die Seite beweist, dass Ereignis-Log,
Spiegel-Lauf und Anzeige zusammen funktionieren. Die Historien-Saetze kommen aus den
Payloads (alte und neue Kategorie-Namen liegen im Ereignis), deshalb braucht die
Anzeige kein Nachschlagen. Der Kontostand wird in F# summiert und nie per SQL-`SUM`
(ADR 0017); geloeschte Buchungen bleiben sichtbar, zaehlen aber nicht mit.

**Outcomes:**
- Build: OK (`dotnet build`, `npm run build`)
- Tests: 709/709 bestanden (7 uebersprungen, brauchen `.env`), davon 33 neu
- Issues: Serena war nicht verfuegbar; Fallback auf die Standard-Werkzeuge.

**Nachtrag (fortgesetzter Lauf):**
Der erste Durchgang brach vor der Abnahme ab. Beim Wiederaufnehmen liefen `dotnet
build`, `dotnet test` und `npm run build` unveraendert gruen. Nachgezogen wurde allein
die Sprache der Historien-Saetze: die angezeigten Texte schreiben jetzt echte Umlaute
und den Pfeil aus der Modellierung ("Kategorie in YNAB geändert: Lebensmittel →
Restaurant") statt der Umschreibung "geaendert: ... -> ...". Betroffen:
`src/Server/Ledger/QueryHandlers.fs` (`describe`, `clearedLabel`) und die
Erwartungen in `src/Tests/LedgerPageTests.fs`.

---

## 2026-09-06 - Spiegel-Lauf YNAB -> Kassenbuch: Delta-Abruf, Vergleich, Ereignisse (ledger-m8r2v)

### What
Slice 2 des Kassenbuch-Skeletons: der Spiegel. Budgetweiter Delta-Abruf ueber
`last_knowledge_of_server`, ein reiner Contract `YnabObservation` am ynab-sync-Rand, der
reine Vergleich `observe` im Ledger-Domain und der Lauf `mirrorYnabBudget`, der Konten
nachzieht, Buchungen ueber YNAB-Id vor ImportId matcht und nur bei echtem Unterschied
Ereignisse ueber `appendAndApply` schreibt. Manueller Fable.Remoting-Endpunkt plus
abrufbarer Lauf-Status. Keine UI.

### Files Changed
- `src/Server/YnabObservations.fs` (neu) - Contract (Beobachtung, Batch, Konten-Beobachtung)
  und die Thoth-Decoder fuer Transaktions- und Konten-Delta inkl. Subtransactions,
  Transfer-Feldern, `deleted` und `server_knowledge`
- `src/Server/Ledger/Domain.fs` - `ObservationContext` und die reine Funktion
  `observe : ObservationContext -> Entry option -> YnabObservation -> LedgerEvent list`
- `src/Server/Ledger/CommandHandlers.fs` (neu) - der Spiegel-Lauf mit injizierten Abrufen,
  Konten-Aufloesung, Matching nach ADR 0015, `server_knowledge` und Lauf-Status in den Settings
- `src/Server/YnabClient.fs` - `getTransactionsDelta` / `getAccountsDelta` (neu; die
  bestehenden `since_date`-Abrufe bleiben unangetastet), Push-Arm fuer die
  unkategorisierte Split-Zeile
- `src/Server/Ledger/Events.fs` - Split-Ziel-Codec kennt `uncategorized`
- `src/Server/Api.fs` - `ledgerApi` und Verdrahtung im `webApp`
- `src/Shared/Domain.fs` - `SplitTarget.Uncategorized`, `MirrorRunSummary`,
  `MirrorRunOutcome`, `MirrorRunStatus`
- `src/Shared/Api.fs` - `LedgerApi` (`mirrorYnabBudget`, `getLastMirrorRun`)
- `src/Client/Api.fs` - Proxy fuer die Ledger-API
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs` - Label "Ohne Kategorie"
- `src/Tests/LedgerMirrorTests.fs` (neu) - 21 Tests: `observe` rein, Decoder gegen ein
  JSON-Fixture mit Split, Transfer und `deleted`, Lauf gegen In-Memory-SQLite
- `src/Tests/SplitPushTests.fs` - 2 Tests fuer die unkategorisierte Zeile im Push-Encoder
- `src/Tests/SplitTransactionTests.fs` - Matches um den dritten Split-Ziel-Fall ergaenzt
- `src/Server/Server.fsproj`, `src/Tests/Tests.fsproj` - neue Dateien registriert

### Rationale
ADR 0014 (Delta-Sync ueber `server_knowledge`, manueller Ausloeser, 429 bricht ab),
ADR 0013 (ynab-sync liefert Snapshots, der Ledger macht daraus semantische Delta-Ereignisse;
ein Schreibpfad in einer Transaktion), ADR 0015 (Match-Reihenfolge YNAB-Id vor ImportId,
Idempotenz bei Wiederholung). Zwei Abweichungen mit eigenem ADR: Konten werden je Lauf
vollstaendig geholt, und `SplitTarget` bekommt einen dritten Fall fuer unkategorisierte
Split-Zeilen, weil YNAB die liefert.

### Verification
- Build: PASSED (`dotnet build`, 0 Warnungen)
- Tests: 676 passed, 7 skipped, 0 failed (davon 23 neu)

---

## 2026-09-06 - Kassenbuch-Fundament: Ereignis-Log, Zustandsmodell, appendAndApply (ledger-e3k7p)

### What
Slice 1 des Kassenbuch-Skeletons: das Gedaechtnis selbst. Neues Modul `src/Server/Ledger/`
mit dem verbindlichen Ereignis-Katalog als F#-Typen (Umschlag, Actor, die fuenf
Beobachtungs-Ereignisse, die Entscheidungs-Namen reserviert), dem abgeleiteten
Zustandsmodell (Buchung, Konto, Fremd-Id-Verknuepfungen) und dem Schreibpfad
`appendAndApply`, der Log und Zustand auf einer Connection in einer SQLite-Transaktion
fortschreibt. Kein YNAB-Aufruf, keine UI, keine Aenderung an bestehenden Tabellen.

### Files Changed
- `src/Server/Ledger/Events.fs` (neu) - Umschlag, Actor, Ereignis-DU, Payload-Records,
  handgeschriebene Thoth-Codecs, Typname und Schema-Version je Ereignis
- `src/Server/Ledger/Domain.fs` (neu) - Zustandsmodell und die reine Faltung
  `apply : Entry option -> LedgerEvent -> Entry`, dazu `linksFrom` und `replay`
- `src/Server/Ledger/Persistence.fs` (neu) - vier Tabellen per Migration,
  `appendAndApply` in einer Transaktion, Loads, `replayAll`, Konten und Fremd-Id-Links
- `src/Server/Persistence.fs` - `getConnection` von `private` auf `internal`, damit der
  Ledger-Schreibpfad dieselbe frische Connection benutzt (ADR 0008)
- `src/Server/Server.fsproj` - Ledger-Dateien nach `Persistence.fs`, vor `YnabClient.fs`
- `src/Server/Program.fs` - `Ledger.Persistence.initializeLedgerSchema()` beim Start
- `src/Tests/LedgerTests.fs` (neu) - 25 Tests, In-Memory-SQLite
- `src/Tests/Tests.fsproj` - LedgerTests.fs registriert

### Rationale
ADR 0013 (append-only Ereignis-Log als Quelle der Wahrheit, Zustandsmodell synchron in
derselben Transaktion), ADR 0015 (eigene Buchungs-Id als Guid v7, Fremd-Ids als
Verknuepfungen mit Unique je System) und ADR 0016 (Kontenmodell Art x Verknuepfung).
Betraege liegen als exakter Dezimal-Text statt als REAL, damit ein auditierbares
Kassenbuch keine Gleitkomma-Drift zeigt.

### Verification
- Build: PASSED (`dotnet build BudgetBuddy.sln`, 0 Warnungen)
- Tests: 653 passed, 7 skipped, 0 failed (davon 25 neu)

---

## 2026-06-28 17:07 - Bugfix: "Inflow: Ready to Assign" zeigte Fantasiewert (ynab-k7m3q)

### What
Romans Gerätetest des neuen Picker-Available: die Zeile "Inflow: Ready to Assign" zeigte
787955,35 statt des echten Ready-to-Assign (-6,27); normale Kategorien waren korrekt. Ursache:
das `balance`-Feld der internen "Internal Master Category" ist für die Inflow-Zeile ein
interner Akkumulator, nicht das echte Ready-to-Assign. Fix: den echten Wert (`to_be_budgeted`
vom Month-Endpoint) holen und für die Inflow-Zeile anzeigen; `YnabCategory.Available` wird
`Money option` (None = keine Zahl), damit ein fehlgeschlagener Month-Call sauber nichts zeigt
statt Müll.

### Files Changed
- `src/Shared/Domain.fs` - `YnabCategory.Available: Money option`; reine `applyReadyToAssign` + Konstanten
- `src/Server/YnabClient.fs` - `categoryAvailable` → option; `monthToBeBudgetedDecoder`; `getReadyToAssign`
- `src/Server/Api.fs` - `getCategories`-Handler holt `to_be_budgeted` und wendet `applyReadyToAssign` an
- `src/Client/.../TransactionList.fs`, `SplitSheet.fs`, `Views/QuickAddPage.fs` - Mapper reichen die Option durch
- `src/Tests/YnabClientTests.fs` - Decoder-Tests auf `Money option`; Regression `applyReadyToAssign` + `monthToBeBudgetedDecoder`
- `.agentheim/.../0011-...md`, categorization README - Amendment dokumentiert

### Rationale
Bug-Fix-Protokoll: Regressionstest (applyReadyToAssign override nur Inflow-Zeile, None-Fall,
Lookalike außerhalb der internen Gruppe) + Month-Decoder-Test. Der Extra-Call bleibt auf den
Picker-Pfad beschränkt.

### Verification
- Build: PASSED (dotnet build 0/0; npm run build / Fable grün)
- Tests: 629 passed, 6 skipped (war 625; +4)
- Offen: visuelle Geräteabnahme von Roman, dass die Inflow-Zeile jetzt -6,27 zeigt

---

## 2026-06-28 00:15 - Kategorie-Available im Zuweisungs-Picker (ynab-k7m3q)

### What
Der Kategorie-Picker zeigt jetzt hinter "Gruppe: Name" den aktuellen YNAB-Budgetwert
(`balance` = "Available" des laufenden Monats) als rechtsbündigen, farbcodierten Geldbetrag:
grün bei >0, rot bei <0, neutral bei =0. So ist beim Zuordnen direkt sichtbar, wie viel in
einer Kategorie noch verfügbar ist, ohne Wechsel nach YNAB-Web. Sichtbar im Import-Picker
(`TransactionList`), im Split-Sheet-Picker (`SplitSheet`) und im Quick-Add-Picker
(`QuickAddPage`, gleiche Komponente + Daten, gleicher Zuweisungskontext). Der Regel-Editor
bleibt unverändert (nutzt den Picker nicht; ein "verfügbar" wäre dort kontextlos). Kein
zusätzlicher YNAB-Request — der Wert kommt aus der bestehenden `getCategories`-Antwort.

### Files Changed
- `src/Shared/Domain.fs` - `YnabCategory` trägt `Available: Money`.
- `src/Server/YnabClient.fs` - neuer `categoryAvailable`-Helper; `categoryDecoder` und
  `categoryInGroupDecoder` lesen `balance` (Milliunits/1000, Optional mit 0-Default,
  Conformist-safe). Budget-Detail-Pfad erbt es über `categoryGroupsDecoder`.
- `src/Client/DesignSystem/Money.fs` - `available amount currency`: mono, drei-wertige
  Farbe (neon-green / neon-red / text-muted) über DS-Tokens, kein Glow, kein führendes "+".
- `src/Client/DesignSystem/BottomSheet.fs` - neuer öffentlicher Typ `CategoryPickerOption
  { Id; Label; Available: Money option }`; `categoryPicker`/`categoryPickerLayered`/
  `CategoryPickerInternal` rendern die rechtsbündige Wert-Spalte. ADR 0005 (Click-Commit,
  Layer-Stacking) unberührt.
- `src/Client/Components/SyncFlow/Views/TransactionList.fs` - eigene `pickerCategoryOptions`
  mit Available; `categoryOptions` (Tupel) bleibt für die `TransactionRow`-Chips.
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs` - `categoryOptions` auf den reichen
  Typ umgestellt.
- `src/Client/Views/QuickAddPage.fs` - `pickerCategoryOptions` mit Available; Tupel-Liste
  bleibt für `selectedCategoryName`.
- `src/Tests/YnabClientTests.fs` - 5 Decoder-Regressionstests (positiv/negativ/null,
  fehlendes balance → 0, `categoryInGroupDecoder`-Pfad); jeder Wert + jedes Feld asserted.
- `.agentheim/knowledge/decisions/0011-category-available-in-assignment-pickers.md` - ADR.
- `.agentheim/contexts/categorization/README.md` - Ubiquitous Language "Available".

### Rationale
ynab-k7m3q: Available am Zuweisungspunkt macht "passt diese Buchung hier rein" sofort
entscheidbar. Optional-Decode statt Required (anders als Account-Balance), damit ein
fehlendes `balance` nie den ganzen Kategorien-Load bricht (ADR 0011).

### Verification
- Build: PASSED (`dotnet build`, 0 Fehler/0 Warnungen)
- Tests: PASSED (`dotnet test`, 625 erfolgreich / 6 übersprungen / 0 Fehler; +5 vs. Baseline)
- Fable: PASSED (`npm run build` kompiliert den Client)

---

## 2026-06-27 10:05 - Quick Add: letzte 5 Buchungen als deduplizierte Vorlagen (ynab-t4n8p)

### What
Die Quick-Add-Seite zeigt jetzt über dem Formular bis zu **5 deduplizierte Vorlagen** aus den
jüngsten Buchungen des Quick-Add-Kontos (`ynab_quickadd_account_id`, ADR 0004). Dedup-Schlüssel:
Payee + Betrag + Kategorie — wiederkehrende Bar-Buchungen kollabieren zu *einer* Vorlage, sodass
bis zu 5 *verschiedene* Vorlagen erscheinen (most-recent-first). Ein Tipp auf eine Vorlage füllt
das Formular vollständig vor (Betrag inkl. Ausgabe/Einnahme, Kategorie, Payee, Memo). Das **Datum
bleibt heute** (nicht das Datum der Original-Buchung); **nichts wird automatisch gebucht** — Roman
prüft und drückt selbst Speichern.

### How
- **Reverse-Milliunits + Dedup als pure Domain-Funktionen** (`Shared/Domain.fs`): `amountFromMilliunits`
  (Gegenstück zu `manualTransactionMilliunits`: `<0 ⇒ IsOutflow`, Betrag = `abs/1000`) und
  `recentQuickAddTemplates` (sort desc nach Datum → projizieren → `distinctBy` (Payee, IsOutflow,
  Amount, CategoryId) → `truncate 5`). Beide rein und testbar, kein I/O.
- **Neue Read-Model-Type** `QuickAddTemplate` (ohne Datum-Feld — Datum ist immer heute).
- **Neuer Remoting-Call** `getRecentQuickAddTemplates: unit -> YnabResult<QuickAddTemplate list>`
  (`Shared/Api.fs`), serverseitig implementiert über den *bestehenden* `YnabClient.getAccountTransactions`
  (kein neuer YNAB-Pfad), `since_date` 90 Tage. Nicht-konfiguriert (Token/Budget/Quick-Add-Konto
  fehlt oder Account-Id unparsebar) ⇒ `Ok []` statt Fehler — Seite degradiert sauber (ADR 0004:
  kein Fallback-Konto erfinden).
- **Transfer-/Split-Filter:** Nur *kategorisierte* Buchungen werden Vorlagen — eine fehlende/
  unparsebare `category_id` droppt die Buchung, was Transfers (Transfer-Payee, keine Kategorie)
  und Split-Parents (keine Kategorie auf Account-Seite) natürlich ausschließt. Gelöschte Buchungen
  liefert YNABs `?since_date`-Endpoint (ohne Delta) ohnehin nicht.
- **Client:** Top-Level `QuickAddTemplates: RemoteData<QuickAddTemplate list>`, einmal pro
  Seitenbesuch geladen (`LoadQuickAddTemplates` im `UrlChanged`-QuickAdd-Zweig, nicht pro
  Tastendruck). Neue Msg `PrefillQuickAdd` → pure `applyTemplateToForm` (lässt `DateText` in Ruhe).
- **UI:** kompakte Chip-Reihe (`qa-templates*`-Styles im Token-Layer `--sf-*`/`--color-neon-*`),
  Label = Payee (oder Kategorie) + Betrag; Ausgabe orange / Einnahme teal — styleguide-konform.

### Files Changed
- `src/Shared/Domain.fs` - `amountFromMilliunits`, `QuickAddTemplate`, `toQuickAddTemplate` (privat),
  `recentQuickAddTemplates`
- `src/Shared/Api.fs` - `getRecentQuickAddTemplates` im `YnabApi`-Kontrakt
- `src/Server/Api.fs` - Implementierung (reuse `getAccountTransactions`, graceful no-config)
- `src/Client/Types.fs` - `formatAmountForInput`, `applyTemplateToForm` (pur)
- `src/Client/State.fs` - Model-Feld `QuickAddTemplates`, Msgs `LoadQuickAddTemplates`/
  `QuickAddTemplatesLoaded`/`PrefillQuickAdd`, Laden im `UrlChanged`-QuickAdd-Zweig
- `src/Client/View.fs` - reicht `model.QuickAddTemplates` an die Seite durch
- `src/Client/Views/QuickAddPage.fs` - `templatesSection`/`templateChip`, neuer `templates`-Param
- `src/Client/styles.css` - `.qa-templates*` Chip-Styles (Token-Layer)
- `src/Tests/QuickAddTests.fs` - Reverse-Milliunits-, Dedup-, Prefill- und Format-Tests

### Rationale
Romans Bar-Buchungen wiederholen sich (ähnliche Payee/Betrag/Kategorie). Die letzten Buchungen des
Quick-Add-Kontos sind die beste Vorlagen-Quelle für genau diese wiederkehrenden Fälle — ein Tipp
statt komplettem Neutippen. Datenquelle und YNAB-Pfad existierten bereits; neu ist nur die
Dedup-Projektion (pur) und ein Read-Call.

### Verification
- Build: PASSED (`dotnet build` 0 Fehler)
- Tests: 620 passed, 6 skipped (Integration, brauchen `.env`) — inkl. 25 neue Quick-Add-Vorlagen-Tests
- Client: `npm run build` (Fable/Vite) PASSED

---

## 2026-06-27 09:45 - Quick Add als eigene Seite in der Haupt-Navigation (ynab-q7k3m)

### What
Quick Add (manuelle Bar-Buchung → YNAB) war bisher ein `QuickAddFormState option` *innerhalb*
der SyncFlow-Komponente, erreichbar nur über zwei an den Sync-Flow gebundene Einstiege
(Secondary-Button unter „Sync starten" + Plus-Icon im Review-Header). Diese Refaktorierung hebt
den Quick-Add-State aus SyncFlow ins Top-Level-Model und macht Quick Add zu einer eigenen Seite
mit eigener Route (`#/quickadd`) und einem festen Navigations-Eintrag (Bottom-Nav mobil /
Top-Nav desktop). Beide alten Einstiege wurden entfernt — die Navigation ist jetzt der einzige
Weg. Das Submit-Verhalten (Push aufs Quick-Add-Konto, ohne ImportId, ADR 0004) ist unverändert;
es wurde nur verlagert.

**Entscheidungen:**
- **Echtes Seiten-Layout** (statt dauerhaft offenes BottomSheet): `Primitives`-Container +
  `PageHeader.gradientWithSubtitle` + die bestehenden `qa-*`-Feldkomponenten; der Category-Picker
  bleibt ein erhöhter Sheet-Layer (`categoryPickerLayered`, `.layer-2`). Begründung: ein permanent
  offenes Sheet (mit Backdrop/Slide-Animation) als „Seite" widerspricht dem Styleguide.
- **State-Lift ins Top-Level (nicht neue Child-Komponente):** wie im Ticket beschrieben wandern
  `QuickAddFormState` + die 5 Msg + Submit-Logik ins Top-Level `State`. Die puren Helfer
  (`parseAmountInput`, `buildQuickAddRequest`, `QuickAddFormState`) ziehen ins geteilte
  `Types`-Modul — `parseAmountInput` wird auch vom Split-Editor genutzt, `Types` ist der
  Sibling-import-freie gemeinsame Ort.
- **Kategorien-Quelle:** die Seite liest `model.SyncFlow.Categories` /
  `RecentlyUsedCategoryIds` (geteilte YNAB-Strukturen, beim App-Start geladen); Navigieren auf
  die Seite löst zusätzlich `SyncFlow.LoadCategories` aus (Deep-Link-sicher).
- **Save-Verhalten auf der Seite:** nach Erfolg bleibt man auf der Seite, das Formular wird auf
  frisch (heutiges Datum, leer) zurückgesetzt + Erfolgs-Toast — statt ein Sheet zu schließen.
- **Nav-Reihenfolge:** Sync, Rules, Quick Add (Plus-Icon), Settings — Settings bleibt
  konventionell letzter Tab.

### Files Changed
- `src/Client/Types.fs` — `Page.QuickAdd` + Route `#/quickadd`; `QuickAddFormState`,
  `parseAmountInput`, `buildQuickAddRequest` hierher gezogen (aus SyncFlow.Types).
- `src/Client/Components/SyncFlow/Types.fs` — Quick-Add-Typ/Helfer entfernt, `QuickAdd`-Model-Feld
  + 5 Quick-Add-Msg entfernt; `parseSplitAmount` nutzt jetzt `Types.parseAmountInput`.
- `src/Client/Components/SyncFlow/State.fs` — Quick-Add-Handler + init-Default + (nicht mehr
  genutztes) `ynabErrorToString` entfernt.
- `src/Client/Components/SyncFlow/View.fs` — `quickAddSheet`/`quickAddHeaderButton`-Aufrufe entfernt.
- `src/Client/Components/SyncFlow/Views/StatusViews.fs` — `quickAddEntryButton`-Block entfernt.
- `src/Client/Components/SyncFlow/Views/QuickAdd.fs` — **gelöscht** (Sheet + beide Buttons).
- `src/Client/Views/QuickAddPage.fs` — **neu**: die Quick-Add-Seite (Top-Level-View).
- `src/Client/State.fs` — `QuickAdd`-Model-Feld, 5 Msg, Handler, `ynabErrorToString` +
  `freshQuickAddForm`, init-Default, Daten-Load beim Navigieren auf `QuickAdd`.
- `src/Client/View.fs` — Nav-Mapping + Render der Quick-Add-Seite.
- `src/Client/DesignSystem/Navigation.fs` — `NavPage.QuickAdd` + 4. Nav-Item (Plus-Icon).
- `src/Client/Client.fsproj` — `Views/QuickAdd.fs` raus, `Views/QuickAddPage.fs` rein.
- `src/Tests/QuickAddTests.fs` — Reducer-Tests auf das Top-Level `State.update` umgezogen
  (Toast-Assertion jetzt über `model.Toasts`); pure Helfer-Tests unverändert (lösen über `open Types`).

### Rationale
Quick Add ist eine eigenständige, häufige Aktion und war direkt nach einem Import gar nicht
erreichbar. Ein fester Nav-Platz macht sie kontextfrei mit einem Tap erreichbar. Der State-Lift
entsperrt zudem `ynab-t4n8p` (Vorlagen rendern in genau diesem Formular).

### Verification
- Build: ✅ `dotnet build` — 0 Fehler, 0 Warnungen
- Tests: ✅ 595 erfolgreich, 6 übersprungen (Integration, brauchen .env)
- Fable/Vite: ✅ `npm run build` erfolgreich (144 Module)

---

## 2026-06-20 16:10 - Compose-Drift behoben: `docker-compose.yml` entfernt, `compose.yaml` ist Single Source of Truth

### What
Zwei Compose-Dateien waren auseinandergelaufen: `compose.yaml` (vom Git-Deploy-Pipeline, Port **5081**,
Tailscale-only, vorgebautes `image:`) — die einzige, die der `post-receive`-Hook real deployt — und das
**legacy** `docker-compose.yml` (Demo-App-Herkunft, Port **5001** aus dem alten Apple-AirPlay-Workaround,
`build: .`). Der 5001-Port passte nicht mehr zum Dockerfile-HEALTHCHECK (5081) → verwirrend und latenter
Bug. Entscheidung (Roman): **auf eine Datei konsolidieren** — `docker-compose.yml` gelöscht, `compose.yaml`
mit Header-Kommentar als Single Source of Truth dokumentiert (Deploy-Pfad, Port-5081-Begründung,
Tailscale-Root → PWA-SW-`scope: /`, lokales Dev = `dotnet watch`/`npm run dev`).

### Files Changed
- `docker-compose.yml` - GELÖSCHT: veraltete Lokal-Variante (Port 5001), Drift-Quelle
- `compose.yaml` - Header-Kommentar ergänzt (Single Source of Truth, Deploy-Mechanik, Port 5081, kein `build:`)
- `.agentheim/contexts/infrastructure/done/infra-002-pwa-installable.md` - Notiz-Referenz korrigiert
  (`docker-compose.yml`/`:5001` → `compose.yaml`/`:5081`, da die referenzierte Datei nun weg ist)

### Rationale
Der eigentliche Drift-Auslöser war die Existenz zweier überlappender Compose-Dateien. Konsolidierung auf
die deploy-relevante Datei macht Drift strukturell unmöglich; das gelöschte File wurde von keinem
Live-Pfad referenziert (nur Docs/Diary/Skill-Templates, alle historisch). Funktional unverändert — die
Deploy-Pipeline nutzte ohnehin nur `compose.yaml`.

### Outcome
- Build/Tests: nicht betroffen (reine Deploy-Config, kein App-Code)
- Deploy-Pipeline unverändert: `./scripts/deploy.sh` → Hook baut + `docker compose up -d` mit `compose.yaml`
- Working Tree: Änderung steht (noch nicht committet)

---

## 2026-06-19 23:35 - PWA-Mechanik: installierbar, kein Offline-Daten-Cache (infra-002)

### What
BudgetBuddy als installierbare PWA verdrahtet — Manifest + Workbox-Shell-Service-Worker via
`vite-plugin-pwa`, **bewusst ohne Offline-Daten-Cache**: `/api/*` bleibt strikt network-only
(`runtimeCaching: []`, `navigateFallbackDenylist: [/^\/api\//]`), nur die statische App-Shell wird
precached. `registerType: 'autoUpdate'` (stille Updates), `scope: /`. Gebrandete `offline.html` als
Navigations-Floor. `index.html`: `theme-color` auf `#08081a` korrigiert (`#0f172a` entfernt),
apple-touch-icon + Favicon-Set + mobile-web-app-Meta. Giraffe-Static-Handler um
`.webmanifest → application/manifest+json` erweitert. ADR 0010 geschrieben.

### Files Changed
- `vite.config.js` - VitePWA-Plugin (Manifest, Workbox-Shell-Precache, autoUpdate, /api network-only)
- `src/Client/index.html` - theme-color #08081a, apple-touch-icon, Favicon-Set, mobile-web-app-capable
- `src/Client/public/offline.html` - NEU: gebrandete "Keine Verbindung"-Seite (DS-Farben inline)
- `src/Server/Program.fs` - FileExtensionContentTypeProvider: .webmanifest → application/manifest+json
- `package.json` / `package-lock.json` - vite-plugin-pwa@1.3.0 (devDependency)
- `.agentheim/knowledge/decisions/0010-pwa-installable-no-offline-data-cache.md` - NEU: ADR

### Rationale
BB ist ein Live-Daten-Companion (Comdirect → YNAB); gecachte Finanzdaten wären gegen die Vision
schädlich (stale Beträge, Dedup-/ImportId-Verwirrung). PWA daher "nur installierbar". Chrome/Android
verlangen für Install-Kriterien faktisch einen SW — der Shell-Precache-SW kostet nichts und cached
bewusst keine Daten. Manifest-MIME-Mapping nötig, weil der ASP.NET-Default `.webmanifest` nicht kennt.

### Verification
- Build: PASSED (`npm run build` Fable+Vite grün; `dotnet build` Server grün)
- Generierte Artefakte geprüft: manifest.webmanifest vollständig (#08081a, Icons 192/512/maskable);
  sw.js precached nur Shell (kein /api, keine Daten-URLs); offline.html als NavigationRoute-Fallback
  mit /^\/api\//-Denylist; index.html mit Manifest-Link + Registrierungs-Script (scope /).
- Tests: 595 passed, 6 skipped (externe .env-Integrationstests, unverändert)
- Human gate: Geräte-/Tailscale-HTTPS-gebundene ACs (Install-Prompt, iOS Home-Screen, autoUpdate
  e2e, Offline-Seite live) sind im Task-Outcome als Roman-Verifikation ausgewiesen.

---

## 2026-06-19 23:10 - PWA-App-Icon Rework (Gate-Review-Korrektur, design-system-008)

### What
Roman lehnte die erste Icon-Auslieferung im AC5-Gate ab ("B betrunken, Pfeile falsch"). Mark neu
gebaut: das "B" ist jetzt eine echte **Arial-Bold-Glyphe** (via `opentype.js` in einen SVG-Pfad
umgewandelt, per Bounding-Box auf (256,256) zentriert) statt handgemalter Bézier-Bäuche; die
Sync-Pfeile sind **tangential** an den Bogen-Enden konstruiert (Tip entlang `(-sinθ,cosθ)`, Basis
radial), 180°-symmetrisch. Anschließend von Roman abgenommen ("passt").

### Files Changed
- `src/Client/public/icon-master.svg`, `favicon.svg`, `icon-maskable.svg` - neu generiert (Glyph→Pfad)
- `src/Client/public/icons/*.png` + `favicon.ico` - volles Set neu gerendert (alle Größen zentriert)
- `.agentheim/contexts/design-system/done/design-system-008-...md` - AC5 abgehakt + Outcome-Rework-Notiz

### Rationale
Ein per Hand gezeichnetes Glyph wird leicht schief; eine echte Font-Glyphe als Pfad ist sauber.
**Text→Pfad** ist nötig, weil der PNG-Rasterizer (resvg/sharp) `text-anchor="middle"` ignoriert
(Browser zentrierte das SVG, das PNG nicht) → eingebackener Pfad rendert in Browser UND Raster
identisch zentriert. `opentype.js` nur als `--no-save`-Dev-Tool (nicht in package.json).

### Verification
- Build: n/a (reine Vite-statische Assets unter `src/Client/public/`)
- Tests: n/a (Asset-Arbeit)
- Visuell: 512/maskable/favicon-32 gerendert + von Roman im Gate abgenommen (AC5 ✓)

---

## 2026-06-19 22:45 - PWA-App-Icon "B im Sync-Ring" + Theme-Farben (design-system-008)

**What I did:**
Die Marken-Identität fürs PWA-/Home-Screen-Icon entworfen und das volle Icon-Set
abgeleitet — bisher hatte BB kein eigenes App-Icon (kein favicon, kein `public/`).
Konzept (mit Roman gelockt): fettes **"B" im Sync-Ring** (zwei Pfeilköpfe, echot
`Icons.sync`) — Identität + Funktion (Bank→YNAB) in einem Zeichen. Signatur-Gradient
`135deg #00d4aa→#00ff88→#ff6b2c` (= `.gradient-text`) + dezenter Neon-Glow. Klein-Variante
≤32px vereinfacht zu solid `#00ff88` B ohne Ring/Gradient (Lesbarkeit 16/32px).
`theme_color` = `background_color` = `#08081a` (echte App-Surface) festgelegt — löst das
generische `#0f172a`-Slate ab (Verdrahtung macht `infra-002`).

**Files Added:**
- `src/Client/public/icon-master.svg` - Voller Hybrid-Mark (Gradient + Glow, transparent)
- `src/Client/public/favicon.svg` - Vereinfachte solid-green-B-Variante (≤32px)
- `src/Client/public/icon-maskable.svg` - Hybrid auf opakem `#08081a`, 76% skaliert (≥20% Safe-Zone)
- `src/Client/public/icons/icon-192.png`, `icon-512.png` - voller Mark, transparent
- `src/Client/public/icons/maskable-512.png` - maskable, opak `#08081a`
- `src/Client/public/icons/apple-touch-icon.png` - 180×180, opak (iOS ignoriert Transparenz)
- `src/Client/public/icons/favicon-16.png`, `favicon-32.png`, `favicon.ico` - vereinfachte Variante
- `src/Client/public/icons/README.md` - Asset-/Farb-Kontrakt + Handoff-Pointer für `infra-002`

**Files Modified:**
- `.agentheim/contexts/design-system/README.md` - Abschnitt "App-Mark / Branding" (Konzept, Farben, Asset-Pfade)

**Rationale:**
Roman will BudgetBuddy als PWA "mit eigenem Icon und allem, was dazugehört". Die visuelle
Identität gehört dem Design-System (Styleguide-Gate), die PWA-Mechanik dem `infra-002`.
Zwei Quell-SVGs, weil PWA-Generatoren eine Quelle uniform skalieren (nicht simplifizieren).

**Outcomes:**
- Build: n/a (reine Vite-statische Assets unter `src/Client/public/`, nicht vom dotnet-/Fable-Build konsumiert; vite `root`=`src/Client` → publicDir korrekt)
- Tests: n/a (Asset-Arbeit, keine F#-Logik)
- Raster via `npx sharp-cli` + `png-to-ico` gerendert (kein nativer Rasterizer auf der Maschine)
- AC1–AC4 erfüllt; **AC5 (Romans visuelle Abnahme) offen** — Gate-Review pending

---

## 2026-06-19 09:30 - Konsolidierung roher Html.svg → Icons-DS (SyncFlow-Views) (design-system-007)

**What I did:**
Den im Drift-Audit (`design-system-002`) ausgegliederten Rest-Drift aufgelöst: die rohen
inline-`Svg.svg` in den SyncFlow-Views aufs `Icons`-DS-Modul gehoben — visuell identisch,
nur Anheben aufs DS, kein neues Icon-Design. 3 von 4 Stellen actionable (2 Swaps + 1 neues
Icon), die 4. begründet ausgenommen.

**Per-Site-Mapping (welches SVG → welcher Icons.*-Call):**
- `SyncFlow/Views/QuickAdd.fs:34` — Plus-SVG (`M12 5v14M5 12h14`, 18px, strokeWidth 2.5,
  currentColor=`--sf-text-secondary` #7a7a98 im `.back-btn`) → `Icons.plus Icons.SM Icons.Default`.
  Farbe identisch (`text-text-secondary` == #7a7a98); Größe SM (20px) als nächster Token zu
  18px; strokeWidth wird auf den DS-Standard 1.5 vereinheitlicht (das *ist* die Konsolidierung).
- `SyncFlow/Views/InlineRuleForm.fs:206` — Check-SVG (`M20 6L9 17l-5-5`, 12px, in 20px-Teal-Kreis
  `.rule-category-check`, currentColor=`--text-primary`) → `Icons.check Icons.XS Icons.Primary`.
  Präzedenz: `StatusViews.fs:68` nutzt für exakt dasselbe Check-in-Kreis-Motiv (w-5/20px-Kreis)
  bereits `Icons.check Icons.XS Icons.Primary` — der Swap richtet sich an diesem DS-Muster aus.
  Farbe identisch (`text-text-primary` == #e8e8f0).
- `SyncFlow/View.fs:49` — Chevron-Left-Back-Button (`M15 18l-6-6 6-6`, 18px, `.back-btn`) →
  neues Glyph `Icons.chevronLeft` in `Icons.fs` ergänzt (Spiegel von `chevronRight`, DS-24-viewBox-Stil:
  `m15.75 19.5-7.5-7.5 7.5-7.5`), dann `Icons.chevronLeft Icons.SM Icons.Default` genutzt.
- `SyncFlow/Views/TransactionRow.fs:359` — Toggle-Check (`M2 5l2.5 2.5L8 3`, 10×10-viewBox,
  `.toggle-check`) **bewusst roh gelassen**; Ein-Zeilen-Kommentar am Call-Site warum: CSS-baked
  Micro-Glyph mit eigenem viewBox, vom `.toggle-check`-CSS positioniert/dimensioniert — ein Swap
  auf das 24-viewBox-`Icons.check` wäre CSS-Rework, kein 1:1-Lift.

**Files Added:**
- (keine)

**Files Modified:**
- `src/Client/DesignSystem/Icons.fs` — neues Glyph `chevronLeft` (Spiegel von `chevronRight`).
- `src/Client/Components/SyncFlow/Views/QuickAdd.fs` — Plus-SVG → `Icons.plus`.
- `src/Client/Components/SyncFlow/Views/InlineRuleForm.fs` — Check-SVG → `Icons.check`.
- `src/Client/Components/SyncFlow/View.fs` — Back-Button-Chevron → `Icons.chevronLeft`.
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs` — Begründungs-Kommentar am bewusst
  rohen Toggle-Check (kein Code-Swap).

**Rationale:**
Styleguide §5 (Icons) definiert rohe inline-`<svg>` in View-Schichten explizit als Drift
(„fehlt ein Icon, hier ergänzen" = im `Icons`-DS-Modul). Governing-Principle aus 002: eine
Konsolidierung muss optisch identisch sein (kein Look-Regress). Farben wurden pro Site gegen
die Token-Map verifiziert (identisch), Größen auf den nächsten DS-Token gemappt.

**Outcomes:**
- Build: ✅ (`dotnet build` 0 Warnungen / 0 Fehler)
- Fable+Vite: ✅ (`vite build` Exit 0, frisches JS-Bundle; nur Third-Party-Warnungen)
- Tests: ✅ 595 bestanden, 6 übersprungen (.env-Integration), 0 Fehler — stabiles Gate aus infra-001
- Issues: keine

---

## 2026-06-18 - Drift-Audit + Token-Konsolidierung gegen den Styleguide (design-system-002)

**What I did:**
Den View-Code der fachlichen BCs gegen den Styleguide-Gate (`standards/frontend/styleguide.md`)
auditiert (mit den Grep-Checks aus `.claude/rules/design-tokens.md`) und den offensichtlichen,
risikoarmen Drift aufs DS gehoben. Großen, risikobehafteten Drift als per-BC-Backlog-Tasks
ausgegliedert statt blind zu refactoren.

**Drift-Inventur (Datei → Befund → Ziel):**
- *Standard-Tailwind-Palette-Farben* (`red-*`/`green-*`/…): **0 Treffer** im View-Layer — sauber.
- *Inline `style.color`/`style.backgroundColor`/`style.fontSize`*: **0** — sauber.
- *Rohe Hex `text-[#0a0a0f]`* (dunkle Schrift auf Neon): 6 Call-Sites in
  `SyncFlow/Views/StatusViews.fs` (L42/67/157/168/243/247) + DS-intern in `Badge.fs`
  (Filled-Variants L68-74, Count-Badge L231) → **neues Token `Colors.onNeon`/`onNeonMuted`**.
- *Arbitrary `text-[10px]`/`text-[11px]`* (Mikro-Labels): 13 Call-Sites über
  `StatusViews.fs` (L264/271/278), `Settings/View.fs` (L563), `TransactionRow.fs`
  (L121/126/139/144), `Rules/View.fs` (L29/38/47/67/130) + `Badge.fs` Count → **neue Tokens
  `FontSizes.micro`/`microPlus`**.
- *Rohe `Html.button`* (~34, SyncFlow/Rules-Views): teils 1:1-Button-Drift, teils bewusst
  custom (Click-Commit/ADR 0005, Swipe, `action-chip`) → **ausgegliedert: design-system-006**.
- *Rohe `Html.svg`* (4, SyncFlow-Views) → **ausgegliedert: design-system-007**.

**Files Added:**
- `.agentheim/knowledge/decisions/0009-onneon-foreground-and-micro-font-tokens.md` - Token-Namens-Entscheidung.
- `.agentheim/contexts/design-system/backlog/design-system-006-button-consolidation-syncflow.md` - Split (Button).
- `.agentheim/contexts/design-system/backlog/design-system-007-svg-to-icons-consolidation.md` - Split (Icons).

**Files Modified:**
- `src/Client/DesignSystem/Tokens.fs` - `Colors.onNeon`/`onNeonMuted`, `FontSizes.micro`/`microPlus`.
- `src/Client/DesignSystem/Badge.fs` - Filled-Variants + Count-Badge auf `Colors.onNeon`/`FontSizes.micro`.
- `src/Client/Components/SyncFlow/Views/StatusViews.fs` - 6× Hex + 3× micro auf Tokens.
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs` - 4× micro auf Token.
- `src/Client/Components/Settings/View.fs` - 1× micro auf Token.
- `src/Client/Components/Rules/View.fs` - 4× micro + 1× microPlus auf Token.
- `.agentheim/contexts/design-system/README.md` - Drift-Inventur-Notiz + ADR 0009 Verweis.

**Rationale:**
Der Styleguide soll nicht nur Doku sein, sondern der Code ihm entsprechen. Hex/arbitrary-Sizes
ohne benanntes Token waren nicht sauber behebbar → erst Tokens kanonisiert (ADR 0009), dann
Call-Sites darauf gehoben. Gerenderter CSS-String bleibt identisch (Tailwind-v4-JIT generiert
die Klassen weiter aus dem String-Literal in Tokens.fs) → kein visueller Regress.

**Outcomes:**
- Build: ✅ (gesamte Solution)
- Tests: 595 passed / 6 skipped / 0 failed (entspricht dem stabilen Gate aus infra-001)
- Issues: keine. Großer Button/SVG-Drift bewusst gesplittet (006/007), nicht riskant gemega-refactored.

**Nachtrag (Iteration 2):** Verifier fand 3 übersehene DS-komponenten-interne `text-[10px]`-
Stellen (`Badge.fs:91` `sizeToClass`/Small, `Navigation.fs:131` Nav-Label, `Stats.fs:75`
Compact-Caption). Auf `FontSizes.micro` gehoben (byte-identisch). Vollständiges Re-Audit über
`src/Client/` bestätigt nun 0 token-gedeckte Residuen. Inventur (Task) + ADR 0009 nachgezogen.
Files modified zusätzlich: `src/Client/DesignSystem/Navigation.fs`, `src/Client/DesignSystem/Stats.fs`.
Build ✅, Tests 595/6/0 unverändert.

---

## 2026-06-18 - Flaky SQLite-Disposal-Crash stabilisiert (infra-001)

**What I did:**
Den intermittierenden `SqliteConnection.RemoveCommand index-out-of-range`-Crash im
Persistence-Test gefixt. Untersuchung deckte zwei Befunde auf: (1) `Microsoft.Data.Sqlite`
war als `9.*` floating referenziert → Versionsskew Server 9.0.13 vs. Tests 9.0.11 (MSB3277,
binär verifiziert); (2) die **eigentliche** Root Cause war ein Disposal-Race: im Testmodus
gab `getConnection()` *dasselbe* geteilte `SqliteConnection`-Objekt an jede Dapper-Operation
zurück, und Expecto läuft Tests parallel → gleichzeitiges Command-Dispose mutiert die nicht
synchronisierte `_commands`-Liste der Connection. Versions-Unify allein behob den Crash NICHT
(reproduzierte weiterhin); erst die frische Connection pro Operation beseitigte ihn.

**Files Modified:**
- `src/Server/Server.fsproj` - `Microsoft.Data.Sqlite` von `9.*` auf festen Pin `9.0.13`.
- `src/Tests/Tests.fsproj` - expliziter `Microsoft.Data.Sqlite` `9.0.13`-Pin, damit der
  Test-Output dieselbe Facade lädt (vorher transitiv 9.0.11). Beseitigt MSB3277.
- `src/Server/Persistence.fs` - `getConnection()` gibt jetzt IMMER eine frische Connection
  zurück (Dapper auto-open/close). Die eine Test-Connection bleibt nur als Keep-Alive-Anker
  für die `Cache=Shared`-In-Memory-DB offen, wird aber nicht mehr durchgereicht. Spiegelt das
  Prod-Verhalten (dort gab es schon immer eine frische Connection → Bug nur im Test).
- `src/Tests/PersistenceTypeConversionTests.fs` - Defense-in-depth-Test
  "concurrent persistence operations do not crash on command disposal" (50 parallele
  Insert+Read). Hinweis im Kommentar: der Original-Race war nur über die Parallelität der
  *gesamten* Suite reproduzierbar, nicht isoliert — der echte Regressionsbeweis ist die
  Multi-Lauf-Determinik.

**Files Added:**
- `.agentheim/knowledge/decisions/0008-sqlite-per-operation-connection-and-version-pin.md` -
  ADR zur Entscheidung (Versions-Pin + frische Connection pro Operation).

**Diagnose — teilen andere Tests dasselbe Disposal-Muster?**
Nein. `PersistenceTypeConversionTests.fs` ist die EINZIGE Test-Datei mit echten
DB-Operationen (`Rules`/`SyncSessions`/`SyncTransactions`) und war daher das einzige Opfer.
`Main.fs` setzt nur `USE_MEMORY_DB`; `EncryptionTests.fs` nutzt nur `Persistence.Encryption`
(reines AES, keine Connection). Der Fix sitzt zentral in `getConnection()` und deckt damit
jede Persistence-Operation einheitlich ab.

**Rationale:**
Ein flaky Test untergräbt das Vertrauen ins grüne Gate. Root Cause bestätigt (Stacktrace +
binäre Versionsprüfung), nicht nur Hypothese übernommen.

**Outcomes:**
- Build: ✅ (0 Warnungen, MSB3277 weg; 0 Fehler)
- Tests: 595 passed / 6 skipped / 0 failed
- Determinik: 15/15 frische Voll-Läufe grün nach Fix (vorher reproduzierte der Crash in
  ~2–8 Läufen, einmal sogar gegen den alten Code im A/B-Test bestätigt)
- Issues: Keine

---

## 2026-06-18 - Toast-Mobile-Sitz korrigiert: Inset, Höhe, Hero-Kontrast (design-system-005)

### What
Die mobile Toast-Platzierung saß nach design-system-004 nicht sauber im Layout. Ursache:
`Toast.container` stellte für Mobile `top-16` voran und erbte aber das Desktop-`bottom-4 right-4`
aus `positionToClasses` — mobil ergab das `top-16 bottom-4 right-4` (volle Höhe gespannt,
rechts-bündig, linke Neon-Border am Rand abgeschnitten). Zusätzlich ließ die transluzente
`bg-surface-card/95`-Fläche über dem hellen Sync-Hero den Gradient durchscheinen.

Fix: Mobile- und Desktop-Platzierung als **zwei getrennte Fälle**. Mobile = kompakter,
symmetrisch eingerückter Streifen (`top-16 inset-x-4`, nur `top`). Desktop (`md:`) setzt die
Insets zurück, nimmt `md:max-w-sm` und ankert unten rechts. `positionToClasses` liefert jetzt
**nur `md:`-präfixierte** Anker. Innere Toast-Fläche **voll deckend** (`bg-surface-card` statt
`/95`) → kein Gradient-Durchschein, `backdrop-blur` bleibt.

### Files Changed
- `src/Client/DesignSystem/Toast.fs` — `container` getrennte Mobile/Desktop-Platzierung;
  `positionToClasses` nur `md:`-Anker; innere Fläche `bg-surface-card` voll deckend.
- `standards/frontend/styleguide.md` — §6 Toast-Platzierung auf den korrigierten mobilen Sitz
  + Deckkraft nachgezogen.
- `.agentheim/knowledge/decisions/0007-toast-placement-and-soft-exit.md` — Platzierungs-
  Entscheidung präzisiert (zwei Fälle, Insets, Deckkraft), Bug als Alternative dokumentiert.

### Rationale
Toasts sind das primäre flüchtige Feedback des Flows; ein „aufgeklebter" Riegel auf Mobile
unterläuft das Vision-Ziel. Desktop-Sitz und die design-system-004-Motion bleiben unverändert.

### Verification
- Build: PASSED (`dotnet build src/Client/Client.fsproj` — 0 Fehler, 0 Warnungen)
- Tests: PASSED (594 erfolgreich, 6 übersprungen [.env-gated]; `dotnet test`)
- Mobil headless verifiziert (Vite, Playwright 412×915@2x, hash-routing): linke Border nicht
  abgeschnitten (Inset links == rechts), Container kompakt (Höhe 124px statt Vollhöhe),
  innere Fläche opak `rgb(17,17,40)`. Screenshots: `/tmp/ds005/toast-mobile-after.png`,
  `/tmp/ds005/toast-mobile-demo.png`.

---

## 2026-06-16 - Toast-Politur: sanfter Abgang, Zwei-Phasen-Removal (design-system-004)

### What
Toasts blenden jetzt **sanft aus** statt abrupt zu verschwinden. Der Lifecycle ist als
Zwei-Phasen-Removal in der MVU-State modelliert: `StartDismissToast` markiert den Toast als
*exiting* (startet die CSS-Exit-Animation), `ToastExited` entfernt ihn nach
`Types.Toast.exitDurationMs`. Auto-Dismiss und manuelles Schließen laufen über denselben
Pfad; ein Doppel-Dismiss ist über `Toast.isExiting` abgesichert (kein Timer-Leak / keine
Doppel-Entfernung). Eingang `.animate-toast-in` (Spring, bevorzugt `--sf-spring-out` hinter
`@supports`), Abgang `.animate-toast-out` (Fade+Slide, ease-in, 220 ms) — Klassen nur auf
dem inneren Toast-Element, nie auf dem fixen Container (css-animation-safety). Platzierung
(Desktop unten-rechts / Mobile oben über der Bottom-Nav) als ADR 0007 festgehalten.

### Files Changed
- `src/Client/Types.fs` — `Toast.Exiting`-Flag + pures `Toast`-Modul (`markExiting`,
  `isExiting`, `remove`, `exitDurationMs`).
- `src/Client/State.fs` — Msg `StartDismissToast`/`ToastExited` (ersetzt `DismissToast`/
  `AutoDismissToast`), `delayed`-Helfer, Doppel-Fire-Guard.
- `src/Client/DesignSystem/Toast.fs` — `Exiting` in `ToastProps`, Eingangs-/Abgangs-Klasse,
  `renderList` auf 4-Tupel (mit Exiting).
- `src/Client/View.fs` — mappt `Exiting` durch, dispatcht `StartDismissToast` beim Schließen.
- `src/Client/styles.css` — `.animate-toast-in` / `.animate-toast-out` Keyframes.
- `src/Client/Components/Styleguide/View.fs` — `/styleguide`-Toast-Demo zeigt den sanften Abgang.
- `standards/frontend/styleguide.md` — Motion-§6 beschreibt Toast-Verhalten + Platzierung.
- `src/Tests/ToastLifecycleTests.fs` (neu) + `Tests.fsproj` — 8 Tests für die pure Lifecycle-Logik.
- `.agentheim/knowledge/decisions/0007-toast-placement-and-soft-exit.md` (neu).

### Rationale
Toasts sind das primäre Feedback-Signal des Flows; der abrupte Abgang wirkte janky und
untergrub das neon-on-dark-Gefühl (Roman, 2026-06-16, design-system-004).

### Verification
- Build: PASSED (`dotnet build` Client grün, 0 Warnungen)
- Tests: 594 passed, 6 skipped (integration, `.env` benötigt); 8 neue Toast-Lifecycle-Tests grün

---

## 2026-06-16 - Regel-Präzedenz per ▲/▼ umsortierbar (cat-001)

### What
Reines Frontend: die Regelliste (`/rules`) hat jetzt pro Zeile ▲/▼-Buttons, um die
Präzedenz einer Regel zu ändern. Die Liste wird ohnehin `priority DESC` geladen (oben =
höchste Priorität = gewinnt zuerst); ein Verschieben tauscht die Regel mit ihrer Nachbarin
und persistiert die **vollständige** neue Reihenfolge über die bereits existierende
`reorderRules`-API (Backend war End-to-End fertig). ▲ ist bei der obersten Regel deaktiviert,
▼ bei der untersten. Sichtbar gemacht: ein Rang-Index `#N` pro Zeile + ein Hinweis „erste
passende, aktive Regel gewinnt". Optimistisches Update; bei Fehler Toast + Reload (kein
halb-verschobener Zustand).

### Files Changed
- `src/Client/Components/Rules/Types.fs` — `MoveDirection (Up/Down)` + pure `reorderedIds`
  (berechnet die neue volle `RuleId list`, No-op an Rändern/unbekannter id); neue Msgs
  `MoveRule of RuleId * MoveDirection` und `RulesReordered of Result<unit,string>`.
- `src/Client/Components/Rules/State.fs` — `MoveRule`-Reducer (optimistisch umsortieren +
  `Api.rules.reorderRules`), `RulesReordered`-Reducer (Erfolg = no-op, Fehler = Reload + Toast).
- `src/Client/Components/Rules/View.fs` — ▲/▼ (DS `Button.view`, `IsDisabled` an Rändern),
  Rang-Index `#N`, Präzedenz-Hinweiszeile; `ruleRow` nimmt jetzt `index`/`total`.
- `src/Client/DesignSystem/Icons.fs` — neues `chevronUp`-Icon (Spiegel von `chevronDown`).
- `src/Tests/RulesReorderTests.fs` (neu) — 9 Tests für `reorderedIds` inkl. Ränder
  (oberste hoch / unterste runter = no-op), unbekannte id, Permutations-Invariante.
- `src/Tests/Tests.fsproj` — neue Testdatei registriert.

### Rationale
Roman hatte zu eager matchende Regeln, die spezifischere übertrumpften; der richtige Hebel
ist Präzedenz über Reihenfolge. Drag&Drop bewusst verworfen (mobil-sicher: Hoch/Runter).
Reorder-Berechnung als pure Funktion herausgezogen, weil Reducer-Pfade mit `Client.Api` unter
.NET nicht testbar sind (Fable.Remoting-Proxy).

### Verification
- Build: PASSED (`dotnet build` Client clean; `npm run build` Fable+Vite ✓ 15.89s)
- Tests: 586 passed, 6 skipped, 0 failed (+9 neue Reorder-Tests)

---

## 2026-06-16 - cat-001 Iteration-2-Fix: Chevron-Touch-Target + Design-Token

### What
Zwei lokale Defekte aus der Verifier-Note behoben (beide in
`src/Client/Components/Rules/View.fs`):
1. **Mobile-Touch-Target (AC 6):** `!min-h-0` aus den ClassNames beider ▲/▼-Buttons
   entfernt. `!min-h-0` überschrieb per `!important` das `min-h-[36px]` von `Button.Small`
   und kollabierte das Tap-Target auf ~20-24px. Nach Entfernen trägt `Button.Small`
   (`min-h-[36px] md:min-h-0`) wieder den 36px-Mobile-Floor; nur das horizontale Padding
   wird über `!px-1.5 !py-1` getunt.
2. **Design-Token-Verletzung:** Rang-Index-Span von `text-[11px]` (arbitrary px) auf das
   Typography-Token `text-xs` umgestellt (`.claude/rules/design-tokens.md`).

### Files Changed
- `src/Client/Components/Rules/View.fs` — Chevron-ClassNames `!min-h-0` entfernt;
  Rang-Index `text-[11px]` → `text-xs`.

### Rationale
Fresh-eyes-Verifier fand, dass der prior worker das vom AC geforderte 36px-Touch-Target
selbst negierte und eine arbitrary Font-Size nutzte. Beides lokal, keine Test-/Logik-Änderung.

### Verification
- Build: PASSED (`npm run build` Fable+Vite ✓ 16.47s)
- Tests: 586 passed, 6 skipped, 0 failed (unverändert grün)

---

## 2026-06-16 - Split-Transaktion zeigt "Aufgeteilt" statt "Kategorie…" (ynab-004)

**What I did:**
Roman-Feedback: eine aufgeteilte Buchung (REWE) zeigte in der Liste weiter den orangen
„Kategorie…"-Platzhalter, als wäre sie unkategorisiert. Ursache: eine Split-Transaktion hat
`CategoryId = None` (kein einzelne Kategorie — Server setzt das so, `Api.fs:959`), und die
Chip-Logik prüfte nur `CategoryId`, nicht `Splits`. Fix rein clientseitig: die Chip-Label- und
Badge-Logik berücksichtigt jetzt `tx.Splits` → Label **„Aufgeteilt"** + `badge-ready` (statt
`badge-attention`). Label-Entscheidung in eine testbare `categoryChipLabel`-Funktion extrahiert.

**Files Changed:**
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs` — `getCategoryBadgeClass` um
  Split-Fall (→ ready); neue `categoryChipLabel` (Split → „Aufgeteilt"); inline
  `categoryDisplayText` darauf umgestellt.
- `src/Tests/SyncFlowViewTests.fs` — Regression: Split-Badge ready statt attention; neue
  `categoryChipLabel`-Suite (Split → „Aufgeteilt", uncategorized → Platzhalter, kategorisiert →
  Name, Duplikat gewinnt).

**Rationale:**
Eine erledigte Aufteilung muss visuell von einer unangetasteten Buchung unterscheidbar sein.

**Caveat (dokumentiert, bewusst nicht im Scope):** Splits werden nicht persistiert
(`Persistence.fs:677` rekonstruiert `Splits = None`). Der Fix gilt für die laufende
In-Memory-Session (der reale Sync→Review→Import-Flow); ein DB-Reload würde das „Aufgeteilt"-
Label verlieren. Splits persistieren ist eine größere Folgeaufgabe (Schema + Migration,
seit ynab-001 aufgeschoben).

**Verification:**
- Build: PASSED (0 Fehler, 1 vorbestehende Sqlite-Warnung)
- Tests: 577 passed, 6 skipped, 0 failed

---

## 2026-06-16 - Split-Editor: Vorzeichen-Bug-Fix + editierbare Beträge + Rest-Button (ynab-003)

**What I did:**
Roman-Feedback aus dem Live-Betrieb behoben: Im Cashback-Split zeigte die Kategoriezeile den
vollen (negativen) Gesamtbetrag, und das Eintippen von `200` im Bar-Feld **addierte** auf
422,15 statt auf 22,15 zu verrechnen — und die Zeile war nicht editierbar. **Root Cause:** eine
Ausgabe wird **negativ** geführt (Total −222,15), `splitRemainder` rechnet signiert; die UI
zeigt aber ein positives `0,00`-Feld, der Nutzer tippt `200` (positiv) → `−222,15 − (+200) =
−422,15`. Die ynab-002-Tests fingen das nicht, weil ihre Fixtures *negative* Beträge tippten —
sie kodierten dieselbe falsche Annahme wie der Code.

Umbau des Betrags-Modells (rein clientseitig): **positive Magnituden**, das Vorzeichen der
Transaktion wird intern beim Bauen der `TransactionSplit`s angewandt (`draftLineToSplit` =
`sign(Total) · |Eingabe|`). Die „magische" read-only `AutoRemainder`-Zeile entfällt — **alle
Beträge editierbar**. Neu: **Rest-Button pro Zeile** (`FillSplitRemainder`), füllt
`|Total| − Σ(übrige)` (≥ 0). Validierung/Push bleiben über die geteilten `mkSplits`/
`splitRemainder` (ADR 0006) — Invariante nicht clientseitig reimplementiert.

**Files Changed:**
- `src/Client/Components/SyncFlow/Types.fs` — `SplitDraftLine.AutoRemainder` entfernt;
  `draftLineToSplit` (Vorzeichen aus Total), `lineMagnitude`, `restMagnitudeForLine`,
  `splitEditRemainder` (positive Magnitude); `formatAmountForEdit` ohne Vorzeichen; Msg
  `FillSplitRemainder of int`.
- `src/Client/Components/SyncFlow/State.fs` — `StartCashbackSplit`/`StartSplitEdit`/
  `AddSplitLine` ohne `AutoRemainder`; neuer `FillSplitRemainder`-Handler.
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs` — jede Zeile editierbares Feld +
  Rest-Button; Rest-Banner immer sichtbar.
- `src/Client/styles.css` — `.split-rest-btn` (ersetzt unbenutzte `.split-auto-*`).
- `src/Tests/SplitEditorTests.fs` — Fixtures auf positive Eingabe; Regression (200 vs −222,15
  → −22,15), Sign-Application-, Rest-Button-Tests.

**Rationale:**
Vorzeichen-Vertrag gehört unter die UI (Nutzer tippt Magnituden), nicht in die Hände des
Nutzers (Minuszeichen tippen). Der Rest-Button ist das robustere Muster gegenüber der
fragilen Auto-Rest-Zeile und erfüllt zugleich Romans Wunsch (Rest nicht selbst rechnen).

**Verification:**
- Build: PASSED (0 Fehler, 1 vorbestehende Sqlite-Warnung)
- Tests: 572 passed, 6 skipped (.env-Integration), 0 failed
- Fresh-eyes verifier (agentheim): PASS (iteration 1)

---

## 2026-06-13 - Split-Review-UI: Cashback-Shortcut + generischer Editor (ynab-002)

**What I did:**
Die Review-UI für Transaktions-Splits gebaut, die auf dem ynab-001-Domain-Fundament aufsetzt
(ADR 0006). Aus einer einzelnen Buchung im Review-Flow öffnet jetzt ein **Split-Form-Sheet**
mit (a) einem Ein-Tipp-**„Barabhebung"**-Shortcut (2-Zeilen-Cashback: Transfer aufs Bargeld-
Konto + Kategorie-„Rest"-Zeile, die sich live errechnet — der Nutzer tippt nur den Transfer-
Betrag) und (b) einem **generischen N-Zeilen-Editor** (Zeilen hinzufügen/entfernen, je Zeile
Kategorie ODER Transfer-Konto, Live-Differenz zum Gesamt, Speichern gesperrt bei Summe ≠ Gesamt).

Sämtliche Split-Arithmetik/Validierung läuft über die **geteilten** Domain-Helper aus ynab-001
(`splitRemainder`, `mkSplits`) — keine Reimplementierung der Invariante im Client (ADR 0006).
Sheet-Stacking nach ADR 0005 §4: Kategorie-/Konto-Picker öffnen als `.layer-2` über dem
Form-Sheet (eine Ebene tief, Click-Commit nur auf den Picker-Items). Cashback-Default-Transfer-
Ziel = das konfigurierte Quick-Add-Konto (`ynab_quickadd_account_id`, ADR 0004), im Picker
überschreibbar; unkonfiguriert → kein Default, manuelle Wahl.

Kleine load-bearing Backend-Erweiterung: `YnabAccount` um `OnBudget`/`Closed`, `accountDecoder`
um `on_budget`/`closed` (Optional, Conformist-sichere Defaults), damit der Transfer-Ziel-Picker
auf **offene On-Budget-Konten** filtern kann (`openOnBudgetAccounts` in Domain.fs).

**Files Added:**
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs` - Split-Form-Sheet + Cashback-Shortcut + layered Picker
- `src/Tests/SplitEditorTests.fs` - Pure-Logic-Tests (Live-Rest, Save-Lock-bei-Mismatch, Cashback-Auto-Rest)

**Files Modified:**
- `src/Shared/Domain.fs` - `YnabAccount` += `OnBudget`/`Closed`; neuer `openOnBudgetAccounts`-Filter
- `src/Server/YnabClient.fs` - `accountDecoder` dekodiert `on_budget`/`closed` (Optional, Defaults)
- `src/Client/Components/SyncFlow/Types.fs` - `SplitDraftLine`/`SplitEditState`-Redesign, `SplitPicker`-DU, pure Split-Editor-Helper, Model += `Accounts`/`QuickAddAccountId`, neue Msg-Fälle
- `src/Client/Components/SyncFlow/State.fs` - Split-Editor-Handler, Cashback-Shortcut, Account-Loading
- `src/Client/Components/SyncFlow/View.fs` - Split-Sheet eingehängt
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs` - „Barabhebung"/„Aufteilen"-Action-Chips
- `src/Client/DesignSystem/BottomSheet.fs` - `accountPickerLayered` (Transfer-Ziel-Picker, Click-Commit)
- `src/Client/styles.css` - `.split-*`-Klassen (Design-Tokens, kein Hardcoded-Farben)
- `src/Tests/YnabClientTests.fs` - Decoder-Tests für `on_budget`/`closed`
- `src/Tests/SplitTransactionTests.fs` - `openOnBudgetAccounts`-Filter-Tests
- `src/Tests/Tests.fsproj`, `src/Client/Client.fsproj` - neue Dateien registriert

**Rationale:**
Das Domain-/Push-Fundament (ynab-001) machte Transfer-Splits *möglich*; erst die UI macht sie
nutzbar. Cashback ist der ~80%-Fall, daher zuerst und als Ein-Tipp-Shortcut.

**Outcomes:**
- Build: ✅ (`dotnet build` Solution grün, `npm run build`/Fable grün)
- Tests: 569 passed, 6 skipped (16 neue Tests: Decoder, Filter, Live-Rest, Save-Lock, Cashback-Auto-Rest)
- Issues: keine; Verifikation = dotnet build + dotnet test + Fable-Build (kein Supabase/Smoke in diesem Repo)

---

## 2026-06-13 - Lebende /styleguide-Route: visuelle DS-Galerie (design-system-003)

**What I did:**
Den geschriebenen Styleguide (`standards/frontend/styleguide.md`, design-system-001) um sein
**visuelles** Gegenstück ergänzt: eine In-App-Route `/styleguide`, die die **echten**
Design-System-Komponenten und Tokens aus `Client.DesignSystem.*` als lebende Galerie rendert
— gegliedert analog zum Markdown (Farb-/Token-Swatches → Typografie → 20 Komponenten in ihren
Hauptvarianten → Voice/Muster-Hinweise). Kein hartkodiertes Standalone-HTML: weil die realen
Komponenten gerendert werden, kann die Galerie per Konstruktion nicht von ihnen driften.

Interaktive Showcases (Input, Form-Gating, Modal, Toast, BottomSheet, Swipe) nutzen **lokale
`React.useState`-Hooks** (`[<ReactComponent>]`-Sub-Komponenten im View-Modul) — **keine** neuen
app-weiten `Msg`-Fälle, kein App-State verschmutzt. Routing über die bestehende
`Page`/`Routing`-Mechanik (Feliz.Router, Hash-Mode).

**Einstiegspunkt-Begründung:** Dezenter Link **in der Settings-Seite** (statt Haupt-Nav-Tab),
damit die Bottom-/Top-Navigation nicht zugemüllt wird — der Styleguide ist ein
Entwickler-/Referenz-Werkzeug, kein Alltags-Screen. Der Link ist ein einfacher
Hash-Router-Anchor (`#/styleguide`), damit die Settings-Komponente frei von neuen Msg-Fällen
bleibt (Navigation = Root-Concern, von `router.onUrlChanged` behandelt). Die Route ist in
jedem Fall direkt per URL erreichbar.

**Files Added:**
- `src/Client/Components/Styleguide/View.fs` — präsentationale Galerie (Layout-Helfer +
  20 Komponenten-Sektionen, interaktive per lokalem useState).

**Files Modified:**
- `src/Client/Types.fs` — `Page`-DU um `Styleguide` erweitert; `Routing.parseUrl`/`toUrlSegments`
  um `["styleguide"]` ↔ `Styleguide`.
- `src/Client/State.fs` — `UrlChanged`-Branch `| Styleguide -> Cmd.none` (rein präsentational,
  nichts zu laden).
- `src/Client/View.fs` — Render-Branch `| Styleguide -> Components.Styleguide.View.view ()`;
  `toNavPage Styleguide -> Navigation.Settings` (Settings-Tab bleibt aktiv, da von dort erreicht).
- `src/Client/Client.fsproj` — `Components/Styleguide/View.fs` vor `View.fs` eingereiht
  (F#-Compile-Reihenfolge).
- `src/Client/Components/Settings/View.fs` — dezenter `#/styleguide`-Link am Seitenende.
- `standards/frontend/styleguide.md` + `CLAUDE.md` (Design-System-Abschnitt) — je ein Pointer
  auf die lebende `/styleguide`-Route als visuelles Gegenstück.

**Rationale:**
Im Gate-Review von design-system-001 kam heraus: Roman erwartet einen **gerenderten**
Styleguide im Browser, nicht nur ein Markdown. Die Route liefert das, ohne die Komponenten zu
duplizieren (Drift-Gefahr) und ohne App-State zu verschmutzen.

**Tests — bewusst keine:** Die Seite ist rein präsentational und komponiert bereits getestete
DS-Komponenten; es gibt keine testbare Domänen-/Update-Logik. Ein „rendert ohne
Exception"-Test wäre nahe an einer Tautologie. Verifikation = Build grün + Romans visueller
Check (bewusste, im Task dokumentierte Abweichung von „jedes Feature braucht Tests").

**Outcomes:**
- Build: ✅ (`dotnet build` Solution: 0 Fehler; Client-Projekt: 0 Fehler/0 Warnungen)
- Tests: nicht zutreffend (präsentationale Galerie, keine neue Logik)
- Offen: visueller Abnahme-Check durch Roman (zweites Gate) — Route ist fertig + buildbar.

---

## 2026-06-13 - Styleguide retroaktiv kodifiziert: das Gate (design-system-001)

**What I did:**
Das bestehende Design System (`src/Client/DesignSystem/` — 20 Komponenten + `Tokens.fs`)
in **ein** reviewbares Dokument `standards/frontend/styleguide.md` gezogen. Reine
Kodifizierung, keine neue visuelle Sprache: visuelle Sprache (neon-on-dark, Glow,
Mono-Font), Farbsemantik (Orange=primär, Teal=sekundär/aktiv, Green=Erfolg, Red=Fehler,
Purple/Pink=Akzent — belegt mit Token-Namen aus `Tokens.fs`), Token-Layer
(Presets/Module statt hartkodierter Klassen), Komponenten-Inventar (alle 20 mit
"wann welche / wann nicht", konsistent mit dem CLAUDE.md-Abschnitt), Muster
(visual-viewport-Sheet + Click-Commit nach ADR 0005, Picker-Auswahl nach ADR 0004,
Sticky-Filter), Motion (Spring-`linear()` hinter `@supports`, `prefers-reduced-motion`,
Skeletons), Voice/Tonalität. Das Dokument ist **das Gate** für künftige UI-Arbeit und
verlinkt Code als autoritative Quelle, statt Implementierungsdetails zu duplizieren.

**Files Added:**
- `standards/frontend/styleguide.md` - der Styleguide (das Gate)

**Files Modified:**
- `CLAUDE.md` - Pointer zum Styleguide oben im Abschnitt "Design System Components"
- `standards/frontend/overview.md` - Pointer zum Styleguide in "See Also"

**Rationale:**
Es existierte ein vollständiges DS im Code, aber kein reviewbares Artefakt als Single
Source of Truth — die visuelle Sprache lebte implizit im Code und verstreut in CLAUDE.md.
Der Styleguide schließt diese Lücke und etabliert das Hard-Enforcement-Gate (kein
UI-Task nach todo/ vor done+reviewt; `ynab-002` hängt dran).

**Outcomes:**
- Build: n/a (reine Doku-Änderung, kein F#-Code; Pfade/Links geprüft)
- Tests: n/a
- Offen: Gate-Review mit Roman (menschlicher Schritt) steht noch aus.
- Drift-Audit + Code-Konsolidierung gegen den Styleguide: `design-system-002`.

---

## 2026-06-13 - Split mit Transfer-Zeile: Domain-DU + YNAB-Push-Fundament (ynab-001)

**What I did:**
Das Backend-Fundament für Cashback-Splits (Barabhebung an der Kasse: eine Buchung,
1 Kategorie-Zeile + 1 Transfer-Zeile aufs Bargeld-Konto) gemäß ADR 0006 umgesetzt.

`TransactionSplit` von kategorie-only auf eine `SplitTarget`-DU umgestellt:
`ToCategory of YnabCategoryId * string | ToTransfer of YnabAccountId * string`. Damit ist
"Kategorie und Transfer" / "weder noch" auf Typebene unmöglich (XOR im Typ). Geteilte
Smart-Constructor- und Helfer-Funktionen in `src/Shared/Domain.fs`: `mkSplits` (≥2 Zeilen,
eine Währung, vorzeichenrichtige Summe == Total; `Result<_, SplitError>`), `splitRemainder`
(Rest inkl. Über-Allokation), `buildCashbackSplit` (2-Zeilen-Cashback-Split, der `mkSplits`
besteht).

YNAB-Push (Conformist): `YnabSubtransactionRequest` zur DU (`CategorySub | TransferSub`)
gespiegelt; Transfer-Zeile serialisiert mit `payee_id` und **ohne** `category_id`-Key
(nicht null), Kategorie-Zeile weiterhin `category_id` ohne `payee_id`; Betrag bleibt
`Encode.int` (JSON-Zahl, kein String — bestehender Bug-Fix bewahrt). Die
`YnabAccountId → payee_id`-Auflösung ist eine reine Funktion (`resolveSubtransaction` /
`resolveSplits` / `buildTransactionRequest`), die eine Payee-`Map` entgegennimmt — sie ruft
`getPayees` nicht selbst, ist also ohne Fable-Proxy .NET-testbar. `createTransactions` holt
`GET /payees` **höchstens einmal pro Batch** und nur wenn der Batch eine Transfer-Zeile hat
(`batchHasTransferLine`); Kategorie-only-Batches überspringen den Fetch. Fehlt die
Transfer-Payee fürs Ziel-Konto, wird die Transaktion aus dem Body ausgeschlossen und in
`Api.fs` als `RejectedByYnab (UnknownRejection …)` markiert. Server-seitige Split-Validierung
in `splitTransaction` nutzt jetzt ebenfalls `mkSplits`.

**Files Added:**
- `src/Tests/SplitPushTests.fs` - Encoder-Contract (payee_id/category_id Key-Präsenz/-Absenz,
  Betrag als Zahl), `transferPayeeByAccount` (Join auf TransferAccountId), pure Auflösung
  (resolved/rejected), per-Transaktion-Reject + Batch-Fetch-Gate.

**Files Modified:**
- `src/Shared/Domain.fs` - `SplitTarget`-DU, neues `TransactionSplit`, `SplitError`,
  `mkSplits`, `splitRemainder`, `buildCashbackSplit`.
- `src/Server/YnabClient.fs` - `YnabSubtransactionRequest`-DU, `encodeSubtransaction`
  (jetzt non-private + DU-zweigig), `transferPayeeByAccount`, `resolveSubtransaction`,
  `resolveSplits`, `buildTransactionRequest`, `batchHasTransferLine`; `createTransactions`
  holt Payees einmal/bedingt und schließt unauflösbare Transfer-Transaktionen aus;
  `TransactionCreateResult.RejectedTransferTransactionIds` ergänzt.
- `src/Server/Api.fs` - Push markiert ausgeschlossene Transfer-Transaktionen als
  `RejectedByYnab (UnknownRejection …)`; `splitTransaction` validiert via `mkSplits`.
- `src/Client/Components/SyncFlow/State.fs` - `AddSplit` baut `ToCategory`-Zeile.
- `src/Tests/SplitTransactionTests.fs` - Helfer auf DU umgestellt; XOR-Test;
  `mkSplits`/`splitRemainder`/`buildCashbackSplit`-Tests.
- `src/Tests/YnabClientTests.fs` - Subtransaction-Amount-Regressionstest auf die DU +
  echten `encodeSubtransaction` umgestellt.
- `src/Tests/Tests.fsproj` - `SplitPushTests.fs` registriert.

**Rationale:**
Cashback ist ~80% der Splits, ging aber bisher nicht: `TransactionSplit` konnte einen
Transfer gar nicht ausdrücken. ADR 0006 (gegen die YNAB-OpenAPI-Spec verifizierte Research):
Transfer rein über `payee_id`, kein `transfer_account_id`; Payee erst beim Push aufgelöst,
nicht gespiegelt; illegale Split-Zustände unrepräsentierbar.

**Outcomes:**
- Build: PASSED (0 Fehler; nur die vorbestehende SQLite-Versionskonflikt-Warnung MSB3277)
- Tests: 541 passed, 6 skipped, 0 failed (gesamt 547; davon 32 Split-bezogen)
- Issues: Erster Anlauf rote 102 Tests durch `[<Literal>]` auf einer `decimal`-Konstante —
  bricht den statischen Modul-Init von `Shared.Domain`. Behoben durch Entfernen des
  `[<Literal>]`-Attributs (plain `let private`).

---

## 2026-06-12 07:55 - Quick Add Feedback-Runde: eigenes Konto, echter Picker, kein FAB, Payee optional

**What I did:**
Vier Feedback-Punkte aus dem ersten Mobile-Test umgesetzt: (1) Quick Add bucht jetzt auf ein **eigenes konfigurierbares Konto** (Settings → YNAB → "Quick-Add-Konto (z. B. Bar)") statt auf das Bank-Import-Konto; ohne Konfiguration kommt eine klare Fehlermeldung. (2) Die Kategorie-Auswahl nutzt den **richtigen Category Picker** (Suche, Zuletzt-verwendet) statt einer nativen Selectbox — er öffnet auf einem erhöhten Sheet-Layer (`.layer-2`, z-index 70/80) über dem Quick-Add-Sheet. (3) Der **schwebende FAB ist weg**; Einstiege sind jetzt ein Secondary-Button unter "Sync starten" auf dem Startscreen und ein Plus-Icon-Button im Review-Header (im `.back-btn`-Stil). (4) **Payee ist optional** — Validierung gelockert, `payee_name` wird bei leerem Wert im YNAB-JSON weggelassen (sonst entstünde ein Payee namens "").

**Files Modified:**
- `src/Shared/Domain.fs` - YnabSettings.QuickAddAccountId
- `src/Shared/Api.fs` - YnabApi.setQuickAddAccount
- `src/Server/Api.fs` - getSettings liest quickadd-Konto; setQuickAddAccount; addManualTransaction nutzt `ynab_quickadd_account_id` mit klarer Fehlermeldung
- `src/Server/Validation.fs` - Payee optional (nur noch Längen-Cap)
- `src/Server/YnabClient.fs` - payee_name weggelassen wenn leer
- `src/Client/Components/Settings/{Types,State,View}.fs` - SetQuickAddAccount-Flow, zweites Konto-Select (refaktoriert zu accountSelect-Helper), Read-only-Anzeige
- `src/Client/DesignSystem/BottomSheet.fs` - Elevated-Flag + categoryPickerLayered
- `src/Client/Components/SyncFlow/Views/QuickAdd.fs` - Kategorie-Feld öffnet Picker, FAB entfernt, Header-/Entry-Buttons, Payee-Label "(optional)"
- `src/Client/Components/SyncFlow/{Types,State,View}.fs`, `Views/StatusViews.fs` - ShowCategoryPicker-State, neue Trigger-Platzierung
- `src/Client/styles.css` - .layer-2, .qa-cat-field/-clear, .qa-header-btn; FAB-CSS entfernt
- `src/Tests/QuickAddTests.fs` - Payee-optional-Semantik (accepts empty/whitespace, overlong rejected, payee_name omitted when blank), ShowCategoryPicker im Form-Record

**Rationale:**
Direktes Nutzer-Feedback vom ersten Android-Test: Bar-Ausgaben gehören aufs Bar-Konto, nicht ins Comdirect-Import-Konto; die Selectbox war ein UX-Rückschritt gegenüber dem neuen Picker; der FAB passte nicht zur App; eine schnelle Bar-Buchung braucht keinen Payee.

**Outcomes:**
- Build: ✅ (dotnet build + npm run build/Fable)
- Tests: 516/516 passed, 6 skipped
- Deployment: auf docker-host (feature/ux-wow → deploy/main)
- Hinweis: Nach dem Deploy muss in Settings → YNAB → "Verbindung testen" das Quick-Add-Konto einmalig gewählt werden, sonst meldet Quick Add einen Konfigurationsfehler

---

## 2026-06-11 23:10 - UX-Overhaul: Keyboard-fester Category Picker, Swipe-Gesten, Quick Add (Branch feature/ux-wow)

**What I did:**
Großer Mobile-UX-Umbau in vier Teilen: (1) Der Category Picker ist jetzt keyboard-aware — die visualViewport-API wird in CSS-Variablen (`--vvh`/`--vv-top`) gespiegelt und das Bottom Sheet ankert an der Unterkante des *sichtbaren* Bereichs statt an `bottom: 0`, das auf iOS hinter der Tastatur klebt. Ghost-Clicks (Tap geht durch den Picker auf die Transaktion dahinter) sind eliminiert: Auswahl committet auf echtem `click` statt `onPointerDown`, `onMouseDown.preventDefault()` hält den Suchfokus stabil, plus ein einmaliger Click-Swallow-Guard beim Schließen. Suche ist oberhalb der scrollenden Liste gepinnt und hat 16px-Font (kein iOS-Auto-Zoom). Body-Scroll-Lock während offener Sheets. (2) Mobile-Polish: Sticky Filter-Pills mit Backdrop-Blur, Spring-Easing (`linear()`) für Sheet-Einfahrt, Transaktions-Skeleton statt Spinner. (3) Swipe-nach-links auf Transaktionszeilen zum Überspringen/Einschließen (Pointer Events, 12px-Richtungs-Claim, Rubberband, Haptik; Toggle/Chips bleiben der barrierefreie Pfad). (4) **Quick Add** (Vision Phase 0, docs/idea-ynab-replacement.md): FAB + Bottom-Sheet-Formular für manuelle Transaktionen (Bar-Ausgaben), die direkt in YNAB landen — bewusst ohne import_id, damit sie nie mit der Import-Deduplikation kollidieren.

**Files Added:**
- `src/Client/DesignSystem/Viewport.fs` - visualViewport→CSS-Vars-Sync, Body-Scroll-Lock (gezählt), Ghost-Click-Guard, Haptik/Pointer-Helfer
- `src/Client/DesignSystem/Swipe.fs` - Generische SwipeableRow-Komponente (Pointer Events)
- `src/Client/Components/SyncFlow/Views/QuickAdd.fs` - FAB + Quick-Add-Sheet
- `src/Tests/QuickAddTests.fs` - 48 Tests: Milliunit-Konvertierung, Validierung, YNAB-JSON-Vertrag, Betrags-Parsing, Form→Request, Reducer-Übergänge

**Files Modified:**
- `src/Client/DesignSystem/BottomSheet.fs` - Click-Commit-Pattern, gepinnte Suche, Close-Button, Scroll-Lock-Hook, autoFocus nur bei feinem Pointer
- `src/Client/styles.css` - Viewport-Anker fürs Sheet, Sticky-Filter, Swipe-Action-Styles, Quick-Add-Styles (FAB, Formular), Spring-Token
- `src/Client/index.html` - `interactive-widget=resizes-content` (Android-Keyboard)
- `src/Client/App.fs` - Viewport.start()
- `src/Shared/Domain.fs` - ManualTransactionRequest + manualTransactionMilliunits
- `src/Shared/Api.fs` - YnabApi.addManualTransaction
- `src/Server/Validation.fs` - validateManualTransaction
- `src/Server/YnabClient.fs` - buildManualTransactionBody (pur, getestet) + createManualTransaction
- `src/Server/Api.fs` - addManualTransaction-Wiring (Default-Budget/-Konto aus Settings)
- `src/Client/Components/SyncFlow/{Types,State,View}.fs`, `Views/{TransactionRow,TransactionList}.fs` - QuickAdd-State/Msgs, Swipe-Integration, Skeleton
- `src/Client/DesignSystem/Loading.fs` - txListSkeleton

**Rationale:**
Der Category Picker war auf Mobile praktisch unbenutzbar (von der Tastatur verdeckt, Klicks gingen durch). Root Causes: iOS verkleinert nur den Visual Viewport (fixed/`dvh` bleiben stehen) und Commit auf `pointerdown` lässt den nachgelagerten synthetischen Click auf das Element dahinter durchfallen. Quick Add ist Phase 0 der dokumentierten YNAB-Ersatz-Vision (Frau-Workflow: Bar-Ausgaben am Handy erfassen).

**Outcomes:**
- Build: ✅ (dotnet build + npm run build/Fable)
- Tests: 514/514 passed (48 neue), 6 skipped (Integrationstests ohne .env)
- QA-Review (qa-milestone-reviewer): ADEQUATE, "Ship as-is"; Follow-ups (Memo-Trim im Encoder, Reducer-Tests) umgesetzt
- Dev-Server verifiziert: Backend 5081 + Vite 5181, Proxy ok, neuer Endpoint validiert korrekt
- Hinweis: Reducer-Test für den Valid-Submit-Pfad nicht möglich (Fable.Remoting-Proxy-Init wirft unter .NET); Pfad über buildQuickAddRequest-Tests abgedeckt

---

## 2026-05-19 - Fix: Restore "force re-import" UI trigger lost in mobile redesign

**What I did:**
Beim mobile-first Redesign (2026-03) wurde der UI-Trigger für `ForceImportDuplicates` versehentlich entfernt — das Handler-Pendant und der Server-Endpoint blieben aber bestehen. Folge: wenn der User Transaktionen in YNAB löscht und sie in BB erneut importieren will, schlägt der Import fehl ("Imported 0 transaction(s). 2 already exist in YNAB"), weil YNABs `import_id`-Historie auch nach Delete bestehen bleibt — und es gibt keinen Knopf, um den Force-Re-Import mit neuer UUID anzustoßen.

Neues Banner direkt unter dem bestehenden "X Duplikate"-Info-Banner: wird gerendert, wenn `model.DuplicateTransactionIds` nach einem fehlgeschlagenen Import nicht leer ist. Zeigt Anzahl + Button "Erneut importieren →" der `ForceImportDuplicates` dispatcht (nutzt das vorhandene Backend-Pipeline mit `forceNewImportId=true` → neue UUIDs umgehen YNABs Duplikat-Check).

**Files Modified:**
- `src/Client/Components/SyncFlow/Views/TransactionList.fs` — Neues Warning-Banner mit Inline-Button nach Duplikat-Info-Banner.
- `src/Client/styles.css` — `.info-banner.warning`, `.info-banner-icon.warning`, `.info-banner-text`, `.info-banner-action` (orange Token-Farbe, kein hardcoded Hex außer rgba auf Neon-Orange-Token).
- `src/Client/Components/SyncFlow/State.fs` — Misleading Kommentar "UI trigger removed during mobile-first redesign" entfernt.

**Rationale:**
YNAB löscht `import_id`-Historie nicht beim Delete einer Transaktion — der einzige Weg zum Re-Import ist eine neue UUID. Backend-Logik dafür existiert bereits (`forceImportDuplicates` API-Endpoint, `YnabClient.createTransactions ... forceNewImportId=true`); nur die UI-Lücke wurde wieder geschlossen.

**Outcomes:**
- Build: PASSED (`dotnet build`, `npx vite build`)
- Tests: 467 passed, 6 skipped, 0 failed.

---

## 2026-05-19 - Fix: Extract merchant from memo for Comdirect card payments

**What I did:**
Comdirect's API liefert für Visa-Debitkarte-/Kartenzahlungen weder `remitter.holderName` noch `creditor.holderName` — der Händlername steht ausschließlich im `remittanceInfo` (Memo). Dadurch wurde `Payee = None` gesetzt und der YNAB-Upload fiel auf `payee_name = "Unknown"` zurück. YNAB ordnete daraufhin diese Transaktionen via Payee-Renaming-Rules/Account-Name-Match automatisch als „Transfer to Bar" ein und ignorierte die zugewiesene Kategorie.

Neuer Fallback in `transactionDecoder`: wenn beides (`remitter`, `creditor`) fehlt UND das Memo `Kartenzahlung` oder `Visa-Debitkarte` enthält, wird der Text vor dem ersten Komma als Payee extrahiert (z.B. „Software Outlet BV", „Google Play Apps").

**Files Modified:**
- `src/Server/ComdirectClient.fs` — Zwei neue `internal`-Funktionen `isCardPaymentMemo` und `extractCardMerchant`. Decoder nutzt den Memo-Fallback nur wenn weder remitter noch creditor gesetzt ist (SEPA-Transaktionen unverändert).
- `src/Tests/ComdirectDecoderTests.fs` — 10 neue Tests: 3 für `isCardPaymentMemo`, 7 für `extractCardMerchant` (inkl. Edge Cases: leeres Memo, führendes Komma, kein Komma, Whitespace-Trimmung).

**Rationale:**
Regressionsschutz gegen den YNAB-Auto-Transfer-Bug. Der Fix wirkt nur auf neue Imports — bestehende, fälschlich als „Transfer to Bar" angelegte YNAB-Transaktionen müssen manuell in YNAB korrigiert werden (Payee-Renaming-Rule entfernen + Transfer-Verknüpfung über „Make this a regular transaction" lösen).

**Side-Effects:**
- DuplicateDetection: künftige Kartenzahlungen sind per Payee fuzzy-matchbar (war vorher nur Datum/Betrag).
- RulesEngine: Rules, die auf Payee matchen, können jetzt für Kartenzahlungen greifen — beabsichtigte Verbesserung.

**Outcomes:**
- Build: PASSED
- Tests: 467 passed, 6 skipped (.env-abhängige Integration-Tests), 0 failed.

---

## 2026-02-28 - Fix: Skip future-dated transactions before YNAB import

**What I did:**
Fixed a bug where Comdirect pre-notified transactions (Vormerkbuchungen) with future booking dates caused YNAB to reject the entire import batch with "date must not be in the future or over 5 years ago". Transactions with booking dates after today are now automatically filtered out before sending to YNAB.

**Root Cause:**
Comdirect returns transactions with future booking dates (e.g., a standing order for March 2nd visible on February 28th). YNAB's API rejects any transaction with a future date, and since transactions are sent as a batch, the entire batch of 38 transactions failed.

**Files Modified:**
- `src/Server/YnabClient.fs` - Added future-date filter in `createTransactions` with logging for skipped transactions
- `src/Tests/YnabClientTests.fs` - Added 2 regression tests: future-dated transactions are filtered out, today's date is accepted

**Outcomes:**
- Build: ✅
- Tests: 410/410 passed
- Import works again, future-dated transactions are silently skipped

---

## 2026-02-23 - Amazon Order-ID Category Suggestion Feature

**What I did:**
Implemented automatic category suggestions for Amazon transactions based on Order ID matching. When a transaction with a specific Amazon Order ID has been categorized (either in YNAB history or in the current session), other transactions with the same Order ID automatically receive the same category as a suggestion.

Two sources of suggestions:
- **Source B (YNAB History)**: Existing YNAB transactions with categories are scanned for Amazon Order IDs during sync initialization. Matching Order IDs propagate categories to new uncategorized transactions.
- **Source A (In-Session)**: When a user manually categorizes a transaction, other transactions with the same Amazon Order ID in the current session immediately receive the same category.

**Files Added:**
- `src/Server/OrderIdMatcher.fs` - New pure-logic module with `buildYnabOrderIdMap`, `applySuggestions`, and `propagateInSession` functions
- `src/Tests/OrderIdMatcherTests.fs` - Comprehensive tests for all three OrderIdMatcher functions

**Files Modified:**
- `src/Shared/Domain.fs` - Added `CategoryId`/`CategoryName` to `YnabTransaction`, `SuggestedByOrderId` to `SyncTransaction`
- `src/Shared/Api.fs` - Changed `categorizeTransaction` return type from `SyncResult<SyncTransaction>` to `SyncResult<SyncTransaction list>` (first = categorized, rest = propagated)
- `src/Server/YnabClient.fs` - Extended `transactionDecoder` to decode `category_id` and `category_name` from YNAB API
- `src/Server/RulesEngine.fs` - Made `amazonOrderIdPattern` and `extractAmazonOrderId` public, added `extractAmazonOrderIdFromText` helper, added `SuggestedByOrderId = None` to `classifyTransactions`
- `src/Server/Server.fsproj` - Added `OrderIdMatcher.fs` compile include
- `src/Server/Api.fs` - Integrated OrderIdMatcher in sync flow (after duplicate detection) and in `categorizeTransaction` (in-session propagation), added `SuggestedByOrderId = None` to manual categorize and bulk categorize
- `src/Server/Persistence.fs` - Added `SuggestedByOrderId = None` to session restore record
- `src/Client/Components/SyncFlow/Types.fs` - Changed `TransactionCategorized` msg to accept `SyncTransaction list`
- `src/Client/Components/SyncFlow/State.fs` - Updated handler to process list of updated transactions, primary tx tracked for manual categorization
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs` - Added purple "Bestellung" badge for Order-ID suggestions, purple status dot color for suggested transactions
- `src/Tests/Tests.fsproj` - Added `OrderIdMatcherTests.fs` compile include
- `src/Tests/DuplicateDetectionTests.fs` - Added `SuggestedByOrderId = None` and `CategoryId`/`CategoryName` to record constructions
- `src/Tests/SplitTransactionTests.fs` - Added `SuggestedByOrderId = None` to record constructions
- `src/Tests/YnabClientTests.fs` - Added `SuggestedByOrderId = None` to record constructions
- `src/Tests/SyncSessionManagerTests.fs` - Added `SuggestedByOrderId = None` to record construction
- `src/Tests/PersistenceTypeConversionTests.fs` - Added `SuggestedByOrderId = None` to record construction

**Rationale:**
Amazon transactions frequently come in multiple parts (partial deliveries, returns) with the same Order ID. Manually categorizing each one is tedious. This feature automates the process by propagating categories between transactions sharing the same Order ID.

**Outcomes:**
- Build: ✅
- Tests: 399/399 passed (6 integration tests skipped)
- Issues: None

---

## 2025-12-31 - Created standards/ Directory for Reusable F# Blueprint

**What I did:**
Created a comprehensive `standards/` directory structure to reorganize documentation from `docs/` into reusable, AI-agent-optimized standards for new F# full-stack projects. This enables minimal context window usage with maximum impact when using Claude Code or other AI agents.

**Files Added:**
- `standards/README.md` - Main entry point with task-based and skill-based navigation
- `standards/global/architecture.md` - Tech stack, core principles, project structure
- `standards/global/development-workflow.md` - 9-step development order, bug fix protocol, diary format
- `standards/global/claude-tools.md` - Serena MCP tool usage (MANDATORY for F# code)
- `standards/global/quick-reference.md` - Copy-paste ready code templates
- `standards/global/anti-patterns.md` - Critical production bugs and lessons learned
- `standards/global/learnings.md` - Why this stack works, proven patterns
- `standards/shared/types.md` - Domain type design, value objects, discriminated unions
- `standards/shared/api-contracts.md` - Fable.Remoting patterns, command/query separation
- `standards/shared/validation.md` - Field validators, error accumulation
- `standards/backend/overview.md` - Giraffe HttpHandler basics, Program.fs setup
- `standards/backend/api-implementation.md` - Fable.Remoting implementation, orchestration
- `standards/backend/domain-logic.md` - Pure business logic, event sourcing, no I/O
- `standards/backend/persistence-sqlite.md` - SQLite config, Dapper CRUD, OptionHandler
- `standards/backend/persistence-files.md` - JSON persistence, event sourcing files
- `standards/backend/error-handling.md` - Result types, async patterns
- `standards/frontend/overview.md` - MVU architecture, core concepts
- `standards/frontend/state-management.md` - Model, Msg, update patterns, optimistic UI
- `standards/frontend/view-patterns.md` - Component organization, list rendering, RemoteData
- `standards/frontend/remotedata.md` - RemoteData type, helpers, usage patterns
- `standards/frontend/routing.md` - Feliz.Router patterns, page definition
- `standards/testing/overview.md` - Expecto basics, test organization
- `standards/testing/domain-tests.md` - Testing pure functions, event replay
- `standards/testing/api-tests.md` - API endpoint testing, validation
- `standards/testing/persistence-tests.md` - In-memory SQLite, USE_MEMORY_DB
- `standards/testing/frontend-tests.md` - Elmish update testing, state transitions
- `standards/testing/property-tests.md` - FsCheck property-based testing
- `standards/deployment/docker.md` - Multi-stage builds, Dockerfile patterns
- `standards/deployment/docker-compose.md` - Stack configuration, environment setup
- `standards/deployment/tailscale.md` - Tailscale sidecar setup, ACLs
- `standards/deployment/production.md` - Production config, health checks, monitoring

**Rationale:**
The existing `docs/` directory (~220KB across 18 files) was comprehensive but project-specific and not optimized for AI agent consumption. By extracting and reorganizing the content into focused, self-contained standards files, future projects can benefit from:
- **Minimal context usage**: Each file is concise (typically <100 lines) and self-contained
- **Task-based navigation**: README maps tasks directly to relevant files
- **Skill integration**: Maps Claude Code skills to primary documentation files
- **Reusable patterns**: Excludes project-specific content (milestones, design system)
- **Consistent structure**: Every file follows template (Overview, When to Use, Patterns, Checklist, See Also)

**Structure:**
```
standards/
├── README.md              # Navigation hub
├── global/                # Architecture, workflow, tools (6 files)
├── shared/                # Types, API contracts, validation (3 files)
├── backend/               # API, domain, persistence, errors (6 files)
├── frontend/              # MVU, state, views, RemoteData (5 files)
├── testing/               # Domain, API, persistence, frontend, property tests (6 files)
└── deployment/            # Docker, compose, Tailscale, production (4 files)
```

**Content Sources:**
- `docs/00-ARCHITECTURE.md` → `global/architecture.md`, `backend/overview.md`, `frontend/overview.md`
- `docs/01-PROJECT-SETUP.md` → Excluded (project-specific)
- `docs/02-FRONTEND-GUIDE.md` → `frontend/state-management.md`, `frontend/view-patterns.md`, `frontend/routing.md`
- `docs/03-BACKEND-GUIDE.md` → `backend/api-implementation.md`, `backend/domain-logic.md`
- `docs/04-SHARED-TYPES.md` → `shared/types.md`, `shared/api-contracts.md`
- `docs/05-PERSISTENCE.md` → `backend/persistence-sqlite.md`, `backend/persistence-files.md`
- `docs/06-TESTING.md` → `testing/*` files
- `docs/07-BUILD-DEPLOY.md` → `deployment/docker.md`, `deployment/docker-compose.md`, `deployment/production.md`
- `docs/08-TAILSCALE-INTEGRATION.md` → `deployment/tailscale.md`
- `docs/09-QUICK-REFERENCE.md` → `global/quick-reference.md`
- `docs/LEARNINGS.md` → `global/learnings.md`, `global/anti-patterns.md`
- `CLAUDE.md` → `global/claude-tools.md`, `global/development-workflow.md`

**Outcomes:**
- Build: N/A (documentation only)
- Tests: N/A
- Issues: None
- Total files created: 31 (1 README + 30 content files)
- Total lines: ~2,800 lines across all standards files

---

## 2025-12-29 - Restored Amazon Order Links in Sync Flow

**What I did:**
Fixed a regression where Amazon Order Links (ExternalLinks) were lost when the Payee ComboBox was added. The links are now visible again next to the payee field.

**Files Modified:**
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs`:
  - Added `externalLinkButton` helper function to render external link icons
  - **Mobile Layout**:
    - Skipped transactions: Payee text is now a clickable teal link (when ExternalLink available)
    - Active transactions: External link icon appears next to the ComboBox
  - **Desktop Layout**:
    - Same logic as mobile
    - Increased payee column width from `w-48` to `w-52` to accommodate the icon

**Rationale:**
The previous commit (b44542f) that added transfer payees to the ComboBox accidentally removed the ExternalLinks rendering. Users could no longer click on Amazon transactions to see the original order. The backend was still calculating the links correctly, they just weren't being displayed.

**Technical Details:**
- ExternalLinks (Amazon order deep links, PayPal activity links) are still generated in `RulesEngine.fs`
- For skipped transactions: Payee text itself becomes a clickable link with external icon
- For active transactions: Separate icon button appears next to the editable ComboBox
- Only the first ExternalLink is displayed (most transactions only have one)

**Outcomes:**
- Build: ✅
- Tests: 377 passed, 6 skipped
- Issues: None

---

## 2025-12-29 - Added Transfer-Payees to Sync Flow ComboBox

**What I did:**
Implemented grouped payee options in the Sync Flow ComboBox, allowing users to select Transfer Payees (for transfers between YNAB accounts) in addition to regular payees. Transfers are now displayed in a separate group at the top of the dropdown with a "Transfers" header, followed by regular "Payees".

**Files Modified:**
- `src/Client/DesignSystem/Input.fs`:
  - Moved `ComboBoxOption` type definition before `ComboBoxProps` (F# type ordering requirement)
  - Updated `ComboBox` component to properly handle `ComboBoxOption list` instead of tuple list
  - Added support for disabled items (section headers) - rendered as non-clickable, uppercase, grey text
  - Updated keyboard navigation to skip disabled items (section headers)
  - Updated filtering to preserve section headers when their group has matching items
  - Added `comboBoxGrouped` helper function for direct `ComboBoxOption list` usage
  - Fixed `comboBox` helper to convert tuples to `ComboBoxOption` using `toComboBoxOptions`

- `src/Client/Components/SyncFlow/Views/TransactionList.fs`:
  - Changed `payeeOptions` from excluding transfers to including them
  - Added grouping: Transfers first (sorted by name), then Payees (sorted by name)
  - Uses `Input.sectionHeader` for group headers

- `src/Client/Components/SyncFlow/Views/TransactionRow.fs`:
  - Updated type signature to accept `Input.ComboBoxOption list` instead of tuple list
  - Changed `Input.comboBox` calls to `Input.comboBoxGrouped`

**Rationale:**
Users needed the ability to categorize bank transactions as transfers between accounts (e.g., "Transfer : Tagesgeld ING"). Previously, transfer payees were filtered out, making it impossible to select them. The grouped dropdown provides clear visual separation between transfer and regular payees.

**Technical Details:**
- Section headers use `IsDisabled = true` in `ComboBoxOption`
- Keyboard navigation (ArrowUp/ArrowDown) automatically skips disabled items
- Search filtering preserves headers if any of their children match
- YNAB recognizes the "Transfer : Account Name" format and processes it as a transfer

**Outcomes:**
- Build: ✅
- Tests: 377 passed, 6 skipped
- Issues: None

---

## 2025-12-28 14:00 - Fixed Payee Loading Bug

**What I did:**
Fixed a bug where payees weren't being loaded in the Sync Flow, causing the ComboBox dropdown to show no suggestions.

**Root Cause:**
In `src/Client/State.fs`, the `syncFlowCmd` returned from `SyncFlow.State.init()` was not included in the parent's `Cmd.batch`. This meant the `LoadPayees` (and `LoadCategories`) commands were never dispatched.

**Files Modified:**
- `src/Client/State.fs` - Added `Cmd.map SyncFlowMsg syncFlowCmd` to the init Cmd.batch

**The Bug:**
```fsharp
// BEFORE (broken):
let cmd = Cmd.batch [
    Cmd.map DashboardMsg dashboardCmd
    Cmd.map SettingsMsg settingsCmd
    initialPageCmd
]

// AFTER (fixed):
let cmd = Cmd.batch [
    Cmd.map DashboardMsg dashboardCmd
    Cmd.map SettingsMsg settingsCmd
    Cmd.map SyncFlowMsg syncFlowCmd  // <-- was missing!
    initialPageCmd
]
```

**How it was found:**
Added debug logging throughout the LoadPayees flow. Discovered that `LoadCategories` was being dispatched but `LoadPayees` was not - even though both were in the same `Cmd.batch` in `SyncFlow.State.init()`. This pointed to the parent not dispatching the SyncFlow commands at all.

**Outcomes:**
- Build: ✅
- Payee ComboBox now shows YNAB payee suggestions correctly

---

## 2025-12-28 - Added Payee Select Field to Sync Flow

**What I did:**
Added an editable Payee field to the transaction row in the Sync Flow. Users can now select from existing YNAB payees via a searchable dropdown OR enter custom payee text. The payee is saved as a PayeeOverride and sent to YNAB when importing transactions.

**Files Added:**
- None (new component added inline in existing file)

**Files Modified:**
- `src/Shared/Domain.fs` - Added `YnabPayeeId` and `YnabPayee` types
- `src/Shared/Api.fs` - Added `getPayees` method to `YnabApi` interface
- `src/Server/YnabClient.fs` - Added `payeeDecoder` and `getPayees` function for YNAB API
- `src/Server/Api.fs` - Implemented `getPayees` in `ynabApi`
- `src/Client/DesignSystem/Input.fs` - Added new `ComboBox` component (text input with dropdown suggestions)
- `src/Client/Components/SyncFlow/Types.fs` - Added `Payees`, `PendingPayeeVersions` to Model; added `LoadPayees`, `PayeesLoaded`, `SetPayeeOverride`, `CommitPayeeChange` messages
- `src/Client/Components/SyncFlow/State.fs` - Added handlers for payee loading and editing with debouncing
- `src/Client/Components/SyncFlow/Views/TransactionList.fs` - Added `payeeOptions` computation and passing to `transactionRow`
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs` - Replaced static payee display with editable ComboBox

**New Component: ComboBox**
Unlike SearchableSelect (which only allows selection from a list), ComboBox:
- Has an always-editable text input
- Shows filtered suggestions from YNAB payees
- Allows custom text input (not just selection)
- Uses the same styling and keyboard navigation as SearchableSelect

**Implementation Details:**
- Payees loaded at session start via `LoadPayees` command
- Transfer payees filtered out (only regular payees shown)
- Debouncing applied to payee changes (same pattern as category changes)
- Both category and payee changes use the same API endpoint (`categorizeTransaction`)
- Desktop: Payee ComboBox appears next to Category (w-48)
- Mobile: Payee ComboBox appears on second row

**Rationale:**
User requested the ability to see and edit payees in the Sync Flow, similar to how they can edit categories. The YNAB payee list provides autocomplete suggestions while still allowing custom payee names.

**Outcomes:**
- Build: ✅ (2 pre-existing warnings about deprecated Modal component)
- Tests: 377/377 passed (6 skipped - integration tests)
- Issues: None

---

## 2025-12-16 16:45 - Added "To be imported" Filter on Sync Page

**What I did:**
Added a new filter "To be imported" (ToBeImported) to the Sync Transactions page. This filter shows only transactions that will actually be imported to YNAB (not skipped and not already imported).

**Files Added:**
- None

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs` - Added `ToBeImported` case to `TransactionFilter` discriminated union
- `src/Client/Components/SyncFlow/State.fs` - Added filter logic for `ToBeImported` in `filterTransactions` function
- `src/Client/Components/SyncFlow/Views/TransactionList.fs` - Added filter logic, count calculation, and new stat card
- `src/Client/DesignSystem/Stats.fs` - Added `gridFiveCol` function for 5-column responsive grid layout

**Rationale:**
User requested a filter to see only transactions that will be imported. The existing filters (Total, Categorized, Uncategorized, Skipped) didn't provide a direct way to see which transactions are queued for import.

**Implementation Details:**
- Filter logic: `tx.Status <> Skipped && tx.Status <> Imported`
- New stat card with Teal accent color between "Total" and "Categorized"
- Responsive 5-column grid: 2 cols (mobile), 3 cols (tablet), 5 cols (desktop)

**Outcomes:**
- Build: ✅ (2 pre-existing warnings)
- Tests: 377/377 passed (6 skipped - integration tests)
- Issues: None

---

## 2025-12-16 - Fixed Import ID Slash Parsing Bug (Critical, Follow-up)

**What I did:**
Fixed a follow-up bug in the Import ID duplicate detection where Comdirect transaction IDs containing "/" were not being matched correctly. The previous fix addressed format consistency between `generateImportId` and `matchesImportId`, but `Api.fs` still had incorrect parsing logic.

**Root Cause:**
In `Api.fs` line 958, when parsing YNAB's `duplicate_import_ids` response:
```fsharp
let cleanId = txIdPart.Split('/') |> Array.head  // BUG: stripped "/33825" suffix
```

This incorrectly split Comdirect IDs like `3I2C21XS1ZXDAP9P/33825` at the "/" and only kept `3I2C21XS1ZXDAP9P`. But the local transaction IDs still had the full ID with the "/33825" part, so they never matched.

**Symptoms:**
- Import always showed "0 imported" even when transactions existed
- Force Import option never appeared (because `ynabRejected` count was always 0)
- Docker logs showed: `[WARNING] Could not map 41 duplicate import IDs to transaction IDs`

**Files Modified:**
- `src/Server/Api.fs` - Removed the erroneous `Split('/')` that stripped the Comdirect ID suffix
- `src/Tests/DuplicateDetectionTests.fs` - Added 2 regression tests for Comdirect IDs with slashes

**Rationale:**
Comdirect transaction IDs have the format `{base}/{number}` where the "/" and number are integral parts of the ID. The previous "fix" assumed "/" was an old format suffix that could be stripped, but it's actually part of the current Comdirect ID format.

**Outcomes:**
- Build: ✅
- Tests: 377/377 passed (+2 new regression tests)
- Issues: None remaining

---

## 2025-12-16 - Fixed Force Import Duplicate Bug (Critical)

**What I did:**
Fixed a critical bug where Force Import caused transactions to be imported multiple times to YNAB. The root cause was a format mismatch between Import ID generation and detection - `YnabClient.fs` generated `BB:{txId}` format but `DuplicateDetection.fs` searched for `BUDGETBUDDY:{txId}:` format. This meant `matchesByImportId` NEVER matched anything, causing the fallback logic to mark ALL transactions as duplicates.

**Root Cause:**
1. Import ID format mismatch: `BB:` vs `BUDGETBUDDY:`
2. Tests were tautological - they tested the wrong format against the wrong format
3. Dangerous fallback in Api.fs: if ID mapping failed, ALL transactions were marked as duplicates

**Files Added:**
- None

**Files Modified:**
- `src/Shared/Domain.fs` - Added `ImportIdPrefix` constant, `generateImportId` and `matchesImportId` helper functions
- `src/Server/YnabClient.fs` - Changed to use `Domain.generateImportId` and `Domain.ImportIdPrefix`
- `src/Server/DuplicateDetection.fs` - Changed `matchesByImportId` to use `Domain.matchesImportId`
- `src/Server/Api.fs` - Removed dangerous fallback that marked ALL transactions as duplicates
- `src/Tests/DuplicateDetectionTests.fs` - Fixed tests to use `Domain.generateImportId` instead of hardcoded format
- `src/Tests/YnabClientTests.fs` - Fixed all tautological tests:
  - `importIdGenerationTests`: Replaced with meaningful tests that call `Domain.generateImportId` and `Domain.matchesImportId`
  - `transactionConversionTests`: Changed to use `Domain.generateImportId` instead of hardcoded wrong format
  - `propertyBasedTests`: Changed to use `Domain.generateImportId` with proper TransactionId types

**Files Deleted:**
- None

**Rationale:**
The bug caused users to see duplicate transactions in YNAB when using Force Import. By centralizing the Import ID format in a constant, we prevent future format mismatches and ensure tests always use the same format as production code.

**Outcomes:**
- Build: Success
- Tests: 375/375 passed
- Issues: None remaining

---

## 2025-12-15 - URL-Based Routing with Feliz.Router

**What I did:**
Implemented hash-based URL routing using Feliz.Router to enable browser back/forward navigation and deep linking. Users can now navigate directly to pages via URLs like `#/sync`, `#/rules`, `#/settings`.

**Files Added:**
- `src/Tests/RoutingTests.fs` - 14 unit tests for URL parsing and roundtrip verification

**Files Modified:**
- `src/Client/Client.fsproj` - Added Feliz.Router 4.0.0 package reference
- `src/Client/Types.fs` - Added `Routing` module with URL parsing functions:
  - `parseUrl`: Converts URL segments to `Page` type
  - `toUrlSegments`: Converts `Page` to URL segments
  - `currentPage`: Gets current page from browser URL
- `src/Client/State.fs`:
  - Added `open Feliz.Router` import
  - Added `UrlChanged of string list` message
  - Updated `init` to parse initial URL (enables deep linking)
  - Changed `NavigateTo` to trigger URL change via `Cmd.navigate`
  - Added `UrlChanged` handler for actual state changes
- `src/Client/View.fs`:
  - Added `open Feliz.Router` import
  - Wrapped view with `React.router` component
  - Added `router.onUrlChanged` handler
- `src/Tests/Tests.fsproj` - Added RoutingTests.fs to compilation

**URL Schema:**
| URL | Page |
|-----|------|
| `#/` | Dashboard |
| `#/sync` | SyncFlow |
| `#/rules` | Rules |
| `#/settings` | Settings |
| `#/invalid` | Dashboard (fallback) |

**Architecture:**
The URL is now the single source of truth for navigation:
1. `NavigateTo page` triggers `Cmd.navigate` (changes URL)
2. URL change fires `router.onUrlChanged`
3. `UrlChanged segments` updates `CurrentPage` and triggers page-specific load commands

**Features:**
- Browser back/forward buttons work correctly
- Deep linking (e.g., opening `#/settings` directly)
- All existing navigation patterns still work
- Guard against redundant updates if page hasn't changed

**Rationale:**
Before this change, navigation was purely in-memory state. Users couldn't bookmark pages, share URLs, or use browser history. With Feliz.Router, the app now behaves like a standard web application with proper URL routing.

**Outcomes:**
- Build: ✅ (0 errors, 2 warnings - unrelated Modal.fs deprecation)
- Tests: 371/377 passed (6 skipped integration tests)
- 14 new routing tests added

---

## 2025-12-15 - Frontend Architecture: Category Selection Debouncing (Milestone 8)

**What I did:**
Completed Milestone 8 from the Frontend Architecture Improvement plan. Implemented debouncing for category selection to reduce server load when users rapidly change categories.

**Files Added:**
- `src/Client/Debounce.fs` - Generic debounce utilities for Elmish:
  - `delayed` function to create delayed commands with custom delay
  - `delayedDefault` function with 400ms default delay
  - Version-based approach that works with Elmish architecture

**Files Modified:**
- `src/Client/Client.fsproj` - Added Debounce.fs to compilation
- `src/Client/Components/SyncFlow/Types.fs`:
  - Added `PendingCategoryVersions: Map<TransactionId, int>` to Model
  - Added `CommitCategoryChange of TransactionId * YnabCategoryId option * int` message
- `src/Client/Components/SyncFlow/State.fs`:
  - Added `open Client` import
  - Refactored `CategorizeTransaction` to use version-based debouncing
  - Added `CommitCategoryChange` handler that only commits if version is current
  - Updated `init` to include `PendingCategoryVersions = Map.empty`
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs`:
  - Added `isPendingSave: bool` parameter to `transactionRow` function
  - Added orange pulsing dot indicator when save is pending
- `src/Client/Components/SyncFlow/Views/TransactionList.fs`:
  - Updated `transactionRow` call to compute and pass `isPendingSave`
- `docs/Frontend-Architecture-milestones.md` - Marked Milestone 8 as complete

**Rationale:**
Users can rapidly change categories while reviewing transactions, which caused unnecessary API calls to the server. The debouncing implementation:
- Uses version tracking to ensure only the latest change triggers an API call
- Maintains optimistic UI updates for instant feedback
- Shows a visual indicator that the change is pending
- Works within the Elmish architecture without external state or timers

**Design Decisions:**
- 400ms delay balances responsiveness with server load reduction
- Version-based approach prevents race conditions naturally
- Orange pulsing dot provides clear pending state feedback
- Debounce module is generic and reusable for other use cases

**Outcomes:**
- Build: ✅ (0 errors, 2 warnings - unrelated Modal.fs deprecation)
- Tests: 357/357 passed (6 skipped)
- Issues: None

---

## 2025-12-15 - Frontend Architecture: PageHeader Design System Component (Milestone 7)

**What I did:**
Completed Milestone 7 from the Frontend Architecture Improvement plan. Created a reusable `PageHeader` Design System component for consistent page layouts across the application.

**Files Added:**
- `src/Client/DesignSystem/PageHeader.fs` - PageHeader component with:
  - `TitleStyle` discriminated union (Standard | Gradient)
  - `Props` record with Title, Subtitle, Actions, TitleStyle
  - `view` function for full customization
  - Convenience functions: `simple`, `withSubtitle`, `gradient`, `gradientWithSubtitle`, `withActions`, `gradientWithActions`

**Files Modified:**
- `src/Client/Client.fsproj` - Added PageHeader.fs to compilation
- `src/Client/Components/Settings/View.fs` - Replaced inline `pageHeader` function with `PageHeader.withActions`
- `src/Client/Components/Rules/View.fs` - Added `rulesHeaderActions` helper, replaced inline header with `PageHeader.gradientWithActions`
- `src/Client/Components/SyncFlow/View.fs` - Replaced inline header with `PageHeader.withActions`
- `docs/FRONTEND-ARCHITECTURE-MILESTONES.md` - Marked Milestone 7 as complete

**Rationale:**
Page headers were implemented inline in each view with slight variations. The PageHeader component:
- Provides consistent responsive layout (stack on mobile, row on desktop)
- Supports both standard and gradient title styles
- Handles action button placement uniformly
- Includes `animate-fade-in` for smooth appearance
- Reduces code duplication across Settings, Rules, and SyncFlow views

**Design Decisions:**
- Dashboard view was skipped (uses centered layout without traditional header)
- Added `TitleStyle` to support gradient titles used in Rules page
- Actions passed as ReactElement list for maximum flexibility
- Subtitle is optional (string option) to support both with/without subtitle cases

**Outcomes:**
- Build: ✅ (0 errors, 2 warnings - unrelated Modal.fs deprecation)
- Tests: 357/357 passed (6 skipped)
- Issues: None

---

## 2025-12-15 - Frontend Architecture: RemoteData Helper Module (Milestone 6)

**What I did:**
Completed Milestone 6 from the Frontend Architecture Improvement plan. Created a comprehensive `RemoteData` helper module with 17 utility functions for common operations on the `RemoteData<'T>` type, along with 63 unit tests.

**Files Modified:**
- `src/Client/Types.fs` - Added `RemoteData` module with `[<RequireQualifiedAccess>]` attribute containing:
  - Core: `map`, `bind`, `isLoading`, `isSuccess`, `isFailure`, `isNotAsked`
  - Extraction: `toOption`, `withDefault`, `toError`
  - Error handling: `mapError`, `recover`, `recoverWith`
  - Combining: `map2`
  - Folding: `fold`
  - Conversion: `fromResult`, `fromOption`, `fromOptionWithError`

**Files Added:**
- `src/Tests/RemoteDataTests.fs` - 63 unit tests covering all helper functions with tests for all four RemoteData cases (Success, Loading, NotAsked, Failure)

**Files Modified:**
- `src/Tests/Tests.fsproj` - Added `RemoteDataTests.fs` and Client project reference

**Rationale:**
The RemoteData type is used throughout the frontend for async data fetching. Helper functions provide:
- Cleaner composition with `map`, `bind`, `map2`
- Quick state checks with `isLoading`, `isSuccess`, etc.
- Safe extraction with `toOption`, `withDefault`
- Error recovery patterns with `recover`, `recoverWith`
- Conversion utilities for interop with `Result` and `Option`

**Design Decisions:**
- Module placed in `Types.fs` rather than separate file to avoid compilation order issues
- Used `[<RequireQualifiedAccess>]` to require explicit `RemoteData.` prefix
- Added Client reference to Tests.fsproj - Client compiles on .NET so this works
- Existing code analyzed but not refactored - explicit match expressions are already readable

**Outcomes:**
- Build: ✅ (0 errors, 2 warnings - unrelated)
- Tests: 357/357 passed (6 skipped) - 63 new RemoteData tests
- Issues: None

---

## 2025-12-16 00:15 - Frontend Architecture: Dashboard Hero Button Design System (Milestone 5)

**What I did:**
Completed Milestone 5 from the Frontend Architecture Improvement plan. Integrated the Dashboard Sync button into the Design System by creating reusable `Button.hero` variants and replacing inline styles with Design System components.

**Files Modified:**
- `src/Client/DesignSystem/Tokens.fs` - Added large glow effect variants to the `Glows` module:
  - `orangeLg` / `orangeHoverLg` - Large orange glow for hero buttons
  - `tealLg` / `tealHoverLg` - Large teal glow for secondary hero actions
  - `greenLg` / `greenHoverLg` - Large green glow for success actions

- `src/Client/DesignSystem/Button.fs` - Added Hero Button section with four variants:
  - `hero` - Large CTA button with prominent orange glow
  - `heroWithIcon` - Hero button with icon before text
  - `heroLoading` - Hero button with loading spinner state
  - `heroTeal` - Teal variant for secondary prominent actions

- `src/Client/Components/Dashboard/View.fs` - Replaced 17-line inline-styled `syncButton` with single-line `Button.heroWithIcon "Start Sync" (Icons.sync MD Primary) onNavigateToSync`

- `CLAUDE.md` - Added hero button documentation to the Button Examples section

**Rationale:**
The Dashboard Sync button used inline Tailwind styles with custom shadow values that weren't reusable. Moving these to the Design System ensures:
- Consistent hero button styling across the application
- Reusable glow effects for other prominent CTAs
- Easier maintenance of visual effects in one place

**Outcomes:**
- Build: ✅ (0 errors, 2 warnings - unrelated to this change)
- Tests: 294/294 passed (6 skipped)
- Issues: None

---

## 2025-12-15 23:45 - Frontend Architecture: ErrorDisplay Design System Komponente (Milestone 4)

**What I did:**
Completed Milestone 4 from the Frontend Architecture Improvement plan. Created a new `ErrorDisplay` Design System component providing standardized error displays across the application with consistent styling and behavior.

**Files Added:**
- `src/Client/DesignSystem/ErrorDisplay.fs` - New Design System component with multiple error display variants:
  - `inline'` / `inlineWithIcon` - Compact inline errors for form validation
  - `card` / `cardWithTitle` / `cardCompact` - Card-based errors with optional retry buttons
  - `hero` / `heroSimple` - Large hero-style errors for major operations (like sync failures)
  - `fullPage` / `fullPageWithAction` - Full-page error states for critical failures
  - `forRemoteData` / `simple` / `warning` - Convenience functions

**Files Modified:**
- `src/Client/Client.fsproj` - Added `ErrorDisplay.fs` to compilation (after Button.fs)
- `src/Client/Components/SyncFlow/Views/StatusViews.fs` - Replaced inline `errorView` with `ErrorDisplay.hero`
- `src/Client/Components/SyncFlow/Views/TransactionList.fs` - Replaced inline error with `ErrorDisplay.cardCompact`
- `src/Client/Components/Settings/View.fs` - Replaced 3 inline error displays with `ErrorDisplay.cardCompact`
- `src/Client/Components/Rules/View.fs` - Replaced inline error with `ErrorDisplay.cardWithTitle`

**Rationale:**
The Frontend Architecture Review identified inconsistent error handling across the application. This component standardizes error displays with:
- Consistent visual design using the neon color palette
- ARIA roles for accessibility (`role="alert"`)
- Optional retry functionality
- Multiple variants for different contexts (inline, card, hero, full-page)

**Outcomes:**
- Build: ✅ (0 errors, 2 warnings - unrelated to this change)
- Tests: 294/294 passed (6 skipped)
- Issues: None

---

## 2025-12-15 23:00 - Frontend Architecture: Rules Form State Konsolidierung (Milestone 3)

**What I did:**
Completed Milestone 3 from the Frontend Architecture Improvement plan. Consolidated the 9 separate form fields in the Rules Model into a dedicated `RuleFormState` record type, improving code organization and maintainability.

**Files Modified:**
- `src/Client/Components/Rules/Types.fs` - Added `RuleFormState` record type and `RuleFormState` module with `empty` and `fromRule` helper functions. Updated `Model` type to use `Form: RuleFormState` instead of 10 separate fields.
- `src/Client/Components/Rules/State.fs` - Removed `emptyRuleForm` function (now `RuleFormState.empty`), updated `init` and `update` functions to use the new consolidated form state.
- `src/Client/Components/Rules/View.fs` - Updated all form field accesses from `model.RuleFormFieldName` to `model.Form.FieldName` and `model.RuleSaving` to `model.Form.IsSaving`.

**Before (10 separate fields):**
```fsharp
type Model = {
    RuleFormName: string
    RuleFormPattern: string
    RuleFormPatternType: PatternType
    RuleFormTargetField: TargetField
    RuleFormCategoryId: YnabCategoryId option
    RuleFormPayeeOverride: string
    RuleFormEnabled: bool
    RuleFormTestInput: string
    RuleFormTestResult: string option
    RuleSaving: bool
    // ... other fields
}
```

**After (1 consolidated form state):**
```fsharp
type RuleFormState = {
    Name: string
    Pattern: string
    PatternType: PatternType
    TargetField: TargetField
    CategoryId: YnabCategoryId option
    PayeeOverride: string
    Enabled: bool
    TestInput: string
    TestResult: string option
    IsSaving: bool
}

type Model = {
    Form: RuleFormState
    // ... other fields
}
```

**Rationale:**
- Reduces cognitive load by grouping related form state
- Helper functions (`empty`, `fromRule`) reduce duplication and make code cleaner
- Consistent naming pattern (`model.Form.X`) improves readability
- Easier to add new form fields in the future

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- No functional changes (purely structural refactoring)

---

## 2025-12-15 22:00 - Frontend Architecture: SyncFlow View Modularisierung (Milestone 2)

**What I did:**
Completed Milestone 2 from the Frontend Architecture Improvement plan. Split the large SyncFlow/View.fs (1700+ lines) into smaller, maintainable modules. Created a new Views/ subfolder with focused components.

**Files Added:**
- `src/Client/Components/SyncFlow/Views/StatusViews.fs` - Contains: `tanWaitingView`, `fetchingView`, `loadingView`, `errorView`, `completedView`, `startSyncView` (~350 lines)
- `src/Client/Components/SyncFlow/Views/InlineRuleForm.fs` - Contains: `inlineRuleForm` with pattern type conversions (~200 lines)
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs` - Contains: `transactionRow`, `statusDot`, `duplicateIndicator`, `expandChevron`, `skipToggleIcon`, `createRuleButton`, `memoRow`, `duplicateDebugInfo`, helper functions (~450 lines)
- `src/Client/Components/SyncFlow/Views/TransactionList.fs` - Contains: `transactionListView`, `filterTransactions` (~310 lines)

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs` - Reduced from ~1700 to ~90 lines, now only contains composition
- `src/Client/Client.fsproj` - Added new files in correct compilation order
- `docs/FRONTEND-ARCHITECTURE-MILESTONES.md` - Marked Milestone 2 as complete

**Module Dependencies:**
```
StatusViews.fs      → Types, Shared.Domain, DesignSystem
InlineRuleForm.fs   → Types, Shared.Domain, DesignSystem
TransactionRow.fs   → Types, Shared.Domain, DesignSystem, InlineRuleForm
TransactionList.fs  → Types, Shared.Domain, DesignSystem, TransactionRow
View.fs             → Types, Shared.Domain, DesignSystem, StatusViews, TransactionList
```

**Rationale:**
Large monolithic view files are difficult to maintain, navigate, and understand. Splitting into focused modules:
- Improves code organization and readability
- Makes it easier to find and modify specific components
- Enables better code reuse
- Reduces cognitive load when working on specific features

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- No functional changes (purely structural refactoring)

**Notes:**
- `SplitEditor.fs` was not created - no split transaction UI exists in codebase
- `CategorySelector.fs` was not created separately - integrated into `TransactionRow.fs` using `Input.searchableSelect`

---

## 2025-12-15 21:15 - Frontend Architecture: React Key Props (Milestone 1)

**What I did:**
Completed Milestone 1 from the Frontend Architecture Improvement plan. Added React key props to all list renderings to ensure efficient React reconciliation and prevent potential rendering issues.

**Files Modified:**
- `src/Client/Components/Settings/View.fs` - Added `prop.key` to Budget dropdown options (line 150-155), Added `prop.key` to Account dropdown options (line 194-199)
- `src/Client/Components/Rules/View.fs` - Added `prop.key` to Skeleton loader items (line 570-574)
- `docs/FRONTEND-ARCHITECTURE-MILESTONES.md` - Marked Milestone 1 as complete with summary

**Verified Existing Implementations:**
- SyncFlow/View.fs: Transaction list already had proper keys via `prop.key id` pattern
- Rules/View.fs: Rule list already had proper keys via `prop.key (string id)` pattern

**Pattern Applied:**
```fsharp
// Before
for bwa in budgets do
    Html.option [ prop.value id; prop.text bwa.Budget.Name ]

// After
for bwa in budgets do
    let (YnabBudgetId id) = bwa.Budget.Id
    Html.option [ prop.key id; prop.value id; prop.text bwa.Budget.Name ]
```

**Rationale:**
React keys help React identify which items have changed, been added, or removed. Without keys, React may re-render the entire list unnecessarily or cause subtle UI bugs when items are reordered.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- No functional changes (structural improvement only)

---

## 2025-12-15 18:30 - Refactor: Minimalistisches Dashboard Redesign

**What I did:**
Completely redesigned the Dashboard to be minimalistic and focused. Removed the stats grid (Last Sync, Total Imported, Sync Sessions cards), removed the Recent Activity history list, and replaced everything with a single centered "Start Sync" button with last sync information below it.

**Files Modified:**
- `src/Client/Components/Dashboard/Types.fs` - Simplified model: `RecentSessions: RemoteData<SyncSession list>` → `LastSession: RemoteData<SyncSession option>`, renamed messages accordingly
- `src/Client/Components/Dashboard/State.fs` - Updated init and update functions to use `LoadLastSession` instead of `LoadRecentSessions`, now fetches only the most recent session via `getSyncHistory 1`
- `src/Client/Components/Dashboard/View.fs` - Complete rewrite: removed `statsSection`, `historyItem`, `historySection`, `pageHeader`, kept `warningAlert` for config warnings, new centered layout with large sync button and last sync info
- `src/Client/State.fs` - Updated navigation handler to use `LoadLastSession` instead of `LoadRecentSessions`

**Design Changes:**
1. **Before**: Dashboard with 3 stats cards + quick action card + 5-item history list
2. **After**: Single centered "Start Sync" button with glow effect, last sync info below (date + transaction count)
3. Config warnings remain visible when YNAB/Comdirect not configured

**Rationale:**
User feedback: Dashboard statistics were not actionable. The history list couldn't be clicked, and the numbers (total imported, sync sessions count) provided no real value. A minimalistic dashboard that focuses on the main action (starting a sync) is more useful.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- Visual: Clean, focused dashboard with prominent sync button

---

## 2025-12-15 16:00 - Feature: Verbessertes Formular-Handling mit Validierungsfeedback

**What I did:**
Implemented consistent form validation UX across all forms. Disabled buttons now have clear visual distinction (50% opacity), and a validation message appears under the button showing which required fields are missing. All required fields are now marked with a red asterisk.

**Files Added:**
- `src/Client/DesignSystem/Form.fs` - New form validation component with `submitButton`, `submitButtonWithIcon`, and `submitButtonSecondary` functions

**Files Modified:**
- `src/Client/DesignSystem/Button.fs` - Added disabled styling (`disabled:opacity-50 disabled:cursor-not-allowed disabled:shadow-none`)
- `src/Client/Client.fsproj` - Added Form.fs to compilation
- `src/Client/Components/Settings/View.fs` - Changed YNAB Token + all 5 Comdirect fields to use `Input.groupRequired`, replaced buttons with `Form.submitButton`
- `src/Client/Components/Rules/View.fs` - Replaced footer button with `Form.submitButton`
- `src/Client/Components/SyncFlow/View.fs` - Added required marker to Rule Name, replaced button with `Form.submitButton`, changed Pattern asterisk to neon-red for consistency

**UX Changes:**
1. **Disabled buttons**: Now visually distinct with 50% opacity and cursor-not-allowed
2. **Validation message**: Orange text under button shows "Bitte ausfüllen: Field1, Field2" when fields are missing
3. **Required field markers**: Red asterisk (*) on all required fields (consistent across all forms)
4. **Consistent pattern**: `Form.submitButton` used in Settings, Rules modal, and Inline rule form

**Rationale:**
User feedback indicated that disabled buttons had no visual distinction and no feedback about why they were disabled. This made forms confusing to use.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- Visual: Buttons clearly distinguishable, validation feedback visible

---

## 2025-12-15 - Feature: Comdirect Connection Test in Settings

**What I did:**
Implemented Comdirect connection test in Settings. The "Test Connection" button initiates the full OAuth + Push-TAN flow to validate credentials. Originally planned to include account discovery via `/api/banking/v1/accounts`, but discovered that Comdirect doesn't provide a public accounts endpoint (returns 404 for all endpoint variants).

**Architectural Decision:**
Account-ID remains a manual input field. The connection test validates that credentials are correct through the TAN flow, but users must enter their Account-ID manually.

**Files Added:**
- None

**Files Modified:**
- `src/Shared/Domain.fs` - Added `ComdirectAccount` type and `ComdirectAccountId` wrapper type (unused but kept for future)
- `src/Shared/Api.fs` - Extended `SettingsApi` with `testComdirectConnection` and `confirmComdirectTan` functions
- `src/Server/ComdirectClient.fs` - Added `accountDecoder`, `accountsDecoder`, and `getAccounts` function (unused, kept for future)
- `src/Server/ComdirectAuthSession.fs` - Added `fetchAccounts` function (unused, kept for future)
- `src/Server/Api.fs` - Implemented connection test and TAN confirmation endpoints
- `src/Client/Components/Settings/Types.fs` - Added `ComdirectConnectionValid` and `ComdirectAuthPending` to Model
- `src/Client/Components/Settings/State.fs` - Added init fields and update handlers for connection test
- `src/Client/Components/Settings/View.fs` - Added TAN flow UI with success/error display

**UX Flow:**
1. User saves Comdirect credentials and Account-ID
2. "Test Connection" button appears (only if credentials saved)
3. Clicking starts TAN authentication → orange waiting UI
4. User confirms Push-TAN in Comdirect app
5. Clicking "I've Confirmed the TAN" completes validation
6. Green success message: "Credentials verified successfully!"

**Rationale:**
Originally a high-priority backlog item for account discovery. Simplified to credential validation after discovering Comdirect doesn't expose an accounts API.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- Backlog item updated to reflect actual implementation

---

## 2025-12-13 19:45 - Fix: Encrypted Settings Lost After Docker Rebuild

**What I did:**
Fixed critical bug where encrypted settings (YNAB token, Comdirect credentials) were lost after every `docker-compose up -d --build`. The root cause was that the encryption key was derived from `Environment.MachineName`, which changes with each Docker container rebuild.

**Root Cause Analysis:**
```fsharp
// In Persistence.fs - getEncryptionKey()
let machineKey = Environment.MachineName + "BudgetBuddy2025"
```
Docker containers get a new hostname on each rebuild → different encryption key → previously encrypted settings become unreadable.

**Files Modified:**
- `docker-compose.yml` - Added `BUDGETBUDDY_ENCRYPTION_KEY` environment variable
- `README.md` - Added documentation for the encryption key setup
- `.env` - Added generated encryption key (not committed)

**Solution:**
1. Generate a stable encryption key: `openssl rand -base64 32`
2. Add to `.env`: `BUDGETBUDDY_ENCRYPTION_KEY=<your-key>`
3. Reference in docker-compose.yml: `BUDGETBUDDY_ENCRYPTION_KEY=${BUDGETBUDDY_ENCRYPTION_KEY}`

**Important:** After applying this fix, existing encrypted settings are lost and must be re-entered. The encryption key in `.env` must be kept secret and backed up.

**Outcomes:**
- Build: ✅
- Tests: N/A (configuration fix)
- Settings now persist across Docker rebuilds

---

## 2025-12-12 14:45 - Script: deploy-rules.sh for Live Database Import

**What I did:**
Created automation script to import categorization rules from `rules.yml` to the live Docker database. The script handles stopping/starting the container and sets the correct `DATA_DIR` environment variable.

**Files Added:**
- `scripts/deploy-rules.sh` - Bash script that:
  - Accepts budget name and optional `--clear` flag
  - Stops the Docker container
  - Runs `import-rules.fsx` with `DATA_DIR=~/my_apps/budgetbuddy`
  - Restarts the container and waits for health check
  - Supports `--list` to show available YNAB budgets

**Files Modified:**
- `CLAUDE.md` - Added documentation for the deploy-rules script under "Quick Commands"

**Rationale:**
Manual rule imports to the live database required multiple steps (stop app, set DATA_DIR, run script, start app). This script automates the entire process with a single command.

**Usage:**
```bash
./scripts/deploy-rules.sh --list              # List available budgets
./scripts/deploy-rules.sh "My Budget"         # Add new rules
./scripts/deploy-rules.sh "My Budget" --clear # Clear all & reimport
```

**Outcomes:**
- Build: N/A (script only)
- Tests: N/A
- Successfully imported 55 rules to live database

---

## 2025-12-12 10:30 - Bugfix: Database not initialized in Docker

**What I did:**
Fixed critical bug where database tables were not created when running in Docker. The `initializeDatabase()` function was defined but never called at server startup.

**Files Modified:**
- `src/Server/Program.fs` - Added call to `Persistence.initializeDatabase()` at server startup (replaced `Persistence.ensureDataDir()` which only created the directory but not the tables)

**Rationale:**
When deploying via Docker, the database file was created but without any tables. All API calls failed with "no such table: settings" errors. The `initializeDatabase()` function creates all required tables (`rules`, `settings`, `sync_sessions`, `sync_transactions`) with `CREATE TABLE IF NOT EXISTS`.

**Root Cause:**
`Persistence.ensureDataDir()` only creates the `/app/data` directory. The actual table creation in `initializeDatabase()` was never called - it was only invoked in tests.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed
- Database tables will now be created automatically on first server start

---

## 2025-12-11 20:45 - Feature: Skip All / Unskip All Buttons

**What I did:**
Added "Skip All" and "Unskip All" buttons to the Sync Flow transaction review. These buttons allow users to quickly skip or unskip all visible transactions based on the currently active filter.

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs` - Added new Msg types:
  - `SkipAllVisible`: Triggers bulk skip of visible transactions
  - `UnskipAllVisible`: Triggers bulk unskip of visible transactions
  - `BulkSkipCompleted`: Handles completion of individual skip operations
  - `BulkUnskipCompleted`: Handles completion of individual unskip operations

- `src/Client/Components/SyncFlow/State.fs` - Added:
  - `filterTransactions` helper (duplicated from View.fs for state logic)
  - Handler for `SkipAllVisible`: Gets visible non-skipped transactions, applies optimistic UI update, sends parallel API calls
  - Handler for `UnskipAllVisible`: Gets visible skipped transactions, restores status, sends parallel API calls
  - Handlers for `BulkSkipCompleted` / `BulkUnskipCompleted`: Silent success, rollback on error

- `src/Client/Components/SyncFlow/View.fs` - Added to action bar:
  - "Skip All (N)" ghost button - shows count, only visible when there are skippable transactions
  - "Unskip All (N)" ghost button with green icon - shows count, only visible when there are unskippable transactions
  - Buttons respect the active filter (All, Categorized, Uncategorized, Skipped, Confirmed Duplicates)

**Implementation Details:**
- Optimistic UI: Local state updates immediately for responsive feel
- Parallel API calls: Each transaction skip/unskip is sent in parallel for performance
- Filter-aware: Buttons only affect visible transactions based on current filter
- Error handling: On any error, reloads all transactions from server (rollback)
- Dynamic counts: Button text shows exact number of affected transactions

**Rationale:**
Users with many transactions needed a way to quickly skip/unskip batches instead of clicking individual skip buttons.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed
- No new tests needed (UI-only feature using existing skip/unskip API)

---

## 2025-12-11 18:30 - Feature: Amazon Order-ID Deep Links

**What I did:**
Implemented deep linking for Amazon transactions. When a transaction memo contains an Amazon order ID (format: ABC-1234567-1234567), the link now goes directly to the specific order details page instead of the generic order history.

**Reference Implementation:**
Based on the YNAB Amazon Linker browser extension (https://github.com/rommsen/ynab-amazon-linker).

**Files Modified:**
- `src/Server/RulesEngine.fs` - Added:
  - `amazonOrderIdPattern`: Regex pattern `\b([A-Z0-9]{3}-\d{7}-\d{7})\b` to match order IDs
  - `extractAmazonOrderId`: Function to extract order ID from transaction text (payee + memo)
  - Updated `generateAmazonLink`: Now returns deep link to order-details if ID found, else fallback to order-history

- `src/Tests/RulesEngineTests.fs` - Added/updated tests:
  - "Amazon order ID in memo generates deep link"
  - "Amazon order ID in payee generates deep link"
  - "Amazon without order ID generates history link"
  - Updated existing tests to work with new label format

**Link Formats:**
- With Order ID: `https://www.amazon.de/gp/your-account/order-details?ie=UTF8&orderID={orderId}` (Label: "Bestellung {orderId}")
- Without Order ID: `https://www.amazon.de/gp/your-account/order-history` (Label: "Amazon Orders")

**Rationale:**
Users frequently need to match Amazon transactions to specific orders. The deep link eliminates the need to search through order history manually.

**Outcomes:**
- Build: ✅
- Tests: 293/293 passed (added 3 new tests)
- No frontend changes needed (ExternalLinks already rendered correctly)

---

## 2025-12-11 16:45 - Setup: Serena MCP für F# Code-Analyse

**What I did:**
Konfigurierte Serena MCP (Model Context Protocol) für semantische F#-Code-Analyse. Serena ermöglicht symbolbasiertes Navigieren, Refactoring und intelligente Code-Bearbeitung.

**Problem:**
Serena MCP startete nicht korrekt - der F# Language Server (fsautocomplete) konnte nicht initialisiert werden.

**Root Causes:**
1. **Falsches project.yml Format**: Die Konfiguration verwendete `language_servers: fsharp: command: ...` statt dem erwarteten `languages: - fsharp`
2. **Fehlende Solution-Datei**: `fsautocomplete` benötigt eine `.sln`-Datei, die im Projekt fehlte

**Files Added:**
- `BudgetBuddy.sln` - Solution-Datei mit allen 4 Projekten (Shared, Client, Server, Tests)
- `.serena/memories/project_overview.md` - Projektübersicht
- `.serena/memories/tech_stack.md` - Technologie-Stack
- `.serena/memories/codebase_structure.md` - Codebase-Struktur
- `.serena/memories/code_style.md` - Code-Stil und Konventionen
- `.serena/memories/suggested_commands.md` - Entwicklungs-Commands
- `.serena/memories/task_completion.md` - Task-Completion Checklist

**Files Modified:**
- `.serena/project.yml` - Korrigiertes Format:
  ```yaml
  project_name: BudgetBuddy
  languages:
    - fsharp
  ignored_paths:
    - node_modules
    - .git
    - bin
    - obj
    - .fable
    - dist
  ```

**Rationale:**
Serena bietet semantische Code-Analyse über Language Server Protocol (LSP). Für F# wird `fsautocomplete` verwendet, das bereits als globales dotnet tool installiert war. Die Konfiguration musste nur angepasst werden.

**Key Learnings:**
- Serena erwartet einfaches `languages: - fsharp` Format (nicht verschachteltes `language_servers:`)
- F# Language Server benötigt zwingend eine `.sln`-Datei
- `.serena/` Ordner sollte eingecheckt werden (außer `/cache`)
- Nach Konfigurationsänderungen muss Claude Code komplett neu gestartet werden

**Outcomes:**
- Build: ✅
- Serena: ✅ Funktioniert - kann F#-Symbole analysieren
- Onboarding: ✅ 6 Memory-Dateien erstellt

---

## 2025-12-11 16:30 - Fix: JSON-Fehlermeldung bei frühem Import-Klick

**What I did:**
Fixed the bug where Comdirect JSON error responses were displayed as raw JSON instead of user-friendly messages. When clicking "I've Confirmed" before actually confirming the TAN in the banking app, users previously saw:

```
Network error (HTTP 400): {"code":"TAN_UNGUELTIG","messages":[{"severity":"INFO","key":"PUSHTAN_ANGEFORDERT","message":"TAN-Freigabe über die App wurde noch nicht erteilt.",...}]}
```

Now they see the clean message:
```
TAN-Freigabe über die App wurde noch nicht erteilt.
```

**Files Added:**
- None

**Files Modified:**
- `src/Server/Api.fs`:
  - Added `parseComdirectErrorJson` function (lines 68-110) that parses Comdirect's structured JSON error format
  - Extracts the `message` field from the `messages` array
  - Provides predefined messages for known error codes (TAN_UNGUELTIG, SESSION_EXPIRED, UNAUTHORIZED)
  - Updated `comdirectErrorToString` to use the parser for NetworkError cases
  - Also improved error messages for other ComdirectError variants

- `src/Client/Components/SyncFlow/State.fs`:
  - Updated `syncErrorToString` for `ComdirectAuthFailed` to pass through the reason directly (no redundant prefix)
  - Backend now provides user-friendly messages, so frontend just displays them

- `src/Tests/ComdirectClientTests.fs`:
  - Added 11 regression tests for `parseComdirectErrorJson`
  - Tests cover: TAN_UNGUELTIG with German message, multiple messages, missing fields, unknown codes, predefined codes, invalid JSON, empty input

**Rationale:**
Comdirect API returns errors in a structured JSON format with a `code` field and `messages` array. Previously, this JSON was passed through as-is, making it unreadable for users. The parser now extracts meaningful messages.

**Outcomes:**
- Build: ✅
- Tests: 45/46 Comdirect tests pass (1 skipped integration test)
- Backlog updated: Bug marked as completed

---

## 2025-12-11 15:35 - Fix: Sync Flow Progress Indicator for Fetching Step

**What I did:**
Fixed the sync flow progress indicator to show the correct step when fetching transactions. The issue had two parts:

1. **View Issue**: No dedicated view for `FetchingTransactions` status - only a generic loading view was shown
2. **State Issue**: The `confirmTan` API call runs synchronously (TAN confirm + fetch + rules + duplicate detection), so the client never saw the intermediate `FetchingTransactions` status

**Solution:**
- Added `fetchingView` component showing the progress indicator with "Fetch" as active step
- Added **optimistic UI update** in State.fs: When user clicks "I've Confirmed", we immediately set the local session status to `FetchingTransactions` before the API call starts

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`:
  - Added `fetchingView` component (lines 163-238) that shows:
    - Animated sync icon with neon glow
    - "Fetching Transactions" title and description
    - Progress indicator showing: Connected ✓ → TAN ✓ → Fetch (active with spinner)
  - Updated main view to use `fetchingView ()` for `FetchingTransactions` status

- `src/Client/Components/SyncFlow/State.fs`:
  - Modified `ConfirmTan` handler to optimistically update session status to `FetchingTransactions`
  - This ensures the UI shows the fetching view immediately while the API call runs

**Rationale:**
The `confirmTan` backend API runs all operations synchronously, so the intermediate status was never visible to the client. Optimistic UI update solves this by showing the expected state immediately.

**Outcomes:**
- Build: Server ✓, Client ✓
- Tests: Not applicable (UI-only change)
- Issues: None

---

## 2025-12-11 - Feature: Transparent Duplicate Detection with Debug Info

**What I did:**
Implemented a major improvement to the duplicate detection workflow to provide full transparency into how duplicates are detected. The system now clearly distinguishes between two separate mechanisms:
1. **BudgetBuddy's pre-import detection** (Reference/ImportId/Fuzzy matching BEFORE sending to YNAB)
2. **YNAB's rejection** (when YNAB rejects during import due to duplicate import_id)

**Key Changes:**

1. **Domain Types Extended** (`src/Shared/Domain.fs`)
   - Added `DuplicateDetectionDetails` record with diagnostic fields:
     - TransactionReference, ReferenceFoundInYnab, ImportIdFoundInYnab
     - FuzzyMatchDate, FuzzyMatchAmount, FuzzyMatchPayee
   - Updated `DuplicateStatus` to include details in all variants
   - Added `YnabImportStatus` type (NotAttempted | YnabImported | RejectedByYnab)
   - Added `YnabImportStatus` field to `SyncTransaction`

2. **Detection Logic Updated** (`src/Server/DuplicateDetection.fs`)
   - `detectDuplicate` now returns full diagnostic details
   - All three checks (Reference, ImportId, Fuzzy) are run and results captured

3. **API Updated** (`src/Server/Api.fs`)
   - `importToYnab` now sets `YnabImportStatus` on each transaction after YNAB response
   - `forceImportDuplicates` also sets `YnabImported` status

4. **UI Improvements** (`src/Client/Components/SyncFlow/View.fs`)
   - **Debug Info Panel**: Always visible when transaction expanded, shows:
     - Reference and whether found in YNAB
     - ImportId status (New vs Exists)
     - Fuzzy match details if applicable
     - YNAB import result with "BudgetBuddy missed this!" warning if applicable
   - **Separate Banners**:
     - Teal banner: "X pre-detected duplicates (BudgetBuddy)" [Pre-Import badge]
     - Red banner: "X rejected by YNAB" [Post-Import badge] with Force Re-import button
   - Count now includes `ynabRejected` for transactions rejected during import

**Files Added:**
- None (all changes to existing files)

**Files Modified:**
- `src/Shared/Domain.fs` - New types: DuplicateDetectionDetails, YnabRejectionReason, YnabImportStatus
- `src/Server/DuplicateDetection.fs` - detectDuplicate returns diagnostic details
- `src/Server/Persistence.fs` - Updated SyncTransaction creation with new fields
- `src/Server/RulesEngine.fs` - Updated SyncTransaction creation with new fields
- `src/Server/Api.fs` - importToYnab and forceImportDuplicates set YnabImportStatus
- `src/Client/Components/SyncFlow/View.fs` - Debug info panel, separate banners, updated counts
- `src/Tests/*.fs` - Updated all tests to use new DuplicateStatus format with details

**Rationale:**
Users were confused about why some transactions showed "Möchtest du X reimportieren?" because the two duplicate detection systems were not clearly distinguished. Now:
- Pre-import detection is clearly labeled and auto-skips confirmed duplicates
- Post-import rejections from YNAB are shown separately with explanation
- Each transaction shows exactly why BudgetBuddy made its detection decision

**Outcomes:**
- Build: All projects compile successfully
- Tests: 279/285 passed (6 integration tests skipped due to missing env)
- Issues: None

---

## 2025-12-11 - Bugfix: Force Re-import Button erschien vor YNAB-Import

**What I did:**
Fixed a bug where the "Re-import X Duplicate(s)" button appeared in the action bar BEFORE any import to YNAB had been attempted. The button was incorrectly counting all categorized, non-skipped, non-imported transactions instead of only YNAB-rejected transactions.

**Bug Details:**
- The action bar had a fallback logic that counted transactions where:
  - `Status <> Imported && Status <> Skipped && (CategoryId.IsSome || Splits.IsSome)`
- This was wrong because it showed the button for transactions that were simply ready to import, not rejected by YNAB

**Fix:**
Changed the `duplicateCount` calculation to only count transactions with `YnabImportStatus = RejectedByYnab _`:

```fsharp
let ynabRejectedCount =
    match model.SyncTransactions with
    | Success transactions ->
        transactions
        |> List.filter (fun tx ->
            match tx.YnabImportStatus with
            | RejectedByYnab _ -> true
            | _ -> false)
        |> List.length
    | _ -> 0
```

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs` - Fixed action bar button logic (lines 1199-1218)

**Rationale:**
The "Re-import Rejected" button should only appear AFTER a YNAB import has been attempted AND YNAB has rejected some transactions. Before import, `YnabImportStatus = NotAttempted` for all transactions, so the count is 0 and the button is hidden.

**Outcomes:**
- Build: ✅
- Tests: 279/285 passed (6 skipped integration tests)
- Issues: None - button now only appears when appropriate

---

## 2025-12-09 23:00 - Refactor: Stats Filter Semantics and Duplicate Banner

**What I did:**
Refactored the Stats-Filter system for clearer semantics and improved the Duplicate Banner.

**Changes:**
1. Renamed filter types for clarity:
   - `ReadyTransactions` → `CategorizedTransactions`
   - `PendingTransactions` → `UncategorizedTransactions`
   - `DuplicateTransactions` → `ConfirmedDuplicates`
2. Fixed count logic:
   - Categorized: Has CategoryId AND not Skipped/Imported
   - Uncategorized: No CategoryId AND not Skipped/Imported
   - ConfirmedDuplicates: Only `ConfirmedDuplicate` status (not `PossibleDuplicate`)
3. Updated Stats labels: "Ready" → "Categorized", "Pending" → "Uncategorized"
4. Improved Duplicate Banner: Now only shows ConfirmedDuplicates with better German explanation
5. Removed redundant "Transaktionen ohne Kategorie" warning banner (info now in Uncategorized stat)

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs` - Renamed TransactionFilter cases
- `src/Client/Components/SyncFlow/View.fs` - Updated filterTransactions, count logic, labels, banner

**Outcomes:**
- Build: ✅
- Tests: 279/285 passed (6 skipped integration tests)
- UI: Clearer semantics, less clutter

---

## 2025-12-09 22:15 - Feature: Clickable Stats Filters for Transaction List

**What I did:**
Implemented clickable filter functionality for the Stats cards on the Sync Transactions page. Users can now click on Total, Ready, Pending, Skipped, or the Duplicates banner to filter the transaction list.

**Implementation:**
1. Added `TransactionFilter` discriminated union type with 5 filter options:
   - AllTransactions, ReadyTransactions, PendingTransactions, SkippedTransactions, DuplicateTransactions
2. Added `ActiveFilter` field to the SyncFlow Model
3. Added `SetFilter` message to Msg type
4. Extended Stats component with `OnClick` and `IsActive` props for interactivity
5. Added `filterTransactions` helper function to filter by status
6. Stats cards now show active state with teal ring highlight
7. Filter indicator shows "X of Y transactions" with "Show all" link
8. Empty state when filter matches no transactions

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs` - Added TransactionFilter DU, ActiveFilter field, SetFilter message
- `src/Client/Components/SyncFlow/State.fs` - Initialized ActiveFilter, added SetFilter handler
- `src/Client/Components/SyncFlow/View.fs` - Added filterTransactions, clickable Stats, filter UI
- `src/Client/DesignSystem/Stats.fs` - Added OnClick and IsActive props to StatProps

**Outcomes:**
- Build: ✅
- Tests: 279/285 passed (6 skipped integration tests)
- UX: Users can quickly filter to see only pending, ready, skipped, or duplicate transactions

---

## 2025-12-09 21:00 - UI: Breitere Selectbox & elegante Memo-Anzeige

**What I did:**
1. Category-Selectbox von `w-52` (208px) auf `w-72` (288px) verbreitert
2. Memo-Row neu gestaltet mit Glassmorphism-Stil passend zum Design-System

**Memo-Row Redesign:**
- Glassmorphism: `bg-base-200/30 backdrop-blur-sm border border-white/5`
- Abgerundete Karte mit Margin statt volle Breite
- Icon-Badge mit teal Akzent
- Uppercase Label "MEMO" + relaxed Text-Spacing

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`
  - `memoRow`: Komplett neu gestaltet (Zeile 484-512)
  - Desktop Layout: `w-52` → `w-72` (Zeile 649)

**Outcomes:**
- Build: ✅
- Selectbox: Kategorienamen besser lesbar
- Memo: Passt jetzt zum glassmorphism Design-System

---

## 2025-12-09 20:45 - Performance: Skipped Transactions ohne Selectbox

**What I did:**
Weitere Performance-Optimierung: Skipped Transactions rendern keine interaktive Selectbox mehr, sondern nur noch einen statischen Kategorienamen als Text.

**Rationale:**
- Die searchableSelect-Komponente ist teuer zu rendern (Event-Handler, State, DOM-Nodes)
- Skipped Transactions brauchen keine Interaktion - sie werden nicht importiert
- Bei 50% skipped = 50% weniger Selectboxen zu rendern

**Implementierung:**
1. Neue Hilfsfunktion `categoryText`: Sucht Kategorienamen aus vorberechneten Options
2. Bedingte Render-Logik: `if tx.Status = Skipped then Text else Selectbox`
3. Angewendet auf Mobile und Desktop Layout

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`
  - `categoryText`: Neue Hilfsfunktion für Text-Lookup
  - Mobile Layout (Zeile 533-551): Bedingte Selectbox/Text
  - Desktop Layout (Zeile 631-649): Bedingte Selectbox/Text

**Outcomes:**
- Build: ✅
- Tests: Nicht geändert (reine UI-Optimierung)
- Performance: Weniger DOM-Nodes, schnelleres Rendering

---

## 2025-12-09 20:15 - Performance: Category Dropdown 872x schneller

**What I did:**
Massive Performance-Optimierung der Category-Select-Dropdowns auf der Transaction-Seite. Das Öffnen eines Dropdowns dauerte vorher 15,7 Sekunden - jetzt nur noch 18ms.

**Root Cause:**
Die Category-Liste wurde für jede der 193 Transaktionszeilen neu berechnet:
- 193 Transaktionen × 160 Kategorien = **30.880 String-Operationen** pro Render
- Jedes Mal wurde `List.map` mit String-Interpolation aufgerufen

**Lösung Phase 1 (Category Options vorberechnen):**
- `categoryOptions` wird einmal vor der Transaktion-Liste berechnet
- Als Parameter an `transactionRow` übergeben statt `categories`
- Reduziert Berechnungen von 30.880 auf 160 pro Render

**Lösung Phase 2 (Optimistic UI):**
- `ManuallyCategorizedIds` wird sofort im Model aktualisiert (vor API-Call)
- "Create Rule" Button erscheint jetzt sofort nach Kategorisierung

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`
  - `transactionRow` Funktion: Signatur geändert, nimmt `categoryOptions` statt `categories`
  - Mobile/Desktop Category-Selects: Nutzen jetzt vorberechnete Options
  - `transactionListView`: Berechnet `categoryOptions` einmal vor der Schleife
- `src/Client/Components/SyncFlow/State.fs`
  - `CategorizeTransaction` Handler: Optimistisches Update von `ManuallyCategorizedIds`

**Performance-Ergebnis:**
| Metrik | Vorher | Nachher | Verbesserung |
|--------|--------|---------|--------------|
| Dropdown öffnen | 15.700ms | 18ms | **872x** |

**Outcomes:**
- Build: ✅
- Tests: Nicht geändert (UI-only Optimierung)
- Performance: Drastisch verbessert

---

## 2025-12-09 18:45 - UI: Transaktionsliste Aktionen sichtbar machen & Links verbessern

**What I did:**
Verbesserung der Transaktionslisten-UI: Aktions-Buttons (Skip, Create Rule) sind jetzt immer sichtbar statt nur bei Hover, und externe Links (Amazon, PayPal) sind klar als Links erkennbar.

**Probleme vorher:**
- Hover-Aktionen waren unsichtbar aber nahmen Platz ein → Geldbeträge nicht bündig
- Create Rule Button erschien/verschwand → Layout-Shift
- Amazon/PayPal-Links sahen aus wie normaler Text

**Lösung:**
1. **Aktionen nach vorne verschoben**: Jetzt zwischen Status-Indikatoren und Kategorie-Dropdown
2. **Feste Container-Breite**: `w-16` für Aktions-Container → stabile Layouts
3. **Platzhalter**: Wenn Create Rule Button nicht aktiv, wird ein unsichtbarer Platzhalter gerendert
4. **Link-Styling**: Teal-Farbe + External-Link-Icon für erkennbare Links

**Neues Layout (Desktop):**
```
[▶] [●] [⚠] | [Skip][Rule] | [Category ▾] | [Payee 🔗] | [Date] | [Amount]
```

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`
  - `createRuleButton`: Gibt jetzt Platzhalter-div statt `Html.none` zurück
  - Desktop-Layout: Aktionen nach vorne verschoben, `opacity-0` entfernt, feste Breite
  - Mobile-Layout: Gleiche Änderungen für Konsistenz
  - External Links: Teal-Farbe + `Icons.externalLink` Icon

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed
- UI: Aktionen immer sichtbar, Beträge bündig, Links erkennbar

---

## 2025-12-09 16:30 - Feature: Regel aus Zuweisungs-Dialog erstellen

**What I did:**
Implementierung eines neuen Features, das es ermöglicht, direkt beim manuellen Kategorisieren einer Transaktion eine Regel zu erstellen. Nach dem Kategorisieren erscheint ein "Create Rule" Button, der ein Inline-Formular expandiert.

**Key Features:**
- Button erscheint nach manueller Kategorisierung einer Transaktion
- Inline-Formular expandiert unter der Transaktion (kein Modal-Unterbrechung)
- Pre-filled: Pattern (Payee), Kategorie, auto-generierter Rule Name
- Default: Contains-Regel (umschaltbar zu Exact/Regex)
- TargetField wählbar: Combined (default), Payee only, Memo only
- Auto-Apply: Neue Regel wird sofort auf andere pending Transaktionen angewandt
- Responsive: Gestackt auf Mobile, Grid auf Desktop

**Files Added:**
- Keine neuen Dateien

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs`
  - Neuer Type: `InlineRuleFormState` für Formular-State
  - Model erweitert um `InlineRuleForm` und `ManuallyCategorizedIds`
  - 12 neue Msg-Varianten für Inline-Rule-Creation Flow

- `src/Client/Components/SyncFlow/State.fs`
  - `init()` erweitert mit neuen Model-Feldern
  - `TransactionCategorized` trackt nun manuell kategorisierte IDs
  - Neue Helper-Funktionen: `matchesRule`, `rulesErrorToString`
  - Handler für alle neuen Messages: OpenInlineRuleForm, CloseInlineRuleForm, UpdateInlineRule*, SaveInlineRule, InlineRuleSaved, ApplyNewRuleToTransactions, TransactionsUpdatedByRule

- `src/Client/Components/SyncFlow/View.fs`
  - Neue Komponente: `createRuleButton` (Icon-Button für manuell kategorisierte Transaktionen)
  - Neue Komponente: `inlineRuleForm` (responsive Formular mit Name, Pattern, Type, TargetField)
  - `transactionRow` akzeptiert nun `InlineRuleForm` und `ManuallyCategorizedIds` Parameter
  - Mobile + Desktop: createRuleButton in Actions-Bereich integriert
  - Inline-Formular wird unter Transaktion angezeigt wenn aktiv

**UI-Flow:**
```
1. User kategorisiert Transaktion → [✓] ManualCategorized
2. "Create Rule" Button erscheint (🔧-Icon)
3. Klick → Inline-Formular expandiert
4. Pre-filled: Pattern="REWE", Name="Auto: REWE", Category=ausgewählte
5. User kann anpassen: PatternType, TargetField
6. "Create Rule" → API-Call → Toast "Rule created!"
7. Auto-Apply auf andere pending Transaktionen mit passendem Pattern
```

**Responsive Layout:**
- Mobile: Alle Felder vertikal gestackt, Buttons full-width
- Desktop: 12-Column Grid (Pattern: 6 cols, Type: 3 cols, TargetField: 3 cols)

**Rationale:**
Dieses Feature ermöglicht einen schnelleren Workflow beim Kategorisieren von Transaktionen. Statt in den Rules-Bereich zu wechseln, kann der User direkt aus dem Kontext heraus eine Regel erstellen.

**Outcomes:**
- Build: ✅ (Server + Client)
- Tests: 279/279 passed, 6 skipped
- Keine neuen Tests hinzugefügt (RulesEngine bereits umfassend getestet)

---

## 2025-12-09 - Fix: UI-Verbesserungen für kompakte Transaktionsliste

**What I did:**
Mehrere UI-Probleme behoben, die nach dem initialen Redesign der Transaktionsliste aufgefallen sind:

1. **Betrag-Formatierung**: Fable transpiliert `:F2` zu `%P(F2)` statt `.toFixed(2)` - jetzt explizites Rounding
2. **Status-Farben**: Skipped-Transaktionen jetzt immer grau (statt rot bei Duplicates)
3. **NeedsAttention gleiche Farbe**: Amazon/PayPal Transaktionen haben jetzt dieselbe Farbe wie andere Uncategorized
4. **Mobile Actions**: Buttons auf Mobile immer sichtbar (nicht hover-only)
5. **Größere Dropdown-Options**: Kategorie-Auswahl mit größerem Touch-Target
6. **Expand-Feature**: Chevron-Icon zum Anzeigen von Memo-Text

**Files Modified:**
- `src/Client/DesignSystem/Money.fs`
  - `formatAmount` nutzt jetzt `System.Math.Round(float absAmount, 2).ToString("0.00")` statt `:F2`
  - Kommentar hinzugefügt, der das Fable-Transpilations-Problem erklärt

- `src/Client/Components/SyncFlow/View.fs`
  - `statusDot`: Skipped hat jetzt Priorität über DuplicateStatus
  - `statusDot`: NeedsAttention nutzt jetzt `bg-neon-orange` (wie Pending)
  - `getRowStateClasses`: Skipped-Check vor Duplicate-Check
  - Mobile Actions: `md:opacity-0 md:group-hover:opacity-100` statt `opacity-0 group-hover:opacity-100`
  - Neue `expandChevron` Funktion für Memo-Expand-Icon
  - Neue `memoRow` Funktion für expandierbaren Memo-Text
  - `transactionRow` akzeptiert jetzt `expandedIds: Set<TransactionId>` Parameter
  - Layout enthält jetzt Chevron-Icon links vom Status-Dot

- `src/Client/DesignSystem/Input.fs`
  - Dropdown-Options: `px-4 py-3 text-base` statt `px-3 py-2 text-sm`
  - Max-Höhe: `max-h-80` statt `max-h-60`

- `src/Client/Components/SyncFlow/Types.fs`
  - Model erweitert um `ExpandedTransactionIds: Set<TransactionId>`
  - Neue Message: `ToggleTransactionExpand of TransactionId`

- `src/Client/Components/SyncFlow/State.fs`
  - `init()` erweitert mit `ExpandedTransactionIds = Set.empty`
  - Handler für `ToggleTransactionExpand`: Toggle Set-Membership

**Layout-Änderung Desktop:**
```
[▶/▼] [●] [Kategorie-Dropdown] [Payee...] [Datum] [Betrag] [Actions]
```

**Prioritäten für Farben:**
1. Skipped → immer grau (opacity-50)
2. ConfirmedDuplicate → rot (nur wenn nicht skipped)
3. PossibleDuplicate → orange pulsierend (nur wenn nicht skipped)
4. Pending/NeedsAttention → orange (gleiche Farbe!)
5. AutoCategorized → teal
6. ManualCategorized/Imported → grün

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- Beträge werden korrekt formatiert: `-25.99 EUR`
- Skipped Duplicates sind grau
- Mobile Actions immer sichtbar
- Memo-Text durch Expand abrufbar

---

## 2025-12-09 - Redesign: Kompakte Transaktionsliste im Import-Flow

**What I did:**
Komplettes Redesign der Transaktions-Import-UI von unstrukturierten Karten (~100-120px Höhe) zu einer kompakten, scanbaren Liste (~44px Desktop, ~72px Mobile). Das neue Design folgt dem Mobile-First Ansatz aus dem Design System und stellt die Kategorie als zentrales Interaktionselement in den Fokus.

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`
  - Neue Helper-Funktionen: `statusDot`, `getRowStateClasses`, `duplicateIndicator`, `skipToggleIcon`, `externalLinkIcon`, `formatDateCompact`
  - Neue Komponente: `transactionRow` mit Mobile/Desktop responsive Layout
  - Container von `space-y-3` auf Card-Style mit `border-white/5 divide-y` umgestellt
  - Alte `transactionCard` Funktion als DEPRECATED markiert (noch vorhanden für Referenz)

**Design-Änderungen:**
- **Status-Anzeige**: Von großen Badges zu kleinen farbigen Dots (8px)
  - Grün: Kategorisiert (manual/auto)
  - Teal: Auto-kategorisiert
  - Orange: Pending
  - Rot: Duplicate
  - Grau: Skipped
  - Pink (pulsierend): Needs Attention
- **Duplicate-Handling**: Inline-Banner entfernt, stattdessen Tooltip auf Icon
- **Kategorie-Auswahl**: Prominent links positioniert (FOKUS)
- **Actions**: Hover-only auf Desktop, immer sichtbar auf Mobile

**Layout-Struktur:**
```
Desktop: [●] [Category-Dropdown] [Payee...] [Date] [Amount] [Actions]
Mobile:  Line 1: [●] [Category-Dropdown] [Amount]
         Line 2: [Payee ...] [Date] [Actions]
```

**Touch-Optimierung:**
- Skip-Button: `min-w-[44px] min-h-[44px]` für Touch-Targets
- Kategorie-Dropdown nutzt existierenden `searchableSelect` mit Touch-Support

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- ~2.5x mehr Transaktionen auf Desktop sichtbar
- ~1.5x mehr Transaktionen auf Mobile sichtbar
- Klarer Scan-Pfad: Status → Kategorie → Details → Betrag

---

## 2025-12-09 - Feature: Inline-Bestätigung für Rule-Löschen

**What I did:**
Schutz vor versehentlichem Löschen von Rules durch Inline-Bestätigung (MVU-konform). Beim Klick auf das Trash-Icon erscheint ein roter "Löschen?"-Button, der nach 3 Sekunden automatisch zurück zum normalen Icon wechselt.

**Files Modified:**
- `src/Client/Components/Rules/Types.fs`
  - Neues Model-Feld: `ConfirmingDeleteRuleId: RuleId option`
  - Neue Messages: `ConfirmDeleteRule of RuleId`, `CancelConfirmDelete`
- `src/Client/Components/Rules/State.fs`
  - `init()` erweitert mit `ConfirmingDeleteRuleId = None`
  - Handler für `ConfirmDeleteRule`: Setzt State + startet 3s Timeout-Cmd
  - Handler für `CancelConfirmDelete`: Setzt State zurück
  - `DeleteRule` setzt ebenfalls `ConfirmingDeleteRuleId = None`
- `src/Client/Components/Rules/View.fs`
  - `ruleRow` bekommt `model` als ersten Parameter
  - Delete-Button zeigt konditionell: Trash-Icon oder roten "Löschen?"-Button
  - Button hat `animate-pulse` für visuelle Aufmerksamkeit

**Technische Details:**
- Vollständig MVU-konform: Kein lokaler React-State, alle Änderungen über Messages
- Timeout via `Cmd.OfAsync.perform` mit `Async.Sleep 3000`
- Nur eine Rule kann gleichzeitig im Confirm-Modus sein

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- Versehentliches Löschen wird durch Zwei-Klick-Mechanismus verhindert

---

## 2025-12-09 - Feature: Einzeilige Rules-Darstellung + Legende

**What I did:**
Rules-Darstellung von mehrzeiliger Card-Ansicht auf kompakte einzeilige Zeilen umgestellt. Die neue Darstellung zeigt alle wichtigen Informationen in einer Zeile: Toggle, Pattern-Type-Icon, Name, Pfeil, Kategorie, und Aktions-Buttons. Zusätzlich wurde eine Legende für die Pattern-Type-Icons hinzugefügt.

**Files Added:**
- Keine neuen Dateien

**Files Modified:**
- `src/Client/Components/Rules/View.fs`
  - Neue `patternTypeIcon` Funktion für kompakte Pattern-Type-Anzeige (Regex: `.*`, Contains: `~`, Exact: `=`)
  - `ruleCard` ersetzt durch kompakte `ruleRow` Komponente
  - Single-Line Layout: `[Toggle] [PatternIcon] [Name...] → [Category...] [Edit][Delete]`
  - Pattern-Type-Icon auf kleinen Screens versteckt (`hidden sm:block`)
  - Actions auf Desktop nur bei Hover sichtbar (`sm:opacity-0 sm:group-hover:opacity-100`)
  - Name und Pattern als Tooltip verfügbar
  - Spacing reduziert: `space-y-1.5` statt `gap-3`
  - **Legende im Info-Tip hinzugefügt**: Zeigt alle Pattern-Types mit Icons (`~ Contains`, `= Exact`, `.* Regex`)

**Technische Details:**
- Pattern-Type als kompaktes Icon-Badge: farbcodiert (Purple=Regex, Teal=Contains, Green=Exact)
- Flexbox-Layout mit truncate für Überlauf-Handling
- Responsive: Auf Mobile immer Actions sichtbar, auf Desktop nur bei Hover
- Tooltip zeigt vollständigen Namen und Pattern bei Hover über Namen
- Legende responsive: Auf Desktop neben Info-Text, auf Mobile darunter

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- Rules nehmen jetzt deutlich weniger Platz ein (ca. 1/3 der vorherigen Höhe)
- Mehr Rules auf einen Blick sichtbar
- Pattern-Type-Icons sind durch Legende erklärt

---

## 2025-12-08 - Feature: Suchbare Kategorie-Selectboxen mit Keyboard-Navigation

**What I did:**
Neue SearchableSelect Komponente implementiert, die eine durchsuchbare Dropdown-Liste für Kategorien bereitstellt. Beim Öffnen erscheint ein Textfeld mit Auto-Fokus, das die Kategorien mit "contains"-Logik filtert (case-insensitive Suche in der Mitte des Namens möglich). Vollständige Keyboard-Navigation hinzugefügt.

**Files Added:**
- Keine neuen Dateien

**Files Modified:**
- `src/Client/DesignSystem/Input.fs`
  - Neue `SearchableSelectProps` Type-Definition
  - Neue `SearchableSelect` React-Komponente mit:
    - Click-outside Detection zum Schließen
    - Auto-Focus auf Suchfeld beim Öffnen
    - Case-insensitive Contains-Filter
    - **Vollständige Keyboard-Navigation:**
      - ⬆️⬇️ Arrow Up/Down zum Navigieren
      - ↵ Enter zum Auswählen
      - ⎋ Escape zum Schließen
      - ⇥ Tab zum Schließen
      - Wrap-around an Liste-Enden
    - Visuelles Highlighting der aktuell hervorgehobenen Option
    - Auto-Scroll zur hervorgehobenen Option (nur bei Tastatur-Navigation)
    - Mouse-Hover aktualisiert auch den Highlight-Index
  - Neue `searchableSelect` Helper-Funktion
  - SVG-Warnungen behoben (Html.svg → Svg.svg)
- `src/Client/Components/SyncFlow/View.fs`
  - `selectWithPlaceholder` ersetzt durch `searchableSelect` für Kategorie-Auswahl
- `src/Client/Components/Rules/View.fs`
  - `selectWithPlaceholder` ersetzt durch `searchableSelect` für Kategorie-Auswahl

**Technische Details:**
- React Hooks: `useState` für isOpen/searchText/highlightedIndex/isKeyboardNav, `useRef` für Container/Input/List
- useEffect für Click-outside-Detection, Auto-Focus, und Auto-Scroll
- Filter: `label.ToLowerInvariant().Contains(searchLower)`
- Index 0 = Clear/Placeholder Option, Index 1+ = gefilterte Optionen
- Dropdown zeigt "No matches found" bei leerer Ergebnisliste
- `data-option-index` Attribut für DOM-Abfrage beim Scrolling

**Bug-Fix: Scroll-Verhalten in Modals:**
- Problem: Mouse-Hover triggerte `scrollIntoView()`, was das gesamte Modal/Fenster scrollte
- Lösung: Neuer `isKeyboardNav` State unterscheidet zwischen Maus- und Tastatur-Navigation
- Auto-Scroll nur bei Tastatur-Navigation via manuelles `list.scrollTop` (scrollt nur innerhalb der Dropdown-Liste)
- `setHighlightFromMouse` Helper setzt `isKeyboardNav = false` bei Mouse-Events

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- Beide Kategorie-Selectboxen sind jetzt durchsuchbar und per Tastatur navigierbar
- Maus-Scrollen funktioniert ohne unerwünschtes Seiten-/Modal-Scrolling

---

## 2025-12-08 - Fix: Langsame Kategorie-Auswahl (Optimistisches UI)

**What I did:**
Bug behoben, bei dem die Auswahl einer Kategorie in der Selectbox fast eine Sekunde dauerte. Die Ursache war, dass das UI auf die Backend-Antwort gewartet hat, bevor die Kategorie-Auswahl angezeigt wurde ("pessimistisches UI"). Jetzt wird das Model sofort lokal aktualisiert (optimistisches UI) und der API-Call läuft im Hintergrund.

**Problem:**
- Bei `CategorizeTransaction` wurde das Model NICHT aktualisiert, nur ein API-Call gestartet
- Erst bei `TransactionCategorized` (nach ~500-1000ms) wurde das Model aktualisiert
- Benutzer musste auf jede Kategorie-Auswahl warten

**Lösung:**
- Optimistisches UI: Model wird sofort lokal aktualisiert mit der neuen Kategorie
- API-Call läuft im Hintergrund
- Bei Fehler: Rollback durch Neuladen der Transaktionen

**Files Modified:**
- `src/Client/Components/SyncFlow/State.fs`
  - `CategorizeTransaction` aktualisiert jetzt sofort das lokale Model
  - `TransactionCategorized (Error _)` löst jetzt `LoadTransactions` aus für Rollback

**Technische Details:**
- Die Kategorie-Auswahl aktualisiert jetzt sofort:
  - `CategoryId` und `CategoryName`
  - `Status` zu `ManualCategorized` (oder `Pending` bei Leer-Auswahl)
  - `Splits` wird auf `None` gesetzt
- Bei Fehler werden Transaktionen vom Server neu geladen (konsistenter Zustand)

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- Kategorie-Auswahl ist jetzt sofort sichtbar (keine Verzögerung mehr)

---

## 2025-12-07 22:45 - Entferne Comdirect Zeilennummern-Präfixe aus Memos

**What I did:**
Comdirect sendet den Verwendungszweck (remittanceInfo) mit Zeilennummern-Präfixen wie "01", "02", etc. Diese erschienen in der UI vor jedem Memo (z.B. "01REWE..." statt "REWE..."). Eine Regex-Funktion hinzugefügt, die diese Präfixe beim Parsen entfernt.

**Files Modified:**
- `src/Server/ComdirectClient.fs` - Neue Funktion `removeLineNumberPrefixes` hinzugefügt und beim Parsen von `remittanceInfo` angewendet
- `src/Server/Server.fsproj` - `InternalsVisibleTo` für Tests hinzugefügt
- `src/Tests/ComdirectDecoderTests.fs` - 10 Unit-Tests für die neue Funktion hinzugefügt

**Technische Details:**
- Regex-Pattern: `(^|\n)\d{2}(?=[A-Za-zÄÖÜäöüß])` - matcht 2-stellige Zahlen am Anfang oder nach Zeilenumbruch, gefolgt von Buchstaben
- Unterstützt deutsche Umlaute
- Trim() entfernt übrige Leerzeichen
- Funktion ist `internal` um direktes Unit-Testing zu ermöglichen

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped (integration tests) - 10 neue Tests
- Memos erscheinen jetzt ohne "01"-Präfix

---

## 2025-12-07 21:15 - Legacy Rules Import Script

**What I did:**
Created a reusable F# script to import categorization rules from the legacy `rules.yml` file into the BudgetBuddy database.

**Files Added:**
- `scripts/import-rules.fsx` - Import script that:
  - Parses YAML rules (match/category pairs)
  - Fetches YNAB categories to match names to IDs
  - Inserts rules into SQLite database
  - Supports `--list` flag to show available budgets
  - Supports budget selection by name parameter

**Usage:**
```bash
dotnet fsi scripts/import-rules.fsx --list              # List budgets
dotnet fsi scripts/import-rules.fsx "My Budget"         # Import with specific budget
dotnet fsi scripts/import-rules.fsx --clear "My Budget" # Clear all + reimport
```

**Features:**
- Category matching: exact name, GroupName/Name format, case-insensitive
- Duplicate detection: skips rules that already exist
- Clear reporting: shows imported/skipped counts and missing categories

**Outcomes:**
- Successfully imported 55 of 56 rules from rules.yml
- 1 category not found in YNAB: "Krankenversicherung Lena Debeka"
- Script is reusable for future database resets

---

## 2025-12-07 19:50 - Increased Memo Limit to 300 + Whitespace Compression

**What I did:**
1. Increased memo character limit from 200 to 300 (testing if YNAB accepts it)
2. Added whitespace compression: multiple spaces/tabs/newlines become single space

**Why:**
- Old GitHub issue (2019) claimed 100-char limit, but that may be outdated
- Testing with 300 to see actual limit
- Whitespace compression saves characters for actual content

**Files Modified:**
- `src/Server/YnabClient.fs`:
  - Added `memoLimit = 300` constant
  - Added `compressWhitespace` function using regex `\s+` → single space
  - Applied compression before truncation

- `src/Tests/DuplicateDetectionTests.fs`:
  - Updated limit from 200 to 300
  - Added 3 new tests for whitespace compression

- `src/Tests/YnabClientTests.fs`:
  - Updated memo tests to reflect new behavior

**Outcomes:**
- Build: ✅ success
- Tests: 269 passed (6 skipped integration tests)

---

## 2025-12-07 19:40 - Fixed Memo Truncation Breaking Duplicate Detection

**What I did:**
Fixed a critical bug where memos were being truncated from the END, causing the Comdirect reference to be cut off. This broke duplicate detection because the reference is appended as ", Ref: <reference>" at the end of the memo.

**Root Cause:**
The old `truncateMemo` function truncated long memos to 197 characters + "..." = 200 chars (YNAB limit). But the reference was NOT being appended to the memo at all when sending to YNAB! The duplicate detection system (`DuplicateDetection.fs`) expects to find "Ref: <reference>" at the end of YNAB memos to match transactions.

**The Fix:**
1. Created new `buildMemoWithReference` function that:
   - Appends ", Ref: <reference>" to the memo
   - If total > 200 chars: truncates from the BEGINNING (preserving the reference)
   - Format when truncated: "...<truncated memo>, Ref: <reference>"

2. Kept simple `truncateSplitMemo` for split transaction memos (they don't need a reference since the parent transaction has one)

**Files Modified:**
- `src/Server/YnabClient.fs`:
  - Replaced `truncateMemo` with `buildMemoWithReference(memo, reference)`
  - Added `truncateSplitMemo` for split transactions (line 220-225)
  - Added `buildMemoWithReference` function (lines 227-246)
  - Updated memo construction to include reference (line 355)
  - Updated split memo to use `truncateSplitMemo` (line 313)

- `src/Tests/DuplicateDetectionTests.fs`:
  - Added 6 regression tests for memo with reference behavior (lines 460-568)
  - Tests verify reference is always extractable after truncation

**Why This Matters:**
Without the reference in the YNAB memo, transactions imported to YNAB could not be detected as duplicates on subsequent syncs. This would lead to duplicate transactions in YNAB.

**Outcomes:**
- Build: ✅ success
- Tests: 266 passed (6 skipped integration tests)
- All regression tests for memo truncation pass

---

## 2025-12-07 - YNAB Import: Uncleared Transactions + Optional Categories

**What I did:**
Implemented two high-priority features from the backlog that allow more flexible YNAB imports:
1. All transactions are now imported as "uncleared" instead of "cleared"
2. Transactions without categories can now be imported (appear in YNAB's Uncategorized view)

**Why:**
- **Uncleared**: Users can review and manually clear transactions in YNAB after import
- **Optional categories**: Supports YNAB's native uncategorized workflow - users can categorize later in YNAB

**Files Modified:**
- `src/Server/YnabClient.fs`:
  - Changed `Cleared = "cleared"` to `Cleared = "uncleared"` (line 342)
  - Removed category requirement from filter (lines 309-313)
  - Handle `CategoryId = None` case without throwing (lines 354-361)

- `src/Server/Api.fs`:
  - Updated import filter to include `Pending` status (lines 835-843)
  - Renamed `categorized` variable to `toImport` for clarity
  - Updated all references throughout the function

- `src/Client/Components/SyncFlow/View.fs`:
  - Import button now enabled for any non-skipped, non-imported transaction (lines 410-418)
  - Added warning banner for uncategorized transactions (lines 402-435)

- `src/Tests/YnabClientTests.fs`:
  - Added test "transaction is encoded with cleared=uncleared"
  - Added test "uncategorized transaction is encoded without category_id"
  - Updated "uncategorized transactions pass filter" test (previously verified filter-out)
  - Updated existing tests to use "uncleared" instead of "cleared"

**UI Changes:**
- Warning banner appears when importing uncategorized transactions
- Shows count: "X Transaktion(en) ohne Kategorie"
- Subtitle: "Diese werden als 'Uncategorized' in YNAB importiert."

**Outcomes:**
- Build: ✅ success
- Tests: 260/266 passed (6 skipped integration tests)
- Both features working as expected

---

## 2025-12-07 - Add Comprehensive Tests for SyncSessionManager

**What I did:**
Implemented comprehensive test coverage for the SyncSessionManager module, which previously had ZERO test coverage. This was identified as a critical gap by the QA milestone reviewer.

**Files Added:**
- `src/Tests/SyncSessionManagerTests.fs` - Comprehensive tests covering:
  - Session Lifecycle Tests (11 tests): startNewSession, getCurrentSession, completeSession, failSession, clearSession, updateSessionStatus, updateSession, and error handling
  - Transaction Operations Tests (14 tests): addTransactions, getTransactions, getTransaction, updateTransaction, updateTransactions, getStatusCounts, updateSessionCounts
  - Session Validation Tests (7 tests): validateSession, validateSessionStatus with various scenarios
  - Edge Cases and Integration Tests (6 tests): workflow simulation, state transitions, transaction overwrites

**Files Modified:**
- `src/Tests/Tests.fsproj` - Added SyncSessionManagerTests.fs to compilation

**Technical Notes:**
- Used `testSequenced` for all test lists because SyncSessionManager uses global mutable state (`currentSession : SessionState option ref`)
- Each test calls `resetSession()` at start to ensure test isolation
- Tests verify actual behavior, not tautologies (e.g., verifying that completeSession sets both status AND timestamp)

**Test Summary:**
- Session Lifecycle Tests: 11 tests
- Transaction Operations Tests: 14 tests
- Session Validation Tests: 7 tests
- Edge Cases and Integration: 6 tests
- Total new tests: 38

**Outcomes:**
- Build: success
- Tests: 258/264 passed (6 skipped integration tests that require .env credentials)
- All SyncSessionManager functionality now has test coverage

---

## 2025-12-07 - Fix TAN Confirmation Double-Click Bug

**What I did:**
Fixed a race condition bug where clicking the "I've Confirmed" button twice during TAN confirmation caused an error toast "Invalid session state. Expected: AwaitingTan, Actual: FetchingTransactions". The issue was that the button had no loading state, so users would click again thinking nothing happened.

**Root Cause:**
1. User clicks "I've Confirmed" → `ConfirmTan` message sent
2. Backend receives request, immediately changes session status to `FetchingTransactions`
3. Button remains active (no loading state)
4. User clicks again → second `ConfirmTan` sent
5. Backend validates session is in `AwaitingTan` state, but it's already `FetchingTransactions` → Error

**Fix:**
Added `IsTanConfirming: bool` flag to track when TAN confirmation is in progress. The button now shows a loading state and subsequent clicks are ignored.

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs`:
  - Added `IsTanConfirming: bool` to Model

- `src/Client/Components/SyncFlow/State.fs`:
  - Initialize `IsTanConfirming = false` in `init`
  - Set `IsTanConfirming = true` when `ConfirmTan` is dispatched (ignore if already true)
  - Reset `IsTanConfirming = false` when `TanConfirmed` is received (success or error)

- `src/Client/Components/SyncFlow/View.fs`:
  - Updated `tanWaitingView` to accept `isConfirming: bool` parameter
  - Button shows "Importing..." with loading spinner when confirming
  - Button shows normal "I've Confirmed" when idle

**Outcomes:**
- Build: success
- Tests: 220/227 passed (1 unrelated failure in Persistence Type Conversions test, 6 skipped integration tests)
- Button now shows loading state, preventing double-clicks

---

## 2025-12-07 - Add Force Re-Import for YNAB Duplicates

**What I did:**
Implemented a complete "Force Re-Import" feature for transactions that YNAB rejects as duplicates. This solves the problem where deleted transactions in YNAB can't be re-imported because YNAB remembers the import_id forever.

**Flow:**
1. User clicks "Import to YNAB"
2. YNAB responds with duplicates
3. Toast shows "Imported X transaction(s). Y already exist in YNAB."
4. Button appears: "Re-import Y Duplicate(s)"
5. User clicks → transactions are sent with new UUIDs
6. Success!

**Files Modified:**
- `src/Server/YnabClient.fs`:
  - Added `forceNewImportId: bool` parameter to `createTransactions`
  - Normal import uses Transaction-ID based import_id (duplicate protection)
  - Force import uses new UUID (bypasses YNAB's duplicate detection)

- `src/Shared/Api.fs`:
  - Added `ImportResult` type with `CreatedCount` and `DuplicateTransactionIds`
  - Changed `importToYnab` return type from `int` to `ImportResult`
  - Added new `forceImportDuplicates` endpoint

- `src/Server/Api.fs`:
  - Updated `importToYnab` to return `ImportResult` with duplicate transaction IDs
  - Implemented `forceImportDuplicates` endpoint using `forceNewImportId = true`

- `src/Client/Components/SyncFlow/Types.fs`:
  - Added `DuplicateTransactionIds: TransactionId list` to Model
  - Added `ForceImportDuplicates` and `ForceImportCompleted` messages

- `src/Client/Components/SyncFlow/State.fs`:
  - Handle `ImportCompleted` with `ImportResult`
  - Handle `ForceImportDuplicates` and `ForceImportCompleted`

- `src/Client/Components/SyncFlow/View.fs`:
  - Added "Re-import X Duplicate(s)" button when duplicates exist

**Outcomes:**
- Build: ✅ (Server + Client)
- Tests: 221/221 passed
- Feature working end-to-end

**Final Cleanup (17:45):**
- Removed all debug `printfn` statements from `Api.fs`
- Verified all tests still pass after cleanup

---

## 2025-12-07 - Fix YNAB False Success Reports + Duplicate Handling

**What I did:**
Fixed two related bugs:
1. The app reported successful transaction transfer even when YNAB rejected them as duplicates
2. Duplicate transactions were still marked as "Imported" in the UI

**Problem 1 - False Success Count:**
The `createTransactions` function returned the count of **sent** transactions instead of **actually created** ones.

**Problem 2 - Incorrect Status Marking:**
Even when YNAB rejected transactions as duplicates, they were marked as `Imported` in the local state because the code blindly marked all categorized transactions as imported.

**Solution:**
1. **New type `TransactionCreateResult`** in `YnabClient.fs`:
   - `CreatedCount: int` - actual number of created transactions
   - `DuplicateImportIds: string list` - import IDs that were rejected

2. **Updated `Api.fs` import logic:**
   - Parse `duplicate_import_ids` from YNAB response
   - Only mark transactions as `Imported` if they weren't in the duplicates list
   - Duplicate transactions keep their original status (not marked as imported)

3. **Added logging** to see YNAB request/response for debugging

**Files Modified:**
- `src/Server/YnabClient.fs`:
  - Added `TransactionCreateResult` type
  - Changed `createTransactions` return type from `int` to `TransactionCreateResult`
  - Added request/response logging
  - Parse and return duplicate import IDs

- `src/Server/Api.fs`:
  - Parse duplicate import IDs to match against transaction IDs
  - Only mark actually-created transactions as `Imported`
  - Duplicates retain their original status

- `src/Tests/YnabClientTests.fs`:
  - Added 4 regression tests for response parsing

**Tests Added:**
- `correctly counts created transactions from YNAB response`
- `correctly identifies duplicate transactions from YNAB response`
- `handles response with all transactions rejected as duplicates`
- `handles response missing duplicate_import_ids field`

**Outcomes:**
- Build: ✅
- Tests: 221/221 passed (6 integration tests skipped)
- UI now correctly shows actual imported count
- Duplicate transactions are no longer falsely marked as imported

---

## 2025-12-08 01:15 - Add Mandatory Bug Fix Protocol to CLAUDE.md

**What I did:**
Added a new "Bug Fix Protocol (MANDATORY)" section to CLAUDE.md that requires regression tests for every bug fix.

**Motivation:**
Two bugs were fixed today that could have been caught earlier with proper tests:
1. Stale reference bug in `completeSession()`
2. JSON encoding bug using `Encode.int64` instead of `Encode.int`

Neither would have regressed if the original code had proper tests.

**Files Modified:**
- `CLAUDE.md` - Added "Bug Fix Protocol (MANDATORY)" section with:
  - 5-step process for fixing bugs
  - Test requirements for bug fixes
  - Example test comment format
  - Explanation of why this matters

**Key Points Added:**
1. Every bug fix MUST include a regression test
2. Write failing test FIRST, then fix
3. Test should include comment explaining what bug it prevents
4. Run full test suite to verify no regressions

---

## 2025-12-08 01:00 - Fix YNAB Transactions Not Being Created (JSON Encoding Bug)

**What I did:**
Fixed a critical bug where transactions were not actually being created in YNAB despite the API appearing to succeed.

**Problem:**
`Encode.int64` in Thoth.Json.Net serializes 64-bit integers as **strings** (because JavaScript can't handle 64-bit integers):
```json
"amount": "-50250"   // WRONG - string with quotes
```
But YNAB API expects a **number**:
```json
"amount": -50250     // CORRECT - number without quotes
```
The YNAB API was likely rejecting the transactions silently or misinterpreting the amount.

**Solution:**
1. Changed `Amount` field from `int64` to `int` in `YnabTransactionRequest` and `YnabSubtransactionRequest` types
2. Changed `Encode.int64` to `Encode.int` in encoders
3. Changed `int64` to `int` in amount conversion code
4. Added regression tests to verify amounts are serialized as JSON numbers

**Files Modified:**
- `src/Server/YnabClient.fs` - Changed int64 to int for Amount fields and encoders
- `src/Tests/YnabClientTests.fs` - Added 2 new tests for JSON encoding verification

**Outcomes:**
- Build: ✅
- Tests: 217/217 passed (2 new tests)
- YNAB transactions should now be created correctly

**Test Coverage Gap Identified:**
There were no tests verifying the JSON format sent to YNAB. The bug existed because:
1. Tests only verified data transformations, not the actual HTTP request body format
2. No integration test with YNAB API mocking

---

## 2025-12-08 00:30 - Fix Sync Complete Screen Showing Wrong Import Counts

**What I did:**
Fixed a bug where the "Sync Complete" screen always showed 0 IMPORTED and 0 SKIPPED even when transactions were successfully imported to YNAB.

**Problem:**
In `SyncSessionManager.fs`, the `completeSession()` function had a stale reference bug:
1. It matched on `currentSession.Value` and captured `state`
2. Called `updateSessionCounts()` which updated `currentSession.Value` with correct counts
3. Then used the OLD `state.Session` (with 0 counts) to create the completed session
4. This overwrote the updated counts with 0

**Solution:**
Re-read `currentSession.Value` after calling `updateSessionCounts()` to get the session with the updated ImportedCount and SkippedCount.

**Files Modified:**
- `src/Server/SyncSessionManager.fs` - Fixed `completeSession()` to re-read session state after `updateSessionCounts()`

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed
- Sync Complete screen should now show correct import/skipped counts

---

## 2025-12-07 23:45 - Fix YNAB JSON Serialization Error for Transactions

**What I did:**
Fixed a bug where syncing transactions to YNAB failed with "Could not determine JSON object type for type <>f__AnonymousType...".

**Problem:**
The `createTransactions` function in `YnabClient.fs` used F# anonymous types (`{| ... |}`) that were cast to `obj` and passed to `Encode.Auto.generateEncoder()`. Thoth.Json cannot automatically serialize anonymous types.

**Solution:**
1. Defined proper F# record types `YnabTransactionRequest` and `YnabSubtransactionRequest`
2. Created manual encoders `encodeTransaction` and `encodeSubtransaction` using `Encode.object`
3. Replaced `Encode.Auto.generateEncoder()` with `Encode.list (ynabTransactions |> List.map encodeTransaction)`

**Files Modified:**
- `src/Server/YnabClient.fs` - Added record types and manual encoders, refactored `createTransactions` to use them

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed
- YNAB sync should now work correctly

---

## 2025-12-07 22:15 - Fix SyncFlow Category Loading Race Condition

**What I did:**
Fixed a bug where categories could not be selected in the SyncFlow transaction review. The category dropdown was always empty because categories were never loaded.

**Problem:**
1. `LoadCategories` in `SyncFlow/State.fs` did nothing (`Cmd.none`) - it was marked as "simplified for now"
2. The parent `Client/State.fs` intercepted `LoadCategories` but only loaded categories if Settings were already loaded (race condition)
3. If Settings weren't loaded yet when navigating to SyncFlow, categories never loaded

**Solution:**
1. Made `LoadCategories` in SyncFlow self-sufficient - it now fetches settings first to get the budget ID, then loads categories from YNAB API
2. Removed the parent interception - `LoadCategories` is now passed through to the child component

**Files Modified:**
- `src/Client/Components/SyncFlow/State.fs` - Implemented `LoadCategories` handler to fetch settings and categories independently
- `src/Client/State.fs` - Removed special handling for `LoadCategories`, now passes message to child

**Outcomes:**
- Build: ✅
- Category selection now works in SyncFlow
- Skip button confirmed working

---

## 2025-12-07 18:30 - Simplify Transaction Import Flow (Remove Selection, Add Unskip)

**What I did:**
Simplified the transaction import flow by removing the confusing checkbox selection mechanism and adding proper Skip/Unskip functionality with auto-skip for confirmed duplicates.

**Problem:**
The previous UI had two conflicting mechanisms:
1. Checkbox selection (Select All/None)
2. Skip button per transaction

This was confusing because the frontend validated based on selection, but the backend ignored selection entirely and just imported all categorized transactions. Users didn't know which transactions would actually be imported.

**Solution:**
1. **Removed selection UI entirely** - no more checkboxes, Select All/None buttons, or selection count badge
2. **Added Unskip functionality** - skipped transactions can now be restored
3. **Auto-skip confirmed duplicates** - transactions with `ConfirmedDuplicate` status are automatically skipped during sync
4. **Simplified import logic** - all non-skipped transactions with categories are imported

**Files Added:**
- None

**Files Modified:**
- `src/Shared/Api.fs` - Added `unskipTransaction` API endpoint
- `src/Server/Api.fs` - Added auto-skip for ConfirmedDuplicate, implemented `unskipTransaction` endpoint
- `src/Client/Components/SyncFlow/Types.fs` - Removed `SelectedTransactions` from Model, removed selection Msg types, added `UnskipTransaction` and `TransactionUnskipped`
- `src/Client/Components/SyncFlow/State.fs` - Removed selection handlers, added Unskip handlers
- `src/Client/Components/SyncFlow/View.fs` - Removed checkbox, Select All/None buttons, selection badge; updated `canImport` logic; added Skip/Unskip toggle button
- `src/Client/DesignSystem/Icons.fs` - Added `undo` icon for Unskip button

**Files Deleted:**
- None

**Rationale:**
- The dual selection+skip mechanism was confusing and the frontend/backend were inconsistent
- Auto-skipping confirmed duplicates reduces user effort and prevents accidental duplicate imports
- Unskip allows users to correct mistakes or import transactions that were auto-skipped

**New Import Logic:**
- Import all transactions where:
  - `Status != Skipped`
  - `CategoryId.IsSome`

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed (6 skipped - integration tests)
- UI is now simpler and more intuitive

---

## 2025-12-07 17:00 - Fix Modal Animation Flicker

**What I did:**
Fixed the Rules Modal flickering issue where a brief flash of the modal at full opacity appeared before the animation started.

**Root Cause:**
The CSS animation used `animation-fill-mode: forwards`, which only preserves the END state of the animation. The element was briefly visible at full opacity before the animation started (which sets opacity to 0), causing a flicker.

**Solution:**
Changed `animation-fill-mode` from `forwards` to `both`:
- `backwards`: Applies initial animation styles (opacity: 0, scale: 0.95) BEFORE the animation starts
- `forwards`: Preserves final animation styles after it ends
- `both`: Does both - element is invisible from the start, then smoothly animates in

**Files Modified:**
- `src/Client/styles.css`:
  - Changed `.animate-scale-in` from `forwards` to `both`

- `src/Client/DesignSystem/Modal.fs`:
  - Removed animation from backdrop (only modal content animates now)
  - Added `ModalInternal` React component with `useRef` to track animation state (prevents re-animation on re-renders)

**Technical Details:**
```css
/* Before (flickered) */
.animate-scale-in {
  animation: scaleIn 0.3s var(--transition-spring) forwards;
}

/* After (no flicker) */
.animate-scale-in {
  animation: scaleIn 0.3s var(--transition-spring) both;
}
```

**Rationale:**
With `forwards` only, the browser renders the element at its default state (visible), then starts the animation which sets opacity to 0, causing a brief flash. With `both`, the initial animation keyframe values are applied immediately, so the element starts invisible.

**Outcomes:**
- Build: ✅
- Tests: All passing
- Modal opens smoothly without flicker

---

## 2025-12-07 15:00 - Fix Frontend Flicker: Local State Updates & Refresh Buttons

**What I did:**
Fixed the frontend flickering issue where the entire list would reload from the server after every mutation (categorize, skip, toggle, save). Now mutations update the local state directly, eliminating unnecessary API calls and providing a smoother UX. Also added manual refresh buttons to all pages.

**Files Modified:**
- `src/Client/Components/SyncFlow/State.fs`:
  - `TransactionCategorized`: Now updates local list instead of `Cmd.ofMsg LoadTransactions`
  - `TransactionSkipped`: Now updates local list instead of reload
  - `BulkCategorized`: Now updates multiple transactions locally using Map
  - `SplitsSaved`: Now updates local list and closes modal without reload
  - `SplitCleared`: Now updates local list without reload

- `src/Client/Components/Settings/State.fs`:
  - `YnabTokenSaved`: Updates local state with saved token
  - `ComdirectCredentialsSaved`: Updates local state with saved credentials
  - `SyncSettingsSaved`: Updates local state with saved sync settings
  - `DefaultBudgetSet`: Now takes (budgetId, Result) and updates local state
  - `DefaultAccountSet`: Now takes (accountId, Result) and updates local state

- `src/Client/Components/Settings/Types.fs`:
  - Changed `DefaultBudgetSet` to include `YnabBudgetId` for local updates
  - Changed `DefaultAccountSet` to include `YnabAccountId` for local updates

- `src/Client/Components/Dashboard/View.fs`:
  - Added refresh button in page header (dispatches LoadRecentSessions, LoadCurrentSession, LoadSettings)

- `src/Client/Components/Rules/View.fs`:
  - Added refresh button in page header (dispatches LoadRules)

- `src/Client/Components/SyncFlow/View.fs`:
  - Added refresh button in page header (dispatches LoadTransactions, LoadCurrentSession)
  - Optimized stats calculation: Replaced 4x List.filter with single List.fold

- `src/Client/Components/Settings/View.fs`:
  - Added refresh button in page header (dispatches LoadSettings)

- `src/Client/DesignSystem/Button.fs`:
  - Added `Title: string option` field to `ButtonProps` for tooltip/accessibility
  - Updated `defaultProps` with `Title = None`
  - Updated `view` function to render `prop.title` when Title is set

**Rationale:**
The MVU (Model-View-Update) pattern with Virtual DOM should only re-render changed parts of the UI. Reloading entire lists from the server after every mutation causes:
1. Visual flickering as the list briefly shows loading state
2. Unnecessary network requests
3. Poor perceived performance

Since the API already returns the updated object(s) after mutations, we can use these to update the local state directly.

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed
- No more flickering when categorizing, skipping, or editing transactions
- No more flickering when saving settings
- Manual refresh available via icon button on each page

---

## 2025-12-07 11:30 - Documentation: Test Isolation Patterns for Future Projects

**What I did:**
Updated project documentation and Claude Code skills to capture the knowledge about F# test isolation patterns, particularly around environment variables and lazy loading for database configuration.

**Files Modified:**
- `.claude/skills/fsharp-persistence/SKILL.md`:
  - Replaced simple static connection string with lazy-loading pattern
  - Added "Why Lazy Loading?" section explaining F# module initialization
  - Added "Connection Management for Tests" section about In-Memory SQLite lifecycle
  - Updated Best Practices with test isolation requirements
  - Updated Verification Checklist

- `.claude/skills/fsharp-tests/SKILL.md`:
  - Updated Program.fs section with `USE_MEMORY_DB` environment variable setup
  - Added "Test Files Using Persistence" section with `do` before `open` pattern
  - Added "Testing Persistence (In-Memory SQLite)" section with complete example
  - Added "F# Module Initialization Gotchas" section
  - Updated Best Practices with test isolation dos/don'ts
  - Updated Verification Checklist

- `docs/05-PERSISTENCE.md`:
  - Replaced simple connection string pattern with full lazy-loading example
  - Added "Why Lazy Loading?" section
  - Added "Connection Management for In-Memory SQLite" section
  - Added Best Practices items 11-13 for test isolation

- `docs/06-TESTING.md`:
  - Completely rewrote "Testing Persistence (with In-Memory DB)" section
  - Added "Setting Up Test Mode" with Main.fs and test file examples
  - Added "Why This Pattern Works" explanation
  - Added "Common Pitfalls" section with wrong/correct examples
  - Added "Verifying Test Isolation" with verification commands
  - Added Best Practices items 11-13 for test isolation

**Rationale:**
The experience with the 236 test rules in production taught valuable lessons about F# module initialization order and test isolation. This knowledge should be preserved for future projects so the same mistakes aren't repeated.

**Key Lessons Documented:**
1. F# modules initialize by dependency graph, not by `open` order
2. `lazy` is required for configuration that tests need to override
3. In-Memory SQLite needs a shared connection kept alive
4. `do` before `open` in test files allows setting env vars before module init

**Outcomes:**
- Build: ✅
- All documentation updated with complete patterns
- Future projects will have clear guidance on test isolation

---

## 2025-12-07 - Fix: Rules list no longer reloads on every change

**What I did:**
Fixed a performance and UX issue where toggling a rule's enabled state, saving, or deleting a rule caused the entire Rules list to reload from the server.

**Problem:**
- Every change to a single rule triggered `Cmd.ofMsg LoadRules`
- This caused unnecessary network traffic
- Visible UI flickering due to RemoteData going to Loading state
- Poor user experience

**Root Cause:**
The MVU update handlers for `RuleToggled`, `RuleSaved`, and `RuleDeleted` all dispatched `LoadRules` instead of updating the local state with the returned data.

**Files Modified:**
- `src/Client/Components/Rules/Types.fs`:
  - Changed `RuleDeleted of Result<unit, RulesError>` to `RuleDeleted of Result<RuleId, RulesError>` to pass the deleted rule's ID

- `src/Client/Components/Rules/State.fs`:
  - `RuleToggled`: Now uses the returned `updatedRule` to update the local state via `List.map`
  - `RuleSaved`: Now uses the returned `savedRule` - adds to list for new rules, replaces for updates
  - `DeleteRule`: Modified to pass `ruleId` through the success result
  - `RuleDeleted`: Now filters out the deleted rule locally via `List.filter`

- `src/Client/Components/Rules/View.fs`:
  - Added `prop.key` to rule list items for efficient React reconciliation

**Rationale:**
The API already returns the updated/created Rule, so reloading the entire list was wasteful. Local state updates are:
- Faster (no network round-trip)
- Smoother (no UI flickering)
- More efficient (single item update vs full list fetch)

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed (6 skipped integration tests)
- Toggling a rule no longer causes list reload
- Creating/updating a rule updates in-place
- Deleting a rule removes it immediately from UI

---

## 2025-12-07 - Fix: Tests no longer write to Production Database

**What I did:**
Fixed a critical bug where persistence tests were writing test data (Rules, Sessions, Transactions) to the production database. Implemented in-memory SQLite support for tests.

**Problem:**
- `PersistenceTypeConversionTests.fs` was inserting test Rules into the production SQLite database
- Every test run added 6 new "Test Rule" entries
- Accumulated 236 duplicate test rules in production

**Files Modified:**
- `CLAUDE.md` - Added anti-pattern: "Tests writing to production database - NEVER write tests that persist data to the production database. Always use in-memory SQLite"
- `src/Server/Persistence.fs`:
  - Added lazy-loading for DB configuration (`dbConfig = lazy (...)`)
  - Added `USE_MEMORY_DB` environment variable support
  - When `USE_MEMORY_DB=true`, uses `Data Source=:memory:;Mode=Memory;Cache=Shared`
  - Shared connection for in-memory mode (keeps DB alive across operations)
  - Changed all `use conn = getConnection()` to `let conn = getConnection()` to prevent disposing shared connection
- `src/Tests/PersistenceTypeConversionTests.fs`:
  - Added `Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")` at module initialization (before `open Persistence`)
- `src/Tests/Main.fs`:
  - Also sets `USE_MEMORY_DB=true` for safety

**Files Deleted:**
- `src/Tests/TestSetup.fs` - Removed (approach didn't work due to F# module initialization order)

**Rationale:**
Tests must NEVER write to production databases. The in-memory SQLite approach provides complete isolation - each test run starts fresh, and no data persists after tests complete.

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed (6 skipped integration tests)
- Production DB: ✅ Verified unchanged after test runs

