---
id: 0096
title: Am PC steht das Panel immer und zeigt die Auswahl, die Zeile hat keine Prüf-Schaltfläche
scope: ledger
status: accepted
date: 2026-11-14
supersedes: []
superseded_by: []
related_tasks: [ledger-cnaz3]
related_adrs: [0053, 0087]
related_research: []
---

# ADR 0096: Am PC steht das Panel immer und zeigt die Auswahl

## Context

ADR 0053 hat Panel und Auswahl getrennt: eine beginnende Auswahl schloss das Panel (Punkt 2), und am PC
trug jede Zeile bei Hover und Fokus eine Prüf-Schaltfläche (Punkt 4). Das Panel mit der Auswahl („Finder-Modell“)
war dort ausdrücklich verworfen. ADR 0087 Punkt 17 und der von Roman freigegebene Prototyp Version 17
(`selSummary`, `panelDesk`) sehen es anders: ab zwei gewählten Buchungen wird das Panel zur Zusammenfassung,
und das Panel steht am PC immer. Roman hat am 2026-11-14 entschieden, dass die Zeile am PC keinen Knopf
„Geprüft“ oder „Ungeprüft“ mehr trägt, auch nicht bei Hover oder Fokus (ledger-cnaz3).

## Decision

1. Ab 1024 px steht rechts immer das Panel. Ohne gewählte Buchung zeigt es „Buchung wählen, um Details zu sehen.“
   ohne Aktion. Reicht die Breite daneben für keine Tabellenstufe, steht links die Kompaktliste; das Detail
   ist am PC nie mehr ein Sheet.
2. Am PC folgt das Panel der Auswahl: bei genau einer gewählten Buchung zeigt es ihr Detail, ab zwei die
   Zusammenfassung (Summe, Ausgaben, Einnahmen, Davon zu prüfen, nach Kategorie, Sammelaktionen). Das gilt nur,
   wenn sich die Auswahl ändert; Esc schließt das Detail der einen gewählten Buchung, die Auswahl bleibt.
   Ab zwei stehen die Sammelaktionen im Panel, die Leiste unten zeigt nur Anzahl, Summe und „Auswahl aufheben“.
3. Unter 1024 px (Sheet) bleibt ADR 0053 Punkt 2: eine beginnende Auswahl schließt das Detail.
4. Keine Zeile hat eine Prüf-Schaltfläche mehr (ADR 0053 Punkt 4 ohne die Schaltfläche). Geprüft wird mit G,
   per Wisch am Handy, mit dem Schalter im Detail und über die Auswahl.
5. Strg- oder Cmd-Klick auf eine Zeile nimmt sie in die Auswahl oder heraus, Umschalt-Klick wählt den Bereich ab
   dem Anker, Umschalt+↑↓ erweitert um die nächste Zeile (§7.7). Ohne Auswahl zählt eine im Panel offene Buchung
   als erste gewählte (Prototyp `selectWithMods`), aber nur, wenn sie geladen und nicht ausgegraut in der Liste
   steht. Gewählt ist nur, was ein Häkchen tragen kann: Klick, Bereich und Taste lassen ausgegraute und nicht
   geladene Zeilen aus (auch die Leertaste auf einer nur über die Route geöffneten Fokuszeile und langes Drücken
   am Handy), und eine gewählte Buchung, die durch eine Bearbeitung im Detail aus dem Filter fällt,
   verlässt die Auswahl. So trifft keine Sammelaktion eine Buchung, die in der Liste nicht als gewählt zu sehen ist.
   Ein Klick ohne Taste öffnet das Detail wie bisher.

## Alternatives considered

- **Panel nur bei offener Buchung, Auswahl nur in der Leiste (ADR 0053 wie bisher):** verworfen, Prototyp und
  ADR 0087 Punkt 17 zeigen die Zusammenfassung im Panel, und die Leiste hatte keinen Platz für Ausgaben,
  Einnahmen und Kategorien.
- **Bei genau einer gewählten Buchung das leere Panel (wie der Prototyp, wenn vorher nichts offen war):**
  verworfen, die Task verlangt das Detail dieser Buchung; ein leeres Panel neben einer gewählten Zeile sagt nichts.
- **Eine ausgegraute oder nur über die Route geöffnete Buchung als erste gewählte mitnehmen (Prototyp wörtlich):**
  verworfen, sie hätte kein Häkchen, Anzahl und Summe der Leiste stimmten nicht, und eine Sammelaktion träfe sie
  trotzdem (Verifier-FAIL Iteration 1 und 2).

## Consequences

- ADR 0053 Punkt 2 gilt am PC nicht mehr, Punkt 4 ohne die Schaltfläche an der Zeile. `pointerRowReviewMarkSlot`,
  `touchRowReviewMarkSlot` und `reviewMarkButton` sind entfernt.
- Meint die Auswahl mehr Buchungen als geladen sind („alle“ über das Kopfzeilen-Kästchen), zählen Ausgaben,
  Einnahmen und Kategorien nur die geladenen; ein Satz in der Zusammenfassung sagt das. Summe und Anzahl
  stimmen weiter (`selectionTotals`).
