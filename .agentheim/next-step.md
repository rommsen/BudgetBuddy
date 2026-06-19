---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: Icon gebaut — dein Gate-Review, dann PWA-Mechanik

`design-system-008` ist gebaut + Verifier-PASS (`18b92e7`): App-Mark **"B im Sync-Ring"** in der
Signatur-Gradient + volles Icon-Set unter `src/Client/public/`. Die **eine offene Sache ist AC5 —
deine visuelle Abnahme** des gerenderten Marks (oben im Chat: 512, maskable, 32px-Favicon). Erst
danach gilt das Icon als wirklich „durch". `infra-002` (PWA-Mechanik) ist jetzt **entsperrt**
(008 done), bleibt aber `backlog` (under-refined). Lokal liegen mehrere ungepushte Commits
(ds-007-Code + die PWA-Markdown/Asset-Commits).

<options>
  <option title="Icon abnehmen → infra-002 vorbereiten" cmd='/agentheim:modeling refine infra-002'>Wenn der Mark passt: infra-002 todo-reif machen (vite-plugin-pwa, Manifest mit den #08081a-Werten, index.html-theme-color-Fix, Generator zeigt auf die zwei Quell-SVGs, Shell-SW network-only für /api). Danach `/agentheim:work`.</option>
  <option title="Icon nachschärfen">Änderungswünsche am Mark (Form/Gewicht des B, Ring-Stärke, mehr/weniger Orange, Glow) — sag's mir, ich passe die Quell-SVGs an und regeneriere das Set. AC5 bleibt offen bis du zufrieden bist.</option>
  <option title="Push + Deploy">Die lokalen Commits nach origin. ds-007 + das Icon-Asset sind Code-/Asset-Changes → Re-Deploy sinnvoll; der Rest ist Markdown. (PWA wirkt erst nach infra-002.)</option>
  <option title="Nichts — später">Icon ist sicher im Repo; PWA-Mechanik + Push später.</option>
</options>
