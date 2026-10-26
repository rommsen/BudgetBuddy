---
id: budgeting-h2zfn
title: "Targets anwenden: Vorschau mit Summe und Zu verteilen danach, Übernehmen schreibt die Zuweisungen nach YNAB, Budget-Bewegungen werden in BB festgehalten, Fehlgeschlagene erneut sendbar"
status: doing
type: feature
context: budgeting
created: 2026-10-26
completed:
depends_on: [budgeting-k7wmd, design-system-001]
blocks: []
tags: [budgeting, ui, ynab-write, targets, adr]
related_adrs: [0063, 0055, 0050, 0013]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-r4tkq]
---

## Why
Die Handlung, die Roman aus YNAB holt (Vision, Budget Stufe 1): am Monatsersten Targets mit
einem Knopf anwenden statt in YNAB alle Kategorien auszuwählen und "Underfunded" zu drücken.
Zugleich beginnt hier die Budget-Historie. YNAB liefert per API nicht, wer wann was
zugewiesen hat; eine erst in Stufe 2 beginnende Aufzeichnung wäre für die ersten Monate leer
und nicht nachholbar.

## What
**Entscheidungen von Roman (2026-10-26):** Vorschau, dann Übernehmen. Monat wählbar (der
Umschalter aus `budgeting-k7wmd`). Pausierte Targets (`goal_snoozed_at`) werden wie in YNAB
ausgelassen. Kein Warnen und keine Sperre, wenn Zu verteilen danach negativ ist, nur
anzeigen. Betrag je Kategorie = Zugewiesen + Fehlbetrag von YNAB (`goal_under_funded`), BB
rechnet keine Targets.

**Server**
- Domain (rein, ohne I/O): `planTargetApplication` nimmt das Monatsmodell aus
  `budgeting-k7wmd`. Aufgenommen wird jede Kategorie mit Fehlbetrag größer 0 und ohne
  pausiertes Target. Neu = altes Zugewiesen + Fehlbetrag, auf 10 Milliunits geprüft.
  Pausierte erscheinen separat als "übersprungen (pausiert)". Liefert die Summe und Zu
  verteilen danach (`ToBeBudgeted` minus Summe), darf negativ sein.
- Vorschau: reine Abfrage ohne Schreiben.
- Übernehmen: liest den Monat neu und vergleicht mit der Vorschau. Weicht etwas ab, wird
  nichts geschrieben und die neue Vorschau mit "Stand hat sich geändert" geliefert. Sonst
  werden zuerst alle Budget-Bewegungen festgehalten, dann PATCHes nacheinander gesendet
  (`PATCH /budgets/{id}/months/{YYYY-MM-01}/categories/{id}`, Body
  `{"category":{"budgeted":n}}`). Jeder Ausgang wird als eigene Ergebnis-Zeile
  festgehalten. Bei 429 Abbruch, der Rest als "fehlgeschlagen, Rate-Limit". Danach erneutes
  Lesen und Rückgabe des tatsächlichen Zu verteilen.
- Fehlgeschlagene erneut senden: live lesen, Zielwert erneut setzen. Steht YNAB weder auf
  Baseline (altes Zugewiesen) noch auf Ziel, gewinnt YNAB, die Zeile wird als "in YNAB
  geändert" geschlossen, nichts überschrieben (ADR 0055).
- Persistenz (SQLite/Dapper): Tabelle für unveränderliche Bewegungs-Zeilen und eine
  append-only Tabelle für Rückschreib-Ergebnisse, wie im ADR-Entwurf unten. Keine
  Historie-UI.
- Schreib-Sperre: eine zweite Anwendung für denselben Monat läuft nicht parallel (Wächter
  wie `MirrorGuard`, ADR 0055).

**Client** (auf der Budget-Seite)
- Knopf "Targets anwenden" öffnet die Vorschau: Kategorien mit Betrag, Summe, "Zu verteilen
  danach", Liste der übersprungenen pausierten. Knöpfe "Übernehmen" und "Abbrechen".
