---
id: budgeting-nfrds
title: "Verschieben absichern: Erneut senden hält die Reihenfolge ein, das Sheet rechnet mit dem aktuellen Stand, Monat und Eingaben werden an der API-Grenze festgelegt"
status: done
type: bug
context: budgeting
created: 2026-10-30
completed:
depends_on: [design-system-001, budgeting-afd8h]
blocks: [infrastructure-mngfn, categorization-f955d]
tags: [review, verschieben, mobile]
related_adrs: [0065, 0069, 0070]
related_research: []
prior_art: [budgeting-afd8h]
---

## Why
Sammel-Task aus den vier Reviews des work-Laufs vom 2026-10-30 (Umfang `fc24d9d~1..5d7b09f`). Der Owner will keine vielen Klein-Tasks, deshalb liegt alles zum Verschieben hier. Es geht um echtes Geld: Ein Abbruch oder eine alte Momentaufnahme kann Budget still aus Zu verteilen ziehen oder einen falschen Betrag vorbelegen.

Die Befunde stammen aus dem Lesen des Codes. Jeder Bugfix startet mit einem Regressionstest, der vor dem Fix rot ist. Bleibt der Test grün, war der Befund falsch: dann entfällt der Fix und das Outcome nennt das.

## What
**Backend (`src/Server/Budgeting/CommandHandlers.fs`, `Domain.fs`, `src/Server/Api.fs`)**
- `retryFailed` (`CommandHandlers.fs:313-320`, `:94`): `held` wird heute nur aus im selben Lauf gesendeten Zeilen gesetzt. Der Vorgängerstand kommt künftig aus `states`, `Latest = None` zählt wie fehlgeschlagen. Das deckt beide Fälle ab: Quelle ist `ChangedInYnab` und das Ziel würde trotzdem gesendet (Geld aus Zu verteilen), und Abbruch nach der Quelle lässt das Ziel als „nicht gesendet“ ewig offen.
- Entscheidung des Owners zu ADR 0070: Vor dem erneuten Senden werden beide Stände geprüft. Ist das Ziel schon verloren (YNAB gewinnt), wird die Quelle nicht gesendet. Das kommt als Nachtrag in ADR 0070.
- `moveMoney` (`Api.fs:1848`) löst den Monat nicht mehr serverseitig über `DateTime.Today` auf. Der Client schickt `BudgetMonth.Month`.
- `MoveRequest` wird an der API-Grenze geprüft (`src/Server/Validation.fs`, alle Fehler gesammelt): Überlauf in `isOnTenMilliunits`, Obergrenze vor dem Festhalten, Währung. Zu große Beträge dürfen keine Ausnahme werfen und nicht als „Netzwerkfehler“ erscheinen.
- Fehlertexte im Sheet sind deutsch (`Api.fs:1791-1793`). Es gibt genau eine Übersetzung von `YnabError` an einer Stelle, die `categorization` ohne Abhängigkeit auf `Server.Budgeting.CommandHandlers` nutzen kann.
- Herkunft wird als DU `MovementOrigin` geführt, der `string` steuert `inOrder` nicht mehr, kein `failwith`.

**Client (`src/Client/Components/Budget/{Types,State,View}.fs`, `src/Client/State.fs`)**
- `MoveForm.Category` friert die Kategorie ein (`Types.fs:34`, `:88`, `:250`, `:308`). Künftig `OpenMove of Guid`, Vorbelegung und „Verfügbar danach“ werden aus `month` abgeleitet.
- `MoveForm.Sending` wird durch `Submission` mit `Editing | Sending | Rejected of string` ersetzt.
- „Erneut senden“ bleibt erreichbar, nachdem der Toast weg ist. `MoveRetried (Error _)` führt zu sichtbarer Rückmeldung und Reload (`Types.fs:392`, `State.fs:117-118`, `Client/State.fs:95`).
- Der Auslöser schließt das Popover am PC (`View.fs:524`, `onMouseDown stopPropagation`). Das Formular überlebt den Seitenwechsel nicht, ein gescheiterter stiller Reload ersetzt die Liste nicht (`State.fs:66`).

