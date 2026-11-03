---
id: budgeting-dyn8g
title: "Tab Historie je Kategorie: Aufbau von Verfügbar, Bewegungen des Monats und Ausgaben der letzten 6 Monate aus dem Kassenbuch"
status: done
type: feature
context: budgeting
created: 2026-10-29
completed:
depends_on: [design-system-001, budgeting-afd8h, budgeting-m1bce, budgeting-c5w2n, budgeting-g8s3w]
blocks: [budgeting-t6p8z, budgeting-h7q4v]
tags: [budget, historie, inspektor]
related_adrs: [0065, 0069, 0049]
related_research: []
prior_art: [budgeting-h2zfn, budgeting-m1bce]
---

## Why
Task 3 von 5 des Gesamtkonzepts (`docs/ux/budget/konzept.md`), Ablauf "Was ist passiert". Vision, Budget Stufe 2: sehen, wann wie viel zugewiesen, verschoben oder per Targets verteilt wurde, und woher Verfügbar kommt. Die Bewegungen liegen seit budgeting-h2zfn und budgeting-afd8h in `budget_movements` und `budget_movement_results` (ADR 0069). Der Tab Historie im Inspektor zeigt heute nur "Die Historie dieser Kategorie folgt." (`InspectorView.fs`).

Die allgemeine Ansicht für alle Kategorien ist abgetrennt (budgeting-h7q4v).

## What
**Tab Historie** im Inspektor, für die gewählte Kategorie und den gezeigten Monat, in jedem Monat (vergangen, aktuell, künftig). Drei Abschnitte von oben nach unten:

1. **Aufbau von Verfügbar:** Vortrag, Zugewiesen, Aktivität, Verfügbar. Vortrag = Verfügbar − Zugewiesen − Aktivität aus dem geladenen Monat (reine Funktion, keine Anfrage).
2. **Bewegungen** dieser Kategorie im Monat, neueste zuerst, aus Sicht der Kategorie:
   - Verschieben: "−20,00 € nach Urlaub" bzw. "+20,00 € von Lebensmittel" (Gegenseite aus derselben Gruppen-Id).
   - Zuweisen: "+20,00 € zugewiesen" bzw. "−20,00 € zurück nach Zu verteilen".
   - Targets anwenden: "+50,00 € Targets anwenden".
   - Je Zeile Zeitpunkt (Berliner Zeit) und Stand: geschrieben, fehlgeschlagen (mit Grund), in YNAB geändert, noch offen. Stand ist das jüngste Ergebnis der Bewegung.
   - Ohne Bewegungen ein Leerzustand. Unter der Liste ein Satz: Was direkt in YNAB zugewiesen wurde, steht hier nicht (ADR 0065, Negative).
3. **Ausgaben der letzten 6 Monate** als Balken: die 6 Monate bis einschließlich des gezeigten Monats, Wert ist die Netto-Aktivität der Kategorie aus dem Kassenbuch (Erstattungen mindern, gelöschte Buchungen zählen nicht, Splits mit ihrem Anteil, wie `getCategoryMonthEntries`). Der laufende Monat ist als unvollständig erkennbar. Jeder Balken trägt Monat und Betrag. Optik aus Styleguide und DS, Rot nur für Beträge im Minus (ADR 0049).

**Server:** eine Abfrage für Kategorie und Monat liefert die Bewegungen mit Gruppen-Gegenseite und jüngstem Ergebnis sowie die 6 Monatssummen aus dem Kassenbuch. Lesend, keine YNAB-Anfrage. Die Bewegungs-Abfrage und die Zeilen-Funktion so schneiden, dass budgeting-h7q4v sie für alle Kategorien und mit Filter nach Herkunft wiederverwendet (optionale Filter Kategorie, Monat, Herkunft).

**Laden im Client:** Das Lade-Muster der Buchungen (`requestEntries`, `EntriesGeneration`, `resetEntries`, `invalidateEntries`) wird auf Tab-Daten mit Schlüssel `(CategoryId, Monat)` verallgemeinert, eine Generation für beide Tabs (Hinweis aus dem Review von budgeting-g8s3w, `Types.fs`, `Inspector.fs`, `Month.fs`). Nach einer Bewegung (Zuweisen, Holen, Abgeben, Targets anwenden) ist die Historie wieder fällig, sonst wäre sie veraltet.

