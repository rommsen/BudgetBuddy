---
id: budgeting-c5w2n
title: "Tab Geld: rechnendes Zuweisen, Fehlbetrag-Knopf, Handy-Schritte, Geld holen und abgeben mit Vorschlägen, Speichern beim Verlassen"
status: backlog
type: feature
context: budgeting
created: 2026-11-02
completed:
depends_on: [design-system-001, budgeting-g8s3w, budgeting-p4n6r, budgeting-h3k9d]
blocks: [budgeting-dyn8g]
tags: [budget, zuweisen, verschieben]
related_adrs: [0005, 0055, 0069, 0070]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-afd8h, budgeting-nfrds]
---

## Why
Task 2 von 5 des Gesamtkonzepts. Im Monat soll man direkt handeln, wie Roman es von YNAB kennt, und schneller sein als dort.

## What
Regeln: `docs/ux/budget/konzept.md`, Abschnitte Zuweisen sowie Geld holen und abgeben.
- Befehl Zuweisen mit Herkunft "Zuweisen" (ADR 0069) über die vorhandene Sperre je Monat, sendet genau einen PATCH beim Verlassen.
- Rechnen als reine Client-Funktion mit `decimal`, die API erhält den fertigen Betrag.
- Knopf "12,30 zuweisen" aus `fehlt`.
- Umschalter Geld holen und abgeben nutzt die Gegenseite des bestehenden Verschiebens. Aufbau nach Variante D der Zeichenfläche, Seite "Geld bewegen": großer Betrag, `von` und `nach` untereinander, Knopf nennt die Handlung, `CategoryPicker` mit "Zuletzt genutzt". Offen in der Modellierung: wie Zugewiesen und Holen/Abgeben im Tab Geld zusammen Platz finden.
- Der bisherige Verschieben-Dialog entfällt, Offene Verschiebung bleibt.

## Acceptance criteria
Werden in der taktischen Modellierung geschärft.
- [ ] Rechenfunktion: `+20`, `-20`, `*2`, `/2`, `900+40`, Punkt vor Strich, Division durch 0, deutsche Beträge, Unsinn.
- [ ] Mehrere Handy-Schritte ergeben einen Schreibvorgang und eine Budget-Bewegung, gespeichert bei Tab-, Kategorie- und Monatswechsel und beim Schließen.
- [ ] Verlassen ohne Änderung schreibt nichts, Esc verwirft.
- [ ] Holen und Abgeben, Warnung bei Überziehen der Quelle, lange Kategorienamen werden nicht abgeschnitten, Teilfehler bleiben als Offene Verschiebung sichtbar.
- [ ] Knopf `fehlt` respektiert pausierte Targets und zu wenig Zu verteilen.
- [ ] Browser-Wächter: Sheet mit offener Tastatur, Schritte behalten den Fokus (ADR 0005), Trefferflächen 48 px.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Hinweise aus dem Review von budgeting-g8s3w
- `Model.Move` passt nicht zu Verschieben im Tab Geld: `selectCategory` sperrt bei offenem Formular den Kategoriewechsel (`Types.fs:786`), `sheetCategory` blendet das Sheet aus (`View.fs:995`). `Move` gehört in den Tab-Zustand, `openMove`, `applyLoaded` und die Lage von `moveRetries` ziehen mit.
- Speichern beim Verlassen braucht einen gemeinsamen Übergang `leaveInspector`: Verlassen geschieht heute an sieben Stellen (`select`, `selectCategory`, `stepSelection`, `SelectInspectorTab`, `CloseInspector`, `leaveForEntry`, `enterPage`). Der Entwurf hält Kategorie und Monat selbst. Esc braucht Behandlung in `BottomSheet` und Panel.
- `isInputActive` in `src/Client/State.fs:385` kennt die Budget-Seite nicht (ADR 0057). Entwürfe wären sonst verlorene Eingabe.
- Zwei Sammelzweige in `Components/Budget/State.fs:162`, `:206` verschlucken neue `Msg`-Fälle. `updatePage` gibt besser ein Tripel mit `ExternalMsg` zurück, ein `match` ohne `_`.
- `Types.fs` (893 Zeilen) und `View.fs` (1114 Zeilen) vorher nach Monat, Targets, Verschieben, Inspektor teilen.
- `height` fehlt in der Transition des Sheets (`styles.css:1687-1690`), relevant beim kleiner werdenden Sheet mit Tastatur.
