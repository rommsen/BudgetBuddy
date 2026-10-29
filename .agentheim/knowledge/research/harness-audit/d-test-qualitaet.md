# Test-Qualität in BudgetBuddy

Datum: 2026-10-29. Stand: `main` (ed3a6ab). Rein lesende Analyse, keine Code-Änderung, kein Testlauf.

## Vorbemerkung zur Methode

- Gelesen: `src/Tests/*.fs`, `src/Tests/Tests.fsproj`, `src/Tests/test.runsettings`, `scripts/*.test.mjs` (Kopf und Stichproben), `src/Shared/Api.fs`, `src/Server/Api.fs` (Endpoint-Rümpfe und `webApp`), `src/Server/BotApi.fs` (Routing, Token-Vergleich), `src/Server/Validation.fs` (Signaturen), `src/Server/Persistence.fs` (DB-Konfiguration, Schlüssel), `.agentheim/board/protocol.md` und `protocol/*.md`, `git log`.
- Zählungen per Skript: Testfall = Zeile, die mit `testCase "`, `test "`, `testProperty "` oder `testCaseAsync "` beginnt. Hilfsfunktionen, die `testCase` umdefinieren (z. B. `LedgerSplitReadTests.fs:548`), werden dadurch nicht doppelt gezählt; in Schleifen erzeugte Fälle werden nur einmal gezählt.
- Transkripte (241 `.jsonl`-Dateien, 506 MB) per Python-Skript nach `dotnet test`-Zusammenfassungen und Fehlernamen durchsucht. Die Ausgaben in Transkripten sind oft gekürzt, die Zahlen sind daher Untergrenzen.
- Projektdoktrin (`standards/testing/*.md`, `.claude/rules/testing.md`) erst am Ende gelesen (Abschnitt 10).
- Kennzeichnung: **bestätigt** heißt im Code nachvollzogen, **Verdacht** heißt plausibel, aber nicht zu Ende geprüft.

## 1. Inventar

### F# (Expecto, ein Testprojekt)

`src/Tests/Tests.fsproj`: net10.0, Expecto 10.2.1, Expecto.FsCheck, YoloDev-Adapter. Referenziert Shared, Server **und Client** (Client-State und View-Code laufen unter .NET). 95 Compile-Einheiten, davon 94 Testdateien, rund 36 900 Zeilen.

| Kennzahl | Wert |
|---|---|
| Testfälle gesamt | 2463 (1654 `testCase`, 806 `test`-CE, 3 `testProperty`) |
| `testList` | 445 |
| `testSequenced` | 47 Aufrufe in 23 Dateien |
| `Expect.*`-Aufrufe | 5691 (davon `equal` 3310, `isFalse` 510, `isTrue` 472, `stringContains` 467) |
| `failtest` | 494 |
| übersprungen pro Lauf | 6 (Live-Integrationstests, siehe Abschnitt 8) |

Nach Bereich (Dateizuordnung per Namensmuster):

| Bereich | Dateien | Testfälle |
|---|---|---|
| Ledger Server (Befehle, Abfragen, Spiegel, Ereignisse) | 18 | 642 |
| Ledger Client (State, View) | 25 | 675 |
| Sync/Import (Comdirect, Duplikate, Push, Split, SyncFlow-View) | 15 | 377 |
| Client allgemein und Design-System | 16 | 278 |
| QuickAdd, Validation, Encryption, Persistence-Typen | 4 | 162 |
| Budget | 4 | 93 |
| YNAB-Client | 2 | 84 |
| Regeln | 3 | 67 |
| Bot | 2 | 65 |
| Infrastruktur-Wächter | 5 | 20 |

Größte Dateien: `LedgerPageTests.fs` (163 Fälle, 3189 Zeilen, mischt Client und Server), `LedgerViewTests.fs` (122), `LedgerTests.fs` (103), `SyncFlowViewTests.fs` (84), `YnabClientTests.fs` (78), `QuickAddTests.fs` (74). Nur 3 Property-Tests im ganzen Projekt.

### Node (`npm run test:scripts`)

39 Dateien `scripts/*.test.mjs`, rund 270 `test(`-Aufrufe (Protokoll nennt 278, `protocol.md:489`). Zwei Arten:

- Rund 28 Browser-Wächter (Playwright, Chromium) gegen den gebauten Client mit per `page.route` gemockten API-Antworten (Beispiel `scripts/ledger-memo.test.mjs:1-12`).
- Werkzeug-Tests für Skripte: `readme-delta-check`, `run-guards`, `screenshot-route`, `static-serve`, `tailwind-class-guard`, `token-guard`, `export-design-system`, `dockerfile-tzdata`, `pwa-update`.

`scripts/run-guards.mjs` baut den Client einmal und lässt die Dateien mit Parallelität 4 laufen. Eine separate Playwright-Konfiguration oder E2E-Tests gegen einen echten Server gibt es nicht.

## 2. Tautologien

**Methode:** alle 2463 Testkopfzeilen per `grep -n` über `ls *.fs | sort` gelistet, jede 40. Zeile ab der ersten gezogen (NR % 40 == 1). Das ergibt 62 Testfälle aus 52 Dateien. Jeder Testkörper wurde gelesen. Reproduzierbar mit
`grep -nE '^\s*(testCase|test|testProperty|testCaseAsync) "' $(ls *.fs | sort) | awk 'NR%40==1'` in `src/Tests`.

**Ergebnis der Stichprobe (n = 62):**

