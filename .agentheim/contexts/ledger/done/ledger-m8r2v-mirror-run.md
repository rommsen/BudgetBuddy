---
id: ledger-m8r2v
title: Spiegel-Lauf YNAB → Kassenbuch: Delta-Abruf, Snapshot-Vergleich, Ereignisse, manueller Auslöser
status: done
type: feature
context: ledger
created: 2026-09-06
depends_on: [ledger-e3k7p, ynab-czk34]
blocks: [ledger-q5w9c, ledger-kt0wq]
tags: [foundation, backend, integration]
related_adrs: [0013, 0014, 0015, 0018, 0019]
related_research: []
prior_art: []
---

## Why
Slice 2 von 3 des Walking Skeletons (`ledger-kt0wq`). Das Kassenbuch wird erst durch den
Spiegel vollständig (ADR 0012, 0014). Dieser Slice bringt Buchungen aus YNAB als
Beobachtungen ins Log aus Slice 1 und beweist Delta, Idempotenz und Löschungen, ohne UI.

## What
1. **YnabClient**: neuer Abruf `getTransactionsDelta budgetId (lastKnowledge: int option)`
   auf `GET /budgets/{id}/transactions?last_knowledge_of_server=N` (liefert bei Delta auch
   `deleted: true`) und `getAccountsDelta` analog; Decoder für Subtransactions,
   `transfer_account_id`, `transfer_transaction_id`, `cleared`, `deleted`, `import_id`,
   Kategorie- und Payee-Namen. Rückgabe enthält das `server_knowledge` der Antwort.
   Bestehende `since_date`-Abrufe bleiben unverändert.
2. **Contract** nach ADR 0014: eine `YnabObservation` als reiner Datentyp (im `ynab-sync`-
   Rand, z.B. `YnabClient.fs`), Liste je Lauf plus `server_knowledge`.
3. **Spiegel-Lauf** (`mirrorYnabBudget`, Command-Naming nach semantic-messaging):
   Beobachtungen holen, Konten aus dem Accounts-Delta anlegen/aktualisieren, je Beobachtung
   Buchung suchen (YNAB-Id, dann ImportId, ADR 0015), Snapshot gegen Zustand vergleichen und
   nur bei Unterschied Ereignisse erzeugen (pure Funktion `observe : EntryState option ->
   YnabObservation -> LedgerEvent list` in Domain), alles über `appendAndApply` schreiben,
   `server_knowledge` pro Budget in den Settings persistieren, Lauf-Status (Zeitpunkt,
   Ergebnis, Fehlertext) persistieren.
4. **Erstbefüllung**: ohne gespeichertes Knowledge Vollabruf aller Konten (offen und
   geschlossen), danach nur Delta. Verlust des Knowledge muss idempotent sein.
5. **Auslöser in diesem Slice nur manuell**: ein Fable.Remoting-Endpunkt
   `mirrorYnabBudget: unit -> Async<Result<MirrorRunSummary, string>>` (Anzahl neuer,
   geänderter, gelöschter Buchungen). Frische-Kriterium und Lauf nach Push aus ADR 0014 sind
   Folge-Task, nicht hier.
6. **Rate-Limit**: 429 bricht den Lauf ab, Status zeigt es; kein Retry im Slice.
7. **Tests**: `observe` rein (neue Buchung → `EntryObservedInYnab`; gleiche Daten → keine
   Ereignisse; geänderte Kategorie → genau ein Kategorie-Ereignis; `deleted` → Löschungs-
   Ereignis, Zustand behält die Buchung mit Flag); Matching-Reihenfolge YNAB-Id vor
   ImportId; Decoder gegen ein JSON-Fixture mit Split, Transfer und `deleted`;
   `server_knowledge` wird nach dem Lauf persistiert (In-Memory-SQLite, YNAB-Client
   gemockt oder über injizierten Fetch wie in bestehenden YnabClient-Tests).

## Acceptance criteria
- [x] Delta-Abruf mit `last_knowledge_of_server` implementiert; Erstbefüllung ohne
      Knowledge; `server_knowledge` je Budget persistiert.
- [x] Zweiter Lauf ohne Änderung erzeugt kein Ereignis (Test).
- [x] Kategorie-Änderung in YNAB erzeugt genau ein Änderungs-Ereignis auf derselben
      Buchungs-Id (Test mit zwei Beobachtungen nacheinander).
- [x] Löschung in YNAB setzt das Flag und erzeugt ein Ereignis; nichts wird physisch
      gelöscht (Test).
