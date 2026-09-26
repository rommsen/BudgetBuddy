---
id: ledger-j8xq4
title: "Kassenbuch-Filterleiste und Summenzeile: Chips, Kategorienauswahl mit Mehrfachauswahl, Popover am PC"
status: done
type: feature
context: ledger
created: 2026-09-19
completed:
depends_on: [ledger-vgfw3, design-system-001]
blocks: [ledger-r1d2f]
tags: [frontend, kassenbuch, scheibe]
related_adrs: [0004, 0005, 0043]
related_research: []
prior_art: [ledger-b1xwe, ledger-x0yga]
---

## Why
Die Oberflächen-Hälfte der zweiten lesenden Scheibe. `ledger-vgfw3` baut die Filter im
Lesepfad; ohne Bedienelemente kann Roman sie nicht benutzen. Beide Hälften zusammen decken
seinen häufigsten Arbeitsablauf ab: Buchungen finden.

Zwei Dinge sollen besser sein als in YNAB-Web: mehrere Kategorien auf einmal, und der
Filter muss am Handy genauso gut gehen. Roman nutzt ihn dort heute nie, weil er zu
umständlich ist. Genau daran misst sich diese Aufgabe.

## What
Vorlage ist `docs/ux/kassenbuch/handoff-ablauf-2-buchungen-finden.md`, sechzehn Screens
mit verbindlichen Werten.

**Sticky Filterleiste aus Chips** über der Liste, auf beiden Geräten: Kategorie,
Zeitraum, Payee, Betrag, Memo. Der Filter wirkt immer serverseitig über
`ledger-vgfw3`, nie im Client über die geladene Seite.

