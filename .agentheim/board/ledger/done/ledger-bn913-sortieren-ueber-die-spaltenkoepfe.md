---
id: ledger-bn913
title: "Kassenbuch sortieren, PC und Client-Zustand: Klick auf einen Spaltenkopf sortiert serverseitig nach Datum, Betrag, Payee, Kategorie oder Konto; außer bei Datum fallen die Tagesköpfe weg"
status: done
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001, ledger-qh9qx, ledger-r72re]
blocks: [ledger-skpcx, ledger-q1hw2]
tags: [frontend, kassenbuch, sortierung, tabelle]
related_adrs: [0036, 0037, 0049, 0051]
related_research: []
prior_art: [ledger-b1xwe, ledger-be1yh, ledger-p3n8k]
---

## Why
Die Liste ist heute fest nach Datum absteigend sortiert (`ORDER BY date DESC, id DESC` in
`Ledger.Persistence`). Roman will "mit Klick auf Datum etc." sortieren (2026-10-20), etwa
die größten Ausgaben eines Treffers oder alle Buchungen eines Payees beisammen sehen.

## What
Die Aufgabe ist in drei Teile geschnitten, jeder für sich auslieferbar:

1. `ledger-r72re`: Server. Vertrag, Persistenz, Messung. Ohne Oberfläche, Standardverhalten
   unverändert.
2. **Diese Aufgabe:** Sortierzustand im Client, klickbare Spaltenköpfe am PC, Tagesköpfe nur
   bei Datumssortierung, Datum in der Handy-Zeile bei flacher Liste.
3. `ledger-skpcx`: Sortier-Chip und BottomSheet am Handy.

Entscheidung Roman 2026-10-20 (gilt für alle drei Teile):

- **Sortierbar:** Datum, Betrag, Payee, Kategorie, in der Gesamtsicht Konto. Memo und
  Status nicht.
- **PC:** Klick auf einen sortierbaren Spaltenkopf sortiert danach; erneuter Klick dreht
  die Richtung. Der aktive Kopf zeigt Richtung per Pfeil und `aria-sort`.
- **Handy:** eine Sortier-Auswahl am Ende der Filterleiste, öffnet ein `BottomSheet`
  (`ledger-skpcx`).
- **Server sortiert** (`ledger-r72re`, ADR 0051). Trefferzahl und Summe bleiben über den
  ganzen Treffer korrekt.
- **Tagesköpfe:** Bei Sortierung nach Datum (auf- oder absteigend) bleiben Tagesgruppen
  und Tagessummen. Bei jeder anderen Sortierung wird die Liste flach, ohne Tagesköpfe; das
  Datum steht dann in jeder Zeile (PC: Datumsspalte; Handy: in der Zeile).
- Standard ist Datum absteigend. Die Sortierung lebt im Client-Zustand, übersteht Konto-
  und Filterwechsel, wird nicht dauerhaft gespeichert. Ein Sortierwechsel leert die
  Auswahl (sie bezieht sich auf geladene Zeilen) und lädt ab Seite 1 neu.

Festgelegt beim Verfeinern (2026-10-20, Begründung in ADR 0051):

- **Startrichtung** beim Wechsel auf ein anderes Feld: Datum absteigend, Betrag aufsteigend
  (kleinster Wert zuerst, also die größte Ausgabe oben), Payee, Kategorie und Konto
  aufsteigend. Ein Klick auf das schon aktive Feld dreht die Richtung. Eine reine Funktion
  `sortStartDirection field` liefert die Startrichtung.
- **Modell:** `Model.Sort: LedgerSort` (Typ aus `ledger-r72re`), Standard
  `LedgerSort.defaultSort`. Nachricht `SetSort of LedgerSortField`: gleiches Feld dreht die
  Richtung, anderes Feld setzt das Feld mit seiner Startrichtung. Der Sortierwechsel läuft
  über denselben Weg wie ein Filterwechsel (`resetEntries`, dann `LoadEntries`): Seite 0,
  `LocallyRemoved = 0`, Auswahl leer, Filter und Konto unverändert.
