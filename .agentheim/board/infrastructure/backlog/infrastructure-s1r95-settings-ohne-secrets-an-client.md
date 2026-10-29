---
id: infrastructure-s1r95
title: "getSettings liefert YNAB-Token, Comdirect-Secret und Passwort im Klartext an den Client"
status: backlog
type: bug
context: infrastructure
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`src/Server/Api.fs:145-186` gibt alle Secrets zurück; `/api` hat keine eigene Anmeldung, Schutz nur über Tailscale/Caddy (Audit D, geprüft).

## What
Der Client bekommt nur, ob ein Secret gesetzt ist (und ggf. eine Maske). Speichern ohne erneute Eingabe behält den alten Wert.

## Acceptance criteria
- [ ] `getSettings` enthält kein Token, Secret oder Passwort im Klartext; Test rot vor dem Fix.
- [ ] Die Settings-Seite zeigt gesetzte Secrets als gesetzt und speichert andere Felder, ohne Secrets zu löschen.
- [ ] Ein Test deckt Speichern ohne Secret-Eingabe ab.

## Notes
Migrationsschritt P7. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Berührt Shared-Typen und Client; im Refine den kleinsten Schnitt festlegen.
