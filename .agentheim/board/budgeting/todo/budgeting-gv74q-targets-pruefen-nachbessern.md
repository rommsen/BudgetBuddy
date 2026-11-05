---
id: budgeting-gv74q
title: "Targets prüfen nachbessern: Testlücken aus dem QA-Review schließen, TargetChangeMark als DU statt Bool, Prüfung ohne Lesen der Pausen"
status: todo
type: refactor
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-p2zr6]
blocks: [budgeting-q4v8n]
tags: [budget, target, pruefen, tests]
related_adrs: [0082]
related_research: []
prior_art: [budgeting-p2zr6]
---

## Why
Die Reviews nach budgeting-p2zr6 (2026-11-05) haben keine kritischen Befunde, aber Lücken: das QA-Review zeigt fünf Mutanten, die grün bleiben, das Backend-Review eine Bool-Flagge im Shared-Typ und eine unnötige Abhängigkeit vom Lesen der Pausen. Beides ist jetzt am billigsten, weil noch kein Client `TargetCheckData` nutzt und keine Fixture in `scripts/*.test.mjs` den Typ kennt. Roman hat die Umsetzung am 2026-11-05 freigegeben.

## What
Domäne und Tests in `src/Shared/Budgeting.fs`, `src/Tests/TargetCheckTests.fs`, `src/Tests/TargetCheckQueryTests.fs`, Handler in `src/Server/Budgeting/QueryHandlers.fs`.

1. **Sortierung nach Abstand prüfen** (`TargetCheckTests.fs`, Test "Gruppen"): Namen so wählen, dass die Reihenfolge nach Abstand gegen die alphabetische läuft ("A klein" vor "Z groß"), plus ein Fall mit gleichem Abstand für den Namens-Tiebreak. Die Mutante `List.sortBy (fun r -> r.Name)` statt der Abstandssortierung muss rot werden.
2. **Live-Anschluss von `LoadLastTargetChanges`** (Doktrin Punkt 3): im Endpoint-Test über `budgetApiWith` für die eindeutige Kategorie eine Änderung über den echten Persistenzweg schreiben (Ergebnis Schritt 1 `written`) und `LastChange` erwarten. Die Mutante `LoadLastTargetChanges = fun () -> async { return Ok Map.empty }` in `liveTargetCheckPort` muss rot werden. Der Test darf nicht von fremden Zeilen in `target_changes` der geteilten DB abhängen (etwa der Zeile mit Art "Quatsch" aus `TargetChangeTests.fs`); entweder nur die eigene Kategorie auswerten oder die Verbindungsquelle von `loadLastTargetChanges` als Parameter, dann auch die Abbildung eines `Error` auf `TargetChangesUnreadableText` direkt prüfen.
3. **FsCheck-Property ADR 0082 Punkt 9 mit echten Beträgen**: eigener Generator für Monatsbeträge, etwa `Gen.choose (-50_000, 1_000_000)` Cent, damit "mehr" und "weniger" erreichbar sind. `Years 2` und `Years 3` aus den Rhythmen der Property nehmen (immer "nicht geprüft") oder begründet kommentieren. Gegenprobe im Outcome: eine Rundungsmutante in der Rückrechnung (etwa wöchentlich ohne kaufmännisches Runden) macht die Property rot.
4. **Entfernen mit genug gezählten Monaten**: Entfernen am 15.07. bei laufendem November ergibt "Kein Target" ohne Zusatz (bei Ausgaben, die "Kein Target" treffen). Die Mutante `Some mark when n < needed || mark.Removed` muss rot werden.
5. **Tiebreak `rowid`** bei gleichem `occurred_at` in `lastChangeTests` testen. `lastChangeTests` auf `testCase` umstellen wie der Rest der Datei (Doktrin Punkt 19).
6. **`TargetChangeMark.Removed: bool` als DU**: etwa `TargetChangeMark = TargetSetInBb of day: string | TargetRemovedInBb of day: string` oder ein Feld `Kind` mit DU. Name legt der Worker fest. `targetCheckRow`, `withWrittenTarget`, `queryLastTargetChanges`/`loadLastTargetChanges` und die Tests ziehen mit.
7. **Targets prüfen ohne Pausen**: `targetCheck` liest den Monat über `toBudgetMonth Set.empty` direkt auf `port.FetchMonth` statt über `budgetMonthOf` (ADR 0082 Punkt 3: pausierte Kategorien werden geprüft wie andere). Ein Lesefehler bei den Pausen darf die Seite nicht mehr scheitern lassen. `budgetMonth` bleibt unverändert.
8. **Fehlertext einmal**: `SpendingUnreadableText` vor `categoryHistory` ziehen und an beiden Stellen nutzen.
9. **AC13 in `src/Tests/LedgerPageTests.fs:2871`**: Name und Kommentar auf `loadCategoriesEntries` aktualisieren. Löschen gehört nicht in diese Task.

## Acceptance criteria
- [ ] Jede der Mutanten aus Punkt 1, 2, 3 und 4 macht mindestens einen Test rot; im Outcome als Mutantenliste mit dem roten Test vermerkt, danach zurückgesetzt.
- [ ] Die Property erzeugt Beträge, bei denen "mehr" und "weniger" ohne Vorschlag als Target vorkommen (im Outcome belegt, etwa über `Prop.classify` oder einen Zähler in einem Probelauf).
- [ ] Ein Test zeigt den `rowid`-Tiebreak; `lastChangeTests` nutzt `testCase`.
- [ ] `TargetChangeMark` trägt kein Bool mehr für Setzen oder Entfernen; alle bisherigen Tests zu "neu gesetzt am" und "entfernt am" bleiben grün.
- [ ] Ein Handler-Test: der Pausen-Port bzw. das Lesen der Pausen wird von `targetCheck` nicht aufgerufen (oder ein Pausen-Lesefehler ändert das Ergebnis nicht). Bestehende Tests zu `budgetMonth` bleiben unverändert grün.
- [ ] `SpendingUnreadableText` ist der einzige Ort des Textes "Ausgaben aus dem Kassenbuch nicht lesbar" im Server.
- [ ] Kein bestehender Shared-Record bekommt ein Pflichtfeld, `scripts/*.test.mjs` bleiben unberührt (`TargetCheckData` hat noch keine Fixture).
- [ ] README budgeting: der Eintrag **Target-Prüfung** nennt "Entfernen markiert" passend zur neuen Form, falls nötig als README-Delta (geprüft mit `npm run check:readme-delta -- --bc budgeting`).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Quelle: QA-Review (fehlende Tests 1 bis 5, falsch platziert 1 und 2) und Backend-Review (Warnungen 1 und 2, Vorschlag 3) vom 2026-11-05 nach budgeting-p2zr6. Die übrigen Backend-Vorschläge (gemeinsamer Helfer für `targetCheckMonths`/`spendingMonths`, ein Durchlauf in `getMonthlyActivityByCategory`, Name `loadCategoriesEntries`, Literal `'written'`) sind optional; der Worker nimmt sie mit, wenn sie klein bleiben, sonst nicht.

Das Rennen in `rebuildSplitIndexes` ist ledger-cy1j8, nicht diese Task.

Läuft parallel zu budgeting-p8nd0 (Client) und ledger-cy1j8 (Kassenbuch-Persistenz).
