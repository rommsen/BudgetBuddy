---
id: infra-za7fq
title: "Wächter-Test, der laut scheitert, wenn ein Testlauf auf die echte Datei-Datenbank zeigt"
status: backlog
type: feature
context: infrastructure
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [test-infra, sicherheit]
related_adrs: [0030]
related_research: []
prior_art: []
---

## Why
Befund des Verifiers bei `ledger-b1xwe` (2026-09-12). ADR 0030 hat den Fehler behoben,
durch den `dotnet test` in Romans echte Entwicklungsdatenbank schrieb: `test.runsettings`
setzt `USE_MEMORY_DB` jetzt im Test-Host, weil VSTest den Einstiegspunkt in `Main.fs` nie
aufruft.

Der Schutz hängt aber daran, dass die Tests auf dem üblichen Weg gestartet werden. Zwei
Wege umgehen ihn **stillschweigend**:
- `dotnet test --settings andere.runsettings` überschreibt die `RunSettingsFilePath`-Angabe
  aus der `.fsproj`.
- ein direkter `dotnet vstest`-Aufruf liest die Projekt-Einstellung gar nicht.

In beiden Fällen fällt der Lauf lautlos auf die Datei-Datenbank zurück und schreibt wieder
in echte Daten. Genau diese Lautlosigkeit hat den ursprünglichen Fehler monatelang
unsichtbar gemacht: 106 Test-Konten und 365 Test-Buchungen sind so in eine Datenbank mit
rund 14.000 echten Buchungen gelangt, ohne dass irgendetwas gemeldet hat (Aufräumen:
`ledger-vm3dk`).

## What
Ein Test, der als einer der ersten läuft und den **tatsächlich aktiven** Connection-String
prüft. Zeigt er nicht auf eine In-Memory-Datenbank, scheitert er laut und nennt den Pfad,
auf den er zeigen würde.

Damit wird aus einem stillen Datenverlust ein roter Test. Der Schutz liegt dann nicht mehr
allein in einer Konfigurationsdatei, die man umgehen kann, sondern in einer Zusicherung,
die mitläuft.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured am 2026-09-12 aus der Verifikation von `ledger-b1xwe`. Zusammenhang: ADR 0030
(der Mechanismus), `ledger-vm3dk` (die Nachwirkung in der Datenbank).

Bei der Verfeinerung zu klären: ob der Wächter ein normaler Expecto-Test ist (läuft dann
irgendwann, nicht unbedingt zuerst) oder eine Zusicherung beim ersten Zugriff auf die
Datenbankkonfiguration (greift dann früher, kostet aber eine Prüfung im Produktivpfad).
