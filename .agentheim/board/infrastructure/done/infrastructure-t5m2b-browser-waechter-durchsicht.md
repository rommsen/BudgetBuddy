---
id: infrastructure-t5m2b
title: "Browser-Tests auf das reduzieren, was nur der Browser prüfen kann, Logik wandert in MVU-Tests"
status: done
type: chore
context: infrastructure
created: 2026-11-02
completed:
depends_on: []
blocks: []
tags: [test, waechter, gate]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Roman: "500 sind mir eindeutig zu viele. Die Doktrin ist, dass nur Dinge, die wirklich nur im Frontend getestet werden können, auch darin laufen. Alles andere nicht. Wenn man Frontend-Logik testet, sollte das mit der MVU-Architektur auch anders gehen. Ich will nicht ständig flaky Tests."

Stand 2026-11-03: 437 Tests in `scripts/*.test.mjs`, davon 341 in 42 Dateien mit Browser oder gestartetem Frontend. 35-mal `waitForTimeout`, obwohl Doktrin Punkt 5 es verbietet. Die Node-Tests kosten rund 248 von rund 420 Sekunden Gate und sind die Quelle des Flackerns.

## What
Elmish trennt Zustand und Darstellung: `update` läuft unter .NET ohne Browser. Logik gehört dorthin, in reine Funktionen oder in Server-Tests. Im Browser bleibt, was nur dort messbar ist.

**Bleibt im Browser:**
- Layout und Maße (Breiten, Abschneiden, sticky, Viewport, Trefferflächen)
- Fokus, Tastatur-Ereignisse im echten DOM, Scrollen
- was nur im Fable-Kompilat gilt (Dezimalrechnung, Datumsformate)
- je Nutzerfluss genau ein Happy Path

**Wandert oder fällt weg:**
- welcher Text, welche Zahl, welcher Zustand nach einem Klick erscheint: Client-`update` unter .NET
- Sichtbarkeits- und Sperrlogik: reine Funktion aus dem View ziehen und unter .NET testen (Vorbild `assignKeyAction` und `amountKeyAction` in `Components/Budget/Moves.fs`), die View ruft sie nur auf
- Doppelungen zu vorhandenen .NET-Tests: löschen
- Tests für Hilfswerkzeuge (`screenshot-route`, `export-design-system`) auf das Nötigste kürzen, der Screenshot-Pfad ist kein Produkt

Dateien ohne Browser (`tailwind-class-guard`, `readme-delta-check`, `verify-lib`, `token-guard` und ähnliche) sind nicht Gegenstand dieser Task.

## Acceptance criteria
- [ ] Höchstens 100 Tests laufen im Browser. Die Zahl ist die Vorgabe des Owners im Sinn von "deutlich weniger". Liegt eine ehrliche Durchsicht darüber, begründet das Outcome je Datei, warum der Rest nur im Browser geht.
- [ ] Kein `waitForTimeout` in `scripts/*.test.mjs`.
- [ ] Ein Wächter im Gate (Vorschlag: `scripts/run-guards.test.mjs` oder eigener Test ohne Browser) zählt die Browser-Tests und `waitForTimeout` und wird rot, sobald die Obergrenze überschritten ist. Die Obergrenze steht als Konstante mit Kommentar im Wächter. Gegenbeweis: ein zusätzlicher Test oder ein `waitForTimeout` macht ihn rot.
- [ ] Doktrin geschärft in `.claude/rules/test-doctrine.md` Punkt 5 und Punkt 1: Browser-Wächter nur für das, was unter .NET nicht prüfbar ist, neue Logik-Tests im Browser sind verboten, die Obergrenze ist genannt. CLAUDE.md verweist weiter nur auf die Doktrin.
- [ ] Liste im Outcome je Datei: Tests vorher und nachher, Laufzeit vorher und nachher, was gezogen und was gelöscht wurde.
- [ ] Kein Verhalten verliert seine Absicherung: Je gezogener oder gelöschter Verhaltensgruppe nennt das Outcome die Mutante im Produktionscode, die den verbleibenden Test rot macht. Eine Gruppe ist ein fachliches Verhalten, nicht ein einzelner Test. Reine Lesart reicht nicht, gemessen wird stichprobenartig mindestens je Datei eine Mutante, bei `budget-page`, `move-money`, `ledger-filter-bar` und `rules-page-mobile` je Verhaltensgruppe.
- [ ] Produktionscode ändert nur seine Form, damit Logik testbar wird (reine Funktionen). Kein sichtbares Verhalten ändert sich.
- [ ] Der Test-Gesamtlauf ist dreimal hintereinander grün, ohne Wiederholung einzelner Tests.
- [ ] `TEST_CONCURRENCY` bleibt unverändert (die Messung ist eine eigene Aufgabe auf Romans Auftrag).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Löschen ist von Roman freigegeben (2026-11-03, "setze das selbständig um"), die alte Bedingung "Löschen nur nach Freigabe" entfällt damit. Die frühere Abhängigkeit von `budgeting-m3x7f` ist aufgehoben: Die Budget-Seite wird jetzt mit reduziert, die Tasks 3 bis 5 folgen der geschärften Doktrin.

