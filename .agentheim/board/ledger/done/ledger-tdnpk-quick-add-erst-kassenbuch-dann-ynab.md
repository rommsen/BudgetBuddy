---
id: ledger-tdnpk
title: "Quick Add: erst Kassenbuch (CashExpenseAdded), dann Push nach YNAB, dann Verknüpfung über die Id aus der Push-Antwort"
status: done
type: feature
context: ledger
created: 2026-09-11
completed: 2026-09-11
depends_on: [ledger-cscw1]
blocks: []
tags: [ledger, ynab-sync, quick-add, identity]
related_adrs: [0004, 0013, 0015, 0021]
related_research: []
prior_art: []
---

## Why
Roman hat entschieden (2026-09-11): Quick Add schreibt künftig zuerst eine Buchung ins
Kassenbuch, pusht dann nach YNAB und verknüpft danach die YNAB-Id aus der Push-Antwort.
Heute pusht Quick Add direkt nach YNAB (ADR 0004) und kommt nur über den Spiegel zurück,
ohne eigene Herkunft im Kassenbuch. Das ist das Muster der Vision ("BB entscheidet, YNAB
wird nachgezogen") am kleinsten Fall.

Quick-Add-Buchungen tragen keine ImportId (ADR 0004). Die Antwort des Einzel-Posts ist
deshalb die einzige Stelle, an der BBs Buchung und YNABs Buchung verbunden werden können
(ADR 0015). Heute gibt `createManualTransaction` nur `unit` zurück. Dieser Task nimmt den
Quick-Add-Teil des früheren Tasks `ynab-b7q2m` in sich auf (Entscheidung Roman,
2026-09-11).

## What
1. `YnabClient.createManualTransaction` gibt die YNAB-Transaction-Id aus der
   Einzel-Post-Antwort zurück statt `unit`.
2. Neuer Command-Handler in `Ledger/CommandHandlers.fs` (z. B. `addCashExpense`) nimmt die
   Quick-Add-Eingabe (Konto "Bar", Betrag, Payee, Memo, Kategorie) und schreibt zuerst
   `CashExpenseAdded` (Actor `QuickAdd`) via `appendAndApply`.
3. Danach ruft er `createManualTransaction` auf.
4. Bei Erfolg: `EntryLinkedToYnab` mit der zurückgegebenen Id auf derselben Buchung.
5. Bei Push-Fehler: die Kassenbuch-Buchung bleibt stehen (ADR 0021 Punkt 2), kein
   Rollback; Fehlermeldung an Roman wie heute.
6. Die bestehende `AddManualTransaction`-API-Signatur bleibt für den Client unverändert.

## Acceptance criteria
- [x] `createManualTransaction` gibt die YNAB-Transaction-Id aus der Antwort zurück (Test
      gegen eine aufgezeichnete Antwort).
- [x] Eine Quick-Add-Eingabe erzeugt zuerst eine Kassenbuch-Buchung mit Herkunft
      "Quick Add", danach eine YNAB-Buchung, danach eine Verknüpfung; die Historie zeigt
      `CashExpenseAdded` und `EntryLinkedToYnab`.
- [x] Ein simulierter Push-Fehler lässt die Kassenbuch-Buchung stehen, ohne Verknüpfung
      (Test).
- [x] Der nächste Spiegel-Lauf trifft die verknüpfte Buchung über die YNAB-Id und legt
      keine zweite an (Test).
- [x] Bestehende Quick-Add-Vertragstests (`QuickAddTests.fs`, ADR 0004) bleiben grün.
- [x] `dotnet build` und `dotnet test` grün.

## Notes
- Das kurze Zeitfenster zwischen erfolgreicher Push-Antwort und `EntryLinkedToYnab`
  (Absturz genau dazwischen ergibt beim nächsten Spiegel-Lauf eine zweite Buchung, weil
  Quick Add keine ImportId hat) ist laut ADR 0021 für v1 hingenommen.
- Für `ledger-h4p6c` (Spiegel nach jedem Push) vermerkt: der Lauf nach Quick Add darf erst
  nach `EntryLinkedToYnab` starten.
- `ledger-qvt84` ändert parallel `createTransactions` in derselben Datei, aber eine andere
  Funktion.

## Outcome
Quick Add schreibt jetzt in der Reihenfolge aus ADR 0021: erst die Kassenbuch-Buchung
(`CashExpenseAdded`, Actor `QuickAdd`), dann der Push nach YNAB, dann bei Erfolg die
Verknüpfung (`EntryLinkedToYnab`) über die YNAB-Transaction-Id aus der Push-Antwort.

**`src/Server/YnabClient.fs`:** `createManualTransaction` gibt jetzt `Async<YnabResult<string>>`
statt `Async<YnabResult<unit>>` zurück. Neue, nicht-private Funktion
`manualTransactionIdDecoder : Decoder<string>` liest `data.transaction.id` aus der Antwort
des Einzel-Posts (nicht die Batch-Antwort mit `data.transactions`); ein Decode-Fehler wird
als `YnabError.InvalidResponse` durchgereicht, kein stiller Fallback.

**`src/Server/Ledger/CommandHandlers.fs`:** neuer Typ `CashExpenseInput` (Konto, Datum,
Betrag, Payee, Memo, Kategorie) und neue Funktion `addCashExpense (now) (push) (input)`.
`push: unit -> Async<YnabResult<string>>` ist injiziert (Muster wie `MirrorFetchers` beim
Spiegel-Lauf), damit der Ablauf ohne Uhr und ohne Netz testbar ist. Schreibt zuerst
`CashExpenseAdded` über `appendAndApply`, ruft danach `push` auf; bei Erfolg folgt
`EntryLinkedToYnab` als zweites `appendAndApply`, bei Fehler bleibt die Buchung
unverändert stehen (kein Rollback, ADR 0021 Punkt 2) und der Fehler wird durchgereicht.

**`src/Server/Api.fs` (`addManualTransaction`):** die Client-Signatur
(`ManualTransactionRequest -> Async<YnabResult<unit>>`) bleibt unverändert. Intern löst der
Handler jetzt zuerst das Kassenbuch-Konto zum konfigurierten YNAB-Quick-Add-Konto auf
(`Ledger.Persistence.findAccountByYnabAccountId`). Ist der Spiegel für dieses Konto noch nie
gelaufen, gibt es keine Kassenbuch-Buchung ohne Push (ADR 0021 verlangt Kassenbuch zuerst)
und keinen stillen Push ohne Kassenbuch-Eintrag: der Handler lehnt mit einer Fehlermeldung
an Roman ab ("Einmal 'Jetzt spiegeln' ausführen"), statt ein zweites, unverknüpftes
"Bar"-Konto zu erfinden (ADR 0016 kennt nur ein Konto "Bar"). Ist eine Kategorie gesetzt,
löst der Handler ihren Namen über `YnabClient.getCategories` auf (gleiches Muster wie beim
Regel-Anlegen weiter oben in derselben Datei), weil `ManualTransactionRequest` nur die
Id trägt, `CashExpenseAddedPayload.Category` aber eine `CategoryRef` mit Namen verlangt
(ADR 0013 Punkt 4: der Name gehört in den Snapshot, damit die Historie ohne Nachschlagen
lesbar bleibt). Danach `Ledger.CommandHandlers.addCashExpense DateTime.UtcNow push input`,
`push` ist die Closure auf `YnabClient.createManualTransaction` mit Token, Budget und
Quick-Add-Konto.

**Getroffene Entscheidung (dokumentiert, keine eigene ADR-Datei):** fehlt das gespiegelte
Kassenbuch-Konto für das Quick-Add-Konto, lehnt `Server.Api` mit einer klaren Fehlermeldung
ab, statt YNAB stumm ohne Kassenbuch-Eintrag zu pushen oder ein zweites "Bar"-Konto zu
erfinden. Alternative (Push ohne Kassenbuch-Eintrag als Fallback) verworfen: das würde eine
BB-geführte Buchung erzeugen, die erst beim nächsten Spiegel-Lauf mit falscher Herkunft
("YNAB-Spiegel") im Kassenbuch auftaucht, genau das Muster, das ADR 0021 vermeiden will.

**Tests** (TDD, rot vor jedem Fix beobachtet):
- `src/Tests/YnabClientTests.fs`: neue Testliste `manualTransactionIdDecoder (Quick Add,
  Einzel-Post-Antwort)`, zwei Fälle: liest die Id aus einer repräsentativen aufgezeichneten
  Antwort; eine Batch-Antwort (kein `data.transaction`) liefert einen Decode-Fehler statt
  einer falschen Id.
- `src/Tests/LedgerTests.fs`: neue Testliste `Ledger CommandHandlers (addCashExpense)`, zwei
  Fälle: Erfolgspfad (Historie zeigt `CashExpenseAdded` dann `EntryLinkedToYnab`, beide
  Actor `QuickAdd`, Verknüpfung über die YNAB-Id auffindbar); Push-Fehler (Buchung bleibt
  stehen, nur `CashExpenseAdded` im Log, keine YNAB-Verknüpfung, Fehler wird durchgereicht).
- `src/Tests/LedgerMirrorTests.fs`: ein neuer Testfall in der Spiegel-Lauf-Liste zeigt, dass
  der nächste Spiegel-Lauf eine per `addCashExpense` verknüpfte Buchung über die YNAB-Id
  trifft (`NewEntries = 0`, `ChangedEntries = 0` bei unveränderten Daten) und keine zweite
  Buchung für dasselbe Bar-Konto anlegt.

**README** (`.agentheim/contexts/ledger/README.md`, Abschnitt "Key commands"): der
Quick-Add-Teil von `BuchungÜbernehmen` beschreibt jetzt den tatsächlichen Ablauf
(`addCashExpense`, Push, `EntryLinkedToYnab`, Verhalten ohne gespiegeltes Bar-Konto).

**Verifikation:** `dotnet build BudgetBuddy.sln` grün, 0 Warnungen. `dotnet test
src/Tests/Tests.fsproj` grün: 741 bestanden (5 mehr als vorher), 7 übersprungen (externe
Integrationstests, unverändert), 0 Fehler. Bestehende Quick-Add-Vertragstests
(`QuickAddTests.fs`) unverändert und weiterhin grün, weil `ManualTransactionRequest` und
`buildManualTransactionBody` nicht angefasst wurden.

## Diary entry

## 2026-09-11 13:05 - Quick Add: erst Kassenbuch, dann Push, dann Verknüpfung

**What I did:**
`createManualTransaction` liest jetzt die YNAB-Transaction-Id aus der Antwort des
Einzel-Posts; ein neuer Command-Handler `addCashExpense` schreibt zuerst die
Kassenbuch-Buchung (`CashExpenseAdded`), pusht danach nach YNAB und verknüpft bei Erfolg
mit `EntryLinkedToYnab` (ADR 0021). Scheitert der Push, bleibt die Buchung ohne
Verknüpfung stehen, kein Rollback. `Server.Api.addManualTransaction` verdrahtet das, mit
unveränderter Client-Signatur; fehlt das gespiegelte Bar-Konto, lehnt der Server mit einer
klaren Fehlermeldung ab, statt ein zweites Konto zu erfinden.

**Files Added:**
- keine

**Files Modified:**
- `src/Server/YnabClient.fs` - `createManualTransaction` gibt `Async<YnabResult<string>>`
  zurück; neue Funktion `manualTransactionIdDecoder`.
- `src/Server/Ledger/CommandHandlers.fs` - neuer Typ `CashExpenseInput` und neue Funktion
  `addCashExpense`.
- `src/Server/Api.fs` - `addManualTransaction` löst das Kassenbuch-Konto und die Kategorie
  auf und ruft `addCashExpense` mit einer Push-Closure auf.
- `src/Tests/YnabClientTests.fs` - neue Testliste für `manualTransactionIdDecoder`.
- `src/Tests/LedgerTests.fs` - neue Testliste für `addCashExpense` (Erfolg, Push-Fehler).
- `src/Tests/LedgerMirrorTests.fs` - neuer Testfall: Spiegel-Lauf nach Quick Add legt keine
  zweite Buchung an.
- `.agentheim/contexts/ledger/README.md` - Abschnitt "Key commands" um den tatsächlichen
  Quick-Add-Ablauf ergänzt.

**Files Deleted:**
- keine

**Rationale:**
Quick Add hat keine ImportId (ADR 0004); die Push-Antwort ist die einzige Stelle, an der
BBs Buchung und YNABs Buchung verbunden werden können (ADR 0015). Roman hat entschieden,
dass das Kassenbuch dafür zuerst geschrieben wird (ADR 0021), symmetrisch zum
Comdirect-Import, damit ein Timeout oder Absturz zwischen Push und Verknüpfung nie die
BB-Herkunft der Buchung verliert.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 741 bestanden (5 mehr als vorher), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`).
- Issues: keine.
