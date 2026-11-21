---
id: budgeting-ffjk9
title: "Nachziehen beim Öffnen: Budget-Seite und Übersicht zeigen sofort BBs Stand und gleichen danach still mit YNAB ab (syncBudgetMonth, Frische 60 s und 5 min, spätere Monate veraltet)"
status: done
type: feature
context: budgeting
created: 2026-11-20
completed:
depends_on: [budgeting-m5pqn, budgeting-yx51n]
blocks: []
tags: [budget-seite, lesemodell, ynab, spiegel]
related_adrs: [0014, 0065, 0084, 0101]
related_research: []
prior_art: []
---

## Why
Teil 3 von 4 nach ADR 0101 (Ursprung budgeting-m5pqn). Ohne Beobachten im Lesepfad braucht BB einen anderen Ort, an
dem es Änderungen in YNAB sieht (ADR 0101 Punkt 6).

## What
- Endpoint `syncBudgetMonth` (Monat): beobachtet, wenn der Stand älter als 60 s oder als veraltet markiert ist; liest
  den Rahmen nach, wenn er älter als 5 min ist; liefert den Monat nur, wenn sich etwas geändert hat, sonst
  „unverändert“.
- Ein Budget-Befehl auf Monat M markiert die Monate nach M als veraltet.
- Nachziehen und Befehle auf demselben Monat laufen unter `MonthGuard` nacheinander. Die Echo-Regel aus ADR 0084
  Punkt 3 bleibt: eine Beobachtung gewinnt, das Soll wird YNABs Wert.
- Ausgelöst nach dem sofortigen Lesen beim Öffnen von Budget-Seite und Übersicht, gestartet wie
  `triggerMirrorInBackground` (ADR 0014), ohne Timer. Rate höchstens etwa 84/h, mit dem Kassenbuch-Spiegel (48/h)
  unter 200.
- Die Target-Übernahme, die heute beim Lesen läuft (`targetRulesBestEffort`), läuft hier nach bestem Bemühen.
- Client: nach `MonthLoaded` `syncBudgetMonth` aufrufen und still tauschen (kein `Loading`, kein Toast), ebenso in der
  Übersicht (`src/Client/Components/Overview/State.fs`). Scheitert das Nachziehen, steht der Hinweis „YNAB-Stand von
  <Uhrzeit>“ (F4).

## Acceptance criteria
- [ ] Handler-Test mit zählendem Port und eingespeister Uhr: Stand 59 s alt ergibt null Aufrufe, 61 s einen
      `FetchMonth`, ein veralteter Stand einen, unabhängig vom Alter.
- [ ] Handler-Test: bei unverändertem YNAB-Stand liefert `syncBudgetMonth` „unverändert“; hat YNAB ein anderes
      Zugewiesen, steht `AssignmentChangeObservedInYnab` im Stream und der Monat kommt mit YNABs Wert als Soll.
- [ ] Handler-Test: ein Zuweisen in M markiert M+1 und M+2 als veraltet, M−1 nicht.
- [ ] Handler-Test: Nachziehen und Befehl auf demselben Monat laufen in beiden Reihenfolgen nacheinander
      (`MonthGuard`); keine Reihenfolge erzeugt eine falsche Beobachtung, kein älterer Stand überschreibt einen
      neueren.
- [ ] Client-`update` unter .NET, Aufzählung aller Reihenfolgen von `MonthLoaded`, Sync-Antwort, `SelectMonth` und
      `PageLeft` (Doktrin Punkt 20, Vorbild `moveWays`): eine Sync-Antwort für einen anderen als den gezeigten Monat
      wird verworfen; „geändert“ tauscht Beträge, ohne dass `Month` je `Loading` wird; eine offene Eingabe im Feld
      Zugewiesen bleibt erhalten.
- [ ] Reine Funktion für den Hinweistext; Client-`update`-Test: ein Fehlschlag des Nachziehens zeigt
      „YNAB-Stand von <Uhrzeit>“, ein Erfolg zeigt nichts.
- [ ] Zeile in `RemotingEndpointNegativeTests` (ungültiger Monat, fehlende Einrichtung), Aufruf in
      `RemotingPipelineTests`.
- [ ] README budgeting nachgezogen, Delta mit `npm run check:readme-delta` geprüft.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.

