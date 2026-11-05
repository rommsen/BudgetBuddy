---
id: budgeting-eh8xe
title: "Targets als BB-Regeln: einmalige Übernahme aus YNAB 1:1, Fehlbetrag aus BB, Rückbau des Goal-Schreibwegs und der YNAB-Pause, Wochentag in BB setzbar"
status: backlog
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-hyn77, budgeting-v7kfw, budgeting-dgrp7]
blocks: [budgeting-e2z7v, budgeting-q4v8n, budgeting-m3x7f]
tags: [targets, uebernahme, rueckbau, fuehrung]
related_adrs: [0072, 0079, 0080, 0082, 0083, 0084]
related_research: []
prior_art: [budgeting-k8wq3, budgeting-t6p8z, budgeting-h2zfn, budgeting-p2zr6]
---

## Why
Nach ADR 0084 Punkt 6 führt BB die Targets selbst. Die Goals in YNAB werden eingefroren und nur
noch gelesen. Damit entfällt der Zwei-Schritt-Schreibweg aus ADR 0072 und 0080 mit seinen
Teilfehlern, und Targets können, was YNAB-Goals nicht können (Wochentag setzen).

## What
- Einmalige Übernahme aus `/categories` (`goal_*`, `src/Server/YnabClient.fs:90-103`) als
  `TargetTakenOverFromYnab` je Kategorie (Regel plus rohes `ynabGoalType`) im Stream
  `budgeting:target:<categoryId>`. Nur die in Romans Budget genutzten Arten; DEBT wird nicht
  übernommen und gemeldet.
- `changeCategoryTarget` hängt nur noch `TargetSet` (vollständige Regel, `effectiveFrom` = laufender
  Monat) oder `TargetRemoved` an, unter `TargetGuard`, verglichen gegen `Shown` aus der BB-Faltung.
- Fehlbetrag aus budgeting-v7kfw statt `goal_under_funded`; "Targets anwenden" schaltet erst um,
  wenn die Abweichungsliste innerhalb der von Roman festgelegten Toleranz liegt.
- Pause nur noch in BB: `PausedInYnab` und `PausedInBoth` entfallen (ADR 0079 Punkt 2).
- Rückbau: `planTargetWrite` (`Domain.fs:206`), `sendSteps` (`CommandHandlers.fs:444-483`), Port
  `SetTarget`, `TargetPartlyWritten`, `TargetChangedInYnab`. `setCategoryTarget`
  (`YnabClient.fs:553-577`) bleibt nur, wenn budgeting-e2z7v ihn nutzt.
- Wochentag wöchentlicher Targets in BB setzbar.
- Vergangene Monate zeigen die damals gültige Regel.

## Acceptance criteria
- [ ] Übernahme: GIVEN Kategorien mit jeder in Romans Budget genutzten Art und eine mit DEBT, WHEN
      die Übernahme läuft, THEN steht je Kategorie mit übernehmbarem Target genau ein
      `TargetTakenOverFromYnab`, DEBT fehlt und wird gemeldet, ein zweiter Lauf schreibt nichts.
- [ ] Vergleich Übernahme gegen YNAB: Für jede übernommene Kategorie ergibt die Faltung dieselbe
      Art, denselben Betrag, Rhythmus, Datum und Tag wie die gelesenen `goal_*`-Felder.
- [ ] `changeCategoryTarget`: Anlegen, Ändern, Wechsel der Art und Entfernen hängen je genau ein
      Ereignis an und rufen YNAB nicht auf (Port zählt null `SetTarget`-Aufrufe bzw. der Port
      existiert nicht mehr); weicht `Shown` von der Faltung ab, wird abgelehnt; außerhalb des
      laufenden Monats wird abgelehnt wie heute (ADR 0080 Punkt 1).
- [ ] Die Historie zeigt "Angelegt" und "50 → 60" aus der vorigen Faltung, ohne eigene Typen dafür.
- [ ] GIVEN ein Target, das im Monat M geändert wurde, WHEN ein Monat vor M gezeigt wird, THEN
      zeigt er die Regel vor der Änderung.
- [ ] Der Wochentag eines wöchentlichen Targets lässt sich setzen, und der Fehlbetrag rechnet mit
      ihm (Test der Faltung und der Fehlbetrag-Funktion).
- [ ] Targets anwenden nutzt BBs Fehlbetrag; ein Test belegt, dass der Umschalter erst greift,
      wenn die Toleranz gesetzt ist (oder das Umschalten ist im Outcome als von Roman freigegeben
      vermerkt).
- [ ] Eine in YNAB gesetzte Pause ändert in BB nichts mehr; `PauseSource` kennt nur noch die
      BB-Pause oder entfällt.
- [ ] Die Target-Prüfung (ADR 0082 Punkt 7) nimmt die letzte Änderung aus `TargetSet` oder
      `TargetRemoved` (bzw. dem Altbestand-Typ mit geschriebenem Schritt 1);
      `TargetTakenOverFromYnab` zählt nicht als Änderung.
- [ ] Die entfernten Teile (`planTargetWrite`, `sendSteps`, `TargetPartlyWritten`,
      `TargetChangedInYnab`, `PausedInYnab`, `PausedInBoth`) sind aus Code und Tests verschwunden.
- [ ] Die Fixtures in `scripts/*.test.mjs` werden im selben Schritt an die geänderten Typen aus
      `src/Shared` angepasst.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkte 5, 6 und 7. Bedingung für Targets anwenden: Parallelrechnung aus
  budgeting-v7kfw und Toleranz von Roman.
- BBs Fehlbetrag braucht Zugewiesen aus der Soll-Faltung von budgeting-dgrp7. In der
  Verfeinerung prüfen, ob budgeting-dgrp7 in `depends_on` gehört.
- budgeting-q4v8n und budgeting-m3x7f werden erst danach gebaut, auf diesen Schreibweg.
- Erweiterungen (etwa Auffüllen über den Monatswechsel aus budgeting-bk6tq) sind eigene Tasks
  mit neuen Ereignistypen.
- `depends_on` um budgeting-dgrp7 ergänzt (Conductor, 2026-11-05): Targets anwenden braucht das BB-Soll aus der Faltung in budgeting-dgrp7.
- Fachsprache: "Soll", "Übernahme" und "Rückweg" bekommen spätestens hier einen Eintrag in der Ubiquitous language der budgeting-README (Hinweis aus der Verifikation von budgeting-zkbbd).
- Rückbau aus budgeting-v7kfw (Verfeinerung 2026-11-05): Die Zustandstabelle `underfunded_comparisons` und ihr Speichern beim Lesen eines Monats entfallen hier, sobald BB den Fehlbetrag aus `events` faltet. Die Projektion nutzt die reine Funktion `underfunded` aus v7kfw.
- Aus der Verfeinerung von budgeting-qm5x9 und budgeting-dgrp7 (2026-11-05): Target ändern
  bekommt hier seine Befehls-Id (`TargetChangeRequest.CommandId`, Prüfung unter `TargetGuard` im
  Target-Stream, Antwort aus `TargetSet`), entschieden von Roman. Targets anwenden rechnet dann den
  Fehlbetrag gegen das Soll; die Ablehnung bei offener Rückschreibung aus dgrp7 entfällt.
