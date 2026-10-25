---
id: ledger-v9rq3
title: "Liste im Prüf-Zustand nach den Tafeln: Summenzeile auch bei Zu prüfen und Ohne Kategorie, am PC mit Filter im Klartext, eigener Leerzustand nach dem letzten Prüfen, Zuletzt verwendet im Kategorie-Filter, Übernehmen erst mit Auswahl"
status: done
type: feature
context: ledger
created: 2026-10-25
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, kassenbuch, summenzeile, filter, leerzustand, tafeln]
related_adrs: [0048, 0049, 0051]
related_research: []
prior_art: [ledger-j8xq4, ledger-d4kx8, ledger-p3n8k]
---

## Why
Abgleich der Design-Tafeln gegen den Code (2026-10-25, Ablauf 1 und 2). Der Prüf-Durchgang
ist der tägliche Weg durchs Kassenbuch, und genau dort fehlt die Summenzeile: Tafel 1.1 bis
1.12 zeigen sie bei aktivem "Zu prüfen", der Code blendet sie aus, weil `hasAnyFilter`
(`Types.fs`) die Chips "Zu prüfen" und "Ohne Kategorie" nicht mitzählt (`summaryState` in
`View.fs` liefert dann `Hidden`). Daraus folgt ein zweiter Fehler: Ist die letzte Buchung
geprüft, zeigt die Liste "Für diesen Filter steht noch nichts im Kassenbuch · Jetzt
spiegeln", als fehlten Daten.

## What
1. **Summenzeile bei Chips.** Die Summenzeile erscheint auch, wenn nur "Zu prüfen" oder nur
   "Ohne Kategorie" aktiv ist (allein oder mit anderen Filtern). Treffer und Summe gelten
   für die gefilterte Menge wie bei den übrigen Filtern (d4kx8: gelöschte zählen mit).
   `hasAnyFilter` selbst nicht umdeuten, wenn andere Stellen ("Alle Filter löschen",
   Leerzustand ohne Treffer) an seiner heutigen Bedeutung hängen; dann eine eigene
   Prädikat-Funktion für die Summenzeile.
2. **Filter im Klartext am PC.** Die PC-Summenzeile nennt nach der Trefferzahl den Filter in
   Worten, wie auf den Tafeln: "· ING Extra · zu prüfen", "· alle Konten · Lebensmittel,
   Restaurants · letzte 12 Monate". Reihenfolge: Konto, Chips, Kategorien, Zeitraum, Payee,
   Memo, Betrag. Am Handy bleibt die Summenzeile wie heute.
3. **Leerzustand nach dem letzten Prüfen.** Ist "Zu prüfen" aktiv und die Liste leer, weil
   nichts mehr zu prüfen ist, steht dort "Alles geprüft" (mit Konto bzw. "in allen Konten")
   ohne "Jetzt spiegeln". Für "Ohne Kategorie" entsprechend "Alles kategorisiert". Der
   Leerzustand für ein Konto ohne jede Buchung bleibt unverändert.
4. **Zuletzt verwendet im Kategorie-Filter.** Die Kategorienauswahl des Filter-Chips zeigt
   über der Gruppenliste einen Abschnitt "Zuletzt verwendet" mit den zuletzt im Filter
   gewählten Kategorien (höchstens 5, je Gerät gemerkt, wie der Picker im Detail es schon
   kann, falls dort ein Muster existiert; sonst `localStorage`).
5. **Übernehmen erst mit Änderung.** "Übernehmen" in der Kategorie-Mehrfachauswahl des
   Filters ist gesperrt, solange die Auswahl gegenüber dem aktiven Filter unverändert ist.

## Acceptance criteria
- [ ] Reducer/View-Test: Mit nur "Zu prüfen" aktiv liefert die Summenzeilen-Logik einen sichtbaren Zustand mit Trefferzahl und Summe; dasselbe für nur "Ohne Kategorie".
- [ ] "Alle Filter löschen" und der Leerzustand "Kein Treffer. Filter lockern …" verhalten sich wie vorher (bestehende Tests grün, neuer Test für "nur Chip aktiv, keine Treffer").
- [ ] Reine Funktion für den Klartext-Filter mit Tests: Konto allein, Gesamtsicht, Chip, mehrere Kategorien, Zeitraum, Kombination in der festgelegten Reihenfolge.
- [ ] Playwright (PC-Breite): Mit "Zu prüfen" steht die Summenzeile mit "zu prüfen" im Klartext; nach dem Prüfen der letzten Buchung steht "Alles geprüft" und kein "Jetzt spiegeln".
- [ ] Playwright (Handy-Breite): Summenzeile bei "Zu prüfen" sichtbar.
- [ ] Die Kategorie-Filterauswahl zeigt nach einer Filterwahl "Zuletzt verwendet" mit dieser Kategorie, auch nach Neuladen der Seite.
- [ ] "Übernehmen" ist bei unveränderter Auswahl `disabled`, nach einer Änderung aktiv (Test).
- [ ] `guard:tokens` und `guard:classes` grün.
- [ ] Die Summenzeile im Prüf-Durchgang liest sich wie auf Tafel 1.1 bis 1.12. [human-eye]

