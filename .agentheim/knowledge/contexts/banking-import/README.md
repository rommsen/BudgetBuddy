# banking-import

## Purpose
Holt Transaktionen aus Bankquellen (Comdirect via OAuth + Push-TAN; perspektivisch ING)
und beurteilt **vor** dem Push, ob eine Transaktion ein Duplikat ist. Übersetzt rohe
Bank-JSON in das saubere interne Modell.

## Classification
**core** — der zuverlässige Comdirect-Import (den YNAB selbst nicht gut hinbekommt) ist
ein Hauptgrund, warum BudgetBuddy existiert.

## Actors
- **Roman** — löst einen Sync aus, bestätigt die Push-TAN auf dem Handy.
- **Comdirect** — externe Datenquelle (Anticorruption Layer übersetzt ihre JSON).

## Ubiquitous language
- **BankTransaction** — eine roh geholte Buchung: Id, BookingDate, Amount, Payee (optional),
  Memo, **Reference** (Comdirect-Dedup-Schlüssel), RawData (Original-JSON).
- **Reference** — der von Comdirect mitgelieferte Schlüssel, der eine Buchung über
  wiederholte Abrufe hinweg identifiziert.
- **DuplicateStatus** — BBs *Vor-Import*-Urteil gegen die YNAB-Buchungen des Import-Kontos: `NotDuplicate | PossibleDuplicate | ConfirmedDuplicate`, jeweils mit **DuplicateDetectionDetails**. `ConfirmedDuplicate` (Reference oder `import_id` in YNAB gefunden) heißt **sicheres Duplikat** und wird übersprungen, `PossibleDuplicate` (Betrag, Tag ±1, Payee) heißt **unsicheres Duplikat** und wartet im Kassenbuch (ADR 0093 Punkt 2 und 3).
- **DuplicateDetectionDetails** — Belege fürs Urteil: FuzzyMatch auf Amount/Date/Payee,
  ob die ImportId oder Reference bereits in YNAB gefunden wurde.
- **Push-TAN** — Comdirects Zwei-Faktor-Bestätigung; während eines Sync wird auf sie
  gewartet (`AwaitingTan`).