| Klasse | Anzahl | Quote | Beispiele |
|---|---|---|---|
| Verhaltenstest mit echter Logik | 49 | 79 % | `BotApiTests.fs:135` (Cache-Ablauf mit injizierter Uhr), `LedgerCategoryChangeTests.fs:454`, `TargetsApplyTests.fs:352` |
| Strukturtest (Quelltext, Doku oder Klassen-String) | 11 | 18 % | `LedgerSplitEditorClientTests.fs:680`, `SyncFlowViewTests.fs:582`, `FontFaceRegressionTests.fs:133` |
| Tautologie im engen Sinn | 1 | 1,6 % | `LedgerViewTests.fs:892`: prüft, dass die Konstante `ListRoles.mobileLine1` den Teilstring `h-5` enthält, also die Konstante gegen ihren eigenen Inhalt |
| nahezu tautologisch | 1 | 1,6 % | `SyncSessionManagerTests.fs:132`: Setter aufrufen, Getter lesen, gleiches Feld vergleichen |

Tautologie-Quote also 2 von 62 (3,2 %, 95-%-Wilson-Intervall rund 1 % bis 11 %).

**Gezielte Suche außerhalb der Stichprobe** (Namensmuster `stores`, `contains all`, `can be created`, `documents`, `Always pass`) fand eine Häufung in den ältesten, englischsprachigen Dateien aus der Juni-Phase:

- Immer grün, ohne Aussage: `YnabClientTests.fs:984`, `:1010`, `:1024` ("documents ...", Körper nur Kommentar und `()`), `ComdirectIntegrationTests.fs:145` ("documents manual testing procedure", `// Always pass` in `:174`).
- Record bauen und Felder zurücklesen: `ComdirectIntegrationTests.fs:198` (ApiKeys), `:207` (Challenge), `:216` (AuthSession), `SplitTransactionTests.fs:48`.
- Testet selbst gebauten Wert statt Produktionscode: `ComdirectClientTests.fs:121` und `:141` (rechnen die Request-Id im Test selbst aus), `:149` (prüft `Guid.NewGuid()`), `:156`.

Zusammen rund 13 Fälle, etwa 0,5 % der Suite. Die neueren deutschsprachigen Ledger- und Bot-Tests enthalten in der Stichprobe keine Tautologie. Ein `Expect.isTrue true` in `LedgerViewTests.fs:1041` ist dagegen berechtigt: der Test erzwingt per Reflection den Typinitialisierer und prüft, dass er nicht wirft.

## 3. Tests ohne Verhaltensaussage (Strukturtests)

Per Skript gezählt: Testfälle, deren Körper Quelltext oder Doku einliest (`File.ReadAllText`, `readRepo`, `source`, `viewSource`, `functionBody`, `bindingBody`, `Quelltext.read` und ähnliche): **140 von 2463 (5,7 %)**. Das ist eine Untergrenze, Tests auf Klassen-Strings von Token-Konstanten (`LedgerViewTests.fs:892`, `SyncFlowViewTests.fs:399-405`) fehlen darin. Die Stichprobe kommt mit diesen auf 18 %.

Schwerpunkte: `LedgerViewTests.fs` (41 von 122), `LedgerDetailTests.fs` (15 von 62), `LedgerSplitEditorClientTests.fs` (9 von 60), `FontFaceRegressionTests.fs` (7 von 10), je 6 in `LedgerCategoryChangeClientTests.fs`, `LedgerFilterButtonTests.fs`, `LedgerKeyboardTests.fs`.

Bewertung nach Zweck:

| Art | Beispiel | Urteil |
|---|---|---|
| Wächter für eine Invariante, die zur Laufzeit nicht prüfbar oder teuer ist | `CorsTests.fs:31-46` (keine CORS-Middleware), `BackendPortGuardTests.fs` (`app.Run()` ohne URL), `FontFaceRegressionTests.fs:133` (keine Google-Fonts, Datenschutz), `ModuleLevelDbInitGuardTests.fs` (keine DB-Initialisierung auf Modulebene) | sinnvoll. Die Regel ist global, ein Rückfall wäre teuer, und der Wächter prüft sich selbst (`ModuleLevelDbInitGuardTests.fs:186`, `TailwindClassGuardTests.fs:71-109` mit absichtlich falscher Datei). |
| Pflicht-Beispiel im Styleguide | `LedgerDetailTests.fs:604`, `LedgerMemoClientTests.fs:353` | vertretbar als Prozessregel, sagt aber nichts über Verhalten. Bricht bei jeder Umbenennung. |
| Verdrahtung in der View per Teilstring | `LedgerSplitEditorClientTests.fs:680` (`"SplitLogic.canSave editor"` muss im Rumpf von `detailPanelView` stehen), `LedgerViewTests.fs:1228`, `:262-266` | schwach. Fixiert Bezeichner und Formatierung. Ein Refactoring mit gleichem Verhalten macht ihn rot, ein Fehler mit gleichem Text (z. B. falsches Argument) lässt ihn grün. Für das Verhalten existieren meist schon Playwright-Wächter (`scripts/ledger-split-editor.test.mjs`). |
| Rückbau-Wächter | `LedgerFilterButtonTests.fs:278` (`historySheet` darf nicht mehr vorkommen), `SyncFlowViewTests.fs:582` (Satz in `standards/frontend/styleguide.md` ersetzt) | geringer Wert. Prüft die Abwesenheit eines alten Namens oder Satzes. Nach einem Lauf ist die Aussage verbraucht. |
| Klassen-String-Inhalt | `SyncFlowViewTests.fs:399-405`, `LedgerViewTests.fs:892` | nahe an der Tautologie. Dass eine Konstante `h-5` enthält, steht in der Konstante selbst. Sinnvoll wird es nur mit einem Verbot (`neon-green` darf nicht vorkommen), weil das eine Designregel absichert. |

