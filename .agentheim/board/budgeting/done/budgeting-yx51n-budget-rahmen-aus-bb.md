---
id: budgeting-yx51n
title: "Budget-Rahmen aus BB: Monatsfenster und Kategorie-Platzierung als YNAB-Stand, der Rahmen der Budget-Seite liest ohne YNAB-Aufruf"
status: done
type: feature
context: budgeting
created: 2026-11-20
completed:
depends_on: []
blocks: [budgeting-ffjk9]
tags: [budget-seite, lesemodell, performance, ynab]
related_adrs: [0074, 0076, 0088, 0101]
related_research: []
prior_art: []
---

## Why
Teil 2 von 4 nach ADR 0101 (Ursprung budgeting-m5pqn, Roman 2026-11-20: „Die Seite muss instant da sein. Immer zuerst
BB.“). Der Rahmen (`Budgeting.QueryHandlers.frame`) kostet heute je Seitenaufruf zwei YNAB-Aufrufe: Monatsliste
(`GET /months`) und Kategorie-Reihenfolge (`getCategoriesPlaced`).

## What
- Zustand `YnabMonthWindow` (Fenster der Monate) und `YnabCategoryCatalog` (Platzierung der Kategorien), geschrieben
  beim Nachlesen des Rahmens; Schema über `initializeDatabase`, wiederholbar (ADR 0083: Zustand).
- `QueryHandlers.frame` liest nur die DB, dazu BBs Layout wie heute.
- Eine bestätigte BB-Zuweisung ungleich 0 im letzten Monat erweitert das Fenster um einen Monat (ADR 0074: die Grenze
  nicht selbst rechnen, sonst 404).
- Ohne Stand wird einmal live gelesen und festgehalten.
- Kategorien verwalten und Picker (`getCategories`) bleiben live, nicht Teil dieser Task.

## Acceptance criteria
- [ ] Handler-Test mit zählendem `BudgetFramePort`: `frame` mit gehaltenem Stand ruft `FetchMonths` und
      `FetchCategoryOrder` null mal.
- [ ] Handler-Test: ohne Stand je ein Aufruf, danach null.
- [ ] Domain-Test: eine bestätigte Zuweisung ungleich 0 im letzten Monat öffnet den nächsten; eine Zuweisung 0, eine
      nicht bestätigte (fehlgeschlagene) und eine in einem früheren Monat öffnen keinen.
- [ ] Handler-Test: eine neue Kategorie im Katalog steht am Ende ihrer Gruppe (ADR 0088 Punkt 3).
- [ ] README budgeting nachgezogen, Delta mit `npm run check:readme-delta` geprüft.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.

## Notes
- Unabhängig von budgeting-m5pqn baubar. Das Nachziehen des Rahmens (ab 5 min Alter) gehört zu budgeting-ffjk9.

## Outcome

Der Rahmen der Budget-Seite (`getBudgetFrame`) liest ohne YNAB-Aufruf (ADR 0101, Teil 2 von 4). Monatsfenster und Platzierung der Kategorien hält BB als Stand des Rahmens, YNAB wird nur gefragt, solange kein Stand da ist.

