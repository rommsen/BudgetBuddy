---
id: ledger-wj6pd
title: Playwright-Wächter ledger-status-column misst Zeilenhöhen zeitweise als 0 px und wird ohne Codeänderung sporadisch rot
status: backlog
type: bug
context: ledger
created: 2026-10-27
completed:
depends_on: []
blocks: []
tags: [test-scripts, playwright, flaky]
related_adrs: []
related_research: []
prior_art: [ledger-vqvq6]
---

## Why
Im work-Lauf ledger-rjhwp (2026-10-27) war `scripts/ledger-status-column.test.mjs` im seriellen Gesamtlauf des Workers einmal rot und beim Verifier in zwei von drei Läufen (87/88 und 86/88), jeweils ein anderer Zeilenhöhen-Test. Gemessen wurde eine Zeile mit 0 px (einmal die Vergleichszeile, einmal die geprüfte Zeile). Einzeln und in kleinen Teilmengen lief die Datei grün (9/9, 12/12, 23/23). Die Änderung von ledger-rjhwp hat an dieser Datei nur `ExternalLinks: []` in der Fixture ergänzt. Jeder Fehlalarm kostet bei der heutigen Suite einen weiteren Lauf von 15 bis 20 Minuten.

## What
Die Höhenmessung wartet auf einen stabilen, gerenderten Zustand der gemessenen Zeilen, bevor sie misst (Zeile sichtbar und mit Höhe größer 0), statt sofort nach dem Laden zu messen. Ähnlich gelagert: ledger-vqvq6 (Wächter ledger-split-cashback wartet auf die Settings-Antwort statt fest 400 ms).

## Acceptance criteria
- [ ] To be defined during refinement. Richtwert: `scripts/ledger-status-column.test.mjs` zehnmal hintereinander im seriellen Gesamtlauf-Kontext grün, ohne festes Warten per Zeitkonstante.

## Notes
Erfasst am 2026-10-27 aus dem Verifier-Bericht zu ledger-rjhwp.
