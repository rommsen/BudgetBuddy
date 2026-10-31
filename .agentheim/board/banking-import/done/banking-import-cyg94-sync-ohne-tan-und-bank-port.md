---
id: banking-import-cyg94
title: "Sync ohne TAN bei aktiver Bankverbindung, über einen schmalen Bank-Port"
status: done
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-tshq9]
blocks: [banking-import-t4raw]
tags: [backend, frontend, comdirect, sync, bank-port]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Das ist das eigentliche Ziel. Der Abruf steckt in `confirmTan`, die TAN-Entscheidung im Client:
`SyncFlow/State.fs` schickt nach `SyncStarted` immer `InitiateComdirectAuth`.

## What
- `BankPort`-Record, nur Comdirect implementiert ihn. Keine Umbenennung der `Comdirect*`-Typen.
- `fetchForReview` aus `confirmTan` ziehen.
- `startSync` liefert `SyncStart = TanRequired | ReviewReady`, `initiateComdirectAuth` entfällt.
  `src/Tests/RemotingEndpointNegativeTests.fs` und Fixtures in `scripts/*.test.mjs` im selben Schritt.
- `ReviewReady` lädt im Client direkt die Transaktionen. Toast-Text und Fortschritt nach Styleguide.

## Acceptance criteria
- [ ] Regression: GIVEN Comdirect-JSON mit fester Reference WHEN Decoder und `generateImportId` THEN `TransactionId` und `import_id` sind exakt wie vor dem Umbau, einschließlich heutiger Normalisierung.
- [ ] GIVEN `Connected` WHEN `startSync` THEN `ReviewReady`, keine Challenge.
- [ ] GIVEN `Disconnected` WHEN `startSync` THEN `TanRequired`, genau eine Challenge.
- [ ] GIVEN die Verbindung stirbt im Abruf WHEN `startSync` THEN `TanRequired`.
- [ ] GIVEN eine offene Prüfung WHEN `startSync` THEN dieselbe Sitzung zurück, Transaktionen unverändert.
- [ ] GIVEN ein Fake-Port WHEN `fetchForReview` THEN Regeln, Duplikate, Order-ID und Auto-Skip verhalten sich wie bisher.
- [ ] Browser-Wächter: ein Happy Path ohne TAN-Schritt und ein Pfad mit TAN.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Ein echter zweiter Sync läuft im Betrieb ohne TAN. [human-eye]

## Notes
Design-System: nichts Neues.

Promotion erst nach Go in `banking-import-p5mcp`.

## Verifier note (iteration 1)

VERDICT: FAIL

