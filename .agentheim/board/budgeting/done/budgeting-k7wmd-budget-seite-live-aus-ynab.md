---
id: budgeting-k7wmd
title: "Budget-Seite: Monat (dieser oder nächster), Zu verteilen und Kategorien nach Gruppen mit Zugewiesen, Aktivität und Verfügbar, live aus YNAB, am Handy und am PC"
status: done
type: feature
context: budgeting
created: 2026-10-26
completed:
depends_on: [design-system-001]
blocks: [budgeting-h2zfn]
tags: [budgeting, ui, ynab-read, mobile]
related_adrs: [0063, 0055]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-r4tkq]
---

## Why
Das Budget ist die nächste große Fähigkeit (ADR 0063). Die Seite ist die Fläche, auf der
`budgeting-h2zfn` "Targets anwenden" sitzt, und für sich nutzbar, weil sie Roman den Stand
zeigt. Sie liest live aus YNAB und speichert keine Beträge. Damit kann BB YNAB nicht
unerklärt widersprechen (Scheiter-Kriterium der Vision).

## What
Seite "Budget" im Client, über die Navigation erreichbar.
- Monat wählbar: dieser Monat (Vorgabe) und nächster Monat, als Umschalter (Budgetieren am
  30. für den 1.).
- **Zu verteilen** als große Zahl. Negativ wird einfach negativ angezeigt, kein Warnen,
  kein Sperren (Roman geht bewusst ins Minus, bis das Kindergeld am 15. kommt).
- Kategorien nach Kategoriegruppe mit Zugewiesen, Aktivität, Verfügbar. Beträge über
  `Money`. Handy: Zeilen oder Karten ohne horizontales Scrollen. PC: Tabelle.
- Server: neuer Lesepfad in `YnabClient` für `GET /budgets/{id}/months/{YYYY-MM-01}`, der
  `to_be_budgeted` und je Kategorie `budgeted`, `activity`, `balance`, Gruppe sowie
  `goal_under_funded` und `goal_snoozed_at` dekodiert (die beiden letzten braucht
  `budgeting-h2zfn`). Ausgeblendete und gelöschte Kategorien und Gruppen werden nicht
  angezeigt (Verhalten prüfen, im Outcome festhalten).
- Struktur nach dem Vorbild von `src/Server/Ledger/` und `src/Client/Components/Ledger/`:
  eigener Ordner für `budgeting`, eigene Shared-Typen, neuer Fable.Remoting-Vertrag in
  `src/Shared/Api.fs`.
- Kein Schreiben nach YNAB, keine Persistenz, keine Kategorie-Suche.

## Acceptance criteria
- [ ] `YnabClient` liest den Monat und liefert ein Modell mit `ToBeBudgeted` und je Kategorie Id, Name, Gruppenname, Zugewiesen, Aktivität, Verfügbar, Fehlbetrag (option) und `Pausiert` (bool).
- [ ] Decoder-Test mit festem JSON ohne Netz: Milliunits, `goal_under_funded` null, 0 und positiv, `goal_snoozed_at` gesetzt und null.
- [ ] Ein reiner Domain-Baustein gruppiert nach Kategoriegruppe in YNAB-Reihenfolge und lässt ausgeblendete und gelöschte Kategorien weg. Tests: leere Gruppe, mehrere Gruppen, Reihenfolge.
- [ ] Die API nimmt "dieser" oder "nächster" Monat und rechnet zu `YYYY-MM-01` um (Test Dezember zu Januar). Ein ungültiger Monat wird an der Grenze abgelehnt (Validation-Test).
- [ ] YNAB-Fehler (auch 429) kommen als lesbare Meldung an. Die Seite nutzt `RemoteData` und `ErrorDisplay` mit Wiederholen.
- [ ] Die Seite zeigt Monat, Umschalter, Zu verteilen und Gruppen mit Kategorien. Negatives Zu verteilen erscheint ohne Warnhinweis und ohne Sperre.
- [ ] Nur DS-Komponenten und Tokens, keine Inline-Farben, gemessen an `standards/frontend/styleguide.md`.
- [ ] Die Seite ist über die Navigation erreichbar, auch am Handy.
- [ ] Der bestehende Aufrufer von `monthToBeBudgetedDecoder` (Ready to Assign im Kategorie-Picker) funktioniert unverändert.
- [ ] Zu verteilen und Verfügbar stimmen im aktuellen Monat mit YNAB überein (Stichprobe, Datum und Beispielkategorien im Outcome). [human-eye]
- [ ] Lesbar am Handy-Format (etwa 390 px) und am PC, am Handy ohne horizontales Scrollen. [human-eye]
- [ ] Lesende Probe, im Outcome dokumentiert: enthält `to_be_budgeted` des nächsten Monats den Rest des laufenden Monats? (GET gegen Romans Budget erlaubt, nie PATCH, POST oder DELETE.) Ergebnis per README_DELTA ins `budgeting`-README.
- [ ] `dotnet build` und `dotnet test` grün, kein Zugriff auf die Produktiv-Datenbank, Eintrag in `diary/development.md`.

