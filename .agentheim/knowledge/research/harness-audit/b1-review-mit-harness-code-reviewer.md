# B1: Review von acht Commits mit dem Harness-Code-Reviewer

Datum: 2026-10-29
Reviewer: Code-Reviewer-Agent (Harness `.claude/agents/review-code.md`), nur lesend
Geprüfte Commits: a20fef6, ae7cfb5, 92ac090, cfb05ff, 0f39ac0, a0a8cab, b4b5cc3, 897b13c
Zeilenangaben: Stand `main` (HEAD ed3a6ab).

Werkzeug: Serena war in dieser Sitzung nicht verfügbar. Gelesen wurde per `git show`, `Read`
und `grep`. Das verletzt formal CLAUDE.md:3 ("STOP ... USE SERENA INSTEAD"), siehe Abschnitt
Regelbewertung.

## Zusammenfassung

Die acht Commits sind solide: jeder Bug-Commit hat einen Regressionstest mit Kommentar, alle
haben einen Diary-Eintrag, SQL ist parametrisiert, Tests laufen gegen die In-Memory-DB, die
Zeit ist überall injiziert (`BotDeps.Now`, `today`-Parameter). Es gibt keinen Befund der
Schwere hoch. Die wichtigsten Punkte sind drei Verhaltensfragen in `BotApi.fs` ohne
Regelbezug (gecachter fehlgeschlagener Commit, fehlende `entry_id` nach Push-Fehler, globale
Drosselung vor der Anmeldung) und Platzierungsfragen (Geschäftsregeln und Zeitzonenlogik in
`Validation.fs`, Server-Typen außerhalb von `src/Shared/`), bei denen die Regel selbst
fragwürdig ist.

Urteil: IMPROVEMENTS SUGGESTED (freigegeben, Empfehlungen prüfen).

## Befunde

### Mittel

M1. b4b5cc3, `src/Server/BotApi.fs:76-78` und `:362-374`. Schwere: mittel. Ohne Regelbezug.
`CommitOnce` legt `lazy (Async.StartAsTask(work ()))` dauerhaft in `commits` ab, auch wenn die
Aufgabe mit einer Ausnahme endet. Jeder weitere Commit derselben Vorschau überspringt wegen
`IsCommitted` die Ablaufprüfung und bekommt dieselbe fehlerhafte Aufgabe, also wieder 500
`commit_failed`, bis `Purge` greift (bis 75 Minuten). Das verhindert zwar eine Doppelbuchung,
falls `CashExpenseAdded` schon geschrieben war, ist aber nirgends dokumentiert
(`docs/bot-api.md:36-39` nennt 500 nicht) und nicht getestet (kein Test für den 500-Pfad in
`src/Tests/BotApiTests.fs`). Bob legt dann eine neue Vorschau an und kann doppelt buchen, wenn
der erste Versuch die Buchung schon geschrieben hatte. Empfehlung: Verhalten bewusst
entscheiden, dokumentieren und mit einem Test festhalten.

M2. b4b5cc3, `src/Server/BotApi.fs:334-339`. Schwere: mittel. Ohne Regelbezug.
Scheitert der Push, steht die Buchung im Kassenbuch (`CashExpenseAdded` plus
`EntryPushToYnabFailed`), die Antwort meldet aber `status: "booked"` mit `entry_id: null`,
weil `addCashExpense` im Fehlerfall nur den `YnabError` liefert und die `EntryId` verliert
(`src/Server/Ledger/CommandHandlers.fs:663-714`). `docs/bot-api.md:39` sagt "Die Antwort nennt
`entry_id`". Bob kann die Buchung so nicht referenzieren. Empfehlung: `addCashExpense` die
`EntryId` auch im Fehlerfall zurückgeben lassen oder die Doku angleichen.

M3. 92ac090, `src/Server/BotApi.fs:404-407` mit `:112-127`. Schwere: mittel. Ohne Regelbezug.
Die Drosselung ist ein einziges globales Fenster vor der Anmeldung. Wer `/bot/` ohne Token
erreicht (laut Memory "Bot-API-Token live" leitet Caddy `/bot` durch), kann mit 10 Anfragen je
Minute Bob dauerhaft aussperren. Das ist laut ADR 0068 Punkt 4 und `docs/bot-api.md:50-55`
gewollt (Raten des Tokens bremsen), der Zielkonflikt Sperre gegen Verfügbarkeit ist aber nicht
benannt. Empfehlung: in der ADR oder Doku festhalten, dass ein Fremder Bob blockieren kann, oder
getrennte Zähler für 401 und angemeldete Anfragen.

