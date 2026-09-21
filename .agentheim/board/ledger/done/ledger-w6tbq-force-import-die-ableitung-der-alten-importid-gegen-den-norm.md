---
id: ledger-w6tbq
title: "Force-Import: die Ableitung der alten ImportId gegen den normalen Push festnageln"
status: done
type: chore
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [tests, force-import, ynab, abdeckung]
related_adrs: [0015, 0021]
related_research: []
prior_art: [ledger-fq3kd]
---

## Why

Lücke 3 aus dem QA-Review nach dem Work-Lauf vom 2026-09-19 (erfasst als
`ledger-r7dm2`), das Risiko mit dem höchsten Schaden.

`ledger-fq3kd` hat Force-Import so umgebaut, dass er die bestehende
Kassenbuch-Buchung über ihre alte, deterministische ImportId wiederfindet und neu
verknüpft, statt eine zweite „Comdirect-Import"-Buchung anzulegen. Die drei Tests dazu
sitzen auf `Ledger.CommandHandlers.pushComdirectImports` und bauen ihren Kandidaten von
Hand. Ungeprüft bleibt, was die API-Ebene neu tut, vor allem
`src/Server/Api.fs:1394`:

    ExistingEntryImportId = Some (Shared.Domain.generateImportId p.TransactionId)

Dieser Ausdruck muss byteweise dieselbe ImportId erzeugen wie der normale Push über
`Ynab.prepareTransactions ... forceNewImportId = false` (`src/Server/YnabClient.fs:661`).
Driften die beiden Ableitungen je auseinander, findet Force-Import die bestehende
Buchung nicht mehr, legt wieder still eine zweite an, und alle Tests bleiben grün, weil
keiner die beiden Ableitungen gegeneinander hält.

Dazu kommt, dass die beiden Aufrufstellen in `src/Server/Api.fs` (Z. 1131 bis 1163 für
den normalen Push, Z. 1369 bis 1394 für Force-Import) denselben 20-Zeilen-Block
Kandidatenbau doppelt führen und sich nur in einem Feld unterscheiden. Die Regel
„Payee/Memo kommen aus `p.Request`, nicht aus den rohen Bank-Feldern" (Verifier-Fund aus
`ledger-qvt84`, Iteration 3) steht deshalb zweimal da und ist an keiner Stelle getestet.

## What

Den Kandidatenbau aus beiden Closures in eine reine Funktion auf Modulebene in
`src/Server/Api.fs` ziehen, etwa:

    let buildComdirectImportCandidates
        (forceImport: bool)
        (transactions: SyncTransaction list)
        (prepared: Ynab.PrepareResult)
        : Ledger.CommandHandlers.ComdirectImportCandidate list

`forceImport = false` setzt `ExistingEntryImportId = None`, `true` setzt
`Some (Shared.Domain.generateImportId p.TransactionId)`. Beide Aufrufstellen rufen
diese Funktion. Derselbe Zug, den `ledger-w9skm` für `entryRowClassName` gemacht hat:
die Entscheidung wird eine reine Funktion, damit sie ohne Netz, ohne Datenbank und ohne
Session testbar ist.

Dazu eine neue Testdatei `src/Tests/ForceImportCandidateTests.fs` samt Eintrag in
`src/Tests/Tests.fsproj`. Kein Test baut eine ImportId von Hand: beide Seiten des
Vergleichs entstehen durch den Produktionscode.

## Acceptance criteria

- [ ] **Die Suchschlüssel-Ableitung deckt sich mit dem normalen Push.** Ein Test in
      `src/Tests/ForceImportCandidateTests.fs` baut drei `SyncTransaction` mit
      verschiedenen `TransactionId` und bildet beide Vorbereitungen über den echten
      Produktionscode:
      `let normal = Ynab.prepareTransactions today acct Map.empty false txs` und
      `let forced = Ynab.prepareTransactions today acct Map.empty true txs`, daraus
      `buildComdirectImportCandidates false txs normal` und
      `buildComdirectImportCandidates true txs forced`. Erwartet: für jede
      `TransactionId` gilt
      `forcedCandidate.ExistingEntryImportId = Some normalCandidate.Prepared.ImportId`.
      Kein Erwartungswert wird im Test hingeschrieben; beide Seiten entstehen aus
      `prepareTransactions`. Fängt jede Drift zwischen `generateImportId` und dem
      Normal-Zweig von `prepareTransactions`, etwa ein `generateImportId`, das die
      Bindestriche der Guid stehen lässt, oder ein `prepareTransactions`, das das Präfix
      ändert.
