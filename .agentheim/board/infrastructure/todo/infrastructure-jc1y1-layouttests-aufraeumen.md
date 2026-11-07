---
id: infrastructure-jc1y1
title: "Die 61 [layout]-Tests aufräumen: Designwert-Assertions löschen, Invarianten dem Sweep überlassen, Verhaltens-Assertions in passende Tests oder nach .NET verschieben"
status: todo
type: chore
context: infrastructure
created: 2026-11-05
completed:
depends_on: [infrastructure-nx14s]
blocks: []
tags: [etappe-1, tests, browser, layout, doktrin]
related_adrs: []
related_research: []
prior_art: [infrastructure-t5m2b, infrastructure-b3t9k]
---

## Why
Siehe `infrastructure-nx14s`. Roman will vor einem Design-Overhaul keine Tests mehr nachziehen, die nur festhalten, wie etwas aussieht. Nach dem Sweep aus nx14s sind die Invarianten generisch gedeckt, die Designwerte können weg.

## What
Jeden der 61 Tests im Anhang nach seiner Einordnung behandeln:
- **LÖSCHEN-Assertions** entfernen. Bleibt danach nichts übrig, fliegt der Test.
- **INVARIANTE-Assertions** entfernen, sobald der Sweep aus nx14s denselben Zustand prüft. Fehlt der Zustand im Sweep, dort nachtragen, nicht im Einzeltest behalten.
- **BEHALTEN-Assertions** auf die billigste Ebene nach Doktrin Punkt 1 verschieben: Logik (welcher Text, welche Zahl, welcher Aufruf) in Client-`update` oder eine reine Funktion unter .NET, echte Browser-Aussagen in den passenden `[flow:…]`-, `[tastatur]`- oder `[fokus]`-Test. Ist sie dort schon gedeckt (im Anhang als Dublette vermerkt), nur löschen.
- Hilfsfunktionen, die nur Designwerte messen (`tokenColor`, `headParts`, `MIN_NAME_WIDTH`, `MIN_WIDTH`, `opacityOf`), entfernen, wenn kein Test sie mehr nutzt.

Bugbezogene Tests (Spalte Bugbezug im Anhang) brauchen vor dem Löschen einen Nachweis, wo ihr Regressionsfall weiterlebt: im Sweep, in einem Verhaltens-Test oder in `guard:tokens` (etwa "kein Pink" aus `targets-apply:165`, ADR 0075).

## Acceptance criteria
- [ ] Kein Test unter `scripts/` prüft mehr einen festen Designwert: exakte Höhen, Breiten oder Abstände, Farben über Tokens, Reihenfolge oder Position von Knöpfen, Animationen, Zahl sichtbarer Zeilen. Ausgenommen sind die Styleguide-Schwellen im Sweep.
- [ ] Für jeden Test mit Bugbezug im Anhang nennt das Outcome, wo sein Regressionsfall jetzt geprüft wird (Datei und Test oder Sweep-Zustand).
- [ ] Jede BEHALTEN-Assertion aus dem Anhang ist verschoben oder als Dublette nachgewiesen gelöscht. `category-detail.test.mjs:159` trägt nicht mehr `[layout]`.
- [ ] `npm run test:scripts` meldet in "Browser-Tests je Kennung" deutlich weniger `[layout]`. Die Zahl vorher und nachher steht im Outcome.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Entscheidet `infrastructure-n2w8q` Punkt 1 (gemischte Tests) für `[layout]`: Logik-Teilaussagen wandern aus gekennzeichneten Tests nach .NET oder in den passenden Flow. Die übrigen Punkte von n2w8q bleiben offen.
- Flake-Kandidaten, die mit dem Aufräumen wegfallen sollten: `ledger-status-column:324` (laut Kommentar 30 bis 40 % Fehlläufe), `budget-page:735` (hängt an `transitionrun`), `sync-start:92` (wartet auf alle Animationen, hängt bei einem Spinner). Verhaltens-Assertions, die nach zwei Frames eine Negativaussage machen (`rules-page-mobile:428`, `move-money:198`), beim Verschieben auf eine echte Bedingung warten lassen.
- Die Einordnung haben zwei Agents am 2026-11-05 aus den Testkörpern erstellt, die Zeilennummern beziehen sich auf den Stand von Commit 03b6d8e.

## Anhang: Einordnung der 61 Tests

### [layout]-Tests in den Budget-Dateien

37 Tests mit `[layout]` im Titel. Die Kategorie in der Tabelle ist die überwiegende, bei gemischten Tests steht die Aufteilung der Assertions in der Begründung. "Bugbezug" nennt Kennungen aus Kommentaren und den Commit, der den Testtitel eingeführt hat (`git log -S`), sofern er Bug- oder Review-Korrektur ist.

