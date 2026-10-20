---
id: ADR-0053
title: Bedienung der Kassenbuch-Zeile, das Panel für eine Buchung, die Leiste für viele
scope: ledger
status: accepted
date: 2026-10-20
supersedes: []
superseded_by: []
related_tasks: [ledger-q1hw2, ledger-rfq5p, ledger-r1d2f, ledger-rjb8w, ledger-mhe2s, ledger-th36h, ledger-nhwrt, ledger-bn913, ledger-skpcx, ledger-xfjcp]
related_adrs: [0028, 0043, 0049, 0050, 0052]
related_research: []
---

# ADR-0053: Bedienung der Kassenbuch-Zeile, das Panel für eine Buchung, die Leiste für viele

## Context

Die Bedienwege einer Kassenbuch-Zeile sind über mehrere Tasks gewachsen (q5w9c, r1d2f,
th36h, r777c, rjb8w, nhwrt, mhe2s, rfq5p), ohne gemeinsame Leitlinie. Nach dem Deploy von
`ledger-rfq5p` am 2026-10-20 fand Roman die Bedienung am PC verwirrend: Ein Klick auf Payee
oder Kategorie öffnet ein kleines Menü mit einem einzigen Eintrag, ein Klick sonst auf die
Zeile öffnet die Historie, und dort stehen dieselben Filter-Aktionen noch einmal.

Die Bestandsaufnahme (`docs/ux/kassenbuch/bedienkonzept-zeile-bestandsaufnahme.md`) nennt
weitere Reibungen: Zeilen- und Zellklick sind mehrdeutig; die Handoffs widersprechen sich
bei der Kategorie-Zelle (Ablauf 1: Picker direkt in der Zelle, Ablauf 7: Picker im Panel);
die Wege zum Prüfvermerk haben verschiedene Sperren; ein Mausklick setzt die Fokuszeile
nicht; die PC-Kompaktliste (ADR 0052) hat nur Touch-Gesten; Esc, Enter und Leertaste sind
nicht belegt.

Kategorie ändern (v2, Vision) wird kommen und muss mitgedacht werden, für eine Buchung und
für viele.

Entschieden im Gespräch mit Roman am 2026-10-20.

## Decision

**1. Eine Buchung: das Panel.** Ein Klick irgendwo auf die Zeile markiert sie und öffnet das
Detail, `Enter` auf der markierten Zeile ebenso. Am PC ist das Detail ein Seitenpanel rechts,
am Handy und in der PC-Kompaktliste ein BottomSheet. Das Detail ist der eine Ort für alle
Aktionen zu dieser Buchung: Prüfen (Schalter), "Erneut senden", Filtern nach Payee und nach
Kategorie, ab v2 Kategorie ändern, später weitere (etwa "Lena fragen"). Die Filter-Knöpfe
sitzen direkt an dem Feld, zu dem sie gehören. Filtern lässt das Panel offen. Ab v2 öffnet
ein Pfeil an der Kategorie-Zeile den Picker; Filtern und Ändern sind getrennte Ziele.

**2. Viele Buchungen: die Leiste.** Kästchen, Umschalt-Klick, Leertaste und am Handy langes
Drücken füllen die Auswahl. Die Leiste zeigt Anzahl, Summe und die Sammelaktionen: heute
"Geprüft", ab v2 "Kategorie setzen" mit demselben Picker wie im Panel. Panel und Auswahl sind
getrennt (kein Panel, das die Auswahl zeigt): sobald eine Auswahl beginnt, schließt das Panel.