REASONS:
- Blockierend 1, überlebender Mutant an der harten Grenze: In `src/Client/Components/SyncFlow/State.fs:148` das Nachlesen auf `Debounce.delayed interruptedStartPollMs StartSync` geändert. Das entspricht einem automatischen Neustart alle 3 s, bei `Disconnected` also einer Challenge ohne Klick. Der .NET-Lauf bleibt grün: "nach verlorener Antwort wartet ein laufender Abruf weiter" prüft nur `Expect.isNonEmpty cmd`, und `scripts/sync-start.test.mjs` hat keinen Pfad mit `FetchingTransactions` oder `AwaitingBankAuth`.
- Blockierend 2, das Nachlesen endet nicht sicher: Der Zweig `Some _, Some { Status = (AwaitingBankAuth | FetchingTransactions) }` (State.fs:146) hat keine Obergrenze. Erreichbar, weil `confirmTanWith` kein `try/with` hat: Eine Ausnahme lässt die Sitzung dauerhaft in `FetchingTransactions`. `fetchingView` und `loadingView` haben keinen Abbrechen-Knopf.
- Blockierend 3, `cancelSync` wartet im Live-Weg doch auf den Abruf: `cancelSyncWith` (`src/Server/Api.fs:692`) ruft zuerst `discardPendingChallenge`, das `locked` nutzt. `withConnection` hält dasselbe `Cell.Gate` während des ganzen Bankabrufs. Belegt: Mit echtem Zellen-Gate und angehaltenem Fake-Abruf kam `discardPendingChallengeWith` in 3 s nicht durch, `tick` ebenso. Die Abbruch-Tests rufen `probe.Sessions.Clear ()` direkt im Abruf auf und bilden diese Reihenfolge nicht ab. Folge (aus dem Code abgeleitet): Stirbt die Verbindung im Abruf, kann die Challenge trotz Abbruch starten, `requireTan` liefert `SessionNotFound`, ein `ChallengePending` bleibt ohne Sitzung liegen. Outcome-Aussage "`cancelSync` wartet nicht auf den Abruf" und die Delta-Zeilen zu Abbruch und Verfall passen nicht zum Code.
- Bestanden: `VERIFY: PASS`, Datenintegrität (`generateImportId`, `matchesImportId`, Decoder unverändert), `fetchForReview` zeilenweise gleich dem alten `confirmTan`, Challenge beim Verbindungstod entspricht ADR 0071 Punkt 6 und entsteht höchstens einmal je `startSync`, Remoting-Fixtures vollständig.
- Hinweise ohne FAIL: Ein wartender zweiter Start ersetzt eine frische `AwaitingTan`-Sitzung und schickt eine zweite Push-TAN, ein einziges gescheitertes Neulesen beendet das Nachlesen (`Retry.reading` würde das entschärfen). `src/Client/State.fs:185` startet beim Routenwechsel eine weitere Nachlese-Kette. Der `with`-Zweig von `startSyncWith` ruft `Sessions.Fail` auch, wenn `LoadSettings` vor `StartNew` wirft, und markiert so die alte Sitzung als gescheitert.
- Hänger: Die Blätterschleife kann Takt und `cancelSync` im schlimmsten Fall rund 20 min blockieren und endet in einem falschen Verlust samt Challenge. Für den Commit tragbar, die Seitenobergrenze sollte aber vor dem Deploy kommen.
- Merge mit banking-import-z4h6p: `ConnectionPorts` bekommt `Store` und `LoadKeys` (Probe in `BankImportTests.fs`). `BankPort.Status` liest die Zelle ohne Gate, ein Klick während `resumeConnectionWith` kostet eine unnötige TAN. `applyRefreshFailure` wird dort asynchron, Reihenfolge prüfen.

SUGGESTED_FIX: (1) Die Folgeentscheidung des Nachlesens in eine reine Funktion ziehen oder einen Browser-Pfad Fetching zu Reviewing ergänzen, der `calls.start === 1` prüft. Die Assertion muss den Mutanten `StartSync` töten. (2) Das Nachlesen mit einer festen Frist begrenzen, danach bleibt ein Fehler sichtbar, Test dazu. Das Neulesen über `Retry.reading` führen. (3) In `cancelSyncWith` die Sitzung vor dem Warten auf das Gate löschen, oder `ChallengePending` ohne wartendes Gate behandeln, mit einem Test über das echte `Cell.Gate`. Delta und Outcome danach anpassen.

ITERATION_HINT: likely-fixable

Conductor-Zusatz für Iteration 2: Die Seitenobergrenze aus dem Backlog-Vorschlag banking-import-m3kx8 wird hier mit erledigt, ebenso `try/with` in `confirmTanWith`.

## Verifier note (iteration 2)

VERDICT: FAIL

