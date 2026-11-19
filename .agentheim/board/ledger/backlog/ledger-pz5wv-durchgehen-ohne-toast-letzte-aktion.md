---
id: ledger-pz5wv
title: "Durchgehen ohne Toast (B3): letzte Aktion unter „Passt, weiter“ mit Rückgängig für jede Aktion, Fehler rot an derselben Stelle"
status: backlog
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, design-system-gkded, ledger-s2cg3, ledger-gwr9j]
blocks: [ledger-mf2bg]
tags: [design-exploration, durchgehen, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, ledger-th36h, ledger-v6s7c, ledger-nc708]
---

## Why
Roman am 2026-11-19: der Toast der vorigen Aktion liegt im Durchgehen auf „Passt, weiter“, man muss warten.
Entscheidung B3 und Regel R3 (design-system-ytgnc), Freigabe in ledger-vf5h6. Die Bestätigung der Kategorie am Feld ist
in ledger-mf2bg abgeteilt (baut auf dieser Zeile auf).

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben). Worker können sie nicht öffnen; Dateien und Screenshots stehen unter „Notes“. Verhalten in
`skizze.src.html`, Funktionen `leave`, `undo`, `confirm`, `footHTML`, `lastInner`, CSS `.ph-foot`, `.txt`, `.undo`.

### 1. Die Zeile der letzten Aktion (Skizze 07, 08, 09, 10)
Platz ist die Zeile `review-foot` unter „Passt, weiter“ aus ledger-s2cg3 (feste Höhe, einzeilig). Bis zur ersten Aktion
steht dort der Wisch-Hinweis; nach einer Aktion steht dort diese, bis zur nächsten Aktion oder bis man die Seite verlässt
(kein ✕, kein Zeitlimit). Nach „Rückgängig“ steht wieder der Hinweis (Skizze `undo`: `last = null`).

Aufbau links nach rechts: Zeichen (✓ in `success`, ! in `attention` bei Warnung, ! in `error` bei Fehler), die Aktion
halbfett in `text-primary` (bei Fehler in `error`), danach der Payee bzw. der Grund in `text-secondary`, rechts
„Rückgängig“ bzw. „Erneut“ im Akzent, halbfett. Die Aktion wird nie gekürzt, nur der zweite Teil endet mit „…“
(Skizze 08: „✓ Passt REWE Markt GmbH Filiale …“). Schrift 12 px wie die Zeile aus ledger-s2cg3 (§7.14). „Rückgängig“ und
„Erneut“ haben 48 px Trefferfläche (§7.15). `role="status"` für ok und Warnung, `role="alert"` für Fehler.

Gebaut als Variante des DS-Bausteins aus design-system-ytgnc (Modul vorläufig `Feedback`, Name dort festgelegt),
etwa `Feedback.line` mit den Zuständen ok, Warnung, Fehler. Liefert ytgnc keine einzeilige Variante, ergänzt diese Task sie
dort (Eintrag in §5, Beispiel auf `/styleguide`). Die reine Funktion für Zeichen, Aktion und zweiten Teil kommt aus
`Types.fs` (Vorschlag `reviewLastLine : ReviewLast -> ...`) mit Tests.

Texte (Aktion · zweiter Teil · Knopf):

