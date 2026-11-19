---
id: ledger-ef068
title: "Kassenbuch-Liste nach DS: gelbe Kante am Anker in der Kompaktliste mit Maus, Tokens für Auswahl und Rahmen, Kopf „Schnellwahl“ als sectionTitle, „Alles geprüft“ und „Alles kategorisiert“ als Card.allDone"
status: done
type: bug
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-rb3xw]
tags: [bestandsreview, frontend, design-system]
related_adrs: [0049, 0087]
related_research: []
prior_art: [ledger-dn4vy]
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G5, Teil 1 von 4 (Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`).
In der Kompaktliste mit Maus (1024 px neben dem Panel) verliert eine Buchung „zu prüfen“ ihre gelbe Kante, sobald sie
Anker der Spanne ist; §2 („Ruhige Listen“, Zeile „Zu prüfen“) verlangt beide Kanten in einem Schatten, und die Tabelle
macht es schon so. Dazu stehen in der Liste rohe Klassen statt Tokens (§3), ein nachgebauter Abschnittskopf
„Schnellwahl“ und „Alles geprüft“ als leere Liste statt als erledigte Aufgabenliste (§5 Card, §7.12).

## What
- **Kanten der Zeile** (`Ledger/View.fs:458` `mouseRowContainerClassName`, `:742` `selectableRowClassName`): beide
  bilden die Kante über eine reine Funktion `rowEdgeClassName (isAnchor: bool) (needsReview: bool) : string option`:
  Anker und zu prüfen `ListRoles.statusEdgeWithAnchor`, nur Anker `ListRoles.selectedEdge`, nur zu prüfen
  `ListRoles.statusEdge`, sonst keine. Der rohe Schatten `shadow-[inset_2px_0_0_0_var(--color-accent)]` in
  `mouseRowContainerClassName` entfällt. Den Doc-Kommentar (`:452` bis `:456`, „gewinnt gegen die Statuskante“)
  berichtigen.
- **Auswahlfläche**: `bg-accent/[0.07]` in `Ledger/View.fs:450` (`entryRowSelectedClassName`), `:464`, `:666`
  (Kästchen-Container der Handy-Zeile) und `:741` durch `Tokens.ListRoles.selectedFill` ersetzen.
- **Rahmen und Fläche**: `border-border-subtle bg-surface-card` im Sheet „Konto“ (`Ledger/View.fs:3366`) über
  `Tokens.Borders.subtle` und `Tokens.Backgrounds.dark` (das ist `bg-surface-card`; ein `Backgrounds.card` gibt es
  nicht), `border-border-subtle` im Filter-Sheet (`:3485`) über `Tokens.Borders.subtle`.
- **Kopf „Schnellwahl“** (`Ledger/View.fs:3057` in `datePickerBody`) über `BottomSheet.sectionTitle "Schnellwahl"` wie
  die Köpfe beim Sortieren (`:3267`). `datePickerBody` steht im Filter-Popover am PC (`:3161`) und im Filter-Sheet
  (`:3460`), beide bekommen den Kopf.
- **„Alles geprüft“ und „Alles kategorisiert“** (`Ledger/View.fs:2906` bis `:2921`) über
  `Card.allDone "ledger-all-reviewed" "Alles geprüft" (allReviewedText scope)` und
  `Card.allDone "ledger-all-categorized" "Alles kategorisiert" <bisheriger Satz>`. Die Sätze bleiben wörtlich, wie
  heute ohne Aktion. „Kein Treffer“ und „Keine Buchungen“ bleiben `Card.emptyState` (§5 Card: `allDone` nur für eine
  Aufgabenliste, die leer ist, weil alles erledigt ist).

Entscheidungen Conductor 2026-11-19, nach Empfehlung:
- `rowEdgeClassName` steht in `Ledger/View.fs` neben den beiden Nutzern, nicht in `Tokens.fs`: nur das Kassenbuch kennt
  Anker und „zu prüfen“ zugleich.
- Kein DS-Baustein wird geändert.

Betroffene Dateien: `src/Client/Components/Ledger/View.fs` (Zeilenklassen 440 bis 750, Leerzustände 2906 bis 2921,
`datePickerBody` 3053, Sheet „Konto“ 3366, Filter-Sheet 3485), `src/Tests/LedgerCompactInteractionTests.fs`,
`src/Tests/LedgerSelectionTests.fs`, `scripts/ledger-review-state.test.mjs`.

## Acceptance criteria
- [ ] Kaputt-Szenario (Regressionstest, Kommentar nennt den Bug): GIVEN Kompaktliste mit Maus, Zeile gewählt, Anker und
  „zu prüfen“ WHEN `mouseRowContainerClassName` die Klassen bildet THEN enthalten sie `ListRoles.statusEdgeWithAnchor`.
  Vor dem Fix rot. Intakt-Szenarien: nur Anker gibt `ListRoles.selectedEdge`, nur „zu prüfen“ gibt
  `ListRoles.statusEdge`, keins gibt keinen Schatten. Beleg: Expecto über alle vier Fälle von Anker × zu prüfen für
  `mouseRowContainerClassName` und `selectableRowClassName`, Orakel ist eine im Test geschriebene Tabelle der
  erwarteten Tokens (nicht `rowEdgeClassName`). Der Fall „Anker gewinnt gegen die Statuskante“
  (`src/Tests/LedgerCompactInteractionTests.fs:89` bis `:91`) wird dadurch ersetzt.
- [ ] `grep -n 'bg-accent/\[0.07\]' src/Client/Components/Ledger/View.fs` findet nichts, ebenso
  `shadow-\[inset_2px` und die Literale `border-border-subtle` und `bg-surface-card` in den Funktionen um 3366 und 3485.
  Die Literale `"bg-accent/[0.07]"` in `src/Tests/LedgerCompactInteractionTests.fs:88` und
  `src/Tests/LedgerSelectionTests.fs:315` vergleichen danach mit `Tokens.ListRoles.selectedFill` oder entfallen, wenn
  der Fall nichts anderes prüft (Doktrin Punkt 6).
- [ ] Der Kopf „Schnellwahl“ ist `BottomSheet.sectionTitle`. Beleg: Diff; der Sweep-Zustand `kassenbuch-filter-zeitraum`
  und der `[fable]`-Test `scripts/ledger-filter-bar.test.mjs:115` bleiben grün.
- [ ] „Alles geprüft“ und „Alles kategorisiert“ stehen als `Card.allDone` mit den Test-Ids `ledger-all-reviewed` und
  `ledger-all-categorized`. Beleg: der Flow `[flow:pruefliste-abarbeiten]` (`scripts/ledger-review-state.test.mjs:74`)
  sucht die Karte über `getByTestId("ledger-all-reviewed")` statt über `div` mit Text und findet „steht nichts mehr zu
  prüfen“; die Fälle in `src/Tests/LedgerReviewStateTests.fs` bleiben grün.
- [ ] `npm run guard:classes` und `npm run guard:tokens` grün.
- [ ] Romans Blick: am Handy (412 px, Touch) Kassenbuch mit Filter „Zu prüfen“ bis „Alles geprüft“, Sheet „Konto“,
  Filter-Sheet mit Zeitraum; am PC 1024 px Kompaktliste mit Maus, Umschalt-Klick über eine Zeile „zu prüfen“ (beide
  Kanten am Anker), und 1440 px Tabelle. Vorher und nachher über `npm run screenshot:route -- '#/ledger'` und je ein
  Bild der offenen Zustände; Abweichungen nur die genannten. `[human-eye]`
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteile zu den Befunden dieses Teils:
- `Ledger/View.fs:467` gelbe Kante am Anker: bestätigt, ein Bug. `mouseRowContainerClassName` setzt bei Anker nur den
  Akzent-Schatten, `selectableRowClassName` seit dem Nachtrag in §2 beide Kanten. Der Test
  `LedgerCompactInteractionTests.fs:90` schreibt das alte Verhalten fest und wird umgedreht.
- `Ledger/View.fs:741` (`bg-accent/[0.07]`, dazu 450, 464, 666): bestätigt, `Tokens.ListRoles.selectedFill` hat genau
  diesen Wert. Keine sichtbare Änderung.
- `Ledger/View.fs:3366` und `:3485`: angepasst. Den genannten `Tokens.Backgrounds.card` gibt es nicht; der Token für
  `bg-surface-card` heißt `Tokens.Backgrounds.dark`. Keine sichtbare Änderung.
- `Ledger/View.fs:3058` „Schnellwahl“: bestätigt. Heute 12 px Mono mit `tracking-wider`; `sectionTitle`
  (`PickerParts.groupHeader`, `ListRoles.groupHeader`) ist 10 px Mono halbfett mit `.16em`. Der Kopf wird etwas kleiner.
- `Ledger/View.fs:2911` und `:2920` „Alles geprüft“, „Alles kategorisiert“: bestätigt. Heute große gestrichelte Karte
  mit Häkchen 48 px; `Card.allDone` ist eine flache Karte mit grünem Punkt, Kopf 16 px, Satz in `text-secondary` (wie
  „Alles erledigt“ auf der Übersicht, `Overview/View.fs:723`). Nichts geht verloren: beide haben heute keine Aktion.

Entschieden (Roman, 2026-11-19): jeweils die Empfehlung.
- „Alles geprüft“ im Kassenbuch sieht danach aus wie „Alles erledigt“ auf der Übersicht (flache Karte, grüner Punkt)
  statt der großen Karte mit Häkchen. Empfehlung: übernehmen, §5 sieht `allDone` genau dafür vor.

Kann-Sammlung ledger-k4n2s: Punkt 48 (Statuspunkte inline, auch in `Card.allDone`) wird hier berührt, bleibt aber dort.

Überschneidungen: Teil 4 (ledger-rb3xw) ändert im selben `match` den Fall „Keine Buchungen“ und im Filter-Sheet den
Namen des Lena-Filters, daher erst dieser Teil. Teil 2 (ledger-gy77x) und Teil 3 (ledger-zf6zg) arbeiten an anderen
Stellen.

## Outcome

Die Kassenbuch-Liste folgt an den genannten Stellen dem DS. Kein DS-Baustein wurde geändert.

**Kanten der Zeile (Bug).** Neue reine Funktion `rowEdgeClassName (isAnchor: bool) (needsReview: bool) : string option` in `src/Client/Components/Ledger/View.fs` direkt nach `entryRowSelectedClassName`: Anker und zu prüfen ergeben `ListRoles.statusEdgeWithAnchor`, nur Anker `ListRoles.selectedEdge`, nur zu prüfen `ListRoles.statusEdge`, sonst `None`. `mouseRowContainerClassName` und `selectableRowClassName` holen ihre Kante daraus. Der rohe Schatten `shadow-[inset_2px_0_0_0_var(--color-accent)]` ist weg, und der Doc-Kommentar „gewinnt gegen die Statuskante“ ist berichtigt.

**Regressionstest.** `rowEdgeTests` („Kante der Zeile: Anker × zu prüfen (ledger-ef068)“) in `src/Tests/LedgerCompactInteractionTests.fs`. Er hat vier Fälle und prüft jeweils `mouseRowContainerClassName` und `selectableRowClassName`. Das Orakel ist eine im Test geschriebene Tabelle der erwarteten Tokens. Geprüft wird: genau die erwartete Kante steht als Klasse da, keine der anderen beiden, und ein `shadow-` kommt nur zusammen mit einer Kante vor. Vor dem Fix war der Fall „Anker True, zu prüfen True“ rot („Kompaktliste mit Maus: Kante shadow-[inset_3px…,inset_5px…]“), die anderen drei waren grün. Der alte Fall „Anker gewinnt gegen die Statuskante“ ist entfernt.

**Tokens.** Alle vier `bg-accent/[0.07]` (Handy-Zeile gewählt, Maus-Container, Kästchen-Container der Handy-Zeile, Tabellenzeile) sind jetzt `Tokens.ListRoles.selectedFill`. Im Sheet „Konto“ steht `{Tokens.Borders.subtle} {Tokens.Backgrounds.dark}`, im Filter-Sheet `{Tokens.Borders.subtle}`. `grep -n 'bg-accent/\[0.07\]\|shadow-\[inset_2px'` auf View.fs findet nichts mehr. In View.fs stehen `border-border-subtle` und `bg-surface-card` noch an einer Stelle: Zeile 2865 im `ClassName` der Tabelle. Die gehört nicht zu den Funktionen um 3366 und 3485 und bleibt außerhalb des Auftrags. Die Testliterale in `LedgerCompactInteractionTests.fs` und `LedgerSelectionTests.fs:315` vergleichen jetzt mit `Tokens.ListRoles.selectedFill`.

**Kopf „Schnellwahl“.** In `datePickerBody` steht `BottomSheet.sectionTitle "Schnellwahl"`. Das gilt für das Popover am PC und für das Filter-Sheet. Der Kopf ist jetzt 10 px Mono halbfett, wie beim Sortieren.

**„Alles geprüft“ und „Alles kategorisiert“.** Beide sind jetzt `Card.allDone "ledger-all-reviewed" …` bzw. `Card.allDone "ledger-all-categorized" …`. Die Sätze sind wörtlich gleich geblieben, eine Aktion gibt es weiterhin nicht. „Kein Treffer“ und „Keine Buchungen“ bleiben `Card.emptyState`. Der Flow `[flow:pruefliste-abarbeiten]` in `scripts/ledger-review-state.test.mjs` sucht die Karte jetzt über `getByTestId("ledger-all-reviewed")`.

**Gate.** `./verify.sh` endet mit `VERIFY: PASS` (dotnet 5179 grün, node 351 grün, guard:classes und guard:tokens grün). Darin sind auch der Sweep-Zustand `kassenbuch-filter-zeitraum`, der `[fable]`-Test in `scripts/ledger-filter-bar.test.mjs` und `LedgerReviewStateTests` grün.

**Bilder für Romans Blick.** Die Bilder kommen aus gemockten Sweep-Zuständen mit dem HEAD-Bundle (before) und dem Bundle nach der Änderung (after). `npm run screenshot:route` braucht das echte Backend und kann nicht bedienen, darum dieser Weg. Skript: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/ef068-shots.mjs.
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-ef068/kompakt-anker-pruefen-1024-before.png und …-after.png. 1024 px mit Maus, Klick auf Bauhaus (zu prüfen, Anker), dann Umschalt-Klick auf PayPal Shop. Vorher nur die Akzent-Kante, nachher gelb außen und Akzent innen.
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-ef068/tabelle-anker-pruefen-1440-before.png und …-after.png (Tabelle, keine Änderung)
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-ef068/alles-geprueft-412-before.png und …-after.png. 412 px mit Touch, Filter „Zu prüfen“. Vorher die große gestrichelte Karte mit Häkchen, nachher die flache Karte mit grünem Punkt.
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-ef068/alles-geprueft-1280-before.png und …-after.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-ef068/konto-sheet-412-before.png und …-after.png (keine sichtbare Änderung)
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-ef068/filter-zeitraum-412-before.png und …-after.png sowie filter-zeitraum-pc-1280-before.png und …-after.png. Der Kopf „Schnellwahl“ ist etwas kleiner und halbfett.
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-ef068/filter-sheet-412-before.png und …-after.png (keine sichtbare Änderung)
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-ef068/kassenbuch-412-before.png und …-after.png sowie kassenbuch-1440-before.png und …-after.png (keine sichtbare Änderung)
Für „Alles kategorisiert“ gibt es kein eigenes Bild: es läuft durch denselben Baustein.

**README-Delta.** Geprüft mit `node scripts/readme-delta-check.mjs --bc ledger` (Exit 0). „Ganz nachlesen“ meldet keine Einträge.
