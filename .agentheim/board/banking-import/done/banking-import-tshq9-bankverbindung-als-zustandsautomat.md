---
id: banking-import-tshq9
title: "Bankverbindung als Zustandsautomat mit Erneuern im Hintergrund. Abbruch und Verbindungstest beenden sie nicht mehr"
status: done
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-p5mcp]
blocks: [banking-import-z4h6p, banking-import-bftq0, banking-import-cyg94]
tags: [backend, comdirect, session, hintergrunddienst]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Offene Challenge und aktive Session sind heute nicht unterscheidbar (`isAuthenticated` ist bei
beiden wahr). `cancelSync` löscht die Session, ein Verbindungstest ersetzt sie.

## What
- Reiner Automat `Disconnected | ChallengePending | Connected` nach ADR 0071 Punkt 2, Uhr als Parameter.
- Semaphor um Refresh und Bankzugriff.
- `tick ports now`, `BackgroundService` nur in `Program.fs`.
- Strukturwächter für `AddHostedService` mit Gegenbeweis, neue Module in den Netzwächter.
- `cancelSync` und Verbindungstest umstellen, geänderte Zugangsdaten trennen die Verbindung.
- `DateTime.Today` in `getTransactions` durch einen Parameter ersetzen.

## Acceptance criteria
- [ ] GIVEN `Connected` und das Token läuft in unter 60 s ab WHEN tick THEN genau ein Refresh, das neue Token ist in Verwendung.
- [ ] GIVEN `ChallengePending` WHEN tick THEN kein Refresh.
- [ ] GIVEN zwei gleichzeitige Refresh-Anlässe THEN ein Aufruf, beide nutzen das Ergebnis.
- [ ] GIVEN Refresh `ReloginRequired` THEN `Disconnected`, der Verlust ist einmal gemeldet, keine Challenge.
- [ ] GIVEN Refresh `RetryLater` THEN `Connected`, nächster Versuch nach 60 s.
- [ ] GIVEN ein Datenabruf antwortet 401 oder 403 THEN ein Refresh und ein Wiederholversuch, bei erneutem Fehlschlag `Disconnected`.
- [ ] Kaputt: GIVEN `Connected` WHEN `cancelSync` THEN die Verbindung bleibt (vor dem Fix rot). Intakt: GIVEN `ChallengePending` WHEN `cancelSync` THEN `Disconnected`.
- [ ] Kaputt: GIVEN `Connected` WHEN `testComdirectConnection` THEN keine Challenge, Verbindung unverändert. Intakt: GIVEN `Disconnected` THEN Challenge wie bisher.
- [ ] GIVEN Zugangsdaten geändert THEN `Disconnected`.
- [ ] GIVEN `Disconnected` WHEN beliebig viele ticks THEN keine Challenge und kein Passwort-Login.
- [ ] Ein Testlauf im TestServer startet keinen Dienst.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Offene Frage an Roman: Soll eine Verbindung nach fester Zeit zwangsweise eine neue TAN verlangen?
Stand ADR 0071: nein, "Bank trennen" reicht. Ein Ja wäre eine kleine Ergänzung hier.

Promotion erst nach Go in `banking-import-p5mcp`.

## Verifier note (iteration 1)
REASONS:
- Kriterium "GIVEN zwei gleichzeitige Refresh-Anlässe THEN ein Aufruf, beide nutzen das Ergebnis": Die Tests "zwei gleichzeitige Ticks: ein Aufruf" und "Tick und Datenabruf gleichzeitig: ein Aufruf, beide nutzen das Ergebnis" (src/Tests/BankConnectionTests.fs) sind nicht falsifizierbar. Gegenprobe: `locked` in src/Server/ComdirectAuthSession.fs:39 auf reines `return! work` reduziert, also Semaphor ausgebaut: "Bankverbindung: tick" 5 von 5 Läufen 9/9 grün, die Listen Datenabruf, Übergänge sowie Abbruch, Verbindungstest, Zugangsdaten ebenfalls grün.
- Ursache: `release.SetResult ()` läuft direkt nach dem Start beider Tasks, ohne zu warten, dass der erste Refresh im Port hängt. Der Test belegt keine Serialisierung. Das Semaphor ist der Kern von ADR 0071 Punkt 3 und damit ungetestet. Verletzt .claude/rules/test-doctrine.md Punkt 6.
- Lesebefund: Zugangsdaten werden als Tupel aller vier Felder verglichen, der Test deckt nur Username ab. Kommentar src/Shared/Api.fs:47 nennt noch "Challenge ID". Kommentar an `Probe` in BankConnectionTests.fs passt nicht zum Code. `ComdirectConnectionTest`, `TanChallengeStarted`, `BankConnectionActive` stehen nicht im README-Delta.
SUGGESTED_FIX: Beide Nebenläufigkeitstests so bauen, dass der zweite Anlass nachweislich startet, während der erste im Refresh-Port hängt: `BeforeRefresh` setzt ein "Refresh läuft"-Signal und wartet auf `release`, der Test wartet auf dieses Signal, startet dann den zweiten Anlass und gibt erst danach frei. Dabei prüfen, dass der zweite Anlass vor der Freigabe nicht fertig ist. `Probe.Refreshes` threadsicher machen. Gegenprobe: ohne Semaphor müssen die Tests rot sein, das im Outcome festhalten. Dazu den Rückgabe-Kommentar in src/Shared/Api.fs korrigieren.
ITERATION_HINT: likely-fixable

## Outcome