| Auslöser | Zeile |
|---|---|
| „Passt“ (Knopf, Enter, Wisch) | ✓ **Passt** · Payee · Rückgängig |
| „Passt“, YNAB nimmt den Prüfvermerk nicht | ! **Passt, nicht in YNAB** · Grund · Rückgängig (gelb, Frage 2) |
| „Später“ (Knopf, S, Wisch) | ✓ **Später** · Payee · Rückgängig |
| „Lena fragen“ (Knopf, L) | ✓ **Lena gefragt** · Payee · Rückgängig |
| „Lena fragen“, Flag nicht in YNAB | ! **Lena gefragt, nicht in YNAB** · Grund · Rückgängig |
| „Ist doppelt, verwerfen“ | ✓ **Verworfen** · Payee · Rückgängig |
| „Ist neu“ | ✓ **Als neu übernommen** · Payee (ohne Rückgängig, Frage 3) |
| „Ist neu“, nicht in YNAB | ! **Als neu übernommen, nicht in YNAB** · Grund |
| Memo auf der Karte gespeichert / entfernt (bis ledger-wg42w es streicht) | ✓ **Memo gespeichert** bzw. **Memo entfernt** · Payee · Rückgängig; Warnung „Memo gespeichert, nicht in YNAB“ · Grund |
| Payee auf der Karte geändert (Frage 4) | ✓ **Payee geändert** · neuer Payee · Rückgängig; Warnung „Payee geändert, nicht in YNAB“ · Grund |
| Befehl scheitert | ! **Nicht geprüft** / **Nicht übernommen** / **Nicht verworfen** / **Flag nicht geändert** / **Memo nicht geändert** / **Payee nicht geändert** / **Rückgängig nicht möglich** · Grund · Erneut (rot) |

„Rückfrage zurücknehmen“ schreibt keine Zeile: die Karte bleibt, der Knopf wechselt sichtbar zu „Lena fragen“ und ist
selbst das Rückgängig (R3 Punkt 1, wie im Kassenbuch-Detail der Skizze). Die Zeile behält die vorige Aktion.

Gleiche Aktionen werden im Durchgehen nicht zusammengefasst (Skizze 08 zeigt nach zwei „Passt“ nur das letzte). Abweichung
von der allgemeinen R3-Regel „3 geprüft“ mit Grund: Rückgängig holt eine Karte zurück; drei auf einmal zurückzuholen
wäre ein Sprung, den man nicht sieht. Der Conductor gleicht das mit der ADR aus design-system-ytgnc ab.

### 2. Rückgängig (neu für Passt und Später)
Rückgängig gilt nur für die letzte Aktion der Zeile (eine Stufe, wie die Skizze). Die Karte kommt von der Seite zurück, zu
der sie ging (Skizze `enter-back-l`/`enter-back-r`, unter `prefers-reduced-motion` ohne Bewegung), „i von n“ und
Fortschritt stehen wieder auf ihrer Stelle (`Index`). Eine Karte, die zwischendurch auf der aktuellen Karte begonnen
wurde (Kategorie, Payee), bleibt mit ihrem neuen Stand; offene Eingaben auf der aktuellen Karte halten die Karte fest wie
heute bei Lena (`reviewCardEditing`).

- **Passt rückgängig:** derselbe Weg wie im Kassenbuch (ledger-th36h): `StartUndoReviewMark (entryId, outcome.Previous)`
  ruft `restoreReviewMark` mit dem Vorzustand aus `ReviewConfirmed`'s `ReviewMarkOutcome.Previous`. Der vorhandene Weg
  reicht für Server und Liste (`applyUndoReviewMark`, `reinsertSwipedAway`, Zähler „Zu prüfen“), er kennt aber das
  Durchgehen nicht. Dazu kommt: die Karte über `returnToReviewCard entryId` zurückholen, die Zeile leeren, Fehler und
  YNAB-Warnung in die Zeile statt Toast. Falle: `showReviewCardAt` liest die Karte neu (`RunReviewRefresh`), und
  `ReviewEntryRefreshed` überspringt sie über `reviewSkipsEntry`, solange der Server sie noch als geprüft meldet. Die Karte
  kommt sofort zurück (wie die Skizze); solange die Rücknahme für sie läuft (`rowBusy ReviewMarkLock`), überspringt das
  Neulesen sie nicht, und nach `ReviewMarkRestored Ok` steht sie mit `NeedsReview` da. Scheitert die Rücknahme, bleibt die
  Karte stehen und die Zeile zeigt rot „Rückgängig nicht möglich“ · Grund · Erneut (Erneut schickt die Rücknahme noch einmal).
- **Später rückgängig:** kein Aufruf, `returnToReviewCard entryId`; die Buchung war nie geändert.
- **Lena rückgängig:** vorhandenes `StartUndoFlag` (holt die Karte schon zurück, ledger-v6s7c); der Toast
  `flagRestoredToastText` entfällt im Durchgehen.