M4. b4b5cc3 und cfb05ff, `src/Server/Validation.fs:160-178`, `:224-229`, `:336-372`. Schwere:
mittel. Regel: CLAUDE.md:228-230 ("Domain.fs ... pure transformations"),
`standards/backend/overview.md:40-42` (Domain.fs: Business Logic, Validation.fs: Input
validation), `.claude/rules/feature-workflow.md:19-20`. `checkBotDailyLimit` (Tagesgrenze),
`resolveBotCategory`/`selectableBotCategories` (Kategorie-Auflösung) und die Typen
`BotDirection`, `BotPreviewInput`, `BotCategoryResolution` sind Geschäftsregeln, liegen aber in
`Validation.fs`. Die Funktionen sind rein und gut getestet, inhaltlich ist es also nur eine
Platzierungsfrage. Hinweis zur Regel: das Repo hat keine `src/Server/Domain.fs`, die Domäne
liegt BC-weise in `src/Server/Ledger/Domain.fs`. Die Regeln nennen nur den Einzeldatei-Pfad
und greifen hier mechanisch nicht.

M5. a20fef6 und cfb05ff, `src/Server/Validation.fs:194-220`, `src/Server/Program.fs:34-40`.
Schwere: mittel. Regel: `standards/backend/overview.md:42` (Validation.fs: Input validation),
`.claude/agents/review-code.md:40` (Result statt Exceptions für erwartete Fehler),
`standards/backend/error-handling.md:7`. Zeitzonenauflösung, Berliner Tagesgrenzen und eine
`failwith` in `berlinZone` (Zeile 199-203) stehen im Validierungsmodul, und `Program.fs` ruft
für den Startcheck `Server.Validation.tryResolveBerlinZone` auf. Die `failwith` selbst ist
vertretbar, weil `Program.fs` vorher abbricht (ein Startinvariant, kein erwarteter Fehler). Die
Platzierung gehört eher in ein eigenes Modul (z.B. `Server.BerlinTime`).

### Niedrig

N1. b4b5cc3 und cfb05ff, `src/Server/BotApi.fs:248-307` (`previewHandler`, 60 Zeilen, sieben
Ebenen `match`) und `src/Server/Validation.fs:234-325` (`parseBotPreviewRequest`, 92 Zeilen).
Schwere: niedrig. Regel: `.claude/agents/review-code.md:61` ("Functions < 30 lines"). Die
Treppe in `previewHandler` lässt sich mit einem `taskResult`-artigen Verlauf oder kleinen
Schritt-Funktionen flacher halten. Die Regel steht nur in der Agentenbeschreibung, nicht in
CLAUDE.md oder `standards/`. Für Parser mit gesammelten Feldfehlern ist 30 Zeilen zu knapp,
die Länge von `parseBotPreviewRequest` ist lesbar.

N2. b4b5cc3, `src/Server/BotApi.fs:24-56`, `:112`, `:131`, `:152-163`, sowie
`src/Server/Validation.fs:160-178`, `:342-345`. Schwere: niedrig. Regel: CLAUDE.md:223
("Define ALL types in src/Shared/"), `standards/global/architecture.md:27`,
`.claude/agents/review-code.md:39`. Alle Bot-Typen liegen im Server. Die Regel ist hier
unpassend: die Typen gehen nicht über Fable.Remoting zum Client, die Bot-Antworten sind per Hand
codiertes JSON. `standards/shared/types.md:7` begründet die Regel mit der Netzgrenze
Client/Server, die hier nicht besteht. Empfehlung: Regel auf "Typen, die Client und Server
teilen" einschränken.

N3. b4b5cc3, `src/Server/BotApi.fs:133`, `:113-114`, `:90`. Schwere: niedrig. Regel:
`.claude/agents/review-code.md:63` ("No mutable state"). `BotCategoryCache` hat `let mutable
cached`, `BotRateLimiter` eine `Queue`, `bookingGate` ist ein prozessweites Semaphor auf
Modulebene (gilt auch über mehrere `BotDeps`-Instanzen in Tests). Für Cache, Drosselung und
Sperre ist veränderlicher Zustand angemessen und jeweils mit Sperre geschützt. Die Regel ist in
dieser Absolutheit für Server-Infrastruktur unsinnig.

