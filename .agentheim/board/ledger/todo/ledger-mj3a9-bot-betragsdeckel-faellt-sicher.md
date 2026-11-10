---
id: ledger-mj3a9
title: "BOT_MAX_AMOUNT_EUR: ein gesetzter, aber ungültiger Wert sperrt Bob, statt auf 500 € zurückzufallen"
status: todo
type: bug
context: ledger
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [laufend, bot, sicherheit]
related_adrs: [0068]
related_research: []
prior_art: []
---

## Why
`resolveBotMaxAmountEur` liefert bei unlesbarem Wert die Vorgabe 500 EUR, die gleich dem harten Deckel ist
(`src/Server/ServerConfig.fs`). Ein Tippfehler beim Absenken ("50,00") gibt Bob die höchste Grenze (Audit c-review B2,
geprüft). ADR 0068 Punkt 1 hatte diesen Rückfall ausdrücklich festgelegt; Roman hat ihn am 2026-11-10 geändert.

## What
Entscheidung Roman (2026-11-10): Bob sperren, BB läuft weiter.

- **Nicht gesetzt** (leer oder nur Leerraum): 500 EUR wie heute.
- **Gesetzt und gültig** (nur Ziffern mit optionalem Dezimalpunkt, größer 0): der Wert, höchstens 500 EUR
  (ein größerer Wert wird wie heute auf `hardBotMaxAmountEur` gedeckelt; die Umgebung darf nur absenken).
- **Gesetzt, aber ungültig** (Komma, Vorzeichen, Tausendertrennzeichen, 0, Text): Bob ist gesperrt. Jede Anfrage
  unter `/bot/` (`preview` und `commit`) wird abgelehnt, mit dem Grund
  "Bot gesperrt: BOT_MAX_AMOUNT_EUR ist ungültig" und ohne Rohwert in der Antwort. Kein Ereignis, kein Aufruf an
  YNAB, keine Vorschau gespeichert. Der Rest von BB läuft normal.
- **Form:** `resolveBotMaxAmountEur` liefert statt `decimal` einen Typ mit drei Fällen (etwa
  `BotMaxAmount = Default | Lowered of decimal | Invalid of raw: string`) oder `Result<decimal, string>`; der
  Worker wählt. `BotDeps.MaxAmountEur` trägt das Ergebnis, der Bot-Handler prüft es vor jeder anderen
  Validierung (nach der Token-Prüfung: ohne gültiges Token bleibt es bei 401).
- **Startlog:** bei ungültigem Wert nennt die Zeile den Rohwert und die Folge ("Bob ist gesperrt, bis der Wert
  korrigiert ist"). Das Token kommt nie im Log vor.
- **Nachtrag zu ADR 0068** (Punkt 1): ungültige Werte sperren Bob, zu hohe werden auf 500 € gedeckelt.
- Die Grenzen aus CLAUDE.md (Security-Fallen) werden nur strenger, nichts wird gelockert oder konfigurierbar.

## Acceptance criteria
- [ ] Kaputt-Szenario (Regressionstest, rot vor dem Fix): `"50,00"` ergibt nicht mehr 500 EUR, sondern den
      gesperrten Fall; ebenso `"0"`, `"-5"`, `"1,000"` und `"abc"`, je ein Fall in `src/Tests/ServerConfigTests.fs`.
- [ ] Intakt-Szenario: nicht gesetzt und nur Leerraum ergeben 500 EUR, `"100.00"` ergibt 100 EUR, `"900"` ergibt
      500 EUR.
- [ ] Bot-Handler (`src/Tests/BotApiTests.fs`): bei gesperrtem Deckel lehnen `preview` und `commit` mit dem Grund
      ab, ohne YNAB-Aufruf, ohne Ereignis und ohne gespeicherte Vorschau; ohne gültiges Token bleibt es 401.
- [ ] Startlog-Zeile bei ungültigem Wert nennt Rohwert und Folge, bei nicht gesetztem und gültigem Wert wie heute;
      das Token steht in keiner Zeile.
- [ ] Nachtrag zu ADR 0068 Punkt 1 geschrieben; BC-README ledger (Abschnitt Bob) nachgezogen, alte Sätze zum
      Rückfall auf 500 € an allen Stellen (grep nach "fallen auf 500", "Vorgabe gilt", "ungültig").
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Migrationsschritt P3. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`.
Roman hat am 2026-11-10 entschieden (Bob sperren statt Startabbruch oder niedrigem Rückfallwert): ein
Startabbruch nähme ganz BB vom Netz, ein fester kleiner Wert wäre eine Grenze, die niemand gewählt hat.
