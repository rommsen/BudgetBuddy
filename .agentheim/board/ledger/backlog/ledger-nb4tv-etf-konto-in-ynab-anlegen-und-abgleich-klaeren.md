---
id: ledger-nb4tv
title: "Geldmarkt-ETF als Konto anlegen und den monatlichen Abgleich klären (offenes v1-Kriterium)"
status: backlog
type: decision
context: ledger
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [etappe-2, vision, v1, etf, kontostand, research, valuation]
related_adrs: [0016]
related_research: []
prior_art: [ledger-zm0zx]
---

## Why
Die Vision nennt als v1-Kriterium: "Das Budget-Loch durch den ETF ist geschlossen, in v1
über das manuelle YNAB-Konto." Dahinter steht das Kernproblem der Vision: seit dem Umstieg
von Tagesgeld auf einen Geldmarkt-ETF im ING-Depot liegt **Budget-Geld** an einer Stelle,
die YNAB nicht selbst bewerten kann. Bis das Konto existiert, ist das Budget um diesen
Posten falsch und driftet weiter.

**Befund vom 2026-09-12** (Romans YNAB-Referenz für Ablauf 6 der UX-Session): Das Konto
ist in YNAB **noch gar nicht angelegt**. Roman dazu: "Da müsste ich mir dann überlegen,
jedes Mal ein Reconcile zu machen, einmal im Monat oder so. Habe ich aber noch ehrlich
gesagt keine Idee für."

Damit ist ein v1-Kriterium offen, ohne dass es irgendwo als Aufgabe stand. Es ist auch
keine reine Code-Aufgabe: der erste Schritt ist eine Entscheidung darüber, wie der
Abgleich im Alltag aussehen soll.

## What
Mit Roman zu klären, in dieser Reihenfolge:

1. **Wie wird der Wert gepflegt?** Die Vision sagt für v1: YNAB führt das Konto manuell,
   Roman gleicht monatlich auf Stückzahl mal Kurs ab. Offen ist, was "abgleichen" praktisch
   heisst: ein YNAB-Reconcile mit eingetragenem Zielsaldo, eine Korrekturbuchung, oder
   etwas anderes.
2. **Was macht BB dabei?** In v1 spiegelt BB das Konto wie jedes andere und rechnet nicht
   selbst (ADR 0016: die Bewertung in BB ist eine spätere BB-geführte Fähigkeit). Zu
   prüfen ist, ob BB den Abgleich wenigstens **erinnern** oder **vorbereiten** kann, etwa
   indem es den fälligen Termin und den zuletzt bekannten Kurs zeigt.
3. **Woher kommt der Kurs?** Hängt an `ledger-c7j8e` (Kursquelle per ISIN, Research-Spike).
   Für v1 reicht möglicherweise ein von Hand abgelesener Wert; dann bleibt c7j8e v2.
4. **Wann ist das Kriterium erfüllt?** Konkret: das Konto existiert in YNAB, BB spiegelt
   es mit richtigem Stand, und es gibt einen Ablauf, den Roman monatlich tatsächlich
   durchführt. Ein Konto, das nach zwei Monaten veraltet ist, schliesst das Budget-Loch
   nicht.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured am 2026-09-12 aus der YNAB-Referenz für Ablauf 6. Der UX-Entwurf zeigt den ETF
als viertes Konto mit Stand (erfundener Wert 12.004,88 €), damit die Sicht vollständig
ist; das ist unabhängig davon, ob das echte Konto schon existiert.

Zusammenhang: `ledger-c7j8e` (Kursquelle), ADR 0016 (Bewertung als spätere BB-geführte
Fähigkeit), `ledger-zm0zx` (Kontomodell und Bewertung).

**Dies ist der erste Task, der ein v1-Kriterium betrifft, das bisher nirgends als Aufgabe
stand.** Beim Verfeinern lohnt ein Blick, ob die Vision weitere solche Lücken hat.

## Hinweis Triage 2026-11-06
Der Research-Spike zur Kursquelle (ledger-c7j8e) ist hier als Notiz aufgenommen. Er wird erst relevant, wenn die Entscheidung zum Abgleich eine BB-geführte Bewertung wählt.

## Aufgenommen aus ledger-c7j8e (Triage 2026-11-06)
**Kursquelle für Geldmarkt-ETF per ISIN (Research-Spike)** (spike, erfasst 2026-09-06)

### Why
ADR 0016 sieht eine spätere BB-geführte Bewertung des Geldmarkt-ETF vor
(Stückzahl mal Kurs, Buchung "Bewertungsänderung", `ValuationRecorded`), aber nicht in v1.
In v1 läuft der Abgleich manuell in YNAB. Voraussetzung der späteren Fähigkeit ist eine
automatische Kursquelle per ISIN. ADR 0016 lehnt eine automatische Quelle als v1-Pflicht
ab, weil keine öffentliche Quelle ohne Prüfung als robust gelten kann. Dieser Spike ist
der Research-Auftrag, der diese Prüfung nachholt, bevor die Kursquelle implementiert wird.

### What
Research-Auftrag, kein Code. Kandidaten für kostenfreie Kurs-APIs, die einen Kurs per
ISIN liefern (z. B. für ETF/Geldmarktfonds), sind zu identifizieren und **zu verifizieren**:
Verfügbarkeit ohne Kosten, Rate-Limits, Antwortformat, Abdeckung der konkreten ISIN des
Geldmarkt-ETF, Stabilität/Wartungsstatus der API. Scraping von Webseiten ist ausdrücklich
ausgeschlossen (keine robuste, erlaubte Quelle). Zusätzlich zu klären: Fallback-Verhalten
bei Ausfall der Quelle (ADR 0016: letzter Kurs bleibt stehen, wird als veraltet markiert)
und ob eine einzelne Quelle reicht oder eine Kaskade (mehrere Kandidaten) nötig ist.

Dieser Spike ist Voraussetzung der späteren BB-geführten Bewertung (ADR 0016), nicht von
v1. Er blockiert nichts Aktuelles.

### Acceptance criteria
- [ ] Mindestens zwei bis drei kostenfreie ISIN-Kurs-API-Kandidaten recherchiert und mit
      Quelle belegt (Dokumentation, nicht nur Werbeaussage).
- [ ] Für jeden Kandidaten: Kosten, Rate-Limit, Antwortformat, Abdeckung der ETF-ISIN,
      Wartungsstatus geprüft und im Research-Report festgehalten.
- [ ] Scraping-Optionen ausdrücklich ausgeschlossen und begründet, warum.
- [ ] Empfehlung für Fallback/Robustheit (Kaskade oder Einzelquelle, Umgang mit Ausfall)
      formuliert.
- [ ] Research-Report unter `.agentheim/knowledge/research/` abgelegt.

### Notes
Kontext: ADR 0016 (`0016-kontenmodell-und-bewertung-als-buchung.md`), Aufgabe
`ledger-zm0zx`. Die Bewertung selbst (Buchung, Event `ValuationRecorded`) ist bereits
entschieden; dieser Spike liefert nur die Kursquelle als Zulieferer dafür.
