---
id: design-system-q2bmv
title: "screenshot-route.mjs: die Aufnahme so testen, dass der BottomSheet-Fehler auch wirklich auffiele"
status: done
type: chore
context: design-system
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [test-infra, visuelles-gate, screenshot, playwright]
related_adrs: [0005, 0034]
related_research: []
prior_art: [ledger-p9k3w]
---

## Why

Lücke 5 aus dem QA-Review nach dem Work-Lauf vom 2026-09-19 (erfasst als
`ledger-r7dm2`). Das ist der Befund, der diesem ganzen Zerlegungsvorgang seine Richtung
gibt.

`ledger-p9k3w` hat einen echten Fehler behoben: `captureScreenshot` nahm mit
`fullPage: true` auf, Chromium vergrößert dafür intern das Layout-Viewport auf die volle
Scroll-Höhe, `100dvh` wird dabei neu berechnet, und die eigentlich inaktive
`.bottom-sheet`-Leiste (`src/Client/styles.css:1556`, positioniert über
`top: calc(var(--vv-top, 0px) + var(--vvh, 100dvh))`, ADR 0005) rutschte ins Bild.

Die beiden Chromium-Tests in `scripts/screenshot-route.test.mjs` bilden aber nur den
Rumpf von `captureScreenshot` gegen eine synthetische Seite aus schwarzen Divs nach und
messen PNG-Maße. Keiner enthält eine `.bottom-sheet`, ein `100dvh` oder eine Aussage
darüber, was im Bild zu sehen ist. Sie belegen, dass Playwright sich so verhält wie
angenommen, nicht dass das Skript das Richtige tut. Kehrte `captureScreenshot` morgen zu
`fullPage: true` zurück, blieben beide grün; rot würde allein die
Quelltext-Zusicherung.

Das Skript gehört design-system (gebaut von `design-system-wa6wz`, erweitert von
`design-system-m4kp7`, im design-system-README als Werkzeug des visuellen Gates
geführt). `ledger-p9k3w` saß in `ledger`, weil der Fehler beim Kassenbuch-Screenshot
auffiel; dieser Task fasst nicht die Kassenbuch-Seite an, sondern die Aufnahme-Funktion
des Werkzeugs.

## What

Die Aufnahme-Mechanik aus `captureScreenshot` als eigene, exportierte Funktion
herausziehen, etwa:

    export async function captureAtDocumentHeight(page, baseViewport, outPath)

Sie misst `document.documentElement.scrollHeight`, rechnet über das bestehende
`computeCaptureViewport`, setzt den Viewport und nimmt mit `fullPage: false` auf.
`captureScreenshot` behält `goto` plus `waitForPageLoaded` und ruft danach diese
Funktion. Damit prüfen die Browser-Tests die echte Funktion statt ihren Nachbau.

Dazu die Tests umbauen: die zwei bestehenden Chromium-Tests rufen
`captureAtDocumentHeight`, statt die Mechanik ein zweites Mal hinzuschreiben, und es
kommt ein Testpaar dazu, das den Fehler synthetisch nachstellt.

Nebenher, ohne neue Abdeckung: `import { chromium } from "playwright"` steht am
Modulkopf, sodass eine Maschine ohne Browser-Binaries alle 29 Tests verliert statt nur
der browsergebundenen. Den Import in die Tests hineinziehen, die ihn brauchen
(`const { chromium } = await import("playwright")`).

## Acceptance criteria

- [ ] **Die Gegenprobe zeigt, dass der Fehler wirklich nachgestellt ist.** Ein Test in
      `scripts/screenshot-route.test.mjs` baut per `page.setContent` eine Seite mit
      einem Inhalts-Div von 2200px Höhe und einem zweiten Element mit
      `position: fixed; top: calc(var(--vv-top, 0px) + var(--vvh, 100dvh)); left:0; right:0; height:80px; background: rgb(255,0,0)`,
      also derselben Positionierungsregel wie `.bottom-sheet` in
      `src/Client/styles.css:1556`. Bei einem Basis-Viewport von 390x600 erwartet der
      Test, dass eine Aufnahme mit `page.screenshot({ fullPage: true })` mindestens ein
      Pixel `rgb(255,0,0)` enthält. Ohne diese Zusicherung wäre das nächste Kriterium
      nicht falsifizierend, weil es auch dann grün bliebe, wenn die Leiste aus einem
      ganz anderen Grund nie im Bild wäre.
- [ ] **Die echte Aufnahme-Funktion hält die Leiste draußen.** Derselbe Test ruft auf
      derselben Seite `captureAtDocumentHeight(page, { width: 390, height: 600 }, out)`
      aus `scripts/screenshot-route.mjs` (die Funktion, die `captureScreenshot`
      tatsächlich benutzt, kein Nachbau) und erwartet: das PNG enthält kein einziges
      Pixel `rgb(255,0,0)`, und seine Höhe ist gleich der vor der Aufnahme gemessenen
      `document.documentElement.scrollHeight`. Kehrt jemand in
      `captureAtDocumentHeight` zu `fullPage: true` zurück, wird dieser Test rot.
