---
id: banking-import-d0fey
title: "Import direkt ins Kassenbuch: „Abrufen“ bucht neue Umsätze ohne Prüfsitzung sofort in Kassenbuch und YNAB als „zu prüfen“, unsichere Duplikate warten im Kassenbuch, der Sync-Ablauf schrumpft auf das Sheet „Abrufen“"
status: done
type: feature
context: banking-import
created: 2026-11-11
completed:
depends_on: [ledger-s1weh]
blocks: [banking-import-t4raw, ledger-r7h2w, ledger-v6s7c]
tags: [etappe-3, backend, frontend, sync]
related_adrs: [0021, 0071, 0083, 0086, 0087, 0093]
related_research: []
prior_art: []
---

## Why
Import und Durchsicht sind heute eine eigene Insel: ein Abruf landet in einer Prüfsitzung im Speicher, und erst
„Importieren“ bucht ins Kassenbuch und nach YNAB. Neue Umsätze fehlen deshalb im Kassenbuch, bis Roman die Sitzung
abschließt, und eine liegen gebliebene Sitzung geht beim Neustart verloren. ADR 0093 (Roman, 2026-11-11): ein
Abruf bucht sofort, durchgesehen wird danach im Kassenbuch (`ledger-v6s7c`).

## What
Vorlage: Prototyp Version 17 (https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg), Sheet `import` in `sheetHtml`
(Zustände `tan`, `run`, `done`), Aktion `impGo`, `txRow` („doppelt?“), `homeHead`. Screenshots:
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/p-abrufen-tan-handy-412.png`,
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/p-abrufen-tan-pc-1280.png`,
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/p-abrufen-fertig-handy-412.png`,
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/p-abrufen-fertig-pc-1280.png`,
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/p-kassenbuch-nach-import-handy-412.png`,
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/p-kassenbuch-nach-import-pc-1280.png`.

**Server: ein Abruf bucht** (ADR 0093 Punkt 1 bis 8)
- Eine Funktion für den Abruf mit Buchen (Ports wie `ImportPorts`, Uhr und Berliner Tag als Parameter), aufgerufen
  von `startSync` bei `Connected`, von `confirmTan` und später vom Zeitplan (`banking-import-t4raw`). Schritte:
  Umsätze holen, Regeln (`RulesEngine.classifyTransactions`), YNAB-Buchungen des Import-Kontos lesen, sichere
  Duplikate überspringen, Abgleich gegen das Kassenbuch über die `import_id` (ADR 0093 Punkt 7, reine Funktion
  über den gefundenen Eintrag), Duplikaterkennung, Order-ID-Vorschlag (`OrderIdMatcher.applySuggestions`), dann je
  Buchung `EntryImportedFromComdirect` (`Approved = false`, `Uncleared`) mit den Kategorisierungs-Ereignissen
  (`EntryCategorizedByRule`, `EntryRecategorized` mit Actor `OrderIdMatch`), danach
  - bei `PossibleDuplicate`: `EntryHeldAsPossibleDuplicate` (Actor `ComdirectImport`, Payload nach ADR 0093
    Punkt 3), kein Push;
  - sonst der Push wie heute (`YnabClient.prepareTransactions`/`sendPrepared`, `pushComdirectImports`), dann
    `EntryLinkedToYnab` oder `EntryPushToYnabFailed`, danach `triggerMirrorInBackground`.
  Die Invariante Importschlüssel (`BB:` plus Reference ohne Bindestriche) bleibt unverändert.
- Ohne YNAB-Abgleich (`LoadYnabTransactions` liefert `None`) wird nichts gebucht, Meldung „YNAB ist nicht
  erreichbar, es wurde nichts gebucht.“ (ADR 0093 Punkt 8). Heute läuft der Abruf ohne Abgleich weiter.
- Ein Abruf zur Zeit: `ImportPorts.Gate` umschließt Abruf und Buchen. Ein zweiter Start während des Abrufs wartet
  und bekommt danach das Ergebnis des ersten (wie die Invariante offene Prüfung heute).
- Sitzung: `ReviewingTransactions` entfällt. Ablauf `AwaitingBankAuth` / `AwaitingTan` → `FetchingTransactions` →
  `ImportingToYnab` → `Completed` oder `Failed`. `SyncSession` trägt die Zahlen des Ergebnisses (neu, per Regel,
  aus Bestellnummer, ohne Kategorie, sichere Duplikate übersprungen, wartend, nicht in YNAB) und den Auslöser
  (`Manual`, ab `banking-import-t4raw` auch `Scheduled`). Sie wird wie heute in `sync_sessions` geschrieben
  (Zustand, ADR 0083 Abschnitt 8). `startSync` liefert `TanRequired` oder das fertige Ergebnis, `confirmTan` das
  Ergebnis; nach verlorener Antwort liest der Client es über `getCurrentSession` (Invariante verlorene Antwort
  bleibt).
- Wartende Buchung im Kassenbuch (ledger, nach ADR 0093 Punkt 5 und 6, Rat des `tactical-modeler`):
  - `Entry.PossibleDuplicate` (aus dem Ereignis), Spalte `possible_duplicate` in `ledger_entries` über
    `migrateLedgerEntryColumns`, `LedgerEntryView.PossibleDuplicate` (Grund und Treffer für die Karte in
    `ledger-v6s7c`).
  - Kontostand (`ledgerBalancesByAccount`, `loadAmountsForBalances`), Summe der Liste und Budget-Aktivität
    zählen sie mit wie jede Buchung (ADR 0093 Punkt 5, Roman 2026-11-11). Eine verworfene zählt nirgends. Der
    Worker prüft jede Stelle mit `deleted_in_ynab = 0`, ob sie eine verworfene sicher ausschließt, und listet sie
    im Outcome.
  - „Buchung nicht in YNAB“ (`entriesNotInYnab`, `loadEntriesNotLinkedToYnab`) zählt sie nicht. „Zu prüfen“
    (`countNeedsReviewEntries`) zählt sie ohne Änderung.
  - Jeder schreibende Kassenbuch-Befehl lehnt sie ab (Liste und Satz in ADR 0093 Punkt 6), mit einer Prüffunktion
    `Ledger.Domain.isHeldAsPossibleDuplicate` als erstem Zweig jeder Sperre; Sammelbefehle überspringen sie mit
    eigenem Grund und zählen sie im Toast mit.
  - `apply` lehnt `EntryLinkedToYnab` auf einer wartenden Buchung ab.