## Notes
- Entschieden (Roman, 2026-11-20), F4: Hinweis „YNAB-Stand von <Uhrzeit>“ nur, wenn das Nachziehen gescheitert ist,
  sonst nichts zeigen. Ort und Form des Hinweises gegen `standards/frontend/styleguide.md` prüfen
  (Hinweis am Ort, kein Toast).
- Der `TakeoverWorker` bleibt auf die Übernahme beschränkt.
- Stand nach dem Bau der Vorgänger (2026-11-21): der Monat steht als `YnabMonthSnapshot` (`ynab_month_snapshot`,
  `FetchedAt`, einziger Schreiber `observeUnguarded` über `recordObservationOn`), der Rahmen als `YnabMonthWindow` und
  `YnabCategoryCatalog` mit `FetchedAt` (`holdMonthWindowOn`, `holdCategoryCatalogOn`, Port `FetchCategoryPlacements`).
  „Rahmen älter als 5 min“ heißt: diese beiden `FetchedAt`. Seit budgeting-yx51n öffnet eine bestätigte Zuweisung im letzten
  Monat den nächsten im gehaltenen Fenster (`monthOpenedBy`). Siehe Outcomes von budgeting-m5pqn und budgeting-yx51n.
- Kann-Sammlung budgeting, Review-Lauf 2026-11-21: `checkedWrite` (Seite Kategorien) ruft nach dem Schreiben ohnehin
  `RefreshMirror` (`GET /categories`); daraus ließe sich der Katalog ohne weiteren Aufruf festhalten, sonst erscheint eine
  neu angelegte oder verschobene Kategorie bis zu 5 min später in der Budget-Liste. Nicht Teil dieser Task, außer Roman
  nimmt es auf.

## Outcome

Budget-Seite und Übersicht zeigen sofort BBs Stand und ziehen danach still mit YNAB nach (ADR 0101 Punkt 6, Teil 3 von 4).

**Was gebaut ist**
- Endpoint `BudgetApi.syncBudgetMonth` (`src/Shared/Api.fs`), Antwort `Shared.Budgeting.BudgetMonthSync` (`MonthUnchanged` oder `MonthChanged of BudgetMonth`). Der neue Fall ist ein Union-Typ als Antwort eines neuen Endpoints, kein Pflichtfeld in einem bestehenden Record. Bestehende Fixtures mussten nur den neuen Aufruf mocken.
- Rein in `Server.Budgeting.Domain`: `YnabMonthSnapshot.Stale`, `monthSyncDue` (ohne Stand, veraltet oder älter als 60 s; genau 60 s gilt als frisch) und `frameStateDue` (älter als 5 min).
- Persistenz: Spalte `stale` in `ynab_month_snapshot` (per `ALTER TABLE` wiederholbar, ein alter Stand gilt als nicht veraltet). Jedes Beobachten schreibt `stale = 0`. `markMonthsAfterStaleOn` markiert alle Monate nach M. Aufgerufen wird es in `recordWriteBackOn` nach dem PATCH, nach bestem Bemühen mit Log, also bei Zuweisen, Verschieben, Targets anwenden und Erneut senden.
- `CommandHandlers.observeIfDueWith`: unter `MonthGuard` den Stand lesen und nur bei Fälligkeit beobachten (`observeUnguarded`, Echo-Regel unverändert).
- `QueryHandlers.syncMonthWithLedger`/`syncMonth`: beobachtet, liest Fenster und Platzierung je Teil nach, wenn sie älter als 5 min sind (`refreshIfDue`, bestes Bemühen), und führt danach `targetRulesBestEffort` aus. `MonthUnchanged` kommt, wenn der Stand frisch war oder YNABs Monat und BBs Targets gleich blieben, sonst `MonthChanged` mit `pageMonthOf`.
- `Api.syncBudgetMonthFor` (Monat an der Grenze prüfen, YNAB-Port und Rahmen-Port) und das Feld in `budgetApiWith`.
- Client Budget-Seite: Nach jeder Antwort `Loaded` oder `Reloaded`, die zur laufenden Abfrage gehört und einen Monat bringt, startet `syncBudgetMonth` (`Month.startYnabSync`, `State.syncCmd`, über `Retry.reading`). Die Antwort `MonthSynced (generation, month, result)` verarbeitet `Month.applyYnabSynced`. Sie wird verworfen, wenn der Monat ein anderer ist, ein Wechsel läuft oder die Generation überholt ist. „Geändert“ tauscht den Monat über `applyLoaded` mit derselben Generation: kein `Loading`, die Schlange gilt weiter, `TabMoney` bleibt. Ein Fehlschlag setzt `YnabSync = SyncFailed`. Den Hinweis liefert `Month.ynabSyncHint`, den Text `Month.ynabCopyText` („YNAB-Stand von HH:mm“ in Ortszeit). Gezeigt wird er unter „Zu verteilen“ mit `Feedback.stateHint` (`budget-ynab-copy`) und „Neu laden“ (`RetryYnabSync`), am selben Ort und in derselben Form wie „nicht neu gelesen“ (Styleguide §7.13: ein Zustand der Seite mit Aktion). Ohne `CopiedAt` erscheint kein Hinweis.
- Übersicht (`Components/Overview/State.fs`): Nach `MonthLoaded (Ok)` startet das Nachziehen, `MonthSynced` tauscht still nur den gezeigten Monat. Einen Hinweis bei Fehlschlag gibt es dort bewusst nicht: die Task nennt den Hinweis nur für die Budget-Seite.

