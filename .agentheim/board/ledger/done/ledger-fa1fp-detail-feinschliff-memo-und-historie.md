---
id: ledger-fa1fp
title: "Detail-Feinschliff nach den Tafeln: Memo in BB bearbeiten mit Schreibweg nach YNAB, Historie nach dem Aufteilen mit Zielen und Beträgen, Kategorie am Handy in Kurzform, Fußtext im Picker, Auswahl-Bausteine im Styleguide"
status: done
type: feature
context: ledger
created: 2026-10-25
completed:
depends_on: [design-system-001, ledger-xfjcp]
blocks: []
tags: [frontend, backend, kassenbuch, detail, memo, historie, styleguide, tafeln]
related_adrs: [0045, 0050, 0055, 0058, 0059, 0062]
related_research: []
prior_art: [ledger-q1hw2, ledger-pafkp, ledger-sfab0, ledger-nhwrt]
---

## Why
Abgleich der Design-Tafeln (2026-10-25, Ablauf 4, 5 und 7). Mehrere kleinere Lücken im
Detail, gebündelt, weil sie dieselbe Fläche berühren. Das Memo lässt sich in BB nicht
bearbeiten (Detail zeigt "Kein Memo"), die Tafeln sehen "Memo hinzufügen" vor (5.2, 7.8).

## What
1. **Memo bearbeiten.** Im Detail lässt sich das Memo setzen, ändern und leeren (Handy und PC).
   Schreibweg nach dem Muster der Kategorie (ADR 0055): Befehl, Ereignis mit alt und neu,
   PATCH/PUT nach YNAB, Fehlschlag-Zustand mit Hinweisleiste und "Erneut senden", Historie-Zeile,
   Toast mit Rückgängig. Gesperrt bei gelöschten Buchungen. Leerer Text heißt "kein Memo".
2. **Historie nach dem Aufteilen.** Die Historie-Zeile eines Aufteilens nennt die Zeilen mit
   Kategorie und Betrag (Tafel 4.7), statt nur deren Anzahl.
3. **Kategorie am Handy kurz.** Im Detail-Sheet am Handy steht die Kategorie in Kurzform
   (Tafel 4.9); voll qualifiziert bleibt sie am PC und bei Split-Zeilen nach ADR 0045, sofern
   die Kurzform dort mehrdeutig wäre.
4. **Fußtext im Picker.** Der Kategorie-Picker am PC zeigt unten "Esc schließt ohne zu
   ändern · Klick wählt sofort" (Tafel 4.2).
5. **Styleguide.** `/styleguide` zeigt den Baustein `MultiSelect`: Summenzeile im
   Auswahl-Zustand, Auswahl-Leiste Handy und PC, Kopfzeilen-Kästchen. Der veraltete
   Kopfkommentar in `MultiSelect.fs` wird berichtigt.

## Acceptance criteria
- [ ] Domain-/Validierungs-Tests für das Memo: Setzen, Ändern, Leeren, Ablehnung bei gelöschter Buchung, Längengrenze nach YNAB.
- [ ] Server-Test: der Memo-Befehl schreibt das Ereignis mit alt und neu und schickt das Memo nach YNAB; Fehlschlag setzt den Zustand "Memo nicht in YNAB"; Retry sendet erneut.
- [ ] Reducer-Test: Memo speichern zeigt Toast mit Rückgängig; Rückgängig stellt das alte Memo wieder her.
- [ ] Die Historie-Zeile nach dem Aufteilen enthält jede Zeile mit Kategorie und Betrag (Test am QueryHandler bzw. an der Formatierung).
- [ ] View-Test: Handy-Detail zeigt die Kategorie in Kurzform, PC-Detail unverändert.
- [ ] Der Picker am PC rendert den Fußtext; am Handy nicht.
- [ ] `/styleguide` rendert die drei Auswahl-Bausteine (Playwright oder View-Test über die Styleguide-Seite).
- [ ] Playwright: Memo im Detail setzen, Liste und Detail zeigen das neue Memo.
- [ ] `guard:tokens` und `guard:classes` grün.

## Notes
- Tafeln: `docs/ux/kassenbuch/tafeln/` (Ablauf 4, 5, 7).
- Hängt an `ledger-xfjcp`: dieselbe Hinweisleiste im Detail (`DetailNotice`, `detailNoticeActionMsg`), und der Memo-Schreibweg folgt dem Flag-Schreibweg als Muster.
- Das Memo ist der größte Teil. Wird die Task zu groß, darf der Worker die Punkte 2 bis 5 in einer Folge-Task melden, statt sie halb zu bauen.
- Gap-Bericht Ablauf 3 und 4 ("Offen, ohne Task"), Ablauf 5 bis 7 Punkt 6.

## Merge-conflict note (iteration 1)

