---
id: ledger-kxme4
title: "Split-Buchungen in BB bearbeiten, Schreibweg: Befehl setEntrySplits mit Prüfung Summe gleich Betrag, EntryRecategorized mit Splits, eigener Push nach YNAB, derselbe Fehlschlag-Zustand wie bei der Kategorie"
status: done
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [ledger-k2rbn, ledger-mas5g]
blocks: [ledger-pafkp]
tags: [backend, kassenbuch, split, v2, schreibweg]
related_adrs: [0006, 0015, 0019, 0021, 0028, 0038, 0045, 0046, 0054, 0055, 0058, 0059]
related_research: []
prior_art: [ledger-k2rbn, ledger-h6mer]
---

## Why
Roman hat am 2026-10-24 entschieden, die Split-Bearbeitung mitzubauen (der Pfeil an der
Kategorie einer Split-Buchung öffnet einen Split-Editor, `ledger-pafkp`). Dies ist der
Server-Teil. Wie YNAB Splits schreiben lässt, hat der Spike `ledger-mas5g` und der
Probelauf vom 2026-10-24 geklärt (ADR 0058, angenommen).

## What
1. **Befehl** `setEntrySplits: Guid -> TransactionSplit list -> Async<Result<CategoryChangeOutcome, string>>`
   in `LedgerApi` (`src/Shared/Api.fs`), umgesetzt in `Ledger/CommandHandlers.fs`, unter der
   Schreib-Sperre (`WriteGuard`) wie `setEntryCategory`.
   - Zwei oder mehr Zeilen: die Buchung wird aufgeteilt bzw. ihre Zeilen ersetzt.
   - Genau eine Zeile mit Kategorie oder ohne Kategorie und vollem Betrag: der Split wird
     aufgehoben, die Buchung trägt danach diese Kategorie (bzw. keine). So braucht "Aufheben"
     und das Rückgängig aus `ledger-pafkp` keinen zweiten Befehl.
   - Leere Liste und eine einzelne Übertrags-Zeile: abgelehnt.
2. **Validierung** über die gemeinsamen Helfer aus `Shared.Domain` (`mkSplits`,
   `splitRemainder`, ADR 0006) bei zwei oder mehr Zeilen; bei einer Zeile Betrag gleich
   Buchungsbetrag. Weitere Ablehnungen (Fehler, kein Ereignis), als Erweiterung von
   `categoryNotChangeableReason` oder als eigene Prüfung:
   - Buchung in YNAB gelöscht, nicht mit YNAB verknüpft, oder selbst ein Übertrag zwischen
     Budget-Konten;
   - Konto ist ein Tracking-Konto (Spec: kein Split auf Tracking-Konten);
   - `Cleared = Reconciled` (ADR 0058, Punkt 7, Roman 2026-10-24);
   - Ändern oder Aufheben einer schon aufgeteilten Buchung, deren `import_id` nicht mit `BB:`
     beginnt (ADR 0058, Punkt 2). Aufteilen bleibt für sie erlaubt; ohne `import_id` ist
     beides erlaubt.
   - Eine Kategorie in einer Zeile, die BB nicht kennt (wie bei `setEntryCategory`).
3. **Ereignis** `EntryRecategorized` mit `OldSplits`/`NewSplits` (bzw. `NewCategory` beim
   Aufheben), Actor `UserInBb`, vor dem Push. `apply` bleibt bei "Kategorie XOR Splits".
   Ziel gleich Ist: kein Ereignis, kein Push.
4. **Push nach YNAB**, zwei Wege (ADR 0058):
   - **Aufteilen** (vorher kein Split): PUT mit `category_id: null` und `subtransactions`.
     Fehlen `subtransactions` in der Antwort, gilt das als Fehlschlag.
   - **Ändern oder Aufheben** (vorher Split): `YnabClient.replaceSplitTransaction`: DELETE,
     dann POST mit Datum, Betrag, Konto, Payee, Memo, Flag, `cleared`, `approved` aus dem
     Eintrag und den neuen `subtransactions` bzw. der `category_id`. `import_id` = alte plus
     `:r<n>` (n = Zahl der bisherigen Ersätze plus 1), ohne alte `import_id` keine.
     Übertrags-Zeilen über die `transfer_payee_id` des Zielkontos (ADR 0006).
