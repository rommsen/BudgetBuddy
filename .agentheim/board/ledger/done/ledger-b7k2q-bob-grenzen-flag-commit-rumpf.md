---
id: ledger-b7k2q
title: "Bob-Grenzen vollständig: Commit prüft das Datumsfenster am Berliner Tag des Commits neu, Vorschau begrenzt Rumpf auf 8192 Bytes und Kategorie auf 200 Zeichen, „kein Flag“ im gesendeten Rumpf belegt, API-Test des Datumsfensters auf einen Fall gekürzt (BotApi.fs bookWithinLimit, readBody, Validation.fs parseBotPreviewRequest)"
status: done
type: bug
context: ledger
created: 2026-11-19
completed:
depends_on: []
blocks: []
tags: [bestandsreview, backend, tests, bot]
related_adrs: [0066, 0068, 0097]
related_research: []
prior_art: [ledger-ssmp3, ledger-ay81j, ledger-z71ka]
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G1 (Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`), von Roman
zur Task gewählt. Höchste Schwere hoch.

Die Grenzen der Bot-Schnittstelle (ADR 0066 Punkt 5, ADR 0068, CLAUDE.md Security-Fallen) sind an vier Stellen nicht
vollständig gehalten oder belegt:

- „Bob-Buchungen bekommen kein Flag“ (ADR 0068 Punkt 5) prüft kein Test. Gäbe `botPushOn` die Lena-Farbe an
  `createTransactionOn`, bliebe die Suite grün, und Bobs Buchungen landeten in YNAB als „Lena fragen“.
- Der Commit prüft das Datumsfenster nicht neu. Eine Vorschau um 23:55 Berliner Zeit mit Datum heute minus 31 Tage,
  committet um 00:05, bucht 32 Tage zurück (ADR 0068 Punkt 3).
- Die Vorschau liest den Rumpf ohne eigene Obergrenze (Kestrel-Vorgabe rund 30 MB) und `category` ohne Längengrenze;
  die 422-Meldung gibt `category` zurück. Payee (200) und Memo (500) sind schon begrenzt.
- Der API-Test des Datumsfensters wiederholt die Fälle der Domänentests (test-doctrine.md Punkt 2).

## What
- **Datumsfenster beim Commit** (`src/Server/BotApi.fs`, `bookWithinLimit`, heute Zeile 381): vor der Tagesgrenze
  `Ledger.Bot.Domain.checkBotBookingDate (berlinDayOf deps.BerlinZone now) p.Date` mit dem Zeitpunkt des Commits.
  Ein `now` je Aufruf, das auch an `loadDailyUsage` und `addCashBookingWith` geht (heute drei Aufrufe von
  `deps.Now()`). Ablehnung als neuer Fall `BotDateNotAllowed of message: string` in `BotCommitOutcome`, im
  `commitHandler` 422 mit Code `date_not_allowed` und dem Satz der Domäne („Das Datum darf höchstens 31 Tage
  zurückliegen“). Die Prüfung steht in `bookWithinLimit`, also nach `bookedFromLedger`: eine schon gespeicherte Buchung
  wird bei einem erneuten Commit nach Mitternacht weiter mit 200 gemeldet (wie bei der Tagesgrenze, ledger-z71ka).
- **Rumpfgrenze der Vorschau** (`readBody`, heute Zeile 296): BotApi liest höchstens `botMaxBodyBytes + 1` Bytes aus
  `ctx.Request.Body`. Sind es mehr als `botMaxBodyBytes`, antwortet die Vorschau 400 `invalid_request` mit
  `fields: [{ "field": "body", "message": "Der Rumpf darf höchstens 8192 Bytes haben" }]`, ohne JSON zu parsen, ohne
  YNAB-Aufruf, ohne Quick-Add-Auflösung, ohne gespeicherte Vorschau. Konstante `botMaxBodyBytes = 8192` und der Satz
  stehen im Bot-Abschnitt von `src/Server/Validation.fs` (neben `botForbiddenAccountFields`), fest im Code.
- **Kategorie-Grenze** (`src/Server/Validation.fs`, `parseBotPreviewRequest`, heute Zeile 486
  `requiredString "category"` ohne Länge): wie Payee höchstens 200 Zeichen nach `Trim`, sonst Feldfehler `category`
  „Höchstens 200 Zeichen“, gesammelt mit den übrigen Feldfehlern (400 `invalid_request`). Damit ist auch die
  Rückgabe in der 422-Meldung (`category_not_found`, `category_ambiguous`, BotApi.fs Zeile 341 ff.) begrenzt.
- **Kein Flag belegen** (`src/Tests/LedgerCashBookingTests.fs`, Test „Bobs Commit pusht den Request der Barbuchung auf
  das YNAB-Konto der Vorschau“, heute Zeile 642): der gesendete Rumpf (`handler.Bodies[0]`) trägt keinen Schlüssel
  `flag_color` (`transactionKeys`). Kein Produktionscode ändert sich dafür, `botPushOn` gibt heute schon `None` weiter
  (BotApi.fs Zeile 547).
- **API-Test des Datumsfensters kürzen** (`src/Tests/BotApiTests.fs`, heute Zeile 773): nur noch ein Fall außerhalb
  des Fensters (heute minus 32 Tage) mit 400 und Feld `date`. „heute“, „heute minus 31 Tage“ und „morgen“ stehen in
  `src/Tests/BotDomainTests.fs` (Liste „Bot Datumsfenster“, Zeilen 38 und 42), die Sammlung mit anderen Feldfehlern in
  `src/Tests/BotValidationTests.fs` (Zeile 79), der Berliner Tag an der API im Test „Berliner Tag: …“ (Zeile 782).

Entscheidungen Conductor 2026-11-19, nach Empfehlung:
- Rumpfgrenze über eigenes Lesen mit Limit in BotApi. Verworfen: `KestrelServerOptions.Limits.MaxRequestBodySize`
  (gilt für den ganzen Host, auch `/api` mit Fable.Remoting) und `IHttpMaxRequestBodySizeFeature` je Anfrage (fehlt im
  `DefaultHttpContext` der Tests, Kestrel wirft dann eine Ausnahme außerhalb des JSON-Fehlerformats). Das eigene Lesen
  folgt dem Muster der Bot-Schnittstelle: alle Antworten als JSON über `respond` und `errorBody`, Prüfung nach Token
  und Sperre, testbar mit `MemoryStream`.
- 8192 statt der im Bericht genannten rund 4 KB: die längste gültige Anfrage (Payee 200, Kategorie 200, Memo 500
  Zeichen, jedes Zeichen als `\uXXXX` maskiert, also 6 Bytes) braucht rund 5,5 KB. Mit 8192 lehnt die Grenze nie eine
  sonst gültige Anfrage ab.
- 400 mit Feld `body` statt 413: ein unlesbarer Rumpf meldet heute schon 400 mit Feld `body` („Der Rumpf ist kein
  gültiges JSON“). Bob wertet `fields` aus, ein eigener Status bräuchte einen eigenen Zweig.
- Commit außerhalb des Fensters 422, nicht 400: die Anfrage selbst ist gültig, der Zustand lehnt ab, wie
  `daily_limit_reached` beim Commit. Das Ergebnis bleibt im Commit-Slot (`CommitOnce`), ein zweiter Commit derselben
  Vorschau liefert wieder 422; Bob legt eine neue Vorschau an und bekommt dort 400 mit Feld `date`.
- Commit und `GET /bot/categories` lesen keinen Rumpf, die Grenze betrifft nur die Vorschau.
- Keine der Grenzen wird konfigurierbar (keine Umgebungsvariable, kein Setting), sie werden nur strenger (ADR 0068
  Nachtrag).

## Acceptance criteria
- [ ] Regressionstest, vor dem Fix rot (`src/Tests/BotApiTests.fs`, Liste „Bot-Schnittstelle (Routen)“, neben dem
      Datumsfenster; Kommentar nennt den Bug): GIVEN eine `Env`, deren Uhr 5 Minuten vor dem Ende des Berliner Tags D
      steht (Grenze über `Ledger.Bot.Domain.berlinDayBoundsUtc`, nicht als feste UTC-Stunde, weil die Testtage über
      Sommer- und Winterzeit verteilt sind), und eine Vorschau mit Datum D minus 31 Tage (200), WHEN die Uhr auf
      5 Minuten nach Mitternacht gestellt und die Vorschau committet wird, THEN 422 mit `error = "date_not_allowed"`
      und dem Satz „Das Datum darf höchstens 31 Tage zurückliegen“, `env.Pushes = 0`, keine Buchung für den Payee
      (`entriesFor`). Gegenprobe im Outcome: Prüfung aus `bookWithinLimit` entfernt, Test rot.
- [ ] Intakt-Fall im selben Aufbau: Vorschau mit D minus 30 Tage vor Mitternacht, Commit nach Mitternacht ergibt 200
      und genau einen Push.
- [ ] Reihenfolge belegt (wie die Tests „Ausnahme nach dem Buchen …“ ab Zeile 539 mit `failingOnce`): GIVEN Vorschau
      mit D minus 31 Tage und ein Buchungs-Port, der beim ersten Commit vor Mitternacht nach dem Schreiben wirft
      (`AfterWrite`), WHEN der erneute Commit nach Mitternacht kommt, THEN 200 mit derselben `entry_id`, kein zweiter
      Push. Mit `BeforeWrite` ergibt derselbe Ablauf 422 `date_not_allowed` und keine Buchung. Gegenprobe im Outcome:
      Datumsprüfung vor `bookedFromLedger` gezogen, der `AfterWrite`-Fall rot.
- [ ] Regressionstest, vor dem Fix rot (`src/Tests/BotApiTests.fs`): GIVEN ein gültiger Vorschau-Rumpf, mit Leerraum
      auf 8193 Bytes aufgefüllt, WHEN `POST /bot/transactions/preview` mit gültigem Token, THEN 400 `invalid_request`,
      `fields` genau `[ body ]` mit „Der Rumpf darf höchstens 8192 Bytes haben“, `env.CategoryLoads = 0`,
      `env.Deps.Store.Count = 0`. Intakt: derselbe Rumpf auf genau 8192 Bytes ergibt 200.
- [ ] Ohne Token ergibt derselbe übergroße Rumpf 401 (die Grenze greift erst nach Anmeldung und Sperre, die
      Reihenfolge in `handler` bleibt).
- [ ] Regressionstest, vor dem Fix rot (`src/Tests/BotValidationTests.fs`, neben „zu langer Payee und zu langes Memo
      werden abgelehnt“): `parseBotPreviewRequest` mit `category` aus 201 Zeichen liefert den Feldfehler `category`
      „Höchstens 200 Zeichen“, gesammelt mit einem zweiten Feldfehler derselben Anfrage (etwa `payee` mit 201 Zeichen,
      `fieldsOf` ergibt `[ "category"; "payee" ]`); 200 Zeichen gehen, Leerraum am Rand zählt nicht (wie bei Payee).
- [ ] `src/Tests/LedgerCashBookingTests.fs`, Test „Bobs Commit pusht den Request der Barbuchung …“: zusätzlich
      `transactionKeys handler.Bodies[0]` enthält kein `flag_color`. Gegenprobe im Outcome: in `botPushOn` das zweite
      `None` durch `Some "red"` ersetzt, Test rot, danach zurückgesetzt.
- [ ] `src/Tests/BotApiTests.fs`, Test „Datumsfenster …“ (heute Zeile 773): auf den einen Fall heute minus 32 Tage mit
      400 und Feld `date` gekürzt, Name entsprechend („Datumsfenster: heute minus 32 Tage ergibt 400 mit Feld date“).
      Die gestrichenen Fälle stehen in `BotDomainTests.fs` (Zeilen 38, 42); Outcome nennt das.
- [ ] Doc-Kommentare nachgezogen: `BotCommitOutcome` („gebucht oder wegen der Tagesgrenze abgelehnt“), `bookPreview`,
      `readBody`.
- [ ] README_DELTA für ledger: **Bot-Vorschau (Preview)** um Rumpfgrenze (8192 Bytes, 400 Feld `body`) und
      Kategorie-Grenze (200 Zeichen) ergänzt; **Bot-Commit** um „prüft das Datumsfenster am Berliner Tag des Commits
      neu, außerhalb 422 `date_not_allowed`, nach dem Blick ins Kassenbuch“. Geprüft mit
      `npm run check:readme-delta -- --bc ledger --delta <datei>`, Ergebnis im Outcome.
- [ ] `./verify.sh` mit `VERIFY: PASS`.

## Notes
- Herkunft: Bestandsreview ledger 2026-11-19, Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`, Gruppe G1
  (4 Befunde). Eine Task: alle vier betreffen die Grenzen derselben Schnittstelle in denselben Dateien, getrennt
  gebaut kollidierten sie in `BotApiTests.fs`.
- Urteile am Code vom 2026-11-19:
  - `LedgerCashBookingTests.fs:642` gilt: der Test prüft nur `import_id` (`hasImportId`), kein Test in
    `BotApiTests.fs`, `BotDomainTests.fs` oder `LedgerCashBookingTests.fs` nennt `flag_color` für Bob. Der Code ist
    richtig (`createTransactionOn http None None None`, die zweite `None` ist `flagColor`, YnabClient.fs:1238), es
    fehlt der Beleg. Teil der Pflichtliste Punkt 13.
  - `BotApi.fs:444` gilt, die Stelle liegt heute in `bookWithinLimit` ab Zeile 381 (Aufruf in Zeile 429). Nur das
    Zurückliegen kann beim Commit kippen: die Vorschau gilt 15 Minuten, ein Datum in der Zukunft wird durch Warten
    nicht neu. Möglich ist der Fall nur in den 15 Minuten vor Berliner Mitternacht.
  - `BotApi.fs:296` gilt: `readBody` liest mit `ReadToEndAsync` ohne Grenze; `Validation.fs:486` (im Bericht 478,
    das ist `botForbiddenAccountFields`) prüft `category` nur auf leer. Im ganzen Server gibt es keine
    Rumpfgrenze (kein `MaxRequestBodySize`), deshalb das eigene Lesen.
  - `BotApiTests.fs:773` gilt: der Test prüft heute, minus 31, minus 32 und morgen; drei davon stehen schon in
    `BotDomainTests.fs`, ein Zukunftsdatum an der API zusätzlich im Test „Berliner Tag“ (Zeile 787, übermorgen nach Berliner Tag).
- Die Commit-Tests nutzen Tage D und D+1 der `Env`; erlaubt ist D-1 bis D+2 (Kommentar `testDayStride`). Das
  Buchungsdatum D minus 31 zählt nicht für die Tagesgrenze, die nach dem Ereignis-Zeitstempel zählt.
- Rumpf-Lesen: Bytes zählen, nicht Zeichen. Den Puffer erst nach der Prüfung als UTF-8 lesen. Ein `Content-Length`
  allein reicht nicht, er kann fehlen oder lügen.
- Bob sieht nur neue Ablehnungen, die eine gültige Anfrage nie trifft (Rumpf, Kategorie) oder die nur um Mitternacht
  auftreten (Commit). Kein Client-Code, keine UI.
- Entschieden (Roman, 2026-11-19): ja, ablehnen. Frage war: Soll ein Commit nach Mitternacht mit einem Datum, das am Tag der Vorschau noch im Fenster lag,
  abgelehnt werden? Empfehlung: ja, wie hier beschrieben. ADR 0068 Punkt 3 misst „heute“, und beim Buchen ist heute
  der Tag des Commits; die Alternative (Fenster am Tag der Vorschau messen) ließe 32 Tage zu. Bob legt in dem seltenen
  Fall eine neue Vorschau an. Die Task ist nach dieser Empfehlung geschrieben; entscheidet Roman anders, entfallen die
  ersten drei Kriterien und die Notes-Zeile zu `BotApi.fs:444` wird ein „entfällt“.

## Verifier note (iteration 1)

**REASONS:**
- Check 6 (ADRs for decisions): die neue harte Grenze der Bot-Schnittstelle (`botMaxBodyBytes = 8192`, Lesen mit Limit in `readBody`, 400 `invalid_request` mit Feld `body`) steht in keinem ADR. ADR 0068 zählt Bobs Grenzen auf (nur strenger, nie konfigurierbar), die Rumpfgrenze fehlt dort. Die Gründe (8192 statt etwa 4 KB, eigenes Lesen statt `KestrelServerOptions.Limits.MaxRequestBodySize`/`IHttpMaxRequestBodySizeFeature`, 400 statt 413) stehen nur im Task-Block „Entscheidungen Conductor“.
- Dasselbe gilt für das Datumsfenster beim Commit (422 `date_not_allowed` statt 400, Prüfung nach `bookedFromLedger`, Romans Entscheidung 2026-11-19) und die Kategorie-Grenze von 200 Zeichen.
- Sonst alles in Ordnung: Kriterien belegt, `./verify.sh` im Worktree `VERIFY: PASS` (dotnet 5170, node 348), README_DELTA passt.

**SUGGESTED_FIX:** Ein ADR im ADRS-Block, etwa „Bob-Grenzen: Rumpfgrenze der Vorschau und Datumsfenster beim Commit“, das ADR 0068 erweitert: 8192 Bytes beim Lesen gezählt (warum 8192, warum nicht Kestrel, warum 400 Feld `body`), Kategorie 200 Zeichen, Datumsfenster am Berliner Tag des Commits (422 nach dem Blick ins Kassenbuch, Roman 2026-11-19). Festhalten, dass diese Grenzen wie die aus ADR 0068 fest im Code stehen und nur strenger werden dürfen. Optional ein Satz im Kommentar von `readBody`, warum nicht Kestrels Grenze. Sonst kein Code nötig.

**ITERATION_HINT:** likely-fixable

## Outcome

Die Grenzen der Bot-Schnittstelle sind jetzt an allen vier Stellen gehalten und belegt. Gate: `./verify.sh` endet mit `VERIFY: PASS` (dotnet 5170 grün, node 348 grün).

- **Datumsfenster beim Commit** (`src/Server/BotApi.fs`, `bookWithinLimit`): ein `now` je Aufruf. Zuerst `checkBotBookingDate (berlinDayOf deps.BerlinZone now) p.Date`, danach die Tagesgrenze, dann `addCashBookingWith` mit demselben `now`. Neuer Fall `BotDateNotAllowed of message` in `BotCommitOutcome`, im `commitHandler` 422 `date_not_allowed` mit dem Satz der Domäne. Die Prüfung steht hinter `bookedFromLedger` (in `bookPreview`).
- **Rumpfgrenze** (`readBody`): liest höchstens `botMaxBodyBytes + 1` Bytes und zählt dabei Bytes, nicht `Content-Length`. Als UTF-8 (über `StreamReader`, BOM-Verhalten wie vorher) wird erst nach der Prüfung gelesen. Bei Überlauf `None`, die Vorschau antwortet 400 `invalid_request` mit genau dem Feld `body`. `botMaxBodyBytes = 8192` und `botBodyTooLarge` stehen im Bot-Abschnitt von `src/Server/Validation.fs`, fest im Code. Die 400-Antwort mit Feldfehlern läuft über den neuen Helfer `fieldErrorsResponse`.
- **Kategorie-Grenze** (`parseBotPreviewRequest`): `category` hat nach `Trim` höchstens 200 Zeichen, sonst den Feldfehler „Höchstens 200 Zeichen“, gesammelt mit den übrigen.
- **Doc-Kommentare** nachgezogen: `BotCommitOutcome`, `bookWithinLimit`, `bookPreview`, `readBody`. Der Kommentar von `readBody` nennt, warum Kestrels Grenze nicht genutzt wird, und verweist auf ADR 0097.
- **ADR 0097** (`0097-bob-grenzen-rumpf-kategorie-und-datumsfenster-beim-commit.md`, im ADRS-Block, Nummer vorläufig) erweitert ADR 0068 um Rumpfgrenze, Kategorie-Grenze und Datumsfenster beim Commit, mit Begründung (8192, eigenes Lesen statt Kestrel, 400 Feld `body`, 422 beim Commit, Entscheidung Roman 2026-11-19). Die Grenzen stehen fest im Code, dürfen nur strenger werden und sind nicht konfigurierbar.

Tests:
- `src/Tests/BotApiTests.fs`, Liste „Bot-Schnittstelle (Routen)“: vier Commit-Tests um Berliner Mitternacht (Uhr über `berlinDayBoundsUtc` 5 Minuten vor bzw. nach dem Ende von Tag D, Helfer `beforeBerlinMidnight` und `afterBerlinMidnight`, `previewFor`). Die Fälle: D minus 31 Tage ergibt 422 ohne Push und ohne Buchung, D minus 30 Tage ergibt 200 mit einem Push, `AfterWrite` ergibt 200 mit derselben `entry_id` ohne zweiten Push, `BeforeWrite` ergibt 422 ohne Buchung. Dazu zwei Rumpf-Tests: 8193 Bytes ergeben 400 mit `fields` genau `[body]`, `CategoryLoads = 0`, `Store.Count = 0`, 8192 Bytes ergeben 200, und ohne Token ergibt der übergroße Rumpf 401.
- Der Test „Datumsfenster: heute minus 32 Tage ergibt 400 mit Feld date“ ist auf den einen Fall gekürzt. Die gestrichenen Fälle (heute, heute minus 31 Tage, morgen) stehen in `src/Tests/BotDomainTests.fs`, Liste „Bot Datumsfenster“, die Sammlung mit anderen Feldfehlern in `BotValidationTests.fs`.
- `src/Tests/BotValidationTests.fs`: 201 Zeichen Kategorie plus 201 Zeichen Payee ergeben `[ "category"; "payee" ]` mit dem Satz zur Kategorie. 200 Zeichen mit Leerraum am Rand gehen.
- `src/Tests/LedgerCashBookingTests.fs`, „Bobs Commit pusht den Request der Barbuchung …“: `transactionKeys handler.Bodies[0]` enthält kein `flag_color`.

Rot vor dem Fix: die Tests D minus 31, `BeforeWrite`, Rumpf 8193 und Kategorie 201 waren vor der Produktionsänderung rot (4 von 92 in den Bot-Listen). D minus 30, `AfterWrite` und 401 ohne Token sind Intakt- und Reihenfolge-Fälle, die schon vorher grün waren.

Gegenproben (jeweils danach zurückgesetzt):
- Datumsprüfung aus `bookWithinLimit` entfernt: die Tests D minus 31 Tage und `BeforeWrite` sind rot.
- Datumsprüfung in `bookPreview` vor `bookedFromLedger` gezogen: der `AfterWrite`-Test ist rot.
- In `botPushOn` das zweite `None` durch `Some "red"` ersetzt: ein Test ist rot (die flag_color-Prüfung in „Bot Barbuchung (Commit)“).

README_DELTA (beide Bullets nennen ADR 0097) geprüft mit `npm run check:readme-delta -- --bc ledger --delta -`: Exit 0, „Ganz nachlesen: keine Einträge“. `./verify.sh` in Iteration 2 erneut: `VERIFY: PASS` (dotnet 5170 grün, node 348 grün).
