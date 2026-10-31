---
id: infrastructure-jsys4
title: "Web-Push-Kanal mit Abo in den Einstellungen, zuerst auf echten Geräten bewiesen"
status: todo
type: feature
context: infrastructure
created: 2026-10-30
completed:
depends_on: [banking-import-p5mcp]
blocks: [banking-import-bftq0]
tags: [backend, frontend, pwa, web-push, benachrichtigung, secrets]
related_adrs: [0071, 0010, 0057]
related_research: []
prior_art: []
---

## Why
Es gibt keinen ausgehenden Kanal, die Bot-API nimmt nur an. Roman will bei Verlust der
Bankverbindung eine Nachricht aufs Handy. `ledger-g47hp` (Benachrichtigung für Lena) braucht
denselben Kanal, deshalb liegt er in `infrastructure`.

## What
- Schritt 1: Geräte-Beweis mit einem Wegwerf-Push auf Romans Android-Handy. Empfängt die installierte PWA
  Push, und geht das ohne aktives Tailscale? Scheitert das auf Android: Befund notieren, Kanal stoppen,
  `banking-import-bftq0` meldet dann nur im UI.
- `notify`, VAPID, Abos und Handler per `importScripts`, `Cache-Control: no-cache`, Allowlist nach
  ADR 0071 Punkt 9.
- Knopf "Benachrichtigungen aktivieren" in den Einstellungen (Nutzer-Geste).
- `Lib.Net.Http.WebPush` auf .NET-10-Eignung prüfen. Subject ist die öffentliche Repo-URL, keine
  private Mail.

## Acceptance criteria
- [ ] GIVEN der Push-Dienst antwortet 404 oder 410 THEN das Abo ist gelöscht.
- [ ] GIVEN ein Endpoint außerhalb der Allowlist oder ohne HTTPS THEN das Abo ist abgelehnt, mit konkreter Meldung.
- [ ] `src/Tests/RemotingEndpointNegativeTests.fs` hat eine Zeile je neuem Endpoint, ein neues Interface bekommt einen Aufruf in `src/Tests/RemotingPipelineTests.fs`.
- [ ] GIVEN die Entschlüsselung der VAPID-Schlüssel scheitert THEN keine neuen Schlüssel, Fehler im Log.
- [ ] Der private Schlüssel geht nie an den Client (Vorbild `src/Tests/SettingsSecretsTests.fs`).
- [ ] Build-Prüfung: `sw.js` importiert den Handler, `runtimeCaching` bleibt leer, der Handler hat keinen `fetch`-Listener.
- [ ] Browser-Wächter: ein Happy Path für den Abo-Knopf, API gemockt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Eine Testnachricht kommt auf dem Android-Handy an, ein Tipp öffnet die App. [human-eye]

## Notes
Design-System: nichts Neues, vorhandener Button und Styleguide §8 für Text.

Android-Eignung und Empfang ohne Tailscale sind Annahmen ohne Beleg, deshalb Schritt 1. Der Inhalt
der Nachricht braucht Tailscale erst beim Öffnen der App.

Promotion erst nach Go in `banking-import-p5mcp`.
