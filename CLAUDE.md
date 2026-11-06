# BudgetBuddy

BudgetBuddy wird das führende System für Romans Familienfinanzen, Fähigkeit für Fähigkeit.
YNAB bleibt Projektion und Fallback, solange BB eine Fähigkeit noch nicht hat. Daily Driver
für Roman (Frau nutzt mit), Single-User, kein Hobby-Projekt: was nicht gebraucht wird, fliegt
raus. Vision: `.agentheim/knowledge/vision.md`.

## Domäne

- **Kassenbuch** (Ledger): BBs eigenes Buch aller Buchungen über alle Konten, mit lückenloser
  Historie nach Zeitpunkt und Herkunft. Der **Spiegel** zieht YNABs Zustand nach.
- **BB entscheidet, YNAB wird nachgezogen**: Kategorie ändern, Splits, Memo, Prüfvermerk laufen
  in BB und werden zu YNAB gepusht (`ynab-sync`).
- **Comdirect-Import** (`banking-import`): OAuth plus Push-TAN, Dedup über die Comdirect-Reference.
  Regeln aus `rules.yml` kategorisieren (`categorization`).
- **Budget** (`budgeting`): Geld auf Kategorien verteilen und verschieben, die nächste große Fähigkeit.
- **Lena fragen**: setzt in YNAB ein Flag mit der Farbe aus den Settings (`lena_flag_color`, ADR 0056).
- **Bob**: externer Agent, legt über REST unter `/bot/` Barbuchungen auf dem Quick-Add-Konto an,
  zweistufig `preview` und `commit` (ADR 0066). Actor `Bob`, kein Lesezugriff aufs Kassenbuch.
- **Ereignis oder Zustand**: Geht es um Geld (was passiert ist oder was entschieden wurde), sind es
  Ereignisse in der gemeinsamen Tabelle `events`, alles andere ist Zustand (ADR 0083).
- Bounded Contexts und Sprache: `.agentheim/knowledge/context-map.md`, `contexts/<bc>/README.md`.

## Security-Fallen

- Das Repo ist öffentlich (GitHub). Keine Tokens, Kontodaten oder echten Buchungen in Code,
  Tests, Doku oder Commits. `.env` ist ignoriert und bleibt es.
- Bob-Grenzen (ADR 0068) sind im Code fest: höchstens 500 € je Buchung (`BOT_MAX_AMOUNT_EUR` darf
  nur absenken), je Kalendertag Europe/Berlin höchstens 500 € Summe und 10 Commits, Buchungsdatum
  höchstens 31 Tage zurück, 10 Anfragen pro Minute auf `/bot/`. Bob-Buchungen bekommen kein Flag.
  Diese Grenzen nicht lockern und nicht konfigurierbar machen.
- Secrets gehen nie an den Client. `/api` hat keine eigene Anmeldung, Schutz nur über Tailscale.
  `getSettings` liefert `AppSettingsView` ohne Secrets, `AppSettings` bleibt auf dem Server.
- Live-Dienste (YNAB, Comdirect) nur in Integrationstests hinter `RUN_INTEGRATION_TESTS`.
  Ein normaler Testlauf ruft nie echte APIs auf und schreibt nie in eine echte Datenbank
  (`USE_MEMORY_DB` in `src/Tests/Main.fs`).

## Laufzeitfallen (Compiler merkt es nicht)

- Fable.Remoting: ein neues Pflichtfeld in einem Record aus `src/Shared/` bricht die
  Playwright-Fixtures in `scripts/*.test.mjs` und ältere Clients erst zur Laufzeit. Fixtures im
  selben Schritt mitziehen.
- Giraffe: ein Handler im `choose` von `Api.webApp` gibt fremde Pfade per `skipPipeline` weiter.
  `next ctx` beendet jede Anfrage mit 200 und leerem Rumpf, so fiel `/api` aus (Fix 2a5c229,
  Test in `src/Tests/RemotingPipelineTests.fs`).
- Dapper: jeder Row-Typ braucht `[<CLIMutable>]`, jedes Optionsfeld einen registrierten
  `OptionHandler<'T>` (`src/Server/Persistence.fs`, heute für `string` und `DateTime`). Ein
  neuer Optionstyp braucht eine eigene Registrierung.
- Beträge sind im Modell `Money` (decimal), an der YNAB-Grenze Milliunits (Schreibweg `int`).
  Zu YNAB gehen sie per `Encode.int` als JSON-Zahl; als String verwirft YNAB die Buchung still.
