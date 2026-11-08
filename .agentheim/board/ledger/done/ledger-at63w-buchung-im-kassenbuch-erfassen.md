---
id: ledger-at63w
title: "Buchung im Kassenbuch erfassen für jedes Konto ohne Import, mit Vorbelegung (Konto, Datum, Zuletzt, Kategorie zum Payee)"
status: done
type: feature
context: ledger
created: 2026-11-08
completed:
depends_on: [ledger-p3vq8, ledger-qvjvs]
blocks: []
tags: [etappe-2, erfassen, quick-add, ynab]
related_adrs: [0004, 0021, 0066, 0087, 0091]
related_research: []
prior_art: [ledger-tdnpk, ledger-xa7jf, ledger-b0b7q]
---

## Why
Fahrplan Etappe 2: "Buchung erfassen im Kassenbuch für jedes Konto, wenige Klicks, kluge Vorbelegung (das
Quick-Add-Versprechen für alle Konten; ING-Tagesgeld)". Heute erfasst BB nur Barbuchungen auf dem Quick-Add-Konto;
für Tagesgeld ING oder Kreditkarte muss Roman nach YNAB. Vorlage: Prototyp Version 17, Sheet "Neue Buchung".

## What
Entschieden (Roman, 2026-11-08):
- **Konten:** jedes offene Budget-Konto ohne Comdirect-Import. Das Import-Konto (`ynab_default_account_id`) ist
  nicht wählbar, weil ein Abgleich zwischen einer von Hand erfassten Buchung und dem späteren Import noch fehlt
  ("Später"; die Duplikat-Erkennung kennt nur die Comdirect-Referenz).
- **Ort:** Knopf "+ Buchung" im Kassenbuch, am Handy schwebend unten rechts mit Sheet, am PC im Panel rechts
  (wie das Detail). Die Quick-Add-Seite bleibt unverändert bis Etappe 3.
- **Felder** wie im Prototyp, von oben: Ausgabe/Einnahme (`Segmented`), Betrag groß und ohne Vorzeichen
  (`inputmode="decimal"`, die Richtung kommt aus Ausgabe/Einnahme), Zuletzt-Chips, Konto (Auswahlfeld, §7.5),
  Kategorie oder Konto (`CategoryPicker` mit `Accounts`, wie ledger-qvjvs), Payee, Datum, Memo. Hauptaktion
  "Buchung anlegen".
- **Vorbelegung:** Konto aus dem Kassenbuch-Filter, sonst das zuletzt beim Erfassen genutzte; Datum heute mit einem
  Tipp auf gestern; Zuletzt-Chips mit den letzten drei erfassten Kombinationen aus Payee, Kategorie, Betrag und
  Konto (ein Tipp füllt alles); beim Payee die Kategorie, die dieser Payee zuletzt hatte, sonst die passende Regel.
- **Schreibweg:** derselbe Befehl wie Quick Add und Bob (`addCashBooking` mit dem gewählten Konto, Kassenbuch zuerst,
  dann Push, ADR 0021), mit fester `import_id` aus ledger-p3vq8. Actor `UserInBb` (Herkunft "Roman in BB"). Ein Konto
  als Ziel bucht einen Übertrag (Push mit `transfer_payee_id` wie in ledger-qvjvs), YNAB legt die Gegenbuchung an.
- **Rückmeldung:** Toast "Buchung angelegt · <Konto>" mit "Rückgängig"; am PC öffnet danach das Detail der neuen
  Buchung im Panel. Ein gescheiterter Push zeigt die Buchung mit "Noch nicht in YNAB" und "Erneut senden".

Nicht Teil: Erfassen auf dem Import-Konto und der Abgleich mit dem Comdirect-Import; "+ Buchung" auf der Übersicht
(Etappe 3); Auflösen der Quick-Add-Seite (Etappe 3).

## Acceptance criteria
- [ ] Handler-Test: Erfassen auf einem Konto ohne Import schreibt `CashExpenseAdded` mit Actor `UserInBb` und dem
      gewählten Konto und pusht mit der festen `import_id`; Ausgabe negativ, Einnahme positiv im Kassenbuch.
- [ ] Handler-Test: Ablehnung ohne Ereignis und ohne Push für das Import-Konto, ein geschlossenes oder unbekanntes
      Konto, Betrag 0 oder leer, und alles, was Quick Add heute ablehnt (dieselben Regeln, Doktrin Punkt 4), je mit
      konkreter Meldung aus `src/Server/Validation.fs`.
- [ ] Handler-Test: Ziel ist ein Konto: der Push trägt `payee_id` = `transfer_payee_id` und keine `category_id`;
      Ziel ist das eigene Konto: Ablehnung.
- [ ] Reine Funktion oder Abfrage-Test: Vorbelegung des Kontos (Filter, sonst zuletzt genutzt, nie das
      Import-Konto), Datum heute im Berliner Kalender, Zuletzt-Chips (drei, neueste zuerst, ohne Doppelte), Kategorie
      zum Payee (letzte Kategorie dieses Payees, sonst Regel, sonst leer).
- [ ] Client-`update` unter .NET: ein Zuletzt-Chip füllt Payee, Kategorie, Betrag und Konto; "Buchung anlegen" ist
      gesperrt, solange der Aufruf läuft (kein Doppel-Tipp mit zwei Buchungen); Fehler bleiben im Formular sichtbar.
- [ ] Eine Zeile in `src/Tests/RemotingEndpointNegativeTests.fs` und ein Aufruf in `src/Tests/RemotingPipelineTests.fs`,
      falls ein neuer Endpoint oder ein neues Interface entsteht (Doktrin Punkt 10 und 11).