REASONS:
- Überlebender Mutant am größten Betriebsrisiko: In `src/Server/ComdirectClient.fs` `getTransactionsWith` die Bedingung `not (List.isEmpty transactions) &&` entfernt. Die .NET-Suite bleibt grün, obwohl dann jede leere Folgeseite über das leer wahre `List.forall` in `InvalidResponse` läuft. Das träfe jedes Konto, dessen Daten im Zeitraum enden, bei jedem Sync. Das Intakt-Szenario "Datenende per leerer Seite" fehlt (Test-Doktrin Punkt 9). Der Produktionscode ist hier korrekt.
- Fehlalarme gegenüber dem alten Verhalten: Die letzte Seite trägt nur bekannte References, oder eine neue Buchung verschiebt zwischen zwei Abrufen eine Einzelbuchung am Datenende auf die nächste Seite. Das setzt ein erreichtes Datenende und eine sehr kleine letzte Seite voraus, ein erneuter Klick behebt es. Das Bankverhalten am Datenende ist nicht belegt.
- Bestanden: `VERIFY: PASS`, die drei blockierenden Befunde aus Iteration 1 sind behoben (alle Mutanten rot), Seite 1 kann die Prüfung nie auslösen, Vormerkungen kommen wegen `transactionState=BOOKED` nicht an, `InvalidResponse` löst weder Refresh noch Verlust noch Challenge aus, Fehler statt Teilergebnis ist richtig, Verwerfen kann `Connected` nicht zerstören, Frist von 2 Minuten lässt die Prüfung über Neuladen erreichbar, `fetchForReview` unverändert.
- Hinweise: Delta-Zeile "Eine Seite ohne neue Reference" muss "eine vollständig im Zeitraum liegende Seite" heißen. Das Delta hängt eine zweite `**Invariante Abbruch:**` neben die vorhandene, zusammenlegen. Alte Open-questions-Zeile von Hand entfernen. Merge mit banking-import-z4h6p: `Store` und `LoadKeys`, Speichern in `startChallenge` und `discardPendingChallengeWith`, Reihenfolge in `withConnection`, `BankPort.Status` ohne Gate.

SUGGESTED_FIX: In `src/Tests/ComdirectHttpTests.fs` einen Intakt-Test: Seite 1 vollständig im Zeitraum, Folgeseite `{"values":[]}`, Ergebnis `Ok` mit allen Buchungen. Optional die Fehlalarme verkleinern: Eine Seite, die kürzer ist als die erste und keine neue Reference bringt, gilt als Datenende und liefert `Ok`, eine volle Seite ohne neue Reference bleibt `InvalidResponse`. Tests für beide Fälle.

ITERATION_HINT: likely-fixable

## Outcome

Der Server entscheidet jetzt über die TAN. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 2852, node 342). Das README-Delta besteht `check:readme-delta` (Exit 0).

**Nicht live gelaufen:** Es gab keinen Aufruf gegen Comdirect oder YNAB, alles läuft gegen Fake-Ports und gemockte API-Antworten. Das Kriterium "Ein echter zweiter Sync läuft im Betrieb ohne TAN" [human-eye] ist offen.

**Bank-Port (`src/Server/BankPort.fs`):** Record mit Status, Auth starten, Auth abschließen, Konten und Umsätzen. `comdirectBankPort` legt ihn über `ConnectionPorts`, die Tests laufen dadurch über den echten Zustandsautomaten. Die `Comdirect*`-Typen bleiben. `StartAuth` nimmt eine Bedingung und liefert `Ok None` ohne Bankkontakt, wenn sie nicht mehr gilt.

**Abruf und Start (`src/Server/BankImport.fs`):** `fetchForReview` holt Umsätze über die konfigurierte Konto-Id, wendet Regeln, Duplikate, Order-ID und Auto-Skip an und fasst die Sitzung nicht an. Es hängt nicht am Kontenabruf, ein Test belegt das. `startSyncWith` und `confirmTanWith` liegen dort, `Api.fs` verdrahtet sie nur noch (`liveImportPorts`). `initiateComdirectAuth` entfällt, ebenso die ungenutzten `startAuth` und `fetchTransactions` in `ComdirectAuthSession`.

