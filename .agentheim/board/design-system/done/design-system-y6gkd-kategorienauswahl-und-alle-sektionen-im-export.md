---
id: design-system-y6gkd
title: Kategorienauswahl (BottomSheet.categoryPicker) im Styleguide und alle Styleguide-Sektionen im Claude-Design-Export
status: done
type: feature
context: design-system
created: 2026-09-11
completed: 2026-09-11
depends_on: [design-system-001]
blocks: [ledger-xbrht]
tags: [frontend, design-system, claude-design, export]
related_adrs: [0024, 0005, 0027]
related_research: []
prior_art: [design-system-t82sg, design-system-h58y4, design-system-003]
---

## Why
Fund aus Romans Sichtprüfung beim Erst-Sync (`design-system-h58y4`, 2026-09-11): im
Claude-Design-Projekt fehlt die Kategorienauswahl. Sie ist eine Design-System-Komponente
(`BottomSheet.categoryPicker` / `categoryPickerLayered` in
`src/Client/DesignSystem/BottomSheet.fs`), wird aber in der `/styleguide`-Route nicht
gezeigt, und der Export kennt bisher nur Card, Money, Badge, Table, BottomSheet und
Navigation. "Kategorie ändern" ist einer von Romans Hauptabläufen im Kassenbuch
(`ledger-xbrht`, Ablauf 4); ohne die echte Kategorienauswahl im Design-System entstehen die
Mockups dafür frei erfunden.

Außerdem braucht das UX-Konzept mehr als sechs Komponenten (Entscheidung Roman,
2026-09-11): der Export soll alle Sektionen der Styleguide-Route enthalten.

## What
1. **Styleguide-Sektion "Kategorienauswahl"** in `src/Client/Components/Styleguide/View.fs`
   mit dem echten `BottomSheet.categoryPicker`, erfundenen Beispieldaten und beiden
   Zuständen, die Roman nutzt:
   - Einstieg mit "Vorgeschlagen" und "Zuletzt verwendet" (Chips).
   - Gruppenliste: Kategorien nach Gruppe, rechts der Available-Betrag farbig
     (positiv, negativ, null; `CategoryPickerOption.Available`), mit Suchfeld.
   Keine echten Kategorienamen aus Romans Budget, sondern plausible Beispiele.
2. **Export-Karte(n)** für die Kategorienauswahl: der Picker portalt wie das BottomSheet
   nach `document.body` und wird wie dieses über einen Trigger geöffnet und eingesammelt.
   Beide Zustände als eigene Karten oder als eine Karte mit beiden, Wahl beim
   Implementieren.
3. **Alle übrigen Styleguide-Sektionen exportieren:** jede Sektion der Route bekommt ein
   `data-ds-card` und landet als `preview/*.html` mit `@dsCard`-Marker im Bundle (Button,
   Input, Form, Modal, Toast, Stats, Loading, ErrorDisplay, PageHeader, Icons, Primitives,
   Swipe, Typografie, Voice, soweit vorhanden). Sinnvolle Gruppen, z. B. "Components",
   "Feedback", "Forms", "Patterns"; Sektionen, die nur Text sind (Voice), gehören eher in
   README/SKILL als in eine Karte, Entscheidung beim Implementieren.
4. Der Export bleibt idempotent (zwei Läufe byte-identisch) und ohne Backend.

## Acceptance criteria
- [x] Die `/styleguide`-Route zeigt eine Sektion "Kategorienauswahl" mit dem echten
      `BottomSheet.categoryPicker`, Beispieldaten mit positiven, negativen und
      Null-Available-Beträgen, Vorschlägen und Zuletzt verwendet.
- [x] Das Bundle enthält Karte(n) für die Kategorienauswahl in beiden Zuständen, deren
      Markup aus der gerenderten Route stammt.
- [x] Jede weitere Sektion der Styleguide-Route ist im Bundle als Karte vertreten oder
      begründet ausgelassen (Liste im Diary).
- [x] Jede `preview/*.html` beginnt in Zeile 1 mit `<!-- @dsCard group="…" -->`.
- [x] Zwei Export-Läufe ergeben ein byte-identisches Bundle; keine Daten aus der Datenbank.
- [x] Nur DS-Komponenten und Tokens in der neuen Sektion (Styleguide-Gate), keine
      Inline-Farben; `dotnet build`, `dotnet test`, `npm run build` grün.