- Entfernt: die Sitzungs-Endpunkte `getTransactions`, `categorizeTransaction`, `skipTransaction`,
  `unskipTransaction`, `bulkCategorize`, `splitTransaction`, `clearSplit`, `setAskLena`, `importToYnab`,
  `forceImportDuplicates` samt Handlern (`src/Server/Api.fs`, `src/Server/ImportAskLena.fs`, soweit nur dafür),
  ihren Zeilen in `src/Tests/RemotingEndpointNegativeTests.fs` und ihren Tests. Was von `SyncTransaction` danach
  ungenutzt ist, fällt mit.
- Bugfix (aus `categorization-hz6vj`): eine nicht lesbare Kategorie-Id aus der YNAB-Historie lässt heute
  `Guid.Parse` in `OrderIdMatcher.applySuggestions` werfen (`src/Server/OrderIdMatcher.fs:61`) und bricht damit
  den Order-ID-Abgleich und den ganzen Abruf ab. Danach überspringt der Abgleich den Eintrag (Result statt
  Ausnahme, `.claude/rules/domain-purity.md`).

**Client: Sheet „Abrufen“ statt Sync-Seite**
- „Abrufen“ (Kopf der Übersicht und jede heutige Stelle) öffnet am Handy ein `BottomSheet`, ab 1024 px einen
  Dialog (`Modal`), jeweils über der aktuellen Seite. Titel „Abrufen“, Untertitel „Comdirect“, Schließen-Knopf.
  Zustände:
  - TAN: Hinweis in Gelb (`ErrorDisplay.notice`) „**Push-TAN bestätigen.** Öffne die Comdirect-App und gib den
    Zugriff frei.“, darunter „Danach ruft BB die Umsätze der letzten <n> Tage ab und gleicht sie mit dem
    Kassenbuch ab.“ (n aus `DaysToFetch`). Fuß: „Abbrechen“ (`cancelSync`) und primär „Ich habe bestätigt“
    (`confirmTan`).
  - Läuft: leerer Zustand „Rufe ab…“, „Umsätze werden geladen und mit Regeln kategorisiert.“
  - Fertig mit neuen Buchungen: „<n> neue Buchungen“ in Grün, darunter ein Satz aus den Zahlen der Sitzung, Teile
    mit 0 entfallen, Einzahl beachtet: „<a> per Regel kategorisiert, <b> ohne Kategorie. <c> sichere Duplikate
    übersprungen, <d> unsicheres wartet auf dich und geht erst danach nach YNAB.“ (Mehrzahl „<d> unsichere warten
    auf dich und gehen erst danach nach YNAB.“). Fuß: „Später“ (schließt) und primär „Jetzt durchgehen“.
  - Fertig ohne neue Buchung: „Nichts Neues“, „Seit dem letzten Abruf gibt es keine neuen Umsätze.“, Fuß wie oben.
  - Fehler: die Meldung des Servers (`ErrorDisplay.card` im Sheet) mit „Schließen“; bei Verbindungsverlust
    während des Abrufs startet der Server wie heute eine Challenge, das Sheet wechselt in den Zustand TAN.
- „Jetzt durchgehen“ führt bis `ledger-v6s7c` in das Kassenbuch mit Filter „Zu prüfen“ über alle Konten
  (dasselbe Ziel wie heute Zu-tun-Eintrag 1, `PresetNeedsReview`). Übergang, `ledger-v6s7c` stellt es um.
- Nach einem Abruf laden Zu tun, Zähler, Zeile „Comdirect · Import <Stand>“ und eine offene Kassenbuch-Liste neu.
- Kassenbuch: eine wartende Buchung trägt in der Zeile „doppelt?“ in Gelb (`Badge.notice` mit Wort, Prototyp
  `txRow`) statt „Noch nicht in YNAB“; das Detail zeigt eine gelbe Hinweisleiste „Vielleicht doppelt. Geht erst
  nach deiner Entscheidung nach YNAB.“ und sperrt seine Aktionen mit dem Satz aus ADR 0093 Punkt 6.
- Entfernt: die Seite `SyncFlow` (`src/Client/Components/SyncFlow/*` samt `InlineRuleForm`, `SplitSheet`,
  `TransactionList`, `TransactionRow`), ihre Route und ihre Client-Tests. „Abrufen“ (`FetchFromBank` in
  `src/Client/State.fs`) navigiert nicht mehr, es öffnet das Sheet.

## Acceptance criteria

### Abruf bucht (Handler mit Fake-Ports für Bank, YNAB und Kassenbuch, In-Memory-SQLite, Uhr als Parameter)
- [ ] GIVEN drei neue Umsätze, einer passt zu einer Regel, einer hat eine Bestellnummer mit Kategorie in der
      YNAB-Historie, einer nichts WHEN Abruf bei `Connected` THEN drei Buchungen mit `EntryImportedFromComdirect`
      (Approved false, Uncleared), `EntryCategorizedByRule` bzw. `EntryRecategorized` (Actor `OrderIdMatch`), ein
      Push mit den drei Buchungen, je `EntryLinkedToYnab`; die Sitzung ist `Completed` mit 3 neu, 1 per Regel,
      1 aus Bestellnummer, 1 ohne Kategorie.