## Acceptance criteria
**Server**
- [ ] (Handler, In-Memory-SQLite) GIVEN Bewegungen aller drei Herkünfte in zwei Kategorien und zwei Monaten, eine davon mit zwei Ergebnissen WHEN Abfrage für Kategorie A und Monat M THEN nur die Bewegungen von A in M, neueste zuerst, je mit dem jüngsten Ergebnis, Verschiebungen mit der Gegenseite.
- [ ] (Handler) GIVEN optionaler Filter nach Herkunft WHEN Abfrage ohne Kategorie und Monat THEN alle Bewegungen dieser Herkunft (Grundlage für budgeting-h7q4v).
- [ ] (Handler) GIVEN Buchungen der Kategorie über 7 Monate, darunter eine Erstattung, eine gelöschte Buchung und ein Split WHEN Ausgaben für den gezeigten Monat THEN genau 6 Monatssummen bis einschließlich des gezeigten Monats, netto, ohne die gelöschte, mit dem Split-Anteil; Monate ohne Buchung sind 0.
- [ ] Neuer Endpoint: Aufruf in `RemotingPipelineTests.fs` (falls neues Interface) und eine Zeile in `RemotingEndpointNegativeTests.fs` (ungültiger Monat, unbekannte Kategorie). Fehler werden in `Validation.fs` gesammelt gemeldet.

**Reine Funktionen**
- [ ] (Domain) Bewegung → Zeile aus Sicht der Kategorie: je Herkunft und Richtung der Text aus "What", Betrag = neu − alt, Stand je Ergebnisart inklusive "noch offen" ohne Ergebnis.
- [ ] (Domain) Aufbau von Verfügbar: Vortrag = Verfügbar − Zugewiesen − Aktivität, auch bei negativem Verfügbar und negativem Zugewiesen.
- [ ] (Domain) Balkenwerte: laufender Monat als unvollständig markiert, vergangene und künftige gezeigte Monate nicht.

**Client-`update`**
- [ ] GIVEN Kategorie gewählt WHEN Tab Historie gewählt THEN die Historie wird für `(CategoryId, Monat)` angefragt; eine Antwort mit alter Generation (Kategorie- oder Monatswechsel dazwischen) wird verworfen.
- [ ] GIVEN Historie geladen WHEN Zuweisen, Holen, Abgeben oder Targets anwenden bestätigt ist THEN die Historie ist wieder fällig und wird beim sichtbaren Tab neu geholt.
- [ ] GIVEN Tab Buchungen WHEN Wechsel auf Tab Historie und zurück THEN keine doppelte Anfrage für schon geladene Buchungen (das verallgemeinerte Muster ändert das Verhalten der Buchungen nicht; bestehende Tests zu Buchungen bleiben grün).

**Sonstiges**
- [ ] Kein neuer Browser-Test (Regel zu Browser-Tests klärt infrastructure-b3t9k). Die Anzahl bleibt bei `MAX_BROWSER_TESTS`.
- [ ] Der Tab Historie ist am Handy im 500-px-Sheet lesbar, der Balken passt ohne waagrechtes Scrollen. [human-eye]
- [ ] README budgeting beschreibt den Tab Historie (Ubiquitous language: Aufbau von Verfügbar, Ausgaben der letzten 6 Monate).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Verfeinert am 2026-10-29, neu geschnitten am 2026-11-03.

**Entscheidungen (Roman, 2026-11-03):**
- dyn8g wird geteilt: diese Task ist der Tab Historie je Kategorie, die allgemeine Ansicht `#/budget/history` ist budgeting-h7q4v.
- Balken: 6 Monate bis einschließlich des gezeigten Monats, netto aus dem Kassenbuch, laufender Monat als unvollständig erkennbar.

