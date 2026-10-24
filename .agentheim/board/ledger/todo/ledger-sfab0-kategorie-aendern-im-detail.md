---
id: ledger-sfab0
title: "Kassenbuch-Detail: Pfeil an der Kategorie öffnet den Picker, Änderung über setEntryCategory mit Karte Budget nach dem Wechsel und Toast mit Rückgängig, Vorschläge zum Payee, Hinweisleiste Kategorie nicht in YNAB mit Erneut senden, gesperrte Fälle, Zeile bleibt stehen (ausgegraut bei Filter-Nichttreffer, Hinweisleiste Neu laden bei Sortierschlüssel)"
status: todo
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
`detailContent`, View.fs). Gestaltung: Tafel Ablauf 4, Screens 4.2 bis 4.4, 4.9, 4.10 (`docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md`).

## What
1. **Pfeil und Picker.** In `detailContent` steht rechts an der Kategorie-Zeile neben dem
   Filter-Knopf ein Pfeil-Knopf. Er öffnet den Picker aus `design-system-gffps` (Popover am PC,
   Sheet am Handy und in der Kompaktliste), gefüllt aus `Model.Categories` mit Available.
   Auch ein Klick auf die Zeile selbst öffnet ihn. Filtern und Ändern bleiben getrennte Ziele.
2. **Auswahl.** Ein Klick im Picker ruft `setEntryCategory` (aus `ledger-k2rbn`) über einen
   neuen Message-Weg (`StartSetEntryCategory`, Zustand je Zeile wie `ReviewMarkRows`).
   Das Detail zeigt die neue Kategorie sofort, die Zeile in der Liste ebenso.
3. **Karte, Toast und Rückgängig** (Roman, 2026-10-24). Nach Erfolg (`CategoryChanged`):
   - Available der beiden Kategorien wird mit `shiftAvailable` fortgeschrieben.
   - Die Karte "Budget nach dem Wechsel" (`CategoryChangeCard`, `design-system-gffps`) zeigt
     die Zeilen aus `categoryChangeHint` (`ledger-kwwxz`): alter Wert durchgestrichen, Pfeil,
     neuer Wert, für beide Kategorien. Ort: am PC an der Stelle des Popovers bzw. im Panel unter
     dem Kategorie-Feld, am Handy unter dem Kategorie-Feld im Detail-Sheet. Sichtbar etwa 6
     Sekunden, schließt früher bei Esc, bei Wechsel oder Schließen des Details und bei der
     nächsten Änderung; der Toast trägt sie nicht.
   - Der Toast sagt "Kategorie geändert: <Name>" mit Knopf "Rückgängig" (ohne Kategorie:
     "Kategorie entfernt: <alter Name>"). Neue `ToastActionKind`-Variante (`UndoCategory`
     mit Buchungs-Id und `Previous` aus `CategoryChangeOutcome`), übersetzt in
     `toastActionMsg` wie `UndoReviewMark`.
   - "Rückgängig" setzt den alten Stand über denselben Schreibweg zurück
     (`setEntryCategory` mit `Previous |> Option.map _.CategoryId`): zweites `EntryRecategorized`, zweiter Push nach
     YNAB, die Historie zeigt beide. Available wird mit vertauschten Kategorien fortgeschrieben,
     ohne neue Karte; der Rücknahme-Toast hat kein "Rückgängig". Eine Aktion, ein Toast, eine
     Rücknahme.
   - `CategoryUnchanged` erzeugt weder Karte noch Toast.
4. **Vorschläge zum Payee.** Der Picker zeigt oben VORGESCHLAGEN (`design-system-gffps`):
   Kategorien, die der Payee der Buchung im Kassenbuch bisher hatte, höchstens drei, häufigste
   zuerst, ohne die aktuelle Kategorie. Die Berechnung liegt auf dem Server, weil der Client
   nur die geladenen Seiten kennt: neue Abfrage
   `getPayeeCategorySuggestions: string -> YnabCategoryId option -> Async<YnabCategoryId list>`
   (Payee, auszuschließende aktuelle Kategorie). SQL über die Projektion: Buchungen mit
   genau diesem Payee (ohne Beachtung der Groß/Kleinschreibung, invariant), nicht in YNAB
   gelöscht, `category_id` nicht null, `GROUP BY category_id`, Anzahl absteigend, bei Gleichstand
   die jüngste Buchung zuerst, `LIMIT 3`. Dateien: `Shared/Api.fs`, `Server/Api.fs`,
   `Ledger/QueryHandlers.fs`, `Ledger/Persistence.fs` (Muster `loadPayeeSuggestions`). Der Client
   lädt beim Öffnen des Pickers (ein Aufruf je Öffnung); bis die Antwort da ist, fehlt der
   Abschnitt, ein Fehler lässt ihn weg. Ohne Payee kein Abschnitt.
