module Server.ServerConfig

/// Der bisherige Standardport, wenn ASPNETCORE_URLS nicht gesetzt ist
/// (infra-jh7g9: der Port muss aus der Konfiguration kommen, das
/// Standardverhalten ohne gesetzte Variable aber unverändert bleiben).
let defaultUrls = "http://0.0.0.0:5081"

/// Reine Auflösung: liefert den übergebenen Wert von ASPNETCORE_URLS zurück,
/// oder defaultUrls, wenn nichts gesetzt ist (null oder leerer String).
/// Bewusst als reine Funktion des übergebenen Werts statt eines direkten
/// Environment.GetEnvironmentVariable-Aufrufs, damit sie ohne Prozess-
/// Umgebung testbar ist.
let resolveUrls (currentValue: string) =
    if System.String.IsNullOrEmpty(currentValue) then defaultUrls else currentValue

// ============================================
// Bot-Schnittstelle (ledger-b0b7q, ADR 0066)
// ============================================

/// Name der Umgebungsvariable mit dem Bearer-Token der `/bot/`-Routen.
[<Literal>]
let botApiTokenVariable = "BOT_API_TOKEN"

/// Name der Umgebungsvariable mit der Betragsobergrenze (Euro) einer Bot-Buchung.
[<Literal>]
let botMaxAmountVariable = "BOT_MAX_AMOUNT_EUR"

/// Die Vorgabe der Obergrenze: 500 EUR (ADR 0066).
let defaultBotMaxAmountEur = 500m

/// Das Token aus der Umgebung. Leer oder nur Leerraum heißt: nicht gesetzt, die Routen
/// bleiben abgeschaltet (nie offen).
let resolveBotToken (currentValue: string) : string option =
    if System.String.IsNullOrWhiteSpace currentValue then None else Some (currentValue.Trim())

/// Harter Deckel im Code (ADR 0068 Punkt 1): höchstens 500 EUR je Buchung. Die Umgebung
/// darf die Obergrenze nur absenken, nie über diesen Wert heben.
let hardBotMaxAmountEur = 500m

/// Liest den Rohwert streng: nur Ziffern mit optionalem Dezimalpunkt (`500`, `250.50`),
/// kein Tausendertrennzeichen, kein Komma, kein Vorzeichen. Nicht lesbar oder nicht
/// positiv ergibt None.
let private tryParseBotMaxAmount (currentValue: string) : decimal option =
    match System.Decimal.TryParse(
            (if isNull currentValue then "" else currentValue.Trim()),
            System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture) with
    | true, d when d > 0m -> Some d
    | _ -> None

/// Was `BOT_MAX_AMOUNT_EUR` für die Obergrenze je Bot-Buchung ergibt (ADR 0068 Punkt 1,
/// Nachtrag 2026-11-10).
type BotMaxAmount =
    /// Nicht gesetzt (leer oder nur Leerraum): es gilt die Vorgabe (500 EUR).
    | Default
    /// Gesetzt und gültig, höchstens `hardBotMaxAmountEur`: dieser Wert gilt.
    | Lowered of decimal
    /// Gesetzt und gültig, aber über `hardBotMaxAmountEur`: es gilt der Deckel.
    | Capped of raw: string
    /// Gesetzt, aber ungültig (Komma, Vorzeichen, Tausendertrennzeichen, 0, Text): Bob ist
    /// gesperrt. Ein Rückfall auf die Vorgabe gäbe einem Tippfehler beim Absenken die
    /// höchste Grenze.
    | Invalid of raw: string

/// Liest `BOT_MAX_AMOUNT_EUR` (Rohwert, Leerraum wird getrimmt).
let resolveBotMaxAmountEur (currentValue: string) : BotMaxAmount =
    let raw = if isNull currentValue then "" else currentValue.Trim()
    if raw = "" then
        Default
    else
        match tryParseBotMaxAmount raw with
        | Some d when d > hardBotMaxAmountEur -> Capped raw
        | Some d -> Lowered d
        | None -> Invalid raw

/// Die wirksame Obergrenze in Euro. `None` heißt: Bob ist gesperrt.
let effectiveBotMaxAmountEur (limit: BotMaxAmount) : decimal option =
    match limit with
    | Default -> Some defaultBotMaxAmountEur
    | Lowered d -> Some d
    | Capped _ -> Some hardBotMaxAmountEur
    | Invalid _ -> None

/// Der Grund, mit dem die Bot-Schnittstelle jede Anfrage ablehnt, solange
/// `BOT_MAX_AMOUNT_EUR` ungültig ist. Ohne Rohwert, der steht nur im Startlog.
let botBlockedReason = sprintf "Bot gesperrt: %s ist ungültig" botMaxAmountVariable

/// Der Logtext für den Start: nennt den wirksamen Wert. Bei gedeckeltem Rohwert nennt er
/// zusätzlich den gelesenen Rohwert, bei ungültigem Rohwert diesen und die Sperre. Ein nicht
/// gesetzter Wert ist die Vorgabe und braucht keinen Hinweis.
/// Bekommt nur den Rohwert von `BOT_MAX_AMOUNT_EUR`, das Token kann hier nicht vorkommen.
let botMaxAmountLogLine (currentValue: string) : string =
    let format (d: decimal) = d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
    match resolveBotMaxAmountEur currentValue with
    | Invalid raw ->
        sprintf "Bot-Schnittstelle: %s=\"%s\" ist ungültig (nur Dezimalpunkt, positiv). Bob ist gesperrt, bis der Wert korrigiert ist." botMaxAmountVariable raw
    | Default ->
        sprintf "Bot-Schnittstelle: Obergrenze je Buchung %s EUR (Vorgabe)." (format defaultBotMaxAmountEur)
    | Lowered d ->
        sprintf "Bot-Schnittstelle: Obergrenze je Buchung %s EUR." (format d)
    | Capped raw ->
        sprintf "Bot-Schnittstelle: Obergrenze je Buchung %s EUR. %s=\"%s\" liegt über dem harten Deckel und wird gedeckelt." (format hardBotMaxAmountEur) botMaxAmountVariable raw

/// Die Zeitzone Europe/Berlin aus der Zeitzonendatenbank des Systems, einmal beim Start
/// aufgelöst (`Program.fs`) und als Abhängigkeit an die Bot-Schnittstelle gegeben. Fehlt sie
/// (Image ohne tzdata), ist das ein Fehler mit Hinweis, kein stiller Ersatz durch eigene Regeln.
let tryResolveBerlinZone () : Result<System.TimeZoneInfo, string> =
    try Ok (System.TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")
    with e ->
        Error $"Zeitzone Europe/Berlin nicht gefunden (tzdata im Image installiert?): {e.Message}"
