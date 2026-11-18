---
id: budgeting-ht88a
title: "Targets prüfen: Gelb nur am Titel „Gibst du mehr aus?“ und an den Balken, Kopf als ein neutraler Satz wie im Prototyp"
status: done
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

## Outcome

Der Kopf von Targets prüfen ist am Handy und am PC gleich: `PageHeader.top` mit „Zurück“ und „Targets prüfen“ (wie der Prototyp `tcheck()` und das Beispiel „Kopf einer Unterseite“ im Styleguide), darunter ein Satz 13 px (`FontSizes.compact`) in `text-secondary` ohne eigene Farbe für die Zahlen (`target-check-intro`). Der Satz kommt aus der reinen Funktion `checkHeadText` in `src/Client/Components/Budget/TargetCheck.fs`; sie ersetzt `checkIntroText` und `checkCountsText`. Die Karte mit den großen Zählern „Hinweise“ und „Passt“ am PC (`counter`) und der zweite Handy-Kopf mit Monat und gelb/grünen Zahlen sind entfernt (`src/Client/Components/Budget/TargetCheckView.fs`, `header`). `checkCounts` bleibt (auch die Leiste Monatserster nutzt es).

Gelb (`Colors.attention`) steht in `TargetCheckView.fs` nur noch an `checkGroupTitleClass` (SpendsMore) und an der Legende „gelb: über dem Target“; die Balken über dem Target färbt `Stats.sparkToneClass` (`SparkOver`). Weniger bleibt Blau, Passt Grün, nie und Kein Target Textfarbe. Aufklappbare Gruppen, „n von 12 Mon. darüber“, Panel „Anpassen …“ und „monatlich anlegen“ sind unverändert.

Test: `src/Tests/TargetCheckClientTests.fs`, „Der Satz im Kopf: Vergleich mit dem Schnitt, Hinweise und passen in Einzahl und Mehrzahl“ (Mehrzahl, Einzahl, null Hinweise, null passen); vor der Umsetzung rot mit leerem Ergebnis. Der alte Test auf `checkIntroText`/`checkCountsText` ist mit den Funktionen entfallen. Farben sind nach Doktrin Punkt 5 und 6 nicht im Browser getestet. Der Styleguide-Absatz zu Targets prüfen (`standards/frontend/styleguide.md`) beschreibt den neuen Kopf.

`./verify.sh` im Worktree: VERIFY: PASS (dotnet 5149 grün, node 348 grün), darin der Layout-Sweep mit den Zuständen `targets-pruefen` und `targets-pruefen-anpassen`. Ein erster Lauf unter hoher Last (load 34, parallele Worktrees) brach im Sweep (Zeitlimit 120 s) und in `[flow:bank-trennen]` ab („element is not stable“); beide waren einzeln grün, der zweite volle Lauf war grün. README-Delta mit `npm run check:readme-delta -- --bc budgeting` geprüft: Exit 0, „Ganz nachlesen: keine Einträge“. Das Delta zieht im Bullet **Targets prüfen** den Kopf und die veralteten Farbnamen (Orange, Teal, `Colors.calmBlue`, `Colors.calmViolet`) auf den Stand nach ADR 0087 nach.

Screenshot-Vergleich (Daten aus einer Kopie der Entwicklungsdatenbank, nur im Scratchpad):
- BB: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/24b8079f-2912-4751-9c18-a9bb18d50825/scratchpad/shots-bb/budgeting-ht88a-1440.png und /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/24b8079f-2912-4751-9c18-a9bb18d50825/scratchpad/shots-bb/budgeting-ht88a-390.png
- Prototyp: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/24b8079f-2912-4751-9c18-a9bb18d50825/scratchpad/proto/shots/tcheck-1440.png und /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/24b8079f-2912-4751-9c18-a9bb18d50825/scratchpad/proto/shots/tcheck-412.png
- Kopf: Zurück und Titel in einer Zeile, darunter der Satz, wie der Prototyp. Zahlen neutral statt gelb/grün (w02 a, gewollt).
- Abweichungen, alle in der Task genannt oder BB-Bestand: aufklappbare Gruppen mit Regelsatz statt Erklärsatz, „n von 12 Mon. darüber“, Panel „Anpassen …“, „monatlich anlegen“, vier Spalten am PC.
- Grenze: `scripts/screenshot-route.mjs` nimmt das Handy fest mit 390 px auf, nicht 412 px; der Layout-Sweep prüft 412 px. Im 390-px-Bild steht mitten auf der Seite ein Sheet „Abrufen“ (Fixed-Element im Ganzseiten-Bild), das hat mit dieser Task nichts zu tun.
- [human-eye] Der Blick neben die Skizze bleibt für Roman.
