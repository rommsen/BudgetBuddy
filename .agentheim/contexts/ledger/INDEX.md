# ledger — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `modeling` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 7
- **Todo:** 0
- **Doing:** 1
- **Done:** 11
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **ledger-s8m3w** — Bau-Scheiben aus dem Kassenbuch-UX-Konzept ableiten und über das Vorziehen schreibender Scheiben vor v2 entscheiden (decision) — `doing/ledger-s8m3w-scheiben-aus-ux-konzept-ableiten-und-vorziehen-entscheiden.md`
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
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
- **ledger-xfjcp** — Buchung an Lena zuweisen statt nur ein Flag setzen; später Sicht "Für mich" und Benachrichtigung (feature) — `backlog/ledger-xfjcp-lena-zuweisen-statt-flag.md`
- **ledger-tpbe7** — Randfälle, in denen die Kassenbuch-Buchung nach dem Push von YNAB abweicht, und ungetesteter Klebecode im Import-Push (bug) — `backlog/ledger-tpbe7-randfaelle-kassenbuch-weicht-nach-push-von-ynab-ab.md`
- **ledger-fq3kd** — Force-Import (Duplikat erneut pushen) schreibt noch nicht ins Kassenbuch (feature) — `backlog/ledger-fq3kd-force-import-schreibt-noch-nicht-ins-kassenbuch.md`
- **ledger-p3n8k** — Kassenbuch-Liste: gelöschte Buchungen standardmäßig ausblenden, eingeblendet klar als nicht mitzählend zeigen (feature) — `backlog/ledger-p3n8k-kassenbuch-liste-gel-schte-buchungen-standardm-ig-ausblenden.md`
- **ledger-h4p6c** — Spiegel-Auslöser: Frische-Kriterium beim Seitenaufruf und Lauf nach jedem Push (feature) — `backlog/ledger-h4p6c-spiegel-ausl-ser-frische-kriterium-beim-seitenaufruf-und-lau.md`
- **ledger-t7v2q** — Übertrag: die beiden Buchungen miteinander verknüpfen (feature) — `backlog/ledger-t7v2q-bertrag-die-beiden-buchungen-miteinander-verkn-pfen.md`
- **ledger-c7j8e** — Kursquelle für Geldmarkt-ETF per ISIN (Research-Spike) (spike) — `backlog/ledger-c7j8e-kursquelle-etf-isin-research-spike.md`
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
- **0025** — Order-ID-Abgleich als eigener Actor im Kassenbuch, kein eigenes Ereignis — 2026-09-11 — `knowledge/decisions/0025-order-id-abgleich-als-eigener-actor-kein-eigenes-ereignis.md`
- **0021** — BB-geführte Buchungen entstehen zuerst im Kassenbuch, der YNAB-Push folgt; die Verknüpfung ist ein eigenes Ereignis — 2026-09-11 — `knowledge/decisions/0021-kassenbuch-zuerst-dann-push-verknuepfung-als-ereignis.md`
- **0020** — Im Kassenbuch einen Elmish-Auslöser mit Entscheidungslogik in eine reine und eine unreine Nachricht teilen (Reduzierer unter .NET testbar) — 2026-09-06 — `knowledge/decisions/0020-elmish-ausloeser-in-reine-und-unreine-nachricht-teilen.md`
- **0019** — Unkategorisierte Split-Zeile ist ein benanntes drittes Split-Ziel (ergänzt ADR 0006) — 2026-09-06 — `knowledge/decisions/0019-unkategorisierte-split-zeile-als-drittes-split-ziel.md`
- **0017** — Beträge im Kassenbuch als exakter Dezimal-Text, Kontostand in F# summiert — 2026-09-06 — `knowledge/decisions/0017-betraege-als-exakter-dezimaltext-im-kassenbuch.md`
- **0016** — Kontenmodell (Art x Verknüpfung), Bewertung als Buchung, Bar/Quick-Add und ETF-Konto in v1 — 2026-09-06 — `knowledge/decisions/0016-kontenmodell-und-bewertung-als-buchung.md`
- **0015** — Buchungs-Identität: eigene Id im Kassenbuch, Fremd-Ids als Verknüpfungen, Spiegel matcht YNAB-Id vor ImportId — 2026-09-06 — `knowledge/decisions/0015-buchungs-identitaet-eigene-id-fremd-id-verknuepfung.md`
- **0013** — Kassenbuch-Historie als append-only Ereignis-Log mit synchron geführtem Zustandsmodell — 2026-09-06 — `knowledge/decisions/0013-append-only-event-log-with-synchronous-state.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
