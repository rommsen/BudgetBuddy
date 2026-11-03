---
id: budgeting-c5w2n
title: "Tab Geld: Umschalter Zuweisen, Holen, Abgeben, rechnendes Feld mit Speichern beim Verlassen, Fehlbetrag-Knopf, Handy-Schritte, Holen und Abgeben nach Variante D"
status: done
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

## Verifier-Note Iteration 1

FAIL. `./verify.sh` war grün, die Gründe liegen im Verhalten und in der Abdeckung:

- ADR 0078 Punkt 5: während des Fehlbetrag-Aufrufs ist das Feld Zugewiesen bedienbar. `editEntry` und `saveEntry` prüfen `inspectorBusy` nicht. Stand 900, Knopf +12,30, Schritt +10 sendet 910 statt 922,30.
- "Rahmen still nachziehen": der Test geht nicht durch `AssignAnswered`, `AssignCallLost` ist ungedeckt.
- Esc im Feld Zugewiesen, Enter und Esc im Betragsfeld haben keinen Test der Verdrahtung.
- Regel 3: die Live-Stellen von `assignMoneyWith` fehlen im Outcome.

## Verifier-Note Iteration 2

FAIL. `./verify.sh` war grün, die Punkte 1 bis 3 der ersten Note sind behoben.

- Regel 3: `Api.fs:1913` (`livePort`, `liveFramePort`) und `Api.fs:1917` (`getSetting`) sind nicht belegt. Beleg per Strukturwächter in `apiWirings` (`StructureGuardTests.fs:635`), ohne Netz.
- ADR 0078 Punkt 5 auf dem Weg "Erneut senden": `startMoveRetry` prüft die Schlange nicht, `inspectorBusy` kennt `RetryRunning` nicht, `applyMoveRetried` trägt den Bericht nicht ein. Fehlbetrag +12,30 scheitert bei 900, "Erneut senden" läuft, Schritt +10 reiht 910 ein und nimmt 912,30 zurück.
- Nach `MoveCallLost` ist das Feld bis zum Neu-Lesen frei und rechnet vom alten Stand.

## Verifier-Note Iteration 3

FAIL. `./verify.sh` war grün, die beiden früheren Noten sind behoben, `apiWirings` greift gegen die echte `Api.fs`.

- ADR 0078 Punkt 5 mit offener Eingabe: am Handy setzt ein Schritt ohne Fokus `Entry = Exact 910`, ohne Blur und ohne Schlange. "Erneut senden" startet, YNAB steht auf 912,30, beim Verlassen gehen 910 hinaus. `startMoveRetry` prüft `tab.Entry` nicht, `confirmTargets` und `retryFailed` ebenso.
- Stiller Klick: `ConfirmTargets`, `RetryFailed`, `RetryMove` tun bei nicht leerer Schlange nichts, die Knöpfe bleiben aktiv.

Vierte Iteration über die Grenze von drei hinaus, entschieden vom Conductor, weil der Owner selbständiges Arbeiten verlangt hat und nichts gemergt ist. Im Bericht an Roman ausgewiesen.

## Verifier-Note Iteration 4

FAIL. `./verify.sh` war grün, innerhalb der Seite ist die Regel dicht.

- Stiller Klick auf dem Toast "Erneut senden": `ToastActionClicked` schließt den Toast, `startMove` tut bei laufender Schlange nichts und meldet nichts.
- Umgehung über den Seitenwechsel: `PageEntered` läuft während eines Aufrufs durch, `enterPage` setzt `TabMoney = None`, damit fällt `moveRunning` auf falsch, obwohl der Aufruf läuft.

## Outcome

Der Tab Geld steht. `./verify.sh`: `VERIFY: PASS` (dotnet 3425 grün, 10 opt-in übersprungen, node 471 grün). `npm run check:readme-delta -- --bc budgeting` Exit 0. Offen sind nur die `[human-eye]`-Punkte für Roman: Farben der Vorschau und des Knopfs, Schrumpfen ohne Sprung, Aufbau gegen Variante D und E. Screenshots gibt es keine. Kein Live-Aufruf gegen YNAB, `RUN_INTEGRATION_TESTS` nicht gesetzt.

**Wichtige Stellen:**
- Rechnen: `src/Client/Components/Budget/AmountMath.fs` (`evaluateAmount`, `formatSignedAmount`).
- Zustand: `Components/Budget/Moves.fs` (`syncMoneyTab`, `saveEntry`, `leaveInspector`, `enqueueAssign`, `startNextAssign`, `missingAction`, `confirmMissing`, `transferEnds`, `transferWarning`, `confirmTransfer`, `inputActive`), `Components/Budget/State.fs` (`transition` ohne Sammelzweig, `leavesInspector`, `update` als Einstieg mit `flushAssign`).
- View: `Components/Budget/MovesView.fs`, `InspectorView.fs`.
- Server: `planAssign` in `Server/Budgeting/Domain.fs`, `assignMoney` und `recordAndSend` in `CommandHandlers.fs`, `queryRecentCategories` in `Persistence.fs`, `validateAssignRequest`, `Api.assignMoneyWith`. `freeAmount` in `Shared/Budgeting.fs`.
- Design-System: `Segmented.fs`, `Button.amountStep`, `Button.sideRow`, `Button.sideRowFixed`, `Money.afterAmount`, `CategoryPicker.popoverInPanelRevealed`, `Input.TextInputProps` mit `ReadOnly`, `OnFocus`, `OnBlur`, `OnKeyDown`. Styleguide §5 und `/styleguide` Sektion 23b.

**Aufteilen.** `Types.fs` und `View.fs` sind geteilt in `Month.fs`, `Targets.fs`, `Moves.fs`, `Inspector.fs` und `MonthView.fs`, `TargetsView.fs`, `MovesView.fs`, `InspectorView.fs`, `View.fs` als Seite. `Update` ist ein einziges `match`. Belegt waren `dotnet build`, 3339 .NET-Tests und `budget-page.test.mjs` 45 von 45 grün. Ein eigenes `VERIFY: PASS` für diesen Schritt allein gibt es nicht, die Task verlangte es, ein Worker kann aber keinen Zwischen-Commit setzen. Der Klassen-Wächter löst Bindungen nur je Datei auf, deshalb ein Eintrag für `{monthChangeClass model}` in `scripts/tailwind-class-guard-exceptions.json`. Der Strukturtest in `TargetsApplyClientTests.fs` liest jetzt alle `*View.fs`.

