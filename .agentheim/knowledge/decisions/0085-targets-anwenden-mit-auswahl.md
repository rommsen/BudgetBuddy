---
id: 0085
title: Targets anwenden mit Auswahl. Die gesendete Vorschau trägt nur die gewählten Zeilen, der Server gleicht sie als Teilmenge der frischen ab
scope: budgeting
status: accepted
date: 2026-11-06
supersedes: []
superseded_by: []
related_tasks: [budgeting-m3x7f]
related_adrs: [0065, 0075, 0083, 0084]
related_research: []
---

# ADR 0085: Targets anwenden mit Auswahl

## Context

Die freigegebene Skizze (Gesamt-Prototyp, Zeile 183 bis 196) zeigt in der Vorschau von Targets anwenden je Zeile eine Checkbox, alle beim Öffnen an; abgewählte Zeilen werden nicht geschrieben, Summe und "Zu verteilen danach" folgen der Auswahl. ADR 0065 Punkt 6 verlangte bisher, dass die übergebene Vorschau in allen Zeilen (Kategorie, altes und neues Zugewiesen) und in Zu verteilen mit der frisch geplanten übereinstimmt, sonst "Stand hat sich geändert". Mit einer Auswahl wäre jede abgewählte Zeile eine Abweichung. Die Summe des Felds Targets anwenden in der Leiste Monatserster muss außerdem gleich der Summe der Vorschau sein.

## Decision

1. Der Client schickt beim Anwenden die Vorschau nur mit den gewählten Zeilen; Summe und Zu verteilen danach sind auf diese Zeilen umgerechnet (`previewWithLines`). Kein neues Feld im Vertrag, ein alter Client schickt wie bisher alle Zeilen.
2. Der Server plant frisch und prüft unter der Sperre je Monat: jede gezeigte Zeile steht mit Kategorie, altem und neuem Zugewiesen in der frischen Vorschau, Monat und Zu verteilen davor sind gleich, und es ist mindestens eine Zeile gewählt (`Domain.previewsMatch`). Sonst kommt die frische Vorschau zurück, nichts wird geschrieben. Abgewählte oder seither neu hinzugekommene Zeilen zählen dabei nicht.
3. Festgehalten und gesendet werden nur die gewählten Zeilen der frischen Vorschau (`Domain.chosenLines`). Pausierte Zeilen bleiben wie bisher übersprungen.
4. Die Zeilen von Targets anwenden entstehen an einer Stelle in `Shared.Budgeting` (`targetApplicationLines`), die Summe über `linesTotal`. Der Server plant damit die Vorschau, der Client rechnet damit das Feld Targets anwenden der Leiste (`targetApplicationTotal`).

## Consequences

### Positive
- Abwählen bewegt kein Geld, und der Stand-Abgleich schützt weiter alles, was geschrieben wird.
- Leiste und Vorschau können in der Summe nicht auseinanderlaufen.

### Negative
- Eine Zeile, die zwischen Vorschau und Anwenden neu hinzukommt, meldet kein "Stand hat sich geändert"; sie wird nur nicht geschrieben und steht in der nächsten Vorschau.

### Neutral
- ADR 0065 Punkt 6 gilt für die gewählten Zeilen weiter. Zu verteilen davor muss gleich bleiben wie bisher.

## Alternatives considered
- **Die volle Vorschau senden und die abgewählten Ids getrennt mitgeben.** Verworfen: braucht ein neues Feld im Remoting-Vertrag (Fixture-Falle) und einen Abgleich auch über Zeilen, die gar nicht geschrieben werden.
- **Vollständigen Abgleich behalten und Abwählen nur im Client.** Verworfen: der Client müsste ganze Zeilen weglassen, die der Server dann als Abweichung wertet; jede Auswahl endete in "Stand hat sich geändert".
