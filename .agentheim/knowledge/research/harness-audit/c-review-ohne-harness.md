# Review von acht Commits ohne Projektregeln

Datum: 2026-10-29. Geprüfte Commits: a20fef6, ae7cfb5, 92ac090, cfb05ff, 0f39ac0, a0a8cab, b4b5cc3, 897b13c.

## Vorbemerkung zur Methode

Gelesen wurden die Diffs (`git show`), die Commit-Messages und der umgebende Code unter `src/` und `scripts/` (vor allem `src/Server/BotApi.fs` ganz, `Validation.fs` ab Zeile 150, `ServerConfig.fs`, `Program.fs`, `Api.fs` um `webApp`, `Ledger/CommandHandlers.fs` um `addCashExpense`, `Ledger/Persistence.fs` um `loadBobBookedAmountsBetween`, die Testdateien `BotApiTests.fs`, `BotValidationTests.fs`, `CorsTests.fs`, `ButtonSizeTests.fs` und die beiden Node-Wächter). Projektdoku, ADRs, Standards, Diary und Backlog wurden nicht geöffnet. Tests wurden nicht ausgeführt.

Das Review ist nicht völlig harness-frei: Die Laufumgebung blendet die globale und die projektweite CLAUDE.md automatisch in den Kontext ein. Ihr Inhalt wurde bewusst nicht als Maßstab verwendet, ganz ausschließen lässt sich ein Einfluss aber nicht. Kommentare im Code verweisen auf ADR-Nummern; deren Inhalt kenne ich nur aus diesen Kommentaren.

Zeilenangaben beziehen sich auf den Stand von `main` (ed3a6ab).

Kennzeichnung: **bestätigt** heißt im Code nachvollzogen, **Verdacht** heißt plausibel, aber nicht bis zum Ende geprüft oder abhängig von Verhalten außerhalb des Repos.

## Befunde

### B1. Drosselung vor der Anmeldung ist global und sperrt Bob aus (mittel, bestätigt)

- Commit: 92ac090
- Ort: `src/Server/BotApi.fs:404-407`, `BotRateLimiter` in `:112-127`
- Beschreibung: Der Limiter hat genau einen Zähler für alle Aufrufer und läuft vor der Token-Prüfung. Fehlversuche ohne gültiges Token zählen mit, abgewiesene Anfragen nicht. Der Test `BotApiTests.fs:138` sichert genau dieses Verhalten ab.
- Angriffsszenario: Wer `/bot/` über den Proxy erreicht (laut Kommentar in `BotApi.fs:388` leitet Caddy `^/bot` weiter), schickt zehn Anfragen pro Minute ohne Token. Bob bekommt danach dauerhaft 429, ohne dass der Angreifer etwas über das Token wissen muss. Für Brute-Force schützt die Drossel praktisch nichts, weil ein 256-Bit-Token ohnehin nicht zu raten ist; sie erzeugt also vor allem einen billigen Aussperr-Hebel.
- Vorschlag: Zwei Zähler führen, einen für fehlgeschlagene Anmeldungen je Client-Adresse (aus `X-Forwarded-For` des vertrauten Proxys) und einen für angemeldete Anfragen, sodass Fremde den Zähler des Tokens nicht leeren können.

### B2. Ungültiger Wert für BOT_MAX_AMOUNT_EUR fällt auf das Maximum zurück (niedrig, bestätigt)

- Commit: a0a8cab
- Ort: `src/Server/ServerConfig.fs:53-56`
- Beschreibung: Nicht lesbare Werte (`"50,00"`, `"1,000"`, `"abc"`) ergeben die Vorgabe 500 EUR, die zugleich der harte Deckel ist. Wer absenken wollte und sich vertippt, bekommt den höchsten erlaubten Wert. Das ist fail-open in Richtung der größten Befugnis. Der Startlog nennt es (`:68-70`), aber nur als Logzeile.
- Fehlerfall: `BOT_MAX_AMOUNT_EUR=50,00` in `.env` gesetzt, Bob darf 500 EUR je Buchung statt 50.
- Vorschlag: Bei gesetztem, aber ungültigem Wert den Start abbrechen (wie bei fehlender Zeitzone in `Program.fs:36-40`) oder die Schnittstelle abschalten.

