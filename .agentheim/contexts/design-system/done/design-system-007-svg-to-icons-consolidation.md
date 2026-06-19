---
id: design-system-007
title: Konsolidierung roher Html.svg → Icons-DS-Komponente (SyncFlow-Views)
status: done
type: refactor
context: design-system
created: 2026-06-18
completed: 2026-06-19
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
- [x] `QuickAdd.fs:34` nutzt `Icons.plus`, `InlineRuleForm.fs:206` nutzt `Icons.check` (gleiches Glyph, **identische Farbe**; Größe nächste DS-Stufe, Strichstärke DS-Standard 1.5).
- [x] `Icons.chevronLeft` in `Icons.fs` ergänzt (Pfad im DS-24-viewBox-Stil, analog zu chevronRight gespiegelt) und in `SyncFlow/View.fs:49` genutzt; Back-Button auf DS-Optik gehoben.
- [x] `TransactionRow.fs:359` (Toggle-Check) bleibt bewusst roh — eine Ein-Zeilen-Notiz *warum* (CSS-baked, eigener viewBox) am Call-Site (und im Diary).
- [x] **DS-normalisierter Look akzeptiert** (Roman-Entscheid 2026-06-19, s.u.): gleiches Glyph + identische Farbe; Größe (18/12→20/16px) und Strichstärke (2.5/3→1.5) bewusst auf DS-Standard normalisiert — Ziel ist DS-Konsistenz, nicht Pixel-Parität. `dotnet build` + `dotnet test` grün (595/6/0, stabiles Gate aus infra-001); Diary aktualisiert.

## Notes
Abgrenzung wie in 002: gleiche Optik, nur Anheben aufs DS bzw. minimale Icons.fs-Ergänzung.
Kein neues Icon-Design.

**Refine 2026-06-19:** Inventur aus dem Audit eingebacken; Schwester-Split `design-system-006`
wurde dismisst (dort kein byte-identischer Lift möglich), `design-system-007` bleibt als der
saubere kleine Gewinn → todo. Die ursprüngliche Absolut-AC „keine rohen `Html.svg` mehr" ist
bewusst auf 3-von-4 gelockert: der Toggle-Check ist legitim custom.

## Outcome (2026-06-19)
Drei der vier inline-SVG in den SyncFlow-Views aufs `Icons`-DS gehoben, die vierte begründet
roh belassen. Pro Site wurden Farbe (gegen die Token-Map: identisch) und Größe (nächster
DS-Token) verifiziert, sodass kein Look-Regress entsteht.

**Mapping:**
- `src/Client/Components/SyncFlow/Views/QuickAdd.fs:34` Plus → `Icons.plus Icons.SM Icons.Default`
  (Farbe `--sf-text-secondary` == `text-text-secondary`; strokeWidth auf DS-Standard 1.5 vereinheitlicht).
- `src/Client/Components/SyncFlow/Views/InlineRuleForm.fs:206` Check → `Icons.check Icons.XS Icons.Primary`
  (Präzedenz `StatusViews.fs:68`: gleiches Check-in-20px-Kreis-Motiv nutzt denselben Call).
