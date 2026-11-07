---
id: budgeting-my8p8
title: "Erneut senden: eine von BB zurückgezogene Zeile heißt nicht mehr \"in YNAB geändert\", und Erneut senden im Folgemonat ist getestet"
status: doing
type: bug
context: budgeting
created: 2026-11-07
completed:
depends_on: []
blocks: [budgeting-r5h2n]
tags: [budget, rueckschreibung, erneut-senden, historie, bestandsreview]
related_adrs: [0055, 0069, 0070, 0074, 0075, 0084]
related_research: []
prior_art: [budgeting-hyn77, budgeting-dgrp7, budgeting-nfrds, budgeting-h2zfn, budgeting-et7z8]
---

## Why
Bestandsreview budgeting vom 2026-11-07, Gruppe 1 (`.agentheim/knowledge/reviews/2026-11-07-budgeting.md`, Abschnitt 1, höchste Schwere hoch). Roman hat am 2026-11-07 die Option "Test plus eigener Anzeigefall" gewählt.

Was heute passiert:
- **Falscher Stand im Bericht und in der Historie.** Schließt BB eine Zeile ungesendet (`Withdrawn`), bildet `lineOutcomeOf` sie auf `ChangedInYnab` ab (`src/Server/Budgeting/Events.fs:332`). Bericht (`src/Client/Components/Budget/TargetsView.fs:108`) und Historie (`src/Client/Components/Budget/Inspector.fs:252`) zeigen "in YNAB geändert", obwohl YNAB diese Zeile nie berührt hat. Beispiel mit erfundenen Werten: Verschiebung von 50 € von Kategorie A nach Kategorie B. Beim erneuten Senden steht A in YNAB auf einem dritten Wert, YNAB gewinnt, BB schließt B ungesendet. Im Bericht steht bei B "YNAB steht anders als erwartet, nichts überschrieben", und Roman sucht in YNAB eine Änderung an B, die es nicht gibt.
- **Erneut senden im vergangenen Monat ohne Test.** Die README sagt: "Erneut senden geht auch hinaus, wenn der Monat inzwischen ein vergangener ist" (README budgeting, **Anwendung**). Die Testliste "Targets anwenden in vergangenen Monaten (ADR 0074)" (`src/Tests/TargetsApplyTests.fs:622`) prüft nur die Ablehnung von Vorschau und Übernehmen. Würde jemand die Sperre für vergangene Monate auf `retryFailedTargets` ausdehnen, bliebe jeder Test grün, und BB und YNAB stünden für den Vormonat dauerhaft auseinander.

ADR 0070 hat den Stand "in YNAB geändert" für das ungesendete Ziel bewusst gewählt, mit der Begründung "Es kommt kein neuer Stand dazu. Das Schema von `budget_movement_results` bleibt." Seit ADR 0084 gibt es die Tabelle nicht mehr, `AssignmentWriteBackWithdrawn` ist ein eigenes Ereignis. Der Grund für den gemeinsamen Stand ist entfallen.

## What
Am Code vom 2026-11-07 (main 7423fb0) geprüft.

1. **Eigener Stand für "von BB zurückgezogen"** in `LineOutcome` (`src/Shared/Budgeting.fs:322`). `lineOutcomeOf Withdrawn` (`Events.fs:332`) liefert ihn, `SupersededInYnab` bleibt `ChangedInYnab`. Der Lesepfad (`src/Server/Budgeting/Persistence.fs:249`) übernimmt ihn.
2. **Geschlossen bleibt geschlossen.** `isOpenLine` (`src/Server/Budgeting/Domain.fs:177`) und `planRetry` (`src/Server/Budgeting/Assignments.fs:229`) behandeln den neuen Stand wie `ChangedInYnab`: nicht offen, verloren. Die Bedingung `target.Latest = Some ChangedInYnab` (`Assignments.fs:238`) prüft beide Stände (oder `isLost target`).
3. **Anzeige.** Bericht: `outcomeLine` (`TargetsView.fs:103`) zieht Text und Badge in eine reine Funktion, der neue Stand bekommt Badge und Satz nach "Entscheidung Roman". Historie je Kategorie: `historyStatus` (`src/Shared/Budgeting.fs:883`) und `historyStatusText` (`Inspector.fs:248`) mit eigenem Fall; `HistoryReplacedInBb` ("in BB ersetzt") geht weiter vor (`movementStatus`, `Budgeting.fs:892`). Allgemeine Historie: `RunStatus` (`Budgeting.fs:1014`) und `runBadge` (`src/Client/Components/Budget/History.fs:77`); in der Rangfolge von `runStatus` (`Budgeting.fs:1060`) steht der neue Stand gleichauf mit "in BB ersetzt", also hinter "in YNAB geändert".
4. **Test: Erneut senden im Folgemonat.** Server am API-Adapter über `Server.Api.budgetApiWith` (`src/Server/Api.fs:1967`) mit einer Uhr im Folgemonat; Client an den reinen Übergängen `retryFailed` (`src/Client/Components/Budget/Targets.fs:81`) und `startMoveRetry` (`src/Client/Components/Budget/Moves.fs:702`) mit einem Rahmen, dessen `CurrentMonth` der Folgemonat ist.
5. **Doku.** README budgeting und Nachtrag an ADR 0070.

