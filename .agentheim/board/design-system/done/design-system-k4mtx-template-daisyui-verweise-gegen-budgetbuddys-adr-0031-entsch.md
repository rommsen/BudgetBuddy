---
id: design-system-k4mtx
title: "DaisyUI-Verweise in template/** und docs/MILESTONE-PLAN.md auf ADR 0031 nachziehen"
status: done
type: chore
context: design-system
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [doku, template, daisyui]
related_adrs: [0031]
related_research: []
prior_art: [design-system-fq8dn, design-system-k2vgz]
---

## Why
ADR 0031 (`design-system-fq8dn`) hat DaisyUI aus BudgetBuddy entfernt: es war nie installiert,
sieben Stellen setzten trotzdem DaisyUI-Klassen, alle sieben sind durch echtes Tailwind
ersetzt. `design-system-k2vgz` hat danach die letzten beiden lebenden Dokument-Verweise auf
DaisyUI aus BudgetBuddys eigener Doku entfernt (`docs/DESIGN-SYSTEM.md`, `docs/README.md`
gelöscht).

`template/**` ist von beiden Aufgaben bewusst ausgenommen worden, führt DaisyUI aber weiter:

- `template/CLAUDE.md:254`: Tabelle „Styling | TailwindCSS, DaisyUI"
- `template/README.md:7`: „Frontend: Elmish.React + Feliz + TailwindCSS/DaisyUI"
- `template/docs/00-ARCHITECTURE.md:8`: Tabelle „Styling | TailwindCSS, DaisyUI"

## What

### Entscheidung Roman, 2026-09-19: das Template folgt BudgetBuddy