## Notes
- Roman meinte mit "Detailansicht der Kategorien" die Gruppenliste mit Available im Picker
  (2026-09-11), keine eigene Komponente.
- Kann parallel zu `design-system-f7k2p` (Schriften selbst ausliefern) laufen; beide ändern
  `scripts/export-design-system.mjs`, aber an verschiedenen Stellen (Karten vs. Fonts).
- Nach diesem Task und `design-system-f7k2p` folgt ein Re-Sync nach dem README-Ablauf
  (Hauptsession mit Roman), zu Beginn von `ledger-xbrht`.
- Entscheidungen (eine Karte statt zwei für die Kategorienauswahl, Export-Umfang und
  Ausnahmen, sequentielles Öffnen/Schließen der Portal-Karten): ADR 0027
  (`.agentheim/knowledge/decisions/0027-kategorienauswahl-und-vollstaendiger-sektions-export.md`).

## Outcome
Kategorienauswahl-Sektion (`CategoryPickerDemo`, Sektion 19) in
`src/Client/Components/Styleguide/View.fs` ergänzt: echter
`BottomSheet.categoryPicker` mit fünf erfundenen Kategorien in zwei Gruppen
(Lebenshaltung/Fixkosten), Available positiv (128.40/850.00 EUR), negativ
(-32.10/-15.50 EUR) und null (0.00 EUR), zwei Vorschlägen und einer Zuletzt-verwendet-
Kategorie. Zusätzlich 12 weitere Sektionen mit `data-ds-card` markiert (Button, Stats,
Icons, PageHeader, Primitives, Swipe, Loading, ErrorDisplay, Input, Form, Toast, Modal).
`scripts/export-design-system.mjs` erweitert: `COMPONENT_CARDS` trägt jetzt eine
`group`-Spalte (Components/Feedback/Forms), neue Helferfunktion `extractPortalCard`
öffnet/erfasst/schließt Portal-Karten (BottomSheet, Kategorienauswahl, Modal)
nacheinander, Toast bekommt einen Vorab-Klick vor der normalen Erfassung. Bundle-
README/SKILL-Templates im Skript aktualisiert. BC-README: `data-ds-card`-Bullet
aktualisiert (Verweis statt veralteter Liste), neuer Abschnitt
"Styleguide-Sektionen im Export" mit vollständiger Liste und Ausnahmen. ADR 0027
dokumentiert die drei Entscheidungen. Details, Testzahlen und die vollständige
Liste der exportierten/ausgelassenen Sektionen: Diary-Eintrag unten.

## Diary entry

## 2026-09-11 21:50 - Kategorienauswahl im Styleguide, vollständiger Sektions-Export im Claude-Design-Bundle

**What I did:**
`BottomSheet.categoryPicker` als neue Styleguide-Sektion "Kategorienauswahl" ergänzt
(erfundene Beispieldaten, Available positiv/negativ/null, Vorschläge + Zuletzt
verwendet, Suchfeld). Zusätzlich 12 weitere Styleguide-Sektionen mit `data-ds-card`
markiert und in den Export aufgenommen. Export-Skript um eine Portal-Karten-Helfer-
funktion und Gruppen-Metadaten erweitert. Kein Zugriff auf `src/Client/styles.css` oder
Font-Code (Sperrbereich des parallelen Tasks `design-system-f7k2p`).

**Files Added:**
- `.agentheim/knowledge/decisions/0027-kategorienauswahl-und-vollstaendiger-sektions-export.md` -
  ADR: eine Karte für beide Picker-Zustände, Export-Umfang und -Ausnahmen,
  sequentielles Öffnen/Schließen der Portal-Karten.

**Files Modified:**
- `src/Client/Components/Styleguide/View.fs` - `open Shared.Domain` ergänzt; neue
  Sektion 19 "Kategorienauswahl (Kategorie ändern)" (`CategoryPickerDemo`); `data-ds-card`
  auf Button-, Stats-, Loading-, ErrorDisplay-, Icons-, PageHeader-, Primitives-,
  Input-, Form-, Swipe-Sektion ergänzt; Modal- und Toast-Trigger (`data-ds-card-trigger`)
  ergänzt, Toast-Renderliste in einen `data-ds-card`-Wrapper gehüllt; Swipe/Navigation/
  Voice-Sektionsnummern um eins verschoben (19→20/21/22).
