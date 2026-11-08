---
id: budgeting-pscgb
title: "Kategorien verwalten: Seite mit Anlegen, Umbenennen und Gruppe wechseln, Gruppen anlegen und umbenennen, Schreibweg nach YNAB"
status: todo
type: feature
context: budgeting
created: 2026-11-08
completed:
depends_on: []
blocks: [budgeting-sn2sn]
tags: [etappe-2, kategorien, ynab]
related_adrs: [0088, 0086, 0087]
related_research: []
prior_art: []
---

## Why
Fahrplan Etappe 2: Kategorien verwalten, heute nur in YNAB möglich. ADR 0088 Punkt 1.

## What
- Neue Seite "Kategorien" im Budget, erreichbar über das Menü ⋯ der Budget-Seite ("Kategorien verwalten"), wie im
  Prototyp Version 17: Gruppen als Abschnitte mit "Umbenennen", darin die Kategorien; unten "+ Kategorie" und
  "+ Gruppe".
- Kategorie anlegen und bearbeiten in einem Formular (am Handy Sheet, ab 1024 px Panel, §7.6): Name, Gruppe als
  Auswahlfeld mit Eintrag "Neue Gruppe" (Fahrplan; §7.5), Hinweis "Wird nach YNAB übertragen".
- Gruppe anlegen und umbenennen: Name, höchstens 50 Zeichen (YNAB-Grenze).
- Schreibweg: Befehl nach dem Kassenbuch-Muster (Validierung in `src/Server/Validation.fs`, Befehl, Port für den
  YNAB-Aufruf), `POST`/`PATCH .../categories` und `.../category_groups`, danach den Kategorie-Spiegel anstoßen.
  Kein eigenes Ereignis (ADR 0088 Punkt 1).
- Fehler aus YNAB konkret melden, die Liste bleibt dann unverändert.

Nicht Teil: Deaktivieren und Reihenfolge (budgeting-sn2sn), Löschen (gibt es nicht).

## Acceptance criteria
- [ ] Handler-Test: Kategorie anlegen schickt `POST` mit `name` und `category_group_id` und stößt danach den
      Kategorie-Spiegel an (Ports zählen die Aufrufe).
- [ ] Handler-Test: Umbenennen und Gruppe wechseln schicken `PATCH` nur mit den geänderten Feldern.
- [ ] Handler-Test: Gruppe anlegen und umbenennen schicken `POST`/`PATCH` auf `category_groups`.
- [ ] Validierung: leerer Name, Name über 50 Zeichen bei Gruppen, Name schon in derselben Gruppe vorhanden,
      unbekannte Gruppe, unbekannte Kategorie; je mit konkreter Meldung, ohne YNAB-Aufruf (Doktrin Punkt 4).
- [ ] Handler-Test: YNAB antwortet mit Fehler (400, 429, Netz): Meldung an den Aufrufer, kein Spiegel-Lauf, Liste
      unverändert.
- [ ] Encoder-Test für die JSON-Rümpfe (Pfad `plans` oder `budgets` so, wie der übrige `YnabClient` ihn nutzt).
- [ ] Je neuem Endpoint eine Zeile in `src/Tests/RemotingEndpointNegativeTests.fs`, je neuem Interface ein Aufruf in
      `src/Tests/RemotingPipelineTests.fs` (Doktrin Punkt 10 und 11).
- [ ] Client-`update` unter .NET: Speichern ist gesperrt, solange der Aufruf läuft; Fehler bleiben im Formular;
      "Neue Gruppe" im Auswahlfeld öffnet das Gruppen-Formular und kehrt danach mit der neuen Gruppe zurück.
- [ ] Zustände für die Seite und das offene Formular im Layout-Sweep (`scripts/layout-states.mjs`), Handy und PC.
- [ ] Seite neben dem Prototyp Version 17 verglichen (Budget ⋯ "Kategorien verwalten"), Abweichungen nur die
      genannten; Screenshots im Outcome. [human-eye]

## Notes
- Prototyp: https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Der Conductor legt Screenshots in den
  Scratchpad und gibt die Pfade an Worker und Verifier.
- Abweichungen vom Prototyp: Pfeile für die Reihenfolge, der Schalter "Aktiv" und der Abschnitt "Deaktiviert" kommen
  erst mit budgeting-sn2sn. Die Gruppenwahl ist im Prototyp ein eigenes Sheet; in BB ein Auswahlfeld, weil Gruppen
  etwas sind, das der Nutzer anlegt (§7.5).
- YNAB-API laut OpenAPI-Spezifikation (ADR 0088 Context). Ob der Client `plans` oder `budgets` im Pfad nutzt, folgt
  dem bestehenden `src/Server/YnabClient.fs`.
- Kategorie-Spiegel: `refreshCategories` und `upsertCategories` (`src/Server/Ledger/CommandHandlers.fs:177`,
  `src/Server/Ledger/Persistence.fs:2226`).
