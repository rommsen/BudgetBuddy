# Nachtrag für das Design-Projekt (2026-09-18)

Zum Einfügen in die `CLAUDE.md` des Claude-Design-Projekts „Gelesen UI-Mockups"
(`5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`).

**Warum dieser Umweg:** Das Werkzeug, mit dem ich Dateien im Design-Projekt lese und
schreibe, lehnt `CLAUDE.md` und `.claude/` als reservierte Pfade ab, weil sie den
Design-Agenten steuern. Das ist eine gewollte Sperre. Den Text muss deshalb Roman in einen
Chat des Design-Projekts geben; der Agent pflegt seine Anweisungsdatei dann selbst.

Alles hier stammt aus dem Abgleich vom 2026-09-18 (Repo-Commits `ac8661c`, `fad1ed9`,
`cb3b29e`, `095dd71`, `6b0cda2`).

---

## 1. Abschnitt „Übergabe an Entwicklung": DaisyUI streichen

Dort steht, nachzubauen sei „F# / Fable / Elmish mit Tailwind 4 **und DaisyUI**". Das ist
falsch und die Quelle eines Kreislaufs: die Angabe landet in jedem Übergabe-Dokument, das
Dokument wandert ins Repo, und dort widerspricht es dem Projekt.

**Neu:** „nachzubauen in F# / Fable / Elmish mit Tailwind 4 **ohne DaisyUI** gegen
`src/Client/DesignSystem/*.fs` und die Tokens aus `src/Client/styles.css`. DaisyUI ist im
Repo mit **ADR 0031** entfernt und steht nicht in `package.json`."

Im Repo sind am 2026-09-18 neun Stellen mit derselben veralteten Angabe korrigiert worden,
darunter das Export-Skript, das den Bundle-README für dieses Design-Projekt erzeugt.

## 2. Block „Aus Ablauf 7 dazugekommen": zwei Regeln ändern

**Gelöschte Buchungen.** Bisher: „sie zählen in der Trefferzahl, nicht in der Summe".

**Neu:** Eingeblendet zählen sie **in Trefferzahl und Summe**, damit die sichtbaren Zeilen
sich zur angezeigten Summe addieren. Mit den erfundenen Daten heißt das **-722,06 €** statt
der gezeichneten -712,26 €. Die Zeile über der Liste sagt es aus, im Stil „13 Buchungen,
davon 1 in YNAB gelöscht". Im Kontostand zählen sie weiterhin nie mit. Entscheidung Roman,
Aufgabe `ledger-d4kx8`. Der Hinweistext im Detail verliert damit seinen Halbsatz „und nicht
zur Summe".

**Historie.** Die Richtung des Entwurfs bleibt: neueste Zeile oben. Ergänzen, wie es im
Code läuft, damit niemand es für einen Fehler hält: gedreht wird nur die Anzeige, die
Abfrage `getEntryHistory` bleibt aufsteigend, damit laufende Nummer und Replay-Pflichttest
unberührt bleiben. Aufgabe `ledger-h9tq2`.

## 3. Offener Punkt 16 ist entschieden

Punkt 16 stellte die Varianten für die gelöschte Zeile noch zur Wahl. Er ist erledigt und
kann durch die Entscheidung aus Nummer 2 ersetzt werden.

## 4. Tabelle „Stand der Abläufe"

Roman hat die Screens der Abläufe 3 bis 7 am 2026-09-18 abgenommen.

- **Ablauf 3 und 7:** Übergabe ist im Repo aus der Tafel verfasst, weil es hier kein Paket
  gibt: `docs/ux/kassenbuch/handoff-ablauf-3-mehrfachauswahl-mit-summe.md` (566 Zeilen) und
  `docs/ux/kassenbuch/handoff-ablauf-7-was-nur-bb-kann.md` (711 Zeilen).
- **Abläufe 4, 5 und 6:** abgenommen, Übergabe folgt auf Zuruf.

Für Ablauf 1 und 2 bleibt es dabei: die Repo-Dokumente sind wortgleiche Kopien der Pakete,
mit zwei bewussten Abweichungen (Bundle-Pfade gekürzt, DaisyUI gestrichen).

## 5. Neue offene Punkte: beim Ablesen der Tafeln aufgefallen

Beim Verfassen der zwei Übergabe-Dokumente sind Ungereimtheiten in den Screens
aufgetaucht. Sie sind dort benannt, nicht glattgezogen, und beheben lassen sie sich nur in
den Tafeln.

1. **„Abweichung" und „Zu prüfen" tragen dasselbe Pink** (`#ff1f8f`). In der Zeile „REWE
   Mitte" (Screen 7.6) stehen zwei farbgleiche Badges übereinander, die Verschiedenes
   bedeuten. Eine der beiden braucht eine andere Farbe oder eine andere Form.
2. **Die Zeitleiste der gelöschten Buchung ist unmöglich:** Löschung am 11.09., Import am
   15.09., Prüfung am 16.09. (Screen 7.10). Die Notiz beschreibt die umgekehrte Reihenfolge.
3. **Drei Werte in Ablauf 7 weichen von Ablauf 1 und 2 ab:** Tabellenkopf 40px auf
   `#111128` statt 42px auf `#1a1a36`, Memo-Spalte `#4a4a68` statt `#7a7a98`, PC-Summe 14px
   statt 15px. Dazu fehlt der markierten Zeile der Teal-Ring aus Ablauf 1, und der
   Ungeprüft-Punkt ist 7 × 7px inline statt 6 × 6px absolut. Entweder nachziehen oder die
   Abweichung begründen.
4. **Am PC fehlen in Ablauf 3 die Chips „Flag" und „Gelöschte zeigen"**, die Ablauf 2
   zeigt. Kürzung oder Absicht?
5. **Der Kita-Beitrag steht in den zwölf geladenen Zeilen von Ablauf 3 nur zweimal**
   (09.09. und 12.08.), im Juli fehlt er. Die Annahme „125,00 € im Monat" ist im Screen
   damit nicht nachvollziehbar, obwohl die Summe der zwölf Zeilen mit -780,00 € stimmt.
6. **Zählung in der Notiz zu Screen 7.1** („fünf der neun ungeprüften plus vier geprüfte")
   passt nicht zu den sechs gezeichneten Zeilen (drei und drei). In 7.6 sind zwölf Zeilen
   sichtbar, aber nur fünf tragen „Zu prüfen", während der Chip „Zu prüfen (9)" zeigt.
7. **Chip-Reihenfolge am PC (Screen 7.9):** die Notiz sagt „aktive Chips wandern am PC
   nicht nach vorn", gezeichnet stehen die zwei aktiven Chips trotzdem zuerst.
8. **Scrollbarkeit der PC-Chip-Leiste bei offenem Panel:** die Notiz behauptet „waagerecht
   scrollbar", im Markup steht `overflow:hidden`.

## 6. Aufgeräumt

Die leere Datei `Ablauf 4 - Kategorie aendern und Split.dc.html` (Rest einer Umbenennung,
nur das Gerüst `<x-dc></x-dc>`) ist nach Romans Freigabe gelöscht. `Canvas.dc.html` ist
ebenfalls leer, bleibt aber stehen, weil sie zur Projektvorlage gehört.

Alle sieben Tafeln sind auf Vollständigkeit geprüft, über ihre Sprungmarken gezählt:
1.1 bis 1.12, 2.1 bis 2.16, 3.1 bis 3.7, 4.1 bis 4.11, 5.1 bis 5.12, 6.1 bis 6.10,
7.1 bis 7.10, zusammen 78 Screens, deckungsgleich mit der Tabelle in der `CLAUDE.md`.
