---
id: budgeting-p3sk0
title: "Angepinnt ohne Meldung (R3): Anpinnen und Lösen im Kategorie-Detail ohne Toast, die Kategorie leuchtet auf der Übersicht im Bereich „Angepinnt“ kurz, ein Fehler beim Speichern steht am Ort"
status: backlog
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
  am PC entfällt der Toast ebenso (Abweichung, siehe unten).
- **Übersicht, Bereich „Angepinnt“:** eine seit dem letzten Besuch der Übersicht neu angepinnte Kategorie leuchtet beim
  nächsten Anzeigen der Übersicht kurz (Skizze `.flash-row`: Ring in Akzentfarbe um die Zeile). Ebenso nach Anpinnen im
  Sheet „Angepinnt“ der Übersicht die Zeile nach dem Schließen des Sheets. Eine gelöste verschwindet ohne Meldung.
- **Fehler beim Speichern** (`Overview.State.pinSaveNotice`, `pinsNotSavedText` „Nicht gespeichert: …“, heute Toast
  `src/Client/State.fs:969 bis 973`): Die Anzeige springt wie heute auf den gespeicherten Stand zurück. Der Satz steht:
  im Kategorie-Detail in der Zeile im Sheet (budgeting-gtrgk, am PC Toast wie heute), im Sheet „Angepinnt“ der
  Übersicht rot unter der Liste „Angepinnt (n)“, auf der Übersicht selbst rot im Bereich „Angepinnt“. Er bleibt bis zur
  nächsten Aktion.

## Funktionsabgleich
- Pin-Knopf im Detail, Sheet „Angepinnt“ mit Suche, Ordnen, Lösen, Tastatur am PC: **bleiben**.
- Toast „angepinnt / gelöst“: **gestrichen** am Handy (Roman, 2026-11-19); am PC siehe Abweichung.
- Rücksprung der Anzeige bei gescheitertem Speichern (README „Speichern der Pins“): **bleibt**.

## Abweichungen von der Skizze und vom Brief
- Am PC entfällt der Toast „angepinnt / gelöst“ ebenfalls. Grund: der Knopf zeigt das Ergebnis an derselben Stelle
  (Regel 1 von R3 „Ergebnis am Ort sichtbar“ gilt in allen Breiten); ein Toast meldete nichts Neues. Offene Frage an Roman.
- Das Leuchten auf der Übersicht kommt erst beim nächsten Anzeigen, nicht „sofort“: angepinnt wird im Budget, die
  Übersicht ist dann nicht zu sehen.

## Acceptance criteria
- [ ] `TogglePinFromInspector` (`src/Client/State.fs:980 bis 987`) gibt keinen Toast mehr aus; Test auf die
      App-Update-Funktion (keine Meldung, Pin-Zustand gewechselt).
- [ ] Reine Funktion für „neu angepinnt seit dem letzten Anzeigen“ in `src/Client/Components/Overview/` mit Tests
      (anpinnen, lösen, anpinnen und wieder lösen ergibt kein Leuchten, Reihenfolge ändern ergibt keins).
- [ ] Fehler beim Speichern an den drei Orten oben, nie als Toast unter 1024 px; Test auf `pinSaveNotice` bzw. ihren
      Nachfolger (Ort und Text).
- [ ] Leuchten mit `prefers-reduced-motion: reduce` ohne Animation; `[human-eye]` für das Leuchten selbst.
- [ ] `[flow:kategorie-anpinnen]` (`scripts/kategorie-anpinnen.test.mjs:34`) prüft nach dem Anpinnen: kein Toast im DOM, die
      Kategorie auf der Übersicht trägt die Markierung des Leuchtens.
- [ ] Sweep: `uebersicht-angepinnt` und `uebersicht-angepinnt-sheet` bleiben grün; neuer Zustand
      `uebersicht-angepinnt-fehler` (roter Satz im Bereich).
- [ ] README budgeting „Angepinnt“ und „Speichern der Pins“: Toast-Stellen im README_DELTA nachgezogen.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome: Übersicht mit leuchtender Kategorie), Abweichungen
      nur die in dieser Task genannten.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Notes
Abgespalten 2026-11-19 aus budgeting-gtrgk.

**Skizze:** `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/30-uebersicht-leiste.png`
(Essen im Bereich „Angepinnt“ mit Ring), Quelltext `.../scratchpad/vf5h6/skizze.src.html` Zeilen um 411 bis 430, CSS
`.flash-row` Zeile 270. Heute: `.../scratchpad/shots/vf5h6/heute-uebersicht-412.png`.

**Nicht hier:** die Leiste mit „+ Buchung“ auf der Übersicht (ersetzt `Overview.View.captureFab`, `src/Client/View.fs:223
bis 228`) und die Meldung „Abgerufen“ dort. Die Übersicht als Seite gehört zu ledger (ledger-s1weh, README ledger „Zu tun
(Übersicht)“); budgeting liefert nur den Bereich „Angepinnt“ (budgeting-w9yt4). Die Leiste ist derselbe Baustein wie im
Kassenbuch und gehört zu ledger-gwr9j, „Abgerufen“ zu banking-import-vwhfm.

**Offene Frage an Roman:** Toast „angepinnt / gelöst“ auch am PC weg? Empfehlung: ja, der Knopf zeigt das Ergebnis selbst.
