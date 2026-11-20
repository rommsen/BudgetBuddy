---
id: infrastructure-x7p2e
title: "Expecto wieder schnell: weniger testSequenced, lange Aufzählungen teilen, kurze Zeitgeber"
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
  Kern, er wartet mehr, als er rechnet.
- **Sequenzielle Listen:** 101 erkannte `testSequenced`-Listen mit 921 Fällen und zusammen rund 34 s (am 5.11. 67
  Aufrufe von `testSequenced`, heute 119). Expecto führt sie nacheinander aus, das ist rund die Hälfte der Wandzeit.
  Größte: „Targets anwenden: Abläufe (h2zfn)“ 5,4 s, „Verschieben: API-Grenze“ 3,6 s, „Zuweisen: API-Grenze“ 2,5 s,
  „Pause: Abläufe“ 1,7 s, „Zuweisen: Abläufe“ 1,6 s, mehrere „Schreib-Sperre“-Listen mit 0,6 bis 0,9 s.
- **Lange Einzelfälle** setzen die Untergrenze des parallelen Teils: „Budget führen: Abläufe (dgrp7); Reihenfolgen
  von Lesen und Senden; Jede Folge aus drei Schritten hält O1 bis …“ 16,2 s, „Tab-Daten: Generationsregel über alle
  Reihenfolgen“ 7,1 s, „Kassenbuch-Seite: Reduzierer; Über alle Reihenfolgen aus Tippen, Ablauf und Entfernen“ 4,9 s,
  „Regeln: Reihenfolge in jeder Reihenfolge“ 4,4 s, „Payee ändern: ein Aufruf zugleich“ 4,3 s.
- **Echte Wartezeiten** über `src/Tests/CmdTiming.fs`: „Kategorie im Detail: Der Zeitgeber der Karte meldet nach
  ihrer Dauer“ 6,0 s, „Bank trennen; die Rückfrage fällt nach der Wartezeit von selbst zurück“ 3,0 s, „Abrufen: der
  Takt beim Nachlesen läuft nach der Pause ab“ 3,0 s.

## What
Drei Hebel, in dieser Reihenfolge nach erwartetem Gewinn:

1. **`testSequenced` nur, wo Tests wirklich Zustand teilen.** Test-Doktrin Punkt 16: Isolation kommt aus eindeutigen
   Ids, Reihenfolge innerhalb einer Liste nur mit `testSequenced`. Für jede Liste prüfen, warum sie sequenziell ist
   (gemeinsame Zeile in der In-Memory-DB, globaler Zähler, Server-Log, Uhr), und wo der Grund fehlt oder sich über
   eindeutige Ids auflösen lässt, die Markierung entfernen. Wo ein Grund bleibt, steht er als Kommentar an der Liste.
2. **Lange Aufzählungen teilen.** Die Reihenfolgen-Tests nach dem Muster `moveWays` (Doktrin Punkt 20) so aufteilen,
   dass Expecto sie über die Kerne verteilt (etwa ein `testCase` je erstem Schritt oder je Block von Folgen). Das
   Orakel und die Menge der geprüften Folgen bleiben gleich.
3. **Kurze Zeitgeber.** Wo der getestete Code die Dauer als Parameter oder Konstante nimmt, im Test eine kurze Dauer
   einsetzen. Die Aussage „läuft nach der Dauer ab“ bleibt (CmdTiming misst weiter die vergangene Zeit).

## Acceptance criteria
- [ ] Messtabelle im Outcome, vorher und nachher auf derselben Maschine bei ähnlicher Last: Wandzeit von Expecto,
      CPU (user), Summe der sequenziellen Listen, die fünf längsten Fälle. Gemessen mit
      `USE_MEMORY_DB=true TZ=Europe/Berlin dotnet src/Tests/bin/Debug/net10.0/Tests.dll --junit-summary <datei>`.
- [ ] Jede Liste, die `testSequenced` behält, hat einen Kommentar mit dem Grund. Jede entfernte Markierung ist im
      Outcome mit Liste und Grund genannt, warum sie parallel sicher ist.
- [ ] Die Zahl der Fälle bleibt gleich oder wächst nur durch das Aufteilen. Keine Aussage fällt weg: Für jede
      geteilte Aufzählung steht im Outcome, dass die Menge der geprüften Folgen gleich geblieben ist.
- [ ] Zehn Expecto-Läufe hintereinander sind grün, einer davon unter Last (parallel zu `npm run test:scripts`).
      Flackernde Fälle stehen mit Name und Fehlermeldung im Outcome (bekannter Kandidat: `BudgetTakeoverTests.fs:529`
      „die Wartefunktion des Servers wartet wirklich“, siehe Nachtrag in infrastructure-mshaf).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Ziel ohne Messung geschätzt: Expecto bei 20 bis 25 s. Bleibt der Gewinn nach Hebel 1 unter 15 s, Hebel 2 und 3 nur
  noch für die drei längsten Fälle umsetzen.
- Paralleles Expecto neben dem Client-Strang ist infrastructure-pgpb6 in infrastructure-mshaf. Die dortige Annahme
  „nur noch rund 14 s wert“ gilt nach dieser Messung nicht mehr. Beides zusammen nicht in einer Task.
- Am 20.11. hat Roman den Layout-Sweep auf 412 und 1440 px verkleinert (512a1cf0), test:scripts von 288 auf 204 s.
