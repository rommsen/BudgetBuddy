---
id: ledger-r8h19
title: "Aufräum-Lauf ledger: Quelltext-, Klassenstring- und Konstanten-Tests ersetzen oder löschen, Logik aus [layout]-Tests nach .NET, N+1 in Sammelbefehlen und Spiegel-Lauf"
status: doing
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
- [ ] Für jeden Test aus der Tabelle in A und jeden Quelltext-Test in `LedgerCategoryChangeClientTests.fs` und `LedgerFilterButtonTests.fs` ist entschieden: gelöscht, gehoben oder mit Begründung behalten. Das Outcome hat eine Tabelle je Test: Entscheidung und der Test (Name und Datei), der das Verhalten heute abdeckt, oder "keine Aussage (Konstante, Klassenstring, Quelltext)" mit Doktrin-Punkt.
- [ ] Jede gehobene Entscheidung (mindestens `LedgerSelectionTests.fs` Umschaltung, `LedgerKeyboardTests.fs` :387 und :397, `LedgerCategoriesAwaitSettingsTests.fs` :106) ist eine reine Funktion im Produktionscode, die View oder `update` nur aufruft; je Funktion Tests für alle Zweige und im Outcome eine Mutante, die den neuen Test rot macht.
- [ ] Der Root-`update` ruft `getCategories` über eine injizierte Funktion; der Ersatz für :106 prüft am Reducer, dass mit Settings `Success` und Budget der Abruf startet und das Warten endet (Cmd über Lambda, Doktrin Punkt 1).
- [ ] Ein Verhaltenstest der Payee-Vorschläge deckt einen Umlaut-Fall ab (erfundene Payees, die sich nur in Groß- und Kleinschreibung eines Umlauts unterscheiden, werden zusammengefasst); er wird rot, wenn `loadPayeeSuggestions` eine eigene Faltung statt `Core.searchFold` nutzt, die Umlaute anders behandelt.
- [ ] Nach dem Lauf liest kein Test in den genannten Dateien Client-Quelltext per Teilstring, außer den begründet behaltenen; keine Tailwind-Größe als Klassenstring in diesen Dateien.
- [ ] `scripts/ledger-sort-chip.test.mjs` und `scripts/ledger-filter-bar.test.mjs`: die `[layout]`-Tests treffen nur noch Lage-Aussagen; die herausgenommene Logik hat je einen .NET-Test (Client-`update` oder reine Funktion), der vor dem Entfernen aus dem Browser-Test existiert und grün ist.
- [ ] Die Meldung "Browser-Tests je Kennung" aus `npm run test:scripts` steht vorher und nachher im Outcome; keine Kennung wächst.
- [ ] Sammelbefehle: `changeReviewMarkBatch`, `setEntryCategoryBatch` und `changeFlagBatch` laden Buchungen und Verknüpfungen je Auswahl einmal (bis auf Stücke zu 400). Die bestehenden Tests in `LedgerReviewMarkBatchTests.fs`, `LedgerCategoryBatchTests.fs` und `LedgerLenaFlagBatchTests.fs` bleiben ohne inhaltliche Änderung grün, die Sperr-Tests aus ledger-kfd5m ebenso.
- [ ] Neue Lesefunktionen in `Ledger.Persistence` haben Handler-Tests mit In-Memory-SQLite: leere Liste, mehr als 400 Ids (Stückgrenze), unbekannte Id fehlt im Ergebnis.
- [ ] Spiegel-Lauf: Vor dem Umbau ein Charakterisierungstest, der für einen Batch mit neuer Buchung, Treffer über YNAB-Id, Treffer über ImportId, Übertragspaar im selben Batch und ersetzter Split-Id (ADR 0058 Punkt 5) die entstehenden Ereignisse festhält; er ist vor und nach dem Umbau grün. `LedgerMirrorTests.fs` bleibt ohne inhaltliche Änderung grün.
- [ ] Das Outcome nennt die gemessene Zahl der Datenbankabfragen für einen Sammelbefehl mit 200 Buchungen und einen Spiegel-Lauf mit 200 Beobachtungen, vor und nach dem Umbau (gemessen, nicht geschätzt; kein Strukturtest dafür).
- [ ] Neue Row-Typen tragen `[<CLIMutable>]`; neue Optionstypen haben einen registrierten `OptionHandler<'T>`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

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
