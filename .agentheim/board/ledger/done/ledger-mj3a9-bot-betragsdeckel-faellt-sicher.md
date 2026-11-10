---
id: ledger-mj3a9
title: "BOT_MAX_AMOUNT_EUR: ein gesetzter, aber ungültiger Wert sperrt Bob, statt auf 500 € zurückzufallen"
status: done
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

## Outcome

Ein gesetzter, aber ungültiger `BOT_MAX_AMOUNT_EUR` sperrt Bob. Bisher fiel er auf 500 EUR zurück, also auf den harten Deckel.

- `src/Server/ServerConfig.fs`: `resolveBotMaxAmountEur` gibt jetzt `Result<decimal, string>` zurück. Nicht gesetzt (null, leer, Leerraum) ergibt `Ok 500`, gültig ergibt `Ok (min wert 500)`, ungültig ergibt `Error rohwert`. Neu ist `botBlockedReason` ("Bot gesperrt: BOT_MAX_AMOUNT_EUR ist ungültig"). Bei ungültigem Wert nennt `botMaxAmountLogLine` den Rohwert und "Bob ist gesperrt, bis der Wert korrigiert ist", ohne wirksame Grenze. Bei nicht gesetztem, gültigem und gedeckeltem Wert bleibt die Zeile unverändert. Der Aufruf in `Api.fs` bleibt, wie er war.
- `src/Server/BotApi.fs`: `BotDeps.MaxAmountEur : unit -> Result<decimal, string>`. Der Einstieg `handler` prüft den Deckel nach Drosselung und Token-Prüfung und vor dem Routing. Bei `Error` antwortet er mit 503 `bot_blocked` und dem festen Grund, ohne Rohwert. Das gilt für jede angemeldete Route (preview, commit, categories). Es gibt keinen YNAB-Aufruf, kein Ereignis und keine gespeicherte Vorschau. `previewHandler` bekommt den gültigen Wert als Parameter. Neu ist `BotPreviewStore.Count` für den Nachweis "keine Vorschau gespeichert". Die Wahl von 503 fiel ohne Vorgabe: Die Schnittstelle ist durch die Konfiguration abgeschaltet, mit der Anfrage selbst ist nichts falsch.
- Tests: Die Tests zur Obergrenze liegen jetzt in `src/Tests/ServerConfigTests.fs` (Liste `BOT_MAX_AMOUNT_EUR`), die alten Fälle mit Rückfall wurden aus `src/Tests/BotValidationTests.fs` entfernt. Je ein Sperr-Fall für "50,00", "0", "-5", "1,000" und "abc". Dazu die Intakt-Fälle nicht gesetzt und Leerraum (500), "100.00" (100) und "900" (500), die Startlog-Fälle, kein Token im Log und der Sperrgrund. In `src/Tests/BotApiTests.fs` stehen drei Handler-Tests: Vorschau gesperrt, Commit einer offenen Vorschau gesperrt (kein Commit gestartet, kein Push, kein Kassenbuch-Eintrag) und 401 ohne gültiges Token, 503 mit Token auf jeder Route. Regressionsnachweis: Der Startlog-Test war vor dem Fix rot (die Zeile lautete "... ist ungültig ..., die Vorgabe gilt."). Der alte Test verlangte für "500,00" noch 500 EUR.
- Gate: `./verify.sh` meldet `VERIFY: PASS` (dotnet 4654 grün, node 311 grün).

**Nachtrag zu ADR 0068** (die Rückgabe hat keine Form für Nachträge, der Conductor hängt ihn unten an `.agentheim/knowledge/decisions/0068-bob-tagesgrenze-datumsfenster-und-drosselung.md` an):

```markdown
## Nachtrag 2026-11-10 (ledger-mj3a9): ungültiger Wert sperrt Bob

Punkt 1 wird geändert. Der Rückfall auf 500 € gab einem Tippfehler beim Absenken (etwa "50,00")
die höchste Grenze. Roman hat am 2026-11-10 entschieden, Bob in diesem Fall zu sperren. BB läuft weiter.

- **Nicht gesetzt** (leer oder nur Leerraum): 500 €.
- **Gesetzt und gültig** (nur Ziffern mit optionalem Dezimalpunkt, größer 0): der Wert. Zu hohe
  Werte werden auf 500 € gedeckelt.
- **Gesetzt, aber ungültig** (Komma, Vorzeichen, Tausendertrennzeichen, 0, Text): Bob ist gesperrt.
  Jede angemeldete Anfrage unter `/bot/` bekommt nach der Token-Prüfung und vor jeder anderen
  Prüfung 503 `bot_blocked` mit dem Grund "Bot gesperrt: BOT_MAX_AMOUNT_EUR ist ungültig", ohne
  Rohwert in der Antwort. Es gibt keinen YNAB-Aufruf, kein Ereignis und keine gespeicherte Vorschau.
  Ohne gültiges Token bleibt es bei 401.
- Der Startlog nennt bei ungültigem Wert den Rohwert und "Bob ist gesperrt, bis der Wert korrigiert ist".

Verworfen: ein Startabbruch, der ganz BB vom Netz nähme, und ein fester kleiner Rückfallwert,
also eine Grenze, die niemand gewählt hat. Die Grenzen werden nur strenger, nichts wird konfigurierbar.
```
