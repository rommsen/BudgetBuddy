---
id: design-system-yxrxb
title: "Wartepredikat von screenshot-route.mjs blockiert die /styleguide-Route, deren Demo-Inhalte dauerhaft Spinner und Skeletons zeigen"
status: backlog
type: bug
context: design-system
created: 2026-09-19
completed:
depends_on: []
blocks: [ledger-p9k3w]
tags: [test-infra, visuelles-gate, screenshot]
related_adrs: []
related_research: []
prior_art: [design-system-wa6wz]
---

## Why
`npm run screenshot:route -- '#/styleguide'` bricht ab mit:

```
FEHLER: Aufnahme für Route '/styleguide' fehlgeschlagen: Route '/styleguide' lädt nicht
(Spinner oder Skeleton-Platzhalter hängen fest): page.waitForFunction: Timeout 30000ms exceeded.
```

Die Route lädt einwandfrei. Sie zeigt Spinner und Skeletons als **Demo-Inhalte** der
Loading-Sektion, und die verschwinden naturgemäss nie. `waitForPageLoaded` wartet aber
darauf, dass `.proto-spinner` **und** `.animate-pulse` aus dem Dokument verschwunden sind,
und kann nicht unterscheiden zwischen "Seite lädt noch" und "Seite zeigt absichtlich einen
Ladezustand".

Das ist die Kehrseite einer berechtigten Verschärfung: In Iteration 1 von
`design-system-wa6wz` wartete das Skript nur auf `.proto-spinner`, was für das Kassenbuch
zu wenig war (dessen Ladezustand ist ein Skeleton, kein Spinner). Der Verifier hat deshalb
`.animate-pulse` verlangt. Damit stimmt es fürs Kassenbuch und blockiert den Styleguide.

**Warum das mehr als ein Schönheitsfehler ist:** Die `/styleguide`-Route ist die lebende
Galerie der Design-System-Komponenten und damit die Seite, für die ein automatischer
visueller Abgleich am meisten wert wäre. Ausgerechnet sie lässt sich mit dem Werkzeug
nicht aufnehmen. Roman hat sie am 2026-09-19 von Hand geprüft (Befund: passt), aber von
Hand ist genau das, was das Werkzeug ablösen sollte.

## What
**Entschieden (Roman, 2026-09-19): die App sagt selbst, wann sie fertig ist.** Statt aus
dem Verschwinden von CSS-Klassen zu raten, setzt der Client ein Bereitschaftssignal, und
das Skript wartet darauf.

Die beiden verworfenen Wege und warum: *nur im Inhalts-Container warten* scheitert
voraussichtlich genau bei `/styleguide`, weil die Demo-Spinner im selben Container liegen;
*ein Opt-out je Route* wäre eine Pflegeliste, und genau die Sorte ist in
`design-system-bq4tz` zweimal veraltet, bevor jemand es merkte.

**Was "fertig" heißt, ist der Kern der Aufgabe.** Ein Signal, das nur das Mounten meldet,
wäre ein Rückschritt: es käme, bevor die Kassenbuch-Tabelle ihre Daten hat, und das
Werkzeug nähme wieder Platzhalter auf. Das Signal muss den Ladezustand der Seite tragen,
nicht ihr Vorhandensein. Die Seiten halten ihn bereits als `RemoteData` im Elmish-Modell;
die App mountet über `Program.withReactSynchronous "root"` (`src/Client/App.fs`), das
Wurzel-Element steht in `src/Client/index.html`.

Eine Route ohne eigenen Ladezustand (`/styleguide`) ist demnach sofort bereit. Eine Route
mit Ladezustand ist bereit, wenn keiner ihrer Datenstränge mehr lädt.

## Acceptance criteria
- [ ] `npm run screenshot:route -- '#/styleguide'` erzeugt beide Bilder, statt nach 30 s
      abzubrechen.
- [ ] Das Signal trägt den Ladezustand, nicht das Mounten: eine Aufnahme von `#/ledger`
      wartet, bis Tabelle und Liste gefüllt sind, und zeigt keine Skeleton-Platzhalter.
      Belegt durch eine tatsächliche Aufnahme, nicht durch Lesen des Codes.
- [ ] Eine Route, die tatsächlich nicht lädt, scheitert weiterhin laut mit der Route im
      Fehlertext (Kriterium aus `design-system-wa6wz` bleibt erfüllt).
- [ ] Das Skript prüft nicht mehr auf `.proto-spinner`/`.animate-pulse`; die Heuristik über
      Klassennamen ist ersetzt, nicht ergänzt.
- [ ] Der gewählte Weg ist an der Setz-Stelle im Client und im Skript kurz begründet.
- [ ] `node --test scripts/screenshot-route.test.mjs` und `dotnet test` sind grün,
      `npm run build` läuft sauber.

## Notes
Gefunden am 2026-09-19 beim Versuch, das offene Augen-Kriterium von
`design-system-bq4tz` (`/styleguide`-Route unverändert?) mit dem neuen Werkzeug
abzuschliessen. Das Werkzeug hat sich dabei korrekt verhalten: es hat laut abgebrochen und
die Route genannt, statt ein Bild voller Platzhalter abzulegen.

Verwandt: `ledger-p9k3w` (BottomSheet-Leiste in Full-Page-Aufnahmen). Beide betreffen
`scripts/screenshot-route.mjs` und könnten zusammen angegangen werden.
