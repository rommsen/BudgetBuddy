# Idee: BudgetBuddy als YNAB-Ersatz

**Status:** Idee, noch nicht entschieden
**Erfasst:** 2026-05-09
**Kontext:** Sokratischer Dialog zur Frage „komplett von YNAB weg, BB zum vollwertigen Budget-Tool ausbauen?"

---

## Vision

BudgetBuddy von einem YNAB-Companion (Import + Categorize + Push) zu einem vollwertigen Budget-Tool weiterentwickeln, das YNAB ersetzt. Source of Truth wird die eigene SQLite-DB. YNAB wird abgelöst.

Antrieb: **fertiges Tool**, kein Hobby-Projekt. Heißt: jedes Feature, das nicht real gebraucht wird, fliegt raus.

---

## Scope & Constraints (aus dem Dialog herausdestilliert)

| Frage | Antwort | Konsequenz |
|---|---|---|
| Daily Driver? | Plan-Änderungen, Targets, Pausieren, Geld schieben — am **PC** | Mobile-Budgeting fällt aus dem MVP raus |
| Mobile-Nutzung? | 75 %, aber nur Read + Bar-Eingabe + Kategorisieren | Bestehender BB-Stack reicht, +1 Feature: manuelle Transaktion-Eingabe |
| Multi-User? | Frau nutzt Family-Account, trägt nur Bar ein und kategorisiert. Keine Plan-Änderungen. 8 Jahre keine Konflikte | Kein Konflikt-Modell, gemeinsamer Account reicht. Optional später Tailscale-Identity als Soft-Audit |
| Tool-Bauen oder fertiges Tool? | Fertiges Tool | Kein Über-Engineering, Reports erst wenn vermisst |
| Plan B bei Scheitern? | Aktueller BB-Stand + YNAB + Reconciliation | Phase 2 muss parallel zu YNAB schreiben → Notausgang lebendig halten |
| Toleranz für unfertiges Tool? | Sehr niedrig — lieber zahlen als unfertig nutzen | Übergang muss parallel laufen, kein „bigbang" |

---

## Goal-Modell (klein gehalten, weil Roman nur 2 Typen real nutzt)

```fsharp
type GoalRecurrence = Once | Monthly | Quarterly | Yearly

type Goal =
    | MonthlyContribution of amount: Money
        // YNAB Typ 1/3 ("Set aside another"): jeden Monat X € obendrauf, Balance akkumuliert.
        // Verwendung: Sinking Funds (Auto, Geschenke, Urlaub).

    | TargetByDate of amount: Money
                    * date: DateTime
                    * recurrence: GoalRecurrence
        // YNAB Typ 4: bis Datum X € erreichen, optional wiederkehrend (monatlich/quartal/jährlich).

type CategoryGoal = {
    CategoryId: YnabCategoryId
    Goal: Goal
    IsPaused: bool
}
```

**Bewusst NICHT enthalten:**
- „Refill up to" (YNAB Typ 2) — Roman nutzt es nicht, manuelle Allokation reicht
- Underfunded-/Carry-Over-Berechnungen über das Minimum hinaus
- Goals mit komplexen Bedingungen

---

## Phasenmodell

| Phase | Inhalt | Wochenenden | Akzeptanz-Ende |
|---|---|---|---|
| **0** | Manuelle Transaction-Eingabe (für Frau-Workflow + Bar). Standalone-nutzbar, unabhängig vom Rest. | 1 | Frau kann am Phone eine Bar-Ausgabe in BB eintragen, wird zu YNAB synchronisiert |
| **1** | Eigene Categories + Monthly Allocations als **Read-only-Mirror** aus YNAB. Desktop-First Budget-View. UX-Hypothese-Test: „Ist meine Sicht besser als YNAB-Web?" | 2 | BB zeigt aktuellen Monat synchron zu YNAB, Roman arbeitet aber weiter in YNAB |
| **2 — Kern** | Move Money + Goals (setzen / pausieren / ändern) als **Parallel-Write** zu YNAB. Roman arbeitet primär in BB, YNAB wird via API mitgezogen. | 3 | 4–6 Wochen Daily-Use ohne Diskrepanzen YNAB↔BB |
| **3** | Cutover: BB wird Source of Truth. Rolling CSV-Export im YNAB-Format als Disaster-Recovery. Reconciliation-View (1×/Jahr → simpel). YNAB-Sub kündigen. | 1.5 | YNAB abgeschaltet, kein Daten-Verlust |
| **4** | Reports & Polish — strikt on-demand, nichts spekulativ | laufend | — |

