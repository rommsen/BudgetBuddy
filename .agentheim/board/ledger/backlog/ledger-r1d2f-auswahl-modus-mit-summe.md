---
id: ledger-r1d2f
title: "Auswahl-Modus mit Summe der Auswahl (Handy und PC)"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-vgfw3, design-system-001]
blocks: [ledger-nhwrt]
tags: [frontend, kassenbuch, scheibe]
related_adrs: []
related_research: []
prior_art: [ledger-vgfw3]
---

## Why
Dritte lesende Scheibe. Roman markiert heute in YNAB-Web einzelne Zeilen, um ihre Summe zu
sehen (Beispiel Steuererklärung: Kategorie wählen, die letzten zwölf Buchungen markieren,
Gesamtbetrag ablesen). Am Handy geht das gar nicht, er hätte es aber gern.

Der häufige Fall ist mit der Summenzeile aus `ledger-vgfw3` bereits abgedeckt. Diese
Scheibe ergänzt die Teilmenge.

## What
Maße, Farben und Texte: `docs/ux/kassenbuch/handoff-ablauf-3-mehrfachauswahl-mit-summe.md`
(Screens 3.1 bis 3.7, verbindlich). Dieser Abschnitt legt das Verhalten fest.

### Auswahl als Menge über den Treffer
- Zustand im Client (`src/Client/Components/Ledger/State.fs`):
  `Selection = NoSelection | Picked of Set<Guid> | AllExcept of Set<Guid>` plus
  `SelectionAnchor: Guid option` und am Handy `SelectionMode: bool`.
- `Picked` enthält nur geladene Zeilen (man kann nur antippen, was man sieht).
  `AllExcept` entsteht durch "Alle" bzw. das Kopfzeilen-Kästchen; abwählen kann man wieder
  nur geladene Zeilen, also sind auch die Ausnahmen immer geladen.
- **Anzahl und Summe der Auswahl rechnet der Client**, ohne neuen Server-Aufruf:
  - `Picked ids`: Anzahl und Summe der geladenen Zeilen in `ids`.
  - `AllExcept ex`: `TotalCount - |ex|` und `TotalAmount - Summe(ex)`.
  - Bei Summen gilt dieselbe Regel wie für `TotalAmount`: in YNAB gelöschte Buchungen
    zählen nicht zur Summe (siehe `applyReviewMarkResult`).
  - Eine reine Funktion (`selectionTotals`) liefert beides; Summenzeile und Leiste lesen
    nur sie.
- Nachladen unter `AllExcept`: neu geladene Zeilen erscheinen gewählt.
- Verlässt eine Zeile die Liste (Prüfvermerk bei aktivem Chip, `ledger-r777c`), fällt sie
  aus `Picked` bzw. aus den Ausnahmen; Anzahl und Summe bleiben stimmig, weil `TotalCount`
  und `TotalAmount` schon nachgeführt werden.
- **Konto-, Filter- oder Chipwechsel leert die Auswahl** und beendet den Modus. Die Auswahl
  bezieht sich auf einen Treffer.

### Handy
- Langes Drücken auf eine Zeile startet den Modus mit dieser Zeile gewählt.
- Im Modus wählt ein Tipp auf die Zeile oder ihr Kästchen aus bzw. ab. Ein Detail gibt es
  im Kassenbuch noch nicht (`ledger-q1hw2`); wenn es kommt, entscheidet jene Task, wie man
  aus dem Modus ins Detail gelangt.
- Die Auswahl-Leiste ersetzt die Tab-Bar: Reihe 1 "n ausgewählt" und Summe, Reihe 2 "Alle"
  und "Auswahl aufheben". Abwählen der letzten Zeile beendet den Modus nicht.
- "Alle" setzt `AllExcept ∅` und trägt dann den Teal-Aktivzustand; "Alle" nochmal leert die
  Auswahl, der Modus bleibt.
- "Auswahl aufheben" beendet den Modus, die Tab-Bar kommt zurück.

### PC
- Kästchen je Zeile; ein Klick wählt aus bzw. ab und setzt den Anker.
- Shift-Klick wählt alle geladenen Zeilen zwischen Anker und Klick (Listenreihenfolge)
  dazu; unter `AllExcept` nimmt er sie aus den Ausnahmen. Der Anker trägt
  `box-shadow:inset 2px 0 0 #00d4aa` und bleibt bis zum nächsten einfachen Klick.
- Kopfzeilen-Kästchen, Zustand abgeleitet aus Anzahl der Auswahl gegen `TotalCount`:
  leer / Teal-Strich / Haken. Klick bei leer oder Strich wählt den ganzen Treffer
  (`AllExcept ∅`), Klick bei Haken leert die Auswahl.
- Die Auswahl-Leiste erscheint, solange die Auswahl nicht leer ist, und ersetzt den
  Tastatur-Hinweis: "n ausgewählt", Summe, "Auswahl aufheben". Kein "Alle".

### Beide Geräte
- Solange die Auswahl nicht leer ist, zeigt die Summenzeile "Auswahl: n von N Buchungen"
  mit der Summe der Auswahl und der Teal-Kante. Die Treffer-Form der Summenzeile am PC bleibt,
  wie `ledger-vgfw3` sie gebaut hat.
