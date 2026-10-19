---
id: ynab-sync-e4gm3
title: Import prüfen: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter "Details"; YNAB-Ergebnis getrennt
status: backlog
type: feature
context: ynab-sync
created: 2026-10-19
completed:
depends_on: [ynab-sync-aj5tv]
blocks: []
tags: [frontend, ruhige-listen, duplikate]
related_adrs: [0049]
related_research: []
prior_art: []
---

## Why
Befund S6 der Bestandsaufnahme vom 2026-10-19 (`docs/ux/design-system/audit-2026-10-19/report.md`,
Bild `pc-1440x900-import-pruefen-aufgeklappt.png`): Der englische Block "BudgetBuddy Duplicate
Detection" (`duplicateDebugInfo`, `TransactionRow.fs` ~98-205) erscheint in jeder aufgeklappten
Zeile (`if isExpanded then duplicateDebugInfo tx`, ~460), auch ohne Verdacht. "Überspringen" ist
ein roter Chip.

Der Block mischt zwei Dinge: die Erkennungsdetails (Referenz, Import-ID, Fuzzy-Treffer) und das
Ergebnis des YNAB-Imports (`YnabImportStatus`: erfolgreich, abgelehnt, "BudgetBuddy missed this").
Das Ergebnis ist nach einem Import wichtig für die Fehlersuche.

Entscheidung Roman (2026-10-19): Details bei Verdacht sofort sichtbar, sonst über einen Link
"Details" erreichbar. Abgetrennt aus `ynab-sync-aj5tv`, baut auf dessen ruhigen Zeilen auf.

## What
- **Trennen.** `duplicateDebugInfo` wird in zwei Teile zerlegt:
  - Duplikat-Prüfung (Erkennung): "Duplikat-Prüfung", "Referenz", "Import-ID", "In YNAB gefunden",
    "Nicht in YNAB", Fuzzy-Treffer als "Gefunden: <Payee> am 07.10.2026 über 12,34 €"
    (Betrag über `Money.formatAmountText`, Datum `dd.MM.yyyy`).
  - YNAB-Ergebnis: "YNAB: erfolgreich importiert", "YNAB hat abgelehnt: …",
    "BudgetBuddy hat das nicht erkannt". Immer sichtbar in der aufgeklappten Zeile, wenn ein
    Importergebnis vorliegt, unabhängig vom Verdacht.
- **Sichtbarkeit.** Reine Funktion `duplicateDetailsMode tx : Shown | BehindLink`:
  `Shown` bei `ConfirmedDuplicate`, `PossibleDuplicate`, Referenz-Treffer, Import-ID-Treffer oder
  Fuzzy-Treffer (Felder aus `getDuplicateDetails`, `Shared/Domain.fs` ~238), sonst `BehindLink`.
  Bei `BehindLink` steht im aufgeklappten Bereich ein gedämpfter Link "Details", der die
  Duplikat-Prüfung aufklappt (lokaler Zustand im Modell der Zeile oder der Liste, Wahl des Workers).
- **Ruhig.** Kein Teal-Kasten; Schrift in `ListRoles.secondary`/`muted`, Mono nur für IDs.
- **"Überspringen"** im Aktionsbereich ist ein neutraler Chip, kein `action-chip danger`
  (die Aktion ist per Schalter und Wischen umkehrbar).

## Acceptance criteria
- [ ] `duplicateDetailsMode` liefert `Shown` für Confirmed, Possible, Referenz-Treffer, Import-ID-Treffer und Fuzzy-Treffer, `BehindLink` für `NotDuplicate` ohne Treffer. Ein Expecto-Test je Fall.
- [ ] Eine reine Funktion entscheidet, ob das YNAB-Ergebnis angezeigt wird; Test: `RejectedByYnab` bei `NotDuplicate` wird angezeigt, ohne Importergebnis nicht.
- [ ] `TransactionRow.fs` enthält keines der Literale "BudgetBuddy Duplicate Detection", "Reference:", "Import ID:", "Found in YNAB", "Not in YNAB", "Exists in YNAB", "Fuzzy match", "Successfully imported", "YNAB rejected", "missed this" (Quelltext-Zusicherung), und enthält die deutschen Entsprechungen.
- [ ] Der Fuzzy-Text nutzt `Money.formatAmountText` und kein `sprintf "%.2f"` und kein `yyyy-MM-dd` (Quelltext-Zusicherung).
- [ ] `TransactionRow.fs` enthält kein `action-chip danger` mehr (Grep-Wächter).
- [ ] Playwright am gemockten Lauf (Mock wie in `ynab-sync-aj5tv`, mit einer Verdachtszeile und einer unauffälligen Zeile): die Verdachtszeile zeigt aufgeklappt "Duplikat-Prüfung" ohne weiteren Klick; die unauffällige zeigt "Details" und erst nach Klick die Prüfung. Ergebnis im Diary.
- [ ] Nachher-Bilder `docs/ux/ynab-sync/import-pruefen-ruhig/{pc-1440x900,handy-390x844}-import-pruefen-duplikat-details.png` (beide Zeilenarten, einmal mit geöffnetem "Details").
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`, `npm run test:scripts` grün.

## Notes
- **Vorher prüfen:** können Referenz-/Import-ID-Treffer bei `NotDuplicate` überhaupt auftreten? Wenn nicht, reicht der Status allein; dann die Tests entsprechend kürzen und im Outcome vermerken.
- **Risiko:** die Meldung "YNAB lehnt ab, BudgetBuddy hat es nicht erkannt" darf beim Ausblenden nicht verschwinden; deshalb die Trennung.
- **Nicht in dieser Aufgabe:** Änderungen an der Duplikat-Erkennung selbst (Server, `Shared/Domain.fs`), neue Aktionen.
- Größe S bis M.
