---
paths:
  - "src/Tests/**"
  - "scripts/*.test.mjs"
  - "src/Server/**"
  - "src/Client/**"
  - "src/Shared/**"
---

# Test-Doktrin

Gate: `./verify.sh` (dotnet build, Expecto über die Tests.dll, npm run build, guard:classes, guard:tokens, test:scripts). Fertig ist, was dort `VERIFY: PASS` zeigt. Übersprungene Pflichttests zählen als rot; erlaubt sind nur die Listen "(requires .env)".

Mechanisch geprüft von: `src/Tests/RemotingPipelineTests.fs`, `src/Tests/RemotingEndpointNegativeTests.fs`, `src/Tests/NetworkCallGuardTests.fs`, `src/Tests/TestDatabaseGuardTests.fs`, `src/Tests/ModuleLevelDbInitGuardTests.fs`, `src/Tests/StructureGuardTests.fs`, `scripts/claude-harness-check.test.mjs`, `scripts/browser-test-budget.test.mjs` mit `scripts/run-guards.mjs`.

## Heimat-Ebene

1. Ein Test prüft beobachtbares Verhalten auf der billigsten Ebene, auf der ein Fehler im echten Code ihn rot macht. Ebenen, billig nach teuer: Domain, Decoder und Encoder pur; Validation; Befehls- und Abfrage-Handler mit In-Memory-SQLite; API-Adapter (Record-Feld aus `src/Server/Api.fs` direkt aufrufen); Client-`update` unter .NET; Browser-Wächter in `scripts/*.test.mjs`. Client-`update` unter .NET nur für Nachrichten, deren Cmd nicht `Client.Api` berührt: der Fable.Remoting-Proxy wirft unter .NET im Typinitialisierer. Mapping-Logik dafür in reine Funktionen ziehen (Vorbild `buildQuickAddRequest`), Cmds mit `Client.Api` hinter ein Lambda legen. Sichtbarkeits- und Sperrlogik aus dem View in eine reine Funktion ziehen, die View ruft sie nur auf (Vorbild `assignKeyAction`, `amountKeyAction` in `Components/Budget/Moves.fs`). Geht ein Verhalten unter .NET prüfbar, ist ein Browser-Test dafür verboten.
2. Kein zweiter Test auf einer teureren Ebene für dasselbe Verhalten. Eine zweite Ebene nur mit eigener, benannter Aussage.
3. Handler-Tests nach dem Kassenbuch-Muster: Ports als Funktionswerte (Vorbild `MemoProbe` in `src/Tests/LedgerMemoTests.fs`), Uhr als Parameter, Schema-Initialisierung im Testfall, eindeutige Ids (`freshAccount`). Geprüft werden Ereignisse, gelesener Zustand und ausgehende Aufrufe. Wer Logik hinter einem `*With` mit Ports zieht, listet im selben Schritt jede Stelle auf, die den Live-Port anschließt (`live*`, Einzeiler im Endpoint, `Program.fs`), und belegt jede per Verhalten oder Strukturwächter mit Gegenbeweis. Ein Satz "kein Überlebender" nennt immer die Mutantenliste und gilt nur für sie.
4. Eine neue Funktion nach dem Vorbild einer bestehenden bekommt Tests für dieselben Verhaltensfälle wie die Vorlage (jede Ablehnungsregel, jede Grenze), keine Feldkopie-Tests.
5. Browser-Wächter nur für eine Aussage, die nur der Browser treffen kann. Welche das ist, sagt eine Kennung am Anfang des Namens jedes Blatt-Tests in einer Datei mit Browser oder gestartetem Frontend, auch bei Untertests und in Schleifen erzeugten Tests; nach der Kennung folgt der Name als deutscher Satz (Punkt 19):

   | Kennung | Aussage |
   |---|---|
   | `[layout]` | Layout-Invarianten: seitliches Scrollen, Abschneiden, Verdecken, sticky, Viewport, Trefferflächen |
   | `[fokus]` | Fokus im echten DOM |
   | `[tastatur]` | Tastatur-Ereignisse im echten DOM |
   | `[scroll]` | Scrollen und Scrollposition |
   | `[fable]` | gilt nur im Fable-Kompilat (Dezimalrechnung, Datumsformate, Netzfehler des Remoting-Proxys) |
   | `[flow:<name>]` | der eine Happy Path eines Nutzerflusses |

   Quelle der Kennungen und der Flow-Liste ist `scripts/browser-usage.mjs` (`TAGS`, `FLOWS`, je Flow ein Name und ein Satz). Ein neuer Nutzerfluss bekommt eine Zeile in `FLOWS` und genau einen `[flow:…]`-Test, dazu nur Tests mit den anderen Kennungen. Je Flow höchstens ein Test. Passt ein Test zu keiner Kennung, prüft er Logik: welcher Text, welche Zahl, welcher Zustand nach einem Klick erscheint, gehört in Client-`update` oder eine reine Funktion. Eine feste Obergrenze gibt es nicht. `npm run test:scripts` wird rot bei fehlender oder unbekannter Kennung, bei einem Flow außerhalb von `FLOWS` und bei zwei Tests für denselben Flow, und meldet die Zahl je Kennung samt Summe ("Browser-Tests je Kennung: …"), damit Wachstum sichtbar bleibt (Prüfung in `scripts/run-guards.mjs`, Gegenbeweise in `scripts/browser-test-budget.test.mjs`). Geprüft wird zur Laufzeit über den Hook in `NODE_OPTIONS` und `BB_BROWSER_USAGE_OWNER`: ein Kindprozess, der beide nicht erbt (etwa `env: {}` beim Start), startet einen Browser ungeprüft und ist deshalb verboten. Ebenso ungeprüft bleiben die synchronen Starts (`spawnSync`, `execSync`, `execFileSync`); einen Browser darüber zu starten ist verboten. Ein Zeitgeber-Cmd (`Debounce.delayed`, Rückfall, Takt) bekommt einen .NET-Test, der ihn ablaufen lässt (Vorbild `src/Tests/CmdTiming.fs`). Die API wird per `page.route` gemockt. Auf Bedingungen warten, kein `waitForTimeout`. "Heute" als Berliner Kalendertag, nicht `new Date().toISOString()`. Bei Fehlern zuerst die Netzwerk-Antworten ansehen.

   Layout prüft ein Sweep: `scripts/layout-invariants.test.mjs` besucht die Zustände aus `scripts/layout-states.mjs` (Routen, offene Sheets, Popover und Dialoge) bei mehreren Breiten und prüft für jeden dieselben Invarianten aus `scripts/layout-invariants.mjs` (kein seitliches Scrollen, Trefferflächen 48 px und Feldschrift 16 px am Handy, kein Text abgeschnitten außer mit Ellipse und `title`, nichts verdeckt, Sticky-Köpfe überlappen nicht, Primärknopf ohne Scrollen erreichbar auch bei offener Tastatur, Spaltenköpfe überlappen nicht, kein Sprung beim Nachladen). Ein neuer Zustand oder eine neue Invariante dort statt eines eigenen `[layout]`-Tests je Komponente. Feste Designwerte (Größen, Abstände, Farben, Reihenfolge, Animation) werden nicht im Browser getestet: sie stehen im Styleguide, die Tokens prüfen `guard:tokens` und `guard:classes`. Als Schwellen gelten nur die des Styleguides. Eine Ausnahme nennt Zustand, Breite, Invariante und Selektor (`exceptions` im Zustand), nie eine ganze Breite, und steht in der Kann-Sammlung des BC.

