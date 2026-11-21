---
id: 0102
title: ING per FinTS über einen python-fints-Sidecar, Umsätze als CAMT.052, Importschlüssel aus dem Inhalt, eigener Abrufweg neben dem Comdirect-Port
scope: global
status: proposed
date: 2026-11-21
supersedes: []
superseded_by: []
related_tasks: [banking-import-y7m4r, banking-import-r28hx]
related_adrs: [0021, 0058, 0066, 0071, 0083, 0093]
related_research: [ing-fints-anbindung-2026-11-06]
---

# ADR 0102: ING per FinTS über einen python-fints-Sidecar, CAMT.052 und Importschlüssel aus dem Inhalt

## Context
BB soll Girokonto und Extra-Konto der ING selbst per FinTS abrufen (banking-import-r28hx), damit YNABs eigener
ING-Abruf wegfallen kann. Das Depot ist nur eine spätere Option. Grundlage ist der Live-Befund aus
banking-import-y7m4r (README `banking-import`, Abschnitt „Live-Befund ING per FinTS“, zwei Tage echter Abrufe) und
die Recherche `ing-fints-anbindung-2026-11-06`.

Aus dem Befund zählt für diese Entscheidung:
- Anmeldung im Einschritt-Verfahren 999 ohne TAN, kein Lese-Geschäftsvorfall verlangt eine TAN. Vorbedingung ist ein
  Log-in in App oder Online-Banking innerhalb der letzten 90 Tage. PIN 5 bis 64 Zeichen.
- Die ING bietet HKKAZ (MT940) und HKCAZ 1 mit camt.052.001.02. Beide liefern dieselben Buchungen, alle gebucht,
  keine Vormerkungen.
- Die ING liefert **keine Referenz je Buchung**: in MT940 kein Wert nach `//`, Kundenreferenz `NONREF`, kein `EREF+`
  und kein `KREF+`; in CAMT ist `AcctSvcrRef` ein Vorgangscode mit vier Werten je Konto, `EndToEndId` und `NtryRef`
  fehlen.
- Zwei Abrufe im Abstand von 19 Stunden waren Inhalt für Inhalt und in der Reihenfolge gleich. Inhaltsgleiche
  Buchungen gab es keine. Ob die ING den Text einer frischen Buchung später ändert, ist nicht beobachtet.
- Rückschau: Girokonto 90 Tage (bei mehr meldet die Bank 3010 neben den Daten), Extra-Konto etwa ein Jahr.
- **python-fints 5.0.1** lief ohne Eingriff, maskiert Sonderzeichen im Passwort und kann HKWPD.
- **libfintx 1.4.0** maskiert die PIN nicht (`?`, `+`, `:`, `'`, `@` gehen roh in HNSHA, am Dummy-Server belegt).
  Mit Romans Passwort wäre das ein Fehlversuch, und Fehlversuche können den Zugang sperren. Mit selbst maskierter PIN
  lief alles gleich gut. `UserIdEscaped` maskiert `@` nicht, HKWPD fehlt, Teilausführung (3010 neben 3997) meldet
  sie als `IsSuccess = false`.

Im Code: `src/Server/BankPort.fs` ist auf Comdirect zugeschnitten (Wiederaufnahme, Challenge, Push-TAN,
`ComdirectResult`). `fetchAndBook` in `src/Server/BankImport.fs` braucht davon nur `FetchTransactions`. Die
`import_id` entsteht über `Shared.Domain.generateImportId` als `BB:` plus Reference ohne Bindestriche (README,
„Invariante Importschlüssel“). YNAB nimmt höchstens 36 Zeichen (`ImportIdMaxLength = 36` in
`src/Server/Ledger/CommandHandlers.fs`). Ein Ersatz beim Zurückschreiben hängt `:r<n>` an und kürzt den Stamm auf
36 Zeichen (`replacementImportId`, ADR 0058). Das Kassenbuch sucht bekannte Importe über die `import_id`-Verknüpfung
ohne Konto (`knownComdirectImports`). Die Produktion ist ein Compose-Stack ohne eigenes Netz; der Proxy `bb-bot`
hängt im Netz `budgetbuddy_default` und erreicht jeden Dienst darin.

