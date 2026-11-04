---
id: design-system-w7c2h
title: "Fokus ohne Kasten: Tabs zeigen den Tastatur-Fokus als helle Fläche, Filter-Chips als Ring direkt auf dem Rand"
status: done
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

## Builder note (2026-11-04, Änderung der Entscheidung)

Roman: Tabs sollen bei Pfeiltasten immer gleich aussehen, egal ob vorher geklickt wurde. Deshalb **keine eigene Fokus-Markierung an Tabs**, weder Rahmen noch Fläche: Der fokussierte Tab ist bei dieser Leiste immer der ausgewählte (Pfeiltasten wählen sofort, roving tabindex), der teal Strich zeigt die Fokusposition also schon. Ersetzt Punkt 1 aus "What". Test: Tab "Geld" nach Mausklick plus Pfeiltasten sieht genauso aus wie ein ausgewählter Tab, der über das Seiten-Kürzel aus dem Suchfeld gewählt wurde (`outline-style` none, gleicher Hintergrund). Chips unverändert nach Punkt 2.

## Verifier note (iteration 1)

**REASONS:**
- Die Verschiebung der Polsterung in `ChipBar` ändert die Breite jedes aktiven Chips mit Entfernen-Knopf, entgegen dem Outcome. Vorher: Container `px-1` 4 + Label `pl-2.5` 10 + Text + Label `pr-2.5` 10 + `gap-1.5` 6 + `pl-0.5` 2 + × + `pr-2.5` 10 + Container 4. Nachher: 14 + Text + 14 + 6 + 2 + × + 14. `ChipBar.fs:36` setzt `px-3.5` auf beide Seiten des Labels, die 4 px des Containers saßen aber nur außen. Bei Chips mit "×" wächst der innere rechte Abstand des Labels von 10 auf 14 px, der Chip wird 4 px breiter und der Abstand Text bis "×" von 18 auf 22 px. Betroffen: Filterleiste im Kassenbuch (aktive Chips Konto, Kategorie, Zeitraum in `Components/Ledger/View.fs`) und die Karte in /styleguide. Chips ohne "×" (MultiSelect, Zu prüfen, Budget-Filter) behalten ihre Breite. Der Satz im Outcome "Die Chips sind durch die verschobene Polsterung gleich breit geblieben" stimmt für Chips mit "×" nicht. Unverlangter Seiteneffekt (Check 3).
- Alles andere passt: `./verify.sh` PASS, ein `[layout]`-Test nach der Builder note, rot gegen k4f9t, Gegenproben genannt; Tab-Regel trifft nur `Tabs.fs`; Fokus gleich Auswahl durch roving tabindex; Ring am "×" passt; Styleguide-Satz stimmt.

**SUGGESTED_FIX:** Äußere Polsterung der Pille bei 14 px lassen, ohne Chips mit "×" zu verbreitern, etwa Label `pl-3.5 pr-2.5`, wenn ein "×" folgt, und `px-3.5`, wenn keins folgt. Eine Breiten- oder Abstandsprüfung für einen aktiven Chip mit "×" ergänzen oder den Outcome-Satz korrigieren.

**ITERATION_HINT:** likely-fixable

## Outcome

Umgesetzt nach der geänderten Entscheidung von Roman (Builder note): Tabs bekommen keine eigene Fokus-Markierung, weder Rahmen noch helle Fläche. Chips wie ursprünglich entschieden.

