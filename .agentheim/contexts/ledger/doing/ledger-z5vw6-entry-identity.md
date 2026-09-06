---
id: ledger-z5vw6
title: Identität einer Buchung über Systeme hinweg festlegen (eigene Id + Fremd-Id-Verknüpfung)
status: doing
type: decision
context: ledger
created: 2026-09-06
depends_on: []
blocks: [ledger-kt0wq]
tags: [foundation, domain]
related_adrs: [0012, 0004]
related_research: []
prior_art: []
---

## Why
Der Spiegel muss seine Beobachtungen der richtigen Buchung zuordnen, und BB-eigene
Entscheidungen müssen später der YNAB-Buchung zugeordnet werden, die daraus entstand.
Heute gibt es drei Fremdschlüssel (Comdirect-Reference, ImportId, YNAB-Transaction-Id),
Quick-Add-Buchungen haben bewusst keine ImportId (ADR 0004), und der Push wertet YNABs
Antwort nur nach Anzahl und Duplikaten aus, nicht nach zurückgegebenen Ids.

## What
ADR aus dem Entwurf unten committen. Kein Code.

Routing: **BC-lokal (`ledger`)**, mit Auswirkung auf `ynab-sync` (Push muss YNAB-Ids aus
der Antwort mitnehmen) und `banking-import` (Reference wird mitgeführt). Test: ohne
Kassenbuch gäbe es die Frage nicht.

## Acceptance criteria
- [ ] ADR committed, `scope: ledger`.
- [ ] Das ADR benennt: eigene Buchungs-Id, die Fremd-Id-Verknüpfung, die Match-Reihenfolge
      im Spiegel, und was bei "kein Match" passiert (neue Buchung mit Herkunft Spiegel).
- [ ] Die Folgeänderung an `ynab-sync` (YNAB-Ids aus `POST /transactions` mitnehmen, für
      Import-Push und Quick Add) ist als Task im `ynab-sync`-Backlog erfasst.
- [ ] `ledger/README.md` Open Question "Identität der Buchung" als gelöst markiert.
- [ ] Keine Code-Änderung.

## Notes
### ADR-Entwurf (Architekten-Pass, 2026-09-06)

```markdown
---
id: NNNN
title: Buchungs-Identität: eigene Id im Kassenbuch, Fremd-Ids als Verknüpfungen, Spiegel matcht YNAB-Id vor ImportId
scope: ledger
status: proposed
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ledger-z5vw6]
related_research: []
---

## Context
Eine Buchung hat je nach Weg bis zu drei Fremdschlüssel: Comdirect-Reference (Bank),
ImportId (BB-generiert, YNAB-Dedup), YNAB-Transaction-Id (vergibt YNAB). Quick Add hat
keine ImportId (ADR 0004). Der Spiegel liefert YNAB-Ids und ggf. ImportIds; BB-eigene
Konten haben gar keine Fremd-Ids. Der Push (`createTransactions`, `createManualTransaction`)
liest YNABs Antwort heute nicht auf zurückgegebene Ids aus.

## Decision
- Jede Buchung im Kassenbuch hat eine **eigene, stabile Id** (zeitsortierbar, z.B. ULID),
  vergeben beim ersten Ereignis, nie geändert.
- **Fremd-Ids sind Verknüpfungen**, keine Teile der Id: pro Buchung null bis n Einträge
  (System: ynab | importid | comdirect; Wert). Eine Fremd-Id gehört zu höchstens einer
  Buchung (Unique).
- **Match-Reihenfolge im Spiegel:** 1. YNAB-Id, 2. ImportId, 3. kein Match → neue Buchung
  mit Herkunft "YNAB-Spiegel" (das ist der Normalfall für ING-Import und Änderungen in
  YNAB an Buchungen, die BB nie gesehen hat).
- **Beim Push** nimmt `ynab-sync` die von YNAB zurückgegebenen Transaction-Ids mit und
  verknüpft sie sofort (Import-Push über ImportId-Zuordnung, Quick Add über die Antwort
  des Einzel-Posts). Damit ist auch eine Quick-Add-Buchung ab dem ersten Spiegel-Lauf
  eindeutig zuordenbar.
- Splits sind Teil der Buchung, keine eigenen Buchungen; YNABs Subtransaction-Ids werden
  im Snapshot mitgeführt, aber nicht als Verknüpfung.
- Erstbefüllung und wiederholte Vollabrufe sind **idempotent**: gleiche YNAB-Id, keine
  Änderung im Snapshot → kein neues Ereignis.

## Consequences
### Positive
- Spiegel-Updates treffen sicher; BB-Entscheidungen bleiben ihrer YNAB-Buchung zugeordnet.
- Idempotenz macht den Spiegel robust gegen verlorenes `server_knowledge`.
### Negative
- `ynab-sync` muss die Push-Antwort auswerten (kleine, aber echte Änderung am
  Conformist-Rand, inkl. Tests).
- Eine Buchung, die BB gepusht hat und die YNAB als Duplikat ablehnt, bleibt ohne
  YNAB-Id, bis der Spiegel sie über die ImportId findet.
### Neutral
- Comdirect-Reference wird nur mitgeführt, nie gematcht (der Spiegel kennt sie nicht).

## Alternatives considered
- **YNAB-Id als Primärschlüssel** — abgelehnt: BB-eigene Konten haben keine; Buchungen
  existieren in BB vor dem Push.
- **ImportId als Primärschlüssel** — abgelehnt: Quick Add und Spiegel-Buchungen haben keine.
- **Fuzzy-Match (Betrag/Datum/Payee) im Spiegel** — abgelehnt als Regel, bleibt als
  Werkzeug der Duplikat-Erkennung in `banking-import`.
```

### Entscheidung mit Roman (2026-09-06, Work-Session)

Der Entwurf oben gilt mit diesen Ergänzungen:

1. **Eigene Id per `Guid.CreateVersion7()`** (.NET 9, zeitsortierbar, keine Library) statt
   ULID.
2. **Fremd-Ids in eigener Verknüpfungstabelle** (Buchungs-Id, System, Wert; Wert eindeutig
   je System). Systeme: `ynab`, `importid`, `comdirect`.
3. **Überträge** (fehlte im Entwurf): YNAB führt einen Übertrag als zwei Buchungen auf zwei
   Konten, verbunden über `transfer_transaction_id`. Im Kassenbuch sind das **zwei
   Buchungen**; die Verbindung wird als Verknüpfung mitgeführt. Die Sicht "eine Bewegung"
   ist v2-Arbeit, wenn Überträge BB-geführt werden. Ohne diesen Satz rät der Skeleton beim
   ersten Übertrag auf dem Comdirect-Konto (Bargeld-Abhebungen).
4. Bestätigt: der Push liest heute keine Ids aus (`TransactionCreateResult` in
   `YnabClient.fs` hat nur `CreatedCount`, `DuplicateImportIds`,
   `RejectedTransferTransactionIds`). Der Folge-Task in `ynab-sync` bleibt Acceptance
   Criterion. Begründung im ADR am Quick-Add-Beispiel: ohne ImportId (ADR 0004) ist die
   Push-Antwort die einzige Stelle, an der BBs Entscheidung und YNABs Buchung verbunden
   werden können; sonst entstehen zwei Buchungen für dieselbe Bar-Ausgabe.
