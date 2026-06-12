---
schema_version: 1
project: BudgetBuddy
---

## Nächste Schritte nach dem Mobile-UX-Overhaul + Quick Add

Der UX-Umbau (Picker-Fix, Swipe, Quick Add) ist auf `feature/ux-wow` umgesetzt, getestet
und auf dem docker-host deployed — aber noch nicht nach `main` gemergt. Kandidaten:

<options>
  <option title="feature/ux-wow nach main mergen" cmd='/agentheim:work den Branch feature/ux-wow nach main mergen (Tests grün, deployed, Android-Test durch Roman bestanden)'>Bringt 8 Commits Produktiv-Stand ins Haupt-Repo. Voraussetzung: Roman hat die Feedback-Runde auf dem Handy abgenommen. Geringes Risiko, räumt auf.</option>
  <option title="Transfer-Payees modellieren" cmd='/agentheim:model Transfer-Payees im ynab-sync-Context modellieren (Transfer to/from statt Kategorie, z.B. Barabhebung)'>Das verbliebene hochpriorisierte In-Scope-Feature — passt thematisch perfekt zum neuen Quick Add (Barabhebung = Transfer aufs Bar-Konto). Braucht erst Modellierung.</option>
  <option title="Phase-1-Frage durchdenken" cmd='/sparring Quick Add (Phase 0) läuft — lohnt Phase 1 der YNAB-Ersatz-Idee (Read-only Budget-Mirror) oder bleibt die Companion-Grenze?'>ADR 0003 hat Phase 0 aktiviert; nach ein paar Wochen Quick-Add-Nutzung steht die bewusste Weiter-oder-Stopp-Entscheidung an (Kriterien in docs/idea-ynab-replacement.md). Strategisch, kein Code.</option>
  <option title="Restliche Backlog-Items überführen" cmd='/agentheim:model die verbliebenen backlog.md-Items (Split-UI, Cleared-Setting, optionale Comdirect-PIN, ING) in die passenden Contexts erfassen'>Macht den agentheim-Backlog zur einzigen Wahrheit; danach kann work direkt loslaufen. Reine Hygiene.</option>
</options>
