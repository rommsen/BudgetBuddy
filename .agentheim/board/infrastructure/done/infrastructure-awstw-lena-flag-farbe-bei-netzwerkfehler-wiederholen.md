---
id: infrastructure-awstw
title: Die Abfrage der Lena-Flag-Farbe (getLenaFlagSettings) läuft an allen drei Aufrufstellen über Retry.reading, damit ein Firefox-Abbruch mit Http error (0) keinen Fehler-Toast mehr auslöst.
status: done
type: bug
context: infrastructure
created: 2026-10-29
completed: 2026-10-30
depends_on: []
blocks: []
tags: [retry, firefox, http-status-0, lena-flag, settings]
related_adrs: []
related_research: []
prior_art: [infrastructure-y6atr]
---

## Why
Roman sieht auf der Settings-Seite ab und zu (Firefox) den Toast
"Failed to load Lena flag colour: Http error (0) from server occured while making request
to /api/SettingsApi/getLenaFlagSettings". Aufgefallen ist es, solange die Flag-Farbe noch
nicht gewählt ist.

Der leere Zustand ist dabei nicht die Ursache: `getLenaFlagSettings`
(`src/Server/Ledger/QueryHandlers.fs`) liefert ohne gespeicherte Farbe
`{ SavedColor = None; SuggestedColor = ... }`, das ist in `LedgerLenaFlagTests.fs` getestet.
Status 0 heißt, der Browser hat die Anfrage abgebrochen, bevor ein Status ankam. Das ist
der Firefox-Befund vom 2026-10-24, für den infrastructure-y6atr den Helfer `Retry.reading`
(`src/Client/Retry.fs`) gebaut hat. y6atr hat nur die vier Abfragen des Kassenbuch-Starts
umhüllt. `getLenaFlagSettings` läuft direkt neben dem umhüllten `getSettings`, aber ohne
Wiederholung.

## What
`Api.settings.getLenaFlagSettings` läuft an allen Aufrufstellen über `Retry.reading`:

- `src/Client/Components/Settings/State.fs` (`LoadLenaFlagSettings`, zeigt den Toast)
- `src/Client/Components/Ledger/State.fs` (`LoadLenaFlagSettings`, Fehler wird still verworfen,
  dann fehlt die Aktion "Lena fragen")
- `src/Client/Components/SyncFlow/State.fs` (Laden der Flag-Farbe für "Lena fragen beim Import")

Die Abfrage ist rein lesend, eine Wiederholung ist also unbedenklich. Am Helfer selbst ändert
sich nichts.

## Acceptance criteria
- [x] Der Quelltext-Test in `src/Tests/RetryTests.fs` (der heute die vier y6atr-Aufrufe per
      `Expect.stringContains ... "Retry.reading Api...."` prüft) prüft zusätzlich, dass
      Settings-, Ledger- und SyncFlow-State `Api.settings.getLenaFlagSettings` über
      `Retry.reading` aufrufen. Der Test scheitert vor der Änderung und läuft danach grün.
- [x] Kein ungeschützter Aufruf bleibt: im Client-Quelltext steht
      `Api.settings.getLenaFlagSettings` nur noch hinter `Retry.reading` (Test zählt die
      Vorkommen ohne den Präfix und erwartet 0).
