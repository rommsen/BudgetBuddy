---
id: banking-import-t4raw
title: "Zeitgesteuertes Vorbereiten zur Prüfung, per Setting an und aus, Default aus"
status: backlog
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-bftq0, banking-import-cyg94]
blocks: []
tags: [etappe-3, backend, frontend, sync, zeitplan]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Roman will Umsätze zur Prüfung bereitliegen haben, ohne einen Sync anzustoßen. Der Lauf bucht nichts
und lässt eine offene Sitzung unberührt (ADR 0071 Punkt 7).

## What
- Setting an/aus und Intervall in `SyncSettings`, Default aus, Voreinstellung 360 Minuten, erlaubt 60
  bis 1440. Validierung in `src/Server/Validation.fs`.
- Ein eigener `BackgroundService`, registriert nur in `Program.fs`, löst `fetchForReview` aus. Der Lauf
  hängt nicht in `tick`.
- `tryInstallPrepared` in `SyncSessionManager`, atomar.
- UI-Felder in den Einstellungen, `StartedAt` als "vorbereitet am" in der Prüfung, in Berliner Zeit.
- Neue Pflichtfelder in `SyncSettings` brechen Fixtures in `scripts/*.test.mjs`: im selben Schritt nachziehen.

**Fälligkeit:** Die Zeit des letzten Laufs lebt im Speicher. Nach einem Neustart ist der Lauf sofort
fällig, weil die vorbereitete Prüfung mit dem Prozess weg ist. Ein Lauf zählt, sobald er die Bank
anfragt, auch wenn er scheitert. Übersprungen wegen `Disconnected` oder einer offenen Sitzung zählt
nicht, der Lauf bleibt fällig.

## Acceptance criteria
- [ ] GIVEN aus WHEN Takt THEN kein Bankzugriff.
- [ ] GIVEN an, Intervall fällig, `Connected`, keine Sitzung THEN eine Sitzung mit `ReviewingTransactions`.
- [ ] GIVEN an und das Intervall ist nicht fällig THEN kein Bankzugriff.
- [ ] GIVEN eine Sitzung mit Status außer `Completed` oder `Failed` THEN sie bleibt unberührt, kein Abruf.
- [ ] GIVEN ein manueller Start während des Laufs THEN das Ergebnis des Laufs wird verworfen, die manuelle Sitzung bleibt.
- [ ] GIVEN eine manuelle Sitzung, die während des Laufs beginnt und schon `Completed` ist THEN das Ergebnis des Laufs wird verworfen.
- [ ] GIVEN `Disconnected` THEN keine Challenge, kein Abruf, der Lauf bleibt fällig.
- [ ] GIVEN die Verbindung stirbt im Lauf THEN keine Challenge, keine Sitzung.
- [ ] GIVEN ein Bankfehler bei lebender Verbindung THEN keine Sitzung, Log-Eintrag, der nächste Versuch folgt nach dem Intervall.
- [ ] GIVEN alle Buchungen übersprungen THEN keine Sitzung.
- [ ] GIVEN der YNAB-Abgleich ist nicht möglich THEN keine Sitzung, Log-Eintrag.
- [ ] GIVEN der Lauf ist fertig THEN kein Ereignis im Kassenbuch und kein Aufruf an YNAB außer Lesen.
- [ ] GIVEN 59 oder 1441 Minuten WHEN `saveSyncSettings` THEN Ablehnung mit konkreter Meldung, 60 und 1440 gehen durch, `src/Tests/RemotingEndpointNegativeTests.fs` hat die zugehörige Zeile.
- [ ] Ein Strukturwächter mit Gegenbeweis hält fest, dass der neue Dienst nur in `Program.fs` registriert ist, also im TestServer nie läuft (ADR 0071 Punkt 8).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Einstellungen sind am Handy und am PC geprüft. [human-eye]

## Notes
**Warum ein eigener Dienst:** `tick` liegt in `ComdirectAuthSession.fs` vor `BankImport.fs` und kann
`fetchForReview` nicht aufrufen. Der Worker wartet außerdem jeden Takt ab, ein Lauf im selben Takt hielte
den 15-s-Takt des Refresh auf. Der neue Dienst bekommt seine Funktion als Parameter wie
`BankConnectionWorker`, die Live-Anschlüsse liegen in `liveImportPorts` in `Api.fs`.

**`tryInstallPrepared`:** Der Lauf merkt sich beim Start die aktuelle Sitzung und installiert nur, wenn
sie noch dieselbe ist und ihr Status `Completed`, `Failed` oder keine Sitzung lautet. Der Status allein
reicht nicht, eine in der Zwischenzeit abgeschlossene Sitzung sähe sonst harmlos aus. Der Lauf hält
`ImportPorts.Gate` nicht, sonst wartete ein manueller Start auf ihn.

**YNAB-Abgleich:** `fetchForReview` läuft bei `LoadYnabTransactions = None` weiter, für den manuellen
Sync ist das gewollt. Der Lauf braucht hier eine Unterscheidung, damit keine Sitzung ohne Abgleich entsteht.

Die Challenge bei Verbindungsverlust steckt in `startSyncWith`, nicht in `fetchForReview`. Der Lauf ruft
`fetchForReview` direkt. `Today` kommt als Port, `infrastructure-p747v` stellt ihn auf den Berliner Tag um.

Das Intervall 60 bis 1440 ist Romans Entscheidung vom 2026-11-01. Eine liegen gebliebene Prüfung hält den
Zeitplan an, bis Roman sie abschließt oder abbricht. `banking-import-p5mcp` zeigte bis 3 h 07 min keine
Maximaldauer, darüber ist sie unbekannt. Endet die Session vorher, ist das ein Verlust, und der Lauf
überspringt.

Design-System: nichts Neues.
