---
id: banking-import-z4h6p
title: "Bankverbindung übersteht einen Neustart über verschlüsselt gespeichertes Refresh-Token"
status: done
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-tshq9]
blocks: []
tags: [backend, comdirect, session, persistenz, secrets]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Ein Deploy soll keine TAN kosten, solange das Refresh-Token noch gilt.

## What
Verschlüsselter JSON-Wert unter einem Settings-Schlüssel nach ADR 0071 Punkt 4, geschrieben vor
der Verwendung des neuen Tokens, ein Wiederaufnahmeversuch beim Start.

## Acceptance criteria
- [ ] GIVEN Refresh erfolgreich THEN der gespeicherte Wert enthält das neue Token, bevor es verwendet wird.
- [ ] GIVEN ein gespeicherter Wert WHEN Start und Refresh gelingt THEN `Connected`, keine Challenge.
- [ ] GIVEN `ReloginRequired` beim Start oder ein nicht entschlüsselbarer Wert THEN Wert gelöscht, `Disconnected`, keine Challenge.
- [ ] GIVEN `RetryLater` beim Start THEN der Wert bleibt, nächster Versuch nach 60 s.
- [ ] GIVEN kein `BUDGETBUDDY_ENCRYPTION_KEY` THEN nichts gespeichert, die Verbindung lebt im Speicher.
- [ ] GIVEN Trennen oder geänderte Zugangsdaten THEN Wert gelöscht.
- [ ] Der Wert erscheint nie in `getSettings` (Vorbild `src/Tests/SettingsSecretsTests.fs`).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Entfällt, wenn die Wiederaufnahme in `banking-import-p5mcp` scheitert.

## Verifier note (iteration 1)

VERDICT: FAIL

REASONS:
- Hängender Takt hält das Beenden auf: `src/Server/BankConnectionWorker.fs:30` wartet mit `do! Async.StartAsTask(guarded "Takt" tick)` ohne `stopping`, nur die Wiederaufnahme (Zeile 27) hat `.WaitAsync(stopping)`. Gegenprobe zweimal rot: hängender `tick`, `worker.StopAsync(None).Wait(5 s)` kehrt nicht zurück. Der Tick wartet außerdem am `Gate`, also blockiert auch ein hängender Nutzerabruf das Beenden. `HttpClient` hat keinen eigenen Timeout (Default 100 s), es gibt keine `HostOptions`, `compose.yaml` setzt kein `stop_grace_period`. Die Outcome-Aussage "Eine hängende Bank hält weder den Start noch das Beenden auf" stimmt damit nicht, und nach `RetryLater` trifft der nächste Versuch gerade diesen Takt-Weg.
- Die Verdrahtung ist ungetestet. Vier Mutanten überleben die gesamte Suite: W1 `livePorts.Store` mit `(fun () -> false)`, es wird nie gespeichert. W2 `Program.fs` übergibt statt `resumeConnection` ein `(fun _ -> async.Return ())`, keine Wiederaufnahme. W3 `hasConfiguredKey` liefert immer `true`, der Maschinen-Fallback schützt dann das Token, das verletzt das Kriterium "kein Schlüssel" und ADR 0071 Punkt 4. W4 `livePorts.LoadKeys` liefert `None`, jeder Wert wird beim Start gelöscht. Die Kriterien 1, 2 und 5 sind damit nur an Fake-Ports belegt.
- Überlebender Mutant N1: ohne `ports.ReportLoss` in `dropUnusableStored` (`src/Server/ComdirectAuthSession.fs:149`) bleibt alles grün. Kein Test prüft `Losses` im Fall `StoredUnreadable` oder `Keys = None`.
- Lücke bei "geänderte Zugangsdaten THEN Wert gelöscht" (aus dem Code gelesen): `saveComdirectCredentialsWith` (`src/Server/Api.fs:210-218`) schreibt die Zugangsdaten vor `disconnect ()`. Scheitert `Store.Clear`, liefert der Aufruf `DatabaseError`, die neuen Daten liegen aber schon. Ein zweiter Speicherversuch sieht `credentialsChanged = false` und löscht nicht mehr, der nächste Start nimmt den Wert mit den neuen Schlüsseln wieder auf.
- Bestanden: Gate `VERIFY: PASS`, kein Token verlässt den Server, Reihenfolge Refresh, Speichern, Zelle stimmt, kein automatischer Challenge-Pfad nach Neustart, `RetryLater` zulässig. Der Satz in ADR 0071 Punkt 4 "Scheitert er, wird der Wert gelöscht" braucht beim Integrieren einen Nachtrag.
- Merge mit banking-import-cyg94: `src/Tests/BankImportTests.fs:141-156` baut einen eigenen `ConnectionPorts`-Record und kompiliert ohne `Store` und `LoadKeys` nicht. `BankPort.fs:29` liest `connectionState` ohne `Gate`: Direkt nach dem Start, vor der Wiederaufnahme, steht dort `Disconnected`, ein Sync in diesem Fenster startet eine Challenge und löscht den Wert, das kostet eine TAN. Der `fetchForReview`-Zweig "Verbindung stirbt" muss berücksichtigen, dass nach `RetryLater` weiter `Connected` gilt.

