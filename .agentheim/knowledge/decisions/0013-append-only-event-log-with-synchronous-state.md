---
id: 0013
title: Kassenbuch-Historie als append-only Ereignis-Log mit synchron geführtem Zustandsmodell
scope: ledger
status: accepted
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ledger-fsz0a]
related_research: []
---

## Context

Das Kassenbuch braucht eine Historie, die auditierbar ist (ADR 0012): jede Änderung an
einer Buchung mit Zeitpunkt und Herkunft, nichts wird überschrieben. Später sollen neue
Sichten aus der Historie entstehen. Besonderheit gegenüber einem gewöhnlichen CRUD-Modell:
ein großer Teil der Änderungen sind keine eigenen Commands, sondern Beobachtungen fremder
Änderungen (Spiegel aus YNAB, vgl. `ynab-sync`).

Rahmen: Single-User, SQLite via Dapper mit frischer Connection pro Operation (ADR 0008),
Solo-Entwickler an Wochenenden, niedrige Toleranz für Unfertiges. Heute existiert
`sync_transactions` (Buchungen pro SyncSession), aber kein bleibendes Buch.

Das Projekt kennt bereits ein Nachrichten-Muster für Commands und Events aus dem
Schwesterprojekt souls (`~/src/souls/standards/backend/semantic-messaging.md`, hier im
Repo als `.claude/rules/semantic-messaging.md`): Commands imperativ benannt
(`RegisterEntity`), Events in Vergangenheitsform mit gruppierter Granularität und reichem
Payload (`EntityRegistered`), Events dort aber ausdrücklich nur in-memory, ohne Event
Store. Der `ledger` übernimmt die Namens- und Payload-Konventionen, weicht aber an genau
einer Stelle ab: hier werden Events persistiert (siehe Decision, Punkt 2).

## Decision

Die Historie ist ein **append-only Ereignis-Log pro Buchung** in SQLite und die einzige
Quelle der Wahrheit für die Vergangenheit. Daneben wird ein **Zustandsmodell** (aktueller
Stand jeder Buchung, Kontostände) **in derselben Transaktion synchron** mitgeschrieben.
Das Zustandsmodell ist ableitbar und darf jederzeit aus dem Log neu gebaut werden
(Replay-Funktion existiert und wird getestet), muss es aber im Normalbetrieb nie.

1. **Semantische Delta-Ereignisse, nicht Snapshots.** Events folgen dem souls-Muster:
   Commands imperativ (`MirrorYnabAccount`, `RecategorizeEntry`, `AddCashExpense`), Events
   in Vergangenheitsform mit gruppierter Granularität und reichem, selbstbeschreibendem
   Payload. Ein Event sagt *was* passiert ist (`EntryCategorizationChangeObservedInYnab`),
   nicht wie der Zustand danach aussieht. Kein Feld-für-Feld-Diffing im Zustandsmodell,
   sondern ein benanntes, fachliches Ereignis pro Änderungsart.

2. **Die eine Abweichung von souls: Events werden persistiert.** souls' Regel "Events
   nur in-memory" gilt dort, weil kein Historie-Grund besteht. Im `ledger` ist die
   Historie der Grund für den Context: Events sind hier die dauerhafte Quelle der
   Wahrheit, nicht nur ein Transportmechanismus zwischen Domain und State. Alles davor
   (Command → Validate → Domain → Event) und danach (EventHandler → State) bleibt wie in
   souls; nur die Lebensdauer des Events selbst ändert sich von "verworfen nach Verarbeitung"
   zu "append-only gespeichert".

3. **Zwei Ereignisfamilien, über die Namen sichtbar.** **Entscheidungen** (BB hat etwas
   festgelegt: `EntryCategorizedByRule`, `EntryRecategorized`, `CashExpenseAdded`) und
   **Beobachtungen** (BB hat in YNAB etwas gesehen: `EntryObservedInYnab`,
   `EntryCategorizationChangeObservedInYnab`, `EntryDeletionObservedInYnab`). Beide
   Familien ändern das Zustandsmodell gleichermaßen, eine Beobachtung bleibt aber am Namen
   erkennbar. Jedes Event trägt zusätzlich einen **Actor** (Discriminated Union:
   Comdirect-Import, Quick Add, Regel mit RuleId, Roman in BB, YNAB-Spiegel). Abweichung
   (siehe `ledger`-README) ist der Fall, dass die letzte Entscheidung der letzten
   Beobachtung widerspricht.

4. **Der Spiegel liefert Snapshots, der Ledger macht daraus Ereignisse.** `ynab-sync`
   gibt Beobachtungen als reine Daten ab (Contract in ynab-czk34, kein Event-Vokabular
   dort). Der `ledger` vergleicht den gelieferten Snapshot mit seinem eigenen Zustand und
   schreibt daraus das semantische Delta-Event. Welche Feldgruppen genau ein Event bilden
   (Granularität im Sinne von Punkt 1), ist Modellierungsarbeit im Skeleton-Task, nicht
   Gegenstand dieses ADRs.

5. **Ein Schreibpfad in einer Transaktion.** ADR 0008 (frische Connection pro Operation)
   bedeutet: zwei Persistence-Funktionen sind zwei Connections und damit zwei
   Auto-Commits. Für den Ledger müssen Event-Insert und Zustands-Update auf **einer**
   Connection in **einer** SQLite-Transaktion liegen (z. B. eine Funktion `appendAndApply`,
   die Log-Insert und State-Update umschließt). Das steht nicht im Widerspruch zu ADR 0008,
   die Operation ist nur größer als bisherige Einzel-Inserts; ADR 0008s Grundregel
   (frische Connection pro Operation, kein geteiltes mutable Connection-Objekt über
   Threads) bleibt unverändert, "Operation" ist hier eben die zusammengesetzte
   Append-und-Apply-Einheit.

