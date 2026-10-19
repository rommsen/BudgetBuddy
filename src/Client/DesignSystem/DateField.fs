/// Deutsches Datumsfeld (design-system-m7z9q): ein Textfeld "TT.MM.JJJJ" in
/// `Input.Small`-Maßen mit Kalender-Knopf. Der Knopf öffnet ein verstecktes natives
/// `<input type="date">`; dessen Auswahl schreibt das Datum deutsch ins Textfeld. Das
/// Textfeld bleibt immer bedienbar, auch wo der native Kalender fehlt.
///
/// Die Datumsrechnung steht in reinen Funktionen ohne Kultur und ohne
/// `DateTime.Parse`, damit .NET (Tests) und Fable (Browser) gleich rechnen.
module Client.DesignSystem.DateField

open System
open Feliz
open Fable.Core.JsInterop
open Client.DesignSystem.Tokens

// ============================================
// Reine Funktionen
// ============================================

/// Ergebnis einer Eingabe im Feld
type DateEntry =
    | EmptyDate
    | ValidDate of DateTime
    | InvalidDate of string

/// Schriftgröße des Textfelds: 16px unterhalb `md` (iOS-Zoom-Schutz), 14px ab `md`.
/// Das `!` (Tailwind important) ist nötig, weil `.input-field` in `styles.css` ohne
/// Layer `font-size: 14px` setzt und die Utility-Klassen von `Input.Small` sonst am Handy
/// unterliegen.
let textSizeClass = "text-base! md:text-sm!"

/// Hinweis unter einem Feld mit ungültiger Eingabe
let invalidHint = "Datum als TT.MM.JJJJ"

let private isDigits (s: string) : bool =
    s.Length > 0 && s |> Seq.forall (fun c -> c >= '0' && c <= '9')

/// Baut ein Datum aus Tag, Monat und Jahr; `None` bei unmöglichen Kombinationen
/// (31.02., Monat 13, Jahr 0).
let private tryMakeDate (year: int) (month: int) (day: int) : DateTime option =
    if year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 then None
    elif day > DateTime.DaysInMonth(year, month) then None
    else Some (DateTime(year, month, day))

/// "07.10.2026" und "7.10.2026" ergeben ein Datum. "2026-10-07", "07.10.26",
/// "31.02.2026" und "" ergeben `None`.
let parseGermanDate (text: string) : DateTime option =
    let parts = text.Trim().Split('.')
    match parts with
    | [| d; m; y |] when isDigits d && isDigits m && isDigits y && d.Length <= 2 && m.Length <= 2 && y.Length = 4 ->
        tryMakeDate (int y) (int m) (int d)
    | _ -> None

/// "07.10.2026"
let formatGermanDate (d: DateTime) : string =
    sprintf "%02d.%02d.%04d" d.Day d.Month d.Year

/// "2026-10-07" ergibt ein Datum, alles andere `None`
let private parseIsoDate (text: string) : DateTime option =
    match text.Trim().Split('-') with
    | [| y; m; d |] when isDigits d && isDigits m && isDigits y && d.Length = 2 && m.Length = 2 && y.Length = 4 ->
        tryMakeDate (int y) (int m) (int d)
    | _ -> None

/// "2026-10-07" ergibt "07.10.2026"; ein ungültiger Wert bleibt unverändert
let isoToGerman (iso: string) : string =
    match parseIsoDate iso with
    | Some d -> formatGermanDate d
    | None -> iso

/// "07.10.2026" ergibt `Some "2026-10-07"`
let germanToIso (german: string) : string option =
    parseGermanDate german
    |> Option.map (fun d -> sprintf "%04d-%02d-%02d" d.Year d.Month d.Day)

/// Leer (auch nur Leerzeichen), gültig oder ungültig
let classifyDateText (text: string) : DateEntry =
    if String.IsNullOrWhiteSpace text then EmptyDate
    else
        match parseGermanDate text with
        | Some d -> ValidDate d
        | None -> InvalidDate text

/// ISO-Wert für das versteckte native Feld ("" ohne Datum)
let isoForValue (value: DateTime option) : string =
    value |> Option.bind (fun d -> germanToIso (formatGermanDate d)) |> Option.defaultValue ""

