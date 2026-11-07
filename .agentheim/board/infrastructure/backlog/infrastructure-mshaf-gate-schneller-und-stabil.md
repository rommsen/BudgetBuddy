---
id: infrastructure-mshaf
title: "Gate schneller und stabil: parallele Stränge, gezielter Lauf, Kernbudget, flackernde Wächter, veraltete Referenz-Assembly"
status: backlog
type: chore
context: infrastructure
created: 2026-11-06
completed:
depends_on: []
blocks: []
tags: [laufend, verify, gate, laufzeit, worktree, test-scripts, build, test, tests, playwright, flackern, captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Das Gate dauert rund 220 s und flackert gelegentlich. Beides bremst jeden work-Lauf. Die Triage vom 2026-11-06 hat sechs Einzeltickets zum Gate hier gebündelt.

## What
Die aufgenommenen Tickets stehen unten vollständig. Geschnitten und priorisiert wird im Refinement.

## Aufgenommen aus infrastructure-hwfkn (Triage 2026-11-06)
**Spike: Kernbudget für gleichzeitige Gate-Läufe messen. Parallelität je Lauf, Sperre um test:scripts und Zahl gleichzeitiger Worktrees zusammen entscheiden** (spike, erfasst 2026-11-05)

### Why
Nach infrastructure-d1dwb und infrastructure-shsvf dauert ein Gate 221 s, davon 153 s in Schritt 6 (`npm run test:scripts`, Messung 2026-11-05). Zwei Stellschrauben bestimmen, wie schnell mehrere Tickets durchkommen, und beide verteilen dieselben 10 Kerne (16 GB):

- **Parallelität je Lauf:** `TEST_CONCURRENCY` in `scripts/run-guards.mjs` steht auf 4 (gemessen bei infrastructure-eq27s mit 278 Tests, heute sind es 310).
- **Sperre um Schritt 6:** `verify.sh` lässt über `vl_lock_acquire` immer nur einen Lauf in Schritt 6. Ihr Grund laut Kopfkommentar, der 5-Sekunden-Start von `vite preview`, ist seit infrastructure-r5k2w behoben (`PREVIEW_START_TIMEOUT_MS` 60 s). Bei drei Worktrees warten die Läufe deshalb nacheinander, rund 3 × 153 s.

Getrennt optimiert heben sich die beiden auf: 8 je Lauf bringt nichts, wenn drei Läufe gleichzeitig um die Kerne konkurrieren. Ohne Sperre und mit 4 je Lauf liegen 12 Dateien auf 10 Kernen. Die Frage ist deshalb eine gemeinsame: wie verteilt man die Kerne auf die Läufe, die gerade aktiv sind. Diese Task ersetzt infrastructure-hh19s (verworfen 2026-11-05).

### What
Messen, entscheiden, eine Folgeaufgabe für die Umsetzung schreiben. Der Spike ändert keine Gate-Logik dauerhaft.

**Messaufbau.** Gemessen wird Schritt 6 allein, weil nur er unter der Sperre liegt: `npm run test:scripts` mit gesetztem `BB_GUARD_DIST` (ein vorher gebautes `dist/public` je Worktree, nach shsvf baut run-guards dann nicht) und `BB_GUARD_CONCURRENCY=<c>`. "Ohne Sperre" heißt: test:scripts direkt in N Worktrees gleichzeitig starten. "Mit Sperre" heißt: dieselben N Läufe nacheinander, das entspricht dem heutigen Verhalten in Schritt 6. Mindestens die gewählte Variante läuft zusätzlich einmal als voller `./verify.sh`-Lauf in N Worktrees gleichzeitig. So zählen auch dotnet build und npm build als Last mit.

**Matrix** (je Zelle mindestens zwei Durchgänge):

| N gleichzeitige Läufe | Varianten |
|---|---|
| 1 | c = 4, 6, 8 |
| 2 | mit Sperre c = 8; ohne Sperre c = 4, 5 |
| 3 | mit Sperre c = 8; ohne Sperre c = 3, 4 |

**Messgrößen je Zelle:** Zeit, bis alle N Läufe fertig sind (das Warten, das der Builder spürt), Dauer je Lauf, Zahl roter oder abgebrochener Tests mit Namen.

**Entscheidung** zwischen drei Formen, mit Begründung aus den Zahlen:
1. feste Parallelität, Sperre bleibt;
2. feste Parallelität, Sperre fällt;
3. Parallelität richtet sich nach der Zahl aktiver Läufe (die Sperre wird zum Zähler, etwa c = max(2, floor(K / aktive Läufe))).

Stop-Loss: Wenn sich mitten im Spike zeigt, dass die Abhilfe schon bekannt und billig ist, wird sie notiert und der Spike endet dort. Beispiel: schon die Zellen für N = 1 und N = 3 zeigen eindeutig, welche Form gewinnt. Dann werden die übrigen Zellen nicht mehr gemessen.

### Acceptance criteria
- [ ] Messtabelle im Outcome: je Zelle N, Sperre ja/nein, c, Zeit bis alle fertig, Dauer je Lauf, rote oder abgebrochene Tests mit Namen. Gemessen per `date +%s` oder `duration_ms` aus der node-test-Zusammenfassung, gleiche Maschine.
- [ ] Mindestens ein voller `./verify.sh`-Lauf in N ≥ 2 Kopien gleichzeitig mit der gewählten Variante, Zeiten im Outcome. In den Kopien unter `$TMPDIR` darf `verify.sh` dafür angepasst sein (etwa Sperre aus, `BB_GUARD_CONCURRENCY` gesetzt), im Repo nicht.
- [ ] Entscheidung (Form 1, 2 oder 3) mit Begründung im Outcome.
- [ ] Ein Backlog-Eintrag für die Umsetzung im BACKLOG_ITEMS-Block, mit konkreten Akzeptanzkriterien (Werte, Stelle im Code, Wächtertest für die Kopplung). Entfällt nur, wenn die Entscheidung "alles bleibt wie heute" lautet; dann steht der Grund im Outcome.
- [ ] Jeder Test, der in einer Zelle rot war und einzeln grün ist, steht mit Datei, Zeile und Fehlermeldung im Outcome (bekannter Kandidat: `[layout]` in `scripts/budget-page.test.mjs:968`, Suche 117 px statt mindestens 120 px unter Last).
- [ ] Die Gate-Logik im Repo ist nach dem Spike unverändert. Messhilfen liegen außerhalb des Repos oder werden nicht in FILE_LIST gemeldet. Ausnahme: der Kopfkommentar von `verify.sh` zur Sperre darf den aktuellen Stand der Begründung nennen.

### Notes
Refine 2026-11-05. Aus der Sitzung:
- Rechnung (geschätzt, nicht gemessen): heute bei drei Tickets rund 7,5 min, bis Schritt 6 überall durch ist. Mit Sperre und c = 8 vielleicht 5 min. Ohne Sperre und c = 4 vermutlich 3 bis 4 min.
- Gleichzeitige Läufe ohne eigene Worktrees: Ein Worker legt keine git-Worktrees an. Für Schritt 6 ist das auch nicht nötig. N Läufe von `npm run test:scripts` lassen sich gleichzeitig aus demselben Arbeitsverzeichnis starten (Build-Verzeichnisse per `mkdtemp`, Ports per `getFreePort`, Export nach `DS_EXPORT_OUT_DIR`). Genau das verhindert heute die Sperre. Stören sich die Läufe dort gegenseitig (gemeinsame Datei im Repo, fester Port), ist das ein Befund für die Tabelle. Dann wird mit Kopien des Arbeitsverzeichnisses unter `$TMPDIR` weitergemessen (`cp -R` ohne `node_modules`, `node_modules` als Symlink). Für den vollen `./verify.sh`-Lauf gilt dasselbe: Kopien unter `$TMPDIR`, nie `git worktree add`. Kopien und Symlinks werden am Ende entfernt, das echte `node_modules` bleibt unberührt.
- Gleichzeitige Läufe teilen Ports: `getFreePort` reserviert je Server einen freien Port. Ein Kollisionsfehler wäre selbst ein Befund und gehört in die Tabelle.
- Zeitabhängige Wächter (ledger-read-retry, siehe infrastructure-v3tqk) werden unter Last eher wackelig. Dass sie wackeln, ist ein Ergebnis und kein Grund, die Messung abzubrechen.
- pgpb6 (Expecto parallel zum Client-Strang) ist nach d1dwb nur noch rund 14 s wert und hier nicht Thema.

## Aufgenommen aus infrastructure-pgpb6 (Triage 2026-11-06)
**Gate läuft nach dem dotnet build in zwei parallelen Strängen: Expecto neben npm build, Wächtern und test:scripts** (chore, erfasst 2026-11-05)

### Why
`verify.sh` läuft alle sechs Schritte nacheinander. Nach infrastructure-d1dwb braucht Expecto rund 14 s, der Client-Strang (npm build, guard:classes, guard:tokens, test:scripts) weiter mehrere Minuten. Beide hängen nur vom dotnet build ab, nicht voneinander.

### What
Nach Schritt 1 laufen zwei Stränge gleichzeitig: Strang A Expecto (Tests.dll direkt, baut nichts), Strang B npm build, Wächter, test:scripts. Die Ausgabe bleibt je Schritt lesbar (Logs je Schritt wie heute, Zusammenfassung erst nach beiden Strängen), die Schlusszeile und die Gate-Semantik bleiben gleich.

### Acceptance criteria
- [ ] Schritt 2 läuft parallel zu den Schritten 3 bis 6; Gesamtdauer vorher und nachher gemessen.
- [ ] Ausgabe und Schlusszeile bleiben in Form gleich, rote Tests beider Stränge stehen gesammelt am Ende.
- [ ] Ctrl+C beendet beide Stränge ohne verwaiste Prozesse.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

### Notes
Offen für das Refinement:
- Verträgt sich das mit infrastructure-v8m2d (Vite stürzt ab, wenn dotnet parallel das Client-Projekt baut)? Strang A baut nach d1dwb nichts mehr, das Risiko sollte damit entfallen. Nachweisen.
- Lohnt der Umbau nach d1dwb noch? Ersparnis rund 14 s. Erst messen, dann entscheiden; bei zu kleinem Gewinn verwerfen.
- Reicht Bash mit Hintergrundjobs und `wait`, oder wird die Ausgabe dabei zu unübersichtlich?

## Aufgenommen aus infrastructure-ztgge (Triage 2026-11-06)
**Gezielter Testlauf während der Arbeit, volles Gate vor dem Abschluss: verify.sh --changed wählt Schritte nach geänderten Pfaden** (chore, erfasst 2026-11-05)

### Why
Worker lassen während der TDD-Schleife oft das ganze Gate laufen, obwohl ihre Änderung nur einen Teil berührt. Eine reine Server-Änderung braucht keine Browser-Tests, eine Änderung nur an `.agentheim/` oder Doku gar keine Tests. Aufteilen nach Bounded Context passt schlecht: `src/Shared` und die Remoting-Fixtures in `scripts/*.test.mjs` gehen quer durch alle BCs, und dort liegt die bekannte Laufzeitfalle (neues Pflichtfeld bricht die Fixtures erst zur Laufzeit).

### What
Vorschlag aus der Sitzung 2026-11-05:
- `verify.sh --changed` vergleicht gegen `main` (oder den Batch-Start) und wählt Schritte:
  - nur `.agentheim/`, `*.md`, `diary/`: keine Testschritte;
  - nur `src/Server/**`: dotnet build und Expecto;
  - etwas in `src/Client/**`, `src/Shared/**`, `scripts/**`, `vite.config.js`, Styles, `package.json`: alles.
- Die Test-Command-Abschnitte der BC-READMEs unterscheiden: während der Arbeit gezielt (`Tests.dll --filter "<Gruppe>"`, `npm run test:scripts -- scripts/<datei>.test.mjs` oder `--changed`), vor `RESULT: SUCCESS` und im Verifier das volle `./verify.sh`.

### Acceptance criteria
- [ ] Offen, siehe Notes. Vor dem Refinement nicht bereit.

### Notes
Entscheidungen für das Refinement:
- Ändert das, was das Gate garantiert? Der Verifier läuft weiter voll; `--changed` wäre nur für die Arbeitsschleife. Soll der Verifier jemals `--changed` nutzen dürfen?
- Ausgabe bei `--changed`: muss sichtbar sagen, welche Schritte übersprungen wurden, und darf nie `VERIFY: PASS` drucken (sonst verwechselbar mit dem vollen Gate). Vorschlag: eigene Schlusszeile.
- Der Worker-Prompt liegt im Agentheim-Plugin, außerhalb des Repos. Erreichbar ist nur der Test-Command-Abschnitt der BC-READMEs, den `work` an Worker und Verifier weitergibt.
- Eine Testdatei mit `--filter` für Expecto braucht stabile Gruppennamen; prüfen, ob die heutigen `testList`-Namen dafür taugen.

## Aufgenommen aus infrastructure-r6c3v (Triage 2026-11-06)
**Testbau sieht neue öffentliche Client-Werte nicht: veraltete Referenz-Assembly** (bug, erfasst 2026-11-03)

### Why
Bei infrastructure-t5m2b zweimal aufgetreten: Wird im Client ein `[<Literal>]` neu angelegt oder ein `private let` öffentlich, meldet der Bau von `src/Tests` "not defined". `src/Client/obj/Debug/net10.0/ref/Client.dll` und `refint/Client.dll` bleiben veraltet, obwohl `Client.dll` neu gebaut ist. Löschen beider Dateien behebt es. `./verify.sh` ist davon betroffen, solange `obj/` besteht.

### What
Ursache klären, etwa durch `ProduceReferenceAssembly=false` in `Client.fsproj` oder eine bessere Zuordnung der Eingaben, und den Weg ohne Handarbeit stabil machen.

### Acceptance criteria
- [ ] Regressionstest oder reproduzierbarer Ablauf: neues `[<Literal>]` im Client, Testbau sieht es ohne Löschen von `obj/`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Aufgenommen aus infrastructure-v3tqk (Triage 2026-11-06)
**Wächter ledger-read-retry flackert bei getLedgerPayeeSuggestions unter Last** (bug, erfasst 2026-11-01)

### Why
`scripts/ledger-read-retry.test.mjs:223` ("getLedgerPayeeSuggestions (/ledger) übersteht einen Abbruch") war in einem vollen `./verify.sh`-Lauf rot ("keine Wiederholung" nach 5 s) und ohne Codeänderung im nächsten grün. Das Gate wird dadurch unzuverlässig. Die Vermutung ist ein Lastrennen gegen die feste Frist, nachgewiesen ist sie nicht.

### What
Die Ursache klären und den Test auf eine Bedingung warten lassen statt auf eine feste Frist (Test-Doktrin Punkt 5).

### Acceptance criteria
- [ ] Die Ursache steht im Outcome, mit Beleg.
- [ ] Der Test bleibt in zehn vollen `npm run test:scripts`-Läufen grün.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

### Notes
Dublette von infrastructure-h4q8v. Die Ursache ist dort belegt, Lösung dort. Diese Task entfällt, sobald Roman sie verwirft.

## Aufgenommen aus ledger-vqvq6 (Triage 2026-11-06)
**Playwright-Wächter ledger-split-cashback wartet auf die Settings-Antwort statt fest 400 ms** (chore, erfasst 2026-10-26)

### Why
Der Verifier von ledger-s8t3s hat angemerkt: `scripts/ledger-split-cashback.test.mjs` wartet mit einem festen `waitForTimeout` von 400 ms (`settle`) auf die Settings-Antwort. Das kann je nach Last wackeln.

### What
Das feste Warten durch ein Warten auf die Antwort (z. B. `waitForResponse` auf `getSettings`) oder auf ein sichtbares Element ersetzen.

### Acceptance criteria
- [ ] To be defined during refinement.

### Notes
Captured via `quick-capture` on 2026-10-26, raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Herkunft: nicht blockierender Hinweis des Verifiers von ledger-s8t3s.

## Nachtrag 2026-11-06 (Verifier budgeting-r4kqm)
- `src/Tests/BudgetTakeoverTests.fs:529` "die Wartefunktion des Servers wartet wirklich" misst unter paralleler Last 49,785 ms gegen eine Untergrenze von 50 ms und wird rot. Ein zweiter Lauf war grün.
- `[flow:bank-trennen]` in `scripts/settings-secrets.test.mjs`: Playwright-Klick lief unter Last ab ("element not stable"), einzeln und im zweiten Lauf grün (Verifier budgeting-b9869).
- Expecto blieb unter Last (über 20) mehrfach hängen, an CmdTiming-, YnabClient-Fake-Handler- und Netz-Wächter-Tests, auch mit der Tests.dll von `main` (Worker budgeting-b9869). Hängende Prozesse ließen sich aus der Session nicht beenden.
