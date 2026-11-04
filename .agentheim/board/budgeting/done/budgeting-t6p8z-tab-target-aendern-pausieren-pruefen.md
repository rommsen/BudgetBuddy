---
id: budgeting-t6p8z
title: "Target pausieren: Pause je Kategorie und Monat in BB, wirkt in Fehlt, Frei, Vorschau und Targets anwenden, endet mit dem Monat, Knopf im Tab Geld"
status: done
type: feature
context: budgeting
created: 2026-11-02
completed:
depends_on: [design-system-001, budgeting-dyn8g, budgeting-v4ht9]
blocks: [budgeting-k8wq3, budgeting-p2zr6, budgeting-m3x7f, budgeting-v6m3z]
tags: [budget, target, pausieren]
related_adrs: [0065, 0069, 0073, 0074, 0079]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-h2zfn, budgeting-c5w2n, budgeting-dyn8g]
---

## Why
Teil A von budgeting-t6p8z (Tab Target, Task 4 von 5 des Gesamtkonzepts). Roman will ein Target für einen Monat aussetzen, etwa wenn eine Ausgabe in diesem Monat entfällt. YNAB lässt `goal_snoozed_at` nicht schreiben, deshalb liegt die Pause in BB (ADR 0073). Bisher beachtet BB nur Pausen aus YNAB. Die Teile B (budgeting-k8wq3, Target ändern und entfernen), C (budgeting-xp46a, Target anlegen und Art wechseln) und D (budgeting-p2zr6, "Passt das Target noch?") sind abgetrennt. A schreibt nichts nach YNAB.

## What
Regeln: `docs/ux/budget/konzept.md`, Abschnitt Targets, ADR 0073, ADR 0079 (Pause als Ereignisfolge in BB).

**Zustand und Persistenz**
- Neue Tabelle `target_pause_events` (id, occurred_at, month, category_id, category_name, kind), append-only mit Triggern wie `budget_movements`. `kind` ist "Pausiert" oder "Fortgesetzt", ein unbekannter Wert beim Lesen wird gemeldet (`failwith`), nicht auf einen Standard gelegt. Fachlicher Schlüssel ist (month, category_id), der Stand ist das jüngste Ereignis. Angelegt in `initializeBudgetingSchema` mit `CREATE TABLE IF NOT EXISTS`, wiederholbar auf bestehender Datenbank.
- Die Pausen eines Monats liest eine Abfrage für alle Kategorien zugleich (kein N+1).

**Domäne (Shared und Server)**
- `BudgetCategory.Snoozed: bool` wird `Pause: PauseSource option` mit `PausedInYnab | PausedInBb | PausedInBoth`. Dazu `isPaused` in `Shared.Budgeting`. `YnabMonthCategory.Snoozed` bleibt der rohe YNAB-Wert.
- `toBudgetMonth` bekommt die BB-Pausen des Monats als Parameter (`Set<Guid>`) und setzt `Pause`. Das ist die einzige Stelle, an der YNAB-Monat und BB-Pause zusammenkommen (heute aufgerufen in `QueryHandlers` und an drei Stellen in `CommandHandlers`).
- `missingAmount`, `freeAmount`, `planTargetApplication` (Skipped), `targetUnderfunded`, `targetStatusText` und `listStatusText` fragen nur `isPaused`.
- Befehl **TargetPausieren** und **PauseAufheben** über einen Endpoint `setTargetPause: string -> TargetPauseRequest -> Async<Result<PauseSource option, string>>` (Monat, `CategoryId`, `Paused: bool`). Er läuft unter der Sperre je Monat (wie Targets anwenden, Verschieben, Zuweisen), liest den Monat frisch aus YNAB und prüft: Monat nicht vergangen, Kategorie sichtbar im Monat, Kategorie hat ein Target. Doppeltes Pausieren oder Aufheben schreibt keine Zeile. Aufheben geht nur für eine BB-Pause, eine YNAB-Pause bleibt (ADR 0073 Punkt 2).
- Wird das Target später entfernt (budgeting-k8wq3), bleibt die Pause gespeichert und wirkt nicht, weil ohne Target kein Fehlbetrag zählt.

