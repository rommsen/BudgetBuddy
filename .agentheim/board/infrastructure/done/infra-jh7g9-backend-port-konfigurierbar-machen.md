---
id: infra-jh7g9
title: "Backend-Port konfigurierbar machen: app.Run überschreibt das bereits gesetzte ASPNETCORE_URLS, zwei Instanzen können nicht nebeneinander laufen"
status: done
type: bug
context: infrastructure
created: 2026-09-19
completed:
depends_on: []
blocks: [design-system-m4kp7]
tags: [deployment, dev-umgebung, test-infra]
related_adrs: [0010, 0034]
related_research: []
prior_art: [design-system-wa6wz]
---

## Why
Der Backend-Port steht als Literal in `src/Server/Program.fs:85`:

```fsharp
app.Run("http://0.0.0.0:5081")
```

Damit lässt sich keine zweite Instanz starten. Konkret aufgefallen am 2026-09-19: Roman
hatte seinen Entwicklungsserver laufen und rief `npm run screenshot:route -- '/styleguide'`
auf. Das Skript startet einen eigenen Server (gegen eine Temp-Kopie der Datenbank, ADR
0030) und scheiterte mit `AddressInUseException` auf 5081. Für das Frontend reserviert es
längst einen freien Port (`getFreePort()`, im Lauf etwa 65196); fürs Backend kann es das
nicht, weil der Port nicht konfigurierbar ist. `scripts/screenshot-route.mjs:35-38`
dokumentiert diese Grenze bereits als bekannt.

**Der eigentliche Fund ist ein anderer, und er ist ernster.** `compose.yaml:28` setzt
bereits

```
ASPNETCORE_URLS=http://+:5081
```

und der Header-Kommentar der Datei (Zeile 10 bis 11) beschreibt den Port so, als sei er
über diese Variable gesetzt. Das stimmt nicht: Ein explizites URL-Argument an `app.Run`
**überschreibt** `ASPNETCORE_URLS`. Die Zeile in `compose.yaml` ist heute wirkungslos, die
Konfigurierbarkeit ist vorgetäuscht. Wer den Port dort ändert, ändert nichts, und das
fällt erst im Betrieb auf, nicht beim Lesen der Konfiguration.

Das ist dieselbe Fehlerklasse, die in `design-system-bq4tz` zweimal zugeschlagen hat: eine
Stelle, die aussieht, als kontrolliere sie etwas, und es nicht tut.

## What
Den Port an beiden Stellen konfigurierbar machen, mit unverändertem Standardverhalten.

**1. `src/Server/Program.fs:83, 85`.** `app.Run()` ohne Argument respektiert
`ASPNETCORE_URLS` von selbst. Ein Standardwert muss dann anders kommen, etwa indem die
Variable vorbelegt wird, wenn sie nicht gesetzt ist. Die Log-Zeile darüber (Zeile 83) nennt
den Port fest und muss den tatsächlich gebundenen ausgeben, sonst lügt sie bei abweichender
Konfiguration.

**2. `vite.config.js:77`.** Das Proxy-Ziel zeigt hart auf `http://localhost:5081`. Ein
konfigurierbarer Server allein reicht nicht, sonst redet das Frontend weiter mit dem
falschen Port. Vite kann die Variable im Config-File lesen, Standard bleibt 5081.

**3. `scripts/screenshot-route.mjs`.** Danach kann `BACKEND_PORT` (Zeile 78) einem
`getFreePort()` weichen, der reservierte Port geht per Umgebungsvariable an Server und
Vite. Die Abbruchprüfung in `startBackend` (Zeile 258) wird damit überflüssig, und der
Kommentar über die bekannte Grenze (Zeile 35 bis 38) kann weg.

**Vor der Umsetzung prüfen, was sonst noch auf 5081 festgenagelt ist.** Vollständige Liste
aus einem Suchlauf vom 2026-09-19 (ohne Doku, Diary und Task-Dateien):