**3. Die Zellen sind keine eigenen Klickziele.** Ein Klick auf Payee, Kategorie oder jede
andere Zelle ist ein Zeilenklick. Das Zellmenü aus `ledger-rfq5p` und der Filter-Block im
Historie-Sheet entfallen; die Abkürzung lebt im Panel. Ablauf 1 ("Klick auf Kategorie-Zelle
öffnet den Picker direkt") ist damit aufgegeben, Ablauf 7 gilt.

**4. Prüfen hat fünf Wege mit einer Regel.** Schaltfläche an der Zeile (PC bei Hover und
Fokus, Handy immer), `G` auf der markierten Zeile, Wisch nach rechts (Touch), Schalter im
Panel, "Geprüft" in der Leiste. Solange eine Auswahl läuft, gibt es nur die Sammelaktion:
Schaltfläche, `G` und Wisch ruhen, am PC wie am Handy.

**5. Die Tastatur folgt der Markierung.** Markierte Zeile und Tastatur-Fokuszeile sind
dasselbe; ein Mausklick setzt sie. `↑ ↓ J K` bewegen sie, ein offenes Panel folgt. `Enter`
öffnet das Panel. `Esc` schließt das zuletzt Geöffnete: Popover, dann Panel, dann Auswahl.
Leertaste nimmt die markierte Zeile in die Auswahl oder heraus. `G` und `Umschalt+G` wie in
`ledger-rjb8w`. Der Hinweis in der Fußzeile nennt alle belegten Tasten des Zustands.

**6. Gesten folgen dem Eingabegerät, nicht der Breite.** Die Breite wählt nur das Layout
(Tabelle oder Kompaktliste, ADR 0052). Mit Maus (`pointer: fine`) hat auch die Kompaktliste
Kästchen, Umschalt-Klick, die Prüf-Schaltfläche bei Hover, eine sichtbare Markierung und alle
Tasten. Mit Finger (`pointer: coarse`) gelten langes Drücken, Wisch und die immer sichtbare
Schaltfläche.

**7. Handy-Auswahlmodus.** Ein Tipp auf Zeile oder Kästchen wählt aus oder ab. Ins Detail
kommt man nach "Auswahl aufheben"; es gibt keinen zweiten Weg im Modus.

## Alternatives considered

- **Zellen handeln direkt (Klick auf Payee filtert sofort):** abgelehnt: versehentliches
  Filtern, wenn das Detail gemeint war, und die Kategorie-Zelle kann nicht zugleich filtern
  und ab v2 den Picker öffnen.
- **Zellmenü mit mehreren Einträgen behalten (rfq5p-Modell):** abgelehnt: bis v2 ein Menü mit
  einem Eintrag, und die Mehrdeutigkeit zwischen Zelltext und freier Fläche bleibt.
- **Das Panel zeigt die Auswahl (Finder-Modell: eine ausgewählt = Detail, mehrere =
  Sammelansicht):** abgelehnt: Kästchen, Zeilenklick und Fokuszeile müssten zu einem Begriff
  verschmelzen, und am Handy verdeckt ein Sheet die Liste, in der man auswählt. PC und Handy
  liefen auseinander.
- **Langes Drücken im Handy-Auswahlmodus öffnet das Detail:** abgelehnt: unsichtbar und
  verwechselbar mit dem langen Drücken, das den Modus startet.

## Consequences

- `ledger-q1hw2` wird nach dieser ADR neu verfeinert und in größere Bau-Stücke geschnitten.
  Das Zellmenü (`cellMenuPopover`, `Model.CellMenu`, Klickziele in `payeeCell` und
  `categoryCell`) und das alte `historySheet` werden zurückgebaut. `FilterPopover.menuItem`
  darf im Design-System bleiben.
- ADR 0052, Folge "Die kompakte Liste hat keine Kästchen-Spalte und keine
  Tabellen-Interaktionen": gilt nur noch für Touch. Mit Maus bekommt die Kompaktliste die
  Bedienung aus Punkt 6.
- Am PC ruht die Prüf-Schaltfläche künftig während einer Auswahl (heute bleibt sie aktiv).
- Kategorie ändern (v2) hat zwei feste Orte: Panel (eine Buchung) und Leiste (viele), ein
  Picker. Der Schreibweg folgt dem Muster von ADR 0050 und ist eigene Arbeit.
- `ledger-bn913` und `ledger-skpcx` (Sortierung) sind nicht betroffen; sie ändern Kopf und
  Filterleiste, nicht die Zeile.
- Ein Fokus auf einem Knopf (nach Mausklick auf Kästchen oder Prüf-Schaltfläche) darf Enter
  und Leertaste nicht doppelt auslösen; das regelt die Verfeinerung.