- [ ] **Gesendete und gesuchte ImportId sind verschieden.** Derselbe Test erwartet für
      jeden Force-Kandidaten
      `forcedCandidate.Prepared.ImportId <> forcedCandidate.ExistingEntryImportId.Value`
      und dass `Prepared.ImportId` mit `"BB:"` beginnt. Fängt eine Vereinfachung, die
      beide Werte gleichsetzt und damit YNABs Dedup wieder ins Messer laufen lässt.
- [ ] **Der normale Pfad sucht nicht.** Derselbe Test erwartet
      `normalCandidates |> List.forall (fun c -> c.ExistingEntryImportId = None)`.
      Fängt eine Umsetzung, die das Flag ignoriert und den Suchschlüssel überall setzt.
- [ ] **Payee und Memo kommen aus dem vorbereiteten Request, nicht aus den Rohdaten.**
      Ein Test baut eine `SyncTransaction`, deren roher Comdirect-Payee sich vom
      aufgelösten `Request.PayeeName` unterscheidet (etwa leerer Payee, der auf
      `"Unknown"` fällt) und deren `Reference` gesetzt ist. Erwartet für den Kandidaten:
      `c.Payee = Some c.Prepared.Request.PayeeName`, `c.Memo = Some c.Prepared.Request.Memo`,
      `c.Payee <> Some rawPayee` und `c.Memo.Value` enthält `"Ref: " + reference`.
      Fängt einen Rückfall auf die rohen Bank-Felder, der den nächsten Spiegel-Lauf ein
      falsches Kopfdaten-Ereignis schreiben ließe (der Verifier-Fund aus `ledger-qvt84`,
      Iteration 3, heute ungetestet).
- [ ] **Die Rohdatenfelder bleiben roh.** Derselbe Test erwartet
      `c.ComdirectReference = tx.Transaction.Reference`,
      `c.Date = tx.Transaction.BookingDate`, `c.Amount = tx.Transaction.Amount` und
      `c.ReviewSteps = tx.ReviewSteps`.
- [ ] **Es gibt nur noch eine Stelle, die `ExistingEntryImportId` setzt.** In
      `src/Server/Api.fs` kommt der Bezeichner `ExistingEntryImportId` nur noch
      innerhalb von `buildComdirectImportCandidates` vor; beide Aufrufstellen (normaler
      Import-Push und `forceImportDuplicates`) rufen diese Funktion.
- [ ] `dotnet build` und `dotnet test` grün, `src/Tests/Tests.fsproj` führt die neue
      Datei.

## Notes

**Herkunft der Testdefinitionen.** Der Elterntask `ledger-r7dm2` behauptet, der QA-Review
liege als Volltext im Diary-Eintrag vom 2026-09-19 20:14 und nenne je Test Datei, Eingabe
und erwartetes Ergebnis. Das stimmt nicht; dort steht nur ein Nachtrag von rund 15
Zeilen. Die Kriterien oben sind aus `src/Server/Api.fs` und `src/Server/YnabClient.fs`
abgeleitet, nicht abgeschrieben.

Ein Fable.Remoting-Proxy ist unter `dotnet test` nicht aufrufbar; deshalb wird nicht
`forceImportDuplicates` selbst getestet, sondern die reine Funktion, die es ab jetzt
benutzt. Das ist dieselbe Trennung, die `ledger-fq3kd` mit `pushComdirectImports`
(injizierter Push, keine Uhr, kein HTTP) bereits gewählt hat.

`Ynab.prepareTransactions` ist rein und nimmt `today` als Parameter, der Test braucht
also keine Systemzeit. Die Payee-Map bleibt `Map.empty`, solange keine Transfer-Zeile im
Spiel ist.

## Verifier note (iteration 1)

**VERDICT: FAIL** — `likely-fixable`.

**REASONS:**

- **Kriterium 5 ("Die Rohdatenfelder bleiben roh") ist nur zu einem Viertel abgedeckt.** In
  `src/Tests/ForceImportCandidateTests.fs` steht als einzige Rohdaten-Zusicherung
  `Expect.equal c.ComdirectReference rawReference` (Z. 147). `c.Date = tx.Transaction.BookingDate`,
  `c.Amount = tx.Transaction.Amount` und `c.ReviewSteps = tx.ReviewSteps` werden im gesamten Diff
  nirgends geprüft (`Date`/`Amount`/`ReviewSteps` kommen nur in den Fixture-Feldern Z. 37, 38, 55
  vor). Ein Vertauschen dieser drei Felder in `buildComdirectImportCandidates`
  (`src/Server/Api.fs:756-760`) fiele durch kein Netz.
