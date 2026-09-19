# ledger — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `modeling` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 14
- **Todo:** 1
- **Doing:** 1
- **Done:** 18
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
- **ledger-d4kx8** — "Gelöschte Buchungen: Entwurf zählt sie in der Trefferzahl, aber nicht in der Summe; gebaut zählen sie in beidem" (decision) — `todo/ledger-d4kx8-geloeschte-buchungen-in-trefferzahl-und-summe.md`
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **ledger-h9tq2** — "Reihenfolge der Historie entscheiden: Entwurf zeigt neueste zuerst, gebaut ist älteste zuerst" (decision) — `doing/ledger-h9tq2-reihenfolge-der-historie-entscheiden.md`
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **ledger-kdnnz** — "Spiegel erweitern: Prüfvermerk (approved und cleared) und Flags beobachten, Chip \"Zu prüfen\" und Kennzeichen \"Rückfrage Lena\" anzeigen" (feature) — `done/ledger-kdnnz-spiegel-pruefvermerk-und-flags-lesend.md`
- **ledger-h4p6c** — Spiegel-Auslöser: Frische-Kriterium beim Seitenaufruf und Lauf nach jedem Push (feature) — `done/ledger-h4p6c-spiegel-ausl-ser-frische-kriterium-beim-seitenaufruf-und-lau.md`
- **ledger-p3n8k** — Kassenbuch-Liste: gelöschte Buchungen standardmäßig ausblenden, eingeblendet klar als nicht mitzählend zeigen (feature) — `done/ledger-p3n8k-kassenbuch-liste-gel-schte-buchungen-standardm-ig-ausblenden.md`
- **ledger-x0yga** — "Kassenbuch-Liste an das Mockup angleichen: Zahlenformat, Betragsfarbe, Datum, Memo, Spaltenbreiten, Konten-Spalte" (bug) — `done/ledger-x0yga-kassenbuch-liste-an-das-mockup-angleichen.md`
- **ledger-vm3dk** — Testdaten aus der echten Entwicklungsdatenbank entfernen (Nachwirkung eines Test-Infra-Fehlers) (bug) — `done/ledger-vm3dk-testdaten-in-echter-dev-datenbank-aufraeumen.md`
- **ledger-b1xwe** — "Kassenbuch-Liste neu: seitenweise laden, Gesamtsicht über alle Konten, Kontostände-Leiste" (feature) — `done/ledger-b1xwe-liste-seitenweise-gesamtsicht-kontostaende.md`
- **ledger-s8m3w** — Bau-Scheiben aus dem Kassenbuch-UX-Konzept ableiten und über das Vorziehen schreibender Scheiben vor v2 entscheiden (decision) — `done/ledger-s8m3w-scheiben-aus-ux-konzept-ableiten-und-vorziehen-entscheiden.md`
- **ledger-xbrht** — UX-Konzept Kassenbuch als Arbeitsfläche (Handy und PC, mindestens YNAB-Niveau) (spike) — `done/ledger-xbrht-ux-konzept-kassenbuch-als-arbeitsflaeche.md`
- **ledger-qvt84** — "Import-Push schreibt zuerst die Kassenbuch-Buchung (Comdirect-Import + Review-Spur), dann YNAB, dann Verknüpfung; Push liefert YNAB-Ids und behebt drei Push-Bugs" (feature) — `done/ledger-qvt84-import-push-schreibt-ins-kassenbuch.md`
- **ledger-tdnpk** — "Quick Add: erst Kassenbuch (CashExpenseAdded), dann Push nach YNAB, dann Verknüpfung über die Id aus der Push-Antwort" (feature) — `done/ledger-tdnpk-quick-add-erst-kassenbuch-dann-ynab.md`
- **ledger-cscw1** — "Ereignis-Katalog: Entscheidungs-Ereignisse für Comdirect-Import, Regel, Korrektur, Quick Add und YNAB-Verknüpfung" (feature) — `done/ledger-cscw1-entscheidungs-ereignisse-im-katalog.md`
- **ledger-kt0wq** — Walking Skeleton Kassenbuch: ein YNAB-Konto spiegeln, Buchungen mit Herkunft und Historie anzeigen (spike) — `done/ledger-kt0wq-walking-skeleton.md`
- **ledger-q5w9c** — Kassenbuch-Seite: Buchungen eines Kontos mit Herkunfts-Badge und Historie, "Jetzt spiegeln" (feature) — `done/ledger-q5w9c-ledger-page.md`
- **ledger-m8r2v** — Spiegel-Lauf YNAB → Kassenbuch: Delta-Abruf, Snapshot-Vergleich, Ereignisse, manueller Auslöser (feature) — `done/ledger-m8r2v-mirror-run.md`
- **ledger-e3k7p** — Kassenbuch-Fundament: Ereignis-Log, Zustandsmodell, appendAndApply und Replay-Test (feature) — `done/ledger-e3k7p-event-log-and-state.md`
- **ledger-zm0zx** — Kontenmodell und Bewertung festlegen (Konto-Arten, BB-eigene Konten, Kursquelle) (decision) — `done/ledger-zm0zx-account-model-valuation.md`
- **ledger-z5vw6** — Identität einer Buchung über Systeme hinweg festlegen (eigene Id + Fremd-Id-Verknüpfung) (decision) — `done/ledger-z5vw6-entry-identity.md`
- **ledger-fsz0a** — Historie-Technik im Kassenbuch festlegen (Ereignis-Log + synchrones Zustandsmodell) (decision) — `done/ledger-fsz0a-history-mechanism.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **ledger-p9k3w** — "screenshot-route.mjs zeigt die inaktive BottomSheet-Leiste bei Full-Page-Aufnahmen mit scrollbarem Inhalt" (bug) — `backlog/ledger-p9k3w-screenshot-route-bottomsheet-artefakt.md`
- **ledger-qns4j** — Konten-Spalte beim Scrollen halten, und die Statusspalte mit Status statt Herkunft belegen (bug) — `backlog/ledger-qns4j-konten-spalte-haftend-und-statusspalte-neu-belegen.md`
- **ledger-w9skm** — Kassenbuch-Liste: Kategorie in Violett, und am Handy flache Zeilen statt Karten (bug) — `backlog/ledger-w9skm-kategorie-violett-und-flache-handy-zeilen.md`
- **ledger-nb4tv** — Geldmarkt-ETF als Konto anlegen und den monatlichen Abgleich klären (offenes v1-Kriterium) (decision) — `backlog/ledger-nb4tv-etf-konto-in-ynab-anlegen-und-abgleich-klaeren.md`
- **ledger-r777c** — Prüfvermerk in BB setzen und nach YNAB zurückschreiben (die eine schreibende Fähigkeit vor v2) (feature) — `backlog/ledger-r777c-pruefvermerk-setzen-und-nach-ynab-zurueckschreiben.md`
- **ledger-vgfw3** — Kassenbuch-Filter mit Summenzeile: Kategorie (Mehrfachauswahl), Zeitraum, Payee, Betrag, Memo (feature) — `backlog/ledger-vgfw3-filter-und-summenzeile.md`
- **ledger-r1d2f** — Auswahl-Modus mit Summe der Auswahl (Handy und PC) (feature) — `backlog/ledger-r1d2f-auswahl-modus-mit-summe.md`
- **ledger-q1hw2** — Buchungs-Detail: Herkunft, Historie und "noch nicht in YNAB" (Sheet am Handy, Seitenpanel am PC) (feature) — `backlog/ledger-q1hw2-detail-herkunft-historie-noch-nicht-in-ynab.md`
- **ledger-kwwxz** — Available-Betrag je Kategorie spiegeln und in der Kategorienauswahl zeigen (feature) — `backlog/ledger-kwwxz-available-betrag-je-kategorie-spiegeln.md`
- **ledger-xfjcp** — Buchung an Lena zuweisen statt nur ein Flag setzen; später Sicht "Für mich" und Benachrichtigung (feature) — `backlog/ledger-xfjcp-lena-zuweisen-statt-flag.md`
- **ledger-tpbe7** — Randfälle, in denen die Kassenbuch-Buchung nach dem Push von YNAB abweicht, und ungetesteter Klebecode im Import-Push (bug) — `backlog/ledger-tpbe7-randfaelle-kassenbuch-weicht-nach-push-von-ynab-ab.md`
- **ledger-fq3kd** — Force-Import (Duplikat erneut pushen) schreibt noch nicht ins Kassenbuch (feature) — `backlog/ledger-fq3kd-force-import-schreibt-noch-nicht-ins-kassenbuch.md`
- **ledger-t7v2q** — Übertrag: die beiden Buchungen miteinander verknüpfen (feature) — `backlog/ledger-t7v2q-bertrag-die-beiden-buchungen-miteinander-verkn-pfen.md`
- **ledger-c7j8e** — Kursquelle für Geldmarkt-ETF per ISIN (Research-Spike) (spike) — `backlog/ledger-c7j8e-kursquelle-etf-isin-research-spike.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/ledger/INDEX.md`
