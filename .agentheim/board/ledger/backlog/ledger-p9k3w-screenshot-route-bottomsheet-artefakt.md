---
id: ledger-p9k3w
title: "screenshot-route.mjs zeigt die inaktive BottomSheet-Leiste bei Full-Page-Aufnahmen mit scrollbarem Inhalt"
status: backlog
type: bug
context: ledger
created: 2026-09-18
completed:
depends_on: []
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

Mögliche Wege, beim Verfeinern zu entscheiden: `fullPage: false` mit definierter Zielhöhe,
ein Ausschluss beim Screenshot, oder eine CSS-Änderung, die die inaktive Leiste zuverlässig
ausserhalb des aufgenommenen Bereichs hält. Die dritte Variante ist die riskanteste, weil
sie die App selbst anfasst und die Positionierung am Visual Viewport hängt (ADR 0005).

## Acceptance criteria
- [ ] Ein Screenshot einer Route mit einer Liste, die länger als der Handy-Viewport ist
      (etwa `#/ledger` mit mehreren Buchungen), zeigt am unteren Bildrand keine
      Sheet-Leiste mehr.
- [ ] Der gewählte Weg ist im Skript oder im design-system-README kurz begründet, damit die
      nächste Person nicht dieselbe Ursache erneut herleiten muss.
- [ ] Die bestehenden Tests in `scripts/screenshot-route.test.mjs` bleiben grün.
- [ ] Ein Screenshot einer Route ohne scrollbaren Inhalt (etwa der PC-Viewport derselben
      Seite) sieht unverändert aus.

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
