---
id: budgeting-y1kkr
title: "„Rückgängig“ nach einer Verschiebung im Sheet „Geld“: Verschiebung in die Gegenrichtung mit demselben Betrag, für Holen, Abgeben und den Fehlbetrag-Knopf, nur wenn alles geschrieben ist"
status: done
type: feature
context: budgeting
created: 2026-11-19
completed:
depends_on: [budgeting-gtrgk]
blocks: []
tags: [design-exploration, budget, frontend]
related_adrs: [0099, 0083, 0069, 0055, 0084, 0098]
related_research: []
prior_art: [ledger-vf5h6, budgeting-afd8h, budgeting-c5w2n]
---

## Why
Roman am 2026-11-19 bei der Freigabe der Skizze ledger-vf5h6 (Version 9): nach einer Verschiebung soll es „Rückgängig“ geben
(„ja“). Heute gibt es das nicht; wer sich vertippt oder die falsche Gegenseite gewählt hat, muss die Verschiebung von Hand
zurück anlegen. Skizze, Notiz unter „Budget: Geld verschieben im Sheet“: „Entschieden am 19.11.: es kommt, als
Verschiebung in die Gegenrichtung.“ Abgespalten aus budgeting-gtrgk.

## What
Entscheidung in ADR 0099 (`.agentheim/knowledge/decisions/0099-rueckgaengig-nach-verschiebung.md`, Status accepted,
Roman hat die Punkte (a) bis (d) am 2026-11-19 entschieden).

- **Wo:** rechts in der Ergebniszeile über dem Hauptknopf des Tabs Geld (budgeting-gtrgk), Textaktion „Rückgängig“ in
  Akzentfarbe (Skizze `.mk-msg .undo`): „✓ 50,00 € nach Reise · Rückgängig“. Am PC (ab 1024 px) steht dieselbe Zeile im
  Panel über dem Hauptknopf, ohne Toast (ADR 0098, Entscheidung Roman 2026-11-19; budgeting-gtrgk).
- **Für welche Wege:** Holen, Abgeben (Kategorie ↔ Kategorie oder ↔ Zu verteilen) und den Fehlbetrag-Knopf. Nicht für das
  Feld Zugewiesen (absolut, Schlange; der Rückweg ist, den alten Wert zu tippen; Entscheidung Roman) und nicht für
  Targets anwenden (mehrere Zeilen, Bericht im Dialog).
- **Wann angeboten:** nur wenn jede Zeile des Berichts geschrieben ist (`Written`). Bei Fehlgeschlagen gilt „Erneut
  senden“ (Offene Verschiebung), bei „In YNAB geändert, nichts überschrieben“ und „in BB zurückgezogen“ gibt es keins.
- **Was es tut:** ein normales Verschieben mit vertauschter Quelle und vertauschtem Ziel, demselben Betrag, an den Monat
  der ursprünglichen Verschiebung (nicht den gerade gezeigten), mit frischer Befehls-Id. Relativ wie jede Verschiebung
  (ADR 0069, Entscheidung Roman): Änderungen in YNAB oder BB dazwischen bleiben erhalten (ADR 0055). Neues Entscheidungsereignis
  (`MoneyMoved` bzw. `AssignmentChangedInBb`, wie `moveEventTypes` es heute ergibt), das Original bleibt unverändert
  (ADR 0083). Kein neuer Ereignistyp, kein neues Feld, Server unverändert.
- **Quelle:** der Client merkt sich den gesendeten Auftrag (`MoveRequest`) mit Monat und Bericht der letzten Verschiebung,
  nicht aus dem Bericht zurückgerechnet (`moveSummaryText` erkennt Zu verteilen nur am Vorzeichen).
