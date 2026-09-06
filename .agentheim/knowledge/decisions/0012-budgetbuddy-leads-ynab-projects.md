---
id: 0012
title: BudgetBuddy wird führendes System, YNAB Projektion und Fallback (löst ADR 0001 ab)
scope: global
status: accepted
date: 2026-09-06
supersedes: [0001]
superseded_by: []
related_tasks: []
related_research: []
---

# ADR 0012: BudgetBuddy wird führendes System, YNAB Projektion und Fallback

## Context
ADR 0001 (2026-06-01) legte BudgetBuddy als YNAB-Companion fest: YNAB bleibt Source of
Truth, eine eigene Wahrheit ist Non-Goal. ADR 0003 öffnete diese Grenze einen Spalt
(Quick Add als Phase-0-Experiment). Seitdem hat sich die Lage verändert:

- Roman hat Tagesgeld in einen **Geldmarkt-ETF** (ING-Depot) umgeschichtet. Dieses Geld
  ist Budget-Geld, YNAB kann es aber nicht als Budget-Konto führen. Heute passiert damit
  nichts: das Budget ist um diesen Posten falsch und driftet.
- BudgetBuddy **behält keine Buchung**. Es entscheidet, pusht und vergisst. Alles, was
  in YNAB an BB vorbei passiert (ING-Import, Transfer-Erkennung, Split- und Kategorie-
  Korrekturen, Amazon-Bestellungen der Frau), ist in BB unsichtbar. Roman hat keine
  Ownership über seine Daten und keine auditierbare Historie.
- Die Abhängigkeit von YNAB (Kosten, Produktrichtung) wird als Risiko empfunden. Kein
  akuter Schmerz, aber ein Versicherungsmotiv.

Zugleich gilt: **Budgetieren tut nicht weh.** "Alle auswählen, Underfunded, fertig"
funktioniert gut. Ein Nachbau wäre reine Parität. Und die Toleranz für ein unfertiges
Tool ist niedrig (Mai-Dialog, `docs/idea-ynab-replacement.md`).

## Decision
BudgetBuddy wird das **führende System, Fähigkeit für Fähigkeit.** YNAB bleibt Projektion
und Fallback für alles, was BB noch nicht kann. Konkret:

1. BB bekommt ein eigenes, dauerhaftes **Kassenbuch** (neuer Bounded Context `ledger`)
   über alle Konten, mit **Herkunft** und **auditierbarer Historie** pro Buchung.
2. Das Kassenbuch startet als **Spiegel mit Gedächtnis**: es führt YNABs Zustand laufend
   nach (weil YNAB weiter an fünf Stellen Eingabeort ist) und merkt sich zusätzlich, was
   BB selbst entschieden hat. Abweichungen werden sichtbar, nicht still überschrieben.
3. Fähigkeiten wandern einzeln von **YNAB-geführt** zu **BB-geführt**. Heute BB-geführt:
   Import, Kategorisierung, Quick Add. Als nächstes: Nachbearbeitung (Kategorie, Split,
   Übertrag). Budgetieren erst nach bewährtem v2, und nur mit Kassenbuch-Grund.
4. Zwei Stufen: **v1** alle Konten unter einem Dach inkl. ETF als Budget-Konto; **v2**
   Nachbearbeitung in BB mit Historie ("das war es wert"). v2 ist das Gate für alles
   Weitere.
5. **Cutover ist eine Option, kein Ziel.** YNAB abschalten steht nicht in dieser Vision.

Die Scope-Formel aus ADR 0001 wird ersetzt: *In-Scope = eine Fähigkeit von YNAB nach BB
holen, so dass BB führt und YNAB nachgezogen wird, oder eine Fähigkeit, die YNAB nicht hat
und die das Kassenbuch braucht. Non-Goal = Parität ohne Kassenbuch-Grund.*

## Consequences
### Positive
- Das ETF-Loch im Budget wird geschlossen, ohne dass YNAB es können muss.
- Roman bekommt Ownership und eine Historie, die er heute nirgends hat.
- Jede Fähigkeit hat einen klaren Weg (YNAB-geführt → BB-geführt) und ein Vertrauens-
  Gate; kein Big Bang, jederzeit abbrechbar ohne Datenverlust, weil YNAB weiterläuft.
- Die Companion-Funktionen bleiben unverändert und werden zum ersten Fall des Musters.

### Negative
- Ein Spiegel, der YNAB laufend nachführt, ist Dauerbetrieb: Delta-Sync, Rate-Limits,
  Abweichungs-Erkennung. Das ist mehr Infrastruktur als bisher.
- Die Historie-Anforderung zieht eine Architekturentscheidung nach sich (Event Sourcing
  wurde vorgeschlagen), die für ein persönliches Tool schwer sein kann. Wird als
  Decision-Task im `ledger` entschieden, nicht hier.
- Zwei Systeme mit je eigener Sicht auf dieselben Buchungen: jede unerklärte Abweichung
  kostet Vertrauen. Die Abbruchkriterien aus dem Mai-Dialog bleiben Vorschlag und werden
  zu Beginn von v2 festgezurrt.
- Die Companion-Grenze aus ADR 0001 als Scope-Schutz fällt weg; an ihre Stelle tritt das
  Vertrauens-Gate v2 und die Regel "Parität ohne Kassenbuch-Grund ist Non-Goal".

### Neutral
- `docs/idea-ynab-replacement.md` (Phasenmodell vom Mai) bleibt als Referenz; sein
  Phasenmodell ist durch v1/v2 ersetzt, seine Abbruchkriterien und das Goal-Modell werden
  bei Bedarf wiederverwendet.
- ADR 0003 bleibt als historisches Amendment stehen; sein "Phase 1+ bleibt Non-Goal"
  ist durch dieses ADR aufgehoben.
- Depots (Vermögen), ING-Adapter, Amazon-Auslesen, lokale LLMs, Reports sind weiterhin
  Non-Goals für v1/v2.

## Alternatives considered
- **Companion bleiben (ADR 0001 beibehalten)** — abgelehnt: das ETF-Loch und die fehlende
  Ownership lassen sich innerhalb der Companion-Grenze nicht lösen.
- **Direkter Cutover nach dem Mai-Phasenmodell (Budget-View → Parallel-Write → Cutover)**
  — abgelehnt: setzt beim Budgetieren an, das nicht weh tut, und übersieht, dass BB gar
  kein Gedächtnis hat. Das Kassenbuch muss zuerst kommen.
- **Reiner Read-only-Spiegel ohne eigene Führung** — abgelehnt: ein Spiegel kann YNAB nie
  widersprechen; ohne "BB entscheidet, YNAB wird nachgezogen" gibt es keine Ownership und
  keinen Grund, das Ding zu bauen.

## References
- `.agentheim/vision.md` (Revision 2026-09-06)
- `.agentheim/context-map.md`
- ADR 0001, ADR 0003
- `docs/idea-ynab-replacement.md`
