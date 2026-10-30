---
id: categorization-cgq35
title: "Regelseite am Handy und per Tastatur"
status: done
type: bug
context: categorization
created: 2026-10-30
completed: 2026-10-30
depends_on: [design-system-001, categorization-msep5]
blocks: []
tags: [captured, bestandsreview]
related_adrs: [0005, 0043, 0053]
related_research: []
prior_art: []
---

## Why
Gruppe 2 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **hoch**. Trefferflächen unter 48 px, ein Menü, das nur über Fokus öffnet, ein zentriertes Modal am Handy und Texte, die dem Styleguide nicht folgen.

## What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Client/Components/Rules/View.fs:94` | standards/frontend/styleguide.md §4 (Touch-Targets mindestens 48 px) | hoch | Prioritätspfeile der Regelliste sind am Handy 24 mal 20 px groß und liegen neben dem Zeilen-Tap.
- `src/Client/Components/Rules/View.fs:569` | standards/frontend/styleguide.md §4 und §5 (Button, Icons) | mittel | Der Overflow-Knopf der Regelseite ist 32 px groß und hat keine Beschriftung.
- `src/Client/Components/Rules/View.fs:577 und :583` | standards/frontend/styleguide.md §5 (FilterPopover, BottomSheet, Button: jede klickbare Aktion), §7 | mittel | Das Menü der Regelseite öffnet nur über Fokus und seine Einträge sind nicht per Tastatur bedienbar.
- `src/Client/Components/Rules/View.fs:294` | standards/frontend/styleguide.md §5 (Modal nur Desktop, mobile Auswahl über BottomSheet) | mittel | Der Regel-Dialog läuft am Handy als zentriertes Modal mit Dropdown für die Kategorie.
- `src/Client/Components/Rules/View.fs:427` | standards/frontend/styleguide.md §8 (UI-Texte deutsch) | mittel | Englischer Platzhalter im Regel-Dialog.
- `src/Client/Components/Rules/View.fs:400` | styleguide §5 ErrorDisplay.warning | niedrig | selbst gebauter Warnkasten "Keine Kategorien geladen" durch `ErrorDisplay.warning` ersetzen.
- `src/Client/Components/Rules/View.fs:311` | styleguide §8 konsistente Begriffe | niedrig | "z.B." (311, 368 bis 370) zu "z. B.", "..." (393, 472, 648) zu "…".
- `src/Client/Components/Rules/View.fs:476` | styleguide §8 Buttons als Verb | niedrig | "Test" zu "Testen".
- `src/Client/Components/Rules/View.fs:648` | styleguide §6 Skeletons | niedrig | `NotAsked` und `Loading` zeigen dasselbe Skeleton.

## Acceptance criteria
Handy heißt 390 px Breite im Browser-Wächter (neu, `scripts/rules-page-mobile.test.mjs`, YNAB und `/api` gefälscht).

