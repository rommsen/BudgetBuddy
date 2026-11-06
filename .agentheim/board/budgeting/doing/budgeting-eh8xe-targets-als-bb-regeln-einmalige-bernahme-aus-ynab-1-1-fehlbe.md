---
id: budgeting-eh8xe
title: "Targets in BB führen: einmalige Übernahme aus YNAB, Target ändern schreibt nur in BB mit Befehls-Id, Wochentag setzbar, Pause nur in BB, Rückbau des Goal-Schreibwegs"
status: doing
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-hyn77, budgeting-v7kfw, budgeting-dgrp7]
blocks: [budgeting-hbhzf, budgeting-e2z7v, budgeting-q4v8n, budgeting-m3x7f]
tags: [targets, uebernahme, rueckbau, fuehrung]
related_adrs: [0072, 0079, 0080, 0082, 0083, 0084]
related_research: []
prior_art: [budgeting-k8wq3, budgeting-t6p8z, budgeting-h2zfn, budgeting-p2zr6]
---

## Why
Nach ADR 0084 Punkt 6 führt BB die Targets selbst. Die Goals in YNAB werden eingefroren und nur
noch gelesen. Damit entfällt der Zwei-Schritt-Schreibweg aus ADR 0072 und 0080 mit seinen
Teilfehlern, und Targets können, was YNAB-Goals nicht können (Wochentag setzen).

Teil 1 von 2 (Teilung 2026-11-06). Teil 2 ist budgeting-hbhzf: BBs Fehlbetrag in "Targets
anwenden" und in der Anzeige, Rückbau der Parallelrechnung.

## What
- Einmalige Übernahme aus `/categories` (`goal_*`, `src/Server/YnabClient.fs`) als
  `TargetTakenOverFromYnab` je Kategorie (Regel plus rohes `ynabGoalType`) im Stream
  `budgeting:target:<categoryId>`. Nur die in Romans Budget genutzten Arten; DEBT wird nicht
  übernommen und gemeldet.
- `changeCategoryTarget` hängt nur noch `TargetSet` (vollständige Regel, `effectiveFrom` = laufender
  Monat) oder `TargetRemoved` an, unter `TargetGuard`, verglichen gegen `Shown` aus der BB-Faltung.
- Befehls-Id für Target ändern (`TargetChangeRequest.CommandId`): Prüfung unter `TargetGuard` im
  Target-Stream, eine Wiederholung mit derselben Id bekommt die Antwort aus dem vorhandenen
  `TargetSet` bzw. `TargetRemoved` und hängt nichts an (Entscheidung Roman, Verfeinerung qm5x9/dgrp7).
- Pause nur noch in BB: `PausedInYnab` und `PausedInBoth` entfallen (ADR 0079 Punkt 2).
- Rückbau: `planTargetWrite`, `sendSteps`, Port `SetTarget`, `TargetPartlyWritten`,
  `TargetChangedInYnab`. `setCategoryTarget` in `YnabClient.fs` bleibt nur, wenn budgeting-e2z7v
  ihn nutzt (heute: entfernen, e2z7v baut ihn bei Bedarf neu).
- Wochentag wöchentlicher Targets in BB setzbar.
- Vergangene Monate zeigen die damals gültige Regel.

## Acceptance criteria
- [ ] Übernahme: GIVEN Kategorien mit jeder in Romans Budget genutzten Art und eine mit DEBT, WHEN
      die Übernahme läuft, THEN steht je Kategorie mit übernehmbarem Target genau ein
      `TargetTakenOverFromYnab`, DEBT fehlt und wird gemeldet, ein zweiter Lauf schreibt nichts.
- [ ] Vergleich Übernahme gegen YNAB: Für jede übernommene Kategorie ergibt die Faltung dieselbe
      Art, denselben Betrag, Rhythmus, Datum und Tag wie die gelesenen `goal_*`-Felder.
- [ ] `changeCategoryTarget`: Anlegen, Ändern, Wechsel der Art und Entfernen hängen je genau ein
      Ereignis an und rufen YNAB nicht auf (der Port `SetTarget` existiert nicht mehr); weicht
      `Shown` von der Faltung ab, wird abgelehnt; außerhalb des laufenden Monats wird abgelehnt wie
      heute (ADR 0080 Punkt 1).
- [ ] Befehls-Id: GIVEN ein gespeichertes Target ändern mit Id X, WHEN derselbe Befehl mit Id X noch
      einmal kommt, THEN hängt er nichts an und antwortet wie beim ersten Mal; ein Befehl mit neuer
      Id und veraltetem `Shown` wird abgelehnt.
- [ ] Die Historie zeigt "Angelegt" und "50 → 60" aus der vorigen Faltung, ohne eigene Typen dafür.
- [ ] GIVEN ein Target, das im Monat M geändert wurde, WHEN ein Monat vor M gezeigt wird, THEN
      zeigt er die Regel vor der Änderung.
- [ ] Der Wochentag eines wöchentlichen Targets lässt sich setzen, und die reine Funktion
      `underfunded` aus budgeting-v7kfw rechnet mit ihm (Test der Faltung und der Funktion).
- [ ] Eine in YNAB gesetzte Pause ändert in BB nichts mehr; `PauseSource` kennt nur noch die
      BB-Pause oder entfällt.
- [ ] Die Target-Prüfung (ADR 0082 Punkt 7) nimmt die letzte Änderung aus `TargetSet` oder
      `TargetRemoved` (bzw. dem Altbestand-Typ mit geschriebenem Schritt 1);
      `TargetTakenOverFromYnab` zählt nicht als Änderung.
- [ ] Die entfernten Teile (`planTargetWrite`, `sendSteps`, `TargetPartlyWritten`,
      `TargetChangedInYnab`, `PausedInYnab`, `PausedInBoth`) sind aus Code und Tests verschwunden.
- [ ] Die Fixtures in `scripts/*.test.mjs` werden im selben Schritt an die geänderten Typen aus
      `src/Shared` angepasst.
- [ ] Die Ubiquitous language der budgeting-README hat Einträge für "Target-Übernahme" und
      "Target ändern (nur BB)".
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkte 5, 6 und 7.
- **Nicht allein deployen.** Nach diesem Teil sind die YNAB-Goals eingefroren, YNABs
  `goal_under_funded` passt bei jedem in BB geänderten Target nicht mehr. Deploy erst zusammen mit
  budgeting-hbhzf, das "Targets anwenden" und die Anzeige auf BBs Fehlbetrag umstellt.
- Die Parallelrechnung (`underfunded_comparisons`, Speichern beim Lesen eines Monats) bleibt in
  diesem Teil unverändert. Ihr Rückbau gehört zu budgeting-hbhzf.
- Erweiterungen (etwa Auffüllen über den Monatswechsel aus budgeting-bk6tq) sind eigene Tasks
  mit neuen Ereignistypen.
- Fachsprache: "Soll", "Übernahme" und "Rückweg" bekommen spätestens hier einen Eintrag in der
  Ubiquitous language der budgeting-README (Hinweis aus der Verifikation von budgeting-zkbbd).
  "Soll" und "Übernahme" (des Budgets) stehen seit dgrp7 dort.
- Teilung 2026-11-06 (Roman: "ja" zum Vorschlag): Fehlbetrag, Toleranz und Rückbau der
  Parallelrechnung gingen nach budgeting-hbhzf.
