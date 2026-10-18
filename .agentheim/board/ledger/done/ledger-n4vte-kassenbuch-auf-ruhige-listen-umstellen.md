---
id: ledger-n4vte
title: Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)
status: done
type: feature
context: ledger
created: 2026-10-18
completed:
depends_on: [design-system-q7rkm, design-system-001]
blocks: []
tags: [frontend, kassenbuch, styleguide, visuell, ruhige-listen]
related_adrs: [0049, 0032, 0048]
related_research: []
prior_art: [ledger-x0yga, ledger-w9skm, ledger-qns4j, ledger-h6ptn]
---

## Why
Das Kassenbuch wirkt neben "Import prüfen" laut und schwer und nutzt den Platz nicht aus:
Gradient-Titel in 4xl, Liste auf `max-w-7xl` begrenzt, alle Zellen 16px grau, Kategorie
violett, Beträge rot und grün. Roman hat am 2026-10-18 im Sichttest Fassung B (ruhige
Tabelle) gewählt, festgehalten in ADR 0049 und im Styleguide §2 "Ruhige Listen".

## What
Zielbild (im Repo):
- PC: `docs/ux/kassenbuch/mockups/ruhige-listen-b-pc-1440.png`
- Handy: `docs/ux/kassenbuch/mockups/ruhige-listen-b-handy-390x844.png`

Struktur und Verhalten aus Handoff Ablauf 1 (`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`)
gelten weiter. Dessen Farb- und Schriftwerte für Listen sind durch ADR 0049 überholt.

**Beide Viewports** (`src/Client/Components/Ledger/View.fs`, `view`):
- `PageHeader.gradientWithActions` durch die schlanke Variante aus `design-system-q7rkm`
  ersetzen. "Jetzt spiegeln" bleibt erreichbar, aber nicht mehr als großer oranger Knopf
  über der Liste; am Handy als Symbol-Knopf in der Kopfzeile, der Untertitel nennt den
  letzten Spiegel kurz ("Gespiegelt heute 22:24").
- Container ohne Breitenbegrenzung.
- Kontostände ruhig (neutral, negativ Red) über die angepasste `Money.accountListRow`.

**PC** (`entryListDesktop`, `entryTableRow`):
- Zellrollen aus `design-system-q7rkm` statt `Table.td`/`tdTruncated`; Kategorie über die
  sekundäre Rolle statt `categoryColorClassName` (violett).
- Betrag rechtsbündig über die Betragsrolle.
- "Zu prüfen" als Statuskante statt `border-l-2 border-l-neon-pink`; in der Statusspalte
  ein kleines "zu prüfen" statt Badge.
- Datumsgruppen mit Tagessumme auch am PC (Gruppenkopf wie am Handy).
- Spaltenanteile so, dass Payee und Kategorie nicht mehr bei Inhalten wie "Transfer :
  Tagesgeld ING" oder "Alltag: Essen/Einkaufen" abgeschnitten werden, während die
  Memo-Spalte leer ist (Payee ca. 30 %, Kategorie ca. 26 %, Memo Rest).

**Handy** (`entryRow`, `dateGroupHeader`):
- Zweizeilig: Payee und Betrag in Zeile 1 (Primär-Rolle, 15px), Kategorie und Memo in
  Zeile 2 (12px, sekundär bzw. gedämpft).
- Badge-Zeile entfernen: `originBadge` raus (Herkunft steht in der Historie, wie am PC seit
  `ledger-qns4j`). `accountBadge` (Gesamtsicht), `flagBadge` und `deletedBadge` wandern in
  Zeile 2, damit keine dritte Zeile entsteht.
- "Zu prüfen" als Statuskante statt `Badge.dot`.
- Gruppenkopf mit Tagessumme rechts.

Bestehende Tests, die den alten Zustand festschreiben, werden angepasst, nicht gelöscht
ohne Ersatz: `categoryColorTests` (violett) und die Quelltext-Zusicherung "Die Handy-Zeile
zeigt die Herkunft weiterhin inline" in `src/Tests/LedgerViewTests.fs`.

## Acceptance criteria
- [ ] Die Kassenbuch-Seite verwendet weder `PageHeader.gradientWithActions` noch
      `Container.view` mit Breitenbegrenzung (Quelltext-Zusicherung).
- [ ] PC-Tabelle und Handy-Zeile färben die Kategorie über die sekundäre Rolle; der
      bisherige Test auf `neonPurple` ist durch einen Test auf die neue Rolle ersetzt.
      "Keine Kategorie" bleibt Orange.
