---
id: infrastructure-x7p2e
title: "Expecto wieder schnell: testSequenced nur bei geteiltem Zustand, lange Aufzählungen teilen, Ziel höchstens 30 s"
status: done
type: chore
context: infrastructure
created: 2026-11-20
completed:
depends_on: []
blocks: []
tags: [captured, verify, gate, laufzeit, expecto, tests]
related_adrs: [0100]
related_research: []
prior_art: [infrastructure-d1dwb, infrastructure-mshaf]
---

## Why
Schritt 2 des Gates (Expecto über die Tests.dll) brauchte nach infrastructure-d1dwb am 2026-11-05 14 s, am
2026-11-20 sind es 65 s. Das Gate dauert dadurch rund 6 statt 4 Minuten, bei jedem work-Lauf und jedem Verifier.
Roman hat am 20.11. gefragt, warum das Gate wieder so lange dauert, und die Task bestellt.

Messung 2026-11-20 (Expecto allein, Last um 8 auf 10 Kernen, JUnit-Bericht über `--junit-summary`):
- 5.495 Fälle, Summe der Einzelzeiten 175 s, Wandzeit 65 s, CPU (user) 75 s. Der Lauf nutzt im Schnitt gut einen
  Kern.
- **Sequenzielle Listen:** 101 erkannte `testSequenced`-Listen mit 921 Fällen und zusammen rund 34 s (am 5.11. 67
  Aufrufe von `testSequenced`, heute 119 in 47 Dateien). Expecto führt sie nacheinander aus, das ist rund die Hälfte
  der Wandzeit. Größte: „Targets anwenden: Abläufe (h2zfn)“ 5,4 s (`TargetsApplyTests.fs:360`), „Verschieben:
  API-Grenze“ 3,6 s (`MoveMoneyTests.fs:621`), „Zuweisen: API-Grenze“ 2,5 s (`MoveMoneyTests.fs:935`), „Pause:
  Abläufe“ 1,7 s (`TargetPauseTests.fs:219`), „Zuweisen: Abläufe“ 1,6 s, mehrere „Schreib-Sperre“-Listen mit 0,6 bis
  0,9 s. Die vier größten rufen je Testfall `initializeBudgetingSchema ()` auf der geteilten In-Memory-DB auf und
  zählen Zeilen über die Ids des Falls.
- **Lange Einzelfälle** setzen die Untergrenze des parallelen Teils: „Budget führen: Abläufe (dgrp7); Reihenfolgen
  von Lesen und Senden; Jede Folge aus drei Schritten hält O1 bis …“ 16,2 s, „Tab-Daten: Generationsregel über alle
  Reihenfolgen“ 7,1 s, „Kassenbuch-Seite: Reduzierer; Über alle Reihenfolgen aus Tippen, Ablauf und Entfernen“ 4,9 s,
  „Regeln: Reihenfolge in jeder Reihenfolge“ 4,4 s, „Payee ändern: ein Aufruf zugleich; über alle Reihenfolgen …“
  4,3 s.

Rechnung: Läuft alles parallel, liegt die Untergrenze bei rund 16 bis 18 s (längster Fall; 175 s Testzeit auf 10
Kernen), dazu der Start der Tests.dll.

## What
Zwei Hebel, in dieser Reihenfolge:

1. **`testSequenced` nur, wo Tests wirklich Zustand teilen** (Test-Doktrin Punkt 16: Isolation kommt aus eindeutigen
   Ids, Reihenfolge innerhalb einer Liste nur mit `testSequenced`). Für jede Liste den Grund bestimmen. Typische
   echte Gründe: ein Fall liest eine Tabelle ohne Filter auf seine Ids (Zählung über alles), ein globaler Zähler oder
   Schalter, das Server-Log (`testSequencedGroup "Server-Log"`), eine Schema-Initialisierung, die gleichzeitig nicht
   sicher ist. Wo der Grund fehlt, die Markierung entfernen. Wo er sich billig auflösen lässt (Abfrage auf die Ids des
   Falls einschränken, eigene Ids statt geteilter Konstanten), ihn auflösen und die Markierung entfernen. Wo ein Grund
   bleibt, steht er als Kommentar über `testSequenced`.