- [ ] GIVEN ein Umsatz mit gefundener Reference in YNAB WHEN Abruf THEN keine Buchung, 1 sicheres Duplikat.
- [ ] GIVEN ein Umsatz mit Fuzzy-Treffer WHEN Abruf THEN die Buchung steht im Kassenbuch mit
      `EntryHeldAsPossibleDuplicate` (Grund, YNAB-Id, EntryId des Treffers, Schnappschuss), sie ist nicht im Push,
      die Sitzung zählt 1 wartend.
- [ ] GIVEN der Push scheitert WHEN Abruf THEN die Buchungen bleiben, je `EntryPushToYnabFailed`, die Sitzung zählt
      sie als „nicht in YNAB“, Zu-tun-Eintrag 5 zeigt sie.
- [ ] GIVEN dieselbe Reference ein zweites Mal, einmal wartend, einmal verworfen (Ereignis direkt im Testaufbau),
      einmal verknüpft, einmal unverknüpft mit Push-Fehlschlag WHEN Abruf THEN wartend und verworfen: kein Ereignis,
      kein Push; verknüpft: `AlreadyImported`; mit Fehlschlag: erneut gesendet. Keine Reference entsteht zweimal
      (reine Entscheidungsfunktion auf Domain-Ebene, dazu ein Handler-Fall je Zweig).
- [ ] GIVEN `LoadYnabTransactions` liefert `None` WHEN Abruf THEN kein Ereignis, kein Push, Sitzung `Failed` mit der
      Meldung aus dem What.
- [ ] GIVEN zwei Starts kurz hintereinander WHEN der erste noch abruft THEN der zweite wartet am Gate und liefert
      dasselbe Ergebnis, die Bank wird einmal gefragt.
- [ ] GIVEN eine Kassenbuch-Invariante scheitert beim Schreiben WHEN Abruf THEN die Sitzung endet `Failed` mit
      Meldung (`summarizeLedgerWriteFailures`), sie bleibt nie in `ImportingToYnab` stehen.
- [ ] Die bestehenden Invarianten Sync-Start, Konto-Id, Abbruch, Sitzungsende, Trennen und verlorene Antwort aus der
      README banking-import gelten weiter; ihre Tests laufen grün und sind auf das neue Ergebnis umgestellt.
- [ ] Jede Stelle, die den Live-Port des neuen Abrufs anschließt (`liveImportPorts`, Einzeiler in `Api.fs`), ist im
      Outcome aufgelistet und per Verhalten oder Strukturwächter belegt (Doktrin Punkt 3).

### Bugfix Order-ID (aus categorization-hz6vj)
- [ ] kaputt: GIVEN eine YNAB-Buchung der Historie mit Bestellnummer und Kategorie-Id „keine-guid“ WHEN Abruf THEN
      der Abruf bucht die neuen Umsätze, die betroffene Buchung bekommt keinen Order-ID-Vorschlag. Der
      Regressionstest (`src/Tests/OrderIdMatcherTests.fs`) ist vor dem Fix rot und nennt im Kommentar den Bug.
- [ ] intakt: GIVEN dieselbe Historie mit gültiger Kategorie-Id WHEN Abruf THEN der Vorschlag wird gesetzt wie heute.

### Wartende Buchung im Kassenbuch
- [ ] GIVEN eine wartende Buchung über 50 € auf dem Import-Konto WHEN Kontostände gelesen werden THEN sie zählt
      mit; WHEN die Aktivität ihrer Kategorie gelesen wird THEN sie zählt mit; WHEN `getEntriesNotInYnab` THEN
      sie fehlt; WHEN `NeedsReviewCount` THEN sie zählt.
- [ ] GIVEN eine wartende Buchung WHEN `setReviewMark`, `setEntryCategory`, `setEntrySplits`, `setEntryTransfer`,
      `setLenaFlag`, `setEntryMemo`, `setEntryAmount`, `deleteEntry`, `retryPushToYnab` und jeder `retry*WriteBack`
      THEN Fehler mit dem Satz aus ADR 0093 Punkt 6, kein Ereignis, kein YNAB-Aufruf (ein Fall je Befehl).
- [ ] GIVEN eine Auswahl mit einer wartenden Buchung WHEN `setReviewMarkBatch`, `setEntryCategoryBatch`,
      `setLenaFlagBatch` THEN sie wird übersprungen und mit eigenem Grund gezählt.
- [ ] GIVEN eine wartende Buchung WHEN `apply` ein `EntryLinkedToYnab` bekommt THEN Invariantenfehler.
- [ ] Replay: das Log einer wartenden Buchung ergibt denselben Zustand (Replay-Test um den Typ erweitert).
- [ ] Liste und Detail: „doppelt?“ in der Zeile, Hinweisleiste im Detail, gesperrte Aktionen (reine Funktion für
      Sichtbarkeit und Sperre, Client-Test unter .NET nach Doktrin Punkt 1).

### Sheet „Abrufen“
- [ ] Jedes sichtbare Element des Prototyp-Sheets `import` steht wie oben beschrieben da (Titel, Untertitel,
      Schließen, Hinweis in Gelb, Erklärsatz, Fußknöpfe, leere Zustände, Ergebnissatz in Grün).
- [ ] Der Ergebnissatz entsteht in einer reinen Funktion aus den Zahlen der Sitzung; Expecto-Fälle für jede Zahl 0,
      Einzahl und Mehrzahl je Teil, mit und ohne Bestellnummer, mit und ohne „nicht in YNAB“.
- [ ] Zustandswechsel durch das echte `update` (Start mit `TanRequired`, mit Ergebnis, verlorene Antwort, Abbrechen,
      Fehler, Verbindungsverlust im Abruf), als Aufzählung der Reihenfolgen nach Doktrin Punkt 20.