## Acceptance criteria
- [ ] Kaputt, Domain- oder Handler-Ebene (`src/Tests/MoveMoneyTests.fs`), vor dem Fix rot: GIVEN eine Verschiebung mit fehlgeschlagener Quelle und offenem Ziel, YNAB steht bei der Quelle weder auf dem alten noch auf dem Zielwert, WHEN erneut senden, THEN trägt im Bericht die Quelle `ChangedInYnab` und das Ziel den neuen Stand. Der Kommentar nennt den Bug (ungesendetes Ziel hieß "in YNAB geändert").
- [ ] Kaputt, Lesepfad, vor dem Fix rot: der Test "Der Grund eines Fehlschlags bleibt erhalten, ersetzt und zurückgezogen lesen sich als in YNAB geändert" (`src/Tests/TargetsApplyTests.fs:590`) erwartet für `Withdrawn` den neuen Stand und heißt entsprechend; `SupersededInYnab` liest sich weiter als `ChangedInYnab`, `WriteBackFailed "Rate-Limit"` weiter mit Grund.
- [ ] Kaputt, Gegenrichtung der Paarregel: GIVEN beide Zeilen offen, das Ziel ist in YNAB verloren, WHEN erneut senden, THEN schließt die Quelle mit dem neuen Stand, das Ziel mit `ChangedInYnab`. Vor dem Fix rot.
- [ ] Intakt: Gewinnt YNAB bei der Quelle nicht (Quelle scheitert wieder, Netz oder Rate-Limit), bleibt das Ziel fehlgeschlagen und erneut sendbar. Die bestehenden Tests zu `planRetry` und ADR 0070 in `MoveMoneyTests.fs` bleiben grün, angepasst wird nur der erwartete Stand der zurückgezogenen Zeile.
- [ ] `isOpenLine` liefert für den neuen Stand `false` (Domain-Test). Eine zurückgezogene Zeile wird beim nächsten erneuten Senden nicht gesendet (Handler-Test, `f.Attempts` ohne PATCH für sie).
- [ ] Historie je Kategorie: eine Zeile mit `Withdrawn` ohne ersetzende Entscheidung zeigt den neuen Text; eine von einer späteren BB-Entscheidung zurückgezogene Zeile zeigt weiter "in BB ersetzt" (`BudgetHistoryTests.fs` oder `BudgetHistoryClientTests.fs`, reine Funktionen).
- [ ] Allgemeine Historie: Verschiebung mit Quelle `ChangedInYnab` und zurückgezogenem Ziel steht als "in YNAB geändert"; ein Durchlauf nur aus geschriebenen und zurückgezogenen Zeilen steht mit dem neuen Badge (`BudgetGeneralHistoryTests.fs`).
- [ ] Bericht: Text und Badge je Stand kommen aus einer reinen Funktion, die `outcomeLine` nur aufruft; ein .NET-Test deckt alle vier Stände ab. Der Toast einer Verschiebung (`moveToast`, `Moves.fs:387`) bleibt "In YNAB geändert, nichts überschrieben", weil die Gegenseite in jedem Paarfall `ChangedInYnab` trägt (bestehender Test grün).
- [ ] Erneut senden im Folgemonat, Server (API-Adapter, `TargetsApplyTests.fs`, neue Liste neben `lastMonthTests`): GIVEN eine Anwendung von Targets anwenden im laufenden Monat mit einer fehlgeschlagenen Zeile, WHEN `retryFailedTargets` über `budgetApiWith` mit einer Uhr im Folgemonat, THEN geht der PATCH für diese Zeile in den Monat der Anwendung hinaus und der Bericht meldet sie geschrieben.
- [ ] Erneut senden im Folgemonat, Client (`TargetsApplyClientTests.fs`, `MoveMoneyClientTests.fs`): mit `CurrentMonth` im Folgemonat liefert `retryFailed` die Anwendung und `startMoveRetry` `true` für eine offene Verschiebung des Vormonats.
- [ ] Gegenprobe im Outcome: Mutant "vergangener Monat in `retryFailedWith` abgelehnt" (`src/Server/Budgeting/CommandHandlers.fs:1073`) macht den Server-Test rot, Mutant "vergangener Monat in `retryFailed` bzw. `startMoveRetry` gesperrt" den jeweiligen Client-Test.
- [ ] Fable.Remoting: die neuen Fälle in `LineOutcome`, `HistoryStatus` und `RunStatus` sind Union-Fälle in `src/Shared`. Die Fixtures in `scripts/*.test.mjs` (etwa `scripts/targets-apply.test.mjs:156`) sind geprüft und laufen unverändert; kein neuer Browser-Test, das Verhalten ist unter .NET prüfbar.
- [ ] README budgeting: **Verschieben** (heute "wird das Ziel ungesendet ebenfalls als in YNAB geändert geschlossen" und "kann eine Zeile 'in YNAB geändert' heißen, die YNAB nie berührt hat"), **Anwendung** (Liste der Stände), **Historie** (Stand der Zeile, Rangfolge des Durchlauf-Stands), das Ereignis-Bullet zu `AssignmentWriteBackWithdrawn` ("zurückgezogen schließt ein ungesendetes Ziel in BB") und die Startmigration ("Leser zeigen beide als in YNAB geändert") nennen den neuen Stand. Delta mit `npm run check:readme-delta -- --bc budgeting` geprüft.
- [ ] ADR 0070 bekommt einen Nachtrag 2026-11-07: das ungesendet geschlossene Ziel (und die ungesendet geschlossene Quelle) trägt den eigenen Stand, weil ADR 0084 `AssignmentWriteBackWithdrawn` als eigenes Ereignis führt; die Begründung unter "Neutral" gilt nicht mehr.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Entscheidung Roman (2026-11-07)
Roman wählte am 2026-11-07 A (AskUserQuestion): Badge "in BB zurückgezogen", neutral klein, im Bericht der Satz dazu.