**Test zuerst, ehrlich:**
- Server, `freeAmount` und `evaluateAmount`: Test zuerst, mit Stummeln rot gesehen (28 von 30 Server-Tests, 8 von 10 Rechnen-Tests, 2 Frei-Tests).
- Client-Zustand, View und Browser-Wächter: nicht Test zuerst. Ich habe den Zustand am Stück gebaut, weil `Model.Move` und der Dialog auf einmal fallen mussten, und danach die Tests geschrieben. Das weicht von Vorgabe 3 ab. Rot ist dort nur nachträglich per Gegenprobe belegt, und nur für die Tabelle unten.

**Gegenprobe, je Mutante einzeln gesetzt, rot gesehen, zurückgenommen:**
| Mutante | Rot |
|---|---|
| `planAssign` ohne Gleichheit | 2, "Gleicher Wert ergibt keine Zeile" |
| Grenze einseitig | "Über 1.000.000,00 ... auf beiden Seiten" |
| Abfrage `origin <> @origin` | 4, "Herkunft Targets anwenden und Zuweisen zählen nicht" |
| Schlange ersetzt nie | "Eine wartende ... wird ersetzt" |
| ersetzt auch die laufende | dieselbe |
| `SelectMonth` verlässt nicht | 3, "Jeder Weg ergibt genau eine Zuweisung" |
| `SelectInspectorTab` verlässt nicht | 3, dieselbe |
| Sperre bei laufendem Aufruf weg | "Solange ein Aufruf läuft ..." |
| Schreiben ohne neue Generation | "Der gespeicherte Wert steht sofort im Monat ..." |
| `canSend` ohne Schlange | "Holen, Abgeben und der Fehlbetrag-Knopf sind gesperrt ..." |
| `canSend` ohne Monatswechsel | 2 |
| `Exact` über `formatAmountForInput` | "Bei negativem Zugewiesen behält das Feld das Vorzeichen ..." |
| Startmodus immer Zuweisen | 5 |
| Schlüssel ohne Monat | 2 |
| Holen dreht Quelle und Ziel | 6 |
| Warnung nach Verfügbar statt Frei | "Die Warnung nennt den freien Betrag ..." |
| Mal rechnet plus | 3 |
| Rest ohne Kappung bei 0 | "Reicht Zu verteilen nicht ..." |
| Browser: `focusLeft` immer wahr | "Der Wechsel in ein anderes Programm ..." |
| Browser: `popoverInPanel` ohne Einrücken ins Bild | "Panel 1280 × 800", 861 gegen 779 |
| Browser: `PageLeft` nicht gesendet | "Der Rücksprung per Browser ..." |
| Browser: Knopf ohne `preventDefault` | "Handy: ein Tipp auf Schritt ..." |
| Browser: Name `truncate` | "Lange Kategorienamen", beide Breiten. Überlebte zuerst, der Wächter maß nur die Hülle. Geschärft, dann rot |

**Über den Wortlaut hinaus und Abweichungen:**
- `inputActive` liegt in `Moves.fs`, nicht in `Types.fs`: es braucht `entryValue` nicht, aber der Rest der Regeln steht dort. `src/Client/State.fs` hängt es an.
- Alle Lese-Cmds der Seite liegen hinter einem Lambda. So läuft `update` unter .NET auf jedem Weg, die Cmds werden dort nie ausgeführt. Die Tabellentests gehen deshalb durch das echte `update` und nicht nur durch Einzelfunktionen.
- Die Vorbelegung gilt nur in Holen. Eine rote Kategorie zeigt in Abgeben ein leeres Betragsfeld.
- Ungültiger Text ist rot über die Textfarbe, ohne `Input.Error`: dessen `animate-shake` zuckte sonst bei jedem Zwischenstand wie `+` oder `5+`.
- Ein Knopf ohne Gegenseite oder Betrag heißt schlicht "Holen" oder "Abgeben".
- Eine Fehlerantwort auf den Fehlbetrag-Knopf steht im Tab in jedem Modus (`transfer-error`).
- Zwischen 768 und 1023 px gilt das Sheet: Schritte da, Picker als `sheetLayered`.
- `popoverInPanel` war im Panel abgeschnitten, deshalb neu `popoverInPanelRevealed`.
- Die Wächter für `--vvh` setzen jetzt `page.setViewportSize`: `Viewport` schreibt den Wert nach rund 300 ms zurück, das fiel erst mit dem Übergang der Höhe auf. Eine echte Tastatur ändert den Viewport und ist davon nicht betroffen.
- Die Sperre nach dem Picker in `move-money.test.mjs` wartet nicht mehr 400 ms: ein Klick ohne Wirkung nimmt sie weg.
- `scripts/export-design-system.mjs` führt die neue Karte `components-segmented`.

**Bekannte Grenzen:**
- Ein Zugewiesen von YNAB neben dem Cent bleibt per Schritt neben dem Cent, der Server lehnt mit Toast ab.
- Die Rechnung einer Eingabe gilt zum Stand beim ersten Tippen. Ein Reload ändert ihn nicht.
- Eine Warteschlange lebt im Client, ein Neuladen der App sperrt der PWA-Hinweis nur über `isInputActive`.
- Der Wächter für das andere Programm löst `focusout` von Hand aus, ein echter Fensterwechsel ist so nicht gemessen.

## Outcome (Ergänzung Iteration 2)

`./verify.sh`: `VERIFY: PASS` (dotnet 3439 grün, node 471 grün). Kein Live-Aufruf gegen YNAB. Sprache unverändert, deshalb kein neues README-Delta; es gilt das aus Iteration 1.

**Sperre (ADR 0078 Punkt 5).** Die Sperre sitzt an einer Stelle: `update` in `State.fs` tut bei `inspectorBusy` nichts, wenn die Nachricht den Inspektor verlässt oder das Feld ändert (`touchesAssignField`: tippen, Schritt, Verlassen, Esc). `assignFieldReadOnly` in `Moves.fs` setzt zusätzlich `readOnly` an der View. Test zuerst: alle 6 neuen Tests waren vor dem Fix rot, die 5 aus der Tastentabelle mit Stummeln ebenso.

