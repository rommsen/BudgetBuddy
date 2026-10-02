---
id: ledger-b7qmx
title: "Split-Zeilen-Kategorie im Buchungs-Detail voll qualifizieren (ADR 0045)"
status: backlog
type: feature
context: ledger
created: 2026-09-25
depends_on: [ledger-q1hw2, design-system-001]
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

Befund bei der Verfeinerung: ein Bereich, der eine Split-Zeile im `ledger`-Client
überhaupt einzeln anzeigt, existiert heute noch nicht. `OpenHistory`/`historySheet` zeigt
nur sprechende Sätze aus `getEntryHistory`; `entry.Splits` wird im `ledger`-Client aktuell
nur gezählt. Der tatsächlich existierende Split-Editor mit Zeilen-Picker
(`src/Client/Components/SyncFlow/Views/SplitSheet.fs`, `targetLabel`) gehört zur Bounded
Context `ynab-sync`, nicht zu `ledger`, und ist nicht Teil dieser Aufgabe (ADR 0045 Punkt
6). Das für `ledger` geplante Lesen-Detail (BottomSheet am Handy, Seitenpanel am PC,
"Kategorie oder Splits" lesend) ist `ledger-q1hw2`, noch offen. Deshalb hängt diese
Aufgabe an `ledger-q1hw2`: die Server-/Shared-Erweiterung und ihre Verdrahtung in die
Darstellung gehören in eine Umsetzung, sobald es dort tatsächlich etwas zu qualifizieren
gibt (ADR 0045 Punkt 5, gegen die Alternative, ein Feld ohne Konsumenten vorzuziehen).

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

### Frontend: Verdrahtung in `ledger-q1hw2`s Detail-Darstellung
- Wo genau `ledger-q1hw2` die Split-Zeilen rendert (BottomSheet am Handy, Seitenpanel am
  PC): für jede `ToCategory`-Zeile `qualify (entry.SplitCategoryGroupNames.TryFind
  categoryId) categoryName` anzeigen; `ToTransfer`- und `Uncategorized`-Zeilen behalten
  ihr bisheriges Label unverändert.

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
- [ ] Im Detail-Panel (`ledger-q1hw2`) erscheint eine Split-Zeile mit bekannter Gruppe als
      "Gruppe: Kategorie"; eine Split-Zeile ohne bekannte Gruppe nur mit dem Blattnamen;
      eine `ToTransfer`-Zeile und eine `Uncategorized`-Zeile unverändert mit ihrem
      bisherigen Label. Die Listenzeile derselben Buchung zeigt weiterhin unverändert
      "N Split-Zeilen" (Regressionstest: `categoryLabel`/`categoryLabelQualified` für die
      Listenzeile ändern sich nicht).
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Abgetrennt am 2026-09-25 bei der Verfeinerung von `ledger-h6ptn`. Von `type: decision` zu
`type: feature` umgewandelt und modelliert am 2026-10-02 (tactical-modeler-Konsultation
durch den orchestrator); Entscheidung und Schnitt in ADR 0045. Bewusst **kein** separater
Folge-Task: `depends_on: [ledger-q1hw2, design-system-001]` sorgt dafür, dass diese Aufgabe
erst beginnt, wenn es im `ledger`-Client tatsächlich eine Stelle gibt, die eine Split-Zeile
einzeln rendert.