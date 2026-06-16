---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: Split-Zeile zeigt "Aufgeteilt" (ynab-004, ae7d448)

Dritte Feedback-Runde zum Split umgesetzt: eine aufgeteilte Buchung zeigt in der Liste jetzt
**„Aufgeteilt"** mit ready-Badge statt dem orangen „Kategorie…"-Platzhalter (577 Tests grün).
Wird gerade deployt. Dabei ist eine **bewusste Grenze** aufgetaucht, die du kennen solltest:
**Splits werden nicht persistiert** (`Persistence.fs:677` rekonstruiert `Splits = None`, seit
ynab-001 aufgeschoben) — der Fix gilt für die laufende In-Memory-Session (Sync→Review→Import),
ein DB-Reload würde das Label verlieren. Wie weiter?

<options>
  <option title="ynab-004 am Gerät gegenchecken">Nach dem Deploy: aufgeteilte Buchung in der Liste ansehen — zeigt sie „Aufgeteilt"? Und einen Durchlauf bis zum Import, ob der Split sauber nach YNAB geht. Ich kann auch headless screenshotten.</option>
  <option title="Splits persistieren (ynab-005) modellieren" cmd='/agentheim:modeling'>Die aufgedeckte Grenze schließen: Splits in SQLite ablegen (Schema + Migration), damit „Aufgeteilt" + die Split-Zeilen einen Reload/Neustart überleben. Nur sinnvoll, wenn dich der Verlust nach Reload im Alltag wirklich trifft — sonst YAGNI.</option>
  <option title="design-system-002 refinen" cmd='/agentheim:modeling design-system-002'>Der schon länger offene Backlog-Task: Drift-Audit der View-Schichten gegen den Styleguide. Noch unrefined.</option>
  <option title="Nichts — im Alltag nutzen">Erstmal benutzen; nächste Auffälligkeit/Idee per `capture`/`modeling` einkippen.</option>
</options>
