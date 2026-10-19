# ledger — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `modeling` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 11
- **Todo:** 1
- **Doing:** 3
- **Done:** 51
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
- **ledger-r777c** — "Prüfvermerk in BB setzen und nach YNAB zurückschreiben: Befehl, YNAB-PATCH, Zustand \"Prüfvermerk nicht in YNAB\" und eine Schaltfläche Geprüft/Ungeprüft an der Zeile" (feature) — `todo/ledger-r777c-pruefvermerk-setzen-und-nach-ynab-zurueckschreiben.md`
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **ledger-zmbf1** — "Flaky Test: Replay-Test für SplitTransferCounterparts läuft in der reinen Domain-Liste ohne Schema-Initialisierung" (bug) — `doing/ledger-zmbf1-flaky-test-replay-test-f-r-splittransfercounterparts-l-uft-i.md`
- **ledger-d218j** — Interne YNAB-Kategorien in der Kategorieauswahl ausblenden, Ready to Assign behalten (feature) — `doing/ledger-d218j-interne-ynab-kategorien-in-der-kategorieauswahl-ausblenden.md`
- **ledger-vqxvc** — "Build-Warnung FS0025: OpenFilterPopover deckt den neuen Chip WithoutCategoryChip nicht ab" (bug) — `doing/ledger-vqxvc-build-warnung-fs0025-openfilterpopover-deckt-den-neuen-chip.md`
<!-- doing-list:end -->

### Done (current-month entries live; older months archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
- **ledger-m23nz** — Kassenbuch-Filterleiste und Summenzeile nach ADR 0049 nachziehen (feature) — `done/ledger-m23nz-kassenbuch-filterleiste-und-summenzeile-ruhig.md`
- **ledger-n4vte** — Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste) (feature) — `done/ledger-n4vte-kassenbuch-auf-ruhige-listen-umstellen.md`
- **ledger-saqsk** — "Chip \"ohne Kategorie\" in der Kassenbuch-Filterleiste: Buchungen ohne Kategorie und Splits ohne eine einzige Kategorie finden" (feature) — `done/ledger-saqsk-chip-ohne-kategorie-in-der-filterleiste.md`
- **ledger-h4nxz** — "Split-Zeilen-Überträge: Gegenbuchung über YNABs Subtransaction-Id verknüpfen" (feature) — `done/ledger-h4nxz-split-zeilen-bertr-ge-transfer-transaction-id-auf-subtransac.md`
- **ledger-fgt4k** — "DeletedCount widerspricht den zurückgegebenen Buchungen, wenn OnlyNeedsReview und IncludeDeleted zusammen aktiv sind" (bug) — `done/ledger-fgt4k-deletedcount-widerspricht-den-zur-ckgegebenen-buchungen-wenn.md`
- **ledger-ggzdw** — "Spiegel wertet eine Kategorie-Umbenennung in YNAB als Umkategorisierung jeder Buchung: observe vergleicht CategoryRef samt Name statt nur die Id" (bug) — `done/ledger-ggzdw-spiegel-kategorie-vergleich-nur-ueber-id.md`
- **ledger-fgw2q** — "Nicht auswertbare YNAB-Antwort beim Comdirect-Push lässt die Session ohne Verknüpfung abschließen" (bug) — `done/ledger-fgw2q-nicht-auswertbare-ynab-antwort-beim-comdirect-push-l-sst-die.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **ledger-nhwrt** — "Aktion \"Geprüft\" für die ganze Auswahl in der Auswahl-Leiste" (feature) — `backlog/ledger-nhwrt-geprueft-fuer-die-ganze-auswahl.md`
- **ledger-rjb8w** — "Taste G und Tastaturfokus in der Kassenbuch-Liste (PC)" (feature) — `backlog/ledger-rjb8w-taste-g-und-tastaturfokus-in-der-liste.md`
- **ledger-th36h** — "Wisch nach rechts markiert eine Buchung als geprüft, Toast mit Rückgängig (Handy)" (feature) — `backlog/ledger-th36h-wisch-geprueft-mit-rueckgaengig-toast.md`
- **ledger-h6mer** — "Split-Zeilen-Überträge: Bestandsfälle auf der Live-Datenbank nachverknüpfen" (chore) — `backlog/ledger-h6mer-split-zeilen-uebertraege-bestand-live-nachverknuepfen.md`
- **ledger-b7qmx** — "Split-Zeilen-Kategorie im Buchungs-Detail voll qualifizieren (ADR 0045)" (feature) — `backlog/ledger-b7qmx-split-zeilen-kategorie-in-der-pc-tabelle-voll-qualifizieren.md`
- **ledger-nb4tv** — Geldmarkt-ETF als Konto anlegen und den monatlichen Abgleich klären (offenes v1-Kriterium) (decision) — `backlog/ledger-nb4tv-etf-konto-in-ynab-anlegen-und-abgleich-klaeren.md`
- **ledger-r1d2f** — Auswahl-Modus mit Summe der Auswahl (Handy und PC) (feature) — `backlog/ledger-r1d2f-auswahl-modus-mit-summe.md`
- **ledger-q1hw2** — Buchungs-Detail: Herkunft, Historie und "noch nicht in YNAB" (Sheet am Handy, Seitenpanel am PC) (feature) — `backlog/ledger-q1hw2-detail-herkunft-historie-noch-nicht-in-ynab.md`
- **ledger-kwwxz** — Available-Betrag je Kategorie spiegeln und in der Kategorienauswahl zeigen (feature) — `backlog/ledger-kwwxz-available-betrag-je-kategorie-spiegeln.md`
- **ledger-xfjcp** — Buchung an Lena zuweisen statt nur ein Flag setzen; später Sicht "Für mich" und Benachrichtigung (feature) — `backlog/ledger-xfjcp-lena-zuweisen-statt-flag.md`
- **ledger-c7j8e** — Kursquelle für Geldmarkt-ETF per ISIN (Research-Spike) (spike) — `backlog/ledger-c7j8e-kursquelle-etf-isin-research-spike.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/ledger/INDEX.md`
