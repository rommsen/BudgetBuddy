---
id: design-system-001
title: Styleguide retroaktiv kodifizieren (das Gate)
status: done
type: chore
context: design-system
created: 2026-06-13
completed: 2026-06-13
commit: c611e98
depends_on: []
blocks: [ynab-002]
tags: [styleguide, design-system, tokens, components, patterns, documentation, gate, frontend]
related_adrs: [0004, 0005]
related_research: []
prior_art: []
---

## Why
Es existiert ein vollständiges Design System im Code (`src/Client/DesignSystem/` — 20
Komponenten + `Tokens.fs`), aber **kein Styleguide** als reviewbares Artefakt. Die
visuelle Sprache (neon-on-dark, Farbsemantik), das Komponenten-Inventar, die projektweiten
Muster (Sheets/Click-Commit, ADR 0005; Picker, ADR 0004), Motion und Voice leben nur
implizit im Code und verstreut in `CLAUDE.md`. Damit fehlt die Single Source of Truth, an
der UI-Entscheidungen gemessen werden — und das **Gate**, das künftige UI-Arbeit
konsistent hält. Roman will das retroaktiv nachziehen.

## What
Das bestehende Design System in **ein** reviewbares Dokument
`standards/frontend/styleguide.md` ziehen. Reine Kodifizierung — kein Erfinden neuer
Sprache, sondern Extrahieren und Strukturieren dessen, was schon da ist
(`Tokens.fs`, die 20 Komponenten, ADR 0004/0005, der CLAUDE.md-Komponenten-Abschnitt).
Dieses Dokument ist **das Gate** für alle künftige UI-Arbeit.

Der Drift-Audit und das tatsächliche Aufräumen des View-Codes sind bewusst **nicht** hier,
sondern in `design-system-002` (hängt an diesem Task) — der Styleguide muss erst als
kanonische Wahrheit stehen, bevor man Code dagegen konsolidiert.

## Acceptance criteria
- [x] `standards/frontend/styleguide.md` existiert und deckt mindestens ab:
  - [x] **Visuelle Sprache**: neon-on-dark, dunkler Grund + Neon-Akzente, Glow, Mono-Font.
  - [x] **Farbsemantik**: feste Bedeutung je Token-Farbe (Orange=primär/CTA, Teal=sekundär,
        Green=Erfolg, Red=Fehler, Purple/Pink=Akzent) — aus `Tokens.fs` belegt.
  - [x] **Token-Layer**: wie Tokens statt hartkodierter Klassen genutzt werden, mit
        Presets/Beispielen aus `Tokens.fs`.
  - [x] **Komponenten-Inventar**: alle 20 DS-Komponenten mit "wann welche / wann *nicht*",
        konsistent mit dem Abschnitt in `CLAUDE.md` (keine Widersprüche).
  - [x] **Muster**: visual-viewport-Sheet + Click-Commit (ADR 0005), Picker-Auswahl
        (ADR 0004), Spring-Easing/Skeleton (Mobile-Polish) — als wiederverwendbare Regeln.
  - [x] **Motion** und **Voice/Tonalität** (Microcopy-Kurzregeln).
- [x] Das Dokument verweist auf `Tokens.fs` und die Komponentendateien als Quelle (keine
      Duplizierung von Implementierungsdetails, die veralten).
- [x] `CLAUDE.md` und `standards/frontend/overview.md` zeigen auf den neuen Styleguide
      (ein Pointer genügt, kein Copy-Paste).
- [x] **Styleguide wird mit Roman reviewt** (das Gate-Review) — erst danach gilt der Task
      als done. Reine Doku-Änderung: kein `dotnet`-Build nötig, aber Links/Pfade prüfen.
      *(reviewt 2026-06-13: Roman akzeptiert das Markdown als **geschriebenen Begleiter**.
      Im Review kam heraus, dass Roman zusätzlich einen **visuell gerenderten** Styleguide
      erwartet → als Feature `design-system-003` (live In-App `/styleguide`-Route)
      ausgegliedert. Links/Pfade geprüft, lösen auf.)*