`RetryTests.fs`: Das dortige Inventar liest Quelltext und ist nach infrastructure-h4q8v die einzige Absicherung der Aufrufstellen. Es ist kein Kandidat zum Aufräumen.

Client-`update` unter .NET geht nur für Nachrichten, deren Cmd nicht `Client.Api` berührt (Doktrin Punkt 1). Nach budgeting-c5w2n liegen auf der Budget-Seite die Lese-Cmds hinter einem Lambda. Andere Seiten, vor allem das Kassenbuch, brauchen dieselbe Form, bevor ihre Logik unter .NET prüfbar ist. Das ist Teil dieser Task und gehört in denselben Schritt wie das Ziehen.

Vorgehen in dieser Reihenfolge: erst die Doktrin und den Wächter mit der heutigen Zahl als Obergrenze, dann Datei für Datei von groß nach klein, zuletzt die Obergrenze absenken. Nach jeder großen Datei grün belegen.

Auffällig: `budget-page` 45, `screenshot-route` 32, `export-design-system` 19, `ledger-filter-bar` 19, `rules-page-mobile` 18. Prüfe, ob `static-serve` und `export-design-system` wirklich einen Browser brauchen.

## Verifier-Note Iteration 1

FAIL. `./verify.sh` war grün (dotnet 3510, node 280), der Produktionscode ändert nur seine Form, zehn Stichproben gelöschter Tests haben Ersatz.

- Wächter umgehbar: `it(`, `describe` mit `it`, einzeilige Schleife, `forEach`, umbenannter Import zählen 0. Eine Schleife mit `test(` am Zeilenanfang zählt 1. `t.test(` zählt keine Untertests.
- Verlorene Absicherung: Die Rückfrage "Bank trennen" klappt nach 3 s zurück. `Debounce.delayed 3000 AbortBankDisconnect` (`src/Client/Components/Settings/State.fs:374`) durch `Cmd.none` ersetzen macht keinen Test rot.
- Hinweis: In `budget-page` stecken noch Logik-Tests ("der Pfeil fehlt", "Tabelle mit Spaltenköpfen", "Zu verteilen negativ ohne Warnhinweis").

## Verifier-Note Iteration 2

FAIL. `./verify.sh` grün (dotnet 3521, node 278), Bank trennen und die gezogenen Logik-Tests sind belegt.

- Wächter: `scripts/browser-usage-hook.mjs` umhüllt nur `playwright`. Ein Start über `playwright-core` zählt 0.
- Hinweis: Ein Kindprozess ohne `NODE_OPTIONS` und `BB_BROWSER_USAGE_OWNER` wird nicht gezählt, ein Satz in Doktrin Punkt 5 genügt.


## Outcome

Die Browser-Tests sind von 360 zur Laufzeit (336 statisch gezählt, 42 Dateien) auf 118 in 37 Dateien gesunken. `waitForTimeout` ist von 35 auf 0 gefallen. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 3510 grün, node 280 grün). `npm run test:scripts` lief dreimal hintereinander grün, ohne dass einzelne Tests wiederholt wurden (181 s, 179 s, 179 s). `TEST_CONCURRENCY` ist unverändert, `RetryTests.fs` ist nicht aufgeräumt: Das Inventar bekam nur `Components/SyncFlow/State.fs` als neue Heimat der gelöschten Abbruch-Tests aus dem Import.

