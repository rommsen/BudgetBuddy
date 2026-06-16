---
id: cat-001
title: Regel-Präzedenz per Hoch/Runter umsortierbar machen (UI)
status: done
type: feature
context: categorization
created: 2026-06-16
completed: 2026-06-16
commit:
depends_on: [design-system-001]
blocks: []
tags: [frontend, rules, ui, priority]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Roman hat Regeln, die **zu eager** matchen und vor spezifischeren Regeln greifen. Das
Pattern lässt sich nicht immer anders definieren, um das zu umgehen — der richtige Hebel
ist **Präzedenz über Reihenfolge**: die zu eager Regel soll *über die Reihenfolge
verlieren*. Die Engine entscheidet bereits „erste passende, aktivierte Regel gewinnt"
(`priority DESC`), aber es gibt **kein UI**, um die Reihenfolge zu ändern — also kann eine
über-eifrige Regel aktuell nicht zurückgestuft werden.

## What
Reorder-Bedienelemente (▲/▼ pro Regel-Zeile) in der Regel-Verwaltung. Eine Verschiebung
tauscht die Regel mit ihrer Nachbarin und persistiert die **komplette neue Reihenfolge**
über die *bereits existierende* `reorderRules: RuleId list`-API. Die Liste wird in
Präzedenz-Reihenfolge angezeigt (oben = höchste Priority = gewinnt zuerst), und diese
Semantik wird für den Nutzer sichtbar gemacht.

**Reiner Frontend-Task** — das Backend ist End-to-End vorhanden (s. Notes).