**Tests** (22 neu)
- `src/Tests/BudgetMonthSyncTests.fs` (12): rein 59, 60 und 61 s, veraltet, Rahmen 5 min. Handler mit zählendem Port und eingespeister Uhr: 59 s null Aufrufe, 61 s ein `FetchMonth`, veraltet einer unabhängig vom Alter, danach null. Ein Monat ohne Stand ergibt einen Aufruf und „geändert“. Der Rahmen wird nach 4 min nicht gelesen, nach 6 min je einmal und neu festgehalten. Ein Rahmenfehler hält den Monat nicht auf. Unverändertes YNAB ergibt `MonthUnchanged`; YNAB 50 statt 30 ergibt `AssignmentChangeObservedInYnab` (30 → 50) und Soll 50 mit neuem `CopiedAt`. Ein YNAB-Fehler kommt als Fehler, der alte Stand bleibt. Zuweisen in M markiert M+1 und M+2, nicht M−1 und nicht M; M+1 wird danach bei 6 s Alter beobachtet. Beide Reihenfolgen unter `MonthGuard` (Tor im Fake-Port, `waitingFor`): keine falsche Beobachtung, der neuere Stand bleibt (FetchedAt und YNABs 40). API-Weg `budgetApiWith.syncBudgetMonth`: frisch null Aufrufe, Gegenbeweis ohne Stand ein `FetchMonth`.
- `src/Tests/BudgetMonthSyncClientTests.fs` (6): `ynabCopyText` für Winter- und Sommerzeit. Das Nachziehen startet nach dem Lesen, nicht nach einem Fehler und nicht nach einer überholten Antwort. Ein Fehlschlag zeigt den Hinweis mit Grund, ein Erfolg nichts. Ohne `CopiedAt` gibt es keinen Hinweis. „Neu laden“ startet einen Aufruf, ein zweiter Tipp keinen. Aufzählung bis Tiefe 6 nach Doktrin Punkt 20 über November, Dezember, Seite weg, Seite da, Tippen, Gelesen, Sync geändert, Sync unverändert, Sync gescheitert und Sync anderer Monat, mit eigenem Merker für die Gültigkeit. Geprüft wird: `Month` wird nie `Loading`, die Eingabe bleibt, eine Antwort für einen anderen Monat oder eine überholte wird verworfen, „geändert“ tauscht Zugewiesen und Zu verteilen (eine gespeicherte Zuweisung der Schlange gilt weiter), und der Hinweis erscheint nur nach einem Fehlschlag. Über 50 Tausche erreicht.
- `OverviewTests.fs` (2), eine Zeile in `RemotingEndpointNegativeTests` (ungültiger Monat, fehlende Einrichtung), ein Aufruf in `RemotingPipelineTests` (`/api/BudgetApi/syncBudgetMonth` durch `Api.webApp`).
- Angepasst: `YnabMonthSnapshotTests` (`Stale = false` in zwei erwarteten Ständen). `RetryTests`: Inventar der lesenden Abfragen um `syncBudgetMonth` ergänzt; das Nachziehen ist wiederholbar, ein zweiter Versuch entscheidet nichts.
- Layout: neuer Zustand `budget-ynab-stand-alt` in `scripts/layout-states.mjs` bei 412 und 1440 px (`mockBudgetSyncFails`: Monat mit `CopiedAt`, Nachziehen mit Fehler, kein Toast). `mockApp` mockt `syncBudgetMonth` mit `MonthUnchanged`, weil der Sweep ungemockte Aufrufe ablehnt. Andere Browser-Tests antworten auf ungemockte Aufrufe mit 500. Ihre Fixtures tragen `CopiedAt: null`, also bleibt der Hinweis dort aus.
- Gegenproben, lokal zurückgedreht und wiederhergestellt: (1) Nachziehen ohne `MonthGuard` macht 2 Reihenfolge-Tests rot. (2) Keine Markierung in `recordWriteBackOn` macht den Test „M+1 und M+2“ rot. (3) Client ohne Monatsprüfung, (4) ohne Generationsprüfung und (5) Tausch mit `TabMoney = None` machen jeweils die Aufzählung rot. Die Aussage gilt nur für diese fünf Mutanten.
- Live-Port-Stellen nach Doktrin Punkt 3: `budgetApiWith.syncBudgetMonth` → `syncBudgetMonthFor` → `QueryHandlers.syncMonth` (Datenbank und Kassenbuch der Anwendung) belegt der API-Test mit Gegenbeweis. `liveBudgetApi` → `budgetApiWith … livePort liveFramePort` ist unverändert und durch den bestehenden Strukturwächter belegt.
- `./verify.sh`: Lauf 3 `VERIFY: PASS` (dotnet 5605, node 358). Lauf 1 war rot im fehlenden Retry-Inventar (behoben) und in „Ledger Spiegel-Lauf … Replay reproduziert die Verknüpfung“; das ist der bekannte Flake mit fremden Einträgen, Lauf 2 war er grün. Lauf 2 war nur in `[flow:payee-aendern]` (Kassenbuch, `scripts/ledger-payee.test.mjs`) rot. Diesen Code berührt die Task nicht, in Lauf 1 und 3 war der Test grün.

