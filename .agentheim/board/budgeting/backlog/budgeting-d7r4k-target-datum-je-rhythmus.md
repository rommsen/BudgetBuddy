---
id: budgeting-d7r4k
title: "Target-Datum je Rhythmus: kein Datum bei der Woche, beim Monat und Jahr als \"fällig am\""
status: backlog
type: feature
context: budgeting
created: 2026-11-04
completed:
depends_on: []
blocks: []
tags: [budget, target, datum, ux]
related_adrs: [0072]
related_research: [ynab-api-targets-zuweisung-2026-10-25, ynab-reihenfolge-monate-target-schreiben-2026-11-02]
prior_art: [budgeting-xp46a, budgeting-k8wq3]
---

## Why
Roman (2026-11-04): "Warum gibt es überhaupt bei Target jetzt ein Datum? Was genau sagt mir das Datum? Insbesondere bei Woche und Monat und Jahr."

Das Formular im Tab Target zeigt das `DateField` bei allen drei Rhythmen gleich, weil ADR 0072 Punkt 2 "jeweils optional mit Datum" festlegt. YNABs `goal_target_date` bedeutet aber je Kadenz etwas anderes:

- **Jahr** (cadence 13): Fälligkeit. YNAB verteilt den Betrag auf die Monate bis dahin, danach wiederholt es sich jährlich. In Romans Budget 28 Targets.
- **Monat** (cadence 1): Fälligkeit wie beim Jahr. YNAB verteilt den Betrag auf die Monate bis zum Datum (Spike budgeting-s5n2w: 18.000 € bis 2030-01-01 ergibt 461,54 € im Monat, dieselbe Zahl wie jährlich). Danach beginnt der Zyklus nach YNABs Kadenz-Logik von vorn, das ist nicht beobachtet. Bei "alle N Monate" (4 Targets mit frequency 3) ist es die nächste Fälligkeit. Die alten MF-Targets tragen dort Startdaten von 2019.
- **Woche** (cadence 2): keine belegte Bedeutung. Maßgeblich ist der Wochentag (`goal_day`), den BB nicht setzen kann.

## What
Vorschlag, zu verfeinern:

- Woche: kein Datumsfeld beim Anlegen und Wechseln. Ein vorhandenes Datum bleibt lesbar und lässt sich entfernen.
- Monat: Feld beschriftet als "fällig am", mit Hinweis, dass das Target danach monatlich weiterläuft.
- Jahr: Feld beschriftet als "fällig am".
- Anzeige des gelesenen Targets und Historientext ("Datum 01.06.2027") je Rhythmus gleich benennen.
- Nachtrag zu ADR 0072 Punkt 2.

## Offene Fragen an Roman
1. Monat mit Datum: als Sparziel anbieten (es rechnet die Rate bis zum Datum) oder streichen, weil es danach jeden Monat den vollen Betrag verlangt?
2. Ist "alle 3 Monate ab" für dich eine gebrauchte Form, oder reichen Monat und Jahr?

## Notes
Hängt mit budgeting-s5n2w zusammen: ein einmaliges Sparziel bis zu einem Datum wäre die vierte Bedeutung des Datums.
