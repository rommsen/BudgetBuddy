---
id: budgeting-r4tkq
title: "Spike: liefert die YNAB-API die Targets lesbar (Art, Betrag, Rhythmus) und erlaubt sie, den zugewiesenen Betrag pro Kategorie und Monat zu setzen? Voraussetzung für Targets anwenden."
status: doing
type: spike
context: budgeting
created: 2026-10-25
completed:
depends_on: []
blocks: []
tags: [ynab-api, targets, budget]
related_adrs: [0063, 0058]
related_research: []
prior_art: []
---

## Why
Stufe 1 des Budgets beginnt mit **Targets anwenden**: ein Knopf am Monatsersten bringt
alle Kategorien auf das, was ihre Targets verlangen (YNABs "Underfunded" ohne Auswählen).
Die Targets bleiben dabei zunächst YNAB-geführt (Vision, Revision 2026-10-25; ADR 0063).
Das geht nur, wenn BB die Targets aus YNAB lesen und den zugewiesenen Betrag nach YNAB
schreiben kann. Beides ist nicht verifiziert (offene Frage in der Vision und im
`budgeting`-README).

## What
Gegen die echte YNAB-API klären und als Research-Bericht in
`.agentheim/knowledge/research/` festhalten:

1. **Targets lesen:** Welche Felder liefert eine Kategorie zum Target (Art, Betrag,
   Rhythmus, Stichtag, "bis Datum", wöchentlich)? Lassen sich Romans vier Arten daraus
   eindeutig erkennen: *pro Woche ein Betrag*, *jeden Monat ein fester Betrag*,
   *auffüllen bis*, *bis zu einem Datum ansparen*? Gibt es ein Target mit Deckel?
2. **Was YNAB schon ausrechnet:** Liefert die API den für den Monat noch fehlenden Betrag
   pro Kategorie direkt (das, was "Underfunded" zuweisen würde), oder muss BB ihn aus den
   Target-Feldern selbst berechnen?
3. **Zuweisen schreiben:** Lässt sich der zugewiesene Betrag einer Kategorie für einen
   bestimmten Monat setzen? Absolut oder als Delta? Ein Aufruf pro Kategorie oder gesammelt?
   Was passiert mit "Zu verteilen" (`to_be_budgeted`) danach?
4. **Verfügbar und Vortrag:** Liefert die API pro Kategorie und Monat Zugewiesen,
   Aktivität und Verfügbar? Das entscheidet mit, ob BB selbst rechnet oder übernimmt
   (offene Frage der Vision).
5. **Targets schreiben** (nur feststellen, nicht bauen): Kann die API Targets anlegen oder
   ändern? Relevant für Stufe 3 (Targets in BB pflegen).

## Acceptance criteria
- [ ] Research-Bericht in `.agentheim/knowledge/research/` beantwortet die Fragen 1 bis 5
      je mit ja/nein/teilweise und den konkreten Endpunkten und Feldnamen.
- [ ] Für Frage 1 steht eine Zuordnung von Romans vier Target-Arten zu den API-Feldern,
      oder klar benannt, welche Art sich nicht erkennen lässt.
- [ ] Frage 3 ist durch einen echten Probelauf belegt (Request, Response, Wirkung auf
      `to_be_budgeted`), nicht nur aus der Doku abgeleitet.
- [ ] Der Probelauf hinterlässt Romans Budget im Ausgangszustand (zugewiesener Betrag der
      Test-Kategorie zurückgesetzt), im Bericht dokumentiert.
- [ ] Der Bericht endet mit einer Empfehlung für den Zuschnitt von "Targets anwenden":
      Fehlbetrag von YNAB übernehmen oder in BB berechnen.

## Notes
- **Stop-loss:** if, mid-spike, the mitigation is already known and cheap, record it and
  stop. Konkret: sobald feststeht, dass YNAB den Fehlbetrag pro Kategorie liefert und
  Zuweisen per API geht, reichen die Fragen 1 bis 3; 4 und 5 dann nur noch aus der Doku.
- **Vorhandener Code:** `src/Server/YnabClient.fs` liest bereits `to_be_budgeted` über
  `GET /budgets/{id}/months/current` (`monthToBeBudgetedDecoder`, Ready to Assign im
  Kategorie-Picker). Der Monats-Endpunkt ist also schon angebunden.
- **Probelauf-Muster:** wie ADR 0058 (Split-Schreibweg, Probelauf 2026-10-24), mit
  `YNAB_TOKEN` aus `.env`. Für den Schreibtest eine unkritische Kategorie wählen, Betrag
  vorher notieren, danach exakt zurücksetzen. Kein Test darf gegen das echte Budget laufen
  (CLAUDE.md); der Probelauf ist ein einmaliges Skript im Scratchpad, kein Testcode.
- Ergebnis fließt in die erste Feature-Task "Targets anwenden" in `budgeting`, die nach
  diesem Spike geschnitten wird.
