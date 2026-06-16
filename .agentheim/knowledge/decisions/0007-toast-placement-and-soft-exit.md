---
id: 0007
title: Toast-Platzierung (Desktop unten-rechts / Mobile oben) und Zwei-Phasen-Abgang
scope: design-system
status: accepted
date: 2026-06-16
supersedes: []
superseded_by: []
related_tasks:
  - contexts/design-system/done/design-system-004-toast-polish.md
related_research: []
---

# ADR 0007: Toast-Platzierung und sanfter Zwei-Phasen-Abgang

## Context
Toasts sind das primäre flüchtige Feedback-Signal des gesamten Flows (Sync, Rules,
Settings, Quick Add). Vor dieser Entscheidung verschwanden sie **abrupt**: beide
Dismiss-Pfade (`AutoDismissToast` / `DismissToast`) entfernten den Toast sofort per
`List.filter`, ohne Exit-Animation. Das wirkte janky und nicht ins neon-on-dark-Design
integriert — was direkt dem Vision-Ziel "Roman fühlt den Workflow als angenehmer als
YNAB-Web" zuwiderläuft. Außerdem war die Platzierung nicht als projektweite Regel
festgehalten (nur als Default im Container-Code).

## Decision
Projektweite Konventionen für alle Toasts (DesignSystem):

1. **Platzierung.** Desktop: unten rechts (`bottom-4 right-4`). Mobile: **oben**
   (`top-16`, unter dem App-Header). Mobil bewusst oben, damit der Toast die
   **mobile Bottom-Nav nicht überdeckt** — die Kern-UI bleibt frei. Der Container ist
   `fixed z-50 … pointer-events-none`; nur die Toasts selbst sind klickbar
   (`pointer-events-auto`).

2. **Zwei-Phasen-Removal (sanfter Abgang).** Toasts verschwinden nie abrupt. Der
   Lifecycle liegt in der App-State (`State.fs`):
   - `StartDismissToast id` — markiert den Toast als *exiting* (`Toast.markExiting`),
     was die CSS-Exit-Animation startet, und plant `ToastExited id` nach
     `Types.Toast.exitDurationMs`.
   - `ToastExited id` — entfernt den Toast endgültig (`Toast.remove`).
   - **Auto-Dismiss und manuelles Schließen** laufen über **denselben** Pfad
     (`StartDismissToast`), damit beide den weichen Abgang bekommen.
   - **Doppel-Fire-Guard:** `StartDismissToast` ist ein No-Op, wenn der Toast bereits
     exiting ist (`Toast.isExiting`) — kein zweiter Removal-Timer, keine
     Doppel-Entfernung, kein Timer-Leak bei schnellem Dismiss.

3. **Motion.** Eingang `.animate-toast-in` (Slide-up + leichtes Scale), bevorzugt den
   geteilten Federwert `--sf-spring-out` hinter `@supports (transition-timing-function:
   linear(0,1))`, Fallback `--transition-spring`. Abgang `.animate-toast-out`
   (Fade + Slide-down, `ease-in`, 220 ms). Beide Klassen sitzen **nur auf dem inneren
   Toast-Element**, nie auf dem fixen Container (`css-animation-safety`:
   `transform`/`fill-mode` auf einem Container mit `fixed`-Kindern bricht deren
   Viewport-Positionierung). `prefers-reduced-motion` ist über den globalen
   `@media`-Block abgedeckt; die Entfernung ist timer-getrieben und damit unabhängig
   von der CSS-Dauer robust.

## Consequences
### Positive
- Toasts fühlen sich poliert und ins Design integriert an; der weiche Abgang ist
  konsistent für Auto-Dismiss und manuelles Schließen.
- Die Lifecycle-Übergänge (`markExiting` / `isExiting` / `remove`) sind als **pure**
  Helfer in `Types.Toast` herausgezogen und unit-getestet (`ToastLifecycleTests`).
- Die Exit-Dauer ist an genau einer Stelle (`Types.Toast.exitDurationMs`) definiert und
  per Kommentar an die CSS-Keyframe-Dauer gekoppelt.

### Negative
- Eine Konstante muss in zwei Sprachen synchron gehalten werden (F#
  `exitDurationMs` ↔ CSS `0.22s`). Driftet sie, ist der Abgang entweder abgeschnitten
  oder hängt kurz nach. Mitigiert durch Kommentare auf beiden Seiten.

### Neutral
- Stapel-Reflow (mehrere Toasts gleichzeitig) wurde bewusst **nicht** poliert
  (Roman hat das nicht als Problem markiert).

## Alternatives considered
- **Delayed-Unmount allein (Timeout entfernt ohne Exiting-Flag).** Abgelehnt: ohne
  ein gerendertes Exiting-Flag gibt es keine Exit-Animation, nur eine Verzögerung.
- **Exit-Animation auf dem fixen Container.** Abgelehnt: `transform`/`fill-mode` auf
  einem Container mit `fixed`-Kindern bricht deren Positionierung (css-animation-safety).
- **Toasts mobil unten lassen.** Abgelehnt: überdeckt die Bottom-Nav.

## References
- `src/Client/DesignSystem/Toast.fs`, `src/Client/Types.fs` (`Toast`-Modul),
  `src/Client/State.fs`, `src/Client/styles.css` (`.animate-toast-in/out`).
- `standards/frontend/styleguide.md` §6 (Motion).
- `src/Tests/ToastLifecycleTests.fs`.
- Federwert-Konvention: ADR 0005, `infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs`.
