---
id: ledger-bk3q8
title: "Quick Add und Bob über einen gemeinsamen Barbuchungs-Befehl, Push-Bau als reine Funktion; ein Kategorien-Ladefehler bucht nicht mehr still ohne Kategorie"
status: done
type: bug
context: ledger
created: 2026-11-07
completed:
depends_on: [ledger-kfd5m]
blocks: [infrastructure-vp308]
tags: [ledger, quick-add, bot, push, ynab, bestandsreview]
related_adrs: [0021, 0004, 0016, 0066, 0068, 0044]
related_research: []
prior_art: [ledger-tdnpk, ledger-b0b7q, ledger-cks06, ledger-q4m7z, ledger-z71ka]
---

## Why
Bestandsreview ledger vom 2026-11-06, Gruppe 2 (`.agentheim/knowledge/reviews/2026-11-06-ledger.md`, Abschnitt 2, höchste Schwere hoch). Roman hat am 2026-11-07 die Option "Reine Push-Funktion plus gemeinsamer Barbuchungs-Befehl" gewählt.

Was heute passiert:
- **Kategorie geht still verloren.** Quick Add lädt die Kategorien bei YNAB, um zur gewählten Id den Namen zu finden (`src/Server/Api.fs:474` bis `:485`). Scheitert der Abruf (etwa Rate-Limit) oder fehlt die Id in der Antwort, wird daraus `None`: Im Kassenbuch steht die Buchung ohne Kategorie, an YNAB geht sie mit `CategoryId`. Beispiel: Roman trägt 12,50 € in der Kategorie "Kaffee" ein, YNAB meldet beim Laden ein Rate-Limit; YNAB hat die Kategorie, das Kassenbuch nicht. Kein Hinweis.
- **Push-Bau ungetestet.** Wie aus einer Kassenbuch-Buchung der YNAB-Request für "Erneut senden" wird (Betrag ohne Vorzeichen, `IsOutflow`, Kategorie, ImportId, Flag, Konto ohne YNAB-Verknüpfung), steht inline in `entryPush` (`Api.fs:1381`). Ein vertauschtes Vorzeichen machte aus einer Ausgabe in YNAB eine Einnahme, kein Test schlüge an.
- **Dreifach kopiert.** Kontoauflösung des Quick-Add-Kontos, Vorzeichen und Request-Bau stehen in `addManualTransaction` (`Api.fs:433`) und als Kopie bei Bob (`src/Server/BotApi.fs:374` `bookWithinLimit`, `:536` `ResolveTarget`, `:551` `Push`). Eine Änderung an einer Kopie vergisst die andere.

## What
Am Code vom 2026-11-07 (main 7423fb0) geprüft.

1. **Reine Funktion für den Push einer Buchung** (aus `entryPush`, `Api.fs:1381` bis `:1418`) in `Ledger.CommandHandlers` oder einem eigenen Modul: aus `Entry`, den Konten und den Verknüpfungen entweder der Request (`ManualTransactionRequest` plus ImportId plus Flag-Farbe) und das YNAB-Konto, oder der Grund `pushNotPossibleReason`. `entryPush` in `Api.fs` bindet danach nur noch Token, Budget und `YnabClient.createTransactionWithFlag` an.
2. **Gemeinsamer Barbuchungs-Befehl** in `Ledger.CommandHandlers` für Quick Add und Bob. Er nimmt eine fachliche Eingabe (Konto, Datum, Betrag mit Richtung, Payee, Memo, Kategorie als `CategoryRef option`), bildet daraus `CashExpenseInput` **und** den YNAB-Request aus denselben Werten und ruft `addCashExpense` (mit der Sperre aus ledger-kfd5m). So können Kassenbuch und YNAB-Request nicht mehr auseinanderlaufen. Vorzeichen, Trimmen von Payee und Memo und das Weglassen leerer Werte stehen nur noch dort.
3. **Kontoauflösung des Quick-Add-Kontos** (Setting `ynab_quickadd_account_id`, Guid, `findAccountByYnabAccountId`) als eine Lesefunktion in `Ledger.QueryHandlers` mit festen deutschen Fehlersätzen. Quick Add (`Api.fs:445` bis `:472`) und Bobs `ResolveTarget` (`BotApi.fs:536`) rufen sie. Bobs Ablauf Vorschau und Commit (ADR 0066) bleibt: aufgelöst wird bei der Vorschau.
4. **Kategorie bei Quick Add**: Die Id wird vor dem Ereignis zu `CategoryRef` aufgelöst. Gelingt das nicht, bricht Quick Add mit einer konkreten deutschen Meldung ab: kein Ereignis, kein Push. Woher der Name kommt, siehe "Entscheidung Roman". Eine Id, die es nicht gibt, wird abgelehnt ("Die Kategorie gibt es in YNAB nicht mehr").
5. **API-Adapter-Tests für das fehlende Kassenbuch-Konto**: Quick Add (`Api.fs:465`), `importToYnab` (`Api.fs:1017`) und `forceImportDuplicates` (`Api.fs:1166`) melden ohne gespiegeltes Konto einen Fehler, schreiben kein Ereignis und pushen nicht.
6. **`LedgerRetryPushTests.fs`** (:71): Der Auslöser `triggerMirror` wird mitgezählt (einmal bei Erfolg, nie bei Fehlschlag), und der Actor der Ereignisse ist `UserInBb`.

