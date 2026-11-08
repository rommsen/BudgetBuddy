---
id: ledger-qvjvs
title: "Buchung zum Übertrag machen: Konten im Kategorie-Picker des Kassenbuch-Details, Schreibweg nach YNAB"
status: done
type: feature
context: ledger
created: 2026-11-08
completed:
depends_on: []
blocks: []
tags: [etappe-2, ynab, uebertrag, picker]
related_adrs: [0055, 0087, 0021, 0089]
related_research: []
prior_art: [ledger-vq2g8]
---

## Why
Roman kann in BB eine einzelne Buchung nicht zum Übertrag auf ein anderes Konto machen, nur eine Split-Zeile.
Dafür muss er heute nach YNAB. Etappe 2 ("nichts zwingt mehr zu YNAB außer Reports"). Ersetzt in Etappe 2 den
Kategorie-Teil von design-system-taytp, das nach Etappe 3 wandert (Roman, 2026-11-08). Vorlage: Prototyp Version 17
(Picker "Kategorie oder Konto" mit Abschnitt "Konten (Übertrag)", Übertrag mit Gegenbuchung).

## What
- Im Buchungs-Detail des Kassenbuchs bietet der Kategorie-Picker (`CategoryPicker.popoverInPanel` am PC,
  `sheetLayered` am Handy) dieselben Konten an wie im Split: `Accounts = transferTargetsFor model entry` (heute
  `Accounts = []`), mit Abschnitt "Übertrag auf Konto", Suche und Präfix "Konto:" wie im Styleguide §2 "Auswahl".
- Wählt Roman ein Konto, wird die Buchung ein Übertrag auf dieses Konto: ein neuer Befehl im Kassenbuch nach dem Muster
  von `setEntryCategory` (Validierung in `src/Server/Validation.fs`, Befehl in `CommandHandlers.fs`, Ereignis im
  Log). Der Push setzt in YNAB `payee_id` auf die `transfer_payee_id` des Zielkontos und keine `category_id` (wie
  `encodeSubtransaction` für Split-Ziele, `src/Server/YnabClient.fs`). YNAB legt die Gegenbuchung an; der Spiegel
  verknüpft sie über den vorhandenen Weg (`linkTransferCounterpart`, `EntryTransferCounterpartObservedInYnab`).
- Die Zeile "Kategorie" im Detail zeigt danach "Übertrag → <Konto>"; die Liste zeigt den Übertrag wie andere
  Überträge.
- Zurück: wählt Roman bei einem Übertrag wieder eine Kategorie, wird die Buchung wieder eine normale Buchung mit
  ihrem Payee aus BB; YNAB entfernt die Gegenbuchung.
- Rückmeldung wie beim Kategorie-Wechsel: Toast mit "Rückgängig"; ein gescheiterter Push bleibt sichtbar wie beim
  Kategorie-Push (Gelb mit "Erneut senden", ADR 0087 Nachtrag).
- Nur einzeln im Detail. Die Auswahl-Leiste ("Kategorie setzen" für mehrere) zeigt weiter keine Konten (Roman).

Nicht Teil: Erfassen einer neuen Buchung als Übertrag (Etappe 2, "Buchung erfassen für jedes Konto"), Import und
Quick Add (Etappe 3).

## Acceptance criteria
- [ ] Handler-Test: der Befehl auf einer verknüpften Buchung mit einem Konto als Ziel schreibt das Ereignis und ruft
      den Push mit `payee_id` = `transfer_payee_id` des Zielkontos und ohne `category_id` (Port als Funktionswert,
      Vorbild `MemoProbe`).
- [ ] Handler-Test: Ablehnung ohne Push für unbekannte Buchung, in YNAB gelöschte Buchung, nicht verknüpfte Buchung,
      das eigene Konto als Ziel, ein Konto, das kein offenes Budget-Konto ist, und eine Buchung, deren Kategorie nicht
      änderbar ist (dieselben Regeln wie `setEntryCategory`, Doktrin Punkt 4). Jede Ablehnung mit konkreter Meldung.
- [ ] Handler-Test: scheitert der Push, steht der Fehlschlag mit Grund im Log und das Detail kann ihn anzeigen; ein
      späterer Erfolg löst ihn auf.
- [ ] Handler-Test: Übertrag zurück auf eine Kategorie sendet `category_id` und den Payee der Buchung, ohne
      Übertrags-Payee.
- [ ] Spiegel-Test: nach dem Push beobachtet der Spiegel die Buchung mit `transfer_transaction_id`; die Gegenbuchung
      auf dem Zielkonto wird verknüpft und nicht als fremde zweite Buchung angelegt.