- **Kriterium 1 verlangt drei `SyncTransaction` mit verschiedenen `TransactionId`** und die
  Zusicherung "für jede `TransactionId`". Der zentrale Test baut genau **eine** und arbeitet mit
  `List.exactlyOne` (Z. 74-86). **Der Verlust ist real und belegt:** in einer Arbeitskopie
  außerhalb des Worktrees wurde `let tx = Map.find p.TransactionId byTransactionId`
  (`src/Server/Api.fs:754`) durch `let tx = List.head transactions` ersetzt, also die Zuordnung
  zwischen Rohdaten und Vorbereitung zerstört. **Alle vier neuen Tests bleiben grün**
  ("Fehler: 0, erfolgreich: 4"). Mit drei Transaktionen würde genau diese Mutation rot.
- **Kriterium 3 verlangt `normalCandidates |> List.forall (...)`**; geprüft wird `Expect.isNone`
  auf einem einzelnen Kandidaten (Z. 105-113). Für eine einelementige Liste formal gleichwertig,
  fällt aber mit Kriterium 1 zusammen: sobald drei Transaktionen im Spiel sind, muss das `forall`
  her.

**SUGGESTED_FIX:** Den Payee/Rohdaten-Test um die drei fehlenden Zusicherungen ergänzen und die
Fixture dabei so wählen, dass sie etwas beweist: `BookingDate` ungleich dem an
`prepareTransactions` übergebenen `today`, und eine nicht leere `ReviewSteps`-Liste. Den
zentralen Drift-Test auf drei `SyncTransaction` mit verschiedenen `TransactionId` umstellen, den
Vergleich je `TransactionId` über beide Kandidatenlisten führen, und Kriterium 3 als
`List.forall` über alle normalen Kandidaten schreiben, damit die Zuordnung über `Map.find`
mitgeprüft wird.

**ITERATION_HINT:** likely-fixable

### Ein Befund über das Kriterium selbst, kein Fehler des Workers

Die in Kriterium 1 genannte Beispiel-Drift, "etwa ein `generateImportId`, das die Bindestriche
der Guid stehen lässt", ist mit zwei produktionsabgeleiteten Seiten **prinzipiell nicht
fangbar**: der Normalzweig von `prepareTransactions` ruft selbst
`Shared.Domain.generateImportId tx.Transaction.Id` auf (`src/Server/YnabClient.fs:665`), und
`Api.fs:764` ruft dieselbe Funktion mit demselben Wert. Beide Seiten bewegen sich also
gemeinsam. Belegt: `generateImportId` in der Arbeitskopie auf "Bindestriche behalten" mutiert,
alle vier Tests bleiben grün.

**Die echte Divergenz fängt der Test dagegen sauber:** Normalzweig auf
`generateImportId ... + "-v2"` mutiert, der zentrale Test wird rot (1 Fehler,
`actual: Some "BB:txfq3kd"`). Die Eigenschaft ist also richtig gepinnt; nur das im Kriterium
genannte Beispiel ist so nicht erfüllbar. Wer das Kriterium später schärfen will, muss das
Beispiel ersetzen, nicht den Test.

## Outcome

`src/Server/Api.fs` trug die Kandidaten-Konstruktion für den Comdirect-Import-Push zweimal,
identisch bis auf das eine Feld `ExistingEntryImportId`. Beide Blöcke sind jetzt ein Aufruf der
neuen, reinen Funktion `buildComdirectImportCandidates` (Z. 745-768), der **einzigen** Stelle,
die dieses Feld setzt: `forceImport = false` liefert `None` (Suche und Versand teilen sich
dieselbe ImportId, `ledger-fq3kd`), `forceImport = true` liefert
`Some (Shared.Domain.generateImportId p.TransactionId)`, also die alte, deterministische
ImportId, über die die bestehende Kassenbuch-Buchung ursprünglich angelegt wurde, während
`p.Request`/`p.ImportId` bereits die neu erzeugte ImportId zum Versand tragen (ADR 0021/0015).

Neue Testdatei `src/Tests/ForceImportCandidateTests.fs` mit drei Testfällen. Der zentrale Test
baut **drei** `SyncTransaction` mit verschiedenen `TransactionId`, bildet beide Vorbereitungen
über echte `prepareTransactions`-Aufrufe und vergleicht je `TransactionId` über einen
Map-Lookup auf `c.Prepared.TransactionId`, nicht über Listenposition. Kein Erwartungswert steht
im Test; beide Seiten entstehen aus dem Produktionscode.

`dotnet test`: 966 erfolgreich, 7 übersprungen, 0 Fehler.