/// Der Text, den das Feld für einen Eingangswert zeigt
let textForValue (value: DateTime option) : string =
    value |> Option.map formatGermanDate |> Option.defaultValue ""

/// Soll der Text dem Eingangswert nachziehen? Eigene Tippschritte bleiben stehen:
/// solange die Eingabe als ungültig gemeldet ist (`invalid`), ändert der Eingangswert
/// den Text nicht. Sonst zieht der Text nach, wenn er nicht zum Wert passt (Schnellwahl,
/// Zurücksetzen, Öffnen des Popovers).
let syncedText (value: DateTime option) (invalid: bool) (current: string) : string =
    if invalid then current
    else
        match value, classifyDateText current with
        | Some d, ValidDate cur when cur = d -> current
        | Some d, _ -> formatGermanDate d
        | None, EmptyDate -> current
        | None, _ -> ""

// ============================================
// Baustein
// ============================================

type DateFieldProps = {
    /// Der Wert von außen (zum Beispiel der Entwurf des Zeitraum-Filters)
    Value: DateTime option
    /// Meldet jede Änderung des Textes als Eintrag
    OnChange: DateEntry -> unit
    /// Meldet der Aufrufer die Eingabe als ungültig? Steuert den Fehlerzustand.
    Invalid: bool
    /// Beschriftung für Screenreader und Kalender-Knopf
    Label: string
}

/// Öffnet den nativen Kalender; ohne `showPicker()` (ältere Browser) greift das Feld
/// per `focus()` und `click()`.
let private openNativePicker (el: Browser.Types.HTMLInputElement) : unit =
    try
        el?showPicker ()
    with _ ->
        el.focus ()
        el.click ()

[<ReactComponent>]
let view (props: DateFieldProps) =
    let text, setText = React.useState (textForValue props.Value)
    let nativeRef = React.useRef<Browser.Types.HTMLInputElement option> None
    let valueKey = textForValue props.Value

    // Der Eingangswert zieht den Text nach (Schnellwahl, Zurücksetzen, Öffnen).
    React.useEffect (
        (fun () ->
            let next = syncedText props.Value props.Invalid text
            if next <> text then setText next),
        [| box valueKey; box props.Invalid |]
    )

    let handleText (newText: string) =
        setText newText
        props.OnChange (classifyDateText newText)

    let handleNative (iso: string) =
        if not (String.IsNullOrEmpty iso) then
            let german = isoToGerman iso
            setText german
            props.OnChange (classifyDateText german)

    let state = if props.Invalid then Input.Error invalidHint else Input.Normal

    Html.div [
        prop.className "relative space-y-1"
        prop.children [
            Html.div [
                prop.className "flex items-center gap-1.5"
                prop.children [
                    Html.div [
                        prop.className "min-w-0 flex-1"
                        prop.children [
                            Input.text { Input.textInputDefaults with
                                            Value = text
                                            OnChange = handleText
                                            Placeholder = "TT.MM.JJJJ"
                                            Size = Input.Small
                                            State = state
                                            ClassName = Some $"w-full {textSizeClass}" }
                        ]
                    ]
                    Html.button [
                        prop.type' "button"
                        prop.ariaLabel $"{props.Label}: Kalender öffnen"
                        prop.className $"shrink-0 inline-flex items-center justify-center rounded-lg min-w-[36px] min-h-[36px] {Backgrounds.dark} {Borders.default'} border hover:border-neon-teal transition-colors"
                        prop.onClick (fun _ ->
                            match nativeRef.current with
                            | Some el -> openNativePicker el
                            | None -> ())
                        prop.children [ Icons.calendar Icons.SM Icons.Default ]
                    ]
                ]
            ]
            // Verstecktes natives Feld: nur Träger des Kalenders, kein Fokusziel.
            Html.input [
                prop.type' "date"
                prop.ref nativeRef
                prop.tabIndex -1
                prop.custom ("aria-hidden", "true")
                prop.className "absolute right-0 top-0 h-9 w-9 opacity-0 pointer-events-none"
                prop.value (isoForValue props.Value)
                prop.onChange handleNative
            ]
            if props.Invalid then
                Html.div [
                    prop.className $"{FontSizes.xs} {Colors.neonRed}"
                    prop.text invalidHint
                ]
        ]
    ]
