---
id: ledger-r1d2f
title: "Auswahl-Modus mit Summe der Auswahl (Handy und PC)"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-vgfw3, design-system-001]
blocks: []
tags: [frontend, kassenbuch, scheibe]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Dritte lesende Scheibe. Roman markiert heute in YNAB-Web einzelne Zeilen, um ihre Summe zu
sehen (Beispiel Steuererklärung: Kategorie wählen, die letzten zwölf Buchungen markieren,
Gesamtbetrag ablesen). Am Handy geht das gar nicht, er hätte es aber gern.

Der häufige Fall ist mit der Summenzeile aus `ledger-vgfw3` bereits abgedeckt. Diese
Scheibe ergänzt die Teilmenge.

## What
- Auswahl starten: langes Drücken auf eine Zeile (Handy), Kästchen oder Shift-Klick (PC).
- Auswahl-Leiste unten mit Anzahl, **Summe der Auswahl**, Aktion "Auswahl aufheben" und
  "Alle". Am Handy über der Bottom-Navigation, am PC über dem Fensterrand.
- **Das Kopfzeilen-Kästchen am PC wählt den gesamten Treffer**, nicht die geladene Seite;
  bei Teilauswahl zeigt es einen Teal-Strich statt des Hakens. Fachlich heißt das: die
  Auswahl ist eine Menge über den Treffer, keine Liste geladener Ids. Wie sich das mit
  Nachladen und Shift-Klick-Spannen verträgt, gehört in die Verfeinerung.
- Shift-Klick markiert den Anker der Spanne (`box-shadow:inset 2px 0 0 #00d4aa`).
- Der Ausstieg heißt überall **"Auswahl aufheben"**, nie "Fertig".
- Am Handy ersetzt die Leiste die Tab-Bar. Mit "Alle" sind es vier Aktionen auf drei Reihen
  à 44px (165px hoch, 411px bleiben für die Liste); kommen die zwei Flag-Arten aus Ablauf 5
  dazu, wächst sie auf 197px.
- Solange eine Auswahl aktiv ist, zeigt die Summenzeile die Auswahl statt des Treffers.
- Auswahl übersteht Konto- und Filterwechsel nicht zwingend; Verhalten bei der
  Verfeinerung festlegen.
- Schreibende Aktionen in der Leiste (geprüft, Kategorie, Lena fragen) gehören **nicht**
  hierher, sie hängen an der v2-Entscheidung.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Brief: `docs/ux/kassenbuch/briefs.md`,
Ablauf 3.

Am 2026-09-18 aus den fertigen Screens nachgetragen (Kopfzeilen-Kästchen über den ganzen
Treffer, Anker der Shift-Spanne, Beschriftung des Ausstiegs, Höhen der Handy-Leiste). Die
Aktion "Kopieren" aus dem ursprünglichen Brief ist gestrichen (Entscheidung Roman
2026-09-14, war eine Erfindung im Brief). Ein Übergabepaket für Ablauf 3 gibt es noch
nicht, die Screens sind bei Roman zur Abnahme.