6. **Zustandsmodell abgeleitet, Replay pur.** `Domain.fs` bekommt eine reine Funktion
   `apply : EntryState option -> LedgerEvent -> EntryState` ohne I/O. Ein Replay-Test
   faltet alle gespeicherten Events einer Buchung über `apply` und vergleicht das
   Ergebnis mit dem, was in den Zustandstabellen steht. Der Test ist Pflicht (siehe
   Consequences), nicht optional.

7. **Versionierung als Regel, nicht als Framework.** Jedes gespeicherte Event trägt
   Typname, Schema-Version und JSON-Payload. Ein einmal gespeicherter Event-Typ ändert
   nie mehr seine Bedeutung; braucht ein späterer Task eine andere Payload-Form, kommt
   ein neuer Event-Typ (bzw. eine neue Version) dazu, alte gespeicherte Events bleiben
   unangetastet lesbar.

8. **Keine SQLite-Spezialitäten im Log.** Payload als JSON-Text, kein `json_extract` in
   Queries, keine SQLite-eigenen Typen im Schema, Migrationen als normale SQL-Skripte.
   Domain und EventHandler kennen den Store ausschließlich über `appendAndApply` /
   `loadEvents`, nie direkt über SQL. Das hält Punkt 9 (Wechsel der Datenbank) billig,
   falls er je nötig wird.

9. **Postgres / Supabase als verworfene Alternative.** souls läuft auf Supabase Cloud
   (Auth, mehrere Nutzer, gehostet). BB ist Single-User im Homelab hinter Tailscale, mit
   Bankdaten und Comdirect-Tokens, die das Netz heute nie verlassen. Self-hosted Postgres
   brächte einen zweiten Container, eigene Backups, eine Datenmigration und eine andere
   Test-Story (heute In-Memory-SQLite für Tests), ohne Feature-Gegenwert bei ein paar
   tausend Events pro Jahr. Neu zu bewerten, wenn: ein zweiter Prozess dieselben Daten
   braucht, Ad-hoc-Analysen direkt auf den Events gewünscht sind, oder die Erstbefüllung
   (Replay beim Start) messbar langsam wird. Punkt 8 (keine SQLite-Spezialitäten) hält
   diesen Wechsel, falls er kommt, billig.

Kein Event-Bus, keine asynchronen Projektionen, keine Snapshots, kein generisches
Event-Store-Framework. Ein Log, ein Zustandsmodell, eine Transaktion.

## Consequences

### Positive
- Historie ist vollständig und nicht überschreibbar; Herkunft (Actor) ist Pflichtfeld
  auf jedem Event.
- Neue Sichten sind per Replay möglich, ohne Daten erneut von YNAB zu holen.
- Anzeige und Kontostände lesen aus normalen Zustandstabellen: kein Replay auf dem Hot Path.
- Passt zu ADR 0008 und Dapper ohne neue Infrastruktur (kein Bus, kein Framework).
- Die souls-Namenskonvention (Commands imperativ, Events Vergangenheitsform) macht das
  Vokabular über beide Projekte hinweg lesbar, ohne dass souls' In-Memory-Regel für BB
  übernommen werden muss.

### Negative
- Zwei Schreibziele pro Änderung (Log + Zustand); Konsistenz hängt an der SQLite-
  Transaktion in `appendAndApply`. Der Replay-Test ist Pflicht, sonst driftet das
  Zustandsmodell unbemerkt vom Log weg.
- Event-Payloads müssen versionierbar sein (Schema-Feld pro Event), sonst bricht Replay
  bei späteren Typänderungen.
- Zwei Ereignisfamilien plus Actor-Feld sind mehr Modellierungsaufwand pro Event als ein
  einzelnes generisches "geändert"-Event; das ist bewusst in Kauf genommen, weil sonst
  Abweichung (Entscheidung vs. Beobachtung) nicht unterscheidbar wäre.

### Neutral
- Das ist Event Sourcing ohne den CQRS-Überbau. Der Begriff "Event Sourcing" darf intern
  benutzt werden; die Vision und die BC-README sprechen von Historie.

## Alternatives considered

- **Volles Event Sourcing mit asynchronen Projektionen**: abgelehnt, bringt Bus,
  Replay-Zwang auf dem Lesepfad, Eventual Consistency und Versionierung als Dauerkosten,
  für einen Nutzer und ein paar tausend Buchungen pro Jahr ohne Gegenwert.
- **Zustandsmodell mit nachgelagertem Audit-Log**: abgelehnt, knapp: das Log wäre
  abgeleitet und optional; genau die Sichten, die später gewünscht sind, wären schwerer
  zu bauen, und ein vergessener Log-Eintrag fällt nie auf, weil das Zustandsmodell auch
  ohne ihn korrekt aussieht.
- **Postgres / Supabase statt SQLite**: abgelehnt (siehe Decision, Punkt 9): kein
  Feature-Gegenwert für Single-User-Betrieb im Homelab, zusätzlicher Container, Backups
  und Datenmigration ohne Not. Neu bewerten bei zweitem Prozess auf denselben Daten,
  gewünschten Ad-hoc-Analysen auf Events, oder messbar langsamer Erstbefüllung.