SUGGESTED_FIX: Den Takt ebenfalls mit `.WaitAsync(stopping)` an das Beenden binden und mit einem Test "hängender Takt" belegen. `hasConfiguredKey` über einen eingespeisten Leser testen, die Verdrahtung von `resumeConnection` in `Program.fs` im vorhandenen Strukturwächter festhalten und `livePorts` so testbar machen, dass `Store` und `LoadKeys` geprüft sind, etwa über einen Test gegen die In-Memory-DB. Zusätzlich `Losses` im Fall des unbrauchbaren Werts prüfen und in `saveComdirectCredentialsWith` das Trennen vor dem Schreiben der Zugangsdaten ausführen.

ITERATION_HINT: likely-fixable

## Verifier note (iteration 2)

VERDICT: FAIL

REASONS:
- Überlebender Mutant W1: `liveConnectionStore` in `src/Server/BankConnectionStore.fs:80` auf `connectionStoreFor (fun _ -> null) connectionSettingKey` gesetzt, ebenso auf `settingsConnectionStore (fun () -> false) connectionSettingKey`. Beide bleiben grün. Der Produktionsserver speichert dann nie, ein Deploy kostet wieder eine TAN. Ursache: Der Test "der laufende Server hält den Wert unter dem festen Schlüssel und folgt der echten Umgebung" (`src/Tests/RemotingEndpointNegativeTests.fs:352`) leitet die Erwartung aus der Prozessumgebung ab. Das Gate läuft ohne `BUDGETBUDDY_ENCRYPTION_KEY`, er erwartet dann `NothingStored`, und genau das liefert der kaputte Speicher. Der Test prüft je Lauf nur eine Richtung.
- Überlebender Mutant W2c: `resumeConnection` in `src/Server/ComdirectAuthSession.fs` auf `async.Return ()` gesetzt, die Suite bleibt grün. `isBankWorkerWired` prüft nur den Namen in `Program.fs`, nicht dass `resumeConnection` an `resumeConnectionWith livePorts` hängt.
- Der Strukturwächter erfüllt Punkt 7 der Doktrin für `Program.fs` und `livePorts`, die Gegenbeweise sind echt. W3c und W1 sind kein Strukturthema, dort fehlt die Prüfung in beiden Richtungen in einem Gate-Lauf.
- Sonst in Ordnung: `VERIFY: PASS`. Rot wurden Takt ohne `stopping`, W1a, W1save, W2, W3, W3c, W4, W4b, N1, A1, A2, R1. Der neue Test erreicht weder echte DB noch Netz. Bei gleichen Zugangsdaten trennt `saveComdirectCredentialsWith` nicht. Der Takt-Fix kann keinen kaputten Zustand erzeugen, schlimmster Fall ist eine TAN, von ADR 0071 Punkt 4 hingenommen.

SUGGESTED_FIX: `liveConnectionStore` im Strukturwächter auf `connectionStoreFor System.Environment.GetEnvironmentVariable connectionSettingKey` festlegen, mit Gegenbeweisen für `fun _ -> null`, `fun _ -> "x"` und `settingsConnectionStore (fun () -> false)`. Alternativ den Test in einem Kindprozess mit gesetztem Schlüssel. Den Wächter zusätzlich auf `resumeConnection`, das an `resumeConnectionWith livePorts` hängt, erweitern. W1null und W2c im Outcome als rot belegen.

ITERATION_HINT: likely-fixable

## Verifier note (iteration 3)

VERDICT: FAIL

