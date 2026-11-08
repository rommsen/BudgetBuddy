---
id: ledger-qvjvs
title: "Buchung zum Übertrag machen: Konten im Kategorie-Picker des Kassenbuch-Details, Schreibweg nach YNAB"
status: todo
type: feature
context: ledger
created: 2026-11-08
completed:
depends_on: []
blocks: []
tags: [etappe-2, ynab, uebertrag, picker]
related_adrs: [0055, 0087, 0021]
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
