---
id: infrastructure-c7f53
title: "Event Sourcing für die Kernkontexte ledger und budgeting mit gemeinsamem Ereignis-Umschlag; Reports als Read Models darauf, alles andere bleibt Zustand"
status: todo
type: decision
context: infrastructure
created: 2026-11-05
completed:
depends_on: []
blocks: [budgeting-zkbbd]
tags: [event-sourcing, architektur, read-models]
related_adrs: [0008, 0012, 0013, 0021, 0065, 0079]
related_research: []
prior_art: []
---

## Why
Ziel ist, von YNAB wegzukommen (ADR 0012, Fähigkeit für Fähigkeit). Dafür muss BB in seinen
Kernkontexten selbst Quelle der Wahrheit sein, und Reports sollen als Read Models auf einem
einheitlichen Ereignismodell entstehen. Heute gibt es drei Speichermuster ohne gemeinsame Regel:
Event Sourcing nur im Kassenbuch (ADR 0013, `scope: ledger`), append-only Aufzeichnungen ohne
`apply`/Replay im Budget (ADR 0065, 0079, 0080) und normalen Zustand für den Rest. Die Einteilung
steht nirgends, jede Entscheidung wurde im jeweiligen BC für sich getroffen. Dass das
auseinanderläuft, zeigt schon heute: Das Budget hat append-only-Trigger, `ledger_entry_events`
nicht (`src/Server/Ledger/Persistence.fs:218-228`).

Gespräch mit Roman am 2026-11-05, Refinement mit architect und tactical-modeler am selben Tag.

## What
Ein globales ADR (`scope: global`), das die folgenden Entscheidungen festhält. Alle Punkte sind mit
Roman entschieden; der Worker schreibt sie aus und begründet sie, er entscheidet sie nicht neu.

### 1. Gemeinsame Ereignistabelle (entschieden: Roman, 2026-11-05)
- Eine Tabelle `events` für alle Kontexte mit Event Sourcing. `position INTEGER PRIMARY KEY` ist die
  globale Reihenfolge, SQLite vergibt sie selbst. Kein Zähler, kein AUTOINCREMENT nötig.
- Umschlag als Schema: `position`, `event_id` (eindeutig), `context`, `stream_id`,
  `stream_version`, `occurred_at`, Actor (Art plus freie Referenz, weil `OrderIdMatch` die
  Bestell-Id in `actor_rule_id` ablegt, `Ledger/Events.fs:1158-1160`), `event_type`,
  `schema_version`, `payload` (JSON-Text), optional `command_id`.
- `UNIQUE(stream_id, stream_version)`: Zwei gleichzeitige Schreibvorgänge in denselben Stream
  scheitern statt still zu überschreiben.
- append-only-Trigger gegen UPDATE und DELETE, einmal für alle Kontexte.
- Die Kontexte bleiben fachlich getrennt: eigene `context`-Werte, eigenes Vokabular, eigener Decoder
  je Kontext. ADR 0065 lehnte das *Kassenbuch*-Log für Budget-Bewegungen ab, weil es an Buchungen
  hängt; ein eigener Budget-Stream in der gemeinsamen Tabelle ist damit vereinbar.
- **Pflichtsatz:** `position` ist die Reihenfolge des Festhaltens, nicht des Geschehens.
  `occurred_at` ist schon heute innerhalb einer Buchung nicht monoton (`recordComdirectPush` schreibt
  `PushedAt`, `Ledger/Domain.fs:711`; die Review-Schritte danach tragen das frühere `step.At`,
  `Domain.fs:791`). Reports sortieren fachlich nach Buchungsdatum oder `occurred_at`. Kein
  `recorded_at`, `occurred_at` wird nicht zurückdatiert.
- Verworfen: Tabelle je Kontext mit Zählertabelle. Kleinere Migration, aber der Umschlag bliebe
  Konvention, die je Kontext neu umgesetzt werden muss, und jeder kontextübergreifende Report
  braucht ein `UNION ALL`.

### 2. Read Models
- Standard ist Falten auf Anfrage aus dem Log.
- Eine Read-Model-Tabelle gibt es nur, wenn (a) ein Schreibweg aus ihr entscheidet, (b) sie eine
  Eindeutigkeit trägt (Fremd-Id, Befehls-Id) oder (c) ein Lesepfad mit Produktionsdaten gemessen über
  200 ms braucht.
