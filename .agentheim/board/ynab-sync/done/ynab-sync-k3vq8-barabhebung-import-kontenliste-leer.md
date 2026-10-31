---
id: ynab-sync-k3vq8
title: "Barabhebung in Import prüfen: Kontenliste bleibt still leer, das Bar-Konto ist nicht vorbelegt und im Picker gibt es keine Konten"
status: done
type: bug
context: ynab-sync
created: 2026-10-31
completed:
depends_on: [design-system-001]
blocks: []
tags: [import, split, barabhebung, picker, regression]
related_adrs: [0004, 0006]
related_research: []
prior_art: [ynab-002, design-system-bym4m, ledger-s8t3s]
---

## Why

Roman, 2026-10-31: Der Chip "Barabhebung" in Import prüfen öffnet das Aufteilen-Sheet, aber die zweite Zeile ist leer statt mit dem Bar-Konto belegt, und im Picker lässt sich kein Konto wählen. Eine EC-Zahlung mit Bargeld-Anteil lässt sich damit in BB nicht mehr als Übertrag aufteilen.

## What

Beide Symptome haben eine gemeinsame Ursache im Code: `model.Accounts` ist im SyncFlow leer. `StartCashbackSplit` findet das Quick-Add-Konto dann nicht (`openOnBudgetAccounts model.Accounts`), und `CategoryPicker` zeigt ohne Konten keinen Abschnitt "Übertrag auf Konto" und keine Konto-Treffer.

Der Ladeweg `LoadAccounts` (`src/Client/Components/SyncFlow/State.fs:841`) hat drei Schwächen:

- Er ruft `Api.ynab.getBudgetDetails`, das heißt `GET /budgets/{id}` mit dem kompletten Budget samt aller Buchungen.
- Jeder Fehler wird zu `AccountsLoaded ([], ...)` und verschwindet ohne Meldung. Auch wenn der Abruf von Einstellungen scheitert, geht zusätzlich die Quick-Add-Id verloren.
- Er läuft nur einmal in `init`, nicht in `pageLoadCmd` (`src/Client/State.fs:183`). Ein Fehler beim App-Start bleibt bis zum Neuladen bestehen.

**Hauptursache, Stand 2026-10-31:** Firefox bricht lesende Abfragen beim Laden mit "Http error (0)" ab (Befund 2026-10-24, `src/Client/Retry.fs`). Das Kassenbuch ist seit 05536fd über `Retry.reading` geschützt, der SyncFlow nicht. Alle lesenden Aufrufe in `SyncFlow/State.fs` (`getCurrentSession`, `getTransactions`, die drei `getSettings`, `getCategories`, `getPayees`, der Konten-Abruf) laufen über `Retry.reading`. `getSettings` steht vor Kategorien, Konten und Payees: Scheitert es, fehlen die Daten dahinter.

Der Import zieht auf denselben Weg wie das Kassenbuch um: `Api.ledger.getBudgetAccounts` (`GET /budgets/{id}/accounts`, 5 Minuten serverseitig gecacht, Fehlertext statt Schweigen). `getSettings` bleibt für die Quick-Add-Id.

Der Import lädt die Konten beim Seitenaufruf und erneut beim Öffnen des Split-Sheets, solange sie nicht geladen sind (Vorbild `Ledger/State.fs:2068`). Ein Fehlschlag erscheint als Toast mit dem Fehlertext. Kommt die Liste erst nach dem Tipp auf "Barabhebung" an, belegt sie die noch leere Übertrag-Zeile nachträglich. Eine vom Nutzer gesetzte Zeile bleibt unangetastet.

## Acceptance criteria

