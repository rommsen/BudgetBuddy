---
id: budgeting-yarty
title: "Budget-Historie: Feld „Zeigen“ statt Chipleiste, Zeilen nach §7.11 aus HistoryList, auch die Historie im Kategorie-Detail"
status: todo
type: feature
context: budgeting
created: 2026-11-18
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, budget]
related_adrs: [0087]
related_research: []
prior_art: [budgeting-r5h2n, budgeting-a3k7m]
---

## Why
Teil C aus budgeting-wnkgj (Roman, Prüfliste w01). Die Budget-Historie filtert über eine wischende Chipleiste
(`HistoryView.fs:171-187`), am Handy ist „Zuweisen“ angeschnitten. Ihre Zeilen und die Historie im Kategorie-Detail
(`InspectorView.historyLineView`) folgen nicht §7.11, das Kassenbuch nutzt dafür schon `HistoryList`.

## What
Wie im Prototyp (`histView()`, Sheet `histf`, Tab „Historie“ in `bcat`): ein Feld „Zeigen: Alle“ öffnet „Historie
zeigen“, die Zeilen haben die Form Zeit, Kürzel, Text, Betrag.

## Acceptance criteria
- [ ] Die Seite Historie zeigt oben das Feld „Zeigen“ mit der gewählten Art (`Input.selectField`), keine Chipleiste mehr.
      Es öffnet die kurze Auswahl „Historie zeigen“ mit den sieben Arten aus `runFilters` (Alle, Targets anwenden,
      Verschieben, Zuweisen, Targets, In YNAB zugewiesen, Aus YNAB übernommen; Roman w05 a), die gewählte mit Haken; am
      Handy im Sheet, am PC als Dialog (`PickerParts.ShortChoiceFor`). Wahl schließt und filtert (`SelectRunFilter`).
- [ ] Jede Zeile der Seite folgt §7.11 über `HistoryList.row`: Zeit `TT.MM. HH:MM`, Kürzel (`BB` für Entscheidungen in BB,
      `YNAB` für Beobachtetes und Übernommenes), Text, Betrag in Mono (Roman w06 a).
- [ ] Was BB heute zusätzlich zeigt, bleibt als Zusatz zur Zeile: Monat der Buchung, Status (geschrieben, fehlgeschlagen
      mit Grund) und das Aufklappen eines Durchlaufs mit seinen Kategorien. Braucht `HistoryList` dafür eine Variante,
      wird sie im DS angelegt, auf `/styleguide` gezeigt und im Inventar (§5, Zeile HistoryList) beschrieben.
- [ ] Der Tab „Historie“ im Kategorie-Detail zeigt seine Einträge ebenfalls über `HistoryList` (Kann-Sammlung
      budgeting-h9c3w, Zeile zu ledger-zfj78: dort streichen). Der Aufbau Vortrag/Zugewiesen/Aktivität/Verfügbar bleibt.
- [ ] Kürzel, Zusatz und Text je Zeilenart kommen aus reinen Funktionen mit .NET-Tests (je Herkunft, je Status).
- [ ] Die Seite wird umgebaut: `[flow:budget-historie]` in `scripts/budget-history.test.mjs` fliegt raus und wird gegen
      die neue Form neu geschrieben (ein Flow: Historie öffnen, über „Zeigen“ Targets anwenden wählen, Durchlauf
      aufklappen, zurück). Neuer Layout-Zustand für die offene Auswahl „Historie zeigen“ bei 412 und 1440 px.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten; Screenshot-Vergleich im Outcome. [human-eye]

## Notes
Vorlage: Prototyp Version 17 (https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg). Der Conductor legt die Datei und Screenshots je Ansicht (412 und 1440 px) in den Scratchpad und gibt Worker und Verifier die Pfade mit.
Abweichungen vom Prototyp, entschieden: sieben statt sechs Arten (w05 a, BB trennt die YNAB-Herkünfte); Monat, Status und
Aufklappen bleiben (BB kann mehr). Die 12 Monatsbalken im Tab „Historie“ (BB zeigt 6) gehören zu budgeting-wnkgj (Teil D).
Höchstbreite der Seite am PC hängt an Romans Antwort w07 und gehört ebenfalls zu Teil D. Bestandsaufnahme in budgeting-wnkgj.
