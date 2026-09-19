---
id: ledger-p9k3w
title: "screenshot-route.mjs zeigt die inaktive BottomSheet-Leiste bei Full-Page-Aufnahmen mit scrollbarem Inhalt"
status: done
type: bug
context: ledger
created: 2026-09-18
completed:
depends_on: [design-system-yxrxb]
blocks: []
tags: [test-infra, visuelles-gate, screenshot]
related_adrs: []
related_research: []
prior_art: [design-system-wa6wz]
---

## Why
`scripts/screenshot-route.mjs` (aus `design-system-wa6wz`) nimmt Screenshots per Playwright
mit `page.screenshot({ fullPage: true })` auf. Bei Routen, deren Inhalt höher als der
Viewport ist (etwa `#/ledger` am Handy-Viewport 390 × 844 mit mehreren Buchungen),
erscheint die `BottomSheet`-Leiste (`src/Client/DesignSystem/BottomSheet.fs`, Titel-Fallback
"Buchung") sichtbar am unteren Bildrand, obwohl kein Sheet offen ist und kein Ereignis eines
öffnet. Sie verdeckt dort einen Teil der letzten Zeile.

Das entwertet genau den Zweck des Werkzeugs: Es liefert Bilder fürs visuelle Gate, und ein
Artefakt, das wie ein offener Dialog aussieht, führt beim Abgleich mit dem Mockup in die
Irre.

## What
Ursache (vermutet, am Code hergeleitet, nicht abschliessend verifiziert): `.bottom-sheet`
positioniert sich im inaktiven Zustand über
`top: calc(var(--vv-top, 0px) + var(--vvh, 100dvh))` (`src/Client/styles.css:1556-1583`),
liegt also normalerweise knapp unterhalb des sichtbaren Bereichs. Ein
`fullPage: true`-Screenshot lässt Chromium die Seite intern auf ihre volle Scroll-Höhe
vergrössern; dabei wird `100dvh` gegen die neue, grössere effektive Höhe neu berechnet und
die Leiste rutscht in den aufgenommenen Bereich.

**Entschieden (Roman, 2026-09-19): Viewport auf die Inhaltshöhe setzen, `fullPage` aus.**
Das Skript misst die Höhe des Dokuments, setzt den Viewport darauf und nimmt ohne
`fullPage` auf. Damit entfällt Chromiums interne Vergrößerung, `100dvh` wird nicht neu
gerechnet, und die inaktive Leiste bleibt außerhalb des Bildes. Die App wird nicht
angefasst.

Die beiden verworfenen Wege: *die Leiste beim Screenshot ausblenden* würde ein Bild
erzeugen, das bewusst nicht den echten DOM-Zustand zeigt — für ein Werkzeug, dessen Zweck
der Abgleich mit dem Entwurf ist, die falsche Richtung. *Die CSS in der App ändern* fasst
die Positionierung am Visual Viewport an (ADR 0005) und riskiert am echten Gerät genau das
Verhalten, das dort funktioniert.

Betroffene Stelle: `captureScreenshot` in `scripts/screenshot-route.mjs` (Aufruf
`page.screenshot({ path: outPath, fullPage: true })`; der Kontext wird dort mit
`browser.newContext({ viewport })` erzeugt).

## Acceptance criteria
- [ ] Ein Screenshot einer Route mit einer Liste, die länger als der Handy-Viewport ist
      (etwa `#/ledger` mit mehreren Buchungen), zeigt am unteren Bildrand keine
      Sheet-Leiste mehr. [human-eye]
- [ ] Das Bild enthält weiterhin den **ganzen** Inhalt der Seite, nicht nur den ersten
      Viewport-Ausschnitt: die Bildhöhe entspricht der Inhaltshöhe. Maschinell belegt
      (Bildabmessungen gegen die gemessene Dokumenthöhe), nicht nur per Augenschein.
- [ ] Ein Screenshot einer Route ohne scrollbaren Inhalt (PC-Viewport derselben Seite)
      behält seine bisherigen Abmessungen.
- [ ] Der gewählte Weg ist im Skript kurz begründet, damit die nächste Person die Ursache
      nicht erneut herleiten muss.
- [ ] `node --test scripts/screenshot-route.test.mjs` ist grün.

## Notes
Gefunden bei `ledger-kdnnz`, Iteration 2 (2026-09-18), im Screenshot
`handy-390x844-ledger.png` gegen eine Wegwerf-Datenbank mit vier Buchungen. Beim
PC-Screenshot derselben Aufgabe trat es nicht auf, dort passte der Inhalt in den Viewport.
Der Verifier hat den Befund bestätigt und die Herleitung über `styles.css:1556-1583`
nachvollzogen.

Kein Blocker für `ledger-kdnnz` gewesen: die dort gelieferten Zeilen- und
Kennzeichen-Layouts waren auf beiden Bildern klar erkennbar und korrekt. Das Artefakt
betrifft die immer im DOM vorhandene BottomSheet-Komponente und ist von der konkreten Route
unabhängig, tritt also bei jeder ausreichend langen Seite auf.

**Fasst `scripts/screenshot-route.mjs` an**, und zwar dieselbe Funktion (`captureScreenshot`)
wie `design-system-yxrxb`. Deshalb `depends_on: [design-system-yxrxb]`: nacheinander, nicht
gleichzeitig. `infra-jh7g9` und `design-system-m4kp7` fassen dieselbe Datei an anderer
Stelle an (`startBackend`/`main`).

