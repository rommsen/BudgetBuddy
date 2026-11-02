---
id: budgeting-p4n6r
title: "Review-Korrekturen zur neuen Budget-Seite: Monatswechsel ohne Sprung, Pink raus, Trefferflächen, sticky Filter, Testlücken"
status: done
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

## Verifier note (iteration 1)
REASONS:
- Blockierend (Punkte 14 und 16): `standards/frontend/styleguide.md:255` nennt für `selectableListRow` weiter "gewählt mit `aria-selected` und Teal". Der Styleguide widerspricht damit dem Code.
- Nicht blockierend: Das Outcome nennt 4,50:1 auf `tealSubtle`, genau gemischt sind es 4,48.
- Nicht blockierend: Scheitert das Laden bei bestehender Auswahl, steht im Panel neben der Fehlerkarte ein dauerhaftes Skeleton.
- Nicht blockierend: Der Kommentar an `monthChangeClass` sagt "Bleibt bedienbar", zwei Aktionen sind aber gesperrt.

SUGGESTED_FIX: Styleguide auf `aria-current`, Outcome-Wert korrigieren, Kommentar richtigstellen.

ITERATION_HINT: likely-fixable

## Verifier note (iteration 2)
REASONS:
- `scripts/budget-page.test.mjs:778`, Wächter "Blättern am PC": `settledScroll` wartet nur vor dem Blättern, die Schlussprüfung liest `scrollY` direkt nach `aria-busy=false`. Gegenprobe des Verifiers in einer Scratch-Kopie: Ein injiziertes `window.scrollTo(0,0)` blieb in 3 von 3 Läufen grün.
- Die Vorbedingung `scrollY > 0` ist ersatzlos entfallen.
- Nicht blockierend: "Kategorie wählen" neben der Fehlerkarte weicht von Punkt 2 ab.
- Nicht blockierend: `styles.css:2338` trägt im SVG-Pfeil noch `#7a7a98`.
- Das Produkt selbst ist in Ordnung, `./verify.sh` zeigt `VERIFY: PASS`. Die Iteration-1-Gründe sind behoben.

SUGGESTED_FIX: Nach `aria-busy=false` `await settledScroll(page)`, dann `scrollY` gegen `scrollBefore` prüfen, dazu `scrollBefore > 0` belegen.

ITERATION_HINT: likely-fixable

Entscheidung des Conductors: Scheitert das Laden bei bestehender Auswahl, bleibt das Panel leer, ohne "Kategorie wählen".

## Outcome

Alle 31 Punkte sind umgesetzt, keiner ist ausgelassen. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 3328 grün, 10 opt-in übersprungen, node 449 grün, Stand Iteration 3). `npm run check:readme-delta -- --bc budgeting` Exit 0.

**TDD, ehrlich:** Die Punkte 1, 3, 4, 5, 6 und 18 sind nicht Rot vor Grün entstanden. Ich habe zuerst den Fix geschrieben, dann die Tests. Rot ist nachträglich per Mutante belegt (Tabelle unten), je Punkt der fachliche Test. Das ist schwächer als die Doktrin (Punkt 8) verlangt.

**Wichtige Stellen:**
- Monatswechsel: `monthChanging` und `displayedMonthKind` in `Components/Budget/Types.fs`. Kein Merker, der Zustand folgt aus `Month` gegen `shownMonth`. `select` lässt einen geladenen Monat stehen, `entriesDue`, `openTargets` und `openMove` warten, solange der Wechsel läuft. `applyReloaded` zeigt beim Scheitern während eines Wechsels die Fehlerkarte, sonst bliebe die Seite gedimmt. Fertige Buchungen bleiben bis zur Antwort stehen.
- Leisten: `MeasuredBar` in `Budget/View.fs`, `Viewport.observeHeight` (aus dem Kassenbuch hochgezogen), Regeln in `styles.css` ab `.budget-page`.
- Sheet: `InspectorSheet` in `DesignSystem/BottomSheet.fs` hält den Inhalt 500 ms, zu ist es `inert` und `aria-hidden`.
- Hinweise: `Badge.notice`, `ErrorDisplay.notice`, `Icons.CalmYellow`.
- Tabs: `Tabs.panel`, Pfeiltasten, Home, End, roving `tabindex`.
- Backend: `QueryHandlers.budgetMonth`, `withYnabPort`, in `YnabClient.fs` `interpretCategoriesResponse` und `getCategoriesWith` nach dem Muster von `getMonthsWith`.