2. **Lange Aufzählungen teilen.** Die fünf längsten Fälle oben (Reihenfolgen-Tests nach dem Muster `moveWays`,
   Doktrin Punkt 20) so in mehrere Testfälle zerlegen, dass Expecto sie über die Kerne verteilt, etwa ein `testCase`
   je erstem Schritt oder je Block von Folgen. Orakel und Menge der geprüften Folgen bleiben gleich.

Nicht Teil der Task:
- Zeitgeber-Tests über `CmdTiming` (bis 6 s echte Wartezeit) laufen im parallelen Teil und enden vor dem längsten
  Fall. Ihre Dauern sind Konstanten im Produktionscode (`categoryCardDurationMs`, `bankDisconnectConfirmMs`); sie
  nur für Tests umzubauen lohnt nicht.
- Expecto parallel zum Client-Strang (infrastructure-pgpb6 in infrastructure-mshaf).
- `verify.sh` selbst bleibt unverändert.

## Acceptance criteria
- [ ] Messtabelle im Outcome, vorher und nachher auf derselben Maschine bei Last unter 10 (`uptime` im Outcome):
      Wandzeit von Expecto, CPU (user), Zahl und Summe der sequenziellen Listen, die fünf längsten Fälle. Gemessen mit
      `USE_MEMORY_DB=true TZ=Europe/Berlin dotnet src/Tests/bin/Debug/net10.0/Tests.dll --junit-summary <datei>`,
      je Seite mindestens zwei Läufe.
- [ ] Expecto braucht nachher höchstens 30 s Wandzeit (Median der Läufe aus der Messtabelle).
- [ ] Jede Liste, die `testSequenced` oder `testSequencedGroup` behält, hat direkt darüber einen Kommentar mit dem
      Grund (was geteilt wird). Jede entfernte Markierung steht im Outcome mit Datei, Liste und dem Grund, warum sie
      parallel sicher ist.
- [ ] Die Zahl der Fälle bleibt gleich oder wächst nur durch das Aufteilen. Für jede geteilte Aufzählung steht im
      Outcome, dass die Menge der geprüften Folgen gleich geblieben ist (Zahl der Folgen vorher und nachher).
- [ ] Zehn Expecto-Läufe hintereinander sind grün, zwei davon parallel zu `npm run test:scripts`. Ein Fall, der in
      einem Lauf rot war, steht mit Name und Fehlermeldung im Outcome und ist behoben oder bekommt den Grund für
      `testSequenced` (bekannter Kandidat: `BudgetTakeoverTests.fs:529` „die Wartefunktion des Servers wartet
      wirklich“, Nachtrag in infrastructure-mshaf).
- [ ] Kein Produktionscode unter `src/Server`, `src/Client` oder `src/Shared` geändert.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Stop-Loss: Liegt Expecto schon nach Hebel 1 bei höchstens 30 s, wird Hebel 2 nur noch für den 16-s-Fall umgesetzt
  und der Rest als Zeile in die Kann-Sammlung infrastructure (infrastructure-hgbey) geschrieben.
- Wie Expecto sequenzielle Listen ausführt: Fälle unter `testSequenced` laufen nicht parallel zueinander und nicht
  parallel zum Rest. Vor dem Umbau einmal prüfen, ob das in der verwendeten Expecto-Version so ist (Wandzeit mit und
  ohne eine große Liste vergleichen), damit die Rechnung oben stimmt.
- Unter Last (über 20) blieb Expecto früher mehrfach hängen (Nachtrag 2026-11-06 in infrastructure-mshaf). Hängt ein
  Lauf, ist das ein Befund für das Outcome und kein Grund, Markierungen ungeprüft zurückzusetzen.
