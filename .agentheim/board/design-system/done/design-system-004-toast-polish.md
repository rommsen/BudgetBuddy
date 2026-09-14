---
id: design-system-004
title: Toast-Politur — sanfter Abgang, Positionierung & Styleguide-Motion
status: done
type: feature
context: design-system
created: 2026-06-16
completed: 2026-06-16
commit: 18cf474
depends_on: [design-system-001]
blocks: []
tags: [frontend, toast, motion, design-system]
related_adrs: [0007]
related_research: []
prior_art: []
---

## Why
Die Toasts wirken **janky und nicht ins Design integriert** (Roman, 2026-06-16). Konkret
benannt: (1) **kein sanfter Abgang** — Toasts verschwinden abrupt; (2) **Position/Integration**
wirkt losgelöst vom Layout; (3) **Look/Motion** nicht auf Styleguide-Niveau. Toasts sind das
primäre Feedback-Signal des gesamten Flows (Sync, Rules, Settings, Quick Add) — wenn sie billig
wirken, untergräbt das direkt das Vision-Ziel „Roman fühlt den Workflow als angenehmer als
YNAB-Web".

## What
Politur-Pass der Toast-DS-Komponente **plus** des Toast-Lifecycles:
1. **Sanfter Abgang** via Zwei-Phasen-Removal (markieren → Exit-Animation → entfernen) in der App-State.
2. **Positionierung neu entscheiden** und sauber ins Layout integrieren (Desktop vs Mobile), gegen den Styleguide begründet.
3. **Motion/Look** (Easing, Timing, Glow/Glas) auf Styleguide-Motion-Niveau; Styleguide-Motion-Sektion + `/styleguide`-Route nachziehen.

**Explizit NICHT in Scope:** Stapel-Reflow-Politur — Roman hat „Stapeln ruckelt" *nicht* als
Problem markiert. Nur mitnehmen, falls trivial.