## Decision

### 1. Bibliothek: python-fints 5.0.1 als Sidecar-Container
Der FinTS-Dialog läuft in einem eigenen Container `budgetbuddy-fints` mit python-fints, Version fest auf 5.0.1
(mit Hash im Lockfile). libfintx in-process wird nicht genommen.

Ausschlaggebend ist die Sperrgefahr. Mit libfintx müsste BB die PIN selbst maskieren, um einen Fehler der Bibliothek
herum. Behebt eine spätere libfintx-Version das, maskiert BB doppelt, und die ING sieht ein falsches Passwort. Dazu
kommen die eigene Behandlung von 3010/3997 und `@` in der Benutzerkennung. python-fints hat eine gepflegte
ING-Sonderregel und lief im Befund ohne Eingriff. Der zweite Container kostet ein Image, einen Build-Schritt im Deploy
und einen kleinen HTTP-Vertrag. Das ist umkehrbar: Weil BB die CAMT-Dokumente selbst liest (Punkt 3) und den Schlüssel
selbst bildet (Punkt 4), hängt kein gespeicherter Wert an der Bibliothek. Ein Wechsel zu libfintx tauscht nur den
Adapter hinter dem Abrufweg (Punkt 7).

### 2. Schnittstelle zwischen Server und Sidecar
- **Netz:** HTTP ohne TLS, nur in einem eigenen Compose-Netz `fints`, an dem nur `budgetbuddy-app` und
  `budgetbuddy-fints` hängen. Der Sidecar hängt nicht in `budgetbuddy_default`, damit `bb-bot` ihn nicht erreicht. Kein
  veröffentlichter Port, kein Tailscale. Ausgehend erreicht der Sidecar nur die ING. Ein eigenes Token zwischen App und
  Sidecar gibt es nicht, weil außer der App niemand im Netz `fints` ist.
- **Bankziel fest im Sidecar:** URL `https://fints.ing.de/fints/` und BLZ stehen in seiner Konfiguration und kommen nie
  aus der Anfrage. Der Sidecar ist damit kein allgemeines FinTS-Relais mit Romans Produktnummer. Eine weitere Bank
  kommt ausdrücklich dazu.
- **Zustandslos:** ein Aufruf ist ein FinTS-Dialog (öffnen, Geschäftsvorfälle, schließen). Der Sidecar speichert
  nichts, kein Volume, kein Dialogzustand zwischen Aufrufen.
- **Endpunkte** (versioniert unter `/v1`, JSON, Vertrag gehört BB):
  - `POST /v1/accounts`: hin Benutzerkennung und PIN; zurück die Konten aus der UPD (IBAN, Kontonummer,
    Produktbezeichnung, erlaubte Geschäftsvorfälle) und die Rückmeldungen der Bank. Für die Einrichtung (Konto wählen)
    und als Verbindungstest.
  - `POST /v1/statements`: hin Benutzerkennung, PIN, eine IBAN, Von- und Bis-Datum; zurück die CAMT.052-Dokumente
    unverändert (Base64) und die Rückmeldungen der Bank. Das Blättern über Aufsetzpunkte (3040) erledigt python-fints.
  - `GET /health`: ohne Bankkontakt.
  - Saldo (HKSAL) und Depot (HKWPD) kommen später als eigene Endpunkte, wenn eine Task sie braucht.
- **Rückmeldungen** gehen als Liste aus Code und Text zurück (Dialog- und Auftragsebene getrennt). Das Urteil fällt in
  BB: 3010 und 3997 neben Daten sind ein Erfolg. Ein abgelehnter Dialogaufbau (9xxx bei der Initialisierung) wird in BB
  zu `IngLoginRejected` mit den Codes. Die Codes für falsches Passwort und für den fälligen 90-Tage-Log-in sind nicht
  beobachtet; bis sie es sind, behandelt BB jede Ablehnung beim Dialogaufbau gleich (Punkt 6).
- **Fehler des Sidecars:** HTTP 200 mit Rückmeldungen, wenn die Bank geantwortet hat; 502, wenn die ING nicht
  erreichbar war; 400 bei ungültiger Anfrage. Der Rumpf eines Fehlers trägt nur Codes und einen festen Satz.
