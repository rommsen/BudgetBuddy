---
id: budgeting-gtrgk
title: "Budget-Seite und Sheet „Geld“ am Handy ohne Toast (R3): Ergebnis und Fehler in der Zeile über dem Knopf, „Verfügbar“ leuchtet kurz, Hinweis „nicht neu gelesen“ unter „Zu verteilen“ mit „Neu laden“"
status: todo
type: feature
context: budgeting
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, design-system-gkded]
blocks: [budgeting-y1kkr]
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, budgeting-nfrds, budgeting-c5w2n]
---

## Why
Regel R3 (design-system-ytgnc), Skizze ledger-vf5h6 Abschnitt 3. Roman am 2026-11-19: am Handy kein Toast. Heute liegt nach
einer Verschiebung der Toast „50,00 € von Essen nach Reise“ über dem Sheet, der gelbe Toast „Beträge nicht neu gelesen …“
verschwindet nach fünf Sekunden, obwohl der Monat weiter veraltet ist.

Beim Verfeinern geteilt (2026-11-19):
- diese Task: Budget-Seite, Inspektor-Sheet (Tab Geld, Pause, Historie), Monatserster;
- budgeting-y1kkr: „Rückgängig“ nach einer Verschiebung (Domäne und Client, braucht die Zeile dieser Task);
- budgeting-sx9v5: Targets (Tab Target, Seite Targets prüfen) und Kategorien verwalten;
- budgeting-p3sk0: Angepinnt ohne Meldung. Die Leiste mit „+ Buchung“ auf der Übersicht gehört nicht hierher (siehe Notes).

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben), Ansichten „Budget: Geld verschieben im Sheet“ und „Budget: Stand nicht neu gelesen“ und die
Zeilen „Budget, Sheet Geld“ und „Budget, Seite“ der Tabelle „Übrige Seiten“. Pfade der Screenshots in den Notes.

Alle Meldungen gehen über den Meldungsweg aus design-system-ytgnc (DS-Modul vorläufig `Feedback`; Name, Nachricht und
Orte nimmt der Worker aus der fertigen Task). Eine Meldung steht an dem Ort, den diese Task nennt, am PC (ab 1024 px)
genauso wie am Handy: die Zeile im Sheet steht am PC im Panel über dem Hauptknopf, „Verfügbar“ leuchtet dort, der
Hinweis „nicht neu gelesen“ steht auf der Seite, alles ohne zusätzlichen Toast (Entscheidung Roman 2026-11-19, ADR
0098). Nur Meldungen ohne Ort (am Handy die Leiste, etwa bei geschlossenem Sheet) kommen am PC als Toast unten rechts,
höchstens einer, 5 s, mit ✕.

### 1. Zeile im Sheet „Geld“ (Ort: Zeile über dem Hauptknopf)
Skizze `.mk-msg`: eine Zeile, links das Zeichen, dann der Text fett (Kürzung mit „…“, nie über zwei Zeilen), rechts eine
Textaktion in Akzentfarbe. Mindesthöhe wie eine Textaktion. Steht über dem Hauptknopf des Modus (Zuweisen: der
Fehlbetrag-Knopf `assign-missing`, Holen/Abgeben: der Knopf `transfer-submit` bzw. der Knopf des Modus), nie darüber
gelegt (ytgnc Regel 2).