### B3. Commit meldet `entry_id: null`, obwohl die Buchung im Kassenbuch steht (niedrig, bestätigt)

- Commit: b4b5cc3
- Ort: `src/Server/BotApi.fs:334-339`, Ursache in `src/Server/Ledger/CommandHandlers.fs:684-717`
- Beschreibung: `addCashExpense` schreibt `CashExpenseAdded` vor dem Push und gibt bei Push-Fehler nur `Error err` zurück, ohne die `EntryId`. Die Bot-Antwort sagt dann `status: "booked"`, `entry_id: null`, `pushed_to_ynab: false`. Bob kann die Buchung nicht referenzieren.
- Fehlerfall: YNAB hat eine Störung. Bob sieht "booked" ohne Id und mit Fehlertext, legt eine neue Vorschau an und bucht erneut. Im Kassenbuch stehen dann zwei Bar-Ausgaben, beide ohne YNAB-Verknüpfung. Die Tagesgrenze begrenzt den Schaden, verhindert ihn nicht.
- Vorschlag: `addCashExpense` sollte bei Push-Fehler die `EntryId` mitliefern (z. B. `Result<EntryId, EntryId * YnabError>` oder ein eigener Ergebnis-DU), damit die Antwort die Id immer enthält.

### B4. `BotCommitResult` erlaubt widersprüchliche Zustände (Hinweis, bestätigt)

- Commit: b4b5cc3
- Ort: `src/Server/BotApi.fs:47-51`
- Beschreibung: `EntryId: Guid option`, `PushedToYnab: bool`, `PushError: string option` lassen Kombinationen wie `PushedToYnab = true` mit `PushError = Some _` zu. Der Code erzeugt sie nicht, der Typ verhindert sie aber auch nicht.
- Vorschlag: Ein DU wie `Pushed of EntryId | PushFailed of EntryId * reason` und die JSON-Felder daraus ableiten.

### B5. Ein fehlgeschlagener Commit bleibt für die Vorschau dauerhaft gescheitert (niedrig, Verdacht)

- Commit: b4b5cc3
- Ort: `src/Server/BotApi.fs:76-78` und `:366-374`
- Beschreibung: `CommitOnce` legt `Lazy<Task>` ab. Wirft die Arbeit eine Ausnahme (z. B. SQLite-Fehler beim zweiten `appendAndApply` nach erfolgreichem YNAB-Push), bleibt der fehlerhafte Task gespeichert, jeder weitere Commit derselben Vorschau liefert 500. Das schützt vor Doppelbuchung, aber die Antwort sagt Bob nicht, ob schon etwas geschrieben wurde. Legt Bob eine neue Vorschau an, kann in YNAB eine zweite Transaktion entstehen. Ob `YnabClient.createManualTransaction` Ausnahmen selbst abfängt, habe ich nicht geprüft.
- Vorschlag: Im Fehlerfall den Zustand der Vorschau ("Ergebnis unklar, nicht wiederholen") in der Antwort nennen und laut protokollieren, statt einen generischen 500 zu geben.

### B6. Die Buchungssperre umschließt den YNAB-Aufruf (Hinweis, bestätigt)

- Commit: cfb05ff
- Ort: `src/Server/BotApi.fs:90` und `:343-350`
- Beschreibung: `bookingGate` wird über Prüfung, Schreiben und den Netzwerk-Push gehalten. Hängt YNAB, warten alle weiteren Commits bis zum HTTP-Timeout. Für einen Einzelnutzer-Bot ist das tragbar; korrekt ist die Sperre (Prüfung und Schreiben atomar).
- Vorschlag: Die Sperre nach `CashExpenseAdded` freigeben, weil die Zählung aus diesem Ereignis kommt und der Push für die Grenze keine Rolle spielt.

### B7. Kategorien-Cache ignoriert das Budget (Hinweis, Verdacht)

- Commit: 92ac090
- Ort: `src/Server/BotApi.fs:131-149`, `:166-167`
- Beschreibung: Der Cache hat keinen Schlüssel. Wechselt jemand in den Einstellungen das Standard-Budget, liefert der Cache bis zu fünf Minuten die Kategorien des alten Budgets, während `Push` (`:470-485`) schon gegen das neue Budget bucht. YNAB lehnt eine fremde Kategorie-Id vermutlich ab, die Buchung steht dann als Push-Fehler im Kassenbuch.
- Vorschlag: Die Budget-Id als Cache-Schlüssel führen.

