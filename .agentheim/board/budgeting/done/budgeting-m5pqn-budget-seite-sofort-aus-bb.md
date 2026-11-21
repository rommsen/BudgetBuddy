---
id: budgeting-m5pqn
title: "Budget-Monat aus BB: YNAB-Stand je Monat festhalten, getBudgetMonth liest nur die Datenbank, Verfügbar und Zu verteilen leitet BB aus Soll, Kassenbuch und Stand ab"
status: done
type: feature
context: budgeting
created: 2026-11-20
completed:
depends_on: []
blocks: [budgeting-ffjk9, budgeting-vksa6]
tags: [budget-seite, lesemodell, performance, ynab]
related_adrs: [0065, 0074, 0076, 0083, 0084, 0088, 0101]
related_research: []
prior_art: [budgeting-zkbbd]
---

## Why
Roman (2026-11-20): „Budget-Seite ist nervig, dass die Budgets immer erst geladen werden müssen. Die Seite muss
instant da sein.“ Auf die Rückfrage, ob ein zuletzt bekannter Stand mit Abgleich im Hintergrund reicht: „Wir hatten
doch entschieden, dass Budgets führend sind. Also immer zuerst BB.“ (budgeting-zkbbd).

Heute fragt jeder Aufruf YNAB live: `getBudgetMonth` → `budgetMonthWithLedger` → `observeMonthWith` (`GET /months/{m}`)
und `targetRulesBestEffort` (`GET /categories`). Der Rahmen kostet zwei weitere Aufrufe (budgeting-yx51n).

Teil 1 von 4 nach ADR 0101: dieser Monat, budgeting-yx51n Rahmen, budgeting-ffjk9 Nachziehen beim Öffnen,
budgeting-vksa6 Monat beim Ortswechsel halten.

## What
Quellen je Wert (ADR 0101):

| Wert | Künftig |
|---|---|
| Zugewiesen | BB-Soll (Faltung `budgeting:month:<YYYY-MM>`), wie heute |
| Aktivität | BB, aus dem Kassenbuch (`getMonthlyActivityByCategory`); vor dem ersten Kassenbuch-Monat aus dem Stand |
| Vortrag (intern) | Stand, beim Nachziehen aus einem YNAB-Monat berechnet |
| Verfügbar | abgeleitet: Vortrag + Soll + Aktivität |
| Zu verteilen | Stand minus Summe (Soll minus YNAB-Zugewiesen) |
| Name, Gruppe, ausgeblendet, gelöscht, `internal` | Stand (ADR 0088: YNAB bleibt Quelle der Liste) |
| Goal-Vergleich (`TargetInYnab`) | Stand |
| Target, Pause, offene Rückschreibung, Deaktiviert | BB, wie heute |

- Zustand `YnabMonthSnapshot` (ADR 0083: Zustand, kein Ereignis), Schema über `initializeDatabase`, wiederholbar,
  Beträge als Milliunits `int64`. In der UI heißt er „YNAB-Stand“.
- Einziger Schreiber ist `observeUnguarded` (`src/Server/Budgeting/CommandHandlers.fs`, aufgerufen von
  `observeMonthWith` und `ensureTakenOverWith`), in derselben Transaktion wie Übernahme und Beobachtungen. Damit füllen
  ihn auch alle Befehle und der `TakeoverWorker`.
- `budgetMonthWithLedger` liest Faltung, Stand, Kassenbuch, Pausen, Regeln, offene Target-Schreibungen und Layout und
  ruft weder `observeMonthWith` noch `targetRulesBestEffort` mit YNAB-Port; `withTargetInYnab` liest das Goal aus dem
  Stand.
- Reine Funktionen in `Server.Budgeting.Domain` für Vortrag, Verfügbar und Zu verteilen.
- Ein Monat ohne Stand wird einmal beobachtet wie heute (F3), danach nie mehr im Lesepfad.
- Neues Feld `CopiedAt: DateTime option` in `BudgetMonth`.
- Die Echo-Regel aus ADR 0084 Punkt 3 bleibt unverändert, sie läuft künftig beim Nachziehen (budgeting-ffjk9).