| Heute (Toast, Datei:Zeile) | Mit R3 in der Zeile |
|---|---|
| `moveToast` Erfolg: „50,00 € von Essen nach Reise“ (`Moves.fs:392`, `moveSummaryText`) | ✓ grün (Rolle ok) „50,00 € nach Reise“ bei Abgeben, „50,00 € von Reise“ bei Holen, aus Sicht der gewählten Kategorie (Skizze; „von Reise“ bei Holen: Entscheidung Roman); bei Zu verteilen „50,00 € nach Zu verteilen“ / „50,00 € von Zu verteilen“. Rechts vorerst keine Aktion; „Rückgängig“ bringt budgeting-y1kkr. |
| Fehlbetrag-Knopf Erfolg (heute auch `moveToast`, „12,30 € von Zu verteilen nach Essen“) | ✓ „12,30 € von Zu verteilen“ |
| `moveToast` „In YNAB geändert, nichts überschrieben“ (`Moves.fs:391`, Warnung) | „!“ gelb (Rolle Warnung) „In YNAB geändert, nichts überschrieben“, keine Aktion |
| `failureToast` „Nicht vollständig verschoben: …“ / „Nicht zugewiesen: …“ mit „Erneut senden“ (`Moves.fs:380`, aus `moveToast` und `assignToast`) | rot (Rolle Fehler) „Nicht vollständig verschoben: <Grund>“ bzw. „Nicht zugewiesen: <Grund>“, rechts „Erneut senden“ (gleiche Wirkung wie heute: `RetryBudgetMove`) |
| `assignToast` ohne offenen Bericht „Nicht zugewiesen: …“ (`Moves.fs:399`) | rot, ohne Aktion |
| `applyAssignAnswered` Error „Nicht zugewiesen: …“ (`Moves.fs:442`) | rot, ohne Aktion |
| `applyAssignCallLost` „Ob zugewiesen wurde, ist unklar: …“ (`Moves.fs:449`) | rot, ohne Aktion, Text unverändert |
| `applyMoveAnswered` Error ohne Tab „Nicht verschoben: …“ (`Moves.fs:685`) | Sheet offen: rot in der Zeile; Sheet zu: Leiste (ytgnc Regel 3), am PC Toast |
| `applyMoveCallLost` „Ob verschoben wurde, ist unklar: …“ (`Moves.fs:690`) | rot, ohne Aktion, Text unverändert |
| `applyMoveRetried` Error „Erneut senden gescheitert: …“ (`Moves.fs:733`) | rot, rechts „Erneut senden“ |
| `startMove` „Eine Verschiebung läuft noch. Gleich noch einmal versuchen.“ (`State.fs:810`, `moveBlockedText`) | gelb, ohne Aktion |
| `CoverMissing` „Eingabe verworfen: der Fehlbetrag wurde zugewiesen“ (`State.fs:460`, `coverDroppedEntryText`) | gelb, ohne Aktion |
| `leaveToast` „Zugewiesen nicht gespeichert: keine gültige Rechnung“ (`Moves.fs:332`, `invalidEntryText`) | Sheet bleibt offen (Kategorie- oder Monatswechsel): gelb in der Zeile; Sheet geschlossen: Leiste, am PC Toast |
| `droppedEntryToast` „Zugewiesen nicht gespeichert: die Kategorie gibt es nicht mehr“ (`State.fs:826`) | wie `leaveToast` |

Erfolgreiches Zuweisen über das Feld zeigt wie heute keinen Text (die Zahl ist die Antwort, `assignToast`); es gibt nur
das Leuchten.

**Verfügbar leuchtet kurz**: Nach jedem Bericht mit mindestens einer geschriebenen Zeile für die gewählte Kategorie
(Zuweisen, Fehlbetrag-Knopf, Holen, Abgeben, Erneut senden) leuchtet der Wert „Verfügbar“ im Tab Geld kurz (Skizze
`.flash-val`: Ring in Akzentfarbe um den Wert). Das Leuchten kommt aus ytgnc (Regel 1, „Feld leuchtet kurz“), nicht neu
erfunden; bei `prefers-reduced-motion: reduce` ohne Bewegung (der Ring darf stehen und ohne Übergang verschwinden).

**Lebensdauer** (ytgnc): eine Meldung zugleich; sie bleibt bis zur nächsten Aktion im Sheet (Tipp auf einen Knopf, eine
neue Eingabe), bis zum Kategorie- oder Monatswechsel oder bis das Sheet schließt. Der Wechsel zwischen Zuweisen, Holen
und Abgeben beendet sie nicht (wie das Rückgängig-Angebot in budgeting-y1kkr, ADR 0099 Entscheidung b). Kein ✕, kein Zeitlimit.
Fehler bleiben ebenso bis zur nächsten Aktion.

### 2. Hinweis „nicht neu gelesen“ (Ort: unter „Zu verteilen“, auf der Seite)
Skizze `.mk-hint`: Fläche in Warnfarbe (gelb, schwach hinterlegt), links „! Beträge nicht neu gelesen, sie können veraltet
sein.“, rechts die Textaktion „Neu laden“. Steht unter dem Seitenkopf mit „Zu verteilen“, über Monatserster/Suche.

