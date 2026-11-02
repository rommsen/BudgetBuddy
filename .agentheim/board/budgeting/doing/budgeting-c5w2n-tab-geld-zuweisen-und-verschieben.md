---
id: budgeting-c5w2n
title: "Tab Geld: Umschalter Zuweisen, Holen, Abgeben, rechnendes Feld mit Speichern beim Verlassen, Fehlbetrag-Knopf, Handy-Schritte, Holen und Abgeben nach Variante D"
status: doing
type: feature
context: budgeting
created: 2026-11-02
completed:
depends_on: [design-system-001, budgeting-g8s3w, budgeting-p4n6r, budgeting-h3k9d]
blocks: [budgeting-dyn8g]
tags: [budget, zuweisen, verschieben]
related_adrs: [0005, 0055, 0057, 0069, 0070, 0073, 0074, 0075, 0077, 0078]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-afd8h, budgeting-nfrds]
---

## Why
Task 2 von 5 des Gesamtkonzepts. Im Monat soll man direkt handeln, wie Roman es von YNAB kennt, und schneller sein als dort.

## What
Maßstab: `docs/ux/budget/konzept.md`, Abschnitte Zuweisen sowie Geld holen und abgeben. Das Konzept geht dem Prototyp vor: Der Prototyp zeigt noch zwei Modi und Vorschläge im Tab, gewählt sind Variante E (drei Modi) und Variante D (Aufbau von Holen und Abgeben).

- Tab Geld mit Umschalter **Zuweisen, Holen, Abgeben**, das Sheet bleibt gleich hoch.
- Feld Zugewiesen mit Betragsrechnung, Vorschau, Handy-Schritten und Speichern beim Verlassen. Neuer Befehl Zuweisen, der den fertigen Wert absolut setzt (ADR 0078).
- Fehlbetrag-Knopf "12,30 zuweisen" über das vorhandene `moveMoney`.
- Holen und Abgeben nach Variante D, Gegenseite im `CategoryPicker` mit den zuletzt genutzten Kategorien aus den Budget-Bewegungen.
- Der Verschieben-Dialog (`moveDialog`, `Modal` und `BottomSheet`) und der Knopf "Verschieben" entfallen, Offene Verschiebung bleibt.
- Nicht Teil dieser Task: Target pausieren, siehe Entscheidung 1. Sein Platz im Modus Zuweisen bleibt frei.

## Acceptance criteria

Heimat-Ebene in eckigen Klammern nach `.claude/rules/test-doctrine.md`. **[rein]** reine Funktion oder Domain, **[Handler]** Befehl oder Abfrage mit In-Memory-SQLite und Fake-Port, **[Client-update]** reiner Übergang unter .NET (Vorbild `src/Tests/MoveMoneyClientTests.fs`; ein Cmd mit `Client.Api` läuft dort nicht, deshalb stehen die Übergänge in reinen Funktionen), **[Browser]** Wächter in `scripts/*.test.mjs`, **[human-eye]** rein Sichtbares ohne Test.

### Rechnen
- [ ] **[rein]** `evaluateAmount base text` mit Stand 900: `+20` ergibt 920, `-20` 880, `*2` 1800, `x2` 1800, `/2` 450, `:2` 450, `900+40` 940 (ersetzt den Stand), `940` 940.
- [ ] **[rein]** Punkt vor Strich: `2+3*4` ergibt 14, `+3*4` mit Stand 900 ergibt 912.
- [ ] **[rein]** Deutsche Beträge: `1.240,50` ergibt 1240,50, `1.240` ergibt 1240, `12,5` ergibt 12,50, `1.5` ergibt 1,50, Leerzeichen zählen nicht.
- [ ] **[rein]** Ungültig (`None`): leer, `abc`, `+`, `5+`, `2**3`, `5*-2`, `1,2,3`, `/0`, `5/0`, `20 €`, ein Ergebnis über 1.000.000,00 im Betrag. Es wirft nie, auch nicht bei einer Eingabe, die `decimal` überliefe.
- [ ] **[rein]** Gerechnet wird mit `decimal`, das Ergebnis liegt auf dem Cent, kaufmännisch gerundet: `100/3` ergibt 33,33, `0,05/2` ergibt 0,03, `0,1+0,2` ergibt genau 0,30. Der Test läuft unter .NET; die Rundung nutzt nur, was auch unter Fable gleich rechnet (`Math.Round` mit `MidpointRounding.AwayFromZero`, sonst handgeschrieben).
- [ ] **[rein]** Ein negatives Ergebnis ist im Feld Zugewiesen gültig: `0-20` ergibt -20, `-1000` mit Stand 900 ergibt -100.