5. **Nach einem Ersatz** ein neues Ereignis (Name beim Bau, etwa `EntryReplacedInYnab`) mit
   alter und neuer YNAB-Id und neuer `import_id`. `linksFrom` stiftet die neuen
   Verknüpfungen (YNAB-Id, ImportId, ADR 0015). Die alte YNAB-Id gilt als abgelöst: eine
   spätere Lösch-Beobachtung des Spiegels zu ihr setzt `DeletedInYnab` nicht. Die
   Split-Übertrags-Verknüpfung (`SplitTransferCounterparts`, ADR 0046) wird für die neuen
   Subtransaction-Ids neu gezogen (Mechanik aus `ledger-h6mer`).
6. **Fehlschlag** wie ADR 0055, Punkt 4: `EntryCategorizationWriteBackFailed` mit Baseline,
   bei Erfolg `EntryCategorizationWrittenBackToYnab`. Zusätzlicher Fall "in YNAB gelöscht,
   Neuanlage offen" (DELETE gelungen, POST gescheitert): der Fehlschlag merkt sich das, und
   "Erneut senden" führt dann nur den POST aus, ohne erneutes DELETE.
7. `CategoryChangeOutcome` passt als Rückgabe; `Previous` reicht für ein Rückgängig mit
   Kategorie. Für das Rückgängig mit alten Zeilen bekommt der Client die Zeilen aus seinem
   `LedgerEntryView` (`ledger-pafkp`), der Befehl muss sie nicht zurückgeben.

## Acceptance criteria
- [ ] `setEntrySplits` steht in `LedgerApi` und ist in `src/Server/Api.fs` verdrahtet.
- [ ] Validierungs-Tests: Summe ungleich Betrag, leere Liste, einzelne Übertrags-Zeile,
      unbekannte Kategorie: je `Error`, kein Ereignis, kein YNAB-Aufruf.
- [ ] Sperr-Tests, je `Error` ohne Ereignis und ohne YNAB-Aufruf: Buchung in YNAB gelöscht;
      nicht verknüpft; Übertrag zwischen Budget-Konten; Tracking-Konto; `Reconciled`;
      Ändern einer Split-Buchung mit `import_id` `YNAB:…`.
- [ ] Test: Aufteilen einer unaufgeteilten Buchung mit `import_id` `YNAB:…` ist erlaubt
      (die Sperre greift nur bei Ändern und Aufheben).
- [ ] Test: Ziel gleich Ist ergibt `CategoryUnchanged`, kein Ereignis, kein Push.
- [ ] Test Aufteilen: ein `EntryRecategorized` mit `NewSplits`, danach genau ein PUT mit
      `category_id: null` und den Zeilen; bei Erfolg `EntryCategorizationWrittenBackToYnab`,
      YNAB-Id unverändert.
- [ ] Test Aufteilen: Antwort ohne `subtransactions` ergibt
      `EntryCategorizationWriteBackFailed` (stilles Ignorieren durch YNAB wird erkannt).
- [ ] Test Ändern: DELETE, danach POST; der POST-Body trägt `cleared`, `approved`, Flag,
      Memo, Payee, Datum, Betrag und Konto des Eintrags und die `import_id` `<alt>:r1`; ein
      zweiter Ersatz derselben Buchung sendet `<alt>:r2`.
- [ ] Test Aufheben (eine Zeile): DELETE, danach POST mit `category_id` und ohne
      `subtransactions`; der Eintrag trägt danach die Kategorie und keine Splits.
- [ ] Test: nach einem gelungenen Ersatz verweisen die neue YNAB-Id und die neue `import_id`
      auf denselben Eintrag (Fremd-Id-Tabelle), und eine Lösch-Beobachtung der alten YNAB-Id
      setzt `DeletedInYnab` nicht. Eine Lösch-Beobachtung der neuen Id setzt es weiterhin.
- [ ] Test: POST scheitert nach gelungenem DELETE: Fehlschlag mit dem Fall "in YNAB
      gelöscht, Neuanlage offen"; "Erneut senden" ruft nur POST auf.
- [ ] Test: DELETE scheitert: Fehlschlag wie bei der Kategorie, kein POST.
- [ ] Test: nach einem Ersatz mit Übertrags-Zeile ist `SplitTransferCounterparts` auf die neue
      Subtransaction-Id und die neue Gegenbuchung umgestellt, sobald der Spiegel die neue
      Gegenbuchung gesehen hat.
