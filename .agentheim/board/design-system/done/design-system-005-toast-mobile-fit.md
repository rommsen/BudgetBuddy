---
id: design-system-005
title: Toast fügt sich auf Mobile nicht ins Layout — Platzierung, Insets, Hero-Kontrast
status: done
type: bug
context: design-system
created: 2026-06-18
completed: 2026-06-18
commit: 895e56e
depends_on: [design-system-001]
blocks: []
tags: [frontend, toast, mobile, layout, design-system]
related_adrs: [0007]
related_research: []
prior_art: []
---

## Why
Nach design-system-004 (18cf474, deployt) wirkt der Toast auf Mobile **immer noch fremd**
(Roman, Screenshots 2026-06-18). Der sanfte Abgang stimmt — aber die **Platzierung/Integration**
auf dem Gerät tut es nicht: der Toast sitzt nicht sauber im Layout. Toasts sind das primäre
Feedback-Signal des Flows; solange sie „aufgeklebt" wirken, bleibt der Eindruck billig.

## What
Die mobile Platzierung/Integration des Toasts richtig machen. Beobachtete Defekte aus den
Screenshots (Sync-Flow, Push-TAN, Sync-cancelled):
1. **Flush am linken Viewport-Rand** — die linke Neon-Border + Glow wird vom Bildschirmrand
   **abgeschnitten**; kein sauberer Inset links.
2. **Spannt fast die volle Breite** und sitzt als schwerer Riegel oben, statt als leichte,
   eingerückte Notification.
