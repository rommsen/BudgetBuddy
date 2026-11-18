---
id: budgeting-h2brj
title: "Wochentag eines Targets als Auswahlfeld statt sieben Knöpfe, ohne Wahl „Wochentag wählen“ in Gelb"
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
prior_art: [budgeting-eh8xe, budgeting-c2t9w]
---

## Why
Teil B aus budgeting-wnkgj (Roman, Prüfliste w01). Der Wochentag eines wöchentlichen Targets ist heute ein `Segmented`
mit sieben Knöpfen Mo bis So (`TargetFormView.fs:159-177`). Styleguide §7.5 verlangt dafür ein Auswahlfeld. Der
Prototyp hat keinen Wochentag, BB schon: BB gilt, nur die Form ändert sich.

## What
Ein Feld „Wochentag“ (Muster `Input.selectField`), das eine kurze Auswahl Montag bis Sonntag öffnet, wie die Gruppe in
„Kategorie bearbeiten“ (`PickerParts.ShortChoiceFor`, design-system-gcpws G13). Gilt überall, wo das Target-Formular
steht: Tab „Target“ im Kategorie-Detail und Panel „Anpassen …“ in Targets prüfen.

## Acceptance criteria
- [ ] Das Feld zeigt die Beschriftung „Wochentag“ und den Tag ausgeschrieben („Montag“). Antippen öffnet die kurze
      Auswahl mit sieben Zeilen, Montag zuerst, die gewählte mit Haken; am Handy ersetzt sie den Inhalt des Sheets mit
      „Zurück“ (§7.15), am PC ein Dialog. Wahl schließt die Auswahl und setzt den Tag (`OnWeekdayChanged`).
- [ ] Am PC: ↑↓ und Enter in der Auswahl, Esc schließt nur die Auswahl, der Fokus kehrt auf das Feld zurück (§7.8, §7.9).
- [ ] Ohne gewählten Tag zeigt das Feld „Wochentag wählen“ in Gelb (`Colors.attention`), kein roter Fehlertext mehr
      unter dem Feld (Roman w04 a). Speichern bleibt gesperrt, bis ein Tag gewählt ist; die API-Grenze meldet weiter
      `TargetWeekdayMissingText`. Kein Tag wird still vorbelegt.
- [ ] Gesperrt wie heute der Rhythmus (`formFieldsLocked`): das Feld ist dann nicht antippbar.
- [ ] Braucht `Input.selectField` dafür eine Variante mit gelbem Platzhalter, wird sie im DS angelegt und auf
      `/styleguide` gezeigt, mit Zeile im Inventar (§5).
- [ ] Sichtbarkeit und Text des Felds (Tag, Platzhalter, gesperrt) kommen aus einer reinen Funktion mit .NET-Test; die
      Wahl über die Auswahl ist im Client-`update` getestet.
- [ ] Neuer Layout-Zustand für die offene Wochentag-Auswahl bei 412 und 1440 px im Sweep; der bestehende Tasten-Sweep
      (`scripts/key-invariants.test.mjs`) deckt die kurze Auswahl mit.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten; Screenshot-Vergleich im Outcome. [human-eye]

## Notes
Vorlage: Prototyp Version 17 (https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg). Der Conductor legt die Datei und Screenshots je Ansicht (412 und 1440 px) in den Scratchpad und gibt Worker und Verifier die Pfade mit.
Abweichung vom Prototyp, entschieden: der Wochentag selbst (nur in BB). Das Feld öffnet eine Auswahl, die den
Sheet-Inhalt ersetzt; die Tastatur des Betragsfelds schließt dabei wie bei der Gegenseite im Verschieben, das ist im
Prototyp genauso. Bestandsaufnahme in budgeting-wnkgj.
