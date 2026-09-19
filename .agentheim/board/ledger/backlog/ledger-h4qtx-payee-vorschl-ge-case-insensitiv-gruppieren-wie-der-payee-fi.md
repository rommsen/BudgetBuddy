---
id: ledger-h4qtx
title: "Payee-Vorschläge case-insensitiv gruppieren, wie der Payee-Filter selbst"
status: backlog
type: chore
context: ledger
created: 2026-09-19
completed:
depends_on: [ledger-vgfw3]
blocks: []
tags: [backend, kassenbuch]
related_adrs: [0036]
related_research: []
prior_art: [ledger-vgfw3]
---

## Why
`ledger-vgfw3` (ADR 0036) hat den Payee-Filter case-insensitiv gemacht
(`payee = @payee COLLATE NOCASE`), damit "rewe" auch "Rewe" und "REWE" trifft. Die
Payee-Vorschlagsabfrage (`Ledger.Persistence.loadPayeeSuggestions`), aus der die
Klick-Auswahl für den Filter stammt, gruppiert aber weiterhin case-sensitiv: der
SQL-Pfad über `GROUP BY payee`, der F#-Pfad (bei aktivem Betragsfilter) über
`List.countBy id`. Ergebnis: "Rewe" und "REWE" erscheinen als zwei getrennte
Vorschläge mit je einer eigenen Anzahl, obwohl ein Klick auf einen von beiden dank
`COLLATE NOCASE` dieselbe, größere Treffermenge liefert. Kein Test-Regressionsrisiko
(vom Verifier in Iteration 1 als "nicht blockierend" eingestuft), aber sichtbar
irreführend, sobald ein Payee in freier Schreibweise mehrfach vorkommt.

## What
Payee-Vorschläge case-insensitiv gruppieren, in beiden Pfaden von
`loadPayeeSuggestions` (SQL: `GROUP BY payee COLLATE NOCASE`; F#: Gruppierung über
eine normalisierte Schreibweise statt `List.countBy id`). Zu entscheiden: welche
Schreibweise als Anzeige-Text gewählt wird, wenn mehrere vorkommen (z. B. die
häufigste, die zuletzt beobachtete, oder eine feste Normalform) — das ist der Teil,
der mehr als eine Ein-Zeilen-Änderung ist und deshalb hier, nicht in `ledger-vgfw3`
selbst, entschieden werden soll.

## Acceptance criteria
- [ ] Ein Bestand mit "Rewe" (n mal) und "REWE" (m mal) liefert genau einen
      Vorschlag mit Anzahl n+m, nicht zwei getrennte.
- [ ] Die gewählte Anzeige-Schreibweise ist deterministisch (durch einen Test
      belegt, nicht zufällig je nach SQL-Ausführungsplan).
- [ ] Gilt in beiden Pfaden von `loadPayeeSuggestions` (mit und ohne aktivem
      Betragsfilter) gleich.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Gefunden als Randbeobachtung im Verifier-Befund zu `ledger-vgfw3`, Iteration 1
(2026-09-19). Nicht blockierend für `ledger-vgfw3` selbst.