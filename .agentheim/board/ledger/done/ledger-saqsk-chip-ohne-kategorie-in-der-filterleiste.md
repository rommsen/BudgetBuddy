---
id: ledger-saqsk
title: "Chip \"ohne Kategorie\" in der Kassenbuch-Filterleiste: Buchungen ohne Kategorie und Splits ohne eine einzige Kategorie finden"
status: done
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

## Merge-conflict note (iteration 1)

Sibling: ledger-h4nxz (Split-Zeilen-Überträge über Subtransaction-Id, bereits auf main integriert).
New base SHA: 3250658c40cf6127083e2d5de6aa385bed9a707c
Resolution allow-list: src/Server/Ledger/Domain.fs, src/Server/Ledger/Persistence.fs, src/Tests/LedgerPageTests.fs, src/Tests/LedgerTests.fs
Salvage: /Users/romansachse/src/BudgetBuddy/.agentheim/salvage/ledger-saqsk-merge-conflict.patch

```
3250658 feature(ledger): Übertrags-Split-Zeilen werden jetzt über YNABs Subtransaction-Id mit ihrer Gegenbuchung verknüpft (neues Ereignis, Map auf dem Entry, Nachlauf im Spiegel-Lauf, Persistenz mit Replay, Historien-Satz). [ledger-h4nxz]

 src/Server/Ledger/Domain.fs      |  22 +++++++
 src/Server/Ledger/Persistence.fs |  44 ++++++++++++--
 src/Tests/LedgerPageTests.fs     |   1 +
 src/Tests/LedgerTests.fs         | 128 +++++++++++++++++++++++++++++++++++++++
 4 files changed, 191 insertions(+), 4 deletions(-)
```

## Verifier note (iteration 1)

**REASONS:**
- Residual conflict markers: none. Criteria coverage: every machine-checkable criterion maps to a named test in src/Tests/LedgerWithoutCategoryTests.fs (14 tests).
- Check 2 fails: the committed merge commit f097e99 does not compile. The `expected : Entry` literal in LedgerWithoutCategoryTests.fs:135 lacks `SplitTransferCounterparts` (added by ledger-h4nxz). The worker's fix (`SplitTransferCounterparts = Map.empty`) existed only uncommitted in the worktree. Cause: the conductor staged only the four conflicted files when committing the merge resolution, not the worker's full FILE_LIST. Not a worker defect.
- Sibling ledger-h4nxz intact in the diff.

**SUGGESTED_FIX:** Commit the pending change in LedgerWithoutCategoryTests.fs so HEAD builds, then re-run the suite against the committed state.

**ITERATION_HINT:** likely-fixable

## Outcome

Chip "Ohne Kategorie" nach ADR 0048 umgesetzt.

Server: `Entry.TransferAccountId` (roh aus `EntryObservedInYnab`), `splitTransferAccountIds` in `src/Server/Ledger/Domain.fs`. In `Persistence.fs`: Spalte `ledger_entries.transfer_account_id` (CREATE TABLE und `ALTER TABLE`), einmaliger Backfill aus dem Log (`backfillTransferAccountId`, nur wenn das ALTER neu griff, übersprungen ohne Ereignis-Log-Tabelle), Tabelle `ledger_entry_split_transfers`, `writeSplitIndexes` und `rebuildSplitIndexes` (umbenannt aus `...CategoryIndex`, Rückgabe zählt beide Tabellen), `EntryFilter.WithoutCategory` und die Klausel in `buildFilterClause` (OR in gemeinsamer Klammer mit `CategoryIds`). `WithoutCategory` an `LedgerEntryPageQuery`/`LedgerPayeeSuggestionQuery` (`src/Shared/Domain.fs`) und in `QueryHandlers.fs` durchgereicht.

Client: `WithoutCategoryChip` hinter `CategoryChip`, `Filters.WithoutCategory`, `ChipActivity.WithoutCategory`, `ToggleWithoutCategory`; kein Teil von `hasAnyFilter`, `ClearAllFilters` behält den Wert. Chip ohne Zähler und ohne Popover (wie `NeedsReviewChip`, der Zähler dort kommt aus `NeedsReviewCount`).

Hinweise: `NeedsReviewCount` ignoriert bewusst alle Such-Filter (ADR 0032), also auch den neuen Chip; der Test belegt das. Die Summenzeile erscheint beim Chip allein nicht (`hasAnyFilter` bleibt unverändert, wie beim Chip "Zu prüfen"). Tests in `src/Tests/LedgerWithoutCategoryTests.fs` (neue Datei, eigene Fixture-Ids 5a5a5a5a-*), bestehende Fixtures um die neuen Felder ergänzt, `rebuildSplitCategoryIndex` in `LedgerPageTests.fs` umbenannt. `dotnet test`: 1132 grün, 7 übersprungen (.env). `npm run guard:classes` grün. `npm run test:scripts`: die Browser-Tests (u. a. ledger-status-column) konnten hier nicht laufen (`data-app-ready` nie erreicht, kein Dev-Setup); `scripts/ledger-filter-bar.mjs` wurde nicht ausgeführt, kein Erfolg behauptet.

Merge-Auflösung (ledger-h4nxz): `Entry` trägt beide Felder (`TransferAccountId` und `SplitTransferCounterparts`), `migrateLedgerEntryColumns`, CREATE TABLE, `EntryRow`, `entryParams` und der Upsert führen beide Spalten (`transfer_account_id`, `split_transfer_counterparts`), alle Entry-Literale in den Tests setzen beide Felder; h4nxz' Änderungen sind unverändert erhalten. Nach dem Merge: `dotnet test` 1147 grün, 7 übersprungen; neue Tests dieses Tasks: 14.

README: Der Absatz "Migration bestehender Zustandstabellen" im ledger-README ist kein Bullet und per Delta nicht erreichbar; er nennt noch `rebuildSplitCategoryIndex` und sollte von Hand um die neue Spalte, die Tabelle `ledger_entry_split_transfers`, den Backfill und den neuen Namen `rebuildSplitIndexes` ergänzt werden. Der Glossar-Bullet im Delta enthält die Fakten.