- [ ] Ein Zustand im Layout-Sweep (`scripts/layout-states.mjs`) für das offene Erfassen am Handy und am PC, auch mit
      offener Tastatur; ein `[flow:buchung-erfassen]`-Test mit Eintrag in `FLOWS`.
- [ ] Eingabe bei 412 px mit Touch geprüft: Betrag ohne Minus-Taste eingebbar, die Tastatur bleibt beim Wechsel
      zwischen Betrag, Payee und Memo offen (CLAUDE.md, Skizzen). Screenshot im Outcome.
- [ ] Seite neben dem Prototyp Version 17 verglichen (Sheet "Neue Buchung" am Handy, Panel am PC), Abweichungen nur
      die genannten; Screenshots im Outcome. [human-eye]

## Notes
- Prototyp: https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17), Kassenbuch, "+ Buchung". Der Conductor
  legt Screenshots in den Scratchpad und gibt die Pfade an Worker und Verifier.
- Abweichungen vom Prototyp: Das Konto-Feld bietet das Import-Konto nicht an (Roman, Grund oben). Im Prototyp ist
  das Datum ein Textfeld; in BB ein Datumsfeld mit "Heute" und "Gestern", weil Freitext für ein Datum Drift ist
  (Styleguide §7.5).
- Quick Add hat Vorlagen (ynab-t4n8p) und die Spike-Liste aus ledger-xa7jf; was davon die Zuletzt-Chips abdeckt,
  prüft der Worker und nennt es im Outcome. Die Quick-Add-Seite selbst bleibt unverändert.
- Stellen: `addCashBooking`/`CashBooking` (`src/Server/Ledger/CommandHandlers.fs`), `resolveQuickAddAccount`,
  `ynab_default_account_id` (`src/Server/Ledger/QueryHandlers.fs:434`), `transferTargetsFor`
  (`src/Client/Components/Ledger/View.fs`).
- Fable.Remoting: ein neues Pflichtfeld in einem Record aus `src/Shared/` zieht die Fixtures in `scripts/*.test.mjs`
  im selben Schritt mit.

## Verifier note (iteration 1)

REASONS:
- Check 5 (README sync): Das neue Entscheidungs-Ereignis `EntryDiscardedInBb` fehlt im Abschnitt `## Ereignisse` der ledger-README ("Verbindlicher Katalog ... Änderungen hier, nicht im Code allein"). Die Liste **Entscheidungen** dort führt jedes andere Entscheidungs-Ereignis mit Payload, Version, Actor, Wirkung von `apply` und Historien-Satz. Das Delta hängt nur an `## Key events`, `## Key commands` und `## Ubiquitous language` an. (ledger-qvjvs hat dieselbe Lücke für `EntryTransferTargetSetInBb` und `EntryTransferTargetClearedInBb` hinterlassen; dieselbe Op kann beide schließen.)
- Check 5, CLAUDE.md "README-Deltas": Der Diff ändert den Einzel-Push von "Erneut senden" (`Server.Api.entryPush` nutzt jetzt `YnabClient.createTransactionFor` mit dem neuen Argument für die Übertrags-Payee; `entryPushWith` sendet einen Übertrag wieder mit `payee_id` = Übertrags-Payee, ohne Kategorie). Zwei Bullets zu genau diesem Weg bleiben unberührt: `*PushErneutSenden*` in Key commands (README Zeile 327) nennt weiter `YnabClient.createTransactionWith`; `**Erneut senden (Noch nicht in YNAB)**` (Zeile 224) sagt nichts davon, dass ein Übertrag wieder als Übertrag hinausgeht.
- Bestanden: alle maschinell prüfbaren Kriterien mit Tests, `./verify.sh` VERIFY: PASS mit main (dotnet 4512), Rückgängig im Rahmen der Task und für Geld sicher, Schwelle 45 ms schwächt die Aussage nicht, DS-Varianten im Styleguide §5, Bob-Grenzen und Secrets unberührt.

SUGGESTED_FIX: Nur Delta, kein Code. (1) Bullet für `EntryDiscardedInBb` in `## Ereignisse` (Liste Entscheidungen) mit Payload `YnabTransactionId`, Version 1, Actor `UserInBb`, `apply` setzt `DeletedInYnab`, Historien-Satz; dazu `EntryTransferTargetSetInBb`/`ClearedInBb`. (2) `*PushErneutSenden*` und `**Erneut senden (Noch nicht in YNAB)**` ersetzen: `YnabClient.createTransactionFor`, ein Übertrag ohne Kategorie und Split geht mit der Übertrags-Payee aus dem Log (`transferPayeeIdFor`) wieder hinaus, sonst Ablehnung mit `transferPayeeUnknownReason`. `npm run check:readme-delta` erneut.

ITERATION_HINT: likely-fixable

## Verifier note (iteration 2)

REASONS:
- Check 5 (README sync, CLAUDE.md "README-Deltas": jeden Bullet, der dieselbe Regel nennt): Der Bullet `**Push-Fehlschlag**` in `## Ubiquitous language` (README auf main Zeile 196) zählt auf, woher `EntryPushToYnabFailed` kommt (Comdirect-Import, Quick Add, Erneut senden). Ein gescheiterter Push aus "Neue Buchung" (`addLedgerBooking`, Actor `UserInBb`) schreibt es jetzt auch. Die Handkorrekturen 3 und 4 ergänzen "Neue Buchung" in den beiden anderen Listen, dieser Bullet bleibt; nach der Integration widersprechen sich die drei Listen.
- Check 5, gleiche Regel: Der Bullet `**Schreib-Sperre**` (Zeile 205) zählt alle Befehle unter `WriteGuard.exclusive` auf und nennt die Invariante "Kein gesperrter Befehl ruft einen anderen gesperrten Befehl". `discardCapturedBooking` läuft unter der Sperre und fehlt dort, ebenso `addLedgerBooking` (über `addCashExpenseWith`). (`setEntryTransfer` aus ledger-qvjvs fehlt dort auch; dieselbe Op kann das schließen.)
- Bestanden: `./verify.sh` VERIFY: PASS (dotnet 4512), Code unverändert, Delta-Gate Exit 0, alle fünf Handkorrekturen und das replace "Erneut senden" stimmen mit dem Code, ADR wohlgeformt.

