---
id: ledger-q1hw2
title: "Buchungs-Detail: Herkunft, Historie und \"noch nicht in YNAB\" (Sheet am Handy, Seitenpanel am PC)"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-b1xwe, design-system-001, ledger-rynk5, ledger-rjb8w, ledger-rfq5p, ledger-qh9qx]
blocks: [ledger-b7qmx]
tags: [frontend, kassenbuch, scheibe]
related_adrs: [0013, 0021, 0045, 0049, 0050]
related_research: []
prior_art: [ledger-q5w9c, ledger-fsz0a, ledger-h9tq2, ledger-r777c]
---

## Why
Das ist, was nur BB kann und YNAB nicht hat: woher eine Buchung stammt, was sich wann und
woher geändert hat, und ob sie überhaupt schon in YNAB angekommen ist. Heute steckt die
Historie in einem eigenen Sheet, das nur am Handy gedacht ist; am PC fehlt eine Form, die
neben der Liste stehen bleibt.

## What
Bild und Text: Handoff Ablauf 7 (`docs/ux/kassenbuch/handoff-ablauf-7-was-nur-bb-kann.md`),
Screens 7.2, 7.3 (ohne Pink-Leiste), 7.5, 7.7, 7.8 (ohne Pink-Leiste), 7.10.

Ein Detail je Buchung, am Handy als BottomSheet, am PC als **Seitenpanel rechts** (420 px),
damit die Liste sichtbar bleibt. Das bisherige Historie-Sheet (`OpenHistory`/
`historySheet`) geht darin auf.

1. **Öffnen und Schließen.**
   - Handy: Tipp auf eine Zeile öffnet das Sheet (außerhalb des Auswahl-Modus aus
     `ledger-r1d2f`; dort wählt ein Tipp weiterhin aus). Schließen über den Knopf oder den
     Griff. Das Sheet übernimmt die beiden Aktionen "Nach <Payee> filtern" und "Nach
     <Kategorie> filtern" aus `ledger-rfq5p`, die dort im alten Historie-Sheet sitzen, mit
     derselben Wirkung und denselben Ausnahmen (kein Payee, keine Kategorie, Split).
   - PC: Klick auf eine Zeile oder `Enter` auf der Fokuszeile (`ledger-rjb8w`) öffnet das
     Panel. Ausgenommen sind Payee- und Kategorie-Zelle: sie öffnen weiter das Menü aus
     `ledger-rfq5p`, nicht das Panel. `↑`/`↓` bewegen die Fokuszeile, das offene Panel
     folgt. `Esc` schließt. `G` wirkt wie in `ledger-rjb8w` auf die Fokuszeile und damit
     auf die Buchung im Panel.
   - Bei offenem Panel fällt die Memo-Spalte immer weg. Die übrigen Spalten entscheidet
     `visibleColumns` aus `ledger-qh9qx` mit der Tabellenbreite abzüglich 420 px, so dass
     in schmalen Fenstern auch die Konto-Spalte wegfallen kann (Kontokürzel in der
     Payee-Zelle). Reicht die Restbreite für keine Tabellenstufe, öffnet das Detail als
     Sheet wie am Handy statt als Panel. Die Form wird beim Öffnen aus der Breite ohne
     Panel bestimmt, damit Panel und Handy-Liste nicht hin und her kippen.
   - Der Tastaturhinweis bei offenem Panel lautet
     "↑ ↓ bewegen · G geprüft · Esc Panel schließen".
2. **Kopf:** Betrag, Payee, "Konto · Datum", Schließen-Knopf.
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
   - **Kategorie** lesend, am PC voll qualifiziert (`categoryLabelQualified`, ADR 0039),
     ohne Chevron (Kategorie ändern ist v2).
   - **Split** lesend: "Kein Split" oder je Split-Zeile Ziel und Betrag. Die volle
     Qualifizierung der Split-Kategorien kommt mit `ledger-b7qmx`.
   - **Memo** lesend, leer als Platzhalter in `text-muted`.
   - **Geprüft-Schalter** (DS-Komponente, neu): wirkt über `setReviewMark` aus
     `ledger-r777c` (an: `Reviewed`, aus: `Unreviewed`), mit demselben Fehlschlag-Weg
     wie die Schaltfläche an der Zeile.