## Outcome

`captureScreenshot` in `scripts/screenshot-route.mjs` nahm bisher mit `page.screenshot({ fullPage: true })`
auf. Chromium vergrößert dafür intern den Viewport auf die volle Scroll-Höhe der Seite, BEVOR es den
Screenshot schneidet; dabei wird `100dvh` (verwendet von `.bottom-sheet` in `src/Client/styles.css:1556ff.`,
Komponente `src/Client/DesignSystem/BottomSheet.fs`) gegen die neue, größere effektive Höhe neu berechnet,
und die eigentlich inaktive Leiste (`top: calc(var(--vv-top, 0px) + var(--vvh, 100dvh))`) rutscht in den
aufgenommenen Bereich. Bei `#/ledger` mit mehreren Buchungen war das sichtbar; bei kürzeren Seiten trat es
nicht auf, weil der Inhalt schon in den Viewport passte.

Umgesetzt wie von Roman entschieden (s. Task-Notiz "Entschieden ... Viewport auf die Inhaltshöhe setzen,
`fullPage` aus"): eine neue reine Funktion `computeCaptureViewport(baseViewport, documentHeight)` berechnet
den Zielviewport — wächst auf die gemessene Dokumenthöhe, schrumpft aber nie unter die angeforderte Höhe.
`captureScreenshot` misst `document.documentElement.scrollHeight` nach `waitForPageLoaded`, setzt den
Viewport per `page.setViewportSize(...)` darauf und nimmt anschließend mit `fullPage: false` auf. Damit
entfällt Chromiums interne Vergrößerung und `100dvh` bleibt während der Aufnahme unverändert. Die Begründung
steht als Kommentar direkt bei `captureScreenshot` im Skript (AC4), inklusive der beiden verworfenen Wege
(Leiste künstlich ausblenden; App-CSS anfassen). Die App selbst (`BottomSheet.fs`, `styles.css`) wurde nicht
angefasst — der Fix bleibt vollständig im Skript.

`startBackend`, `assertDataDirIsolated`, `main()` und die `ROUTES_WITHOUT_BACKEND`/`routeNeedsBackend`-Logik
aus `design-system-m4kp7` sind unverändert; `/ledger` bleibt eine datenbedürftige Route und läuft weiterhin
gegen eine isolierte Temp-Kopie der Datenbank (ADR 0030 unberührt).

**Tests** (`scripts/screenshot-route.test.mjs`, 6 neu, 29/29 grün via `node --test`):
- 3 reine Unit-Tests für `computeCaptureViewport` (wächst bei längerem Inhalt / AC2, behält die Höhe bei
  kürzerem Inhalt / AC3, Grenzfall exakt passender Inhalt).
- 1 Quelldatei-Regressionstest (gleiches Muster wie der bestehende `data-app-ready`-Test): stellt sicher,
  dass `captureScreenshot` tatsächlich `computeCaptureViewport`, `setViewportSize` und `fullPage: false`
  verwendet, statt weiter unbedingt mit `fullPage: true` aufzunehmen.
- 2 Tests mit echtem Chromium (kein Mock) gegen eine lokal erzeugte, App-unabhängige Seite (kein Backend,
  keine Datenbankkopie): messen die tatsächliche PNG-Bildhöhe (Bytes 16-23 des IHDR-Chunks, keine neue
  Abhängigkeit) einer langen bzw. kurzen Seite gegen die gemessene Dokumenthöhe — genau die "maschinell
  belegt"-Vorgabe aus AC2, nicht nur eine Prüfung der reinen Rechenregel.
- Red/Green verifiziert: mit der alten `fullPage: true`-Fassung schlägt der Import von
  `computeCaptureViewport` fehl (Export existiert nicht); nach dem Fix sind alle 29 Tests grün.

**Manueller Lauf gegen die echte App (AC1, human-eye)**: `node scripts/screenshot-route.mjs '#/ledger'`
gegen eine isolierte Temp-Kopie von `~/my_apps/budgetbuddy-dev/budgetbuddy.db` (wie vom Skript selbst
vorgesehen, keine Berührung des Originals) ausgeführt. Das Handy-Bild (390×10036, AC2: Bildhöhe entspricht
der gemessenen Dokumenthöhe) zeigt am unteren Rand die normale Bottom-Navigation (Sync/Rules/Quick
Add/Kassenbuch/Settings) direkt unter der letzten Buchungszeile, keine überlagernde BottomSheet-Leiste mehr.
Temp-Verzeichnis und gestartete Prozesse wurden vom Skript selbst wieder aufgeräumt (cleanup()).

**Cross-BC-Hinweis (Rule 5)**: `BottomSheet.fs` gehört zur design-system-BC-Oberfläche, wurde aber NICHT
verändert — der Fix bleibt vollständig in `scripts/screenshot-route.mjs` (ledger-BC-Tooling-Task, betrifft
aber kein BC-spezifisches Verhalten). Kein Cross-BC-Effekt, daher kein README-Delta und keine ADR.

Geänderte Dateien:
- `scripts/screenshot-route.mjs` — `computeCaptureViewport` (neu, exportiert), `captureScreenshot`
  angepasst (Viewport-Messung + `setViewportSize` + `fullPage: false` statt `fullPage: true`).
- `scripts/screenshot-route.test.mjs` — 6 neue Tests wie oben beschrieben.
