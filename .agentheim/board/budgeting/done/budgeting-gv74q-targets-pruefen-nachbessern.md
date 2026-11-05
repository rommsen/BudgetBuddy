---
id: budgeting-gv74q
title: "Targets prüfen nachbessern: Testlücken aus dem QA-Review schließen, TargetChangeMark als DU statt Bool, Prüfung ohne Lesen der Pausen"
status: done
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

## Änderung während der Umsetzung (2026-11-05)
Roman hat entschieden: Punkt 9 lautet jetzt "AC13 in `src/Tests/LedgerPageTests.fs:2871` löschen" (Teilstring-Zähler, Doktrin Punkt 7), statt Name und Kommentar zu aktualisieren. Dem laufenden Worker mitgeteilt.

## Outcome

Nachbesserung zu budgeting-p2zr6 nach den Reviews vom 2026-11-05. `./verify.sh`: `VERIFY: PASS` (dotnet 3859, node 310), zweimal hintereinander, der zweite Lauf nach einer reinen Kommentaränderung.

**Domäne** (`src/Shared/Budgeting.fs`):
- `TargetChangeMark` ist ein DU `TargetSetOn of day | TargetRemovedOn of day` mit `changeMarkDay`. `targetCheckRow` unterscheidet über den Fall, `withWrittenTarget` setzt `TargetSetOn Today` oder bei `None` `TargetRemovedOn Today`. `TargetCheckData` hat keine Fixture, kein Shared-Record hat ein neues Pflichtfeld, `scripts/*.test.mjs` sind unberührt.

**Server:**
- `Budgeting.Persistence.queryLastTargetChanges` bildet auf den DU ab. Neu ist `loadLastTargetChangesWith (connect)`, `loadLastTargetChanges ()` ruft es mit `Core.getConnection` auf.
- `QueryHandlers.targetCheck` liest den Monat direkt über `port.FetchMonth` und `toBudgetMonth Set.empty`, ohne die BB-Pausen (ADR 0082 Punkt 3). `budgetMonth`/`budgetMonthOf` sind unverändert.
- `SpendingUnreadableText` steht vor `categoryHistory` und wird an beiden Stellen genutzt. Im Server ist es der einzige Ort des Textes.

**Tests:**
- `TargetCheckTests.fs`: Gruppen-Test mit Namen gegen den Abstand ("A … klein", "Z … groß") und je Gruppe ein Fall mit gleichem Abstand ("M … gleich") für den Namens-Tiebreak. Neuer Test "Entfernen am 15.07. bei laufendem November: … Kein Target ohne Zusatz". Die Property hat einen eigenen Generator (`targetAndSpending`): Target 1 € bis 20.000 €, 12 Monate in Cent mit Obergrenze 20/200/2.000/10.000 € und einem negativen Zwanzigstel als Untergrenze, 1000 Fälle, `Prop.classify` für "vorher mehr" und "vorher weniger". `Years 2` und `Years 3` sind aus den Rhythmen genommen, mit Kommentar (P > 12 heißt immer "nicht geprüft").
- Probelauf mit 1000 Stichproben aus dem Generator, Gruppe mit dem Ausgangstarget: SpendsMore 644, SpendsLess 284, Fits 72. "Mehr" und "weniger" kommen also vor. Der Zähler war ein vorübergehender Test und ist wieder entfernt.
- `TargetCheckQueryTests.fs`: `lastChangeTests` nutzt `testCase`. Neu: rowid-Tiebreak bei gleichem `occurred_at` (zwei Kategorien, je beide Reihenfolgen), `loadLastTargetChangesWith` bildet eine unbekannte Art auf `TargetChangesUnreadableText` ab (eigene `:memory:`-Verbindung), Handler-Test "Die BB-Pausen liest der Handler nicht" (eine Pause mit Art "Quatsch" im eigenen Monat 2093-04; Gegenprobe: `budgetMonthOf` scheitert dort, `targetCheck` liefert Ok). Der Endpoint-Test über `budgetApiWith` schreibt eine Änderung über `appendTargetChanges` und `appendTargetChangeStepResult … 1 … Written` und erwartet `LastChange = Some (TargetSetOn "2026-08-10")`. Er wertet nur die eigene Kategorie aus. Die Zeile "Quatsch" aus `TargetChangeTests.fs` hat kein Ergebnis und fällt aus dem Join.
- `LedgerPageTests.fs`: der Strukturtest AC13 (Teilstring-Zähler auf `loadAllMatching`/`hasAmountFilter`) ist gelöscht, auf Romans Entscheidung (Doktrin Punkt 7). Er hatte keine eigenen Helfer, `Regex`/`Path` nutzt AC5 weiter. AC5 verwies im Kommentar auf "das Muster von AC13" und verweist jetzt auf FontFaceRegressionTests.fs. Weitere Verweise auf diesen AC13 gibt es nicht. Das "AC13" bei Zeile 2049 gehört zu einer anderen Task. ADR 0037/0038 nennen ledger-r7dm2 nur historisch und bleiben unverändert.