## Notes
- Muster: `monthToBeBudgetedDecoder` und der Aufruf um `src/Server/YnabClient.fs` Zeile 58 und 219 (liest heute nur `to_be_budgeted` aus `months/current`).
- Spike: `.agentheim/knowledge/research/ynab-api-targets-zuweisung-2026-10-25.md`, Abschnitt 4. Die Pfade `/budgets/` und `/plans/` gehen beide; BB nutzt `/budgets/`.
- Aktivität kommt bewusst von YNAB. Die Naht `ledger` zu `budgeting` folgt erst, wenn BB selbst rechnet (Entwurf ADR 0064 in `budgeting-h2zfn`).
- Kategorie-Suche am Handy: eigenes Backlog-Item, zusammen mit "Geld verschieben" zu schneiden.
- Kein Design-Board (Roman, 2026-10-26): aus DS-Komponenten nach Styleguide, Feinschliff am echten Stand.

## Verifier note (iteration 1)

**REASONS:**
- Kriterium "YNAB-Fehler (auch 429) kommen als lesbare Meldung an" nur zur Hälfte belegt: die Client-Seite ist getestet (`BudgetPageTests`, Playwright "Fehler: lesbare Meldung mit Wiederholen"), die Server-Seite nicht. Die Statuszuordnung in `YnabClient.getMonth` (`| 401 | 404 | 429 | status`, Zweig `with ex`) und der Weg in `budgetApi` über `ynabErrorToString` (auch fehlender Token, fehlendes Budget) haben keinen Test; `getMonth` nutzt FsHttp ohne Naht und ist so nicht testbar. Muster im Repo: `LedgerCategoryBatchTests` "Fehlerzuordnung 400, 401, 429 und ein Netzfehler" mit injiziertem HttpMessageHandler.
- README: **Aktivität** ist dort als "Summe der Buchungen einer Kategorie in einem Monat (aus dem Kassenbuch)" definiert, die Seite zeigt aber YNABs `activity`. Kein Delta hält fest, dass Aktivität bis zur Naht ledger/budgeting von YNAB kommt.
- Schreibregel: "Zu verteilen ist ein laufender Stand und keine Monatssumme" definiert über die Verneinung des Gegenteils.
- Alles andere geprüft und grün (dotnet build 0 Warnungen, dotnet test 2306/0/7, npm run build, guard:classes, guard:tokens, test:scripts seriell 248/248).

**SUGGESTED_FIX:** `getMonth` eine testbare Naht geben (injizierter HttpClient oder reine Funktion Status zu YnabResult) und Tests für 401, 404, 429 und Netzfehler, dazu `ynabErrorToString` mit lesbarer 429-Meldung. Delta-Op, dass Aktivität auf der Budget-Seite vorerst aus YNAB kommt; den Satz "und keine Monatssumme" direkt formulieren.

**ITERATION_HINT:** likely-fixable

## Outcome

Budget-Seite live aus YNAB, ohne Persistenz und ohne Schreiben nach YNAB.