- **Refresh-Urteil** (`RefreshFailure`): Ergebnis eines gescheiterten Token-Refresh. `ReloginRequired` (400, 401, jedes andere 4xx außer 429) verlangt eine neue TAN, `RetryLater` (Status 0, 429, 5xx, nicht lesbare Antwort) lässt die Verbindung stehen.
- **Beobachtungssonde**: Testfall hinter `RUN_INTEGRATION_TESTS` und `COMDIRECT_PROBE` (1, 2 oder 3, nur als Prozess-Variable), der die Trageweite der Session mit einer echten TAN misst. Nur dort darf ein Testlauf eine TAN auslösen.
- **Bankverbindung** (`BankConnectionState`): reiner Zustandsautomat `Disconnected | ChallengePending | Connected` in `src/Server/BankConnection.fs`. Eine offene Push-TAN ist `ChallengePending` und gibt keine Daten frei, Daten fließen nur aus `Connected`.
- **Tick**: ein Schritt des Hintergrunddiensts (`tick ports now`). Erneuert fällige Tokens etwa 60 s vor Ablauf und tut sonst nichts.
- **Verlust**: Übergang von `Connected` nach `Disconnected` ohne Handlung von Roman (Refresh-Urteil `ReloginRequired` oder 401/403 auch nach Refresh), dazu eine gescheiterte Wiederaufnahme mit unbrauchbarem gespeichertem Wert. Wird einmal gemeldet. Gescheiterte TAN, Trennen und geänderte Zugangsdaten sind kein Verlust.
- **Verbindungstest** (`ComdirectConnectionTest`): Ergebnis von `testComdirectConnection`. `BankConnectionActive` heißt, die Verbindung lebt und die erste Umsatzseite der konfigurierten Konto-Id war lesbar, es ist keine TAN nötig. Ohne Konto-Id gibt es bei `Connected` eine konkrete Meldung, keinen Bankzugriff und keine Challenge. Bankfehler und fehlende Konto-Id meldet er als `SettingsError.ComdirectConnectionTestFailed`, das ist kein Urteil über die Zugangsdaten. `TanChallengeStarted` trägt die Challenge-Id, es wartet eine Push-TAN.
- **Bank-Port** (`BankPort`): schmaler Record aus Funktionswerten in `src/Server/BankPort.fs` (Wiederaufnahme, Status, Auth starten, Auth abschließen, Umsätze). Nur Comdirect implementiert ihn, die `Comdirect*`-Typen bleiben. `BankLinkStatus` kennt `BankReady` und `BankAuthRequired`, eine offene Challenge zählt als `BankAuthRequired`. `StartAuth` nimmt eine Bedingung, die direkt vor der Anmeldung bei gehaltenem Bank-Gate geprüft wird.
- **Sync-Start** (`SyncStart`): Antwort von `startSync`. `Fetched` trägt die beendete Sitzung mit den Zahlen des Abrufs, `TanRequired` heißt, eine Push-TAN wartet. `confirmTan` liefert die beendete Sitzung. Der Server entscheidet, der Client folgt.
- **Abruf mit Buchen** (`fetchAndBook`, `src/Server/BankImport.fs`, ADR 0093): die eine Stelle für den Abruf, gerufen von `startSync` bei `Connected` und von `confirmTan`. Umsätze über die konfigurierte Konto-Id, Regeln (`classifyTransactions`), YNAB-Buchungen des Import-Kontos, Abgleich mit dem Kassenbuch über die `import_id` (`knownComdirectImports`), Duplikaterkennung, Order-ID-Vorschlag, dann je neuer Buchung Kassenbuch zuerst, Push, Verknüpfung (`pushComdirectImports` über den Port `BookImports`, ADR 0021). Jede neue Buchung steht danach als „zu prüfen“ im Kassenbuch und in YNAB, eine Prüfsitzung gibt es nicht.
- **Nachlesen** (`StartRecovery`): Der Client liest nach verlorener Antwort von `startSync` oder `confirmTan` den Serverzustand neu. Eine Kette, Pause 3 s, Frist 40 Pausen (2 Minuten). Eine beendete Sitzung, die schon vor dem Start da war, ist kein Ergebnis.
- **Gespeicherter Wert** (`StoredConnection`, Settings-Schlüssel `comdirect_bank_connection`): verschlüsselter JSON-Wert aus Tokens, `RequestInfo`, Session-Id und `SessionIdentifier`, gelesen und geschrieben über `ConnectionStore` in `src/Server/BankConnectionStore.fs`. Die Zugangsdaten bleiben in den Einstellungen. Ohne gesetzten `BUDGETBUDDY_ENCRYPTION_KEY` wird nichts geschrieben und ein vorhandener Wert gilt als unlesbar, die Verbindung lebt dann nur im Speicher.
- **Wiederaufnahme**: Versuch, aus dem gespeicherten Wert per Refresh wieder `Connected` zu werden. Der Hintergrunddienst macht ihn einmal nach dem Start, `startSync` und der Verbindungstest (`awaitResumeWith`) bei `Disconnected` vor der Entscheidung über die TAN. Gelingt er, ist keine TAN nötig. `ReloginRequired` oder ein unbrauchbarer Wert löschen den Wert und lassen `Disconnected`. `RetryLater` lässt den Wert und `Connected` stehen, der nächste Versuch folgt nach 60 s, unabhängig von der Restlaufzeit des Access-Tokens.
- **Stand der Bankverbindung** (`BankConnectionStatus`): Anzeigewert aus `getBankConnectionStatus`, `BankDisconnected | BankChallengePending | BankConnected`. Trägt nur den Fall, nie Tokens. Gelesen wird ohne Bankzugriff, ohne `Gate` und ohne Wiederaufnahme, direkt nach einem Neustart zeigt er deshalb kurz `BankDisconnected`. Gesetzte Zugangsdaten heißen in den Einstellungen "Eingerichtet", "Verbunden" steht nur für `BankConnected`.
- **Sync-Sitzung** (`SyncSession`, ADR 0093 Punkt 10): trägt nur Push-TAN und Abruf. Ablauf `AwaitingBankAuth` / `AwaitingTan` → `FetchingTransactions` → `ImportingToYnab` → `Completed` oder `Failed`. Sie trägt den Auslöser (`SyncTrigger`, heute `Manual`) und die **Zahlen des Abrufs** (`FetchCounts`: neu, per Regel, aus Bestellnummer, ohne Kategorie, sichere Duplikate übersprungen, wartend, nicht in YNAB) und steht als Zustand in `sync_sessions` (ADR 0083 Abschnitt 8). „Neu“ zählt jede neu gebuchte Buchung, auch die wartenden.
- **Abrufen** (Sheet, `src/Client/Components/Fetch/`, Prototyp Version 17 Sheet `import`): „Abrufen“ öffnet am Handy ein `BottomSheet`, ab 1024 px einen Dialog über der aktuellen Seite. Zustände TAN, „Rufe ab…“, Ergebnis („<n> neue Buchungen“ mit dem Satz aus `resultSentence`, Teile mit 0 entfallen), „Nichts Neues“ und Fehler. „Jetzt durchgehen“ führt bis ledger-v6s7c ins Kassenbuch über alle Konten mit Filter „Zu prüfen“. Nach jedem Abruf laden Zu tun, Zähler, die Zeile „Comdirect · Import <Stand>“ und eine offene Kassenbuch-Liste neu. Die Seite Sync (`/sync`) gibt es nicht mehr.
- **Umsatz mit Datum in der Zukunft** (Review-Lauf 2026-11-12): ein Umsatz nach dem Berliner Tag wird beim Abruf nicht gebucht, auch kein unsicheres Duplikat; er zählt als `FetchCounts.FutureDated` und das Ergebnis nennt ihn („<n> mit Datum in der Zukunft folgt/folgen beim nächsten Abruf.“). Ein erneuter Abruf sendet eine schon gebuchte, unverknüpfte Buchung mit BBs Stand aus dem Kassenbuch (`importResendRequest`); ein in BB gesetzter Übertrag geht stattdessen über „Erneut senden“. Ein unsicheres Duplikat wartet auch, wenn der Treffer nicht mehr gefunden wird (Schnappschuss aus der Erkennung). Ein zweiter Start am Gate übernimmt nur eine danach begonnene Sitzung und behält deren Fehlertyp.

