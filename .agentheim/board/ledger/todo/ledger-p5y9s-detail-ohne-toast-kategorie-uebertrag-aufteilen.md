---
id: ledger-p5y9s
title: "Kassenbuch am Handy ohne Toast (R3), Teil 2 Detail I: Rückgängig am Feld im Detail-Sheet als Mechanik, Kategorie, Übertrag und Aufteilen bestätigen am Feld, Fehler am Feld, Übergang beim Schließen"
status: todo
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
