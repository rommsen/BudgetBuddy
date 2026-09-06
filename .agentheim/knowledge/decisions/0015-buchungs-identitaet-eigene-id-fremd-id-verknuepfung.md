---
id: 0015
title: Buchungs-Identität: eigene Id im Kassenbuch, Fremd-Ids als Verknüpfungen, Spiegel matcht YNAB-Id vor ImportId
scope: ledger
status: accepted
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ledger-z5vw6]
related_research: []
---

## Context
Eine Buchung hat je nach Weg bis zu drei Fremdschlüssel: Comdirect-Reference (Bank),
ImportId (BB-generiert, YNAB-Dedup), YNAB-Transaction-Id (vergibt YNAB). Quick Add hat
keine ImportId (ADR 0004, `ynab-sync`). Der Spiegel (ADR 0014, `ynab-sync`) liefert
YNAB-Ids und ggf. ImportIds; BB-eigene Konten haben gar keine Fremd-Ids. Der Push
(`createTransactions`, `createManualTransaction`) liest YNABs Antwort heute nicht auf
zurückgegebene Ids aus: `TransactionCreateResult` in `src/Server/YnabClient.fs` trägt nur
`CreatedCount`, `DuplicateImportIds` und `RejectedTransferTransactionIds`, keine
Transaction-Ids.

YNAB führt zusätzlich einen Übertrag zwischen zwei Konten als zwei Transaktionen, die über
`transfer_transaction_id` verbunden sind (Research:
`ynab-transfer-in-split-subtransaction-2026-06-13.md`). Das Kassenbuch (ADR 0013,
`ledger-fsz0a`) muss auch dafür eine Identität festlegen, sonst rät der Skeleton beim
ersten Übertrag auf dem Comdirect-Konto (Bargeld-Abhebungen).

## Decision
- Jede Buchung im Kassenbuch hat eine **eigene, stabile Id**: ein `Guid`, erzeugt mit
  `Guid.CreateVersion7()` (.NET 9, zeitsortierbar, keine zusätzliche Library). Vergeben
  beim ersten Ereignis, nie geändert.
- **Fremd-Ids sind Verknüpfungen**, keine Teile der Id. Eigene Tabelle mit den Spalten
  Buchungs-Id, System, Wert. System ist eine von `ynab`, `importid`, `comdirect`. Pro
  Buchung stehen null bis n Zeilen; ein Wert ist je System eindeutig (Unique-Constraint auf
  System + Wert), sodass eine Fremd-Id höchstens einer Buchung gehört.
- **Match-Reihenfolge im Spiegel:**
  1. YNAB-Id (`ynab`-Verknüpfung) matcht.
  2. Sonst ImportId (`importid`-Verknüpfung) matcht.
  3. Sonst kein Match: neue Buchung mit Herkunft "YNAB-Spiegel". Das ist der Normalfall für
     ING-Import und für Änderungen in YNAB an Buchungen, die BB nie gesehen hat.
- **Beim Push** nimmt `ynab-sync` die von YNAB zurückgegebenen Transaction-Ids mit und
  verknüpft sie sofort:
  - Import-Push: Zuordnung über die ImportId, die BB selbst gesetzt hat.
  - Quick Add: Zuordnung über die Antwort des Einzel-Posts (`POST /transactions`), da Quick
    Add nach ADR 0004 keine ImportId trägt. Ohne diese Auswertung ist die Push-Antwort die
    einzige Stelle, an der BBs Entscheidung (die Quick-Add-Buchung im Kassenbuch) und YNABs
    Buchung verbunden werden können. Bleibt das aus, sieht der nächste Spiegel-Lauf die
    YNAB-Buchung als kein Match und legt eine zweite Buchung für dieselbe Bar-Ausgabe an.
  - Damit ist auch eine Quick-Add-Buchung ab dem ersten Spiegel-Lauf nach dem Push
    eindeutig zuordenbar, ohne auf ImportId angewiesen zu sein.
- **Überträge:** YNAB führt einen Übertrag als zwei Transaktionen auf zwei Konten,
  verbunden über `transfer_transaction_id`. Im Kassenbuch entstehen dafür **zwei
  Buchungen** (je Konto eine), nicht eine. Die Verbindung zwischen ihnen wird als eigene
  Verknüpfung mitgeführt (analog zur Fremd-Id-Verknüpfung, aber Buchung-zu-Buchung statt
  Buchung-zu-Fremdsystem). Die Sicht "ein Übertrag ist eine Bewegung" ist v2-Arbeit, für
  den Fall, dass Überträge BB-geführt werden.
- Splits sind Teil der Buchung, keine eigenen Buchungen; YNABs Subtransaction-Ids werden im
  Snapshot mitgeführt, aber nicht als Verknüpfung.
- Erstbefüllung und wiederholte Vollabrufe sind **idempotent**: gleiche YNAB-Id, keine
  Änderung im Snapshot → kein neues Ereignis.

## Consequences
### Positive
- Spiegel-Updates treffen sicher; BB-Entscheidungen bleiben ihrer YNAB-Buchung zugeordnet,
  auch für Quick Add ohne ImportId.
- Idempotenz macht den Spiegel robust gegen verlorenes `server_knowledge`.
- Überträge sind von Anfang an modelliert, kein Nacharbeiten beim ersten Comdirect-Übertrag
  nötig.

### Negative
- `ynab-sync` muss die Push-Antwort auswerten (kleine, aber echte Änderung am
  Conformist-Rand, inkl. Tests). Nachfolge-Task erfasst.
- Eine Buchung, die BB gepusht hat und die YNAB als Duplikat ablehnt, bleibt ohne YNAB-Id,
  bis der Spiegel sie über die ImportId findet.
- Zwei Buchungen pro Übertrag bedeuten doppelte Buchführung für dieselbe Bewegung, bis v2
  eine zusammengefasste Sicht liefert.

### Neutral
- Comdirect-Reference wird nur mitgeführt, nie gematcht (der Spiegel kennt sie nicht).

## Alternatives considered
- **YNAB-Id als Primärschlüssel**: abgelehnt, BB-eigene Konten haben keine; Buchungen
  existieren in BB vor dem Push.
- **ImportId als Primärschlüssel**: abgelehnt, Quick Add und Spiegel-Buchungen haben keine.
- **Fuzzy-Match (Betrag/Datum/Payee) im Spiegel**: abgelehnt als Regel, bleibt als
  Werkzeug der Duplikat-Erkennung in `banking-import`.
- **ULID statt `Guid.CreateVersion7()`**: abgelehnt, zusätzliche Library für denselben
  Effekt (zeitsortierbare Id), .NET 9 liefert das nativ.
- **Ein Übertrag als eine Buchung mit zwei Konten-Feldern**: abgelehnt für v1: passt nicht
  zur bestehenden Ein-Konto-pro-Buchung-Struktur des Kassenbuchs (ADR 0013) und würde die
  Match-Logik im Spiegel verkomplizieren, bevor Überträge überhaupt BB-geführt sind.