**Über den Wortlaut hinaus:**
- Ein Fehler, den erst der Sheet-Wächter zeigte: Der Hinweis `moveRetries` stand unter einem `if` vor Liste und Sheet. Fiel er weg, rückten die Geschwister auf, React baute Liste und Sheet neu auf, das Sheet fuhr nie ein. Der Platz bleibt jetzt auch leer belegt.
- "Targets anwenden" wird im vergangenen Monat `invisible` statt entfernt. Sonst änderte der Kopf beim Blättern die Höhe, die Liste sprang um 14 px.
- `aria-busy` an der Liste, `data-testid="budget-list-area"` neu.
- `getCategories` ändert seine Fehlertexte für alle Aufrufer, auch außerhalb der Budget-Seite. Das folgt aus Punkt 18, weil die Funktion geteilt ist.
- Der Monatsname zeigt beim Blättern sofort das Ziel, alles andere bleibt beim alten Monat. Die Task lässt das offen: sonst liefe schnelles Blättern ins Leere.

**Punkt 11, gemessene Höhe der festen Zeilen (ohne Navbar):**
| Breite | Monatsleiste | Chips | Summe |
|---|---|---|---|
| 390 × 844 | 64 px | 58 px | 122 px |
| 320 × 700 | 107 px | 58 px | 165 px |

**Punkt 15, `--text-muted` von `#7a7a98` auf `#8787a5`:** der kleinste Wert derselben Tonreihe (je Kanal +1), der auf `tealSubtle` exakt gerechnet 4,5:1 erreicht. Nur für `bg-card` hätte `#7b7b99` genügt, aber die gewählte Handy-Zeile trägt muted auf Teal.

Korrektur aus Iteration 2: In Iteration 1 standen hier `#8686a4` und 4,50:1 auf `tealSubtle`. Das war falsch: Ich hatte die Mischung auf `#0f2435` gerundet. Exakt (10 % `#00d4aa` auf `#111128`, also rgb 15,3 / 36,5 / 53,0) erreicht `#8686a4` nur 4,48:1.
| Fläche | `#7a7a98` (alt) | `#8686a4` (Iteration 1) | `#8787a5` (jetzt) |
|---|---|---|---|
| `bg-card` #111128 | 4,46:1 | 5,25:1 | 5,32:1 |
| `bg-elevated` #1a1a36 | 4,07:1 | 4,79:1 | 4,85:1 |
| `bg-hover` #1e1e3c | 3,88:1 | 4,56:1 | 4,62:1 |
| `tealSubtle` exakt gemischt | 3,81:1 | 4,48:1 | 4,54:1 |

`offline.html` definiert kein `--text-muted`, dort war nichts zu ändern. Die Stufe ist größer als "leicht", und sie gilt für alle Seiten.

