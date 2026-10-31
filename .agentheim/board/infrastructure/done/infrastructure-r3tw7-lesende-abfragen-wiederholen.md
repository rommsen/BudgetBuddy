---
id: infrastructure-r3tw7
title: "Alle übrigen lesenden Abfragen laufen über Retry.reading, damit ein Verbindungsabbruch im Kassenbuch keine Fehlerseite mehr zeigt"
status: done
type: bug
context: infrastructure
created: 2026-10-31
completed:
depends_on: []
blocks: []
tags: [retry, netzwerk, firefox, kassenbuch, regression]
related_adrs: []
related_research: []
prior_art: [infrastructure-y6atr, infrastructure-awstw]
---

## Why

Roman, 2026-10-31: Auch im Kassenbuch erscheinen ständig Fehlerseiten, die nach dem Tipp auf "Erneut versuchen" sofort weg sind. infrastructure-y6atr hat nur vier Start-Abfragen geschützt, die Buchungsliste selbst nicht.

## What

Diese lesenden Aufrufe laufen über `Retry.reading`:

- `src/Client/Components/Ledger/State.fs`: `getLedgerAccountBalances`, `getLedgerEntriesPage` (zwei Stellen), `getEntryHistory`, `getLedgerPayeeSuggestions`, `getPayeeCategorySuggestions`, `getBudgetAccounts`, `getSettings`, `getLedgerEntry`
- `src/Client/State.fs`: `getCategories`, `getRecentQuickAddTemplates`
- `src/Client/Components/Rules/State.fs`: `getAllRules`

Schreibende Aufrufe bleiben ungeschützt. `SyncFlow/State.fs` gehört zu ynab-sync-k3vq8.

## Acceptance criteria

- [ ] Kaputt-Szenario, GIVEN der erste `getLedgerEntriesPage`-Aufruf bricht ab (`route.abort`, Status 0), WHEN das Kassenbuch lädt, THEN erscheint die Liste ohne Fehlerseite. Der Browser-Wächter ist vor dem Fix rot und nennt im Kommentar diesen Bug.
- [ ] Intakt-Szenario: Scheitern alle drei Versuche, erscheint die Fehlerseite mit "Erneut versuchen" wie bisher.
- [ ] Alle oben genannten Aufrufe laufen über `Retry.reading`, kein schreibender Aufruf.
- [ ] `.agentheim/knowledge/contexts/infrastructure/README.md`, Bullet "Lesende Abfrage mit Wiederholung", nennt den neuen Umfang.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes

Die Ursache der Abbrüche klärt infrastructure-g4wy8. Dieser Task lindert die Wirkung und hängt nicht daran.

## Verifier note (iteration 1)

VERDICT: FAIL

