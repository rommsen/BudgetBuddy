# ledger — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `modeling` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 9
- **Todo:** 1
- **Doing:** 0
- **Done:** 104
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
- **ledger-z71ka** — "Bot-Commit: entry_id auch nach gescheitertem Push, und ein abgebrochener Commit ist wiederholbar, ohne doppelt zu buchen" (bug) — `todo/ledger-z71ka-bot-commit-entry-id-nach-push-fehler.md`
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- doing-list:end -->

### Done (current-month entries live; older months archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
- **ledger-k9tm2** — "Kassenbuch-Ereignisse nach `events` überführen: Kopie in rowid-Reihenfolge mit Abbruchprüfung, `appendAndApply` und Ladepfade umstellen, `ledger_entry_events` danach entfernen" (feature) — `done/ledger-k9tm2-kassenbuch-ereignisse-nach-events-berf-hren-kopie-in-rowid-r.md`
- **ledger-r6wn3** — "Spiegel verknüpft eine über die ImportId gefundene Buchung mit `EntryLinkedToYnab` statt über `linkEntry` ohne Ereignis; alte Verknüpfungen ohne Ereignis bekommen einmalig ihr Ereignis nachgetragen" (bug) — `done/ledger-r6wn3-linkentry-schreibt-ledger-entry-links-ohne-ereignis-gegen-ad.md`
- **ledger-cy1j8** — "Bug: rebuildSplitIndexes löscht und füllt die Split-Indextabellen ohne Transaktion, parallele initializeLedgerSchema-Aufrufe werfen einen UNIQUE-Fehler" (bug) — `done/ledger-cy1j8-rebuild-split-indexes-rennen.md`
- **ledger-q4m7z** — "Quick-Add: BB-Validierungsfehler nicht als \"YNAB hat abgelehnt\" melden" (bug) — `done/ledger-q4m7z-quick-add-bb-validierungsfehler-nicht-als-ynab-hat-abgelehnt.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **ledger-yfqxp** — "Ledger-README: Barabhebung nennt noch die Konto-Auswahl statt des Ziel-Pickers" (chore) — `backlog/ledger-yfqxp-ledger-readme-barabhebung-nennt-noch-die-konto-auswahl-statt.md`
- **ledger-x2aan** — "Bot-Drossel: globaler Zähler vor der Token-Prüfung erlaubt es Fremden, Bob auszusperren" (decision) — `backlog/ledger-x2aan-bot-drossel-vor-anmeldung.md`
- **ledger-mj3a9** — "BOT_MAX_AMOUNT_EUR: ungültiger Wert fällt auf das Maximum zurück statt auf einen sicheren Wert" (bug) — `backlog/ledger-mj3a9-bot-betragsdeckel-faellt-sicher.md`
- **ledger-vqvq6** — "Playwright-Wächter ledger-split-cashback wartet auf die Settings-Antwort statt fest 400 ms" (chore) — `backlog/ledger-vqvq6-split-cashback-waechter-ohne-festes-warten.md`
- **ledger-g47hp** — "Benachrichtigung für Lena bei neuer Rückfrage" (feature) — `backlog/ledger-g47hp-benachrichtigung-fuer-lena.md`
- **ledger-ayjzq** — "Sicht 'Für mich' für Lena in BB: alles, was ihr zugewiesen ist" (feature) — `backlog/ledger-ayjzq-sicht-fuer-mich.md`
- **ledger-hg5m5** — "Regel-Angebot nach einer Kategorie-Änderung im Kassenbuch: Anbieten, aus Payee und neuer Kategorie eine Kategorisierungsregel anzulegen" (feature) — `backlog/ledger-hg5m5-regel-angebot-nach-kategorie-aenderung.md`
- **ledger-nb4tv** — Geldmarkt-ETF als Konto anlegen und den monatlichen Abgleich klären (offenes v1-Kriterium) (decision) — `backlog/ledger-nb4tv-etf-konto-in-ynab-anlegen-und-abgleich-klaeren.md`
- **ledger-c7j8e** — Kursquelle für Geldmarkt-ETF per ISIN (Research-Spike) (spike) — `backlog/ledger-c7j8e-kursquelle-etf-isin-research-spike.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/ledger/INDEX.md`
