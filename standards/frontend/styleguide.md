# Styleguide — Visuelle Sprache, Tokens, Komponenten, Muster

> **Das ist das Gate.** Dieses Dokument ist die Single Source of Truth für alle
> UI-Entscheidungen in BudgetBuddy. Jeder neue Frontend-/UI-Task in **jedem** BC wird
> daran gemessen; abweichender View-Code (hartkodierte Farben, inline-Feliz statt
> DS-Komponente, eigene Muster) gilt als **Drift** und gehört korrigiert.
> Es kodifiziert *retroaktiv*, was bereits im Code lebt — es erfindet keine neue Sprache.
>
> **Quelle, nicht Kopie:** Implementierungsdetails (genaue Klassen, Props, Hex-Werte)
> stehen im Code und sind dort autoritativ. Dieses Dokument verlinkt die Quellen und
> beschreibt Bedeutung und Regeln — es dupliziert keine Details, die veralten.
>
> **Verwandt:** [`overview.md`](overview.md) (MVU-Architektur), `CLAUDE.md` →
> *"Design System Components"* (API-Schnellreferenz mit Codebeispielen).
>
> **Lebendes visuelles Gegenstück:** die In-App-Route **`/styleguide`** (erreichbar in
> der App, dezenter Link in den Einstellungen) rendert die **echten** DS-Komponenten und
> Tokens als Galerie — gegliedert wie dieses Dokument. Dieses Markdown bleibt der
> geschriebene Begleiter (Farbsemantik, „wann nicht", Voice, ADR-Muster); die Route
> zeigt das Aussehen live und kann per Konstruktion nicht driften
> (`src/Client/Components/Styleguide/View.fs`).

---

## 1. Visuelle Sprache: neon-on-dark

Das Look-and-Feel ist **neon-on-dark** — ein dunkler Grund mit hellen Neon-Akzenten und
dezentem Glow. Es trägt direkt das Vision-Ziel "Roman fühlt den Workflow als angenehmer
als YNAB-Web": die Oberfläche soll fokussiert, schnell und ein bisschen "cyber" wirken,
nicht wie eine generische Tabelle.

Die drei tragenden Mittel:

- **Dunkler Grund.** Flächen sind dunkel (`bg-surface-app` / `-card` / `-elevated` /
  `-input`), gestaffelt nach Tiefe. Neon-Farben wirken nur auf dunklem Grund.
- **Glow als Hervorhebung, nicht als Deko.** Glow-Shadows (`shadow-glow-*`,
  `text-glow-*`) markieren das Aktive/Wichtige — primäre CTAs, positive Beträge,
  fokussierte Karten. Glow ist sparsam: alles glüht = nichts glüht.
- **Mono-Font für Zahlen und Daten.** Beträge, Counts, technische Werte stehen in
  JetBrains Mono mit `tabular-nums`, damit Ziffern in Spalten sauber untereinander
  stehen. Fließtext in Outfit (`font-sans`), große Header optional in Orbitron
  (`font-display`).

Quelle der Tokens: [`src/Client/DesignSystem/Tokens.fs`](../../src/Client/DesignSystem/Tokens.fs)
(Modul-Header dort: *"Design tokens for the Neon Glow Dark Mode Theme"*). Die Tokens
spiegeln die CSS-Custom-Properties in `src/Client/styles.css` und die Tailwind-Config.

---

## 2. Farbsemantik

Jede Neon-Farbe hat **eine feste Bedeutung**. Farbe wird nie rein dekorativ gewählt —
sie kommuniziert Zustand/Rolle. Belegt durch `Tokens.fs` (Modul `Colors`, `Glows`,
`Borders`, `Backgrounds`) und die jeweiligen Token-Kommentare:

| Farbe | Bedeutung | Token-Beispiele (`Tokens.fs`) |
|-------|-----------|-------------------------------|
| **Orange** | primär / CTA / Action / Energie | `Colors.neonOrange`, `Glows.orangeLg` (Hero-CTAs), `Borders.orange` |
| **Teal** | sekundär / aktiv / Navigation / interaktiv / Info | `Colors.neonTeal`, `Glows.teal`, `Presets.glowCard` (teal-Border) |
| **Green** | Erfolg / positiv / Wachstum | `Colors.neonGreen`, `Glows.green`, `Presets.moneyPositive` |
| **Red** | Fehler / Gefahr / negativ | `Colors.neonRed`, `Presets.moneyNegative`, `Borders.red` |
| **Purple** | Akzent / Kategorie / "special/premium" | `Colors.neonPurple`, `Backgrounds.purpleSubtle` |
| **Pink** | Akzent / "attention needed" | `Colors.neonPink`, `Backgrounds.pinkSubtle` |

Die Token-Kommentare in `Tokens.fs` sind die kanonische Begründung (z. B. *"Primary:
Neon Green — Success, Growth, Positive"*, *"Accent: Cyber Teal — Info, Navigation,
Interactive"*).

**Praktische Konsequenzen:**

- Geld: positiv ist immer Green mit Green-Glow (`Presets.moneyPositive`), negativ immer
  Red (`Presets.moneyNegative`). `Money.view` setzt das automatisch anhand des
  Vorzeichens — nicht manuell überschreiben.
- Primäre Aktion eines Screens: Orange (`Button.primary` / `Button.hero`). Pro
  Screen-Kontext höchstens eine primäre Aktion.
- Sekundär/Abbrechen/Navigation: Teal (`Button.secondary`, aktiver Nav-Tab).
- Löschen/Zerstörendes: Red (`Button.danger`, `Modal.confirmDanger`).
- Text-Tonwerte über `Colors.textPrimary/Secondary/Muted` — nicht über Neon-Farben.

> **Harte Regel (siehe `.claude/rules/design-tokens.md`):** keine Standard-Tailwind-Farben
> (`red-*`, `green-*`, `blue-*`, `gray-*`, …), keine hartkodierten Hex-Werte
> (`bg-[#…]`, `text-[#…]`), keine inline `style.color`. Immer die semantischen Tokens.

---

## 3. Token-Layer — Tokens statt hartkodierter Klassen

UI referenziert **benannte Tokens** aus `Tokens.fs`, nicht rohe Tailwind-Klassen. Der
Token-Layer ist die Indirektionsschicht über Tailwind 4 / DaisyUI: ändert sich der
Look, ändert sich der Token — nicht hunderte Call-Sites.

Module in [`Tokens.fs`](../../src/Client/DesignSystem/Tokens.fs):

- `Colors`, `Backgrounds`, `Borders`, `Glows`, `TextGlows` — Farbsemantik (s. o.).
- `Fonts` (`sans`/`display`/`mono`), `FontSizes` (mobile-first, z. B.
  `pageTitle = "text-xl md:text-4xl"`), `FontWeights`.
- `Spacing`, `Padding`, `Margin` — alle mobile-first (z. B.
  `Padding.cardMobile = "p-4 md:p-6"`).
- `Radius`, `ZIndex`, `TouchTargets` (`minSize = "min-h-[48px] min-w-[48px]"`),
  `Breakpoints`.
- `Animations`, `StaggerDelays`, `Transitions` — siehe Motion (§6).
- `Presets` — fertige Kombinationen für wiederkehrende Muster.

**Presets sind der bevorzugte Einstieg** (Beispiele aus `Tokens.fs`):

- `Presets.card`, `Presets.glassCard`, `Presets.glowCard` — Karten-Looks.
- `Presets.pageHeader`, `Presets.sectionHeader` — Header-Typografie.
- `Presets.monoNumber` (`"font-mono font-semibold tabular-nums"`) — Zahlen.
- `Presets.moneyPositive` / `Presets.moneyNegative` — Geld-Farbe inkl. Glow.
- `Presets.gradientText` — Teal→Green→Orange-Verlaufstext für Hero-Titel.

**Reihenfolge der Wahl:**
1. Gibt es eine **DS-Komponente** für das Ding? → Komponente nutzen (§5).
2. Sonst: gibt es ein **Preset**? → Preset nutzen.
3. Sonst: einzelne **Tokens** kombinieren.
4. Rohe Tailwind-Klassen oder Hex-Werte sind die Ausnahme und gelten als Drift.

---

## 4. Typografie- und Spacing-Skala

- **Mobile-first.** Token-Größen tragen die Responsiveness in sich
  (`md:`-Prefix bereits eingebaut, z. B. `FontSizes.body = "text-[15px] md:text-base"`).
  Keine eigenen `text-[Npx]` setzen — das umgeht die Skala (Drift, siehe Rule).
- **Touch-Targets ≥ 48px** auf Mobile (`TouchTargets.minSize`). Tappables bekommen
  außerdem `touch-action: manipulation`.
- **Zahlen** immer `tabular-nums` (über `Presets.monoNumber` / `Money`).

---

## 5. Komponenten-Inventar (20 DS-Komponenten)

Alle Komponenten liegen unter
[`src/Client/DesignSystem/`](../../src/Client/DesignSystem/). Konkrete API/Props/Beispiele
stehen im Code und in `CLAUDE.md` → *"Design System Components"* — hier steht nur
**wann welche / wann nicht** (konsistent mit `CLAUDE.md`, keine Widersprüche).

> **Drift-Definition:** Inline-Feliz für etwas, das eine dieser Komponenten abdeckt,
> ist Drift. Erst Komponente suchen, dann bauen.

| Komponente | Wann nutzen | Wann **nicht** |
|------------|-------------|----------------|
| **Button** | Jede klickbare Aktion: `primary` (Orange-CTA), `secondary` (Teal), `ghost`, `danger`, `hero`/`heroTeal` (große CTAs), `*Loading`, `*WithIcon`, `group` | Navigation zwischen Seiten → `Navigation`; Link nach extern → eigener Anker mit `Icons.externalLink` |
| **Card** | Inhaltsblöcke: `standard`, `glass`, `glow` (featured), `withAccent`, `emptyState`, plus `header`/`body`/`footer` | Volle Seiten-Layouts → `Primitives`; modale Inhalte → `Modal` |
| **Badge** | Kompakter Status/Label: semantisch (`success`/`warning`/`error`/`info`), Domänen-Status (`imported`, `pendingReview`, `autoCategorized`, `uncategorized`), `count` | Klickbare Filter → `Button.ghost`/Pills; lange Texte |
| **Input** | Alle Formularelemente: `text`/`password`/`email`/`number`, `textarea`, `select`/`selectSimple`, `checkbox`, `toggle`, `searchableSelect`, plus `group`/`groupWithError`/`formSection` | Reine Anzeige; mobiles Kategorie-/Konto-Auswählen → `BottomSheet` (§7) |
| **Modal** | Zentrierte Dialoge auf **Desktop**: `simple`, `custom`, `fullScreen`, `confirm`/`confirmDanger`, `alert`, `loading` | Mobile Auswahl-Flows → `BottomSheet` (keyboard-fest); persistente Inhalte |
| **Toast** | Flüchtiges Feedback nach Aktion: `success`/`error`/`warning'`/`info'`, `renderList` | Blockierende Fehler → `ErrorDisplay`/`Modal`; persistenter Status → `Badge` |
| **Stats** | KPI-/Kennzahl-Kacheln: `withIcon`, `withTrend`, `transactionCount`, `syncCount`, `grid*` | Vollständige Tabellen → `Table`; einzelner Betrag im Fließtext → `Money` |
| **Money** | **Jede** Geldanzeige — setzt Vorzeichen-Farbe + Glow automatisch: `simple`, `large`, `hero`, `noSign`, `fromMoney` | Nicht-monetäre Zahlen → `Presets.monoNumber` |
| **Table** | Dichte tabellarische Daten (Desktop), inkl. `headerCell`/sortierbar/`sticky` | Mobile Listen mit Zeilen-Aktionen → Card-Liste + `Swipe`; einfache Key-Value-Paare → `Card` |
| **Loading** | Ladezustände: `spinner`, `centered`, `neonPulse`, Skeletons (`txListSkeleton`, `tableSkeleton`, `statsGridSkeleton`, `cardSkeleton`) | Inhalt da → echten Inhalt zeigen; Fehler → `ErrorDisplay` |
| **ErrorDisplay** | Fehlerdarstellung: `inline'` (Feldfehler), `cardCompact`/`card`, `hero` (große Operation), `fullPage`, `forRemoteData`, `warning` | Kurzes Erfolgs-/Info-Feedback → `Toast`; Pflichtfeld-Hinweis → `Input.group*` |
| **Icons** | SVG-Icons mit `Size`×`Color`-Tokens (`Icons.sync MD Teal`, …) | Eigene rohe `<svg>` inline (Drift) — fehlt ein Icon, hier ergänzen |
| **Navigation** | Top-/Bottom-Nav, App-Wrapper, Page-Content-Container — einmal in der Haupt-`View.fs` | Aktionen innerhalb einer Seite → `Button` |
| **PageHeader** | Seitentitel: `simple`, `withSubtitle`, `gradient*`, `withActions`, `gradientWithActions` | Karten-Titel → `Card.header`; Abschnittstitel → `Card.headerSimple`/`Presets.sectionHeader` |
| **Primitives** | Layout-Bausteine: `Container`, `Stack`/`HStack`, `Grid`, `Spacer`, responsive Sichtbarkeit (`mobileOnly`/`desktopOnly`) | Visuell gestylte Blöcke → `Card`; Token-Klassen reichen für Spacing |
| **Tokens** | Quelle aller Klassen-Konstanten (§3) — überall referenzieren | Niemals umgehen: rohe Tailwind-/Hex-Klassen sind Drift |
| **Form** | Submit-Buttons mit Required-Field-Gating: `submitButton(WithIcon/Secondary)` deaktivieren bei fehlenden Pflichtfeldern | Reine Felder → `Input`; freie Buttons → `Button` |
| **BottomSheet** | **Mobile** Auswahl-/Picker-Flows (keyboard-aware): `view`, `categoryPicker`, `categoryPickerLayered`, `chipButton`, `sectionTitle` | Desktop-Dialog → `Modal`; einfaches Dropdown → `Input.select` |
| **Swipe** | Wischbare Listenzeilen mit enthüllter Aktion (mobile) | Statische Zeilen → Card/`Table`; Desktop-Hover-Aktionen → `Button.iconButton` |
| **Viewport** | Infrastruktur für mobile Sheets (visualViewport→CSS-Vars, Scroll-Lock, Click-Swallow, Haptik) — von `BottomSheet` genutzt | Direkt nur, wenn du ein **neues** Sheet/Overlay baust (dann die Muster aus §7 erben) |

---

## 6. Motion

Bewegung ist subtil, schnell und respektiert Nutzerpräferenzen. Quelle:
`Tokens.fs` (Module `Animations`, `Transitions`, `StaggerDelays`) und
[`src/Client/styles.css`](../../src/Client/styles.css).

**Regeln:**

- **Standard-Transitions** über `Transitions.fast/normal/slow` (150/200/300ms, `ease-out`).
- **Spring-Einfahrt** für Sheets/Overlays über `linear()`-Easing, **hinter
  `@supports`** (Safari 17.2+ / Chrome 113+), mit Fallback auf die bestehende Kurve.
  Token: `--sf-spring-out` in `styles.css` (`@supports (transition-timing-function:
  linear(0, 1))`). Sheet-Einfahrt nutzt diesen Federwert statt mechanischem `ease`.
- **`prefers-reduced-motion: reduce` wird global respektiert** (mehrere
  `@media`-Blöcke in `styles.css`) — Animationen werden dort entschärft/abgeschaltet.
  Neue Animationen müssen das mit abdecken.
- **Skeletons statt Spinner**, wo die Zielform bekannt ist: `Loading.txListSkeleton`
  zeigt transaktionszeilenförmige Platzhalter → **kein Layout-Sprung**, wenn echte
  Zeilen eintreffen. Generischer `spinner`/`neonPulse` nur, wo die Form unbekannt ist.
  (Beleg: `infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs`.)
- **Stagger** für sequentielle Einblendungen über `StaggerDelays.forIndex i`.
- `neonPulse`/`text-glow-*` sparsam — Glow markiert, es dekoriert nicht (§1).

---

## 7. Muster (projektweite Interaktions-Konventionen)

Muster stehen **oberhalb** einzelner Komponenten: verbindliche Konventionen, die jede
neue Overlay-/Auswahl-UI erbt.

### 7.1 Visual-Viewport-Sheet + Click-Commit (ADR 0005)

Das zentrale Mobile-Muster für **alle** Bottom Sheets / Overlays. Quelle:
[`Viewport.fs`](../../src/Client/DesignSystem/Viewport.fs),
[`BottomSheet.fs`](../../src/Client/DesignSystem/BottomSheet.fs),
`styles.css`, `index.html`. Verbindlich:

1. **Visual-Viewport-Anker.** `Viewport.fs` spiegelt `visualViewport.height/offsetTop`
   in die CSS-Variablen `--vvh` / `--vv-top`. Sheets ankern mit
   `top: calc(var(--vv-top) + var(--vvh))` + `translateY(-100%)` (Fallback `100dvh`).
   `interactive-widget=resizes-content` steht im Viewport-Meta (`index.html`) für
   Android. → Sheet bleibt bei offener Tastatur vollständig sichtbar.
2. **Click-Commit.** Auswahl committet auf echtem `click`, **nie** auf
   `pointerdown`/`touchend`. `onMouseDown → preventDefault()` hält Fokus/Tastatur.
   Beim Schließen einmaliger Click-Swallow-Guard (`Viewport.swallowNextClick`), damit
   der nachgelagerte synthetische Click nicht auf das Element hinter dem geschlossenen
   Sheet durchschlägt (der "Ghost-Click").
3. **Flankierend verbindlich:** gezählter Body-Scroll-Lock (iOS-fest via
   `position: fixed`); Suchfelder ≥ 16px und **oberhalb** der Liste gepinnt (kein
   iOS-Auto-Zoom, bleibt beim Scrollen sichtbar); `touch-action: manipulation` auf
   Tappables; expliziter Close-Button; `autoFocus` **nur bei feinem Pointer**
   (`Viewport.isFinePointer()`).
4. **Stacking nur über Layer-Klasse** (`.layer-2`, z-index 70/80), maximal eine Ebene
   tief (`categoryPickerLayered`).

> Baust du ein neues Sheet/Overlay, baue es über `BottomSheet`/`Viewport` — erfinde den
> Anker- und Commit-Mechanismus nicht neu. Begründung & Root-Causes: **ADR 0005**;
> Beleg: `categorization/done/2026-06-11-mobile-category-picker-keyboard-ghostclick`.

### 7.2 Picker-Auswahl statt Freitext (ADR 0004)

Im Quick-Add-Flow (und analogen Flows) wird **strukturiert ausgewählt**, nicht frei
getippt, wo es eine endliche Menge gibt:

- **Konto:** echter Picker (z. B. über `BottomSheet`/`Input.select`/`searchableSelect`),
  kein Freitextfeld. (ADR 0004 begründet das fachlich: ein konfiguriertes
  Quick-Add-Konto, kein geratener Fallback.)
- **Payee:** **optional** — leere optionale Felder werden weggelassen, nicht leer
  abgesendet. UI macht klar, dass Payee weggelassen werden darf.

> UI-relevanter Teil von ADR 0004; die Backend-/Domain-Details (kein ImportId,
> `uncleared`, eigenes `ynab_quickadd_account_id`) stehen in der ADR und werden hier
> **nicht** dupliziert.

### 7.3 Sticky-Filter über Listen

Filter-Pills bleiben **sticky** unter dem Header (Backdrop-Blur), Date-/Section-Header
darunter neu verankert. Filterwechsel braucht kein Hochscrollen. Beleg:
`infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs`;
Positionierung in `styles.css`. Sticky-Tabellenköpfe analog über `Table` (`Sticky=true`).

---

## 8. Voice / Tonalität (Microcopy)

Die UI spricht **Deutsch, direkt, knapp, ohne Füllwörter** — wie Roman selbst
("Neue Kategorienauswahl ist gut."). Kurzregeln:

- **Deutsch.** UI-Texte auf Deutsch (Code/Token-Namen bleiben Englisch).
- **Knapp.** Buttons als Verb oder Verb+Objekt: "Speichern", "Sync starten",
  "Kategorie wählen". Keine Höflichkeitsfloskeln ("Bitte klicken Sie hier, um …").
- **Direkt & ehrlich.** Fehlertexte benennen, was passiert ist und was zu tun ist —
  kein Beschönigen, kein generisches "Etwas ist schiefgelaufen", wenn die Ursache
  bekannt ist.
- **Beträge sprechen für sich.** Vorzeichen + Farbe (Green/Red) tragen die Bedeutung;
  kein "Sie haben X ausgegeben" drumherum nötig.
- **Empty States** sagen, was als Nächstes zu tun ist (`Card.emptyState` mit
  Aktion-Button), statt nur "Keine Daten".
- **Konsistente Begriffe.** Ubiquitous Language der BCs verwenden (Import, Kategorie,
  Sync, Split, Transfer) — nicht synonym variieren.

---

## Quellen (autoritativ — bei Konflikt gewinnt der Code)

- [`src/Client/DesignSystem/Tokens.fs`](../../src/Client/DesignSystem/Tokens.fs) — Farb-/Glow-/Spacing-/Preset-Tokens.
- [`src/Client/DesignSystem/`](../../src/Client/DesignSystem/) — die 20 Komponenten.
- [`src/Client/styles.css`](../../src/Client/styles.css) / [`index.html`](../../src/Client/index.html) — Spring-Easing, `prefers-reduced-motion`, visualViewport-Anker, Viewport-Meta.
- `CLAUDE.md` → *"Design System Components"* — API-Schnellreferenz mit Codebeispielen.
- `.claude/rules/design-tokens.md` — die harte Token-only-Regel + Grep-Checks.
- ADR 0005 (visual-viewport-Sheets + Click-Commit), ADR 0004 (Quick-Add-Picker).
- Prior art: `categorization/done/2026-06-11-mobile-category-picker-keyboard-ghostclick`,
  `infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs`.

> Drift-Audit und Code-Konsolidierung gegen diesen Styleguide: `design-system-002`.