**Sibling:** ledger-y54gr (Lena fragen in der Auswahl-Leiste), integriert als 96659fe.
**Neue Basis:** 96659fe (main).
**Konfliktdateien (Allow-List):** `src/Server/Api.fs`, `src/Server/Ledger/CommandHandlers.fs`, `src/Shared/Api.fs`.
**Zusätzlich nötig:** y54gr-Tests brauchen das neue Pflichtfeld `MemoWriteBackFailure = None`; `/styleguide` ruft `MultiSelect.barMobile`/`barDesktop` mit den zwei neuen `LenaAction`-Argumenten; Kopfkommentar in `MultiSelect.fs` berichtigen.
**Gesichert:** `.agentheim/salvage/ledger-fa1fp-merge-conflict.patch`.

## Outcome

Alle fünf Punkte sind gebaut, nichts abgespalten.

1. **Memo bearbeiten.** Shared: `normalizeMemo`, `ynabMemoMaxLength = 500`, `MemoOutcome`, `MemoChange`, `MemoWriteBack`, `LedgerEntryView.MemoWriteBackFailure`; `LedgerApi.setEntryMemo` und `retryMemoWriteBack`. Server: drei Ereignisse (Codec, Namen, Version 1), `Entry.MemoWriteBackFailure` samt Spalte `memo_write_back_failure`, `apply`, `observe` mit Baseline, `CommandHandlers.setEntryMemo` und `retryMemoWriteBack`, `YnabClient.buildMemoBody/sendMemo/updateMemo`. Client: `MemoEdit`, `MemoRows`, Nachrichten `StartMemoEdit` bis `MemoRetried`, Toast `UndoMemo`, Hinweisleiste `MemoNotice`, Memo-Zeile mit Knopf "Memo hinzufügen" bzw. "Bearbeiten", Eingabefeld `Detail.memoEditor` (Enter speichert, Esc schließt nur das Feld, Zähler, Grund bei zu langem Text), Historie wird nach der Änderung neu geladen.
2. **Historie nach dem Aufteilen:** `ReadModel.describeRecategorized` nennt Ziel und Betrag je Zeile.
3. **Kategorie am Handy kurz:** `View.detailCategoryLabel` (Sheet: Blattname, Panel: qualifiziert); Split-Zeilen bleiben qualifiziert (ADR 0045).
4. **Fußtext im Picker:** `CategoryPicker.popoverFooterHint` nur in den Popover-Hüllen, nie im Sheet.
5. **Styleguide:** Sektion "28. Mehrfachauswahl in Listen" (Kästchen, Kopfzeilen-Kästchen, Summenzeile, Leiste Handy und PC, die Leisten über Helfer `demoBarMobile`/`demoBarDesktop`, damit das Nachziehen auf die zwei neuen Argumente von ledger-y54gr eine Stelle ist) und die Karte "Detail: Memo bearbeiten".

**Annahme:** Die YNAB-Grenze für `memo` ist 500 Zeichen (OpenAPI), online nicht geprüft. Weicht bewusst von `memoLimit = 300` des Imports ab (ADR 0062).

**MultiSelect-Kopfkommentar:** erledigt. y54gr hat den Kommentar in `MultiSelect.fs` selbst berichtigt, die Merge-Fassung (ohne Gedankenstrich, nennt Geprüft, Kategorie setzen und Lena) gilt; ich ändere die Datei nicht.

**Auflösung des Merge-Konflikts (Iteration 2, Basis 96659fe):** In `src/Shared/Api.fs`, `src/Server/Api.fs` und `src/Server/Ledger/CommandHandlers.fs` hatten beide Seiten direkt hinter dem Flag-Block angefügt; beide Absichten bleiben vollständig erhalten (Memo-Member `setEntryMemo`/`retryMemoWriteBack`, `memoPush`, Memo-Abschnitt der Befehle neben `setLenaFlagBatch`/`restoreFlagBatch`, `flagBatchPush` und dem Lena-Sammelabschnitt). Nachgezogen: `MemoWriteBackFailure = None` im neuen Record-Literal von `LedgerLenaFlagBatchClientTests.fs`; `/styleguide` ruft die Leisten über `demoBarMobile`/`demoBarDesktop` mit den zwei neuen `MultiSelect.LenaAction`-Argumenten (`demoLena`, `demoWithdraw`) auf. Die Hinweisleiste bleibt: `MemoNotice` steht hinter `FlagNotice`.

**Hinweis zur Zusammenführung mit y54gr:** `LedgerEntryView` und `Entry` haben ein neues Pflichtfeld `MemoWriteBackFailure`. Neue Record-Literale in y54gr-Tests (`FlagWriteBackFailure = None`) brauchen die Zeile `MemoWriteBackFailure = None`. Bestehende Tests habe ich ergänzt (15 Dateien). Zwei bestehende Tests angepasst: `LedgerDetailTests.fs` (Memo-Zeile lebt in `memoField`, Kategorie-Form). Die YNAB-Beobachtungssätze der Historie ("In YNAB in 2 Split-Zeilen aufgeteilt") blieben unverändert.