- Am 20.11. hat Roman den Layout-Sweep auf 412 und 1440 px verkleinert (512a1cf0), test:scripts von 288 auf 204 s.
- Refine 2026-11-20: Hebel 3 (Zeitgeber kürzen) gestrichen, siehe „Nicht Teil der Task“.


## Outcome

Expecto braucht nachher rund 29,5 s statt 65 s (Median aus acht Läufen hintereinander, Last 7 bis 12). Kein
Produktionscode unter `src/Server`, `src/Client` oder `src/Shared` geändert. ADR `0100-db-testlisten-als-expecto-gruppe.md`
(vorläufige Nummer).

### Befund

- Expecto 10.2.1 führt `testSequenced` so aus, wie die Task rechnet: erst alle parallelen Fälle, dann die
  sequenziellen einzeln. Probe mit `--filter-test-list`: „Targets anwenden: Abläufe (h2zfn)“ allein 0,5 s, zusammen
  mit „Budget führen: Abläufe (dgrp7)“ 4,5 s und danach 4,6 s für h2zfn, hintereinander.
- Die sequenziellen Listen waren nicht von sich aus langsam. Die Aufzählung „Jede Folge aus drei Schritten“
  (`BudgetLeadTests.fs`, 1.539 Folgen, je ein eigener Monat) schrieb rund 1.500 Monats-Streams in die gemeinsame
  In-Memory-DB. Budget-Abfragen über alle Monate (`queryMovements` ohne Monat, `loadBudgetEventsOn … OfTypes`) lesen
  und dekodieren danach alle diese Ereignisse, rund 125 ms je Aufruf. Probe: nur diesen Fall übersprungen, Expecto
  37 s statt 65 s, die sequenziellen Listen 18,7 s statt 34 s.
- DB-Listen vertragen untereinander keine Parallelität. Probe mit allen `testSequenced` entfernt: 140 s, 11 rote
  Fälle (Tabellensperren des Shared Cache, dazu Leser über alle Zeilen): „Kassenbuch-Seite: seitenweise Liste und
  Kontostaende (Persistence); Kontostaende aller Konten …“ (Kontostand B erwartet −11, war 0), „Lena fragen: Filter,
  Zähler und Historie; Historie …“, „Lena fragen: Befehle; retryFlagWriteBack mit Erfolg …“ (FlagNotSent statt FlagSent),
  „Lena fragen: Schreib-Sperre; Erneut senden des Flags …“, „Lena fragen: Einstellung“ (zwei Fälle, Some "purple"
  statt None), „Ledger Split-Schreibweg (Befehle); Kaputt (ledger-f8bjm) …“ (37 statt 35 sichtbare Buchungen),
  „Zuweisen; Zuletzt verwendet; Der Weg über die Abfrage liest die Bewegungen aus jedem Monat …“, „Kassenbuch-Sortierung
  im Lesepfad; Standardsortierung …“, „Ledger Persistence (appendAndApply); Replay reproduziert die
  Uebertrags-Verknuepfung“, „Abrufen startet erst nach dem Lesen der Sitzung“ (zwei Fälle).
- Die Listen der Schreib-Sperre als eigene Gruppe neben der DB-Gruppe: in zwei von zwei Läufen rot „Kassenbuch-Sortierung
  im Lesepfad (ledger-r72re, ADR 0051); Trefferzahl, Summe und Zähler sind für alle zehn Sortierungen gleich“
  (NeedsReviewCount erwartet 2, war 3), und nicht schneller (29,2 und 29,4 s gegen 31,1 und 29,5 s). Darum stehen sie
  in der DB-Gruppe.

### Umsetzung

