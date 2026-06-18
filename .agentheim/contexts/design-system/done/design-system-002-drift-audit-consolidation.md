---
id: design-system-002
title: Drift-Audit + Konsolidierung des View-Codes auf den Styleguide
status: done                 # backlog | todo | doing | done
type: refactor               # feature | bug | refactor | chore | spike | decision
context: design-system
created: 2026-06-13
completed: 2026-06-18
commit: 9e9526a
depends_on: [design-system-001]
blocks: []
tags: [styleguide, design-system, refactor, drift, tokens, consolidation, frontend]
related_adrs: [0004, 0005, 0009]
related_research: []
prior_art: []
---

## Why
Sobald der Styleguide (`design-system-001`) als kanonische Wahrheit steht, lässt sich der
reale View-Code dagegen messen. Erfahrungsgemäß ist über die Zeit **Drift** entstanden:
hartkodierte Tailwind-Klassen statt Tokens, inline-Feliz statt DS-Komponenten, lokale
Abweichungen von den Sheet-/Picker-Mustern. Roman will das in einem Zug konsolidieren, damit
der Styleguide nicht nur Doku ist, sondern der Code ihm auch entspricht
(Wahl 2026-06-13: "Kodifizieren + Konsolidieren").

## What
Zwei Schritte in einem Task:
1. **Audit** — alle `View.fs`/View-Schichten der fachlichen BCs gegen den Styleguide
   scannen und eine **Drift-Inventur** erstellen (hartkodierte Farben → Token, inline-Feliz
   → DS-Komponente, Pattern-Abweichungen). Mit Serena (`search_for_pattern`), nicht roh.
2. **Konsolidieren** — den offensichtlichen, risikoarmen Drift auf DS-Komponenten/Tokens
   refactoren. Verhalten bleibt gleich (reines Anheben aufs DS), kein UI-Redesign.

## Acceptance criteria
- [ ] Drift-Inventur als kurze Liste (Datei → Befund → Ziel-Token/-Komponente) im Task
      oder in `diary/development.md` festgehalten.
- [ ] Hartkodierte Farben/Glows, die ein `Tokens.fs`-Token haben, sind ersetzt.
- [ ] Inline-Feliz, das eine bestehende DS-Komponente 1:1 abdeckt, nutzt die Komponente.
- [ ] Kein Verhaltens-/Layout-Regress: vorhandene Tests bleiben grün; visuell stichprobenhaft
      bestätigt (mobil + Desktop).
- [ ] `dotnet build` + `dotnet test` grün; **qa-milestone-reviewer** über die Änderungen;
      Diary aktualisiert.

## Notes
**Readiness: backlog** — zwei Dinge vor PROMOTE klären:
1. **Abhängigkeit:** hängt hart an `design-system-001` (Styleguide muss done + reviewt sein —
   sonst gibt es keine kanonische Wahrheit, gegen die man konsolidiert).
2. **Sizing:** Der echte Umfang wird erst nach dem Audit (Schritt 1) sichtbar. Wenn der Drift
   groß ist, diesen Task in mehrere Refactor-Tasks pro BC/Komponente splitten, statt einen
   Mega-Refactor zu fahren. Vor PROMOTE entscheiden: ein Task oder Split.

**Abgrenzung:** Kein neues Design, keine neuen Komponenten — nur den Code aufs bestehende DS
heben. Neue UI-Bedürfnisse gehören als eigene Tasks in den jeweiligen fachlichen BC (und
hängen dann am Gate `design-system-001`).

**Promote 2026-06-18:** Gate erfüllt — `design-system-001` ist done + akzeptiert (war beim
Schreiben am 13.06. noch offen). Schnitt-Entscheidung (Roman): als **ein Task** fahren — das
Audit ist Schritt 1, die Konsolidierung folgt darin; erst **nach** dem Audit pro BC/Komponente
splitten, falls der Drift groß ist (Worker darf bouncen, statt einen Mega-Refactor zu fahren).
→ todo.

## Outcome (2026-06-18)
Audit durchgeführt (Grep-Checks aus `.claude/rules/design-tokens.md` über den View-Layer,
DesignSystem-Quelle + `/styleguide`-Galerie ausgenommen). **Befund: wenig Token-, viel
Komponenten-Drift.**

**Drift-Inventur (Datei → Befund → Ziel) — vollständig, per `search_for_pattern`/grep über `src/Client/`:**
- Standard-Tailwind-Farben: **0** · inline `style.color/fontSize`: **0** → sauber.
- `text-[#0a0a0f]` (dunkle Schrift auf Neon): **6×** `SyncFlow/Views/StatusViews.fs`
  (L42/67/157/168/243/247) + **DS-intern** `Badge.fs` (Filled-Variants L68-74, Count L231)
  → **neues `Colors.onNeon`/`onNeonMuted`**.
- `text-[10px]` (Mikro-Labels): **fachliche Views (13×):** `StatusViews.fs` (L264/271/278),
  `Settings/View.fs` (L563), `TransactionRow.fs` (L121/126/139/144), `Rules/View.fs`
  (L29/38/47/67); **DS-komponenten-intern (3×):** `Badge.fs` (Count L231 **+** `sizeToClass`
  Small-Branch L91), `Navigation.fs` (L131, Nav-Label), `Stats.fs` (L75, Compact-Caption)
  → **neues `FontSizes.micro`**.