- [ ] „Jetzt durchgehen“ öffnet das Kassenbuch über alle Konten mit genau dem Filter „Zu prüfen“ (Root-`update`).
- [ ] Nach dem Abruf laden Zu tun, Zähler und Importzeile neu (Root-`update`).

### Endpunkte, Fixtures, Browser-Tests
- [ ] Die entfernten Endpunkte sind aus `SyncApi`, `Api.fs`, `RemotingEndpointNegativeTests.fs` und allen Tests
      verschwunden; geänderte (`startSync`, `confirmTan`) haben ihre Zeile in der Negativtabelle.
      `RemotingPipelineTests.fs` bleibt grün.
- [ ] Neue Pflichtfelder in `SyncSession`, `SyncStart` und `LedgerEntryView` sind in allen Fixtures unter
      `scripts/` im selben Schritt nachgezogen (`scripts/ledger-filter-bar.mjs` `buildEntryFixture`,
      `scripts/uebersicht.test.mjs`, `scripts/layout-states.mjs` und jede Fundstelle von `getSyncHistory`,
      `getCurrentSession`, `startSync`).
- [ ] Browser-Tests beim Umbau (Fahrplan Etappe 3): Die Seite Sync entfällt. Raus: `scripts/import-lena.test.mjs`
      (`[flow:import-lena-fragen]`), `scripts/import-split-target.test.mjs` (`[flow:import-uebertrag]`),
      `scripts/inline-rule-form.test.mjs` (`[flow:regel-aus-import]`), `scripts/import-pruefen-duplikat-details.test.mjs`
      (`[fable]`, die Aussage zu Datum und Betrag des Treffers aus dem Fable-Kompilat zieht mit `ledger-v6s7c` auf die
      Duplikat-Karte), `[flow:sync-starten]` in `scripts/sync-start.test.mjs`, und die drei bzw. vier Zeilen in
      `FLOWS`. Neu: Flow `abrufen` („Über Abrufen mit Push-TAN abrufen und das Ergebnis lesen.“) mit genau einem
      `[flow:abrufen]`-Test bei 412 px. Der `[fable]`-Test zur verlorenen Antwort in `scripts/sync-start.test.mjs`
      bleibt und wird gegen das Sheet neu geschrieben. Hat ein gelöschter Test Bugbezug, gilt Doktrin Punkt 8
      (Fix zurückdrehen, neuer Ort wird rot, im Outcome). Der Sweep bekommt die Zustände „Abrufen TAN“, „Abrufen
      fertig“ und „Kassenbuch mit wartender Buchung“ bei 412 und 1280 px.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten (Notes). Screenshots von BB bei 412 und
      1280 px (Sheet TAN, Sheet fertig, Kassenbuch nach dem Import) neben den Prototyp-Screenshots oben im Outcome.
- [ ] README banking-import: Sitzung, Invarianten und Begriffe auf den Abruf mit Buchen umgestellt, „Invariante
      offene Prüfung“ ersetzt. README ledger: **Unsicheres Duplikat („doppelt?“)**, Ereignis
      `EntryHeldAsPossibleDuplicate`, Sperre, Kontostand, *BuchungÜbernehmen* (Comdirect-Import-Push) ohne
      `importToYnab` und `forceImportDuplicates`. Jedes Delta vor dem Integrieren durch `npm run check:readme-delta`
      und die grep-Prüfung aus CLAUDE.md.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [ ] Roman hat „Abrufen“ am Handy (412 px, Touch) und am PC neben dem Prototyp angesehen. [human-eye]

## Notes
Schnitt 4 aus Etappe 3 (Fahrplan, Roman 2026-11-11). Nimmt `categorization-hz6vj` auf (Bugfix oben, Rest in der
Kann-Sammlung categorization). `banking-import-t4raw` ist als eigenes Kind umgeschrieben (automatischer Abruf).
Die Befehle „Ist neu“, „Verwerfen“ und Rückgängig für wartende Buchungen kommen mit `ledger-r7h2w`, die
Duplikat-Karte mit `ledger-v6s7c`. Bis dahin warten unsichere Duplikate ohne Ausweg; das ist sicher, weil nichts
nach YNAB geht.

Größe: Server und Client hängen über `SyncApi` zusammen (entfernte Endpunkte brechen den Client), eine Teilung in
Server und Client ließe einen Stand ohne Bedienung zurück. Abgeteilt sind Ausweg aus dem Warten (`ledger-r7h2w`),
Zeitplan (`banking-import-t4raw`) und Payee ändern (`ledger-fxt09`).

**Bewusste Lücken bis zu späteren Schnitten** (Roman, 2026-11-11, Entscheidung 3): Mit dem Sync-Ablauf entfallen
Payee ändern (kommt mit `ledger-fxt09`), Regel aus der Zeile (kommt mit `categorization-n3sxw`), Lena vor dem
Import (geht im Kassenbuch), Aufteilen vor dem Import samt Barabhebung (geht im Kassenbuch, Split-Editor), Filter
der Prüfliste (Kassenbuch-Filter). Weiter gilt der Order-ID-Vorschlag beim Import. Die Weitergabe einer Kategorie an
Buchungen derselben Bestellnummer entfällt (Kann-Sammlung categorization).

**Abweichungen vom Prototyp Version 17**
- (a) entfällt: Eine wartende Buchung zählt im Kontostand mit wie im Prototyp (Roman, 2026-11-11, ADR 0093 Punkt 5).
- (b) Der Ergebnissatz nennt zusätzlich „<n> aus Bestellnummer“ und „<n> noch nicht in YNAB, sie stehen unter Zu
  tun.“, wenn die Zahl über 0 ist; die Zahl der sicheren Duplikate kommt aus dem Abruf (im Prototyp fest „2“).
  Grund: BB kann beides, Roman soll gescheiterte Pushes sehen.