Faustregel aus den Befunden: Ein Strukturtest lohnt sich, wenn er eine repo-weite Invariante absichert, die kein Verhaltenstest erreicht (Netzwerk-Ziel, Middleware, Startcode, DB-Pfad), und wenn er seine eigene Fehlerfähigkeit beweist. Für Verdrahtung einzelner Views ist ein Browser-Wächter oder ein Test der `update`-Funktion die bessere Ebene.

## 4. Mock-Theater

Kaum vorhanden. 153 Testfälle nutzen Probes, Fakes oder einen Fake-`HttpMessageHandler`, fast alle in den Ledger-Befehlstests und `TargetsApplyTests.fs`. Das Muster: Port als Funktionswert injizieren (z. B. `MemoProbe` in `LedgerMemoTests.fs:90-99`), Befehl gegen In-Memory-SQLite ausführen, dann **Ereignisse, gelesenen Zustand und ausgehende Aufrufe** prüfen (`LedgerCategoryChangeTests.fs:454-492`, `LedgerSplitWriteTests.fs:645-655`).

Per Skript gesucht: Tests mit Mock, deren sämtliche `Expect`-Zeilen nur den Mock betreffen. 5 Treffer, alle berechtigt, weil der ausgehende Aufruf dort das Verhalten ist (z. B. `LedgerCategoryChangeTests.fs:579`: "retry einer Buchung ohne Kategorie sendet null", geprüft wird der PATCH-Inhalt). Die Fake-`HttpMessageHandler`-Tests (z. B. `LedgerCategoryBatchTests.fs:416`) prüfen echte Status-Zuordnung (400, 401, 429).

Die Bot-Tests (`BotApiTests.fs`) rufen den Giraffe-Handler mit injizierten Abhängigkeiten und echter In-Memory-DB auf und prüfen HTTP-Status, Ledger-Ereignisse und Nebenläufigkeit. Das ist kein Mock-Theater.

## 5. Stil-Uneinheitlichkeit

- **Sprache der Testnamen** (Heuristik nach Stoppwörtern): 1650 deutsch, 424 englisch, 389 nicht eindeutig. Englisch fast nur in den älteren Dateien (`SyncSessionManagerTests.fs`, `ValidationTests.fs`, `RemoteDataTests.fs`, `DuplicateDetectionTests.fs`, `ComdirectClientTests.fs`, `RulesReorderTests.fs`). Fünf Dateien mischen beide Sprachen mit je mindestens drei Fällen: `FontFaceRegressionTests.fs`, `RulesEngineTests.fs`, `SyncFlowViewTests.fs`, `ToastLifecycleTests.fs`, `YnabClientTests.fs` (z. B. englische Namen mit deutschen Meldungen in `FontFaceRegressionTests.fs:133-176`).
- **Umlaute:** 144 Stellen mit Umschreibung (`uebernommen`, `Uebertrag`, `laedt`, `traegt`) in `LedgerTests.fs`, `LedgerMirrorTests.fs`, `LedgerPageTests.fs`, `LedgerViewTests.fs`, `MoneyFormatTests.fs`, `YnabClientTests.fs`. Neuere Dateien nutzen echte Umlaute. Teils im selben Testnamen gemischt (`LedgerPageTests.fs:2355` "Vorschläge" neben `:2251` "Vorschlaege").
- **Aufbau:** 32 Dateien nutzen die `test "..." { }`-CE, die übrigen `testCase "..." <| fun () ->`. Drei Dateien mischen beides (`LedgerMirrorTests.fs`, `LedgerPageTests.fs`, `TargetsApplyTests.fs`).
- **Namensschema:** ältere Dateien `Funktion returns X when Y` (`SyncSessionManagerTests.fs:79`), neuere fachliche Sätze (`BotApiTests.fs:379`), dazwischen Akzeptanzkriterien-Präfixe (`LedgerPageTests.fs:2577` "AC6: ...") und Task-Ids im Namen (`LedgerKeyboardTests.fs:246` "(ledger-q1hw2, ledger-w4vte)").
- **Assertions:** ausschließlich Expecto-`Expect.*` und `failtest`, kein Unquote. Ergebnis-Typen werden uneinheitlich geprüft: mal `Expect.isError`, mal `match ... | Ok _ -> failtest`, mal eigene `unwrap`-Helfer pro Datei (`LedgerMemoTests.fs:103-106`, ähnlich in jeder Ledger-Befehlsdatei).
- **DB-Setup:** jede DB-Testdatei setzt `USE_MEMORY_DB` zusätzlich auf Modulebene (`LedgerLenaFlagTests.fs:17`), obwohl `test.runsettings` das schon tut. Schema-Initialisierung je Testfall über umdefiniertes `testCase` (`LedgerSplitReadTests.fs:548-557`), je Datei leicht anders.

Die Uneinheitlichkeit folgt der Entstehungszeit, nicht dem Bereich. Innerhalb einer Datei ist der Stil meist konsistent.

## 6. Security und Datenschutz: Endpoint-Abdeckung

### Grundsätzliches (bestätigt)