## Notes
- Tafeln: `docs/ux/kassenbuch/tafeln/` (Ablauf 1 und 2). Optik gilt nach ADR 0049, die Tafeln nur für Ablauf und Inhalt.
- Bewusst nicht enthalten: "Gelöschte zeigen" bleibt ein Schalter (p3n8k). Prüfen ohne Warten auf den Server (Tafel 1.3 "optimistisch") ist offen, erst am Gerät prüfen, ob die Wartezeit stört.
- Gap-Bericht: Punkte 1, 2, 5, 6 aus "Offen, ohne Task" in Ablauf 1 und 2.

## Outcome

Liste im Prüf-Zustand nach den Tafeln (Ablauf 1 und 2).

- Summenzeile: neues Prädikat `hasSummaryFilter` (Types.fs) zählt die Chips mit; `summaryState` nutzt es. `hasAnyFilter`, "Alle Filter löschen" und ClearAllFilters sind unverändert.
- Klartext am PC: reine Funktion `filterPlainText` (Types.fs), Reihenfolge Konto, Chips, Kategorien, Zeitraum, Payee, Memo, Betrag. Die Summenzeile (`summaryRow`, View.fs) hängt sie am PC hinter die Trefferzahl; am Handy bleibt es bei der Trefferzahl. `summaryOrSelectionRow` bekam `accounts categories` als Parameter (Quelltext-Test in LedgerSelectionTests angepasst).
- Leerzustand: `emptyListKind` (Types.fs) unterscheidet AllReviewed, AllCategorized, NoMatch, NoEntries. Nur Zu prüfen: "Alles geprüft", nur Ohne Kategorie: "Alles kategorisiert", jeweils ohne Jetzt spiegeln. Chip plus weiterer Filter oder beide Chips: "Kein Treffer" (Knopf "Alle Filter löschen" nur bei `hasAnyFilter`). Kein Filter: wie vorher.
- Zuletzt verwendet: Modellfeld `RecentCategories`, `pushRecentCategories`, `parseRecentCategories`/`serializeRecentCategories`, Lesen in `init` und Schreiben per `Cmd.ofEffect` beim Übernehmen über localStorage (try/catch, daher unter .NET leer). Das Design-System `CategoryMultiSelect` hat die neuen Props `Recent` und `ApplyDisabled` (Abschnitt mit `data-testid=category-recent`, nur ohne Suchtext); `PickerParts.footerWhen` neu. Der Styleguide füllt beide Props neutral.
- Übernehmen erst mit Änderung: `categoryApplyDisabled`; `ApplyCategoryFilter` tut bei unverändertem Entwurf nichts.
- Tests: 16 neu (13 Expecto in `LedgerReviewStateTests.fs`, 3 Playwright in `scripts/ledger-review-state.test.mjs`: PC Zu prüfen mit Klartext und Alles geprüft ohne Jetzt spiegeln, Handy Summenzeile nur mit Trefferzahl, Übernehmen gesperrt und Zuletzt verwendet nach Neuladen). `scripts/ledger-filter-bar.test.mjs` angepasst: Summenzeile über `data-testid=ledger-summary-text` (PC hat nun Zusatztext), Kategorie-Klicks mit `.last()`, weil Zuletzt verwendet den Namen doppelt zeigt.
- Zahlen: `dotnet build` 0 Warnungen; `dotnet test` 2070 grün, 7 übersprungen; `npm run build`, `guard:classes`, `guard:tokens` grün; `npm run test:scripts` 219 von 219 grün.
- Offen: das [human-eye]-Kriterium (Lesbarkeit wie Tafel 1.1 bis 1.12) ist nicht per Screenshot belegt, nur der Text per Playwright. Ein Regressionstest wurde nicht durch Zurückdrehen rot geprüft. Beim Prüfen ist versehentlich ein `git stash` plus `git stash apply` gelaufen; der Arbeitsbaum ist unverändert, es bleibt aber ein Stash-Eintrag in der Worktree-Repo-Liste (bitte verwerfen).
