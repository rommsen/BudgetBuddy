---
id: design-system-bq4tz
title: "Eager ausgewertete Top-Level-Werte im Design-System zu Funktionen machen, damit View-Module unter dotnet test testbar werden"
status: done
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

## Verifier note (iteration 1)

**VERDICT: FAIL** — `ITERATION_HINT: likely-fixable`

**REASONS (wörtliches Zitat des Verifier-Urteils):**
- Akzeptanzkriterium 6 ("Ein Suchlauf über `src/Client/DesignSystem/` findet keinen Top-Level-`let` mehr, der eine Feliz-Komponente eager rendert") ist nicht erfüllt. Zwei Design-System-Module halten weiterhin eager Modul-Level-Feliz-Werte und reproduzieren exakt den Fehler, den diese Aufgabe beseitigen soll. Zur Laufzeit belegt, nicht gelesen: Ich habe jeden `<StartupCode$Client>.$Client.DesignSystem.*`-Typinitialisierer auf dem Build des Branches (`src/Tests/bin/Debug/net9.0/Client.dll`) per `RuntimeHelpers.RunClassConstructor` erzwungen. `Badge`, `Loading`, `Table` und `Components.Ledger.View` laufen jetzt sauber, aber: `$Client.DesignSystem.Primitives` wirft `InvalidCastException: Unable to cast object of type 'System.Tuple\`2[System.String,System.Object]' to type 'Feliz.IReactProperty'`, und `$Client.DesignSystem.BottomSheet` denselben Cast-Fehler gegen `Feliz.ISvgAttribute`.
- Die betroffenen Bindungen, nach Datei und Zeile: `src/Client/DesignSystem/Primitives.fs`: `Spacer.xs` 168, `Spacer.sm` 169, `Spacer.md` 170, `Spacer.lg` 171, `Spacer.xl` 172 (je `= view Spacer<Size>`, was `Html.div` rendert), `Spacer.flex` 175, `Divider.horizontal` 187, `Divider.large` 193, `Divider.gradient` 199, `Divider.vertical` 205. Reflection auf den kompilierten Startup-Typ bestätigt alle zehn als echte eager Felder. Dazu `src/Client/DesignSystem/BottomSheet.fs:86` `let private closeIcon : ReactElement = Svg.svg [ ... ]`. Sie liegen in verschachtelten Modulen (`module Spacer =`, `module Divider =`), weshalb ein an Spalte 0 verankerter grep sie verfehlt; es sind aber Modul-Level-Werte, keine lokalen Bindungen, und sie initialisieren beim Modul-Load genau wie `Badge.imported` zuvor.
- Folge, keine Formalie: Sobald irgendein .NET-Test einen Wert in `Primitives` oder `BottomSheet` anfasst, trifft er dieselbe `TypeInitializationException`, die diese Aufgabe beseitigen sollte. Das Suchlauf-Kriterium ist der Teil, der die Korrektur dauerhaft statt lokal macht, und genau er ist offen geblieben.
- Alles Übrige hält, fürs Protokoll: die Suite ist nach dem Urteil des Runners grün, nicht nach der Behauptung des Workers (`dotnet test` aus dem Worktree, Exit 0, 872 bestanden, 0 Fehler, 7 übersprungen), `npm run build` Exit 0. Der Reflection-Wächter in `src/Tests/LedgerViewTests.fs:179-197` ist tragfähig und nicht leer: der Ledger-Startup-Typ löst nach dem Fix auf (eine Umbenennung ließe `GetType` null liefern und der Test stürbe laut an `startupType.TypeHandle`, würde also nie still bestehen), und meine Probe beweist, dass derselbe `RunClassConstructor`-Aufruf eager Feliz-Felder heute noch erkennt, weil er bei `Primitives` und `BottomSheet` anschlägt. Die erweiterte Reichweite (`spinnerDefault`/`spinnerInline`/`spinnerLarge` samt ihren drei internen Aufrufstellen in Loading.fs 359/372/409, und `Loading.cardSkeleton ()` in Rules/View.fs:549) ist legitime Erfüllung von Kriterium 6, kein Wildwuchs, und alle internen Aufrufstellen in `Loading.fs`/`Table.fs` sind nachgezogen. Die 17 Styleguide-Sektionswerte sind unberührt, die vier Badge-Aufrufstellen dort (269-272) und beide `deletedBadge ()`-Stellen (Ledger/View.fs 250, 333) sind aktualisiert, und CLAUDE.md 675-678 zeigt die `()`-Form. Kein `.agentheim/`-Pfad und keine Task-Datei im Diff. Das letzte Kriterium ist `[human-eye]`, Augenprüfung durch Roman offen.

