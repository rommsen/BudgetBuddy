---
id: ledger-z71ka
title: "Bot-Commit: entry_id auch nach gescheitertem Push, und ein abgebrochener Commit ist wiederholbar, ohne doppelt zu buchen"
status: done
type: bug
context: ledger
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: [0066, 0068, 0021]
related_research: []
prior_art: []
---

## Why
Zwei Wege führen heute zu einer Doppelbuchung durch Bob (Audit B1 M1, M2, B2, C B3, C B5; zusammengelegt aus ledger-z71ka und ledger-z6c16):

1. **Push gescheitert:** `addCashExpense` (`src/Server/Ledger/CommandHandlers.fs`) schreibt `CashExpenseAdded`, der Push scheitert, es folgt `EntryPushToYnabFailed`, zurück kommt nur `Error err`. `bookWithinLimit` (`src/Server/BotApi.fs`) antwortet deshalb `entry_id: null`, obwohl die Buchung im Kassenbuch steht. Bob hält das für "nicht gebucht" und legt eine neue Vorschau an. Das widerspricht `docs/bot-api.md` ("Die Antwort nennt `entry_id`").
2. **Commit wirft:** `BotPreviewStore.CommitOnce` legt den Task als `Lazy` ab. Wirft der Commit (etwa ein DB-Fehler in `appendAndApply` oder `loadDailyUsage`), bleibt der fehlerhafte Task gespeichert. Jede Wiederholung derselben Vorschau bekommt bis zum Purge 500 `commit_failed`, ohne Angabe, ob gebucht wurde. Über eine neue Vorschau kann Bob doppelt buchen.

## What
- Der Commit legt die `EntryId` fest, bevor er bucht, und merkt sie sich je Vorschau im `BotPreviewStore`. `addCashExpense` bekommt die `EntryId` als Parameter (Quick Add in `src/Server/Api.fs` erzeugt sie wie bisher mit `newEntryId ()` und reicht sie hinein) und meldet sie auch bei gescheitertem Push zurück, zusammen mit dem Push-Fehler.
- **Push gescheitert:** Antwort 200 mit `status: "booked"`, der `entry_id` der Kassenbuch-Buchung, `pushed_to_ynab: false` und `push_error`. Ein zweiter Commit derselben Vorschau liefert wie heute dasselbe Ergebnis und versucht den Push nicht noch einmal (Erneut senden und Spiegel bleiben dafür zuständig, ADR 0021).
- **Commit wirft (Entscheidung des Owners 2026-11-05):** Ein zweiter Commit derselben Vorschau sieht nach, ob die vorab festgelegte `EntryId` im Kassenbuch steht (`Ledger.Persistence.loadEventsForEntry`).
  - Steht sie da: 200 `booked` mit dieser `entry_id`. `pushed_to_ynab` ergibt sich aus dem Stream (`EntryLinkedToYnab` vorhanden), `push_error` aus `EntryPushToYnabFailed`, sonst `null`.
  - Steht sie nicht da: Der Commit läuft erneut, mit derselben `EntryId`, unter `bookingGate` und mit Prüfung der Tagesgrenze. Bob muss nie eine neue Vorschau anlegen.
  - Der erste, werfende Aufruf antwortet weiter 500 `commit_failed`, der Text sagt aber, dass ein erneuter Commit derselben `preview_id` sicher ist.
- Ablauf und Abgelaufen-Regel (410) bleiben: Eine Vorschau, deren Commit schon einmal begonnen hat, gilt als committed und verfällt nicht mit 410.