- **Wie lange:** nur für die letzte Verschiebung. Das Angebot endet mit dem nächsten Senden jeder Art (Holen, Abgeben,
  Fehlbetrag, Zuweisen, Pause, Target), mit Kategorie- oder Monatswechsel, Schließen des Sheets, Verlassen der Seite und
  Neuladen der App. Der Wechsel zwischen Zuweisen, Holen und Abgeben beendet es nicht (Entscheidung Roman). Sonst kein
  Zeitlimit (ytgnc).
- **Danach:** die Zeile zeigt das Ergebnis des Rückgängig ohne Aktion: „✓ Rückgängig: 50,00 € zurück von Reise“ (Text
  aus Sicht der gewählten Kategorie, Muster wie die Erfolgszeile in budgeting-gtrgk). Ein Rückgängig des Rückgängig gibt
  es nicht. Scheitert es, steht die rote Zeile wie bei jeder Verschiebung („Nicht vollständig verschoben: …“ mit „Erneut
  senden“, oder die Ablehnung des Servers, etwa `CategoryNotVisibleText`). „Verfügbar“ leuchtet wie nach jeder
  Verschiebung.
- **Doppeltipp:** während des Aufrufs gesperrt (`canSend`); ein Neuversuch nach Status 0 nutzt dieselbe Id
  (`Retry.command`), es geht nie zweimal zurück.

## Funktionsabgleich
- Verschieben, Holen, Abgeben, Fehlbetrag-Knopf: **bleiben** unverändert, auch der Server.
- Ergebniszeile aus budgeting-gtrgk: **ändert sich** (rechts „Rückgängig“).
- Budget-Historie und Allgemeine Historie: **bleiben**; ein Rückgängig erscheint als zweite normale Bewegung („Verschieben“
  bzw. „Zuweisen“ in Gegenrichtung), ohne Vermerk „rückgängig gemacht“ (Entscheidung Roman).
- Offene Verschiebung mit „Erneut senden“: **bleibt**.

## Acceptance criteria
- [ ] Reine Funktion `undoRequest` (Name frei) in `src/Client/Components/Budget/Moves.fs`: vertauscht Quelle und Ziel,
      behält Betrag und Monat, nimmt die Befehls-Id als Parameter; Tests für Kategorie ↔ Kategorie, Kategorie → Zu
      verteilen, Zu verteilen → Kategorie (Fehlbetrag-Knopf).
- [ ] Reine Funktion für das Angebot: „Rückgängig“ nur, wenn jede Zeile `Written` ist; kein Angebot bei Failed,
      ChangedInYnab, WithdrawnInBb, beim Feld Zugewiesen und bei Targets anwenden. Tests je Fall.
- [ ] Lebensdauer unter .NET auf der Update-Funktion, als Aufzählung der Reihenfolgen (Vorbild `moveWays` in
      `src/Tests/MoveMoneyClientTests.fs`): nächstes Senden (Holen, Abgeben, Fehlbetrag, Zuweisen, Pause), Kategorie-,
      Monatswechsel, Sheet schließen, Antwort auf das Rückgängig, Doppeltipp (genau ein Auftrag); Moduswechsel
      (Zuweisen, Holen, Abgeben) lässt das Angebot stehen.
- [ ] Das Rückgängig geht an den Monat der Verschiebung, auch wenn inzwischen ein anderer Monat gezeigt würde (Test).
- [ ] Domäne: Rundlauf-Eigenschaft für `planMove` (`src/Server/Budgeting/Domain.fs`): Verschieben und die Gegenrichtung
      mit demselben Betrag ergeben das alte Soll beider Seiten (FsCheck oder Tabelle, Test-Doktrin).
- [ ] Nach dem Rückgängig steht „✓ Rückgängig: …“ ohne Aktion; ein Fehler steht rot wie bei jeder Verschiebung.
- [ ] Am PC steht „Rückgängig“ in der Zeile im Panel über dem Hauptknopf mit derselben Wirkung, kein Toast (Test auf die
      reine Funktion der Zeile).
