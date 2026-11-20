---
id: infrastructure-x7p2e
title: "Expecto wieder schnell: testSequenced nur bei geteiltem Zustand, lange Aufzählungen teilen, Ziel höchstens 30 s"
status: backlog
type: chore
context: infrastructure
created: 2026-11-20
completed:
depends_on: []
blocks: []
tags: [captured, verify, gate, laufzeit, expecto, tests]
related_adrs: []
related_research: []
prior_art: [infrastructure-d1dwb, infrastructure-mshaf]
---

## Why
Schritt 2 des Gates (Expecto über die Tests.dll) brauchte nach infrastructure-d1dwb am 2026-11-05 14 s, am
2026-11-20 sind es 65 s. Das Gate dauert dadurch rund 6 statt 4 Minuten, bei jedem work-Lauf und jedem Verifier.
Roman hat am 20.11. gefragt, warum das Gate wieder so lange dauert, und die Task bestellt.

Messung 2026-11-20 (Expecto allein, Last um 8 auf 10 Kernen, JUnit-Bericht über `--junit-summary`):
- 5.495 Fälle, Summe der Einzelzeiten 175 s, Wandzeit 65 s, CPU (user) 75 s. Der Lauf nutzt im Schnitt gut einen
  Kern.
- **Sequenzielle Listen:** 101 erkannte `testSequenced`-Listen mit 921 Fällen und zusammen rund 34 s (am 5.11. 67
  Aufrufe von `testSequenced`, heute 119 in 47 Dateien). Expecto führt sie nacheinander aus, das ist rund die Hälfte
  der Wandzeit. Größte: „Targets anwenden: Abläufe (h2zfn)“ 5,4 s (`TargetsApplyTests.fs:360`), „Verschieben:
  API-Grenze“ 3,6 s (`MoveMoneyTests.fs:621`), „Zuweisen: API-Grenze“ 2,5 s (`MoveMoneyTests.fs:935`), „Pause:
  Abläufe“ 1,7 s (`TargetPauseTests.fs:219`), „Zuweisen: Abläufe“ 1,6 s, mehrere „Schreib-Sperre“-Listen mit 0,6 bis
  0,9 s. Die vier größten rufen je Testfall `initializeBudgetingSchema ()` auf der geteilten In-Memory-DB auf und
  zählen Zeilen über die Ids des Falls.
- **Lange Einzelfälle** setzen die Untergrenze des parallelen Teils: „Budget führen: Abläufe (dgrp7); Reihenfolgen
  von Lesen und Senden; Jede Folge aus drei Schritten hält O1 bis …“ 16,2 s, „Tab-Daten: Generationsregel über alle
  Reihenfolgen“ 7,1 s, „Kassenbuch-Seite: Reduzierer; Über alle Reihenfolgen aus Tippen, Ablauf und Entfernen“ 4,9 s,
  „Regeln: Reihenfolge in jeder Reihenfolge“ 4,4 s, „Payee ändern: ein Aufruf zugleich; über alle Reihenfolgen …“
  4,3 s.

Rechnung: Läuft alles parallel, liegt die Untergrenze bei rund 16 bis 18 s (längster Fall; 175 s Testzeit auf 10
Kernen), dazu der Start der Tests.dll.

## What
Zwei Hebel, in dieser Reihenfolge:

1. **`testSequenced` nur, wo Tests wirklich Zustand teilen** (Test-Doktrin Punkt 16: Isolation kommt aus eindeutigen
   Ids, Reihenfolge innerhalb einer Liste nur mit `testSequenced`). Für jede Liste den Grund bestimmen. Typische
   echte Gründe: ein Fall liest eine Tabelle ohne Filter auf seine Ids (Zählung über alles), ein globaler Zähler oder
   Schalter, das Server-Log (`testSequencedGroup "Server-Log"`), eine Schema-Initialisierung, die gleichzeitig nicht
   sicher ist. Wo der Grund fehlt, die Markierung entfernen. Wo er sich billig auflösen lässt (Abfrage auf die Ids des
   Falls einschränken, eigene Ids statt geteilter Konstanten), ihn auflösen und die Markierung entfernen. Wo ein Grund
   bleibt, steht er als Kommentar über `testSequenced`.
