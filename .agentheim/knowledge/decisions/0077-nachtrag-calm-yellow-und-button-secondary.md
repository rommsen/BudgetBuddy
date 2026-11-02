---
id: 0077
title: Nachtrag zu ADR 0075. Token calm-yellow, Button.secondary ändert nur den Hover, bestehende Seiten ziehen sofort mit
scope: global
status: accepted
date: 2026-11-02
supersedes: []
superseded_by: []
related_tasks: [budgeting-g8s3w]
related_adrs: [0075, 0049]
related_research: []
---

# ADR 0077: Nachtrag zu ADR 0075, Gelb und Standard-Knopf

## Context
ADR 0075 überlässt budgeting-g8s3w zwei Punkte: den endgültigen Wert für Gelb und die Frage, ob `Button.secondary` sich ändert oder eine neue Variante entsteht, und wann bestehende Seiten mitziehen.

## Decision
1. **Token `--color-calm-yellow: #cdb27a`** im `@theme`-Block, `Colors.calmYellow` und `ListRoles.missing` in `Tokens.fs`. Der Wert des Prototyps wird endgültig: Kontrast 9,03:1 auf `bg-card`, 8,23:1 auf `bg-elevated`, 7,84:1 auf `bg-hover`.
2. **Keine neue Knopf-Variante.** `Button.secondary` war im Ruhezustand schon neutral mit grauem Rand, teal war nur der Hover. Der Hover wird `hover:bg-surface-hover`.
3. **Bestehende Seiten ziehen sofort mit.** Es ändert sich nur ein Hover-Zustand, das sichtbare Risiko ist klein.

## Consequences
### Positive
- Die Uneinheitlichkeit, die ADR 0075 unter "Negative" nennt, tritt bei `Button.secondary` nicht ein.
- `guard:tokens` braucht keine Ausnahme: die Definition steht in `styles.css`, die Klasse trifft das Palette-Muster nicht.

### Negative
- `Money.availableButton` hatte keinen Aufrufer mehr und entfällt samt Eintrag im Styleguide.

### Neutral
- `Button.heroTeal` bleibt unverändert.

## Alternatives considered
- **Neue Variante `Neutral`.** Verworfen: sie wäre im Ruhezustand identisch mit `Secondary`.