### B8. Token-Vergleich verrät die Länge (Hinweis, bestätigt)

- Commit: b4b5cc3
- Ort: `src/Server/BotApi.fs:228-236`
- Beschreibung: `FixedTimeEquals` bricht bei ungleicher Länge sofort ab. Bei einem zufälligen 64-Zeichen-Token ist das ohne praktische Bedeutung.
- Vorschlag: Beide Seiten vorher mit SHA-256 hashen und die Hashes vergleichen.

### B9. Entfernen von CORS schließt Cross-Site-Schreibzugriffe nicht ab (mittel, Verdacht)

- Commit: 0f39ac0
- Ort: `src/Server/Program.fs` (ganze Middleware-Kette), `src/Server/Api.fs:1982-2010`
- Beschreibung: Die Änderung ist richtig: `AllowAnyOrigin` gab fremden Seiten Lesezugriff. Der Testkommentar in `CorsTests.fs:3-9` legt aber nahe, damit sei der Browser-Angriff erledigt. Ohne CORS kann eine fremde Seite Antworten nicht lesen, sie kann aber weiterhin "einfache" POST-Anfragen (z. B. `Content-Type: text/plain`) an `/api/...` absetzen. Die App hat keine Anmeldung und kein CSRF-Merkmal. Falls Fable.Remoting den Rumpf unabhängig vom Content-Type als JSON liest (nicht geprüft), sind schreibende Aufrufe aus jedem Browser im Tailnet möglich. Ebenso fehlt eine Host-Prüfung gegen DNS-Rebinding (keine `AllowedHosts`-Einstellung gefunden).
- Angriffsszenario: Roman öffnet eine präparierte Seite auf einem Tailnet-Gerät; sie sendet per `fetch(..., {mode: "no-cors", method: "POST", body: ...})` einen Remoting-Aufruf, der Regeln löscht oder Einstellungen ändert.
- Vorschlag: Für `/api` einen Pflicht-Header (z. B. `X-Requested-With` oder `Content-Type: application/json` streng prüfen) verlangen und `AllowedHosts` auf die echten Hostnamen setzen.

### B10. CORS-Tests sind reine Quelltext-Suche (niedrig, bestätigt)

- Commit: 0f39ac0
- Ort: `src/Tests/CorsTests.fs:31-48`
- Beschreibung: Die Tests suchen Zeichenketten wie `Cors` in den Server-Quellen. Eine Policy über `IConfiguration`, ein eigenes Middleware-Lambda, das `Access-Control-Allow-Origin` setzt, oder ein Header im Proxy bleiben unentdeckt. Umgekehrt schlägt der Test bei harmlosen Bezeichnern mit "Cors" fehl. Der Kommentar begründet den Verzicht auf einen Host-Test ehrlich.
- Vorschlag: Die Middleware-Kette in eine Funktion `configureApp` ziehen und mit `TestServer` prüfen, dass ein Preflight `OPTIONS` mit `Origin` keinen `Access-Control-Allow-Origin` zurückgibt.

### B11. Testreihenfolge hängt an der gemeinsamen In-Memory-Datenbank (niedrig, bestätigt)

- Commit: b4b5cc3 (Grundlage), cfb05ff (Zählung)
- Ort: `src/Tests/BotApiTests.fs:266-335` gegen `:39-41` (Standarduhr 2026-10-28 10:00 UTC)
- Beschreibung: Die frühen Commit-Tests buchen alle auf den Berliner Tag 2026-10-28 (fünf Commits, rund 103 EUR). Die Tagesgrenze-Tests weichen bewusst auf eigene Tage aus (Kommentar `:336-338`), die frühen Tests aber nicht. Kommen auf dem Standardtag sechs weitere Commit-Tests dazu, scheitert der elfte mit 422 aus einem Grund, der mit dem Test nichts zu tun hat.
- Vorschlag: Jedem Test eine eigene Datenbank oder einen eigenen Tag geben, auch den frühen.

### B12. Veralteter Testname (Hinweis, bestätigt)

