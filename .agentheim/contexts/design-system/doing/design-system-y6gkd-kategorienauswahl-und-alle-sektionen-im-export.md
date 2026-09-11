---
id: design-system-y6gkd
title: Kategorienauswahl (BottomSheet.categoryPicker) im Styleguide und alle Styleguide-Sektionen im Claude-Design-Export
status: doing
type: feature
context: design-system
created: 2026-09-11
completed:
depends_on: [design-system-001]
blocks: [ledger-xbrht]
tags: [frontend, design-system, claude-design, export]
related_adrs: [0024, 0005]
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
- [ ] Die `/styleguide`-Route zeigt eine Sektion "Kategorienauswahl" mit dem echten
      `BottomSheet.categoryPicker`, Beispieldaten mit positiven, negativen und
      Null-Available-Beträgen, Vorschlägen und Zuletzt verwendet.
- [ ] Das Bundle enthält Karte(n) für die Kategorienauswahl in beiden Zuständen, deren
      Markup aus der gerenderten Route stammt.
- [ ] Jede weitere Sektion der Styleguide-Route ist im Bundle als Karte vertreten oder
      begründet ausgelassen (Liste im Diary).
- [ ] Jede `preview/*.html` beginnt in Zeile 1 mit `<!-- @dsCard group="…" -->`.
- [ ] Zwei Export-Läufe ergeben ein byte-identisches Bundle; keine Daten aus der Datenbank.
- [ ] Nur DS-Komponenten und Tokens in der neuen Sektion (Styleguide-Gate), keine
      Inline-Farben; `dotnet build`, `dotnet test`, `npm run build` grün.

## Notes
- Roman meinte mit "Detailansicht der Kategorien" die Gruppenliste mit Available im Picker
  (2026-09-11), keine eigene Komponente.
- Kann parallel zu `design-system-f7k2p` (Schriften selbst ausliefern) laufen; beide ändern
  `scripts/export-design-system.mjs`, aber an verschiedenen Stellen (Karten vs. Fonts).
- Nach diesem Task und `design-system-f7k2p` folgt ein Re-Sync nach dem README-Ablauf
  (Hauptsession mit Roman), zu Beginn von `ledger-xbrht`.
