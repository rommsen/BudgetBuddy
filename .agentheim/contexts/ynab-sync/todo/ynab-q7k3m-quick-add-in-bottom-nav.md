---
id: ynab-q7k3m
title: Quick Add als eigene Seite, erreichbar aus der Haupt-Navigation
status: todo
type: feature
context: ynab-sync
created: 2026-06-27
completed:
depends_on: [design-system-001]
blocks: [ynab-t4n8p]
tags: [quick-add, navigation, routing, discoverability, ui]
related_adrs: [0003, 0004]
related_research: []
prior_art: [2026-06-11-quick-add-manual-entry, 2026-06-12-quick-add-feedback-round]
---

## Why
Quick Add (manuelle Bar-Buchung → YNAB) ist im Alltag schwer erreichbar. Die bisherigen
Einstiege sind beide an den Sync-Flow gebunden — ein Secondary-Button unter „Sync starten"
(`Views/StatusViews.fs:391`) und ein Plus-Icon im Review-Header (`SyncFlow/View.fs:68`).
Direkt nach einem erfolgreichen Import gibt es z. B. keinen Klickpfad zum Quick-Add-
Formular. Quick Add ist aber eine eigenständige, häufige Aktion und verdient einen festen,
kontextfreien Platz.

## What
Quick Add zu einer **eigenen Seite** mit eigener Route machen, erreichbar als eigener
Eintrag in der Haupt-Navigation (Bottom-Nav mobil / Top-Nav desktop). Die beiden
bisherigen sync-flow-gebundenen Einstiege werden **entfernt** — die Navigation wird der
einzige Weg zu Quick Add.

Heute ist Quick Add keine Seite, sondern ein `QuickAddFormState option` *innerhalb* der
SyncFlow-Komponente (`Components/SyncFlow/Types.fs:266`), geöffnet via `OpenQuickAdd`,
gerendert als BottomSheet. Diese Refaktorierung hebt den Quick-Add-State aus SyncFlow
heraus zu einer Top-Level-Seite.

## Acceptance criteria
- [ ] Neuer Page-Fall `QuickAdd` im Page-DU (`Types.fs:130-134`) mit Route in
      `parseUrl`/`toUrlSegments` (`Types.fs:142,151`) — direkt per URL erreichbar.
- [ ] Eigener Eintrag in der Haupt-Navigation (`DesignSystem/Navigation.fs:26-30`), sowohl
      in der Bottom-Nav (mobil) als auch der Top-Nav (desktop); Reihenfolge/Icon nach
      Styleguide, Aktiv-Zustand wie die übrigen Items.
- [ ] Quick Add ist von **jeder** Seite mit einem Tap erreichbar — insbesondere existiert
      direkt nach erfolgreichem Import ein Klickpfad.
- [ ] Quick-Add-Formular-State ist aus SyncFlow ins Top-Level-Model gehoben; Öffnen/
      Absenden/Schließen funktionieren von der neuen Seite aus unverändert (Push aufs
      Quick-Add-Konto, ADR 0004).
- [ ] Navigieren auf die Quick-Add-Seite lädt die nötigen Daten (Kategorien für den
      Picker, Quick-Add-Konto-Id) analog zu den anderen Seiten (`State.fs:118-135`).
- [ ] Die beiden alten Einstiege sind entfernt: `quickAddEntryButton`
      (`Views/StatusViews.fs:391`) und `quickAddHeaderButton` (`SyncFlow/View.fs:68`).
- [ ] `dotnet build` + Fable-Build grün; bestehende Quick-Add-Tests weiterhin grün.
- [ ] Folgt dem Styleguide (`design-system-001`).

## Notes
- **Page vs. Sheet:** Als eigene Seite bietet sich ein normales Seiten-Layout an
  (`Primitives.container` + Formular), das die bestehenden Feld-Komponenten aus
  `Views/QuickAdd.fs` wiederverwendet; der Category-Picker bleibt ein erhöhter Sheet-Layer
  (`categoryPickerLayered`). Variante mit weniger Umbau: die Seite zeigt das bestehende
  BottomSheet dauerhaft offen. Worker-Entscheidung — Tendenz: echtes Seiten-Layout.
- **Nav-Slot:** Bottom-Nav hat aktuell 3 Items (Sync, Rules, Settings); ein 4. Item passt.
  Icon-Wahl (Plus/Wallet) nach Styleguide.
- **State-Lift:** `QuickAddFormState` + die 5 Msg (`OpenQuickAdd`/`CloseQuickAdd`/
  `UpdateQuickAdd`/`SubmitQuickAdd`/`QuickAddSaved`, `SyncFlow/Types.fs:358-362`) wandern
  von SyncFlow ins Top-Level; die Submit-Logik (`SyncFlow/State.fs:1106`) bleibt inhaltlich
  gleich.
- **Keine ADR nötig** — Standard-Elmish-Umbau (Page-DU + Routing + State-Lift), keine
  architektonische Grundsatzentscheidung.
- Entsperrt `ynab-t4n8p` (Vorlagen rendern in genau diesem Formular) → erst die Seite,
  dann die Vorlagen, sonst Rework am Prefill-Ziel.
- Hängt am Styleguide-Gate (`design-system-001`, done).
