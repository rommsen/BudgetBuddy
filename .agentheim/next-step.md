---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: ynab-002 gebaut & verifiziert (f2a71ac)

Die Split-Review-UI steht und ist grün (`dotnet test` 569 passed, verifier-PASS, Commit
`f2a71ac`): Cashback-„Barabhebung"-Shortcut (Default-Ziel = Quick-Add-Konto, Kategorie-Rest
rechnet live) + generischer N-Zeilen-Editor (Save gesperrt bei Summe ≠ Gesamt), Transfer-Picker
nur offene On-Budget-Konten. Die im Refinement vorhergesagte Backend-Erweiterung
(`YnabAccount`/`accountDecoder` um `on_budget`/`closed`) war real und wurde mitgebaut. **Zwei
lose Enden:** (a) die Mechanik ist nur durch Tests geprüft — ADR 0005 sagt, mobile Sheet-/Stacking-
Mechanik ist erst am Gerät wirklich verifizierbar; (b) das `.agentheim`-Doku-Bookkeeping (INDEX,
protocol, next-step, Task-commit-Feld + ältere lose Modeling-Stände) liegt bewusst uncommitted
neben dem Feature-Commit. Wie weiter?

<options>
  <option title="ynab-002 am Gerät/Browser testen">Split-Sheet real ausprobieren: `npm run dev` (Vite, Hash-Routing), eine Buchung aufteilen — besonders der Cashback-Ein-Tipp und das Picker-über-Sheet-Stacking (`.layer-2`) auf Mobile, das laut ADR 0005 nur manuell verifizierbar ist. Findet, was Tests nicht sehen (Tastatur, Ghost-Clicks, Live-Rest-Gefühl). Ich kann die App auch selbst starten + screenshotten.</option>
  <option title="Doku-Bookkeeping committen">Der Working Tree trägt uncommittete `.agentheim`-Doku: dieser Work-Session-Stand (INDEX doing→done, protocol-Einträge, next-step, Task-commit-Feld) plus ältere lose Modeling-Stände (vision/context-map/knowledge-index/design-system-003/serena). Als ein sauberer Doku-Commit ablegen, getrennt vom Feature-Code.</option>
  <option title="design-system-002 refinen" cmd='/agentheim:modeling design-system-002'>Letzter offener Backlog-Task (Drift-Audit): View-Code gegen den Styleguide prüfen (hartkodierte Farben, inline-Feliz statt DS-Komponente) und in Refactor-Tasks splitten. Noch unrefined — braucht eine Modeling-Runde, bevor `work` ihn nehmen kann.</option>
</options>
