---
id: infrastructure-c7f53
title: "Event Sourcing für die Kernkontexte ledger und budgeting mit gemeinsamem Ereignis-Umschlag; Reports als Read Models darauf, alles andere bleibt Zustand"
status: backlog
type: decision
context: infrastructure
created: 2026-11-05
completed:
depends_on: []
blocks: [budgeting-zkbbd]
tags: [event-sourcing, architektur, read-models]
related_adrs: [0012, 0013, 0065, 0079]
related_research: []
prior_art: []
---

## Why
Ziel ist, von YNAB wegzukommen (ADR 0012, Fähigkeit für Fähigkeit). Dafür muss BB in seinen
Kernkontexten selbst Quelle der Wahrheit sein, und Reports sollen als Read Models auf einem
einheitlichen Ereignismodell entstehen. Heute gibt es drei Speichermuster ohne gemeinsame Regel:
Event Sourcing nur im Kassenbuch (ADR 0013, `scope: ledger`), append-only Aufzeichnungen ohne
`apply`/Replay im Budget (ADR 0065, 0079, 0080) und normalen Zustand für den Rest. Die Einteilung
steht nirgends, jede Entscheidung wurde im jeweiligen BC für sich getroffen.

Ergebnis eines Gesprächs mit Roman am 2026-11-05.

## What
Ein globales ADR, das festlegt:

**Schon entschieden (im ADR festhalten, nicht neu diskutieren):**
- Die Kernkontexte `ledger` und `budgeting` werden mit Event Sourcing gebaut: append-only Log als
  Quelle der Wahrheit, reine `apply`-Funktion, Pflicht-Replay-Test, Versionierung wie ADR 0013
  Punkt 7.
- Gemeinsamer Ereignis-Umschlag: Ereignis-Id, Zeitpunkt, Actor, Typ, Schema-Version, globale
  Reihenfolge. Das Vokabular bleibt je Kontext eigen (ADR 0065 hat zu Recht das
  *Kassenbuch*-Log für Budget-Bewegungen abgelehnt; ein eigenes Budget-Log in derselben Form ist
  damit vereinbar).
- Reports werden Read Models auf diesen Ereignissen.
- Normaler Zustand bleibt: Regeln, Settings, Arbeitsstand des Comdirect-Imports
  (`sync_sessions`, `sync_transactions`), gespiegelte Stammdaten (Konten, Kategorien).
- Weiter gilt ADR 0013: kein Event-Bus, keine asynchronen Projektionen, kein Framework.

**Im ADR zu klären:**
1. **Globale Reihenfolge.** `ledger_entry_events` hat nur eine Sequenz je Buchung
   (`PRIMARY KEY (entry_id, sequence)`, `src/Server/Ledger/Persistence.fs:218`). Ein Read Model
   über beide Kontexte braucht eine Reihenfolge über alle Ereignisse; `occurred_at` hat
   Gleichstände. Optionen: eine gemeinsame Ereignistabelle mit fortlaufender Nummer, oder eine
   Tabelle je Kontext mit globaler Nummer aus einer Quelle. Die implizite SQLite-`rowid` gibt
   heute faktisch die Einfügereihenfolge, ist aber kein Vertrag.
2. **Read Models.** Synchron in derselben Transaktion mitschreiben oder auf Anfrage aus dem Log
   falten. Bei ein paar tausend Ereignissen im Jahr ist Falten auf Anfrage vermutlich schnell
   genug und braucht keine Tabellen. Kriterium festlegen, ab wann ein Read Model eine Tabelle
   bekommt.
3. **Umschlag fürs Kassenbuch.** Ob `ledger_entry_events` an den Umschlag angepasst wird
   (Ereignis-Id, globale Nummer) und wie bestehende Ereignisse migriert werden, ohne ihre
   Bedeutung zu ändern (ADR 0013 Punkt 7).
4. **Regel für neue Fähigkeiten.** Woran ein Modeler erkennt, ob eine neue Fähigkeit Ereignisse
   oder Zustand bekommt. Vorschlag aus dem Gespräch: Ereignisse dort, wo BB den Zustand führt und
   Historie gebraucht wird.

## Acceptance criteria
- [ ] Ein ADR unter `.agentheim/knowledge/decisions/` mit globalem Scope hält die schon
      entschiedenen Punkte und die Antworten auf 1 bis 4 fest.
- [ ] Das ADR nennt ausdrücklich, welche heutigen Tabellen Zustand bleiben.
- [ ] ADR 0013 bekommt einen Verweis auf das neue ADR (Nachtrag oder `superseded_by`, je nachdem,
      ob es Teile ersetzt).
- [ ] CLAUDE.md oder eine Regel unter `.claude/rules/` nennt die Regel aus Punkt 4 in einem Satz,
      damit Refinement und Worker sie sehen (prose-only, unenforced).
- [ ] Folge-Tasks für die Umsetzung (Umschlag, globale Reihenfolge, Kassenbuch-Anpassung) liegen
      im Backlog des jeweiligen BC.

## Notes
- Gesprächsverlauf: Event Sourcing war bisher nur für das Kassenbuch festgehalten (ADR 0012 hat
  die Frage in den `ledger` verschoben, ADR 0013 hat `scope: ledger`).
- Die Budget-Seite davon regelt budgeting-zkbbd.
