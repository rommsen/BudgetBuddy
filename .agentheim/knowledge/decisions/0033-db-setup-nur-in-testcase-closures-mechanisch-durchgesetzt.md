---
id: 0033
title: "DB-Setup in [<Tests>]-Bindings nur in testCase-Closures, mechanisch durchgesetzt"
scope: infrastructure
status: accepted
date: 2026-09-19
supersedes: []
superseded_by: []
related_tasks: [infra-sdad8, infra-za7fq]
related_adrs: [0030]
related_research: []
---

# ADR 0033: DB-Setup in `[<Tests>]`-Bindings nur in `testCase`-Closures, mechanisch durchgesetzt

## Context
infra-za7fq hatte einen Wächter-Test eingeführt (`TestDatabaseGuardTests.fs`), der meldet, wenn ein Testlauf auf eine Datei-Datenbank statt auf die In-Memory-Datenbank zeigt. Bei der Verifikation dieses Wächters fiel auf, dass er zwar meldet, aber nicht verhindert: `LedgerMirrorTests.fs` und `PersistenceTypeConversionTests.fs` riefen `initializeDatabase()`/`initializeLedgerSchema()` in fünf `[<Tests>]`-Bindings direkt im `let`-Body auf, außerhalb jeder `testCase`-Closure, im Muster `[<Tests>] let xyz = initializeDatabase(); ...; testList [...]`.

F# wertet Modul-Level-Bindings aus, sobald das Modul zum ersten Mal berührt wird. Genau diesen Zugriff löst Expectos eigene `[<Tests>]`-Entdeckung per Reflection aus: sie liest den Wert jedes `[<Tests>]`-gebundenen Members, um die `Test`-Struktur zu bekommen, und zwingt damit den Modul-Initialisierer zu laufen, bevor auch nur ein einzelner Testfall gestartet wurde. In einem reproduzierten Umgehungslauf (abweichende Runsettings, `DATA_DIR` auf ein Scratch-Verzeichnis) entstand dadurch trotz des roten Wächter-Tests eine vollständige Datei-Datenbank mit Schema und fünf Zeilen in `ledger_accounts`, bevor der Wächter überhaupt zur Ausführung kam.

Die erste Iteration dieser Aufgabe behob die fünf Fundstellen (sowie zwei weitere gleichartige in `LedgerPageTests.fs` und `LedgerTests.fs`, die derselbe Suchlauf zusätzlich fand), verankerte die Konvention aber nur in Prosa (README-Delta, diese ADR), ohne sie durchzusetzen. Ein künftiges Testmodul, das denselben Fehler reintroduziert, wäre vom bestehenden Wächter-Test nicht erkannt worden: der läuft unter einem normalen `dotnet test`-Lauf grün, weil `test.runsettings` `USE_MEMORY_DB` bereits vor jedem Code setzt.

