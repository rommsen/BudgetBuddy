---
id: budgeting-p3sk0
title: "Angepinnt ohne Meldung (R3): Anpinnen und Lösen im Kategorie-Detail ohne Toast, die Kategorie leuchtet auf der Übersicht im Bereich „Angepinnt“ kurz, ein Fehler beim Speichern steht am Ort"
status: done
type: feature
context: budgeting
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, design-system-gkded]
blocks: []
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, budgeting-w9yt4]
---

## Why
Skizze ledger-vf5h6 Abschnitt 3, „Übersicht: Abrufen fertig, Kategorie angepinnt“: „„Angepinnt“ braucht keine Meldung, die
Kategorie steht sofort im Bereich „Angepinnt“ und leuchtet kurz.“ Tabelle „Übrige Seiten“: „Pin am Ort; Fehler beim Pin
am Ort“. Regel R3 (design-system-ytgnc). Abgespalten aus budgeting-gtrgk.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben).

- **Anpinnen/Lösen im Kategorie-Detail** (Budget, Pin-Knopf im Kopf von Sheet und Panel, `InspectorView.pinButton`,
  `Button.pinToggle`): heute Toast „Essen angepinnt“ / „Essen gelöst“ (`src/Client/State.fs:986`,
  `TogglePinFromInspector`, `Overview.Types.pinToggleNotice`). Mit R3: kein Text, der Knopf wechselt sichtbar seinen
  Zustand (ist schon heute so) und ist die Bestätigung. Gilt in allen Breiten: es ist Ergebnis am Ort (ytgnc Regel 1),
  am PC entfällt der Toast ebenso (Entscheidung Roman 2026-11-19, ADR 0098).
- **Übersicht, Bereich „Angepinnt“:** eine seit dem letzten Besuch der Übersicht neu angepinnte Kategorie leuchtet beim
  nächsten Anzeigen der Übersicht kurz (Skizze `.flash-row`: Ring in Akzentfarbe um die Zeile). Ebenso nach Anpinnen im
  Sheet „Angepinnt“ der Übersicht die Zeile nach dem Schließen des Sheets. Eine gelöste verschwindet ohne Meldung.
- **Fehler beim Speichern** (`Overview.State.pinSaveNotice`, `pinsNotSavedText` „Nicht gespeichert: …“, heute Toast
  `src/Client/State.fs:969 bis 973`): Die Anzeige springt wie heute auf den gespeicherten Stand zurück. Der Satz steht:
  im Kategorie-Detail in der Zeile im Sheet (budgeting-gtrgk, am PC in derselben Zeile im Panel), im Sheet „Angepinnt“ der
  Übersicht rot unter der Liste „Angepinnt (n)“, auf der Übersicht selbst rot im Bereich „Angepinnt“. Er bleibt bis zur
  nächsten Aktion.

## Funktionsabgleich
- Pin-Knopf im Detail, Sheet „Angepinnt“ mit Suche, Ordnen, Lösen, Tastatur am PC: **bleiben**.
- Toast „angepinnt / gelöst“: **gestrichen** am Handy und am PC (Roman, 2026-11-19).
- Rücksprung der Anzeige bei gescheitertem Speichern (README „Speichern der Pins“): **bleibt**.

## Abweichungen von der Skizze und vom Brief
- Am PC entfällt der Toast „angepinnt / gelöst“ ebenfalls. Grund: der Knopf zeigt das Ergebnis an derselben Stelle
  (Regel 1 von R3 „Ergebnis am Ort sichtbar“ gilt in allen Breiten); ein Toast meldete nichts Neues. Von Roman am
  2026-11-19 so entschieden.
- Das Leuchten auf der Übersicht kommt erst beim nächsten Anzeigen, nicht „sofort“: angepinnt wird im Budget, die
  Übersicht ist dann nicht zu sehen.

## Acceptance criteria
- [ ] `TogglePinFromInspector` (`src/Client/State.fs:980 bis 987`) gibt keinen Toast mehr aus; Test auf die
      App-Update-Funktion (keine Meldung, Pin-Zustand gewechselt).
- [ ] Reine Funktion für „neu angepinnt seit dem letzten Anzeigen“ in `src/Client/Components/Overview/` mit Tests
      (anpinnen, lösen, anpinnen und wieder lösen ergibt kein Leuchten, Reihenfolge ändern ergibt keins).