**Design**
- Dialog nutzt `Modal.body`, `popoverInDialog` und `Modal.Large` (`View.fs:533`, `:584-585`, `:604`), Popover ragt nicht mehr über den Dialog.
- `Money.fs:242`: `aria-label` nennt den Betrag. Verfügbar ist als bedienbar erkennbar, mit Hover am PC.
- `View.fs:183-190`: Icon-Knopf mit `AriaLabel`.
- `standards/frontend/styleguide.md`: Zeile „Zu verteilen“ im Abschnitt Kategorie-Picker, `Money.available` und `availableButton` im Inventar. Demo in `/styleguide`.

## Acceptance criteria
- [ ] Test: Quelle `ChangedInYnab`, Ziel ohne Stand, `retryFailed` sendet das Ziel nicht.
- [ ] Test: Quelle gesendet, Ziel `Latest = None`, `retryFailed` greift das Ziel auf, der Bericht weist es nicht mehr als dauerhaft offen aus.
- [ ] Test: Beide Zeilen fehlgeschlagen, Ziel in YNAB geändert, `retryFailed` sendet die Quelle nicht.
- [ ] ADR 0070 trägt den Nachtrag zu diesem Fall.
- [ ] Test: Der Server schreibt in den vom Client geschickten Monat, unabhängig von `DateTime.Today`.
- [ ] Tests: Überlauf, zu großer Betrag und falsche Währung liefern einen konkreten deutschen Fehler an der API-Grenze, ohne dass eine Budget-Bewegung festgehalten wird.
- [ ] Tests: Fehlertexte aus `moveMoney` sind deutsch, es gibt eine einzige `YnabError`-Übersetzung.
- [ ] Test: Zwei Verschiebungen kurz hintereinander, das zweite Sheet zeigt Vorbelegung und „Verfügbar danach“ aus dem aktuellen `month`.
- [ ] Wächter in `scripts/move-money.test.mjs`: Reload nach „Erneut senden“, `MoveCallLost`, „Erneut senden“ erreichbar nach Ablauf des Toasts, Popover schließt per Auslöser, Sheet springt nach Seitenwechsel nicht wieder auf.
- [ ] Fable.Remoting-Fixtures in `scripts/*.test.mjs` sind mitgezogen.
- [ ] Verfügbar nennt dem Screenreader den Betrag, Icon-Knöpfe haben `AriaLabel`.
- [ ] Styleguide und `/styleguide` zeigen „Zu verteilen“, `Money.available` und `availableButton`.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.
- [ ] Verfügbar sieht am Handy antippbar aus, das Popover steht bei 1280×720 ohne Überstand im Dialog. [human-eye]

## Notes
Live-Probe aus `budgeting-afd8h` lief am 2026-10-30 grün: YNAB nimmt ein negatives Zugewiesen an. Die Annahme aus ADR 0069 hält.

Diese Task läuft allein, weil sie groß ist und mehrere Dateien trifft, die die beiden Folge-Tasks ebenfalls berühren.

Niedrige Befunde aus den Reviews laufen hier nur mit, wenn sie in ohnehin angefassten Zeilen liegen (etwa `MovePlan.Month` ungenutzt, veraltete Kommentare). Dafür entstehen keine eigenen Tasks.

