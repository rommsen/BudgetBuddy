---
id: ledger-j8xq4
title: "Kassenbuch-Filterleiste und Summenzeile: Chips, Kategorienauswahl mit Mehrfachauswahl, Popover am PC"
status: backlog
type: feature
context: ledger
created: 2026-09-19
completed:
depends_on: [ledger-vgfw3, design-system-001]
blocks: [ledger-r1d2f]
tags: [frontend, kassenbuch, scheibe]
related_adrs: [0004, 0005]
related_research: []
prior_art: [ledger-b1xwe, ledger-x0yga]
---

## Why
Die Oberflächen-Hälfte der zweiten lesenden Scheibe. `ledger-vgfw3` baut die Filter im
Lesepfad; ohne Bedienelemente kann Roman sie nicht benutzen. Beide Hälften zusammen decken
seinen häufigsten Arbeitsablauf ab: Buchungen finden.

Zwei Dinge sollen besser sein als in YNAB-Web: mehrere Kategorien auf einmal, und der
Filter muss am Handy genauso gut gehen. Roman nutzt ihn dort heute nie, weil er zu
umständlich ist. Genau daran misst sich diese Aufgabe.

## What
Vorlage ist `docs/ux/kassenbuch/handoff-ablauf-2-buchungen-finden.md`, sechzehn Screens
mit verbindlichen Werten.

**Sticky Filterleiste aus Chips** über der Liste, auf beiden Geräten: Kategorie,
Zeitraum, Payee, Betrag, Memo. Der Filter wirkt immer serverseitig über
`ledger-vgfw3`, nie im Client über die geladene Seite.