N4. b4b5cc3, `src/Server/BotApi.fs:474`. Schwere: niedrig. Ohne Regelbezug. Fehlt das
YNAB-Token oder Standard-Budget, liefert `Push` `YnabError.Unauthorized reason`. Das ist ein
Konfigurationsfehler, kein Anmeldefehler; `describePushFailure` erzeugt dann eine irreführende
Meldung an Bob.

N5. b4b5cc3, `src/Server/BotApi.fs:205`. Schwere: niedrig. Ohne Regelbezug. `"amount",
Encode.float (float p.AmountEur)` wandelt einen Geldbetrag über `float`. Bei zwei
Nachkommastellen bis 500 ist das verlustfrei darstellbar, `Encode.decimal` wäre die direkte Form.

N6. 897b13c, `src/Client/DesignSystem/Button.fs:64-71`. Schwere: niedrig. Regel:
`standards/frontend/styleguide.md:180` und `:192-197` (Token vor rohen Tailwind-Klassen, rohe
Klassen gelten als Drift), `:206-207` (Touch-Targets über `TouchTargets.minSize`, Tappables mit
`touch-action: manipulation`). `Tokens.TouchTargets.minHeight` ist genau
`"min-h-[48px] md:min-h-0"` (`src/Client/DesignSystem/Tokens.fs:311`), `Button.sizeToClass`
schreibt den Wert trotzdem roh aus, ebenso `min-w-[48px]`. `touch-action: manipulation` fehlt
am Button weiterhin (bestand schon vorher, gehört aber zum selben Styleguide-Punkt).

