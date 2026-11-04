---
id: 0079
title: Die Pause eines Targets ist eine Ereignisfolge in BB, die Kategorie trägt ihre Quelle, zusammengeführt beim Bau des Monats
scope: budgeting
status: accepted
date: 2026-11-04
supersedes: []
superseded_by: []
related_tasks: [budgeting-t6p8z]
related_adrs: [0065, 0073, 0074]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
---

# ADR 0079: Pause als Ereignisfolge in BB

## Context
ADR 0073 legt die Pause je Kategorie und Monat in BB und lässt offen, wie sie gespeichert und aufgezeichnet wird. Bisher kennt `BudgetCategory` nur `Snoozed: bool` aus YNABs `goal_snoozed_at`. Fehlt, Frei, Vorschau und Targets anwenden fragen dieses Feld. Eine YNAB-Pause lässt sich in BB nicht aufheben, eine BB-Pause schon (Annahme F1).

## Decision
1. **Append-only Ereignisse.** Tabelle `target_pause_events` mit "Pausiert" und "Fortgesetzt" je (Monat, Kategorie), Trigger gegen UPDATE und DELETE wie bei `budget_movements`. Der Stand ist das jüngste Ereignis. Aufheben hängt ein Ereignis an. Die Folge ist zugleich die Aufzeichnung für die Historie.
2. **Die Kategorie trägt die Quelle.** `Snoozed: bool` wird `Pause: PauseSource option` (`PausedInYnab | PausedInBb | PausedInBoth`). Fachregeln fragen nur `isPaused`, die Oberfläche braucht die Quelle für Aufheben und den Hinweis zur YNAB-Pause.
3. **Zusammengeführt in `toBudgetMonth`.** Die BB-Pausen des Monats gehen als Parameter hinein. Es gibt keinen zweiten Ort, an dem YNAB-Monat und BB-Pause zusammenkommen.
4. **Unter der Sperre je Monat.** Pausieren und Aufheben laufen unter derselben Sperre wie Targets anwenden. Übernehmen liest die Pausen innerhalb der Sperre, eine Pause zwischen Vorschau und Übernehmen ergibt "Stand hat sich geändert" über `previewsMatch`, ohne dessen Änderung.

## Consequences
### Positive
- Fehlt, Frei und Targets anwenden behandeln beide Quellen gleich, ohne doppelte Fallunterscheidung.
- Die Historie bekommt die Pause ohne eigene Aufzeichnung.

### Negative
- Das Feld `Snoozed` ändert seinen Namen. Fünf Playwright-Fixtures ziehen mit.
- Jeder Monatsaufruf liest eine Tabelle mehr.

### Neutral
- Beim Wechsel zu BB als führendem System liegt die Pause schon in BB (ADR 0073, Neutral).
- Pausieren geht weiter im laufenden und in künftigen Monaten (ADR 0073 Punkt 5, `targetsAllowed`). Die Entscheidung vom 2026-11-04, Targets nur im laufenden Monat zu ändern (ADR-Entwurf zu Target-Änderungen), betrifft die Pause nicht.

## Alternatives considered
- **Tabelle mit Zeile je Pause, Aufheben löscht.** Verworfen: Die Historie verlöre die Pause, und die Budget-Tabellen sind sonst durchgehend append-only.
- **Zwei Bool-Felder `Snoozed` und `PausedInBb`.** Verworfen: jede Regel müsste beide fragen, ein vergessener Fall rechnet den Fehlbetrag falsch.
- **BB-Pause im Client mischen.** Verworfen: Targets anwenden läuft auf dem Server und braucht sie dort.

## References
- ADR 0065, 0073, 0074
- docs/ux/budget/konzept.md, Abschnitt Targets

## Nachtrag 2026-11-04 (budgeting-r3wq8)
Das Aufheben heißt in Code und Spalte einheitlich `PauseLifted` mit dem Text "Aufgehoben". Der in Punkt 1 genannte Text "Fortgesetzt" ist ein Altwert: er wird beim Lesen weiter als Aufheben erkannt (lokale Dev-Datenbanken), geschrieben wird er nicht mehr. Eine BB-Pause lässt sich auch ohne Target aufheben; ohne Target wirkt sie nicht.