1. Hebel 1, `testSequenced` wird `testSequencedGroup TestGroups.InMemoryDb` (`src/Tests/TestGroups.fs`, neu): die
   Fälle der Gruppe laufen nacheinander, die Gruppe parallel zum Rest. Keine Markierung ist ganz entfallen. In den 99
   Listen unten entfällt die Sperre gegen alle anderen Tests; sie bleibt gegen die übrigen DB-Listen. Parallel zu den
   Tests ohne DB sind sie sicher, weil sie mit ihnen nichts teilen: die DB, die Schreib-Sperre, der Einzel-Flug-Schutz
   und die Einstellungen werden nur von Listen der Gruppe benutzt. Über jeder steht der Kommentar „Geteilt: die
   In-Memory-DB …“, bei den 15 Schreib-Sperre-Listen dazu die prozessweite Schreib-Sperre, bei Lena die Einstellung
   `lena_flag_color`, bei Seiten, Sortierung, Warteschlange, Replay, Nachladen, Split-Zählung und Bob-Tagesgrenze der
   Leser über alle Zeilen.
   - `BotApiTests.fs`: „Bot-Schnittstelle (Routen)“
   - `BudgetGeneralHistoryTests.fs`: „Allgemeine Historie: Abfrage (SQLite)“; „Allgemeine Historie: Pausen und Target-Änderungen über alle Streams (SQLite)“
   - `BudgetHistoryTests.fs`: „Historie: Abfrage (SQLite)“
   - `CategoryMonthEntriesTests.fs`: „Buchungen einer Kategorie im Monat (SQLite)“
   - `LedgerAmountTests.fs`: „Betrag: Befehl setEntryAmount (ADR 0092)“; „Betrag: Schreib-Sperre“; „Betrag: aufgeteilte Buchung als ein Ersatz (ADR 0092 Punkt 3)“
   - `LedgerBookingCaptureTests.fs`: „Buchung erfassen: Befehl (ledger-at63w)“; „Buchung erfassen: Erneut senden eines Übertrags (ledger-at63w)“; „Buchung erfassen: Rückgängig (ledger-at63w)“; „Buchung erfassen: Rückgängig, Schreib-Sperre“; „Buchung erfassen: Zuletzt-Chips aus dem Kassenbuch je Konto (ledger-sre0f)“
   - `LedgerCashBookingTests.fs`: „Ledger Barbuchung (Befehl)“; „Ledger Barbuchung (Adapter mit Ports)“; „Ledger Versand ohne import_id (echter Einzel-Post, ledger-ay81j)“; „Ledger Barbuchung (verlorene Antwort, ledger-ay81j)“
   - `LedgerCategoryBatchTests.fs`: „Ledger Kategorie für die Auswahl (Befehle)“; „Ledger Kategorie für die Auswahl (Schreib-Sperre)“
   - `LedgerCategoryChangeTests.fs`: „Ledger Kategorie ändern (Befehle)“; „Ledger Kategorie ändern (Schreib-Sperre)“
   - `LedgerDuplicateDecisionTests.fs`: „Wartende Buchung entscheiden: Befehle“; „Wartende Buchung entscheiden: Ablehnungen“; „Wartende Buchung entscheiden: Schreib-Sperre“
   - `LedgerEntryDeletionTests.fs`: „Buchung löschen und Rückgängig (Befehle, ledger-z6sya)“; „Buchung löschen: Schreib-Sperre“; „Buchung löschen: Spiegel (ledger-z6sya)“; „Buchung löschen: API (ledger-z6sya)“; „Buchung wiederherstellen: in BB erfasst (ledger-ay81j)“
   - `LedgerHeldDuplicateTests.fs`: „Wartende Buchung: Lesen“; „Wartende Buchung: jeder schreibende Befehl lehnt sie ab“; „Wartende Buchung: Sammelbefehle überspringen sie mit eigenem Grund“
   - `LedgerLenaFlagBatchTests.fs`: „Ledger Lena fragen für die Auswahl (Befehle)“; „Ledger Lena fragen für die Auswahl (Schreib-Sperre)“
   - `LedgerLenaFlagTests.fs`: „Lena fragen: Befehle (ADR 0056)“; „Lena fragen: Schreib-Sperre“; „Lena fragen: Filter, Zähler und Historie“; „Lena fragen: Einstellung (ADR 0056, Punkt 1)“
   - `LedgerMemoTests.fs`: „Memo: Befehle (ADR 0062)“; „Memo: Schreib-Sperre“
   - `LedgerMirrorBatchTests.fs`: „Spiegel-Lauf: ein Batch mit allen Fällen (ledger-r8h19, Charakterisierung)“; „Spiegel-Lauf: offene Erfassung (ledger-ay81j)“
   - `LedgerMirrorTests.fs`: „Ledger Spiegel-Lauf (mirrorYnabBudget)“; „Ledger Spiegel: Einzel-Flug-Schutz (runMirrorGuarded)“; „Ledger Spiegel-Lauf (Split-Übertrag, Nachlauf)“
   - `LedgerPageTests.fs`: „Kassenbuch-Seite: seitenweise Liste und Kontostaende (Persistence)“; „Kassenbuch-Seite: Kategorie voll qualifizieren (ledger-h6ptn)“
   - `LedgerPagingTests.fs`: „Kassenbuch Nachladen (ledger-be1yh, Server)“
   - `LedgerPayeeCategorySuggestionsTests.fs`: „Ledger Vorschläge zum Payee (ledger-sfab0)“
   - `LedgerPayeeTests.fs`: „Payee: Befehle“; „Payee: Comdirect-Import“; „Payee: Schreib-Sperre“
   - `LedgerPushFailureTests.fs`: „Ledger Push-Fehlschlag (Befehle)“
   - `LedgerRetryPushTests.fs`: „Ledger Erneut senden (Noch nicht in YNAB)“; „Ledger Erneut senden (Schreib-Sperre)“; „Ledger Erneut senden einer offenen Erfassung (ledger-ay81j)“
   - `LedgerReviewMarkBatchTests.fs`: „Ledger Prüfvermerk für die Auswahl (Befehle)“; „Ledger Prüfvermerk für die Auswahl (Schreib-Sperre)“
   - `LedgerReviewMarkTests.fs`: „Ledger Prüfvermerk (Befehle)“; „Ledger Prüfvermerk (Schreib-Sperre)“
   - `LedgerReviewQueueTests.fs`: „Warteschlange: getReviewQueue (In-Memory-SQLite)“
   - `LedgerSortTests.fs`: „Kassenbuch-Sortierung im Lesepfad (ledger-r72re, ADR 0051)“
   - `LedgerSplitReadTests.fs`: „Ledger Split-Sperren (Lesepfad)“
   - `LedgerSplitWriteTests.fs`: „Ledger Split-Schreibweg (Befehle)“; „Ledger Split-Schreibweg (Schreib-Sperre)“; „Ledger Split-Ersatz einer erfassten Buchung (ledger-ay81j)“
   - `LedgerTests.fs`: „Ledger Persistence (appendAndApply)“; „Ledger CommandHandlers (addCashExpense)“; „Ledger CommandHandlers (addCashExpense, Schreib-Sperre)“; „Ledger CommandHandlers (pushComdirectImports)“
   - `LedgerTransferTests.fs`: „Übertrag in BB: Befehle (ledger-qvjvs)“; „Übertrag in BB: Schreib-Sperre“; „Übertrag in BB: Spiegel (ledger-qvjvs)“
   - `LedgerWithoutCategoryTests.fs`: „Ledger Persistence (Ohne Kategorie, ADR 0048)“
   - `LedgerYnabMergeTests.fs`: „Zusammenlegung in YNAB: Spiegel-Lauf“; „Spiegel: Vollabruf-Generation“
   - `MoveMoneyTests.fs`: „Verschieben: Abläufe“; „Verschieben: API-Grenze“; „Zuweisen: Abläufe“; „Zuweisen: API-Grenze“; „Befehls-Id: Verschieben und Zuweisen“
   - `PinnedCategoriesTests.fs`: „Angepinnte Kategorien: echte Verdrahtung im Budget-API“
   - `RemotingEndpointNegativeTests.fs`: „Remoting-Endpoints: Negativfall je Endpoint (infrastructure-am805)“
   - `RemotingPipelineTests.fs`: „Remoting-Pipeline über Api.webApp (infrastructure-am805)“
   - `RuleCommandHandlerTests.fs`: „Regel ändern: PayeeOverride“; „Regel ändern: Kategorie“; „Regel anlegen“; „Gleiche Priorität“; „Gelöschte Regel wiederherstellen“; „Endpoint ruleCategoryFor“
   - `TargetChangeTests.fs`: „Target ändern: Abläufe nur in BB“; „Target-Übernahme: Abläufe“
   - `TargetPauseTests.fs`: „Pause: Abläufe“; „Pause: Persistenz“; „Pause: Befehls-Id“
   - `TargetToYnabTests.fs`: „Target nach YNAB: Abläufe“
   - `TargetsApplyTests.fs`: „Targets anwenden: Abläufe (h2zfn)“; „Budget-Bewegungen: Persistenz (h2zfn)“; „Targets anwenden: Befehls-Id“