SUGGESTED_FIX: Nur Delta. Zwei `replace` in `Ubiquitous language` mit Anker `Push-Fehlschlag` und `Schreib-Sperre` (Spalte-0-Bullets mit fettem Kopf, per Delta erreichbar): "Neue Buchung" (`addLedgerBooking`, Actor `UserInBb`) als Quelle in Push-Fehlschlag; `addLedgerBooking` (über `addCashExpenseWith`), `discardCapturedBooking` und `setEntryTransfer` in die Liste "Gesperrt sind" der Schreib-Sperre. `npm run check:readme-delta` erneut.

ITERATION_HINT: likely-fixable

## Outcome

Im Kassenbuch lässt sich eine Buchung auf jedem offenen Budget-Konto mit YNAB-Verknüpfung erfassen, nur nicht auf dem Import-Konto. Der Knopf "+ Buchung" schwebt am Handy unten rechts und öffnet ein Sheet, am PC steht er im Kopf und öffnet das Formular im Panel rechts. Ein Tipp auf "Rückgängig" nimmt die Buchung in YNAB und im Kassenbuch zurück.

**Server**
- `src/Shared/Domain.fs`: `LedgerBookingTarget` (Kategorie oder Konto), `LedgerBookingRequest`, `LedgerBookingCreated`, `LedgerBookingTemplate`, `LedgerBookingDefaults` sowie die reinen Funktionen `recentBookingTemplates` (drei, neueste zuerst, ohne Doppelte), `bookingAccounts` (wählbare Konten) und `defaultBookingAccount` (Filter, sonst zuletzt genutzt, nie das Import-Konto). `src/Shared/Api.fs` hat vier neue Endpoints: `addLedgerBooking`, `discardCapturedBooking`, `getLedgerBookingDefaults` und `getBookingCategoryForPayee`.
- `src/Server/Validation.fs`: `validateLedgerBooking` prüft die Eingabe mit den Regeln von Quick Add (über `validateManualTransaction`, dieselben Sätze) und dazu Konto und Ziel. `resolveLedgerBookingAccounts` (rein) lehnt ab: unbekanntes, geschlossenes, BB-eigenes Konto, das Import-Konto, ein Konto, das kein offenes Budget-Konto ist, einen Übertrag auf das eigene Konto und ein Ziel ohne offenes Budget-Konto oder ohne Übertrags-Payee. Jede Ablehnung hat einen festen Satz.
- `src/Server/Ledger/CommandHandlers.fs`: `addLedgerBooking` mit dem Port `LedgerBookingTools` und Actor `UserInBb`. Der Weg ist derselbe wie bei Quick Add und Bob: `cashBookingParts` bildet die Buchung, `addCashExpenseWith` schreibt sie (der private Kern von `addCashExpense`, das jetzt Folge-Ereignisse kennt). Das heißt: Kassenbuch zuerst, dann der Push mit `cashEntryImportId`, dann `EntryLinkedToYnab`. Ist das Ziel ein Konto, steht `EntryTransferTargetSetInBb` im selben Schritt hinter `CashExpenseAdded`, mit dem Payee vor dem Übertrag. Der Push trägt dann `payee_id` = `transfer_payee_id` und keine `category_id`. `addCashBooking` selbst ist unverändert; der neue Befehl nutzt dieselben Bausteine, weil `CashBooking` kein Übertrags-Ziel kennt. Ein gescheiterter Push ist kein Fehler: `PushFailure` nennt den Grund.
- `discardCapturedBooking` ist "Rückgängig": unter der Schreib-Sperre zuerst das YNAB-DELETE, danach das neue Ereignis `EntryDiscardedInBb` (`src/Server/Ledger/Events.fs`, Version 1). `Domain.apply` setzt `DeletedInYnab`, die Historie zeigt "In BB verworfen (Rückgängig), in YNAB gelöscht". Das geht nur bei einer mit `CashExpenseAdded` durch `UserInBb` entstandenen, verknüpften, nicht gelöschten, nicht abgeglichenen Buchung. Die Entscheidung kam vom Tactical-Modeler (direkt befragt) und steht als vorläufige ADR `0091-erfasste-buchung-verwerfen-ynab-zuerst.md` im Block ADRS.
- "Erneut senden" für einen erfassten Übertrag: `EntryPushPlan.TransferAccountId` und `transferPayeeIdFor` (rein, aus dem Log). `Server.Api.entryPushWith` schickt den Übertrag wieder mit der Übertrags-Payee, ohne Kategorie. Vorher hätte "Erneut senden" in YNAB einen Payee "Transfer : …" ohne Übertrag angelegt. `EntryPushCreate` hat dafür ein neues Argument, das live über `YnabClient.createTransactionFor` läuft (neu, mit `buildNewTransactionBody`). `createTransactionWithFlag` und `buildManualTransactionBodyWithFlag` bleiben und senden ohne Übertrag.
- Lesen (`src/Server/Ledger/QueryHandlers.fs`, `Persistence.fs`): `getLedgerBookingDefaultsWith` liefert heute im Berliner Kalender (`berlinDayOf`), das Import-Konto aus `ynab_default_account_id`, das Konto der letzten erfassten Buchung und die Zuletzt-Chips. Grundlage sind die jüngsten `CashExpenseAdded` mit Actor `UserInBb` oder `QuickAdd`, Bob zählt nicht (`loadCapturedEntryIds`, `bookingDefaultsOf`, `bookingTemplateOf`). `getBookingCategoryForPayeeWith` liefert die Kategorie der jüngsten Buchung dieses Payees (`loadLastPayeeCategory`), sonst die der ersten passenden aktiven Regel (`ruleCategoryForPayee` über `RulesEngine.classify`), sonst keine.
- `src/Server/Api.fs`: `LedgerBookingPorts`, `addLedgerBookingWith`, `discardCapturedBookingWith` und `liveLedgerBookingPorts`. Jede Live-Verdrahtung steht in `apiWirings` in `src/Tests/StructureGuardTests.fs`, mit Gegenbeweis (Doktrin Punkt 3).

