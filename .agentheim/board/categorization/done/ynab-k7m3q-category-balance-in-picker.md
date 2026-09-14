---
id: ynab-k7m3q
title: Aktuellen Budgetwert (balance) hinter Kategorienamen im Picker anzeigen
status: done
type: feature
context: categorization
created: 2026-06-27
completed: 2026-06-28
depends_on: [design-system-001]
blocks: []
tags: [frontend]
related_adrs: [0005, 0011]
related_research: []
prior_art: []
---

## Why
Beim Zuordnen einer Kategorie im Import soll Roman direkt sehen, wie viel in einer
Kategorie aktuell noch verfügbar ist (YNAB "Available"), ohne in YNAB-Web nachzuschauen.
Das macht die Entscheidung "passt diese Buchung hier rein" am Punkt der Zuordnung
treffbar.

## What
Im Kategorie-Picker hinter dem Kategorienamen ("Gruppe: Name") den aktuellen
Budgetwert anzeigen: YNAB `balance` der Kategorie (= Available des aktuellen Monats,
in Milliunits geliefert). Als farbcodierter Geldbetrag, rechtsbündig, im Money-/Mono-Stil.

Entschieden bei der Refinement-Runde (2026-06-27):
- **Wert:** `balance` (Available aktueller Monat), nicht `budgeted`/`activity`.
- **Optik:** farbiger Betrag rechtsbündig pro Picker-Zeile. Grün bei >0 (`text-success`),
  rot bei <0 (`text-danger`), neutral bei =0. Kein Fortschrittsbalken, kein Wert auf dem
  eingeklappten Chip.
- **Scope:** sichtbar im Import-Zuweisungs-Picker (TransactionList) und im
  Split-Sheet-Picker (SplitSheet). **Nicht** im Regel-Editor (Rules/View), da dort ein
  "aktuell verfügbar" für eine Config-Regel kontextlos wäre.

## Acceptance criteria
- [x] `YnabCategory` (`src/Shared/Domain.fs:354`) trägt das aktuelle Available als
      Geldwert (z.B. `Available: Money`), dekodiert aus YNABs `balance` (Milliunits / 1000,
      analog zur bestehenden Account-Balance-Konvertierung).
- [x] Die Kategorie-Decoder in `src/Server/YnabClient.fs` lesen `balance` und reichen den
      Wert durch: `categoryDecoder` (:41), `categoryInGroupDecoder` (:58) und der
      Budget-Detail-Pfad (`budgetDetailDecoder`). Kein zusätzlicher YNAB-Request — der Wert
      kommt aus der bestehenden `getCategories`-Antwort (`GET /budgets/{id}/categories`).
- [x] Der Picker zeigt pro Zeile rechtsbündig den farbcodierten Available-Betrag hinter
      "Gruppe: Name": grün bei >0, rot bei <0, neutral bei =0; Mono-Font im Money-Stil,
      ausschließlich über DS-Tokens (kein Drift, styleguide-konform).
- [x] Sichtbar im Import-Zuweisungs-Picker (`TransactionList.fs:178-182`) und im
      Split-Sheet-Picker (`SplitSheet.fs`). Der Regel-Editor (`Components/Rules/View.fs`)
      bleibt unverändert (nutzt den Picker nicht).
- [x] Der Picker bekommt einen reicheren Option-Typ als `(string * string)`, der den Wert
      mitträgt; `BottomSheet.categoryPicker` / `categoryPickerLayered` /
      `CategoryPickerInternal` (`src/Client/DesignSystem/BottomSheet.fs`) rendern ihn.
      Das Sheet-Stacking des Split-Pickers folgt weiter ADR 0005 §4.
- [x] Decoder-Regressionstest: `balance` wird korrekt aus Milliunits konvertiert, geprüft
      für positiven, negativen und Null-Wert (Bug-Fix-Protokoll: jeder Wert ein Assert).
- [x] `dotnet build` und `dotnet test` grün.

