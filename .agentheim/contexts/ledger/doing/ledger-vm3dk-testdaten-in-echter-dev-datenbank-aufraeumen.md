---
id: ledger-vm3dk
title: Testdaten aus der echten Entwicklungsdatenbank entfernen (Nachwirkung eines Test-Infra-Fehlers)
status: doing
type: bug
context: ledger
created: 2026-09-12
depends_on: []
blocks: []
tags: [test-infra, data-cleanup]
related_adrs: [0030]
related_research: []
prior_art: []
---

## Why
ADR 0030 beschreibt den Fund: `dotnet test` rief nie `Main.fs`s `main` auf (VSTest
laedt die Test-Assembly als Bibliothek), also griff `USE_MEMORY_DB=true` nie, und
Ledger-Tests schrieben in die echte Datei-Datenbank unter
`~/my_apps/budgetbuddy-dev/budgetbuddy.db` — Romans tatsaechliche Entwicklungs-DB mit
~14.000 echten Buchungen. Der Fehler selbst ist mit `ledger-b1xwe` behoben
(`src/Tests/test.runsettings` + `RunSettingsFilePath`); diese Aufgabe ist die
Nachwirkung: die bereits geschriebenen Testdaten stehen noch in der Datei.

Der Worker, der den Fund gemacht hat, hat **nichts geloescht**: der
Sandbox-Berechtigungsfilter hat jeden Loeschversuch blockiert ("Cloud Storage Mass
Delete"), und eine destruktive Aenderung an einer echten, credential-benachbarten
Datei ohne Rueckfrage waere ohnehin falsch gewesen. Eine Sicherungskopie liegt unter
`~/my_apps/budgetbuddy-dev/budgetbuddy.db.pre-cleanup-backup-20260912-150615`.

## What
Roman (oder ein Worker mit expliziter Freigabe fuer Datenbank-Schreibzugriff ausserhalb
von Tests) entscheidet, wie aufgeraeumt wird — vermutlich per SQL-Skript, nicht per
App-Feature. Betroffen, identifiziert am 2026-09-12:
- 106 Test-Konten in `ledger_accounts` (Praefix der Id `01a095*` oder `0195bb00*`,
  Namen wie "Konto 1", "Konto 2", "Comdirect Giro" — Duplikate der Fixtures aus
  `LedgerMirrorTests.fs`).
- 365 Test-Buchungen in `ledger_entries` (195 an den Test-Konten, 170 "verwaist" auf
  den generischen Test-Konto-Ids `0195c000-0000-7000-8000-000000000001`/`...002` aus
  `LedgerTests.fs`) samt zugehoeriger `ledger_entry_events` und `ledger_entry_links`.
- 11 Test-Budget-Schluessel in `settings`
  (`ledger_mirror_server_knowledge_budget-*`, z. B. `budget-erstlauf`,
  `budget-idempotent`, `budget-quickadd-mirror`).
- `settings.ledger_mirror_last_run` steht vermutlich auf einem Test-Ergebnis, nicht
  auf dem letzten echten Spiegel-Lauf; ein neuer "Jetzt spiegeln"-Klick in der App
  korrigiert das von selbst (kein manueller Fix noetig, dieser Wert ist reine
  Anzeige).

Die 7 echten Konten (Praefix `01a07852*`, Namen "Girokonto", "Tagesgeld Consors",
"Tagesgeld ING", "Bar", "Depot", "Kreditkarte", "ING Girokonto") und ~13.864 echten
Buchungen sind unangetastet und muessen es bleiben.

## Acceptance criteria
- [ ] Roman hat entschieden, ob/wie aufgeraeumt wird (SQL-Skript, Wiederherstellung
      aus einer aelteren Sicherung, oder bewusstes Stehenlassen).
- [ ] Falls aufgeraeumt wird: die 7 echten Konten und alle ihre Buchungen bleiben
      exakt unveraendert (Stichprobe vor/nach per `sqlite3` vergleichen).
- [ ] Falls aufgeraeumt wird: ein neuer "Jetzt spiegeln"-Lauf nach dem Aufraeumen
      bestaetigt, dass die Seite wieder korrekt laedt.
- [ ] `settings`-Schluessel mit echten Zugangsdaten (`comdirect_*`, `ynab_token`,
      `ynab_default_*`) werden nicht angefasst.

## Notes
Gefunden waehrend `ledger-b1xwe` (2026-09-12). Backup-Pfad:
`~/my_apps/budgetbuddy-dev/budgetbuddy.db.pre-cleanup-backup-20260912-150615`.