## Acceptance criteria
- [ ] Handler-Test mit zählendem `YnabPort` (alle Felder zählen): `budgetMonthWithLedger` für einen Monat mit Stand
      macht null Aufrufe; mit einem Port, der bei jedem Aufruf wirft, kommt der Monat trotzdem.
- [ ] Handler-Test: ein Monat ohne Stand wird genau einmal beobachtet (ein `FetchMonth`), das zweite Lesen macht null
      Aufrufe.
- [ ] Handler-Test: nach `observeMonthWith` steht der Stand in der DB, nach einem Befehl (Zuweisen) ebenso; scheitert
      das Festhalten, wird weder Stand noch Beobachtung geschrieben (eine Transaktion).
- [ ] Domain-Test: ohne offene Rückschreibung und mit Kassenbuch gleich YNAB sind Verfügbar und Zu verteilen gleich
      den Werten des Stands, für jede Kategorie.
- [ ] Domain-Test: Soll 50 bei Stand-Zugewiesen 30 ergibt Verfügbar +20 und Zu verteilen −20; eine Verschiebung
      zwischen zwei Kategorien lässt Zu verteilen unverändert.
- [ ] Domain-Test: der Vortrag ist bei jedem Soll gleich; eine Kassenbuch-Buchung von −12 ändert Verfügbar um −12.
- [ ] Domain-Test: für einen Monat vor der ersten Kassenbuch-Buchung kommt die Aktivität aus dem Stand.
- [ ] Handler-Test: der Fehlbetrag rechnet mit dem Vortrag aus dem Stand, die bisherigen Fehlbetrag-Fälle bleiben grün.
- [ ] Live-Port-Stellen nach Doktrin Punkt 3 aufgelistet (`Api.budgetMonthFor`, `budgetApiWith`, Übersicht) und je
      per Verhalten oder Strukturwächter mit Gegenbeweis belegt.
- [ ] Pipeline- und Negativtests für `getBudgetMonth` grün; Fixtures in `scripts/*.test.mjs` tragen `CopiedAt` im
      selben Schritt.
- [ ] README budgeting nachgezogen (ADR 0101 ist angenommen, die Nachträge an 0065, 0076 und 0084 stehen schon), Delta mit `npm run check:readme-delta` geprüft.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.

## Notes
- Entschieden (Roman, 2026-11-20, alle Empfehlungen, ADR 0101 angenommen): F1 Vortrag und Zu verteilen vorerst
  als YNAB-Stand, F2 Aktivität aus dem Kassenbuch (eine in BB erfasste Buchung senkt Verfügbar sofort), F3 Monat
  ohne Stand einmal beobachten, F5 Verfügbar sofort abgeleitet, der dgrp7-Satz fällt.
- Konflikt der Spezialisten: der Architekt wollte die Aktivität aus dem Stand (sonst gemischte Quellen im Vortrag);
  gefolgt wurde dem tactical-modeler, weil der Vortrag beim Nachziehen aus einem einzigen YNAB-Monat gerechnet wird.
- Die PATCH-Antwort von YNAB wird nicht gebraucht (`YnabClient.setAssignedWith` dekodiert zu `unit`).
- Risiko: die Target-Übernahme beim Lesen entfällt; sie läuft künftig beim Nachziehen (budgeting-ffjk9) und vor
  Befehlen.
- Später, nicht ohne Roman: BB rechnet Vortrag und Zu verteilen selbst (Anker bei der Übernahme, Überziehungsregeln,
  Parallelrechnung). Dafür klären, ob Roman in YNAB Kreditkarten-Konten nutzt.
- budgeting-wnkgj (Budget angleichen) soll die abgeleiteten Gruppensummen nutzen und nach dieser Task gebaut werden.

