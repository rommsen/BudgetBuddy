---
id: infrastructure-d1dwb
title: "Gate startet Expecto direkt über die Tests.dll statt über dotnet test: 14 statt 168 Sekunden"
status: done
type: chore
context: infrastructure
created: 2026-11-05
completed:
depends_on: []
blocks: [infrastructure-pgpb6]
tags: [verify, gate, laufzeit, expecto]
related_adrs: ["0030", "0081"]
related_research: []
prior_art: [infrastructure-dvy29, infrastructure-eq27s]
---

## Why
`./verify.sh` braucht 6:44 Minuten (Messung 2026-11-05 auf `main`, alles grün). Schritt 2 `dotnet test` kostet davon 168 s. Expecto selbst meldet für alle 3818 Tests 13 s, ein Direktaufruf `dotnet src/Tests/bin/Debug/net10.0/Tests.dll` braucht 14 s. Die übrigen rund 155 s gehen in den VSTest-Adapter (`YoloDev.Expecto.TestSdk`: Discovery und Melden jedes einzelnen Ergebnisses) und den trx-Logger. Jeder Worker- und Verifier-Lauf zahlt das, bei mehreren Tickets mehrfach.

## What
Schritt 2 in `verify.sh` ruft Expecto direkt auf, nachdem Schritt 1 (`dotnet build BudgetBuddy.sln`) die `Tests.dll` gebaut hat:

```
USE_MEMORY_DB=true TZ=Europe/Berlin dotnet src/Tests/bin/Debug/net10.0/Tests.dll \
  --junit-summary "$LOG_DIR/expecto.xml" --fail-on-focused-tests
```

- **Auswertung über JUnit statt trx.** Expecto 10.2.1 schreibt mit `--junit-summary` einen JUnit-Bericht. `scripts/verify-lib.sh` bekommt Leser dafür (Zähler Total, Passed, Failed, Skipped; Namen der roten und der übersprungenen Tests; erste Zeile der Fehlermeldung je rotem Test). Die trx-Leser (`vl_trx_tests`, `vl_trx_first_message`) und `vl_dotnet_count` entfallen samt ihren Tests in `scripts/verify-lib.test.mjs`, wenn sie keinen Aufrufer mehr haben. `vl_is_optional_skip` bleibt unverändert: nur `(requires .env)`-Tests dürfen übersprungen sein.
- **Gleiche Gate-Semantik wie heute.** Rot bei rotem Test, bei Exit-Code ungleich 0, bei unlesbarem Bericht, bei 0 Tests und bei übersprungenem Pflichttest. Die Ausgabezeile `Total …, Passed …, Failed …, Skipped …` und die Liste der roten Tests am Ende bleiben in Form und Inhalt gleich.
- **DB- und Zeitzonen-Schutz am neuen Aufruf.** Unter dem Direktaufruf greift `test.runsettings` nicht. `main` in `src/Tests/Main.fs` setzt `USE_MEMORY_DB` selbst, `TestDatabaseGuardTests` prüft in-process gegen `:memory:` (ADR 0030). `verify.sh` setzt zusätzlich `USE_MEMORY_DB=true` und `TZ=Europe/Berlin` im Aufruf; ohne `TZ` wäre der Kalendertag-Regressionstest aus ynab-r9k4t blind (Begründung in `test.runsettings`).
- **Fokussierte Tests machen das Gate rot.** `--fail-on-focused-tests`: ein vergessenes `ftestCase` würde sonst still alle anderen Tests abschalten.
- `dotnet test` und `test.runsettings` bleiben für IDE und Einzelaufrufe bestehen.

