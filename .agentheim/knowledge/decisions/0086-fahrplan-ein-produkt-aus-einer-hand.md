---
id: 0086
title: Fahrplan zu 95 % BB als ein Produkt aus einer Hand; drei Orte, Bedeutungen vor Lücken, Kategorie-Verwaltung wird Ziel
scope: global
status: accepted
date: 2026-11-07
supersedes: []
superseded_by: []
related_tasks: [ledger-ayjzq, design-system-taytp]
related_adrs: [0063, 0075, 0084]
related_research: []
---

# ADR 0086: Fahrplan zu 95 % BB als ein Produkt aus einer Hand

## Context
Grundlage, Stufe 1 und Stufe 2 des Budgets sind gebaut (ADR 0063, ADR 0084). Am
Monatsersten hat Roman Targets zum ersten Mal in BB angewendet. Er will BB als Haupttool
nutzen. Im Brainstorm vom 2026-11-07 wurde klar, was ihn noch zu YNAB zwingt:

- **Kategorien bearbeiten** (anlegen, umbenennen, Gruppe wechseln, ausblenden) geht nur in
  YNAB. ADR 0063 hatte das bewusst als Non-Goal gesetzt.
- **Von Hand buchen** geht nur in YNAB, etwa für das ING-Tagesgeld, das nicht automatisch
  kommt. Dabei sind Buchungen liegen geblieben, und die Zahlen stimmen nicht mehr.
- **Geldmarkt-ETF** abgleichen und **Reports** laufen in YNAB.

Zweites Ergebnis: BB fühlt sich nicht rund an. Sync, Regeln, Quick Add, Kassenbuch und
Budget sind nacheinander entstanden. Roman benennt drei Ebenen:

1. **Uneinheitlich**: Die Historie sieht im Kassenbuch anders aus als im Budget. Die
   Kontowahl am Handy ist mal eine Pillenleiste, mal eine Selectbox. Orange steht mal für
   eine Warnung, mal für einen Button. ADR 0075 hat Farbrollen eingeführt, deckt aber nicht
   alle Fälle ab, und neuere Seiten weichen ab.
2. **Ungeführt**: große Leerflächen am PC, ein ungeordnetes Seitenpanel im Kassenbuch, nicht
   sofort sichtbar, wo was ist.
3. **Zerschnitten**: Import und Quick Add sind eigene Inseln. Der Einstieg ist der Sync, weil
   er zuerst da war.

Gedankenexperiment: Mit allen Funktionen, aber uneinheitlich, würde Roman BB zu 95 %
nutzen. Die Funktionen entscheiden also über die Nutzung, die Einheitlichkeit über das
Gefühl. Zeit und Tokens sind begrenzt, und eine Flut von Backlog-Tickets nimmt Roman den
Überblick.

## Decision
1. **Ziel ist 95 % des Alltags in BB, als ein Produkt aus einer Hand.** YNAB bleibt Spiegel
   und Ort für Reports, bis BB sie hat.
2. **BB hat drei Orte:** **Übersicht** (Einstieg: wo stehe ich, was steht an), **Kassenbuch**
   (alle Buchungen; dorthin wandern Import, Durchgehen neuer Buchungen, Regeln und Erfassen)
   und **Budget**. Quick Add hört auf, ein eigener Ort zu sein. Sein Versprechen (wenige
   Klicks, kluge Vorbelegung) gilt für jedes Erfassen.
3. **Fahrplan in drei Etappen, in dieser Reihenfolge:**
   - **Etappe 1 · Bedeutungen festlegen.** Kein ganzer Styleguide, sondern die Bedeutungsschicht:
     welche Farbe was heißt (aufbauend auf ADR 0075) und die wenigen Muster, die heute
     auseinanderlaufen (Auswahl aus wenigen Optionen, Historie, Seitenpanel, Liste mit
     Aktionen, leerer Zustand). Dazu der Zuschnitt in die drei Orte als Skizze. Klein
     halten. Die Etappe verhindert, dass neue Funktionen eine weitere Variante bauen.
   - **Etappe 2 · Lücken schließen, die zu YNAB zwingen.** Zuerst **Buchung erfassen** im
     Kassenbuch für jedes Konto, dann **Kategorien verwalten** (anlegen, umbenennen, Gruppe
     wechseln, deaktivieren). Beides wird gegen Etappe 1 gebaut.
   - **Etappe 3 · Ein Produkt.** Die drei Orte umsetzen, Seite für Seite an einer Skizze:
     Übersicht als Einstieg, Import mit Durchgehen und Regeln ins Kassenbuch, Budget an die
     Bedeutungen angleichen, Handy-Macken beheben, neues Logo.
4. **Kategorie-Verwaltung ist kein Non-Goal mehr** (ändert ADR 0063 an dieser Stelle).
   Kategorien werden **deaktiviert**, nicht gelöscht, damit ihre Historie erhalten bleibt.
5. **Lena bekommt keine eigene Identität.** Sie sieht dasselbe wie Roman. Dazu kommt ein
   Filter auf das, was ihr zugewiesen ist. Multi-User bleibt Non-Goal.
6. **Fahrplan statt Backlog-Flut.** Verfeinert wird nur die nächste Etappe. Ideen für spätere
   Etappen werden eine Zeile im Fahrplan, kein Ticket. Die bestehenden Backlog-Tasks werden
   in einer Modeling-Runde den Etappen zugeordnet oder fallen weg.
7. **Nach den drei Etappen, bei Bedarf:** Reports, Budget-Stufe 3 (erwartete Einnahme,
   Leihen, Entwurf, Targets mit Deckel), ETF-Bewertung, ING-Import.

## Consequences
### Positive
- Die Spannung „erst Funktionen oder erst UX“ ist aufgelöst. Eine kleine Bedeutungsschicht
  vorab ist billiger als neue Funktionen, die man danach glattziehen muss.
- Das Erfassen kommt vor den Kategorien, weil seine Lücke heute die Zahlen verfälscht.
- Der Fahrplan begrenzt, was verfeinert wird. Roman behält den Überblick.

### Negative
- Budget-Stufe 3, für die das Budget ursprünglich nach BB kam, rückt nach hinten.
- Etappe 3 baut bestehende Seiten um. Ein Teil der jüngsten Arbeit am Budget-Aussehen wird
  angepasst.
- Kategorie-Verwaltung bedeutet: BB legt Kategorien in YNAB an und benennt sie dort um.
  Das ist ein neuer Schreibweg in `ynab-sync`.

### Neutral
- Open Source, FinTS und ein allgemeiner Bankimport bleiben ausdrücklich außen vor.
- Reports bleiben in YNAB, solange BB spiegelt.
