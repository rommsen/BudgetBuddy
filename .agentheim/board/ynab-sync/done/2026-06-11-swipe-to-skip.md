---
title: "Swipe-nach-links: Transaktion überspringen/einschließen"
status: done
bc: ynab-sync
created: 2026-06-11
completed: 2026-06-11
captured: retroaktiv 2026-06-12 — Arbeit lief außerhalb von agentheim (Session feature/ux-wow)
related_adrs: []
commits: [fd98128]
branch: feature/ux-wow
---

# Swipe-nach-links: Transaktion überspringen/einschließen

## Problem
Die Skip-Entscheidung ("geht diese Transaktion in den YNAB-Push oder nicht") war auf
Mobile nur über die kleine Checkbox oder den expandierten Action-Chip erreichbar —
für die häufigste Review-Geste zu viel Präzisionsarbeit am Daumen.

## Acceptance criteria
- [x] Swipe nach links auf einer Zeile überspringt sie (bzw. schließt geskippte wieder ein)
- [x] Vertikales Scrollen bleibt ungestört (Richtungs-Claim erst ab 12px horizontal)
- [x] Commit bei 35% Zeilenbreite oder Flick-Velocity; Rubberband + haptisches Tick
- [x] Abgeschlossener Swipe löst kein versehentliches Expand aus (Click-Suppression)
- [x] Geste ist Beschleuniger, nie einziger Pfad — Toggle + Action-Chips bleiben (WCAG 2.5.1)

## Outcome
Generische `DesignSystem/Swipe.fs` (Pointer Events, `touch-action: pan-y`,
setPointerCapture); `TransactionRow` in `SwipeableRow` gewrappt, Aktions-Label
("Überspringen" rot / "Einschließen" teal) hinter der Zeile. Kein Undo-Toast:
geskippte Zeilen bleiben sichtbar (ausgegraut), die Geste ist direkt umkehrbar.

## Verification
- dotnet build + Fable-Build grün; Gesten-Verhalten Code-Review-verifiziert
  (keine E2E-Infrastruktur), Gerätetest durch Roman
