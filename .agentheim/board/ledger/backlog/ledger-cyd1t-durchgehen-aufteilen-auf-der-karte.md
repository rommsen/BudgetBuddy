---
id: ledger-cyd1t
title: "Durchgehen: Aufteilen direkt auf der Karte, ohne Umweg über „Details“"
status: backlog
type: feature
context: ledger
created: 2026-11-20
completed:
depends_on: [design-system-001]
blocks: []
tags: [durchgehen, pruefen, split, frontend]
related_adrs: []
related_research: []
prior_art: [ledger-v6s7c, ledger-hme9k, ledger-pafkp, ledger-s8t3s]
---

## Why
Roman (2026-11-20): „Man kann im Prüfen-Feature keine Split-Buchung anlegen.“ Heute geht Aufteilen im Durchgehen nur
über den Link „Details“ und dort den Knopf „Aufteilen“ (ledger-v6s7c, Abweichung (a): „das Detail kann es schon, eine
zweite Aufteilung auf der Karte wäre doppelt“). Roman findet den Weg zu weit; beim Erfassen hat er „Knopf direkt auf
der Karte“ gewählt. Das nimmt Abweichung (a) zurück.

## What
Auf der Karte im Durchgehen gibt es einen direkten Weg zum Split-Editor. Er öffnet denselben Editor wie im Detail
(samt Barabhebung, ledger-s8t3s) und kehrt nach dem Speichern auf die Karte zurück. Eine aufgeteilte Buchung gilt als
Ziel, „Passt“ ist danach frei (wie heute über das Detail).

## Acceptance criteria
- To be defined during refinement.

## Notes
Offen für die Verfeinerung:
- Wo sitzt der Knopf? Heute stehen unter der Karte „Lena fragen“ und „Später“, darüber das Ziel (Kategorie, Übertrag,
  Aufteilung). Möglich: dritter Knopf in der Reihe, oder Eintrag im Kategorie-Feld („Aufteilen“ als Ziel neben
  Kategorie und Übertrag). Braucht eine Skizze zur Freigabe (Ablauf in CLAUDE.md), Touch bei 412 px.
- Tastenkürzel am PC (heute Enter, S, K, L, Esc), etwa A für Aufteilen.
- Sperren: dieselben Gründe wie im Detail (`splitLockTextFor`, Tracking-Konto, Ersatz offen), gesperrter Knopf nennt
  den Grund.
- Ein Sheet zur Zeit (ledger-hme9k): Editor ersetzt den Inhalt, kein zweites Sheet über der Karte.
- Rückweg und Stand der Schlange nach dem Speichern (ledger-nc708, ledger-pz5wv: keine Toasts, letzte Aktion).
