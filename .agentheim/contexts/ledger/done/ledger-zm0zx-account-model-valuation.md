---
id: ledger-zm0zx
title: Kontenmodell und Bewertung festlegen (Konto-Arten, BB-eigene Konten, Kursquelle)
status: done
type: decision
context: ledger
created: 2026-09-06
completed: 2026-09-06
depends_on: []
blocks: []
tags: [foundation, domain]
related_adrs: [0012, 0016]
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

### Entscheidung mit Roman (2026-09-06, Work-Session)

Zwei Entscheidungen, die den Entwurf **deutlich verkleinern**; das ADR wird entsprechend
umgeschrieben, nicht nur ergänzt:

1. **Bar- und Quick-Add-Konto: eins.** Quick Add ist ein Eingabeweg (Herkunft), kein
   Konto. In YNAB gibt es das Konto Bargeld; `ynab_quickadd_account_id` (ADR 0004) zeigt
   darauf. Im Kassenbuch gibt es ein Konto "Bar"; Quick Add ist eine Herkunft an der
   Buchung. Die Vision-Zeile "heute in YNAB zwei" war ein Irrtum aus dem Brainstorm.
2. **ETF als manuelles YNAB-Konto, nicht als BB-eigenes Konto in v1.** YNAB kann ein
   manuelles On-Budget-Konto ohne Bankanbindung führen. Roman legt "Geldmarkt-ETF" mit
   dem heutigen Wert an und gleicht einmal im Monat auf Stückzahl mal Kurs ab; YNAB
   erzeugt die Saldo-Anpassung. Das Budget-Loch ist damit ab dem nächsten Monat zu, ohne
   Code, und YNABs Ready-to-Assign (die Zahl, mit der Roman bis nach v2 budgetiert) ist
   richtig. Der Entwurf hätte diese Zahl bis nach v2 falsch gelassen (sein eigener
   "Negative"-Punkt).
   - Folge: der ETF ist ein **gespiegeltes Konto wie alle anderen**. Das Kontomodell
     (Art × Verknüpfung) bleibt wie im Entwurf, "BB-eigen" ist modelliert, hat aber in v1
     keine Instanz.
   - **Bewertung** wird später eine **BB-geführte Fähigkeit** nach dem Vision-Muster: BB
     rechnet Stückzahl mal Kurs, schreibt `ValuationRecorded` ins Kassenbuch und pusht die
     Anpassung nach YNAB. Kursquelle ist erst dann Thema. Das ADR beschreibt diese
     Fähigkeit als "vorgesehen, nicht v1"; der Folge-Spike "Kursquelle" wird trotzdem im
     Backlog angelegt (Acceptance Criterion), aber als Voraussetzung dieser späteren
     Fähigkeit, nicht von v1.
   - Der Worker prüft und benennt im ADR, wohin YNAB die Saldo-Anpassung eines
     Budget-Kontos bucht (Erwartung: "Inflow: Ready to Assign"); falls unklar, als "zu
     verifizieren" markieren, nicht raten.
3. Das Acceptance Criterion "Kurs-Eingabe von Hand ist der v1-Boden" wird damit zu
   "Kurs-Eingabe von Hand in YNAB (Abgleich) ist der v1-Boden; BB-seitige Eingabe kommt
   mit der BB-geführten Bewertung".

**Verifiziert (Conductor, 2026-09-06):** YNAB-Support bestätigt, dass eine
Reconciliation Balance Adjustment auf einem Budget-Konto den Betrag aus dem Kontostand
**und aus Ready to Assign** zieht bzw. dorthin gibt. Quellen:
https://support.ynab.com/en_us/balance-adjustments-a-guide-rko4OwILs und
https://support.ynab.com/en_us/reconciling-accounts-a-guide-BJFE3fHys. Punkt 2 im ADR
kann das als gesichert nennen (mit Quelle), nicht als "zu verifizieren".

## Outcome
ADR 0016 (`0016-kontenmodell-und-bewertung-als-buchung.md`) committed: Kontenmodell
Art x Verknüpfung bleibt, hat in v1 aber keine BB-eigene Instanz. Bar und Quick-Add sind
ein Konto ("Bar"), Quick Add ist eine Herkunft an der Buchung. Der ETF wird in v1 als
manuelles YNAB-Konto geführt (Reconciliation, Ready to Assign bleibt korrekt), nicht als
BB-eigenes Konto. Bewertung als BB-geführte Fähigkeit (`ValuationRecorded`, Buchung
"Bewertungsänderung") ist vorgesehen, nicht v1. Folge-Spike `ledger-c7j8e` im Backlog
angelegt (Kursquelle per ISIN, Research). README-Bullets "Kursquelle" und "Bar- und
Quick-Add-Konto" in `## Open questions` auf gelöst gesetzt.