5. **Herkunft:** eigene Zeile "HERKUNFT" mit Teal-Badge aus `LedgerEntryView.Origin`:
   Comdirect-Import, Regel, YNAB-Spiegel, In BB entschieden.
6. **Historie** als Zeitleiste aus `getEntryHistory`, neueste Zeile oben
   (`historyLinesForDisplay`, `ledger-h9tq2`), je Eintrag Satz, Zeitstempel, Urheber.
   Punktfarbe nach Urheber: Teal Import, Violett Regel, Grau YNAB-Spiegel, Green eigene
   Prüfung in BB, Rot für Fehlschläge und Löschung. Als `RemoteData`: Lade- und
   Leerzustand ("Noch keine Einträge") sind Pflicht (Handoff, offener Punkt 12). Im
   Sheet scrollt die Historie.
7. **Gelöschte Buchung ist ein Lesezustand:** Betrag in `text-muted` statt Rot, Felder
   grau, kein Geprüft-Schalter, keine Aktion.
8. Neue DS-Bausteine nach der Tabelle "Neu zu bauende Komponenten" im Handoff
   (Herkunft-Zeile, Historie-Zeitleiste, Hinweisleiste mit Grund und Aktion, Seitenpanel,
   Geprüft-Schalter), je mit Galerie-Beispiel in `/styleguide`. Darstellung ruhig nach
   ADR 0049.