- [ ] Kaputt: GIVEN die Regelliste am Handy WHEN die Prioritätspfeile gemessen werden THEN waren sie 24 mal 20 px. Intakt: beide Pfeile sind mindestens 48 mal 48 px, ein Tipp verschiebt die Regel und öffnet den Dialog nicht. Der Test ist vor dem Fix rot.
- [ ] Kaputt: GIVEN die Regelseite WHEN der Overflow-Knopf per Tastatur erreicht wird THEN öffnet das Menü nur über Fokus und die Einträge reagieren nicht auf Enter. Intakt: Der Knopf ist ein `Button` mit Icon und `aria-label`, am Handy mindestens 48 px. Klick oder Enter öffnet das Menü, Esc und Klick außerhalb schließen es, jeder Eintrag (neu laden, exportieren, importieren) ist per Tab erreichbar und per Enter auslösbar.
- [ ] Das Menü liegt am PC in `FilterPopover` mit `menuItem`, am Handy in `BottomSheet`, sein Offen-Zustand im Model. Der Quelltext von `Components/Rules/View.fs` enthält kein `group-focus-within` mehr.
- [ ] Kaputt: GIVEN Handy-Breite WHEN der Regel-Dialog geöffnet wird THEN ist er ein zentriertes Modal mit Dropdown. Intakt: Handy `BottomSheet`, PC weiter `Modal`, gewählt über `Viewport.isDesktopViewport()`; die Kategorie kommt in beiden Hüllen aus `CategoryPicker` mit Suche. Anlegen und Ändern speichern dieselben Werte wie vorher.
- [ ] Die Texte folgen §8: "Override payee name in YNAB" ist deutsch, "z. B." statt "z.B.", "…" statt "...", Knopf "Testen". Ein Quelltext-Test sichert die vier Punkte.
- [ ] "Keine Kategorien geladen" läuft über `ErrorDisplay.warning`.
- [ ] `NotAsked` und `Loading` zeigen dasselbe Skeleton.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Captured via `quick-capture` on 2026-10-30. Refine am 2026-10-30: nicht geschnitten. Alle neun Befunde liegen in `Components/Rules/View.fs`, Teil-Tasks würden nur nacheinander in derselben Datei arbeiten.

- Pfeile: `moveButton` nutzt am Handy `TouchTargets.minSize`; ob die beiden neben- oder übereinander liegen, entscheidet der Worker nach dem Bild.
- Für den Overflow-Knopf gibt es in `Icons.fs` noch kein Icon, also dort ergänzen. Die Glyphe "⋮" fliegt raus.
- "Regeln importieren" ist heute ein `label` um ein verstecktes `input[file]` und per Tastatur nicht bedienbar. Es wird zu einem Menü-Eintrag, der das Input auslöst. Das Lesen der Datei in der View bleibt hier unangetastet, das verschiebt categorization-krscs (Befund kpsp8).
- Der Dialog im Sheet braucht `CategoryPicker.sheetLayered`, weil ein Sheet über dem Sheet liegt.
- categorization-msep5 läuft vorher (`depends_on`). categorization-krscs ändert denselben Client-Bereich: nicht im selben Batch laufen lassen, falls sie zugleich in todo liegt.

## Verifier note (iteration 1)
REASONS:
- `./verify.sh` endet mit `VERIFY: FAIL (rote Schritte: dotnet test, guard:classes)`. Die im Worker-Ergebnis genannte PASS-Zeile lässt sich nicht reproduzieren.
- Roter Test `CategoryPickerTests.fs:323` erwartet 3 `Content {`, `CategoryPicker.popoverInDialog` (`CategoryPicker.fs:731`) ergänzt eine vierte, fast wörtliche Kopie.
- Roter Wächter `guard:classes`: `{sizeClass}` in `Rules/View.fs:109`, weil `moveButton` die Größe in den Klassen-String interpoliert.
- ADR 0005 Punkt 3 und Styleguide §7.1.3: `rulesMenuItems` (`Rules/View.fs:632`) nutzt `menuItemAutoFocus` auch im BottomSheet, Autofokus gilt nur bei feinem Pointer.
- Nicht blockierend: Doc-Kommentar in `Icons.fs:288` steht an der falschen Funktion. Outcome-Aussage zum Kassenbuch-Sheet ist falsch, `menuItem` hat dort keinen Aufrufer. `popoverInDialog` und `viewEnd` fehlen in Styleguide §3.
SUGGESTED_FIX: `popoverInDialog` über einen gemeinsamen Helfer bauen, `moveButton` mit Klassen-Konstanten statt Interpolation, Autofokus nur bei `Viewport.isFinePointer()`, die Kleinigkeiten mitnehmen, `./verify.sh` erneut.
ITERATION_HINT: likely-fixable

