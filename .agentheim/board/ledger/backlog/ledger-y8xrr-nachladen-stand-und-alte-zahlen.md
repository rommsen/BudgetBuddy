---
id: ledger-y8xrr
title: "Nachladen: Liste verliert ihren Stand, alte Zahlen stehen ohne Hinweis"
status: backlog
type: refactor
context: ledger
created: 2026-11-19
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G3, von Roman zur Task gewählt. Daten im Alltag falsch oder veraltet angezeigt (Available, Kontostände), Auswahl und Scrollstand gehen verloren; höchste Schwere mittel. Verweis: ledger-k4n2s Zeile 76.

## What
Alle Befunde der Gruppe in Befundform, wörtlich aus dem Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`:

### G3 Nachladen: Liste verliert ihren Stand, alte Zahlen stehen ohne Hinweis

**Kern:** Mehrere Lade- und Nachladewege verwerfen Auswahl und Scrollstand, überschreiben frische Werte mit alten (Available) oder verschlucken ein Scheitern, sodass veraltete Kontostände ohne Hinweis stehen.

**Höchste Schwere:** mittel · **Befunde:** 10 · **BCs:** ledger

**Verweis:** Teilweise schon in der Kann-Sammlung ledger-k4n2s Zeile 76 (ledger-ct3cx).

- src/Client/Components/Ledger/State.fs:3510 | README „Nachladen im Hintergrund“ (ledger-ct3cx), Kommentar `rebuildEntries` State.fs:199 | mittel | Nach Buchung anlegen, Verwerfen (3542), Löschen (3574), Wiederherstellen (3601) und „Geprüft“ für die Auswahl (3992) baut `LoadEntries` neu auf (Auswahl, Seiten, Scrollstand weg); einheitlich `RefreshEntries`.
- src/Client/Components/Ledger/State.fs:1997 | standards/frontend/overview.md „Späte Antworten“; README „Available frisch halten“ | mittel | `CategoriesLoaded` ohne Lade-Nummer; ein älterer Abruf überschreibt die Fortschreibung aus `shiftAvailable` mit altem Available. `CategoriesGeneration` einführen.
- src/Client/Components/Ledger/State.fs:1650 | ledger/README.md „Kontostand“ | mittel | `LoadBalances` lässt `Success` stehen, `lateFailure` schluckt jedes spätere Scheitern; nach gescheitertem Nachladen zeigt die Leiste alte Stände ohne Hinweis. Scheitern sichtbar machen, Annahme in `RemoteData.lateFailure` (Types.fs:48) stimmt nicht mehr.
- src/Client/Components/Ledger/State.fs:1923 | overview.md „Späte Antworten“ | niedrig | `LoadMirrorStatus` setzt `Loading`, „Gespiegelt …“ verschwindet kurz; `Error` überschreibt vorhandenen Stand. `Success` stehen lassen.
- src/Client/State.fs:403 | standards/frontend/overview.md „Späte Antworten“/Entwürfe; ADR 0095 | mittel | `isInputActive` vergleicht `AmountMinDraft`/`AmountMaxDraft` als Text mit `formatAmountDraft`; ein getipptes „12,50“ bleibt nach dem Commit „ungleich 12.5“ und hält den Update-Neustart (ADR 0057) zurück. Werte über `parseAmountFilterText` vergleichen.
- src/Client/Components/Ledger/State.fs:3632 | .claude/rules/elmish-state.md, doppelte Wege | mittel | `QuickAddAccountId` über eigenen `getSettings`-Aufruf, obwohl Root `Settings` und `LedgerBookingDefaults.QuickAddAccountId` denselben Wert liefern; Fehler wird geschluckt (3643). Eine Quelle, als `RemoteData`.
- src/Client/Components/Ledger/Types.fs:2564 | elmish-state.md (Antworten tragen `Result`) | mittel | `CapturePayeeCategoryLoaded`, `ReviewSuggestionsLoaded` (2775), `ReviewMatchLoaded` (2778) ohne `Result`, Fehler wird im `Cmd` geschluckt; an `CategorySuggestionsLoaded` angleichen.
- src/Client/Components/Ledger/State.fs:2005 | Types.fs `RemoteData.lateFailure` | niedrig | `CategoriesLoaded (Error)` baut `lateFailure` inline nach; Helfer nehmen.
- src/Client/Components/Ledger/State.fs:1991 | keine toten Pfade | niedrig | Zweige `LoadCategories`, `EnsureCategoriesFresh` (2014) unerreichbar (Root fängt ab, State.fs:813, 819); entfernen.
- src/Client/Components/Ledger/Types.fs:1432 | elmish-state.md „Kein IsLoading: bool“ | niedrig | `LoadingMore`, `CategoriesRefreshing` (1516), `MirrorRunning` (1491) neben `RemoteData` desselben Abrufs.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-19, raw, unrefined. Bewusst groß; kleiner geschnitten wird im Refinement (`agentheim:modeling`).
