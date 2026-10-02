---
id: 0048
title: Chip "ohne Kategorie" fragt nach einem fehlenden Ziel, nicht nach einer fehlenden Kategorie; transfer_account_id wird roh in den Zustand projiziert
scope: ledger
status: accepted
date: 2026-10-02
supersedes: []
superseded_by: []
related_tasks: [ledger-saqsk]
related_adrs: [0013, 0015, 0018, 0019, 0032, 0036, 0038, 0039]
related_research: []
---

# ADR 0048: "Ohne Kategorie" heißt "ohne Ziel"

## Context

`ledger-saqsk` soll einen Filter-Chip "ohne Kategorie" liefern, der genau die Buchungen
findet, bei denen Roman wirklich noch etwas zuordnen muss. Roman hat entschieden: Überträge
zählen nicht dazu (sie tragen in YNAB kein `category_id`, sind aber kein offener Fall), in
YNAB gelöschte Buchungen folgen dem bestehenden Schalter `IncludeDeleted`.

Zwei Fälle machen `category_id IS NULL` allein zu einer falschen Bedingung:

1. **Einfache Buchung, Übertrag.** YNAB liefert für einen Übertrag kein `category_id`, exakt
   dieselbe Beobachtung wie bei einer echten Kategorie-Lücke. Die einzige vorhandene
   Unterscheidung im Zustand ist `Entry.TransferEntryId` (Spalte `transfer_entry_id`). Das
   Feld wird aber erst durch ein zweites, separates Ereignis gesetzt
   (`EntryTransferCounterpartObservedInYnab`, `ledger-t7v2q`), und zwar nur, wenn der
   Spiegel-Lauf die Gegenbuchung über `findEntryByLink` tatsächlich findet (eigene Buchung,
   gemirrortes Konto, `transfer_transaction_id` auf Buchungsebene). Eine Gegenbuchung, die
   selbst eine Split-Zeile ist, trägt laut `YnabObservations.RawSubtransaction` kein
   `transfer_transaction_id` (nur `transfer_account_id`): der Lookup findet sie
   grundsätzlich nie (dieselbe Lücke, die `ledger-h4nxz` für die Verknüpfung offen lässt).
   `TransferEntryId IS NOT NULL` als alleiniges Ausschlusskriterium ließe solche Überträge
   dauerhaft im Chip stehen, nicht nur in einem kurzen Zeitfenster.
2. **Split-Buchung.** `Ledger.Domain.splitCategoryIds` speist nur `ToCategory`-Zeilen in die
   Indextabelle `ledger_entry_split_categories` (ADR 0038); `ToTransfer`- und
   `Uncategorized`-Zeilen bleiben dort unsichtbar. Eine Split-Buchung mit mindestens einer
   `ToCategory`-Zeile hat bereits eine Indexzeile und fällt korrekt aus dem Chip heraus. Eine
   Split-Buchung, deren Zeilen ausschließlich `ToTransfer` (z. B. Aufteilung auf zwei
   Zielkonten) und/oder `Uncategorized` sind, hat dagegen gar keine Indexzeile und
   `category_id IS NULL` am Kopf: sie würde ohne weitere Behandlung fälschlich im Chip
   landen, obwohl eine reine Übertrags-Zeile (`ToTransfer`) genauso ein gesetztes Ziel ist
   wie eine Kategorie-Zeile.

`TransferAccountId` (YNABs `transfer_account_id`) steht roh in `EntryObservedInYnabPayload`
(Events.fs, beide Schema-Versionen 1 und 2 tragen das Feld, geprüft), wird aber nirgends in
`Entry` oder die Spalte `ledger_entries` projiziert, nur die aufgelöste `TransferEntryId`
landet im Zustand. Für Split-Zeilen trägt `SplitTarget.ToTransfer` das Übertrags-Ziel bereits
vollständig (Konto-Id und Name), nur ohne SQL-abfragbare Form.

## Decision