| Datei:Zeile | Titel (gekürzt) | Kategorie | Begründung | Bugbezug | Flake-Risiko |
|---|---|---|---|---|---|
| budget-page.test.mjs:192 | Handy: dritte Zeile mit Target, nichts abgeschnitten, 320 px Status sichtbar | INVARIANTE (gemischt) | Invariante: kein `span` mit `scrollWidth > clientWidth`, Status bei 320 px `right <= innerWidth`; Löschen: `deepEqual` auf die exakten Target-Texte (gehört in .NET). | Kommentar "Bug F3", Fixture budgeting-kjsht; eingeführt in 6c82444 (feature) | gering |
| budget-page.test.mjs:222 | PC: Spalte Target hinter Kategorie, Gruppenzeile spannt alle Spalten | LÖSCHEN | `headers[1] === "target"`, genau 5 Spalten, `colspan='5'`, Betrag in Mono, Rhythmus nicht: Spaltenfolge und Schriftwahl. | keiner | gering |
| budget-page.test.mjs:263 | Monate: am Rand bleibt der Platz des Pfeils frei, Name teal, Klick zurück | LÖSCHEN (gemischt) | Löschen: `atEdge.x === atStart.x` (Platzhalter für Pfeil), Farbe `--color-neon-teal`; Behalten: Klick auf den Namen führt zu "Oktober 2026" zurück, laut Kommentar schon in BudgetPageTests. | eingeführt in 86ea682 (Testreduktion infrastructure-t5m2b) | mittel: `atStart` wird direkt nach dem Laden gemessen, ohne auf `animate-page-enter` zu warten (Test :1265 wartet dafür ausdrücklich) |
| budget-page.test.mjs:301 | Filter: fehlt in Gelb, Verfügbar im Minus in Rot | LÖSCHEN | Vergleicht `color` mit `--color-calm-yellow` und `--color-neon-red`. | Fixture budgeting-g8s3w | gering |
| budget-page.test.mjs:321 | Inspektor am PC: gleiche Höhe in allen vier Tabs, Zeile markiert | LÖSCHEN (gemischt) | Löschen: Höhe `=== 640`, Breite `=== 340`, gleiche Höhe je Tab; Behalten: `aria-current="true"` an der gewählten Zeile (am Handy schon im [tastatur]-Test Barrierefreiheit). | keiner | gering |
| budget-page.test.mjs:381 | Fokus am PC: Tab Geld per Pfeil wie per Kürzel, Ring am Chip auf dem Pillenrand, nach Mausklick keiner | BEHALTEN (gemischt) | Behalten: Pfeil aus der Suche wechselt den Tab (Dublette zu [tastatur] "Tastatur in Feldern"), `:focus-visible` nach Mausklick false und nach Tab true (gehört in [fokus]); Löschen: `outlineOffset === 0`, `gap <= 0.5`, Rundung, `tabLook` gleich. | Kommentar "Regression (design-system-k4f9t, design-system-w7c2h)", Commit 0f9453c bug(design-system) | gering, `twoFrames` statt Timeout |
| budget-page.test.mjs:430 | Sheet: 500 px in allen Tabs, 48 px Tabs, ˄ ˅ ohne Schließen, Kopf und Tabs stehen, --vvh schrumpft | INVARIANTE (gemischt) | Invariante: Tabs `>= 48`, `.sheet-header` und `tablist` behalten `top` beim Scrollen des Body, Sheet schrumpft mit kleinerem Viewport; Löschen: Höhe `=== 500` und exakt `=== 400`; Behalten: ˄ ˅ wechseln die Kategorie, am Rand fehlt der Pfeil ([tastatur]/[flow]). | keiner | mittel: `waitForFunction` auf exakt 400 px, Messung des Sheets ohne Warten auf das Ende der Einfahranimation |
| budget-page.test.mjs:499 | Trefferflächen am Handy 48 px, bei 320 px kein seitliches Scrollen | INVARIANTE (gemischt) | Invariante: Monatspfeile, Chips, ˄ ˅, Schließen, Suche leeren `>= 48`, `scrollWidth <= clientWidth`; Löschen: `::before`-Breite `=== "32px"`. | eingeführt in 6c82444 (feature) | gering |
| budget-page.test.mjs:658 | Handy: nach dem Scrollen Monatsleiste unter der Navbar, Chips haften, Suche scrollt mit | INVARIANTE (gemischt) | Invariante: Leiste `top >= navBottom`, `elementFromPoint` trifft Monat und Chips (nichts verdeckt); Löschen: Chips haften direkt unter der Leiste, Suche ist weggescrollt (welche Teile haften, ist Design). | Kommentar "Bug D3"; eingeführt in 6c82444 | gering bis mittel: `scrollToEnd` wartet nur auf `scrollY > 300`, nicht auf Stillstand |
| budget-page.test.mjs:678 | PC: nach dem Scrollen haftet der Kopf, Spaltenkopf darunter, auch bei großer Schrift | INVARIANTE (gemischt) | Invariante: Kopf unter der Navigation, nichts verdeckt die Chips, Panel `y >= bar.bottom` (kein Überlappen), auch bei 20 px Root-Schrift; Löschen: Chips und Suche liegen im Kopf, Spaltenkopf `< bar.bottom + 24`. | keiner | gering bis mittel (wie :658) |
| budget-page.test.mjs:735 | Sheet: Schließen fährt sichtbar aus, Inhalt hält sich, Auswahl weg | LÖSCHEN (gemischt) | Löschen: `transitionrun` auf `transform` beim Öffnen und Schließen (Animation); Behalten: Titel bleibt während der Ausfahrt, `inert` beim Schließen, danach `aria-current` weg. | eingeführt in 5850313 bug(budgeting) | mittel: hängt an Übergangs-Events, Kommentar nennt seltene Frames im Headless-Browser |
| budget-page.test.mjs:886 | PC: 1440 × 900 zeigt mindestens 16 Kategorien, je höchstens 40 px, einzeilig | LÖSCHEN | `inView >= 16`, erste Zeile im oberen Viertel, Zeile `<= 40`, Gruppenkopf `<= 32`, Target `<= 24`, fehlt neben dem Namen: Dichte und Zeilenmaß. | budgeting-h3k9d (Abschnitt) | gering |
| budget-page.test.mjs:919 | Handy 375 px: Suche teilt Zeile nur mit Targets anwenden, Historie im Kopf mit 48 px | LÖSCHEN (gemischt) | Löschen: Suche nimmt den Rest neben Targets anwenden, Historie im Monatskopf; Invariante: Historie `round >= 48`; Suche `>= 160` ist ein Grenzfall (Feld benutzbar). | Kommentare budgeting-c6vn4 (M1), budgeting-h7q4v, budgeting-r7h2q; Commit 9faa461 (Review-Korrekturen) | gering |
| budget-page.test.mjs:968 | Kopf: ab 1024 px eine Zeile in der verlangten Reihenfolge, bei 768 px zwei Zeilen | LÖSCHEN (gemischt) | Löschen: `sharesLine` mit dem Monat und Reihenfolge Monat, Suche, Filter, Zu verteilen, Targets, Historie, bei 768 px `<= 130` Höhe; Invariante: `noSideScroll` bei 768 px. | Kommentar budgeting-c6vn4 (M1), Fixture budgeting-a3k7m | gering |
| budget-page.test.mjs:1011 | Breiten 320 bis 1920: kein seitliches Scrollen, Tabelle in ihrer Karte, Panel ab 1024 | INVARIANTE (gemischt) | Invariante: `noSideScroll` je Breite, Tabelle und letzte Spalte `<= card.right` (nichts abgeschnitten), langer Name `>= 80` bei 768; Löschen: Panel genau ab 1024 px (Breakpoint). | keiner | gering |
| budget-page.test.mjs:1042 | Volle Breite: Liste wächst, Panel bleibt 340 px, Seitenrand bleibt | LÖSCHEN | Panel `=== 340`, Rand links und rechts `=== 24`, Liste wächst um `>= 600`. | budgeting-h3k9d; eingeführt in 3a1cd35 (feature) | gering |
| budget-page.test.mjs:1212 | Sheet: ˄ ˅ rechts neben Schließen, je 48 px, keine ‹ ›-Zeile | LÖSCHEN (gemischt) | Löschen: Reihenfolge Titel, ˄, ˅, Schließen, gleiche `y`, kein `‹›`, Text "Alltag · Oktober 2026", Monat kein Knopf, Titelbreite bleibt am Rand; Invariante: ˄ ˅ `>= 48`. | keiner | gering, wartet auf `getAnimations().finished` |
| budget-page.test.mjs:1245 | Sheet: langer Name kürzt im Kopf, Knöpfe bleiben im Bild | INVARIANTE (gemischt) | Invariante: Sheet `scrollWidth <= clientWidth`, Schließen `right <= 320`; Löschen: Kopf `<= 80` px. | keiner | gering, wartet auf Animationen |
| budget-page.test.mjs:1265 | Handy: Targets anwenden in der Zeile der Suche, im vergangenen Monat bleibt der Platz frei | LÖSCHEN | `sharesLine(search, targets)`, `sharesLine(month, assign)`, Suche behält `right` und erste Zeile springt nicht, wenn der Knopf ausblendet (Platzierung; "kein Sprung" wäre eine neue Invariante). | eingeführt in 3a1cd35 (feature) | gering, wartet auf Animationen und `visibility` |
| budget-page.test.mjs:1293 | Tab Historie am Handy: Fehlgrund mit Ellipse, Datum einzeilig, Betrag neutral | LÖSCHEN (gemischt) | Löschen: `textOverflow === "ellipsis"`, Datum einzeilig, Betrag in `--color-text-primary`; Invariante: Sheet ohne seitlichen Überlauf, Grund `<= rowRight`; Behalten: ganzer Grund im `title`. | Kommentare budgeting-x4k7p D2, budgeting-h7q4v H2; Commit 46518f1 (Review-Korrekturen) | gering |
| budget-page.test.mjs:1448 | Ausgaben-Balken am Handy: Wertzeile hält Höhe, bleibt am ersten und letzten Balken im Sheet | INVARIANTE (gemischt) | Invariante: Wert `left/right` im Sheet, kein seitlicher Überlauf; Löschen: Zeile `>= 16`, Balken und Zeile gleich hoch mit und ohne Wert. | budgeting-v6m3z | gering |
| budget-money-tab.test.mjs:182 | Handy: Sheet in allen drei Modi 500 px, Feld, sechs Schritte und Knopf ohne Scrollen | INVARIANTE (gemischt) | Invariante: `insideBody` für Feld, Schritte und Fehlbetrag-Knopf (Primärknopf ohne Scrollen erreichbar); Löschen: Höhe `=== 500`, genau 6 Schritte. | budgeting-c5w2n | gering |
| budget-money-tab.test.mjs:216 | Handy mit offener Tastatur: Sheet schrumpft, Feld und Verfügbar danach sichtbar | INVARIANTE | `insideBody` für Feld und Verfügbar danach bei verkleinertem Viewport; der exakte Wert `=== 408` wäre zu lösen. | budgeting-c5w2n | mittel: `waitForFunction` auf exakt 408 px |
| budget-money-tab.test.mjs:302 | Lange Kategorienamen in von, nach und Knopf vollständig | INVARIANTE (gemischt) | Invariante: `nothingCut` und `innerText` enthält den ganzen Namen, Handy und PC; Behalten nebenbei: Picker-Auswahl landet in von und nach. | keiner | gering |
| budget-money-tab.test.mjs:328 | Handy: Picker über dem Inspektor, Zuletzt verwendet, schließt ohne Ghost-Click | BEHALTEN | Tipp auf Zu verteilen übernimmt die Wahl, Inspektor bleibt sichtbar, Modus Holen bleibt `aria-pressed` (kein Ghost-Click, ADR 0005); passt als [flow] oder [fokus]. | eingeführt in d0a794d (feature) | gering, `twoFrames` |
| budget-money-tab.test.mjs:348 | Handy: Hinweis einer offenen Verschiebung im festen Bereich, in jedem Tab sichtbar | BEHALTEN (gemischt) | Behalten: nach fehlgeschlagenem `moveMoney` ist `move-retry` in jedem Tab sichtbar; Löschen: Selektor `.sheet-pinned` legt die Stelle fest. | eingeführt in 86ea682 (Testreduktion) | gering |
| budget-history.test.mjs:145 | Handy: Filterleiste haftet, Chevron gedämpft und dreht, Monat und Teil-Fehlschlag kurz, Kopf bricht um | LÖSCHEN (gemischt) | Löschen: Chevron in `--color-text-muted`, `rotate === "180deg"`, Texte "1 von 2" und "Nov. 2026", höchstens 2 Zeilen, `overflowX === "auto"`, Leiste exakt an der Navbar; Invariante: Kopftext, Betrag und Statuszeile nicht abgeschnitten; Behalten: Aufklappen (schon im [flow:budget-historie]). | Kommentare budgeting-c6vn4 (A3, A4, A6), budgeting-a3k7m; Commit 9faa461 (Review-Korrekturen) | gering bis mittel: wartet auf das Ende der Drehung, Scroll ohne Stillstandsprüfung |
| category-detail.test.mjs:159 | PC: Inspektor als Panel, Sprung ins Kassenbuch und zurück, Kontowechsel lädt nicht neu | BEHALTEN | Prüft Beträge im Panel, Sprung zur Buchung, Esc zurück, `calls.accounts` unverändert nach Kontowechsel, Browser-Zurück: reiner Ablauf, falsch als [layout] gekennzeichnet; gehört als PC-Variante zu [flow:budget-zur-buchung] oder in einen eigenen Flow. | Kommentar infrastructure-b3t9k | gering |
| category-detail.test.mjs:192 | Handy: Rand im Sheet einfach, großer Betrag überlappt bei 360 px nicht | INVARIANTE (gemischt) | Invariante: `payeeRight <= amountLeft` (kein Überlappen), Betrag im Viewport; Löschen: Zeile `left === 0`, Rand `=== 16`. | Commit 66a7c32 fix(budgeting) Review-Findings budgeting-m1bce, design-system-t7kqz | gering |
| category-picker.test.mjs:40 | PC: Popover mit Suche, Ohne Kategorie, Vorgeschlagen, Beträgen rechts, Stand-Zeile | LÖSCHEN (gemischt) | Löschen: erste Option "Ohne Kategorie", Kopf "Alle Kategorien", "Stand 14:02", kein Übernehmen, Betragsslots `.w-24` bündig; Behalten: Fokus im Suchfeld ([fokus]), `aria-selected`, Versteckt aufklappen. | design-system-gffps | gering |
| category-picker.test.mjs:107 | Handy (390): Sheet mit denselben Zeilen, 16 px Suchschrift, Betragsslots bündig | INVARIANTE (gemischt) | Invariante: Suchschrift `=== "16px"`, Zeilen `>= 43.5`; Löschen: Titel "Kategorie wählen", erste Option, bündige Slots; Behalten: Tipp wählt und schließt. | design-system-gffps | gering, wartet per `raf` auf stabile Lage |
| target-change.test.mjs:127 | Handy: Target speichern im 500-px-Sheet ohne Scrollen im Bild | INVARIANTE | Knopf `y + height <= body.y + body.height` (Primärknopf erreichbar); die 500 px stehen nur im Titel, geprüft wird nur die Unterkante. | budgeting-k8wq3 | gering |
| target-change.test.mjs:214 | PC: Platzhalter des Datumsfelds ganz im Feld | INVARIANTE | `scrollWidth <= clientWidth` und gemessene Platzhalterbreite `<= room` (nichts abgeschnitten). | Kommentar budgeting-g2ty9 (X6); Commit bd78ffc (Review-Korrekturen) | gering |
| target-change.test.mjs:258 | Handy: Target anlegen und Warnung vor dem Wechsel im Sheet ohne Scrollen | INVARIANTE | `inView` für "Target anlegen" und "Wechseln" im Alertdialog: oben und unten innerhalb `.sheet-body`. | budgeting-xp46a | gering |
| targets-apply.test.mjs:125 | Handy: Summe und Zu verteilen danach sichtbar, Übernehmen ohne Scrollen | INVARIANTE (gemischt) | Invariante: `targets-total`, `targets-after` und Übernehmen im Viewport, kein seitliches Scrollen; Behalten: Vorschau für den laufenden Monat, 40 Zeilen, pausierte übersprungen, `apply` nicht aufgerufen (gehört in einen [flow]). | budgeting-h2zfn | gering |
| targets-apply.test.mjs:151 | Handy: Vorschau-Zeile zeigt Target, Zugewiesen, Fehlbetrag vollständig | INVARIANTE (gemischt) | Invariante: `scrollWidth/scrollHeight` nicht größer als `client*`; Löschen: exakter Text (gehört in .NET). | Kommentar budgeting-kjsht | gering |
| targets-apply.test.mjs:165 | Hinweise im Dialog in Gelb, kein Pink (ADR 0075) | LÖSCHEN (gemischt) | Löschen: Icon und Badge in `--color-calm-yellow`, keine Klasse `neon-pink` (Farbregel, eher Sache von `guard:tokens`); Behalten nebenbei: PreviewChanged, dann Applied zeigt `targets-changed` und `targets-differs`. | ADR 0075; Commit 5850313 bug(budgeting) | gering |

