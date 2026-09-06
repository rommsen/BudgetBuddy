---
id: ledger-fsz0a
title: Historie-Technik im Kassenbuch festlegen (Ereignis-Log + synchrones Zustandsmodell)
status: doing
type: decision
context: ledger
created: 2026-09-06
depends_on: []
blocks: [ledger-kt0wq]
tags: [foundation, persistence]
related_adrs: [0012, 0008]
related_research: []
prior_art: []
---

## Why
Die Vision fordert eine auditierbare Historie pro Buchung: jede Änderung mit Zeitpunkt und
Herkunft, nichts wird überschrieben, und spätere Sichten (Reports) sollen aus der Historie
gebaut werden können. Roman hat Event Sourcing + CQRS vorgeschlagen. Gegengewicht: fertiges
Tool, Solo-Entwickler an Wochenenden, Single-User, SQLite. Die Entscheidung bestimmt das
Persistenz-Fundament des gesamten `ledger`-Contexts und muss vor dem Skeleton fallen.

## What
ADR aus dem Entwurf unten committen (angepasst, falls Roman abweicht). Kein Code.

Routing: **BC-lokal (`ledger`)**. Ohne das Kassenbuch bräuchte kein anderer Context eine
Historie; Regeln, Settings und SyncSessions bleiben Zustandstabellen.

## Acceptance criteria
- [ ] ADR committed unter `.agentheim/knowledge/decisions/`, `scope: ledger`, Begründung
      entspricht dem Entwurf oder Romans Änderung daran.
- [ ] Das ADR benennt: Source of Truth, Form des Zustandsmodells, wie Herkunft modelliert
      wird, wie Spiegel-Beobachtungen von BB-Entscheidungen unterschieden werden, und was
      bewusst *nicht* gebaut wird (Bus, asynchrone Projektionen, Snapshots).
- [ ] `ledger/README.md` Open Question "Historie-Technik" als gelöst markiert.
- [ ] Keine Code-Änderung.

## Notes
### ADR-Entwurf (Architekten-Pass, 2026-09-06)

```markdown
---
id: NNNN
title: Kassenbuch-Historie als append-only Ereignis-Log mit synchron geführtem Zustandsmodell
scope: ledger
status: proposed
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ledger-fsz0a]
related_research: []
---

## Context
Das Kassenbuch braucht eine Historie, die auditierbar ist (ADR 0012): jede Änderung an
einer Buchung mit Zeitpunkt und Herkunft, nichts wird überschrieben. Später sollen neue
Sichten aus der Historie entstehen. Besonderheit: ein großer Teil der Änderungen sind
keine eigenen Commands, sondern Beobachtungen fremder Änderungen (Spiegel aus YNAB).
Rahmen: Single-User, SQLite via Dapper mit frischer Connection pro Operation (ADR 0008),
Solo-Entwickler, niedrige Toleranz für Unfertiges. Heute existiert `sync_transactions`
(Buchungen pro SyncSession), aber kein bleibendes Buch.

## Decision
Die Historie ist ein **append-only Ereignis-Log pro Buchung** in SQLite und die einzige
Quelle der Wahrheit für die Vergangenheit. Daneben wird ein **Zustandsmodell** (aktueller
Stand jeder Buchung, Kontostände) **in derselben Transaktion synchron** mitgeschrieben.
Das Zustandsmodell ist ableitbar und darf jederzeit aus dem Log neu gebaut werden
(Replay-Funktion existiert und wird getestet), muss es aber im Normalbetrieb nie.

- Jedes Ereignis trägt: Buchungs-Id, laufende Nummer, Zeitpunkt, **Herkunft** (Discriminated
  Union: Comdirect-Import, Quick Add, Regel mit RuleId, Roman in BB, YNAB-Spiegel), Payload.
- Zwei Ereignisfamilien: **Entscheidungen** (BB hat etwas festgelegt) und **Beobachtungen**
  (BB hat in YNAB etwas gesehen). Eine Beobachtung ändert das Zustandsmodell wie eine
  Entscheidung, bleibt aber als Beobachtung erkennbar. Abweichung = letzte Entscheidung
  widerspricht letzter Beobachtung.
- Kein Event-Bus, keine asynchronen Projektionen, keine Snapshots, kein generisches
  Event-Store-Framework. Ein Log, ein Zustandsmodell, eine Transaktion.

## Consequences
### Positive
- Historie ist vollständig und nicht überschreibbar; Herkunft ist Pflichtfeld.
- Neue Sichten sind per Replay möglich, ohne Daten neu zu holen.
- Anzeige und Kontostände lesen aus normalen Tabellen: kein Replay auf dem Hot Path.
- Passt zu ADR 0008 und Dapper ohne neue Infrastruktur.

### Negative
- Zwei Schreibziele pro Änderung (Log + Zustand); Konsistenz hängt an der SQLite-
  Transaktion. Der Replay-Test ist Pflicht, sonst driftet das Zustandsmodell unbemerkt.
- Ereignis-Payloads müssen versionierbar sein (Schema-Feld pro Ereignis), sonst bricht
  Replay bei späteren Typänderungen.

### Neutral
- Das ist Event Sourcing ohne den CQRS-Überbau. Der Begriff "Event Sourcing" darf
  intern benutzt werden; die Vision spricht von Historie.

## Alternatives considered
- **Volles Event Sourcing mit asynchronen Projektionen** — abgelehnt: bringt Bus, Replay-
  Zwang, Eventual Consistency und Versionierung als Dauerkosten, für einen Nutzer und
  ein paar tausend Buchungen pro Jahr ohne Gegenwert.
- **Zustandsmodell mit nachgelagertem Audit-Log** — abgelehnt, knapp: das Log wäre
  abgeleitet und optional; genau die Sichten, die Roman später will, wären schwerer zu
  bauen, und ein vergessener Log-Eintrag fällt nie auf.
```