Die Optionen, wie sie zur Wahl standen:
**Wie heißt der neue Stand, und was sagt der Bericht dazu?** Sichtbar in Bericht, Historie je Kategorie und allgemeiner Historie.

- A: Badge "in BB zurückgezogen", neutral klein wie "in BB ersetzt" (`Badge.neutralSmall`). Im Bericht der Satz "Nicht gesendet, BB hat die Zeile zurückgezogen. YNAB blieb unverändert."
- B: Badge "nicht gesendet", neutral klein, derselbe Satz.

Empfehlung: A. "zurückgezogen" ist schon der Fachbegriff in README und Ereignisname, und es steht neben "in BB ersetzt" als zweiter Fall, in dem BB selbst entschieden hat. Neutral statt Gelb, weil Roman nichts tun muss; die Gegenseite der Verschiebung trägt weiter das gelbe "in YNAB geändert" und zeigt, wo etwas passiert ist.

## Nicht dazu
- Umbenennungen aus Gruppe 1 ohne fachlichen Nutzen: `retryFailedTargets` zu `retryWriteBack` (`src/Shared/Api.fs:524`), `ApplyReport.ApplicationId` als `Guid` statt `string` (`src/Shared/Budgeting.fs:340`), `Guid.TryParse` der Anwendungs-Id nach `Validation.fs` (`src/Server/Api.fs:1998`), die Meldung "Targets-Anwendung nicht gefunden" auch für Verschieben und Zuweisen (`CommandHandlers.fs:1082`). Laut Empfehlung erst beim nächsten Vertragsbruch; der Conductor nimmt sie als Zeilen in die Kann-Sammlung budgeting-h9c3w.
- Ruhigere Badges im Bericht (jede Zeile grünes `Badge.success`, `TargetsView.fs:110`, styleguide §2): Kann-Sammlung budgeting-h9c3w.
- Ein Hinweis auf das Geld, das nach einer verlorenen Antwort in Zu verteilen liegt (ADR 0070, Negative): eigene Frage, nicht Teil dieser Task.

## Notes
- Ältere offene Browser-Tabs kennen den neuen Union-Fall erst nach Neuladen. Bei einem Single-User-System hingenommen (so in der Option genannt).
- Migrierter Altbestand bleibt: jedes migrierte "changed-in-ynab" ist `AssignmentWriteBackSupersededInYnab` und zeigt weiter "in YNAB geändert". Die Probe `EventMigrationProbe.fs:95` ist davon nicht betroffen.
- Wo heute `Withdrawn` entsteht: Paarregel beim erneuten Senden (`Assignments.fs:238`, `:244`, `:254`), Paarregel vor dem ersten Senden einer Verschiebung, neue Entscheidung auf offener Rückschreibung (`Persistence.fs:829`) und die Übernahme (`Assignments.fs:134`). Der Worker prüft, welche davon in der Historie als "in BB ersetzt" erscheinen (`replacedLines`, `Persistence.fs:365`), und nennt im Outcome, welcher Weg welchen Stand zeigt.
- Keine echten Beträge oder Kategorienamen in Tests, Doku oder Commits.