2. Neu in der Gruppe (liefen parallel und bremsten die Gruppe über die Tabellensperren aus): `BankImportTests.fs`
   „Abruf bucht ins Kassenbuch und nach YNAB“, „Wiederholter Abruf: Abgleich mit dem Kassenbuch über die import_id“,
   „Automatisch abrufen: der Lauf“; `LedgerBatchReadTests.fs` „Ledger Persistence: Lesen für eine Auswahl
   (ledger-r8h19)“; `BudgetLeadTests.fs` „Übernahme“, „Befehle und Beobachtung“, „Historie“ und die festen Wege in
   „Reihenfolgen von Lesen und Senden“.
3. Bleiben bei `testSequenced`, mit Grund darüber: `ComdirectHttpTests.fs` „Rumpf“ (`Console.SetOut` gilt
   prozessweit, Kommentar stand schon da) und `SyncStartOrderTests.fs` „Abrufen startet erst nach dem Lesen der
   Sitzung“ (die Modulzähler `reads`, `starts`, `confirms`, `cancels`). Die Gruppe „Server-Log“ (3 Listen) ist
   unverändert, ihr Kommentar stand schon da.
4. Hebel 2, nur für den 16-s-Fall (Stop-Loss): „Jede Folge aus drei Schritten hält O1 bis O3 und das Soll des
   Merkers“ ist ein Fall je erstem Schritt („Jede Folge aus drei Schritten ab „<Schritt>“ hält …“), jeder auf einer
   eigenen Datenbank (`ownBudgetDb`: `BudgetEventsFixture.isolatedDb` plus Monatserster- und Anpinn-Schema). Die
   Helfer lesen und schreiben über `FakeYnab.Connect` (ohne Angabe die gemeinsame DB, für alle festen Wege
   unverändert). Orakel unverändert. Menge der Folgen: vorher ein Fall, 1.728 Folgen, davon 1.539 gelaufen; nachher
   zwölf Fälle mit zusammen 1.539 gelaufenen Folgen (Erneut senden 0; Lesen, YNAB ändert a, YNAB ändert b je 129; die
   übrigen acht je 144), dazu der Fall „Die Teile nach dem ersten Schritt decken jede Folge aus drei Schritten genau
   einmal ab“: die zwölf Teile sind genau die 1.728 Folgen aus `paths 3`, die Erwartungen ergeben zusammen 1.539.
   Gegenprobe: Erwartung für die drei nicht entscheidenden Schritte auf 130 gesetzt, rot sind der Deckungsfall und die
   drei Teile. Ein Teil braucht 4 bis 5 s.