## Acceptance criteria
- [x] Kaputt (Kategorie), Handler-Ebene: GIVEN Quick Add mit einer Kategorie-Id, WHEN das Auflösen des Namens scheitert, THEN kein `CashExpenseAdded`, kein Push, ein Fehler mit deutschem Satz. Vor dem Fix rot (heute entsteht die Buchung ohne Kategorie und der Push trägt die Id). Der Kommentar nennt den Bug.
- [x] Kaputt (Kategorie unbekannt): GIVEN eine Id, die die Kategorienquelle nicht kennt, THEN Ablehnung ohne Ereignis und ohne Push. Vor dem Fix rot.
- [x] Intakt: Quick Add mit gültiger Kategorie schreibt `CashExpenseAdded` mit Id und Namen, der Push-Request trägt dieselbe Id; ohne Kategorie stehen beide ohne Kategorie. Eine Ausgabe geht mit negativem Betrag ins Kassenbuch und mit `IsOutflow = true` und positivem Betrag an YNAB, eine Einnahme umgekehrt.
- [x] Reine Push-Funktion, Domain-Ebene, je ein Test: Ausgabe (Betrag ohne Vorzeichen, `IsOutflow = true`), Einnahme (`IsOutflow = false`), ohne Kategorie (`CategoryId = None`), ohne Payee (leerer Text), mit ImportId aus den Verknüpfungen (auch nach einer Ersetzung, `currentImportId`), mit Flag-Farbe, Konto ohne YNAB-Verknüpfung (`pushNotPossibleReason`, kein Request).
- [x] Gegenprobe im Outcome: Mutanten "Vorzeichen in der Push-Funktion vertauscht", "`abs` entfernt", "Kategorie nicht übernommen", "Flag-Farbe nicht übernommen", je mit dem Test, der rot wird.
- [x] Der gemeinsame Barbuchungs-Befehl hat Tests für dieselben Fälle wie die heutigen Wege (Doktrin Punkt 4): Konto fehlt im Kassenbuch, Push gelingt (Verknüpfung), Push scheitert (`EntryPushToYnabFailed`, Buchung bleibt), Actor `QuickAdd` bzw. `Bob`, vorgegebene `entryId` gilt auch nach einem Push-Fehler (ledger-z71ka).
- [x] Bob ruft den gemeinsamen Befehl. `BotApiTests.fs` und `BotValidationTests.fs` bleiben ohne inhaltliche Änderung grün; Grenzen nach ADR 0068 (500 € je Buchung, Tagessumme, 10 Commits, 31 Tage, Rate-Limit) und `bookingGate` sind unverändert.
- [x] Die Live-Verdrahtung ist belegt (Doktrin Punkt 3): `addManualTransaction`, `entryPush`, `BotApi.liveDeps` (`ResolveTarget`, `Push`, `AddCashExpense`) schließen die neuen Funktionen an; Liste im Outcome, je Stelle Verhalten oder Strukturwächter mit Gegenbeweis.
- [x] Ohne gespiegeltes Kassenbuch-Konto: Quick Add, `importToYnab` und `forceImportDuplicates` liefern einen Fehler, schreiben kein Ereignis und rufen YNAB nicht schreibend auf (je ein Test am API-Adapter oder, wo der Adapter sonst live aufriefe, an einer Funktion mit Port). Ein Fehler im Code darf in diesen Tests keinen echten YNAB-Aufruf auslösen (Doktrin Punkt 12, `NetworkCallGuardTests.fs` grün).
- [x] `LedgerRetryPushTests.fs`: Auslöser einmal bei Erfolg, nie bei Fehlschlag und nie bei Ablehnung; Actor `UserInBb` auf `EntryLinkedToYnab` und `EntryPushToYnabFailed`.
- [x] Endpoint-Negativtabelle (`RemotingEndpointNegativeTests.fs`): die Zeile für `YnabApi.addManualTransaction` deckt auch eine unbekannte Kategorie-Id ab.
- [x] Keine Änderung an Records in `src/Shared`. Falls doch nötig: Fixtures in `scripts/*.test.mjs` im selben Schritt nachgezogen.
- [x] README ledger: *BuchungÜbernehmen* beschreibt den gemeinsamen Befehl, die Kategorie-Auflösung und den Abbruch; README-Delta mit `npm run check:readme-delta -- --bc ledger` geprüft.
- [x] `./verify.sh` zeigt `VERIFY: PASS`.

