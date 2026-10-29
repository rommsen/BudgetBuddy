# categorization — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 17
- **Todo:** 0
- **Doing:** 0
- **Done:** 5
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **categorization-wnxd6** — Regeln-Seite als ruhige Liste (schlanker Kopf, volle Breite, Tabelle statt Karten) (feature) — `done/categorization-wnxd6-regeln-seite-als-ruhige-liste.md`
- **categorization-gdrx8** — "Review-Spur: SyncTransaction hält Kategorisierungs-Schritte mit Zeitpunkt und Herkunft fest" (feature) — `done/categorization-gdrx8-review-spur-in-synctransaction.md`
- **ynab-k7m3q · Aktuellen Budgetwert (balance) hinter Kategorienamen im Picker anzeigen** — 2026-06-28 — — (Available pro Kategorie als farbcodierter Betrag im Import-/Split-/Quick-Add-Picker; ADR 0011; Verifier PASS iter 1) — `done/ynab-k7m3q-category-balance-in-picker.md`
- **cat-001 · Regel-Präzedenz per Hoch/Runter umsortierbar machen (UI)** — 2026-06-16 — — (9112013; ▲/▼-Reorder, Backend `reorderRules` war fertig) — `done/cat-001-rule-reorder-ui.md`
- **Mobile Category Picker: keyboard-aware + ghost-click-frei** — 2026-06-11 — `done/2026-06-11-mobile-category-picker-keyboard-ghostclick.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **categorization-m61mq** — "Eine deaktivierte Regel ist im Persistenz-Rundlauf nicht abgesichert" (chore) — `backlog/categorization-m61mq-eine-deaktivierte-regel-ist-im-persistenz-rundlauf.md`
- **categorization-h88qx** — "Ablehnung eines kaputten Regex beim Anlegen und Ändern einer Regel ist ungetestet" (chore) — `backlog/categorization-h88qx-ablehnung-eines-kaputten-regex-beim-anlegen-und.md`
- **categorization-e8e56** — "Payee- und Memo-Regeln haben keine Gegenprobe gegen das jeweils andere Feld" (chore) — `backlog/categorization-e8e56-payee-und-memo-regeln-haben-keine-gegenprobe.md`
- **categorization-jnyk8** — "Sammel-Kategorisieren, Aufteilen und Aufteilung entfernen haben keine API-Adapter-Tests" (chore) — `backlog/categorization-jnyk8-sammel-kategorisieren-aufteilen-und-aufteilung-entfernen-haben.md`
- **categorization-vzh1m** — "Manuelles Kategorisieren im Import hat keinen Test auf API-Adapter-Ebene" (chore) — `backlog/categorization-vzh1m-manuelles-kategorisieren-im-import-hat-keinen-test.md`
- **categorization-pk04j** — "Sammel-Kategorisierung überträgt die Kategorie nicht auf Buchungen mit derselben Order-Id" (bug) — `backlog/categorization-pk04j-sammel-kategorisierung-uebertraegt-die-kategorie-nicht-auf.md`
- **categorization-w32b3** — "Fehlermeldungen der Regeln und Splits erscheinen beim Nutzer auf Englisch" (refactor) — `backlog/categorization-w32b3-fehlermeldungen-der-regeln-und-splits-erscheinen-beim.md`
- **categorization-s2cdw** — "Kategorie-Ids in YnabTransaction und OrderIdSuggestion sind nackte Strings statt YnabCategoryId" (refactor) — `backlog/categorization-s2cdw-kategorie-ids-in-ynabtransaction-und-orderidsuggestion-sind.md`
- **categorization-tg9fw** — "Eine nicht lesbare Kategorie-Id aus der YNAB-Historie lässt den Order-Id-Abgleich und damit confirmTan abbrechen" (bug) — `backlog/categorization-tg9fw-eine-nicht-lesbare-kategorie-id-aus-der.md`
- **categorization-xn2sb** — "Regel testen meldet ein ungültiges Muster als kein Treffer, und der Treffer-Fall ist ungetestet" (bug) — `backlog/categorization-xn2sb-regel-testen-meldet-ein-ungueltiges-muster-als.md`
- **categorization-a510t** — "Bei gleicher Priorität ist offen, welche Regel gewinnt, und die Reihenfolge ist ungetestet" (bug) — `backlog/categorization-a510t-bei-gleicher-prioritaet-ist-offen-welche-regel.md`
- **categorization-b2576** — "Regel-Import läuft ohne Transaktion und ohne die Regel-Validierung" (bug) — `backlog/categorization-b2576-regel-import-laeuft-ohne-transaktion-und-ohne.md`
- **categorization-syy3j** — "Regeln mit unbekannter Kategorie werden mit dem Platzhalternamen Unknown gespeichert" (bug) — `backlog/categorization-syy3j-regeln-mit-unbekannter-kategorie-werden-mit-dem.md`
- **categorization-k4xp9** — "Unbekannte PatternType- und TargetField-Werte aus der Datenbank werden still zu Regex bzw. Combined" (refactor) — `backlog/categorization-k4xp9-unbekannte-patterntype-und-targetfield-werte-aus-der.md`
- **categorization-m856w** — "Prioritätspfeile der Regelliste sind am Handy 24 mal 20 px groß und liegen neben dem Zeilen-Tap" (bug) — `backlog/categorization-m856w-prioritaetspfeile-der-regelliste-sind-am-handy-24.md`
- **categorization-aemz9** — "Kategoriewechsel einer Regel speichert bei YNAB-Fehler den alten Kategorienamen" (bug) — `backlog/categorization-aemz9-kategoriewechsel-einer-regel-speichert-bei-ynab-fehler.md`
- **categorization-q3960** — "Ein gesetzter PayeeOverride einer Regel lässt sich nicht mehr entfernen" (bug) — `backlog/categorization-q3960-ein-gesetzter-payeeoverride-einer-regel-laesst-sich.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/categorization/INDEX.md`
