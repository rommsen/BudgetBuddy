---
id: budgeting-nfrds
title: "Verschieben absichern: Erneut senden hält die Reihenfolge ein, das Sheet rechnet mit dem aktuellen Stand, Monat und Eingaben werden an der API-Grenze festgelegt"
status: todo
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