| Heute (Toast) | Mit R3 |
|---|---|
| `applyReloaded` „Beträge nicht neu gelesen, sie können veraltet sein: <Grund>“ (`Month.fs:211`) | Hinweis „! Beträge nicht neu gelesen, sie können veraltet sein.“ Der Grund steht in einer zweiten Zeile kleiner darunter (heute im Toast, er geht nicht verloren). |
| `applyFrameLoaded` „Monate nicht neu gelesen, sie können veraltet sein: <Grund>“ (`Month.fs:247`) | derselbe Platz, „! Monate nicht neu gelesen, sie können veraltet sein.“ |
| `applyCategoryHistoryReloaded` „Historie nicht neu gelesen, sie kann veraltet sein: <Grund>“ (`Inspector.fs:268`) | oben im Tab Historie des Inspektors, gleiche Form, „Neu laden“ liest die Historie neu (Abweichung, siehe unten) |

- Der Hinweis ist Zustand im Modell (nicht die Meldung von ytgnc): er bleibt bei Tab-, Kategorie-, Modus- und
  Sheet-Wechsel stehen, bis ein Neu-Lesen gelingt (dann ist er weg) oder ein Monatswechsel den Monat ohnehin neu lädt.
  Muster wie die alten Kontostände im Kassenbuch (ledger-pfhdq).
- „Neu laden“ startet das stille Neu-Lesen (`rereadMonth` bzw. Rahmen, für Historie das stille Nachladen der Historie);
  währenddessen steht die Aktion als laufend (nicht doppelt tippbar). Scheitert es wieder, bleibt der Hinweis mit dem
  neuen Grund.
- Stehen Beträge und Monate zugleich aus, steht ein Hinweis („Beträge und Monate nicht neu gelesen …“), nicht zwei.

### 3. Übrige Toasts der Budget-Seite und des Sheets
| Heute (Toast) | Mit R3 |
|---|---|
| `TargetPauseAnswered` Error, Pause-Knopf nicht im offenen Tab: `pauseRejectedAwayText` (`State.fs:554`) | Sheet offen: Zeile im Sheet (rot); sonst Leiste, am PC Toast |
| `TargetPauseCallLost`, nicht im Tab: `pauseCallLostText` (`State.fs:565`) | wie oben |
| `MonthStartHideAnswered` Error „Ausblenden nicht gespeichert …“ (`State.fs:516`) | Leiste (gelb), die Budget-Seite hat kein „+ Buchung“, die Leiste steht nur solange (ytgnc); am PC Toast |
| Offene Verschiebung (Hinweis mit „Erneut senden“, `moveRetries`, `InspectorView.fs:347`) | **bleibt** wie heute (Zustand, kein Toast) |

## Funktionsabgleich (heutige Funktionen der betroffenen Stellen)
- Toast nach Verschieben, Fehlbetrag-Knopf, Fehlern: **ändert sich** (Zeile im Sheet, Tabelle oben). Toasts am Handy:
  gestrichen (Roman, 2026-11-19).
- „Erneut senden“ am Toast: **ändert sich** (in der roten Zeile), Wirkung gleich.
- Hinweis Offene Verschiebung mit „Erneut senden“ im festen Bereich des Sheets und auf der Seite: **bleibt**. Er steht
  nicht in der Skizze, BB kann ihn heute (README „Offene Verschiebung“), er überdauert Tab- und Monatswechsel.
- Markierung „noch nicht in YNAB“ an der Kategorie mit „Erneut senden“ (`notInYnabNotice`): **bleibt**.
- Kein Erfolgs-Toast beim Zuweisen im Feld: **bleibt**.
- Sperre des Fehlbetrag-Knopfs bei veraltetem Monat (`canCover`): **bleibt**.
- PC (ab 1024 px, Panel): Toast nach Verschieben und Fehlern im Panel **ändert sich** (Zeile im Panel über dem
  Hauptknopf wie am Handy, „Verfügbar“ leuchtet); Toast unten rechts **bleibt** nur für Meldungen ohne Ort (Panel zu,
  Ausblenden des Monatsersten), höchstens einer, 5 s, mit ✕.
- Rückgängig nach Verschiebung: neu, aber in budgeting-y1kkr.

## Abweichungen von der Skizze
- Text der Erfolgszeile: die Skizze zeigt nur „50,00 € nach Reise“ (Abgeben). Für Holen und Zu verteilen leitet diese Task
  „von …“ und „… Zu verteilen“ ab; Grund: dieselbe Sicht aus der gewählten Kategorie, die Skizze zeigt nur einen Fall.
  Für Holen von Roman bestätigt („50,00 € von Reise“).