- [ ] Browser: `[flow:geld-verschieben]` (`scripts/move-money.test.mjs:128`) endet mit „Rückgängig“ in der Zeile: der
      zweite Auftrag an `moveMoney` hat Quelle und Ziel vertauscht, denselben Betrag und eine andere `CommandId`; die
      Zeilen zeigen wieder die alten Beträge. Kein neuer Flow (je Flow höchstens ein Test).
- [ ] Sweep: Zustand `budget-geld-ergebnis` aus budgeting-gtrgk zeigt jetzt „Rückgängig“; der Knopf bleibt ohne Scrollen
      im Sheet, „Rückgängig“ mindestens 44 px hoch tippbar (Invariante des Sweeps); bei 1280 steht „Rückgängig“ im Panel.
- [ ] README budgeting: neuer Begriff **Rückgängig (Verschiebung)** im README_DELTA, Verweis auf ADR 0099.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome: Zeile mit „Rückgängig“ und nach dem
      Rückgängig; PC 1280 mit der Zeile im Panel, ohne Toast), Abweichungen nur die in dieser Task genannten.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Entscheidungen Roman (2026-11-19, auch in ADR 0099)
- (a) **Historie:** zwei normale Bewegungen ohne Vermerk. Folge: kein Verweis `undoes`, kein neues Feld in `src/Shared`,
  keine Fixture-Änderung.
- (b) **Moduswechsel:** das Angebot endet beim nächsten Senden, nicht beim Wechsel Holen, Abgeben, Zuweisen. Folge:
  Lebensdauer-Test prüft den Moduswechsel als Fall ohne Ende.
- (c) **Feld Zugewiesen:** kein Rückgängig. Folge: das Angebot fehlt dort (Test).
- (d) **Relativ:** derselbe Betrag zurück, kein absoluter Stand. Folge: Änderungen aus YNAB dazwischen bleiben erhalten
  (ADR 0055).

## Notes
Abgespalten 2026-11-19 aus budgeting-gtrgk. Fachliche Analyse: tactical-modeler beim Verfeinern (Ergebnis in ADR 0099).

**Skizze:** `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/31-budget-sheet-geld.png`
(„✓ 50,00 € nach Reise · Rückgängig“), Quelltext `.../scratchpad/vf5h6/skizze.src.html` Zeilen um 433 bis 455.
Heute: `.../scratchpad/shots/vf5h6/heute-budget-geld-zuweisen-412.png`.

**Code:** `src/Client/Components/Budget/Moves.fs` (`confirmTransfer`, `confirmMissing`, `applyMoveAnswered`, `moveToast`,
`moveSummaryText`, `noteMoveReport`, `startMoveRetry`), `src/Client/Components/Budget/State.fs` (`startMove`, `moveCmd`),
`src/Client/Types.fs:323` (`ToastActionKind`), Server `src/Server/Budgeting/CommandHandlers.fs`
(`moveMoneyWith`) und `Domain.fs` (`planMove`, `moveEventTypes`) nur lesen.

**Abweichung von der Skizze:** keine im sichtbaren Teil. Den Text nach dem Rückgängig zeigt die Skizze nicht; er folgt dem
Muster der Erfolgszeile.


## Outcome

„Rückgängig“ nach einer Verschiebung im Sheet „Geld“ ist umgesetzt (ADR 0099). Nur Client: kein neuer Ereignistyp, kein neues Feld in `src/Shared`, Server unverändert, keine Fixture-Änderung. `./verify.sh`: `VERIFY: PASS` (dotnet 5297, node 354).