**Wächter und Doktrin**
- `scripts/browser-test-budget.test.mjs` zählt statisch die Tests in Dateien mit Playwright-Import, dynamischem Import oder `startFrontend(` und dazu `waitForTimeout` in allen `scripts/*.test.mjs`. Konstanten: `MAX_BROWSER_TESTS = 118`, `MAX_FIXED_WAITS = 0`. Ein eingerückter `test(`-Aufruf in einer Browser-Datei (Schleife) ist ein Verstoß, weil die statische Zählung ihn nur einmal sähe. Gegenbeweise sind im Wächter eingebaut. Zusätzlich von Hand an einer echten Datei geprüft: Ein zusätzlicher Test in `ledger-memo.test.mjs` meldet "119 Browser-Tests, erlaubt sind 118", ein zusätzliches `waitForTimeout` meldet "1 feste Wartezeiten, erlaubt sind 0".
- `.claude/rules/test-doctrine.md`: Punkt 1 nennt Cmds hinter einem Lambda und reine Funktionen für Sichtbarkeits- und Sperrlogik, und verbietet Browser-Tests für .NET-prüfbares Verhalten. Punkt 5 nennt, was im Browser bleibt, das Verbot neuer Logik-Tests, die Obergrenze 118 samt Wächter und den Verzicht auf Schleifen. Der Wächter steht in der Liste "Mechanisch geprüft von". CLAUDE.md ist unverändert und verweist weiter auf die Doktrin.
- Hänger: Ein Gesamtlauf über die Kassenbuch-Dateien hing drei Stunden. Einzeln (11 Dateien) und gemeinsam (dreimal 113 Tests) ließ er sich nicht nachstellen. Ein Kandidat ist entschärft: `clickSortThenAccount` in `ledger-read-retry` wartete ohne Frist auf die dritte Antwort und hat jetzt `withDeadline` mit 10 s. `scripts/run-guards.mjs` setzt jetzt `--test-timeout=120000` (`TEST_TIMEOUT_MS`, `nodeTestArgs`, Test in `run-guards.test.mjs`, Gegenbeweis: ohne das Flag ist der Test rot). Ein hängender Test wird damit rot, statt das Gate anzuhalten. Eine weitere Hängestelle ist ebenfalls behoben: Die Speichern-Schleife in `import-split-target` klickte nach erfolgreichem Speichern ein zweites Mal.

**Je Datei: Tests vorher → nachher, Laufzeit seriell vorher → nachher (je Datei ein eigener node-Prozess)**

| Datei | Tests | Laufzeit |
|---|---|---|
| budget-page | 45 → 28 | 100,6 → 66,8 s |
| budget-money-tab | 12 → 11 | 51,8 → 52,0 s |
| move-money | 10 → 3 | 78,7 → 27,3 s |
| targets-apply | 8 → 4 | 37,8 → 24,5 s |
| category-detail | 12 → 4 | 30,1 → 10,9 s |
| category-picker | 12 → 4 | 14,6 → 8,9 s |
| ledger-filter-bar | 19 → 6 | 13,1 → 12,7 s |
| ledger-detail | 11 → 2 | 8,0 → 7,6 s |
| ledger-status-column | 9 → 3 | 6,8 → 6,7 s |
| ledger-split-editor | 9 → 2 | 35,9 → 16,5 s |
| ledger-memo | 8 → 1 | 14,4 → 6,5 s |
| ledger-review-mark | 8 → 1 | 11,8 → 7,0 s |
| ledger-category-change | 7 → 2 | 14,4 → 9,9 s |
| ledger-read-retry | 6 → 3 | 14,8 → 11,7 s |
| ledger-not-in-ynab | 6 → 0, gelöscht | 7,0 → 0 s |
| ledger-selection | 5 → 4 | 23,8 → 16,4 s |
| ledger-lena | 5 → 1 | 12,5 → 6,5 s |
| ledger-compact-mouse | 5 → 2 | 11,5 → 7,4 s |
| ledger-account-choice | 2 → 1 | 13,5 → 12,8 s |
| ledger-category-qualified | 2 → 1 | 6,3 → 6,3 s |
| ledger-external-links | 3 → 2 | 6,6 → 6,6 s |
| ledger-lena-batch | 3 → 2 | 13,0 → 12,5 s |
| ledger-mirror-buttons | 2 → 1 | 6,3 → 6,3 s |
| ledger-recent-categories | 3 → 0, gelöscht | 16,1 → 0 s |
| ledger-review-button-visibility | 2 → 1 | 8,7 → 6,3 s |
| ledger-review-state | 3 → 2 | 7,9 → 7,5 s |
| ledger-sort-chip | 3 → 1 | 13,9 → 12,8 s |
| ledger-sort-headers | 2 → 1 | 7,3 → 7,2 s |
| ledger-split-cashback | 4 → 1 | 16,1 → 7,3 s |
| ledger-table-columns | 3 → 1 | 11,6 → 7,6 s |
| import-split-target | 18 → 1 | 27,1 → 9,0 s |
| import-lena | 4 → 1 | 9,8 → 7,2 s |
| import-pruefen-duplikat-details | 2 → 1 | 7,1 → 6,4 s |
| touch-target-small-button | 2 → 1 | 7,9 → 7,9 s |
| styleguide-split-line-actions | 1 → 0, gelöscht | 6,4 → 0 s |
| rules-page-mobile | 22 → 11 | 45,0 → 30,6 s |
| sync-start | 10 → 3 | 16,2 → 9,5 s |
| settings-secrets | 8 → 2 | 28,1 → 14,0 s |
| inline-rule-form | 5 → 1 | 17,6 → 8,6 s |
| screenshot-route | 32 Browser → 0 (25 ohne Browser) + `screenshot-route-capture` 1 | 1,0 → 0,3 + 0,7 s |
| export-design-system | 20 Browser → 0 (19 ohne Browser) + `export-design-system-run` 1 | 40,0 → 6,7 + 34,8 s |
| static-serve | 7 Browser → 0 (6 ohne Marker) | 5,6 → 5,6 s |

