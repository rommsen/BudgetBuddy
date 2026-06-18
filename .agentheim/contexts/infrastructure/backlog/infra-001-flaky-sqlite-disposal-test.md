---
id: infra-001
title: Flaky Persistence-Test — SQLite-Disposal-Crash + Microsoft.Data.Sqlite-Versionskonflikt
status: backlog
type: bug
context: infrastructure
created: 2026-06-18
completed:
commit:
depends_on: []
blocks: []
tags: [tests, sqlite, persistence, flaky, dependencies]
related_adrs: []
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