## Decision
1. Jeder DB-Setup-Aufruf (`initializeDatabase()`, `initializeLedgerSchema()`, `seedAccounts()`) in einem `[<Tests>]`-Binding läuft ab sofort nur noch innerhalb einer `testCase`-Closure, nicht mehr im `let`-Body davor. Umgesetzt über eine lokal geschattete `testCase`-Hilfsfunktion pro Binding: `let testCase name body = testCase name (fun () -> initializeDatabase (); body ())`. Alle Aufrufe von `testCase` innerhalb des restlichen Bindings greifen dadurch automatisch auf die geschattete Version zu, ohne jeden einzelnen Testfall einzeln anfassen zu müssen. Beide Setup-Funktionen sind idempotent (`CREATE TABLE IF NOT EXISTS` bzw. Upsert per Id), der wiederholte Aufruf pro Testfall kostet nur eine überflüssige Prüfung.
2. Betroffen waren `LedgerMirrorTests.fs` (drei Bindings: `mirrorRunTests`, `mirrorGuardTests`, `runMirrorInBackgroundTests`), `PersistenceTypeConversionTests.fs` (zwei Bindings: `typeConversionTests`, `connectionDisposalRegressionTests`), sowie `LedgerPageTests.fs` (`ledgerPagingTests`, inklusive `seedAccounts()`) und `LedgerTests.fs` (`ledgerPersistenceTests`).
3. Die Konvention wird mechanisch durchgesetzt statt nur dokumentiert: `src/Tests/ModuleLevelDbInitGuardTests.fs` durchsucht jeden `[<Tests>]`-Block unter `src/Tests/` textbasiert auf genau dieses Muster (Aufruf von `initializeDatabase`/`initializeLedgerSchema` außerhalb jeder `fun ... ->`-Closure) und schlägt fehl, falls ein künftiges Testmodul es reintroduziert. Der Scan arbeitet über einen Einrücktiefen-Stack relativ zur öffnenden Closure-Zeile, nicht über eine feste Spalte, und ist deshalb robust gegen verschachtelte Module (ein Spalte-0-Anker würde solche Fälle verfehlen, wie es bei der Design-System-Variante desselben Wächter-Musters in design-system-bq4tz passiert ist). Er sucht ausschließlich innerhalb von `[<Tests>]`-Blöcken nach den zwei konkreten Funktionsnamen, nicht nach jeder beliebigen eingerückten Zeile, und trifft deshalb keine gewöhnlichen lokalen Bindungen in Funktionsrümpfen.
4. Die Durchsetzung wurde falsifizierbar geprüft: das historische Bug-Muster wurde testweise in `LedgerMirrorTests.fs` zurückgespielt (`initializeLedgerSchema ()` und `global.Persistence.initializeDatabase ()` wieder direkt im `let`-Body statt in der geschatteten Closure), der neue Wächter-Test schlug daraufhin rot fehl und nannte Datei und Zeile (`LedgerMirrorTests.fs:539: initializeLedgerSchema ()`), danach wurde die Datei zurückgesetzt und der Test war wieder grün. Zusätzlich prüfen vier synthetische Testfälle innerhalb von `ModuleLevelDbInitGuardTests.fs` selbst das historische Muster, das reparierte Muster und ein verschachteltes Modul gegen die Erkennungsfunktion.
5. Verworfene Alternative: eine reflection-basierte Laufzeitprüfung analog zu `DesignSystemStartupTests.fs` (Modul-Typinitialisierer per Reflection erzwingen und beobachten). Verworfen, weil sie unter einem normalen `dotnet test`-Lauf nichts unterscheiden könnte: `test.runsettings` setzt `USE_MEMORY_DB` bereits, bevor irgendein Code läuft, sodass sowohl das korrekte als auch das fehlerhafte Muster auf dieselbe In-Memory-Datenbank zeigen würden. Der Unterschied zwischen den beiden Mustern liegt nicht im Laufzeitzustand der Datenbank, sondern darin, ob der Aufruf als Anweisung im `let`-Body oder als Anweisung innerhalb einer Closure im Quelltext steht. Das ist eine Quelltext-Eigenschaft, kein Laufzeitzustand, und deshalb ist ein Quelltext-Scan hier das treffendere Werkzeug als ein Reflection-Sweep.

## Consequences
### Positive
- Ein künftiges Testmodul, das dieselbe Modul-Level-Falle reintroduziert, macht `dotnet test` rot, statt sich lautlos einzuschleichen.
- Das Muster (lokal geschattete `testCase`-Closure) ist für künftige Testdateien mit DB-Setup wiederverwendbar und bereits an vier Stellen im Repo vorexerziert.
### Negative
- Der Quelltext-Scan ist kein vollständiger F#-Parser, sondern ein zeilenbasierter Scan mit einem Einrücktiefen-Stack. Bekannter, dokumentierter Grenzfall: ein unbewachter Aufruf, der textlich nach einem thematisch unabhängigen `fun ... ->` an anderer Stelle desselben Blocks steht, ohne selbst in dessen Closure zu liegen, könnte in seltenen, im Repo aktuell nicht vorkommenden Layouts fälschlich als sicher gelten. Für die tatsächlich vorkommenden Muster (Aufruf direkt im `let`-Body vs. Aufruf innerhalb der ersten öffnenden Closure) ist der Scan durch vier synthetische Tests und den erfolgreichen Reproduktionsversuch belegt korrekt.
- Der Scan deckt namentlich nur `initializeDatabase`/`initializeLedgerSchema` ab, nicht `seedAccounts()` (das in `LedgerPageTests.fs` derselben Konvention folgt). Diese Einschränkung ist bewusst dokumentiert, nicht stillschweigend: würde `seedAccounts()` künftig wieder auf Modulebene aufgerufen, bliebe das vom Scan unentdeckt, bis die Namensliste in `ModuleLevelDbInitGuardTests.fs` erweitert wird.