## Notes
Refined via `modeling` on 2026-06-27 (REFINE + PROMOTE → todo). Ursprünglich via
`quick-capture` erfasst, dann von ynab-sync nach categorization re-routet.

**Cross-BC:** Der Anzeige-Ort (Zuweisungs-Dialog / Picker) gehört zu `categorization`,
die Datenseite (YNAB-Mirror der `YnabCategory` + Decoder) berührt `ynab-sync`. Die Task ist
hier gefilt, weil der spürbare Effekt im Picker liegt; die `ynab`-Präfix-ID bleibt stabil.

**Datenfrische:** Der Wert ist so aktuell wie der letzte Kategorien-Load
(`model.Categories`, RemoteData) — kein Live-Refresh pro Zeile. Für die Zuordnung beim
Import ausreichend.

**Edge cases (Default, nicht extra zu bauen):** Den `balance` so anzeigen, wie YNAB ihn
liefert; keine Sonderbehandlung für versteckte Kategorien o.ä. Währung wie bisher (EUR-
Formatierung über den bestehenden Money-Pfad).

**Prior art (gleicher Picker, anderer Aspekt):**
`categorization/done/2026-06-11-mobile-category-picker-keyboard-ghostclick.md` — Mobile-
Verhalten des Kategorie-Pickers (keyboard-aware, ghost-click-frei). Beim Anfassen von
`CategoryPickerInternal` nicht regressen.

**Styleguide-Gate:** `design-system-001` (done) ist die Referenz; die neue Picker-Zeile
muss gegen `standards/frontend/styleguide.md` gemessen werden (Farbsemantik grün=Erfolg /
rot=Gefahr, Mono für Zahlen).

## Outcome
`YnabCategory` trägt jetzt `Available: Money`, dekodiert aus YNABs `balance`
(Milliunits/1000) im neuen `categoryAvailable`-Helper, der von `categoryDecoder` und
`categoryInGroupDecoder` (und damit dem Budget-Detail-Pfad) genutzt wird. Decode ist
**Optional mit 0-Default** (Conformist-safe — ein fehlendes `balance` bricht nicht den
ganzen Load; bewusste Abweichung von der Required Account-Balance, siehe ADR 0011). Kein
zusätzlicher YNAB-Request.

Der Picker bekam einen öffentlichen Option-Typ `BottomSheet.CategoryPickerOption
{ Id; Label; Available: Money option }` statt `(string * string)`. Pro Zeile rendert er
rechtsbündig `Money.available` (neue DS-Funktion: mono, drei-wertige Farbe grün>0 /
rot<0 / neutral=0 über DS-Tokens `text-neon-green`/`text-neon-red`/`text-text-muted`, kein
Glow, kein führendes "+"). Click-Commit und Layer-Stacking aus ADR 0005 sind unberührt.

Sichtbar in allen drei Zuweisungs-Pickern, die die Komponente teilen: Import
(`TransactionList`), Split (`SplitSheet`) und Quick-Add (`QuickAddPage`). Quick-Add wurde
bewusst einbezogen (gleiche Komponente + Daten + Zuweisungskontext; den Wert dort zu
unterdrücken wäre der Sonderfall — siehe ADR 0011 §4). Der Regel-Editor nutzt den Picker
nicht und bleibt unverändert.

Schlüsseldateien: `src/Shared/Domain.fs`, `src/Server/YnabClient.fs`,
`src/Client/DesignSystem/Money.fs`, `src/Client/DesignSystem/BottomSheet.fs`,
`src/Client/Components/SyncFlow/Views/TransactionList.fs`, `.../SplitSheet.fs`,
`src/Client/Views/QuickAddPage.fs`, `src/Tests/YnabClientTests.fs`.

Verifikation: `dotnet build` 0 Fehler; `dotnet test` 625 passed / 6 skipped / 0 failed
(+5 Tests vs. ~620-Baseline); `npm run build` (Fable) kompiliert den Client.

Entscheidung dokumentiert in ADR 0011
(`.agentheim/knowledge/decisions/0011-category-available-in-assignment-pickers.md`).