**Mutanten** (jede einzeln angewendet, gebaut und `--filter "Targets prüfen"` ausgeführt, danach zurückgesetzt):
1. `List.sortBy (fun r -> r.Name)` statt Abstand: rot in "Gruppen.Reihenfolge nach ADR 0082 Punkt 3 …". Gegenprobe ohne Namens-Tiebreak (`-(gap r)` allein): rot in demselben Test und in "Nie steht nach Name sortiert …".
2. `LoadLastTargetChanges = fun () -> async { return Ok Map.empty }` in `liveTargetCheckPort`: rot in "Endpoint.getTargetCheck liest den Monat …". Zusatz: `TargetRemoved` als `TargetSetOn` abgebildet: rot in "Bei gleichem occurred_at …" und "Eine geschriebene mit fehlgeschlagenem Schritt 2 zählt, Entfernt kommt als Entfernen".
3. Rundung in der Rückrechnung: wöchentlich auf volle 5 € aufgerundet (`Math.Ceiling(x / 5m) * 5m`, die in ADR 0082 verworfene Alternative): rot in der Property "Invariant ADR 0082 Punkt 9 …" und in "Vorschlag.Zurückgerechnet in den Rhythmus". Wöchentlich mit `Math.Floor` statt kaufmännisch: rot nur in "Zurückgerechnet in den Rhythmus" (215 € ergibt 49 statt 50 €), die Property bleibt grün. Das ist so gewollt: eine Abweichung auf volle Euro je Woche ist höchstens 4,33 € im Monat, die Schwellen für mehr und weniger verlangen mindestens 10 €. Die Invariante toleriert sie also, und die Property kann sie nicht fangen.
4. `Some mark when n < needed || mark.Removed` (hier: `TargetRemovedOn` ohne `when n < needed`): rot in "Entfernen am 15.07. bei laufendem November …".
5. rowid-Tiebreak entfernt (`maxBy` nur nach `at`): rot in "Bei gleichem occurred_at gilt die später eingefügte Zeile (rowid)".
6. `targetCheck` wieder über `budgetMonthOf`: rot in "Die BB-Pausen liest der Handler nicht …".

Hinweis: im ersten Mutantenlauf blieben Mutanten stehen, weil die zurückgelegte Datei eine ältere mtime hatte und MSBuild nicht neu baute. Gelöst mit `touch` nach dem Zurücklegen, danach alle Mutanten sauber neu gelaufen. Der UNIQUE-Fehler aus `rebuildSplitIndexes` trat nicht auf.

Die optionalen Backend-Vorschläge (gemeinsamer Helfer für `targetCheckMonths`/`spendingMonths`, ein Durchlauf in `getMonthlyActivityByCategory`, Literal `'written'`) sind nicht umgesetzt. README budgeting: der Eintrag **Target-Prüfung** per README-Delta auf die neue Form gebracht, geprüft mit `npm run check:readme-delta -- --bc budgeting` (Exit 0).
