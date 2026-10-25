---
id: ledger-w29y6
title: "Kategorie setzen für die Auswahl: Sammel-Befehl mit einem Sammel-PATCH nach YNAB, Schaltfläche Kategorie setzen in der Auswahl-Leiste mit demselben Picker, Toast mit Zahl der geänderten und übersprungenen Buchungen und einer Rücknahme für alle"
status: backlog
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001, ledger-k2rbn, ledger-w4vte, design-system-gffps, ledger-sfab0]
blocks: []
tags: [frontend, backend, kassenbuch, kategorie, v2, auswahl]
related_adrs: [0050, 0053, 0055]
related_research: []
prior_art: [ledger-nhwrt, ledger-r1d2f, ledger-th36h]
---

## Why
Einstieg für viele Buchungen (ADR 0053, Punkt 2): "Kategorie setzen" in der Auswahl-Leiste,
mit demselben Picker wie im Detail. Muster: "Geprüft für die Auswahl" (`ledger-nhwrt`) für
Auswahl, Sammel-PATCH und Toast; Einzelweg `setEntryCategory` (`ledger-k2rbn`, `ledger-sfab0`)
für Regeln, Fehlschlag und Liste.

## What
1. **Befehl** `LedgerApi.setEntryCategoryBatch: ReviewMarkBatchSelection -> YnabCategoryId option -> Async<Result<CategoryBatchOutcome, string>>`,
   Handler `Ledger.CommandHandlers.setEntryCategoryBatch`, verdrahtet in `Server/Api.fs` über
   einen `categoryBatchPush ()` wie `reviewMarkBatchPush ()`. Die Auswahl löst
   `resolveReviewMarkBatchSelection` auf (ADR 0051, Punkt 3, sortierungsfrei, intern
   `LedgerSort.defaultSort`); der Name der Auswahl-Union und der Auflösung bleibt, ein
   Umbenennen ist nicht Teil dieser Task. Eine unbekannte Ziel-Kategorie (nicht in
   `ledger_categories`) lehnt den ganzen Befehl ab (Fehler, kein Ereignis), wie im Einzelweg.
2. **Je Buchung ein `EntryRecategorized`** (Actor `UserInBb`, Felder wie in
   `setEntryCategory`: `OldSplits`/`NewSplits` leer, `OldPayee = NewPayee`) für jede, deren
   Ziel vom Ist abweicht (`categorizationIdentity`). **Übersprungen** (mit Grund gezählt, kein
   Ereignis, nicht im PATCH): alle Fälle von `categoryNotChangeableReason` (in YNAB gelöscht,
   mit Split-Zeilen, Split in YNAB noch nicht angepasst nach ADR 0058/0059, Übertrag ohne
   Kategorie) und Ziel gleich Ist. Die Prüfung nutzt `categoryNotChangeableReason` selbst,
   keine Kopie. Ohne YNAB-Verknüpfung (`tryYnabTransactionId` = `None`): Ereignis plus
   `EntryCategorizationWriteBackFailed` mit `categoryNotLinkedReason` wie im Einzelweg, nicht
   im PATCH.
3. **Ein Sammel-PATCH.** In `YnabClient` neben dem Prüfvermerk-Sammelweg: `CategoryBatchItem`
   (`YnabTransactionId`, `CategoryId: YnabCategoryId option`), `buildCategoryBatchBody`
   (`{"transactions":[{"id", "category_id"}]}`, `null` für ohne Kategorie),
   `sendCategoryBatch` (injizierbarer `HttpClient`, `PATCH /budgets/{id}/transactions`,
   Fehlerzuordnung und Rückgabe der nicht gespeicherten Ids über
   `savedTransactionIdsDecoder` wie `sendReviewMarkBatch`), `updateCategoryBatch`. Typ
   `CategoryBatchPush` im Handler wie `ReviewMarkBatchPush`. Ohne geänderte verknüpfte
   Buchung geht kein PATCH hinaus. Ein Fehler trifft den ganzen Stapel: je Buchung im PATCH
   `EntryCategorizationWriteBackFailed` mit Baseline über `categoryBaseline` (bei offenem
   Fehlschlag bleibt die erste Baseline) und `describeCategoryPushError`. Von YNAB nicht als
   gespeichert genannte Ids werden einzeln als Fehlschlag geführt, mit eigenem Grund
   `categoryNotSavedReason` (Wortlaut wie `reviewMarkNotSavedReason`). Erfolg bei offenem
   früheren Fehlschlag schreibt je Buchung `EntryCategorizationWrittenBackToYnab` (Regel aus
   `pushCategory`). Der ganze Befehl läuft unter `WriteGuard.exclusive` wie
   `setEntryCategory` (der Prüfvermerk-Sammelweg tut das nicht, hier ist es Pflicht).