**Über den Auftrag hinaus.** Die Sperre allein reichte nicht. Nach `MoveAnswered` stand der Monat bis zur Antwort des Neu-Lesens noch auf 900, ein Schritt +10 in dieser Lücke schickte wieder 910. Deshalb trägt `applyMoveAnswered` die geschriebenen Zeilen des Berichts sofort in den Monat ein (`withReport`, `withAssigned`, gemeinsam mit `writeThrough`). Gescheiterte Zeilen und Berichte eines anderen Monats zählen nicht.

**Vorhandene Eingabe beim Verlassen während des Aufrufs.** Über die View kann keine bestehen, der Fehlbetrag-Knopf verwirft sie laut Kriterium, und in Holen und Abgeben gibt es das Feld nicht. Auf Nachrichtenebene gibt es den Fall doch (`typed` vor `ConfirmTransfer`): Sie bleibt stehen, geht nicht in die Schlange und wird nach der Antwort beim Verlassen gespeichert. Getestet.

**Rahmen.** Beide Tests gehen durch `update` mit `AssignAnswered` und `AssignCallLost`. Gezählt wird die Zahl der angestoßenen Aufrufe (`commands`), ausführen lassen sie sich unter .NET nicht. Der Test sieht also 2 statt 1, nicht das Ziel des zweiten Aufrufs.

**Tasten.** `assignKeyAction` und `amountKeyAction` in `Moves.fs` bilden die Taste auf `FieldKeyAction` ab. Die View (`fieldKeys`) führt nur aus und nimmt den Fokus. `stopPropagation` und `blur` selbst sind nicht unter .NET geprüft, `blur` nach Enter deckt der vorhandene Browser-Wächter "PC, Panel" ab. Keine neuen Playwright-Tests.

**Live-Stellen von `assignMoneyWith` (Regel 3):**
| Stelle | Beleg | Gegenbeweis |
|---|---|---|
| `Api.fs:1899`, Endpoint `assignMoney` | `TargetsApplyTests.fs` "Jeder Endpunkt reicht Token und Budget in dieser Reihenfolge an den Port" | Token und Budget vertauscht: rot |
| `Api.fs:1913`, `liveBudgetApi` übergibt `setting` | neu "Der Live-Anschluss liest Zuweisen-Token und Budget aus den Settings" | Settings leer: rot. Vor dem neuen Test überlebte dieser Mutant |
| `Api.fs:1913`, `livePort` (lesen und PATCH) | nur `YnabIntegrationTests.fs` hinter `RUN_INTEGRATION_TESTS`, und dort nur `getBudgetFrame` | kein Gegenbeweis im normalen Lauf |
| `Api.fs:1917`, `budgetApi` mit `getSetting` | Negativtabelle ruft `budgetApi` auf, Pfad ohne Token | Settings leer: **überlebt**, die Testdatenbank hat kein Token und ein Token führte ins Netz |
| `Api.fs:1974`, `Remoting.fromValue` | `RemotingPipelineTests.fs` schickt `getBudgetMonth` durch den Host | nicht mutiert |

Die Lücken bei `livePort` und `:1917` teilt `assignMoney` mit allen Budget-Endpoints. Ein Lauf mit gesetztem Schalter hat in dieser Task nicht stattgefunden, ein echtes `assignMoney` gegen YNAB ist also unbelegt.

**Gegenprobe, je Mutante einzeln gesetzt, rot gesehen, zurückgenommen. Die Aussage gilt nur für diese Liste:**
| Mutante | Rot |
|---|---|
| Feld ohne Sperre | 3, "Tippen, Schritt, Enter und Esc ändern nichts ...", "Stand 900 ...", "Eine vorhandene Eingabe ..." |
| Schritt nicht gesperrt | 2, "Tippen, Schritt ...", "Stand 900 ..." |
| Verlassen nicht gesperrt | "Eine vorhandene Eingabe ..." |
| `readOnly` ohne Aufruf | "Das Feld ist schreibgeschützt ..." |
| Bericht nicht eingetragen | 2, "Nach der Antwort rechnet das Feld vom neuen Stand ...", "Eine gescheiterte Zeile ..." |
| auch gescheiterte Zeilen eingetragen | "Eine gescheiterte Zeile ..." |
| Bericht ohne Monatsprüfung | "Eine gescheiterte Zeile ..." |
| `due` immer falsch | "Eine Zuweisung im letzten Monat ..." |
| `AssignAnswered` ohne `frameRefreshCmd` | "Eine Zuweisung im letzten Monat ..." |
| `AssignCallLost` ohne `frameRefreshCmd` | "Ein abgebrochener Aufruf ..." |
| Enter und Esc im Feld vertauscht | 3, "Tasten in den Feldern" |
| Enter im Betragsfeld sendet | 2, darunter "Enter im Betragsfeld sendet nichts" |
| Esc im Betragsfeld ohne Rücksetzen | 2 |
| Rücksetzen nimmt die Gegenseite | 2 |
| `Api.fs:1899` Token und Budget vertauscht | "Jeder Endpunkt reicht ..." |
| `Api.fs:1913` Settings leer | "Der Live-Anschluss ...", siehe oben |
| `Api.fs:1917` Settings leer | **Überlebender**, siehe oben |

**Bekannte Grenzen, zusätzlich zu Iteration 1:**
- Nach `MoveCallLost` ist der Ausgang unbekannt, bis zum Neu-Lesen rechnet das Feld vom alten Stand. Die Lücke ist dieselbe wie oben, nur ohne Bericht zum Eintragen.
- "Erneut senden" läuft ohne diese Sperre und ohne `withReport`. Während `RetryRunning` ist das Feld bedienbar.

## Outcome (Ergänzung Iteration 3)

`./verify.sh`: `VERIFY: PASS` (dotnet 3450 grün, node 471 grün). Kein Live-Aufruf gegen YNAB. Sprache unverändert, kein neues README-Delta.