## Verifier note (iteration 1)
REASONS:
- AC "Überlauf, zu großer Betrag und falsche Währung liefern einen konkreten deutschen Fehler an der API-Grenze": Die Grenze ist einseitig. `validateMoveRequest` prüft nur `Amount > MaxMoveAmount`. `Amount = -Decimal.MaxValue` an `Server.Api.moveMoneyWith` wirft `System.OverflowException`, nach Codelage aus `planMove` (`c.Assigned.Amount + sign * amount`, src/Server/Budgeting/Domain.fs), bevor `amount <= 0m` greift. Ein solcher Auftrag liest zudem YNAB, bevor er scheitert. Kein Test für negative Beträge an der Grenze.
- AC "Der Auslöser schließt das Popover": Gegenprobe bleibt grün. `prop.onMouseDown (fun e -> e.stopPropagation ())` in src/Client/Components/Budget/View.fs entfernt: scripts/move-money.test.mjs 9 von 9 grün. Entweder bildet der Test den Bug nicht ab, oder der Befund war falsch; dann verlangt die Task, dass der Fix entfällt und das Outcome das nennt.
- AC "es gibt eine einzige YnabError-Übersetzung": stimmt nicht. In src/Server/Ledger/CommandHandlers.fs stehen weiter `describeYnabError` (Zeile 140), `describePushFailure` (Zeile 628), `describeReviewMarkPushError` (Zeile 1133). Kein Test belegt die Einzigkeit.
Geprüft und in Ordnung: Gate, 9 Server-Mutationen und 5 Client-Mutationen rot, ADR-0070-Text passt zum Code, Quelle-ungesendet-Regel gefährdet die Invariante nicht.
SUGGESTED_FIX: `validateMoveRequest` prüft den Betrag beidseitig vor `planMove`, mit Zeile in der Negativtabelle und Test für `-Decimal.MaxValue`. Den Popover-Wächter so umbauen, dass er ohne `onMouseDown` rot wird, oder den Fix entfernen und das im Outcome nennen. Die drei Ledger-Übersetzungen auf `ynabErrorText` zusammenführen.
ITERATION_HINT: likely-fixable

## Outcome

`./verify.sh`: `VERIFY: PASS` (dotnet 2791 grün, node 341 grün). Das README-Delta ist mit `scripts/readme-delta-check.mjs` geprüft (Exit 0).

**Backend.** `retryFailed` (`src/Server/Budgeting/CommandHandlers.fs`) nimmt den Vorgängerstand jetzt aus `states`, `Latest = None` zählt als offen (`isOpenLine`), auch bei Targets anwenden. Vor dem ersten Senden prüft es beide Stände: ist das Ziel verloren, geht die Quelle nicht hinaus und schließt als "in YNAB geändert"; steht die Quelle schon auf dem Ziel, gilt sie als geschrieben. Fehlt das Ziel im Monat, bleibt die Quelle ungesendet fehlgeschlagen (Grund `SourceHeldBack`). Dieser letzte Fall stand nicht in der Task, ich habe ihn wie den Nachtrag behandelt. `moveMoneyWith` (`src/Server/Api.fs`) ist der Weg ohne Settings und Netz, der Monat kommt vom Client. `validateMoveRequest` (`src/Server/Validation.fs`) sammelt Monat, Währung (nur EUR) und den Betrag, beidseitig: größer 0 und höchstens 1.000.000 €. Sie läuft vor dem Lesen, `-Decimal.MaxValue` wirft keine Ausnahme mehr. `isOnTenMilliunits` rechnet per Rest und läuft nicht mehr über. `MovementOrigin` steht in `src/Server/Budgeting/Domain.fs`, `originOfText` liefert `Result`, deshalb gibt `loadApplication` jetzt `Result` zurück. `MovePlan.Month` ist entfallen.

**Eine Übersetzung.** `ynabErrorText` steht in `src/Shared/Domain.fs`. Die Duplikate in `src/Server/Api.fs`, `src/Client/State.fs`, `src/Client/Components/Settings/State.fs`, `failureReason` und jetzt auch `describeYnabError`, `describePushFailure`, `describeReviewMarkPushError` aus `src/Server/Ledger/CommandHandlers.fs` sind weg.

