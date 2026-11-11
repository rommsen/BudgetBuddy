---
id: ledger-f8d7x
title: "Kassenbuch ordnen: am Handy Filter-Knopf mit Sheet und Chips statt zwei wischender Leisten, am PC Seitenleiste mit Konten, Seitenpanel und Historie wie im Budget"
status: backlog
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0086,0087]
related_research: []
prior_art: []
---

## Why
Das Kassenbuch ist am Handy mit zwei wischenden Leisten überladen, die Kontowahl uneinheitlich, das Seitenpanel ungeordnet (ADR 0086, „ungeführt“).

## What
Handy: Kontowahl als Auswahlfeld mit Saldo, Suche, Filter-Knopf mit Sheet, gesetzte Filter als Chips (Styleguide §7.5). PC: Seitenpanel geordnet, Historie in der Form von §7.11, Mehrfachauswahl mit Zusammenfassung im Panel (§7.7), Rückmeldung über der Auswahl-Leiste (§7.13).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Groß: beim Verfeinern nach Handy und PC teilen (Roman, 2026-11-11). Nimmt auf: ledger-ayjzq (Sicht „Für mich“). Die Seitenleiste mit Konten kommt mit design-system-k6kfw; bleibt dort eine zweite Kontowahl im Kassenbuch stehen, wird sie hier entfernt.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