- (c) Ein Fehlerzustand im Sheet. Grund: der Prototyp hat keinen, BB zeigt Fehler heute.
- (d) „Jetzt durchgehen“ führt bis `ledger-v6s7c` ins Kassenbuch mit Filter (Übergang).
- (e) Im Kassenbuch steht bei einer wartenden Buchung zusätzlich eine Hinweisleiste im Detail. Grund: der Prototyp
  hat im Detail keinen Fall „doppelt?“, die gesperrten Aktionen brauchen einen Grund.

`DaysToFetch` bleibt; der Zeitraum steht im TAN-Satz („letzten 30 Tage“ im Prototyp).

Der Conductor legt vor dem Start Prototyp und Screenshots in den Scratchpad (Pfade oben) und gibt sie Worker,
Verifier und `design-quality` mit.

## Merge-conflict note (iteration 1)
Nach Iteration 1 (vor der Verifikation) wurde `main` in den Worktree gemergt, weil im selben Batch ledger-fxt09 (Payee ändern, 0a7d9cf0) und budgeting-w9yt4 (Angepinnte Kategorien, 828f6a24) integriert wurden. Konflikte: `src/Client/View.fs`, `src/Server/Ledger/Persistence.fs`. Zusätzlich verlangt ADR 0093 Punkt 6, dass `setEntryPayee` (aus fxt09) und `retryPayeeWriteBack` eine wartende Buchung ablehnen. Sicherung des Stands vor dem Merge: `.agentheim/salvage/banking-import-d0fey-merge-conflict.patch`.

## Verifier note (iteration 1)
**REASONS:** README-Sync (Check 5): Das ledger-Delta ist nach dem Merge von main veraltet. (a) README_DELTA_LEDGER, Bullet **Unsicheres Duplikat („doppelt?“)**: die Sperrliste nennt den Payee nicht, der Code sperrt `setEntryPayee` aber (ADR 0093 Punkt 6, Test in `LedgerHeldDuplicateTests.commands`). (b) Der Bullet **Payee ändern** in der ledger-README auf main sagt „Gesperrt (`payeeLockReason`): … nicht mit YNAB verknüpft (auch eine wartende Buchung nach ADR 0093) …“; nach dem Merge wird eine wartende Buchung zuerst über `isHeldAsPossibleDuplicate` (Server) bzw. `heldLockReason` in `payeeLockFor` mit `possibleDuplicateLockedText` abgelehnt. Gleiches für *PayeeÄndern* unter `## Key commands`. Alles andere bestanden (verify PASS, keine Marker, Geschwister erhalten, ADR 0093 belegt, Importschlüssel, Deltas grün im Gate).
**SUGGESTED_FIX:** Sperrliste von **Unsicheres Duplikat** um „Payee“ ergänzen; je ein replace-Op für **Payee ändern** und *PayeeÄndern*. grep-Prüfung aus CLAUDE.md gegen die ledger-README auf main wiederholen, `readme-delta-check --bc ledger` erneut. Code und Tests brauchen keine Änderung.
**ITERATION_HINT:** likely-fixable

## Outcome

Ein Abruf bucht jetzt direkt (ADR 0093 Punkt 1 bis 8), die Prüfsitzung samt ihren zehn Endpunkten ist weg, „Abrufen“ ist ein Sheet über der aktuellen Seite. `./verify.sh` nach dem Merge von main (828f6a24): VERIFY: PASS (dotnet 4612 grün, node 313 grün, Builds und beide Wächter grün).

**Server**
- `src/Server/BankImport.fs`: `fetchAndBook` macht in dieser Reihenfolge: Umsätze holen, Regeln anwenden, YNAB lesen, gegen das Kassenbuch abgleichen (`knownComdirectImports`, zwei Abfragen statt N+1), dann über die reine Funktion `planImport` Duplikate und Order-ID prüfen, `holdOf` und `candidatesOf` bauen, Kassenbuch zuerst, Push, Verknüpfung über den Port `BookImports`, am Ende die Zahlen über `countsOf`. Ohne YNAB-Abgleich wird nichts gebucht („YNAB ist nicht erreichbar, es wurde nichts gebucht.“). Ein zweiter `startSync` während eines Abrufs wartet am `Gate` und bekommt dessen Ergebnis. `confirmTan` liefert die beendete Sitzung. Jeder Fehlschlag (auch ein gescheitertes Kassenbuch-Schreiben, `summarizeLedgerWriteFailures`) endet `Failed` und steht in `sync_sessions`. `BankImport.fs` wird jetzt nach `Ledger/CommandHandlers.fs` kompiliert.
- `src/Shared/Domain.fs`: `SyncSession` trägt `Trigger` (`Manual`) und `Counts` (`FetchCounts`), `ReviewingTransactions` ist weg, `SyncStart` hat `TanRequired | Fetched`. `PossibleDuplicateView`, `possibleDuplicateLockedText`, `HeldCount` in `ReviewMarkBatchOutcome` und `FlagBatchOutcome`, `SkipPossibleDuplicate`. Aus `SyncTransaction` entfallen `AskLena`, `YnabImportStatus` und `UserNotes`, ebenso `YnabImportStatus`, `YnabRejectionReason` und `SyncError.AskLenaRejected`. `SyncApi` (`src/Shared/Api.fs`) hat nur noch startSync, getCurrentSession, cancelSync, getBankConnectionStatus, disconnectBank, confirmTan und getSyncHistory. `ImportResult` ist weg.
- Kassenbuch: Neues Ereignis `EntryHeldAsPossibleDuplicate` mit Codec, `Entry.PossibleDuplicate` und der Spalte `possible_duplicate` (`migrateLedgerEntryColumns`). `isHeldAsPossibleDuplicate` ist der erste Zweig in 15 Befehlen, `knownImportDecision` ist eine reine Funktion. `apply` lehnt `EntryLinkedToYnab` auf einer wartenden Buchung ab und ebenso das Zurückhalten einer schon verknüpften. `pushComdirectImports` hat einen `Hold`-Kandidaten und die Ergebnisse `HeldAsPossibleDuplicate` und `SkippedKnownImport`; `ExistingEntryImportId` (Force-Import) ist weg. Sammelbefehle zählen wartende Buchungen eigens.
- `sync_sessions` hat neue Spalten für die Zahlen und `triggered_by` (Migration in `initializeDatabase`). `Persistence.SyncTransactions` ist entfernt, die Tabelle `sync_transactions` bleibt ungenutzt stehen.
- Entfernt: `ImportAskLena.fs`, `PushOutcomeClassification.fs`, `YnabClient.applyLenaFlag`, `OrderIdMatcher.propagateInSession`, `Api.prepareForImport` und `buildComdirectImportCandidates` sowie alle Sitzungs-Endpunkte samt Negativzeilen und Tests.
- Bugfix (categorization-hz6vj): `OrderIdMatcher.applySuggestions` überspringt eine Kategorie-Id, die keine Guid ist (`Guid.TryParse`). Der Regressionstest „kaputt“ in `src/Tests/OrderIdMatcherTests.fs` war vor dem Fix rot (FormatException aus `Guid.Parse`), dazu „intakt“ und ein Handler-Fall in `BankImportTests` (der Abruf bucht trotzdem).