Die Texte sind als Daten gespeichert: `Reason` in den Ereignissen `EntryPushToYnabFailed` und den `...WriteBackFailed`-Ereignissen, `MirrorRunFailed` im letzten Spiegel-Lauf, `Failed` in den Budget-Bewegungen. Geprüft: Kein Code wertet sie aus (kein Vergleich, kein `Contains`), sie werden nur angezeigt. Alte Einträge behalten ihren alten Text, es gibt keine Migration, die Historie zeigt also künftig beide Fassungen.

Das Zusammenführen ändert sichtbares Verhalten, deshalb hier ausdrücklich:
- Gemerged wurden die Inhalte: `ynabErrorText` nennt jetzt bei 401 und 429 den HTTP-Status und bei 429 die Wartezeit ("Rate-Limit (HTTP 429), erneut in 60 Sekunden", "YNAB-Token ungültig (HTTP 401)"). Das reine "Rate-Limit" aus Iteration 1 hätte Wartezeit und Status aus den Kassenbuch-Texten verloren.
- YNAB-Ids stehen nicht mehr im Text (Budget, Konto, Kategorie). Der Text geht über `push_error` an Bob, einen externen Agenten, und in das dauerhafte Log. Zwei der drei Ledger-Texte hatten keine Ids, Bob wäre sonst neu an sie gekommen. Preis: Settings, Sync und der Spiegel-Lauf nennen die Id nicht mehr.
- Die Fehlermeldung aus `Unauthorized` entfällt im Spiegel-Lauf und beim Prüfvermerk, dort steht nur noch "YNAB-Token ungültig (HTTP 401)".
- Bob bekommt in `push_error` und beim Kategorien-Laden die neuen Sätze. Ein Abgleich auf den alten Wortlaut in Bob selbst liegt außerhalb dieses Repos und ist nicht geprüft.
- Die Budget-Zeile, die den 429 selbst bekommt, trägt jetzt den langen Text. Die dahinter abgebrochenen Zeilen behalten das feste "Rate-Limit" (`src/Server/Budgeting/CommandHandlers.fs`).
- Angepasste bestehende Tests: `TargetsApplyTests`, `BudgetingTests`, `LedgerMemoTests`, `LedgerCategoryChangeTests`, `LedgerReviewMarkTests`, `LedgerPushFailureTests`, `LedgerRetryPushTests` (Erwartungstexte und Marker "Verbindung", "Zugang" wurden zu "Netzwerkfehler", "401").

Fachlich anders bleiben vier Texte in `src/Server/Ledger/CommandHandlers.fs`: `describeCategoryPushError`, `describeSplitPushError`, `describeFlagPushError`, `describeMemoPushError`. Sie übersetzen nur `InvalidResponse` selbst, weil der Hinweis nennt, was YNAB abgelehnt hat (Kategorie, Aufteilung, Flag, Memo). Alles andere geben sie an `ynabErrorText` weiter.

Für die Einzigkeit gibt es keinen Wächter und keinen Test (prose-only, unenforced). Punkt 7 der Test-Doktrin deckt ihn nicht: er erlaubt Strukturtests für repo-weite Invarianten ohne Verhaltenszugang und schließt Wächter für eine gerade gefixte Stelle und Rückbau-Wächter aus. Das AC "es gibt eine einzige YnabError-Übersetzung" ist damit im Code erfüllt, die Einzigkeit selbst belegt nur die Suche: `grep` nach einem Match über `YnabError` in `src` zeigt nur `ynabErrorText` und die vier Weitergaben. Die Texte der Kassenbuch-Wege sind per Verhaltenstest gesichert.

