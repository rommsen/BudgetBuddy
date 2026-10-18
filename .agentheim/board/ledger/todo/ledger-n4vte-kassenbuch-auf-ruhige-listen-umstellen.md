---
id: ledger-n4vte
title: Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)
status: todo
type: feature
context: ledger
created: 2026-10-18
completed:
depends_on: [design-system-q7rkm, design-system-001]
blocks: []
tags: [frontend, kassenbuch, styleguide, visuell, ruhige-listen]
related_adrs: [0049, 0032, 0048]
related_research: []
prior_art: [ledger-x0yga, ledger-w9skm, ledger-qns4j, ledger-h6ptn]
---

## Why
Das Kassenbuch wirkt neben "Import prüfen" laut und schwer und nutzt den Platz nicht aus:
Gradient-Titel in 4xl, Liste auf `max-w-7xl` begrenzt, alle Zellen 16px grau, Kategorie
violett, Beträge rot und grün. Roman hat am 2026-10-18 im Sichttest Fassung B (ruhige
Tabelle) gewählt, festgehalten in ADR 0049 und im Styleguide §2 "Ruhige Listen".

## What
Zielbild (im Repo):
- PC: `docs/ux/kassenbuch/mockups/ruhige-listen-b-pc-1440.png`
- Handy: `docs/ux/kassenbuch/mockups/ruhige-listen-b-handy-390x844.png`

Struktur und Verhalten aus Handoff Ablauf 1 (`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`)
gelten weiter. Dessen Farb- und Schriftwerte für Listen sind durch ADR 0049 überholt.

**Beide Viewports** (`src/Client/Components/Ledger/View.fs`, `view`):
- `PageHeader.gradientWithActions` durch die schlanke Variante aus `design-system-q7rkm`
  ersetzen. "Jetzt spiegeln" bleibt erreichbar, aber nicht mehr als großer oranger Knopf
  über der Liste; am Handy als Symbol-Knopf in der Kopfzeile, der Untertitel nennt den
  letzten Spiegel kurz ("Gespiegelt heute 22:24").
- Container ohne Breitenbegrenzung.
- Kontostände ruhig (neutral, negativ Red) über die angepasste `Money.accountListRow`.

**PC** (`entryListDesktop`, `entryTableRow`):
- Zellrollen aus `design-system-q7rkm` statt `Table.td`/`tdTruncated`; Kategorie über die
  sekundäre Rolle statt `categoryColorClassName` (violett).
- Betrag rechtsbündig über die Betragsrolle.
- "Zu prüfen" als Statuskante statt `border-l-2 border-l-neon-pink`; in der Statusspalte
  ein kleines "zu prüfen" statt Badge.
- Datumsgruppen mit Tagessumme auch am PC (Gruppenkopf wie am Handy).
- Spaltenanteile so, dass Payee und Kategorie nicht mehr bei Inhalten wie "Transfer :
  Tagesgeld ING" oder "Alltag: Essen/Einkaufen" abgeschnitten werden, während die
  Memo-Spalte leer ist (Payee ca. 30 %, Kategorie ca. 26 %, Memo Rest).

**Handy** (`entryRow`, `dateGroupHeader`):
- Zweizeilig: Payee und Betrag in Zeile 1 (Primär-Rolle, 15px), Kategorie und Memo in
  Zeile 2 (12px, sekundär bzw. gedämpft).
- Badge-Zeile entfernen: `originBadge` raus (Herkunft steht in der Historie, wie am PC seit
  `ledger-qns4j`). `accountBadge` (Gesamtsicht), `flagBadge` und `deletedBadge` wandern in
  Zeile 2, damit keine dritte Zeile entsteht.
- "Zu prüfen" als Statuskante statt `Badge.dot`.
- Gruppenkopf mit Tagessumme rechts.

Bestehende Tests, die den alten Zustand festschreiben, werden angepasst, nicht gelöscht
ohne Ersatz: `categoryColorTests` (violett) und die Quelltext-Zusicherung "Die Handy-Zeile
zeigt die Herkunft weiterhin inline" in `src/Tests/LedgerViewTests.fs`.

## Acceptance criteria
- [ ] Die Kassenbuch-Seite verwendet weder `PageHeader.gradientWithActions` noch
      `Container.view` mit Breitenbegrenzung (Quelltext-Zusicherung).
- [ ] PC-Tabelle und Handy-Zeile färben die Kategorie über die sekundäre Rolle; der
      bisherige Test auf `neonPurple` ist durch einen Test auf die neue Rolle ersetzt.
      "Keine Kategorie" bleibt Orange.
