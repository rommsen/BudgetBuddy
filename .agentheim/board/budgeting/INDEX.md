# Budgeting — Index (task board)

Catalog of this bounded context's tasks by status.

> Updated by: `modeling` (tasks), the lifecycle verbs (promote / claim / complete / capture / dismiss / bounce / reroute).
> Hand-edits are fine but the verbs will append at the section markers below.

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 6
- **Todo:** 0
- **Doing:** 1
- **Done:** 17
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **budgeting-x4k7p** — "Review-Korrekturen zum Tab Historie und zum stillen Neu-Lesen: Historie lädt still nach, Stand-Badges nach Styleguide, Monatsbalken als DS-Baustein, fehlende Verdrahtungstests" (refactor) — `doing/budgeting-x4k7p-review-korrekturen-zum-tab-historie-und-zum-stillen-neu-lese.md`
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **budgeting-dyn8g** — "Tab Historie je Kategorie: Aufbau von Verfügbar, Bewegungen des Monats und Ausgaben der letzten 6 Monate aus dem Kassenbuch" (feature) — `done/budgeting-dyn8g-budget-historie-ansicht.md`
- **budgeting-r7t3k** — "Budget-Seite: Rückkehr aus dem Kassenbuch und Schließen von Targets ohne Sprung" (bug) — `done/budgeting-r7t3k-budget-seite-r-ckkehr-aus-dem-kassenbuch-und-schlie-en-von-t.md`
- **budgeting-c5w2n** — "Tab Geld: Umschalter Zuweisen, Holen, Abgeben, rechnendes Feld mit Speichern beim Verlassen, Fehlbetrag-Knopf, Handy-Schritte, Holen und Abgeben nach Variante D" (feature) — `done/budgeting-c5w2n-tab-geld-zuweisen-und-verschieben.md`
- **budgeting-h3k9d** — "Budget-Seite dichter: Kopf in einer Zeile, volle Breite, einzeilige Zeilen, Tastatur seitenweit, Filter Überzogen, Kategorie-Pfeile im Sheet" (feature) — `done/budgeting-h3k9d-budget-seite-dichter-und-tastatur.md`
- **budgeting-p4n6r** — "Review-Korrekturen zur neuen Budget-Seite: Monatswechsel ohne Sprung, Pink raus, Trefferflächen, sticky Filter, Testlücken" (bug) — `done/budgeting-p4n6r-review-korrekturen-budget-seite.md`
- **budgeting-g8s3w** — "Budget-Seite neu: Liste und Inspektor mit vier Tabs, freie Monate, Kategorie-Reihenfolge, Filter und Gelb für fehlt" (feature) — `done/budgeting-g8s3w-grundgeruest-liste-und-inspektor.md`
- **budgeting-v4ht9** — "ADRs zur Budget-Seite: Targets über BB, Pause nur in BB, freie Monate, Farbrollen mit Gelb und neutralen Knöpfen" (decision) — `done/budgeting-v4ht9-entscheidungen-budget-seite.md`
- **budgeting-q7m2c** — "Spike: Liefert YNAB die Anzeigereihenfolge der Kategorien, wie weit reichen Monate, und erhält eine Target-Änderung Tag und Rhythmus?" (spike) — `done/budgeting-q7m2c-spike-ynab-reihenfolge-monate-target-schreiben.md`
- **budgeting-kjsht** — "Targets sichtbar: Budget-Seite und Vorschau zeigen je Kategorie den Target-Betrag mit Rhythmus, die Budget-Seite auch den Fehlbetrag" (feature) — `done/budgeting-kjsht-targets-sichtbar.md`
- **budgeting-et7z8** — "Budget-Seite: dritter Knopf Letzter Monat, Verschieben geht dort, Targets anwenden nicht" (feature) — `done/budgeting-et7z8-letzter-monat.md`
- **budgeting-m1bce** — "Kategorie-Detail: Tipp auf den Namen zeigt Zugewiesen, Aktivität, Verfügbar und die Buchungen des Monats aus dem Kassenbuch; Tipp auf eine Buchung öffnet sie im Kassenbuch" (feature) — `done/budgeting-m1bce-kategorie-detail.md`
- **budgeting-nfrds** — "Verschieben absichern: Erneut senden hält die Reihenfolge ein, das Sheet rechnet mit dem aktuellen Stand, Monat und Eingaben werden an der API-Grenze festgelegt" (bug) — `done/budgeting-nfrds-verschieben-absichern.md`
- **budgeting-afd8h** — "Geld verschieben am Handy: Tipp auf Verfügbar öffnet ein Sheet, Richtung nach Vorzeichen, Gegenseite Kategorie oder Zu verteilen, schreibt nach YNAB und hält Budget-Bewegungen fest" (feature) — `done/budgeting-afd8h-geld-verschieben.md`
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
- **budgeting-v6m3z** — "Tab Historie: Farbe für Monate mit negativen Ausgaben und Teal-Betrag neben dem Stand entscheiden" (decision) — `backlog/budgeting-v6m3z-tab-historie-farbe-f-r-monate-mit-negativen-ausgaben-und-tea.md`
- **budgeting-h7q4v** — "Allgemeine Budget-Historie #/budget/history: alle Bewegungen, neueste zuerst, Filter nach Art, Targets-Durchlauf aufklappbar, Knopf Historie im Seitenkopf" (feature) — `backlog/budgeting-h7q4v-allgemeine-historie-ansicht.md`
- **budgeting-w2f6c** — "Budget-Seite: Listenbeträge gegen ADR 0049" (bug) — `backlog/budgeting-w2f6c-budget-seite-listenbetr-ge-gegen-adr-0049.md`
- **budgeting-m3x7f** — "Monatserster: Leiste mit Targets prüfen, Targets anwenden, rot decken und verteilen, Vorschau mit abwählbaren Zeilen" (feature) — `backlog/budgeting-m3x7f-monatserster-leiste.md`
- **budgeting-t6p8z** — "Tab Target: Targets in BB anlegen, ändern und entfernen, je Monat pausieren und die Frage Passt das Target noch? mit gespeichertem So lassen" (feature) — `backlog/budgeting-t6p8z-tab-target-aendern-pausieren-pruefen.md`
- **budgeting-bk6tq** — "Spike: Ausgaben am Monatswechsel. Wann kommen Buchungen bei Comdirect und ING an (vorgemerkt, gebucht, welches Datum), und was heißt das für Auffüllen auf einen festen Betrag am Monatsersten?" (spike) — `backlog/budgeting-bk6tq-vortrag-ab-dem-ersten-tag.md`
<!-- backlog-list:end -->

## Pointers

- Done-list archive (entries rolled out beyond the live cap, if any): `done-archive/YYYY-MM.md` (ADR-0039 convention, agentic-workflow-c8j3w)
- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/<bc-name>/INDEX.md`