### Feld Zugewiesen
- [ ] **[Client-update]** GIVEN Zugewiesen 900 WHEN `+20` steht im Feld THEN Vorschau: Verfügbar danach alt plus 20, Ergebnis 920,00 und Zu verteilen danach alt minus 20. Bei `+0` oder `900` steht keine Vorschau.
- [ ] **[Client-update]** GIVEN Zu verteilen 10 WHEN `+20` THEN Zu verteilen danach -10, nichts sperrt, gespeichert wird beim Verlassen.
- [ ] **[Client-update]** GIVEN negatives Zugewiesen -50 WHEN Schritt +10 THEN Wert -40, das Feld zeigt `-50,00` und `-40,00` mit Vorzeichen, und der Wert wird nicht als Rechnung relativ zum Stand gelesen (Regression gegen `formatAmountForInput`, das `abs` nimmt, und gegen `-40,00` als Rechnung).
- [ ] **[Client-update]** GIVEN `abc` WHEN Vorschau THEN "Keine gültige Rechnung". Enter oder Fokus weg: nichts geschrieben, das Feld behält den Text und zeigt ihn rot.
- [ ] **[Client-update]** GIVEN ungültiger Text WHEN Tab-, Kategorie-, Monats- oder Moduswechsel oder Schließen THEN nichts geschrieben, Eingabe verworfen, Warn-Toast "Zugewiesen nicht gespeichert: keine gültige Rechnung".
- [ ] **[Browser]** Beim Fokus ist der Inhalt des Felds markiert. Die Farben der Vorschau: Verfügbar danach rot unter 0, grün bei genau 0 **[human-eye]**.

### Speichern beim Verlassen
- [ ] **[Client-update]** Tabellentest: Enter, Fokus weg, Tab-Wechsel per Klick, Tab-Wechsel per Pfeiltaste, `˄ ˅`, Zeile wählen, Pfeiltaste Hoch und Runter, Monat blättern, Monatsname, Sheet schließen, Moduswechsel, Seite verlassen: je genau eine Zuweisung in der Schlange, mit Kategorie und Monat der Eingabe. Beim Monatswechsel trägt die Zuweisung den alten Monat.
- [ ] **[Client-update]** Fokus weg gefolgt vom Klick, der ihn auslöste (Blur, dann Tab-Wechsel), ergibt genau eine Zuweisung.
- [ ] **[Client-update]** Der Modus ist ein Fall von Verlassen: Zuweisen nach Holen oder Abgeben speichert die Eingabe. Holen nach Zuweisen schreibt nichts, Betrag und Gegenseite stehen wieder da, wenn der Modus zurückkehrt, bis Kategorie oder Monat wechseln.
- [ ] **[Client-update]** Zwei Schritte +50, +50, dann weiter: eine Zuweisung, eine Budget-Bewegung, Zielwert alt plus 100.
- [ ] **[Client-update]** Verlassen ohne Änderung schreibt nichts: ohne Eingabe, bei gleichem Wert (`900,00`, `+0`, `900`), und nach Schritten +10, -10.
- [ ] **[Client-update]** Esc verwirft: das Feld zeigt den Stand, nichts in der Schlange, der folgende Blur schreibt nichts. Enter und Esc nehmen dem Feld den Fokus, die Pfeiltasten gehen danach am PC wieder (`pageKeyAction`).
- [ ] **[Client-update]** Der gespeicherte Wert steht sofort im Modell: Zugewiesen, Verfügbar und Zu verteilen des Monats sind fortgeschrieben, die Generation des Monats steigt, eine ältere Monatsantwort überschreibt ihn nicht. Nach der letzten Antwort aus der Schlange wird der Monat einmal neu gelesen.
- [ ] **[Client-update]** Schlange: Es läuft eine Zuweisung, weitere warten in Reihenfolge, eine wartende für dieselbe Kategorie und denselben Monat wird ersetzt (keine laufende). Holen, Abgeben und der Fehlbetrag-Knopf sind gesperrt, solange die Schlange nicht leer ist.
- [ ] **[Client-update]** Antwort `Error` (Monat nicht lesbar, abgelehnt): Toast nennt den Grund, die Schlange läuft weiter, der Monat wird neu gelesen und zeigt den echten Stand. Aufruf abgebrochen: Toast "Ob zugewiesen wurde, ist unklar", Monat neu lesen. Bericht ohne Zeilen: kein Toast. Erfolg: kein Toast, die Zahl in der Liste ist die Antwort.
- [ ] **[Client-update]** Während `monthChanging` nimmt das Feld keine Eingabe an, Schritte und Knöpfe tun nichts (`readOnly`, der Fokus bleibt).
- [ ] **[Client-update]** Fällt die gewählte Kategorie beim Neu-Lesen weg, ist die Eingabe verworfen und ein Warn-Toast nennt das.
- [ ] **[Client-update]** Eine Zuweisung im letzten Monat des Fensters zieht den Rahmen still nach (`frameDueAfterReport`), wie bei Verschiebungen.
- [ ] **[Client-update]** `isInputActive` ist wahr bei Eingabe im Feld, Betrag oder Gegenseite in Holen und Abgeben, laufendem Aufruf und nicht leerer Schlange; falsch im Ruhezustand und bei bloßer Vorbelegung (`src/Tests/PwaTests.fs`).
- [ ] **[Browser]** Am Fenster: Der Wechsel in ein anderes Programm speichert nicht (`document.activeElement` bleibt das Feld).

