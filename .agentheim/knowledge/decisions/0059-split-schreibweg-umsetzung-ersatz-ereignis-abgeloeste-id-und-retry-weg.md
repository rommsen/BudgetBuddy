---
id: ADR-0059
title: Split-Schreibweg, Umsetzung: Ersatz-Ereignis, abgelöste Id als Verknüpfung, Weg nach Baseline, Tracking-Prüfung per Konten-Abruf
scope: ledger
status: accepted
date: 2026-10-24
related_tasks: [ledger-kxme4, ledger-pafkp]
related_adrs: [0006, 0015, 0046, 0055, 0058]
---

# ADR-0059: Split-Schreibweg, Umsetzung

## Context

ADR 0058 legt fest, wie Splits nach YNAB geschrieben werden (PUT zum Aufteilen, DELETE und POST
zum Ändern und Aufheben, abgeleitete `import_id`, abgelöste Id). Beim Bau von `ledger-kxme4`
blieben vier Umsetzungsfragen offen, die 0058 nicht beantwortet.

## Decision

1. **Ein neues Ereignis `EntryReplacedInYnab`** (Version 1, Actor `UserInBb`, Payload alte und
   neue YNAB-Id, neue `import_id` als Option). `linksFrom` stiftet daraus drei Verknüpfungen: die
   neue YNAB-Id, die neue `import_id` und die alte YNAB-Id im neuen Fremdsystem
   `ynabsuperseded` (`LinkSystem.YnabSupersededSystem`). `apply` leert
   `SplitTransferCounterparts`, weil die Subtransaction-Ids und Gegenbuchungen neu sind; der
   Spiegel zieht sie nach (ADR 0046). Die alte YNAB-Verknüpfung bleibt bestehen, damit der Spiegel
   die Buchung über die alte Id findet. `currentYnabTransactionId` wählt die Verknüpfung, die nicht
   abgelöst ist. Der Spiegel überspringt eine Lösch-Beobachtung zu einer abgelösten Id
   (`isSupersededDeletion`).
2. **Der Zustand "in YNAB gelöscht, Neuanlage offen"** liegt am Fehlschlag, nicht an einem
   eigenen Ereignis: `EntryCategorizationWriteBackFailed` und `CategorizationWriteBackFailure`
   bekommen das optionale Feld `YnabDeletedTransactionId` (alte JSON-Zeilen lesen sich als `None`).
   Es bleibt über weitere Fehlschläge erhalten. Solange es gesetzt ist, gilt die Id für den Spiegel
   als abgelöst, und "Erneut senden" ruft nur das POST.
   Deshalb ruft der Befehl `YnabClient.deleteTransaction` und `createReplacement` einzeln
   statt `replaceSplitTransaction` (ADR 0058, Punkt 3): nur so lässt sich zwischen beiden
   Aufrufen der Zustand "Neuanlage offen" festhalten. `replaceSplitTransaction` bleibt als
   Zusammenfassung beider Schritte im Client.
3. **Der Weg nach YNAB folgt der Baseline.** Hält YNAB (laut Baseline des offenen Fehlschlags,
   sonst laut BB-Stand vor der Änderung) keinen Split, ist es ein PUT (Aufteilen, oder bei einer
   Zeile der Kategorie-PUT); hält YNAB einen, sind es DELETE und POST. Ein gescheitertes Aufteilen
   wird beim erneuten Senden deshalb wieder ein PUT, ein gescheitertes Aufheben wieder DELETE und
   POST. `setEntryCategory` lehnt eine Buchung ab, deren offener Fehlschlag noch einen Split als
   Baseline trägt, weil YNAB eine Kategorie auf einer Split-Buchung still ignoriert (0058, Punkt 4).
   Die API-Methode `retryCategorizationWriteBack` ruft dafür `retryCategorizationOrSplitWriteBack`.
4. **Erfolg schreibt `EntryCategorizationWrittenBackToYnab` nur bei einem vorher offenen
   Fehlschlag** (ADR 0055, Punkt 2 und 4), auch beim Split-Schreibweg. Ein Aufteilen oder Ersetzen
   ohne offenen Fehlschlag erzeugt neben `EntryRecategorized` (und beim Ersatz `EntryReplacedInYnab`)
   kein weiteres Ereignis.
5. **Tracking-Konten** erkennt der Befehl über einen eigenen leichten Abruf `GET .../accounts`
   (`YnabClient.isTrackingAccount`, `on_budget`), weil `LedgerAccount` kein `OnBudget` führt (ADR 0055,
   Punkt 7). Der Abruf läuft erst nach allen lokalen Sperren und vor dem Ereignis; scheitert er,
   lehnt der Befehl ab. Kostet einen Aufruf je geänderter Split-Anfrage.

## Consequences

- Positiv: der Ersatz ist im Log sichtbar und replay-fähig; kein neuer Spalten-Schema-Eintrag auf
  `ledger_entries`, die abgelösten Ids liegen in der bestehenden Fremd-Id-Tabelle.
- Negativ: ein Ersatz kostet zwei YNAB-Aufrufe plus einen Konten-Abruf, ein Aufteilen einen Konten-Abruf
  plus den PUT (Ratenbegrenzung rund 200 je Stunde).
- Offen: scheitert das POST nach einem tatsächlich angekommenen Request (Zeitüberschreitung), liefert
  "Erneut senden" mit derselben `import_id` ein 409 aus YNAB und bleibt im Fehlschlag, bis Roman die
  Buchung in YNAB prüft. Ein Weg dafür ist nicht gebaut.