## Acceptance criteria
- [x] Jede Regel in der Liste hat ▲/▼-Buttons (DS `Button`/`Icons`, kein Inline-Feliz-Drift), die sie eine Position nach oben/unten verschieben.
- [x] ▲ ist bei der obersten Regel deaktiviert, ▼ bei der untersten.
- [x] Eine Verschiebung sendet die **vollständige** neue Reihenfolge an `Api.rules.reorderRules` (RuleId list); nach Erfolg spiegelt die Liste die Server-Reihenfolge (`priority DESC`).
- [x] Die Liste ist sichtbar nach Präzedenz sortiert und es ist erkennbar, dass die **oberste passende** Regel gewinnt (Rang-Index `#N` + Hinweiszeile).
- [x] Bei Fehler: Toast + die Liste bleibt konsistent (Reload des Server-Stands, kein „halb verschobener" Zustand).
- [x] Mobil bedienbar — DS `Button` Small mit `min-h-[36px]`-Touch-Target, keine Drag-Geste nötig.
- [x] Regressionstest: der Reorder-Dispatch erzeugt die korrekte RuleId-Reihenfolge inkl. Ränder (oberste/unterste Regel verschieben); bestehende Rules-Tests bleiben grün.

## Outcome
Reines Frontend umgesetzt — das Backend (`reorderRules` → `updatePriorities`) blieb unberührt.

Kern: eine **pure** Funktion `reorderedIds (direction) (ruleId) (rules)` in
`src/Client/Components/Rules/Types.fs` berechnet die neue volle `RuleId list` in
Präzedenz-Reihenfolge (oben = höchste Priority). Sie ist No-op an den Rändern (oberste hoch /
unterste runter) und bei unbekannter id, und gibt immer eine Permutation der Eingabe zurück —
direkt für `reorderRules` verwendbar. Die Berechnung wurde bewusst als reine Funktion
herausgezogen, weil Reducer-Pfade, die `Client.Api` (Fable.Remoting-Proxy) berühren, unter .NET
nicht testbar sind.

Reducer (`State.fs`): `MoveRule` sortiert die Liste optimistisch um und persistiert die volle
Reihenfolge; `RulesReordered (Error …)` lädt neu + zeigt Toast (kein halb-verschobener Zustand).
View (`View.fs`): ▲/▼ via DS `Button.view` (`IsDisabled` an den Rändern), Rang-Index `#N` pro
Zeile + Hinweiszeile „erste passende, aktive Regel gewinnt". Neues `chevronUp`-Icon in
`Icons.fs` (es gab nur `chevronDown`).

Die ▲/▼-Buttons nutzen DS `Button.Small`, dessen `min-h-[36px]` (mobil; `md:min-h-0`) das
Touch-Target trägt — die ClassName tunt nur das horizontale Padding (`!px-1.5 !py-1`) und
überschreibt **nicht** mehr die Mindesthöhe. Der Rang-Index-Span nutzt das Typography-Token
`text-xs` (kleinste Token-Größe in `Tokens.FontSizes`) statt einer arbitrary px-Größe.

**Iteration-2-Fix (2026-06-16):** Zwei lokale Defekte aus der Verifier-Note (beide in
`View.fs`) behoben: (1) `!min-h-0` aus beiden Chevron-Button-ClassNames entfernt → der
36px-Mobile-Touch-Target von `Button.Small` überlebt jetzt tatsächlich (AC 6). (2) Rang-Index
von `text-[11px]` auf `text-xs` umgestellt (Design-Token-Konformität, `.claude/rules/design-tokens.md`).
Keine Test-Änderungen; Suite bleibt grün.

**Schlüsseldateien:**
- `src/Client/Components/Rules/Types.fs` — `MoveDirection`, `reorderedIds`, neue Msgs.
- `src/Client/Components/Rules/State.fs` — `MoveRule` / `RulesReordered` Reducer.
- `src/Client/Components/Rules/View.fs` — ▲/▼-UI, Rang-Index, Präzedenz-Hinweis.
- `src/Client/DesignSystem/Icons.fs` — `chevronUp`.
- `src/Tests/RulesReorderTests.fs` — 9 Reorder-Tests (inkl. Ränder, unbekannte id, Permutation).

**Verifikation:** `dotnet test` 586 passed / 6 skipped / 0 failed (+9 neu); `npm run build`
(Fable+Vite) ✓. (Iteration 2 re-verifiziert: identisch grün nach den View.fs-Fixes.)

## Notes
**Backend ist komplett — am Server NICHTS bauen:**
- `Persistence.Rules.updatePriorities` (`src/Server/Persistence.fs:384`) — schreibt `priority = len - i` transaktional aus einer geordneten `RuleId list`.
- `rulesApi.reorderRules` (`src/Server/Api.fs:552`) → ruft `updatePriorities`.
- Contract `RulesApi.reorderRules: RuleId list -> Async<RulesResult<unit>>` (`src/Shared/Api.fs:122`).
- Client-Proxy `Api.rules` (`src/Client/Api.fs`).

**Engine-Semantik:** `RulesEngine.classify` (`src/Server/RulesEngine.fs:200`) macht `List.tryFind`
über nach `priority DESC` geladene Regeln → **erste passende, aktivierte** Regel gewinnt.
„Oben in der Liste" = höchste Priority = gewinnt zuerst.

**UI lebt in `src/Client/Components/Rules/`** (Types.fs `Msg`, State.fs `update`, View.fs).
Heute existiert *keine* Reorder-`Msg` (nur Create/Edit/Delete/Toggle/Test/Import/Export).
Vorschlag: neue Msg `MoveRuleUp of RuleId` / `MoveRuleDown of RuleId` → neue Reihenfolge
berechnen → `Cmd.OfAsync` auf `reorderRules` → bei Erfolg `LoadRules` (oder optimistic).
Neue Regeln bekommen aktuell `Priority = max+1` (`State.fs:219`) → landen oben; nach Reorder
wird ohnehin durchnummeriert.

**Drag & Drop bewusst NICHT** (Roman: Hoch/Runter; mobil-sicher). Falls später gewünscht:
eigener Folge-Task.

**Gate:** UI-Task → hängt an `design-system-001` (done + reviewt, nicht blockierend). DS-Komponenten nutzen.

## Verifier note (iteration 1)
**VERDICT: FAIL** — zwei konkrete Defekte, beide lokal in `src/Client/Components/Rules/View.fs`. Der Rest ist sauber (Suite grün: 586 passed / 6 skipped / 0 failed; `reorderedIds` + 9 Tests korrekt; Reducer/Reload/Toast korrekt; Rang-Index + Hinweiszeile vorhanden; keine Protocol/Index/Git-Eingriffe).

**REASONS:**
- **AC 6 (Mobil) verletzt:** Beide ▲/▼-Buttons setzen `ClassName = Some "!min-h-0 !px-1.5 !py-1"`. Das `!min-h-0` überschreibt per Tailwind-`!important` das `min-h-[36px]` von `Button.Small` (`src/Client/DesignSystem/Button.fs:64`) → Touch-Target kollabiert auf ~20-24px (XS-Icon + `!py-1`) auf **allen** Viewports inkl. Mobile. Das vom AC geforderte 36px-Touch-Target ist negiert; das Outcome behauptet fälschlich „`min-h-[36px]`-Touch-Target".
- **Design-Token-Verletzung:** Der Rang-Index-Span nutzt `text-[11px]` (arbitrary font size). `.claude/rules/design-tokens.md` verbietet `text-[Npx]` explizit (umgeht die Typography-Token-Scale; Grep-Check `text-\[[0-9]`).

**SUGGESTED_FIX:** `!min-h-0` aus der ClassName der Chevron-Buttons entfernen (das `min-h-[36px]` von `Button.Small` fürs Mobile-Touch-Target behalten; bei Bedarf nur das horizontale Padding tunen). `text-[11px]` am Rang-Index durch eine vorhandene Typography-Token-Klasse ersetzen (z.B. `text-xs` bzw. ein Meta-Token aus dem Design System) statt arbitrary px.

**ITERATION_HINT:** likely-fixable — keine Test-Änderungen nötig, nur die zwei Stellen in `View.fs`.