- Jede Read-Model-Tabelle lässt sich aus dem Log neu bauen und steht im Replay-Test. Heute sind das
  `ledger_entries`, `ledger_entry_split_categories`, `ledger_entry_split_transfers`
  (`Ledger/Persistence.fs:770-825`) und `ledger_entry_links`.
- Weiter gilt ADR 0013: synchron in derselben Transaktion, kein Bus, keine asynchronen Projektionen.

### 3. Übergang des Kassenbuchs
- `ledger_entry_events` wird in `events` überführt: Kopie in `rowid`-Reihenfolge (echte
  Einfügereihenfolge). Abbruch, wenn `sequence` je Buchung in dieser Reihenfolge nicht steigt (ein
  früheres VACUUM kann die rowid neu nummeriert haben). `stream_version` = bisherige `sequence`.
  `event_id` darf beim Kopieren zufällig vergeben werden.
- Typ, Version und Payload bleiben unverändert (ADR 0013 Punkt 7). Das ADR nennt die einmalige
  Kopie als ausdrückliche Ausnahme vom "nie mehr anfassen".
- Umzuschreiben: `appendAndApply` (`Ledger/Persistence.fs:661`), `loadEventsForEntry` (847),
  `loadAllEvents` (861), `loadBobBookedAmountsBetween` (873, Bob-Tagesgrenze, ADR 0068),
  `backfillTransferAccountId` (91), `EventRow` (311), Tests in `LedgerTests.fs` ab 1437 und 1929.
- **Abfolge (entschieden: Roman, 2026-11-05):** Die Kassenbuch-Migration läuft, bevor das erste
  Budget-Ereignis in `events` geschrieben wird. So ist `position` über die ganze Historie
  konsistent. Das ADR für budgeting-zkbbd kann parallel entstehen, seine Umsetzung wartet.

### 4. Regel für neue Fähigkeiten (entschieden: Roman, 2026-11-05)
"Eine Fähigkeit bekommt Ereignisse, wenn sie Geld-Fakten oder Entscheidungen über Geld festhält
(Buchungen, Zuweisungen, Targets) und ein Bericht deren Verlauf lesen soll. Konfiguration,
Stammdaten aus YNAB und der Arbeitsstand eines Imports sind Zustand. Eine Ursache aus der
Konfiguration steht im Ereignis als Name und Id."

Kurzform: Geht es um Geld (was passiert ist oder was entschieden wurde), sind es Ereignisse. Alles
andere ist Zustand. Damit sind YNAB-Beobachtungen im Kassenbuch Ereignisse und
Kategorisierungsregeln Zustand. Targets sind Entscheidungen über Geld und keine Regeln im Sinn von
`rules.yml`.

Verworfen: "Ereignisse, wenn BB den Zustand führt und Historie gebraucht wird". Unschärfer und
passt nicht auf YNAB-Beobachtungen.

### Zustand bleibt
Kategorisierungsregeln (`rules`), `settings`, `sync_sessions`, `sync_transactions`,
`ledger_accounts`, `ledger_categories`.

### Im ADR noch auszuarbeiten (technisch, ohne Rückfrage)
- Stream-Namen je Kontext (etwa `ledger:entry:<id>`), Form der Actor-Referenz.
- Befehls-Id: Ein Befehl kann mehrere Ereignisse erzeugen (Übertragspaar), ein UNIQUE auf
  `command_id` je Zeile reicht dann nicht. Lösung festlegen (eigene Tabelle verarbeiteter Befehle
  oder eindeutiger Index auf Befehl und Stream o. Ä.). Hintergrund: budgeting-zkbbd Punkt 8.
- Ob `BeginTransaction()` in Microsoft.Data.Sqlite mit `BEGIN IMMEDIATE` öffnet und was das für
  `UNIQUE(stream_id, stream_version)` heißt. Im Folge-Task per Test belegen.

## Acceptance criteria
- [ ] Unter `.agentheim/knowledge/decisions/` liegt ein ADR mit `scope: global`, das die Punkte 1
      bis 4 und "Zustand bleibt" festhält.
- [ ] Das ADR beschreibt das Schema von `events` mit Spaltentypen, `UNIQUE(stream_id,
      stream_version)`, append-only-Triggern und dem Pflichtsatz "Reihenfolge des Festhaltens,
      nicht des Geschehens".