#### Zahlen je Kategorie (überwiegende Einordnung)

- LÖSCHEN: 15 (davon 10 gemischt)
- INVARIANTE: 18 (davon 13 gemischt)
- BEHALTEN: 4 (davon 2 gemischt)

Tests aus einem Commit mit Präfix `bug(...)`: budget-page.test.mjs:381 (0f9453c), budget-page.test.mjs:735 und targets-apply.test.mjs:165 (5850313). Vor dem Löschen klären, ob der jeweilige Regressionsfall anders abgedeckt bleibt (Doktrin: Bugfix nur mit Regressionstest). Weitere Tests stammen aus Review-Korrektur-Commits (`refactor`/`fix`): :919, :1293, budget-history:145, target-change:214, category-detail:192.

Kein Test nutzt `waitForTimeout` oder ein festes `setTimeout`-Warten; einzig `settledScroll` (budget-page.test.mjs:557) pollt mit `setTimeout(check, 30)` auf 300 ms Stillstand, wird aber nur von [scroll]-Tests genutzt.

#### Invarianten in diesen Dateien und vorhandene Hilfsfunktionen

Es gibt kein gemeinsames Hilfsmodul; jede Datei hat eigene, teils doppelte Helfer (kein seitliches Scrollen steht in budget-page als Funktion, in targets-apply und mehreren Tests inline).

