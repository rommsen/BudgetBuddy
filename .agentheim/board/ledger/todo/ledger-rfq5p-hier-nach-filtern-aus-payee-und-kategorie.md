---
id: ledger-rfq5p
title: "\"Hier nach filtern\": Klick auf Payee oder Kategorie einer Zeile öffnet ein kleines Menü, das den passenden Such-Filter in einem Schritt setzt"
status: todo
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, kassenbuch, filter, payee, kategorie]
related_adrs: [0036, 0037, 0038, 0043, 0048, 0049]
related_research: []
prior_art: [ledger-j8xq4, ledger-saqsk]
---

## Why
Um alle Buchungen eines Payees oder einer Kategorie zu sehen, muss Roman heute den Chip
öffnen, suchen und auswählen, obwohl der Wert direkt vor ihm in der Liste steht. Roman
wünscht sich "eine Abkürzung zum Filtern" per Klick auf Payee und Kategorie
(2026-10-20).

## What
Entscheidung Roman 2026-10-20: ein kleines Menü beim Klick.

- **PC:** Klick auf die Payee-Zelle öffnet ein Mini-Menü direkt an der Zelle mit
  "Nach <Payee> filtern". Klick auf die Kategorie-Zelle entsprechend "Nach <Kategorie>
  filtern". Der Klick öffnet dann nicht die Historie (`stopPropagation`); ein Klick
  irgendwo sonst auf der Zeile öffnet sie wie bisher.
