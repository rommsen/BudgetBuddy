---
id: 0034
title: "Backend-Port über ASPNETCORE_URLS statt Literal, mechanisch abgesichert"
scope: infrastructure
status: accepted
date: 2026-09-19
supersedes: []
superseded_by: []
related_tasks: [infra-jh7g9]
related_adrs: [0010, 0033]
related_research: []
---

# ADR 0034: Backend-Port über ASPNETCORE_URLS statt Literal, mechanisch abgesichert

## Context

`src/Server/Program.fs` rief bislang `app.Run("http://0.0.0.0:5081")` mit einem festen
URL-Argument auf. `compose.yaml` setzte parallel dazu bereits
`ASPNETCORE_URLS=http://+:5081` und der Header-Kommentar der Datei beschrieb den Port so,
als sei er über diese Variable konfigurierbar. Das stimmte nicht: ein explizites
URL-Argument an `app.Run` überschreibt `ASPNETCORE_URLS` bedingungslos. Die Zeile in
`compose.yaml` war wirkungslos, ohne dass das beim Lesen der Konfiguration auffiel, nur im
Betrieb (siehe Task `## Why`: derselbe Fehlerklasse-Fund wie in `design-system-bq4tz`:
eine Stelle, die aussieht, als kontrolliere sie etwas, tut es aber nicht).

Konkret aufgefallen, als ein laufender Entwicklungsserver auf 5081 einen zweiten Lauf von
`npm run screenshot:route` blockierte: das Skript reserviert dem Frontend längst einen
freien Port, dem Backend nicht, weil dessen Port nicht konfigurierbar war.

Iteration 1 dieser Aufgabe behob den Kern (siehe Decision 1-3 unten), lieferte aber keine
Durchsetzung: die neue Konvention stand nur in Prosa (README-Delta, dieser ADR). Der
Verifier lehnte das unter Check 6c (mechanize-or-drop, ADR-0059) ab, mit Verweis auf das
Hausmuster aus `src/Tests/ModuleLevelDbInitGuardTests.fs` (ADR 0033): eine Konvention, die
sich mechanisch mit vertretbarem Aufwand prüfen lässt, muss geprüft werden, sonst bleibt
ein künftiger Rückfall (`app.Run("http://0.0.0.0:5081")` erneut, oder `BACKEND_PORT`
verliert sich aus einem der beiden Übergabepunkte) unbemerkt grün. Iteration 2 (dieser
ADR-Stand) schließt genau diese Lücke.

## Decision

1. **`app.Run()` wird ohne Argument aufgerufen.** `app.Run()` respektiert
   `ASPNETCORE_URLS` selbst. `Program.fs` setzt die Variable, falls sie nicht gesetzt ist,
   über `Server.ServerConfig.resolveUrls` (reine Funktion, siehe `src/Server/ServerConfig.fs`)
   auf den bisherigen Standard `http://0.0.0.0:5081`, VOR `WebApplication.CreateBuilder`
   (die Konfiguration liest Umgebungsvariablen dort ein; ein späteres Setzen käme zu spät).
2. **Die Start-Log-Zeile nennt den tatsächlich gebundenen Wert** (`urls`, aus `resolveUrls`
   aufgelöst), keinen festen Port mehr.
3. **`vite.config.js`s Proxy-Ziel liest `BACKEND_PORT`** aus der Umgebung (Vorgabe `5081`,
   wenn nicht gesetzt), statt hart auf `localhost:5081` zu zeigen.
4. **`scripts/screenshot-route.mjs` reserviert für Backend und Frontend je einen freien
   Port** (`getFreePort()`, wie zuvor schon fürs Frontend) und reicht den Backend-Port als
   `ASPNETCORE_URLS=http://0.0.0.0:<port>` an den Server-Kindprozess sowie als
   `BACKEND_PORT=<port>` an den Vite-Kindprozess weiter (`startFrontend`). Die frühere
   Abbruchprüfung ("Port 5081 belegt") entfällt, weil ein belegter 5081 den Lauf nicht mehr
   blockiert.
