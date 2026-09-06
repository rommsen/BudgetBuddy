---
id: ledger-m8r2v
title: Spiegel-Lauf YNAB → Kassenbuch: Delta-Abruf, Snapshot-Vergleich, Ereignisse, manueller Auslöser
status: doing
type: feature
context: ledger
created: 2026-09-06
depends_on: [ledger-e3k7p, ynab-czk34]
blocks: [ledger-q5w9c, ledger-kt0wq]
tags: [foundation, backend, integration]
related_adrs: [0013, 0014, 0015]
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
- [ ] Delta-Abruf mit `last_knowledge_of_server` implementiert; Erstbefüllung ohne
      Knowledge; `server_knowledge` je Budget persistiert.
- [ ] Zweiter Lauf ohne Änderung erzeugt kein Ereignis (Test).
- [ ] Kategorie-Änderung in YNAB erzeugt genau ein Änderungs-Ereignis auf derselben
      Buchungs-Id (Test mit zwei Beobachtungen nacheinander).
- [ ] Löschung in YNAB setzt das Flag und erzeugt ein Ereignis; nichts wird physisch
      gelöscht (Test).
- [ ] Überträge kommen als zwei Buchungen mit mitgeführter Verknüpfung an (ADR 0015).
- [ ] Manueller Endpunkt vorhanden; Lauf-Status abrufbar (Zeitpunkt, Ergebnis, Fehler).
- [ ] Bestehende Sync-Pipeline, Push und Quick Add unverändert; alle bestehenden Tests grün.
- [ ] `dotnet build` und `dotnet test` grün.
- [ ] `ledger/README.md` und `ynab-sync/README.md`: Spiegel-Lauf und Contract in der
      Sprache dokumentiert (je ein kurzer Abschnitt).

## Notes
Dieser Task berührt zwei BCs: der Abruf und der Contract gehören zu `ynab-sync`
(Conformist-Rand), der Vergleich und das Schreiben zu `ledger`. Der Worker darf beide
READMEs anfassen, aber nur die neuen Abschnitte. Der Comdirect-Konto-Filter aus dem
ursprünglichen Skeleton entfällt: der Spiegel ist budgetweit (ADR 0014); die Seite
(`ledger-q5w9c`) zeigt dann ein Konto.