5. Wächter `src/Tests/TestSequencingGuardTests.fs` (3 Fälle): jede Markierung in `src/Tests` hat direkt darüber einen
   Kommentar; Gegenproben für eine Markierung ohne Kommentar (gemeldet, auch in `List.map (testSequencedGroup …)`)
   und für Kommentare und String-Literale (nicht gemeldet).
6. Test-Doktrin Punkt 16 (`.claude/rules/test-doctrine.md`) nennt die Gruppe, die eigene Datenbank für große
   Aufzählungen und den Wächter. Die Kopfkommentare der Ledger-Testdateien nennen die Gruppe statt `testSequenced`.

Verworfen: eine leere Datenbank je Folge statt je Teil (Teile zusammen 65 s statt 48 s); `commandHeadStartMs` von
300 auf 150 ms (ohne Gegenprobe gegen eine fehlende Sperre nicht belegbar, die Gegenprobe hätte Produktionscode
gebraucht).

### Messtabelle

Gemessen mit `USE_MEMORY_DB=true TZ=Europe/Berlin /usr/bin/time -p dotnet src/Tests/bin/Debug/net10.0/Tests.dll
--junit-summary <datei>` im Worktree, 10 Kerne. Last ist `uptime` (1, 5, 15 Minuten) vor dem Lauf.

