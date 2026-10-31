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

Der Import zieht auf denselben Weg wie das Kassenbuch um: `Api.ledger.getBudgetAccounts` (`GET /budgets/{id}/accounts`, 5 Minuten serverseitig gecacht, Fehlertext statt Schweigen). `getSettings` bleibt für die Quick-Add-Id.

Der Import lädt die Konten beim Seitenaufruf und erneut beim Öffnen des Split-Sheets, solange sie nicht geladen sind (Vorbild `Ledger/State.fs:2068`). Ein Fehlschlag erscheint als Toast mit dem Fehlertext. Kommt die Liste erst nach dem Tipp auf "Barabhebung" an, belegt sie die noch leere Übertrag-Zeile nachträglich. Eine vom Nutzer gesetzte Zeile bleibt unangetastet.

## Acceptance criteria

- [ ] Kaputt-Szenario, GIVEN `getBudgetDetails` scheitert und `getBudgetAccounts` liefert die Konten, WHEN der Chip "Barabhebung" getippt wird, THEN zeigt Zeile 2 "→ Bar" (Quick-Add-Konto). Der Browser-Wächter ist vor dem Fix rot und nennt im Kommentar diesen Bug.
- [ ] Das Sheet zeigt im Picker den Abschnitt "Übertrag auf Konto", die Suche "Bar" findet "→ Bar", die Wahl setzt den Übertrag (Wächter `scripts/import-split-target.test.mjs` auf `getBudgetAccounts` umgestellt).
- [ ] Intakt-Szenario: Ohne Quick-Add-Konto bleibt Zeile 2 leer und das Konto ist im Picker wählbar.
- [ ] Der Client ruft `getBudgetDetails` im SyncFlow nicht mehr auf.
- [ ] Scheitert `getBudgetAccounts`, erscheint ein Toast mit dem Fehlertext; die Quick-Add-Id bleibt erhalten, wenn nur die Konten scheitern.
- [ ] Scheitert der erste Abruf, lädt das Öffnen des Split-Sheets die Konten neu, ohne dass die App neu geladen werden muss (`update` unter .NET, Mapping als reine Funktion, soweit `Client.Api` im Weg ist).
- [ ] Kommt die Liste nach dem Tipp auf "Barabhebung" an, ist die leere Übertrag-Zeile danach belegt; eine vom Nutzer gesetzte Zeile ändert sich nicht.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes

**Nicht bewiesen:** warum der Abruf in Produktion scheitert. Der Ladeweg ist seit ynab-002 (2026-06-13) unverändert, `budgetDetailDecoder` ebenfalls; die Kassenbuch-Umstellung hat diese Stelle nicht angefasst. Der Browser-Wächter aus design-system-bym4m ist grün, weil er `getBudgetDetails` mockt. Kandidaten sind Laufzeit oder Rate-Limit des vollen Budget-Abrufs, der mit dem Datenbestand wächst, oder ein anderer dauerhafter Fehler dieses Abrufs. Lokal lief kein Server, und gegen die Produktion wurde nichts abgefragt. Der Umstieg auf `/accounts` entfernt alle diese Kandidaten außer einem echten YNAB-Ausfall, und der wird durch den Toast sichtbar. Falls der Fehler nach dem Fix bleibt, nennt der Toast die Ursache.

**Beobachtung Roman, 2026-10-31:** Beim Neuladen scheiterte `getTransactions` einmal mit "Http error (0)" (keine Antwort vom Server), beim nächsten Neuladen lud die Seite wieder. Die Konten blieben auch danach leer. Der Kandidat "einmaliger Fehlschlag beim App-Start" ist damit als Ursache verworfen, der Abruf scheitert bei jedem Laden. Ob der Aussetzer damit zusammenhängt, ist offen; die Produktion wurde nicht geprüft.

Die Meldung nennt diesen Fall "Database error during load", obwohl keine Antwort kam. Das ist irreführend, aber nicht Teil dieses Tasks.

`YnabApi.getBudgetDetails` bleibt als Endpoint bestehen, Aufräumen ist nicht Teil dieses Tasks. `YnabClient.getBudgetWithAccounts` nutzt der Verbindungstest weiter.

Fable.Remoting-Fixtures mitziehen. Die Fixture mockt heute alle unbekannten `/api/**` mit 500, `LedgerApi/getBudgetAccounts` braucht also einen eigenen Mock.

Der offene Punkt aus design-system-bym4m (im Import ist das Konto der Buchung selbst wählbar) bleibt außerhalb dieses Tasks.
