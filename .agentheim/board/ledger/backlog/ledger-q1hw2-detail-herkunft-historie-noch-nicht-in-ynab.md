---
id: ledger-q1hw2
title: "Buchungs-Detail nach ADR 0053: ein Klick auf die Zeile markiert sie und öffnet das Detail (Panel rechts am PC, Sheet am Handy und in der Kompaktliste) mit Herkunft, Historie, Hinweisleiste, Geprüft-Schalter und Filter-Knöpfen; Zellmenü und altes Historie-Sheet entfallen"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-b1xwe, design-system-001, ledger-rynk5, ledger-rjb8w, ledger-rfq5p, ledger-qh9qx, ledger-bn913]
blocks: [ledger-b7qmx, ledger-w4vte]
tags: [frontend, kassenbuch, scheibe, bedienkonzept]
related_adrs: [0013, 0021, 0045, 0049, 0050, 0052, 0053]
related_research: []
prior_art: [ledger-q5w9c, ledger-fsz0a, ledger-h9tq2, ledger-r777c, ledger-rfq5p]
---

## Why
Das ist, was nur BB kann und YNAB nicht hat: woher eine Buchung stammt, was sich wann und
woher geändert hat, und ob sie überhaupt schon in YNAB angekommen ist. Heute steckt die
Historie in einem Sheet, das auch am PC unten aufgeht; am PC fehlt eine Form, die neben der
Liste stehen bleibt.

Dazu kommt die Bedienung: ADR 0053 (Gespräch mit Roman, 2026-10-20) legt fest, dass das
Detail der eine Ort für alle Aktionen zu einer Buchung ist. Heute öffnen Payee- und
Kategorie-Zelle am PC ein Zellmenü mit einem Eintrag, und die Historie zeigt dieselben
Filter-Aktionen noch einmal (Bestandsaufnahme
`docs/ux/kassenbuch/bedienkonzept-zeile-bestandsaufnahme.md`, Reibungen 1 bis 4, 8, 11, 12).
Diese Aufgabe ist Stück 1 von drei (danach `ledger-w4vte` Auswahl, `ledger-m6zpd`
Kompaktliste nach Eingabegerät).

## What
Bild und Text: Handoff Ablauf 7 (`docs/ux/kassenbuch/handoff-ablauf-7-was-nur-bb-kann.md`),
Screens 7.2, 7.3 (ohne Pink-Leiste), 7.5, 7.7, 7.8 (ohne Pink-Leiste), 7.10. Wo der Handoff
und ADR 0053 sich unterscheiden, gilt ADR 0053.

Ein Detail je Buchung, am PC als **Seitenpanel rechts** (420 px), damit die Liste sichtbar
bleibt; am Handy und in der PC-Kompaktliste (ADR 0052) als BottomSheet. Das bisherige
Historie-Sheet (`OpenHistory`/`historySheet`) geht darin auf.

