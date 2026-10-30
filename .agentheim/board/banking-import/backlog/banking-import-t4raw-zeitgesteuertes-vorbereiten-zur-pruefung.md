---
id: banking-import-t4raw
title: "Zeitgesteuertes Vorbereiten zur Prüfung, per Setting an und aus, Default aus"
status: backlog
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-bftq0, banking-import-cyg94]
blocks: []
tags: [backend, frontend, sync, zeitplan]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Roman will Umsätze zur Prüfung bereitliegen haben, ohne einen Sync anzustoßen. Der Lauf bucht nichts
und lässt eine offene Sitzung unberührt (ADR 0071 Punkt 7).

## What
- Setting an/aus und Intervall, Default aus. Validierung in `src/Server/Validation.fs`.
- `tick` löst `fetchForReview` aus.
- `tryInstallPrepared` in `SyncSessionManager`, atomar.
- UI-Felder in den Einstellungen, `StartedAt` als "vorbereitet am" in der Prüfung.
- Neue Pflichtfelder in `SyncSettings` brechen Fixtures in `scripts/*.test.mjs`: im selben Schritt nachziehen.

## Acceptance criteria
- [ ] GIVEN aus WHEN tick THEN kein Bankzugriff.
- [ ] GIVEN an, Intervall fällig, `Connected`, keine Sitzung THEN eine Sitzung mit `ReviewingTransactions`.
- [ ] GIVEN eine Sitzung mit Status außer `Completed` oder `Failed` THEN sie bleibt unberührt, kein Abruf.
- [ ] GIVEN ein manueller Start während des Laufs THEN das Ergebnis des Laufs wird verworfen, die manuelle Sitzung bleibt.
- [ ] GIVEN `Disconnected` THEN keine Challenge, kein Abruf.
- [ ] GIVEN alle Buchungen übersprungen THEN keine Sitzung.
- [ ] GIVEN der YNAB-Abgleich ist nicht möglich THEN keine Sitzung, Log-Eintrag.
- [ ] GIVEN der Lauf ist fertig THEN kein Ereignis im Kassenbuch und kein Aufruf an YNAB außer Lesen.
- [ ] Die Intervall-Grenzen sind validiert, `src/Tests/RemotingEndpointNegativeTests.fs` hat die zugehörige Zeile.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Einstellungen sind auf iPhone und PC geprüft. [human-eye]

## Notes
Offene Frage an Roman: Passt ein Intervall von 30 bis 1440 Minuten? Bei ein bis zwei Läufen am Tag
kann die Untergrenze höher liegen und entlastet Comdirect und YNAB.

Eine liegen gebliebene Prüfung hält den Zeitplan an, bis Roman sie abschließt oder abbricht.
Entfällt oder wird neu bewertet, wenn `banking-import-p5mcp` eine Maximaldauer unter dem Intervall zeigt.

Design-System: nichts Neues. Promotion erst nach Go in `banking-import-p5mcp`.
