# Bedienkonzept Kassenbuch-Zeile: Bestandsaufnahme

Stand: 2026-10-20, Code auf `main` bei 4b58b94. Eingabe für die Konzeptrunde "Was passiert,
wenn ich auf eine Zeile klicke". Danach werden `ledger-q1hw2` und ein möglicher Umbau des
Zellmenüs neu verfeinert.

Abkürzungen: V = `src/Client/Components/Ledger/View.fs`, S = `.../State.fs`,
T = `.../Types.fs`, DS = `src/Client/DesignSystem/`. Zeilennummern zum Stand oben.

## Anlass

Nach dem Deploy von `ledger-rfq5p` am 2026-10-20: Am PC öffnet ein Klick auf Payee oder
Kategorie ein kleines, schwer lesbares Menü mit einem einzigen Eintrag. Ein Klick sonst auf
die Zeile öffnet die Historie, und dort stehen dieselben Filter-Aktionen noch einmal. Die
Bedienwege einer Zeile sind über sechs Tasks gewachsen, ohne gemeinsame Leitlinie.

## Heute

### PC-Tabellenzeile (`entryTableRow` V:806-862)

| Element | Wirkung | eingeführt | Ausnahmen |
|---|---|---|---|
| Klick auf die Zeile | Historie als BottomSheet (`OpenHistory`) | q5w9c | auch bei laufender Auswahl; setzt die Tastatur-Fokuszeile nicht; Zeile ohne `tabIndex`, per Tastatur nicht erreichbar |
| Hover | Prüf-Schaltfläche wird sichtbar, Zeiger | mhe2s | |
| Datum, Konto, Memo, Betrag | wie Zeilenklick | q5w9c | Memo und Konto fallen je nach Breite weg (ADR 0052) |
| Payee-Text | Zellmenü "Nach <Payee> filtern" | rfq5p | nur mit Payee; freie Fläche neben dem Text öffnet die Historie; Kontokürzel gehört nicht dazu |
| Kategorie-Text | Zellmenü "Nach <Kategorie> filtern" | rfq5p | nicht bei Split, ohne Kategorie, Übertrag ohne Kategorie; dann wie Zeilenklick |
| Eintrag im Zellmenü | setzt den Payee- bzw. Kategorie-Filter, lädt neu | rfq5p | leert dabei die Auswahl (`resetEntries`) |
| Kästchen | Zeile an/ab, Umschalt-Klick nimmt die Spanne | r1d2f | |
| Prüf-Schaltfläche | geprüft / ungeprüft | r777c, mhe2s | nur bei Hover oder Fokus sichtbar; fehlt bei gelöschter Buchung; bleibt bei laufender Auswahl aktiv |
| "Erneut senden" (Hinweiszeile) | Prüfvermerk erneut nach YNAB | r777c | nur bei Fehlschlag |

### Handy-Zeile (`mobileEntryRow` V:1291-1307), auch PC-Kompaktliste

| Element | Wirkung | eingeführt | Ausnahmen |
|---|---|---|---|
| Tipp auf die Zeile | Historie-Sheet | q5w9c | im Auswahl-Modus: Zeile an/ab |
| Langes Drücken (500 ms) | Auswahl-Modus starten | r1d2f | nur Touch, mit der Maus nicht möglich |
| Wisch nach rechts | geprüft, Toast mit "Rückgängig" | th36h | nur Touch, nur bei "zu prüfen", nicht im Modus; Wisch nach links unbelegt |
| Prüf-Schaltfläche (44 px) | geprüft / ungeprüft | r777c, mhe2s | fehlt bei gelöschter Buchung und im Modus |
| Kästchen im Modus | nur Anzeige | r1d2f | laut Code ohne Handler, laut r1d2f soll ein Tipp darauf auswählen (nicht im Browser geprüft) |

### Historie-Sheet (`historySheet` V:1035-1092), am PC und am Handy dasselbe

| Element | Wirkung | eingeführt | Ausnahmen |
|---|---|---|---|
| Filter-Aktionen oben | wie das Zellmenü, schließen das Sheet | rfq5p | ohne Geräteunterscheidung, also auch am PC |
| Inhalt | Herkunft und Historie, nur lesen | q5w9c, fsz0a, h9tq2 | keine Felder, kein Geprüft-Schalter |
| Schließen | Knopf oder Abdunklung | q5w9c | Esc nicht belegt, Griff nicht ziehbar |

Am PC ist es ein unten angeschlagenes Sheet (max. 520 px), kein Seitenpanel.

### Tastatur (PC)

