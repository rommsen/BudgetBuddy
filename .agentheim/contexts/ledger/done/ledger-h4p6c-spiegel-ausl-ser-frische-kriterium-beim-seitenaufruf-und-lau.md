---
id: ledger-h4p6c
title: Spiegel-Auslöser: Frische-Kriterium beim Seitenaufruf und Lauf nach jedem Push
status: done
type: feature
context: ledger
created: 2026-09-06
completed: 2026-09-13
depends_on: [ledger-m8r2v, ledger-q5w9c]
blocks: []
tags: [backend, frontend, integration]
related_adrs: [0014, 0021]
related_research: []
prior_art: [ledger-m8r2v]
---

## Why
ADR 0014 nennt drei Auslöser für den Spiegel-Lauf. `ledger-m8r2v` hat nur den dritten
gebaut (manuell, "Jetzt spiegeln"); `ledger-kt0wq` hält ausdrücklich fest, dass das
Frische-Kriterium Folge-Task ist. Ohne die automatischen Auslöser muss Roman den Spiegel
von Hand anstoßen, damit das Kassenbuch stimmt — genau die Handarbeit, die BB abnehmen soll.

## What
1. **Frische-Kriterium:** beim Öffnen der Kassenbuch-Seite und beim Wechsel der PWA in den
   Vordergrund prüfen, ob der letzte Lauf älter als 5 Minuten ist (Zeitpunkt steht im
   Lauf-Status, `LedgerApi.getLastMirrorRun`). Wenn ja, den Lauf **im Hintergrund**
   anstoßen; die Seite zeigt sofort den vorhandenen Stand und aktualisiert danach.
2. **Nach jedem Push:** nach einem erfolgreichen Import nach YNAB einen Lauf anstoßen,
   damit die frisch gepushten Buchungen mit ihrer YNAB-Id im Kassenbuch landen.
3. **Kein Server-Timer** (ADR 0014 verwirft ihn ausdrücklich) und kein paralleler
   Doppel-Lauf: ein bereits laufender Spiegel darf nicht ein zweites Mal gestartet werden.
4. Frontend-Zustand über RemoteData; ein Hintergrund-Lauf blockiert die Seite nicht.

## Acceptance criteria
- [x] Seitenaufruf mit letztem Lauf älter als 5 Minuten löst genau einen Hintergrund-Lauf
      aus; jüngerer Lauf löst keinen aus (Test).
- [x] PWA-Vordergrund verhält sich wie der Seitenaufruf.
- [x] Nach erfolgreichem Push läuft der Spiegel genau einmal (Test).
- [x] Zwei gleichzeitige Auslöser ergeben nur einen Lauf (Test).
- [x] Die Seite zeigt während des Hintergrund-Laufs den vorhandenen Stand, nicht leer.
- [x] `dotnet build` und `dotnet test` grün.

## Notes
Der Lauf selbst existiert bereits (`Ledger.CommandHandlers.runMirror` / `mirrorYnabBudget`);
dieser Task baut nur die Auslöser und die Doppel-Lauf-Sperre.

Nachtrag 2026-09-11 (aus ADR 0021 und `ledger-tdnpk`): Nach einem Quick Add darf der
Spiegel-Lauf erst starten, wenn `EntryLinkedToYnab` geschrieben ist. Quick-Add-Buchungen
haben keine ImportId (ADR 0004); ein Lauf dazwischen sieht die YNAB-Buchung ungematcht
und legt eine zweite Buchung an. Gilt sinngemäß für den Import-Push (`ledger-qvt84`), dort
fängt die ImportId den Fall aber ab.

## Outcome

Alle drei Auslöser aus ADR 0014 stehen jetzt, mit serverseitigem Einzel-Flug-Schutz.

