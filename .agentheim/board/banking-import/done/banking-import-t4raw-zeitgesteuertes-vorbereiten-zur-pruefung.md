---
id: banking-import-t4raw
title: "Automatisch abrufen im Intervall: ein Hintergrunddienst bucht neue Umsätze direkt ins Kassenbuch, Vorgabe an, alle 360 Minuten, in den Einstellungen abschaltbar, nie eine Push-TAN"
status: done
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

## Outcome

Automatisch abrufen läuft (ADR 0093 Punkt 9). Der automatische Abruf nimmt denselben Weg wie „Abrufen“, bucht direkt ins Kassenbuch und nach YNAB und startet nie eine Challenge oder einen Passwort-Login.

**Server**
- `src/Shared/Domain.fs`: `SyncTrigger` kennt jetzt `Scheduled`. `SyncSettings` hat die neuen Felder `AutoFetchEnabled` und `AutoFetchIntervalMinutes`, die Vorgaben heißen `defaultAutoFetchEnabled` (an) und `defaultAutoFetchIntervalMinutes` (360).
- `src/Server/BankImport.fs`:
  - `isAutoFetchDue` ist rein und rechnet mit der Uhr als Parameter. Fällig ist ein Lauf bei Schalter an, wenn seit dem jüngsten Abruf mindestens das Intervall vergangen ist, ohne Historie sofort.
  - `runAutoFetchWith` bekommt `AutoFetchPorts` (Import, Zeitplan, jüngster Abruf, Log) und liefert `AutoFetchOutcome` (Off, NotDue, Skipped FetchRunning/BankNotConnected/NotConfigured, Ran).
  - Der Lauf nimmt das Gate nur mit `Wait 0`, wartet also nicht. Er ruft `fetchAndBook` und schreibt die eigene Sitzung (`Trigger = Scheduled`) nur in die Historie. Die Sitzung im Speicher (TAN, „Abrufen“) bleibt unberührt.
  - Ein Fehler endet `Failed` mit dem Satz aus `describeFailure`, der aus `failFetch` herausgezogen ist. Bei Bankfehlern kommt eine Log-Zeile mit Status und Code dazu (`bankErrorLogDetail`).
- `src/Server/AutoFetchWorker.fs` ist neu: ein `BackgroundService` mit Minutentakt (`tickPeriod`). Ein Fehler beendet den Dienst nicht, gemeldet wird nur der Typname.
- Registriert ist der Dienst nur in `src/Server/Program.fs` mit `Server.Api.runAutoFetch`.
- `src/Server/Api.fs`:
  - `liveAutoFetchPorts` und `runAutoFetch` sind neu.
  - `loadAppSettingsFrom` liest `sync_auto_fetch_enabled` und `sync_auto_fetch_interval_minutes`. Fehlen die Schlüssel (Bestand), gelten an und 360.
  - `saveSyncSettingsWith` ist aus dem Endpoint herausgezogen und schreibt alle drei Schlüssel.
- `src/Server/Persistence.fs`: Der Auslöser `Scheduled` wird gespeichert und gelesen. `latestFinishedStartedAt` liefert den Beginn des jüngsten beendeten oder gescheiterten Abrufs in UTC (`AssumeUniversal`/`AdjustToUniversal`; ohne das käme Ortszeit zurück, Mutant geprüft).
- `src/Server/Validation.fs` meldet „Intervall muss zwischen 60 und 1440 Minuten liegen“ und sammelt die Fehler.

**Client**
- In der Karte Comdirect, unter den Aktionen der Verbindung, stehen `Input.switchRow` „Automatisch abrufen“ mit dem Erklärsatz aus der Task und darunter das Auswahlfeld „Alle“ (`Input.select` nach §7.5, Stufen 1, 2, 3, 4, 6, 8, 12 und 24 Stunden).
- Beide speichern sofort über `saveSyncSettings` (`saveAutoFetchCmd`, Aufruf hinter einem Lambda) und sind gesperrt, solange gespeichert wird. Bei Schalter aus ist das Feld gesperrt, sein Wert bleibt (`autoFetchIntervalLocked`).
- Lehnt der Server ab, steht die Meldung über `Input.groupWithError` am Feld, ohne Toast. Ein Erfolg meldet „Automatisch abrufen gespeichert“.
- Die Karte Sync-Einstellungen schickt Schalter und Intervall so mit, wie sie gespeichert sind (`syncDaysSettingsToSave`).
- Abweichung von der Task-Skizze in Worten: Der Block steht unter den Knöpfen, nicht direkt unter dem Status. Direkt unter dem Status verschob er „Bank trennen“, der Flow `[flow:bank-trennen]` wurde damit rot, weil der Knopf beim Klick wanderte und die Rückfrage nach 3 s zurückfiel.

**Tests (Heimat-Ebene)**
- `src/Tests/BankImportTests.fs`:
  - Fälligkeit rein: 359, 360 und 361 Minuten, keine Historie, aus.
  - Der Lauf mit Fake-Ports und In-Memory-Kassenbuch deckt ab: Schalter aus; fällig mit Buchung (Actor ComdirectImport, Push, Eintrag Scheduled, Sitzung im Speicher unberührt); 359 Minuten; von Hand vor 10 Minuten; keine Historie; Disconnected und ChallengePending (keine Challenge, kein Login, kein Eintrag, der nächste Takt ruft ab); Verbindung stirbt im Abruf; Bankfehler mit Log „HTTP 503, Code …“; YNAB nicht erreichbar; Gate belegt (übersprungen, danach fällig); „Abrufen“ wartet auf einen laufenden Lauf (ein Push, keine Reference zweimal).
  - Dazu zwei Tests für den Dienst und `latestFinishedStartedAt` auf der In-Memory-SQLite.
