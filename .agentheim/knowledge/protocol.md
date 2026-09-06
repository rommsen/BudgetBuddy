# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

---

## 2026-09-06 18:22 -- Task verified and completed: ledger-m8r2v - Spiegel-Lauf YNAB → Kassenbuch: Delta-Abruf, Snapshot-Vergleich, Ereignisse, manueller Auslöser

**Type:** Work / Task completion
**Task:** ledger-m8r2v - Spiegel-Lauf YNAB → Kassenbuch: Delta-Abruf, Snapshot-Vergleich, Ereignisse, manueller Auslöser
**Summary:** Spiegel-Lauf YNAB → Kassenbuch: budgetweiter Delta-Abruf über server_knowledge, YnabObservation-Contract am ynab-sync-Rand, reiner Vergleich observe, Ereignisse nur bei echtem Unterschied; manueller Endpunkt und Lauf-Status
**Duration:** 39m (2 Iterationen)
**Verification:** PASS (iteration 2)
**Files changed:** 16
**Tests added:** 23
**ADRs written:** 0018, 0019

---

## 2026-09-06 18:10 -- Verification failed: ledger-m8r2v - Spiegel-Lauf YNAB → Kassenbuch

**Type:** Work / Verification failure
**Task:** ledger-m8r2v - Spiegel-Lauf YNAB → Kassenbuch: Delta-Abruf, Snapshot-Vergleich, Ereignisse, manueller Auslöser
**Iteration:** 1 of 3
**Reasons:** SplitTarget-Bullet in ynab-sync/README.md behauptet weiter "nie beides/keins", obwohl ADR 0019 den dritten Fall Uncategorized einführt; README_DELTA enthält keinen replace-Op auf diesen Bullet, die Sektion würde sich nach dem Materialisieren selbst widersprechen
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-09-06 17:40 -- Batch started: [ledger-m8r2v]

**Type:** Work / Batch start
**Tasks:** ledger-m8r2v - Spiegel-Lauf YNAB → Kassenbuch: Delta-Abruf, Snapshot-Vergleich, Ereignisse, manueller Auslöser
**Parallel:** no (1 worker; ledger-q5w9c und ledger-kt0wq bleiben in der Kette auf m8r2v blockiert, keine weiteren ready tasks im Projekt)

---

## 2026-09-06 17:38 -- Task verified and completed: ledger-e3k7p - Kassenbuch-Fundament: Ereignis-Log, Zustandsmodell, appendAndApply und Replay-Test

**Type:** Work / Task completion
**Task:** ledger-e3k7p - Kassenbuch-Fundament: Ereignis-Log, Zustandsmodell, appendAndApply und Replay-Test
**Summary:** Kassenbuch-Fundament: append-only Ereignis-Log pro Buchung nach dem verbindlichen Katalog, abgeleitetes Zustandsmodell mit Fremd-Id-Verknüpfungen, appendAndApply schreibt Log und Zustand in einer SQLite-Transaktion, replayAll gegen die Zustandstabellen geprüft
**Duration:** 24m
**Verification:** PASS (iteration 1)
**Files changed:** 9
**Tests added:** 25
**ADRs written:** 0017

---

## 2026-09-06 15:40 -- Work session ended

