---
id: ledger-k9tq7
title: Kassenbuch lädt fehlende Settings nach, statt beim Laden der Kategorien still aufzugeben (sonst bleibt der Kategorie-Picker leer bis zum Neuladen)
status: doing
type: bug
context: ledger
created: 2026-10-24
completed:
depends_on: []
blocks: []
tags: [captured, kategorie, picker]
related_adrs: []
related_research: []
prior_art: [ledger-kwwxz]
---

## Why
Live-Befund 2026-10-24: Am PC zeigte der Kategorie-Picker im Kassenbuch-Detail nur "Ohne
Kategorie", ohne Kategorien und ohne Fußzeile "Stand …". Die Kategorien wurden in dieser
Sitzung nie geladen, weil das Laden der Settings beim Seitenaufruf gescheitert war (siehe
infrastructure-y6atr).

## What
`LoadCategories` im Kassenbuch gibt still auf, wenn die Settings nicht `Success` sind
(`src/Client/State.fs`, Zweig `LedgerMsg LoadCategories`, `| _ -> model, Cmd.none`). Danach
kommen nie Kategorien, auch nicht über `EnsureCategoriesFresh` beim Öffnen des Pickers. Der
Nachhol-Weg in `syncCategoriesCmd` (nach `SettingsLoaded`) greift nur bei
`Ledger.Categories = NotAsked`.

Neues Verhalten:
1. `LoadCategories` bei Settings `NotAsked` oder `Failure`: löst `LoadSettings` aus, setzt
   `Ledger.Categories` auf `Loading` und merkt sich, dass die Kategorien auf die Settings
   warten.
2. Bei Settings `Loading` (Abfrage läuft schon): kein zweites `LoadSettings`, aber ebenfalls
   `Loading` und "wartet".
3. Kommt danach `SettingsLoaded (Ok …)` mit Budget, startet `LoadCategories` (auch wenn
   `Ledger.Categories` inzwischen `Loading` statt `NotAsked` ist).
4. Kommt `SettingsLoaded (Error …)`, während die Kategorien warten, wird
   `Ledger.Categories` zu `Failure` (Text sinngemäß "Einstellungen konnten nicht geladen
   werden"), damit der Picker aus `ledger-dmw83` Fehler und "Erneut versuchen" zeigt. Ein
   erneutes `LoadCategories` beginnt wieder bei Punkt 1.

## Acceptance criteria
- [ ] Reducer-Test: Settings `Failure`, dann `LedgerMsg LoadCategories` ergibt
      `Ledger.Categories = Loading` und einen Befehl, der `LoadSettings` auslöst (kein
      stilles `Cmd.none` mehr).
- [ ] Reducer-Test: Settings `NotAsked` verhält sich wie `Failure`.
- [ ] Reducer-Test: Settings `Loading`, dann `LoadCategories` ergibt `Loading`, aber kein
      zweites `LoadSettings`.
- [ ] Reducer-Test der Folge: nach Punkt 1 liefert `SettingsLoaded (Ok settings)` mit
      `DefaultBudgetId` einen Befehl, der `LedgerMsg LoadCategories` auslöst, und dieser
      ruft dann `getCategories` (Kategorien kommen an).
- [ ] Reducer-Test: nach Punkt 1 setzt `SettingsLoaded (Error …)` `Ledger.Categories` auf
      `Failure`.
- [ ] Bestehendes Verhalten bleibt: ein erneutes `SettingsLoaded (Ok …)`, während die
      Kategorien schon `Success` sind und nicht warten, löst kein `LoadCategories` aus
      (bestehender Test oder neuer Test).
- [ ] Die Regressionstests tragen einen Kommentar zum Befund vom 2026-10-24 (leerer Picker
      nach gescheitertem Settings-Laden).
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Captured via `quick-capture` on 2026-10-24, verfeinert am 2026-10-24.

Settings `Success` ohne YNAB-Budget bleibt außerhalb dieser Task (heute ebenfalls still).

Überschneidung ohne fachliche Abhängigkeit: `infrastructure-y6atr` und `ledger-dmw83`
ändern dieselbe Stelle in `src/Client/State.fs` (Zweig `LedgerMsg LoadCategories`). Nicht
parallel bauen; Reihenfolge y6atr, k9tq7, dmw83. `ledger-dmw83` nutzt den `Failure`-Zustand
aus Punkt 4, funktioniert aber auch ohne ihn (heute entsteht `Failure`, wenn `getCategories`
scheitert).
