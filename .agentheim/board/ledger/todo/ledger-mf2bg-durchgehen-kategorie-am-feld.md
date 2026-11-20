---
id: ledger-mf2bg
title: "Durchgehen: Kategorie und Übertrag auf der Karte am Feld bestätigt (Feld leuchtet, „<Name> · <Betrag> verfügbar“ mit Rückgängig), ohne Toast"
status: todo
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [ledger-pz5wv]
blocks: []
tags: [design-exploration, durchgehen, toast, kategorie, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, ledger-v6s7c]
---

## Why
Roman am 2026-11-19: der Toast nach „Bahn“ aus den Vorschlägen liegt genau auf „Passt, weiter“ (Skizze Anhang, heute
`heute-toast-412.png`). Entscheidung B3 und Regel R3 Punkt 1 („Ergebnis am Ort sichtbar: die Stelle bestätigt selbst“),
Freigabe in ledger-vf5h6. Abgeteilt aus ledger-pz5wv beim Verfeinern.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben), Ansicht 06; Verhalten in `skizze.src.html`, Funktionen `pick`, `catUndo`, `cardInner` (`catNote`),
CSS `.field.flash`, `.cap`, `.cap .avail`, `.cap .undo`.

- **Kategorie auf der Karte gewählt** (Vorschlag `ReviewPickSuggestion`, Auswahl über Feld oder K): das Feld leuchtet kurz
  (Rand und Ring im Akzent, etwa 700 ms, Dauer aus §6), der Wert steht im Feld. Direkt unter dem Feld, vor „Vorgeschlagen“,
  eine Zeile 12 px in `text-muted`: „<Name> · <Betrag> verfügbar“, Betrag in Mono in `text-secondary`, rechts daneben
  „Rückgängig“ im Akzent, halbfett, Trefferfläche 48 px (§7.15). Ist das Verfügbare unbekannt: nur „<Name>“ mit
  Rückgängig. Kategorie entfernt: „Kategorie entfernt“ mit Rückgängig. Quelle des Betrags wie heute
  (`reviewCategoryToastText`, `categoryOfId ... Available`).
- **Übertrag auf der Karte** (Konto als Ziel): das Feld zeigt „→ <Konto>“ wie heute (`reviewCategoryValue`), darunter
  „Gegenbuchung angelegt“ mit Rückgängig (Skizze Anhang: „wie Kategorie, darunter „Gegenbuchung angelegt““).
- Der gewählte Vorschlag verschwindet aus „Vorgeschlagen“ (heute so).
- Die Zeile bleibt, bis die Karte wechselt oder die Kategorie wieder geändert wird.
- **Rückgängig** dort: `undoKindFor entryId outcome` (`UndoCategory` → `StartUndoCategory`, `UndoTransfer` →
  `StartSetEntryTransfer (…, true)`); danach steht der alte Wert im Feld, die Zeile verschwindet, keine Meldung
  („… zurückgesetzt“ entfällt im Durchgehen).
- **YNAB nimmt die Kategorie nicht** (`CategorySendFailed`): die Zeile am Feld bleibt, die Zeile unter „Passt, weiter“
  (ledger-pz5wv) zeigt gelb „! Kategorie geändert, nicht in YNAB · Grund“ (bzw. „Übertrag gesetzt, nicht in YNAB“).
- **Fehler** (`EntryCategorySet Error`, `EntryTransferSet Error`, Rücknahme scheitert): das Feld behält den alten Wert,
  rot in der Zeile unter „Passt, weiter“: „! Kategorie nicht geändert“ / „! Übertrag nicht gesetzt“ / „! Rückgängig nicht
  möglich“ · Grund · „Erneut“ (schickt denselben Befehl noch einmal). Die Karte bleibt.
- **Taste Z am PC** nimmt die jüngste Aktion mit Rückgängig zurück: die Kategorie am Feld, wenn sie jünger ist als die
  Zeile unter dem Knopf, sonst die Zeile (reine Funktion, Vorschlag `reviewUndoTarget`).
- `prefers-reduced-motion`: das Feld leuchtet ohne Übergang (oder gar nicht), die Zeile darunter erscheint trotzdem.
- Toasts, die im Durchgehen entfallen (`src/Client/Components/Ledger/State.fs`), solange `model.Review` gesetzt ist und
  die Buchung die Karte ist (`isReviewCard`): `EntryCategorySet` 2567, 2577, 2578, 2585; `EntryCategoryRestored` 2612,
  2614, 2621; `EntryTransferSet` 2652, 2663, 2664, 2672. Das gilt am Handy und am PC gleich (Entscheidung Roman
  2026-11-19, wie Frage 1 in ledger-pz5wv). Außerhalb des Durchgehens und im Detail-Sheet bleibt es wie heute bzw. wie
  ledger-p5y9s es baut. `reviewCategoryToastText` und `reviewTransferToastText` (`Types.fs` 1323, 1330)
  werden zu Texten der Zeile am Feld.
- Modell: Feld in `ReviewSession`, etwa `CategoryNote: { EntryId; Text; Undo }`, beim Kartenwechsel
  (`showReviewCardAt`) geleert.