1. **Öffnen, Markieren, Schließen (ADR 0053 Punkte 1, 3, 5).**
   - **Ein Klick irgendwo auf die Zeile** markiert sie und öffnet das Detail. Markieren
     heißt: die Tastatur-Fokuszeile aus `ledger-rjb8w` (`FocusedRow`) springt auf diese
     Zeile. Das gilt für jede Zelle, auch Payee und Kategorie; die Zellen haben keine
     eigenen Klickziele mehr (siehe 9, Rückbau). Ausgenommen bleiben nur Kästchen und
     Prüf-Schaltfläche (eigene Wirkung, `stopPropagation` wie heute).
   - **PC:** `Enter` auf der markierten Zeile öffnet das Panel. `↑ ↓ J K` bewegen die
     Markierung, ein offenes Panel folgt und lädt die Historie der neuen Buchung. `G` und
     `Umschalt+G` wirken wie in `ledger-rjb8w` auf die markierte Zeile, also auf die
     Buchung im Panel. Dafür darf ein offenes Panel die Listentasten nicht mehr sperren
     (heute sperrt `keyboardBlocked` sie bei `HistoryFor`); ein offenes Sheet am Handy und
     in der Kompaktliste sperrt sie weiter. `Esc` schließt das Panel. (Die volle Esc-Kette
     Popover, Panel, Auswahl kommt in `ledger-w4vte`.)
   - **Handy:** Tipp auf eine Zeile öffnet das Sheet, außerhalb des Auswahl-Modus aus
     `ledger-r1d2f`; dort wählt ein Tipp weiterhin aus. Schließen über den Knopf, die
     Abdunklung oder `Esc` (Tastatur am Tablet).
   - **Spalten bei offenem Panel:** Memo fällt immer weg. Die übrigen Spalten entscheidet
     `visibleColumns` aus `ledger-qh9qx` mit der Tabellenbreite abzüglich 420 px, so dass in
     schmalen Fenstern auch die Konto-Spalte wegfallen kann (Kontokürzel in der
     Payee-Zelle). Reicht die Restbreite für keine Tabellenstufe, oder zeigt der PC schon
     die Kompaktliste, öffnet das Detail als Sheet statt als Panel. Die Form wird beim
     Öffnen aus der Breite ohne Panel bestimmt, damit Panel und Kompaktliste nicht hin und
     her kippen.
   - **Sortierung (`ledger-bn913`):** Die markierte Zeile und das Panel überstehen einen
     Sortierwechsel; das Panel zeigt weiter seine Buchung, auch wenn sie nach dem Neuladen
     nicht auf den geladenen Seiten steht (dann ist keine Zeile markiert).
   - Der Tastaturhinweis bei offenem Panel lautet
     "↑ ↓ bewegen · G geprüft · Esc Panel schließen".
2. **Kopf:** Betrag, Payee mit Filter-Knopf (siehe 4a), "Konto · Datum", Schließen-Knopf.
3. **Hinweisleiste** direkt unter dem Kopf, höchstens eine, in dieser Rangfolge:
   - Rot **"in YNAB gelöscht"** (`DeletedInYnab`): Text mit Löschzeitpunkt aus der
     Historie, "Zählt nicht zum Kontostand.", keine Aktion.
   - Orange **"Noch nicht in YNAB"** (`LinkedToYnab = false`): mit
     `PushToYnabFailure` der Satz "Push gescheitert am <Datum> <Uhrzeit>: <Grund>. BB
     versucht es beim nächsten Lauf erneut."; ohne Fehlschlag "Noch nicht an YNAB
     gesendet." Keine Aktion (ein "Erneut senden" für den Push ist nicht Teil dieser
     Scheibe).
   - Orange **"Prüfvermerk nicht in YNAB"** (`ReviewMarkWriteBackFailure`): Grund und die
     wirkende Aktion "Erneut senden" (`retryReviewMarkWriteBack` aus `ledger-r777c`).
   - Die Pink-Leiste "Abweichung" ist v2 und wird nicht gebaut.
4. **Felder, auf Handy und PC gleich** (Roman, 2026-10-19: die PC-Fassung gilt überall):
   - **Kategorie** lesend, am PC voll qualifiziert (`categoryLabelQualified`, ADR 0039), mit
     Filter-Knopf (4a). Rechts in der Zeile bleibt Platz für den Pfeil, mit dem ab v2 ein
     Klick den Kategorie-Picker öffnet (`ledger-k2rbn`); in dieser Aufgabe gibt es keinen
     Pfeil und die Zeile ist nicht klickbar.
   - **Split** lesend: "Kein Split" oder je Split-Zeile Ziel und Betrag. Die volle
     Qualifizierung der Split-Kategorien kommt mit `ledger-b7qmx`.
   - **Memo** lesend, leer als Platzhalter in `text-muted`.
   - **Geprüft-Schalter** (DS-Komponente, neu): wirkt über `setReviewMark` aus
     `ledger-r777c` (an: `Reviewed`, aus: `Unreviewed`), mit demselben Fehlschlag-Weg
     wie die Schaltfläche an der Zeile.
