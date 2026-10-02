---
id: ledger-saqsk
title: "Chip \"ohne Kategorie\" in der Kassenbuch-Filterleiste: Buchungen ohne Kategorie und Splits ohne eine einzige Kategorie finden"
status: todo
type: feature
context: ledger
created: 2026-09-25
completed:
depends_on: [ledger-j8xq4, ledger-xk9wq, design-system-001]
blocks: []
tags: [backend, frontend, kassenbuch, filter]
related_adrs: [0013, 0015, 0018, 0019, 0032, 0036, 0038, 0039, 0048]
related_research: []
prior_art: [ledger-vgfw3]
---

## Why
Die Kategorienauswahl der Filterleiste (`ledger-j8xq4`) speist sich aus dem YNAB-Katalog
(`getCategories`), nicht aus dem Bestand des Kassenbuchs. Zwei Fälle fallen deshalb durch
jeden Kategorie-Filter, egal wie gut er gebaut ist: eine Buchung ohne Kategorie, und eine
aufgeteilte Buchung, deren Split-Zeilen alle `Uncategorized` sind (ADR 0019). Beide sind
fachlich "unkategorisiert", aber es gibt keine Kategorie, die man auswählen könnte, um sie
zu finden. Genau diese Menge sucht Roman beim Durchsehen. Gefunden bei der Verfeinerung
von `ledger-xk9wq` (2026-09-20), dort ausdrücklich ausgeklammert (ADR 0038), bei
`ledger-j8xq4` am 2026-09-25 ebenfalls als eigenes Item abgetrennt.

Bei der Verfeinerung (2026-10-02) hat sich die Produktfrage "wie werden Überträge
behandelt" als Modellierungsfrage mit Schema-Auswirkung herausgestellt, siehe ADR 0048
(tactical-modeler-Konsultation): `Entry.TransferEntryId` wird erst durch ein zweites
Ereignis gesetzt, sobald der Spiegel die Gegenbuchung findet, bleibt also bei vielen
echten Überträgen auf Dauer `None` (z. B. wenn die Gegenbuchung selbst eine Split-Zeile
ist, `ledger-h4nxz`). `TransferEntryId IS NOT NULL` allein als Übertrags-Erkennung reicht
deshalb nicht; ADR 0048 legt eine zusätzliche, roh projizierte Spalte fest.

## What
Ein eigener Chip "ohne Kategorie" neben der Kategorienauswahl, als eigenes
Filterkriterium im Lesepfad (`LedgerEntryPageQuery`, `Ledger.Persistence.EntryFilter`,
`LedgerPayeeSuggestionQuery`, `buildFilterClause`, ADR 0036), das über alle vier
Zählwege und die Payee-Vorschläge gleich wirkt. Definition und SQL-Klausel: ADR 0048.

**Entscheidung (Roman, 2026-10-02, verfeinert durch ADR 0048):** "Ohne Kategorie" heißt
"ohne Ziel". Ein Übertrag ist ein Ziel, genau wie eine Kategorie, und wird deshalb
ausgenommen. In YNAB gelöschte Buchungen folgen ausschließlich dem bestehenden Schalter
`IncludeDeleted`, kein Sonderfall. Bei einer Split-Buchung zählt jede Zeile einzeln: hat
mindestens eine Zeile ein Ziel (`ToCategory` **oder** `ToTransfer`), ist die Buchung kein
Treffer, auch wenn andere Zeilen noch `Uncategorized` sind (eine Buchung mit teilweiser
Lücke ist bewusst kein Treffer dieses Chips, siehe ADR 0048, Abschnitt "Nicht behandelt").

### Betroffene Stellen (Server)
- `src/Server/Ledger/Domain.fs`: `Entry` bekommt `TransferAccountId: YnabAccountId option`
  (ADR 0048 Punkt 2); `apply`s `EntryObservedInYnab`-Zweig setzt
  `TransferAccountId = p.TransferAccountId`. Neue Funktion `splitTransferAccountIds (entry:
  Entry) : Guid list`, Schwesterfunktion zu `splitCategoryIds`, zieht die Konto-Ids der
  `ToTransfer`-Zeilen aus `entry.Splits` (entdoppelt).