- Validierung an der API-Grenze (`src/Server/Validation.fs`), alle Fehler sammeln und konkret
  melden. Domänenlogik bleibt rein, Zeit und Zeitzone kommen als Parameter.
- Große Listen im Client: teure Werte (etwa Kategorie-Optionen) einmal vor der Schleife
  berechnen, inaktive Zeilen schlicht rendern.

## Architektur

- F#: `src/Shared` (Typen, Remoting-Verträge), `src/Client` (Fable, Elmish, Feliz, TailwindCSS 4),
  `src/Server` (Giraffe, Fable.Remoting, SQLite über Dapper), Tests mit Expecto in `src/Tests`.
- Pfadbezogene Regeln liegen in `.claude/rules/`, die Test-Doktrin in `.claude/rules/test-doctrine.md`
  (Bugfix nur mit Regressionstest, der vor dem Fix rot ist).
- Browser-Tests tragen eine Kennung im Namen (`[layout]`, `[fokus]`, `[tastatur]`, `[scroll]`,
  `[fable]`, `[flow:<name>]`), je Flow höchstens einer; Flows stehen in `FLOWS` in
  `scripts/browser-usage.mjs`. Keine feste Obergrenze, das Gate meldet die Zahl je Kennung
  (Doktrin Punkt 5).
- UI-Gate ist `standards/frontend/styleguide.md`; die Route `/styleguide` zeigt die echten
  DS-Komponenten aus `src/Client/DesignSystem/`. Werte dort nachschlagen, nie erfinden.

## Dev

- Backend `cd src/Server && dotnet watch run` (Port 5081), Frontend `npm run dev` (Port 5181), beides `npm run dev:all`.
- Lokal starten mit `npm run dev:restart` (`scripts/dev-restart.sh`): beendet, was auf 5081 und 5181 läuft, auch
  verwaiste `dotnet watch`, und startet `dev:all`; Ctrl+C beendet beides. `--stop` beendet nur, etwa vor `./verify.sh`
  (ein paralleles `dotnet watch` stört `dotnet build`). `dotnet watch` beendet sich bei Ctrl+C nicht von selbst.
- Lokal läuft der Server als `Development` (`src/Server/Properties/launchSettings.json`), die Produktion setzt
  `Production` in `compose.yaml`.
- Lokale Datenbank unter `DATA_DIR`, ohne Angabe `~/my_apps/budgetbuddy-dev/`.
- Einzelne Wächter: `npm run test:scripts -- scripts/<datei>.test.mjs`.
- Screenshot einer Route: `npm run screenshot:route`.

## Gate

`./verify.sh` ist das Gate: dotnet build, Expecto über die Tests.dll (Direktaufruf mit
JUnit-Bericht, kein `dotnet test`), npm run build, beide Wächter (`guard:classes`,
`guard:tokens`) und `npm run test:scripts`. Fertig ist eine Änderung erst bei
`VERIFY: PASS`. Übersprungene Pflichttests zählen als rot. Schlägt ein Schritt fehl: Ursache
beheben, nicht umgehen.

## Ablauf

- Features und Bugs: `/agentheim:modeling` (capture, refine, promote), dann `/agentheim:work`.
- Skizzen (claude.ai-Artifact, von Roman freigegeben) sind die Vorlage für Form und Inhalt einer Seite.
  Beim Verfeinern jedes sichtbare Element der Skizze als Kriterium übernehmen (Zeilenaufbau, Balken, Farben,
  Texte, Kopf, Panel). Jede Abweichung steht ausdrücklich in der Task, mit Grund, sonst gilt die Skizze. Ein
  Baustein aus dem DS ersetzt ein Element der Skizze nur, wenn er gleich aussieht. Sonst wird er eine neue
  DS-Variante.
- Worker und Verifier können claude.ai-Artifacts nicht öffnen. Der Conductor liest die Skizze (`Artifact`
  read), legt ihre Dateien und einen Screenshot je Ansicht in den Scratchpad und gibt Worker, Verifier und
  `design-quality` die Pfade mit. Die Task trägt ein Kriterium „Seite neben der Skizze verglichen, Abweichungen
  nur die genannten“ mit Screenshot-Vergleich im Outcome.
- Worktrees unter `.worktrees/`: nach `git worktree add` `scripts/worktree-prep.sh`, Abbau nur
  über `scripts/worktree-teardown.sh` (sonst leert `git worktree remove --force` das echte
  `node_modules`).
- Commits scoped mit expliziten Pfaden. Der Hook `.claude/hooks/git-guard.sh` sperrt
  Sammel-Staging und `commit -a` und fragt bei jedem Push.