## Verifier note (iteration 1)
**REASONS:**
- Kriterium „README budgeting nachgezogen“: Das README_DELTA lässt im Bullet **Budget-Seite** (README Zeile 52) den dgrp7-Satz stehen: „Aktivität, Verfügbar und Zu verteilen liest die Seite live aus YNAB, ohne Korrektur um die Differenz. Jedes Lesen beobachtet den Monat (`observeMonth`, ein YNAB-Aufruf).“ ADR 0101 Punkt 4 löst ihn ab, die Notes der Task sagen „der dgrp7-Satz fällt“. Prüfung 7 von `check:readme-delta` findet das nicht, weil das Delta diesen Bullet nie ersetzt.
- Gleiche Art, kleiner: Bullet **Aktivität je Monat** (Zeile 96) nennt das Kassenbuch beim Laden nur für Kategorien mit Periodenmonaten und bei unlesbarem Kassenbuch nur den fehlenden Fehlbetrag. Neu liest die Seite die Monatsaktivität aller Kategorien aus dem Kassenbuch (`monthActivityForPage`), bei unlesbarem Kassenbuch gilt die Aktivität aus dem YNAB-Stand.
- Gate im Worktree: Schritte 1 bis 5 grün (Expecto 5512), Schritt 6 rot (356/357, `VERIFY: FAIL`): `[layout] Sweep`, Zustand `targets-pruefen-nicht-neu-gelesen @ 412 px`, Invarianten 5 und 7 („Neu laden“ verdeckt oder außerhalb). Einzeln wiederholt grün. Derselbe Zustand war schon im Worker-Lauf rot. Es braucht ein eigenes `VERIFY: PASS`.
- Nebenbei (kein FAIL-Grund allein): im Pfad `NotRecorded` bekommt `pageMonthOf` `{ Raw = raw; FetchedAt = now }`, also `CopiedAt = Some now`, obwohl kein Stand gespeichert wurde.

**SUGGESTED_FIX:** replace-Op für den Bullet **Budget-Seite** mit der Fassung nach ADR 0101 (Aktivität aus dem Kassenbuch, Verfügbar und Zu verteilen abgeleitet, Lesen ohne YNAB, nur ein Monat ohne Stand wird einmal beobachtet); **Aktivität je Monat** passend nachziehen. Danach `npm run check:readme-delta` und `./verify.sh` bis `VERIFY: PASS`.

**ITERATION_HINT:** likely-fixable

## Verifier note (iteration 2)
**REASONS:**
- Gründe aus Iteration 1 erledigt (Budget-Seite, Aktivität je Monat, `CopiedAt = None` im Pfad `NotRecorded` mit Test). Gate im Worktree beim ersten Lauf `VERIFY: PASS` (dotnet 5513, node 357), `check:readme-delta` Exit 0.
- README-Abgleich: Bullet **Abweichung Kassenbuch/YNAB** (README Zeile 61) sagt „die Summe der Buchungen im Kategorie-Detail gegen YNABs Aktivität … BB rechnet Verfügbar nicht selbst (ADR 0065)“. Nach dem Diff stimmt beides nicht mehr: `BudgetCategory.Activity` kommt auf der Seite aus dem Kassenbuch (`bbMonthOf` mit `monthActivityForPage`), der Inspektor vergleicht genau dieses Feld (`detailTotal category.Activity entries`, `src/Client/Components/Budget/InspectorView.fs:115`, `deviationHint` in `src/Client/Components/Budget/Inspector.fs:25-29`). Der Hinweis „Kassenbuch: x, YNAB: y“ vergleicht damit Kassenbuch mit Kassenbuch und erscheint praktisch nicht mehr. Weder Delta noch Outcome nennen das.
- Nebenbei: die offene Frage „Rechnet BB Verfügbar und Zu verteilen selbst oder übernimmt es sie von YNAB und gleicht ab?“ (README Zeile 163) bleibt offen, obwohl ADR 0101 sie entscheidet.

**SUGGESTED_FIX:** Festlegen, wogegen der Hinweis jetzt vergleicht (Option 1: Aktivität aus dem YNAB-Stand; Option 2: Hinweis streichen, braucht Roman). replace-Op für **Abweichung Kassenbuch/YNAB** passend zum Verhalten, Test bei Code-Änderung, `check:readme-delta` und `./verify.sh` erneut.

**ITERATION_HINT:** likely-fixable

**Entscheidung Conductor (2026-11-21, Roman offline):** Option 1. Der Hinweis ist eine Funktion, die BB heute hat; nach CLAUDE.md bleibt sie, bis Roman sie streicht. Er vergleicht künftig die Summe der Buchungen im Detail mit YNABs Aktivität aus dem YNAB-Stand.

