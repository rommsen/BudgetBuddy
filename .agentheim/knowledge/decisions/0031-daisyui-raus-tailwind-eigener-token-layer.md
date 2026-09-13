---
id: 0031
title: DaisyUI kommt raus — sieben tote Klassen durch echtes Tailwind ersetzt, kein Nachrüsten
scope: design-system
status: accepted
date: 2026-09-13
supersedes: []
superseded_by: []
related_tasks: [design-system-fq8dn]
related_adrs: []
related_research: []
---

# ADR 0031: DaisyUI kommt raus — sieben tote Klassen durch echtes Tailwind ersetzt

## Context
Bei der Verifikation von `ledger-x0yga` (2026-09-12) fiel auf: die Kassenbuch-Tabelle
hatte keinen waagerechten Zellenabstand. Ursache war `Table.view`, das die Klassen
`.table`/`.table-sm` setzte — DaisyUI-Komponentenklassen. DaisyUI ist in diesem Projekt
nie installiert worden: nicht in `package.json`, nicht unter `node_modules/`,
`src/Client/styles.css` importiert ausschließlich `tailwindcss`. `CLAUDE.md` behauptete
unter „Tech Stack Reference" trotzdem „TailwindCSS 4.3, DaisyUI".

Eine vollständige Bestandsaufnahme (Conductor, 2026-09-13: alle String-Literale der
Client-Dateien gegen die Selektoren im gebauten CSS und gegen bekannte
DaisyUI-Komponentennamen gehalten) fand **sieben Stellen in drei Dateien**, die
DaisyUI-Klassen setzten, ohne dass ein Stylesheet dafür existierte:

| Datei | Klasse | Effekt ohne DaisyUI |
|---|---|---|
| `DesignSystem/Table.fs` (×4: `table-sm`, `table-lg`, `table-zebra` ×2) | Zeilendichte/Zebra | keiner — Zellen hatten bis `ledger-x0yga` sogar 0px Innenabstand |
| `DesignSystem/Table.fs` (`table`, auf `<table>`) | — | keiner — `<table>`-Elemente haben `display: table` per User-Agent-Stylesheet ohnehin |
| `DesignSystem/Loading.fs` (`table`, ebenfalls auf `<table>`, nicht — wie zunächst vermutet — auf einem `<div>`) | — | wie oben, keiner |
| `Components/Settings/View.fs` (`range range-sm`) | Schieberegler-Optik | keiner — nativer Browser-Schieberegler, `accent-neon-teal` griff bereits |

Keine dieser Stellen war Absicht: sie stammen aus einer Zeit, in der das Projekt
vermutlich DaisyUI nutzen sollte oder wollte, ohne dass die Abhängigkeit je gezogen
wurde. Der Fund blieb unbemerkt, weil eine Tabelle ohne Zellenabstand nicht kaputt
aussieht, nur eng — sichtbar wurde er erst neben einem Mockup-Entwurf.

## Decision
**DaisyUI kommt raus, nicht rein.** Alle sieben Stellen wurden durch echtes,
funktionierendes Tailwind ersetzt statt DaisyUI nachzuinstallieren:

1. **Zeilendichte (`TableSize.Compact`/`Spacious`).** `cell`/`headerCell` besitzen die
   Zellen-Padding-Deklaration bereits direkt (`px-3 py-2`, seit `ledger-x0yga`). Die
   Dichte-Varianten hängen jetzt an genau dieser Stelle: die Padding-Utilities lauten
   `px-[var(--table-cell-px,0.75rem)] py-[var(--table-cell-py,0.5rem)]`, und
   `Table.view` überschreibt die beiden CSS-Variablen für `Compact`/`Spacious` per
   `prop.style` auf dem `<table>`-Element — bewusst ein `style`, keine Klasse, wie es
   die alte `table-sm`/`table-lg`-Klasse war (deren Fehler ja genau war, dass keine
   Regel dafür existierte). `Normal` setzt keine Variable, der Fallback ist exakt der
   vorherige feste Wert — für jeden heutigen Aufrufer byte-identisch.
2. **Zebra-Variante.** Als Tailwind-Arbitrary-Variant am Zeilen-Selektor gelöst, analog
   zu `Hover` (`[&_tr:hover]:bg-white/5`, bereits vorhandenes Muster in derselben
   Datei): `[&_tbody_tr:nth-child(even)]:bg-white/[0.03]`, eine Klasse direkt am
   `<table>`. Anders als bei der Zeilendichte ist hier eine Tabellenklasse richtig: es
   ist eine echte, von Tailwind kompilierte Utility, kein Verweis auf ein fehlendes
   Fremd-Stylesheet.
3. **`.table` auf `<table>`-Elementen** (`Table.fs`, `Loading.fs`) entfernt: Tailwinds
   `.table`-Utility (`display: table`) ist auf einem `<table>`-Element wirkungslos, weil
   der Browser das ohnehin per User-Agent-Stylesheet setzt. Für `Loading.fs` wurde die
   ursprüngliche Vermutung — die Klasse stehe dort auf einem `<div>` und `display: table`
   könnte beabsichtigt sein — beim Lesen des Codes widerlegt: es ist ebenfalls ein
   `Html.table`-Element, derselbe Fall wie in `Table.fs`.
