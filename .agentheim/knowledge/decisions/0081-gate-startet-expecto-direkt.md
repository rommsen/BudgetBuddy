---
id: ADR-0081
title: Das Gate startet Expecto direkt über die Tests.dll statt über dotnet test
scope: infrastructure
status: accepted
date: 2026-11-05
related_tasks: [infrastructure-d1dwb]
related_adrs: ["0030"]
---
# ADR-0081: Das Gate startet Expecto direkt über die Tests.dll statt über dotnet test

## Kontext
`./verify.sh` lief Schritt 2 als `dotnet test` mit trx-Logger. Das kostete 168 s, Expecto selbst meldet für alle Tests 13 s. Die Differenz geht in den VSTest-Adapter (`YoloDev.Expecto.TestSdk`, Discovery und Melden jedes Einzelergebnisses) und den trx-Logger. Jeder Worker- und Verifier-Lauf zahlt das.

## Entscheidung
Schritt 2 ruft nach grünem Build `USE_MEMORY_DB=true TZ=Europe/Berlin dotnet src/Tests/bin/Debug/net10.0/Tests.dll --junit-summary <bericht> --fail-on-focused-tests` auf. Ausgewertet wird der JUnit-Bericht von Expecto (eine XML-Zeile mit BOM, ohne Zähl-Attribute, rote Tests als `<failure>` oder `<error>`), gelesen von `scripts/junit-summary.mjs`. Ist der Build rot, läuft Schritt 2 nicht und ist rot, damit keine veraltete DLL grün meldet.

## Folgen
- `test.runsettings` (ADR 0030) greift im Gate nicht mehr. Den Schutz tragen `main` in `src/Tests/Main.fs` (setzt `USE_MEMORY_DB`), `TestDatabaseGuardTests` (prüft in-process gegen `:memory:`) und die Umgebung im Aufruf selbst; ein Wächter in `scripts/verify-lib.test.mjs` hält `USE_MEMORY_DB=true` und `TZ=Europe/Berlin` im Aufruf fest. `TZ` braucht der Kalendertag-Regressionstest aus ynab-r9k4t.
- Ein vergessenes `ftestCase` macht das Gate rot statt still alle anderen Tests abzuschalten.
- `dotnet test` und `test.runsettings` bleiben für IDE und Einzelaufrufe.
- Verworfen: `dotnet test --no-build` (170 s, gleiche Adapterkosten).