3. **Überdeckt primäre Bedienelemente** (Filter-Chips „Alle/Import/Prüfen", Header) bei `top-16`.
4. **Über dem hellen Hero-Gradient** (Sync-Startscreen) scheint der Gradient durch den
   transluzenten `bg-surface-card/95 backdrop-blur` durch → schlechter Kontrast, wirkt losgelöst.

## Acceptance criteria
- [x] Auf Mobile **beidseitig sauber eingerückt** — linke Neon-Border/Glow wird nicht mehr vom Viewport-Rand abgeschnitten. (`inset-x-4`; Inset links == rechts == 32px im Mess-Screenshot.)
- [x] `top-16` + `bottom-4`-Konflikt aufgelöst: der fixed-Container wird **nicht über die volle Höhe** gespannt; mobile Platzierung bewusst oben (`top-16`, kein `bottom`). (Container-Höhe 124px statt 915px.)
- [x] Der Toast verdeckt die primären Bedienelemente nicht dauerhaft (kompakter Overlay-Streifen unter dem Header; `pointer-events-none` Container, nur Toast klickbar).
- [x] Über dem hellen Hero-Gradient bleibt der Toast **lesbar und integriert** — innere Fläche voll deckend `bg-surface-card` (#111128) statt `/95`; kein durchscheinender Gradient.
- [x] Desktop-Platzierung (unten-rechts) bleibt intakt; Eingang/Abgang-Motion aus design-system-004 unverändert. (`positionToClasses` liefert `md:bottom-4 md:right-4`; `animate-toast-in/out` unverändert.)
- [x] Styleguide + ADR 0007 auf die finale Platzierung/Insets nachgezogen; `/styleguide`-Route zeigt den korrekten mobilen Sitz (rendert über das echte `Toast.containerDefault`/`renderList`).
- [x] Headless mobil verifiziert (Vite, Playwright 412×915@2x, hash-routing) — alle vier Screenshot-Defekte weg.

## Notes
**Root-Cause-Richtung (Code):** `src/Client/DesignSystem/Toast.fs` `container` baut
`"fixed z-50 … w-full max-w-sm px-4 md:px-0 … top-16 md:top-auto " + positionToClasses position`.
Default `position = BottomRight` → `positionToClasses = "bottom-4 right-4 md:bottom-4"`. Auf
**Mobile** ergibt das `fixed top-16 bottom-4 right-4 w-full max-w-sm px-4` — d.h. `top-16` wurde
für Mobile **vorangestellt, ohne** das geerbte `bottom-4`/`right-4` zu reconcilen: top+bottom
spannt die Höhe, und das Insetting/Deckkraft passt nicht. Die mobile Platzierung sollte als
**eigener, bewusster** Fall gebaut werden (nicht das Desktop-`positionToClasses` halb
überschreiben) — z.B. eine `inset-x`-basierte, symmetrisch eingerückte Box oben, mit klar
deckender Fläche über dem Hero.

- **Folge-Task zu** design-system-004 (18cf474; Live-Feedback) — Motion/Lifecycle dort ist ok, nur Platzierung/Integration offen. ADR 0007 (`related_adrs`) hält die Platzierungs-Entscheidung; ggf. dort präzisieren.
- **Gate:** UI-Task → `design-system-001` (done + reviewt, nicht blockierend). Nur DS-Tokens; KEINE arbitrary `text-[Npx]` / raw Tailwind-Palette (vgl. cat-001-Verifikation).
- **Quelle:** Roman-Screenshots 2026-06-18 (Mobile, Samsung-Browser, Tailscale-Host).

## Outcome
Die mobile Toast-Platzierung in `src/Client/DesignSystem/Toast.fs` wurde als **eigener,
bewusster Fall** neu gebaut, statt das Desktop-`positionToClasses` halb zu überschreiben.

**Root cause:** `container` stellte `top-16` voran und erbte aus `positionToClasses BottomRight`
weiterhin `bottom-4 right-4` → mobil `top-16 bottom-4 right-4` (volle Höhe gespannt, rechts-bündig,
linke Border am Rand abgeschnitten). Zusätzlich ließ `bg-surface-card/95` über dem hellen Hero
den Gradient durchscheinen.

**Fix (3 gezielte Änderungen in `Toast.fs`):**
1. `container` baut Mobile (`top-16 inset-x-4` — symmetrischer Inset, nur `top`) und Desktop
   (`md:top-auto md:inset-x-auto md:w-full md:max-w-sm` + Anker) getrennt.
2. `positionToClasses` liefert jetzt **nur `md:`-präfixierte** Anker — mobil wird nichts geerbt.
3. Innere Toast-Fläche `bg-surface-card` (voll deckend) statt `/95`; `backdrop-blur` bleibt.

**Verifikation (headless mobil, Playwright 412×915@2x, hash-routing über `/styleguide`,
das den echten `Toast.containerDefault`/`renderList` rendert):**
- Inset links == rechts (== 32px gemessen) → linke Neon-Border nicht abgeschnitten.
- Container-Höhe 124px (nicht 915px) → kein Full-Height-Stretch.
- Innere `background-color` = `rgb(17,17,40)` (= opakes `#111128`) → kein Gradient-Durchschein.
- Screenshots: `/tmp/ds005/toast-mobile-after.png` (canonical), `/tmp/ds005/toast-mobile-demo.png`.

**Tests:** Kein neuer Unit-Test — der Defekt ist reine fixed-position-/CSS-Layout-Geometrie
(keine Logik-Änderung; die Lifecycle-Logik aus `ToastLifecycleTests` ist unberührt und weiter
grün). Verifikation erfolgt über die geforderte headless-Mobile-Screenshot-Geometrie. Build:
`dotnet build` 0 Fehler/Warnungen; `dotnet test` 594/600 grün (6 .env-gated übersprungen).

**Doku:** ADR 0007 präzisiert (zwei Platzierungs-Fälle, Insets, Deckkraft; alter Erb-Bug als
Alternative dokumentiert), styleguide.md §6 nachgezogen, diary aktualisiert. README-Summary
blieb korrekt (mobil weiterhin „oben") → nicht angefasst.

**Key files:** `src/Client/DesignSystem/Toast.fs`,
`standards/frontend/styleguide.md` (§6),
`.agentheim/knowledge/decisions/0007-toast-placement-and-soft-exit.md`,
`diary/development.md`.
