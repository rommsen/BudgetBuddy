---
id: budgeting-w5k2r
title: "Pause verschwindet, wenn beim Tipp ein Lesen des Monats schon läuft (Wechsel hin und zurück)"
status: backlog
type: bug
context: budgeting
created: 2026-11-04
completed:
depends_on: []
blocks: []
tags: [budget, pause, bug]
related_adrs: []
related_research: []
prior_art: [budgeting-m4v8t, budgeting-c6vn4, budgeting-e6p4r]
---

## Why
Aus budgeting-m4v8t. Ablauf: Monat geladen, `SelectMonth` nächster Monat, vor dessen Antwort `SelectMonth` zurück. Die alte Liste steht, kein Wechsel läuft (`monthChanging` falsch), `Freshness = MonthRead`, das Lesen des Rückwegs läuft noch. Jetzt `SetTargetPause`: der Aufruf startet mit der Generation dieses Lesens. Kommt die Pause-Antwort vor dem Lesen, sieht `pauseNeedsReread` nichts (Generation gleich, geladen, nicht veraltet). Danach kommt das Lesen an, das vor dem Speichern begann, und `applyLoaded` überschreibt die Pause bis zum nächsten Lesen. Die Monatspfeile sind während eines Wechsels nicht gesperrt, der Fall ist in der UI erreichbar. Der Walk in `src/Tests/TargetPauseClientTests.fs` erreicht ihn nicht: sein Ereignis "Monat" kommt nur, wenn `Displayed = Chosen`.

## What
Kaputt-Szenario (GIVEN/WHEN/THEN): GIVEN Wechsel hin und zurück, das Lesen des Rückwegs läuft, Tipp auf Pausieren WHEN die Pause-Antwort vor dem Lesen kommt und danach das Lesen ohne Pause THEN zeigt der Monat nach allen Lesevorgängen die Pause. Intakt-Szenario: ohne laufendes Lesen beim Tipp liest die Antwort nicht neu (bestehender Test "GIVEN Fehlbetrag WHEN Pause bestätigt ..."). Mögliche Wege: ein Lesen, das beim Start schon lief, im Modell erkennbar machen (etwa `MonthStale` auch bei `select`, Folgen für `Moves.fs:481` prüfen) oder `startPause` sperren, solange ein Lesen läuft. Den Walk um "Monat" während eines Wechsels erweitern.

## Acceptance criteria
- [ ] Regressionstest mit Bug-Kommentar, vor dem Fix rot.
- [ ] Der Walk kennt den Monatswechsel während eines laufenden Wechsels und bleibt grün.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.