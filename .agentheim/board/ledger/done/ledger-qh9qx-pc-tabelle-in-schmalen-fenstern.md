---
id: ledger-qh9qx
title: "PC-Tabelle des Kassenbuchs bricht in schmalen Fenstern: Payee, Kategorie und Memo schrumpfen auf null, die Spaltenköpfe liegen übereinander"
status: done
type: bug
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001]
blocks: [ledger-q1hw2]
tags: [frontend, kassenbuch, responsive, tabelle]
related_adrs: [0049, 0052]
related_research: []
prior_art: [ledger-n4vte, ledger-qns4j]
---

## Why
Ab `md` (768px) zeigt das Kassenbuch die PC-Tabelle. Die festen Spalten (Auswahl, Datum
96px, Konto 72px, Betrag 132px, Status 128px) brauchen zusammen rund 480px, Payee und
Kategorie bekommen Prozentanteile der Tabellenbreite, Memo den Rest
(`tableColumnWidths`, `Components/Ledger/View.fs`). Neben der Kontenspalte bleibt in einem
halb breiten Fenster kaum Platz: Payee, Kategorie und Memo werden auf ein bis zwei
Zeichen gekürzt ("Jo...", "Üb...") oder ganz unsichtbar, die Köpfe "PAYEE",
"KATEGORIE" und "MEMO" liegen übereinander. Roman hat das mit Screenshots gemeldet
(2026-10-20).

## What
Die Tabelle blendet Spalten stufenweise aus, statt sie zusammenzudrücken (Entscheidung
Roman 2026-10-20):

1. Payee und Kategorie haben eine Mindestbreite (Richtwert Payee 140px, Kategorie 120px,
   beim Bau gegen echte Werte wie "Transfer : Tagesgeld ING" prüfen).
2. Reicht die Breite dafür nicht, fällt zuerst die Memo-Spalte weg.
3. Reicht es dann immer noch nicht, fällt in der Gesamtsicht die Konto-Spalte weg; das
   Kontokürzel steht dann klein vor oder unter dem Payee in derselben Zelle.
4. Erst wenn auch das nicht reicht, greift die Handy-Liste. Die Schwelle richtet sich nach
   der verfügbaren Tabellenbreite, nicht allein nach dem Viewport-Breakpoint `md`.

Welche Spalten sichtbar sind, entscheidet eine reine Funktion aus Tabellenbreite und
Gesamtsicht/Konto (etwa `visibleColumns tableWidth showAccountColumn`), damit sie ohne
Browser testbar ist. Die Breite misst ein `ResizeObserver` auf dem Tabellencontainer, wie
schon `FilterBarColumn` die Höhe der Filterleiste misst.

## Acceptance criteria
- [ ] Eine reine Funktion bestimmt die sichtbaren Spalten aus Tabellenbreite und
      Gesamtsicht/Konto; Expecto-Tests decken die Stufen ab (alle Spalten, ohne Memo,
      ohne Memo und Konto, Handy-Liste) inklusive der Grenzwerte.
- [ ] In keiner Stufe ist eine sichtbare Spalte schmaler als ihre Mindestbreite.
- [ ] Bei Fensterbreiten 800, 1024, 1280 und 1440px (Gesamtsicht und ein Konto) überlappt
      kein Spaltenkopf seinen Nachbarn: für jedes `th` gilt `scrollWidth <= clientWidth`,
      geprüft im echten Browser (Chrome DevTools, Screenshot im Verifier-Bericht).
- [ ] Ist die Konto-Spalte ausgeblendet, ist das Konto jeder Zeile in der Gesamtsicht
      weiter sichtbar (Kürzel in der Payee-Zelle).
- [ ] Der Sticky-Kopf und die Statuskante "zu prüfen" funktionieren in jeder Stufe
      unverändert.
- [ ] Die gekürzte Darstellung bei schmaler Breite wirkt ruhig und lesbar (ADR 0049). [human-eye]

## Notes
- `memoMinShare` und `referenceTableWidth` in `View.fs` sind ein früherer Ansatz für die
  Memo-Breite; prüfen, ob sie in der neuen Funktion aufgehen oder wegfallen.