REASONS:
- Kriterium "GIVEN Trennen oder geänderte Zugangsdaten THEN Wert gelöscht" ist im laufenden Server unbelegt. `disconnect` auf `async.Return ()` in `src/Server/ComdirectAuthSession.fs:265-266` bleibt grün, ebenso `(fun () -> async.Return ())` statt `ComdirectAuthSession.disconnect` in `src/Server/Api.fs:302`. Folge: Geänderte Zugangsdaten lassen den Wert liegen, der nächste Start nimmt das alte Token mit den neuen Schlüsseln wieder auf.
- Kriterium "GIVEN RetryLater beim Start THEN der Wert bleibt, nächster Versuch nach 60 s" ist im laufenden Server unbelegt. `runTick` auf `async.Return ()` (`src/Server/ComdirectAuthSession.fs:268-269`) bleibt grün. Dass `runTick` aus banking-import-tshq9 stammt, entlastet nicht.
- Tragbar als Folgeaufgabe: `ReportLoss` auf `ignore` in `livePorts`. `Persistence.Settings.getSetting` statt `(fun _ -> async.Return None)` in `Api.fs:300` bricht kein Kriterium, schließt aber mit demselben Wächter.
- Bestanden: `VERIFY: PASS` (dotnet 2845, node 336), W1null, W1false, W1x, W2c rot, `isBoundExactlyTo` eng genug, keine Metrik-Drift, Outcome und Delta stimmen mit dem Code.

SUGGESTED_FIX: Keine Produktionsänderung, nur drei Wächter in `src/Tests/StructureGuardTests.fs` mit Gegenbeweisen: `isBoundExactlyTo "runTick" @"tick\s+livePorts\s+now"`, `isBoundExactlyTo "disconnect" @"disconnectWith\s+livePorts"`, und für das Record-Feld in `Api.fs` `saveComdirectCredentials\s*=\s*saveComdirectCredentialsWith\s+Persistence\.Settings\.getSetting\s+Persistence\.Settings\.setSetting\s+ComdirectAuthSession\.disconnect\b`. Vom Verifier in seiner Kopie geprüft: Basis ohne Rot, `L-runTick`, `L-disconnect`, `L-apiDisconnect`, `L-apiGet` je rot. `ReportLoss` bleibt in banking-import-q7ds2. Danach Outcome und Delta anpassen.

ITERATION_HINT: likely-fixable

## Outcome

Die Bankverbindung übersteht einen Neustart über einen verschlüsselten Wert nach ADR 0071 Punkt 4. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 2989 grün, node 366 grün). Das README-Delta besteht `check:readme-delta` (Exit 0) gegen die README von `main`.

**Reichweite der Tests:** Nichts lief gegen Comdirect oder YNAB. Alles ist gegen Fake-Ports und In-Memory-SQLite geprüft, dass die Wiederaufnahme gegen die echte Bank klappt, stützt sich nur auf den Live-Befund der Sonde. Der Gate-Lauf hat keinen `BUDGETBUDDY_ENCRYPTION_KEY`, Speichern mit Schlüssel läuft dort nie gegen den echten Speicher. Die Strukturwächter sind Belege am Quelltext, kein Verhalten.

**Wert (`src/Server/BankConnectionStore.fs`):** `StoredConnection` liegt verschlüsselt unter `comdirect_bank_connection`. Ohne gesetzten Schlüssel wird nichts geschrieben, ein vorhandener Wert gilt als unlesbar, auch nicht über den Maschinen-Fallback. Decoder-Meldungen fallen weg, weil Thoth darin den Wert zitiert. `Persistence.Encryption.hasConfiguredKey` bekommt die Umgebung als Leser, neu ist `readEncryptedSetting`, weil `getSetting` aus einem kaputten Wert `None` macht.

**Speichern und Löschen (`src/Server/ComdirectAuthSession.fs`):** `refreshConnection` schreibt den Wert, bevor die Zelle das neue Token bekommt. Gelöscht wird bei Verlust, Trennen, geänderten Zugangsdaten und neuer Challenge.

**Wiederaufnahme:** `resumeConnectionWith` läuft unter dem Semaphor, nur aus `Disconnected`, immer mit Refresh. Der Hintergrunddienst (`src/Server/BankConnectionWorker.fs`) macht das einmal vor dem ersten Takt. Ein Fehler beendet den Dienst nicht, auf Wiederaufnahme und Takt wartet er nur bis `stopping`.

**Zugangsdaten (`src/Server/Api.fs`):** `saveComdirectCredentialsWith` trennt vor dem Schreiben. Scheitert `Store.Clear`, kommt `DatabaseError`, nichts ist geschrieben, der zweite Versuch sieht die Änderung noch.