- `src/Tests/SettingsSecretsTests.fs`: Bestand ohne Schlüssel lädt an und 360; nach dem Speichern liefert `getSettings` die Werte zurück, ohne Secrets; ein abgelehntes Intervall schreibt nichts.
- `src/Tests/ValidationTests.fs`: 59 und 1441 Minuten abgelehnt, 60 und 1440 angenommen, Fehler gesammelt.
- `src/Tests/RemotingEndpointNegativeTests.fs`: Die eine Zeile für `saveSyncSettings` prüft jetzt Tage und Intervall mit konkreter Meldung. Die Tabelle verlangt genau eine Zeile je Endpoint.
- `src/Tests/ClientCommandTests.fs`: Client-`update` unter .NET (Schalter aus sperrt und behält den Wert, Speichern schickt beide Felder, Feldfehler ohne Toast, gesperrt während des Speicherns, Sync-Karte, Stufen).
- `src/Tests/StructureGuardTests.fs`:
  - `isAutoFetchWorkerWired` mit Gegenbeweis (leerer Takt, anderer Abstand, Kommentar).
  - Neue `apiWirings` für `saveSyncSettings`, `liveAutoFetchPorts` und `runAutoFetch`, mit Gegenbeweisen.
  - Belegt sind damit alle Live-Anschlüsse: `Program.fs`, `liveAutoFetchPorts` (Import, Zeitplan aus `loadAppSettings`, `latestFinishedStartedAt`) und `runAutoFetch`.
- `AutoFetchWorker` steht in der Modulliste von `NetworkCallGuardTests.fs`.
- Die Fixtures in `scripts/settings-secrets.test.mjs`, `scripts/rules-page-mobile.test.mjs`, `scripts/ledger-filter-bar.mjs` und `scripts/layout-states.mjs` tragen die neuen Felder.
- Neuer Sweep-Zustand `einstellungen` (PAGE_WIDTHS, also auch 412 und 1280) mit Comdirect eingerichtet.
  - Er zeigte, dass der Link „Styleguide ansehen“ am Handy keine 48 px Trefferfläche hatte. Behoben mit `min-h-[48px] md:min-h-0` statt einer Ausnahme.
- `src/Tests/LedgerPageTests.fs` „Ohne Konto-Filter …“ fragt jetzt mit großer Seite. Grund: Die geteilte In-Memory-DB bekommt parallel jüngere Buchungen aus dem Bank-Import, und Konto B fiel je nach Reihenfolge von der 100er-Seite. Im Gate-Lauf war der Test damit rot.
- `./verify.sh`: VERIFY: PASS (dotnet 4686 grün, node 313 grün).

**Für Roman (human-eye)**: BB-Aufnahmen der Karte bei 412 und 1280 px, Schalter an und aus:
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/banking-import-t4raw/bb-einstellungen-handy-412-an.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/banking-import-t4raw/bb-einstellungen-handy-412-aus.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/banking-import-t4raw/bb-einstellungen-pc-1280-an.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/banking-import-t4raw/bb-einstellungen-pc-1280-aus.png

Das Kriterium „Roman hat die Einstellungen am Handy und am PC angesehen“ ist offen.

**Für die Kann-Sammlung banking-import**
- Die Fälligkeit zählt jeden gescheiterten Abruf in `sync_sessions`, auch eine von Hand abgelehnte TAN. `sync_sessions` unterscheidet „Bankfehler“ nicht von anderen Fehlern. Folge: höchstens ein später automatischer Lauf.
- Zwischen „Bank trennen“ und dem Block „Automatisch abrufen“ steht keine Trennlinie.
- Das Auswahlfeld „Alle“ ist über `Input.group` nicht per `for` mit seiner Bezeichnung verbunden (gilt für alle `Input.group`-Felder).
- Der Flow `[flow:bank-trennen]` reagiert empfindlich auf Layout unter dem Status. Fällt die Rückfrage nach 3 s zurück, während der Knopf wandert, wird der Test rot.

### Nachtrag Review-Lauf 2026-11-12
Behoben (424d33c9): Eine Ablehnung beim Speichern setzt Schalter und Intervall auf den gespeicherten Stand zurück, die Meldung steht am auslösenden Element; „Einstellungen speichern“ der Sync-Karte ist gesperrt, solange „Automatisch abrufen“ speichert; ein gespeichertes Intervall außerhalb 60 bis 1440 wird beim Laden auf die Grenze gezogen, ein unlesbares gilt als 360 (je mit Log-Zeile); deutscher Text in `validateSyncSettings`; Aufzählung der Speicher-Reihenfolgen. Dazu (acc3ba6e): Abbruch-Token im `AutoFetchWorker`, eine Log-Zeile je Bankfehler, `Log`-Port im Strukturwächter mit Mutante belegt.