- [ ] **Die Pixelprüfung kommt ohne neue Abhängigkeit aus.** Das aufgenommene PNG wird
      innerhalb des bereits geöffneten Browsers ausgewertet (etwa als Data-URL auf ein
      `<canvas>` gezeichnet und per `getImageData` gelesen), nicht über ein neu
      hinzugefügtes Paket. `package.json` gewinnt keine Abhängigkeit. Dasselbe Motiv wie
      bei `pngDimensions` im selben File.
- [ ] **Kurze Seiten behalten ihre Maße, geprüft an der echten Funktion.** Der
      bestehende AC3-Fall (Inhalt 100px, Basis-Viewport 1440x900) ruft ab jetzt
      `captureAtDocumentHeight` statt die Mechanik nachzubauen und erwartet unverändert
      ein PNG von 1440x900.
- [ ] **Lange Seiten wachsen auf die Inhaltshöhe, geprüft an der echten Funktion.** Der
      bestehende AC2-Fall ruft ebenfalls `captureAtDocumentHeight` und erwartet ein PNG,
      dessen Höhe der gemessenen Dokumenthöhe entspricht und über die
      Basis-Viewport-Höhe hinausgeht.
- [ ] **Kein Test baut den Rumpf der Aufnahme mehr nach.** In
      `scripts/screenshot-route.test.mjs` kommt `page.setViewportSize(` nur noch in
      Testaufbauten vor, die den Basis-Viewport setzen, nicht mehr als Nachbildung des
      Aufnahme-Ablaufs; die Folge `computeCaptureViewport` gefolgt von
      `setViewportSize` gefolgt von `screenshot` steht ausschließlich in
      `scripts/screenshot-route.mjs`.
- [ ] **Die Quelltext-Zusicherung zeigt auf die neue Naht.** Der bestehende Test
      „captureScreenshot verwendet computeCaptureViewport und nimmt ohne fullPage auf"
      wird darauf umgestellt, dass `captureScreenshot` `captureAtDocumentHeight` ruft
      und selbst kein `fullPage` mehr setzt; die Zusicherung gegen `fullPage: true` gilt
      dann für den Rumpf von `captureAtDocumentHeight`.
- [ ] **Playwright wird erst geladen, wenn ein Browser gebraucht wird.**
      `scripts/screenshot-route.test.mjs` enthält kein
      `import { chromium } from "playwright"` auf Modulebene; die browsergebundenen
      Tests laden es per `await import("playwright")`. Auf einer Maschine ohne
      installierte Browser-Binaries schlagen genau diese Tests fehl, alle übrigen laufen
      durch.
- [ ] `node --test scripts/screenshot-route.test.mjs` grün.

## Notes

**Herkunft der Testdefinitionen.** Der Elterntask `ledger-r7dm2` behauptet, der QA-Review
liege als Volltext im Diary-Eintrag vom 2026-09-19 20:14 vor und nenne je Test Datei,
Eingabe und erwartetes Ergebnis. Das stimmt nicht; die Kriterien oben sind aus
`scripts/screenshot-route.mjs` und `src/Client/styles.css` abgeleitet.

Betroffen, aber nicht besitzend: `infrastructure`. Zwei Tests in derselben Datei setzen
ADR 0034 durch (`BACKEND_PORT` in `screenshot-route.mjs` und `vite.config.js`). Diese
zwei Tests bleiben unverändert; ein späterer Umbau des Skripts darf sie nicht still
fallen lassen.

**Wenn sich die Gegenprobe nicht rot bekommen lässt** (also `fullPage: true` auf der
synthetischen Seite kein rotes Pixel liefert), ist der Fehler mit diesem Aufbau nicht
nachgestellt. Dann gehört der Task mit einer Notiz zurück in den Backlog, statt einen
grünen Test auszuliefern, der nichts belegt. Genau dieser Fehler wird hier ja gerade
behoben.

## Outcome

`captureScreenshot` in `scripts/screenshot-route.mjs` bildete die eigentliche Aufnahme-Mechanik
(Dokumenthöhe messen, `computeCaptureViewport`, `setViewportSize`, `screenshot` ohne `fullPage`)
bisher nur inline ab. Die zwei Chromium-Tests in `scripts/screenshot-route.test.mjs` bauten diese
Mechanik ein zweites Mal nach, statt die echte Funktion zu prüfen: keiner enthielt eine
`.bottom-sheet`, ein `100dvh` oder eine Aussage darüber, was im Bild zu sehen ist. Kehrte jemand
zu `fullPage: true` zurück, wären beide grün geblieben.

Umgesetzt: die Mechanik ist jetzt die eigene, exportierte Funktion
`captureAtDocumentHeight(page, baseViewport, outPath)`; `captureScreenshot` behält `goto` plus
`waitForPageLoaded` und ruft danach diese Funktion, statt selbst `fullPage` zu setzen.

