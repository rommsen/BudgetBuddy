---
id: design-system-m4kp7
title: "Präsentationale Routen ohne Backend aufnehmen: screenshot-route.mjs startet für /styleguide keinen Server"
status: done
type: feature
context: design-system
created: 2026-09-19
completed:
depends_on: [infra-jh7g9]
blocks: []
tags: [test-infra, visuelles-gate, screenshot]
related_adrs: [0030]
related_research: []
prior_art: [design-system-wa6wz, design-system-t82sg]
---

## Why
`scripts/screenshot-route.mjs` startet für **jede** Route ein Backend, auch für Routen, die
gar keine Daten brauchen. Für `/styleguide` ist das reiner Aufwand: die Route ist rein
präsentational, `scripts/export-design-system.mjs` nimmt sie seit Monaten allein mit
`vite preview` auf, ohne Server.

Der Preis ist nicht nur Zeit. Jeder Lauf kopiert die Entwicklungsdatenbank in ein
Temp-Verzeichnis (ADR 0030), startet `dotnet run`, wartet auf den Port und räumt danach auf.
Für eine Seite, die nichts davon liest, ist jeder dieser Schritte eine Gelegenheit zu
scheitern — am 2026-09-19 tatsächlich passiert, als Romans Entwicklungsserver den Port
belegt hielt (`infra-jh7g9`).

`infra-jh7g9` macht den Port konfigurierbar und löst den Konflikt allgemein. Dieser Task
löst ihn für präsentationale Routen **grundsätzlich**: kein Backend, kein Port, keine
Datenbankkopie, nichts aufzuräumen.

## What
Das Skript entscheidet je Route, ob es ein Backend braucht, und startet nur dann eines.

Beim Verfeinern war offen, woher die Entscheidung kommt; festgelegt ist: **die Route sagt
es selbst.** `KNOWN_ROUTES` im Skript spiegelt bereits `Routing.parseUrl` aus
`src/Client/Types.fs` (geprüft durch einen bestehenden Test). Diese Liste bekommt je Route
ein Kennzeichen, ob sie Daten braucht. Eine neue Route ohne Kennzeichen gilt als
datenbedürftig, damit die sichere Annahme die voreingestellte bleibt.

Nicht Teil dieser Aufgabe: die Route-Liste selbst umbauen oder aus dem F#-Code generieren.
Der bestehende Abgleich-Test bleibt, wie er ist.

## Acceptance criteria
- [ ] `npm run screenshot:route -- '#/styleguide'` läuft ohne Backend-Prozess und ohne
      Datenbankkopie durch und erzeugt beide Bilder.
- [ ] Ein Lauf gegen `#/styleguide` funktioniert auch dann, wenn auf dem Backend-Port
      bereits ein Entwicklungsserver läuft. Nachgewiesen durch einen tatsächlichen Lauf mit
      belegtem Port, nicht durch Lesen des Codes.
- [ ] Eine datenbedürftige Route (`#/ledger`) startet weiterhin ein Backend gegen eine
      isolierte Temp-Kopie; `assertDataDirIsolated` bleibt wirksam (ADR 0030).
- [ ] Eine Route, die in `KNOWN_ROUTES` kein Kennzeichen trägt, wird als datenbedürftig
      behandelt, nicht als präsentational. Durch einen Test belegt.
- [ ] `node --test scripts/screenshot-route.test.mjs` ist grün, einschließlich des
      bestehenden Abgleichs zwischen `KNOWN_ROUTES` und `Routing.parseUrl`.

## Notes
Abgespalten von `infra-jh7g9` am 2026-09-19 (Entscheidung Roman: beide Wege bauen, als zwei
Tasks). `infra-jh7g9` heilt die Ursache (fest verdrahteter Port), dieser Task nimmt
präsentationalen Routen den Bedarf überhaupt.

**Fasst `scripts/screenshot-route.mjs` an.** Dieselbe Datei bearbeiten auch
`design-system-yxrxb` (Wartepredikat), `ledger-p9k3w` (Sheet-Artefakt) und `infra-jh7g9`
(Port). Deshalb `depends_on: [infra-jh7g9]` — die beiden greifen in `startBackend` und
`main` an derselben Stelle ineinander und sollen nicht gleichzeitig laufen.

