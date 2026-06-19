---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: infra-002 verfeinert → todo, bereit zum Bauen

`infra-002` (PWA installierbar) ist verfeinert, alle 4 offenen Fragen geschlossen
(autoUpdate · minimale `offline.html` · iOS-Splash übersprungen · Tailscale-Root → SW-`scope: /`)
und nach `todo` promoted. Das Board ist sonst **leer** (alle Backlogs + übrigen BCs = 0) —
infra-002 ist der einzige arbeitbare Task. Hinweis: mehrere lokale Commits sind noch **ungepusht**
(ds-007/ds-008 + PWA-Kette + dieser Refine-Commit) — sinnvoll zusammen mit der fertigen PWA zu
pushen/deployen.

<options>
  <option title="infra-002 bauen" cmd='/agentheim:work infra-002'>Einziger ready Task, deps (ds-008 Icons/Farben, ds-001 Gate) erfüllt. Tradeoff: lokal verifizierbar sind Build + Manifest + Shell-Precache; die Install-/SW-Registrierungs-AC braucht den secure context → echtes Abnehmen erst hinter Tailscale-HTTPS (Deploy).</option>
  <option title="Backlog füllen" cmd='/agentheim:modeling'>Board ist sonst leer — nächste Vision-Prioritäten einkippen (Split-Transaction-UI: Backend da/UI fehlt; Transfer-Payees: hohe Prio). Tradeoff: verzögert das Ausliefern der PWA.</option>
</options>
