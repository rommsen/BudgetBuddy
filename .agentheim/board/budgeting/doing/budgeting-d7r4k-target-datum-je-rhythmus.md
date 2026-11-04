---
id: budgeting-d7r4k
title: "Target-Datum je Rhythmus: kein Datum bei der Woche, beim Monat und Jahr als \"fällig am\""
status: doing
type: feature
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-c2t9w]
blocks: []
tags: [budget, target, datum, ux]
related_adrs: [0072]
related_research: [ynab-sparziel-bis-datum-2026-11-04, ynab-api-targets-zuweisung-2026-10-25, ynab-reihenfolge-monate-target-schreiben-2026-11-02]
prior_art: [budgeting-xp46a, budgeting-k8wq3]
---

## Why
Roman (2026-11-04): "Warum gibt es überhaupt bei Target jetzt ein Datum? Was genau sagt mir das Datum? Insbesondere bei Woche und Monat und Jahr."

Das Formular im Tab Target zeigt das `DateField` bei allen drei Rhythmen gleich, weil ADR 0072 Punkt 2 "jeweils optional mit Datum" festlegt. YNABs `goal_target_date` bedeutet aber je Kadenz etwas anderes:

- **Jahr** (cadence 13): Fälligkeit. YNAB verteilt den Betrag auf die Monate bis dahin, danach wiederholt es sich jährlich. In Romans Budget 28 Targets.
- **Monat** (cadence 1): Fälligkeit wie beim Jahr. YNAB verteilt den Betrag auf die Monate bis zum Datum (Spike budgeting-s5n2w: 18.000 € bis 2030-01-01 ergibt 461,54 € im Monat, dieselbe Zahl wie jährlich). Danach beginnt der Zyklus nach YNABs Kadenz-Logik von vorn, das ist nicht beobachtet. Bei "alle N Monate" (4 Targets mit frequency 3) ist es die nächste Fälligkeit. Die alten MF-Targets tragen dort Startdaten von 2019.
- **Woche** (cadence 2): keine belegte Bedeutung. Maßgeblich ist der Wochentag (`goal_day`), den BB nicht setzen kann.

## What
Entschieden (Roman hat die Entscheidung am 2026-11-04 übertragen, Nachtrag zu ADR 0072 vom 2026-11-04):

- **Woche:** beim Anlegen und beim Wechsel auf wöchentlich kein Datumsfeld (`src/Client/Components/Budget/TargetEditView.fs`, `Input.groupSimple "Datum"`). Ein gelesenes Datum eines wöchentlichen Targets bleibt sichtbar und lässt sich entfernen. Wechselt der Entwurf auf Woche und trägt ein Datum, fällt das Datum aus dem Entwurf (reine Funktion in `TargetEdit.fs`), damit kein `goal_target_date` für ein wöchentliches Target hinausgeht.
- **Monat und Jahr:** das Feld heißt "fällig am" (Label und `aria-label` des `DateField`). Bei Monat steht unter dem Feld der Satz "Danach läuft das Target jeden Monat weiter." (`ListRoles.secondary`), nur wenn ein Datum gesetzt ist.
- **Lesende Anzeige** (vergangene und künftige Monate, `figure "target-date-text" "Datum"`): Beschriftung "fällig am" bei Monat und Jahr, sonst "Datum".
- **Historie:** Texte bleiben ("Datum 01.06.2027"), kein Umbau.
- "Alle n Monate" und andere nicht herstellbare Rhythmen bleiben, wie sie sind.

## Acceptance criteria
- [ ] Reine Funktion (Client, unter .NET getestet): bei Rhythmus Woche ist kein Datumsfeld vorgesehen; ein Entwurf, der auf Woche wechselt, verliert sein ungespeichertes Datum; ein gelesenes Datum eines wöchentlichen Targets bleibt entfernbar.
- [ ] Der Auftrag beim Anlegen wöchentlich trägt nie ein Datum (Client-`update`-Test), auch wenn vorher bei Monat ein Datum getippt war.
- [ ] Label "fällig am" bei Monat und Jahr, "Datum" sonst, als reine Funktion getestet; der Hinweissatz bei Monat mit Datum ebenso.
- [ ] `/styleguide` zeigt nichts Neues (keine DS-Änderung nötig); falls doch, Styleguide nachziehen.
- [ ] README budgeting (Target-Änderung) per README_DELTA: Datum je Rhythmus.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Hängt an budgeting-c2t9w (gleiche Dateien im Target-Formular). Spike-Ergebnis: `.agentheim/knowledge/research/ynab-sparziel-bis-datum-2026-11-04.md`. Ein Sparziel, das von selbst endet, kommt erst mit BB als führendem System.
