---
id: infrastructure-vp308
title: "Fehler über die Grenze: kein Stacktrace an den Client, kein YNAB-Rohtext an Client, Bob und in events"
status: done
type: bug
context: infrastructure
created: 2026-11-07
completed:
depends_on: [ledger-bk3q8]
blocks: []
tags: [server, fehlertexte, remoting, ynab, bot, events, security, bestandsreview, budget, ledger]
related_adrs: [0055, 0066, 0068, 0083, 0084]
related_research: []
prior_art: [budgeting-p4n6r, ledger-b0b7q, ledger-z71ka]
---

## Why
Bestandsreview budgeting vom 2026-11-07, Gruppe 2, und Bestandsreview ledger vom 2026-11-06, Gruppe 4 (`.agentheim/knowledge/reviews/2026-11-07-budgeting.md`, `.agentheim/knowledge/reviews/2026-11-06-ledger.md`). Beide nennen denselben Fehlerhandler. Roman hat am 2026-11-07 entschieden, beide Gruppen in einer Task zusammenzuführen, mit den empfohlenen Optionen: budgeting "Ganze Gruppe, gemeinsam mit Ledger Gruppe 4", ledger "Fehlerhandler und Bob-Meldungen".

Was heute passiert:
- **Stacktrace an den Client.** `errorHandler` (`src/Server/Api.fs:2057`) gibt jede Ausnahme mit `Propagate ex` weiter, für alle sechs Remoting-Interfaces (`Api.fs:2073` bis `:2103`). Der Antwortrumpf trägt Typ, Meldung und Stacktrace mit Dateipfaden des Servers, auch für jedes `failwith` in den Budget-Abläufen. Der Browser zeigt im Toast den englischen Satz des Proxys ("Internal server error (500) while making request to ..."), die Ausnahme steht im Netzwerk-Tab.
- **YNAB-Rohtext dauerhaft in `events`.** Beim Zuweisen und Verschieben hängt `interpretAssignResponse` den Antwortrumpf an ("Bad request: {bodyText}", "HTTP {other}: {bodyText}", `src/Server/YnabClient.fs:438` und `:442`). Über `ynabErrorText` wird daraus der Grund von `AssignmentWriteBackFailed` (`src/Server/Budgeting/CommandHandlers.fs:724`, `:832`, `:1119`), der für immer in `events` steht und in Bericht und Historie erscheint.
- **Ausschnitte aus Romans Monat im Client.** `interpretMonthResponse` (`YnabClient.fs:319`) meldet einen Dekodierfehler mit dem Thoth-Text samt JSON-Ausschnitt (Kategorienamen, Beträge, `:325`) und einen unerwarteten Status mit Rumpf (`:329`). `getMonthWith` meldet "Failed to fetch month" mit der Ausnahme (`:342`), `setAssignedWith` "Failed to assign category" (`:471`).
- **YNAB-Rohtext an Bob.** `createTransactionWithFlag` (`YnabClient.fs:1189`, Quick Add und Bob über `createManualTransaction`) hängt bei 400 und unerwartetem Status den Rumpf an und meldet 401, 404 und Dekodierfehler englisch (`:1213` bis `:1225`). Bob bekommt das als `push_error` (`src/Server/BotApi.fs:405`), das Kassenbuch als Grund von `EntryPushToYnabFailed`.
- **Leser ohne `readWith`.** `loadApplicationWith` (`src/Server/Budgeting/Persistence.fs:283`) und `loadMovementsWith` (`:438`) reichen einen Dekodierfehler mit Ereignis-Id ungeloggt nach oben; die übrigen Leser laufen über `readWith` und `logReadFailure` (`:188`).
- **Guid und Monatsschlüssel in Ablehnungen.** `planMove` und `planAssign` melden "Die Kategorie {id} fehlt im Monat {month}" (`src/Server/Budgeting/Domain.fs:227`, `:276`), obwohl es `CategoryNotVisibleText` gibt (`:109`).
- **Verschluckter Lesefehler.** `reportOf` (`CommandHandlers.fs:560`) verwirft einen Fehler beim Lesen von Zu verteilen ohne Log.

