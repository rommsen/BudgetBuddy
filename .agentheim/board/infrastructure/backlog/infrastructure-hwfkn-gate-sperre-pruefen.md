---
id: infrastructure-hwfkn
title: "Spike: Kernbudget für gleichzeitige Gate-Läufe messen. Parallelität je Lauf, Sperre um test:scripts und Zahl gleichzeitiger Worktrees zusammen entscheiden"
status: backlog
type: spike
context: infrastructure
created: 2026-11-05
completed:
depends_on: [infrastructure-shsvf]
blocks: []
tags: [verify, gate, laufzeit, worktree, test-scripts]
related_adrs: []
related_research: []
prior_art: [infrastructure-dvy29, infrastructure-r5k2w, infrastructure-eq27s, infrastructure-shsvf]
---

## Why
Nach infrastructure-d1dwb und infrastructure-shsvf dauert ein Gate 221 s, davon 153 s in Schritt 6 (`npm run test:scripts`, Messung 2026-11-05). Zwei Stellschrauben bestimmen, wie schnell mehrere Tickets durchkommen, und beide verteilen dieselben 10 Kerne (16 GB):

- **Parallelität je Lauf:** `TEST_CONCURRENCY` in `scripts/run-guards.mjs` steht auf 4 (gemessen bei infrastructure-eq27s mit 278 Tests, heute sind es 310).
- **Sperre um Schritt 6:** `verify.sh` lässt über `vl_lock_acquire` immer nur einen Lauf in Schritt 6. Ihr Grund laut Kopfkommentar, der 5-Sekunden-Start von `vite preview`, ist seit infrastructure-r5k2w behoben (`PREVIEW_START_TIMEOUT_MS` 60 s). Bei drei Worktrees warten die Läufe deshalb nacheinander, rund 3 × 153 s.

Getrennt optimiert heben sich die beiden auf: 8 je Lauf bringt nichts, wenn drei Läufe gleichzeitig um die Kerne konkurrieren. Ohne Sperre und mit 4 je Lauf liegen 12 Dateien auf 10 Kernen. Die Frage ist deshalb eine gemeinsame: wie verteilt man die Kerne auf die Läufe, die gerade aktiv sind. Diese Task ersetzt infrastructure-hh19s (verworfen 2026-11-05).

## What
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

## Acceptance criteria
- [ ] Messtabelle im Outcome: je Zelle N, Sperre ja/nein, c, Zeit bis alle fertig, Dauer je Lauf, rote oder abgebrochene Tests mit Namen. Gemessen per `date +%s` oder `duration_ms` aus der node-test-Zusammenfassung, gleiche Maschine.
- [ ] Mindestens ein voller `./verify.sh`-Lauf in N ≥ 2 Kopien gleichzeitig mit der gewählten Variante, Zeiten im Outcome. In den Kopien unter `$TMPDIR` darf `verify.sh` dafür angepasst sein (etwa Sperre aus, `BB_GUARD_CONCURRENCY` gesetzt), im Repo nicht.
- [ ] Entscheidung (Form 1, 2 oder 3) mit Begründung im Outcome.
- [ ] Ein Backlog-Eintrag für die Umsetzung im BACKLOG_ITEMS-Block, mit konkreten Akzeptanzkriterien (Werte, Stelle im Code, Wächtertest für die Kopplung). Entfällt nur, wenn die Entscheidung "alles bleibt wie heute" lautet; dann steht der Grund im Outcome.
- [ ] Jeder Test, der in einer Zelle rot war und einzeln grün ist, steht mit Datei, Zeile und Fehlermeldung im Outcome (bekannter Kandidat: `[layout]` in `scripts/budget-page.test.mjs:968`, Suche 117 px statt mindestens 120 px unter Last).
- [ ] Die Gate-Logik im Repo ist nach dem Spike unverändert. Messhilfen liegen außerhalb des Repos oder werden nicht in FILE_LIST gemeldet. Ausnahme: der Kopfkommentar von `verify.sh` zur Sperre darf den aktuellen Stand der Begründung nennen.

## Notes
Refine 2026-11-05. Aus der Sitzung:
- Rechnung (geschätzt, nicht gemessen): heute bei drei Tickets rund 7,5 min, bis Schritt 6 überall durch ist. Mit Sperre und c = 8 vielleicht 5 min. Ohne Sperre und c = 4 vermutlich 3 bis 4 min.
- Gleichzeitige Läufe ohne eigene Worktrees: Ein Worker legt keine git-Worktrees an. Für Schritt 6 ist das auch nicht nötig. N Läufe von `npm run test:scripts` lassen sich gleichzeitig aus demselben Arbeitsverzeichnis starten (Build-Verzeichnisse per `mkdtemp`, Ports per `getFreePort`, Export nach `DS_EXPORT_OUT_DIR`). Genau das verhindert heute die Sperre. Stören sich die Läufe dort gegenseitig (gemeinsame Datei im Repo, fester Port), ist das ein Befund für die Tabelle. Dann wird mit Kopien des Arbeitsverzeichnisses unter `$TMPDIR` weitergemessen (`cp -R` ohne `node_modules`, `node_modules` als Symlink). Für den vollen `./verify.sh`-Lauf gilt dasselbe: Kopien unter `$TMPDIR`, nie `git worktree add`. Kopien und Symlinks werden am Ende entfernt, das echte `node_modules` bleibt unberührt.
- Gleichzeitige Läufe teilen Ports: `getFreePort` reserviert je Server einen freien Port. Ein Kollisionsfehler wäre selbst ein Befund und gehört in die Tabelle.
- Zeitabhängige Wächter (ledger-read-retry, siehe infrastructure-v3tqk) werden unter Last eher wackelig. Dass sie wackeln, ist ein Ergebnis und kein Grund, die Messung abzubrechen.
- pgpb6 (Expecto parallel zum Client-Strang) ist nach d1dwb nur noch rund 14 s wert und hier nicht Thema.
