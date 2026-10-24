---
id: ledger-sfab0
title: "Kassenbuch-Detail: Pfeil an der Kategorie öffnet den Picker, Änderung über setEntryCategory mit Hinweis alt und neu als Toast, Hinweisleiste Kategorie nicht in YNAB mit Erneut senden, gesperrte Fälle, Liste bleibt stehen mit Hinweisleiste Neu laden"
status: backlog
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001, ledger-k2rbn, design-system-gffps, ledger-kwwxz]
blocks: [ledger-w29y6, ledger-pafkp, ledger-hg5m5]
tags: [frontend, kassenbuch, kategorie, v2, bedienkonzept]
related_adrs: [0050, 0051, 0053, 0055]
related_research: []
prior_art: [ledger-q1hw2, ledger-r777c]
---

## Why
Einstieg für eine Buchung (ADR 0053, Punkt 1): der Pfeil rechts an der Kategorie-Zeile im
Detail öffnet den Picker. `ledger-q1hw2` hat den Platz freigehalten (`detail-category` in
`detailContent`, View.fs).

## What
1. **Pfeil und Picker.** In `detailContent` steht rechts an der Kategorie-Zeile neben dem
   Filter-Knopf ein Pfeil-Knopf. Er öffnet den Picker aus `design-system-gffps` (Popover am PC,
   Sheet am Handy und in der Kompaktliste), gefüllt aus `Model.Categories` mit Available.
   Auch ein Klick auf die Zeile selbst öffnet ihn. Filtern und Ändern bleiben getrennte Ziele.
2. **Auswahl.** Ein Klick im Picker ruft `setEntryCategory` (aus `ledger-k2rbn`) über einen
   neuen Message-Weg (`StartSetEntryCategory`, Zustand je Zeile wie `ReviewMarkRows`).
   Das Detail zeigt die neue Kategorie sofort, die Zeile in der Liste ebenso.
3. **Hinweis alt und neu.** Nach Erfolg ein Toast mit den Zeilen aus `categoryChangeHint`
   (`ledger-kwwxz`); Available der beiden Kategorien wird mit `shiftAvailable`
   fortgeschrieben. Kein "Rückgängig" in dieser Scheibe.
4. **Fehlschlag.** Hinweisleiste im Detail (Muster `reviewMarkHintFor`): "Kategorie nicht in
   YNAB" mit Grund und "Erneut senden" (`retryCategorizationWriteBack`). Der Toast meldet
   bei `CategorySendFailed` eine Warnung.
5. **Gesperrt** (Pfeil sichtbar, aber gedämpft und ohne Wirkung, mit Hinweistext am Knopf):
   in YNAB gelöschte Buchung; Übertrag ohne Kategorie und ohne Split-Zeilen ("Übertrag
   zwischen Budget-Konten, YNAB lässt hier keine Kategorie zu"). Bei einer Split-Buchung
   öffnet der Pfeil den Split-Editor (`ledger-pafkp`); bis dahin ist er dort gesperrt
   mit dem Text "Split bearbeiten folgt".
6. **Liste nach der Änderung** (ADR 0055, Punkt 9): die Zeile bleibt an ihrem Platz. Ändert
   die Änderung den Schlüssel der aktiven Sortierung (`SortByCategory`) oder lässt sie die
   Zeile aus dem aktiven Filter fallen (Kategorie-Filter, Chip "Ohne Kategorie"), setzt das
   Modell `OrderStale`. Dann erscheint über der Liste die Hinweisleiste "Reihenfolge oder
   Treffer haben sich geändert. Neu laden", das Nachladen weiterer Seiten ruht, Treffer und
   Summe werden über die Zählwege neu geladen. "Neu laden" setzt die Liste ab Seite 1
   neu auf (`LocallyRemoved = 0`, Auswahl bleibt leer).
7. **Esc-Kette** (ADR 0053, Punkt 5): Picker zuerst, dann Detail.

## Acceptance criteria
- [ ] Reducer-Test: Pfeil-Klick öffnet den Picker nur bei änderbarer Buchung; bei gelöschter
      Buchung und bei Übertrag ohne Kategorie bleibt er zu.
- [ ] Reducer-Test: Auswahl ruft `setEntryCategory` genau einmal; Antwort `CategoryChanged`
      setzt Kategorie in Liste und Detail, schließt den Picker, löst den Toast aus und
      schreibt Available fort; `CategoryUnchanged` erzeugt keinen Toast.
- [ ] Reducer-Test: `CategorySendFailed reason` setzt `CategorizationWriteBackFailure` auf
      der Zeile und zeigt die Warnung; "Erneut senden" ruft `retryCategorizationWriteBack`
      und räumt den Zustand bei `CategorySent`.
- [ ] Reducer-Test `OrderStale`: bei Sortierung nach Kategorie und bei Kategorie-Filter, bei
      dem die Zeile herausfällt, wird `OrderStale` gesetzt; bei Sortierung nach Datum
      nicht; im Stale-Zustand löst Nachladen keine Seitenanfrage aus; "Neu laden" lädt
      Seite 1 mit der aktiven Sortierung.
- [ ] Ansichts-Test: Pfeil-Knopf rendert in `detail-category`, gesperrt mit Hinweistext in
      den beiden gesperrten Fällen; die Hinweisleiste erscheint mit Grund und Knopf.
- [ ] Test der Esc-Kette: Esc schließt zuerst den Picker, dann das Detail.
- [ ] Playwright oder Ansichts-Test (390 px): Der Picker öffnet als Sheet über dem Detail.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün. Die BC-README ist nachgezogen.
- [ ] Kategorie ändern fühlt sich am Handy so schnell an wie in der YNAB-App. [human-eye]

## Notes
Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (c, Oberfläche). Ziel-Lage: backlog,
bis `ledger-k2rbn`, `ledger-kwwxz` und `design-system-gffps` in done liegen (Abhängigkeiten),
danach todo. Dateien: `src/Client/Components/Ledger/Types.fs`, `State.fs`, `View.fs`,
`src/Client/State.fs` (Toast-Weg wie `StartUndoReviewMark`), Tests. Überschneidung mit
`ledger-w4vte` (Esc-Kette, `keyboardBlocked`), `ledger-kwwxz` und `ledger-w29y6` und `-6`
in denselben drei Ledger-Client-Dateien: nicht parallel in einer Welle bauen.