Alle Testdateien seriell: 863 s → 571 s. Gate-Schritt `test:scripts` (parallel 4): 181 s.

**Was gezogen, was gelöscht, Mutanten** (alle Mutanten gesetzt, rot gesehen, zurückgenommen; 110 insgesamt)
- **budget-page** (Mutante je Gruppe): Monat nicht offen (`previousMonth` tryLast→tryHead, vorhandener Test). Fehler und Wiederholen mit Rahmen (`Load` ohne `frameCmd`, neu "Wiederholen liest den Rahmen neu"). Filter ohne Treffer und Such-Hinweis (`emptyListHint` gezogen, Text geändert). Chip-Zahlen und Sprache "Rot" (`filterChipLabel` gezogen, "Rot" bzw. `OverdrawnOnly <= 0`). Suche als Logik (`filterGroups` ganze Gruppe; Betreten leert, Mutante in `Inspector.fs`). "erfüllt" in Liste und Tab (`listStatusText = targetStatusText`; `targetUnderfunded` gezogen, ohne Snoozed). Scheitert das Blättern (`applyLoaded` Error setzt ChosenMonth None; `emptyPanel` PanelHint). Zusammengelegt wurden Tests mit gleicher Messfrage (Breiten 320 bis 1920, Sticky von Leiste und Chips, Tastatur seitenweit samt Alt und unter 1024). Es bleiben Layout, Sticky, Farben aus Tokens, Fokus, Tastatur, Scrollen, Sheet-Übergang, Fable-Formate und der Navigations-Happy-Path.
- **move-money** (Mutante je Gruppe): Vormonat (feste Monatszahl im Auftrag), Neu-Lesen gegen Wechsel (`entriesOutdated` false), wachsendes Fenster (`frameDueAfterReport` false), Hinweis je Tab und Stelle (`pageRetries` gezogen, ohne `isPanel`), Abbruch (`applyMoveCallLost` ohne `awaitMonth`), Wahl im Popover (`PickerOpen` bleibt), X fehlt (Filter raus). Es bleiben der Handy-Fluss, der Toast "Erneut senden" und das Popover am PC.
- **budget-money-tab**: Es bleiben Fokus, Maße, Kompilat und zwei Hauptabläufe, die Schleife ist in einen Test gezogen. Mutante `pageRetries`.
- **targets-apply**: `closeTargets` true, Rang, Stand geändert, `reportLines` gezogen (ohne Sortierung). `waitForTimeout` ist durch `waitForResponse` ersetzt.
- **category-detail**: `entryRouteExit`, `entriesDue`, `Entries = Loading`, `AbandonsReturn`, `shown <> routeId`, `parseEntryRoute`, `routeEntryLoadFailed`, `urlChangedCmd`, `ReplacesHistory`. Es bleiben der Sprung am Handy mit Zurück, Layout bei 360 px, das PC-Panel mit Esc und das echte Browser-Zurück.
- **category-picker**: `suggestedList`, `noticeFor`. Browser-Mutante "Stand-Zeile immer zeigen".
- **ledger-filter-bar** (Mutante je Gruppe): M1 `ApplyCategoryFilter`, M2 `RemoveCategoryFilter`, M3 `ClearAllFilters`, M4 `hasAnyFilter`, M5 `hasSummaryFilter`, M6 `emptyListKind`, M7 `quickDateRange`, M8 `dateDraftHasError`, M9 `orderChipsForDisplay`. Es bleiben die Happy Paths für PC und Handy, Datum und ISO im Fable-Kompilat, Schriftgröße gegen den iOS-Zoom, der native Kalender und Sticky.
- **Kassenbuch Teil 1**, Heimat in den vorhandenen `Ledger*ClientTests`: detail M10 `FilterByRowPayee`, M18 `detailNoticeFor`; not-in-ynab M12 `rowStatusMark`, die Layoutmessung ist in ledger-status-column gewandert; memo M11 `memoActionFor`; lena M13 `lenaActionFor`, M15 `rowMatchesFilters`; read-retry M14 Generationswächter; selection M16 `selectionPickerSubtitle`; split-editor M17 `canSave`; review-mark M19 `reviewMarkActionFor` und Browser-Mutante M22 (Prüfknopf an gelöschter Zeile, Sperre an drei View-Stellen, bleibt im Browser); category-change M20 `categoryLockHint`; compact-mouse M21 `keyboardHintVisible`. Ersatzlos gestrichen ist eine Maß-Zusicherung zur Styleguide-Galeriekarte der Auswahl-Leiste. Das ist Doku, kein Produkt.
- **Kassenbuch Teil 2 und Import**: import-split-target M1 bis M5 (`shouldShowLoadFailure`, Toast-Stapel, `pageLoadCmd QuickAdd`, Retry-Inventar SyncFlow), neue Tests in `SyncFlowStateTests`. import-lena M11, duplikat M10 `duplicateDetailsMode`. review-state M6 und M7 (`summaryLineText`, `allReviewedText` aus `Ledger/View.fs` gezogen), Browser B4. recent-categories M8 `pushRecentCategories`. sort-chip M9 `sortOptions`. account-choice M12. external-links M13. lena-batch M14. split-cashback M15. styleguide-split-line-actions M16. Browser B1 (Hover-Deckkraft), B2 (`md:hidden`), B3 (`min-h-[48px]`).
- **rules-page-mobile** (Mutante je Gruppe, Browser): B1 Trefferfläche, B2 `stopPropagation` am Pfeil, B3 `swallowNextClick`, B4 Autofokus, B5 Leertaste auf der Zeile, B6 `ariaLabel`, B7 `onMouseDown` am Auslöser, B8 Dialogtext, B9 und B10 Picker-Form, B11 Ghost-Click vor der Dateiauswahl. Unter .NET kamen N1 und N2 dazu (`testPatternCmd` gezogen, `ClientCommandTests`).
- **sync-start, settings-secrets, inline-rule-form**: N3 bis N7 in `ClientCommandTests` (`inlineRuleTestCmd`, `disconnectBankCmd`, `comdirectActionsLocked`, `connectionTestVisible`, `hintFor` gezogen), dazu E1 bis E6 auf vorhandenen Tests und die Browser-Mutanten B12 (`min-h-6`), B13 (Knöpfe vertauscht) und B14 (Debounce 0).
- **Werkzeuge**: Reine Tests stehen ohne Browser-Marker, je Werkzeug bleibt ein Rauchtest. Fünf Strukturtests am Screenshot-Werkzeug sind nach Doktrin 7 gelöscht. `static-serve` startet `startStaticFrontend` direkt, die Wahl des Servers prüft die reine `frontendServerKind`. Mutanten T1, T2, T3b, T3c und T5 sind rot geworden. T3 (`fullPage: true`) ist gleichwertig und überlebt.