**Historie**
- `CategoryHistory.Movements` wird `Entries: HistoryEntry list` mit `Movement of HistoryMovement | TargetPaused of ... | PauseLifted of ...`, neueste zuerst. `HistoryLine.Change` wird `Money option`, eine Pause hat keinen Betrag. Text "Target pausiert" bzw. "Pause aufgehoben", Stand immer geschrieben (eine Pause geht nicht nach YNAB). `MovementFilter` und `queryMovements` bleiben für budgeting-h7q4v nutzbar.

**Client**
- Knopf "Target pausieren" im Tab Geld, Modus Zuweisen, auf dem frei gelassenen Platz (`MovesView.moneyMode`). Bei `PausedInBb` heißt er "Pause aufheben". Bei `PausedInYnab` steht statt des Knopfs ein Satz, dass die Pause in YNAB gesetzt ist und nur dort aufgehoben wird. Bei `PausedInBoth` hebt der Knopf die BB-Pause auf, der Satz zur YNAB-Pause steht dabei. Ohne Target fehlt der Knopf. Im vergangenen Monat fehlt er, sein Platz bleibt frei (wie Targets anwenden).
- Neutraler Knopf (`Button.secondary`, ADR 0075 Punkt 2), Orange bleibt der Fehlbetrag-Knopf. Sichtbarkeit und Beschriftung als reine Funktion (Vorbild `assignKeyAction`), die View ruft sie nur auf.
- Tab Target zeigt "pausiert" mit Quelle (in BB, in YNAB) und denselben Knopf.
- Die Antwort setzt die Pause der Kategorie im geladenen Monat (reine Funktion, Vorbild `withReport`), ohne YNAB neu zu lesen: die Pause ändert in YNAB nichts. Fehlt, Frei, Filterzahlen und der Fehlbetrag-Knopf folgen sofort. Die Historie ist danach wieder fällig (wie nach einer Bewegung).
- Fehler: konkrete Meldung im Tab, nichts geändert.

**Laufzeitfalle:** `Snoozed` wird umbenannt. Die Fixtures in `scripts/budget-page.test.mjs`, `targets-apply.test.mjs`, `category-detail.test.mjs`, `move-money.test.mjs` und `budget-money-tab.test.mjs` im selben Schritt mitziehen.

## Acceptance criteria
**Domain**
- [ ] (Domain) `toBudgetMonth` mit BB-Pausen: Kategorie nur in BB pausiert ergibt `PausedInBb`, nur in YNAB `PausedInYnab`, beides `PausedInBoth`, keins `None`. Eine BB-Pause für eine Kategorie, die im Monat fehlt, ändert nichts.
- [ ] (Domain) `missingAmount` und `freeAmount` bei `PausedInBb` wie bei der YNAB-Pause: Fehlbetrag zählt nicht, negatives Verfügbar bleibt in Fehlt. Je ein Fall für jede der drei Quellen.
- [ ] (Domain) `planTargetApplication`: eine in BB pausierte Kategorie mit Fehlbetrag größer 0 steht in `Skipped`, nicht in `Lines`.
- [ ] (Domain) Sichtbarkeit und Text des Pause-Knopfs: ohne Target keiner, vergangener Monat keiner, künftiger Monat wie der laufende, `None` "Target pausieren", `PausedInBb` "Pause aufheben", `PausedInYnab` kein Knopf mit Satz, `PausedInBoth` "Pause aufheben" mit Satz.
- [ ] (Domain) Historie-Zeile für Pausiert und Fortgesetzt: Text, kein Betrag, Stand geschrieben.