## Entscheidung Roman (2026-11-07)
Roman wählte am 2026-11-07 B (AskUserQuestion): Name aus den gespiegelten Kategorie-Stammdaten, YNAB nur als Rückfall, wenn die Id dort fehlt; abbrechen nur, wenn auch das scheitert.

Die Optionen, wie sie zur Wahl standen:
**Woher kommt bei Quick Add der Kategoriename?** Das ändert, ob Quick Add bei einem YNAB-Ausfall bucht.

- A: Wie heute bei YNAB laden; scheitert der Abruf, bricht Quick Add mit "Kategorien konnten nicht geladen werden, bitte erneut versuchen" ab. So lautet die Empfehlung im Review.
- B: Den Namen aus den gespiegelten Kategorie-Stammdaten nehmen (`ledger_categories`, `Ledger.Persistence.loadCategoryNames`, ADR 0044). Kein YNAB-Abruf je Quick Add, ein Rate-Limit beim Laden kann die Buchung nicht verhindern. Fehlt die Id dort (Kategorie in YNAB neu, noch nicht gespiegelt), erst bei YNAB nachfragen und nur dann abbrechen, wenn auch das scheitert.

Empfehlung: B. Quick Add spart einen YNAB-Aufruf und bucht auch bei YNAB-Schluckauf mit Kategorie. Der Name ist ohnehin nur Beschriftung, der Lesepfad zeigt seit ADR 0044 den heutigen Namen aus denselben Stammdaten. Ohne Antwort gilt A, weil es die kleinere Änderung ist.

## Nicht dazu
- Die Schreib-Sperre selbst (ledger-kfd5m) und der Fall "Push gelungen, Verknüpfung nicht geschrieben" (ledger-p3vq8).
- Die Umbenennung von `addCashExpense` (bucht auch Einnahmen) und `BotCommitResult` als DU: Kann-Sammlung ledger-k4n2s, Punkt 2. Der neue Befehl bekommt gleich einen Namen aus der Fachsprache (Barbuchung), `addCashExpense` bleibt darunter.
- Feste Fehlermeldungen an Bob statt YNAB-Rohtext und deutsche YNAB-Fehlertexte: infrastructure-vp308.
- Die kleineren Punkte aus Gruppe 2: ein gemeinsamer `withYnabContext` für die neun Push-Bauer (`Api.fs:1291` bis `:1513`, Vorbild `BotApi.fs:508`), `budgetAccountsCache` und `withTrackingLocks` nach `Ledger.QueryHandlers` (`Api.fs:1470`, `:1495`), `loadDailyUsage` über QueryHandlers (`BotApi.fs:129`). Sie bleiben offen; der Conductor nimmt sie beim Abschluss in die Kann-Sammlung, sofern der Worker sie nicht ohnehin mitnimmt.
- Quick-Add-Oberfläche und Vorlagen (`getRecentQuickAddTemplates`).

