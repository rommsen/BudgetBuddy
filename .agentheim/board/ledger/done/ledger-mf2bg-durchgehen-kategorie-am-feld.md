---
id: ledger-mf2bg
title: "Durchgehen: Kategorie und Übertrag auf der Karte am Feld bestätigt (Feld leuchtet, „<Name> · <Betrag> verfügbar“ mit Rückgängig), ohne Toast"
status: done
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


## Outcome

Kategorie und Übertrag auf der Karte des Durchgehens ohne „Details“ melden sich jetzt ohne Toast am Feld (B3, R3 Punkt 1, ADR 0098). Das gilt am Handy und am PC gleich (Entscheidung Roman A). `./verify.sh` ergab im ersten Lauf `VERIFY: PASS` (dotnet 5450 grün, node 355 grün, Wächter grün).

**Modell** (`src/Client/Components/Ledger/Types.fs`)
- `ReviewSession` hat drei neue Felder:
  - `FieldNote: ReviewFieldNote option` mit `EntryId`, `Text`, `Available` (schon formatiert), `Undo: ToastActionKind`, `Flashing` und `Stamp`. Die Task nannte das Feld vorläufig `CategoryNote`, im Code heißt es `FieldNote`.
  - `Clock` und `LastStamp`.
- Neue Texte statt `reviewCategoryToastText` und `reviewTransferToastText`:
  - `reviewCategoryNoteText` liefert Name und Betrag getrennt, ohne Kategorie „Kategorie entfernt“.
  - `reviewTransferNoteText` ist „Gegenbuchung angelegt“.
- `ReviewLastKind` hat zwei neue Fälle: `LastCategoryChanged` und `LastTransferSet`.
- `ReviewRetry` hat drei neue Fälle: `RetryCategory`, `RetryTransfer` und `RetryFieldUndo`.
- Neu sind `ReviewUndoTarget` (`UndoAtField`, `UndoAtLine`) und die reine Funktion `reviewUndoTarget`. Sie wählt die Bestätigung am Feld, wenn deren `Stamp` jünger ist als `LastStamp` oder die Zeile kein Rückgängig anbietet. Sonst wählt sie die Zeile.
- Neue Nachrichten: `ReviewUndoField` und `ReviewFieldFlashEnded stamp`.

**Ablauf** (`State.fs`)
- Ob eine Antwort zur Karte gehört, erkennt der Client wie bisher an `detailNoteRoute` = `NoteOnReviewCard`. Dafür gibt es eigene Zweige in `EntryCategorySet`, `EntryCategoryRestored` und `EntryTransferSet`, jeweils für Ok und Error. Mit „Details“ (`NoteAtField`) und außerhalb des Durchgehens (`NoteInBar`) bleibt alles wie in ledger-p5y9s.
- Neue Helfer:
  - `postReviewFieldNote`: setzt die Bestätigung, das Feld leuchtet 700 ms (`detailNoteFlashMs`).
  - `reviewFieldNotInYnab`: gelbe Zeile ohne Knopf, weil das Rückgängig schon am Feld steht.
  - `reviewFieldFailed`: rote Zeile mit „Erneut“. Die Zeile der Buchung trägt weiter `CategoryCommandFailed`.
  - `reviewFieldRestored`: die Bestätigung verschwindet ohne Meldung. Nimmt YNAB die Rücknahme nicht, steht gelb „Rückgängig, nicht in YNAB“.
  - `clearReviewField`.
- `setReviewLast (Some …)` zählt `Clock` hoch.
- Wann die Bestätigung verschwindet: `showReviewCardAt` leert `FieldNote`. Ein neuer `StartSetEntryCategory` oder `StartSetEntryTransfer` ohne `undo` nimmt Bestätigung und Feld-Zeile dieser Buchung weg.
- `ReviewUndoField` schickt `detailNoteUndoMsg note.Undo`. Ein zweiter Tipp wird über `whenRowIdle` gesperrt.
- `KeyUndo` geht über `reviewUndoTarget`. `ReviewRetryLast` behandelt die neuen Retry-Fälle über `onCard`.
- Diese Toasts entfallen im Durchgehen: Kategorie gesetzt, „unverändert, aber nicht in YNAB“, Befehlsfehler, „zurückgesetzt“ und Fehler der Rücknahme. Beim Übertrag entfallen dieselben.

**DS**
- `ReviewCard.FieldState` (`FieldRest`, `FieldNeed`, `FieldFlash`) mit `ReviewCard.fieldIn`. `field` und `fieldNeeding` rufen `fieldIn` auf, ihre Signaturen bleiben gleich.
- `ReviewCard.fieldNote text available act` (Testid `review-field-note`):
  - Text 12 px in `text-muted`, der Betrag in `font-mono` und `text-secondary`.
  - „Rückgängig“ im Akzent, halbfett, mit 48 px Trefferfläche (`TouchTargets.alwaysMinHeight`).
  - `-my-3` hält die Zeile niedriger als 48 px. Die Trefferfläche reicht dadurch nur in die 12 px Abstand der Karte.
- Styleguide: Zeile **ReviewCard** in §5 nennt `fieldIn` und `fieldNote`. `/styleguide` zeigt in Abschnitt 29 „ReviewCard.fieldIn und ReviewCard.fieldNote“ mit Kategorie und Übertrag.
- View (`Ledger/View.fs`, `ReviewNormalCard`): die Zeile steht direkt unter dem Feld, vor „aus Bestellnummer“ und „Vorgeschlagen“. `reviewKeyFor` bietet Z über `reviewUndoTarget` an.

