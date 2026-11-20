---
id: ledger-p5y9s
title: "Kassenbuch am Handy ohne Toast (R3), Teil 2 Detail I: Rückgängig am Feld im Detail-Sheet als Mechanik, Kategorie, Übertrag und Aufteilen bestätigen am Feld, Fehler am Feld, Übergang beim Schließen"
status: done
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, ledger-gwr9j]
blocks: [ledger-enjex]
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0055, 0058, 0098]
related_research: []
prior_art: [ledger-vf5h6, ledger-gwr9j, ledger-sfab0, ledger-qvjvs, ledger-pafkp]
---

## Why
Regel R3 (design-system-ytgnc), Platz 1: ist das Ergebnis am Ort sichtbar, bestätigt die Stelle selbst. Im Detail-Sheet
am Handy liegt ein Toast heute über dem Sheet oder dahinter (Regression c819eeb), und die Leiste aus ledger-gwr9j liegt
unter dem Sheet. Teil 2 von 4 der ursprünglichen ledger-gwr9j.

## What
Vorlage: Skizze aus ledger-vf5h6 (Version 9, freigegeben 2026-11-19), Anhang „Alle Toasts“, Abschnitt Kassenbuch, Zeilen
„Kategorie geändert …; Übertrag gesetzt …“ (anders: Rückgängig am Feld im Detail) und „Aufteilung gespeichert …“. Die
Gestalt von „Rückgängig am Feld“ zeigt die Skizze im Durchgehen (Abschnitt 1, Kategorie: Feld leuchtet kurz, darunter
Bestätigung und „Rückgängig“, Skizze 06); der Baustein kommt aus design-system-ytgnc.

Gilt für das Detail als Sheet (`DetailForm = DetailSheet`, unter 1024 px) und für das Buchungs-Panel am PC (ab 1024 px)
gleich: Bestätigung und „Rückgängig“ am Feld, kein Toast (Entscheidung Roman 2026-11-19, ADR 0098).

### Mechanik „letzte Aktion im Detail“
- Das Modell hält höchstens eine Bestätigung im Detail: Buchung, Feld, Ton (ok, Warnung, Fehler), Text, optional die
  Rückgängig-Nachricht. Eine neue Aktion im Detail ersetzt sie (gleiche Regel wie die Leiste).
- Die Bestätigung steht unter dem Feld, das sich geändert hat; das Feld leuchtet kurz (Motion aus §6, mit
  `prefers-reduced-motion` ohne Leuchten). Kein Zeitlimit: sie bleibt bis zur nächsten Aktion im Detail oder bis das Detail
  schließt (Entscheidung Roman). Die Karte „Budget nach dem Wechsel“ behält ihr heutiges Zeitlimit.
- „Rückgängig“ an der Bestätigung löst dieselbe Nachricht aus wie heute der Toast. Danach steht der alte Wert im Feld und
  die Bestätigung verschwindet; die heutigen Rücknahme-Toasts („Kategorie zurückgesetzt: …“, „Übertrag zurückgesetzt …“,
  „Aufteilung zurückgenommen“) entfallen (Anhang: „Zeile bzw. Feld steht sichtbar wieder da“).
- Schließen des Details mit offener Bestätigung, die „Rückgängig“ trägt: sie wandert in die Leiste aus ledger-gwr9j
  (Aktion „Kategorie geändert“, zweite Zeile Payee, „Rückgängig“; Entscheidung Roman). Am PC ist das der Toast unten
  rechts, weil die Meldung nach dem Schließen keinen Ort mehr hat (ADR 0098).
- Kommt die Antwort, wenn das Detail dieser Buchung nicht mehr offen ist, geht die Meldung in die Leiste, am PC in den
  Toast (heutiges Muster `splitSaveFailed`, State.fs ~1032).
- Detail aus dem Durchgehen geöffnet (`ReviewShowDetails`, `isReviewCard`): im offenen Sheet gilt dieselbe Bestätigung am
  Feld. Was nach dem Schließen in der Zeile unter „Passt, weiter“ steht, regelt ledger-pz5wv (Abgleich durch den Conductor).

