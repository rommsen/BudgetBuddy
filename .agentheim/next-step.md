---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: beide Tickets erledigt — Push/Deploy + Drift-Splits offen

`infra-001` (flaky SQLite-Test → frische Connection/Op + 9.0.13-Pin, Verifier 10/10 grün,
`82b5cef`) und `design-system-002` (Drift-Audit → Token-Drift voll konsolidiert, Komponenten-Drift
gesplittet, `9e9526a`) sind committet und verifiziert. Das Audit hat zwei neue Backlog-Items
geboren: `design-system-006` (rohe `Html.button` → `Button`) und `design-system-007`
(rohe `Html.svg` → `Icons`). Was als Nächstes?

<options>
  <option title="Push + Deploy">Beide Task-Commits + den chore(agentheim)-Commit nach origin pushen und neu deployen (dein üblicher Abschluss nach einer Work-Session). Lokal alles grün.</option>
  <option title="Drift-Splits refinen" cmd='/agentheim:modeling design-system-006'>006/007 sind raw im Backgelandet (Split-Output, noch nicht promotbar). Per-Site-Urteil (1:1 vs custom Click-Commit/ADR 0005) verfeinern, bevor sie nach todo gehen.</option>
  <option title="Drift-Splits direkt abarbeiten" cmd='/agentheim:work'>006/007 nach todo promoten und durchziehen — die Konsolidierung von Buttons/SVGs aufs DS, jetzt wo die Token-Basis steht. Größerer Refactor mit echtem Verhaltens-Risiko (Sheets/Swipe).</option>
  <option title="Nichts — im Alltag nutzen">Erstmal benutzen; nächste Auffälligkeit/Idee per `capture`/`modeling` einkippen.</option>
</options>
