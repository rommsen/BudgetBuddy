---
id: ledger-b0b7q
title: "Quick Add für Bob: schmale HTTP-API unter /bot/ mit Vorschau und Bestätigung, Bearer-Token, Obergrenze 500 € und eigener Herkunft Bob (ADR 0066)"
status: done
type: feature
context: ledger
created: 2026-10-27
completed:
depends_on: []
blocks: []
tags: [backend, api, quick-add, bob, sicherheit]
related_adrs: [0004, 0013, 0066, 0067]
related_research: []
prior_art: []
---

## Why
Bob (Hermes-Agent auf der Hetzner-VM, Repo `~/src/personal-agent`) soll Barbuchungen
anlegen können, so wie Roman es heute über Quick Add tut: "50 Euro heute an Anastasia,
Kategorie Haushaltshilfe, Barkonto". Bob zerlegt den Satz selbst in Felder, BB nimmt nur
strukturierte Daten an.

## What
Eigene HTTP-Routen unter `/bot/`, getrennt von Fable.Remoting (ADR 0066). Das Wire-Format
von Fable.Remoting ist ein internes Detail und darf nicht zum Vertrag mit Bob werden.

Ablauf in zwei Schritten, weil Bob die Buchung vor dem Schreiben Roman zur Bestätigung
zeigt:

1. `POST /bot/transactions/preview` mit `amount`, `direction` (`outflow`/`inflow`),
   `payee`, `category` (Name oder Id), `date`, `memo`. BB prüft, löst die Kategorie auf
   und legt eine Vorschau mit `preview_id` und Ablaufzeit (15 Minuten) an. Antwort: die
   aufgelöste Buchung (Konto, Betrag, Payee, Kategorie mit Name und Gruppe, Datum, Memo).
   Nichts geht an YNAB, nichts landet im Kassenbuch.
2. `POST /bot/transactions/{preview_id}/commit`. Bucht genau die Vorschau, über
   denselben Weg wie Quick Add (`Ledger.CommandHandlers.addCashExpense`, dann Push).
   Ein zweiter Commit derselben Vorschau bucht nicht noch einmal, sondern liefert das
   Ergebnis des ersten.
3. `GET /bot/categories`: Name, Gruppe, Id. Damit Bob Namen auflösen und Rückfragen
   stellen kann.

Schnitt nach Projektmuster: Validierung der Vorschau-Eingabe in `Validation.fs`,
Kategorie-Auflösung und Obergrenzen-Prüfung als reine Funktionen, Vorschau-Speicher
(In-Memory, TTL) und Handler im Server. `addCashExpense` bekommt den Actor als Parameter
(heute fest `QuickAdd`), damit Quick Add und Bob denselben Weg gehen.

## Acceptance criteria
- [ ] Ohne gültiges Bearer-Token antworten alle `/bot/*`-Routen mit 401. Das Token kommt
      aus der Umgebung (`.env`, Name z. B. `BOT_API_TOKEN`), nicht aus der Datenbank. Ist
      kein Token gesetzt, sind die Routen abgeschaltet (401 oder 404, nie offen).
- [ ] Unbekannte Pfade unter `/bot/` antworten mit 404 als JSON, nicht mit der
      `index.html` des SPA-Fallbacks.
- [ ] Kategorie-Auflösung: genau ein Treffer (Id oder Name, ohne Groß-/Kleinschreibung)
      wird übernommen. Kein oder mehrere Treffer ergeben 422 mit Kandidatenliste, BB rät
      nicht. Interne YNAB-Kategorien sind nicht wählbar.
- [ ] Das Konto ist immer das Quick-Add-Konto (ADR 0004). Die Eingabe hat kein Konto-Feld;
      ein mitgeschicktes wird ignoriert oder abgelehnt. Ohne eingerichtetes Quick-Add-Konto
      antwortet die Vorschau mit 422 und Grund.
- [ ] Betrag über der Obergrenze (Vorgabe 500 €, per Umgebung konfigurierbar, z. B.
      `BOT_MAX_AMOUNT_EUR`) wird in der Vorschau mit 422 abgelehnt. Betrag 0 oder negativ
      ebenso; die Richtung kommt allein aus `direction`.