**Korrektur zu Iteration 2.** Die Begründung zu `Api.fs:1917`, ein Token führte ins Netz, war falsch: `withYnabPort` endet ohne `ynab_default_budget_id` vor dem Port. Der Beleg läuft jetzt über `apiWirings`.

**Erneut senden.** `inspectorBusy` kennt `RetryRunning`, auch ohne gewählte Kategorie, und `canSend` stützt sich darauf (Parameter `tab` entfällt). `startMoveRetry` startet nicht bei laufendem Aufruf oder nicht leerer Schlange. `applyMoveRetried` trägt den Bericht per `withReport` ein. `inputActive` zählt den Lauf mit.

**Unklarer Ausgang.** `awaitMonth` setzt `Month = Loading` und eine neue Generation, kein neuer Merker, dasselbe Muster wie `closeTargets`. Es gilt für `MoveCallLost` und `MoveRetried (Error _)`, also auch für die Antwort des Neu-Lesens nach Erneut senden. Ohne `Success` gibt es weder Feld noch Knopf, eine ältere Monatsantwort zählt nicht. Das Neu-Lesen beendet die Sperre. Scheitert es, steht die Fehlerkarte mit Wiederholen, und danach ist das Feld frei. Läuft schon ein Monatswechsel, bleibt die gedimmte Liste stehen, er sperrt selbst.

**Preis dieser Wahl.** Auf diesen seltenen Wegen weicht die Liste kurz "Lade Budget ..." und das Inspektor-Sheet fährt aus, bis die Antwort da ist; Auswahl und Modus bleiben. Gescheitertes Neu-Lesen zeigt die Karte statt der alten Zahlen, weil die Stände nicht mehr gelten. Sichtprüfung fehlt.

**Weitere Wege geprüft:**
| Weg | Ergebnis |
|---|---|
| `MoveAnswered` Bericht | `withReport`, seit Iteration 2 |
| `MoveAnswered (Error _)` | nichts geschrieben, keine Lücke |
| `MoveCallLost`, `MoveRetried (Error _)` | `awaitMonth`, neu |
| `MoveRetried` Bericht | `withReport`, neu |
| `ConfirmTargets`, `RetryFailed` | starteten mit wartender Zuweisung, warten jetzt auf die leere Schlange, neu |
| Targets, Dialog offen bis Schließen | Der Dialog deckt das Feld ab, `closeTargets` setzt `Loading`. Auf Nachrichtenebene nicht gesperrt, über die View nicht erreichbar |
| `AssignCallLost`, `AssignAnswered (Error _)` | keine Bewegung. Zuweisen ist absolut, das Feld darf bis zum Neu-Lesen vom gezeigten Wert rechnen, denn der ist der gemeinte (ADR 0078) |

**Live-Stellen, aktualisiert (Regel 3):**
| Stelle | Beleg | Gegenbeweis |
|---|---|---|
| `Api.fs:1899`, Endpoint `assignMoney` | "Jeder Endpunkt reicht Token und Budget ..." | Vertauscht: rot (Iteration 2) |
| `livePort` (`getMonth`, `setAssigned`) | `apiWirings` | Mutant im echten `Api.fs` rot, Einträge in der Gegenbeweis-Tabelle |
| `Api.fs:1913`, `liveBudgetApi` | `apiWirings`, ganze Bindung mit `setting`, `livePort`, `liveFramePort`, Uhr, Zone; dazu der Verhaltenstest aus Iteration 2 | leere Settings im echten `Api.fs` rot, je ein Ersatz je Argument im Gegenbeweis |
| `Api.fs:1917`, `budgetApi` | `apiWirings` | leere Settings im echten `Api.fs` rot, Iteration 2 überlebte |
| `Api.fs:1974`, `Remoting.fromValue` | `apiWirings` | anderer Server im Gegenbeweis |

Das ist ein Beleg des Anschlusses per Quelltext. `setAssigned` selbst ist gegen YNAB in dieser Task nicht gelaufen, `RUN_INTEGRATION_TESTS` war nicht gesetzt.

**Änderungen außerhalb des Auftrags:**
- `retryMoveCmd` liegt hinter einem Lambda (`State.fs`), sonst warf `RetryMove` unter .NET beim Zugriff auf den Proxy. Ohne das liefen die neuen Tests nicht durch `update`.
- `scripts/move-money.test.mjs`, "Überholt das Neu-Lesen ...": kein neuer Test, nur die Reihenfolge getauscht, erst Monatswechsel, dann Erneut senden. Die alte Folge ist gesperrt, seit ein laufendes Erneut senden den Inspektor hält. Der Wächter beißt weiter: Mutante `Reloaded` ohne `withEntries` ist rot.

**Test zuerst.** 9 Tests vor dem Fix rot gesehen. Grün schon vorher: "nach dem Neu-Lesen zählt der gelesene Stand" (Intakt-Szenario). Der Test zum Monatswechsel kam nach, weil dessen Mutante zunächst überlebte.

**Gegenprobe, je Mutante einzeln gesetzt, rot gesehen, zurückgenommen. Die Aussage gilt nur für diese Liste:**
| Mutante | Rot |
|---|---|
| `inspectorBusy` ohne `RetryRunning` | 2, "Fehlbetrag scheitert bei 900 ...", "Erneut senden sperrt auch ohne ..." |
| `inputActive` ohne laufenden Aufruf | "Erneut senden sperrt auch ohne ..." |
| `canSend` ohne `inspectorBusy` | 4 |
| `startMoveRetry` ohne Schlange | "Erneut senden startet nicht ..." |
| `startMoveRetry` ohne Sperre | dieselbe |
| `applyMoveRetried` ohne `withReport` | "Nach Erneut senden rechnet das Feld ..." |
| `MoveCallLost` ohne `awaitMonth` | 3, "Aufruf abgebrochen: ..." |
| `MoveRetried (Error _)` ohne `awaitMonth` | "Scheitert Erneut senden, bleibt das Feld gesperrt ..." |
| `awaitMonth` ohne neue Generation | "Aufruf abgebrochen: Formular zurückgesetzt ..." |
| `awaitMonth` räumt auch beim Monatswechsel | "Scheitert Erneut senden während eines Monatswechsels ...". Überlebte zuerst, Test ergänzt, dann rot |
| `confirmTargets` ohne Schlange | "Solange Zuweisungen in der Schlange stehen ..." |
| `retryFailed` ohne Schlange | dieselbe |
| `livePort` ohne `setAssigned` | "die Endpoints hängen an der echten Verbindung ..." |
| `liveBudgetApi` mit leeren Settings | dieselbe |
| `budgetApi` mit leeren Settings | dieselbe |
| Browser: `Reloaded` ohne `withEntries` | "Überholt das Neu-Lesen ..." |

