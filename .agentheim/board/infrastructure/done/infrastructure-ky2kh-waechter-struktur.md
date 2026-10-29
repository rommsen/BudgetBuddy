---
id: infrastructure-ky2kh
title: "Struktur-Wächter: CLIMutable auf Dapper-Row-Typen, Domain pur, SQL ohne Interpolation"
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
`domain-purity`, `persistence-safety`, `query-safety` zielen auf nicht existierende bzw. Teil-Pfade und greifen für `src/Server/Ledger/`, `src/Server/Budgeting/` nie (Audit A, B1, geprüft). Souls W3, W4, W7.

## What
Reflection-Wächter `[<CLIMutable>]` nach Vorlage `souls/src/Tests/CliMutableGuard.Tests.fs`. Quelltext-Wächter über `src/Server/**/Domain.fs` (kein `UtcNow`, `NewGuid`, I/O). Wächter gegen String-Interpolation in SQL über `**/Persistence.fs` und `QueryHandlers.fs`. Jeder Wächter mit eingebautem Gegenbeweis.

## Acceptance criteria
- [ ] Die drei Wächter sind grün auf main oder die Funde sind als Teil des Tasks behoben.
- [ ] Jeder Wächter hat einen Gegenbeweis-Test, der zeigt, dass er anschlägt.

## Notes
Migrationsschritt M6. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Nach diesem Schritt messen (Baseline-Replay).

## Outcome

Neue Datei `src/Tests/StructureGuardTests.fs` (14 Tests), in `src/Tests/Tests.fsproj` mittig nach `BackendPortGuardTests.fs` eingetragen. Registrierung über `[<Tests>]`, wie bei den übrigen Wächtern.

Gemeinsamer Lexer `lex`: entfernt `//`- und verschachtelte `(* *)`-Kommentare, löst String-Literale heraus (normal, verbatim, dreifach, interpoliert mit `$`/`$$`, Zeichenliterale wie `'"'`) und ersetzt sie durch Platzhalter. Zeilennummern bleiben erhalten. Die Helfer aus `NetworkCallGuardTests.fs` sind privat und kennen keine dreifachen Literale, deshalb ein eigener Lexer; `NetworkCallGuardTests.fs` ist unverändert.

**CLIMutable-Wächter** (Vorlage Souls `CliMutableGuard.Tests.fs`). Regel für "Dapper-Row-Typ": jeder F#-Record der Server-Assembly (auch private), dessen Name auf `Row` endet. Damit die Namensregel nicht umgangen wird, prüft ein zweiter Test im Quelltext aller `src/Server/**/*.fs`, dass jedes Typargument von `Query`, `QueryFirst`, `QueryFirstOrDefault`, `QuerySingle`, `QuerySingleOrDefault` (je auch `Async`) ein einfacher Typ (string, int64, ...) oder ein solcher Row-Record ist. Ein dritter Test liest die `AddTypeHandler(OptionHandler<T>())`-Registrierungen aus `src/Server/Persistence.fs` (heute string, DateTime) und meldet Option-Felder von Row-Records mit anderem inneren Typ. Leere Typmenge ist rot. Gegenbeweise: Probe-Record ohne Attribut, `QueryFirstOrDefault<Entry>`, Probe-Record mit `int option`.

**Domain-Wächter**: alle `src/Server/**/Domain.fs` (heute Ledger und Budgeting, mindestens 2 verlangt). Verboten außerhalb von Kommentaren und Strings: `DateTime(Offset).Now/UtcNow/Today`, `Guid.NewGuid`, `Random`, `Stopwatch`/`Thread.Sleep`/`Task.Delay|Run`/`Async.Sleep`, `File.`/`Directory.`/Streams, `HttpClient`/FsHttp, `Sqlite*`/`Dapper`/`IDbConnection`/`Persistence.`, `Console.`/`printf(n)`/`eprintf(n)`, `Environment.`, `Process.Start`, die externen Clients, `open System.IO|System.Net|Dapper|Microsoft.Data.Sqlite`. Gegenbeweise: synthetische Datei mit fünf Verstößen (Zeilen genau gemeldet) und eine mit denselben Wörtern nur in Kommentar, Blockkommentar, String, Zeichenliteral und `sprintf`.

**SQL-Wächter**: alle `src/Server/**/Persistence.fs` und `src/Server/**/QueryHandlers.fs`. SQL ist ein Literal mit Großbuchstaben-Schlüsselwort (SELECT, WHERE, AND, ORDER BY, ...). Fund ist: ein eingesetzter Ausdruck in einem interpolierten SQL-Literal, `sprintf`/`failwithf`/`String.Format` mit SQL-Literal, eine `+`/`^`-Kette mit SQL-Literal und Nicht-Literal, `StringBuilder.Append(x)` auf einem Builder, der auch SQL-Literale bekommt. Ausnahmen nur über die kommentierte `sqlAllowList` (Datei, Ausdruck, Begründung): in `Ledger/Persistence.fs` `whereSql`, `needsReviewSqlClause`, `askLenaSqlClause`, `categoryClause`, `withoutCategoryClause`, `orderBySql sort`, `dir`, `excludeSql`, alle aus Konstanten bzw. geschlossenen Unions gebaut, Werte als `@`-Parameter. Ein nicht mehr gebrauchter Eintrag macht einen eigenen Test rot. Gegenbeweise: synthetische Datei mit Interpolation, `sprintf`, Verkettung, Append und mehrzeiligem `$"""..."""` (Zeile und Ausdruck je genau gemeldet), dazu eine Datei mit Parameter, Fehlermeldung, deutschem Text und erlaubtem Baustein (grün; ohne Erlaubnis wird der Baustein gemeldet).

Funde auf main: keine. AC 1 ist damit ohne Codeänderung erfüllt. Zusätzlich gegen die echten Dateien geprüft: `System.DateTime.UtcNow` in `Budgeting/Domain.fs` und `$"SELECT ... {x}"` in `Budgeting/Persistence.fs` testweise angehängt, zwei Wächter-Tests rot, Dateien danach per Kopie wiederhergestellt.

Bekannte Grenzen (im Dateikopf benannt): `f x + "..."` meldet nur `x`; SQL-Text ohne Großbuchstaben-Schlüsselwort (`$"date {dir}, id {dir}"`) gilt nicht als SQL.

Diary-Notiz (paralleler Worker, Eintrag hier statt in `diary/development.md`): Dateien wie oben. Gate `./verify.sh`: VERIFY: PASS (dotnet 2600 grün, node 306 grün, dotnet build, npm run build, guard:classes, guard:tokens grün). Vorher 2586: +14 neue Tests.