- [ ] Ungültige Eingaben (fehlendes Feld, falsches Datum, unbekannte `direction`) ergeben
      400 mit Feldnamen.
- [ ] Commit einer unbekannten Vorschau ergibt 404, einer abgelaufenen 410.
- [ ] Commit ist idempotent: zweimal dieselbe `preview_id` erzeugt genau eine Buchung, der
      zweite Aufruf liefert das Ergebnis des ersten. Auch zwei gleichzeitige Commits
      buchen nur einmal.
- [ ] Die Buchung trägt die Herkunft `Bob`: neuer `Actor`-Fall in
      `Server/Ledger/Events.fs` (samt JSON-Codec, additiv, Payload-Schema-Versionen
      unverändert) und neuer `LedgerOrigin`-Fall in `Shared/Domain.fs` mit Anzeigename.
      Sichtbar in Historie und Detail.
- [ ] Ein Push-Fehlschlag verhält sich wie bei Quick Add (`EntryPushToYnabFailed`, Actor
      `Bob`), die Commit-Antwort meldet ihn.
- [ ] Keine Lese-Route für Buchungen oder Salden. Bob sieht nur Kategorien und seine
      eigenen Vorschauen.
- [ ] Tests: Validierung, Kategorie-Auflösung, Obergrenze und TTL als Unit-Tests;
      die Routen (401, 404, 410, 422, Idempotenz) als Integrationstests über den
      Giraffe-Handler mit Test-Datenbank, nie gegen die Produktionsdatenbank. Ein Codec-
      Rundlauf-Test für `Actor.Bob`.
- [ ] `.env.example` und die Doku nennen die neuen Umgebungsvariablen.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Entscheidungen mit Roman (2026-10-27):
- REST plus Skill bei Bob, kein MCP-Server. Zwei, drei Operationen rechtfertigen keinen
  eigenen MCP-Server. Kommen später Lesezugriffe dazu, lässt sich diese Schicht als MCP
  verpacken.
- Bob zeigt die erkannte Buchung vor dem Schreiben zur Bestätigung. Dass Roman wirklich
  bestätigt hat, kann BB nicht prüfen; das regelt Bobs Skill. Die zweistufige API stellt
  sicher, dass genau das gebucht wird, was Roman gesehen hat, und deckt zugleich die
  Idempotenz ab (Bob wiederholt Aufrufe bei Timeouts).
- Beim Verfeinern (2026-10-27): Obergrenze 500 €. Vorschauen im Speicher mit 15 Minuten
  TTL; nach einem Neustart verfallen sie. `Bob` wird ein eigener `Actor`, keine Ausprägung
  von `QuickAdd`. `/bot/*` liegt in `Server.Api.webApp` vor dem SPA-Fallback
  (`MapFallbackToFile` in `Program.fs`); unbekannte `/bot/`-Pfade fängt ein eigener
  404-Handler ab. Festgehalten in ADR 0066.

Netzgrenze liegt im Homelab-Repo, nicht hier: BB hat keine eigene Anmeldung, und eine
Tailscale-ACL filtert nur Ports. Bob kommt deshalb über einen Proxy-Stack nach dem Muster
von `stacks/ha-mcp/` (Caddy lässt nur `/bot/*` durch, eigener Tag, ACL-Test), nie direkt
auf `budgetbuddy`. Das Bearer-Token hier ist die zweite Schicht. Der Proxy-Stack und Bobs
Skill sind nicht Teil dieses Tasks.

Bedrohungsbild: Bob liest Mails und Webseiten, eine Prompt-Injection ist realistisch.
Darum nur Schreiben aufs Barkonto, Obergrenze, eigene Herkunft und kein Lesezugriff auf
das Kassenbuch (alles, was Bob liest, geht an die Hetzner-VM und den LLM-Anbieter).


## Verifier note (iteration 1)

