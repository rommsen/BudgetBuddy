# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

---

## 2026-06-18 22:20 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (first-try PASS: 1, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 2 Task (82b5cef bug/infra-001, 9e9526a refactor/design-system-002) + 1 chore(agentheim)-Bookkeeping
**Note:** Sequenziell (geteilte Solution/`Tests.fsproj`/Diary → kein Parallel-Dispatch). infra-001 zuerst → Test-Gate stabil (595 grün), dann design-system-002 dagegen verifiziert. design-system-002 brauchte iteration 2 (Verifier fand 3 übersehene rohe `text-[10px]` in DS-Komponenten; byte-identisch gefixt). Audit splittete den Komponenten-Drift in design-system-006/007 (backlog). Origin-Push + Deploy: nicht ausgeführt (Roman-Go ausstehend).

---

## 2026-06-18 22:18 -- Task verified and completed: design-system-002 - Drift-Audit + Konsolidierung

**Type:** Work / Task completion
**Task:** design-system-002 - Drift-Audit + Konsolidierung des View-Codes auf den Styleguide
**Summary:** View-Layer gegen den Styleguide-Gate auditiert: wenig Token-, viel Komponenten-Drift. Risikoarmen Token-Drift voll konsolidiert — neue Tokens `Colors.onNeon`/`onNeonMuted` (dunkle Schrift auf Neon) + `FontSizes.micro`/`microPlus` (Sub-xs-Mikro-Labels); alle Call-Sites byte-identisch gehoben (kein visueller Regress). Großen Button/SVG-Drift bewusst gesplittet statt Mega-Refactor.
**Verification:** PASS (iteration 2) — iter 1 FAIL (3 übersehene rohe `text-[10px]` in DS-Komponenten: Badge.fs:91, Navigation.fs:131, Stats.fs:75), Worker re-dispatcht, alle drei gehoben + Inventur/ADR wahrheitsgetreu nachgezogen; Verifier-Re-Check: 0 Call-Site-Residuen, build 0/0, tests 595 passed / 6 skipped / 0 failed.
**Commit:** 9e9526a
**Files changed:** 11 (Tokens.fs, Badge.fs, Navigation.fs, Stats.fs, StatusViews.fs, TransactionRow.fs, Settings/View.fs, Rules/View.fs, Diary, DS-README, ADR 0009)
**Tests added:** 0 (reines Token-Anheben, CSS-String identisch; stabiles Gate aus infra-001 als Regressions-Schutz)
**ADRs written:** 0009-onneon-foreground-and-micro-font-tokens.md (scope: design-system)
**New backlog items:** design-system-006 (Html.button → Button), design-system-007 (Html.svg → Icons) — Split aus dem Audit

---

## 2026-06-18 22:10 -- Verification failed: design-system-002 - Drift-Audit + Konsolidierung

**Type:** Work / Verification failure
**Task:** design-system-002 - Drift-Audit + Konsolidierung des View-Codes auf den Styleguide
**Iteration:** 1 of 3
**Reasons:** AC#2 unvollständig — `Badge.fs:91` (`sizeToClass`/Small) noch rohes `text-[10px]` trotz neuem `FontSizes.micro`-Token, in einer vom Worker editierten Datei (SUMMARY „voll konsolidiert" ist dafür falsch); Audit-Inventur hat die Stelle nie erfasst (AC#1-Lücke); zwei weitere rohe `text-[10px]` in DS-Komponenten (Navigation.fs:131, Stats.fs:75) unerwähnt.
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker (iteration 2)
**Note:** Rest sauber — AC#1/#3/#4/#5, ADR 0009, Splits 006/007 (ehrlich, ADR-0005-bewusst), Build 0/0, Tests 595/6/0, alle umgestellten Token-Werte byte-identisch. Fix ist byte-identisch + risikolos.

---

## 2026-06-18 21:58 -- Batch started: [design-system-002]

**Type:** Work / Batch start
**Tasks:** design-system-002 - Drift-Audit + Konsolidierung des View-Codes auf den Styleguide
**Parallel:** no (1 worker)
**Note:** Wave 2 von 2. Nach infra-001 (Test-Gate jetzt stabil, 595 grün). Audit zuerst, dann risikoarme Konsolidierung; bei großem Drift Konsolidierung pro BC als neue Backlog-Items splitten (NEW_BACKLOG_ITEMS) statt Mega-Refactor — Worker darf bouncen.

---

## 2026-06-18 21:55 -- Task verified and completed: infra-001 - Flaky Persistence-Test