### Befehl Zuweisen
- [ ] **[rein]** `planAssign`: neu gleich gelesenes Zugewiesen ergibt keine Zeile; sonst eine Zeile mit gelesenem Alt-Wert, Herkunft Zuweisen. Abgelehnt mit gesammelten Gründen: Kategorie fehlt im Monat, Betrag neben dem 10-Milliunits-Raster. Negatives neues Zugewiesen ist gültig.
- [ ] **[Handler]** `assignMoney`: Monat frisch lesen, Bewegung vor dem PATCH festhalten, genau ein PATCH mit dem absoluten Wert, Ergebnis angehängt. Fälle wie die Vorlage `moveMoney` (Regel 4 der Doktrin): Monat nicht lesbar (außen `Error`, nichts festgehalten), Monat nicht offen (404), Ablehnung (nichts festgehalten), PATCH scheitert (Zeile fehlgeschlagen, Grund im Bericht), 429, Sperre je Monat hält eine zweite Zuweisung und eine Verschiebung an (Vorbild `MoveMoneyTests.fs:446`).
- [ ] **[Handler]** Steht YNAB anders als der Client sah: Der Wert gilt, die Bewegung nennt YNABs Stand als alt (ADR 0078 Punkt 2). Steht YNAB schon auf dem Wert: keine Bewegung, kein PATCH, Bericht ohne Zeilen.
- [ ] **[Handler]** Fehlgeschlagene Zuweisung über `retryFailed`: gesendet, schon am Ziel (geschrieben), YNAB gewinnt (in YNAB geändert). Eine Zeile, keine Reihenfolge-Regel.
- [ ] **[Handler]** `validateAssignRequest` sammelt alle Fehler: Monat, Währung, leere Kategorie, Betrag über 1.000.000,00 im Betrag. Vorbild `validateMoveRequest`. Zeile in `src/Tests/RemotingEndpointNegativeTests.fs` je neuem Endpoint.
- [ ] **[Handler]** `assignMoneyWith` hinter Ports: Die Live-Stelle in `src/Server/Api.fs` ist nach Regel 3 der Doktrin aufgelistet und belegt, fehlendes Token und fehlendes Budget geben deutsche Fehler.

### Fehlbetrag-Knopf
- [ ] **[Client-update]** GIVEN Verfügbar -12,30, kein Target WHEN Modus Zuweisen THEN Knopf "12,30 zuweisen", Auftrag `moveMoney` von Zu verteilen an X über 12,30, Herkunft Zuweisen.
- [ ] **[Client-update]** Größeres von negativem Verfügbar und Fehlbetrag: Verfügbar -5, Fehlbetrag 20 ergibt "20,00 zuweisen".
- [ ] **[Client-update]** Pausiertes Target: Fehlbetrag zählt nicht. Verfügbar 10 und pausiert: kein Knopf. Verfügbar -5, Fehlbetrag 20 und pausiert: "5,00 zuweisen". Nichts fehlt: kein Knopf.
- [ ] **[Client-update]** Zu verteilen 60,20 bei fehlt 80: Knopf "Nur noch 60,20 zu verteilen", gesperrt, ohne Auftrag. Zu verteilen genau gleich fehlt: Knopf wirkt. Zu verteilen negativ: "Nur noch 0,00 zu verteilen".
- [ ] **[Client-update]** Offene Eingabe plus Knopf: Die Eingabe wird verworfen, genau ein Auftrag geht hinaus, zwei Tipps ergeben einen (Sperre wie `confirmMove`).
- [ ] **[human-eye]** Der wirksame Knopf ist orange, "Nur noch ..." neutral.