- `scripts/export-design-system.mjs` - `COMPONENT_CARDS` um 10 neue Einträge samt
  `group`-Feld erweitert (Components/Feedback/Forms); neue Konstanten
  `CATEGORY_PICKER_CARD`, `MODAL_CARD`; neue Helferfunktion `extractPortalCard`
  (öffnen, Bereitschafts-Selektor abwarten, Markup klonen, schreiben, schließen,
  Verschwinden der `.active`-Klasse abwarten) für BottomSheet/Kategorienauswahl/Modal;
  Toast bekommt einen Vorab-Klick vor der Schleife über `COMPONENT_CARDS`; README_MD-
  Template ("## Komponenten") auf die vollständige Liste aktualisiert.
- `.agentheim/contexts/design-system/README.md` - `data-ds-card`-Bullet im
  Export-Bundle-Abschnitt aktualisiert (Verweis auf ADR 0027 statt veralteter
  Sechs-Komponenten-Liste); neuer Abschnitt "Styleguide-Sektionen im Export
  (design-system-y6gkd, 2026-09-11)" vor "Open questions".

**Files Deleted:**
- keine.

**Rationale:**
Roman fand beim Erst-Sync die Kategorienauswahl im Design-System-Projekt vermisst
(Ablauf 4 "Kategorie ändern" im Kassenbuch, `ledger-xbrht`, braucht den echten Picker
als Vorbild statt eines frei erfundenen Mockups) und entschied, dass das UX-Konzept
mehr als sechs Komponenten braucht.

**Exportierte Sektionen (19 Komponenten-Karten, Gruppe "Components" sofern nicht
anders vermerkt):** Card, Money, Badge, Table, Navigation, Button, Stats, Icons,
PageHeader, Primitives, Swipe, BottomSheet, Kategorienauswahl, Modal; Gruppe
"Feedback": Loading, ErrorDisplay, Toast; Gruppe "Forms": Input, Form.

**Bewusst ausgelassen (mit Begründung):**
- Farbsemantik-Sektion: bereits über die generierten Token-Karten
  `color-neon`/`color-surfaces` abgedeckt (dieselben Werte, kein zweites Set).
- Typografie-Sektion: bereits über die generierte Token-Karte `type-fonts` abgedeckt
  (dieselben Werte, kein zweites Set).
- Voice-Sektion: reiner Fließtext ohne visuell prüfbares Markup; steht im
  Bundle-README/SKILL statt in einer Karte (wie im Task vorgeschlagen).

**Outcomes:**
- Build: dotnet build (0 Fehler), Fable/`npm run build` (grün, ein bereits bestehender
  eval-Hinweis in `Rules/State.fs`, nicht Teil dieses Tasks).
- Tests: `dotnet test` 778 bestanden, 7 übersprungen (Integrationstests brauchen `.env`,
  unverändert gegenüber vor diesem Task). Kein neuer dotnet-Test: reine Feliz-View-
  und Node-Export-Änderung ohne testbare pure Funktion (TDD_SKIPPED, wie schon bei
  `design-system-t82sg`).

**Iteration 2 (2026-09-11, nach Merge mit feature/kassenbuch):**
Der Konduktor hat `feature/kassenbuch` in dieses Worktree gemergt; alles bis auf
`scripts/export-design-system.mjs` ging automatisch durch. Konflikt gelöst: eigene
Logik (`extractPortalCard`, Karten-Gruppen über `card.group`, BottomSheet/
Kategorienauswahl/Modal-Erfassung) behalten, dabei das `fontsUrl`-Parameter
durchgängig entfernt (`writePreview`, `extractPortalCard`, `extractPreviews` und alle
Aufrufstellen), passend zur Font-Selbstauslieferung aus `design-system-f7k2p`/ADR 0026.
Keine Konfliktmarker mehr (`grep` leer), `node --check` grün. Zusätzlich die
Verifier-Funde aus Iteration 1 behoben: BC-README-Bullet zu den `@dsCard`-Gruppen
(jetzt Colors/Type/Components/Feedback/Forms/Brand statt der alten Sechs-Komponenten-
Liste), "18" auf "19" Komponenten-Karten korrigiert (README-Abschnitt und Diary oben),
Kategorienauswahl in der "Neu"-Liste ergänzt.

