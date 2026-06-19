---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: infra-002 gebaut & committet → Board leer

`infra-002` (PWA installierbar) ist **fertig gebaut, verifiziert (PASS iter 2) und committet**
(`7937b0d`): `vite-plugin-pwa` mit Shell-Precache-SW, `/api` strikt network-only (kein
Daten-Cache), `autoUpdate`, gebrandete `offline.html`, iOS/Favicon/theme-color auf `#08081a`,
`.webmanifest`-MIME im Giraffe-Static-Handler, ADR 0010. **todo + doing sind jetzt über alle
BCs leer.**

Zwei Dinge stehen offen: (1) Das **Human-Gate** — die device-/deploy-gebundenen ACs
(Chrome/Edge-Install-Prompt, iOS „Zum Home-Bildschirm", `autoUpdate` end-to-end, Live-`offline.html`)
sind nur **hinter Tailscale-HTTPS** im secure context abnehmbar, nicht maschinell. (2) Mehrere lokale
Commits (PWA-Kette + ds-007/ds-008) sind weiterhin **ungepusht** — sinnvoll zusammen mit der fertigen
PWA zu pushen/deployen.

<options>
  <option title="Deployen & PWA-Abnahme">Docker-Image bauen, ungepushte Commits pushen, hinter Tailscale-HTTPS deployen — dann Install auf echten Geräten (Chrome/Edge + iOS) prüfen. Tradeoff: schließt das offene Human-Gate und liefert die PWA real aus, ist aber manuelle Deploy-/Geräte-Arbeit außerhalb der Worker-Schleife.</option>
  <option title="Backlog füllen" cmd='/agentheim:modeling'>Board ist leer — nächste Vision-Prioritäten einkippen (Split-Transaction-UI: Backend da/UI fehlt; Transfer-Payees: hohe Prio; ggf. die in infra-002 ausgeklammerte reichere In-App-Offline-UX). Tradeoff: verschiebt Deploy/Abnahme der gerade fertigen PWA nach hinten.</option>
  <option title="Empfehlung holen" cmd='/agentheim:whats-next'>Unentschieden? `whats-next` liest Vision + Boards + Protokoll und schlägt den nächsten sinnvollen Schritt vor. Tradeoff: eine Orientierungsrunde, ändert nichts am Stand.</option>
</options>