- [ ] Client-`update` unter .NET: die Wahl eines Kontos im Picker des Details schickt den neuen Befehl, die Wahl einer
      Kategorie weiter `setEntryCategory`; die Auswahl-Leiste bietet keine Konten an.
- [ ] Eine Zeile in `src/Tests/RemotingEndpointNegativeTests.fs` für den neuen Endpoint, falls einer entsteht
      (Doktrin Punkt 11).
- [ ] Wie YNAB eine bestehende Buchung per PUT zum Übertrag macht und wieder zurück (Felder, Antwort, Gegenbuchung),
      steht mit Quelle im Outcome.
- [ ] Seite neben dem Prototyp Version 17 verglichen (Picker "Kategorie oder Konto" mit Konten unten, Zeile
      "Übertrag → Konto" im Detail), Abweichungen nur die genannten; Screenshots bei 412 px und am PC im Outcome.
      [human-eye]

## Notes
- Prototyp: https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Der Conductor legt Screenshots in den
  Scratchpad und gibt die Pfade an Worker und Verifier (CLAUDE.md, Skizzen).
- Abweichung vom Prototyp: Der Abschnitt heißt in BB wie im Styleguide ("Übertrag auf Konto", Zeilen "→ Name"), der
  Prototyp schreibt "Konten (Übertrag)". Grund: Styleguide §2, Split und Detail nutzen denselben Picker.
- Ereignisname und Payload klärt der Worker mit dem tactical-modeler (Ereignis in Vergangenheitsform, trägt, was der
  Push braucht: Zielkonto und dessen `transfer_payee_id` oder YNAB-Konto-Id).
- Stellen: `setEntryCategory` (`src/Server/Ledger/CommandHandlers.fs:1825`), `pushCategory`, `transferTargetsFor`
  (`src/Client/Components/Ledger/View.fs:1822`), Picker im Detail (`View.fs:1530`, `Accounts = []`), Split-Ziele
  (ledger-vq2g8, design-system-bym4m).
- Fable.Remoting: ein neues Pflichtfeld in einem Record aus `src/Shared/` zieht die Fixtures in `scripts/*.test.mjs`
  im selben Schritt mit.

## Verifier note (iteration 1)

REASONS:
- Check 5 (README sync): nach der Integration widerspräche sich die ledger-README. Der Bullet `*KategorieÄndern*` unter `## Key commands` (README.md Zeile 320 im Worktree) nennt weiter "Übertrag ohne Kategorie und ohne Split-Zeilen" unter den Fällen, die `setEntryCategory` ablehnt. Der Diff entfernt diese Ablehnung (`setEntryCategory` ruft bei `SkipTransfer` jetzt `clearTransfer`). Das Delta hängt nur einen neuen Bullet `*ÜbertragSetzen*` an und hat kein replace für den alten. CLAUDE.md ("README-Deltas", Lehre aus budgeting-eh8xe) verlangt, jeden Bullet nachzuziehen, der dieselbe Regel nennt.
- Check 6b (ADR 0055): ADR 0089 Punkt 4 beschränkt ADR 0055 Punkt 7 auf die Sammelaktion, aber es gibt keinen Nachtrag an ADR 0055. Wer ADR 0055 Punkt 7 liest, findet die alte Regel weiter in Kraft. Hauskonvention für eine Teiländerung: ein Nachtrag an 0055 Punkt 7 mit Verweis auf 0089 (wie 0055 "Nachtrag 2026-11-07").
- Sonst sauber: `./verify.sh` VERIFY: PASS, alle maschinell prüfbaren Kriterien mit Tests belegt, YNAB-Quelle im Outcome, Form von ADR 0089 korrekt.
- Human-eye (Info): Screenshots vorhanden, Abweichungen 2 bis 4 akzeptabel als gemeldete Abweichungen. Nicht genannt: der Prototyp zeichnet Kategorie- und Kontengruppen als gerahmte Karten, der DS-Picker in BB zeigt schlichte Zeilen.

SUGGESTED_FIX: Ein `replace` im README_DELTA für den Bullet `*KategorieÄndern*` in `## Key commands`, der "Übertrag ohne Kategorie und ohne Split-Zeilen" aus der Ablehnungsliste nimmt und an seiner Stelle die verbleibende Ablehnung eines Übertrags aus YNAB ohne bekannten Payee davor (`transferUnknownPayeeReason`) und den Weg zurück über `clearTransfer` nennt. Dazu einen Nachtrag für ADR 0055 Punkt 7 mit Verweis auf ADR 0089 melden und die Abweichung "Karten statt schlichter Zeilen" im Outcome ergänzen.