## Acceptance criteria
- [ ] Kaputt-Szenario Push: GIVEN eine Vorschau, WHEN der Commit läuft und der Push scheitert, THEN enthält die Antwort die `EntryId` der gespeicherten `CashExpenseAdded`-Buchung, `pushed_to_ynab: false` und `push_error`. Test in `src/Tests/BotApiTests.fs`, rot vor dem Fix.
- [ ] Kaputt-Szenario Ausnahme nach dem Buchen: GIVEN ein Commit, der nach `CashExpenseAdded` wirft (Port wirft nach dem Schreiben), WHEN derselbe Commit erneut kommt, THEN 200 `booked` mit derselben `entry_id`, und im Kassenbuch steht genau eine Bob-Buchung. Rot vor dem Fix.
- [ ] Kaputt-Szenario Ausnahme vor dem Buchen: GIVEN ein Commit, der vor dem Schreiben wirft, WHEN derselbe Commit erneut kommt und der Fehler weg ist, THEN wird genau einmal gebucht und die Antwort ist 200 `booked`. Rot vor dem Fix.
- [ ] Intakt-Szenarien bleiben grün: idempotenter Commit, zehn gleichzeitige Commits buchen einmal, Tagesgrenze nach Summe und Anzahl, 404 und 410.
- [ ] Die erneute Buchung nach einer Ausnahme hält die Tagesgrenze ein (Test: Grenze inzwischen erreicht, Wiederholung ergibt 422 und bucht nicht).
- [ ] Quick Add verhält sich unverändert (bestehende Tests in `LedgerTests.fs`, `LedgerPushFailureTests.fs` und `LedgerMirrorTests.fs` nur an die neue Signatur angepasst, nicht inhaltlich geändert).
- [ ] `docs/bot-api.md` beschreibt beide Fälle: Push gescheitert (`entry_id` gesetzt, `pushed_to_ynab: false`) und erneuter Commit nach `commit_failed`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Owner-Ja für promote und work liegt vor (2026-11-05). Migrationsschritte P4 und P5, Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`.
- Bob-Grenzen (ADR 0068) nicht lockern: Die Wiederholung läuft durch `bookPreview` mit `bookingGate` und `checkBotDailyLimit`.
- Damit die Tests eine Ausnahme an definierter Stelle auslösen können, braucht `BotDeps` einen Port für das Buchen oder für das Schreiben (Vorbild: `Push` als Funktionswert). Jede Stelle, die den Live-Port anschließt (`liveBotDeps` o. ä. am Ende von `BotApi.fs`), wird nach Test-Doktrin Punkt 3 benannt und belegt.
- Mögliche Form im Store: je Vorschau `EntryId` und der laufende oder abgeschlossene Task. Ein fehlerhafter Task wird beim nächsten Aufruf durch Nachsehen im Kassenbuch ersetzt, nicht wiederverwendet. Gleichzeitige Wiederholungen dürfen nicht zweimal buchen (`GetOrAdd` mit Austausch per `TryUpdate`, oder unter der bestehenden Sperre prüfen).
- Kein Zustand über einen Neustart: Vorschauen liegen weiter nur im Speicher (ADR 0066).
- Läuft parallel zu budgeting-dgrp7 im Nachtlauf. Berührt `src/Server/BotApi.fs`, `src/Server/Ledger/CommandHandlers.fs` (nur `addCashExpense`), `src/Server/Api.fs` (nur die Quick-Add-Stelle) und `docs/bot-api.md`.

## Outcome

Beide Wege zur Doppelbuchung durch Bob sind geschlossen.

- `Ledger.CommandHandlers.addCashExpense` (`src/Server/Ledger/CommandHandlers.fs`) bekommt die `EntryId` als Parameter (nach `now`). Der Rückgabetyp `YnabResult<EntryId>` bleibt: Bei gescheitertem Push steht die Buchung unter genau der übergebenen Id, `Error` trägt den Push-Fehler. Der Aufrufer hält die Id also auch im Fehlerfall. Quick Add (`src/Server/Api.fs`) erzeugt sie mit `newEntryId ()`. Die Tests in `LedgerTests.fs`, `LedgerPushFailureTests.fs` und `LedgerMirrorTests.fs` sind nur an die Signatur angepasst (`(newEntryId ())` eingefügt).
- `src/Server/BotApi.fs`: `BotPreviewStore.CommitOnce` legt je Vorschau einen Slot mit `EntryId` und laufendem oder abgeschlossenem Versuch an. Ein Versuch, der mit einer Ausnahme endete, wird beim nächsten Aufruf unter einer Sperre durch einen neuen mit derselben `EntryId` ersetzt. Gleichzeitige Wiederholungen teilen sich den neuen Versuch. `bookPreview` sieht unter `bookingGate` zuerst im Kassenbuch nach (`bookedFromLedger` über `loadEventsForEntry`): steht die Buchung da, ist sie das Ergebnis (`pushed_to_ynab` aus `EntryLinkedToYnab`, `push_error` aus dem letzten `EntryPushToYnabFailed`), sonst prüft `bookWithinLimit` die Tagesgrenze und bucht mit derselben Id. `BotCommitResult.EntryId` ist kein Option mehr. Die 500 `commit_failed` sagt, dass ein erneuter Commit derselben `preview_id` sicher ist. Neuer Port `BotDeps.AddCashExpense`, live `Ledger.CommandHandlers.addCashExpense` in `liveDeps`, belegt durch den Test "die echte Umgebung bucht über addCashExpense ins Kassenbuch". Die Bob-Grenzen (ADR 0068) sind unverändert.
- Tests in `src/Tests/BotApiTests.fs` (vor dem Fix rot): der Push-Fehlschlag-Test prüft jetzt die `entry_id` der gespeicherten Buchung; neu sind Ausnahme nach dem Buchen (auch nach Ablauf, kein 410), Ausnahme nach dem Buchen mit gescheitertem Push (push_error aus dem Stream), Ausnahme vor dem Buchen, zehn gleichzeitige Wiederholungen nach einer Ausnahme, und die Tagesgrenze bei der Wiederholung (422, keine Buchung). Der Buchungs-Port `failingOnce BeforeWrite|AfterWrite` löst die Ausnahmen aus. Die bestehenden Intakt-Tests (Idempotenz, zehn gleichzeitige Commits, Tagesgrenze, 404/410) sind weiter grün.
- `docs/bot-api.md` beschreibt beide Fälle beim Commit: Push gescheitert und erneuter Commit nach `commit_failed`.
- `./verify.sh`: VERIFY: PASS (dotnet 4018 grün, node 310 grün).
