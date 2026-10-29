---
id: infrastructure-am805
title: "Security-Wächter: Pipeline-Test über Api.webApp, Negativtest je Remoting-Endpoint, Netzaufrufe nur hinter Schalter"
status: done
type: feature
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-dvy29]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Der /api-Ausfall (Fix 2a5c229) rutschte durch, weil kein Test die echte Giraffe-Pipeline fuhr. 13 von 75 Remoting-Endpoints ohne Test (Audit D). Souls E7, W1.

## What
Ein Test startet `Api.webApp()` über einen Test-Host und prüft je Remoting-Interface einen Aufruf mit Antwortrumpf. Ein tabellengetriebener Test über alle Remoting-Endpoints (Reflection) verlangt je Endpoint einen Negativfall; ein neuer Endpoint ohne Tabellenzeile macht den Test rot. Ein Wächter findet Tests mit Netzaufruf ohne `RUN_INTEGRATION_TESTS`-Prüfung.

## Acceptance criteria
- [ ] Pipeline-Test ist grün auf main und rot, wenn man `skipPipeline` in `BotApi.handler` testweise durch `next ctx` ersetzt.
- [ ] Endpoint-Tabelle deckt alle Remoting-Endpoints ab; Reflection-Abgleich schlägt bei fehlender Zeile fehl.
- [ ] Netz-Wächter schlägt bei einem Test ohne Schalter fehl (Gegenbeweis im Test).

## Notes
Migrationsschritt M5. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Startet vermutlich rot wegen fehlender Tabellenzeilen; die Zeilen sind Teil des Tasks.

## Outcome

Drei Wächter für den Gate-Lauf, alle in `src/Tests/`, eingetragen in `Tests.fsproj` (mittig nach `BotApiTests.fs`). Neues Paket: `Microsoft.AspNetCore.TestHost` 10.0.0.

**Pipeline-Test** (`src/Tests/RemotingPipelineTests.fs`): startet `Server.Api.webApp()` per `WebApplication` mit `UseTestServer` (kein Port, kein Netz, In-Memory-DB) und schickt je Remoting-Interface einen POST wie der Fable-Client (`getSettings`, `getBudgets`, `getAllRules`, `getSyncHistory`, `getLedgerAccounts`, `getBudgetMonth "kein-monat"`). Verlangt 200 und einen nicht leeren JSON-Rumpf. Die Interfaces kommen per Reflection aus `Shared.Api` (`remotingInterfaces`: Records, deren Felder alle Funktionen sind); ein Interface ohne Pipeline-Aufruf macht den Test rot. Gegenprobe: `/bot/categories` ohne Token gibt 401 aus derselben Pipeline.
AC 1 belegt: `skipPipeline` in `BotApi.handler` testweise durch `_next ctx` ersetzt, Pipeline-Test rot mit `BudgetApi: Rumpf ist leer` (200 mit leerem Rumpf, wie beim Ausfall), danach Datei per Kopie wiederhergestellt, `git diff src/Server/` leer.
Nebenbefund: ein GET auf eine unit-Methode endete im Testhost beim ersten Aufruf mit einer NullReferenceException in `Fable.Remoting.Server.Proxy`; der Test nutzt deshalb POST wie der echte Client.

**Endpoint-Tabelle** (`src/Tests/RemotingEndpointNegativeTests.fs`): 75 Zeilen, eine je Endpoint (Reflection zählt heute 75). Der Abgleichstest meldet fehlende, doppelte und verwaiste Zeilen; ein Gegenbeweis-Test entfernt eine Zeile und erwartet genau diesen Endpoint als fehlend. Die Fälle rufen die Server-Werte (`settingsApi`, `ynabApi`, ...) direkt auf: ungültige Eingabe, unbekannte Id/Sitzung oder fehlende Einrichtung führen zu Error, None oder leerem Ergebnis. Vor jedem Fall prüft `setup`, dass weder YNAB-Token noch Comdirect-Zugangsdaten in der Test-DB stehen, sonst bricht er ab (Schutz gegen Netzaufrufe).
Schwach abgedeckt (im Dateikopf benannt): die Lesesichten ohne Argument (`getLenaFlagSettings`, `getAllRules`, `getCurrentSession`, `getLastMirrorRun`, `getLedgerAccounts`, `getLedgerAccountBalances`, `exportRules`, `getSyncHistory -1`) prüfen nur "kein Absturz" bzw. den Leerzustand. `setDefaultBudget`, `setDefaultAccount` und `setQuickAddAccount` prüfen ihre Eingabe gar nicht; die Zeilen halten das fest (unbekannte Id wird angenommen) und stellen den alten Wert wieder her, siehe Backlog-Eintrag. Die vier Sammel-Endpoints `setReviewMarkBatch`, `restoreReviewMarkBatch`, `setEntryCategoryBatch`, `restoreFlagBatch` liefern für unbekannte Buchungen Ok ohne geänderte Buchung; die Zeilen prüfen genau das.

**Netz-Wächter** (`src/Tests/NetworkCallGuardTests.fs`): leitet die Netzfunktionen aus `YnabClient.fs`, `ComdirectClient.fs` und `ComdirectAuthSession.fs` ab (Rumpf mit `Request.sendAsync`, `new HttpClient()`, `createHttpClient(`, transitiv über Aufrufe; Funktionen mit eingespeistem HttpClient wie `sendReviewMark` zählen nicht). Danach prüft er je `testCase`-Block jeder Testdatei: Netzaufruf (Netzfunktion qualifiziert oder bei geöffnetem Modul, HttpClient ohne Handler, echte Hosts) nur mit `RUN_INTEGRATION_TESTS`/`shouldRunIntegrationTests` im Block. Kommentare, Blockkommentare und String-Literale zählen nicht. Gegenbeweise: synthetischer Testfall ohne Schalter wird gemeldet (Zeile 6), mit Schalter nicht, `new HttpClient(handler)` nicht, `new HttpClient()` und der Comdirect-Host schon.
Funde und Behebung: `YnabIntegrationTests` "handles invalid token gracefully" rief bei jedem Lauf die echte YNAB-API auf; `ComdirectIntegrationTests` "handles invalid credentials gracefully" rief Comdirect auf, sobald die API-Schlüssel in `.env` stehen (die Datei hat also einen Schalter, dieser Testfall aber nicht). Beide prüfen jetzt `shouldRunIntegrationTests()` und überspringen sonst; sie liegen in "(requires .env)"-Listen, verify.sh zählt den Skip als erlaubt.

Diary-Notiz (paralleler Worker, Eintrag hier statt in `diary/development.md`): Dateien wie oben; Gate `./verify.sh`: VERIFY: PASS (dotnet 2586 grün, node 300 grün, build, npm build, guard:classes, guard:tokens grün). Vorher 2502 grün: +86 neue Tests, 2 Integrationstests jetzt übersprungen.
