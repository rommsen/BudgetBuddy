---
id: ledger-fgw2q
title: "Nicht auswertbare YNAB-Antwort beim Comdirect-Push lässt die Session ohne Verknüpfung abschließen"
status: backlog
type: bug
context: ledger
created: 2026-09-25
completed:
depends_on: []
blocks: []
tags: [ledger, push, session]
related_adrs: [0021]
related_research: []
prior_art: [ledger-na3rq]
---

## Why

`Server.PushOutcomeClassification.classifyPushOutcomes` (ledger-na3rq) hat sichtbar
gemacht, aber bewusst nicht behoben: ein `WrittenNotLinked`-Outcome ohne gemeldete
Duplikat-ImportId (das heißt, YNABs Antwort auf den Push war nicht auswertbar,
`TransactionCreateResult.ResponseUnparseable = true`) zählt weder als Duplikat
(`DuplicateTransactionIds`) noch als gescheitertes Kassenbuch-Schreiben
(`LedgerWriteFailureSummary`). Auf dem NORMALEN Import-Push (`importToYnab`,
`forceImport = false`) wird `SessionMayComplete` in diesem Fall also weiterhin `true`,
obwohl keine der Buchungen dieses Versuchs bestätigt mit YNAB verknüpft wurde. Die
Buchungen bleiben zwar unverändert stehen (kein Rollback, ADR 0021 Punkt 2) und der
nächste Spiegel-Lauf verknüpft sie nach, aber die Session schließt bereits jetzt ab, ohne
dass Roman erfährt, dass die Antwort nicht ausgewertet werden konnte. (Auf dem Force-Pfad
ist dieser konkrete Fall seit einer zweiten Iteration von ledger-na3rq bereits behoben:
dort blockiert eine im Force-Batch selbst unverändert gebliebene Buchung den Abschluss.)

## What

Klären (tactical-modeler oder Roman direkt), ob eine nicht auswertbare Antwort auf dem
NORMALEN Import-Push ebenfalls `SessionMayComplete = false` ergeben soll (wie auf dem
Force-Pfad) oder ob ein anderer sichtbarer Hinweis genügt (z. B. eine eigene
Fehlermeldung analog zum `LedgerWriteFailureSummary`-Pfad). `classifyPushOutcomes` ist der
richtige, schon getestete Ort für die Änderung.

## Acceptance criteria

- [ ] Entscheidung getroffen und in `classifyPushOutcomes` bzw. seinen Aufrufstellen
      umgesetzt.
- [ ] Regressionstest: eine Klassifikation mit mindestens einem `WrittenNotLinked`-Outcome
      ohne gemeldete Duplikat-ImportId auf dem normalen Import-Push ergibt das neu
      festgelegte Verhalten.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

Gefunden bei der Extraktion in `ledger-na3rq`; siehe dort den Test "WrittenNotLinked ohne
gemeldete Duplikat-ImportId (nicht auswertbare Antwort) bleibt unverändert und blockiert
den Abschluss nicht", der das heutige (auf dem normalen Pfad unveränderte) Verhalten
dokumentiert, sowie den neuen Force-Pfad-Test in derselben Datei, der zeigt, dass genau
dieser Fall auf dem Force-Pfad bereits blockiert.