- **Wirksame Sortierung:** `effectiveSort model` liefert `LedgerSort.defaultSort`, wenn
  `SortByAccount` gewählt ist und `SelectedFilter` ein einzelnes Konto ist (die Spalte gibt
  es dort nicht). Das Modell behält die Wahl für die Rückkehr in die Gesamtsicht.
  `buildPageQuery` setzt `Sort = effectiveSort model`, Kopf und Listenform lesen ebenfalls
  `effectiveSort`.
- **Listenform:** Tagesgruppen (`groupByDate`, Tagessumme) nur, wenn die wirksame
  Sortierung `SortByDate` ist, in beiden Richtungen und auf PC und Handy. Sonst eine flache
  Liste ohne `calmGroupRow` und ohne `dateGroupHeader`. Eine reine Funktion
  `usesDayGroups sort` trägt die Entscheidung. In der flachen Handy-Zeile steht das Datum
  (`formatDate`) vorn in Zeile 2, vor Kontokürzel und Kategorie.
- **Spaltenkopf:** neue Design-System-Funktion `Table.calmSortableHeaderCell text align
  direction onSort` neben `calmHeaderCell`: ein `th` mit `aria-sort`
  (`ascending`, `descending`, `none`) und einem `button` darin (Tastatur: Enter und
  Leertaste), Pfeil nur am aktiven Kopf. Sie nutzt den vorhandenen `Table.SortDirection`
  (`Ascending | Descending | Unsorted`). Datum, Konto (nur in der Gesamtsicht), Payee,
  Kategorie und Betrag benutzen sie, Kästchen, Memo und Status bleiben `calmHeaderCell`
  bzw. `calmHeaderContentCell`. Die neue Funktion kommt in die `/styleguide`-Route
  (`src/Client/Components/Styleguide/View.fs`, Abschnitt der ruhigen Tabelle).
- "Alle Filter löschen" setzt die Sortierung nicht zurück: sie ist kein Filter.

## Acceptance criteria
Maschinell prüfbar:

- [ ] `Model.Sort` startet mit `LedgerSort.defaultSort`; `buildPageQuery` trägt
      `effectiveSort` (Update-Test, inklusive des Falls Konto-Sortierung mit
      `OneAccount`, der `defaultSort` liefert).
- [ ] `sortStartDirection` liefert Datum absteigend, Betrag aufsteigend, Payee, Kategorie,
      Konto aufsteigend (Test).
- [ ] `SetSort` auf ein anderes Feld setzt Feld und Startrichtung, auf das aktive Feld dreht
      es die Richtung; in beiden Fällen: `NextPage = 0`, `LocallyRemoved = 0`,
      `Selection = NoSelection`, Filter und `SelectedFilter` unverändert, `LoadEntries`
      wird ausgelöst (Update-Tests).
- [ ] Ein Konto- oder Filterwechsel (`SelectFilter`, Filter-Nachrichten) lässt `Sort`
      unverändert (Update-Test); "Alle Filter löschen" ebenso.
- [ ] `usesDayGroups` ist nur für `SortByDate` wahr, in beiden Richtungen (Test).
- [ ] Bei Datum aufsteigend gruppiert `groupByDate` die aufsteigend gelieferte Liste
      richtig, mit Tagessumme je Gruppe (Test mit zwei Tagen).
- [ ] Die flache Handy-Zeile zeigt das Datum in Zeile 2, die gruppierte nicht (Test der
      reinen Zeilen-Hilfsfunktion).
- [ ] Der Wert von `aria-sort` je `Table.SortDirection` ist `ascending`, `descending`
      oder `none` (Test der reinen Hilfsfunktion); Memo- und Status-Kopf bauen sich nicht
      über die Sortier-Funktion.