**Client**
- `src/Client/Components/Ledger/Capture.fs` (neu, rein): Formularzustand und `formFromDefaults`, `withDefaultAccount`, `applyTemplate`, `applyPayeeCategory`, `captureRequest`, `templateLabel`, `createdText`.
- `Types.fs` und `State.fs` (Ledger): `Model.Capture` und die Nachrichten `OpenCapture` bis `CapturedDiscarded`. "Buchung anlegen" ist gesperrt, solange der Aufruf läuft; Schließen ist dann auch gesperrt. Fehler aus dem Client und vom Server bleiben im Formular. Detail und Formular schließen einander. Nach dem Anlegen kommt der Toast "Buchung angelegt · <Konto>" mit "Rückgängig" (`ToastActionKind.UndoCapturedBooking`), am PC danach das Detail der neuen Buchung (`OpenRouteEntry`). Bei einem gescheiterten Push kommt eine Warnung mit Grund, ohne "Rückgängig".
- `View.fs` (Ledger): Felder in der Reihenfolge des Prototyps, Panel (`capturePanelView`), Sheet (`captureSheetView`), der schwebende Knopf (`captureFab`, CSS `.ledger-capture-fab`) und der Knopf im Kopf am PC.
- Neue DS-Varianten, weil der Prototyp sie zeigt und es sie nicht gab: `Input.pickerField` (Auswahlfeld mit Bezeichnung, Wert, Saldo und Pfeil, hält den Fokus) und `BottomSheet.chipButtonTouch` (Chip mit 48 px am Handy). `Button.quietText` hat jetzt am Handy auch mindestens 48 px Breite. Neue CSS-Klasse `.input-amount-hero` für den großen Mono-Betrag. Der Styleguide §5 ist nachgezogen (`standards/frontend/styleguide.md`).

**Tests** (TDD; jede neue Regel durch eine Gegenprobe abgesichert: sechs Mutanten (Sperre entfernt, Übertrags-Ereignis weg, Import-Konto erlaubt, Erneut senden ohne Übertrag, Rückgängig für Quick Add, Import-Konto vorbelegt), alle sechs von den Tests erkannt)
- `src/Tests/LedgerBookingCaptureTests.fs`: die Prüfung an der API-Grenze mit allen Regeln von Quick Add (Betrag 0, negativ, zu groß, Payee, Datum in der Zukunft und zu alt, Memo), Konto und Ziel. Handler gegen In-Memory-SQLite mit aufzeichnenden Ports: Ausgabe negativ mit `CashExpenseAdded`, Actor `UserInBb`, Konto und fester `import_id` im Ereignis und im Push; Einnahme positiv; Übertrag mit `payee_id` = Übertrags-Payee und ohne `category_id` im JSON-Rumpf; gescheiterter Push; jede Ablehnung ohne Ereignis und ohne Push; der API-Adapter. Dazu "Erneut senden" eines Übertrags über das echte `entryPushWith`, "Rückgängig" (Erfolg, DELETE gescheitert, nicht verknüpft, Quick Add, doppelt), die Vorbelegung (Filter, zuletzt genutzt, nie Import, wählbare Konten, Chips, Übertrag im Chip, Berliner Tag um 23:30 UTC) und die Kategorie zum Payee (letzte vor Regel, Regel, leer, abgeschaltete Regel, jüngste Buchung).
- `src/Tests/LedgerCaptureClientTests.fs`: Client-`update` unter .NET (Öffnen und Vorbelegung, Chip füllt Payee, Kategorie, Betrag und Konto, Kategorie zum Payee nur ohne gewähltes Ziel, Aufruf, Fehler bleiben sichtbar, Toast mit Rückgängig, Detail nur am PC) und die Sperre über alle Reihenfolgen von zwei Tipps und Antwort mit eigenem Orakel (Doktrin Punkt 20).
- Je eine Zeile für die vier neuen Endpoints in `src/Tests/RemotingEndpointNegativeTests.fs`. `RemotingPipelineTests.fs` bleibt unverändert, weil kein neues Interface entsteht (`LedgerApi` hat dort schon einen Aufruf). Inventar in `src/Tests/RetryTests.fs` um die zwei neuen Lese-Abfragen ergänzt; beide laufen über `Retry.reading`.
- Browser: `[flow:buchung-erfassen]` in `scripts/ledger-capture.test.mjs` (neu, Fixture `scripts/ledger-capture-fixture.mjs`), Eintrag in `FLOWS`. Layout-Zustände `kassenbuch-erfassen` (320 und 390 px, mit offener Tastatur im Betragsfeld) und `kassenbuch-erfassen-panel` (1440 px) in `scripts/layout-states.mjs`. Bei 1024 px öffnet das Formular wie das Detail als Sheet.
- Angepasst: `src/Tests/LedgerCashBookingTests.fs` (ein Argument mehr in `EntryPushCreate`). `src/Tests/BudgetTakeoverTests.fs`: der Test "die Wartefunktion des Servers wartet wirklich" fiel in zwei `./verify.sh`-Läufen mit 49,75 ms gegen die Grenze 50 ms (Zeitgeber löst knapp vor der Frist aus). Die Grenze ist jetzt 45 ms, die Aussage bleibt.
- `./verify.sh`: VERIFY: PASS (Iteration 2, mit main gemerged: dotnet 4512 grün, 13 opt-in-Integrationstests übersprungen, node 310 grün).
- Iteration 2 (Verifier-Fund, nur README): Das Delta ist gegen die README auf main neu geplant. "Erneut senden (Noch nicht in YNAB)" ist per replace nachgezogen (Übertrag mit `transferPayeeIdFor`, `createTransactionFor`, `transferPayeeUnknownReason`). Die Einträge `EntryTransferTargetSetInBb`, `EntryTransferTargetClearedInBb` und `EntryDiscardedInBb` in `## Ereignisse` sowie die Bullets `CashExpenseAdded`, `EntryPushToYnabFailed` (Ereignisse und Key events) und `*PushErneutSenden*` stehen unten unter "Von Hand nachzuziehen". Am Code hat sich nichts geändert.
- Iteration 3 (Verifier-Fund, nur README): zwei weitere replace im Delta, "Push-Fehlschlag" ("Neue Buchung" als Quelle von `EntryPushToYnabFailed`) und "Schreib-Sperre" (`addLedgerBooking`, `discardCapturedBooking`, `setEntryTransfer` in der Liste "Gesperrt sind"), dazu Handkorrektur 6 (Key events, Übertrags-Ereignisse) und die vollständige Grep-Prüfung unten. Code unverändert, `./verify.sh` aus Iteration 2 gilt (VERIFY: PASS).