Die Gegenprobe wurde vor jeder Test-Änderung tatsächlich ausgeführt: eine synthetische Seite
(Basis-Viewport 390x600, ein 2200px hohes Inhalts-Div, ein zweites Element mit derselben
Positionierungsregel wie `.bottom-sheet` in `src/Client/styles.css:1556`,
`top: calc(var(--vv-top, 0px) + var(--vvh, 100dvh))`, `background: rgb(255,0,0)`).
`page.screenshot({ fullPage: true })` lieferte 31200 rote Pixel (390 x 80, die Leiste
vollständig sichtbar). Der Fehler ist mit diesem Aufbau also wirklich nachgestellt; andernfalls
wäre die Aufgabe laut ihren eigenen Notes zurück in den Backlog gegangen.

Genau diese Seite steckt jetzt im neuen Test: er hält zuerst per `fullPage: true` die Gegenprobe
fest (mindestens ein rotes Pixel, sonst schlägt der Test selbst fehl), ruft dann die echte
`captureAtDocumentHeight` auf derselben Seite auf und erwartet null rote Pixel sowie eine
Bildhöhe gleich der gemessenen `document.documentElement.scrollHeight`. Die Pixelprüfung
(`countRedPixels`) liest das PNG als Data-URL auf ein `<canvas>` im bereits geöffneten Browser
und zählt per `getImageData`, ohne neue Abhängigkeit, im selben Motiv wie das vorhandene
`pngDimensions`.

Die beiden bestehenden AC2/AC3-Browser-Tests rufen jetzt ebenfalls `captureAtDocumentHeight`;
ein echter `page.setViewportSize(`-Aufruf kommt in der Testdatei gar nicht mehr vor. Der
Quelltext-Regressionstest gegen `fullPage: true` zielt jetzt auf den Rumpf von
`captureAtDocumentHeight`; ein neuer, separater Test stellt sicher, dass `captureScreenshot`
diese Funktion ruft und selbst kein `fullPage` mehr setzt. Der statische
`import { chromium } from "playwright"` ist aus dem Modulkopf der Testdatei verschwunden; die
drei browsergebundenen Tests laden Playwright per `await import("playwright")`, abgesichert durch
einen eigenen Quelltext-Test.

Die zwei ADR-0034-Tests (`BACKEND_PORT` in `screenshot-route.mjs` und `vite.config.js`) sind
unberührt und grün. `node --test scripts/screenshot-route.test.mjs`: 32/32 grün (29 vorher,
3 neu). `dotnet test`: 940 bestanden, 7 übersprungen, 0 fehlgeschlagen.

**Verifikation (Iteration 1, PASS).** Der Verifier hat nicht nur die Gegenprobe nachgemessen
(31200 rote Pixel in den Bildzeilen y=600..679 eines 2200px hohen Bilds), sondern eine
Kontrollprobe gefahren, die die eigentliche Frage beantwortet: wird der Test aus dem *richtigen*
Grund rot? Mit identischer Fixture, aber hart `top: 600px` statt `100dvh`, liefert auch
`captureAtDocumentHeight` 31200 rote Pixel. Die Null-Zusicherung hängt damit nachweislich an der
`100dvh`-Neuberechnung aus ADR 0005 und nicht an einer unbeteiligten Eigenschaft der Fixture. Vor
der Aufnahme steht die Leiste bei `top: 600px`, nach der Viewport-Vergrößerung rechnet `dvh` auf
2200 um (`getComputedStyle(...).top === "2200px"`) und sie verlässt das Bild. Zusätzlich hat er
die Slice-Grenzen beider Quelltext-Tests von Hand nachgerechnet (kein Überlaufen in die
Nachbarfunktion, beide Anker genau einmal an Spalte 0), die Testarithmetik geprüft (3 entfernte
gegen 6 neue `test(`-Deklarationen, jede entfernte mit umbenanntem Gegenstück, kein stiller
Verlust) und AC7 empirisch nachgestellt, indem er `PLAYWRIGHT_BROWSERS_PATH` auf ein leeres
Verzeichnis zeigen ließ: genau die drei browsergebundenen Tests scheitern, die übrigen 29 laufen
durch.

**Nicht blockierender Restbefund des Verifiers, außerhalb des Zuschnitts dieser Aufgabe:** fehlt
nicht nur die Browser-Binary, sondern das Paket `playwright` selbst, fällt weiterhin die ganze
Testdatei aus (0 von 32), weil `scripts/screenshot-route.mjs:67` seinen
`import { chromium } from "playwright"` unverändert auf Modulebene hält und die Testdatei dieses
Modul zwangsläufig importiert. Der Task verlangt an dieser Stelle ausdrücklich nichts anderes
("Nebenher, ohne neue Abdeckung"), ein Eingriff wäre Scope-Ausweitung gewesen.
