---
id: ledger-d9z3n
title: "Kassenbuch am Handy ordnen: eine Kontowahl als Feld mit Saldo, freie Suche statt Payee- und Memo-Filter, Filter-Knopf mit Sheet und gesetzte Filter als Chips; gilt unter 1024 px"
status: done
type: feature
context: ledger
created: 2026-11-14
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-cnaz3]
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0086, 0087, 0095]
related_research: []
prior_art: [ledger-ct3cx, ledger-pnjz3, ledger-dn4vy]
---

## Why
Schnitt 8 aus Etappe 3 (Fahrplan), erster von drei Teilen aus ledger-f8d7x (Kassenbuch ordnen). Das Kassenbuch hat am
Handy zwei Kontowahlen (Titel-Knopf und wischende Kartenleiste) und eine wischende Filterleiste mit acht Chips. Der
Styleguide (§7.5) verlangt eine Kontowahl als Auswahlfeld mit Saldo, darunter Suche und Filter-Knopf mit Sheet, gesetzte
Filter als Chips, keine seitlich wischende Leiste.

## What
Kopf, Kontowahl, Suche, Filter und Listenzeile des Tabs „Buchungen“ unter 1024 px nach Prototyp Version 17 (Funktion
`ledger()` und `sheetHtml` mit den Sheets `acct` und `filter`). Dazu im Modell die freie Suche, die die Filter Payee und
Memo ersetzt, und „Filter löschen“ für alle Filter. Beides gilt auch am PC; die PC-Anordnung baut ledger-cnaz3.

## Acceptance criteria

Breite
- [ ] Die Handy-Form gilt für den Tab „Buchungen“ unter 1024 px, auch zwischen 768 und 1023 px (Roman, 2026-11-14). Die
      Konten-Spalte neben der Liste (768 bis 1023 px, `accountStrip`/`accountListRowView`) entfällt.

Kopf
- [ ] Titel „Kassenbuch“ mit dem Untertitel wie heute („Gespiegelt …“, BB).
- [ ] Rechts im Kopf zwei Symbolknöpfe: „Abrufen“ (Symbol Aktualisieren wie im Prototyp, öffnet denselben Abruf wie auf
      der Übersicht) und „Jetzt spiegeln“ wie heute (BB). Beide mit `aria-label`.
- [ ] Unter 1024 px steht „+ Buchung“ als schwebender Knopf (FAB) wie heute, nicht im Kopf; er fehlt im Auswahl-Modus.

Kontowahl
- [ ] Unter den Tabs ein Auswahlfeld: Beschriftung „Konto“, Wert Kontoname oder „Alle Konten“, daneben der Saldo (Mono,
      klein, gedämpft), rechts ein Pfeil nach unten (Prototyp `.field`).
- [ ] Tippen öffnet ein BottomSheet „Konto“: „Alle Konten“ mit Gesamtsaldo, danach jedes Konto mit Saldo, ein negativer
      Saldo in `text-danger`, das gewählte mit Haken und Akzentrahmen. Tippen wählt und schließt.
- [ ] Der Titel-Knopf „<Konto> ▾“ und die wischende Kartenleiste mit Salden entfallen. Es gibt unter 1024 px genau eine
      Kontowahl.
- [ ] Die Wahl bleibt wie heute gespeichert (`ledger.selectedAccountFilter`).

Suche
- [ ] Unter der Kontowahl: Suchfeld „Suchen…“ (Lupe, `type=search`, `aria-label` „Buchungen durchsuchen“)
      und rechts der Knopf „Filter“ mit Filter-Symbol.
- [ ] Die Suche findet Teiltext ohne Groß- und Kleinschreibung in Payee, Memo und Kategoriename der Buchung, auf dem
      Server, mit Verzögerung beim Tippen wie heute der Memo-Filter. Gelöschte Zeilen bleiben ausgeblendet, solange
      „Gelöschte zeigen“ aus ist.
