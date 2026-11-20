---
id: banking-import-ns4re
title: "Abrufen ohne Toast (R3): Meldungen im Sheet, solange es offen ist; Ergebnis und Fehler nach geschlossenem Sheet in der Meldungsleiste"
status: doing
type: feature
context: banking-import
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, design-system-gkded]
blocks: []
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, banking-import-d0fey, banking-import-vwhfm]
---

## Why
Regel R3 (design-system-ytgnc, von Roman am 2026-11-19 freigegeben): am Handy kein Toast. Das Sheet „Abrufen“ zeigt TAN,
Lauf, Ergebnis und Fehler schon selbst. Zwei Toasts bleiben: das Ergebnis, wenn Roman das Sheet während des Abrufs
geschlossen hat, und ein gescheitertes Abbrechen. Skizze, Tabelle „Übrige Seiten“, Zeile „Abrufen (Sheet)“: „im Sheet,
solange es offen ist; sonst Leiste unten“. Abgeteilt aus banking-import-vwhfm.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, freigegeben am
2026-11-19). Für die Leiste gilt der Baustein aus design-system-ytgnc; Form der Meldung wie Skizze 30 (Übersicht) und
26 (Fehler): links zweizeilig, oben die Aktion (nie gekürzt), darunter Grund oder Ergebnis (gekürzt mit „…“).

- **Sheet offen:** unverändert. TAN (`TanScreen`), Lauf, Ergebnis (`DoneScreen`, „Jetzt durchgehen“), Fehler
  (`ErrorScreen`) stehen im Sheet. Für TAN und Fehler öffnet es sich wieder, auch wenn Roman es geschlossen hat (heute,
  `surfaced`).
- **Ergebnis bei geschlossenem Sheet** (heute Toast „Abruf fertig: 6 neue Buchungen“, `resultToast`): Meldung in der
  Leiste, Erfolg. Aktion „✓ Abgerufen“, darunter `resultTitle` („6 neue Buchungen“, „1 neue Buchung“, „Nichts Neues“).
  Ohne neue Buchung, aber mit Umsätzen in der Zukunft: darunter „Nichts Neues, 2 folgen später“ (heute steht dort der
  ganze Satz; die zweite Zeile ist am Handy zu schmal dafür). Rechts kein Knopf; „Jetzt durchgehen“ bleibt im Sheet und
  in „Zu tun“ der Übersicht.
- **Abbrechen gescheitert** (heute Toast mit `syncErrorText`, das Sheet ist dann schon zu): Meldung in der Leiste,
  Fehler. Aktion „Nicht abgebrochen“, darunter der Grund. Kein „Erneut“: die Sitzung ist im Modell schon verworfen, ein
  neuer Abruf liest sie wieder (ADR 0071).
- Die Meldung folgt den Regeln der Leiste aus ytgnc (eine Meldung, bis zur nächsten Aktion, kein ✕). Sie erscheint auf
  der Seite, auf der Roman gerade ist (Übersicht mit „+ Buchung“, Kassenbuch, Budget, Einstellungen).
- Am PC (ab 1024 px, Dialog statt Sheet) gilt R3: solange der Dialog offen ist, stehen die Meldungen im Dialog. Ergebnis
  und Fehler bei geschlossenem Dialog sind Meldungen ohne Ort, der Meldungsweg aus ytgnc zeigt sie dort als Toast unten
  rechts, höchstens einer, geht nach 5 s, mit ✕ (Entscheidung Roman 2026-11-19, ADR 0098). Texte wie am Handy.

### Jeder heutige Toast und sein neuer Platz
| Heute | Text heute | Neu |
|---|---|---|
| `src/Client/State.fs:570` (`FetchFinished (Some …)`) aus `Fetch/State.fs:110` (`settled`) | „Abruf fertig: <Titel>[. <Satz>]“ | Leiste: „✓ Abgerufen“ / Titel |
| `Fetch/State.fs:221` (`SyncCancelled (Error …)`), weitergereicht `State.fs:565` | `syncErrorText` | Leiste rot: „Nicht abgebrochen“ / Grund |

### Funktionsabgleich Sheet „Abrufen“
- Abrufen aus der Übersicht (Handy Sheet, PC Dialog), Push-TAN mit „Ich habe bestätigt“, Lauf, Ergebnis mit Zählern und
  „Jetzt durchgehen“, Fehler, Nachlesen nach verlorener Antwort, Abbrechen: bleibt.
