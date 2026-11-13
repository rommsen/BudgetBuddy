---
id: infrastructure-ewasy
title: README-Delta-Gate meldet alten Wortlaut, der nach einem replace anderswo in der README stehen bleibt
status: backlog
type: chore
context: infrastructure
created: 2026-11-08
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Ein README_DELTA ersetzt einen Bullet, aber andere Bullets, `## Ereignisse`, `## Key commands`, `## Key events`,
Open questions oder Verweise nennen dieselbe Regel noch im alten Wortlaut. Der Verifier lehnt ab. Das kam im
work-Lauf 2026-11-08 viermal vor (ledger-qvjvs, budgeting-e2z7v, ledger-at63w zweimal), vorher bei budgeting-eh8xe
und im Protokoll als "README-Bullets Push-Fehlschlag und Schreib-Sperre nennen dieselbe Regel und sind nicht
nachgezogen".

## What
`scripts/readme-delta-check.mjs` bekommt ein siebtes Kriterium: Begriffe in Backticks und Sätze in „…“, die ein
replace aus `expected` entfernt und die nicht im `body` stehen, dürfen sonst in der README nicht mehr vorkommen.
Sonst Exit 1 mit Fundstelle. Gegenbeweis in der zugehörigen `.test.mjs`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-08 aus dem `learn`-Lauf nach dem Review-Lauf e83f2d00..ad18f0c9, raw,
unrefined. Bis das Kriterium gebaut ist, gilt die geschärfte Prosa-Regel in CLAUDE.md, Abschnitt README-Deltas.

Nachtrag 2026-11-13 (learn nach ledger-ay81j): Zwei Verifier-FAILs in Folge, obwohl der Worker beide Male
Vollständigkeit per grep meldete. Iteration 1: Bullet **Schreib-Sperre** nannte weiter „feste `import_id`“
(reiner Begriffstreffer, das Kriterium aus What hätte ihn gefunden). Iteration 2: Key command *PushErneutSenden*
sagte „trägt die Buchung eine `import_id`, geht sie mit“, der ersetzte Bullet hatte „mit der `import_id`, falls
die Buchung eine trägt“: umformuliert, kein gemeinsamer seltener Begriff. Folgerungen fürs Verfeinern:
- Begriffe, die auch im `body` stehen oder in der README häufig sind (etwa `import_id`), taugen nicht als
  Treffer. Nur Begriffe, die nach dem Delta seltener werden, sonst Fehlalarme.
- Ergänzend eine Liste statt Exit 1: jeder Key command, Key event und `## Ereignisse`-Eintrag, dessen Name
  (kursiv oder in Backticks) im `expected` oder `body` eines replace vorkommt, wird mit Zeilennummer als
  „ganz nachlesen“ ausgegeben. Der Worker nennt im OUTCOME je Eintrag „passt“ oder „nachgezogen“.
- Damit wird die Task vom Kann- zum Alltagsfall: drei Verifier-Iterationen kosten mehr als der Wächter.