- **Logs:** Der Sidecar schreibt nur Pfad, Status, Dauer, Rückmelde-Codes und Zählungen. Nie Anfrage- oder
  Antwortrumpf, nie Benutzerkennung, PIN, Produktnummer, IBAN oder CAMT-Inhalt. Das Logging von python-fints und der
  HTTP-Bibliotheken steht fest auf WARNING, weil DEBUG ganze Nachrichten samt HNSHA ausgibt. Für BB gilt dasselbe wie für
  Comdirect (README, „Invariante Fehlertexte“): Meldungen tragen Status und Codes, nie den Rumpf.
- **Laufzeit:** Python-Standardbibliothek (`http.server`) statt eines Web-Frameworks, damit außer python-fints keine
  Abhängigkeit dazukommt. Eine Anfrage zur Zeit reicht, BB ruft ohnehin nacheinander auf.

### 3. Format: CAMT.052
BB fragt Umsätze per HKCAZ als camt.052.001.02 ab und liest das XML selbst, in einer reinen F#-Funktion als
Anticorruption Layer (CAMT-Eintrag zu `BankTransaction`). MT940 bleibt der Rückweg, falls die ING HKCAZ einstellt.

Gründe: Beide Formate tragen laut Befund dieselben Buchungen und keinen Schlüssel. CAMT trennt Betrag, Richtung,
Buchungstag, Valuta, Gegenseite (Name, IBAN) und Verwendungszweck in eigene Elemente, MT940 packt sie in `:86:` mit
Unterfeldern, und die ING schreibt dort Umlaute in Latin1 außerhalb des SWIFT-Zeichensatzes. XML hat eine
festgelegte Kodierung. Weil der Sidecar das Dokument unverändert durchreicht, liegt das Parsen in BB und ist mit
Expecto an festen Beispieldokumenten testbar, ohne Python im Gate.

Folge: Ein späterer Wechsel des Formats ändert die Texte und damit die Schlüssel (Punkt 4). Er braucht deshalb eine
eigene Entscheidung mit Übergang.

### 4. Importschlüssel für ING-Buchungen
Ohne Referenz der Bank bildet BB den Schlüssel aus dem Inhalt.

- **Inhalt** (kanonische Zeile, Version im Text): `v1`, IBAN des Kontos, Buchungstag, Valuta, Betrag in Cent mit
  Vorzeichen aus `CdtDbtInd`, Währung, Geschäftsvorfallcode (`BkTxCd`), Name und IBAN der Gegenseite,
  Verwendungszweck (`Ustrd` aneinandergehängt). Texte in Unicode NFC, Leerraum auf ein Leerzeichen zusammengezogen,
  außen getrimmt. Fehlt ein Element, steht ein leerer Wert an seinem Platz.
- **Laufende Nummer:** Innerhalb eines Abrufs eines Kontos zählt BB je gleicher kanonischer Zeile 1, 2, 3 und hängt die
  Nummer an die Zeile an. Zwei echte, inhaltsgleiche Buchungen bekommen so zwei Schlüssel. Eine dritte, später
  gekommene bekommt die 3. Weil gleiche Zeilen untereinander austauschbar sind, hängt das Ergebnis allein von ihrer
  Zahl ab. Eine Gruppe hat denselben Buchungstag und verlässt das Abruffenster deshalb als
  Ganzes.
- **Form:** `ING:` plus die ersten 28 Hex-Zeichen von SHA-256 über die nummerierte Zeile, zusammen 32 Zeichen. Das
  lässt 4 Zeichen für `:r<n>` beim Ersatz (ADR 0058), ab `:r10` kürzt `replacementImportId` den Stamm wie heute. Hex
  enthält kein `r`, ein Stamm endet also nie auf `:r<Ziffern>`. Das eigene Präfix folgt ADR 0071 Punkt 5 und kann mit
  keiner Comdirect-`import_id` (`BB:`) und keiner YNAB-eigenen (`YNAB:`) zusammenfallen. Weil die IBAN im Inhalt steht,
  ist der Schlüssel über alle Konten eindeutig; das braucht die kontolose Suche im Kassenbuch.