- **Tabs** (`src/Client/styles.css`): `[role="tab"]:focus-visible { outline: none; }` ersetzt den inneren Ring aus design-system-k4f9t, ungelayert. Pfeiltasten wählen sofort (roving `tabindex`), der teal Strich zeigt Auswahl und Fokus zugleich. Keine Fläche, kein neuer Token.
- **Filter-Chips** (`src/Client/DesignSystem/ChipBar.fs`, `src/Client/styles.css`): die Pille trägt die Klasse `filter-chip` und keinen eigenen Innenabstand mehr (`px-1` entfällt), die Knöpfe tragen ihn und füllen die Pille bis an den Rand. Label ohne folgendes "×": `px-3.5`; Label mit "×": `pl-3.5 pr-2.5` (`chipLabelPadding`); das "×": `pl-0.5 pr-3.5`. Die ungelayerte Regel `.filter-chip button:focus-visible { outline-offset: 0; }` legt den Ring ohne Abstand auf den Rand (Label-Ring links bündig, "×"-Ring rechts bündig); am aktiven Chip überdeckt er den teal Rand als eine dickere Linie. Die Rundung aus k4f9t bleibt.
- **Maße gemessen** (Wegwerf-Skript im Scratchpad, Chromium 1440 px, Karte ChipBar auf `/styleguide`, dieselbe Komponente wie in der Kassenbuch-Filterleiste). Stand vor w7c2h (`main:src/Client/DesignSystem/ChipBar.fs`) und nach dem Fix gleich: aktiv mit "×" "Konto: Girokonto" 153,63 px, "Kategorie (2)" 132,05 px; ohne "×" "Zeitraum" 82 px, "Zu prüfen" 86,63 px, "Ohne Kategorie" 120,47 px. Je Chip Text bis Außenkante links 15 px (1 px Rand plus 14 px), rechts 15 px, Text bis "×" 18 px, alles wie vorher. Der Stand aus Iteration 1 war bei Chips mit "×" 4 px breiter (157,63 und 136,05 px, Text bis "×" 22 px); das ist behoben.
- **Test** (`scripts/budget-page.test.mjs`): der k4f9t-Test ist umgestellt und bleibt ein einziger `[layout]`-Test ("Fokus am PC: der Tab Geld sieht per Pfeiltaste aus wie per Seiten-Kürzel gewählt, am Chip Überzogen liegt der Ring auf dem Rand der Pille, nach einem Mausklick fehlt er"). Er vergleicht Ring, Hintergrund und Schatten des Tabs Geld nach Mausklick plus Rechts/Links mit dem Zustand nach Wahl über das Seiten-Kürzel aus dem Suchfeld (`outline-style` none, gleicher Hintergrund) und prüft am aktiven, per Tastatur fokussierten Chip Überzogen: Ring sichtbar, Rundung wie die Pille, `outline-offset` 0 px, kein Abstand zwischen Knopf und Pillenrand; nach Mausklick kein Ring. Kein neuer Browser-Test für die Maße.
- **Rot vor dem Fix:** gegen den k4f9t-Stand rot mit "Tab: kein Ring, der Strich zeigt Auswahl und Fokus" (`solid` statt `none`).
- **Gegenproben je Fix einzeln** (jeweils nur dieser Teil zurückgenommen, Rest bleibt; in Iteration 1 gelaufen):
  - Tab-Regel wie k4f9t (`outline-offset: -2px`): rot, "Tab: kein Ring …".
  - Tab mit heller Fläche (`background-color: var(--color-surface-hover)`): rot, "Tab: per Pfeiltaste wie per Seiten-Kürzel, keine eigene Fläche".
  - Chip ohne `outline-offset: 0`: rot, "Chip: kein Abstand zwischen Knopf und Ring" (2 statt 0).
  - Chip-Pille wieder mit `px-1`: rot, "Chip: der Ring liegt auf dem Rand der Pille, eine Linie (Abstand 4 px)".
- **Styleguide:** `standards/frontend/styleguide.md` nennt im Tabs-Eintrag (§5) in einem Satz: Tabs zeigen Auswahl und Fokus mit demselben Strich, Chips den Fokus als Ring auf dem Rand. `/styleguide` (`src/Client/Components/Styleguide/View.fs`) nennt beides in den Notizen der Karten Tabs und ChipBar; die echten Komponenten dort zeigen die Markierung per Tastatur.
- Das Seiten-Kürzel Links/Rechts und der globale Ring `*:focus-visible` bleiben unverändert.
- `./verify.sh`: `VERIFY: PASS` (dotnet 3580 grün, node 288 grün, Builds und beide Wächter grün).
- Offen für das Auge von Roman [human-eye]: ruhige Tabs, eine Linie am aktiven Chip mit Fokus.
