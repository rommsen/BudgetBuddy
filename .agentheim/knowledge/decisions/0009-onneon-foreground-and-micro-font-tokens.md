# ADR 0009: Token-Namen für „dunkle Schrift auf Neon" und Mikro-Schriftgrößen

- Status: accepted
- Date: 2026-06-18
- Scope: design-system
- Context-task: design-system-002 (Drift-Audit + Konsolidierung)
- Supersedes: —
- Related: ADR 0005 (DS-Muster), Styleguide §2 (Farbsemantik) / §3 (Token-Layer) / §4 (Typo-Skala)

## Kontext

Das Drift-Audit (design-system-002) gegen den Styleguide-Gate fand im View-Code zwei
wiederkehrende Verstöße gegen die harte Token-only-Regel (`.claude/rules/design-tokens.md`,
Styleguide §3), für die es **kein benanntes Token** gab — also auch keinen sauberen Weg,
sie zu beheben:

1. **`text-[#0a0a0f]`** — dunkle „ausgestanzte" Schrift auf hellen Neon-Flächen
   (Badge-Counts, Schritt-Kreise im Sync-Flow, Text auf dem Neon-Gradient-Header).
   Der Hex `#0a0a0f` ist exakt der App-Hintergrund (`color: #0a0a0f` in `styles.css`,
   `bg-surface-app`). Der Wert tauchte **roh** an 6 Call-Sites in `StatusViews.fs` auf
   und zusätzlich in den DS-Komponenten selbst (`Badge.fs` Filled-Variants + Count-Badge).

2. **`text-[10px]` / `text-[11px]`** — Mikro-Labels (Stat-Captions, Status-Chips,
   Mono-Glyphen in Regel-Badges, Desktop-Meta-Strings). Bewusst **kleiner** als der
   kleinste Skalen-Token `FontSizes.xs = text-xs` (12px). 13 rohe Call-Sites.

Beides sind legitime visuelle Bedürfnisse (kein zu entfernendes Design), aber als rohe
arbitrary-Klassen gelten sie als Drift.

## Entscheidung

Die Werte werden als **benannte Tokens** in `Tokens.fs` kanonisiert; Call-Sites
referenzieren das Token statt der rohen Klasse. Der gerenderte CSS-String bleibt
unverändert (reines Anheben aufs DS, kein Look-Change).

- `Colors.onNeon = "text-[#0a0a0f]"` — dunkle Schrift auf hellen Neon-Flächen.
- `Colors.onNeonMuted = "text-[#0a0a0f]/70"` — sekundärer Text auf Neon-Gradient.
- `FontSizes.micro = "text-[10px]"` — Mikro-Labels (unter der `xs`-Skala, bewusst).
- `FontSizes.microPlus = "text-[11px]"` — etwas größere Mikro-Labels.

Begründung der Namen:
- `onNeon` macht die **Semantik** explizit (Vordergrund AUF Neon), nicht den Hex-Wert —
  konsistent mit der Farbsemantik-Regel „Farbe kommuniziert Rolle, nicht Deko" (§2).
- `micro`/`microPlus` reihen sich unter `xs` in die bestehende `FontSizes`-Skala ein und
  signalisieren „absichtlich winzig", statt einen rohen Pixelwert zu raten.

## Konsequenzen

- Die harte Token-only-Regel ist an **allen** Call-Sites für diese zwei Kategorien erfüllt
  — sowohl in den fachlichen Views als auch **DS-komponenten-intern**: `Badge.fs`
  (Filled-Variants, Count **und** `sizeToClass`/Small), `Navigation.fs` (Nav-Label) und
  `Stats.fs` (Compact-Caption) nutzen die Tokens ebenfalls. DS-komponenten-interne
  Mikro-Größen werden **nicht** bewusst roh gelassen: derselbe Token-Layer gilt für die
  DS-Komponenten selbst (sonst driftet die Quelle gegen ihren eigenen Token-Layer).
- Der rohe Hex/arbitrary-Wert lebt nur noch an der **Definitionsstelle** (Tokens.fs) —
  Tailwind v4 JIT generiert die Klasse weiterhin, weil der String-Literal dort steht;
  Runtime-CSS identisch, kein visueller Regress (Tests 595/595 grün).
- `Loading.fs` nutzt `bg-[#0a0a0f]/80` als **Hintergrund-Overlay** — das ist eine andere
  Rolle (Background, nicht Foreground) und bleibt bewusst außerhalb von `onNeon`.
- Die `neon*Dim`-Tokens (`text-[#…]`) bleiben unverändert (vorbestehend, eigene Rolle).

## Nicht Teil dieser Entscheidung

Die größere Drift-Klasse — rohe `Html.button` → `Button` und rohe `Html.svg` → `Icons`
in den SyncFlow-Views — wird **nicht** hier behandelt: viele dieser Elemente tragen
Custom-Interaktion (Click-Commit/ADR 0005, Swipe, `action-chip`-Chips), sind also kein
1:1-Lift. Sie sind als eigene, per-Kontext zu prüfende Backlog-Tasks ausgegliedert
(design-system-006, design-system-007).
