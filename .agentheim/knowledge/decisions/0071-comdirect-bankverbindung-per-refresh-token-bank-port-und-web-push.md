---
id: 0071
title: Comdirect-Bankverbindung lebt per Refresh-Token im Server, mit schmalem Bank-Port, vorbereitendem Zeitplan und Web-Push bei Verlust
scope: global
status: accepted
date: 2026-10-30
supersedes: []
superseded_by: []
related_tasks: [banking-import-cx4b7, banking-import-p5mcp, banking-import-tshq9, banking-import-z4h6p, banking-import-cyg94, banking-import-bftq0, banking-import-t4raw, infrastructure-jsys4]
related_adrs: [0010, 0057, 0066, 0068]
related_research: []
---

# ADR 0071: Comdirect-Bankverbindung per Refresh-Token, Bank-Port, Zeitplan und Web-Push

## Context
Jeder Sync verlangt heute eine Push-TAN. Der Grund liegt im Client:
`SyncFlow/State.fs` schickt nach `SyncStarted` immer `InitiateComdirectAuth`, der Server ruft
dann `startAuth`. Ein Refresh-Token wird dekodiert, aber nie benutzt. Roman will nach einer TAN
beliebig viele Syncs ohne weitere TAN, optional zeitgesteuert vorbereitet. Die ING soll später
per FinTS daneben passen; die ING-Anbindung selbst bleibt Non-Goal.

Der Code trägt das heute nicht:
- `isAuthenticated` ist auch bei offener Challenge wahr. Pending und aktiv sind nicht
  unterscheidbar.
- `cancelSync` löscht die Auth-Session, ein Verbindungstest ersetzt sie.
- Der ganze Abruf steckt in `confirmTan`.
- `startNewSession` ersetzt die Sync-Sitzung ohne Prüfung.
- Es gibt keinen Hintergrunddienst und keinen ausgehenden Kanal.