### Modi
- [ ] **[Client-update]** Startmodus je Kategorie und Monat: Verfügbar im Minus startet auf Holen mit Betrag gleich fehlendem Minus, sonst Zuweisen mit leerem Betrag. Gilt beim Wählen, bei `˄ ˅`, Pfeiltasten und nach dem Monatswechsel. Reload bei gleicher Kategorie und gleichem Monat ändert den Modus nicht, auch wenn die Kategorie dadurch nicht mehr rot ist.
- [ ] **[Client-update]** Rot und Vorbelegung lesen den aktuellen Monat: Ändert ein Reload das Minus und wurde nichts getippt, folgt der Betrag. Getippter Betrag bleibt.
- [ ] **[Browser]** Sheet ist in allen drei Modi gleich hoch (390 mal 844, gemessen). Modus Zuweisen zeigt ohne Scrollen Feld, sechs Schritte und Knopf.

### Holen und Abgeben
- [ ] **[Client-update]** Holen in X aus Y ergibt Quelle Y, Ziel X. Abgeben von X an Y ergibt Quelle X, Ziel Y. Mit Zu verteilen als Gegenseite eine Zeile mit Herkunft Zuweisen.
- [ ] **[Client-update]** `von` und `nach` zeigen je den Stand danach, die Gegenseite Zu verteilen zeigt Zu verteilen danach. Ohne gültigen Betrag stehen die heutigen Stände. Negativ ist gültig.
- [ ] **[Client-update]** Knopftext: "20,00 € aus Kleidung holen", "20,00 € an Kleidung abgeben", "20,00 € aus Zu verteilen holen". Gesperrt ohne Gegenseite, ohne gültigen Betrag, bei Betrag 0 oder negativ, während eines Aufrufs. Keine Gegenseite ist vorbelegt (Entscheidung 2).
- [ ] **[Client-update]** Das Betragsfeld rechnet mit `evaluateAmount`, der Stand ist die Vorbelegung, sonst 0: Vorbelegt 12,30 und `*2` ergibt 24,60. Rechnung und Ergebnis über Cent wie oben.
- [ ] **[rein]** `freeAmount`: Verfügbar minus Fehlbetrag. Pausiertes Target und Target ohne Fehlbetrag: Verfügbar. Ohne Target: Verfügbar.
- [ ] **[Client-update]** Warnung: Betrag über dem freien Betrag der Quelle zeigt "Kleidung hat 30,00 € frei", in Holen und Abgeben, ohne Sperre. Betrag gleich frei: keine Warnung. Quelle Zu verteilen: keine Warnung. Der Stand der Quelle ist rot, sobald er unter 0 fällt; negativer freier Betrag erscheint als 0,00 €.
- [ ] **[Client-update]** Nach Erfolg: Modus bleibt, Betrag und Gegenseite sind zurückgesetzt, Toast aus `moveSummaryText`. `Error`: Grund steht im Tab, Eingaben bleiben. Aufruf abgebrochen: Formular zurückgesetzt, Toast, Monat neu lesen (kein zweiter Versuch im Formular, der Server rechnet relativ).
- [ ] **[Client-update]** Solange ein Aufruf läuft, wird der Inspektor nicht verlassen: `˄ ˅`, Zeile wählen, Pfeiltasten, Monat, Tab, Modus und Schließen tun nichts. Doppeltipp ergibt einen Auftrag.
- [ ] **[Client-update]** Esc im Betragsfeld setzt auf die Vorbelegung zurück, die Gegenseite bleibt. Enter nimmt nur den Fokus, gesendet wird allein über den Knopf.
- [ ] **[Client-update]** Während `monthChanging` ist der Knopf gesperrt, kein Auftrag geht in den alten Monat.
- [ ] **[Client-update]** Der Monat des Auftrags ist `BudgetMonth.Month` wie heute; Holen und Abgeben gehen in jeder Monatsart.
- [ ] **[Browser]** Lange Kategorienamen (Fixture mit 60 Zeichen) erscheinen in `von`, `nach` und im Knopf vollständig: `scrollWidth <= clientWidth`, Text bricht um. In 390 und im Panel.

### Gegenseite und Zuletzt verwendet
- [ ] **[Handler]** `recentMoveCategories`: Kategorien aus Bewegungen mit Herkunft Verschieben, neueste zuerst, ohne Dubletten, höchstens zehn, auch aus anderen Monaten. Herkunft Targets anwenden und Zuweisen zählen nicht. Leere Tabelle: leere Liste. Keine YNAB-Anfrage.
- [ ] **[Client-update]** Picker: Zuerst "Zu verteilen" mit Betrag, dann "Zuletzt verwendet", dann alle; X fehlt überall, eine ausgeblendete oder unbekannte Kategorie fällt aus den zuletzt verwendeten weg, höchstens fünf (`CategoryPicker.recentCategories`).
- [ ] **[Client-update]** Scheitert die Abfrage, öffnet der Picker ohne den Abschnitt, ohne Fehlermeldung. Beim Öffnen wird neu gelesen.
- [ ] **[Client-update]** Offener Picker sperrt die Pfeiltasten der Seite (`pageKeyActionFor`).
- [ ] **[Browser]** Picker geht am Handy als `sheetLayered` über dem Inspektor auf, schließt ohne Ghost-Click, das Inspektor-Sheet bleibt offen. Im Panel 1280 mal 800 ist das Popover ganz sichtbar und wird vom Panel nicht abgeschnitten.