| Datei | Zeile | Bedeutung |
|---|---|---|
| `src/Server/Program.fs` | 83, 85 | Log-Zeile und `app.Run`, der Kern |
| `vite.config.js` | 77 | Proxy-Ziel `/api` |
| `compose.yaml` | 28 | `ASPNETCORE_URLS`, heute wirkungslos |
| `compose.yaml` | 40 | Healthcheck des Containers |
| `compose.yaml` | 64 | `tailscale serve --https=443 http://127.0.0.1:5081` |
| `compose.yaml` | 10, 11 | Header-Kommentar, beschreibt die Annahme |
| `Dockerfile` | 68 | `EXPOSE 5081` |
| `Dockerfile` | 72 | `HEALTHCHECK` |
| `scripts/screenshot-route.mjs` | 78, 258, 35-38 | Konstante, Abbruchprüfung, Kommentar |

Der Vite-Port 5181 (`vite.config.js:74`) ist davon nicht betroffen, das Skript reserviert
dort ohnehin schon frei.

**Der Deployment-Pfad darf sich nicht ändern.** Im Container bleibt 5081 der gebundene
Port, weil Healthcheck und `tailscale serve` darauf zeigen (ADR 0010, `infra-002`). Ziel
ist nur, dass der Wert aus der Konfiguration kommt statt aus dem Quelltext, und dass die
bereits vorhandene `ASPNETCORE_URLS`-Zeile endlich das tut, was sie verspricht.

## Acceptance criteria
- [ ] Ohne gesetzte Umgebungsvariable bindet der Server weiterhin auf 5081; `npm run dev:all`
      funktioniert unverändert.
- [ ] Mit gesetzter Variable bindet der Server auf den angegebenen Port, und die Log-Zeile
      beim Start nennt den tatsächlich gebundenen Port, nicht einen festen Wert.
- [ ] Der Vite-Proxy folgt derselben Variable; ein Frontend gegen einen abweichenden
      Server-Port erreicht dessen `/api`.
- [ ] Zwei Instanzen laufen gleichzeitig: ein Entwicklungsserver auf 5081 und ein zweiter
      auf einem freien Port, beide erreichbar. Nachgewiesen durch einen tatsächlichen Lauf,
      nicht durch Lesen des Codes.
- [ ] `npm run screenshot:route` läuft bei belegtem 5081 durch, statt abzubrechen.
- [ ] Der Container bindet weiterhin auf 5081: `compose.yaml`-Healthcheck und
      `tailscale serve` funktionieren unverändert, und die `ASPNETCORE_URLS`-Zeile ist jetzt
      tatsächlich wirksam (nachweisbar, indem eine Änderung dort den gebundenen Port ändert).
- [ ] Die Log-Zeile beim Start (`src/Server/Program.fs`, heute
      `printfn "🚀 BudgetBuddy server starting on http://localhost:5081"`) nennt keinen
      festen Port mehr.
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
Gefunden am 2026-09-19, als Roman mit laufendem Entwicklungsserver einen Screenshot der
`/styleguide`-Route ziehen wollte. Der Screenshot-Lauf selbst scheiterte zusätzlich an
einem zweiten, unabhängigen Problem: das Wartepredikat blockiert die Styleguide-Route,
weil deren Demo-Inhalte dauerhaft Spinner und Skeletons zeigen (`design-system-yxrxb`).
Beide Befunde stammen aus demselben Aufruf, sind aber verschiedene Ursachen und gehören in
verschiedene Kontexte: der Port hierher nach `infrastructure`, das Predikat ins
Design-System.

**Entschieden (Roman, 2026-09-19):** Die zweite, unabhängige Entlastung — präsentationale
Routen wie `/styleguide` brauchen gar kein Backend, `scripts/export-design-system.mjs` nimmt
sie seit Monaten allein mit `vite preview` auf — wird gebaut, aber als eigener Task
(`design-system-m4kp7`). Diese Aufgabe hier heilt die Ursache: der Port kommt aus der
Konfiguration statt aus dem Quelltext, und `compose.yaml`s `ASPNETCORE_URLS`-Zeile tut
endlich, was sie verspricht.

