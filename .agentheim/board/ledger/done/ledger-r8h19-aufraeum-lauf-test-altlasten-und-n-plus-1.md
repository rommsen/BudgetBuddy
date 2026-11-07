---
id: ledger-r8h19
title: "Aufräum-Lauf ledger: Quelltext-, Klassenstring- und Konstanten-Tests ersetzen oder löschen, Logik aus [layout]-Tests nach .NET, N+1 in Sammelbefehlen und Spiegel-Lauf"
status: done
type: chore
context: ledger
created: 2026-11-07
completed:
depends_on: [ledger-kfd5m]
blocks: []
tags: [ledger, tests, doktrin, aufraeumen, n-plus-1, query-safety, bestandsreview]
related_adrs: [0050, 0055, 0014, 0058]
related_research: []
prior_art: [infrastructure-hdf7j, infrastructure-phadg]
---

## Why
Bestandsreview ledger vom 2026-11-06, Gruppe 7 (`.agentheim/knowledge/reviews/2026-11-06-ledger.md`, Abschnitt 7). Roman hat am 2026-11-07 die Option "Eigener Aufräum-Lauf für Tests und N+1" gewählt, als eine Task.

Was heute passiert:
- **Tests, die Quelltext lesen statt Verhalten zu prüfen.** Viele Kassenbuch-Tests suchen Teilstrings in `View.fs` oder `State.fs`, vergleichen Tailwind-Klassen oder prüfen Konstanten (Doktrin Punkt 6 und 7). Eine harmlose Umbenennung färbt ein Dutzend Tests rot, ein echter Fehler mit gleichem Text bleibt grün.
- **Logik in Browser-Tests.** Zwei `[layout]`-Tests prüfen nebenbei, welcher Text oder welche Trefferzahl erscheint (Doktrin Punkt 5). Das gehört in Client-`update` oder eine reine Funktion.
- **N+1.** Setzt Roman für 200 Buchungen den Prüfvermerk, lädt der Server jede Buchung und ihre YNAB-Verknüpfung einzeln. Der Spiegel-Lauf löst jede Beobachtung mit bis zu vier Einzelabfragen auf. Beides dauert bei großen Mengen spürbar länger als nötig (`.claude/rules/query-safety.md`, "Kein N+1").

## What
Am Code vom 2026-11-07 (main 7423fb0) geprüft; die Zeilen stimmen mit dem Review überein.

### A. Test-Altlasten (`src/Tests/`, Zeilen sind Testanfänge)
Je Test eine der drei Entscheidungen: **löschen** (Verhalten anderswo abgedeckt oder keine Aussage), **heben** (Entscheidung als reine Funktion im Produktionscode, Test daran) oder **behalten mit Begründung** (repo-weite Invariante nach Doktrin Punkt 7, mit Gegenbeweis).

| Datei | Stellen | Vorschlag aus dem Review |
|---|---|---|
| `LedgerRetryPushTests.fs` | :119 | löschen, keine Gegenprobe möglich (Lauf kennt keinen Schreibaufruf) |
| `LedgerViewTests.fs` | :551 | löschen, die 128 px misst `scripts/ledger-status-column.test.mjs:324` |
| `LedgerViewTests.fs` | :474, :512, :525, :563, :574, :585, :592, :612, :626 | Funktionsrumpf-Wächter löschen oder Entscheidung in reine Funktion |
| `LedgerViewTests.fs` | :850 bis :921, :1227 bis :1313 | Quelltext-Wächter löschen |
| `LedgerSelectionTests.fs` | :298, :309, :332, :356, :368; :304 | Umschaltung als reine Funktion, Quelltext-Tests löschen (:309 ist veraltet); :304 prüft Label-Konstanten, löschen |
| `LedgerKeyboardTests.fs` | :387, :397; :408 | Zweige in reine Funktion; :408 (44 px als Klassenstring) löschen |
| `LedgerCompactInteractionTests.fs` | :91, :98, :104 | löschen, anderweitig abgedeckt |
| `LedgerReviewButtonVisibilityTests.fs` | :21 bis :50, :51, :60 | Klassenstring- und Teilstring-Tests löschen |
| `LedgerNotInYnabClientTests.fs` | :57 | Teilstring-Zählung löschen |
| `LedgerCategoriesAwaitSettingsTests.fs` | :106 | `getCategories` im Root-`update` (`src/Client/State.fs`) hinter eine injizierte Funktion, am Reducer prüfen statt `State.fs` per Teilstring |
| `LedgerPageTests.fs` | :3044 | löschen, den Verhaltenstest der Payee-Vorschläge (`loadPayeeSuggestions`, Faltung über `Core.searchFold`) um einen Umlaut-Fall ergänzen |
| `LedgerSortSheetTests.fs` | :123, :128, :135 | init-Konstante und Quelltext-Tests löschen |
| `LedgerDetailTests.fs` :604, `LedgerSplitEditorClientTests.fs` :705, `LedgerCategoryBatchClientTests.fs` :365, `LedgerMemoClientTests.fs` :353, `LedgerSortClientTests.fs` :211 | | "Baustein steht im Styleguide"-Tests löschen |
| `LedgerSplitEditorClientTests.fs` | :688, :694, :710 | Quelltext-Wächter und 44 px als Klassenstring löschen |
| `LedgerRecentCategoriesTests.fs` :76, `LedgerLenaFlagClientTests.fs` :166, `LedgerReviewMarkClientTests.fs` :158, :310, `LedgerMemoClientTests.fs` :337, `LedgerLenaFlagBatchClientTests.fs` :274, :292, `LedgerReviewMarkBatchClientTests.fs` :259, :279, `LedgerVisibleColumnsTests.fs` :109, `LedgerAccountChoiceTests.fs` :94 | | Verdrahtung per Teilstring löschen |

Dazu aus `infrastructure-phadg` (Abschnitt "Aufgenommen aus infrastructure-q7v2m") die übrigen Quelltext-Tests in `LedgerCategoryChangeClientTests.fs` und `LedgerFilterButtonTests.fs`, damit keine Kassenbuch-Testdatei auf zwei Tasks verteilt ist (siehe Notes).

### B. Logik aus `[layout]`-Browser-Tests nach .NET
- `scripts/ledger-sort-chip.test.mjs:80`: Die Sortier-Logik steht schon in `LedgerSortSheetTests.fs`; im `[layout]`-Test bleiben nur Lage-Aussagen (Chip am Ende der Leiste, Sheet am Handy).
- `scripts/ledger-filter-bar.test.mjs:229`: "Tippen außerhalb filtert nicht", Chip-Text und Trefferzahl wandern in Client-`update`-Tests unter .NET; im `[layout]`-Test bleibt das BottomSheet am Handy.

### C. N+1 (`src/Server/Ledger/CommandHandlers.fs`, `src/Server/Ledger/Persistence.fs`)
- Sammelbefehle laden je Buchung einzeln: `changeReviewMarkBatch` (:1375 `loadEntry`, :1385 `tryYnabTransactionId`), `setEntryCategoryBatch` (:1699, :1734), `changeFlagBatch` (:2582). Neu: Buchungen und Verknüpfungen einer Auswahl je einmal laden (`IN @ids`, in Stücken wie `Persistence.fs:1878` mit `List.chunkBySize 400`).
- Spiegel-Lauf (`runMirror`, Schleife ab :435): je Beobachtung `resolveEntry` (:445, bis zu zwei `findEntryByLink`), `loadEntry` (:468) und `isSupersededDeletion` (:473). Neu: Verknüpfungen (YNAB-Ids und ImportIds) und Buchungen des Batches einmal laden. Was ein Schritt im selben Lauf ändert (neue Buchung, Übertrags-Gegenbuchung, ersetzte Id), muss ein späterer Schritt sehen wie heute.
- Neue Row-Typen mit `[<CLIMutable>]`, Optionsfelder nur mit registriertem `OptionHandler<'T>`.

## Acceptance criteria
- [x] Für jeden Test aus der Tabelle in A und jeden Quelltext-Test in `LedgerCategoryChangeClientTests.fs` und `LedgerFilterButtonTests.fs` ist entschieden: gelöscht, gehoben oder mit Begründung behalten. Das Outcome hat eine Tabelle je Test: Entscheidung und der Test (Name und Datei), der das Verhalten heute abdeckt, oder "keine Aussage (Konstante, Klassenstring, Quelltext)" mit Doktrin-Punkt.
- [x] Jede gehobene Entscheidung (mindestens `LedgerSelectionTests.fs` Umschaltung, `LedgerKeyboardTests.fs` :387 und :397, `LedgerCategoriesAwaitSettingsTests.fs` :106) ist eine reine Funktion im Produktionscode, die View oder `update` nur aufruft; je Funktion Tests für alle Zweige und im Outcome eine Mutante, die den neuen Test rot macht.
- [x] Der Root-`update` ruft `getCategories` über eine injizierte Funktion; der Ersatz für :106 prüft am Reducer, dass mit Settings `Success` und Budget der Abruf startet und das Warten endet (Cmd über Lambda, Doktrin Punkt 1).
- [x] Ein Verhaltenstest der Payee-Vorschläge deckt einen Umlaut-Fall ab (erfundene Payees, die sich nur in Groß- und Kleinschreibung eines Umlauts unterscheiden, werden zusammengefasst); er wird rot, wenn `loadPayeeSuggestions` eine eigene Faltung statt `Core.searchFold` nutzt, die Umlaute anders behandelt.
- [x] Nach dem Lauf liest kein Test in den genannten Dateien Client-Quelltext per Teilstring, außer den begründet behaltenen; keine Tailwind-Größe als Klassenstring in diesen Dateien.
- [x] `scripts/ledger-sort-chip.test.mjs` und `scripts/ledger-filter-bar.test.mjs`: die `[layout]`-Tests treffen nur noch Lage-Aussagen; die herausgenommene Logik hat je einen .NET-Test (Client-`update` oder reine Funktion), der vor dem Entfernen aus dem Browser-Test existiert und grün ist.
- [x] Die Meldung "Browser-Tests je Kennung" aus `npm run test:scripts` steht vorher und nachher im Outcome; keine Kennung wächst.
- [x] Sammelbefehle: `changeReviewMarkBatch`, `setEntryCategoryBatch` und `changeFlagBatch` laden Buchungen und Verknüpfungen je Auswahl einmal (bis auf Stücke zu 400). Die bestehenden Tests in `LedgerReviewMarkBatchTests.fs`, `LedgerCategoryBatchTests.fs` und `LedgerLenaFlagBatchTests.fs` bleiben ohne inhaltliche Änderung grün, die Sperr-Tests aus ledger-kfd5m ebenso.
- [x] Neue Lesefunktionen in `Ledger.Persistence` haben Handler-Tests mit In-Memory-SQLite: leere Liste, mehr als 400 Ids (Stückgrenze), unbekannte Id fehlt im Ergebnis.
- [x] Spiegel-Lauf: Vor dem Umbau ein Charakterisierungstest, der für einen Batch mit neuer Buchung, Treffer über YNAB-Id, Treffer über ImportId, Übertragspaar im selben Batch und ersetzter Split-Id (ADR 0058 Punkt 5) die entstehenden Ereignisse festhält; er ist vor und nach dem Umbau grün. `LedgerMirrorTests.fs` bleibt ohne inhaltliche Änderung grün.
- [x] Das Outcome nennt die gemessene Zahl der Datenbankabfragen für einen Sammelbefehl mit 200 Buchungen und einen Spiegel-Lauf mit 200 Beobachtungen, vor und nach dem Umbau (gemessen, nicht geschätzt; kein Strukturtest dafür).
- [x] Neue Row-Typen tragen `[<CLIMutable>]`; neue Optionstypen haben einen registrierten `OptionHandler<'T>`.
- [x] `./verify.sh` zeigt `VERIFY: PASS`.

