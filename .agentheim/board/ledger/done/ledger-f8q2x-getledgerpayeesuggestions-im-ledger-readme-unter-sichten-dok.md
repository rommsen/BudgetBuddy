---
id: ledger-f8q2x
title: "getLedgerPayeeSuggestions im ledger-README unter Sichten dokumentieren"
status: done
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

## Outcome

Ein neuer Unterbullet `getLedgerPayeeSuggestions` steht jetzt unter `## Sichten` im
ledger-README, zwischen `getLedgerEntriesPage` und `getLedgerAccountBalances` (dort
platziert, weil `getLedgerPayeeSuggestions` denselben Filter-Aufbau wie
`getLedgerEntriesPage` teilt). Der Eintrag nennt: die Rückgabe (Payee plus Anzahl
innerhalb des aktuellen Treffers, nie über alle Buchungen, nie über die geladene
Seite), dass `Payee` im intern gebauten `EntryFilter` fest auf `None` steht, weil
der Aufruf nie einen bereits gewählten Payee-Filter an sich selbst übergibt, den
geteilten Filter-Aufbau mit `getLedgerEntriesPage` (Kategorie, Zeitraum, Betrag,
Memo) und den Verwendungszweck (Grundlage der Payee-Auswahl im Filter-Chip, ADR
0036).

Gegen den aktuellen Code geprüft, nicht gegen den Task-Text (der Task wurde am
2026-09-19 erfasst, `getLedgerPayeeSuggestions` hat sich seither weiterentwickelt):
`src/Server/Ledger/QueryHandlers.fs:402-418` baut den `EntryFilter` für die
Payee-Vorschläge bewusst separat von `toEntryFilter` (Zeile 334), mit `Payee = None`
fest gesetzt (Zeile 411) und allen übrigen Kriterien aus der Query übernommen. Der
Code-Kommentar über `toEntryFilter` (Zeile 330-333) behauptet zwar, sie sei "die
gemeinsame Grundlage fuer getLedgerEntriesPage und getLedgerPayeeSuggestions":
tatsächlich ruft `getLedgerPayeeSuggestions` `toEntryFilter` nicht auf, sondern baut
den Record-Literal ein zweites Mal. ADR 0036 (Negative Consequences) dokumentiert
das ausdrücklich als bewusst getrennt gehaltene "zwei parallele, sich in Teilen
überschneidende `EntryFilter`-Aufbauten". Der neue Bullet folgt dieser (zutreffenden)
ADR-Aussage, nicht dem veralteten Code-Kommentar, um keinen dritten, abweichenden
Wortlaut einzuführen.

Doppelte Aussagen zur Gruppierung/Faltung von `loadPayeeSuggestions`
(`Ledger.Persistence`, `groupFoldedPayeeCounts`, `Core.searchFold`, ADR 0037/0042,
`ledger-h4qtx`) sind bewusst nicht wiederholt: der Bullet verweist stattdessen auf
den bestehenden Eintrag "Such-Filter (Kassenbuch)" unter `## Ubiquitous language`
(README-Zeile 160), der das bereits vollständig trägt.

**Werkzeug-Grenze wie erwartet, diesmal ohne Ausweichen auf den vollen Block.**
`ledger-t4m9c` (dieser Aufgabe vorgeschaltet, `depends_on`) hatte den Bestand des
`## Sichten`-Blocks bereits korrigiert (u. a. den `getLedgerEntriesPage`-Satz
"Grundlage für den serverseitigen Such-Filter (`ledger-vgfw3`, erledigt)"); diese
Aufgabe hat den Block in genau dieser, von `ledger-t4m9c` hinterlassenen Fassung
übernommen, nicht in der Fassung von vor `ledger-t4m9c`. Anders als bei `ledger-ezrw0`
musste `expected`/`body` hier nicht den ganzen 37-Zeilen-Extent des Eltern-Bullets
`**Sichten**` ausgeben, weil das `replace` mit `anchor: "Sichten"` gegen die reale
Grammatik lief statt gegen eine Annahme: `node scripts/readme-delta-check.mjs --bc
ledger --delta <datei>` (Exit 0) zeigt im Diff einen einzigen Hunk von genau 11
eingefügten Zeilen zwischen `getLedgerEntriesPage` und `getLedgerAccountBalances`,
alle übrigen Zeilen des Blocks unverändert. `section` musste dabei auf die echte
`##`-Überschrift `Ubiquitous language` gesetzt werden (nicht auf den Bullet-Namen
`Sichten`); mit `section: "Sichten"` verwarf das Gate den Delta mit "Prüfung 2
(Abschnitt vorhanden) verletzt", weil `Sichten` selbst kein `##`-Abschnitt ist,
sondern ein Bullet innerhalb von `## Ubiquitous language`.

Kein Code geändert (reine Doku-Aufgabe): `git status` im Worktree blieb während der
gesamten Bearbeitung leer. `dotnet build` lief zur Bestätigung trotzdem grün (0
Warnungen, 0 Fehler), `dotnet test` wurde nicht gefahren, weil kein Testverhalten
betroffen ist.

Diary-Notiz (dieses Projekt schreibt `diary/development.md` normalerweise bei jeder
Änderung; für diese reine `.agentheim/`-Aufgabe wird die Notiz hier statt dort
geführt, wie im Task-Zuschnitt vorgegeben): 2026-09-25, `getLedgerPayeeSuggestions`
im ledger-README unter `## Sichten` dokumentiert, kein Code geändert, README-Delta
gegen `scripts/readme-delta-check.mjs` grün geprüft (Exit 0).

Schlüsseldateien (nur gelesen, nicht verändert):
- `src/Server/Ledger/QueryHandlers.fs`: `getLedgerPayeeSuggestions` (Zeilen
  399-418), `toEntryFilter` (Zeilen 330-344)
- `src/Server/Ledger/Persistence.fs`: `loadPayeeSuggestions` (Zeilen 966-997),
  `groupFoldedPayeeCounts` (Zeilen 935-964)
- `src/Shared/Domain.fs`: `LedgerPayeeSuggestion` (Zeile 966),
  `LedgerPayeeSuggestionQuery` (Zeile 975)
- `src/Shared/Api.fs:313`: `getLedgerPayeeSuggestions` im API-Vertrag