**Merge mit banking-import-cyg94 (Iteration 4):**
- Konflikt gelöst: `startChallenge` bleibt privat und wird von `startChallengeWith` und `startChallengeWhileWith` genutzt, das Löschen des Werts liegt darin. Der Wrapper `startAuth` bleibt entfernt, `resumeConnection` bleibt.
- `startChallengeWhileWith`: Nur eine tatsächlich gestartete Challenge löscht den Wert. `stillWanted = false` und eine gescheiterte Anmeldung lassen ihn stehen.
- `discardPendingChallengeWith` fasst den Wert nicht an. Eine offene Challenge hat ihn schon gelöscht, zu `Connected` gehört er weiter. Getestet mit freiem und belegtem echten Gate.
- `applyRefreshFailure` ist asynchron. Reihenfolge in `withConnection`: erst Zelle, dann Löschen, dann Meldung. Tests prüfen den Zustand der Zelle im Moment von `Clear` und `ReportLoss`, für beide Verlustwege.
- `getTransactionsWith` im Client kennt die Verbindung nicht und bleibt unverändert. Die Behandlung liegt in `withConnection` und `fetchOrRequireTan`: Nach `RetryLater` gilt `Connected`, `Status` liefert `BankReady`, ein Abruffehler endet als `TransactionFetchFailed` mit gescheiterter Sitzung, ohne Challenge, ohne Verlust, der Wert bleibt.
- `BankImportTests.fs`: `Store` und `LoadKeys` in der Probe.

**Fenster nach dem Start:** `BankPort` hat das neue Feld `Resume`, dahinter `awaitResumeWith`. `startSyncWith` ruft es nach `RecordStarted` und vor `Status`. So ist der Fall ohne gestartete Wiederaufnahme genauso abgedeckt wie der Klick während der Wiederaufnahme: Der Versuch ist wiederholbar, beide teilen das echte `Gate`, der zweite findet `Connected` vor. Tests laufen mit dem echten `Cell.Gate`, auch für den Moment, in dem das Gate belegt ist und die Zelle noch `Disconnected` zeigt. Ein Abbruch während des Wartens beendet den Start ohne Bankkontakt (`isCurrent` nach `Resume`). Wirft das Lesen des Werts, geht der Klick weiter in die TAN.

Über den Auftrag hinaus: `testComdirectConnectionWith` hatte dasselbe Fenster und ruft jetzt ebenfalls `awaitResumeWith`.

**Grenze ehrlich:** Es gibt keine eigene Frist. Sie brächte nichts, weil Abruf und Challenge danach am selben `Gate` warten. Hängt die Bank, wartet der Klick bis zum Default-Timeout des `HttpClient` (100 s, aus dem Code abgeleitet, nicht gemessen), danach ist das Urteil `RetryLater`. `startSync` bleibt so lange im Ladezustand, `fetchingView` und `loadingView` haben keinen Abbrechen-Knopf (aus cyg94). Serverseitig wartet `cancelSync` nicht mit, ein Test belegt das mit hängender Bank. Neue Blockade entsteht keine: Dasselbe Warten gab es schon für den Abruf hinter einem laufenden Refresh.

**Live-Anschlüsse, vollständige Liste (Doktrin Regel 3):**
- `Program.fs` Worker-Argumente `resumeConnection`, `runTick`, `tickPeriod`: Wächter `isBankWorkerWired`, Mutanten L-tickProgram, L-tickPeriod.
- `resumeConnection`: Wächter `isResumeOnLivePorts` (Iteration 3, W2c).
- `runTick`, `disconnect`, `confirmTan`, `discardPendingChallenge` an `livePorts`: Wächter `isSessionBoundToLivePorts`, Mutanten L-runTick, L-disconnect, L-confirmLive, L-discardLive.
- `livePorts`: `Refresh`, `Store`, `LoadKeys`: Wächter `isLivePortsStorageWired`, Mutante L-refreshLive, W4 aus früheren Iterationen.
- `livePorts.ReportLoss`: offen, Mutante L-report überlebt, geht in `banking-import-q7ds2`.
- `liveConnectionStore`: Wächter `isLiveStoreOnRealEnvironment` (Iteration 3, W1null, W1x, W1false). `loadLiveApiKeys`: In Iteration 3 per Mutante L-keysEarly rot, nicht wiederholt.
- `Api.fs`, Wächter `findMissingApiWirings`: `saveComdirectCredentials` (Mutanten L-apiGet, L-apiSet, L-apiDisconnect), `testComdirectConnection` (L-apiTestPorts), `liveImportPorts.Bank = comdirectBankPort livePorts` (L-bankLive), `startSync` (L-apiStart), `confirmTan` (L-apiConfirm), `cancelSync` (L-apiCancel).
- `comdirectBankPort`, Feld `Resume`: Verhalten, die Tests laufen über den echten Port (R-noField).
- Übrige Felder von `liveImportPorts` (`LoadRules`, `LoadYnabTransactions`, `Sessions`, `LoadSettings`) und `Login`, `CompleteAuth`, `FetchAccounts`, `FetchTransactions` in `livePorts`: gehören nicht zu diesem Task, nicht geprüft.

