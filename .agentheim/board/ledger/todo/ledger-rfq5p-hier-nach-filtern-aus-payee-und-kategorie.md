---
id: ledger-rfq5p
title: "\"Hier nach filtern\": Klick auf Payee oder Kategorie einer Zeile öffnet ein kleines Menü, das den passenden Such-Filter in einem Schritt setzt"
status: todo
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, kassenbuch, filter, payee, kategorie]
related_adrs: [0036, 0038, 0043, 0049]
related_research: []
prior_art: [ledger-j8xq4, ledger-saqsk]
---

## Why
Um alle Buchungen eines Payees oder einer Kategorie zu sehen, muss Roman heute den Chip
öffnen, suchen und auswählen, obwohl der Wert direkt vor ihm in der Liste steht. Roman
wünscht sich "eine Abkürzung zum Filtern" per Klick auf Payee und Kategorie
(2026-10-20).

## What
Entscheidung Roman 2026-10-20: ein kleines Menü beim Klick.

- **PC:** Klick auf die Payee-Zelle öffnet ein Mini-Menü direkt an der Zelle mit
  "Nach <Payee> filtern". Klick auf die Kategorie-Zelle entsprechend "Nach <Kategorie>
  filtern". Der Klick öffnet dann nicht die Historie (`stopPropagation`); ein Klick
  irgendwo sonst auf der Zeile öffnet sie wie bisher.
- **Handy:** Das Sheet, das ein Tipp auf die Zeile öffnet, bekommt dieselben zwei
  Aktionen.
- Die Aktion setzt den bestehenden Such-Filter (keinen neuen Filterweg): Payee über
  `SelectPayeeFilter` (exakt, gefaltet, ADR 0037), Kategorie als Kategorienauswahl mit
  genau dieser einen Kategorie (`ApplyCategoryFilter`-Weg). Andere aktive Filter bleiben
  stehen (Und-Verknüpfung); eine schon gesetzte Kategorienauswahl wird ersetzt, nicht
  ergänzt.
- Kein Eintrag, wo es nichts zu filtern gibt: Buchung ohne Payee, ohne Kategorie, Split
  (Kategorie-Zelle zeigt den Split, keine einzelne Kategorie) und Übertrag ohne Kategorie.
- Esc oder Klick daneben schließt das Menü ohne zu filtern, wie `FilterPopover`.

## Acceptance criteria
- [ ] Klick auf die Payee-Zelle einer PC-Zeile öffnet das Menü, nicht die Historie; der
      Eintrag "Nach <Payee> filtern" setzt den Payee-Filter, der Chip zeigt den Payee,
      Trefferzahl und Summe stimmen mit der Serverabfrage überein.
- [ ] Dasselbe für die Kategorie-Zelle: der Kategorie-Filter enthält danach genau diese
      Kategorie; eine vorher gewählte Mehrfachauswahl ist ersetzt.
- [ ] Bestehende andere Filter (Zeitraum, Betrag, Memo, Konto, Chip "Zu prüfen") bleiben
      nach der Aktion aktiv.
- [ ] Split-Buchungen, Buchungen ohne Payee und ohne Kategorie bieten den jeweiligen
      Eintrag nicht an.
- [ ] Esc und Klick daneben schließen das Menü ohne Filteränderung.
- [ ] Am Handy bietet das Zeilen-Sheet beide Aktionen mit derselben Wirkung.
- [ ] Update-Tests (Elmish `update`) decken die Nachrichten ab: Filterzustand nach der
      Aktion, Ersetzen der Kategorienauswahl, Menü schließen ohne Änderung.
- [ ] Payee- und Kategorie-Zelle verraten beim Hover, dass sie klickbar sind, ohne die
      ruhige Liste zu stören (ADR 0049). [human-eye]

## Notes
- Heute öffnet ein Klick auf die Zeile die Historie (`OpenHistory`). Die README nennt als
  Ziel "Kategorie über den Picker (auch direkt aus der Tabellenzelle)"; das ist noch nicht
  gebaut (Bearbeiten sitzt in `ledger-q1hw2`). Kommt das Bearbeiten, wird "Kategorie
  ändern" ein zweiter Eintrag im selben Menü. Das Menü deshalb als Liste von Einträgen
  bauen, nicht als Einzelschaltfläche.
- Prüfen, ob `Client.DesignSystem.FilterPopover` als Hülle passt oder ein schlankes
  Menü-Element im Design-System fehlt; ein neues DS-Element gehört in die
  `/styleguide`-Route.
- Ein ausgeblendeter Split mit einer einzigen Kategorie (ADR 0038) zählt als Split, also
  kein Eintrag.
