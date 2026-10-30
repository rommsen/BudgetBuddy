---
id: categorization-krscs
title: "Regel-Dialog: Ladezustände, Fehler und Seiteneffekte"
status: backlog
type: refactor
context: categorization
created: 2026-10-30
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Gruppe 3 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **mittel**. Der Client der Regeln verschluckt Fehler, führt Ladezustände als Bool oder Text, bindet späte Antworten nicht an die Anfrage und hat Seiteneffekte in update und View.

## What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Client/Components/Rules/State.fs:201` | .claude/rules/elmish-state.md, "Boolean Flags für Async State", Failure anzeigen; standards/frontend/styleguide.md §8 (Ursache benennen) | mittel | Regel-Dialog zeigt bei fehlgeschlagenem Kategorien-Laden oder Deep-Link auf /rules eine leere Liste mit falschem Hinweis.
- `src/Client/Components/Rules/Types.fs:17` | .claude/rules/elmish-state.md, "Boolean Flags für Async State" | mittel | Speicherzustand des Regel-Dialogs ist ein Bool-Flag.
- `src/Client/Components/Rules/Types.fs:16` | .claude/rules/elmish-state.md, RemoteData-Muster | mittel | Ergebnis von Regel testen ist ein Anzeigetext mit Emoji, den die View wieder zerlegt.
- `src/Client/Components/Rules/State.fs:241` | .claude/rules/elmish-state.md, Ladeantwort an die Anfrage binden | mittel | Eine späte Antwort von Regel testen zeigt passt für eine inzwischen geänderte Eingabe.
- `src/Client/Components/Rules/State.fs:238` | .claude/rules/elmish-state.md, Failure korrekt anzeigen | mittel | Ein Netzwerkfehler beim Regel testen erscheint als ungültiges Muster.
- `src/Client/Components/Rules/State.fs:88` | .claude/rules/elmish-state.md, "Cmd.OfAsync.perform" | mittel | Der Timeout der Lösch-Bestätigung läuft über Cmd.OfAsync.perform.
- `src/Client/Components/Rules/State.fs:334` | .claude/rules/elmish-state.md, "Side Effects NUR über Cmd" | mittel | Der Regel-Export startet den Download per JS.eval direkt im update.
- `src/Client/Components/Rules/View.fs:613` | .claude/rules/elmish-state.md, "Side Effects NUR über Cmd" | mittel | Dieselbe Regeldatei lässt sich kein zweites Mal importieren, weil die View die Datei selbst liest.
- `src/Client/Components/Rules/Types.fs:107, :108, :129` | .claude/rules/semantic-messaging.md, "Kein Befehls-DU ohne Aufrufer" | mittel | Tote Nachrichten im Regel-Modul: ToggleRuleEnabled, RuleToggled und ImportRulesStart.
- `src/Client/Components/SyncFlow/Types.fs:72` | .claude/rules/elmish-state.md, "Boolean Flags für Async State" | mittel | Speicherzustand des Regel-Formulars im Zuweisungs-Dialog ist ein Bool-Flag.
- `src/Client/Components/Rules/State.fs:94` | elmish-state.md, Ladeantwort-Bindung | niedrig | `CancelConfirmDelete of RuleId`, nur zurücksetzen, wenn dieselbe Id noch bestätigt wird.
- `src/Client/Components/Rules/Types.fs:115` | semantic-messaging.md, Absicht statt CRUD | niedrig | `UpdateRuleForm*`-Setter (115 bis 122) nach Nutzerabsicht benennen (`PatternEntered`, `CategoryChosen` usw.).
- `src/Client/Components/Rules/Types.fs:125` | categorization README, Key events RuleCreated/RuleUpdated | niedrig | `SaveRule`/`RuleSaved` in Anlegen und Ändern trennen, `IsNewRule` plus `EditingRule` als DU `Editor`.
- `src/Client/Components/Rules/State.fs:52` | elmish-state.md, Ladeantwort-Bindung | niedrig | späte `LoadRules`-Antwort überschreibt eine optimistische `MoveRule`-Reihenfolge; Ladeversion mitführen oder MoveRule sperren.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-30, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn der Owner das will.