**Warum 118 statt 100:** Was bleibt, misst Layout, Maße, Sticky, Trefferflächen, Fokus, echte Tastatur, Scrollen, CSS-Übergänge, berechnete Farben, Status 0 und Generation im `Client.Api`-Lambda, Fable-Formate oder ist der eine Happy Path eines Flusses. budget-page (28) und budget-money-tab (11) messen fast nur Layout, Fokus und Tastatur von Seite, Sheet und Panel über fünf Breiten. rules-page-mobile (11) misst je Test Trefferflächen, Tastatur, Fokus, Ghost-Click, Dateiauswahl oder den gerenderten Dialogtext, den der Owner in categorization-f955d vom Quelltext in den Browser verlegt hat. Mit weniger fielen Aussagen weg, die unter .NET nicht prüfbar sind. Die Begründung je Test steht in den Kopfkommentaren der Dateien.

**Produktionscode, nur Form:** reine Funktionen und Cmd-Seams in `Budget/Month.fs`, `MonthView.fs`, `InspectorView.fs`, `View.fs`, `Targets.fs`, `TargetsView.fs`, `Ledger/View.fs`, `Rules/State.fs`, `Settings/State.fs`, `Types.fs`, `View.fs`, `SyncFlow/State.fs`, `Views/StatusViews.fs`, `BankConnectionBadge.fs`, `scripts/screenshot-route.mjs`. Sichtbares Verhalten ist unverändert. Ob `update` wirklich den neuen Seam ruft, ist unter .NET nicht prüfbar, weil der Aufruf `Client.Api` berührt. Der Browser-Happy-Path deckt nur die Wege, die er geht.

Neue Tests: `src/Tests/ClientCommandTests.fs` (12), `BudgetPageTests.fs` (8), `SyncFlowStateTests.fs` (3), `LedgerReviewStateTests.fs` (2), `MoveMoneyClientTests.fs` (1), `TargetsApplyClientTests.fs` (1), `browser-test-budget.test.mjs` (5), `run-guards.test.mjs` (1), `screenshot-route.test.mjs` (`frontendServerKind`, 1).

