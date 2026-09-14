---
id: ledger-c7j8e
title: Kursquelle für Geldmarkt-ETF per ISIN (Research-Spike)
status: backlog
type: spike
context: ledger
created: 2026-09-06
depends_on: [ledger-zm0zx]
blocks: []
tags: [research, valuation]
related_adrs: [0016]
related_research: []
prior_art: []
---

## Why
ADR 0016 sieht eine spätere BB-geführte Bewertung des Geldmarkt-ETF vor
(Stückzahl mal Kurs, Buchung "Bewertungsänderung", `ValuationRecorded`), aber nicht in v1.
In v1 läuft der Abgleich manuell in YNAB. Voraussetzung der späteren Fähigkeit ist eine
automatische Kursquelle per ISIN. ADR 0016 lehnt eine automatische Quelle als v1-Pflicht
ab, weil keine öffentliche Quelle ohne Prüfung als robust gelten kann. Dieser Spike ist
der Research-Auftrag, der diese Prüfung nachholt, bevor die Kursquelle implementiert wird.

## What
Research-Auftrag, kein Code. Kandidaten für kostenfreie Kurs-APIs, die einen Kurs per
ISIN liefern (z. B. für ETF/Geldmarktfonds), sind zu identifizieren und **zu verifizieren**:
Verfügbarkeit ohne Kosten, Rate-Limits, Antwortformat, Abdeckung der konkreten ISIN des
Geldmarkt-ETF, Stabilität/Wartungsstatus der API. Scraping von Webseiten ist ausdrücklich
ausgeschlossen (keine robuste, erlaubte Quelle). Zusätzlich zu klären: Fallback-Verhalten
bei Ausfall der Quelle (ADR 0016: letzter Kurs bleibt stehen, wird als veraltet markiert)
und ob eine einzelne Quelle reicht oder eine Kaskade (mehrere Kandidaten) nötig ist.

Dieser Spike ist Voraussetzung der späteren BB-geführten Bewertung (ADR 0016), nicht von
v1. Er blockiert nichts Aktuelles.

## Acceptance criteria
- [ ] Mindestens zwei bis drei kostenfreie ISIN-Kurs-API-Kandidaten recherchiert und mit
      Quelle belegt (Dokumentation, nicht nur Werbeaussage).
- [ ] Für jeden Kandidaten: Kosten, Rate-Limit, Antwortformat, Abdeckung der ETF-ISIN,
      Wartungsstatus geprüft und im Research-Report festgehalten.
- [ ] Scraping-Optionen ausdrücklich ausgeschlossen und begründet, warum.
- [ ] Empfehlung für Fallback/Robustheit (Kaskade oder Einzelquelle, Umgang mit Ausfall)
      formuliert.
- [ ] Research-Report unter `.agentheim/knowledge/research/` abgelegt.

## Notes
Kontext: ADR 0016 (`0016-kontenmodell-und-bewertung-als-buchung.md`), Aufgabe
`ledger-zm0zx`. Die Bewertung selbst (Buchung, Event `ValuationRecorded`) ist bereits
entschieden; dieser Spike liefert nur die Kursquelle als Zulieferer dafür.
