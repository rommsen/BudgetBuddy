---
id: design-system-h58y4
title: Erst-Sync des Design-Systems nach Claude Design ("BudgetBuddy Design System") und Sichtprüfung durch Roman
status: done
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
- [x] Projekt "BudgetBuddy Design System" existiert in Romans Account, Typ
      `PROJECT_TYPE_DESIGN_SYSTEM` (Id `3a764a23-efa6-4d3a-8a66-e0325666d0c3`).
- [x] Alle Dateien des Bundles liegen im Projekt; `list_files` stimmt mit
      `dist/design-system/` überein (16 Dateien).
- [x] Der Design-System-Bereich in claude.ai/design zeigt die Karten gruppiert (Colors, Type,
      Components, Brand).
- [x] Roman bestätigt: Tokens und Komponenten sehen aus wie in der App (Human-Gate), mit zwei
      Funden, die als Folge-Tasks weiterlaufen (siehe Outcome).
- [x] Der Re-Sync-Ablauf steht im `design-system/README.md`.

## Notes
- Die ID des neuen Projekts im README notieren, damit ein Re-Sync das richtige Projekt
  trifft (`get_project` prüft den Typ vor dem Schreiben).
- Bleibt bewusst im Backlog, bis `design-system-t82sg` erledigt ist; wird dann promotet
  und in der Hauptsession abgearbeitet, nicht von `work` an einen Worker gegeben.

## Outcome
Erst-Sync am 2026-09-11 in der Hauptsession mit Roman. `npm run export:design-system`
erzeugte 16 Dateien (184 KB, zehn Vorschau-Karten mit `@dsCard`-Marker, Schriften per
`document.fonts` geprüft). Vor dem Upload auf persönliche Daten geprüft: nur Beispieldaten
aus der Styleguide-Galerie. Projekt "BudgetBuddy Design System" per `create_project`
angelegt (Id `3a764a23-efa6-4d3a-8a66-e0325666d0c3`), Typ geprüft, Schreibplan mit genau
den 16 Pfaden von Roman freigegeben, per `write_files` mit `localPath` hochgeladen,
`list_files` stimmt mit dem lokalen Bundle überein. Re-Sync-Ablauf und Projekt-Id stehen im
`design-system/README.md`.

**Sichtprüfung durch Roman:** sieht gut aus, mit zwei Funden.
1. Es fehlen Komponenten, vor allem die Kategorienauswahl (`BottomSheet.categoryPicker` mit
   Vorschlägen, Zuletzt verwendet und der Gruppenliste mit Available-Beträgen). Sie war nie
   im Umfang: der Export kennt nur die sechs Kassenbuch-Komponenten, und die
   `/styleguide`-Route zeigt den Kategorien-Picker nicht. Folge-Task
   `design-system-y6gkd` (Kategorienauswahl in Styleguide und Export, dazu alle übrigen
   Styleguide-Sektionen).
2. Claude Design meldet "Missing brand fonts": es erwartet die Schriften als Dateien, der
   Google-Fonts-Link im Bundle reicht dort nicht. Zusammen mit dem App-Bug
   `design-system-f7k2p` gelöst, der dafür zu "Schriften selbst ausliefern" umgebaut wurde
   (woff2 im Repo, `@font-face`, Export legt die Dateien ins Bundle).

Nach beiden Folge-Tasks folgt ein Re-Sync nach dem README-Ablauf, zu Beginn von
`ledger-xbrht`.

## Diary entry

## 2026-09-11 20:35 - Erst-Sync des Design-Systems nach Claude Design (design-system-h58y4)

**What I did:**
Das Export-Bundle des Design-Systems in ein neues Design-System-Projekt "BudgetBuddy Design
System" in claude.ai/design hochgeladen (16 Dateien, Schreibplan von Roman freigegeben) und
den Re-Sync-Ablauf mit der Projekt-Id im design-system-README festgehalten. Romans
Sichtprüfung ergab zwei Funde, beide als Folge-Tasks erfasst.

**Files Added:**
- keine im Repo (das Bundle unter `dist/design-system/` ist git-ignoriert)

**Files Modified:**
- `.agentheim/contexts/design-system/README.md` - Abschnitt "Claude-Design-Projekt und
  Re-Sync" mit Projekt-Id und Ablauf.

**Files Deleted:**
- keine

**Rationale:**
Das UX-Konzept des Kassenbuchs (`ledger-xbrht`) soll mit Mockups auf Basis der echten
Tokens und Komponenten entstehen.

**Outcomes:**
- Build: nicht betroffen (kein Code geändert)
- Tests: nicht betroffen
- Issues: fehlende Kategorienauswahl im Export (`design-system-y6gkd`), Schriften als
  Dateien benötigt (`design-system-f7k2p`)
