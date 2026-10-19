---
id: ledger-m23nz
title: Kassenbuch-Filterleiste und Summenzeile nach ADR 0049 nachziehen
status: todo
type: feature
context: ledger
created: 2026-10-19
completed:
depends_on: [design-system-zahv9]
blocks: [design-system-m7z9q]
tags: [frontend, ruhige-listen, kassenbuch]
related_adrs: [0049, 0005]
related_research: []
prior_art: [design-system-q7rkm, ledger-n4vte]
---

## Why
Bestandsaufnahme gegen ADR 0049 (Ruhige Listen) am 2026-10-19. Bericht und Vorher-Bilder im Repo:
`docs/ux/design-system/audit-2026-10-19/report.md` (Befunde K5, K7, K8, K10, K12, K13, K14) und
`docs/ux/design-system/audit-2026-10-19/pc-1440x900-ledger*.png`, `handy-390x844-ledger*.png`.

Die Kategorie-Auswahl selbst ist mit `design-system-zahv9` ruhig (K1 bis K4). Übrig sind der
Rahmen des Popovers, die anderen Filter, die Summenzeile und zwei Abstände.

## What
Alle Zeilenangaben in `src/Client/Components/Ledger/View.fs`, Stand nach zahv9; per Symbol suchen.
Bausteine aus zahv9: `PickerParts`, `Input.Small`, `Money.listAmount`, `Tokens.ListRoles`.

1. **Popover-Breite (K5, `popoverWidth` ~1048).** Kategorie-Popover `w-[min(92vw,460px)]`
   (Entscheidung Roman: 460px wie im Handoff). Payee bleibt 340px, die übrigen 320px. Die Breite
   kommt aus der reinen Funktion `popoverWidth`. Das Styleguide-Demo (`Styleguide/View.fs` ~1007)
   nutzt dieselbe Funktion oder denselben Wert.
2. **Payee-Liste (K8, `payeePickerBody` ~975).** Name über `PickerParts.nameClass` mit `truncate`,
   Zahl rechts in `PickerParts.amountSlotClass` (`shrink-0`). Überschrift "Vorschläge aus den
   12.116 Treffern" und die Zahl je Zeile mit Tausenderpunkt. Dafür wird das private
   `Money.groupThousands` zur öffentlichen reinen Funktion `Money.formatCount (n: int)`
   (kein `ToString("N0")`, Fable ignoriert die Kultur).
3. **Memo und Betrag (K9-Rest, ~1018-1035).** `Input.Small` statt `Input.textSimple`. Betragsfelder
   zeigen sichtbar einen Euro-Hinweis (Suffix oder Platzhalter, frei). Labels "Von"/"Bis" in
   `ListRoles.muted` (12px).
4. **Zeitraum, nur Layout (K7, `datePickerBody` ~930-965).** Datumsfelder in `Input.Small`-Maßen,
   Labels 12px, Schnellwahl-Knöpfe unverändert. "Übernehmen" ohne Leuchten über
   `PickerParts.applyButtonClass` (bei Bedarf `PickerParts.footer` um eine Variante mit freier
   Beschriftung ergänzen). Das deutsche Datumsformat folgt in `design-system-m7z9q`.
5. **Summenzeile (K10, `summaryRow` ~1158).** Ohne Karte, eine schlanke Zeile: links
   "12.116 Buchungen" (`ListRoles.secondary`), rechts die Summe über `Money.listAmount`. Beim Laden
   (Entscheidung: Platzhalter) bleibt die Zeile stehen und zeigt Zahl und Betrag als schmales
   Skeleton (`Loading.skeleton`), damit die Liste nicht springt. Eine reine Funktion
   `summaryState` liefert `Hidden | Loading | Ready`, `summaryCountText` die Beschriftung.
6. **Abstand Kopf zu "KONTEN" (K12, `PageHeader.containerClass Calm`).** Fußabstand `mb-4`. Wirkt
   auch auf das Styleguide-Demo und auf die Regeln-Seite, sobald sie `PageHeader.calm` nutzt; gewollt.
7. **Memo-Spalte in "Alle Konten" (K13, `colgroup` ~786-795).** Breiten über eine reine Funktion
   `tableColumnWidths (showAccountColumn: bool)`, Memo bekommt mindestens 16% der Tabelle.
   `FixedLayout = true` bleibt Voraussetzung.
8. **Historie (K14, `historyLine` ~636-653).** Beschreibung `ListRoles.primary` statt
   `Tokens.FontSizes.body`.

