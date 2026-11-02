---
id: budgeting-g8s3w
title: "Budget-Seite neu: Liste und Inspektor mit vier Tabs, freie Monate, Kategorie-Reihenfolge, Filter und Gelb für fehlt"
status: backlog
type: feature
context: budgeting
created: 2026-11-02
completed:
depends_on: [design-system-001, budgeting-q7m2c, budgeting-v4ht9]
blocks: [budgeting-c5w2n]
tags: [budget, inspektor, monate, reihenfolge]
related_adrs: [0005, 0049, 0065]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-k7wmd, budgeting-m1bce, budgeting-et7z8, budgeting-kjsht, budgeting-rxxb4]
---

## Why
Task 1 von 5 des Gesamtkonzepts (`docs/ux/budget/konzept.md`). Die Seite war eine Machbarkeitsprobe: Man sieht nicht, was klickbar ist, und die Kategorien stehen nicht in Romans Reihenfolge.

## What
- Liste mit Spalten Kategorie, Target, Zugewiesen, Aktivität, Verfügbar. Eine Zeile wählt die Kategorie.
- Inspektor am PC rechts, am Handy `BottomSheet`, beide mit fester Höhe und den Tabs Geld, Buchungen, Historie, Target. Der Tab Buchungen übernimmt das Kategorie-Detail (budgeting-m1bce). Die übrigen Tabs zeigen vorerst den heutigen Stand, der bisherige Verschieben-Dialog bleibt über den Tab Geld erreichbar.
- Kategorie wechseln ohne Schließen: Pfeiltasten am PC, `‹ ›` im Sheet.
- Freie Monate mit `‹ Monat ›` statt der drei Knöpfe, Vertrag mit Monatswert statt `BudgetMonthChoice`, Grenzen laut budgeting-q7m2c.
- Kategorie-Reihenfolge laut Empfehlung aus budgeting-q7m2c.
- Filter Alle, Fehlt, Rot neben der Suche. `fehlt` in Gelb.
- Neuer Token Gelb, Knopf-Regel aus budgeting-v4ht9 im Styleguide und in `/styleguide`.

## Acceptance criteria
Werden in der taktischen Modellierung nach budgeting-q7m2c geschärft.
- [ ] Prüfskript-Abläufe zu Monaten und Filtern gelten als Szenarien mit Test auf Heimat-Ebene.
- [ ] Ungültiger Monat wird an der API-Grenze abgelehnt, die Endpoint-Negativtabelle ist ergänzt.
- [ ] Budget-Fixtures in `scripts/*.test.mjs` sind mitgezogen.
- [ ] Browser-Wächter: Inspektor und Sheet behalten beim Tab-Wechsel ihre Höhe, Sheet-Wechsel mit `‹ ›` ohne Schließen.
- [ ] Die Seite entspricht dem Konzept am PC und am Handy. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