**Wo der Live-Port angeschlossen ist** (Doktrin Punkt 3), jede Stelle per Strukturwächter `apiWirings` in `src/Tests/StructureGuardTests.fs` belegt, je mit einem eigenen Gegenbeweis-Ersatz: `LoadYnabTransactions = loadYnabTransactionsForImport`, `Complete = SyncSessionManager.completeSession`, `LoadTarget = fun () -> liveImportTargetWith Persistence.Settings.getSetting` mit `TriggerMirror = Ledger.CommandHandlers.triggerMirrorInBackground`, `match! importTargetWith setting 0 with`, `Push = sendPrepared token budgetId`, `BookImports = Ledger.CommandHandlers.pushComdirectImports`. Weiter belegt bleiben `Bank = BankPort.comdirectBankPort ComdirectAuthSession.livePorts`, `Today = liveBerlinDay`, `startSync` und `confirmTan` an `liveImportPorts`. Das Verhalten ist mit Fake-Bank, Fake-YNAB und In-Memory-Kassenbuch getestet (`BankImportTests`: Listen „Abruf bucht ins Kassenbuch und nach YNAB“ und „Wiederholter Abruf“).

**Stellen mit `deleted_in_ynab = 0`** (eine verworfene Buchung soll nirgends zählen). Mit ledger-r7h2w wird „Verwerfen“ die Buchung als gelöscht markieren (wie `EntryDiscardedInBb`, `DeletedInYnab = true`), dann schließt jede dieser Stellen sie aus: `loadEntriesNotLinkedToYnab` (zusätzlich `possible_duplicate IS NULL`), der Seitenfilter in `loadEntriesPage`/`entryFilterOfPageQuery` (Zeile 1255), `loadFirstEntryDate`, `loadCategoriesEntries` (beide Zweige, Aktivität), `countNeedsReviewEntries` (beide), der Lena-Zähler (beide), `loadPayeeSuggestions` und `loadLastPayeeCategory`, der Gelöscht-Zähler (`= 1`). `loadAmountsForBalances` liest alle Zeilen, und `ledgerBalancesByAccount` schließt gelöschte aus. Keine Stelle zählt eine verworfene Buchung mit. Eine wartende Buchung zählt in Kontostand, Liste, Aktivität und „Zu prüfen“, aber nicht in „Buchung nicht in YNAB“ (`LedgerHeldDuplicateTests`).

**Client**
- `src/Client/Components/Fetch/` (Types, State, View): das Sheet „Abrufen“. Am Handy ist es ein `BottomSheet`, ab 1024 px ein `Modal` (`Viewport.isInspectorPanelViewport`). Es zeigt die Zustände TAN, Läuft, Ergebnis, Nichts Neues und Fehler. Die Logik ist rein (`sheetScreen`, `resultTitle`, `resultSentence`, `tanExplanation`), und `updateWith` bekommt seine Ports als Funktionswerte. Das Nachlesen nach verlorener Antwort deckt `startSync` und `confirmTan` ab.
- Root (`src/Client/State.fs`, `View.fs`): `FetchFromBank` öffnet das Sheet. `fetchExternalCmd` lädt nach dem Abruf die Übersicht neu, auf dem Kassenbuch zusätzlich Liste und Stände. „Jetzt durchgehen“ führt zu `OpenOverviewTodo TodoReview`. Die Quick-Add-Kategorien liegen jetzt im Root (`QuickAddCategories`). Die Seite und Route `/sync` und alles unter `Components/SyncFlow/` sind entfernt.
- Kassenbuch: In der Zeile steht „doppelt?“ (`Badge.Warning`, `RowPossibleDuplicate`/`StatusPossibleDuplicate`). Im Detail steht die Leiste `PossibleDuplicateNotice` („Vielleicht doppelt.“, „Geht erst nach deiner Entscheidung nach YNAB.“). Alle Aktionen sind über `heldLockReason` gesperrt: Kategorie, Split, Lena, Memo, Betrag, Prüf-Schaltfläche, Wisch und Geprüft-Schalter. Die Sammel-Toasts nennen „<n> vielleicht doppelt“.
- DS: zwei neue Varianten. `ErrorDisplay.noticeWithLead` ist der gelbe Hinweis mit fettem Anfang, `Card.sheetMessage` der Kopf und Satz mittig ohne Rahmen (Prototyp `.empty`, grün bei neuen Buchungen).