4. **Schieberegler** (`Components/Settings/View.fs`): `range`/`range-sm` ersatzlos
   entfernt, `accent-neon-teal` (Akzentfarbe für Spur/Griff) bleibt. Der native
   Browser-Schieberegler reicht für den einzigen Slider im Projekt; eine eigene
   Design-System-Komponente dafür wäre Vorratsbau ohne zweiten Aufrufer.
5. **`CLAUDE.md`** korrigiert: „Styling | TailwindCSS 4.3, DaisyUI" →
   „TailwindCSS 4.3 (kein DaisyUI, ADR 0031)".
6. **`Ledger/View.fs`** pinnt seine Tabelle jetzt explizit auf `Variant = Hover` statt
   den (jetzt tatsächlich wirksamen) `ZebraHover`-Default zu erben: die
   Kassenbuch-Referenzscreenshots (`docs/ux/kassenbuch/screenshots/`) zeigen keinen
   Zebra-Streifen, weil `Zebra` vorher inert war. Ohne diese explizite Pinnung hätte das
   Beheben des Zebra-Bugs die Kassenbuch-Liste sichtbar verändert — ein
   Verifikations-Fehlschlag laut design-system-README ("Mockups gehören ins Repo, und
   Frontend-Arbeit hat ein visuelles Gate"), obwohl kein Akzeptanzkriterium dieser
   Aufgabe das verlangt hätte.

## Consequences
### Positive
- Alle sieben Stellen setzen keine tote Klasse mehr; Zeilendichte und Zebra-Variante
  wirken tatsächlich, wenn sie genutzt werden (verifiziert: `Table.Compact` testweise auf
  die Styleguide-Tabelle angewandt, sichtbar engere Zeilen, danach zurückgesetzt — kein
  Aufrufer nutzt `Compact`/`Spacious` heute, der Mechanismus ist aber real).
- `CLAUDE.md` behauptet nicht mehr etwas, das nie stimmte.
- Kein zweites Gestaltungssystem neben dem Token-Layer (`styles.css` `@theme`,
  `standards/frontend/styleguide.md`) — keine Frage, welches bei einem Konflikt gewinnt.
- Kassenbuch-Liste bleibt visuell unverändert (Ledger-Pinnung auf `Hover`).

### Negative
- Zeilendichte kaskadiert nicht automatisch von `TableProps.Size` zu jeder Zelle wie
  eine echte DaisyUI-Klasse es täte — der Mechanismus (CSS-Variable + `var()`-Fallback in
  `cell`/`headerCell`) ist ein Konstrukt speziell für dieses eine Problem, kein
  allgemeines Prop-Cascading. Für dieses Projekt (eine Tabellenkomponente, zwei
  Aufrufer) ausreichend; ein drittes, unabhängig konfigurierbares Dichte-Bedürfnis würde
  das Muster erneut prüfen müssen.
- `.table{display:table}` bleibt im gebauten CSS-Bundle vorhanden (0 Byte praktische
  Kosten, wenige Bytes Bundle-Größe): Tailwinds Content-Scanner findet das Wort „table"
  weiterhin in Code-Kommentaren (`` `.table`/`.table-sm` ``), die die Historie dieses
  Fundes dokumentieren, und kompiliert die Utility vorsorglich. Keine funktionale
  Auswirkung, da kein Element mehr die Klasse `table` trägt.

## Alternatives considered
- **DaisyUI nachrüsten** (`npm install daisyui`, Tailwind-Plugin registrieren): hätte alle
  sieben Klassen ohne Codeänderung zum Laufen gebracht. Verworfen (Romans Entscheidung,
  2026-09-13): das Design-System trägt sich bereits vollständig über den eigenen
  Token-Layer; ein zweites Gestaltungssystem daneben würde bei jedem neuen
  UI-Baustein die Frage aufwerfen, welches gewinnt, und DaisyUIs eigene Farb-/
  Spacing-Skala würde neben `Tokens.fs` existieren, ohne dass ein Konsument sie
  bräuchte — für sieben Klassen in einer Komponente unverhältnismäßig.
- **Zeilendichte über eine Klasse am `<table>` lösen** (wie bei `Zebra`, via
  `[&_th]:px-2 [&_td]:px-2`-Deskendant-Selektoren): hätte funktioniert (höhere
  CSS-Spezifität schlägt die feste `px-3`-Utility auf der Zelle unabhängig von der
  Quellreihenfolge), wurde aber verworfen, weil das Zellen-Padding laut `ledger-x0yga`
  bewusst direkt auf `cell`/`headerCell` gesetzt wurde und dort auch bleiben soll — eine
  zweite, konkurrierende Padding-Deklaration am `<table>` hätte genau das Muster
  reproduziert, das hier gerade als Fehlerquelle beseitigt wird (eine Klasse am falschen
  Ort bestimmt das Aussehen eines Kindelements über eine Stylesheet-Regel, die man beim
  Lesen des Zellencodes nicht sieht).
- **Kassenbuch mit dem jetzt echten `ZebraHover`-Default rendern lassen** (keine
  Ledger-Änderung): hätte den Zebra-Streifen ungefragt in die Kassenbuch-Liste
  eingeführt und die vorhandenen Referenzscreenshots ungültig gemacht — außerhalb des
  Auftrags dieser Aufgabe. Verworfen zugunsten der expliziten `Variant = Hover`-Pinnung.