### Offene Verschiebung
- [ ] **[Client-update]** Teilfehler bei Holen und Abgeben: Hinweis steht wie bisher, "Erneut senden" geht, nach Erfolg weg. Der Hinweis bleibt bei Tab-, Kategorie-, Monats- und Moduswechsel stehen und nennt den Monat.
- [ ] **[Client-update]** Fehlgeschlagene Zuweisung und fehlgeschlagener Fehlbetrag-Knopf erscheinen im selben Hinweis, Text "Nicht zugewiesen", mit "Erneut senden". Der Toast nennt dasselbe. Eine Verschiebung bleibt "Nicht vollständig verschoben".
- [ ] **[Browser]** Am Handy steht der Hinweis im festen Bereich des Inspektor-Sheets, das Sheet bleibt nach Holen offen (bisher trat es hinter dem Dialog zurück).

### Tastatur, Fokus, Handy
- [ ] **[Client-update]** `pageKeyActionFor` mit Fokus im Feld Zugewiesen und im Betragsfeld: alle vier Pfeiltasten `LeaveKey`, Cursor bleibt im Feld. Auf dem Umschalter wechseln Links und Rechts die Tabs des Inspektors.
- [ ] **[Browser]** Handy, Hauptablauf: Zeile wählen, `+20` tippen, `˄ ˅`, die Antwort von `assignMoney` trägt den alten Monat und den Wert (Happy Path per `page.route`).
- [ ] **[Browser]** Handy mit offener Tastatur (`--vvh` gesetzt): Sheet schrumpft, Feld und Verfügbar danach liegen im sichtbaren Bereich.
- [ ] **[Browser]** Ein Tipp auf einen Schritt oder den Fehlbetrag-Knopf lässt `document.activeElement` auf dem Feld (ADR 0005). Sechs Schritte, Umschalter, Knöpfe und Felder messen mindestens 48 px hoch, die Schritte passen in 360 px Breite. Feldschrift 16 px.
- [ ] **[Browser]** PC, Panel: Feld tippen, Enter, Pfeil runter wählt die nächste Zeile. Keine Schritte im Panel.
- [ ] **[Browser]** Je ein Happy Path Holen und Abgeben; `scripts/move-money.test.mjs`, `scripts/budget-page.test.mjs` und `scripts/category-detail.test.mjs` sind auf den Tab umgestellt, Selektoren des Dialogs sind fort.
- [ ] **[human-eye]** Sheet folgt dem Schrumpfen ohne Sprung (`height` in beiden Transition-Blöcken von `.bottom-sheet`, `src/Client/styles.css:1683` und `:1698`). Aufbau gegen Variante D und E der Zeichenfläche, Rollen der Farben gegen ADR 0075 und 0077.

### Abschluss
- [ ] Neue DS-Bausteine stehen in `/styleguide` und `standards/frontend/styleguide.md`.
- [ ] README `budgeting` ist nach dem Block in den Notes fortgeschrieben, Delta gegen `npm run check:readme-delta` geprüft.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Innere Form

**Reihenfolge der Commits.** Zuerst Aufteilen ohne Verhaltensänderung, dann der Bau (siehe unten).

**Neue Begriffe im Code.** Die Eingabe im Feld heißt `AssignEntry`, nie Draft oder Entwurf: **Entwurf** ist in der README schon eine Reihe von Verschiebungen.

```fsharp
type MoneyMode = AssignMode | TakeMode | GiveMode

/// Getippt oder per Schritt entstanden. `Exact` schützt negative Werte davor, als Rechnung zu gelten.
type AssignText = Typed of string | Exact of decimal

/// Die Zuweisen-Eingabe. Hält Kategorie und Monat selbst: Sie wird oft erst gespeichert, wenn beide wechseln.
type AssignEntry = { CategoryId: Guid; Month: string; BaseAssigned: decimal; Text: AssignText }

/// Ersetzt `MoveForm`. X kommt aus `MoneyTabState`, die Richtung aus `Mode`.
type TransferForm = { Counterpart: MoveSide option; AmountInput: string option; PickerOpen: bool; Submission: Submission }

/// Entwurfszustand des Tabs Geld, Schlüssel Kategorie und Monat.
type MoneyTabState = { CategoryId: Guid; Month: string; Mode: MoneyMode; Entry: AssignEntry option; Transfer: TransferForm }

type AssignWriteState = WriteWaiting | WriteRunning

type AssignWrite = { CategoryId: Guid; Month: string; Assigned: Money; State: AssignWriteState }
```