## Acceptance criteria
- [ ] `verify.sh` Schritt 2 startet die `Tests.dll` direkt mit `--junit-summary`, kein `dotnet test` mehr im Gate.
- [ ] Neue Leser in `scripts/verify-lib.sh` sind in `scripts/verify-lib.test.mjs` gegen eine JUnit-Beispieldatei getestet: Zähler, Namen roter Tests (XML-Entities aufgelöst), Namen übersprungener Tests, erste Fehlerzeile je rotem Test.
- [ ] GIVEN ein absichtlich roter Expecto-Test WHEN `./verify.sh` läuft THEN `VERIFY: FAIL`, der Testname steht unter "Rote Tests", Exit 1. (Einmal von Hand nachgestellt, im Outcome notiert, der Test wird danach entfernt.)
- [ ] GIVEN ein `ftestCase` im Testprojekt WHEN `./verify.sh` läuft THEN Schritt 2 ist rot. (Von Hand nachgestellt, im Outcome notiert.)
- [ ] GIVEN ein übersprungener Test ohne `(requires .env)` im Namen WHEN `./verify.sh` läuft THEN Schritt 2 ist rot mit "Pflichttests übersprungen". Abgedeckt über die Leser-Tests plus die bestehende Logik in `verify.sh`.
- [ ] Ein Wächtertest (in `scripts/verify-lib.test.mjs` oder einer eigenen Datei unter `scripts/`) liest `verify.sh` und schlägt fehl, wenn der Expecto-Aufruf `USE_MEMORY_DB=true` oder `TZ=Europe/Berlin` nicht mehr setzt.
- [ ] `TestDatabaseGuardTests` läuft im neuen Aufruf mit und ist grün.
- [ ] Schritt 2 dauert auf Romans Rechner unter 30 s; vorher und nachher gemessen (Zeit aus der `verify.sh`-Ausgabe), Werte im Outcome.
- [ ] Kopfkommentar von `verify.sh` und der Abschnitt "Gate" in `CLAUDE.md` nennen den neuen Schritt ("Expecto über die Tests.dll" statt "dotnet test").
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Refine 2026-11-05, Messung in derselben Sitzung:

| Aufruf | Dauer |
|---|---|
| `dotnet test src/Tests/Tests.fsproj` (Gate heute, mit trx) | 168 s |
| `dotnet test --no-build` mit trx | 170 s |
| `dotnet Tests.dll --summary` | 14 s |

Beide Schalter geprüft (`Tests.dll --help`, Probelauf 14 s, Exit 0). Form des JUnit-Berichts aus dem Probelauf:
- eine einzige Zeile XML mit BOM, `<testsuites><testsuite name="Tests">`, ohne Zähl-Attribute (`tests`, `failures`, `skipped` fehlen). Zählen heißt also `<testcase`-Elemente zählen und die mit `<skipped` bzw. `<failure` darin.
- Testnamen in eckigen Klammern, Ebenen mit `; ` getrennt: `[YNAB Integration Tests (requires .env); validates token correctly]`. `vl_is_optional_skip` prüft per Teilstring auf `(requires .env)` und passt damit unverändert. Für die Anzeige die Klammern entfernen; ob `; ` zu `.` wird wie heute in der trx-Ausgabe, entscheidet der Worker, Hauptsache einheitlich.
- Im Probelauf 3818 `testcase`, 11 mit `<skipped message="RUN_INTEGRATION_TESTS not set …">`, 0 `failure`. Wie ein `<failure>`-Element samt Meldung aussieht, liefert der von Hand nachgestellte rote Test; daraus die Beispieldatei für `verify-lib.test.mjs` bauen.
- Zeilenorientierte Werkzeuge (grep pro Zeile) reichen wegen der Einzeile nicht. Ein kleiner Node-Leser (`scripts/junit-summary.mjs`, von `verify-lib.sh` aufgerufen) ist vermutlich robuster als Bash-Regex; Node ist im Gate ohnehin vorhanden.

Pfad der DLL: `src/Tests/bin/Debug/net10.0/Tests.dll`. Schritt 1 baut die Solution, `Tests.fsproj` ist darin enthalten. Schlägt Schritt 1 fehl, ist die DLL veraltet oder fehlt: Schritt 2 soll dann rot sein und nicht gegen eine alte DLL grün melden. Vorschlag: Schritt 2 nur starten, wenn Schritt 1 grün war, sonst als "nicht gelaufen, Build rot" melden.

Die Test-Command-Abschnitte der BC-READMEs nennen "dotnet test" als Teil des Gates. Der Worker meldet im README_DELTA nur infrastructure; die übrigen sechs Abschnitte zieht der Conductor beim Integrieren mit (reine Wortänderung, gleicher Satz in allen READMEs).

Kein Gedankenstrich in Kommentaren und Doku (globale Schreibregel).

## Merge-conflict note (iteration 1)

Geschwister-Task: infrastructure-shsvf (Gate übersetzt den Client einmal: verify.sh reicht dist/public aus Schritt 3 per BB_GUARD_DIST an Schritt 6 weiter), integriert als 3dac6b7.
Neue Basis: 3dac6b74ef4a72b518e06452b800369819db02da.
Auflöse-Liste: verify.sh (Konflikt nur im Kopfkommentar, Zeilen zu Schritt 2 und 3).
Änderung des Geschwisters an verify.sh: 13 Einfügungen, 3 Löschungen.
Gesicherter Diff vor dem Merge: /Users/romansachse/src/BudgetBuddy/.agentheim/salvage/infrastructure-d1dwb-merge-conflict.patch

