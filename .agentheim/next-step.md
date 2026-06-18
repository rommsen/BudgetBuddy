---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: Toast-Mobile-Fit gefixt (design-system-005), Deploy läuft

Romans Geräte-Feedback umgesetzt: der Toast sitzt mobil jetzt als kompakter, symmetrisch
eingerückter Streifen (keine abgeschnittene Neon-Border, kein Full-Bleed, deckend über dem
Hero) — diesmal **vor** dem Deploy per headless mobilem Screenshot gegengecheckt (895e56e).
Außerdem erfasst: **infra-001** (flaky SQLite-Disposal-Test) liegt im infrastructure-Backlog.
Nach Push + Re-Deploy: was als Nächstes?

<options>
  <option title="Toast am echten Gerät gegenchecken">Nach dem Deploy einen echten Toast auslösen (z.B. Sync starten/abbrechen) und schauen, ob er jetzt auch über dem pink/orangen Hero sauber sitzt — der Styleguide-Shot war auf dunklem Grund.</option>
  <option title="infra-001 angehen" cmd='/agentheim:modeling infra-001'>Den flaky Persistence-Test stabilisieren: Root Cause bestätigen + `Microsoft.Data.Sqlite`-Versionen vereinheitlichen. Noch under-refined → erst Refine/Investigation.</option>
  <option title="design-system-002 refinen" cmd='/agentheim:modeling design-system-002'>Der ältere Backlog-Task: Drift-Audit der View-Schichten gegen den Styleguide. Jetzt mit Toast + Reorder-Buttons als frische Referenz.</option>
  <option title="Nichts — im Alltag nutzen">Erstmal benutzen; nächste Auffälligkeit/Idee per `capture`/`modeling` einkippen.</option>
</options>