**Modell** (`src/Client/Components/Budget/Types.fs`)
- `SentMove` {Month; Request}: der gesendete Auftrag mit Monat, Quelle für „Rückgängig“, nicht aus dem Bericht zurückgerechnet.
- `TabSend` (`ForwardMove of SentMove` | `UndoingMove`) in `Model.TabSend`: was der laufende Aufruf aus dem Tab ist. Gesetzt von `confirmTransfer`, `confirmMissing`, `confirmUndo`; geleert von `applyMoveAnswered` und `applyMoveCallLost`.
- `SheetLine.Undo: SentMove option`: das Angebot lebt in der Zeile und endet mit ihr (Lebensdauer aus budgeting-gtrgk: `endsSheetLine`, `sheetKey`, Schließen, Seite verlassen; Moduswechsel und Tabwechsel lassen es stehen). Ein Neuladen der App löscht es, weil es nur im Client lebt.
- Neue Nachricht `UndoMove` (Route `StartsMove` wie `ConfirmTransfer`, also gesperrt per `canSend` und `moveGate`).

**Reine Funktionen** (`src/Client/Components/Budget/Moves.fs`, Abschnitt „Rückgängig“)
- `undoRequest commandId sent`: Quelle und Ziel vertauscht, Betrag und Monat bleiben, Befehls-Id als Parameter.
- `undoOffer send report`: nur bei `ForwardMove` und jeder Zeile `Written`; kein Angebot bei Failed, ChangedInYnab, WithdrawnInBb, leerem Bericht, nach dem Rückgängig selbst und ohne Auftrag aus dem Tab (Feld Zugewiesen, Targets anwenden).
- `undoneLineText` / `undoneLine`: „Rückgängig: 50,00 € zurück von Reise“ (aus Sicht der gewählten Kategorie, Muster von `moveLineText`, das dafür in `perspectiveParts` zerlegt ist); Fehler und Warnung wie bei jeder Verschiebung. Eine Ablehnung des Servers nach dem Rückgängig steht als rote Zeile „Nicht verschoben: …“ (keine Absage im Formular, das schon zurückgesetzt ist).
- `sheetLineAction`: die Aktion rechts in der Zeile, in Sheet und Panel gleich („Erneut senden“ vor „Rückgängig“).
- `confirmUndo`: nimmt das Angebot aus der Zeile, die Zeile endet, `MoveInFlight` läuft.
- `src/Client/Components/Budget/State.fs`: `undoCmdWith` (über `Retry.command`: frische Id je Absicht, derselbe zweite Versuch nach Status 0 trägt dieselbe Id), Zweig `UndoMove`.
- `src/Client/Components/Budget/MovesView.fs`: `sheetLineView` nutzt `sheetLineAction`; „Rückgängig“ in Akzentfarbe über `Feedback.sheetLine` (vorhandenes `Act`, 48 px Trefferfläche). Kein neuer DS-Baustein.

