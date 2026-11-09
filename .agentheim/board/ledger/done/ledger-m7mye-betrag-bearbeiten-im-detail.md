---
id: ledger-m7mye
title: "Betrag einer Buchung im Detail bearbeiten, auch in YNAB"
status: done
type: feature
context: ledger
created: 2026-11-09
completed:
depends_on: []
blocks: []
tags: [etappe-2, ynab, detail, betrag]
related_adrs: [0006, 0053, 0055, 0058, 0083, 0092]
related_research: []
prior_art: [ledger-m7k2d, ledger-pafkp]
---

## Why
Etappe 2 des Fahrplans (Lücken schließen, ADR 0086). Einen Betrag korrigieren (Tippfehler bei einer Barbuchung,
Trinkgeld nachtragen) geht heute nur in YNAB; in BB lässt sich der Betrag einer Buchung nicht ändern. Im
Prototyp ist das ab Version 15 vorgesehen (Roman, 2026-11-08).

## What
Entscheidung Roman (2026-11-09, ADR 0092 Punkte 3 und 4).

- **Wer:** jede Buchung außer abgeglichenen, gelöschten, Noch nicht in YNAB und Überträgen (Übertrags-Ziel
  als ganze Buchung oder als Split-Zeile). Auch Comdirect-Importe; Bank-Referenz und `import_id` bleiben.
- **Befehl `setEntryAmount`** (Sprache: *BetragSetzen*): Validierung an der API-Grenze (Betrag größer 0,
  Richtung Ausgabe oder Einnahme, höchstens zwei Nachkommastellen), unter `WriteGuard.exclusive` ein neues
  Ereignis `EntryAmountChangedInBb` (Entscheidungs-Familie, Actor `UserInBb`, alter und neuer Betrag), dann
  der Push. Eine gewöhnliche Buchung: Update mit dem Betrag nach YNAB (Milliunits per `Encode.int`, sonst
  verwirft YNAB still). Scheitert der Push, bleibt der Betrag in BB gesetzt und das Detail zeigt "Betrag nicht
  in YNAB" mit "Erneut senden" nach dem Muster von Memo und Kategorie.