1. **Kein seitliches Scrollen** (Seite und Sheet)
   - `noSideScroll` budget-page.test.mjs:874 (genutzt in :968, :1011)
   - inline `document.documentElement.scrollWidth > clientWidth` in budget-page.test.mjs:499 und targets-apply.test.mjs:125
   - inline `el.scrollWidth > el.clientWidth` am Sheet in budget-page.test.mjs:1245, :1293, :1448
2. **Trefferfläche >= 48 px (Handy) bzw. >= 44 px**
   - kein Helfer, jeweils `boundingBox()` inline: budget-page.test.mjs:430, :499, :919 (mit `Math.round`, budgeting-r7h2q), :1212; category-picker.test.mjs:107 (`>= 43.5`)
3. **Input-Schrift >= 16 px am Handy**
   - inline `getComputedStyle(input).fontSize` in category-picker.test.mjs:107 (der [fokus]-Test budget-money-tab.test.mjs:231 prüft das bei :253 ebenfalls, ist aber kein [layout])
4. **Nichts abgeschnitten (Text, Platzhalter, Zeilen)**
   - `nothingCut` budget-money-tab.test.mjs:108 (genutzt in :302)
   - `targetTexts` budget-page.test.mjs:184 liefert nur Texte; das Abschneiden prüft :192 inline über `scrollWidth > clientWidth`
   - inline in budget-history.test.mjs:145, targets-apply.test.mjs:151, target-change.test.mjs:214 (Canvas-Messung des Platzhalters)
