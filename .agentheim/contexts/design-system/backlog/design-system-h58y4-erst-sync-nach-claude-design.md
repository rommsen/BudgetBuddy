---
id: design-system-h58y4
title: Erst-Sync des Design-Systems nach Claude Design ("BudgetBuddy Design System") und Sichtprüfung durch Roman
status: backlog
type: chore
context: design-system
created: 2026-09-11
completed:
depends_on: [design-system-t82sg]
blocks: [ledger-xbrht]
tags: [design-system, claude-design, human-gate]
related_adrs: []
related_research: []
prior_art: [design-system-003]
---

## Why
`design-system-t82sg` erzeugt das Bundle, lädt es aber nicht hoch. Das Hochladen geht nur
über das `DesignSync`-Werkzeug, das Worker nicht haben, und jeder Schreibplan
(`finalize_plan`) wird von Roman freigegeben. Dieser Task ist deshalb **kein Worker-Task**,
sondern läuft in der Hauptsession mit Roman, wie die Abnahme von `ledger-kt0wq`.

Erst danach kann der UX-Durchgang fürs Kassenbuch mit Mockups in Claude Design starten.

## What
1. Neues Design-System-Projekt "BudgetBuddy Design System" anlegen
   (`DesignSync create_project`). Das bestehende Projekt "Smolder Design System" wird
   nicht angefasst.
2. `npm run export:design-system` ausführen, Bundle aus `dist/design-system/` prüfen.
3. Schreibplan mit `finalize_plan` festlegen (`localDir` = `dist/design-system`), von Roman
   freigeben lassen, dann `write_files` mit `localPath`.
4. Roman öffnet das Projekt in claude.ai/design und prüft die Karten gegen die
   `/styleguide`-Route der App.
5. Den Re-Sync-Ablauf (Export, Plan, Freigabe, Schreiben; gelöschte Karten per
   `delete_files`) im `design-system/README.md` festhalten.

## Acceptance criteria
- [ ] Projekt "BudgetBuddy Design System" existiert in Romans Account, Typ
      `PROJECT_TYPE_DESIGN_SYSTEM`.
- [ ] Alle Dateien des Bundles liegen im Projekt; `list_files` stimmt mit
      `dist/design-system/` überein.
- [ ] Der Design-System-Bereich in claude.ai/design zeigt die Karten gruppiert (Colors, Type,
      Components, Brand).
- [ ] Roman bestätigt: Tokens und Komponenten sehen aus wie in der App (Human-Gate).
- [ ] Der Re-Sync-Ablauf steht im `design-system/README.md`.

## Notes
- Die ID des neuen Projekts im README notieren, damit ein Re-Sync das richtige Projekt
  trifft (`get_project` prüft den Typ vor dem Schreiben).
- Bleibt bewusst im Backlog, bis `design-system-t82sg` erledigt ist; wird dann promotet
  und in der Hauptsession abgearbeitet, nicht von `work` an einen Worker gegeben.