**Modell.** `Model.Move` fällt weg. Neu: `TabMoney: MoneyTabState option`, `AssignQueue: AssignWrite list`, `RecentCounterparts: RemoteData<Guid list>`. Ein späterer Tab Target legt sein `TabTarget` daneben. `OpenRetries` bleibt auf der Seite: Der Hinweis gehört zu keiner Kategorie. Die Lage von `moveRetries` in der View bleibt, die Fallunterscheidung über `sheetCategory` wird einfacher, weil das Sheet nicht mehr zurücktritt; der Zweig `model.Move` fällt aus `sheetCategory`.

**Abgleich des Tab-Zustands.** Reine Funktion `syncMoneyTab`, einmal am Ende von `update`. Keine Auswahl: `None`. Während `monthChanging`: unverändert. Schlüssel (Auswahl, geladener Monat) anders: frisch mit Startmodus. Gleicher Schlüssel: unverändert. Damit ersetzt sie `openMove` und die `Move`-Regel in `applyLoaded`, und `selectCategory` und `pageKeyActionFor` verlieren die Bedingung auf `Move`.

**Zwei Übergänge.**
- `saveEntry`: Feld verlassen (Enter, Fokus weg). Gültig und geändert: in die Schlange, Monat fortschreiben. Gleich: Eingabe weg. Ungültig: Eingabe bleibt rot.
- `leaveInspector: Model -> Model * ExternalMsg`: wie `saveEntry`, nur fällt eine ungültige Eingabe weg, mit Warn-Toast. Holen und Abgeben bleiben unberührt, ihre Form stirbt mit dem Schlüssel.

Vor der Änderung rufen `leaveInspector`: `SelectMonth`, `SelectCategory`, `StepCategory`, `PageKeyPressed` außer `LeaveKey`, `SelectInspectorTab`, `SwitchMoneyMode`, `CloseInspector`, `ReturnAbandoned` und `PageLeft`. Der gemeinsame Einstieg ist ein Wrapper in `State.fs`, die reinen Funktionen `select`, `selectCategory`, `stepSelection` bleiben klein. `inspectorBusy` (Submission `Sending`) lässt alle diese Wege nichts tun.

`leaveForEntry` und `enterPage` brauchen keinen Aufruf: Eine Eingabe gibt es nur im Tab Geld, jeder Tab-Wechsel räumt sie, und `PageLeft` räumt beim Seitenwechsel. `enterPage` setzt `TabMoney` und, wie bisher `Move`, den Formularzustand zurück. `PageLeft` ist neu: `urlChangedCmd` in `src/Client/State.fs:291` bekommt den Fall `Budget, _`, ohne ihn ginge die Eingabe beim Rücksprung per Browser verloren. Einen Blur löst das Abbauen eines Felds in React nicht aus.

**Senden.** `flushAssign` in `State.fs` startet den Kopf der Schlange, wenn keiner läuft. Nachrichten `AssignAnswered of AssignWrite * Result<ApplyReport, string>` und `AssignCallLost`. Schreibende Aufrufe laufen nie über `Retry`. Nach der letzten Antwort `reloadCmd`.

**Fokus in der View.** `onBlur` meldet nur, wenn `document.activeElement` nicht mehr das Feld ist. `onKeyDown` Enter und Esc dispatchen und rufen danach `blur()`, `e.stopPropagation()` bei Esc. Das Feld trägt alles selbst, `BottomSheet` und Panel brauchen keine Esc-Behandlung; sie haben keine, die dazwischenfunkt. Schritte und Fehlbetrag-Knopf: `onMouseDown` mit `preventDefault` (ADR 0005 Punkt 2), Commit auf `click`. Der Umschalter darf den Fokus nehmen, das ist ein Verlassen.

**Tastatur.** `keyFocus` erkennt beide Felder schon als `TextField`, `pageKeyAction` bleibt. `pageKeyActionFor` prüft statt `model.Move` den offenen Picker.

**`isInputActive`.** Neue reine Funktion `Components.Budget.Types.inputActive`, in `src/Client/State.fs:385` mit `||` angehängt.

