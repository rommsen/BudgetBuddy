---
id: categorization-cgq35
title: "Regelseite am Handy und per Tastatur"
status: doing
type: bug
context: categorization
created: 2026-10-30
completed:
depends_on: [design-system-001, categorization-msep5]
blocks: []
tags: [captured, bestandsreview]
related_adrs: [0005, 0043, 0053]
related_research: []
prior_art: []
---

## Why
Gruppe 2 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **hoch**. Trefferflächen unter 48 px, ein Menü, das nur über Fokus öffnet, ein zentriertes Modal am Handy und Texte, die dem Styleguide nicht folgen.

## What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Client/Components/Rules/View.fs:94` | standards/frontend/styleguide.md §4 (Touch-Targets mindestens 48 px) | hoch | Prioritätspfeile der Regelliste sind am Handy 24 mal 20 px groß und liegen neben dem Zeilen-Tap.
- `src/Client/Components/Rules/View.fs:569` | standards/frontend/styleguide.md §4 und §5 (Button, Icons) | mittel | Der Overflow-Knopf der Regelseite ist 32 px groß und hat keine Beschriftung.
- `src/Client/Components/Rules/View.fs:577 und :583` | standards/frontend/styleguide.md §5 (FilterPopover, BottomSheet, Button: jede klickbare Aktion), §7 | mittel | Das Menü der Regelseite öffnet nur über Fokus und seine Einträge sind nicht per Tastatur bedienbar.
- `src/Client/Components/Rules/View.fs:294` | standards/frontend/styleguide.md §5 (Modal nur Desktop, mobile Auswahl über BottomSheet) | mittel | Der Regel-Dialog läuft am Handy als zentriertes Modal mit Dropdown für die Kategorie.
- `src/Client/Components/Rules/View.fs:427` | standards/frontend/styleguide.md §8 (UI-Texte deutsch) | mittel | Englischer Platzhalter im Regel-Dialog.
- `src/Client/Components/Rules/View.fs:400` | styleguide §5 ErrorDisplay.warning | niedrig | selbst gebauter Warnkasten "Keine Kategorien geladen" durch `ErrorDisplay.warning` ersetzen.
- `src/Client/Components/Rules/View.fs:311` | styleguide §8 konsistente Begriffe | niedrig | "z.B." (311, 368 bis 370) zu "z. B.", "..." (393, 472, 648) zu "…".
- `src/Client/Components/Rules/View.fs:476` | styleguide §8 Buttons als Verb | niedrig | "Test" zu "Testen".
- `src/Client/Components/Rules/View.fs:648` | styleguide §6 Skeletons | niedrig | `NotAsked` und `Loading` zeigen dasselbe Skeleton.

## Acceptance criteria
Handy heißt 390 px Breite im Browser-Wächter (neu, `scripts/rules-page-mobile.test.mjs`, YNAB und `/api` gefälscht).

- [ ] Kaputt: GIVEN die Regelliste am Handy WHEN die Prioritätspfeile gemessen werden THEN waren sie 24 mal 20 px. Intakt: beide Pfeile sind mindestens 48 mal 48 px, ein Tipp verschiebt die Regel und öffnet den Dialog nicht. Der Test ist vor dem Fix rot.
- [ ] Kaputt: GIVEN die Regelseite WHEN der Overflow-Knopf per Tastatur erreicht wird THEN öffnet das Menü nur über Fokus und die Einträge reagieren nicht auf Enter. Intakt: Der Knopf ist ein `Button` mit Icon und `aria-label`, am Handy mindestens 48 px. Klick oder Enter öffnet das Menü, Esc und Klick außerhalb schließen es, jeder Eintrag (neu laden, exportieren, importieren) ist per Tab erreichbar und per Enter auslösbar.
- [ ] Das Menü liegt am PC in `FilterPopover` mit `menuItem`, am Handy in `BottomSheet`, sein Offen-Zustand im Model. Der Quelltext von `Components/Rules/View.fs` enthält kein `group-focus-within` mehr.
- [ ] Kaputt: GIVEN Handy-Breite WHEN der Regel-Dialog geöffnet wird THEN ist er ein zentriertes Modal mit Dropdown. Intakt: Handy `BottomSheet`, PC weiter `Modal`, gewählt über `Viewport.isDesktopViewport()`; die Kategorie kommt in beiden Hüllen aus `CategoryPicker` mit Suche. Anlegen und Ändern speichern dieselben Werte wie vorher.
- [ ] Die Texte folgen §8: "Override payee name in YNAB" ist deutsch, "z. B." statt "z.B.", "…" statt "...", Knopf "Testen". Ein Quelltext-Test sichert die vier Punkte.
- [ ] "Keine Kategorien geladen" läuft über `ErrorDisplay.warning`.
- [ ] `NotAsked` und `Loading` zeigen dasselbe Skeleton.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Captured via `quick-capture` on 2026-10-30. Refine am 2026-10-30: nicht geschnitten. Alle neun Befunde liegen in `Components/Rules/View.fs`, Teil-Tasks würden nur nacheinander in derselben Datei arbeiten.

- Pfeile: `moveButton` nutzt am Handy `TouchTargets.minSize`; ob die beiden neben- oder übereinander liegen, entscheidet der Worker nach dem Bild.
- Für den Overflow-Knopf gibt es in `Icons.fs` noch kein Icon, also dort ergänzen. Die Glyphe "⋮" fliegt raus.
- "Regeln importieren" ist heute ein `label` um ein verstecktes `input[file]` und per Tastatur nicht bedienbar. Es wird zu einem Menü-Eintrag, der das Input auslöst. Das Lesen der Datei in der View bleibt hier unangetastet, das verschiebt categorization-krscs (Befund kpsp8).
- Der Dialog im Sheet braucht `CategoryPicker.sheetLayered`, weil ein Sheet über dem Sheet liegt.
- categorization-msep5 läuft vorher (`depends_on`). categorization-krscs ändert denselben Client-Bereich: nicht im selben Batch laufen lassen, falls sie zugleich in todo liegt.