- Nach Übernehmen: "X von N übernommen", fehlgeschlagene Kategorien mit Grund,
  "Fehlgeschlagene erneut senden", tatsächliches Zu verteilen. Weicht es von der Vorschau
  ab, steht ein Hinweis dabei.

**Probe gegen das Wegwerf-Budget "Test"** (geschrieben wird nie in Romans echtem Budget):
eine Kategorie mit Target und teilweise zugewiesenem Betrag. Prüfen, ob
`budgeted + goal_under_funded` die Kategorie auf Kurs bringt (Fehlbetrag danach 0). Außerdem
ein pausiertes Target und, falls machbar, ein Target mit Deckel. Dokumentiert im Outcome und
per README_DELTA, kein automatisierter Test. Stimmt die Gleichung nicht: BOUNCE mit dem
Befund, nicht raten.

## Acceptance criteria
- [ ] `planTargetApplication` ist rein und getestet: Fehlbetrag größer 0 aufgenommen, 0 und null nicht, pausiertes übersprungen aber ausgewiesen, Neu = Alt + Fehlbetrag (auch bei Alt größer 0), Summe und Zu verteilen danach stimmen, negatives Ergebnis erlaubt, nicht auf 10 Milliunits liegende Beträge abgelehnt.
- [ ] Die Vorschau schreibt weder nach YNAB noch in die Datenbank (Fake-Client zählt Schreibaufrufe).
- [ ] Übernehmen mit abweichendem Live-Stand schreibt nichts und liefert die neue Vorschau (Fake-Client).
- [ ] Übernehmen hält zuerst alle Bewegungen fest, dann folgen die PATCHes. Schlägt der Fake-Client bei Kategorie k fehl: Bewegungen für alle Zeilen, "geschrieben" für die erfolgreichen, "fehlgeschlagen" mit Grund für k, die übrigen Zeilen werden trotzdem gesendet.
- [ ] Bei 429 bricht der Lauf ab, der Rest ist "fehlgeschlagen, Rate-Limit", bereits Geschriebenes bleibt unberührt.
- [ ] Erneut senden sendet nur Zeilen, deren jüngstes Ergebnis "fehlgeschlagen" ist. Live-Wert weder Baseline noch Ziel: "in YNAB geändert", nichts geschrieben. Erneutes Senden eines schon geschriebenen Ziels ändert nichts.
- [ ] Persistenz-Tests nur gegen In-Memory-SQLite (`Data Source=:memory:`) oder eine temporäre Test-DB, nie die Produktiv-DB. Bewegungs-Zeilen sind nach dem Schreiben nicht veränderbar, Ergebnisse werden nur angehängt.
- [ ] Der PATCH-Body ist `{"category":{"budgeted":<Zahl>}}` mit JSON-Zahl, kein String (Regressionstest mit Kommentar). Monatspfad `YYYY-MM-01`.
- [ ] Zwei gleichzeitige Anwendungen für denselben Monat laufen nicht ineinander (Wächter-Test).
- [ ] Negatives "Zu verteilen danach" erscheint ohne Warnung und Sperre, Übernehmen bleibt aktiv.
- [ ] Das tatsächliche Zu verteilen nach dem Lauf stimmt mit der Vorschau überein, sonst zeigt die Seite den Unterschied an (Test mit Fake-Client).
- [ ] Der Client nutzt nur DS-Komponenten (`Modal`, `Button`, `Money`, `Badge`, `ErrorDisplay`), gemessen am Styleguide.
- [ ] Die Vorschau ist am Handy-Format lesbar, Summe und "Zu verteilen danach" sind sichtbar, Übernehmen ist ohne Scrollen erreichbar. [human-eye]
- [ ] Probe gegen "Test" durchgeführt und im Outcome dokumentiert (vorher und nachher `budgeted`, `goal_under_funded`, `to_be_budgeted`, pausiertes Target, optional Deckel). In Romans echtem Budget wurde nichts geschrieben.
- [ ] ADR 0064 (Entwurf unten) im ADRS-Block, README_DELTA mit den Begriffen unten, Aggregat, Events, Commands und der geschlossenen offenen Frage zum Fehlbetrag.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün, Diary-Eintrag nennt die neuen Tests.

