---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: Icon abgenommen — PWA-Mechanik (infra-002) ist dran

`design-system-008` ist **vollständig** (`dcd6aa4`): App-Mark „B im Sync-Ring" nach einer
Rework-Runde von Roman im Gate **abgenommen** (AC5 ✓). Saubere Font-Glyphe (Arial-Bold → Pfad,
zentriert) + tangentiale Sync-Pfeile; volles Icon-Set unter `src/Client/public/`, lean favicon.ico
(16+32). `infra-002` (PWA-Mechanik) ist jetzt **entsperrt**, hängt aber noch im `backlog`
(under-refined). Mehrere ungepushte Commits liegen lokal (ds-007-Code + die ganze PWA-Kette).

<options>
  <option title="PWA-Mechanik bauen (infra-002)" cmd='/agentheim:modeling refine infra-002'>infra-002 todo-reif machen (vite-plugin-pwa, Manifest mit #08081a + den Icon-Pfaden, index.html-theme-color-Fix #0f172a→#08081a, Generator zeigt auf master/maskable/favicon, Shell-SW network-only für /api) — dann `/agentheim:work`. Das ist der eigentliche „BB als PWA"-Schritt.</option>
  <option title="Push + Deploy">Die lokalen Commits nach origin. Das Icon + ds-007 sind Asset-/Code-Changes → Re-Deploy. Hinweis: installierbar wird BB erst nach infra-002 (Manifest/SW); das Icon allein erscheint nur als Favicon/Tab.</option>
  <option title="Nichts — später">Icon ist durch und im Repo; PWA-Mechanik + Push wann du willst.</option>
</options>