**Prüfläufe im Worktree:** `dotnet build` 0 Warnungen, 0 Fehler. `dotnet test` 2273 bestanden, 7 übersprungen (Integrationstests ohne .env), 0 Fehler. `npm run build` ok. `npm run guard:classes` und `guard:tokens` grün. `npm run test:scripts` seriell (`--test-concurrency=1`): 243 von 243 bestanden, darunter `ledger-memo.test.mjs` und `ledger-lena-batch.test.mjs`, darunter `scripts/ledger-memo.test.mjs` (8 Tests: Setzen mit Liste und Detail, Rückgängig, Ändern und Leeren, Grenze und Esc, Hinweisleiste, gelöscht gesperrt, Kategorie kurz am Handy und Fußtext nur am PC, Handy-Sheet, /styleguide am echten Element PC und Handy). Optik der Karten per Screenshot im Scratchpad geprüft.

**Tests:** `src/Tests/LedgerMemoTests.fs` (Regel, Codec, Befehle gegen In-Memory-SQLite, observe mit Baseline, Historie, PUT gegen Fake-Handler), `src/Tests/LedgerMemoClientTests.fs` (Bausteine, Reducer, Ansicht), `scripts/ledger-memo.test.mjs`.

### README von Hand
1. Abschnitt `## Key events`, nach der Zeile `- *EntryFlagChangedInBb* / *EntryFlagWriteBackFailed* / *EntryFlagWrittenBackToYnab*: ...` neue Zeile:
`- *EntryMemoChangedInBb* / *EntryMemoWriteBackFailed* / *EntryMemoWrittenBackToYnab*: Memo in BB gesetzt, geändert, geleert oder wiederhergestellt (ein Typ mit Alt und Neu), das Zurückschreiben nach YNAB gescheitert (Grund, Baseline-Memo) oder angekommen (ADR 0062, Schema-Version 1).`
2. `## Key commands`: Zeile `- *BuchungÄndern* (v2) — Kategorie / Split / Memo in BB ändern, Push folgt.` ersetzen durch `- *BuchungÄndern* (v2) — Kategorie / Split in BB ändern, Push folgt. Das Memo ist gebaut (`setEntryMemo`, ADR 0062).`
3. `## Ereignisse`, nach dem Eintrag `EntryFlagWrittenBackToYnab` und vor `EntryReplacedInYnab` einfügen:
```
- `EntryMemoChangedInBb`: das Memo wurde in BB geändert (ADR 0062). Payload `OldMemo`, `NewMemo`
  (getrimmt, `None` für kein Memo). Ein Typ für Setzen, Ändern, Leeren und "Rückgängig".
  Schema-Version 1, Actor `UserInBb`. Setzt `Entry.Memo` und leert `MemoWriteBackFailure`.
  Historien-Sätze: "Memo gesetzt: <neu>", "Memo geändert: <alt> → <neu>", "Memo entfernt: <alt>".
- `EntryMemoWriteBackFailed`: das Zurückschreiben des Memos nach YNAB ist gescheitert (ADR 0062).
  Payload `Reason`, `BaselineMemo` (der `OldMemo`-Wert der ersten gescheiterten Änderung; bei einem
  schon offenen Fehlschlag bleibt die erste Baseline). Schema-Version 1, Actor `UserInBb`. Setzt
  `Entry.MemoWriteBackFailure`. Historien-Satz: "Memo nicht in YNAB: <Grund>".
- `EntryMemoWrittenBackToYnab`: der offene Memo-Fehlschlag ist beendet. Kein Payload, Schema-Version 1.
  Actor `UserInBb` nach Erfolg bei offenem Fehlschlag oder "Erneut senden", Actor `YnabMirror`, wenn der
  Spiegel YNAB auf dem BB-Stand sieht. Historien-Satz: "Memo in YNAB angekommen".
```
4. `## Ereignisse`, Eintrag `EntryDetailsChangeObservedInYnab` (Zeile `- `EntryDetailsChangeObservedInYnab`: Datum, Betrag, Payee, Memo oder Konto geändert (alt/neu).`) ergänzen um: `Bei offenem Memo-Fehlschlag (ADR 0062): zeigt YNAB das BB-Memo, entsteht `EntryMemoWrittenBackToYnab` (Actor `YnabMirror`); zeigt YNAB die Baseline, bleibt das Memo und der Fehlschlag; jeder andere Stand ist eine echte Änderung, die den Fehlschlag in `apply` beendet.`
5. Eintrag `EntryRecategorized` (Ereignisse): anhängen `Historien-Satz bei Aufteilung: "In BB umkategorisiert: Aufgeteilt in <Ziel> <Betrag>, ..." (Ziel und Betrag je Zeile, ledger-fa1fp).`
6. Prosa-Bullet `**Buchungs-Detail**` (Ubiquitous language): "Hinweisleiste (in YNAB gelöscht, Noch nicht in YNAB, Prüfvermerk nicht in YNAB)" ist seit Kategorie, Flag und Memo unvollständig. Neu: "Hinweisleiste (in YNAB gelöscht, Noch nicht in YNAB, Prüfvermerk, Kategorie, Flag oder Memo nicht in YNAB)", und "Felder (Payee, Kategorie, Split, Memo)" ergänzen um: "Memo mit Knopf Memo hinzufügen bzw. Bearbeiten und Eingabefeld; die Kategorie steht im Sheet am Handy kurz, am PC qualifiziert".