- **`TransactionId`** einer ING-Buchung ist derselbe Wert. `generateImportId` (Präfix `BB:`) gilt nur für Comdirect;
  die `import_id` entsteht je Quelle. Die Comdirect-Form bleibt unverändert.
- **Fest wie bei Comdirect:** Ein Regressionstest mit festen Werten hält kanonische Zeile, Nummer und Hash fest. Jede
  Änderung an Normalisierung oder Feldliste bringt gebuchte Umsätze doppelt nach YNAB; sie geht nur mit neuer Version
  in der Zeile und eigenem ADR.

**Nachträglich geänderter Text.** Ändert die ING Text, Gegenseite oder Valuta einer schon gebuchten Buchung, entsteht
ein neuer Schlüssel und BB hielte sie für neu. Dagegen gibt es zwei Netze:
- Die Duplikaterkennung gegen YNAB (ADR 0093) findet die erste Fassung meist als unsicheres Duplikat (Betrag, Tag ±1,
  ähnlicher Payee).
- Dazu prüft BB vor dem Buchen gegen das Kassenbuch, rein und ohne YNAB: Steht im Kassenbuch eine ING-Buchung dieses
  Kontos, deren Schlüssel im aktuellen Abruf fehlt, obwohl ihr Buchungstag im Fenster liegt (ohne den ältesten Tag des
  Fensters), und hat eine neue Buchung desselben Kontos denselben Buchungstag und Betrag, wartet die neue als
  unsicheres Duplikat mit der alten als Treffer (`EntryHeldAsPossibleDuplicate`, Grund „Bei der ING geändert?“).
  Roman entscheidet wie bei jedem unsicheren Duplikat.

Jeder Abruf zählt die verschwundenen Schlüssel (nur die Zahl, im Log und in den Zahlen des Abrufs). Damit wird in der
Probezeit sichtbar, ob die ING Texte ändert. Zeigt sich das oft, wird die Feldliste in einer neuen Version verengt.

### 5. Produktnummer und Zugangsdaten
- **Produktnummer** `FINTS_PRODUCT_ID` liegt in `.env` und geht per Compose nur in den Sidecar. Der Server kennt sie
  nicht, sie kann also weder in `AppSettings` noch in `AppSettingsView`, Logs oder Fehlermeldungen landen. Im Code, in
  Tests, Beispielen und Doku steht sie nie; `.env.example` hat einen leeren Platzhalter. Fehlt sie, startet der Sidecar
  nicht (`/health` meldet das ohne Wert), die Standardnummer von python-fints wird nie benutzt. Die Produktversion
  (BBs Version) ist kein Geheimnis und darf im Code stehen. Läuft FinTS später doch in-process, wandert die Nummer aus
  `.env` in `AppSettings`, mit denselben Regeln.
- **ING-Benutzerkennung und PIN** liegen wie die Comdirect-Zugangsdaten verschlüsselt in den Einstellungen (ADR 0071,
  `Persistence.Encryption`, `BUDGETBUDDY_ENCRYPTION_KEY`), eingegeben in einer Karte ING der Einstellungen. Sie gehen
  je Aufruf im Rumpf an den Sidecar, verlassen den Server sonst nie und stehen nicht in `AppSettingsView` (Pflichtliste
  Punkt 14 der Test-Doktrin, Test nach `src/Tests/SettingsSecretsTests.fs`). Ohne Verschlüsselungsschlüssel speichert
  BB sie nicht. Die Validierung an der API-Grenze prüft die Länge der PIN (5 bis 64).
- Für Sonden und Integrationstests bleiben `ING_FINTS_USER` und `ING_FINTS_PIN` in `.env` als Prozess-Variablen, nur
  hinter `RUN_INTEGRATION_TESTS`.