### Zuordnung je Toast (State.fs, Stand d6e9cb95)
| Zeile | Heute | Mit R3 im Detail-Sheet und im Panel am PC |
|---|---|---|
| 2577/2578 | „Kategorie geändert: X“ (gelb mit „Kategorie nicht in YNAB: …“) mit Rückgängig | unter „Kategorie“: „✓ Geändert“, „Rückgängig“ (= `UndoCategory`); bei Fehlschlag zu YNAB gelb „Geändert, nicht in YNAB: Grund“ mit „Rückgängig“. Die Karte „Budget nach dem Wechsel“ und „Regel anlegen?“ bleiben darunter wie heute |
| 2567 | „Kategorie nicht in YNAB: …“ ohne Änderung | gelb unter „Kategorie“ |
| 2585 | „Kategorie nicht geändert: …“ | rot unter „Kategorie“ (Fehler am Feld, Styleguide-Regel `task-ui-vorgaben`) |
| 2612/2614 | Rücknahme-Toast | entfällt (gelbe Warnung 2612 bleibt am Feld) |
| 2621 | „Rückgängig nicht möglich: …“ | rot unter „Kategorie“ |
| 2663/2664 | „Übertrag auf X“ mit Rückgängig, Rücknahme „Übertrag zurückgesetzt“ | unter „Kategorie“: „✓ Gegenbuchung angelegt“ (Text aus dem Anhang Durchgehen), „Rückgängig“ (= `UndoTransfer`); Rücknahme entfällt |
| 2652 | „Kategorie nicht in YNAB: …“ beim Übertrag | gelb am Feld |
| 2672 | „Übertrag nicht gesetzt: …“ / „Rückgängig nicht möglich: …“ | rot am Feld |
| 2707 | „Kategorie nicht in YNAB: …“ nach „Erneut senden“ | entfällt als Meldung: die Hinweisleiste „Kategorie nicht in YNAB“ oben im Detail (`detailNoticeWithCategory`) zeigt den neuen Grund (Zustand, keine Meldung, R3) |
| 3759/3761 | „Aufgeteilt in n Zeilen“ / „Aufteilung aufgehoben“ (mit Rückgängig, wenn `SplitEditor.undoAvailable`) | Editor schließt wie heute, im Detail unter den Split-Zeilen (`Detail.splitCard`) bzw. unter „Kategorie“ nach Aufheben: „✓ Aufgeteilt in n Zeilen“ / „✓ Aufteilung aufgehoben“, „Rückgängig“ (= `UndoSplit`) nur wo heute |
| 3748 | „Kategorie nicht in YNAB: …“ beim Split | gelb am selben Platz |
| 3785/3786 | „Betrag geändert · …“ nach Split mit neuem Betrag (ohne Rückgängig, ADR 0092) | am Betrag oben im Detail: „✓ Geändert“, ohne Rückgängig; gelb bei Fehlschlag zu YNAB |
| 3793/3796 via 1038 | Speicherfehler: im Editor, sonst Toast | im Editor wie heute; Editor schon zu: Leiste rot (am PC Toast) |
| 3818/3820 | Rücknahme | entfällt (Warnung gelb am Platz) |
| 3826 | „Rückgängig nicht möglich: …“ | rot am Platz |

Abweichung vom Anhang: „Aufteilung gespeichert …; Aufteilung zurückgenommen“ steht dort mit „bleibt, Leiste unten; das
Formular schließt“. Nach dem Schließen des Editors steht wieder das Detail-Sheet, und das deckt die Leiste zu. Deshalb am
Platz der Aufteilung (R3 Platz 1, ytgnc). Ebenso die gelben Warnungen „… nicht in YNAB“: Anhang „Leiste unten, gelb“, im
offenen Sheet stattdessen gelb am Feld, aus demselben Grund.

