---
id: ledger-dn4vy
title: "Filter und Liste nach Styleguide: Betrag Von/Bis mit Zifferntastatur, Name und Fehlerzustand, Schnellwahl als Segmented, Payee-Vorschläge mit Tastatur, lesbare ausgegraute Zeile, Fokus am Prüfknopf, Lena-Grund als Text"
status: done
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: [design-system-001-styleguide, ledger-ct3cx, ledger-smv1s]
blocks: []
tags: [bestandsreview, frontend, tests]
related_adrs: [0049, 0055, 0087]
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-12, Gruppe G5, Teil 3 von 3 (Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`).
Im Filter fehlen am Betrag Zifferntastatur, Name und Fehlerzustand, und ein unlesbarer Betrag entfernt still einen
aktiven Filter. Schnellwahl und Payee-Vorschläge sind Nachbauten ohne Tastatur und mit zu kleiner Trefferfläche. Die
ausgegraute Zeile unterschreitet 4,5:1 (§2 Kontrast), und der Prüfknopf wirft den Fokus auf `body`.

## What
- Betrag Von/Bis: `InputMode "decimal"`, `amountFieldClass false invalid`, Namen „Betrag von“ und „Betrag bis“,
  Fehlerzustand bei unlesbarer Eingabe wie beim Datum; ein unlesbarer Betrag lässt den Filter stehen
  (`Ledger/View.fs:3217`, `:3097`, `Ledger/State.fs:1816`, `Types.fs:118`).
- Schnellwahl Zeitraum als `Segmented` mit `aria-pressed`, in zwei Reihen (neue Variante mit Spaltenzahl in
  `DesignSystem/Segmented.fs`). Entscheidung Conductor 2026-11-12, nach Empfehlung (`Ledger/View.fs:3085`).
- Payee-Vorschläge im Filter über `PayeePicker` (oder seine Teile `options`, `keyAction`, Zeilenklasse), ohne die Zeile
  „„Eingabe“ übernehmen“ (`Ledger/View.fs:3181`).
- Ausgegraute Zeile („passt nicht mehr zum Filter“): alle Texte der Zeile und des Vermerks in `text-muted` bei voller
  Deckkraft, kein `opacity-50` mehr (`Ledger/View.fs:982`, `:2624`). Entscheidung Roman 2026-11-12.
- Prüfknopf der Zeile mit `aria-disabled` und `aria-busy` statt `disabled` (`Ledger/View.fs:572`).
- Grund der Sperre von „Lena fragen“ auf der Karte beim Durchgehen als Text unter den Knöpfen (`Ledger/View.fs:4659`).
- „Jetzt spiegeln“ am Handy mit `AriaLabel` (`Ledger/View.fs:4346`).
- „Spiegel läuft. Die Liste bleibt so lange stehen.“ ohne Gedankenstrich (`Ledger/View.fs:4251`).

Betroffene Dateien: `src/Client/Components/Ledger/View.fs` (`reviewMarkButton` 558, PC-Zeile 982, Handy-Zeile 2624,
Filter-Popover 3085 bis 3230, Kopf 4251 und 4346, Karte 4659), `src/Client/Components/Ledger/State.fs` (Betragsfilter
1816 bis 1835), `src/Client/Components/Ledger/Types.fs` (`parseAmountFilterText`), `src/Client/DesignSystem/Segmented.fs`,
`src/Client/DesignSystem/PayeePicker.fs`, `scripts/layout-invariants.mjs`, `scripts/layout-states.mjs`,
`scripts/ledger-review-mark.test.mjs`, `scripts/ledger-filter-bar.test.mjs`, Tests unter `src/Tests/`,
`standards/frontend/styleguide.md` (§5 Zeile „Segmented“).

## Acceptance criteria
- [ ] Betragsfilter, kaputt-Szenario: GIVEN aktiver Filter „ab 5 €“ WHEN Von = „abc“ und die Verzögerung läuft ab THEN
  `Filters.AmountMin` bleibt 5 und das Feld gilt als ungültig. Intakt: GIVEN aktiver Filter WHEN Von = „“ THEN der Filter
  fällt weg; WHEN Von = „7,5“ THEN `AmountMin = 7,5`. Beleg: Expecto über das echte `update` mit Ablauf der Verzögerung
  (Vorbild `src/Tests/CmdTiming.fs`); kaputt-Test vor dem Fix rot. Die Aufzählung der Reihenfolgen (Setzen, Ablauf,
  Entfernen) bringt ledger-ct3cx; die neuen Fälle hier in diese Aufzählung einbauen.
- [ ] `parseAmountFilterText` unterscheidet leer, lesbar und unlesbar (etwa `Result` oder eigene DU); der Doc-Kommentar
  `Types.fs:114` stimmt danach. Beleg: Expecto pur.
- [ ] Am Handy öffnet das Betragsfeld des Filters die Zifferntastatur (`inputmode="decimal"`), hat 16 px Schrift und
  48 px Höhe und heißt „Betrag von“ bzw. „Betrag bis“. Beleg: neuer Zustand `kassenbuch-filter-betrag`
  (Handy-Breiten), der die Felder per `getByRole("textbox", { name: "Betrag von" })` füllt (vor dem Fix findet er sie
  nicht, der Zustand bricht) und `inputmode` prüft; Invarianten 2 und 3.
- [ ] Payee-Vorschläge im Filter haben am Handy 48 px je Zeile. Beleg: neuer Zustand `kassenbuch-filter-payee`
  (Handy-Breiten), Invariante 2; vor dem Fix rot (heute etwa 40 px).
- [ ] PC: beim Öffnen des Payee-Filters steht der Fokus in der Suche, ↓ markiert die erste Zeile, Enter filtert nach ihr,
  Esc schließt nur das Popover. Beleg: die Tastenlogik ist über `PayeePicker.keyAction` in
  `src/Tests/LedgerPayeeClientTests.fs:111` belegt; dazu ein `[tastatur]`-Test in `scripts/ledger-filter-bar.test.mjs`
  „PC: im Payee-Filter wählt ↓ und Enter den ersten Vorschlag“, weil die Verdrahtung im Filter-Popover neu ist (eigene
  Aussage nach Doktrin Punkt 2).
- [ ] Die Schnellwahl steht in zwei Reihen, trägt `aria-pressed` am gewählten Knopf und passt bei 320 px ohne
  Abschneiden. Beleg: Invarianten 2 und 4 im Zustand `kassenbuch-filter-zeitraum`; der `[fable]`-Test
  `scripts/ledger-filter-bar.test.mjs:105` findet „Dieser Monat“ weiter per Rolle.
- [ ] Ausgegraute Zeile: jeder Text der Zeile und des Vermerks hat mindestens 4,5:1 gegen seinen tatsächlichen
  Hintergrund. Beleg: neue Invariante 10 in `scripts/layout-invariants.mjs` „sichtbarer Text hat mindestens 4,5:1“,
  gerechnet aus `getComputedStyle` (Farbe samt Alpha, Produkt der `opacity` aller Vorfahren, erster nicht transparenter
  Hintergrund), ausgenommen gesperrte Bedienelemente (`disabled`, `aria-disabled`, WCAG 1.4.3) und Platzhalter;
  Gegenbeweis mit absichtlich kaputter Seite (Text mit `opacity: .5`). Dazu neue Zustände `kassenbuch-ausgegraut`
  (PC 1280) und `kassenbuch-ausgegraut-handy` (412): Kategorie einer Zeile unter Kategorie-Filter ändern, bis „passt
  nicht mehr zum Filter“ steht (Vorlage `scripts/ledger-category-change.test.mjs`). Vor dem Fix rot. Den Kopfkommentar
  „Farben prüft der Sweep nicht“ (`layout-invariants.mjs:25`) anpassen.
- [ ] Treffer der Invariante 10 außerhalb des Kassenbuchs stehen je Zustand, Breite und Selektor als `exceptions` im
  Zustand und sind im RESULT als Zeile für die Kann-Sammlung des betroffenen BC gemeldet (Doktrin Punkt 5), nicht hier
  behoben.
- [ ] Prüfknopf: während der Aufruf läuft, behält der Knopf den Fokus und trägt `aria-disabled="true"` und
  `aria-busy="true"`. Beleg: `[fokus]`-Test in `scripts/ledger-review-mark.test.mjs` „PC: der Fokus bleibt am Prüfknopf,
  solange der Aufruf läuft“ mit verzögerter Mock-Antwort, vor dem Fix rot (Fokus auf `body`). Dass ein zweiter Klick
  nichts schickt, belegt ledger-smv1s in `update`.
- [ ] Karte beim Durchgehen: ist „Lena fragen“ gesperrt, steht der Grund als Text unter den Knöpfen (gleicher Baustein
  wie im Detail, `Detail.fieldLockHint` oder `ReviewCard.meta`). Der Text kommt aus `lenaActionFor` (schon getestet).
  `[human-eye]` mit Screenshot der Karte bei fehlender Flag-Farbe.
- [ ] „Jetzt spiegeln“ am Handy hat `AriaLabel = Some "Jetzt spiegeln"`. Kein eigener Test (Doktrin Punkt 6); Beleg ist
  der Diff.
- [ ] Der Hinweis lautet „Spiegel läuft. Die Liste bleibt so lange stehen.“; `scripts/layout-states.mjs:1246` und
  `:1257` sind im selben Schritt nachgezogen.
- [ ] Filter am Handy und PC, ausgegraute Zeile und Karte neben dem heutigen Stand verglichen (`npm run screenshot:route`),
  Abweichungen nur die genannten. `[human-eye]`
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteile zu den Befunden dieses Teils:
- `Ledger/View.fs:3217` Betrag Von/Bis: gilt teilweise. Kein `InputMode`, keine Mono-Klasse; der Name fehlt, weil
  `fieldLabel` (`View.fs:3097`) ein `Html.label` ohne `htmlFor` neben das Feld setzt. Das Datum benennt sich über
  `DateField.Label` und ist nicht betroffen.
- `Ledger/State.fs:1816` unlesbarer Betrag: gilt, schlimmer als beschrieben. `parseAmountFilterText` liefert für „abc“
  `None`, `CommitAmountFilter` setzt dann `AmountMin = None` und entfernt einen aktiven Filter, gegen den Doc-Kommentar
  `Types.fs:114`.
- `Ledger/View.fs:3085` Schnellwahl: gilt teilweise. Kein `aria-pressed`; `Segmented.view` ist aber einreihig, vier Texte
  passen bei 320 px nicht sicher, daher die zweireihige Variante.
- `Ledger/View.fs:3181` Payee-Vorschläge: gilt. `py-2.5` ohne Mindesthöhe, keine Tasten; der Sweep hatte keinen Zustand
  mit offenem Payee-Filter.
- `Ledger/View.fs:2624` und `:982` ausgegraute Zeile: gilt. Gegen `bg-card` bei 50 %: `text-secondary` 2,77:1,
  `text-muted` 2,23:1, Gelb 3,11:1. Keine Deckkraft rettet `text-muted` (90 %: 4,01:1 auf `bg-hover`), daher volle
  Deckkraft in `text-muted` (5,32:1 auf `bg-card`).
- `Ledger/View.fs:572` Prüfknopf: gilt. Ohne Sperre in `update` schickte ein zweiter Klick einen zweiten Aufruf, daher
  depends_on ledger-smv1s.
- `Ledger/View.fs:4659` Lena-Grund nur im `title`: gilt; im Detail steht er als Text (`View.fs:1814`).
- `Ledger/View.fs:4346` „Jetzt spiegeln“: gilt teilweise. Der Name kommt über `title`, `AriaLabel` fehlt gegen die
  Button-Doku; am Handy zeigt `title` nichts.
- `Ledger/View.fs:4251` Gedankenstrich: gilt.

Kann-Sammlung ledger-k4n2s: aus Punkt 1 gehen die nachgebaute Zeitraum-Schnellwahl und die Payee-Vorschläge in diese
Task auf; die übrigen Teile von Punkt 1 (Trefferflächen im Detail-Kopf, Chip-„×“, Split-Sheet, Konto-Wechsler,
Settings-Selects, Orange) bleiben dort.

Überschneidungen:
- ledger-ct3cx (Kassenbuch behält Stand) bindet `RemoveAmountFilter` und `CommitAmountFilter` an Versionen, dieselben
  Zweige wie der Betragsfilter hier. Erst ct3cx.
- ledger-smv1s (Doppelaufruf sperren) bringt die Sperre von `StartSetReviewMark` in `update`. Erst smv1s.
- Teil 1 (ledger-ng110, Split-Editor) und Teil 2 (ledger-vf6z5, „Neue Buchung“) arbeiten in anderen Funktionen.

Hinweise für den Worker:
- `PayeePicker.options` bietet immer „„Eingabe“ übernehmen“ an. Im Filter ist die Zeile falsch (ein getippter Name ohne
  Buchung liefert null Treffer). Eine Option am Baustein (etwa `AllowTyped: bool`) oder `keyAction` und Zeilenklasse in
  einer schlanken Filter-Liste wiederverwenden; `keyAction` arbeitet auch ohne `TakeTyped`-Zeile.
- Die Segmented-Variante in §5 nennen; „höchstens vier Optionen“ bleibt erfüllt.
- „Jetzt spiegeln“ setzt während des Laufs `IsLoading` und damit `disabled` (Fokusverlust wie beim Prüfknopf). Gering;
  als Zeile für die Kann-Sammlung ledger im RESULT melden.
- Der Fehlerzustand des Filter-Betrags gilt sofort beim Tippen (rein aus dem Text), nicht erst nach Ablauf der
  Verzögerung.

## Outcome

Filter und Liste des Kassenbuchs folgen jetzt dem Styleguide. `./verify.sh`: VERIFY: PASS (dotnet 4833 grün, 15 erlaubte übersprungen; node 322 grün).

- **Betragsfilter** (`Ledger/Types.fs`, `Ledger/State.fs`): `parseAmountFilterText` liefert `AmountFilterText` (leer, lesbar, unlesbar). `amountFilterValue` lässt bei unlesbarem Text die bisherige Grenze stehen, `SetAmountMinDraft`/`SetAmountMaxDraft` schicken das an `CommitAmountFilter`. Der Doc-Kommentar stimmt damit wieder. Belege: `LedgerPageTests.fs` „Über alle Reihenfolgen aus Tippen, Ablauf und Entfernen …“. Der Test geht für „abc“, „“ und „7,5“ durch das echte `update` und lässt die echte Verzögerung über `CmdTiming` ablaufen, mit und ohne Entfernen. Vor dem Fix war er rot (erwartet Some 5, war None). Dazu „Ein unlesbarer Bis-Betrag lässt beide Grenzen stehen …“ und die reinen Tests in `LedgerViewTests.fs`.
- **Betragsfeld** (`Ledger/View.fs`, `amountField`): `InputMode "decimal"`, `Input.Medium` (48 px und 16 px am Handy), `amountFieldClass false invalid`, `AriaLabel` „Betrag von“/„Betrag bis“, `aria-invalid` und `ErrorDisplay.inline'` „Betrag als Zahl, z. B. 12,50“, sobald getippt wird. Beleg: neuer Sweep-Zustand `kassenbuch-filter-betrag` (320, 390). Er füllt die Felder per `getByRole("textbox", { name: "Betrag von" })` und prüft `inputmode`. Vor dem Fix brach er (`locator.fill` Timeout).
- **Schnellwahl** als `Segmented.viewColumns … TwoColumns` (neue Variante in `DesignSystem/Segmented.fs`, Styleguide §5 nachgezogen). Belege: Zustand `kassenbuch-filter-zeitraum` grün; der `[fable]`-Test findet „Dieser Monat“ weiter per Rolle.
- **Payee-Filter** (`PayeeFilterList` in `Ledger/View.fs`): nutzt `PayeePicker.suggestedOptions` (neu, ohne „übernehmen“), `PayeePicker.rowClassFor` (jetzt öffentlich) und `payeeFilterKeyAction`. Am PC Autofokus in der Suche, Combobox mit `aria-activedescendant`. Neu ist die Grenze von 50 Vorschlägen (`PayeePicker.maxShown`) und die Sortierung nach Häufigkeit. Belege: `LedgerPayeeClientTests.fs` (zwei Fälle „Payee-Filter: …“), `[tastatur]` „PC: im Payee-Filter wählt ↓ und Enter den ersten Vorschlag“ in `scripts/ledger-filter-bar.test.mjs` (mit Esc ohne Filter), Zustand `kassenbuch-filter-payee` (320, 390). Vor dem Fix war die Invariante 2 rot: 42,5 px je Zeile.
- **Ausgegraute Zeile**: `.filtered-out-text` in `styles.css` (alle Texte `text-muted`, Symbole ausgenommen) statt `opacity-50`, am PC und am Handy. Neue **Invariante 10** in `scripts/layout-invariants.mjs`: sichtbarer Text hat mindestens 4,5:1. Gerechnet wird mit Farbe samt Alpha (normiert über eine Leinwand, auch oklab), dem Produkt der `opacity` aller Vorfahren und den Hintergründen bis zum ersten deckenden. Ausgenommen sind `disabled`/`aria-disabled`, Platzhalter, sr-only sowie Text vor Verlauf oder Bild. Der Kopfkommentar ist angepasst. Neue Zustände `kassenbuch-ausgegraut` (1280) und `kassenbuch-ausgegraut-handy` (412): Kategorie-Filter Lebensmittel, dann Bauhaus auf Nebenkosten. Vor dem Fix rot, mit der alten `opacity-50` lokal gebaut: Bauhaus 4,33:1, Kategorie 2,67:1, „zu prüfen“ 2,98:1, Datum 2,16:1, Vermerk am Handy 2,23:1. Gegenbeweise in `layout-invariants.test.mjs`: „Text mit halber Deckkraft meldet den Kontrast“ und „ein gesperrtes Element zählt nicht, ein freies mit derselben Farbe schon“.
- **Treffer der Invariante 10 außerhalb des Kassenbuchs** stehen als `exceptions` (inv 10) in `layout-states.mjs`: `budget-historie` 768 bis 1920, `budget-target-anlegen` 1440, `regeln-dialog-picker` 1440, `uebersicht-zu-tun` 900 und 1280. Einen Treffer im Kassenbuch selbst habe ich behoben statt ihn auszunehmen: die Kontokarten am Handy, deren Label in `DesignSystem/Stats.fs` in `text-muted/70` stand (3,23:1, in rund 15 Kassenbuch-Zuständen). Das Label steht jetzt in `Colors.textMuted`. Davon ist auch `/styleguide` betroffen.
- **Prüfknopf**: `aria-disabled` und `aria-busy` statt `disabled`, Klassen `aria-disabled:`. Beleg: `[fokus]` „PC: der Fokus bleibt am Prüfknopf, solange der Aufruf läuft“ in `scripts/ledger-review-mark.test.mjs`, mit zurückgehaltener Mock-Antwort. Vor dem Fix rot: Fokus auf BODY.
- **Karte beim Durchgehen**: Ist „Lena fragen“ gesperrt, steht `ReviewCard.meta reason` unter den Knöpfen. Der Text kommt aus `lenaActionFor`. Der Lena-Fehlerzweig (ledger-nc708) ist nicht angefasst.
- **„Jetzt spiegeln“ am Handy**: `AriaLabel = Some "Jetzt spiegeln"`. **Hinweis**: „Spiegel läuft. Die Liste bleibt so lange stehen.“, nachgezogen in `layout-states.mjs` (`kassenbuch-spiegel-laeuft`, `kassenbuch-nach-spiegel`).
- **`[human-eye]` Vergleich vorher/nachher**: Die Vorher-Bilder sind mit den HEAD-Ständen von View/Segmented/PayeePicker/styles/Stats lokal gebaut, danach habe ich die Dateien wiederhergestellt. Die Bilder liegen unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/ledger-dn4vy/`, je `vorher-` und `nachher-` für `zeitraum-390/1280`, `payee-390/1280`, `betrag-390/1280`, `ausgegraut-1280/412` und `karte-ohne-flagfarbe-412/1280` (.png). Abweichungen sind nur die genannten. Dazu kommen der Fokusrahmen in der Payee-Suche am PC und die eingerückten Payee-Zeilen der Payee-Auswahl.

Für die Kann-Sammlung:
- ledger: „Jetzt spiegeln“ setzt während des Laufs `IsLoading` und damit `disabled`, der Fokus geht verloren wie früher am Prüfknopf.
- ledger: Am Handy mit aktivem Filter sind „Alle Filter löschen“ (44 px) und das Chip-„×“ (24 px) zu klein, Ausnahme `kassenbuch-ausgegraut-handy` @ 412, inv 2 (Chip-„×“ steht schon in ledger-k4n2s Punkt 1).
- ledger: Übersicht, Leertext „Buchungen erscheinen hier nach dem nächsten …“ in `text-muted/70` (3,23:1), Ausnahme `uebersicht-zu-tun` @ 900/1280, inv 10, `[data-testid="overview-page"] p "Buchungen erscheinen hier`.
- budgeting: „1 von 2 fehlgeschlagen“ in Rot auf seiner Fläche 4,46:1, Ausnahme `budget-historie` @ 768/900/1024/1280/1440/1920, inv 10, `[data-testid="general-history-partly-failed"] span`.
- budgeting: „Verfügbar“ 0,00 € in `text-muted` auf heller Fläche 4,44:1, Ausnahme `budget-target-anlegen` @ 1440, inv 10, `[data-testid="budget-available"] span`.
- categorization: Hilfetexte im Regel-Dialog in `text-muted/70` (3,23:1), Ausnahme `regeln-dialog-picker` @ 1440, inv 10, `[data-testid="rule-dialog"] p`.