### 6. Schutz vor Sperre und der 90-Tage-Log-in
- Ein Aufruf ist genau ein Anmeldeversuch. Weder Sidecar noch BB wiederholen eine abgelehnte Anmeldung.
- Nach `IngLoginRejected` ruft der automatische Abruf die ING nicht mehr an, bis Roman von Hand „Abrufen“ drückt, den
  Verbindungstest startet oder die Zugangsdaten neu speichert. Der Zustand lebt in den Einstellungen und übersteht einen
  Neustart. Das entspricht der Invariante Challenge bei Comdirect: einen Versuch, der scheitern kann, löst nur Roman aus.
- Die Meldung nennt beide Ursachen, die BB nicht unterscheiden kann: Passwort falsch oder seit 90 Tagen kein Log-in in
  App oder Online-Banking. Sobald die Codes beobachtet sind, trennt BB sie.

### 7. Einordnung: eigener schmaler Abrufweg, der Comdirect-Port bleibt
`BankPort` wird nicht für die ING verallgemeinert. Wiederaufnahme, Status, Challenge und Push-TAN gibt es bei der ING
nicht, eine ING-Implementierung müsste sie leer füllen. Stattdessen wird die Stelle geteilt, die beide brauchen:
- `fetchAndBook` bekommt statt `Sources.Bank` eine quellneutrale Abruffunktion (Konto, Tage, heutiger Tag zu
  `BankTransaction list` oder Fehler), dazu je Quelle die `import_id`-Bildung, den Actor und das Ziel (Kassenbuch- und
  YNAB-Konto). Der Fehlertyp wird quellneutral (`BankFetchFailed` trägt heute `ComdirectError`), die
  Comdirect-Beschreibung bleibt im Comdirect-Adapter.
- `BankPort` bleibt der Comdirect-Verbindungsport für `startSync`, `confirmTan`, Wiederaufnahme und Tick. Der Name darf
  dabei zu einem Comdirect-Namen werden; das entscheidet der Worker.
- Der ING-Adapter ist ein HTTP-Client auf den Sidecar plus die reine CAMT-Übersetzung und Schlüsselbildung. Er kommt in
  den Netzwächter (`src/Tests/NetworkCallGuardTests.fs`), Live-Aufrufe nur hinter `RUN_INTEGRATION_TESTS`.
- Ein ING-Abruf geht Konto für Konto durch `fetchAndBook`, jedes ING-Konto mit eigenem Ziel. Er hält dasselbe
  `ImportPorts.Gate` wie Comdirect: ein Abruf zur Zeit über alle Quellen, damit Kassenbuch und YNAB nie zwei Importe
  gleichzeitig sehen.
- Für das Girokonto fragt BB höchstens 89 Tage zurück, für das Extra-Konto höchstens 365.
- Die Sync-Sitzung und `sync_sessions` bekommen die Quelle, damit „ING · Import <Stand>“ und das Intervall je Quelle
  lesbar sind. Ob ING im selben Takt wie Comdirect läuft, entscheidet die Verfeinerung von r28hx.
- Für das Kassenbuch empfiehlt dieses ADR ein eigenes Ereignis `EntryImportedFromIng` mit der Payload-Form von
  `EntryImportedFromComdirect` und Actor `IngImport`, statt das Comdirect-Ereignis um eine Quelle zu erweitern (das
  bräuchte eine neue Payload-Version). Die Entscheidung trifft der `tactical-modeler` beim Verfeinern.

## Consequences
### Positive
- Kein Anmeldeversuch mit falsch kodiertem Passwort; die bekannte Sperrfalle von libfintx fällt weg.
- Die Produktnummer erreicht den Server nie. Die Zusage an die DK hängt an einer Stelle (Compose und `.env`).
- Schlüssel, Parsen und Duplikatlogik liegen in F# und laufen im Gate mit festen Beispieldokumenten. Der Sidecar ist
  dünn, ein Tausch der Bibliothek berührt keinen gespeicherten Wert.
- Depot und Saldo sind mit python-fints ohne Bibliothekswechsel erreichbar.

### Negative
- Zweiter Container und zweite Sprache. Der Deploy-Hook baut ein zweites Image; Python-Abhängigkeiten brauchen eigene
  Pflege (Version fest, Updates von Hand).
- Der Sidecar-Code läuft nicht in `./verify.sh`. Er bleibt deshalb so klein, dass er kaum Logik trägt; der Vertrag wird
  in BB über Decoder-Tests an Beispiel-JSON geprüft.