### Funktionsabgleich Detail (dieser Teil)
| Funktion heute | Stand |
|---|---|
| Kategorie über Picker (Sheet ersetzt den Inhalt), „alle zeigen“, Karte „Budget nach dem Wechsel“ mit eigenem Zeitlimit (`categoryCardDurationMs`) | bleibt |
| „Regel anlegen?“ nach Kategorieänderung (`RuleOffer`) | bleibt; Rückgängig nimmt das Angebot weiter mit weg |
| Übertrag auf ein Konto aus dem Picker | bleibt |
| Aufteilen, Barabhebung, Rest, Speicher-Sperre | bleibt |
| Hinweisleisten „… nicht in YNAB“ mit „Erneut senden“ | bleibt |
| Rückgängig für Kategorie, Übertrag, Aufteilen | bleibt, am Feld statt im Toast |
| Hinweis „passt nicht mehr zum Filter“ an der Zeile | bleibt |
| Toasts am Handy | gestrichen (Roman, 2026-11-19, R3) |
| Toasts nach Kategorie, Übertrag, Aufteilen im Panel am PC | gestrichen (Roman, 2026-11-19): Rückgängig am Feld |

## Acceptance criteria
- [ ] Am Handy (412 px) zeigt das Detail-Sheet und am PC (1280) das Buchungs-Panel nach Kategorie, Übertrag und
      Aufteilen die Bestätigung unter dem Feld mit „Rückgängig“; es erscheint kein Toast. Das Feld leuchtet kurz, ohne
      Leuchten bei `prefers-reduced-motion`.
- [ ] Jede Zeile der Tabelle „Zuordnung je Toast“ umgesetzt, im Sheet und im Panel am PC gleich.
- [ ] Die Bestätigung bleibt ohne Zeitlimit bis zur nächsten Aktion im Detail; beim Schließen mit offenem „Rückgängig“
      wandert sie in die Leiste (am PC in den Toast) und „Rückgängig“ dort löst dieselbe Nachricht aus.
- [ ] Reine Funktion(en) für „welche Bestätigung nach welchem Ergebnis“ und für „Detail schließt mit offener Bestätigung“
      mit Tests in `src/Tests` (heute `LedgerCategoryChangeClientTests.fs`, `LedgerSplitEditorClientTests.fs`,
      `LedgerDetailTests.fs`): je Weg Text, Ton, Rückgängig-Nachricht; Ersetzen durch die nächste Aktion; Antwort bei
      geschlossenem Detail geht in die Leiste.
- [ ] „Rückgängig“ am Feld stellt den alten Wert her (Kategorie, Übertrag, Aufteilung) und die Bestätigung verschwindet; ein
      zweiter Tipp während der Rücknahme schickt nichts (heutige Sperre `whenRowIdle`).
- [ ] Fehler eines Befehls stehen rot unter dem Feld, nicht als Toast und nicht in der Leiste, solange das Detail (Sheet
      oder Panel) offen ist.
- [ ] Sweep-Zustände: `kassenbuch-kategorie-toast` wird `kassenbuch-kategorie-rueckgaengig` (wartet auf „Rückgängig“ am
      Feld statt `[data-toast-action]`); ebenso `kassenbuch-regel-angebot` und `openFilteredOut` mit `mobile: true`
      (`kassenbuch-ausgegraut-handy`). Neu bei `HANDY_WIDTHS`: `kassenbuch-aufgeteilt-rueckgaengig`,
      `kassenbuch-kategorie-fehler-feld`. `kassenbuch-kategorie-rueckgaengig`, `kassenbuch-aufgeteilt-rueckgaengig` und
      `kassenbuch-kategorie-fehler-feld` laufen zusätzlich bei 1280 (Panel) und zeigen dort „Rückgängig“ bzw. den Fehler
      am Feld, keinen Toast. Die PC-Breite 1280 aus design-system-gkded für `kassenbuch-kategorie-toast` wartet damit auf
      das Feld; der Gegenbeweis zu c819eeb (Toast über dem Panel) braucht eine Meldung ohne Ort bei offenem Panel. Die
      Invariante „keine Rückmeldung überdeckt Bedienelement oder Inhalt“ aus ytgnc ist grün.
