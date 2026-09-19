---
id: design-system-t3vqr
title: "Neue Money-Varianten in Styleguide-Dokument und /styleguide-Route nachziehen"
status: done
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

## Outcome

Beide von `ledger-x0yga` eingeführten Money-Varianten sind jetzt im Styleguide-Dokument
und in der `/styleguide`-Route sichtbar, damit der Claude-Design-Export sie mitnimmt.

- `standards/frontend/styleguide.md` (Abschnitt 5, Money-Zeile): `accountBalance` und
  `accountListRow` ergänzt, je mit Zweck statt nur dem Namen (Kontostand ohne führendes
  Plus für Kontostände-Leiste/-Liste; klickbare Kontozeile mit Name links, farbcodiertem
  Betrag rechts, aktivem Zustand mit Teal-Akzentlinie).
- `src/Client/Components/Styleguide/View.fs` (`moneySection`, Sektion 6, Export-Marker
  `data-ds-card="components-money"`): eine neue Demo-Zeile für `accountBalance` (erfundener
  Beispielwert `1250,40 €`) und eine neue Gruppe für `accountListRow` mit beiden
  Zuständen, `"Girokonto"` aktiv/positiv und `"Tagesgeld"` inaktiv/negativ, analog zum
  Beispielmuster aus `design-system-y6gkd`.

Verifikation:
- `dotnet build`: grün, 0 Warnungen.
- `dotnet test`: 914 bestanden, 7 übersprungen (Integrationstests ohne `.env`), 921
  gesamt, keine Fehlschläge.
- `npm run build`: grün.
- `npm run export:design-system`: zweimal ausgeführt, `dist/design-system` beider Läufe
  per SHA-256 über alle Dateien verglichen, byte-identisch. `preview/components-money.html`
  enthält die neuen Varianten (Girokonto/Tagesgeld-Zeilen und die accountBalance-Caption
  geprüft).

Die "Styleguide-Sektionen im Export"-Liste in der BC-README wurde geprüft und bewusst
nicht geändert: sie führt Karten (Komponenten), nicht deren einzelne Varianten, und die
Money-Karte selbst ist dort bereits gelistet.

Offen und nicht Teil dieser Aufgabe: der Re-Sync nach Claude Design (letztes
Akzeptanzkriterium, `[human-eye]`). Das `DesignSync`-Werkzeug existiert nur in der
Hauptsitzung; das bleibt Romans Schritt, nachdem `npm run export:design-system` (oben
bereits zweifach grün gelaufen) den neuen Bundle-Stand erzeugt hat.

Die im Task-`What` angerissene weitergehende Zusicherung (jede öffentliche Funktion
eines Design-System-Moduls gegen die Route abgleichen) wurde bewusst nicht gebaut, wie
vom Task selbst verlangt; sie deckt sich mit dem bereits vorhandenen `design-system-thtpp`.
