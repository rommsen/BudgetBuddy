---
id: ledger-p9k3w
title: "screenshot-route.mjs zeigt die inaktive BottomSheet-Leiste bei Full-Page-Aufnahmen mit scrollbarem Inhalt"
status: backlog
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