**Aufbau (Muster wie `ledger`):**
- Shared: `src/Shared/Budgeting.fs` (`BudgetMonthChoice` mit Schlüssel "current"/"next", rohe `YnabMonth`/`YnabMonthCategory`, angezeigte `BudgetMonth`/`BudgetGroup`/`BudgetCategory` mit `Underfunded` (Fehlbetrag, option) und `Snoozed` (Pausiert)); Vertrag `BudgetApi.getBudgetMonth: string -> Async<Result<BudgetMonth,string>>` am Ende von `src/Shared/Api.fs`.
- Server: `YnabClient.getMonth` und `Decoders.monthDecoder`/`monthCategoryDecoder` (`GET /budgets/{id}/months/{YYYY-MM-01}`; 401, 404, 429 und Netzfehler als `YnabError`); `src/Server/Budgeting/Domain.fs` (`monthKey` rechnet dieser/nächster auf `YYYY-MM-01` um, Dezember zu Januar; `toBudgetMonth` gruppiert in YNAB-Reihenfolge); `Validation.validateBudgetMonth` lehnt alles außer "current"/"next" an der Grenze ab; `budgetApi` in `src/Server/Api.fs`, registriert in `webApp()`. `monthToBeBudgetedDecoder` und `getReadyToAssign` sind unverändert (Test liest dieselbe Antwort).
- Client: `Components/Budget/{Types,State,View}.fs`, Seite `Budget` in `Types.Page` (Routing `#/budget`), Eintrag "Budget" in der Navigation (Handy-Tab-Leiste mit sechs Einträgen passt in 390 px), `Api.budget`-Proxy, `isPageReady`. Der Übergang (`select`, `applyLoaded`) ist rein; eine späte Antwort zu einem abgewählten Monat wird verworfen. Das Modell (`Choice`, `Month` mit `ToBeBudgeted` und Kategorien samt `Underfunded`/`Snoozed`) und der Umschalter sind für budgeting-h2zfn wiederverwendbar.
- Anzeige nur mit DS-Bausteinen (`PageHeader.calm`, `ChipBar`, `Card`, `Money`, `Table`, `ErrorDisplay`, `Loading`) und `ListRoles`-Tokens. Fehler: `ErrorDisplay.card` mit Wiederholen (Knopftext des DS ist "Try Again"), `RemoteData`.

**Verhalten ausgeblendeter Kategorien (geprüft):** YNABs Monats-Endpunkt liefert `hidden` pro Kategorie (auch für Kategorien in ausgeblendeten Gruppen, z. B. "Miete" in Wohnen), `deleted` (in Romans Daten 0) und `internal` (die drei internen Kategorien in der Gruppe "Internal Master Category", darunter Inflow). Die Seite lässt alle drei weg; Gruppen ohne sichtbare Kategorie erscheinen nicht. Romans Oktober: 167 Kategorien, davon 36 hidden und 3 intern, 128 sichtbar. Hidden-Kategorien hatten kein Zugewiesen und kein Verfügbar. YNAB liefert Gruppennamen teils mit Leerzeichen am Ende ("Medien + Software "); sie werden gekürzt.

**Stichprobe gegen YNAB (2026-10-26, GET gegen "My Budget", Budget-Id e0b4c3e7-d283-423e-8e30-3f37b6328ebf):** Zu verteilen laut API `to_be_budgeted` 35450 = 35,45 EUR. Die Seite, gestartet über `scripts/screenshot-route.mjs` gegen eine Kopie der Entwicklungs-DB (echter Server, echter YNAB-Aufruf), zeigte 35,45 EUR. Kategorien, Verfügbar: Essen/Einkaufen 1.120,09 (Zugewiesen 1.600,00, Aktivität -921,10), Ausflüge 185,49 (100,00 / -10,00), Kleidung 280,01 (300,00 / -19,99); identisch zu den API-Werten. Der Vergleich mit der YNAB-Web-Oberfläche selbst bleibt Augenarbeit für Roman.

**Probe (lesend):** `to_be_budgeted` des nächsten Monats enthält den Rest des laufenden Monats: Oktober 35.450, November 35.450 bei November-Einnahmen 0, -Zugewiesen 0, -Aktivität 0. Dezember (zwei Monate voraus) liefert 404. Dazu README_DELTA.

**Screenshots (einmalig, außerhalb der Tests):** Live gegen echtes YNAB (volle Seite, sehr lang): `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/04b6aafe-6e4f-485e-a143-c16e2369c195/scratchpad/budgeting-k7wmd/live/handy-390x844-budget.png` und `.../live/pc-1440x900-budget.png`. Mit Romans echten Oktober-Zahlen und negativem Zu verteilen (Vorzeichen für die Aufnahme umgekehrt, Toasts stammen von ungemockten Settings-Routen): `.../budgeting-k7wmd/handy-budget-negativ.png` (390 px) und `.../budgeting-k7wmd/pc-budget-negativ.png` (1440 px). Beobachtung: ein erster Lauf zeigte die Kopfzeile der PC-Tabelle gequetscht, weil `Table.thead` die Zellen selbst in eine `tr` legt und ich noch eine `tr` übergeben hatte; behoben und per Playwright abgesichert (genau eine Kopfzeile mit vier `th`).

