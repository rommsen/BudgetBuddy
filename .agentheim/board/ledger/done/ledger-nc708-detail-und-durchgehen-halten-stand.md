---
id: ledger-nc708
title: "Detail und Durchgehen halten ihren Stand: Historie an Ladevorgänge gebunden ohne Flackern, jede Änderung an der offenen Buchung lädt ihre Historie nach, gescheitertes „Lena fragen“ holt die Karte zurück (Components/Ledger/State.fs LoadHistory, HistoryLoaded, LenaFlagSet)"
status: done
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: [design-system-001-styleguide, ledger-smv1s]
blocks: []
tags: [bestandsreview, frontend, tests]
related_adrs: [0086]
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-12, Gruppe G2, Teil 2 von 2 (Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`).
Teil 1 ist ledger-ct3cx (Liste behält Stand).

Die Historie im Detail flackert bei jedem Nachladen auf „Historie wird geladen…“, eine späte Antwort überschreibt eine
neuere, und nach Kategorie, Übertrag, Split, Lena oder Prüfvermerk fehlt die neue Zeile, bis das Detail neu geöffnet
wird. Scheitert „Lena fragen“ im Durchgehen, ist die Karte weg und der Fehler steht an einer Buchung, die Roman nicht
mehr sieht.

## What
- `LoadHistory`/`HistoryLoaded` (`State.fs:1498` bis 1515) tragen eine Lade-Nummer (Model-Feld, etwa
  `HistoryGeneration`); nur die Antwort des zuletzt gestarteten Ladens zählt. Beim Nachladen derselben Buchung bleibt
  die angezeigte Historie stehen, bis die Antwort da ist; beim Öffnen einer anderen Buchung gilt `Loading` wie heute.
  Vorbild `CategoryHistoryLoaded` in `src/Client/Components/Budget/State.fs`.
- Ein gemeinsamer Helfer nach einer Änderung der offenen Buchung (etwa `afterEntryChange`, ersetzt `reloadHistoryFor`
  und `afterAmountChange`, 679 und 755) wird von jeder erfolgreichen Antwort eines Einzelbefehls gerufen:
  `EntryCategorySet` (2089), `EntryCategoryRestored` (2128), `EntryTransferSet` (2170), `CategorizationRetried`
  (2213), `LenaFlagSet` (2280), `FlagRestored` (2318), `FlagRetried` (2347), `ReviewMarkSet` (1869),
  `ReviewMarkRetried` (1896), `ReviewMarkRestored` (2003), `SwipeReviewedDone` (1970), `PushRetried` (1926),
  `MemoRetried` (2492), `AmountRetried` (2662), `SplitsSaved` (3240), `SplitsRestored` (3317) und die heutigen
  Memo-, Betrag-, Payee- und Split-mit-Betrag-Zweige.
- `LenaFlagSet (entryId, _, Error _)` (2296) holt im Durchgehen die Karte der Buchung zurück (`returnToReviewCard`),
  Fehlerzeile und Toast wie heute.

## Acceptance criteria
- [ ] Regressionstest, vor dem Fix rot (Expecto über das echte `update`, `src/Tests/LedgerDetailTests.fs`): GIVEN
  Detail von A offen, Historie `Success [h1]`, WHEN zweimal `LoadHistory A` gestartet und die Antwort des zweiten
  (`[h1; h2; h3]`) vor der des ersten (`[h1; h2]`) ankommt, THEN die Historie zeigt `[h1; h2; h3]`. Die Antworten
  baut eine reine Antwort-Funktion, die die Lade-Nummer beim Start bindet.
- [ ] Regressionstest, vor dem Fix rot (Expecto über `update`): GIVEN Detail von A offen mit `Success [h1]`, WHEN
  `LoadHistory A`, THEN die Historie bleibt `Success [h1]` bis zur Antwort. Intakt: WHEN Detail von B öffnet, THEN
  `Loading`, und eine späte Antwort für A ändert nichts; scheitert das Nachladen bei vorhandenem Stand, bleibt der
  Stand (`RemoteData.lateFailure` wie heute).
- [ ] Reihenfolgen (Doktrin Punkt 20, Aufzählung nach Vorbild `moveWays` in `src/Tests/MoveMoneyClientTests.fs`):
  Nachrichten zwei `LoadHistory A`, deren zwei Antworten, `CloseDetail`, Öffnen von B. Orakel über alle Reihenfolgen,
  aus der Nachrichtenfolge berechnet: ist A offen, zeigt die Historie die Antwort des letzten gestarteten Ladens von A
  oder den alten Stand, solange sie fehlt; ist B offen, nie eine Antwort für A; ist das Detail zu, `NotAsked`.
- [ ] Regressionstest, vor dem Fix rot (Expecto über `update`, Tabellentest in `src/Tests/LedgerDetailTests.fs`): für
  jede der oben genannten Antworten gilt: ist die Buchung im Detail offen, setzt die erfolgreiche Antwort das Neuladen
  ihrer Historie ab; ist eine andere oder keine Buchung offen, nicht. Die Tabelle nennt jede Nachricht einzeln
  (Orakel ist die Liste, nicht der Helfer). Ein Fall prüft die Karte im Durchgehen mit „Details“ offen.
- [ ] Regressionstest, vor dem Fix rot (Expecto über `update`, `src/Tests/LedgerReviewClientTests.fs`): GIVEN
  Durchgehen mit Karten A und B, Karte A sichtbar, WHEN `ReviewLena` und danach
  `LenaFlagSet (A, AssignLena, Error "…")`, THEN Karte A ist wieder sichtbar, `FlagRows` zeigt für A den Fehler, der
  Toast „Flag nicht geändert: …“ erscheint. Intakt: außerhalb des Durchgehens ändert der Fehler nur Zeile und Toast;
  nach `ReviewLeft` passiert am Durchgehen nichts; nach Erfolg bleibt B sichtbar.
- [ ] Reihenfolgen „Lena im Durchgehen“ (Doktrin Punkt 20): `ReviewLena` auf A, `ReviewSkip` oder `ReviewConfirm` auf
  B samt Antwort, Fehlerantwort für A, letzte Karte erreicht (Endansicht). Orakel: kommt die Fehlerantwort für A,
  solange das Durchgehen läuft, ist danach A sichtbar, auch aus der Endansicht; eine später ankommende Antwort für B
  schiebt die Karte nicht weiter.
- [ ] README ledger, Bullet **Durchgehen**: der Satz zu „Lena fragen“ (weiter, Toast mit „Rückgängig“) nennt den
  Fehlerfall („Scheitert es, kommt die Karte zurück“).

## Notes
Urteile (geprüft an HEAD 98f69c32):
- `State.fs:1499` `HistoryLoaded` nur an die Buchungs-Id gebunden: gilt (Zeile im Code 1498 bis 1510). `LoadHistory`
  setzt `History = Loading` (`View.fs:1524` zeigt dann „Historie wird geladen…“), `HistoryLoaded` prüft nur
  `DetailFor.Id`.
- `State.fs:2089` Historie nach Änderung: gilt. `reloadHistoryFor` rufen heute nur Memo, Betrag, Payee und Split mit
  Betrag. Die Historie listet jedes Ereignis der Buchung (`Ledger.QueryHandlers.getEntryHistory`,
  `src/Server/Ledger/QueryHandlers.fs:816`). Der Regelbezug §7.11 trägt nur schwach (der Abschnitt regelt die Form);
  der Befund gilt über die Uneinheitlichkeit.
- `State.fs:3954` Lena im Durchgehen: gilt. `ReviewLena` geht nach `StartSetLenaFlag` sofort weiter
  (`advanceReview`), der Fehlerzweig 2296 setzt nur `FlagRows` und Toast. `returnToReviewCard` (1243) existiert und
  wird von `StartUndoFlag` schon so benutzt.

Hinweise für den Worker:
- Batch-Befehle laufen ohne offenes Detail (`OpenDetail` ist bei Auswahl gesperrt, 1461); dort kein Neuladen nötig.
- `showDetail` (1152) und `ReviewShowDetails` (4085) laden beim Öffnen; dort gilt weiter `Loading`.
- `ReviewLena` mit `WithdrawLena` geht nicht weiter (3964); `returnToReviewCard` tut bei gleicher Karte nichts (1248).
- Toast beim Lena-Fehler bleibt: er meldet das Ergebnis einer Aktion, keine Eingabe (Styleguide §5,
  `.claude/rules/task-ui-vorgaben.md`).

Überschneidungen:
- ledger-smv1s ändert die `Start*`-Zweige direkt neben den Antwortzweigen hier (`StartUndoFlag` 2302 neben
  `LenaFlagSet` Error 2296, `StartUndoCategory` 2114, `StartUndoSplit` 3303); deshalb `depends_on`.
- ledger-ct3cx (Teil 1) ruft nach dem Nachladen der Liste `reloadHistoryFor`; ist ct3cx zuerst gebaut, ersetzt der
  Helfer hier auch diesen Aufruf.
- ledger-f8d7x baut die Historie im Seitenpanel in Form §7.11 um (`View.fs`); dieser Teil ändert nur den Zustand.
- ledger-k4n2s Punkt 67 (G7: Durchgehen nach `Components/Review/`) verschiebt die Review-Zweige: erst danach bauen.

## Outcome

Historie, Nachladen nach Änderungen und der Lena-Fehler im Durchgehen sind gebaut. `./verify.sh` im Worktree: VERIFY: PASS (dotnet 4863 grün, node 318 grün). Gegenprobe: mit zurückgedrehtem Fix (Lena-Fehler ohne `returnToReviewCard`, `LoadHistory` immer `Loading`, Antwort ohne Lade-Nummer) waren die neuen Regressionstests und beide Aufzählungen rot.

- **Lade-Nummer**: `Model.HistoryGeneration` (`src/Client/Components/Ledger/Types.fs`), `HistoryLoaded of Guid * int * Result<…>`. Die reine Antwort-Funktion `historyAnswer entryId generation` bindet Buchung und Nummer beim Start, `isCurrentHistory` prüft beides (`src/Client/Components/Ledger/State.fs`). `LoadHistory` zählt die Nummer weiter und lässt einen `Success`-Stand bis zur Antwort stehen, sonst gilt `Loading`. `showDetail` und `ReviewShowDetails` setzen vorher `Loading` wie bisher. Ohne offenes Detail der Buchung tut `LoadHistory` nichts. Der Aufruf an den Server steht in einem Lambda, damit `update (LoadHistory …)` unter .NET läuft (Vorbild `tabDataCmd` im Budget).
- **`afterEntryChange`** ersetzt `reloadHistoryFor` und wird jetzt auch von `EntryCategorySet`, `EntryCategoryRestored`, `EntryTransferSet`, `CategorizationRetried`, `LenaFlagSet`, `FlagRestored`, `FlagRetried`, `ReviewMarkSet`, `ReviewMarkRetried`, `ReviewMarkRestored`, `SwipeReviewedDone`, `PushRetried`, `MemoRetried`, `AmountRetried`, `SplitsSaved` und `SplitsRestored` gerufen (Ok-Zweige). Im Durchgehen lädt es nur bei offenem „Details“ nach, denn die Karte allein zeigt keine Historie. Die bestehenden Zweige für Memo, Betrag, Payee und Split mit Betrag behalten ihre Bedingung „nur bei Änderung“. `afterAmountChange` bleibt als Bündel aus `afterEntryChange` und `LoadBalances`, weil es zusätzlich die Kontostände holt.
- **Lena im Durchgehen**: `LenaFlagSet (_, _, Error _)` ruft `returnToReviewCard`, auch aus der Endansicht. Fehlerzeile und Toast „Flag nicht geändert: …“ bleiben wie bisher. Außerhalb des Durchgehens und nach `ReviewLeft` ändert sich nur die Zeile.
- **Tests**: `src/Tests/LedgerDetailTests.fs` (`historyGenerationTests`, Aufzählung `historyWalkTests` über alle 180 gültigen Reihenfolgen aus zwei `LoadHistory A`, ihren Antworten, `CloseDetail` und dem Öffnen von B, Tabelle `afterChangeTests` mit 24 einzeln genannten Antworten und dem Fall der Karte mit „Details“); `src/Tests/LedgerReviewClientTests.fs` (`lenaFailureTests`, Aufzählung `lenaWalkTests` bis Tiefe 6 mit Endansicht). Angepasst wurden zwei Tests, die das alte Verhalten festhielten: `LedgerSplitEditorClientTests` („kein Folgebefehl“ heißt jetzt „nur `LoadHistory`“) und der Zeitgeber-Test in `LedgerCategoryChangeClientTests`, der über das neue `Tests.CmdTiming.expectAfterIgnoring` das sofortige `LoadHistory` überspringt. Die Signatur von `HistoryLoaded` ist in `LedgerDetailTests` und `LedgerPageTests` nachgezogen.
- Kein Skizzenvergleich (die Task hat keine Skizze). An View.fs und an den Filter-Zweigen (ledger-dn4vy) hat sich nichts geändert.

Für die Kann-Sammlung:
- Memo, Betrag und Payee laden die Historie nur bei `…Changed` nach, alle anderen Antworten bei jedem Ok. Ein einheitliches „bei jedem Ok“ würde die Ausnahme beseitigen, dafür müssten einige bestehende Tests angepasst werden.
- `LenaFlagSet` Error prüft nicht, ob die Antwort aus demselben Durchgehen stammt (anders als `releaseDeciding`). Startet Roman ein neues Durchgehen, bevor die alte Antwort ankommt, springt die Karte auf A, wenn A in der neuen Schlange steht.
