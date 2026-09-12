---
id: 0030
title: "dotnet test erzwingt die In-Memory-DB über test.runsettings (VSTest ruft nie main auf)"
scope: infrastructure
status: accepted
date: 2026-09-12
supersedes: []
superseded_by: []
related_tasks: [ledger-b1xwe]
related_adrs: []
related_research: []
---

# ADR 0030: `dotnet test` erzwingt die In-Memory-DB über `test.runsettings`

## Context
Bei der Arbeit an `ledger-b1xwe` (seitenweise Kassenbuch-Liste) liefen neue und
bestehende Ledger-Tests unter `dotnet test` reproduzierbar, aber sinnlos falsch
(doppelte Konten, falsche Trefferzahlen, "erwartet 1 neue Buchung, war 0"). Die
Ursache war keine Logik im neuen Code: `src/Tests/Main.fs`s `[<EntryPoint>] main`
setzt `USE_MEMORY_DB=true`, **bevor** `runTestsInAssemblyWithCLIArgs` laeuft — das
schützt aber nur den Direktaufruf `dotnet exec Tests.dll`. `dotnet test` startet die
Tests über den VSTest-Adapter (`YoloDev.Expecto.TestSdk`), der die Test-Assembly als
Bibliothek lädt und Expecto per Reflection aufruft; **`main` wird dabei nie
ausgeführt**. Ohne die Umgebungsvariable im aufrufenden Prozess fiel
`Persistence.dbConfig` (lazy, `src/Server/Persistence.fs`) auf den Datei-Pfad zurück:
`DATA_DIR` (Default `~/my_apps/budgetbuddy-dev/`) `/budgetbuddy.db` — eine echte,
persistente Entwicklungsdatenbank mit ~14.000 echten Buchungen aus Romans
tatsächlichem YNAB-Spiegel.

Jeder `dotnet test`-Lauf ohne manuell gesetztes `USE_MEMORY_DB=true` schrieb seither
Testdaten in genau diese Datei: Test-Konten ("Konto 1", "Konto 2", "Comdirect Giro"
u. a. aus `LedgerMirrorTests.fs`s Fixtures), Test-Buchungen und Test-Eintraege in der
globalen `settings`-Tabelle (`ledger_mirror_server_knowledge_budget-*` je
Testfall-Budget, sowie `ledger_mirror_last_run` bei jedem `runMirror`-Testlauf
überschrieben). Das ist ein direkter Verstoss gegen die Projektregel "Tests dürfen
niemals in die Produktionsdatenbank schreiben" — nicht durch eine einzelne Testdatei,
sondern strukturell: **jeder** `dotnet test`-Aufruf in diesem Repo war betroffen,
nicht nur die Ledger-Tests dieser Scheibe.

Befund und Reproduktion: `git stash` (Code ohne diese Scheibe) lief unter `dotnet
test` zuverlässig grün; derselbe Befehl mit den neuen Ledger-Tests schlug
deterministisch fehl — nicht weil der neue Code falsch war, sondern weil mehr
Testfälle mehr Kollisionen mit bereits akkumulierten Alt-Testdaten in derselben realen
Datei erzeugten. Mit `USE_MEMORY_DB=true dotnet test` liefen exakt dieselben Tests
sofort grün.

## Decision
1. **`src/Tests/test.runsettings`** setzt `RunConfiguration/EnvironmentVariables/
   USE_MEMORY_DB=true` — eine von VSTest selbst unterstützte Einstellung, kein
   Custom-Code. Sie landet im Environment des Test-Host-Prozesses, bevor irgendein
   Testcode (und damit `Persistence.dbConfig`) läuft.
2. **`src/Tests/Tests.fsproj`** referenziert die Datei über die MSBuild-Property
   `<RunSettingsFilePath>`, die `dotnet test` automatisch einliest — kein `--settings`-
   Flag nötig, kein Merken einer Umgebungsvariable durch die aufrufende Person.
   Verifiziert: `dotnet test src/Tests/Tests.fsproj` läuft mit sauberer Umgebung
   (`USE_MEMORY_DB` nicht gesetzt) grün, 798 von 798 lauffähigen Tests.
