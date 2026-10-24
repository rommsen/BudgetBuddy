---
id: ledger-b7qmx
title: "Split-Zeilen-Kategorie im Buchungs-Detail voll qualifizieren (ADR 0045)"
status: todo
type: feature
context: ledger
created: 2026-09-25
depends_on: [design-system-001]
blocks: []
tags: [backend, frontend, kassenbuch]
related_adrs: [0039, 0045]
related_research: []
prior_art: [ledger-h6ptn]
---

## Why

`ledger-h6ptn` hat die Kategorie einer Buchung in der PC-Tabelle voll qualifiziert
("Alltag: Lebensmittel", ADR 0039). Split-Zeilen (`SplitTarget.ToCategory`) sind davon
bewusst ausgenommen geblieben: der Lookup (`Ledger.Persistence.loadCategoryGroups`,
`Map<Guid, string>`) könnte eine Split-Zeilen-Kategorie sofort genauso qualifizieren,
aber der Entwurf (`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`) sagt
nichts dazu, und `ledger-h6ptn`s Task-Text hat das ausdrücklich als offenen Punkt für
später benannt, statt es zu erfinden.

**Entscheidung (Roman, 2026-10-02, ADR 0045):** eine Split-Zeilen-Kategorie erscheint im
Buchungs-Detail voll qualifiziert, genau wie die Kopf-Kategorie in der PC-Tabelle. Die
Listenzeile selbst bleibt unverändert bei "N Split-Zeilen"
(`categoryLabel`/`categoryLabelQualified`, `src/Client/Components/Ledger/View.fs`): sie
fasst ohnehin mehrere Ziele zusammen, eine Qualifizierung dort wäre nicht sinnvoll lesbar.

Befund bei der Verfeinerung (Stand 2026-10-24): `ledger-q1hw2` ist gebaut (Commit 2d4f5c0,
Fix a009eb9) und rendert Split-Zeilen im Detail bereits einzeln, heute unqualifiziert. Die
Feldzeile "Split" (`detail-split`) steht in `detailContent`
(`src/Client/Components/Ledger/View.fs`) und baut ihre Zeilen aus `splitLineTexts entry`;
das ruft `splitTargetLabel` je `TransactionSplit` auf (`ToCategory` zeigt nur den
Blattnamen, `ToTransfer` "Übertrag: <Konto>", `Uncategorized` "kein Ziel") und gibt
`(Ziel, Betrag)`-Paare an `Detail.splitLines` (`src/Client/DesignSystem/Detail.fs`). Panel
(`detailPanelView`, PC) und BottomSheet (Handy, Kompaktliste, schmales Fenster) teilen sich
dieselbe `detailContent`, es gibt also genau eine Stelle für die Verdrahtung. Der
Split-Editor in `ynab-sync` (`SplitSheet.fs`) bleibt außen vor (ADR 0045 Punkt 6).

## What

### Server/Shared (ADR 0045 Punkt 3)
- `src/Shared/Domain.fs`: `LedgerEntryView` bekommt ein neues Feld
  `SplitCategoryGroupNames: Map<Guid, string>` (Schlüssel: `YnabCategoryId` einer
  `ToCategory`-Zeile dieser Buchung als `Guid`, Wert: ihr Gruppenname).
- `src/Server/Ledger/QueryHandlers.fs`: `ReadModel.toEntryView` füllt die Map aus
  demselben `categoryGroups: Map<Guid, string>`-Parameter, der dort schon
  `CategoryGroupName` für die Kopf-Kategorie liefert. Für jede `ToCategory`-Zeile in
  `entry.Splits` wird die Kategorie-Id nachgeschlagen; findet der Lookup eine Gruppe,
  bekommt sie einen Eintrag, sonst bleibt die Id weg (kein Platzhalter-Eintrag, kein
  `None`-Wert in der Map). `ToTransfer`- und `Uncategorized`-Zeilen erzeugen nie einen
  Eintrag.
