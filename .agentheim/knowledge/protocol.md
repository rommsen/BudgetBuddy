# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

---

## 2026-06-12 -- Work: Mobile-UX-Overhaul + Quick Add (retroaktiv erfasst)

**Type:** Work (lief außerhalb von agentheim auf Branch `feature/ux-wow`; am 2026-06-12
nachträglich in agentheim-Artefakte überführt)
**Outcome:** 5 Tasks done, 3 ADRs, deployed auf docker-host (2× , Health-Check grün)
**BCs touched:** categorization, ynab-sync, infrastructure
**Summary:** Großer Mobile-UX-Umbau in einer autonomen Session plus Feedback-Runde nach
Romans Android-Test. (1) Category Picker keyboard-fest und ghost-click-frei gemacht —
visualViewport-Anker + Click-Commit-Pattern, als projektweite Sheet-Patterns in ADR 0005
festgehalten (`categorization`, `infrastructure`). (2) Mobile-Polish: Sticky-Filter,
Spring-Easing, Skeleton (`infrastructure`). (3) Swipe-nach-links für Skip/Unskip
(`ynab-sync`). (4) **Quick Add**: manuelle Transaktions-Eingabe direkt nach YNAB — bewusster
Bruch mit dem Non-Goal aus ADR 0001, nachträglich als Amendment legitimiert (ADR 0003:
enthaltenes Phase-0-Experiment, YNAB bleibt Source of Truth). Technische Gestalt in ADR 0004
(keine ImportId, eigenes Quick-Add-Konto, optionale Felder weggelassen). Feedback-Runde
ergänzte konfigurierbares Quick-Add-Konto, echten Picker (Sheet-über-Sheet), App-konforme
Einstiege statt FAB, Payee optional.
**ADRs written:** 0003 (quick-add-activates-phase-0, global, amendiert 0001),
0004 (manual-entries-no-importid-own-account, ynab-sync),
0005 (visual-viewport-sheets-click-commit, infrastructure)
**Tasks completed:** categorization/done/2026-06-11-mobile-category-picker-keyboard-ghostclick,
infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs,
ynab-sync/done/2026-06-11-swipe-to-skip, ynab-sync/done/2026-06-11-quick-add-manual-entry,
ynab-sync/done/2026-06-12-quick-add-feedback-round
**Verification:** 516/516 Tests (48+ neue in `QuickAddTests.fs`), QA-Review ADEQUATE,
Fable-Build sauber, Deploy-Health-Check bestanden. Offen: Quick-Add-Konto einmalig in
Settings wählen; Vision/READMEs entsprechend aktualisiert.

---

## 2026-06-01 -- Brainstorm: BudgetBuddy als agentheim-Projekt onboarden

**Type:** Brainstorm
**Outcome:** vision created
**BCs identified:** banking-import, categorization, ynab-sync, infrastructure
**Summary:** BudgetBuddy (reifes F#-Tool) wurde unter agentheim aufgesetzt. Zentrale
strategische Entscheidung: die Vision beschreibt den **YNAB-Companion** (Bank→YNAB-Pipeline),
nicht den YNAB-Ersatz — letzterer bleibt explizites Non-Goal (ADR 0001). Ubiquitous Language
direkt aus `src/Shared/Domain.fs` destilliert; drei fachliche Contexts entlang der Sync-Pipeline
plus ein infrastructure-Context (ADR 0002). In-Scope an der Companion-Grenze: Split-UI,
Transfer-Payees, ING als zweite Quelle. Non-Goals: manuelle Eingabe, Cleared-Setting, eigene
Source of Truth.
**ADRs written:** 0001 (companion-not-replacement, global), 0002 (three-bounded-contexts, global)
**Foundation tasks emitted:** skipped — reifes Projekt, Architektur (Stack/SQLite+Dapper/
Fable.Remoting/Docker+Tailscale) existiert bereits und läuft. infrastructure-BC angelegt als
Heimat künftiger Querschnitts-Captures; ADR-Backfill der bestehenden Architektur als offene
Option vermerkt. Kein Walking-Skeleton (App läuft), kein Styleguide-Task (vollständiges Design
System existiert unter `src/Client/DesignSystem/`).

---