- [ ] Die Filter Payee und Memo entfallen in Modell, Vertrag und Oberfläche (Roman, 2026-11-14). Der Knopf im Detail,
      der heute den Payee-Filter setzt (`FilterByRowPayee`), setzt stattdessen die Suche auf den Payee; Beschriftung und
      Ort ändert erst ledger-zfj78.
- [ ] Am PC steht das Suchfeld bis ledger-cnaz3 am Ende der Filterleiste, damit die Suche dort nicht fehlt.

Filter-Sheet
- [ ] „Filter“ öffnet ein BottomSheet „Filter“ mit, in dieser Reihenfolge:
      - Schalter „Zu prüfen“, „Lena gefragt“, „Ohne Kategorie“, je mit der Anzahl gedämpft hinter dem Text (Prototyp).
      - Zeile „Kategorie“ mit Wert „alle“ oder den gewählten Kategorien und Pfeil; öffnet die heutige
        Kategorie-Auswahl mit Mehrfachauswahl (BB).
      - Zeile „Zeitraum“ mit Wert „alle“ oder dem gesetzten Zeitraum; öffnet die heutige Zeitraum-Auswahl mit
        Schnellwahl und Von/Bis (BB; Abweichung (a)).
      - „Betrag von“ und „bis“ als zwei Felder nebeneinander, `inputmode="decimal"`, ohne Vorzeichen.
      - Sortieren wie heute (BB), als Zeile „Sortieren“ mit Wert, öffnet die heutige Sortier-Auswahl.
      - Schalter „Gelöschte zeigen“ mit Anzahl, nur wenn es gelöschte Buchungen gibt (BB).
- [ ] Fuß des Sheets: „Zurücksetzen“ (neutral) und „n zeigen“ (primär, n = Treffer der Liste, während des Ladens nur
      „Zeigen“). Jede Änderung im Sheet wirkt sofort auf die Liste; „n zeigen“ schließt das Sheet.
- [ ] Der Filter-Knopf zeigt bei gesetzten Filtern „Filter (n)“ mit Akzentrahmen und Akzentschrift.

Gesetzte Filter
- [ ] Unter Suche und Filter-Knopf steht je gesetztem Filter ein Chip mit „×“: „Zu prüfen“, „Lena“, „Ohne Kategorie“,
      „Kategorie: <Name>“ (bei mehreren „Kategorie: n“), Zeitraum mit Kurzname, Betrag mit Bereich, eine andere
      Sortierung als „Datum, neueste zuerst“ und „mit Gelöschten“. „×“ nimmt genau diesen Filter zurück.
- [ ] Die Chips brechen um, die Zeile wischt nicht seitlich (§7.5).
- [ ] Ohne gesetzten Filter gibt es die Zeile nicht. Die wischende Filterleiste unter 1024 px entfällt.

Betragsfilter (Roman, 2026-11-14, nach Verifier iteration 1)
- [ ] Der Betragsfilter vergleicht die Höhe ohne Vorzeichen: „von 5 bis 50“ trifft jede Buchung, deren Betrag ohne
      Vorzeichen zwischen 5 und 50 liegt, Ausgaben wie Einnahmen. Die Felder nehmen kein Vorzeichen an. Gilt auf dem
      Server für alle Zählwege und auf allen Breiten.
- [ ] Neu: Richtung als Umschalter „Alle | Ausgaben | Einnahmen“ (`Segmented`), im Filter-Sheet direkt unter „Betrag von“
      und „bis“, am PC im Popover „Betrag“. Wirkt auch ohne Betrag. Eine gesetzte Richtung steht als Chip „Ausgaben“ bzw.
      „Einnahmen“ mit „×“ hinter dem Betrag-Chip und zählt zu „Filter (n)“; „Filter löschen“ setzt sie auf „Alle“.
- [ ] Handler-Test mit In-Memory-SQLite: „von 5 bis 50“ trifft eine Ausgabe über 12,00 € und eine Einnahme über 12,00 €;
      mit Richtung „Ausgaben“ nur die Ausgabe; Richtung allein ohne Betrag filtert ebenso. Die bestehenden Tests zum
      Betragsfilter folgen der neuen Regel.
