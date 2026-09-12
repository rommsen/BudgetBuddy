---
id: ledger-vm3dk
title: Testdaten aus der echten Entwicklungsdatenbank entfernen (Nachwirkung eines Test-Infra-Fehlers)
status: done
type: bug
context: ledger
created: 2026-09-12
completed: 2026-09-12
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
- [x] Roman hat entschieden, ob/wie aufgeraeumt wird (SQL-Skript, Wiederherstellung
      aus einer aelteren Sicherung, oder bewusstes Stehenlassen).
- [x] Falls aufgeraeumt wird: die 7 echten Konten und alle ihre Buchungen bleiben
      exakt unveraendert (Stichprobe vor/nach per `sqlite3` vergleichen).
- [x] Falls aufgeraeumt wird: ein neuer "Jetzt spiegeln"-Lauf nach dem Aufraeumen
      bestaetigt, dass die Seite wieder korrekt laedt.
- [x] `settings`-Schluessel mit echten Zugangsdaten (`comdirect_*`, `ynab_token`,
      `ynab_default_*`) werden nicht angefasst.

## Notes
Gefunden waehrend `ledger-b1xwe` (2026-09-12). Backup-Pfad:
`~/my_apps/budgetbuddy-dev/budgetbuddy.db.pre-cleanup-backup-20260912-150615`.

## Outcome

Aufgeräumt am 2026-09-12 in der Hauptsession, auf Romans ausdrückliche Freigabe ("dann
räum auf"), per SQL in einer einzigen Transaktion.

**Vorgehen.** Vor jedem Schreibzugriff ein eigenes Backup
(`budgetbuddy.db.backup-vor-vm3dk-20260912-173520`, 21.475.328 Bytes) und ein
Fingerabdruck der echten Daten (SHA-256 über alle Buchungen als
`id|account_id|amount|date` und über alle Konten als `id|name|kind|ynab_account_id`).
Gelöscht wurde in der Reihenfolge Kind vor Elternteil: Verknüpfungen, Ereignisse,
Buchungen, Konten, Einstellungen. Die sieben echten Konten wurden **einzeln als
Id-Liste** angegeben, nicht über ein Präfix-Muster, damit kein Zufallstreffer möglich ist.

**Wie die Trennung belegt wurde.** Nicht über den Id-Präfix allein, sondern über die
Buchungszahl je Konto. Die sieben echten Konten tragen zwischen 26 und 11.917 Buchungen
und zusammen genau 13.864, die Zahl aus der Abnahme des Walking Skeletons
(`ledger-kt0wq`). Jedes der 106 Test-Konten trug höchstens fünf. Dazwischen liegt keine
Grauzone.

**Ergebnis:**

| Tabelle | vorher | nachher | entfernt |
|---|---|---|---|
| `ledger_accounts` | 113 | 7 | 106 |
| `ledger_entries` | 14.059 | 13.864 | 195 (25 an Test-Konten, 170 verwaist) |
| `ledger_entry_events` | 14.309 | 13.866 | 443 |
| `ledger_entry_links` | 16.017 | 15.698 | 319 (davon 14 verwaist) |
| `settings` | 21 | 10 | 11 Test-Budget-Schlüssel |

Die beiden Fingerabdrücke der echten Daten sind vor und nach dem Löschen **identisch**
(Buchungen `5229eda7…`, Konten `4cb4b531…`), auch nach dem anschliessenden `VACUUM`
(Datei von 21,5 auf 20,6 MB). `PRAGMA integrity_check` meldet `ok`,
`PRAGMA foreign_key_check` keine Verletzung.

**Zugangsdaten unangetastet:** alle fünf `comdirect_*`-Schlüssel, `ynab_token`,
`ynab_default_account_id` und `ynab_default_budget_id` stehen unverändert, ebenso der
echte Spiegel-Schlüssel `ledger_mirror_server_knowledge_e0b4c3e7-…`.

**Lesepfad danach verifiziert** (Server gestartet, nur lesende Abfragen):
`getLedgerAccountBalances` liefert sieben Konten, `getLedgerEntriesPage` über alle Konten
liefert `TotalCount` 13.864 und `TotalAmount` 38.686,82 EUR in 0,155 Sekunden. Die
Differenz von 3,80 EUR zur Summe der sieben Kontostände (38.690,62 EUR) löst sich
vollständig auf: genau eine in YNAB gelöschte Buchung über -3,80 EUR, die der Kontostand
nach ADR 0013 ausschliesst und die Treffersumme einschliesst. Genau dieser Effekt ist der
Grund für `ledger-p3n8k`.

**Was bewusst offen blieb:** `settings.ledger_mirror_last_run` steht noch auf einem
Testwert (2026-02-04). Der Wert ist reine Anzeige und korrigiert sich beim nächsten Klick
auf "Jetzt spiegeln" von selbst; ein echter Spiegel-Lauf wurde hier nicht ausgelöst, weil
er YNAB kontaktiert und schreibt. Das ist Romans Entscheidung, nicht die eines
Aufräum-Skripts.