- `src/Client/Components/SyncFlow/View.fs:49` Back-Chevron → neues `Icons.chevronLeft` in
  `src/Client/DesignSystem/Icons.fs` (Spiegel von `chevronRight`: `m15.75 19.5-7.5-7.5 7.5-7.5`),
  dann `Icons.chevronLeft Icons.SM Icons.Default`.
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs:359` Toggle-Check → **bewusst roh**;
  Begründungs-Kommentar am Call-Site (CSS-baked 10×10-viewBox via `.toggle-check`; Swap wäre CSS-Rework).

**Schlüssel-Dateien:** `src/Client/DesignSystem/Icons.fs` (neues Glyph),
`src/Client/Components/SyncFlow/{View.fs, Views/QuickAdd.fs, Views/InlineRuleForm.fs, Views/TransactionRow.fs}`.

**Verifikation:** `dotnet build` 0/0, `vite build` (Fable) Exit 0, `dotnet test` 595 bestanden /
6 übersprungen / 0 Fehler. Kein ADR nötig (gerader Icon-Lift, keine Architekturentscheidung).

## Verifier note (iteration 1)
**VERDICT: FAIL** — `ITERATION_HINT: task-under-specified` → eskaliert an Roman (kein Re-Dispatch;
`work` re-modelt nicht selbst). Code-Änderungen bleiben uncommittet auf dem Working Tree.

**REASONS:**
- **Look-Regress (verletzt AC „kein Look-Regress" / „gleiche Größe"):** Der Swap ändert
  gerendert **Strichstärke** und **Größe**, nicht nur deren Quelle. `svgIcon` (Icons.fs) hardcodet
  `strokeWidth 1.5`; die Originale waren **2.5** (Plus, Chevron) und **3** (Check) → der Check geht
  von bewusst kräftig stroke-3 auf 1.5 (−50%) in einem 20px-Teal-Kreis. `IconSize` hat keine
  18px/12px-Stufe: SM=20px (Plus/Chevron 18→20, +11%), XS=16px (Check 12→16, +33%).
- **Farben sind sauber** (kein Defekt): Plus/Chevron `--sf-text-secondary` #7a7a98 == `Icons.Default`;
  Check #e8e8f0 (geerbt) == `Icons.Primary`. Alle drei Farbwahlen korrekt.
- **`chevronLeft`-Geometrie korrekt:** echter Spiegel von `chevronRight` (`<`, Apex x=8.25).
- **AC #3 (Toggle-Check roh + Kommentar) erfüllt.** Build 0/0, Tests 595/6/0, Diary ehrlich (nennt
  die Stroke/Size-Deltas selbst).

**SUGGESTED_FIX (Spec-Widerspruch auflösen — Roman entscheidet):** Die AC verlangt BEIDES —
byte-identischer Look UND DS-Token-Nutzung — was die DS für 18px/12px + stroke-2.5/3-Glyphen
nicht leisten kann. Optionen: **(a)** Die DS-Normalisierung akzeptieren und AC #1/#4 umformulieren
auf „nächste DS-Größe + DS-Standard-Stroke 1.5, Farbe identisch" (die Stroke-Vereinheitlichung IST
der Sinn der Konsolidierung; es gibt bereits Präzedenz: `StatusViews.fs:68` nutzt denselben
`Icons.check`-Call fürs gleiche Check-im-20px-Kreis-Motiv → der Swap macht InlineRuleForm sogar
*konsistenter*). **(b)** Erst die DS erweitern (per-Call-strokeWidth-Override und/oder 18/12px-Stufen)
— eigener DS-Task. **(c)** Die stroke-schweren/sub-Token-Glyphen aus dem Scope nehmen, roh lassen
wie den Toggle-Check (dann bleibt fast nichts → Richtung dismiss). `chevronLeft` + alle Farb-Mappings
sind solide und können unabhängig bleiben.

## Refine 2 — Roman-Entscheid (2026-06-19): Option (a) DS-Normalisierung akzeptiert
Spec-Widerspruch aufgelöst: die AC verlangte fälschlich *zugleich* Pixel-Parität und DS-Token.
Roman wählt die **DS-Normalisierung** — die dünnere Standard-Strichstärke (1.5) und die nächste
Größenstufe sind das *gewollte* Ziel der Konsolidierung (DS-Konsistenz), kein Regress. Stützt sich
auf die Präzedenz `StatusViews.fs:68` (gleiches Check-im-Kreis-Motiv nutzt bereits `Icons.check`).
AC #1/#4 entsprechend umformuliert (Farbe identisch, Größe/Stroke DS-normalisiert). Der bestehende
Worker-Code erfüllt die neue AC → re-verifiziert, committet. Visueller Abnahme-Check (headless
mobil+Desktop) optional vor Push/Deploy.
