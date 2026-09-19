---
id: 0035
title: "Stats.NoAccent statt ClassName-Override: der Gradient-Akzentstreifen lässt sich jetzt ganz abschalten"
scope: design-system
status: accepted
date: 2026-09-19
related_tasks: [ledger-w9skm]
related_adrs: []
related_research: []
---

# ADR-0035: Stats.NoAccent statt ClassName-Override: der Gradient-Akzentstreifen lässt sich jetzt ganz abschalten

## Context

`ledger-x0yga` hatte festgestellt: die Kontostände-Karten am Handy tragen einen
Farbverlaufsstreifen, den keine einzige Zeile im Kassenbuch-Code je gesetzt hat.
Ursache ist `Stats.view` (`src/Client/DesignSystem/Stats.fs`): es zeichnet
unconditional eine "decorative gradient accent line at top"
(`h-[3px] bg-gradient-to-r {gradientClass}`), und `defaultProps.Accent = Gradient`
ist der Default. `accountCardView` in `Components/Ledger/View.fs` ruft `Stats.view`
ohne eigenes `Accent` auf und erbt den Streifen.

`ledger-w9skm` (AC4) verlangt: kein Streifen mehr, nicht nur ein anderer. Vor
dieser Aufgabe kannte `StatAccent` sechs Varianten (`Teal`/`Green`/`Orange`/
`Purple`/`Pink`/`Gradient`) — jede davon zeichnet den Streifen weiterhin, nur mit
anderer Füllfarbe. Es gab keinen Weg, ihn ganz zu unterdrücken, weder über einen
bestehenden `Accent`-Wert noch über `StatProps.ClassName` (das landet auf dem
äußeren Card-`div`, nicht auf dem inneren, absolut positionierten Streifen-`div`).

## Decision

`StatAccent` bekommt einen siebten, additiven Fall: `NoAccent`. `Stats.view`
prüft `props.Accent` vor dem Rendern des Streifens und gibt `Html.none` statt
des `div`s zurück, wenn `NoAccent` gesetzt ist. `accentToGradient` bekommt eine
(nie tatsächlich aufgerufene) `NoAccent -> ""`-Zeile, damit der Pattern-Match
über `StatAccent` weiterhin vollständig ist. `Components/Ledger/View.fs`s
`accountCardView` setzt jetzt `Accent = Stats.NoAccent`.

**Verworfene Alternative: eine Klasse am Aufrufer, die den Streifen per
CSS-Selektor versteckt** (z. B. `[&>div:first-child]:hidden` über
`StatProps.ClassName`). Hätte `Stats.fs` unangetastet gelassen und die Änderung
vollständig im Kassenbuch-Code gehalten. Verworfen, weil das Verstecken dann von
der zufälligen DOM-Position des Streifen-`div`s abhinge (erstes Kind) statt von
einer benannten Eigenschaft — genau das Muster, das ADR 0031 bei der
Zeilendichte bereits als Fehlerquelle vermieden hat (eine Regel an einer Stelle
bestimmt das Aussehen eines Kindelements, ohne dass man das beim Lesen des
Kindelement-Codes sieht). `Stats.fs` gehört zur Design-System-BC-Oberfläche;
diese Aufgabe (`ledger-w9skm`) berührt sie trotzdem, weil die pixelgenaue
Angleichung an den Kassenbuch-Entwurf das verlangt — dieselbe Abwägung wie bei
`Stats.ValueElement`/`Table.FixedLayout` aus `ledger-x0yga`.

## Consequences

### Positive
- `NoAccent` ist rein additiv: kein bestehender Aufrufer (aktuell keiner
  außerhalb von `Stats.fs` selbst nutzt einen anderen `Accent`-Wert explizit)
  ändert sein Verhalten.
- Der Mechanismus ist benannt und dokumentiert, nicht implizit über DOM-Position
  erraten.

### Negative
- Ein siebter Fall in einem bereits sechs Fälle umfassenden Enum; jede weitere
  Pattern-Match-Stelle über `StatAccent` muss ihn mitführen (aktuell nur
  `accentToGradient` und `view` selbst).
- `standards/frontend/styleguide.md` und die `/styleguide`-Route
  (`Components/Styleguide/View.fs`) kennen `NoAccent` noch nicht — eigenes
  Backlog-Item (design-system-Kontext), analog zu `design-system-t3vqr` aus
  `ledger-x0yga`.

## Alternatives considered

Siehe "Verworfene Alternative" oben (CSS-Selektor-Override am Aufrufer).