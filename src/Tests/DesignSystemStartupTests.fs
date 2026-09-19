module DesignSystemStartupTests

// Verallgemeinerung des Wächters aus LedgerViewTests.fs (design-system-bq4tz, Iteration 2).
//
// Akzeptanzkriterium 6 verlangt einen Suchlauf über `src/Client/DesignSystem/`, der keinen
// Top-Level-`let` mehr findet, der eine Feliz-Komponente eager rendert. Ein grep an Spalte 0
// verfehlt genau diesen Fall, wenn der Wert in einem verschachtelten Modul liegt (`module
// Spacer =`, `module Divider =` in Primitives.fs; `closeIcon` in BottomSheet.fs) - beide
// wurden in Iteration 1 übersehen. Ein grep kann das grundsätzlich nicht verlässlich prüfen;
// eine Laufzeitprüfung schon: jeder eager ausgewertete Wert löst beim ersten Feldzugriff
// .NET F#s Modul-Startup-Code aus, der ALLE Top-Level-Werte des Moduls initialisiert
// (`<StartupCode$Client>.$Client.DesignSystem.<Modul>..cctor()`), und genau der lässt sich
// per Reflection erzwingen.
//
// Dieser Test erzwingt daher jeden `<StartupCode$Client>.$Client.DesignSystem.*`-
// Typinitialisierer, den er per Reflection auf der geladenen Client-Assembly findet - nicht
// eine handgeschriebene Liste, die beim nächsten neuen Modul sofort veraltet wäre (derselbe
// Veraltungsfehler, der zum Iteration-1-FAIL führte). Wirft einer eine
// TypeInitializationException (Ursache: ein `(string, obj)`-Tupel lässt sich unter echtem
// .NET nicht auf `Feliz.IReactProperty`/`Feliz.ISvgAttribute` casten), nennt der Test das
// betroffene Modul.
//
// Bewusst ausgenommen: `Components.Styleguide.View` (17 Sektionswerte, laut Task-Datei
// explizit nicht in dieser Aufgabe) - der Namensfilter `$Client.DesignSystem.` trifft dieses
// Modul ohnehin nicht.
//
// Zweiter blinder Fleck, gefunden vom Verifier in Iteration 2: `ErrorDisplay.fs` deklariert
// `namespace Client.DesignSystem` mit innerem `module ErrorDisplay =` statt `module
// Client.DesignSystem.ErrorDisplay`. Sein Startup-Typ trägt deshalb NICHT den Präfix
// `$Client.DesignSystem.`, sondern heißt `<StartupCode$Client>.$ErrorDisplay`, und ein reiner
// Präfixfilter verfehlt ihn. Deshalb wird er unten explizit dazugenommen. Kein genereller
// Ausschluss-statt-Einschluss-Filter über alle `<StartupCode$Client>`-Typen: der Sweep aus
// Iteration 2 fand daneben auch `$Api` (Fable.Remoting-Proxy) und `$App` (Fable-Dummy-Code),
// die beide werfen und außerhalb des Design Systems liegen - "alles außer den bekannten drei
// Ausnahmen" würde jeden vierten Fall lautlos mitreißen. Die gezielte Ergänzung um den einen
// bekannten Sonderfall hält den Suchlauf auf `src/Client/DesignSystem/` beschränkt, wie
// Akzeptanzkriterium 6 es verlangt.

// design-system-e5vfc härtet diesen Wächter gegen seinen eigenen Verlust. Der Verifier
// von design-system-bq4tz hat gemessen, nicht vermutet, dass die Sonderfallzeile für
// ErrorDisplay unbemerkt verschwinden kann: er hat `|| t.FullName = errorDisplayStartupTypeName`
// aus dem Filter entfernt, und die Suite blieb grün, weil der einzige begleitende Test
// nur `Expect.isNonEmpty` auf die gefundene Typliste prüfte, was auch bei 19 statt 20
// Typen hält.
//
// Zwei neue Tests schließen diese Lücke:
//
// 1. `Expect.contains` nagelt genau den bekannten Sonderfall fest. Bricht die Filterzeile,
//    fehlt der Name in der Liste, und der Test wird rot, unabhängig davon, ob irgendwo
//    ein eager Wert existiert.
//
// 2. Die eigentliche Absicherung vergleicht die Zahl der gefundenen Startup-Typen gegen die
//    Zahl der `.fs`-Dateien unter `src/Client/DesignSystem/` (Ermittlung über
//    `__SOURCE_DIRECTORY__` relativ zum Testlauf, demselben Muster wie in
//    `ModuleLevelDbInitGuardTests.fs` und `FontFaceRegressionTests.fs`, kein neuer
//    Mechanismus). Das fängt nicht nur den einen bekannten Sonderfall, sondern jede
//    künftige Abweichung von der Namenskonvention: eine neue Datei, deren Modul nicht dem
//    `$Client.DesignSystem.*`-Muster folgt (und auch nicht wie ErrorDisplay explizit
//    aufgenommen wird), ändert die Dateizahl, ohne die Typzahl mitzuziehen, und der
//    Vergleich schlägt fehl.
//
// Beide Fälle wurden tatsächlich ausgeführt (nicht nur gelesen): die Sonderfallzeile
// entfernt und wieder eingesetzt, sowie testweise eine neue Datei mit abweichender
// Moduldeklaration unter `src/Client/DesignSystem/` angelegt und wieder gelöscht. Ergebnis
// im Outcome-Text der Task.