- [ ] Das ADR enthält das Read-Model-Kriterium (a) bis (c) und listet die heutigen
      Read-Model-Tabellen mit der Pflicht, sie aus dem Log neu bauen zu können.
- [ ] Das ADR beschreibt den Übergang des Kassenbuchs: Kopie in rowid-Reihenfolge mit
      Abbruchprüfung, einmalige Ausnahme vom Nicht-Anfassen, kein `recorded_at`, kein Zurückdatieren,
      Abfolge vor dem ersten Budget-Ereignis.
- [ ] Das ADR legt Stream-Namen und die Lösung für die Befehls-Id fest.
- [ ] Das ADR nennt die verworfenen Varianten (Tabelle je Kontext mit Zähler, Regelsatz "BB
      führt") mit Grund.
- [ ] ADR 0013 bekommt einen Nachtrag mit Verweis auf das neue ADR, kein `superseded_by` (Punkte 1
      bis 9 gelten weiter, die Tabelle wechselt).
- [ ] Die Kurzform der Regel aus Punkt 4 steht in einem Satz in CLAUDE.md, Abschnitt Domäne oder
      Architektur (prose-only, unenforced).
- [ ] Die Folge-Tasks aus den Notes liegen mit `depends_on` im Backlog des jeweiligen BC.
      `budgeting-zkbbd` nennt in den Notes, dass seine Umsetzungs-Tasks auf die
      Kassenbuch-Migration warten.

## Notes
### Folge-Tasks (legt der Worker an)
1. `infrastructure`: "Gemeinsame Ereignistabelle `events`: Schema, append-only-Trigger,
   `UNIQUE(stream_id, stream_version)`, Befehls-Id, generisches Anhängen und Laden; Tests belegen
   Konflikt bei gleicher Stream-Version, Rollback und das Verhalten von `BeginTransaction`".
   depends_on [infrastructure-c7f53].
2. `ledger`: "Kassenbuch-Ereignisse nach `events` überführen: Kopie in rowid-Reihenfolge mit
   Abbruchprüfung, `appendAndApply` und Ladepfade umstellen, `ledger_entry_events` danach entfernen".
   depends_on [1]. AKs: Replay vor und nach der Migration gleich und gleich `ledger_entries`;
   Payload, Typ und Version je Ereignis unverändert; `stream_version` je Buchung lückenlos;
   Bob-Tagesgrenze (`loadBobBookedAmountsBetween`) liefert vor und nach dieselben Summen; Probe
   gegen eine Kopie der Produktions-DB vor dem Deploy (außerhalb des Repos).
3. `ledger`: "`linkEntry` (`Ledger/CommandHandlers.fs:206`) schreibt `ledger_entry_links` ohne
   Ereignis: gegen ADR 0013 und 0021 prüfen". depends_on [].
4. Die Umsetzungs-Tasks aus budgeting-zkbbd hängen an 2.

### Risiken
- **Startmigration in Produktion:** Schlägt die Abbruchprüfung an, startet BB nicht. Vorher Probe
  gegen eine DB-Kopie; das tägliche Backup ist vorhanden.
- **Kein Zurück ohne Backup:** Sobald neue Ereignisse in `events` stehen, geht ein Rollback nur über
  das Backup.
- **Nummer als Fachreihenfolge gelesen:** Ein Report, der nach `position` statt nach Datum sortiert,
  zeigt einen falschen Verlauf.
- **Bob-Tagesgrenze** hängt an `occurred_at` (`Ledger/Persistence.fs:873`, 878). Die Grenzen aus
  ADR 0068 dürfen sich durch den Umbau nicht verschieben.
- **Fable.Remoting:** Alte Clients merken nichts, solange `src/Shared` unverändert bleibt
  (`LedgerHistoryLine`, `src/Shared/Domain.fs:1293`; `HistoryEntry`, `src/Shared/Budgeting.fs:697`).
  Wandern neue Felder nach Shared, im selben Schritt `scripts/ledger-detail.test.mjs` und
  `scripts/budget-history.test.mjs` nachziehen.
- **Echo im Budget (für zkbbd):** Ohne Echo-Regel wie im Kassenbuch (`...WrittenBackToYnab`, Actor
  `YnabMirror`) erzeugt BBs eigene Schreibung nach YNAB Beobachtungen.