**SUGGESTED_FIX:** Dasselbe Rezept auf die zwei vom Suchlauf verfehlten Module anwenden: die zehn `Spacer`/`Divider`-Werte in `Primitives.fs` und `closeIcon` in `BottomSheet.fs:86` zu `()`-Funktionen machen und die Aufrufstellen nachziehen (`Spacer.*`/`Divider.*` haben derzeit null Aufrufstellen unter `src/`, sind also reine Definitions-Änderungen; `closeIcon` wird in den Sheet-Views derselben Datei benutzt). Danach den Suchlauf als **Laufzeitprüfung** statt als grep wiederholen: jeden `<StartupCode$Client>.$Client.DesignSystem.*`-Typinitialisierer zu erzwingen ist, was Kriterium 6 tatsächlich beweist, und eine Verallgemeinerung des bestehenden `LedgerViewTests`-Wächters zu einer Schleife über diese Startup-Typen würde die Regel mechanisieren, die das `README_DELTA` sonst nur als Prosa vorschlägt.

**Conductor-Anmerkung:** Der übersehene Fall geht auch auf mich zurück. Meine eigene Gegenprobe nach dem Worker-Lauf nutzte dasselbe an Spalte 0 verankerte Muster und meldete deshalb fälschlich "keine Reste". Ein grep taugt für dieses Kriterium grundsätzlich nicht, die Laufzeitprüfung schon.

## Verifier note (iteration 2)

**VERDICT: FAIL** — `ITERATION_HINT: likely-fixable`. **Dies ist die letzte reguläre Iteration; ein weiterer Fehlschlag eskaliert an Roman.**

**REASONS (wörtliches Zitat des Verifier-Urteils):**
- Verstoß gegen die bindende Schreibregel des Projekts (echte Umlaute, nie ae/oe/ue/ss-Umschreibung) in neu hinzugefügten Zeilen, und zwar im sichtbarsten Artefakt überhaupt, einem Testnamen: `src/Tests/DesignSystemStartupTests.fs:44` `test "Es gibt mindestens ein Design-System-Startup-Modul zu pruefen"` (zu "prüfen"). Dieser Name erscheint in jeder Ausgabe von `dotnet test`.
- Dieselbe Drift in der Assertion-Meldung und den Kommentaren derselben Datei: `:49` `"... Filter oder Namenskonvention pruefen"`; `:45` `Waechter gegen den Waechter`; `:47` `bestuende` und `geprueft`. Die Datei schreibt in derselben Zeile 47 `grün` korrekt, ist also in sich inkonsistent, was Versehen statt Absicht belegt.
- Ebenfalls neu hinzugefügt, gleiche Regel: `src/Tests/LedgerViewTests.fs:190` `wieder \`deletedBadge\` heisst oder anders` (zu "heißt"). Stammt aus Iteration 1, steht aber in diesem Diff. Gedankenstriche: null Treffer. Präzedenzfall in derselben Charge: `infra-za7fq` wurde in Iteration 1 für genau diesen Fehler zurückgewiesen, einschließlich des Namens seiner Testliste.
- **Blinder Fleck im neuen Wächter**, kein heute offenes Kriterium, aber dieselbe Fehlerklasse, die zum Iteration-1-FAIL führte: der Namensfilter `<StartupCode$Client>.$Client.DesignSystem.` trifft nur 19 der 20 Dateien unter `src/Client/DesignSystem/`. `src/Client/DesignSystem/ErrorDisplay.fs:1` deklariert `namespace Client.DesignSystem` mit innerem `module ErrorDisplay =` statt `module Client.DesignSystem.ErrorDisplay`; sein Startup-Typ heißt deshalb `<StartupCode$Client>.$ErrorDisplay` und wird vom Filter nie erfasst. Der Sweep bestätigt: `ErrorDisplay` läuft heute sauber durch, Kriterium 6 ist also erfüllt, aber ein künftiger eager Wert in dieser einen DS-Komponente liefe am Wächter vorbei, genau wie die `Spacer`/`Divider`-Werte am grep vorbeiliefen.