**Tests**: neu sind `FetchSheetTests.fs` (Ergebnissatz je Teil 0, Einzahl und Mehrzahl, mit und ohne Bestellnummer, mit und ohne „nicht in YNAB“, Zustandswechsel, Takt beim Nachlesen), `LedgerHeldDuplicateTests.fs` (Codec, apply, Replay, Entscheidung, Lesen, alle 15 Befehle einzeln, 3 Sammelbefehle) und `LedgerHeldDuplicateClientTests.fs`. `SyncStartOrderTests.fs` ist als Aufzählung aller Reihenfolgen für das Sheet neu geschrieben (Doktrin Punkt 20), dazu kommen die Handler-Fälle in `BankImportTests.fs`.

**Browser-Tests**: Entfernt sind `import-lena`, `import-split-target`, `inline-rule-form`, `import-pruefen-duplikat-details` und die FLOWS sync-starten, import-lena-fragen, import-uebertrag und regel-aus-import. Neu ist der Flow `abrufen` mit genau einem `[flow:abrufen]`-Test bei 412 px Touch in `scripts/sync-start.test.mjs`. Der `[fable]`-Test zur verlorenen Antwort ist gegen das Sheet neu geschrieben. Der Sweep hat die neuen Zustände `abrufen-tan`, `abrufen-fertig` und `kassenbuch-wartend` (320, 412, 1280 px), ohne Verletzung. Der Gegenbeweis für Invariante 9 ist von `sync-start` nach `uebersicht-ohne-minus` @1280 umgezogen. In den Fixtures sind `PossibleDuplicate` und `HeldCount` nachgezogen.

**Doktrin Punkt 8 (Tests mit Bugbezug)**
- Umgezogen, und der neue Ort wird rot, wenn ich den Fix lokal zurückdrehe (danach wiederhergestellt):
  - infrastructure-p747v: früher `prepareForImport`-Tests in `YnabClientTests`. Mit `request.Now.Date` statt `request.Today` beim Vorbereiten wird „eine Buchung vom Berliner Tag wird gebucht, auch wenn die Uhr in UTC noch den Vortag zeigt“ (BankImportTests) rot.
  - design-system-k6kfw (doppelter Start): Wird der Wächter in `Opened` zu `if false` abgeschaltet, werden in `SyncStartOrderTests` die Fälle „ohne Sitzung“ und „mit beendeter Sitzung“ rot.
  - Review-Fund „Salden nach dem Import“ (früher `importChangesBalances`): Ohne `ReloadRequested` in `fetchExternalCmd` wird „Nach einem Abruf laden Übersicht, Zähler, Importzeile …“ (OverviewTests) rot.
- Ersatzlos entfernt, weil die Funktion entfiel:
  - Zustand `sync-start` (Fix 31f15e0, Zeile zum Bankstand auf `/sync`)
  - SyncFlowStateTests B7/B4 (Bankstand im Sync-Ablauf; der Bankstand bleibt in den Einstellungen mit eigenen Tests)
  - categorization-f955d am Regelformular der Importzeile (der Fall im Regel-Dialog bleibt)
  - ynab-004 und der Cashback-Vorzeichen-Fall im Import-Split-Editor (der Kassenbuch-Split-Editor hat eigene Tests)
  - ledger-mshd8, die Force-Import-Bugs (ledger-fgw2q, ledger-fq3kd) und ImportAskLena

**Seite neben der Skizze verglichen.** BB-Screenshots: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/banking-import-d0fey/bb-abrufen-tan-handy-412.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/banking-import-d0fey/bb-abrufen-tan-pc-1280.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/banking-import-d0fey/bb-abrufen-fertig-handy-412.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/banking-import-d0fey/bb-abrufen-fertig-pc-1280.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/banking-import-d0fey/bb-kassenbuch-nach-import-handy-412.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/banking-import-d0fey/bb-kassenbuch-nach-import-pc-1280.png. Prototyp daneben: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/p-abrufen-tan-handy-412.png und die übrigen p-abrufen-*/p-kassenbuch-nach-import-*. Abweichungen sind nur die in der Task genannten (b) bis (e), dazu zwei DS-bedingte:
- Der gelbe TAN-Hinweis trägt das Warnsymbol und den blasseren Rand aus `ErrorDisplay.notice`, die Task nennt `ErrorDisplay.notice` ausdrücklich.
- Der Dialog am PC dunkelt den Hintergrund mit Unschärfe ab (`Modal`).
Den Blick am Handy (412 px, Touch) und am PC neben dem Prototyp kann nur Roman tun, dieses Kriterium ist offen [human-eye].

**Für den Conductor beim Integrieren**
- ledger-fxt09 (parallel) bringt `setEntryPayee`. Dessen `loadEntry`-Zweig braucht beim Merge `| Some entry when isHeldAsPossibleDuplicate entry -> return Error possibleDuplicateLockedText` als ersten Zweig (ADR 0093 Punkt 6 nennt Payee), dazu einen Fall in `LedgerHeldDuplicateTests.commands`.
- Von Hand nachziehen, weil die Bullets keinen fetten Kopf haben:
  - ledger README unter `## Key commands`: In *BuchungÜbernehmen* wird „aus der Sync-Pipeline“ zu „aus dem Abruf mit Buchen“. Der Absatz „Comdirect-Import-Push (`Server.Api.importToYnab` …)“ und der Schluss „`importToYnab` schließt die Session nicht ab“ werden auf `fetchAndBook` umgestellt (die Sitzung endet `Failed`). Der Bullet *BuchungÜbernehmen (Force-Import)* wird gelöscht.
  - categorization README `## Key commands`: *PropagateOrderId* löschen.
  - ynab-sync README: „Rückfrage Lena beim Import“, „Konten im Import“ und *SetAskLena* sind entfallen.
  - design-system README: „Ort und Navigation“ nennt noch den „Sync-Ablauf unter Übersicht“.
  - infrastructure README: „Lesende Abfrage mit Wiederholung“ nennt noch „SyncFlow“.
