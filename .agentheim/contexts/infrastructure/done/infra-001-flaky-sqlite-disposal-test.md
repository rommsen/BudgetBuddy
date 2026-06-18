---
id: infra-001
title: Flaky Persistence-Test — SQLite-Disposal-Crash + Microsoft.Data.Sqlite-Versionskonflikt
status: done
type: bug
context: infrastructure
created: 2026-06-18
completed: 2026-06-18
commit: 82b5cef
depends_on: []
blocks: []
tags: [tests, sqlite, persistence, flaky, dependencies]
related_adrs: [0008]
related_research: []
prior_art: []
---

## Why
Der Persistence-Test `PatternType Conversions.Contains roundtrip` fällt **sporadisch** aus
(bei der design-system-004-Verifikation aufgetaucht: erster voller Lauf rot, danach grün in
Isolation und beim Re-Run). Ein flaky Test untergräbt das Vertrauen ins grüne Gate — man weiß
nicht mehr, ob ein roter Lauf ein echter Regressions-Fund oder nur das Rauschen ist.

## What
Den intermittierenden Fehler stabilisieren. Beobachtete Ursache (Hypothese, noch zu
bestätigen): `SqliteConnection.RemoveCommand` wirft eine *index-out-of-range* beim
**Disposen** der Connection, begünstigt durch einen **Versionskonflikt von
`Microsoft.Data.Sqlite`** (9.0.11 vs 9.0.13) über die Projekte hinweg.

## Acceptance criteria
- [ ] Root Cause bestätigt (Disposal-Race vs. Versionskonflikt vs. beides) — nicht nur die Hypothese übernehmen.
- [ ] `Microsoft.Data.Sqlite`-Version über alle `.fsproj` (Server, Tests, ggf. zentrale Props) vereinheitlicht.
- [ ] Der Test (und die Suite) läuft deterministisch grün über mehrere Läufe — auch der erste volle Lauf, nicht nur Isolation/Re-Run.
- [ ] Falls ein Connection-Disposal-/Lifecycle-Pattern schuld ist: Pattern gefixt (kein Test, der auf die Prod-DB schreibt — `:memory:`/temp).
- [ ] Kurzer Hinweis in der Diagnose, ob andere Tests dasselbe Disposal-Muster teilen.

## Notes
- **Quelle:** design-system-004-Verifier (2026-06-16/18). Wortlaut: „SqliteConnection.RemoveCommand index-out-of-range during connection disposal (aggravated by a Microsoft.Data.Sqlite 9.0.11/9.0.13 version conflict)". Passt deterministisch in Isolation, schlägt nur im vollen Lauf gelegentlich fehl → klassischer Disposal-/Reihenfolge-Effekt.
- **Erst-Schritte:** `grep -rn "Microsoft.Data.Sqlite" src/**/*.fsproj` + zentrale Paketverwaltung prüfen; `dotnet list package --include-transitive | grep -i sqlite`. Dann gezielt den Test in einer Schleife laufen lassen, um Flakiness zu reproduzieren.
- **Scope-Hinweis:** globally-true (Persistence/Test-Infra + Dependency-Versionierung über BCs hinweg) → infrastructure, nicht categorization, obwohl der Test eine `PatternType`-Konvertierung prüft.
- Noch **under-refined**: Root Cause unbestätigt → vor `work` ein Refine/Investigations-Schritt sinnvoll.

**Promote 2026-06-18:** Schnitt-Entscheidung (Roman): **ein Bug-Task** statt Spike+Fix —
Untersuchung und Fix sind eng gekoppelt (die Ursache bestätigt sich faktisch über das
Versions-Unify + einen Schleifen-Lauf). AC #1 erzwingt die Root-Cause-Bestätigung als
Schritt 1; der Worker darf bouncen, falls die Ursache sich als größer entpuppt als die
Versions-Hypothese. → todo.

## Outcome (2026-06-18)
Root Cause **bestätigt** und gefixt. Die Versions-Hypothese war nur **halb** richtig:

- **Versionskonflikt (bestätigt, Aggravator/Red Herring):** `Microsoft.Data.Sqlite` war
  `9.*` floating → Server-Output 9.0.13, Tests-Output 9.0.11 (binär verifiziert, MSB3277).
  Vereinheitlicht auf festen Pin `9.0.13` in `Server.fsproj` + `Tests.fsproj`. **Allein
  behob das den Crash NICHT** — er reproduzierte weiterhin.
- **Eigentliche Root Cause (bestätigt per Stacktrace):** Disposal-Race. Im Testmodus gab
  `getConnection()` *dasselbe* geteilte `SqliteConnection`-Objekt an jede Dapper-Operation;
  Expectos parallele Tests disposen Commands gleichzeitig → `RemoveCommand` mutiert die nicht
  synchronisierte `_commands`-Liste → `RemoveAt` index-out-of-range.

**Fix:** `getConnection()` gibt jetzt immer eine **frische** Connection zurück (wie schon im
Prod-Modus); die eine Test-Connection bleibt nur als Keep-Alive-Anker offen. Zentral in
`getConnection()` → deckt alle Operationen ab.

**AC-Diagnose (andere Tests mit gleichem Muster?):** Nein — `PersistenceTypeConversionTests.fs`
ist die einzige Datei mit echten DB-Operationen. `Main.fs` setzt nur das Env-Flag,
`EncryptionTests.fs` nutzt nur reines AES.

**Verifikation:** Build 0 Warnungen/0 Fehler (MSB3277 weg). Tests 595 passed / 6 skipped /
0 failed. Determinik: **15/15 frische Voll-Läufe grün** nach Fix (vorher reproduzierte der
Crash in ~2–8 Läufen; gegen den alten Code im A/B-Test erneut bestätigt).

**Key files:**
- `src/Server/Persistence.fs` — frische Connection pro Operation (Kern-Fix)
- `src/Server/Server.fsproj`, `src/Tests/Tests.fsproj` — Versions-Pin 9.0.13
- `src/Tests/PersistenceTypeConversionTests.fs` — Concurrency-Guard-Test
- `.agentheim/knowledge/decisions/0008-sqlite-per-operation-connection-and-version-pin.md` — ADR