## Outcome
`scripts/screenshot-route.mjs` entscheidet jetzt je Route, ob sie ein Backend braucht, statt für jede Route Server, Datenbankkopie und Aufräumen durchzuziehen.

**Was geändert wurde:**
- Neu: `export const ROUTES_WITHOUT_BACKEND = new Set(["/styleguide"])` und `export function routeNeedsBackend(route)` (beide direkt unter `KNOWN_ROUTES`). Bewusst eine eigene, kleine Liste statt eines Felds direkt in `KNOWN_ROUTES`: Letzteres bleibt unverändert ein `Set<string>`, gegen das der bestehende Abgleichs-Test (`KNOWN_ROUTES spiegelt Routing.parseUrl ...`) unverändert weiterprüft — genau wie in den Task-Notes verlangt ("der bestehende Abgleich-Test bleibt, wie er ist"). Eine Route ohne Eintrag in `ROUTES_WITHOUT_BACKEND` gilt als datenbedürftig (sichere Voreinstellung, AC4).
- `main()`: der komplette DB-Kopie-/`startBackend()`-Block läuft nur noch, wenn `routeNeedsBackend(normalizedRoute)` wahr ist; sonst nur ein Log-Hinweis. `startBackend()` selbst (inkl. `assertDataDirIsolated`) blieb unangetastet.
- `startFrontend()`: `backendPort` ist für präsentationale Routen `undefined`; `BACKEND_PORT` wird dann gar nicht erst gesetzt, `vite.config.js` fällt auf seine eigene Vorgabe (5081) zurück, die ohnehin nie kontaktiert wird.
- Kopfkommentar aktualisiert (der alte Satz "braucht das hier den echten Server" für "die meisten Routen" war irreführend geworden, seit auch dieses Skript für /styleguide keinen mehr startet).

**Getestet:**
- `node --test scripts/screenshot-route.test.mjs`: 23/23 grün, inkl. der zwei neuen Tests für AC4 (`routeNeedsBackend: eine Route ohne Kennzeichen ... gilt als datenbedürftig`, geprüft an `/ledger` und einer unbekannten Route; `routeNeedsBackend: /styleguide ist als einzige Route ... gekennzeichnet`) und des unveränderten Abgleichs-Tests gegen `Routing.parseUrl`.
- AC1 (tatsächlicher Lauf): `npm run screenshot:route -- '#/styleguide'` — Log zeigt "ist präsentational: kein Backend, keine Datenbankkopie", beide PNGs entstehen, kein `dotnet`-Prozess, kein `bb-screenshot-route-*`-Temp-Verzeichnis.
- AC2 (tatsächlicher Lauf mit belegtem Port, nicht nur Code gelesen): ein echter zweiter BudgetBuddy-Backend-Prozess wurde gegen ein isoliertes Scratch-`DATA_DIR` auf Port 5081 gestartet (derselbe Port, den Romans Entwicklungsserver laut `## Why` blockierte); derselbe `#/styleguide`-Lauf lief währenddessen unverändert erfolgreich durch, weil dieses Skript für die Route gar nicht erst versucht, einen eigenen Server zu binden. Danach beendet, Scratch-Verzeichnis entfernt.
- AC3 (tatsächlicher Lauf): `npm run screenshot:route -- '#/ledger'` — Log zeigt weiterhin "Entwicklungsdatenbank nach .../bb-screenshot-route-... kopiert" und "Server startet gegen .../bb-screenshot-route-...", beide PNGs entstehen, Temp-Verzeichnis danach wieder weg. `assertDataDirIsolated` bleibt der unveränderte Wachtposten in `startBackend()`.
- AC5: `node --test scripts/screenshot-route.test.mjs` grün (s. o.).
- `dotnet test` nicht erneut gelaufen: kein Code unter `src/` geändert (nur `scripts/*.mjs`).

**Für die nächste Aufgabe (`ledger-p9k3w`):** Die Änderung bleibt auf `ROUTES_WITHOUT_BACKEND`/`routeNeedsBackend`, den `if (needsBackend)`-Block in `main()` und die `backendPort`-Optionalität in `startFrontend()` begrenzt; der Rest von `startBackend()` und `main()` ist unangetastet.

Geänderte Dateien:
- `scripts/screenshot-route.mjs`
- `scripts/screenshot-route.test.mjs`