5. **Nichts überlappt**
   - `sharesLine` budget-page.test.mjs:872 (zeigt gleiche Zeile, für Überlappung umgekehrt nutzbar)
   - inline `payeeRight <= amountLeft` category-detail.test.mjs:192; `panel.y >= bar.bottom` budget-page.test.mjs:678
6. **Element ganz im sichtbaren Bereich (Viewport, Sheet-Body)**
   - `insideBody` budget-money-tab.test.mjs:114 (genutzt in :182, :216)
   - `inView` target-change.test.mjs:259 (lokal im Test, prüft oben und unten)
   - inline Unterkante gegen `.sheet-body` target-change.test.mjs:127; gegen Viewport targets-apply.test.mjs:125, budget-page.test.mjs:192, :1245, :1448
7. **Bei offener Tastatur Feld und Primärknopf erreichbar**
   - Simulation per `page.setViewportSize` (schreibt `--vvh`), dann `insideBody`: budget-money-tab.test.mjs:216; nur Schrumpfen ohne Feldprüfung: budget-page.test.mjs:430
8. **Sticky-Kopf verdeckt keinen Inhalt**
   - `stickyRects` budget-page.test.mjs:627 (Navbar-Unterkante, Leiste, Chips, `elementFromPoint` für Monat und Chips; genutzt in :658, :678)
   - `scrollToEnd` budget-page.test.mjs:651 (blendet `.toast-layer` aus und scrollt)
   - inline in budget-history.test.mjs:145 (Leiste an der Navbar) und im Sheet budget-page.test.mjs:430 (Kopf und Tabs behalten `top`)
9. **Fokus-Ring nur bei Tastatur** (Kandidat, steht nicht in der Vorgabeliste)
   - `focusRing` budget-page.test.mjs:347 (liefert `focusVisible`), `tabLook` :372

Hilfsfunktionen für Design-Werte, die mit den LÖSCHEN-Assertions entfallen würden: `tokenColor` budget-page.test.mjs:247 (Farbvergleich, auch inline in budget-history:145 und targets-apply:165), `headParts` budget-page.test.mjs:853 (Lage der Kopfteile), `MIN_NAME_WIDTH` budget-page.test.mjs:182.

### [layout]-Tests: Einordnung nach Assertions

24 Tests in 17 Dateien. Kürzel: LÖ = LÖSCHEN, INV = INVARIANTE, BEH = BEHALTEN. Pfade relativ zu `scripts/`.