- [ ] Browser-Tests: die PC-Flows `[flow:kategorie-aendern]` und `[flow:aufteilen]` finden die Bestätigung und
      „Rückgängig“ am Feld im Panel statt im Toast und sind grün. Ein Handy-Test je Kennung höchstens: `[layout]` Handy,
      Kategorie ändern im Sheet, Bestätigung am Feld sichtbar ohne Scrollen, Rückgängig stellt zurück.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome: Detail nach Kategorie, nach Übertrag, nach
      Aufteilen, Fehler am Feld; dazu PC 1280 Panel mit Bestätigung am Feld, ohne Toast), Abweichungen nur die in dieser
      Task genannten.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Entscheidungen Roman (2026-11-19)
1. **Wie lange steht „Rückgängig“ am Feld, wenn das Detail offen bleibt?** Ohne Zeitlimit bis zur nächsten Aktion im
   Detail. Folge: die Karte „Budget nach dem Wechsel“ behält als einzige ihr Zeitlimit.
2. **Was passiert beim Schließen des Detail-Sheets mit offenem „Rückgängig“?** Die Bestätigung wandert in die Leiste
   („Kategorie geändert“ / Payee / „Rückgängig“). Folge: ein „Rückgängig“ geht beim schnellen Schließen nicht verloren.
3. **Rückgängig am Feld auch im Panel am PC?** Ja, für Kategorie, Übertrag und Aufteilen (und in ledger-enjex für Memo,
   Betrag, Payee), ohne Toast. Folge: diese Task deckt das Panel mit ab, Kriterien, Sweep-Zustände und Tests haben eine
   PC-Breite 1280.

## Notes
Teil 2 von 4 aus ledger-gwr9j (Verfeinerung 2026-11-19). Mechanik hier, ledger-enjex nutzt sie für Memo, Betrag, Payee
und Lena.

Code: `src/Client/Components/Ledger/State.fs` (Zweige oben; `applyCategoryResult` ~640, `splitSaveFailed` ~1032,
`closeDetail` ~1357, `detailCloseStep` ~1364), `Types.fs` (Texte `categoryChangedToastText` ~1157, `transferSetToastText`,
`categoryRestoredToastText`; `SplitEditor.savedToastText` ~185), `View.fs` (`categoryField` ~1559, `detailContent` ~1796,
`DetailBottomSheet` ~2190; Platz der Bestätigung unter `Detail.fieldRowWith`). Undo-Arten dieses Teils: `UndoCategory`,
`UndoTransfer`, `UndoSplit` (`toastActionMsg`, `src/Client/State.fs` ~110), am Feld lösen sie dieselben `Start…`-Nachrichten
aus. Die Kategorie-Zweige mit `isReviewCard` gehören ledger-pz5wv.

Skizze (lokal):
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze.src.html (Anhang ~Zeile 561)
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/06-kategorie-aus-vorschlag.png (Gestalt „Feld leuchtet, darunter Bestätigung und Rückgängig“), 11-details-memo-block.png (Detail-Sheet)
- Leiste: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/22-kassenbuch-geprueft.png

## Verifier note (iteration 1)
REASONS: Auf dem gemeinsamen Stand mit ledger-k68sd (VERIFY: PASS, Styleguide mit beiden Seiten) ein Fehler im Zusammenspiel: Löscht man eine Buchung aus dem offenen Detail, während ihre Bestätigung am Feld mit „Rückgängig“ steht, und steht die Buchung in der geladenen Liste, setzt `EntryDeleteFinished (entryId, Ok ())` die Zeile am Platz und gibt bewusst `NoOp` zurück. `handOverDetailNote` sieht danach eine Bestätigung für eine Buchung, die das Detail nicht mehr zeigt, und schickt `PostFeedback (detailNoteBar note)`: Leiste bzw. PC-Toast „Kategorie geändert · <Payee> · Rückgängig“ für die gelöschte Buchung neben der Zeile „… gelöscht · Rückgängig“; ein Tipp schickt `StartUndoCategory` für eine gelöschte Buchung. Kein Test deckt Löschen mit offener Bestätigung ab.
SUGGESTED_FIX: In `EntryDeleteFinished (entryId, Ok ())` die Bestätigung dieser Buchung vor dem Schließen des Details entfernen (das Löschen ist die neue Aktion, die sie ersetzt). Regressionstest in `LedgerEntryDeletionClientTests.fs` über `update`: offenes Detail mit Bestätigung „Kategorie geändert“, Buchung in der Liste, Löschen gelingt → `DeletedRow = Some …`, `DetailNote = None`, external `NoOp`; vor dem Fix rot.
ITERATION_HINT: likely-fixable