Die API-Fakten sind Community-Wissen, unbelegt ist vor allem: trägt die Session über Stunden, bleibt
die Session-ID nach einem Refresh gültig, klappt ein Refresh in einem neuen Prozess
(comdirect-api-simple Issue #16: 401), und gibt es eine harte Maximaldauer. Drei falsche
TAN-Validierungen sperren den Zugang.

## Decision
**Vorbehalt.** Das ADR gilt unter dem Vorbehalt des Live-Beweises (Punkt 1). Scheitert er, wird das
ADR ersetzt, und Punkte 2 bis 9 werden nicht gebaut.

1. **Erst der Beweis.** Vor Zustandsmodell, UI, Web-Push und Zeitplan zeigt eine Sonde hinter
   `RUN_INTEGRATION_TESTS` mit Romans echter TAN, dass die Session mindestens 2 Stunden nur per
   Refresh trägt und Umsätze abrufbar bleiben. Das Ergebnis steht in der README von
   `banking-import` und im Task-Outcome. `diary/development.md` bleibt eingefroren.

2. **Bankverbindung als reiner Zustandsautomat** mit Uhr als Parameter:
   `Disconnected | ChallengePending | Connected`. Daten und Persistenz nur aus `Connected`.
   - Eine Challenge startet nur eine Handlung von Roman (Sync-Start, Verbindungstest). Hintergrund,
     Zeitplan und Neustart starten nie eine und melden sich nie per Passwort neu an.
   - `cancelSync` verwirft nur `ChallengePending`. `Connected` bleibt.
   - Ein Verbindungstest bei `Connected` liest die Konten ohne Challenge.
   - Gescheiterte TAN und geänderte Zugangsdaten führen nach `Disconnected`.

3. **Erneuern.** Der Refresh läuft etwa 60 s vor Ablauf des Access-Tokens, serialisiert per Semaphor
   gegen jeden Bankzugriff, weil ein zweiter Refresh mit demselben Token die Verbindung fälschlich
   beenden würde. Das Ergebnis ist eines von zwei Urteilen:
   - **Neu anmelden nötig** (400, 401, unbekanntes 4xx): Token löschen, `Disconnected`, Verlust melden.
   - **Später nochmal** (Status 0, 429, 5xx, nicht lesbare Antwort): `Connected` bleibt, nächster
     Versuch nach 60 s.

   Antwortet ein Datenabruf mit 401 oder 403, folgen ein Refresh und ein Wiederholversuch.
   Fehlertexte tragen Status und `error`-Code, nie den Antwortrumpf.

4. **Wiederaufnahme.** Tokens, `RequestInfo`, `SessionIdentifier` und Ablaufzeit liegen als ein
   verschlüsselter JSON-Wert unter einem Settings-Schlüssel, über `Persistence.Encryption`.
   - Geschrieben wird nach jedem Refresh, bevor das neue Token benutzt wird.
   - Ohne gesetzten `BUDGETBUDDY_ENCRYPTION_KEY` wird nichts gespeichert, die Verbindung lebt dann
     nur im Speicher. Der Maschinen-Fallback gilt für diesen Wert nicht.
   - Beim Start ein Versuch. Scheitert er, wird der Wert gelöscht, der nächste Sync verlangt die TAN.
   - Absturz zwischen Antwort und Speichern kostet eine TAN. Das bleibt so.

5. **Bank-Port** als Record aus Funktionswerten: Status, Auth starten, Auth abschließen, Konten,
   Umsätze. Nur Comdirect implementiert ihn, die `Comdirect*`-Typen bleiben. Heute trägt er die
   Testnaht für den Abruf, später die ING. `TransactionId` und `import_id` für Comdirect bleiben
   exakt gleich; neue Quellen bekommen ein eigenes Präfix.

6. **Der Server entscheidet über die TAN.** `startSync` liefert `SyncStart = TanRequired of
   SyncSession | ReviewReady of SyncSession`, `initiateComdirectAuth` entfällt.
   - Bei `Connected` läuft der Abruf in diesem Aufruf, wie heute in `confirmTan`.
   - Stirbt die Verbindung dabei, startet der Server die Challenge. Roman hat den Sync ausgelöst.
   - Eine offene Prüfung (`ReviewingTransactions`) gibt `startSync` unverändert zurück.
   - Der Abruf liegt als `fetchForReview` an einer Stelle und nutzt den Bank-Port.

7. **Vorbereiten nach Zeitplan.** Setting an/aus plus Intervall, Default aus.
   - Der Lauf baut das Ergebnis außerhalb der Sitzung und installiert es am Ende atomar, nur wenn
     keine Sitzung außer `Completed` oder `Failed` besteht. Sonst verfällt das Ergebnis.
   - Ohne `Connected` überspringt er, ohne verwertbare Buchung legt er keine Sitzung an, ohne
     YNAB-Abgleich ebenfalls. Er bucht nichts.
   - Eine vorbereitete Prüfung lebt im Speicher wie jede Sitzung. Nach einem Neustart erzeugt der
     nächste Lauf sie neu; verloren geht nichts Gebuchtes.

8. **Hintergrunddienst.** Ein `BackgroundService` mit `PeriodicTimer`, registriert nur in
   `Program.fs`, ruft `tick ports now` auf. Im TestServer (`Api.webApp`) läuft er damit nie. Ein
   Strukturwächter mit Gegenbeweis hält das fest, neue Netzmodule kommen in den Netzwächter.

9. **Web-Push-Kanal in `infrastructure`**, mit einer Schnittstelle
   `notify: PushMessage -> Async<unit>` (`Title`, `Body`, `Tag`).
   - Handler per `workbox.importScripts` aus `public/`, `generateSW` bleibt, ADR 0010 und 0057
     bleiben unberührt. Die Datei geht mit `Cache-Control: no-cache` hinaus.
   - VAPID-Schlüssel entstehen beim ersten Bedarf und liegen verschlüsselt in SQLite. Ein
     Entschlüsselungsfehler erzeugt keine neuen, das würde alle Abos entwerten. Nur der öffentliche
     Schlüssel geht an den Client.
   - Abos in SQLite, 404 und 410 löschen das Abo, Push-Hosts über Allowlist und nur HTTPS.
   - Der Text ist fest, ohne Beträge und Bankdaten. Ein gescheiterter Push ändert nichts an der
     Bankverbindung. Die Bot-API bleibt unverändert.

## Consequences
### Positive
- Nach einer TAN laufen Syncs ohne TAN. Eine Challenge ohne Handlung von Roman ist ausgeschlossen,
  das schont die Sperre nach drei Fehlversuchen.
- Der Abruf ist ohne Bank testbar.

### Negative
- **Die TAN schützt nur noch den Start.** Solange die Verbindung lebt, löst jeder mit `/api`-Zugang
  Bankabrufe ohne Handy aus. Das gilt für das Tailnet und für den Proxy `bb-bot` im Docker-Netz, der
  `budgetbuddy-app:5081` direkt erreicht. Wer dort sitzt, liest schon heute das Kassenbuch; neu ist
  der Zugriff auf frische Umsätze. "Bank trennen" löscht die Verbindung lokal, ein Höchstalter gibt
  es nicht.
- **Token auf der Platte.** Datenbank und Backups enthalten ein Token, der Schlüssel liegt auf
  demselben Host. Solange das Token gilt, reicht beides zusammen für einen Abruf.
  `Persistence.Encryption` ist CBC ohne Authentifizierung; das bleibt, ein zweiter Kryptoweg
  wäre für einen Wert zu viel.
- Ein Deploy, der länger dauert als das Refresh-Token gilt, kostet eine TAN.
- Eine liegen gebliebene Prüfung hält den Zeitplan an, bis Roman sie abschließt oder abbricht.
- Ob eine Anmeldung aus der Dev-Umgebung die Produktionsverbindung beendet, ist unbekannt.
- Web-Push hängt von Geräten ab. iOS-Eignung und Empfang ohne Tailscale sind aus Wissen, nicht
  belegt. Sie werden am Anfang des Kanal-Tasks auf echten Geräten geprüft. Der Klick auf die
  Nachricht braucht Tailscale am Handy, um Daten zu sehen.

### Neutral
- `SyncStart` ändert einen Remoting-Vertrag. Fixtures in `scripts/*.test.mjs` ziehen im selben
  Schritt mit, alte Clients holt ADR 0057 ab.
- `banking-import`: die offene Frage nach dem Bank-Source-Port ist beantwortet. Die Frage nach dem
  ING-Adapter bleibt offen.

## Alternatives considered
- **Neuer Endpunkt "Status prüfen" statt `SyncStart`.** Verworfen: die Entscheidung bliebe im Client.
- **Abruf im Hintergrund, Client fragt den Status ab.** Verworfen: `confirmTan` läuft heute schon als
  langer Aufruf, das reicht.
- **Vorbereitete Prüfung in SQLite.** Verworfen: `sync_transactions` wird nirgends gelesen, das
  brächte eine zweite Baustelle für einen Zustand, den der nächste Lauf neu erzeugt.
- **Fehlversuchszähler mit Zustand `Locked`.** Verworfen: ein Fehlversuch braucht jetzt Romans
  Handlung, und TANs werden seltener.
- **AES-GCM für das Token.** Verworfen: Owner-Entscheidung für `Persistence.Encryption`, siehe
  Negative. Die Schutzlücke des Maschinen-Fallbacks schließt Punkt 4.
- **Eigene Tabelle für die Session.** Verworfen: ein verschlüsselter JSON-Wert schreibt ebenso
  atomar und braucht kein Schema.
- **Höchstalter, das eine TAN erzwingt.** Nicht gebaut, weil es dem Ziel "beliebig viele Syncs"
  widerspricht.
- **`injectManifest` für den Push-Handler.** Verworfen: Precache und Denylist müssten nachgebaut
  werden.

## References
- comdirect REST-API (Community-Fakten); comdirect-api-simple Issue #16; Rust-Library
  `comdirect_rest_api` (`try_restore`)
- ADR 0010 (PWA nur Shell), ADR 0057 (PWA-Update), ADR 0066 und 0068 (Bob)

## Nachtrag 2026-10-31 (banking-import-z4h6p)

Punkt 4 gilt mit zwei Präzisierungen. "Scheitert er, wird der Wert gelöscht" gilt für `ReloginRequired` und einen unbrauchbaren Wert. Bei `RetryLater` bleiben Wert und `Connected` stehen, der nächste Versuch folgt nach 60 s. "Beim Start ein Versuch" gilt für den Hintergrunddienst. Zusätzlich warten `startSync` und der Verbindungstest bei `Disconnected` auf die Wiederaufnahme, bevor sie über die TAN entscheiden, damit ein Klick direkt nach dem Start keine TAN kostet. Eine Challenge entsteht dabei weiter nur aus Romans Handlung.
