/// Die Betragsrechnung der Budget-Seite (budgeting-c5w2n): das Feld Zugewiesen und das Betragsfeld
/// von Holen und Abgeben. Rein, ohne Elmish und Feliz, rechnet unter Fable wie unter .NET.
///
/// Ein Operator vorn rechnet relativ zum Stand (`+20`, `*2`), eine Zahl vorn ersetzt ihn (`900+40`).
/// Punkt vor Strich, deutsche Beträge, das Ergebnis liegt auf dem Cent. `parseAmountInput` reicht
/// dafür nicht: es lehnt Vorzeichen ab und kennt keine Tausenderpunkte.
module Components.Budget.AmountMath

/// Größter Betrag eines Ergebnisses, wie `MaxMoveAmount` auf dem Server.
let private maxAmount = 1000000m

type private Token =
    | Number of decimal
    | Plus
    | Minus
    | Times
    | Divide

/// Kaufmännisch auf den Cent, von Hand: gleich unter .NET und Fable.
let private roundToCent (value: decimal) : decimal =
    let cents = System.Math.Floor(abs value * 100m + 0.5m)
    if value < 0m then -cents / 100m else cents / 100m

let private isDigit (c: char) = c >= '0' && c <= '9'

let private digits (text: string) : decimal option =
    if text <> "" && text |> Seq.forall isDigit then
        Some (text |> Seq.fold (fun total c -> total * 10m + decimal (int c - int '0')) 0m)
    else
        None

/// Eine Zahl: Ein Punkt vor genau drei Ziffern ist Tausenderpunkt, sonst Dezimaltrenner. Neben
/// einem Komma sind Punkte immer Tausenderpunkte.
let private parseNumber (text: string) : decimal option =
    let fraction (part: string) =
        digits part
        |> Option.map (fun value -> value / (Seq.replicate part.Length 10m |> Seq.fold (*) 1m))
    let grouped (head: string) (groups: string list) =
        if groups |> List.forall (fun g -> g.Length = 3) then
            digits (head + String.concat "" groups)
        else
            None
    match text.Split ',' |> List.ofArray with
    | [ whole; decimals ] ->
        match whole.Split '.' |> List.ofArray, fraction decimals with
        | head :: groups, Some part -> grouped head groups |> Option.map (fun value -> value + part)
        | _ -> None
    | [ whole ] ->
        match whole.Split '.' |> List.ofArray with
        | [ single ] -> digits single
        | [ head; tail ] when tail.Length <> 3 ->
            match digits head, fraction tail with
            | Some value, Some part -> Some (value + part)
            | _ -> None
        | head :: groups -> grouped head groups
        | [] -> None
    | _ -> None

/// Zerlegt die Eingabe ohne Leerzeichen. `None` bei einem Zeichen, das in keine Rechnung gehört.
let private tokenize (text: string) : Token list option =
    let flush (buffer: string) (tokens: Token list option) =
        if buffer = "" then
            tokens
        else
            match tokens, parseNumber buffer with
            | Some list, Some value -> Some (Number value :: list)
            | _ -> None
    let step (buffer: string, tokens: Token list option) (c: char) =
        let operator token = "", flush buffer tokens |> Option.map (fun list -> token :: list)
        match c with
        | '+' -> operator Plus
        | '-' -> operator Minus
        | '*' | 'x' | 'X' -> operator Times
        | '/' | ':' -> operator Divide
        | _ when isDigit c || c = ',' || c = '.' -> buffer + string c, tokens
        | _ -> "", None
    let buffer, tokens = text |> Seq.fold step ("", Some [])
    flush buffer tokens |> Option.map List.rev

/// Rechnet eine Punkt-Rechnung ab. Es wechseln Zahl und Operator, sonst `None`: `5*-2` und `2**3`
/// sind keine Rechnung. Liefert das Produkt und den Rest ab dem nächsten Strich.
let rec private product (value: decimal) (tokens: Token list) : (decimal * Token list) option =
    match tokens with
    | Times :: Number factor :: rest -> product (value * factor) rest
    | Divide :: Number divisor :: rest when divisor <> 0m -> product (value / divisor) rest
    | Times :: _
    | Divide :: _ -> None
    | _ -> Some (value, tokens)

let rec private sum (total: decimal) (tokens: Token list) : decimal option =
    match tokens with
    | [] -> Some total
    | Plus :: Number value :: rest ->
        product value rest |> Option.bind (fun (term, rest') -> sum (total + term) rest')
    | Minus :: Number value :: rest ->
        product value rest |> Option.bind (fun (term, rest') -> sum (total - term) rest')
    | _ -> None

/// Der Wert der Eingabe, gerundet auf den Cent. `baseValue` ist der Stand. `None` heißt: keine
/// gültige Rechnung. Wirft nie, auch nicht bei einer Eingabe, die `decimal` überliefe.
let evaluateAmount (baseValue: decimal) (text: string) : decimal option =
    try
        let compact = text.Replace(" ", "")
        let result =
            match tokenize compact with
            | None
            | Some [] -> None
            | Some (Number first :: rest) ->
                product first rest |> Option.bind (fun (term, rest') -> sum term rest')
            | Some tokens ->
                // Ein Operator vorn: der Stand ist die erste Zahl.
                product baseValue tokens |> Option.bind (fun (term, rest') -> sum term rest')
        result
        |> Option.map roundToCent
        |> Option.filter (fun value -> abs value <= maxAmount)
    with _ ->
        None

/// Ein Betrag fürs Feld, mit Vorzeichen: "-40,00". `formatAmountForInput` nimmt den Betrag.
let formatSignedAmount (amount: decimal) : string =
    let cents = int64 (System.Math.Floor(abs amount * 100m + 0.5m))
    let sign = if amount < 0m && cents > 0L then "-" else ""
    let rest = cents % 100L
    let padded = if rest < 10L then $"0{rest}" else string rest
    $"{sign}{cents / 100L},{padded}"