5. **Fehlschlag.** Hinweisleiste im Detail (Muster `reviewMarkHintFor`): "Kategorie nicht in
   YNAB" mit Grund und "Erneut senden" (`retryCategorizationWriteBack`). Der Toast meldet
   bei `CategorySendFailed` eine Warnung.
6. **Gesperrt** (Pfeil sichtbar, aber gedämpft und ohne Wirkung, mit Hinweistext am Knopf):
   in YNAB gelöschte Buchung; Übertrag ohne Kategorie und ohne Split-Zeilen ("Übertrag
   zwischen Budget-Konten, YNAB lässt hier keine Kategorie zu"). Bei einer Split-Buchung
   öffnet der Pfeil den Split-Editor (`ledger-pafkp`); bis dahin ist er dort gesperrt
   mit dem Text "Split bearbeiten folgt".
7. **Liste nach der Änderung** (ADR 0055, Punkt 9 mit Nachtrag vom 2026-10-24; Tafel Ablauf 5,
   Screen 5.8): die Zeile bleibt an ihrem Platz.
   - **Fällt die Zeile aus dem aktiven Filter** (Kategorie-Filter, Chip "Ohne Kategorie"),
     bleibt sie ausgegraut stehen, mit dem Vermerk "passt nicht mehr zum Filter". Trefferzahl
     und Summe gehen sofort mit (lokal um die Zeile vermindert, danach über die Zählwege
     bestätigt). `LocallyRemoved` passt dafür: die Zeile bleibt gerendert, fehlt aber im
     Server-Ergebnis, also zählt der Versatz sie wie eine entfernte Zeile (`LocallyRemoved + 1`),
     und das Nachladen weiterer Seiten läuft unverändert weiter. Die ausgegraute Zeile ist
     nicht auswählbar (auch nicht über "alle auswählen"), ihr Detail bleibt bedienbar, damit
     "Rückgängig" und eine erneute Wahl möglich sind. Macht die Rücknahme die Zeile wieder zum
     Treffer, verschwindet der Vermerk und `LocallyRemoved` sinkt um eins. Die ausgegraute Zeile
     verschwindet bei Neuladen, Filter- oder Sortierwechsel.
   - **Ändert die Änderung den Schlüssel der aktiven Sortierung** (`SortByCategory`),
     setzt das Modell `OrderStale` (auch wenn die Zeile zugleich aus dem Filter fällt). Dann
     erscheint über der Liste die Hinweisleiste "Reihenfolge oder Treffer haben sich geändert.
     Neu laden", das Nachladen weiterer Seiten ruht, Treffer und Summe werden über die Zählwege
     neu geladen. `OrderStale` bleibt gesetzt, bis "Neu laden" die Liste ab Seite 1 neu aufsetzt
     (`LocallyRemoved = 0`, Auswahl bleibt leer); eine Rücknahme löscht es nicht.
   - Bei Sortierung nach Datum, Betrag, Payee usw. und ohne Filterwirkung ändert sich nichts
     an der Liste.
8. **Esc-Kette** (ADR 0053, Punkt 5): Picker zuerst, dann Detail.

## Acceptance criteria
- [ ] Reducer-Test: Pfeil-Klick öffnet den Picker nur bei änderbarer Buchung; bei gelöschter
      Buchung und bei Übertrag ohne Kategorie bleibt er zu.
- [ ] Reducer-Test: Auswahl ruft `setEntryCategory` genau einmal; Antwort `CategoryChanged`
      setzt Kategorie in Liste und Detail, schließt den Picker, setzt die Karte und den Toast
      "Kategorie geändert: <Name>" mit Aktion `UndoCategory` und schreibt Available fort;
      `CategoryUnchanged` erzeugt weder Karte noch Toast.
- [ ] Test `toastActionMsg (UndoCategory ...)` und Reducer: "Rückgängig" ruft `setEntryCategory`
      mit `Previous |> Option.map _.CategoryId` (auch `None` für "ohne Kategorie"), die Zeile trägt danach wieder die
      alte Kategorie, Available ist mit vertauschten Kategorien fortgeschrieben, der
      Rücknahme-Toast hat keine Aktion.
- [ ] Ansichts-Test Karte: erscheint nach der Änderung mit durchgestrichenem alten und neuem
      Wert für beide Kategorien, am PC im Panel unter dem Kategorie-Feld, bei 390 px unter dem
      Kategorie-Feld im Detail-Sheet; verschwindet nach Ablauf, bei Esc und beim Schließen des
      Details; der Toast-Text enthält keine Beträge.
- [ ] Handler- und Persistence-Test `getPayeeCategorySuggestions`: höchstens drei, häufigste
      zuerst, Gleichstand jüngste zuerst, aktuelle Kategorie ausgeschlossen, in YNAB gelöschte
      und kategorielose Buchungen zählen nicht, Payee ohne Beachtung der Groß/Kleinschreibung.
- [ ] Reducer-Test: Öffnen des Pickers lädt die Vorschläge genau einmal; Fehler oder leere
      Antwort lassen den Abschnitt weg.
- [ ] Reducer-Test: Öffnen des Pickers schickt `EnsureCategoriesFresh` (`ledger-kwwxz`, lädt
      nur bei Stand über zwei Minuten); die Fußzeile `StandLabel` kommt aus `availableAgeLabel`
      mit `CategoriesLoadedAt` und fehlt, solange `CategoriesLoadedAt` leer ist.
- [ ] Reducer-Test: `WriteBack = CategorySendFailed reason` setzt `CategorizationWriteBackFailure` auf
      der Zeile und zeigt die Warnung; "Erneut senden" ruft `retryCategorizationWriteBack`
      und räumt den Zustand bei `CategorySent`.
- [ ] Reducer-Test Filter-Nichttreffer: Fällt die Zeile bei Kategorie-Filter (oder Chip "Ohne
      Kategorie") heraus und die Sortierung ist nach Datum, bleibt sie in `Rows`, trägt das
      Merkmal "passt nicht mehr zum Filter", `LocallyRemoved` steigt um eins, `TotalCount` sinkt
      um eins und `TotalAmount` um ihren Betrag, `OrderStale` bleibt aus; die nächste
      Seitenanfrage nutzt den erhöhten Versatz. Rücknahme macht die Zeile wieder zum Treffer
      (Merkmal weg, `LocallyRemoved` zurück, Zähler zurück).
- [ ] Reducer-Test `OrderStale`: bei `SortByCategory` wird `OrderStale` gesetzt; bei Sortierung
      nach Datum und ohne Filterwirkung nicht; im Stale-Zustand löst Nachladen keine
      Seitenanfrage aus; "Neu laden" lädt Seite 1 mit der aktiven Sortierung; die
      ausgegraute Zeile ist von "alle auswählen" ausgenommen.
- [ ] Ansichts-Test: die ausgegraute Zeile rendert mit gedämpfter Klasse und dem Vermerk
      "passt nicht mehr zum Filter"; die Hinweisleiste "Neu laden" erscheint nur bei
      `OrderStale`.
- [ ] Ansichts-Test: Pfeil-Knopf rendert in `detail-category`, gesperrt mit Hinweistext in
      den beiden gesperrten Fällen; die Hinweisleiste erscheint mit Grund und Knopf.
- [ ] Test der Esc-Kette: Esc schließt zuerst den Picker, dann das Detail.
- [ ] Playwright oder Ansichts-Test (390 px): Der Picker öffnet als Sheet über dem Detail.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün. Die BC-README ist nachgezogen.
- [ ] Kategorie ändern fühlt sich am Handy so schnell an wie in der YNAB-App. [human-eye]
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; ein neuer DS-Baustein erscheint in
      `/styleguide` (`src/Client/Components/Styleguide/View.fs`); `npm run guard:tokens` grün
      (Wächter aus `design-system-wfhxj`).
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Manuelle Prüfung
Von `ledger-k2rbn` übernommen (dort ohne Oberfläche nicht von Hand prüfbar). Roman prüft nach
dem Deploy; der Verifier bewertet diese Punkte nicht.
- Kategorie im Detail ändern, während YNAB nicht erreichbar ist (z. B. Token in den
  Einstellungen ungültig machen), danach einen Spiegel-Lauf starten: Die BB-Kategorie bleibt,
  die Hinweisleiste "Kategorie nicht in YNAB" bleibt stehen. YNAB wieder erreichbar machen,
  "Erneut senden": Die Hinweisleiste verschwindet, YNAB zeigt die neue Kategorie.
- Während eines laufenden Spiegel-Laufs eine Kategorie ändern: Die Antwort kommt erst nach
  dem Lauf, die neue Kategorie bleibt stehen und wird nicht vom Spiegel zurückgedreht.
- Einen Fehlschlag wie im ersten Punkt erzeugen, dann den Container neu starten: Die
  Hinweisleiste ist danach noch da. Die Spalte `categorization_write_back_failure` ist in der
  Live-Datenbank vorhanden (am 2026-10-24 lesend geprüft, 14159 Zeilen, kein Fehlschlag).

## Notes
Verfeinert am 2026-10-24 (Sammel-Modeling, Ablauf 4; Entscheidungen Roman, 2026-10-24): Rückgängig
im Toast, Budget-Hinweis als Karte statt im Toast, Vorschläge zum Payee mit Server-Abfrage,
Filter-Nichttreffer ausgegraut statt `OrderStale`. Handoff: `docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md`. Die Server-Abfrage
für Vorschläge bleibt hier (klein, eine SQL-Gruppierung); wird die Task beim Bau zu groß, ist
sie als eigener Task abspaltbar. Zu klären beim Bau: Dauer der Karte (Annahme 6 Sekunden).

Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (c, Oberfläche). Ziel-Lage: backlog,
bis `ledger-k2rbn`, `ledger-kwwxz` und `design-system-gffps` in done liegen (Abhängigkeiten),
danach todo. Dateien: `src/Client/Components/Ledger/Types.fs`, `State.fs`, `View.fs`,
`src/Client/State.fs` (Toast-Weg wie `StartUndoReviewMark`, `toastActionMsg`), `src/Shared/Api.fs`,
`src/Server/Api.fs`, `Ledger/QueryHandlers.fs`, `Ledger/Persistence.fs` (Vorschläge), Tests. Überschneidung mit
`ledger-w4vte` (Esc-Kette, `keyboardBlocked`), `ledger-kwwxz` und `ledger-w29y6`
in denselben drei Ledger-Client-Dateien: nicht parallel in einer Welle bauen.

Abgleich mit dem gebauten Stand (2026-10-24, nach k2rbn, kwwxz, gffps, w4vte):
- `setEntryCategory: Guid -> YnabCategoryId option -> Async<Result<CategoryChangeOutcome, string>>`.
  `CategoryChangeOutcome` ist ein Record: `Previous: CategoryChoice option` (`CategoryId`,
  `CategoryName`), `Change: CategoryChanged | CategoryUnchanged`, `WriteBack: CategoryNotSent
  | CategorySent | CategorySendFailed of reason`. "Kategorie geändert: <Name>" und "Kategorie
  entfernt: <alter Name>" bilden sich aus `Previous.CategoryName` und `Model.Categories`.
- `retryCategorizationWriteBack: Guid -> Async<Result<CategoryWriteBack, string>>`. Der offene
  Fehlschlag steht als `CategorizationWriteBackFailure: string option` auf `LedgerEntryView`.
  Ereignis des Befehls: `EntryRecategorized`.
- Picker: `CategoryPicker.popover` (PC) und `CategoryPicker.sheetLayered` (Handy, über dem
  Detail-Sheet, nutzt `BottomSheet.viewLayered`), Props `CategoryPickerProps` mit
  `Categories`, `Current`, `Suggested`, `StandLabel`, `OnSelect`, `OnClose`. Der Baustein
  kürzt die Vorschläge selbst auf drei und lässt die aktuelle Kategorie weg.
- Karte: `CategoryPicker.CategoryChangeCard.view` nimmt `ChangeLine list`
  (`Label`, `OldText`, `NewText`); `categoryChangeHint` liefert `CategoryChangeHintRow` mit
  denselben Feldern, eine Abbildung genügt. `CategoryChangeCard.hasContent` entscheidet, ob
  die Karte erscheint.
- `shiftAvailable`, `categoryChangeHint`, `availableAgeLabel`, `categoriesReloadDue` und
  die Message `EnsureCategoriesFresh` liegen in `src/Client/Components/Ledger/Types.fs`
  bzw. `src/Client/State.fs`.

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).
