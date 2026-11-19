---
id: ledger-b7k2q
title: "Bob: kein Flag nicht belegt, Grenzen am Commit und am Rumpf lückenhaft"
status: backlog
type: refactor
context: ledger
created: 2026-11-19
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G1, von Roman zur Task gewählt. Sicherheitsgrenze nach ADR 0068 und CLAUDE.md („Bob-Buchungen bekommen kein Flag“): höchste Schwere hoch.

## What
Alle Befunde der Gruppe in Befundform, wörtlich aus dem Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`:

### G1 Bob: kein Flag nicht belegt, Grenzen am Commit und am Rumpf lückenhaft

**Kern:** Die Sicherheitsgrenzen der Bot-Schnittstelle (ADR 0068, CLAUDE.md) sind nicht vollständig belegt oder geprüft: kein Test auf fehlendes Flag, das Datumsfenster gilt beim Commit nicht neu, Rumpf und Kategorie haben keine Obergrenze.

**Höchste Schwere:** hoch · **Befunde:** 4 · **BCs:** ledger

- src/Tests/LedgerCashBookingTests.fs:642 | CLAUDE.md Security-Fallen („Bob-Buchungen bekommen kein Flag“), test-doctrine.md Punkt 13, ADR 0068 | hoch | Im Test „Bobs Commit pusht den Request der Barbuchung …“ prüfen, dass der gesendete Rumpf (`handler.Bodies`) kein `flag_color` trägt; heute bliebe er grün, wenn `botPushOn` (BotApi.fs:547) die Lena-Farbe an `createTransactionOn` gäbe.
- src/Server/BotApi.fs:444 | ADR 0068 Punkt 3 (Datumsfenster heute minus 31 Tage) | niedrig | Commit prüft das Datumsfenster nicht neu; Vorschau 23:59, Commit nach Mitternacht bucht 32 Tage zurück. `checkBotBookingDate` mit dem Berliner Tag des Commits in `bookWithinLimit` wiederholen.
- src/Server/BotApi.fs:296 | ADR 0066 Punkt 5, ADR 0068 (enge Grenzen der Bot-Schnittstelle) | niedrig | `readBody` ohne eigene Obergrenze (Kestrel 30 MB), `category` ohne Längengrenze (Validation.fs:478) und in der 422-Meldung zurückgegeben; Rumpf (~4 KB) und `category` (~200 Zeichen) begrenzen.
- src/Tests/BotApiTests.fs:773 | test-doctrine.md Punkt 2 | niedrig | Datumsfenster steht schon in BotDomainTests.fs:38, 42; auf API-Ebene reicht ein Fall außerhalb mit 400 und Feld `date`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-19, raw, unrefined. Bewusst groß; kleiner geschnitten wird im Refinement (`agentheim:modeling`).