**Einzel-Flug-Schutz** (`src/Server/Ledger/CommandHandlers.fs`, `MirrorGuard`): ein
gerade laufender Spiegel-Lauf wird als `Task` festgehalten; ein zweiter, gleichzeitiger
Aufruf startet keinen zweiten YNAB-Abruf, sondern wartet auf denselben Task und teilt sein
Ergebnis (single flight). `runMirrorGuarded` wraps `runMirror`; `mirrorYnabBudget` (die
eine Stelle, durch die alle drei Auslöser laufen) ruft `runMirrorGuarded`. Das hält den
Lock über zwei Browser-Tabs und über ein Seiten-Öffnen, das mit einem Lauf-nach-Push
zusammentrifft, nicht nur der client-seitige `MirrorRunning`-Flag, der nur ein Tab kennt.
Die Freigabe von `inFlight` steht in `try/finally` (Verifier-Fund Iteration 1: sie lief
vorher nur beim normalen Abschluss, ein fehlgeschlagener oder abgebrochener Task hielt den
Schutz dauerhaft blockiert, bis zum Neustart).

**Frische-Kriterium** (`src/Client/Components/Ledger/Types.fs`: `isMirrorStale`,
`freshnessThreshold` = 5 Minuten, pure Funktion mit injiziertem `now`): die Kassenbuch-
Seite lädt beim Betreten ohnehin `LoadMirrorStatus`; `MirrorStatusLoaded (Ok status)`
(`Components.Ledger.State`) prüft jetzt zusätzlich `isMirrorStale`, und startet bei
Bedarf denselben Hintergrund-Lauf wie der manuelle Knopf (`StartMirror`/`RunMirror`
wiederverwendet, kein neuer Code-Pfad). Läuft schon ein Lauf (`MirrorRunning`), tut ein
später eintreffender Status nichts: das ist die client-seitige Hälfte der
Doppel-Lauf-Sperre.

**PWA-Vordergrund** (`src/Client/App.fs`: `visibilitySubscription`, Elmish-Subscription
auf `document`s `visibilitychange`, dynamischer Zugriff auf `visibilityState` wie
`Viewport.fs` es für `visualViewport` schon macht) dispatcht eine neue Root-Nachricht
`State.VisibilityChanged`; die tut auf der Kassenbuch-Seite dasselbe wie ein Seitenaufruf
(`LoadMirrorStatus` neu laden, dessen Frische-Prüfung entscheidet), auf jeder anderen
Seite nichts.

**Lauf nach Push** (`Ledger.CommandHandlers.triggerMirrorInBackground`, fire-and-forget
über `Async.Start`): `addCashExpense` und `pushComdirectImports` bekommen ein neues,
injiziertes `triggerMirror: unit -> unit`-Argument (Muster wie `push`/`MirrorFetchers`,
ohne Netz und ohne echten Spiegel-Lauf testbar) und rufen es genau dann auf, wenn
`EntryLinkedToYnab` für den erfolgreichen Versuch schon geschrieben ist (Task-Nachtrag,
ADR 0021): bei Quick Add nach der Push-Antwort, beim Comdirect-Import erst nachdem alle
`EntryLinkedToYnab`-Verknüpfungen eines Sende-Versuchs stehen, und nur wenn der
Sende-Versuch tatsächlich `Ok` war (kein Auslöser bei einem leeren Versuch oder einem
Fehler). `Server.Api` verdrahtet beide Aufrufer mit `triggerMirrorInBackground`. Der
eigentliche Async.Start-Aufruf steckt jetzt in `runMirrorInBackground` (`run` injiziert,
testbar ohne `Async.Start`): fängt jede Ausnahme aus `run` ab, einschließlich einer, die
schon vor `runMirror`s eigenem try geworfen wird oder aus einem fehlgeschlagenen Task im
Einzel-Flug-Schutz kommt (Verifier-Fund Iteration 1: `Async.Start` ließ Ausnahmen sonst
unbehandelt auf dem Thread-Pool landen und hätte den Prozess mitreißen können), loggt sie
und speichert best effort einen fehlgeschlagenen Lauf-Status (ADR 0014: Fehler sichtbar,
nicht verschluckt).