BB ist nur über Tailscale erreichbar, Roman ist der einzige Nutzer; ein Leck im engen Sinn ist das nicht. Es verletzt aber die Regel "Secrets gehen nie an den Client" (CLAUDE.md, Security-Fallen; Doktrin Punkt 14: "auch nicht in Fehlermeldungen") und "konkrete Meldungen auf Deutsch" (`standards/backend/overview.md`), und `events` sammelt Rohtexte, die niemand mehr bereinigt.

## What
Am Code vom 2026-11-07 (main 7423fb0) geprüft.

1. **Fehlerhandler** (`Api.fs:2057` bis `:2060`): erzeugt je Ausnahme eine kurze Kennung, schreibt Methode, Kennung und `ex.ToString()` ins Log und propagiert nur einen festen deutschen Satz mit der Kennung, etwa "Interner Fehler im Server (Kennung …), Details stehen im Server-Log." Die Funktion wird testbar (öffentlich oder `internal`, Kennung und Log als Parameter); alle sechs `withErrorHandler` nutzen sie.
2. **Monat lesen** (`YnabClient.fs:319` bis `:343`): Dekodierfehler über `unreadableResponse`, unerwarteter Status über `statusFailure`, Ausnahme über `fetchFailure`. Die drei Helfer stehen heute erst ab `:365` und rücken nach oben; `statusFailure` loggt heute fest "GET", für PATCH und POST bekommt es die Methode als Parameter oder einen Bruder.
3. **Zuweisen** (`YnabClient.fs:437` bis `:472`): 400 mit festem Satz (etwa "YNAB hat die Zuweisung abgelehnt (HTTP 400)"), unerwarteter Status und Ausnahme über die Helfer; der Rumpf nur ins Log. 401, 404 (`MonthNotOpen`) und 429 (`RateLimitExceeded`) bleiben, wie sie sind.
4. **Buchung anlegen** (`YnabClient.fs:1213` bis `:1225`): dieselben festen Sätze für 400, 401, 404, unerwarteten Status, Dekodierfehler und Ausnahme, Details ins Log. Damit trägt `push_error` an Bob und der Grund von `EntryPushToYnabFailed` nur noch feste Sätze. `ynab_unavailable` (`BotApi.fs:326`, `:459`) liest Kategorien über `getCategories`, das schon `statusFailure` und `unreadableResponse` nutzt (`YnabClient.fs:503` bis `:537`); der Worker prüft, dass jeder Grund an Bob aus einem festen Satz kommt (`BotApi.fs:534`).
5. **Leser**: `loadApplicationWith` und `loadMovementsWith` über `readWith` mit `logReadFailure` und festem Satz.
6. **Ablehnungen**: `planMove` und `planAssign` nennen `CategoryNotVisibleText` statt Guid und Monatsschlüssel.
7. **`reportOf`**: ein gescheitertes Lesen von Zu verteilen steht mit Grund im Log; der Bericht bleibt mit `ActualToBeBudgeted = None`.