| Taste | Wirkung | eingeführt | Ausnahmen |
|---|---|---|---|
| ↓ / J, ↑ / K | Fokuszeile bewegen | rjb8w | gesperrt bei offenem Popover, Zellmenü oder Historie |
| G / Umschalt+G | geprüft / ungeprüft, Fokus springt weiter | rjb8w | gesperrt bei Auswahl, gelöschter Buchung, während des Sendens |
| Enter, Esc, Leertaste | nicht belegt | | ein fokussierter Button reagiert nativ auf Enter und Leertaste |

Der Hinweis in der Fußzeile nennt "↑ ↓ bewegen · G geprüft · Umschalt+G ungeprüft".

### Auswahl

| Element | Wirkung | eingeführt |
|---|---|---|
| Leiste "Geprüft" | Sammel-Prüfvermerk mit "Rückgängig" | nhwrt |
| "Auswahl aufheben" | leert die Auswahl, beendet den Modus | r1d2f |
| jeder Filter-, Chip-, Konto- oder Sortierwechsel | leert die Auswahl | r1d2f |

Am PC gibt es keinen Auswahl-Modus, die Leiste erscheint ab einer gewählten Zeile.

## Geplant

- **`ledger-q1hw2` Buchungs-Detail:** Die Historie geht im Detail auf, am Handy als Sheet,
  am PC als Seitenpanel rechts (420 px). PC: Zeilenklick oder Enter öffnet das Panel,
  ↑ ↓ bewegen und das Panel folgt, Esc schließt, G wirkt weiter. Payee- und Kategorie-Zelle
  öffnen weiter das Zellmenü. Neu im Detail: Geprüft-Schalter, Hinweisleiste mit "Erneut
  senden", Felder nur lesend. Kategorie ändern ist v2.
- **`ledger-bn913` / `ledger-skpcx` Sortieren:** klickbare Spaltenköpfe am PC, Sortier-Chip
  mit Sheet am Handy. An der Zeile ändert sich nichts, außer dass Tagesköpfe bei anderer
  Sortierung wegfallen.
- **`ledger-b7qmx`:** Split-Kategorien voll qualifiziert im Detail.
- **`ledger-xfjcp` (roh):** "Lena fragen/zuweisen" im Detail und in der Auswahl-Leiste.
- **`ledger-kwwxz` (v2):** Kategorie-Picker mit Available. `rfq5p` sah "Kategorie ändern" als
  zweiten Eintrag im Zellmenü vor.
- Die Leertaste zum Auswählen steht in allen Handoffs, ist aber in keiner Aufgabe vergeben.

## Reibungen

1. **Filter-Aktionen doppelt am PC:** Zellmenü und Historie-Sheet.
2. **Zellmenü mit einem Eintrag:** zwei Klicks für einen Befehl; 240 px mit `p-4`, die
   Beschriftung wird gekürzt, das Menü deckt die Zeile darunter ab.
3. **Zeilen- und Zellklick sind mehrdeutig:** In derselben Zelle öffnet der Text das Menü,
   die freie Fläche daneben die Historie. Ob eine Zelle klickbar ist, hängt vom Inhalt ab
   und zeigt sich nur beim Hover.
4. **Handoff und Code widersprechen sich bei der Kategorie-Zelle:** Ablauf 1 will dort den
   Kategorie-Picker direkt, Ablauf 7 legt den Picker ins Panel, der Code öffnet ein
   Filtermenü.
5. **Klick daneben bei offenem Zellmenü:** Das Menü schließt auf `mousedown`, der folgende
   Klick erreicht die getroffene Zeile. Ein Klick auf eine andere Zeile schließt das Menü und
   öffnet deren Historie zugleich (aus dem Code, nicht im Browser geprüft).
6. **Vier Wege zum Prüfvermerk mit verschiedenen Sperren:** Schaltfläche, G, Wisch, Leiste.
   Am PC bleibt die Schaltfläche bei Auswahl aktiv, G nicht; am Handy fehlt die Schaltfläche
   im Modus. q1hw2 bringt mit dem Geprüft-Schalter einen fünften Weg.
7. **Fokus nach Mausklick:** Nach einem Klick auf Kästchen oder Prüf-Schaltfläche liegt der
   Browserfokus auf dem Button; Enter oder Leertaste lösen ihn erneut aus. Belegt q1hw2 Enter
   global, muss das abgegrenzt werden.
8. **Mausklick und Tastaturfokus sind getrennt:** Ein Klick auf Zeile B setzt die Fokuszeile
   nicht, G wirkt weiter auf A. Ablauf 7 erwartet, dass der Klick die Zeile markiert.