## Outcome

Kategorie, Übertrag und Aufteilen melden im offenen Buchungs-Detail ohne Toast am Feld (Regel R3 Platz 1, ADR 0098). Das gilt für das Sheet am Handy und das Panel am PC gleich (Entscheidung Roman 3).

**Mechanik „letzte Aktion im Detail“** (`src/Client/Components/Ledger/Types.fs`, `State.fs`)
- `DetailNote` (Buchung, Feld `AtCategory`/`AtSplits`/`AtAmount`, Ton, Text, `Undo: ToastActionKind option`, `BarAction`/`BarDetail` für die Leiste, `Flashing`, `Version`) in `Model.DetailNote`, dazu `Model.DetailNoteVersion`. Neue Nachricht `DetailNoteFlashEnded`.
- Reine Funktionen: `categoryChangedNote`, `transferSetNote`, `splitSavedNote`, `amountChangedNote`, `notInYnabDetailNote`, `failedDetailNote`, `undoFailedDetailNote`, `restoredNotInYnabNote` (welche Bestätigung nach welchem Ergebnis), `detailNoteRoute` (`NoteAtField`, `NoteOnReviewCard`, `NoteInBar`), `detailNoteBar`, `handOverDetailNote` (Detail schließt mit offener Bestätigung), `detailNoteAt`, `detailNoteUndoMsg`.
- `State.update` ruft `handOverDetailNote` im Nachlauf auf. Das deckt jeden Weg ab, auf dem das Detail schließt oder die Buchung wechselt: ×, Esc, `OpenDetail`, Auswahl, Filter. Mit „Rückgängig“ wird daraus `PostFeedback`: Leiste am Handy, Toast am PC. Meldet der schließende Schritt selbst etwas, ersetzt das die Bestätigung. Ohne „Rückgängig“ endet sie still.
- Eine neue Aktion (`StartSetEntryCategory`, `StartSetEntryTransfer` ohne `undo`, `StartSaveSplits`) nimmt die alte Bestätigung weg. Eine gelungene Rücknahme entfernt sie ebenfalls.
- Das Leuchten endet nach `detailNoteFlashMs` (700 ms), es gilt nur für die eigene Nummer. Ein Zeitlimit für die Bestätigung gibt es nicht. Die Karte „Budget nach dem Wechsel“ behält ihres.
- Befehlsfehler im offenen Detail stehen rot am Feld. `CategoryRows` bekommt dann keinen `CategoryCommandFailed`, damit die Hinweisleiste oben nicht ein zweites Rot zeigt. Bei geschlossenem Detail bleibt der Befehlsfehler an der Zeile, und die Leiste meldet ihn (`categoryCommandFailed`).
- Die Karte des Durchgehens ohne „Details“ (`isReviewCard`, aber nicht `detailShows`) meldet unverändert per Toast. Das regelt ledger-pz5wv. Im Durchgehen mit „Details“ gilt die Bestätigung am Feld. Schließen der „Details“ übergibt an die Leiste. Ob das für die Zeile unter „Passt, weiter“ passt, gleicht der Conductor mit pz5wv ab.