Für die offenen Punkte des Handoffs gilt: Badge-Schreibung im Satzanfang groß ("In YNAB
gelöscht", "Noch nicht in YNAB"), sonst wie im Styleguide; die markierte Zeile bei
offenem Panel nutzt die Fokus-Darstellung aus `ledger-rjb8w`.

## Acceptance criteria
- [ ] Reducer-Test: Tipp bzw. Klick auf eine Zeile öffnet das Detail für genau diese
      Buchung und lädt die Historie (`Loading`, dann `Success`); im Auswahl-Modus öffnet
      ein Tipp kein Detail.
- [ ] Reducer-Test (PC): bei offenem Panel bewegt `↓` die Fokuszeile und das Panel zeigt
      die neue Buchung mit neu geladener Historie; `Esc` schließt; `Enter` auf der
      Fokuszeile öffnet.
- [ ] Reducer-Test (PC): Klick auf Payee- oder Kategorie-Zelle öffnet das Menü aus
      `ledger-rfq5p` und kein Panel; ein Klick sonst auf die Zeile öffnet das Panel.
- [ ] Reducer-Test (Handy): "Nach <Payee> filtern" und "Nach <Kategorie> filtern" im
      Detail setzen denselben Filterzustand wie die Menü-Einträge aus `ledger-rfq5p`;
      Split, Buchung ohne Payee und ohne Kategorie bieten den jeweiligen Eintrag nicht an.
- [ ] Expecto-Test der Spaltenwahl: bei offenem Panel ist Memo in keiner Breite sichtbar,
      die übrigen Spalten folgen `visibleColumns` mit Breite minus 420 px; reicht die
      Restbreite für keine Tabellenstufe, ist die Detail-Form Sheet statt Panel
      (Grenzwerte getestet).
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
      "Kein Split".
- [ ] Ansichts-Test: die Herkunft-Zeile zeigt für jede der vier `LedgerOrigin`-Arten das
      richtige Badge.
- [ ] Ansichts-Test: die Historie erscheint neueste zuerst; die Punktfarbe folgt dem
      Urheber; Lade- und Leerzustand sind vorhanden.
- [ ] Ansichts-Test: eine gelöschte Buchung hat keinen Geprüft-Schalter, keine Aktion und
      einen grauen Betrag.
- [ ] Das alte Historie-Sheet ist entfernt; es gibt keinen zweiten Weg zur Historie.
- [ ] Playwright-Wächter (Handy 390 px und PC 1440 px, gemockte API): Detail öffnet sich,
      am PC bleibt die Liste sichtbar und die Memo-Spalte fehlt bei offenem Panel.
- [ ] Neue DS-Bausteine haben je ein Galerie-Beispiel in `/styleguide`.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.
- [ ] Sheet und Panel lesen sich ruhig, die Hinweisleiste fällt auf, ohne zu schreien
      (ADR 0049). [human-eye]

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Brief: `docs/ux/kassenbuch/briefs.md`,
Ablauf 7.

Sortierung (ADR 0051, Punkt 7): Ändert ein Schreibweg aus dem Detail den Schlüssel der
aktiven Sortierung (etwa Kategorie ändern bei Kategorie-Sortierung), stimmt der
Seitenversatz `LocallyRemoved` nicht mehr; an der Seitengrenze können Buchungen fehlen
oder doppelt erscheinen. Beim Verfeinern festlegen, wie das Detail damit umgeht
(z. B. die geladenen Seiten neu laden).

Verfeinert am 2026-10-19. Entscheidungen Roman: Handy und PC zeigen dieselben Felder (die
PC-Fassung gilt); der Geprüft-Schalter wirkt und "Erneut senden" für den Prüfvermerk
wirkt; der Grund eines gescheiterten Pushs wird als Ereignis festgehalten
(`ledger-rynk5`, eigene Task, Vorbedingung). Die Fokuszeile und `G` kommen aus
`ledger-rjb8w`, deshalb hängt das PC-Panel daran. Die Historie-Reihenfolge ist in
`ledger-h9tq2` entschieden (neueste oben).

Nachgezogen am 2026-10-20 nach den Captures vom selben Tag. Entscheidungen Roman: q1hw2
läuft nach `ledger-rfq5p` (Filter-Menü an Payee/Kategorie) und nach `ledger-qh9qx`
(`visibleColumns`); das Panel nutzt deren Spaltenwahl statt einer eigenen Regel. Kein
weiterer Schnitt. Die Regel "Sheet statt Panel, wenn die Restbreite für keine
Tabellenstufe reicht" ist ein Vorschlag aus der Verfeinerung, Roman beim Bau zeigen.
`ledger-mhe2s` (Prüf-Schaltfläche bei Hover/Fokus) wirkt ohne Zutun auch auf die
Panel-Zeile, weil sie die Fokuszeile ist.

Nicht Teil: Abweichung (Pink, v2), Kategorie- und Split-Bearbeitung (v2), "Erneut senden"
für den Push, "Wiederherstellen" gelöschter Buchungen.

Befund nach dem Deploy vom 2026-10-20 (Roman am PC und am Handy), vor dem nächsten
Verfeinern klären:
- Die Aktionen "Nach <Payee> filtern" und "Nach <Kategorie> filtern" stehen auch am PC
  oben in der Historie. `historySheet` ist am PC und am Handy dasselbe Sheet, und
  `ledger-rfq5p` hat den Block ohne Geräteunterscheidung eingebaut. Am PC ist dieselbe
  Aktion dadurch zweimal erreichbar, einmal über das Zellmenü und einmal über die
  Historie.
- Das Zellmenü am PC ist schwer lesbar: `FilterPopover`-Hülle mit 240 px und `p-4`, die
  Beschriftung wird auf eine Zeile gekürzt ("Nach STUDENTENWERK..."), das Menü deckt die
  Zeile darunter ab. Ein Menü mit einem einzigen Eintrag wirkt wie ein Umweg.
- Roman will die Bedienung der Zeile als Ganzes denken (Zeilenklick, Zellklick,
  Kästchen, Prüf-Schaltfläche, Tasten, Detail, später "Kategorie ändern"), bevor einzeln
  nachgebessert wird. Bestandsaufnahme: `docs/ux/kassenbuch/bedienkonzept-zeile-bestandsaufnahme.md`.
  Diese Aufgabe wird nach der Konzeptrunde neu verfeinert.
