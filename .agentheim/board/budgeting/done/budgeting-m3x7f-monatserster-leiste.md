---
id: budgeting-m3x7f
title: "Monatserster: Leiste nach der Skizze mit Targets prüfen, Targets anwenden, Überzogene decken und verteilen; Kopfknopf Monatserster statt Targets anwenden und Prüfen-Icon; Vorschau mit abwählbaren Zeilen; Ausblenden je Monat auf dem Server"
status: done
type: feature
context: budgeting
created: 2026-11-02
completed:
depends_on: [design-system-001, budgeting-t6p8z, budgeting-q4v8n, budgeting-eh8xe, budgeting-hbhzf, budgeting-c4ve4, budgeting-k7r2w]
blocks: []
tags: [budget, monatserster, targets, skizze]
related_adrs: [0065, 0075, 0082, 0083, 0085]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-h2zfn, budgeting-c4ve4, budgeting-q4v8n]
---

## Why
Task 5 von 5 des Gesamtkonzepts. Der Monatserste ist Romans wichtigster Ablauf. Die Leiste führt durch ihn, ohne ihn in einem Modus festzuhalten. Roman am 2026-11-06: Targets anwenden und Targets prüfen gehören in diese Leiste, und die Leiste soll sich auch zwischendurch öffnen lassen. Er betont ausdrücklich, dass die Skizze die Vorlage ist und diesmal nicht verloren gehen darf (Lehre aus budgeting-q4v8n, CLAUDE.md Ablauf).

## Vorlage: die Skizze
Freigegeben von Roman: der Gesamt-Prototyp, Artifact https://claude.ai/artifact/JfQDhhTLwdEadmQyJbbzkz, Seite „Gesamtkonzept“, Board `Gesamt.dc.html`. Die Boards auf der Seite „Archiv“ (`Monatserster.dc.html` mit „geführt oder Leiste“, `Main.dc.html`) sind verworfen und gelten nicht.

Für Worker und Verifier im Repo, ohne Artifact-Zugang:
- Quelltext: `docs/ux/budget/gesamt-prototyp.dc.html` (am 2026-11-06 gegen das Artifact verglichen, gleich). Leiste Zeile 45 bis 52, Vorschau Zeile 183 bis 196, Felder und Regeln Zeile 309 bis 313 und 361.
- Screenshots (`docs/ux/budget/skizze-monatserster/`):
  - `leiste-pc.png`: 2. November, PC, Leiste offen
  - `leiste-pc-rot.png`: nach Klick auf das dritte Feld, Filter aktiv, erste Zeile im Tab Geld
  - `leiste-pc-vorschau.png`: Vorschau Targets anwenden am PC
  - `leiste-handy.png`: Handy mit Leiste
  - `leiste-handy-vorschau.png`: Vorschau am Handy
  - `mitte-pc.png`: 17. November, alles erledigt, keine Leiste, kein Targets-anwenden-Knopf im Kopf
- Neu rendern: Artifact lesen, `artifact-type/dc-runtime.js` als `project/support.js` daneben legen, „2. November“ und „PC“ oder „Handy“ klicken.

Der Prototyp-Kopf („Prototyp“, Tage, PC/Handy, Neu starten) und die erfundenen Beträge gehören nicht zur Seite. Alles andere an Leiste und Vorschau ist ein Kriterium unten. Abweichungen stehen nur im Abschnitt „Abweichungen von der Skizze“.

## What