2. **Lange Aufzählungen teilen.** Die fünf längsten Fälle oben (Reihenfolgen-Tests nach dem Muster `moveWays`,
   Doktrin Punkt 20) so in mehrere Testfälle zerlegen, dass Expecto sie über die Kerne verteilt, etwa ein `testCase`
   je erstem Schritt oder je Block von Folgen. Orakel und Menge der geprüften Folgen bleiben gleich.

Nicht Teil der Task:
- Zeitgeber-Tests über `CmdTiming` (bis 6 s echte Wartezeit) laufen im parallelen Teil und enden vor dem längsten
  Fall. Ihre Dauern sind Konstanten im Produktionscode (`categoryCardDurationMs`, `bankDisconnectConfirmMs`); sie
  nur für Tests umzubauen lohnt nicht.
- Expecto parallel zum Client-Strang (infrastructure-pgpb6 in infrastructure-mshaf).
- `verify.sh` selbst bleibt unverändert.

## Acceptance criteria
- [ ] Messtabelle im Outcome, vorher und nachher auf derselben Maschine bei Last unter 10 (`uptime` im Outcome):
      Wandzeit von Expecto, CPU (user), Zahl und Summe der sequenziellen Listen, die fünf längsten Fälle. Gemessen mit
      `USE_MEMORY_DB=true TZ=Europe/Berlin dotnet src/Tests/bin/Debug/net10.0/Tests.dll --junit-summary <datei>`,
      je Seite mindestens zwei Läufe.
- [ ] Expecto braucht nachher höchstens 30 s Wandzeit (Median der Läufe aus der Messtabelle).
- [ ] Jede Liste, die `testSequenced` oder `testSequencedGroup` behält, hat direkt darüber einen Kommentar mit dem
      Grund (was geteilt wird). Jede entfernte Markierung steht im Outcome mit Datei, Liste und dem Grund, warum sie
      parallel sicher ist.
- [ ] Die Zahl der Fälle bleibt gleich oder wächst nur durch das Aufteilen. Für jede geteilte Aufzählung steht im
      Outcome, dass die Menge der geprüften Folgen gleich geblieben ist (Zahl der Folgen vorher und nachher).
- [ ] Zehn Expecto-Läufe hintereinander sind grün, zwei davon parallel zu `npm run test:scripts`. Ein Fall, der in
      einem Lauf rot war, steht mit Name und Fehlermeldung im Outcome und ist behoben oder bekommt den Grund für
      `testSequenced` (bekannter Kandidat: `BudgetTakeoverTests.fs:529` „die Wartefunktion des Servers wartet
      wirklich“, Nachtrag in infrastructure-mshaf).
- [ ] Kein Produktionscode unter `src/Server`, `src/Client` oder `src/Shared` geändert.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Stop-Loss: Liegt Expecto schon nach Hebel 1 bei höchstens 30 s, wird Hebel 2 nur noch für den 16-s-Fall umgesetzt
  und der Rest als Zeile in die Kann-Sammlung infrastructure (infrastructure-hgbey) geschrieben.
- Wie Expecto sequenzielle Listen ausführt: Fälle unter `testSequenced` laufen nicht parallel zueinander und nicht
  parallel zum Rest. Vor dem Umbau einmal prüfen, ob das in der verwendeten Expecto-Version so ist (Wandzeit mit und
  ohne eine große Liste vergleichen), damit die Rechnung oben stimmt.
- Unter Last (über 20) blieb Expecto früher mehrfach hängen (Nachtrag 2026-11-06 in infrastructure-mshaf). Hängt ein
  Lauf, ist das ein Befund für das Outcome und kein Grund, Markierungen ungeprüft zurückzusetzen.
- Am 20.11. hat Roman den Layout-Sweep auf 412 und 1440 px verkleinert (512a1cf0), test:scripts von 288 auf 204 s.
- Refine 2026-11-20: Hebel 3 (Zeitgeber kürzen) gestrichen, siehe „Nicht Teil der Task“.