- **Verworfen rückgängig:** vorhandenes `StartRestoreDiscardedDuplicate`; die Toasts `reviewRestoredText` entfallen im
  Durchgehen (außerhalb bleibt der Toast bis ledger-gwr9j).
- **Memo / Payee rückgängig:** vorhandene `StartUndoMemo` / `StartUndoPayee`; die Karte steht noch da.
- **Taste Z am PC:** nimmt die letzte Aktion der Zeile zurück, auch in der Endansicht; ohne Rückgängig nichts.
  `reviewKeyAction` bekommt den Fall `KeyUndo` und die Angabe, ob ein Rückgängig angeboten wird (die Endansicht hat heute
  außer Esc keine Taste); `reviewKeys` bekommt „Z rückgängig“. Nicht aus Eingabefeldern, nicht mit Strg/Meta/Alt (wie
  `reviewKeyFor`).
- **Endansicht (Skizze 13):** steht eine letzte Aktion mit Rückgängig, steht unter „Zur Übersicht“ der Knopf
  „Letzte rückgängig“ (Knopf ohne Füllung nach §5, die Hauptaktion bleibt „Zur Übersicht“); er holt die letzte Karte
  zurück. War die letzte Aktion eine Warnung, steht zusätzlich ihre gelbe Zeile darunter (Abweichung: die Skizze zeigt den
  Fall nicht, ohne Zeile ginge die Warnung verloren). Überschrift „Alles geprüft“ und Text bleiben (Abgleich der Skizze:
  „Endansicht gleich, bleibt“).

Modell: neues Feld in `ReviewSession`, etwa `Last: ReviewLast option` (Art, `EntryId`, Payee, Ton ok/Warnung/Fehler,
Rückgängig und Erneut als Beschreibung wie `ToastActionKind`, keine Funktion im Modell). `reviewStarting` setzt es leer,
`ReviewLeft` verwirft es mit der Sitzung.

### 3. Fehler (Skizze 10)
Scheitert ein Befehl der Karte, bleibt die Karte unverändert (bei „Passt“ federt sie zurück, ledger-s2cg3; bei „Lena fragen“
holt `LenaFlagSet Error` sie wie heute zurück, ledger-nc708), und die Zeile zeigt rot den Fehler mit „Erneut“. „Erneut“
schickt denselben Befehl für dieselbe Buchung noch einmal (Passt: `ReviewConfirm` auf der zurückgekehrten Karte; Ist neu,
Verwerfen, Lena, Memo, Payee, Rückgängig entsprechend). Der Fehler steht nicht auf der Karte (dort müsste man scrollen).
Die Zeilenzustände des Kassenbuchs (`ReviewMarkRows`, `FlagRows`, `MemoRows`, …) bleiben wie heute gesetzt.

### 4. Toasts, die im Durchgehen entfallen
Gilt, solange `model.Review` gesetzt ist; außerhalb des Durchgehens bleibt jeder Toast unverändert (das Kassenbuch baut
ledger-gwr9j um). Die gemeinsamen Handler (Prüfvermerk, Flag, Memo, Payee) brauchen dafür einen Zweig fürs Durchgehen.
`src/Client/Components/Ledger/State.fs`:
- `ReviewConfirmed`: 4548 (Warnung), 4560 (Fehler)
- `ReviewConfirmedNew`: 4630, 4631, 4636
- `ReviewDiscarded`: 4656 (mit Rückgängig), 4664
- `DiscardedDuplicateRestored`: 4690 (im Durchgehen), 4699
- `LenaFlagSet`: 2775, 2776, 2788; `FlagRestored`: 2813, 2815, 2821
- `ReviewMarkRestored` (neu aus dem Durchgehen erreicht): 2472, 2479
- `MemoSet` auf der Karte: um 2924 (`reviewMemoToastText`), `MemoSet Error` 2935 ff., `StartUndoMemo`-Antwort
- `PayeeSet` auf der Karte: um 3274, Fehler und Rücknahme dazu
`reviewConfirmedNewText`, `reviewDiscardedText`, `reviewRestoredText`, `reviewMemoToastText` in `Types.fs` entfallen oder
werden zu Zeilentexten, wo sie noch gebraucht werden. Aktionen im Detail-Sheet über der Karte („Details“: Memo, Payee,
Betrag, Aufteilung, Flag, Prüfvermerk, Löschen) sind nicht Teil dieser Task: dort gilt Rückgängig am Feld nach
ledger-gwr9j (dasselbe Detail wie im Kassenbuch). Kategorie und Übertrag auf der Karte: ledger-mf2bg.