- **Handy:** Das Sheet, das ein Tipp auf die Zeile öffnet, bekommt dieselben zwei
  Aktionen. Dieses Sheet ist heute `historySheet` (`Components/Ledger/View.fs`, ein
  `BottomSheet`, geöffnet über `OpenHistory`, Titel der Payee, Untertitel "Datum ·
  Historie"). Im Auswahl-Modus schaltet ein Tipp die Auswahl und öffnet kein Sheet, dort
  gibt es die Aktionen nicht.
- Die Aktion setzt den bestehenden Such-Filter (keinen neuen Filterweg): Payee wie
  `SelectPayeeFilter` (exakt, gefaltet, ADR 0037), Kategorie als Kategorienauswahl mit
  genau dieser einen Kategorie (Zustand wie nach `ApplyCategoryFilter`). Andere aktive Filter
  bleiben stehen (Und-Verknüpfung); eine schon gesetzte Kategorienauswahl wird ersetzt, nicht
  ergänzt.
- Kein Eintrag, wo es nichts zu filtern gibt: Buchung ohne Payee (leer oder nur
  Leerzeichen), ohne Kategorie, Split (Kategorie-Zelle zeigt den Split, keine einzelne
  Kategorie) und Übertrag ohne Kategorie. Gibt es für eine Zelle keinen Eintrag, ist sie
  nicht klickbar und ein Klick darauf öffnet wie überall die Historie.
- Esc oder Klick daneben schließt das Menü ohne zu filtern, wie `FilterPopover`.

Festgelegt beim Verfeinern (2026-10-20):

- **Hülle am PC: `Client.DesignSystem.FilterPopover` passt als Hülle, ein neues Element ist
  nicht nötig.** Es bringt Klick-daneben, Esc, Oberfläche und Schatten mit und hängt
  absolut unter einem `relative`-Wrapper. Zwei Ergänzungen im selben Modul:
  `FilterPopover.menuWidthClass` (`w-[min(92vw,240px)]`) und
  `FilterPopover.menuItem label onClick`, eine volle Zeile als `button`
  (`role="menuitem"`, Hover-Grund wie `Tokens`, Beschriftung auf eine Zeile gekürzt,
  `title` mit dem vollen Text). Die Eintragsliste ist ein `ReactElement list`, ein zweiter
  Eintrag ("Kategorie ändern", `ledger-q1hw2`) hängt später nur an. Die Ergänzungen
  erscheinen in der `/styleguide`-Route als eigene Karte im Abschnitt "24. FilterPopover"
  (Muster der vorhandenen Karte mit `data-ds-card`, `data-ds-card-name`,
  `data-ds-group`).
- **Zwei Stolperstellen der Hülle, die der Bau beachten muss:** (1) Die Payee- und
  Kategorie-Zellen stecken in einem `truncate`-Div (`truncatedRoleCell`); ein Popover
  darin würde abgeschnitten. Der `relative`-Wrapper mit dem Popover steht deshalb als
  Geschwister neben dem gekürzten Div, beide in der Zelle. (2) `FilterPopover` schließt auf
  Esc nur, wenn der Fokus im Popover liegt. Der erste Eintrag bekommt deshalb `autoFocus`.
- **Auslöser** ist ein `button` im Zelltext (`type="button"`, `aria-haspopup="menu"`,
  `aria-expanded`), dessen Klick `stopPropagation` ruft. Hover: Zeiger und leichte
  Aufhellung des Textes, keine Unterstreichung in der Ruhe (ADR 0049).
- **Zustand:** `Model.CellMenu: CellMenuTarget option` mit `CellMenuTarget = { EntryId:
  Guid; Kind: PayeeCell | CategoryCell }`. Nachrichten: `OpenCellMenu of Guid *
  CellMenuKind`, `CloseCellMenu`, `FilterByRowPayee of string`, `FilterByRowCategory of
  Guid`. `OpenCellMenu` schließt einen offenen Chip-Popover (`OpenPopover = None`),
  `OpenFilterPopover` schließt das Zellmenü: es ist immer höchstens eines offen. Die
  beiden Filter-Nachrichten setzen das Menü und, am Handy, `HistoryFor` zurück und laden
  über denselben Weg wie `SelectPayeeFilter` (`resetEntries`, `LoadEntries`).
- **Die Kategorie-Id fehlt im Lesepfad:** `LedgerEntryView` trägt nur `CategoryName` und
  `CategoryGroupName`. Neues Feld `CategoryId: Guid option` (reines Read-Model-DTO, kein
  Ereignis, kein Schema), gefüllt in `ReadModel.toEntryView` aus `entry.Category`. Ein
  Nachschlagen über Name und Gruppe in der Kategorienliste ist unzulässig (Namen sind
  über Gruppen hinweg nicht eindeutig, eine Umbenennung trennt sie). Alle
  `LedgerEntryView`-Literale in `QueryHandlers` und den Testdateien (`LedgerSelectionTests`,
  `LedgerPagingTests`, `LedgerViewTests`, `LedgerPageTests`, `LedgerReviewMarkClientTests`)
  setzen das Feld.
- **Kategorie-Aktion setzt `Filters.CategoryIds = { id }` und schaltet den Chip "Ohne
  Kategorie" aus** (`WithoutCategory = false`): er verbindet sich mit einer nicht leeren
  Kategorienauswahl per Oder (ADR 0048) und würde sonst auch Buchungen ohne Ziel
  einschließen, obwohl "nach dieser Kategorie filtern" gewählt wurde. Alle anderen Filter
  (Zeitraum, Betrag, Memo, Payee, "Zu prüfen", Konto) bleiben. Die Payee-Aktion lässt
  alles außer `Payee` unverändert.
- **Anzeige der Einträge:** reine Funktionen `payeeMenuLabel entry : string option` und
  `categoryMenuLabel entry : string option`, `None` heißt kein Eintrag. Kategorie:
  `entry.CategoryId` ist gesetzt und `entry.Splits` ist leer; der Text nimmt
  `categoryLabelQualified entry`, damit er der PC-Zelle gleicht.
- **Handy:** `historySheet` bekommt oben im Inhalt, vor der Historie, einen Block mit den
  beiden Aktionen (nur die, für die es einen Eintrag gibt), jeweils als Zeile in der Art von
  `FilterPopover.menuItem`. Ohne Eintrag entfällt der Block ganz. Eine Aktion schließt das
  Sheet und filtert. Der Footer des Sheets bleibt leer.

## Acceptance criteria
Maschinell prüfbar:

- [ ] `payeeMenuLabel` liefert für einen Payee "Nach REWE filtern" und `None` für `None`,
      leeren Text und nur Leerzeichen; `categoryMenuLabel` liefert `Some` nur bei gesetzter
      `CategoryId` und leerer `Splits`-Liste (Tests, inklusive Split, Übertrag ohne
      Kategorie und Buchung ohne Kategorie).
- [ ] `ReadModel.toEntryView` füllt `CategoryId` aus der Kategorie der Buchung und lässt es
      leer bei Buchungen ohne Kategorie und bei Splits (Test).
- [ ] `FilterByRowPayee` setzt `Filters.Payee` auf den Wert, lässt alle übrigen Filter,
      `OnlyNeedsReview` und `SelectedFilter` unverändert, setzt `CellMenu` und `HistoryFor`
      zurück und löst `LoadEntries` aus (Update-Test).
- [ ] `FilterByRowCategory` ersetzt eine vorher gewählte Mehrfachauswahl durch genau diese
      Id, schaltet `WithoutCategory` aus, lässt Zeitraum, Betrag, Memo, Payee, `OnlyNeedsReview`
      und Konto stehen und löst `LoadEntries` aus (Update-Test).
- [ ] Beide Filter-Nachrichten leeren die Auswahl und setzen die Seite auf 0 (wie jeder
      Filterwechsel, Update-Test).
- [ ] `CloseCellMenu` ändert `Filters` und `Payee` nicht; `OpenCellMenu` schließt einen
      offenen Chip-Popover, `OpenFilterPopover` schließt das Zellmenü (Update-Tests).
- [ ] Trefferzahl und Summe nach der Aktion stammen aus der Serverabfrage mit dem neuen
      Filter; der Chip zeigt den Payee bzw. "Kategorie: 1" (Test der Anfrage über
      `buildPageQuery` mit dem Zustand nach der Aktion).
- [ ] Im Browser (PC): Klick auf die Payee-Zelle öffnet das Menü und nicht die Historie, der
      Eintrag filtert; Esc und Klick daneben schließen ohne Filteränderung (der Fokus liegt
      beim Öffnen im Menü); eine Zelle ohne Eintrag öffnet die Historie. Das Menü wird in der
      gekürzten Zelle nicht abgeschnitten (Chrome DevTools, Screenshot im Verifier-Bericht).
- [ ] Im Browser (390x844-Viewport): Tipp auf eine Zeile öffnet das Sheet mit den
      Aktionen oberhalb der Historie; die Aktion schließt das Sheet und filtert.
- [ ] `/styleguide` zeigt die Karte mit `menuItem` im Popover.

Zum Hinschauen:

- [ ] Payee- und Kategorie-Zelle verraten beim Hover, dass sie klickbar sind, ohne die
      ruhige Liste zu stören (ADR 0049). [human-eye]

## Notes
- Heute öffnet ein Klick auf die Zeile die Historie (`OpenHistory`, PC über
  `Table.row OnClick` in `entryTableRow`, Handy über `entryRow`). Die README nennt als
  Ziel "Kategorie über den Picker (auch direkt aus der Tabellenzelle)"; das ist noch nicht
  gebaut (Bearbeiten sitzt in `ledger-q1hw2`). Kommt das Bearbeiten, wird "Kategorie
  ändern" ein zweiter Eintrag im selben Menü und im selben Block des Sheets. `historySheet`
  wird dort zum Detail (Handy-Sheet, PC-Seitenpanel); der Aktionsblock zieht mit.
- Ein ausgeblendeter Split mit einer einzigen Kategorie (ADR 0038) zählt als Split, also
  kein Eintrag.
- `ledger-bn913` und `ledger-qh9qx` ändern die Spaltenköpfe und Zellen derselben Tabelle
  (`entryTableRow`, `truncatedRoleCell`); bei gleichzeitiger Arbeit auf Merge-Konflikte
  achten. Eine Reihenfolge ist nicht nötig.
- Nach `ledger-qh9qx` kann die Payee-Zelle ein Konto-Kürzel tragen. Der Auslöser umfasst nur
  den Payee-Text, nicht das Kürzel.