ITERATION_HINT: likely-fixable

## Merge-conflict note (iteration 2)

- Geschwister: budgeting-pscgb (Kategorien verwalten, Commit 2d0bfe25) hat in `src/Server/Validation.fs` die Prüfungen `validateCategoryName`, `validateCategoryGroupName`, `validateCategorySave` und `validateCategoryGroupSave` ergänzt.
- Neue Basis: 29a8920dc1660a1bb275faeae0040a04f66aeee0 (`main`, schon mit ledger-p3vq8 und budgeting-pscgb).
- Allow-list: `src/Server/Validation.fs`.
- Rung-1-Patch: `.agentheim/salvage/ledger-qvjvs-merge-conflict.patch`.

## Outcome

Eine Buchung lässt sich im Buchungs-Detail zum Übertrag auf ein anderes offenes Budget-Konto machen und wieder zurückholen.

- Server: neuer Befehl `setEntryTransfer` (`src/Server/Ledger/CommandHandlers.fs`, Port `TransferWriteTools`), API `setEntryTransfer` mit `Validation.validateTransferRequest`. Ereignisse `EntryTransferTargetSetInBb` und `EntryTransferTargetClearedInBb` (`src/Server/Ledger/Events.fs`, `Domain.fs`), neues Zustandsfeld `PayeeBeforeTransfer` (Spalte `payee_before_transfer`), Baseline des Fehlschlags mit `BaselineTransferAccountId`. Der Weg zurück läuft über `setEntryCategory`. YNAB-Seite: `YnabClient.fetchTransferAccounts`, `buildTransferBody`, `sendTransfer`, `updateTransfer`. Erneut senden (`retryCategorizationOrSplitWriteBack`) kennt den Übertrag. Historien-Sätze in `QueryHandlers.describe`, `LedgerEntryView.TransferAccountId`, `CategoryChangeOutcome.PreviousTransferAccountId`.
- Client: der Picker im Detail bietet die Konten an (`View.detailPickerProps`, `transferTargetsFor`), ein Konto schickt `StartSetEntryTransfer`, eine Kategorie weiter `StartSetEntryCategory` (`View.detailPickerMsg`). Die Zeile Kategorie zeigt "Übertrag → <Konto>" (`transferCategoryLabel`). Ein Übertrag ist nicht mehr gesperrt (`CategoryLockedTransfer` entfällt). Toast mit Rückgängig (`UndoTransfer` bzw. `UndoCategory`), danach lädt die Buchung nach. Die Auswahl-Leiste bietet keine Konten (`View.batchPickerProps`).
- Tests: `src/Tests/LedgerTransferTests.fs` (Handler mit Port als Funktionswert: Ereignis vor dem PUT, PUT mit `transfer_payee_id` ohne `category_id`; jede Ablehnung ohne Ereignis und PUT; Fehlschlag mit Grund im Log und im Detail, Erneut senden löst ihn auf; zurück mit `category_id` und Payee aus BB; Übertrag aus YNAB; Spiegel-Test mit Gegenbuchung; Body und Antwort des PUT gegen Fake-Handler; Validierung), Client-Tests in `src/Tests/LedgerCategoryChangeClientTests.fs`, Zeile in `RemotingEndpointNegativeTests.fs`. Angepasst: der Layout-Zustand `kassenbuch-kategorie-toast` (`scripts/layout-states.mjs`) sucht das Sheet jetzt über den Titel "Ziel wählen". `./verify.sh`: VERIFY: PASS (dotnet 4312 grün, node 309 grün), in Iteration 2 erneut.
- Merge-Konflikt beim Zurückführen nach main (Iteration 3): `src/Server/Validation.fs`, beide Seiten hatten am Dateiende angefügt. Aufgelöst, indem `validateTransferRequest` (diese Task) und die Kategorie-Prüfungen von budgeting-pscgb (`validateCategoryName`, `validateCategoryGroupName`, `validateCategorySave`, `validateCategoryGroupSave`) unverändert untereinander stehen. Durch den Merge brach ein Test: ledger-p3vq8 hat `CashExpenseAddedPayload` um das Pflichtfeld `ImportId` erweitert; der Testfall "nicht verknüpft" in `src/Tests/LedgerTransferTests.fs` setzt jetzt `ImportId = None`. `./verify.sh` mit main (budgeting-pscgb, ledger-p3vq8) erneut: VERIFY: PASS (dotnet 4354 grün, node 309 grün).
- Tactical-modeler direkt befragt (Ereignisnamen, Payload, `PayeeBeforeTransfer`, Baseline mit Ziel). Entscheidung als ADR `0089-buchung-zum-uebertrag-in-bb.md` (vorläufige Nummer).

