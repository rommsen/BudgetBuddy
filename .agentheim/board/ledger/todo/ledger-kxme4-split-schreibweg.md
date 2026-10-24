---
id: ledger-kxme4
title: "Split-Buchungen in BB bearbeiten, Schreibweg: Befehl setEntrySplits mit Prüfung Summe gleich Betrag, EntryRecategorized mit Splits, eigener Push nach YNAB, derselbe Fehlschlag-Zustand wie bei der Kategorie"
status: todo
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [ledger-k2rbn, ledger-mas5g]
blocks: [ledger-pafkp]
tags: [backend, kassenbuch, split, v2, schreibweg]
related_adrs: [0006, 0015, 0019, 0021, 0028, 0038, 0045, 0046, 0054, 0055, 0058]
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