**Tests**
- Neue Liste in `src/Tests/LedgerReviewClientTests.fs`: „Durchgehen: Kategorie am Feld der Karte (ledger-mf2bg)“ mit 15 Fällen:
  - die Texte;
  - Kategorie mit Verfügbarem: Leuchten, Undo, kein Toast, keine Zeile unter dem Knopf;
  - Kategorie ohne bekanntes Verfügbares und entfernte Kategorie;
  - Übertrag;
  - das Leuchten endet nur für die eigene Nummer;
  - Kartenwechsel und neue Kategorie leeren die Bestätigung;
  - Rückgängig bei Kategorie und bei Übertrag, beide ohne Meldung;
  - YNAB-Warnung gelb, bei Kategorie und bei Übertrag;
  - Fehler rot mit „Erneut“ bei Kategorie, Übertrag und Rücknahme (Kategorie und Übertrag);
  - Z als reine Funktion (5 Fälle) und durch `update`;
  - Gegentest außerhalb des Durchgehens: `PostFeedback` mit Rückgängig bzw. rot.
- Der Test „die Kategorie auf der Karte meldet Name und Verfügbares“ prüft jetzt `FieldNote` und `NoOp` statt des Toasts.
- Gegenprobe: drei Zweige stillgelegt (Karten-Zweig in `EntryCategorySet` Ok und Error, Z über `ReviewUndoField`). Danach waren 9 Tests rot. Danach wiederhergestellt.
- Browser `[flow:durchgehen]`: nach der Kategorie aus dem Sheet prüft der Flow die Zeile am Feld mit „Rückgängig“ und dass die Leiste nichts zur Kategorie meldet. Die Prüfung der Leiste am Ende sucht jetzt auch nach „Kategorie“. Name und `FLOWS` sind unverändert.
- Sweep: neuer Zustand `durchgehen-kategorie-feld`.
  - Breiten 320, 412 und 1280. Karte `amazon`, ein Tipp auf „Bahn“. Der Mock gibt „Bahn“ 105,70 € verfügbar.
  - `primary` ist „Passt, weiter“, `reach` ist „Rückgängig“ am Feld.
  - Der Zustand prüft den Text „Bahn · … verfügbar“, dass kein Toast steht und dass die Leiste nichts zur Kategorie meldet.
  - Einzeln ohne Verletzung und ohne Ausnahme, im Gate grün.

**Seite neben der Skizze**

Eigene Bilder liegen unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/result-shots/ledger-mf2bg/`, die Skizze unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/`.
- Ansicht 06 (Kategorie aus Vorschlag): `06-kategorie-feld-412.png` (während des Leuchtens) und `06-kategorie-feld-412-nach-leuchten.png`, verglichen mit `06-kategorie-aus-vorschlag.png`.
  - Gleich: Rand und Ring des Felds im Akzent, „Bahn“ im Feld. Darunter „Bahn · 84,20 € verfügbar“ (Betrag in Mono) und „Rückgängig“ im Akzent. „Nebenkosten“ steht als übriger Vorschlag. Kein Toast, darunter der Wisch-Hinweis.
  - Unterschied 1: 12 px statt 12,5 px (in der Task genannt).
  - Unterschied 2: „aus Bestellnummer“ steht zwischen der Zeile und „Vorgeschlagen“, weil die Fixture `CategoryFromOrderId` behält. Die Skizze hat diese Zeile nicht, BB zeigt sie heute schon.
- PC 1280: `06-kategorie-feld-1280.png` und `06-kategorie-feld-1280-nach-leuchten.png`. Dieselbe Zeile in der Spalte, unten rechts kein Toast, die Tastenleiste nennt „Z rückgängig“.
- Offen [human-eye]: ob das Leuchten am echten Handy wahrnehmbar und nicht grell ist. Es nutzt dieselbe Animation wie das Detail (ledger-p5y9s).

**README-Gate**: `npm run check:readme-delta -- --bc ledger --delta …` hat nichts abgelehnt. „Ganz nachlesen“ hat keine Einträge.

**Abweichungen und Grenzen**
- Warnung nach Kategorie oder Übertrag: die Zeile unter dem Knopf hat keinen Knopf, weil „Rückgängig“ schon am Feld steht. Z nimmt dann die Kategorie zurück.
- Scheitert die Rücknahme, bleibt die Bestätigung am Feld mit „Rückgängig“ stehen. Die Zeile unter dem Knopf zeigt rot „Rückgängig nicht möglich“ mit „Erneut“. Beide schicken dieselbe Rücknahme.
- Kommt die Antwort erst nach dem Kartenwechsel, meldet die Leiste wie bisher (`NoteInBar`).

**Kann-Sammlung ledger**
- Zwei README-Sätze sind seit ledger-p5y9s und ledger-pz5wv veraltet. Diese Task ändert sie nicht:
  - im Bullet **Regel aus der Buchung**: „Der Toast „Kategorie geändert“ mit „Rückgängig“ bleibt wie vorher.“
  - der Schlusssatz der **Meldungsleiste im Kassenbuch** („… melden bis ledger-p5y9s … noch als Toast“).
- Der Mock `setEntryCategory` in `mockReview` setzt immer den Namen „Lebensmittel“, auch bei einer anderen Kategorie.
