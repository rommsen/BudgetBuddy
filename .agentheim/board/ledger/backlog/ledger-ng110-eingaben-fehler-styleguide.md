---
id: ledger-ng110
title: "Eingaben und Fehler im Kassenbuch nach Styleguide: Split-Rest gelb, Fehler am Feld, Zifferntastatur und Namen im Filter, Kontrast, Barrierefreiheit"
status: backlog
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: []
blocks: []
tags: [bestandsreview, captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger vom 2026-11-12, Gruppe G5. Roman hat die Gruppe als Task gewählt.

## What
Alle Befunde der Gruppe in Befundform, wörtlich aus dem Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`:

### G5 Eingaben und Fehler im Kassenbuch nach Styleguide (hoch, 17 Befunde, ledger)
Formulare des Kassenbuchs (Erfassen, Split, Filter) zeigen Fehler und Zustände anders als der Styleguide und die neueren Detail-Felder.
- src/Client/DesignSystem/SplitEditor.fs:19 | styleguide.md §2 Farbsemantik | hoch | Rest ungleich 0 im Split-Editor in Gelb (`Colors.attention`) statt Rot; Rot nur für Fehler und Minus.
- src/Client/Components/Ledger/View.fs:3794 | styleguide.md §5 ErrorDisplay, Viewport `focusAndReveal` | mittel | „Neue Buchung“: Betragsfeld nie ungültig markiert, Fehler gesammelt am Formularende; Fehler ans Feld wie im Sheet „Betrag“.
- src/Client/Components/Ledger/State.fs:3301 | styleguide.md §5 Toast, §7.13 | mittel | „Aufteilung nicht gespeichert“ und „Betrag nicht geändert“ (3295) nur als Toast bei offenem Editor; zusätzlich `ErrorDisplay.inline'` im Editor.
- src/Client/Components/Ledger/View.fs:3217 | CLAUDE.md Ablauf; styleguide.md §5 Input | mittel | Betragsfelder „Von“/„Bis“ im Filter ohne `inputmode="decimal"`, Mono-Klasse und zugänglichen Namen.
- src/Client/DesignSystem/SplitEditor.fs:124 | styleguide.md §5 Input, Drift | mittel | Betrag und Memo einer Split-Zeile als rohe `Html.input` ohne Fehlerzustand; über `Input.text` mit `amountFieldClass`.
- src/Client/DesignSystem/SplitEditor.fs:145 | styleguide.md §5 `removeIcon`, Zugänglichkeit | mittel | „Zeile entfernen“ nur mit `title`; `Button.removeIcon`.
- src/Client/Components/Ledger/View.fs:2624 | styleguide.md §2 Kontrast | mittel | Ausgegraute Zeile mit `opacity-50` auf dem Text unterschreitet 4,5:1.
- src/Client/Components/Ledger/View.fs:3085 | styleguide.md §5 Segmented | mittel | Schnellwahl im Filter Zeitraum als Nachbau ohne `aria-pressed`.
- src/Client/Components/Ledger/View.fs:3181 | styleguide.md §7.9, §7.15 | mittel | Payee-Vorschläge im Filter: rohe Knöpfe (~40 px), ohne ↑↓/Enter; über `PickerParts`/`PayeePicker` (design und frontend).
- src/Client/Components/Ledger/View.fs:4659 | styleguide.md §7.15 | mittel | Grund der Sperre von „Lena fragen“ auf der Karte nur im `title`; wie im Detail als Text.
- src/Client/DesignSystem/SplitEditor.fs:95 | styleguide.md §5 `pickerField` | niedrig | Ziel-Feld der Split-Zeile auf `Input.pickerField`.
- src/Client/Components/Ledger/View.fs:572 | styleguide.md §5 Button `IsBusy` | niedrig | Prüf-Schaltfläche der Zeile setzt `disabled`, Fokus fällt auf `body`.
- src/Client/Components/Ledger/View.fs:4346 | Zugänglichkeit Icon-Buttons | niedrig | „Jetzt spiegeln“ am Handy ohne `aria-label`.
- src/Client/Components/Ledger/State.fs:1816 | styleguide.md §8 | niedrig | Unlesbarer Betrag im Filter wird still leer; Fehlerzustand wie beim Datum.
- src/Client/Components/Ledger/Capture.fs:51 | styleguide.md §8 Voice | niedrig | „Bitte einen gültigen Betrag eingeben“ direkt formulieren.
- src/Client/Components/Ledger/View.fs:3738 | styleguide.md §2, §7.9 | niedrig | Kontoliste beim Erfassen ohne Tastenführung, Ladehinweis ohne Spinner.
- src/Client/Components/Ledger/View.fs:4251 | Schreibstil (kein Gedankenstrich) | niedrig | „Spiegel läuft — …“ ohne Gedankenstrich.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-12, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn Roman das will.