**Tests:** 841 grün, 0 Fehler, 7 übersprungen (`.env`-Integrationstests), 22 neu über
beide Iterationen. Server: `MirrorGuard` single-flight (zwei gleichzeitige Läufe teilen
sich einen YNAB-Abruf via künstlicher Verzögerung im Fetcher, zwei nacheinander
abgeschlossene Läufe nicht, ein fehlgeschlagener Lauf gibt den Schutz wieder frei),
`runMirrorInBackground` (eine Ausnahme propagiert nicht und landet als fehlgeschlagener
Lauf-Status, ein Erfolg propagiert unauffällig) und je zwei Tests für `triggerMirror` bei
`addCashExpense`/`pushComdirectImports` (genau einmal bei Erfolg, nie bei Fehler, nie bei
einem Versuch ohne neuen Versand), dazu je ein Test, der im Trigger-Callback selbst
nachschaut, dass `EntryLinkedToYnab` schon im Log steht (Task-Nachtrag, ADR 0021). Client:
`isMirrorStale` pur (genau an der Schwelle noch frisch, eine Sekunde drüber nicht mehr,
ein gescheiterter Lauf zählt trotzdem als Zeitpunkt, kein Lauf ist sofort "nicht frisch"),
der Reduzierer-Übergang `MirrorStatusLoaded` (alt löst aus, jung nicht, noch nie ein Lauf
löst aus, ein schon laufender Spiegel wird nicht zweimal gestartet, die Liste bleibt
während des Hintergrund-Laufs stehen) und `VisibilityChanged` auf/außerhalb der
Kassenbuch-Seite. `dotnet build`, `dotnet test` und `npm run build` grün.

**Nicht angefasst:** keine neue ADR, die Entscheidungen (drei Auslöser, kein
Server-Timer, Einzel-Flug statt DB-Lock für den Single-Process-Deploy) stehen schon in
ADR 0014; der In-Prozess-Lock ist für diese Deployment-Form (ein Docker-Container, kein
horizontal skalierter Server) die naheliegende Wahl, keine, die eine eigene Abwägung
bräuchte.

## Diary

## 2026-09-13 - Spiegel-Auslöser: Frische-Kriterium und Lauf nach Push

### What
Alle drei Auslöser aus ADR 0014 implementiert (manuell bestand schon): Frische-Kriterium
beim Seitenaufruf und PWA-Vordergrund, Lauf nach erfolgreichem Push, dazu ein
serverseitiger Einzel-Flug-Schutz gegen doppelte, gleichzeitige Läufe.

### Files Changed
- `src/Server/Ledger/CommandHandlers.fs` - `MirrorGuard` (Einzel-Flug-Schutz),
  `runMirrorGuarded`, `triggerMirrorInBackground`; `addCashExpense` und
  `pushComdirectImports` bekommen ein `triggerMirror`-Argument
- `src/Server/Api.fs` - beide Push-Aufrufer verdrahtet mit `triggerMirrorInBackground`
- `src/Client/Components/Ledger/Types.fs` - `isMirrorStale`, `freshnessThreshold`
- `src/Client/Components/Ledger/State.fs` - `MirrorStatusLoaded` prüft Frische
- `src/Client/State.fs` - neue Nachricht `VisibilityChanged`
- `src/Client/App.fs` - Elmish-Subscription auf `visibilitychange`
- `src/Tests/LedgerTests.fs`, `src/Tests/LedgerMirrorTests.fs`,
  `src/Tests/LedgerPageTests.fs` - 17 neue Tests, bestehende Aufrufe auf die neuen
  Signaturen angepasst
- `.agentheim/contexts/ledger/README.md` - Spiegel-Lauf-Abschnitt um die drei Auslöser
  und den Einzel-Flug-Schutz ergänzt

### Rationale
ADR 0014 hatte den automatischen Auslöser als Folge-Task benannt; ohne ihn musste Roman
den Spiegel von Hand anstoßen. Der Einzel-Flug-Schutz war laut Task-Nachtrag zwingend
serverseitig nötig, nicht nur im Client-Zustand (zwei Tabs, oder Seiten-Öffnen zeitgleich
mit Lauf-nach-Push).

### Verification
- Build: PASSED (`dotnet build`, `npm run build`)
- Tests: 836 passed, 7 skipped, 0 failed (17 neu laut Diff, ursprünglich als 24 gemeldet,
  vom Verifier korrigiert)

## 2026-09-13 - Iteration 2: Guard-Freigabe, Hintergrund-Fehler, Reihenfolge-Test, Schreibregeln

