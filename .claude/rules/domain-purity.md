---
paths: src/Server/Domain.fs
---

# Domain Purity — Kein I/O, keine Seiteneffekte

## Verboten

- `Guid.NewGuid()` — IDs werden injiziert
- `DateTimeOffset.UtcNow` / `DateTime.UtcNow` — Zeitstempel werden injiziert
- `async { }` / `task { }` — Domain ist synchron
- `Persistence.*` / Datenbankzugriffe
- `printfn` / `Console.*` / Logging
- `Random()` / `Environment.*` / System-Zugriffe

## Richtig

- Alle Werte als Parameter injizieren: `let createInvite (now: DateTimeOffset) (inviteId: InviteId) ...`
- Pure Funktionen die Records transformieren
- `Result<'T, string>` für Validierungen im Domain

## Grep-Checks

```bash
# Finde I/O in Domain.fs
grep -n "Guid.NewGuid\|DateTimeOffset.UtcNow\|DateTime.UtcNow\|async\|Persistence\.\|printfn\|Console\." src/Server/Domain.fs

# Finde System-Zugriffe
grep -n "Random()\|Environment\.\|File\.\|Directory\." src/Server/Domain.fs
```