1. **Definition: eine Buchung ist "ohne Kategorie", wenn sie kein Ziel trägt.** Kategorie und
   Übertrag sind beide ein Ziel.
   - Einfache Buchung: weder Kategorie noch Übertrag. Übertrag erkannt an `transfer_account_id
     IS NOT NULL` (roh beobachtet, Punkt 2) **oder** `transfer_entry_id IS NOT NULL`
     (aufgelöste Gegenbuchung): beide reichen unabhängig voneinander, weil
     `transfer_account_id` nicht nachgezogen wird, wenn eine Buchung erst später per
     Detail-Änderung zum Übertrag wird (bislang nicht modelliert, aber `transfer_entry_id`
     fängt diesen Rand ab, falls er doch eintritt).
   - Split-Buchung: jede Zeile ist eine `Uncategorized`-Zeile (ADR 0019). Eine Split-Buchung
     mit mindestens einer `ToCategory`- **oder** `ToTransfer`-Zeile hat ein Ziel und zählt
     nicht als "ohne Kategorie", auch wenn andere Zeilen in derselben Buchung noch
     `Uncategorized` sind. Eine teilweise aufgeteilte Buchung mit einer Lücke in einer
     einzelnen Zeile ist bewusst **kein** Treffer des Chips (Konsequenzen, "Nicht
     behandelt").
   - In YNAB gelöschte Buchungen folgen ausschließlich `IncludeDeleted`, kein Sonderfall.
2. **`TransferAccountId` wird roh in den Zustand projiziert, kein gedeutetes Flag.**
   `Entry` bekommt ein neues Feld `TransferAccountId: YnabAccountId option`, gesetzt in
   `Ledger.Domain.apply` im `EntryObservedInYnab`-Zweig (`TransferAccountId = p.TransferAccountId`,
   genau wie die anderen Kopf-Felder dort). Kein anderer Ereignis-Zweig ändert es: YNAB macht
   aus einer Buchung nach ihrer Entstehung in diesem Modell nie einen Übertrag (dieselbe
   Annahme, unter der `EntryCategorizationChangeObservedInYnab` heute schon Category/Splits
   ändert, aber keinen Übertrag daraus macht). `Events.fs` ändert sich nicht: das Feld steht
   bereits in beiden Schema-Versionen der Payload, nur ungenutzt. `ledger_entries` bekommt die
   Spalte `transfer_account_id TEXT NULL`, per `ALTER TABLE` in `migrateLedgerEntryColumns`
   (Muster: `approved`/`flag_color`/`flag_name`), gelesen/geschrieben analog zu
   `transfer_entry_id` in `rowToEntry`/`entryParams`/`upsertEntrySql`. Kein `is_transfer`-Flag:
   der rohe Wert ist ehrlicher (ADR 0013, "roh im Log, gedeutet im Lesepfad", hier roh im
   *Zustand*, gedeutet erst in der WHERE-Klausel) und eine spätere zusammengefasste
   Übertrags-Sicht (v2, siehe ADR 0015) kann ihn direkt weiterverwenden, ohne erneut Codec- und
   Projektionsarbeit zu leisten.
3. **Zweite abgeleitete Indextabelle für Split-Übertrags-Zeilen**, neben
   `ledger_entry_split_categories` (ADR 0038), nach demselben Muster:
   ```sql
   CREATE TABLE IF NOT EXISTS ledger_entry_split_transfers (
       entry_id            TEXT NOT NULL,
       transfer_account_id TEXT NOT NULL,
       PRIMARY KEY (entry_id, transfer_account_id)
   );
   CREATE INDEX IF NOT EXISTS idx_ledger_entry_split_transfers_entry
       ON ledger_entry_split_transfers(entry_id);
   ```
   Befüllt über eine Schwesterfunktion zu `splitCategoryIds` in `Ledger.Domain`, z. B.
   `splitTransferAccountIds (entry: Entry) : Guid list`, die aus `entry.Splits` die
   Konto-Ids der `ToTransfer`-Zeilen zieht (entdoppelt), symmetrisch zu `splitCategoryIds`.
   Geschrieben wird sie an derselben Stelle und in derselben Transaktion wie
   `writeSplitCategoryIndex` (eine gemeinsame Funktion, z. B. `writeSplitIndexes`, die beide
   Tabellen nachführt: erst `DELETE` beider Tabellen für die Buchung, dann `INSERT` je
   Kategorie- bzw. Übertrags-Ziel). `rebuildSplitCategoryIndex` wird entsprechend zu
   `rebuildSplitIndexes` und baut beide Tabellen aus `ledger_entries.splits` neu auf
   (dasselbe `WHERE splits <> '[]'`, Parsen in F#, keine SQLite-JSON-Funktion, ADR 0013 Punkt
   8). Die Konto-Id statt nur `entry_id` kostet nichts Zusätzliches und hält die Tabelle
   symmetrisch zur Kategorie-Tabelle.
4. **SQL-Klausel in `buildFilterClause`.** Neues Feld `WithoutCategory: bool` an
   `EntryFilter`, `LedgerEntryPageQuery` und `LedgerPayeeSuggestionQuery`. Das Prädikat (Spalten-
   /Tabellennamen wie oben):
   ```sql
   (category_id IS NULL
    AND transfer_account_id IS NULL
    AND transfer_entry_id IS NULL
    AND id NOT IN (SELECT entry_id FROM ledger_entry_split_categories)
    AND id NOT IN (SELECT entry_id FROM ledger_entry_split_transfers))
   ```
   Wirkt per `AND` mit `OnlyNeedsReview`, Kontoauswahl, Zeitraum, Payee, Memo, Betrag und
   `IncludeDeleted`, wie jedes bestehende Kriterium in `buildFilterClause`. Mit der
   Kategorienauswahl (`CategoryIds`) verknüpft es sich dagegen per `OR`, in einer gemeinsamen
   Klammer: `(<Kategorie-Treffer-Klausel> OR <ohne-Kategorie-Klausel>)`, wenn beide gesetzt
   sind. Begründung: der Chip ist eine zusätzliche Option auf derselben Achse ("welche
   Kategorie, oder keine") wie die Kategorienauswahl, kein zusätzliches Ausschlusskriterium:
   eine `AND`-Verknüpfung ergäbe strukturell immer die leere Menge, weil keine Buchung
   gleichzeitig eine der gewählten Kategorien UND keine Kategorie tragen kann. Ist nur der
   Chip aktiv, gilt nur die "ohne Kategorie"-Klausel; ist nur die Kategorienauswahl aktiv,
   gilt nur deren Klausel; unverändert gegenüber heute.
   Die Klausel kommt automatisch allen acht Lese-Funktionen zugute, die durch
   `buildFilterClause` laufen (vier Zählwege, Summenzeile, `loadPayeeSuggestions`), weil sie
   alle dieselbe private Funktion aufrufen, dieselbe Garantie, die ADR 0036 für die
   bestehenden fünf Such-Filter schon trägt.
5. **Migration des Bestands: einmaliger Backfill aus dem Log, kein Umschreiben.** Die neue
   Spalte `transfer_account_id` startet nach `ALTER TABLE` leer (`NULL`) für alle rund 14.000
   Bestandsbuchungen. Ein reiner Spalten-Default ohne Backfill (wie bei `approved` in ADR
   0032, dort bewusst `DEFAULT 1`/"gilt als genehmigt") ist hier **keine vertretbare
   Näherung**: anders als bei `approved`, wo der Default die sichere, unauffällige Seite
   trifft, träfe ein `NULL`-Default hier für jeden Bestands-Übertrag genau die falsche Seite
   und würde den Chip am ersten Tag mit Alt-Überträgen überschwemmen, bis jede einzelne
   Buchung einmal erneut beobachtet wird. Das widerspricht dem eigentlichen Zweck des Chips.
   Deshalb: ein einmaliger Lauf, nur wenn das `ALTER TABLE` tatsächlich neu gegriffen hat
   (dieselbe Bedingung, mit der `migrateColumn` schon zwischen Erst- und Folgestart
   unterscheidet), der jedes `EntryObservedInYnab`-Ereignis aus `ledger_entry_events` über den
   bestehenden Decoder liest (kein `json_extract`, ADR 0013 Punkt 8 bleibt gewahrt: gelesen
   wird nur, das Log wird nicht umgeschrieben) und `transfer_account_id` direkt in
   `ledger_entries` einträgt. `TransferAccountId` ändert sich laut Punkt 2 nach der Entstehung
   nie wieder, ein Entstehungs-Ereignis je Buchung reicht als Quelle. Die neue
   Split-Indextabelle braucht kein eigenes Backfill: sie entsteht leer und wird vom ohnehin
   vorhandenen `rebuildSplitIndexes`-Lauf aus der Spalte `splits` gefüllt.

## Consequences

### Positive
- Der Chip trifft ab dem ersten Tag auch auf den Bestand zu, nicht erst, nachdem jede
  Alt-Buchung erneut vom Spiegel gesehen wurde.
- `Events.fs` bleibt unberührt: `TransferAccountId` stand schon in der Payload, nur ungenutzt.
- Dieselbe Projektion (`transfer_account_id` im Zustand) steht für eine spätere
  zusammengefasste Übertrags-Sicht (v2) zur Verfügung, ohne dass dort erneut Codec-Arbeit
  nötig wird.
- `ledger_entry_split_transfers` ist symmetrisch zu `ledger_entry_split_categories` und damit
  für Worker ohne neue Lesegewohnheit verständlich.

### Negative
- Zwei neue Tabellen-/Spalten-Konzepte (eine Spalte, eine Tabelle) für einen einzelnen Chip;
  der Migrationslauf liest einmalig den gesamten Bestand an `EntryObservedInYnab`-Ereignissen
  (rund 14.000), kostet also einen spürbaren, aber einmaligen Start.
- `writeSplitCategoryIndex`/`rebuildSplitCategoryIndex` wachsen um eine zweite Tabelle
  (Umbenennung auf `writeSplitIndexes`/`rebuildSplitIndexes`), jeder bisherige Aufrufer muss
  einmal angefasst werden.

### Nicht behandelt (bewusst, Romans Entscheidung)
Eine Split-Buchung mit einer **teilweisen** Lücke (eine Zeile hat ein Ziel, eine andere ist
`Uncategorized`) ist **kein** Treffer dieses Chips: die Definition in Punkt 1 verlangt, dass
*jede* Zeile `Uncategorized` ist. Eine solche Buchung bleibt unsichtbar für den Chip, obwohl
eine Zeile fachlich noch zuzuordnen wäre. Käme ein eigener Chip "teilweise unkategorisierte
Splits" in Frage, ist das ein eigenes, später zu entscheidendes Backlog-Item, kein Teil dieser
Entscheidung.

### Neutral
- Umkehrbar: stellt sich die `OR`-Verknüpfung mit der Kategorienauswahl als unerwartet heraus
  (z. B. weil Roman die beiden Filter lieber gegenseitig ausschließen will), ist das eine
  lokale Änderung der Klammerung in `buildFilterClause`, ohne Schema-Auswirkung.

## Alternatives considered

**`TransferEntryId IS NOT NULL` als alleiniges Ausschlusskriterium, ohne neue Spalte.**
Billigste Umsetzung, aber systematisch falsch: jeder Übertrag, dessen Gegenbuchung eine
Split-Zeile ist (kein `transfer_transaction_id` auf Buchungsebene, `ledger-h4nxz`) oder deren
Gegenkonto nicht gemirrort ist, bliebe dauerhaft, nicht nur übergangsweise, im Chip stehen.
Abgelehnt, weil der ganze Zweck des Chips ("wo muss ich wirklich noch zuordnen") daran
hängt, dass er keine falschen Treffer zeigt.

**`is_transfer: bool` statt des rohen `transfer_account_id`.** Spart in der WHERE-Klausel ein
`IS NOT NULL`, verwirft aber die Konto-Id, die eine spätere Übertrags-Sicht (ADR 0015, v2)
ohnehin bräuchte. Abgelehnt: der rohe Wert ist genauso billig zu prüfen und strikt
informativer.

**Spalten-Default ohne Backfill (wie `approved` in ADR 0032).** Für `approved` eine bewusst
vertretbare Näherung, weil der Default die harmlose Seite trifft. Für `transfer_account_id`
träfe der Default (`NULL`, "kein Übertrag") die falsche Seite für jeden Bestands-Übertrag und
würde den Chip am Start mit falschen Treffern überschwemmen. Abgelehnt.

**JSON-Abfrage auf der `splits`-Spalte für Übertrags-Zeilen statt einer zweiten
Indextabelle.** Durch ADR 0013 Punkt 8 ohnehin ausgeschlossen (keine SQLite-JSON-Funktionen
in Lese-Abfragen); dieselbe Begründung, mit der ADR 0038 die erste Indextabelle eingeführt
hat, gilt hier unverändert für Übertrags-Zeilen.