## Outcome

Die Budget-Seite liest einen Monat nur aus BBs Datenbank (ADR 0101, Teil 1 von 4). Wer die Seite oder die Übersicht öffnet, wartet bei einem Monat mit YNAB-Stand nicht mehr auf YNAB.

**Was gebaut ist**
- YNAB-Stand als Zustand (ADR 0083): Tabellen `ynab_month_snapshot` (Zu verteilen in Milliunits, `fetched_at`) und `ynab_month_snapshot_categories` (Name, Gruppe, Zugewiesen, Aktivität, Verfügbar in Milliunits, Goal als JSON über `Codec.encodeRule`, ausgeblendet, gelöscht, `is_internal`). Das Schema legt `initializeBudgetingSchema` an und lässt sich wiederholen (`Budgeting.Persistence.createYnabMonthSnapshotSchemaOn`, `writeYnabMonthSnapshotOn`, `loadYnabMonthSnapshotOn`, `loadYnabMonthSnapshotWith`). Typ `Domain.YnabMonthSnapshot`.
- Es gibt genau einen Schreiber: `observeUnguarded` über `recordObservationWith`, das jetzt `Persistence.recordObservationOn` aufruft. Ereignisse und Stand laufen in einer Transaktion. Damit schreiben ihn auch alle Befehle und der `TakeoverWorker`. `appendMonthEventsOn` ist entfallen, weil es keinen Aufrufer mehr hat.
- Reine Funktionen in `Server.Budgeting.Domain`: `carryoverOf` (Vortrag), `ledgerCoversMonth`, `monthActivity`, `derivedAvailable`, `derivedToBeBudgeted` und `bbMonthOf`.
- `QueryHandlers.budgetMonthWithLedger`: Gibt es einen Stand, liest es Faltung, Stand, Targets (`loadTargetRulesWith`, ohne Übernahme), Pausen, Kassenbuch, offene Target-Schreibungen und Layout, ohne `observeMonthWith` und ohne `targetRulesBestEffort`. `withTargetInYnab` liest das Goal aus dem Stand. Gibt es keinen Stand, wird der Monat einmal beobachtet wie bisher (F3), samt der Target-Übernahme nach bestem Bemühen. Die Aktivität kommt aus dem Kassenbuch, für einen Monat vor dessen erster Buchung aus dem Stand. Ist das Kassenbuch nicht lesbar, gilt die Aktivität des Stands und der Grund steht im Log. Ist der Stand nicht lesbar, scheitert das Lesen mit „YNAB-Stand des Monats nicht lesbar“.
- `BudgetMonth.CopiedAt: DateTime option` (`src/Shared/Budgeting.fs`) trägt `FetchedAt` des Stands. Die Fixtures in 9 Dateien unter `scripts/` tragen im selben Schritt `CopiedAt: null`.
- Befehle (`preparedMonth`, Vorschau) planen weiter auf YNABs Verfügbar aus dem frisch gelesenen Monat. Die Echo-Regel ist unverändert.