## Nicht dazu
- Aus Gruppe 7 bleiben in der Kann-Sammlung (`ledger-k4n2s`, Punkt 2): Benennungen (`addCashExpense`, `reviewMarkEvent`, `SetSortExactly`/`SetSort`, `BotCommitResult`), Bool-Ladeflags (`LoadingMore`, `MirrorRunning`, `CategoriesRefreshing`), Elmish-Muster (`Run…`-Zweige mit Abfragebau nach ADR 0020, `RunLoadTransferTargets`, Kartentimer mit `Async.Sleep`, toter innerer `LoadCategories`-Zweig, `prop.key` nach Konto-Id), der stille Default in `rowToEntry` (`Persistence.fs:576`) und die Fachbegriff-Texte ("Kategorie und/oder Transfer", "Noch keine Einträge").
- Neue Tests für bisher ungetestetes Verhalten, außer als Ersatz für einen gelöschten oder gehobenen Test.
- Der Wächter gegen Tailwind-Größen in .NET-Tests (design-system-fpem2 in infrastructure-phadg) und alle Nicht-Kassenbuch-Dateien aus infrastructure-phadg, auch `CategoryPickerTests.fs` (dort Abschnitt q7r4m), obwohl der Review dort :410 nennt.
- Verhaltensänderungen am Kassenbuch. Sichtbar ändert sich nichts.

## Notes
- **Überschneidung mit infrastructure-phadg.** Der Abschnitt q7v2m dort nennt `LedgerViewTests.fs`, `LedgerDetailTests.fs`, `LedgerSplitEditorClientTests.fs`, `LedgerCategoryChangeClientTests.fs`, `LedgerFilterButtonTests.fs` und `LedgerKeyboardTests.fs`. Diese Task übernimmt die Kassenbuch-Dateien daraus vollständig; beim Refinement von phadg den Abschnitt q7v2m auf die übrigen Dateien kürzen oder streichen. Aus fpem2 fallen `LedgerSortSheetTests.fs:138` und `LedgerViewTests.fs:1257` hier mit weg; die Bestandsliste des Wächters wird dann kürzer.
- **Überschneidung mit anderen ledger-Tasks.** ledger-vq2g8 ersetzt in `LedgerViewTests.fs` den Test zu `buildAccountCards` und fasst `LedgerCategoriesAwaitSettingsTests.fs` an, ledger-bk3q8 ergänzt `LedgerRetryPushTests.fs`. Wer später läuft, gleicht ab. Abhängig nur von ledger-kfd5m, weil die Sammelbefehle dort unter die Sperre kommen.
- Risiko laut Review: beim Löschen eine echte Absicherung mitnehmen. Deshalb die Tabelle im Outcome mit dem abdeckenden Test je Löschung. Fehlt eine Abdeckung, wird gehoben statt gelöscht.
- Bewusst groß. Lässt sich bei Bedarf in A und B (Tests) und C (N+1) teilen; die Kriterien sind danach getrennt.

## Outcome

Aufräum-Lauf ledger: Quelltext-, Klassenstring- und Konstanten-Tests der Kassenbuch-Testdateien sind gelöscht oder auf reine Funktionen gehoben, die zwei `[layout]`-Tests treffen nur noch Lage-Aussagen, Sammelbefehle und Spiegel-Lauf laden ihre Buchungen und Verknüpfungen einmal statt je Element. Sichtbar ändert sich nichts.

Wichtige Dateien:
- Produktionscode, neue reine Funktionen: `src/Client/Components/Ledger/View.fs` (`payeeAccountAbbreviation`, `tableHeaderFor`, `sortChipVisible`, `summaryRowKind`, `mobileRowMode`, `mobileRowLongPress`, `pointerRowReviewMarkSlot`, `touchRowReviewMarkSlot`, `detailReadOnly`, `tableStatusMarks`; `chipLabel` jetzt öffentlich), `src/Client/Components/Ledger/State.fs` (`sortChipActive`), `src/Client/State.fs` (`UpdatePorts`, `updateWith`, `livePorts`; `update = updateWith livePorts`).
- N+1: `src/Server/Ledger/Persistence.fs` (`loadEntries`, `linksForEntries`, `findEntriesByLinks`, Stücke zu 400), `src/Server/Ledger/CommandHandlers.fs` (`ynabTransactionIdIn`, `MirrorBatchState`; `changeReviewMarkBatch`, `setEntryCategoryBatch`, `changeFlagBatch`, `runMirror`, `resolveEntry`, `linkTransferCounterpart`, `linkSplitTransferCounterparts`, `isSupersededDeletion`).
- Neue Tests: `src/Tests/LedgerMirrorBatchTests.fs` (Charakterisierung), `src/Tests/LedgerBatchReadTests.fs` (Lesefunktionen).
- Browser: `scripts/ledger-sort-chip.test.mjs`, `scripts/ledger-filter-bar.test.mjs`.
- Mitgezogen: `src/Tests/RetryTests.fs` (Inventar von `src/Client/State.fs`: beide Kategorien-Abrufe laufen jetzt über den einen Port, ein `Retry.reading` statt zwei), `src/Tests/PwaTests.fs` (zwei Fälle für `isInputActive`).

Live-Port-Stellen (Doktrin Punkt 3): `GetCategories` schließt nur `livePorts` in `src/Client/State.fs` an (`Retry.reading Api.ynab.getCategories`), `App.fs` nutzt weiter `State.update`. Belegt durch das Inventar in `RetryTests` ("die Inventare der lesenden Abfragen laufen über den Helfer, kein Schreibaufruf", genau ein `getCategories` in `State.fs`) und den Ablauf im Browser (`[flow:kategorie-aendern]` lädt die Kategorien über diesen Weg). Der Regeln-Zweig (`RulesMsg LoadCategories`) nutzt denselben Port, Verhalten unverändert.

### A. Entscheidung je Test

Zeilennummern der Task stimmen bis auf `LedgerViewTests.fs` ab :1227 (dort um etwa 20 Zeilen verschoben, ledger-vq2g8) mit dem Stand überein. Die Tabelle nennt auch Quelltext-Tests in den genannten Dateien, die nicht in der Task-Tabelle standen (Kriterium 5).