- [ ] Encoder-Test: Übertrags-Zeilen gehen mit der `transfer_payee_id` des Zielkontos als
      `payee_id` und ohne `category_id` an YNAB; Beträge in Milliunits als JSON-Zahl.
- [ ] YNAB-Aufrufe sind in Tests ersetzt (kein Aufruf gegen echtes YNAB, keine Schreibvorgänge
      in die Produktiv-Datenbank).
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (e, Server), verfeinert am 2026-10-24
nach Probelauf und Annahme von ADR 0058.

Probelauf gegen das Wegwerf-Budget "Test" (Tabelle in ADR 0058): Aufteilen per PUT geht,
Zeilen ändern gibt 400, Kategorie auf Split wird still ignoriert (BB schickt das nie,
`setEntryCategory` sperrt Splits schon), `import_id` nach DELETE gibt 409, mit Suffix 201,
`approved`/`cleared`/`reconciled` werden übernommen, YNAB löscht Übertrags-Gegenbuchungen mit
und legt beim POST neue an, eine Übertrags-Zeile auf ein Budget-Konto ist erlaubt.

Ratenbegrenzung: ein Ersatz kostet zwei Aufrufe, ein Rückgängig noch einmal zwei.

Rückgängig eines Aufteilens ist ein Aufheben und läuft damit über Löschen und Neuanlegen; bei
einer `YNAB:…`-Buchung ist es deshalb gesperrt. Roman hat solche Buchungen nicht
(2026-10-24); `ledger-pafkp` blendet "Rückgängig" in dem Fall aus.

Dateien: `src/Shared/Domain.fs`, `src/Shared/Api.fs`, `src/Server/Api.fs`,
`src/Server/YnabClient.fs`, `src/Server/Ledger/CommandHandlers.fs`, `Ledger/Domain.fs`,
`Ledger/Events.fs` (neues Ersatz-Ereignis samt Codec), Spiegel-Beobachtung
(`YnabObservations.fs` bzw. die Lösch-Behandlung), Tests.

## Outcome

Server-Teil des Split-Schreibwegs nach ADR 0058 ist gebaut (Client und Split-Editor sind `ledger-pafkp`).

**API und Befehl.** `LedgerApi.setEntrySplits: Guid -> TransactionSplit list -> Async<Result<CategoryChangeOutcome, string>>` ist in `src/Shared/Api.fs` deklariert und in `src/Server/Api.fs` verdrahtet (neue Hilfsfunktion `splitWriteTools`, Token und Budget aus den Settings, dieselben Fehlerfälle wie `categoryPush`). Umsetzung `Ledger.CommandHandlers.setEntrySplits` unter `WriteGuard`. Prüfreihenfolge: Buchung bekannt, Sperren (`splitNotChangeableReason`: in YNAB gelöscht, `Reconciled`, Übertrag zwischen Budget-Konten), Zeilen (`classifySplitLines`: leer, einzelne Übertrags-Zeile, eine Zeile mit Teilbetrag, `mkSplits`), Kategorien-Namen (`resolveSplitNames`, unbekannt wird abgelehnt), YNAB-Verknüpfung, Ziel gleich Ist (`CategoryUnchanged`), fremde `import_id` bei Ändern/Aufheben, Länge der Ersatz-`import_id` (36), Tracking-Konto (Konten-Abruf). Danach `EntryRecategorized` (Actor `UserInBb`, `NewSplits` bzw. beim Aufheben `NewCategory`), dann `pushSplitState`.

**Push (`pushSplitState`).** Der Weg folgt der Baseline (Stand, den YNAB vermutlich hält): ohne Split dort PUT (`YnabClient.updateSplits`, `category_id` null und `subtransactions`; eine Antwort ohne `subtransactions` ist ein Fehlschlag) oder bei einer Zeile der Kategorie-PUT; mit Split dort DELETE (`deleteTransaction`) und POST (`createReplacement`) mit Datum, Betrag, Konto, Payee, Memo, Flag, `cleared`, `approved` des Eintrags und `import_id` plus `:r<n>`. Übertrags-Zeilen laufen über die `transfer_payee_id` (Payee-Abruf nur bei Übertrags-Zeile). `replaceSplitTransaction` fasst beide Schritte zusammen, der Befehl ruft sie einzeln, um dazwischen den Zustand zu halten.

