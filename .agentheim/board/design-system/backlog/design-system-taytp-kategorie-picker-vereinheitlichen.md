---
id: design-system-taytp
title: "Kategorie-Picker vereinheitlichen"
status: backlog
type: refactor
context: design-system
created: 2026-10-30
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Gruppe 4 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **mittel**. Import, Split und Quick-Add nutzen eine zweite, ältere Kategorieauswahl ohne Tastatur, Vorschläge und Fehlerzustand; Trefferflächen sind zu klein, und ADR 0011 widerspricht der heutigen Färbung.

## What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Client/DesignSystem/BottomSheet.fs:439 und :410` | standards/frontend/styleguide.md §2 "Auswahl" (Einfachauswahl über CategoryPicker.sheet/popover, Pfeile und Enter), §5 | mittel | BottomSheet.categoryPicker ist eine zweite Kategorieauswahl neben CategoryPicker, ohne Tastatur und ohne Ohne Kategorie.
- `src/Client/DesignSystem/BottomSheet.fs:110` | standards/frontend/styleguide.md §5 Icons (keine rohen svg inline) | mittel | Der Schließen-Knopf der Bottom Sheets ist ein eigenes Inline-SVG.
- `src/Client/DesignSystem/BottomSheet.fs:186` | standards/frontend/styleguide.md §4 (Touch-Targets mindestens 48 px) | mittel | Der Schließen-Knopf aller Bottom Sheets ist 32 px groß.
- `src/Client/DesignSystem/BottomSheet.fs:71` | standards/frontend/styleguide.md §4 (Touch-Targets) | mittel | Die Chips Vorgeschlagen und Zuletzt verwendet im Kategorie-Sheet sind etwa 33 px hoch.
- `src/Client/DesignSystem/BottomSheet.fs:586` | standards/frontend/styleguide.md §2 "Auswahl" (PickerParts.row mit truncate), Zugänglichkeit | mittel | Kontozeilen im Account-Picker sind nicht fokussierbar und kürzen lange Namen nicht.
- `src/Client/DesignSystem/BottomSheet.fs:36` | .agentheim/knowledge/decisions/0011-category-available-in-assignment-pickers.md Decision 3 gegen src/Client/DesignSystem/Money.fs:225 und standards/frontend/styleguide.md §2 | mittel | ADR 0011 nennt positive Available-Beträge grün, Code und Styleguide färben sie neutral.
- `src/Client/Components/SyncFlow/Views/TransactionList.fs:315` | standards/frontend/styleguide.md §2 "Auswahl", Abschnitt Vorgeschlagen | mittel | Der Kategorie-Picker im Import zeigt nie Vorschläge zum Payee.
- `src/Client/Components/SyncFlow/Views/TransactionList.fs:66` | CLAUDE.md MVU (alle Zustandsänderungen über update); .claude/rules/elmish-state.md | mittel | Welche Import-Buchung den Kategorie-Picker offen hat, liegt außerhalb des Models.
- `src/Client/Components/SyncFlow/State.fs:851` | .claude/rules/elmish-state.md, Failure anzeigen, RemoteData | mittel | Ein Fehler beim Laden der Kategorien im Import lässt alle Kategorie-Picker stumm leer.
- `src/Client/Views/QuickAddPage.fs:177` | standards/frontend/styleguide.md §4 (Touch-Targets), §5 (Icons) | mittel | Kategorie entfernen auf der Quick-Add-Seite ist 42 px breit und eine Textglyphe.
- `src/Client/Views/QuickAddPage.fs:83` | CLAUDE.md MVU; .claude/rules/semantic-messaging.md (Absicht statt technischer Setter) | mittel | Die Quick-Add-Seite baut den Formularzustand in der View und schickt ihn ungeprüft ins update.
- `src/Client/Views/QuickAddPage.fs:262` | elmish-state.md, Option statt Leerwert | niedrig | `CategoryId: YnabCategoryId option` statt "".
- `src/Client/Views/QuickAddPage.fs:256` | styleguide §7.1 Punkt 4 Layer-Klasse | niedrig | `categoryPickerLayered` stapelt auf der Seite über nichts, Kopfkommentar Zeile 15 veraltet.
- `src/Client/Views/QuickAddPage.fs:162` | Zugänglichkeit, Beschriftung | niedrig | Label "Kategorie" per `htmlFor`/`id` mit dem Auswahl-Knopf verknüpfen.
- `src/Client/DesignSystem/BottomSheet.fs:403` | styleguide §2 "Keine Treffer" | niedrig | leere Kategoriesuche zeigt "Keine Treffer".
- `src/Client/DesignSystem/BottomSheet.fs:597` | styleguide §2 "Keine Treffer" | niedrig | leerer Account-Picker als Hinweiszeile statt Abschnittskopf.
- `src/Client/DesignSystem/BottomSheet.fs:335` | Zugänglichkeit, Beschriftung | niedrig | `ariaLabel` "Kategorie suchen" (auch Zeile 566 für Konten).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-30, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn der Owner das will.