- **Aufgeteilte Buchung:** wie im Prototyp öffnet das Speichern des Betrags den Split-Editor mit dem neuen
  Betrag (Hinweis "Die Buchung ist aufgeteilt. Nach dem Speichern passt du die Aufteilung an den neuen Betrag
  an."). Erst das Speichern einer gültigen Aufteilung schickt Betrag und Zeilen zusammen als einen Ersatz
  (DELETE und POST, ADR 0058 Punkt 2, Ersatz-`import_id`). Bricht Roman im Split-Editor ab, ändert sich
  nichts, weder in BB noch in YNAB. Ein Befehl für beides oder eine Erweiterung von `setEntrySplits` um den
  Betrag; die Task nimmt die Erweiterung an.
- **Rückgängig** im Toast "Betrag geändert · <Betrag>" schreibt den alten Betrag auf demselben Weg zurück
  (zweites `EntryAmountChangedInBb`); bei einer aufgeteilten Buchung gibt es kein Rückgängig im Toast, weil
  die alte Aufteilung zum alten Betrag gehört (Abweichung, siehe unten).
- **Wirkung:** Kontostand, Budget (Aktivität der Kategorie) und Tagessumme folgen dem neuen Betrag.
- **Client (Detail, ADR 0053), nach dem Prototyp:** der Betrag im Kopf des Details ist ein Knopf
  ("Betrag bearbeiten", mit Pfeil `›` dahinter, Mono, groß). Er öffnet ein Sheet bzw. Panel "Betrag" mit dem
  Payee als Untertitel, darin ein Umschalter "Ausgabe | Einnahme" (vorbelegt aus der Buchung), ein Betragsfeld
  ohne Vorzeichen (`inputmode="decimal"`, rechtsbündig, Mono, groß, vorbelegt mit dem heutigen Betrag ohne
  Tausenderpunkt), bei einer aufgeteilten Buchung der Hinweis oben, darunter "Geht auch an YNAB. Bei einer
  importierten Buchung bleibt die Bank-Referenz erhalten." und unten "Abbrechen" und "Speichern". Ein Betrag
  von 0 oder leer: Toast "Bitte einen Betrag größer als 0 eingeben". Unverändert gespeichert: zurück ins
  Detail ohne Aufruf. Gesperrt: der Betrag ist kein Knopf (kein Pfeil), mit Grund als Hinweistext wie bei der
  Kategorie (`categoryLockFor` als Vorbild).
- **Historie:** "Betrag geändert, vorher <alter Betrag>" mit Actor Roman und dem neuen Betrag.

**Abweichungen vom Prototyp** (Version 17, "BB Prototyp Handy", Ansichten Buchungs-Detail und Sheet "Betrag"):
- Überträge, abgeglichene, gelöschte und nicht verknüpfte Buchungen sind gesperrt (ADR 0092 Punkt 3 und 4);
  der Prototyp sperrt nichts.
- Kein Rückgängig im Toast nach dem Ändern einer aufgeteilten Buchung: die alte Aufteilung passt nur zum alten
  Betrag, ein Rückgängig wäre ein zweiter Ersatz mit alter Aufteilung. Der Prototyp zeigt Rückgängig auch da.
- "Betrag nicht in YNAB" mit "Erneut senden" bei gescheitertem Push: im Prototyp nicht gezeigt, BB kann es bei
  Memo und Kategorie heute schon (BB gilt).

## Acceptance criteria
- [ ] Validierung (`src/Server/Validation.fs`): leer, 0, negativ, drei Nachkommastellen und unbekannte Richtung
      abgelehnt, alle Fehler gesammelt.
- [ ] Handler: `setEntryAmount` auf eine gewöhnliche Buchung hält `EntryAmountChangedInBb` fest und schickt ein
      Update mit dem Betrag in Milliunits als JSON-Zahl; Kontostand und Aktivität der Kategorie folgen.
- [ ] Handler: auf einen Comdirect-Import gelingt es, `import_id` und Bank-Referenz bleiben unverändert.
- [ ] Handler: gescheiterter Push lässt den Betrag in BB gesetzt und hält den offenen Fehlschlag fest;
      "Erneut senden" schickt ihn nach.
- [ ] Handler: abgelehnt ohne Ereignis und ohne Aufruf bei abgeglichen, gelöscht, Noch nicht in YNAB,
      Übertrag (ganz) und Split mit Übertrags-Zeile, je ein Fall; unveränderter Betrag ohne Ereignis.
- [ ] Handler: Split-Buchung mit neuem Betrag und gültiger Aufteilung läuft als ein Ersatz (DELETE, POST mit
      neuem Betrag und neuen Zeilen, Ersatz-`import_id`); eine Aufteilung, deren Summe nicht zum neuen Betrag
      passt, wird abgelehnt (`mkSplits`).
- [ ] Spiegel: der nach dem Push beobachtete YNAB-Betrag dreht die BB-Entscheidung nicht zurück; ein später in
      YNAB geänderter Betrag wird übernommen (wie bei der Kategorie, ADR 0055).
- [ ] Endpoint-Negativtabelle hat je neuem Endpoint eine Zeile; neue Pflichtfelder in `src/Shared` ziehen die
      Playwright-Fixtures im selben Schritt mit.
- [ ] Client-`update` unter .NET: Sheet öffnen mit Vorbelegung (Richtung und Betrag), Speichern ruft den Befehl,
      0 oder leer zeigt den Toast, unverändert schließt ohne Aufruf, Split öffnet den Split-Editor mit neuem
      Betrag, Rückgängig im Toast ruft den Befehl mit altem Betrag (nicht bei Split). Sperrlogik als reine
      Funktion.
- [ ] Browser-Wächter `[tastatur]` oder `[layout]` nur, wenn eine Aussage nur der Browser treffen kann; das Sheet
      "Betrag" als neuer Zustand im Layout-Sweep (`scripts/layout-states.mjs`).
- [ ] Eingabe mit Touch bei 412 px geprüft: Betragsfeld ohne Vorzeichen, die Zifferntastatur öffnet sich, das
      Feld zeigt den vorbelegten Betrag. [human-eye]
- [ ] Seite neben der Skizze verglichen (Detail und Sheet "Betrag" am Handy 412 px, Detail mit Panel am PC,
      Screenshot je Ansicht neben dem Prototyp im Outcome), Abweichungen nur die genannten. [human-eye]
- [ ] BC-README ledger: Befehl, Ereignis, Schreib-Sperre, Buchungs-Detail, Hinweisleiste nachgezogen, alte Sätze
      an allen Stellen (grep nach "Betrag", "nur in YNAB").
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- ADR 0092 (Entscheidung), ADR 0058 (Split-Betrag wird von YNAB still ignoriert, Ersatz), ADR 0006 (Splits),
  ADR 0055 (Rückschreiben und Fehlschlag), ADR 0053 (Detail), ADR 0083 (Ereignis).
- Beträge sind `Money` (decimal), an der YNAB-Grenze Milliunits als JSON-Zahl (`Encode.int`), sonst verwirft
  YNAB still (CLAUDE.md).
- Der Conductor legt beim Bauen die Prototyp-Datei und Screenshots der Ansichten Detail und Sheet "Betrag" in
  den Scratchpad und gibt Worker, Verifier und `design-quality` die Pfade (CLAUDE.md, Skizzen).
- Beide Tasks (mit ledger-z6sya) ändern das Detail und `CommandHandlers.fs`; nacheinander integrieren.

## Merge-conflict note (iteration 1)
- Geschwister: ledger-z6sya (Buchung löschen im Detail, mit Rückgängig), auf `main` als 67becce0.
- Neue Basis: 67becce0f227f34a7cfba9702461a427b235a631
- Konflikte (Allow-List): `src/Client/Components/Ledger/View.fs`, `src/Tests/Tests.fsproj`.
- Zusätzlich semantisch: z6sya bringt das Pflichtfeld `LedgerEntryView.Reconciled: bool`; neue Konstruktionen von `LedgerEntryView` aus ledger-m7mye müssen es setzen.
- Gesicherter Diff: .agentheim/salvage/ledger-m7mye-merge-conflict.patch

## Outcome

Betrag bearbeiten im Detail gebaut (ADR 0092 Punkt 3 und 4). `./verify.sh` nach dem Merge mit ledger-z6sya: VERIFY: PASS (dotnet 4635 grün, node 311 grün).

**Server**
- Ereignisse `EntryAmountChangedInBb` (OldAmount/NewAmount mit Vorzeichen), `EntryAmountWriteBackFailed` (Reason, BaselineAmount), `EntryAmountWrittenBackToYnab`, alle Schema-Version 1 (`src/Server/Ledger/Events.fs`). `Entry.AmountWriteBackFailure`, Spalte `amount_write_back_failure` (Migration), `apply` und `observe` mit Baseline-Regel wie beim Memo (`src/Server/Ledger/Domain.fs`, `Persistence.fs`).
- `Server.Validation.validateAmountChange`: leer, 0, negativ, drei Nachkommastellen, über 1.000.000 und unbekannte Richtung, alle Fehler gesammelt; Ergebnis mit Vorzeichen.
- `Ledger.CommandHandlers`: `amountNotChangeableReason` (eine Quelle für Befehl und Lesepfad: gelöscht, abgeglichen, Noch nicht in YNAB, Übertrag, Split mit Übertrags-Zeile, Kategorie/Aufteilung noch nicht in YNAB), `setEntryAmount`, `retryAmountWriteBack` (Split per Ersatz), `setEntrySplitsWithAmount`; `setEntrySplits` und die Variante mit Betrag teilen `changeSplits` (die Task-Annahme "Erweiterung von setEntrySplits"; als eigener Remoting-Endpoint, damit bestehende Aufrufer und Fixtures unverändert bleiben). `pushSplitState` hält bei einem Ersatz mit Betrag den Betrags-Fehlschlag fest und beendet ihn bei Erfolg.
- `YnabClient.buildAmountBody/sendAmount/updateAmount`: PUT mit nur `amount` als JSON-Zahl in Milliunits (`Encode.int`).
- Lesepfad: `LedgerEntryView.AmountWriteBackFailure` und `AmountLockReason` (beides Optionsfelder, Playwright-Fixtures bleiben gültig); Historie "Betrag geändert auf <neu>, vorher <alt>" (BB-Historie hat keine Betragsspalte, deshalb steht der neue Betrag im Satz), "Betrag nicht in YNAB: <Grund>", "Betrag in YNAB angekommen".
- API `setEntryAmount`, `retryAmountWriteBack`, `setEntrySplitsWithAmount` (`src/Shared/Api.fs`, `src/Server/Api.fs`), je eine Zeile in `RemotingEndpointNegativeTests.fs`.

**Client**
- Kopf des Details: Betrag als Knopf `Detail.amountButton` mit `›`, gesperrt `Detail.amountLocked` mit Grund darunter (`src/Client/DesignSystem/Detail.fs`); Sperrlogik rein in `amountLockFor` (Grund des Lesepfads plus Split-Sperre).
- Sheet "Betrag" am Handy ersetzt den Inhalt des Detail-Sheets (wie der Split-Editor, kein Stapel), am PC `Modal` über der Liste, Panel bleibt rechts. Segmented "Ausgabe | Einnahme", Betragsfeld `inputmode="decimal"` (Klassen wie Neue Buchung), Split-Hinweis, YNAB-Satz, Abbrechen/Speichern.
- Reine Entscheidung `amountSaveStep`: leer/0 Toast "Bitte einen Betrag größer als 0 eingeben", unverändert schließt ohne Aufruf, Split öffnet den Split-Editor mit neuem Gesamtbetrag (`SplitEditor.startStateWithAmount`, `AmountFrom`), sonst `setEntryAmount`. Toast "Betrag geändert · <Betrag>" mit Rückgängig (`UndoAmount`), bei Split ohne Rückgängig. Hinweisleiste "Betrag nicht in YNAB" mit "Erneut senden" (`detailNoticeWithAmount`). Kontostände und Historie laden nach einer Änderung neu.
- Layout-Sweep: neue Zustände `kassenbuch-betrag` (Handy, mit Tastatur-Prüfung) und `kassenbuch-betrag-dialog` (1440) in `scripts/layout-states.mjs`; kein eigener Browser-Test.

**Tests**: `src/Tests/LedgerAmountTests.fs` (Validierung, Ereignisse, Befehle gegen In-Memory-SQLite inkl. Comdirect-Import, Kontostand und Aktivität, Fehlschlag/Erneut senden, Sperren je Fall, Split-Ersatz, Schreib-Sperre, Spiegel mit Baseline, Historie, PUT-Body), `src/Tests/LedgerAmountClientTests.fs` (Reducer und reine Bausteine).

**Vergleich mit der Skizze (Prototyp Version 17)**: Screenshots von BB mit Mock-Daten unter
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/09c6dbe6-3a1e-4560-9130-ac0c7d910325/scratchpad/shots/` (`bb-handy-sheet-betrag.png` neben `prototyp/handy-sheet-betrag.png`, `bb-pc-panel-betrag.png` neben `prototyp/pc-panel-betrag.png`, `bb-pc-detail-panel.png` neben `prototyp/pc-detail-panel.png`, `bb-handy-detail.png` neben `prototyp/handy-detail.png`). Gleich: Titel "Betrag" mit Payee, Ausgabe/Einnahme, großes rechtsbündiges Mono-Feld ohne Vorzeichen, YNAB-Satz, Abbrechen/Speichern nebeneinander, Betrag mit `›` im Kopf, am PC Dialog über der Liste mit Panel rechts. Abweichungen: die drei in der Task genannten (Sperren, kein Rückgängig bei Split, "Betrag nicht in YNAB"); dazu der DS-`Modal` mit seinem Hintergrund (dunkler und unscharf als im Prototyp), der Toast-Text formatiert wie BB sonst ("-45,00 €"). Touch bei 412 px auf echtem Gerät [human-eye] steht aus; der Layout-Sweep prüft das Sheet mit offener Tastatur bei den Handy-Breiten.

**Für den Conductor**: Die `## Ereignisse`-Einträge ließen sich nicht per Delta anhängen (Abschnitt endet mit Fließtext, Gate Prüfung 3). Bitte von Hand unter "Entscheidungen" nach `EntryMemoWrittenBackToYnab` einsetzen:

- `EntryAmountChangedInBb`: der Betrag wurde in BB geändert (ADR 0092, ledger-m7mye). Payload `OldAmount`, `NewAmount` (Money mit Vorzeichen). Ein Typ für Ändern und "Rückgängig". Schema-Version 1, Actor `UserInBb`. Setzt `Entry.Amount` und leert `AmountWriteBackFailure`. Historien-Satz: "Betrag geändert auf <neu>, vorher <alt>".
- `EntryAmountWriteBackFailed`: das Zurückschreiben des Betrags nach YNAB ist gescheitert (ADR 0092). Payload `Reason`, `BaselineAmount` (der `OldAmount` der ersten gescheiterten Änderung; bei einem schon offenen Fehlschlag bleibt die erste Baseline). Schema-Version 1, Actor `UserInBb`. Setzt `Entry.AmountWriteBackFailure`. Historien-Satz: "Betrag nicht in YNAB: <Grund>". Bei offenem Fehlschlag gilt für `EntryDetailsChangeObservedInYnab` dieselbe Baseline-Regel wie beim Memo: YNAB auf der Baseline lässt den BB-Betrag stehen, ein dritter Betrag beendet den Fehlschlag.
- `EntryAmountWrittenBackToYnab`: der offene Betrags-Fehlschlag ist beendet. Kein Payload, Schema-Version 1. Actor `UserInBb` nach Erfolg bei offenem Fehlschlag oder "Erneut senden", Actor `YnabMirror`, wenn der Spiegel YNAB auf dem BB-Stand sieht. Historien-Satz: "Betrag in YNAB angekommen".

Außerdem in der README die Zeile `EntryDetailsChangeObservedInYnab` (Abschnitt Ereignisse) um "Dieselbe Baseline-Regel gilt für den Betrag (ADR 0092)" ergänzen.

**Merge mit ledger-z6sya (Buchung löschen, Iteration 2)**: Konflikte in `src/Client/Components/Ledger/View.fs` (beide Teile behalten: `detailDeleteVisible` aus z6sya und der Kopf mit Betrags-Knopf `amountEditMsg`/`detailHead dispatch`; der Knopf "Buchung löschen" unter der Historie bleibt unverändert) und `src/Tests/Tests.fsproj` (beide Testdateien). Nachgezogen: `Reconciled = false` in `LedgerAmountClientTests.fs`, `AmountWriteBackFailure`/`AmountLockReason = None` in `LedgerEntryDeletionClientTests.fs`. Die Betrags-Sperre bleibt eine Quelle auf dem Server (`amountNotChangeableReason`, liest `Cleared = Reconciled` und `DeletedInYnab`, das `EntryDeletedInBb` setzt); der Client liest `AmountLockReason`, das neue Feld `LedgerEntryView.Reconciled` braucht er dafür nicht. Das README-Delta ist gegen die Bullets auf main (mit den z6sya-Ergänzungen "Buchung löschen" und *BuchungLöschen*) neu gebaut und vom Gate angenommen.
