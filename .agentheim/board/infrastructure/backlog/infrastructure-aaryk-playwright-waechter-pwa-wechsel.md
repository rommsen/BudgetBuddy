---
id: infrastructure-aaryk
title: Playwright-Wächter für den PWA-Wechsel mit zwei Builds: alter Stand läuft, neuer Deploy kommt, Zurückholen lädt neu bzw. zeigt den Hinweis
status: backlog
type: chore
context: infrastructure
created: 2026-10-24
completed:
depends_on: [infrastructure-bnbdc]
blocks: []
tags: [pwa, service-worker, test, playwright]
related_adrs: [0057]
related_research: []
prior_art: [infrastructure-bnbdc]
---

Ziel-Lage: backlog (erst nach infrastructure-bnbdc und nur, wenn Roman den Wächter will).

## Why
Die Expecto-Tests von infrastructure-bnbdc decken Entscheidungslogik und Zustandsübergänge ab,
nicht den echten Service-Worker-Wechsel. Ein Wächter mit zwei Builds würde ihn im Browser prüfen.

## What
`scripts/pwa-update.test.mjs` nach dem Muster der anderen `scripts/*.test.mjs`: Build A und
Build B (anderer Inhalt, z. B. eine Konstante im Bundle) werden in zwei Verzeichnisse gebaut.
Ein kleiner statischer Server liefert zuerst A, Playwright (Chromium) öffnet die Seite, wartet
auf den aktiven Worker, der Server schaltet auf B um, die Seite wird per `visibilitychange`
zurückgeholt.

## Acceptance criteria
- [ ] Ohne offene Eingabe lädt die Seite nach dem Zurückholen genau einmal neu und zeigt Build B.
- [ ] Mit offenem Buchungs-Detail lädt sie nicht, der Hinweis "Neue Version, neu laden" steht da;
  der Klick lädt neu und zeigt Build B.
- [ ] Beim ersten Öffnen von A gibt es kein Neuladen.
- [ ] Der Test läuft in `npm run test:scripts` in unter 90 Sekunden oder ist dort bewusst
  ausgenommen und eigens aufrufbar.

## Notes
Unsicher: Service Worker in Playwright brauchen eine sichere Herkunft (localhost reicht) und
lassen sich pro Kontext zählen; ob der Wechsel des Servers zwischen zwei Verzeichnissen ohne
Neustart verlässlich ist, ist nicht geprüft. Ist der Aufwand zu groß, den Entwurf verwerfen.