5. **Beide neuen Kopplungen sind mechanisch abgesichert, nicht nur dokumentiert:**
   - `src/Tests/BackendPortGuardTests.fs` liest den tatsächlichen Quelltext von
     `Program.fs` und lehnt jeden `app.Run(...)`-Aufruf mit nicht-leerem Argument laut ab
     (Regel aus Decision 1). String-literal-bewusster Kommentar-Scan (verfolgt
     `"..."`-Literale pro Zeile), damit weder ein Kommentar, der `app.Run()` als Text
     erwähnt, fälschlich als Verstoß zählt, noch ein URL-Literal-Argument (das selbst ein
     `//` enthält, z. B. `"http://0.0.0.0:5081"`) den naiven "ab dem ersten `//`
     abschneiden"-Ansatz umgeht. Getestet, dass genau dieser zweite Fall erkannt wird
     (eigene Falle: eine naive erste Fassung dieses Wächters brach an ihrem eigenen
     Prüfzweck, weil sie das `//` im URL-Literal selbst als Kommentarbeginn las).
   - Zwei neue Tests in `scripts/screenshot-route.test.mjs` lesen `screenshot-route.mjs`
     bzw. `vite.config.js` und schlagen fehl, wenn `startFrontend` `BACKEND_PORT` nicht
     mehr an den Vite-Kindprozess übergibt, oder wenn `vite.config.js` `BACKEND_PORT`
     nicht mehr liest (Regel aus Decision 3/4). Quelltext-Scan wie der bereits vorhandene
     `data-app-ready`-Test in derselben Datei, kein funktionaler Vite-Start (für diesen
     schmalen Zweck unverhältnismäßig teuer).
   - Alle drei neuen Tests wurden falsifizierbar erprobt: das jeweilige Muster testweise
     zurückgebaut (`app.Run("http://0.0.0.0:5081")` bzw. `BACKEND_PORT` aus beiden Stellen
     entfernt), der jeweilige Test schlug rot fehl mit der erwarteten Meldung, danach
     zurückgesetzt und wieder grün (Zahlen im Outcome-Text des Workers).
6. **Der Container bindet weiterhin auf 5081, unverändert.** `compose.yaml` und
   `Dockerfile` sind nicht angefasst; die dort bereits vorhandene
   `ASPNETCORE_URLS=http://+:5081`-Zeile ist jetzt tatsächlich wirksam, statt nur so
   auszusehen (ADR 0010: Healthcheck und `tailscale serve --https=443 -> 127.0.0.1:5081`
   hängen an diesem festen Port).

## Consequences

- Zwei Instanzen können gleichzeitig laufen (ein Entwicklungsserver auf 5081, ein zweiter
  auf einem freien Port), ohne dass eine die andere blockiert. `npm run screenshot:route`
  läuft bei belegtem 5081 durch statt abzubrechen.
- Ein künftiger Rückfall auf ein festes URL-Argument in `app.Run` fällt beim nächsten
  `dotnet test` laut auf, nicht erst im Betrieb. Ein künftiger Verlust der
  `BACKEND_PORT`-Weitergabe zwischen `screenshot-route.mjs` und `vite.config.js` fällt
  beim nächsten `node --test scripts/screenshot-route.test.mjs` laut auf.
- Dokumentierte Grenze des `Program.fs`-Wächters: der string-literal-bewusste
  Kommentar-Scan geht davon aus, dass ein `//` innerhalb eines `"..."`-Literals korrekt
  erkannt wird (Escape via `\"`), erkennt aber keine mehrzeiligen Kommentare (`(* ... *)`,
  in F# unüblich, in `Program.fs` nicht verwendet) und keine verschachtelten
  Anführungszeichen jenseits einfacher Escapes. Für die eine, schmale Datei, die dieser
  Wächter prüft, ist das ausreichend; ein genereller F#-Parser wäre für diesen Zweck
  unverhältnismäßig.
- Dokumentierte Grenze der beiden `screenshot-route.test.mjs`-Tests: ein Quelltext-Scan
  erkennt eine korrekt benannte, aber falsch verdrahtete Variable nicht (z. B.
  `BACKEND_PORT` wird gesetzt, aber nie tatsächlich gelesen): dagegen hilft nur ein
  echter, teurer Lauf des gesamten Skripts (Server + Vite + Playwright), den die
  Verifier-Iteration 1 bereits einzeln nachgewiesen hat und der nicht bei jedem
  `dotnet test`/`node --test`-Lauf wiederholt wird.
- `design-system-m4kp7` (abhängig von dieser Aufgabe) kann jetzt darauf aufbauen, dass
  präsentationale Routen wie `/styleguide` ohne Backend auskommen, ohne die hier gelöste
  Portkollision erneut zu berühren.