- `Viewport.isDesktopViewport()` entscheidet heute zwischen Tabelle und Handy-Liste
  (ADR 0043). Stufe 4 verschiebt diese Grenze; die Filterleisten-Chrome soll dabei nicht
  ungewollt mitwechseln.
- Verwandt: `ledger-bn913` (Sortierung über die Spaltenköpfe) braucht dieselben Köpfe.


## Verifier note (iteration 1)

**REASONS:**
- Check 5 (README sync): README_DELTA ist `[]`, aber der Absatz "**Liste.**" im Abschnitt "Oberfläche" der ledger-README (Zeilen ca. 437 bis 440) sagt weiter "Am PC eine Tabelle mit Sticky-Kopf (Auswahl, Datum, Konto in der Gesamtsicht, Payee, Kategorie, Memo, Betrag, Status)". Nach dem Diff fallen Memo und dann Konto mit der gemessenen Breite weg, darunter zeigt der PC die kompakte Liste; das Kontokürzel in der Payee-Zelle fehlt im README.
- Check 6 (ADR): Die Grenze Tabelle/Liste folgt jetzt der gemessenen Breite (ResizeObserver), die Filter-Chrome bleibt bei `Viewport.isDesktopViewport()`. ADR 0043 nennt genau diese Trennung als Risiko. Die Entscheidung ist dauerhaft (kompakte Liste am PC ohne Kästchen, Spaltenköpfe, Tabellen-Interaktionen), aber in keiner ADR festgehalten.
- Alles andere geprüft und in Ordnung: Build 0 Warnungen, dotnet test 1541/0/7, test:scripts 140/140, eigene Browsermessung bei 800/1024/1280/1440 in beiden Sichten bestanden, Sticky-Kopf, pinke Kante, Kürzel, Filter-Chrome unverändert, kein ResizeObserver-Loop.

**SUGGESTED_FIX:** Neuen Text für den Absatz "**Liste.**" liefern (als `### README von Hand`, da Prosa-Absatz): Memo fällt zuerst weg, dann Konto mit Kürzel in der Payee-Zelle, dann die kompakte Liste; entschieden von `visibleColumns` aus der gemessenen Tabellenbreite. Dazu eine ADR (oder einen datierten Nachtrag zu ADR 0043), dass die Grenze Tabelle/Liste der gemessenen Breite folgt, die Filter-Chrome bei `isDesktopViewport()` bleibt, mit Alternativen und Folgen. Keine Codeänderung nötig.

**ITERATION_HINT:** likely-fixable

## Outcome

Die PC-Tabelle des Kassenbuchs zeigt in schmalen Fenstern nur noch Spalten, die ihre Mindestbreite bekommen.

- `visibleColumns (tableWidth: float) (showAccountColumn: bool) : TableLayout` in `src/Client/Components/Ledger/View.fs` (rein, ohne Browser testbar). Stufen: alle Spalten, ohne Memo, ohne Memo und Konto (nur Gesamtsicht), sonst `CompactList`. Die erste Stufe, deren Mindestbreiten (Kästchen 44, Datum 96, Konto 72, Payee 140, Kategorie 120, Memo 120, Betrag 132, Status 128) inklusive in die Breite passen, gewinnt. Den Überschuss teilen Payee 40 %, Kategorie 30 %, Memo (oder ohne Memo die Kategorie) als Restspalte.
- `tableColumnWidths`, `memoMinShare`, `referenceTableWidth`, `memoWidthAt` und `Percent` sind entfallen (gingen in `visibleColumns` auf); neu `LedgerColumn`, `TableLayout`, `columnPixelWidth`, `minColumnWidth`.
- Breite: `FilterBarColumn` misst die Spalte per `ResizeObserver` (`observeWidth`, daneben `observeHeight` unverändert) und reicht `tableWidth` an `entryList` weiter. Bis zum ersten Messwert gilt 1090 px.
- Stufe 4: bei `CompactList` wird die Tabelle nicht gerendert und die Handy-Liste ohne `md:hidden` gezeigt (Parameter `alwaysVisible` in `entryListMobile`). `Viewport.isDesktopViewport()` und damit die Filterleisten-Chrome (ADR 0043) bleiben unberührt.
- Konto-Spalte weg in der Gesamtsicht: das Kürzel steht klein (ListRoles.muted) als eigenes `span` vor dem Payee-Text in `payeeCell` (Payee-Text ein getrenntes `span`, für ledger-rfq5p). Köpfe, `colgroup`, Datumsgruppen-Kopf und Hinweiszeile richten sich nach den sichtbaren Spalten. Statuszelle und Sticky-Kopf unverändert.
- Tests: `src/Tests/LedgerVisibleColumnsTests.fs` (9 Tests: alle Stufen mit Grenzwerten, Mindestbreiten über Breitenraster, genau eine Restspalte, Kürzel-Wächter), 2 bestehende Tests in `LedgerViewTests.fs` angepasst (Prozent-Test ersetzt). Browser: neuer Wächter `scripts/ledger-table-columns.test.mjs` (3 Tests, echter App-Client gegen gemockte API, atomares `page.evaluate`).
- `scripts/ledger-status-column.test.mjs` (AC6) prüfte 28 % / 24 %; ersetzt durch Mindestbreiten Payee >= 140, Kategorie >= 120.