**Entscheidungen (Roman, 2026-10-29), weiter gültig:** kompakte Zeilen; "Wer" entfällt vorerst, nur Roman budgetiert, die Herkunft steht für "wie".

Die Aktivität im Aufbau von Verfügbar bleibt YNABs Wert wie in der Liste (ADR 0065). Die Balken kommen dagegen aus dem Kassenbuch, das ist gewollt: budgeting-t6p8z ("Passt das Target noch?") rechnet mit denselben Kassenbuch-Summen. Weichen Balken des gezeigten Monats und Aktivität voneinander ab, ist das ein Hinweis auf fehlende Spiegelung, kein Fehler dieser Task.

Bausteine: `queryRecentCategories` und `loadApplication` in `src/Server/Budgeting/Persistence.fs` (Join auf das jüngste Ergebnis), `LedgerApi.getCategoryMonthEntries` als Vorbild für die Kassenbuch-Summen, `budgetApiWith` für die Verdrahtung mit Ports.

## Verifier note (iteration 1)

**REASONS:**
- Check 2 (Testlauf): `./verify.sh` scheitert im dotnet build: `src/Client/Components/Budget/State.fs(170,43): error FS0039: The value or constructor 'withEntries' is not defined`. Kein `VERIFY: PASS`.
- Ursache ist ein semantischer Merge-Konflikt ohne Textkonflikt: budgeting-r7t3k (428916d) hat in `State.fs` (Zeilen 168 bis 171) einen Zweig `PageEntered` mit `KeptPage` ergänzt, der `withEntries rereading` aufruft. dyn8g hat `withEntries` in `withTabData` umbenannt (State.fs:147). Nach dem Merge gibt es keine Definition von `withEntries` mehr. Andere veraltete Namen (`EntriesGeneration`, `resetEntries`, `invalidateEntries`, `requestEntries`) kommen im Budget-Client und in den beiden r7t3k-Testdateien nicht mehr vor.
- Check 1c: Im Diff steht kein Konfliktmarker und kein `.agentheim/`-Pfad.
- Nicht blockierend: (a) Die Tests entstanden nach dem Code, die genannten Mutanten machen sie aber widerlegbar. (b) `historyLineView` nutzt `OccurredAt.ToLocalTime()`, also die Zeitzone des Browsers. Die Task verlangt "Zeitpunkt (Berliner Zeit)". Sauberer wäre eine ausdrückliche Umrechnung nach Europe/Berlin oder eine Entscheidung im Outcome.

**SUGGESTED_FIX:** In State.fs:170 `withEntries rereading` durch `withTabData rereading` ersetzen (dort eventuell auch `entriesCmd` umbenennen). Danach `./verify.sh` bis `VERIFY: PASS` und prüfen, dass die KeptPage- und CloseTargets-Tests von r7t3k in BudgetPageTests.fs und TargetsApplyClientTests.fs zusammen mit den dyn8g-Tests grün sind.

**ITERATION_HINT:** likely-fixable

## Outcome

Der Tab Historie im Inspektor ist gebaut. Er gilt für die gewählte Kategorie und den gezeigten Monat, in jedem Monat. Drei Abschnitte: Aufbau von Verfügbar (Vortrag, Zugewiesen, Aktivität, Verfügbar, sofort aus dem geladenen Monat), Bewegungen (neueste zuerst, aus Sicht der Kategorie, mit Zeitpunkt in Berliner Zeit und Stand, Leerzustand und Satz zu YNAB-Zuweisungen) und Ausgaben der letzten 6 Monate als Balken (laufender Monat gestrichelt mit "läuft", je Balken Monat und Betrag in ganzen Euro).