Die offene Frage aus der Erfassung („darf ein neues Projekt aus dem Template bewusst mit
DaisyUI starten?") ist beantwortet: **nein.** Die drei Stellen werden auf reines Tailwind
umgestellt, in der Diktion von ADR 0031. Ein Gerüst, das eine im Hauptprojekt bereits
verworfene Bibliothek empfiehlt, gibt den Fund an jedes daraus erzeugte Projekt weiter.

Damit ist der Entscheidungsteil erledigt; übrig bleibt eine Korrektur. Der Typ ist
deshalb von `decision` auf `chore` geändert.

### Vierte Stelle, beim Verfeinern gefunden (2026-09-19)

`docs/MILESTONE-PLAN.md` führt DaisyUI ebenfalls, und zwar als lebende Aussage über den
Stack, nicht als Protokoll:

- Zeile 18: das Architektur-Diagramm nennt „Elmish.React + Feliz + TailwindCSS + DaisyUI"
- Zeile 84 und 99: Milestone 0 listet DaisyUI unter den zu prüfenden Frontend-Abhängigkeiten
  und bestätigt „DaisyUI 4.12.14"

Das wiegt schwerer als `template/**`, weil `CLAUDE.md` `docs/MILESTONE-PLAN.md`
ausdrücklich als Arbeitsgrundlage benennt. Die Datei trägt keinen Historik-Hinweis.

Zum Vergleich: `docs/UI-REFACTORING-MILESTONES.md` nennt DaisyUI an sechs Stellen, trägt
aber seit `design-system-k2vgz` einen Kopf, der sie ausdrücklich zum abgeschlossenen
Protokoll erklärt („Nicht als Anweisung lesen"). **Diese Datei bleibt unverändert.** Ein
Protokoll beschreibt, was damals galt; es rückwirkend zu glätten würde die Historie
verfälschen.

Für `docs/MILESTONE-PLAN.md` ist zwischen zwei Wegen zu wählen, und die Wahl ist zu
begründen: entweder das Architektur-Diagramm auf den heutigen Stand ziehen (Zeile 18),
oder die Datei wie `UI-REFACTORING-MILESTONES.md` mit einem Historik-Kopf versehen und die
abgehakten Milestone-Zeilen unangetastet lassen. Die Milestone-0-Zeilen 84 und 99 sind
abgehakte Protokollzeilen und sprechen eher für den zweiten Weg.

## Acceptance criteria
- [ ] Die drei `template/**`-Stellen nennen TailwindCSS ohne DaisyUI, in der Diktion von
      ADR 0031.
- [ ] `grep -ri daisyui template/` liefert keinen Treffer mehr, oder jeder verbliebene
      Treffer trägt an Ort und Stelle eine Begründung.
- [ ] Für `docs/MILESTONE-PLAN.md` ist einer der beiden Wege umgesetzt (Diagramm
      korrigiert oder Historik-Kopf ergänzt) und die Wahl im Ergebnis begründet.
- [ ] `docs/UI-REFACTORING-MILESTONES.md` ist unverändert; dass sie bewusst
      ausgenommen ist, steht im Ergebnis.
- [ ] Kein Code ist angefasst worden: die Änderung betrifft ausschließlich Markdown.

## Notes
Backlog-Item aus `design-system-k2vgz` heraus erfasst; die Aufgabe selbst grenzte `template/**`
explizit aus („eigene Frage, eigener Task"). Verfeinert am 2026-09-19: Entscheidung von
Roman getroffen, vierte Fundstelle ergänzt, Typ von `decision` auf `chore` geändert.

Reine Doku-Aufgabe ohne Testbedarf: es gibt kein Verhalten, das sich ändern könnte. Das
prüfbare Kriterium ist der Grep, nicht eine Zusicherung im Testlauf.

## Outcome

**`template/**` (drei Stellen, alle geändert):**
- `template/CLAUDE.md:254` — Tabellenzeile „Styling | TailwindCSS, DaisyUI" → „Styling | TailwindCSS".
- `template/README.md:7` — „Frontend: Elmish.React + Feliz + TailwindCSS/DaisyUI" → „... + TailwindCSS".
- `template/docs/00-ARCHITECTURE.md:8` — dieselbe Tabellenzeile wie in `CLAUDE.md`, gleiche Korrektur.

DaisyUI wurde an allen drei Stellen ersatzlos entfernt, ohne Klammer-Anmerkung. Das ist
bewusst keine wörtliche Übernahme von ADR 0031s „(kein DaisyUI, ADR 0031)"-Anmerkung aus
BudgetBuddys eigener `CLAUDE.md`: `template/**` wird in fremde Projekte kopiert, die ADR
0031 nicht in ihrer eigenen Historie haben — ein Verweis darauf wäre dort ein
hängender Zeiger ins Leere. Vorbild ist stattdessen ADR 0031 Punkt 4 (der Schieberegler:
„range/range-sm ersatzlos entfernt"), derselbe Umgang mit einer toten Abhängigkeit ohne
Fußnote. Damit liefert `grep -ri daisyui template/` jetzt keinen Treffer mehr (verifiziert,
Exit-Code 1) — die erste, stärkere Alternative aus Akzeptanzkriterium 2, keine
Begründungs-Annotation nötig.

**`docs/MILESTONE-PLAN.md` — Weg (a) gewählt: Architektur-Diagramm korrigiert, nicht
Weg (b) mit Historik-Kopf:**

Zeile 18 (Architektur-Übersicht, vor jedem Milestone) korrigiert:
„Elmish.React + Feliz + TailwindCSS + DaisyUI" → „Elmish.React + Feliz + TailwindCSS"
(Box-Innenbreite von 61 Zeichen exakt erhalten, per Skript gegen die Nachbarzeile
abgeglichen).

Die drei abgehakten Protokollzeilen bleiben unangetastet:
- Zeile 84 (Milestone 0, Aufgaben-Liste): „- DaisyUI"
- Zeile 99 (Milestone 0, Abschluss-Notiz 2025-11-29): „Frontend dependencies confirmed:
  ... DaisyUI 4.12.14 ..."
- Zeile 1610 (Milestone 7, Verifikations-Checkliste 2025-11-30, beim Verfeinern nicht
  genannt, beim Umsetzen gefunden): „- [x] DaisyUI components render correctly"

Begründung für Weg (a) statt (b): Der Task-Text selbst schlägt Weg (b) vor (Historik-Kopf
wie bei `docs/UI-REFACTORING-MILESTONES.md`), mit der Beobachtung, dass die
Milestone-0-Zeilen abgehakte Protokollzeilen sind. Das trifft zu — aber die Prämisse für
einen Historik-Kopf trägt hier nicht so weit wie bei
`docs/UI-REFACTORING-MILESTONES.md`: dessen Kopf ist zutreffend, weil dort wirklich alle
Milestones R0-R12 abgeschlossen sind, das Dokument also als Ganzes abgeschlossenes
Protokoll ist. `docs/MILESTONE-PLAN.md` dagegen hat Milestone 16 („Docker & Deployment")
noch offen: die Verifikations-Checkliste dort ist ungehakt, es fehlt der
„✅ Milestone 16 Complete"-Abschnitt, den jeder der 16 vorherigen Milestones hat. Ein
dokumentweiter „nicht als Anweisung lesen"-Kopf würde diese Datei fälschlich als
vollständig abgeschlossen darstellen, obwohl ein Teil noch aktive Arbeitsgrundlage ist —
genau die Rolle, die `CLAUDE.md` der Datei ausdrücklich zuweist. Das Architektur-Diagramm
in Zeile 18 ist zudem keine datierte, abgehakte Protokollzeile wie 84/99/1610, sondern eine
lebende Behauptung über den heutigen Stack (Überschrift „Architecture Summary", vor jedem
Milestone) — sie direkt zu korrigieren folgt demselben Muster, das ADR 0031 selbst für
BudgetBuddys `CLAUDE.md`-Stack-Tabelle gewählt hat (Punkt 5: korrigiert, nicht mit einem
Kopf versehen). Die drei ticked Zeilen dagegen sind datierte Aussagen darüber, was am
29./30.11.2025 tatsächlich geprüft wurde — sie rückwirkend zu glätten würde die Historie
verfälschen, exakt aus demselben Grund, aus dem `docs/UI-REFACTORING-MILESTONES.md` nicht
angefasst wird.

**`docs/UI-REFACTORING-MILESTONES.md`:** bewusst unverändert gelassen (nicht in
`git status` — geprüft). Sie trägt bereits seit `design-system-k2vgz` ihren eigenen
Historik-Kopf und ist als Ganzes abgeschlossenes Protokoll; ihre sechs DaisyUI-Stellen
(Zeilen 15, 32, 42, 824, 842, 1107) gehören zu dieser Historie und bleiben unangetastet.

**Kein Code angefasst:** `git status --short` zeigt ausschließlich die vier oben
genannten Markdown-Dateien.

**Tests:** `dotnet test src/Tests/Tests.fsproj` aus dem Workspace-Root:
932 bestanden, 0 fehlgeschlagen, 7 übersprungen (bestehende .env-gebundene
Integrationstests, unverändert) — keine Regression durch die reine Doku-Änderung.