| Datei | Test | Entscheidung | Deckt heute ab / Grund |
|---|---|---|---|
| LedgerRetryPushTests.fs | "der nächste Spiegel-Lauf pusht nichts und lässt die Buchung unverknüpft" | gelöscht | keine Aussage (Gegenprobe unmöglich: `MirrorFetchers` hat keinen Schreibaufruf, der Test bleibt bei jeder Änderung grün), Punkt 6 |
| LedgerCompactInteractionTests.fs | "das Kästchen der Maus-Zeile schickt ClickRowCheckbox mit Umschalt-Information" | gelöscht | "ClickRowCheckbox wählt und nimmt die Spanne ohne SelectionMode" (gleiche Datei), `[layout] Maus 900 px: Kästchen ohne Hover, Umschalt-Klick, …` (scripts/ledger-compact-mouse.test.mjs); Quelltext, Punkt 7 |
| LedgerCompactInteractionTests.fs | "die Maus-Zeile ist kein Knopf und setzt die Fokus-Attribute" | gelöscht | `[layout] Maus 900 px: …` (Markierung über `data-keyboard-focus`) und `[tastatur] Maus 900 px: nach einem Klick auf die Zeile wählt die Leertaste die markierte Zeile`; Quelltext, Punkt 7 |
| LedgerCompactInteractionTests.fs | "langes Drücken ist mit Maus aus und die Zeile wird nicht gewischt" | gelöscht | "Merkmale je Modus: Kästchen, Hover-Schaltfläche, Markierung, Gesten, Hinweis" (GesturesEnabled aus bei Maus) und `[layout] Maus 900 px: …`; Quelltext, Punkt 7 |
| LedgerReviewButtonVisibilityTests.fs | "PC: ohne Hover und Fokus unsichtbar, Breite bleibt (opacity-0, kein hidden)" | gelöscht | `[layout] Prüf-Schaltfläche: unsichtbar mit Breite, bei Zeilen-Hover und Tastaturfokus sichtbar, neutral, mit title` (scripts/ledger-review-button-visibility.test.mjs); Klassenstring, Punkt 6 |
| LedgerReviewButtonVisibilityTests.fs | "PC: sichtbar bei Zeilen-Hover, Fokus in der Zeile und eigenem Fokus" | gelöscht | derselbe `[layout]`-Test (Hover und Fokus); Klassenstring, Punkt 6 |
| LedgerReviewButtonVisibilityTests.fs | "PC: Zeile mit Tastaturcursor zeigt die Schaltfläche dauerhaft" | gelöscht | `[layout] Maus 900 px: …` (scripts/ledger-compact-mouse.test.mjs, "markierte Zeile zeigt die Schaltfläche", Deckkraft 1); Klassenstring, Punkt 6 |
| LedgerReviewButtonVisibilityTests.fs | "Handy: immer sichtbar, 44px Trefferfläche, neutral" | gelöscht | `[flow:buchung-pruefen] Geprüft an der Zeile: Trefferfläche, …` (scripts/ledger-review-mark.test.mjs); Tailwind-Größe als Klassenstring, Punkt 6 |
| LedgerReviewButtonVisibilityTests.fs | "Farbe ist neutral, nie Teal" | gelöscht | `[layout] Prüf-Schaltfläche: …` misst die berechnete Farbe (neutral); Klassenstring, Punkt 6 |
| LedgerReviewButtonVisibilityTests.fs | "Haken und Rückgängig-Pfeil nutzen Icons.Default statt NeonTeal" | gelöscht | `[layout] Prüf-Schaltfläche: …` (berechnete Farbe); Quelltext, Punkt 7 |
| LedgerReviewButtonVisibilityTests.fs | "PC-Zeile trägt die Klasse group, damit group-hover wirkt" | gelöscht | `[layout] Prüf-Schaltfläche: …` (sichtbar bei Zeilen-Hover, wirkt nur mit `group`); Quelltext, Punkt 7 |
| LedgerNotInYnabClientTests.fs | "Handy-Zeile und PC-Statusspalte rendern das Kennzeichen aus rowStatusMark" | gelöscht | die Entscheidung prüfen "eine nicht verknüpfte Buchung trägt das Kennzeichen Nicht in YNAB", "eine verknüpfte Buchung trägt kein Kennzeichen", "eine gelöschte Buchung bleibt gelöscht, …" (gleiche Datei, `rowStatusMark`); die Zählung der Aufrufe ist View-Verdrahtung per Teilstring, Punkt 7 |
| LedgerRecentCategoriesTests.fs | "Verdrahtung: Einzelwahl und Auswahl merken, beide Picker bekommen die Liste" | gelöscht, Zustandsteil gehoben | State-Teil jetzt am Reducer: "Auswahl im Picker setzt die Zeile auf Sendet und ruft setEntryCategory genau einmal" (LedgerCategoryChangeClientTests.fs) und "die Wahl ruft den Befehl mit der Auswahl auf, sperrt die Aktionen und schließt den Picker" (LedgerCategoryBatchClientTests.fs) prüfen `RecentCategories`; View-Teil (zwei Picker bekommen die Liste) ist Verdrahtung per Teilstring, Punkt 7, die Liste selbst prüft "im Filter gewählte Kategorie erscheint im Picker" |
| LedgerVisibleColumnsTests.fs | "ohne Konto-Spalte steht das Kürzel in der Payee-Zelle (Quelltext-Wächter)" | gehoben | neue reine Funktion `payeeAccountAbbreviation` (View.fs), Test "das Kürzel steht in der Payee-Zelle nur in der Gesamtsicht ohne Konto-Spalte" (gleiche Datei); die Aussage "Payee-Text und Kürzel sind getrennte Elemente" ist Markup, Punkt 7 |
| LedgerAccountChoiceTests.fs | "der Wechsler nutzt den BottomSheet-Baustein und den Speicher-Schlüssel" | gelöscht | `[flow:konto-wechseln] Handy: Titel zeigt den Kontonamen, Sheet listet alle Konten, Tipp wechselt und schließt` (scripts/ledger-account-choice.test.mjs, prüft Sheet, gespeicherten Schlüssel und Neuladen); Quelltext, Punkt 7 |
| LedgerLenaFlagClientTests.fs | "die Ansicht ist verdrahtet: Flag-Zeile, Knopf, Chip und Hinweisleiste" | gelöscht | `[flow:lena-fragen] PC: Lena fragen setzt das Kennzeichen, der Knopf wechselt, der Toast bietet Rückgängig` (scripts/ledger-lena.test.mjs); Chip-Logik prüft "Chip-Beschriftung, Reihenfolge und Zeilentreffer" (gleiche Datei); der Styleguide-Teil ist "Baustein steht im Styleguide", keine Aussage; Quelltext, Punkt 7 |
| LedgerReviewMarkClientTests.fs | "Die Ansicht verdrahtet die Schaltfläche mit den Start-Nachrichten" | gelöscht | `[flow:buchung-pruefen] Geprüft an der Zeile: Trefferfläche, Aufruf, Wechsel auf Ungeprüft ohne Historie, …` (scripts/ledger-review-mark.test.mjs, Klick und "ohne Historie" = stopPropagation); Hinweis mit Erneut senden: "Zeile mit Fehlschlag zeigt den Hinweis mit Grund und Erneut senden" (gleiche Datei) und "Erneut senden: Laden, dann Erfolg leert den Hinweis, …"; Quelltext, Punkt 7 |
| LedgerReviewMarkClientTests.fs | "Die Handy-Liste nutzt SwipeableRowRight nur für zu prüfende Zeilen" | gelöscht | "Wisch auf eine zu prüfende Zeile ruft setReviewMark Reviewed auf" und "Wisch auf eine geprüfte Zeile: kein Aufruf, kein Toast" (gleiche Datei, Reducer mit `swipeAppliesTo`), Geste: "Das Gummiband folgt nur der erlaubten Richtung" und "Die bisherige Wisch-Geste nach links (SyncFlow) bleibt, …"; Quelltext, Punkt 7 |
| LedgerMemoClientTests.fs | "der Picker am PC trägt den Fußtext, das Sheet am Handy nicht" | gelöscht | `[layout] Panel 1280 × 800: der Auslöser schließt das offene Popover, …` (scripts/move-money.test.mjs, wartet auf `category-picker-footer-hint` im Popover); der Wortlaut ist eine Konstante, Punkt 6, der Rest Quelltext, Punkt 7 |
| LedgerMemoClientTests.fs | "die Ansicht ist verdrahtet: Memo-Zeile, Knopf, Eingabefeld und Hinweisleiste" | gelöscht | `[flow:memo-setzen] PC: Memo hinzufügen setzt das Memo, Detail und Liste zeigen es, der Toast bietet Rückgängig` (scripts/ledger-memo.test.mjs); Quelltext, Punkt 7 |
| LedgerMemoClientTests.fs | "/styleguide zeigt die Auswahl-Bausteine und die neuen Memo-Bausteine" | gelöscht | keine Aussage ("Baustein steht im Styleguide", Quelltext), Punkt 7 |
| LedgerLenaFlagBatchClientTests.fs | "die Leiste reicht beide Aktionen an Handy- und PC-Leiste, Lena fragen sekundär (Quelltext)" | gelöscht | `[flow:lena-fragen-mehrere] PC: …` und `[layout] Handy 390x844: dritte Reihe mit beiden Aktionen, der Grund als Zeile, nichts läuft über` (scripts/ledger-lena-batch.test.mjs); Beschriftung und Grund: "ohne Farbe ist Lena fragen gesperrt mit Grund, mit Farbe nicht" (gleiche Datei); Quelltext, Punkt 7 |
| LedgerLenaFlagBatchClientTests.fs | "Seitenwechsel- und Sperrlogik kennt die neue Sammelaktion (Quelltext)" | gehoben | neue Fälle "isInputActive ist true bei: Kassenbuch: LenaBatchRunning" und "… CategoryBatchRunning" in `PwaTests.fs` (`State.isInputActive`, Reducer-Ebene) |
| LedgerReviewMarkBatchClientTests.fs | "Geprüft ist die primäre Aktion auf Handy und PC (Quelltext der Bausteine)" | gelöscht | `[flow:mehrfachauswahl] Handy 390x844: …` (scripts/ledger-selection.test.mjs: Geprüft in der Leiste, Orange, Reihenfolge Alle, Geprüft, Kategorie setzen, Auswahl aufheben) und `[tastatur] PC 1440x900: Kästchen, Shift-Spanne mit Anker, Kopfzeilen-Kästchen und Leiste`; Quelltext, Punkt 7 |
| LedgerReviewMarkBatchClientTests.fs | "Die Seite reicht Beschriftung, Sperre und Klick an beide Leisten (Quelltext)" | gelöscht | Sperre: "ohne Auswahl und während ein Aufruf läuft löst der Klick nichts aus" (gleiche Datei); Leisten: dieselben Browser-Tests; die Beschriftung "Geprüft" ist eine Konstante, Punkt 6; Quelltext, Punkt 7 |
| LedgerSortClientTests.fs | "Kopfzellen: sortierbare über die Sortier-Funktion, Memo und Status nicht" | gehoben | neue reine Funktion `tableHeaderFor` (View.fs, der Tabellenkopf läuft über die sichtbaren Spalten), Test "Kopfzellen: Datum, Konto, Payee, Kategorie und Betrag sortieren, Memo und Status nicht" (gleiche Datei) |
| LedgerSortClientTests.fs | "Styleguide zeigt die sortierbare Kopfzelle in drei Zuständen" | gelöscht | keine Aussage ("Baustein steht im Styleguide", Quelltext), Punkt 7; aria-sort je Richtung prüft "aria-sort je Richtung" (gleiche Datei) |
| LedgerSortSheetTests.fs | "Das Sheet startet geschlossen" | gelöscht | keine Aussage (init-Konstante), Punkt 6; dass Öffnen und Schließen wirken, prüft "OpenSortSheet und CloseSortSheet ändern Sort und Filter nicht" |
| LedgerSortSheetTests.fs | "Der Chip steht nur bei CompactList und ist nur bei Abweichung vom Standard aktiv" | gehoben | neue reine Funktionen `sortChipVisible` (View.fs) und `sortChipActive` (State.fs), gleichnamiger Test am Funktionswert (gleiche Datei); "kein Kreuz" (`OnRemove = None`) ist ein Record-Literal ohne Entscheidung, Punkt 6 |
| LedgerSortSheetTests.fs | "BottomSheet.optionRow existiert und steht im Styleguide" | gelöscht | Signatur prüft der Compiler; die 44 px misst jetzt `[layout] Handy: Chip am Ende der Leiste, das Sheet öffnet im Bild, Optionen mit 44 px Trefferfläche` (scripts/ledger-sort-chip.test.mjs); Styleguide-Teil keine Aussage; Klassenstring und Quelltext, Punkt 6 und 7 |
| LedgerCategoriesAwaitSettingsTests.fs | "Mit Settings Success und Budget führt LoadCategories zum Abruf getCategories und beendet das Warten" | gehoben | Root-`update` ist jetzt `updateWith ports`, `update = updateWith livePorts` (src/Client/State.fs, Port `GetCategories`); gleichnamiger Test am Reducer: Warten endet, Cmd läuft über die Lambda, die Antwort kommt als `CategoriesLoaded` an, Abruf genau einmal mit dem Standard-Budget |
| LedgerPageTests.fs | "AC5: loadPayeeSuggestions enthält keine eigene Groß-/Kleinschreibungs-Faltung, nur Core.searchFold" | gelöscht, durch Verhalten ersetzt | "AC4: SQL-Zweig (ohne Betragsfilter) und F#-Zweig (mit Betragsfilter) liefern dieselbe Gruppierung und Anzeige-Schreibweise" um "Bäckerei Müller"/"BÄCKEREI MÜLLER" ergänzt (beide Zweige); dazu besteht "AC2 (Umlaut): 'MÜLLER GMBH' und 'Müller GmbH' liefern einen Vorschlag mit Anzahl n+m" (SQL-Zweig); Quelltext, Punkt 7 |
| LedgerSelectionTests.fs | "Summenzeile zeigt bei leerer Auswahl den Treffer, sonst die Auswahl (Quelltext)" | gehoben | neue reine Funktion `summaryRowKind` (View.fs), Test "Summenzeile zeigt bei leerer Auswahl den Treffer, sonst die Auswahl" (gleiche Datei) |
| LedgerSelectionTests.fs | "Leiste: 'n ausgewählt', Alle und Auswahl aufheben" | gelöscht | keine Aussage (Label-Konstanten), Punkt 6; "n ausgewählt" zählt `[flow:mehrfachauswahl] Handy 390x844: …` (scripts/ledger-selection.test.mjs) |
| LedgerSelectionTests.fs | "Handy-Leiste hat Alle, PC-Leiste nicht; als schreibende Aktion nur Geprüft (Quelltext)" | gelöscht | veraltet (die Leiste trägt inzwischen Kategorie setzen und Lena fragen); `[flow:mehrfachauswahl] Handy 390x844: …` prüft Alle, Geprüft, Kategorie setzen, Auswahl aufheben und "keine weitere schreibende Aktion"; Quelltext, Punkt 7 |
| LedgerSelectionTests.fs | "Die Auswahl-Leiste steht außerhalb der Seitenhülle (Quelltext)" | gelöscht | `[flow:mehrfachauswahl] Handy 390x844: …` ("liegt im Bildschirm" je Knopf der fixen Leiste) und `[layout] PC 1440x900: Kategorie setzen öffnet das Popover ganz im Bild über der Leiste, …`; Quelltext, Punkt 7 |
| LedgerSelectionTests.fs | "Kästchen: drei Zustände, 20px am Handy und 17px am PC" | behalten ohne Größen (umbenannt in "Kästchen: drei Zustände in Teal, blassem Teal und ohne Teal") | nicht in der Tabelle der Task, aber Tailwind-Größe als Klassenstring (Kriterium 5): die Größen-Zeilen sind gelöscht, 17 px misst `[tastatur] PC 1440x900: Kästchen, Shift-Spanne mit Anker, Kopfzeilen-Kästchen und Leiste`; die Farbzustände bleiben (reine Funktion `boxClassName`) |
| LedgerSelectionTests.fs | "Im Auswahl-Modus ist die Wisch-Geste aus (Quelltext)" | gehoben | neue reine Funktionen `mobileRowMode` und `mobileRowLongPress` (View.fs, `mobileEntryRow` ruft nur sie), Tests "Handy-Zeile: Kästchen im Modus, Maus ohne Wisch, Auswahl ohne Modus schlicht, sonst Wisch" und "langes Drücken startet den Auswahl-Modus nur ohne Modus und ohne Maus" (gleiche Datei); sie decken auch die drei gelöschten Quelltext-Tests in LedgerCompactInteractionTests.fs |
| LedgerSelectionTests.fs | "die PC-Leiste trägt 'Esc aufheben', die Handy-Leiste nicht (Quelltext)" | gelöscht | `[tastatur] PC 1440x900: Leertaste wählt genau einmal, schließt das Panel, Prüf-Schaltfläche ruht, Esc-Kette (ledger-w4vte)` ("die PC-Leiste nennt Esc aufheben"); Text ist eine Konstante, Punkt 6, der Rest Quelltext, Punkt 7 |
| LedgerKeyboardTests.fs | "PC-Zeile: bei laufender Auswahl ein Platzhalter statt Prüf-Schaltfläche (Quelltext)" | gehoben | neue reine Funktion `pointerRowReviewMarkSlot` (View.fs, Tabelle und Maus-Zeile rufen sie), Test "Tabelle und Maus-Zeile: bei laufender Auswahl ein Platzhalter statt Prüf-Schaltfläche"; die 28 px des Platzhalters misst `[tastatur] PC 1440x900: Leertaste wählt genau einmal, …` |
| LedgerKeyboardTests.fs | "Handy-Zeile: bei hasSelection ohne Modus weder Wisch noch Schaltfläche (Quelltext)" | gehoben | neue reine Funktionen `touchRowReviewMarkSlot` und `mobileRowMode` (View.fs), Test "Handy-Zeile: bei laufender Auswahl ohne Modus weder Wisch noch Schaltfläche" |
| LedgerKeyboardTests.fs | "Handy-Kästchen: der Container trägt den Tipp und ist mindestens 44 px breit (Quelltext)" | gelöscht | `[flow:mehrfachauswahl] Handy 390x844: langes Drücken, Tippen, Alle und Auswahl aufheben` (Tipp wählt ab und aus); Tailwind-Größe als Klassenstring, Punkt 6, Quelltext, Punkt 7 |
| LedgerKeyboardTests.fs | "nur Kassenbuch und Budget-Seite hören auf Tasten am Dokument, kein anderes Kürzel kollidiert" | behalten mit Begründung, Verdrahtung entfernt | nicht in der Tabelle; repo-weite Invariante ohne Verhaltenszugang (Dokument-Handler über alle Client-Dateien), jetzt mit eingebautem Gegenbeweis für die Erkennung (Punkt 7); die Zeile "in der Seite eingebunden" (Teilstring auf View.fs) ist gelöscht, die Einbindung prüfen die `[tastatur]`-Tests in scripts/ledger-selection.test.mjs |
| LedgerKeyboardTests.fs | "die Fokuszeile trägt ein Merkmal, andere Zeilen nicht" | behalten, Quelltext-Teil gelöscht | nicht in der Tabelle; der Teil `aria-selected` per Teilstring auf Table.fs ist weg, für `aria-selected` der PC-Tabellenzeile gibt es keinen Browser-Test, der Teil hatte keine Aussage (Quelltext, Punkt 7); die Klasse der Fokuszeile bleibt (reine Funktion `Table.rowClass`, keine Größe) |
| LedgerKeyboardTests.fs | "die Liste reicht den Fokus an die PC-Zeile durch" | gelöscht | nicht in der Tabelle; Quelltext, Punkt 7; `[tastatur] PC 1440x900: Leertaste wählt genau einmal, …` ("es ist die markierte Zeile") |
| LedgerCategoryBatchClientTests.fs | "die Leiste reicht die Sammelaktion durch, Geprüft bleibt primär, Kategorie setzen sekundär" | gelöscht | `[flow:mehrfachauswahl] Handy 390x844: …` (Geprüft primär in Orange, Kategorie setzen sekundär, Reihenfolge) und für die gemeinsame Sperre "ohne Auswahl und während ein Aufruf läuft löst die Wahl nichts aus" (gleiche Datei); Quelltext, Punkt 7 |
| LedgerCategoryBatchClientTests.fs | "Picker der Auswahl öffnet über der Leiste und ohne Haken (Bausteine)" | gelöscht | `[layout] PC 1440x900: Kategorie setzen öffnet das Popover ganz im Bild über der Leiste, Esc schließt nur den Picker (ledger-w29y6)` (Lage über der Leiste, "keine Zeile mit Haken"); Klassenstring und Quelltext, Punkt 6 und 7 |
| LedgerCategoryBatchClientTests.fs | "die neuen Varianten erscheinen in /styleguide und im Styleguide-Dokument" | gelöscht | keine Aussage ("Baustein steht im Styleguide", Quelltext und Doku), Punkt 7 |
| LedgerDetailTests.fs | "die Form hängt nicht davon ab, ob das Panel offen ist (kein Kippen)" | gelöscht | nicht in der Tabelle, Quelltext und Klassenstring `md:pr-[420px]`, Punkt 6 und 7; die Form aus der Breite ohne Panel prüfen "Grenzwerte: ein Konto braucht 660 px neben dem Panel, also 1080 px ohne" und "zeigt der PC schon die Kompaktliste, ist die Form ein Sheet" (gleiche Datei), das stehende Panel `[flow:buchung-oeffnen] PC (1440): …` (scripts/ledger-detail.test.mjs, "Liste bleibt") |
| LedgerDetailTests.fs | "nur detailContent ruft splitLineTexts auf, Panel und Sheet zeigen dieselben Texte" | gelöscht | nicht in der Tabelle; "eine Buchung mit zwei Split-Zeilen zeigt beide mit Ziel und Betrag" (gleiche Datei, `splitLineTexts`), `[flow:aufteilen] PC: …` und `[layout] Handy (390x844): Aufteilen im selben Sheet, …`; Quelltext, Punkt 7 |
| LedgerDetailTests.fs | "ohne Splits gibt es keine Zeilen, die Ansicht schreibt dann Kein Split" | behalten ohne Quelltext-Teil (umbenannt in "ohne Splits gibt es keine Zeilen") | der Platzhalter "Kein Split" war ein Teilstring auf View.fs (Konstante, Punkt 6 und 7) |
| LedgerDetailTests.fs | "Kategorie, Split, Memo und Geprüft erscheinen im Inhalt für Panel und Sheet gleich" | gelöscht | nicht in der Tabelle; `[flow:buchung-oeffnen] PC (1440): …` (Panel) und `[layout] Handy (390x844): Tipp öffnet das Sheet mit Filter-Knöpfen an den Feldern, kein Block oben` (scripts/ledger-detail.test.mjs); Quelltext, Punkt 7 |
| LedgerDetailTests.fs | "die Felder im Inhalt sind lesend, der Pfeil der Kategorie lebt in categoryField (ledger-sfab0)" | gelöscht | nicht in der Tabelle; `[flow:kategorie-aendern] PC (1440): Pfeil öffnet den Picker im Panel, …` (scripts/ledger-category-change.test.mjs); Quelltext, Punkt 7 |
| LedgerDetailTests.fs | "die Kategorie steht am PC voll qualifiziert, im Sheet am Handy kurz (ledger-fa1fp)" | gelöscht | nicht in der Tabelle; "Handy-Detail zeigt die Kategorie kurz, PC-Detail voll qualifiziert" (LedgerMemoClientTests.fs, `detailCategoryLabel`); Quelltext, Punkt 7 |
| LedgerDetailTests.fs | "die Herkunft-Zeile zeigt für jede Art das Wort der Domäne in einem Teal-Badge" | behalten ohne Quelltext-Teil (umbenannt in "die Herkunft-Zeile zeigt für jede Art das Wort der Domäne") | `originLabel` bleibt geprüft; der Teilstring auf `Detail.originRow` und `Badge.Info` war Verdrahtung, Punkt 7 |
| LedgerDetailTests.fs | "Lade- und Leerzustand der Historie sind vorhanden" | gelöscht | nicht in der Tabelle; keine Aussage (Texte als Konstante und Bausteine per Teilstring), Punkt 6 und 7 |
| LedgerDetailTests.fs | "die Zeitleiste des Details hat Punktfarben je Urheber im Design-System" | gelöscht | nicht in der Tabelle; die Wahl der Farbe prüfen "die Punktfarbe folgt dem Urheber" und "Fehlschläge und die Löschung sind rot, gleich welcher Urheber" (gleiche Datei); Klassenstring per Teilstring, Punkt 6 und 7 |
| LedgerDetailTests.fs | "kein Geprüft-Schalter, grauer Betrag und nur Filter-Knöpfe als Aktion" | gehoben | neue reine Funktion `detailReadOnly` (View.fs, Kopf und Inhalt des Details rufen sie), Test "eine gelöschte Buchung ist ein Lesezustand, eine offene nicht"; Filter-Knöpfe: "die gelöschte Buchung bekommt trotzdem beide Filter-Knöpfe" |
| LedgerDetailTests.fs | "jeder neue Baustein hat ein Beispiel im Styleguide" | gelöscht | keine Aussage ("Baustein steht im Styleguide"), Punkt 7 |
| LedgerDetailTests.fs | "bei leerer Liste baut das Detail keine Link-Zeile" | behalten ohne Quelltext-Teil | `detailExternalLinks` leer bleibt geprüft; der Teilstring `\| [] -> None` war Quelltext, Punkt 7 |
| LedgerDetailTests.fs | "Sheet und Seitenpanel teilen dieselbe Detail-Funktion mit der Zeile unter dem Memo" | gelöscht | nicht in der Tabelle; `[flow:bestell-link-oeffnen] PC: das Seitenpanel zeigt den Bestell-Link unter dem Memo, …` und `[layout] Handy (390): das Sheet zeigt den PayPal-Link mit mindestens 44 px Trefferfläche` (scripts/ledger-external-links.test.mjs); Quelltext, Punkt 7 |
| LedgerDetailTests.fs | "der Link öffnet in neuem Tab, mit rel und Symbol" | gelöscht | nicht in der Tabelle; `[flow:bestell-link-oeffnen] PC: …, öffnet in neuem Tab, …`; Quelltext, Punkt 7 |
| LedgerDetailTests.fs | "die Kassenbuch-Liste (Tabelle und Kompaktliste) zeigt keinen Link" | gelöscht | nicht in der Tabelle; `[flow:bestell-link-oeffnen] PC: …, die Liste zeigt keinen Link`; Quelltext-Zählung, Punkt 7 |
| LedgerSplitEditorClientTests.fs | "der Pfeil an der Kategorie öffnet bei einer Split-Buchung den Editor" | behalten ohne Quelltext-Teil | der Rückbau-Wächter "Split bearbeiten folgt" (Teilstring auf Types.fs) ist gelöscht, Punkt 7; `categoryLockFor` und `categoryArrowMsg` bleiben geprüft |
| LedgerSplitEditorClientTests.fs | "die Hinweisleiste nennt für Neuanlage offen einen eigenen Text und Erneut senden" | behalten ohne Quelltext-Teil | der Teilstring `Api.ledger.retryCategorizationWriteBack` auf State.fs ist gelöscht (Verdrahtung, Punkt 7); den Aufruf im Browser prüft scripts/ledger-category-change.test.mjs (Route `retryCategorizationWriteBack`) |
| LedgerSplitEditorClientTests.fs | "Ziel je Zeile über den einen Picker (PC Popover, Handy sheetLayered)" | gelöscht | `[flow:aufteilen] PC: Aufteilen mit Übertrag über den Picker im Panel: …` und `[layout] Handy (390x844): Aufteilen im selben Sheet, …, Auswahl als zweites Sheet` (scripts/ledger-split-editor.test.mjs); die Ziele prüft "die Kontenliste enthält nur offene Budget-Konten, ohne das eigene Konto der Buchung" (gleiche Datei); Quelltext, Punkt 7 |
| LedgerSplitEditorClientTests.fs | "Rechnung kommt aus Shared.Domain, nicht aus SyncFlow" | gelöscht | Rückbau-Wächter per Teilstring, Punkt 7; die Rechnung prüft "Speichern nur bei Rest 0,00 € und wenn jede Zeile ein Ziel hat" und die Rest-Tests (gleiche Datei) |
| LedgerSplitEditorClientTests.fs | "Aufteilen und Ändern sind feste Knöpfe mit Trefferfläche ab 44 px" | behalten ohne Klassenstring (umbenannt in "der Knopf heißt Aufteilen ohne Split und Ändern mit Split") | 44 px misst `[layout] Handy (390x844): Aufteilen im selben Sheet, …` ("Trefferfläche mindestens 44 px"); `min-h-11` und der Rest waren Klassenstring und Quelltext, Punkt 6 und 7 |
| LedgerSplitEditorClientTests.fs | "neue Bausteine stehen in /styleguide" | gelöscht | keine Aussage ("Baustein steht im Styleguide"), Punkt 7 |
| LedgerSplitEditorClientTests.fs | "Optik nur aus Tokens: keine Hex-Werte, keine Standardfarben, kein style.fontSize" | gelöscht | doppelt zu `npm run guard:tokens` (scripts/token-guard.mjs prüft alle `.fs` unter src/Client, Teil des Gates) |
| LedgerSplitEditorClientTests.fs | "Ansicht: der Knopf Barabhebung steht im Editor und löst das Ziel aus Quick-Add-Konto und Konto-Liste auf" | gelöscht | nicht in der Tabelle; `[flow:barabhebung-aufteilen] Handy (390x844): Barabhebung im Editor-Sheet, …, der Tipp setzt den Übertrag aufs Bar-Konto` (scripts/ledger-split-cashback.test.mjs) und die `cashbackTarget`-Tests (gleiche Datei); die Beschriftung ist eine Konstante, Punkt 6 |
| LedgerCategoryChangeClientTests.fs | "Rückgängig ruft denselben Schreibweg mit der alten Kategorie auf" | behalten ohne Quelltext-Teil | der Teilstring auf State.fs (`previous \|> Option.map …`) ist gelöscht; den gesendeten Wert prüft `[flow:kategorie-aendern] PC (1440): …, Rückgängig stellt zurück` ("Rückgängig sendet die alte Kategorie"); Punkt 7 |
| LedgerCategoryChangeClientTests.fs | "Der Picker geht am PC als Popover im Panel auf, am Handy und in der Kompaktliste als Sheet über dem Detail" | behalten ohne Quelltext-Teil | `categoryPickerPlacement` bleibt geprüft; die Verdrahtung prüfen `[flow:kategorie-aendern]` (Popover im Panel) und `[layout] Handy (390x844): Der Picker öffnet als zweites Sheet über dem Detail, …`; Punkt 7 |
| LedgerCategoryChangeClientTests.fs | "Der Pfeil sitzt in detail-category, gesperrt mit Hinweistext, die Zeile selbst öffnet auch" | gelöscht | Sperre und Pfeil: "abgeglichen, Tracking-Konto und Übertrag sperren Aufteilen, Ändern und den Pfeil, mit Hinweis" und "der Pfeil an der Kategorie öffnet bei einer Split-Buchung den Editor" (LedgerSplitEditorClientTests.fs, `categoryLockHint`, `categoryArrowMsg`); Klick auf den Pfeil `[flow:kategorie-aendern]`; Klassenstring und Quelltext, Punkt 6 und 7 |
| LedgerCategoryChangeClientTests.fs | "Die Karte steht direkt unter dem Kategorie-Feld und nimmt die Zeilen aus dem Modell" | behalten ohne Quelltext-Teil (umbenannt in "Die Karte nimmt die Zeilen aus dem Modell, nur für die geänderte Buchung") | `categoryCardLinesFor` bleibt geprüft; die Lage messen `[flow:kategorie-aendern]` ("Karte unter dem Kategorie-Feld") und `[layout] Handy (390x844): …`; Punkt 7 |
| LedgerCategoryChangeClientTests.fs | "Die ausgegraute Zeile trägt die gedämpfte Klasse und den Vermerk, die Leiste Neu laden nur bei OrderStale" | gelöscht | die Ansicht liest nur Modellfelder, die der Reducer setzt: "Filter-Nichttreffer: Zeile bleibt, wird ausgegraut, …", "OrderStale: bei Sortierung nach Kategorie gesetzt, …", "Im Stale-Zustand ruht das Nachladen, Neu laden setzt ab Seite 1 neu auf", "Die ausgegraute Zeile ist von Alle auswählen ausgenommen und nicht wählbar" (gleiche Datei); Klassenstring, Konstanten und Quelltext, Punkt 6 und 7 |
| LedgerCategoryChangeClientTests.fs | "Erneut versuchen im Picker löst LoadCategories aus, das auf Loading schaltet (dmw83)" | behalten ohne Quelltext-Teil (umbenannt in "LoadCategories nach einem Fehlschlag schaltet auf Loading (dmw83)") | der Teilstring `OnRetry = …` auf View.fs ist Verdrahtung, Punkt 7; der Reducer-Teil bleibt |
| LedgerFilterButtonTests.fs | "Payee- und Kategorie-Zelle der PC-Zeile sind reiner Text ohne eigenen Auslöser" | gelöscht | `[flow:buchung-oeffnen] PC (1440): Klick auf die Payee-Zelle öffnet das Panel, kein Menü; …` (scripts/ledger-detail.test.mjs: "kein Zellmenü", "keine Auslöser in den Zellen"); Quelltext, Punkt 7 |
| LedgerFilterButtonTests.fs | "Die Zeile öffnet das Detail, nicht mehr die Historie" | gelöscht | derselbe `[flow:buchung-oeffnen]`-Test (Klick öffnet das Panel); Quelltext, Punkt 7 |
| LedgerFilterButtonTests.fs | "Der Filter-Knopf stoppt die Weitergabe und trägt Name und Tooltip" | gelöscht | `[layout] Handy (390x844): Tipp öffnet das Sheet mit Filter-Knöpfen an den Feldern, kein Block oben` (Knopf an Payee und Kategorie, der Tipp lädt neu, "das Sheet bleibt offen" = keine Weitergabe); Quelltext, Punkt 7 |
| LedgerFilterButtonTests.fs | "FilterPopover.menuItem bleibt im Design-System, mit seiner Styleguide-Karte" | gelöscht | keine Aussage ("Baustein steht im Styleguide", Rückbau-Wächter), Punkt 7 |
| LedgerViewTests.fs | "styles.css deklariert die CSS-Variable, auf die der Token zeigt" | behalten mit Begründung, Gegenbeweis ergänzt | nicht in der Tabelle; Bindung Token zu CSS-Variable ohne Verhaltenszugang unter .NET (Punkt 7), jetzt mit eingebautem Gegenbeweis (erkennt die Deklaration, verwirft eine andere Variable) |
| LedgerViewTests.fs | "Keine Farbliterale in der Kassenbuch-Ansicht: 'text-neon-' kommt nicht vor" | gelöscht | nicht in der Tabelle; Stilregel einer Datei per Teilstring, kein Verhalten, Punkt 7 (Wächter-Lücke steht in der Kann-Sammlung) |
| LedgerViewTests.fs | "'ohne Kategorie' steht genau einmal, in categoryLabel" | gelöscht | nicht in der Tabelle; "Ohne Kategorie und ohne Splits steht 'ohne Kategorie'" und "Keine Kategorie und keine Splits zeigt 'ohne Kategorie'" (gleiche Datei); Quelltext-Zählung, Punkt 7 |
| LedgerViewTests.fs | "'ohne Payee' steht genau einmal, in payeeLabel" | gelöscht, durch Verhalten ersetzt | neuer Test "Ohne Payee steht 'ohne Payee', sonst der Name" (`payeeLabel`, gleiche Datei; vorher prüfte kein Test die Funktion) |
| LedgerViewTests.fs | "entryRow und entryTableRow teilen payeeLabel; die Kategorie-Funktion unterscheidet sich bewusst (ADR 0039)" | gelöscht | "Die PC-Tabelle zeigt 'Gruppe: Name', die Handy-Zeile den Blattnamen, ohne Gruppe beide den Namen" (`listCategoryLabel`, gleiche Datei) und "Ohne Payee steht 'ohne Payee', sonst der Name"; Funktionsrumpf-Wächter, Punkt 7 |
| LedgerViewTests.fs | "Weder entryRow noch entryTableRow rufen originBadge auf, das Detail zeigt die Herkunft" | gehoben | `tableStatusMarks` (View.fs) kennt nur Status-Kennzeichen, keine Herkunft; Tests der Liste "PC-Statusspalte zeigt Status statt Herkunft (ledger-qns4j)"; Herkunft im Detail: "die Herkunft-Zeile zeigt für jede Art das Wort der Domäne" (LedgerDetailTests.fs) |
| LedgerViewTests.fs | "Beide Ansichten speisen die Kategorie-Rolle aus demselben Wert (categoryClassName)" | gelöscht | die Rolle prüfen die `categoryClassName`-Tests (gleiche Datei, "Ohne Kategorie bleibt Orange", …); Funktionsrumpf-Wächter und Rückbau-Wächter, Punkt 7 |
| LedgerViewTests.fs | "statusColumnWidth ist auf den Handoff-Wert 128 zurückgesetzt (AC6)" | gelöscht | `[layout] Spaltenraster: Kästchen 44px, Status 128px, …` (scripts/ledger-status-column.test.mjs); Konstante im Quelltext, Punkt 6 und 7 |
| LedgerViewTests.fs | "colgroup und Köpfe richten sich nach den sichtbaren Spalten (Quelltext-Wächter)" | gehoben | der Kopf läuft jetzt über die sichtbaren Spalten und `tableHeaderFor` ("Kopfzellen: Datum, Konto, Payee, Kategorie und Betrag sortieren, Memo und Status nicht", LedgerSortClientTests.fs); Breiten misst `[layout] Spaltenraster: …` und `[layout] Gesamtsicht und ein Konto: kein Spaltenkopf überlappt …` (scripts/ledger-table-columns.test.mjs) |
| LedgerViewTests.fs | "needsReviewStatusBadge zeigt das kleine Wort 'zu prüfen' in Pink, kein Badge mehr (ADR 0049 Punkt 7)" | gelöscht | `[layout] Zwei Kennzeichen in der Statuszelle: kein Umbruch, kein Überlauf, keine höhere Zeile, volle Bedeutung im Hover-Text (AC5)`; Wortlaut und Farbe als Teilstring, Punkt 6 und 7 |
| LedgerViewTests.fs | "entryTableRow rendert needsReviewStatusBadge nur bei entry.NeedsReview, kein neuer Zustand (AC4)" | gehoben | neue reine Funktion `tableStatusMarks` (View.fs), Test "zu prüfen nur bei NeedsReview, vor dem Flag" |
| LedgerViewTests.fs | "entryTableRow rendert deletedStatusIcon nur für rowStatusMark RowDeleted (an entry.DeletedInYnab gebunden)" | gehoben | `tableStatusMarks`, Tests "das Gelöscht-Symbol nur bei einer in YNAB gelöschten Buchung, hinter den übrigen" und "eine nicht verknüpfte Buchung trägt nur Nicht in YNAB, eine gelöschte bleibt gelöscht"; `rowStatusMark` prüfen die Tests in LedgerNotInYnabClientTests.fs |
| LedgerViewTests.fs | "Status-Kennzeichen der PC-Tabelle bleiben ohne Zeilenumbruch nebeneinander (AC5)" | gelöscht | `[layout] Zwei Kennzeichen in der Statuszelle: kein Umbruch, …`; Klassenstring, Punkt 6 |
| LedgerViewTests.fs | "entryTableRow verwendet die gekürzte flagStatusBadge, nicht die volltextige flagBadge (AC5)" | gelöscht | `[layout] Zwei Kennzeichen in der Statuszelle: …, kein Überlauf, …, volle Bedeutung im Hover-Text`; Funktionsrumpf-Wächter, Punkt 7 |
| LedgerViewTests.fs | "flagStatusBadge kürzt Rückfrage Lena mit voller Bedeutung für assistive Technologie, NoFlag bleibt unverändert" | gelöscht | nicht in der Tabelle; derselbe `[layout]`-Test (Hover-Text); `NoFlag` ohne Kennzeichen jetzt in "eine geprüfte, verknüpfte Buchung ohne Flag trägt kein Kennzeichen"; Quelltext, Punkt 7 |
| LedgerViewTests.fs | "flagStatusBadge zeigt Markiert als Symbol statt Text, mit voller Bedeutung für assistive Technologie" | gelöscht | nicht in der Tabelle; derselbe `[layout]`-Test; Quelltext, Punkt 7 |
| LedgerViewTests.fs | "withAccessibleName trägt title und aria-label nach" | gelöscht | nicht in der Tabelle; derselbe `[layout]`-Test ("volle Bedeutung im Hover-Text"); Quelltext, Punkt 7 |
| LedgerViewTests.fs | "deletedStatusIcon zeigt kein Text-Kennzeichen mehr, nur ein Symbol mit voller Bedeutung für assistive Technologie" | gelöscht | nicht in der Tabelle; derselbe `[layout]`-Test; Quelltext, Punkt 7 |
| LedgerViewTests.fs | "accountStrip trägt die Sticky-Klasse ledger-account-strip" und die zwei Tests "Die Regel .ledger-account-strip selbst …" (AC1, AC2) | gelöscht | nicht in der Tabelle; `[layout] Die Konten-Spalte haftet unter der Navbar und scrollt bei zu vielen Konten in sich, ohne sich selbst zu bewegen (AC1, AC2)` (scripts/ledger-status-column.test.mjs) misst genau das; Quelltext, Punkt 2 und 7 |
| LedgerViewTests.fs | drei Tests der Liste "withoutCommentLines (ledger-nnn47)" | gelöscht | nicht in der Tabelle; prüften nur den Helfer der gelöschten Zähl-Tests, der Helfer ist mit weg |
| LedgerViewTests.fs | "Die Seite nutzt weder PageHeader.gradientWithActions noch Container.view mit Breitenbegrenzung" | gelöscht | Rückbau-Wächter per Teilstring, keine Aussage, Punkt 7 |
| LedgerViewTests.fs | "Jetzt spiegeln bleibt erreichbar, am Handy als Symbol-Knopf mit Titel" | gelöscht | `[layout] Handy und PC: genau ein sichtbarer Spiegel-Knopf, am Handy das Symbol, am PC der Textknopf` (scripts/ledger-mirror-buttons.test.mjs) und die Spiegel-Reducer-Tests; Rückbau- und Verdrahtungswächter, Punkt 7 |
| LedgerViewTests.fs | "Beträge laufen über die Listenvariante, gelöschte bleiben bei transactionDeleted" | gelöscht | "Summenzeile: Summe 0 und negative Summe ohne Rot und Grün" (`Money.listAmountClass`, gleiche Datei); Rückbau-Wächter, Punkt 7 |
| LedgerViewTests.fs | "Der PC-Betrag läuft über Table.amountCell, rechtsbündig mit dem Spaltenkopf" | gehoben (Kopf) | rechtsbündiger Kopf jetzt über `tableHeaderFor` ("Kopfzellen: …", LedgerSortClientTests.fs, `HeaderAlign`); `Table.amountCell` ist Verdrahtung, Punkt 7 |
| LedgerViewTests.fs | "Zu prüfen ist in beiden Viewports die Statuskante, kein Punkt und kein Rahmen mehr" | gelöscht | `[layout] Maus 900 px: …` (scripts/ledger-compact-mouse.test.mjs) und "der Container der Maus-Zeile: Markierung, Anker und Statuskante" (LedgerCompactInteractionTests.fs); Rückbau-Wächter, Punkt 7 |
| LedgerViewTests.fs | "Konto-Kürzel, Flag und Gelöscht-Kennzeichen stehen in Zeile 2 der Handy-Zeile" | gelöscht | Lage im Markup, keine Logik; Quelltext, Punkt 7 |
| LedgerViewTests.fs | "Beide Viewports zeigen die Tagessumme im Gruppenkopf" | gelöscht | "Die Tagessumme ist die Summe der Zeilen der Gruppe" und die übrigen `daySum`/`daySumText`-Tests (gleiche Datei), `[flow:kassenbuch-sortieren] …, ohne Tagesköpfe; …` (scripts/ledger-sort-headers.test.mjs); Quelltext, Punkt 7 |
| LedgerViewTests.fs | "Die PC-Tagessumme steht in der Betragsspalte, nicht unter der Statusspalte" | gelöscht | Lage im Markup; Quelltext, Punkt 7 |
| LedgerViewTests.fs | "Kontostände laufen über die ruhigen Money-Bausteine" | gelöscht | die Kontostände der Leiste prüfen die `buildAccountCards`-Tests aus ledger-vq2g8 (gleiche Datei) und "Negativer Kontostand ist Red, positiver neutral"; Quelltext, Punkt 7 |
| LedgerViewTests.fs | "popoverWidth: Kategorie 460px, Payee 340px, sonst 320px" | behalten ohne Klassenstring (umbenannt in "popoverWidth: Kategorie und Payee haben eigene Breiten, sonst die Standardbreite") | nicht in der Tabelle; Tailwind-Größen als Klassenstring (Kriterium 5), jetzt Vergleich mit den Konstanten aus `FilterPopover`; die Wahl je Chip bleibt die Aussage |
| LedgerViewTests.fs | "Das Styleguide-Demo des FilterPopover nutzt die Breite der Seite" | gelöscht | keine Aussage (Styleguide, Quelltext), Punkt 7 |
| LedgerViewTests.fs | "Ledger/View.fs formatiert Zahlen nicht mit string" | gelöscht | "payeeSuggestionsHeading nutzt Tausenderpunkt" und "summaryCountText: 0, 1 und 12116" (gleiche Datei); Quelltext, Punkt 7 |
| LedgerViewTests.fs | "Payee-Zeile: Name gekürzt über PickerParts, Zahl in einem shrink-0-Bereich" | gelöscht | Klassenstring und Quelltext, Punkt 6 und 7; Abschneiden ist eine `[layout]`-Aussage |
| LedgerViewTests.fs | "memoPickerBody und amountPickerBody nutzen Input.Small, kein Input.textSimple" | gelöscht | `[layout]`-Tests der Filterleiste zu Schriftgrößen (scripts/ledger-filter-bar.test.mjs, 16 px am Handy); Quelltext, Punkt 7 |
| LedgerViewTests.fs | "Von und Bis tragen die 12px-Rolle ListRoles.muted" | gelöscht | Quelltext, Punkt 7; Schriftgrößen misst scripts/ledger-filter-bar.test.mjs |
| LedgerViewTests.fs | "datePickerBody: Übernehmen ohne Button.primary, mit PickerParts.applyButtonClass" | gelöscht | die Sperre bei Fehler prüft "SetDateQuick und ResetDateDraft heben den Fehlerzustand auf" (LedgerPageTests.fs, `dateDraftHasError`); Quelltext, Punkt 7 |
| LedgerViewTests.fs | "Datumsfelder in Input.Small-Maßen (über den DS-Baustein DateField)" | gelöscht | Tailwind-Größe als Klassenstring und Quelltext, Punkt 6 und 7; `[fokus] Der Kalender-Knopf wirft keinen Konsolenfehler und lässt das Textfeld bedienbar (m7z9q)` (scripts/ledger-filter-bar.test.mjs) |
| LedgerViewTests.fs | "summaryRow: ohne Karte, Betrag über Money.listAmount, Skeleton beim Laden" | gelöscht | die Zustände prüfen die vier `summaryState`-Tests (gleiche Datei); Quelltext, Punkt 7 |
| LedgerViewTests.fs | "Detail-Zeitleiste: kein FontSizes.body, die Schrift folgt den Listenrollen (ADR 0049)" | gelöscht | Stilregel per Teilstring auf DesignSystem/Detail.fs, Rückbau-Wächter, Punkt 7 |

