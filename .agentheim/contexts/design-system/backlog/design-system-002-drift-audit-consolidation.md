---
id: design-system-002
title: Drift-Audit + Konsolidierung des View-Codes auf den Styleguide
status: backlog              # backlog | todo | doing | done
type: refactor               # feature | bug | refactor | chore | spike | decision
context: design-system
created: 2026-06-13
completed:
commit:
depends_on: [design-system-001]
blocks: []
tags: [styleguide, design-system, refactor, drift, tokens, consolidation, frontend]
related_adrs: [0004, 0005]
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
