---
id: infrastructure-pgpb6
title: "Gate läuft nach dem dotnet build in zwei parallelen Strängen: Expecto neben npm build, Wächtern und test:scripts"
status: backlog
type: chore
context: infrastructure
created: 2026-11-05
completed:
depends_on: [infrastructure-d1dwb, infrastructure-shsvf]
blocks: []
tags: [verify, gate, laufzeit]
related_adrs: []
related_research: []
prior_art: [infrastructure-dvy29]
---

## Why
`verify.sh` läuft alle sechs Schritte nacheinander. Nach infrastructure-d1dwb braucht Expecto rund 14 s, der Client-Strang (npm build, guard:classes, guard:tokens, test:scripts) weiter mehrere Minuten. Beide hängen nur vom dotnet build ab, nicht voneinander.

## What
Nach Schritt 1 laufen zwei Stränge gleichzeitig: Strang A Expecto (Tests.dll direkt, baut nichts), Strang B npm build, Wächter, test:scripts. Die Ausgabe bleibt je Schritt lesbar (Logs je Schritt wie heute, Zusammenfassung erst nach beiden Strängen), die Schlusszeile und die Gate-Semantik bleiben gleich.

## Acceptance criteria
- [ ] Schritt 2 läuft parallel zu den Schritten 3 bis 6; Gesamtdauer vorher und nachher gemessen.
- [ ] Ausgabe und Schlusszeile bleiben in Form gleich, rote Tests beider Stränge stehen gesammelt am Ende.
- [ ] Ctrl+C beendet beide Stränge ohne verwaiste Prozesse.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Offen für das Refinement:
- Verträgt sich das mit infrastructure-v8m2d (Vite stürzt ab, wenn dotnet parallel das Client-Projekt baut)? Strang A baut nach d1dwb nichts mehr, das Risiko sollte damit entfallen. Nachweisen.
- Lohnt der Umbau nach d1dwb noch? Ersparnis rund 14 s. Erst messen, dann entscheiden; bei zu kleinem Gewinn verwerfen.
- Reicht Bash mit Hintergrundjobs und `wait`, oder wird die Ausgabe dabei zu unübersichtlich?
