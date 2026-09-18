---
id: design-system-e5vfc
title: "Wächter-Abdeckung gegen den eigenen Verlust sichern: erfasste Startup-Typen gegen die Dateien unter src/Client/DesignSystem/ abgleichen"
status: backlog
type: chore
context: design-system
created: 2026-09-18
completed:
depends_on: []
blocks: []
tags: [test-infra, sicherheitsnetz]
related_adrs: []
related_research: []
prior_art: [design-system-bq4tz]
---

## Why
`src/Tests/DesignSystemStartupTests.fs` (aus `design-system-bq4tz`) erzwingt per Reflection
jeden Typinitialisierer der Design-System-Module und fängt damit eager ausgewertete
Top-Level-Werte ab, bevor sie unter `dotnet test` werfen. Der Filter arbeitet über den
Präfix `<StartupCode$Client>.$Client.DesignSystem.` plus eine Sonderfallzeile für
`<StartupCode$Client>.$ErrorDisplay` (diese Datei nutzt `namespace` mit innerem `module`
und trägt deshalb nicht den üblichen Präfix).

**Die Abdeckung ist heute echt, aber nicht gegen ihren eigenen Verlust geschützt.** Der
Verifier hat das bei der Abnahme von `design-system-bq4tz` gemessen, nicht vermutet: Er hat
`|| t.FullName = errorDisplayStartupTypeName` aus Zeile 52 entfernt, und die Suite blieb
grün. Grund: der begleitende Wächter-gegen-Wächter prüft nur, dass die Typliste **nicht
leer** ist, und das hält auch bei 19 statt 20 Typen.

Damit trägt der Test dieselbe Schwäche, die in dieser Aufgabe zweimal zum Fehlschlag
geführt hat: eine Prüfung, die aussieht, als decke sie etwas ab, und es nicht mehr tut,
sobald sich eine Annahme verschiebt. Erst war es ein Suchmuster, das eingerückte Werte
übersah, dann ein Präfixfilter, der eine abweichende Moduldeklaration übersah. Jetzt ist es
eine Sonderfallzeile, deren Verschwinden niemandem auffällt.

## What
Zwei Stufen, die zweite ist die eigentliche Absicherung.

1. **Die bekannte Ausnahme festnageln.** Ein
   `Expect.contains (designSystemStartupTypes () |> Array.map (fun t -> t.FullName)) errorDisplayStartupTypeName`
   wird rot, sobald die Abdeckung bricht, und zwar ohne dass irgendwo ein eager Wert
   existieren müsste. Das widerlegt zugleich die Annahme aus `design-system-bq4tz`, ein
   dauerhafter Test bräuchte "etwas Rotes zum Dagegenlaufen": geprüft wird hier die
   Abdeckung selbst, nicht ihr Anlass.

2. **Die Abdeckung insgesamt gegen die Wirklichkeit abgleichen**, statt einzelne Ausnahmen
   zu pflegen: die Zahl der erfassten Startup-Typen gegen die Zahl der `.fs`-Dateien unter
   `src/Client/DesignSystem/` prüfen. Das fängt auch die **nächste**
   Namenskonventions-Abweichung, statt nur die eine bereits bekannte. Beim Verfeinern zu
   klären, wie der Test an die Dateiliste kommt, ohne brüchig zu werden (relativer Pfad vom
   Testlauf aus, oder eine im Build erzeugte Liste).

## Acceptance criteria
- [ ] Wird die `ErrorDisplay`-Sonderfallzeile aus dem Filter entfernt, scheitert die Suite.
- [ ] Wird eine neue `.fs`-Datei unter `src/Client/DesignSystem/` angelegt, deren Modul der
      üblichen Namenskonvention nicht folgt, scheitert die Suite ebenfalls, statt sie still
      zu übergehen.
- [ ] Beide Fälle sind durch einen tatsächlich ausgeführten Eingriff belegt, nicht nur durch
      Lesen des Codes, und das Ergebnis steht im Outcome.
- [ ] `dotnet test` ist grün.

## Notes
Gefunden bei der Abnahme von `design-system-bq4tz`, Iteration 3 (2026-09-18). Der Verifier
hat beide Proben selbst gefahren: das Entfernen der Sonderfallzeile liess die Suite grün,
das Einfügen eines eageren `Html.div` in `ErrorDisplay.fs` liess sie mit dem Modulnamen im
Fehlertext scheitern. Die Abdeckung funktioniert also, sie ist nur nicht bewacht.

Kein Blocker: `design-system-bq4tz` erfüllt sein Kriterium 6 nachweislich, alle zwanzig
Dateien sind erfasst. Diese Aufgabe härtet den Wächter, sie repariert ihn nicht.
