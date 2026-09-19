---
id: design-system-yxrxb
title: "Wartepredikat von screenshot-route.mjs blockiert die /styleguide-Route, deren Demo-Inhalte dauerhaft Spinner und Skeletons zeigen"
status: backlog
type: bug
context: design-system
created: 2026-09-19
completed:
depends_on: []
blocks: []
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
Das Wartepredikat differenzieren, statt es global zu lockern. Beim Verfeinern zu
entscheiden, welcher Weg trägt:

- **Auf einen Bereich beschränken:** nur innerhalb des Inhalts-Containers warten, nicht
  über das ganze Dokument. Scheitert, wenn die Demo-Elemente im selben Container liegen.
- **Ein Opt-out je Route:** ein Flag wie `--no-wait-loading` oder eine kleine Liste von
  Routen, die absichtlich Ladezustände zeigen. Einfach, aber wieder eine Pflegeliste,
  genau die Sorte, die in `design-system-bq4tz` zweimal veraltet ist.
- **Auf ein Bereitschaftssignal warten statt auf das Verschwinden von Platzhaltern:** ein
  Marker, den die App setzt, wenn die Seite fertig gerendert ist. Am robustesten, verlangt
  aber eine Änderung an der App.

Die dritte Variante löst zugleich ein verwandtes Problem: Das aktuelle Predikat ist eine
Heuristik über CSS-Klassen und bricht, sobald eine Komponente ihre Klassennamen ändert.

## Acceptance criteria
- [ ] `npm run screenshot:route -- '#/styleguide'` erzeugt beide Bilder, statt nach 30 s
      abzubrechen.
- [ ] Das Kassenbuch bleibt korrekt: eine Aufnahme von `#/ledger` wartet weiterhin, bis
      Tabelle und Liste wirklich gefüllt sind, und liefert kein Bild mit Platzhaltern.
- [ ] Eine Route, die tatsächlich nicht lädt, scheitert weiterhin laut mit der Route im
      Fehlertext (Akzeptanzkriterium aus `design-system-wa6wz` bleibt erfüllt).
- [ ] Der gewählte Weg ist im Skript kurz begründet.
- [ ] `node --test scripts/screenshot-route.test.mjs` ist grün.

## Notes
Gefunden am 2026-09-19 beim Versuch, das offene Augen-Kriterium von
`design-system-bq4tz` (`/styleguide`-Route unverändert?) mit dem neuen Werkzeug
abzuschliessen. Das Werkzeug hat sich dabei korrekt verhalten: es hat laut abgebrochen und
die Route genannt, statt ein Bild voller Platzhalter abzulegen.

Verwandt: `ledger-p9k3w` (BottomSheet-Leiste in Full-Page-Aufnahmen). Beide betreffen
`scripts/screenshot-route.mjs` und könnten zusammen angegangen werden.
