---
id: design-system-003
title: Live In-App /styleguide-Route (visueller Styleguide, rendert echte DS-Komponenten)
status: done                 # backlog | todo | doing | done
type: feature                # feature | bug | refactor | chore | spike | decision
context: design-system
created: 2026-06-13
completed: 2026-06-13
commit: 741af87
depends_on: [design-system-001]
blocks: []
tags: [styleguide, design-system, frontend, route, gallery, gate, feliz]
related_adrs: [0004, 0005]
related_research: []
prior_art: [design-system-001]
---

## Why
Im Gate-Review von `design-system-001` (2026-06-13) kam heraus: Roman erwartet einen
**visuell gerenderten** Styleguide, den er im Browser anschauen kann — nicht (nur) ein
Markdown-Dokument. Das Markdown bleibt als geschriebener Begleiter (Farbsemantik, „wann
nicht", Voice, ADR-Muster — Dinge, die eine Galerie nicht zeigt). Dieser Task liefert das
**visuelle** Gegenstück: eine lebende Komponenten-Galerie.

**Entscheidung (Roman, 2026-06-13):** In-App-`/styleguide`-Route, die die **echten**
DS-Komponenten und Tokens live rendert — **kein** hartkodiertes Standalone-HTML. Begründung:
eine handgeschriebene HTML-Seite würde `Tokens.fs` und die Komponenten duplizieren und sofort
**driften** — genau das, was der Styleguide selbst verbietet („Quelle, nicht Kopie"). Eine
Route, die die realen Komponenten rendert, kann per Konstruktion nicht driften.

## What
Eine neue Seite in der bestehenden Elmish-App, erreichbar unter Route `/styleguide`, die als
**lebende Galerie** das komplette Design System zeigt — gegliedert wie der Markdown-Styleguide
(`standards/frontend/styleguide.md`), aber mit echten, gerenderten Komponenten.

**Integration in die bestehende Routing-Mechanik** (nicht neu erfinden):
- `Page`-DU in `src/Client/Types.fs` (≈ Z. 130) um einen Fall `Styleguide` erweitern.
- `Routing.parseUrl` / `Routing.toUrlSegments` (gleiches Modul) um das Segment `["styleguide"]`
  ↔ `Styleguide` erweitern.
- Render-Branch in `src/Client/View.fs` (`match model.CurrentPage with … | Styleguide -> …`).
- **Auffindbarkeit:** ein dezenter Einstiegspunkt — bevorzugt ein Link in der **Settings**-Seite
  (`Components/Settings/View.fs`), damit die Haupt-Navigation nicht zugemüllt wird. (Falls ein
  Nav-Eintrag besser ins bestehende Muster passt, ist das ok — die Wahl im Diary/Outcome
  begründen.) Die Route muss in jedem Fall direkt per URL erreichbar sein.
- Neues View-Modul nach Projekt-Konvention, z. B. `src/Client/Components/Styleguide/View.fs`
  (in `Client.fsproj` **vor** `View.fs` einreihen). Reines Präsentations-Modul.

**Lokale Interaktivität ohne App-State-Verschmutzung:** Für interaktive Showcases (Modal
öffnen, Toast feuern, BottomSheet hochfahren, Swipe) **`React.useState`-Hooks (Feliz) lokal**
im Styleguide-View nutzen — **keine** neuen app-weiten `Msg`-Fälle dafür einführen. Die Seite
trägt ihren eigenen Demo-Zustand.

## Acceptance criteria
- [x] Route `/styleguide` ist in der App erreichbar (über die bestehende
      `Page`/`Routing`-Mechanik, Feliz.Router) und direkt per URL aufrufbar.
- [x] Ein dezenter Einstiegspunkt existiert (Settings-Link bevorzugt); Wahl im Outcome begründt.
- [x] Die Seite rendert die **echten** DS-Komponenten/Tokens (keine handkopierten Klassen/Hex —
      Import aus `Client.DesignSystem.*`). Gegliedert analog zum Markdown-Styleguide:
  - [x] **Farb-/Token-Swatches**: Neon-Farben + Glows + Backgrounds + Borders aus `Tokens.fs`,
        je mit semantischem Label (Orange=primär/CTA, Teal=sekundär, Green=Erfolg, Red=Fehler,
        Purple/Pink=Akzent).
  - [x] **Typografie**: Font-Familien (`Fonts`) + Größenskala (`FontSizes`) als gerenderte Proben.
  - [x] **Komponenten** in ihren Hauptvarianten, live gerendert: Button (alle Varianten inkl.
        hero/loading/withIcon/group), Card (standard/glass/glow/withAccent/emptyState), Badge
        (alle), Input (text/select/toggle/checkbox/searchableSelect), Money (positiv/negativ/
        large/hero), Stats (withIcon/withTrend/grid), Table (mit Headern), Loading
        (spinner/neonPulse + Skeletons), ErrorDisplay (inline/card/hero/warning), Icons (Grid),
        PageHeader (Varianten), Primitives (Stack/Grid-Demo), Form (submitButton-Gating),
        Toast (per useState feuerbar), Modal (per useState öffenbar), BottomSheet (per useState
        öffenbar), Swipe (eine wischbare Demo-Zeile).
  - [x] **Muster-Hinweise** als kurze Texte mit Verweis auf den Markdown-Styleguide für die
        ausführlichen Regeln (kein Duplizieren der ADR-Begründungen).
- [x] `standards/frontend/styleguide.md` (oben) und `CLAUDE.md` (Design-System-Abschnitt)
      verweisen auf die **lebende** `/styleguide`-Route als visuelles Gegenstück (je ein Pointer).
- [x] `dotnet build` (bzw. der F#/Fable-Build) ist grün — das ist **echter** Client-Code,
      kein reines Doku-Update.
- [x] Diary-Eintrag in `diary/development.md` (inkl. Begründung des Einstiegspunkts).
- [ ] **Visueller Check durch Roman** (zweites Gate): erst danach gilt der Task als done. Der
      Worker liefert die Route fertig + buildbar; der Build-grüne Stand wird verifiziert, der
      visuelle Abnahme-Schritt ist menschlich (Orchestrator reicht die URL/Startanweisung an Roman).

## Notes
**Tests — bewusst pragmatisch:** Die Seite ist **rein präsentational** und komponiert bereits
getestete DS-Komponenten; es entsteht keine neue Domänen-/Update-Logik. Daher sind hier **keine**
erzwungenen Unit-Tests sinnvoll (ein „rendert ohne Exception"-Test wäre nahe an einer Tautologie).
Verifikation = **Build grün** + Romans visueller Check. Falls doch nicht-triviale Hilfslogik
entsteht (z. B. eine Swatch-Liste mit Ableitung), darf/soll die einen kleinen Test bekommen —
aber nichts Tautologisches. (Weicht bewusst von „jedes Feature braucht Tests" ab, weil hier keine
testbare Logik existiert — im Outcome festhalten.)

**Quelle für Inhalt/Gliederung:** `standards/frontend/styleguide.md` (das Markdown-Gate, committet
in c611e98) listet die zu zeigenden Tokens und alle 20 Komponenten mit Varianten auf — das ist die
Inhaltsvorlage. `CLAUDE.md` → „Design System Components" hat die konkreten API-Aufrufe je Komponente.

**Serena-Realität:** Die `mcp__serena__*`-Tools waren in der letzten Session **nicht verfügbar**
(`initial_instructions` → „No such tool"; Projekt-Memory dokumentiert das). CLAUDE.md mandatet zwar
Serena für `.fs`, aber wenn die MCP-Tools nicht da sind: gezielt mit `Read` (offset/limit) lesen,
nicht raten. Komponenten-APIs primär aus `CLAUDE.md` ziehen (vollständige Beispiele dort).

**Bewusst NICHT hier:** Drift-Audit/Konsolidierung des bestehenden View-Codes → `design-system-002`.

## Outcome
Lebende `/styleguide`-Route umgesetzt — eine präsentationale Galerie, die die echten
`Client.DesignSystem.*`-Komponenten und `Tokens.fs`-Werte rendert (keine handkopierten
Klassen/Hex), gegliedert analog zum Markdown-Styleguide: Farb-/Token-Swatches (Neon-Farben,
Backgrounds, Glows) → Typografie (Fonts/FontSizes/Gradient) → 20 Komponenten-Sektionen
(Button, Card, Badge, Money, Stats, Table, Loading, ErrorDisplay, Icons, PageHeader,
Primitives, Input, Form-Gating, Modal, Toast, BottomSheet, Swipe) → Voice/Muster-Hinweise mit
Verweis auf das Markdown.

**Interaktivität ohne App-State:** Modal/Toast/BottomSheet/Swipe/Input/Form-Demos sind
`[<ReactComponent>]`-Sub-Komponenten mit lokalem `React.useState` — keine neuen app-weiten
`Msg`-Fälle.

**Einstiegspunkt:** dezenter Hash-Router-Link (`#/styleguide`) am Ende der **Settings**-Seite
(statt Haupt-Nav-Tab), damit die Navigation nicht zugemüllt wird; der Styleguide ist ein
Referenz-/Entwickler-Werkzeug. Der Anchor hält die Settings-Komponente frei von neuen
Msg-Fällen (Navigation = Root-Concern). Route zusätzlich direkt per URL erreichbar.

**Schlüsseldateien:**
- `src/Client/Components/Styleguide/View.fs` (neu) — die Galerie.
- `src/Client/Types.fs` — `Page.Styleguide` + `Routing` `["styleguide"]`.
- `src/Client/State.fs` — `UrlChanged | Styleguide -> Cmd.none`.
- `src/Client/View.fs` — Render-Branch + `toNavPage Styleguide -> Navigation.Settings`.
- `src/Client/Client.fsproj` — Modul vor `View.fs` registriert.
- `src/Client/Components/Settings/View.fs` — Einstiegs-Link.
- `standards/frontend/styleguide.md`, `CLAUDE.md` — je ein Pointer auf die lebende Route.

**Verifikation:** `dotnet build` (Solution) 0 Fehler; Client-Projekt 0 Fehler/0 Warnungen.
Bewusst **keine** Tests (rein präsentational, keine testbare Logik — „rendert ohne
Exception" wäre tautologisch; im Task so dokumentiert).

**Offen (menschliches Gate):** Romans visueller Abnahme-Check. Der Build-grüne Stand ist
verifiziert, die Route ist fertig und buildbar — bereit zur visuellen Review (Aufruf in der
App: Einstellungen → „Styleguide ansehen", oder direkt `#/styleguide`).