**Befehl Zuweisen (ADR 0078).** Shared: `AssignRequest = { CategoryId: Guid; Assigned: Money }`, `BudgetApi.assignMoney: string -> AssignRequest -> Async<Result<ApplyReport, string>>`. Domain: `planAssign (month: BudgetMonth) (request) : Result<MovePlan option, string>`, Herkunft `MoneyAssigned`. Handler in `CommandHandlers.fs` unter `MonthGuard.exclusive`, Form wie `moveMoney`, gesendet über `sendInOrder` mit der einen Zeile, ohne Neu-Lesen von Zu verteilen. `Validation.validateAssignRequest`, Grenze `MaxMoveAmount`. `Api.assignMoneyWith` wie `moveMoneyWith`.

**Fehlbetrag-Knopf.** Baut einen `MoveRequest` mit `ToBeBudgetedSide` und läuft über `moveCmd` und `MoveAnswered`. Eine eigene Absolut-Logik braucht er nicht.

**Abfrage Zuletzt verwendet.** `BudgetApi.getRecentMoveCategories: unit -> Async<Result<Guid list, string>>`. Query in `Server/Budgeting/QueryHandlers.fs`, SQL in `Budgeting.Persistence.loadRecentMoveCategories`:

```sql
SELECT category_id FROM budget_movements WHERE origin = @origin
GROUP BY category_id ORDER BY MAX(rowid) DESC LIMIT @limit
```

`@origin` aus `originText MoneyMoved`, Limit 10, weil X und ausgeblendete Kategorien noch herausfallen. Die Liste gilt für alle Kategorien gemeinsam: Die Historie ist kurz, eine Liste je X bliebe meist leer. Keine Fortschreibung im Client, gelesen wird bei `OpenPicker` (lokal, kostet kein YNAB-Limit). Ablauf über `Retry.reading`. Das Ergebnis geht als `Recent` an den Picker. Ein unter `page.route` nicht gemockter Endpoint bricht Fixtures, sie ziehen im selben Schritt mit. Kein Pflichtfeld in `BudgetMonth`.

**Rechnen.** `evaluateAmount (baseValue: decimal) (text: string) : decimal option` in eigener Datei `Components/Budget/AmountMath.fs`, ohne Elmish und ohne Feliz. Nicht auf `parseAmountInput` aufbauen: das lehnt Vorzeichen ab und kennt keine Tausenderpunkte. Punkt-Regel wie im Prototyp: Ein Punkt vor genau drei Ziffern ist Tausenderpunkt, sonst Dezimaltrenner. Für das Feld mit Vorzeichen ein eigener Formatierer, `formatAmountForInput` nimmt `abs`.

**Frei.** `freeAmount` in `src/Shared/Budgeting.fs` neben `missingAmount`.

**Design-System.** `Input.text` kennt weder `onBlur`, `onFocus`, `onKeyDown` noch `readOnly`: optionale Felder in `TextInputProps`, Vorgaben in `textInputDefaults`. Der Umschalter ist ein neuer Baustein `Segmented`: `Tabs.bar` nimmt die Pfeiltasten, und zwei Tab-Leisten übereinander stören. Buttons mit `aria-pressed`, 48 px, aktiv teal (ADR 0075). Im Panel `CategoryPicker.popoverInPanel`; das Panel schneidet per `overflow-hidden` und `overflow-y-auto` ab, das prüft der Browser-Test zuerst. Der Auslöser von `von` und `nach` braucht den `onMouseDown`-Stopper aus `moveFields`.

**Aufbau im Tab.** Umschalter oben, darunter die Kennzahlen Aktivität und Verfügbar (Zugewiesen ist das Feld), dann der Modus. Werte aus dem Styleguide, Aufbau aus der Zeichenfläche.

**Sammelzweige.** `updatePage` gibt ein Tripel mit `ExternalMsg` zurück und `update` wird ein einziges `match` ohne `_` (Aufteil-Commit). Sonst schluckten die Zweige `State.fs:162` und `:206` die neuen Nachrichten still.

## Aufteilen von `Types.fs` und `View.fs`
Gehört in diese Task, als erster Commit ohne Verhaltensänderung mit eigenem `VERIFY: PASS`. Vorgelagert wäre es eine zweite Task ohne eigenen Nutzen, und die Verschieben-Teile werden hier ohnehin umgebaut. `Types.fs` hat 1028 Zeilen, `View.fs` 1228.

Schnitt: `Types.fs` behält Typen, `Msg`, `init` und `ExternalMsg`. Danach `Month.fs`, `Targets.fs`, `Moves.fs`, `Inspector.fs`, in dieser Folge in `src/Client/Client.fsproj`. Die View entsprechend mit `View.fs` als Seite am Ende. Auch die Tests ziehen ihre `open` um. `budgeting-r7t3k` berührt dieselben Stellen: Wer zuerst läuft, gewinnt, die andere Task rebased.

