---
id: infrastructure-bnbdc
title: "Installierte PWA übernimmt einen neuen Deploy von selbst: beim Zurückholen nach einem neuen Service Worker fragen und nach dem Wechsel einmal neu laden"
status: backlog
type: feature
context: infrastructure
created: 2026-10-20
completed:
depends_on: []
blocks: []
tags: [frontend, pwa, service-worker, deploy]
related_adrs: []
related_research: []
prior_art: [infra-002]
---

## Why
Nach dem Deploy vom 2026-10-19 zeigte der PC den neuen Stand nach einem Neuladen, die auf
dem Handy installierte PWA aber nicht. Roman musste den Cache von Hand loswerden. Eine PWA
vom Homescreen wird beim Öffnen oft nur aus dem Speicher fortgesetzt, ohne Navigation;
dann fragt der Browser nicht nach einem neuen Service Worker. `vite.config.js` registriert
den Worker mit `registerType: 'autoUpdate'` und `injectRegister: 'script'`: der neue Worker
übernimmt zwar, die offene Seite läuft aber mit dem alten Bundle weiter, bis sie neu lädt.

## What
1. Beim Zurückholen der App in den Vordergrund (`visibilitychange` auf `visible`) fragt der
   Client nach einem neuen Worker (`registration.update()`), gedrosselt (nicht öfter als
   etwa alle paar Minuten).
2. Übernimmt ein neuer Worker die Kontrolle (`controllerchange`), lädt die Seite einmal neu,
   nicht in einer Schleife und nicht beim allerersten Installieren.
3. Bezug zu `infra-002` (PWA installierbar) und zum Deploy-Weg
   (`scripts/deploy.sh`, Hook auf docker-host).

## Acceptance criteria
- To be defined during refinement.

## Notes
Erfasst am 2026-10-20 auf Romans Wunsch. Offene Fragen für die Verfeinerung:
- Darf das Neuladen mitten in einer Eingabe passieren (offenes Sheet, halber
  Filter-Entwurf, laufende Auswahl)? Oder nur, wenn die Seite gerade sichtbar wird, bevor
  der Nutzer etwas tut? Alternative: ein Hinweis "Neue Version, neu laden" statt
  automatisch.
- Wie wird das testbar: die Logik (Drosselung, einmaliges Neuladen) als reine Funktion,
  dazu ein Playwright-Wächter mit zwei Builds?
- Gilt das für iOS und Android gleich? iOS-PWAs halten den Cache besonders hartnäckig.