**Was belegt in Ordnung ist** (eigenständig nachgemessen, nicht geglaubt):
- **Kriterium 6 ist zur Laufzeit erfüllt.** Eigener Sweep über die gebaute `Client.dll` mit `RuntimeHelpers.RunClassConstructor` auf allen 48 `<StartupCode$Client>`-Typen: alle 19 `$Client.DesignSystem.*`-Typinitialisierer laufen sauber, einschließlich `Primitives` und `BottomSheet`, die in Iteration 1 warfen. `Input` läuft ebenfalls sauber, womit `Input.fs:26` (`baseInputClass`) als reiner String bestätigt ist. Nur drei Typen werfen überhaupt, alle außerhalb des Auftrags: `$Api` (Fable.Remoting-Proxy), `$App` (Fable-Dummy) und `$Components.Styleguide.View` mit dem bekannten Cast-Fehler, also die 17 ausdrücklich ausgeklammerten Sektionswerte. Der Präfix-Filter lässt dieses Modul korrekt außen vor.
- **Der Wächter ist echt und nicht leer, durch Eingriff bewiesen:** `Spacer.md` vorübergehend wieder zu einem eager Wert gemacht, der Test schlug fehl und nannte `$Client.DesignSystem.Primitives` namentlich (873 erfolgreich, 1 Fehler). Danach zurückgespielt, `git status --porcelain` leer, Suite wieder grün. Der Test-gegen-den-Test trägt ebenfalls: der Filter liefert heute 19 Typen, bei gebrochener Namenskonvention wäre die Liste leer und `Expect.isNonEmpty` schlüge an.
- `dotnet test` Exit 0, 874 erfolgreich, 0 Fehler, 7 übersprungen; `npm run build` Exit 0; echte Entwicklungsdatenbank unberührt; Scope genau die zwölf Dateien, in Iteration 2 nur die vier erwarteten; kein `.agentheim/`-Pfad. Kriterien 1 bis 5, 7 und 8 gedeckt, das letzte ist `[human-eye]`.
- Zur Metrik-Drift-Prüfung: der Messwechsel von grep zu Laufzeitprüfung bei unverändertem Kriteriumstext ist keine Drift, sondern genau die Verschärfung, die der Vermerk aus Iteration 1 verlangt hat.

**SUGGESTED_FIX:** Die fünf Umschreibungen in `src/Tests/DesignSystemStartupTests.fs` und "heisst" in `src/Tests/LedgerViewTests.fs:190` auf echte Umlaute umstellen. Zusätzlich den Wächter-Filter so erweitern, dass er alle 20 Dateien unter `src/Client/DesignSystem/` erfasst.

## Outcome

Eager ausgewertete Top-Level-Werte im Design-System sind beseitigt, und die Regel dahinter
ist nicht mehr Prosa, sondern ein laufender Test.

**Konvertiert zu `()`-Funktionen:** die sieben Statusbadges in `Badge.fs` (`imported`,
`pendingReview`, `autoCategorized`, `manual`, `uncategorized`, `skipped`, `failed`), acht
Werte in `Loading.fs` plus die drei Spinner-Konstanten `spinnerDefault`, `spinnerInline`,
`spinnerLarge`, `Table.tdEmpty`, `Components.Ledger.View.deletedBadge`, sowie die zehn
`Spacer`/`Divider`-Werte in `Primitives.fs` und `closeIcon` in `BottomSheet.fs`. Alle
Aufrufstellen nachgezogen: zwei in `Ledger/View.fs` (250, 333), vier in
`Styleguide/View.fs` (269-272), eine in `Rules/View.fs` (549), drei intern in `Loading.fs`,
drei intern in `BottomSheet.fs`, dazu die Badge-Quick-Reference in `CLAUDE.md` (675-678).