## Outcome
`./verify.sh` Schritt 2 heißt jetzt "Expecto (Tests.dll)" und startet `src/Tests/bin/Debug/net10.0/Tests.dll` direkt mit `--junit-summary "$LOG_DIR/expecto.xml" --fail-on-focused-tests` und `USE_MEMORY_DB=true TZ=Europe/Berlin` im Aufruf. Kein `dotnet test` mehr im Gate. Ist Schritt 1 rot, meldet Schritt 2 "nicht gelaufen, Build rot".

Messung (Zeit aus der verify.sh-Ausgabe): vorher 168 s (Messung der Sitzung 2026-11-05 auf main), nachher 14 s im grünen Lauf, 15 s im roten Lauf. Grüner Lauf: `Total 3818, Passed 3807, Failed 0, Skipped 11`, die 11 sind die opt-in-Integrationstests "(requires .env)". `TestDatabaseGuardTests` ("Test-Datenbank-Wächter; Testlauf zeigt auf eine In-Memory-Datenbank") steht im JUnit-Bericht als grüner testcase.

Von Hand nachgestellt (temporäre Datei `src/Tests/TmpRedTests.fs`, danach samt fsproj-Eintrag entfernt):
- Roter Test (`Expect.equal 90 100`): Schritt 2 zeigt `✗ Tmp Rot.absichtlich rot` mit erster Fehlerzeile "Betrag stimmt.", `VERIFY: FAIL (rote Schritte: Expecto)`, unter "Rote Tests (1)" `dotnet: Tmp Rot.absichtlich rot`, Exit 1.
- `ftestCase`: Expecto schreibt dann keinen Bericht und endet mit Exit 1; Schritt 2 meldet "fokussierte Tests gefunden (ftestCase, ftestList), Exit 1", `VERIFY: FAIL`, Exit 1.
- Ein Probelauf mit einer Ausnahme im Test lieferte die `<error message="…"><![CDATA[…]]></error>`-Form, die Expect-Form ist `<failure message="…"/>` mit ANSI-Farbcodes in der Meldung. Beide zählen als rot, die Beispieldatei in den Tests enthält beide.

Dateien:
- `scripts/junit-summary.mjs`: Node-Leser für den einzeiligen JUnit-Bericht (count, tests, message). Fehlender oder fremder Bericht ergibt leere Ausgabe, verify.sh meldet dann "JUnit-Bericht nicht lesbar".
- `scripts/verify-lib.sh`: `vl_junit_count`, `vl_junit_tests`, `vl_junit_first_message` ersetzen `vl_dotnet_count`, `vl_trx_tests`, `vl_trx_first_message` (keine Aufrufer mehr, samt Tests entfernt). `vl_is_optional_skip` unverändert. Anzeige der Namen ohne eckige Klammern, Ebenen mit "." wie früher aus der trx-Datei.
- `scripts/verify-lib.test.mjs`: Tests gegen eine JUnit-Beispieldatei (Zähler, unlesbarer Bericht, rote und übersprungene Namen mit aufgelösten Entities, grüner Bericht, erste Fehlerzeile ohne Farbcodes) und der Wächter, der den Expecto-Aufruf aus `verify.sh` liest (USE_MEMORY_DB=true, TZ=Europe/Berlin, --junit-summary, --fail-on-focused-tests, kein `dotnet test`), mit Gegenbeweis.
- Kopfkommentar von `verify.sh`, Abschnitt "Gate" in `CLAUDE.md` und die Gate-Zeile in `.claude/rules/test-doctrine.md` nennen "Expecto über die Tests.dll".

Merge-Konflikt mit infrastructure-shsvf (Iteration 1): einziger Konflikt war der Kopfkommentar von `verify.sh`. Aufgelöst mit der eigenen Zeile für Schritt 2 und der Zeile des Geschwisters für Schritt 3 ("das Bundle dist/public nutzt Schritt 6 mit"). Der automatisch zusammengeführte Rest (Expecto-Block in Schritt 2, `BUILD_DIST` in Schritt 3, `BB_GUARD_DIST` in Schritt 6) ist stimmig; `scripts/verify-lib.test.mjs` und `scripts/run-guards.test.mjs` zusammen 27 von 27 grün. `./verify.sh` nach der Auflösung: Schritt 2 14 s, `VERIFY: PASS (dotnet 3807 grün, node 310 grün, dotnet build, npm run build, guard:classes, guard:tokens grün)`, Exit 0.

Für den Conductor: der prosaische Abschnitt "## Test command" der infrastructure-README (und der gleiche Satz in den übrigen BC-READMEs) nennt noch "dotnet test"; Wortänderung zu "Expecto über die Tests.dll". ADR 0030 bekommt den Hinweis, dass das Gate nicht mehr über `test.runsettings` läuft (ADR 0081).