## Aggregates
- **BankTransaction** — unveränderliche Repräsentation einer geholten Buchung; Reference
  ist innerhalb einer Quelle eindeutig.
- **ComdirectAuthSession** — hält die Bankverbindung des Servers und serialisiert per Semaphor jeden Refresh und Bankzugriff (ADR 0071 Punkt 3).
- **Invariante Fehlertexte:** Comdirect-Fehler tragen Status und `error`-Code, nie den Antwortrumpf. Das gilt auch für Decoder-Meldungen, weil Thoth darin den gelesenen Wert zitiert.
- **Invariante Challenge:** Nur eine Handlung von Roman (Sync-Start, Verbindungstest) startet eine Challenge oder einen Passwort-Login. Tick, Hintergrunddienst und Neustart tun das nie, auch nicht bei `Disconnected`.
- **Invariante Abbruch:** `cancelSync` verwirft nur `ChallengePending`, eine aktive Verbindung bleibt stehen. Er wartet nicht auf den Abruf: Er löscht zuerst die Sitzung, ab dann verfällt ein laufendes Ergebnis, und es startet keine neue Challenge mehr. Eine Challenge, die schon unterwegs ist oder offen liegt, wird verworfen, bei belegtem Bank-Gate im Hintergrund, sobald es frei wird. `ChallengePending` bleibt ohne Sitzung nicht liegen. Ein Verbindungstest bei `Connected` liest genau eine Umsatzseite der konfigurierten Konto-Id ohne Challenge. Geänderte Zugangsdaten (nicht die Konto-Id) trennen die Verbindung.
- **Invariante Sync-Start:** Bei `Connected` startet `startSync` keine Challenge. Stirbt die Verbindung im Abruf, startet der Server genau eine, außer der Sync wurde abgebrochen. Ein anderer Bankfehler bei lebender Verbindung startet keine.
- **Invariante Konto-Id:** An die Bank geht nie eine leere Konto-Id. Fehlt sie, endet `startSync` vor Sitzung und Challenge mit `TransactionFetchFailed` und derselben Meldung wie der Verbindungstest. Fehlt sie erst nach der TAN, scheitert die Sitzung ohne Abruf.
- **Invariante ein Abruf zur Zeit:** `ImportPorts.Gate` umschließt Abruf und Buchen. `startSync`, `confirmTan` und `disconnectBank` laufen nacheinander. Ein zweiter Start während eines Abrufs wartet am Gate und bekommt dessen Ergebnis (beendete Sitzung, TAN-Schritt oder Fehler), die Bank wird einmal gefragt.
- **Invariante Sitzungsende:** Eine Ausnahme in `startSync` nach `StartNew` oder in `confirmTan` markiert die Sitzung als gescheitert, sie bleibt nie in `FetchingTransactions` oder `ImportingToYnab` stehen. Scheitert das Kassenbuch-Schreiben (`summarizeLedgerWriteFailures`), endet die Sitzung `Failed` mit Meldung. Jedes Ende steht in `sync_sessions`. Eine Ausnahme vor `StartNew` lässt eine ältere Sitzung unberührt.
- **Invariante Umsatzabruf:** Das Blättern endet immer. Eine vollständig im Zeitraum liegende Seite ohne neue Reference, die kürzer ist als die erste Seite, gilt als Datenende: Sie fällt weg, der Abruf liefert `Ok` mit den bisher gesammelten Buchungen. Ist eine solche Seite nicht kürzer oder sind es mehr als 100 Seiten, endet der Abruf mit `InvalidResponse`, nie mit einem still unvollständigen Ergebnis. Das ist keine Ablehnung, die Bankverbindung bleibt, es entsteht kein Verlust und keine Challenge.
- **Invariante Importschlüssel:** `TransactionId` ist die Comdirect-Reference, `import_id` ist `BB:` plus Reference ohne Bindestriche. Ein Regressionstest mit festen Werten hält das fest (`src/Tests/ComdirectHttpTests.fs`), weil jede Änderung gebuchte Umsätze doppelt nach YNAB bringt.
- **Invariante verlorene Antwort:** Geht die Antwort von `startSync` oder `confirmTan` auf dem Transportweg verloren, liest der Client nur den Serverzustand neu (`getCurrentSession`) und startet nie selbst neu. Eine beendete neue Sitzung ist das Ergebnis, eine wartende TAN wird wieder gezeigt. Liegt keine neue Sitzung vor oder läuft die Frist ab, bleibt ein Fehler sichtbar. Ein neuer Start entsteht nur durch Romans Klick.
- **Invariante gespeicherter Wert:** Er spiegelt die aktive Verbindung. Er wird nach TAN und Refresh geschrieben, bevor das neue Token in Verwendung geht, und gelöscht bei Verlust, Trennen, geänderten Zugangsdaten und neuer Challenge. Eine gescheiterte Anmeldung, ein Sync-Abbruch und das Verwerfen einer Challenge löschen ihn nicht. Bei geänderten Zugangsdaten wird zuerst getrennt und gelöscht, erst dann werden sie geschrieben. Er erscheint nie im Client, in Logs oder Fehlermeldungen. Scheitert das Speichern, läuft die Verbindung im Speicher weiter. Beleg: Verhalten und Reihenfolge sind gegen Fake-Ports und In-Memory-SQLite getestet, die Anschlüsse des laufenden Servers per Strukturwächter in `src/Tests/StructureGuardTests.fs`. Der Gate-Lauf hat keinen `BUDGETBUDDY_ENCRYPTION_KEY`, Speichern mit Schlüssel läuft dort nie gegen den echten Speicher. Gegen Comdirect lief nichts.
- **Invariante Klick nach Neustart:** Ein liegender Wert kostet keine TAN, solange sein Refresh gelingt. `startSync` und der Verbindungstest warten auf die Wiederaufnahme, bevor sie über die TAN entscheiden. Eine eigene Frist gibt es nicht: Hängt die Bank, begrenzt der `HttpClient`-Timeout (100 s) die Wartezeit, `cancelSync` wartet nicht mit. Nach `RetryLater` gilt weiter `Connected`, ein Abruffehler führt dann zu einem sichtbaren Fehler und zu keiner Challenge.
- **Invariante Trennen:** `disconnectBank` läuft nach einem laufenden Start oder einer TAN-Bestätigung (`disconnectBankWith`, `ImportPorts.Gate`), kein `ReportLoss`. Es löscht zuerst den gespeicherten Wert und setzt erst danach `Disconnected`. Scheitert das Löschen, kommt der Fehler an (`SyncError.DatabaseError ("disconnect_bank", ...)`, nur mit Typnamen), Zustand und Sitzung bleiben unverändert, auch `ChallengePending`, und ein zweites Trennen ist möglich. Nach gelungenem Trennen endet eine Sitzung in `AwaitingBankAuth` oder `AwaitingTan` als `Failed`, `confirmTan` scheitert danach. Eine beendete Sitzung bleibt beendet, ihre Buchungen stehen im Kassenbuch.
- **Invariante YNAB-Abgleich:** Liefert das Lesen der YNAB-Buchungen nichts (`LoadYnabTransactions` = `None`), bucht der Abruf nichts und endet `Failed` mit „YNAB ist nicht erreichbar, es wurde nichts gebucht.“ (ADR 0093 Punkt 8).
- **Invariante wiederholter Abruf** (ADR 0093 Punkt 7): Vor der YNAB-Erkennung entscheidet die reine Funktion `Ledger.Domain.knownImportDecision` über eine Buchung, deren `import_id` das Kassenbuch kennt: wartend oder verworfen (gelöscht, ohne je verknüpft gewesen zu sein) wird übersprungen, kein Ereignis, kein Push; verknüpft gilt als schon importiert; unverknüpft (Push gescheitert oder Ausgang unbekannt) wird erneut gesendet, ohne Duplikaterkennung, die feste `import_id` hält YNAB von einer zweiten Buchung ab. Keine Reference entsteht zweimal.