**Gegenprobe, je Mutante rot gesehen und zurückgenommen.** Die Mutanten liefen gebündelt. Die Spalte nennt die jeweilige Meldung.
| Punkt | Mutante | Rot in |
|---|---|---|
| 1 | `applyLoaded` setzt Buchungen nicht zurück | "Gewinnt das Neu-Lesen gegen den Wechsel…" (.NET) |
| 1 | `Reloaded` ohne `withEntries` | "Überholt das Neu-Lesen…" (Browser). Überlebte zuerst, weil der Wächter die falsche Anfrage zurückhielt. Geschärft, dann rot |
| 2 | `select` setzt `Loading` | "Wechsel lässt den alten Monat…", "Blättern am PC": "der Wechsel läuft" |
| 2 | Knopf `hidden` statt `invisible` | "Blättern am PC": "die Seite springt nicht" |
| 3 | Nachlesen prüft den Monat nicht | "Ändert das stille Nachlesen…" |
| 4 | `beginLoad` ohne `requestMonth` | "Beim Wiederholen wird eine späte Monatsantwort verworfen" |
| 5 | `Error` ergibt `false` | "Scheitert erneutes Senden…" |
| 6 | `ChosenMonth = None` entfernt | "Betreten der Seite setzt…" |
| 7 | ohne `max-height` an `.sheet-pinned` | "viele offene Verschiebungen": Inhalt 20 px |
| 8 | `ErrorDisplay.warning` zurück | "kein Pink beim Hinweis Stand geändert" |
| 9 | `.sheet-close` 32 px | "Schließen 48 px" |
| 10 | Panel `top: 80px` fest | "Leiste und Panel überlappen nicht" |
| 10, 28 | Panel ohne `max-height` | "das Panel endet in der Sichthöhe" |
| 11 | Chips `position: static` | "die Chips liegen unter der Leiste" |
| 12 | Karte `overflow-hidden` | "der Spaltenkopf liegt unter den Chips" |
| 12 | Regel für den Kopf entfernt | dieselbe Meldung |
| 13, 23 | Sheet zu nicht gerendert | "das Sheet steht schon vor dem Öffnen im DOM" |
| 14 | `tr` mit `aria-selected` | "Inspektor am PC" (Attribut) |
| 14 | Tabs ohne `onKeyDown` | "Barrierefreiheit" (Timeout auf den Fokus) |
| 14 | Suchfeld ohne `AriaLabel` | "Barrierefreiheit". Der erste Wächter blieb hier grün, weil der Platzhalter als Name zählt. Geschärft auf das Attribut |
| 18 | Rumpf, Parse-Text und Ausnahmetext im Fehler | die vier Tests "Kein Antworttext…" und "Eine Ausnahme beim Abruf…" |
| 21 | `frameRefreshCmd` nach `MoveAnswered` entfernt | "Fenster wächst" |
| 22 | `Load` tut nichts | "Wiederholen nach dem ersten Rahmen-Fehler" |
| 24 | `StepCategory` ohne `withEntries` | "Kategoriewechsel im Tab Buchungen" |
| 25 | `SelectMonth` liest den Rahmen mit | "Monate: bis zum Rand blättern" (`frames`) |
| 27 | View ohne `orderedPreviewLines` | "Die Vorschau steht in Romans Reihenfolge" |
| 29 | `ArrowUp` entfernt | "Tastatur am PC" |
| 30 | Text geändert | "Filter ohne Treffer" |

Nicht einzeln belegt:
- Die Mutanten für 13, 14, 22, 24, 29 und 30 liefen im selben Bündel wie die Mutanten der Rand-Fälle. Die Timeouts passen zur Mutante, ein Mitverursacher ist nicht ausgeschlossen.
- Bei `top: 80px` schlug die Prüfung schon im Durchlauf ohne große Schrift an. Der Fall Root-Font 20 px ist grün, aber nicht für sich rot gesehen.
- Punkt 26 hat keine eigene Mutante, nur den Test "Mit aktivem Filter überspringt der Schritt…".