- [x] Überträge kommen als zwei Buchungen mit mitgeführter Verknüpfung an (ADR 0015).
- [x] Manueller Endpunkt vorhanden; Lauf-Status abrufbar (Zeitpunkt, Ergebnis, Fehler).
- [x] Bestehende Sync-Pipeline, Push und Quick Add unverändert; alle bestehenden Tests grün.
- [x] `dotnet build` und `dotnet test` grün.
- [x] `ledger/README.md` und `ynab-sync/README.md`: Spiegel-Lauf und Contract in der
      Sprache dokumentiert (je ein kurzer Abschnitt).

## Notes
Dieser Task berührt zwei BCs: der Abruf und der Contract gehören zu `ynab-sync`
(Conformist-Rand), der Vergleich und das Schreiben zu `ledger`. Der Worker darf beide
READMEs anfassen, aber nur die neuen Abschnitte. Der Comdirect-Konto-Filter aus dem
ursprünglichen Skeleton entfällt: der Spiegel ist budgetweit (ADR 0014); die Seite
(`ledger-q5w9c`) zeigt dann ein Konto.

## Verifier note (iteration 1)

**REASONS:**
- Check 4 (ubiquitous language): the diff redefines the existing term **SplitTarget** in a
  way that directly contradicts its BC README definition.
  `.agentheim/contexts/ynab-sync/README.md:35-38` states: "**SplitTarget** — das Ziel einer
  Split-Zeile als DU: `ToCategory (Kategorie) | ToTransfer (Ziel-Konto)`. XOR auf Typebene —
  eine Zeile ist *entweder* Kategorie *oder* Transfer, **nie beides/keins**."
  `src/Shared/Domain.fs` now adds a third case `| Uncategorized`, so "keins" is
  representable and the DU no longer has the two cases the README enumerates. The worker
  updated the doc comment in `src/Shared/Domain.fs:96-115` and wrote ADR 0019 to amend
  ADR 0006, but left the README bullet, the term's canonical definition, factually wrong.
- Check 5 (README sync): the parsed `readmeDelta` for `../ynab-sync/README.md` carries no op
  against that bullet — its four Ubiquitous-language ops are all appends (Beobachtung,
  server_knowledge, Unkategorisierte Split-Zeile, Transfer-Payee im Lesepfad) and its only
  `replace` targets *Open questions*. Once the conductor materializes this delta, the README
  will hold two adjacent, mutually contradictory statements in the same section:
  "nie beides/keins" and a new **Unkategorisierte Split-Zeile** bullet describing exactly
  that case. The task's `## Notes` restriction ("nur die neuen Abschnitte") does not cover
  this: it was written before the third DU case emerged, and it cannot license shipping a
  false invariant statement that `mkSplits`, the split editor and the ynab-002 review UI all
  read.

**SUGGESTED_FIX:** Add a `replace` op to the `README_DELTA` for
`.agentheim/contexts/ynab-sync/README.md`, anchored on the **SplitTarget** bullet, restating
the DU as three cases and narrowing the XOR claim to what ADR 0019 actually preserves
("Kategorie *und* Transfer" bleibt unrepräsentierbar; "keins" ist als `Uncategorized`
benannt, nur vom Spiegel erzeugt). Change nothing else — the code, tests and the remaining
delta ops were verified sound.

**ITERATION_HINT:** likely-fixable

## Outcome

Slice 2 des Walking Skeletons steht: das Kassenbuch füllt sich aus YNAB.

**Der Contract** (`src/Server/YnabObservations.fs`, neu) ist die Naht zu `ynab-sync`:
`YnabObservation` als reine Daten nach ADR 0014 (YNAB-Id, Konto, ImportId, Datum, Betrag,
Payee-Name, Memo, Kategorie mit Namen, Splits, Cleared, `transfer_account_id`,
`transfer_transaction_id`, `deleted`) plus `server_knowledge` je Batch, dazu die
Thoth-Decoder für Transaktions- und Konten-Delta. Kein Ereignis-Vokabular. Die Datei liegt
im Kompilat bewusst zwischen `Ledger/Events.fs` und `Ledger/Domain.fs` und leiht sich von
dort genau zwei YNAB-geformte Wertobjekte (`ClearedState`, `CategoryRef`).

**Der Vergleich** ist rein und liegt neben `apply`:
`observe : ObservationContext -> Entry option -> YnabObservation -> LedgerEvent list`
(`src/Server/Ledger/Domain.fs`). Unbekannte Buchung → Erstbeobachtung mit vollem Snapshot;
gleiche Daten → keine Ereignisse; geänderte Gruppen → ein Ereignis je Gruppe in der
Reihenfolge Kopfdaten, Kategorisierung, Cleared; `deleted` → ein Löschungs-Ereignis, danach
keins mehr. Eine gelöschte Buchung, die das Kassenbuch nie sah, erzeugt bewusst nichts.
Der `ObservationContext` (Buchungs-Id, aufgelöstes Kassenbuch-Konto, Zeitpunkt) hält Uhr,
Fremd-Id-Suche und Konto-Auflösung aus der reinen Funktion heraus.