open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open Expecto

let private startupTypePrefix = "<StartupCode$Client>.$Client.DesignSystem."

// `ErrorDisplay.fs` liegt unter `src/Client/DesignSystem/`, aber sein Startup-Typ folgt nicht
// dem `$Client.DesignSystem.*`-Namensmuster der übrigen 19 Module (siehe Kommentar oben).
let private errorDisplayStartupTypeName = "<StartupCode$Client>.$ErrorDisplay"

let private designSystemStartupTypes () =
    Assembly.GetAssembly(typeof<Client.DesignSystem.Badge.BadgeProps>).GetTypes()
    |> Array.filter (fun t ->
        t.FullName.StartsWith(startupTypePrefix) || t.FullName = errorDisplayStartupTypeName)
    |> Array.sortBy (fun t -> t.FullName)

// Gleiches Muster wie `repoRoot` in `ModuleLevelDbInitGuardTests.fs` und
// `FontFaceRegressionTests.fs`: `__SOURCE_DIRECTORY__` liegt unter `src/Tests`, zwei
// Ebenen darüber liegt die Repo-Wurzel. Kein neuer Mechanismus, kein fest verdrahteter
// Pfad, der beim Verschieben des Testprojekts veralten würde.
let private designSystemDir =
    let testDir = __SOURCE_DIRECTORY__
    let repoRoot = Directory.GetParent(Directory.GetParent(testDir).FullName).FullName
    Path.Combine(repoRoot, "src", "Client", "DesignSystem")

let private designSystemSourceFileCount () =
    Directory.GetFiles(designSystemDir, "*.fs") |> Array.length

[<Tests>]
let designSystemStartupTests =
    testList "Modul-Startup: Design System (design-system-bq4tz)" [

        test "Es gibt mindestens ein Design-System-Startup-Modul zu prüfen" {
            // Wächter gegen den Wächter: bricht die Namenskonvention (z.B. durch ein
            // Assembly- oder Namespace-Rename), liefert der Filter eine leere Liste und
            // der Test darunter bestünde grün, ohne je etwas geprüft zu haben.
            let types = designSystemStartupTypes ()
            Expect.isNonEmpty types "Kein <StartupCode$Client>.$Client.DesignSystem.*-Typ gefunden - Filter oder Namenskonvention prüfen"
        }

        test "Die ErrorDisplay-Sonderfallzeile im Filter bleibt erhalten (design-system-e5vfc)" {
            // Nagelt genau den Sonderfall fest, dessen Verschwinden der Verifier von
            // design-system-bq4tz unbemerkt an `Expect.isNonEmpty` vorbeischleusen konnte.
            // Verschwindet die Zeile `|| t.FullName = errorDisplayStartupTypeName`, fehlt
            // der Name unten in der Liste und dieser Test wird rot, ganz ohne dass
            // irgendwo ein eager ausgewerteter Wert existieren müsste.
            let typeNames = designSystemStartupTypes () |> Array.map (fun t -> t.FullName)
            Expect.contains
                typeNames
                errorDisplayStartupTypeName
                "ErrorDisplay-Startup-Typ fehlt in der erfassten Liste - Sonderfallzeile im Filter geprüft?"
        }

        test "Die Zahl der erfassten Startup-Typen entspricht der Zahl der .fs-Dateien unter src/Client/DesignSystem/ (design-system-e5vfc)" {
            // Die eigentliche Absicherung: statt nur den einen bekannten Sonderfall zu
            // pflegen, wird die Abdeckung insgesamt gegen die Wirklichkeit abgeglichen.
            // Das fängt auch die nächste, noch unbekannte Namenskonventions-Abweichung,
            // nicht nur die eine, die design-system-bq4tz bereits aufgedeckt hat.
            let typeCount = designSystemStartupTypes () |> Array.length
            let fileCount = designSystemSourceFileCount ()
            Expect.equal
                typeCount
                fileCount
                $"Erfasste Startup-Typen ({typeCount}) und .fs-Dateien unter src/Client/DesignSystem/ ({fileCount}) laufen auseinander - eine Datei folgt vermutlich nicht der Namenskonvention"
        }

        test "Kein Design-System-Modul wirft beim Typinitialisierer eine TypeInitializationException" {
            let failures =
                designSystemStartupTypes ()
                |> Array.choose (fun t ->
                    try
                        RuntimeHelpers.RunClassConstructor(t.TypeHandle)
                        None
                    with ex ->
                        Some (t.FullName, ex))

            if not (Array.isEmpty failures) then
                let details =
                    failures
                    |> Array.map (fun (name, ex) -> $"{name}: {ex.GetType().Name}: {ex.Message}")
                    |> String.concat "; "
                failtestf "Eager ausgewertete Feliz-Komponente(n) beim Modul-Start in: %s" details
        }
    ]