**Zuordnung je Toast** (alle Zeilen der Tabelle umgesetzt)
- Kategorie geändert: „✓ Geändert“ (ohne Kategorie „✓ Entfernt“) mit „Rückgängig“. Gelb „Geändert, nicht in YNAB: Grund“ mit „Rückgängig“.
- Unverändert und nicht in YNAB: gelb „Nicht in YNAB: Grund“.
- Befehlsfehler: rot „Nicht geändert: Grund“. Rückgängig gescheitert: rot „Rückgängig nicht möglich: Grund“.
- Rücknahme-Toasts entfallen im offenen Detail. Die gelbe Warnung danach heißt „Zurückgesetzt, nicht in YNAB: Grund“.
- Übertrag: „✓ Gegenbuchung angelegt“, Fehler rot „Nicht gesetzt: Grund“.
- Erneut senden (`CategorizationRetried`) meldet im offenen Detail nichts, die Hinweisleiste zeigt den neuen Grund.
- Split: „✓ Aufgeteilt in n Zeilen“ unter `Detail.splitCard`, „✓ Aufteilung aufgehoben“ unter „Kategorie“. „Rückgängig“ steht nur dort, wo es auch der Toast hatte.
- Split mit Betrag: „✓ Geändert“ am Betrag, ohne Rückgängig.
- Speicherfehler mit offenem Editor bleiben im Editor, bei schon geschlossenem Editor meldet die Leiste (`splitSaveFailed`, unverändert).
- Die Texte in der Leiste nach dem Schließen: „Kategorie geändert“/„Kategorie entfernt“, „Übertrag auf <Konto>“, „Aufgeteilt in n Zeilen“, jeweils mit dem Payee in der zweiten Zeile. „Betrag geändert“ hat den Betrag in der zweiten Zeile. Warnungen lauten „…, nicht in YNAB“ mit dem Grund.
- Entfernt, weil nicht mehr benutzt: `categoryChangedToastText`, `transferSetToastText`.

**Ansicht** (`Components/Ledger/View.fs`)
- `detailNoteView` setzt eine Zeile `data-testid="detail-note"` mit `data-note-field` unter das Feld. Die Kategorie-Zeile, die Split-Karte und der Betrag tragen beim Leuchten `Feedback.flash`.
- Neuer DS-Baustein `Feedback.fieldNoteWith tone text act` (`src/Client/DesignSystem/Feedback.fs`): ✓ grün bzw. ! gelb/rot, Text gedämpft, gelb oder rot, Fehler mit `role="alert"`, Attribut `data-tone`. `fieldNote` bleibt und ruft die neue Funktion mit `Info` auf.
- Der Styleguide zeigt die drei Töne unter „Feld mit Leuchten und Bestätigung“. `standards/frontend/styleguide.md` §5 und §7.13 nennen ihn, dazu kommt ein neuer Punkt „Bestätigung am Feld im Detail“.

**Tests** (`src/Tests`, 16 neue Expecto-Tests, bestehende umgestellt)
- `LedgerCategoryChangeClientTests.fs` hat die neue Liste „Bestätigung am Feld im Detail (ledger-p5y9s)“ mit 12 Tests:
  - Sheet und Panel zeigen dasselbe.
  - Schließen übergibt an die Leiste mit `toastActionMsg` gleich `StartUndoCategory`.
  - Esc und der Wechsel auf eine andere Buchung übergeben ebenso.
  - Ein Fehler endet beim Schließen still.
  - Eine eigene Meldung des schließenden Schritts ersetzt die Bestätigung.
  - Eine Antwort bei geschlossenem Detail geht in die Leiste.
  - Die nächste Aktion ersetzt, das Leuchten endet nur für die eigene Nummer.
  - Der Zeitgeber des Leuchtens läuft 700 ms.
  - Ein zweiter Tipp während der Rücknahme sendet nichts.
  - Rückgängig gescheitert steht rot, ein YNAB-Fehler danach gelb.
  - Unverändert, aber nicht in YNAB, steht gelb.
  - `detailNoteUndoMsg` entspricht `toastActionMsg` für alle drei Arten.