- [ ] Ein Übertrag erscheint nicht als "Keine Kategorie" (ADR 0048), in beiden Viewports.
- [ ] Beträge in Liste und Tabelle laufen über die Money-Listenvariante; ein positiver
      Betrag ist Teal, ein negativer `text-primary`. Ein in YNAB gelöschter Betrag bleibt
      bei `Money.transactionDeleted`.
- [ ] Der Betrag steht am PC rechtsbündig in seiner Spalte, bündig mit dem Spaltenkopf.
- [ ] "Zu prüfen" erscheint in beiden Viewports als Statuskante links; der `Badge.dot` in
      `entryRow` und `border-l-2 border-l-neon-pink` in `entryTableRow` sind entfernt.
- [ ] Die Handy-Zeile rendert `originBadge` nicht mehr; Konto-Kürzel, Flag und
      Gelöscht-Kennzeichen stehen in Zeile 2. Die Quelltext-Zusicherung zur Herkunft ist
      entsprechend umgedreht.
- [ ] Datumsgruppen zeigen in beiden Viewports eine Tagessumme rechts; die Summe einer
      Gruppe ist die Summe der sichtbaren Zeilen dieser Gruppe (reine Funktion, getestet,
      inklusive eingeblendeter gelöschter Buchungen nach `ledger-d4kx8`).
- [ ] Ein negativer Kontostand erscheint Red, ein positiver neutral, in Kontoliste (PC)
      und Kontokarten (Handy).
- [ ] Tastatursteuerung, Mehrfachauswahl, Wisch-Geste, Filter-Chips, Summenzeile und
      Detail-Sheet funktionieren wie vorher (bestehende Tests grün).
- [ ] Screenshots `#/ledger` in 1440 × 900 und 390 × 844 liegen unter
      `docs/ux/kassenbuch/screenshots/` (`npm run screenshot:route -- '#/ledger' --out
      docs/ux/kassenbuch/screenshots/`).
- [ ] Die Screenshots neben den beiden Zielbildern zeigen dieselbe Hierarchie und
      Farbverteilung: Payee hell, Rest gedämpft, Farbe nur bei Eingang, Statuskante,
      "Keine Kategorie" und negativem Kontostand; kein Gradient-Titel; die Liste füllt die
      Breite. [human-eye]