- [ ] Fehler beim Speichern an den drei Orten oben, nie als Toast, auch am PC nicht; Test auf `pinSaveNotice` bzw. ihren
      Nachfolger (Ort und Text).
- [ ] Leuchten mit `prefers-reduced-motion: reduce` ohne Animation; `[human-eye]` für das Leuchten selbst.
- [ ] `[flow:kategorie-anpinnen]` (`scripts/kategorie-anpinnen.test.mjs:34`) prüft nach dem Anpinnen: kein Toast im DOM, die
      Kategorie auf der Übersicht trägt die Markierung des Leuchtens.
- [ ] Sweep: `uebersicht-angepinnt` und `uebersicht-angepinnt-sheet` bleiben grün; neuer Zustand
      `uebersicht-angepinnt-fehler` (roter Satz im Bereich).
- [ ] README budgeting „Angepinnt“ und „Speichern der Pins“: Toast-Stellen im README_DELTA nachgezogen.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome: Übersicht mit leuchtender Kategorie; dazu PC
      1280 nach dem Anpinnen im Panel, ohne Toast), Abweichungen nur die in dieser Task genannten.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Entscheidungen Roman (2026-11-19)
- **Toast „angepinnt / gelöst“ auch am PC weg?** Ja, der Knopf zeigt das Ergebnis selbst (ADR 0098: Pin-Knopf ist
  Rückmeldung am Ort). Folge: `TogglePinFromInspector` gibt in keiner Breite einen Toast aus.

## Notes
Abgespalten 2026-11-19 aus budgeting-gtrgk.

**Skizze:** `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/30-uebersicht-leiste.png`
(Essen im Bereich „Angepinnt“ mit Ring), Quelltext `.../scratchpad/vf5h6/skizze.src.html` Zeilen um 411 bis 430, CSS
`.flash-row` Zeile 270. Heute: `.../scratchpad/shots/vf5h6/heute-uebersicht-412.png`.

**Nicht hier:** die Leiste mit „+ Buchung“ auf der Übersicht (ersetzt `Overview.View.captureFab`, `src/Client/View.fs:223
bis 228`) und die Meldung „Abgerufen“ dort. Die Übersicht als Seite gehört zu ledger (ledger-s1weh, README ledger „Zu tun
(Übersicht)“); budgeting liefert nur den Bereich „Angepinnt“ (budgeting-w9yt4). Die Leiste ist derselbe Baustein wie im
Kassenbuch und gehört zu ledger-gwr9j, „Abgerufen“ zu banking-import-vwhfm.

## Verifier note (iteration 1)
REASONS: Der Stand nach dem Merge mit main baut nicht: `src/Client/State.fs(975,42): error FS0764: No assignment given for field 'Undo' of type 'Components.Budget.Types.SheetLine'`. budgeting-y1kkr hat `SheetLine` um `Undo: SentMove option` erweitert; der Pin-Fehler im `OverviewMsg`-Zweig und zwei Records in `src/Tests/OverviewPinsTests.fs` (Zeilen 562, 574) setzen es nicht. Dadurch liefen weder die neuen Tests noch Sweep und Flow auf dem gemeinsamen Stand.
SUGGESTED_FIX: `Undo = None` an allen drei Stellen; der Test bei 562 prüft weiter, dass ein Pin-Fehler kein „Rückgängig“ anbietet. Dann `./verify.sh` bis `VERIFY: PASS`.
ITERATION_HINT: likely-fixable


## Outcome

Regel R3 (ADR 0098) für die Pins umgesetzt. `./verify.sh`: `VERIFY: PASS` (dotnet 5292 grün, node 354 grün, 16 übersprungen wie vorher: Integrationstests hinter `RUN_INTEGRATION_TESTS`).

Iteration 2 (Verifier-FAIL: der Build war nach dem Merge mit budgeting-y1kkr rot): `Undo = None` ist im Pin-Fehler (`src/Client/State.fs`, Zweig `OverviewMsg`) und in den zwei Records in `OverviewPinsTests.fs` gesetzt, die Zeile zum Pin-Fehler bietet also kein „Rückgängig“ an. Ein Tipp auf den Pin-Knopf beendet wie jede Aktion im Sheet auch eine grüne Zeile mit „Rückgängig“. Auf dem gemergten Stand ergibt `./verify.sh` `VERIFY: PASS` (dotnet 5329 grün, node 354 grün, Sweep samt `regeln-panel` im ersten Lauf grün). Die Bilder sind neu aufgenommen (`iter2-*.png` im selben Ordner, gleiches Ergebnis: Ring um Kleidung, roter Satz im Bereich, am PC und am Handy kein Toast und keine Meldung in der Leiste).

