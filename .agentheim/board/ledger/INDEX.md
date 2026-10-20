# ledger — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `modeling` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 11
- **Todo:** 0
- **Doing:** 1
- **Done:** 66
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **ledger-bn913** — "Kassenbuch sortieren, PC und Client-Zustand: Klick auf einen Spaltenkopf sortiert serverseitig nach Datum, Betrag, Payee, Kategorie oder Konto; außer bei Datum fallen die Tagesköpfe weg" (feature) — `doing/ledger-bn913-sortieren-ueber-die-spaltenkoepfe.md`
<!-- doing-list:end -->

### Done (current-month entries live; older months archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
- **ledger-rfq5p** — "\"Hier nach filtern\": Klick auf Payee oder Kategorie einer Zeile öffnet ein kleines Menü, das den passenden Such-Filter in einem Schritt setzt" (feature) — `done/ledger-rfq5p-hier-nach-filtern-aus-payee-und-kategorie.md`
- **ledger-mhe2s** — "Prüf-Schaltfläche der PC-Zeile erst bei Hover oder Fokus und neutral grau: der grüne Haken neben \"zu prüfen\" liest sich wie \"erledigt\"" (feature) — `done/ledger-mhe2s-pruef-schaltflaeche-erst-bei-hover.md`
- **ledger-r72re** — "Kassenbuch-Sortierung im Lesepfad: LedgerEntryPageQuery bekommt Sortierfeld und Richtung, der Server sortiert nach Datum, Betrag, Payee, Kategorie und Konto" (feature) — `done/ledger-r72re-kassenbuch-sortierung-im-lesepfad.md`
- **ledger-qh9qx** — "PC-Tabelle des Kassenbuchs bricht in schmalen Fenstern: Payee, Kategorie und Memo schrumpfen auf null, die Spaltenköpfe liegen übereinander" (bug) — `done/ledger-qh9qx-pc-tabelle-in-schmalen-fenstern.md`
- **ledger-a4acn** — "Historie liefert in Produktion bei jeder Buchung HTTP 500: ReadModel lädt CultureInfo de-DE, das es im Alpine-Container ohne ICU nicht gibt" (bug) — `done/ledger-a4acn-historie-500-ohne-kulturdaten-im-container.md`
- **ledger-nhwrt** — "Aktion \"Geprüft\" für die ganze Auswahl in der Auswahl-Leiste, ein Sammel-PATCH an YNAB, ein Toast mit Rückgängig" (feature) — `done/ledger-nhwrt-geprueft-fuer-die-ganze-auswahl.md`
- **ledger-rynk5** — "Gescheiterter Push nach YNAB als Ereignis mit Grund und Zeit, YNAB-Verknüpfung und Push-Fehlschlag im Lesemodell" (feature) — `done/ledger-rynk5-push-fehlschlag-als-ereignis.md`
- **ledger-rjb8w** — "Taste G und Tastaturfokus in der Kassenbuch-Liste (PC)" (feature) — `done/ledger-rjb8w-taste-g-und-tastaturfokus-in-der-liste.md`
- **ledger-r1d2f** — "Auswahl-Modus mit Summe der Auswahl (Handy und PC)" (feature) — `done/ledger-r1d2f-auswahl-modus-mit-summe.md`
- **ledger-th36h** — "Wisch nach rechts markiert eine Buchung als geprüft, Toast mit Rückgängig (Handy)" (feature) — `done/ledger-th36h-wisch-geprueft-mit-rueckgaengig-toast.md`
- **ledger-be1yh** — "Seitenversatz verrutscht, wenn bei aktivem Chip Zu prüfen eine Zeile die Liste verlässt" (bug) — `done/ledger-be1yh-seitenversatz-verrutscht-wenn-bei-aktivem-chip-zu-pr-fen-ein.md`
- **ledger-r777c** — "Prüfvermerk in BB setzen und nach YNAB zurückschreiben: Befehl, YNAB-PATCH, Zustand \"Prüfvermerk nicht in YNAB\" und eine Schaltfläche Geprüft/Ungeprüft an der Zeile" (feature) — `done/ledger-r777c-pruefvermerk-setzen-und-nach-ynab-zurueckschreiben.md`
- **ledger-zmbf1** — "Flaky Test: Replay-Test für SplitTransferCounterparts läuft in der reinen Domain-Liste ohne Schema-Initialisierung" (bug) — `done/ledger-zmbf1-flaky-test-replay-test-f-r-splittransfercounterparts-l-uft-i.md`
- **ledger-vqxvc** — "Build-Warnung FS0025: OpenFilterPopover deckt den neuen Chip WithoutCategoryChip nicht ab" (bug) — `done/ledger-vqxvc-build-warnung-fs0025-openfilterpopover-deckt-den-neuen-chip.md`
- **ledger-d218j** — Interne YNAB-Kategorien in der Kategorieauswahl ausblenden, Ready to Assign behalten (feature) — `done/ledger-d218j-interne-ynab-kategorien-in-der-kategorieauswahl-ausblenden.md`
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
- **ledger-k2rbn** — "Kategorie ändern in BB (v2): ein Kategorie-Picker mit zwei Einstiegen, dem Pfeil an der Kategorie im Detail für eine Buchung und \"Kategorie setzen\" in der Auswahl-Leiste für viele; BB schreibt die Änderung nach YNAB nach dem Muster des Prüfvermerks" (feature) — `backlog/ledger-k2rbn-kategorie-aendern-ein-picker-zwei-einstiege.md`
- **ledger-m6zpd** — "Kompaktliste nach Eingabegerät (ADR 0053 Punkt 6): mit Maus bekommt die schmale PC-Liste Kästchen, Umschalt-Klick, Prüf-Schaltfläche bei Hover, eine sichtbare Markierung und alle Tasten; mit Finger bleibt sie wie heute" (feature) — `backlog/ledger-m6zpd-kompaktliste-nach-eingabegeraet.md`
- **ledger-w4vte** — "Auswahl nach ADR 0053: Leertaste wählt die markierte Zeile, eine beginnende Auswahl schließt das Detail, Esc schließt Popover, dann Detail, dann Auswahl; während einer Auswahl ruhen Prüf-Schaltfläche, G und Wisch auf PC und Handy" (feature) — `backlog/ledger-w4vte-auswahl-nach-adr-0053.md`
- **ledger-skpcx** — "Kassenbuch sortieren am Handy: Sortier-Chip am Ende der Filterleiste öffnet ein BottomSheet mit allen Sortierungen" (feature) — `backlog/ledger-skpcx-sortier-chip-und-bottomsheet-am-handy.md`
- **ledger-h6mer** — "Split-Zeilen-Überträge: Bestandsfälle auf der Live-Datenbank nachverknüpfen" (chore) — `backlog/ledger-h6mer-split-zeilen-uebertraege-bestand-live-nachverknuepfen.md`
- **ledger-b7qmx** — "Split-Zeilen-Kategorie im Buchungs-Detail voll qualifizieren (ADR 0045)" (feature) — `backlog/ledger-b7qmx-split-zeilen-kategorie-in-der-pc-tabelle-voll-qualifizieren.md`
- **ledger-nb4tv** — Geldmarkt-ETF als Konto anlegen und den monatlichen Abgleich klären (offenes v1-Kriterium) (decision) — `backlog/ledger-nb4tv-etf-konto-in-ynab-anlegen-und-abgleich-klaeren.md`
- **ledger-q1hw2** — Buchungs-Detail: Herkunft, Historie und "noch nicht in YNAB" (Sheet am Handy, Seitenpanel am PC) (feature) — `backlog/ledger-q1hw2-detail-herkunft-historie-noch-nicht-in-ynab.md`
- **ledger-kwwxz** — Available-Betrag je Kategorie spiegeln und in der Kategorienauswahl zeigen (feature) — `backlog/ledger-kwwxz-available-betrag-je-kategorie-spiegeln.md`
- **ledger-xfjcp** — Buchung an Lena zuweisen statt nur ein Flag setzen; später Sicht "Für mich" und Benachrichtigung (feature) — `backlog/ledger-xfjcp-lena-zuweisen-statt-flag.md`
- **ledger-c7j8e** — Kursquelle für Geldmarkt-ETF per ISIN (Research-Spike) (spike) — `backlog/ledger-c7j8e-kursquelle-etf-isin-research-spike.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/ledger/INDEX.md`