### 5. Am PC
Frage 1. Empfohlen und hier als Kriterium: die Zeile gilt auch am PC (unter „Passt, weiter“ in der Spalte), im Durchgehen
gibt es auch am PC keinen Toast. Gründe: dieselbe Rückmeldung an derselben Stelle wie die Taste Z, ein Weg statt zwei, der
Toast unten rechts liegt weit weg von der Spalte. Entscheidet Roman anders, behalten die Handler am PC ihre Toasts
(höchstens einer, unten rechts, R3) und nur das Handy nutzt die Zeile.

### Funktionsabgleich (Seite Durchgehen)
| Funktion heute | Stand |
|---|---|
| Toast nach „Passt“ nur bei YNAB-Warnung, Fehler-Toast | ändert sich: Zeile ✓ / gelb / rot |
| Rückgängig nach „Passt“, „Später“ | neu |
| Toast „Lena gefragt“ mit Rückgängig | ändert sich: Zeile |
| „Rückfrage zurücknehmen“ mit Toast und Rückgängig | ändert sich: Knopf wechselt, keine Zeile |
| „Ist neu“: Toast „Als neu übernommen, geht nach YNAB“ | ändert sich: Zeile, Text „Als neu übernommen“ |
| „Verwerfen“: Toast mit Rückgängig, danach Toast „Steht wieder zur Entscheidung“ | ändert sich: Zeile mit Rückgängig; die Karte steht sichtbar wieder da |
| Memo auf der Karte: Toast mit Rückgängig | ändert sich: Zeile (bis ledger-wg42w das Bearbeiten auf der Karte streicht) |
| Payee auf der Karte: Toast mit Rückgängig | ändert sich: Zeile (Frage 4) |
| Fehler-Toasts („Nicht geprüft: …“ usw.) | ändert sich: rot in der Zeile mit „Erneut“ (neu) |
| Wisch-Hinweis | bleibt bis zur ersten Aktion und nach Rückgängig |
| Tasten Enter, S, K, L, Esc | bleibt; Z neu |
| Endansicht „Alles geprüft“, „Zur Übersicht“ | bleibt; „Letzte rückgängig“ neu |
| Kategorie und Übertrag auf der Karte mit Toast | ledger-mf2bg |
| Detail-Sheet über der Karte | ledger-gwr9j |

### Abweichungen von der Skizze (mit Grund)
- Warnungen tragen im zweiten Teil den Grund statt des Payees (Skizze 09: Payee), sofern Roman Frage 2 so beantwortet:
  heute nennt der Toast den Grund, und die Fehlerzeile (Skizze 10) hat dieselbe Form „Aktion · Grund“.
- „Ist neu“ ohne Rückgängig, sofern Roman Frage 3 so beantwortet (Anhang der Skizze: „Rückgängig nein“; die erste Fassung
  dieser Task nannte es).
- Die Pille oben in Skizze 09 wird nicht gebaut (Rest von B2, R3).
- Schrift 12 px statt 13 px (§7.14).

## Offene Fragen an Roman
1. **PC:** Zeile auch am PC, kein Toast im Durchgehen? Empfehlung ja (ein Weg, Z passt dazu, Toast unten rechts weit weg).
2. **YNAB-Warnung:** zweiter Teil Grund („! Passt, nicht in YNAB · Zeitüberschreitung“) statt Payee (Skizze 09)?
   Empfehlung Grund: der heutige Toast nennt ihn, ohne ihn sieht man nicht, warum; der Payee steht auf der verlassenen
   Karte und ist mit Rückgängig wieder da.
