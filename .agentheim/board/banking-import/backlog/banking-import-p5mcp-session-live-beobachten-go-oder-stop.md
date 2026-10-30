---
id: banking-import-p5mcp
title: "Roman beobachtet die comdirect-Session live über mindestens 2 Stunden und entscheidet Go oder Stop"
status: backlog
type: spike
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-cx4b7]
blocks: [banking-import-tshq9, infrastructure-jsys4]
tags: [comdirect, session, owner-run, live]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Nur Roman kann eine echte TAN bestätigen. Dieser Task entscheidet, ob die Folge-Tasks gebaut
werden: ADR 0071 gilt unter dem Vorbehalt dieses Beweises.

## What
Sonde aus `banking-import-cx4b7` laufen lassen, gern über Nacht. Das Ergebnis kommt in die README
von `banking-import` und ins Task-Outcome, nicht in `diary/development.md` (eingefroren).

Zu beantworten:
- Wie lange trägt die Session nur per Refresh?
- Bleibt die Session-ID samt `x-http-request-info` nach einem Refresh gültig?
- Klappt die Wiederaufnahme in einem neuen Prozess?
- Wie lange lebt das Refresh-Token wirklich, und wie sieht die Fehlerantwort bei Ablauf aus?
- Gibt es einen Hinweis auf eine Maximaldauer?

**Stop-Loss:** if, mid-spike, the mitigation is already known and cheap, record it and stop.

Abbruchwirkung:
- **Go:** mindestens 2 h und mindestens ein erfolgreicher Umsatzabruf je Stunde ohne weitere TAN.
  Die Folge-Tasks laufen.
- **Stop:** die Session stirbt in zwei Versuchen vor 1 h, oder der Refresh verlangt eine TAN. Die
  Folge-Tasks entfallen, ADR 0071 wird ersetzt.
- **Dazwischen:** Roman entscheidet.
- Wiederaufnahme scheitert bei sonst Go: `banking-import-z4h6p` entfällt.
- Maximaldauer unter dem Zeitplan-Intervall: `banking-import-t4raw` entfällt oder wird neu bewertet.
- Abweichende Fehlerform: Klassifikation aus `banking-import-cx4b7` nachziehen, bevor
  `banking-import-tshq9` startet.

## Acceptance criteria
- [ ] Die README von `banking-import` enthält den Befund mit den beobachteten Laufzeiten.
- [ ] `diary/development.md` ist unverändert.
- [ ] Das Protokoll liegt vor, und Go oder Stop steht im Outcome. [human-eye]

## Notes
Owner-Task: ein Worker kann ihn nicht ausführen, weil die Push-TAN auf Romans Handy bestätigt wird.
Nicht per `work` starten. Während der Beobachtung nicht parallel aus einer anderen Umgebung
anmelden, ob das die Session beendet, ist unbekannt.
