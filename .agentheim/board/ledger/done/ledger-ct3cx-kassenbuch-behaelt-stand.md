---
id: ledger-ct3cx
title: "Kassenbuch-Liste behält Auswahl, Seiten und Scrollstand nach Spiegel-Lauf und Abruf: Nachladen im Hintergrund statt Neuaufbau, Filter entfernen überholt wartende Eingaben, ein Neuaufbau je Wechsel (Components/Ledger/State.fs resetEntries, AccountsLoaded, fetchExternalCmd)"
status: done
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-dn4vy, ledger-f8d7x]
tags: [bestandsreview, frontend, tests]
related_adrs: [0014]
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-12, Gruppe G2, Teil 1 von 2 (Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`).
Teil 2 ist ledger-nc708 (Detail und Durchgehen).

Nach jedem Spiegel-Lauf, auch dem automatischen beim Öffnen oder Zurückholen der App nach mehr als 5 Minuten, und nach
jedem Abruf baut das Kassenbuch die Liste neu auf: Skeleton, Sprung nach oben, Auswahl und Auswahl-Modus weg,
nachgeladene Seiten weg. Roman verliert mitten in der Arbeit seinen Stand. Dazu setzt ein wartendes Suchfeld einen
gerade entfernten Filter wieder.

## What
- **Neuaufbau** der Liste (Seite 0, Auswahl leer, Skeleton) gibt es nur noch, wenn sich die Abfrage ändert (Konto,
  Filter, Chip, Sortierung, „Gelöschte zeigen“), wenn noch keine Liste geladen ist (`Entries` nicht `Success`) oder
  beim Betreten der Seite von einer anderen Seite. Sonst lädt die Liste **im Hintergrund nach**.
- **Nachladen im Hintergrund** (neue Nachricht, etwa `RefreshEntries`, Antwort `EntriesRefreshed`): eine Abfrage mit
  derselben Filterung und Sortierung ab Zeile 0, so viele Zeilen wie heute geladen sind (`NextPage * pageSize`,
  `LocallyRemoved = 0`). Die Antwort ersetzt die Zeilen in der Reihenfolge des Servers: geänderte Zeilen tragen den
  neuen Stand, gelöschte und nicht mehr passende fehlen, neue stehen an ihrem Platz. Die Auswahl wird auf die noch
  vorhandenen Ids beschnitten (`Picked` als Schnittmenge, bei `AllExcept` die Ausnahmen), Auswahl-Modus bleibt, der
  Anker nur, wenn seine Zeile noch da ist. `FocusedRow` über `reconcileFocus`; ein offenes Detail bekommt den neuen
  Stand seiner Buchung, wenn sie in der Antwort steht. Trefferzahl, Summe und Zähler kommen aus der Antwort;
  `FilteredOut`, `OrderStale` und `LocallyRemoved` enden.
- Auslöser des Nachladens: das Neuladen der Konten nach einem Spiegel-Lauf bei unverändertem Filter
  (`State.fs:1273`, heute `resetEntries`), `FetchFinished` auf der Kassenbuch-Seite (`src/Client/State.fs:603`,
  heute `LoadEntries`) und „Rückgängig“ auf ein Verwerfen außerhalb des Durchgehens (`DiscardedDuplicateRestored`,
  `State.fs:4070`, Kann-Sammlung ledger-k4n2s Punkt 63).
- `RemoveMemoFilter` (1810), `RemoveAmountFilter` (1835), `ClearAllFilters` (1839) und `presetQuickFilter` (1177)
  erhöhen `MemoVersion` bzw. `AmountVersion`; `ClearAllFilters` leert dazu `MemoDraft`, `AmountMinDraft`,
  `AmountMaxDraft`.
- Ein Helfer für den Neuaufbau (`resetEntries` plus genau eine Abfrage der Seite 0). Die 22 Zweige, die heute
  `resetEntries` setzen und `LoadEntries` schicken (das in 1363 erneut `resetEntries` ruft), und `LoadEntries` selbst
  nutzen ihn; die Generation steigt je Neuaufbau um genau 1.

Entscheidungen Conductor 2026-11-12, nach Empfehlung:
- Zurück ins Kassenbuch von einer anderen Seite: wie heute neu anfangen (oben, ohne Auswahl). Nur Spiegel-Lauf, Abruf
  und das Zurückholen der App halten den Stand.
- Ist „alle“ gewählt (`AllExcept`), gehören neue Buchungen aus dem Spiegel-Lauf nach dem Nachladen zur Auswahl; Zahl
  und Summe der Auswahl-Leiste zeigen den neuen Stand.

## Acceptance criteria
- [ ] Regressionstest, vor dem Fix rot (Expecto über das echte `update`, neue Liste „Kassenbuch: Nachladen im
  Hintergrund“ in `src/Tests/LedgerPageTests.fs` oder eigene Datei `src/Tests/LedgerBackgroundRefreshTests.fs`): GIVEN
  geladene Liste mit zwei Seiten, Auswahl-Modus an, zwei Zeilen gewählt, WHEN `MirrorFinished (Ok _)` und danach die
  Konten-Antwort mit denselben Konten, THEN `Entries` bleibt `Success` mit denselben Zeilen, `Selection`,
  `SelectionMode`, `NextPage` unverändert, im Cmd steht das Nachladen im Hintergrund, kein `LoadEntries` und kein
  `RunLoadEntriesPage 0`.
- [ ] Antwort des Nachladens (Expecto über `update`, Antwort gebaut mit einer reinen Antwort-Funktion nach Vorbild
  `totalsRefreshAnswer`): GIVEN geladene Zeilen A, B, C, D, gewählt B und C, Fokus auf D, WHEN die Antwort E (neu),
  A (Betrag geändert), C liefert, THEN die Liste ist E, A mit neuem Betrag, C in dieser Reihenfolge, gewählt ist nur
  noch C, der Auswahl-Modus bleibt, der Fokus folgt `reconcileFocus`. Zweiter Fall mit `AllExcept {B}`: die Ausnahme B
  fällt weg, die Auswahl bleibt `AllExcept`, Zahl und Summe der Auswahl kommen aus der neuen Trefferzahl und Summe
  (E gehört dazu). Trefferzahl, Summe, „Zu prüfen (n)“ und „Rückfrage Lena (n)“ stammen aus der Antwort.
- [ ] Ein offenes Detail bekommt beim Nachladen den Stand seiner Buchung aus der Antwort und bleibt offen; fehlt seine
  Buchung in der Antwort, bleibt es mit dem bisherigen Stand offen (Expecto über `update`).
- [ ] GIVEN `FilteredOut = {B}`, `OrderStale = true`, `LocallyRemoved = 1`, WHEN Antwort des Nachladens, THEN
  `FilteredOut` leer, `OrderStale` falsch, `LocallyRemoved` 0, das nächste „Weitere laden“ fragt mit Versatz ab
  `NextPage * pageSize` ohne Abzug (Expecto über `update`).
- [ ] Intakt-Szenarien, belegt per Expecto über `update` (bestehende Tests in `LedgerPageTests.fs` „Erster
  Spiegel-Lauf auf leerem Kassenbuch“ und „Geladene Konten behalten…“ mitziehen): ohne geladene Liste (`NotAsked`,
  auch nach dem ersten Spiegel-Lauf auf leerem Kassenbuch, ledger-kt0wq) und nach `PresetFilter`/`PresetQuickFilter`
  (Liste steht auf `Loading`) baut die Konten-Antwort die Liste neu auf und lädt Seite 0; verschwindet das gewählte
  Konto, gilt die Vorauswahl und die Liste baut neu auf; ohne Konto `Entries = NotAsked`; `AccountsLoaded` nach
  `pageLoadCmd Ledger` (Seitenaufruf) baut wie heute neu auf.
- [ ] Nach dem Nachladen setzt ein offenes Detail das Neuladen seiner Historie ab (`reloadHistoryFor` bzw. der Helfer
  aus ledger-nc708). Beleg: Expecto über `update`, der Befehl steht im Cmd.
- [ ] Reihenfolgen (Doktrin Punkt 20, Aufzählung nach Vorbild `moveWays` in `src/Tests/MoveMoneyClientTests.fs`):
  Nachrichten Nachladen-Start, Filterwechsel (`SelectFilter` oder `ToggleOnlyNeedsReview`), `LoadMoreEntries` samt
  Antwort, Antwort des Nachladens, zweites `MirrorFinished`. Orakel über alle Reihenfolgen, berechnet aus der
  Nachrichtenfolge und nicht aus `update`: die Liste zeigt die Antwort der zuletzt gestarteten Abfrage für den
  aktuellen Filter; nie die Antwort eines abgelösten Filters, nie eine angehängte Seite aus dem Fenster vor dem
  Nachladen; nach einem Filterwechsel ist die Auswahl leer.
- [ ] Scheitert das Nachladen, bleibt die Liste mit Auswahl stehen und ein Toast (Warnung) meldet „Liste konnte nicht
  aktualisiert werden: <Grund>“ (Muster: Fehlerzweig von `EntriesPageLoaded` beim Nachladen, 1426). Beleg: Expecto
  über `update`.
- [ ] Nach einem Abruf (`FetchFinished`) auf der Kassenbuch-Seite lädt die Liste im Hintergrund nach statt neu
  aufzubauen; auf anderen Seiten schickt `fetchExternalCmd` nichts an das Kassenbuch. Beleg: Test über
  `fetchExternalCmd` bzw. Root-`update` (der bestehende Test, der dort `LoadEntries` erwartet, zieht um).
- [ ] Regressionstest, vor dem Fix rot (Expecto über `update`): GIVEN eine verworfene Duplikat-Buchung außerhalb des
  Durchgehens, WHEN `DiscardedDuplicateRestored (id, Ok ())`, THEN im Cmd steht das Nachladen im Hintergrund der Liste
  (Kann-Sammlung ledger-k4n2s Punkt 63); im Durchgehen holt es weiter die Karte zurück.
- [ ] Regressionstest, vor dem Fix rot (Expecto über `update`, bei den Filtertests in `LedgerPageTests.fs` oder
  `LedgerFilterButtonTests.fs`): GIVEN Memo-Entwurf „abc“ getippt (`SetMemoDraft`, Verzögerung läuft), WHEN
  `RemoveMemoFilter` und danach das wartende `CommitMemoFilter ("abc", v)`, THEN `Filters.Memo = None` und keine neue
  Abfrage. Derselbe Fall je für `RemoveAmountFilter` mit `CommitAmountFilter`, `ClearAllFilters` (Memo und Betrag)
  und `PresetQuickFilter`. Intakt: ohne Entfernen setzt `CommitMemoFilter` den Filter wie bisher. Nach
  `ClearAllFilters` sind `MemoDraft`, `AmountMinDraft`, `AmountMaxDraft` leer.
- [ ] Jeder Neuaufbau (Konto-, Filter-, Chip-, Sortierwechsel) läuft über einen Helfer und setzt genau eine Abfrage der
  Seite 0 ab. Beleg: der bestehende Test „Jeder Neuaufbau dreht die Generation weiter…“ in
  `src/Tests/LedgerPagingTests.fs` bleibt grün, die Tests, die `LoadEntries` im Cmd erwarten, ziehen auf die neue Form
  um. Kein eigener Test für die Zahl der Generationsschritte (reiner Umbau, Doktrin Punkt 6).
- [ ] `[scroll]` Browser-Test unter `scripts/` (API per `page.route`, Vorbild `scripts/budget-page.test.mjs:274`), am
  Handy-Breakpoint und am PC: 150 Buchungen, zweite Seite nachgeladen, bis Zeile 120 gescrollt, eine Zeile gewählt;
  dann endet ein Spiegel-Lauf (`getLastMirrorRun` älter als 5 Minuten, `mirrorYnabBudget` antwortet, danach
  `getLedgerEntriesPage` mit derselben Liste und einer geänderten Zeile). Danach steht Zeile 120 an derselben Stelle im
  Viewport und kein Skeleton ist erschienen. Auswahl und Betrag sind über die Expecto-Tests belegt und dienen hier nur
  als Wartebedingung.
- [ ] README ledger: Bullet **Auswahl** („Konto-, Filter- oder Chipwechsel leert die Auswahl“) um das Nachladen im
  Hintergrund ergänzen (Spiegel-Lauf und Abruf behalten die Auswahl, beschnitten auf die noch vorhandenen Buchungen;
  bei „alle“ gehören neue dazu), ebenso die Stelle, die den Spiegel-Lauf im Kassenbuch beschreibt. Den veralteten
  Kommentar in `src/Client/State.fs:235` („Kein automatischer Spiegel-Lauf“) korrigieren.

## Notes
Urteile (geprüft an HEAD 98f69c32):
- `State.fs:1273` `AccountsLoaded` ruft `resetEntries`: gilt, weiter als beschrieben. Der Spiegel-Lauf startet von
  Hand (`StartMirror`) oder aus `MirrorStatusLoaded`, sobald der letzte Lauf älter als 5 Minuten ist
  (`freshnessThreshold`, `Types.fs:2482`); `MirrorStatusLoaded` kommt beim Betreten des Kassenbuchs und bei jedem
  PWA-Vordergrund auf `Ledger`/`LedgerEntry` (`VisibilityChanged`, `src/Client/State.fs:655`). Beim Öffnen nach mehr
  als 5 Minuten heißt das: Liste lädt, Sekunden später Skeleton, Sprung nach oben, zweites Laden. Der automatische
  Abruf alle 6 h (banking-import-t4raw) läuft nur im Server; der Client hat keinen Push-Kanal und erfährt nichts
  davon, er löst `AccountsLoaded` nicht aus. Derselbe Verlust nach „Abrufen“ von Hand über `FetchFinished` →
  `LoadEntries`. Verloren gehen heute `Selection`, `SelectionAnchor`, `SelectionMode`, nachgeladene Seiten,
  `SwipedAway`, `FilteredOut`, `OrderStale`, `LocallyRemoved` und der Scrollstand (Skeleton mit 6 Zeilen,
  `View.fs:2950`). Das offene Detail bleibt offen, zeigt aber den alten Stand. Das Durchgehen bleibt unberührt.
- `State.fs:1810` Filter entfernen ohne Version: gilt; dieselbe Lücke in `presetQuickFilter` (1177).
- `State.fs:1348` doppeltes `resetEntries`: gilt; 22 Zweige (1285, 1325, 1350, 1354, 1440, 1446, 1451, 1456, 1651,
  1658, 1684, 1690, 1733, 1737, 1787, 1791, 1808, 1812, 1833, 1837, 1842, 3372, 3540), Generation +2, eine Abfrage.

Hinweise für den Worker:
- Seitenaufruf (`pageLoadCmd Ledger` schickt `LoadAccounts`) und Lauf-Ende (`MirrorFinished` schickt heute dasselbe)
  trennen: `MirrorFinished` bekommt eine eigene Nachricht für das Neuladen der Konten, deren Antwort nur neu aufbaut,
  wenn sich der aufgelöste Filter ändert oder keine Liste geladen ist.
- Die Antwort des Nachladens braucht eine eigene Bindung an die Liste beim Start; ein Filterwechsel während des
  Nachladens gewinnt. Zählt das Nachladen `EntriesGeneration` hoch, verlieren laufende `TotalsRefreshed` und
  `PayeeSuggestionsLoaded` ihre Gültigkeit; der Kommentar bei `payeeSuggestionsAnswer` (460) warnt vor einer
  Endlosschleife des Payee-Popovers. Besser eine eigene Lade-Nummer für das Nachladen neben `EntriesGeneration`.
- Läuft beim Start des Nachladens ein „Weitere laden“: warten oder dessen Antwort verwerfen; die Aufzählung legt nur
  das Orakel fest.
- Unter .NET wirft der Proxy: Abfrage hinter eine eigene `Run*`-Nachricht, Antwort in eine reine Funktion (Vorbild
  `entriesPageAnswer`, 455). `getLedgerEntriesPage` hat serverseitig keine Obergrenze für `PageSize`
  (`QueryHandlers.fs:714`).
- Bleibt `Entries` `Success`, baut React die Zeilen nur um (`prop.key` je Id); Chrome hält per `overflow-anchor` die
  sichtbaren Zeilen, auch wenn oben neue erscheinen.
- `LoadEntries` bleibt für äußere Aufrufer (`CapturedDiscarded` 3093, `EntryDeleteFinished` 3125,
  `EntryRestoreFinished` 3152, `ReviewMarkBatchDone` 3508), dort ohne vorheriges `resetEntries`. Ob diese im
  Hintergrund nachladen, ist nicht Teil dieser Task.
- Tests, die heute `LoadEntries` im Cmd erwarten (22 Stellen, u. a. `LedgerFilterButtonTests.fs`,
  `LedgerSortClientTests.fs`, `LedgerSortSheetTests.fs`, `LedgerSelectionTests.fs`,
  `LedgerReviewMarkBatchClientTests.fs`, `LedgerCaptureClientTests.fs`, `LedgerEntryDeletionClientTests.fs`,
  `OverviewTests.fs`), ziehen mit.
- „Alle außer“ lösen die Sammelbefehle serverseitig über die Abfrage auf (`BatchAllExcept`, 873); Buchungen aus dem
  automatischen Abruf gehören schon heute dazu. Die Entscheidung oben macht das sichtbar.

Überschneidungen:
- ledger-k4n2s Punkt 63 (Rückgängig auf Verwerfen lädt die Liste nicht nach) geht hier mit; beim Abschluss in der
  Kann-Sammlung als erledigt vermerken.
- ledger-k4n2s Punkt 67 (G7: `updateCore` zerlegen, Durchgehen nach `Components/Review/`) verschiebt dieselben Zweige:
  erst danach bauen.
- ledger-ng110: Befund `State.fs:1816` (unlesbarer Betrag im Filter) liegt in denselben Betragsfilter-Zweigen;
  nacheinander bauen.
- ledger-f8d7x baut Filter-Sheet und Chips, deren Entfernen die Zweige hier ruft; übernimmt die neuen Nachrichten statt
  eigener Lade-Wege.
- ledger-nc708 (Teil 2) ändert das Innere von `reloadHistoryFor`; dieser Teil ruft es nur.

## Verifier note (iteration 1)

REASONS:
- Bei „alle“ (`AllExcept`) kann eine abgewählte Buchung durch das Nachladen wieder in die Auswahl rutschen und dann von einer Sammelaktion getroffen werden. `applyEntriesRefresh` (`| AllExcept excluded -> AllExcept (Set.intersect excluded present)`) entfernt jede Ausnahme, deren Id nicht in der Antwort steht; die Antwort umfasst aber nur ein Fenster von `NextPage * pageSize` Zeilen (`refreshRequest`). Bringt der Spiegel-Lauf eine neue Buchung oben in die Liste, fällt die letzte geladene Zeile aus dem Fenster, obwohl sie noch zum Treffer gehört. Hat Roman genau diese Zeile unter „alle“ abgewählt, ist die Ausnahme weg, und `reviewMarkBatchSelection` (`BatchAllExcept (query, Set.toList excluded)`) schickt sie nicht mehr mit. „Geprüft für die Auswahl“ oder „Kategorie setzen“ schreibt dann auf eine abgewählte Buchung, der Push geht nach YNAB. Neue Regression, verfälscht Daten.
- Das Beschneiden bringt dort nichts: `selectionTotals` zieht nur geladene Ausnahmen ab, der Server ignoriert Ids, die nicht mehr existieren. Für das Herausschieben aus dem Fenster gibt es keinen Test; die Aufzählung „Reihenfolgen beim Nachladen im Hintergrund“ prüft `AllExcept` nicht. Bei `Picked` dasselbe in die harmlose Richtung: eine gewählte Zeile, die aus dem Fenster geschoben wird, verliert still ihre Auswahl.
- README_DELTA (Spiegel-Lauf-Op, letzter Satz): „Neu auf baut sie nur, wenn …“ ist kein Satz; besser „Neu aufgebaut wird sie nur, wenn …“. Die Code-Bedingung nennt auch eine Liste, die noch lädt (`Entries` nicht `Success`).

SUGGESTED_FIX: Ausnahmen (und gewählte Ids) bei `AllExcept`/`Picked` nur dann fallen lassen, wenn die Antwort den ganzen Treffer abdeckt (Länge der Antwort kleiner als die angefragte Fenstergröße bzw. gleich TotalCount). Sonst Ausnahmen und gewählte Ids behalten, die nicht in der Antwort stehen, weil sie hinter dem Fenster liegen können. Dazu ein Expecto-Test: unter „alle“ wird die letzte geladene Zeile abgewählt, das Nachladen liefert oben eine neue Buchung und schiebt sie aus dem Fenster, danach steht sie weiter in `excluded` von `reviewMarkBatchSelection`; ebenso für `Picked`. Die Aufzählung um `AllExcept` mit einer Ausnahme am Fensterende erweitern. README-Satz glätten.

ITERATION_HINT: likely-fixable

## Outcome

Die Kassenbuch-Liste lädt nach einem Spiegel-Lauf (auch dem automatischen beim Öffnen oder Zurückholen der App), nach einem Abruf und nach „Rückgängig“ auf ein Verwerfen außerhalb des Durchgehens im Hintergrund nach. Auswahl, Auswahl-Modus, nachgeladene Seiten und Scrollstand bleiben stehen. `VERIFY: PASS` nach Iteration 2 (dotnet 4802 grün, node 318 grün).

Umsetzung:
- `src/Client/Components/Ledger/Types.fs`: neue Nachrichten `RefreshAccounts`/`AccountsRefreshed` (Konten nach dem Lauf, getrennt vom Seitenaufruf `LoadAccounts`) und `RefreshEntries`/`RunRefreshEntries`/`EntriesRefreshed (generation, refresh, result)`; neue Felder `RefreshGeneration` (eigene Lade-Nummer neben `EntriesGeneration`, damit laufende Summen- und Payee-Abfragen gültig bleiben), `Refreshing`, `RefreshQueued`.
- `src/Client/Components/Ledger/State.fs`: `MirrorFinished` schickt `RefreshAccounts`. `AccountsRefreshed` lädt im Hintergrund nach, wenn der aufgelöste Filter gleich bleibt und eine Liste steht; sonst gilt der Weg von `AccountsLoaded` (Neuaufbau, Vorauswahl, `NotAsked` ohne Konto). Reine Funktionen `entriesRefreshAnswer`, `refreshRequest` (Seite 0, `NextPage * pageSize` Zeilen, `LocallyRemoved = 0`) und `pageRequest`, die die `Run*`-Zweige selbst nutzen. `applyEntriesRefresh` ersetzt die Zeilen in Server-Reihenfolge, beschneidet `Picked`/`AllExcept` auf die vorhandenen Ids, aber nur wenn die Antwort den ganzen Treffer umfasst (Korrektur in Iteration 2, siehe unten), hält den Anker nur mit seiner Zeile, Fokus über `reconcileFocus`, frischt ein offenes Detail auf und lädt seine Historie neu (`reloadHistoryFor`, nicht im Durchgehen). Zähler kommen aus der Antwort, `FilteredOut`, `OrderStale` und `LocallyRemoved` enden. Fehler: Liste bleibt, Warnung „Liste konnte nicht aktualisiert werden: <Grund>“. Während des Nachladens ruht `LoadMoreEntries`; läuft „Weitere laden“ schon, wartet das Nachladen (`RefreshQueued`) und startet nach der Antwort der Seite. `DiscardedDuplicateRestored` außerhalb des Durchgehens schickt `RefreshEntries`, im Durchgehen holt es wie bisher die Karte zurück (ledger-k4n2s Punkt 63, in der Kann-Sammlung als erledigt vermerken). `RemoveMemoFilter`, `RemoveAmountFilter`, `ClearAllFilters` und `presetQuickFilter` erhöhen `MemoVersion` bzw. `AmountVersion`; `ClearAllFilters` leert dazu `MemoDraft`, `AmountMinDraft`, `AmountMaxDraft`. Neuer Helfer `rebuildEntries`/`rebuildWith` (`resetEntries` plus genau `RunLoadEntriesPage 0`): die 22 Neuaufbau-Zweige und `LoadEntries` selbst nutzen ihn, die Generation steigt je Neuaufbau um 1. `LoadEntries` bleibt für die äußeren Aufrufer. Die Start-Zweige `StartUndo*`, `StartRetry*` und `StartSetReviewMark` (Parallel-Task ledger-smv1s) sind nicht angefasst.
- `src/Client/State.fs`: `fetchExternalCmd` schickt auf der Kassenbuch-Seite `RefreshEntries` statt `LoadEntries`; `syncNavAccounts` zieht auch `AccountsRefreshed` in die Seitenleiste; der veraltete Kommentar „Kein automatischer Spiegel-Lauf“ in `pageLoadCmd` ist korrigiert.
- `src/Client/Components/Ledger/View.fs`: der Sentinel für „Weitere laden“ ruht während des Nachladens. Dazu kam ein Fund aus dem Browser-Test: der Hinweis „Spiegel läuft“ stand als bedingtes erstes Kind in einer keylosen Liste. Fiel er am Ende des Laufs weg, verschoben sich alle folgenden Kinder, React baute die Buchungsliste neu auf, und die gescrollte Zeile sprang um 36 px. Jetzt hält `Html.none` den Platz.

Tests:
- `src/Tests/LedgerBackgroundRefreshTests.fs` (neu): Liste „Kassenbuch: Nachladen im Hintergrund“ (13 Tests: der Regressionstest Spiegel-Lauf mit zwei Seiten, Auswahl-Modus und zwei gewählten Zeilen; die Antwort E/A/C mit `Picked` und `AllExcept`; Detail mit und ohne Buchung in der Antwort samt `LoadHistory`; Ende von `FilteredOut`/`OrderStale`/`LocallyRemoved` mit dem Versatz des nächsten „Weitere laden“; Fehler-Toast; verworfene Antworten; Warten auf „Weitere laden“; Regressionstest `DiscardedDuplicateRestored`). Dazu die Liste „Kassenbuch: Konten nach dem Spiegel-Lauf“ (7 Intakt-Szenarien: `NotAsked`, `PresetFilter`, `PresetQuickFilter`, verschwundenes Konto, ohne Konto, Seitenaufruf baut weiter neu auf, Seitenleiste) und die Aufzählung „Reihenfolgen beim Nachladen im Hintergrund“ (180 Reihenfolgen aus Nachladen-Start, `ToggleOnlyNeedsReview`, `LoadMoreEntries` samt Antwort, Antwort des Nachladens und zweitem `MirrorFinished`. Das Orakel kommt aus der Nachrichtenfolge und einem gemockten Server: Präfix der aktuellen Antwort für den aktuellen Filter, keine doppelte Zeile, Seitenzeiger passt zur Liste, Auswahl leer nach Filterwechsel, sonst beschnitten). Gegenprobe: ohne die Sperre von „Weitere laden“ während des Nachladens und ohne das Warten auf „Weitere laden“ wird die Aufzählung je rot. Vor dem Fix (mit Stubs für die neuen Nachrichten) waren alle 13 Tests der ersten Liste rot.
- `src/Tests/LedgerPageTests.fs`: vier Regressionstests (Memo, Betrag, „Alle Filter löschen“ samt leeren Eingabefeldern, Eintrag aus „Zu tun“). Gegenprobe: ohne die Versionserhöhungen werden genau diese vier rot. Intakt bleibt der bestehende Test „Eine Memo-Eingabe erhöht die Version …“. Nachgezogen sind „Ein gelungener Spiegel-Lauf laedt die Konten neu“ (jetzt `RefreshAccounts`) sowie „Geladene Konten behalten …“ und „Verschwindet das gewaehlte Konto …“ (jetzt `AccountsRefreshed`).
- Nachgezogen von `LoadEntries` auf `RunLoadEntriesPage 0`: `LedgerPageTests.fs`, `LedgerFilterButtonTests.fs`, `LedgerLenaFlagClientTests.fs`, `LedgerReviewMarkBatchClientTests.fs` (Rückgängig), `LedgerDetailTests.fs` (Route), `LedgerSortSheetTests.fs`, `LedgerSortClientTests.fs`, `OverviewTests.fs` (Konto aus der Seitenleiste). Der Abruf-Test in `OverviewTests.fs` erwartet jetzt `RefreshEntries`. `RetryTests.fs`: das Inventar der lesenden Abfragen ist um `getLedgerAccounts` (RefreshAccounts) und `getLedgerEntriesPage` (RunRefreshEntries) ergänzt. Der Test „Jeder Neuaufbau dreht die Generation weiter …“ in `LedgerPagingTests.fs` bleibt unverändert grün.
- `scripts/ledger-background-refresh.test.mjs` (neu): zwei `[scroll]`-Tests bei 412x915 (Touch) und 1440x900. 150 Buchungen, zweite Seite nachgeladen, Zeile 120 in die Mitte gescrollt und gewählt. Dann endet der zurückgehaltene Spiegel-Lauf, und der Server liefert Zeile 121 geändert. Geprüft wird, dass der DOM-Knoten von Zeile 120 bestehen bleibt, kein Skeleton erscheint und die Zeile auf 1 px genau an derselben Stelle im Viewport steht. Gegenprobe: mit `LoadAccounts` statt `RefreshAccounts` in `MirrorFinished` sind beide Tests rot. Vor der `Html.none`-Änderung im View waren beide auch mit dem Nachladen rot (Knoten neu, Sprung um 36 px).

Merge mit main (ledger-smv1s, Zeilen-Sperre, und ledger-xqqnv): der einzige Konflikt lag in `src/Client/Components/Ledger/State.fs` vor `updateCore`, wo `applyEntriesRefresh` und `releaseDeciding` an derselben Stelle standen. Beide Funktionen bleiben unverändert nebeneinander stehen. Geprüft auf Wechselwirkung: die Sperrgruppen (`RowLock`, `rowBusy`, `whenRowIdle`) lesen nur `CategoryRows`, `ReviewMarkRows`, `FlagRows`, `MemoRows`, `AmountRows`, `PushRetryRows` und `Review.Confirming`. Weder das Nachladen im Hintergrund noch `resetEntries`/`rebuildEntries` fassen diese Felder an, und die Start-Zweige über `whenRowIdle` lösen kein Nachladen aus. `DiscardedDuplicateRestored` stammt allein aus dieser Task (smv1s ändert ihn nicht), `ReviewLena` bleibt unberührt. Die neuen Nachrichten (`RefreshEntries` usw.) ändern keine Zeilen-Sperre, `src/Tests/LedgerRowLockWaysTests.fs` braucht sie deshalb nicht. `VERIFY: PASS` über den kombinierten Stand (dotnet 4799 grün, node 318 grün).

Eine Skizze gibt es nicht, das Kriterium zum Skizzenvergleich entfällt (Hinweis des Conductors).

Korrektur nach Verifier-FAIL (Iteration 1):
- Fehler: `applyEntriesRefresh` beschnitt `AllExcept` und `Picked` auf die Ids der Antwort. Die Antwort umfasst aber nur das Fenster `NextPage * pageSize`. Brachte der Spiegel-Lauf oben eine neue Buchung, rückte die letzte geladene Zeile hinter das Fenster; war sie unter „alle“ abgewählt, fiel ihre Ausnahme weg, und `reviewMarkBatchSelection` schickte sie nicht mehr als Ausnahme mit. „Geprüft für die Auswahl“ oder „Kategorie setzen“ hätte die abgewählte Buchung getroffen und nach YNAB gepusht.
- Fix in `src/Client/Components/Ledger/State.fs` (`applyEntriesRefresh`): fehlende Ids fallen nur noch aus Auswahl und Ausnahmen, wenn die Antwort den ganzen Treffer umfasst (`List.length entries >= page.TotalCount`). Sonst bleiben sie, weil sie hinter dem Fenster liegen können. Eine gelöschte gewählte Buchung bleibt dann bis zum nächsten vollständigen Nachladen in `Picked`; der Server ignoriert Ids, die es nicht mehr gibt, und die Anzeige zählt nur geladene Zeilen.
- Regressionstests in `src/Tests/LedgerBackgroundRefreshTests.fs`, alle drei vor dem Fix rot (Gegenprobe mit `coversHit = true`: genau diese drei rot): „Unter „alle“ bleibt eine Ausnahme, die das Nachladen aus dem Fenster schiebt“ (100 Zeilen geladen, 100 abgewählt, die Antwort bringt 1000 oben und endet bei 99; danach steht 100 in `AllExcept` und in den `excluded` von `BatchAllExcept`), „Eine gewählte Zeile, die das Nachladen aus dem Fenster schiebt, bleibt gewählt“ (`Picked`), und in der Liste „Reihenfolgen beim Nachladen im Hintergrund“ die neue Aufzählung „Rückt die letzte geladene Zeile hinter das Fenster, behält sie über alle Reihenfolgen Wahl und Ausnahme“: 12 Reihenfolgen aus Nachladen-Start, Filterwechsel, Antwort und zweitem Spiegel-Lauf, je für `Picked {3, 5, 199}` und `AllExcept {199}`, gegen einen Server mit drei neuen Buchungen oben (Buchung 199 rückt hinter das Fenster). Nach jedem Schritt bleibt 199 gewählt bzw. abgewählt, am Ende steht 199 im Stapel-Auftrag. Die gemockte Welt trägt dafür ihren Server als Feld; die bestehende Aufzählung (180 Reihenfolgen, Server mit einer neuen Buchung) läuft unverändert.
- README_DELTA nachgezogen: der Satz im Spiegel-Lauf lautet jetzt „Neu aufgebaut wird sie nur, wenn das gewählte Konto verschwunden ist oder noch keine Liste geladen ist oder noch lädt.“; Auswahl und Nachladen im Hintergrund nennen die Regel zum Beschneiden, `Picked`/`AllExcept` heißen „gewählte Zeilen“ bzw. „abgewählte Zeilen“ (nicht mehr nur geladene). Geprüft mit `npm run check:readme-delta` (Exit 0) und `grep` nach „beschnitten“ und „geladene Zeilen“ in der README.
- Hinweis zur Integration: `main` ist seit dem Merge im Worktree (c19248ba) weitergelaufen (ledger-vfr4h u. a.). Die Dateiliste ist gegen die Merge-Basis (`git diff main...HEAD`) bestimmt.

Für die Kann-Sammlung:
- `presetQuickFilter` erhöht die Versionen, leert aber `MemoDraft`/`AmountMinDraft`/`AmountMaxDraft` nicht. Das Eingabefeld kann nach einem Eintrag aus „Zu tun“ alten Text zeigen, obwohl kein Filter gilt.
- `RefreshEntries` bei `Entries = Loading` (etwa ein Abruf, während ein Neuaufbau läuft) baut erneut auf, statt auf die laufende Abfrage zu warten. Das kostet eine doppelte Abfrage der Seite 0, das Ergebnis stimmt.
- Kommt die Antwort eines Nachladens nach der Antwort eines Zeilen-Befehls, dessen Abfrage aber davor gestartet war, zeigt die Zeile kurz den Server-Stand vor dem Befehl, bis der nächste Lauf nachzieht. Denselben Wettlauf hatte der frühere Neuaufbau auch.
- Die äußeren `LoadEntries`-Aufrufer (`CapturedDiscarded`, `EntryDeleteFinished`, `EntryRestoreFinished`, `ReviewMarkBatchDone`) bauen weiter neu auf. Ob sie im Hintergrund nachladen sollen, ist nicht Teil dieser Task.
- Wenn das Nachladen eine gewählte oder abgewählte Zeile hinter das Fenster schiebt, stimmt die angezeigte Auswahl-Zahl nicht mehr genau: `selectionTotals` zieht bei `AllExcept` nur geladene Ausnahmen ab (eine zu viel), bei `Picked` zählt es nur geladene Zeilen (eine zu wenig). Der Stapel-Auftrag selbst ist richtig. Nach „Weitere laden“ bis zur Zeile stimmt es wieder.
- Antwortet „Weitere laden“ gegen einen Server, der sich seit der ersten Seite verschoben hat (neue Buchungen oben nach einem Spiegel-Lauf), kann die angehängte Seite Zeilen doppelt zeigen, bis das wartende Nachladen (`RefreshQueued`) die Liste ersetzt. Das Anhängen filtert bereits geladene Ids nicht heraus.
