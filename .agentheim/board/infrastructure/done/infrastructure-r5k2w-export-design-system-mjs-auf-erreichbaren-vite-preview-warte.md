---
id: infrastructure-r5k2w
title: "export-design-system.mjs: auf erreichbaren vite preview warten statt fester 5 Sekunden"
status: done
type: bug
context: infrastructure
created: 2026-10-29
depends_on: []
blocks: []
tags: [harness, flake]
related_adrs: []
related_research: [harness-audit]
prior_art: [infrastructure-dvy29]
---

## Why
`startPreviewServer` in `scripts/export-design-system.mjs` wartet auf die Zeile "Local:" von `vite preview` höchstens 5 Sekunden und macht danach ohne Prüfung weiter. Unter Last (mehrere gleichzeitige Gate-Läufe) ist der Server dann noch nicht erreichbar, `export-design-system.test.mjs` scheitert mit `ERR_CONNECTION_REFUSED`. Ein Fehler-Exit von vite nach Ablauf der 5 Sekunden wird zudem nicht mehr beachtet. verify.sh umgeht das mit einem Lock um test:scripts (infrastructure-dvy29); der Fehler selbst besteht weiter.

## What
Wie in `scripts/screenshot-route.mjs` per echtem TCP-Connect (waitForPort) auf den Port warten, mit großzügigem Timeout, und laut scheitern, wenn der Server nicht kommt oder vorher endet.

## Acceptance criteria
- [ ] Der Export wartet, bis der Preview-Port einen TCP-Connect annimmt, und wirft nach Ablauf des Timeouts einen sprechenden Fehler.
- [ ] Ein vorzeitiges Ende von vite preview führt zu einem Fehler statt zu einem späteren Verbindungsfehler.
- [ ] Regressionstest, der ohne den Fix rot ist (z.B. verzögert startender Server).
## Outcome

`scripts/export-design-system.mjs` hat jetzt `startServerProcess({ command, args, env, port, timeoutMs, whatFor })`. Es startet den Prozess und wartet per TCP-Connect auf 127.0.0.1:port. Die Log-Zeile "Local:" spielt keine Rolle mehr. Nach `timeoutMs` wirft es "<whatFor> antwortet nach <n>ms nicht auf Port <p>.". Endet der Prozess vor der Bereitschaft, wirft es sofort "<whatFor> beendete sich vor der Bereitschaft (Code …, Signal …)", auch bei Code 0, den der alte Code ignoriert hat. Die gesammelte stdout/stderr-Ausgabe hängt an der Meldung, und im Fehlerfall wird der Prozess beendet. `startPreviewServer` nutzt das mit `PREVIEW_START_TIMEOUT_MS = 60000`. `stopServer` und `getFreePort` sind zusätzlich exportiert, damit der Test sie nutzen kann.

Helfer: Der TCP-Check (`canConnect`) ist eine Kopie der Logik von `waitForPort` aus `scripts/screenshot-route.mjs`. Es gibt kein gemeinsames Modul, und `screenshot-route.mjs` ist unverändert. Ich habe dupliziert, weil die Warteschleife hier zusätzlich das Ende des Kindprozesses beobachten muss.

Regressionstests in `scripts/export-design-system.test.mjs`, Abschnitt "Bereitschaft des Preview-Servers". Ein Kommentar nennt den verhinderten Fehler (ERR_CONNECTION_REFUSED unter paralleler Last). Ein Node-Ersatzprozess loggt sofort "Local:" und
- öffnet den Port nach 1,5 s: der Port muss nach dem Auflösen erreichbar sein,
- öffnet ihn nie: Fehler nach dem Timeout,
- endet mit Code 1 bzw. 0: Fehler vor dem Timeout.

Mit der alten Logik waren alle vier rot, und zwar an der Assertion: "Port muss nach dem Auflösen erreichbar sein" bzw. "Start hätte scheitern müssen, löste aber auf". Mit dem Fix sind sie grün.

Gate: `./verify.sh` meldet `VERIFY: PASS (dotnet 2586 grün, node 310 grün, ...)`, vorher waren es 306 node-Tests. verify.sh ist unverändert.

Zum Lock in verify.sh: Er sollte vorerst bleiben. Ich habe `npm run test:scripts -- scripts/export-design-system.test.mjs` zweimal parallel im selben Worktree gestartet. Ein Lauf war grün, einschließlich des vollen Exports unter Last. Der andere scheiterte schon im Client-Build von `run-guards.mjs`, mit einem anderen Fehler: Fable meldet, dass `src/Client/obj/Release/net10.0/Client.fsproj.vite-plugin-design-time` von einem anderen Prozess belegt ist. Parallele Builds im selben Worktree stoßen also weiterhin zusammen, unabhängig von diesem Fix. Ob der worktree-übergreifende Lock (`<git-common-dir>/bb-verify-test-scripts.lock`) wegfallen kann, ist mit einem einzelnen Lauf nicht belegt. Dafür braucht es mehrere parallele verify.sh-Läufe in getrennten Worktrees.

**Nachtrag Conductor:** Der Verifier meldete PASS, sein Gate-Lauf zeigte aber die Zahlen des M6-Worktrees (dotnet 2600, node 306), lief also vermutlich im falschen Verzeichnis. Der Conductor hat `./verify.sh` daraufhin selbst im Worktree von r5k2w ausgeführt: `VERIFY: PASS (dotnet 2586 grün, node 310 grün, ...)`, Exit 0. Ein vom Worker angelegter Eintrag in `diary/development.md` wurde verworfen (Diary eingefroren seit infrastructure-kq13v).