Begründet behalten mit Quelltext-Zugriff (Kriterium 5), je mit eingebautem Gegenbeweis: "nur Kassenbuch und Budget-Seite hören auf Tasten am Dokument, kein anderes Kürzel kollidiert" (LedgerKeyboardTests.fs, alle Client-Dateien) und "styles.css deklariert die CSS-Variable, auf die der Token zeigt" (LedgerViewTests.fs). In den genannten Dateien steht keine Tailwind-Größe mehr als Klassenstring (geprüft per grep auf `h-`, `w-`, `min-h-`, `size-`, `px]`, `text-[`, Abstände).

### Gehobene Entscheidungen und Mutanten

Je Funktion eine Mutante im Produktionscode, jede macht den neuen Test rot (Lauf mit `--filter-test-case`, danach zurückgesetzt). Die Liste gilt nur für diese Mutanten.

| Funktion (Datei) | Mutante | roter Test |
|---|---|---|
| `payeeAccountAbbreviation` (View.fs) | Bedingung ohne `not (List.contains AccountColumn columns)` | "das Kürzel steht in der Payee-Zelle nur in der Gesamtsicht ohne Konto-Spalte" |
| `tableHeaderFor` (View.fs) | Memo sortiert nach Datum | "Kopfzellen: Datum, Konto, Payee, Kategorie und Betrag sortieren, Memo und Status nicht" |
| `sortChipVisible` (View.fs) | `layout <> CompactList` | "Der Chip steht nur bei CompactList und ist nur bei Abweichung vom Standard aktiv" |
| `sortChipActive` (State.fs) | nur das Feld vergleichen, nicht die Richtung | derselbe Test ("Datum aufsteigend weicht ab") |
| `summaryRowKind` (View.fs) | `model.Selection <> NoSelection` statt `hasSelection` | "Summenzeile zeigt bei leerer Auswahl den Treffer, sonst die Auswahl" |
| `mobileRowMode` (View.fs) | Zweige Maus und Auswahl ohne Modus vertauscht | "Handy-Zeile: Kästchen im Modus, Maus ohne Wisch, Auswahl ohne Modus schlicht, sonst Wisch" |
| `mobileRowLongPress` (View.fs) | ohne `&& not mouse` | "langes Drücken startet den Auswahl-Modus nur ohne Modus und ohne Maus" |
| `pointerRowReviewMarkSlot` (View.fs) | Auswahl ergibt `NoReviewMarkSlot` statt Platzhalter | "Tabelle und Maus-Zeile: bei laufender Auswahl ein Platzhalter statt Prüf-Schaltfläche" |
| `touchRowReviewMarkSlot` (View.fs) | ohne `\|\| selectionActive` | "Handy-Zeile: bei laufender Auswahl ohne Modus weder Wisch noch Schaltfläche" |
| `detailReadOnly` (View.fs) | `not entry.LinkedToYnab` statt `entry.DeletedInYnab` | "eine gelöschte Buchung ist ein Lesezustand, eine offene nicht" |
| `tableStatusMarks` (View.fs) | Flag vor "zu prüfen" | "zu prüfen nur bei NeedsReview, vor dem Flag" |
| `updateWith`/`GetCategories` (State.fs) | `CategoriesAwaitSettings = true` im Abruf-Zweig | "Mit Settings Success und Budget führt LoadCategories zum Abruf getCategories und beendet das Warten" |
| `updateWith`/`GetCategories` (State.fs) | eigene Lambda statt `ports.GetCategories` | derselbe Test |
| `chipLabel` (View.fs) | `"Kategorie"` statt `"Kategorie: n"` | "'Übernehmen' schreibt den Entwurf in den Filter, schließt das Popover und lädt ab Seite 0 neu" |
| `isInputActive` (State.fs) | ohne `ledger.LenaBatchRunning` | "isInputActive ist true bei: Kassenbuch: LenaBatchRunning" |
| `StartSetEntryCategory` (Ledger/State.fs) | ohne `RecentCategories = …` | "Auswahl im Picker setzt die Zeile auf Sendet und ruft setEntryCategory genau einmal" |
| `groupFoldedPayeeCounts` (Persistence.fs) | ASCII-Faltung A bis Z statt `Core.searchFold` | "AC4: SQL-Zweig (ohne Betragsfilter) und F#-Zweig (mit Betragsfilter) liefern dieselbe Gruppierung und Anzeige-Schreibweise" und "AC2 (Umlaut): …" |
| `MirrorBatchState.append` (CommandHandlers.fs) | Verknüpfungen nicht nachtragen | "neue Buchung, Treffer über YNAB-Id und ImportId, Übertragspaar und ersetzte Id schreiben dieselben Ereignisse" |
| `MirrorBatchState.append` (CommandHandlers.fs) | Zustand nicht nachtragen | derselbe Test |
| `ynabTransactionIdIn` (CommandHandlers.fs) | Verknüpfungen der ersten Buchung für alle | fünf Tests in "Ledger Prüfvermerk für die Auswahl (Befehle)" |