- [ ] Ein Übertrag erscheint nicht als "Keine Kategorie" (ADR 0048), in beiden Viewports.
- [ ] Beträge in Liste und Tabelle laufen über die Money-Listenvariante; ein positiver
      Betrag ist Teal, ein negativer `text-primary`. Ein in YNAB gelöschter Betrag bleibt
      bei `Money.transactionDeleted`.
- [ ] Der Betrag steht am PC rechtsbündig in seiner Spalte, bündig mit dem Spaltenkopf.
- [ ] "Zu prüfen" erscheint in beiden Viewports als Statuskante links; der `Badge.dot` in
      `entryRow` und `border-l-2 border-l-neon-pink` in `entryTableRow` sind entfernt.
- [ ] Die Handy-Zeile rendert `originBadge` nicht mehr; Konto-Kürzel, Flag und
      Gelöscht-Kennzeichen stehen in Zeile 2. Die Quelltext-Zusicherung zur Herkunft ist
      entsprechend umgedreht.
- [ ] Datumsgruppen zeigen in beiden Viewports eine Tagessumme rechts; die Summe einer
      Gruppe ist die Summe der sichtbaren Zeilen dieser Gruppe (reine Funktion, getestet,
      inklusive eingeblendeter gelöschter Buchungen nach `ledger-d4kx8`).
- [ ] Ein negativer Kontostand erscheint Red, ein positiver neutral, in Kontoliste (PC)
      und Kontokarten (Handy).
- [ ] Tastatursteuerung, Mehrfachauswahl, Wisch-Geste, Filter-Chips, Summenzeile und
      Detail-Sheet funktionieren wie vorher (bestehende Tests grün).
- [ ] Screenshots `#/ledger` in 1440 × 900 und 390 × 844 liegen unter
      `docs/ux/kassenbuch/screenshots/` (`npm run screenshot:route -- '#/ledger' --out
      docs/ux/kassenbuch/screenshots/`).
- [ ] Die Screenshots neben den beiden Zielbildern zeigen dieselbe Hierarchie und
      Farbverteilung: Payee hell, Rest gedämpft, Farbe nur bei Eingang, Statuskante,
      "Keine Kategorie" und negativem Kontostand; kein Gradient-Titel; die Liste füllt die
      Breite. [human-eye]
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
- Quellen: ADR 0049, Styleguide §2 "Ruhige Listen", Artefakt "Kassenbuch Sichttest"
  (https://claude.ai/artifact/2eGrKDs4GxUGuGJdfL1XaZ), Fassung B.
- Romans Entscheidungen vom 2026-10-18: Fassung B; negative Kontostände Red; am Handy
  Payee und Betrag 15px, am PC 14px.
- Im Zielbild steht der Übertrag "Transfer : Tagesgeld ING" fälschlich als "Keine
  Kategorie" (Fehler in den Beispieldaten des Sichttests). Maßgeblich ist ADR 0048.
- Die Datumsspalte am PC zeigt im Zielbild zusätzlich zum Gruppenkopf das Datum je Zeile.
  Das darf bleiben (hilft beim Scannen mit Tastatur), ist aber kein Muss.
- Bausteine aus `design-system-q7rkm` (Stand 2026-10-18): `Tokens.ListRoles`,
  `Money.listAmount`/`accountBalanceClass`, `Table.roleCell`/`amountCell`/`calmHeaderCell`/
  `calmRow` mit `Table.CellRole` und `RowProps.StatusEdge`, `Primitives.Container.full`,
  `PageHeader.calm`. Vorbild für die Verwendung: Abschnitt "8a. Ruhige Listen" in
  `src/Client/Components/Styleguide/View.fs`.
- Zwei Abweichungen im Styleguide-Beispiel, die das Kassenbuch nicht übernehmen soll: die
  Tagessumme im Gruppenkopf steht dort unter der Status-Spalte statt bündig unter den
  Beträgen (Zielbild: unter der Betragsspalte), und in der zweizeiligen Handy-Zeile ist die
  erste Zeile niedriger als die übrigen (Abstände zwischen Zeile 1 und 2 uneinheitlich).
- `ledger-b7qmx` (Split-Zeilen voll qualifizieren) liegt im Backlog und berührt dieselbe
  Kategorie-Zelle. Keine Abhängigkeit, aber bei gleichzeitiger Arbeit Merge-Konflikt in
  `tdTruncatedCategory` möglich.
