---
id: ledger-g47hp
title: "Benachrichtigung für Lena bei neuer Rückfrage"
status: backlog
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [ledger-xfjcp]
blocks: []
tags: [backend, kassenbuch, flags, lena, benachrichtigung]
related_adrs: [0056]
related_research: []
prior_art: [ledger-xfjcp]
---
Ziel-Lage: backlog. Offen: Kanal (Web-Push hinter Tailscale, Nachricht über einen Dienst,
nur Zähler in der Sicht "Für mich"), Zeitpunkt (beim Zuweisen oder gebündelt), und ob
Lena einen Browser-Zugang mit Push-Erlaubnis hat. Braucht vermutlich eine Architekt-Frage
(Push ohne öffentlichen Endpunkt) und hängt praktisch an `ledger-ayjzq`.

## Why
Aus `ledger-xfjcp`, Punkt 2: Lena soll von einer Rückfrage erfahren, ohne YNAB zu öffnen.

## What
Zu verfeinern.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Kein Bau vor der Entscheidung zu `ledger-ayjzq`.