Beim Durchlauf fiel auf: `shutil.move` stellt die gesicherte Datei mit alter Änderungszeit zurück, MSBuild baute den Client danach nicht neu. Die letzte Client-Mutante (`RecentCategories`) blieb dadurch in der DLL stehen, bis die Dateien angefasst und neu gebaut waren. Danach lief die volle Suite grün; alle Ergebnisse oben stammen aus Läufen, in denen das betroffene Projekt frisch gebaut war.

### B. Logik aus den `[layout]`-Tests

Die .NET-Tests unten standen vor dem Kürzen der Browser-Tests im Code und waren grün; die neu ergänzten Zusicherungen in "'Übernehmen' schreibt den Entwurf …" liefen grün, bevor `scripts/ledger-filter-bar.test.mjs` gekürzt war.

| Browser-Test (alt) | herausgenommen | .NET-Test, der es abdeckt |
|---|---|---|
| `scripts/ledger-sort-chip.test.mjs` `[layout] Handy: Chip am Ende der Leiste, Sheet mit zehn Optionen, Betrag aufsteigend sortiert flach` | Chip-Text "Datum ↓" und "Betrag ↑" | "sortChipLabel: Standard Datum ↓, Betrag aufsteigend Betrag ↑" (LedgerSortSheetTests.fs) |
| | zehn Optionen in der Gesamtsicht, genau eine gewählt | "sortOptions true liefert zehn Optionen in fester Reihenfolge" (Kombinationen eindeutig) (LedgerSortSheetTests.fs) |
| | Tipp setzt Betrag aufsteigend, Sheet schließt, lädt neu | "SetSortExactly setzt die Sortierung genau, auch wenn sie gleich bleibt" (LedgerSortSheetTests.fs), "buildPageQuery trägt die wirksame Sortierung" (LedgerSortClientTests.fs) |
| | flache Liste, Datum in Zeile 2 | "usesDayGroups ist nur für Datum wahr, in beiden Richtungen" und "mobileRowDateText: flache Liste zeigt das Datum, gruppierte nicht" (LedgerSortClientTests.fs) |
| neu: `[layout] Handy: Chip am Ende der Leiste, das Sheet öffnet im Bild, Optionen mit 44 px Trefferfläche` | bleibt: Chip am Ende der Leiste, Sheet ganz im Bild, jede Option mindestens 44 px hoch (ersetzt den Klassenstring `min-h-[44px]` aus LedgerSortSheetTests.fs) | |
| `scripts/ledger-filter-bar.test.mjs` `[layout] Am Handy öffnet der Chip ein BottomSheet, Tippen außerhalb filtert nicht, zwei Kategorien filtern, der aktive Chip rückt vor Zu prüfen` | Schließen filtert nicht | "Esc/Schließen verwirft den Entwurf, der vorherige Filterzustand bleibt unverändert" (LedgerPageTests.fs) |
| | Chip-Text "Kategorie: 2", Kategorien in der Anfrage | "'Übernehmen' schreibt den Entwurf in den Filter, schließt das Popover und lädt ab Seite 0 neu" (LedgerPageTests.fs, neu um `chipLabel` und `buildPageQuery` ergänzt; `chipLabel` dafür öffentlich) |
| | Trefferzahl "3 Buchungen" | "Summenzeile: am PC Trefferzahl mit Klartext, am Handy nur die Trefferzahl" (LedgerReviewStateTests.fs) |
| | aktiver Chip vor Zu prüfen | "Am Handy wandert ein aktiver Kategorie-Filter vor das inaktive 'Zu prüfen' (Handoff Screen 2.4)" (LedgerViewTests.fs) |
| neu: `[layout] Am Handy öffnet der Chip ein BottomSheet statt eines Popovers` | bleibt: Sheet am Handy, kein Popover, Tippen auf das Overlay schließt | |