3. **„Ist neu“ rückgängig:** die Task nannte es, die Skizze sagt nein, und es gibt keinen Server-Weg (die Buchung ist
   schon nach YNAB gegangen; Rücknahme hieße in YNAB löschen und wieder zurückhalten). Empfehlung: kein Rückgängig.
   Will Roman es, wird es eine eigene Task mit Server-Endpoint.
4. **Payee auf der Karte:** Bestätigung in der Zeile („✓ Payee geändert · neuer Payee · Rückgängig“)? Die Skizze zeigt
   den Fall nicht. Empfehlung Zeile: unter dem Payee ist kein Platz ohne dass der Karteninhalt springt.

## Acceptance criteria
- [ ] Zeile der letzten Aktion nach Abschnitt 1 für jede Zeile der Tabelle; Aktion nie gekürzt, zweiter Teil mit „…“;
      Zeichen und Farben nach Ton; bleibt bis zur nächsten Aktion; nach Rückgängig wieder der Hinweis.
- [ ] Rückgängig für Passt, Später, Lena, Verworfen, Memo, Payee (Ist neu nach Frage 3) holt die Karte bzw. den Wert zurück,
      „i von n“ und Fortschritt stimmen. Je Weg ein Reducer-Test in `src/Tests/LedgerReviewClientTests.fs`.
- [ ] „Passt rückgängig“: die zurückgeholte Karte wird nicht übersprungen, egal ob das Neulesen vor oder nach der Antwort
      von `restoreReviewMark` kommt; dazu das Orakel „jede Reihenfolge bis Tiefe 6“ um Rückgängig nach Passt, Später, Lena
      und Verwerfen erweitert (Test-Doktrin Punkt 20).
- [ ] Fehler eines Befehls: Karte unverändert, Zeile rot mit Grund und „Erneut“; „Erneut“ schickt denselben Befehl
      (Reducer-Test je Befehl). YNAB-Warnungen gelb in der Zeile.
- [ ] Kein `ShowToast`/`ShowToastWithAction` aus den Handlern in Abschnitt 4, solange das Durchgehen offen ist (Reducer-Tests
      prüfen `NoOp` als externen Befehl); außerhalb des Durchgehens bleiben die Toasts (je ein Gegentest für Prüfvermerk
      und Flag).
- [ ] Taste Z: Reducer-Test für `reviewKeyAction` (mit und ohne Rückgängig, Endansicht, aus Eingabefeld nicht);
      `[tastatur]`-Test in `scripts/durchgehen.test.mjs` um Z erweitert; Tastenleiste zeigt „Z rückgängig“.
- [ ] Endansicht mit „Letzte rückgängig“ (und gelber Zeile bei Warnung).
- [ ] `[flow:durchgehen]` nimmt einmal „Passt“ zurück (Karte wieder da, Zeile zeigt den Hinweis) und bestätigt erneut;
      Satz in `FLOWS` nachgezogen. Kein zweiter Flow-Test.
- [ ] Sweep: neue Zustände `durchgehen-letzte-aktion` (nach „Passt“ auf einer Karte mit langem Payee, 320/412/1280) und
      `durchgehen-fehler` (`setReviewMark` scheitert im Mock, 412), `primary` „Passt, weiter“, `reach` „Rückgängig“ bzw.
      „Erneut“; Invarianten grün ohne Ausnahme. `mockReview` bekommt dafür eine Option für einen scheiternden Aufruf.
- [ ] DS: Zeilenvariante im Baustein aus design-system-ytgnc mit §5-Eintrag und Beispielen (ok, Warnung, Fehler, langer
      Payee) auf `/styleguide`.
- [ ] Seite neben der Skizze verglichen: Screenshots bei 412 px im Outcome für 07 (nach Passt), 08 (langer Payee),
      09 (Warnung), 10 (Fehler), 13 (Endansicht), dazu PC 1280 nach „Passt“; Abweichungen nur die genannten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6. Verfeinert 2026-11-19, dabei geteilt: Kategorie und Übertrag am Feld in ledger-mf2bg.