## Acceptance criteria
- [x] Kaputt, Fehlerhandler direkt (API-Adapter-Ebene), vor dem Fix rot: GIVEN eine Ausnahme, deren Meldung einen Marker trägt (erfunden, etwa "marker-vp308") und die einen Stacktrace hat, WHEN der Fehlerhandler sie behandelt, THEN enthält der propagierte Wert weder Marker noch Stacktrace noch Typnamen, aber die Kennung; das Log enthält Marker und dieselbe Kennung. Der Kommentar nennt den Bug.
- [x] Verdrahtung: ein Test durch den echten Host (`src/Tests/RemotingPipelineTests.fs`) zeigt, dass eine Ausnahme in einem Remoting-Aufruf ohne Marker im Antwortrumpf ankommt, sofern sie sich ohne neuen Produktionsschalter auslösen lässt. Geht das nicht, nennt das Outcome die sechs `withErrorHandler`-Stellen und den Grund.
- [x] Kaputt, Monat lesen (pur, `src/Tests/YnabClientTests.fs`), vor dem Fix rot: `interpretMonthResponse` mit Status 500 und Rumpf mit Marker liefert einen Fehler ohne Marker; mit Status 200 und unlesbarem Rumpf mit Marker "YNAB-Antwort nicht lesbar" ohne Marker; `getMonthWith` mit werfendem `fetch` (Marker in der Meldung) einen Fehler ohne Marker.
- [x] Kaputt, Zuweisen (pur), vor dem Fix rot: `interpretAssignResponse` mit 400 und mit 503, je mit Marker im Rumpf, liefert einen festen deutschen Satz ohne Marker; `setAssignedWith` mit werfendem `send` ebenso.
- [x] Kaputt, Handler-Ebene mit In-Memory-SQLite (`BudgetAssignmentsTests.fs` oder `MoveMoneyTests.fs`), vor dem Fix rot: GIVEN YNAB antwortet beim PATCH mit 400 und Marker im Rumpf, WHEN Zuweisen, THEN steht in `AssignmentWriteBackFailed` ein Grund ohne Marker, und der Bericht nennt denselben Grund.
- [x] Intakt: 429 bleibt `RateLimitExceeded`, der Grund "Rate-Limit" und das erneute Senden wie bisher; 404 bleibt `MonthNotOpen` ("Monat nicht offen"); 401 bleibt "YNAB-Token ungültig (HTTP 401)" (bestehende Tests grün).
- [x] Kaputt, Bob (`src/Tests/BotApiTests.fs`, Push über Fake), vor dem Fix rot: GIVEN YNAB lehnt die Buchung mit 400 und Marker im Rumpf ab, WHEN Bob committet, THEN trägt die Antwort `pushed_to_ynab = false` und ein `push_error` ohne Marker, die Buchung steht im Kassenbuch. Ebenso für `createTransactionWithFlag` pur in `YnabClientTests.fs` (400, 401, 404, 500, unlesbare 201-Antwort): je fester deutscher Satz, kein Marker, kein englischer Text.
- [x] Intakt, Bob: gelingt der Push, ist `push_error` `null`; Grenzen nach ADR 0068 und `BotApiTests.fs` sonst unverändert grün.
- [x] Kaputt, Leser (Handler-Ebene), vor dem Fix rot: GIVEN ein Budget-Ereignis, das sich nicht dekodieren lässt, WHEN `loadApplicationWith` bzw. `loadMovementsWith`, THEN ein fester Satz ohne Ereignis-Id und ohne Guid; lesbare Ereignisse liefern dasselbe wie vorher.
- [x] Kaputt, Domain-Ebene, vor dem Fix rot: `planMove` und `planAssign` mit einer im Monat fehlenden Kategorie melden `CategoryNotVisibleText` ohne Guid und Monatsschlüssel. Intakt: `planAssign` sammelt weiter beide Fehler (fehlende Kategorie und Betrag neben dem Cent).
- [x] `reportOf` loggt ein gescheitertes Lesen von Zu verteilen mit Grund (im Diff sichtbar, kein Test: ein Log ist kein beobachtbares Verhalten nach Doktrin Punkt 1 und 6).
- [x] Tests, die heute einen Rohtext oder englischen Text erwarten (etwa "Bad request", "Failed to parse month"), sind auf die festen Sätze umgestellt und im Outcome je Datei genannt.
- [x] Keine Änderung an Typen in `src/Shared` (die Fälle von `YnabError` bleiben). Falls doch nötig: Fixtures in `scripts/*.test.mjs` im selben Schritt nachgezogen.
- [x] Doku: README infrastructure bekommt ein Bullet **Fehler über die Grenze** (fester Satz mit Kennung, Ausnahme und YNAB-Rumpf nur im Log). `docs/bot-api.md` und README ledger (**Bot-Commit**) sind geprüft und nennen, dass `push_error` ein fester deutscher Satz ist. README-Deltas mit `npm run check:readme-delta -- --bc <kontext>` je berührtem Kontext geprüft.
- [x] Die Entscheidungen aus "Entscheidung Roman" sind umgesetzt und im Outcome genannt.
- [x] `./verify.sh` zeigt `VERIFY: PASS`.

