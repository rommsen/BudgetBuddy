---
id: budgeting-d7r4k
title: "Target-Datum je Rhythmus: kein Datum bei der Woche, beim Monat als \"ab\", beim Jahr als \"fällig am\""
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
- **Monat** (cadence 1): Startdatum des Zyklus. Trägt nur bei "alle N Monate ab ..." (4 Targets mit frequency 3). Bei einem schlichten Monats-Target ohne sichtbare Wirkung. Die alten MF-Targets tragen dort Startdaten von 2019.
- **Woche** (cadence 2): keine belegte Bedeutung. Maßgeblich ist der Wochentag (`goal_day`), den BB nicht setzen kann.

## What
Vorschlag, zu verfeinern:

- Woche: kein Datumsfeld beim Anlegen und Wechseln. Ein vorhandenes Datum bleibt lesbar und lässt sich entfernen.
- Monat: Feld beschriftet als "ab" (Startdatum), oder ganz weg, wenn keine Wirkung belegt ist.
- Jahr: Feld beschriftet als "fällig am".
- Anzeige des gelesenen Targets und Historientext ("Datum 01.06.2027") je Rhythmus gleich benennen.
- Nachtrag zu ADR 0072 Punkt 2.

## Offene Fragen an Roman
1. Monat: Datum als "ab" behalten oder streichen?
2. Ist "alle 3 Monate ab" für dich eine gebrauchte Form, oder reichen Monat und Jahr?

## Notes
Hängt mit budgeting-s5n2w zusammen: ein einmaliges Sparziel bis zu einem Datum wäre die vierte Bedeutung des Datums.