**Der eigentliche Gewinn ist `src/Tests/DesignSystemStartupTests.fs`.** Das Kriterium war
als Suchlauf formuliert, und genau daran ist die Aufgabe zweimal gescheitert: Ein an Spalte
0 verankerter Grep übersieht Werte in verschachtelten Modulen (`module Spacer =`,
`module Divider =`), und ein Muster, das Einrückung zulässt, findet stattdessen lokale
Bindungen in Funktionsrümpfen. Eine Textsuche kann einen Modul-Level-Wert nicht von einer
lokalen Bindung unterscheiden. Der Test stellt die Frage deshalb anders: Er ermittelt per
Reflection alle `<StartupCode$Client>.$Client.DesignSystem.*`-Typen der Client-Assembly
plus den Sonderfall `$ErrorDisplay` (diese Datei nutzt `namespace` mit innerem `module` und
trägt deshalb nicht den üblichen Präfix) und erzwingt jeden Typinitialisierer. Ein neuer
eager Wert in irgendeinem Design-System-Modul lässt den Test sofort scheitern, mit dem
Modulnamen im Fehlertext.

Ein zweiter Test sichert den ersten ab: Er prüft, dass der Filter überhaupt Typen findet.
Ohne ihn könnte eine gebrochene Namenskonvention die Liste leeren und der Haupttest
bestünde grün, ohne je etwas geprüft zu haben.

**Verifikation durch den Verifier selbst, nicht durch Behauptung:**
- Eigener Sweep über alle 48 `<StartupCode$Client>`-Typen der gebauten `Client.dll`: alle
  20 Design-System-Typen laufen sauber. Nur drei Typen im ganzen Client werfen, alle
  ausserhalb des Auftrags: `$Api` (Fable.Remoting-Proxy), `$App` (Fable-Dummy) und
  `$Components.Styleguide.View` mit den 17 bewusst ausgeklammerten Sektionswerten.
- Ende-zu-Ende-Probe: temporär ein eager `Html.div` in `ErrorDisplay.fs` angehängt, die
  Suite schlug fehl (Exit 1) und nannte `$ErrorDisplay` namentlich; danach wiederhergestellt.
- In Iteration 2 dieselbe Probe mit `Spacer.md`, ebenfalls fehlgeschlagen und namentlich.
- `dotnet test` Exit 0 mit 874 bestanden, 0 Fehlern, 7 übersprungen; `npm run build` Exit 0;
  die echte Entwicklungsdatenbank unberührt.

**Drei Iterationen, jede mit einem berechtigten Befund:** Iteration 1 übersah `Primitives`
und `BottomSheet`, weil der Suchlauf ein Grep war. Iteration 2 lieferte den Laufzeittest,
erfasste aber 19 von 20 Dateien und trug sechs Umschriften. Iteration 3 schloss beides.

**Bekannte, bewusst offengelassene Schwäche** (vom Verifier gemessen, nicht vermutet, und
als `design-system-e5vfc` im Backlog): Die `ErrorDisplay`-Abdeckung ist heute echt, aber nicht
gegen das Verschwinden ihrer eigenen Sonderfallzeile geschützt. Entfernt man
`|| t.FullName = errorDisplayStartupTypeName`, bleibt die Suite grün, weil die
Nichtleer-Prüfung auch bei 19 Typen hält. Ein `Expect.contains` auf die Typliste würde das
fangen, ganz ohne eager Wert.

**Offen für Romans Auge** (Kriterium `[human-eye]`, bewusst nicht abgehakt): ob die
`/styleguide`-Route die Badge- und Loading-Sektionen unverändert zeigt. Die Änderung ist
reine Aufrufform, das Ergebnis der Funktionen bleibt identisch, aber die Sichtprüfung
gehört dir.

Dateien: `src/Client/DesignSystem/{Badge,Loading,Table,Primitives,BottomSheet}.fs`,
`src/Client/Components/{Ledger,Styleguide,Rules}/View.fs`, `CLAUDE.md`,
`src/Tests/{DesignSystemStartupTests,LedgerViewTests}.fs`, `src/Tests/Tests.fsproj`.