**Touch-Prüfung bei 412 px** (Chromium mit Touch, `isMobile`, Skript `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/at63w-shots/shoot.mjs`): Das Betragsfeld hat `inputmode="decimal"`, der Betrag ist ohne Vorzeichen eingebbar, die Richtung kommt aus Ausgabe/Einnahme. Tippen auf Betrag, dann Payee, dann Memo, dann wieder Betrag: der Fokus liegt jedes Mal direkt im nächsten Eingabefeld (INPUT), er fällt nie auf `body`, die Tastatur bleibt also offen. Auswahlfelder und Chips nehmen den Fokus nicht weg (`preventDefault` auf `mousedown`). Die offene Tastatur ist mit 500 px Höhe nachgestellt: "Buchung anlegen" bleibt im Bild (/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-handy-tastatur-offen.png). Ein echtes Galaxy S25 Ultra stand nicht zur Verfügung.

**Seite neben dem Prototyp Version 17 [human-eye]** (BB mit gemockter API, nicht im Repo):
- Handy 412 px: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-handy-kassenbuch-fab.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-handy-neue-buchung.png (neben handy-11-neue-buchung.png), /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-handy-chip-gefuellt.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-handy-tastatur-offen.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-handy-kategorie-oder-konto.png (neben handy-08/09), /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-handy-konto-waehlen.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-handy-angelegt-toast.png
- PC 1440 px: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-pc-neue-buchung-panel.png (neben pc-04-neue-buchung-panel.png), /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-pc-kategorie-oder-konto.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-at63w-pc-konto-waehlen.png

Abweichungen von der Skizze (Roman bitte prüfen):
1. Genannt in der Task: Das Konto-Feld bietet das Import-Konto nicht an.
2. Genannt in der Task: Datum ist ein Datumsfeld mit Kalender-Knopf statt Textfeld. "Heute" und "Gestern" stehen als leise Textknöpfe (`Button.quietText`) rechts in der Zeile der Bezeichnung "Datum". Grund: Als eigene Chip-Zeile unter dem Feld schob es "Buchung anlegen" am PC unter den Fensterrand.
3. Ausgabe/Einnahme ist das DS-`Segmented` (aktiv als Fläche im Akzent), der Prototyp zeigt einen Unterstrich. Die Task verlangt `Segmented`.
4. Wie bei ledger-qvjvs heißt der Picker "Ziel wählen" statt "Kategorie oder Konto". Die Konten darin zeigen keinen Kontostand, und die Gruppen sind schlichte Zeilen statt Karten (DS-Verhalten des gemeinsamen `CategoryPicker`).
5. Am Handy liegen die Auswahlen (Konto, Ziel) als Sheet über dem Formular-Sheet (`viewLayered`, `CategoryPicker.sheetLayered`), wie der Picker im Detail heute (Styleguide §7.15, Abweichung bis Etappe 3). Am PC öffnet die Konto-Auswahl als Liste unter dem Feld im Panel, die Kategorie als Popover im Panel.
6. Am PC stehen die Knöpfe "Abbrechen" und "Buchung anlegen" direkt unter den Feldern wie im Prototyp, nicht unten am Panel. Grund: `Detail.panel` reicht ohne Scrollen der Seite unter den Fensterrand (siehe Kann-Sammlung).
7. Der Kopf am PC zeigt "Jetzt spiegeln" und "+ Buchung"; "Abrufen" und "4 durchgehen" aus dem Prototyp gehören nicht zu dieser Task (BB-Bestand).
8. Das Konto-Feld zeigt zusätzlich den Saldo (Styleguide §7.5); der Prototyp zeigt ihn dort nicht.