**Tests** (`src/Tests/YnabMonthSnapshotTests.fs`, neu, 17 Tests mit dem aus Iteration 2, dazu 1 in `UnderfundedTests.fs`)
- Domain: eine Property „ohne Abweichung gleich dem Stand“ für Verfügbar und Zu verteilen, dazu derselbe Fall über `toBudgetMonthWith`; Soll 50 bei Stand 30 ergibt +20 und −20; eine Verschiebung lässt Zu verteilen gleich; eine Kategorie, die nur in der Faltung steht, zählt mit; der Vortrag ist bei jedem Soll gleich; eine Buchung von −12 ändert Verfügbar um −12; vor der ersten Buchung kommt die Aktivität aus dem Stand (Gegenprobe im Monat der ersten Buchung); ohne Faltung gelten YNABs Werte.
- Handler (eigene DB): Mit Stand macht ein zählender Port mit allen vier Feldern null Aufrufe, und mit einem Port, der bei jedem Aufruf wirft, kommt der Monat trotzdem. Ohne Stand kommt genau ein `FetchMonth`, danach null. Nach `observeMonthWith` und nach `assignMoneyWith` steht der Stand in der DB. Ein Trigger, der den Stand verhindert, verhindert auch die Übernahme, mit Gegenprobe. Der Fehlbetrag (Auffüllen bis 100, Vortrag 30, Kassenbuch −10) ist 70 bei Verfügbar 20.
- Live-Port-Stellen nach Doktrin Punkt 3: `Api.budgetMonthFor` und `budgetApiWith.getBudgetMonth` haben je einen Verhaltenstest mit einem Port, der zählt und dann wirft: mit Stand 0 Aufrufe, Gegenbeweis ohne Stand 1 Aufruf. Die Übersicht (`Client/Components/Overview/State.fs`) ruft denselben Endpoint `Api.budget.getBudgetMonth` und ist damit abgedeckt. Den Anschluss `liveBudgetApi` → `budgetApiWith … livePort` prüft schon der Strukturwächter in `StructureGuardTests.fs:681`. `liveLedgerActivityPort.LoadMonthlyActivity` belegt der neue Test „Eine Buchung im Kassenbuch senkt Verfügbar im Monat, ohne dass YNAB sie kennt“ (UnderfundedTests, Jahr 2040).
- Gegenproben, lokal zurückgedreht: Aktivität immer aus dem Stand macht „Der Fehlbetrag rechnet mit dem Vortrag aus dem Stand“ und den Kassenbuch-Test rot. Stand in einer zweiten Transaktion macht den Transaktionstest rot.
- Angepasste Tests: In `BudgetLeadTests` heißt `read` jetzt „Nachziehen und Lesen“ (erst `observeMonthWith`, dann lesen) und bildet so das spätere Öffnen mit Nachziehen ab. Neu ist `readOnly`. „Lesen braucht genau einen FetchMonth“ wurde zu „höchstens einen, danach keinen“. Die offene Rückschreibung erwartet jetzt Verfügbar 120 und Zu verteilen 80 statt YNABs 100 und 100. Im Verbindungsfehler-Test bricht die zweite Verbindung statt der ersten ab. `TargetPauseTests.monthOk` beobachtet vor dem Lesen, weil die festen Monate in der geteilten DB einen fremden Stand tragen können. Die beiden YNAB-Fehlerfälle in `BudgetingTests` (k7wmd) nutzen eigene Monate (1987-03 und 1987-04) und legen das Schema an. `isolatedDb` und `CategoryLayoutFixture` legen die Tabellen des Stands an.
- Pipeline- und Negativtests für `getBudgetMonth` sind unverändert grün. `./verify.sh` in Iteration 1: `VERIFY: PASS` (dotnet 5512, node 357). Zwei frühere Läufe waren unter Last rot, jedes Mal in einem fremden Test: einmal „Replay aus dem Log ergibt denselben Zustand“ (Ledger), einmal der Layout-Sweep „targets-pruefen-nicht-neu-gelesen @ 412 px“. Beide waren ohne Änderung im nächsten Lauf grün, der Code dieser Task berührt sie nicht.

**README-Delta**: lokal mit `npm run check:readme-delta -- --bc budgeting` geprüft, Exit 0, „Ganz nachlesen: keine Einträge“, kein `--keep`. Die offene Frage zu Verfügbar und Zu verteilen zieht der Conductor von Hand nach (Iteration 3).

