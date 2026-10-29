---
paths: src/Server/**
---

# Domain Purity

Mechanisch geprüft von: `src/Tests/StructureGuardTests.fs` (Domain-Wächter). Jede `src/Server/**/Domain.fs` liest weder Uhr noch Zufall und macht kein I/O (Datei, Netz, Datenbank, Konsole, Umgebung, externe Clients). Was der Wächter meldet, nicht wegdiskutieren, sondern den Wert hineingeben.

Urteil, das kein Wächter prüft:

- Domain-Funktionen sind synchron. Kein `async { }` und kein `task { }` in `Domain.fs`; das Laden und Schreiben gehört in `CommandHandlers.fs`, `QueryHandlers.fs` oder `Persistence.fs`.
- Uhr, Ids und Konfiguration kommen als Parameter (`now: DateTimeOffset`, `id: LedgerEntryId`), nicht über Funktionswerte, die heimlich lesen.
- Regelverletzungen sind `Result`-Werte mit konkreter Meldung, keine Ausnahmen.
- Fachlogik, die außerhalb von `Domain.fs` liegt (`src/Server/RulesEngine.fs`, `src/Server/DuplicateDetection.fs`, `src/Server/OrderIdMatcher.fs`), hält sich an dieselben Regeln, auch wenn der Wächter sie nicht sieht.