4. **Leiste.** `MultiSelect.barDesktop` und `MultiSelect.barMobile` bekommen eine zweite
   Sammelaktion "Kategorie setzen" (Label, Busy, Handler) neben "Geprüft"; `selectionBars` in
   `Components/Ledger/View.fs` reicht sie durch. "Geprüft" bleibt die primäre (orange)
   Aktion, "Kategorie setzen" ist sekundär aus dem Design-System, am Handy wie am PC in einer
   Reihe (Roman, 2026-10-25). Während einer der beiden
   Sammelaufrufe läuft, sind beide gesperrt. Die bestehenden Leistentests
   (`scripts/ledger-selection.test.mjs`: Reihenfolge "Alle, Geprüft, Auswahl aufheben", eine
   Reihe; `LedgerSelectionTests`) werden auf die neue Reihenfolge angepasst; am Handy passt die
   Reihe bei 390 px ohne Überlauf.
5. **Picker.** "Kategorie setzen" öffnet `CategoryPicker` (am PC als Popover an der Leiste,
   am Handy als `CategoryPicker.sheet`) mit eigenem Modellfeld (zum Beispiel
   `BatchCategoryPickerOpen: bool`; `CategoryPickerFor: Guid option` bleibt dem Detail).
   Props: `Suggested = []` (kein Payee), `LoadState` über `categoryLoadState`, `OnRetry` löst
   `LoadCategories` aus, `StandLabel` über `availableAgeLabel`, Öffnen schickt
   `EnsureCategoriesFresh`. Die Auswahl bleibt während der Wahl bestehen; Esc schließt nur
   den Picker (Esc-Kette ADR 0053: Popover, dann Auswahl), bei offenem Picker ruhen die
   Listentasten (`keyboardBlocked`). Nach Erfolg endet die Auswahl samt `SelectionMode`.
   Zwei Lücken im Baustein, die diese Task schließt:
   - `FilterPopover` öffnet nur nach unten (`top-full`); an der festen Leiste unten am
     Bildschirm braucht das Popover eine Variante, die nach oben öffnet.
   - `CategoryPickerProps.Current = None` markiert "Ohne Kategorie" mit Haken. Für eine
     Auswahl braucht der Picker einen Zustand ohne markierte Zeile. Er öffnet immer ohne
     Haken, auch wenn alle gewählten Buchungen dieselbe Kategorie haben (Roman, 2026-10-25).
   - Die Unterzeile des Sheets am Handy lautet "n Buchungen · Summe" (etwa "12 Buchungen ·
     -340,20 €"), analog zu "Payee · Betrag" im Einzelweg (Roman, 2026-10-25).
   Beide Erweiterungen erscheinen in `/styleguide` (Sektion 26) und in
   `standards/frontend/styleguide.md` unter "Auswahl".
6. **Toast:** "n Buchungen nach <Kategorie> verschoben" (bei `None`: Wortlaut für "ohne
   Kategorie") und, falls nötig, ", m übersprungen (Split, Übertrag, gelöscht)", mit Knopf
   "Rückgängig"; Warnung bei Fehlschlägen (Grund wie im Einzelweg). Ohne geänderte Buchung
   ein Toast ohne Aktion.
   **Rücknahme für alle** (Roman, 2026-10-24): `CategoryBatchOutcome` trägt je geänderter
   Buchung Id, vorherige Kategorie (`CategoryChoice option`, wie `CategoryChangeOutcome.Previous`)
   und `CategoryWriteBack`, dazu die Zähler je Überspring-Grund. Bei "Rückgängig" ruft der
   Client den Sammel-Befehl je Gruppe gleicher vorheriger Kategorie auf (ein Sammel-PATCH je
   Gruppe, Auswahl `BatchPicked` mit genau den geänderten Ids); die Historie zeigt beide
   Ereignisse. Eine Aktion, ein Toast, eine Rücknahme. Neue `ToastActionKind`-Variante
   `UndoCategoryBatch` in `src/Client/Types.fs`, Übersetzung in `toastActionMsg`
   (`src/Client/State.fs`).
7. **Liste und Filter** wie im Einzelweg (`ledger-sfab0`), je geladener geänderter Zeile über
   `applyFilterEffect`: eine Zeile, die aus dem aktiven Filter fällt, bleibt in `Entries`
   ausgegraut stehen (`FilteredOut`, "passt nicht mehr zum Filter", `LocallyRemoved` +1,
   `TotalCount` und `TotalAmount` gehen sofort mit). Ändert sich der Schlüssel der aktiven
   Sortierung (`SortByCategory`), setzt der Client `OrderStale` und stößt `RunRefreshTotals`
   an. Unter `BatchAllExcept` kennt der Client nie geladene geänderte Buchungen nicht: bei
   aktivem Filter, der die Kategorie betrifft, holt er Treffer und Summe mit
   `RunRefreshTotals` neu. Die Rücknahme macht ausgegraute Zeilen wieder zu Treffern.
   Available wird nicht fortgeschrieben (zu viele Buchungen, verschiedene Monate); nach dem
   Sammel-Befehl löst der Client `LoadCategories` aus (unbedingt, nicht nur über die
   Zwei-Minuten-Schwelle von `ledger-kwwxz`).

## Acceptance criteria
- [ ] Handler-Test: `BatchPicked` mit vier Buchungen, eine gleich Ziel, eine mit Splits, eine
      mit offenem Split-Fehlschlag (`BaselineSplits` nicht leer): genau ein Ereignis je
      geänderter Buchung, ein einziger Sammel-PATCH mit den geänderten, Antwort zählt
      geändert und übersprungen mit Grund.
- [ ] Handler-Test: `BatchAllExcept` löst den ganzen Treffer ohne Ausnahmen auf, auch nie
      geladene Buchungen (Seitengröße kleiner als der Treffer, wie in
      `LedgerReviewMarkBatchTests`).
- [ ] Handler-Test: Sammel-PATCH scheitert: je geänderter Buchung steht ein Fehlschlag mit
      Baseline im Log, die Kategorien bleiben in BB; bei schon offenem Fehlschlag bleibt die
      erste Baseline; nicht genannte Ids werden einzeln Fehlschlag; nicht verknüpfte Buchung:
      Ereignis plus Fehlschlag, nicht im PATCH.
- [ ] Handler-Test: Erfolg bei offenem Fehlschlag schreibt `EntryCategorizationWrittenBackToYnab`;
      unbekannte Ziel-Kategorie lehnt ohne Ereignis ab; der Befehl läuft unter der
      Schreib-Sperre (Test wird rot ohne `WriteGuard.exclusive`, Muster `LedgerCategoryChangeTests`).
- [ ] JSON-Vertragstest `buildCategoryBatchBody` (`id`, `category_id`, `null` für ohne
      Kategorie) und PATCH-Test mit Fake-`HttpMessageHandler` (Methode, Pfad, Body,
      Fehlerzuordnung, `transaction_ids`).
- [ ] Reducer-Test: "Kategorie setzen" ist nur bei nicht leerer Auswahl wirksam und ruft den
      Befehl mit der Auswahl (`reviewMarkBatchSelection`); Esc bei offenem Picker schließt nur
      den Picker; nach Erfolg endet die Auswahl (auch `SelectionMode`), Zeilen tragen die neue
      Kategorie, `OrderStale` wie im Einzelweg, `LoadCategories` wird ausgelöst.
- [ ] Handler-Test: die Antwort nennt je geänderter Buchung die vorherige Kategorie; ein
      Sammel-Befehl mit diesen Ids und der vorherigen Kategorie stellt den Ausgangsstand her
      (zweites Ereignis je Buchung, ein PATCH je Gruppe), auch zurück zu ohne Kategorie.
- [ ] Reducer-Test: "Rückgängig" im Sammel-Toast ruft den Sammel-Befehl je Gruppe gleicher
      vorheriger Kategorie auf; der Rücknahme-Toast hat keine Aktion.
- [ ] Reducer-Test Filter: Zeilen, die durch den Sammel-Befehl aus dem aktiven Filter fallen,
      bleiben in `Entries` und stehen in `FilteredOut`, `LocallyRemoved` steigt um ihre Zahl,
      `TotalCount` und `TotalAmount` sinken sofort, `OrderStale` bleibt bei Datumssortierung
      aus; unter `AllExcept` mit Kategorie-Filter folgt `RunRefreshTotals`.
- [ ] Ansichts-Test (Playwright, `scripts/ledger-selection.test.mjs`): die Leiste rendert
      beide Sammelaktionen am PC und bei 390 px, in einer Reihe ohne Überlauf; das Popover
      liegt am PC ganz im Bildschirm über der Leiste; der Toast nennt Zahl und übersprungene
      Buchungen.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run guard:tokens`, `npm run test:scripts` grün. Die BC-README ist nachgezogen
      (Befehl neben *PrüfvermerkFürAuswahlSetzen*).
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; die neuen Picker-Varianten (nach oben,
      ohne markierte Zeile) erscheinen in `/styleguide` (`src/Client/Components/Styleguide/View.fs`).
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Notes
Verfeinert am 2026-10-24 (Sammel-Modeling, Ablauf 4): Rücknahme für alle im Toast, Filter-
Nichttreffer ausgegraut statt `OrderStale` (Roman, 2026-10-24). Handoff: `docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md`.

Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (d). Ziel-Lage: backlog (wartet auf
`ledger-w4vte`, `ledger-sfab0`). Dateien: Shared `Domain.fs`/`Api.fs`, `src/Server/Api.fs`,
`YnabClient.fs`, `Ledger/CommandHandlers.fs`, `Ledger/Events.fs` (nur Nutzung), Client
`Ledger/Types.fs`, `State.fs`, `View.fs`, `src/Client/State.fs`. Überschneidung mit
`ledger-k2rbn` (Server), `ledger-sfab0` und `ledger-w4vte` (Client).

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).

Abgleich mit dem gebauten Code (2026-10-25): geprüft gegen `Shared/Domain.fs`, `Shared/Api.fs`,
`YnabClient.fs`, `Ledger/CommandHandlers.fs`, `Server/Api.fs`, Client `Ledger/Types.fs`,
`State.fs`, `View.fs`, `DesignSystem/MultiSelect.fs`, `CategoryPicker.fs`, `FilterPopover.fs`,
`package.json` und die Outcomes von k2rbn, w4vte, gffps, sfab0, kwwxz, wfhxj, nhwrt. Namen
`ReviewMarkBatchSelection`, `resolveReviewMarkBatchSelection`, `BatchPicked`, `BatchAllExcept`,
`LedgerSort.defaultSort`, `EntryRecategorized`, `EntryCategorizationWriteBackFailed`,
`reviewMarkNotSavedReason`, `WriteGuard`, `ToastActionKind`, `LocallyRemoved`, `TotalCount`,
`TotalAmount`, `OrderStale`, `SortByCategory`, `SelectionMode`, `barDesktop`/`barMobile`,
`guard:tokens`, `test:scripts` stimmen. Geändert: `Rows` heißt `Entries` (ausgegraut =
`FilteredOut`); Überspring-Gründe auf `categoryNotChangeableReason` gestützt, dabei der Fall
"Split in YNAB noch nicht angepasst" aus `ledger-kxme4` ergänzt; YnabClient-Namen nach dem
Muster `sendReviewMarkBatch`/`savedTransactionIdsDecoder` konkretisiert (PATCH ist der
Sammelweg, der Einzelweg ist PUT); Schreib-Sperre ausdrücklich, weil `setReviewMarkBatch` sie
nicht nutzt; `WrittenBackToYnab` bei offenem Fehlschlag und erste Baseline ergänzt;
"sobald integriert" beim Token-Wächter gestrichen. Neu als Arbeit: Popover nach oben an der
Leiste, Picker ohne markierte Zeile, eigenes Modellfeld für den Picker, Anpassung der
Leistentests, `RunRefreshTotals` unter `AllExcept`, unbedingtes `LoadCategories` nach dem
Befehl. Zusätzliche Dateien: `src/Client/Types.fs`, `DesignSystem/MultiSelect.fs`,
`CategoryPicker.fs`, `FilterPopover.fs`, `Components/Styleguide/View.fs`,
`scripts/ledger-selection.test.mjs`, `standards/frontend/styleguide.md`.

Entscheidungen am 2026-10-25 (Roman): Handy-Leiste in einer Reihe mit "Geprüft" primär und
"Kategorie setzen" sekundär; Picker der Auswahl öffnet immer ohne Haken; Sheet-Unterzeile
"n Buchungen · Summe".
