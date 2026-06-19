---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: Icon-Task ist todo-reif — zeichnen oder Mechanik vorbereiten

`design-system-008` refined + promotet (`9d145fa`): Mark-Konzept gelockt — **"B" im Sync-Ring**
(Hybrid: Identität + Sync-Funktion), **Signatur-Gradient** teal→green→orange, klein auf solid-green
vereinfacht, Grund `#08081a`, maskable/apple-touch-Opazität + ≥20% Safe-Zone in den AC. Damit ist die
offene Design-Frage gelöst → **todo** (einziger todo-Task). `infra-002` (PWA-Mechanik) bleibt
**backlog** und hängt an 008 (Manifest braucht die Icons). Mehrere ungepushte Markdown-Commits +
der ds-007-Code-Change liegen weiter lokal.

<options>
  <option title="Icon zeichnen (work 008)" cmd='/agentheim:work'>Der Worker setzt das Icon-Set um (Master-SVG B-im-Ring + vereinfachte Favicon-SVG + 192/512/maskable/apple-touch). Danach Gate-Review = dein Blick auf den gerenderten Mark. Entsperrt infra-002. Hinweis: Icon-Zeichnen durch einen Agenten ist Geschmackssache — der Verifier/dein Review fängt's.</option>
  <option title="Mechanik vorbereiten (refine infra-002)" cmd='/agentheim:modeling refine infra-002'>infra-002 todo-reif machen: SW-Update-Strategie (autoUpdate vs prompt), Generator-Config (zwei Quell-SVGs), SW-scope hinter Tailscale, index.html-theme-color-Fix. Dann steht alles bereit, sobald das Icon da ist.</option>
  <option title="Push + Deploy">Die lokalen Commits nach origin. Nur ds-007 ist ein Code-Change → Re-Deploy sinnvoll; der Rest (Capture/Refine/Bookkeeping) ist reines Markdown.</option>
  <option title="Nichts — später">Beides sicher erfasst; Icon + PWA später angehen.</option>
</options>