**Type:** Work / Session end
**Duration:** ~2h10m (erster Batch-Start bis jetzt, inkl. Diskussion der Decisions mit Roman)
**Completed:** 4 (first-try PASS: 4, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Dispatches:** ledger-fsz0a: 1, ynab-czk34: 1, ledger-z5vw6: 1, ledger-zm0zx: 1, ledger-e3k7p: 1 (auf Romans Wunsch abgebrochen vor dem ersten Edit; Modell nachgezogen, kein Re-Dispatch)
**Commits:** 14 (2 Batch-Start, 4 Task-Integration, 1 .gitignore, 2 Vision, 1 Split, 2 Reconcile, 1 Ereignis-Katalog, 1 Session-End)
**Vision-conformance:** ledger-zm0zx: diverges from success criterion "v1 · Alles unter einem Dach" (ETF-Bullet, "auch wenn YNAB es weiterhin nicht kennt") — bewusste Entscheidung mit Roman (manuelles YNAB-Konto statt BB-eigenes Konto); Vision-Text in derselben Session nachgezogen (9a48cb4), whats-next-Advisory geschrieben. Sonst: batch aligns with vision.
**Carry-over:** .agentheim/knowledge/research/comdirect-kreditkarten-transaktionen-2026-08-04.md: committed (reconcile, 1be1cb0); .serena/project.yml: committed (reconcile, 1bf10fd); .worktrees/ledger-e3k7p: kept (owner: ledger-e3k7p, Task in doing/ nach Batch-Start 09f0043, Worker auf Romans Wunsch gestoppt, Ereignis-Katalog nachmodelliert, nächste Session: Phase 1 Recovery, Worker auf Opus starten)
**Note:** Alle vier Foundation-Decisions wurden vor dem Dispatch mit Roman diskutiert und weichen an drei Stellen von den Architekten-Entwürfen ab (semantische Delta-Ereignisse nach souls-Muster; kein Server-Timer; ETF als manuelles YNAB-Konto). Worker und Verifier auf Sonnet. Skeleton in drei Slices geteilt; e3k7p wartet mit verbindlichem Ereignis-Katalog auf einen Opus-Worker.

---

## 2026-09-06 14:00 -- Batch started: [ledger-e3k7p]

**Type:** Work / Batch start
**Tasks:** ledger-e3k7p - Kassenbuch-Fundament: Ereignis-Log, Zustandsmodell, appendAndApply und Replay-Test
**Parallel:** no (1 worker; ledger-m8r2v, ledger-q5w9c, ledger-kt0wq blocked in chain on e3k7p)

---

## 2026-09-06 15:05 -- Modeling / Split: ledger-kt0wq - Walking Skeleton in drei Slices aufgeteilt

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** todo (alle vier)
**Summary:** Nach den vier Foundation-ADRs (0013 bis 0016) war der Skeleton der einzige freie Task und zu groß für einen Worker-Durchlauf mit sauberer Verifikation. Aufgeteilt in ledger-e3k7p (Ereignis-Log, Zustandsmodell, appendAndApply, Replay), ledger-m8r2v (Spiegel-Lauf: Delta über server_knowledge, Snapshot-Vergleich zu semantischen Ereignissen, manueller Endpunkt) und ledger-q5w9c (Kassenbuch-Seite). ledger-kt0wq bleibt als Abnahme auf dem echten Budget (Human-Gate) und hängt an den drei Slices. Scope-Änderungen gegenüber dem Original: Spiegel budgetweit, Seite zeigt ein Konto, kein BB-eigenes Konto, Auslöser manuell.
**Split into:** ledger-e3k7p, ledger-m8r2v, ledger-q5w9c
**ADRs written:** —

---

## 2026-09-06 12:48 -- Task verified and completed: ledger-zm0zx - Kontenmodell und Bewertung festlegen (Konto-Arten, BB-eigene Konten, Kursquelle)

**Type:** Work / Task completion
**Task:** ledger-zm0zx - Kontenmodell und Bewertung festlegen (Konto-Arten, BB-eigene Konten, Kursquelle)
**Summary:** Kontenmodell (Art × Verknüpfung): ein Konto Bar mit Quick Add als Herkunft; Geldmarkt-ETF in v1 als manuelles YNAB-Konto gespiegelt, Bewertung später als BB-geführte Fähigkeit; Research-Spike ledger-c7j8e (Kursquelle) im Backlog
**Duration:** ~8m
**Verification:** PASS (iteration 1)
**Files changed:** 3
**Tests added:** 0
**ADRs written:** 0016

---

## 2026-09-06 12:47 -- Task verified and completed: ledger-z5vw6 - Identität einer Buchung über Systeme hinweg festlegen (eigene Id + Fremd-Id-Verknüpfung)

**Type:** Work / Task completion
**Task:** ledger-z5vw6 - Identität einer Buchung über Systeme hinweg festlegen (eigene Id + Fremd-Id-Verknüpfung)
**Summary:** Buchungs-Identität: eigene Id (Guid v7), Fremd-Ids als Verknüpfungstabelle, Spiegel matcht YNAB-Id vor ImportId, Überträge als zwei verbundene Buchungen; Folge-Task ynab-b7q2m (Push liest YNAB-Ids aus der Antwort)
**Duration:** ~7m
**Verification:** PASS (iteration 1)
**Files changed:** 3
**Tests added:** 0
**ADRs written:** 0015

---

## 2026-09-06 12:47 -- Task verified and completed: ynab-czk34 - Spiegel-Mechanik YNAB → Kassenbuch festlegen (Delta-Sync, Intervall, Löschungen, Contract)

**Type:** Work / Task completion
**Task:** ynab-czk34 - Spiegel-Mechanik YNAB → Kassenbuch festlegen (Delta-Sync, Intervall, Löschungen, Contract)
**Summary:** Spiegel YNAB → Kassenbuch per Delta-Sync mit server_knowledge, ausgelöst über Frische-Kriterium (5 Minuten bei Seitenaufruf/Vordergrund, nach jedem Push, manueller Fallback) statt Server-Timer; Contract an ledger als reine Daten
**Duration:** ~5m
**Verification:** PASS (iteration 1)
**Files changed:** 2
**Tests added:** 0
**ADRs written:** 0014

---

## 2026-09-06 12:46 -- Task verified and completed: ledger-fsz0a - Historie-Technik im Kassenbuch festlegen (Ereignis-Log + synchrones Zustandsmodell)

**Type:** Work / Task completion
**Task:** ledger-fsz0a - Historie-Technik im Kassenbuch festlegen (Ereignis-Log + synchrones Zustandsmodell)
**Summary:** Kassenbuch-Historie als append-only Ereignis-Log (semantische Delta-Ereignisse nach souls-Muster, persistiert) mit synchron geführtem Zustandsmodell in einer Transaktion; Postgres als verworfene Alternative
**Duration:** ~6m
**Verification:** PASS (iteration 1)
**Files changed:** 3
**Tests added:** 0
**ADRs written:** 0013

---

## 2026-09-06 12:38 -- Batch started: [ledger-fsz0a, ledger-z5vw6, ynab-czk34, ledger-zm0zx]

**Type:** Work / Batch start
**Tasks:** ledger-fsz0a - Historie-Technik im Kassenbuch festlegen (Ereignis-Log + synchrones Zustandsmodell), ledger-z5vw6 - Identität einer Buchung über Systeme hinweg festlegen (eigene Id + Fremd-Id-Verknüpfung), ynab-czk34 - Spiegel-Mechanik YNAB → Kassenbuch festlegen (Delta-Sync, Intervall, Löschungen, Contract), ledger-zm0zx - Kontenmodell und Bewertung festlegen (Konto-Arten, BB-eigene Konten, Kursquelle)
**Parallel:** yes (4 workers; ledger-kt0wq blocked on fsz0a/czk34/z5vw6 — dispatched after this batch, scope re-check with Roman first)
**Planning advisory:** whats-next.md vom 2026-06-20 — stale, ignoriert; Reihenfolge nach Diskussion mit Roman: alle vier Decisions vorab abgestimmt, Entscheidungen in den Task-Notes

---

## 2026-09-06 11:55 -- Brainstorm: BudgetBuddy als führendes System (YNAB-Ablösung, graduell)

**Type:** Brainstorm
**Outcome:** vision revised
**BCs identified:** ledger (neu); ynab-sync erweitert um Spiegel-Richtung; Seeds wealth, budgeting (nicht angelegt)
**Summary:** Sokratische Session mit Roman. Kern-Erkenntnisse: Budgetieren tut nicht weh (Parität ist Non-Goal ohne Kassenbuch-Grund); der konkrete Schmerz ist das Geldmarkt-ETF-Konto als Budget-Geld, das YNAB nicht führen kann, plus fehlende Ownership/Historie (BB vergisst jede Buchung). YNAB wird an fünf Stellen an BB vorbei beschrieben, deshalb startet das Kassenbuch als "Spiegel mit Gedächtnis". Fähigkeiten wandern einzeln von YNAB-geführt zu BB-geführt; v1 = alle Konten unter einem Dach inkl. ETF, v2 = Nachbearbeitung in BB mit Historie (Gate für Budgetieren). Cutover ist Option, kein Ziel. ADR 0001 abgelöst; `docs/idea-ynab-replacement.md` als aktiviert-in-revidierter-Form markiert.
**ADRs written:** 0012 (global, supersedes 0001)
**Foundation tasks emitted:** ledger-fsz0a (Historie-Technik), ynab-czk34 (Spiegel-Mechanik), ledger-zm0zx (Kontenmodell/Bewertung), ledger-z5vw6 (Buchungs-Identität); Skeleton-Spike ledger-kt0wq (statt reserviertem infrastructure-001: Projekt reif, Skeleton gilt dem Kassenbuch); Styleguide-Task nicht neu (design-system-001 done, Gate gilt für ledger-UI). Architekten-Pass ohne Subagent (Agent-Tool braucht tmux, nicht installiert); ADR-Entwürfe in den Task-Notes.

---