## Key events
- *TransactionsFetched* — neue BankTransactions liegen vor.
- *DuplicateDetected* — eine Transaktion wurde als (möglicher/bestätigter) Duplikat erkannt.

## Key commands
- *FetchTransactions* (für einen Zeitraum / DaysToFetch).
- *AnalyzeDuplicates* gegen bereits bekannte/importierte Buchungen.

## Relationships with other contexts
- **Upstream of:** `categorization` — gibt geholte, dedup-beurteilte BankTransactions weiter.
- **Anticorruption layer zu:** Comdirect (rohe JSON → BankTransaction).
- Voller Kontext-Überblick: `../../context-map.md`.

## Szenarien und Tests

- **Bug-Tasks:** mindestens ein „kaputt“- und ein „intakt“-Szenario als Akzeptanzkriterium (GIVEN/WHEN/THEN). Der Regressionstest zum kaputt-Szenario ist vor dem Fix rot.
- **Fachliches Verhalten** (Zustandswechsel, Grenzen, Berechtigungen, was an den Client geht) steht in den ACs als Szenario; jedes Szenario hat einen Test. Einfache Fakten-ACs (Datei existiert, Zahl im Outcome) brauchen keins.
- **Maßstab für Tests:** `.claude/rules/test-doctrine.md` (Heimat-Ebene, Pflichtliste, kein Test für Tautologien).