**Tests**
- `src/Tests/MoveMoneyClientTests.fs`: „Rückgängig: Auftrag“ (3, je Weg), „Rückgängig: Angebot“ (11: je Ausgang, Fehlbetrag-Knopf, Feld Zugewiesen über `update`, Targets anwenden über `update`, Panel ohne Toast, Leiste ohne Rückgängig), „Rückgängig: Ablauf“ (5: genau ein Auftrag und Doppeltipp, Monat der Verschiebung über `confirmUndo` und den echten `undoCmdWith` mit Fake-Aufruf, Text danach ohne Aktion und „Verfügbar“ leuchtet, Sicht von Holen, Fehler/Ablehnung/Abbruch), „Rückgängig: Lebensdauer“ (2: Aufzählung aller Reihenfolgen bis Tiefe 4 über Rückgängig, Abgeben, Holen, Fehlbetrag, Zuweisen, Pause, Kategorie, Monat, Schließen, Seite verlassen, Moduswechsel, Neu-Lesen, Antworten; prüft Ende, Stehenbleiben, höchstens ein Auftrag je Angebot, neues Angebot nur für die letzte Verschiebung; dazu Moduswechsel einzeln). Bestehende Erwartungen an `SheetLine` um `Undo` ergänzt; `okLine` erwartet nach dem Abgeben jetzt das Angebot, damit prüfen die Lebensdauer-Tests aus gtrgk es mit.
- `src/Tests/MoveMoneyTests.fs`: „Rückgängig: Rundlauf“ für `planMove`: FsCheck-Eigenschaft (500 Fälle, drei Wege, auch negative Stände) und eine Tabelle: hin und zurück mit demselben Betrag ergibt das alte Soll beider Seiten. Die Domäne ist unverändert, der Test war also sofort grün (dokumentiert die Annahme aus ADR 0099 Punkt 3).
- Gegenprobe (lokal, wiederhergestellt): Quelle/Ziel nicht vertauscht: 4 rot; Angebot ohne Prüfung der Ausgänge: 6 rot; `confirmUndo` ohne Sperre und ohne Ende der Zeile: 2 rot; Rückgängig des Rückgängig erlaubt: 5 rot.
- Browser: `[flow:geld-verschieben]` (`scripts/move-money.test.mjs`) endet jetzt mit „Rückgängig“: Zeile „✓ 20,00 € nach Urlaub afd8h Rückgängig“, Klick, „✓ Rückgängig: 20,00 € zurück von Urlaub afd8h“ ohne Aktion, kein Toast; der zweite `moveMoney`-Auftrag hat Quelle und Ziel vertauscht, denselben Betrag, denselben Monat und eine andere `CommandId`; die Zeilen zeigen wieder 80,00 € und -30,00 €. Kein neuer Flow, `FLOWS`-Satz in `scripts/browser-usage.mjs` ergänzt.
- Sweep (`scripts/layout-states.mjs`): `budget-geld-ergebnis` wartet auf „Rückgängig“ in der Zeile; 320, 390, 412 und 1280 ohne Verletzung (Invariante 2: Knopf 90 × 48 px; Invariante 7: Hauptknopf ohne Scrollen, auch mit Tastatur), ohne neue Ausnahme.

**README_DELTA**: geprüft mit `npm run check:readme-delta -- --bc budgeting` (Exit 0, „Ganz nachlesen: keine Einträge“). Neu: „Rückgängig (Verschiebung)“; ersetzt: „Zeile im Sheet“ (Satz zu grün mit „Rückgängig“).

**Skizzenvergleich** (Bilder in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/result-shots/budgeting-y1kkr/`)
- `budget-geld-ergebnis-412.png` neben `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/31-budget-sheet-geld.png`: ✓ grün, „5,00 € nach Reise“ halbfett links, „Rückgängig“ in Akzentfarbe rechts, darunter „Abgeben“. Unterschiede nur die aus budgeting-gtrgk bekannten (BB hat Feld „Betrag“ und „von/nach“, deshalb ist der Inhalt gescrollt; das Leuchten ist auf dem Bild nicht zu sehen).
- `budget-geld-rueckgaengig-412.png` (keine Skizzen-Ansicht, Text laut Task nach dem Muster der Erfolgszeile): „✓ Rückgängig: 5,00 € zurück von Reise“ ohne Aktion.
- `budget-geld-ergebnis-1280.png` und `budget-geld-rueckgaengig-1280.png`: dieselbe Zeile im Panel über „Abgeben“, kein Toast (0 Elemente in `.toast-layer`).

**Echte Namen**: Zeile `data-testid="feedback-sheet-line"`, Knopf `[data-feedback-action]` mit Text „Rückgängig“, Nachricht `UndoMove`.

**Kann-Sammlung budgeting**
- Nach erfolgreichem „Erneut senden“ (alle Zeilen geschrieben) gibt es kein „Rückgängig“; die Task nennt nur die letzte Verschiebung aus dem Tab. Bei Bedarf nachziehen.
- Ein Doppeltipp auf „Rückgängig“ zeigt beim zweiten Tipp die gelbe Zeile „Eine Verschiebung läuft noch“ (wie beim Doppeltipp auf Abgeben); der Auftrag geht nur einmal hinaus.
