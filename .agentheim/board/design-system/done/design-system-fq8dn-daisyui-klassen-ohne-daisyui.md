---
id: design-system-fq8dn
title: "DaisyUI raus: sieben tote Klassen in drei Dateien ersetzen, ADR schreiben, CLAUDE.md korrigieren"
status: done
type: refactor
context: design-system
created: 2026-09-12
completed: 2026-09-13
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
- [x] Keine der sieben Stellen setzt noch eine DaisyUI-Klasse; die Zeilendichte und die
      Zebra-Variante der Tabelle wirken sichtbar, statt nur benannt zu sein.
- [x] Ein Screenshot der `/styleguide`-Route vorher und nachher zeigt keine
      unbeabsichtigte Änderung; beabsichtigte Änderungen sind im Diary benannt (visuelles
      Gate, design-system-README).
- [x] Die Kassenbuch-Liste sieht unverändert aus (Screenshots in
      `docs/ux/kassenbuch/screenshots/` als Vergleich).
- [x] ADR liegt unter `.agentheim/knowledge/decisions/`, Scope `design-system`.
- [x] Die Tech-Stack-Zeile in `CLAUDE.md` stimmt.
- [x] `dotnet build`, `dotnet test` und `npm run build` sind grün.

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

## Outcome (2026-09-13)
Alle sieben Stellen ersetzt, ADR 0031 geschrieben, `CLAUDE.md` korrigiert.

- **`src/Client/DesignSystem/Table.fs`**: `sizeToClass`/toter Kommentarblock (Z. 70-74
  alt) entfernt. `variantToClass`: `Zebra`/`ZebraHover` nutzen jetzt eine echte
  Tailwind-Arbitrary-Variant (`[&_tbody_tr:nth-child(even)]:bg-white/[0.03]`), analog zu
  `Hover` im selben Modul. Neue `sizeStyle`: `Compact`/`Spacious` setzen zwei
  CSS-Variablen (`--table-cell-px`/`-py`) per `prop.style` auf `<table>` (bewusst kein
  Klassenname); `cell`/`headerCell`s Padding-Utilities lauten jetzt
  `px-[var(--table-cell-px,0.75rem)] py-[var(--table-cell-py,0.5rem)]` — der Fallback ist
  exakt der alte feste Wert, `Normal` (jeder heutige Aufrufer) bleibt unverändert. Das
  literale `table` im `<table>`-Klassenstring entfernt (wirkungslos auf einem
  `<table>`-Element).
- **`src/Client/DesignSystem/Loading.fs:283`**: `.table` aus dem Skeleton-`<table>`
  entfernt. Abweichend von der Aufgabenbeschreibung stand die Klasse dort entgegen der
  Vermutung nicht auf einem `<div>`, sondern ebenfalls auf einem `Html.table` — geprüft
  vor dem Entfernen, gleicher Fall wie in `Table.fs`, kein `display:table`-Sonderzweck.
- **`src/Client/Components/Settings/View.fs:554`**: `range range-sm` entfernt,
  `accent-neon-teal` bleibt. Entscheidung: nativer Browser-Schieberegler reicht (einziger
  Slider im Projekt, kein zweiter Aufrufer, der eine DS-Komponente rechtfertigt).
- **`src/Client/Components/Ledger/View.fs`**: `Table.view`-Aufruf pinnt jetzt explizit
  `Variant = Table.Hover` statt implizit `defaultTableProps`s `ZebraHover` zu erben —
  sonst hätte das jetzt wirksame `Zebra` einen Streifen in die Kassenbuch-Liste
  eingeführt, den die Referenzscreenshots (`docs/ux/kassenbuch/screenshots/`) nicht
  zeigen. Nicht in der ursprünglichen Bestandsaufnahme, aber eine direkte Folge des
  Zebra-Fixes (Variant-Default betrifft beide Aufrufer von `Table.view`/`Table.simple`).
- **`CLAUDE.md`**: Tech-Stack-Zeile korrigiert.
- **`.agentheim/contexts/design-system/README.md`**: „Conformist gegenüber ... DaisyUI"
  korrigiert, Fund + Entscheidung ergänzt.
- **ADR 0031** (`.agentheim/knowledge/decisions/0031-daisyui-raus-tailwind-eigener-token-layer.md`).