Produktionsfehler wurden keine gefunden. Ein Fork hat einmal `git checkout` auf seine eigene Zwischenfassung von `scripts/rules-page-mobile.test.mjs` angewandt (Regelverstoß, ohne Folge für fremde Änderungen).

## Outcome (Ergänzung Iteration 2)

Nach Verifier-FAIL in Iteration 1. Stand jetzt: 116 Browser-Tests, zur Laufzeit gezählt. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 3521 grün, node 278 grün). `npm run test:scripts` lief dreimal hintereinander grün, ohne dass einzelne Tests wiederholt wurden (180 s, 179 s, 181 s), jeweils mit "Browser-Tests (zur Laufzeit gezählt): 116 von höchstens 116". Alle Läufe hatten eine Zeitgrenze (`perl alarm`, `--test-timeout`).

**1. Wächter zählt zur Laufzeit**
- `scripts/browser-usage-hook.mjs` lädt `run-guards.mjs` über `NODE_OPTIONS=--import` in jeden Testprozess und dessen Kindprozesse. Er umhüllt `launch`, `launchPersistentContext`, `launchServer`, `connect` und `connectOverCDP` von chromium, firefox und webkit. Ein Start hinterlässt eine Marke für die Testdatei; ein gestartetes Werkzeug erbt die Zuordnung über `BB_BROWSER_USAGE_OWNER`. `startFrontend` in `screenshot-route.mjs` setzt ebenfalls eine Marke.
- `scripts/test-count-reporter.mjs` ist ein zweiter Reporter neben `spec` und schreibt je gelaufenem Test Datei, Verschachtelung und Art. `scripts/browser-usage.mjs` zählt daraus die Blatt-Tests: Untertests zählen statt ihres Elterntests, Suiten zählen nicht. Pfade gehen über `realpath`, weil auf macOS `/var` ein Verweis auf `/private/var` ist.
- `run-guards.mjs` (`runCounted`, `runGate`) gibt Exit 1 mit "Browser-Wächter rot: N Browser-Tests, erlaubt sind M …", sobald `MAX_BROWSER_TESTS = 116` überschritten ist. `verify.sh` meldet den Schritt dann rot.
- Statisch bleibt nur das Verbot von `waitForTimeout` (`scripts/browser-test-budget.test.mjs`, 0 erlaubt, mit Gegenbeweis).
- Gegenbeweis je Form: `browser-test-budget.test.mjs` lässt einen echten gezählten Lauf über Beispieldateien laufen, die über ein Hilfsmodul mit `import * as pw` wirklich Chromium starten. Die Formen sind `it` (2), `describe` mit `it` (2), einzeilige `for`-Schleife (3), `forEach` (2), `t.test` (2), umbenanntes `test as pruefe` (1), Frontend über `startFrontend` ohne Browser (1) und eine Datei ohne Browser (5 Tests, zählt nicht). Erwartet 13, gezählt 13; mit Obergrenze 12 kommt die Meldung.
- Mutanten an der Zählung, alle rot:
  - Hülle ohne `launch`
  - `startFrontend` ohne Marke
  - Elterntests zählen mit
- Gegenbeweis am Gate: Mit `MAX_BROWSER_TESTS = 0` endet `npm run test:scripts -- scripts/ledger-memo.test.mjs` mit Exit 1 und der Meldung, obwohl der Test grün ist.

**2. Bank trennen fällt nach 3 s zurück**
- Die Wartezeit ist die benannte Konstante `bankDisconnectConfirmMs` (`src/Client/Components/Settings/State.fs`).
- Neuer Test in `src/Tests/ClientCommandTests.fs`: "die Rückfrage fällt nach der Wartezeit von selbst zurück". Er lässt den Cmd von `RequestBankDisconnect` ablaufen, erwartet `AbortBankDisconnect` frühestens nach der Wartezeit und eine danach geschlossene Rückfrage.
- Mutanten, beide rot:
  - `Cmd.none` statt `Debounce.delayed …`: "keine Nachricht innerhalb von 6000 ms"
  - Wartezeit 0
- Ein `[<Literal>]` sah der Testbau nicht, weil `src/Client/obj/.../ref/Client.dll` veraltet blieb. Siehe Backlog.