- [ ] Die Regel steht als ADR (Betragsfilter als Höhe, Richtung getrennt) und im BC-README.

Konto-Sheet
- [ ] Alle Zeilen im Sheet „Konto“ zeigen Namen und Saldo in Primärtext mit Trennlinien wie im Prototyp; nur der Haken
      und der Akzentrahmen markieren das gewählte, ein negativer Saldo bleibt rot.

Filter löschen
- [ ] „Zurücksetzen“ im Sheet und „Filter löschen“ im leeren Zustand löschen alle Filter einschließlich „Zu prüfen“,
      „Ohne Kategorie“, „Lena“ und der Suche; das Konto bleibt (Roman, 2026-11-14). Gilt für `ClearAllFilters` auf
      allen Breiten.

Liste
- [ ] Trefferzeile „n Buchungen“ mit Summe über der Liste wie heute, sobald ein Filter oder eine Suche gesetzt ist (BB).
- [ ] Zeile: oben Payee und Betrag; darunter Konto als gedämpfter Text (nur in „Alle Konten“), Kategorie oder „ohne
      Kategorie“ in Gelb, Lena-Marke, Memo gedämpft, „nicht in YNAB“ in Rot, „doppelt?“ in Gelb, „in YNAB gelöscht“
      (BB), Datum bei anderer Sortierung als Datum (BB).
- [ ] Eine ungeprüfte Zeile hat die gelbe Kante und unter dem Betrag das Wort „zu prüfen“ in Gelb (Prototyp).
- [ ] Kein Prüf-Knopf an der Zeile, auch nicht bei Maus zwischen 768 und 1023 px (Roman, 2026-11-14). Geprüft wird per
      Wisch nach rechts (ledger-th36h, unverändert), im Detail oder mit G.
- [ ] Tagesköpfe mit Datum und Tagessumme wie heute (ledger-pnjz3).
- [ ] Leere Zustände nach §7.12: „Kein Treffer“ mit „Keine Buchung passt zu Suche und Filtern.“ und „Filter löschen“;
      die übrigen wie heute.

Tests und Abgleich
- [ ] Reine Funktionen unter .NET für: Chips aus dem Filterzustand (Text, Reihenfolge, „×“ nimmt den richtigen zurück),
      `ClearAllFilters` löscht alles außer dem Konto, Anzahl am Filter-Knopf, Suche setzt aus dem Detail den Payee.
- [ ] Handler-Test der Suche mit In-Memory-SQLite: Treffer in Payee, Memo und Kategoriename, Groß- und Kleinschreibung,
      kein Treffer bei gelöschter Zeile ohne „Gelöschte zeigen“.
- [ ] Browser-Tests: `[flow:konto-wechseln]` und `[flow:kassenbuch-filtern]` werden für die neue Form neu geschrieben
      (Handy: Feld, Sheet, Chip, „×“), je einer. `[flow:buchung-pruefen]` prüft am Handy per Wisch statt Knopf. Der
      `[fokus]`-Test zum Prüfknopf an der Zeile (`ledger-review-mark.test.mjs`) und der `[tastatur]`-Test zum
      Payee-Filter (`ledger-filter-bar.test.mjs`) entfallen. Fixtures in `scripts/*.test.mjs` folgen dem neuen Vertrag.
- [ ] Layout-Sweep: Zustände `kassenbuch-filter-payee` entfernt; neu `kassenbuch-filter-sheet` (offenes Sheet) und
      `kassenbuch-filter-gesetzt` (drei Chips) bei 320, 390, 412 und 768 px; `kassenbuch` bei 900 px ohne Konten-Spalte.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten: Screenshots bei 412 px von Liste, Konto-Sheet,
      Filter-Sheet und gesetzten Filtern neben `f8d7x-proto/handy-*.png`, im Outcome.

## Abweichungen vom Prototyp
- (a) Zeitraum als Zeile mit der heutigen Auswahl (Schnellwahl und freier Bereich), ohne Vorgabe, statt Umschalter
  „Monat | Vormonat | Jahr | Alle“ mit Vorgabe „Monat“. Grund: BB kann den freien Bereich, Roman hat das für den PC so
  entschieden (2026-11-14), am Handy gilt dasselbe.