## Acceptance criteria
- [ ] `Money.formatCount` ist öffentlich und rein: 0 → "0", 999 → "999", 1000 → "1.000", 12116 → "12.116", 1234567 → "1.234.567". Expecto-Test in `MoneyFormatTests.fs`.
- [ ] Payee-Überschrift und Zahl je Payee-Zeile nutzen `Money.formatCount`; `Ledger/View.fs` enthält weder `string model.TotalCount` noch `string s.Count` (Quelltext-Zusicherung in `LedgerViewTests.fs`).
- [ ] Payee-Zeile: Name trägt `truncate` und `PickerParts.nameClass` (oder `ListRoles.primary`), die Zahl liegt in einem `shrink-0`-Bereich (Quelltext-Zusicherung oder getestete Klassenfunktion).
- [ ] `popoverWidth` liefert für Kategorie 460px (`min(92vw,460px)`), für Payee 340px, sonst 320px; Test als reine Funktion. Das Styleguide-Demo nutzt dieselbe Breite (Grep-Zusicherung).
- [ ] Im Kategorie-Popover bei 1440x900 bricht kein Name um: Playwright-Prüfung, dass alle sichtbaren Zeilen dieselbe Höhe haben; Ergebnis im Diary.
- [ ] `memoPickerBody` und `amountPickerBody` enthalten kein `Input.textSimple` mehr und nutzen `Input.Small`; die Betragsfelder tragen einen Euro-Hinweis (Quelltext-Zusicherung).
- [ ] `datePickerBody` enthält kein `Button.primary` mehr, "Übernehmen" nutzt `PickerParts.applyButtonClass` (Quelltext-Zusicherung).
- [ ] `summaryState`: ohne Filter → `Hidden`; gefiltert und `Entries = Loading` → `Loading`; gefiltert und `Success` → `Ready`, auch bei 0 Treffern. Je ein Test. Im Zustand `Loading` rendert die Zeile weder "0 Buchungen" noch einen Betrag.
- [ ] `summaryCountText`: 0 → "0 Buchungen", 1 → "1 Buchung", 12116 → "12.116 Buchungen". Test.
- [ ] `summaryRow` enthält weder `Card.standard` noch `Money.transactionWithCurrency`; der Betrag kommt aus `Money.listAmount` (Quelltext-Zusicherung). Test: für Summe 0 und eine negative Summe enthält `Money.listAmountClass` weder `neon-red` noch `neon-green`.
- [ ] `PageHeader.containerClass Calm` enthält `mb-4` (Test in `RuhigeListenTests.fs`).
- [ ] `tableColumnWidths true` gibt Memo mindestens 16%, die Prozentwerte summieren sich auf unter 100% (Test). Bei 1440x900 in "Alle Konten" ist die Memo-Spalte mindestens 150px breit (Playwright misst das `th`, Wert im Diary).
- [ ] `historyLine` nutzt `ListRoles.primary` und kein `Tokens.FontSizes.body` (Quelltext-Zusicherung).
- [ ] Nachher-Bilder in 1440x900 und 390x844 unter `docs/ux/kassenbuch/filterleiste-ruhig/`: ledger-allaccounts, ledger-filtered, ledger-filter-Kategorie, -Zeitraum, -Payee, -Memo, -Betrag. Vorher sind die Audit-Bilder (nur verweisen).
- [ ] Keine neue arbiträre Schriftgröße außerhalb `Tokens.fs`, kein neues `style.fontSize`, keine Hex-Werte. `npm run guard:classes`, `npm run build`, `npm run test:scripts`, `dotnet build` und `dotnet test` grün.
- [ ] Filterleiste und Summenzeile wirken neben der ruhigen Tabelle gleich ruhig, ohne Leuchten und ohne Rot/Grün in der Summenzeile. [human-eye]

## Notes
- **Nicht in dieser Aufgabe:** deutsches Datumsformat im Zeitraum-Filter (`design-system-m7z9q`, baut hierauf auf), K6 (`ledger-d218j`), K11 (erledigt in `design-system-qz7b6`), K1 bis K4 und P1 bis P4 (erledigt in zahv9), Import (`ynab-sync-aj5tv`), Regeln (`categorization-wnxd6`). Die Semantik der Filter ändert sich nicht.
- **Risiken:** Prozentbreiten der `colgroup` wirken nur mit `table-layout: fixed`; bei 1100px Fensterbreite prüfen, dass Payee und Kategorie lesbar bleiben (sie kürzen per `truncate`). Seite und Styleguide-Demo dürfen bei der Popover-Breite nicht auseinanderlaufen. ADR-0005-Mechanik am Handy (BottomSheet) bleibt unberührt; Zeilen und Felder gelten dort trotzdem.
- `Money.groupThousands` zu öffnen ändert keine Ausgabe; die bestehenden `MoneyFormatTests` sichern das.
- `PageHeader` Calm `mb-4` berührt auch `categorization-wnxd6`. Wer zuerst integriert, ist egal; der zweite sieht den Abstand schon.
- Größe M, eine Worker-Iteration. Alles liegt in `Ledger/View.fs` plus drei kleine DS-Stellen; ein Schnitt würde nur Konflikte erzeugen.