## Kein Test für

6. Konstanten, Feldkopien, gerade gebaute Records, Framework- oder Sprachverhalten, Tailwind-Größen als Klassenstring (Größe und Trefferfläche misst der Browser-Wächter, Punkt 5). Gegenprobe: bleibt der Test grün, wenn die getestete Zeile im Produktionscode gelöscht oder vertauscht wird, ist er wertlos. Keine "documents"-Tests; Doku gehört in Kommentare.
7. Strukturtests (Quelltext lesen) nur für repo-weite Invarianten ohne Verhaltenszugang (Netzziele, Middleware, Startcode, DB-Pfad, verbotene Klassen), immer mit eingebautem Gegenbeweis. Keine neue View-Verdrahtung per Teilstring, keine Rückbau-Wächter. Einen Wächter nicht nur für eine gerade gefixte Stelle anlegen; der Regressionstest gehört dann auf die Verhaltensebene.

## Bugfixes

8. Jeder Bugfix bekommt einen Regressionstest, der vor dem Fix rot ist. Der Test nennt im Kommentar, welchen Bug er verhindert. Ein Layoutbug bekommt seinen Regressionstest als neuen Zustand (mit Kommentar zum Bug) oder als neue Invariante im Sweep (Punkt 5), nicht als eigenen Test je Komponente; eine neue Invariante bringt einen Gegenbeweis mit absichtlich kaputter Seite mit.
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

## Zustandsregeln

20. Regeln über mehrere Nachrichten (Sperren während eines Aufrufs, Schlangen, Speichern beim Verlassen) werden als Aufzählung aller Reihenfolgen durch das echte `update` getestet, mit einem Orakel, das nicht vom getesteten Code abhängt, und nicht Weg für Weg. Vorbild: `moveWays` in `src/Tests/MoveMoneyClientTests.fs`.
