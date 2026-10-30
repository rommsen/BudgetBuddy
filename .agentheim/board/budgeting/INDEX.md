# Budgeting — Index (task board)

Catalog of this bounded context's tasks by status.

> Updated by: `modeling` (tasks), the lifecycle verbs (promote / claim / complete / capture / dismiss / bounce / reroute).
> Hand-edits are fine but the verbs will append at the section markers below.

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 3
- **Todo:** 0
- **Doing:** 1
- **Done:** 4
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **budgeting-afd8h** — "Geld verschieben am Handy: Tipp auf Verfügbar öffnet ein Sheet, Richtung nach Vorzeichen, Gegenseite Kategorie oder Zu verteilen, schreibt nach YNAB und hält Budget-Bewegungen fest" (feature) — `doing/budgeting-afd8h-geld-verschieben.md`
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **budgeting-rxxb4** — "Kategorie-Suche auf der Budget-Seite: Suchfeld über der Liste filtert Kategorien nach Namen, am Handy und am PC" (feature) — `done/budgeting-rxxb4-kategorie-suche-am-handy.md`
- **budgeting-h2zfn** — "Targets anwenden: Vorschau mit Summe und Zu verteilen danach, Übernehmen schreibt die Zuweisungen nach YNAB, Budget-Bewegungen werden in BB festgehalten, Fehlgeschlagene erneut sendbar" (feature) — `done/budgeting-h2zfn-targets-anwenden.md`
- **budgeting-k7wmd** — "Budget-Seite: Monat (dieser oder nächster), Zu verteilen und Kategorien nach Gruppen mit Zugewiesen, Aktivität und Verfügbar, live aus YNAB, am Handy und am PC" (feature) — `done/budgeting-k7wmd-budget-seite-live-aus-ynab.md`
- **budgeting-r4tkq** — "Spike: liefert die YNAB-API die Targets lesbar (Art, Betrag, Rhythmus) und erlaubt sie, den zugewiesenen Betrag pro Kategorie und Monat zu setzen? Voraussetzung für Targets anwenden." (spike) — `done/budgeting-r4tkq-ynab-api-targets-und-zuweisung.md`
<!-- done-list:end -->

Current-month entries stay live in full — the nominal ~30-entry figure
(`lib/index-rotation.mjs`'s `DEFAULT_CAP_ENTRIES`) is a steady-state target, not a hard cap, so a
busy month can legitimately hold well past 30 live entries (ADR-0039); rotation never splits a
month. Once a whole month closes and rotation rolls it out, this header is rewritten in place to
name the archive location (ADR-0047) and the block above holds only the still-live entries.
Rolled-out months land verbatim, newest-on-top, in `done-archive/YYYY-MM.md` — the same
cap-and-roll convention ADR-0039 established for `protocol.md` (agentic-workflow-r2c7m), applied
here to the INDEX done-list (agentic-workflow-c8j3w). Rotation never touches the actual
`done/<task-id>-<slug>.md` files or the `**Done:** N` lifetime count above, so `depends_on`/
`blocks` resolution and the dashboard search corpus (ADR-0023) are unaffected; `modeling`'s
prior-art matcher additionally checks `done-archive/` when present.

### Backlog
<!-- backlog-list:start -->
- **budgeting-m1bce** — "Kategorie-Detail: Ausgaben des Monats mit Sprung zur Buchung im Kassenbuch" (feature) — `backlog/budgeting-m1bce-kategorie-detail.md`
- **budgeting-dyn8g** — "Budget-Historie als Ansicht, allgemein und pro Kategorie, nach Art filterbar" (feature) — `backlog/budgeting-dyn8g-budget-historie-ansicht.md`
- **budgeting-bk6tq** — "Spike: Ausgaben am Monatswechsel. Wann kommen Buchungen bei Comdirect und ING an (vorgemerkt, gebucht, welches Datum), und was heißt das für Auffüllen auf einen festen Betrag am Monatsersten?" (spike) — `backlog/budgeting-bk6tq-vortrag-ab-dem-ersten-tag.md`
<!-- backlog-list:end -->

## Pointers

- Done-list archive (entries rolled out beyond the live cap, if any): `done-archive/YYYY-MM.md` (ADR-0039 convention, agentic-workflow-c8j3w)
- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/<bc-name>/INDEX.md`
