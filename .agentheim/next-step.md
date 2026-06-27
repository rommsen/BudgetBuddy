---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: Quick-Add-Verbesserungen modelliert → 1 todo, 1 backlog

Zwei Ideen rund um Quick Add captured **und** refined (alles committet, Tree sauber):

- **`ynab-q7k3m` → todo, bereit:** Quick Add wird eine **eigene Seite** mit eigener Route
  (neuer Page-DU-Fall), erreichbar aus der Haupt-Navigation; der Quick-Add-State wird aus
  SyncFlow ins Top-Level gehoben; die zwei alten sync-flow-gebundenen Einstiege fliegen
  raus. ACs mit file:line-Ankern, dep `design-system-001` (done) erfüllt.
- **`ynab-t4n8p` → backlog, gated:** Letzte **5 deduplizierte** Buchungen des Quick-Add-
  Kontos als Vorlagen → Formular voll vorausgefüllt (Datum=heute), kein Auto-Push.
  `depends_on: ynab-q7k3m` (die Vorlagen rendern in der neuen Seite). Schlüssel-Befund:
  der YNAB-Read-Pfad (`getAccountTransactions`) **existiert schon** → deutlich kleinerer Task.

Sinnvolle Reihenfolge: erst die Seite (`ynab-q7k3m`), dann die Vorlagen darauf
(`ynab-t4n8p` nach Promote).

<options>
  <option title="Quick-Add-Seite bauen" cmd='/agentheim:work ynab-q7k3m'>Den einzigen todo abarbeiten — liefert den fehlenden Klickpfad (z. B. nach Import) und entsperrt die Vorlagen. Tradeoff: Elmish-Refactor mit State-Lift + Routing, etwas mehr als ein One-Liner.</option>
  <option title="Vorlagen jetzt refinen/promoten" cmd='/agentheim:modeling promote ynab-t4n8p'>Wenn du die Vorlagen zuerst willst — aber sie hängen an der Seite; vorzuziehen riskiert Rework am Prefill-Ziel. Tradeoff: gegen die sinnvolle Reihenfolge.</option>
  <option title="Weiter modellieren" cmd='/agentheim:modeling'>Andere Vision-Prioritäten einkippen (Split-UI, Transfer-Payees …) statt jetzt zu bauen. Tradeoff: verschiebt die Quick-Add-Verbesserung nach hinten.</option>
</options>
