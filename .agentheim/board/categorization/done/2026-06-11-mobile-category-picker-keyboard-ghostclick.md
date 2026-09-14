---
title: "Mobile Category Picker: keyboard-aware + ghost-click-frei"
status: done
bc: categorization
created: 2026-06-11
completed: 2026-06-11
captured: retroaktiv 2026-06-12 — Arbeit lief außerhalb von agentheim (Session feature/ux-wow)
related_adrs: [0005]
commits: [2501ce9]
branch: feature/ux-wow
---

# Mobile Category Picker: keyboard-aware + ghost-click-frei

## Problem
Der Kategorie-Picker — das Herzstück des Kategorisierens auf Mobile — war praktisch
unbenutzbar: das Bottom Sheet wurde von der On-Screen-Tastatur verdeckt, und Taps auf
Kategorien "fielen durch" auf die nächste Transaktion dahinter (Roman landete sofort im
falschen Kontext). Root Causes: `bottom: 0`-Anker ignoriert den Visual Viewport;
Auswahl-Commit auf `onPointerDown` lässt den nachgelagerten synthetischen Click auf das
Element hinter dem geschlossenen Sheet durchschlagen (Details: ADR 0005).

## Acceptance criteria
- [x] Picker bleibt bei geöffneter Tastatur vollständig sichtbar (iOS + Android)
- [x] Tap auf eine Kategorie wählt genau diese — nichts dahinter erhält den Click
- [x] Suchfeld bleibt beim Scrollen der Liste sichtbar (gepinnt), kein iOS-Auto-Zoom (≥16px)
- [x] Kein automatisches Tastatur-Öffnen auf Touch-Geräten (Chips zuerst nutzbar)
- [x] Expliziter Schließen-Button; Body scrollt nicht hinter dem offenen Sheet
- [x] Bestehender Flow (Vorgeschlagen / Zuletzt verwendet / Suche / Gruppen) unverändert

## Outcome
Neues `DesignSystem/Viewport.fs` (visualViewport→CSS-Vars, Body-Scroll-Lock,
Click-Swallow-Guard, Haptik-Helper); `BottomSheet.fs` auf Click-Commit-Pattern und
Visual-Viewport-Anker umgebaut; `interactive-widget=resizes-content` im Viewport-Meta.
Patterns als ADR 0005 verallgemeinert — generisches Sheet (Inline-Rule-Form) erbt sie.

## Verification
- dotnet build + npm run build (Fable) grün; Bestands-Testsuite grün
- Browser-Verhalten auf Android von Roman bestätigt: "Neue Kategorienauswahl ist gut."