| Datei:Zeile | Titel (gekürzt) | Kategorie | Begründung (konkrete Assertion) | Bugbezug | Flake-Risiko |
|---|---|---|---|---|---|
| ledger-external-links.test.mjs:102 | Handy: Sheet zeigt PayPal-Link, ≥ 44 px | gemischt: INV + BEH | `box.height >= 44` ist die Trefferflächen-Invariante (Achtung: 44 statt 48 am Handy); `href` und `target="_blank"` sind Verhalten und gehören in `[flow:detail]` oder einen MVU-Test. | Feature ledger-rjhwp (00c3057), kein Bug | gering |
| ledger-category-change.test.mjs:216 | Handy: Picker als zweites Sheet, Karte unter Kategorie-Feld | gemischt: LÖ + INV + BEH | LÖ: zwei aktive Sheets, `picker.z > detail.z`, `cardTop >= rowBottom` (Platzierung der Karte); INV: Toast-Knopf „Rückgängig“ ist per `elementFromPoint` oberstes Element (nicht verdeckt); BEH: Picker schließt nach Wahl, Rückgängig sendet `setCalls[1]` mit alter Kategorie (gehört in `[flow:kategorie-wechsel]`). | Regression c819eeb `bug(design-system)` (Toast hinter Detail-Sheet, Kommentar Z. 252); Feature ledger-sfab0 | mittel: Tap-Wiederholungsschleife gegen das 350-ms-Ghost-Click-Fenster, `pollUntil(…, 500)` |
| ledger-compact-mouse.test.mjs:114 | Maus 900 px: Kästchen, Umschalt-Klick, Tasten, Hover, Sheet | gemischt: BEH (überwiegend) + LÖ | BEH: Klick wählt, Umschalt-Klick wählt Spanne 1 bis 5 (`aria-checked`), Esc hebt auf, Pfeil markiert, `g` ruft `setReviewMark`, Fokus springt auf Zeile 2, Leertaste wählt, Enter öffnet Sheet (gehört in `[tastatur]` und `[flow:auswahl]`); LÖ: `opacityOf(...) === 0/1` bei Hover und Kästchen „ohne Hover sichtbar“. | Feature ledger-m6zpd (4ea4099) | gering: Hover wartet per rAF auf Deckkraft 1 |
| ledger-review-button-visibility.test.mjs:73 | Prüf-Schaltfläche unsichtbar mit Breite, Hover/Fokus sichtbar, neutral | LÖ | `idle.width >= 27`, Deckkraft 0/1 bei Hover, Farbspanne `< 60` (neutral grau) sind reine Designwerte; nur „bei Tastaturfokus Deckkraft 1“ wäre als generische Invariante „fokussiertes Element ist sichtbar“ zu retten, `title` als a11y-Check in MVU. | Feature ledger-mhe2s (cc26eca) | gering: wartet per `waitForFunction` auf Übergangsende |
| ledger-filter-bar.test.mjs:159 | Input.Small am Handy 16 px, am PC 14 px, Medium 16, Large 18 | gemischt: INV + LÖ | INV: Handy `small/medium = "16px"`, Datumsfeld und Kategorie-Suche am Handy `"16px"` (iOS-Zoom); LÖ: PC `"14px"` und Large `"18px"`. | Bug design-system-btwhg (04be7c9 `bug(design-system)`), m7z9q | gering |
| ledger-filter-bar.test.mjs:229 | Handy: Chip öffnet BottomSheet, außen tippen filtert nicht, zwei Kategorien filtern | gemischt: BEH + LÖ | BEH: Tipp auf Overlay lässt Chip bei „Kategorie“, zwei Kategorien filtern („Kategorie: 2“, Stadtwerke fehlt, „3 Buchungen“), gehört in `[flow:filter]`; LÖ: `.shadow-2xl` count 0 (Sheet statt Popover) und `compareDocumentPosition` (aktiver Chip vor „Zu prüfen“). | keiner erkennbar (seit t5m2b-Konsolidierung 86ea682) | gering |
| ledger-filter-bar.test.mjs:301 | Leiste haftet unter Navbar, Tabellenkopf direkt darunter, mit Spiegel-Lauf | gemischt: INV + LÖ | INV: `theadTop >= barBottom - 1` (Sticky-Kopf überdeckt Tabellenkopf nicht), auch bei Umbruch und Spiegel-Hinweis; LÖ: `position === "sticky"`, `top === 64`, Abstand `<= 2` px. | Regression laut Kommentar Z. 257 (feste Höhenschätzung, Observer maß Spiegel-Hinweis); kein bug-Commit gefunden | mittel: wartet auf Seiten-Animationen und zwei rAF, misst nach `scrollTo` |
| ledger-mirror-buttons.test.mjs:71 | genau ein sichtbarer Spiegel-Knopf, Handy Symbol, PC Text | gemischt: INV (Kandidat) + LÖ | `visible.length === 1` lässt sich als „kein Knopf doppelt sichtbar“ verallgemeinern; `title`/leerer Text am Handy und Textknopf am PC (Symbol statt Text) sind LÖ. | Regression design-system-qz7b6 (0d8e6aa `bug(design-system)`) | gering |
| ledger-detail.test.mjs:123 | Handy: Tipp öffnet Sheet mit Filter-Knöpfen an den Feldern | gemischt: BEH + LÖ | BEH: Tipp auf Filter-Knopf lädt mit `Payee === "Deutsche Bahn"` neu, Sheet bleibt offen, `aria-label` der Knöpfe (gehört in `[flow:detail]`); LÖ: kein `row-filter-actions`-Block, Knopf sitzt in `detail-payee` bzw. `detail-category`. | Feature ledger-q1hw2 (2d4f5c0) | gering |
| ledger-lena-batch.test.mjs:199 | Handy: dritte Reihe mit beiden Aktionen, Grund als Zeile, nichts läuft über | gemischt: INV + LÖ + BEH | INV: Knöpfe in `0..390`, `scrollWidth <= clientWidth`, Abstandshalter `>= barHeight` (Leiste verdeckt keine Zeile); LÖ: „Reihe 3 unter Reihe 2“, beide auf gleicher `y`; BEH: Grundtext und `ask.isDisabled()` ohne Farbe (eher MVU). | Feature ledger-y54gr (96659fe) | mittel: synthetisches langes Drücken per `dispatchEvent` |
| ledger-sort-chip.test.mjs:80 | Handy: Chip am Ende, Sheet mit zehn Optionen, Betrag aufsteigend | gemischt: BEH + LÖ | BEH: Wahl sendet `{ Field: "SortByAmount", Direction: "SortAsc" }`, Chip zeigt „Betrag ↑“, zehn Optionen, eine gewählt (gehört in `[flow:sortieren]`); LÖ: letzter Knopf der Leiste ist der Chip, Datum in `div.h-5.mt-0\.5` (Klassen-Selektor), keine Tabelle. | Feature ledger-skpcx (c8a0404) | gering, aber Klassen-Selektor bricht bei jedem Redesign |
| ledger-table-columns.test.mjs:103 | Gesamtsicht und Konto: kein Spaltenkopf überlappt, keine Spalte unter Mindestbreite | gemischt: INV + LÖ | INV: `scrollWidth <= clientWidth` je `th` über 800/1024/1280/1440; LÖ: `MIN_WIDTH` (Payee 140, Status 128 usw.) als feste px-Kopie aus der View. | Bug ledger-qh9qx (38f2118 `bug(ledger)`) | gering: wartet auf zwei gleiche Messungen; `console.log` erzeugt Rauschen |
| ledger-selection.test.mjs:405 | PC: Kategorie setzen öffnet Popover ganz im Bild, Esc schließt nur Picker | gemischt: INV + BEH | INV: `popoverRect` in `0..1440`, `top >= 0`, `bottom <= barRect.top + 1` (überlappt Leiste nicht); BEH: keine `aria-selected`-Zeile, Esc schließt nur den Picker, Auswahl bleibt bei 3 (gehört in `[tastatur]`). | Feature ledger-w29y6 (d8e4f14), kein Bug | gering |
| ledger-split-editor.test.mjs:208 | Handy: Aufteilen im selben Sheet, Speichern klebt unten, Auswahl als zweites Sheet | gemischt: INV + LÖ + BEH | INV: `split-action` und `memo-action` `>= 44` px, `footerBottom <= viewport + 1` (Primärknopf erreichbar); LÖ: Überschrift „Aufteilen“, Unterzeile „Payee · Datum“, kein Abbrechen, nur Speichern, `footerBottom >= viewport - 40`, `.layer-2`; BEH: Speichern gesperrt bis Rest stimmt, Kontoziel „→ Bargeld“, `saveCalls.length === 1` (gehört in `[flow:split]`). | Feature ledger-pafkp (9b2bf8b) | mittel: `tapUntil`-Schleifen gegen geschluckte Tipps |
| sync-start.test.mjs:92 | Zeile zum Bankstand nimmt beim Laden schon ihre Höhe ein | INV (Kandidat) | `start.boundingBox().y` bleibt vor und nach der Antwort gleich: „kein Layout-Sprung beim Nachladen“ gilt für jedes Design. | fix-Commit 31f15e0 (Review-Findings banking-import-v8n3k u. a.), kein `bug(...)` | mittel: `Promise.all(getAnimations().map(a => a.finished))` filtert endlose Animationen nicht und hängt bei einem Spinner bis zum Timeout |
| rules-page-mobile.test.mjs:154 | Handy: Prioritätspfeile ≥ 48 × 48, Tipp verschiebt, öffnet Dialog nicht | gemischt: INV + BEH | INV: `box.width >= 48 && box.height >= 48`; BEH: gesperrter Pfeil öffnet keinen Dialog, `reorderRules` mit neuer Reihenfolge, kein Dialog (gehört in `[flow:regeln]`). | Bug categorization-cgq35 (c9804da `bug(categorization)`) | gering (`click({ force: true })`) |
| rules-page-mobile.test.mjs:184 | Handy: Overflow-Knopf ≥ 48, Menü im Sheet, Export trifft Zeile nicht | gemischt: INV + BEH | INV: Knopf `>= 48 × 48`, jeder Eintrag `height >= 48`; BEH: `aria-label`, Menütexte, kein Autofokus bei Touch, Esc schließt, kein Ghost-Click nach Export (`dialog` count 0). | Bugs categorization-cgq35 (c9804da) und categorization-f955d (Kommentar Z. 205) | gering: beide Klicks im selben Tick; Negativ-Assertion nach zwei rAF |
| rules-page-mobile.test.mjs:350 | PC: Regel-Dialog bleibt Modal, Picker als Popover im Bild, Ändern speichert | gemischt: LÖ + INV + BEH | LÖ: kein Sheet am PC (Modal statt Sheet); INV: Hinweis endet über dem Dialogfuß (Popover nicht abgeschnitten); BEH: 12 Optionen, Speichern sendet `CategoryId` mit guid(3) (gehört in `[flow:regeln]`). | Bug categorization-cgq35 (c9804da) | gering |
| rules-page-mobile.test.mjs:428 | PC: Auslöser schließt offenes Kategorie-Popover im Dialog | BEH | Einzige Assertion: nach Klick auf den Auslöser bei offenem Popover ist der Hinweis weg (`hint.count() === 0`); reines Verhalten ohne Layoutbezug, gehört in `[flow:regeln]`. | Bug categorization-f955d (f271f35 `bug(categorization)`) | mittel: Negativ-Assertion nach nur zwei rAF kann falsch grün sein |
| move-money.test.mjs:198 | Panel 1280 × 800: Auslöser schließt Popover, ganz sichtbar, nicht abgeschnitten | gemischt: LÖ + INV + BEH | LÖ: kein Sheet am PC; INV: Popover-Box liegt waagerecht und senkrecht im Panel, Fuß per `elementFromPoint` nicht verdeckt; BEH: Klick auf Auslöser schließt (`hint.count() === 0`). | Bug budgeting-nfrds (7cb8540 `bug(budgeting)`, Kommentar Z. 222) | mittel: Negativ-Assertion nach zwei rAF; Popover über Klassenkette `.rounded-2xl.bg-surface-elevated.shadow-2xl` |
| ledger-status-column.test.mjs:127 | Konten-Spalte haftet unter Navbar, scrollt in sich (AC1, AC2) | gemischt: LÖ + INV | LÖ: `stripTopAfter` bei 64 px nach Seitenscroll, Spalte bewegt sich beim Eigen-Scroll nicht; INV: späte Konten sind über `scrollTop` erreichbar (Inhalt nicht abgeschnitten). | Bug ledger-qns4j (91a7a2e `bug(ledger)`), AC1/AC2 | gering: `behavior: "instant"` |
| ledger-status-column.test.mjs:293 | Zwei Kennzeichen in Statuszelle: kein Umbruch, kein Überlauf (AC5) | gemischt: INV + LÖ + BEH | INV: `scrollWidth <= clientWidth + 1` und `scrollHeight <= clientHeight + 1` der Zelle (nichts abgeschnitten); LÖ: Zeilenhöhe gleich Vergleichszeile (±1 px); BEH: `aria-label` mit voller Bedeutung (eher MVU). | Bug ledger-qns4j (AC5), Flake-Fix ledger-wj6pd (f1d8681 `bug(ledger)`) | mittel: hatte sporadisch 0 px, jetzt atomar gemessen |
| ledger-status-column.test.mjs:324 | Spaltenraster: Kästchen 44, Status 128, Memo Rest (AC6) | LÖ | `boxWidth ≈ 44`, `datumWidth ≈ 96`, `betragWidth ≈ 132`, `statusWidth ≈ 128`, Kopfreihenfolge, Memo füllt den Rest: alles feste Designwerte; die Mindestbreiten-Prüfung doppelt `ledger-table-columns.test.mjs:103`. | ledger-n4vte, ledger-r1d2f (5e8b12a), ledger-qh9qx (38f2118 `bug(ledger)`), AC6 | hoch: Kommentar Z. 325 bis 345 beschreibt 30 bis 40 % Fehlläufe mit 0 px, Fix in Iteration 3 |
| touch-target-small-button.test.mjs:91 | Handy: Zeile hinzufügen und Barabhebung ≥ 48, PC < 44 | gemischt: INV + LÖ | INV: Handy `height >= 47.5`; LÖ: PC `height < 44` legt eine Desktop-Designgröße fest. | Bug design-system-j6dnj (897b13c `bug(design-system)`) | gering |