**3. Logik aus Browser-Tests gezogen**
- `budget-page` hat 28 → 27 Tests. Neue reine Funktionen in `Budget/Month.fs` (`monthStepperFor`, `targetsButtonVisible`, `amountColumns`, `mobileFiguresText`), die View ruft sie nur auf.
  - Neue Tests in `BudgetPageTests.fs`: Pfeil fehlt am Rand und Name führt zurück (P1, P2); Blättern liest den Rahmen nicht neu, Targets anwenden nur im laufenden Monat (P3, P4); Spalten und Beträge PC und Handy (P5, P6).
  - "Zu verteilen negativ ohne Warnhinweis" hat seine Heimat im vorhandenen Test "Ein negatives Zu verteilen bleibt negativ, ohne Sonderzustand" (P7).
  - Alle Mutanten rot. "PC: Tabelle mit Spaltenköpfen" ist gelöscht. Im Browser bleiben der Proxy-Aufruf, das Fable-Format, die Lage des Monatsnamens, teal und der Klick auf den Namen.
- `ledger-category-qualified` hat 1 → 0 Tests, die Datei ist gelöscht. Neue reine Funktion `listCategoryLabel (TableRow | PhoneRow)` in `Ledger/View.fs`, Test in `LedgerViewTests`, Mutanten Q1 und Q2 rot.
- Die übrigen Browser-Dateien sind mit derselben Frage erneut durchgesehen, ohne weiteren Fund. Was bleibt, misst Layout, Fokus, Tastatur, CSS-Breakpoint, Fable-Kompilat, React-Klick oder ist der eine Happy Path.

**4. Cmds, die nur der Browser ausführte**
Durchsucht wurden die gelöschten Zeilen in `scripts/` und der Client nach Debounce, `Async.Sleep`, `setTimeout`, Navigation, localStorage und Fokus. Neues Hilfsmodul `src/Tests/CmdTiming.fs` (`expectAfter`: frühestens nach der Wartezeit, Frist +3 s).

| Fund | Ersatz | Mutante, Ergebnis |
|---|---|---|
| Bank trennen, Rückfall 3 s | ClientCommandTests (Punkt 2) | `Cmd.none`, Wartezeit 0: rot |
| Regel löschen, Rückfall 3 s (`ruleDeleteConfirmMs`) | RulesListTests "die Rückfrage Löschen fällt nach der Wartezeit von selbst zurück" | `Cmd.none`, `Async.Sleep 0`: rot |
| Memo-Filter verzögert | LedgerPageTests "Memo- und Betragsfeld committen erst nach der Verzögerung …" | `Cmd.none`: rot |
| Betragsfilter verzögert | derselbe Test | `Cmd.ofMsg`: rot |
| Kategorie-Karte läuft nach 6 s ab | LedgerCategoryChangeClientTests "Der Zeitgeber der Karte meldet nach ihrer Dauer den Ablauf …" | `Async.Sleep 0`: rot |
| Import: Kategorie und Payee verzögert | SyncFlowStateTests "Kategorie und Payee einer Importzeile gehen erst nach der Verzögerung hinaus …" | `Cmd.none`, `Cmd.ofMsg`: rot |
| Toast blendet nach 5 s aus (`autoDismissAfterMs`) | SyncFlowStateTests "ein Toast blendet sich nach seiner Zeit von selbst aus" | `Cmd.none`: rot |
| Neulesen nach verlorener Antwort im 3-s-Takt (`interruptedStartPollMs`) | SyncFlowStateTests "… kommt der nächste Takt nach der Pause", Browser sync-start | `Cmd.ofMsg`: rot |
| Muster testen an der Importzeile verzögert | Browser inline-rule-form | Debounce 0 (Iteration 1): rot |
| Navigation, URL folgt dem Panel, History ersetzen | BudgetPageTests `urlChangedCmd`, `ReplacesHistory`; Browser category-detail | Iteration 1: rot |
| localStorage Kontowahl, Zuletzt verwendet | Browser ledger-account-choice, ledger-review-state (Neuladen) | Iteration 1 M12: rot |
| Fokus (Tabs, Liste, Suchfeld, Regelmenü) | Browser budget-page, rules-page-mobile | Iteration 1 B4: rot |
| Ghost-Click-Fenster 350 ms | Browser rules-page-mobile | Iteration 1 B3, B11: rot |

Außer "Bank trennen" hatte keiner dieser Zeitgeber vor der Task einen Browser-Test, der gelöscht wurde. Abgesichert sind sie trotzdem, weil es dieselbe Art Lücke ist.

**Browser-Tests je Datei zur Laufzeit (116):**
- 27: budget-page
- je 11: rules-page-mobile, budget-money-tab
- 6: ledger-filter-bar
- je 4: targets-apply, ledger-selection, category-picker, category-detail
- je 3: sync-start, move-money, ledger-status-column, ledger-read-retry
- je 2: settings-secrets, ledger-split-editor, ledger-review-state, ledger-lena-batch, ledger-external-links, ledger-detail, ledger-compact-mouse, ledger-category-change
- je 1: touch-target-small-button, screenshot-route-capture, ledger-table-columns, ledger-split-cashback, ledger-sort-headers, ledger-sort-chip, ledger-review-mark, ledger-review-button-visibility, ledger-mirror-buttons, ledger-memo, ledger-lena, ledger-account-choice, inline-rule-form, import-split-target, import-pruefen-duplikat-details, import-lena, export-design-system-run