**Client.** `MoveForm` hält nur `CategoryId`, `AmountInput` (`None` heißt Vorbelegung aus dem aktuellen `month`) und `Submission`. `Reloaded`/`applyReloaded` lassen die Liste bei gescheitertem stillem Neu-Lesen stehen und zeigen einen Hinweis. `PageEntered` schließt das Sheet. Fehlt X im neuen Stand, schließt es ebenfalls. "Erneut senden" bleibt über `Model.OpenRetries` als Hinweis auf der Seite (`data-testid="move-retry"`) erreichbar, auch für mehrere Verschiebungen. Dieser Hinweis lebt nur im Client: nach einem Neuladen der App ist er weg, die fehlgeschlagene Zeile in der Datenbank bleibt. Eine serverseitige Liste offener Verschiebungen gibt es nicht.

**Popover: der Bug ist echt, die Zeile bleibt.** Ohne `prop.onMouseDown` in `src/Client/Components/Budget/View.fs` zählt der Mausdruck als Klick außerhalb und schließt das Popover, der Klick öffnet es wieder. Dass der alte Wächter grün blieb, hat eine Ursache, die ich im Browser gemessen habe: Das volle Popover scrollt `Modal.body` um 104 px. Schließt es beim Mausdruck, springt der Auslöser zurück, das Loslassen trifft ihn nicht mehr (`mousedown` auf dem Knopf, `mouseup` und `click` auf einem anderen Element). Der Bug zeigt sich also, sobald das Popover kurz genug ist, um nicht zu scrollen. Mit einem Sucheintrag ohne Treffer liegt der Auslöser still, und `mousedown`, `mouseup`, `click` treffen ihn alle drei: das Popover ging wieder auf. Der Wächter in `scripts/move-money.test.mjs` tippt deshalb den Filter ein und prüft den Fußtext des Popovers. Das behebt nicht den Scroll-Sprung selbst: ohne Filter sitzt der Auslöser im offenen Zustand zum Teil unter dem Dialogkopf.

**Design.** `Modal.body`, `popoverInDialog`, `Modal.Large`, der Auslöser stoppt `onMouseDown`. `Money.availableButton`: `aria-label` mit Betrag, gestrichelte Linie, Hover-Fläche. Suche-Leeren hat `AriaLabel`. Styleguide und `/styleguide` sind ergänzt, die Picker-Demo mit Zu verteilen gab es schon.

**ADR 0070, Nachtrag: nicht im ADR, Conductor muss ihn einfügen.** Eine Worker-Regel verbietet das Schreiben unter `.agentheim/`, das AC verlangt aber den Text im ADR. Der fertige Text steht unten unter "Text für ADR 0070" und gehört ans Ende von `0070-ungesendetes-ziel-schliesst-wenn-ynab-bei-der-quelle-gewinnt.md`. Das AC bleibt offen, bis das geschehen ist.

**Gegenproben dieser Iteration, wirklich ausgeführt, je Regel entfernt und danach wiederhergestellt.**
- Betrag: Prüfung `<= 0` aus `validateMoveRequest` entfernt. Rot: "Null und negative Beträge werden abgelehnt, bevor YNAB gelesen wird, auch am Rand von decimal" (`MoveMoneyTests`), 1 von 2791. Die neue Zeile der Negativtabelle blieb dabei grün: in der Tabelle ist kein Token gesetzt, sie endet auch ohne die Regel mit einem Fehler. Sie erfüllt die Pflichtzeile, belegt die Regel aber nicht, das tut der `MoveMoneyTests`-Fall.
- Popover: `prop.onMouseDown` entfernt, Client neu gebaut. Der alte Wächter 9 von 9 grün (bestätigt die Verifier-Beobachtung), auch mit `delay` beim Klick. Der neue Wächter 3 von 3 Läufen rot, `AssertionError: bleibt zu`. Mit der Zeile grün.
- Übersetzung: Kassenbuch-Grund abweichend gesetzt (`"Keine Verbindung: " + ynabErrorText err`, zwei Stellen) und die Budget-Id wieder in den Text genommen. Rot: "Fehlschlag: speichert Grund und neuen Zeitpunkt, bleibt nicht verknüpft" und "Jeder YNAB-Fehler hat einen deutschen Satz". Die zweite Stelle (`reason` beim Prüfvermerk-Erneut-Senden) hat dabei keinen eigenen roten Test gezeigt, der eine rote Ledger-Test kann von nur einer der beiden Stellen kommen.

