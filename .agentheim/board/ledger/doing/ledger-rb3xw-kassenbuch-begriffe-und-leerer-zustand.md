---
id: ledger-rb3xw
title: "Begriffe und Texte im Kassenbuch: ein Name für den Lena-Filter, leerer Zustand „Keine Buchungen“ nach §7.12 mit „Abrufen“, Listen-Platzhalter ohne „Transaktionen“"
status: doing
type: refactor
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide, ledger-ef068]
blocks: []
tags: [bestandsreview, frontend, design-system]
related_adrs: [0056, 0087, 0093]
related_research: []
prior_art: [ledger-dn4vy]
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G5, Teil 4 von 4 (Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`).
Der Lena-Filter hat drei Namen („Lena gefragt“ im Filter-Sheet, „Lena“ am Chip, „Rückfrage Lena“ in einer toten
Hilfsfunktion), gegen §8 „Konsistente Begriffe“. Der leere Zustand ohne Filter sagt „Für diesen Filter steht noch
nichts im Kassenbuch.“, obwohl kein Filter gesetzt ist, und bietet „Jetzt spiegeln“ statt des Abrufs aus §7.12
(Import direkt ins Kassenbuch, ADR 0093). Der Platzhalter der Liste heißt für Screenreader „Lade Transaktionen“; das
Wort gibt es in BB nicht („Buchung“).

## What
- **Lena-Filter** (`Ledger/View.fs:3470`, `:3219`, `:161`, `Ledger/Types.fs:304`): ein Name nach Romans Wahl (siehe
  von Roman entschieden 2026-11-19: „Lena“) als eine Konstante `askLenaFilterLabel` für Filter-Sheet, PC-Chip und aktiven Chip.
  `askLenaChipLabel` und der Zweig `AskLenaChip` in `chipLabel` (`:3183`) haben keinen Aufrufer außer
  `src/Tests/LedgerLenaFlagClientTests.fs:151`: Zweig auf die Konstante, `askLenaChipLabel` samt Test entfernen.
  Der Name der Marke in der Zeile für Screenreader („Rückfrage Lena“, `:305`) und Toast und Historie („Lena gefragt“,
  `Types.fs:700`) bleiben: sie nennen den Zustand der Buchung, nicht den Filter.
- **Leerer Zustand ohne Filter** (`Ledger/View.fs:2935` bis `:2940`, `EmptyListKind.NoEntries`): Kopf „Keine Buchungen“,
  Satz „Buchungen erscheinen hier nach dem nächsten Import.“, Aktion `Button.secondary "Abrufen"` mit demselben Abruf
  wie im Kopf (`LedgerTabs.Fetch`, bis in die Liste durchreichen). §7.12 wörtlich. Den Kommentar `:2925` bis `:2927`
  („weist auf den fehlenden Spiegel-Lauf hin“) nachziehen.
- **Aktion je leerem Zustand als reine Funktion** (`Ledger/Types.fs` neben `emptyListKind`):
  `emptyListAction : EmptyListKind -> EmptyListAction option` mit `ClearFilters` („Filter löschen“), `Fetch`
  („Abrufen“) und `None` für „Alles geprüft“ und „Alles kategorisiert“; die View rendert nur.
- **Listen-Platzhalter** (`DesignSystem/Loading.fs:306`, `txListSkeleton`): `aria-label` „Wird geladen“ und
  `aria-busy="true"` (wie `firstItemListSkeleton`, `:357`). Neutral, weil der Platzhalter auch Konten, den Monat und
  Budget-Bewegungen vertritt. Den Selektor `[aria-label="Lade Transaktionen"]` in
  `scripts/ledger-background-refresh.test.mjs:155` auf `[aria-busy="true"]` umstellen.
- **README ledger**: Eintrag „Rückfrage Lena (Chip)“ (README Zeile 252, nennt den Chip „Rückfrage Lena (n)“) auf den
  gewählten Namen ziehen; README-Delta vor dem Integrieren durch `npm run check:readme-delta -- --bc ledger --delta …`.

Geänderter DS-Baustein und seine anderen Nutzer: `Loading.txListSkeleton`: Übersicht (`Overview/View.fs:236` Konten,
`:295` Neueste Buchungen, `:587` Monat), Budget (`Budget/InspectorView.fs:92`, `:195`, `:463`,
`Budget/HistoryView.fs:168`), Kassenbuch (`Ledger/View.fs:2894`, `:2898`, `:2986`). Nur der Name ändert sich.

Entscheidungen Conductor 2026-11-19, nach Empfehlung:
- `txListSkeleton` bekommt einen neutralen Namen statt eines Parameters: zehn Aufrufer in drei Kontexten, und ein
  `aria-label` an einem `div` ohne Rolle wird ohnehin kaum vorgelesen; `aria-busy` trägt die Aussage.
- `emptyListAction` als reine Funktion, damit die Aktion je Zustand unter .NET belegt ist (Doktrin Punkt 1); der Flow
  `[flow:pruefliste-abarbeiten]` prüft danach nur noch, dass „Alles geprüft“ erscheint (G7 im Bericht).

Betroffene Dateien: `src/Client/Components/Ledger/View.fs` (161, 2925 bis 2940, 3183, 3219, 3470),
`src/Client/Components/Ledger/Types.fs` (304, `emptyListKind` 338), `src/Client/DesignSystem/Loading.fs`,
`src/Tests/LedgerLenaFlagClientTests.fs`, `src/Tests/LedgerReviewStateTests.fs`,
`scripts/ledger-background-refresh.test.mjs`, `scripts/ledger-review-state.test.mjs`,
`.agentheim/knowledge/contexts/ledger/README.md` (über README-Delta).

## Acceptance criteria
- [ ] Der Lena-Filter heißt im Filter-Sheet, am Chip am PC und am aktiven Chip gleich, aus einer Konstante.
  `askLenaChipLabel` gibt es nicht mehr. Beleg: Diff; `grep -n '"Lena gefragt"' src/Client/Components/Ledger/View.fs`
  findet nichts; der Sweep-Zustand `kassenbuch-filter-sheet` und der Weg über `filter-ask-lena`
  (`scripts/layout-states.mjs:1165`) bleiben grün. Kein Test auf die Konstante (Doktrin Punkt 6).
- [ ] `emptyListAction`: kaputt-Szenario GIVEN kein Filter, kein Chip (`NoEntries`) WHEN `emptyListAction` THEN `Fetch`
  (heute „Jetzt spiegeln“, vor dem Fix rot). Intakt: `NoMatch` gibt `ClearFilters`, `AllReviewed` und `AllCategorized`
  geben keine Aktion. Beleg: Expecto über alle Fälle von `EmptyListKind` in `src/Tests/LedgerReviewStateTests.fs`.
- [ ] Der leere Zustand ohne Filter zeigt „Keine Buchungen“, „Buchungen erscheinen hier nach dem nächsten Import.“ und
  „Abrufen“; „Abrufen“ startet denselben Abruf wie im Kopf. Beleg: Diff für die Texte; die Verdrahtung über
  `LedgerTabs.Fetch` ist dieselbe Funktion wie im Kopf.
- [ ] Der Flow `[flow:pruefliste-abarbeiten]` (`scripts/ledger-review-state.test.mjs:74`) prüft nur noch, dass „Alles
  geprüft“ erscheint; die Prüfung „kein Jetzt spiegeln“ (`:87`) entfällt, sie deckt `emptyListAction` (Doktrin Punkt 2).
- [ ] `txListSkeleton` trägt `aria-label="Wird geladen"` und `aria-busy="true"`; `scripts/ledger-background-refresh.test.mjs`
  ist nachgezogen und grün.
- [ ] README-Delta „Rückfrage Lena (Chip)“ durch `npm run check:readme-delta` mit Exit 0, jeder Treffer der Prüfung 7
  nachgezogen oder mit Grund als `--keep` im OUTCOME.
- [ ] Romans Blick: am Handy (412 px, Touch) Filter-Sheet mit dem Lena-Schalter, aktiver Chip, ein leeres Konto mit
  „Keine Buchungen“ und „Abrufen“ antippen; am PC 1440 px Filterleiste mit Chip und derselbe leere Zustand. Vorher und
  nachher über `npm run screenshot:route`; Abweichungen nur die genannten. `[human-eye]`
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteile zu den Befunden dieses Teils:
- `Ledger/View.fs:3470` Lena-Filter: bestätigt. Sichtbar sind heute „Lena gefragt“ (Filter-Sheet) und „Lena“ (PC-Chip
  `:3219`, aktiver Chip `Types.fs:304`). „Rückfrage Lena (n)“ aus `askLenaChipLabel` (`:161`) ist nicht mehr sichtbar:
  `chipLabel` wird nur für Zeitraum und Betrag aufgerufen (`:3223`) und im Test mit `CategoryChip`.
- `Ledger/View.fs:2940` leerer Zustand: bestätigt, schlimmer als beschrieben. Der Fall `NoEntries` gilt nur ohne jeden
  Filter (`Types.fs:338`), der Satz „Für diesen Filter …“ stimmt dort nie. Wegfall: „Jetzt spiegeln“ steht dann nicht
  mehr im leeren Zustand, bleibt aber im Kopf.
- `DesignSystem/Loading.fs:306` „Lade Transaktionen“: angepasst. Der vorgeschlagene Name „Buchungen werden geladen“
  wäre falsch für Konten (`Overview/View.fs:236`) und Monat (`:587`), die denselben Platzhalter nutzen.

Entschieden (Roman, 2026-11-19): jeweils die Empfehlung.
- **Name des Lena-Filters.** (A) „Lena“ überall sichtbar: wie die Marke „Lena“ in der Zeile und der Chip am PC nach
  Prototyp Version 17; nur das Filter-Sheet ändert sich. (B) „Rückfrage Lena“ überall, der Begriff aus der README; die
  Chips am PC und am Handy werden länger, bei 1024 px neben dem Panel wird die Filterleiste enger. Empfehlung: (A).
- **Leerer Zustand ohne Filter.** (A) §7.12 wörtlich: „Keine Buchungen“, „Buchungen erscheinen hier nach dem nächsten
  Import.“, Aktion „Abrufen“. (B) Satz wie (A), Aktion bleibt „Jetzt spiegeln“: holt auch Buchungen von Konten ohne
  Comdirect aus YNAB, passt aber nicht zum Satz. Empfehlung: (A). Für ein Konto ohne Comdirect (etwa Bar) passt der
  Satz weniger; das ist selten leer.

Kann-Sammlung ledger-k4n2s: Punkt 135 („prüfen, ob `chipLabel` für Zu prüfen und Lena, `needsReviewChipLabel`,
`askLenaChipLabel` … noch Nutzer haben“) geht für `askLenaChipLabel` hier auf; `needsReviewChipLabel` und
`ChipBar.clearAllButton` bleiben dort.

Überschneidungen: Teil 1 (ledger-ef068) ändert im selben `match` „Alles geprüft“ und „Alles kategorisiert“ und im
Filter-Sheet eine Klasse drei Zeilen tiefer, daher erst Teil 1.

Bereit für todo erst nach Romans Wahl zu beiden Fragen.
