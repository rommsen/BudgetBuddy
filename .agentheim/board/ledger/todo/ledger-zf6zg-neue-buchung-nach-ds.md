---
id: ledger-zf6zg
title: "„Neue Buchung“ nach DS: Kontowahl als Money.accountChoiceRow wie im Sheet „Konto“, Kopf des Panels wie der Kopf des Details, „Abbrechen“ ohne Füllung, Payee-Feld sagt „optional“"
status: todo
type: refactor
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide, ledger-gy77x]
blocks: []
tags: [bestandsreview, frontend, design-system]
related_adrs: [0053, 0087]
related_research: []
prior_art: [ledger-vf6z5, ledger-ea70s]
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G5, Teil 3 von 4 (Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`).
Im Formular „Neue Buchung“ markiert die Kontowahl das gewählte Konto anders als das Sheet „Konto“ (Akzentlinie links
statt Haken und Akzentrahmen), der Kopf im Panel hat einen anderen Schließen-Knopf als das Detail an derselben Stelle,
„Abbrechen“ steht mit Fläche (§2), und das Payee-Feld sagt nicht, dass es leer bleiben darf (§7.2).

## What
- **Kontowahl** (`Ledger/View.fs:4116` bis `:4123`, `captureAccountList`): jede Zeile über
  `Money.accountChoiceRow "capture-account-choice" account.Name <Saldo> isActive pick`. Saldo wie im Sheet „Konto“
  (`cardBalanceElement true`: Primärtext, negativ Rot), ohne bekannten Saldo `Html.none`. `PickerParts.ShortChoice`
  bleibt: es findet die Zeilen als `button` und setzt den Fokus auf `aria-current="true"`, das `accountChoiceRow` setzt.
  Lade-, Fehler- und Leerzeile bleiben. Gilt für Panel, Sheet und Dialog (alle über `captureAccountList`).
- **Kopf im Panel** (`Ledger/View.fs:4369` bis `:4384`, `capturePanelView`): neue DS-Funktion
  `Detail.formHead (title: string) (closeLabel: string) (closing: bool) (onClose: unit -> unit)`: derselbe Kopf wie
  `Detail.titleHead` ohne Unterzeile, Titel 17 px halbfett, rechts derselbe Schließen-Knopf (am Handy 48 px, am PC
  44 px), hier mit Namen „Formular schließen“ und `data-testid="capture-close"`. `closing = true` (während „Buchung
  anlegen“ läuft) setzt `aria-disabled`, kein `disabled`; `CloseCapture` tut dann schon nichts (`Ledger/State.fs:3347`).
  Dafür bekommt das private `closeButton` (`DesignSystem/Detail.fs:128`) Name, Test-Id und Sperre als Parameter;
  `titleHead` und `head` übergeben ihre heutigen Werte („Detail schließen“, `detail-close`, nicht gesperrt).
- **„Abbrechen“** (`Ledger/View.fs:4359` `captureCancelButton`): `Button.Outline`. Wirkt im Panel (`:4397`), im Sheet
  (`:4424`) und im Dialog auf der Übersicht (`:4498`).
- **Payee-Feld** (`Ledger/View.fs:4264`): Platzhalter „optional“ wie beim Memo (`:4320`) statt „z. B. Bäckerei“.
- **Styleguide §5** Zeile „Detail“: `formHead` (Kopf eines Formulars im Panel, „Neue Buchung“); Zeile „Button“
  `Outline`: „Neue Buchung“ nennen. `/styleguide` zeigt `formHead` neben `titleHead` (`Styleguide/View.fs:2345`).

Geänderter DS-Baustein und seine anderen Nutzer: `Detail` (`closeButton` intern, neue Funktion `formHead`):
`Detail.titleHead` im Buchungs-Detail (`Ledger/View.fs:1385`), `Detail.head` über dem Split-Editor, `/styleguide`
(`Styleguide/View.fs:2345`, `:2346`, `:2537`). Deren Aussehen und Namen bleiben.

Entscheidungen Conductor 2026-11-19, nach Empfehlung:
- `formHead` als eigene Funktion statt `titleHead` mit optionaler Unterzeile: die Test-Ids `detail-title`,
  `detail-subtitle` und `detail-close` bleiben eindeutig dem Buchungs-Detail (der Sweep klickt `detail-close`,
  `scripts/layout-states.mjs:488`).
- Der Container der Kontowahl im Panel bleibt, wie er ist; nur die Zeilen wechseln.
- Payee-Platzhalter „optional“: dasselbe Wort wie am Memo (§8 „Konsistente Begriffe“).

Betroffene Dateien: `src/Client/Components/Ledger/View.fs` (`captureAccountList` 4100, Payee 4264, `captureCancelButton`
4359, `capturePanelView` 4366), `src/Client/DesignSystem/Detail.fs`, `src/Client/Components/Styleguide/View.fs`,
`standards/frontend/styleguide.md` (§5 Detail, Button), `scripts/layout-states.mjs`.

## Acceptance criteria
- [ ] Kontowahl: jede Zeile ist `Money.accountChoiceRow`, das gewählte Konto trägt `aria-current="true"`, Haken und
  Akzentrahmen. Beleg: Diff; Sweep `kassenbuch-erfassen-konto` (Handy) und `kassenbuch-erfassen-konten-laden` grün,
  Invariante 2 (56 px je Zeile); der Tastatur-Fall der Kontowahl in `scripts/key-invariants.test.mjs:184` (↑↓ und Enter
  am PC) bleibt grün.
- [ ] Kopf im Panel: `Detail.formHead` mit „Neue Buchung“ und Schließen „Formular schließen“ (`capture-close`). Beleg:
  Sweep `kassenbuch-erfassen-panel` (1440 px) grün, Invariante 2 (44 px am PC); im Detail heißt der Knopf weiter
  „Detail schließen“ (`kassenbuch-detail` grün).
- [ ] Während „Buchung anlegen“ läuft, trägt Schließen `aria-disabled="true"`, ein Klick schließt nicht. Das Nicht-Schließen
  belegt `update` schon (`src/Tests/LedgerCaptureClientTests.fs:393`, „bleibt offen“); die ARIA-Sperre belegt der Diff.
- [ ] „Abbrechen“ ist in Panel, Sheet und Dialog `Button.Outline`. Beleg: Diff; Sweep `kassenbuch-erfassen` und
  `kassenbuch-erfassen-panel` grün.
- [ ] Das Payee-Feld zeigt den Platzhalter „optional“. Beleg: Diff (Doktrin Punkt 6).
- [ ] Styleguide §5 (Detail, Button) nachgezogen, `/styleguide` zeigt `formHead`.
- [ ] `npm run guard:classes` und `npm run guard:tokens` grün.
- [ ] Romans Blick: am Handy (412 px, Touch) „Neue Buchung“ als Sheet, Kontowahl öffnen, Konto wählen, zurück, Payee
  leer lassen; am PC 1440 px Panel mit Kopf und Kontowahl, auf der Übersicht 1440 px der Dialog. Vorher und nachher über
  `npm run screenshot:route`; Abweichungen nur die genannten. `[human-eye]`
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteile zu den Befunden dieses Teils:
- `Ledger/View.fs:4122` Kontowahl: bestätigt. Heute `Money.accountListRow` (Akzentlinie links, farbiger Saldo), im Sheet
  „Konto“ `Money.accountChoiceRow` (Haken, Akzentrahmen, Saldo in Primärtext). Nichts geht verloren: Fokus und Tasten
  hängen an `button` und `aria-current`, beides hat `accountChoiceRow`. Die Zeilen werden 56 px hoch, die Liste im Panel
  wird bei vielen Konten länger. `Money.accountListRow` hat danach außer `/styleguide` keinen Nutzer mehr (Zeile für die
  Kann-Sammlung design-system im RESULT, nicht hier entfernen).
- `Ledger/View.fs:4374` Kopf: bestätigt. Heute `h2` 17 px mit Linie darunter und `Button.Secondary` mit Rand als
  Schließen; das Detail hat ein schlichtes Symbol ohne Rand. Ein reiner Wechsel auf `titleHead` verlöre den Namen
  „Formular schließen“ und die Sperre beim Speichern, daher `formHead`. Die Linie unter dem Kopf entfällt wie im Detail.
- `Ledger/View.fs:4363` „Abbrechen“: bestätigt, eine Funktion für Panel, Sheet und Dialog.
- `Ledger/View.fs:4264` Payee-Platzhalter: bestätigt. Das Beispiel „z. B. Bäckerei“ entfällt; die Beschriftung „Payee“
  bleibt.

Entschieden (Roman, 2026-11-19): jeweils die Empfehlung.
- Kontowahl in „Neue Buchung“ markiert das Konto danach wie das Sheet „Konto“ (Haken und Rahmen statt Linie links).
  Empfehlung: übernehmen.
- Kopf von „Neue Buchung“ im Panel ohne Linie darunter, Schließen ohne Rand wie im Detail. Empfehlung: übernehmen.
- Payee-Platzhalter „optional“ statt „z. B. Bäckerei“. Empfehlung: „optional“ wie beim Memo.

Kann-Sammlung ledger-k4n2s: aus Punkt 26 geht „Payee-Feld sagt nicht, dass es leer bleiben darf“ hier auf. Der
Inline-Kasten der Kontowahl im Panel statt eines DS-Popovers (`View.fs:3514` im Bericht 2026-11-08, heute
`captureAccountList`), `.input-amount-hero` und die übrigen Teile von Punkt 26 bleiben dort.

Überschneidungen: Teil 2 (ledger-gy77x) setzt in §5 die Regel für „Abbrechen“ ohne Füllung und ändert `captureFab`
nebenan, daher erst Teil 2.