- [ ] Erweiterung (Roman, 2026-10-18): Im Styleguide-Abschnitt "8a. Ruhige Listen" steht
      die Tagessumme im Gruppenkopf bündig unter der Betragsspalte, und alle Zeilen der
      zweizeiligen Handy-Zeile haben denselben Abstand zwischen Zeile 1 und 2. Liegt die
      Ursache in einem DS-Baustein, ist sie dort behoben, sodass Kassenbuch und Styleguide
      dieselbe Lösung nutzen. Neue Screenshots unter `docs/ux/design-system/screenshots/`.
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
- Quellen: ADR 0049, Styleguide §2 "Ruhige Listen", Artefakt "Kassenbuch Sichttest"
  (https://claude.ai/artifact/2eGrKDs4GxUGuGJdfL1XaZ), Fassung B.
- Romans Entscheidungen vom 2026-10-18: Fassung B; negative Kontostände Red; am Handy
  Payee und Betrag 15px, am PC 14px.
- Im Zielbild steht der Übertrag "Transfer : Tagesgeld ING" fälschlich als "Keine
  Kategorie" (Fehler in den Beispieldaten des Sichttests). Maßgeblich ist ADR 0048.
- Die Datumsspalte am PC zeigt im Zielbild zusätzlich zum Gruppenkopf das Datum je Zeile.
  Das darf bleiben (hilft beim Scannen mit Tastatur), ist aber kein Muss.
- Bausteine aus `design-system-q7rkm` (Stand 2026-10-18): `Tokens.ListRoles`,
  `Money.listAmount`/`accountBalanceClass`, `Table.roleCell`/`amountCell`/`calmHeaderCell`/
  `calmRow` mit `Table.CellRole` und `RowProps.StatusEdge`, `Primitives.Container.full`,
  `PageHeader.calm`. Vorbild für die Verwendung: Abschnitt "8a. Ruhige Listen" in
  `src/Client/Components/Styleguide/View.fs`.
- Zwei Abweichungen im Styleguide-Beispiel, die das Kassenbuch nicht übernehmen soll: die
  Tagessumme im Gruppenkopf steht dort unter der Status-Spalte statt bündig unter den
  Beträgen (Zielbild: unter der Betragsspalte), und in der zweizeiligen Handy-Zeile ist die
  erste Zeile niedriger als die übrigen (Abstände zwischen Zeile 1 und 2 uneinheitlich).
- `ledger-b7qmx` (Split-Zeilen voll qualifizieren) liegt im Backlog und berührt dieselbe
  Kategorie-Zelle. Keine Abhängigkeit, aber bei gleichzeitiger Arbeit Merge-Konflikt in
  `tdTruncatedCategory` möglich.

## Verifier note (iteration 1)

**REASONS:**
- `npm run test:scripts` im Worktree: 118 Tests, 6 rot. `dotnet build` sauber (nur das bekannte FS0025), `dotnet test` 1194 grün, `npm run build` grün.
- `scripts/ledger-status-column.test.mjs:262` und die Tests an Zeile 279, 321, 365: erwarten `includes("Zu prüfen")`, der Diff rendert jetzt das kleine Wort "zu prüfen"; die `badgeCount == 2`-Annahmen passen nicht mehr, weil "zu prüfen" kein Badge mehr ist.
- `scripts/ledger-status-column.test.mjs:407`: "Kategorie sollte ~200px sein, war 285.73". Der Wächter prüft noch das alte 200px-Raster und die alte Payee/Memo-Aufteilung.
- `scripts/ledger-filter-bar.test.mjs:488`: "Der Abstand zwischen Leiste und Tabellenkopf darf höchstens 2px sein, war 45.5" (1024px, umbrechende Chips, aktiver Kategorie-Filter). Vom Conductor auf `main` (Stand vor diesem Diff) geprüft: dort grün. Also eine Regression aus diesem Diff (Seitenrahmen `pageContentFull`, `PageHeader.calm` oder Gruppenzeilen im tbody).
- Das Kriterium "bestehende Tests grün" ist damit nicht erfüllt; TESTS_PASSING stützte sich nur auf `dotnet test`.

**SUGGESTED_FIX:** `npm run test:scripts` laufen lassen. Die Wächter in `scripts/ledger-status-column.test.mjs` auf den neuen Stand ziehen (kleines pinkes Wort "zu prüfen" neben der Statuskante, Spaltenaufteilung Payee ca. 30 %, Kategorie ca. 26 %, Memo Rest, im Browser gemessen) und dabei erhalten, was sie zu Zeilenhöhe, fehlendem Umbruch und Bedeutung für Screenreader zusichern. Die 45,5px-Lücke bei 1024px an der Ursache im Layout beheben, nicht an der Schwelle.

**ITERATION_HINT:** likely-fixable

## Verifier note (iteration 2)

**REASONS:**
- Kriterium "Ein Übertrag erscheint nicht als Keine Kategorie (ADR 0048)": nur die Client-Hälfte ist getestet. Die Regel `IsTransfer = entry.TransferAccountId.IsSome || entry.TransferEntryId.IsSome` in `src/Server/Ledger/QueryHandlers.fs:148` hat keinen Test; `IsTransfer = false` oder eine fehlende Hälfte des `||` ließe alle Suiten grün.
- Kein Mangel, festgehalten, damit Iteration 3 es nicht anfasst: die Änderung an `scrollListIntoStickyState` in `scripts/ledger-filter-bar.test.mjs` ist eine legitime Korrektur der Messung. Ein einzelnes synthetisches 1200px-Mausrad erreicht in keinem Stand 1200px (Basis 393 bis 565, Worktree 123 oder 543), keine Mutationen während des Scrollens, schrittweises Scrollen erreicht in beiden Ständen über 1100px, im Haft-Zustand ist die Lücke 0.
- Sonst grün: `dotnet build` (nur das bekannte FS0025), `dotnet test` 1194, `npm run build`, `npm run test:scripts` 118/118.

**SUGGESTED_FIX:** In `src/Tests/LedgerPageTests.fs` einen Test (oder eine kleine Liste) für `ReadModel.toEntryView` ergänzen, der `IsTransfer` prüft: true nur mit `TransferAccountId = Some ...`, true nur mit `TransferEntryId = Some ...`, false ohne beides. Filterleisten-Test und Layout unverändert lassen.

**ITERATION_HINT:** likely-fixable

## Outcome

Das Kassenbuch folgt jetzt ADR 0049 (Fassung B). `Components/Ledger/View.fs`: `PageHeader.calm` mit Untertitel "Gespiegelt heute 22:24" (`mirrorRunSubtitle`, rein und getestet), "Jetzt spiegeln" als Symbol-Knopf am Handy und sekundärer Knopf am PC; `Primitives.Container.full`. PC-Tabelle mit `Table.roleCell`/`amountCell`/`calmHeaderCell`, `RowProps.StatusEdge` statt `border-l-neon-pink`, "zu prüfen" als kleines pinkes Wort, Spaltenanteile über eine `colgroup` (Payee 30 %, Kategorie 26 %, Memo Rest). Handy-Zeile zweizeilig (feste Höhe `h-5` je Zeile über `ListRoles.mobileLine1/2`), `originBadge` raus, Konto-Kürzel, Flag und Gelöscht-Kennzeichen in Zeile 2, Statuskante statt `Badge.dot`. Tagessumme in beiden Viewports über die reine Funktion `daySum includeDeleted group` (gelöschte nur bei Einblendung, `ledger-d4kx8`) und `daySumText`. Kategorie über `categoryClassName` (sekundäre Rolle, nur "ohne Kategorie" Orange).

Übertrag (ADR 0048): `LedgerEntryView` hat das neue Feld `IsTransfer` (aus `TransferAccountId`/`TransferEntryId`); `categoryLabel` zeigt "Übertrag", `isWithoutCategory` ist false. Der Platzhaltertext "ohne Kategorie" blieb unverändert (Zielbild sagt "Keine Kategorie", eigener Schritt). Seitenrahmen: `Navigation.pageContentFull` (ohne `container`, max 1280px) nur für das Kassenbuch. Neue DS-Bausteine: `Tokens.ListRoles.cellPaddingX/mobileLine1/mobileLine2`, `Table.calmGroupRow`.

Erweiterung auf Wunsch des Builders: Styleguide-Abschnitt "8a. Ruhige Listen" nutzt `Table.calmGroupRow` (Tagessumme bündig unter dem Betrag) und `ListRoles.mobileLine1/2` (gleiche Zeilenhöhe und Abstand), Styleguide-Screenshots neu aufgenommen.

Iteration 2 (Verifier: `npm run test:scripts` hatte 6 rote Tests):
- `scripts/ledger-status-column.test.mjs`: die Wächter erwarten jetzt das Wort "zu prüfen" statt "Zu prüfen", zählen nur noch das Symbol als Badge (1 statt 2) und behalten Zeilenhöhe, Umbruchfreiheit und die Bedeutung für Screenreader (`title`/`aria-label`). Der Spaltenraster-Wächter misst im Browser Datum 96, Betrag 132, Status 128, Payee ca. 30 %, Kategorie ca. 26 %, Memo füllt den Rest und ist mindestens 120px breit.
- `scripts/ledger-filter-bar.test.mjs` (1024px, aktiver Kategorie-Filter, Lücke 45,5px): kein Layoutfehler. Der scrollY-Verlauf zeigte, dass der animierte Mausrad-Scroll (`html { scroll-behavior: smooth }`) je nach Lauf nach 123 bis 543px abbrach; bei 123px erreicht der Tabellenkopf (natürliche Lage 294px, Haften ab 168px Scroll) seine Haft-Position nie. Mit `window.scrollTo(1200, instant)` stehen Leiste und Kopf im Haft-Zustand bündig (Lücke 0), dreimal hintereinander grün. Die Schwelle von 2px ist unverändert. Ob der abgebrochene Smooth-Scroll auf `main` ebenfalls auftritt, habe ich nicht geprüft (kein zweiter Worktree). Der Test wartet zusätzlich, bis die Liste geladen ist.
- Ergebnis aus dem Worktree: `dotnet build` 0 Warnungen, `dotnet test` 1194 grün (7 übersprungen), `npm run build` grün, `npm run test:scripts` 118 von 118 grün. Layout unverändert, Screenshots aus Iteration 1 gelten weiter. Das `[human-eye]`-Kriterium bleibt offen.

Diary: Kassenbuch auf ruhige Listen umgestellt (ADR 0049), Iteration 2 zieht die Browser-Wächter nach. Dateien: siehe FILE_LIST. Grund: Verifier-Befund, test:scripts rot. Build und alle Suiten grün.

Iteration 3: Die Server-Regel `IsTransfer = TransferAccountId.IsSome || TransferEntryId.IsSome` (`QueryHandlers.toEntryView`) hat jetzt Tests in `src/Tests/LedgerPageTests.fs` (`isTransferTests`): nur `TransferAccountId` gesetzt ist ein Übertrag, nur `TransferEntryId` gesetzt ist ein Übertrag, keines von beiden ist keiner. Jeder Fall wurde gegen eine absichtlich gebrochene Regel geprüft und ist dann rot (eine Seite des `||` entfernt: genau der zugehörige Fall rot; Regel auf `false`: beide Übertrags-Fälle rot); die Regel ist wieder wie vorher. Layout und Filterleisten-Test unverändert. Ergebnis aus dem Worktree: `dotnet build` 0 Warnungen, `dotnet test` 1197 grün (7 übersprungen), `npm run build` grün, `npm run test:scripts` 118 von 118 grün.
