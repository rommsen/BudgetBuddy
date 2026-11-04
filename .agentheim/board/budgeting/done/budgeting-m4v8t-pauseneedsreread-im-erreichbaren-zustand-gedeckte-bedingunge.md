---
id: budgeting-m4v8t
title: "pauseNeedsReread: im erreichbaren Zustand gedeckte Bedingungen streichen oder benennen"
status: done
type: chore
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-n7h3x]
blocks: []
tags: [budget, pause, test]
related_adrs: []
related_research: []
prior_art: [budgeting-c6vn4, budgeting-e6p4r]
---

## Why
Aus budgeting-e6p4r: die Gegenprobe "`|| monthChanging model` in `pauseNeedsReread` streichen" bleibt grün, auch im Walk. Jeder Monatswechsel hebt die Generation seit dem Start des Aufrufs, `generationMoved` deckt den Fall. c6vn4 hatte dasselbe schon für `generationMoved` und `notLoaded` gegenüber `MonthStale` festgestellt (QA-Review c6vn4, Kann-Punkt). Bedingungen ohne Test, der sie trägt, sind nach Doktrin Punkt 6 ungeprüft.

## What
In `src/Client/Components/Budget/Pause.fs` entscheiden: die vier Teilbedingungen von `pauseNeedsReread` auf die nötigen kürzen oder jede mit einem Kommentar versehen, warum sie als Absicherung bleibt. Der Walk in `src/Tests/TargetPauseClientTests.fs` muss grün bleiben.

## Acceptance criteria
- [ ] Jede verbliebene Teilbedingung hat einen Test, der sie gestrichen rot macht, oder einen Kommentar, der sie als Absicherung benennt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
## Outcome

`pauseNeedsReread` in `src/Client/Components/Budget/Pause.fs` hat jetzt drei Teilbedingungen hinter `shownMonth model = Some month`: `MonthStale`, `generationMoved` und `notLoaded`. `|| monthChanging model` ist gestrichen. Begründung im Doc-Kommentar: beim Start zeigt die Seite den Monat des Aufrufs (`startPause` verlangt `Success` und keinen Wechsel), und jeder Weg zu einem Wechsel (`select`, `applyFrameRefreshed`, `applyFrameLoaded`, `beginLoad`) hebt die Generation. Ein Wechsel ohne gestiegene Generation ist damit nicht erreichbar. Nur `enterPage` (frischer Eintritt) setzt `ChosenMonth = None` ohne Generation, das führt aber nur vom Monat des Aufrufs weg, dann greift schon die erste Bedingung.

Die Feststellung aus c6vn4, `generationMoved` und `notLoaded` seien von `MonthStale` gedeckt, stimmt nicht in jedem erreichbaren Zustand: `select` setzt kein `MonthStale`. Zwei neue Tests in `src/Tests/TargetPauseClientTests.fs` belegen das:
- "GIVEN Pause läuft, Wechsel in den nächsten Monat und zurück vor dessen Antwort WHEN die Pause-Antwort kommt THEN wird der Monat noch einmal gelesen": kein Wechsel läuft, `MonthRead`, nur die Generation zeigt das Lesen. Trägt `generationMoved`.
- "GIVEN Pause läuft, ein schon laufendes Lesen scheitert WHEN die Pause-Antwort kommt THEN wird der Monat noch einmal gelesen": hin und zurück, Tipp, das Lesen des Wechsels scheitert (`Loaded` mit Fehler bei gleicher Generation), dann die Antwort. Trägt `notLoaded`.

Gegenproben (Zeile geändert, `Target pausieren im Client` gelaufen, Datei per `cmp` exakt zurückgesetzt):
- M1: `model.Freshness = MonthStale ||` gestrichen: rot ("Ein Neu-Lesen, das vor der Pause begann, wird nach der Antwort wiederholt" und der Walk).
- M2: `|| generationMoved` gestrichen: rot (neuer Test Hin und zurück vor der Antwort, der e6p4r-Regressionstest zum Wechsel zurück, der Walk).
- M3: `|| notLoaded` gestrichen: rot (neuer Test zum gescheiterten Lesen).
- M4: `shownMonth model = Some month` durch `true` ersetzt: rot (1 Test, "Während eines Wechsels in einen anderen Monat liest die Pause-Antwort nicht neu").
"Kein Überlebender" gilt für M1 bis M4. Der gestrichene Term `monthChanging` war der Überlebende M1 aus e6p4r. Walk und die beiden e6p4r-Regressionstests bleiben grün.

Nebenbefund: Wählt man einen anderen Monat und vor dessen Antwort wieder den Monat zurück und tippt dann auf Pausieren, läuft beim Start schon ein Lesen, das vor dem Speichern begann. Kommt die Pause-Antwort vor diesem Lesen, nimmt seine Antwort die Pause zurück; keine Bedingung sieht das (`MonthRead`, gleiche Generation, geladen, kein Wechsel). Der Walk erreicht den Fall nicht, weil sein Ereignis "Monat" nur ohne laufenden Wechsel kommt. Nicht hier gefixt, Folge-Task budgeting-w5k2r.

`./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3806 grün, node 300 grün), Log in `verify-budgeting-m4v8t.log`. Kein Live-Aufruf.
