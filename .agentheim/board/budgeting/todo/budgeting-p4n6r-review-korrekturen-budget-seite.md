---
id: budgeting-p4n6r
title: "Review-Korrekturen zur neuen Budget-Seite: Monatswechsel ohne Sprung, Pink raus, Trefferflächen, sticky Filter, Testlücken"
status: todo
type: bug
context: budgeting
created: 2026-11-02
completed:
depends_on: [budgeting-g8s3w]
blocks: [budgeting-c5w2n]
tags: [budget, inspektor, review]
related_adrs: [0049, 0065, 0074, 0075, 0076, 0077]
related_research: []
prior_art: [budgeting-g8s3w]
---

## Why
Die vier Reviews nach budgeting-g8s3w (Test, Backend, Frontend, Design) haben Fehler und Lücken gefunden. Sie werden behoben, bevor Roman die Seite ansieht und bevor budgeting-c5w2n darauf aufbaut. Zeilennummern beziehen sich auf Commit 6c82444.

## What

### Fehler im Verhalten
1. **Reloaded holt Buchungen nicht.** `Components/Budget/State.fs:203`: Der `Reloaded`-Zweig läuft durch `withEntries` wie `Loaded`. Sonst bleibt im Tab Buchungen das Skeleton stehen, wenn eine Antwort auf "Erneut senden" einen Monatswechsel überholt.
2. **Monatswechsel springt.** Beim Blättern bleiben der alte Monat, Liste, Suche, Filter, Zu verteilen und der Inspektor stehen, bis die Antwort kommt (etwa Inhalt gedimmt, kein Spinner, keine Layout-Verschiebung). Das Panel zeigt "Kategorie wählen" nur bei `Selected = None`. Der Tastaturfokus der Liste und die Scrollposition bleiben erhalten. Betrifft `Types.fs:298`, `View.fs:93-95`, `:1023`, `:1062`. Der erste Ladevorgang behält Spinner oder Skeleton. Scheitert das Laden, gilt das bisherige `ErrorDisplay.card`, die Pfeile bleiben.
3. **`applyFrameRefreshed` zieht Monate auseinander.** `Types.fs:325`: Ändert das stille Nachlesen `shownMonth`, wird der Monat neu geladen wie in `applyFrameLoaded`.
4. **`beginLoad` erhöht die Generation nicht** (`Types.fs:303`). Eine späte Monatsantwort darf nichts setzen.
5. **Nach gescheitertem erneutem Senden** wird der Rahmen nachgelesen wie bei `MoveCallLost`, wenn der gezeigte Monat der letzte ist (`State.fs:154`, `:201`, `Types.fs:749`).
6. **Betreten der Seite setzt den Monat zurück.** `enterPage` setzt `ChosenMonth = None`, außer bei `ReturnPending`. Entscheidung des Conductors: Die Vorgabe bleibt der laufende Monat.
7. **Offene Verschiebungen im Sheet begrenzen.** `.sheet-pinned` wird in der Höhe begrenzt und scrollt, damit `.sheet-body` immer Platz behält.

### Design
8. **Kein Pink auf der Budget-Seite** (ADR 0075 Punkt 5). `View.fs:479`, `:498`, `:559`: Eine Hinweis-Variante in `calm-yellow` für `Badge` und `ErrorDisplay` ersetzt `warning` auf dieser Seite. Das Kassenbuch behält Pink, `warning` bleibt bestehen. Styleguide und `/styleguide` nachziehen.
9. **Schließen-Knopf 48 px Trefferfläche** (`styles.css:1715`, gilt für alle Sheets), das sichtbare Rund bleibt 32 px. Wächter "Trefferflächen" um `.sheet-close` erweitern.
10. **Handwerte am Panel.** Variable `--budget-month-bar-height`, gemessen per `ResizeObserver` wie `.ledger-filter-bar` (`styles.css:1107`). Daraus leiten sich `top`, `max-height`, Scroll-Rand ab, statt `136px`, `152px`, `scroll-mt-32`. `styles.css:1130-1136` teilt sich den Selektor mit `.ledger-filter-bar`, statt zu kopieren.
11. **Filter sticky** (Styleguide §7.3). Am PC stehen Suche und Chips in einer sticky Zeile unter der Monatsleiste. Am Handy sind nur die Chips sticky, die Suche scrollt mit, damit die festen Zeilen wenig Höhe nehmen. Gemessen wird die Gesamthöhe bei 390 × 844 und 320 px Breite.
12. **Spaltenkopf sticky** am PC (`View.fs:250`, `:335`, Regeln im Kommentar von `Table.fs:38`), Offset aus Punkt 10 und 11.
13. **Sheet fährt ein und aus** (`View.fs:1000-1018`): immer rendern, `IsOpen` aus der Auswahl, Inhalt während der Ausfahrt halten. `.sheet-pinned` und feste Höhe bleiben.
14. **Barrierefreiheit.** Gewählte Zeile: `aria-current="true"` statt `aria-selected`, auch an `tr` (`Button.fs:276`, `View.fs:217`, `:327`, Wächter `budget-page.test.mjs:455`). Das ersetzt die Vorgabe `aria-selected` aus budgeting-g8s3w. `Tabs`: `tabpanel`, `aria-controls`, Pfeiltasten, roving `tabindex`. Monatsname: `aria-label` nennt den Monat und das Ziel ("Oktober 2026, zum laufenden Monat"). Suchfeld bekommt einen Namen, `Input` dazu eine `AriaLabel`-Prop.
15. **`--text-muted`** leicht anheben, damit es auf `bg-card` mindestens 4,5:1 erreicht (gemessen 4,46:1). Prüfen auch auf `tealSubtle`, Wert in `styles.css` und `src/Client/public/offline.html`. Den Wert nachrechnen und im Outcome nennen.
16. **`/styleguide` und Doku.** Karten für `selectableListRow`, `stepArrow`, `stepArrowGap`, `homeLabel` mit Zuständen, `ListRoles.missing`. `styleguide.md:109-115`, `:68`, Zählung in §5, `Tokens.fs:20` (Teal), `konzept.md:96` (überholt durch ADR 0077). Kommentare `View.fs:35`, `Types.fs:112` nennen die alte Struktur.
17. `tabular-nums` an den Beträgen der Handy-Zeile (`View.fs:178`).

