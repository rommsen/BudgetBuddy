---
id: categorization-gdrx8
title: "Review-Spur: SyncTransaction hält Kategorisierungs-Schritte mit Zeitpunkt und Herkunft fest"
status: doing
type: feature
context: categorization
created: 2026-09-11
completed:
depends_on: []
blocks: [ledger-qvt84]
tags: [categorization, history, sync-session]
related_adrs: [0021]
related_research: []
prior_art: []
---

## Why
Roman hat entschieden (2026-09-11), dass Regel-Vorschlag und Romans Korrektur beim Push
als Ereignisfolge im Kassenbuch sichtbar werden ("Regel X: Lebensmittel", dann "Roman in
BB: Drogerie"). So sieht man später, welche Regeln danebenliegen. Heute überschreibt
`categorizeTransaction` (`src/Server/Api.fs`) Kategorie und `MatchedRuleId` still; eine
vorherige Regel-Entscheidung ist danach nicht mehr rekonstruierbar. Da die
Kassenbuch-Buchung laut ADR 0021 erst mit dem Push entsteht, muss die Review-Spur bis
dahin in der SyncSession mitgeführt werden.

## What
1. `SyncTransaction` (`src/Shared/Domain.fs`) um ein Feld `ReviewSteps: ReviewStep list`
   erweitern:
   ```
   type ReviewActor = ByRule of RuleId * ruleName: string | ByUser
   type ReviewStep = {
       At: DateTime
       By: ReviewActor
       Category: YnabCategoryId option
       CategoryName: string option
       Splits: TransactionSplit list option
       PayeeOverride: string option
   }
   ```
   Die Reihenfolge (ältester Schritt zuerst empfohlen, damit sie direkt in
   Ereignis-Reihenfolge übersetzbar ist) beim Implementieren festlegen und im Code
   dokumentieren.
2. Jeder Schritt, der Kategorie, Splits oder PayeeOverride ändert (Regel-Anwendung,
   `categorizeTransaction`, `clearSplit`, Aufrufe aus dem Split-Editor), hängt einen
   Schritt an, statt nur den aktuellen Stand zu überschreiben. Eine Wiederholung mit
   demselben Wert erzeugt keinen neuen Schritt.
3. Persistenz (`Persistence.SyncTransactions`) um die Review-Spur erweitern.
4. Für Roman sichtbar ändert sich in diesem Task nichts; die Spur wird nur mitgeführt.
   `ledger-qvt84` liest sie beim Push.

## Acceptance criteria
- [ ] Regel-Anwendung gefolgt von manueller Korrektur ergibt zwei Einträge in
      `ReviewSteps` mit unterschiedlichem `At` und `By`.
- [ ] Eine erneute Kategorisierung mit demselben Wert erzeugt keinen weiteren Eintrag.
- [ ] Die Review-Spur übersteht einen Server-Neustart (Persistenz-Test gegen
      In-Memory-SQLite).
- [ ] Reducer- und Client-Code kompilieren mit dem erweiterten Typ; keine Tests über
      Pfade, die den Fable.Remoting-Proxy berühren.
- [ ] `dotnet build`, `dotnet test` und `npm run build` grün.

## Notes
Context `categorization` gewählt, weil die Schritte fachlich dessen Sprache sind
(`AutoCategorized`, `ManualCategorized`, Regel); der Lebenszyklus der SyncSession selbst
bleibt unverändert (Empfehlung strategic-modeler, 2026-09-11). Kann parallel zu
`ledger-cscw1` laufen.