- [ ] Kaputt-Szenario, GIVEN `getBudgetDetails` scheitert und `getBudgetAccounts` liefert die Konten, WHEN der Chip "Barabhebung" getippt wird, THEN zeigt Zeile 2 "→ Bar" (Quick-Add-Konto). Der Browser-Wächter ist vor dem Fix rot und nennt im Kommentar diesen Bug.
- [ ] Das Sheet zeigt im Picker den Abschnitt "Übertrag auf Konto", die Suche "Bar" findet "→ Bar", die Wahl setzt den Übertrag (Wächter `scripts/import-split-target.test.mjs` auf `getBudgetAccounts` umgestellt).
- [ ] Intakt-Szenario: Ohne Quick-Add-Konto bleibt Zeile 2 leer und das Konto ist im Picker wählbar.
- [ ] Kaputt-Szenario Abbruch, GIVEN der erste `getSettings`- oder `getTransactions`-Aufruf bricht ab (`route.abort`, Status 0), WHEN die Seite lädt, THEN erscheinen Buchungen, Kategorien und Konten ohne Neuladen und ohne Fehlerbanner. Der Browser-Wächter ist vor dem Fix rot.
- [ ] Alle lesenden Aufrufe in `SyncFlow/State.fs` laufen über `Retry.reading`, schreibende nicht.
- [ ] Scheitert das Laden der Kategorien auch nach den Wiederholungen, erscheint ein Toast, statt dass der Picker still leer bleibt.
- [ ] Der Client ruft `getBudgetDetails` im SyncFlow nicht mehr auf.
- [ ] Scheitert `getBudgetAccounts`, erscheint ein Toast mit dem Fehlertext; die Quick-Add-Id bleibt erhalten, wenn nur die Konten scheitern.
- [ ] Scheitert der erste Abruf, lädt das Öffnen des Split-Sheets die Konten neu, ohne dass die App neu geladen werden muss (`update` unter .NET, Mapping als reine Funktion, soweit `Client.Api` im Weg ist).
- [ ] Kommt die Liste nach dem Tipp auf "Barabhebung" an, ist die leere Übertrag-Zeile danach belegt; eine vom Nutzer gesetzte Zeile ändert sich nicht.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes

**Belege (Roman, 2026-10-31):** Im Kassenbuch funktioniert die Barabhebung. Im Import kommt nach Reloads wiederholt "Http error (0)" bei `getTransactions`, die Seite lädt erst nach mehreren Versuchen, und dann fehlen je nach Lauf Konten oder Kategorien und Konten. Der Browser ist Firefox. Das passt zu einzelnen Abbrüchen je Abfrage. Eine frühere Notiz hatte den Startfehler verworfen, weil ein Reload gelang: Das war falsch, jede Abfrage kann unabhängig scheitern.

**Befund Produktion (Logs docker-host, 2026-10-31, 09:14 bis 10:16 UTC):**

- `budgetbuddy-app` läuft seit 7 Stunden ohne Neustart und ohne OOM, Speicher 590 MiB. Ein Server-Absturz ist ausgeschlossen.
- `tailscale_budgetbuddy` (Proxy vor der App) protokolliert in Schüben zu den Reloads `http2: received GOAWAY` und `http: proxy error: context canceled`, 147 Zeilen seit 09:14. Der GOAWAY kommt am HTTP/2-Server des Proxys an, also von der Client-Seite. Die Anfragen werden demnach zwischen Browser und Proxy abgebrochen.
- Die App beantwortet ihre Anfragen meist mit 200. `getCategories` scheiterte zweimal mit 500 "Unexpected end of request content", der Rumpf kam nicht vollständig an.
- `getBudgetDetails` braucht 3 bis 17 Sekunden, `getBudgetAccounts` aus dem Cache 3 ms. Der langsame Abruf bleibt also lange genug offen, um von einem Abbruch oder Reload getroffen zu werden. `Retry.reading` allein reicht deshalb nicht, der Umstieg auf `getBudgetAccounts` bleibt nötig.
- Status 200 sagt bei Remoting nichts über ein fachliches `Error` im Rumpf. Ob `getBudgetDetails` zusätzlich inhaltlich scheitert, ist offen.

**Nicht bewiesen:** warum die Client-Seite die Verbindungen schließt (Firefox, Mac, Tailscale-Client). Bleibt das nach dem Fix störend, ist das ein eigener Task im BC infrastructure. Der Container nutzt `tailscale/tailscale:latest` (1.102.3), die übrigen Tailscale-Container 1.104.1.