**REASONS:**
- Check 5 (README): neue Herkunft `LedgerOrigin.AddedByBob` (Anzeigename "Bob") fehlt in den Bullets `**Herkunft**` (README Zeile 34: "Comdirect-Import, Quick Add, Regel, Roman in BB, YNAB-Spiegel") und `**Herkunfts-Badge**` (Zeilen 144 bis 146, Werte von `Shared.Domain.originLabel`).
- Check 5: Actor-Listen unvollständig in `## Key events`, Bullet *EntryPushToYnabFailed* (Zeile 244, "Actor `ComdirectImport`, `QuickAdd` bzw. `UserInBb`") und `## Key commands`, Bullet *BuchungÜbernehmen*, Zeile 257 ("`EntryPushToYnabFailed` (Grund, Actor `QuickAdd`)").
- Check 6b: ADR 0066 Entscheidung 5 sagt "nur Ausgaben aufs Quick-Add-Konto", die Folgen sagen "höchstens Barausgaben bis zur Obergrenze". Der Code nimmt `direction: "inflow"` an und bucht positiv (wie die Task verlangt). Die Abweichung von ADR 0066 ist nicht festgehalten; "Bewusst keine ADR: alles folgt ADR 0066" stimmt nicht.

**SUGGESTED_FIX:** Im OUTCOME unter "README von Hand" Bob in die Listen Herkunft und Herkunfts-Badge (Zeilen 34, 144 bis 146) und in die Actor-Listen (Zeilen 244, 257) aufnehmen. Eine ADR, die ADR 0066 ergänzt: Bob darf auf dem Quick-Add-Konto auch Einnahmen buchen. Kein weiterer Code-Durchgang nötig.

**ITERATION_HINT:** likely-fixable

## Outcome

Die Bot-Schnittstelle nach ADR 0066 steht. Neue Datei `src/Server/BotApi.fs` (Giraffe-Handler, vor Fable.Remoting in `Server.Api.webApp` eingehängt, daher vor dem SPA-Fallback): `POST /bot/transactions/preview`, `POST /bot/transactions/{preview_id}/commit`, `GET /bot/categories`. Auth zuerst: ohne gültiges Bearer-Token (Vergleich in konstanter Zeit) oder ohne gesetztes `BOT_API_TOKEN` antwortet jede `/bot/*`-Route mit 401, danach folgen die Routen, unbekannte Pfade und falsche Methoden antworten 404 als JSON. Pfade außerhalb von `/bot/` (auch `/botanik`) gehen unverändert weiter.

Aufteilung: Eingabeprüfung (`parseBotPreviewRequest`, `checkBotAmount`, `selectableBotCategories`, `resolveBotCategory`) als reine Funktionen in `src/Server/Validation.fs`; Token und Obergrenze (`resolveBotToken`, `resolveBotMaxAmountEur`, Vorgabe 500 EUR, `BOT_MAX_AMOUNT_EUR`) in `src/Server/ServerConfig.fs`; Vorschau-Speicher (`BotPreviewStore`, im Speicher, TTL 15 Minuten) und Handler in `BotApi.fs`; alles Externe (Token, Grenze, Uhr, YNAB-Kategorien, Quick-Add-Konto, Push) kommt als `BotDeps` von außen, `liveDeps ()` verdrahtet die echte Umgebung. `addCashExpense` hat jetzt den Actor als ersten Parameter (`Ledger.Events.QuickAdd` in `Server.Api`, `Bob` im Bot-Commit); die acht Aufrufstellen in `LedgerTests.fs`, `LedgerPushFailureTests.fs`, `LedgerMirrorTests.fs` sind angepasst.

Idempotenz: `BotPreviewStore.CommitOnce` hält je Vorschau ein `Lazy<Task>`, gleichzeitige Commits teilen sich eine Ausführung (Test mit 10 parallelen Aufrufen, 150 ms Push-Verzögerung: genau eine Buchung, ein Push). Ein Wiederholungsaufruf liefert das erste Ergebnis auch nach Ablauf der 15 Minuten und auch nach einem gescheiterten Push (sonst entstünde eine zweite Buchung).