- `src/Server/Ledger/Persistence.fs`:
  - `migrateLedgerEntryColumns`: `ALTER TABLE ledger_entries ADD COLUMN
    transfer_account_id TEXT NULL` (Muster: `approved`/`flag_color`/`flag_name`).
  - `createLedgerSchemaOn`: neue Tabelle `ledger_entry_split_transfers (entry_id TEXT NOT
    NULL, transfer_account_id TEXT NOT NULL, PRIMARY KEY (entry_id,
    transfer_account_id))` plus Index auf `entry_id` (SQL in ADR 0048 Punkt 3).
  - `EntryRow`/`rowToEntry`/`entryParams`/`upsertEntrySql`: neue Spalte lesen/schreiben,
    analog zu `transfer_entry_id`.
  - `writeSplitCategoryIndex` wird zu `writeSplitIndexes`: führt zusätzlich
    `ledger_entry_split_transfers` nach (erst `DELETE` beider Tabellen für die Buchung,
    dann `INSERT` je Kategorie- bzw. Übertrags-Ziel). `rebuildSplitCategoryIndex` wird
    entsprechend zu `rebuildSplitIndexes`, baut beide Tabellen aus `ledger_entries.splits`
    neu auf.
  - `EntryFilter`/`LedgerPayeeSuggestionQuery` (sofern dort gespiegelt) bekommen
    `WithoutCategory: bool`; `buildFilterClause` bekommt die Klausel aus ADR 0048 Punkt 4,
    inklusive der `OR`-Verknüpfung mit einer nicht leeren `CategoryIds`-Auswahl.
  - **Einmaliger Backfill beim Start** (nur wenn `ADD COLUMN` neu gegriffen hat, ADR 0048
    Punkt 5): alle `EntryObservedInYnab`-Ereignisse aus `ledger_entry_events` lesen,
    `transfer_account_id` je Buchung daraus in `ledger_entries` eintragen. Kein
    `json_extract`, keine Umschreibung des Logs (ADR 0013 Punkt 8).
- `src/Shared/Domain.fs`: `LedgerEntryPageQuery` und `LedgerPayeeSuggestionQuery` bekommen
  `WithoutCategory: bool`.

### Betroffene Stellen (Client)
- `src/Client/Components/Ledger/Types.fs`: `ChipId` bekommt `WithoutCategoryChip`, in
  `fixedChipOrder` hinter `CategoryChip` einsortiert; `ChipActivity` bekommt
  `WithoutCategory: bool`; `Filters` bekommt `WithoutCategory: bool`. Wie `NeedsReviewChip`
  (nicht `hasAnyFilter`): ein reiner Toggle ohne Popover/Entwurf, zählt nicht zu "Alle
  Filter löschen" und wird von `ClearAllFilters` nicht zurückgesetzt (derselbe Schnitt wie
  beim Schalter "Gelöschte zeigen" und `OnlyNeedsReview`, siehe bestehender Kommentar an
  `ClearAllFilters`).
  - `Msg`: `ToggleWithoutCategory` (lädt ab Seite 0 neu, analog zu
    `ToggleOnlyNeedsReview`/`ToggleIncludeDeleted`).
- `src/Client/Components/Ledger/View.fs`: `chipLabel`/`isChipActive`/`removeChipMsg`/
  `chipOnClick`/`filterChip` um den neuen Chip-Fall ergänzen; Chip-Text "Ohne Kategorie"
  ohne Zähler (wie bei den bestehenden Chips ohne Popover) oder mit Trefferzahl, falls das
  bestehende Muster das für `NeedsReviewChip` schon tut (dort nachsehen und spiegeln).
- `src/Client/Components/Ledger/State.fs`: `ToggleWithoutCategory`-Zweig analog zu
  `ToggleOnlyNeedsReview`.

## Acceptance criteria
- [ ] `splitTransferAccountIds` liefert für eine Split-Buchung genau die entdoppelten
      Konto-Ids ihrer `ToTransfer`-Zeilen, leer bei keiner solchen Zeile. Reiner
      Domain-Test, analog zu den bestehenden `splitCategoryIds`-Tests.
- [ ] `apply` auf `EntryObservedInYnab` mit gesetztem `TransferAccountId` liefert einen
      `Entry` mit `TransferAccountId = Some ...`; alle übrigen Felder unverändert
      (Feld-für-Feld-Vergleich). Kein anderer Ereignis-Zweig ändert das Feld.
- [ ] Nach `appendAndApply` eines `EntryObservedInYnab` mit `TransferAccountId = Some a`
      steht `a` in der Spalte `transfer_account_id` von `ledger_entries`; `rowToEntry`
      liest denselben Wert zurück (Replay-Test, ADR 0013 Punkt 6).
