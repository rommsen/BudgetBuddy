---
id: ledger-bn913
title: "Kassenbuch sortieren, PC und Client-Zustand: Klick auf einen Spaltenkopf sortiert serverseitig nach Datum, Betrag, Payee, Kategorie oder Konto; außer bei Datum fallen die Tagesköpfe weg"
status: doing
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001, ledger-qh9qx, ledger-r72re]
blocks: [ledger-skpcx, ledger-q1hw2]
tags: [frontend, kassenbuch, sortierung, tabelle]
related_adrs: [0036, 0037, 0049, 0051]
related_research: []
prior_art: [ledger-b1xwe, ledger-be1yh, ledger-p3n8k]
---

## Why
Die Liste ist heute fest nach Datum absteigend sortiert (`ORDER BY date DESC, id DESC` in
`Ledger.Persistence`). Roman will "mit Klick auf Datum etc." sortieren (2026-10-20), etwa
die größten Ausgaben eines Treffers oder alle Buchungen eines Payees beisammen sehen.

## What
Die Aufgabe ist in drei Teile geschnitten, jeder für sich auslieferbar:

1. `ledger-r72re`: Server. Vertrag, Persistenz, Messung. Ohne Oberfläche, Standardverhalten
   unverändert.
2. **Diese Aufgabe:** Sortierzustand im Client, klickbare Spaltenköpfe am PC, Tagesköpfe nur
   bei Datumssortierung, Datum in der Handy-Zeile bei flacher Liste.
3. `ledger-skpcx`: Sortier-Chip und BottomSheet am Handy.

Entscheidung Roman 2026-10-20 (gilt für alle drei Teile):

- **Sortierbar:** Datum, Betrag, Payee, Kategorie, in der Gesamtsicht Konto. Memo und
  Status nicht.
- **PC:** Klick auf einen sortierbaren Spaltenkopf sortiert danach; erneuter Klick dreht
  die Richtung. Der aktive Kopf zeigt Richtung per Pfeil und `aria-sort`.
- **Handy:** eine Sortier-Auswahl am Ende der Filterleiste, öffnet ein `BottomSheet`
  (`ledger-skpcx`).
- **Server sortiert** (`ledger-r72re`, ADR 0051). Trefferzahl und Summe bleiben über den
  ganzen Treffer korrekt.
- **Tagesköpfe:** Bei Sortierung nach Datum (auf- oder absteigend) bleiben Tagesgruppen
  und Tagessummen. Bei jeder anderen Sortierung wird die Liste flach, ohne Tagesköpfe; das
  Datum steht dann in jeder Zeile (PC: Datumsspalte; Handy: in der Zeile).
- Standard ist Datum absteigend. Die Sortierung lebt im Client-Zustand, übersteht Konto-
  und Filterwechsel, wird nicht dauerhaft gespeichert. Ein Sortierwechsel leert die
  Auswahl (sie bezieht sich auf geladene Zeilen) und lädt ab Seite 1 neu.

Festgelegt beim Verfeinern (2026-10-20, Begründung in ADR 0051):

- **Startrichtung** beim Wechsel auf ein anderes Feld: Datum absteigend, Betrag aufsteigend
  (kleinster Wert zuerst, also die größte Ausgabe oben), Payee, Kategorie und Konto
  aufsteigend. Ein Klick auf das schon aktive Feld dreht die Richtung. Eine reine Funktion
  `sortStartDirection field` liefert die Startrichtung.
- **Modell:** `Model.Sort: LedgerSort` (Typ aus `ledger-r72re`), Standard
  `LedgerSort.defaultSort`. Nachricht `SetSort of LedgerSortField`: gleiches Feld dreht die
  Richtung, anderes Feld setzt das Feld mit seiner Startrichtung. Der Sortierwechsel läuft
  über denselben Weg wie ein Filterwechsel (`resetEntries`, dann `LoadEntries`): Seite 0,
  `LocallyRemoved = 0`, Auswahl leer, Filter und Konto unverändert.
- **Wirksame Sortierung:** `effectiveSort model` liefert `LedgerSort.defaultSort`, wenn
  `SortByAccount` gewählt ist und `SelectedFilter` ein einzelnes Konto ist (die Spalte gibt
  es dort nicht). Das Modell behält die Wahl für die Rückkehr in die Gesamtsicht.
  `buildPageQuery` setzt `Sort = effectiveSort model`, Kopf und Listenform lesen ebenfalls
  `effectiveSort`.
- **Listenform:** Tagesgruppen (`groupByDate`, Tagessumme) nur, wenn die wirksame
  Sortierung `SortByDate` ist, in beiden Richtungen und auf PC und Handy. Sonst eine flache
  Liste ohne `calmGroupRow` und ohne `dateGroupHeader`. Eine reine Funktion
  `usesDayGroups sort` trägt die Entscheidung. In der flachen Handy-Zeile steht das Datum
  (`formatDate`) vorn in Zeile 2, vor Kontokürzel und Kategorie.
