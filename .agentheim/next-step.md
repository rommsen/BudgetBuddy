---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: ds-006 dismisst, ds-007 ready

Die Drift-Splits sind sortiert: Ein Klassifikations-Audit zeigte, dass `design-system-006`
(Buttons) nur 8/34 Kandidaten hat und KEINER byte-identisch liftbar ist (Sheet-CSS, `ready`-Klasse,
`animate-pulse`) → **dismisst** (`1418eac`). `design-system-007` (SVGs) ist der saubere kleine
Gewinn — Inventur eingebacken (2 Swaps + `Icons.chevronLeft` neu, Toggle-Check bleibt custom),
**nach todo promotet** (`050f99d`). Die Work-Session-Commits (infra-001, ds-002, Bookkeeping) sind
bereits auf `origin/main`; **3 Commits** (Dismiss, Promote, dieser next-step-chore) sind noch
ungepusht. Was als Nächstes?

<options>
  <option title="ds-007 abarbeiten" cmd='/agentheim:work design-system-007'>Der einzige todo-Task: 2 Icon-Swaps + `Icons.chevronLeft` ergänzen, Toggle-Check custom lassen. Klein, risikoarm, klar spezifiziert. Danach push+deploy in einem Rutsch.</option>
  <option title="Push (Doku-Stand)">Die 3 ungepushten Doku-/Bookkeeping-Commits nach origin schieben. Kein Code-Change → kein Re-Deploy nötig.</option>
  <option title="Nichts — im Alltag nutzen">Erstmal benutzen; nächste Auffälligkeit/Idee per `capture`/`modeling` einkippen.</option>
</options>