**Bekannte Grenzen, ersetzt die Liste aus Iteration 2:**
- Während Erneut senden läuft, tun Monat, Kategorie, Tab und Schließen nichts, auch ohne offene Kategorie. Das ist die Folge von `inspectorBusy` mit `RetryRunning`.
- Ein Klick auf "Erneut senden" bei offener Eingabe speichert per Blur, die Schlange ist dann nicht leer, und der Klick tut nichts. Der Knopf zeigt das nicht, ein zweiter Klick nach dem Speichern wirkt.
- Dasselbe gilt für "Übernehmen" bei wartender Zuweisung.
- `stopPropagation` und `blur` sind nicht unter .NET geprüft (Iteration 2).

## Outcome (Ergänzung Iteration 4)

`./verify.sh`: `VERIFY: PASS` (dotnet 3455 grün, node 471 grün). Kein Live-Aufruf gegen YNAB. Sprache unverändert, kein neues README-Delta.

**Die Regel.** `update` leitet jede startende Nachricht über eine Stelle: `startsMove` nennt die Nachrichten, `startMove` wendet die Regel an, `assignSettled` in `Moves.fs` entscheidet. Dazu gehören `RetryMove`, `ConfirmTargets`, `RetryFailed` und, nach Prüfung, `ConfirmTransfer`. Der Fehlbetrag-Knopf bleibt draußen, er verwirft die Eingabe laut Kriterium. Die getrennten Bedingungen in `startMoveRetry`, `confirmTargets` und `retryFailed` aus Iteration 3 sind entfernt.

**Verhalten:**
| Lage beim Tipp | Ergebnis |
|---|---|
| Bewegung läuft | nichts |
| Zuweisen ruht | Bewegung startet |
| offene Eingabe, geändert | wie Verlassen: Sie geht in die Schlange und die Bewegung startet nicht. Der Knopf ist gesperrt und nach dem Leerlauf wieder frei. Der Tipp wird nicht nachgeholt, der Mensch sieht die neue Lage und tippt noch einmal |
| offene Eingabe, ungültig | fällt mit Warn-Toast weg, die Bewegung startet nicht |
| offene Eingabe ohne Änderung (`+0`) | fällt weg, die Bewegung startet im selben Tipp. Sonst wäre das ein stiller Klick, denn es ändert sich nichts |
| Schlange läuft, ohne Eingabe | Knopf gesperrt, die Nachricht tut nichts |

**Sichtbarkeit.** "Übernehmen", "Fehlgeschlagene erneut senden" und "Erneut senden" sind über `canStartMove` gesperrt, solange die Schlange oder eine Bewegung läuft. Eine offene Eingabe sperrt nicht. `moveRetries` bekommt dafür `model`. Die `IsDisabled`-Verdrahtung selbst hat keinen Test, geprüft ist nur `canStartMove`, keine neuen Playwright-Tests wie beauftragt.

**Fund der Aufzählung.** Nach Targets anwenden gilt der gezeigte Stand bis zum Schließen nicht. `inspectorBusy` umfasst jetzt `moveRunning` und die Berichte `ApplyDone` und `ApplyFailed`. Das in Iteration 3 als "über die View nicht erreichbar" stehen gelassene Loch ist damit auch auf Nachrichtenebene zu.

**Tests.** Eine Tabelle `moveWays` über alle vier Wege mit fünf Prüfungen, dazu die Aufzählung. Sie läuft durch das echte `update` gegen ein kleines YNAB-Modell: alle Reihenfolgen aus Schritt, Tippen, Verlassen, Start, Antwort, Abbruch, Schließen, Zuweisung fertig und Neu-Lesen bis Tiefe 6, je Weg, dazu Tiefe 5 ab offener Eingabe. Geprüft wird bei jedem Schritt: Beim Start sind Schlange und Eingabe leer, danach liegt kein Wert von vor der Bewegung in der Schlange. Ein Zähler belegt, dass Zuweisungen nach der Bewegung überhaupt erreicht werden.

Grenzen der Aufzählung: Sie kennt nur relative Eingaben, `950` ist gewollt absolut. Ein abgebrochener Aufruf gilt als angekommen. Der Fall "nicht angekommen" steht als Einzeltest aus Iteration 3.

**Test zuerst.** Vor dem Fix rot: 5 von 6 neuen Tests, davon 2 als Fehler, weil `retryCmd` unter .NET den Proxy anfasste, und die Aufzählung mit der Meldung "Eingabe beim Start: Schritt, ..., Start". Der sechste lief vor dem Fix grün.

**Änderungen an Bestehendem:**
- `retryCmd` liegt hinter einem Lambda, wie `retryMoveCmd` in Iteration 3.
- Test "Solange Zuweisungen in der Schlange stehen ..." entfernt, die Tabelle deckt ihn über `update` ab.
- Test "Eine vorhandene Eingabe ...": Über `update` entsteht dieser Zustand nicht mehr, er ist per `confirmTransfer` von Hand gebaut. Der Fall bleibt als Absicherung für `startMove` und das Verlassen.
- Strukturtest in `TargetsApplyClientTests.fs`: Literal `Button.primary "Übernehmen"` zu `Text = "Übernehmen"`, der Knopf braucht `IsDisabled`.
- `canSend` verliert nichts: Der Fehlbetrag-Knopf sperrt weiter darüber.

