---
id: 0076
title: Nachtrag zu ADR 0074. Der Monatsrahmen liegt je Seitenaufruf im Client, zusammengeführt wird dort, kein Server-Cache
scope: budgeting
status: accepted
date: 2026-11-02
supersedes: []
superseded_by: []
related_tasks: [budgeting-g8s3w]
related_adrs: [0074, 0065]
related_research: [ynab-reihenfolge-monate-target-schreiben-2026-11-02]
---

# ADR 0076: Nachtrag zu ADR 0074, Monatsrahmen

## Context
ADR 0074 legt fest, dass das Monatsfenster aus `GET /months` und die Reihenfolge aus `GET /categories` kommen, sagt aber nicht, wann BB sie liest und wo sie mit den Monatswerten zusammenkommen. Lägen beide Quellen in `getBudgetMonth`, kostete jeder Monatswechsel drei statt einer Anfrage gegen das YNAB-Limit.

## Decision
1. **Eigenes Endpoint `BudgetApi.getBudgetFrame`.** Es liefert `BudgetFrame` mit laufendem Monat, Fenster und Reihenfolge. `BudgetMonth` bleibt unverändert.
2. **Der Rahmen liegt je Seitenaufruf im Client-Modell.** Ein Seitenaufruf kostet drei Anfragen, Monatswechsel und Neu-Lesen eine, die Rückkehr aus dem Kassenbuch eine.
3. **Zusammengeführt wird im Client.** `Shared.Budgeting.orderGroups` läuft in `applyLoaded`, bevor der Monat ins Modell kommt. `toBudgetMonth` bleibt für die Server-Abläufe in der Folge der Monatsantwort.
4. **Die Vorschau wird nur in der View sortiert.** `TargetPreview.Lines` bleibt unangetastet, weil `previewsMatch` reihenfolgetreu vergleicht und sonst bei jedem Übernehmen `PreviewChanged` meldete.
5. **Kein Server-Cache.** Er brächte Zustand samt Invalidierung für zwei gesparte Anfragen.
6. **Der Rahmen wird still nachgelesen, wenn das Fenster gewachsen sein kann.** Das gilt nach einem Bericht mit geschriebener Zeile ungleich 0 im letzten Monat. Bei unbekanntem Ausgang reicht, dass der gezeigte Monat der letzte ist.

## Consequences
### Positive
- Blättern kostet je Klick genau eine YNAB-Anfrage.
- `YnabPort` und die Fakes der Befehle bleiben unberührt, der Rahmen hat seinen eigenen `BudgetFramePort`.

### Negative
- Ein Fenster, das in YNAB selbst wächst, sieht BB erst beim nächsten Seitenaufruf.
- Langes Blättern kostet je Klick eine Anfrage. Ein Debounce kommt erst, wenn es stört.

### Neutral
- Ein offener Tab aus der Zeit vor dem Update sendet "current" und bekommt "Ungültiger Monat", bis die App neu lädt.

## Alternatives considered
- **Felder in `BudgetMonth`.** Verworfen: jeder Monatswechsel kostete zwei bis drei Anfragen.
- **Ordnen auf dem Server.** Verworfen: die Server-Abläufe brauchen die Reihenfolge nicht und zahlten sonst bei jedem Lesen eine weitere Anfrage.