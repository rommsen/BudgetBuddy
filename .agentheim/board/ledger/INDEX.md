# ledger — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `modeling` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 12
- **Todo:** 0
- **Doing:** 0
- **Done:** 76
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- doing-list:end -->

### Done (current-month entries live; older months archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
- **ledger-sfab0** — "Kassenbuch-Detail: Pfeil an der Kategorie öffnet den Picker, Änderung über setEntryCategory mit Karte Budget nach dem Wechsel und Toast mit Rückgängig, Vorschläge zum Payee, Hinweisleiste Kategorie nicht in YNAB mit Erneut senden, gesperrte Fälle, Zeile bleibt stehen (ausgegraut bei Filter-Nichttreffer, Hinweisleiste Neu laden bei Sortierschlüssel)" (feature) — `done/ledger-sfab0-kategorie-aendern-im-detail.md`
- **ledger-b7qmx** — "Split-Zeilen-Kategorie im Buchungs-Detail voll qualifizieren (ADR 0045)" (feature) — `done/ledger-b7qmx-split-zeilen-kategorie-in-der-pc-tabelle-voll-qualifizieren.md`
- **ledger-kwwxz** — "Available-Betrag je Kategorie im Kassenbuch frisch halten und fortschreiben: Kategorien neu laden bei Seitenaufruf, PWA-Vordergrund und Picker-Öffnung, lokale Fortschreibung nach einer Änderung, Zeilen der Karte Budget nach dem Wechsel mit alt und neu" (feature) — `done/ledger-kwwxz-available-betrag-je-kategorie-spiegeln.md`
- **ledger-w4vte** —  (chore) — `done/ledger-w4vte-auswahl-nach-adr-0053.md`
- **ledger-k2rbn** —  (chore) — `done/ledger-k2rbn-kategorie-aendern-ein-picker-zwei-einstiege.md`
- **ledger-h6mer** — "Split-Zeilen-Überträge: Bestandsfälle auf der Live-Datenbank nachverknüpfen" (chore) — `done/ledger-h6mer-split-zeilen-uebertraege-bestand-live-nachverknuepfen.md`
- **ledger-mas5g** — "Spike: Lässt die YNAB-API zu, die Split-Zeilen einer bestehenden Buchung zu setzen, zu ändern und aufzuheben, und mit welchem Aufruf" (spike) — `done/ledger-mas5g-spike-ynab-split-api.md`
- **ledger-q1hw2** — "Buchungs-Detail nach ADR 0053: ein Klick auf die Zeile markiert sie und öffnet das Detail (Panel rechts am PC, Sheet am Handy und in der Kompaktliste) mit Herkunft, Historie, Hinweisleiste, Geprüft-Schalter und Filter-Knöpfen; Zellmenü und altes Historie-Sheet entfallen" (feature) — `done/ledger-q1hw2-detail-herkunft-historie-noch-nicht-in-ynab.md`
- **ledger-skpcx** — "Kassenbuch sortieren am Handy: Sortier-Chip am Ende der Filterleiste öffnet ein BottomSheet mit allen Sortierungen" (feature) — `done/ledger-skpcx-sortier-chip-und-bottomsheet-am-handy.md`
- **ledger-bn913** — "Kassenbuch sortieren, PC und Client-Zustand: Klick auf einen Spaltenkopf sortiert serverseitig nach Datum, Betrag, Payee, Kategorie oder Konto; außer bei Datum fallen die Tagesköpfe weg" (feature) — `done/ledger-bn913-sortieren-ueber-die-spaltenkoepfe.md`
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
- **ledger-k9tq7** — Kassenbuch lädt fehlende Settings nach, statt beim Laden der Kategorien still aufzugeben (sonst bleibt der Kategorie-Picker leer bis zum Neuladen) (bug) — `backlog/ledger-k9tq7-kategorien-laden-holt-fehlende-settings-nach.md`
- **ledger-g47hp** — "Benachrichtigung für Lena bei neuer Rückfrage" (feature) — `backlog/ledger-g47hp-benachrichtigung-fuer-lena.md`
- **ledger-ayjzq** — "Sicht 'Für mich' für Lena in BB: alles, was ihr zugewiesen ist" (feature) — `backlog/ledger-ayjzq-sicht-fuer-mich.md`
- **ledger-y54gr** — "Lena fragen in der Auswahl-Leiste: eine Sammelaktion für viele Buchungen, ein Sammel-PATCH mit flag_color" (feature) — `backlog/ledger-y54gr-lena-fragen-in-der-auswahl-leiste.md`
- **ledger-hg5m5** — "Regel-Angebot nach einer Kategorie-Änderung im Kassenbuch: Anbieten, aus Payee und neuer Kategorie eine Kategorisierungsregel anzulegen" (feature) — `backlog/ledger-hg5m5-regel-angebot-nach-kategorie-aenderung.md`
- **ledger-pafkp** — "Split-Editor im Kassenbuch: Pfeil an der Kategorie einer Split-Buchung öffnet das Formular-Sheet zum Aufteilen, Ändern und Aufheben der Zeilen mit Rest-Anzeige und Speichern über setEntrySplits" (feature) — `backlog/ledger-pafkp-split-editor-im-kassenbuch.md`
- **ledger-kxme4** — "Split-Buchungen in BB bearbeiten, Schreibweg: Befehl setEntrySplits mit Prüfung Summe gleich Betrag, EntryRecategorized mit Splits, eigener Push nach YNAB, derselbe Fehlschlag-Zustand wie bei der Kategorie" (feature) — `backlog/ledger-kxme4-split-schreibweg.md`
- **ledger-w29y6** — "Kategorie setzen für die Auswahl: Sammel-Befehl mit einem Sammel-PATCH nach YNAB, Schaltfläche Kategorie setzen in der Auswahl-Leiste mit demselben Picker, Toast mit Zahl der geänderten und übersprungenen Buchungen" (feature) — `backlog/ledger-w29y6-kategorie-setzen-fuer-die-auswahl.md`
- **ledger-m6zpd** — "Kompaktliste nach Eingabegerät (ADR 0053 Punkt 6): mit Maus bekommt die schmale PC-Liste Kästchen, Umschalt-Klick, Prüf-Schaltfläche bei Hover, eine sichtbare Markierung und alle Tasten; mit Finger bleibt sie wie heute" (feature) — `backlog/ledger-m6zpd-kompaktliste-nach-eingabegeraet.md`
- **ledger-nb4tv** — Geldmarkt-ETF als Konto anlegen und den monatlichen Abgleich klären (offenes v1-Kriterium) (decision) — `backlog/ledger-nb4tv-etf-konto-in-ynab-anlegen-und-abgleich-klaeren.md`
- **ledger-xfjcp** — Buchung an Lena zuweisen statt nur ein Flag setzen; später Sicht "Für mich" und Benachrichtigung (feature) — `backlog/ledger-xfjcp-lena-zuweisen-statt-flag.md`
- **ledger-c7j8e** — Kursquelle für Geldmarkt-ETF per ISIN (Research-Spike) (spike) — `backlog/ledger-c7j8e-kursquelle-etf-isin-research-spike.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/ledger/INDEX.md`
