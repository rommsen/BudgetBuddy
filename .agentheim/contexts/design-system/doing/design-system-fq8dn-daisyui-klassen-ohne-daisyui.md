---
id: design-system-fq8dn
title: "DaisyUI raus: sieben tote Klassen in drei Dateien ersetzen, ADR schreiben, CLAUDE.md korrigieren"
status: doing
type: refactor
context: design-system
created: 2026-09-12
completed:
depends_on: [design-system-001]
blocks: []
tags: [styleguide, tailwind]
related_adrs: []
related_research: []
prior_art: [ledger-x0yga]
---

## Why
Fund aus der Verifikation von `ledger-x0yga` (2026-09-12): die Kassenbuch-Tabelle hatte
keinen waagerechten Zellenabstand. Ursache war nicht der Seiten-Code, sondern dass
`Table.view` die Klassen `.table` und `.table-sm` setzt, die aus **DaisyUI** stammen.
DaisyUI ist in diesem Projekt nicht vorhanden: nicht in `package.json`, nicht unter
`node_modules/`, und `src/Client/styles.css` importiert ausschliesslich `tailwindcss`.

Die Projektanleitung behauptet das Gegenteil: `CLAUDE.md` führt unter „Tech Stack
Reference" für Styling „TailwindCSS 4.3, DaisyUI".

**Entscheidung Roman (2026-09-13): DaisyUI kommt raus.** Nicht nachrüsten. Das
Design-System trägt sich über den eigenen Token-Layer (`@theme` in `styles.css`,
`standards/frontend/styleguide.md`); ein zweites Gestaltungssystem daneben würde die Frage
aufwerfen, welches gewinnt.

## Bestandsaufnahme (Conductor, 2026-09-13)
Gemacht: alle String-Literale der 48 eigenen Client-Dateien (ohne `fable_modules`) gegen
die Selektoren im gebauten CSS (`dist/public/assets/*.css`) und gegen die bekannten
DaisyUI-Komponentennamen gehalten, danach jede Fundstelle einzeln angesehen.

**Es sind sieben Stellen in drei Dateien.**

| Datei | Zeile | Klasse | Anmerkung |
|---|---|---|---|
| `DesignSystem/Table.fs` | 76 | `table-sm` | `sizeToClass Compact` |
| `DesignSystem/Table.fs` | 78 | `table-lg` | `sizeToClass Spacious` |
| `DesignSystem/Table.fs` | 82, 84 | `table-zebra` | `variantToClass Zebra` / `ZebraHover` |
| `DesignSystem/Table.fs` | 107 | `table` | im Klassen-String des `<table>` |
| `DesignSystem/Loading.fs` | 283 | `table` | `"table w-full"` im Skeleton |
| `Components/Settings/View.fs` | 554 | `range range-sm` | Schieberegler, daneben steht `accent-neon-teal` |

**Wichtig zu `.table`:** die Klasse existiert im gebauten CSS, aber als **Tailwind-Utility
`display: table`**, nicht als DaisyUIs Tabellen-Gestaltung. Auf einem `<table>`-Element ist
sie damit wirkungslos, aber harmlos. In `Loading.fs:283` steht sie auf einem `<div>`, dort
ist `display: table` möglicherweise sogar beabsichtigt. Beim Aufräumen zu prüfen, nicht
blind zu entfernen.

**Falsch positiv und ausdrücklich nicht zu ändern** (damit es niemand ein zweites Mal
prüft): `prop.role "alert"` in `ErrorDisplay.fs` (10 Stellen), `prop.type' "checkbox"` in
`Input.fs` und `TransactionRow.fs` (4), `prop.ariaLabel "Close modal"` in `Modal.fs` (2),
der Fehlertext `"toggle"` in `Rules/State.fs`, und `Money.hero` in der Styleguide-Route.
Das sind HTML-Attribute und eigene Funktionsnamen, keine CSS-Klassen.

## What
1. **Die sieben Stellen ersetzen.** Was die Klasse hätte leisten sollen, ergibt sich aus
   dem Styleguide (`standards/frontend/styleguide.md`), nicht aus DaisyUIs Vorstellung
   davon:
   - `table-sm` / `table-lg`: Zeilendichte. Das Zellen-Padding liegt seit `ledger-x0yga`
     direkt auf `cell` / `headerCell` (`px-3 py-2`); die Grössenvarianten müssen dort
     ansetzen statt über eine Klasse am `<table>`.
   - `table-zebra`: abwechselnde Zeilenhintergründe. Als Tailwind-Variante am Zeilen-Selektor
     lösbar, wie `Hover` es bereits vormacht (`[&_tr:hover]:bg-white/5`).
   - `range` / `range-sm`: der Schieberegler in den Einstellungen. Ohne DaisyUI ist er
     Browser-Standard mit Akzentfarbe. Entscheiden, ob das reicht oder ob er eine eigene
     Gestaltung bekommt; wenn ja, gehört sie ins Design-System und in die Styleguide-Route,
     nicht in die Seite.
2. **ADR schreiben.** Entscheidung, Begründung, verworfene Alternative (DaisyUI nachrüsten).
   Scope `design-system`.
3. **`CLAUDE.md` korrigieren.** Die Tech-Stack-Zeile nennt den tatsächlichen Zustand.
4. **Der Kommentar in `Table.fs:70-74`**, der auf diese Aufgabe verweist und die Klassen
   bewusst stehen liess, wird beim Aufräumen hinfällig und gehört entfernt.

## Acceptance criteria
- [ ] Keine der sieben Stellen setzt noch eine DaisyUI-Klasse; die Zeilendichte und die
      Zebra-Variante der Tabelle wirken sichtbar, statt nur benannt zu sein.
- [ ] Ein Screenshot der `/styleguide`-Route vorher und nachher zeigt keine
      unbeabsichtigte Änderung; beabsichtigte Änderungen sind im Diary benannt (visuelles
      Gate, design-system-README).
- [ ] Die Kassenbuch-Liste sieht unverändert aus (Screenshots in
      `docs/ux/kassenbuch/screenshots/` als Vergleich).
- [ ] ADR liegt unter `.agentheim/knowledge/decisions/`, Scope `design-system`.
- [ ] Die Tech-Stack-Zeile in `CLAUDE.md` stimmt.
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
Bestandsaufnahme am 2026-09-13 vom Conductor gemacht, nachdem Roman vermutete, es sei
wenig; das hat sich bestätigt. Die ursprüngliche Dreiteilung (`design-system-my32n` als
eigener Ersetzungs-Task) ist damit überdimensioniert und wurde aufgelöst; das Ersetzen
steht jetzt hier.

`design-system-thtpp` (Wächter gegen Klassen, die weder Tailwind-Utility noch in
`styles.css` definiert sind) bleibt als eigene Aufgabe bestehen und ist der eigentliche
Schutz davor, dass so etwas zurückkommt.

Bemerkenswert am Fund: er lag seit dem Bau der Table-Komponente vor und ist nie
aufgefallen, weil eine Tabelle ohne Zellenabstand nicht kaputt aussieht, nur eng. Sichtbar
wurde er erst neben einem Entwurf.
