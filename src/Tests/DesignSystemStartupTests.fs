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