**Ereignisse und Zustand.** Neu: `EntryReplacedInYnab` (Version 1, mit Codec, `linksFrom`, `apply`, Historien-Satz) und das Fremdsystem `ynabsuperseded` (`LinkSystem.YnabSupersededSystem`). `EntryCategorizationWriteBackFailed` und `CategorizationWriteBackFailure` tragen das optionale Feld `YnabDeletedTransactionId` (Zustand "in YNAB gelöscht, Neuanlage offen"; alte JSON-Zeilen lesen sich als `None`, Persistenz in `Ledger/Persistence.fs` nachgezogen). Der Spiegel überspringt Lösch-Beobachtungen abgelöster Ids (`isSupersededDeletion` in `runMirror`); `currentYnabTransactionId` löst die aktuelle Id auf und wird auch von den Prüfvermerk- und Kategorie-Pfaden benutzt (`tryYnabTransactionId`). Nach einem Ersatz ist `SplitTransferCounterparts` leer, der Spiegel-Nachlauf (ADR 0046) zieht die neuen Subtransaction-Ids und Gegenbuchungen nach.

**Erneut senden.** `retryCategorizationOrSplitWriteBack` wählt nach Baseline und BB-Stand den Kategorie- oder den Split-Weg (PUT, DELETE und POST, oder nur POST bei gelöschter Id). Die API-Methode `retryCategorizationWriteBack` ruft es. `setEntryCategory` lehnt eine Buchung ab, deren offener Fehlschlag noch einen Split als Baseline trägt.

**Abweichungen und Entscheidungen.** (1) Das Kriterium "bei Erfolg `EntryCategorizationWrittenBackToYnab`" ist nach ADR 0055 Punkt 2 und 4 umgesetzt: das Ereignis entsteht nur, wenn vorher ein Fehlschlag offen war; getestet in beide Richtungen. (2) Die Tracking-Konto-Prüfung nutzt einen Konten-Abruf, weil `LedgerAccount` kein `OnBudget` führt. (3) Entscheidungen in ADR 0059 (provisorische Nummer).

**Tests (35 neu, `src/Tests/LedgerSplitWriteTests.fs`, in `Tests.fsproj` mitten in die Liste hinter `LedgerCategoryChangeTests.fs` gesetzt).** Domäne: Codec-Rundreise beider Payloads, Lesbarkeit alter Fehlschlag-JSONs, `linksFrom`, `apply`, `replacementImportId`, `currentImportId`, `currentYnabTransactionId`, `classifySplitLines`, Historien-Satz. Befehle (In-Memory-SQLite, YNAB durch Aufzeichner ersetzt): Validierung (Summe, leer, einzelne Übertrags-Zeile, unbekannte Kategorie, je kein Ereignis und kein schreibender Aufruf), Sperren (gelöscht, nicht verknüpft, Übertrag, Tracking, Reconciled, fremde `import_id`), Aufteilen mit `YNAB:`-import_id erlaubt, Ziel gleich Ist, Aufteilen (Ereignis vor PUT, genau ein PUT, YNAB-Id gleich), PUT-Fehlschlag und Erneut senden mit WrittenBack, Ändern (Felder des POST, `:r1`, zweiter Ersatz `:r2`), Aufheben (Kategorie, keine Splits), Fremd-Id-Tabelle und Lösch-Beobachtung über `runMirror` (alte Id lässt `DeletedInYnab` aus, neue Id setzt es), POST scheitert nach DELETE (Erneut senden ruft nur POST), DELETE scheitert (kein POST, wiederholt), Sperre von `setEntryCategory`, Umstellung von `SplitTransferCounterparts` über `runMirror`, Replay gleich Zustand. YnabClient (Fake-Handler): `buildSplitBody` und `buildReplacementBody` (Übertrag mit `payee_id` und ohne `category_id`, Milliunits als JSON-Zahl), `sendSplits` (200 mit und ohne `subtransactions`, 400), `sendDeleteTransaction`, `sendCreateTransaction` (Id, 409), `parseCreatedTransactionId`, `parseAccountsResponse`. Die bestehenden Tests in `LedgerCategoryChangeTests.fs` wurden nur um das neue Pflichtfeld ergänzt. Rotprobe: ohne `isSupersededDeletion` schlagen zwei der neuen Tests fehl.

