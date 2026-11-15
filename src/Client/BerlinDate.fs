/// Der heutige Kalendertag in Europe/Berlin (aus `Components.Overview.State` hierher gezogen in ledger-zfj78, damit
/// auch das Kassenbuch ihn nutzt, das vor der Übersicht kompiliert).
module Client.BerlinDate

#if FABLE_COMPILER
[<Fable.Core.Emit("new Date().toLocaleDateString('en-CA', { timeZone: 'Europe/Berlin' })")>]
let private berlinDateText () : string = Fable.Core.Util.jsNative
#endif

/// Der heutige Kalendertag in Europe/Berlin, unabhängig von der Zeitzone des Geräts. Unter .NET (Tests) die
/// Ortszeit, die Tests laufen mit `TZ=Europe/Berlin`.
let today () : System.DateTime =
#if FABLE_COMPILER
    match System.DateTime.TryParse(berlinDateText ()) with
    | true, day -> day.Date
    | _ -> System.DateTime.Today
#else
    System.DateTime.Today
#endif
