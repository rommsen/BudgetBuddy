---
id: design-system-ytgnc
title: "Rückmeldung am Handy ohne Toast (Regel R3): ein fester Meldungsplatz je Seite, Leiste über der Navigation mit „+ Buchung“, Zeile im Sheet, Rückgängig am Feld; Styleguide §7.13 und ADR"
status: backlog
type: feature
context: design-system
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-pz5wv, ledger-gwr9j, budgeting-gtrgk, categorization-t6j00, banking-import-vwhfm]
tags: [design-exploration, toast, frontend]
related_adrs: [0087]
related_research: []
prior_art: [ledger-vf5h6]
---

## Why
Roman am 2026-11-19 (Design-Exploration ledger-vf5h6): Toasts stehen im Weg und sind hässlich. Am Handy liegen heute
zwei Toasts gleichzeitig auf dem Bildschirm (einfache oben über `Toast.container`, solche mit „Rückgängig“ unten über
`actionContainer`), mehrere Wische stapeln drei Toasts über Liste und „+ Buchung“, im Durchgehen liegt der Toast auf
„Passt, weiter“. Entscheidung: **am Handy kein Toast** (R3). Löst design-system-vf94m (Toast verdeckt „+ Buchung“) ab.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am 2026-11-19 freigegeben). Worker können sie
nicht öffnen: beim Verfeinern liest der Conductor sie (`Artifact` read), legt Datei und Screenshots je Ansicht in den
Scratchpad und nennt die Pfade in der Task (CLAUDE.md „Skizzen“).

Regel R3 (Skizze Abschnitte 1 bis 3), Reihenfolge der Plätze:
1. **Ergebnis am Ort sichtbar:** die Stelle bestätigt selbst (Feld leuchtet kurz, darunter „Rückgängig“).
2. **Sheet offen:** Zeile im Sheet über dem Hauptknopf, nie darüber.
3. **Sonst:** Meldungsleiste über der Navigation. Auf Seiten mit „+ Buchung“ trägt sie den Knopf (der schwebende FAB
   entfällt, die Liste endet über der Leiste); kommt eine Meldung, wird der Knopf zum runden „+“ und die Meldung steht
   links zweizeilig: oben die Aktion (nie gekürzt), darunter Name oder Grund (gekürzt mit „…“), rechts „Rückgängig“
   bzw. „Erneut“. Seiten ohne „+ Buchung“ zeigen die Leiste nur, solange es eine Meldung gibt.
- Immer nur eine Meldung. Gleiche Aktionen hintereinander werden eine („3 geprüft“, Rückgängig nimmt alle zurück),
  eine andere Aktion ersetzt sie. Sie bleibt bis zur nächsten Aktion oder bis man die Seite verlässt: kein ✕, kein Zeitlimit.
- Fehler stehen rot am selben Platz und bleiben bis zur nächsten Aktion. Ein Zustand der Seite (etwa „nicht neu
  gelesen“) ist keine Meldung, sondern ein Hinweis auf der Seite (Muster wie ledger-pfhdq).
- Gelöschtes in einer Liste hinterlässt eine Zeile am Platz („„Regel drei“ gelöscht · Rückgängig“), bis zur nächsten Aktion.
- Am PC bleibt der Toast unten rechts (§7.13), höchstens einer zugleich.

Baustein im DS (Name beim Verfeinern), Eintrag in §5 und auf `/styleguide`; §7.13 neu geschrieben; ADR zur Regel.

## Acceptance criteria
- [ ] ADR „Rückmeldung am Handy ohne Toast“ mit der Regel oben; §7.13 und §5 (Toast, neuer Baustein) nachgezogen.
- [ ] DS-Baustein Meldungsleiste mit Knopf-Variante (rund bei Meldung), Zustände ok, Warnung, Fehler, läuft; Zusammenfassen gleicher Aktionen als reine Funktion mit Test.
- [ ] Sweep-Invariante: am Handy überdeckt keine Rückmeldung ein Bedienelement oder Inhalt, mit Gegenbeweis (Regressionstest aus design-system-vf94m).
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome), Abweichungen nur die in dieser Task genannten.
- [ ] Die Seiten selbst bauen die Folge-Tasks um; diese Task liefert Regel, Baustein und Styleguide-Route.

## Notes
Captured 2026-11-19 aus ledger-vf5h6. Löst design-system-vf94m ab. Toast-Liste Kassenbuch und Durchgehen: Anhang der Skizze.
