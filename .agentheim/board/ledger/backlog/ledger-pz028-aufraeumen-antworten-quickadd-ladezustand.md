---
id: ledger-pz028
title: "Kassenbuch-State aufräumen: drei Antworten tragen Result, das Quick-Add-Konto kommt aus den Settings des Root statt aus einem eigenen Abruf, die toten Kategorien-Zweige fallen weg, „Weitere laden“ und Nachladen teilen einen Zustand statt LoadingMore (Components/Ledger/State.fs, Types.fs)"
status: backlog
type: refactor
context: ledger
created: 2026-11-19
completed:
depends_on: [ledger-y8xrr, ledger-pfhdq, design-system-001-styleguide]
blocks: []
tags: [bestandsreview, frontend, tests]
related_adrs: []
related_research: []
prior_art: [ledger-ct3cx]
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G3 (Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`), Teil 3 von 4.
Teil 1 ist ledger-y8xrr (Liste behält Stand nach eigenen Aktionen), Teil 2 ledger-pfhdq (späte und gescheiterte
Antworten), Teil 4 ledger-awf5b (Betragsfilter und Update-Neustart). Diese Task wartet auf Teil 1 und 2, weil sie
dieselben Zweige (`RefreshEntries`, Kategorien) anfasst.

Vier Stellen weichen von `.claude/rules/elmish-state.md` ab: Antworten ohne `Result`, deren Fehler im `Cmd` verschwindet;
ein eigener `getSettings`-Aufruf für einen Wert, den der Root schon hält; zwei Zweige, die nie laufen; ein Bool neben
dem Zustand des Nachladens, der ungültige Kombinationen zulässt. Sichtbares Verhalten ändert sich nicht.

## What
- **Antworten mit `Result`** (Vorbild `CategorySuggestionsLoaded`, Types.fs:2517):
  `CapturePayeeCategoryLoaded of payee * Result<YnabCategoryId option, string>` (Types.fs:2564, Erzeuger State.fs:3404),
  `ReviewSuggestionsLoaded of Guid * Result<YnabCategoryId list, string>` (Types.fs:2775, State.fs:4444),
  `ReviewMatchLoaded of Guid * Result<LedgerEntryView option, string>` (Types.fs:2778, State.fs:4458). Der Fehlerzweig
  im `Cmd` reicht `Error ex.Message` durch, die Entscheidung fällt im `update`. Verhalten bleibt: bei
  `CapturePayeeCategoryLoaded (_, Error _)` gilt „keine Kategorie gefunden“, ein wartendes „Buchung anlegen“ startet
  wie heute ohne Kategorie; `ReviewSuggestionsLoaded (_, Error _)` lässt die Vorschläge leer; `ReviewMatchLoaded (_, Error _)`
  lässt den Schnappschuss aus dem Ereignis stehen.
- **Quick-Add-Konto aus einer Quelle**: `QuickAddAccountId: YnabAccountId option` (Types.fs:1623) wird
  `QuickAddAccount: RemoteData<YnabAccountId option>`. Der Root fängt `LedgerMsg LoadQuickAddAccount` ab (wie
  `LoadCategories`, `src/Client/State.fs:819`) und füllt den Wert aus `model.Settings.Settings`: `Success` sofort; ohne
  geladene Settings wartet er auf `SettingsLoaded` (ein `LoadSettings`, wenn keines läuft), ein Fehler wird `Failure`.
  Der eigene Aufruf `Api.settings.getSettings` in State.fs:3632 und `QuickAddAccountLoaded` entfallen. Die View
  (`View.fs:1979`, `SplitLogic.cashbackTarget`) liest den Wert aus `RemoteData`; ohne Wert bleibt die Übertrag-Zeile wie
  heute leer und wählbar, kein Toast.
- **Tote Zweige**: die inneren Zweige `LoadCategories` (State.fs:1991) und `EnsureCategoriesFresh` (2014) laufen nie, der
  Root fängt beide vorher ab (`src/Client/State.fs:813`, `819`). Sie werden zu einem Zweig ohne Zustandsänderung mit
  einem Satz Kommentar, wohin der Root sie leitet. Der Test `LedgerCategoryChangeClientTests.fs:637` („LoadCategories
  nach einem Fehlschlag schaltet auf Loading“) prüft den toten Zweig und zieht auf das Root-`update` um.
- **Ladezustand der Liste**: `LoadingMore: bool` (Types.fs:1432) und `Refresh: RefreshState` (1343, 1696) werden ein
  Zustand, der nur gültige Kombinationen kennt (etwa `ListActivity = ListIdle | LoadingMorePage | LoadingMoreThenRefresh
  | Refreshing of basis`). Heute ist `Refresh = RefreshAfterPage` bei `LoadingMore = false` darstellbar.
  `refreshRunning` (Types.fs:3066) und `resetEntries` lesen bzw. setzen den neuen Zustand.

Entscheidungen Conductor 2026-11-19, nach Empfehlung:
- `MirrorRunning: bool` (Types.fs:1491) bleibt. Es ist die Doppeldruck-Sperre des Befehls `mirrorYnabBudget`, kein
  Ladezustand neben `LastMirrorRun` (das gehört zu `getLastMirrorRun`, einem anderen Aufruf). Ein Bool reicht dort wie
  bei den Sperr-Mengen `DeletingEntries` und `RestoringEntries`.
- Das Quick-Add-Konto kommt aus den Root-Settings, nicht aus `LedgerBookingDefaults.QuickAddAccountId`: dieses Feld ist
  die Guid des Kassenbuch-Kontos (`Shared/Domain.fs:1199`), die Übertrag-Zeile braucht die YNAB-Konto-Id
  (`YnabAccountId`, wie in `AppSettingsView`, Domain.fs:595/622).

## Acceptance criteria
- [ ] Expecto über `update` je Antwort, für `Ok` und `Error` (Dateien `src/Tests/LedgerCaptureClientTests.fs` und
  `src/Tests/LedgerReviewClientTests.fs`): GIVEN „Buchung anlegen“ wartet auf die Kategorie des Payees, WHEN
  `CapturePayeeCategoryLoaded (payee, Error "x")`, THEN startet das Anlegen ohne Kategorie (wie heute mit `None`).
  GIVEN eine Karte im Durchgehen, WHEN `ReviewSuggestionsLoaded (id, Error "x")`, THEN sind die Vorschläge leer; WHEN
  `ReviewMatchLoaded (matchId, Error "x")`, THEN bleibt der Treffer aus dem Ereignis. Die bestehenden Tests ziehen auf
  die neue Form um und bleiben inhaltlich gleich.
- [ ] Root-`update` (Expecto, `src/Tests/LedgerSplitEditorClientTests.fs` oder dort, wo `LoadQuickAddAccount` heute
  getestet wird): GIVEN Settings geladen mit Quick-Add-Konto K, WHEN `LedgerMsg LoadQuickAddAccount`, THEN
  `Ledger.QuickAddAccount = Success (Some K)` ohne Aufruf. GIVEN Settings laden noch, WHEN `LoadQuickAddAccount` und
  danach `SettingsLoaded (Ok …)` mit K, THEN `Success (Some K)` und genau ein `LoadSettings`. GIVEN Settings
  gescheitert, THEN `Failure` und die Übertrag-Zeile ohne Ziel (Cashback-Ziel `None`).
- [ ] `grep -rn "getSettings" src/Client/Components/Ledger/` ist leer; `QuickAddAccountLoaded` gibt es nicht mehr.
- [ ] Der umgezogene Test „LoadCategories nach einem Fehlschlag schaltet auf Loading“ läuft über das Root-`update` und
  ist grün; im Kassenbuch-`update` setzen `LoadCategories` und `EnsureCategoriesFresh` keinen Zustand.
- [ ] `grep -rn "LoadingMore" src/` findet nur noch den neuen Zustand (kein Feld `LoadingMore: bool`); die Aufzählungen
  „Reihenfolgen beim Nachladen im Hintergrund“ in `src/Tests/LedgerBackgroundRefreshTests.fs` und die Tests zu
  „Weitere laden“ in `src/Tests/LedgerPageTests.fs` und `LedgerPagingTests.fs` bleiben inhaltlich gleich und grün. Kein
  neuer Test für den Umbau selbst (Doktrin Punkt 6).
- [ ] README ledger: wo `LoadingMore` oder `RefreshAfterPage`/`RefreshState` genannt sind, den neuen Namen eintragen
  (`grep -n "RefreshAfterPage\|RefreshState\|LoadingMore" .agentheim/knowledge/contexts/ledger/README.md`). Delta
  vor der Integration durch `npm run check:readme-delta`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteile (geprüft an HEAD f98a8ce6):
- Befund 6 (State.fs:3632 eigener `getSettings`-Aufruf, Fehler geschluckt 3643): bestätigt. Angepasst: nur die
  Root-Settings liefern denselben Wert; `LedgerBookingDefaults.QuickAddAccountId` ist ein anderer (Guid des
  Kassenbuch-Kontos statt `YnabAccountId`).
- Befund 7 (Types.fs:2564, 2775, 2778 ohne `Result`): bestätigt, Erzeuger State.fs:3404/3405, 4444/4445, 4458/4459.
- Befund 9 (State.fs:1991, 2014 unerreichbar): bestätigt, Root fängt in `src/Client/State.fs:813` und `819` ab. Ein
  Test ruft den toten Zweig direkt (`LedgerCategoryChangeClientTests.fs:637`), er zieht um.
- Befund 10 (Types.fs:1432, 1516, 1491): angepasst. `CategoriesRefreshing` erledigt ledger-pfhdq. `LoadingMore` wird
  hier mit `Refresh` zusammengelegt. `MirrorRunning` entfällt: anderer Aufruf als `LastMirrorRun`, eine Befehlssperre
  (siehe Entscheidungen).
- Befund 8 erledigt ledger-pfhdq.