**Rot-Nachweise richtig gestellt.** Die Regel "Regressionstest vor dem Fix rot" ist nur teilweise durch Rot vor dem Fix belegt:
- Sechs `retryFailed`-Tests sah ich in Iteration 1 vor dem Fix rot (Lauf: 25 grün, 6 rot).
- Der Betrags-Test dieser Iteration ist durch die Gegenprobe belegt, die `OverflowException` selbst hat der Verifier gemessen. Ich habe nur gesehen, dass der Test ohne die Regel rot ist, er stoppt schon beim ersten Betrag 0, weil YNAB gelesen wird.
- Der neue Popover-Wächter ist belegt, siehe oben.
- Die übrigen Tests aus Iteration 1 (Grenze, Herkunft, Client, Browser-Wächter) entstanden zusammen mit dem Produktionscode, vor dem Fix habe ich sie nicht rot gesehen. Der Verifier hat dazu 9 Server- und 5 Client-Mutationen rot gemeldet. Das ist ein Nachweis nach dem Fix, von ihm, nicht von mir, und kein Rot vor dem Fix.
- Das Bild von Iteration 1, der Popover-Auslöser sei abgesichert, war falsch: der alte Wächter blieb ohne den Fix grün.

**Offen:** AC [human-eye] (Verfügbar sieht am Handy antippbar aus, Popover bei 1280×720). Der Wächter misst bei 1280×720 nur den seitlichen Stand im Dialog, nicht den unteren Rand. Zwei schnelle stille Reloads können sich überholen, eine alte Antwort überschriebe dann die neue bis zum nächsten Laden; das habe ich nicht abgesichert. Der Scroll-Sprung des Popovers im Dialog ist unverändert, siehe oben. `src/Client/Components/Rules/View.fs` hat dieselbe Konstruktion, dort habe ich nichts geprüft oder geändert.

Kein Live-Aufruf gegen YNAB, `RUN_INTEGRATION_TESTS` nicht gesetzt, `.env` nicht gelesen.

### Text für ADR 0070

#### Nachtrag 2026-10-31 (budgeting-nfrds, Entscheidung des Owners)

Vor dem erneuten Senden einer Verschiebung werden beide Stände geprüft. Ist das Ziel schon verloren (YNAB steht dort weder auf Baseline noch auf Zielwert), wird die Quelle nicht gesendet: sonst sänke sie, das Ziel bekäme nichts, und das Geld läge in Zu verteilen. Beide Zeilen schließen als "in YNAB geändert", die Verschiebung ist erledigt. Steht die Quelle schon auf ihrem Zielwert (Antwort ging verloren), bleibt sie geschrieben, das Ziel schließt.

Maßgeblich ist der Stand der Vorgänger-Zeile, auch aus einem früheren Lauf. Eine Zeile ohne Stand zählt wie fehlgeschlagen: ein Abbruch nach der Quelle lässt das Ziel nicht ewig offen, und eine geschlossene Quelle schließt ein Ziel ohne Stand.

Fehlt das Ziel im Monat, bleibt die Quelle ungesendet fehlgeschlagen und erneut sendbar. Das ist die Wahl des Workers, nicht des Owners.

Folge für budgeting-dyn8g: auch eine Quelle, die YNAB nie berührt hat, kann jetzt "in YNAB geändert" tragen.

Tests in `src/Tests/MoveMoneyTests.fs`: "GIVEN beide Zeilen fehlgeschlagen, Ziel in YNAB geändert ...", "GIVEN Quelle in YNAB geändert, Ziel ohne Stand ...", "GIVEN Quelle geschrieben, Ziel ohne Stand ...".