Die Begründung, warum der Rest über 100 nur im Browser geht, gilt wie in Iteration 1.

**Doktrin:** Punkt 5 nennt die Obergrenze 116, die Laufzeitzählung in jeder Form, die Konstante in `scripts/browser-usage.mjs` und die Pflicht, einen Zeitgeber-Cmd in .NET ablaufen zu lassen (Vorbild `src/Tests/CmdTiming.fs`).

**Produktionscode, nur Form:**
- `Budget/Month.fs` und `MonthView.fs`, `Ledger/View.fs`: reine Funktionen
- `Settings/State.fs`: `bankDisconnectConfirmMs`
- `Rules/State.fs`: `ruleDeleteConfirmMs`
- `State.fs`: `autoDismissAfterMs` jetzt öffentlich
- `SyncFlow/State.fs`: `interruptedStartPollMs` jetzt öffentlich

Ein Produktionsfehler wurde nicht gefunden.

## Outcome (Ergänzung Iteration 3)

Nach Verifier-FAIL in Iteration 2. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 3521 grün, node 279 grün, Lauf mit Zeitgrenze). Die Obergrenze bleibt bei 116 Browser-Tests, das Gate ist grün.

**1. Browserstart über jedes Paket**
- Im Baum liegen sechs Kopien von `playwright-core`: oben, unter `playwright`, unter drei `@playwright/browser-*` und unter `@executeautomation/playwright-mcp-server`. Jede hat ihr eigenes `chromium`-Objekt mit eigener Klasse, es gibt also keinen gemeinsamen Prototyp. Gemeinsam ist der Start des Browsers über `child_process.spawn` (`playwright-core/lib/server/utils/processLauncher.js`).
- `scripts/browser-usage-hook.mjs` umhüllt jetzt `spawn`, `execFile`, `exec` und `fork` des eingebauten `node:child_process`. `syncBuiltinESMExports` sorgt dafür, dass auch `import { spawn }` die Hülle sieht. Erkennt die Hülle einen Browser, setzt sie die Marke.
- Ein Browser ist erkannt, wenn die ausführbare Datei auf Chromium, Chrome, `headless_shell`, Firefox, WebKit (`pw_run.sh`) oder msedge passt oder ihr Pfad im `ms-playwright`-Cache liegt (`isBrowserCommand` in `scripts/browser-usage.mjs`). Das gilt auch für andere Bibliotheken und für einen von Hand gestarteten Browser.
- Gegenbeweis in `scripts/browser-test-budget.test.mjs`: Der gezählte Lauf enthält jetzt zusätzlich je eine Beispieldatei mit echtem Start über `playwright-core` (`core.test.mjs`) und über `@playwright/test` (`playwright-test.test.mjs`). Erwartet 15, gezählt 15. Ein neuer Test prüft `isBrowserCommand` an echten Cache-Pfaden und an Werkzeugen (`npx`, `node`, `dotnet`).
- Mutanten, beide rot:
  - Die alte Hülle nur um `playwright`: Der gezählte Lauf findet 14 statt 15, es fehlt genau `core.test.mjs`. `@playwright/test` lädt dieselbe `playwright`-Kopie und wird deshalb auch mit der alten Hülle gezählt.
  - `isBrowserCommand` liefert immer `false`: zwei Tests rot.

**2. Doktrin Punkt 5**
Neuer Satz: Gezählt wird über den Hook in `NODE_OPTIONS` und `BB_BROWSER_USAGE_OWNER`. Ein Kindprozess, der beide nicht erbt (etwa `env: {}` beim Start), startet einen Browser ungezählt und ist deshalb verboten. Eine Mutante gibt es dafür nicht, weil der Satz eine Grenze der Messung benennt, die die Messung selbst nicht prüfen kann.

## Outcome (Nachbesserung nach PASS)

Die Hülle in `browser-usage-hook.mjs` übernimmt alle Eigenschaften der umhüllten `child_process`-Funktionen, `promisify(exec)` und `promisify(execFile)` liefern mit Hook wieder `{ stdout, stderr }`, belegt durch einen Regressionstest, der vor dem Fix rot war. `isBrowserCommand` prüft bei einer Befehlszeile nur das erste Wort. Die synchronen Formen bleiben unumhüllt, Doktrin Punkt 5 verbietet einen Browserstart darüber. Gate vom Conductor gefahren: `VERIFY: PASS` (dotnet 3521, node 281).