**Handler (In-Memory-SQLite, YNAB als Port)**
- [ ] GIVEN Kategorie mit Target im aktuellen Monat WHEN pausieren THEN ein Ereignis "Pausiert", `getBudgetMonth` liefert `PausedInBb`, Fehlt ohne Fehlbetrag. Kein Schreibaufruf an den YNAB-Port.
- [ ] GIVEN BB-Pause im Monat M WHEN `getBudgetMonth` für M+1 THEN keine Pause (endet mit dem Monat, wird nicht übertragen).
- [ ] GIVEN BB-Pause WHEN noch einmal pausieren THEN keine neue Zeile. GIVEN keine BB-Pause WHEN aufheben THEN keine Zeile.
- [ ] GIVEN `PausedInBoth` WHEN aufheben THEN Ereignis "Fortgesetzt", Ergebnis `PausedInYnab`.
- [ ] GIVEN vergangener Monat WHEN pausieren THEN Fehler, keine Zeile, kein YNAB-Aufruf. GIVEN künftiger Monat im Fenster WHEN pausieren THEN Ereignis wie im laufenden Monat. GIVEN Kategorie ohne Target oder ausgeblendet THEN Fehler mit konkreter Meldung, keine Zeile.
- [ ] GIVEN Vorschau mit Kategorie K in `Lines` WHEN K wird pausiert und dann Übernehmen mit der alten Vorschau THEN `PreviewChanged`, nichts geschrieben; die neue Vorschau führt K in `Skipped`. Übernehmen liest die Pausen innerhalb der Sperre je Monat.
- [ ] GIVEN eine laufende Anwendung für Monat M WHEN gleichzeitig pausieren in M THEN es wartet auf die Sperre (Wächter-Test wie bei Targets anwenden).
- [ ] `getCategoryHistory` liefert Bewegungen und Pause-Ereignisse der Kategorie im Monat gemischt, neueste zuerst.
- [ ] Die Tabelle ist append-only: UPDATE und DELETE auf `target_pause_events` werden abgewiesen. `initializeBudgetingSchema` zweimal auf derselben Datenbank läuft ohne Fehler.

**Validation und API**
- [ ] (Validation) `setTargetPause`: ungültiger Monat und leere Kategorie-Id werden zusammen gemeldet.
- [ ] Neuer Endpoint: Zeile in `RemotingEndpointNegativeTests.fs` (ungültiger Monat, vergangener Monat, unbekannte Kategorie: Fehler statt Ausnahme). `setTargetPause` im Retry-Inventar von `RetryTests.fs`, falls die anderen BudgetApi-Endpoints dort stehen.
- [ ] Liste der Live-Anschlüsse des neuen Ports (`live*`, `budgetApiWith`, `Program.fs`) im Outcome, je mit Beleg (Doktrin Punkt 3).

**Client-`update`**
- [ ] GIVEN Kategorie mit Fehlbetrag WHEN Pause bestätigt THEN Fehlt der Kategorie leer, Filterzahl Fehlt sinkt, Fehlbetrag-Knopf verschwindet, kein Neu-Lesen des Monats, Historie wieder fällig.
- [ ] GIVEN Pause-Aufruf läuft WHEN Kategorie- oder Monatswechsel THEN die Antwort wirkt nur auf ihre Kategorie und ihren Monat; ein zweiter Tipp während des Aufrufs sendet nicht doppelt (Aufzählung der Reihenfolgen nach Doktrin Punkt 20, Vorbild `moveWays`).
- [ ] GIVEN Fehler vom Server THEN Meldung im Tab, Pause unverändert.

**Sonstiges**
- [ ] Kein Browser-Test: Pausieren ist ein Knopf ohne eigenen Fluss, alles ist unter .NET prüfbar. Die Playwright-Fixtures der fünf Dateien tragen das neue Feld `Pause`, `npm run test:scripts` ist grün.
- [ ] README budgeting: **Pausiert** neu gefasst (Quelle BB, YNAB, beide; endet mit dem Monat; Aufheben nur in BB), Key commands TargetPausieren und PauseAufheben, Key events TargetPausiert und PauseAufgehoben, Tab Historie mit Pause-Zeilen. Delta vorher mit `npm run check:readme-delta` geprüft.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Verfeinert am 2026-11-04 (Orchestrator mit tactical-modeler und architect). t6p8z wird Teil A, wie bei budgeting-dyn8g und budgeting-h7q4v behält die erste Teilaufgabe die Id. budgeting-m3x7f hängt danach an t6p8z und budgeting-p2zr6.