- DS: die Zeile am Feld ist eine Variante des Bausteins aus design-system-ytgnc („Rückgängig am Feld“, R3 Punkt 1) oder
  `ReviewCard.caption` mit Knopf; Eintrag in §5 und Beispiel auf `/styleguide`. Das Leuchten des Felds als Zustand von
  `ReviewCard.field` (gleiches Muster soll ledger-gwr9j im Detail nutzen, der Conductor gleicht ab).

### Funktionsabgleich
| Funktion heute | Stand |
|---|---|
| Kategorie über Feld, Vorschläge, K | bleibt |
| Toast „Kategorie: <Name> · <Betrag> verfügbar“ mit Rückgängig | ändert sich: Zeile am Feld, Feld leuchtet |
| Toast „Übertrag nach … · Gegenbuchung angelegt“ mit Rückgängig | ändert sich: Zeile am Feld |
| Toast „Kategorie zurückgesetzt“ nach Rückgängig | entfällt (Wert steht sichtbar wieder da) |
| Toasts „Kategorie nicht geändert: …“, „Kategorie nicht in YNAB: …“ | ändert sich: Zeile unter dem Knopf, rot bzw. gelb |
| Angebot „Regel anlegen?“ nach der Änderung | bleibt (im Detail) |

### Abweichungen von der Skizze (mit Grund)
- Zeile 12 px statt 12,5 px, Farben aus Tokens.
- Warnung und Fehler in der Zeile unter dem Knopf statt am Feld: Skizze Anhang („rot in der Zeile unter dem Knopf“,
  „Zeile unter dem Knopf, gelb“), und der Projektregel „Fehler zu einer Eingabe am Feld“ (`.claude/rules/task-ui-vorgaben.md`)
  steht das nicht entgegen: die Wahl einer Kategorie ist eine Aktion mit Aufruf, keine Eingabe mit Validierung; das Feld
  zeigt unverändert den alten Wert.

## Acceptance criteria
- [ ] Nach einer Kategorie auf der Karte: Feld leuchtet kurz, darunter „<Name> · <Betrag> verfügbar“ mit Rückgängig;
      Übertrag mit „Gegenbuchung angelegt“; kein Toast, am Handy und am PC. Reducer-Tests in `src/Tests/LedgerReviewClientTests.fs`
      (Kategorie, ohne Verfügbares, entfernt, Übertrag); der Test „die Kategorie auf der Karte meldet Name und Verfügbares“
      (Zeile 555) prüft jetzt die Zeile am Feld statt des Toasts.
- [ ] Rückgängig am Feld stellt Kategorie bzw. Übertrag zurück, ohne Meldung; Test je Weg.
- [ ] Warnung gelb und Fehler rot mit „Erneut“ in der Zeile unter dem Knopf; Test je Fall.
- [ ] Z nimmt die jüngste Aktion zurück (reine Funktion mit Test).
- [ ] Außerhalb des Durchgehens ändert diese Task die Rückmeldung der Kategorie nicht (Gegentest; dort gilt
      ledger-p5y9s).
- [ ] Sweep: Zustand `durchgehen-kategorie-feld` (nach Tipp auf einen Vorschlag, 320/412/1280), `reach` auf „Rückgängig“
      am Feld; „Passt, weiter“ bleibt ohne Scrollen sichtbar; Invarianten grün ohne Ausnahme.
- [ ] Seite neben der Skizze verglichen: Screenshot bei 412 px im Outcome für 06 (Kategorie aus Vorschlag), dazu PC 1280;
      mit der Zeile am Feld, ohne Toast; Abweichungen nur die genannten.
- [ ] Das kurze Leuchten des Felds ist am Handy wahrnehmbar und nicht grell. [human-eye]

## Entscheidungen Roman (2026-11-19)
- **Am PC:** Zeile am Feld und Zeile unter „Passt, weiter“ auch am PC, im Durchgehen kein Toast (wie ledger-pz5wv,
  Frage 1; ADR 0098). Folge: Kriterien und Sweep bei 1280 prüfen die Zeile am Feld ohne Toast.

## Notes
Abgeteilt 2026-11-19 aus ledger-pz5wv beim Verfeinern. Braucht die Zeile unter dem Knopf aus ledger-pz5wv (Warnung, Fehler,
Z).

**Skizze** (Basis `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- `.../scratchpad/vf5h6/skizze-shots/06-kategorie-aus-vorschlag.png`
- Quelltext `.../scratchpad/vf5h6/skizze.src.html`, ganze Skizze `.../scratchpad/vf5h6/durchgehen-handy.html`
- heute: `.../scratchpad/shots/vf5h6/heute-toast-412.png` (Toast auf „Passt, weiter“)

**Am Code:** `src/Client/Components/Ledger/State.fs` (`ReviewPickSuggestion` 4590, `ReviewOpenCategory` 4584,
`EntryCategorySet` 2556 ff., `StartUndoCategory` 2587 ff., `EntryTransferSet` 2643 ff., `undoKindFor` 717),
`Types.fs` (`reviewCategoryToastText`, `reviewTransferToastText`), `View.fs` (`reviewNormalCard` Feld um 5215,
`reviewCategoryValue` 5027), `src/Client/DesignSystem/ReviewCard.fs` (`field`, `caption`).
