---
id: design-system-m4kp7
title: "Präsentationale Routen ohne Backend aufnehmen: screenshot-route.mjs startet für /styleguide keinen Server"
status: todo
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
