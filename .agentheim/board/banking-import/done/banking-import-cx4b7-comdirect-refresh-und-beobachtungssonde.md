---
id: banking-import-cx4b7
title: "Comdirect-Refresh im Client und Beobachtungssonde hinter RUN_INTEGRATION_TESTS"
status: done
type: spike
context: banking-import
created: 2026-10-30
completed:
depends_on: []
blocks: [banking-import-p5mcp]
tags: [backend, comdirect, session, refresh-token]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Alle Annahmen zum Refresh sind Community-Wissen: Laufzeit, Rotation, Session-ID nach Refresh,
Wiederaufnahme in einem neuen Prozess. Vor jedem Umbau muss eine Sonde bereitstehen, die Roman
mit einer echten TAN starten kann (ADR 0071 Punkt 1). Der Refresh selbst ist produktiver Code.

## What
- `refreshTokens` in `src/Server/ComdirectClient.fs` mit eingespeistem `HttpClient` und dem Urteil
  `ReloginRequired | RetryLater`. `expires_in` decodieren, Ablaufzeit im Token-Typ.
- Token-Anfragen per `FormUrlEncodedContent` statt `sprintf`.
- Fehlertexte tragen Status und `error`-Code, nie den Antwortrumpf, auch in
  `comdirectErrorToString`. `printfn` mit Antwortrumpf in `getAccounts` entfernen.
- `getAccounts` und `getSessionIdentifier` melden 401 und 403 korrekt (heute 404 bzw. NetworkError).
- Sonde als Testfall in einer Liste "(requires .env)", hinter `RUN_INTEGRATION_TESTS` und einem
  eigenen Schalter `COMDIRECT_PROBE`, damit ein normaler Integrationslauf keine TAN auslöst.

Vorgaben für die Sonde:
- Bestätigt die TAN genau einmal nach Romans Signal (Sentinel-Datei). Kein Polling, weil jede
  Fehlvalidierung auf die Sperre nach drei Versuchen einzahlt.
- Refresh etwa 60 s vor Ablauf, alle 30 Minuten Konten und Umsätze mit unveränderter `RequestInfo`.
- Protokolldatei unter `DATA_DIR` nur mit Zeit, Status und `expires_in`. Nie Tokens.
- Phase 2: ein neuer Prozess setzt aus einer verschlüsselten Datei fort.
- Phase 3: Refresh aussetzen, bis er abgelehnt wird. Das liefert echte Laufzeit und Fehlerform.

**Stop-Loss:** if, mid-spike, the mitigation is already known and cheap, record it and stop.

## Acceptance criteria
- [ ] GIVEN der Refresh antwortet 200 WHEN `refreshTokens` THEN neues Access-Token, neues Refresh-Token und Ablaufzeit liegen vor.
- [ ] GIVEN 400 oder 401 THEN Urteil `ReloginRequired`. GIVEN Status 0, 429, 503 oder nicht lesbarer Rumpf THEN Urteil `RetryLater`.
- [ ] GIVEN ein Fehlerrumpf mit Token-Text THEN weder Meldung noch Log enthalten ihn.
- [ ] Kaputt: GIVEN Passwort oder Secret mit `&` oder `+` WHEN Token-Anfrage THEN das Formular ist korrekt kodiert (Test vor dem Fix rot). Intakt: alphanumerische Werte bleiben unverändert.
- [ ] GIVEN ein Lauf mit `RUN_INTEGRATION_TESTS=true` ohne `COMDIRECT_PROBE` THEN keine TAN-Anfrage.
- [ ] `src/Tests/NetworkCallGuardTests.fs` ist grün.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Die Live-Beobachtung selbst ist `banking-import-p5mcp`. Dieser Task startet keine echte TAN.
- Ungeprüft: ob `dotnet test` einen Lauf über Stunden zulässt. Falls nicht, ist eine kleine
  Konsole die Ersatzlösung.
- Ob comdirect die `000000`-Bestätigung bei zu frühem Klick als Fehlversuch zählt, ist unbekannt.
  Die Sonde vermeidet es.

## Outcome

Der Refresh ist produktiver Code, die Sonde ist startbereit. `./verify.sh` zeigt `VERIFY: PASS`.

**Nicht live gelaufen:** Es gab keinen Aufruf gegen Comdirect und keine TAN. Refresh, Formular und Sonde sind nur gegen Fake-Handler und Fake-Ports geprüft. Die Form der echten Refresh-Antwort, die `grant_type=refresh_token`-Anfrage gegen die echte API, die Frist von 5 Minuten für das Sentinel-Signal und die Dauer von `dotnet test` über Stunden sind damit unbelegt. Das klärt `banking-import-p5mcp`.