## Verifier note (iteration 2)
REASONS:
- `./verify.sh` endet mit `VERIFY: FAIL (rote Schritte: test:scripts)`: `scripts/rules-page-mobile.test.mjs:175`, Zeile 223, Timeout beim Warten auf `filechooser`.
- Der Test ist reproduzierbar instabil: einzeln 8 von 8 grün, unter CPU-Last (2 mal ncpu Prozesse) 1 von 3 rot, insgesamt dreimal gesehen. Die Ursache ist nicht belegt. Spur Test: `keyboard.press` hat keine Wartezeit, das Abfangen des Dateidialogs ist evtl. noch nicht aktiv. Spur Produkt: `rulesMenuItems` schickt `CloseRulesMenu` vor `getElementById(importInputId).click()`, oder Tab und Enter landen auf dem falschen Element.
- Alles andere aus Iteration 1 ist behoben, der Merge mit `main` ist intakt.
SUGGESTED_FIX: Unter Last reproduzieren, `document.activeElement` vor Enter protokollieren, prüfen, ob der Klick auf das Input feuert, dann die Ursache beheben. Keine Timeouts erhöhen, keine Retries. `./verify.sh` mehrmals, einmal unter Last.
ITERATION_HINT: likely-fixable

## Outcome

Alle neun Befunde der Gruppe 2 sind behoben. `./verify.sh` endet mit Exit 0 und `VERIFY: PASS` (dotnet 2701, node 336, 9 Integrationstests opt-in übersprungen), Log der dritten Iteration: `verify-cgq35-iter3-worker.log` im Scratchpad der Sitzung. Die Zahlen der ersten Iteration stimmten nicht, der Verifier fand dort zwei rote Schritte.

- Prioritätspfeile: am Handy 48 mal 48 px, nebeneinander links in der Zeile (so bleibt die Liste kurz). Zwei weitere Fehler am selben Knopf sind mit behoben: ein Tipp auf den gesperrten Pfeil fiel durch `pointer-events-none` zur Zeile durch und öffnete den Dialog, und Enter auf einem Pfeil öffnete den Dialog statt zu verschieben. Die Größe kommt über `MoveButtonSize`, der Klassen-String wird nicht mehr interpoliert.
- Menü: `Button` mit neuem Icon `Icons.ellipsisVertical` und `aria-label`, Offen-Zustand in `Model.MenuOpen`. Am PC `FilterPopover`, am Handy `BottomSheet`, gewählt über `Viewport.isDesktopViewport()`. Beide Hüllen zeigen dieselben drei Einträge, am PC steht "Neu laden" damit zweimal auf der Seite (Knopf und Menü). Der erste Eintrag nimmt den Fokus nur bei feinem Pointer (Styleguide §7.1.3, ADR 0005). Esc greift auch im Sheet: dort bleibt der Fokus auf dem Knopf, das Ereignis erreicht den Wrapper des Menüs. Das Datei-Input steht außerhalb des Menüs, der Eintrag löst es aus; das Lesen der Datei bleibt in der View (categorization-krscs).
- Regel-Dialog: Handy `BottomSheet`, PC `Modal`, gemeinsamer Inhalt in `ruleFields`. Die Kategorie kommt aus `CategoryPicker` (`Form.PickerOpen`), am Handy per `sheetLayered`. Der Auslöser zeigt die Kategorie, ohne geladene Kategorien den gespeicherten Namen der Regel.
- Design System: `Button` bekommt `AriaLabel`; `FilterPopover.viewEnd` (rechtsbündig, das Menü ragte sonst aus dem Bild) und `FilterPopover.viewRevealed` mit `CategoryPicker.popoverInDialog` (das Popover rückt im Scrollbereich des Modals ins Bild). `popoverInDialog` und `popoverWithWidth` teilen ihren Inhalt über `popoverChildren`. `viewEnd` und `viewRevealed` stehen in `standards/frontend/styleguide.md` §3. `menuItem` ist unter `md` jetzt mindestens 48 px hoch; außerhalb der Regelseite ruft es nur die Styleguide-Route auf, im Kassenbuch-Sheet gibt es keinen Aufrufer.
- Texte, `ErrorDisplay.warning` und das gemeinsame Skeleton für `NotAsked` und `Loading` sind umgesetzt.

