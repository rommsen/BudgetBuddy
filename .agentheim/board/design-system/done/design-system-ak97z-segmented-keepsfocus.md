---
id: design-system-ak97z
title: "Segmented mit KeepsFocus: Ausgabe/Einnahme im Sheet Betrag schließt die Tastatur nicht mehr"
status: done
type: bug
context: design-system
created: 2026-11-10
completed:
depends_on: []
blocks: []
tags: [laufend, frontend, tastatur, handy, betrag]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Im Sheet "Betrag" (ledger-m7mye, ledger-q42pg) nimmt ein Tipp auf "Ausgabe" oder "Einnahme" (`Segmented`) dem
Betragsfeld den Fokus, am Handy schließt sich die Tastatur. Das verletzt die Regel aus CLAUDE.md, dass der Wechsel
zwischen zwei Feldern die Tastatur nicht schließen darf. Gefunden im Review-Lauf 2026-11-10; Roman: eigene Task.

## What
- `Segmented` bekommt eine Option wie `Button.KeepsFocus` (onMouseDown preventDefault am Segment), Vorgabe aus.
- Das Sheet bzw. der Dialog "Betrag" im Kassenbuch nutzt sie, das Betragsfeld behält den Fokus beim Umschalten.
- Ebenso "Ausgabe | Einnahme" in "Neue Buchung" im Kassenbuch (`src/Client/Components/Ledger/View.fs` um Zeile 3650,
  ledger-at63w), falls dort ein Betragsfeld daneben den Fokus haben kann.
- Form: ein Feld `KeepsFocus: bool` am `Segment`-Record (`src/Client/DesignSystem/Segmented.fs`) oder ein Parameter von
  `Segmented.view`; der Worker wählt die Form, die zu `Button.KeepsFocus` passt, und zieht alle Nutzer nach.
- Andere Nutzer von `Segmented` (Tab Geld, Rhythmus im Tab Target) bleiben unverändert, außer dort steht ebenfalls
  ein Betragsfeld daneben; das prüft der Worker und nennt es im Outcome.
- Beispiel unter /styleguide, Zeile im Inventar `Segmented` (Styleguide §5).

## Acceptance criteria
- [ ] Layout-Sweep `kassenbuch-betrag-feldfehler` (oder eigener Zustand): Fokus im Betragsfeld, Tipp auf "Einnahme",
      kein `blur` am Feld; Gegenprobe ohne die Option wird rot.
- [ ] /styleguide zeigt `Segmented` mit der Option, Styleguide §5 nennt sie.
- [ ] Am Handy bei 412 px mit Touch: Umschalten lässt die Tastatur offen. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Vorbild: `Button.KeepsFocus` (Commit 5444edd7). Sichtbares Verhalten gegen den Styleguide geprüft
(`.claude/rules/task-ui-vorgaben.md`): keine Abweichung.

## Outcome

`Segmented.Segment` hat ein neues Feld `KeepsFocus: bool` (`src/Client/DesignSystem/Segmented.fs`). Ist es gesetzt, ruft das Segment auf `mousedown` `preventDefault` auf, genau wie `Button.KeepsFocus`. Ich habe die Form als Record-Feld gewählt, passend zu `Button.ButtonProps.KeepsFocus`, und alle Nutzer angepasst.

- **Mit `KeepsFocus = true`:** Ausgabe/Einnahme im Sheet bzw. Dialog "Betrag" (`amountFormBody`) und in "Neue Buchung" (`captureFields`), beide in `src/Client/Components/Ledger/View.fs`. Neben beiden Leisten steht ein Betragsfeld, das den Fokus haben kann.
- **Unverändert mit `KeepsFocus = false`:**
  - Modus im Tab Geld (`Budget/MovesView.fs`): ein Moduswechsel baut die Felder darunter neu auf, kein Wechsel zwischen zwei Feldern.
  - Rhythmus und Wochentag im Tab Target (`Budget/TargetFormView.fs`): das sind eigene Auswahlfelder im Formular, kein Umschalter für das Betragsfeld.
  - Die bisherigen Demos unter /styleguide.
- **/styleguide (Abschnitt 23b, Karte Segmented):** neues Beispiel `demo-segmented-keeps-focus`, Ausgabe/Einnahme mit Betragsfeld darunter und einer Bildunterschrift. Styleguide §5, Zeile `Segmented`, nennt jetzt `Segment.KeepsFocus`.
- **Layout-Sweep (`scripts/layout-states.mjs`):**
  - `kassenbuch-betrag-feldfehler`: der Fokus liegt im Feld, dann ein Tipp auf "Einnahme" und auf "Ausgabe", danach darf kein `blur` gezählt sein.
  - Den Fall von ledger-q42pg (Feld hat den Fokus verloren, Speichern holt ihn zurück) deckt der Zustand weiter ab, jetzt mit einem `blur()` per Skript vor dem letzten Speichern.
  - Am Ende wartet der Zustand, bis "Speichern" nicht mehr `:active` ist. Chromium hält `:active` nach einem Tipp kurz fest, dann misst der Knopf wegen `active:scale-[0.98]` nur 47 px. Vorher hat das Zurückholen des Fokus diese Zeit überbrückt.
  - `kassenbuch-erfassen`: Tipp ins Betragsfeld, dann auf "Einnahme", danach darf kein `blur` gezählt sein.
- **Gegenprobe:**
  - Vor der Änderung schlugen beide Zustände bei 320 und 390 px fehl ("Betragsfeld verlor bei Einnahme den Fokus" bzw. "Betragsfeld in Neue Buchung verlor bei Einnahme den Fokus").
  - Mit `KeepsFocus = false` nur im Sheet "Betrag" schlug `kassenbuch-betrag-feldfehler` wieder fehl.
- **Gate:** `./verify.sh` ergibt `VERIFY: PASS`.
- **Noch offen:** der Test am Handy bei 412 px mit Touch [human-eye] ist Romans Sache.