- **Spaltenkopf:** neue Design-System-Funktion `Table.calmSortableHeaderCell text align
  direction onSort` neben `calmHeaderCell`: ein `th` mit `aria-sort`
  (`ascending`, `descending`, `none`) und einem `button` darin (Tastatur: Enter und
  Leertaste), Pfeil nur am aktiven Kopf. Sie nutzt den vorhandenen `Table.SortDirection`
  (`Ascending | Descending | Unsorted`). Datum, Konto (nur in der Gesamtsicht), Payee,
  Kategorie und Betrag benutzen sie, Kästchen, Memo und Status bleiben `calmHeaderCell`
  bzw. `calmHeaderContentCell`. Die neue Funktion kommt in die `/styleguide`-Route
  (`src/Client/Components/Styleguide/View.fs`, Abschnitt der ruhigen Tabelle).
- "Alle Filter löschen" setzt die Sortierung nicht zurück: sie ist kein Filter.

## Acceptance criteria
Maschinell prüfbar:

- [ ] `Model.Sort` startet mit `LedgerSort.defaultSort`; `buildPageQuery` trägt
      `effectiveSort` (Update-Test, inklusive des Falls Konto-Sortierung mit
      `OneAccount`, der `defaultSort` liefert).
- [ ] `sortStartDirection` liefert Datum absteigend, Betrag aufsteigend, Payee, Kategorie,
      Konto aufsteigend (Test).
- [ ] `SetSort` auf ein anderes Feld setzt Feld und Startrichtung, auf das aktive Feld dreht
      es die Richtung; in beiden Fällen: `NextPage = 0`, `LocallyRemoved = 0`,
      `Selection = NoSelection`, Filter und `SelectedFilter` unverändert, `LoadEntries`
      wird ausgelöst (Update-Tests).
- [ ] Ein Konto- oder Filterwechsel (`SelectFilter`, Filter-Nachrichten) lässt `Sort`
      unverändert (Update-Test); "Alle Filter löschen" ebenso.
- [ ] `usesDayGroups` ist nur für `SortByDate` wahr, in beiden Richtungen (Test).
- [ ] Bei Datum aufsteigend gruppiert `groupByDate` die aufsteigend gelieferte Liste
      richtig, mit Tagessumme je Gruppe (Test mit zwei Tagen).
- [ ] Die flache Handy-Zeile zeigt das Datum in Zeile 2, die gruppierte nicht (Test der
      reinen Zeilen-Hilfsfunktion).
- [ ] Der Wert von `aria-sort` je `Table.SortDirection` ist `ascending`, `descending`
      oder `none` (Test der reinen Hilfsfunktion); Memo- und Status-Kopf bauen sich nicht
      über die Sortier-Funktion.
- [ ] Im Browser (PC-Viewport, Gesamtsicht): Klick auf "Betrag" lädt die Liste neu, die
      Beträge laufen vom kleinsten Wert aufwärts, kein Tageskopf steht in der Tabelle; ein
      zweiter Klick kehrt die Reihenfolge um; Klick auf "Datum" bringt die Tagesköpfe
      zurück. Der Kopf trägt `aria-sort`, der Fokus folgt der Tab-Reihenfolge, Enter löst
      aus (Chrome DevTools, Screenshot im Verifier-Bericht).
- [ ] `/styleguide` zeigt die sortierbare Kopfzelle mit den drei Zuständen.

Zum Hinschauen:

- [ ] Die Sortier-Pfeile im Kopf fügen sich in die ruhige Tabelle (ADR 0049). [human-eye]

## Notes
- Hängt an `ledger-qh9qx`, weil beide die Spaltenköpfe anfassen und die Kopf-Zellen
  dort neu verteilt werden. Blendet `ledger-qh9qx` bei schmaler Breite die Konto-Spalte aus,
  bleibt eine gewählte Konto-Sortierung aktiv, auch wenn kein Kopf sie mehr anzeigt; das
  Konto steht dann im Kürzel der Payee-Zelle. Die Handy-Auswahl (`ledger-skpcx`) zeigt die
  Wahl in jedem Fall.
- Hängt an `ledger-r72re`: ohne das Feld `Sort` in `LedgerEntryPageQuery` gibt es nichts
  zu senden.
- Der Sortierwechsel hängt an `resetEntries`; Fokuszeile und Auswahl-Modus fallen damit wie
  bei jedem Filterwechsel zurück.
- README-Satz "Kategoriewechsel sortiert nie um" bleibt gültig: eine Änderung an einer
  Zeile sortiert die geladene Liste nicht neu. Heute ändert kein Schreibweg einen
  Sortierschlüssel (der Prüfvermerk berührt nur `approved` und `cleared`), also gibt es nichts
  zu behandeln. Kommt das Bearbeiten der Kategorie (`ledger-q1hw2`), gilt die Grenze aus
  ADR 0051 Punkt 7.
- `ledger-rfq5p` ändert dieselben Zellen (Payee, Kategorie) in `View.fs`; bei gleichzeitiger
  Arbeit auf Merge-Konflikte in `entryTableRow` achten.