- **Kategorie** öffnet die Kategorienauswahl mit **Mehrfachauswahl**. Versteckte
  Kategorien stehen eingeklappt am Ende („Versteckt (n)"), damit sie nicht versehentlich
  getroffen werden. Am Handy als BottomSheet, am PC als Popover.
- **Click-Commit** (Styleguide 7.1, ADR 0005): die Mehrfachauswahl wirkt erst auf
  „Übernehmen (n)", nicht bei jedem Antippen. Einzelauswahl (Payee) filtert sofort.
- **Zeitraum** mit Schnellwahl (dieser Monat, letzter Monat, dieses Jahr, letzte zwölf
  Monate) und Von/Bis. Die Schnellwahl **schreibt Von und Bis mit**, damit sichtbar ist,
  was gefiltert wird.
- **Payee**, **Memo**, **Betrag** als Suchfelder mit kurzer Verzögerung beim Tippen.
  Die Payee-Vorschläge kommen mit Anzahl aus dem aktuellen Treffer (Abfrage aus
  `ledger-vgfw3`), nicht aus allen Buchungen.
- **Summenzeile** über der Liste, sobald ein Filter aktiv ist: Trefferzahl und
  Gesamtbetrag über den ganzen Treffer. Ohne aktiven Filter steht dort nichts. Damit ist
  Romans Steuererklärungs-Fall ohne Ankreuzen erledigt.
- **Am Handy wandern aktive Chips nach vorn**, weil die Leiste waagerecht scrollt und der
  aktive Filter sichtbar bleiben muss. Am PC bleibt die Reihenfolge fest.
- Beschriftung im Aktivzustand: Zähler bei Mehrfachauswahl („Kategorie: 2"), Kurzform beim
  Zeitraum („12 Monate"), Klartext beim Payee („Café Lotte").
- Am PC hängt die Auswahl als **Popover unter seinem Chip**, die Liste bleibt sichtbar,
  `Esc` und Klick daneben schließen ohne zu filtern. Ein Muster für Kategorie, Zeitraum
  und Payee, später auch für die Kategoriezuweisung (Ablauf 4) und den Flag-Chip
  (Ablauf 5).
- Jeder Chip trägt ein „×", am Ende der Leiste „Alle Filter löschen".
- Filterwechsel springt an den Listenanfang und setzt die Seite zurück, die Chips bleiben
  sichtbar (Styleguide 7.3).
- **Leerzustand ohne Treffer** sagt, was als Nächstes zu tun ist („Kein Treffer. Filter
  lockern oder alle Filter löschen.").

**Bewusst nicht in dieser Aufgabe:** das Verhalten „Passt eine Zeile nach einer Änderung
nicht mehr zum aktiven Filter, bleibt sie ausgegraut an ihrem Platz" (Entscheidung aus
Ablauf 5). Es setzt voraus, dass eine Zeile in BB überhaupt geändert werden kann, und
hängt damit an `ledger-r777c` beziehungsweise der v2-Entscheidung. Solange das Kassenbuch
nur liest, tritt der Fall nicht ein. Wird er später gebaut, ist er ein eigener Task, weil
er einem blinden Neuladen nach jeder Änderung widerspricht.

## Acceptance criteria
- [ ] Ein Kategorie-Filter über **zwei** Kategorien lässt sich am Handy und am PC setzen,
      und die Liste zeigt danach die Vereinigung beider.
- [ ] Die Mehrfachauswahl wirkt erst auf „Übernehmen", nicht beim Antippen einer
      einzelnen Kategorie (Click-Commit, ADR 0005). Durch einen Test über den Reduzierer
      belegt, nicht nur per Augenschein.
- [ ] Versteckte Kategorien sind eingeklappt und erst nach einem zusätzlichen Schritt
      erreichbar.
- [ ] `YnabCategory` (`src/Shared/Domain.fs`) trägt `Hidden: bool`, dekodiert aus YNABs
      `hidden`-Feld in beiden Kategorie-Decodern (`categoryDecoder` und
      `categoryInGroupDecoder` in `src/Server/YnabClient.fs`). Ein Decoder-Test belegt:
      `hidden: true` ergibt `Hidden = true`, fehlendes Feld ergibt `false`. Die
      Einklapp-Regel oben speist sich aus diesem Feld, nicht aus einer Namenskonvention.
- [ ] Die Kategorienauswahl zeigt je Kategorie `YnabCategory.Available`, wenn es `Some`
      ist, rechtsbündig wie im Design-System-Muster `components-categorypicker`; bei `None`
      bleibt die Stelle leer. Kein neuer Spiegel-Lauf und keine neue Tabelle dafür: das
      Feld kommt seit dem Import-Ablauf aus `getCategories`.
- [ ] Die Zeitraum-Schnellwahl trägt Von und Bis sichtbar ein, statt sie nur intern zu
      setzen.
- [ ] Die Summenzeile erscheint genau dann, wenn mindestens ein Filter aktiv ist, und
      zeigt Trefferzahl und Summe über den ganzen Treffer, nicht über die geladene Seite.
      Mit mehr Treffern als einer Seite belegt.
- [ ] Am PC schließt `Esc` das Popover, ohne den Filter anzuwenden; der vorherige
      Filterzustand bleibt unverändert.
- [ ] Am Handy stehen aktive Chips vorn, am PC bleibt die Reihenfolge fest. Über den
      Reduzierer belegt, der die Chip-Reihenfolge liefert, nicht über einen Screenshot.
- [ ] „×" am Chip entfernt genau diesen Filter, „Alle Filter löschen" alle; die
      Summenzeile verschwindet danach wieder.
- [ ] Ein Filterwechsel setzt die Seite auf die erste zurück, statt die bisherige
      Seitennummer beizubehalten.
- [ ] Ohne Treffer steht ein Leerzustand da, der eine nächste Handlung nennt.
- [ ] Screenshots in beiden Viewports neben den Screens 2.1 bis 2.14 des Übergabepakets
      zeigen keine sichtbare Abweichung in Chip-Zuständen, Picker und Summenzeile.
      [human-eye]
- [ ] Die Filterleiste bleibt am Handy und am PC beim Scrollen der Liste sichtbar (sticky
      unter der Navbar). Per Browsertest belegt, Messwerte in einem einzigen
      `page.evaluate` gelesen.
- [ ] Der sticky Tabellenkopf am PC klebt direkt unter der Filterleiste: nach dem Scrollen
      gilt `thead.top >= leiste.bottom - 1` und `thead.top - leiste.bottom <= 2`. Belegt
      per Browsertest in drei Zuständen: 1440x900 ohne Filter, 1024x900 mit zwei aktiven
      Kategorien (Chips umbrochen), und 1024x900 **während eines laufenden Spiegel-Laufs**
      (veralteter `getLastMirrorRun`, `mirrorYnabBudget` noch offen, Hinweis "Spiegel
      läuft" sichtbar). Ein weiterer Test belegt, dass der Abstand stimmt, nachdem der
      Spiegel-Lauf endet und der Hinweis wieder verschwindet.
- [ ] Die Höhenmessung folgt dem Knoten, in den die Leiste tatsächlich rendert, über einen
      Callback-Ref an `ChipBar.bar`: wechselt React den Knoten, trennt der Observer den
      alten und beobachtet den neuen; beim Unmount wird er getrennt. Kein `useEffect` mit
      leeren Abhängigkeiten, das den Knoten einmalig beim Mount festhält.
- [ ] Außerhalb der Kassenbuch-Seite klebt `.table-sticky-thead` unverändert direkt unter
      der Navbar (Versatz-Variable fällt auf `0px` zurück); der Vertrag des
      `Sticky`-Felds in `src/Client/DesignSystem/Table.fs` stimmt mit dem Verhalten überein.
- [ ] Jeder neue Browsertest läuft fünfmal hintereinander grün, und nach
      `npm run test:scripts` zeigt `git status --short` nur beabsichtigte Änderungen.
- [ ] `dotnet build` und `dotnet test` grün, `npm run build` sauber.

## Notes
Abgespalten von `ledger-vgfw3` am 2026-09-19 beim Verfeinern. `vgfw3` behält die Id, weil
`ledger-r1d2f` und der Oberflächen-Abschnitt des ledger-README darauf verweisen; diese
Aufgabe übernimmt die Oberfläche und blockt nun ihrerseits `ledger-r1d2f` (die Auswahl mit
Summe setzt die Summenzeile voraus, nicht die Server-Filter).

Die neu zu bauenden Bausteine sind im Übergabepaket am Ende aufgelistet
(Kategorienauswahl mit Mehrfachauswahl, Filter-Popover, Chip-Leiste mit Zähler und „×",
Summenzeile, Schnellwahl Zeitraum, Payee-Vorschlagsliste, Konto-Kürzel-Pill). Sie gehören
als echte Komponenten ins Design-System, nicht als Inline-Feliz in die Kassenbuch-Seite.

Der Chip „Zu prüfen" existiert bereits (`ledger-kdnnz`) und ist in die neue Leiste
einzuordnen, nicht neu zu bauen. Der Available-Betrag je Kategorie im Picker kommt aus
`ledger-kwwxz`; ohne ihn zeigt der Picker die Kategorien ohne Betrag, was hier kein
Fehler ist.

**Chip "ohne Kategorie" (erfasst am 2026-09-20 bei der Verfeinerung von `ledger-xk9wq`):**
Die Kategorienauswahl dieser Leiste speist sich aus `getCategories` gegen YNAB, also aus dem
Katalog, nicht aus dem Bestand des Kassenbuchs. Zwei Fälle fallen deshalb durch jeden
Kategorie-Filter, egal wie gut er gebaut ist: eine Buchung ohne Kategorie, und eine
aufgeteilte Buchung, deren Split-Zeilen alle `Uncategorized` sind. Beide sind fachlich
"unkategorisiert", aber es gibt keine Kategorie, die man auswählen könnte, um sie zu finden.
Das ist genau die Menge, die Roman beim Durchsehen sucht. Ein eigener Chip "ohne Kategorie"
neben der Kategorienauswahl löst es; `ledger-xk9wq` deckt es ausdrücklich nicht ab und hält
die Grenze in ADR 0038 fest. **Nicht Teil dieser Aufgabe:** der Chip ist als eigenes
Backlog-Item `ledger-saqsk` erfasst (2026-09-25); er braucht einen eigenen Filterzweig im
Lesepfad und eine Produktentscheidung zu Überträgen ohne Kategorie.

**Am Code nachgesehen bei der Verfeinerung am 2026-09-25:**

- `YnabCategory` hat **kein** `Hidden`-Feld; `categoryDecoder` und `categoryInGroupDecoder`
  (`src/Server/YnabClient.fs`, ab Zeile 55) lesen `id`, `name`, `category_group_name` und
  das Available. YNABs Kategorie-Objekt führt `hidden` (bool) und `deleted` (bool). Für die
  eingeklappte Gruppe "Versteckt (n)" muss `hidden` mit durch den Vertrag, deshalb das
  Kriterium oben. Gelöschte Kategorien (`deleted: true`) sind heute schon nicht im Katalog
  und bleiben draußen.
- `YnabCategory.Available: Money option` existiert bereits (Import-Ablauf, mit
  `applyReadyToAssign` für die Inflow-Kategorie). Der Picker im Kassenbuch kann es sofort
  zeigen. `ledger-kwwxz` Punkt 1 und 2 (Spiegel führt Budget-Zahlen, Picker zeigt Betrag)
  sind damit für den Picker abgedeckt, ohne den Spiegel zu erweitern; was von `kwwxz`
  bleibt, ist Punkt 3 (Hinweis beim Wechsel, v2) und die Frage, ob der Betrag auch ohne
  YNAB-Erreichbarkeit stehen soll. Das ist dort zu entscheiden, nicht hier.
- Dateien, die diese Aufgabe voraussichtlich anfasst: `src/Shared/Domain.fs`
  (`YnabCategory.Hidden`), `src/Server/YnabClient.fs` (zwei Decoder),
  `src/Client/Components/Ledger/State.fs` und `View.fs` (Chips, Summenzeile, Popover,
  Reduzierer-Zustand für Mehrfachauswahl und Chip-Reihenfolge), neue Komponenten unter
  `src/Client/DesignSystem/` (Chip-Leiste, Filter-Popover, Kategorienauswahl mit
  Mehrfachauswahl, Summenzeile), `src/Client/Components/Styleguide/View.fs` (die neuen
  Komponenten gehören als Karten in die `/styleguide`-Route, `design-system-t3vqr`),
  `src/Tests/LedgerViewTests.fs` und `src/Tests/YnabClientTests.fs`.

## Stand und Wiederaufnahme (Refine 2026-09-26)

Nach drei Verifikationsrunden eskaliert, danach mit Roman nachgeschärft. Der Task bleibt in
`doing/`; der nächste `work`-Lauf nimmt ihn über die Recovery (Phase 1) im **bestehenden
Worktree** `.worktrees/ledger-j8xq4` (Branch `aw/ledger-j8xq4`) wieder auf und baut auf dem
dort committeten Stand aller drei Iterationen auf, statt neu anzufangen. Sicherung, falls
der Worktree verloren geht: `.agentheim/salvage/ledger-j8xq4-escalated-iter3.patch`
(Code) und `.bookkeeping.md` (README-Delta, ADR-Entwurf 0043, Outcome, Backlog-Item
`design-system-bz0gn`).

**Bereits verifiziert** (über die drei Runden, siehe Verifier-Notizen unten): alle
Filter-Kriterien oben einschließlich Handy-BottomSheet (390x844), Click-Commit,
Zeitraum-Schnellwahl, Summenzeile, Leerzustand, Deep-Link-Nachladen der Kategorien,
`YnabCategory.Hidden`, die Sticky-Leiste und der Tabellenkopf-Abstand **im Normalzustand**
(1440 und 1024 mit Filter jeweils 0px), die auf die Kassenbuch-Seite begrenzte
Versatz-Variable. Diese Teile nicht umbauen.

**Offen ist genau ein Punkt:** während eines laufenden Spiegel-Laufs misst der
ResizeObserver in `FilterBarColumn` (`src/Client/Components/Ledger/View.fs`) den falschen
Knoten. Ursache: der Observer wird einmal in einem `useEffect` mit `[||]` an den Knoten
gebunden, den `barRef` beim Mount hat; die Kinder der Spalte sind eine Liste ohne Keys, an
erster Stelle steht bedingt `if model.MirrorRunning then Loading.inlineWithText ...`. Wird
`MirrorRunning` wahr, verwendet React den alten Leisten-`div` für den Hinweis weiter und legt
für die Leiste einen neuen Knoten an. Folge: bis zu 42px Überdeckung des Tabellenkopfs bei
1024x900 mit Filter.

**Entscheidung (Roman, 2026-09-26):** Callback-Ref an `ChipBar.bar`, der beim Knotenwechsel
den alten Observer trennt und den neuen Knoten beobachtet. Nicht gewählt: den Hinweis hinter
die Leiste verschieben (ändert die sichtbare Reihenfolge) und stabile Keys allein (schützt
nur, solange niemand die Keys vergisst). Stabile Keys dürfen zusätzlich gesetzt werden.
Die Entscheidung gehört als Nachtrag in ADR-Entwurf 0043 (Abschnitt "Nachtrag"), der mit
diesem Task integriert wird.

## Verifier note (iteration 1)

REASONS:
- Check 1, criterion 1 ("Ein Kategorie-Filter über zwei Kategorien lässt sich am Handy und am PC setzen, und die Liste zeigt danach die Vereinigung beider") has no evidence for the phone half. The two-category union is covered device-independently by the reducer test "'Übernehmen' schreibt den Entwurf in den Filter, schließt das Popover und lädt ab Seite 0 neu" (LedgerPageTests.fs). The desktop half is covered by the Playwright test "Zwei Kategorien gewählt und übernommen zeigen die Vereinigung ..." (scripts/ledger-filter-bar.test.mjs), but that file only opens a 1440x900 context. On a phone, `filterChrome` (View.fs) takes the other branch and renders `BottomSheet.view` instead of `FilterPopover`. No test in the diff ever renders or drives that branch. The worker filed follow-up ledger-fw3mp for it, but the criterion names the phone explicitly, and the task's ## Why says this is what the task is measured by, so a deferred follow-up does not satisfy it now.
- Stale doc comments contradict ADR 0043 and the code. src/Client/DesignSystem/ChipBar.fs `chipWithPopover` says "der Aufrufer rendert beide Varianten nebeneinander, je hinter `Primitives.Responsive.desktopOnly`/`mobileOnly`". The module header of src/Client/DesignSystem/FilterPopover.fs says the same. That is exactly the dual-render approach ADR 0043 rejects.
- The "Sticky Filterleiste" from ## What is not implemented. `ChipBar.bar`'s class list has no `sticky`, and there is no sticky wrapper in Ledger/View.fs. The doc comment on `bar` ("Die Leiste selbst: sticky, ...") is therefore false (Styleguide 7.3).
- Everything else checked out: full suite green (dotnet test 1099/0 failed, test:scripts 106/106, guard clean), browser test stable, clean tree afterwards, deep-link fix cannot loop and is tested, ADR 0043 reasoning and 768px breakpoint consistent.

SUGGESTED_FIX: Add a second Playwright context at 390x844 to scripts/ledger-filter-bar.test.mjs. It should show that tapping the category chip opens `.bottom-sheet.active` and no popover, that picking two categories and tapping "Übernehmen (2)" shows their union in the mobile list and in the summary row, and that the active chip comes before "Zu prüfen" in the DOM. That absorbs ledger-fw3mp. Also make `ChipBar.bar` actually sticky, and fix the ChipBar and FilterPopover doc comments so they describe the render-time viewport choice from ADR 0043.

ITERATION_HINT: likely-fixable

## Verifier note (iteration 2)

REASONS:
- The three points from the iteration-1 note are handled on paper: phone tests at 390x844 (BottomSheet, no popover, union in mobile list and summary, active chip before "Zu prüfen"), doc comments match ADR 0043, suite green (dotnet test 1099/0 failed, test:scripts 109/109, filter-bar test stable), clean tree, no leftover process.
- The sticky filter bar fix still collides with the sticky table head. Measured in a real browser (worktree build, repo fixtures, 40 rows, scrolled). The table head is shifted by a fixed `--ledger-filter-bar-height: 44px` (`:root` and `.table-sticky-thead thead { top: calc(64px + var(--ledger-filter-bar-height)) }`), but the bar's real height differs: at 1440x900 the bar is 38px high (top 64, bottom 102) and the head sticks at 108, leaving a 6px strip where scrolled rows show through; at 1024x900 with a category filter active the chips wrap (`md:flex-wrap`), the bar is 62px high (bottom 126) and covers 18px of the head (DATUM/PAYEE/KATEGORIE labels cut off); at 800x900 with a filter active the bar is 76px high and covers 32px. The 44px is a guess, not tied to the rendered height, and the summary's claim "ohne Kollision mit dem sticky Tabellenkopf" does not hold.
- The fix leaks into the Design System: `.table-sticky-thead` is what `Table.view`'s `Sticky = true` emits (src/Client/DesignSystem/Table.fs:103). A ledger-specific offset now lives in `:root` with a non-zero global value, so any future sticky table on a page without a filter bar would stick 44px below the navbar. The documented contract of `Sticky` (Table.fs:49, 98-101) is now wrong.
- Mobile is fine: at 390x844 the bar sticks at top 56, with or without a filter.

SUGGESTED_FIX: Limit the offset to the ledger page: `.table-sticky-thead` reads a variable whose default is 0px, and only the ledger page sets it. Tie the value to the bar's real height, not a fixed 44px (e.g. a ResizeObserver/effect writing the measured height into the variable, or a layout where the head cannot overlap the bar). Add a browser test at about 1024x900 with a filter active asserting, after scrolling, `thead.getBoundingClientRect().top >= bar.getBoundingClientRect().bottom` with a gap of at most 1 to 2px; also check at 1440 that the gap is gone.

ITERATION_HINT: likely-fixable

## Verifier note (iteration 3)

REASONS:
- The iteration-2 points hold in the steady state (measured independently): at 1440x900 bar bottom 102 = thead top 102; at 1024x900 with two categories active 126 = 126; at 800x900 with a filter active 140 = 140; at 390x844 the bar sticks at 56. `.table-sticky-thead thead` reads `var(--ledger-filter-bar-height, 0px)`, the Table.fs `Sticky` contract holds again, the ResizeObserver is disconnected on unmount, no render loop.
- New defect: the measurement follows the wrong element while a mirror run is active. `FilterBarColumn` (src/Client/Components/Ledger/View.fs, around lines 1250 to 1290) attaches `observeHeight` once, in a `useEffect` with `[||]` deps, to the DOM node `barRef` points to at mount. The column's children are a keyless, index-reconciled list whose first entry is the conditional `if model.MirrorRunning then Loading.inlineWithText ...`. When `MirrorRunning` turns true, React reuses the original bar `div` at index 0 for the loading hint and creates a new node for the bar at index 1. The observer keeps watching the old node (now the hint) and writes its height. Measured with a stale `getLastMirrorRun` (triggers the automatic mirror run from ADR 0014, a common case when opening the page) and a pending `mirrorYnabBudget`: at 1440x900 the variable reads 20px, thead sticks at 84, bar ends at 102 (18px overlap); at 1024x900 with a filter active thead at 84, bar ends at 126 (42px overlap), column labels completely hidden. The collision tests always mock a fresh mirror status, so they never cover this case.
- Everything else passed: dotnet test 1099/0 failed, test:scripts 111/111, filter-bar test 12/12 repeatedly, clean tree, no leftover process, README_DELTA/ADR 0043/OUTCOME consistent with the code, no new dash punctuation.

SUGGESTED_FIX: Make the observer follow the node the bar really renders into: e.g. a callback ref on `ChipBar.bar` that disconnects the old observer and observes the new node whenever React swaps it; or stable keys for the column children; or move the conditional mirror hint after the bar so the bar's DOM node never changes. Add a browser test at 1024x900 with a stale `getLastMirrorRun` and a pending `mirrorYnabBudget` asserting the same 0 to 2px gap while "Spiegel läuft" is visible.

ITERATION_HINT: likely-fixable

## Salvage note

Nach drei Verifikationsrunden an Roman eskaliert (2026-09-26). Der Worktree `.worktrees/ledger-j8xq4` (Branch `aw/ledger-j8xq4`) bleibt mit dem vollständigen Stand aller drei Iterationen erhalten.
- Code-Diff gesichert: `/Users/romansachse/src/BudgetBuddy/.agentheim/salvage/ledger-j8xq4-escalated-iter3.patch`
- Buchführungsblöcke (README_DELTA, ADR-Entwurf 0043, OUTCOME, Backlog-Item design-system-bz0gn) gesichert: `/Users/romansachse/src/BudgetBuddy/.agentheim/salvage/ledger-j8xq4-escalated-iter3.bookkeeping.md`

## Outcome

Die Filterleiste über der Kassenbuch-Liste ist gebaut: eine sticky Chip-Leiste
(Kategorie, Zeitraum, Payee, Memo, Betrag, plus der bestehende Chip "Zu prüfen") mit
Popover am PC und BottomSheet am Handy für dieselben Inhalte, Click-Commit bei der
Kategorien-Mehrfachauswahl (erst "Übernehmen" schreibt den Entwurf in den Filter),
Zeitraum-Schnellwahl, die Von/Bis sichtbar einträgt, Payee-Einzelauswahl mit
Vorschlägen aus dem aktuellen Treffer, Memo-/Betrags-Suchfelder mit Verzögerung, eine
Summenzeile über der Liste (Trefferzahl und Summe über den ganzen Treffer, nicht die
geladene Seite), "×" je Chip und "Alle Filter löschen" am Ende der Leiste, und einen
Leerzustand ("Kein Treffer. Filter lockern oder alle Filter löschen.") mit eigener
Handlung.

**Backend/Shared:** `YnabCategory` (`src/Shared/Domain.fs`) trägt `Hidden: bool`,
dekodiert aus YNABs `hidden`-Feld in beiden Kategorie-Decodern
(`Server.YnabClient.Decoders.categoryDecoder`/`categoryInGroupDecoder`), fehlendes
Feld dekodiert konformistisch zu `false`. Vier Decoder-Tests in `YnabClientTests.fs`.

**Client-Zustand** (`src/Client/Components/Ledger/Types.fs`, `State.fs`): Typen
`Filters`, `DateQuick`/`DateRangeFilter`, `ChipId`/`ChipActivity` und die reine
Ordnungsfunktion `orderChipsForDisplay` (am Handy wandern aktive Chips nach vorn, am
PC bleibt die Reihenfolge fest, belegt über den Reduzierer). `Model` trägt
`Categories`, `Filters`, `OpenPopover`, Entwurfsfelder je Filtertyp und
`TotalAmount`. `RunLoadEntriesPage` verdrahtet `model.Filters` in die
Server-Abfrage.

**Design System:** zwei Module, `Client.DesignSystem.ChipBar` (Chip als zwei echte
Knöpfe in einem Rahmen, kein Knopf-im-Knopf) und `Client.DesignSystem.FilterPopover`
(PC-Popover, Esc/Klick außerhalb schließen ohne zu filtern).

**View** (`src/Client/Components/Ledger/View.fs`): Kategorienauswahl mit
Mehrfachauswahl, Zeitraum-Popover, Payee-Vorschlagsliste, Memo-/Betrags-Suchfelder,
`filterChipBar` und `summaryRow`. Der alte, separat gerenderte Chip "Zu prüfen" ist
in dieselbe `ChipBar`-Optik überführt, seine Logik unverändert.

**ADR 0043:** welche Chrome (Popover/BottomSheet) gerendert wird, entscheidet
`Viewport.isDesktopViewport()` zur Renderzeit, nicht das sonst auf der Seite übliche
CSS-Doppel-Render-Muster: ein `BottomSheet` portalt immer nach `document.body`, ein
Doppel-Render hätte zwei echte Overlays für denselben Zustand erzeugt (im ersten
Anlauf tatsächlich passiert, durch den Browsertest aufgedeckt).

**Kritischer Nebenfund der ersten Iteration, mit Regressionstest behoben:** Lädt man
die Kassenbuch-Seite per Deep-Link direkt, läuft `pageLoadCmd Ledger`s
`LoadCategories` synchron im selben `Cmd.batch` wie
`Components.Settings.Types.LoadSettings`, und findet `Settings.Settings` dabei
strukturell IMMER noch nicht `Success`. Ohne Gegenmaßnahme blieb `Ledger.Categories`
für immer `NotAsked`. Behoben in `src/Client/State.fs`s `SettingsMsg`-Zweig: sobald
`SettingsLoaded (Ok settings)` mit konfiguriertem Budget eintrifft, wird
`LedgerMsg LoadCategories` nachgeholt, wenn die Kassenbuch-Seite noch nichts hat.
Fünf Tests in `src/Tests/LedgerCategoriesSyncTests.fs`.

### Zweite Iteration: Handy-Beleg, stale Doc-Kommentare, erster Sticky-Versuch

Der Verifier der ersten Iteration bemängelte: kein Handy-Beleg für
Akzeptanzkriterium 1 (Kategorie-Filter über zwei Kategorien, "am Handy und am PC"),
zwei stale Doc-Kommentare, die noch das verworfene CSS-Doppel-Render-Muster
beschrieben, und eine Filterleiste, die trotz Aufgabentext ("Sticky Filterleiste ...
auf beiden Geräten", Styleguide 7.3) tatsächlich nicht sticky war.

`scripts/ledger-filter-bar.test.mjs` bekam einen zweiten Playwright-Kontext im
390x844-Viewport (Kategorie-Chip öffnet `.bottom-sheet.active`, nie ein Popover;
zwei übernommene Kategorien zeigen dieselbe Vereinigung wie am PC; der aktive Chip
steht im DOM vor "Zu prüfen"), die beiden Doc-Kommentare wurden korrigiert, und die
Filterleiste bekam `position: sticky`. Für den Versatz zwischen der jetzt sticky
Leiste und dem bereits sticky Tabellenkopf setzte diese Iteration eine feste
globale Schätzung (`:root { --ledger-filter-bar-height: 44px }`), die die
grundlegende Kollision zwar entschärfte, aber nicht wirklich löste.

### Dritte Iteration: der sticky Versatz misst die reale Leistenhöhe

Ein frischer Verifier maß in einem echten Browser, dass die feste Schätzung falsch
war: bei 1440px war die Leiste tatsächlich 38px hoch (nicht 44px), der Tabellenkopf
ließ eine sichtbare Lücke von 6px; bei 1024px mit aktivem Kategorie-Filter brachen
die Chips zweizeilig um (`md:flex-wrap`), die Leiste war 62px hoch, der
Tabellenkopf überlappte sie um 18px (DATUM/PAYEE/KATEGORIE angeschnitten). Zusätzlich
lag die Schätzung als globaler `:root`-Wert vor, außerhalb des dokumentierten
`Table.Sticky`-Vertrags (`src/Client/DesignSystem/Table.fs`): jede künftige sticky
Tabelle ohne eigene Filterleiste hätte denselben Versatz geerbt.

**Fix:**
1. `.table-sticky-thead thead`s `top` liest `var(--ledger-filter-bar-height, 0px)`
   mit Fallback `0px`, statt eines global gesetzten `:root`-Werts. Jede sticky
   Tabelle ohne eine Spalte, die diese Variable setzt, klebt unverändert direkt
   `64px` unter der Navbar.
2. `Components.Ledger.View.FilterBarColumn` (neue `[<ReactComponent>]`-Funktion,
   ersetzt den bisherigen inline `Html.div` in `view`) misst die Filterleiste per
   `ResizeObserver` (`observeHeight`, analog zu `observeIntersection` und
   `Viewport.fs`s visualViewport-Bindung) und schreibt ihre tatsächliche
   Rand-zu-Rand-Höhe (`getBoundingClientRect().height`, bewusst nicht
   `entries[0].contentRect.height`: Letztere ist die Content-Box ohne das `py-1`-
   Padding von `ChipBar.bar`, ein per Browsertest gefundener 8px-Fehler) als
   Inline-Style `--ledger-filter-bar-height` auf die Kassenbuch-Spalte
   (`flex-1 min-w-0`). `ChipBar.bar` bekam dafür einen neuen optionalen
   Ref-Parameter, durchgereicht über `filterChipBar` (in der dritten Iteration noch
   ein Objekt-Ref, siehe vierte Iteration unten).
3. Neue Tests in `scripts/ledger-filter-bar.test.mjs`: bei 1440px ohne umbrechende
   Chips und bei 1024px mit aktivem Zwei-Kategorien-Filter (Chips umbrechen)
   scrollt jeweils ein eigener, kurzlebiger Kontext die Liste und liest Leiste und
   Tabellenkopf in EINEM atomaren `page.evaluate`: `thead.top` muss mindestens
   `bar.bottom - 1` sein (keine Überlappung) und die Lücke `thead.top - bar.bottom`
   darf höchstens 2px betragen.
4. `ADR 0043` bekam einen Nachtrag zu dieser Entscheidung samt zwei zusätzlichen
   verworfenen Alternativen (feste globale Schätzung, ein gemeinsamer sticky
   Container für Leiste und Tabellenkopf).

### Vierte Iteration: die Höhenmessung folgt dem Knoten per Callback-Ref, auch während eines Spiegel-Laufs

Nach der dritten Iteration an Roman eskaliert, dann mit ihm nachgeschärft: der
Verifier der dritten Iteration fand, dass die Messung während eines laufenden Spiegel-Laufs den
falschen Knoten traf. `FilterBarColumn`s Objekt-Ref (`React.useRef`) wurde in einem
`useEffect` mit leeren Abhängigkeiten EINMALIG an den Knoten gebunden, den `barRef`
beim allerersten Mount hatte. Die Kinder der Spalte sind eine keylose Liste, an
deren erster Stelle bedingt der Hinweis "Spiegel läuft" steht
(`Loading.inlineWithText`, ebenfalls ein `div`). Wird `model.MirrorRunning` NACH dem
ersten Mount wahr (der übliche Fall: das Frische-Kriterium aus ADR 0014 stößt den
automatischen Lauf erst nach dem ersten Render an), verwendet React den bisherigen
Leisten-`div` an dieser Listenposition für den Hinweis weiter (beide Host-Typ "div")
und legt für die Leiste, jetzt eine Position weiter hinten, einen neuen Knoten an.
`barRef.current` zeigt danach zwar korrekt auf den neuen Knoten, der längst
gelaufene `useEffect` bemerkt den Wechsel aber nie: gemessen wurde fortan der
Hinweis, nicht die Leiste (18px Überlappung bei 1440x900, `theadTop=84` gegen
`barBottom=102`; 42px bei 1024x900 mit aktivem Filter).

**Romans Entscheidung:** ein Callback-Ref an `ChipBar.bar` (`bar`s `elementRef`-
Parameter ist seither `Browser.Types.Element -> unit` statt eines
`IRefValue`-Objekts), kein Objekt-Ref mit begleitendem `useEffect`. React ruft einen
Callback-Ref bei jedem Knotenwechsel erneut auf, mit dem neuen Knoten oder mit
`null` beim Unmount. `FilterBarColumn` (`src/Client/Components/Ledger/View.fs`)
merkt sich die laufende Observer-Trennfunktion in einem `React.useRef`; der über
`React.useCallback` memoisierte Callback trennt sie zuerst (falls vorhanden), dann
beobachtet er den neuen Knoten (falls einer da ist, geprüft über `isNull (box
element)`, dasselbe Muster wie `Viewport.fs`s `tryVisualViewport`). Nicht gewählt:
den Hinweis hinter die Leiste zu verschieben (ändert die sichtbare Reihenfolge ohne
fachlichen Grund) und stabile Keys allein (schützt nur, solange niemand künftig eine
weitere bedingte Zeile ohne Key einfügt). Stabile Keys dürfen zusätzlich gesetzt
werden, ersetzen den Callback-Ref aber nicht. Details und die vollständige
Alternativenabwägung in `ADR 0043`s neuem Nachtrag.

**Zwei neue Browsertests** in `scripts/ledger-filter-bar.test.mjs`, mit einer neuen
Fixture-Hilfsfunktion `mockRunningMirror` (`scripts/ledger-filter-bar.mjs`): ein
veralteter `getLastMirrorRun`-Stand (absichtlich um 300ms verzögert beantwortet,
damit die Spalte nachweislich zuerst mit `MirrorRunning = false` fertig mountet,
bevor der Status umschlägt) löst den automatischen Lauf aus (ADR 0014), ohne einen
Klick auf "Jetzt spiegeln"; `mirrorYnabBudget` bleibt danach absichtlich unbeantwortet
(die zurückgegebene `finishMirror()` erfüllt die Anfrage erst, wenn der Aufrufer sie
ruft). Der erste Test misst Leiste und Tabellenkopf bei 1024x900, während der
Hinweis "Spiegel läuft" sichtbar ist (`thead.top >= bar.bottom - 1` und
`thead.top - bar.bottom <= 2`, beide Werte aus demselben `page.evaluate`). Der
zweite Test lässt den Lauf über `finishMirror()` enden, wartet, bis der Hinweis aus
dem DOM verschwindet, und prüft denselben Abstand danach erneut.

**Falsifiziert:** `ChipBar.bar` und `FilterBarColumn` testweise auf den vorherigen
Objekt-Ref-/`useEffect [||]`-Stand zurückgesetzt (Edit-Tool, kein Git), neu gebaut,
Testlauf: genau der erste der beiden neuen Tests schlägt fehl
(`theadTop=84, barBottom=102`, dieselbe 18px-Überlappung wie beim Verifier), der
zweite bleibt grün, weil React den ursprünglichen Leisten-Knoten nach Ende des
Laufs an derselben Listenposition wiederverwendet und die Messung dadurch zufällig
wieder stimmt, unabhängig vom Fix. Danach die Mutation zurückgesetzt und neu
gebaut. `node --test scripts/ledger-filter-bar.test.mjs` fünfmal hintereinander
frisch gelaufen: 70/70 grün (14 Tests je Lauf), keine Flakiness.

**Tests, alle vier Iterationen zusammen:** 69 neue (4 Decoder, 25 Reduzierer in
`LedgerPageTests.fs`, 19 reine Bausteine in `LedgerViewTests.fs`, 2
`isPageReady`-Fälle, 5 für den Settings-Nachhol-Fix, 14 Browsertests: 7 PC + 2
Handy-Chrome/Reihenfolge + 1 Sticky-Scroll + 2 Kollisionstests (dritte Iteration) +
2 Kollisionstests während/nach einem Spiegel-Lauf (vierte Iteration)). `dotnet
build`/`dotnet test` (1099 grün, 7 übersprungen, 0 Fehler), `npm run build`,
`npm run test:scripts` (113 grün, davon 14 in `ledger-filter-bar.test.mjs`) sind
sauber; `git status --short` zeigt nach jedem Testlauf nur die vier durch die
vierte Iteration tatsächlich geänderten Dateien
(`scripts/ledger-filter-bar.mjs`, `scripts/ledger-filter-bar.test.mjs`,
`src/Client/Components/Ledger/View.fs`, `src/Client/DesignSystem/ChipBar.fs`).

**Bewusste Lücke, weiterhin als Backlog-Item erfasst** (siehe BACKLOG_ITEMS): die
Aufnahme der neuen Komponenten (`ChipBar`, `FilterPopover`, Kategorienauswahl mit
Mehrfachauswahl, Zeitraum-/Payee-/Betrags-Popover) als Karten in die
`/styleguide`-Route, nach dem etablierten Muster aus `design-system-t3vqr` (jetzt
`design-system-bz0gn`, design-system-BC). Die Pixel-Abnahme gegen die 16
Mockup-Screens (Screens 2.1 bis 2.14, das einzige mit `[human-eye]` markierte
Akzeptanzkriterium) bleibt bewusst Romans Augenarbeit, wie die Aufgabe selbst
vorgibt.

**Wichtige Dateien:**
- `src/Shared/Domain.fs`: `YnabCategory.Hidden`.
- `src/Server/YnabClient.fs`: `categoryHidden`, beide Decoder erweitert.
- `src/Client/Components/Ledger/Types.fs`, `State.fs`: die Filterleisten-Logik
  (Typen, Reduzierer, Chip-Reihenfolge).
- `src/Client/Components/Ledger/View.fs`: Filterleiste, Summenzeile, Popover-/
  BottomSheet-Weiche, `FilterBarColumn` und `observeHeight` (dritte Iteration),
  seit der vierten Iteration mit Callback-Ref (`barCallbackRef`,
  `disconnectRef`) statt Objekt-Ref/`useEffect [||]`.
- `src/Client/DesignSystem/ChipBar.fs`: `ledger-filter-bar`-Klasse auf `bar`,
  `elementRef`-Parameter (dritte Iteration, seit der vierten Iteration ein
  Callback-Ref `Browser.Types.Element -> unit` statt eines Objekt-Refs).
- `src/Client/DesignSystem/FilterPopover.fs`: Modulkopf-Kommentar korrigiert
  (zweite Iteration).
- `src/Client/DesignSystem/Viewport.fs`: `isDesktopViewport`.
- `src/Client/styles.css`: `.ledger-filter-bar`; `.table-sticky-thead thead`s
  `top` liest `--ledger-filter-bar-height` mit Fallback `0px` statt eines
  global gesetzten `:root`-Werts (dritte Iteration).
- `src/Client/State.fs`: `LedgerMsg LoadCategories`-Abfangpunkt,
  Settings-Nachhol-Logik, `isPageReady` um `Categories` erweitert.
- `src/Tests/YnabClientTests.fs`, `LedgerPageTests.fs`, `LedgerViewTests.fs`,
  `AppReadinessTests.fs`, `LedgerCategoriesSyncTests.fs`.
- `scripts/ledger-filter-bar.mjs`: optionaler `resolveEntries`-Parameter (zweite
  Iteration); `mockRunningMirror` (vierte Iteration).
- `scripts/ledger-filter-bar.test.mjs`: zweiter Handy-Kontext (zweite
  Iteration); zwei Kollisionstests bei 1440px und 1024px (dritte Iteration);
  zwei Kollisionstests während/nach einem laufenden Spiegel-Lauf (vierte
  Iteration).
- `.agentheim/knowledge/decisions/0043-...md` (siehe ADRS-Block, mit Nachträgen
  aus der dritten und vierten Iteration).

## Diary

**2026-09-25** (erste Iteration): Kassenbuch-Filterleiste und Summenzeile gebaut.
Chips, Kategorien-Mehrfachauswahl mit Click-Commit, Zeitraum-Schnellwahl,
Payee-Vorschläge, Memo-/Betrags-Suchfelder, Popover am PC/BottomSheet am Handy,
Summenzeile. Beim Browsertesten zwei echte Funde gemacht: ein Doppel-Overlay-Bug
(`BottomSheet` portalt immer nach `document.body`, ADR 0043) und ein
Deep-Link-Wettlauf zwischen `Settings.LoadSettings` und `Ledger.LoadCategories`,
beide mit Regressionstests behoben.

**2026-09-25** (zweite Iteration, nach Verifier-Befund): Handy-Beleg für die
Popover/BottomSheet-Weiche und die Chip-Reihenfolge per zweitem
Playwright-Kontext (390x844) nachgerüstet, zwei stale Doc-Kommentare korrigiert,
und die Filterleiste sticky gemacht, allerdings mit einer festen 44px-Schätzung
für den Versatz zum Tabellenkopf.

**2026-09-25** (dritte Iteration, nach frischem Verifier-Befund): die feste
44px-Schätzung durch eine Messung ersetzt. `Components.Ledger.View.FilterBarColumn`
misst die Filterleiste per `ResizeObserver` und schreibt ihre echte Höhe in eine
CSS-Variable, die nur auf der Kassenbuch-Spalte gilt und außerhalb davon auf 0px
zurückfällt, sodass der dokumentierte `Table.Sticky`-Vertrag für jede andere
sticky Tabelle unverändert bleibt. Zwei neue Kollisionstests (1440px ohne
Umbruch, 1024px mit umbrechenden Chips und aktivem Filter) einzeln falsifiziert
(testweise wieder die feste Summe `64px + 44px` gesetzt): genau diese beiden
Tests schlagen dann fehl, mit denselben Werten wie der Verifier maß, alle
anderen bleiben grün. `node --test scripts/ledger-filter-bar.test.mjs` fünfmal
hintereinander: 60/60 grün.

**2026-09-26** (vierte Iteration, nach Eskalation und Nachschärfen mit Roman): den
letzten offenen Punkt behoben, den der Verifier der dritten Iteration gefunden hatte:
während eines laufenden Spiegel-Laufs maß der `ResizeObserver` den falschen Knoten,
weil ihn ein `useEffect` mit leeren Abhängigkeiten einmalig an den Knoten vom
allerersten Mount band, während React denselben Knoten später für den Spiegel-
Lauf-Hinweis weiterverwendete und für die Leiste selbst einen neuen Knoten anlegte.
Nach Romans Entscheidung ersetzt jetzt ein Callback-Ref an `ChipBar.bar` (statt
eines Objekt-Refs) die Bindung: er trennt und beobachtet bei jedem Knotenwechsel
neu, nicht nur beim Mount. Zwei neue Browsertests (während und nach einem
laufenden, absichtlich künstlich verzögerten Spiegel-Lauf) belegen es; der erste
per Falsifikation nachweislich rot ohne den Fix (18px Überlappung, wie vom
Verifier gemeldet), grün mit ihm. `node --test scripts/ledger-filter-bar.test.mjs`
fünfmal hintereinander: 70/70 grün. `dotnet test`: 1099 grün, 0 Fehler.
`npm run test:scripts`: 113 grün. `git status --short` zeigt nur die vier
tatsächlich geänderten Dateien.