**Iteration 2 (Verifier-Note Iteration 1)**
- README-Delta vollständig: zusätzlich `replace` für **Budget-Seite** (der dgrp7-Satz „Aktivität, Verfügbar und Zu verteilen liest die Seite live aus YNAB … Jedes Lesen beobachtet den Monat“ fällt, an seiner Stelle das Verhalten nach ADR 0101), **Fehlbetrag** (auf der Seite Übertrag aus dem YNAB-Stand und Aktivität aus dem Kassenbuch, Vorschau und Targets anwenden mit dem frisch gelesenen YNAB-Monat, belegt an `previewWithLedger`), **Aktivität je Monat** (`monthActivityForPage` für alle Kategorien des Stands, vor der ersten Buchung und bei unlesbarem Kassenbuch die Aktivität aus dem Stand) und im Abschnitt Relationships **Lese-Naht zu `ledger`** (der Satz „Die Aktivität auf der Budget-Seite bleibt YNABs Wert (ADR 0065)“ fällt). Die README nach „live“ und „jedem Lesen“ durchsucht: übrig ist nur die Seite Kategorien (`GET /categories` bei jedem Betreten), die diese Task nicht berührt. Geprüft mit `npm run check:readme-delta -- --bc budgeting --delta …/m5pqn/readme-delta.json`: Exit 0, „Ganz nachlesen: keine Einträge“, kein `--keep`.
- `CopiedAt` im Pfad `NotRecorded`: `pageMonthOf` bekommt den YNAB-Monat und `copiedAt: DateTime option` getrennt; ohne festgehaltenen Stand ist `CopiedAt = None`, nach erfolgreichem Beobachten `Some now` (`src/Server/Budgeting/QueryHandlers.fs`). Neuer Test „Lässt sich der Stand beim ersten Lesen nicht festhalten, kommt der Monat ohne Zeitpunkt des Stands“ (`YnabMonthSnapshotTests.fs`, Trigger verhindert den Stand, Gegenprobe ohne Trigger `Some now`), vor dem Fix rot mit „Expected None, was Some(…)“.
- Layout-Zustand `targets-pruefen-nicht-neu-gelesen @ 412 px`: geprüft, ob die Task ihn beeinflusst. Der Zustand mockt nur `getTargetCheck` und `changeCategoryTarget`; `src/Client` ist unverändert, `TargetCheckData` ebenso, in `scripts/layout-states.mjs` kam nur `CopiedAt: null` in `budgetMonth` dazu (gleichwertig zu einem fehlenden Optionsfeld). „Neu laden“ lag in der roten Meldung bei y = −387, die Seite war also nach dem Klick auf „übernehmen“ gescrollt. Belege für reine Last: `scripts/layout-invariants.test.mjs` allein viermal hintereinander grün (28/28 je Lauf); derselbe Zustand war in ledger-enjex (fremder Branch, Detail ohne Toast) unter Last rot. Der erste `./verify.sh` dieser Iteration war bei Last 13 bis 16 wieder nur dort rot, der zweite grün.
- `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 5513 grün, node 357 grün).

**Iteration 3 (Verifier-Note Iteration 2, Entscheidung Conductor: Option 1)**
- Hinweis „Abweichung Kassenbuch/YNAB“ wieder sinnvoll: `BudgetCategory` trägt das neue Feld `ActivityInYnab: Money option` (`src/Shared/Budgeting.fs`), YNABs Aktivität des Monats aus dem YNAB-Stand. `Domain.withActivityInYnab` setzt es rein aus dem Stand, `QueryHandlers.pageMonthOf` ruft es auf (mit Stand aus dem gespeicherten Stand, beim ersten Lesen ohne Stand aus dem gerade gelesenen YNAB-Monat). Befehle setzen es nicht (`None`), sie liefern keinen Monat an die Seite. Im Client vergleicht `Inspector.categoryDeviationHint` die Summe der Buchungen mit `ActivityInYnab`; ohne das Feld (kein Stand, älterer Server) gibt es keinen Hinweis. `InspectorView.detailTotal` ruft nur noch diese Funktion. Aussehen unverändert.
- Regressionstests, vor dem Fix rot: „Die Seite trägt neben der Aktivität aus dem Kassenbuch YNABs Aktivität aus dem Stand“ (`YnabMonthSnapshotTests.fs`, Handler, erstes Lesen und Lesen aus dem Stand: Aktivität −12 aus dem Kassenbuch, `ActivityInYnab` −10) war rot mit `None`. „Weicht die Summe der Buchungen von YNABs Aktivität aus dem Stand ab, nennt der Hinweis beide“ und „Stimmt die Summe mit YNABs Aktivität aus dem Stand überein, gibt es keinen Hinweis“ (`BudgetPageTests.fs`, Summe und Abweichung) waren rot, solange die Funktion gegen `Activity` verglich (erst ohne Hinweis, dann mit „YNAB: −12,00 €“). Dazu „Ohne YNABs Aktivität gibt es keinen Hinweis“.
- Fixtures im selben Schritt: `ActivityInYnab: null` in allen Kategorie-Fixtures unter `scripts/` (9 Dateien). In `scripts/category-detail.test.mjs` ist die Kategorie jetzt wie vom echten Server: `Activity` −40 (Kassenbuch), Verfügbar 60, `ActivityInYnab` −50; der Flow-Test erwartet weiter „Kassenbuch: −40,00, YNAB: −50,00“ und ist grün. Alle F#-Konstruktionen von `BudgetCategory` in `src/Tests` tragen `ActivityInYnab = None`.
- README-Delta: zusätzlich `replace` für **Aktivität** (der Satz „Auf der Budget-Seite kommt sie vorerst von YNAB (`activity`), bis die Naht ledger/budgeting steht“ fällt), **Abweichung Kassenbuch/YNAB** (gegen YNABs Aktivität aus dem Stand, ohne „BB rechnet Verfügbar nicht selbst“) und **Aufbau von Verfügbar** („Alle Werte sind YNABs (ADR 0065)“ fällt). Die README nach „ADR 0065“, „nicht selbst“, „live“ und „YNABs Aktivität“ durchsucht: übrig bleibt **Ausgaben der letzten 6 Monate** („Weicht der Balken des gezeigten Monats von YNABs Aktivität ab, fehlt meist eine Spiegelung“), das stimmt weiter. `npm run check:readme-delta -- --bc budgeting --delta …/m5pqn/readme-delta-iter3.json`: Exit 0, „Ganz nachlesen: keine Einträge“, kein `--keep`.
- Offene Frage von Hand nachziehen (Conductor): Der Abschnitt `## Open questions` hat kein Bullet mit fettem Kopf, ein `replace` erreicht dort nichts, und ein Anker in einem anderen Abschnitt kann nicht über die Überschrift greifen. Das Bullet „- Rechnet BB Verfügbar und Zu verteilen selbst oder übernimmt es sie von YNAB und gleicht\n  ab? (open since 2026-10-25)“ wird ersetzt durch: „- ~~Rechnet BB Verfügbar und Zu verteilen selbst oder übernimmt es sie von YNAB und gleicht\n  ab?~~ Gelöst durch ADR 0101 (budgeting-m5pqn, 2026-11-21): Auf der Budget-Seite leitet BB Verfügbar und Zu verteilen selbst ab, aus Soll, Aktivität aus dem Kassenbuch und YNAB-Stand; der Hinweis Abweichung Kassenbuch/YNAB gleicht die Aktivität mit YNAB ab. (open since 2026-10-25)“
- `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 5517 grün, node 357 grün) im ersten Lauf.

**Nicht gebaut (Folgetasks)**: Das Merkmal „veraltet“ des Stands und das Nachziehen gehören zu budgeting-ffjk9. Fenster und Platzierung gehören zu budgeting-yx51n. Bis ffjk9 sieht BB eine Zuweisung in YNAB erst beim nächsten Befehl auf dem Monat oder nach der Übernahme im Hintergrund.

**Für die Kann-Sammlung budgeting**
- Viele Handler-Tests lesen feste Monate in der geteilten Test-DB. Seit es den Stand gibt, hängt ein reines Lesen davon ab, welcher Test den Monat zuletzt beobachtet hat. Betroffen ist zum Beispiel `TargetChangeTests` „Ein in YNAB geändertes Goal …“, der mit `port'` jetzt nichts mehr beobachtet. Eigene Monate oder Beobachten vor dem Lesen würde es festigen.
- Befehle planen auf YNABs Verfügbar, die Seite zeigt BBs abgeleitetes. Weichen Kassenbuch und YNAB ab, kann eine Prüfung auf Verfügbar im Befehl andere Zahlen sehen als die Seite.
- Layout-Zustand `targets-pruefen-nicht-neu-gelesen @ 412 px` ist unter Last wackelig (Seite nach „übernehmen“ gescrollt, „Neu laden“ außerhalb des Bildes); in m5pqn und ledger-enjex rot, allein immer grün. Ein stabileres `open` im Zustand würde das Gate festigen.