- **Keine Änderung** an `SplitTarget`, `TransactionSplit`, `Events.fs` (`encodeSplitTarget`/
  `decodeSplitTarget`, `encodeSplit`/`decodeSplit`), keine Migration, keine neue Abfrage:
  `loadCategoryGroups` wird für die Seite ohnehin schon geladen (ADR 0039 Punkt 5).

### Gemeinsame Hilfsfunktion (ADR 0045 Punkt 4)
- Eine reine Hilfsfunktion `qualify (groupName: string option) (leafName: string) :
  string`, die die ": "-Zusammensetzung bündelt (kein erfundener Gruppenname, kein
  führendes ": ", leerer Blattname nach `Trim()` bleibt unqualifiziert). `src/Client/
  Components/Ledger/View.fs`s `categoryLabelQualified` wird darauf umgestellt (reines
  Refactoring, Verhalten unverändert, bestehende Tests müssen weiter grün sein) und die
  neue Split-Zeilen-Darstellung benutzt dieselbe Funktion.

### Frontend: Verdrahtung in `detailContent`
- `splitTargetLabel` bekommt die Map als ersten Parameter:
  `splitTargetLabel (groupNames: Map<Guid, string>) (split: TransactionSplit) : string`.
  `ToCategory (YnabCategoryId id, name)` ergibt `qualify (groupNames.TryFind id) name`;
  `ToTransfer` und `Uncategorized` behalten ihr Label (`Übertrag: <Konto>`, `kein Ziel`).
- `splitLineTexts (entry)` reicht `entry.SplitCategoryGroupNames` durch. `detailContent`
  und `Detail.splitLines` bleiben unverändert (die Paare tragen schon den fertigen Text).
- Die Feldzeile "Kategorie" (`detail-category`) und die Listenzeile
  (`categoryLabel`/`categoryLabelQualified`, "N Split-Zeilen") bleiben unverändert.
- Panel und Sheet brauchen keinen eigenen Code: beide rendern `detailContent`.
- Annahme: `Detail.splitLines` setzt `prop.key (target + amountText)`. Zwei Zeilen mit
  demselben qualifizierten Text und Betrag wären auch heute schon ein Key-Duplikat; das
  Verhalten ändert sich durch die Qualifizierung nicht und gehört nicht in diese Task.

### Test-Literale (neues Pflichtfeld in `LedgerEntryView`)
`SplitCategoryGroupNames` ist ein Server→Client-Feld, ein Null-Fallback für die
Remoting-Deserialisierung ist nicht nötig (der Server füllt es immer, eine leere Map bei
Buchungen ohne Splits). Voll ausgeschriebene Record-Literale brauchen aber
`SplitCategoryGroupNames = Map.empty` in diesen Dateien (Fundstellen mit
`CategoryGroupName = ...` im Literal):
- `src/Tests/LedgerSelectionTests.fs` (`entryOf`, von Detail-, Keyboard- und
  ReviewMarkBatchClient-Tests mitbenutzt)
- `src/Tests/LedgerPagingTests.fs` (`entryView`)
- `src/Tests/LedgerFilterButtonTests.fs` (`view`)
- `src/Tests/LedgerViewTests.fs` (`entryView`)
- `src/Tests/LedgerSortClientTests.fs` (`entryOn`)
- `src/Tests/LedgerReviewMarkClientTests.fs` (`entryView`)
`LedgerPageTests`, `LedgerReviewMarkTests` und `LedgerDetailTests` bauen über `toEntryView`
oder `{ ... with }` und brauchen keine Änderung. Gegenprobe: `grep -rn "IsTransfer = false"
src/Tests` sollte genau diese sechs Dateien liefern.

## Acceptance criteria
- [ ] `LedgerEntryView.SplitCategoryGroupNames` enthält für eine Buchung mit zwei
      `ToCategory`-Split-Zeilen in bekannten, unterschiedlichen Gruppen genau zwei
      Einträge, je mit dem korrekten Gruppennamen (Test in `QueryHandlers`/`ReadModel`,
      analog zu den bestehenden `CategoryGroupName`-Tests aus `ledger-h6ptn`).
- [ ] Eine `ToCategory`-Zeile, deren Kategorie nie gespiegelt wurde oder deren Gruppenname
      nach `Trim()` leer ist, erzeugt KEINEN Eintrag in der Map (nicht `None`, kein
      leerer String).