4a. **Filter-Knöpfe (ADR 0053 Punkt 1, löst die Abkürzung aus `ledger-rfq5p` ab):** ein
   kleiner Knopf mit Trichter-Symbol neben dem Payee im Kopf und neben dem Kategorie-Wert.
   Zugänglicher Name und Tooltip: "Nur Buchungen von <Payee> zeigen" bzw. "Nur Buchungen in
   <Kategorie> zeigen" (voller Name, keine Kürzung). Wirkung wie heute `FilterByRowPayee`
   und `FilterByRowCategory`: Payee exakt und gefaltet (ADR 0037); Kategorie ersetzt die
   Kategorienauswahl durch genau diese eine und schaltet "Ohne Kategorie" aus; alle anderen
   Filter bleiben. **Das Detail bleibt offen** und zeigt weiter dieselbe Buchung. Keine
   Knöpfe bei leerem Payee, ohne Kategorie, bei Split und Übertrag ohne Kategorie
   (`payeeMenuLabel`/`categoryMenuLabel`, Bedingungen unverändert). Die Knöpfe sind auch in
   einer gelöschten Buchung da (Filtern ist keine Änderung an der Buchung).
5. **Herkunft:** eigene Zeile "HERKUNFT" mit Teal-Badge aus `LedgerEntryView.Origin`:
   Comdirect-Import, Regel, YNAB-Spiegel, In BB entschieden.
6. **Historie** als Zeitleiste aus `getEntryHistory`, neueste Zeile oben
   (`historyLinesForDisplay`, `ledger-h9tq2`), je Eintrag Satz, Zeitstempel, Urheber.
   Punktfarbe nach Urheber: Teal Import, Violett Regel, Grau YNAB-Spiegel, Green eigene
   Prüfung in BB, Rot für Fehlschläge und Löschung. Als `RemoteData`: Lade- und
   Leerzustand ("Noch keine Einträge") sind Pflicht (Handoff, offener Punkt 12). Im
   Sheet und im Panel scrollt die Historie.
7. **Gelöschte Buchung ist ein Lesezustand:** Betrag in `text-muted` statt Rot, Felder
   grau, kein Geprüft-Schalter, keine Aktion außer den Filter-Knöpfen.
8. Neue DS-Bausteine nach der Tabelle "Neu zu bauende Komponenten" im Handoff
   (Herkunft-Zeile, Historie-Zeitleiste, Hinweisleiste mit Grund und Aktion, Seitenpanel,
   Geprüft-Schalter) plus der Filter-Knopf (Icon-Knopf mit Tooltip), je mit
   Galerie-Beispiel in `/styleguide`. Darstellung ruhig nach ADR 0049.
9. **Rückbau (ADR 0053 Punkt 3):**
   - Zellmenü aus `ledger-rfq5p`: `cellMenuPopover`, `Model.CellMenu`, `CellMenuTarget`,
     `CellMenuKind`, `OpenCellMenu`, `CloseCellMenu`, die Auslöser-Buttons in `payeeCell` und
     `categoryCell` (die Zellen werden wieder reiner Text im `truncatedRoleCell`) und
     `CellMenu` in `keyboardBlocked`. `FilterByRowPayee` und `FilterByRowCategory` bleiben
     und werden von den Filter-Knöpfen benutzt (ohne `HistoryFor` zurückzusetzen).
   - Das alte `historySheet` samt Filter-Block (`row-filter-actions`).
   - `FilterPopover.menuItem`, `menuWidthClass` und `menuItemAutoFocus` dürfen im
     Design-System bleiben, mit ihrer Styleguide-Karte.
   - Tests und Playwright-Wächter des Zellmenüs (`LedgerCellMenuTests.fs`,
     `scripts/ledger-cell-menu.test.mjs`) werden entfernt oder auf die Filter-Knöpfe
     umgeschrieben; `payeeMenuLabel`/`categoryMenuLabel` und ihre Tests bleiben.