**Der Lauf** (`src/Server/Ledger/CommandHandlers.fs`, neu) ist die unreine Seite:
`runMirror` nimmt Uhr und Abrufe injiziert entgegen, zieht die Konten nach, sucht je
Beobachtung die Buchung (YNAB-Id vor ImportId nach ADR 0015; matcht die ImportId, wird die
YNAB-Id als Verknüpfung nachgetragen) und schreibt ausschließlich über `appendAndApply`.
Das `server_knowledge` rückt erst vor, wenn alle Beobachtungen verarbeitet sind; ein
abgebrochener Lauf holt dasselbe Delta erneut, was dank Idempotenz folgenlos ist.
`server_knowledge` und Lauf-Status liegen in den bestehenden Settings
(`ledger_mirror_server_knowledge_<budgetId>`, `ledger_mirror_last_run`), keine neue Tabelle.
Ein 429 bricht ab und steht als Fehlertext im Status. `mirrorYnabBudget ()` verdrahtet den
Lauf mit Token und Standard-Budget.

**Abruf und Endpunkt:** `getTransactionsDelta` / `getAccountsDelta` in
`src/Server/YnabClient.fs` sind neu; die bestehenden `since_date`-Abrufe sind unberührt.
`LedgerApi` (`mirrorYnabBudget`, `getLastMirrorRun`) ist in `src/Shared/Api.fs` definiert,
in `src/Server/Api.fs` implementiert und im `webApp` verdrahtet; der Client-Proxy in
`src/Client/Api.fs` liegt für Slice 3 bereit.

**Zwei Entscheidungen unterwegs**, beide als ADR festgehalten: Konten holt der Spiegel je
Lauf vollständig statt als Delta (ADR 0018, sonst kann eine Beobachtung auf ein Konto
zeigen, das der Lauf nicht kennt), und `SplitTarget` bekommt einen dritten Fall
`Uncategorized` für Split-Zeilen, die YNAB weder kategorisiert noch als Übertrag führt
(ADR 0019, ergänzt ADR 0006; mit dem Tactical Modeler entschieden). Der Push-Encoder hat
dafür einen defensiven Arm bekommen, der die Zeile ohne `category_id` und ohne `payee_id`
serialisiert. Der Begriff `SplitTarget` im `ynab-sync`-README ist entsprechend neu gefasst:
drei Fälle statt zwei, und das XOR gilt nur noch für "Kategorie und Transfer".

**Ein echter Bug, den der Test gefunden hat:** `Decode.datetimeUtc` liest YNABs `date`
("2026-02-03") als Ortszeit und schiebt den Buchungstag in MEZ auf den Vortag 23:00. Der
Spiegel benutzt deshalb einen eigenen `ynabDateDecoder` mit `TryParseExact` und
`DateTimeKind.Utc`. Der bestehende `since_date`-Decoder trägt denselben latenten Fehler,
liegt aber außerhalb dieses Slices — als Backlog-Item erfasst.

**Tests:** 23 neu, alle grün, Suite 676 passed / 7 skipped / 0 failed, Build ohne Warnungen.
`src/Tests/LedgerMirrorTests.fs` (neu, 21 Tests) deckt `observe` rein, die Decoder gegen ein
JSON-Fixture mit Split-, Transfer- und gelöschter Buchung und den Lauf gegen In-Memory-SQLite
ab (Erstlauf, Idempotenz beim zweiten Vollabruf, Kategorie-Änderung als genau ein Ereignis
auf derselben Buchungs-Id, Löschung als Flag ohne physisches Löschen, Übertrag als zwei
Buchungen mit mitgeführten Transfer-Ids, Matching-Reihenfolge in beide Richtungen,
Rate-Limit-Abbruch mit Status, Weiterreichen des gemerkten `server_knowledge`). Zwei weitere
Tests in `src/Tests/SplitPushTests.fs` nageln den Push-Arm für die unkategorisierte Zeile
fest. Eine Mutationsprobe an `observe` (Kategorisierungs-Vergleich fest auf `true`) lässt
vier Tests fallen — die Tests greifen wirklich.

Nicht in diesem Slice: der automatische Auslöser (Frische-Kriterium, Lauf nach Push,
ADR 0014) und das Auflösen von `Entry.TransferEntryId` (ADR 0015, `ledger-t7v2q`).

Anmerkung zum Werkzeug: die Serena-MCP-Tools standen in dieser Session nicht zur Verfügung;
gelesen und editiert wurde mit den gewöhnlichen Werkzeugen.
