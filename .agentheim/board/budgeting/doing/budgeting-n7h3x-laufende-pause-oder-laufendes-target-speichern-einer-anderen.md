---
id: budgeting-n7h3x
title: "Laufende Pause oder laufendes Target-Speichern einer anderen Kategorie sperrt den gleichen Knopf ohne sichtbaren Grund"
status: doing
type: bug
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-f8w2n]
blocks: []
tags: [budget, target, pause]
related_adrs: [0080]
related_research: []
prior_art: [budgeting-f8w2n, budgeting-c2t9w]
---

## Why
Nach budgeting-f8w2n sperren sich Target und Pause nur noch je Kategorie. Gleichartige Aufrufe sperren weiter über alle Kategorien: läuft die Pause bei A, ist "Target pausieren" bei B gesperrt; läuft Speichern bei A, sind Speichern und Entfernen bei B gesperrt. Der Spinner steht bei A, bei B sieht man keinen Grund. Ursache: `PauseCall` und `TargetCall` (`src/Client/Components/Budget/Types.fs`) halten je nur einen laufenden Aufruf; ein zweiter überschriebe den ersten, dessen Antwort, Meldung und Neulesen (`pauseStartGeneration`, `applyPauseAnswered`) gingen verloren. Der QA-Review zu c2t9w hatte "nicht gesperrt" auch für eine Pause bei einer anderen Kategorie erwartet.

## What
Entschieden 2026-11-04 (Roman hat übertragen: "entscheide nach bestem Wissen", BB wird bald Quelle): die globale Sperre gleichartiger Aufrufe bleibt, `PauseCall` und `TargetCall` behalten je einen Platz. Keine Map je Kategorie, das wäre viel Umbau für einen seltenen Fall.

Neu: Sperrt ein laufender gleichartiger Aufruf einer **anderen** Kategorie den Knopf, steht darunter der Grund mit Namen der Kategorie des laufenden Aufrufs:
- "Target pausieren" gesperrt durch eine Pause bei A: "Wartet, bis die Pause bei <A> gespeichert ist."
- "Target speichern" oder "Target entfernen" gesperrt durch einen Target-Aufruf bei A: "Wartet, bis das Target bei <A> gespeichert ist."
Bei derselben Kategorie bleibt es beim Spinner am auslösenden Knopf (kein Satz). Die bestehenden Sätze aus budgeting-f8w2n (Sperre durch den andersartigen Aufruf derselben Kategorie) bleiben. Trifft beides zu, hat der Satz zum gleichartigen Aufruf Vorrang, es steht nur ein Satz.

Der Kategoriename kommt aus dem geladenen Monat; fehlt er (Kategorie nicht im gezeigten Monat), steht der Satz ohne Namen ("Wartet, bis die laufende Pause gespeichert ist." bzw. "Wartet, bis das laufende Target gespeichert ist.").

Alles als reine Funktionen in `Pause.fs` und `TargetEdit.fs` (Vorbild `pauseWaitHint`, `saveWaitHint`, `removeWaitHint`), die Views rufen sie nur auf.

## Acceptance criteria
- [ ] README budgeting per README_DELTA: globale Sperre gleichartiger Aufrufe mit Satz.
- [ ] Reine Tests mit zwei Kategorien: Pause bei A, Knopf bei B zeigt den Satz mit Name A; Target-Aufruf bei A, Speichern und Entfernen bei B zeigen den Satz mit Name A; dieselbe Kategorie zeigt keinen dieser Sätze; fehlender Name ergibt den Satz ohne Namen.
- [ ] Die Hinweissätze aus budgeting-f8w2n verhalten sich unverändert (bestehende Tests grün); der Walk der Rückfragen bleibt grün.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Screenreader außerhalb des Umfangs.