- [ ] `ToTransfer`- und `Uncategorized`-Zeilen derselben Buchung erzeugen nie einen
      Eintrag, auch wenn ihre jeweilige Id zufällig mit einer bekannten Kategorie-Id
      kollidieren könnte (Guid-Räume sind getrennt, Test zur Dokumentation des Verhaltens).
- [ ] `qualify` liefert "Gruppe: Blatt" bei bekannter, nicht leerer Gruppe, sonst
      unverändert den Blattnamen; ein leerer Blattname wird nie qualifiziert (kein
      "Gruppe: " ohne Blatt). `categoryLabelQualified`, umgestellt auf `qualify`, liefert
      für alle bestehenden `LedgerViewTests`-Fälle dasselbe Ergebnis wie vorher
      (Regressionstest: bestehende Tests bleiben grün, keine neuen Abweichungen).
- [ ] `splitLineTexts` (Test in `LedgerDetailTests`, neben dem bestehenden Split-Test): eine
      Buchung mit einer `ToCategory`-Zeile (Id in `SplitCategoryGroupNames`, Gruppe
      "Alltag", Name "Lebensmittel"), einer `ToCategory`-Zeile ohne Map-Eintrag, einer
      `ToTransfer`-Zeile und einer `Uncategorized`-Zeile liefert die Ziel-Texte
      "Alltag: Lebensmittel", den bloßen Blattnamen, "Übertrag: <Konto>" und "kein Ziel",
      mit unveränderten Beträgen. Der bestehende Test ohne Map (leere Map) bleibt grün.
- [ ] Panel und Sheet zeigen dieselben Texte: `detailContent` ist die einzige Stelle, die
      `splitLineTexts` aufruft (Quelltext-Test nach dem Muster der bestehenden
      `LedgerDetailTests`, die `detailContent` per `functionBody` prüfen).
- [ ] Die Listenzeile derselben Buchung zeigt weiterhin "N Split-Zeilen" und die
      Feldzeile "Kategorie" im Detail den Kopf-Wert (Regressionstest:
      `categoryLabel`/`categoryLabelQualified` für eine Buchung mit Splits ändern sich
      nicht, auch wenn `SplitCategoryGroupNames` gefüllt ist).
- [ ] Alle sechs oben genannten Test-Dateien kompilieren mit `SplitCategoryGroupNames = Map.empty`.
- [ ] `dotnet build` und `dotnet test` grün.
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; ein neuer DS-Baustein erscheint in
      `/styleguide` (`src/Client/Components/Styleguide/View.fs`); `npm run guard:tokens` grün
      (Wächter aus `design-system-wfhxj`, sobald integriert).
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Notes
Abgetrennt am 2026-09-25 bei der Verfeinerung von `ledger-h6ptn`. Von `type: decision` zu
`type: feature` umgewandelt und modelliert am 2026-10-02 (tactical-modeler-Konsultation
durch den orchestrator); Entscheidung und Schnitt in ADR 0045. `ledger-q1hw2` ist seit
2026-10-24 fertig, deshalb ist das `depends_on` darauf entfallen und die Task bereit für todo.

Server-seitig gibt es eine zweite Zusammensetzung von "Gruppe: Name"
(`Ledger.Persistence.qualifiedCategoryName`, Sortierschlüssel, ADR 0051). Sie bleibt außerhalb
dieser Task; `qualify` lebt im Client (`View.fs`), wie ADR 0045 Punkt 4 es festlegt.

Verfeinert am 2026-10-24 (Sammel-Modeling): Befund gegen den gebauten Code nachgezogen
(`splitTargetLabel`/`splitLineTexts`/`detailContent` statt "Wo genau q1hw2 rendert"),
`depends_on` auf `design-system-001` reduziert, Acceptance criteria auf `splitLineTexts`
und das gemeinsame `detailContent` (Panel und Sheet) zugeschnitten, betroffene
Test-Literale aufgelistet. Kein ADR-Nachtrag nötig.

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).
