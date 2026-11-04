---
id: design-system-w7c2h
title: "Fokus ohne Kasten: Tabs zeigen den Tastatur-Fokus als helle Fläche, Filter-Chips als Ring direkt auf dem Rand"
status: doing
type: bug
context: design-system
created: 2026-11-04
completed:
depends_on: [design-system-k4f9t]
blocks: []
tags: [fokus, tastatur, chipbar, tabs, a11y]
related_adrs: []
related_research: []
prior_art: [design-system-k4f9t, design-system-bz0gn]
---

## Why
Nach design-system-k4f9t (Roman, 2026-11-04, Screenshots): Der Ring am Tab liegt zwar innen, ist aber weiter ein Kasten, und je nach Klickfolge sieht der Tab verschieden aus. Ein aktiver Filter-Chip mit Fokus zeigt zwei Linien (teal Rand plus Ring mit 2 px Abstand). Roman: "Ich will bei diesen Tabs nur den unteren Strich. Wenn man Fokus markiert, muss man ihn irgendwie anders markieren."

## What
Entscheidung von Roman (2026-11-04):
1. **Tabs** (`src/Client/DesignSystem/Tabs.fs`, `src/Client/styles.css`): Die Auswahl bleibt allein der teal Strich unten. Der Tastatur-Fokus (`:focus-visible`) zeigt **keinen Rahmen**, sondern eine helle Fläche (Token `surface-hover`, `--color-surface-hover`) hinter dem Tab. Nach einem Mausklick erscheint weder Rahmen noch Fläche. Die Regel `[role="tab"]:focus-visible { outline-offset: -2px }` aus k4f9t wird dadurch ersetzt.
2. **Filter-Chips** (`src/Client/DesignSystem/ChipBar.fs`, ggf. `styles.css`): Der Fokus liegt als Ring **direkt auf dem Rand** der Pille, ohne Abstand (`outline-offset: 0` oder gleichwertig). Ein aktiver Chip mit Fokus zeigt eine dickere teal Linie, nicht zwei Linien. Die Rundung aus k4f9t bleibt.
3. **Unverändert:** Links und Rechts als Seiten-Kürzel ohne Fokus auf dem Tab zeigen nichts zusätzlich (budgeting-c5w2n). Der globale Ring `*:focus-visible` für alle anderen Elemente bleibt.
4. Der Styleguide (`standards/frontend/styleguide.md`) nennt die Fokus-Markierung für Tabs und Chips in einem Satz, damit sie nicht wieder als Kasten zurückkommt, und `/styleguide` zeigt sie.

## Acceptance criteria
- [ ] `[layout]` Der bestehende Test aus design-system-k4f9t (`scripts/budget-page.test.mjs`, "Fokus-Ring am PC: …") wird auf die neue Markierung umgestellt, bleibt ein einziger Test und ist vor dem Fix rot: Tab "Geld" per Tastatur fokussiert → `outline-style` ist `none` und der berechnete Hintergrund unterscheidet sich vom nicht fokussierten Tab; Chip "Überzogen" aktiv und per Tastatur fokussiert → Ring sichtbar, Rundung wie die Pille, `outline-offset` 0 px (kein Abstand zum Rand); nach einem Mausklick weder Ring noch Fläche.
- [ ] Gegenprobe je Fix einzeln (Tab-Fläche, Chip-Offset): ohne den jeweiligen Fix ist der Test rot, im Outcome genannt.
- [ ] Bestehende Tests zu Tabs, ChipBar und Filterleiste bleiben grün, keine neuen Farben außer vorhandenen Tokens (`guard:tokens`, `guard:classes`).
- [ ] Styleguide-Satz und `/styleguide` zeigen die Fokus-Markierung für Tabs und Chips.
- [ ] Tab-Fokus wirkt ruhig, nur der Strich markiert die Auswahl; der aktive Chip mit Fokus zeigt eine Linie. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Erfasst, verfeinert und promotet am 2026-11-04 in einem Zug; Entscheidung von Roman per Auswahl (Tab: helle Fläche, Chip: Ring direkt auf dem Rand). Roman hatte zuerst auf die Produktion geschaut, wo k4f9t noch nicht deployt ist; die Screenshots dieser Task stammen vom lokalen Stand mit k4f9t.

Tailwind-Utilities liegen in `@layer utilities` und verlieren gegen die ungelayerte globale Regel `*:focus-visible` (Befund aus k4f9t); Fokus-Regeln, die sie überschreiben müssen, gehören deshalb ungelayert in `styles.css`.