**Type:** Work / Task completion
**Task:** infra-001 - Flaky Persistence-Test — SQLite-Disposal-Crash + Microsoft.Data.Sqlite-Versionskonflikt
**Summary:** Root Cause war NICHT der Versionskonflikt (Red Herring/Aggravator), sondern ein Disposal-Race: Testmodus gab dasselbe geteilte `SqliteConnection`-Objekt an alle parallelen Dapper-Ops → `RemoveCommand`-index-out-of-range beim Command-Dispose. Fix: frische Connection pro Operation (Keep-Alive-Anker hält die `Cache=Shared`-In-Memory-DB), `Microsoft.Data.Sqlite` auf 9.0.13 in Server+Tests gepinnt. Spiegelt Prod-Verhalten.
**Verification:** PASS (iteration 1) — Verifier mappte alle 5 AC auf Evidenz und lief **10 frische Voll-Läufe** (595 passed / 6 skipped / 0 failed), nachdem er den Race gegen den alten Code reproduziert hatte; AC #5 (nur `PersistenceTypeConversionTests.fs` macht echte DB-Ops) unabhängig bestätigt.
**Commit:** 82b5cef
**Files changed:** 7 (Persistence.fs, Server.fsproj, Tests.fsproj, PersistenceTypeConversionTests.fs +Regressions-Guard, Diary, infrastructure-README, ADR 0008)
**Tests added:** 1 (`Persistence Connection Disposal` — 50 parallele Insert+Read; Defense-in-depth, der eigentliche Beweis ist die Multi-Lauf-Determinik)
**ADRs written:** 0008-sqlite-per-operation-connection-and-version-pin.md (scope: infrastructure)

---

## 2026-06-18 21:40 -- Batch started: [infra-001]

**Type:** Work / Batch start
**Tasks:** infra-001 - Flaky Persistence-Test — SQLite-Disposal-Crash + Microsoft.Data.Sqlite-Versionskonflikt
**Parallel:** no (1 worker)
**Note:** Wave 1 von 2. Sequenziell, weil infra-001 und design-system-002 die geteilte Solution/`Tests.fsproj` + `diary/development.md` berühren (kein Parallel-Dispatch). infra-001 zuerst, um das grüne Test-Gate zu stabilisieren, bevor design-system-002s „Tests grün"-AC dagegen verifiziert wird.

---

## 2026-06-18 21:37 -- Modeling / Promoted: design-system-002 + infra-001

**Type:** Modeling / Promote (Batch)
**BC:** design-system (design-system-002), infrastructure (infra-001)
**From → To:** backlog → todo (beide)
**Summary:** Romans „die beiden offenen Tickets" → REFINE landete bei PROMOTE für beide.
(1) **design-system-002** (Drift-Audit + Konsolidierung): das blockierende Gate
`design-system-001` ist inzwischen done + akzeptiert — die harte Abhängigkeit war beim
Schreiben am 13.06. noch offen, jetzt erfüllt. Schnitt (Roman): **ein Task**, Audit als
Schritt 1; Konsolidierung erst nach dem Audit pro BC splitten, falls der Drift groß ist.
(2) **infra-001** (Flaky SQLite-Disposal-Test): Schnitt (Roman): **ein Bug-Task** statt
Spike+Fix — Untersuchung/Fix eng gekoppelt; AC #1 erzwingt Root-Cause-Bestätigung als
Schritt 1, Worker darf bouncen, falls die Ursache größer ist als die Versions-Hypothese.
**Nebenbei:** `design-system/todo/` existierte nicht und wurde angelegt.

---

## 2026-06-18 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 Feature (895e56e design-system-005) + 1 Doku-Bookkeeping folgt
**Note:** Diesmal Geräte-Verifikation eingebaut: headless mobiler Playwright-Screenshot, vom Orchestrator mit eigenen Augen gesichtet → die Platzierungs-Defekte sind weg, bevor deployt wird. Danach: origin-Push + Re-Deploy (Roman-Go).

---

## 2026-06-18 -- Task verified and completed: design-system-005 - Toast-Mobile-Platzierung

