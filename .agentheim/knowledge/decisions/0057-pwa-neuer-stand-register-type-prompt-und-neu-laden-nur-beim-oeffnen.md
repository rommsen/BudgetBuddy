---
id: ADR-0057
title: PWA-Update über registerType 'prompt', Neuladen nur beim Zurückholen und nie mitten in einer Eingabe
scope: infrastructure
status: accepted
date: 2026-10-24
supersedes: []
superseded_by: []
related_tasks: [infrastructure-bnbdc]
related_adrs: [0010, 0014, 0053]
related_research: []
---

# ADR-0057: PWA-Update über registerType 'prompt', Neuladen nur beim Zurückholen und nie mitten in einer Eingabe

## Context

Nach dem Deploy vom 2026-10-19 zeigte die auf dem Handy installierte PWA den alten Stand,
bis Roman den Cache von Hand löschte. Eine Homescreen-PWA wird oft nur aus dem Speicher
fortgesetzt, ohne Navigation, dann fragt der Browser nicht nach einem neuen Service Worker.
`vite.config.js` nutzt `registerType: 'autoUpdate'` mit `injectRegister: 'script'`: Der
neue Worker übernimmt sofort (`skipWaiting`, `clientsClaim`), `registerSW.js` lädt die
Seite dann ohne Rücksicht auf eine laufende Eingabe neu (`activated` mit `isUpdate`).
Außerdem fragt niemand nach einem neuen Worker, solange keine Navigation stattfindet.

Roman hat am 2026-10-24 entschieden: Neu laden nur beim Öffnen. Läuft gerade eine Eingabe,
gibt es stattdessen einen Hinweis mit Knopf.

## Decision

1. **`registerType: 'prompt'`, `injectRegister: false`.** Der neue Worker bleibt im Zustand
   "waiting", bis der Client ihn freigibt. Der Client registriert den Worker selbst über
   `virtual:pwa-register` (`registerSW({ onNeedRefresh, onRegisteredSW })`) aus einem
   kleinen Interop-Modul `src/Client/Pwa.fs`. Das Neuladen ist damit eine Entscheidung des
   Elmish-Codes (`updateSW(true)`: Worker freigeben, Seite lädt bei `controlling` neu),
   nicht ein Nebeneffekt von `registerSW.js`.
2. **Die Entscheidung läuft durch `update`.** Zwei Elmish-Subscriptions in `App.fs`
   (neben der bestehenden Sichtbarkeits-Subscription) melden "Seite wieder sichtbar" und
   "neuer Worker wartet" als Nachrichten. Ob geprüft wird (Drosselung), ob sofort geladen
   oder nur gemeldet wird (Eingabe läuft?) und ob der Hinweis schon steht, entscheiden reine
   Funktionen über einem kleinen Snapshot des Modells. Es gibt keinen JS-Hook neben Elmish.
3. **Ablauf.** Wird die Seite sichtbar und liegt die letzte Prüfung mindestens fünf Minuten
   zurück: `registration.update()`. Wartet ein Worker und läuft keine Eingabe: sofort
   `updateSW(true)`. Läuft eine Eingabe: ein Hinweis "Neue Version, neu laden" mit Knopf, der
   bis zum Klick stehen bleibt (kein 5-Sekunden-Toast). Es gibt kein Neuladen, sobald die
   Eingabe endet; erst das nächste Zurückholen oder der Klick lädt.
4. **Kein Neuladen beim ersten Installieren, keine Schleife.** Beim ersten Installieren gibt
   es keinen wartenden Worker, `onNeedRefresh` feuert nicht (`installed` mit
   `isUpdate: false` ruft nur `onOfflineReady`). Nach dem Neuladen wartet kein Worker mehr.
   Der Client hält zusätzlich ein "Neuladen läuft"-Flag, das keinen zweiten Reload zulässt.
5. **`clientsClaim: true` bleibt, `skipWaiting` entfällt** (im Modus 'prompt' setzt das
   Plugin keines von beiden selbst; `clientsClaim` setzen wir explizit, damit die erste
   Installation die Seite übernimmt wie bisher).

## Alternatives considered

- **`autoUpdate` behalten und nur `registration.update()` ergänzen**: abgelehnt, weil
  `registerSW.js` bei jedem Wechsel sofort neu lädt, auch mitten in einem offenen Sheet oder
  halben Filter-Entwurf, und der Client es nicht aufhalten kann.
- **`autoUpdate` mit eigenem `onNeedReload`**: abgelehnt, weil `skipWaiting` dann schon den
  neuen Worker aktiviert hat, während die Seite noch das alte Bundle fährt. Mit 'prompt'
  bleibt der alte Stand, bis wir ihn bewusst wechseln.
- **Globaler JS-Hook, der `controllerchange` abfängt und DOM nach offenen Dialogen fragt**:
  abgelehnt, weil "Eingabe läuft" im Modell steht, nicht im DOM; ein DOM-Test wäre fragil
  und nicht als Expecto-Test prüfbar.
- **Immer automatisch neu laden, auch mitten in der Eingabe**: von Roman abgelehnt.

## Consequences

- `vite.config.js` ändert zwei Zeilen und `workbox`; `index.html` bekommt kein
  `registerSW.js` mehr (das Plugin lässt es bei `injectRegister: false` weg).
- Neuer F#-Baustein `Pwa.fs` (Interop) und ein reiner Teil mit Entscheidungsfunktionen,
  im Expecto-Projekt testbar. Das Root-Modell wächst um einen kleinen `Pwa`-Zustand.
- Der Hinweis braucht einen Toast, der nicht nach 5 s verschwindet, und eine neue
  `ToastActionKind`-Variante.
- Annahmen, die der Worker beim Bau prüft: `visibilitychange` feuert in der iOS-Homescreen-PWA
  beim Zurückholen (ADR 0014 stützt sich schon darauf); `registration.update()` holt `sw.js`
  an der HTTP-Cache-Schicht vorbei (Standardverhalten von `updateViaCache: 'imports'`).