### Backend
18. **Kein Rohtext im Rahmen.** `YnabClient.fs:359`, `:363`, `:372` und `:492`, `:504`, `:508`: Antworttext nur serverseitig loggen, im Fehler ein fester deutscher Satz mit Status. Nur `getMonths` und `getCategories`, die übrigen Aufrufer folgen in budgeting-c5w2n nicht, sie bleiben Bestand.
19. `targetsWith` in `Api.fs:1733` heißt nach seiner Arbeit, etwa `withYnabPort`.
20. **`budgetMonth` nach `QueryHandlers.fs`.** `getBudgetMonth` nutzt den Helfer aus 19, das Duplikat in `Api.fs:1721-1723` und `fetchMonth` (`:1855`) entfallen. `preview` bleibt, wo es ist.

### Tests
Jeder neue Browser-Wächter bekommt eine Gegenprobe, Ergebnis in die Outcome-Tabelle.
21. Fenster wächst: `frameDueAfterReport` rein in `BudgetPageTests.fs`, dazu Browser-Happy-Path in `move-money.test.mjs`: zweiter `getBudgetFrame` liefert einen Monat mehr, der `›`-Pfeil erscheint.
22. Wiederholen nach erstem Rahmen-Fehler in `budget-page.test.mjs`.
23. Inspektor-Sheet schließen im Browser, im Fall `budget-page.test.mjs:473`.
24. Buchungen beim Kategoriewechsel im Tab Buchungen: am PC andere Zeile, im Sheet `‹ ›`, je `calls.lists` prüfen.
25. Rate-Limit-Tabelle: Zähler für `getBudgetFrame`. `category-detail.test.mjs`: nach der Rückkehr `frames === 1`. `budget-page.test.mjs:307`: dasselbe nach dem Blättern.
26. `stepSelection` mit aktivem Filter (`BudgetPageTests.fs:403`).
27. Vorschau-Reihenfolge im Browser (`targets-apply.test.mjs:192`, `CategoryOrder` anders als `Lines`).
28. Feste Panel-Höhe mit 30 Buchungen und bei 700 px Höhe (`budget-page.test.mjs:447`).
29. Pfeil hoch (`budget-page.test.mjs:528`).
30. "Keine Kategorie in dieser Ansicht." (`budget-page.test.mjs:414`).
31. Umschreiben oder löschen: `BudgetPageTests.fs:392` löschen (Duplikat von `TargetsApplyClientTests.fs:67`). `BudgetPageTests.fs:548` ohne `entriesDue`-Zeile, Name gekürzt. `BudgetPageTests.fs:108` auf `State.update (FrameRefreshed (Error ...))` mit `NoOp`. `budget-page.test.mjs:545-547` wartet einen Frame ab und prüft die Auswahl.

Nicht Teil dieser Task: Listenbeträge gegen ADR 0049 (eigener Backlog-Punkt), `preview` verschieben, `Types.fs` und `View.fs` teilen.

## Acceptance criteria
- [ ] Jeder der 31 Punkte ist umgesetzt oder im Outcome mit Grund als nicht umgesetzt genannt.
- [ ] (Client-`update`) GIVEN Tab Buchungen, `Reloaded` gewinnt den Lauf WHEN die Antwort kommt THEN `entriesDue` liefert die Kategorie.
- [ ] (Client-`update`) GIVEN Monat geladen WHEN `SelectMonth` THEN der alte Monat bleibt bis zur Antwort im Modell lesbar, und eine verspätete Antwort des Vormonats wird verworfen.
- [ ] (Browser) GIVEN Kategorie gewählt, am PC WHEN `‹` THEN Inspektor und Tabs bleiben im DOM, die Liste hat ihren Fokus, kein Spinner erscheint.
- [ ] (Client-`update`) GIVEN gewählter anderer Monat WHEN `PageEntered` THEN `ChosenMonth = None`. Bei `ReturnPending` bleibt er.
- [ ] (Decoder) GIVEN `GET /months` und `GET /categories` antworten 500 mit Rumpf WHEN gedeutet THEN der Fehlertext ist deutsch und enthält den Rumpf nicht.
- [ ] (Browser) Auf der Budget-Seite kommt in Hinweisen keine Pink-Klasse mehr vor.
- [ ] (Browser) Filter-Chips bleiben nach dem Scrollen sichtbar, bei 390 px und 1440 px, und der Spaltenkopf bei 1440 px.
- [ ] (Browser) Bei großer Schrift (Root-Font 20 px) überlappen Leiste und Panel nicht.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Bedienung fühlt sich beim Blättern ruhig an, das Sheet fährt sichtbar ein und aus. [human-eye]

## Notes
Fixtures in `scripts/*.test.mjs` im selben Schritt mitziehen. Kontrast `--text-muted`: ADR 0075 nennt mindestens 4,5:1. `ErrorDisplay.warning` und `Badge.warning` nutzen auch andere Seiten, also neue Varianten statt Änderung.
