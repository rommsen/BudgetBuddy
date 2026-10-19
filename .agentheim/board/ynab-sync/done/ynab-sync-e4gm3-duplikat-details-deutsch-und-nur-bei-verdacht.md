---
id: ynab-sync-e4gm3
title: Import prüfen: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter "Details"; YNAB-Ergebnis getrennt
status: done
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
  - Die Zeile "New" bei fehlendem Import-ID-Treffer wird "Neu".
- **Sichtbarkeit.** Reine Funktion `duplicateDetailsMode status : Shown | BehindLink`:
  `Shown` bei `ConfirmedDuplicate` und `PossibleDuplicate`, `BehindLink` bei `NotDuplicate`.
  Der Status allein reicht (geprüft 2026-10-19, siehe Notes): `DuplicateDetection.fs` ~119-137
  macht jeden Referenz- oder Import-ID-Treffer zu `ConfirmedDuplicate` und jeden Fuzzy-Treffer zu
  `PossibleDuplicate`; `NotDuplicate` trägt nie einen Treffer.
  Bei `BehindLink` steht im aufgeklappten Bereich ein gedämpfter Link "Details", der die
  Duplikat-Prüfung aufklappt (lokaler Zustand im Modell der Zeile oder der Liste, Wahl des Workers).
- **Hinweis-Zeile deutsch.** Die Zeile "Hinweis" bei `PossibleDuplicate` (`TransactionRow.fs` ~393)
  zeigt heute den Server-Text `reason` ("Similar transaction found: X on 2026-10-07 for 12.34").
  Sie wird im Client aus den Fuzzy-Feldern gebaut, über dieselbe reine Funktion wie die
  Fuzzy-Zeile der Duplikat-Prüfung (z. B. `fuzzyMatchText details : string option`
  → "Gefunden: Rewe am 07.10.2026 über 12,34 €"). Fehlen die Fuzzy-Felder, steht
  "Ähnliche Buchung in YNAB". Der `reason`-String wird im Client nicht mehr angezeigt;
  der Server bleibt unverändert (Entscheidung Roman, 2026-10-19).
- **Ruhig.** Kein Teal-Kasten; Schrift in `ListRoles.secondary`/`muted`, Mono nur für IDs.
- **"Überspringen"** im Aktionsbereich ist ein neutraler Chip, kein `action-chip danger`
  (die Aktion ist per Schalter und Wischen umkehrbar).