- Außerhalb von `.agentheim/` habe ich die Verweise auf `src/Client/Components/SyncFlow/State.fs` in `.claude/agents/frontend-architecture.md` und `standards/frontend/overview.md` auf `SessionReadVersion` in `Components/Fetch/State.fs` umgestellt; ohne das war die Harness-Prüfung rot.
- Die README-Deltas sind gegen `npm run check:readme-delta` geprüft (banking-import, ledger und categorization jeweils Exit 0). Das ledger- und das categorization-Delta stehen in eigenen Blöcken (`README_DELTA_LEDGER`, `README_DELTA_CATEGORIZATION`, jeweils `document: README.md` des genannten BC).


**Iteration 2: Merge von main (ledger-fxt09 Payee ändern, budgeting-w9yt4 Angepinnte Kategorien)**
- Konflikt in `src/Client/View.fs`: Beide Absichten bleiben. Die Übersicht bekommt `OpenPinned` und `Dispatch` aus w9yt4, der Zweig `SyncFlow` bleibt entfernt.
- Konflikt in `src/Server/Ledger/Persistence.fs`: Das Upsert führt jetzt beide Spalten, `payee_write_back_failure` aus fxt09 und `possible_duplicate`.
- Payee-Sperre (ADR 0093 Punkt 6): `setEntryPayee` und `retryPayeeWriteBack` lehnen eine wartende Buchung als ersten Zweig mit `possibleDuplicateLockedText` ab. Im Client prüft `payeeLockFor` zuerst `heldLockReason`. Dazu je ein Fall in `LedgerHeldDuplicateTests.commands` und ein Satz in `LedgerHeldDuplicateClientTests`. Ohne die Sperre wäre der Fall rot, weil `payeeLockReason` einen anderen Satz liefert (noch nicht verknüpft).
- Was git nicht als Konflikt meldete, habe ich nachgezogen: In `LedgerPayeeTests` wurde `ExistingEntryImportId` zu `Hold`. Das Root-Modell in `OverviewPinsTests` hat jetzt `Fetch` und die Quick-Add-Kategorien statt `SyncFlow`. `LedgerPayeeClientTests` hat `PossibleDuplicate = None`, `LedgerHeldDuplicateClientTests` hat `PayeeWriteBackFailure = None`.
- Bei den übrigen Stellen ohne Konflikt fand ich nichts mehr zu tun: die Fixtures unter `scripts/` (beide Felder vorhanden), `browser-usage.mjs` (FLOWS), `RetryTests`, `RemotingEndpointNegativeTests`, `layout-states.mjs` und die Übersicht mit Pins und „Abrufen“. Gegen die Seite Sync verweisen nur noch Kommentare in alten Tests.
- Die README-Deltas sind gegen die READMEs auf main erneut geprüft (banking-import, ledger und categorization, jeweils Exit 0).
- Der Punkt „ledger-fxt09 braucht beim Merge die Sperre“ unter „Für den Conductor“ ist damit erledigt.
- Verifier-Hinweis Iteration 1 (nur Doku), umgesetzt in README_DELTA_LEDGER: Die Sperrliste von **Unsicheres Duplikat („doppelt?“)** nennt jetzt den Payee. Neu ist ein replace-Op für **Payee ändern** (`## Ubiquitous language`): Eine wartende Buchung lehnt zuerst `isHeldAsPossibleDuplicate` bzw. `heldLockReason` in `payeeLockFor` mit `possibleDuplicateLockedText` ab, erst danach greift `payeeLockReason`; der Rest des Bullets ist wortgleich. `npm run check:readme-delta -- --bc ledger` endet mit Exit 0. Die grep-Prüfung nach `payeeLockReason` und „auch eine wartende Buchung“ in der ledger-README auf main findet nur diesen Bullet und *PayeeÄndern*. Code und Tests sind unverändert.
- Für den Conductor, von Hand (der Bullet hat keinen fetten Kopf, das Delta-Gate erreicht `## Key commands` nicht per replace, Prüfung 6): Im Bullet *PayeeÄndern* (ledger-README, `## Key commands`, Zeile 349 auf main) den Satz „Abgelehnt ohne Ereignis und ohne Push: unbekannte Buchung, leerer oder zu langer Payee und was `payeeLockReason` sperrt.“ ersetzen durch „Abgelehnt ohne Ereignis und ohne Push: unbekannte Buchung, als erster Zweig eine wartende Buchung (`isHeldAsPossibleDuplicate`, Satz `possibleDuplicateLockedText`, ADR 0093 Punkt 6), leerer oder zu langer Payee und was `payeeLockReason` sperrt.“
**Für die Kann-Sammlung**
- banking-import: Die Tabelle `sync_transactions` liest und schreibt niemand mehr. `TransactionStatus` (`ManualCategorized`, `Imported`) ist im Abruf weitgehend tot.
- banking-import: Quick Add zeigt keine zuletzt genutzten Kategorien mehr, sie kamen nur aus dem Sync-Ablauf.
- banking-import: Der Satz „<n> sichere Duplikate übersprungen“ zählt auch Umsätze, die schon vor BB in YNAB standen. Bei jedem Abruf im Zeitraum kommen sie wieder.
- design-system: `noticeWithLead` hat ein Symbol, der Prototyp nicht. Es ist eine Variante ohne Symbol nötig, wenn Roman die Skizze genau will.