**Was gebaut ist**
- Zustand nach ADR 0083: Typen `Domain.YnabMonthWindow` (Monate, `FetchedAt`) und `Domain.YnabCategoryCatalog` (Gruppe und Kategorie in YNABs Folge, `FetchedAt`). Tabellen `ynab_month_window` und `ynab_category_catalog` (je eine Kopfzeile `id = 1` mit `fetched_at`) mit `ynab_month_window_months` und `ynab_category_catalog_placements`. Das Schema legt `Budgeting.Persistence.createYnabFrameSchemaOn` an, aufgerufen aus `initializeBudgetingSchema` (eine Zeile), wiederholbar. Schreiben und Lesen: `holdMonthWindowOn`, `loadMonthWindowOn`, `holdCategoryCatalogOn`, `loadCategoryCatalogOn`, dazu die `*With connect`-Varianten (ein Fehler steht im Log und kommt als `Error`). Jedes Festhalten ersetzt den Stand ganz, in einer Transaktion.
- `QueryHandlers.frame port today now`: Fenster und Katalog aus dem Port (`LoadMonthWindow`, `LoadCategoryCatalog`). Fehlt ein Teil oder ist er nicht lesbar, liest es diesen Teil einmal aus YNAB (`FetchMonths` oder `FetchCategoryOrder`, das jetzt das rohe Ergebnis von `getCategoriesPlaced` liefert) und hält ihn mit `now` fest. Scheitert das Festhalten, gilt das Gelesene, der Grund steht im Log. Die Reihenfolge rechnet der Handler aus BBs Layout (`LoadCategoryLayout`, bestes Bemühen) und `flatCategoryOrder`. `Categories.bbCategoryOrderWith` ist entfallen, weil es keinen Aufrufer mehr hat.
- Fenster erweitern (ADR 0074 Punkt 4, ADR 0101 Punkt 6): Die reine Regel `Domain.monthOpenedBy window month newAssigned outcome` liefert den nächsten Monat nur für `Written`, einen Betrag ungleich 0 und den letzten Monat des Fensters. `Persistence.recordWriteBackOn` (der eine Weg, auf dem Zuweisen, Verschieben, Targets anwenden und Erneut senden ein Ergebnis festhalten) ruft danach `openMonthAfterWriteBackOn`. Ohne gehaltenes Fenster passiert nichts, denn das erste Lesen holt es samt neuem Monat aus YNAB. Scheitert das Erweitern, steht der Grund im Log und die Rückschreibung gilt trotzdem (YNAB hat geschrieben). Das nächste Nachlesen (budgeting-ffjk9) holt das Fenster dann neu.
- Live-Port `Api.liveFramePort`: `FetchMonths` → `YnabClient.getMonths`, `FetchCategoryOrder` → `YnabClient.getCategoriesPlaced`, die DB-Felder über `Persistence.getConnection`, `LoadCategoryLayout` → `Budgeting.Persistence.loadCategoryLayout`. `budgetApiWith.getBudgetFrame` reicht `utcNow ()` durch.
- Kategorien verwalten und die Picker (`getCategories`) lesen unverändert live. `src/Shared` und `src/Client` sind unverändert, Fixtures in `scripts/` mussten nicht mitgezogen werden.