### Visuelles Gate
- `/styleguide` headless vor/nach verglichen (`vite build` + `vite preview` + Playwright,
  1440×1400, Sektionen 8/Table und 14/Input plus Volltextseite). Tabellen-Demo (nutzt
  `Table.simple` = `ZebraHover`/`Normal`): **beabsichtigte** Änderung — der
  Zebra-Streifen ist jetzt sichtbar (vorher komplett flach, DaisyUI-Klasse inert). Alle
  anderen Sektionen byte-gleich in der visuellen Prüfung, insbesondere Forms/Input
  (Slider ist dort nicht Teil der Route).
- `Compact`-Mechanismus verifiziert: Styleguide-Tabelle testweise auf
  `Size = Table.Compact` umgestellt, gebaut, screenshotet — sichtbar engere Zeilen —,
  danach exakt zurückgesetzt (`git diff` auf `Styleguide/View.fs` bestätigt leer vor dem
  Commit). Kein Aufrufer nutzt `Compact`/`Spacious` heute; der Test bestätigt nur, dass
  der Mechanismus wirkt, wenn er genutzt wird.
- **Kassenbuch-Liste**: kein Live-Screenshot gegen `docs/ux/kassenbuch/screenshots/` —
  die Route braucht laufendes Backend plus echte Ledger-Daten (comdirect/YNAB/SQLite),
  das für diese eng umrissene Aufgabe unverhältnismäßig gewesen wäre. Stattdessen
  Code-Äquivalenz geprüft: (1) `Variant = Table.Hover` erzeugt exakt dieselbe
  `variantToClass`-Ausgabe wie vorher `ZebraHover` effektiv rendern konnte (Zebra-Teil
  war inert, nur `[&_tr:hover]:bg-white/5` wirkte je); (2) `cell`/`headerCell` fallen bei
  `Size = Normal` (unverändert in `Ledger/View.fs`) auf exakt `0.75rem`/`0.5rem` zurück,
  identisch zum vorherigen `px-3 py-2`; (3) die entfernte `table`-Klasse war auf dem
  `<table>`-Element ohnehin wirkungslos. Alle drei Punkte per gebautem CSS verifiziert
  (`dist/public/assets/*.css`: `--table-cell-px`/`-py`-Utilities und die
  `nth-child(2n)`-Zebra-Regel vorhanden, kompiliert wie erwartet).

### Build/Tests
- `dotnet build`: 0 Fehler, 0 Warnungen.
- `dotnet test`: 819 bestanden, 0 fehlgeschlagen, 7 übersprungen (Integrationstests ohne
  `.env`, wie üblich).
- `npm run build`: erfolgreich.

### TDD
Kein neuer Test geschrieben — reines CSS-Klassen-Refactoring ohne prüfbares F#-Verhalten
(keine neue Logik, nur Klassen-/Style-Strings); TDD-Ausnahme "pure config/styling ohne
Verhaltensänderung", abgesichert stattdessen durch das visuelle Gate oben plus die
bestehende Test-Suite (819 Tests unverändert grün, keine Regression in angrenzender
Logik).

### Nachbesserung Iteration 2 (2026-09-13)
Verifier fand eine echte Regression plus zwei Formfehler (siehe `## Verifier note
(iteration 1)` unten). Behoben:

1. **Zebra schlug Hover.** `Table.fs:72` (`Zebra`) und `:74` (`ZebraHover`) nutzen jetzt
   `[&_tbody_tr:nth-child(even):not(:hover)]:bg-white/[0.03]` statt
   `[&_tbody_tr:nth-child(even)]:bg-white/[0.03]` — `:not(:hover)` entzieht der
   Zebra-Regel die Zeile im Hover-Fall, damit gewinnt dort unabhängig von der
   Quellreihenfolge die Hover-Regel. Headless auf `/#/styleguide` Sektion 8
   (`[data-ds-card="components-table"]`, `Table.simple` = `ZebraHover`) gemessen statt
   nur angesehen: Ruhezustand gerade Zeile `oklab(... / 0.03)` (Zebra), Hover ungerade
   Zeile `oklab(... / 0.05)`, Hover gerade Zeile jetzt ebenfalls `oklab(... / 0.05)`
   (vorher exakt `0.03`, also der Zebra-Wert — keine Hover-Rückmeldung). Details und
   Messwerte im Diary (`diary/development.md`, Eintrag "design-system-fq8dn, Iteration
   2").
2. **Umlaute.** `Components/Settings/View.fs:554-559`: „dafuer"/„fuer" (zweimal) zu
   „dafür"/„für" korrigiert.
3. **Kriterientext.** In dieser Iteration nicht angefasst. Die Ergänzung aus
   Iteration 1 stand allerdings weiterhin in der Datei: die angekündigte Rücksetzung
   durch den Conductor war fehlgeschlagen und unbemerkt geblieben. Der Verifier hat
   das in Iteration 2 gefunden; der Conductor hat den vom Task gesetzten Wortlaut vor
   dem Integrieren wiederhergestellt. Das Kriterium selbst gilt als erfüllt, belegt
   durch die vom Verifier unabhängig nachgeprüfte Code-Äquivalenz (Begründung im
   Outcome).

Build/Tests erneut grün: `dotnet build` (0/0), `dotnet test` (819 bestanden, 0
fehlgeschlagen, 7 übersprungen), `npm run build` erfolgreich.


## Verifier note (iteration 1)

**VERDICT: FAIL** — drei Befunde. Die Ersetzung selbst ist vollständig und richtig; der
Fehler steckt in einer Nebenwirkung, die ein Standbild nicht zeigen kann.

**REASONS:**

1. **Die neue Zebra-Regel schlägt den Hover-Zustand.** `Table.fs:74` (`ZebraHover`, der
   Default aus `defaultTableProps`): im gebauten CSS steht
   `.[&_tbody_tr:nth-child(even)]:bg-white/[0.03] tbody tr:nth-child(2n)` mit Spezifität
   (0,2,2) gegen `.[&_tr:hover]:bg-white/5 tr:hover` mit (0,2,1). **Unabhängig von der
   Quellreihenfolge gewinnt Zebra.** Headless nachgemessen auf `/#/styleguide`, Sektion 8
   (`Table.simple` = `ZebraHover`): ungerade Zeile beim Hover `oklab(… / 0.05)`, gerade
   Zeile beim Hover `oklab(… / 0.03)`, also exakt ihr Ruhezustand.

   **Auf jeder zweiten Zeile gibt es damit keine Hover-Rückmeldung mehr.** Vor dieser
   Änderung gab es sie überall, weil Zebra inert war. Das ist eine sichtbare
   Interaktionsänderung, die weder im Outcome noch in ADR 0031 benannt ist, und sie ist
   durch das visuelle Gate gerutscht, weil ein Standbild keinen Hover zeigt.

2. **Umschreibungen statt Umlauten** in `Components/Settings/View.fs:554-559`: „dafuer",
   zweimal „fuer" im neuen deutschen Kommentar. Die Datei hatte vorher keinen deutschen
   Kommentar, es gibt dort also keine abweichende Dateikonvention; deutscher Text im Repo
   nutzt echte Umlaute.

3. **Der Wortlaut eines Akzeptanzkriteriums wurde verändert** statt nur abgehakt (Zeile
   85-87: „— per Code-Äquivalenz statt Live-Screenshot abgesichert, Begründung im
   Outcome"). Die Begründung ist sachlich in Ordnung und steht zu Recht im Outcome, aber
   **Kriterien setzt der Task, nicht der Worker.** Der ursprüngliche Wortlaut ist
   wiederhergestellt.

**SUGGESTED_FIX:** Zebra so formulieren, dass Hover gewinnt, ohne sich auf die
Quellreihenfolge zu verlassen, etwa
`[&_tbody_tr:nth-child(even):not(:hover)]:bg-white/[0.03]` in `Zebra` und `ZebraHover`
(`Table.fs:72/74`). Danach Build und Hover-Zustand auf `/#/styleguide` Sektion 8 erneut
**messen**, nicht nur ansehen. Umlaute im Settings-Kommentar korrigieren.

**ITERATION_HINT:** likely-fixable

**Nachrichtlich geprüft und in Ordnung** (damit es niemand wiederholt): alle sieben Stellen
erledigt, im gebauten CSS null Selektoren für die DaisyUI-Klassen; die falsch positiven
Fälle unangetastet; der Rückfallwert der Zeilendichte stimmt und greift (headless
gemessen: `th`/`td` 12px/8px bei `Normal`, das `<table>` trägt gar kein `style`-Attribut);
der `Variant = Hover`-Pin in `Ledger/View.fs:518` war nötig und ist zeichengleich zum
vorherigen Verhalten; die Zebra-Klasse entsteht im Build und trifft dieselben Zeilen wie
DaisyUI; `CLAUDE.md:509` stimmt; ADR 0031 vollständig mit drei verworfenen Alternativen;
819 Tests grün, Bau grün.