### Browser-Tests je Kennung

- vorher (Basislauf `npm run test:scripts` vor allen Änderungen): `Browser-Tests je Kennung: layout 66, fokus 7, tastatur 13, scroll 3, fable 7, flow 35, Summe 131`
- nachher (Gate-Lauf auf dem Endstand der Skripte): `Browser-Tests je Kennung: layout 66, fokus 7, tastatur 13, scroll 3, fable 7, flow 35, Summe 131`

Keine Kennung wächst; die zwei `[layout]`-Tests sind gekürzt, nicht ersetzt.

### C. N+1

- Sammelbefehle: `changeReviewMarkBatch` (für `setReviewMarkBatch`, `restoreReviewMarkBatch`) lädt Buchungen und Verknüpfungen der Auswahl je einmal vor der Schleife; `setEntryCategoryBatch` und `changeFlagBatch` (für `setLenaFlagBatch`, `restoreFlagBatch`) laden die Buchungen vor der Schleife und die Verknüpfungen der geänderten Buchungen einmal nach dem ersten Anhängen, an derselben Stelle wie vorher die Einzelabfragen (das Anhängen ändert keine Verknüpfung). Alles bleibt unter der Schreib-Sperre. Die Tests in `LedgerReviewMarkBatchTests.fs`, `LedgerCategoryBatchTests.fs`, `LedgerLenaFlagBatchTests.fs` und die Sperr-Tests aus ledger-kfd5m sind unverändert grün.
- Spiegel-Lauf: `MirrorBatchState.load` liest einmal je System die Verknüpfungen aller YNAB-Ids des Batches (samt Übertrags- und Split-Übertrags-Gegenbuchungen), ImportIds und gelöschten Ids (`findEntriesByLinks`) und die Buchungen dazu (`loadEntries`). `MirrorBatchState.append` schreibt wie bisher über `appendAndApply` und trägt danach `linksFrom` und `apply` je Ereignis in den Stand nach, dieselben reinen Funktionen, mit denen `applyLedgerReadModels` die Tabellen fortschreibt. `LedgerMirrorTests.fs`, `LedgerSplitWriteTests.fs` und der neue Charakterisierungstest sind unverändert grün.
- Charakterisierungstest `src/Tests/LedgerMirrorBatchTests.fs`, "neue Buchung, Treffer über YNAB-Id und ImportId, Übertragspaar und ersetzte Id schreiben dieselben Ereignisse": vor dem Umbau geschrieben, gegen den alten Code an die tatsächlichen Ereignisse angepasst (Zähler 3/2/0, ImportId-Treffer `EntryLinkedToYnab`, `EntryCategorizationChangeObservedInYnab`, `EntryApprovalChangeObservedInYnab`) und grün, nach dem Umbau ohne Änderung grün.
- Neue Row-Typen: keine. Die Lesefunktionen nutzen die vorhandenen `[<CLIMutable>]`-Typen `EntryRow` und `LinkRow`, kein neuer Optionstyp.
- Handler-Tests `src/Tests/LedgerBatchReadTests.fs` mit In-Memory-SQLite je Funktion: leere Liste, 401 Ids/Werte über die Stückgrenze, unbekannte Id fehlt (für `linksForEntries` zusätzlich gleiche Liste und Reihenfolge wie `linksForEntry`, für `findEntriesByLinks` nur Werte des gefragten Systems). Hinweis: SQLite erlaubt mehr als 400 Parameter, der 401-Test belegt das Zusammenführen der Stücke, nicht dass ein Stück ohne Teilung scheitern würde.