**Spur Firefox:** Die Konsole zeigt je Anfrage die Info "Lokaler Netzwerkzugriff erkannt" (Tailscale-Adresse 100.85.140.42, `auto_allow`). Firefox stuft die Tailscale-Adresse also als lokal ein, blockiert aber nicht und fragt nicht nach. Ein Zusammenhang mit den Abbrüchen ist nicht belegt. Gegenprobe: gleicher Reload in einem anderen Browser.

Die Meldung nennt diesen Fall "Database error during load", obwohl keine Antwort kam. Das ist irreführend, aber nicht Teil dieses Tasks.

`YnabApi.getBudgetDetails` bleibt als Endpoint bestehen, Aufräumen ist nicht Teil dieses Tasks. `YnabClient.getBudgetWithAccounts` nutzt der Verbindungstest weiter.

Fable.Remoting-Fixtures mitziehen. Die Fixture mockt heute alle unbekannten `/api/**` mit 500, `LedgerApi/getBudgetAccounts` braucht also einen eigenen Mock.

Der offene Punkt aus design-system-bym4m (im Import ist das Konto der Buchung selbst wählbar) bleibt außerhalb dieses Tasks.

## Verifier note (iteration 1)

VERDICT: FAIL

REASONS:
- Toast außerhalb des Imports, im Browser belegt: `SyncFlow.State.init` (`src/Client/Components/SyncFlow/State.fs:210`) schickt `LoadCategories` auf jeder Seite, und der neue Zweig `CategoriesLoaded (Error ...)` (State.fs ab Zeile 1007) zeigt immer einen Toast. Mit scheiterndem `getCategories` erscheint er auf `#/ledger`, `#/rules` und `#/settings`, ohne YNAB-Einrichtung auf `#/settings` bei jedem App-Start.
- `scripts/ledger-compact-mouse.test.mjs` verdeckt diesen Fehler: Die neuen Mocks sorgen nur dafür, dass der Wächter den Toast nicht sieht. Im Betrieb liegt er 5 s unten rechts über dem Kassenbuch und fängt dort Klicks ab. Kein Test prüft, dass außerhalb des Imports kein Kategorie-Toast erscheint.
- Überlebender Mutant an der harten Grenze: `confirmTan` über `Retry.reading` (State.fs:334) lässt `scripts/sync-start.test.mjs` grün, die Fixture bricht `confirmTan` nie ab. Der Code ist richtig, die Outcome-Aussage stimmt für `confirmTan` nicht.
- Nebenbefund: Scheitern Konten und Kategorien dauerhaft, gibt jeder Besuch der Import-Seite zwei Toasts, sechs schnelle Wechsel ergaben 14 gestapelte. Begrenzt durch die Besuche, allein kein FAIL.
- Bestanden: `VERIFY: PASS`, `StartRecovery` und Frist unberührt, kein dauerhafter Ladezustand, alte Antworten überschreiben neuere nicht, Nachtrag und `RemoveSplit` richtig, `getBudgetAccounts` liefert dieselben Kontofelder (bis zu 5 Minuten gecacht), README-Delta besteht.

SUGGESTED_FIX: Der Kategorie-Toast darf nur auf Seiten erscheinen, die die Kategorien brauchen (Import, Quick-Add). `init` nicht einfach streichen, `SyncFlow.Categories` speist auch Quick-Add. Ein Test belegt, dass der Toast auf Kassenbuch, Regeln und Settings fehlt und im Import kommt, die Mocks in `ledger-compact-mouse.test.mjs` fallen weg. Die `confirmTan`-Mock in `sync-start.test.mjs` bricht in einem neuen Fall ab, der Test prüft genau einen Versuch.

ITERATION_HINT: likely-fixable

## Outcome

Der Chip "Barabhebung" in Import prüfen belegt Zeile 2 wieder mit "→ Bar", der Picker zeigt die Konten, auch wenn beim Laden einzelne Abfragen abbrechen.