REASONS:
- Im Browser bewiesen: `Retry.reading` an `getLedgerEntriesPage` (`src/Client/Components/Ledger/State.fs:1021`) führt eine Regression ein. Eine verspätete Wiederholung der alten Abfrage ersetzt die neuere Liste. `EntriesPageLoaded` (State.fs:1027, 1056, 1061) trägt nur die Seitennummer, es gibt keine Generation, keinen Abgleich mit Filter oder Sortierung, und `resetEntries` entwertet laufende Abfragen nicht.
- Beleg (Probe `scripts/zz-stale.test.mjs` in der Verifier-Kopie): Klick auf Sortierung "Betrag" (Anfrage "alle" bricht ab), sofort danach Klick auf "Testkonto 1". (a) Der dritte Versuch gelingt: Unter dem aktiven Filter "Testkonto 1" stehen Buchungen aller Konten, ohne Hinweis. (b) Alle drei Versuche scheitern: Die fertige Konto-Liste verschwindet, die Fehlerseite erscheint. Ohne `Retry.reading` sind beide Szenarien grün. Die Regression stammt aus diesem Diff.
- Dieselbe Klasse, aus dem Quelltext: Nachlade-Seite (`page > 0`): Die verspätete Antwort des alten Filters wird an die neue Liste angehängt (State.fs:1031) und setzt `NextPage` und `TotalCount`. `PayeeSuggestionsLoaded` (State.fs:1395) hat keinen Abgleich. `TotalsRefreshed` (State.fs:2250) prüft nur `OrderStale`. `EntryRefreshed` (State.fs:2228) ersetzt ohne Abgleich. `HistoryLoaded` und `CategorySuggestionsLoaded` haben Wächter und sind in Ordnung.
- Nachrangig: Der Inventartest in `src/Tests/RetryTests.fs` ist ein Strukturtest für eine gerade gefixte Stellenliste, Doktrin 7 will den Regressionstest auf der Verhaltensebene. Der Browser steht offen und ist billig (Probe `scripts/zz-cheap.test.mjs`, tabellengetrieben, je unter 1 s).
- Überlebender Mutant m4: `(* Retry.reading Api.ledger.getBudgetAccounts *) Api.ledger.getBudgetAccounts` bleibt grün, die Regex liest auch Kommentare.
- Bestanden: `VERIFY: PASS`, jede genannte Stelle umhüllt, keine schreibende, `getCategories` und `getRecentQuickAddTemplates` ohne Schreibpfad, beide Browser-Wächter grün, README-Delta passt.

SUGGESTED_FIX: Das Modell bekommt einen Zähler (z. B. `EntriesGeneration`), den jeder Neuaufbau der Liste in `LoadEntries` erhöht. `RunLoadEntriesPage` gibt ihn in `EntriesPageLoaded` mit, Antworten einer alten Generation werden verworfen (Ok und Error, Seite 0 und Nachladen). `RunRefreshTotals` und die Payee-Vorschläge bekommen denselben Schutz, `EntryRefreshed` einen passenden Abgleich. Regressionstests auf der Ebene Client-`update` unter .NET. Im Inventartest Kommentare vor dem Abgleich entfernen oder die Ladewege im Browser tabellengetrieben belegen.

ITERATION_HINT: likely-fixable

## Outcome

`Retry.reading` umhüllt in `src/Client/Components/Ledger/State.fs` die Abfragen `getLedgerAccountBalances`, `getLedgerEntriesPage` (beide Stellen), `getEntryHistory`, `getLedgerPayeeSuggestions`, `getPayeeCategorySuggestions`, `getBudgetAccounts`, `getSettings` und `getLedgerEntry`, in `src/Client/State.fs` `getCategories` (Rules-Zweig, der Ledger-Zweig war schon geschützt) und `getRecentQuickAddTemplates`, in `src/Client/Components/Rules/State.fs` `getAllRules`. Kein schreibender Aufruf läuft darüber. `SyncFlow/State.fs` und `Components/Settings/` blieben unberührt.

Iteration 2 behebt die Regression, die der Verifier im Browser bewiesen hat: Die Wiederholung verlängert das Fenster, in dem eine Antwort unterwegs ist, auf bis zu 1,6 Sekunden, und eine verspätete Antwort ersetzte die neuere Liste. Das Modell trägt jetzt `EntriesGeneration`, `resetEntries` erhöht sie, also jeder Neuaufbau, auch der Wechsel auf gar keine Liste (`AccountsLoaded` ohne Filter läuft dafür jetzt durch `resetEntries`). `EntriesPageLoaded`, `TotalsRefreshed` und `PayeeSuggestionsLoaded` tragen die Generation ihres Starts.

Ergebnis je Stelle (Klasse: verspätete Antwort ersetzt neuere Daten):