## Acceptance criteria
- [x] Toasts blenden **sanft aus** (Exit-Animation) statt abrupt — bei Auto-Dismiss **und** manuellem Schließen.
- [x] Exit-Lifecycle sauber in der MVU-State modelliert (Zwei-Phasen: „exiting" markieren, nach Animationsdauer entfernen); keine hängenden Timer / Doppel-Entfernung bei schnellem Dismiss.
- [x] Position/Integration final entschieden (Desktop + Mobile), gegen den Styleguide begründet; überlappt keine Kern-UI (mobile Bottom-Nav). → ADR 0007.
- [x] Eingang + Abgang nutzen Styleguide-konforme Easing/Timing (Spring/Motion-Konvention); `prefers-reduced-motion` wird respektiert (globaler `@media`-Block + timer-getriebene Entfernung).
- [x] Styleguide aktualisiert: Motion-Sektion in `standards/frontend/styleguide.md` beschreibt das Toast-Verhalten; die `/styleguide`-Route (design-system-003) zeigt den neuen Toast inkl. Abgang.
- [x] Nur DS-/Motion-Tokens, kein hartkodiertes Drift; bestehender Aufrufer (`Toast.renderList` in `View.fs`) mitgezogen (4-Tupel mit Exiting-Flag).
- [x] Build + bestehende Tests grün; Lifecycle-Logik als pure Helfer (`Types.Toast`) herausgezogen und unit-getestet (`ToastLifecycleTests`, 8 Tests).

## Notes
**Architektur heute:**
- App-State hält `Toasts: Toast list` (`src/Client/State.fs:18`); `ShowToast` prepended + ein `Cmd`-Auto-Dismiss-Timeout (`State.fs:49-56`).
- **Beide** Dismiss-Pfade (`AutoDismissToast` / `DismissToast`, `State.fs:135/138`) entfernen den Toast **sofort** per `List.filter` → daher kein Abgang.
- Render: `Toast.renderList` (`src/Client/View.fs:83`) → `containerDefault` (BottomRight Desktop / `top-16` Mobile).
- DS-Komponente: `src/Client/DesignSystem/Toast.fs` — Eingang `animate-slide-up` (Z.84), **keine** Exit-Klasse; Container `fixed z-50 … pointer-events-none`, `top-16 md:top-auto` + Position.

**Zwei-Phasen-Removal berührt `State.fs` UND die Toast-Komponente** — nicht rein DS-lokal.
Vorschlag: `Toast`-Record um `Exiting`-Flag, neue Msg `StartDismissToast` (setzt Flag + plant
`ToastExited`-Cmd nach Animationsdauer), `ToastExited` entfernt endgültig.

**Motion nicht neu erfinden:** Spring/Easing-Konventionen existieren bereits (infrastructure
done-Task `2026-06-11-mobile-polish-sticky-filter-skeleton-springs`) + Styleguide-Motion-Sektion
— daran ausrichten. `web-animation-design`-Skill als Hilfe für Easing/Timing.

**Ggf. kleine ADR** für Toast-Platzierung + Motion-Konvention (Worker-Entscheidung), falls die
Positionierung eine projektweite Regel setzt.

**Gate:** UI-Task → `design-system-001` (done + reviewt, nicht blockierend).

## Outcome
Toasts haben jetzt einen **sanften Zwei-Phasen-Abgang** statt der abrupten Sofort-Entfernung.

**Lifecycle (MVU, `src/Client/State.fs`):**
- `StartDismissToast id` markiert den Toast über den puren Helfer `Toast.markExiting` als
  *exiting* (startet die CSS-Exit-Animation) und plant `ToastExited id` nach
  `Types.Toast.exitDurationMs` (220 ms, an die CSS-Keyframe-Dauer gekoppelt).
- `ToastExited id` entfernt den Toast endgültig (`Toast.remove`).
- **Auto-Dismiss** (Timer nach 5 s) **und manuelles Schließen** (Close-Button →
  `dispatch (StartDismissToast id)` in `View.fs`) laufen über **denselben** Pfad → beide
  bekommen den weichen Abgang.
- **Doppel-Fire-Guard:** `StartDismissToast` ist ein No-Op, wenn der Toast bereits exiting
  ist (`Toast.isExiting`) — kein zweiter Removal-Timer, kein Leak, keine Doppel-Entfernung.

**Pure & testbar:** Die Übergänge liegen als pures `Toast`-Modul in `src/Client/Types.fs`
(`markExiting` / `isExiting` / `remove` / `exitDurationMs`) und sind in
`src/Tests/ToastLifecycleTests.fs` mit 8 Tests abgedeckt (inkl. Idempotenz / Doppel-Dismiss /
No-Op-Removal).

**Motion (`src/Client/styles.css`):** Eingang `.animate-toast-in` (Slide-up + leichtes Scale,
bevorzugt den geteilten Federwert `--sf-spring-out` hinter `@supports`, Fallback
`--transition-spring`); Abgang `.animate-toast-out` (Fade + Slide-down, `ease-in`, 220 ms).
Beide Klassen sitzen **nur auf dem inneren Toast-Element** (`Toast.fs`), nie auf dem fixen
Container (css-animation-safety). `prefers-reduced-motion` über den globalen `@media`-Block;
die Entfernung ist timer-getrieben und damit unabhängig von der CSS-Dauer robust.

**Platzierung (ADR 0007):** Desktop unten rechts, Mobile oben (`top-16`) über der Bottom-Nav —
keine Kern-UI-Überlappung. In der Styleguide-Motion-§6 und in der `/styleguide`-Toast-Demo
(`src/Client/Components/Styleguide/View.fs`, zeigt den Abgang) dokumentiert.

**Aufrufer mitgezogen:** `Toast.renderList` nimmt jetzt 4-Tupel `(Id, Message, Variant,
Exiting)`; `View.fs` und die `/styleguide`-Demo angepasst.

**Schlüsseldateien:** `src/Client/Types.fs`, `src/Client/State.fs`,
`src/Client/DesignSystem/Toast.fs`, `src/Client/View.fs`, `src/Client/styles.css`,
`src/Client/Components/Styleguide/View.fs`, `standards/frontend/styleguide.md`,
`src/Tests/ToastLifecycleTests.fs`, ADR 0007.

**Verifikation:** `dotnet build` Client grün (0 Warnungen); `dotnet test` 594 passed /
6 skipped (Integration, `.env`); die 8 neuen Toast-Lifecycle-Tests grün.

**Bewusst NICHT in Scope:** Stapel-Reflow (mehrere Toasts gleichzeitig) — von Roman nicht
als Problem markiert.