| Lauf | Last | Wandzeit | CPU (user) | Fälle |
|---|---|---|---|---|
| vorher 1 | 12,85 8,14 7,39 | 64,9 s | 77,0 s | 5.495 |
| vorher 2 | 6,04 6,66 6,84 | 64,3 s | 74,1 s | 5.495 |
| vorher (Conductor, main) | um 8 | 64,8 s | 74,9 s | 5.495 |
| nachher 1 | 11,73 10,04 9,21 | 29,8 s | 56,3 s | 5.510 |
| nachher 2 | 11,72 10,23 9,31 | 28,3 s | 56,6 s | 5.510 |
| nachher 3 | 9,63 9,88 9,21 | 29,5 s | 57,5 s | 5.510 |
| nachher 4 | 7,23 9,28 9,01 | 29,6 s | 58,7 s | 5.510 |
| nachher 5 | 9,44 9,66 9,16 | 29,0 s | 57,1 s | 5.510 |
| nachher 6 | 8,19 9,37 9,08 | 29,7 s | 60,2 s | 5.510 |
| nachher 7 | 6,96 8,98 8,95 | 29,2 s | 58,2 s | 5.510 |
| nachher 8 | 9,55 9,43 9,11 | 29,5 s | 58,0 s | 5.510 |

Median nachher 29,5 s (alle acht), bei Last unter 10 (Läufe 3 bis 8) ebenfalls 29,5 s.

Sequenzielle Listen:
- vorher: 101 `testSequenced`-Listen, 928 Fälle, 34,1 s (Lauf 1) und 34,4 s (Lauf 2), am Ende allein; dazu Gruppe
  „Server-Log“ mit 3 Listen unter 0,3 s.
- nachher: 2 `testSequenced`-Listen, 10 Fälle, unter 0,1 s; Gruppe `TestGroups.InMemoryDb` mit 107 Markierungen,
  959 Fälle, 27,8 s (Lauf 4) und 27,9 s (Lauf 6), parallel zum Rest; „Server-Log“ unverändert.

Die fünf längsten Fälle:
- vorher (Lauf 1): „Budget führen: Abläufe (dgrp7); …; Jede Folge aus drei Schritten hält O1 bis O3 …“ 16,0 s;
  „Tab-Daten: Generationsregel über alle Reihenfolgen“ 8,3 s; „Kategorie im Detail: …; Der Zeitgeber der Karte meldet
  nach ihrer Dauer den Ablauf …“ 6,0 s; „Filter-Sheet unter 1024 px: …; Alle Reihenfolgen aus Öffnen, Auswahl, …“
  5,8 s; „Payee ändern: ein Aufruf zugleich; über alle Reihenfolgen …“ 4,9 s.
