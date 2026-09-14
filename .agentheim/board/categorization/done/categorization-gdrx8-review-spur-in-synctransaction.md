---
id: categorization-gdrx8
title: "Review-Spur: SyncTransaction hält Kategorisierungs-Schritte mit Zeitpunkt und Herkunft fest"
status: done
type: feature
context: categorization
created: 2026-09-11
completed: 2026-09-11
depends_on: []
blocks: [ledger-qvt84]
tags: [categorization, history, sync-session]
related_adrs: [0021]
related_research: []
prior_art: []
---

## Why
Roman hat entschieden (2026-09-11), dass Regel-Vorschlag und Romans Korrektur beim Push
als Ereignisfolge im Kassenbuch sichtbar werden ("Regel X: Lebensmittel", dann "Roman in
BB: Drogerie"). So sieht man später, welche Regeln danebenliegen. Heute überschreibt
`categorizeTransaction` (`src/Server/Api.fs`) Kategorie und `MatchedRuleId` still; eine
vorherige Regel-Entscheidung ist danach nicht mehr rekonstruierbar. Da die
Kassenbuch-Buchung laut ADR 0021 erst mit dem Push entsteht, muss die Review-Spur bis
dahin in der SyncSession mitgeführt werden.

## What
1. `SyncTransaction` (`src/Shared/Domain.fs`) um ein Feld `ReviewSteps: ReviewStep list`
   erweitern:
   ```
   type ReviewActor = ByRule of RuleId * ruleName: string | ByUser
   type ReviewStep = {
       At: DateTime
       By: ReviewActor
       Category: YnabCategoryId option
       CategoryName: string option
       Splits: TransactionSplit list option
       PayeeOverride: string option
   }
   ```
   Die Reihenfolge (ältester Schritt zuerst empfohlen, damit sie direkt in
   Ereignis-Reihenfolge übersetzbar ist) beim Implementieren festlegen und im Code
   dokumentieren.
2. Jeder Schritt, der Kategorie, Splits oder PayeeOverride ändert (Regel-Anwendung,
   `categorizeTransaction`, `clearSplit`, Aufrufe aus dem Split-Editor), hängt einen
   Schritt an, statt nur den aktuellen Stand zu überschreiben. Eine Wiederholung mit
   demselben Wert erzeugt keinen neuen Schritt.
3. Persistenz (`Persistence.SyncTransactions`) um die Review-Spur erweitern.
4. Für Roman sichtbar ändert sich in diesem Task nichts; die Spur wird nur mitgeführt.
   `ledger-qvt84` liest sie beim Push.

## Acceptance criteria
- [x] Regel-Anwendung gefolgt von manueller Korrektur ergibt zwei Einträge in
      `ReviewSteps` mit unterschiedlichem `At` und `By`.
- [x] Eine erneute Kategorisierung mit demselben Wert erzeugt keinen weiteren Eintrag.
- [x] Die Review-Spur übersteht einen Server-Neustart (Persistenz-Test gegen
      In-Memory-SQLite).
- [x] Reducer- und Client-Code kompilieren mit dem erweiterten Typ; keine Tests über
      Pfade, die den Fable.Remoting-Proxy berühren.
- [x] `dotnet build`, `dotnet test` und `npm run build` grün.

## Notes
Context `categorization` gewählt, weil die Schritte fachlich dessen Sprache sind
(`AutoCategorized`, `ManualCategorized`, Regel); der Lebenszyklus der SyncSession selbst
bleibt unverändert (Empfehlung strategic-modeler, 2026-09-11). Kann parallel zu
`ledger-cscw1` laufen.

## Outcome
`ReviewActor`/`ReviewStep` und das Feld `SyncTransaction.ReviewSteps` sind in
`src/Shared/Domain.fs` angelegt, zusammen mit der reinen Hilfsfunktion
`appendReviewStep (at) (by) (tx)`: sie hängt einen Schnappschuss aus
Kategorie/Splits/PayeeOverride an, außer der letzte Schritt trägt bereits denselben
Stand (keine Wiederholungs-Einträge). `Server.RulesEngine.classifyTransactions` nimmt
jetzt einen `now: DateTime`-Parameter und hängt bei einem Regel-Treffer einen
`ByRule`-Schritt an; `categorizeTransaction`, `bulkCategorize`, `splitTransaction` und
`clearSplit` (`src/Server/Api.fs`) hängen je einen `ByUser`-Schritt an. Persistenz:
`sync_transactions` hat eine neue Spalte `review_steps` (TEXT, JSON via
handgeschriebenem Thoth.Json.Net-Codec `Persistence.SyncTransactions.ReviewStepJson`,
eigenständig statt eines gemeinsamen Codecs mit `Ledger.Events.Codec`, um die
Upstream/Downstream-Richtung zwischen `categorization` und `ledger` nicht umzukehren);
eine `ALTER TABLE ... ADD COLUMN`-Migration mit Duplicate-Column-Fangnetz deckt
bestehende Datenbanken ab, fehlende/nicht lesbare Spalten laden als leere Liste.

**Iteration 2** (nach Verifier-FAIL und Romans Entscheidung zu den Order-ID-Urhebern,
siehe `## Entscheidung Roman` unten): `ReviewActor` bekam einen dritten Fall
`ByOrderIdMatch of orderId: string`. `OrderIdMatcher.applySuggestions` hängt jetzt einen
`ByOrderIdMatch`-Schritt an (Kategorie aus der YNAB-Historie derselben Bestellung, zur
Abrufzeit, eigener Urheber, weil weder Regel noch Roman diese Buchung entschieden
haben); `OrderIdMatcher.propagateInSession` hängt einen `ByUser`-Schritt an (Roman hat
auf der Quell-Buchung entschieden, BB überträgt seine Entscheidung nur auf die
Geschwister derselben Bestellung). Beide Funktionen nehmen jetzt einen Zeitpunkt-
Parameter (`now`/`at`); `categorizeTransaction` (`Server.Api`) verwendet denselben
`now` für den eigenen Schritt und für die Propagation, weil beides Teil derselben
Kategorisierungs-Aktion ist. `Persistence.SyncTransactions.ReviewStepJson` um den
Diskriminator `"orderIdMatch"` erweitert; bestehende, ältere Zeilen ohne diesen
Diskriminator laden unverändert weiter (rein additive Codec-Erweiterung, keine
Migration nötig). Das Backlog-Item aus Iteration 1 ist damit in diesem Task
aufgegangen und wurde vom Auftraggeber gelöscht.

Tests: 13 neue Testfälle insgesamt über beide Iterationen
(`src/Tests/RulesEngineTests.fs`: `classifyTransactionsReviewStepsTests`,
`appendReviewStepTests`; `src/Tests/PersistenceTypeConversionTests.fs`:
`ReviewSteps Conversions`, inkl. `ByOrderIdMatch`-Roundtrip; `src/Tests/OrderIdMatcherTests.fs`:
je ein Test für `ByOrderIdMatch` und `ByUser` als Actor, plus ein Regressionstest
"clearSplit, dann Propagation" aus der Verifier-Note, der belegt, dass der letzte
Review-Schritt dem tatsächlich gepushten Stand entspricht), plus mechanische
`ReviewSteps = []`-Ergänzungen in allen bestehenden `SyncTransaction`-Literalen
(Server- und Testcode), damit der erweiterte Typ überall kompiliert. Rot-Grün per
Sabotage-Check verifiziert: iteration 1 (no-op `appendReviewStep`) ließ genau die 4
davon abhängigen Tests fehlschlagen; iteration 2 (die beiden neuen
`appendReviewStep`-Aufrufe in `OrderIdMatcher.fs` entfernt) ließ genau die 3 davon
abhängigen Tests fehlschlagen, alle anderen blieben grün.

Dateien: `src/Shared/Domain.fs`, `src/Server/RulesEngine.fs`, `src/Server/Api.fs`,
`src/Server/OrderIdMatcher.fs`, `src/Server/Persistence.fs`,
`src/Tests/RulesEngineTests.fs`, `src/Tests/OrderIdMatcherTests.fs`,
`src/Tests/PersistenceTypeConversionTests.fs`, plus `ReviewSteps = []`-Ergänzungen in
`src/Tests/DuplicateDetectionTests.fs`, `src/Tests/SplitPushTests.fs`,
`src/Tests/SplitTransactionTests.fs`, `src/Tests/SyncFlowViewTests.fs`,
`src/Tests/YnabClientTests.fs`, `src/Tests/SyncSessionManagerTests.fs`.

## Diary entry

## 2026-09-11 12:45 - Review-Spur: SyncTransaction hält Kategorisierungs-Schritte fest

**What I did:**
`SyncTransaction` um `ReviewSteps: ReviewStep list` erweitert (neue Typen
`ReviewActor`/`ReviewStep` plus reine Hilfsfunktion `appendReviewStep` in
`src/Shared/Domain.fs`). Jeder Schreibpfad, der Kategorie, Splits oder PayeeOverride
ändert (Regel-Anwendung, `categorizeTransaction`, `bulkCategorize`, `splitTransaction`,
`clearSplit`), hängt jetzt einen Schritt mit Zeitpunkt und Herkunft an; eine
Wiederholung mit unverändertem Stand erzeugt keinen neuen Eintrag. Persistenz um eine
neue Spalte `review_steps` (JSON, eigener Thoth.Json.Net-Codec) erweitert, mit
Migration für bestehende Datenbanken.

**Files Added:**
- Backlog-Item für die Lücke bei Order-ID-Propagation (kein ReviewStep dort), vom
  Conductor auf `categorization-mgnjp` normalisiert. In Iteration 2 direkt in diesem
  Task behoben (siehe Diary-Eintrag Iteration 2 unten); das Backlog-Item ist damit
  aufgegangen und wurde vom Conductor gelöscht.

**Files Modified:**
- `src/Shared/Domain.fs` - `ReviewActor`, `ReviewStep`, `SyncTransaction.ReviewSteps`,
  `appendReviewStep`
- `src/Server/RulesEngine.fs` - `classifyTransactions` nimmt `now: DateTime`, hängt bei
  Regel-Treffer einen `ByRule`-Schritt an
- `src/Server/Api.fs` - `categorizeTransaction`, `bulkCategorize`, `splitTransaction`,
  `clearSplit` hängen `ByUser`-Schritte an; `classifyTransactions`-Aufruf mit
  `DateTime.UtcNow`
- `src/Server/Persistence.fs` - Spalte `review_steps`, Migration, Codec
  `ReviewStepJson`, Save/Load erweitert
- `src/Tests/RulesEngineTests.fs` - neue Testlisten für `classifyTransactions`-
  ReviewSteps und `appendReviewStep`
- `src/Tests/PersistenceTypeConversionTests.fs` - neue Testliste "ReviewSteps
  Conversions" (Roundtrip inkl. Splits, leere Spur)
- `src/Tests/DuplicateDetectionTests.fs`, `OrderIdMatcherTests.fs`, `SplitPushTests.fs`,
  `SplitTransactionTests.fs`, `SyncFlowViewTests.fs`, `YnabClientTests.fs`,
  `SyncSessionManagerTests.fs` - `ReviewSteps = []` in bestehenden
  `SyncTransaction`-Literalen ergänzt (mechanisch, Typ erweitert)
- `.agentheim/contexts/categorization/README.md` - Ubiquitous-Language-Eintrag
  "Review-Spur (`ReviewSteps`)", Relationships-Absatz aktualisiert

**Files Deleted:**
- keine

**Rationale:**
ADR 0021 verlangt, dass die Kassenbuch-Buchung beim Push als Ereignisfolge
("Regel X: ...", dann "Roman in BB: ...") entsteht; da die Buchung selbst erst beim
Push existiert, muss die SyncSession bis dahin Buch führen, wer wann welchen
Kategorisierungs-Stand gesetzt hat. `ledger-qvt84` liest diese Spur beim Push.

**Outcomes:**
- Build: dotnet build (Solution) 0 Warnungen/0 Fehler; npm run build grün
- Tests: 725/725 bestanden (7 übersprungen, .env-abhängig), 8 davon neu
- Issues: keine; Rot-Grün per temporärer Sabotage von `appendReviewStep` verifiziert
  (4 betroffene Tests schlugen erwartungsgemäß fehl, danach wiederhergestellt)

## 2026-09-11 16:33 - Review-Spur, Iteration 2: Order-ID-Pfade schließen die Lücke

**What I did:**
Nach dem Verifier-FAIL aus Iteration 1 (zwei Order-ID-Pfade änderten die Kategorie
ohne ReviewStep) und Romans Entscheidung zu den Urhebern: `ReviewActor` um
`ByOrderIdMatch of orderId: string` erweitert. `OrderIdMatcher.applySuggestions` hängt
jetzt `ByOrderIdMatch`-Schritte an (Kategorie aus der YNAB-Historie derselben
Bestellung), `OrderIdMatcher.propagateInSession` hängt `ByUser`-Schritte an (Romans
Entscheidung, nur übertragen). Beide Funktionen nehmen jetzt einen Zeitpunkt-Parameter;
`categorizeTransaction` verwendet denselben Zeitpunkt für den eigenen Schritt und für
die Propagation. `ReviewStepJson`-Codec um den Diskriminator `"orderIdMatch"`
erweitert (additiv, keine Migration nötig, ältere Zeilen laden unverändert). Das aus
Iteration 1 entstandene Backlog-Item ist damit in diesem Task aufgegangen und wurde vom
Conductor gelöscht. Stilkorrekturen aus der Verifier-Note nachgezogen (Umlaute statt
Umschreibungen in eigenen Kommentaren).

**Files Added:**
- keine

**Files Modified:**
- `src/Shared/Domain.fs` - `ReviewActor` um `ByOrderIdMatch of orderId: string`
  erweitert
- `src/Server/OrderIdMatcher.fs` - `applySuggestions` und `propagateInSession` nehmen
  einen Zeitpunkt-Parameter und hängen Review-Schritte an (`ByOrderIdMatch` bzw.
  `ByUser`)
- `src/Server/Api.fs` - `classifyTransactions`- und `applySuggestions`-Aufrufe mit
  `DateTime.UtcNow`; `categorizeTransaction` teilt sich einen `now` zwischen eigenem
  Schritt und Propagation
- `src/Server/Persistence.fs` - `ReviewStepJson`-Codec um `"orderIdMatch"` erweitert;
  Umlaute im Modul-Kommentar korrigiert (waren als "fuer"/"eigenstaendig"/"wuerde"
  umschrieben)
- `src/Tests/OrderIdMatcherTests.fs` - Aufrufe von `applySuggestions`/
  `propagateInSession` um den neuen Zeitpunkt-Parameter ergänzt; vier neue Testfälle
  (`ByOrderIdMatch`-Actor, kein Schritt ohne Vorschlag, `ByUser`-Actor bei Propagation,
  Regressionstest "clearSplit, dann Propagation")
- `src/Tests/PersistenceTypeConversionTests.fs` - ein neuer Testfall für den
  `ByOrderIdMatch`-Roundtrip
- `.agentheim/contexts/categorization/README.md` - Review-Spur-Eintrag um den dritten
  Actor und alle fünf Schreibpfade ergänzt
- `.agentheim/contexts/categorization/doing/categorization-gdrx8-review-spur-in-synctransaction.md` -
  Outcome/Diary für Iteration 2, stale Referenz auf das aufgegangene Backlog-Item
  bereinigt

**Files Deleted:**
- keine (das Backlog-Item wurde bereits vom Conductor gelöscht, bevor dieser Task
  erneut dispatcht wurde)

**Rationale:**
Der Verifier hat zu Recht bemängelt, dass eine über Order-ID kategorisierte Buchung
ohne ReviewStep gepusht worden wäre: `ledger-qvt84` baut die Ereignisfolge
ausschließlich aus `ReviewSteps`, eine Lücke dort hätte eine Kategorie in YNAB ohne
passendes Kassenbuch-Ereignis bedeutet. Roman hat die fehlende fachliche Entscheidung
(welcher Urheber für welchen Order-ID-Pfad) getroffen; die Umsetzung folgt ihr 1:1.

**Outcomes:**
- Build: `dotnet build` (Solution) 0 Warnungen/0 Fehler; `npm run build` grün
- Tests: 730/737 bestanden, 7 übersprungen (.env-abhängig), 0 fehlgeschlagen; 5 davon
  neu in dieser Iteration (13 insgesamt über beide Iterationen)
- Issues: keine; Rot-Grün per temporärer Sabotage der beiden neuen
  `appendReviewStep`-Aufrufe in `OrderIdMatcher.fs` verifiziert (genau die 3 davon
  abhängigen Tests schlugen fehl, danach wiederhergestellt)

---

## Verifier note (iteration 1)

**REASONS:**
- `## What` Punkt 2 verlangt, dass jeder Schritt, der Kategorie, Splits oder PayeeOverride ändert, einen ReviewStep anhängt. Die Klammer dort ist eine Beispielliste; der Worker hat `bulkCategorize` und `splitTransaction` (ebenfalls nicht genannt) einbezogen, die Order-ID-Pfade aber ausgelassen. Zwei Pfade ändern `CategoryId`/`CategoryName` ohne Schritt: `OrderIdMatcher.applySuggestions` (beim Abruf aus der YNAB-Historie, `src/Server/Api.fs:800`) und `OrderIdMatcher.propagateInSession` (aus `categorizeTransaction`, `src/Server/Api.fs:895`).
- Das bricht den Konsumenten auf einem realistischen Pfad: `importToYnab` pusht `Pending`- und `NeedsAttention`-Buchungen (`src/Server/Api.fs:1033`). Eine Amazon-Geschwisterbuchung mit propagierter Kategorie geht mit Kategorie und leerer `ReviewSteps` nach YNAB; `ledger-qvt84` baut die Ereignisse allein aus `ReviewSteps`. Das Kassenbuch bekäme kein Kategorisierungs-Ereignis, YNAB aber die Kategorie. Zweiter Fall: nach `clearSplit` (Schritt mit Kategorie None) setzt eine spätere Propagation Kategorie X; der letzte Schritt der Spur widerspricht dann dem gepushten Stand.
- `categorization-mgnjp (aufgegangen in Iteration 2)` hat `blocks: []`; nichts hindert `ledger-qvt84` am Bau auf der unvollständigen Spur. Der README-Eintrag "Review-Spur" nennt die Lücke nicht.
- Die Korrektur braucht eine fachliche Entscheidung, die der Task nicht trifft: `ReviewActor` (`ByRule | ByUser`) hat keinen passenden Fall für `applySuggestions` (Kategorie aus der YNAB-Historie, weder Regel noch Roman in BB). `propagateInSession` könnte `ByUser` sein, auch das ist eine fachliche Entscheidung.
- Stil: Kommentar `src/Server/Persistence.fs:614-616` mit "fuer", "eigenstaendig", "wuerde" statt Umlauten; `categorization-mgnjp (aufgegangen in Iteration 2)-...md:32` mit "—" als Satzzeichen; Zeile 52 "ein Lücke" statt "eine Lücke".
- In Ordnung: alle fünf Akzeptanzkriterien getestet; `dotnet build`, `dotnet test` (725 bestanden, 7 übersprungen), `npm run build` grün; Testdatei-Änderungen sind mechanische `ReviewSteps = []`-Ergänzungen; Reihenfolge dokumentiert; Migration und NULL-Zeilen laden als leere Liste; Gleicher-Wert-Regel umgesetzt und getestet.

**SUGGESTED_FIX:** Roman (oder der tactical-modeler) entscheidet, welche Herkunft eine Order-ID-Kategorie bekommt (`ByUser` für die Propagation in der Session, ein neuer `ReviewActor`-Fall für den Vorschlag aus der YNAB-Historie, oder ausdrücklich zurückstellen). Danach entweder beide `OrderIdMatcher`-Pfade `appendReviewStep` aufrufen lassen (Typ und `ReviewStepJson` bei Bedarf erweitern, mit Tests) oder, falls zurückgestellt, `categorization-mgnjp (aufgegangen in Iteration 2)` als Blocker von `ledger-qvt84` eintragen und die Lücke im README dokumentieren. Außerdem die Umlaut-Umschreibungen im `Persistence.fs`-Kommentar und Gedankenstrich und Grammatik im Backlog-Item korrigieren.

**ITERATION_HINT:** task-under-specified

## Entscheidung Roman (2026-09-11, nach Verifier-Eskalation)

Frage: Welchen Urheber tragen Kategorien aus dem Order-ID-Abgleich in der Review-Spur?
Antwort: **je nach Weg.**
- `OrderIdMatcher.propagateInSession` (Roman kategorisiert eine Buchung, BB überträgt auf
  die Geschwister derselben Bestellung): Schritt mit `ByUser`. Die Entscheidung ist Romans,
  BB überträgt sie nur.
- `OrderIdMatcher.applySuggestions` (beim Abruf, Kategorie aus der YNAB-Historie derselben
  Bestellung): eigener Urheber. `ReviewActor` bekommt einen dritten Fall, z. B.
  `ByOrderIdMatch of orderId: string`; Anzeigename "Order-ID-Abgleich". Korrigiert Roman
  danach in der Review, folgt ein Schritt `ByUser` wie sonst.

Damit gilt für Iteration 2 zusätzlich:
- Beide Order-ID-Pfade hängen Schritte an (gleiche Regel wie überall: gleicher Stand ergibt
  keinen neuen Schritt). `ReviewStepJson`/Codec und Migration um den neuen Fall erweitern.
- Tests: je Pfad ein Test, der den Schritt mit dem richtigen Urheber belegt; ein Test für
  "clearSplit, danach Propagation" (letzter Schritt entspricht dem gepushten Stand).
- Das Backlog-Item `categorization-mgnjp (aufgegangen in Iteration 2)` ist damit in diesem Task aufgegangen und wird
  gelöscht.
- README-Eintrag "Review-Spur": alle Pfade nennen, die Schritte anhängen, und den neuen
  Urheber.
- Stilfehler aus der Verifier-Note beheben (Umlaute im `Persistence.fs`-Kommentar).

## Worker response (iteration 2, 2026-09-11 16:33)

Beide vom Verifier gefundenen Lücken sind geschlossen:
- `OrderIdMatcher.applySuggestions` hängt `ByOrderIdMatch orderId` an (neuer dritter
  `ReviewActor`-Fall, wie von Roman entschieden).
- `OrderIdMatcher.propagateInSession` hängt `ByUser` an.
- `ReviewStepJson`-Codec deckt `ByOrderIdMatch` ab (additiv, ältere Zeilen laden
  unverändert).
- Regressionstest "clearSplit, dann Propagation" belegt: der letzte Review-Schritt
  entspricht dem tatsächlich gepushten Stand (kein Widerspruch mehr möglich).
- README-Eintrag "Review-Spur" nennt jetzt alle fünf Schreibpfade und alle drei Actor-
  Fälle.
- Stilkorrekturen übernommen: Umlaute im `Persistence.fs`-Modul-Kommentar; die
  bemängelten Stellen im mittlerweile gelöschten Backlog-Item entfallen mit der Datei.
- Das Backlog-Item aus Iteration 1 (`categorization-mgnjp`) ist vollständig in diesem
  Task aufgegangen; keine neue Backlog-Datei nötig.

`dotnet build` (Solution), `dotnet test` (730/737, 7 übersprungen, 0 Fehler) und
`npm run build` erneut grün.