**Tests:** `src/Tests/BudgetingTests.fs` (19: Decoder mit festem JSON inkl. `goal_under_funded` null/0/positiv und `goal_snoozed_at` gesetzt/null, Gruppieren, Monatsumrechnung inkl. Dezember zu Januar, Validierung), `src/Tests/BudgetPageTests.fs` (14: Wählen, Antwort einnehmen, Fehlermeldung, Monatsname, Routing, Navigation, `isPageReady`), `scripts/budget-page.test.mjs` (5 Playwright-Tests am echten Client gegen gemockte BudgetApi: Handy ohne horizontales Scrollen und ohne Tabelle, negatives Zu verteilen ohne `alert`, PC-Tabelle, Umschalter, Fehler mit Wiederholen, Erreichbarkeit über die Tab-Leiste). Acht bestehende Testdateien bekamen nur die neue Pflichtzeile `Budget = ...` im `State.Model`. `scripts/screenshot-route.mjs`: `/budget` in `KNOWN_ROUTES` (der Paritätstest gegen `Routing.parseUrl` verlangt es).

**Läufe im Worktree, alle grün:** `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` (nach Iteration 2) 2343 bestanden, 7 übersprungen, 0 Fehler; `test:scripts` seriell 252 von 252.

**Iteration 2 (Verifier-FAIL, behoben):** `YnabClient.getMonth` hat jetzt eine Naht: `interpretMonthResponse` (Status und Text zu `YnabResult`) und `getMonthWith` (injizierter Abruf, Ausnahme wird `NetworkError`); `getMonth` ruft sie mit FsHttp. In `src/Server/Api.fs` steht der Weg der Seite als `budgetMonthFor` (Token, Budget, Monats-Schlüssel, Fehlerübersetzung über `ynabErrorToString`), `budgetApi` ruft ihn nur noch mit den Settings auf. Neue Tests in `src/Tests/BudgetingTests.fs` (12): 200, kaputtes JSON, 401, 404, 429, 503, Ausnahme; 429 als lesbare Meldung, fehlender Token (YNAB wird nicht gefragt), fehlendes Standard-Budget, ungültiger Monat, nächster Monat fragt "2026-11-01". Rot-Prüfung: ohne die 429-Zeile in `interpretMonthResponse` und mit entfernter Budget-Meldung schlugen die zugehörigen Tests fehl, danach zurückgesetzt. README_DELTA ergänzt um ein `replace` auf **Aktivität** (auf der Budget-Seite vorerst von YNAB; vom Gate `check:readme-delta` akzeptiert); die Formulierung zu Zu verteilen ist direkt. Läufe: `dotnet build` 0 Warnungen, `dotnet test` 2343 bestanden (7 übersprungen), `npm run build`, `guard:classes`, `guard:tokens` ok, `test:scripts` seriell 252 von 252 (ein erster Lauf hatte einen Zeitfehler in `ledger-status-column.test.mjs`, den Test allein und den ganzen Lauf danach grün, nicht aus dieser Task).

**Offen für Folge-Tasks:** Kategorie-Suche am Handy (eigenes Backlog-Item laut Task). Fehlbetrag und Pausiert sind geladen, aber nicht angezeigt (h2zfn). Die Monatsgrenze nutzt `DateTime.Today` des Servers; im Container (UTC) kann der Monatswechsel um Mitternacht gegenüber Romans Ortszeit um bis zu zwei Stunden verschoben sein.

### README von Hand
Optional, Abschnitt Actors: alter Satz "- **Kassenbuch** (`ledger`): liefert die Aktivität pro Kategorie und Monat." neu "- **Kassenbuch** (`ledger`): liefert die Aktivität pro Kategorie und Monat (auf der Budget-Seite vorerst von YNAB, bis die Naht steht)." Die Prosa "Noch nicht modelliert" bleibt gültig.