## Entscheidung Roman (2026-11-07)
Roman am 2026-11-07 (AskUserQuestion): 1. A, alte Gründe in `events` stehen lassen und so anzeigen. 2. A nach Empfehlung des Conductors, nicht in dieser Task, Zeile in die Kann-Sammlung infrastructure. 3. A, Token-Vergleich bei Bob mit SHA-256 mitnehmen.

Die Optionen, wie sie zur Wahl standen:
**1. Was passiert mit den Rohtexten, die schon in `events` stehen?** `events` ist append-only, Trigger lehnen jedes Ändern ab (ADR 0083). Die budgeting-Empfehlung sagt: "Bereits gespeicherte Rohtexte in events bleiben stehen (Ereignisse werden nicht umgeschrieben)." Offen ist, was die Anzeige mit ihnen tut.

- A: Stehen lassen und so anzeigen, wie sie gespeichert sind. Neue Gründe sind ab dem Fix fest.
- B: Beim Lesen ersetzen: ein Leser erkennt die alten Muster ("YNAB hat abgelehnt: Bad request:", "Netzwerkfehler: HTTP ") und zeigt den festen Satz. `events` bleibt unverändert.
- C: Einmalige Migration als Ausnahme wie bei ADR 0084 Punkt 4. Trigger aus, Gründe umschreiben, Trigger an.

Empfehlung: A. Es sind wenige Zeilen in Romans eigener Datenbank, sie stehen nur in Historie und Bericht. B rät an Textmustern herum und bleibt für immer im Code, C bricht die append-only-Regel für einen kosmetischen Gewinn.

**2. Was zeigt der Browser bei einer unerwarteten Ausnahme?** Heute und nach dieser Task zeigt der Toast den englischen Satz des Proxys, nur der Antwortrumpf wird sauber. Den deutschen Satz zu zeigen, hieße an 101 Stellen in sieben Client-Dateien `ex.Message` durch eine Hilfsfunktion zu ersetzen, die bei Status 500 den Rumpf liest (`ProxyRequestException.ResponseText`).

- A: In dieser Task nicht; als Zeile in die Kann-Sammlung infrastructure.
- B: Mitnehmen, mit einer Hilfsfunktion neben `Retry.isNetworkError` (`src/Client/Retry.fs:18`) und einem Test unter .NET für sie.

Empfehlung: A. Unerwartete Ausnahmen sind selten (Fachfehler kommen als `Result` mit deutschem Satz), und die Änderung berührt jede Seite.

**3. Token-Vergleich bei Bob mitnehmen?** `tokenMatches` (`BotApi.fs:276`) vergleicht mit `FixedTimeEquals` auf Bytes ungleicher Länge; bei ungleicher Länge kehrt der Vergleich sofort zurück und verrät die Länge über die Antwortzeit (ledger Gruppe 4, niedrig). Die ledger-Empfehlung nennt es optional.

- A: Mitnehmen: beide Seiten vor dem Vergleich mit SHA-256 hashen. Die bestehenden 401-Tests bleiben, dazu ein Test mit falschem Token anderer Länge.
- B: Nicht mitnehmen; Zeile in die Kann-Sammlung ledger.

Empfehlung: A. Drei Zeilen, die eine Sicherheitsgrenze von Bob härten; die Grenzen aus ADR 0068 bleiben unberührt.