**Fasst `scripts/screenshot-route.mjs` an** (`startBackend`, `main`). Dieselbe Datei
bearbeiten auch `design-system-yxrxb` (Wartepredikat), `ledger-p9k3w` (Screenshot-Aufruf)
und `design-system-m4kp7`. Letzterer hängt deshalb an dieser Aufgabe.

## Verifier note (iteration 1)

**VERDICT: FAIL** — ITERATION_HINT: likely-fixable

**REASONS:**

- Check 6c (mechanize-or-drop, ADR-0059) greift und ist nicht erfüllt. Die Aufgabe etabliert
  eine vorwärtsgerichtete Konvention, formuliert als Nie-Regel im `README_DELTA` für
  `Invariants / conventions`: "Backend-Port kommt aus ASPNETCORE_URLS, nie aus einem Literal",
  `src/Server/Program.fs` ruft `app.Run()` ohne URL-Argument auf. ADR 0034 wiederholt sie in
  Decision 1 und ergänzt in Decision 4/5 plus Consequences eine zweite Regel: `vite.config.js`
  und `scripts/screenshot-route.mjs` müssen `BACKEND_PORT` konsistent setzen bzw. lesen,
  "sonst redet das Frontend mit dem falschen Backend-Port".
- Enforcement fehlt. Die vier neuen Tests in `src/Tests/ServerConfigTests.fs` prüfen
  ausschließlich `resolveUrls` (null, leerer String, gesetzter Wert, abweichender Port).
  Schreibt ein künftiger Worker wieder `app.Run("http://0.0.0.0:5081")` in
  `src/Server/Program.fs:99`, oder verliert `startFrontend` das `BACKEND_PORT` aus dem
  Kind-Env (`scripts/screenshot-route.mjs:307`), bleiben alle 908 Tests grün. Das ist genau
  die stille Fehlerklasse, die diese Aufgabe laut `## Why` schließen soll: eine Stelle, die
  aussieht, als kontrolliere sie etwas, und es nicht tut.
- Der zweite zulässige Weg fehlt ebenfalls: die Task-Datei trägt weder ein Akzeptanzkriterium
  für eine mechanische Absicherung noch einen ausdrücklichen "prose-only, unenforced"-Vermerk
  in `## Notes`. Die Lücke ist damit unsichtbar statt bewusst entschieden.