- Nach dem Abruf laden Zu tun, Zähler, Konten und eine offene Kassenbuch-Liste neu (`fetchExternalCmd`): bleibt.
- Ergebnis bei geschlossenem Sheet und Fehler beim Abbrechen: ändern sich (Toast wird Leiste).
- Toast am Handy: gestrichen (Roman, 2026-11-19, R3).

### Am Code
- `src/Client/Components/Fetch/State.fs`: `resultToast` wird eine reine Funktion für Aktion und Zeile (etwa
  `resultMessage : FetchCounts -> string * string`), `settled`, `SyncCancelled (Error …)`.
- `src/Client/Components/Fetch/Types.fs`: `ExternalMsg.ShowToast` und `FetchFinished of resultToast: string option`
  tragen künftig eine Meldung für den Meldungsweg aus ytgnc statt Toast-Text.
- `src/Client/State.fs:560` bis `:575` (`fetchExternalCmd`): statt `ShowToast` der Meldungsweg aus ytgnc.
- `scripts/layout-states.mjs`: neuer Zustand `abrufen-fertig-leiste` (Sheet während des Abrufs geschlossen, Ergebnis in
  der Leiste) bei 320, 412 und 1280 px; Leiste verdeckt nichts, zweite Zeile gekürzt statt umgebrochen.

### Tests (Heimat-Ebene nach `.claude/rules/test-doctrine.md`)
- .NET `src/Tests/FetchSheetTests.fs`: Zeilen 309, 316, 320, 324 (heute `resultToast`) auf die neue Meldung ziehen;
  `resultMessage` je Fall (neu, nichts, nur Zukunft, Einzahl); `SyncCancelled (Error …)` liefert die Fehlermeldung.
  Die Erwartungen „ohne Toast bei offenem Sheet“ (Zeilen 133 bis 281) bleiben.
- Browser: `[flow:abrufen]` in `scripts/sync-start.test.mjs` bleibt grün; kein neuer Flow.

## Acceptance criteria
- [ ] `grep -rn "ShowToast\|resultToast" src/Client/Components/Fetch/` ist leer; `fetchExternalCmd` ruft keinen Toast
      mehr direkt.
- [ ] Sheet während des Abrufs geschlossen: am Handy steht das Ergebnis in der Leiste als „✓ Abgerufen“ / „<Titel>“, am
      PC als Toast unten rechts, höchstens einer, 5 s, mit ✕ (Sweep `abrufen-fertig-leiste` bei 320, 412, 1280).
- [ ] Gescheitertes Abbrechen steht rot in der Leiste: „Nicht abgebrochen“ / Grund (.NET-Test).
- [ ] TAN und Fehler holen das Sheet wie heute nach vorn; nach dem Abruf laden dieselben Teile neu wie heute
      (bestehende Tests grün, `[flow:abrufen]` grün).
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome, dazu PC 1280), Abweichungen nur die in dieser
      Task genannten (verkürzte zweite Zeile bei „nur Zukunft“, kein Knopf rechts).
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Entscheidungen Roman (2026-11-19)
- **PC-Toast:** Ergebnis und Fehler bei geschlossenem Sheet bzw. Dialog haben keinen Ort, am PC bleiben sie Toast unten
  rechts: höchstens einer, 5 s, mit ✕, wie heute (ADR 0098). Folge: diese Task ändert am PC nur die Texte.

## Notes
Abgeteilt 2026-11-19 aus banking-import-vwhfm beim Verfeinern: anderer Baustein (Leiste statt Rückmeldung am Ort) und
andere Dateien.

Skizze und Screenshots (`…` = `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- Skizze: `…/vf5h6/durchgehen-handy.html`, Quelltext `…/vf5h6/skizze.src.html` (Tabelle „Übrige Seiten“ Zeile 512,
  Leiste mit Meldung Zeilen 385 bis 400 und 1050 bis 1060)
- Leiste mit Ergebnis: `…/vf5h6/skizze-shots/30-uebersicht-leiste.png`; Fehler in der Leiste:
  `…/vf5h6/skizze-shots/26-kassenbuch-abrufen-fehler.png` (dort Spiegel-Abruf im Kassenbuch, gleiche Form)
- Heute: kein eigener Screenshot; Toast-Lage wie `…/shots/vf5h6/heute-toast-412.png`

Abhängigkeit: Meldungsleiste und Meldungsweg (Handy Leiste, PC Toast) aus design-system-ytgnc. Auf der Übersicht trägt die
Leiste „+ Buchung“ erst mit budgeting-gtrgk; vorher zeigt ytgnc die Leiste dort nur mit Meldung. Abgrenzung: „Abrufen“
im Kassenbuch (Spiegel-Lauf, Skizze 24 bis 26) ist ledger-gwr9j, nicht diese Task.