### What
Verifier-Funde aus Iteration 1 behoben: `MirrorGuard.runGuarded` räumt `inFlight` jetzt in
`try/finally` ab, damit ein fehlgeschlagener oder abgebrochener Lauf den Einzel-Flug-Schutz
nicht dauerhaft blockiert. `triggerMirrorInBackground`s `Async.Start`-Aufruf steckt jetzt
in `runMirrorInBackground` (injizierter `run`, testbar), fängt jede Ausnahme ab, loggt sie
und speichert best effort einen fehlgeschlagenen Lauf-Status. Zwei neue Tests belegen die
Reihenfolge aus dem Task-Nachtrag (ADR 0021): `EntryLinkedToYnab` steht schon im Log, wenn
`triggerMirror` feuert, für `addCashExpense` und `pushComdirectImports`. Dazu alle
Gedankenstriche und ae/oe/ue-Umschreibungen aus den in Iteration 1 hinzugefügten Zeilen
entfernt (echte Umlaute, Punkt/Komma/Doppelpunkt/Klammern statt Gedankenstrich), den
Tippfehler "druebrer" korrigiert und die Testanzahl in Outcome und Diary an den Diff
angepasst.

### Files Changed
- `src/Server/Ledger/CommandHandlers.fs` - `MirrorGuard.runGuarded` mit `try/finally`,
  `MirrorGuard` von `private` auf `internal` (Testzugriff über `InternalsVisibleTo`),
  neue Funktion `runMirrorInBackground` (Fehlerbehandlung, von `triggerMirrorInBackground`
  genutzt), Schreibregeln in Kommentaren korrigiert
- `src/Tests/LedgerMirrorTests.fs` - Test "ein fehlgeschlagener Lauf gibt den
  Einzel-Flug-Schutz wieder frei", neue Testliste `runMirrorInBackgroundTests`
  (Ausnahme propagiert nicht/landet im Lauf-Status, Erfolg propagiert unauffällig),
  Schreibregeln korrigiert
- `src/Tests/LedgerTests.fs` - je ein Reihenfolge-Test für `addCashExpense` und
  `pushComdirectImports` (`EntryLinkedToYnab` schon im Log, wenn der Trigger feuert),
  Schreibregeln korrigiert
- `src/Tests/LedgerPageTests.fs` - Schreibregeln korrigiert (Testnamen, Assertion-Texte,
  Tippfehler "druebrer")
- `src/Client/App.fs`, `src/Client/Components/Ledger/State.fs`,
  `src/Client/Components/Ledger/Types.fs`, `src/Client/State.fs` - Schreibregeln in
  Kommentaren korrigiert
- `.agentheim/contexts/ledger/README.md` - Gedankenstriche in den Iteration-1-Zeilen
  entfernt
- diese Task-Datei - Outcome und Diary an den Diff angepasst, Verifier-Note stehen
  gelassen

### Rationale
Der Verifier hat einen echten Nebenläufigkeits-Fehler gefunden (Guard blieb nach einem
Fehler dauerhaft gesperrt) und eine reale Prozess-Absturzgefahr (`Async.Start` ohne
Fehlerbehandlung), dazu wiederholte Verstöße gegen die globale Schreibregel (keine echten
Umlaute, Gedankenstriche). Beides musste vor einem erneuten Verify-Durchlauf behoben sein.

### Verification
- Build: PASSED (`dotnet build`, `npm run build`)
- Tests: 841 passed, 7 skipped, 0 failed (22 neu über beide Iterationen laut Diff)

## Verifier note (iteration 1)