Verifikation: `dotnet build` 0 Fehler; `dotnet test` 784 bestanden/7 übersprungen (neue
Baseline nach f7k2p, +6 Tests); `npm run build` grün (derselbe bestehende eval-Hinweis);
`npm run export:design-system` zweimal, `diff -r` zwischen den Läufen leer
(byte-identisch); Bundle enthält `fonts/` mit den drei woff2-Dateien plus
`fonts/LICENSE.md`; keine `fonts.googleapis.com`-Referenz im Bundle; alle 23
`preview/*.html` beginnen in Zeile 1 mit `<!-- @dsCard group="…" -->`; headless
Screenshot von `components-categorypicker.html` (Playwright, `file://`, cwd =
Worktree-Root) zeigt Vorschläge, Zuletzt verwendet, beide Gruppen, Suchfeld und alle
drei Available-Vorzeichen korrekt gerendert, Schriften (Inter/JetBrains Mono) laden
lokal ohne Netzzugriff.
- Export: `npm run export:design-system` zweimal ausgeführt, `diff -r` zwischen beiden
  Läufen leer (byte-identisch). Alle 23 `preview/*.html` beginnen in Zeile 1 mit
  `<!-- @dsCard group="…" -->` (verifiziert per Skript). Kategorienauswahl-Karte zeigt
  Vorschläge, Zuletzt verwendet, beide Gruppen und alle drei Available-Vorzeichen
  (128.40/-32.10/0.00/850.00/-15.50 EUR). Headless Screenshot-Stichprobe (Playwright,
  Viewport statt Full-Page wegen eines bekannten Full-Page-Screenshot-Artefakts bei
  `position: fixed`-Inhalten) bestätigt korrektes Rendering für Kategorienauswahl,
  Modal, Toast (Eintritts-Animation braucht in einer statischen Datei ohne
  Nutzerinteraktion messbar länger als die deklarierten 0.42s, bis opacity 1 erreicht;
  Endzustand ist aber korrekt und das exportierte Markup selbst unabhängig davon
  richtig) und Button.
- Issues: keine offenen. Die Toast-Eintritts-Animation in der statischen Vorschau ist
  eine kosmetische Beobachtung (kein Blocker, kein AC betroffen), nicht weiter verfolgt.

---

## Verifier note (iteration 1)

**REASONS:**
- Check 5 (BC README sync): `.agentheim/contexts/design-system/README.md` lines 111-113 (the `@dsCard`-Marker bullet in "Export-Bundle für Claude Design") still say the bundle's groups are "Colors", "Type", "Components" (Card/Money/Badge/Table/BottomSheet/Navigation) and "Brand". This task changed exactly that. The bundle now also has "Feedback" and "Forms" groups, and "Components" holds 14 cards. The worker updated the `data-ds-card` bullet right above it but left this one stale, so it now contradicts the new section further down. (Conductor note: the worker had been told not to touch that bullet to avoid a merge conflict with design-system-f7k2p; that restriction is lifted now.)
- Check 5: the same README, lines 179-180 in "Styleguide-Sektionen im Export", says "sind das 18 Komponenten-Karten". The actual count is 19: 12 new cards listed there, plus the 6 that already existed, plus Kategorienauswahl, which the "Neu" list leaves out. The generated bundle confirms it: 19 `components-*.html` files out of 23 previews in total. The task file's diary (line 137, "18 Komponenten-Karten") has the same wrong count, although the names it lists add up to 19.
- Checked and fine: `dotnet build` 0 errors, `dotnet test` 778 passed / 7 skipped, `npm run build` green; export twice byte-identical; all 23 `preview/*.html` start with `<!-- @dsCard group="…" -->`; `components-categorypicker.html` holds the rendered picker (suggestions, recently used, both groups, search field, Available green/red/muted); DS components only, invented sample categories; TDD skip legitimate; ADR 0027 well-formed; no dashes or transliterations in added lines; scope clean.

**SUGGESTED_FIX:** In the BC README, rewrite the `@dsCard`-Marker bullet (lines 111-113) to list the current groups: Colors, Type, Components, Feedback, Forms, Brand. Correct "18" to "19" in the new section (and add Kategorienauswahl to its "Neu" list) and in the task-file diary line.

**ITERATION_HINT:** likely-fixable