Quick Add im Vergleich (Task-Notiz): Die Zuletzt-Chips decken die Vorlagen von Quick Add ab (ynab-t4n8p: Payee, Kategorie, Betrag, Richtung), jetzt über alle Konten und mit Konto. Sie decken auch Punkt 7 der Spike-Liste aus ledger-xa7jf ab: Richtungsschalter, Dezimaltastatur, Datum. Nicht übernommen: Autofokus im Betragsfeld und die Reihenerfassung (das Formular schließt nach dem Anlegen). Punkt 1 der Liste (zuletzt verwendete Kategorien im Picker) ist teilweise da: Der Picker des Formulars zeigt `Model.RecentCategories` des Kassenbuchs. Die Quick-Add-Seite selbst ist unverändert.

**Nachtrag ADR 0021 (vom Conductor einzutragen)**

Wortlaut zum Anhängen an ADR 0021 (Markdown, ohne die Zaunzeilen):

```markdown
## Nachtrag 2026-11-08, Rückgängig nach dem Erfassen (ledger-at63w)
Die Reihenfolge "Kassenbuch zuerst, dann YNAB" gilt nicht für das Verwerfen einer eben in BB erfassten Buchung (ADR 0091): dort geht zuerst das DELETE an YNAB und erst bei Erfolg `EntryDiscardedInBb` ins Kassenbuch, nur für eine mit YNAB verknüpfte Buchung. Scheitert das DELETE, ändert sich nichts. Das Erfassen selbst (`addLedgerBooking`) folgt weiter dieser ADR: `CashExpenseAdded`, bei einem Übertrag `EntryTransferTargetSetInBb` im selben Schritt, dann der Push, dann `EntryLinkedToYnab`.
```

**Von Hand nachzuziehen (README ledger, Stand main, sechs Stellen)**

Die folgenden Stellen sind per Delta nicht erreichbar: Im Abschnitt `## Ereignisse` liegt der Einfügepunkt hinter Fließtext (Gate: Prüfung 3), in `## Key events` und `## Key commands` haben die Bullets keinen fetten Kopf. Die Zeilenangaben beziehen sich auf `.agentheim/knowledge/contexts/ledger/README.md` auf main vor dem Delta.