## Notes
- Reihenfolge: nach ledger-kfd5m, weil beide `addCashExpense` anfassen und der gemeinsame Befehl die Sperre dort voraussetzt. Der gemeinsame Befehl nimmt die Sperre nicht selbst, `addCashExpense` hält sie (keine Verschachtelung).
- Vor dem Umzug zuerst die heutigen Wege per Test festhalten (Quick Add und Bob laufen täglich), dann umziehen.
- Bob löst die Kategorie bei der Vorschau aus dem Kategorien-Cache auf (`ledger-bbkgs`) und hat den stillen Verlust nicht. Er bekommt den gemeinsamen Befehl wegen der Kopien.
- Keine echten Beträge, Kategorien oder Kontonamen in Tests, Doku oder Commits.

## Outcome

Quick Add und Bob gehen über einen gemeinsamen Barbuchungs-Befehl, der Push einer Kassenbuch-Buchung ist eine reine Funktion, und Quick Add verliert die Kategorie nicht mehr still.

**Produktionscode**
- `src/Server/Ledger/CommandHandlers.fs`:
  - `planEntryPush accounts links entry` (rein, aus `Server.Api.entryPush`) liefert `EntryPushPlan` (YNAB-Konto, `ManualTransactionRequest`, `import_id` über `currentImportId`, Flag-Farbe über `pushFlagColorOf`) oder `pushNotPossibleReason`.
  - `CashBooking` (Betrag ohne Vorzeichen, `IsOutflow`, Payee und Memo roh), `cashBookingParts` (rein: `CashExpenseInput` und Request aus denselben Werten, Vorzeichen, Trimmen und das Weglassen leerer Werte nur hier).
  - `AddCashExpense` (Port-Typ), `addCashBookingWith` und `addCashBooking`. Gebucht wird über `addCashExpense`, das die Schreib-Sperre hält (ledger-kfd5m). Der neue Befehl nimmt die Sperre nicht selbst.
  - `addQuickAdd`: löst die Kategorie-Id vor dem Ereignis zu `CategoryRef` auf. Scheitert das: `RefusedByBb` mit dem Satz der Auflösung, kein Ereignis, kein Push.
- `src/Server/Ledger/QueryHandlers.fs`: `resolveQuickAddAccountWith setting` und `resolveQuickAddAccount ()` (Setting `ynab_quickadd_account_id`, Guid, `findAccountByYnabAccountId`) mit festen Sätzen `quickAddAccountMissingText`, `quickAddAccountInvalidText`, `quickAddAccountNotMirroredText`. `resolveCategoryRefWith mirroredNames fromYnab id`: Name aus `ledger_categories`, YNAB nur, wenn die Id dort fehlt. Sätze: `categoryLoadFailedText` ("Die Kategorie konnte nicht geladen werden, bitte erneut versuchen"), `categoryUnknownText` ("Die Kategorie gibt es in YNAB nicht mehr"). Entscheidung Roman B umgesetzt.
- `src/Server/Api.fs`: `QuickAddPorts`, `addManualTransactionWith` (Token, Budget, Quick-Add-Konto, Kategorie-Auflösung mit `Ledger.Persistence.loadCategoryNames` und YNAB als Rückfall, dann `addQuickAdd`), `liveQuickAddPorts`. Der Endpoint prüft weiter an der API-Grenze und ruft dann `addManualTransactionWith liveQuickAddPorts DateTime.UtcNow validRequest`. `entryPushWith setting create` bindet nur noch Token, Budget und `createTransactionWithFlag` an `planEntryPush`. `importTargetWith setting count` liefert Token, Budget, YNAB-Konto und Kassenbuch-Konto für `importToYnab` und `forceImportDuplicates` (dieselbe Meldung wie vorher, wenn das gespiegelte Konto fehlt).
- `src/Server/BotApi.fs`: `bookWithinLimit` baut eine `CashBooking` und ruft `addCashBookingWith deps.AddCashExpense Bob …`. `BotDeps.Push` bekommt jetzt `(YnabAccountId * ManualTransactionRequest)` statt der Vorschau, die Request-Kopie in `liveDeps.Push` ist weg. `liveDeps.ResolveTarget` ruft `Ledger.QueryHandlers.resolveQuickAddAccount`. `bookingGate`, Vorschau und Commit (ADR 0066) und alle Grenzen nach ADR 0068 sind unverändert. Bobs Fehlersätze beim Quick-Add-Konto lauten jetzt wie bei Quick Add (fester Satz der Lesefunktion).
- Keine Änderung an `src/Shared`, keine Fixture-Anpassung nötig.

