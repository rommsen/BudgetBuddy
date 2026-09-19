---
id: infra-jh7g9
title: "Backend-Port konfigurierbar machen: app.Run überschreibt das bereits gesetzte ASPNETCORE_URLS, zwei Instanzen können nicht nebeneinander laufen"
status: backlog
type: bug
context: infrastructure
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [deployment, dev-umgebung, test-infra]
related_adrs: [0010]
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
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
Gefunden am 2026-09-19, als Roman mit laufendem Entwicklungsserver einen Screenshot der
`/styleguide`-Route ziehen wollte. Der Screenshot-Lauf selbst scheiterte zusätzlich an
einem zweiten, unabhängigen Problem: das Wartepredikat blockiert die Styleguide-Route,
weil deren Demo-Inhalte dauerhaft Spinner und Skeletons zeigen (`design-system-yxrxb`).
Beide Befunde stammen aus demselben Aufruf, sind aber verschiedene Ursachen und gehören in
verschiedene Kontexte: der Port hierher nach `infrastructure`, das Predikat ins
Design-System.

Eine kleinere, aber sofort wirksame Entlastung wäre unabhängig davon möglich: Für rein
präsentationale Routen wie `/styleguide` braucht das Skript gar kein Backend.
`scripts/export-design-system.mjs` nimmt diese Route seit Monaten ohne Server auf, allein
mit `vite preview`. Das löst den Konflikt für diesen Fall vollständig, ohne dass am Port
etwas geändert werden muss, aber eben nur für Routen ohne Datenbedarf. Beim Verfeinern
entscheiden, ob das als eigener, kleiner Schritt vorgezogen wird.