## Test command

`./verify.sh` im Repo-Wurzelverzeichnis. Läuft dotnet build, Expecto über die Tests.dll, npm run build, guard:classes, guard:tokens und test:scripts; grün nur bei der Schlusszeile `VERIFY: PASS` und Exit 0. Bei `VERIFY: FAIL` stehen die roten Schritte und die Namen der roten Tests am Ende der Ausgabe.

## Live-Befund comdirect-Session (2026-10-30, `banking-import-p5mcp`)

Ein Lauf mit genau einer Push-TAN, Protokoll der Beobachtungssonde. Einzelne Messung, kein Dauerbetrieb.

- **Trageweite:** 3 h 07 min nur per Refresh, 20 erfolgreiche Refreshs ohne weitere TAN. Beendet wurde die Session absichtlich. Eine Maximaldauer ist nicht aufgetreten, über 3 h hinaus ist sie unbekannt.
- **Access-Token:** `expires_in` 597 bis 598 s, nach jedem Refresh wieder gleich hoch.
- **Refresh-Token:** nach 15 min noch gültig, nach 20 min abgelehnt, also lebt es länger als 15 und höchstens 20 min. Ein abgelaufenes Access-Token verhindert den Refresh nicht.
- **Fehlerform bei Ablauf:** HTTP 401 mit `invalid_token`, wird als `ReloginRequired` eingestuft.
- **Session-ID:** `RequestInfo` blieb unverändert gültig, Umsätze kamen auch über 2 h nach der TAN.
- **Wiederaufnahme:** Ein neuer Prozess setzte aus der verschlüsselten Datei fort, ohne TAN. Der Stolperstein aus comdirect-api-simple Issue #16 trat nicht auf.
- **Umsatzabruf:** erfolgreich 2 h 01 min und 2 h 31 min nach der TAN. Davor fehlt die Messung, weil die Sonde bis dahin am Kontenabruf hing.
- **Kontenabruf:** `getAccounts` lieferte in allen sechs Runden 404. Die Ursache blieb offen, weil der Code jeden Fehlschlag als 404 meldete. Der Abruf ist seit `banking-import-v8n3k` entfernt, der Verbindungstest liest stattdessen die erste Umsatzseite (ADR 0071, Nachtrag 2026-11-01).
- **Offen, falsche Konto-Id:** Nicht gemessen, wie Comdirect auf eine fremde Konto-Id antwortet. Bei 401 oder 403 endet auch der Verbindungstest nach dem Refresh in einem Verlust (`banking-import-m4xrt`).

## Open questions
- **ING** als zweite Quelle: eigener Adapter oder Generalisierung des Comdirect-Musters?
- **Bank-Source-Port**: durch ADR 0071 Punkt 5 beantwortet (siehe **Bank-Port**). Offen bleibt der ING-Adapter.