- `getLedgerEntriesPage` Liste: betroffen, behoben. Alte Generation wird verworfen, Ok und Error, Seite 0 und Nachladen. Kein Ladezustand bleibt: `resetEntries` setzt `LoadingMore` zurück, die aktuelle Abfrage beendet `Loading` selbst.
- `getLedgerEntriesPage` Summen: betroffen, behoben. Generation zusätzlich zu `OrderStale`.
- `getLedgerPayeeSuggestions`: betroffen, behoben. Alte Generation wird verworfen. Dabei wäre ein Hänger entstanden, weil nicht jeder Neuaufbau neu fragt: Wartet das offene Popover nur noch auf die alte Antwort, fragt es neu, ein geschlossenes geht auf `NotAsked`. Spätes Scheitern derselben Generation lässt `Success` stehen.
- `getLedgerEntry`: betroffen, behoben. `EntryRefreshed` trägt die Zeile beim Start (`basis`) und gilt nur, wenn die Zeile unverändert ist.
- `getLedgerAccountBalances`: spätes Scheitern ersetzte fertige Kontostände, behoben mit `RemoteData.lateFailure`.
- `getEntryHistory`: Wächter auf die Buchung war da. Spätes Scheitern beim erneuten Öffnen derselben Buchung ersetzte die Historie, behoben mit `lateFailure`.
- `getBudgetAccounts`: spätes Scheitern, behoben mit `lateFailure`.
- `getAllRules`: spätes Scheitern ersetzte die Liste und meldete einen Toast, behoben, ein fertiger Stand bleibt ohne Toast.
- `getRecentQuickAddTemplates`: spätes Scheitern, behoben mit `lateFailure`.
- `getCategories` Ledger-Zweig: nicht betroffen, `CategoriesLoaded (Error ...)` behält einen vorhandenen Stand schon, die Antwort hängt von keinem Filter ab.
- `getCategories` Rules-Zweig: nicht betroffen, Error ändert nichts, Ok liefert dieselbe Liste.
- `getSettings` (Quick-Add-Konto): nicht betroffen, Error ändert nichts, die Antwort hängt von keinem Filter ab.
- `getPayeeCategorySuggestions`: Wächter auf den offenen Picker ist da. Bewusst nicht geändert: Spätes Scheitern beim erneuten Öffnen desselben Pickers leert die Vorschläge. Der bestehende Test "ein Fehler lässt den Abschnitt weg" verlangt das, der Picker bleibt benutzbar.

`lateFailure` gilt, weil jede dieser Abfragen vorher `Loading` setzt: Ein `Success` zum Zeitpunkt des Scheiterns stammt von einer neueren Abfrage. Hingenommen: Zwei Abfragen derselben Generation können sich überholen, wie schon vor dieser Task. Eine Wiederholung liest den Stand erst zum Zeitpunkt des Versuchs, das Risiko wächst dadurch nicht.

Tests: Regressionstests auf Client-`update` in `src/Tests/LedgerPageTests.fs`, Liste "Verspätete Antworten" (19 Fälle), und `RemoteData.lateFailure` in `src/Tests/RemoteDataTests.fs`. Rot vor dem Fix: 18 von damals 20 Fällen rot, die zwei Intakt-Fälle grün. Die zwei Stale-Fälle aus der Verifier-Vorlage stehen in `scripts/ledger-read-retry.test.mjs` ohne `waitForTimeout`, beide vor dem Fix rot (Fehlerseite, Zeile der alten Abfrage). Kaputt- und Intakt-Szenario aus Iteration 1 bleiben dort, rot vor dem Fix waren beide.

Beleg je Aufrufstelle: Der Inventartest in `src/Tests/RetryTests.fs` liest keine Kommentare mehr, mit Gegenbeweis für Block- und Zeilenkommentar. Tabelle im Browser ("Aufrufstellen im Browser", zählt nur einen Aufruf nach der Pause des Helfers): `getLedgerAccountBalances`, `getCategories` (Kassenbuch), `getAllRules`, `getRecentQuickAddTemplates`, `getEntryHistory`, `getLedgerPayeeSuggestions`, dazu `getLedgerEntriesPage` (Ladeweg) im Kaputt-Szenario. Nur im Inventar, also Quelltext-Abgleich ohne Verhaltensbeleg: `getBudgetAccounts`, `getSettings` (Quick-Add-Konto), `getPayeeCategorySuggestions`, `getLedgerEntry`, `getLedgerEntriesPage` (Summen), `getCategories` (Regeln). Eine neu hinzukommende ungeschützte Lesestelle erkennt das Inventar nicht. `entryRefreshAnswer` und `totalsRefreshAnswer` sind reine Funktionen, damit die Bindung an den Startzustand unter .NET prüfbar ist.