- Die sechs Fable.Remoting-APIs (`src/Server/Api.fs:1982-2025`) haben **keine Authentifizierung**. Schutz ist allein die Netzgrenze (Tailscale). Es gibt keinen Test, der diese Annahme festhält oder prüft.
- `getSettings` (`Api.fs:145-186`) gibt YNAB-Token, Comdirect-Client-Secret und Comdirect-Passwort entschlüsselt an den Client zurück. Kein Test dazu.
- Der Fehler-Handler (`Api.fs:1977-1980`) gibt Ausnahmen per `Propagate ex` an den Client weiter. Kein Test, ob dabei interne Details (Pfade, SQL) nach außen gehen.
- `CorsTests.fs:31-46` prüft, dass keine CORS-Policy existiert. Das verhindert das Lesen fremder Antworten, nicht das Absenden. Ob Fable.Remoting einen Cross-Origin-POST mit `text/plain` annimmt (CSRF aus einer Webseite im Browser eines Tailnet-Geräts), ist ungetestet (**Verdacht**, nicht geprüft).
- Verschlüsselungsschlüssel: ohne `BUDGETBUDDY_ENCRYPTION_KEY` fällt `Persistence.fs:151-156` auf einen Schlüssel aus dem Maschinennamen zurück. `EncryptionTests.fs` prüft nur Rundlauf und kaputte Eingaben, nicht den Rückfall.
- Fast alle Ledger- und Budget-Tests zielen auf `Ledger.CommandHandlers`, `Ledger.QueryHandlers` und `Server.Budgeting.*`, nicht auf die Adapter in `Api.fs`. Die Adapter wählen Uhr, Token und Budget (`Api.fs:1475-1712`, z. B. `memoPush` `:1529`). Ihre Verdrahtung ist ungetestet. Laut Projekt-Memory ist der Fable.Remoting-Proxy unter .NET nicht testbar; die Record-Felder selbst ließen sich aber direkt aufrufen (wie `BudgetingTests.fs:234` mit `Server.Api.budgetMonthFor` zeigt).

Legende: **ja** = Test am Handler oder an der Funktion, an die der Endpoint 1:1 delegiert. **indirekt** = nur ein Baustein (Validierung, Decoder, reine Funktion) ist getestet, der Endpoint selbst nicht. **nein** = nichts gefunden.

### Bot (`src/Server/BotApi.fs:396-423`, Bearer-Token)

| Endpoint | Positiv | Negativ | Fundstelle |
|---|---|---|---|
| Auth für alle `/bot/`-Routen | ja | ja: ohne Token, falsches Token, kein Token konfiguriert, Schreibweisen, kodierte Pfade, Drossel | `BotApiTests.fs:177`, `:193`, `:202`, `:218`, `:225`, `:154`; `BotValidationTests.fs:157` |
| `POST /bot/transactions/preview` | ja | ja: Betrag 0, negativ, über Grenze, ungültige Felder, Kategorie mehrdeutig, Konto mitgeschickt, Datum außerhalb Fenster, Tagesgrenze | `BotApiTests.fs:243`, `:260`, `:267`, `:275`, `:288`, `:474`; `BotValidationTests.fs:59-105` |
| `POST /bot/transactions/{id}/commit` | ja | ja: unbekannt 404, abgelaufen 410, Idempotenz, 10 parallele Commits, Tagesgrenze, Push-Fehler | `BotApiTests.fs:294`, `:305`, `:330`, `:346`, `:358`, `:390-464` |
| `GET /bot/categories` | ja | ja: YNAB-Fehler 502, interne und versteckte Kategorien ausgeblendet | `BotApiTests.fs:148`, `:235` |

Der Bot ist der am besten abgesicherte Teil der Suite. Konstantzeit-Vergleich des Tokens ist umgesetzt (`BotApi.fs:228-236`), aber nicht testbar und nicht getestet.

### SettingsApi (`Api.fs:133-316`, ohne Auth)

| Endpoint | Positiv | Negativ | Fundstelle / Lücke |
|---|---|---|---|
| getSettings | nein | nein | gibt Geheimnisse im Klartext zurück, ungetestet |
| getLenaFlagSettings | ja | ja (kein Vorschlag) | `LedgerLenaFlagTests.fs:676`, `:689` |
| saveLenaFlagColor | ja | ja (unbekannte Farbe) | `LedgerLenaFlagTests.fs:663`, `:669` |
| saveYnabToken | nein | indirekt | `ValidationTests.fs:100` (validateYnabToken); Ablauf "prüfen, dann verschlüsselt speichern" ungetestet |
| saveComdirectCredentials | nein | indirekt | `ValidationTests.fs:127`; dass Secret und Passwort mit `encrypt=true` gespeichert werden (`Api.fs:225-228`), ungetestet |
| saveSyncSettings | nein | indirekt | `ValidationTests.fs:215`, `:240` |
| testYnabConnection | nein | nein | |
| testComdirectConnection | nein | nein | Fehlerzuordnung `ComdirectClientTests.fs:170-207` |
| confirmComdirectTan | nein | nein | |

### YnabApi (`Api.fs:317-497`)

| Endpoint | Positiv | Negativ | Fundstelle / Lücke |
|---|---|---|---|
| getBudgets | indirekt | nur live | Decoder in `YnabClientTests.fs`; 401 nur live in `YnabIntegrationTests.fs:177` |
| getBudgetDetails | indirekt | nur live, übersprungen | `YnabIntegrationTests.fs:192` |
| getCategories | indirekt | nein | Decoder; Retry-Verdrahtung im Client per Quelltext `RetryTests.fs:118` |
| getPayees | indirekt | nein | |
| setDefaultBudget | nein | nein | keine Prüfung, ob die Id existiert |
| setDefaultAccount | nein | nein | |
| setQuickAddAccount | nein | nein | |
| addManualTransaction | indirekt | indirekt | `QuickAddTests.fs:87` (validateManualTransaction), `:164` (Body) |
| getRecentQuickAddTemplates | indirekt | indirekt | `QuickAddTests.fs:495`, `:559` |

### RulesApi (`Api.fs:498-757`)

