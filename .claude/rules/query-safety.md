---
paths: src/Server/**
---

# Query Safety

Mechanisch geprüft von: `src/Tests/StructureGuardTests.fs` (SQL-Wächter: in `src/Server/**/Persistence.fs` und `src/Server/**/QueryHandlers.fs` kommt kein Wert per `$"..."`, `sprintf`, `String.Format`, Verkettung oder `StringBuilder.Append` in SQL). Ein eingesetzter Baustein aus Konstanten braucht einen kommentierten Eintrag in `sqlAllowList`.

Urteil, das kein Wächter prüft:

- **Kein N+1:** keine Abfrage je Element in einer Schleife. Ids sammeln, einmal mit `IN @Ids` lesen, im Code gruppieren.
- **Mehrere Tabellen, eine Transaktion:** wer in einem Befehl in zwei oder mehr Tabellen schreibt, nutzt `conn.BeginTransaction()` und gibt die Transaktion an jedes `Execute` weiter (Vorbild `src/Server/Ledger/Persistence.fs`).
- **Kein stiller Default in Konvertern:** ein unbekannter String aus der DB wird gemeldet (`failwith $"Unbekannt: {s}"`), nicht auf einen Standardwert abgebildet.
- Der SQL-Wächter sieht keine Verkettung über Funktionsanwendung mit Leerzeichen und keinen SQL-Text ohne Großbuchstaben-Schlüsselwort. Dort selbst auf `@name`-Parameter achten.
