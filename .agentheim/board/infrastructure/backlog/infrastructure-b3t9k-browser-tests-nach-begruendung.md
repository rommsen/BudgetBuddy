---
id: infrastructure-b3t9k
title: "Browser-Tests nach Begründung statt fester Obergrenze: jeder Test trägt eine Kennung, je Nutzerfluss höchstens ein Happy Path"
status: backlog
type: chore
context: infrastructure
created: 2026-11-03
completed:
depends_on: []
blocks: [budgeting-h7q4v]
tags: [tests, browser, doktrin]
related_adrs: []
related_research: []
prior_art: [infrastructure-t5m2b]
---

## Why
Nach infrastructure-t5m2b steht `MAX_BROWSER_TESTS` in `scripts/browser-usage.mjs` auf 116. Die Zahl ist willkürlich: neue Features brauchen begründet neue Browser-Tests, und bei jeder Feature-Task kommt die Frage wieder, ob die Grenze steigen darf. Roman (2026-11-03): "Ich würde gerne irgendwie eine sinnvolle Heuristik finden, wann es gut ist, einen Browser-Test zu schreiben. [...] Und diese Frage soll nicht jedes Mal wiederkommen."

## What
Vorschlag, in der Verfeinerung zu prüfen:
- Jeder Browser-Test trägt eine Kennung seiner Begründung aus Doktrin Punkt 5: `layout`, `fokus`, `tastatur`, `scroll`, `fable` (gilt nur im Kompilat) oder `flow:<name>` (Happy Path eines Nutzerflusses).
- Ein Wächter in `npm run test:scripts` prüft: jeder gezählte Test hat genau eine gültige Kennung, je `flow:<name>` gibt es höchstens einen Test.
- Die feste Zahl entfällt oder wird zur weichen Warnschwelle; die Gesamtzahl folgt aus den Kennungen.
- `.claude/rules/test-doctrine.md` Punkt 5 beschreibt die Regel so, dass eine Feature-Task ohne Rückfrage weiß, welche Browser-Tests sie schreiben darf.

## Acceptance criteria
To be defined during refinement. Offen: Form der Kennung (Testname-Präfix oder Metadaten), Umgang mit den 116 bestehenden Tests (alle kennzeichnen in derselben Task), ob eine weiche Schwelle bleibt.

## Notes
Zählung und Hook heute: `scripts/browser-usage.mjs`, Gegenbeweise in `scripts/browser-test-budget.test.mjs`, Hook über `NODE_OPTIONS` und `BB_BROWSER_USAGE_OWNER`.