- [x] Diary-Eintrag in `diary/development.md`.

## Notes
**Das ist das Gate** (Hard-Enforcement, gewählt 2026-06-13): kein UI-Task geht nach `todo/`,
bevor dieser hier done + reviewt ist. `ynab-002` hängt bereits dran.

**Quellen zum Extrahieren (nichts neu erfinden):**
- `src/Client/DesignSystem/Tokens.fs` — die belegte Farb-/Glow-/Spacing-Sprache.
- Die 20 Komponentendateien unter `src/Client/DesignSystem/`.
- `CLAUDE.md` → "Design System Components" — der bestehende API-Referenz-Abschnitt.
- ADR 0005 (visual-viewport-Sheets + Click-Commit) und ADR 0004 (Quick-Add-Picker) —
  projektweite Muster, gehören in den Patterns-Abschnitt.
- Prior art (andere BCs, als Muster-Beleg): `categorization/done/2026-06-11-mobile-category-picker-keyboard-ghostclick.md`,
  `infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs.md`.

**Bewusst NICHT hier:** Drift finden/fixen im View-Code → `design-system-002`.

**Serena-Hinweis:** beim Extrahieren aus `.fs`-Dateien Serena nutzen (`get_symbols_overview`
/ `find_symbol`), nicht roh lesen.

## Outcome
`standards/frontend/styleguide.md` erstellt — der kanonische Styleguide (das Gate) für alle
UI-Arbeit. Reine retroaktive Kodifizierung des bestehenden Design Systems, keine neue
visuelle Sprache erfunden. Inhalt: visuelle Sprache (neon-on-dark/Glow/Mono), Farbsemantik
(je Neon-Farbe feste Bedeutung, belegt mit Token-Namen aus `Tokens.fs`), Token-Layer
(Presets/Module statt hartkodierter Klassen, Wahl-Reihenfolge Komponente → Preset → Token),
Komponenten-Inventar (alle 20 DS-Komponenten mit "wann welche / wann nicht", konsistent mit
dem CLAUDE.md-Abschnitt), Muster (visual-viewport-Sheet + Click-Commit nach ADR 0005,
Picker-Auswahl nach ADR 0004, Sticky-Filter), Motion (Spring-`linear()` hinter `@supports`,
`prefers-reduced-motion`, Skeletons), Voice. Code (`Tokens.fs`, Komponentendateien,
`styles.css`/`index.html`) ist als autoritative Quelle verlinkt, Implementierungsdetails
nicht dupliziert.

Pointer gesetzt: `CLAUDE.md` (oben im Abschnitt "Design System Components") und
`standards/frontend/overview.md` (in "See Also"). Diary-Eintrag ergänzt. Alle relativen
Links aus dem Styleguide lösen auf (geprüft).

**Schlüsseldateien:**
- `standards/frontend/styleguide.md` (neu — das Gate)
- `CLAUDE.md`, `standards/frontend/overview.md` (je ein Pointer)
- `diary/development.md`

**Offen / nicht vom Worker leistbar:** das Gate-Review mit Roman (menschlicher Schritt) —
Acceptance-Box bewusst ungehakt gelassen. Erst danach ist das Gate "scharf" für
nachgelagerte UI-Tasks (`ynab-002`).

**Hinweis Serena:** Die `mcp__serena__*`-Tools waren in dieser Session nicht verfügbar
(`initial_instructions` → "No such tool"); zudem dokumentiert die Projekt-Memory, dass
Serena hier nur partiell funktioniert (kein `search_for_pattern`/`list_dir`/`find_file`).
Extraktion daher über `Tokens.fs` (direkt gelesen) + grep auf Symbol-Köpfe der
Komponenten + die CLAUDE.md-API-Referenz.
