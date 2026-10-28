---
id: infrastructure-z9b5w
title: "CORS-Policy AllowAll entfernen: sie gilt in Produktion für die ganze API und wird auch in Development nicht gebraucht (Security-Review M4)"
status: done
type: bug
context: infrastructure
created: 2026-10-28
completed:
depends_on: []
blocks: []
tags: [security, cors]
related_adrs: []
related_research: []
prior_art: []
---

## Why
BB hat keine Anmeldung. Mit `access-control-allow-origin: *` auf der ganzen API kann jede Webseite, die im Browser eines Tailnet-Geräts geöffnet ist, die API lesen und schreiben (Security-Review M4). Das ist unabhängig von Bob und kommt zuerst.

## What
`src/Server/Program.fs` registriert die Policy "AllowAll" (`AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`) und ruft `app.UseCors("AllowAll")` in jeder Umgebung auf, obwohl der Kommentar "for development" sagt. Live geprüft von homelab-58: Ein Preflight mit `Origin: https://evil.example` auf `https://budgetbuddy.elver-algol.ts.net/api/ISettingsApi/getSettings` lieferte 204 und `access-control-allow-origin: *`.

Entscheidung Roman (2026-10-28): Die CORS-Policy entfällt ganz, auch in Development. Der Vite-Dev-Server leitet `/api` per Proxy an das Backend weiter (`vite.config`, `server.proxy`), der Browser sieht dort also nur eine Origin; die PWA im Betrieb wird vom Server selbst ausgeliefert.

## Acceptance criteria
- [ ] `src/Server/Program.fs` ruft weder `AddCors` noch `UseCors` auf; die Policy "AllowAll" ist entfernt (Quelltext-Test nach dem Muster der bestehenden Quelltext-Zusicherungen).
- [ ] Eine Anfrage an eine `/api/`-Route mit fremdem `Origin`-Kopf bekommt keinen `Access-Control-Allow-Origin`-Kopf zurück (Test gegen die echte Middleware-Kette, z. B. über einen im Test gestarteten Host; falls das im Projekt nicht ohne neue Abhängigkeit geht, im Outcome begründen und den Quelltext-Test als Nachweis nennen).
- [ ] Die Entwicklung funktioniert weiter: `npm run test:scripts` (Client gegen Backend über den Proxy bzw. den statischen Server) bleibt grün.
- [ ] Die infrastructure-README hält fest, dass BB kein CORS anbietet und warum (keine Anmeldung, Same-Origin über Vite-Proxy und Auslieferung durch den Server).
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Herkunft: Security-Review der Homelab-Sitzung (homelab-58) am 2026-10-28, erfasst per quick-capture, verfeinert mit Roman am 2026-10-28. Befund M4, erster in der Reihenfolge M4, M2, M1, M3, N1.
Nach dem Deploy prüft Roman oder die BB-Sitzung live: `curl -si -X OPTIONS -H 'Origin: https://evil.example' -H 'Access-Control-Request-Method: POST' https://budgetbuddy.elver-algol.ts.net/api/ISettingsApi/getSettings` darf keinen `access-control-allow-origin`-Kopf mehr zeigen.

## Outcome

`AddCors` (Policy "AllowAll" mit AllowAnyOrigin/Method/Header) und `app.UseCors("AllowAll")` sind aus `src/Server/Program.fs` entfernt, in allen Umgebungen. Die Annahme der Entscheidung ist geprüft: `vite.config.js` hat `server.proxy` für `/api` auf das Backend (Port aus BACKEND_PORT, Standard 5081), Development ist also Same-Origin.

Neue Datei `src/Tests/CorsTests.fs` (direkt nach `RoutingTests.fs` in `Tests.fsproj`) mit 3 Tests: Program.fs enthält weder AddCors, UseCors, AllowAnyOrigin noch AllowAll (kommentarbereinigt); kein Server-Quelltext erwähnt Cors; `vite.config.js` enthält den `/api`-Proxy. Die ersten beiden waren vor der Entfernung rot (2 Fehlschläge), danach grün.

Zum Kriterium "Test gegen die echte Middleware-Kette": nicht umgesetzt. Program.fs ist eine EntryPoint-Funktion, die den Host selbst baut und startet (DB-Init, Port aus Umgebung), ein Test-Host bräuchte Umbau und eine neue Abhängigkeit (Microsoft.AspNetCore.TestHost). Beides ist für diese Änderung zu groß, deshalb gilt die Quelltext-Zusicherung als Nachweis, wie vom Task für diesen Fall vorgesehen. Die Live-Prüfung per curl (Notes der Task-Datei) bleibt nach dem Deploy bei Roman.

Läufe im Worktree: dotnet build 0 Warnungen, 0 Fehler; dotnet test 2472 bestanden, 0 fehlgeschlagen, 7 übersprungen (2479 gesamt); npm run build ok; guard:classes ok; guard:tokens ok; npm run test:scripts 281 von 281 bestanden.