Gemessen (nicht geschätzt): zeitweise ein `sqlite3_trace`-Zähler in `Persistence.getConnection` und ein Mess-Test, beides vor dem Commit wieder entfernt. Gezählt sind alle SQL-Anweisungen des Befehls bzw. Laufs (Lesen und Schreiben, samt `BEGIN`/`COMMIT`), 200 Buchungen mit Kategorie, je ein Lauf allein:

| Lauf | vorher | nachher |
|---|---|---|
| Spiegel-Lauf, 200 unveränderte Beobachtungen | 405 | 7 |
| Spiegel-Lauf, 200 neue Beobachtungen | 2205 | 2006 |
| Spiegel-Lauf, 200 geänderte Beobachtungen | 2205 | 1807 |
| `setReviewMarkBatch`, 200 Buchungen | 1603 | 1205 |
| `setLenaFlagBatch` (Lena fragen), 200 Buchungen | 1603 | 1205 |
| `setEntryCategoryBatch` (ohne Kategorie), 200 Buchungen | 1604 | 1206 |

Die Leseabfragen der Schleifen fallen von 400 (zwei je Buchung oder Beobachtung) auf zwei bis vier. Der Rest ist der Schreibpfad: `appendAndApply` schreibt je geänderter Buchung Ereignis und Zustand und lädt dabei den Zustand je Buchung einzeln (`applyLedgerReadModels`), der Spiegel-Lauf hängt je Beobachtung einmal an. Das liegt außerhalb dieser Task (Kann-Sammlung unten).