**Gesamtschätzung:** 7–9 Wochenenden bis Cutover.

---

## Notausgang & Abbruchkriterien

- **Phase 0–2:** YNAB läuft parallel. Abbruch jederzeit ohne Datenverlust möglich.
- **Phase 1 → 2:** Wenn nach 2 Wochen Phase 1 die UX-Hypothese nicht trägt („YNAB-Web war doch besser") → Abbruch, Sunk Cost = 2 Wochenenden.
- **Phase 2 → 3:** Wenn nach 4–6 Wochen täglicher Nutzung Diskrepanzen zwischen BB und YNAB auftreten → Abbruch, weiter mit YNAB.
- **Phase 3 → 4:** Nach Cutover muss CSV-Export im YNAB-Format laufen, damit ein Rückzug auch nach 6 Monaten noch möglich ist (zur Not via YNAB-Reimport).

**Harte Abbruch-Trigger (vorab definieren):**
- Diskrepanz BB↔YNAB an 3 Tagen in Folge unerklärt → Stop
- Aufwand reißt 12 Wochenenden ohne Phase-3-Erreichen → Stop, weiter mit YNAB

---

## Offene Entscheidungen

Diese müssen vor Implementierung von Phase 1 geklärt werden:

1. **Allocation-Datenmodell — `(CategoryId, Month) → Amount` oder Event-basiert (Allocation-Log)?**
   Event-basiert ist sauberer für Audit/Undo, einfaches Modell ist schneller zu bauen. Default-Empfehlung: einfaches Modell, später migrierbar.

2. **Wie gleichen wir BB↔YNAB ab in Phase 2?**
   - *Optimistisch:* BB schreibt zuerst in eigene DB, dann async YNAB-API-Call. Bei API-Fehler: Inconsistency-Anzeige.
   - *Pessimistisch:* erst YNAB-API, dann eigene DB. Bei Fehler: kein Schreibvorgang.
   Default-Empfehlung: optimistisch + Reconciliation-Indikator („YNAB out of sync").

3. **YNAB-API-Rate-Limit reicht für Parallel-Write?**
   200 requests / hour pro Token (laut YNAB-Doku). Bei normalem Daily-Use unkritisch. Bei Bulk-Operationen (initial seed) Rate-Limit-Handler nötig.

4. **Soll Phase 0 (manuelle Transaktion-Eingabe) auch in YNAB pushen, oder erst lokal?**
   Wenn Frau Bar-Ausgaben einträgt, müssen die nach YNAB synchronisieren — sonst entstehen schon vor Phase 2 Diskrepanzen.

---

## Nächster Schritt — wenn Idee aktiviert wird

1. Phase 0 als Spec via `/spec-builder` → manuelle Transaction-Eingabe inkl. Sync zu YNAB
2. Implementieren
3. Entscheidung „weiter mit Phase 1 oder reicht Phase 0 als kleiner Quality-of-Life-Gewinn?"

Phase 0 ist der niedrigschwellige Test: bringt unmittelbaren Nutzen für Frau-Workflow und gibt ein Gefühl, ob das Projekt sich gut anfühlt — bevor Phase-1-Aufwand gerechtfertigt werden muss.

---

## Referenzen

- Dialog-Stand: 2026-05-09
- Aktueller BB-Stand: Comdirect-Import + Auto-Categorize + YNAB-Push, Single-User, ~88 Tests
- YNAB-Pricing zum Zeitpunkt der Idee: ca. 180 €/Jahr (Vergleichsgröße für Aufwand-Nutzen-Abwägung)