**Shared** (`src/Shared/Budgeting.fs`): `MovementOrigin` ist aus `Server.Budgeting.Domain` hierher gezogen (Fälle unverändert, `originText`/`originOfText`/`sendsInOrder` bleiben im Server), damit der Client die Herkunft kennt. Neu sind `HistoryMovement`, `MonthSpending`, `CategoryHistory`, `HistoryStatus`, `HistoryLine` mit `historyLine` (Bewegung zu Zeile: Text je Herkunft und Richtung, Betrag neu minus alt, Stand inklusive "noch offen"), `AvailableBuildup` mit `availableBuildup`, `SpendingBar` mit `spendingBars` (laufender Monat unvollständig, Höhe in Prozent des größten Balkens), dazu `berlinTime` und `historyTimeText`. `src/Shared/Api.fs`: `BudgetApi.getCategoryHistory: CategoryMonthQuery -> Async<Result<CategoryHistory, string>>`.

**Zeitpunkt in Berliner Zeit (Iteration 2):** Im Client gab es keinen Helfer dafür, das Kassenbuch nutzt `ToLocalTime()`. Fable kennt `TimeZoneInfo` nicht. `berlinTime` rechnet deshalb rein nach der EU-Regel: Sommerzeit (UTC+2) vom letzten Sonntag im März bis zum letzten Sonntag im Oktober, jeweils 01:00 UTC, sonst UTC+1. Die Zeitzone des Browsers spielt keine Rolle. `historyTimeText` formatiert "03.10. 11:30" ohne kulturabhängige Formatstrings. Ein Test gleicht die Regel ein Jahr lang in 3-Stunden-Schritten mit `TimeZoneInfo` Europe/Berlin ab. Ändert die EU die Regel, muss `berlinTime` nachgezogen werden.

**Server:** `Budgeting.Persistence.MovementFilter` (Kategorie, Monat, Herkunft, alle optional) und `queryMovements`/`loadMovements`: ein SQL mit Join auf das jüngste Ergebnis und Self-Join auf die Gegenseite derselben Gruppen-Id (nur bei Verschieben), neueste zuerst. `Server.Budgeting.QueryHandlers`: `HistoryPort`, `movements` (Grundlage für budgeting-h7q4v), `categoryHistory`, `liveHistoryPort`. `Server.Budgeting.Domain`: `spendingMonths`, `monthKeyOf`, `spendingOf` (füllt Monate ohne Buchung mit 0). `Ledger.QueryHandlers.monthlyActivity`/`getCategoryMonthlyActivity`: eine Abfrage über alle 6 Monate, Summe über `categoryShare` wie im Kategorie-Detail. `Server.Api.categoryHistoryWith` prüft mit `Validation.validateCategoryMonthQuery` (alle Fehler zugleich) und ist in `budgetApiWith` verdrahtet. Ein neues Remoting-Interface gibt es nicht, `BudgetApi` hat seinen Pipeline-Aufruf schon.

**Client:** `SelectedCategory.History`, `Model.TabGeneration` (statt `EntriesGeneration`), `LoadCategoryHistory`, `CategoryHistoryLoaded`. Das Lade-Muster ist verallgemeinert: `tabDataDue`, `requestTabData` (`TabRequest`), `applyCategoryHistory` in `Inspector.fs`, dazu `resetTabData`, `resetTab` und `invalidateTabData` in `Month.fs`. Eine Generation gilt für beide Tabs. Die Anfrage selbst erhöht sie nicht mehr. Sie steigt nur, wenn Kategorie oder Monat wechseln oder Daten neu fällig werden, und dabei wird jedes laufende `Loading` wieder `NotAsked`. So hängt keine Liste in `Loading`, wenn man zwischen den Tabs wechselt. In `State.fs` macht `afterMovement` die Historie nach `AssignAnswered`, `AssignCallLost`, `MoveAnswered`, `MoveCallLost`, `MoveRetried`, `TargetsApplied` und `TargetsRetried` wieder fällig. `InspectorView.historyTab` zeichnet den Tab nur mit DS-Bausteinen (`Money`, `Badge`, `Loading`, `ErrorDisplay`, `ListRoles`).

