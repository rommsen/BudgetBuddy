---
id: ledger-vf6z5
title: "„Neue Buchung“ nach Styleguide: Betrags- und Kontofehler am Feld mit Fokus, direkte Fehlertexte ohne „Bitte“, Kontoliste mit Spinner und ehrlichem Leer- und Fehlerzustand"
status: todo
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [bestandsreview, frontend, tests]
related_adrs: [0005, 0087]
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-12, Gruppe G5, Teil 2 von 3 (Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`).
„Neue Buchung“ sammelt alle Fehler am Formularende und markiert das Betragsfeld nie; das Sheet „Betrag“ zeigt seit
ledger-q42pg, wie es geht. Die Fehlertexte beginnen mit „Bitte“ (§8: keine Höflichkeitsfloskeln), und die Kontoliste
sagt „Konten werden geladen“, auch wenn nichts mehr lädt.

## What
- Betragsfehler (unlesbar, 0) am Betragsfeld: `ErrorDisplay.inline'` mit Id darunter, `AriaInvalid`, `DescribedBy`,
  `amountFieldClass true invalid`; abgelehntes Absenden legt den Fokus ins Feld und rollt den Fehler ins Bild
  (`Viewport.focusAndReveal`, Vorbild `RevealAmountFieldError`, `State.fs:2578`) (`Ledger/View.fs:3794`,
  `Ledger/State.fs:1108`).
- Kontofehler unter dem Feld „Konto“ als `ErrorDisplay.inline'`.
- Fehler des Servers bleiben gesammelt über „Buchung anlegen“. Bewusste Abweichung von
  `.claude/rules/task-ui-vorgaben.md`, Grund: `validateLedgerBooking` liefert Texte ohne Feldbezug, eine Zuordnung ans
  Feld bräuchte strukturierte Fehler (Zeile für die Kann-Sammlung ledger).
- Fehlertexte nach §8 (Entscheidung Conductor 2026-11-12, nach Empfehlung):
  - `Capture.amountInvalidText` „Betrag nicht lesbar, etwa 12,50“ (statt „Bitte einen gültigen Betrag eingeben“)
  - `Capture.accountMissingText` „Konto fehlt“ (statt „Bitte ein Konto wählen“)
  - `Types.amountMissingText` (Sheet „Betrag“) „Betrag fehlt oder ist 0“ (statt „Bitte einen Betrag größer als 0
    eingeben“)
  - `Capture.amountZeroText` „Der Betrag muss größer als 0 sein“ bleibt.
- Kontoliste: beim Laden `Loading.spinner` mit Text, nach einem Ladefehler ein Fehlertext, ohne wählbares Konto
  „Kein Konto zum Buchen“ statt „Konten werden geladen“ (`Ledger/View.fs:3738`, `:3746`).

Betroffene Dateien: `src/Client/Components/Ledger/Capture.fs`, `src/Client/Components/Ledger/State.fs` (`submitCapture`
um 1108, Antwort-Fehler um 3066), `src/Client/Components/Ledger/View.fs` (`captureAccountList`, `captureFields`, 3738 bis
3940), `src/Client/Components/Ledger/Types.fs` (`amountMissingText`), `src/Tests/LedgerCaptureClientTests.fs`,
`src/Tests/LedgerAmountClientTests.fs`, `scripts/layout-states.mjs`.

## Acceptance criteria
- [ ] `CaptureState` trägt die Fehler getrennt (etwa `AmountError: string option`, `AccountError: string option`,
  `ServerErrors: string list`) statt `Errors: string list`; `Capture.captureRequest` liefert sie getrennt. Beleg: Expecto
  in `src/Tests/LedgerCaptureClientTests.fs`, die bestehenden Fälle `:197` und `:426` umgeschrieben.
- [ ] Absenden mit unlesbarem Betrag schickt keinen Aufruf und gibt als einzige Nachricht `RevealCaptureAmountError`
  (Name frei) zurück; Absenden mit gültigem Betrag ohne Konto gibt keinen Fokus-Effekt. Beleg: Expecto über das echte
  `update`, Vorbild `LedgerAmountClientTests.fs:234`. Vor dem Fix rot.
- [ ] Tippen im Betragsfeld nimmt den Betragsfehler zurück, die Wahl eines Kontos den Kontofehler. Beleg: Expecto über
  `update`.
- [ ] Am Handy: nach abgelehntem Absenden hat das Betragsfeld den Fokus, `aria-invalid="true"` und `aria-describedby`
  auf die Meldung, und die Meldung ist im Bild, auch wenn die Tastatur danach aufgeht. Beleg: neuer Zustand
  `kassenbuch-erfassen-feldfehler` in `scripts/layout-states.mjs` (Handy-Breiten), Muster `kassenbuch-betrag-feldfehler`,
  Kommentar nennt den Bug; vor dem Fix bricht der Zustand.
- [ ] Die Texte lauten wie unter What, auch im Sheet „Betrag“. Beleg: die bestehenden Tests vergleichen gegen die
  Konstanten; kein eigener Konstantentest (Doktrin Punkt 6).
- [ ] Die Kontoliste zeigt beim Laden einen Spinner mit Text, nach einem Ladefehler einen Fehlertext und ohne wählbares
  Konto „Kein Konto zum Buchen“. Die Wahl zwischen den drei Fällen ist eine reine Funktion. Beleg: Expecto auf die reine
  Funktion, je Fall ein Test.
- [ ] „Neue Buchung“ neben dem heutigen Stand verglichen (Handy 412 px und PC-Panel, `npm run screenshot:route`),
  Abweichungen nur die genannten. `[human-eye]`
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteile zu den Befunden dieses Teils:
- `Ledger/View.fs:3794` Betragsfeld nie ungültig: gilt. `Input.amountFieldClass true false` fest, kein `AriaInvalid`, kein
  `DescribedBy`; alle Fehler gesammelt in `capture-errors` (`View.fs:3932`).
- `Ledger/Capture.fs:51` „Bitte …“: gilt, ebenso `accountMissingText` (`Capture.fs:53`) und `amountMissingText`
  (`Types.fs:745`).
- `Ledger/View.fs:3738` Kontoliste: gilt teilweise. Spinner fehlt; dazu steht „Konten werden geladen“ bei jeder leeren
  Liste (`View.fs:3746`), auch nach einem Ladefehler oder ohne wählbares Konto. Die Tastenführung (§7.9: Suche, ↑↓,
  Enter, Fokus in der Suche am PC) gehört nicht hierher, sondern zu ledger-ea70s (Ein Erfassen), weil dort die Auswahlen
  aus dem Formular neu gebaut werden. ledger-ea70s wird hier nicht geändert; der Conductor vermerkt es beim Verfeinern
  von ea70s.

Kann-Sammlung ledger-k4n2s: Punkt 26 (Fehler am Feld statt gesammelt, „Bitte“-Texte in `Capture.fs`) geht in diese Task
auf. Die übrigen Teile von Punkt 26 (Inline-Kasten statt Popover, Payee-Hinweis, `.input-amount-hero`, Skeleton, Regel
„Detail öffnet, Formular schließt“) bleiben dort.

Hinweise für den Worker:
- `Input.pickerField` (Konto) ist ein Knopf; `aria-invalid` gilt für Knöpfe nicht. Die Meldung darunter mit
  `role="alert"` (macht `ErrorDisplay.inline'` schon) reicht. `pickerField` hier nicht ändern.
- Beim Absenden wartet das Formular auf die Kategorie zum Payee (`SubmitWhenLookedUp`, `State.fs:3030`). Die Feldprüfung
  muss vor dem Warten laufen, sonst kommt der Fehler erst nach der Antwort.
- Überschneidung mit ledger-ea70s im selben Formular: diese Task ist klein und kann vorher laufen. Ein 409 bei der
  Barbuchung aus „Neue Buchung“ (Entscheidung Roman 2026-11-12: als angekommen melden wie „Erneut senden“) gehört
  nicht hierher, sondern zu ledger-xqqnv (Schreibwege einheitlich); diese Task ändert nur die Feldfehler vor dem Aufruf.
- Teil 1 (ledger-ng110, Split-Editor) und Teil 3 (ledger-dn4vy, Filter und Liste) arbeiten in `View.fs` und `State.fs`
  in anderen Funktionen.
