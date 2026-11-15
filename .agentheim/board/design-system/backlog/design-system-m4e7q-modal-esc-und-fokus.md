---
id: design-system-m4e7q
title: "Modal: Esc schließt den Dialog auch ohne Fokus darin, der Fokus kehrt zum Auslöser zurück"
status: backlog
type: bug
context: design-system
created: 2026-11-15
completed:
depends_on: []
blocks: []
tags: [frontend, tastatur, dialog]
related_adrs: [0087]
related_research: []
prior_art: [design-system-gcpws]
---

## Why
`Modal.fs` hat ein Feld `CloseOnEscape`, wertet es aber nirgends aus. Öffnet man „Targets anwenden“ mit der Maus, steht der Fokus nicht im Dialog, und Esc schließt ihn nicht. Styleguide §7.8 Punkt 6 verlangt: Esc schließt den obersten Dialog, der Fokus geht auf den Auslöser. Gefunden vom Tasten-Sweep in design-system-gcpws; dort prüft die Ebene „Targets anwenden“ deshalb nur, dass die Liste darunter ruht (`escape: false` in `scripts/key-invariants.mjs`).

## What
`Modal.view` schließt bei offenem Dialog und `CloseOnEscape` auf Esc am Dokument, wenn kein innerer Baustein die Taste genommen hat (`defaultPrevented`), und setzt den Fokus danach auf das Element, das ihn vor dem Öffnen hatte. Die Reihenfolge mit anderen Dokument-Handlern (Kassenbuch `KeyboardNavigation`, `CaptureDialogEscape`) darf keine Stufe doppelt schließen.

## Acceptance criteria
- [ ] GIVEN „Targets anwenden“ ist per Mausklick offen, WHEN Esc, THEN der Dialog ist zu und der Fokus steht auf dem Auslöser (kaputt-Szenario, vor dem Fix rot im Tasten-Sweep, Ebene ohne `escape: false`).
- [ ] GIVEN im Dialog ist ein Picker offen, WHEN Esc, THEN schließt nur der Picker (intakt-Szenario).
- [ ] GIVEN „Neue Buchung“ als Dialog auf der Übersicht, WHEN Esc, THEN genau ein Schließen (kein doppeltes `CloseCapture`).
- [ ] `escape: false` an der Ebene „Targets anwenden“ in `scripts/key-invariants.mjs` entfällt.

## Notes
Kein eigener Tastentest je Dialog: die Ebene steht im Tasten-Sweep (`LISTS[budget].layers`).