- Hinweis „nicht neu gelesen“ auch am PC (Skizze zeigt nur das Handy; Entscheidung Roman 2026-11-19). Grund: es ist ein
  Zustand, kein Ergebnis einer Aktion; ein Toast, der nach fünf Sekunden geht, während der Monat veraltet bleibt, ist
  heute schon falsch.
- Zeile über dem Hauptknopf auch im Panel am PC (Skizze zeigt nur das Handy). Grund: ADR 0098, Rückmeldung am Ort auch am
  PC ohne Toast.
- „Historie nicht neu gelesen“ steht im Tab Historie statt unter „Zu verteilen“ (Skizze nennt sie in der Tabelle unter
  „Budget, Seite“). Grund: „Neu laden“ dort liest die Historie, unter „Zu verteilen“ wäre unklar, was neu geladen wird,
  und am Handy verdeckt das Sheet den Seitenkopf nicht, aber der Blick liegt im Sheet.
- Grund des Fehlers in einer zweiten, kleineren Zeile unter dem Hinweis (Skizze zeigt keinen). Grund: heute steht er im
  Toast, er soll nicht verloren gehen.

## Acceptance criteria
- [ ] Keine Stelle in `src/Client/Components/Budget/Moves.fs`, `Month.fs`, `Inspector.fs`, `State.fs` (Budget) ruft
      mehr einen Toast für eine Meldung mit Ort; jede Zeile der Tabellen in Teil 1 bis 3 steht an ihrem Ort mit dem
      genannten Text, am Handy und am PC (Panel) gleich. Wächter: Expecto-Test je reiner Funktion (Ort und Text je
      Fall), siehe Tests.
- [ ] Ab 1024 px kommen nur die Meldungen ohne Ort (Sheet bzw. Panel zu, Ausblenden des Monatsersten) als Toast unten
      rechts, höchstens einer zugleich, 5 s, mit ✕ (Gegenprobe im Test des Meldungswegs aus ytgnc oder hier).
- [ ] Zeile im Sheet: Zeichen und Farbe je Rolle (ok, Warnung, Fehler), Text mit „…“ gekürzt, „Erneut senden“ nur bei
      offenem Bericht, steht über dem Hauptknopf; Tests auf die reine Funktion, die aus Bericht und Modus die Zeile
      macht (Text aus Sicht der Kategorie für Holen, Abgeben, Fehlbetrag, Zu verteilen).
- [ ] Lebensdauer: die Zeile verschwindet bei der nächsten Aktion im Sheet, bei Kategorie- und Monatswechsel und beim
      Schließen, nicht beim Moduswechsel; Test je Übergang auf der reinen Update-Funktion.
- [ ] „Verfügbar“ leuchtet nach einem Bericht mit geschriebener Zeile kurz, nicht nach einem Fehler; mit
      `prefers-reduced-motion: reduce` ohne Animation. `[human-eye]` für das Leuchten selbst.
- [ ] Hinweis „nicht neu gelesen“ für Beträge und Monate unter „Zu verteilen“, für Historie im Tab Historie; bleibt bis
      ein Neu-Lesen gelingt, „Neu laden“ startet es, doppelter Tipp startet keinen zweiten Aufruf. Tests auf
      `applyReloaded`, `applyFrameLoaded`, `applyCategoryHistoryReloaded` und das Ende des Hinweises.
- [ ] Am Handy liegt der Hauptknopf des Tabs Geld mit Zeile weiter ohne Scrollen im Sheet, bei allen `HANDY_WIDTHS`,
      auch bei offener Tastatur: neue Sweep-Zustände in `scripts/layout-states.mjs` (`budget-geld-ergebnis`: nach
      Abgeben mit Zeile; `budget-geld-fehler`: rote Zeile mit „Erneut senden“; `budget-nicht-neu-gelesen`: Hinweis unter
      „Zu verteilen“), alle mit `primary` auf dem Hauptknopf bzw. dem Kopf; `budget-geld-ergebnis` und
      `budget-nicht-neu-gelesen` zusätzlich bei 1280 (Panel, kein Toast). Die Ausnahme bei 320 px aus
      `budget-geld-zuweisen` gilt nicht weiter, als sie heute gilt.