## Notes
- Muster: Ereignis vor Push und Fehlschlag mit Baseline (ADR 0050, ADR 0055), `RateLimitExceeded` und die 429-Zweige in `src/Server/YnabClient.fs`, `MirrorGuard`, Client-Struktur unter `src/Client/Components/Ledger/`.
- Spike: `.agentheim/knowledge/research/ynab-api-targets-zuweisung-2026-10-25.md`, Abschnitte 2 und 3. Absolute Werte, kein Sammelaufruf, YNAB rundet auf 10 Milliunits (12345 wurde 12350).
- Rate-Limit: etwa 100 PATCHes plus 2 GETs pro Lauf gegen etwa 200 Anfragen pro Stunde und Token. Sequentiell, Abbruch bei 429, kein Zähler, kein Parallelversand.
- Kein Design-Board (Roman, 2026-10-26).

**Begriffe für das `budgeting`-README (per README_DELTA):**
- **Fehlbetrag**: der Betrag, den YNAB für Kategorie und Monat als fehlend ausrechnet (`goal_under_funded`). BB übernimmt ihn und rechnet Targets in Stufe 1 nicht selbst.
- **Pausiertes Target**: Target mit gesetztem `goal_snoozed_at`. Zählt beim Anwenden nicht mit, wie bei YNABs Underfunded.
- **Vorschau** (Targets anwenden): Kategorien mit Betrag, Summe, Zu verteilen danach. Schreibt nichts. **Übernehmen** schreibt sie nach YNAB.
- **Anwendung**: ein Durchlauf von Targets anwenden für einen Monat, mit je einer Zeile pro Kategorie (altes und neues Zugewiesen). Eine Zeile ist geschrieben, fehlgeschlagen oder in YNAB geändert.
- **Budget-Bewegung** (Schärfung): in Stufe 1 nur durch Targets anwenden; trägt Zeitpunkt, Herkunft "Targets anwenden", Monat, Kategorie, altes und neues Zugewiesen.
- Aggregat **Anwendung** (Monat, Zeilen; Invarianten: nur Fehlbetrag größer 0 und nicht pausiert, Beträge auf 10 Milliunits, neu gleich alt plus Fehlbetrag). Events `TargetsAngewendet`, `ZuweisungNachYnabGeschrieben`, `ZuweisungRückschreibenFehlgeschlagen`. Commands `TargetsÜbernehmen`, `FehlgeschlageneErneutSenden`; die Vorschau ist eine Abfrage.

**ADR-Entwurf (für den ADRS-Block des Workers):**