- **Konten:** `LoadAccounts` ruft `Api.ledger.getBudgetAccounts`, die Quick-Add-Id kommt unabhängig davon über `LoadQuickAddAccount` (`getSettings`). `getBudgetDetails` nutzt der SyncFlow nicht mehr. Beides läuft über `pageLoadCmd` und nicht mehr über `init`. Das Öffnen des Split-Sheets lädt fehlende Konten und eine fehlende Id nach.
- **Toasts:** Scheitern Konten oder Kategorien, meldet der SyncFlow `ShowLoadFailure` mit dem Fehlertext. Der Root entscheidet über `shouldShowLoadFailure` (`src/Client/State.fs`): Der Toast erscheint nur auf Import und Quick-Add und nicht, solange derselbe Text sichtbar ist. Die Kategorien laden weiter in `init`, ein Fehlschlag auf anderen Seiten bleibt still, das Betreten von Import oder Quick-Add lädt neu und meldet ihn dann. Eine Quick-Add-Id und eine geladene Liste überstehen einen Fehlschlag. Scheitern beide Abrufe, stehen zwei Toasts da (ein Text je Abruf), das verlangen die Akzeptanzkriterien. Bei schnellem Wechseln bleibt es bei diesen zweien. Nach dem Ablauf von 5 s meldet der nächste Besuch den Fehler erneut.
- **Später Nachtrag:** `AwaitedCashbackLine` merkt sich die Übertrag-Zeile einer Barabhebung. Kommen Liste oder Id nach dem Tipp, wird die Zeile belegt, solange sie leer ist. Der normale Editor und von Roman gesetzte Zeilen bleiben unberührt.
- **Retry:** Alle lesenden Aufrufe in `src/Client/Components/SyncFlow/State.fs` laufen über `Retry.reading`, zusätzlich zur Aufzählung im Task auch `testRule` (liest nur, die Antwort ist schon über den Formularstand abgesichert). Schreibende Aufrufe sind unverändert. `startSync` und `confirmTan` bleiben außerhalb, beide sind jetzt per Abbruch-Test belegt: `scripts/sync-start.test.mjs` prüft je genau einen Versuch.
- **Veraltete Antworten (Hinweis aus infrastructure-r3tw7):** `Retry.reading` hält Antworten bis zu 1,6 Sekunden zurück. Betroffen waren Sitzung, Buchungen, Konten und Quick-Add-Id: Ihre Antworten tragen jetzt den Stand des Abrufs (`SessionReadAnswered`, `TransactionsReadAnswered`, `AccountsLoaded`, `QuickAddAccountLoaded`), und nur der aktuelle Stand zählt. `StartSync`, `SyncStarted`, `ConfirmTan` und `SyncCancelled` überholen laufende Lesevorgänge, das Nachlesen nach `SyncStartInterrupted` endet nicht an einer alten Antwort. Dessen Logik in `CurrentSessionLoaded` ist unverändert. Nicht betroffen: Kategorien (nur ein Abruf zur Zeit), Payees (nur einmal beim Start), `testRule` (eigener Stand). Die Lena-Farbe hat dieselbe Schwäche, sie war schon vorher umhüllt und steht als Folge-Task im Backlog.
- **Fixtures:** `scripts/import-split-target.test.mjs` nutzt `getBudgetAccounts`, `scripts/import-lena.test.mjs` und `scripts/import-pruefen-duplikat-details.test.mjs` sind umgestellt. `scripts/ledger-compact-mouse.test.mjs` ist wieder unverändert, die Mocks aus Iteration 1 sind weg.

Tests: `src/Tests/SyncFlowStateTests.fs` (Listen "Konten im Import", "Veraltete Antworten im Import", "Ladefehler im Import als Toast"), Browser-Wächter `scripts/import-split-target.test.mjs`, `scripts/inline-rule-form.test.mjs`, `scripts/sync-start.test.mjs`. Vor dem Fix der ersten Iteration waren alle Browser-Fälle rot, davon die Fälle für Zeile 2, Picker und Abbrüche an den fachlichen Erwartungen; der Fall "schreibender Aufruf" scheiterte vor dem Fix nur am falschen Toast-Selektor und wurde danach über die Gegenprobe belegt. Vor dem Fix der zweiten Iteration waren die vier Fälle "Toast außerhalb des Imports" und der Stapel-Fall an ihren Erwartungen rot (7 statt 1 Toast). Der Quick-Add-Fall war in seiner ersten Fassung wegen einer falschen Route grün und wurde danach neu gefasst; rot ist er nur über die Mutanten belegt.