| Endpoint | Positiv | Negativ | Fundstelle / Lücke |
|---|---|---|---|
| getAllRules | indirekt | nein | Persistenz-Rundlauf `PersistenceTypeConversionTests.fs:80-130` |
| getRule | indirekt | nein | unbekannte Id ungetestet |
| createRule | nein | indirekt | `ValidationTests.fs:296` |
| updateRule | nein | indirekt | `ValidationTests.fs:370` |
| deleteRule | nein | nein | |
| reorderRules | indirekt (Client) | indirekt (Client) | `RulesReorderTests.fs:38`, `:78`; Server `updatePriorities` ungetestet |
| exportRules | nein | nein | |
| importRules | nein | nein | Import umgeht `validateRuleCreateRequest`, prüft nur `compileRule` (`Api.fs:665-697`) |
| testRule | indirekt | indirekt | `RulesEngineTests.fs:60-106`, ungültiger Regex `:97`; Regex ohne `MatchTimeout` (`RulesEngine.fs:93`, `:143`), kein Test zu katastrophalem Backtracking |

### SyncApi (`Api.fs:792-1474`)

| Endpoint | Positiv | Negativ | Fundstelle / Lücke |
|---|---|---|---|
| startSync | nein | nein | |
| getCurrentSession | ja | ja | `SyncSessionManagerTests.fs:79-86` |
| cancelSync | nein | indirekt | `SyncSessionManagerTests.fs:387-412` (validateSession) |
| initiateComdirectAuth | nein | indirekt | `ComdirectClientTests.fs:170-207` |
| confirmTan | nein | indirekt | `SyncSessionManagerTests.fs:421-476` (validateSessionStatus) |
| getTransactions | ja | ja | `SyncSessionManagerTests.fs:215-230` |
| categorizeTransaction | nein | indirekt | validateSession, `ValidationTests.fs:459` (validatePayeeOverride) |
| skipTransaction | nein | indirekt | validateSession |
| unskipTransaction | nein | indirekt | validateSession |
| bulkCategorize | nein | indirekt | validateSession |
| splitTransaction | indirekt | indirekt | `SplitTransactionTests.fs`, `SplitPushTests.fs:169` |
| clearSplit | indirekt | nein | Ablauf im Test nachgebaut, `OrderIdMatcherTests.fs:352-356` |
| setAskLena | indirekt | nein | reine Funktion `ImportAskLenaTests.fs:62` |
| importToYnab | indirekt | indirekt | `PushOutcomeClassificationTests.fs:83`, `ForceImportCandidateTests.fs:61` |
| forceImportDuplicates | indirekt | indirekt | wie oben |
| getSyncHistory | nein | nein | `count` ohne Obergrenze |

### LedgerApi (`Api.fs:1712-1880`)

| Endpoint | Positiv | Negativ | Fundstelle |
|---|---|---|---|
| mirrorYnabBudget | ja | ja | `LedgerMirrorTests.fs:631`, `:552` |
| getLastMirrorRun | ja | nein | `LedgerPageTests.fs:850-890` |
| getLedgerAccounts | ja | entfällt | `LedgerPageTests.fs` |
| getLedgerEntriesForAccount | ja | nein | `LedgerSplitReadTests.fs` |
| getLedgerEntriesPage | ja | teilweise | `LedgerPageTests.fs:1851`, `:2577`; `PageSize` hat keine Obergrenze (`Ledger/QueryHandlers.fs:545`), kein Test mit 0, negativ oder sehr groß |
| getLedgerPayeeSuggestions | ja | nein | `LedgerPageTests.fs:2251`, `:2355` |
| getPayeeCategorySuggestions | ja | ja | `LedgerPayeeCategorySuggestionsTests.fs:98`, `:118` |
| getLedgerAccountBalances | ja | entfällt | `LedgerPageTests.fs` |
| getEntryHistory | ja | nein | unbekannte Id ungetestet |
| setReviewMark | ja | ja | `LedgerReviewMarkTests.fs:119`, `:461` |
| restoreReviewMark | ja | ja (Reconciled) | `LedgerReviewMarkTests.fs:372`, `:387` |
| retryReviewMarkWriteBack | ja | ja | `LedgerReviewMarkTests.fs:471-506` |
| setEntryCategory | ja | ja | `LedgerCategoryChangeTests.fs:454`, `:482` |
| setEntrySplits | ja | ja | `LedgerSplitWriteTests.fs:328`, `:364`, `:645` |
| getBudgetAccounts | nein | nein | Cache in `Api.fs:1654-1677` ungetestet |
| getLedgerEntry | ja | ja | `LedgerSplitReadTests.fs:213` |
| retryCategorizationWriteBack | ja | ja | `LedgerCategoryChangeTests.fs:543-579` |
| retryPushToYnab | ja | ja | `LedgerRetryPushTests.fs:107`, `:114` |
| setReviewMarkBatch | ja | ja | `LedgerReviewMarkBatchTests.fs` |
| restoreReviewMarkBatch | ja | ja | `LedgerReviewMarkBatchTests.fs` |
| setEntryCategoryBatch | ja | ja | `LedgerCategoryBatchTests.fs:197`, `:277` |
| setLenaFlag | ja | ja | `LedgerLenaFlagTests.fs:265` |
| restoreFlag | ja | ja | `LedgerLenaFlagTests.fs:202`, `:225` |
| retryFlagWriteBack | ja | ja | `LedgerLenaFlagTests.fs:333-361` |
| setEntryMemo | ja | ja (500 Zeichen, gelöscht) | `LedgerMemoTests.fs:131`, `:249` |
| retryMemoWriteBack | ja | ja | `LedgerMemoTests.fs:364` |
| setLenaFlagBatch | ja | ja | `LedgerLenaFlagBatchTests.fs:187` |
| restoreFlagBatch | ja | ja | `LedgerLenaFlagBatchTests.fs` |

