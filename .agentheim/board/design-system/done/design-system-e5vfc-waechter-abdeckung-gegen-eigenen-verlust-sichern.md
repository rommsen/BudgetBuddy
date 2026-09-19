---
id: design-system-e5vfc
title: "Wächter-Abdeckung gegen den eigenen Verlust sichern: erfasste Startup-Typen gegen die Dateien unter src/Client/DesignSystem/ abgleichen"
status: done
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

**Stand bei der Verfeinerung (2026-09-19, nachgesehen, nicht vermutet):** Der Filter sitzt
in `designSystemStartupTypes` (`src/Tests/DesignSystemStartupTests.fs`) und lautet
`t.FullName.StartsWith(startupTypePrefix) || t.FullName = errorDisplayStartupTypeName`. Der
begleitende Wächter-gegen-Wächter ist der Test "Es gibt mindestens ein
Design-System-Startup-Modul zu prüfen" und prüft ausschließlich `Expect.isNonEmpty` — genau
die Lücke, die der Verifier gemessen hat. Die Kriterien oben stehen unverändert; sie waren
bereits konkret und falsifizierbar.

## Outcome

`DesignSystemStartupTests.fs` (design-system-bq4tz) prüfte seine eigene Abdeckung bisher
nur mit `Expect.isNonEmpty` auf die gefundene Typliste, eine Prüfung, die auch bei 19 statt
20 erfassten Typen grün bleibt. Der Verifier von design-system-bq4tz hatte genau das
gemessen: die Sonderfallzeile für `ErrorDisplay` aus dem Filter entfernt, und die Suite
blieb grün. Zwei neue Tests schließen diese Lücke.

**Test 1** nagelt den bekannten Sonderfall fest: `Expect.contains` auf die Liste der
Typnamen prüft explizit, dass `<StartupCode$Client>.$ErrorDisplay` erfasst bleibt.
Verschwindet die Filterzeile, wird dieser Test rot, unabhängig davon, ob irgendwo ein eager
Wert existiert.

**Test 2** ist die eigentliche Absicherung und deckt nicht nur den einen bekannten Fall ab,
sondern jede künftige Namenskonventions-Abweichung: er vergleicht die Zahl der erfassten
Startup-Typen gegen die Zahl der `.fs`-Dateien unter `src/Client/DesignSystem/`. Der
Dateipfad wird über `__SOURCE_DIRECTORY__` relativ zum Testlauf ermittelt, exakt dasselbe
Muster wie in `ModuleLevelDbInitGuardTests.fs` und `FontFaceRegressionTests.fs`, kein neuer
Mechanismus.

**Beide Fälle wurden tatsächlich ausgeführt, nicht nur gelesen:**

1. Die Filterzeile `|| t.FullName = errorDisplayStartupTypeName` entfernt und
   `dotnet test` gefahren: die Suite scheiterte mit 2 Fehlern (Test 1: der Name fehlt in der
   Liste; Test 2: 20 Typen gegen 20 Dateien, aber nur noch 19 gefundene Typen, `actual: 19`).
   Zeile danach wieder eingesetzt, Suite wieder grün.
2. Eine neue Datei `src/Client/DesignSystem/ProbeStrayModule.fs` mit `module
   ProbeStrayModule` (folgt bewusst nicht der `Client.DesignSystem.*`-Konvention) angelegt
   und `dotnet test` gefahren: die Suite scheiterte mit 1 Fehler in Test 2 (`21`
   Dateien gegen `20` erfasste Typen, `actual: 20`), obwohl die Datei nicht einmal in
   `Client.fsproj` kompiliert wurde. Datei danach wieder gelöscht, Suite wieder grün.

`git status --porcelain` ist nach beiden Proben und deren Rücksetzung sauber bis auf die
eine beabsichtigte Änderung an `DesignSystemStartupTests.fs`.

**Endstand:** `dotnet test` läuft grün mit 899 bestanden, 7 übersprungen, 906 gesamt (Basis
vor dieser Aufgabe: 897/904, plus die zwei neuen Tests).

**Randbeobachtung, nicht bearbeitet:** `src/Tests/ModuleLevelDbInitGuardTests.fs`
(infra-sdad8, heute Morgen integriert) trägt strukturell dieselbe Schwäche, die diese
Aufgabe hier behoben hat: sein eigener Wächter-gegen-den-Wächter prüft nur `totalBlocks >
0`. Das liegt außerhalb dieser Aufgabe (andere Datei, andere Bounded-Context-Zuordnung) und
wurde nicht angefasst. Der hier gebaute Mechanismus (Abgleich einer Reflection-basierten
Zählung gegen eine unabhängige, im Dateisystem ermittelte Zählung) ließe sich dort
sinngemäß übertragen, falls das gewünscht ist.

**Diary-Hinweis:** dieser Worker schreibt gemäß Aufgabenkontext nicht selbst in
`diary/development.md` (das ist Konduktor-Bookkeeping); der obige Text deckt den Inhalt ab,
den ein Diary-Eintrag getragen hätte: was geändert wurde, warum, und mit welchem
Testergebnis.
