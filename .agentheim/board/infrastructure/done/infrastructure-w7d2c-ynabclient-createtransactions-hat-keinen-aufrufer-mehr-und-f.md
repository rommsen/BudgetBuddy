---
id: infrastructure-w7d2c
title: "YnabClient.createTransactions hat keinen Aufrufer mehr und fliegt raus"
status: done
type: chore
context: infrastructure
created: 2026-11-01
completed:
depends_on: []
blocks: []
tags: [aufraeumen, ynab]
related_adrs: []
related_research: []
prior_art: [infrastructure-p747v]
---

## Why
Beim Umstellen auf den Berliner Kalendertag (infrastructure-p747v) fiel auf: `createTransactions` in `src/Server/YnabClient.fs` wird im Repo nirgends aufgerufen. `importToYnab` und `forceImportDuplicates` nutzen `prepareTransactions` und `sendPrepared` einzeln. Der Kommentar über der Funktion behauptet noch etwas anderes.

## What
Prüfen, dass es keinen Aufrufer gibt, dann die Funktion entfernen und die Kommentare nachziehen, die sie nennen (`YnabClient.fs` beim `PrepareResult`, `YnabClientTests.fs`).

Dazu gehören zwei nachgebaute Tests in `src/Tests/YnabClientTests.fs`, die Logik aus `createTransactions` im Test nachbilden statt Produktionscode aufzurufen (Test-Doktrin Punkt 6): "Simulate the conversion logic from createTransactions" (um Zeile 667) und "Replicate the filter logic from createTransactions" (um Zeile 853). Sie fliegen raus, wenn dasselbe Verhalten schon über `prepareTransactions` getestet ist (Teil 1, ledger-qvt84: Zukunftsdatum, ImportId). Fehlt dort ein Fall, wird er als Test gegen `prepareTransactions` ergänzt statt den Nachbau zu behalten.

## Acceptance criteria
- [ ] `grep -rn 'createTransactions' src` trifft keinen Produktionscode mehr; verbleibende Treffer sind nur Kommentare, die den heutigen Weg (`prepareTransactions`, `sendPrepared`) richtig beschreiben.
- [ ] Die beiden nachgebauten Tests sind entfernt oder durch Tests gegen `prepareTransactions` ersetzt; das Outcome nennt für jeden, welcher bestehende oder neue Test sein Verhalten abdeckt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Reines Aufräumen, kein fachliches Verhalten ändert sich. Kein Regressionstest nötig.
- Läuft im Nachtlauf parallel zu budgeting-dgrp7 und ledger-z71ka; berührt nur `src/Server/YnabClient.fs` und `src/Tests/YnabClientTests.fs`.

## Outcome

`createTransactions` ist aus `src/Server/YnabClient.fs` entfernt. `grep -rn 'createTransactions' src` findet nichts mehr. Die Aufrufer `Server.Api.importToYnab` und `forceImportDuplicates` liefen schon über `Server.Api.prepareForImport` (`prepareTransactions`) und `sendPrepared`. Der Kommentar über `sendPrepared` beschreibt jetzt genau diesen Weg. In `src/Tests/YnabClientTests.fs` nennen die Kopfzeile des Teil-1-Blocks und der Force-Import-Regressionstest statt `createTransactions` das Senden.

Die beiden Nachbau-Tests:
- "converts SyncTransaction to YNAB format correctly" (vorher: "Simulate the conversion logic from createTransactions"): bleibt unter demselben Namen, ruft jetzt aber `prepareTransactions` auf und prüft den echten `PreparedTransaction.Request`, also Konto, Datum, Milliunits, Payee, Memo mit Referenz, `uncleared`, Kategorie und deterministische ImportId. Gegenüber dem Nachbau sind die Erwartungen schärfer: das Memo trägt den echten Suffix `, Ref: REF123`.
- "future-dated transactions are filtered out before YNAB import" (vorher: "Replicate the filter logic from createTransactions"): entfernt. Abgedeckt durch die bestehenden Tests "zukunftsdatierte Buchungen landen nicht in ToSend, sondern in FutureDatedTransactionIds" und "Skipped-Buchungen werden nie vorbereitet" (`prepareTransactions (Vorbereiten)`). Den Berliner Kalendertag decken die `Server.Api.prepareForImport`-Tests ab.

`./verify.sh`: VERIFY: PASS (dotnet 4011 grün, 13 übersprungen wie üblich hinter `RUN_INTEGRATION_TESTS`, node 310 grün). Der erste Lauf war rot, weil `[layout] Kopf: ab 1024 px ...` in `scripts/budget-page.test.mjs` unter der Last paralleler Worker fehlschlug (Suchfeld bei 1024 px zu schmal). Die Datei allein lief grün (32/32), der volle zweite Lauf ebenfalls. Der Test hat keinen Bezug zu dieser Änderung.

Offen und außerhalb des Auftrags: "transaction dated today is not filtered out" in derselben Testliste prüft auch nur einen nachgebauten Vergleich (`BookingDate.Date > today`) statt Produktionscode. Den Fall deckt der `prepareTransactions`-Test ab, dessen fällige Buchung auf `today` datiert ist. Gleiches gilt für mehrere Tests in "Transaction Conversion Tests" und "Milliunits Conversion Tests" (PayeeOverride, Memo, Whitespace, Skipped-Filter), siehe Backlog-Eintrag.