## Nicht dazu
- Die übrigen Schreibwege in `YnabClient.fs` mit demselben Muster ("Bad request: {bodyText}", "HTTP {statusCode}: {bodyText}", "Invalid YNAB token", etwa `:1110`, `:1298`, `:1388`, `:1452`, `:1531`, `:1595`, `:1672`, `:1736`, `:1877`, `:2224`, und die Lesewege `:242`, `:277`, `:614`, `:2154`): die gewählte ledger-Option schließt "alle YNAB-Texte auf Deutsch" aus. Der Conductor nimmt sie als Zeile in die Kann-Sammlung infrastructure (infrastructure-hgbey); die Helfer aus Punkt 2 machen sie später zu einem mechanischen Schritt.
- Englische Meldungen mit `ex.Message` in Settings und Regeln (`Api.fs:220`, `:279`, `:299`, `:317`, `:412`, `:421`, `:430`, `:573`, `:585`, `:593`, `:727`): ebenso Kann-Sammlung infrastructure.
- Der gemeinsame Barbuchungs-Befehl und die Kontoauflösung für Quick Add und Bob: ledger-bk3q8.

## Notes
- ledger-bk3q8 fasst ebenfalls `BotApi.fs` (`ResolveTarget`, `Push`, `bookWithinLimit`) und Quick Add in `Api.fs` an und verweist für die Fehlertexte hierher. Keine harte Abhängigkeit; wer als Zweiter läuft, gleicht die Stellen ab.
- Der Marker in den Tests ist ein erfundener Text. Keine echten YNAB-Antworten, Beträge oder Kategorienamen in Tests, Doku oder Commits.
- `ynabErrorText` (`src/Shared/Domain.fs:671`) setzt vor `NetworkError` "Netzwerkfehler: " und vor `InvalidResponse` "YNAB hat abgelehnt: ". Die festen Sätze aus `statusFailure` und `unreadableResponse` passen dahinter; der Worker prüft, dass keine doppelte Einleitung entsteht.

## Outcome

Fehler über die Grenze tragen nur noch feste deutsche Sätze. `./verify.sh`: `VERIFY: PASS` (dotnet 4356 grün, 13 opt-in übersprungen, node 316 grün).

**Was geändert ist**
- Fehlerhandler (`src/Server/Api.fs`): `remotingErrorResult newId log methodName ex` ist öffentlich und testbar. Er schreibt "Fable.Remoting ERROR in {Methode} (Kennung {id}): {ex}" ins Log und propagiert "Interner Fehler im Server (Kennung {id}), Details stehen im Server-Log." Die Kennung sind die ersten 8 Hex-Zeichen einer Guid (`newErrorId`). Alle sechs `withErrorHandler` in `webApp` (SettingsApi, YnabApi, RulesApi, SyncApi, LedgerApi, BudgetApi) nutzen weiter denselben `errorHandler`, der nur noch delegiert.
- YNAB-Client (`src/Server/YnabClient.fs`): die Helfer stehen jetzt vor `interpretMonthResponse` und nehmen die HTTP-Methode als Parameter (`statusFailureOf`, `unreadableResponseOf`, `fetchFailureOf`, dazu neu `badRequestOf`). `statusFailure`, `unreadableResponse` und `fetchFailure` bleiben als GET-Kurzformen.
  - Monat lesen: Dekodierfehler, unerwarteter Status und Ausnahme laufen über die Helfer.
  - Zuweisen: 400 wird zu "Zuweisung ungültig (HTTP 400)", unerwarteter Status und Ausnahme laufen über die Helfer. 401, 404 (`MonthNotOpen`) und 429 (`RateLimitExceeded`) sind unverändert.
  - Buchung anlegen: neu als reine Funktion `interpretCreateTransactionResponse` plus Naht `createTransactionVia`, die `createTransactionWithFlag` nutzt. 400 wird zu "Buchung ungültig (HTTP 400)", 401 bleibt `Unauthorized` (Text über `ynabErrorText`: "YNAB-Token ungültig (HTTP 401)"), 404 wird zu "Budget oder Konto nicht gefunden (HTTP 404)". Unerwarteter Status, unlesbare 201-Antwort und Ausnahme laufen über die Helfer, der Rumpf geht nur ins Log.
  - Keine doppelte Einleitung: die Sätze tragen kein "YNAB hat abgelehnt" und kein "Netzwerkfehler" selbst, das setzt `ynabErrorText` davor.