Herkunft: neuer `Actor.Bob` (Spalten `"Bob", None` in `actorToColumns`/`actorFromColumns`, additiv, Payload-Schema-Versionen unverändert), neue `LedgerOrigin.AddedByBob` mit Anzeigename "Bob" (`originLabel`), `ReadModel.originOf Bob = AddedByBob`, Historienpunkt in `Client/Components/Ledger/View.fs` (`historyDotFor`) orange wie Quick Add.

Umgebung und Doku: `.env.example`, `compose.yaml` (reicht `BOT_API_TOKEN` und `BOT_MAX_AMOUNT_EUR` durch, leer = abgeschaltet bzw. Vorgabe) und neue `docs/bot-api.md`.

Entscheidungen im Detail, die über den Task-Text hinausgehen: Ein mitgeschicktes Konto-Feld (`account`, `account_id`, `accountId`, `account_name`) wird mit 400 abgelehnt (nicht ignoriert). Datum nur als `JJJJ-MM-TT`, nicht in der Zukunft, höchstens fünf Jahre zurück; Betrag nur als JSON-Zahl mit höchstens zwei Nachkommastellen. Kategorien kommen live aus YNAB (`getCategories`), wählbar sind alle außer "Internal Master Category" und versteckten. Bei keinem Treffer sind die Kandidaten die Kategorien, deren Name den Suchtext enthält (oder umgekehrt), sonst leer. Die Commit-Antwort bei Push-Fehlschlag ist 200 mit `pushed_to_ynab: false` und `push_error` (die Buchung steht im Kassenbuch); `entry_id` fehlt dann (null), weil `addCashExpense` im Fehlerfall nur den YNAB-Fehler zurückgibt. Abgelaufene Vorschauen bleiben eine Stunde im Speicher, damit ein später Commit 410 statt 404 bekommt. Die Abweichung von ADR 0066 (Bob bucht auch Einnahmen) steht in ADR 0067 (`0067-bob-darf-auch-einnahmen-buchen.md`).

Tests (48 neue Fälle): `src/Tests/BotValidationTests.fs` (Eingabeprüfung, Betragsgrenze 500 EUR bzw. 500000 Milliunits, Kategorie-Auflösung, Konfiguration, Actor-Rundlauf, Herkunft) und `src/Tests/BotApiTests.fs` (Routen über den Giraffe-Handler mit `DefaultHttpContext`, In-Memory-SQLite, injiziertes YNAB: 401 in drei Varianten, abgeschaltet ohne Token, 404 JSON, keine Lese-Routen, 400 mit Feldnamen, 422 Betrag/Kategorie/Konto, 404/410 Commit, Buchung mit Actor Bob aus der Datenbank geladen, -50 EUR = -50000 Milliunits, Einnahme +25 EUR, Idempotenz, 10 parallele Commits, Push-Fehlschlag mit `EntryPushToYnabFailed` Actor Bob). Gegenprobe: ersetzt man `CommitOnce` durch eine Ausführung je Aufruf, werden 3 Tests rot (Idempotenz, Parallel, Push-Fehlschlag). Läufe aus dem Worktree: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 2463 bestanden, 0 Fehler, 7 übersprungen (YNAB-Integrationstests ohne .env); `npm run build` ok; `npm run guard:classes` und `npm run guard:tokens` grün; `npm run test:scripts` 278/278 in 113 s. README_DELTA gegen `npm run check:readme-delta -- --bc ledger` geprüft (Exit 0).

Nicht Teil des Tasks und offen: Proxy-Stack im Homelab-Repo und Bobs Skill (laut Task-Notes). Die Ereignis-Katalog-Zeilen der README ziehe man bitte von Hand nach.

### README von Hand

Abschnitt `## Ereignisse`, Absatz "Actor:" (Zeile 325 ff.), alter Satz:
`**Actor:** \`YnabMirror | ComdirectImport | Rule of RuleId | UserInBb | QuickAdd |` / `OrderIdMatch of orderId: string\`. Der letzte Fall (ADR 0025) markiert ...`
neu: `**Actor:** \`YnabMirror | ComdirectImport | Rule of RuleId | UserInBb | QuickAdd | Bob |` / `OrderIdMatch of orderId: string\`.` und danach als zusätzlicher Satz vor "Der letzte Fall": `Bob (ADR 0066, ledger-b0b7q) ist der Urheber der Buchungen aus der Bot-Schnittstelle /bot/, Anzeigename "Bob" (\`Shared.Domain.originLabel AddedByBob\`), additiv ohne Schema-Änderung.` Der Satz "Der letzte Fall (ADR 0025)" bezieht sich weiter auf `OrderIdMatch`, bitte als "Der Fall OrderIdMatch (ADR 0025)" lesen.

