---
id: ledger-bk3q8
title: "Quick Add und Bob über einen gemeinsamen Barbuchungs-Befehl, Push-Bau als reine Funktion; ein Kategorien-Ladefehler bucht nicht mehr still ohne Kategorie"
status: doing
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
- [ ] Kaputt (Kategorie), Handler-Ebene: GIVEN Quick Add mit einer Kategorie-Id, WHEN das Auflösen des Namens scheitert, THEN kein `CashExpenseAdded`, kein Push, ein Fehler mit deutschem Satz. Vor dem Fix rot (heute entsteht die Buchung ohne Kategorie und der Push trägt die Id). Der Kommentar nennt den Bug.
- [ ] Kaputt (Kategorie unbekannt): GIVEN eine Id, die die Kategorienquelle nicht kennt, THEN Ablehnung ohne Ereignis und ohne Push. Vor dem Fix rot.
- [ ] Intakt: Quick Add mit gültiger Kategorie schreibt `CashExpenseAdded` mit Id und Namen, der Push-Request trägt dieselbe Id; ohne Kategorie stehen beide ohne Kategorie. Eine Ausgabe geht mit negativem Betrag ins Kassenbuch und mit `IsOutflow = true` und positivem Betrag an YNAB, eine Einnahme umgekehrt.
- [ ] Reine Push-Funktion, Domain-Ebene, je ein Test: Ausgabe (Betrag ohne Vorzeichen, `IsOutflow = true`), Einnahme (`IsOutflow = false`), ohne Kategorie (`CategoryId = None`), ohne Payee (leerer Text), mit ImportId aus den Verknüpfungen (auch nach einer Ersetzung, `currentImportId`), mit Flag-Farbe, Konto ohne YNAB-Verknüpfung (`pushNotPossibleReason`, kein Request).
- [ ] Gegenprobe im Outcome: Mutanten "Vorzeichen in der Push-Funktion vertauscht", "`abs` entfernt", "Kategorie nicht übernommen", "Flag-Farbe nicht übernommen", je mit dem Test, der rot wird.
- [ ] Der gemeinsame Barbuchungs-Befehl hat Tests für dieselben Fälle wie die heutigen Wege (Doktrin Punkt 4): Konto fehlt im Kassenbuch, Push gelingt (Verknüpfung), Push scheitert (`EntryPushToYnabFailed`, Buchung bleibt), Actor `QuickAdd` bzw. `Bob`, vorgegebene `entryId` gilt auch nach einem Push-Fehler (ledger-z71ka).
- [ ] Bob ruft den gemeinsamen Befehl. `BotApiTests.fs` und `BotValidationTests.fs` bleiben ohne inhaltliche Änderung grün; Grenzen nach ADR 0068 (500 € je Buchung, Tagessumme, 10 Commits, 31 Tage, Rate-Limit) und `bookingGate` sind unverändert.
- [ ] Die Live-Verdrahtung ist belegt (Doktrin Punkt 3): `addManualTransaction`, `entryPush`, `BotApi.liveDeps` (`ResolveTarget`, `Push`, `AddCashExpense`) schließen die neuen Funktionen an; Liste im Outcome, je Stelle Verhalten oder Strukturwächter mit Gegenbeweis.
- [ ] Ohne gespiegeltes Kassenbuch-Konto: Quick Add, `importToYnab` und `forceImportDuplicates` liefern einen Fehler, schreiben kein Ereignis und rufen YNAB nicht schreibend auf (je ein Test am API-Adapter oder, wo der Adapter sonst live aufriefe, an einer Funktion mit Port). Ein Fehler im Code darf in diesen Tests keinen echten YNAB-Aufruf auslösen (Doktrin Punkt 12, `NetworkCallGuardTests.fs` grün).
- [ ] `LedgerRetryPushTests.fs`: Auslöser einmal bei Erfolg, nie bei Fehlschlag und nie bei Ablehnung; Actor `UserInBb` auf `EntryLinkedToYnab` und `EntryPushToYnabFailed`.
- [ ] Endpoint-Negativtabelle (`RemotingEndpointNegativeTests.fs`): die Zeile für `YnabApi.addManualTransaction` deckt auch eine unbekannte Kategorie-Id ab.
- [ ] Keine Änderung an Records in `src/Shared`. Falls doch nötig: Fixtures in `scripts/*.test.mjs` im selben Schritt nachgezogen.
- [ ] README ledger: *BuchungÜbernehmen* beschreibt den gemeinsamen Befehl, die Kategorie-Auflösung und den Abbruch; README-Delta mit `npm run check:readme-delta -- --bc ledger` geprüft.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

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