Für die offenen Punkte des Handoffs gilt: Badge-Schreibung im Satzanfang groß ("In YNAB
gelöscht", "Noch nicht in YNAB"), sonst wie im Styleguide; die markierte Zeile bei
offenem Panel nutzt die Fokus-Darstellung aus `ledger-rjb8w`.

## Acceptance criteria
- [ ] Reducer-Test: Klick auf eine Zeile setzt `FocusedRow` auf diese Buchung, öffnet das
      Detail für genau sie und lädt die Historie (`Loading`, dann `Success`); im
      Auswahl-Modus am Handy öffnet ein Tipp kein Detail.
- [ ] Reducer-Test (PC): bei offenem Panel bewegt `↓` die Markierung, das Panel zeigt die
      neue Buchung mit neu geladener Historie, und `G` setzt den Prüfvermerk der Buchung im
      Panel; `Esc` schließt; `Enter` auf der markierten Zeile öffnet.
- [ ] Reducer-Test: bei offenem Sheet (Handy, Kompaktliste) bleiben die Listentasten
      gesperrt, bei offenem Panel nicht.
- [ ] Quelltext-/Ansichts-Test: Payee- und Kategorie-Zelle der PC-Zeile enthalten keinen
      eigenen Auslöser mehr; `CellMenu`, `OpenCellMenu`, `CloseCellMenu` und
      `historySheet` gibt es nicht mehr.
- [ ] Reducer-Test: der Payee-Filter-Knopf setzt `Filters.Payee`, der Kategorie-Knopf
      ersetzt die Kategorienauswahl durch genau diese Id und schaltet `WithoutCategory`
      aus; andere Filter bleiben; das Detail bleibt offen mit derselben Buchung; die Liste
      lädt ab Seite 0 neu.
- [ ] Ansichts-Test: Split, Buchung ohne Payee, ohne Kategorie und Übertrag ohne Kategorie
      zeigen den jeweiligen Filter-Knopf nicht; der Tooltip trägt den vollen Namen.
- [ ] Expecto-Test der Detail-Form: bei offenem Panel ist Memo in keiner Breite sichtbar,
      die übrigen Spalten folgen `visibleColumns` mit Breite minus 420 px; reicht die
      Restbreite für keine Tabellenstufe oder ist das Layout schon die Kompaktliste, ist
      die Form Sheet statt Panel (Grenzwerte getestet).
- [ ] Reducer-Test: ein Sortierwechsel lässt das offene Panel mit seiner Buchung stehen.
- [ ] Ansichts-Test: Rangfolge der Hinweisleiste: gelöscht schlägt "Noch nicht in YNAB",
      das schlägt "Prüfvermerk nicht in YNAB"; eine verknüpfte, nicht gelöschte Buchung
      ohne Fehlschlag hat keine Leiste.
- [ ] Ansichts-Test: "Noch nicht in YNAB" mit `PushToYnabFailure` zeigt Datum, Uhrzeit und
      Grund; ohne Fehlschlag den Satz "Noch nicht an YNAB gesendet."; keine Aktion.
- [ ] Reducer-Test: "Erneut senden" in der Prüfvermerk-Leiste ruft
      `retryReviewMarkWriteBack` für diese Buchung.
- [ ] Reducer-Test: der Geprüft-Schalter ruft `setReviewMark Reviewed` bzw. `Unreviewed`;
      Erfolg und Fehlschlag aktualisieren Detail und Listenzeile gleich wie die
      Schaltfläche an der Zeile.
- [ ] Ansichts-Test: Felder Kategorie, Split, Memo und Geprüft erscheinen auf Handy und PC;
      eine Buchung mit zwei Split-Zeilen zeigt beide Zeilen mit Betrag, ohne Splits steht
      "Kein Split"; die Kategorie-Zeile hat keinen Pfeil.
- [ ] Ansichts-Test: die Herkunft-Zeile zeigt für jede der vier `LedgerOrigin`-Arten das
      richtige Badge.
- [ ] Ansichts-Test: die Historie erscheint neueste zuerst; die Punktfarbe folgt dem
      Urheber; Lade- und Leerzustand sind vorhanden.
- [ ] Ansichts-Test: eine gelöschte Buchung hat keinen Geprüft-Schalter, keine Aktion außer
      den Filter-Knöpfen und einen grauen Betrag.
- [ ] Playwright-Wächter (Handy 390 px und PC 1440 px, gemockte API): PC: Klick auf die
      Payee-Zelle öffnet das Panel (kein Menü), die Liste bleibt sichtbar, die Memo-Spalte
      fehlt, die Zeile ist markiert, der Filter-Knopf filtert und das Panel bleibt offen;
      bei etwa 900 px (Kompaktliste) öffnet das Detail als Sheet. Handy: Tipp öffnet das
      Sheet mit Filter-Knöpfen an den Feldern, kein Block oben.
- [ ] Neue DS-Bausteine haben je ein Galerie-Beispiel in `/styleguide`.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.
- [ ] Sheet und Panel lesen sich ruhig, die Hinweisleiste fällt auf, ohne zu schreien
      (ADR 0049). [human-eye]
- [ ] Die Filter-Knöpfe sind zu finden, ohne das Detail unruhig zu machen. [human-eye]

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Brief: `docs/ux/kassenbuch/briefs.md`,
Ablauf 7.

Sortierung (ADR 0051, Punkt 7): Ändert ein Schreibweg aus dem Detail den Schlüssel der
aktiven Sortierung, stimmt der Seitenversatz `LocallyRemoved` nicht mehr. In dieser Aufgabe
schreibt das Detail nur den Prüfvermerk, der kein Sortierschlüssel ist; die Frage stellt
sich erst mit Kategorie ändern (`ledger-k2rbn`).

Verfeinert am 2026-10-19. Entscheidungen Roman: Handy und PC zeigen dieselben Felder (die
PC-Fassung gilt); der Geprüft-Schalter wirkt und "Erneut senden" für den Prüfvermerk
wirkt; der Grund eines gescheiterten Pushs wird als Ereignis festgehalten
(`ledger-rynk5`, eigene Task, Vorbedingung). Die Fokuszeile und `G` kommen aus
`ledger-rjb8w`, deshalb hängt das PC-Panel daran. Die Historie-Reihenfolge ist in
`ledger-h9tq2` entschieden (neueste oben).

Befund nach dem Deploy vom 2026-10-20 (Roman am PC und am Handy): Die Filter-Aktionen
standen am PC doppelt (Zellmenü und Historie-Sheet), das Zellmenü war schwer lesbar und
hatte nur einen Eintrag. Roman wollte die Bedienung der Zeile als Ganzes denken.
Bestandsaufnahme: `docs/ux/kassenbuch/bedienkonzept-zeile-bestandsaufnahme.md`.

Neu verfeinert am 2026-10-20 nach ADR 0053 (Gespräch mit Roman). Entscheidungen: das Detail
ist der eine Ort für Aktionen an einer Buchung; Zellen ohne eigene Klickziele; Klick
markiert; Filter-Knöpfe an den Feldern, das Detail bleibt beim Filtern offen; Rückbau des
Zellmenüs in dieser Aufgabe (keine Zwischenschritte). Roman will große Umbauten. Läuft nach
`ledger-bn913`, weil beide `entryTableRow` ändern. Die Regel "Sheet statt Panel, wenn die
Restbreite für keine Tabellenstufe reicht" ist ein Vorschlag aus der Verfeinerung, Roman
beim Bau zeigen. `ledger-mhe2s` (Prüf-Schaltfläche bei Hover/Fokus) wirkt ohne Zutun auch auf
die markierte Zeile.

Grenze zu `ledger-w4vte` (Stück 2): dort kommen Leertaste, "Auswahl schließt das Panel",
die volle Esc-Kette, die Sperren während einer Auswahl und der vollständige Fußzeilen-Hinweis.
Diese Aufgabe ändert an der Auswahl nichts.

Nicht Teil: Abweichung (Pink, v2), Kategorie- und Split-Bearbeitung (v2, `ledger-k2rbn`),
"Erneut senden" für den Push, "Wiederherstellen" gelöschter Buchungen.