#### Zahlen

- Rein LÖSCHEN: 2 (review-button-visibility:73, status-column:324)
- Rein INVARIANTE: 1 (sync-start:92, als Kandidat außerhalb der vorgegebenen Liste)
- Rein BEHALTEN: 1 (rules-page-mobile:428)
- Gemischt: 20
- Tests mit mindestens einer Assertion je Kategorie: INV 17 (plus Fokus-Kandidat in review-button-visibility:73), LÖ 18, BEH 15
- Mit Bugbezug (`bug(...)`-Commit oder Regressionskommentar): 14

#### Invarianten in diesen Dateien und vorhandene Hilfsfunktionen

Es gibt keine gemeinsame Hilfsdatei für Invarianten. Die Messungen stehen meist inline; geteilte Module (`screenshot-route.mjs`, `ledger-filter-bar.mjs`, `ledger-status-column.mjs`) liefern nur Fixtures, Server-Start und `pollUntil`.

- **Kein seitliches Scrollen**: nur inline `document.documentElement.scrollWidth <= clientWidth` in ledger-lena-batch.test.mjs:232. Keine Hilfsfunktion.
- **Trefferfläche ≥ 44/48 px am Handy**: `measure(page, scopeSelector)` in touch-target-small-button.test.mjs:79 (sucht Knöpfe nach Text); sonst inline `boundingBox()` in ledger-external-links.test.mjs:110, ledger-split-editor.test.mjs:217, rules-page-mobile.test.mjs:159, 188 und 195. Uneinheitlich: 44 px in external-links und split-editor, 48 px in rules und touch-target.
- **Input-Schrift ≥ 16 px am Handy**: lokale `measure` (baut Testfelder mit den Size-Klassen) in ledger-filter-bar.test.mjs:163 und `size` in ledger-filter-bar.test.mjs:183.
- **Popover/Sheet ganz im Viewport bzw. im Container**: `rect(locator)` in ledger-selection.test.mjs:400; inline Box-Vergleich in move-money.test.mjs:211 bis 215 und ledger-lena-batch.test.mjs:226; Dialogfuß-Vergleich inline in rules-page-mobile.test.mjs:363 bis 365.
- **Nichts verdeckt (oberstes Element per `elementFromPoint`)**: inline in ledger-category-change.test.mjs:257 und move-money.test.mjs:216. Zweimal derselbe Code, keine Hilfsfunktion.
- **Sticky-Kopf verdeckt keinen Inhalt**: `scrollIntoStickyState(target)` in ledger-filter-bar.test.mjs:268 (wartet auf endliche Animationen und zwei rAF) und `assertStickyWithoutCollision(target, label, expectedTop)` in ledger-filter-bar.test.mjs:285 (mischt INV `theadTop >= barBottom - 1` mit festem `expectedTop` 64); Abstandshalter-Prüfung für die Auswahl-Leiste inline in ledger-lena-batch.test.mjs:237.
- **Spaltenköpfe überlappen nicht**: `measure(width)` in ledger-table-columns.test.mjs:64 (wartet auf stabile Breiten) und `assertNoOverlap(label, result)` in ledger-table-columns.test.mjs:95 (mischt `scrollWidth <= clientWidth` mit `MIN_WIDTH`); `waitForStableHeaderWidths(page)` in ledger-status-column.test.mjs:54.
- **Zelleninhalt läuft nicht über**: inline `scrollWidth/scrollHeight` in ledger-status-column.test.mjs:305; `measureRowHeights` in ledger-status-column.test.mjs:257 (atomare Zeilenhöhe).
- **Inhalt in scrollbarem Container erreichbar**: inline in ledger-status-column.test.mjs:164 bis 213.
- **Primärknopf erreichbar (Fuß im Viewport)**: inline in ledger-split-editor.test.mjs:229 bis 234. Eine Prüfung bei offener Bildschirmtastatur gibt es in diesen Dateien nicht.
- **Kandidaten außerhalb der vorgegebenen Liste**: kein Layout-Sprung beim Nachladen (inline sync-start.test.mjs:107 bis 111; der Animations-Warter dort filtert endlose Animationen nicht, anders als `scrollIntoStickyState`), kein Knopf doppelt sichtbar (`visibleMirrorButtons(page)` in ledger-mirror-buttons.test.mjs:59), fokussiertes Element sichtbar (`opacityReaches(payee, value)` in ledger-review-button-visibility.test.mjs:62).