- Dazu: Befehlsfehler bei geschlossenem Detail, Rücknahme eines Übertrags im offenen Detail, angepasste Kategorie- und Übertrag-Tests (ohne Toast, Bestätigung am Feld; Detail zu: Leiste wie bisher).
- `LedgerSplitEditorClientTests.fs`: Rückgängig gescheitert rot am Platz, Speicherfehler bei zu Editor geht in die Leiste. Bestehende Split-Tests sind auf `DetailNote` umgestellt.
- `LedgerAmountClientTests.fs`: Split mit Betrag am Betrag, gelb, Leiste ohne Rückgängig.
- Gegenprobe: `handOverDetailNote` im Nachlauf von `update` entfernt, dann waren 3 der 12 Tests rot (Schließen, Esc/Wechsel, still enden). Danach wiederhergestellt.

**Browser und Sweep** (`scripts/`)
- `[flow:kategorie-aendern]` (PC 1440) findet „✓ Geändert · Rückgängig“ im Panel. Rückgängig dort sendet die alte Kategorie, danach ist die Bestätigung weg und es steht kein Toast zur Kategorie.
- `[flow:aufteilen]` (PC 1440) findet „Aufgeteilt in 2 Zeilen“ unter den Split-Zeilen. Rückgängig dort schickt die alten Zeilen, es kommt kein Toast.
- Neu ist `[layout] Handy (412)` in `ledger-category-change.test.mjs`: Bestätigung im Sheet, im Bild und oben, nicht in der Leiste, Rückgängig stellt zurück. Der Tipp wird wiederholt, weil `Viewport.swallowNextClick` 350 ms nach der Picker-Wahl schluckt.
- Die Toast-Prüfungen der Flows lassen einen Toast ohne Bezug zu, etwa den Fehler der nicht gemockten Lena-Einstellungen. Ein Toast zu Kategorie oder Aufteilung macht den Test rot.
- `layout-states.mjs`:
  - `kassenbuch-kategorie-toast` heißt jetzt `kassenbuch-kategorie-rueckgaengig` (320, 390, 1280). Der Zustand wartet auf `fieldUndo`, prüft „Geändert“ und dass kein Toast steht, `reach` ist das „Rückgängig“ am Feld.
  - Die Ausnahme Invariante 5 @ 1280 (`detail-history-toggle`) ist weg (Kann-Sammlung design-system: dieser Punkt ist erledigt).
  - Neu: `kassenbuch-aufgeteilt-rueckgaengig` und `kassenbuch-kategorie-fehler-feld` (je 320, 390, 1280). Beim Fehler-Zustand steht rot „Nicht geändert: YNAB nicht erreichbar“, es kommt kein Toast und die Leiste hat keine Meldung.
  - `kassenbuch-regel-angebot`, `openFilteredOut` (`kassenbuch-ausgegraut-handy`, `kassenbuch-ausgegraut`) und `kassenbuch-leiste-geprueft` @ 900 warten auf „Rückgängig“ am Feld. Bei 900 zeigt die Leiste die Meldung danach durch die Übergabe beim Schließen.
  - Der Gegenbeweis zu c819eeb läuft weiter bei `uebersicht-meldung` @ 1280 (seit design-system-gkded), weil das Kassenbuch-Panel jetzt keine Meldung ohne Ort mehr hat.
- Alle betroffenen Zustände sind ohne Verletzung, auch einzeln bei 412.

