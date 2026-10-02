---
id: 0045
title: Split-Zeilen-Kategorie wird im Detail voll qualifiziert, über denselben Lookup wie die Kopf-Kategorie (ADR 0039 fortgeschrieben)
scope: ledger
status: accepted
date: 2026-10-02
supersedes: []
superseded_by: []
related_tasks: [ledger-b7qmx, ledger-h6ptn, ledger-q1hw2]
related_adrs: [0013, 0039]
related_research: []
---

# ADR 0045: Split-Zeilen-Kategorie im Detail voll qualifiziert

## Context

ADR 0039 hat die Kopf-Kategorie einer Buchung im Lesepfad voll qualifiziert ("Alltag:
Lebensmittel" statt "Lebensmittel", `categoryLabelQualified`, PC-Tabelle) und dabei
ausdrücklich offengelassen, ob dieselbe Qualifizierung auch für eine einzelne Split-Zeile
(`SplitTarget.ToCategory`) gilt. ADR 0039, Positive-Liste: "Derselbe Lookup kann später die
Split-Zeilen qualifizieren, ohne ein zweites Mal Codec-Arbeit."

Roman hat entschieden: im Detail einer Buchung soll eine Split-Zeilen-Kategorie voll
qualifiziert erscheinen, genau wie die Kopf-Kategorie in der PC-Tabelle. Die Listenzeile
selbst bleibt unverändert bei "N Split-Zeilen" (`categoryLabel`/`categoryLabelQualified`,
`src/Client/Components/Ledger/View.fs`): sie fasst ohnehin mehrere Ziele zusammen, eine
Qualifizierung dort wäre nicht sinnvoll lesbar.

Befund bei der Verfeinerung: ein Bereich, der eine Split-Zeile im `ledger`-Client überhaupt
einzeln anzeigt, existiert heute noch nicht. `OpenHistory`/`historySheet` zeigt nur
sprechende Sätze aus `getEntryHistory`, `entry.Splits` wird im `ledger`-Client aktuell nur
gezählt. Der tatsächlich existierende Split-Editor mit Zeilen-Picker
(`src/Client/Components/SyncFlow/Views/SplitSheet.fs`, `targetLabel`) gehört zur Bounded
Context `ynab-sync`, nicht zu `ledger` (ein anderer Kontext, eine andere Task-Quelle, hier
nicht betroffen). Das für `ledger` geplante Lesen-Detail (BottomSheet am Handy, Seitenpanel am
PC, "Kategorie oder Splits" lesend, Bearbeiten erst v2) ist der offene Backlog-Task
`ledger-q1hw2`.

## Decision

1. **Derselbe Lookup, keine neue Abfrage, keine Migration.** Die Qualifizierung einer
   Split-Zeilen-Kategorie benutzt exakt denselben Lookup wie ADR 0039: `Guid ->
   Gruppenname option` aus `Ledger.Persistence.loadCategoryGroups`, serverseitig je Seite
   ohnehin schon geladen (ADR 0039, Punkt 5). Keine neue Tabelle, keine neue
   SQLite-Abfrage, kein zweiter Request an YNAB.
2. **`SplitTarget`, `TransactionSplit` und `Events.fs` bleiben unverändert.** Beide Typen
   sind Teil der Event-Payloads (`encodeSplitTarget`/`decodeSplitTarget`,
   `encodeSplit`/`decodeSplit`); ein neues Feld dort würde den Baustein anfassen, den ADR
   0039 Punkt 7 (Regel für neue Felder im Ereignis-Log) ausdrücklich auf "ehrlich optionale"
   Felder beschränkt, und die Gruppe ist wie bei der Kopf-Kategorie Stammdatum, kein Zustand
   der Zeile, sie gehört nach ADR 0039 Punkt 1 ohnehin nicht ins Log.
3. **`LedgerEntryView` (reines Read-Model-DTO, kein Event) bekommt ein neues Feld
   `SplitCategoryGroupNames: Map<Guid, string>`**, Schlüssel ist die `YnabCategoryId` einer
   `ToCategory`-Zeile dieser Buchung (als `Guid`), Wert ihr Gruppenname. Gefüllt in
   `Ledger.QueryHandlers.ReadModel.toEntryView` aus demselben `categoryGroups`-Parameter, der
   dort schon `CategoryGroupName` für die Kopf-Kategorie füllt: für jede `ToCategory`-Zeile
   in `entry.Splits` wird die Kategorie-Id nachgeschlagen; findet der Lookup eine Gruppe,
   bekommt sie einen Eintrag in der Map, sonst bleibt die Id weg (kein Platzhalter-Eintrag,
   kein `None`-Wert in der Map). `ToTransfer`- und `Uncategorized`-Zeilen erzeugen nie einen
   Eintrag. Eine Map statt eines eigenen Typs je Split-Zeile: dieselbe Kategorie in zwei
   Zeilen einer Buchung hat genau eine Gruppe (Stammdatum der Kategorie, nicht der Zeile,
   ADR 0039 Punkt 1), ein Schlüssel nach Kategorie-Id bildet das direkt ab, ohne
   `TransactionSplit` zu duplizieren oder seinen Vertrag zu ändern. Fable.Remoting
   serialisiert `Map<Guid, string>` wie jeden anderen Record-Feld-Typ im Vertrag.
4. **Zusammengesetzt wird erst in der Anzeige, nach derselben Regel wie ADR 0039 Punkt 5.**
   Eine gemeinsame reine Hilfsfunktion (z. B. `qualify (groupName: string option) (leafName:
   string) : string`) bündelt die ": "-Zusammensetzung; `categoryLabelQualified` (Kopf) und
   das künftige Split-Zeilen-Label in `ledger-q1hw2`s Detail-Darstellung benutzen dieselbe
   Funktion, damit die Fallback-Regel nicht zweimal geschrieben wird. Für eine Split-Zeile:
   fehlt ihre Kategorie-Id in `SplitCategoryGroupNames` (nie gespiegelt, in YNAB gelöscht,
   oder die Zeile ist `ToTransfer`/`Uncategorized`), oder ist der Gruppenname nach `Trim()`
   leer, bleibt es beim Blattnamen der Zeile (`categoryName` aus `ToCategory`, bzw. dem
   bestehenden Label für `ToTransfer`/`Uncategorized`): nie ein führendes ": " und nie ein
   erfundener Gruppenname, exakt wie bei der Kopf-Kategorie. Trägt eine Zeile einen leeren
   Blattnamen (YNAB liefert manchmal kein `category_name`, `observationDecoder` schreibt dann
   `""`), gilt dieselbe Rückfallregel wie am Kopf: keine Qualifizierung eines leeren
   Blattnamens, kein "Gruppe: " ohne Blatt.
5. **Scope: dieselbe Task, kein separater Folge-Task, aber abhängig von `ledger-q1hw2`.**
   Da im `ledger`-Client heute keine Stelle existiert, die eine Split-Zeile einzeln rendert,
   bekommt `ledger-b7qmx` (umgewandelt zum Implementierungs-Task) `depends_on:
   [ledger-q1hw2, design-system-001]`. Die Server-/Shared-Erweiterung (Punkt 3) UND ihre
   Verdrahtung in das Detail-Panel (Punkt 4) gehören in **eine** Umsetzung, sobald
   `ledger-q1hw2` das Detail-Panel liefert, nicht in zwei getrennte Tasks, von denen der
   eine ein ungenutztes Feld ausliefert. Damit entsteht nie ein Zustand, in dem
   `SplitCategoryGroupNames` existiert, aber kein Code sie liest.
6. **Keine Konsequenz für `ynab-sync`.** `SplitSheet.fs`s `targetLabel` zeigt heute nur den
   Blattnamen einer gewählten Split-Kategorie, arbeitet aber gegen den YNAB-Katalog
   (`getCategories`, `YnabCategory.GroupName` liegt dort bereits vor): ein anderer Bounded
   Context, eine andere Datenquelle, kein Bezug zu `loadCategoryGroups` oder dieser ADR.
   Wenn Roman dieselbe Darstellung dort will, ist das ein eigenes Item in `ynab-sync`, nicht
   Teil dieser Entscheidung.

## Consequences

### Positive
- Kein Eingriff in `Events.fs`, keine Migration, keine neue Abfrage: die gesamte
  Erweiterung lebt im Lesepfad, exakt in der von ADR 0039 vorgesehenen Nahtstelle.
- Dieselbe Fallback-Regel (kein erfundener Gruppenname, kein führendes ": ") gilt jetzt an
  genau einer Stelle (`qualify`) für Kopf- und Split-Zeilen-Kategorie, statt zweimal
  geschrieben zu werden.
- Kein Feld ohne Konsumenten: die Erweiterung entsteht zusammen mit ihrer einzigen
  Verwendungsstelle.

### Negative
- `ledger-b7qmx` kann erst beginnen, wenn `ledger-q1hw2` (Detail-Panel) fertig ist; die
  Entscheidung selbst (diese ADR) ist davon unabhängig schon getroffen.
- `LedgerEntryView` wächst um ein weiteres Feld, das für jede Buchung ohne Splits leer bleibt
  (geringer, aber wiederkehrender Leerlast-Anteil in jeder Seiten-Antwort).

### Neutral
- Umkehrbar: stellt sich heraus, dass die Gruppe je Split-Zeile doch direkt an der Zeile
  (statt über eine Map nach Kategorie-Id) gebraucht wird, lässt sich das nachträglich ändern,
  ohne das Log oder ADR 0039 zu berühren.

## Alternatives considered

**`SplitTarget`/`TransactionSplit` um ein `GroupName`-Feld erweitern.** Würde Events.fs
anfassen (beide Typen sind Teil der Payload-Codecs) und liefe der Trennung aus ADR 0039
Punkt 1 zuwider (Gruppe ist Stammdatum, kein Ereignis-Feld). Abgelehnt.

**Eigener Typ `SplitLineView` mit bereits qualifiziertem Label statt einer Map.** Würde
`Splits: TransactionSplit list` im Vertrag verdoppeln oder ersetzen; jeder heutige Konsument
(z. B. `categoryLabel`s Zähl-Logik) müsste nachgezogen werden. Die additive Map ist der
kleinere Schnitt. Abgelehnt.

**`ledger-b7qmx` sofort als reine Server-/Shared-Erweiterung ohne `depends_on` auf
`ledger-q1hw2` ausliefern.** Schnell machbar, aber das Feld hätte keinen Aufrufer und keine
geprüfte Darstellung, bis `ledger-q1hw2` kommt: das Risiko eines falsch geschnittenen Felds
(z. B. Map vs. Zeilen-Struktur) steigt, wenn niemand es tatsächlich konsumiert, bevor es
feststeht. Abgelehnt zugunsten von Punkt 5.

**Gruppe im Client gegen den YNAB-Katalog auflösen** (den die Kategorienauswahl, `ledger-j8xq4`,
ohnehin lädt), statt über `loadCategoryGroups`. Derselbe Gegenvorschlag, den ADR 0039 für die
Kopf-Kategorie schon abgelehnt hat (zweiphasige Beschriftung, zwei Quellen für dieselbe
Information). Für ein einmalig geöffnetes Detail wäre das Flackern-Argument schwächer, aber
"gleicher Lookup" war Romans ausdrückliche Vorgabe für diese Entscheidung. Abgelehnt.