**Absicherung über den Auftrag hinaus:**
- `startSync` und `confirmTan` laufen hinter einem Semaphor. Ein zweiter Start während des Abrufs wartet und bekommt dieselbe Prüfung, statt die Sitzung zu ersetzen.
- Die offene Prüfung wird vor den Zugangsdaten geprüft, sie kommt also auch ohne Zugangsdaten zurück.
- Die Negativzeile für `startSync` ruft `startSyncWith` mit leerer Sitzung auf, weil eine offene Prüfung aus einer parallelen Testliste sonst unverändert zurückkäme.

**Abbruch (`cancelSyncWith`, `discardPendingChallengeWith`):** `cancelSync` wartet nicht mehr am `Cell.Gate`. Er löscht zuerst die Sitzung und verwirft danach die Challenge. Ist das Gate frei, geschieht das sofort, ist es belegt, läuft das Verwerfen im Hintergrund, sobald das Gate frei wird. Antwortet `cancelSync` mit `Ok`, kann bei laufendem Bankaufruf also noch für diesen Moment ein `ChallengePending` bestehen. `requireTan` prüft die Sitzung bei gehaltenem Gate direkt vor der Anmeldung (`startChallengeWhileWith`). Damit gilt: Kommt der Abbruch vorher, geht nichts zur Bank, kommt er während der Anmeldung, wurde die Push-TAN schon ausgelöst, die Challenge wird aber verworfen. Ein Ergebnis nach dem Abbruch verfällt, stirbt die Verbindung nach dem Abbruch, entsteht keine Challenge und kein liegengebliebenes `ChallengePending`. `confirmTan` holt nach einem Abbruch während der TAN-Bestätigung nichts mehr ab, die Verbindung bleibt. Die Tests laufen über das echte `Cell.Gate` mit angehaltenem Fake-Bankaufruf.

**Ausnahmen:** `confirmTanWith` fängt Ausnahmen ab, die Sitzung wird `Failed` und bleibt nie in `FetchingTransactions`. Das gilt auch für eine Ausnahme in `CompleteAuth` bei `AwaitingTan`: Die Sitzung scheitert, weiter geht es mit einem neuen Start. In `startSyncWith` markiert nur eine Ausnahme nach `StartNew` die Sitzung. Wirft `LoadSettings` davor, bleibt eine ältere Sitzung unberührt.

**Seitenobergrenze und Datenende (`getTransactionsWith` in `src/Server/ComdirectClient.fs`):** Eine vollständig im Zeitraum liegende Seite ohne neue Reference, die kürzer ist als die erste Seite, gilt als Datenende: Sie fällt weg, der Abruf liefert `Ok` mit den bisher gesammelten Buchungen, also ohne Dopplung. Eine solche Seite, die gleich lang oder länger ist, und mehr als `MaxTransactionPages` (100) Seiten beenden den Abruf mit `InvalidResponse`. Eine doppelte Reference innerhalb einer Seite bleibt erhalten. Eine leere Folgeseite und eine leere erste Seite beenden den Abruf mit `Ok`. Entscheidung: Fehler und kein Teilergebnis, weil sonst Buchungen still fehlen könnten. Bekannter Preis: Wiederholt die echte Bank am Datenende eine gleich lange Seite, statt leer zu antworten, schlägt der Abruf sichtbar fehl. Das Bankverhalten am Datenende ist nicht belegt. Das alte Verhalten wäre in diesem Fall endlos gelaufen, im Betrieb trat das nicht auf. `InvalidResponse` zählt nicht als Ablehnung: kein Refresh, kein Verlust, keine Challenge. Mehrere echte Seiten kommen vollständig an.

**Client:** `ReviewReady` lädt die Transaktionen direkt, `TanRequired` zeigt den TAN-Schritt. Toasts sind deutsch ("Bitte bestätige die TAN auf deinem Handy", "Umsätze bereit zur Prüfung"), der Ladetext heißt "Bank wird abgefragt...". Während des Abrufs in `startSync` zeigt der Client diesen Ladezustand, weil er vor der Antwort nicht weiß, ob eine TAN nötig ist.