- [ ] Nach `appendAndApply` einer Split-Buchung mit einer `ToTransfer`-Zeile auf Konto `a`
      steht genau eine Zeile `(entry_id, a)` in `ledger_entry_split_transfers`; eine
      zweite `ToTransfer`-Zeile auf dasselbe Konto erzeugt keine doppelte Zeile
      (Primärschlüssel). `rebuildSplitIndexes` baut dieselbe Zeile aus dem Bestand neu auf.
- [ ] `buildFilterClause` mit `WithoutCategory = true` liefert, gegen eine gemischte
      Testmenge, GENAU folgende Treffer (je ein Fall, mindestens ein Test je Zeile):
      - einfache Buchung ohne Kategorie und ohne Übertrag: **Treffer**.
      - einfache Buchung mit `transfer_account_id` gesetzt, `transfer_entry_id = NULL`
        (Übertrag, Gegenbuchung nicht aufgelöst): **kein Treffer**.
      - einfache Buchung mit `transfer_entry_id` gesetzt, `transfer_account_id = NULL`
        (Rand: Verknüpfung ohne rohen Wert): **kein Treffer**.
      - einfache Buchung mit Kategorie: **kein Treffer**.
      - Split-Buchung, alle Zeilen `Uncategorized`: **Treffer**.
      - Split-Buchung, eine Zeile `ToCategory`, Rest `Uncategorized`: **kein Treffer**
        (unverändertes Verhalten von ADR 0038).
      - Split-Buchung, eine Zeile `ToTransfer`, Rest `Uncategorized`, keine `ToCategory`-
        Zeile: **kein Treffer** (der hier entschiedene Fall).
      - in YNAB gelöschte Buchung ohne Kategorie, `IncludeDeleted = false`: **kein
        Treffer**; mit `IncludeDeleted = true`: **Treffer**.
- [ ] `WithoutCategory = true` zusammen mit einer nicht leeren `CategoryIds`-Auswahl
      liefert die Vereinigung beider Mengen (Test mit einer Buchung, die nur die
      Kategorie-Bedingung erfüllt, einer, die nur "ohne Kategorie" erfüllt, und einer, die
      keine der beiden erfüllt).
- [ ] Alle vier Zählwege (`countEntries`, `countDeletedEntries`, `countNeedsReview`, wie
      im Modul benannt) und `loadPayeeSuggestions` wenden `WithoutCategory` sichtbar an
      (je ein Test, der mit/ohne den Filter unterschiedliche Zahlen liefert).
- [ ] Der einmalige Backfill setzt `transfer_account_id` für eine Bestandsbuchung, deren
      einziges `EntryObservedInYnab`-Ereignis `transferAccountId` trägt, korrekt; ein
      zweiter Start (Spalte existiert schon) läuft den Backfill nicht erneut (Test gegen
      In-Memory-SQLite mit einem vorbereiteten Log, ADR 0030).
- [ ] Client: `ToggleWithoutCategory` kippt `Filters.WithoutCategory` und lädt ab Seite 0
      neu (State-Test wie bei `ToggleOnlyNeedsReview`); der Chip erscheint in
      `fixedChipOrder`/`orderChipsForDisplay`, ist kein Teil von `hasAnyFilter` und bleibt
      von `ClearAllFilters` unberührt (je ein Test, analog zu den bestehenden
      `NeedsReviewChip`-Tests).
- [ ] `dotnet build` und `dotnet test` grün. Tests gegen In-Memory-SQLite, nie gegen die
      Entwicklungsdatenbank (ADR 0030).
- [ ] `ledger`-README: Glossar-Eintrag "Ohne Kategorie" (Chip) und ein Satz im Abschnitt
      "Migration bestehender Zustandstabellen" zur neuen Spalte/Tabelle, Wortlaut-Vorschlag
      in ADR 0048.

## Notes
Abgetrennt am 2026-09-25 bei der Verfeinerung von `ledger-j8xq4`. Modelliert am 2026-10-02
vom tactical-modeler (Konsultation durch den orchestrator); Entscheidung und Schema in ADR
0048. Offener Punkt, den der Umsetzende NICHT lösen muss, sondern nur kennen sollte: eine
Split-Buchung mit einer **teilweisen** Lücke (eine Zeile hat ein Ziel, eine andere ist
`Uncategorized`) ist bewusst kein Treffer dieses Chips (ADR 0048, "Nicht behandelt"). Ein
eigener Chip dafür wäre ein neues, separat zu entscheidendes Backlog-Item, kein Teil dieser
Aufgabe.