- Die PIN geht je Aufruf unverschlüsselt über das Docker-Bridge-Netz auf demselben Host. Wer Root auf dem Host hat,
  liest sie ohnehin aus Datenbank und Schlüssel (ADR 0071, Negative).
- Ein Inhaltsschlüssel ist schwächer als eine Bankreferenz. Ändert die ING Texte, entstehen wartende unsichere
  Duplikate, die Roman entscheiden muss. Zwei inhaltsgleiche Buchungen, von denen die ING eine später storniert und
  aus der Liste nimmt, lassen BB eine zu viel behalten; der Abgleich mit dem Saldo wäre der spätere Weg, das zu sehen.
- Ohne beobachtete Codes kann BB falsches Passwort und abgelaufenen 90-Tage-Log-in nicht trennen.

### Neutral
- Die Comdirect-Form der `import_id` und `generateImportId` bleiben, die Invariante Importschlüssel gilt weiter.
- README `banking-import`: die offene Frage „eigener Adapter oder Generalisierung“ ist mit Punkt 7 beantwortet.

## Alternatives considered
- **libfintx 1.4.0 in-process mit selbst maskierter PIN.** Verworfen: hängt an einem Fehler der Bibliothek, der bei
  einer Korrektur zur Doppelmaskierung und damit zur Sperrgefahr wird; dazu `@` in der Kennung, 3010/3997 als Fehler
  und kein HKWPD. Wäre ohne zweiten Container einfacher. Neu zu prüfen, wenn libfintx die PIN selbst maskiert und das an
  einem Dummy-Server belegt ist.
- **Sidecar liest Benutzer und PIN aus `.env`.** Verworfen: zweiter Ort für Bankzugangsdaten neben den Comdirect-Daten
  in den Einstellungen, Klartext auf der Platte, Ändern nur per Neustart auf dem Host. Wäre der kleinere Bau ohne Karte
  in den Einstellungen.
- **Sidecar im Netz `budgetbuddy_default`.** Verworfen: `bb-bot` erreichte ihn dann, und mit Benutzer und PIN im Rumpf
  ist der Sidecar ein Anmeldedienst.
- **MT940.** Verworfen für jetzt: gleicher Inhalt, aber Unterfelder in `:86:` und Latin1-Umlaute außerhalb des
  Zeichensatzes. Bleibt Rückweg.
- **`AcctSvcrRef` als Schlüssel.** Verworfen: vier Werte je Konto, ein Vorgangscode.
- **Schlüssel ohne Text (Konto, Tag, Valuta, Betrag) plus Nummer nach Reihenfolge.** Verworfen: Zwei Kartenzahlungen
  gleichen Betrags am selben Tag hingen dann an der Reihenfolge der Bank. Kommt eine dazu und sortiert sich davor,
  verschieben sich die Nummern, und eine gebuchte Buchung bekäme den Schlüssel einer anderen.
- **Ganzer SHA-256 oder Guid.** Verworfen: 64 Zeichen passen nicht in 36, eine Guid ist nicht aus dem Inhalt ableitbar.
- **`BB:ING:` als Präfix.** Verworfen: 7 Zeichen Präfix lassen weniger Platz für den Ersatz-Suffix, und `BB:` ist an
  `matchesImportId` für Comdirect gebunden.
- **`BankPort` für beide Banken verallgemeinern.** Verworfen: Die ING füllte Challenge und Wiederaufnahme leer, der
  Port würde unschärfer, ohne dass ein Aufrufer gewinnt.

## References
- README `banking-import`, „Live-Befund ING per FinTS (2026-11-20 und 2026-11-21)“
- Recherche `.agentheim/knowledge/research/ing-fints-anbindung-2026-11-06.md`
- ADR 0021 (Kassenbuch zuerst, dann Push), ADR 0058 (Ersatz-`import_id`), ADR 0071 (Bank-Port, Verschlüsselung),
  ADR 0093 (Import direkt ins Kassenbuch, unsichere Duplikate)
- fints.org, FAQ Produktregistrierung (Produktbezeichnung und Produktversion)