**Läufe im Worktree.** `dotnet build`: 0 Warnungen, 0 Fehler. `dotnet test`: 1913 bestanden, 7 übersprungen, 0 Fehler. `npm run build`: grün. `npm run guard:classes`: grün. `npm run test:scripts` (seriell): 198 bestanden, 0 Fehler.

### README von Hand

Der Ereignis-Katalog (`## Ereignisse`) und die Zeile unter `## Key events` brauchen Handarbeit, weil `append` dort hinter Fließtext landen würde.

1. Neuer Eintrag im Ereignis-Katalog, direkt hinter dem Bullet `- \`EntryCategorizationWrittenBackToYnab\`: ...` (endet mit `Historien-Satz: "Kategorie in YNAB angekommen".`) und vor `- \`ValuationRecorded\``:

```
- `EntryReplacedInYnab`: eine Split-Buchung ist in YNAB gelöscht und unter neuer Id
  angelegt worden (ADR 0058 Punkt 2 und 5, ledger-kxme4). Payload: `OldYnabTransactionId`,
  `NewYnabTransactionId`, `NewImportId` (Option, die alte `import_id` plus `:r<n>`).
  Schema-Version 1, Actor `UserInBb`. `linksFrom` stiftet die neue YNAB-Id, die neue
  `import_id` und hält die alte YNAB-Id als `ynabsuperseded` fest; `apply` leert
  `Entry.SplitTransferCounterparts` (der Spiegel zieht sie nach, ADR 0046). Folgt auf
  `EntryRecategorized`; war ein Fehlschlag offen, folgt danach
  `EntryCategorizationWrittenBackToYnab`. Historien-Satz: "In YNAB neu angelegt
  (Split-Buchung ersetzt)".
```

2. Der Katalog-Eintrag `EntryCategorizationWriteBackFailed` bekommt das neue Feld. Alter Text: `` `BaselineCategory` und `BaselineSplits` (der Stand, den YNAB vermutlich noch hat). `` Neuer Text: `` `BaselineCategory` und `BaselineSplits` (der Stand, den YNAB vermutlich noch hat) und `YnabDeletedTransactionId` (Option: beim Ersetzen einer Split-Buchung ist das DELETE gelungen und das POST gescheitert, die Buchung fehlt in YNAB; bleibt über weitere Fehlschläge erhalten, "Erneut senden" ruft dann nur das POST; fehlt das Feld im gespeicherten JSON, gilt `None`). ``

3. Unter `## Key events`, Zeile `- *EntryCategorizationWriteBackFailed* / *EntryCategorizationWrittenBackToYnab*: ...`: hinter dieser Zeile eine neue Zeile einfügen:

```
- *EntryReplacedInYnab*: eine Split-Buchung wurde in YNAB gelöscht und neu angelegt (neue YNAB-Id, neue `import_id`, ADR 0058). Version 1, Actor `UserInBb`. Die alte YNAB-Id gilt als abgelöst, ihre Lösch-Beobachtung setzt `DeletedInYnab` nicht. Historie: "In YNAB neu angelegt (Split-Buchung ersetzt)".
```

4. Das Ubiquitous-language-Bullet `**Fremd-Id-Verknüpfung**` (Zeilen 61 bis 64): Der Satz `eine Id aus einem fremden System (\`ynab\`, \`importid\`, \`comdirect\`)` wird zu `eine Id aus einem fremden System (\`ynab\`, \`importid\`, \`comdirect\`, \`ynabsuperseded\` für abgelöste YNAB-Ids nach einem Ersatz, ADR 0058)`.

5. Das Bullet `**Kategorie nicht in YNAB**` endet mit `Die Leseform ist \`LedgerEntryView.CategorizationWriteBackFailure\` (nur der Grund).`; danach anhängen: ` Beim Ersetzen einer Split-Buchung trägt der Fehlschlag zusätzlich \`YnabDeletedTransactionId\` ("in YNAB gelöscht, Neuanlage offen", ADR 0058).`

6. Der Satz im Bullet `*KategorieÄndern*` `Abgelehnt werden (...): ... Split-Buchung, unbekannte Kategorie-Id.` bleibt gültig; `setEntryCategory` lehnt zusätzlich eine Buchung ab, deren offener Fehlschlag noch einen Split als Baseline trägt ("bitte erneut senden").