3. **`[<ModuleInitializer>]` als Alternative verworfen**: der F#-Compiler unterstützt
   dieses .NET-Attribut nicht (FS0202-Warnung, "wird nicht erreicht") — ein rein
   IL-basierter Trick, den F# nicht emittiert. `test.runsettings` ist die robuste,
   werkzeugunterstützte Lösung statt eines Sprach-Workarounds.
4. **`Main.fs`s `Environment.SetEnvironmentVariable` bleibt bestehen**, als zweite,
   redundante Absicherung für den Direktaufruf `dotnet exec Tests.dll` (Expecto ohne
   VSTest) — dort greift `test.runsettings` naturgemäß nicht.
5. **Die reale Entwicklungsdatenbank wurde NICHT von diesem Worker bereinigt.** Vor
   jedem Löschversuch griff der Sandbox-Berechtigungsfilter ("Cloud Storage Mass
   Delete") und verweigerte die Aktion — ein korrektes Stoppsignal für einen
   Worker-Agenten, der keine destruktive Operation an einer echten, mit Zugangsdaten
   benachbarten Datei ohne Freigabe ausführen darf. Eine Sicherungskopie liegt unter
   `~/my_apps/budgetbuddy-dev/budgetbuddy.db.pre-cleanup-backup-20260912-150615`.
   Betroffen (identifiziert, nicht entfernt): 106 Test-Konten (Präfix `01a095*`/
   `0195bb00*`), 365 Test-Buchungen (195 an Test-Konten, 170 "verwaiste" Buchungen auf
   den generischen Test-Konto-Ids `0195c000-...-001`/`...-002` aus
   `LedgerTests.fs`), sowie 11 Test-Budget-Schlüssel in `settings`
   (`ledger_mirror_server_knowledge_budget-*`) und ein vermutlich überschriebener
   `ledger_mirror_last_run`-Stand. Die 7 echten Konten und ~13.864 echten Buchungen
   sind unangetastet (Präfix `01a07852*`, Zeitstempel 2026-09-06, vor Beginn dieser
   Scheibe). Aufräumen bleibt Romans Entscheidung — siehe Backlog-Eintrag.

## Consequences
### Positive
- `dotnet test` ist ab sofort für **jede** Testdatei in diesem Repo sicher, nicht nur
  für die Ledger-Tests dieser Scheibe — die Regel "keine Produktionsdatenbank in
  Tests" gilt jetzt strukturell, nicht nur, wenn jemand daran denkt, die
  Umgebungsvariable von Hand zu setzen.
- Der Fund erklärt rückwirkend, warum reine Testanzahl-Erhöhungen scheinbar
  unabhängige Tests kippen konnten: es war nie Test-Parallelität, sondern
  akkumulierte Kollisionen in einer echten, nie zurückgesetzten Datei.

### Negative
- Die reale Entwicklungsdatenbank trägt bis auf Weiteres Testreste (siehe Punkt 5);
  ein erneuter "Jetzt spiegeln"-Lauf stellt `ledger_mirror_last_run` richtig, behebt
  aber nicht die verwaisten Test-Konten/-Buchungen und -Settings-Schlüssel.
- `test.runsettings` ist projektweit (alle Testdateien), nicht auf `ledger-b1xwe`
  beschränkt — bewusst, weil der Fehler selbst projektweit war.

## Alternatives considered
- **Umgebungsvariable nur dokumentieren** ("`USE_MEMORY_DB=true` vor `dotnet test`
  setzen"): verworfen — verlässt sich auf menschliches Erinnern bei jedem Aufruf,
  genau das Muster, das den Vorfall verursacht hat.
- **`[<ModuleInitializer>]`**: verworfen, vom F#-Compiler nicht unterstützt
  (Punkt 3).
- **Datenbank-Datei vollständig bereinigen**: nicht umgesetzt — vom
  Sandbox-Berechtigungsfilter blockiert und ohnehin eine Entscheidung, die Roman
  selbst treffen sollte, nicht ein Worker-Agent ohne Rückfrage.