- Bob (`src/Server/BotApi.fs`): `push_error` kommt aus `ynabErrorText` über die festen Sätze. Jeder Grund an Bob ist geprüft: `ynab_unavailable` (`LoadCategories`: `ynabContext` mit festen Sätzen, `getCategories` über `statusFailure`, `unreadableResponse`, `fetchFailure`, 401 und 404 als feste Fälle), `Push` ohne Einrichtung als `Unauthorized`, `no_quick_add_account` aus den festen Sätzen von `resolveQuickAddAccount`. `tokenMatches` hasht beide Seiten vor `FixedTimeEquals` mit SHA-256.
- Leser (`src/Server/Budgeting/Persistence.fs`): `loadApplicationWith` und `loadMovementsWith` laufen über `readWith` mit `logReadFailure`. Neue feste Sätze: `ApplicationUnreadableText` ("Die Zeilen der Entscheidung sind nicht lesbar") und `MovementsUnreadableText` ("Historie nicht lesbar").
- Ablehnungen (`src/Server/Budgeting/Domain.fs`): `planMove` und `planAssign` melden `CategoryNotVisibleText`. `planAssign` sammelt weiter beide Gründe.
- `reportOf` (`src/Server/Budgeting/CommandHandlers.fs`): ein gescheitertes Lesen von Zu verteilen geht mit Grund ins Log (`logReadFailure "Zu verteilen für den Bericht lesen" (ynabErrorText err)`). Der Bericht kommt weiter mit `ActualToBeBudgeted = None`. Kein Test, laut Kriterium.
- Keine Änderung an `src/Shared`, keine Fixture-Änderung nötig.
- Doku: `docs/bot-api.md` sagt jetzt, dass `push_error` und die `message` von `ynab_unavailable` feste deutsche Sätze sind.

**Entscheidung Roman umgesetzt**
1. A: alte Gründe in `events` bleiben stehen und werden so angezeigt, wie sie gespeichert sind. Kein Leser ersetzt sie, keine Migration.
2. A: der Toast im Browser zeigt weiter den Satz des Proxys. Diese Zeile gehört in die Kann-Sammlung infrastructure, siehe unten.
3. A: Bobs Token-Vergleich läuft über SHA-256 (`tokenMatches`). Die bestehenden 401-Tests sind unverändert grün. Neu ist der Test "falsche Tokens gleicher, kürzerer und längerer Länge werden abgelehnt, das richtige nicht" (`BotApiTests.fs`). Er prüft Ablehnung und Zulassung. Die Zeitgleichheit selbst misst kein Test.

**Tests (alle vor dem Fix rot, bis auf die mit "intakt" markierten)**
- `src/Tests/RemotingPipelineTests.fs`:
  - `errorHandlerTests`, "der Fehlerhandler gibt nur einen festen Satz mit Kennung weiter …": prüft, dass der propagierte Wert keinen Marker, keinen Stacktrace und kein "Exception" enthält, aber die Kennung trägt, und dass das Log Marker, Kennung und Methode enthält.
  - Verdrahtung, "eine Ausnahme in einem Remoting-Aufruf kommt ohne Meldung und Stacktrace im Rumpf an": schickt je Interface aus `pipelineCalls` einen kaputten Rumpf mit Marker durch den echten Host. Das löst ohne Produktionsschalter eine Ausnahme beim Deserialisieren aus. Erwartet wird 500 ohne Marker und ohne Typnamen, mit festem Satz. Vor dem Fix trug der Rumpf `ClassName`, `Message` mit Marker und `StackTraceString`.