**Netzabriss während `startSync`:** Eine geworfene Ausnahme wird zu `SyncStartInterrupted` und löst `LoadCurrentSession` aus, jetzt über `Retry.reading`. Fachliche `Error` bleiben unverändert. Der Client übernimmt `AwaitingTan` und `ReviewingTransactions` vom Server. Solange der Server noch abruft, liest er alle 3 s nur neu (`StartRecovery`, `InterruptedStartReadDue`), höchstens 40 Pausen, also 2 Minuten. Danach, ohne Sitzung, bei einer alten `Completed`-Sitzung oder wenn das Neulesen auch nach den Wiederholungen scheitert, bleibt ein Fehler sichtbar. Ein Lesen vom Routenwechsel startet keine zweite Kette und verbraucht die Frist nicht, ein veralteter Takt liest nicht. Ein neuer Start und damit eine neue Challenge entsteht nur durch Romans Klick. `LoadCurrentSession` nutzt `Cmd.OfAsync.either`, vorher blieb der Client bei einem Fehler ewig auf Laden.

**Exception-Zweig von `confirmTan` im Client, nicht geändert:** Er wird zu `Error TanTimeout`, und `TanConfirmed (Error _)` löst bereits `LoadCurrentSession` aus. Es bleiben drei Schwächen: Der Toast meldet einen Timeout, auch wenn es ein Transportfehler ist. Läuft der Abruf auf dem Server noch, bleibt der Fortschritt ohne erneutes Lesen stehen, bis Roman neu lädt. Ohne Sitzung auf dem Server bleibt nur der Toast sichtbar. Siehe Backlog-Eintrag.

**Regression Importschlüssel:** `src/Tests/ComdirectHttpTests.fs` prüft mit fester Reference `3F2A-91C0-XK7Z` über den echten Decoder `TransactionId` und `BB:3F2A91C0XK7Z`. `generateImportId` und der Decoder sind unverändert.

**Gegenproben Iteration 1** (je eine Änderung, danach zurückgebaut): Semaphor entfernt, Prüfung auf offene Sitzung abgeschaltet, immer Challenge statt Abruf, keine Challenge bei Verbindungstod, jeder Bankfehler startet eine Challenge, Ergebnis trotz Abbruch installiert, Auto-Skip, Duplikate und Order-ID entfernt, YNAB-Tage geändert, `generateImportId` ohne Normalisierung, `TransactionId` kleingeschrieben, `ReviewReady` lädt nicht: alle rot.

**Gegenproben Iteration 2** (tatsächlich ausgeführt in einer eigenen Kopie ohne `.env`, je Lauf eine Änderung, 21 Mutanten, alle rot):
- Reduzierer: Takt-Handler startet neu, keine Frist, Zähler läuft nicht, zweite Kette erlaubt, veralteter Takt liest.
- Browser: verzögert `StartSync` statt Lesen (`calls.start` 2 statt 1, der Mutant aus dem Verifier-Befund), ohne `Retry.reading`.
- Abbruch: Reihenfolge getauscht, Abbruch wartet am Gate, kein Verwerfen hinter dem Gate, `stillWanted` ignoriert, Challenge trotz Abbruch im Abruf.
- `confirmTan`: ohne `try` (Testlauf bricht mit Fehler ab), ignoriert Abbruch.
- Blättern: Seitenprüfung entfernt, Wiederholung als Ergebnis, Obergrenze als Ergebnis, Seitenfehler als Ablehnung, Obergrenze um eins daneben.
- `startSync`: `with`-Zweig markiert alte Sitzung, Sitzung bleibt nach Ausnahme stehen.