- [ ] `[flow:geld-verschieben]` (`scripts/move-money.test.mjs:128`) prüft zusätzlich die Zeile „✓ 5,00 € nach …“ im
      Sheet und dass kein Toast im DOM steht. `[flow:verschiebung-erneut-senden]` (`move-money.test.mjs:175`) heißt
      danach sinngemäß „… die Zeile bietet Erneut senden an …“ und klickt „Erneut senden“ in der Zeile statt im Toast;
      die Prüfung „Hinweis in jedem Tab“ bleibt. Fixtures in `scripts/*.test.mjs` mitziehen, falls ein Record in
      `src/Shared/` ein Feld bekommt.
- [ ] Bestehende Sweep-Zustände `budget`, `budget-geld-zuweisen`, `budget-geld-abgeben` bleiben grün.
- [ ] README budgeting: „Offene Verschiebung“ (Satz „der Toast läuft ab, der Hinweis nicht“) und „Veralteter Monat“ (Satz
      „Der Knopf verwirft eine offene Eingabe und meldet das per Warn-Toast“) im README_DELTA nachgezogen.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome: Sheet Geld nach Abgeben, Sheet mit Fehler,
      Seite mit Hinweis; dazu PC 1280 mit Panel, Zeile über dem Hauptknopf und Hinweis, ohne Toast), Abweichungen nur
      die in dieser Task genannten.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Entscheidungen Roman (2026-11-19)
1. **Hinweis „nicht neu gelesen“ auch am PC statt Toast?** Ja, ein Zustand gehört auf die Seite, egal wie breit. Folge:
   der Hinweis steht in allen Breiten unter „Zu verteilen“ bzw. im Tab Historie, kein Toast.
2. **Erfolg nach Holen:** „50,00 € von Reise“, aus Sicht der Kategorie. Folge: Holen und Abgeben haben dieselbe Sicht.
3. **Rückmeldung am Ort am PC** (ADR 0098): Rückmeldungen am Ort stehen auch am PC ohne zusätzlichen Toast. Folge: die
   Zeile über dem Hauptknopf und das Leuchten gelten auch im Panel am PC, der Toast nur für Meldungen ohne Ort.

## Notes
Captured 2026-11-19 aus ledger-vf5h6, verfeinert 2026-11-19 (Teilung siehe Why).

**Skizze (Scratchpad dieser Sitzung, `S` = `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):**
- `S/vf5h6/skizze-shots/31-budget-sheet-geld.png` (Sheet Geld mit Zeile „✓ 50,00 € nach Reise · Rückgängig“; das
  Rückgängig kommt erst mit budgeting-y1kkr)
- `S/vf5h6/skizze-shots/32-budget-stand-hinweis.png` (Hinweis unter „Zu verteilen“)
- `S/vf5h6/skizze.src.html`, Zeilen um 433 bis 470 (Markup), CSS `.mk-msg`, `.mk-hint`, `.flash-val`, `.undo`
  (Zeilen 98, 281 bis 296); Tabelle „Übrige Seiten“ Zeilen um 509 bis 518.
- Heute: `S/shots/vf5h6/heute-budget-412.png`, `S/shots/vf5h6/heute-budget-geld-zuweisen-412.png`.

**Code:** Toasts in `src/Client/Components/Budget/Moves.fs` (`leaveToast` 332, `failureToast` 380, `moveToast` 389 bis
392, `assignToast` 399/400, `applyAssignAnswered` 442, `applyAssignCallLost` 449, `applyMoveAnswered` 685,
`applyMoveCallLost` 690, `applyMoveRetried` 733), `State.fs` (460, 516, 554, 565, 810, 826; Weiterleitung
`Categories.ShowToast` 435 bleibt für budgeting-sx9v5), `Month.fs` (211, 247), `Inspector.fs` (268). Budget-`ExternalMsg`
`ShowToast`/`ShowToastWithAction` in `Types.fs:613`, App-Weiterleitung `src/Client/State.fs:1043`. Ansicht: `MovesView.fs`
(`moneyMode`, `assignMode`, `transferMode`, `missingButton`, `moveRetries`), `View.fs` (`head`, `belowHead`),
`MonthView.fs:85` („Zu verteilen“).

**Tests (Doktrin):** reine Funktionen in `src/Tests` (Budget-Client-Tests, wo `moveToast`/`applyReloaded` heute getestet
werden), Browser nur für Ort und Fluss.

**Nicht hier:** Leiste mit „+ Buchung“ auf der Übersicht und „Abgerufen“ dort (ledger-gwr9j bzw. banking-import-vwhfm),
Pins (budgeting-p3sk0), Targets und Kategorien verwalten (budgeting-sx9v5), Rückgängig (budgeting-y1kkr).