**REASONS:**
- **Prüfung 6b (ADR 0014: nach einem abgebrochenen Lauf versucht es der nächste Auslöser erneut): der Einzel-Flug-Schutz kann dauerhaft hängen bleiben.** In `MirrorGuard.runGuarded` (`src/Server/Ledger/CommandHandlers.fs`, ca. Z. 270) läuft `inFlight <- None` nur, wenn `Async.AwaitTask task` normal zurückkehrt, nicht in try/finally. Schlägt der Lauf-Task fehl, bleibt der fehlerhafte Task in `inFlight`, und jeder spätere Aufrufer bekommt ihn zurück: kein Spiegel-Lauf mehr bis zum Neustart. Das `with ex -> finish (...)` in `runMirror` schließt das nicht aus, weil `finish` `saveLastMirrorRun` (DB-I/O) im Handler aufruft und selbst werfen kann. Kein Test deckt "ein fehlgeschlagener Lauf gibt den Schutz frei" ab.
- Verwandt: `triggerMirrorInBackground` nutzt `Async.Start` ohne eigene Fehlerbehandlung. Eine Ausnahme aus `mirrorYnabBudget` (z. B. `Core.Settings.getSetting` außerhalb des try von `runMirror`, oder der Fall oben) geht unbehandelt auf den Thread-Pool und kann den Server-Prozess direkt nach einem erfolgreichen Push beenden.
- Schreibregeln auf hinzugefügten Zeilen verletzt: viele Gedankenstriche (README "…das Ergebnis teilen — kein Server-Timer"; Task-Datei `## Outcome` mehrfach; `src/Client/App.fs:79`; `Ledger/State.fs` Kommentar zu MirrorStatusLoaded; `Client/State.fs` Kommentar zu VisibilityChanged; `CommandHandlers.fs` MirrorGuard-Kommentar und addCashExpense-Doc; `LedgerMirrorTests.fs`; `LedgerTests.fs`). Umschreibungen statt Umlaute in Kommentaren, Doc-Kommentaren, Testnamen und Assertion-Texten: "Ausloeser", "laeuft", "dafuer", "ueber", "aelter", "stoesst", "zusaetzlich", "haelt", "muessen", "Verknuepfung", "tatsaechlich", "aussen", "gehoert", "geoeffnet", "noetig", "Pruefung". Betroffene Testnamen u. a. "Ein zu alter Lauf-Status loest genau einen Hintergrund-Lauf aus", "Ein juengerer Lauf-Status loest keinen …", "Eine Sekunde ueber der Schwelle …", "zwei gleichzeitige Ausloeser teilen sich einen Lauf …", "zwei nacheinander abgeschlossene Laeufe teilen sich nichts", "ein erfolgreicher Push stoesst den Spiegel genau einmal an", "Auf der Kassenbuch-Seite laedt der Vordergrund-Wechsel …". Tippfehler "druebrer" in einer LedgerPageTests-Assertion. Payee-Strings "Spiegel-Ausloeser Erfolg/Fehlschlag".
- Klein: TESTS_ADDED 24 gemeldet, der Diff enthält 17 neue Testfälle (2 MirrorGuard, 4 Reducer-Frische, 4 isMirrorStale, 2 VisibilityChanged, 2 addCashExpense-Trigger, 3 pushComdirectImports-Trigger). Outcome und Diary sollen zum Diff passen.
- Nicht blockierend, aber sinnvoll: kein Test legt fest, dass der Quick-Add-`triggerMirror` erst nach geschriebenem `EntryLinkedToYnab` feuert (Task-Nachtrag, ADR 0021); die Tests zählen nur Aufrufe. Ein Test, der im Trigger-Callback das Log prüft, würde die Reihenfolge festnageln.
- Geprüft und in Ordnung: Build, `dotnet test` (836 bestanden, 0 fehlgeschlagen, 7 übersprungen, alle `.env`-Integrationstests), `npm run build` grün; jedes Akzeptanzkriterium auf einen Test abgebildet; Scope sauber; README synchron; kein Timer, keine Index- oder Protokoll-Eingriffe.

**SUGGESTED_FIX:** Die Freigabe in `MirrorGuard.runGuarded` in try/finally legen, damit der Schutz auch nach einem fehlerhaften Lauf frei wird, plus Test: Lauf wirft, ein späterer `runMirrorGuarded`-Aufruf läuft wieder. Ausnahmen in `triggerMirrorInBackground` abfangen. Alle Gedankenstriche und ae/oe/ue/ss-Umschreibungen in den hinzugefügten Zeilen (Kommentare, Testnamen, Assertion-Texte, Payee-Strings, README, Outcome und Diary) durch echte Umlaute und normale Satzzeichen ersetzen; Testanzahl in Outcome und Diary korrigieren.

**ITERATION_HINT:** likely-fixable
