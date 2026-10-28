---
id: ledger-ybm5x
title: "BOT_MAX_AMOUNT_EUR parst 500,00 als 50000: nur Dezimalpunkt zulassen, harte Obergrenze im Code, Wert beim Start loggen (Security-Review M2)"
status: backlog
type: bug
context: ledger
created: 2026-10-28
completed:
depends_on: []
blocks: [ledger-ssmp3]
tags: [security, bob]
related_adrs: [0066, 0067, 0068]
related_research: []
prior_art: []
---

## Why
`BOT_MAX_AMOUNT_EUR` liest "500,00" als 50000, weil `NumberStyles.Number` Tausendertrennzeichen zulässt. Ein Tippfehler in der `.env` hebt Bobs Obergrenze damit still um das Hundertfache an (Security-Review M2).

## What
`Server.ServerConfig.resolveBotMaxAmountEur` wird nach ADR 0068 Punkt 1 umgebaut:

- Lesen nur mit `NumberStyles.AllowDecimalPoint` und `InvariantCulture`: "250.50" gilt, "500,00", "1,000", "-5", "abc" und leer gelten nicht.
- Harter Deckel im Code: höchstens 500 € (eigene Konstante, z. B. `hardBotMaxAmountEur`). Ein größerer konfigurierter Wert wird auf 500 € gedeckelt, `BOT_MAX_AMOUNT_EUR` darf nur absenken.
- Ungültiger Wert: Vorgabe 500 €.
- Beim Start steht der wirksame Wert im Log, bei Deckelung oder ungültigem Wert mit Hinweis auf den gelesenen Rohwert (das Token nie).

## Acceptance criteria
- [ ] Werttests für `resolveBotMaxAmountEur`: "250.50" → 250,50 €; "500" → 500 €; "500,00" → 500 € (ungültig, Vorgabe); "1,000" → 500 €; "900" → 500 € (gedeckelt); "0", "-5", "abc", leer und null → 500 €.
- [ ] Der Deckel ist eine benannte Konstante im Code und nicht über die Umgebung änderbar (Test: großer Wert ergibt den Deckel).
- [ ] Beim Start schreibt der Server eine Zeile mit dem wirksamen Wert ins Log; bei gedeckeltem oder ungültigem Rohwert nennt sie den Rohwert (Test über eine reine Funktion, die den Logtext baut).
- [ ] `docs/bot-api.md` und `.env.example` nennen: nur Dezimalpunkt, höchstens 500 €, die Umgebung darf nur absenken.
- [ ] Die ledger-README (Bot-Vorschau) nennt den harten Deckel.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Herkunft: Security-Review der Homelab-Sitzung (homelab-58) am 2026-10-28, erfasst per quick-capture, verfeinert mit Roman am 2026-10-28. Befund M2, zweiter in der Reihenfolge. ADR 0068 Punkt 1.
Die Obergrenze gilt je Buchung; die Tagesgrenze kommt in ledger-ssmp3.
