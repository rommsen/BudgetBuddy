---
id: ledger-q42pg
title: "Betrag bearbeiten: leerer Betrag oder 0 als Meldung am Feld statt Toast"
status: todo
type: bug
context: ledger
created: 2026-11-10
completed:
depends_on: []
blocks: []
tags: [laufend, frontend, betrag, validierung, styleguide]
related_adrs: [0092]
related_research: []
prior_art: [ledger-m7mye]
---

## Why
Roman (2026-11-10, Blick auf ledger-m7mye): "hier brauche ich ne validation msg am feld, kein toast".
Heute zeigt das Sheet bzw. der Dialog "Betrag" bei leerem Feld oder 0 einen Toast
"Bitte einen Betrag größer als 0 eingeben" (`State.fs:2357`, `AmountSaveRejected`). Der Styleguide §5 sagt:
Pflichtfeld-Hinweis über `Input.group*` bzw. `ErrorDisplay.inline'`, Toast nur für flüchtiges Feedback nach
einer Aktion. Die Task ledger-m7mye hatte den Toast vorgegeben; der Review-Lauf 01b989fc..8561beb8 hat es als
Frage in die Kann-Sammlung gelegt (ledger-k4n2s Punkt 35), Roman hat entschieden.

## What
- Speichern mit leerem Feld oder 0 zeigt die Meldung "Bitte einen Betrag größer als 0 eingeben" direkt unter
  dem Betragsfeld (DS-Baustein für Feldfehler, Styleguide §5), kein Toast. Das Sheet bleibt offen, der Fokus
  bleibt im Feld, die Tastatur bleibt am Handy offen.
- Die Meldung verschwindet, sobald Roman tippt (Muster der anderen Formulare mit Feldfehler, etwa Erfassen
  aus ledger-at63w; der Worker nimmt das bestehende Muster, keine neue Variante).
- Der Feldfehler gehört in den Zustand des Sheets (`AmountEdit`), nicht in einen Toast.
- Betrifft Handy (Sheet) und PC (Dialog) gleich.

## Acceptance criteria
- [ ] Client-`update` unter .NET: Speichern mit leer und mit 0 setzt den Feldfehler im `AmountEdit`, ruft keinen
      Befehl und erzeugt keinen Toast; Tippen im Feld löscht den Fehler; ein gültiger Betrag speichert wie bisher.
      Der Test `LedgerAmountClientTests.fs:226` (Toast bei leer) wird auf den Feldfehler umgestellt.
- [ ] Die Meldung steht unter dem Feld, im DS-Baustein für Feldfehler (`ErrorDisplay.inline'` oder
      `Input.groupWithError`), mit `aria-describedby` am Feld bzw. dem, was der Baustein dafür mitbringt.
- [ ] Layout-Sweep: Zustand "Betrag mit Feldfehler" am Handy mit offener Tastatur in `scripts/layout-states.mjs`.
- [ ] `amountMissingToastText` umbenannt oder entfernt, keine verwaisten Texte.
- [ ] Am Handy bei 412 px mit Touch geprüft: Meldung sichtbar über der Tastatur, Tastatur bleibt offen. [human-eye]
- [ ] BC-README ledger: Satz zum Toast bei leerem Betrag nachgezogen (grep nach "größer als 0" und "Toast").
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Aus der Kann-Sammlung ledger-k4n2s Punkt 35 entnommen; die übrigen Teile von Punkt 35 bleiben dort.
- Klein, ein Worker-Lauf.
