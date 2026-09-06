---
id: ledger-zm0zx
title: Kontenmodell und Bewertung festlegen (Konto-Arten, BB-eigene Konten, Kursquelle)
status: todo
type: decision
context: ledger
created: 2026-09-06
depends_on: []
blocks: []
tags: [foundation, domain]
related_adrs: [0012]
related_research: []
prior_art: []
---

## Why
v1 der Vision heißt "alle Konten unter einem Dach", inklusive des Geldmarkt-ETF im
ING-Depot als **Budget-Konto**, das YNAB nicht führen kann. Das Kassenbuch braucht deshalb
ein Kontenmodell, das gespiegelte YNAB-Konten und BB-eigene Konten gleich behandelt, und
eine Bewertung (Stückzahl mal Kurs) mit einer Kursquelle. Diese Entscheidung ist nicht
Voraussetzung für den Skeleton (der spiegelt nur), aber für v1.

## What
ADR aus dem Entwurf unten committen. Kein Code. Die Kursquelle wird im ADR als
Schnittstelle festgelegt; die konkrete automatische Quelle ist ein Folge-Spike mit
Research, weil keine öffentliche Quelle ohne Prüfung als robust gelten kann.

Routing: **BC-lokal (`ledger`)**. Konten und Bewertung existieren nur im Kassenbuch.

## Acceptance criteria
- [ ] ADR committed, `scope: ledger`.
- [ ] Das ADR benennt: Konto-Arten, wie ein BB-eigenes Konto neben gespiegelten Konten
      geführt wird, wie eine Bewertungsänderung in Historie und Kontostand erscheint, und
      dass Kurs-Eingabe von Hand der v1-Boden ist, automatische Quelle ein Aufsatz.
- [ ] Ein Folge-Spike "Kursquelle für Geldmarkt-ETF per ISIN" ist im `ledger`-Backlog
      erfasst (Research-Auftrag, Kandidaten als "zu verifizieren").
- [ ] Offene Frage "Bar- und Quick-Add-Konto: eins oder zwei?" mit Roman entschieden
      und im ADR festgehalten.
- [ ] Keine Code-Änderung.

## Notes
### ADR-Entwurf (Architekten-Pass, 2026-09-06)

```markdown
---
id: NNNN
title: Kontenmodell: Konto-Art, Verknüpfung (YNAB-gespiegelt oder BB-eigen) und Bewertung als Buchung
scope: ledger
status: proposed
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ledger-zm0zx]
related_research: []
---

## Context
Das Kassenbuch soll alle Konten zeigen: Comdirect, Bar, ING-Konten (alle über den YNAB-
Spiegel) und das Wertpapierkonto mit Budget-Geld (Geldmarkt-ETF, ING-Depot), das YNAB
nicht kennt. Roman pflegt die Stückzahl von Hand; ein Kurs pro Monat reicht. Depots als
Vermögen sind kein Konto im Kassenbuch (künftiger Context `wealth`).

## Decision
- **Konto** trägt eine **Art** (Bankkonto | Bar | Wertpapierkonto mit Budget-Geld) und
  eine **Verknüpfung** (YNAB-gespiegelt mit YnabAccountId | BB-eigen). Beide sind
  orthogonal; heute sind alle Bank- und Bar-Konten gespiegelt, das ETF-Konto BB-eigen.
- Ein **Wertpapierkonto** hält Papier (ISIN, Name) und Stückzahl; der Kontostand ist die
  Summe seiner Buchungen, wie bei jedem Konto.
- **Bewertung** erzeugt eine Buchung der Art "Bewertungsänderung" (Betrag = neuer Wert
  minus alter Wert, Herkunft: Bewertung mit Kurs, Datum, Quelle). Damit ist der
  Kontostand korrekt und die Historie vollständig; keine Sonderrechnung an der Anzeige.
- **Kursquelle** ist eine Schnittstelle mit zwei Implementierungen: **Eingabe von Hand**
  (v1-Boden, ein Feld pro Monat) und **automatischer Abruf** (Aufsatz nach Research;
  Kandidaten wie kostenfreie Kurs-APIs per ISIN sind zu verifizieren, Scraping ist
  ausgeschlossen). Fällt der Abruf aus, bleibt der letzte Kurs stehen und wird als
  veraltet markiert.
- Ein BB-eigenes Konto wird nie nach YNAB gepusht; seine Buchungen tragen keine ImportId.

## Consequences
### Positive
- Das ETF-Loch schließt sich in v1 ohne YNAB-Beteiligung und ohne externe Abhängigkeit.
- Gesamtsicht und Kontostände folgen einer Regel für alle Konten.
### Negative
- Bewertungs-Buchungen sind Budget-Bewegungen (Kursgewinn erhöht Verfügbares). Solange
  Budgetieren in YNAB bleibt, sieht YNAB diese Bewegung nicht; die Abweichung zwischen
  BB-Gesamtstand und YNAB-Gesamtstand ist gewollt und muss als solche erklärt werden.
- Stückzahl von Hand: Käufe/Verkäufe im Depot müssen Roman einfallen.
### Neutral
- Bar- und Quick-Add-Konto: Entscheidung in der Task (eins oder zwei), Modell trägt beides.

## Alternatives considered
- **ETF als Tracking-Konto in YNAB und nur Anzeige in BB** — abgelehnt: Tracking-Konten
  zählen nicht ins Budget; das Loch bliebe.
- **Bewertung als berechneter Wert ohne Buchung** — abgelehnt: bricht die Regel "Kontostand
  = Summe der Buchungen" und die Historie.
- **Automatische Kursquelle als v1-Pflicht** — abgelehnt: keine Quelle ist ohne Prüfung
  robust; ein Feld pro Monat kostet Roman 30 Sekunden.
```