### Nachtrag ADR 0014

An ADR 0014 angehängt (2026-11-07). Die Kann-Punkte stehen in ledger-k4n2s.

### Gate

`./verify.sh` im Worktree: `VERIFY: PASS (dotnet 4260 grün, node 316 grün, dotnet build, npm run build, guard:classes, guard:tokens grün)`. Der erste Gate-Lauf war rot: die neuen Tests legten 1.200 Buchungen mit jungem Datum in die geteilte In-Memory-DB und verdrängten Konto B von Seite 0 in "Ohne Konto-Filter zeigt die Seite alle Konten, jede Zeile traegt ihr Konto" (LedgerPageTests.fs). Die neuen Testdaten tragen jetzt das Buchungsdatum 01.01.2001; danach zwei volle Expecto-Läufe und das Gate grün.

## Verifier note

Iteration 1 FAIL nur am Outcome-Text (zwei Zeilen der Tabelle "Entscheidung je Test" nannten einen falschen Ersatztest). Der Conductor hat beide Zeilen korrigiert: "Jetzt spiegeln bleibt erreichbar" deckt `[layout] Handy und PC: genau ein sichtbarer Spiegel-Knopf …` ab; `aria-selected` der PC-Tabellenzeile hatte keine Aussage (Quelltext, Punkt 7). Code unverändert.
