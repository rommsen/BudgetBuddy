---
id: ledger-m7mye
title: "Betrag einer Buchung im Detail bearbeiten, auch in YNAB"
status: backlog
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