- Commit: cfb05ff
- Ort: `src/Tests/BotValidationTests.fs:69`
- Beschreibung: Der Name spricht von "über fünf Jahre zurück", geprüft werden aber das 31-Tage-Fenster und ein Datum von 2021.
- Vorschlag: Name an das 31-Tage-Fenster anpassen.

### B13. Der Zeitzonen-Test schützt das Image nicht (Hinweis, bestätigt)

- Commit: a20fef6
- Ort: `src/Tests/BotValidationTests.fs:244-250`
- Beschreibung: Der Test läuft auf dem Entwicklungsrechner, nicht im Alpine-Image. Er beweist nur, dass die Host-Zeitzonendatenbank Europe/Berlin kennt. Den eigentlichen Fehler (tzdata fehlt im Image) fängt der Node-Wächter `scripts/dockerfile-tzdata.test.mjs` per Textsuche im Dockerfile und der Startabbruch in `Program.fs:36-40`. Die Kombination ist sinnvoll; der Kommentar am F#-Test überschätzt seine Wirkung.
- Vorschlag: Kommentar präzisieren oder einen Smoke-Test gegen das gebaute Image ergänzen (`docker run ... --version` mit Zeitzonen-Prüfung).

### B14. Domänenregeln im Modul Validation (Hinweis, bestätigt)

- Commit: b4b5cc3, cfb05ff, a20fef6
- Ort: `src/Server/Validation.fs:160-229`
- Beschreibung: `BotDirection`, die Tagesgrenzen-Konstanten, `checkBotDailyLimit` und die Berliner Zeitrechnung sind Domänenwissen, liegen aber in `Validation`. `BotApi.fs` hält zusätzlich Speicher, Limiter, Cache, Handler und Live-Verdrahtung in einer Datei. Lesbar ist es, aber `Validation` wird zum Sammelbecken. Die Beträge sind rohe `decimal` mit der Konvention "positiv, Richtung extra"; ein kleiner Typ `PositiveEur` mit privatem Konstruktor würde die Prüfung aus `checkBotAmount` in den Typ ziehen.
- Vorschlag: Zeitzone und Tagesgrenze in ein eigenes Modul (z. B. `Bot.Domain`) verschieben und den Betrag als eigenen Typ führen.

### B15. Tiefe Verschachtelung im Vorschau-Handler (Hinweis, bestätigt)

- Commit: b4b5cc3, cfb05ff
- Ort: `src/Server/BotApi.fs:248-307`
- Beschreibung: Fünf geschachtelte `match`-Ebenen mit je einem `earlyReturn`. Funktional korrekt, aber jede neue Prüfung rückt den Erfolgspfad weiter ein.
- Vorschlag: Die Schritte als `Result<_, HttpHandler>` verketten (eigener kleiner `taskResult`-Builder) und am Ende einmal auswerten.

### B16. `berlinDayBoundsUtc` rechnet mit einem Hilfs-Offset (Hinweis, bestätigt korrekt)

- Commit: cfb05ff, a20fef6
- Ort: `src/Server/Validation.fs:215-220`
- Beschreibung: Die Umrechnung von Berliner Mitternacht nach UTC nutzt den Offset bei "Mitternacht als UTC minus 1,5 Stunden". Ich habe es für beide Umstellungstage durchgerechnet: Weil die EU-Umstellung um 01:00 UTC liegt, stimmt das Ergebnis, die Tests (`BotValidationTests.fs:233-242`) bestätigen 23 und 25 Stunden. Der Kniff ist aber schwer zu lesen und hängt an der Lage der Umstellungszeit.
- Vorschlag: `TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day, Unspecified), berlinZone)` verwenden.

### B17. Logging über `printfn` (Hinweis, bestätigt)

- Commit: b4b5cc3
- Ort: `src/Server/BotApi.fs:373`, `src/Server/Api.fs:1986`
- Beschreibung: Fehler beim Commit werden nur mit `ex.Message` auf stdout geschrieben, ohne Stacktrace und ohne Level. Sensible Daten landen nicht im Log: das Token wird nirgends ausgegeben, Rumpf und Payee auch nicht. Das ist gut.
- Vorschlag: `ILogger` aus dem `HttpContext` nehmen und die Ausnahme vollständig protokollieren.

### B18. Betrag als `float` im JSON (Hinweis, bestätigt)

