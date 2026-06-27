---
schema_version: 1
project: BudgetBuddy
---

# Nächster Schritt nach Quick-Add-Seite + Vorlagen

Quick Add ist jetzt eine eigenständige Seite mit Nav-Eintrag (`ynab-q7k3m`, 06eb2e5) und
zeigt bis zu 5 deduplizierte Vorlagen aus den letzten Quick-Add-Buchungen (`ynab-t4n8p`,
f51d276) — beide first-try verifiziert, `dotnet test` 620 grün, `npm run build` sauber.
todo + doing sind über alle BCs leer. Die Vorlagen werden aber erst sichtbar, wenn ein
Quick-Add-Konto konfiguriert ist und reale YNAB-Daten vorliegen — das ist noch ungetestet
auf dem Gerät.

<options>
  <option title="Quick Add auf dem Handy testen">Das Human-Gate: deployen, Quick-Add-Konto wählen, neuen Nav-Slot + Vorlagen-Chips mobil prüfen. Höchster Realitäts-Check, aber kein Agenten-Schritt — manuell durch Roman.</option>
  <option title="Concept-Page „Quick Add" anlegen" cmd='/agentheim:modeling'>Quick Add konvergiert auf 6 Artefakte (ADR 0003/0004, vier Tasks) und wurde von beiden Workern als Concept-Kandidat gemeldet. Eine Synthese-Seite bündelt die verstreute Quick-Add-Sprache — Doku-Wert, kein neues Feature.</option>
  <option title="Rate-Limit-Handling modellieren" cmd='/agentheim:modeling'>Die offene ynab-sync-Frage (YNAB ~200 req/h) wird durch den zusätzlichen Vorlagen-Read pro Seitenbesuch konkreter. Vorausschauende Härtung, aber noch kein real beobachtetes Problem.</option>
</options>
