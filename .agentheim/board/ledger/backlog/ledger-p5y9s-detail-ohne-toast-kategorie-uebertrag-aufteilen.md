---
id: ledger-p5y9s
title: "Kassenbuch am Handy ohne Toast (R3), Teil 2 Detail I: Rückgängig am Feld im Detail-Sheet als Mechanik, Kategorie, Übertrag und Aufteilen bestätigen am Feld, Fehler am Feld, Übergang beim Schließen"
status: backlog
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

Gilt für das Detail als Sheet (`DetailForm = DetailSheet`, unter 1024 px). Im Panel am PC bleibt der Toast wie heute.

### Mechanik „letzte Aktion im Detail“
- Das Modell hält höchstens eine Bestätigung im Detail: Buchung, Feld, Ton (ok, Warnung, Fehler), Text, optional die
  Rückgängig-Nachricht. Eine neue Aktion im Detail ersetzt sie (gleiche Regel wie die Leiste).
- Die Bestätigung steht unter dem Feld, das sich geändert hat; das Feld leuchtet kurz (Motion aus §6, mit
  `prefers-reduced-motion` ohne Leuchten). Kein Zeitlimit: sie bleibt bis zur nächsten Aktion im Detail oder bis das Detail
  schließt (offene Frage 1).
- „Rückgängig“ an der Bestätigung löst dieselbe Nachricht aus wie heute der Toast. Danach steht der alte Wert im Feld und
  die Bestätigung verschwindet; die heutigen Rücknahme-Toasts („Kategorie zurückgesetzt: …“, „Übertrag zurückgesetzt …“,
  „Aufteilung zurückgenommen“) entfallen (Anhang: „Zeile bzw. Feld steht sichtbar wieder da“).
- Schließen des Details mit offener Bestätigung, die „Rückgängig“ trägt: nach Empfehlung zu offener Frage 2 wandert sie in
  die Leiste aus ledger-gwr9j (Aktion „Kategorie geändert“, zweite Zeile Payee, „Rückgängig“).
- Kommt die Antwort, wenn das Detail dieser Buchung nicht mehr offen ist, geht die Meldung in die Leiste (heutiges Muster
  `splitSaveFailed`, State.fs ~1032).
- Detail aus dem Durchgehen geöffnet (`ReviewShowDetails`, `isReviewCard`): im offenen Sheet gilt dieselbe Bestätigung am
  Feld. Was nach dem Schließen in der Zeile unter „Passt, weiter“ steht, regelt ledger-pz5wv (Abgleich durch den Conductor).

### Zuordnung je Toast (State.fs, Stand d6e9cb95)
| Zeile | Heute | Mit R3 im Detail-Sheet |
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
| 3793/3796 via 1038 | Speicherfehler: im Editor, sonst Toast | im Editor wie heute; Editor schon zu: Leiste rot |
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

## Acceptance criteria
- [ ] Am Handy (412 px) zeigt das Detail-Sheet nach Kategorie, Übertrag und Aufteilen die Bestätigung unter dem Feld mit
      „Rückgängig“; es erscheint kein Toast. Das Feld leuchtet kurz, ohne Leuchten bei `prefers-reduced-motion`.
- [ ] Jede Zeile der Tabelle „Zuordnung je Toast“ umgesetzt; am PC (Panel, 1280) derselbe Toast wie heute.
- [ ] Reine Funktion(en) für „welche Bestätigung nach welchem Ergebnis“ und für „Detail schließt mit offener Bestätigung“
      mit Tests in `src/Tests` (heute `LedgerCategoryChangeClientTests.fs`, `LedgerSplitEditorClientTests.fs`,
      `LedgerDetailTests.fs`): je Weg Text, Ton, Rückgängig-Nachricht; Ersetzen durch die nächste Aktion; Antwort bei
      geschlossenem Detail geht in die Leiste.
- [ ] „Rückgängig“ am Feld stellt den alten Wert her (Kategorie, Übertrag, Aufteilung) und die Bestätigung verschwindet; ein
      zweiter Tipp während der Rücknahme schickt nichts (heutige Sperre `whenRowIdle`).
- [ ] Fehler eines Befehls stehen rot unter dem Feld, nicht als Toast und nicht in der Leiste, solange das Detail offen ist.
- [ ] Sweep-Zustände: `kassenbuch-kategorie-toast` wird `kassenbuch-kategorie-rueckgaengig` (wartet auf „Rückgängig“ am
      Feld statt `[data-toast-action]`); ebenso `kassenbuch-regel-angebot` und `openFilteredOut` mit `mobile: true`
      (`kassenbuch-ausgegraut-handy`). Neu bei `HANDY_WIDTHS`: `kassenbuch-aufgeteilt-rueckgaengig`,
      `kassenbuch-kategorie-fehler-feld`. Die Invariante „keine Rückmeldung überdeckt Bedienelement oder Inhalt“ aus ytgnc
      ist grün.
- [ ] Browser-Tests: die PC-Flows `[flow:kategorie-aendern]` und `[flow:aufteilen]` bleiben unverändert grün (Beleg, dass der
      PC nicht bricht). Ein Handy-Test je Kennung höchstens: `[layout]` Handy, Kategorie ändern im Sheet, Bestätigung am
      Feld sichtbar ohne Scrollen, Rückgängig stellt zurück.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome: Detail nach Kategorie, nach Übertrag, nach
      Aufteilen, Fehler am Feld; dazu PC 1280 Panel mit Toast), Abweichungen nur die in dieser Task genannten.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Offene Fragen an Roman
1. **Wie lange steht „Rückgängig“ am Feld, wenn das Detail offen bleibt?** Empfehlung: bis zur nächsten Aktion im Detail
   oder bis es schließt, ohne Zeitlimit (R3). Die Karte „Budget nach dem Wechsel“ behält ihr heutiges Zeitlimit.
2. **Was passiert beim Schließen des Detail-Sheets mit offenem „Rückgängig“?** Empfehlung: die Bestätigung wandert in die
   Leiste über der Navigation („Kategorie geändert“ / Payee / „Rückgängig“). Grund: man ändert oft und schließt sofort;
   heute überlebt der Toast das Schließen, das soll nicht verloren gehen. Alternative: verfällt beim Schließen.
3. **Rückgängig am Feld auch im Panel am PC?** Empfehlung: nein, am PC bleibt der Toast (R3 sagt das so). Wenn du es dort
   auch willst, wird es eine eigene Task.

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