- **Kategorie** öffnet die Kategorienauswahl mit **Mehrfachauswahl**. Versteckte
  Kategorien stehen eingeklappt am Ende („Versteckt (n)"), damit sie nicht versehentlich
  getroffen werden. Am Handy als BottomSheet, am PC als Popover.
- **Click-Commit** (Styleguide 7.1, ADR 0005): die Mehrfachauswahl wirkt erst auf
  „Übernehmen (n)", nicht bei jedem Antippen. Einzelauswahl (Payee) filtert sofort.
- **Zeitraum** mit Schnellwahl (dieser Monat, letzter Monat, dieses Jahr, letzte zwölf
  Monate) und Von/Bis. Die Schnellwahl **schreibt Von und Bis mit**, damit sichtbar ist,
  was gefiltert wird.
- **Payee**, **Memo**, **Betrag** als Suchfelder mit kurzer Verzögerung beim Tippen.
  Die Payee-Vorschläge kommen mit Anzahl aus dem aktuellen Treffer (Abfrage aus
  `ledger-vgfw3`), nicht aus allen Buchungen.
- **Summenzeile** über der Liste, sobald ein Filter aktiv ist: Trefferzahl und
  Gesamtbetrag über den ganzen Treffer. Ohne aktiven Filter steht dort nichts. Damit ist
  Romans Steuererklärungs-Fall ohne Ankreuzen erledigt.
- **Am Handy wandern aktive Chips nach vorn**, weil die Leiste waagerecht scrollt und der
  aktive Filter sichtbar bleiben muss. Am PC bleibt die Reihenfolge fest.
- Beschriftung im Aktivzustand: Zähler bei Mehrfachauswahl („Kategorie: 2"), Kurzform beim
  Zeitraum („12 Monate"), Klartext beim Payee („Café Lotte").
- Am PC hängt die Auswahl als **Popover unter seinem Chip**, die Liste bleibt sichtbar,
  `Esc` und Klick daneben schließen ohne zu filtern. Ein Muster für Kategorie, Zeitraum
  und Payee, später auch für die Kategoriezuweisung (Ablauf 4) und den Flag-Chip
  (Ablauf 5).
- Jeder Chip trägt ein „×", am Ende der Leiste „Alle Filter löschen".
- Filterwechsel springt an den Listenanfang und setzt die Seite zurück, die Chips bleiben
  sichtbar (Styleguide 7.3).
- **Leerzustand ohne Treffer** sagt, was als Nächstes zu tun ist („Kein Treffer. Filter
  lockern oder alle Filter löschen.").

**Bewusst nicht in dieser Aufgabe:** das Verhalten „Passt eine Zeile nach einer Änderung
nicht mehr zum aktiven Filter, bleibt sie ausgegraut an ihrem Platz" (Entscheidung aus
Ablauf 5). Es setzt voraus, dass eine Zeile in BB überhaupt geändert werden kann, und
hängt damit an `ledger-r777c` beziehungsweise der v2-Entscheidung. Solange das Kassenbuch
nur liest, tritt der Fall nicht ein. Wird er später gebaut, ist er ein eigener Task, weil
er einem blinden Neuladen nach jeder Änderung widerspricht.

## Acceptance criteria
- [ ] Ein Kategorie-Filter über **zwei** Kategorien lässt sich am Handy und am PC setzen,
      und die Liste zeigt danach die Vereinigung beider.
- [ ] Die Mehrfachauswahl wirkt erst auf „Übernehmen", nicht beim Antippen einer
      einzelnen Kategorie (Click-Commit, ADR 0005). Durch einen Test über den Reduzierer
      belegt, nicht nur per Augenschein.
- [ ] Versteckte Kategorien sind eingeklappt und erst nach einem zusätzlichen Schritt
      erreichbar.
- [ ] Die Zeitraum-Schnellwahl trägt Von und Bis sichtbar ein, statt sie nur intern zu
      setzen.
- [ ] Die Summenzeile erscheint genau dann, wenn mindestens ein Filter aktiv ist, und
      zeigt Trefferzahl und Summe über den ganzen Treffer, nicht über die geladene Seite.
      Mit mehr Treffern als einer Seite belegt.
- [ ] Am PC schließt `Esc` das Popover, ohne den Filter anzuwenden; der vorherige
      Filterzustand bleibt unverändert.
- [ ] Am Handy stehen aktive Chips vorn, am PC bleibt die Reihenfolge fest. Über den
      Reduzierer belegt, der die Chip-Reihenfolge liefert, nicht über einen Screenshot.
- [ ] „×" am Chip entfernt genau diesen Filter, „Alle Filter löschen" alle; die
      Summenzeile verschwindet danach wieder.
- [ ] Ein Filterwechsel setzt die Seite auf die erste zurück, statt die bisherige
      Seitennummer beizubehalten.
- [ ] Ohne Treffer steht ein Leerzustand da, der eine nächste Handlung nennt.
- [ ] Screenshots in beiden Viewports neben den Screens 2.1 bis 2.14 des Übergabepakets
      zeigen keine sichtbare Abweichung in Chip-Zuständen, Picker und Summenzeile.
      [human-eye]
- [ ] `dotnet build` und `dotnet test` grün, `npm run build` sauber.

## Notes
Abgespalten von `ledger-vgfw3` am 2026-09-19 beim Verfeinern. `vgfw3` behält die Id, weil
`ledger-r1d2f` und der Oberflächen-Abschnitt des ledger-README darauf verweisen; diese
Aufgabe übernimmt die Oberfläche und blockt nun ihrerseits `ledger-r1d2f` (die Auswahl mit
Summe setzt die Summenzeile voraus, nicht die Server-Filter).

Die neu zu bauenden Bausteine sind im Übergabepaket am Ende aufgelistet
(Kategorienauswahl mit Mehrfachauswahl, Filter-Popover, Chip-Leiste mit Zähler und „×",
Summenzeile, Schnellwahl Zeitraum, Payee-Vorschlagsliste, Konto-Kürzel-Pill). Sie gehören
als echte Komponenten ins Design-System, nicht als Inline-Feliz in die Kassenbuch-Seite.

Der Chip „Zu prüfen" existiert bereits (`ledger-kdnnz`) und ist in die neue Leiste
einzuordnen, nicht neu zu bauen. Der Available-Betrag je Kategorie im Picker kommt aus
`ledger-kwwxz`; ohne ihn zeigt der Picker die Kategorien ohne Betrag, was hier kein
Fehler ist.
