---
id: design-system-j9xnd
title: "Stats.fs: die Akzentstreifen-Entscheidung als reine Funktion herausziehen und testen"
status: doing
type: chore
context: design-system
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [design-system, stats, tests, abdeckung]
related_adrs: [0031, 0035]
related_research: []
prior_art: [ledger-w9skm]
---

## Why

Lücke 4 aus dem QA-Review nach dem Work-Lauf vom 2026-09-19 (erfasst als
`ledger-r7dm2`).

`src/Client/DesignSystem/Stats.fs` hat keinerlei Testabdeckung. Damit ist auch der
siebte `StatAccent`-Fall `NoAccent`, den `ledger-w9skm` eingeführt hat (ADR 0035),
automatisiert nicht belegt: AC4 jener Aufgabe steht nur per Screenshot da. Die
Entscheidung liegt heute inline in `Stats.view` (`match props.Accent with | NoAccent ->
Html.none | _ -> Html.div [...]`, Z. 158 bis 166) und stützt sich auf die private
`accentToGradient`, die für `NoAccent` einen leeren String zurückgibt, der nie
gerendert werden darf. Ein Rückfall auf „leerer String statt gar kein Knoten" sähe im
Code harmlos aus und brächte den dekorativen Streifen als unsichtbaren, aber vorhandenen
DOM-Knoten zurück, den ADR 0035 gerade beseitigt hat.

Dass die Lücke bei einer Kassenbuch-Aufgabe auffiel, macht sie nicht zu einer
Kassenbuch-Aufgabe: `Stats.fs` gehört zum Komponenten-Inventar des Design-Systems, ADR
0035 trägt selbst `scope: design-system`, und Nutznießer des Tests ist jeder künftige
`Stats.view`-Aufrufer.

## What

Die Entscheidung aus `Stats.view` als reine Funktion herausziehen:

    let accentBarClassName : StatAccent -> string option

`NoAccent` ergibt `None`, jeder andere Fall `Some` des vollständigen Klassen-Strings des
Streifens (heute
`"absolute top-0 left-0 right-0 h-[3px] bg-gradient-to-r " + accentToGradient accent`).
`view` entscheidet danach nur noch `match accentBarClassName props.Accent with | None ->
Html.none | Some cls -> Html.div [ prop.className cls ]`. Derselbe Zug, den
`ledger-w9skm` für `entryRowClassName` freiwillig gemacht hat: `dotnet test` kann das
gerenderte Feliz-Element nicht prüfen, den Klassen-String aber schon, und der landet
unverändert am DOM-Knoten.

Dazu eine neue Testdatei `src/Tests/StatsAccentTests.fs` samt Eintrag in
`src/Tests/Tests.fsproj`.

## Acceptance criteria

- [ ] **`NoAccent` liefert gar keine Klasse.** Ein Test in
      `src/Tests/StatsAccentTests.fs` erwartet `accentBarClassName NoAccent = None`.
      Fängt sowohl ein `Some ""` (das den unsichtbaren Streifen-Knoten zurückbrächte,
      den ADR 0035 entfernt hat) als auch einen stillen Rückfall auf `Gradient`.
- [ ] **Alle sechs farbigen Fälle liefern eine Klasse.** Ein tabellengetriebener Test
      über die Liste `[ Teal; Green; Orange; Purple; Pink; Gradient ]` erwartet je
      `Option.isSome`, und der String enthält `"bg-gradient-to-r"` sowie mindestens
      einmal `"from-neon-"`. Fängt einen neuen oder umbenannten Fall, der versehentlich
      im Leerstring-Zweig von `accentToGradient` landet und damit als farbloser Streifen
      rendert.
- [ ] **Jeder Fall trägt seine eigene Farbe.** Derselbe Test prüft je Fall den
      erwarteten Farbteil: `Teal` enthält `"from-neon-teal"`, `Green`
      `"from-neon-green"`, `Orange` `"from-neon-orange"`, `Purple`
      `"from-neon-purple"`, `Pink` `"from-neon-pink"`, `Gradient` den vollständigen
      Verlauf `"from-neon-teal via-neon-green to-neon-orange"`. Zusätzlich: die sechs
      Ergebnisse sind paarweise verschieden. Fängt ein vertauschtes Paar in
      `accentToGradient`, das heute kein Test bemerken würde.
- [ ] **Die Entscheidung steht nur noch an einer Stelle.** In
      `src/Client/DesignSystem/Stats.fs` kommt der Bezeichner `NoAccent` außerhalb der
      Typdefinition `StatAccent` nur noch innerhalb von `accentBarClassName` vor; `view`
      entscheidet über `accentBarClassName props.Accent`, nicht mehr selbst über
      `props.Accent`.
- [ ] **Keine Hex-Literale.** `accentBarClassName` liefert ausschließlich
      Tailwind-Klassennamen; `src/Client/DesignSystem/Stats.fs` enthält in diesem Pfad
      kein `#`-Farbliteral (ADR 0031: Farben laufen über den Token-Layer).
- [ ] `dotnet build`, `dotnet test` und `npm run build` grün,
      `src/Tests/Tests.fsproj` führt die neue Datei.

## Notes

**Herkunft der Testdefinitionen.** Der Elterntask `ledger-r7dm2` behauptet, der QA-Review
liege als Volltext im Diary-Eintrag vom 2026-09-19 20:14 vor. Das stimmt nicht; die
Kriterien oben sind aus `src/Client/DesignSystem/Stats.fs` abgeleitet.

Überschneidet sich thematisch, aber nicht in Dateien, mit `design-system-p3vqk`
(`NoAccent` im Styleguide und in `standards/frontend/styleguide.md` nachziehen). Jener
Task fasst `src/Client/Components/Styleguide/View.fs` und die Stilrichtlinie an, dieser
`src/Client/DesignSystem/Stats.fs` und eine Testdatei. `p3vqk` braucht den Umbau nicht:
er benutzt den bereits öffentlichen Fall `NoAccent` als Demo-Karte. Beide können
parallel laufen, ohne Reihenfolge.

Die Testdatei liegt neben `DesignSystemStartupTests.fs`, dem bereits bestehenden
mechanischen Wächter über dasselbe Verzeichnis.