**Iteration 2 (Verifier-FAIL, behoben):**
- `styleguide.md:255` nennt `aria-current`. Andere `aria-selected` für Budget-Zeilen stehen im Styleguide nicht.
- Kommentar an `monthChangeClass` nennt jetzt, was bedienbar bleibt und was gesperrt ist.
- Scheitert das Laden bei bestehender Auswahl, steht im Panel kein dauerhaftes Skeleton mehr, es gilt nur, solange der Monat lädt. Iteration 2 zeigte dort den Hinweis, das hat Iteration 3 ersetzt (siehe unten).
- `--text-muted` korrigiert, siehe Punkt 15.
- Der Wächter "Blättern am PC" war unzuverlässig, im Gate rot in etwa der Hälfte der Läufe (`die Seite springt nicht`). Zwei Annahmen dazu waren falsch und wurden verworfen. Die gemessene Ursache: `html` hat `scroll-behavior: smooth`, das `scrollIntoView` der gewählten Zeile läuft animiert, der Wächter las `scrollY` mitten darin. Die Spur eines roten Laufs zeigt nur diese Animation. Der Wächter wartet jetzt, bis `scrollY` 300 ms steht (`settledScroll`). Danach vier Läufe `./verify.sh` in Folge `VERIFY: PASS`. Die Gegenprobe "Knopf `hidden` statt `invisible`" lief danach erneut und blieb rot (979 gegen 993), der Wächter fängt echte Sprünge also weiter.
- Einmal, in einem roten Gate-Lauf, schlug zusätzlich `ledger-read-retry.test.mjs:223` fehl ("keine Wiederholung"). Die Datei ist in dieser Task unverändert, die Ursache habe ich nicht untersucht. In den vier Läufen danach trat es nicht wieder auf. Es kann ein bestehender Wackelkandidat sein, belegt ist das nicht. Folgepunkt im Backlog.

**Iteration 3 (Verifier-FAIL, behoben):**
- Scheitert das Laden bei bestehender Auswahl, bleibt das Panel leer. "Kategorie wählen" gilt nur bei `Selected = None`, wie Punkt 2 verlangt. Der Test prüft leeren Text und kein `animate-pulse`.
- Wächter "Blättern am PC": `assert.ok(scrollBefore > 0)` als Vorbedingung, nach `aria-busy=false` wartet er mit `settledScroll` und vergleicht erst dann. Ohne das hätte die Animation einen späten Sprung verdeckt.
- `styles.css:2338`: Der SVG-Pfeil nutzt `#8787a5`, `#7a7a98` steht nirgends mehr in `src/Client`.

| Wächter | Gegenprobe | Ergebnis |
|---|---|---|
| Blättern am PC | `window.scrollTo(0, 0)` nach `aria-busy=false` injiziert | rot, "die Seite springt nicht", 0 gegen 1211 |
| Scheitert das Blättern | Zweig für das leere Panel entfernt, Hinweis erscheint | rot, "das Panel bleibt leer…" |

`budget-page.test.mjs` danach dreimal einzeln grün, `./verify.sh` einmal `VERIFY: PASS`. Beide Gegenproben sind zurückgenommen.

**Punkt 1:** Der AC verlangt die Ebene Client-`update`. `Reloaded` erzeugt dort ein Cmd mit `Client.Api`, das unter .NET nicht läuft. Die Verdrahtung belegt deshalb der Browser-Wächter, die .NET-Ebene prüft `entriesDue` nach `applyReloaded`.

**Punkt 31:** `BudgetPageTests.fs:392` gelöscht, `:548` gekürzt, `:108` über `State.update`, `budget-page.test.mjs:545` wartet Frames ab und prüft die Auswahl. Der Wächter an `TargetsApplyClientTests.fs:192` nennt `ErrorDisplay.notice`.

**Bekannte Grenzen:**
- Rückkehr aus dem Kassenbuch und Schließen des Targets-Dialogs setzen weiter `Loading`, dort springt die Liste noch. Die Task nennt nur das Blättern. Das Panel zeigt beim Laden ein Skeleton. Folgepunkt im Backlog.
- `.budget-row-anchor` rechnet die Höhe des Spaltenkopfs als `3rem`, sie ist nicht gemessen.
- Viele offene Verschiebungen scrollen im festen Bereich zusammen mit Stepper und Tabs: Punkt 7 begrenzt `.sheet-pinned` als Ganzes (55 %).
- Der gehaltene Sheet-Inhalt läuft über eine feste Zeit von 500 ms. Der Wächter misst den Beginn des Übergangs, weil der Headless-Browser zu wenig Frames liefert.
- Während eines Monatswechsels tun "Verschieben" und "Targets anwenden" beim Klick nichts, sie sehen aber nur gedimmt aus.

**[human-eye] offen für Roman.** Ruhe beim Blättern und die Ein- und Ausfahrt des Sheets habe ich nur gemessen, nicht angesehen. Screenshots liegen nicht vor.