**Type:** Work / Task completion
**Task:** design-system-005 - Toast fügt sich auf Mobile nicht ins Layout
**Summary:** Mobile/Desktop als zwei getrennte Fälle: mobil kompakter, symmetrisch eingerückter Streifen (`top-16 inset-x-4`), `positionToClasses` nur noch `md:`-Anker → kein Full-Height-Stretch, keine abgeschnittene Neon-Border; innere Fläche voll deckend (`bg-surface-card`) → lesbar über dem Hero. Motion aus 004 unberührt.
**Verification:** PASS (iteration 1) — verifier: alle Checks grün, `dotnet test` 594 passed / 6 skipped / 0 failed, ToastLifecycleTests unverändert grün, ADR/Styleguide truthful. **Plus Orchestrator-Sichtung der headless-Screenshots** (/tmp/ds005/*.png) → Defekte visuell weg.
**Commit:** 895e56e
**Files changed:** 4 (Toast.fs, styleguide.md, ADR 0007 amended, Diary)
**Tests added:** 0 (reine CSS-Platzierung; visuell verifiziert; 004-Lifecycle-Tests decken die Logik)
**ADRs written:** 0007 (AMENDED — Platzierung als zwei-Fall präzisiert)

---

## 2026-06-18 -- Batch started: [design-system-005]

**Type:** Work / Batch start
**Tasks:** design-system-005 - Toast fügt sich auf Mobile nicht ins Layout
**Parallel:** no (1 worker)
**Note:** Folge-Bug zu design-system-004. Diesmal mit Geräte-Verifikation: headless mobiler Playwright-Screenshot (Vite :5181, hash-routing) — der Orchestrator sichtet das PNG mit eigenen Augen, weil das AC-Gate visuelle Integration nicht fangen kann.

---

## 2026-06-18 -- Modeling / Captured: infra-001 + design-system-005 (Live-Feedback)

**Type:** Modeling / Capture
**BC:** infrastructure (infra-001), design-system (design-system-005)
**Filed to:** backlog (infra-001 — Root Cause unbestätigt), todo (design-system-005 — gut diagnostiziert)
**Summary:** Zwei Punkte aus Romans Geräte-Check nach dem Deploy. (1) **infra-001** — der flaky
Persistence-Test (`PatternType Conversions`; SQLite-`RemoveCommand`-Crash beim Disposal +
`Microsoft.Data.Sqlite` 9.0.11/9.0.13-Versionskonflikt), den der design-system-004-Verifier sah.
(2) **design-system-005** — Folge-Bug zu design-system-004: der Toast-**Abgang** stimmt, aber die
**mobile Platzierung** nicht: linke Neon-Border am Viewport-Rand abgeschnitten, überdeckt die
Filter-Chips, transluzent über dem Hero-Gradient. Root-Cause-Richtung im Code: `container` erbt
`bottom-4 right-4` aus `positionToClasses BottomRight` und stellt `top-16` nur voran → mobil
`top-16 bottom-4 right-4`, halb-überschrieben statt bewusst gebaut. Das AC-Gate konnte das nicht
fangen (keine Geräte-Rendering-Prüfung).

---

## 2026-06-16 23:25 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (first-try PASS: 1, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 2 Feature (9112013 cat-001, 18cf474 design-system-004) + 1 Doku-Bookkeeping folgt
**Note:** Beide todo-Tasks sequenziell abgearbeitet (geteilte `Tests.fsproj`/Solution-Build → kein Parallel-Dispatch). cat-001 brauchte iteration 2 (Token-/Touch-Target-Defekte aus iteration 1 behoben). Roman-Anweisung danach: alles committen + zu origin pushen, dann deployen.

---

## 2026-06-16 23:20 -- Task verified and completed: design-system-004 - Toast-Politur

**Type:** Work / Task completion
**Task:** design-system-004 - Toast-Politur — sanfter Abgang, Positionierung & Styleguide-Motion
**Summary:** Zwei-Phasen-Toast-Abgang (Exiting-Flag → CSS-Exit → entfernen), geteilt von Auto-Dismiss + manuellem Schließen, Double-Fire-Guard; Platzierung + Spring-/Exit-Motion in Styleguide (geschrieben + /styleguide-Route) und ADR 0007; prefers-reduced-motion respektiert.
**Verification:** PASS (iteration 1) — verifier mappte alle 7 AC auf Evidenz, baute unabhängig + `dotnet test` (594 passed / 6 skipped / 0 failed); beide Dismiss-Pfade konvergieren auf `StartDismissToast` (Guard gg. Double-Timer), Token-Disziplin sauber.
**Commit:** 18cf474
**Files changed:** 12 (Types/State/Toast/View/styles.css/Styleguide-Route + styleguide.md + ToastLifecycleTests +8 + Tests.fsproj + Diary + DS-README)
**Tests added:** 8 (`ToastLifecycleTests` — markExiting/isExiting/remove/idempotent/no-op/Dauer)
**ADRs written:** 0007-toast-placement-and-soft-exit.md (scope: design-system)

---

## 2026-06-16 23:08 -- Batch started: [design-system-004]

**Type:** Work / Batch start
**Tasks:** design-system-004 - Toast-Politur — sanfter Abgang, Positionierung & Styleguide-Motion
**Parallel:** no (1 worker; Wave 2 nach cat-001 — sequenziell wegen geteilter `Tests.fsproj`/Solution-Build)

---

## 2026-06-16 23:05 -- Task verified and completed: cat-001 - Regel-Reorder-UI

**Type:** Work / Task completion
**Task:** cat-001 - Regel-Präzedenz per Hoch/Runter umsortierbar machen (UI)
**Summary:** ▲/▼ pro Regel-Zeile ändern die Präzedenz (oben gewinnt zuerst); persistiert die volle Reihenfolge über die bestehende `reorderRules`-API. Reiner Frontend-Task, Backend war fertig.
**Verification:** PASS (iteration 2) — iteration 1 FAILte auf 2 lokalen View-Defekten (`!min-h-0` killte Mobile-Touch-Target; `text-[11px]` arbitrary font size), iteration 2 behob beide; Suite grün 586 passed / 6 skipped / 0 failed.
**Commit:** 9112013
**Files changed:** 7 (Rules Types/State/View, Icons.chevronUp, RulesReorderTests +9, Tests.fsproj, Diary)
**Tests added:** 9 (`RulesReorderTests` — reorderedIds inkl. Ränder, unbekannte id, Permutation)
**ADRs written:** none

---

## 2026-06-16 22:55 -- Verification failed: cat-001 - Regel-Reorder-UI

**Type:** Work / Verification failure
**Task:** cat-001 - Regel-Präzedenz per Hoch/Runter umsortierbar machen (UI)
**Iteration:** 1 of 3
**Reasons:** `!min-h-0` überschreibt `Button.Small`-`min-h-[36px]` → Mobile-Touch-Target kollabiert (AC 6 verletzt); `text-[11px]` arbitrary font size verboten (`.claude/rules/design-tokens.md`)
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker (iteration 2). Rest grün (586 passed; reorderedIds + 9 Tests korrekt).

---

## 2026-06-16 22:15 -- Batch started: [cat-001]

**Type:** Work / Batch start
**Tasks:** cat-001 - Regel-Präzedenz per Hoch/Runter umsortierbar machen (UI)
**Parallel:** no (1 worker)
**Note:** Sequenziell statt parallel mit design-system-004 — beide würden `src/Tests/Tests.fsproj` anfassen und parallele `dotnet build`/`test` racen auf einer Solution. cat-001 zuerst (Frontend-only; Backend `reorderRules`→`updatePriorities` steht).

---

## 2026-06-16 22:10 -- Modeling / Captured: cat-001 + design-system-004 (2 Tasks → todo)

**Type:** Modeling / Capture
**BC:** categorization (cat-001), design-system (design-system-004)
**Filed to:** todo (beide — gut abgegrenzt, Gate design-system-001 done+reviewt)
**Summary:** Zwei Live-Feedback-Punkte von Roman erfasst. (1) **cat-001** — UI zum Umsortieren
von Regeln (▲/▼) für Präzedenz; zu-eager Regeln sollen *über die Reihenfolge verlieren*.
Code-Review ergab: das Backend ist **End-to-End fertig** (`reorderRules` → `updatePriorities`,
Engine sortiert `priority DESC`, „erste passende gewinnt") — es fehlt **nur** das UI (keine
Reorder-`Msg` in `Components/Rules/`). Reiner Frontend-Task. Drag&Drop bewusst verworfen
(Roman: Hoch/Runter, mobil-sicher). (2) **design-system-004** — Toast-Politur: sanfter Abgang
(heute entfernen beide Dismiss-Pfade sofort ohne Exit-Animation → Zwei-Phasen-Removal in
`State.fs` nötig), Positionierung/Integration, Look/Motion gegen den Styleguide. „Stapeln
ruckelt" von Roman *nicht* als Problem markiert → out of scope. Beide hängen am Styleguide-Gate
(design-system-001, done).

---

## 2026-06-16 -- Bug-Fix abgeschlossen: ynab-004 - Split-Zeile zeigt "Aufgeteilt"

**Type:** Work / Task completion (Bug-Fix aus Live-Feedback)
**Task:** ynab-004 - Split-Transaktion in der Liste als "Aufgeteilt" kennzeichnen
**Auslöser:** Roman-Feedback am Gerät: aufgeteilte REWE-Buchung zeigte weiter den orangen „Kategorie…"-Platzhalter (Split hat `CategoryId = None`, Chip-Logik prüfte nur die Kategorie, nicht `Splits`).
**Fix:** Client-Display — `getCategoryBadgeClass` + neue testbare `categoryChipLabel` berücksichtigen `tx.Splits` → Label „Aufgeteilt" + `badge-ready` statt `badge-attention`.
**Verification:** Build grün; `dotnet test` 577 passed / 6 skipped / 0 failed (Regression: Split-Badge ready + categoryChipLabel-Suite). Kleiner, direkt getesteter Display-Fix — kein separater verifier.
**Commit:** ae7d448
**Caveat:** Splits nicht persistiert (`Persistence.fs:677`); Fix gilt für die In-Memory-Session (realer Flow). Persistieren = aufgeschobene Folgeaufgabe.
**ADRs written:** none.

---

## 2026-06-16 23:50 -- Bug-Fix verifiziert + abgeschlossen: ynab-003 - Split-Vorzeichen + Rest-Button

**Type:** Work / Task completion (Bug-Fix aus Live-Feedback)
**Task:** ynab-003 - Split-Editor: Vorzeichen-Fix + editierbare Beträge + Rest-Button
**Auslöser:** Roman-Feedback (2026-06-16) zum deployten ynab-002: Cashback-Split addierte positive Eingabe gegen negatives Total (−222,15 − 200 → −422,15) statt zu verrechnen; erste Kategoriezeile nicht editierbar.
**Root Cause:** Ausgabe ist negativ; UI zeigt positives `0,00`-Feld, Nutzer tippt positiv → `splitRemainder` (signiert) bläht die Magnitude auf. Die ynab-002-Tests maskierten den Bug, weil ihre Fixtures *negative* Beträge tippten (= dieselbe falsche Annahme wie der Code).
**Fix:** Betrags-Modell auf positive Magnituden umgestellt, `sign(Total)` intern angewandt (`draftLineToSplit`); read-only Auto-Rest-Zeile entfernt → alle Beträge editierbar; Rest-Button pro Zeile (`FillSplitRemainder`, `restMagnitudeForLine`). Validierung weiter über shared `mkSplits`/`splitRemainder` (ADR 0006), nicht reimplementiert.
**Verification:** PASS (iteration 1) — fresh-eyes verifier hat unabhängig gebaut + `dotnet test` (572 passed / 6 skipped / 0 failed); Regression nutzt positive Eingabe gegen negatives Total.
**Commit:** 14e1f61
**Files changed:** 5 Code/Test + Diary (Bookkeeping separat)
**Tests added:** Sign-Application, Cashback-Regression (200 vs −222,15 → −22,15), Rest-Button (inkl. Clamp), Live-Rest; alte negativ-tippenden Cashback-Tests ersetzt.
**ADRs written:** none (0006 deckt die Invariante; reiner Client-Adaptions-Fix).

---

## 2026-06-13 23:32 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 (f2a71ac ynab-002)
**Note:** Erster UI-Task gegen den frischen Styleguide. Die im Refinement vorhergesagte Backend-Erweiterung (`YnabAccount`/`accountDecoder` um `on_budget`/`closed`) war real und nötig — sauber mitgebaut. Eine bewusste, vom verifier akzeptierte AC-Abweichung beim Cashback (Auto-Rest-Zeile statt `buildCashbackSplit`, Invariante via `mkSplits`/`splitRemainder` gewahrt). **Uncommitted im Working Tree:** `.agentheim`-Doku-Bookkeeping dieser + früherer Sessions (INDEX/protocol/next-step/Task-commit-Feld + ältere lose Modeling-Stände) — bewusst nicht in den Feature-Commit gemischt; eigener Doku-Commit offen.

---

## 2026-06-13 23:30 -- Task verified and completed: ynab-002 - Split-Review-UI

**Type:** Work / Task completion
**Task:** ynab-002 - Split-Review-UI — Cashback-Shortcut + generischer Editor
**Summary:** Review-Flow-Split-Sheet: Ein-Tipp-„Barabhebung"-Cashback-Shortcut (Transfer aufs Quick-Add-Bargeld-Konto + Kategorie-„Rest"-Zeile, die sich live errechnet) + generischer N-Zeilen-Editor; Save gesperrt bei Summe ≠ Gesamt; Transfer-Picker nur offene On-Budget-Konten. `YnabAccount`/`accountDecoder` um `OnBudget`/`Closed` erweitert.
**Verification:** PASS (iteration 1) — verifier hat unabhängig gebaut (`dotnet build` 0 Fehler, 1 vorbestehende Sqlite-Warnung) + `dotnet test` (569 passed / 6 skipped / 0 failed) und jede der 6 AC auf Tests/Artefakte gemappt.
**Abweichung (non-blocking, vom verifier akzeptiert):** Cashback nicht über `buildCashbackSplit` (das rechnet den Transfer als Rest *einer Kategorie* — invers zur „nur Transfer eintippen"-UX), sondern via Auto-Rest-Kategoriezeile; bindende Invariante (ADR 0006: kein Client-Reimplementieren von `mkSplits`/`splitRemainder`) bleibt gewahrt, im Task-Outcome dokumentiert, unit-getestet.
**Commit:** f2a71ac
**Files changed:** 16 (+ Task-Move backlog→done)
**Tests added:** SplitEditorTests.fs (Live-Rest + Save-Gating) + openOnBudgetAccounts/Decoder-Tests in SplitTransactionTests.fs/YnabClientTests.fs
**ADRs written:** none (0004/0005/0006 deckten den Entwurf)

---

## 2026-06-13 23:05 -- Batch started: [ynab-002]

**Type:** Work / Batch start
**Tasks:** ynab-002 - Split-Review-UI — Cashback-Shortcut + generischer Editor
**Parallel:** no (1 worker)
**Note:** Erster UI-Task gegen den frischen Styleguide (Gate `design-system-001` geschlossen). Trägt eine kleine Backend-Erweiterung (`YnabAccount`/`accountDecoder` um `on_budget`/`closed`) für den On-Budget-Filter.

---

## 2026-06-13 23:00 -- Modeling / Refined + Promoted: ynab-002 - Split-Review-UI

**Type:** Modeling / Refine (+ Promote backlog → todo)
**BC:** ynab-sync
**Status after:** todo
**Summary:** Die drei offenen UI-Fragen aus dem Backlog-Eintrag aufgelöst und den Task
promotet. (1) **Sheet-Stacking** — keine neue Entscheidung nötig: ADR 0005 §4 deckt es bereits
(`.layer-2`, eine Ebene tief, „Picker über Quick-Add-Formular"); das Split-Sheet ist das
Form-Sheet. (2) **Off-Budget-Konten** — Roman entschied: Transfer-Picker zeigt nur offene
On-Budget-Konten. Refinement deckte auf, dass das eine kleine Backend-Erweiterung verlangt
(`YnabAccount`/`accountDecoder` tragen heute kein `on_budget`/`closed` — `src/Shared/Domain.fs:334`,
`src/Server/YnabClient.fs:24`); jetzt in AC 4 als load-bearing notiert. (3) **Cashback-Default-Ziel**
— Roman entschied: das konfigurierte Quick-Add-Konto (`ynab_quickadd_account_id`, ADR 0004)
wiederverwenden, überschreibbar; bewusste Konflation manueller-Eingabe-Konto = Cashback-Ziel.
**Gate:** `design-system-001` ist done + gate-reviewt → das Hard-Enforcement-Gate, das ynab-002 in
Backlog hielt, ist geschlossen; Promotion frei.
**Split into:** —
**ADRs written:** keine (ADR 0004/0005/0006 deckten alle Entscheidungen; ADR 0006 `related_tasks`-
Pfade auf done/ynab-001 + todo/ynab-002 aktualisiert).

---

## 2026-06-13 14:20 -- Follow-up fix + visueller Gate-Check: design-system-003

**Type:** Work / Follow-up fix
**Task:** design-system-003 (visuelles Gate)
**Gate-Review:** Romans visueller Check durchgeführt — Route headless gerendert (Desktop + Mobile, `localhost:5181/#/styleguide`), Galerie rendert alle 14 Sektionen mit echten Komponenten korrekt. **Akzeptiert.**
**Defekt gefunden + gefixt:** React-„unique key"-Warnings in `Styleguide/View.fs` → stabile keys/keyedFragment auf alle kollektions-erzeugten Geschwister. Empirisch verifiziert (Build grün + 0 Warnings beim Re-Render).
**Commit:** fb0849b
**Note:** Das menschliche Gate von design-system-003 ist damit geschlossen.

---

## 2026-06-13 14:06 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (first-try PASS: 2, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 2 (c611e98 design-system-001, 741af87 design-system-003)
**Note:** Eingestiegen mit `work design-system-001` (Markdown-Styleguide). Das Gate-Review mit Roman deckte auf, dass er einen **visuell gerenderten** Styleguide erwartet hatte — mid-session als neues Feature `design-system-003` (live `/styleguide`-Route) erfasst und gleich mitgebaut. Beide Tasks first-try verifiziert. Offen: Romans visueller Abnahme-Check der Route (menschliches Gate). Uncommittete Prior-Modeling-Änderungen (vision/context-map/ynab-sync/serena) bewusst nicht mitgemischt.

---

## 2026-06-13 14:05 -- Task verified and completed: design-system-003 - Live /styleguide-Route

**Type:** Work / Task completion
**Task:** design-system-003 - Live In-App /styleguide-Route (visueller Styleguide)
**Summary:** Neue präsentationale Feliz-Route `/styleguide`, die die echten DS-Komponenten + Tokens als Galerie rendert (gegliedert wie das Markdown-Gate); interaktive Demos per lokalem React.useState, kein neuer app-weiter Msg; dezenter Einstieg via Hash-Link in Settings.
**Verification:** PASS (iteration 1) — verifier hat `dotnet build src/Client/Client.fsproj` unabhängig nachgebaut (0 Warnungen / 0 Fehler), bestätigte echte Komponenten (kein Drift), saubere Scope.
**Gate-Review (offen):** Romans visueller Check (zweites, menschliches Gate) steht noch aus — Route ist buildbar + verifiziert; Abnahme erfolgt durch Anschauen im Browser.
**Files changed:** 9 (neues View-Modul + Routing-Wiring Types/State/View/fsproj + Settings-Link + 2 Doku-Pointer + Diary)
**Tests added:** 0 (rein präsentational — bewusst, im Task dokumentiert)
**ADRs written:** none

---

## 2026-06-13 13:58 -- Batch started: [design-system-003]

**Type:** Work / Batch start
**Tasks:** design-system-003 - Live In-App /styleguide-Route (visueller Styleguide)
**Parallel:** no (1 worker)
**Note:** Aus dem design-system-001-Gate-Review ausgegliedert (Roman erwartete einen visuell gerenderten Styleguide). Frontend-Feature: rendert die echten DS-Komponenten/Tokens live → kein Drift.

---

## 2026-06-13 13:55 -- Task verified and completed: design-system-001 - Styleguide (das Gate)

**Type:** Work / Task completion
**Task:** design-system-001 - Styleguide retroaktiv kodifizieren (das Gate)
**Summary:** Bestehendes Design System (20 Komponenten + Tokens.fs) als Markdown-Styleguide `standards/frontend/styleguide.md` kodifiziert; Pointer in CLAUDE.md + overview.md; Diary.
**Verification:** PASS (iteration 1) — Doku-Content-Audit; verifier fing die Orange/Green-„Primary"-Spannung und bestätigte die Reconciliation als konsistent.
**Gate-Review:** Roman akzeptiert das Markdown als **geschriebenen Begleiter**. Review deckte auf, dass Roman zusätzlich einen **visuell gerenderten** Styleguide erwartet → ausgegliedert als Feature **design-system-003** (live In-App `/styleguide`-Route, rendert die echten DS-Komponenten/Tokens → kein Drift).
**Files changed:** 4 (+ neue BC-Scaffold-Dateien erstmals committet)
**Tests added:** 0 (reine Doku)
**ADRs written:** none

---

## 2026-06-13 13:52 -- Batch started: [design-system-001]

**Type:** Work / Batch start
**Tasks:** design-system-001 - Styleguide retroaktiv kodifizieren (das Gate)
**Parallel:** no (1 worker)

---

## 2026-06-13 -- Modeling / Captured: design-system-001 + design-system-002 (neuer BC design-system)

**Type:** Modeling / Capture
**BC:** design-system (neu angelegt — löst die in vision.md/context-map.md aufgeschobene Frage auf)
**Filed to:** todo (design-system-001), backlog (design-system-002)
**Summary:** Retroaktiver Styleguide. Das Design System existiert vollständig im Code
(`src/Client/DesignSystem/`, 20 Komponenten + `Tokens.fs`), aber ohne reviewbaren Styleguide.
Neuer `design-system`-BC (first-class Frontend-Infra, analog `infrastructure`). **design-system-001**
(todo, chore): bestehendes DS in `standards/frontend/styleguide.md` kodifizieren — visuelle
Sprache, Farbsemantik, Token-Layer, Komponenten-Inventar, Muster (ADR 0004/0005), Motion, Voice.
**Das Gate** (Hard-Enforcement, Roman gewählt 2026-06-13): kein UI-Task → todo vor Styleguide
done+reviewt; `ynab-002.depends_on += design-system-001`. **design-system-002** (backlog, refactor,
hängt an 001): Drift-Audit der View-Schichten + Konsolidierung auf Tokens/DS-Komponenten.
**Scope-Wahl:** "Kodifizieren + Konsolidieren" → bewusst in 001 (Doku/Gate) und 002 (Code) getrennt,
weil der Styleguide erst als Wahrheit stehen muss, bevor man Code dagegen konsolidiert.
**Aktualisiert:** vision.md + context-map.md (Open Question aufgelöst, BC + Relationship ergänzt),
knowledge/index.md (bc-list), ynab-sync INDEX + ynab-002 (Gate-Abhängigkeit).

---

## 2026-06-13 11:56 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 (688d81c)
**Note:** Worker-Verbindung brach einmal mitten in der Arbeit ab (Socket-Fehler nach ~22 min); per SendMessage mit erhaltenem Kontext fortgesetzt, sauber zu Ende geführt. Verifier-Gate trotzdem regulär durchlaufen (PASS, iteration 1).

---

## 2026-06-13 11:55 -- Task verified and completed: ynab-001 - Split mit Transfer-Zeile

**Type:** Work / Task completion
**Task:** ynab-001 - Split mit Transfer-Zeile — Domain + YNAB-Push (Fundament)
**Summary:** SplitTarget-DU (ToCategory | ToTransfer) + geteilter mkSplits-Smart-Constructor; Push löst die Transfer-payee_id beim Senden auf (GET /payees, Join auf TransferAccountId, einmal pro Batch, nur bei Transfer-Zeilen), serialisiert mit payee_id ohne category_id-Key; fehlende Transfer-Payee → RejectedByYnab, aus Body ausgeschlossen.
**Verification:** PASS (iteration 1) — verifier re-ran build + tests
**Commit:** 688d81c
**Files changed:** 10 (4 Quell-, 4 Test-, BC-README, Diary)
**Tests added:** SplitPushTests.fs (neu, 224 Z.) + SplitTransactionTests.fs erweitert; Suite 541 passed / 6 skipped / 0 failed
**ADRs written:** none (ADR 0006 lag bereits vor und deckte das Design vollständig)

---

## 2026-06-13 11:36 -- Batch started: [ynab-001]

**Type:** Work / Batch start
**Tasks:** ynab-001 - Split mit Transfer-Zeile — Domain + YNAB-Push (Fundament)
**Parallel:** no (1 worker)

---

## 2026-06-13 -- Modeling / Refined: ynab-001 - Split mit Transfer-Zeile

**Type:** Modeling / Refine
**BC:** ynab-sync
**Status after:** ynab-001 → todo; ynab-002 → backlog
**Summary:** Über den Orchestrator (tactical-modeler + architect) verfeinert, gestützt auf die
Research (PASS). Kern-Domain-Entscheidung getroffen und als **ADR 0006** festgehalten:
Split-Zeile als DU `SplitTarget = ToCategory | ToTransfer`, die Transfer-Zeile speichert das
Ziel-*Konto*, `payee_id` wird erst beim Push via `GET /payees` (Join auf `TransferAccountId`)
aufgelöst; strukturelle Invarianten im geteilten `mkSplits`; fehlende Transfer-Payee →
per-Transaktion-`RejectedByYnab`. Keine Daten-Migration (Splits nicht persistiert). In zwei
Tasks zerlegt.
**Split into:** ynab-001 (verfeinert, Domain+Push-Fundament → **todo**), ynab-002 (neu,
Review-UI: Cashback-Shortcut + generischer Editor → backlog, hängt an ynab-001)
**ADRs written:** 0006 (transfer-line-in-split-du-account-payee-at-push, scope ynab-sync)

---

## 2026-06-13 -- Research: Transfer in YNAB Split-Subtransaction

**Type:** Research
**Requested by:** model
**Report:** knowledge/research/ynab-transfer-in-split-subtransaction-2026-06-13.md
**Review:** PASS (iteration 1)
**Summary:**
- Eine Transfer-Split-Zeile wird über `payee_id` (die Transfer-Payee des Ziel-Kontos) kodiert.
  `SaveSubTransaction` hat **kein** `transfer_account_id`/`transfer_payee_id` — nur
  `amount`/`payee_id`/`payee_name`/`category_id`/`memo` (verifiziert gegen die offizielle OpenAPI-Spec).
- Transfer-Payee-id via `GET /payees` ermitteln (Payee, dessen `transfer_account_id` == Bargeld-Konto-id);
  **nicht** auf `payee_name` verlassen. Gegenbuchung aufs Bargeld-Konto wird automatisch erzeugt (Inferenz, Sandbox-Test schließt die Restunsicherheit).
- Milliunits, Outflow negativ, Subtransactions müssen auf den Parent-Betrag summieren; `import_id` nur auf dem Parent.

---

## 2026-06-13 -- Modeling / Captured: ynab-001 - Split mit Transfer-Anteil (Barabhebung/Cashback)

**Type:** Modeling / Capture
**BC:** ynab-sync
**Filed to:** backlog
**Summary:** Barabhebung-an-der-Kasse-Fall: eine Comdirect-Buchung (€217) in einen Split
zerlegen, bei dem eine Zeile ein **Transfer auf ein YNAB-Konto** (Bargeld) ist und der Rest
eine Kategorie — ~80 % sind genau dieser Cashback-Fall. Tech-Stand im Code verifiziert:
Split-Backend + Split-Push existieren, aber `TransactionSplit` ist kategorie-only, kein
Transfer-Push, keine Split-UI. Kern ist eine Domain-Entscheidung (Split-Zeile = Kategorie
ODER Transfer) plus eine offene YNAB-API-Frage (Transfer in Subtransaction). UX mit Roman
abgestimmt: Cashback-Shortcut zuerst + generischer Editor, Transfer-Ziel = bestehendes Konto.

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