**Client (`src/Server/ComdirectClient.fs`)**
- `refreshTokens client apiKeys now tokens` liefert `Tokens` oder das Urteil `ReloginRequired | RetryLater` (`RefreshFailure`). Eine 200-Antwort ohne `expires_in` zählt als nicht lesbar, also `RetryLater`.
- `Tokens.ExpiresAt` ist `DateTimeOffset option`. Im Login-Pfad bleibt `expires_in` optional, damit das bisherige Decoder-Verhalten gilt und ein fehlendes Feld nach der TAN nicht scheitert.
- Token-Anfragen laufen über `FormUrlEncodedContent`. Der Bug mit `&` und `+` hatte einen roten Test vor dem Fix, alphanumerische Werte ergeben dieselbe Anfrage wie zuvor.
- Fehlertexte tragen nur Status und Code. Über den Auftrag hinaus war auch die Thoth-Meldung betroffen: sie zitiert den gelesenen Wert, bei einer 200-Antwort also den Rumpf. `InvalidResponse` hat deshalb festen Text, Ausnahmen geben nur den Typnamen weiter, ein `error`-Wert mit Ziffern gilt nicht als Code.
- `getAccounts` und `getSessionIdentifier` melden 401 und 403 korrekt, die `printfn`-Ausgaben in `getAccounts` sind entfernt. Dafür gibt es die eingespeisten Varianten `initOAuthWith`, `getExtendedTokensWith`, `getSessionIdentifierWith` und `getAccountsWith`.

**Sichtbare Änderung für Roman:** `comdirectErrorToString` (`src/Server/Api.fs`) kann den Meldungstext der Bank nicht mehr zeigen, weil er aus dem Rumpf käme. Bei `TAN_UNGUELTIG` erscheint jetzt der feste Text "Please confirm the TAN in your banking app first." statt des Bank-Texts. `parseComdirectErrorJson` und seine Tests sind entfernt, weil der Rumpf diese Stelle nicht mehr erreicht.

**Sonde (`src/Tests/ComdirectProbe.fs`, Testfall in `src/Tests/ComdirectIntegrationTests.fs`)**
- Start steht im Dateikopf von `ComdirectProbe.fs`. `COMDIRECT_PROBE=1|2|3` zählt nur als Prozess-Variable, nie aus `.env`.
- Phase 1 bestätigt die TAN genau einmal nach der Sentinel-Datei, entfernt vorher eine alte Datei und beobachtet 2 Stunden. Phase 2 setzt aus `session.enc` 35 Minuten fort. Phase 3 macht wachsende Pausen von 5 bis 60 Minuten, bis der Refresh abgelehnt wird.
- Phase 2 und 3 starten nie eine TAN. Phase 1 verweigert den Start, wenn `session.enc` liegt oder `BUDGETBUDDY_ENCRYPTION_KEY` fehlt.
- Das Protokoll liegt unter `<DATA_DIR>/comdirect-probe/probe.log` und enthält nur Zeit, Ereignisname, Status und `expires_in`.

**Keine TAN ohne `COMDIRECT_PROBE`:** Der Bestand verletzte dieses Kriterium. Der Testfall "can initiate OAuth flow" löste schon mit `RUN_INTEGRATION_TESTS` allein eine echte TAN aus. Er ist entfernt, die Sonde deckt denselben Weg ab. "handles invalid credentials gracefully" ruft jetzt nur `initOAuth` auf (Schritt 1, keine Challenge möglich) und wird neben der Sonde übersprungen. Den Beweis ohne Netz führen `selectPhase` (`src/Tests/ComdirectProbeTests.fs`) und der neue TAN-Wächter in `src/Tests/NetworkCallGuardTests.fs` samt Gegenbeweis. Der Wächter war auf dem alten Bestand rot.

**`dotnet test` über Stunden:** Keine Konsolen-Ersatzlösung gebaut. `src/Tests/test.runsettings` setzt keine Sitzungsgrenze, und der Fortschritt steht in der Protokolldatei, nicht in der Test-Ausgabe. Das ist aus der Konfiguration abgeleitet, ein langer Lauf ist nicht erprobt.

Punkt 3 von ADR 0071 ist nur teilweise umgesetzt: Semaphor und Wiederholversuch nach 401 oder 403 fehlen, ebenso die Persistenz aus Punkt 4. Die Sonde wiederholt nach einem Datenfehler nichts, sie protokolliert ihn und macht weiter.