- [x] Der Test trägt einen Kommentar zum Befund (Toast "Failed to load Lena flag colour:
      Http error (0)", Firefox, 2026-10-29) und verweist auf infrastructure-y6atr.
- [x] Der Fehler-Toast bei echtem Fehlschlag (alle drei Versuche scheitern) bleibt
      unverändert.
- [x] `dotnet build` und `dotnet test` grün.

## Notes
- Die Fable.Remoting-Proxys sind unter .NET nicht ausführbar (siehe y6atr), deshalb prüfen
  die Tests die Verdrahtung im Quelltext, das Verhalten des Helfers selbst ist in
  `RetryTests.fs` schon abgedeckt.
- Die eigentliche Ursache der Firefox-Abbrüche (Verdacht Service Worker, ADR 0057) bleibt
  offen. Dieses Ticket dämpft nur die Folge an einer weiteren Stelle.
- Weitere lesende Startabfragen ohne `Retry.reading` gibt es vermutlich noch. Wer hier
  arbeitet, darf sie im Abschlussbericht als Folgeticket nennen, aber nicht in diesem Ticket
  mit umbauen.

## Verifier note (iteration 1)

REASONS:
- Check 4 (Ubiquitous language): `Retry.reading` wird entgegen der README-Definition benutzt. `.agentheim/knowledge/contexts/infrastructure/README.md:61` ("Lesende Abfrage mit Wiederholung") endet mit "Gilt für die vier Start-Abfragen des Kassenbuchs." Der Diff ergänzt `getLenaFlagSettings` an drei Aufrufstellen, zwei davon außerhalb des Kassenbuch-Starts. `readmeDelta` ist `[]`, der Satz wäre nach der Integration falsch.
- Checks 1 bis 3 bestanden, `./verify.sh` VERIFY: PASS (dotnet 2571, node 319). Checks nach 4 liefen nicht.

SUGGESTED_FIX: Ein `README_DELTA` mit einem `replace` auf das Bullet "Lesende Abfrage mit Wiederholung", dessen letzter Satz die Lena-Flag-Farbe (Settings, Ledger, SyncFlow) neben den vier Start-Abfragen nennt oder die feste Liste weglässt. Keine Änderung an Quelltext oder Tests. Das Delta vorher mit `npm run check:readme-delta -- --bc infrastructure --delta <datei|->` prüfen.

ITERATION_HINT: likely-fixable

## Outcome

`Api.settings.getLenaFlagSettings` läuft jetzt in `src/Client/Components/Settings/State.fs`, `src/Client/Components/Ledger/State.fs` und `src/Client/Components/SyncFlow/State.fs` über `Retry.reading`. Die Fehlerzweige der `Cmd.OfAsync.either`-Aufrufe sind unverändert: Scheitern alle drei Versuche, kommt der Toast auf der Settings-Seite wie bisher, Ledger und SyncFlow bleiben still. Am Helfer selbst hat sich nichts geändert.

Tests in `src/Tests/RetryTests.fs`: Neu ist "die Lena-Flag-Farbe wird an allen Aufrufstellen über den Helfer geladen" (Kommentar nennt den Befund Firefox, Toast "Failed to load Lena flag colour: Http error (0)", 2026-10-29, und verweist auf infrastructure-y6atr). Neu ist auch "im Client steht kein Aufruf der Lena-Flag-Farbe ohne den Helfer": Er zählt die Vorkommen ohne Präfix in allen `*.fs` unter `src/Client` (ohne bin, obj, fable_modules) und erwartet 0, mit eingebautem Gegenbeweis für die Zählung. Der bestehende Test zu den Start-Abfragen hieß "die vier ..." und führt jetzt `getLenaFlagSettings` in der erwarteten Liste mit, sonst wäre sein Abgleich rot; der Name lautet nun "die lesenden Start-Abfragen laufen über den Helfer, kein Schreibaufruf". Vor dem Fix waren alle drei Tests rot (3 Fehlschläge), danach grün.

Hinweis für ein mögliches Folgeticket: Es gibt weitere lesende Aufrufe ohne `Retry.reading`, im Client grob 17 unterschiedliche Datei-Abfrage-Paare mit `get`, `list` oder `load` im Namen, darunter `Api.settings.getSettings` in Ledger/State.fs (Zeile 2107) und SyncFlow/State.fs (Zeilen 831, 858, 883). Die Zahl stammt aus einem Grep und ist nicht einzeln geprüft. Die Ursache der Firefox-Abbrüche bleibt offen.

Läufe: `./verify.sh` ergab VERIFY: PASS (dotnet 2571 grün, node 319 grün, dotnet build, npm run build, guard:classes, guard:tokens grün).
