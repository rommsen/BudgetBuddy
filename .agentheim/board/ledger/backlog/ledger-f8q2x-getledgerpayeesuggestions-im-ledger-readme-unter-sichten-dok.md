---
id: ledger-f8q2x
title: "getLedgerPayeeSuggestions im ledger-README unter Sichten dokumentieren"
status: backlog
type: chore
context: ledger
created: 2026-09-19
completed:
depends_on: [ledger-t4m9c]
blocks: []
tags: [doku, kassenbuch, readme]
related_adrs: [0036]
related_research: []
prior_art: [ledger-vgfw3, ledger-ezrw0]
---

## Why
`ledger-vgfw3` hat `getLedgerPayeeSuggestions` eingeführt (`src/Server/Ledger/QueryHandlers.fs:391`,
`src/Shared/Api.fs:313`, `LedgerPayeeSuggestion` in `src/Shared/Domain.fs:959`) —
die Payee-Vorschläge eines Filters samt ihrer Anzahl innerhalb dieses Filters, Grundlage
der Payee-Auswahl im Filter-Chip. Der `## Sichten`-Abschnitt im ledger-README listet die
sechs anderen Lesefunktionen von `QueryHandlers.fs` (`getLedgerAccounts`,
`getLedgerEntriesForAccount`, `getLedgerEntriesPage`, `getLedgerAccountBalances`,
`getEntryHistory`, `getLastMirrorRun`), aber `getLedgerPayeeSuggestions` fehlt darin
komplett. Wer die Sichten-Liste als Inventar der Lesefunktionen liest, findet diese
Funktion nicht.

`ledger-ezrw0` hat diese Lücke beim Ergänzen des `getLedgerEntriesPage`-Eintrags
bewusst nicht mitgeschlossen (Scope-Disziplin: eine Aufgabe, eine Ergänzung) und sie
stattdessen hierher zurückgestellt.

## What
Einen neuen Unterbullet `getLedgerPayeeSuggestions` unter `## Sichten` im ledger-README
ergänzen, im selben Stil wie die bestehenden Einträge: was die Funktion zurückgibt
(Payee plus Anzahl **innerhalb des aktuellen Filters**, nie inklusive eines bereits
gewählten Payee-Filters selbst — `query.Payee` wird beim Aufbau des internen
`EntryFilter` nie gesetzt), welchen gemeinsamen Filter-Aufbau sie sich mit
`getLedgerEntriesPage` teilt (`toEntryFilter`, Kommentar in
`src/Server/Ledger/QueryHandlers.fs:320-323`: "damit beide nie auseinanderlaufen
koennen"), und wofür sie in der Oberfläche dient (Grundlage der Payee-Auswahl im
Filter-Chip, ADR 0036).

## Acceptance criteria
- [ ] Ein neuer Unterbullet `getLedgerPayeeSuggestions` steht unter `## Sichten`, an
      passender Stelle in der Aufzählung (z. B. nach `getLedgerEntriesPage`, da beide
      denselben Filter-Aufbau teilen).
- [ ] Der Eintrag nennt: Rückgabe (Payee + Anzahl im aktuellen Filter), dass
      `query.Payee` selbst nie gesetzt wird, den geteilten Filter-Aufbau mit
      `getLedgerEntriesPage`, und den Verwendungszweck (Payee-Auswahl im Filter-Chip).
- [ ] Deckt sich wörtlich mit dem Code (`getLedgerPayeeSuggestions` in
      `src/Server/Ledger/QueryHandlers.fs`, `LedgerPayeeSuggestion` in
      `src/Shared/Domain.fs`). Kein dritter, abweichender Wortlaut.
- [ ] Falls das Delta-Werkzeug den neuen Bullet wegen derselben Spalte-0-Grenze nicht
      direkt einfügen kann (siehe `ledger-ezrw0`s Werkzeug-Hinweis), steht diese Grenze
      im Ergebnis benannt.

## Notes
Erfasst am 2026-09-19 aus der Bearbeitung von `ledger-ezrw0`. Reine Doku-Aufgabe: am
Code ändert sich nichts.

**Reihenfolge (ergänzt bei der Verfeinerung am 2026-09-20):** `depends_on: [ledger-t4m9c]`.
Beide Aufgaben fassen denselben `## Sichten`-Block an, und beide müssen ihn wegen der
Spalte-0-Grenze der README_DELTA-Grammatik als Ganzes neu ausgeben (siehe
`infrastructure-b6kzp`). Liefen sie parallel, würden sich ihre Deltas beim Integrieren
gegenseitig überschreiben, ohne dass die Disposition das meldet. `ledger-t4m9c` korrigiert
zuerst den Bestand, danach ergänzt diese Aufgabe den neuen Unterbullet im bereits
korrigierten Block. Wer hier arbeitet, gibt den Block also in der Fassung aus, die
`ledger-t4m9c` hinterlassen hat, nicht in der heutigen.
