---
id: ledger-k2rbn
title: "Kategorie ändern in BB (v2): ein Kategorie-Picker mit zwei Einstiegen, dem Pfeil an der Kategorie im Detail für eine Buchung und \"Kategorie setzen\" in der Auswahl-Leiste für viele; BB schreibt die Änderung nach YNAB nach dem Muster des Prüfvermerks"
status: backlog
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001, ledger-q1hw2, ledger-w4vte]
blocks: []
tags: [frontend, backend, kassenbuch, kategorie, v2, bedienkonzept]
related_adrs: [0028, 0050, 0051, 0053]
related_research: []
prior_art: [ledger-r777c, ledger-nhwrt]
---

## Why
Vision v2 ("Ownership"): Roman öffnet YNAB nicht mehr zum Nachbearbeiten; Kategorie ändern
ist der häufigste Fall. ADR 0053 legt schon jetzt fest, wo es in der Oberfläche lebt, damit
`ledger-q1hw2` und `ledger-w4vte` den Platz freihalten: im Detail für eine Buchung, in der
Auswahl-Leiste für viele, mit demselben Picker. Erfasst am 2026-10-20 auf Romans Wunsch
("V2 ist nicht in unendlicher Ferne, zumindest mitdenken").

## What
Grobe Form, beim Verfeinern zu schärfen und vermutlich zu schneiden:

1. **Ein Picker** (Design-System): Suche, Kategorien nach Gruppe, zuletzt benutzte oben;
   am PC als Popover am Auslöser, am Handy als BottomSheet. Bezug `ledger-kwwxz`
   (Available-Betrag je Kategorie im Picker), dort bisher "Kategorienauswahl" genannt.
2. **Einstieg eine Buchung:** Pfeil rechts an der Kategorie-Zeile im Detail
   (`ledger-q1hw2` hält den Platz frei); ein Klick auf Zeile oder Pfeil öffnet den Picker.
   Der Filter-Knopf daneben bleibt ein eigenes Ziel.
3. **Einstieg viele Buchungen:** "Kategorie setzen" in der Auswahl-Leiste
   (`ledger-nhwrt`, `ledger-w4vte`); setzt die gewählte Kategorie für alle ausgewählten.
4. **Schreibweg** nach dem Muster von ADR 0050: ein Ereignis mit alt und neu, ein PATCH an
   YNAB (Sammel-PATCH für viele), ein Zustand für den gescheiterten Push mit
   "Erneut senden", Historie-Eintrag "In BB geändert".
5. **Offene Fragen:**
   - Split-Buchungen: gesperrt, oder öffnet der Pfeil die Split-Bearbeitung (Ablauf 7)?
   - Überträge: Kategorie nicht änderbar?
   - Was gilt, wenn YNAB parallel eine andere Kategorie meldet (Spiegel gegen BB, Pink-Leiste
     "Abweichung" ist v2)?
   - Sortierung (ADR 0051 Punkt 7): ändert sich der Schlüssel der aktiven Sortierung oder
     fällt die Buchung aus dem Filter, stimmt `LocallyRemoved` nicht mehr; Vorschlag: die
     geladenen Seiten neu laden.
   - Regeln: Soll eine Änderung anbieten, eine Kategorisierungsregel anzulegen?

## Acceptance criteria
- To be defined during refinement.

## Notes
Bleibt in `backlog/`, bis v2 beginnt oder Roman es vorzieht. ADR 0028 hält den
Prüfvermerk als einzige schreibende Ausnahme in v1; diese Aufgabe ist die erste
Nachbearbeitung aus v2 und braucht beim Verfeinern einen Blick auf die Vision.

ADR 0053 hat Ablauf 1 ("Klick auf die Kategorie-Zelle öffnet den Picker direkt")
aufgegeben; der Picker öffnet nur aus Detail und Leiste.
