---
paths:
  - "src/Tests/**"
  - "scripts/*.test.mjs"
  - "src/Server/**"
  - "src/Client/**"
  - "src/Shared/**"
---

# Test-Doktrin

Gate: `./verify.sh` (dotnet build, dotnet test, npm run build, guard:classes, guard:tokens, test:scripts). Fertig ist, was dort `VERIFY: PASS` zeigt. Übersprungene Pflichttests zählen als rot; erlaubt sind nur die Listen "(requires .env)".

Mechanisch geprüft von: `src/Tests/RemotingPipelineTests.fs`, `src/Tests/RemotingEndpointNegativeTests.fs`, `src/Tests/NetworkCallGuardTests.fs`, `src/Tests/TestDatabaseGuardTests.fs`, `src/Tests/ModuleLevelDbInitGuardTests.fs`, `src/Tests/StructureGuardTests.fs`, `scripts/claude-harness-check.test.mjs`.

## Heimat-Ebene

1. Ein Test prüft beobachtbares Verhalten auf der billigsten Ebene, auf der ein Fehler im echten Code ihn rot macht. Ebenen, billig nach teuer: Domain, Decoder und Encoder pur; Validation; Befehls- und Abfrage-Handler mit In-Memory-SQLite; API-Adapter (Record-Feld aus `src/Server/Api.fs` direkt aufrufen); Client-`update` unter .NET; Browser-Wächter in `scripts/*.test.mjs`. Client-`update` unter .NET nur für Nachrichten, deren Cmd nicht `Client.Api` berührt: der Fable.Remoting-Proxy wirft unter .NET im Typinitialisierer. Mapping-Logik dafür in reine Funktionen ziehen (Vorbild `buildQuickAddRequest`).
2. Kein zweiter Test auf einer teureren Ebene für dasselbe Verhalten. Eine zweite Ebene nur mit eigener, benannter Aussage.
3. Handler-Tests nach dem Kassenbuch-Muster: Ports als Funktionswerte (Vorbild `MemoProbe` in `src/Tests/LedgerMemoTests.fs`), Uhr als Parameter, Schema-Initialisierung im Testfall, eindeutige Ids (`freshAccount`). Geprüft werden Ereignisse, gelesener Zustand und ausgehende Aufrufe.
4. Eine neue Funktion nach dem Vorbild einer bestehenden bekommt Tests für dieselben Verhaltensfälle wie die Vorlage (jede Ablehnungsregel, jede Grenze), keine Feldkopie-Tests.
5. Browser-Wächter: ein Happy Path je Nutzerfluss und was nur der Browser messen kann (Layout, Fokus, Viewport, Trefferflächen). Die API wird per `page.route` gemockt. Auf Bedingungen warten, kein `waitForTimeout`. "Heute" als Berliner Kalendertag, nicht `new Date().toISOString()`. Bei Fehlern zuerst die Netzwerk-Antworten ansehen.

## Kein Test für

6. Konstanten, Feldkopien, gerade gebaute Records, Framework- oder Sprachverhalten, Tailwind-Größen als Klassenstring (Größe und Trefferfläche misst der Browser-Wächter, Punkt 5). Gegenprobe: bleibt der Test grün, wenn die getestete Zeile im Produktionscode gelöscht oder vertauscht wird, ist er wertlos. Keine "documents"-Tests; Doku gehört in Kommentare.
7. Strukturtests (Quelltext lesen) nur für repo-weite Invarianten ohne Verhaltenszugang (Netzziele, Middleware, Startcode, DB-Pfad, verbotene Klassen), immer mit eingebautem Gegenbeweis. Keine neue View-Verdrahtung per Teilstring, keine Rückbau-Wächter. Einen Wächter nicht nur für eine gerade gefixte Stelle anlegen; der Regressionstest gehört dann auf die Verhaltensebene.

## Bugfixes

8. Jeder Bugfix bekommt einen Regressionstest, der vor dem Fix rot ist. Der Test nennt im Kommentar, welchen Bug er verhindert.
9. Bug mit fachlichem Verhalten: vorab mindestens ein kaputt-Szenario (soll nach dem Fix gehen) und ein intakt-Szenario (muss weiter gehen), als GIVEN/WHEN/THEN. Ablauf: kaputt-Test schreiben und rot sehen, Ursache klären, fixen, beide grün, `./verify.sh`.

## BudgetBuddy-Pflichtliste

Diese Tests müssen bestehen und werden bei Änderungen am Bereich mitgezogen:

10. **Pipeline:** `src/Tests/RemotingPipelineTests.fs` schickt je Remoting-Interface eine Anfrage durch den echten Host (`Server.Api.webApp()` im TestServer). Ein neues Interface braucht dort einen Aufruf. Ein Handler im `choose`, der weitergeben will, gibt `skipPipeline` zurück, nie `next ctx` (Bug 2a5c229).
11. **Endpoint-Negativtabelle:** `src/Tests/RemotingEndpointNegativeTests.fs` hat je Remoting-Endpoint eine Zeile (ungültige Eingabe, unbekannte Id, fehlende Einrichtung). Ein neuer Endpoint braucht eine Zeile, sonst ist der Abgleich rot.
12. **Live nur hinter Schalter:** Aufrufe gegen YNAB oder Comdirect stehen nur in Testfällen, die `RUN_INTEGRATION_TESTS` prüfen (`shouldRunIntegrationTests`), in Listen "(requires .env)". Nie mit echten Schlüsseln und falschem Passwort anmelden. `src/Tests/NetworkCallGuardTests.fs` meldet Verstöße.
13. **Bot-Grenzen:** `/bot/` verlangt das Bearer-Token, ist ohne Token abgeschaltet und hält Anfrage-, Betrags- und Tagesgrenzen (`src/Tests/BotApiTests.fs`, `src/Tests/BotValidationTests.fs`). Eine neue Bot-Route bekommt einen 401-Test und, wenn sie bucht, einen Grenztest.
14. **Keine Secrets an den Client:** YNAB-Token und Comdirect-Zugangsdaten verlassen den Server nicht, auch nicht in Fehlermeldungen. Ein Endpoint, der Einstellungen liefert, bekommt einen Test, der das prüft. Vorbild: `src/Tests/SettingsSecretsTests.fs`.

## Testdatenbank und Umgebung

15. Tests laufen nie gegen die echte Datenbank. `src/Tests/test.runsettings` setzt `USE_MEMORY_DB=true` und `TZ=Europe/Berlin`, `src/Tests/Main.fs` setzt `USE_MEMORY_DB` zusätzlich für den Direktaufruf; `src/Tests/TestDatabaseGuardTests.fs` stoppt einen Lauf auf einer Datei-DB. Einzeltests mit eigener Verbindung nutzen `Data Source=:memory:`.
16. Die In-Memory-DB ist für alle Testlisten geteilt und Expecto läuft parallel. Isolation kommt aus eindeutigen Ids; Reihenfolge innerhalb einer Liste nur mit `testSequenced`.
17. Uhr, Zeitzone und Zufall werden injiziert. Kein `DateTime.Now` im getesteten Code, wo eine Uhr übergeben werden kann.

## Property-Tests

18. Keine Pflicht. FsCheck über Expectos `testProperty` nur bei einem benannten Invariant, etwa Rundläufe und Grenzen (Vorbild `src/Tests/YnabClientTests.fs`: "milliunits conversion roundtrip", "memo with reference respects 300 char limit").

## Stil

19. Testnamen auf Deutsch mit echten Umlauten, als fachlicher Satz. Task-Ids in Kommentare, nicht in den Namen. Eine Datei, ein Aufbau (`testCase` oder `test`-CE).
