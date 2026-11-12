---
id: banking-import-t4raw
title: "Automatisch abrufen im Intervall: ein Hintergrunddienst bucht neue Umsätze direkt ins Kassenbuch, Vorgabe an, alle 360 Minuten, in den Einstellungen abschaltbar, nie eine Push-TAN"
status: doing
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-d0fey]
blocks: []
tags: [etappe-3, backend, frontend, sync, zeitplan]
related_adrs: [0071, 0093]
related_research: []
prior_art: []
---

## Why
Roman will neue Umsätze im Kassenbuch haben, ohne „Abrufen“ zu drücken. Mit `banking-import-d0fey` bucht ein
Abruf direkt ins Kassenbuch und nach YNAB (ADR 0093). Der Zeitplan ruft denselben Abruf im Hintergrund auf.
Bisher sah ADR 0071 Punkt 7 nur ein Vorbereiten ohne Buchen vor, Vorgabe aus; ADR 0093 ersetzt das
(Roman, 2026-11-11): Vorgabe an, der Lauf bucht.

## What
- Zwei neue Felder in `SyncSettings`: `AutoFetchEnabled` (Vorgabe `true`) und `AutoFetchIntervalMinutes`
  (Vorgabe 360, erlaubt 60 bis 1440). Gespeichert als Zustand in `settings` (ADR 0083 Abschnitt 8), validiert in
  `validateSyncSettings` (`src/Server/Validation.fs`), alle Fehler gesammelt.
- Ein eigener `BackgroundService` (Vorbild `BankConnectionWorker`: Funktion als Parameter, Fehler beenden den
  Dienst nicht, gemeldet wird nur der Typname), registriert nur in `Program.fs`. Er hängt nicht in `tick`:
  `tick` liegt in `ComdirectAuthSession.fs` vor `BankImport.fs`, und ein Abruf im selben Takt hielte den
  15-s-Takt des Refresh auf.
- Der Dienst prüft in einem festen Takt (etwa eine Minute), ob ein Lauf fällig ist, und ruft dann den Abruf aus
  `banking-import-d0fey` auf, denselben Weg wie „Abrufen“ (Regeln, Order-ID, Duplikate, Kassenbuch, YNAB).
- Einstellungen: in der Karte Comdirect ein Schalter „Automatisch abrufen“ (`Input.switchRow`, Erklärsatz
  „BB ruft neue Umsätze im Hintergrund ab und bucht sie als zu prüfen. Eine Push-TAN fragt es nie an.“) und
  darunter das Auswahlfeld „Alle“ mit den Stufen 1, 2, 3, 4, 6, 8, 12 und 24 Stunden (`Input.pickerField` oder
  `Input.select` nach §7.5, fünf und mehr Optionen sind ein Auswahlfeld). Das Feld ist bei abgeschaltetem
  Schalter gesperrt, sein Wert bleibt. Lehnt der Server ab, steht die Meldung als Feldfehler am Auswahlfeld
  (`Input.groupWithError`, Styleguide §5), nicht als Toast.
- Neue Pflichtfelder in `SyncSettings` brechen die Fixtures in `scripts/*.test.mjs` zur Laufzeit
  (`Sync: { DaysToFetch: … }` in `scripts/settings-secrets.test.mjs`, `scripts/ledger-filter-bar.mjs`,
  `scripts/rules-page-mobile.test.mjs`, `scripts/layout-states.mjs` und jede weitere Fundstelle von
  `DaysToFetch` unter `scripts/`): im selben Schritt nachziehen.

**Fälligkeit:** Ein Lauf ist fällig, wenn der Schalter an ist und seit dem jüngsten Abruf mindestens das
Intervall vergangen ist. Der jüngste Abruf steht in der Sync-Historie (`sync_sessions`, abgeschlossen oder mit
Bankfehler gescheitert, automatisch oder von Hand). So setzt ein Abruf von Hand den Zeitplan zurück, und ein
Neustart (Deploy) löst keinen zusätzlichen Lauf aus. Ohne Eintrag in der Historie ist der Lauf sofort fällig.
Ein übersprungener Lauf (`Disconnected`, `ChallengePending`, ein Abruf läuft schon) schreibt nichts in die
Historie und bleibt fällig.

**Erscheinen auf der Übersicht:** Ein automatischer Lauf schreibt wie ein Abruf von Hand einen Eintrag in die
Sync-Historie, auch wenn er keine neue Buchung findet. Die Zeile „Comdirect · Import <Stand>“ auf der Übersicht
(`Overview.Types.importLine`) zeigt damit den jüngsten gelungenen Abruf, egal wer ihn ausgelöst hat. Der
Eintrag trägt, ob er automatisch lief (Feld in `SyncSession`, etwa `Trigger: Manual | Scheduled`); die Zeile
unterscheidet das nicht.

## Acceptance criteria
Szenarien (GIVEN/WHEN/THEN), jedes mit Test auf der Heimat-Ebene (Fälligkeit als reine Funktion mit Uhr als
Parameter; der Lauf als Handler mit Fake-Ports für Bank, YNAB und Historie, In-Memory-SQLite).
- [ ] GIVEN Schalter aus WHEN Takt THEN kein Bankzugriff, kein Eintrag in der Historie.
- [ ] GIVEN Schalter an, jüngster Abruf vor 361 Minuten, Intervall 360, `Connected` WHEN Takt THEN ein Abruf
      läuft, neue Umsätze stehen im Kassenbuch (Ereignisse mit Actor `ComdirectImport`) und gehen nach YNAB, ein
      Eintrag mit `Scheduled` in der Historie.