Gegenproben in einer eigenen Kopie ohne `.env`, ein Lauf zur Zeit, Kontrolle grün. Gilt nur für diese Mutantenliste, alle getötet:

Generationsvergleich entfernt je für Ok Seite 0, Error Seite 0, Ok Nachladen, Error Nachladen, Summen, Vorschläge Ok, Vorschläge Error; Abgleich entfernt bei `EntryRefreshed`; Generation nicht erhöht; `AccountsLoaded` ohne Filter ohne Reset; Popover fragt nicht neu; geschlossenes Popover bleibt `Loading`; `lateFailure` entfernt bei Kontoständen, Historie, Vorschlägen, Überweisungszielen, Quick-Add-Vorlagen; Regel-Wächter entfernt; m4 (auskommentierter Helfer an `getBudgetAccounts`); feste Generation in der Mitgabe bei Liste Ok, Liste Error, Vorschläge Ok, Vorschläge Error, Summen; `basis` fest `None`; `Retry.reading` entfernt bei `getLedgerAccountBalances`, `getCategories` (Kassenbuch), `getAllRules`, `getRecentQuickAddTemplates`, `getEntryHistory`, `getLedgerPayeeSuggestions`.

Überlebende gab es unterwegs vier, alle sind behoben und getötet: Vergleich entfernt bei Vorschläge Error (Tests decken jetzt Ok und Error), feste Generation bei Vorschläge Error (neuer Browser-Test "Ein Fehler der Vorschläge beendet das Laden ohne neue Abfrage"), feste Generation bei Summen und `basis` fest `None` (per reine Funktionen unter .NET). Keine offenen Überlebenden in dieser Liste. Der Mutantenlauf der Liste lief vor dem Herausziehen der beiden reinen Funktionen; danach wiederholt für Summen und `EntryRefreshed`, die übrigen Zeilen blieben unverändert.

Der Knopf der Fehlerseite heißt im Code "Try Again" (`ErrorDisplay.cardWithTitle`), die Task sagt "Erneut versuchen". Die Task verlangt das Verhalten wie bisher, der Text blieb, Folgeticket design-system-t7kqz.

Offen: `getCategories` in `src/Client/Components/SyncFlow/State.fs` läuft am Start des Kassenbuchs weiter ungeschützt (SyncFlow gehört zu ynab-sync-k3vq8). `testRule` und `exportRules` in `Rules/State.fs` laufen ohne Helfer, die Task nennt sie nicht. Die aus früheren Tasks umhüllten `getLedgerAccounts` und `getLastMirrorRun` wurden nicht auf diese Klasse geprüft, sie liegen außerhalb dieser Liste.

Läufe: `./verify.sh` ergab VERIFY: PASS (dotnet 3010 grün, node 378 grün, dotnet build, npm run build, guard:classes, guard:tokens grün). `npm run check:readme-delta -- --bc infrastructure` Exit 0.

**Vom Verifier ergänzt:** Der Prüfhinweis in der README, dass verspätete Antworten bei jeder neuen Umhüllung zu prüfen sind, ist prose-only, unenforced. Die Aussage "Eine Wiederholung liest den Stand erst zum Zeitpunkt des Versuchs" ist ungenau: Die Query ist zum Start eingefroren, siehe infrastructure-p9kd4. Überlebt haben die Verdrahtungsmutanten w1 und w2: In `RunRefreshEntry` und `RunRefreshTotals` ist die Zeile `(Ok >> answer)` ungedeckt, die Logik dahinter ist rein getestet.