9. **Handy-Kästchen im Modus ohne Handler** (siehe oben, nicht im Browser geprüft).
10. **PC-Kompaktliste ist eine Sackgasse:** Bei schmalem Fenster zeigt der PC die Handy-Liste.
    Langes Drücken und Wisch gehen nur per Touch, mit der Maus gibt es dort keine Auswahl und
    kein Zellmenü. Die Fokuszeile ist unsichtbar, G wirkt trotzdem.
11. **Esc schließt die Historie nicht,** und bei offenem Sheet sind alle Listentasten gesperrt.
12. **Die PC-Zeile ist per Tastatur nicht aktivierbar,** bis q1hw2 Enter belegt.
13. **Filtern aus der Zeile verwirft die Auswahl** ohne Rückfrage.
14. **Hinweistext unvollständig:** J und K fehlen; Enter und Leertaste aus dem Handoff sind
    nicht gebaut.
15. **Auswahl-Modus am Handy hat keinen Weg ins Detail** (Handoff Ablauf 3, offener Punkt 3).

## Was die Handoffs wollten

- Ablauf 7, PC (`handoff-ablauf-7-was-nur-bb-kann.md`:500-505): "Klick auf eine Zeile oder
  `Enter` | öffnet das Seitenpanel rechts, Liste bleibt sichtbar, Zeile wird markiert" ·
  "`↑` `↓` | Markierung bewegen, das offene Panel folgt zur nächsten Buchung" · "`Esc` |
  Panel schließen" · "Klick auf Kategorie- oder Split-Zeile im Panel | öffnet Picker bzw.
  Split-Bearbeitung"
- Ablauf 7, Handy (:487, :492): "Tipp auf eine Zeile | öffnet das Detail als BottomSheet" ·
  "Tipp auf Kategorie-Feld im Sheet | öffnet den Kategorie-Picker (eigener Ablauf)"
- Ablauf 7 (:117-122): "Herkunft und Historie gehören ins Panel, sonst wird die Tabelle
  unlesbar." · "Genau eine primäre Aktion je Screen, und sie sitzt im Hinweis, nicht am Fuß
  des Panels: die Aktion gehört zum Grund."
- Ablauf 1 (`handoff-ablauf-1-durchsehen-und-pruefen.md`:332-334): "`Enter` | Detail im
  Seitenpanel rechts öffnen (Liste bleibt sichtbar)" · "Klick auf Zeile | Seitenpanel
  rechts" · "Klick auf Kategorie-Zelle | öffnet den Kategorie-Picker direkt, ohne Umweg über
  das Panel"
- Ablauf 3 (`handoff-ablauf-3-mehrfachauswahl-mit-summe.md`:554-555): "Tipp auf die Zeile am
  Handy im Auswahl-Modus. Auswählen oder Detail öffnen? Solange der Modus läuft, spricht alles
  für Auswählen, dann fehlt aber der Weg ins Detail."

Bezug: ADR 0043 (eine Chrome je Render), 0049 (ruhige Listen), 0050 (Prüfvermerk), 0051
(Sortierung), 0052 (Tabelle oder Liste nach Breite). Zu Auswahl-Modus und Tastatur gibt es
keine ADR, die Regeln stehen nur in `ledger-r1d2f` und `ledger-rjb8w`.

## Fragen für die Konzeptrunde

1. **Grundprinzip:** Ist das Detail der eine Ort für alle Aktionen zu einer Buchung
   (filtern, prüfen, später Kategorie ändern, Lena fragen)? Was bleibt dann direkt in der
   Zeile?
2. **Zellen am PC:** Filtert ein Klick auf Payee oder Kategorie direkt, öffnet er ein Menü
   mit mehreren Einträgen, oder ist die Zelle Teil des Zeilenklicks? Und wo landet später
   "Kategorie ändern": in der Zelle (Ablauf 1) oder im Panel (Ablauf 7)?
3. **Filter-Abkürzung am Handy:** im Detail-Sheet, anderswo, oder entfällt sie?
4. **Prüfvermerk:** Welche der fünf Wege bleiben, und gelten für alle dieselben Sperren?
5. **Maus und Tastatur:** Setzt ein Mausklick die Fokuszeile? Wofür stehen Enter, Esc und
   die Leertaste?
6. **Auswahl:** Wie kommt man im Handy-Modus ins Detail? Soll Filtern aus der Zeile die
   Auswahl verwerfen?
7. **PC-Kompaktliste:** Bekommt sie Maus-Wege für Auswahl und Aktionen, oder ist sie nur zum
   Lesen da?
8. **Festhalten:** Das Ergebnis als ADR "Bedienung der Kassenbuch-Zeile", an der sich
   q1hw2, xfjcp und das spätere Kategorie-Ändern ausrichten.