- [ ] Im Browser (PC-Viewport, Gesamtsicht): Klick auf "Betrag" lädt die Liste neu, die
      Beträge laufen vom kleinsten Wert aufwärts, kein Tageskopf steht in der Tabelle; ein
      zweiter Klick kehrt die Reihenfolge um; Klick auf "Datum" bringt die Tagesköpfe
      zurück. Der Kopf trägt `aria-sort`, der Fokus folgt der Tab-Reihenfolge, Enter löst
      aus (Chrome DevTools, Screenshot im Verifier-Bericht).
- [ ] `/styleguide` zeigt die sortierbare Kopfzelle mit den drei Zuständen.

Zum Hinschauen:

- [ ] Die Sortier-Pfeile im Kopf fügen sich in die ruhige Tabelle (ADR 0049). [human-eye]

## Notes
- Hängt an `ledger-qh9qx`, weil beide die Spaltenköpfe anfassen und die Kopf-Zellen
  dort neu verteilt werden. Blendet `ledger-qh9qx` bei schmaler Breite die Konto-Spalte aus,
  bleibt eine gewählte Konto-Sortierung aktiv, auch wenn kein Kopf sie mehr anzeigt; das
  Konto steht dann im Kürzel der Payee-Zelle. Die Handy-Auswahl (`ledger-skpcx`) zeigt die
  Wahl in jedem Fall.
- Hängt an `ledger-r72re`: ohne das Feld `Sort` in `LedgerEntryPageQuery` gibt es nichts
  zu senden.
- Der Sortierwechsel hängt an `resetEntries`; Fokuszeile und Auswahl-Modus fallen damit wie
  bei jedem Filterwechsel zurück.
- README-Satz "Kategoriewechsel sortiert nie um" bleibt gültig: eine Änderung an einer
  Zeile sortiert die geladene Liste nicht neu. Heute ändert kein Schreibweg einen
  Sortierschlüssel (der Prüfvermerk berührt nur `approved` und `cleared`), also gibt es nichts
  zu behandeln. Kommt das Bearbeiten der Kategorie (`ledger-q1hw2`), gilt die Grenze aus
  ADR 0051 Punkt 7.
- `ledger-rfq5p` ändert dieselben Zellen (Payee, Kategorie) in `View.fs`; bei gleichzeitiger
  Arbeit auf Merge-Konflikte in `entryTableRow` achten.

## Outcome

Klick auf einen sortierbaren Spaltenkopf der PC-Tabelle sortiert das Kassenbuch serverseitig (ADR 0051). Der Sortierzustand lebt im Client-Modell, die Anfrage trägt die wirksame Sortierung.

**Umgesetzt**
- `Types.fs`: `Model.Sort: LedgerSort`, Nachricht `SetSort of LedgerSortField`.
- `State.fs`: reine Funktionen `sortStartDirection`, `nextSort`, `effectiveSort`; `init` setzt `LedgerSort.defaultSort`; `buildPageQuery` sendet `effectiveSort model`; `SetSort` läuft über `resetEntries` plus `LoadEntries` (Seite 0, `LocallyRemoved = 0`, Auswahl leer, Filter und Konto unverändert). Die Richtung wird gegen die wirksame Sortierung gedreht, damit ein Klick auf das sichtbare Datum bei einer verdeckten Konto-Wahl wirklich dreht.
- `Table.fs`: `Table.ariaSortValue` (`ascending`, `descending`, `none`) und `Table.calmSortableHeaderCell text align direction onSort` (`th` mit `aria-sort`, `button` darin, Pfeil nur am aktiven Kopf).
- `View.fs`: `usesDayGroups`, `mobileRowDateText`, `headerSortDirection`; PC-Köpfe Datum, Konto (nur wenn sichtbar), Payee, Kategorie, Betrag über `sortHeader`, Kästchen, Memo und Status unverändert; Tagesgruppen in PC-Tabelle und Handy-Liste nur bei Datumssortierung, sonst flach; flache Handy-Zeile zeigt das Datum vorn in Zeile 2. `entryTableRow` und die Zellen sind nicht angefasst (ledger-q1hw2 baut sie danach um); `entryRow` bekam nur den Parameter `flatDate`.
- `Styleguide/View.fs`: Abschnitt "Ruhige Listen" zeigt die sortierbare Kopfzelle in den Zuständen aufsteigend, absteigend, ohne.
- `LedgerSort.orDefault` (Hotfix) bleibt unberührt.

