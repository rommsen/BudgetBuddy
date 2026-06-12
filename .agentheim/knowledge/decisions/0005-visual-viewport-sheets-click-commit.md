---
id: 0005
title: Mobile Sheets ankern am Visual Viewport, Auswahl committet auf Click
scope: infrastructure
status: accepted
date: 2026-06-12
supersedes: []
superseded_by: []
related_tasks:
  - contexts/categorization/done/2026-06-11-mobile-category-picker-keyboard-ghostclick.md
  - contexts/infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs.md
related_research: []
---

# ADR 0005: Mobile Sheets ankern am Visual Viewport, Auswahl committet auf Click

## Context
Der Category Picker (Bottom Sheet) war auf Mobile praktisch unbenutzbar: von der
On-Screen-Tastatur verdeckt, und Taps "fielen durch" den Picker auf die Transaktion
dahinter. Zwei getrennte Root Causes:
1. **Tastatur:** iOS Safari (und Android Chrome 108+ per Default) verkleinern beim
   Tastatur-Öffnen nur den *Visual Viewport* — Layout-Viewport, `dvh` und
   `position: fixed; bottom: 0` bleiben unverändert, das Sheet klebt hinter der Tastatur.
2. **Ghost-Clicks:** Auswahl committete auf `onPointerDown`. Nach der Touch-Sequenz
   feuert der Browser synthetische `mousedown/mouseup/click`-Events per Hit-Test auf den
   *aktuellen* Koordinaten — ist das Sheet dann zu, trifft der Click das Element dahinter.

## Decision
Projektweite Patterns für alle Bottom Sheets / Overlays (DesignSystem):

1. **Visual-Viewport-Anker:** `Viewport.fs` spiegelt `visualViewport.height/offsetTop`
   live in die CSS-Variablen `--vvh`/`--vv-top`. Sheets ankern mit
   `top: calc(var(--vv-top) + var(--vvh))` + `translateY(-100%)` an der Unterkante des
   *sichtbaren* Bereichs (Fallback `100dvh` ohne JS). Zusätzlich
   `interactive-widget=resizes-content` im Viewport-Meta für Android.
2. **Click-Commit-Pattern:** Auswahl committet auf dem echten `click`, nie auf
   `pointerdown`/`touchend`. `onMouseDown → preventDefault()` auf Items hält den Fokus
   (und die Tastatur) stabil; beim Schließen wird ein einmaliger Click-Swallow-Guard
   (`swallowNextClick`) installiert.
3. **Flankierend verbindlich:** Body-Scroll-Lock (gezählt, iOS-fest via
   `position: fixed` auf body) solange ein Sheet offen ist; Suchfelder ≥16px
   (iOS-Auto-Zoom) und oberhalb der scrollenden Liste gepinnt; `touch-action:
   manipulation` auf Tappables; expliziter Close-Button (Grabber allein ist nicht
   barrierefrei); autoFocus nur bei feinem Pointer.
4. **Sheet-Stacking nur über die Layer-Klasse** (`.layer-2`, z-index 70/80) und nur
   eine Ebene tief (Picker über Quick-Add-Formular).

## Consequences
### Positive
- Picker, Quick Add und Inline-Rule-Form teilen dieselbe Mechanik — neue Sheets erben
  Tastatur-Festigkeit und Ghost-Click-Freiheit gratis.
- Kein Library-Zukauf; ~130 Zeilen Interop (`Viewport.fs`), Rest ist CSS.

### Negative
- Die Mechanik ist nur auf Code-Ebene verifizierbar (keine Browser-E2E-Infrastruktur
  im Repo) — Regressionen fallen erst beim manuellen Gerätetest auf.
- `--vvh`-Sync ist globaler Zustand; Komponenten außerhalb des DesignSystems müssen
  die Konvention kennen, statt sie aus Typen ableiten zu können.

### Neutral
- `prefers-reduced-motion` deckt die neuen Transitions über den bestehenden globalen
  Block ab; Spring-Easing (`linear()`) nur hinter `@supports`.

## Alternatives considered
- **`dvh`-Einheiten / `interactive-widget` allein** — abgelehnt: iOS ignoriert beides
  fürs Tastatur-Problem; nur die visualViewport-API ist dort verlässlich.
- **Commit auf `touchend` + `preventDefault`** — abgelehnt: Reacts synthetische
  Touch-Events sind dafür historisch unzuverlässig (passive Listener); Click-Commit
  ist das robustere Combobox-Standardpattern.
- **Delayed Unmount (~300ms) als alleiniger Schutz** — abgelehnt: behandelt nur das
  Symptom und macht das Schließen träge; als Guard (`swallowNextClick`) trotzdem
  zusätzlich vorhanden.

## References
- `src/Client/DesignSystem/Viewport.fs`, `BottomSheet.fs`, `src/Client/styles.css`
- `src/Client/index.html` (Viewport-Meta)