**Tests** (neu `src/Tests/LedgerCashBookingTests.fs`, 31 Fälle; dazu 2 in `LedgerRetryPushTests.fs`, 2 in `StructureGuardTests.fs`; eine Zeile in `RemotingEndpointNegativeTests.fs` erweitert):
- Reine Push-Funktion, je ein Test: Ausgabe (ohne Vorzeichen, `IsOutflow = true`), Einnahme, ohne und mit Kategorie, ohne Payee (leerer Text), `import_id` aus den Verknüpfungen auch nach Ersatz (`:r1`), Flag-Farbe, Konto ohne YNAB-Verknüpfung (`pushNotPossibleReason`).
- Barbuchung rein: Ausgabe negativ/positiv mit `IsOutflow`, Einnahme, Trimmen und leere Werte, Kategorie mit Name im Kassenbuch und derselben Id im Request.
- Gemeinsamer Befehl (Doktrin Punkt 4, Vorlage `addCashExpense`): Push gelingt (Verknüpfung, Actor `Bob`, vorgegebene Id, Request der Barbuchung), Push scheitert (`EntryPushToYnabFailed`, Buchung bleibt unter der vorgegebenen Id, Actor `QuickAdd`). "Konto fehlt im Kassenbuch" prüfen die Lesefunktion (`resolveQuickAddAccountWith`, vier Fälle) und der Adapter-Test.
- Kaputt (Handler-Ebene, `addQuickAdd`): Ladefehler der Kategorie und unbekannte Id ergeben je `RefusedByBb` mit deutschem Satz, kein `CashExpenseAdded`, kein Push. Vor dem Fix rot: mit der alten Abbildung "Fehler wird ohne Kategorie" (Mutante unten) sind beide Tests rot. Intakt: mit Kategorie Id und Name im Kassenbuch und dieselbe Id im Push, Ausgabe negativ/Abfluss; ohne Kategorie beide ohne, Einnahme positiv/Zufluss.
- Kategorie-Auflösung: Stammdaten ohne YNAB-Abruf, Rückfall auf YNAB, Ladefehler, unbekannt.
- Adapter mit Ports: Quick Add ohne gespiegeltes Konto (Fehler, kein Ereignis, kein Push); Quick Add mit gespiegeltem Konto und Kategorie in `ledger_categories` (Name aus den Stammdaten, kein YNAB-Kategorienabruf, Push auf das Quick-Add-Konto); `importTargetWith` ohne gespiegeltes Konto (Fehler mit der Zahl der Buchungen, vor jedem Schreiben und YNAB-Aufruf); `entryPushWith` sendet genau den Plan von `planEntryPush`. Alle YNAB-Aufrufe sind Fälschungen, kein Fall erreicht YNAB.
- Bob: Commit über den echten Handler mit zählendem Push: Ausgabe negativ im Kassenbuch, ein Push auf das YNAB-Konto der Vorschau mit Betrag, `IsOutflow`, Kategorie-Id und Payee. Live `ResolveTarget` liefert einen Satz der Lesefunktion.
- `LedgerRetryPushTests.fs`: Auslöser einmal bei Erfolg, nie bei Fehlschlag, nie bei Ablehnung; Actor `UserInBb` auf `EntryLinkedToYnab` und `EntryPushToYnabFailed`.
- `RemotingEndpointNegativeTests.fs`: die Zeile `YnabApi.addManualTransaction` deckt auch eine unbekannte Kategorie-Id ab (ohne Einrichtung endet der Fall vor der Kategorie, die Auflösung selbst prüfen die Port-Tests).
- `BotApiTests.fs` und `BotValidationTests.fs` sind nicht angefasst und grün.