### BudgetApi (`Api.fs:1928-1973`)

| Endpoint | Positiv | Negativ | Fundstelle |
|---|---|---|---|
| getBudgetMonth | ja | ja | `BudgetingTests.fs:167`, `:178`, `:234` |
| previewTargets | ja | ja | `TargetsApplyTests.fs` |
| applyTargets | ja | ja (Drift, Lesefehler) | `TargetsApplyTests.fs:352` |
| retryFailedTargets | ja | teilweise | `TargetsApplyTests.fs:380-419`; ungültige Guid (`Api.fs:1958-1959`) ungetestet |

**Zusammenfassung:** Von 75 Remoting-Endpoints sind 28 direkt mit Positiv- und Negativtest abgedeckt (20 im Ledger, 4 im Budget), 7 nur positiv, 27 nur indirekt über Validierung oder reine Bausteine, 13 gar nicht. Die Lücken liegen genau dort, wo Geheimnisse liegen (Settings) und wo der Code noch inline in `Api.fs` steht (Settings, YNAB, Rules, Sync). Kein Test prüft eine Authentifizierung für `/api/*`, weil es keine gibt.

## 7. Laufzeit und Flakiness

### Laufzeit (bestätigt aus Transkripten und Protokoll)

- F#-Suite: 25 `dotnet test`-Zusammenfassungen in Transkripten, die neueren mit 2479 bis 2507 Tests in **5 bis 6 s** (Agent-Transkripte, Oktober). Eine ältere Sitzung (`8026629e`) brauchte 2:52 bis 3:03 min für 1844 Tests, das fällt in die Zeit vor dem Disposal-Fix.
- Langsame F#-Tests: Unterprozesse in `FontFaceRegressionTests.fs:77-98` (startet `node`) und `TailwindClassGuardTests.fs`. `Thread.Sleep 300` in `LedgerCategoryBatchTests.fs:344` und `LedgerCategoryChangeTests.fs:645`.
- Node-Suite: 278 Tests in 2:10 min nach Umstellung auf einmaligen Build und statischen Server, vorher 15 bis 20 min seriell (`protocol.md:489`, `:518`). Ein paralleler Lauf hing 22 min (`protocol.md:597`).

### Belegte Flakes

| Fund | Ursache | Beleg |
|---|---|---|
| infra-001 | Disposal-Race: geteilte `SqliteConnection` für parallele Dapper-Operationen, dazu Versionskonflikt Microsoft.Data.Sqlite | `protocol/2026-06.md:450-454`, Commit 82b5cef, Kommentar `Tests.fsproj` |
| ledger-zmbf1 | Reihenfolgeabhängigkeit: Replay-Test lief ohne Schema-Initialisierung und war nur grün, wenn vorher ein anderer Test das Schema anlegte | `protocol.md:2745`, `:3330` |
| ynab-r9k4t | Zeitzone: Decoder verschob Kalendertage um den lokalen Offset; Test auf UTC-Maschine blind, daher `TZ=Europe/Berlin` in `test.runsettings` | `protocol/2026-09.md:2004`, `test.runsettings` |
| ledger-wj6pd | Playwright misst Zeilenhöhen zeitweise als 0 px, einzeln grün, im Verbund rot | `protocol.md:493`, `:606` |
| AC6-Browsertest | 2 von 4 Läufen rot | `protocol/2026-09.md:219` |

In Transkripten sind Node-Fehlschläge häufig (z. B. ein Test "Handy" 39-mal in 9 Sitzungen, "PC: Der Kategorie-Eintrag setzt genau diese Kategorie ..." 13-mal in 4 Sitzungen). Ohne Kontext lässt sich TDD-Rot nicht von Flake trennen; die Zahlen sind daher nur ein Hinweis, dass die Browser-Wächter der unruhigste Teil sind.

### Restrisiken (Verdacht)

- **Geteilte In-Memory-DB bei parallelem Expecto:** `Data Source=:memory:;Mode=Memory;Cache=Shared` (`Persistence.fs:68`) ist eine Datenbank für alle Testlisten. Expecto läuft parallel, `testSequenced` ordnet nur innerhalb einer Liste (23 Dateien). Isolation hängt an eindeutigen Ids (`freshAccount`, `uniquePayee`). ledger-zmbf1 zeigt, dass diese Konvention brechen kann.
- **Live-Netz in jedem Lauf:** zwei Tests rufen echte Dienste ohne Schalter auf (Abschnitt 8). Ohne Netz werden sie rot.
- **Node-Fixtures mit UTC-Datum:** `new Date().toISOString().slice(0, 10)` als "heute" in 12 Browser-Wächtern (z. B. `scripts/ledger-memo.test.mjs:29`). Zwischen 0 und 1 Uhr (Winter) bzw. 2 Uhr (Sommer) Berliner Zeit ist das der Vortag. Ob ein Wächter dann rot wird, hängt davon ab, ob er "Heute"-Gruppierung prüft.
- **Feste Wartezeiten:** 48 `waitForTimeout`/`setTimeout` in `scripts/*.test.mjs` (z. B. `scripts/ledger-compact-mouse.test.mjs:94`, `:226`).
- **Wanduhr im Produktionscode des Clients:** `LedgerCategoriesFreshnessTests.fs:66-121` arbeitet mit `DateTime.Now` und Abständen von Minuten. Robust, solange die Schwelle (2 min) weit vom Testabstand liegt.
- **Zeitbasierte Negativprüfung:** `LedgerCategoryChangeTests.fs:645` prüft nach 300 ms, dass ein Befehl noch wartet. Unter Last kann das falsch grün werden, nie falsch rot.
- `DateTime.Today` wird in `DuplicateDetectionTests.fs` (40-mal) und `OrderIdMatcherTests.fs` (43-mal) mehrfach pro Test gelesen. Über Mitternacht hinweg könnten zwei Werte auseinanderfallen. Geringes Risiko bei 5 s Laufzeit.

