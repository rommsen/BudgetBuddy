---
id: budgeting-t6p8z
title: "Target pausieren: Pause je Kategorie und Monat in BB, wirkt in Fehlt, Frei, Vorschau und Targets anwenden, endet mit dem Monat, Knopf im Tab Geld"
status: backlog
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