### Entscheidung mit Roman (2026-09-06, Work-Session)

Der Entwurf oben gilt mit diesen Änderungen; das ADR wird aus Entwurf **plus** diesen
Punkten geschrieben, nicht aus dem Entwurf allein:

1. **Semantische Delta-Ereignisse, nicht Snapshots.** Ereignisse folgen dem
   souls-Muster (`~/src/souls/standards/backend/semantic-messaging.md`, hier im Projekt
   `.claude/rules/semantic-messaging.md`): Commands imperativ (`MirrorYnabAccount`,
   `RecategorizeEntry`, `AddCashExpense`), Events in Vergangenheitsform mit gruppierter
   Granularität und reichem Payload. Ein Ereignis sagt *was* passiert ist
   (`EntryCategorizationChangeObservedInYnab`), nicht wie der Zustand danach aussieht.
2. **Die eine Abweichung von souls:** im `ledger` werden Ereignisse **persistiert** und
   sind die Wahrheit. souls' Regel "Events nur in-memory" gilt dort, weil es keinen
   Historie-Grund gibt; hier ist die Historie der Grund für den Context. Das ADR sagt das
   explizit. Alles davor (Command → Validate → Domain → Event) und danach
   (EventHandler → State) bleibt wie in souls.
3. **Zwei Familien über die Namen sichtbar:** Entscheidungen (`EntryCategorizedByRule`,
   `EntryRecategorized`, `CashExpenseAdded`) und Beobachtungen (`EntryObservedInYnab`,
   `EntryCategorizationChangeObservedInYnab`, `EntryDeletionObservedInYnab`). Jedes
   Ereignis trägt zusätzlich einen Actor (Roman in BB, Regel mit RuleId, Comdirect-Import,
   YNAB-Spiegel).
4. **Der Spiegel liefert Snapshots, der Ledger macht daraus Ereignisse.** `ynab-sync`
   gibt Beobachtungen als reine Daten ab (Contract in ynab-czk34). Der `ledger` vergleicht
   mit seinem Zustand und schreibt das semantische Delta. Welche Feldgruppen ein Ereignis
   bilden, ist Modellierungsarbeit im Skeleton, nicht im ADR.
5. **Ein Schreibpfad in einer Transaktion.** ADR 0008 (frische Connection pro Operation)
   heißt: zwei Persistence-Funktionen = zwei Connections = zwei Auto-Commits. Für den
   Ledger muss Ereignis-Insert und Zustands-Update auf **einer** Connection in **einer**
   SQLite-Transaktion liegen (z.B. `appendAndApply`). Kein Widerspruch zu 0008, die
   Operation ist nur größer. Das ADR benennt das ausdrücklich.
6. **Zustandsmodell abgeleitet, Replay pur.** `Domain.fs` bekommt eine reine Funktion
   `apply : EntryState option -> LedgerEvent -> EntryState`; der Replay-Test faltet alle
   Ereignisse und vergleicht mit den Tabellen.
7. **Versionierung als Regel, nicht als Framework:** jedes gespeicherte Ereignis hat
   Typname, Schema-Version, JSON-Payload. Ein gespeicherter Typ ändert nie seine
   Bedeutung; braucht man etwas anderes, kommt ein neuer Typ dazu.
8. **Keine SQLite-Spezialitäten** im Log: Payload als JSON-Text, kein `json_extract` in
   Queries, keine SQLite-eigenen Typen, Migrationen als normale SQL-Skripte. Domain und
   EventHandler kennen den Store nur über `appendAndApply` / `loadEvents`.
9. **Postgres / Supabase als verworfene Alternative** ins ADR aufnehmen: souls läuft auf
   Supabase Cloud (Auth, mehrere Nutzer, gehostet); BB ist Single-User im Homelab hinter
   Tailscale mit Bankdaten und Comdirect-Tokens, die das Netz heute nie verlassen.
   Self-hosted Postgres brächte zweiten Container, Backups, Datenmigration und eine
   andere Test-Story (heute In-Memory-SQLite), ohne Feature-Gegenwert bei ein paar
   tausend Ereignissen pro Jahr. Neu bewerten, wenn ein zweiter Prozess dieselben Daten
   will, Ad-hoc-Analysen auf Ereignissen gewünscht sind oder die Erstbefüllung messbar
   langsam ist. Punkt 8 hält den Wechsel billig.

Was unverändert aus dem Entwurf bleibt: kein Bus, keine asynchronen Projektionen, keine
Snapshots, kein Event-Store-Framework.