**Pin-Knopf ohne Meldung**
- `src/Client/State.fs`: `TogglePinFromInspector of categoryId` (der Name ist entfallen, er war nur für den Toast da) schaltet nur noch um, kein `ShowToast`, auch am PC nicht. Der Tipp beendet eine stehende Zeile im Sheet (`Budget.SheetLine = None`), weil er die nächste Aktion im Sheet ist.
- `Budget.Types.InspectorPin.Toggle` ist jetzt `Guid -> unit` (`InspectorView.pinButton`, `View.fs`). `Overview.Types.pinToggleNotice` ist entfernt.

**Leuchten auf der Übersicht**
- `Overview/Types.fs`: reine Funktion `newlyPinned seen now` (was jetzt angepinnt ist und damals nicht). Im Modell neu: `PinsSeen` (Stand beim letzten Anzeigen, `None` bis zum ersten Laden), `PinFlash {Ids; Count}`, `PinError`. Neue Nachricht `PinFlashEnded`.
- `Overview/State.fs`: `notePinsShown` setzt beim `Entered` und bei `PinSheetClosed` das Leuchten und den Vergleich. Das erste `PinsLoaded` setzt nur den Vergleich (was schon angepinnt war, leuchtet nicht). Der Zeitgeber ist `pinFlashCmd`, 900 ms (`pinFlashMs`), wie `availableFlashCmd` aus budgeting-gtrgk. Ein älteres Ende nimmt kein neueres ab.
- `Overview/View.fs`: `pinnedRow` setzt `Feedback.flash` mit Schlüssel je Leuchten plus `rounded-[14px]`, Markierung `data-flash="true"`. Reduced motion über das CSS aus design-system-ytgnc: der Ring steht ohne Bewegung, bis die Klasse abgenommen wird. [human-eye] für das Leuchten selbst.

**Speicherfehler am Ort**
- `pinStep` setzt bei Rücksprung `PinError = Some "Nicht gespeichert: <Grund>"`. Beendet wird er durch `endsPinError`: Umschalten, Ordnen, Enter auf der Markierung, Sheet öffnen oder schließen, Suche. Die Pfeile der Markierung beenden ihn nicht.
- Ort für die Übersicht: `pinErrorShown` → `PinErrorInSheet` (rot unter „Angepinnt (n)“, `data-testid="pin-sheet-not-saved"`; ist nach dem Rücksprung nichts mehr angepinnt, direkt unter der Suche) oder `PinErrorInBlock` (rot unter der Karte im Bereich „Angepinnt“, `overview-pins-not-saved`). Darstellung `ErrorDisplay.inline'`.
- Wurzel (`OverviewMsg`): Ist beim Scheitern das Kategorie-Detail offen (Seite Budget und `Budget.State.sheetOpen`), setzt sie `Budget.SheetLine` rot mit dem Satz (budgeting-gtrgk, im Sheet und im Panel). Den Satz auf der Übersicht leert sie dann. Sonst bleibt er in `Overview.PinError`. Es gibt nie einen Toast. `pinSaveNotice` bleibt als Messfunktion für die Wurzel.
- Abweichung (bewusst): `Entered` leert `PinError` nicht. Scheitert das Speichern, nachdem das Detail schon zu ist, steht der Satz beim nächsten Besuch im Bereich „Angepinnt“ und geht nicht verloren. Er endet mit der nächsten Aktion an den Pins.