- Push nur auf ausdrückliche Anweisung. `git push deploy` (oder `scripts/deploy.sh`) baut und
  startet die Produktion; das passiert nie ohne Auftrag.
- `scripts/deploy-rules.sh --clear` löscht alle Regeln der Live-Datenbank und importiert neu:
  nur nach Rückfrage.
- `diary/development.md` ist eingefroren, es kommen keine Einträge mehr dazu.

### Override (C): Reviews nach einem work-Lauf vorschlagen

Am Ende jedes `/agentheim:work`-Laufs `/review-agents` vorschlagen, mit den Agents, die er wählen
würde, und beides im Session-End-Eintrag nennen, nicht automatisch starten: `qa-milestone-reviewer`
bei geänderten Tests, `backend-code-quality` bei `src/Server/**`, `frontend-architecture` bei
`src/Client/**`, `design-quality` bei sichtbaren UI-Änderungen. Der Skill
(`.claude/skills/review-agents/SKILL.md`) lässt sie laufen und behebt die Funde selbst. Liegen seit dem letzten `Bestandsreview: <bc>` im
Protokoll zehn oder mehr erledigte Tasks des BC, zusätzlich `/architecture-review <bc>` vorschlagen.

### Backlog-Disziplin

Für Funde aus einem work-Lauf (Follow-up eines Workers, Anmerkung eines Verifiers, überlebender Mutant einer
Gegenprobe), aus `/review-agents` und aus `/architecture-review` gilt: Ein eigenes Ticket gibt es nur, wenn der Fund
Geld oder Daten verfälscht, im Alltag sichtbar stört, eine Sicherheitsgrenze betrifft oder ein Feature blockiert.
Alles andere wird eine Zeile in der Kann-Sammlung des BC (eine Task je BC mit dem Titel „Kann-Sammlung <bc>“, fehlt
sie, wird sie angelegt). Kann-Sammlungen werden nie automatisch gebaut. Der Conductor wendet die Regel an, wenn er
Follow-ups aus dem RESULT-Block übernimmt. `/architecture-review` legt höchstens fünf Gruppen zur Auswahl vor.

## Lernen

Nach einer Korrektur durch den Owner, einem Verifier-FAIL in der dritten Iteration oder einem
Fehler, der schon einmal vorkam: den globalen `learn`-Skill aufrufen. Er schlägt einen Diff vor
(Check in `./verify.sh`, Rule, Skill, BC-README oder diese Datei); übernommen wird nur nach Freigabe.

## Sprache

UI, Doku und Kommentare Deutsch mit echten Umlauten (ä, ö, ü, ß). Identifier Englisch.

## README-Deltas (Agentheim)

Vor dem Integrieren eines README_DELTA lokal gegen das echte Grammatik-Modul prüfen: `npm run check:readme-delta -- --bc <kontext> --delta <datei|->` (direkt: `node scripts/readme-delta-check.mjs --bc <kontext> --delta <datei|->`).
Liegen zwischen RESULT und Integration Review-Fixes oder ein Merge (etwa bei einer geparkten Task), ist das Delta veraltet. Vor dem Gate jeden Satz am aktuellen Code prüfen: Verhalten, zitierte Nutzersätze (nicht Log-Sätze) und jeden Bullet, der dieselbe Regel nennt. Die bekannten Änderungen nur einzuarbeiten reicht nicht (budgeting-eh8xe, Verifier-FAIL in Iteration 2 und 3).
Ein rotes Gate heißt: den README-Block von Hand platzieren, nicht das Delta blind anwenden.
Exit 1: das Delta wurde inhaltlich abgelehnt, Grund in der Meldung.
Exit 2: das Gate konnte gar nicht urteilen (Plugin-Modul nicht gefunden, Eingabe ungültig), das ist etwas anderes als "abgelehnt".
Umgehung bei einem per replace nicht erreichbaren Bullet: auf das nächstgelegene Spalte-0-Bullet mit fettem Kopf ankern und den ganzen Extent in expected und body mit exakter Einrückung neu ausgeben.
Diese Aufruf-Regel ("vor dem Integrieren durch das Gate") ist prose-only und nicht mechanisch erzwungen (ADR 0059): die Aufrufstelle liegt im Conductor-Plugin-Code, außerhalb dieses Repos.
Erzwungen ist nur das Gate selbst (`npm run test:scripts`) und die Kopplung von Skriptpfad und Text in dieser Datei.