## 8. Tests mit Zugriff auf Produktionsdatenbank oder echte Dienste

### Produktionsdatenbank: gut abgesichert (bestätigt)

- `test.runsettings` setzt `USE_MEMORY_DB=true` für `dotnet test`, `Main.fs` zusätzlich für den Direktaufruf.
- `TestDatabaseGuardTests.fs:28-31` prüft den tatsächlich aufgelösten Connection-String und steht als erste Compile-Einheit.
- `ModuleLevelDbInitGuardTests.fs` verbietet DB-Initialisierung auf Modulebene.
- Dateisystem-Schreibzugriffe nur in `Path.GetTempPath()` (`FontFaceRegressionTests.fs:72`, `TailwindClassGuardTests.fs:71`).
- Browser-Wächter mocken die API (`page.route`), Kommentar in `scripts/ledger-memo.test.mjs:4-5`.
- Restlücke: der Rückfallpfad `Persistence.fs:58-59` zeigt ohne `DATA_DIR` auf `~/my_apps/budgetbuddy-dev`. Wer `--settings andere.runsettings` nutzt, wird vom Wächter gestoppt, aber erst nachdem andere, parallel laufende DB-Tests womöglich schon geschrieben haben. Der Wächter garantiert nur "laut", nicht "vorher".

### Echte Dienste: zwei Tests ohne Schalter (bestätigt)

Die Integrationstests lesen `.env` aus dem Repo-Wurzelverzeichnis über `__SOURCE_DIRECTORY__` und laufen nur bei `RUN_INTEGRATION_TESTS=true`. Zwei Tests prüfen diesen Schalter **nicht**:

1. `YnabIntegrationTests.fs:177` "handles invalid token gracefully" ruft bei jedem `dotnet test` `https://api.youneedabudget.com/v1/budgets` mit einem Fantasie-Token auf (`YnabClient.fs:14`, `:179`). Lesend, aber Netz-Abhängigkeit in jedem Lauf.
2. `ComdirectIntegrationTests.fs:111` "handles invalid credentials gracefully" startet bei vorhandenem `COMDIRECT_CLIENT_ID`/`COMDIRECT_CLIENT_SECRET` einen echten OAuth-Ablauf gegen `https://api.comdirect.de/` (`ComdirectClient.fs:60`, `:461`) mit Romans echten API-Schlüsseln und falschem Benutzer/Passwort. In der `.env` auf `main` sind beide Schlüssel gesetzt, `RUN_INTEGRATION_TESTS` ist nicht `true`. Jeder lokale `dotnet test` im Hauptverzeichnis erzeugt damit einen fehlgeschlagenen Login bei der Bank. Folgen wie Drosselung oder Sperre des API-Clients sind nicht ausgeschlossen (**Verdacht**, nicht geprüft). In Worktrees ohne `.env` wird der Test übersprungen.

Stützender Beleg: Die Läufe melden konstant 6 übersprungene Tests. Das sind genau die fünf geschalteten YNAB-Tests (`:51`, `:76`, `:124`, `:159`, `:192`) und der eine geschaltete Comdirect-Test (`:62`). Die beiden ungeschalteten laufen also mit.

Wird `RUN_INTEGRATION_TESTS=true` gesetzt, löst `ComdirectIntegrationTests.fs:62` eine echte Push-TAN auf Romans Telefon aus (`:73` warnt nur per `printfn`).

## 9. Vorschlag: Test-Doktrin (eine Seite)