Ereignis `CashExpenseAdded` (Zeile 397), alte Zeile: `- \`CashExpenseAdded\`: Quick-Add-Buchung angelegt, vor dem Push (Actor \`QuickAdd\`), darf` / `sofort eine Kategorie tragen.`
neu: `- \`CashExpenseAdded\`: Quick-Add-Buchung angelegt, vor dem Push (Actor \`QuickAdd\`, bei der Bot-Schnittstelle \`Bob\`, ADR 0066), darf` / `sofort eine Kategorie tragen.`

Ereignis `EntryPushToYnabFailed` (Zeile 399 ff.), alter Satz: `Schema-Version 1, Actor \`ComdirectImport\`, \`QuickAdd\` bzw.` / `\`UserInBb\` (Erneut senden, ledger-cks06).` neu: `Schema-Version 1, Actor \`ComdirectImport\`, \`QuickAdd\`, \`Bob\` bzw.` / `\`UserInBb\` (Erneut senden, ledger-cks06).`

Ereignis `EntryLinkedToYnab` (Zeile 467), alt: `(Actor \`ComdirectImport\` bzw. \`QuickAdd\`, beim erneuten Senden aus dem Detail \`UserInBb\`)` neu: `(Actor \`ComdirectImport\`, \`QuickAdd\` bzw. \`Bob\`, beim erneuten Senden aus dem Detail \`UserInBb\`)`.

Abschnitt `## Key commands`, Bullet *BuchungÜbernehmen* (Zeile 252 ff.), alt: `Quick Add: \`Ledger.CommandHandlers.addCashExpense\` schreibt zuerst \`CashExpenseAdded\`` / `(Actor \`QuickAdd\`), ruft danach den Push auf` neu: `Quick Add: \`Ledger.CommandHandlers.addCashExpense\` (Actor als Parameter: \`QuickAdd\` für die eigene Seite, \`Bob\` für die Bot-Schnittstelle) schreibt zuerst \`CashExpenseAdded\`` / `(mit diesem Actor), ruft danach den Push auf`.

Ubiquitous language, Bullet `**Herkunft**` (Zeile 34 bis 36), alt:
`- **Herkunft** — woher eine Buchung oder Änderung stammt: *Comdirect-Import*, *Quick Add*,` / `*Regel* (Auto-Kategorisierung), *Roman in BB*, *YNAB-Spiegel* (dahinter ING-Import,`
neu: `- **Herkunft** — woher eine Buchung oder Änderung stammt: *Comdirect-Import*, *Quick Add*,` / `*Bob* (Bot-Schnittstelle, ADR 0066), *Regel* (Auto-Kategorisierung), *Roman in BB*, *YNAB-Spiegel* (dahinter ING-Import,`

Bullet `**Herkunfts-Badge**` (Zeile 144 bis 146), alt:
`*YNAB-Spiegel*, *Comdirect-Import*, *Regel*, *Roman in BB*, *Quick Add*,` / `*Order-ID-Abgleich* (\`Shared.Domain.originLabel\`).`
neu: `*YNAB-Spiegel*, *Comdirect-Import*, *Regel*, *Roman in BB*, *Quick Add*, *Bob*,` / `*Order-ID-Abgleich* (\`Shared.Domain.originLabel\`).`

Key commands, Bullet *BuchungÜbernehmen* (Zeile 257), alt:
`der Fehlschlag steht danach als \`EntryPushToYnabFailed\` (Grund, Actor \`QuickAdd\`) im`
neu: `der Fehlschlag steht danach als \`EntryPushToYnabFailed\` (Grund, Actor \`QuickAdd\` bzw. \`Bob\`) im`