**Tests** (`src/Tests/OverviewPinsTests.fs`, 19 neu, 2 umgestellt)
- „neu seit dem letzten Anzeigen“ (4): anpinnen; lösen; anpinnen und wieder lösen; umordnen.
- „neu angepinnte leuchten“ (7): erstes Laden; Detail → nächstes Betreten, danach nicht mehr; gelöst; Sheet → nach dem Schließen; nur umgeordnet; älteres und aktuelles Ende; Zeitgeber per `CmdTiming.expectAfter`, ohne Leuchten keiner.
- „Speicherfehler am Ort“ (4) und „…, Wurzel“ (4): Ort und Text im Block, im Sheet und in der Zeile des Details. Lebensdauer. Neuere Änderung ergibt keinen Satz. Detail zu → Übersicht. Pin-Knopf beendet die Zeile. Kein `ShowToast`/`ShowToastWithAction`/`ShowFeedback`, `Feedback = None`.
- Der Root-Test des Pin-Knopfs prüft jetzt keine Meldung und leeren Meldungsplatz.
- Gegenprobe (lokal, wiederhergestellt): `newlyPinned` ohne Differenz, Wurzel nie ins Detail und Toast im Pin-Knopf zurück ergaben 14 von 72 Tests der Liste „Angepinnt“ rot.
- Browser: `[flow:kategorie-anpinnen]` prüft nach dem Anpinnen, dass kein Toast im DOM (`.toast-layer > *`) und keine Meldung in der Leiste steht, auf der Übersicht `overview-pinned[data-flash="true"]` für Kleidung und danach `data-flash="false"`.
- Sweep: neuer Zustand `uebersicht-angepinnt-fehler` (320, 412, 1280). Die Antwort von `setPinnedCategories` kommt 1 s verzögert, „Fertig“ schließt vorher, der rote Satz steht im Bereich. `uebersicht-angepinnt` und `uebersicht-angepinnt-sheet` bleiben grün.
- `uebersicht-meldung` (design-system-gkded) umgebaut, weil der Speicherfehler der Pins keine Meldung in der Leiste mehr erzeugt. Die Meldung kommt jetzt vom gescheiterten Abbrechen eines Abrufs (`cancelSync` mit 500). Am PC kommt die Antwort 1,5 s verzögert, damit der Dialog „Angepinnt“ offen ist und der Toast darüber liegt. Der Gegenbeweis „Toast hinter dem offenen Dialog“ in `layout-invariants.test.mjs` ist weiter grün.

**README_DELTA**: geprüft mit `npm run check:readme-delta -- --bc budgeting` (Exit 0, „Ganz nachlesen: keine Einträge“). Ersetzt: „Inspektor“ (Pin-Knopf ohne Meldung), „Angepinnt“ (Leuchten, Fehler am Ort), „Speichern der Pins“ (Toast → drei Orte).

**Skizzenvergleich** (Bilder in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/result-shots/budgeting-p3sk0/`)
- `uebersicht-leuchtet-412.png` neben `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/30-uebersicht-leiste.png`: „Kleidung“ im Bereich „Angepinnt“ mit Ring im Akzent um die Zeile, aufgenommen mit reduced motion. Unterschiede: BB zeigt Balken und „ausgegeben“-Zeile (BB-Stand, budgeting-w9yt4). Die Leiste unten mit „Abgerufen“ gehört zu ledger-gwr9j und banking-import-vwhfm, nicht zu dieser Task.
- `uebersicht-leuchtet-1280.png`: dasselbe am PC.
- `budget-angepinnt-1280.png`: PC 1280 nach dem Anpinnen im Panel. Der Pin-Knopf ist gefüllt im Akzent, kein Toast. `budget-angepinnt-412.png`: am Handy, keine Meldung in der Leiste.
- `uebersicht-angepinnt-fehler-412-gescrollt.png` und `uebersicht-angepinnt-fehler-1280.png`: rotes ⓧ „Nicht gespeichert: YNAB antwortet nicht“ unter der Karte „Angepinnt“, kein Toast, keine Leiste mit Meldung. Die Skizze hat dazu kein Bild, nur „Fehler beim Pin am Ort“.
- Abweichungen nur die in der Task genannten: kein Toast am PC, das Leuchten kommt beim nächsten Anzeigen.

**Kann-Sammlung budgeting**
- Ist der Monat beim ersten Betreten der Übersicht noch nicht geladen und dauert das Laden länger als 0,9 s, ist das Leuchten vorbei, bevor die Zeile erscheint. Danach bleibt der Monat beim Betreten stehen (`reloading`), dort tritt es nicht auf.
- Pins, die auf einem anderen Gerät angepinnt wurden, leuchten beim nächsten Betreten nach einem Neu-Lesen ebenfalls (Vergleich mit dem zuletzt gezeigten Stand).
