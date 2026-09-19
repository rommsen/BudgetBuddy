---
id: design-system-k4mtx
title: "DaisyUI-Verweise in template/** und docs/MILESTONE-PLAN.md auf ADR 0031 nachziehen"
status: todo
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
