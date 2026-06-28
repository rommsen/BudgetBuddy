# ADR 0011: Kategorie-Available im Zuweisungs-Picker, optionaler Decode, Drei-Farben-Semantik
scope: categorization · status: accepted · date: 2026-06-28 · related_tasks: ynab-k7m3q

## Context
Beim Zuordnen einer Kategorie (Import, Split, Quick-Add) soll am Entscheidungspunkt
sichtbar sein, wie viel in der Kategorie aktuell verfügbar ist (YNAB "Available"), ohne
Wechsel nach YNAB-Web. Der gemeinsame Category-Picker (`BottomSheet.fs`, ADR 0005) trug
bisher nur `(categoryId, categoryName)`-Tupel und konnte keinen Wert pro Zeile rendern.
Drei offene Punkte: (1) wie der Wert dekodiert wird, (2) wie der Picker ihn transportiert,
(3) wo er sichtbar wird.

## Decision
1. **Datenseite — Available auf `YnabCategory`.** `YnabCategory` trägt `Available: Money`,
   dekodiert aus YNABs `balance` (Milliunits/1000, gleicher Pfad wie die Account-Balance).
   Decode ist **Optional mit 0-Default**, nicht Required wie die Account-Balance: der
   Conformist-Vertrag mit YNAB soll nicht den ganzen Kategorien-Load brechen, falls eine
   (interne/spezielle) Kategorie je ohne `balance` kommt. Kein zusätzlicher Request — der
   Wert kommt aus der bestehenden `getCategories`-Antwort.
2. **Transport — reicherer Picker-Option-Typ.** Statt `(string * string)` nimmt der Picker
   `BottomSheet.CategoryPickerOption = { Id; Label; Available: Money option }`. `Available`
   ist optional, damit ein Aufrufer, für den ein Budgetwert kontextlos wäre, die schlichte
   Zeile behält. ADR 0005 bleibt unangetastet (Click-Commit, Layer-Stacking des
   Split-Pickers); ergänzt wird nur eine rechtsbündige Wert-Spalte pro Zeile.
3. **Optik — Drei-Farben-Semantik über DS-Tokens.** `Money.available` rendert mono,
   tabular-nums, ohne Glow/führendes "+": grün (`text-neon-green`) bei >0, rot
   (`text-neon-red`) bei <0, neutral (`text-text-muted`) bei =0. Bewusst drei-wertig, weil
   die bestehende `Money.view`-Färbung 0 als positiv (grün) behandelt; ein 0-Available ist
   aber neutral, kein Erfolg.
4. **Scope — alle Zuweisungs-Picker, nicht der Regel-Editor.** Sichtbar in Import
   (`TransactionList`), Split (`SplitSheet`) **und** Quick-Add (`QuickAddPage`). Quick-Add
   teilt Komponente und Daten und ist derselbe "Kategorie an eine Transaktion zuordnen"-
   Kontext; den Wert dort zu unterdrücken wäre der unnötige Sonderfall. Der **Regel-Editor**
   (`Components/Rules/View.fs`) bleibt unverändert — er nutzt den Picker nicht und ein
   "aktuell verfügbar" für eine Config-Regel wäre kontextlos.

## Consequences
- `TransactionList` baut eine separate `pickerCategoryOptions`-Liste; das schlichte
  `(string*string)`-`categoryOptions` bleibt für `TransactionRow` (Chip-Label) erhalten.
  `QuickAddPage` behält ebenfalls das Tupel für `selectedCategoryName`.
- Datenfrische = Stand des letzten Kategorien-Loads (`model.Categories`), kein Live-Refresh
  pro Zeile. Für die Import-Zuordnung ausreichend.
- Sollte YNAB künftig `balance` verlässlich liefern und ein lauter Vertrag gewünscht sein,
  kann der Decode auf Required umgestellt werden (dann Fixtures anpassen).

## References
- src/Shared/Domain.fs (`YnabCategory.Available`)
- src/Server/YnabClient.fs (`categoryAvailable`, `categoryDecoder`, `categoryInGroupDecoder`)
- src/Client/DesignSystem/BottomSheet.fs (`CategoryPickerOption`), Money.fs (`available`)
- src/Client/.../TransactionList.fs, SplitSheet.fs, QuickAddPage.fs
- ADR 0005 (Picker-Patterns, unberührt)

## Amendment 2026-06-28 — Ready-to-Assign zeigt den echten Wert, `Available` wird optional
Romans Gerätetest: die "Inflow: Ready to Assign"-Zeile zeigte einen Fantasiewert
(787955,35), normale Kategorien waren korrekt. Ursache: das Kategorie-`balance` der internen
"Internal Master Category" ist für die Inflow-Zeile ein interner Akkumulator, nicht der echte
Ready-to-Assign. Der echte Wert ist das `to_be_budgeted` des Monats
(`GET /budgets/{id}/months/current`).

Entscheidung:
- `YnabCategory.Available` wird `Money option` (vorher `Money` mit 0-Default). `None` = keine
  Zahl. Der Picker-Option-Typ war schon optional, die View ändert sich nicht.
- Reine Domain-Funktion `applyReadyToAssign` (`src/Shared/Domain.fs`) ersetzt die Available
  der Inflow-Zeile (Match auf Gruppe "Internal Master Category" + Name "Inflow: Ready to
  Assign") durch `to_be_budgeted`. Schlägt der Month-Call fehl → `None` statt Müll.
- Nur die Picker-serving API (`Api.getCategories`) holt zusätzlich `to_be_budgeted`
  (`YnabClient.getReadyToAssign`, `Decoders.monthToBeBudgetedDecoder`) und wendet die
  Funktion an: +1 Call pro Sync, nicht auf den internen getCategories-Pfaden.
- Fehlendes `balance` dekodiert jetzt zu `None` (vorher 0) — kein irreführendes 0,00.
