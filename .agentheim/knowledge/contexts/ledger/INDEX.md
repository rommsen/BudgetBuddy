## ADRs scoped to this BC

<!-- adr-local:start -->
- **0061**: Rückfrage zurücknehmen schreibt kein Flag nur bei Rückfrage Lena, ein fremdes Flag bleibt; die Lena-Farbe läuft über SettingsApi.getLenaFlagSettings und saveLenaFlagColor · 2026-10-25 · `../../decisions/0061-lena-zurueck-nehmen-nur-bei-rueckfrage-und-einstellung-ueber-settingsapi.md`
- **0060**: Tracking-Konto im Lesepfad der Split-Sperren aus einer fünf Minuten gemerkten YNAB-Kontenliste statt aus der Projektion; der Befehl prüft beim Speichern weiter live, dieselbe Liste speist die Übertrags-Ziele des Split-Editors · 2026-10-25 · `../../decisions/0060-tracking-sperre-aus-gemerkter-ynab-kontenliste.md`
- **0059** — Split-Schreibweg, Umsetzung: neues Ereignis EntryReplacedInYnab, abgelöste YNAB-Id als Verknüpfung ynabsuperseded, Fehlschlag "Neuanlage offen", Weg nach Baseline, Tracking-Prüfung per Konten-Abruf — 2026-10-24 — `../../decisions/0059-split-schreibweg-umsetzung-ersatz-ereignis-abgeloeste-id-und-retry-weg.md`
- **0058** — Split-Schreibweg nach YNAB (angenommen nach Probelauf): Aufteilen per PUT, bestehende Split-Buchungen werden gelöscht und mit abgeleiteter import_id neu angelegt, weil die API das Ändern von subtransactions ausschließt — 2026-10-24 — `../../decisions/0058-split-schreibweg-loeschen-und-neu-anlegen.md`
- **0056** — Lena zuweisen: Flag-Farbe aus den Settings, Schreibweg nach ADR 0050, Fehlschlag mit Baseline (Nachtrag) — 2026-10-24 — `../../decisions/0056-lena-zuweisen-flag-farbe-aus-settings-schreibweg-nach-adr-0050.md`
- **0055** — Kategorie-Schreibweg: Ereignis mit alt/neu, Fehlschlag mit Baseline, YNAB gewinnt, Available live — 2026-10-24 — `../../decisions/0055-kategorie-schreibweg-ereignis-fehlschlag-mit-baseline-ynab-gewinnt.md`
- **0054** — Kategorie ändern ist die erste v2-Fähigkeit, vor Abschluss von v1 — 2026-10-24 — `../../decisions/0054-kategorie-aendern-ist-die-erste-v2-faehigkeit-vor-abschluss-von-v1.md`
- **0053** — Bedienung der Kassenbuch-Zeile: Panel für eine Buchung, Leiste für viele, Zellen ohne eigene Klickziele, Gesten nach Eingabegerät — 2026-10-20 — `../../decisions/0053-bedienung-der-kassenbuch-zeile-panel-fuer-eine-leiste-fuer-viele.md`
- **0052** — Grenze zwischen PC-Tabelle und kompakter Liste folgt der gemessenen Tabellenbreite, die Filter-Chrome bleibt beim Viewport (bewusste Abweichung von ADR 0043) — 2026-10-20 — `../../decisions/0052-grenze-tabelle-liste-folgt-der-gemessenen-tabellenbreite.md`
- **0051** — Kassenbuch-Sortierung gehört zur Seitenanfrage und nicht zum EntryFilter, Betrag und Kategorie sortieren in F# — 2026-10-20 — `../../decisions/0051-kassenbuch-sortierung-teil-der-seitenanfrage-nicht-des-filters.md`
- **0048** — Chip "ohne Kategorie" fragt nach einem fehlenden Ziel, nicht nach einer fehlenden Kategorie; transfer_account_id wird roh in den Zustand projiziert — 2026-10-02 — `../../decisions/0048-ohne-kategorie-chip-ziel-statt-kategorie-transfer-projektion.md`
- **0046** — Split-Zeilen-Überträge werden über YNABs Subtransaction-Id verknüpft, nicht über den Buchungskopf — 2026-10-02 — `../../decisions/0046-split-zeilen-uebertraege-ueber-subtransaction-id-verknuepfen.md`
- **0045** — Split-Zeilen-Kategorie wird im Detail voll qualifiziert, über denselben Lookup wie die Kopf-Kategorie (ADR 0039 fortgeschrieben) — 2026-10-02 — `../../decisions/0045-split-zeilen-kategorie-qualifiziert-fortschreibung-0039.md`
- **0044** — Kategorie-Umbenennung ist keine Umkategorisierung, Spiegel vergleicht die Id, die Liste löst den Blattnamen live auf — 2026-10-02 — `../../decisions/0044-kategorie-umbenennung-ist-keine-umkategorisierung.md`
- **0043**: Die Filterleiste des Kassenbuchs wählt Popover (PC) oder BottomSheet (Handy) zur Renderzeit per `Viewport.isDesktopViewport()`, nicht per CSS-Doppel-Render; der Versatz des sticky Tabellenkopfs folgt der gemessenen Leistenhöhe über einen Callback-Ref an `ChipBar.bar` (2026-09-26), `../../decisions/0043-filterleiste-chrome-per-viewport-check-statt-css-doppel-render.md`
- **0042**: Payee-Vorschläge über eine gemeinsame F#-Faltung (`groupFoldedPayeeCounts`, `Core.searchFold`) gruppieren statt über `GROUP BY bb_lower(payee)`, weil SQLite die angezeigte Schreibweise einer nicht gruppierten Spalte offenlässt; häufigste Schreibweise gewinnt, bei Gleichstand die ordinal kleinste (2026-09-25), `../../decisions/0042-payee-vorschlaege-faltung-in-fsharp-statt-group-by-bb-lower.md`
- **0037** — Groß-/Kleinschreibung der Payee- und Memo-Filter über die anwendungsdefinierte SQL-Funktion `bb_lower` falten statt über `COLLATE NOCASE` (faltet nur ASCII, und `LIKE` ignoriert Kollationen); Lesepfad statt Normalform-Spalte, keine Diakritika-Entfernung — 2026-09-20 — `knowledge/decisions/0037-suchfilter-falten-ueber-anwendungsdefinierte-sql-funktion.md`
- **0036** — Kassenbuch-Suchfilter über eine gemeinsame `EntryFilter`/`buildFilterClause` für alle vier Zählwege plus Payee-Vorschläge; Betrag wird nie in SQL verglichen, sondern in F# nachgefiltert (ADR 0017), Zeitraum kind-unabhängig über `substr(date,1,10)` — 2026-09-19 — `knowledge/decisions/0036-kassenbuch-filter-lesepfad-betrag-in-fsharp.md`
- **0032** — Geprüft heißt approved und cleared; Flags werden roh beobachtet, "Lena" am Flag-Namen erkannt — 2026-09-14 — `../../decisions/0032-pruefvermerk-ist-approved-und-cleared-flags-roh-beobachtet.md`
- **0029** — Seitenweise Kassenbuch-Liste, Kontostände-Leiste und Konto-Filter (AllAccounts vs. OneAccount) — 2026-09-12 — `../../decisions/0029-seitenweise-kassenbuch-liste-und-kontostaende-leiste.md`
- **0028** — Der Prüfvermerk ist die einzige schreibende Fähigkeit, die vor v2 nach BB wandert — 2026-09-12 — `../../decisions/0028-pruefvermerk-als-einzige-schreibende-faehigkeit-vor-v2.md`
- **0025** — Order-ID-Abgleich als eigener Actor im Kassenbuch, kein eigenes Ereignis — 2026-09-11 — `../../decisions/0025-order-id-abgleich-als-eigener-actor-kein-eigenes-ereignis.md`
- **0021** — BB-geführte Buchungen entstehen zuerst im Kassenbuch, der YNAB-Push folgt; die Verknüpfung ist ein eigenes Ereignis — 2026-09-11 — `../../decisions/0021-kassenbuch-zuerst-dann-push-verknuepfung-als-ereignis.md`
- **0020** — Im Kassenbuch einen Elmish-Auslöser mit Entscheidungslogik in eine reine und eine unreine Nachricht teilen (Reduzierer unter .NET testbar) — 2026-09-06 — `../../decisions/0020-elmish-ausloeser-in-reine-und-unreine-nachricht-teilen.md`
- **0019** — Unkategorisierte Split-Zeile ist ein benanntes drittes Split-Ziel (ergänzt ADR 0006) — 2026-09-06 — `../../decisions/0019-unkategorisierte-split-zeile-als-drittes-split-ziel.md`
- **0017** — Beträge im Kassenbuch als exakter Dezimal-Text, Kontostand in F# summiert — 2026-09-06 — `../../decisions/0017-betraege-als-exakter-dezimaltext-im-kassenbuch.md`
- **0016** — Kontenmodell (Art x Verknüpfung), Bewertung als Buchung, Bar/Quick-Add und ETF-Konto in v1 — 2026-09-06 — `../../decisions/0016-kontenmodell-und-bewertung-als-buchung.md`
- **0015** — Buchungs-Identität: eigene Id im Kassenbuch, Fremd-Ids als Verknüpfungen, Spiegel matcht YNAB-Id vor ImportId — 2026-09-06 — `../../decisions/0015-buchungs-identitaet-eigene-id-fremd-id-verknuepfung.md`
- **0013** — Kassenbuch-Historie als append-only Ereignis-Log mit synchron geführtem Zustandsmodell — 2026-09-06 — `../../decisions/0013-append-only-event-log-with-synchronous-state.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- concepts:end -->


## Pointers

- BC README (ubiquitous language, invariants): `README.md`
- Task board (tasks by status) for this BC: `../../../board/ledger/INDEX.md`