**Gegenprobe, je Mutante einzeln gesetzt, rot gesehen, zurückgenommen. Die Aussage gilt nur für diese Liste:**
| Mutante | Rot |
|---|---|
| Regel weg, Start ohne `startMove` | 5 |
| `startsMove` ohne `RetryMove` | 5 |
| `startsMove` ohne `ConfirmTargets` | 4 |
| `startsMove` ohne `RetryFailed` | 4 |
| `startsMove` ohne `ConfirmTransfer` | 3, darunter die Aufzählung |
| speichert auch bei laufender Bewegung | "Eine vorhandene Eingabe ..." |
| startet trotz Warnung | "Eine ungültige Eingabe ..." |
| startet im selben Update | 4, darunter die Aufzählung |
| startet ohne Speichern | 3 |
| `assignSettled` ohne Eingabe | 3, darunter die Aufzählung. Überlebte zuerst, weil `startMove` immer verließ. Umgebaut, dann rot |
| `canStartMove` ohne Schlange | 4, darunter die Aufzählung |
| `canStartMove` ohne laufende Bewegung | "Ohne Eingabe und mit leerer Schlange ...". Überlebte zuerst, Prüfung ergänzt, dann rot |
| `moveRunning` ohne `Applying` | 4 |
| `moveRunning` ohne erneutes Senden der Targets | 4 |
| `inspectorBusy` ohne `ApplyDone` | die Aufzählung |
| `inspectorBusy` ohne `ApplyFailed` | die Aufzählung |

**Bekannte Grenzen, ersetzt die Liste aus Iteration 3:**
- Während Erneut senden und solange ein Targets-Bericht offen steht, tun Monat, Kategorie, Tab und Schließen des Inspektors nichts.
- Auf den Wegen mit unklarem Ausgang weicht die Liste kurz "Lade Budget ...", Sichtprüfung fehlt (Iteration 3).
- Eine ältere Vorschau im offenen Targets-Dialog passt nach dem Speichern nicht mehr. Dann antwortet der Server beim nächsten Tipp mit der geänderten Vorschau, wie bisher.
- `stopPropagation` und `blur` sind nicht unter .NET geprüft (Iteration 2).

## Outcome (Ergänzung Iteration 5)

`./verify.sh`: `VERIFY: PASS` (dotnet 3462 grün, node 471 grün). Kein Live-Aufruf gegen YNAB. Sprache unverändert, kein neues README-Delta.

**Stiller Klick.** `startMove` meldet "Läuft noch. Gleich noch einmal versuchen." (`moveBlockedText`, Warn-Toast):
| Lage beim Tipp | Toast |
|---|---|
| Bewegung läuft | ja |
| Schlange läuft, ohne Eingabe | ja |
| Schlange läuft, Eingabe ohne Änderung fällt weg | ja |
| offene Eingabe gerade gespeichert | nein, der Knopf zeigt es |
| ungültige Eingabe | die Warnung dazu, sonst keiner |

Der Weg über den Toast "Erneut senden" ist durch das Wurzel-`update` getestet: `toastActionMsg` führt zu `RetryMove`, die Antwort ist genau `ShowToast`. Den `ToastActionClicked`-Schritt selbst führt der Test aus, sein Cmd nicht, denn der enthält den Zeitgeber; die Übersetzung geht über `toastActionMsg` direkt.

**Seitenwechsel.** Kein Merker nötig, aber `TabMoney` zu behalten reichte nicht: `syncMoneyTab` hätte ihn bei `Selected = None` gleich wieder geräumt. `enterPage` behandelt eine laufende Bewegung deshalb wie die Rückkehr aus dem Kassenbuch: Auswahl, Tab, Filter, Suche und Monat bleiben, der Monat wird neu gelesen. Das Feld bleibt gesperrt, die Antwort findet den Tab vor. Ohne laufenden Aufruf beginnt die Seite wie bisher neu.

**Über den Auftrag hinaus.** Eine Absage (`Error`) ohne Toast steht nur im Tab. Kam sie, während die Seite verlassen war, hätte `enterPage` sie mit dem Tab gelöscht. Der Tab bleibt deshalb auch bei `Rejected`. Folge: Ein abgelehntes Formular steht nach Seitenwechsel und Rückkehr wieder da.

Der Test "Kommt die Absage erst nach dem Seitenwechsel, nennt ein Toast den Grund" ist ersetzt durch zwei Tests für beide Reihenfolgen, Grund im Tab. Der Toast-Zweig bleibt für einen weggefallenen Tab, eigener Test.

**Aufzählung.** Neu sind die Ereignisse Seite weg, Seite da und Wählen sowie der Fehlbetrag als fünfter Weg. Sie fand den Fund selbst: "Fehlbetrag: 910 nach Start, Seite da, Neu-Lesen, Wählen, Schritt, Verlassen". Grenzen wie in Iteration 4.

**Test zuerst.** Vor dem Fix rot: 5 Tests (Toast-Tabelle, Eingabe ohne Änderung, Seitenwechsel, Aufzählung, Toast-Weg). Grün schon vorher als Intakt-Szenarien: "Wird die offene Eingabe gerade gespeichert ...", "Ohne laufenden Aufruf beginnt die Seite beim Betreten neu". Der Absage-Test auf der anderen Seite entstand nach dem ersten grünen Lauf, sein Rot belegt die Mutante "trotz Absage".

**Gegenprobe, je Mutante einzeln gesetzt, voller .NET-Lauf rot gesehen, zurückgenommen. Die Aussage gilt nur für diese Liste:**
| Mutante | Rot |
|---|---|
| Tab geräumt trotz Lauf | 3, darunter die Aufzählung |
| Tab geräumt trotz Absage | "Kommt die Absage auf der anderen Seite ..." |
| Tab bleibt, Seite beginnt trotzdem neu | 4, darunter die Aufzählung |
| Tab bleibt immer | "Der Tab überlebt das Betreten der Seite nicht" |
| kein Toast bei laufender Bewegung | "Läuft die Schlange oder eine Bewegung ..." |
| kein Toast bei laufender Schlange | 3, darunter der Toast-Weg |
| Toast auch beim Speichern | "Wird die offene Eingabe gerade gespeichert ..." |
| Toast verdrängt die Warnung | "Eine ungültige Eingabe ..." |

