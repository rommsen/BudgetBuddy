---
id: design-system-j9xnd
title: "Stats.fs: die Akzentstreifen-Entscheidung als reine Funktion herausziehen und testen"
status: done
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

## Verifier note (iteration 1)

**VERDICT: FAIL** — `likely-fixable`.

**REASONS:**

- AC2 ("Fängt einen neuen oder umbenannten Fall, der versehentlich im Leerstring-Zweig von
  `accentToGradient` landet und damit als farbloser Streifen rendert") ist nicht erfüllt, und
  dieser Diff entfernt den einzigen Mechanismus, der es vorher erfüllt hat. Empirisch geprüft
  in einer Arbeitskopie außerhalb des Worktrees (ein achter Fall `| Cyan` zu `StatAccent`
  ergänzt): gegen HEAD~1 (benannter Fall `| NoAccent -> ""`) meldet `dotnet build`
  `warning FS0025: Unvollständige Mustervergleiche ... Stats.fs(78,32)`; gegen HEAD
  (`| _ -> ""`, `Stats.fs:81-85`) meldet `dotnet build` 0 Warnungen, 0 Fehler, und die volle
  Suite bleibt grün (0 Fehler, 945 erfolgreich, 7 übersprungen). Der neue Fall liefert still
  `Some "absolute top-0 left-0 right-0 h-[3px] bg-gradient-to-r "`, also genau den farblosen
  Streifen, den AC2 benennt.
- Die vier neuen Tests können diese Compiler-Zusicherung nicht ersetzen:
  `StatsAccentTests.fs:21`, `:36-42` und `:63` iterieren fest verdrahtete Listen der sechs
  bzw. sieben bekannten Fälle, ein neuer Fall kommt in keiner davon vor. Nachgewiesen durch
  einen Suite-Lauf über den veränderten Baum: 945 erfolgreich, 0 Fehlschläge, mit
  vorhandenem und unabgebildetem `Cyan`.
- Check 6b: der Diff widerspricht einem `related_adrs`-Eintrag, ohne ihn abzulösen. ADR 0035
  Decision sagt wörtlich: "`accentToGradient` bekommt eine (nie tatsächlich aufgerufene)
  `NoAccent -> ""`-Zeile, damit der Pattern-Match über `StatAccent` weiterhin vollständig
  ist." Der benannte Fall existierte für die Vollständigkeit; er ist jetzt ein Wildcard, bei
  `ADRS_WRITTEN: none` und ohne `supersedes`.
- AC4 hat den Wildcard nicht erzwungen. `accentToGradient` ist `private` und hat nach diesem
  Diff genau einen Aufrufer: `accentBarClassName` in `Stats.fs:98` (per `grep` über den ganzen
  Worktree bestätigt, keine weitere Verwendung). Ein einziger, vollständiger
  Sieben-Fall-Match in `accentBarClassName` erfüllt AC4 und hält den Match mit benannten
  Fällen vollständig, womit auch ADR 0035s Begründung erhalten bleibt. Die Spannung ist die
  Wahl des Workers, nicht die der Aufgabe.

**SUGGESTED_FIX:** Die Farbabbildung in `accentBarClassName` hineinziehen, als einen
vollständigen Match mit sieben benannten Fällen, und die dann aufruferlose private
`accentToGradient` löschen. Etwa:
`let private bar g = Some $"absolute top-0 left-0 right-0 h-[3px] bg-gradient-to-r {g}"`,
dann `match accent with | NoAccent -> None | Teal -> bar "from-neon-teal to-neon-teal" |
Green -> ... | Gradient -> bar "from-neon-teal via-neon-green to-neon-orange"`, ohne Wildcard
an irgendeiner Stelle, sodass ein achter `StatAccent`-Fall wieder `warning FS0025` erzeugt.

Die bestehenden vier Tests, der `Tests.fsproj`-Eintrag und `view`s
`match accentBarClassName props.Accent` können unverändert bleiben. Als gut geprüft und ohne
Nacharbeit: AC1 (`Expect.equal ... None` weist auch `Some ""` zurück), AC3 (der
Voll-Fragment-Check für `Gradient` plus die paarweise Verschiedenheit fangen ein vertauschtes
Paar), AC5 (kein `#` irgendwo in `Stats.fs`), der Zuschnitt (nur die drei vorgesehenen
Dateien; `Styleguide/View.fs` und `standards/frontend/styleguide.md` unberührt),
`readmeDelta: []` (das design-system-README führt `Stats` nur als Inventar-Eintrag, nichts
wird dadurch veraltet) und `dotnet build`/`dotnet test` (0 Warnungen; 945 erfolgreich, 7
übersprungen, 0 Fehler, inklusive `DesignSystemStartupTests`).

**ITERATION_HINT:** likely-fixable

## Outcome

`accentBarClassName : StatAccent -> string option` in `src/Client/DesignSystem/Stats.fs` ist
jetzt der einzige Ort, an dem `StatAccent` in eine Klasse übersetzt wird: `NoAccent` ergibt
`None`, jeder der sechs farbigen Fälle `Some` des vollständigen Klassen-Strings des
Streifen-`div`s. `view` entscheidet nur noch über
`match accentBarClassName props.Accent with | None -> Html.none | Some cls -> Html.div [ prop.className cls ]`
und verzweigt nicht mehr selbst über `props.Accent`. Die frühere separate
`accentToGradient`-Funktion ist entfernt; ihr Gradient-Fragment-Aufbau steckt in einer kleinen
privaten Hilfsfunktion `bar` für den gemeinsamen Klassen-Rahmen. Der Match ist **vollständig
mit sieben benannten Fällen, ohne Wildcard**.

Neue Testdatei `src/Tests/StatsAccentTests.fs` (vier Tests, dieselbe Technik wie
`ledger-w9skm`s `entryRowClassName`: `dotnet test` läuft unter echtem .NET ohne
Fable/React-Laufzeitkontext, prüft also den reinen Klassen-String statt des gerenderten
Feliz-Elements): `NoAccent` liefert `None`; alle sechs farbigen Fälle liefern `Some` mit
`bg-gradient-to-r` und `from-neon-`; jeder Fall trägt seinen erwarteten Farbteil und alle sechs
Ergebnisse sind paarweise verschieden; kein Hex-Literal im Ergebnis (ADR 0031).
`src/Tests/Tests.fsproj:54` führt die neue Datei zwischen `MoneyFormatTests.fs` und
`DesignSystemStartupTests.fs`.

`dotnet build`: 0 Warnungen, 0 Fehler. `dotnet test`: 945 erfolgreich, 7 übersprungen, 0 Fehler.
`npm run build`: grün.

**Iteration 1 wurde verworfen (FAIL), und warum das wichtig war.** Der erste Entwurf erfüllte
AC4 dadurch, dass er in `accentToGradient` den benannten Fall `| NoAccent -> ""` durch einen
Wildcard `| _ -> ""` ersetzte. Formal waren damit alle sieben Kriterien erfüllt, 945 Tests
grün, Build ohne Warnung. Trotzdem war es eine Regression: der Wildcard verschluckt einen
künftigen achten `StatAccent`-Fall stillschweigend und rendert ihn als farblosen Streifen,
also genau den Fehler, den AC2 abfangen soll. ADR 0035 hatte den benannten Fall ausdrücklich
begründet ("damit der Pattern-Match über `StatAccent` weiterhin vollständig ist"), und die vier
neuen Tests iterieren fest verdrahtete Listen der bekannten Fälle, fangen ihn also ebenfalls
nicht. Der Verifier hat das nicht behauptet, sondern gemessen: mit einem Wegwerf-Fall `| Cyan`
meldete `dotnet build` gegen den Vor-Zustand `warning FS0025`, gegen den Nach-Zustand 0
Warnungen bei voller grüner Suite. Er hat außerdem per `grep` belegt, dass `accentToGradient`
nur einen einzigen Aufrufer hatte, der Wildcard also gar nicht nötig war.

**Iteration 2 (PASS)** zieht die Farbabbildung deshalb in `accentBarClassName` hinein und
löscht die dann aufruferlose `accentToGradient`. Der Re-Verifier hat die Gegenprobe selbst
wiederholt statt sie zu glauben: `| Cyan` eingefügt ergibt exakt eine Warnung,
`Stats.fs(93,11): warning FS0025: Unvollständige Musterübereinstimmungen ... der Wert "Cyan" ...`,
mit Fundstelle auf dem `match accent with` in `accentBarClassName`; nach Entfernen wieder 0
Warnungen. `grep -- '| _'` liefert null Treffer über die ganze Datei.

Zusätzlich hat er drei Zusicherungen per **Mutation** geprüft, statt sie nur zu lesen:
`| NoAccent -> None` zu `Some ""` geändert macht Test 1 rot ("NoAccent muss None ergeben");
`| Pink -> bar "from-neon-purple ..."` macht Test 3 rot ("Klasse für Pink muss 'from-neon-pink'
enthalten"). Die sechs Farb-Strings und das Grundgerüst sind beim Umzug byte-identisch
geblieben, Zeile für Zeile gegen die `-`-Zeilen des kumulativen Diffs verglichen. Und:
`git diff HEAD~1 HEAD -- src/Tests/StatsAccentTests.fs` ist leer, kein Test wurde angepasst, um
den Fix zu akzeptieren. Iteration 2 fasst ausschließlich `Stats.fs` an.

Die Schwester-Arbeit aus `design-system-p3vqk` bleibt lauffähig: weder die Typdefinition
`StatAccent` noch `withAccent` sind angefasst, der Demo-Aufruf
`Stats.withAccent "Kontostand" "1.250,40 €" Stats.NoAccent` in
`src/Client/Components/Styleguide/View.fs` kompiliert unverändert.

Offener Stilhinweis des Verifiers, nicht als Mangel gewertet: `bar` gibt `string option` zurück,
verpackt also selbst in `Some`, und lässt sich damit nicht wiederverwenden, um einen nackten
Klassen-String zu erzeugen. Bei einem `private` Helfer mit genau einem Aufrufer und
erklärendem Doc-Kommentar vertretbar.
