---
id: ledger-d218j
title: Interne YNAB-Kategorien in der Kategorieauswahl ausblenden, Ready to Assign behalten
status: doing
type: feature
context: ledger
created: 2026-10-19
completed:
depends_on: []
blocks: []
tags: [captured, kassenbuch, kategorie]
related_adrs: [0048]
related_research: []
prior_art: []
---

## Why
In der Kategorieauswahl der Kassenbuch-Filterleiste erscheint die YNAB-Gruppe "Internal
Master Category" mit "Inflow: Ready to Assign", "Deferred Income SubCategory" und
"Uncategorized". Die beiden letzten sind YNAB-Interna, die Roman nie filtern will; sie
machen die Auswahl länger und verwirren. Screenshot von Roman am 2026-10-19.

## What
Entscheidung Roman (2026-10-19): "Deferred Income SubCategory" und "Uncategorized" aus
der Kategorieauswahl ausblenden, "Inflow: Ready to Assign" bleibt wählbar, weil Einnahmen
dorthin gehen.

Geklärt bei der Verfeinerung (2026-10-19):

- **Erkennung:** über die Gruppe. `src/Shared/Domain.fs` kennt schon
  `internalMasterGroupName = "Internal Master Category"` und
  `readyToAssignCategoryName = "Inflow: Ready to Assign"` (dort für `applyReadyToAssign`
  benutzt, mit Test gegen eine gleichnamige Kategorie in fremder Gruppe). Regel: eine
  Kategorie der internen Gruppe ist nur dann wählbar, wenn sie Ready to Assign ist. Keine
  Namensliste der auszublendenden Kategorien; was YNAB künftig in die interne Gruppe legt,
  fällt automatisch heraus.
- **Ort:** an der API-Grenze, im Endpunkt `getCategories` (`src/Server/Api.fs` um Zeile
  322), direkt nach `applyReadyToAssign`. Dieser Endpunkt beliefert alle drei Auswahlen
  (Kassenbuch-Filterleiste, Regeln, Import prüfen inkl. Aufteilen-Sheet), dort ist keine der
  beiden Kategorien sinnvoll wählbar. Die server-internen Aufrufe von
  `YnabClient.getCategories` (Api.fs um Zeile 414, 532, 608, 977, Namensauflösung) bleiben
  ungefiltert, damit eine Auflösung nie an einer ausgeblendeten Id scheitert.
- **Funktion:** eine reine Funktion in `src/Shared/Domain.fs` neben `applyReadyToAssign`,
  etwa `withoutInternalCategories : YnabCategory list -> YnabCategory list`.
- Buchungen, die in YNAB auf "Uncategorized" stehen, deckt der Chip "Ohne Kategorie"
  bereits ab (ADR 0048); das bleibt so. `rules.yml` verweist auf keine der beiden
  Kategorien (geprüft 2026-10-19).

## Acceptance criteria
- [ ] Unit-Test der reinen Funktion: aus einer Liste mit "Inflow: Ready to Assign",
      "Uncategorized" und "Deferred Income SubCategory" (alle Gruppe "Internal Master
      Category") plus einer normalen Kategorie bleiben genau Ready to Assign und die
      normale Kategorie übrig, Reihenfolge unverändert.
- [ ] Unit-Test: eine Kategorie "Uncategorized" in einer anderen Gruppe als der internen
      bleibt erhalten (Erkennung über die Gruppe, nicht über den Namen allein).
- [ ] Der Endpunkt `getCategories` wendet die Funktion nach `applyReadyToAssign` an;
      Ready to Assign trägt weiterhin den überschriebenen Monatswert.
- [ ] Die server-internen `YnabClient.getCategories`-Aufrufe sind unverändert ungefiltert.
- [ ] `dotnet build` (0 Warnungen) und `dotnet test` grün.
- [ ] In der Kassenbuch-Kategorieauswahl erscheinen unter "Internal Master Category" nur
      noch "Inflow: Ready to Assign". [human-eye]

## Notes
Captured on 2026-10-19 aus dem Gespräch zu Romans Screenshot des Kategorie-Popovers.
Die Gestaltung des Popovers selbst
(Schriftgröße, Betragsfarben, Knopf) gehört nicht hierher, sondern zur Bestandsaufnahme
gegen ADR 0049.

Verfeinert am 2026-10-19 (Suggestor-Modus, Roman hat "mach so viele sinnvoll sind"
gesagt): Ort und Erkennung sind Vorschläge aus der Codelage. Falls die Regeln- oder
Import-Auswahl die Kategorien doch zeigen soll, den Filter in den Ledger-Client verlegen.