**Bekannte Grenzen, zusätzlich zur Liste aus Iteration 4:**
- Beim Betreten während eines Aufrufs steht kurz "Lade Budget ...", der Monat wird neu gelesen.
- Ein alter Toast "Erneut senden" für eine schon erledigte Verschiebung schließt sich ohne Meldung: Es gibt nichts mehr zu senden.
- Fällt die gewählte Kategorie während eines Aufrufs beim Neu-Lesen weg, geht der Tab und `moveRunning` wird falsch, bevor die Antwort da ist. Das Feld dieser Kategorie gibt es dann nicht mehr, ein Wert für eine andere Kategorie ist nicht betroffen. Nicht in der Aufzählung.


## Outcome (Review-Funde zu d0a794d)

`./verify.sh` im Worktree `.worktrees/review-c5w2n`: `VERIFY: PASS` (dotnet 3483 grün, 10 opt-in übersprungen, node 466 grün). Delta: `npm run check:readme-delta -- --bc budgeting` Exit 0. Ungeprüft: Teil C wurde nicht im Browser angesehen. Abgesichert sind dort nur Gate, Wächter und die Tests zu C2, keine Screenshots. Die Sichtprüfung liegt bei Roman.

**Abweichungen vom Auftrag, bitte lesen:**
- **C14 nicht umgesetzt, der Befund ist falsch.** `noticeFor PickerFailed` ersetzt die ganze Kategorienliste. Mit `RecentCounterparts = Failure` wäre der Picker unbedienbar, nur weil "Zuletzt verwendet" fehlt. Das widerspricht auch dem Kriterium "ohne Abschnitt, ohne Fehlermeldung". `LoadState` und `OnRetry` bleiben, wie sie waren.
- **A9, Vertragssatz: nur eingeschränkt.** `Shared/Api.fs` nennt jetzt die Ausnahme "Bericht nach dem Senden nicht lesbar". Der Client behandelt ein solches `Error` weiter als abgelehnt und lässt die Form stehen. Der Weg setzt eine unlesbare, gerade selbst geschriebene Zeile voraus (`loadStates`), einen Test dafür gibt es nicht.
- **B1: Die Prädikate sind nicht alle fort.** `moveRunning`, `canStartMove`, `canSend`, `canCover`, `inspectorBusy` und `assignFieldReadOnly` bleiben als dünne Lesungen; die ersten beiden Gruppen sind der Rohstoff und die Knopfsicht, `assignSettled` ist weg. `assignFieldReadOnly` prüft `monthChanging` zusätzlich, weil eine laufende Schlange im Gate vor dem Monatswechsel steht. Die `confirm*`-Funktionen haben keine Sperre mehr, sie steht in `transition`.
- **B2: Ein fünfter Fall `MsgRoute.Left`** für `ReturnAbandoned` und `PageLeft`. Sie speichern wie `Leaves`, lassen sich aber nicht sperren, die Seite ist schon weg (A6, und `PageLeft` muss für A5 durch).
- **E6, `keepsTab`:** Für den laufenden Aufruf entfällt die Regel. Geht Roman während eines Aufrufs weg und kommt zurück, beginnt die Seite neu, eine spätere Absage nennt ein Toast. Der alte Test "Kommt die Absage nach der Rückkehr, steht der Grund im Tab" prüft deshalb jetzt den Toast. `keepsTab` bleibt nur für `RejectedAway`.
- **A5:** Eine eigene Bedingung in der View war unnötig. `switchMode` nimmt die Absage weg, im Modus Zuweisen kann also nur die des Fehlbetrag-Knopfs stehen. Sie fällt beim Moduswechsel oder beim nächsten Tipp auf den Knopf weg, nicht beim Tippen ins Feld.
- **E1, Reichweite:** Der Hinweis wird schon beim Speichern ersetzt, nicht erst beim Scheitern; sonst bliebe er nach einem Erfolg stehen. Das gilt auch für den Hinweis eines gescheiterten Fehlbetrag-Knopfs (eine Zeile). Umgekehrt ersetzt ein neuer Fehlbetrag-Bericht einen offenen Zuweisen-Hinweis. Scheitert eine Zuweisung, während eine neuere wartet, nennt der Toast den Grund ohne "Erneut senden".
- **A8:** Der Toast kommt nur, wenn etwas verloren geht und der Auftrag wirklich hinausgeht. Bei `+0` oder gesperrtem Knopf kommt keiner.
- **D3:** Nach dem Löschen von `:349` hat Holen keinen eigenen Happy Path im Browser mehr, das Kriterium der Task nannte je einen für Holen und Abgeben. Abgeben hat ihn weiter.
- **`Input.Large`** misst jetzt 18 px (E5). Der Wächter `ledger-filter-bar.test.mjs` erwartete 16 px und ist nachgezogen, der Betrag prüft `>= 16`.
- **Sammelzweig:** Die Fälle `Blocked ReportOpen` und `Blocked MonthChanging` starten Dialog und Hinweis ohne Speichern. Eine offene Eingabe unter einem Monatswechsel ist über `update` nicht erreichbar, weil `SelectMonth` und das Nachlesen vorher speichern und das Feld dann schreibgeschützt ist.

**Neu im Modell:** `MoveCall` (`Model.MoveInFlight`, E6), `MonthFreshness` (`Model.Freshness`, E2), `OpenMoveRetry.AssignTarget` (E1), `Submission.RejectedAway`; `Sending` ist weg. Die Überlagerung der Schlange steht in `Month.withQueue` und läuft in `applyLoaded`. Regeln: `Moves.moveGate`, `State.routeOf`, `Moves.leaveInspector` mit `LeaveOutcome`.

**Test zuerst.** Alle A-Tests laufen durch das echte `update`, Ausnahme sind die reinen Server-Tests aus A9. Vor dem ersten Fix waren 16 von 140 rot, jeweils aus fachlichem Grund. A9 war danach einzeln rot (`SQLite Error 1: geheim` kam durch; die erfundene Id ebenso). Die Vorbereitung vor dem Rot war nur die Konstante `coverDroppedEntryText` und `recentMoveCategoriesWith` mit dem alten Verhalten. "ReturnAbandoned speichert die Eingabe" und die intakt-Szenarien waren von Anfang an grün, die Mutanten unten belegen sie.

