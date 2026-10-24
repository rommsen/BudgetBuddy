---
id: ledger-kxme4
title: "Split-Buchungen in BB bearbeiten, Schreibweg: Befehl setEntrySplits mit Prüfung Summe gleich Betrag, EntryRecategorized mit Splits, eigener Push nach YNAB, derselbe Fehlschlag-Zustand wie bei der Kategorie"
status: backlog
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [ledger-k2rbn, ledger-mas5g]
blocks: [ledger-pafkp]
tags: [backend, kassenbuch, split, v2, schreibweg]
related_adrs: [0006, 0019, 0028, 0038, 0045, 0046, 0054, 0055, 0058]
related_research: []
prior_art: [ledger-k2rbn]
---

## Why
Roman hat am 2026-10-24 entschieden, die Split-Bearbeitung mitzubauen (der Pfeil an der
Kategorie einer Split-Buchung öffnet einen Split-Editor). Dies ist der Server-Teil. Der
Aufruf nach YNAB hängt am Ergebnis von `ledger-mas5g`.

## What
Wird nach dem Spike verfeinert; der Rahmen steht:
1. **Befehl** `setEntrySplits: Guid -> TransactionSplit list -> Async<Result<CategoryChangeOutcome, string>>`
   (Ergebnis-Typ aus `ledger-k2rbn`). Leere Liste hebt den Split auf, dann braucht die Buchung
   eine Kategorie (`setEntryCategory` danach oder gleichzeitig, Entscheidung beim Bau).
2. **Validierung** über die gemeinsamen Helfer aus `Shared.Domain` (ADR 0006: Summe der
   Zeilen gleich Betrag, mindestens zwei Zeilen, jede Zeile Kategorie, Übertrag oder
   unkategorisiert). `apply` bleibt bei "Kategorie XOR Splits".
3. **Ereignis** `EntryRecategorized` (`OldSplits`, `NewSplits`), danach der Push über eine
   eigene benannte Funktion `YnabClient.updateSplits` (Form laut Spike), dieselben Zustände
   `EntryCategorizationWriteBackFailed` mit Baseline und `WrittenBackToYnab`, dieselbe
   Schreib-Sperre, dieselbe `observe`-Regel (ADR 0055, Punkte 4 bis 6).
4. **Übertrags-Zeilen:** Der Schreibweg ändert Gegenbuchungen nicht (ADR 0046); ob eine
   Übertrags-Zeile neu entstehen darf, steht im Spike-Ergebnis.

## Acceptance criteria
- [ ] To be defined after `ledger-mas5g`.

## Notes
Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (e, Server). Ziel-Lage: backlog.
Dateien: `src/Shared/Domain.fs`, `Api.fs`, `src/Server/Api.fs`, `YnabClient.fs`,
`Ledger/CommandHandlers.fs`, `Domain.fs` (nur Nutzung), Tests. Überschneidung mit
`ledger-k2rbn` und `ledger-w29y6` in `CommandHandlers.fs`/`YnabClient.fs`: nach
`ledger-k2rbn` bauen.

## Spike-Ergebnis (ledger-mas5g, 2026-10-24)

Die YNAB-OpenAPI schließt das Ändern von `subtransactions` einer bestehenden Split-Buchung
aus: "Updating `subtransactions` on an existing split transaction is not supported and will
return an error." Ebenso: "If an existing transaction is a split, the `category_id` cannot
be changed." (https://api.ynab.com/papi/open_api_spec.yaml, Version 1.87.0). Damit sind das
Ändern der Zeilen und das Aufheben eines Splits per Update nicht möglich. Das Aufteilen einer
unaufgeteilten Buchung per PUT ist in der Spec weder erlaubt noch verboten (nicht belegt).

Der Rahmen ändert sich so:
1. Punkt 3 "eigene Funktion `YnabClient.updateSplits`" wird `YnabClient.replaceSplitTransaction`:
   DELETE der Buchung, danach POST mit gleicher `import_id`, gleichem Datum, Betrag, Konto,
   Payee, Flag, ausdrücklich gesetztem `cleared` und `approved` und den neuen
   `subtransactions` (beim Aufheben mit `category_id`). Für eine unaufgeteilte Buchung zuerst
   PUT mit `category_id: null` und `subtransactions` versuchen, bei Ablehnung oder
   stillschweigendem Ignorieren (Antwort ohne `subtransactions`) derselbe Ersatzweg.
2. Der Fehlschlag-Zustand (ADR 0055, Punkt 4) braucht einen zusätzlichen Grund "in YNAB
   gelöscht, Neuanlage offen". "Erneut senden" muss POST allein ausführen können.
3. Die neue YNAB-Id (und neue Subtransaction-Ids, ADR 0046) aus der POST-Antwort in den
   Eintrag übernehmen; `observe` und der Spiegel führen alte (gelöscht) und neue Id über die
   `import_id` zusammen. Übertrags-Gegenbuchungen entstehen neu (Annahme, siehe ADR 0058);
   die Nachverknüpfung aus `ledger-h6mer` muss nach jedem Ersatz laufen.
4. Buchungen mit `cleared: reconciled` im Schreibweg ablehnen, bis der Probelauf zeigt, wie
   YNAB sie behandelt.
5. Vorbedingung: Probelauf gegen ein Wegwerf-Budget (fünf Aufrufe, Liste im Outcome von
   `ledger-mas5g`) und Annahme von ADR 0058 (proposed). Erst danach verfeinern und bauen.
6. Punkt 4 (Übertrags-Zeilen) ist beim Neuanlegen schreibbar: Transfer-Payee-Auflösung wie
   ADR 0006; Tracking-Konto-Regel der Spec ("Splits are not allowed on tracking accounts or
   on transfers between on-budget accounts") im Validierungsschritt prüfen.

Acceptance criteria bleiben "to be defined", bis ADR 0058 angenommen ist.
