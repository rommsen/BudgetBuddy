---
id: ynab-sync-k3vq8
title: "Barabhebung in Import prüfen: Kontenliste bleibt still leer, das Bar-Konto ist nicht vorbelegt und im Picker gibt es keine Konten"
status: todo
type: bug
context: ynab-sync
created: 2026-10-31
completed:
depends_on: [design-system-001]
blocks: []
tags: [import, split, barabhebung, picker, regression]
related_adrs: [0004, 0006]
related_research: []
prior_art: [ynab-002, design-system-bym4m, ledger-s8t3s]
---

## Why

Roman, 2026-10-31: Der Chip "Barabhebung" in Import prüfen öffnet das Aufteilen-Sheet, aber die zweite Zeile ist leer statt mit dem Bar-Konto belegt, und im Picker lässt sich kein Konto wählen. Eine EC-Zahlung mit Bargeld-Anteil lässt sich damit in BB nicht mehr als Übertrag aufteilen.

## What

Beide Symptome haben eine gemeinsame Ursache im Code: `model.Accounts` ist im SyncFlow leer. `StartCashbackSplit` findet das Quick-Add-Konto dann nicht (`openOnBudgetAccounts model.Accounts`), und `CategoryPicker` zeigt ohne Konten keinen Abschnitt "Übertrag auf Konto" und keine Konto-Treffer.

Der Ladeweg `LoadAccounts` (`src/Client/Components/SyncFlow/State.fs:841`) hat drei Schwächen:

- Er ruft `Api.ynab.getBudgetDetails`, das heißt `GET /budgets/{id}` mit dem kompletten Budget samt aller Buchungen.
- Jeder Fehler wird zu `AccountsLoaded ([], ...)` und verschwindet ohne Meldung. Auch wenn der Abruf von Einstellungen scheitert, geht zusätzlich die Quick-Add-Id verloren.
- Er läuft nur einmal in `init`, nicht in `pageLoadCmd` (`src/Client/State.fs:183`). Ein Fehler beim App-Start bleibt bis zum Neuladen bestehen.

**Hauptursache, Stand 2026-10-31:** Firefox bricht lesende Abfragen beim Laden mit "Http error (0)" ab (Befund 2026-10-24, `src/Client/Retry.fs`). Das Kassenbuch ist seit 05536fd über `Retry.reading` geschützt, der SyncFlow nicht. Alle lesenden Aufrufe in `SyncFlow/State.fs` (`getCurrentSession`, `getTransactions`, die drei `getSettings`, `getCategories`, `getPayees`, der Konten-Abruf) laufen über `Retry.reading`. `getSettings` steht vor Kategorien, Konten und Payees: Scheitert es, fehlen die Daten dahinter.

Der Import zieht auf denselben Weg wie das Kassenbuch um: `Api.ledger.getBudgetAccounts` (`GET /budgets/{id}/accounts`, 5 Minuten serverseitig gecacht, Fehlertext statt Schweigen). `getSettings` bleibt für die Quick-Add-Id.

Der Import lädt die Konten beim Seitenaufruf und erneut beim Öffnen des Split-Sheets, solange sie nicht geladen sind (Vorbild `Ledger/State.fs:2068`). Ein Fehlschlag erscheint als Toast mit dem Fehlertext. Kommt die Liste erst nach dem Tipp auf "Barabhebung" an, belegt sie die noch leere Übertrag-Zeile nachträglich. Eine vom Nutzer gesetzte Zeile bleibt unangetastet.

## Acceptance criteria

- [ ] Kaputt-Szenario, GIVEN `getBudgetDetails` scheitert und `getBudgetAccounts` liefert die Konten, WHEN der Chip "Barabhebung" getippt wird, THEN zeigt Zeile 2 "→ Bar" (Quick-Add-Konto). Der Browser-Wächter ist vor dem Fix rot und nennt im Kommentar diesen Bug.
- [ ] Das Sheet zeigt im Picker den Abschnitt "Übertrag auf Konto", die Suche "Bar" findet "→ Bar", die Wahl setzt den Übertrag (Wächter `scripts/import-split-target.test.mjs` auf `getBudgetAccounts` umgestellt).
- [ ] Intakt-Szenario: Ohne Quick-Add-Konto bleibt Zeile 2 leer und das Konto ist im Picker wählbar.
- [ ] Kaputt-Szenario Abbruch, GIVEN der erste `getSettings`- oder `getTransactions`-Aufruf bricht ab (`route.abort`, Status 0), WHEN die Seite lädt, THEN erscheinen Buchungen, Kategorien und Konten ohne Neuladen und ohne Fehlerbanner. Der Browser-Wächter ist vor dem Fix rot.
- [ ] Alle lesenden Aufrufe in `SyncFlow/State.fs` laufen über `Retry.reading`, schreibende nicht.
- [ ] Scheitert das Laden der Kategorien auch nach den Wiederholungen, erscheint ein Toast, statt dass der Picker still leer bleibt.
- [ ] Der Client ruft `getBudgetDetails` im SyncFlow nicht mehr auf.
- [ ] Scheitert `getBudgetAccounts`, erscheint ein Toast mit dem Fehlertext; die Quick-Add-Id bleibt erhalten, wenn nur die Konten scheitern.
- [ ] Scheitert der erste Abruf, lädt das Öffnen des Split-Sheets die Konten neu, ohne dass die App neu geladen werden muss (`update` unter .NET, Mapping als reine Funktion, soweit `Client.Api` im Weg ist).
- [ ] Kommt die Liste nach dem Tipp auf "Barabhebung" an, ist die leere Übertrag-Zeile danach belegt; eine vom Nutzer gesetzte Zeile ändert sich nicht.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes

**Belege (Roman, 2026-10-31):** Im Kassenbuch funktioniert die Barabhebung. Im Import kommt nach Reloads wiederholt "Http error (0)" bei `getTransactions`, die Seite lädt erst nach mehreren Versuchen, und dann fehlen je nach Lauf Konten oder Kategorien und Konten. Der Browser ist Firefox. Das passt zu einzelnen Abbrüchen je Abfrage. Eine frühere Notiz hatte den Startfehler verworfen, weil ein Reload gelang: Das war falsch, jede Abfrage kann unabhängig scheitern.

**Nicht bewiesen:** warum Firefox abbricht. Ob auch ein Fehler speziell in `GET /budgets/{id}` mitspielt, ist offen. `Retry.reading` wiederholt nur zweimal über 1,6 Sekunden; reicht das nicht, ist die Ursache des Abbruchs ein eigener Task. Die Produktion wurde nicht geprüft.

Die Meldung nennt diesen Fall "Database error during load", obwohl keine Antwort kam. Das ist irreführend, aber nicht Teil dieses Tasks.

`YnabApi.getBudgetDetails` bleibt als Endpoint bestehen, Aufräumen ist nicht Teil dieses Tasks. `YnabClient.getBudgetWithAccounts` nutzt der Verbindungstest weiter.

Fable.Remoting-Fixtures mitziehen. Die Fixture mockt heute alle unbekannten `/api/**` mit 500, `LedgerApi/getBudgetAccounts` braucht also einen eigenen Mock.

Der offene Punkt aus design-system-bym4m (im Import ist das Konto der Buchung selbst wählbar) bleibt außerhalb dieses Tasks.