**Entscheidungen (Roman, 2026-11-04):** F1 a (Pause aufheben in BB), F2 a (nur mit Target, auch wenn erfüllt), F3 a (Pause in der Historie, eigene Tabelle). F4 betrifft nur Target ändern, anlegen und entfernen (nur laufender Monat, budgeting-k8wq3). Pausieren bleibt nach ADR 0073 Punkt 5 im laufenden und in künftigen Monaten erlaubt; dafür gilt weiter `targetsAllowed`, nicht `targetEditAllowed`.

budgeting-v6m3z hängt an dieser Task (Roman, 2026-11-04), weil beide `InspectorView.historyTab` und die Historie-Typen anfassen und sonst ein Merge-Konflikt ohne Textkonflikt droht (siehe Verifier-Note in budgeting-dyn8g). v6m3z baut auf dem Stand dieser Task auf.

Bausteine: `MonthGuard` in `CommandHandlers.fs`, `withReport` und `afterMovement` im Client, `assignKeyAction` als Vorbild für die reine Sichtbarkeitsfunktion.

## Hinweise aus dem Review von budgeting-g8s3w
`matchesFilter` (`Month.fs:376`, früher als `Types.fs:409` genannt) sieht nur die Kategorie. Entschieden in budgeting-p2zr6: die Prüfung kommt als Feld an `BudgetCategory`. Diese Task ändert `matchesFilter` nicht über `isPaused` hinaus.

## Outcome

