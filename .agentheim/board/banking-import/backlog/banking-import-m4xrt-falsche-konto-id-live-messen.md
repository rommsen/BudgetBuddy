---
id: banking-import-m4xrt
title: "Live messen, wie Comdirect auf eine falsche Konto-Id antwortet"
status: backlog
type: spike
context: banking-import
created: 2026-11-01
completed:
depends_on: []
blocks: []
tags: [comdirect, session, owner-run, live]
related_adrs: [0071]
related_research: []
prior_art: [banking-import-v8n3k]
---

## Why
Der Verbindungstest hängt seit `banking-import-v8n3k` an der frei editierbaren Konto-Id. Antwortet Comdirect auf eine fremde Id mit 401 oder 403, folgt nach dem Refresh `ReloginRequired`: Die lebende Verbindung endet, der gespeicherte Wert ist weg, der nächste Klick kostet eine TAN. Ob die Bank so antwortet, ist nicht gemessen.

Stop-Loss: Ist die Abhilfe während des Spikes schon bekannt und billig, wird sie festgehalten und der Spike endet.

## What
In der Beobachtungssonde einmal mit falscher Konto-Id abrufen und den Status festhalten.

## Acceptance criteria
- [ ] Das Outcome nennt den gemessenen Status und ob die Sitzung danach lebt.
- [ ] Bei 401 oder 403 liegt eine Entscheidung vor, ob eine Ablehnung direkt nach erfolgreichem Refresh ein Verlust ist (Nachtrag zu ADR 0071 Punkt 3), sonst ist der offene Punkt aus der README entfernt.

## Notes
Owner-Task, braucht eine echte Verbindung. Nicht per `work` starten.