**Gegenprobe Iteration 4:** Kopie ohne `.env`, `.git`, `bin`, `obj`, `dist`, `node_modules` als Verweis, ein Lauf zur Zeit, ohne `BUDGETBUDDY_ENCRYPTION_KEY`, je Mutant die ganze .NET-Suite. Ein erster Lauf hatte 6 rote Basistests (fehlendes `node_modules`), er wurde verworfen. Die neue Basis: 2998 Tests, 0 rot. Danach wurde ein Test verschärft, das ist nicht mehr über die Mutanten gelaufen, nur im Gate. Skript und Ergebnisse: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/9c2c0bc0-e77c-41ab-a823-9f34648f67b5/scratchpad/z4h6p-iter4-worker/mut.py` und `result-*.txt` daneben.

Rot (31):
- Verdrahtung: L-runTick, L-disconnect, L-apiDisconnect, L-apiGet, L-apiSet, L-apiTestPorts, L-bankLive, L-apiStart, L-apiConfirm, L-apiCancel, L-confirmLive, L-discardLive, L-refreshLive, L-tickProgram, L-tickPeriod.
- Nachzüge Punkt 1: N-discardClears (Verwerfen löscht), N-whileClearsEarly (Löschen vor `stillWanted`), N-startNoClear, N-clearBeforeLogin, N-orderCell (Zelle nach Löschen und Meldung), N-retryLaterTan (jeder Abruffehler startet eine Challenge), N-retryDrops (`RetryLater` trennt).
- Punkt 2: R-noField, R-noCall, R-afterStatus (Status vor `Resume` gelesen), R-noCurrent, R-noTry, R-testConn, R-resumeUnlocked, R-resumeWhenConnected.

Überlebend (1): L-report, `ReportLoss` in `livePorts` auf `ignore`. Der Verlust geht dann nicht ins Server-Log. Der Auftrag lässt das offen.

Diese Aussage gilt nur für die genannten 32 Mutanten. Die Tests aus Iteration 4 entstanden mit den Fixes und wurden nicht einzeln vor dem Fix rot gesehen. Der eine beim ersten Lauf rot gewordene Test war ein bestehender cyg94-Test, dessen Wartepunkt sich verschoben hat. Er umgeht jetzt `Resume` und prüft weiter `stillWanted`, ein neuer Test deckt das Warten vor dem Gate ab. Die Rot-Belege sind die Mutanten oben.

**Korrektur früherer Aussagen:** "14 Mutanten, kein Überlebender" aus Iteration 2 war falsch, W1 und W2c überlebten dort. Die Outcome-Liste aus Iteration 3 nannte fünf Überlebende. Vier davon sind jetzt rot.

**Entscheidungen im Rahmen des ADR:**
- `resumedConnection` setzt `ExpiresAt` auf `None`, sonst wäre der Refresh nach `RetryLater` erst kurz vor Ablauf des Access-Tokens fällig.
- `RetryLater` beim Start lässt `Connected` und den Wert stehen, wie das Kriterium es verlangt. ADR 0071 Punkt 4 sagt pauschal "Scheitert er, wird der Wert gelöscht", dieser Satz braucht beim Integrieren einen Nachtrag. Dasselbe gilt für "Beim Start ein Versuch": Der Klick kann einen weiteren Versuch auslösen.
- Gespeichert wird auch direkt nach der TAN, das ADR nennt nur den Refresh.
- Scheitert das Speichern, läuft die Verbindung im Speicher weiter, der nächste Neustart kostet eine TAN. Scheitert das Löschen beim Trennen, kommt der Fehler an, bei Verlust und neuer Challenge wird er nur geloggt.
- Ein unbrauchbarer Wert zählt als Verlust.

**Bleibt offen:**
- Bleibt ein Wert nach gescheitertem Löschen liegen, kann ein späterer Klick ihn nochmals versuchen. `ReloginRequired` löscht ihn dann, das meldet einen weiteren Verlust.
- Während der 60 s Pause nach `RetryLater` löst ein 401 im Abruf sofort einen Refresh aus. Das ist das Verhalten aus banking-import-tshq9 und bleibt so, es entsteht nur durch Romans Klick.
- Der Verlust geht bis `banking-import-bftq0` nur ins Server-Log.