**Gegenproben Iteration 3** (tatsächlich ausgeführt in einer eigenen Kopie ohne `.env`, je Lauf eine Änderung):
- Datenende: Bedingung `isEmpty` weg, Längenvergleich weg, umgedreht (`>`), `<=`, `<>`, nie Datenende, Datenende liefert leeres Ergebnis, Datenende hängt die Seite an, Datenende entdoppelt, Länge an der vorigen statt der ersten Seite: alle rot.
- `isEmpty` weg: Vor dem Datenende-Fix machte die leere Folgeseite diesen Mutanten rot. Nach dem Fix liefert die leere Folgeseite auch ohne die Bedingung `Ok`, rot macht ihn nur noch der Test "ein Konto ohne Buchungen" (leere erste Seite). Beide Tests bleiben.
- "Datenende auch mit neuer Reference" (`forall` zu `exists`) überlebte zunächst und hätte neue Buchungen still verloren. Der Test "eine kürzere Seite mit neuer Reference" macht ihn jetzt rot.
- Der Lauf der übrigen zehn Mutanten stammt von vor diesem letzten Test, nur der Überlebende wurde danach wiederholt.

**Überlebende Mutanten, Korrektur:** Die Aussage aus Iteration 2 "keiner" war falsch. Der Mutant `isEmpty` weg war nicht in der Liste und überlebte, der Verifier fand ihn. Die Aussage galt nur für die 21 ausgeführten Mutanten. Stand jetzt: In Iteration 3 überlebt keiner der elf Mutanten. Das gilt nur für die hier genannten Mutanten am Blättern. Aus Iteration 1 bleibt die Gegenprobe "Immer Abruf statt Status prüfen" als bekannter Überlebender stehen: Bei `Disconnected` liefert `withConnection` ohne Bankkontakt `SessionExpired`, danach greift derselbe Weg in die Challenge. Das Verhalten ist gleich, die Statusprüfung spart nur den Umweg. Diese Probe wurde in Iteration 2 und 3 nicht wiederholt.

**Zur Reihenfolge:** In Iteration 3 waren die drei Datenende-Tests vor dem Fix rot. Die Tests aus Iteration 2 entstanden gleichzeitig mit den Fixes und wurden nicht einzeln vor dem Fix rot gesehen. Die Rot-Belege sind die Mutanten oben, die das alte Verhalten wiederherstellen.

**Bleibt offen:**
- Überlappt eine Seite teilweise mit Bekanntem (neue und bekannte Reference, oder eine bekannte Reference auf einer Seite, die den Zeitraum verlässt), landet die bekannte Buchung wie vor diesem Task doppelt im Ergebnis. Das Datenende filtert nur die ganze Seite weg. Der neue Test prüft deshalb nur, dass die neue Buchung ankommt.
- Ein wartender zweiter Start ersetzt eine frische `AwaitingTan`-Sitzung und schickt eine zweite Push-TAN (Verifier-Hinweis aus Iteration 1, nicht Teil dieses Auftrags). Sie entsteht nur durch einen Klick.
- `fetchingView` und `loadingView` haben weiter keinen Abbrechen-Knopf. Der Ausweg ist die Frist.
- Die alte Zeile "Lohnt ein generischer Bank-Source-Port" unter Open questions in der README ist per `replace` nicht erreichbar (kein fetter Kopf). Auch die Umgehung über den Anker `ING` lehnt `check:readme-delta` ab (Exit 1, der Extent endet vor dieser Zeile). Das Delta ergänzt deshalb einen Hinweis, die Zeile selbst muss von Hand weg.
- Merge mit `banking-import-z4h6p`: `ComdirectAuthSession.fs` ist dort auch in Arbeit. Hier geändert: `startChallengeWhileWith` und `discardPendingChallengeWith`, entfernt die zwei ungenutzten Wrapper. Die Probe in `BankImportTests.fs` braucht dort die neuen `ConnectionPorts`-Felder. Wird `applyRefreshFailure` asynchron, die Reihenfolge prüfen.
