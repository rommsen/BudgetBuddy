---
paths: src/Server/**
---

# Persistence Safety

Mechanisch geprüft von: `src/Tests/StructureGuardTests.fs` (CLIMutable-Wächter: jeder `*Row`-Record trägt `[<CLIMutable>]`, jeder Dapper-Lesezugriff liest einen einfachen Typ oder einen Row-Record, jedes Option-Feld hat einen registrierten `OptionHandler`), `src/Tests/TestDatabaseGuardTests.fs` (Testlauf auf In-Memory-DB), `src/Tests/ModuleLevelDbInitGuardTests.fs` (keine DB-Initialisierung auf Modulebene).

Urteil, das kein Wächter prüft:

- Ein neuer Optionstyp in einem Row-Record (etwa `int option`) braucht eine eigene `OptionHandler`-Registrierung in `src/Server/Persistence.fs`. Der Wächter meldet das Fehlen, die Registrierung schreibst du.
- Verbindungen kommen aus den vorhandenen Helfern (`getConnection` in `src/Server/Persistence.fs`), keine eigene `SqliteConnection` mit eigenem Connection-String.
- Schemaänderungen laufen über `initializeDatabase` und müssen auf einer bestehenden Datenbank wiederholbar sein (`CREATE TABLE IF NOT EXISTS`, Spalten nur ergänzen).
- SQLite: `INTEGER` für `int64`, Beträge als Milliunits (`int64`).
