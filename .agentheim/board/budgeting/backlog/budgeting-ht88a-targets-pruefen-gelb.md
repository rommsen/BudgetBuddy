---
id: budgeting-ht88a
title: "Targets prüfen: Gelb nur am Titel „Gibst du mehr aus?“ und an den Balken, Kopf als ein neutraler Satz wie im Prototyp"
status: backlog
type: feature
context: budgeting
created: 2026-11-18
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, budget]
related_adrs: [0087]
related_research: []
prior_art: [budgeting-j8r4t]
---

## Why
Teil A aus budgeting-wnkgj (Roman, Prüfliste w01). Auf Targets prüfen steht zu viel Gelb: am PC große gelbe Zähler
„Hinweise“, obwohl sie auch Gruppen zählen, die selbst nicht gelb sind. Gelb heißt „braucht dich“ (Styleguide §2,
ADR 0087).

## What
Der Kopf der Seite wird ein Satz wie im Prototyp (`tcheck()`), klein und neutral, am Handy und am PC gleich. Gelb bleibt
nur, wo eine Kategorie mehr ausgibt als ihr Target.

## Acceptance criteria
- [ ] Am PC (ab 1024 px) gibt es keine großen Zähler „Hinweise“ und „Passt“ mehr (`TargetCheckView.fs:385`). Handy und
      PC zeigen denselben Satz: „Vergleich mit dem Schnitt der letzten 12 Monate. <n> Hinweise · <m> passen“, 13 px in
      `text-secondary`, Zahlen ohne eigene Farbe (Roman w02 a). Der Satz kommt aus einer reinen Funktion mit .NET-Test
      (Einzahl und Mehrzahl, null Hinweise).
- [ ] Gelb (`Colors.attention`) steht auf der Seite nur noch am Gruppentitel „Gibst du mehr aus?“, an den Balken über dem
      Target (`Stats.fs:458`) und in deren Legende. Die übrigen Gruppentitel behalten ihre Farben aus BB.
- [ ] Alles andere auf der Seite bleibt, wie BB es heute kann (aufklappbare Gruppen, „n von 12 Mon. darüber“, Panel
      „Anpassen …“, „monatlich anlegen“).
- [ ] Layout-Sweep grün mit den bestehenden Zuständen von Targets prüfen bei 412 und 1440 px.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten; Screenshot-Vergleich (412 und 1440 px) im
      Outcome. [human-eye]

## Notes
Vorlage: Prototyp Version 17 (https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg). Der Conductor legt die Datei und Screenshots je Ansicht (412 und 1440 px) in den Scratchpad und gibt Worker und Verifier die Pfade mit.
Abweichungen vom Prototyp, entschieden: Zahlen im Satz neutral statt gelb/grün (w02 a); aufklappbare Gruppen und das
Panel „Anpassen …“ bleiben (BB kann mehr). Bestandsaufnahme in budgeting-wnkgj.