**Iteration 1 wurde verworfen (FAIL), und der Grund ist lehrreich.** Der erste Entwurf baute
eine einzige Transaktion und deckte von Kriterium 5 nur eine der vier Zusicherungen ab. Der
Verifier hat die Kosten davon belegt statt sie zu behaupten: ersetzt man in
`buildComdirectImportCandidates` das `Map.find p.TransactionId byTransactionId` durch
`List.head transactions`, zerstört also die Zuordnung zwischen Rohdaten und Vorbereitung,
blieben **alle vier Tests grün**. Bei einer einelementigen Liste ist `List.head` zufällig
richtig.

**Iteration 2 (PASS)**, und der Worker hat dabei eine Lücke im vorgeschlagenen Fix selbst
gefunden. Er hat die Mutationsprobe gefahren, bevor er berichtete, und festgestellt: **drei
Transaktionen allein reichen nicht.** `ExistingEntryImportId` hängt allein an
`p.TransactionId`, nicht an `tx`; der reine Drift-Vergleich kann eine falsche `tx`-Zuordnung
also gar nicht sehen, unabhängig von der Anzahl der Transaktionen. Erst eine Zusicherung auf
ein Feld aus `tx` bindet die Zuordnung mit ein. Er hat `ComdirectReference` je `TransactionId`
ergänzt; zweite Probe: rot, an der erwarteten Stelle
(`expected: REF-2, actual: REF-1`). Der Verifier hat das unabhängig nachgestellt und die
Begründung bestätigt: von den Kandidatenfeldern kommen `Prepared`, `Payee`, `Memo` und
`ExistingEntryImportId` aus `p`, nur `ComdirectReference`, `Date`, `Amount` und `ReviewSteps`
aus `tx`.

**Hinweis für spätere Arbeit an dieser Datei:** `ComdirectReference` ist im zentralen Test das
einzige je Transaktion **verschiedene** `tx`-Feld (`Date`, `Amount` und `ReviewSteps` sind dort
in allen drei Fixtures gleich) und bindet die Zuordnung deshalb allein. Das reicht, ist aber
eine dünne Naht: wer die Fixture später umbaut, sollte sie nicht versehentlich einebnen.

Kriterium 5 ist jetzt vollständig, und die Fixture beweist etwas: `BookingDate` (2026-09-18)
weicht bewusst von `Fixtures.today` (2026-09-21) ab, und `ReviewSteps` trägt einen Schritt.
Sonst käme eine Implementierung durch, die `today` einsetzt oder `[]` zurückgibt. Der Verifier
hat alle drei einzeln mutiert (`Date = DateTime.Today`, Amount vorzeichenverdreht,
`ReviewSteps = []`), je rot. Kriterium 3 steht jetzt wörtlich als
`normalCandidates |> List.forall (fun c -> c.ExistingEntryImportId = None)`.

**Ein Befund über den Kriteriumstext selbst, kein Fehler der Umsetzung.** Kriterium 1 nennt als
Beispiel-Drift "etwa ein `generateImportId`, das die Bindestriche der Guid stehen lässt". Das
ist mit zwei produktionsabgeleiteten Seiten **prinzipiell nicht fangbar**: der Normalzweig von
`prepareTransactions` ruft selbst `Shared.Domain.generateImportId tx.Transaction.Id`
(`YnabClient.fs:665`), und `Api.fs:764` ruft dieselbe Funktion mit demselben Wert; beide Seiten
bewegen sich gemeinsam. Nachgestellt: so mutiert bleiben alle Tests grün. Die **echte**
Divergenz fängt der Test dagegen sauber (Normalzweig auf `generateImportId ... + "-v2"`
mutiert → rot, `expected: Some "BB:txfq3kd1-v2", actual: Some "BB:txfq3kd1"`). Die Eigenschaft
ist also richtig gepinnt; wer das Kriterium später schärfen will, muss das **Beispiel**
ersetzen, nicht den Test verbiegen.

Ein Testfall ist in Iteration 2 bewusst entfallen ("Force-Import ohne bestehende Buchung sucht
nicht"), aufgegangen im `List.forall` des zentralen Tests, wie vom Verifier vorgeschlagen. Die
Zahl der `Expect`-Zusicherungen steigt dabei von 9 auf 13: die Datei ist stärker geworden, nicht
nur umgestellt.

Nicht korrigiert, als bekannter Makel notiert: eine in Iteration 1 hinzugefügte Kommentarzeile
(`ForceImportCandidateTests.fs:11`) trägt einen Gedankenstrich. Der Verifier hat sie nicht
beanstandet, und der Bestand der Nachbardateien nutzt sie ebenfalls (`YnabClient.fs`: zwölf
Stellen). Eine von dreizehn still zu ändern wäre weniger ehrlich als sie zu benennen.
