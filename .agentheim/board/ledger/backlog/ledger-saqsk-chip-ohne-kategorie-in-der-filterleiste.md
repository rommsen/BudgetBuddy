---
id: ledger-saqsk
title: "Chip \"ohne Kategorie\" in der Kassenbuch-Filterleiste: Buchungen ohne Kategorie und Splits ohne eine einzige Kategorie finden"
status: backlog
type: feature
context: ledger
created: 2026-09-25
completed:
depends_on: [ledger-j8xq4, ledger-xk9wq]
blocks: []
tags: [backend, frontend, kassenbuch, filter]
related_adrs: [0019, 0036, 0038]
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

## What
Ein eigener Chip "ohne Kategorie" neben der Kategorienauswahl. Er ist ein eigenes
Filterkriterium im Lesepfad (`LedgerEntryPageQuery`, `Ledger.Persistence.EntryFilter`,
`buildFilterClause`, ADR 0036), das über alle vier Zählwege und die Payee-Vorschläge
gleich wirkt. Kandidat für die Klausel, sobald `ledger-xk9wq` die Indextabelle
`ledger_entry_split_categories` liefert: `category_id IS NULL AND id NOT IN (SELECT
entry_id FROM ledger_entry_split_categories)`.

**Produktfrage, bei der Verfeinerung zu entscheiden:** Überträge tragen in YNAB keine
Kategorie (`category_id` leer), sind aber nicht "unkategorisiert" im Sinne von "Roman muss
noch etwas tun". Ob der Chip sie ausnimmt (über die Übertrags-Verknüpfung `TransferEntryId`
oder `transfer_account_id` im Zustand) oder mit zeigt, ist mit Roman zu klären. Dasselbe für
in YNAB gelöschte Buchungen (folgen vermutlich dem `IncludeDeleted`-Schalter).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Abgetrennt am 2026-09-25 bei der Verfeinerung von `ledger-j8xq4`. Erst bearbeitbar, wenn
die Filterleiste steht (der Chip braucht einen Platz) und die Split-Indextabelle da ist
(sonst müsste der Chip Splits als JSON-Text prüfen, was ADR 0013 Punkt 8 verbietet).