- (b) Sortieren, „Gelöschte zeigen“ und die Trefferzeile kommen dazu (BB kann mehr, Roman 2026-11-14).
- (c) „Jetzt spiegeln“ und der Untertitel „Gespiegelt …“ bleiben (BB).
- (d) Der Umschalter „Alle | Ausgaben | Einnahmen“ unter dem Betrag kommt dazu (Roman, 2026-11-14): ohne Vorzeichen in
  den Feldern ließe sich sonst nicht nach Richtung trennen.
- (e) „Abrufen“ mit dem Rahmen von `Button.Secondary` wie „Jetzt spiegeln“; in der Zeile der volle Kontoname, gekürzt
  (vom Worker in Iteration 1 begründet, vom Verifier angenommen).

## Notes
Teil 1 von 3 aus ledger-f8d7x (Kassenbuch ordnen); danach ledger-cnaz3 (PC) und ledger-zfj78 (Detail), nacheinander, weil
alle drei `src/Client/Components/Ledger/View.fs` ändern. ledger-ayjzq (Sicht „Für mich“) bleibt eigene Task.

Vorlage für Worker und Verifier (claude.ai-Artifacts können sie nicht öffnen):
- Prototyp Version 17: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/f8d7x-proto/prototyp-v17.html`
  (Funktionen `txRow`, `ledger`, `sheetHtml` Zweige `acct` und `filter`).
- Screenshots: derselbe Ordner, `handy-liste.png`, `handy-konto.png`, `handy-filter.png`, `handy-filter-gesetzt.png`.
- Screenshot einer BB-Route: `npm run screenshot:route` wartet auf `networkidle` und bricht beim Kassenbuch ab
  (infrastructure-hgbey). Funktioniert: `node <scratchpad>/shotjs/route.mjs '#/ledger' --out <dir>` aus dem Repo.
  Die Dev-DB ohne YNAB-Token stapelt Fehler-Toasts (Kann-Zeile ledger-k4n2s 122); für den Vergleich die API mocken oder
  die Toasts wegklicken.

Fundstellen heute (Inventar 2026-11-14): Kontowahl `View.fs` 1101 bis 1189 und 4535 bis 4579, Filterleiste
`filterChipBar` ab 3588, Sortieren ab 3547, Trefferzeile `summaryRow` ab 3614, Prüf-Knopf an der Zeile 572 bis 600 und
821 bis 832, Filterfelder `Types.fs` 169 bis 216 und 322 bis 369, `ClearAllFilters` `State.fs` 2158, Payee- und
Memo-Vergleich auf dem Server `src/Server/Ledger/Persistence.fs` 1431 bis 1448. Der Wechsel bei 768 px kommt aus
`Primitives.Responsive` und `Viewport.isDesktopViewport`; für diese Seite gilt 1024 px.

Der Vertrag in `src/Shared` ändert sich (Suchfeld statt Payee und Memo): Laufzeitfalle Fable.Remoting, Fixtures in
`scripts/*.test.mjs` im selben Schritt mitziehen.

## Verifier note (iteration 1)
REASONS:
- Kriterium „Betrag von“ und „bis“ ohne Vorzeichen ist im Verhalten nicht erfüllt: das Sheet nutzt das alte vorzeichenbehaftete `amountField`, `parseAmountFilterText` erlaubt ein Vorzeichen, der Server vergleicht mit Vorzeichen (`amountMatches`, Persistence.fs 1341). „5 bis 50“ trifft nur Einnahmen. Die Folge-Task ledger-b7m2q erfüllt das Kriterium dieser Task nicht. Die Regel (Betrag als Höhe oder Schalter für die Richtung) entscheidet die Task nicht.
- Konto-Sheet: nicht gewählte Zeilen gedämpft (`Money.accountChoiceRow` mit `textSecondary`), im Prototyp stehen alle Namen und Salden in Primärtext. Nicht als Abweichung genannt.
- Die zwei vom Worker genannten Abweichungen (Rahmen an „Abrufen“, voller Kontoname) sind in Ordnung. Hinweis: die Symbole „Abrufen“ und „Jetzt spiegeln“ sehen bei 412 px fast gleich aus.
- Sonst alles erfüllt, `./verify.sh` VERIFY: PASS (dotnet 5040, node 338).
SUGGESTED_FIX: Roman entscheidet die Regel für den Betragsfilter ohne Vorzeichen; dann in dieser Task mit Handler-Test „Betrag von 5 bis 50 trifft eine Ausgabe über 12,00 €“ umsetzen. Nicht gewählte Zeilen im Konto-Sheet in Primärtext wie im Prototyp.
ITERATION_HINT: task-under-specified

## Outcome

Der Tab „Buchungen“ hat unter 1024 px die Handy-Form nach Prototyp Version 17, auch zwischen 768 und 1023 px. Iteration 2 setzt Romans Regel für den Betragsfilter um (Höhe ohne Vorzeichen, Richtung getrennt, ADR 0095) und gleicht das Sheet „Konto“ an den Prototyp an. `./verify.sh`: VERIFY: PASS (dotnet 5045 grün, 15 erlaubte übersprungen; node 338 grün; Build, beide Wächter).

Umsetzung:
- **Vertrag** (`src/Shared/Domain.fs`): `LedgerEntryPageQuery` und `LedgerPayeeSuggestionQuery` tragen `Search` statt `Payee`/`Memo` und neu `AmountDirection: AmountDirection` (`AllDirections | OnlyOutflows | OnlyInflows`, ADR 0095); `LedgerEntryPage` trägt neu `WithoutCategoryCount`. Fixtures in `scripts/*.test.mjs` und `scripts/ledger-filter-bar.mjs`, `ledger-status-column.mjs` sind im selben Schritt nachgezogen (die Richtung steht nur in Anfragen, die keine Fixture baut); `scripts/measure-ledger-sort.fsx` hat das neue Feld.
- **Server** (`src/Server/Ledger/Persistence.fs`, `QueryHandlers.fs`): `EntryFilter.Search`, in `buildFilterClause` als Teiltext über `bb_lower` in `payee`, `memo`, `category_name` und im heutigen Namen aus `ledger_categories` (eigene Kategorie und Split-Zeile über `ledger_entry_split_categories`). Gelöschte bleiben außen vor, solange `IncludeDeleted` aus ist. `countWithoutCategoryEntries` liefert die Anzahl hinter „Ohne Kategorie“. **Betragsfilter** (ADR 0095): `amountMatches` vergleicht `AmountMin`/`AmountMax` mit `abs amount` und prüft `EntryFilter.AmountDirection` (Ausgabe unter 0, Einnahme über 0, 0 ist keines); `hasAmountFilter` ist auch bei gesetzter Richtung wahr, so gilt sie für alle Zählwege und die Payee-Vorschläge.
- **Modell** (`Ledger/Types.fs`, `Ledger/State.fs`): `Filters.Search`, `SearchDraft`/`SearchVersion` mit `SetSearchDraft`/`CommitSearch`/`RemoveSearch`, `FilterSheetOpen` mit `OpenFilterSheet`/`CloseFilterSheet`, `WithoutCategoryCount`. Entfallen: Payee- und Memo-Filter samt Vorschlägen, `PayeeChip`/`MemoChip`. `FilterByRowPayee` setzt die Suche. Reine Funktionen `activeFilterChips`/`activeFilters`, `removeActiveFilterMsg`, `clearAllFilters`, `filterButtonText`, `sortValueText`, `dateRangeShortText`. `ClearAllFilters` nimmt alle Filter zurück, auch „Zu prüfen“, „Lena“, „Ohne Kategorie“, Sortierung, „Gelöschte zeigen“, die Richtung und die Suche; das Konto bleibt. Neu in Iteration 2: `Filters.AmountDirection`, `SetAmountDirection` (sofort, gleiche Richtung lädt nicht neu), `RemoveAmountAndDirection` (PC-Chip „Betrag“), `ActiveFilter.Direction` (Chip „Ausgaben“/„Einnahmen“ hinter dem Betrag), `hasDirectionFilter` in `hasAnyFilter`, `directionText`/`directionOptions`, Richtung in `filterPlainText`. Die Betragsfelder nehmen kein Vorzeichen an: `withoutAmountSign` in `SetAmountMinDraft`/`SetAmountMaxDraft`, `parseAmountFilterText` ohne `AllowLeadingSign`.
- **Oberfläche** (`Ledger/View.fs`): unter 1024 px Auswahlfeld „Konto“ mit Saldo und Sheet „Konto“, Suchfeld mit Filter-Knopf, Chip-Zeile (umbrechend), Filter-Sheet mit Schaltern samt Anzahl, Zeilen Kategorie/Zeitraum/Sortieren, Betrag von/bis mit dem Umschalter „Alle | Ausgaben | Einnahmen“ darunter (`Segmented`, Gruppe „Richtung“; am PC derselbe Umschalter im Popover „Betrag“), „Gelöschte zeigen“, Fuß „Zurücksetzen“ und „n zeigen“. Kartenleiste, Titel-Knopf und Konten-Spalte entfallen; die Liste ist unter 1024 px immer die Handy-Liste, das Detail ein Sheet. Kopf: „Abrufen“ und „Jetzt spiegeln“ als Symbolknöpfe, „+ Buchung“ als FAB bis 1023 px. Zeile: Konto als gedämpfter Text in „Alle Konten“, Flag vor dem Memo, „nicht in YNAB“ rot, „doppelt?“ gelb, „zu prüfen“ gelb unter dem Betrag; kein Prüfknopf unter 1024 px, auch nicht mit Maus. Leerer Zustand „Kein Treffer“ mit „Keine Buchung passt zu Suche und Filtern.“ und „Filter löschen“. Ab 1024 px steht das Suchfeld am Ende der Filterleiste.
- **Sheet „Konto“** (Iteration 2): die Zeilen stehen in einer Karte (`rounded-xl border border-border-subtle bg-surface-card`), jede mit Namen (`font-medium`) und Saldo in Primärtext und Trennlinie, 56 px wie `.row` im Prototyp; nur Haken und Akzentrahmen markieren das gewählte Konto, ein negativer Saldo bleibt rot (`Money.accountChoiceRow`, Styleguide §5 und `/styleguide` nachgezogen).
- **Design-System** (neue Varianten, auf `/styleguide` gezeigt, `standards/frontend/styleguide.md` §5 nachgezogen): `Input.selectField`, `Input.searchField` (CSS `.input-field.input-search`), `Input.countSwitchRow`, `Input.valueRow`, `Money.accountChoiceRow`, `Button.OutlineSelected`.

Tests:
- `src/Tests/LedgerHandyFilterTests.fs` (neu, 20): Chips aus dem Filterzustand (Text, Reihenfolge mit „Ausgaben“ hinter dem Betrag, Kurzformen, Sortierung nur bei Abweichung), „×“ nimmt über alle neun Chips genau den einen zurück, `ClearAllFilters` löscht alles außer dem Konto, wartende Sucheingabe holt nichts zurück, Zahl am Filter-Knopf, Suche, Filter-Sheet, `WithoutCategoryCount`, Kopf mit „Abrufen“. Neu in Iteration 2: Richtung als Chip auch ohne Betrag und in „Filter (n)“, „Alle“ ist kein Chip; `SetAmountDirection` wirkt sofort und steht in der Anfrage; die Betragsfelder streichen „-“, „+“ und „−“; „×“ am PC-Chip „Betrag“ nimmt Betrag und Richtung zurück.
- `src/Tests/LedgerPageTests.fs`: Handler-Test „Suche: Treffer in Payee, Memo und Kategoriename …“ mit In-Memory-SQLite. Neu: Handler-Test „Betragsfilter: „von 5 bis 50“ trifft Ausgabe und Einnahme über 12,00 €, die Richtung trennt sie“ (In-Memory-SQLite, eigenes Konto): Liste, Trefferzahl, Summe, `DeletedCount` und Payee-Vorschläge; mit „Ausgaben“ nur die Ausgabe, mit „Einnahmen“ nur die Einnahme; die Richtung allein ohne Betrag filtert ebenso. Vor der Änderung am Server rot (der Vergleich mit Vorzeichen ließ -12 aus).
- Die bestehenden Tests zum Betragsfilter folgen der neuen Regel (nach der Server-Änderung rot, dann umgestellt): AC6, AC-Umlaut, fünf `Betrags-Zweig`-Tests (dazu „Blättern über safePage“ umgestellt, war nicht rot), AC15 und AC4 in `LedgerPageTests.fs`, „Betragsfilter mit Sortierung …“ in `LedgerSortTests.fs` (Höhe 5 bis 100 mit „Ausgaben“); `LedgerViewTests.fs`: ein Vorzeichen ist unlesbar statt „bleibt negativ“. Die einseitigen Grenzen prüfen jetzt Höhe und Randwert mit einer Einnahme im Konto.
- Angepasst in Iteration 1, weil sich die Entscheidung geändert hat: `ClearAllFilters` lässt „Zu prüfen“, „Lena“, „Ohne Kategorie“ und Sortierung nicht mehr stehen (`LedgerPageTests`, `LedgerLenaFlagClientTests`, `LedgerWithoutCategoryTests`, `LedgerSortSheetTests`, `LedgerSortClientTests`).
- Browser: `[flow:konto-wechseln]` (`scripts/ledger-account-choice.test.mjs`) und `[flow:kassenbuch-filtern]` (`scripts/ledger-filter-bar.test.mjs`) neu geschrieben; `[flow:buchung-pruefen]` prüft am Handy per Wisch. Entfallen: `[fokus]` Prüfknopf (ledger-review-mark) und `[tastatur]` Payee-Filter (ledger-filter-bar).
- Layout-Sweep (`scripts/layout-states.mjs`): `kassenbuch-filter-payee` entfernt, neu `kassenbuch-filter-sheet` (seit Iteration 2 mit „Ausgaben“ im Umschalter gewählt) und `kassenbuch-filter-gesetzt` (320, 390, 412, 768); `kassenbuch` läuft bei 768 und 900 in der Handy-Form ohne Konten-Spalte. Ausnahmen: das 24-px-„×“ der Chips (`kassenbuch-filter-gesetzt` und seit Iteration 2 `kassenbuch-filter-sheet` mit dem Chip „Ausgaben“ hinter dem Sheet, je 320/390/412; `kassenbuch-ausgegraut-handy` 412; Inv. 2), steht schon in ledger-k4n2s Punkt 1.

Skizzenvergleich (412 px, Bilder unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/d9z3n-shots/`): `bb-liste-412.png` und `bb-liste-alle-konten-412.png` neben `f8d7x-proto/handy-liste.png`; `bb-konto-412.png` (Iteration 2 neu aufgenommen: Karte mit Trennlinien, alle Zeilen in Primärtext, gewählte mit Haken und Akzentrahmen, negativ rot) neben `handy-konto.png`; `bb-filter-412.png` (neu: Umschalter unter dem Betrag, „Ausgaben“ gewählt) neben `handy-filter.png`; `bb-filter-gesetzt-412.png` (neu aufgenommen) neben `handy-filter-gesetzt.png`; neu `bb-filter-richtung-412.png` (Chip „Ausgaben“ mit „×“, „Filter (1)“); dazu `bb-liste-900.png` und `bb-liste-1280.png` aus Iteration 1. Abweichungen nur die genannten (a) bis (e).

Entscheidungen:
- ADR 0095 (provisorisch, `0095-betragsfilter-als-hoehe-richtung-getrennt.md`): Betragsfilter als Höhe ohne Vorzeichen, Richtung als eigener Umschalter (Roman, 2026-11-14). Die Folge-Task ledger-b7m2q aus Iteration 1 entfällt, die Regel ist Teil dieser Task.
- Ohne ADR: „Filter löschen“ setzt auch Sortierung und „Gelöschte zeigen“ zurück (beide stehen als Chip). Die Zahl am Filter-Knopf zählt die Chips, die Suche nicht (Prototyp `nf`). Ein Betrag von genau 0 ist bei gesetzter Richtung weder Ausgabe noch Einnahme (ADR 0095 Punkt 2). Am PC bleibt der Chip „Betrag“ ein Chip für Betrag und Richtung, sein „×“ nimmt beides zurück; die eigene PC-Form entscheidet ledger-cnaz3.

README_DELTA: geprüft mit `npm run check:readme-delta -- --bc ledger --delta <datei> --keep "übernehmen"`, Exit 0. `--keep "übernehmen"`: das Wort steht weiter im Eintrag **Payee ändern** und meint dort die Zeile „übernehmen“ der Payee-Auswahl im Detail, die bleibt. „Ganz nachlesen“: `## Ereignisse` ValuationRecorded (gemeinsame Begriffe `ledger_entry_split_categories`, `ledger_entry_split_transfers`, `rebuildSplitCategoryIndex`): passt, betrifft Suche und Betragsfilter nicht. Nicht per Delta erreichbar (Fließtext statt Bullet): der Absatz „Einstieg und Navigation“ unter `## Oberfläche` nennt noch Titel-Knopf und Kartenleiste, und der Unterpunkt `getLedgerPayeeSuggestions` unter den Abfragen nennt noch „Memo, Payee selbst ausgenommen“; beides beim Integrieren von Hand nachziehen.

Kann-Zeilen (Kann-Sammlung ledger):
- `MeasuredBottomBar` schreibt `--ledger-bottom-bar-height`, keine CSS-Regel liest den Wert mehr (vorher die Konten-Spalte).
- `FilterPopover.payeeWidthClass` ist ohne Nutzer.
- Die Handy-Zeile gilt auch für die Kompaktliste ab 1024 px (gedämpfter Kontoname, Wörter statt Badges); ledger-cnaz3 entscheidet den PC.
- Das Filter-Sheet zeigt sich nach Kategorie- oder Zeitraum-Auswahl mit neuer Einblende-Animation.
- Die Symbole „Abrufen“ und „Jetzt spiegeln“ sehen bei 412 px fast gleich aus (Hinweis des Verifiers, Iteration 1).
- Die Richtung allein zwingt den F#-Nachfilter ohne `LIMIT`; wird es eng, als SQL-Bedingung auf das Vorzeichen vorziehen (ADR 0095, Folgen).

Bei der Integration nachgezogen (Conductor):
- README von Hand: der Absatz „Einstieg und Navigation“ unter „Oberfläche“ (Auswahlfeld „Konto“ statt Titel-Knopf und Kartenleiste), der Unterpunkt `getLedgerPayeeSuggestions` (kein Payee-Filter mehr, nur noch `payeeChoicesQuery` ohne Filter), im Eintrag **Zellmenü** „Payee als Suche“ statt `SelectPayeeFilter` und im Eintrag **Nachladen im Hintergrund** `RemoveSearch` statt `RemoveMemoFilter`. „Ganz nachlesen“ ValuationRecorded selbst geprüft: passt.
- `scripts/measure-ledger-sort.fsx` hatte noch `Payee`/`Memo` und kein `OnlyAskLena` (Hinweis des Verifiers, Iteration 2): auf `Search = None` und `OnlyAskLena = false` umgestellt. Das Skript läuft nicht im Gate.
- Verifier Iteration 2: PASS. Hinweise ohne FAIL: im Filter-Sheet fehlen die Trennlinien des Prototyps, und das Konto-Sheet des Prototyps hinterlegt die gewählte Zeile zusätzlich leicht (BB folgt dem Kriterium: nur Rahmen und Haken). Beides steht für Romans Blick im Fahrplan.