Geteilt wird die Task nicht. Der Rückbau des Dialogs, das Feld und `leaveInspector` hängen am selben Tab-Zustand; ein Zwischenstand hätte zwei Wege für dasselbe.

## Notes

**Entscheidungen von Roman (2026-11-02).**
1. **Target pausieren:** Der Knopf kommt mit budgeting-t6p8z in den Modus Zuweisen. Hier bleibt nur der Platz frei. Pausen aus YNAB wirken schon.
2. **Gegenseite:** Keine Vorbelegung. Der Knopf ist gesperrt, bis eine Gegenseite gewählt ist.
3. **Überschreiben einer YNAB-Änderung:** Angenommen, ADR 0078 ist `accepted`. Das Feld setzt den Wert ohne Vergleich, die Bewegung hält die parallele Änderung fest.
4. **Wortlaut:** "Zuletzt verwendet", der vorhandene Text des Pickers.

**Hier entschieden, ohne Rückfrage nötig.**
- Der Modus-Wechsel weg von Zuweisen speichert. Das folgt der Konzeptregel: Das Feld ist danach weg, und bei Fokus im Feld fiele der Blur ohnehin.
- Die Warnung gilt für die Quelle in Holen und Abgeben, das Konzept nennt nur Holen. Rot folgt dem Vorzeichen des Stands, ADR 0075 hält Rot für Beträge im Minus.
- Zuweisen im Feld zeigt keinen Erfolgs-Toast, bei jedem Weiterblättern wäre er Lärm. Holen, Abgeben und Fehlbetrag-Knopf behalten ihn.
- Ein Fehler vor dem Festhalten kostet die Eingabe, ein Toast nennt den Grund. Der Wert lässt sich neu tippen, weil Zuweisen absolut ist.
- Ein Fensterwechsel zählt nicht als Fokus weg, sonst schriebe jeder Blick in einen Rechner.

**Rate-Limit.** Eine gespeicherte Zuweisung kostet drei Anfragen, bei rund 200 je Stunde unkritisch; die Schlange liest den Monat erst am Ende neu.

**README `budgeting`, fortzuschreiben.**
- Neu **Modus** (budgeting-c5w2n): die drei Ansichten des Tabs Geld, Zuweisen, Holen und Abgeben. Standard Zuweisen, eine rote Kategorie startet auf Holen mit dem Minus als Betrag. Der Modus beginnt je Kategorie und Monat neu.
- Neu **Holen** und **Abgeben**: Verschieben aus Sicht der gewählten Kategorie. Holen: Geld kommt von der Gegenseite, Abgeben: es geht an sie.
- Neu **Zuweisen-Eingabe**: der noch nicht gespeicherte Wert im Feld Zugewiesen, mit Kategorie und Monat. Nicht zu verwechseln mit **Entwurf**.
- Neu **Verlassen**: Enter, Fokus weg, Tab-, Kategorie-, Monats- oder Moduswechsel, Schließen, Seitenwechsel. Speichert die Zuweisen-Eingabe, Esc verwirft sie.
- Neu **Betragsrechnung**: Regeln des Felds, `+20` relativ zum Stand, `900+40` ersetzt ihn, Punkt vor Strich, auf den Cent.
- Neu **Frei**: Verfügbar minus offener Fehlbetrag, pausierte Targets sperren nichts. Grundlage der Warnung beim Überziehen.
- Neu **Zuletzt verwendet**: Kategorien aus den Bewegungen mit Herkunft Verschieben, neueste zuerst.
- Ändern **Zuweisen**: Das Feld setzt das Zugewiesen absolut (ADR 0078), der Fehlbetrag-Knopf und Zu verteilen als Gegenseite bleiben relativ.
- Ändern **Gegenseite**: gehört zum Modus, die Richtung ist die Wahl von Holen oder Abgeben.
- Ändern **Budget-Seite**, **Kategorie-Detail** ("Knopf Verschieben"), **Offene Verschiebung** (auch fehlgeschlagene Zuweisung), **Monatswechsel**.
- **Aggregates:** neu **Zuweisung**: Monat und eine Zeile, Invarianten Kategorie sichtbar im Monat, Betrag auf 10 Milliunits, negatives Zugewiesen gültig, keine Zeile bei gleichem Wert.
- **Key commands:** neu **Zuweisen** (ADR 0078). **Key events:** **GeldZugewiesen** entsteht nun auch hier.

## Hinweise aus dem Review von budgeting-g8s3w
Eingearbeitet: `Model.Move` (Innere Form, Modell), `leaveInspector` (Zwei Übergänge), `isInputActive`, Sammelzweige, Aufteilen, `height` in der Transition (Kriterium unter Tastatur, Fokus, Handy; die Zeilen stehen heute bei `styles.css:1683` und `:1698`).