- Hausmuster für genau diesen Fall existiert bereits im selben Repo und wäre schmal:
  `src/Tests/ModuleLevelDbInitGuardTests.fs` (ADR 0033), `src/Tests/TestDatabaseGuardTests.fs`,
  `src/Tests/FontFaceRegressionTests.fs` und der quelltextlesende Test in
  `scripts/screenshot-route.test.mjs` ("prüft nicht mehr auf .proto-spinner/.animate-pulse,
  sondern auf data-app-ready") setzen Konventionen durch, indem sie die Quelldatei selbst
  scannen.

**Kein Defekt, nur Kontext für die nächste Iteration — die Checks 1 bis 6b sind grün und der
Verifier hat sie selbst ausgeführt:**

- `dotnet test` aus dem Worktree: Exit 0, 901 bestanden, 7 übersprungen, 908 gesamt.
  `npm run build`: Exit 0. `node --test scripts/screenshot-route.test.mjs`: Exit 0,
  19 bestanden.
- Zwei Instanzen liefen gleichzeitig: die Instanz ohne gesetzte Variable loggte
  `🚀 BudgetBuddy server starting on http://0.0.0.0:5081` und antwortete mit HTTP 404 auf
  5081, die Instanz mit `ASPNETCORE_URLS=http://0.0.0.0:6123` loggte den 6123-Wert und
  antwortete dort. `lsof` zeigte beide Listener parallel, beide Ports waren danach wieder
  frei, beide liefen gegen Scratch-`DATA_DIR`.
- Der Vite-Proxy folgt der Variable nachweislich: Import von `vite.config.js` liefert ohne
  Variable `{"/api":{"target":"http://localhost:5081"}}` und mit `BACKEND_PORT=6123`
  `{"/api":{"target":"http://localhost:6123"}}`.
- Beide `startBackend`-Aufrufstellen sind an die neue Signatur angepasst, weitere Aufrufer
  gibt es im Repo nicht.
- `assertDataDirIsolated` ist weiterhin die erste Anweisung vor jedem `await` und bekommt
  genau den Wert, der in `env.DATA_DIR` landet.
- `isPortFree` bleibt für `stopServer` in Gebrauch, ist also kein toter Code.
- `TestDatabaseGuardTests.fs` steht unverändert als erste Compile-Unit in `Tests.fsproj`.
- `compose.yaml` und `Dockerfile` sind unberührt und binden weiter 5081, das jetzt
  tatsächlich aus `ASPNETCORE_URLS` kommt.
- Den vollen Screenshot-Lauf (AC5) hat der Verifier nicht wiederholt, weil er `dotnet run`
  plus Vite plus Playwright kostet; die tragende Mechanik dahinter wurde stattdessen einzeln
  nachgewiesen.
- Arbeitsbaum sauber (`git status --porcelain` leer).

**SUGGESTED_FIX:**

Entweder einen schmalen Wächter-Test nach Hausmuster ergänzen (F#-Test, der
`src/Server/Program.fs` liest und ein URL-Argument an `app.Run` laut ablehnt, plus optional
ein Node-Test, der prüft, dass `screenshot-route.mjs` `BACKEND_PORT` an den Vite-Prozess
weiterreicht und `vite.config.js` es liest), oder den Vermerk "prose-only, unenforced" in
`## Notes` der Task-Datei aufnehmen, damit die fehlende Absicherung eine sichtbare
Entscheidung ist statt eines Versehens.

## Outcome

Der Backend-Port ist konfigurierbar über `ASPNETCORE_URLS`, und die bereits vorhandene
`ASPNETCORE_URLS`-Zeile in `compose.yaml` ist jetzt tatsächlich wirksam statt nur so
auszusehen (siehe ADR 0034 für die vollständige Begründung und alle Entscheidungen).

**Iteration 1** (Kernfix, vom Verifier bereits einzeln geprüft und für die Checks 1 bis 6b
grün befunden, siehe Verifier-Notiz an der Task-Datei): `app.Run()` in
`src/Server/Program.fs` läuft ohne URL-Argument, `Server.ServerConfig.resolveUrls`
(`src/Server/ServerConfig.fs`, neu, rein) liefert ohne gesetzten Wert weiterhin
`http://0.0.0.0:5081`; `vite.config.js` liest `BACKEND_PORT` fürs Proxy-Ziel;
`scripts/screenshot-route.mjs` reserviert für Backend und Frontend je einen freien Port
und reicht ihn als `ASPNETCORE_URLS` bzw. `BACKEND_PORT` an die jeweiligen
Kindprozesse weiter; `compose.yaml`/`Dockerfile` unverändert (weiterhin 5081, ADR 0010).

**Iteration 2** (diese Runde, schließt Check 6c): der Verifier bemängelte, dass die neue
Konvention nur in Prosa stand, ohne Durchsetzung. Ergänzt:

- `src/Tests/BackendPortGuardTests.fs` (neu, 7 Testfälle): liest `src/Server/Program.fs`
  selbst und lehnt jeden `app.Run(...)`-Aufruf mit nicht-leerem Argument laut ab. Der Scan
  ist string-literal-bewusst (trackt `"..."`-Literale pro Zeile, statt naiv am ersten `//`
  abzuschneiden): eine erste, naive Fassung brach genau an ihrem eigenen Prüfzweck, weil
  `app.Run("http://0.0.0.0:5081")` selbst ein `//` (in "http://") enthält und der naive
  Kommentar-Schnitt den Aufruf vor dem eigentlichen Vergleich abschnitt, sodass der
  Bug-Fall unerkannt blieb (`dotnet test` zeigte das sofort: zwei der sieben neuen
  Testfälle schlugen rot fehl, "Should not be empty"). Mit der string-literal-bewussten
  Fassung: alle 7 grün.
- `scripts/screenshot-route.test.mjs`: zwei neue Tests (jetzt 21 statt 19 Tests
  insgesamt) lesen `screenshot-route.mjs` bzw. `vite.config.js` und schlagen fehl, wenn
  `startFrontend` `BACKEND_PORT` nicht mehr an den Vite-Kindprozess weitergibt oder
  `vite.config.js` es nicht mehr liest.
- `src/Tests/Tests.fsproj`: `BackendPortGuardTests.fs` registriert (nach
  `ServerConfigTests.fs`, vor `EncryptionTests.fs`; `TestDatabaseGuardTests.fs` bleibt
  unangetastet die erste Compile-Unit).

**Falsifizierbarkeits-Probe (wie vom Verifier gefordert), alle drei mit beobachteten
Zahlen:**

1. `app.Run()` in `src/Server/Program.fs` testweise durch
   `app.Run("http://0.0.0.0:5081")` ersetzt. `dotnet test --filter "FullyQualifiedName~app.Run"`:
   1 fehlgeschlagen, 6 bestanden, mit der Meldung
   `app.Run() mit URL-Argument gefunden (...): Program.fs:99: app.Run("http://0.0.0.0:5081")`.
   Zurückgesetzt: 7 bestanden, 0 fehlgeschlagen.
2. `BACKEND_PORT: String(backendPort)` aus dem an `spawn("npx", ["vite", ...])`
   übergebenen Env-Objekt in `startFrontend` entfernt UND `const backendPort =
   process.env.BACKEND_PORT || '5081'` in `vite.config.js` durch ein Literal `'5081'`
   ersetzt. `node --test scripts/screenshot-route.test.mjs`: beide neuen Tests schlugen
   rot fehl mit genau den erwarteten Meldungen ("startFrontend muss BACKEND_PORT an den
   Vite-Kindprozess übergeben ...", "vite.config.js muss BACKEND_PORT lesen ..."), alle
   anderen 19 blieben grün. Zurückgesetzt: 21 bestanden, 0 fehlgeschlagen.

**Alle Gates grün, vollständig aus dem Worktree erneut gelaufen:**

- `dotnet test`: 908 bestanden, 7 übersprungen (Integrationstests ohne `.env`), 915
  gesamt, 0 fehlgeschlagen.
- `dotnet build`: 0 Warnungen, 0 Fehler.
- `npm run build`: erfolgreich (die "eval in Rules/State.fs"-Warnung und der
  Chunk-Größen-Hinweis sind bestehend, unabhängig von dieser Aufgabe).
- `node --test scripts/screenshot-route.test.mjs`: 21 bestanden, 0 fehlgeschlagen.
- Arbeitsbaum nach dem Zurücksetzen beider Proben sauber (`git status --porcelain`: nur
  die drei tatsächlich beabsichtigten Änderungen aus Iteration 2:
  `scripts/screenshot-route.test.mjs`, `src/Tests/Tests.fsproj`, die neue Datei
  `src/Tests/BackendPortGuardTests.fs`).

Den vollständigen Zwei-Instanzen-Lauf (AC4/AC6) und den Vite-Proxy-Nachweis (AC3) hat
bereits der Verifier in Iteration 1 tatsächlich ausgeführt und einzeln bestätigt (siehe
Verifier-Notiz an der Task-Datei); diese Iteration hat nur die Durchsetzung ergänzt, ohne
diese Mechanik erneut anzufassen.

Kein neues Backlog-Item: die einzige verbleibende, dokumentierte Grenze (Quelltext-Scan
erkennt keine korrekt benannte, aber falsch verdrahtete Variable) ist in ADR 0034 unter
Consequences benannt und über den bereits vom Verifier in Iteration 1 ausgeführten echten
Lauf abgedeckt, nicht über einen mechanischen Test.