**Gegenprobe** (Skript `scratchpad/mut/run.py`, je Mutante Build und Lauf der Listen "Ledger Push einer", "Ledger Barbuchung", "Ledger Quick", "Ledger Kategorie einer", "Bot echte", "Bot-Schnittstelle", "Bot Barbuchung", "Ledger Erneut senden (Noch"):
| Mutante | roter Test |
|---|---|
| vor dem Fix: Ladefehler der Kategorie wird zu "ohne Kategorie" | scheitert das Laden der Kategorie, bucht Quick Add nicht und pusht nicht; eine Kategorie-Id, die keine Quelle kennt, wird ohne Ereignis und ohne Push abgelehnt |
| Vorzeichen in der Push-Funktion vertauscht | eine Ausgabe geht mit Betrag ohne Vorzeichen und als Abfluss …; eine Einnahme geht als Zufluss |
| `abs` in der Push-Funktion entfernt | eine Ausgabe geht mit Betrag ohne Vorzeichen und als Abfluss … |
| Kategorie in der Push-Funktion nicht übernommen | eine Buchung ohne Kategorie geht ohne Kategorie, eine mit Kategorie mit deren Id |
| Flag-Farbe in der Push-Funktion nicht übernommen | die Flag-Farbe der Buchung geht mit |
| Vorzeichen in `cashBookingParts` vertauscht | Barbuchung rein (2), Befehl Push scheitert, Quick Add intakt (2), BotApiTests "Commit bucht die Vorschau mit Actor Bob, 50 Euro Ausgabe sind -50000 Milliunits" und "eine Einnahme (inflow) wird positiv gebucht" |
| Bob: Richtung vertauscht | die beiden BotApiTests oben |
| Bob: Push auf ein anderes Konto | Bobs Commit pusht den Request der Barbuchung auf das YNAB-Konto der Vorschau |
| Bob live: `ResolveTarget` ohne Lesefunktion | die echte Umgebung löst das Quick-Add-Konto über die gemeinsame Lesefunktion auf |
| Quick Add: Stammdaten nicht gelesen | Quick Add nimmt den Kategorienamen aus den gespiegelten Stammdaten … |
| `entryPushWith` verändert den Request | Erneut senden pusht den Request der reinen Push-Funktion … |
| Import: Kontoprüfung entfernt | Import-Push und erzwungener Import ohne gespiegeltes Kassenbuch-Konto … |
| Erneut senden: Auslöser auch nach Fehlschlag | der Spiegel-Auslöser läuft einmal nach Erfolg, nie nach Fehlschlag oder Ablehnung |
| Erneut senden: Actor `QuickAdd` bei der Verknüpfung | Verknüpfung und Fehlschlag aus Erneut senden tragen den Actor Roman in BB |
Für diese Liste gilt: kein Überlebender.