N7. 897b13c, `src/Client/DesignSystem/Button.fs:64` und `:70`. Schwere: niedrig. Ohne
Regelbezug. `sizeToClass` wurde von `private` auf öffentlich gestellt, damit
`src/Tests/ButtonSizeTests.fs` die Klassenstrings prüfen kann. Die Tests prüfen damit
Implementierungsdetails (vgl. `.claude/rules/testing.md:52`, "Test names must describe
behavior, not implementation"), das echte Verhalten deckt der Playwright-Wächter
`scripts/touch-target-small-button.test.mjs` ab. Die F#-Tests sind dadurch fast redundant.

N8. 0f39ac0, `src/Tests/CorsTests.fs:30-48`. Schwere: niedrig. Regel:
`.claude/rules/testing.md:52`. Die Regressionstests sind Quelltext-Suchen (`AddCors`,
`UseCors`, `Cors` in allen Server-Dateien), keine Verhaltensprüfung. Der Kommentar
(Zeile 1-9) begründet das nachvollziehbar (Program.fs ohne WebApplicationFactory-fähigen
Einstieg). Schwächen: `f.Contains "/obj/"` (Zeile 42) setzt `/` als Pfadtrenner voraus; der
Vite-Test (Zeile 45-47) prüft nur, dass `proxy` und `'/api'` irgendwo in `vite.config.js`
stehen. Kein Test, dass eine Antwort kein `access-control-allow-origin` trägt.

N9. b4b5cc3 und cfb05ff, `src/Tests/BotApiTests.fs:121-125`. Schwere: niedrig. Ohne
Regelbezug. Alle Tests teilen eine In-Memory-Datenbank; die Tagesgrenzen-Tests zählen alle
Bob-Ereignisse des Tages und bleiben nur isoliert, weil jeder Test einen eigenen Kalendertag
wählt (`2026-11-05`, `2026-11-11`, `2026-11-14` usw.) und `testSequenced` gilt. Ein neuer Test
auf einem schon benutzten Tag kann fremde Tests rot machen. Ein Kommentar oder eine Hilfe
"frischer Tag je Test" würde das absichern.

### Hinweise

H1. a0a8cab, `src/Server/Api.fs:1986`. Hinweis. Regel: `standards/backend/overview.md:64` und
`:99` (Logging über `AddLogging`). Der Startlog geht über `printfn`, ebenso
`src/Server/BotApi.fs:373`. Das entspricht dem Rest des Servers; die Standards-Regel ist hier
nicht gelebt. `webApp()` wird genau einmal aufgerufen (`src/Server/Program.fs:81`), der Log
erscheint also einmal.

H2. a0a8cab, `src/Server/ServerConfig.fs:29` und `:38`. Hinweis. Regel:
`.claude/agents/review-code.md:65` (keine Duplikation). `defaultBotMaxAmountEur` und
`hardBotMaxAmountEur` sind beide 500m. Inhaltlich sind es zwei Begriffe (Vorgabe, Deckel), die
Trennung ist vertretbar. Der Test `Expect.equal hardBotMaxAmountEur 500m "Konstante"`
(`src/Tests/BotValidationTests.fs`, Commit a0a8cab) ist tautologisch nah, hält aber den
ADR-Wert fest.

H3. a20fef6, `src/Server/Validation.fs:215-220`. Hinweis. Ohne Regelbezug.
`berlinDayBoundsUtc` bestimmt den Offset an `Mitternacht als UTC minus 1,5 Stunden`. Das ist
korrekt, weil die Umstellung um 01:00 UTC liegt und nie zwischen 22:00 und 23:00 UTC, aber
schwer nachvollziehbar. `TimeZoneInfo.ConvertTimeToUtc(localMidnight, berlinZone.Value)` wäre
direkter, seit es keinen Rückfall mehr gibt.

H4. a20fef6, `src/Tests/BotValidationTests.fs` (Test "die Berliner Zeitzone ist auflösbar").
Hinweis. Der F#-Test läuft auf der Entwicklermaschine mit Zeitzonendatenbank und kann das
fehlende tzdata im Image nicht erkennen. Das übernimmt korrekt
`scripts/dockerfile-tzdata.test.mjs`; laut Task-Datei wurde auch ein echter `docker build` mit
`test -e /usr/share/zoneinfo/Europe/Berlin` geprüft.

H5. ae7cfb5. Hinweis. Ohne Regelbezug. Ein Security-Befund (N1) ist als `chore(ledger)`
committet, die anderen Review-Befunde als `bug(...)`. Die Tests in `BotApiTests.fs`
setzen `Request.Path` direkt auf `/bot%2fcategories`; dass Kestrel `%2F` wirklich unausgepackt
lässt, steht nur im Kommentar `src/Server/BotApi.fs:388-389` und ist nicht gegen echtes Kestrel
geprüft. Die Aussage stimmt nach meinem Wissen.

H6. b4b5cc3, `src/Server/Ledger/Persistence.fs:856-873`. Hinweis. Regel:
`.claude/rules/persistence-safety.md:11`, `.claude/rules/query-safety.md:12` erfüllt
(parametrisiert, `Core.getConnection`). Der Zeitvergleich über `occurred_at` ist ein
Textvergleich von ISO-Strings (`dateToText`, Format `o`). Das ist korrekt, solange alle Werte
UTC mit `Z` sind, was hier gilt (`DateTime.UtcNow`, `SpecifyKind Utc`). Beide Regeln gelten per
`paths:` nur für `src/Server/Persistence.fs` und erfassen `src/Server/Ledger/Persistence.fs`
mechanisch nicht.

H7. Alle Bug-Commits (0f39ac0, a0a8cab, 897b13c, ae7cfb5). Hinweis. Regel:
`.claude/rules/testing.md:23` (BDD-Szenarien "kaputt" und "intakt" vor dem Fix). Die
Task-Dateien enthalten keine GIVEN/WHEN/THEN-Szenarien; inhaltlich decken die Tests aber
beides ab (z.B. a0a8cab: "500,00" ungültig und "100" senkt ab; 897b13c: Handy 48 px und
Desktop unter 44 px; ae7cfb5: `/BOT` landet im Handler und `/botanik` nicht). Die Regel ist
für agentheim-Tasks mit Akzeptanzkriterien redundant.

## Lob

- Jeder Bug-Commit hat einen Regressionstest mit Kommentar zum verhinderten Fehler
  (CLAUDE.md:335 und :349), z.B. `src/Tests/BotValidationTests.fs` ("Verhindert den Fehler,
  dass "500,00" als 50000 gelesen wurde"), `src/Tests/CorsTests.fs:31-33`.
- Nebenläufigkeit ist ernst genommen und getestet: zehn gleichzeitige Commits derselben
  Vorschau (BotApiTests.fs:346), gleichzeitige Commits verschiedener Vorschauen an der
  Tagesgrenze (:452), 200 parallele `TryAcquire` (:183).
- Token-Vergleich in konstanter Zeit (`BotApi.fs:228-236`), kein Token heißt abgeschaltet statt
  offen (`ServerConfig.fs`, `resolveBotToken`).
- `BotDeps` injiziert Uhr, YNAB, Ziel und Speicher; die Routen sind ohne Netz testbar.
- Der tzdata-Wächter ist rein, prüft Positiv- und Negativfälle samt Kommentar- und
  Fortsetzungszeilen.
- `actorFromColumns` bleibt exhaustiv mit Fehler für Unbekanntes, `Bob` ist additiv.
- Der Playwright-Wächter misst echte Knöpfe im echten Split-Editor auf zwei Breiten.

## Konsultierte Regeln und ihre Brauchbarkeit

Konsultiert:
- `CLAUDE.md` (Projekt) und `~/.claude/CLAUDE.md`
- `.claude/rules/`: build-verification, css-animation-safety, design-tokens, domain-purity,
  elmish-state, fable-pitfalls, feature-workflow, localization, persistence-safety,
  query-safety, semantic-messaging, serena-tools, testing
- `.claude/agents/review-code.md` (Checkliste dieses Reviewers)
- `standards/backend/overview.md`, `standards/backend/error-handling.md`,
  `standards/global/architecture.md`, `standards/shared/types.md`,
  `standards/frontend/styleguide.md` (§3 Token-Layer, §4 Touch-Targets)
- `.agentheim/knowledge/contexts/ledger/README.md` (Zeilen 226-230), Task-Dateien der Commits,
  `docs/bot-api.md`

Nützlich:
- CLAUDE.md Bug-Fix-Protokoll (:335-349) und Diary-Pflicht (:410): klar prüfbar, alle Commits
  erfüllen sie.
- `standards/frontend/styleguide.md` §3/§4: konkret, lieferte N6.
- `.claude/rules/persistence-safety.md`, `query-safety.md`: kurz und prüfbar (H6), auch wenn der
  Pfad-Filter zu eng ist.
- `standards/backend/error-handling.md:7`: brauchbare Abgrenzung erwartet/unerwartet (M5).
- `docs/bot-api.md` und das Ledger-README: unverzichtbar, um Verhalten gegen Absicht zu prüfen
  (M1, M2, M3). Die meisten echten Befunde kamen aus diesem Abgleich, nicht aus den Regeln.

Nicht anwendbar:
- `elmish-state.md`, `css-animation-safety.md`, `fable-pitfalls.md` (kein State-, CSS- oder
  Remoting-Code in den Diffs), `semantic-messaging.md` (keine neuen Command-DUs),
  `localization.md` (alle Optionen auskommentiert, also wirkungslos).
- `domain-purity.md`: gilt per `paths:` nur für `src/Server/Domain.fs`, die es nicht gibt. Die
  echte Domäne liegt unter `src/Server/Ledger/Domain.fs` und anderen BC-Ordnern.
- CLAUDE.md Backlog- und Milestone-Abschnitte (:150, :172): das Projekt führt Arbeit im
  agentheim-Board, nicht in `/backlog.md`.

Veraltet oder widersprüchlich:
- `build-verification.md` (ganz) und `testing.md:50-51`: verweisen auf Supabase, `DATABASE_URL`,
  `RUN_SMOKE_TESTS` und `standards/testing/smoke-tests.md`. Das Projekt nutzt SQLite, im
  Testprojekt gibt es kein `RUN_SMOKE_TESTS`, die Datei `smoke-tests.md` existiert nicht.
  `build-verification.md:37` nennt CORS als Smoke-Test-Grund, seit 0f39ac0 gibt es kein CORS mehr.
- `feature-workflow.md:9-30`: verlangt `src/Server/Commands.fs`, `Events.fs`, `EventHandlers.fs`
  auf oberster Ebene, `spec.md`, `/spec-builder` und `verify-spec-implementation`. Das Repo hat
  BC-Ordner (`src/Server/Ledger/Events.fs`) und agentheim-Tasks. Der Diary-Formatvorschlag
  (:94-109) weicht vom Format in CLAUDE.md (:432-457) ab.
- CLAUDE.md:3 verlangt Serena zwingend, `serena-tools.md:19-30` erlaubt `Read`/`Edit`
  situationsabhängig. In dieser Sitzung war Serena nicht verfügbar; die Pflicht war nicht
  erfüllbar.
- CLAUDE.md:223 und `review-code.md:39` "ALL types in src/Shared/" widerspricht der gelebten
  Praxis (Server-interne Typen in `BotApi.fs`, `Ledger/Events.fs`) und der eigenen Begründung in
  `standards/shared/types.md:7`.
- `review-code.md:61` (Funktionen unter 30 Zeilen) und `:63` (kein veränderlicher Zustand) sind
  als harte Regeln für Handler, Parser und Infrastruktur zu absolut.
- `testing.md:23` (BDD-Szenarien) doppelt sich mit den Akzeptanzkriterien der agentheim-Tasks.
