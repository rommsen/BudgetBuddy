---
id: design-system-007
title: Konsolidierung roher Html.svg → Icons-DS-Komponente (SyncFlow-Views)
status: todo
type: refactor
context: design-system
created: 2026-06-18
completed:
commit:
depends_on: [design-system-001]
blocks: []
tags: [frontend, design-system, refactor, drift, icons, consolidation]
related_adrs: []
related_research: []
prior_art: [design-system-002]
---

## Why
Split aus dem Drift-Audit `design-system-002`. Das Audit fand 4 rohe `Html.svg`/inline-SVG
in den View-Schichten:

- `SyncFlow/View.fs` (1), `SyncFlow/Views/QuickAdd.fs` (1), `InlineRuleForm.fs` (1),
  `TransactionRow.fs` (1).

Der Styleguide (§5, Icons-Zeile) definiert eigene rohe `<svg>` explizit als Drift:
„fehlt ein Icon, hier ergänzen" — also im `Icons`-DS-Modul. Diese vier Stellen wurden in
002 ausgegliedert, weil jede einzeln geprüft werden muss: deckt ein bestehendes
`Icons.*`-Glyph das SVG ab, oder fehlt das Icon und muss erst in `Icons.fs` ergänzt werden
(dann ist es kein reiner Lift mehr, sondern eine DS-Erweiterung).

## What
1. Jedes inline-SVG identifizieren und gegen das `Icons`-Inventar matchen.
2. Wo ein passendes `Icons.*`-Glyph existiert: Call-Site darauf umstellen (Size/Color-Token).
3. Wo kein Glyph existiert: Icon in `Icons.fs` ergänzen (Pfad 1:1 übernehmen), dann nutzen.
4. Tests grün; mobil + Desktop stichprobenhaft visuell bestätigen.

## Inventur (Refine-Audit 2026-06-19)
Klassifikations-Audit (Sub-Agent, read-only) über die vier Call-Sites — konkrete Zuordnung:

| Datei:Zeile | SVG (Glyph) | Ziel |
|---|---|---|
| `SyncFlow/Views/QuickAdd.fs:34` | Plus `M12 5v14M5 12h14` | **bestehend** → `Icons.plus` (sauberer 1:1-Swap) |
| `SyncFlow/Views/InlineRuleForm.fs:206` | Check `M20 6L9 17l-5-5` | **bestehend** → `Icons.check` (sauberer 1:1-Swap) |
| `SyncFlow/View.fs:49` | Chevron-Left `M15 18l-6-6 6-6` (Back-Button) | **fehlt** → `Icons.chevronLeft` in `Icons.fs` ergänzen (DS hat Down/Up/Right, kein Left), dann nutzen |
| `SyncFlow/Views/TransactionRow.fs:359` | Check `M2 5l2.5 2.5L8 3` (Toggle-Track, 10×10 viewBox) | **bewusst custom lassen** — micro-Glyph mit eigenem viewBox, von `.toggle-check`-CSS positioniert/dimensioniert; ein Swap auf `Icons.check` (0..24 viewBox + Tailwind-Sizing) wäre CSS-Rework, kein 1:1-Lift |

Ertrag: 3 von 4 actionable (2 Swaps + 1 neues Icon), 1 begründet ausgenommen.

## Acceptance criteria
- [ ] `QuickAdd.fs:34` nutzt `Icons.plus`, `InlineRuleForm.fs:206` nutzt `Icons.check` (gleiche Größe/Farbe via Size/Color-Token).
- [ ] `Icons.chevronLeft` in `Icons.fs` ergänzt (Pfad im DS-24-viewBox-Stil, analog zu chevronRight gespiegelt) und in `SyncFlow/View.fs:49` genutzt; Back-Button optisch unverändert.
- [ ] `TransactionRow.fs:359` (Toggle-Check) bleibt bewusst roh — eine Ein-Zeilen-Notiz *warum* (CSS-baked, eigener viewBox) am Call-Site oder im Diary.
- [ ] Kein Look-Regress (gleiches Glyph/Größe/Farbe, mobil + Desktop stichprobenhaft); `dotnet build` + `dotnet test` grün (stabiles Gate aus infra-001); Diary aktualisiert.

## Notes
Abgrenzung wie in 002: gleiche Optik, nur Anheben aufs DS bzw. minimale Icons.fs-Ergänzung.
Kein neues Icon-Design.

**Refine 2026-06-19:** Inventur aus dem Audit eingebacken; Schwester-Split `design-system-006`
wurde dismisst (dort kein byte-identischer Lift möglich), `design-system-007` bleibt als der
saubere kleine Gewinn → todo. Die ursprüngliche Absolut-AC „keine rohen `Html.svg` mehr" ist
bewusst auf 3-von-4 gelockert: der Toggle-Check ist legitim custom.