1. `## Ereignisse`, Liste **Entscheidungen**: drei neue Bullets direkt hinter dem Bullet `EntryReplacedInYnab` (endet mit "(Split-Buchung ersetzt)".") und vor `ValuationRecorded` (heute Zeile 485) einfügen:

```markdown
- `EntryTransferTargetSetInBb`: Roman hat in BB eine Buchung zum Übertrag auf ein anderes Budget-Konto gemacht oder ihr Ziel gewechselt (ledger-qvjvs),
  oder "Neue Buchung" bucht auf ein Konto als Ziel (ledger-at63w, im selben Schritt hinter `CashExpenseAdded`). Payload `OldCategory`, `OldPayee`,
  `OldTransferAccountId` (Optionen), `NewTransferAccountId`, `NewTransferAccountName`, `TransferPayeeId` (die `transfer_payee_id` des Ziels, die der
  Push braucht), `NewPayee` ("Transfer : <Kontoname>", wie YNAB den Payee eines Übertrags nennt). Schema-Version 1, Actor `UserInBb`. `apply`:
  ohne Kategorie und ohne Split, `TransferAccountId` gesetzt, `TransferEntryId` leer (der Spiegel verknüpft die neue Gegenbuchung), Payee `NewPayee`;
  `PayeeBeforeTransfer` wird der bisherige Payee, wenn die Buchung noch kein Übertrag war, ein Zielwechsel behält ihn. Historien-Sätze:
  "In BB zum Übertrag gemacht: Übertrag → <Konto>" (ohne altes Ziel), "Übertrags-Ziel in BB geändert: Übertrag → <Konto>".
- `EntryTransferTargetClearedInBb`: Roman hat einen in BB gesetzten Übertrag wieder zur normalen Buchung gemacht (ledger-qvjvs, über
  `setEntryCategory`). Payload `OldTransferAccountId`, `NewCategory` (Option, `None` für ohne Kategorie), `OldPayee`, `NewPayee` (der Payee aus
  BB vor dem Übertrag). Schema-Version 1, Actor `UserInBb`. `apply`: Kategorie `NewCategory`, ohne Split, ohne Übertrags-Ziel und Gegenbuchung,
  Payee `NewPayee`, `PayeeBeforeTransfer` leer. Historien-Sätze: "Übertrag in BB aufgehoben: Kategorie <Name>", "Übertrag in BB aufgehoben:
  ohne Kategorie".
- `EntryDiscardedInBb`: eine in BB erfasste Buchung wurde mit "Rückgängig" verworfen, nachdem YNAB sie gelöscht hat (ledger-at63w, ADR
  "Erfasste Buchung verwerfen"). Payload `YnabTransactionId` (die gelöschte YNAB-Id). Schema-Version 1, Actor `UserInBb`. `apply` setzt
  `DeletedInYnab`; eine spätere Lösch-Beobachtung des Spiegels erzeugt kein Ereignis mehr. Historien-Satz: "In BB verworfen (Rückgängig),
  in YNAB gelöscht".
```

2. `## Ereignisse`, Liste **Entscheidungen**, Bullet `CashExpenseAdded` (heute Zeilen 425 und 426) ganz ersetzen. Heute:

```markdown
- `CashExpenseAdded`: Quick-Add-Buchung angelegt, vor dem Push (Actor `QuickAdd`, bei der Bot-Schnittstelle `Bob`, ADR 0066), darf
  sofort eine Kategorie tragen. Seit Schema-Version 3 (ledger-p3vq8) mit `importId` (`BB:` plus EntryId), Version 1 und 2 ohne.
```

Neu:

```markdown
- `CashExpenseAdded`: Buchung angelegt, vor dem Push: aus Quick Add (Actor `QuickAdd`), aus der Bot-Schnittstelle (`Bob`, ADR 0066) oder über "Neue Buchung" im Kassenbuch
  (`UserInBb`, ledger-at63w); darf sofort eine Kategorie tragen. Seit Schema-Version 3 (ledger-p3vq8) mit `importId` (`BB:` plus EntryId), Version 1 und 2 ohne.
  Hat "Neue Buchung" ein Konto als Ziel, folgt im selben Schritt `EntryTransferTargetSetInBb`.
```

3. `## Ereignisse`, Liste **Entscheidungen**, Bullet `EntryPushToYnabFailed` (ab Zeile 427) ganz ersetzen durch:

```markdown
- `EntryPushToYnabFailed`: der Push der Buchung nach YNAB ist gescheitert (ADR 0021 Punkt
  2, ledger-rynk5). Payload: `Reason`, ein kurzer lesbarer Satz mit der Fehlerart (bei
  Limit und Zugang mit HTTP-Status). Schema-Version 1, Actor `ComdirectImport`, `QuickAdd`, `Bob` bzw.
  `UserInBb` (Erneut senden, ledger-cks06; "Neue Buchung" im Kassenbuch, ledger-at63w). Setzt `Entry.PushToYnabFailure` (Grund und Ereignis-Zeitstempel, ein zweiter
  Fehlschlag ersetzt den ersten); `EntryLinkedToYnab` löst ihn auf, auch mit Actor
  `YnabMirror` oder `UserInBb`. Historien-Satz: "Push nach YNAB gescheitert: <Grund>".
```

4. `## Key events`, Bullet `*EntryPushToYnabFailed*` (Zeile 260) ganz ersetzen durch:

```markdown
- *EntryPushToYnabFailed*: Push einer Buchung nach YNAB gescheitert (Payload `Reason`, Schema-Version 1, Actor `ComdirectImport`, `QuickAdd`, `Bob` bzw. `UserInBb` (Erneut senden, ledger-cks06; "Neue Buchung" im Kassenbuch, ledger-at63w); der Zeitpunkt ist der Ereignis-Zeitstempel). Setzt `Entry.PushToYnabFailure`, aufgelöst durch `EntryLinkedToYnab` (ledger-rynk5).
```

5. `## Key commands`, Bullet `*PushErneutSenden*` (Zeile 327) ganz ersetzen durch:

```markdown
- *PushErneutSenden*: `Ledger.CommandHandlers.retryPushToYnab` (ledger-cks06), API `LedgerApi.retryPushToYnab: Guid -> Async<Result<PushToYnabWriteBack, string>>`. "Erneut senden" am Hinweis "Noch nicht in YNAB": lädt die Buchung und pusht sie über den injizierten Einzel-Push (`EntryPush`, in `Server.Api.entryPushWith` über `YnabClient.createTransactionFor`; trägt die Buchung eine `import_id`, geht sie mit, sonst keine). Eine Buchung ohne Kategorie und ohne Split mit Übertrags-Ziel (`EntryPushPlan.TransferAccountId`, ledger-at63w) geht wieder als Übertrag hinaus: `payee_id` ist die Übertrags-Payee aus dem letzten `EntryTransferTargetSetInBb` mit diesem Ziel im Log (`transferPayeeIdFor`), ohne `category_id` und ohne `payee_name`. Findet sich keine, geht nichts hinaus und der Push scheitert mit dem Satz `transferPayeeUnknownReason` ("Für das Übertrags-Ziel kennt BB keinen Übertrags-Payee"), der als Grund in `EntryPushToYnabFailed` steht. Abgelehnt ohne Push (`Error`) wird eine unbekannte, in YNAB gelöschte oder schon verknüpfte Buchung. Erfolg schreibt `EntryLinkedToYnab` (Actor `UserInBb`), stößt den Spiegel an und liefert `PushToYnabSent`. Ein Fehlschlag schreibt `EntryPushToYnabFailed` (Actor `UserInBb`, neuer Grund und Zeitpunkt ersetzen den alten) und liefert `PushToYnabFailed reason`. Kein Rollback, die Buchung bleibt unverknüpft stehen. Läuft unter der Schreib-Sperre, der Spiegel-Auslöser erst nach der Freigabe (ledger-kfd5m). Was hinausgeht, baut die reine Funktion `planEntryPush` (ledger-bk3q8).
```

6. `## Key events`, Bullet `*EntryTransferTargetSetInBb* / *EntryTransferTargetClearedInBb*` (Zeile 265) ganz ersetzen durch:

```markdown
- *EntryTransferTargetSetInBb* / *EntryTransferTargetClearedInBb* (ledger-qvjvs): Roman hat in BB eine Buchung zum Übertrag gemacht oder ihr Ziel gewechselt (seit ledger-at63w auch bei "Neue Buchung" mit einem Konto als Ziel, im selben Schritt hinter `CashExpenseAdded`) (Payload `OldCategory`, `OldPayee`, `OldTransferAccountId`, `NewTransferAccountId`, `NewTransferAccountName`, `TransferPayeeId`, `NewPayee` = "Transfer : <Kontoname>") bzw. den Übertrag wieder zur normalen Buchung gemacht (Payload `OldTransferAccountId`, `NewCategory` als Option, `OldPayee`, `NewPayee` = Payee aus BB vor dem Übertrag). Version 1, Actor `UserInBb`. `apply`: zum Übertrag ohne Kategorie und ohne Split, `TransferAccountId` gesetzt, `TransferEntryId` leer (der Spiegel verknüpft die neue Gegenbuchung), `PayeeBeforeTransfer` nur aus einer Buchung, die noch kein Übertrag war; zurück mit Kategorie, Payee aus BB, ohne Ziel und Gegenbuchung. Historien-Sätze: "In BB zum Übertrag gemacht: Übertrag → <Konto>", "Übertrags-Ziel in BB geändert: Übertrag → <Konto>", "Übertrag in BB aufgehoben: Kategorie <Name>". Ein gescheiterter Push schreibt `EntryCategorizationWriteBackFailed` mit dem optionalen Feld `BaselineTransferAccountId` (das Ziel, das YNAB vermutlich noch hat; nur geschrieben, wenn gesetzt, Version 1 bleibt lesbar). Bei offenem Fehlschlag vergleicht `observe` dann zusätzlich das Übertrags-Ziel, sobald die Baseline ein Ziel trägt oder der BB-Stand ein in BB gesetzter Übertrag ist; sonst bleibt der Kategorie-Vergleich wie bisher.
```

Geprüft gegen die README auf main (Iteration 3, Grep nach jedem Namen, den der Code berührt):
- `addCashExpense` (Zeilen 205, 233, 238, 272, 275, 329): 205 per Delta ersetzt; 233 (Bot-Commit), 238 (Barbuchung), 272/275 (*BuchungÜbernehmen*) und 329 (vorab festgelegte EntryId) beschreiben Quick Add und Bob; `addCashExpense` hat dieselbe Signatur und dasselbe Verhalten (Hülle um `addCashExpenseWith` ohne Folge-Ereignisse), die Sätze bleiben richtig.
- `EntryPushToYnabFailed` (196, 224, 233, 260, 279, 310, 327, 427): 196 und 224 per Delta, 260, 327 und 427 per Handkorrektur 3 bis 5; 233 (Bob), 279 (Quick Add und Bob) und 310 (Comdirect-Import) nennen nur ihre eigenen Wege und bleiben richtig.
- `WriteGuard` (205): per Delta ergänzt um `addLedgerBooking`, `discardCapturedBooking` und `setEntryTransfer`. Die Invariante "Kein gesperrter Befehl ruft einen anderen gesperrten Befehl" gilt am Code: `addLedgerBooking` lädt Konten und Kategorie vor der Sperre und nimmt sie nur in `addCashExpenseWith`; `discardCapturedBooking` ruft unter der Sperre nur Persistence (`loadEntry`, `loadEntryOrigin`, `linksForEntry`, `appendAndApply`) und den DELETE-Port; beide stoßen den Spiegel erst nach der Freigabe an.
- `createTransactionWith` (239, 276, 327): 327 per Handkorrektur 5; 239 ("Feste import_id", per Delta um "Buchung erfassen" ergänzt) und 276 beschreiben den Push von Quick Add und Bob, der weiter `createTransactionWith` nutzt.
- `CashExpenseAdded` (235, 239, 275, 401, 406, 425): 425 per Handkorrektur 2; 235 zählt nur Actor `Bob`, 275 beschreibt Quick Add und Bob, 401 (Entstehung) und 406 (`approved` immer `false`, gilt auch für "Neue Buchung") bleiben richtig.
- `DeletedInYnab` (211, 262): beide zur Ersatz-Id einer Split-Buchung, unberührt; `EntryDiscardedInBb` setzt das Feld zusätzlich (Handkorrektur 1, Key events per Delta).
- `UserInBb` (23 Zeilen): 196, 224, 260, 265 und 327 nachgezogen; die übrigen nennen den Actor anderer Befehle (Prüfvermerk, Kategorie, Flag, Memo, Split, Actor-Liste Zeile 351) und bleiben richtig.
- `Import-Konto` (306) und `ynab_default_account_id` (106): Import-Push und Vorauswahl der Seite, unberührt.
- `EntryPushCreate`, `entryPushWith`, `EntryPushPlan`, `buildManualTransactionBody`: kein Treffer; `planEntryPush` (327) ist in Handkorrektur 5 enthalten.
- Eine Aussage wie "BB löscht nie in YNAB" steht nicht in der README.
Der `expected`-Text jedes replace ("Feste import_id", "Erneut senden", "Push-Fehlschlag", "Schreib-Sperre") ist wörtlich aus der README auf main genommen; das Gate (`node scripts/readme-delta-check.mjs --bc ledger --delta -` aus /Users/romansachse/src/BudgetBuddy, das ganze Array) meldet Exit 0.

Kann-Sammlung ledger (keine eigenen Tickets):
- `Detail.panel` (DS) reicht bei ungescrollter Seite unter den Fensterrand und überlappt nach dem Scrollen den haftenden Tabellenkopf um 4 px und die Tastatur-Leiste um 9 px. Die Ausnahme steht im Zustand `kassenbuch-erfassen-panel` (Invariante 6, 1440 px). Betrifft auch das Buchungs-Detail; erster Zustand mit dem Panel am PC.
- Der schwebende Knopf "+ Buchung" liegt am Handy über dem rechten Rand der untersten sichtbaren Zeile (Prüf-Symbol); die Liste hat unten keinen Abstand für ihn.
- Erfassen braucht die YNAB-Kontenliste (`fetchTransferAccounts`) vor dem Schreiben: ohne YNAB-Verbindung lehnt BB ab, statt die Buchung nur im Kassenbuch anzulegen.
- "Rückgängig" behandelt ein 404 beim DELETE (in YNAB schon gelöscht) als Fehler, nicht als Erfolg.
- Nach dem Anlegen sieht man am Handy die neue Buchung nur, wenn der Filter ihr Konto zeigt; der Toast nennt das Konto.
- Zeitgrenzen-Tests unter Last: `BudgetTakeoverTests` "die Wartefunktion des Servers wartet wirklich" fiel knapp unter 50 ms. Die Grenze ist jetzt 45 ms; ähnliche Tests könnten ebenso kippen.