- Commit: b4b5cc3
- Ort: `src/Server/BotApi.fs:205`
- Beschreibung: `decimal` wird über `float` ausgegeben. Für Beträge bis 500 EUR mit zwei Nachkommastellen ist die Darstellung exakt genug; in Thoth.Json.Net würde `Encode.decimal` einen String erzeugen, die Wahl ist also nachvollziehbar.
- Vorschlag: Keine Änderung nötig; ein Kommentar zur Absicht würde helfen.

### B19. Button-Änderung (Hinweis, bestätigt)

- Commit: 897b13c
- Ort: `src/Client/DesignSystem/Button.fs:64-71`, `src/Tests/ButtonSizeTests.fs`
- Beschreibung: Sauber und klein. `sizeToClass` ist dafür öffentlich geworden, damit die Tests Klassen-Strings prüfen können; diese F#-Tests sind nahe an Tautologien (sie prüfen den Text, den die Funktion zurückgibt). Der eigentliche Nachweis steckt im Playwright-Wächter `scripts/touch-target-small-button.test.mjs`, der die echte Höhe bei 390 und 768 px misst. `iconOnlyClass` prüft mit `IsNullOrEmpty`, ein Leerzeichen als Text würde nicht als Icon-only erkannt.
- Vorschlag: `IsNullOrWhiteSpace` verwenden; die Klassen-String-Tests kann man behalten, tragend ist der Messtest.

## Was gut gelöst ist

- Keine SQL-Injection: `loadBobBookedAmountsBetween` nutzt Dapper-Parameter, die Zeitstempel kommen im gleichen ISO-Format wie beim Schreiben, der Textvergleich ist dadurch korrekt.
- Das Konto ist aus der Anfrage nicht wählbar; es wird nie gelesen, die Ablehnung von `account`-Feldern ist eine zusätzliche, klare Rückmeldung.
- Ohne gesetztes Token sind die Routen geschlossen (401), nie offen. Leeres und Leerraum-Token werden als "nicht gesetzt" behandelt.
- Betrag positiv, Richtung nur aus `direction`; höchstens zwei Nachkommastellen; Datum in einem engen Fenster am Berliner Tag. Feldfehler werden gesammelt zurückgegeben.
- Die Tagesgrenze zählt aus den gespeicherten Ereignissen, überlebt also Neustarts; Prüfung und Schreiben sind gegen parallele Commits verschiedener Vorschauen abgesichert, und dafür gibt es einen echten Nebenläufigkeitstest.
- Idempotenter Commit über `Lazy<Task>` je Vorschau, mit Test für zehn parallele Aufrufe.
- Ein harter Deckel im Code, den die Umgebung nur senken kann.
- Kategorie-Auflösung rät nicht, sondern liefert Kandidaten; interne und versteckte Kategorien sind ausgeschlossen.
- Abhängigkeiten (Uhr, YNAB, Konto, Speicher) sind als Record injiziert, die Routentests laufen gegen die In-Memory-Datenbank und eine verstellbare Uhr. Die Tests prüfen beobachtbares Verhalten (Statuscodes, gespeicherte Ereignisse, Push-Zähler) statt Interna.
- Startabbruch bei fehlender Zeitzone statt stiller Ersatzrechnung (a20fef6), plus Wächter im Dockerfile.
- Pfadabgleich ohne Groß-/Kleinschreibung passend zum Proxy (ae7cfb5), sodass Varianten nicht in den SPA-Fallback rutschen.

## Gesamturteil

Die Bot-Schnittstelle ist für eine selbst gehostete Einzelnutzer-App sorgfältig gebaut: Grenzen, Idempotenz und Nebenläufigkeit sind durchdacht und getestet. Der wichtigste Mangel ist die globale Drossel vor der Anmeldung (B1), die einen billigen Aussperr-Hebel schafft. Danach folgen das fail-open-Verhalten der Obergrenze (B2) und die fehlende `entry_id` bei Push-Fehler (B3), die zu Doppelbuchungen durch Bob verleiten kann. Der CORS-Commit ist richtig, aber kein vollständiger Schutz gegen Cross-Site-Aufrufe auf `/api` (B9, Verdacht). Die übrigen Punkte betreffen Modellierung, Lesbarkeit und Testrobustheit.
