---
id: ledger-rjb8w
title: "Taste G und Tastaturfokus in der Kassenbuch-Liste (PC)"
status: doing
type: feature
context: ledger
created: 2026-10-19
completed:
depends_on: [ledger-r777c, design-system-001]
blocks: [ledger-q1hw2]
tags: [frontend, kassenbuch, pc, tastatur]
related_adrs: [0028, 0032, 0049, 0050]
related_research: []
prior_art: []
---

## Why
Am PC geht Roman die Liste mit der Tastatur durch, ohne die Hand zur Maus zu bewegen. Für
den Prüfvermerk braucht das eine Fokuszeile und die Taste G. Heute gibt es in
`src/Client/Components/Ledger` keine Tastaturnavigation.

## What
1. **Fokuszeile:** Pfeil auf und ab (und J/K) bewegen einen Fokus durch die Zeilen der
   Liste. Der Fokus bleibt beim Nachladen einer Seite und beim Wechsel eines Filters
   sinnvoll: er springt auf die erste Zeile, wenn die fokussierte Zeile aus der Liste
   fällt.
2. **Taste G:** setzt auf der Fokuszeile `Reviewed` (ADR 0050) und springt auf die
   nächste Zeile. Die Taste "Ungeprüft" liegt auf Umschalt+G. Eingabefelder (Suche,
   Filter) fangen die Taste ab: dort tippt G einen Buchstaben.
3. **Darstellung des Fokus** nach ADR 0049: ruhig, ohne Leuchtfarbe, ein einzelner
   Akzent. Die Pink-Kante für "zu prüfen" bleibt das Signal der Zeile, der Fokus
   unterscheidet sich davon (zum Beispiel ein heller Rahmen oder Hintergrund in
   `text-muted`-Tönen).
4. **Hinweis:** ein kleiner Tastaturhinweis ("G geprüft") in der Listenleiste.
5. **Auswahl-Modus** (`ledger-r1d2f`): solange eine Auswahl läuft, ist G aus, damit
   nicht unklar ist, ob G die Fokuszeile oder die Auswahl meint. Die Pfeiltasten bewegen
   den Fokus weiter. Die Sammel-Aktion kommt in `ledger-nhwrt` über die Leiste, nicht
   über G.
6. **Fokus als gemeinsamer Baustein:** die Fokuszeile ist der `keyboardCursor` aus
   Handoff Ablauf 7; `ledger-q1hw2` lässt das Seitenpanel ihr folgen und belegt `Enter`
   und `Esc`. Diese Task belegt `Enter` und `Esc` nicht.

## Acceptance criteria
- [ ] Reducer-Test: Pfeil ab und auf bewegen den Fokus um eine Zeile, am Listenende und
      am Listenanfang bleibt der Fokus stehen.
- [ ] Reducer-Test: Taste G auf der Fokuszeile löst `setReviewMark Reviewed` für diese
      Buchung aus und setzt den Fokus auf die nächste Zeile (mit aktivem Chip "Zu
      prüfen": die Zeile verschwindet, der Fokus liegt auf der Zeile, die nachrückt).
- [ ] Reducer-Test: Umschalt+G löst `Unreviewed` aus.
- [ ] Reducer-Test: Fokus auf der letzten Zeile, G gesetzt, die Zeile verschwindet:
      der Fokus liegt auf der neuen letzten Zeile (kein Fokus ins Leere).
- [ ] Tastenereignis aus einem Eingabefeld (Suche, Filter) löst keinen Befehl aus
      (Test auf die Filterfunktion für Tastenereignisse).
- [ ] G auf einer Zeile ohne YNAB-Verknüpfung oder mit Fehlschlag verhält sich wie der
      Klick auf die Schaltfläche (Fehlschlag-Hinweis aus `ledger-r777c`).
- [ ] Die fokussierte Zeile hat ein erkennbares Merkmal im Ansichts-Test (Klasse oder
      `aria-selected`), nicht fokussierte Zeilen nicht.
- [ ] Tastenbelegung kollidiert nicht mit bestehenden Kürzeln der Anwendung (Test oder
      Durchsicht der bestehenden Tastatur-Handler, Ergebnis in den Notes der Umsetzung).
- [ ] Reducer-Test: bei laufender Auswahl löst G nichts aus, die Pfeiltasten bewegen den
      Fokus weiter.
- [ ] `dotnet build` und `dotnet test` grün.
- [ ] Der Fokus ist neben der Pink-Kante gut erkennbar und die Liste bleibt ruhig
      (ADR 0049). [human-eye]

## Notes
Abgespalten am 2026-10-19 beim Verfeinern von `ledger-r777c` (Entscheidung von Roman).
Die Mehrfachauswahl (`ledger-r1d2f`, `ledger-nhwrt`) hat eigene Tastaturwünsche und ist
hier nicht Teil des Umfangs.

Verfeinert am 2026-10-19 beim Verfeinern von `ledger-q1hw2`: G ist im Auswahl-Modus aus,
die Fokuszeile ist die Grundlage für das PC-Seitenpanel (`ledger-q1hw2` hängt daran).
