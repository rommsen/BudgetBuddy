---
id: infrastructure-q8f3n
title: "Gate ohne Flakes: Ledger-Replay mit fremden Perf-Buchungen, AC14-Zeittest, Design-System-Export und regeln-panel werden deterministisch"
status: doing
type: bug
context: infrastructure
created: 2026-11-21
completed:
depends_on: []
blocks: []
tags: [gate, flake, tests, verify]
related_adrs: [0100]
related_research: []
prior_art: [infrastructure-x7p2e]
---

## Why
Roman (2026-11-21): nach dem Work-Lauf in der Nacht vom 20. auf den 21.11. („Hat wieder die ganze Nacht gedauert. Warum?“)
soll das Gate ohne Flakes laufen. In dieser Nacht war etwa die Hälfte von rund 25 Gate-Läufen nur an Flakes rot; jeder
rote Lauf kostet einen weiteren (6 bis 8 min). Der häufigste Flake (Layout-Zustand `targets-pruefen-nicht-neu-gelesen`)
ist schon behoben (118819ea, ein Wettlauf zwischen Test und App). Übrig sind die vier unten, bisher Zeile in der
Kann-Sammlung infrastructure, jetzt eigenes Ticket, weil sie jeden Lauf bremsen.

## What
Jeder Flake wird reproduziert oder seine Ursache am Code belegt, dann deterministisch gemacht. Kein Wiederholen, kein
Hochsetzen von Zeitgrenzen ohne Begründung, kein Ausschluss aus dem Gate.

1. **Ledger-Replay mit fremden „Perf“-Buchungen.** Rot waren unter anderem „Ledger Persistence (appendAndApply).Replay
   rekonstruiert Buchungen aus allen drei Entstehungsarten …“ (`src/Tests/LedgerTests.fs`), „… Replay reproduziert
   SplitTransferCounterparts …“ und „Ledger Spiegel-Lauf (Split-Übertrag, Nachlauf).Replay reproduziert die Verknüpfung
   eines Spiegel-Laufs“ (`src/Tests/LedgerMirrorTests.fs`). Die Meldungen zeigen Buchungen mit Payee „Perf <n>“, die
   `src/Tests/LedgerPageTests.fs` ~3228 (AC14, 1000 Buchungen) anlegt. Alle stehen in `TestGroups.InMemoryDb` (ADR 0100).
   Hypothese: AC14 schreibt Zustand ohne passende Ereignisse (oder umgekehrt), und ein Replay-Test, der danach läuft,
   vergleicht alle Buchungen der geteilten DB. Klären und beheben, etwa AC14 auf eine eigene DB (Muster
   `BudgetEventsFixture.isolatedDb`) oder die Replay-Tests auf ihre eigenen Buchungen eingrenzen.
2. **AC14-Zeittest** („1000 Buchungen … in angemessener Zeit“, Grenze 2 s, unter Last 3,7 s gemessen): die Aussage
   (Abfrage skaliert) so prüfen, dass Last auf der Maschine sie nicht kippt, etwa über die Zahl der Abfragen oder den
   Plan statt der Wanduhr, oder begründet trennen.
3. **`[flow:design-system-export]`** (`scripts/export-design-system-run.test.mjs`) unter Last rot: Ursache klären
   (verify.sh nennt `vite preview` mit fest 5 s Start), Start abwarten statt fester Zeit.
4. **`regeln-panel @ 1440 px`** (Layout-Sweep, einmal rot: `[data-testid="rule-form"]` ragt unten aus dem Bild,
   1001,493 bis 1400,905 bei 1440 × 900): prüfen, ob Wettlauf im Zustand (wie 118819ea) oder echter Layout-Fehler.

## Acceptance criteria
- [ ] Je Flake die Ursache im Outcome, belegt (Reproduktion, Log oder Codestelle).
- [ ] Replay-Tests sehen keine Buchungen anderer Tests mehr; Gegenprobe: AC14 vor den Replay-Tests laufen lassen
      (Reihenfolge erzwingen) ist danach grün, vorher rot.
- [ ] AC14 prüft seine Aussage ohne Wanduhr-Grenze, die unter Last kippt, oder die Abweichung ist im Outcome begründet.
- [ ] Design-System-Export und `regeln-panel` ohne feste Wartezeiten, die unter Last zu kurz sind.
- [ ] Testzahlen je Kennung im Gate unverändert oder Abweichung begründet (kein Test still entfernt).
- [ ] `./verify.sh` fünfmal hintereinander `VERIFY: PASS`, davon zweimal mit einem zweiten `./verify.sh` parallel in
      einem anderen Worktree (die Last eines Work-Laufs); die Läufe im Outcome.

## Notes
- Gefunden im Work- und Review-Lauf 2026-11-20/21 (Protokoll, Fahrplan „Stand“ 2026-11-21).
- ADR 0100 (DB-Testlisten als eine Expecto-Gruppe) ist der vermutete Auslöser für Punkt 1; die Gruppe selbst bleibt.
- Kein Produktcode außer, falls Punkt 4 ein echter Layout-Fehler ist; dann dort mit Regressionstest.
