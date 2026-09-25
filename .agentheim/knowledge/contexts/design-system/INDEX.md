## ADRs scoped to this BC

<!-- adr-local:start -->
- **0041**: Tailwind-Klassen-Wächter prüft jeden className-Token gegen den echten Tailwind-4-Compiler und `styles.css`, läuft in `dotnet test` mit; dynamische Tokens nur per Textanalyse aufgelöst, der Rest steht begründet in einer mechanisch geprüften Ausnahmeliste (2026-09-25), `../../decisions/0041-tailwind-klassen-waechter-echter-compiler-textanalyse-grenzen.md`
- **0035** — `Stats.NoAccent` statt ClassName-Override: der Gradient-Akzentstreifen von `Stats.view` lässt sich jetzt ganz abschalten, statt nur umgefärbt zu werden. Additiver siebter `StatAccent`-Fall, gewählt gegen ein Verstecken per CSS-Positionsselektor am Aufrufer — 2026-09-19 — `knowledge/decisions/0035-stats-noaccent-statt-classname-override.md`
- **0031** — DaisyUI raus: Tailwind plus eigener Token-Layer, die inerten Klassen ersetzt — 2026-09-13 — `../../decisions/0031-daisyui-raus-tailwind-eigener-token-layer.md`
- **0027** — Kategorienauswahl als eine Karte mit beiden Zuständen; Sektions-Export-Umfang und -Ausnahmen; Portal-Karten schließen sich nacheinander — 2026-09-11 — `../../decisions/0027-kategorienauswahl-und-vollstaendiger-sektions-export.md`
- **0026** — Schriften (Inter, JetBrains Mono, Space Grotesk) selbst ausliefern statt Google-Fonts-Import (löst Decision 3 von 0024 ab) — 2026-09-11 — `../../decisions/0026-schriften-selbst-ausliefern-statt-google-fonts-import.md`
- **0024** — Export-Bundle rendert aus vite build und preview (nicht vite dev); Navigation bekommt additive nicht-fixe Preview-Varianten; Fonts per explizitem Link geladen (Decision 3 abgelöst durch 0026) — 2026-09-11 — `../../decisions/0024-export-bundle-build-nicht-dev-und-nicht-fixe-navigation-preview.md`
- **0009** — Token-Namen für „dunkle Schrift auf Neon" (`onNeon`) + Mikro-Schriftgrößen (`micro`/`microPlus`) — 2026-06-18 — `../../decisions/0009-onneon-foreground-and-micro-font-tokens.md`
- **0007** — Toast-Platzierung (Desktop unten-rechts / Mobile oben) und Zwei-Phasen-Abgang — 2026-06-16 — `../../decisions/0007-toast-placement-and-soft-exit.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- concepts:end -->


## Pointers

- BC README (visuelle Sprache, Tokens, Muster, das Gate): `README.md`
- Styleguide-Artefakt (entsteht in design-system-001): `../../../standards/frontend/styleguide.md`
- Task board (tasks by status) for this BC: `../../../board/design-system/INDEX.md`