Die Bankverbindung ist als Zustandsautomat gebaut, der Hintergrunddienst erneuert sie. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 2790, node 336). Das README-Delta besteht `check:readme-delta` (Exit 0).

**Nicht live gelaufen:** Es gab keinen Aufruf gegen Comdirect. Alles ist gegen Fake-Ports und Fake-Handler geprüft, der Dienst lief nie gegen die echte Bank.

**Automat (`src/Server/BankConnection.fs`):** rein, Uhr als Parameter. `refreshDue` ist wahr unter 60 s Restlaufzeit, außer während der Pause von 60 s nach `RetryLater`. Fehlt die Ablaufzeit, ist der Refresh fällig. Ein Verlust entsteht nur beim Übergang aus `Connected` und wird deshalb einmal gemeldet.

**Session (`src/Server/ComdirectAuthSession.fs`):** `ConnectionPorts` trägt Zelle, Semaphor und alle Bankaufrufe. Jeder Zugriff läuft unter dem Semaphor. Ein Datenabruf zieht einen fälligen Refresh vor, gleichzeitige Anlässe teilen so das Ergebnis. 401 oder 403 führen zu einem Refresh und einem Wiederholversuch. Sagt der Refresh `RetryLater`, gibt es keinen Wiederholversuch, die Verbindung bleibt. Andere Fehler beim Wiederholversuch (etwa 503) trennen nicht, nur ein erneutes 401 oder 403. `tick` ruft ausschließlich den Refresh auf.

**Dienst (`src/Server/BankConnectionWorker.fs`):** `PeriodicTimer` im Takt von 15 s, registriert nur in `src/Server/Program.fs`. Ein Fehler in einem Takt wird abgefangen, weil eine Ausnahme aus `ExecuteAsync` sonst den Host stoppt. Der Strukturwächter mit Gegenbeweis steht in `src/Tests/StructureGuardTests.fs`, die neuen Module im Netzwächter.

**Nebenläufigkeitstests (`src/Tests/BankConnectionTests.fs`, `withRefreshHeld`):** Der Test wartet auf das Signal, dass der erste Anlass im Refresh-Port hängt. Dann startet er den zweiten mit `Async.StartImmediateAsTask`, der auf dem Testthread bis zur ersten echten Wartestelle läuft. Beim Zurückkehren steht der zweite Anlass also am Semaphor oder, ohne Semaphor, selbst im Refresh-Port. Erst nach den Prüfungen auf diesem Stand gibt der Test frei. Es gibt keine feste Wartezeit, das Zeitlimit von 30 s greift nur gegen Hänger. `Probe.Refreshes` und `Probe.Fetches` sind threadsicher.

**Gegenprobe tatsächlich ausgeführt:** `locked` temporär auf `return! work` reduziert, also ohne Semaphor: in 5 von 5 Läufen 2 von 36 Fällen rot, und zwar genau "zwei gleichzeitige Ticks: ein Aufruf" und "Tick und Datenabruf gleichzeitig: ein Aufruf, beide nutzen das Ergebnis" (die Namen in einem sechsten Lauf bestätigt). Mit Semaphor 5 von 5 Läufen 36 von 36 grün. Der Rückbau ist per `cmp` gegen die gesicherte Datei bestätigt.

**Zugangsdaten:** Der Test deckt jetzt alle vier Felder ab, dazu der Fall, dass leere Secrets im Formular nichts ändern. Gegenprobe: je ein Feld aus dem Vergleich in `src/Server/Api.fs` entfernt, jeweils genau 1 Fall rot.

**Rot vor dem Fix:** Die beiden Kaputt-Fälle aus `src/Tests/BankConnectionTests.fs` (`cancelSync`, `testComdirectConnection`) und der Fall mit nicht lesbaren Konten wurden gegen das alte Verhalten ausgeführt und waren rot (temporär zurückgebaut, die anderen 30 Fälle grün).

**Über den Auftrag hinaus:**
- `testComdirectConnection` liefert jetzt `ComdirectConnectionTest` (`TanChallengeStarted | BankConnectionActive`) statt der Challenge-Id. Der alte Vertrag hätte bei `Connected` den Wartezustand für die TAN gezeigt. Einstellungen und Test sind mitgezogen, Fixtures in `scripts/` gab es dafür keine. Der Rückgabe-Kommentar in `src/Shared/Api.fs` ist angepasst.
- `confirmTan` bei `Connected` geht nicht mehr zur Bank, ein Doppelklick zählt so nicht auf die Sperre ein.
- `getTransactionsWith` mit eingespeistem Client ist neu, `DateTime.Today` steht jetzt in `Api.fs` an der Grenze, das Verhalten ist gleich. `isAuthenticated`, `getTokens` und die übrigen ungenutzten Lesefunktionen sind entfernt.

**Bleibt offen:**
- Bis `banking-import-cyg94` schickt der Client nach jedem Sync-Start weiter `initiateComdirectAuth`. Ein Sync bei `Connected` ersetzt die Verbindung also durch eine neue Challenge, wie bisher. Der Nutzen beginnt erst mit diesem Task.
- Der Verbindungstest bei `Connected` zeigt live vermutlich einen Fehler: `getAccounts` lieferte laut Live-Befund in allen Runden 404. Die Verbindung bleibt dabei stehen. Siehe neuer Backlog-Eintrag.
- Der Verlust geht bis `banking-import-bftq0` nur ins Server-Log (`ReportLoss`), nach einem Neustart ist die Verbindung bis `banking-import-z4h6p` weg.
- Offene Frage an Roman aus den Notes: entschieden ist kein Höchstalter, nichts gebaut.