Gegenproben in der Kopie `k3vq8-iter2-worker` ohne `.env`, einzeln nacheinander. Liste A, gegen den Stand von Iteration 2, 39 Mutanten, 38 getötet:
- Neu: Toast ohne Seitenprüfung, SyncFlow oder QuickAdd fehlt in der Seitenliste, Dedupe weg, Dedupe ignoriert Exiting, Dedupe ignoriert Text, Root zeigt `ShowLoadFailure` nie, `confirmTan` über `Retry.reading`, `startSync` über `Retry.reading`, Kategorie-Toast und Konten-Toast umgehen den Root.
- Wiederholt: Konten-Toast weg, Kategorie-Toast weg, Fehlschlag verwirft geladene Liste, Konten-Doppelschutz weg, Kategorie-Doppelschutz weg, `init` lädt Konten wieder, Unauthorized als Tokenfehler, Auffrischen setzt Loading, `pageLoadCmd` ohne LoadAccounts, ohne LoadQuickAddAccount, Import ohne LoadCategories, Quick-Add ohne LoadCategories, Sheet lädt Konten nicht nach, Sheet lädt Quick-Add nicht nach, Nachtrag überschreibt gesetzte Zeile, Konten-Eintreffen ohne Nachtrag, Quick-Add-Eintreffen ohne Nachtrag, Barabhebung wartet nie, Retry entfernt je getCurrentSession, getTransactions, Kategorie-getSettings, getCategories, getBudgetAccounts, QuickAdd-getSettings, Payee-getSettings, getPayees, testRule.
- Quick-Add ohne LoadCategories überlebte zuerst, der Test wurde verschärft (Fehlschlag auf Settings, dann Betreten von Quick-Add), die Wiederholung ist getötet.
- Überlebender: `init` lädt keine Kategorien. Der einzige Verbraucher außerhalb des SyncFlow ist die Quick-Add-Seite (`src/Client/View.fs:117`), und Import und Quick-Add laden beim Betreten selbst. Der Abruf in `init` ist also nur Vorausladen, sein Fehlen ändert kein geprüftes Verhalten. Er bleibt laut Auftrag bestehen.
- Dedupe weg und beide Seitenlisten-Mutanten sind zusätzlich allein mit den Browser-Wächtern getötet.

Liste B, aus Iteration 1 und gegen den dortigen Stand, 23 Mutanten, in Iteration 2 nicht wiederholt, weil diese Änderung sie nicht berührt: skipTransaction mit Retry, Konten wieder über getBudgetDetails, normaler Editor wartet auch, RemoveSplit rückt nicht mit, RemoveSplit behält entfernte Zeile, sowie die Mutanten zu veralteten Antworten (Stand ungeprüft und begin*Read ohne Stand je Sitzung, Buchungen, Konten, QuickAdd sind 8, dazu TanRequired, ReviewReady, Startfehler, TAN-Bestätigung, Start und Abbruch verwerfen Sitzung oder Buchungen, Abbruch-Ladezustand, Abandon lässt Loading stehen). Die Aussagen oben gelten nur für diese Listen.

Grenze der Gegenprobe: Die Fälle mit `beginSyncStart` und `beginTanConfirm` prüfen die reinen Funktionen, das eigentliche `update` dieser Nachrichten berührt `Client.Api` und ist unter .NET nicht aufrufbar. Von den übrigen schreibenden Aufrufen ist `skipTransaction` per Mutant belegt, der Rest per Durchsicht geprüft.

`./verify.sh`: VERIFY: PASS (dotnet 3031 grün, node 384 grün).
