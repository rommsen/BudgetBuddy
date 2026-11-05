# Budgeting — Index (task board)

Catalog of this bounded context's tasks by status.

> Updated by: `modeling` (tasks), the lifecycle verbs (promote / claim / complete / capture / dismiss / bounce / reroute).
> Hand-edits are fine but the verbs will append at the section markers below.

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 13
- **Todo:** 0
- **Doing:** 1
- **Done:** 42
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **budgeting-v7kfw** — "Fehlbetrag als reine Funktion je Target-Art, parallel zu YNABs goal_under_funded gerechnet, mit Abweichungsliste, ohne Verhaltensänderung" (feature) — `doing/budgeting-v7kfw-fehlbetrag-als-reine-funktion-je-target-art-parallel-zu-ynab.md`
<!-- doing-list:end -->

### Done (current-month entries live; older months archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
- **budgeting-zkbbd** — "BB führt das Budget, YNAB wird Spiegel: Zuweisen, Verschieben und Targets entstehen als Ereignisse in BB, nach YNAB geht nur Zugewiesen je Monat und Kategorie" (decision) — `done/budgeting-zkbbd-bb-fuehrt-das-budget.md`
- **budgeting-gv74q** — "Targets prüfen nachbessern: Testlücken aus dem QA-Review schließen, TargetChangeMark als DU statt Bool, Prüfung ohne Lesen der Pausen" (refactor) — `done/budgeting-gv74q-targets-pruefen-nachbessern.md`
- **budgeting-p8nd0** — "Target-Formular aufräumen: Wrapper, die nur Tests aufrufen, streichen und die Tab-Tests auf Kern und Sperrlage umstellen" (refactor) — `done/budgeting-p8nd0-target-formular-aufraeumen.md`
- **budgeting-p2zr6** — "Targets prüfen, Regel und Daten: reine Einordnungsfunktion in Shared.Budgeting und Endpoint getTargetCheck mit einer Kassenbuch-Abfrage für alle Kategorien und der letzten Target-Änderung je Kategorie" (feature) — `done/budgeting-p2zr6-passt-das-target-noch.md`
- **budgeting-r7k2m** — "Target-Formular aus dem Tab Target herauslösen, damit die Seite Targets prüfen es im Panel nutzen kann, ohne Verhaltensänderung im Tab" (refactor) — `done/budgeting-r7k2m-target-formular-herausloesen.md`
- **budgeting-m4v8t** — "pauseNeedsReread: im erreichbaren Zustand gedeckte Bedingungen streichen oder benennen" (chore) — `done/budgeting-m4v8t-pauseneedsreread-im-erreichbaren-zustand-gedeckte-bedingunge.md`
- **budgeting-n7h3x** — "Laufende Pause oder laufendes Target-Speichern einer anderen Kategorie sperrt den gleichen Knopf ohne sichtbaren Grund" (bug) — `done/budgeting-n7h3x-laufende-pause-oder-laufendes-target-speichern-einer-anderen.md`
- **budgeting-f8w2n** — "Target und Pause sperren sich nur je Kategorie mit sichtbarem Grund, Meldung ohne Target nach Schritt 1, Testlücken aus den Reviews zu c2t9w" (bug) — `done/budgeting-f8w2n-sperren-je-kategorie-und-testluecken.md`
- **budgeting-d7r4k** — "Target-Datum je Rhythmus: kein Datum bei der Woche, beim Monat und Jahr als \"fällig am\"" (feature) — `done/budgeting-d7r4k-target-datum-je-rhythmus.md`
- **budgeting-r7h2q** — "[layout]-Test Historie-Knopf 48 px am Handy rundet nicht: 47,99999 px ließ ihn einmal rot werden" (chore) — `done/budgeting-r7h2q-layout-test-historie-knopf-48-px-am-handy-rundet-nicht-47-99.md`
- **budgeting-c2t9w** — "Target-Formular: Tab-Wechsel schließt Rückfragen, Entfernen-Bestätigung schließt nach Neulesen, genaue Meldungen beim Teilergebnis und beim Anlegen außerhalb des Monats" (bug) — `done/budgeting-c2t9w-target-formular-rueckfragen-und-meldungen.md`
- **budgeting-a3k7m** — "Budget-Kopf bei 1024 px in einer Zeile, Historie am Handy ohne abgeschnittene Kategorien und Beträge" (bug) — `done/budgeting-a3k7m-kopf-1024-und-historie-handy.md`
- **budgeting-b5n8q** — "BB-eigene Ablehnungen nicht als \"YNAB hat abgelehnt\", Targets-Durchlauf mit n von n Fehlschlägen als gescheitert" (bug) — `done/budgeting-b5n8q-bb-meldungen-und-lauf-gescheitert.md`
- **budgeting-e6p4r** — "Benannte Regressionstests: Pause-Antwort während eines Monatswechsels, Zurück aus der allgemeinen Historie" (chore) — `done/budgeting-e6p4r-regressionstests-pause-und-historie.md`
- **budgeting-f4k9n** — "Muss-Befunde zu budgeting-g2ty9: ReturnFocus nimmt beim Tippen den Fokus, Walk der Rückfragen ohne getippten Betrag" (bug) — `done/budgeting-f4k9n-fokus-rueckgabe-und-walk.md`
- **budgeting-g2ty9** — "Review-Korrekturen im Target-Formular: Spinner am auslösenden Knopf, Fokus nach Abbrechen und Speichern bleibt, Datumsfeld nur lesend wie der Betrag, dazu die Befunde der Reviews zu Target anlegen" (refactor) — `done/budgeting-g2ty9-review-korrekturen-target-formular.md`
- **budgeting-s5n2w** — "Spike: Lässt sich über die YNAB-API ein einmaliges Sparziel bis zu einem Datum anlegen und ändern?" (spike) — `done/budgeting-s5n2w-spike-sparziel-bis-datum.md`
- **budgeting-c6vn4** — "Review-Korrekturen zur allgemeinen Historie und zu den Pause-Korrekturen: Suchfeld am Handy wieder breit, Ladeantwort mit Generation, Filter haftet, aufklappbare Zeile mit Chevron, Betrag rechts, BB-Lesefehler nicht als YNAB-Fehler, fehlende Tests" (refactor) — `done/budgeting-c6vn4-review-korrekturen-allgemeine-historie-und-pause.md`
- **budgeting-xp46a** — "Target anlegen und Art wechseln im Tab Target: NEED monatlich, wöchentlich oder jährlich, optional mit Datum in zwei Aufrufen, Warnung vor dem Verlust von Tag und Datum" (feature) — `done/budgeting-xp46a-target-anlegen-art-wechseln.md`
- **budgeting-h7q4v** — "Allgemeine Budget-Historie #/budget/history: alle Bewegungen, neueste zuerst, Filter nach Art, Targets-Durchlauf aufklappbar, Knopf Historie im Seitenkopf" (feature) — `done/budgeting-h7q4v-allgemeine-historie-ansicht.md`
- **budgeting-r3wq8** — "Review-Korrekturen zu Target pausieren, Tab Historie und Target ändern: Eingabe beim Speichern bleibt, Pause-Aufrufe lesen bei Abbruch neu, ein Name je Pause-Ereignis, Absicht statt Bool, Fokus und Rollen der Balken, fehlende Tests" (refactor) — `done/budgeting-r3wq8-review-korrekturen-pause-historie-target-aendern.md`
- **budgeting-k8wq3** — "Target ändern und entfernen im Tab Target: Betrag und Datum, nur geänderte Felder nach YNAB, Abgleich mit dem gezeigten Stand, konkrete Meldung bei Ablehnung, Aufzeichnung als Target-Änderung in der Historie" (feature) — `done/budgeting-k8wq3-target-aendern-entfernen.md`
- **budgeting-v6m3z** — "Tab Historie ruhig und ablesbar: Monat mit Erstattungsüberschuss neutral mit Plus, Bewegungsbeträge neutral mit Vorzeichen, Wertzeile über den Ausgaben-Balken bei Maus, Tipp und Tastatur" (feature) — `done/budgeting-v6m3z-tab-historie-farbe-f-r-monate-mit-negativen-ausgaben-und-tea.md`
- **budgeting-t6p8z** — "Target pausieren: Pause je Kategorie und Monat in BB, wirkt in Fehlt, Frei, Vorschau und Targets anwenden, endet mit dem Monat, Knopf im Tab Geld" (feature) — `done/budgeting-t6p8z-tab-target-aendern-pausieren-pruefen.md`
- **budgeting-x4k7p** — "Review-Korrekturen zum Tab Historie und zum stillen Neu-Lesen: Historie lädt still nach, Stand-Badges nach Styleguide, Monatsbalken als DS-Baustein, fehlende Verdrahtungstests" (refactor) — `done/budgeting-x4k7p-review-korrekturen-zum-tab-historie-und-zum-stillen-neu-lese.md`
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
- **budgeting-e2z7v** — "Rückweg-Aktion \"Targets nach YNAB zurückschreiben\": BBs Targets bewusst als Goals nach YNAB schreiben, soweit darstellbar, und listen, was YNAB nicht darstellen kann" (feature) — `backlog/budgeting-e2z7v-r-ckweg-aktion-targets-nach-ynab-zur-ckschreiben-bbs-targets.md`
- **budgeting-eh8xe** — "Targets als BB-Regeln: einmalige Übernahme aus YNAB 1:1, Fehlbetrag aus BB, Rückbau des Goal-Schreibwegs und der YNAB-Pause, Wochentag in BB setzbar" (feature) — `backlog/budgeting-eh8xe-targets-als-bb-regeln-einmalige-bernahme-aus-ynab-1-1-fehlbe.md`
- **budgeting-dgrp7** — "Budget aus YNAB übernehmen (sofort und im Hintergrund), Soll je Monat und Kategorie aus BB falten, YNAB-Änderungen über observeMonth mit Echo-Regel erkennen" (feature) — `backlog/budgeting-dgrp7-budget-aus-ynab-bernehmen-sofort-und-im-hintergrund-soll-je.md`
- **budgeting-qm5x9** — "Befehls-Id für alle Budget-Entscheidungen aus der UI: optionales CommandId-Feld, Prüfung gegen processed_commands unter der Sperre, Antwort aus dem Stream, Client wiederholt einmal" (feature) — `backlog/budgeting-qm5x9-befehls-id-f-r-alle-budget-entscheidungen-aus-der-ui-optiona.md`
- **budgeting-hyn77** — "Budget-Ereignistypen mit Codec und Startmigration der fünf Budget-Tabellen nach events, Schreibwege und Leser umgestellt, Verhalten unverändert" (feature) — `backlog/budgeting-hyn77-budget-ereignistypen-mit-codec-und-startmigration-der-f-nf-b.md`
- **budgeting-q4v8n** — "Seite Targets prüfen #/budget/target-check: Knopf im Kopf der Budget-Seite, Gruppen mit Balken, Vorschlag übernehmen und Anpassen … im Panel oder BottomSheet" (feature) — `backlog/budgeting-q4v8n-seite-targets-pruefen.md`
- **budgeting-w5k2r** — "Pause verschwindet, wenn beim Tipp ein Lesen des Monats schon läuft (Wechsel hin und zurück)" (bug) — `backlog/budgeting-w5k2r-pause-verschwindet-wenn-beim-tipp-ein-lesen-des-monats-schon.md`
- **budgeting-h9c3w** — "Gesammelte Sollte- und Kann-Befunde der Reviews zu budgeting-c6vn4 und budgeting-g2ty9" (chore) — `backlog/budgeting-h9c3w-review-befunde-sammlung.md`
- **budgeting-q8m2v** — "Allgemeine Historie begrenzen oder nach Monat laden" (feature) — `backlog/budgeting-q8m2v-allgemeine-historie-begrenzen-oder-nach-monat-laden.md`
- **budgeting-w3p7n** — "Allgemeine Historie: Pausen und Target-Änderungen als eigene Arten?" (feature) — `backlog/budgeting-w3p7n-allgemeine-historie-pausen-und-target-nderungen-als-eigene-a.md`
- **budgeting-w2f6c** — "Budget-Seite: Listenbeträge gegen ADR 0049" (bug) — `backlog/budgeting-w2f6c-budget-seite-listenbetr-ge-gegen-adr-0049.md`
- **budgeting-m3x7f** — "Monatserster: Leiste mit Targets prüfen, Targets anwenden, rot decken und verteilen, Vorschau mit abwählbaren Zeilen" (feature) — `backlog/budgeting-m3x7f-monatserster-leiste.md`
- **budgeting-bk6tq** — "Spike: Ausgaben am Monatswechsel. Wann kommen Buchungen bei Comdirect und ING an (vorgemerkt, gebucht, welches Datum), und was heißt das für Auffüllen auf einen festen Betrag am Monatsersten?" (spike) — `backlog/budgeting-bk6tq-vortrag-ab-dem-ersten-tag.md`
<!-- backlog-list:end -->

## Pointers

- Done-list archive (entries rolled out beyond the live cap, if any): `done-archive/YYYY-MM.md` (ADR-0039 convention, agentic-workflow-c8j3w)
- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/<bc-name>/INDEX.md`