**README-Delta**: `replace` für **Budget-Seite**, **Monatsfenster**, **Echo** („künftig beim Nachziehen“ fällt), **YNAB-Stand** (der Satz „kommt mit budgeting-ffjk9; bis dahin … erst beim nächsten Befehl“ fällt), **Stand des Rahmens** („kommt mit budgeting-ffjk9“ fällt) und unter Key commands **Beobachten**. `append` für **Nachziehen**, **Veralteter YNAB-Stand**, **Hinweis „YNAB-Stand von <Uhrzeit>“** und **MonatNachziehen**. Die README nach „ffjk9“, „Nachzieh“, „veraltet“, „nächsten Befehl“, „erst beim“ und „YNAB-Stand“ durchsucht: alle Treffer zu ffjk9 stehen in den ersetzten Bullets. **Veralteter Monat** (Client, budgeting-c5w2n) passt, **YNAB** unter Actors („übernimmt BB das als Beobachtung“) passt. `npm run check:readme-delta -- --bc budgeting --delta …/scratchpad/ffjk9/readme-delta.json`: Exit 0, „Ganz nachlesen: keine Einträge“, kein `--keep`.

**Für die Kann-Sammlung budgeting**
- Wettlauf über Monate: Liest ein Nachziehen von M+1 YNAB vor dem PATCH auf M und schreibt seinen Stand nach der Markierung, nimmt es die Markierung mit einem alten Vortrag. M+1 zeigt den alten Vortrag dann bis zum nächsten Öffnen nach 60 s. Abhilfe wäre ein Zähler je Markierung, den das Beobachten vergleicht. Selten (Befehl auf M, während M+1 gerade nachzieht), es geht kein Geld verloren.
- Die Übersicht zeigt keinen Hinweis, wenn das Nachziehen scheitert.
- Das Nachziehen des Rahmens läuft wie das erste Festhalten (yx51n) nicht unter einer Sperre. Ein Nachlesen kann ein gerade per Zuweisung geöffnetes Fenster überschreiben, wenn YNAB den Monat beim Lesen noch nicht offen hatte.
