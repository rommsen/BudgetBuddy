---
id: ledger-cscw1
title: "Ereignis-Katalog: Entscheidungs-Ereignisse für Comdirect-Import, Regel, Korrektur, Quick Add und YNAB-Verknüpfung"
status: doing
type: feature
context: ledger
created: 2026-09-11
completed:
depends_on: []
blocks: [ledger-qvt84, ledger-tdnpk]
tags: [ledger, events, foundation]
related_adrs: [0006, 0013, 0015, 0019, 0021]
related_research: []
prior_art: [ledger-e3k7p]
---

## Why
Der Ereignis-Katalog (`ledger/README.md`, "Ereignisse") reserviert die Namen
`EntryImportedFromComdirect`, `EntryCategorizedByRule`, `EntryRecategorized` und
`CashExpenseAdded` bereits, aber ohne Payload (`ReservedDecisionEventName` in
`src/Server/Ledger/Events.fs`). Ohne Payloads kann das Kassenbuch keine BB-Entscheidung
aufnehmen, und genau das verlangt das v1-Kriterium der Vision ("merkt sich zusätzlich, was
BB selbst entschieden hat"). ADR 0021 verlangt zusätzlich ein eigenes
Verknüpfungs-Ereignis `EntryLinkedToYnab`, das bisher nicht im Katalog steht.

## What
1. Fünf Ereignistypen in `LedgerEventBody` aufnehmen (Schema-Version 1; `ValuationRecorded`
   bleibt reserviert und ist nicht Teil dieses Tasks):
   - `EntryImportedFromComdirect { AccountId: LedgerAccountId; ImportId: string;
     ComdirectReference: string; Date: DateTime; Amount: Money; Payee: string option;
     Memo: string option; Cleared: ClearedState }`. Trägt bewusst keine Kategorie: jede
     Buchung entsteht kategorielos, die Kategorisierung folgt als eigenes Ereignis in
     derselben Sequenz.
   - `EntryCategorizedByRule { RuleName: string; Category: CategoryRef option;
     Splits: TransactionSplit list; PayeeOverride: string option }`. Actor `Rule RuleId`,
     nur auf einer noch unkategorisierten Buchung zulässig.
   - `EntryRecategorized { OldCategory/NewCategory: CategoryRef option;
     OldSplits/NewSplits: TransactionSplit list; OldPayee/NewPayee: string option }`.
     Actor `UserInBb`.
   - `CashExpenseAdded { AccountId: LedgerAccountId; Date: DateTime; Amount: Money;
     Payee: string option; Memo: string option; Category: CategoryRef option;
     Cleared: ClearedState }`. Actor `QuickAdd`.
   - `EntryLinkedToYnab { YnabTransactionId: string }`. Entsteht aus einer Push-Antwort
     (Actor `ComdirectImport` bzw. `QuickAdd`) oder wenn der Spiegel eine unverknüpfte
     Buchung über die ImportId findet (Actor `YnabMirror`).
2. `apply` in `Ledger/Domain.fs` erweitern. Invarianten:
   - Genau eines der drei Entstehungs-Ereignisse (`EntryObservedInYnab`,
     `EntryImportedFromComdirect`, `CashExpenseAdded`) darf die erste Zeile einer Buchung
     sein; ein zweites Entstehungs-Ereignis auf derselben Buchung schlägt fehl. Heute
     überschreibt `apply` bei `EntryObservedInYnab` still, das wird mit behoben.
   - Kategorie und Splits schließen sich aus, die `mkSplits`-Invarianten gelten auch hier.
   - `EntryLinkedToYnab` setzt die `ynab`-Verknüpfung. Ob ein Aufruf für eine bereits
     verknüpfte Buchung ein Fehler ist oder still idempotent bleibt, entscheidet der
     Aufrufer (`appendAndApply`-Fassade), nicht `apply`.
3. Codecs in `Ledger.Events.Codec` nach bestehendem Muster.
4. `ledger/README.md`, Abschnitt "Ereignisse": Unterabschnitt "Entstehung" und die fünf
   Ereignisse mit Payload-Kurzbeschreibung ergänzen. Textvorschlag:

   > **Entstehung:** Eine Buchung entsteht mit genau einem von `EntryObservedInYnab`,
   > `EntryImportedFromComdirect`, `CashExpenseAdded`.
   >
   > **Entscheidungen** (BB hat etwas festgelegt):
   > - `EntryImportedFromComdirect`: Comdirect-Buchung beim Push angelegt, ohne Kategorie.
   > - `EntryCategorizedByRule`: eine Regel hat kategorisiert; die RuleId steht im Actor.
   > - `EntryRecategorized`: Roman hat in BB kategorisiert oder umkategorisiert.
   > - `CashExpenseAdded`: Quick-Add-Buchung angelegt, vor dem Push.
   > - `ValuationRecorded`: weiterhin reserviert.
   >
   > **Verknüpfung:** `EntryLinkedToYnab`: YNAB-Id bekannt geworden, aus der Push-Antwort
   > oder vom Spiegel über die ImportId.
5. Replay-Test (Pflicht nach ADR 0013) für alle neuen Ereignistypen.

## Acceptance criteria
- [ ] Alle fünf Ereignistypen sind in `Events.fs`, `Domain.fs` (`apply`) und im Codec
      implementiert.
- [ ] Der Replay-Test rekonstruiert den Zustand für Buchungen, die mit jeder der drei
      Entstehungsarten beginnen und danach kategorisiert und verknüpft werden.
- [ ] Zwei Entstehungs-Ereignisse auf derselben Buchungs-Id schlagen fehl (Test).
- [ ] `ledger/README.md`, Abschnitt "Ereignisse", ist aktualisiert.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Reine Fundament-Arbeit; das Verdrahten in den Push-Fluss übernehmen `ledger-qvt84`
(Import) und `ledger-tdnpk` (Quick Add). Payload-Entwurf vom tactical-modeler
(2026-09-11), Reihenfolge und `EntryLinkedToYnab` aus ADR 0021. Kann parallel zu
`categorization-gdrx8` laufen.