Gemessen im echten Browser (`th.scrollWidth <= clientWidth` für jedes th in allen Fällen erfüllt; Spaltenbreiten Kästchen/Datum/[Konto]/Payee/Kategorie/[Memo]/Betrag/Status):

| Fenster | Gesamtsicht | Ein Konto |
|---|---|---|
| 800 | kompakte Liste (keine Tabelle) | kompakte Liste |
| 1024 | ohne Memo und Konto: 44/96/149/134/132/128 | gleich (keine Konto-Spalte) |
| 1280 | alle: 44/96/72/175/146/146/132/128 | 44/96/204/168/167/132/128 |
| 1440 | alle: 44/96/72/239/194/194/132/128 | 44/96/268/216/215/132/128 |

Screenshots (Gesamtsicht 800, 1024, 1280, 1440): `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/19d01155-bd5c-44c4-83fc-f181eb456932/scratchpad/ledger-qh9qx/gesamt-*.png`. Im Screenshot bei 1024 stehen das Kürzel "TEST" und der Payee "Transfer : ..." in der Payee-Zelle; Payee wird bei 149 px mit Ellipse gekürzt (Richtwert 140 px, "Transfer : Tagesgeld ING" braucht ca. 190 px). Das [human-eye]-Kriterium bleibt dem Verifier/Roman.

Läufe: `dotnet build` 0 Warnungen, `dotnet test` 1541 erfolgreich, 0 Fehler, 7 übersprungen (Integration ohne .env); `npm run build` ok, `npm run guard:classes` ok, `npm run test:scripts` 140/140.

Hinweis: In den Messungen sprang das Layout an der Stufengrenze nicht (kein Hysterese-Bedarf beobachtet); ein Scrollbalken, der die Breite um ca. 15 px ändert, könnte genau auf einer Grenze flippen. Nicht gemessen, nicht behandelt.

Dazu eine neue ADR: 0052-grenze-tabelle-liste-folgt-der-gemessenen-tabellenbreite.md hält fest, dass die Grenze zwischen Tabelle und kompakter Liste der gemessenen Breite folgt und die Filter-Chrome beim Viewport bleibt.

### README von Hand

Im Abschnitt "Oberfläche", Absatz "**Liste.**" (README.md, ca. Zeilen 437 bis 440), dieser Teilstring (kommt genau einmal vor, Zeilenumbrüche wie im README):

ALT:
```
Am PC eine Tabelle mit Sticky-Kopf
(Auswahl, Datum, Konto in der Gesamtsicht, Payee, Kategorie, Memo, Betrag, Status).
```

NEU:
```
Am PC eine Tabelle mit Sticky-Kopf
(Auswahl, Datum, Konto in der Gesamtsicht, Payee, Kategorie, Memo, Betrag, Status). Bei zu
wenig Breite fällt zuerst Memo weg, dann in der Gesamtsicht Konto (das Kürzel steht dann
klein vor dem Payee), darunter kommt die kompakte Liste. Das entscheidet `visibleColumns`
aus der gemessenen Tabellenbreite (ADR 0052); die Filter-Chrome bleibt bei
`isDesktopViewport()`.
```
