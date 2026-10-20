---
id: ledger-mhe2s
title: "Prüf-Schaltfläche der PC-Zeile erst bei Hover oder Fokus und neutral grau: der grüne Haken neben \"zu prüfen\" liest sich wie \"erledigt\""
status: doing
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, kassenbuch, pruefvermerk, statusspalte]
related_adrs: [0049, 0050]
related_research: []
prior_art: [ledger-r777c, ledger-qns4j, ledger-rjb8w]
---

## Why
In der Statusspalte der PC-Tabelle steht bei einer zu prüfenden Buchung das pinke Wort
"zu prüfen" und direkt daneben ein grüner Haken. Der Haken ist die Schaltfläche "als
geprüft markieren" (`reviewMarkButton`, ledger-r777c), wirkt neben dem Status aber wie
dessen Gegenteil: "zu prüfen, aber erledigt". Grün ist nach ADR 0049 ein Signal, kein
Aktionsschmuck. Roman: "so richtig Sinn macht es nicht, dass zu prüfen ein grünes
Checkmark hat" (2026-10-20).

## What
Entscheidung Roman 2026-10-20:
- Das Statuswort "zu prüfen" bleibt immer sichtbar.
- Die Schaltfläche (Haken für "Geprüft", Pfeil zurück für "Ungeprüft") ist am PC nur
  sichtbar, wenn die Zeile gehovert ist oder den Tastaturfokus trägt (ledger-rjb8w), oder
  die Schaltfläche selbst fokussiert ist. Sonst nimmt sie ihren Platz ein, ist aber
  unsichtbar (kein Springen der Spalte).
- Beide Symbole sind neutral (`text-secondary`, bei Hover `text-primary`), nicht Teal/Grün.
- Ein Tooltip (`title`) nennt die Aktion: "Als geprüft markieren" bzw. "Als ungeprüft
  markieren".
- Am Handy gibt es kein Hover: dort bleibt die Schaltfläche sichtbar, wird aber ebenfalls
  neutral. Der Wisch nach rechts bleibt die Hauptgeste.

## Acceptance criteria
- [ ] Am PC ist die Prüf-Schaltfläche einer Zeile ohne Hover und ohne Fokus unsichtbar
      (`opacity-0` o. ä.) und behält ihre Breite; mit Hover, Zeilenfokus oder Fokus auf der
      Schaltfläche ist sie sichtbar.
- [ ] Die Schaltfläche ist per Tastatur erreichbar und wird beim Fokussieren sichtbar
      (`focus-visible`); Taste G funktioniert unverändert.
- [ ] Haken und Rückgängig-Pfeil nutzen eine neutrale Farbe, nicht `NeonTeal`; der Test
      oder die Klassenprüfung zeigt das.
- [ ] Die Schaltfläche trägt `title` und den vorgelesenen Text mit der Aktion.
- [ ] Am Handy bleibt die Schaltfläche sichtbar (44px Trefferfläche), in neutraler Farbe.
- [ ] Neben "zu prüfen" liest sich die Zeile nicht mehr wie "erledigt". [human-eye]

## Notes
- Die Statusspalte ist 128px breit (`statusColumnWidth`); die Breite darf durch die
  Änderung nicht wachsen (ledger-qns4j hat sie mühsam eingehalten).
- Muster für Hover-Sichtbarkeit: Tailwind `group` auf der Zeile, `group-hover:` und
  `group-focus-within:` auf der Schaltfläche. Prüfen, ob `Table.RowProps` dafür eine
  Klasse an der Zeile erlaubt.