**Tests** (`src/Tests/YnabFrameSnapshotTests.fs`, neu, 10 Tests)
- Domain: eine bestätigte Zuweisung ungleich 0 im letzten Monat öffnet den nächsten (auch über den Jahreswechsel, auch negativ); eine Zuweisung von 0, eine nicht bestätigte (`Failed`, dazu `ChangedInYnab`, `WithdrawnInBb`) und eine in einem früheren Monat öffnen keinen.
- Handler (eigene DB über `BudgetEventsFixture.isolatedDb`, zählender `BudgetFramePort`): Mit gehaltenem Stand wird `FetchMonths` und `FetchCategoryOrder` null mal aufgerufen. Ohne Stand je einmal, danach null mal, und `FetchedAt = now` ist festgehalten. Bei nicht lesbarem Stand wird einmal YNAB gelesen. Eine neue Kategorie im Katalog (in YNABs Folge vorn in der Gruppe) steht am Ende ihrer Gruppe hinter BBs gemerkter Folge. Eine fehlgeschlagene Rückschreibung im letzten Monat lässt das Fenster gleich, eine bestätigte öffnet den Januar, beides ohne YNAB-Aufruf.
- Live-Port-Stellen nach Doktrin Punkt 3: `liveFramePort.HoldMonthWindow`, `HoldCategoryCatalog`, `LoadMonthWindow`, `LoadCategoryCatalog` und `LoadCategoryLayout` belegt ein Verhaltenstest auf der Datenbank der Anwendung (Gruppe `InMemoryDb`, Monate 2097). `FetchMonths` und `FetchCategoryOrder` des Rahmen-Ports belegt der Strukturwächter (`StructureGuardTests`, neue Regexe mit den Gegenbeweisen „Rahmen ohne YNAB-Fenster“ und „Rahmen ohne YNAB-Platzierung“). `LoadMonthStartHidden` belegt weiter der bestehende Test in `BudgetMonthStartTests`. Den Anschluss `liveBudgetApi` → `budgetApiWith … liveFramePort` prüft weiter der Strukturwächter.
- Gegenproben, lokal zurückgedreht und wiederhergestellt: (M1) Stand nie nutzen → 4 Fälle rot; (M2) Reihenfolge ohne BBs Layout → „neue Kategorie am Ende ihrer Gruppe“ rot; (M3) kein Erweitern in `recordWriteBackOn` → Fenster-Test rot; (M4) jede Zeile im letzten Monat öffnet → 3 Domain-Fälle rot.
- Angepasst: die Rahmen-Fakes in `BudgetingTests`, `BudgetMonthStartTests`, `TargetsApplyTests`, `TargetChangeTests`, `TargetPauseTests`, `RemotingEndpointNegativeTests` und `YnabMonthSnapshotTests` nutzen den neuen `BudgetEventsFixture.framePortWith`/`framePortOf` (eigener, anfangs leerer Speicher, also liest jeder neu gebaute Port YNAB wie bisher). `isolatedDb` und `CategoryLayoutFixture.isolated` legen das neue Schema an. Der Test „der Rahmen der Budget-Seite trägt BBs Reihenfolge“ in `CategoryLayoutTests` ist entfallen, weil er `bbCategoryOrderWith` prüfte. Dieselbe Aussage deckt jetzt der Handler-Test „Eine neue Kategorie im Katalog steht am Ende ihrer Gruppe“ (kein Bugbezug).
- `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 5543 grün, node 358 grün) im fünften Lauf. Rot waren vorher: Lauf 1 der Sequenz-Wächter (Kommentar stand nicht direkt über `testSequencedGroup`, behoben). Lauf 2, 4 und die Einzelprobe zeigten nur den bekannten Layout-Zustand `targets-pruefen-nicht-neu-gelesen @ 412 px` (Invariante 5 und 7). Lauf 3 zeigte zusätzlich den Ledger-Zeittest „AC14: 1000 Buchungen … in angemessener Zeit“ (3,7 s statt unter 2 s), bei Load 18 bis 24 und parallelem Gate von budgeting-nyx75, das denselben Layout-Zustand rot hatte. `test:scripts` war in Lauf 1 mit demselben Client-Code grün (358), und die Task ändert weder `src/Client` noch `src/Shared` noch `scripts/`.

**README-Delta**: `replace` für **Monatsfenster** (gehalten, Lesen ohne `GET /months`, Erweitern nach bestätigter Zuweisung, Wachsen in YNAB erst nach budgeting-ffjk9) und für **Deaktiviert** und **BBs Reihenfolge** (für die Budget-Liste kommen die Gruppen-Ids aus dem Stand des Rahmens, für Picker und die Seite Kategorien weiter aus `GET /categories`), `append` für **Stand des Rahmens**. Die README habe ich nach `GET /months`, `getCategoriesPlaced`, `GET /categories`, Rahmen, `BudgetFrame`, „live“, Seitenaufruf und ADR 0076 durchsucht. Übrig bleiben die Seite Kategorien (live, unberührt), **Hinweis „nicht neu gelesen“** (das Lesen der Monate kann weiter scheitern, passt) und die gelösten offenen Fragen (historisch, passt). `npm run check:readme-delta -- --bc budgeting --delta …/scratchpad/yx51n/readme-delta.json`: Exit 0, „Ganz nachlesen: keine Einträge“, kein `--keep`.

**Nicht gebaut**: das Nachziehen eines alten Stands des Rahmens (ab 5 min) gehört zu budgeting-ffjk9.

**Für die Kann-Sammlung budgeting**
- Das erste Festhalten des Rahmens läuft nicht unter `MonthGuard`. Liest der Rahmen ohne Stand YNAB, während eine Rückschreibung im letzten Monat das Fenster erweitert, kann das spätere Festhalten die Erweiterung überschreiben. Bis zum Nachziehen (budgeting-ffjk9) fehlt dann der Pfeil zum neuen Monat. Selten (nur direkt nach dem Deploy), nichts geht an Geld verloren.
- Layout-Zustand `targets-pruefen-nicht-neu-gelesen @ 412 px` war in diesem Lauf auch einzeln rot (Seite nach „übernehmen“ gescrollt, „Neu laden“ bei y = −387) und in budgeting-nyx75 ebenso. Das stört das Gate inzwischen regelmäßig.