Bereit für todo, sobald Roman die Fragen 1 bis 4 beantwortet hat; danach Kriterien und Abweichungen an die Antworten
anpassen. Wartet zusätzlich auf design-system-ytgnc (Baustein) und ledger-s2cg3 (Zeile, Flug und Rückkehr der Karte).

**Skizze** (Basis `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- ganze Skizze: `.../scratchpad/vf5h6/durchgehen-handy.html`, Quelltext `.../scratchpad/vf5h6/skizze.src.html`
  (Anhang „Abgleich“ und „Alle Toasts im Kassenbuch und Durchgehen“ im HTML)
- `.../scratchpad/vf5h6/skizze-shots/01-durchgehen-ruhe.png` (Hinweis vor der ersten Aktion)
- `.../scratchpad/vf5h6/skizze-shots/07-nach-passt-letzte-aktion.png`
- `.../scratchpad/vf5h6/skizze-shots/08-zweite-karte-langer-payee.png`
- `.../scratchpad/vf5h6/skizze-shots/09-ynab-warnung.png` (die Pille oben wird nicht gebaut)
- `.../scratchpad/vf5h6/skizze-shots/10-fehler-unter-knopf.png`
- `.../scratchpad/vf5h6/skizze-shots/13-endansicht.png`
- heute: `.../scratchpad/shots/vf5h6/heute-toast-412.png`, `heute-toast-150-412.png`, `heute-toast-600-412.png`

**Am Code:**
- `src/Client/Components/Ledger/State.fs`: Durchgehen ab Zeile 4406 (`ReviewConfirm` 4519, `ReviewSkip` 4562,
  `ReviewLena` 4569, `ReviewConfirmNew` 4596, `ReviewDiscard` 4638, `StartRestoreDiscardedDuplicate` 4666,
  `ReviewKey` 4720); `advanceReview`, `returnToReviewCard`, `showReviewCardAt` (um 1470 bis 1530); Prüfvermerk-Rücknahme
  `StartUndoReviewMark`/`ReviewMarkRestored` (2453 bis 2479), `applyUndoReviewMark` (496); Lena 2743 bis 2821; Memo 2900 ff.;
  Payee um 3274.
- `src/Client/Components/Ledger/Types.fs`: `ReviewSession`, `reviewStarting` (1234 ff.), `ReviewKeyAction`,
  `reviewKeyAction` (1302 ff.), Texte 1323 bis 1345.
- `src/Client/Components/Ledger/View.fs`: `reviewKeys` (5073), `reviewKeyFor` (5088), `reviewNormalCard`,
  `reviewDuplicateCard`, `reviewEnd`, `ReviewPage`.
- `src/Client/State.fs` `toastActionMsg` (Zeile 108 ff.): die Rückgängig-Nachrichten, die die Zeile wiederverwendet.
- Tests: `src/Tests/LedgerReviewClientTests.fs` (Orakel Zeile 388, „Rückgängig nach dem Verwerfen meldet sich mit einem
  Toast …“ Zeile 499 und „Verwerfen geht weiter und bietet Rückgängig an …“ Zeile 517 ändern ihre Aussage,
  „scheitert der Prüfvermerk, bleibt die Karte und ein Toast nennt den Grund“ Zeile 431, Lena-Liste ab 649);
  `scripts/durchgehen.test.mjs`; `scripts/layout-states.mjs` (`mockReview` Zeile 666 ff.).

### Reihenfolge zu ledger-gwr9j (Abgleich 2026-11-19)
ledger-gwr9j und diese Task ändern dieselben gemeinsamen Handler in `src/Client/Components/Ledger/State.fs` (`LenaFlagSet`,
`FlagRestored`, `ReviewMarkRestored`, `MemoSet`, `PayeeSet`, `DiscardedDuplicateRestored`). Damit zwei Worker nicht dieselben
Zweige umbauen, kommt diese Task nach gwr9j (`depends_on`). Sie baut nur die Zweige für `model.Review` (`isReviewCard`).
