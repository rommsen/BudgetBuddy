---
id: banking-import-cx4b7
title: "Comdirect-Refresh im Client und Beobachtungssonde hinter RUN_INTEGRATION_TESTS"
status: doing
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