**Zusammenspiel mit budgeting-r7t3k (Iteration 2):** Der `KeptPage`-Zweig von `PageEntered` ruft jetzt `withTabData` statt des umbenannten `withEntries`. Die neuen Wege von r7t3k passen zum Muster: `enterPage` setzt mit `resetTabData` Buchungen und Historie auf `NotAsked`. `rereadMonth` lässt einen geladenen Monat stehen (`QuietReread`), dadurch holt `withTabData` die Daten des sichtbaren Tabs sofort. Die des anderen Tabs kommen beim Wechsel. Bei `FullReread` holt sie der `Loaded`-Zweig. `withReport` in `closeTargets` ändert nichts an den Tab-Daten, die Historie ist nach `TargetsApplied` bereits fällig. Zwei Tests belegen die Rückkehr im Tab Historie und im Tab Buchungen.

**Tests (37 neu, davon 7 in Iteration 2):** `src/Tests/BudgetHistoryTests.fs` (Zeilen je Herkunft und Richtung, Stand je Ergebnisart, Vortrag auch bei negativen Werten, Balken, Monatsfenster über den Jahreswechsel, Berliner Zeit an beiden Umstellungen, über Mitternacht und ein Jahr lang gegen Europe/Berlin; gegen In-Memory-SQLite über das API-Feld: Filter auf Kategorie und Monat mit jüngstem Ergebnis und Gegenseite, Filter nur nach Herkunft, 6 Monatssummen mit Erstattung, gelöschter Buchung, Split und 7. Monat, gesammelte Fehler). `src/Tests/BudgetHistoryClientTests.fs` (echtes `update`: Anfrage beim Tab-Wechsel, verworfene Antworten nach Kategorie- und Monatswechsel, Wiederholen, Neuladen nach jeder bestätigten Bewegung, kein doppelter Abruf der Buchungen beim Hin- und Herwechseln, laufende Buchungen überstehen den Tab-Wechsel, Rückkehr aus dem Kassenbuch in beiden Tabs). Eine Zeile in `RemotingEndpointNegativeTests.fs` (ungültiger Monat ergibt Fehler, unbekannte Kategorie eine leere Historie), `getCategoryHistory` im Retry-Inventar von `RetryTests.fs`. Angepasst wurden in Iteration 1 `BudgetPageTests.fs` (umbenannte Funktionen, gleiches Verhalten), `LedgerPageTests.fs` (neues Feld) sowie `MoveMoneyTests.fs` und `TargetsApplyTests.fs` (Herkunft jetzt aus `Shared.Budgeting`). Kein neuer Browser-Test.

**Lauf (Iteration 2, nach dem Merge von main mit r7t3k):** `./verify.sh` ergibt VERIFY: PASS (dotnet 3568 grün, 10 opt-in übersprungen, node 281 grün, Build und beide Wächter grün). Die Tests von r7t3k in `BudgetPageTests.fs` und `TargetsApplyClientTests.fs` sind darin grün. `npm run check:readme-delta -- --bc budgeting` endet mit Exit 0.

**Abweichungen und offene Punkte:**
- TDD: Die Tests sind nach dem Produktionscode entstanden. Rot geprüft sind diese Mutanten: `afterMovement` ohne `resetTab HistoryTab` (drei Client-Tests rot), `requestTabData` erhöht die Generation wieder (vier Client-Tests rot), Gegenseiten-Join leer und Sortierung aufsteigend (Abfrage-Test rot). Eine vollständige Mutantenprüfung gab es nicht.
- [human-eye] ist offen: Der Tab ist am Handy im 500-px-Sheet nicht per Screenshot angesehen. Die Balken stehen in `flex` mit `flex-1 min-w-0` und tragen Beträge in ganzen Euro ohne Währung, damit sechs nebeneinander passen. `design-quality` ist hier sinnvoll.
- Auslegung zu "Rot nur für Beträge im Minus": Die Balken zeigen Ausgaben, also die Netto-Aktivität mit umgekehrtem Vorzeichen. Rot wird nur ein Monat, in dem Erstattungen überwiegen. Im Aufbau ist nur Verfügbar im Minus rot (`Money.available`).
- Ob Fable den empfangenen `DateTime` als UTC führt, ist nur im Kompilat prüfbar. `berlinTime` rechnet einen als lokal markierten Wert vorher nach UTC um, beide Fälle ergeben dieselbe Uhrzeit.