**Live-Verdrahtung** (Doktrin Punkt 3):
- `ynabApi.addManualTransaction` ruft `addManualTransactionWith liveQuickAddPorts DateTime.UtcNow validRequest`: Strukturwächter in `apiWirings` (`StructureGuardTests.fs`), Gegenbeweise "Quick Add mit anderen Ports", "Quick Add mit fester Uhr".
- `liveQuickAddPorts` (Settings, `YnabClient.getCategories`, `YnabClient.createManualTransaction`, `newEntryId`, `triggerMirrorInBackground`): Strukturwächter, je Feld ein Gegenbeweis. `Ledger.Persistence.loadCategoryNames` in `addManualTransactionWith`: Verhalten (Adapter-Test mit Stammdaten, Mutante oben).
- `entryPush ()` = `entryPushWith Persistence.Settings.getSetting YnabClient.createTransactionWithFlag`: Strukturwächter mit Gegenbeweisen "Erneut senden ohne YNAB", "ohne Settings"; `planEntryPush` in `entryPushWith`: Verhalten (Adapter-Test). `retryPushToYnab` im Endpoint ruft weiter `entryPush ()`.
- `importToYnab` und `forceImportDuplicates` rufen `importTargetWith Persistence.Settings.getSetting toImport.Length` bzw. `transactionsToForce.Length`: Strukturwächter mit Gegenbeweisen.
- `BotApi.liveDeps.ResolveTarget`: Verhalten ("die echte Umgebung löst das Quick-Add-Konto über die gemeinsame Lesefunktion auf", Mutante oben). `liveDeps.Push`: neuer Strukturwächter `isBotPushWired` mit Gegenbeweisen (anderes Konto, veränderter Request, kein Push, andere Einstellungen, nur im Kommentar). `liveDeps.AddCashExpense = addCashExpense`: Verhalten (bestehender Test "die echte Umgebung bucht über addCashExpense ins Kassenbuch"). `bookWithinLimit` ruft `addCashBookingWith`: Verhalten (Bob-Commit-Test und BotApiTests, Mutanten oben).

**Gate:** `./verify.sh` ergibt `VERIFY: PASS` (dotnet 4317 grün, 13 übersprungen aus den Listen "(requires .env)", node 316 grün). Log: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/verify-bk3q8.log`.

**README:** Das Delta ergänzt den Bullet **Barbuchung** in Ubiquitous language (geprüft mit `npm run check:readme-delta -- --bc ledger`, Exit 0). *BuchungÜbernehmen* in Key commands ist kursiv und per `replace` nicht erreichbar, deshalb von Hand:
- Alt: "Quick Add: `Ledger.CommandHandlers.addCashExpense` (Actor als Parameter: `QuickAdd` für die eigene Seite, `Bob` für die Bot-Schnittstelle, ledger-b0b7q) schreibt zuerst `CashExpenseAdded`"
  Neu: "Quick Add und Bob: der gemeinsame Barbuchungs-Befehl `Ledger.CommandHandlers.addCashBooking` (Bob über `addCashBookingWith`, ledger-bk3q8) bildet Kassenbuch-Eingabe und YNAB-Request aus denselben Werten und bucht über `addCashExpense` (Actor als Parameter: `QuickAdd` für die eigene Seite, `Bob` für die Bot-Schnittstelle, ledger-b0b7q). Quick Add (`addQuickAdd`) löst die Kategorie vorher auf, Name aus den Kategorie-Stammdaten, YNAB nur als Rückfall; scheitert das, bricht Quick Add ohne Ereignis und ohne Push ab. `addCashExpense` schreibt zuerst `CashExpenseAdded`"
- Alt: "Ohne ein gespiegeltes Kassenbuch-Konto für das konfigurierte Quick-Add-Konto (Spiegel nie gelaufen) lehnt `Server.Api` mit einer Fehlermeldung ab, statt ein zweites \"Bar\"-Konto zu erfinden (ADR 0016)."
  Neu: "Ohne ein gespiegeltes Kassenbuch-Konto für das konfigurierte Quick-Add-Konto (Spiegel nie gelaufen) lehnen Quick Add und Bobs Vorschau mit dem festen Satz aus `Ledger.QueryHandlers.resolveQuickAddAccount` ab, statt ein zweites \"Bar\"-Konto zu erfinden (ADR 0016)."
- Bullet *PushErneutSenden*, an das Ende: "Was hinausgeht, baut die reine Funktion `planEntryPush` (ledger-bk3q8)."

Nachtrag 2026-11-07 an ADR 0044 angehängt. Die drei Sätze zu den Key commands (*BuchungÜbernehmen* zweimal, *PushErneutSenden*) hat der Conductor von Hand eingearbeitet. Die Kann-Punkte stehen in ledger-k4n2s.
