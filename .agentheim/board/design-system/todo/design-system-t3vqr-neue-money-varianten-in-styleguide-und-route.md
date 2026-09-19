---
id: design-system-t3vqr
title: "Neue Money-Varianten in Styleguide-Dokument und /styleguide-Route nachziehen"
status: todo
type: chore
context: design-system
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [styleguide, gate, claude-design]
related_adrs: []
related_research: []
prior_art: [design-system-y6gkd]
---

## Why
Fund aus der Verifikation von `ledger-x0yga` (2026-09-12). Die Aufgabe hat die
Money-Komponente um zwei Varianten erweitert, `Money.accountBalance` und
`Money.accountListRow`. Letztere ist keine reine Formatierung, sondern eine vollständige
neue Komponente: eine klickbare Kontozeile mit Name links und Betrag rechts.

Nachgezogen wurde nichts davon:
- `standards/frontend/styleguide.md` führt im Komponenten-Inventar für Money weiterhin nur
  `simple`, `large`, `hero`, `noSign`, `fromMoney`.
- Die Route `src/Client/Components/Styleguide/View.fs` zeigt beide neuen Varianten nicht.

Die Folge reicht weiter als eine Doku-Lücke: Der Claude-Design-Export schneidet seine
Karten aus der gerenderten Styleguide-Route (`design-system-t82sg`, ADR 0024). Was dort
nicht steht, existiert im Design-System-Projekt in Claude Design nicht. Beim nächsten
Entwurf sieht niemand, dass es eine fertige Kontozeile gibt, und sie wird ein zweites Mal
erfunden.

## What
1. Beide Varianten in `standards/frontend/styleguide.md` ins Money-Inventar aufnehmen, mit
   dem Zweck je Variante (nicht nur dem Namen).
2. Beide in der `/styleguide`-Route zeigen, mit erfundenen Beispielwerten, damit sie in den
   Export wandern. `Money.accountListRow` braucht dabei ihren aktiven und ihren inaktiven
   Zustand, weil sie beide trägt.
3. Danach `npm run export:design-system` und Re-Sync nach Claude Design nach dem Ablauf im
   design-system-README, damit das Entwurfsprojekt den neuen Stand hat.

**Allgemeiner Punkt für die Verfeinerung:** Dass eine neue Komponente ohne Styleguide-
Eintrag durch die Verifikation kam, ist der eigentliche Befund. Es lohnt zu prüfen, ob das
Styleguide-Gate eine Zusicherung bekommen kann, etwa ein Test, der jede öffentliche
Funktion eines Design-System-Moduls gegen die Route abgleicht und laut scheitert, wenn eine
fehlt. Ohne das bleibt es davon abhängig, dass jemand daran denkt.

## Acceptance criteria
- [ ] Das Money-Inventar in `standards/frontend/styleguide.md` (Abschnitt 5, die Money-Zeile
      führt heute `simple`, `large`, `hero`, `noSign`, `fromMoney`) nennt zusätzlich
      `accountBalance` und `accountListRow`, je mit ihrem Zweck, nicht nur dem Namen.
- [ ] Die `/styleguide`-Route zeigt beide Varianten in der Money-Sektion (die Sektion trägt
      den Export-Marker `data-ds-card="components-money"` und zeigt heute `simple`, `large`,
      `hero`, `transactionDeleted`).
- [ ] `Money.accountListRow` erscheint dort in **beiden** Zuständen, aktiv und inaktiv, weil
      die Komponente beide trägt.
- [ ] `npm run export:design-system` nimmt beide Varianten in
      `preview/components-money.html` auf; zwei Läufe bleiben byte-identisch.
- [ ] `dotnet build` und `npm run build` sind grün.
- [ ] Re-Sync nach Claude Design nach dem Ablauf im design-system-README. [human-eye]

**Nicht Teil dieser Aufgabe:** die im `What` angerissene Zusicherung, die jede öffentliche
Funktion eines Design-System-Moduls gegen die Route abgleicht. Das ist der allgemeinere
Befund und deckt sich mit `design-system-thtpp` (Wächter gegen undefinierte Klassen); beides
gehört in eine eigene Runde, sonst wächst dieser Task vom Nachtrag zum Werkzeugbau.

## Notes
Captured am 2026-09-12 aus der Verifikation von `ledger-x0yga`. Zusammenhang:
`design-system-t82sg` (Export aus der Route), ADR 0024, `design-system-y6gkd` (vollständiger
Sektions-Export), `design-system-fq8dn` (tote DaisyUI-Klassen im selben Modulbereich).