```
---
id: 0064
title: Budget-Zustand in Stufe 1 live aus YNAB, Budget-Bewegungen als append-only Aufzeichnung in BB
scope: budgeting
status: accepted
date: 2026-10-26
supersedes: []
superseded_by: []
related_tasks: [budgeting-k7wmd, budgeting-h2zfn]
related_adrs: [0013, 0050, 0055, 0063]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
---

# ADR 0064: Budget-Zustand live aus YNAB, Budget-Bewegungen in BB

## Context
ADR 0063 macht das Budget zur nächsten Fähigkeit und setzt die Formel "BB führt, YNAB wird
nachgezogen". Stufe 1 (Budget-Seite, Targets anwenden) braucht zwei Antworten: woher die
Beträge kommen und wo festgehalten wird, was BB zugewiesen hat. Die Vision nennt als
Scheiter-Kriterium, dass BBs und YNABs Verfügbar sich unerklärt widersprechen.
Der Spike (budgeting-r4tkq) zeigt: GET /months/{month} liefert Zu verteilen, Zugewiesen,
Aktivität, Verfügbar und den Fehlbetrag je Kategorie. Zuweisen geht absolut per
PATCH /months/{month}/categories/{id}, eine Kategorie je Aufruf. YNAB liefert nicht, wer
wann was zugewiesen hat.

## Decision
1. **Beträge live aus YNAB.** Zu verteilen, Zugewiesen, Aktivität, Verfügbar und Fehlbetrag
   werden bei jedem Öffnen aus YNAB gelesen und nicht in BB gespeichert (wie "Available
   live", ADR 0055). Aktivität kommt in Stufe 1 ebenfalls von YNAB, nicht aus dem Kassenbuch.
   Wann BB selbst rechnet, bleibt offen.
2. **Budget-Bewegungen ab Stufe 1 in BB.** Targets anwenden hält je Kategorie eine
   unveränderliche Bewegung fest (Zeitpunkt, Herkunft, Monat, Kategorie, altes und neues
   Zugewiesen). Die Tabelle ist getrennt vom Kassenbuch-Ereignis-Log (ADR 0013).
   Rückschreib-Ergebnisse (geschrieben, fehlgeschlagen, in YNAB geändert) sind eigene
   angehängte Zeilen. Der Stand einer Bewegung ist ihr jüngstes Ergebnis.
3. **Reihenfolge wie ADR 0050:** erst alle Bewegungen festhalten, dann nacheinander
   senden. Ein Fehlschlag trägt als Baseline das alte Zugewiesen. Kein Rollback. Erneut
   senden ist idempotent, weil der Wert absolut gesetzt wird. Steht YNAB weder auf Baseline
   noch auf Ziel, gewinnt YNAB und die Zeile wird als "in YNAB geändert" geschlossen.
4. **Betrag je Kategorie ist altes Zugewiesen plus Fehlbetrag von YNAB.** BB rechnet in
   Stufe 1 keine Targets. Pausierte Targets (goal_snoozed_at) werden übersprungen.
5. **Rate-Limit:** sequentielle Einzel-PATCHes, Abbruch beim ersten 429, Rest als
   fehlgeschlagen mit Grund. Kein Zähler.

## Consequences
### Positive
- BB kann YNAB in Stufe 1 nicht unerklärt widersprechen, weil BB keine eigenen Beträge hält.
- Die Budget-Historie (Stufe 2) beginnt nicht leer.

### Negative
- "BB führt" gilt in Stufe 1 nur für die Bewegungen. Die Beträge bleiben YNABs.
- Rund 100 PATCHes pro Lauf gegen etwa 200 Anfragen pro Stunde und Token. Das Limit kann
  in einer Stunde mit mehreren Läufen reißen.
- Zuweisungen, die Roman direkt in YNAB macht, tauchen in den Bewegungen nicht auf.

### Neutral
- Die Naht ledger zu budgeting (Aktivität) ist für Stufe 1 nicht gebaut.

## Alternatives considered
- **Beträge in BB persistieren und mit YNAB abgleichen.** Verworfen: schafft die
  Widerspruchsquelle, die die Vision als Scheiter-Kriterium nennt, und braucht einen
  Abgleich ohne Gegenwert in Stufe 1.
- **Bewegungen erst in Stufe 2 aufzeichnen.** Verworfen: die ersten Monate der Historie
  wären leer und nicht nachholbar.
- **Bewegungen im Kassenbuch-Ereignis-Log.** Verworfen: das Log hängt an Buchungen, das
  Budget hat eigene Sprache und einen eigenen Context.
- **Targets in BB berechnen.** Verworfen: YNAB liefert den Fehlbetrag, Targets bleiben bis
  Stufe 3 YNAB-geführt.

## References
- ADR 0013, 0050, 0055, 0063
- research/ynab-api-targets-zuweisung-2026-10-25.md
```