**YNAB: bestehende Buchung per PUT zum Übertrag und zurück.** Quelle: YNAB OpenAPI-Spezifikation `https://api.ynab.com/papi/open_api_spec.yaml`, Version 1.89.0, gelesen am 2026-11-08 (kein Aufruf der Live-API). `PUT /plans/{plan_id}/transactions/{transaction_id}` (`updateTransaction`) nimmt `SaveTransactionWithOptionalFields`. Zum Feld `payee_id`: "The payee for the transaction. To create a transfer between two accounts, use the account transfer payee pointing to the target account. Account transfer payees are specified as `transfer_payee_id` on the account resource." Zu `payee_name`: "If a `payee_name` value is provided and `payee_id` has a null value, the `payee_name` value will be used to resolve the payee ..." Antwort 200 `TransactionResponse`, darin `transfer_account_id` ("If a transfer transaction, the account to which it transfers") und `transfer_transaction_id` ("the id of transaction on the other side of the transfer"); sonst 400. Daraus folgt der Body: zum Übertrag nur `payee_id` = `transfer_payee_id` (vom Konto aus `GET .../accounts`), keine `category_id`; zurück `category_id`, `payee_id` = null und `payee_name`. Dass YNAB die Gegenbuchung beim PUT anlegt und beim Zurück entfernt, sagt die Spezifikation nicht ausdrücklich. Es folgt aus dem doppelseitigen Übertrag (Antwort mit `transfer_transaction_id`) und deckt sich mit der Recherche `ynab-transfer-in-split-subtransaction-2026-06-13` (dort ebenfalls als "inferred" vermerkt). BB prüft darum die Antwort: ein 200 ohne `transfer_account_id` gilt als Fehlschlag, und der Spiegel verknüpft die Gegenbuchung, sobald YNAB sie liefert.

**Seite neben dem Prototyp Version 17 [human-eye]** (Screenshots BB mit gemockter API, nicht im Repo):
- 412 px: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-handy-detail-uebertrag.png` (Zeile "Kategorie: Übertrag → Tagesgeld ING"), `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-handy-picker-oben.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-handy-picker-konten.png` (Abschnitt "Übertrag auf Konto" unten, Ziel angehakt)
- PC 1440: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-pc-detail-uebertrag.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-pc-picker-konten.png`
- Aufnahme-Skript: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/qvjvs-shots/shoot.mjs`

Abweichungen von der Skizze (Roman bitte prüfen):
1. Genannt in der Task: Abschnitt "Übertrag auf Konto" mit Zeilen "→ Name" statt "Konten (Übertrag)".
2. Nicht genannt, Folge desselben Grundes (ein Picker für Split und Detail, Styleguide §2): der Sheet-Titel heißt "Ziel wählen" statt "Kategorie oder Konto", und die Konten-Zeilen zeigen keinen Kontostand (Prototyp: "1.950,00 €"). Beides ist DS-Verhalten des gemeinsamen Pickers, auch im Split und im Import; eine Änderung wäre eine DS-Variante.
3. Am PC öffnet der Picker als Popover im Panel (so verlangt die Task, `popoverInPanel`), der Prototyp zeigt einen Dialog in der Mitte.
4. Der Payee eines Übertrags heißt wie in YNAB "Transfer : Tagesgeld ING" (BB-Bestand), der Prototyp schreibt "Übertrag Tagesgeld ING".
5. Der Prototyp zeichnet die Kategorie- und Kontengruppen im Picker als gerahmte Karten, der DS-Picker in BB zeigt schlichte Zeilen unter den Gruppenköpfen. Grund: derselbe Baustein `CategoryPicker` dient Detail, Split, Import und Auswahl-Leiste und folgt Styleguide §2 ("Auswahl"); gerahmte Karten wären eine neue DS-Variante für alle diese Stellen, nicht Teil dieser Task.

**Nachtrag ADR 0055 (vom Conductor einzutragen)**

Wortlaut zum Anhängen an ADR 0055 (Markdown, ohne die Zaunzeilen):

```markdown
## Nachtrag 2026-11-08, Übertrag in BB setzen (ledger-qvjvs)
Punkt 7 ist eingeschränkt (ADR 0089). Ein Übertrag zwischen Budget-Konten ohne Kategorie und ohne Split-Zeilen ist nur noch in der Sammelaktion gesperrt (`setEntryCategoryBatch` überspringt ihn weiter, `SkipTransfer`). Im Buchungs-Detail ist er änderbar: ein Konto im Picker wechselt das Ziel (`setEntryTransfer`), eine Kategorie macht ihn über `setEntryCategory` wieder zur normalen Buchung mit dem Payee aus BB vor dem Übertrag (`EntryTransferTargetClearedInBb`, PUT mit `category_id`, `payee_id` null und `payee_name`). Abgelehnt bleibt ein Übertrag aus YNAB, dessen Payee davor BB nicht kennt ("BB kennt den Payee vor dem Übertrag nicht, bitte in YNAB ändern"). Der Fehlschlag aus Punkt 4 trägt dafür das optionale Feld `BaselineTransferAccountId`; der Vergleich aus Punkt 5 nimmt bei offenem Fehlschlag das Übertrags-Ziel hinzu, wenn die Baseline ein Ziel trägt oder der BB-Stand ein in BB gesetzter Übertrag ist. Ein Übertrag in ein Tracking-Konto mit Kategorie bleibt wie bisher über `setEntryCategory` änderbar.
```

**Von Hand nachzuziehen (README ledger)**

`.agentheim/knowledge/contexts/ledger/README.md`, Zeile 320, Bullet `*KategorieÄndern*` unter `## Key commands`. Per replace nicht erreichbar: der Abschnitt hat keinen Bullet mit fettem Kopf (Gate: Prüfung 6, Anker-Erreichbarkeit). Neuer Wortlaut des ganzen Bullets:

- *KategorieÄndern*: `Ledger.CommandHandlers.setEntryCategory` (eine Buchung, `YnabCategoryId option`, `None` heißt ohne Kategorie) und `retryCategorizationWriteBack` (ADR 0055, `ledger-k2rbn`). Reihenfolge wie beim Prüfvermerk: erst `EntryRecategorized` per `appendAndApply`, dann der Push (`YnabClient.updateCategory`, PUT mit nur `category_id`). Abgelehnt werden (Fehler, kein Ereignis): unbekannte oder in YNAB gelöschte Buchung, Split-Buchung, unbekannte Kategorie-Id, eine Buchung, deren offener Fehlschlag noch einen Split als Baseline trägt (ADR 0059 Punkt 3). Ziel gleich Ist (`categorizationIdentity`): kein Ereignis, kein Push, `CategoryUnchanged`. Das Rückgängig im Toast ist derselbe Befehl mit `Previous` aus der Antwort. Bei einem Übertrag zwischen Budget-Konten (ohne Kategorie, ohne Split-Zeilen) ist der Befehl seit ledger-qvjvs der Weg zurück zur normalen Buchung (`clearTransfer`, ADR 0089): `EntryTransferTargetClearedInBb`, dann der PUT mit `category_id`, `payee_id` = null und `payee_name` = Payee aus BB vor dem Übertrag (`Entry.PayeeBeforeTransfer`); die Antwort nennt das Ziel davor (`PreviousTransferAccountId`). Abgelehnt bleibt ein Übertrag aus YNAB, dessen Payee davor BB nicht kennt (`transferUnknownPayeeReason`: "BB kennt den Payee vor dem Übertrag nicht, bitte in YNAB ändern"). Läuft unter der Schreib-Sperre.

Die übrige README ist gegen den Code abgeglichen: "Kategorie ändern im Detail" geht per replace im Delta mit; "Zellmenü" (Zeile 200, Filter-Knopf ohne Kategorie bei einem Übertrag), "Kategorie setzen für die Auswahl" (Zeile 216, Übertrag übersprungen), "Split-Sperren (Lesepfad)" (Zeile 218), *SplitSetzen* (Zeile 321) und *KategorieFürAuswahlSetzen* (Zeile 322) beschreiben weiter geltende Regeln. `CategoryLockedTransfer` nennt die README nicht.

Kann-Sammlung ledger (keine eigenen Tickets):
- `observe` zieht ein in YNAB geändertes Übertrags-Ziel auf einer bestehenden Buchung nicht nach (`TransferAccountId` bleibt alt); bekannt seit ADR 0048, durch diese Task nicht schlechter.
- Ein Übertrag aus YNAB lässt sich in BB nur auf ein anderes Konto umlenken, nicht zurück auf eine Kategorie (BB kennt keinen Payee davor); der Picker zeigt das erst nach dem Klick als Fehler-Toast.
- Sheet-Titel und Kontostand im gemeinsamen Picker an Prototyp Version 17 angleichen (siehe Abweichung 2), falls Roman es will.
