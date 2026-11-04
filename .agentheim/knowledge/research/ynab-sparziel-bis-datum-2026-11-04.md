---
title: "YNAB-API: Sparziel bis zu einem Datum anlegen und ändern"
date: 2026-11-04
task: budgeting-s5n2w
related_research: [ynab-api-targets-zuweisung-2026-10-25, ynab-reihenfolge-monate-target-schreiben-2026-11-02]
---

# YNAB-API: Sparziel bis zu einem Datum anlegen und ändern

## Frage
Roman will "18.000 € bis zum 01.01.2030" als Target. BB legt bisher nur NEED über `goal_frequency`
an (ADR 0072 Punkt 2). Entsteht ein einmaliges Sparziel, wenn BB `goal_target` und
`goal_target_date` ohne `goal_frequency` sendet? Lässt es sich ändern? Was macht YNAB mit TB?

## Methode
Echte Aufrufe am 2026-11-04 mit `YNAB_TOKEN`, 33 Aufrufe, nur gegen das Budget "Test". Ein
Wegwerf-Skript verweigert jede Schreibung mit einer anderen Budget-Id. Romans Budget wurde nicht
gelesen und nicht geschrieben. Gelesen wurde nach jedem Schritt `GET /budgets/{id}/categories/{id}`
im Monat 2026-11. Beträge in Milliunits, Kategorie-Guthaben überall 0.

## Ergebnis in Kürze
Ein einmaliges Sparziel lässt sich über die API nicht anlegen. Jeder Weg ergibt `goal_cadence` 1
(monatlich) oder 13 (jährlich), nie 0 (ohne Wiederholung). Bis zum Datum rechnet YNAB aber genau
die Sparrate: 18.000 € bis 2030-01-01 ergibt im November 2026 einen Fehlbetrag von 461,54 € und
39 Monate. Das ist bei monatlich und jährlich mit Datum dieselbe Zahl.

## Beobachtungen

| Schritt | Aufruf | Ergebnis |
|---|---|---|
| F1 | Kategorie ohne Target, `goal_target: 18000000, goal_target_date: 2030-01-01` in **einem** Aufruf | 200. NEED, cadence 1, frequency 1, whole true, Datum gesetzt, `goal_under_funded` 461540, `goal_months_to_budget` 39 |
| F2 | erst nur `goal_target`, dann nur `goal_target_date` | nach Schritt 1 NEED cadence 1 ohne Datum, Fehlbetrag 18000000; nach Schritt 2 wie F1 |
| F3a | auf F1 nur `goal_target: 20000000` | Form bleibt, Fehlbetrag 512830, 39 Monate |
| F3b | nur `goal_target_date: 2029-07-01` | Form bleibt, Fehlbetrag 606070, 33 Monate |
| F3c | `goal_target_date: null` | Form bleibt ohne Datum, Fehlbetrag 20000000, 1 Monat |
| F4 | TB, nur `goal_target: 5000000`, dann nur `goal_target_date` | beides 200, Art bleibt TB, Datum wird gespeichert, Fehlbetrag 0, Monate 0. Es entsteht kein TBD |
| F5 | `goal_target, goal_frequency: yearly`, dann `goal_target_date` | cadence 13 mit Datum, Fehlbetrag 461540, 39 Monate, wie F1 |

Nebenbefund zu F1: Ohne `goal_frequency` nimmt YNAB Betrag und Datum in einem Aufruf an. Der
400er aus ADR 0072 Punkt 5 gilt nur für `goal_frequency` zusammen mit `goal_target_date`.

## Was offen bleibt
- Was YNAB **nach** dem Datum mit cadence 1 oder 13 macht, lässt sich heute nicht beobachten. Nach
  YNABs Kadenz-Logik beginnt das Ziel von vorn: monatlich jeden Monat 18.000 €, jährlich jedes Jahr.
- Wie die YNAB-Oberfläche cadence 1 mit Datum benennt ("monatlich", "Custom"), hat niemand
  angesehen. Das braucht Romans Auge im Budget Test.
- Ob es in YNAB cadence 0 mit Datum gibt (Custom ohne Wiederholung) und wie die API es liefert:
  Roman müsste es im Budget Test von Hand anlegen, dann lässt es sich lesen. Über die API sind
  `goal_cadence` und `goal_cadence_frequency` nicht schreibbar (Bericht vom 2026-11-02).
- TB mit Datum: YNAB speichert das Datum, rechnet aber nichts damit. Ob die Oberfläche es zeigt, ist offen.

## Empfehlung
BB kann ein Sparziel bis zu einem Datum heute als NEED monatlich oder jährlich mit Datum anlegen
(ein Aufruf ohne `goal_frequency` ergibt monatlich mit Datum). Die Sparrate bis zum Datum stimmt,
nach dem Datum müsste Roman das Target entfernen. Ein echtes einmaliges Ziel geht nur über ein in
YNAB angelegtes Target oder über ein Target, das BB selbst führt (Frage aus Stufe 3).

## Budget Test nach dem Spike
Dining out, Phone & Internet und Utilities sind wieder ohne Target. Medical expenses steht wieder
bei TB, target 0, ohne Datum. Geändert geblieben: `goal_creation_month` von Medical expenses steht
auf 2026-11 (vorher leer).

## Quellen
- Echte Aufrufe am 2026-11-04, Skripte und Aufrufprotokoll im Scratchpad, nicht im Repo
- Vorarbeit: `research/ynab-api-targets-zuweisung-2026-10-25.md`,
  `research/ynab-reihenfolge-monate-target-schreiben-2026-11-02.md`