## Acceptance criteria
- [ ] `duplicateDetailsMode` liefert `Shown` für `ConfirmedDuplicate` und `PossibleDuplicate`, `BehindLink` für `NotDuplicate`. Ein Expecto-Test je Fall.
- [ ] `fuzzyMatchText` liefert für Payee "Rewe", Datum 07.10.2026, Betrag -12,34 genau den Text mit `dd.MM.yyyy` und `Money.formatAmountText`; ohne Payee steht ein Ersatztext statt "?"; ohne Fuzzy-Felder `None`. Tests.
- [ ] Die Hinweis-Zeile bei `PossibleDuplicate` zeigt den Text aus `fuzzyMatchText` bzw. "Ähnliche Buchung in YNAB"; `TransactionRow.fs` bindet den `reason` aus `PossibleDuplicate` nicht mehr an `prop.text` (Quelltext-Zusicherung).
- [ ] Eine reine Funktion entscheidet, ob das YNAB-Ergebnis angezeigt wird; Test: `RejectedByYnab` bei `NotDuplicate` wird angezeigt, ohne Importergebnis nicht.
- [ ] `TransactionRow.fs` enthält keines der Literale "BudgetBuddy Duplicate Detection", "Reference:", "Import ID:", "Found in YNAB", "Not in YNAB", "Exists in YNAB", "Fuzzy match", "Successfully imported", "YNAB rejected", "missed this" und kein String-Literal `"New"` (Quelltext-Zusicherung), und enthält die deutschen Entsprechungen.
- [ ] Der Fuzzy-Text nutzt `Money.formatAmountText` und kein `sprintf "%.2f"` und kein `yyyy-MM-dd` (Quelltext-Zusicherung).
- [ ] `TransactionRow.fs` enthält kein `action-chip danger` mehr (Grep-Wächter).
- [ ] Playwright am gemockten Lauf (Mock wie in `ynab-sync-aj5tv`, mit einer Verdachtszeile und einer unauffälligen Zeile): die Verdachtszeile zeigt aufgeklappt "Duplikat-Prüfung" ohne weiteren Klick; die unauffällige zeigt "Details" und erst nach Klick die Prüfung. Ergebnis im Diary.
- [ ] Nachher-Bilder `docs/ux/ynab-sync/import-pruefen-ruhig/{pc-1440x900,handy-390x844}-import-pruefen-duplikat-details.png` (beide Zeilenarten, einmal mit geöffnetem "Details").
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`, `npm run test:scripts` grün.

## Notes
- **Geprüft (Refine 2026-10-19):** Referenz-/Import-ID-Treffer treten bei `NotDuplicate` nicht auf. `DuplicateDetection.fs` ~119-137 priorisiert Referenz > Import-ID > Fuzzy und wählt beim ersten Treffer `ConfirmedDuplicate` bzw. `PossibleDuplicate`. `Persistence.fs` ~896 und `RulesEngine.fs` ~252/269 setzen `NotDuplicate (emptyDetectionDetails …)`. Deshalb hängt `duplicateDetailsMode` nur am Status.
- **Hinweis:** Nach dem Neuladen einer Sitzung ist der Duplikat-Status immer `NotDuplicate` (nicht persistiert, `Persistence.fs` ~896). Das ist bekanntes Verhalten und nicht Teil dieser Aufgabe.
- Zeilenangaben in `TransactionRow.fs` geprüft am Stand nach `ynab-sync-aj5tv`: `duplicateDebugInfo` ~79-180, Aufruf ~404, Hinweis-Zeile ~393, "Überspringen" ~478.
- **Risiko:** die Meldung "YNAB lehnt ab, BudgetBuddy hat es nicht erkannt" darf beim Ausblenden nicht verschwinden; deshalb die Trennung.
- **Nicht in dieser Aufgabe:** Änderungen an der Duplikat-Erkennung selbst (Server, `Shared/Domain.fs`), neue Aktionen.
- Größe S bis M.

## Verifier note (iteration 1)

**REASONS:**
- Kriterium "Eine reine Funktion entscheidet, ob das YNAB-Ergebnis angezeigt wird" nicht erfüllt: `showYnabResult` (TransactionRow.fs:105) wird nirgends aufgerufen; `ynabResultView` entscheidet selbst per `match tx.YnabImportStatus with NotAttempted -> Html.none`. Die drei Tests sichern damit die Ansicht nicht ab.
- Handy-Bild nur teilweise: es zeigt die unauffällige Zeile mit offenem "Details", von der Verdachtszeile nur Titel, Chip und Memo (am Bottom-Nav abgeschnitten), ohne Hinweis-Zeile und ohne sofort sichtbare Duplikat-Prüfung. PC-Bild ist in Ordnung.

**SUGGESTED_FIX:** `ynabResultView` nimmt die Sichtbarkeit aus `showYnabResult` (z. B. `if not (showYnabResult tx.YnabImportStatus) then Html.none else ...`). Handy-Bild neu aufnehmen, sodass auch Hinweis-Zeile und Duplikat-Prüfung der Verdachtszeile sichtbar sind (scrollen, zweites Bild oder Verdachtszeile zuerst).

**ITERATION_HINT:** likely-fixable

## Outcome

`duplicateDebugInfo` in `src/Client/Components/SyncFlow/Views/TransactionRow.fs` ist zerlegt. Reine Funktionen: `duplicateDetailsMode` (Shown bei ConfirmedDuplicate/PossibleDuplicate, BehindLink bei NotDuplicate), `fuzzyMatchText` ("Gefunden: Rewe am 07.10.2026 über 12,34 €", Ersatztext "unbekannter Empfänger", None ohne Fuzzy-Felder, Betrag über `Money.formatAmountText`), `showYnabResult`. `DuplicateDetailsBlock` (React-Komponente, useState) zeigt die Prüfung bei Verdacht sofort, sonst hinter dem Link "Details". `ynabResultView` zeigt das YNAB-Ergebnis getrennt; ab Iteration 2 entscheidet `showYnabResult tx.YnabImportStatus` die Sichtbarkeit (Quelltext-Zusicherung sichert das). Die Hinweis-Zeile bei PossibleDuplicate nutzt `fuzzyMatchText` bzw. "Ähnliche Buchung in YNAB", der Server-`reason` wird nicht mehr angezeigt. "New" ist "Neu", "Überspringen" ein normaler `action-chip`. Server und Shared unverändert. `.action-chip.danger` in styles.css blieb stehen (ungenutzt).

Iteration 2: (1) showYnabResult wird jetzt vom View aufgerufen, plus Quelltext-Test. (2) Zweites Bild je Viewport mit der Verdachtszeile (Hinweis-Zeile und sofort sichtbare Duplikat-Prüfung): `{pc-1440x900,handy-390x844}-import-pruefen-duplikat-verdacht.png`; die `-duplikat-details.png`-Bilder zeigen die unauffällige Zeile mit geöffnetem "Details". Playwright-Wächter `scripts/import-pruefen-duplikat-details.test.mjs` schreibt beide Bilder mit SHOTS_DIR. Alle Bilder liegen unter `docs/ux/ynab-sync/import-pruefen-ruhig/`.

Tests: 18 neue Expecto-Tests in `src/Tests/SyncFlowViewTests.fs`. Läufe: dotnet build 0 Warnungen, 0 Fehler; dotnet test 1298 bestanden, 7 übersprungen, 0 Fehler; npm run build ok; guard:classes ok; test:scripts 122 von 122 grün. Selbstcheck ohne Treffer (nur Dateinamen mit "pruef").