- `src/Tests/YnabClientTests.fs`, `fixedErrorTextTests`:
  - Monat 500, unlesbarer 200-Rumpf und Ausnahme in `getMonthWith`.
  - Buchung anlegen mit 400, 401, 404, 500 und unlesbarer 201-Antwort, je exakter fester Satz, ohne Marker und ohne englische Wörter.
  - Ausnahme in `createTransactionVia`.
  - Intakt: 201 mit Id liefert die Id, 429 bleibt `RateLimitExceeded`.
- `src/Tests/TargetsApplyTests.fs`:
  - Neu "400 und ein unerwarteter Status tragen einen festen Satz ohne Antwortrumpf" (400 und 503 mit Marker).
  - `setAssignedWith` mit werfendem `send` gibt jetzt "Netzwerkfehler: YNAB nicht erreichbar".
- `src/Tests/MoveMoneyTests.fs`:
  - Handler-Ebene "GIVEN YNAB lehnt den PATCH mit 400 ab WHEN Zuweisen THEN nennen Ereignis und Bericht einen festen Satz ohne Rumpf". Der Fake nutzt die echte `interpretAssignResponse`, geprüft werden Bericht und `Latest` aus `events`.
  - Domain: `planMove` auf beiden Seiten und `planAssign` ohne Guid und ohne Monatsschlüssel. Intakt: beide Gründe gesammelt.
- `src/Tests/BotApiTests.fs`:
  - "GIVEN YNAB lehnt die Buchung mit 400 ab WHEN Bob committet THEN ist push_error ein fester Satz ohne Rumpf": der Push-Fake nutzt die echte `interpretCreateTransactionResponse`. Geprüft werden `pushed_to_ynab = false`, der feste Satz und der Grund im Kassenbuch.
  - Token-Test, intakt.
- `src/Tests/BudgetHistoryTests.fs`, `unreadableReaderTests`: ein kaputtes Entscheidungsereignis in eigener In-Memory-DB. `loadApplicationWith` und `loadMovementsWith` (ohne Monat und mit Monat) liefern den festen Satz ohne Ereignis-Id und ohne Stream.
- Intakt, bestehende Tests grün: 429 mit "Rate-Limit" und erneutem Senden (`MoveMoneyTests` "429 beim PATCH …"), 404 als "Monat nicht offen" (`BudgetingTests`, `TargetsApplyTests`), 401 als "YNAB-Token ungültig (HTTP 401)" (`MoveMoneyTests` "Jeder YNAB-Fehler hat einen deutschen Satz"), gelungener Bob-Push mit `push_error: null`, alle Grenzen nach ADR 0068 in `BotApiTests.fs`. Lesbare Ereignisse liefern dasselbe (alle bestehenden Historien- und Berichtstests grün).

**Umgestellte Tests, die Rohtext oder englischen Text erwarteten**
- `src/Tests/BudgetingTests.fs`: "200 mit kaputtem JSON ist eine ungültige Antwort", "Anderer Status ist ein Netzfehler mit Status" und "Eine Ausnahme beim Abruf wird zum Netzfehler" (erwartete "kaputt" in der Meldung) sind entfernt. Sie stehen jetzt mit festen Sätzen in `fixedErrorTextTests` (`YnabClientTests.fs`), damit dieselbe Aussage nicht doppelt geprüft wird.
- `src/Tests/TargetsApplyTests.fs`: der 400- und der 503-Fall aus "Statuscodes werden zugeordnet" stecken jetzt im neuen Test mit festen Sätzen. Der Ausnahme-Fall von `setAssignedWith` erwartete "kaputt" und erwartet jetzt den festen Satz.
- `src/Tests/MoveMoneyTests.fs`: vier Erwartungen auf `string missing` bzw. "fehlt" stehen jetzt auf `Domain.CategoryNotVisibleText`.

