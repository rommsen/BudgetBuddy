---
id: ledger-skpcx
title: "Kassenbuch sortieren am Handy: Sortier-Chip am Ende der Filterleiste öffnet ein BottomSheet mit allen Sortierungen"
status: backlog
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001, ledger-bn913, ledger-r72re]
blocks: []
tags: [frontend, kassenbuch, sortierung, handy, bottomsheet]
related_adrs: [0043, 0049, 0051]
related_research: []
prior_art: [ledger-j8xq4, ledger-saqsk]
---

## Why
Teil 3 von 3 der Sortierung (`ledger-bn913`). Am PC sortieren die Spaltenköpfe; die
Handy-Liste hat keine Köpfe. Am Handy (rund 70 % der Nutzung) braucht dieselbe Sortierung
einen eigenen Zugang. Zustand, Server und flache Listenform kommen aus `ledger-r72re` und
`ledger-bn913`; hier fehlt nur die Bedienung.

## What
Entscheidung Roman 2026-10-20: eine Sortier-Auswahl am Ende der Filterleiste, die ein
`BottomSheet` mit denselben Optionen und der Richtung öffnet.

Festgelegt beim Verfeinern (2026-10-20):

- **Der Chip** steht als letzter Eintrag der Filterleiste (`ChipBar`), hinter Betrag. Er
  zeigt die aktive Sortierung kurz: Feldname und Pfeil, etwa "Datum ↓" (absteigend) oder
  "Betrag ↑" (aufsteigend). Er hat kein "×". Er ist nur dann "aktiv" gefärbt, wenn die
  Sortierung von `LedgerSort.defaultSort` abweicht. Er gehört nicht zu `fixedChipOrder`,
  nicht zu `orderChipsForDisplay` (aktive Chips wandern am Handy nach vorn, dieser bleibt
  hinten), nicht zu `hasAnyFilter` und nicht zu "Alle Filter löschen".
- **Wann er erscheint:** genau dann, wenn die Liste als Handy-Liste gerendert wird, also
  nach derselben Entscheidung, die zwischen Tabelle und Handy-Liste wählt (nach
  `ledger-qh9qx`: die Funktion über die Tabellenbreite, nicht allein der Breakpoint `md`).
  Wird die Tabelle gezeigt, sortieren die Köpfe, und der Chip fehlt. Beide Wege setzen
  dasselbe `Model.Sort`.
- **Das Sheet** (`BottomSheet.view`, Titel "Sortieren") enthält eine Liste von Zeilen,
  eine je Kombination, gruppiert nach Feld, mit einem Haken an der aktiven:
  - Datum: "Neueste zuerst" (Standard), "Älteste zuerst"
  - Betrag: "Größte Ausgabe zuerst" (aufsteigend), "Größte Einnahme zuerst" (absteigend)
  - Payee: "A bis Z", "Z bis A"
  - Kategorie: "A bis Z", "Z bis A"
  - Konto: "A bis Z", "Z bis A", nur in der Gesamtsicht
  Ein Tipp auf eine Zeile setzt Feld und Richtung zugleich (`SetSortExactly`, siehe
  unten), schließt das Sheet und lädt ab Seite 1 neu. Es gibt keinen getrennten
  Richtungsschalter, damit pro Änderung genau eine Anfrage hinausgeht.
- **Nachricht:** `SetSortExactly of LedgerSort` (setzt die Sortierung genau so, ohne
  Umschalten), über denselben Weg wie `SetSort` aus `ledger-bn913` (`resetEntries`,
  `LoadEntries`; Auswahl leer, Seite 0, `LocallyRemoved = 0`). Das Sheet öffnet und
  schließt über `OpenSortSheet` und `CloseSortSheet`.
- **Auswahl der Optionen** als reine Funktion `sortOptions showAccount`, die geordnete Liste
  aus `(LedgerSort, Beschriftung)` liefert. Der Chip-Text kommt aus `sortChipLabel sort`.
- **Design-System:** eine Options-Zeile mit Haken im `BottomSheet`-Modul
  (`BottomSheet.optionRow label isSelected onClick`), 44px Trefferfläche, ruhig nach ADR 0049.
  Das neue Element kommt in die `/styleguide`-Route (Abschnitt "18. BottomSheet").
- Am PC rendert nichts davon (ADR 0043: eine Chrome zur Renderzeit, keine versteckte
  Doppelmontage, weil das BottomSheet nach `document.body` portalt).

## Acceptance criteria
Maschinell prüfbar:

- [ ] `sortOptions true` liefert zehn Optionen in der oben genannten Reihenfolge,
      `sortOptions false` acht (ohne Konto); jede Option hat eine eindeutige
      `LedgerSort`-Kombination (Test).
- [ ] `sortChipLabel` liefert für `defaultSort` "Datum ↓", für Betrag aufsteigend
      "Betrag ↑" (Test).
- [ ] Der Chip ist nur aktiv gefärbt, wenn `Sort <> defaultSort`; er steht nicht in
      `orderChipsForDisplay`, `hasAnyFilter` bleibt von der Sortierung unberührt, "Alle
      Filter löschen" lässt `Sort` stehen (Tests).
- [ ] `SetSortExactly` setzt `Sort` genau auf den übergebenen Wert (auch wenn er dem
      aktuellen entspricht, dann ohne Umschalten), setzt `NextPage = 0`,
      `LocallyRemoved = 0`, `Selection = NoSelection`, lässt Filter und Konto stehen und
      löst `LoadEntries` aus (Update-Tests).
- [ ] `OpenSortSheet` und `CloseSortSheet` ändern `Sort` und die Filter nicht (Update-Test).
- [ ] Im Browser im 390x844-Viewport (zweiter Playwright-Kontext wie bei ADR 0043): der
      Chip steht am Ende der Leiste, ein Tipp öffnet das Sheet, "Größte Ausgabe zuerst"
      sortiert die Liste flach nach Betrag aufsteigend, das Datum steht in Zeile 2 jeder
      Zeile, das Sheet ist zu und der Chip zeigt "Betrag ↑". Am PC-Viewport erscheint der
      Chip nicht. Konto-Optionen fehlen, sobald ein einzelnes Konto gewählt ist.
- [ ] `/styleguide` zeigt die Options-Zeile mit gewähltem und ungewähltem Zustand.

Zum Hinschauen:

- [ ] Der Chip am Ende der Leiste und das Sheet wirken ruhig und lassen die Filterleiste
      lesbar (ADR 0049). [human-eye]

## Notes
- Hängt an `ledger-bn913` (Zustand, `SetSort`, flache Listenform, Datum in der
  Handy-Zeile) und `ledger-r72re` (Server).
- Wechselt ein einzelnes Konto bei aktiver Konto-Sortierung, gilt `effectiveSort` aus
  `ledger-bn913`; der Chip zeigt dann die wirksame Sortierung (Datum ↓), das Modell behält
  die Wahl.
- Der Chip zeigt keinen Zähler und löst keinen Popover aus (`OpenFilterPopover` kennt nur
  Filter-Chips); er braucht eine eigene Klick-Nachricht.
