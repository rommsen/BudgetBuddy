---
id: ledger-z6sya
title: "Buchung löschen im Detail, auch in YNAB, mit Rückgängig"
status: backlog
type: feature
context: ledger
created: 2026-11-09
completed:
depends_on: []
blocks: []
tags: [etappe-2, ynab, detail, loeschen]
related_adrs: [0053, 0058, 0091]
related_research: []
prior_art: [ledger-at63w]
---

## Why
Etappe 2 des Fahrplans (Lücken schließen, ADR 0086): Nichts soll mehr zu YNAB zwingen außer Reports. Eine
Buchung löschen geht heute nur in YNAB; BB sieht die Löschung erst über den Spiegel. Seit ledger-at63w gibt es
"Rückgängig" direkt nach dem Erfassen (ADR 0091), aber kein Löschen einer beliebigen Buchung im Detail.

## What
Im Buchungs-Detail (ADR 0053) eine Aktion "Löschen". BB löscht die Buchung in YNAB und hält die Entscheidung im
Kassenbuch fest; ein Toast bietet "Rückgängig". Offen für die Verfeinerung:
- Welche Buchungen löschbar sind (eigene Barbuchungen, Comdirect-Importe, Buchungen aus YNAB; abgeglichene,
  Übertrags-Buchungen mit Gegenbuchung, Split-Buchungen).
- Wie "Rückgängig" geht: YNAB-Löschung lässt sich nicht zurücknehmen, also Neuanlage per POST. Eine gelöschte
  Buchung sperrt ihre `import_id` (ADR 0058 Probelauf 3), die Neuanlage braucht eine Ersatz-`import_id`
  (Schema aus ADR 0058 Punkt 2 und Nachtrag ledger-m7k2d). Die Buchung bekommt dann eine neue YNAB-Id
  (`ynabsuperseded`, ADR 0058 Punkt 5).
- Reihenfolge und Fehlerfälle nach dem Vorbild von ADR 0091 (YNAB zuerst, dann das Ereignis), eigenes Ereignis
  der Entscheidungs-Familie (ADR 0083) oder Erweiterung von `EntryDiscardedInBb`.
- Wie sich die Löschung auf Budget und Kontostand auswirkt (gelöschte Buchungen zählen heute nicht).
- Skizze: Stelle und Form der Aktion im Detail nach Prototyp (Version 15 oder neuer), Abgleich mit der heutigen
  Seite nach den Regeln in CLAUDE.md.

## Acceptance criteria
Noch offen, kommt in der Verfeinerung.

## Notes
- Vorbild: `discardCapturedBooking` (ADR 0091), Schreib-Sperre `WriteGuard.exclusive`, Historie-Satz.
- Erfordert eine Entscheidung (eigenes ADR oder Nachtrag zu ADR 0091).
- Wahrscheinlich nach ledger-m7k2d bauen, falls Rückgängig das Ersatz-`import_id`-Schema nutzt.
