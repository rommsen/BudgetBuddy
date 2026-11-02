---
id: 0075
title: Farbrollen. Gelb als neuer Token für "offen, kein Fehler", neutrale Standard-Knöpfe, Teal für Ausgewähltes und Navigation
scope: global
status: accepted
date: 2026-11-02
supersedes: []
superseded_by: []
related_tasks: [budgeting-v4ht9, budgeting-g8s3w]
related_adrs: [0049]
related_research: []
---

# ADR 0075: Farbrollen

## Context
Auf der Budget-Seite ist nicht zu erkennen, was klickbar ist. `fehlt` und "Passt das Target
noch?" sind offene Fälle und keine Fehler. Der
Styleguide kennt dafür keine Farbe: Rot heißt Fehler, Pink "attention needed", Orange die
Hauptaktion. Teal ist zugleich `Button.secondary`, aktiver Zustand und Navigation.

ADR 0049 erlaubt je Zeile höchstens einen farbigen Akzent. Eine Budget-Zeile kann aber zugleich
im Minus stehen und `fehlt` tragen.

Commit eaccedd hat `text-secondary` und `text-muted` angehoben, sie erreichen auf den Flächen
mindestens 4,5:1. Das ist der Stand, auf dem diese Farbrollen festgelegt wurden.

## Decision
Dieses ADR ergänzt ADR 0049.

1. **Gelb ist ein neuer Token und heißt "offen, kein Fehler".** Auf der Budget-Seite sind das
   `fehlt` und "Passt das Target noch?". Gelb ist dezent, der Prototyp nutzt `#cdb27a`. Den
   endgültigen Wert legt budgeting-g8s3w im Token-Layer fest.
2. **Standard-Knöpfe sind neutral mit grauem Rand.** Das weicht vom Teal-`Button.secondary` ab.
3. **Teal steht für Ausgewähltes und Navigation.** Orange bleibt die eine Hauptaktion, Rot
   markiert in der Budget-Seite nur Beträge im Minus.
4. **Rot und Gelb dürfen in einer Budget-Zeile zusammenstehen.** Das ist eine Ausnahme von "ein
   Akzent pro Zeile" (ADR 0049 Punkt 1) und gilt nur für die Budget-Seite.
5. **Auf der Budget-Seite gibt es kein Pink.** Das Kassenbuch behält Pink für "zu prüfen"
   (ADR 0049 Punkt 7).

## Consequences
### Positive
- Klickbares und Ausgewähltes sind unterscheidbar.
- Rot behält auf der Budget-Seite eine Bedeutung, ein offener Fehlbetrag wirkt nicht wie ein
  Fehler.

### Negative
- Der Styleguide trägt eine weitere Farbe und eine Ausnahme, die nur für eine Seite gilt.
- Solange andere Seiten Teal-Knöpfe zeigen, ist die Oberfläche uneinheitlich.

### Neutral
- budgeting-g8s3w setzt Token, Knopf-Regel, Styleguide und `/styleguide` um. Dort fällt auch die
  Entscheidung, ob `Button.secondary` sich ändert oder eine neue Variante entsteht, und wann
  bestehende Seiten mitziehen.
- Alle übrigen Punkte von ADR 0049 gelten unverändert, auch für die Budget-Liste.

## Alternatives considered
- **Pink für offene Fälle auf der Budget-Seite.** Verworfen: Pink heißt im Kassenbuch "zu prüfen",
  das Konzept nimmt es von der Budget-Seite.
- **Orange für `fehlt`.** Verworfen: Orange ist die eine Hauptaktion.
- **Nur eine Farbe je Budget-Zeile.** Verworfen: Einer der beiden Zustände wäre unsichtbar.

## References
- ADR 0049
- docs/ux/budget/konzept.md, Abschnitt Farben
- standards/frontend/styleguide.md, Abschnitt 2
- Commit eaccedd (Text-Tokens)
