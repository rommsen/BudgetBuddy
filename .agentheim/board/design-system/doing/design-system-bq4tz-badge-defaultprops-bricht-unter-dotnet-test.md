---
id: design-system-bq4tz
title: "Eager ausgewertete Top-Level-Werte im Design-System zu Funktionen machen, damit View-Module unter dotnet test testbar werden"
status: doing
type: bug
context: design-system
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [testability, badge, feliz, dotnet-test]
related_adrs: [0020]
related_research: []
prior_art: []
---

## Why
Gefunden bei `ledger-x0yga`: eine neue reine, unit-testbare Funktion in
`Components.Ledger.View` (`formatEntryDate`) griff auf ein privates Modul-`let`
(`de`, eine `CultureInfo`) zu. Der erste Feldzugriff auf ein Modul-`let` löst unter
.NET F#s "startup code" aus, das **alle** Top-Level-Werte des Moduls initialisiert.

Das riss unter `dotnet test` (echtes .NET, kein Fable/Feliz-Laufzeitkontext) eine
`InvalidCastException`:

```
System.TypeInitializationException: The type initializer for
'<StartupCode$Client>.$Client.DesignSystem.Badge' threw an exception.
 ---> System.InvalidCastException: Unable to cast object of type
'System.Tuple`2[System.String,System.Object]' to type 'Feliz.IReactProperty'.
   at Client.DesignSystem.Badge.view(BadgeProps props)
   at Client.DesignSystem.Badge.success(String text)
   at <StartupCode$Client>.$Client.DesignSystem.Badge..cctor()
```

**Die Ursache ist nicht `defaultProps`** (ein reiner Record, harmlos), sondern sieben
**eager ausgewertete** Top-Level-Werte am Ende von `Badge.fs`, die schon beim Modul-Load
rendern. Ein parameterloses `let` auf Modulebene ist in F# ein Wert, kein Aufruf. Unter
Fable ist ein `(string, obj)`-Tupel faktisch ein `IReactProperty`, unter echtem .NET
scheitert der Cast.

Warum es bisher grün blieb: die bestehenden View-Tests rufen ausschliesslich **Funktionen**
auf (`accountAbbreviation`, `formatEntryDate`). Funktionen sind keine Felder, lösen den
Startup-Code also nicht aus. Sobald ein Test einen **Wert** anfasst, knallt es.

Das ist kein theoretischer Stolperstein: `Components.Ledger.View` hält mit
`deletedBadge` (Zeile 148) selbst so einen Wert, und genau dieses Modul muss
`ledger-kdnnz` unter .NET testen (Chip-Logik "Zu prüfen", ADR 0020). Dessen Task-Datei
trägt den Workaround heute als Worker-Hinweis: "View-Code nicht unter .NET instanziieren".

## What
Festgelegt mit Roman am 2026-09-18: **die betroffenen Werte werden zu Funktionen**
(`let imported () = success "Imported"`), Aufrufstellen zu `Badge.imported ()`. Das
verschiebt die Auswertung an die Aufrufstelle, wo unter `dotnet test` nie jemand hinkommt,
und behält die sprechenden Namen.

Betroffen sind drei Stellen im Design-System und eine im Kassenbuch-View:

1. **`src/Client/DesignSystem/Badge.fs`**, sieben Statusbadges (Zeilen 203 bis 221):
   `imported`, `pendingReview`, `autoCategorized`, `manual`, `uncategorized`, `skipped`,
   `failed`.
2. **`src/Client/DesignSystem/Loading.fs`**, sieben Werte (Zeilen 178, 190, 209, 225, 402,
   424, 511, 517): `lineFull`, `card`, `avatarWithText`, `cardSkeleton`, `cardLoading`,
   `buttonSpinner`, `shimmer`, `shimmerNeon`.
3. **`src/Client/DesignSystem/Table.fs`**, `tdEmpty` (Zeile 354).
4. **`src/Client/Components/Ledger/View.fs`**, `deletedBadge` (Zeile 148), verwendet in
   Zeile 223 und 294. Der Hebel, der `ledger-kdnnz` freimacht.

Aufrufstellen ausserhalb der Definitionsdateien gibt es nur vier, alle in
`src/Client/Components/Styleguide/View.fs` (Zeilen 269 bis 272), plus vier Doku-Zeilen in
`CLAUDE.md` (675 bis 678). Der Styleguide unter `standards/` nennt die Namen nicht.

**Nicht in dieser Aufgabe:** die 17 Sektions-Werte in
`src/Client/Components/Styleguide/View.fs` (`colorSection`, `badgeSection` und Geschwister).
Sie tragen dasselbe Muster, aber dieses Modul wird nie unter .NET getestet, und der Umbau
wäre ein grosser Diff ohne Nutzen. Wer das Modul künftig testbar machen will, zieht sie
nach demselben Rezept nach.

## Acceptance criteria
- [ ] Ein Test fasst einen **Wert** (kein Funktionsaufruf) aus `Components.Ledger.View` an
      und löst damit den Startup-Code des Moduls aus. Er scheitert vor dem Fix mit
      `TypeInitializationException` und ist danach grün. Kommentar im Test nennt den Bug,
      den er verhindert.
- [ ] Die sieben Statusbadges in `Badge.fs` sind Funktionen mit `()`-Parameter; `Badge.fs`
      enthält danach keinen Top-Level-`let` mehr, der `view` beim Modul-Load aufruft.
- [ ] Dieselbe Umstellung in `Loading.fs` (acht Werte) und `Table.fs` (`tdEmpty`).
- [ ] `deletedBadge` in `Components/Ledger/View.fs` ist eine Funktion; beide
      Verwendungsstellen (Zeile 223 und 294) rufen sie auf.
- [ ] Die vier Aufrufstellen in `Components/Styleguide/View.fs` sind nachgezogen.
- [ ] Ein Suchlauf über `src/Client/DesignSystem/` findet keinen Top-Level-`let` mehr, der
      eine Feliz-Komponente eager rendert (ausgenommen die benannte Styleguide-Datei).
- [ ] `CLAUDE.md` nennt die Statusbadges in der Badge-Quick-Reference mit `()`.
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.
- [ ] Die `/styleguide`-Route zeigt die Badge- und Loading-Sektionen unverändert. [human-eye]

## Notes
Captured am 2026-09-12 aus `ledger-x0yga`, verfeinert am 2026-09-18 mit Roman.

**Umgangen in `ledger-x0yga`:** `Components.Ledger.View` hält keine gemeinsame
Modul-Konstante für `CultureInfo` mehr; jede Datums-Funktion baut ihre eigene Instanz
lokal (`germanCulture ()`). Das machte `formatEntryDate` testbar, ohne die Ursache zu
beheben.

Diese Aufgabe nimmt `ledger-kdnnz` eine Krücke weg, blockiert sie aber nicht: dessen
Worker-Hinweise beschreiben den Workaround, solange der Fix nicht da ist. Läuft
`ledger-kdnnz` parallel, greift der Nutzen erst bei der nächsten Kassenbuch-Scheibe.

Dateien, die `ledger-kdnnz` anfasst: `Events.fs`, `Domain.fs`, `Persistence.fs`,
`QueryHandlers.fs`, `Shared/Domain.fs` und `Components/Ledger/View.fs`. Die Überschneidung
liegt allein bei `deletedBadge` (vier Zeilen, Zeile 148 plus zwei Aufrufe). Wer zuerst
merged, gewinnt; der andere zieht diese vier Zeilen im Merge nach.
