---
id: ledger-ct3cx
title: "Kassenbuch-Liste behält Auswahl, Seiten und Scrollstand nach Spiegel-Lauf und Abruf: Nachladen im Hintergrund statt Neuaufbau, Filter entfernen überholt wartende Eingaben, ein Neuaufbau je Wechsel (Components/Ledger/State.fs resetEntries, AccountsLoaded, fetchExternalCmd)"
status: backlog
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