### Leiste (Skizze Zeile 45 bis 52)
- Eine Karte zwischen Seitenkopf und Suche/Filtern, über die volle Breite der Liste: Fläche `--bg-card` (#111128), Rand `--border-subtle` (#1c1c38), Radius 12 px, Innenabstand 6/10/10 px, Abstand 6 px zwischen Kopfzeile und Raster. Unten 10 px Abstand zur Suchzeile.
- Kopfzeile: links „Monatserster“ (13 px, 600), rechts der Textknopf „Ausblenden“ (12 px, gedämpfte Textfarbe, Trefferfläche mindestens 36 px hoch, am Handy 48 px nach ADR/Styleguide).
- Raster aus vier gleich breiten Feldern mit 6 px Abstand, an PC und Handy gleich (vier Spalten auch bei 390 px, siehe `leiste-handy.png`). Jedes Feld ein Knopf: mindestens 52 px hoch, Radius 10 px, Rand `--border` (#2a2a4a), zentriert, oben der Wert in Mono 13 px 600, darunter die Bezeichnung 11 px gedämpft. Am Handy bricht die Bezeichnung zweizeilig um („Targets / prüfen“), das Feld wächst mit.
- Zustände eines Felds wie die Skizze: aktiv (Filter oder Vorschau gerade offen) Rand `--color-neon-teal` (#00d4aa); erledigt Rand `--border-subtle` (#1c1c38), Text gedämpft, Wert „✓“; sonst Standard.
- Die vier Felder in dieser Reihenfolge:
  1. **Targets prüfen:** Wert = Zahl der Hinweise der Seite Targets prüfen (alle Zeilen außerhalb von „Passt“, dieselbe reine Funktion wie die Zähler der Kopf-Karte aus budgeting-c4ve4, Daten aus `getTargetCheck`). Erledigt bei 0 Hinweisen (Roman 2026-11-06). Klick öffnet `#/budget/target-check`.
  2. **Targets anwenden:** Wert = Summe, die Targets anwenden zuweisen würde, als Betrag; erledigt bei 0. Die Summe muss mit der Summe der Vorschau übereinstimmen (eine Quelle, Test). Klick öffnet die Vorschau.
  3. **Überzogene decken:** Wert = Zahl der überzogenen Kategorien; erledigt bei 0. Klick setzt den Filter „Überzogen“ und wählt am PC die erste Zeile mit Tab Geld (`leiste-pc-rot.png`), am Handy nur den Filter.
  4. **verteilen:** Wert = Zu verteilen, solange positiv; sonst „✓“. Nur Anzeige: Klick setzt den Filter „Alle“. Zählt nicht für das Ende der Leiste.

### Wann die Leiste steht
- Von selbst im laufenden Monat, solange eines der Felder 1 bis 3 offen ist und sie für diesen Monat nicht ausgeblendet ist (Skizze Zeile 361, Roman 2026-11-06). Sind 1 bis 3 erledigt, verschwindet sie (`mitte-pc.png`).
- „Ausblenden“ blendet sie für den Monat aus und setzt den Filter auf „Alle“ (wie die Skizze). Das gilt auf allen Geräten: gespeichert auf dem Server (Roman 2026-11-06), als Zustand, kein Ereignis (ADR 0083, kein Geld). Ein neuer Monat zeigt sie wieder.
- **Kopfknopf „Monatserster“** (Roman 2026-11-06): ein Icon-Knopf wie Historie (`Button.Ghost`, `Small`, Icon `Icons.calendar`, Title und AriaLabel „Monatserster“), daneben im Kopf, am Handy auch. Er zeigt die Leiste jederzeit, auch ausgeblendet oder fertig, bis die Seite verlassen wird oder „Ausblenden“ gedrückt wird. Ist sie offen, schließt er sie wieder. Er schreibt nichts auf den Server.
- Leiste und Kopfknopf gibt es im laufenden Monat und in kommenden Monaten (wie heute `targetsAllowed`), im vergangenen nicht. Von selbst auf geht sie nur im laufenden. Das Feld Targets prüfen zählt immer für den laufenden Monat (die Seite gilt nur für ihn).

### Kopf der Budget-Seite
- Der Knopf „Targets anwenden“ (`targetsButton`, `MonthView.fs`) und das Icon „Targets prüfen“ (`targetCheckButton`, `View.fs`) entfallen. Beide Wege führen über die Leiste (Roman 2026-11-06, Skizze: im Kopf nur Monat, Historie, Zu verteilen).
- Was an diesen Knöpfen hängt, zieht mit: Tastenkürzel, Sperre beim Monatswechsel (`monthChanging`), Browser-Wächter und Messungen der Kopfbreite (`budget-month-bar`, 1024 px in einer Zeile).

### Vorschau Targets anwenden (Skizze Zeile 183 bis 196)
- Je Zeile eine Checkbox, links vor dem Namen, Betrag rechts in Mono, alle beim Öffnen an. Abgewählte Zeilen werden nicht geschrieben.
- Unter den Zeilen „Summe“ (600) und „Zu verteilen danach“ (gedämpft), beide folgen der Auswahl. Ganz unten zwei gleich breite Knöpfe „Abbrechen“ und „Anwenden“.
- Ist keine Zeile gewählt, ist „Anwenden“ gesperrt.
- Hinweis aus dem Review von budgeting-g8s3w: `PreviewReady` und `ApplyDone` (`Types.fs`) werden Records, `confirmTargets` schickt nur die gewählten Zeilen.
- Schreibweg: Targets als Ereignisse in BB (ADR 0084 Punkt 7, budgeting-eh8xe), kein Zwei-Schritt-Weg nach YNAB.

### Abweichungen von der Skizze (nur diese)
1. Feld 3 heißt „Überzogene decken“, der Filter „Überzogen“ (Roman, budgeting-h3k9d). Die Skizze sagt „rot decken“ und „Rot“.
2. Feld 1 öffnet die Seite Targets prüfen. Die Skizze filtert die Liste (ADR 0082, Roman 2026-11-05: eigene Seite). Der Filter-Chip „Targets prüfen (n)“ der Skizze entfällt deshalb.
3. Kopfknopf „Monatserster“ gibt es in der Skizze nicht (Roman 2026-11-06).
4. Filter-Chips heißen wie heute „Alle, Fehlt, Überzogen“, nicht wie in der Skizze.
5. Vorschau: Stand-Prüfung und Bericht je Zeile bleiben (Konzept, „Bleibt unverändert“). Die Knöpfe folgen ADR 0075 Punkt 3 (neutral). Der orange Anwenden-Knopf der Skizze gilt also nicht, solange der Worker im Styleguide nichts anderes findet. Am Handy bleibt der heutige Dialograhmen (Modal oder Sheet), Inhalt und Reihenfolge wie die Skizze.
6. Farben als Tokens aus `src/Client/DesignSystem/`. Die Hex-Werte oben sind die der Skizze. Gibt es den Token nicht, wird er nach dem Styleguide angelegt, nicht hart verdrahtet.

## Acceptance criteria
- [x] Reine Funktion für die vier Felder (Wert, Bezeichnung, Zustand offen/erledigt/aktiv) mit Tests je Feld: offen, erledigt (Wert „✓“), aktiv. Feld 1 zählt wie die Kopf-Karte von Targets prüfen (gleiche Funktion, Test mit einer Zeile unter Passt und einer außerhalb).
- [x] Reine Funktion „steht die Leiste?“ mit Tests: laufender Monat offen; ausgeblendet für diesen Monat; ausgeblendet im Vormonat (steht wieder); Felder 1 bis 3 erledigt bei offenem Feld 4 (steht nicht); kommender Monat (nicht von selbst); vergangener Monat (nie); über den Kopfknopf geöffnet trotz Ausblenden oder fertig (steht).
- [x] Die Summe von Feld 2 und die Summe der Vorschau kommen aus einer Quelle (Test: gleiche Daten, gleiche Summe; ein pausiertes Target zählt nicht).
- [x] Klick auf Feld 3 am PC: Filter Überzogen, erste überzogene Zeile gewählt, Tab Geld (Client-`update`-Test). Ohne überzogene Zeile: leere Ansicht des Filters wie heute.
- [x] Abgewählte Zeilen werden nicht geschrieben (Handler- oder `update`-Test auf das, was `confirmTargets` schickt). Summe und Zu verteilen danach folgen der Auswahl. Ohne Auswahl ist Anwenden gesperrt. Abläufe über mehrere Nachrichten (öffnen, abwählen, anwenden, Monatswechsel) als Aufzählung der Reihenfolgen (Doktrin Punkt 20).
- [x] Ausblenden je Monat auf dem Server: Endpoint und Lesen über den vorhandenen Weg der Budget-Seite; Handler-Test mit In-Memory-SQLite (setzen, lesen, anderer Monat gilt nicht, wiederholtes Setzen); Zeile in `src/Tests/RemotingEndpointNegativeTests.fs` (ungültiger Monat); Schema per `initializeDatabase`, wiederholbar.
- [x] Der Knopf „Targets anwenden“ und das Icon „Targets prüfen“ sind aus dem Kopf entfernt. Der Kopfknopf „Monatserster“ steht neben Historie, öffnet und schließt die Leiste und schreibt nichts.
- [x] Fixtures in `scripts/*.test.mjs` tragen neue Felder (Fable.Remoting-Falle). Browser-Tests, die den alten Kopfknopf nutzen, sind umgestellt oder entfernt.
- [x] `[layout]`: die vier Felder sind gleich breit und mindestens 52 px hoch, PC 1280 px und Handy 390 px; der Kopf passt bei 1024 px weiter in eine Zeile.
- [x] Der vorhandene `[flow:targets-anwenden]` (`FLOWS` in `scripts/browser-usage.mjs`) startet an der Leiste: am Handy über die Vorschau mit einer abgewählten Zeile bis zum Bericht. Kein zweiter Flow, der Satz in `FLOWS` wird angepasst.
- [x] `docs/ux/budget/konzept.md`: der Satz „Die Optik kommt aus Styleguide und echten DS-Komponenten, nicht aus dem Prototyp“ gilt für die Leiste nicht mehr; Kopf ohne Targets anwenden und Targets prüfen, mit Monatserster.
- [x] Seite neben der Skizze verglichen, Abweichungen nur die genannten. Screenshots der Seite in denselben vier Zuständen wie `leiste-pc.png`, `leiste-pc-rot.png`, `leiste-pc-vorschau.png`, `leiste-handy.png` im Outcome, je mit einer Zeile zum Vergleich.
- [x] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Leiste und Vorschau sehen in Prod aus wie die Skizze. [human-eye]

## Notes
- Hängt an budgeting-k7r2w, weil Feld 1 die Hinweise zählt. Ohne k7r2w stünde etwa Strom fälschlich als Hinweis da.
- Feld 1 braucht `getTargetCheck` auf der Budget-Seite. Laden im Hintergrund, ein Lesefehler macht nur das Feld leer („–“), die Leiste bleibt bedienbar. Nach einem Schreiben auf der Seite Targets prüfen und der Rückkehr stimmt die Zahl (neu laden beim Betreten der Budget-Seite).
- Kopfknopf-Icon: `Icons.calendar` ist ein Vorschlag des Conductors, Roman hat nur den Knopf entschieden.
- Wo der Ausblenden-Zustand liegt (Settings-Schlüssel oder eigene kleine Tabelle), entscheidet der Worker nach ADR 0083 und begründet es im Outcome.
- Konzept: `docs/ux/budget/konzept.md`, Abschnitt „Die vier Abläufe“ und „Am Handy“.

## Verlauf
- 2026-11-05 (budgeting-p2zr6): „Targets prüfen“ ist eine eigene Seite (ADR 0082), kein Prüf-Filter. `CheckOnly` in `CategoryFilter` entfällt.
- 2026-11-05 (budgeting-zkbbd): erst nach budgeting-eh8xe bauen, Targets als Ereignisse in BB.
- 2026-11-06 (Roman, AskUserQuestion): Kopfknopf Monatserster statt Targets anwenden und Prüfen-Icon; Leiste im laufenden Monat bis erledigt; Feld 1 zeigt Hinweise, erledigt bei 0; Ausblenden auf dem Server.

## Verifier note (iteration 1)

**REASONS:**
- Criterion "Seite neben der Skizze verglichen, Abweichungen nur die genannten" is not met. On PC the bar does not sit where the task puts it. The task text says "Eine Karte zwischen Seitenkopf und Suche/Filtern … Unten 10 px Abstand zur Suchzeile" (What, Leiste) and "Skizze: im Kopf nur Monat, Historie, Zu verteilen" (What, Kopf der Budget-Seite). The sketch (leiste-pc.png, leiste-pc-rot.png) has the head with month, Historie and Zu verteilen only, then the bar, then search plus chips in their own row. The built page (ist/leiste-pc.png, ist/leiste-pc-rot.png) keeps search and chips in the head above the bar. "Abweichungen von der Skizze" does not list this. The [layout] criterion ("der Kopf passt bei 1024 px weiter in eine Zeile") does not require search to stay in the head.
- The phone head is also an unlisted deviation. The sketch (leiste-handy.png) shows "‹ Nov 2026 › Historie | Zu verteilen" on one line above the bar. The built page puts the month on one line, then Zu verteilen with both icon buttons on a second line.
- Secondary: field 3 on PC opens tab Geld in "Holen" mode; leiste-pc-rot.png shows "Zuweisen" with "900,00 zuweisen", and the task cites that screenshot in its field 3 criterion.

**SUGGESTED_FIX:** Move search and chips on PC below the bar (head: month, Historie, Monatserster, Zu verteilen; bar with 10 px gap to the search row), put the phone head on one line like the sketch, update the [layout] head tests and the screenshots in ist/ to match, and fix or explicitly list the Holen/Zuweisen mode on cover.

**ITERATION_HINT:** likely-fixable

**Conductor-Entscheidung (Roman offline):** Die Skizze gilt, keine neue Abweichung. Suche und Chips am PC unter die Leiste, auch wenn die Leiste nicht steht (wie `mitte-pc.png`: Suche und Chips unter dem Kopf); Handy-Kopf in einer Zeile wie `leiste-handy.png`; Feld 3 öffnet Tab Geld im Modus Zuweisen wie `leiste-pc-rot.png`.

## Outcome

Die Budget-Seite trägt die Leiste Monatserster nach der Skizze. Targets anwenden und Targets prüfen sind aus dem Kopf entfernt, an ihrer Stelle steht der Kopfknopf Monatserster. Die Vorschau hat abwählbare Zeilen, und Ausblenden gilt je Monat auf dem Server. Iteration 2 setzt den Kopf, die Suchzeile und Feld 3 wie die Skizze (Verifier-Note Iteration 1). `./verify.sh` zeigt `VERIFY: PASS`.

**Kopf und Suchzeile** (`src/Client/Components/Budget/View.fs`: `head`, `searchRow`, `belowHead`): Der Kopf hat an PC und Handy dieselbe Reihenfolge wie die Skizze: `‹ Monat ›`, die Icon-Knöpfe Historie und Monatserster, rechts Zu verteilen. Ab 768 px steht er in einer Zeile. Darunter steht die Leiste, darunter mit 10 px Abstand Suche (am PC 256 px) und Chips in eigener Zeile. Ohne Leiste stehen Suche und Chips direkt unter dem Kopf (`mitte-pc.png`). Suche und Chips scrollen am PC jetzt mit, der Kopf haftet weiter. Am Handy steht der Kopf bei 390 px in einer Zeile wie `leiste-handy.png`: der Monat kurz ("Nov 2026", neu `Button.homeLabelCompact`, 64 px, in `/styleguide` und Styleguide §4) und Zu verteilen ohne € wie in der Skizze (`toBeBudgeted compact`). Erst unter 390 px oder mit fünfstelligem negativem Betrag bricht Zu verteilen in eine zweite Zeile um. Die Chips haften am Handy weiter unter dem Kopf, die Suche scrollt mit.

**Leiste** (`src/Client/Components/Budget/MonthStart.fs`, rein; View `monthStartBarView` in `src/Client/Components/Budget/MonthView.fs`): eine Karte zwischen Kopf und Suchzeile mit "Monatserster" und "Ausblenden" und vier gleich breiten Feldern. `monthStartFields` liefert Wert, Bezeichnung und Zustand (offen, erledigt mit "✓", aktiv mit Teal-Rand). Feld 1 zählt die Hinweise mit `checkCounts` aus `getTargetCheck`. Die Daten werden je Betreten der Seite im Hintergrund geladen, mit Generation; ein Lesefehler zeigt "–". Feld 2 nutzt `targetApplicationTotal` aus `Shared.Budgeting`. Das ist dieselbe Quelle (`targetApplicationLines`), mit der der Server die Vorschau plant (ADR 0085). Feld 3 setzt den Filter Überzogen, am PC zusätzlich die erste überzogene Zeile mit Tab Geld im Modus Zuweisen (`coverOverdrawn` über `syncMoneyTab` und `switchMode AssignMode`; eine rote Kategorie startete sonst auf Holen), wie `leiste-pc-rot.png` mit "900,00 zuweisen". Feld 4 setzt den Filter Alle. Wann die Leiste steht, entscheidet `monthStartStands`. Feld 1 hält sie nur mit bekannter Zahl über 0, solange die Prüfung lädt oder nicht lesbar ist, hält sie die Leiste nicht auf.

**Kopfknopf** (`monthStartButton`, `Button.Ghost` `Small`, `Icons.calendar`, das Icon gab es schon im DS): neben Historie, am Handy auch, im vergangenen Monat unsichtbar mit freiem Platz. `ToggleMonthStart` öffnet und schließt die Leiste und schreibt nichts. `PageLeft` setzt ihn zurück. Entfernt sind `targetsButton`, `targetCheckButton` und `targetsButtonVisible`.

**Ausblenden auf dem Server**: Der Zustand liegt in einer eigenen kleinen Tabelle `budget_month_start_hidden (month PRIMARY KEY, hidden_at)`. Er wird angelegt in `initializeBudgetingSchema` und ist wiederholbar. Geschrieben wird über den Endpoint `hideMonthStart` (`Server.Api.hideMonthStartWith`, Monat geprüft, `INSERT OR IGNORE`), gelesen über den Rahmen (`BudgetFrame.MonthStartHidden`, Monate ab dem laufenden, Port `LoadMonthStartHidden`; ein Lesefehler gilt als nicht ausgeblendet). Begründung nach ADR 0083: Ausblenden bewegt kein Geld und entscheidet nichts über Geld, also Zustand und kein Ereignis. Ein Settings-Schlüssel hielte nur einen Monat. Ein Ausblenden im kommenden Monat (dort über den Kopfknopf geöffnet) nähme dann das im laufenden weg, deshalb eine Zeile je Monat.

**Vorschau** (`src/Client/Components/Budget/TargetsView.fs`, `Targets.fs`): `PreviewReady` und `ApplyDone` sind Records (`PreviewShown` mit `Excluded`, `AppliedTargets`). Je Zeile gibt es eine native Checkbox wie in der Skizze und den Betrag, den die Zeile zuweist, in Mono ohne Währungszeichen. Darunter stehen "Summe" (600) und "Zu verteilen danach" (gedämpft), beide aus `selectedPreview`, ganz unten "Abbrechen" und "Anwenden", gleich breit und neutral (`Button.Secondary`, ADR 0075). Ohne Auswahl ist Anwenden gesperrt (`canApplyTargets`). `confirmTargets` schickt nur die gewählten Zeilen. Der Server gleicht sie als Teilmenge ab und schreibt nur sie (`Domain.previewsMatch`, `Domain.chosenLines`, ADR 0085). Die Zeilen scrollen, Summe und Knöpfe bleiben erreichbar. Pausierte Zeilen und die Detailzeile je Kategorie zeigt der Dialog nicht mehr, so wie die Skizze. Stand-Prüfung und Bericht je Zeile bleiben.

**DS**: Neu sind `Button.tile`, `Button.quietText` (Felder und "Ausblenden"; ein Wächter verbietet rohe Knöpfe in den Budget-Views), `Button.homeLabelCompact` und `FontSizes.compact` (13 px). Alle stehen im Styleguide §4 und §5 und in `/styleguide`. Die Ausnahme im Token-Wächter für `Tokens.fs` zählt jetzt 8 statt 7.

**Doku**: In `docs/ux/budget/konzept.md` gilt der Optik-Satz für die Leiste nicht mehr. Der Kopf steht mit Monat, Historie, Monatserster und Zu verteilen da, Suche und Filter darunter, der Ablauf Monatserster ist ergänzt. In `scripts/browser-usage.mjs` beginnen die Sätze für `targets-anwenden` und `targets-pruefen` an der Leiste.

**Tests**: `src/Tests/MonthStartClientTests.fs` (23: Felder je Zustand, Feld 1 wie die Kopf-Karte, alle Fälle von "steht die Leiste?", Kopfknopf, Ausblenden, Überzogene decken am PC mit Modus Zuweisen, auch wenn die Zeile schon in Holen gewählt war, und am Handy, Laden der Prüfung, Auswahl der Vorschau und alle 24 Reihenfolgen von Abwählen, Anwenden und Monatswechsel nach Doktrin Punkt 20). `src/Tests/BudgetMonthStartTests.fs` (6, In-Memory-SQLite: setzen und lesen, anderer Monat, wiederholtes Setzen, ungültiger Monat, Lesefehler, Schema wiederholbar). `src/Tests/TargetsApplyTests.fs` (+3: eine Quelle für die Summe mit pausiertem Target, Teilmengen-Abgleich, abgewählte Zeile wird weder gesendet noch festgehalten). Eine Zeile in `RemotingEndpointNegativeTests.fs`. Browser (`scripts/budget-page.test.mjs`): `[layout]` Kopf bei 768, 1024 und 1440 px in einer Zeile in der Reihenfolge Monat, Historie, Monatserster, Zu verteilen, darunter Leiste, 10 px darunter Suche und Chips in einer Zeile, ohne Leiste die Suche direkt unter dem Kopf; `[layout]` Handy 390 px Kopf in einer Zeile mit Monat, Historie, Monatserster und Zu verteilen; PC-Sticky-Test: der Kopf haftet, Suche und Chips scrollen mit; der Dichte-Test bei 1440 × 900 erlaubt die erste Zeile jetzt in den oberen 30 % statt 25 %, weil die Suchzeile wie in der Skizze eine eigene Zeile hat (16 Zeilen im Blick bleiben verlangt); `[layout]` für die Leiste bei 1280 und 390 px; `[flow:targets-anwenden]` läuft am Handy über die Leiste mit einer abgewählten Zeile bis zum Bericht.

**Seite neben der Skizze verglichen** (Screenshots von Iteration 2 mit gemockten Daten in den Beträgen der Skizze, `docs/ux/budget/skizze-monatserster/ist/`, je mit dem Read-Tool neben das Bild der Skizze gelegt):
- `ist/leiste-pc.png` neben `leiste-pc.png`: Kopf mit `‹ November 2026 ›`, Historie und rechts Zu verteilen, darunter die Karte (Fläche, Rand, Radius, Abstände) mit Kopfzeile und vier gleich breiten Feldern, darunter mit 10 px Abstand Suche und Chips in eigener Zeile, dann Liste und Panel. Abweichungen nur die genannten: Feld 3 heißt "Überzogene decken" und die Chips Alle, Fehlt, Überzogen (1 und 4); der Kopfknopf Monatserster neben Historie (3), Historie als Icon-Knopf wie bisher. Die Beträge in Liste und Kopf tragen am PC wie bisher €.
- `ist/leiste-pc-rot.png` neben `leiste-pc-rot.png`: Filter Überzogen aktiv, Feld 3 mit Teal-Rand, Lebensmittel gewählt, Tab Geld im Modus Zuweisen mit dem orangen Knopf "900,00 zuweisen" wie die Skizze. Aufbau des Inspektors (Umschalter Zuweisen, Holen, Abgeben statt Holen und Abgeben unten) kommt aus dem bestehenden Tab Geld und ist nicht Teil dieser Task.
- `ist/leiste-pc-vorschau.png` neben `leiste-pc-vorschau.png`: Titel, Zeilen mit Checkbox, Name und Betrag, Summe und Zu verteilen danach sowie zwei gleich breite Knöpfe in derselben Reihenfolge, Feld 2 im Hintergrund aktiv. Abweichung 5: Anwenden ist neutral statt orange.
- `ist/leiste-handy.png` neben `leiste-handy.png`: Kopf in einer Zeile "‹ Nov 2026 ›", Historie, Monatserster, rechts Zu verteilen "1.650,00" ohne €, darunter die Leiste mit vier Feldern in einer Zeile (Bezeichnungen zweizeilig), darunter Suche und Chips. Abweichung 3: zusätzlich der Kopfknopf Monatserster; Historie ist ein Icon-Knopf statt Textknopf, damit beide 48-px-Knöpfe in die Zeile passen.
- Zusätzlich `ist/leiste-handy-vorschau.png` neben `leiste-handy-vorschau.png`: gleicher Inhalt. Abweichung 5: der heutige Dialograhmen steht am Handy unten.

**Kann-Sammlung** (für budgeting-h9c3w):
- Die Checkbox der Vorschau ist die native des Browsers wie in der Skizze. Ein DS-Baustein im Stil der Skizze wäre einheitlicher.