- [ ] GIVEN Schalter an, jüngster Abruf vor 359 Minuten WHEN Takt THEN kein Bankzugriff.
- [ ] GIVEN Schalter an, keine Historie WHEN Takt THEN ein Abruf läuft.
- [ ] GIVEN `Disconnected` oder `ChallengePending` WHEN ein fälliger Takt THEN keine Challenge, kein
      Passwort-Login, kein Abruf, kein Eintrag in der Historie; der nächste Takt prüft wieder (ADR 0071 Punkt 2,
      Invariante Challenge).
- [ ] GIVEN die Verbindung stirbt im Abruf WHEN der Lauf endet THEN keine Challenge (anders als `startSync`),
      ein Eintrag „gescheitert“ in der Historie, nichts gebucht.
- [ ] GIVEN ein Bankfehler bei lebender Verbindung WHEN der Lauf endet THEN nichts gebucht, ein Eintrag
      „gescheitert“, Log-Eintrag mit Status und Code (Invariante Fehlertexte), der nächste Versuch folgt nach dem
      Intervall.
- [ ] GIVEN der YNAB-Abgleich ist nicht möglich (`LoadYnabTransactions` liefert `None`) WHEN ein fälliger Lauf
      THEN er bucht nichts und schreibt „gescheitert“ mit Grund. Ohne Abgleich fehlt die Duplikatprüfung, ein
      Lauf ohne Roman darf dann nicht buchen. (Von Hand gilt, was `banking-import-d0fey` festlegt.)
- [ ] GIVEN ein Abruf von Hand läuft WHEN ein fälliger Takt THEN der Lauf wartet nicht und überspringt, er bleibt
      fällig; umgekehrt wartet „Abrufen“ auf einen laufenden automatischen Lauf (gemeinsames `ImportPorts.Gate`)
      und findet danach dessen Buchungen schon im Kassenbuch. Keine Reference entsteht zweimal.
- [ ] GIVEN ein Abruf von Hand vor 10 Minuten WHEN Takt THEN kein automatischer Lauf (Fälligkeit aus der
      Historie).
- [ ] GIVEN 59 oder 1441 Minuten WHEN `saveSyncSettings` THEN Ablehnung mit konkreter Meldung („Intervall muss
      zwischen 60 und 1440 Minuten liegen“), 60 und 1440 gehen durch. `src/Tests/RemotingEndpointNegativeTests.fs`
      hat die Zeile für `saveSyncSettings` mit ungültigem Intervall.
- [ ] GIVEN gespeicherte Einstellungen ohne die neuen Schlüssel (Bestand) WHEN Laden THEN an und 360 Minuten.
- [ ] `getSettings` liefert die beiden Felder in `AppSettingsView`, ohne Secrets (`src/Tests/SettingsSecretsTests.fs`
      bleibt grün).
- [ ] Ein Strukturwächter mit Gegenbeweis hält fest, dass der neue Dienst nur in `Program.fs` registriert ist und
      im TestServer (`Api.webApp`) nie läuft (ADR 0071 Punkt 8, `src/Tests/StructureGuardTests.fs`). Jede Stelle,
      die den Live-Port anschließt, ist aufgelistet und belegt (Doktrin Punkt 3).
- [ ] Einstellungen: Schalter und Auswahlfeld speichern über `saveSyncSettings` (Client-`update` unter .NET:
      Schalter aus sperrt das Feld und behält den Wert, Speichern schickt beide Felder).
- [ ] Fixtures in `scripts/*.test.mjs` tragen die neuen Pflichtfelder (Fable.Remoting), `npm run test:scripts`
      grün.
- [ ] Browser-Tests: Die Einstellungen werden nicht umgebaut, es kommt kein `[flow]` dazu. Der Sweep-Zustand der
      Einstellungen (falls vorhanden, sonst neu in `scripts/layout-states.mjs`) deckt Schalter und Feld bei 412
      und 1280 px.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [ ] Roman hat die Einstellungen am Handy (412 px, Touch) und am PC angesehen. [human-eye]

## Notes
Umgeschrieben am 2026-11-11 (Roman, Entscheidung 2): vorher „Zeitgesteuertes Vorbereiten zur Prüfung, Vorgabe
aus“. Mit der Prüfsitzung entfallen `tryInstallPrepared`, das „vorbereitet am“ in der Prüfung und die Regeln zu
einer liegenden Sitzung.

`banking-import-bftq0` (Status der Bankverbindung, Verlustmeldung) und `banking-import-cyg94` (Sync ohne TAN,
Bank-Port) sind erledigt, die Abhängigkeit entfällt. Bei einem Verlust meldet `bftq0` per Web-Push; bis zum
nächsten Abruf von Hand mit TAN überspringt der Zeitplan still.

Die Challenge bei Verbindungsverlust steckt in `startSyncWith`, nicht im Abruf selbst. Der Lauf ruft den Abruf
direkt, nie `startSyncWith`. `Today` kommt als Port (Berliner Tag).

Live-Befund `banking-import-p5mcp`: 3 h 07 min Trageweite nur per Refresh, darüber unbekannt. Endet die Session,
ist das ein Verlust, und der Zeitplan überspringt, bis Roman „Abrufen“ mit TAN drückt.

Stufen statt freier Minuten im Auswahlfeld: am Handy ohne Tastatur bedienbar. Der Server nimmt jeden Wert von 60
bis 1440 an, Roman 2026-11-11: Stufen 1, 2, 3, 4, 6, 8, 12 und 24 Stunden reichen.

Design-System: nichts Neues, `Input.switchRow` und ein Auswahlfeld gibt es.