**Gegenprobe**
- Vor dem Fix rot: Fehlerhandler 1/1, Pipeline 1/1, YNAB-Sätze 5/6 (rot außer dem intakten 201/429-Test), Zuweisen 400/503 1/1, `setAssignedWith`-Ausnahme rot, Handler-400 rot, Domain 2/2, Bob-400 1/1, Leser 2/2.
- Mutante M1: im `webApp` bekommt nur der BudgetApi `withErrorHandler (fun ex _ -> Propagate ex)`. Ergebnis: der Pipeline-Test ist rot. Danach zurückgesetzt, Gate grün.
- Ohne Test bleiben zwei Stellen. `tokenMatches` ohne Hash bleibt im Verhalten gleich, nur die Antwortzeit ändert sich. Bei `reportOf` steht nur eine Logzeile im Diff, beides wie im Kriterium vorgesehen. Die Liste gilt nur für diese Mutanten.

**README-Deltas** (alle mit `npm run check:readme-delta` geprüft, Exit 0)
- infrastructure: Bullet **Fehler über die Grenze**, im README_DELTA-Block.
- ledger (Abschnitt "Ubiquitous language", replace auf **Bot-Commit**): Nach dem Satz "Die Antwort nennt `entry_id`, `pushed_to_ynab` und `push_error`, auch bei gescheitertem Push (dann `pushed_to_ynab: false`, ein zweiter Commit pusht nicht erneut)." kommt neu dazu: "`push_error` ist ein fester deutscher Satz aus `ynabErrorText` (etwa \"YNAB hat abgelehnt: Buchung ungültig (HTTP 400)\"), YNABs Antworttext steht nur im Server-Log (infrastructure-vp308)." Der Kopf wird zu "**Bot-Commit** (ledger-b0b7q, ledger-z71ka, infrastructure-vp308):". Das Delta liegt geprüft unter /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/84a6753c-d648-4480-b0ae-63597496f4a6/scratchpad/delta-ledger.json.
- budgeting (Abschnitt "Key events", append), geprüft unter /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/84a6753c-d648-4480-b0ae-63597496f4a6/scratchpad/delta-budgeting.json: "- **Grund eines Rückschreib-Fehlschlags** (infrastructure-vp308): `AssignmentWriteBackFailed` trägt seit diesem Fix nur feste Sätze aus `ynabErrorText` (bei 400 \"YNAB hat abgelehnt: Zuweisung ungültig (HTTP 400)\", sonst etwa \"Netzwerkfehler: YNAB antwortet mit HTTP 503\"), nie YNABs Antwortrumpf; ältere Ereignisse behalten ihren gespeicherten Grund und werden so angezeigt. Eine im Monat fehlende Kategorie lehnen Verschieben und Zuweisen mit `CategoryNotVisibleText` ab, ohne Guid und Monatsschlüssel. Ist ein Budget-Ereignis nicht lesbar, melden der Bericht einer Entscheidung `ApplicationUnreadableText` (\"Die Zeilen der Entscheidung sind nicht lesbar\") und die Historie `MovementsUnreadableText` (\"Historie nicht lesbar\"), die Ursache steht im Log (`readWith`). Scheitert im Bericht das Lesen von Zu verteilen, steht der Grund im Log und der Bericht kommt mit `ActualToBeBudgeted = None`."
- Kursive Key-command-Bullets: keins berührt.

Nachtrag 2026-11-07 an ADR 0066 angehängt. Die Deltas für ledger (**Bot-Commit**) und budgeting (**Grund eines Rückschreib-Fehlschlags**) hat der Conductor nach Prüfung mit `readme-delta-check` angewendet. Die Kann-Punkte stehen in infrastructure-hgbey.