**Vergleich mit der Skizze** (Bilder in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/result-shots/ledger-p5y9s/`, Skizze in `…/scratchpad/vf5h6/skizze-shots/`)
- `kategorie-412.png` neben `06-kategorie-aus-vorschlag.png`: unter „Kategorie“ stehen ✓, „Geändert“ und „Rückgängig“ im Akzent, darunter die Karte und „Regel anlegen?“.
  - Unterschied 1 (aus der Task): Text „✓ Geändert“ statt „Bahn · 84,20 € verfügbar“, die Beträge stehen in der Karte.
  - Unterschied 2: das Detail hat eine Zeile mit Rahmen statt der Feld-Box der Karte. Das Leuchten ist ein Ring um die Zeile, 0,7 s lang, auf dem Bild schon vorbei.
- `uebertrag-412.png`: „✓ Gegenbuchung angelegt · Rückgängig“ unter „Übertrag → Tagesgeld“. Das Sheet steht dort weiter unten gescrollt (Nachladen der Buchung).
- `aufgeteilt-412.png`: „✓ Aufgeteilt in 2 Zeilen · Rückgängig“ unter der Split-Karte. Abweichung vom Anhang („Leiste unten“) wie in der Task begründet.
- `fehler-feld-412.png`: rotes „! Nicht geändert: YNAB nicht erreichbar“ unter „Kategorie“, die alte Kategorie steht, kein Toast und keine Leiste mit Meldung.
- `kategorie-panel-1280.png`, `aufgeteilt-panel-1280.png`, `fehler-feld-1280.png`: dasselbe im Panel, unten rechts kein Toast.
- [human-eye] Den Gesamteindruck beurteilt Roman.

**README-Gate**: `npm run check:readme-delta -- --bc ledger --delta …` Exit 0. Ganz nachlesen: `## Key commands` *KategorieÄndern* (Zeile 402) muss von Hand nachgezogen werden, weil der Bullet keinen fetten Kopf hat. Satz alt: „Das Rückgängig im Toast ist derselbe Befehl mit `Previous` aus der Antwort.“ Satz neu: „Das Rückgängig an der Bestätigung am Feld (nach dem Schließen des Details in der Leiste) ist derselbe Befehl mit `Previous` aus der Antwort.“

**Merge mit main (Iteration 2)**: Die Konflikte in `Feedback.fs`, `Styleguide/View.fs` und `styleguide.md` §5 sind aufgelöst, beide Seiten bleiben erhalten. `fieldNoteWith` und `fieldNote` stehen neben `Feedback.saved` aus banking-import-vwhfm. Die Styleguide-Demo zeigt die drei Töne und „✓ Gespeichert“. Die Zeile `Feedback` in §5 nennt `fieldNoteWith` und `saved`.

**Iteration 3 (Verifier-Fund mit ledger-k68sd)**: Gelang das Löschen einer Buchung aus dem offenen Detail, während ihre Bestätigung am Feld mit „Rückgängig“ stand, schickte `handOverDetailNote` dieses „Rückgängig“ für die gelöschte Buchung in die Leiste. Jetzt gilt das Löschen als neue Aktion: `EntryDeleteFinished (entryId, Ok ())` nimmt zuerst die Bestätigung dieser Buchung weg (`clearDetailNote`). Steht die Buchung in der Liste, bleibt nur die Zeile am Platz ohne Meldung, sonst meldet nur „gelöscht“. Zwei Regressionstests in `src/Tests/LedgerEntryDeletionClientTests.fs` (Liste „Buchung löschen mit offener Bestätigung am Feld (ledger-p5y9s)“). Der Test mit der Buchung in der Liste war vor dem Fix rot. Andere Wege, auf denen eine Buchung verschwindet, melden selbst etwas und ersetzen die Bestätigung damit schon: „Ist doppelt, verwerfen“ (`ReviewDiscarded`) mit Toast. Bei einer wartenden Buchung sind Kategorie und Aufteilen ohnehin gesperrt (`heldLockReason`). Das Verwerfen in „Neue Buchung“ hat keine Bestätigung im Detail.

**Gate**: `./verify.sh` → `VERIFY: PASS` (Iteration 1: dotnet 5304, node 355; nach dem Merge: dotnet 5373, node 355; Iteration 3: dotnet 5406, node 355).

**Kann-Sammlung ledger**
- Die Zeile der Bestätigung trägt `border-b` wie die Feldzeilen. Unter „Kategorie“ entstehen so zwei Linien knapp übereinander, ohne Rahmen wäre sie näher an Skizze 06.
- Andere Aktionen im Detail (Memo, Flag, Betrag, Payee) ersetzen die Bestätigung noch nicht, das folgt in ledger-enjex.
