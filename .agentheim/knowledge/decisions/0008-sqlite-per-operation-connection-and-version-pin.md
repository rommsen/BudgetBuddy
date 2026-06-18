---
id: 0008
title: SQLite — feste Versions-Pins + frische Connection pro Operation (kein geteiltes Connection-Objekt)
scope: infrastructure
status: accepted
date: 2026-06-18
supersedes: []
superseded_by: []
related_tasks:
  - contexts/infrastructure/done/infra-001-flaky-sqlite-disposal-test.md
related_research: []
---

# ADR 0008: SQLite — feste Versions-Pins + frische Connection pro Operation

## Context
Der Persistence-Test `Persistence Type Conversions › PatternType Conversions › Contains
roundtrip` (und gelegentlich andere Fälle derselben Datei) fiel **sporadisch** aus: nur im
ersten vollen Suite-Lauf rot, in Isolation und im Re-Run grün. Aufgetaucht bei der
design-system-004-Verifikation.

Bei der Untersuchung (infra-001) zeigten sich **zwei** Befunde:

1. **Versionskonflikt (bestätigt):** `Microsoft.Data.Sqlite` war in `Server.fsproj` als
   `9.*` (floating) referenziert; `Tests.fsproj` hatte keinen direkten Verweis und zog die
   Version transitiv. Resultat: Server-Output lieferte `9.0.13.0`, Tests-Output `9.0.11.0`
   (binär verifiziert), Build-Warnung **MSB3277** ("Konflikt … konnte nicht aufgelöst
   werden"). Das ist eine echte Inkonsistenz, aber **nicht** die Ursache des Crashs — nach
   dem Vereinheitlichen auf `9.0.13` reproduzierte der Crash weiterhin.

2. **Eigentliche Root Cause (bestätigt per Stacktrace):**
   ```
   System.Collections.Generic.List`1.RemoveAt(Int32 index)
     Microsoft.Data.Sqlite.SqliteConnection.RemoveCommand(SqliteCommand command)
     Microsoft.Data.Sqlite.SqliteCommand.Dispose(Boolean disposing)
     Dapper.SqlMapper.QueryRowAsync[T] …
   ```
   Im Testmodus gab `getConnection()` **dasselbe** geteilte `SqliteConnection`-Objekt an
   *jede* Dapper-Operation zurück (eine einzige In-Memory-Connection als Keep-Alive für die
   `Mode=Memory;Cache=Shared`-DB). `SqliteConnection.RemoveCommand` mutiert eine **nicht
   synchronisierte** interne `_commands`-Liste. Expecto führt `testCase`s **parallel** aus →
   mehrere Dapper-Commands disposen gleichzeitig auf demselben Connection-Objekt → `RemoveAt`
   mit Index außerhalb des Bereichs. Klassischer Disposal-/Reihenfolge-Race, intermittierend,
   weil zwei Disposes exakt interleaven müssen.

## Decision
Beides adressieren:

1. **Versions-Pin:** `Microsoft.Data.Sqlite` in **`Server.fsproj` und `Tests.fsproj`** auf
   die **identische, feste** Version `9.0.13` pinnen (kein `9.*` mehr, kein transitiver
   Gewinner). Beseitigt MSB3277 und künftige Patch-Drift.

2. **Frische Connection pro Operation:** `getConnection()` gibt **immer** eine neue
   `SqliteConnection` zurück (Dapper öffnet/schließt geschlossene Connections automatisch).
   Im Testmodus bleibt **eine** Connection als **Keep-Alive-Anker** offen — sie hält die
   `Cache=Shared`-In-Memory-DB am Leben, wird aber **nicht** mehr an Aufrufer durchgereicht.
   Damit registriert/disposed jede Operation Commands auf ihrem **eigenen**
   Connection-Objekt → keine geteilte mutable Liste mehr. Das **spiegelt das Verhalten im
   Produktionsmodus** (dort gab `getConnection()` schon immer eine frische Connection
   zurück → der Bug trat nur im Test auf).

## Consequences
- **Positiv:** Suite läuft deterministisch grün (15/15 frische Voll-Läufe nach dem Fix,
  vorher reproduzierte der Crash in ~2–8 Läufen). Build ohne Versions-Warnung.
  Test-Verhalten = Prod-Verhalten (eine Fehlerquelle weniger).
- **Neutral:** Pro Operation eine neue (gepoolte) Connection — in der Praxis vernachlässigbar,
  da SQLite-Connections leichtgewichtig sind und Dapper das Open/Close übernimmt.
- **Regression-Guard:** `Persistence Connection Disposal › concurrent persistence
  operations …` als Defense-in-depth. **Wichtig:** der Original-Race war nur über die
  Parallelität der *gesamten* Suite auf dem geteilten Objekt reproduzierbar, **nicht** aus
  einem isolierten Testfall — der eigentliche Regressionsbeweis ist die Multi-Lauf-Determinik
  (siehe `diary/development.md`).
- **Folge-Verpflichtung:** Künftige Persistence-Erweiterungen dürfen **kein** langlebiges,
  geteiltes mutable Connection-Objekt über Threads hinweg verwenden. Wer eine zentrale
  Paketverwaltung (`Directory.Packages.props`) einführt, sollte den `9.0.13`-Pin dorthin
  zentralisieren.

## Alternatives considered
- **Nur Versions-Unify:** verworfen — Crash reproduzierte weiterhin (Versionskonflikt war
  Aggravator/Red Herring, nicht Ursache).
- **Lock um jede Dapper-Operation im Testmodus:** verworfen — verkompliziert jede Operation,
  Dapper erzeugt/disposed Commands intern (schwer sauber zu umschließen), und es würde
  Test- von Prod-Verhalten weiter entkoppeln statt angleichen.
- **Expecto sequenziell laufen lassen:** verworfen — versteckt das Symptom (Suite langsamer),
  ohne die fragile geteilte-Connection-Annahme zu beheben.