Drei alte Tests mussten nach den Fixes geändert werden, weil sie das alte Verhalten prüften: Erneut-senden-Weg jetzt mit zwei Zeilen (sonst greift E1), "Erneut senden startet nicht" (Vorbedingung Neu-Lesen wegen E2, andere Kategorie wegen E1), `settledTests` wählt nach der Rückkehr neu.

**Gegenprobe, je Mutante einzeln gesetzt, Lauf, zurückgenommen (Skript). Die Liste gilt nur für diese Mutanten.**

| Fund | Mutante | Rot | Erster roter Test |
|---|---|---|---|
| A1 | `canCover` ohne Frische | 3 | Fehlbetrag 100, Knopf, Antwort da |
| A1 | Antwort beendet Veraltet nie | 3 | Erneut senden startet nicht |
| A1 | Zuweisen markiert nicht veraltet | 1 | Nach dem Speichern und bei leerer Schlange |
| A2 | Aufruf zählt nur mit Tab | 1 | Fehlbetrag läuft, Seite weg und zurück |
| A3 | Speichern lässt den Hinweis stehen | 1 | Zwei gescheiterte Zuweisungen 200 und 250 |
| A3 | ältere Absage trotz wartender | 1 | Beide unterwegs |
| A3 | Bericht ersetzt gleiche Kategorie nicht | 1 | Scheitert der Fehlbetrag-Knopf nach einer gescheiterten Zuweisung. Überlebte zuerst, Test ergänzt |
| A4 | Nachlesen speichert nicht | 2 | Rückt das Nachlesen den Monat weiter |
| A4 | Nachlesen speichert immer | 1 | Ändert das Nachlesen den Monat nicht |
| A5 | Form lässt die Absage stehen | 1 | Jede Änderung der Form |
| A5 | Modus lässt die Absage stehen | 1 | Jede Änderung der Form |
| A5 | Knopf lässt die Absage stehen | 1 | Ein erneuter Tipp auf den Fehlbetrag-Knopf |
| A5 | Abwesenheit nicht gemerkt | 1 | Eine Absage, die kam, während die Seite verlassen war |
| A5 | ungelesen bleibt ungelesen | 1 | dieselbe |
| A5 | gelesene Absage hält die Seite fest | 2 | dieselbe |
| A6 | Sperre schluckt `Left` | 2 | dieselbe |
| A6 | `Left` speichert nicht | 2 | Jeder Weg ergibt genau eine Zuweisung |
| A7 | Antwort ohne Schlange | 2 | Auch eine wartende Zuweisung bleibt stehen |
| A7 | Schlange ohne Monatsprüfung | 1 | dieselbe |
| A8 | keine Meldung | 1 | Der Fehlbetrag-Knopf verwirft eine offene Eingabe |
| A8 | meldet auch ohne Änderung | 1 | Ohne Änderung im Feld |
| A9 | roher Ausnahmetext | 1 | Scheitert das Lesen |
| A9 | erfundene Id | 1 | Steht YNAB schon auf dem Wert |
| B | `ConfirmTransfer` ohne Sperre | 1 | Während eines Monatswechsels ist der Knopf gesperrt |
| B | `CoverMissing` ohne Sperre | 7 | Während eines Monatswechsels nimmt das Feld keine Eingabe an |
| B | Gate ohne Monatswechsel | 2 | dieselbe |
| B | Gate ohne Schlange | 4 | Holen, Abgeben und der Fehlbetrag-Knopf sind gesperrt |
| B | `Queued` startet trotzdem | 1 | Offene Eingabe: der Start speichert sie |
| D1 | Bericht trägt den Stand nicht ein | 4 | Eine gescheiterte Zeile ändert den Stand nicht |
| D1 | Abbruch lässt den alten Monat stehen | 4 | Aufruf abgebrochen: das Feld nimmt nichts an |
| D1 | Feld während der Bewegung frei | 2 | Fehlbetrag scheitert bei 900 |
| D2 | `PageLeft` fehlt | 1 | Verlässt man die Seite |
| C2 | Leer gilt als ungültig | 1 | Das Betragsfeld meldet nur, was keine Rechnung ist |

Die erste A-Reihe lief vor Teil B, die Mutanten A2 und ab A5 "gelesene Absage" sowie B, D und C2 nach Teil B. Die übrigen A-Mutanten wurden nach Teil B nicht wiederholt. Die Tests dazu blieben unverändert grün. Die Tabelle nennt nur den ersten roten Test: Dass die neue Aufzählung selbst rot wurde, ist je Mutante nicht einzeln belegt.

**D1:** `events`, `check` und `walk` sind getrennt, mit eigenem Merker `Pending` und Orakel über `Stand` und Kategorie. Neu: Neu-Lesen ohne Kategorie, `CloseInspector`, Esc, Modus, andere Kategorie. Tiefe 6, Targets-Wege 7, besuchte Zustände werden abgeschnitten. Gemessen je Weg 492 bis 574 Zuweisungen, Targets 84; Mindestzahlen 400 und 60. Der Testlauf der Client-Gruppe dauert rund 0,8 s.

**D2:** `readOnly` während `monthChanging` und `PageLeft` über `State.urlChangedCmd` mit Fang-Dispatch und Gegenprobe. **D3, D4:** wie im Auftrag; Task-Ids aus den Testlisten entfernt, deshalb heißen die Gruppen jetzt `Tab Geld, Client`, `Betragsrechnung`, `Verschieben`, `Zuweisen`.

**Teil C:** C1 bis C13 umgesetzt, neu `TouchTargets.sheetMinHeight` und `alwaysMinHeight`, `Button.amountStep` und `sideRow` haben je einen Parameter mehr. **Teil B5:** Alle genannten Stellen geprüft. `Domain.fs:252` (Doc von `planAssign`) stimmte schon und blieb unverändert, der Hinweis zu Zuweisen steht an `MoveLine` und `MovePlan`.

Während der Arbeit brach ein Schritt von mir ab, weil ein leeres `cat` auf stdin wartete; ohne Folgen für den Code.

Hinweis Kriterium Happy Path: Holen hat keinen eigenen Browser-Test mehr, entschieden mit der neuen Doktrin des Owners.
