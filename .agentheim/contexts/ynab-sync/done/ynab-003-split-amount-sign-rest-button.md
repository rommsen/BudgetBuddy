---
id: ynab-003
title: Split-Editor — Vorzeichen-Fix + editierbare Beträge + Rest-Button
status: done                 # backlog | todo | doing | done
type: bug                    # feature | bug | refactor | chore | spike | decision
context: ynab-sync
created: 2026-06-16
completed: 2026-06-16
commit: 14e1f61
depends_on: [ynab-002]
blocks: []
tags: [split, cashback, bug, sign, review-ux, mobile]
related_adrs: [0004, 0005, 0006]
related_research: []
prior_art: [ynab-002]
---

## Why
Roman-Feedback aus dem Live-Betrieb (2026-06-16) zum frisch deployten Split (ynab-002):
1. **Bug — Vorzeichen.** Eine Ausgabe wird mit **negativem** Betrag geführt (Outflow, z. B.
   Gesamt −222,15). Der Cashback-„Rest"-Mechanismus rechnet `Total − Σ(erfasst)` im
   *signierten* Raum. Die UI zeigt ein `0,00`-Feld, Roman tippt naturgemäß **`200`** (positiv).
   Ergebnis: `−222,15 − (+200) = −422,15` → die Beträge **addieren** sich statt sich zu
   verrechnen. Erwartet: Kategorie wird zu 22,15.
2. **Bug — nicht editierbar.** Die Cashback-Kategoriezeile war die read-only „Rest"-Zeile;
   der Wert ist nicht von Hand korrigierbar — in Kombination mit Bug 1 doppelt ärgerlich.
3. **Enhancement.** In **beiden** Split-Modi ein Knopf, der den fehlenden Restbetrag
   automatisch auf eine Zeile legt, statt im Kopf zu rechnen.

**Warum die ynab-002-Tests das nicht fingen:** die Fixtures tippten *negative* Beträge
(`transferLine "Bargeld" "-200,00"` gegen `mkState -217.00m`) — sie kodierten dieselbe
falsche Annahme wie der Code (Nutzer tippt vorzeichenbehaftet). Der verifier lief grün, weil
das echte UI positive Magnituden präsentiert und der Nutzer positiv tippt. Lehre: Fixtures
müssen die **echte Eingabekonvention** (positive Magnitude) spiegeln.

## What
Das Betrags-Modell des Split-Editors (rein clientseitig, `SyncFlow`) umbauen:
- **Positive Magnituden:** Nutzer tippt positive Beträge; das Vorzeichen der Transaktion
  (`Total`) wird intern beim Bauen der `TransactionSplit`s angewandt → der signierte
  Zeilenbetrag = `sign(Total) * |Eingabe|`. Validierung/Push bleiben über die geteilten
  `mkSplits`/`splitRemainder` (ADR 0006), die Invariante wird **nicht** clientseitig
  reimplementiert; nur die Form→Domain-Adaption ändert sich.
- **Alle Beträge editierbar:** die „magische" read-only `AutoRemainder`-Zeile entfällt
  ersatzlos. Jede Zeile hat ein editierbares Betragsfeld.
- **Rest-Button pro Zeile:** setzt den Betrag der Zeile auf `|Total| − Σ(übrige Zeilen)`
  (Magnitude, ≥ 0), sodass die Summe stimmt. Gilt für Cashback-Shortcut UND generischen Editor.
- **Cashback-Shortcut:** befüllt weiter Kategorie (ursprüngliche) + Transfer-Ziel
  (Quick-Add-Konto, ADR 0004) als Targets, beide Beträge editierbar; Roman tippt den
  Bar-Betrag und tippt „Rest" auf der Kategorie (oder umgekehrt).

## Acceptance criteria
- [ ] **Regression (kaputt→fix):** Gesamt −222,15; Nutzer gibt **`200`** (positiv) auf der
      Transfer-Zeile ein, die Kategoriezeile bekommt per Rest-Button ihren Wert → committeter
      Kategoriebetrag = −22,15, Transfer = −200,00, signierte Summe = −222,15, `canSaveSplits`
      = true. (Test mit POSITIVER Eingabe gegen NEGATIVES Total.)
- [ ] **Intakt:** ein normaler N-Zeilen-Split (alle positiv getippt) gegen ein negatives Total
      validiert weiterhin korrekt; Save gesperrt bei Summe ≠ Total, frei bei Gleichheit.
- [ ] Die erste/jede Kategoriezeile ist editierbar (kein read-only „Rest"-Feld mehr).
- [ ] Rest-Button pro Zeile füllt `|Total| − Σ(übrige)` (≥ 0) als positiven Betrag; danach
      `unallocatedRemainder = 0` und (bei vollständigen Targets) Save frei — Test.
- [ ] Live-Rest-Anzeige zeigt die noch nicht zugeordnete Magnitude; balanciert bei 0 — Test.
- [ ] `AutoRemainder` aus `SplitDraftLine` + `autoRemainderAmount`/`draftLineToSplitWith`-
      Override entfernt; alle Aufrufstellen (State.fs, SplitSheet.fs, Tests) angepasst.
- [ ] `dotnet build` + `dotnet test` grün; Diary aktualisiert.

## Notes
**Load-bearing Code:**
- `src/Client/Components/SyncFlow/Types.fs` — `SplitDraftLine` (:6, Feld `AutoRemainder` raus),
  Helfer-Block (:142–224): `formatAmountForEdit` (positive Magnitude), `draftLineToSplit`
  (Vorzeichen aus Total), `committedSplits`/`validateSplitEdit`/`canSaveSplits`,
  neu `lineMagnitude`/`restMagnitudeForLine`/`unallocatedRemainder`; `Msg` neu `FillSplitRemainder of int`.
- `src/Client/Components/SyncFlow/State.fs` — `StartCashbackSplit` (:521, beide Zeilen
  editierbar, kein AutoRemainder), `StartSplitEdit` (:491), `AddSplitLine` (:570), neuer Handler
  `FillSplitRemainder`.
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs` — `splitLineRow` (:33, editierbares Feld
  + Rest-Button je Zeile), `splitSheet` (:99, kein `autoAmount`/`hasAutoLine`, Rest-Banner
  immer sichtbar).
- `src/Tests/SplitEditorTests.fs` — Fixtures auf **positive** Eingabe umstellen + Regression +
  Rest-Button-Tests.

**Vorzeichen-Modell:** `signed = (if Total.Amount < 0 then -1 else 1) * |parse(text)|`. Gilt
uniform für alle Zeilen (Cashback/Ausgabe ist einheitlich vorzeichig). Gemischt-vorzeichige
Splits (Erstattungszeile in einer Ausgabe) sind bewusst nicht im Scope — YNAB erlaubt sie,
aber sie sind selten; ggf. späteres Enhancement.

**Push unberührt:** der Push (ynab-001, `YnabClient`) konsumiert die committeten, korrekt
signierten `TransactionSplit`s — keine Server-Änderung, keine Migration.
