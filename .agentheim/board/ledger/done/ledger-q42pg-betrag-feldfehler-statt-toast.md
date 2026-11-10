---
id: ledger-q42pg
title: "Betrag bearbeiten: leerer Betrag oder 0 als Meldung am Feld statt Toast"
status: done
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

## Outcome

Leer oder 0 im Sheet "Betrag" (Handy) bzw. Dialog (PC) ist jetzt ein Feldfehler statt Toast.

- `AmountEdit` trägt `AmountFieldError: string option` (`src/Client/Components/Ledger/Types.fs`), der Text heißt `amountMissingText` (vorher `amountMissingToastText`, keine Reste).
- `StartSaveAmount` setzt bei `AmountSaveRejected` den Feldfehler, ohne Befehl und ohne Toast; `AmountDraftChanged` löscht ihn, ein Wechsel Ausgabe/Einnahme nicht (`src/Client/Components/Ledger/State.fs`).
- View (`amountFormBody`, `amountFooter` in `src/Client/Components/Ledger/View.fs`): die Meldung steht in `ErrorDisplay.inline'` unter dem Feld (Wrapper `id/data-testid amount-input-error`), das Feld bekommt `DescribedBy`, `AriaInvalid` und die bestehende Klasse `input-invalid` (Muster der Budget-Betragsfelder). "Speichern" sitzt in einem `div` mit `preventDefault` auf `mousedown` (Muster `Button.amountStep`), damit das Feld den Fokus und die Tastatur offen behält.
- Tests (`src/Tests/LedgerAmountClientTests.fs`): der Toast-Test ist auf den Feldfehler umgestellt, neu "Tippen löscht den Feldfehler, ein gültiger Betrag speichert danach wie bisher". Beide waren vor dem Fix rot (Feld existierte schon, Reducer zeigte noch den Toast), danach grün.
- Layout-Sweep: neuer Zustand `kassenbuch-betrag-feldfehler` in `scripts/layout-states.mjs` (320 und 390 px): leeres Feld, Tipp auf Speichern; `open` prüft Fokus im Feld, `aria-describedby`/`aria-invalid` und dass kein Toast erscheint; mit offener Tastatur müssen Meldung und Speichern im Bild sein. Ausnahme bei 320 px, Invariante 7, nur die Meldung: dort bricht sie zweizeilig um und liegt 22 px unter dem Rand des Sheet-Inhalts; `scroll-margin` am Feld half nicht (`scrollIntoView nearest` ignoriert es, solange das Feld sichtbar ist). Ab 390 px kein Befund.
- Screenshots bei 412 px (Touch-Emulation, Playwright): /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f0e35e7-0a1a-417d-a083-15b0c6af6c56/scratchpad/q42pg/betrag-feldfehler-412.png und mit halber Höhe als Tastatur /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f0e35e7-0a1a-417d-a083-15b0c6af6c56/scratchpad/q42pg/betrag-feldfehler-412-tastatur.png (Meldung über dem Tastaturrand sichtbar); zum Vergleich /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f0e35e7-0a1a-417d-a083-15b0c6af6c56/scratchpad/q42pg/betrag-feldfehler-320-tastatur.png. Der echte Test am Galaxy mit Touch [human-eye] steht für Roman aus.
- `./verify.sh`: VERIFY: PASS (dotnet 4644, node 311).
*Conductor bei der Integration:* Der Satz zur Übertrags-Sperre im README-Bullet "Betrag bearbeiten" nennt jetzt das Ergebnis des Probelaufs ledger-m7a9q statt "ungeprüft".