Target pausieren und Pause aufheben sind gebaut (ADR 0073, ADR 0079). `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3616 grün, 10 opt-in übersprungen, node 288 grün, Build und beide Wächter grün). `npm run check:readme-delta -- --bc budgeting` endet mit Exit 0. Nichts geht nach YNAB, kein Live-Aufruf, `RUN_INTEGRATION_TESTS` nicht gesetzt.

**Shared** (`src/Shared/Budgeting.fs`, `src/Shared/Api.fs`): `PauseSource` (`PausedInYnab | PausedInBb | PausedInBoth`) mit `pauseSourceOf`, `pausedInBb`, `pausedInYnab`; `BudgetCategory.Snoozed` ist jetzt `Pause: PauseSource option`, dazu `isPaused`. `missingAmount` und `freeAmount` fragen nur `isPaused`. `YnabMonthCategory.Snoozed` bleibt der rohe YNAB-Wert. Neu sind `TargetPauseRequest`, `PausePastMonthText`, `HistoryPause`, `HistoryEntry` (`Movement | TargetPaused | PauseLifted`), `historyEntryTime`, `historyEntryLine`. `CategoryHistory.Movements` heißt `Entries`; `HistoryLine.MovementId` heißt `EntryId`, `Change` ist `Money option`. Neuer Endpoint `BudgetApi.setTargetPause: string -> TargetPauseRequest -> Async<Result<PauseSource option, string>>`.

**Server:** `Domain.toBudgetMonth (pausedInBb: Set<Guid>)` ist die einzige Stelle, an der YNAB-Monat und BB-Pause zusammenkommen; `planTargetApplication` fragt `isPaused`. Neu: `PauseKind` (`Paused`/`Resumed`) mit `pauseKindText`/`pauseKindOfText` ("Pausiert"/"Fortgesetzt"), `planPause` (sichtbar, mit Target, kein Ereignis bei doppeltem Pausieren oder Aufheben ohne BB-Pause, YNAB-Pause bleibt), `mergeHistoryEntries`. `Persistence`: Tabelle `target_pause_events` mit Index und zwei Triggern gegen UPDATE und DELETE in `initializeBudgetingSchema` (`IF NOT EXISTS`), `appendPauseEvent`, `queryPausedCategories`/`loadPausedCategories` (eine Abfrage je Monat für alle Kategorien, jüngstes Ereignis je Kategorie), `queryPauseEvents`/`loadPauseEvents`. Eine unbekannte Art wirft beim Lesen (`failwith`). `CommandHandlers.fetchBudgetMonth` liest Monat und BB-Pausen und wird von Vorschau, Übernehmen (in der Sperre), Verschieben und Zuweisen genutzt; `setTargetPause` lehnt den vergangenen Monat vor dem Lesen ab und läuft sonst unter `MonthGuard`. `QueryHandlers.budgetMonth` liest die Pausen des Monats, `HistoryPort.LoadPauseEvents` ist neu, `categoryHistory` mischt Bewegungen und Pausen. `Validation.validateTargetPauseRequest` meldet ungültigen oder vergangenen Monat und leere Kategorie zusammen. `Api.setTargetPauseWith` und die Zeile in `budgetApiWith`.

**Client:** neues Modul `src/Client/Components/Budget/Pause.fs`: `pauseControl` (reine Sichtbarkeit nach dem Vorbild `assignKeyAction`: `NoPauseControl` ohne Target, `PauseSlotFree` im vergangenen Monat, sonst Knopf und Satz zur YNAB-Pause), `startPause` (kein zweiter Aufruf, solange einer läuft, nicht im wechselnden Monat), `withPause` (setzt die Pause im geladenen Monat, Vorbild `withReport`), `applyPauseAnswered`, `pauseAnswerShown`, `pauseNeedsReread`, `syncPause`. `Model.PauseCall` (`PauseIdle | PauseRunning | PauseRejected`) trägt Kategorie und Monat und sperrt weder Auswahl noch Monatswechsel. Nachrichten `SetTargetPause` und `TargetPauseAnswered`. Nach einer gezeigten Antwort ist die Historie wieder fällig (`afterMovement`). Läuft beim Eintreffen ein Neu-Lesen des Monats (`MonthStale`), das vor der Pause begann, wird der Monat einmal neu gelesen, sonst nähme dessen Antwort die Pause zurück; im Normalfall liest der Client YNAB nicht neu. `MovesView.pauseControlView` (Button.Secondary, Platz im vergangenen Monat `invisible` wie Targets anwenden, Fehler als `ErrorDisplay.cardCompact`) steht im Modus Zuweisen des Tabs Geld und im Tab Target. Der Tab Target zeigt "pausiert in BB", "pausiert in YNAB" oder "pausiert in BB und YNAB", die Liste weiter "pausiert". Der Tab Historie zeigt Pausen ohne Betrag.

**Tests (35 neu):** `src/Tests/TargetPauseTests.fs` (Domain: Zusammenführen je Quelle und fehlende Id, Fehlt und Frei je Quelle, Skipped bei BB-Pause, Historie-Zeilen, Art-Text; Handler gegen In-Memory-SQLite und Fake-YNAB: Pausieren ohne Schreibaufruf, Folgemonat ohne Pause, doppelt und Aufheben ohne Zeile, Both zu YnabOnly mit "Fortgesetzt", nur YNAB-Pause bleibt, vergangener Monat ohne Lesen, künftiger Monat, ohne Target, ausgeblendet und unbekannt, PreviewChanged nach Pause, Sperre je Monat, gemischte Historie; Persistenz: Trigger und zweimalige Schema-Initialisierung, unbekannte Art gemeldet; Grenze: Fehler gesammelt, vergangener Monat, Live-Anschluss, Monatsart und Uhr über `budgetApiWith`). `src/Tests/TargetPauseClientTests.fs` (Sichtbarkeit je Monatsart und Quelle, Ablauf durch das echte `update` inklusive Filterzahl, Fehlbetrag-Knopf, Historie fällig, Fehler im Tab, erneutes Lesen bei `MonthStale`, und eine Aufzählung aller Reihenfolgen bis Tiefe 5 aus Tipp, Kategoriewechsel, Monatswechsel mit Antwort des Servers und Pause-Antwort, mit eigenem Orakel nach Doktrin Punkt 20). Eine Zeile `BudgetApi.setTargetPause` in `RemotingEndpointNegativeTests.fs` (ungültiger Monat, vergangener Monat ohne Einrichtung, unbekannte Kategorie mit Fake-Port). `setTargetPause` in der Token-Budget-Verdrahtung in `TargetsApplyTests.fs`. Ein Test in `BudgetPageTests.fs` für die Quelle im Tab Target. Angepasst wurden die übrigen Budget-Tests (`Pause` statt `Snoozed`, `toBudgetMonth Set.empty`, `Entries`). `RetryTests.fs` nicht: das Inventar führt nur lesende Aufrufe über `Retry.reading`, `setTargetPause` schreibt und läuft wie `moveMoney` und `assignMoney` nicht darüber.

**Playwright-Fixtures:** `Pause: null` (bzw. `"PausedInYnab"` in `withTarget`) in `budget-page.test.mjs`, `targets-apply.test.mjs`, `category-detail.test.mjs`, `move-money.test.mjs`, `budget-money-tab.test.mjs`; die Historie-Fixture in `budget-page.test.mjs` trägt `Entries: [{ Movement: ... }]`. Kein neuer Browser-Test.

**Live-Anschlüsse des Endpoints (Doktrin Punkt 3):**
- `Api.livePort` (`FetchMonth = YnabClient.getMonth`): derselbe Port wie bei Targets anwenden, Verschieben und Zuweisen; `setTargetPause` nutzt nur `FetchMonth`. Beleg: "Jeder Endpunkt reicht Token und Budget in dieser Reihenfolge an den Port" (`TargetsApplyTests.fs`, jetzt mit `setTargetPause`).
- `budgetApiWith.setTargetPause` (Einzeiler auf `setTargetPauseWith` mit `today ()` und `utcNow ()`): Beleg "Über die BudgetApi: pausieren mit Uhr und Monatsart aus Berlin" (November in Berlin vergangen, Dezember pausiert, Stempel aus der Uhr).
- `liveBudgetApi`: Beleg "Der Live-Anschluss liest Token und Budget für Pausieren aus den Settings".
- `budgetApi` in `Api.webApp` (`Remoting.fromValue (budgetApi berlinZone)`), dahinter `Program.fs` unverändert; `initializeBudgetingSchema` läuft dort beim Start wie bisher und legt die neue Tabelle an. Belegt über den bestehenden Pipeline-Test für `BudgetApi` und die Negativzeile.
- Die Pausen liest der Server wie `appendMovements` direkt über `Budgeting.Persistence` (kein Port), Tests mit In-Memory-SQLite und frischen Kategorie-Ids.

**Test zuerst, ehrlich:** Die Typänderung (`Snoozed` zu `Pause`, `Movements` zu `Entries`) musste auf einmal fallen; Produktion und Tests sind danach entstanden, nicht Kriterium für Kriterium rot vor grün. Rot ist per Gegenprobe belegt, je Mutante einzeln gesetzt und zurückgenommen: `withPause` ohne Monatsvergleich (Reihenfolgen rot), `startPause` ohne Sperre bei laufendem Aufruf (Reihenfolgen rot), `queryPausedCategories` ohne "jüngstes Ereignis" (Both zu YNAB rot), `setTargetPause` ohne Prüfung des vergangenen Monats (vergangener Monat rot). Die Aussage gilt nur für diese vier Mutanten.

**Abweichungen und Auslegungen:**
- Aufheben ohne BB-Pause (auch bei reiner YNAB-Pause) ist kein Fehler, sondern Ok mit der Pause, wie sie ist (`PausedInYnab` oder `None`), ohne Zeile.
- Pausieren bei reiner YNAB-Pause ist serverseitig erlaubt und ergibt `PausedInBoth`; die Oberfläche bietet es nicht an.
- Auch Aufheben verlangt ein Target. Wird das Target später entfernt (budgeting-k8wq3), bleibt die Pause gespeichert und wirkt nicht.
- Ein abgebrochener Aufruf gilt im Client wie eine Fehlerantwort: Meldung im Tab, Pause unverändert. Ob der Server geschrieben hat, zeigt das nächste Lesen des Monats.
- Eine Fehlermeldung gilt nur, solange Kategorie und Monat gezeigt werden (`syncPause`).
- `[human-eye]` offen: Knopf und Satz im Sheet und Panel nicht per Screenshot angesehen. `design-quality` ist hier sinnvoll.