**Grundsatz.** Ein Test prüft beobachtbares Verhalten an der niedrigsten Ebene, auf der es entsteht, und muss rot werden können. Begründung: die Suite ist schnell (6 s für 2500 F#-Tests) und überwiegend aussagekräftig, weil der Ledger diesem Muster folgt. Die schwachen Stellen sind genau die Abweichungen davon.

| Ebene | Was | Wie | Begründung aus den Befunden |
|---|---|---|---|
| 1. Pure Domain, Decoder, Encoder | Regeln, Zustandsübergänge, Replay, JSON-Verträge mit YNAB/Comdirect | Expecto ohne DB, feste Daten und feste Uhr. Property-Tests für Rundläufe und Invarianten (heute nur 3). | Ledger-Ereignistests und Decoder-Regressionen (ynab-r9k4t) haben echte Bugs gefangen. Rundläufe wie `AuswahlTests.fs:204` sind natürliche Property-Kandidaten. |
| 2. Validation | jede Regel mit Grenzwert links und rechts, Leer, zu lang, falsches Format | reine Funktionen | gut abgedeckt; aber jeder Endpoint, der Eingaben annimmt, muss nachweislich durch seine Validierung laufen (Lücke `importRules`). |
| 3. Befehls- und Abfrage-Handler mit Persistence | Ereignisse, Zustand, ausgehender Port-Aufruf, Fehler- und Sperrfälle | In-Memory-SQLite, Ports als Funktionswerte, Uhr als Parameter, Schema-Init im Testfall, eindeutige Ids | Ledger-Muster ohne Mock-Theater. ledger-zmbf1 zeigt: Init gehört in jeden Testfall. |
| 4. API-Adapter | pro Endpoint ein Durchstich: gültig, ungültig, fehlende Konfiguration | Record-Feld in `Api.fs` direkt aufrufen, Ports oder Settings in der In-Memory-DB vorbelegen. Wo der Code inline in `Api.fs` steht, zuerst in einen Handler ziehen. | 13 Endpoints ohne jeden Test, darunter alle Settings-Endpoints mit Geheimnissen. Die Bot-Tests zeigen, dass Handler-Tests mit echtem HTTP-Status machbar sind. |
| 5. Security-Invarianten | Geheimnisse verlassen den Server nicht, Fehlerantworten ohne Interna, Auth wo vorgesehen, Grenzen für Seiten- und Listen-Größen | Ebene 4 plus gezielte Negativtests | heute nur für `/bot/` vorhanden. |
| 6. Strukturtests | nur repo-weite Invarianten ohne Verhaltenszugang: Netzziele, Middleware, Startcode, DB-Pfad, verbotene Klassen | Quelltext lesen, mit eingebautem Gegenbeweis (Wächter gegen den Wächter) | `CorsTests`, `BackendPortGuardTests`, `ModuleLevelDbInitGuardTests` sind gut. View-Verdrahtung per Teilstring und Rückbau-Wächter nicht mehr neu anlegen; vorhandene beim nächsten Anfassen der Datei durch Ebene 7 oder `update`-Tests ersetzen. |
| 7. Frontend | `update`-Logik und reine View-Helfer unter .NET; Layout, Fokus, Viewport im Browser-Wächter gegen gemockte API | Expecto für State; Playwright mit festem Datum (Berliner Kalendertag) und Warten auf Bedingungen statt `waitForTimeout` | Browser-Wächter sind der unruhigste Teil (wj6pd, AC6, 48 feste Wartezeiten). |
| 8. Live-Integration | nur manuell, nie im Standardlauf | eigener Schalter je Dienst, Test wird ohne Schalter übersprungen, keine Anmeldung mit echten Schlüsseln und falschem Passwort | zwei Tests laufen heute ungeschaltet gegen YNAB und Comdirect. |

**Regeln für alle Ebenen:** keine Tests, die nur Konstanten oder gerade gebaute Records zurücklesen, keine "documents"-Tests (Doku gehört in Kommentare oder Doku). Uhr, Zeitzone und Zufall werden injiziert. Testnamen auf Deutsch mit echten Umlauten, als fachlicher Satz; Task-Ids in Kommentare statt in Namen. Eine Datei, ein Aufbau (`testCase` oder `test`-CE), ein gemeinsamer `unwrap`-Helfer im Testprojekt.

## 10. Abgleich mit der vorhandenen Doktrin

Gelesen nach der Analyse: `.claude/rules/testing.md`, `standards/testing/overview.md`, `api-tests.md`, `frontend-tests.md`, `persistence-tests.md` (und Überschriften von `domain-tests.md`, `property-tests.md`).

**Übereinstimmung:**

- "Bug Regression Tests ALWAYS" (`.claude/rules/testing.md:15`): in der Suite gelebt, viele Tests nennen den Bug und das Ticket im Kommentar (`YnabClientTests.fs:335`, `LedgerPageTests.fs:1282`).
- "Validation Tests ALWAYS, all rules" (`:14`): weitgehend erfüllt (`ValidationTests.fs`, `BotValidationTests.fs`, `QuickAddTests.fs:87`).
- In-Memory-SQLite und "No production database writes" (`persistence-tests.md`): erfüllt und durch Wächter stärker abgesichert, als die Doktrin verlangt.
- "Test names must describe behavior, not implementation" (`.claude/rules/testing.md:52`): für die meisten neueren Tests erfüllt.

**Abweichungen:**

- `.claude/rules/testing.md:50-51` nennt eine lokale Supabase-Instanz und `./scripts/setup-env.sh`, `:28` den Lauf `RUN_SMOKE_TESTS=true dotnet test`. Nichts davon existiert im Repo (bestätigt: keine Treffer für `supabase`, `RUN_SMOKE_TESTS`, Skript fehlt). Die Regel ist veraltet (passt zur Notiz im Projekt-Memory).
- "API Integration: Happy path + key errors" (`:16`) und `api-tests.md` ("Testing API layer with mocked persistence", Beispiel `Api.itemApi.getItem`): die Suite testet die Handler darunter, nicht `Api.fs`, und nutzt In-Memory-DB statt gemockter Persistenz. 13 Endpoints haben gar keinen Test. Die Doktrin wird hier weder befolgt noch ist sie zur gelebten Praxis passend formuliert.
- "Domain Tests: 100 % of pure functions" (`:13`): nicht gemessen, es gibt keine Abdeckungsmessung im Repo.
- `frontend-tests.md`: "Views are typically not unit tested". Gelebt wird das Gegenteil: rund 140 Strukturtests lesen View-Quelltext, dazu rund 28 Browser-Wächter. Die Doktrin sagt nichts dazu, wann Strukturtests erlaubt sind; das erklärt den Wildwuchs in Abschnitt 3.
- Live-Integrationstests, Zeitzone, Parallelität und feste Wartezeiten kommen in der Doktrin nicht vor. Die Lösungen (`TZ` in runsettings, Schema-Init je Testfall, statischer Server für Wächter) stehen nur in Kommentaren und im Protokoll.
- Security-Negativtests sind nirgends gefordert. Die gute Abdeckung des Bots kam aus einem Security-Review (`protocol.md:314`), nicht aus der Doktrin.
- `overview.md` zeigt `testAsync` und `Api.itemApi.getItems()` als Muster; beides wird im Repo nicht verwendet. `property-tests.md` existiert, im Code gibt es 3 Property-Tests.
