---
id: ledger-hme9k
title: "Durchgehen und Detail: gestapelte Sheets und Rücksprünge"
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
Bestandsreview ledger 2026-11-19, Gruppe G2, von Roman zur Task gewählt. Im Alltag sichtbar: Payee oder Memo im offenen Detail des Durchgehens öffnen ein zweites Sheet; höchste Schwere hoch.

## What
Alle Befunde der Gruppe in Befundform, wörtlich aus dem Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`:

### G2 Durchgehen und Detail: gestapelte Sheets und Rücksprünge

**Kern:** Im Durchgehen und im Detail öffnen Editoren ein zweites Sheet über dem Detail oder ersetzen es ohne Übergang, und Schließen führt nicht dorthin zurück, wo man herkam (§7.15, §7.8).

**Höchste Schwere:** hoch · **Befunde:** 4 · **BCs:** ledger

- src/Client/Components/Ledger/View.fs:5437 | standards/frontend/styleguide.md §7.15 und §7.1 Punkt 4 (keine gestapelten Sheets), §7.8 Punkt 6 | hoch | `reviewEditors` nur bei `not review.DetailShown` rendern (wie `CategoryPicker.sheet` Zeile 5290); heute öffnet „Payee“ oder „Memo“ im offenen Detail des Durchgehens ein zweites Sheet (am PC zweiten Dialog, `payeeDialog` auch in `detailPanelView`) über dem Editor im Detail.
- src/Client/Components/Ledger/View.fs:2271 | styleguide §7.15 (Auswahl ersetzt den Inhalt), §7.10 (Animation nur beim Öffnen) | mittel | Regel-Formular als Inhalt von `detailSheetView` statt `ruleFormSheet` als eigenes Sheet; heute verschwindet das Detail-Sheet ohne Übergang, ein neues fährt ein, beim Zurück fährt das Detail erneut ein (Zweig `ruleFormSheet None` hier nie erreicht).
- src/Client/Components/Ledger/View.fs:2137 | styleguide §7.8 Punkt 6, §7.15 | niedrig | × im Kopf des Split-Editors im Panel zurück ins Detail (`CloseSplitEditor`) wie im Sheet (2198); heute schließt es am PC das ganze Detail.
- src/Client/Components/Ledger/State.fs:1860 | elmish-state.md, doppelte Wege | niedrig | `CloseDetail` im Durchgehen setzt Felder von `closeDetail` von Hand und vergisst `HistoryOpenFor`; über `closeDetail`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-19, raw, unrefined. Bewusst groß; kleiner geschnitten wird im Refinement (`agentheim:modeling`).