- nachher (Lauf 4): „Tab-Daten: Generationsregel über alle Reihenfolgen“ 7,9 s; „Kategorie im Detail: …; Der
  Zeitgeber der Karte …“ 6,0 s; „Filter-Sheet unter 1024 px: …“ 5,5 s; „Kassenbuch-Seite: Reduzierer; Über alle
  Reihenfolgen aus Tippen, Ablauf und Entfernen …“ 4,9 s; „Regeln: Reihenfolge in jeder Reihenfolge“ 4,5 s. Der
  längste Teil der geteilten Aufzählung liegt bei 4,5 bis 4,9 s.

### Stabilität

Zehn Läufe grün: nachher 1 bis 8 hintereinander, dazu zwei Läufe parallel zu `npm run test:scripts` (Last 9 bis 11,
32,9 s und 70,1 s; im zweiten lief der Browser-Teil von test:scripts, alles war langsamer, aber grün). Kein Fall war
rot, auch nicht `BudgetTakeoverTests.fs:529` „die Wartefunktion des Servers wartet wirklich“. In dem test:scripts
neben den Expecto-Läufen war nur der bekannte Layout-Sweep rot.

### Gate

`./verify.sh`: erster Lauf `VERIFY: FAIL` nur in test:scripts, „teardown --delete-branch löscht auch den aw/-Branch
des Worktrees“ (`scripts/worktree-scripts.test.mjs:93`, `assert.ok(!fs.existsSync(wt))`, Last 13,6; allein danach
6 von 6 grün). Zweiter Lauf `VERIFY: PASS (dotnet 5494 grün, node 357 grün, dotnet build, npm run build, guard:classes,
guard:tokens grün)`, Expecto im Gate 30,7 s (erster Lauf) bei Last 13 bis 22. Danach nur noch ein Kommentar in
`BudgetLeadTests.fs` verschoben; Lösung neu gebaut, Expecto 5.494 grün in 29,0 s.

### Kann-Sammlung

- Lange Client-Aufzählungen nicht geteilt (Stop-Loss): „Tab-Daten: Generationsregel“ 7 bis 8 s, „Filter-Sheet unter
  1024 px“ 5,5 s, „Kassenbuch-Seite: Reduzierer; Über alle Reihenfolgen …“ 4,9 s, „Regeln: Reihenfolge in jeder
  Reihenfolge“ 4,5 s, „Payee ändern: ein Aufruf zugleich“ 4,4 s. Sie liegen nicht auf dem kritischen Pfad, solange die
  DB-Gruppe rund 28 s braucht.
- Die DB-Gruppe ist die längste Kette (rund 28 s). Rund 9,6 s davon sind die festen 300-ms-Fenster von
  `LedgerWriteGuardProbe.commandWaitsForMirror` (36 Fälle). Ein Zähler wartender Befehle an `WriteGuard` (wie
  `MonthGuard.waitingFor`) könnte die Wartezeit ersetzen; braucht Produktionscode.
- Budget-Abfragen über alle Monate (`queryMovements` ohne Monat, `loadBudgetEventsOn … OfTypes`) laden und
  dekodieren jedes Ereignis dieser Typen über alle Streams; im Test rund 125 ms je Aufruf bei rund 1.500
  Monats-Streams. In der Produktion heute klein.
- `Tests.dll --debug` stand nach rund 10 s ohne weitere Ausgabe still (Last um 6, nach 8 Minuten abgebrochen); ohne
  `--debug` hing in 13 vollen Läufen nichts.

Dateien: `src/Tests/TestGroups.fs`, `src/Tests/TestSequencingGuardTests.fs`, `src/Tests/BudgetLeadTests.fs`
(Aufzählung, `FakeYnab.Connect`, `ownBudgetDb`), `src/Tests/Tests.fsproj`, die Markierungen in den Testdateien oben,
`.claude/rules/test-doctrine.md` Punkt 16.