**Tests (19 neu)**
- `src/Tests/LedgerSortClientTests.fs` (17 Expecto-Tests): Startwert, `buildPageQuery` inkl. `OneAccount`-Fall, `sortStartDirection`, `SetSort` (anderes Feld, aktives Feld, Datum-Klick bei verdeckter Konto-Wahl) mit allen Zuständen und `LoadEntries`, Konto-/Filterwechsel und "Alle Filter löschen" lassen `Sort` stehen, `usesDayGroups`, `groupByDate` bei aufsteigender Liste mit Tagessumme, `mobileRowDateText`, `headerSortDirection`, `ariaSortValue`, Quelltext-Zusicherung (Memo und Status nicht über `sortHeader`), Styleguide-Zusicherung.
- `scripts/ledger-sort-headers.test.mjs` (2 Playwright-Wächter am echten Client mit gemockter API, der Mock sortiert nach der gesendeten Sortierung): Klick auf Betrag, aufsteigend ohne Tageskopf, `aria-sort`, zweiter Klick kehrt um, Datum bringt die Tagesköpfe zurück; Tastatur (Fokus auf den Kopf, Enter), Status ohne Button und ohne `aria-sort`.
- Angepasst: zwei Quelltext-Wächter in `LedgerViewTests.fs` (Payee- und Betrag-Kopf heißen jetzt `sortHeader ...`), gleiche Absicht.

**Läufe (aus dem Worktree)**: `dotnet build` 0 Warnungen, 0 Fehler. `dotnet test` 1597 bestanden, 0 fehlgeschlagen, 7 übersprungen (vorher schon übersprungen). `npm run build` grün. `npm run guard:classes` grün ("Keine undefinierten Klassen gefunden"). `npm run test:scripts`: im Gesamtlauf mit der Standardparallelität hängt die Suite auf dieser Maschine (viele gleichzeitige Vite-/Fable-Server); seriell (`--test-concurrency=1`) liefen 153 Tests, 151 grün. Die zwei Rotausfälle: ein Wartefehler in meinem Wächter (behoben, danach grün) und `ledger-cell-menu.test.mjs` "Der Kategorie-Eintrag setzt..." (wartet mit `waitForFunction(() => true)` nicht auf die Anfrage, Lastflattern; die Datei läuft allein 9 von 9 grün). Dateien einzeln grün: `ledger-sort-headers.test.mjs` (2/2), `ledger-cell-menu.test.mjs` (9/9), `ledger-table-columns.test.mjs` (3/3).

**Hinweis für Wächter mit gemockter API**: ohne Antwort auf `mirrorYnabBudget` startet die Seite den Spiegel-Lauf in einer Schleife und die Tabelle baut sich ständig neu auf (Seitenhöhe wechselt zwischen 4128 und 4168 px, Klicks scheitern an "not stable"). Der neue Wächter lässt die Anfrage offen, wie `ledger-review-button-visibility.test.mjs`. `ledger-table-columns.test.mjs` hat dasselbe Problem (sein erster Test braucht dadurch rund 50 s) und wurde nicht angefasst.

**Nicht gesehen**: das Kriterium "[human-eye] Pfeile fügen sich in die ruhige Tabelle" ist offen, kein Screenshot angefertigt.

**Seiteneffekt im Lauf**: beim Aufräumen hängender Testprozesse habe ich mit `pkill -f vite` gearbeitet; ein Vite-Dev-Server der Hauptsitzung könnte dabei beendet worden sein (`npm run dev` im Hauptbaum prüfen).