Tests: `scripts/rules-page-mobile.test.mjs` (7 Tests, 6 davon im Browser). Gegen einen Build des Clients vor dem Fix, ergänzt nur um `data-testid="rule-row"`, waren alle sechs Browser-Tests rot, der Pfeil-Test mit "24 mal 20". Der Quelltext-Test liest die aktuelle Datei und war dabei grün; die alte Fassung enthält 7 Zeilen mit den verbotenen Texten, von Hand per grep geprüft, der Test selbst lief nicht gegen sie. Der Handy-Test prüft jetzt auch, dass kein Eintrag beim Öffnen den Fokus hat; vor dem Autofokus-Fix war diese Prüfung rot. Dazu 6 `update`- und Label-Tests in `src/Tests/RulesListTests.fs`; der Quelltext-Ausschnitt dort zeigt jetzt auf `categoryField` statt `ruleEditModal`.

Instabiler PC-Test, Ursache belegt: der Fehler lag im Test, das Produkt ist in Ordnung. Im Test steckte ein Wettlauf zwischen `Page.setInterceptFileChooserDialog` und der Taste. `page.waitForEvent("filechooser")` schaltet das Abfangen ein, ohne auf Chromes Bestätigung zu warten, das `keyDown` folgte im selben Moment. Gemessen in einem roten Lauf unter Spinloop-Last (Protokollmitschnitt `pw:protocol`, Ereignislog im Browser): der Fokus lag auf "Regeln importieren", `input.click` lief mit `userActivation.isActive = true` und das Input war im Dokument, also greift `CloseRulesMenu` vor `click()` nicht ein. Die Bestätigung des Abfangens kam nach der des `keyDown`, `Page.fileChooserOpened` blieb aus. In allen 18 grünen Mitschnitten vor dem Fix kam sie vor der Bestätigung des `keyDown`, und `fileChooserOpened` traf ein.

Fix in `scripts/rules-page-mobile.test.mjs`: das Warten auf `filechooser` beginnt vor `menuButton.focus()`, die Schritte bis zum Menü liegen also zwischen Abfangen und Taste. Keine Timeouts geändert, keine Retries, kein Produktcode. Nach dem Fix unter derselben Last (20 Spinloops, je Runde `npm run test:scripts -- scripts/rules-page-mobile.test.mjs`): 8 von 8 Läufen grün, in allen 8 Mitschnitten kam die Bestätigung vor dem ersten `keyDown`-Ack. Vor dem Fix unter derselben Last 1 von 1 rot (Abbruch nach dem Treffer), beim Verifier 1 von 3. Unter Last mit `yes` reproduzierte der Fehler nicht (38 grüne Läufe vor dem Fix), diese Läufe zählen also nicht als Nachweis.

`page.waitForTimeout(400)` im Handy-Test des Regel-Dialogs bleibt: `Viewport.swallowNextClick` hinterlässt nur einen Listener auf `document` und keine Spur im DOM, es gibt keine Bedingung, auf die der Test warten könnte.

Aufräumen: 20 verwaiste Spinloops aus dem Lastlauf des Verifiers liefen noch und sind per PID beendet, diese Prozesse hatten alle Lastmessungen bis dahin zusätzlich belastet. Beim Abbruch eines eigenen Laufs traf ein breites `pkill` auf `headless_shell` und `test-concurrency=4` alle solchen Prozesse der Maschine; ein zeitgleicher Lauf eines anderen Workers wäre dabei mit abgebrochen worden. Am Ende läuft kein Lastprozess mehr, die Diagnosedateien sind entfernt.

Sichtprüfung am Handy: der Dialog passt, im Fuß bricht "Regel löschen" bei 390 px auf zwei Zeilen um. Der Screenshot der Liste fiel in die Einblend-Animation, das Bild der Pfeile ist also nur gemessen, nicht angesehen.
