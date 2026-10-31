---
id: infrastructure-g4wy8
title: "Spike: Warum brechen Anfragen zwischen Browser und Tailscale-Proxy mit Http error (0) ab?"
status: backlog
type: spike
context: infrastructure
created: 2026-10-31
completed:
depends_on: []
blocks: []
tags: [netzwerk, firefox, tailscale, service-worker, pwa]
related_adrs: [0010, 0057]
related_research: []
prior_art: [infrastructure-y6atr, infrastructure-awstw, infrastructure-bnbdc]
---

## Why

Das Retry fängt die Abbrüche ab, beseitigt sie aber nicht. Roman sieht sie 2026-10-31 ständig, in Import und Kassenbuch. Der erste Befund stammt vom 2026-10-24.

Stop-Loss: Ist die Abhilfe während des Spikes schon bekannt und billig, wird sie festgehalten und der Spike endet.

## What

Die Ursache bestimmen und entweder die Abhilfe als Task anlegen oder begründet festhalten, dass das Retry reicht.

Befunde bisher:

- Die App läuft stabil und antwortet mit 200. `tailscale_budgetbuddy` protokolliert zu den Reloads `http2: received GOAWAY` und `http: proxy error: context canceled`. Der GOAWAY kommt am HTTP/2-Server des Proxys an, also von der Client-Seite.
- infrastructure-y6atr: Mit Cmd+Shift+R (am Service Worker vorbei) ging es. Das ist die stärkste Spur. Der Worker läuft im Modus `prompt` (`vite.config.js`, `src/Client/Pwa.fs`, ADR 0057).
- Roman, 2026-10-31: Nach Cmd+Shift+R laden im Import Konten und Kategorien wieder. Das bestätigt die Spur Service Worker, ist aber eine einzelne Beobachtung. Ein Vergleich über mehrere Reloads steht noch aus.
- Roman, 2026-10-31: Kurz nach den harten Reloads kam der Fehler gar nicht mehr.
- **Stärkste Hypothese, nicht bewiesen:** Nach einem Deploy findet die Seite beim Laden einen neuen Worker. `onNeedRefresh` setzt `UpdateWaiting`, `Pwa.decide` antwortet ohne laufende Eingabe mit `ReloadNow`, und das Neuladen bricht die gerade laufenden Anfragen ab (Status 0, `context canceled`). Sobald der neue Worker aktiv ist, endet das. Bis dahin liefert der alte Worker außerdem das alte Client-Bundle aus dem Precache. Ein alter Client gegen den neuen Server könnte `getSettings` nicht lesen (seit 504cb2e `AppSettingsView`), dann blieben Kategorien und Konten leer, das Kassenbuch mit `getBudgetAccounts` aber nicht. Gegenprobe: beim nächsten Deploy Network-Log und Tailscale-Log mitlaufen lassen. Abhilfe wäre dann, das Neuladen nicht während laufender Abfragen auszulösen.
- Der Befund vom 2026-10-24 entstand nach einem Deploy. Zu klären ist, ob ein wartender Worker oder ein Neuladen durch `updateSW` laufende Anfragen abbricht.
- Firefox-Konsole: "Lokaler Netzwerkzugriff erkannt" je Anfrage, `auto_allow`. Ein Zusammenhang ist nicht belegt.
- `tailscale_budgetbuddy` läuft als `latest` (1.102.3), die meisten anderen Tailscale-Container auf 1.104.1.

## Acceptance criteria

- [ ] Gegenproben sind im Outcome festgehalten: Firefox normal, Firefox mit Cmd+Shift+R, Firefox mit deregistriertem Service Worker, ein zweiter Browser.
- [ ] Das Outcome nennt die Ursache mit Beleg oder die verworfenen Kandidaten samt Beleg.
- [ ] Bei gefundener Ursache liegt ein Task mit der Abhilfe vor, oder das Outcome begründet, warum das Retry reicht.

## Notes

Liegt im Backlog, weil die Gegenproben Roman im echten Browser und Zugriff auf die Produktion brauchen. Ein Worker im Worktree kann das nicht.
