---
id: design-system-yxrxb
title: "Wartepredikat von screenshot-route.mjs blockiert die /styleguide-Route, deren Demo-Inhalte dauerhaft Spinner und Skeletons zeigen"
status: done
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

## Outcome

`scripts/screenshot-route.mjs` wartete bisher darauf, dass `.proto-spinner` UND
`.animate-pulse` aus dem DOM verschwinden. Das scheiterte laut an der `/styleguide`-Route:
deren Loading-Sektion zeigt Spinner und Skeletons als Demo-Inhalte der lebenden
Komponenten-Galerie, und die verschwinden naturgemäß nie. Eine Klassen-Heuristik konnte
nicht unterscheiden zwischen "die Seite lädt noch" und "die Seite zeigt absichtlich einen
Ladezustand".

**Die App sagt jetzt selbst, wann sie fertig ist (Romans Entscheidung, s. Task `## What`).**
`State.isPageReady` (`src/Client/State.fs`) ist eine reine Funktion des Elmish-Modells:
für `Styleguide` (kein eigener Ladezustand) immer `true`; für jede andere Seite `true`,
sobald keiner ihrer von `pageLoadCmd` geladenen `RemoteData`-Datenströme mehr `Loading` ist
(bei `SyncFlow` sowohl `CurrentSession` als auch das ggf. anschließend nachgeladene
`SyncTransactions`; bei `Ledger` `Accounts`, `Balances` und `Entries`; bei `Rules` `Rules`;
bei `Settings` `Settings`, bewusst ohne das nutzergetriggerte `YnabBudgets`; bei `QuickAdd`
`QuickAddTemplates`). `NotAsked` zählt als bereit, nicht nur `Success`/`Failure`: eine
Seite, die noch gar nichts angefordert hat, blockiert das Signal nicht.

`src/Client/View.fs` rendert das Ergebnis als unsichtbares, immer vorhandenes Element mit
dem Attribut `data-app-ready="true"|"false"`, als eigenständiges Element statt als Prop auf
`Navigation.appWrapper`, um dessen feste Prop-Liste nicht anzutasten.

`scripts/screenshot-route.mjs` (`waitForPageLoaded`) wartet jetzt auf
`page.waitForSelector('[data-app-ready="true"]', { state: "attached" })` statt auf die
beiden CSS-Klassen; die Begründung des gewählten Wegs steht an beiden Setz-Stellen im
Kommentar (`State.fs` bei `isPageReady`, `View.fs` beim Marker-Element,
`screenshot-route.mjs` bei `waitForPageLoaded`). Der Timeout- und Fehlerpfad (Route im
Fehlertext bei einer tatsächlich nicht ladenden Route) ist unverändert.

**Tatsächlich verifiziert, nicht nur gelesen (AC1/AC2):** `node scripts/screenshot-route.mjs
'#/styleguide'` läuft jetzt ohne Timeout durch und legt beide Bilder ab. `node
scripts/screenshot-route.mjs '#/ledger'` ebenso; das aufgenommene Bild (1440×900) zeigt
Kontostände-Tabelle und Buchungsliste vollständig gefüllt mit echten Daten, keine
Skeleton-Platzhalter.

**Tests:** `src/Tests/AppReadinessTests.fs` (14 Expecto-Fälle, nach dem Muster von
`QuickAddTests.modelWithForm` ohne `State.init`) deckt `isPageReady` für alle sechs Seiten
ab, inklusive des Kettenfalls bei `SyncFlow` (`CurrentSession` fertig, `SyncTransactions`
noch am Laden) und des Failure-Falls beim Kassenbuch. `scripts/screenshot-route.test.mjs`
hat einen neuen Regressionstest, der die tatsächliche Quelldatei liest und sicherstellt,
dass `querySelectorAll(".proto-spinner"|".animate-pulse")` nicht wieder eingeführt wird
und `data-app-ready` tatsächlich abgefragt wird.

`dotnet test` (888/888 grün, 7 übersprungen wie zuvor), `node --test
scripts/screenshot-route.test.mjs` (19/19 grün) und `npm run build` sind sauber.

Kein neuer ADR: die Entscheidung (App-eigenes Signal statt Container-Scoping oder
Routen-Opt-out) ist bereits vollständig im `## What`-Abschnitt dieser Task-Datei
festgehalten, inklusive der beiden verworfenen Wege und ihrer Begründung.