- Der Ausstieg heißt überall "Auswahl aufheben".
- **Keine schreibenden Aktionen in der Leiste** (Entscheidung Roman 2026-10-19): "Geprüft"
  kommt mit `ledger-nhwrt`, "Kategorie ändern" und "Flag" mit ihren eigenen Tasks. Bis dahin
  sind sie weder sichtbar noch deaktiviert gezeigt. Die Handy-Leiste hat deshalb zwei
  Reihen statt drei.
- Tastaturkürzel (Leertaste, `G`) sind nicht Teil dieser Task.

## Acceptance criteria
- [ ] `selectionTotals`: für `Picked` Anzahl und Summe der gewählten geladenen Zeilen; für
      `AllExcept ex` `TotalCount - |ex|` und `TotalAmount - Summe(ex)`; in YNAB gelöschte
      Buchungen zählen in keinem Fall zur Summe (Unit-Tests, je ein Fall).
- [ ] Reducer-Test: langes Drücken startet den Modus mit genau dieser Zeile; ein Tipp im
      Modus wählt ab, ein zweiter wieder aus; Abwählen der letzten Zeile lässt den Modus an.
- [ ] Reducer-Test: "Alle" ergibt `AllExcept ∅` mit Anzahl `TotalCount` und Summe
      `TotalAmount`, auch wenn nur eine Seite geladen ist; "Alle" nochmal leert die Auswahl.
- [ ] Reducer-Test: unter `AllExcept` lädt eine weitere Seite nach, ihre Zeilen gelten als
      gewählt; eine abgewählte Zeile senkt Anzahl und Summe um genau ihren Betrag.
- [ ] Reducer-Test: Shift-Klick vom Anker (Zeile 1) auf Zeile 12 wählt zwölf Zeilen, der
      Anker bleibt Zeile 1; ein einfacher Klick setzt den Anker neu.
- [ ] Reducer-Test: Kopfzeilen-Kästchen leer → alle; Strich → alle; Haken → leer. Der
      abgeleitete Zustand (leer / Strich / Haken) ist für alle drei Fälle getestet.
- [ ] Reducer-Test: Konto-, Filter- und Chipwechsel leeren die Auswahl und beenden den Modus.
- [ ] Reducer-Test: mit aktivem Chip "Zu prüfen" verlässt eine gewählte Zeile nach einem
      Prüfvermerk die Liste; sie ist danach nicht mehr in der Auswahl, Anzahl und Summe
      stimmen (`Picked` und `AllExcept`, je ein Test).
- [ ] Ansichts-Test: Summenzeile zeigt bei leerer Auswahl den Treffer, sonst
      "Auswahl: n von N Buchungen" mit der Summe der Auswahl.
- [ ] Ansichts-Test: Handy-Leiste zeigt "n ausgewählt", Summe, "Alle" und
      "Auswahl aufheben"; PC-Leiste dasselbe ohne "Alle". Keine der beiden zeigt
      "Geprüft", "Kategorie ändern" oder "Flag".
- [ ] Ansichts-Test: der Anker der Spanne trägt die Teal-Kante, andere gewählte Zeilen nicht.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes` grün.
- [ ] Handy 390x844 und PC 1440x900 entsprechen den Screens 3.1 bis 3.7 bis auf die
      weggelassenen schreibenden Aktionen; die Leiste verdeckt am Handy keine gewählte Zeile
      dauerhaft. [human-eye]

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Brief: `docs/ux/kassenbuch/briefs.md`,
Ablauf 3. Handoff: `docs/ux/kassenbuch/handoff-ablauf-3-mehrfachauswahl-mit-summe.md`.

Am 2026-09-18 aus den fertigen Screens nachgetragen (Kopfzeilen-Kästchen über den ganzen
Treffer, Anker der Shift-Spanne, Beschriftung des Ausstiegs, Höhen der Handy-Leiste). Die
Aktion "Kopieren" aus dem ursprünglichen Brief ist gestrichen (Entscheidung Roman
2026-09-14, war eine Erfindung im Brief).

Verfeinert am 2026-10-19. Entscheidungen Roman: schreibende Aktionen weglassen statt
deaktiviert zeigen; Kopfzeilen-Kästchen bei Teilauswahl erweitert auf alle. Als Standard
gesetzt (Vorschlag aus dem Handoff, offene Punkte 1, 3, 4): Wechsel leert die Auswahl, Tipp im
Modus wählt aus, Summenzeile am PC bleibt in der Form von `ledger-vgfw3`. Offene Punkte 5
und 6 des Handoffs (Chips "Flag"/"Gelöschte zeigen", Reihenfolge der Konto-Karten)
betreffen Ablauf 2 und sind nicht Teil dieser Task.

Die Auswahl-Summe braucht keinen Server: abwählbar sind nur geladene Zeilen, deshalb lässt
sich `AllExcept` aus den Server-Summen des Treffers minus den Ausnahmen rechnen. Das Feld
`selectionTotal` aus dem Handoff ("vom Server") ist damit abgeleitet.