- `text-[11px]` (Mikro-Labels): **1×** `Rules/View.fs` (L130) → **neues `FontSizes.microPlus`**.
- Rohe `Html.button` (~34) / rohe `Html.svg` (4): zu groß/riskant für reinen Lift →
  **gesplittet** (s. u.).

**Konsolidiert (risikoarm, CSS-String byte-identisch → kein visueller Regress):**
- `Tokens.fs`: `Colors.onNeon`/`onNeonMuted`, `FontSizes.micro`/`microPlus` (ADR 0009).
- Fachliche Views auf Tokens gehoben: `StatusViews.fs`, `TransactionRow.fs`,
  `Settings/View.fs`, `Rules/View.fs`.
- DS-Komponenten auf Tokens gehoben: `Badge.fs` (Filled-Variants, Count **und**
  `sizeToClass`/Small), `Navigation.fs` (Nav-Label), `Stats.fs` (Compact-Caption).
- **Re-Audit bestätigt 0 token-gedeckte Residuen** (`text-[10px]`/`text-[11px]`/
  `text-[#0a0a0f]` an Call-Sites = leer; verbleibende Literale nur an der Token-
  Definitionsstelle `Tokens.fs` + bewusst roh in der `/styleguide`-Galerie).
- **Bewusst NICHT angefasst (out of scope):** `Loading.fs` `bg-[#0a0a0f]/80`
  (Hintergrund-Overlay, andere Rolle als der `onNeon`-Vordergrund), die `neon*Dim`-Tokens
  (vorbestehende Hex-Definitionen).

**Gesplittet (sanktionierter Valve, kein Bounce):**
- `design-system-006` — rohe `Html.button` → `Button` (custom Click-Commit/Swipe/Chip-
  Elemente pro Fall prüfen, ADR 0005 nicht anfassen).
- `design-system-007` — rohe `Html.svg` → `Icons` (ggf. Icons.fs ergänzen).

**Verifikation:** `dotnet build` ✅ (Solution) · `dotnet test` 595 passed / 6 skipped /
0 failed (entspricht dem stabilen Gate aus infra-001). Diary + BC-README aktualisiert.

**Key files:** `src/Client/DesignSystem/Tokens.fs`, `…/DesignSystem/Badge.fs`,
`…/DesignSystem/Navigation.fs`, `…/DesignSystem/Stats.fs`,
`src/Client/Components/SyncFlow/Views/StatusViews.fs`,
`…/SyncFlow/Views/TransactionRow.fs`, `…/Settings/View.fs`, `…/Rules/View.fs`,
`.agentheim/knowledge/decisions/0009-onneon-foreground-and-micro-font-tokens.md`.

## Iteration 2 (2026-06-18)
Verifier-Blocker behoben: die drei übersehenen DS-komponenten-internen `text-[10px]`-Stellen
(`Badge.fs:91` `sizeToClass`/Small, `Navigation.fs:131` Nav-Label, `Stats.fs:75`
Compact-Caption) auf `FontSizes.micro` gehoben (byte-identisch). Vollständiges Re-Audit über
`src/Client/` bestätigt **0 token-gedeckte Residuen**. Inventur oben entsprechend nachgezogen
(jetzt wahrheitsgetreu: `text-[10px]` = 13 Views + 3 DS-intern; `text-[11px]` = 1). Build ✅,
Tests 595/6/0 unverändert. Splits 006/007 bleiben wie sie sind.

## Verifier note (iteration 1)
**VERDICT: FAIL** — `ITERATION_HINT: likely-fixable`. AC#1/#3/#4/#5 + ADR 0009 + die
Splits (006/007) sind sauber; einziger Blocker ist eine unvollständige Token-Konsolidierung
(AC#2):

**REASONS:**
- AC#2 unvollständig: `src/Client/DesignSystem/Badge.fs:91` (`sizeToClass`,
  `Small -> "text-[10px] px-1.5 py-0.5"`) nutzt noch rohes `text-[10px]`, obwohl das neu
  angelegte `FontSizes.micro`-Token es abdeckt — und das ist eine Datei, die der Worker
  editiert und teil-konsolidiert hat (er hat das `text-[10px]` in `count`/Zeile 228
  umgestellt, dieses hier übersehen). Die SUMMARY behauptet „voll konsolidiert" — für
  diese Stelle falsch.
- Die Audit-Inventur selbst ist unvollständig: sie listet nur „Badge.fs Count" für
  `text-[10px]`, die `sizeToClass`/Small-Stelle wurde nie erfasst (AC#1-Lücke in einer
  in-scope-Datei).
- Zwei weitere rohe `text-[10px]`-Mikro-Label-Stellen in DS-Komponenten mit vorhandenem
  Token: `src/Client/DesignSystem/Navigation.fs:131` und `src/Client/DesignSystem/Stats.fs:75`.
  DS-komponenten-intern (nicht fachliche Views) → weichere Lücke, aber nirgends in Inventur
  oder Split-Begründung erwähnt → Audit still unvollständig.

**SUGGESTED_FIX:** `text-[10px]` an `Badge.fs:91` durch `{FontSizes.micro}` ersetzen
(byte-identisch, null visueller Change). Navigation.fs:131 / Stats.fs:75 entweder ebenfalls
auf `FontSizes.micro` heben ODER eine Ein-Zeilen-Notiz in Inventur/ADR 0009 ergänzen, warum
DS-komponenten-interne Mikro-Größen bewusst roh bleiben. Danach Build + Tests erneut
(sollte 595/6/0 bleiben). Inventur in der Outcome-Sektion entsprechend nachziehen, damit
sie der Realität entspricht.
