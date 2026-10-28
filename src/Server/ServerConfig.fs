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

/// Die Obergrenze in Euro. Nicht gesetzt, nicht lesbar oder nicht positiv: die Vorgabe
/// (500 EUR). Ein größerer Wert wird auf `hardBotMaxAmountEur` gedeckelt.
let resolveBotMaxAmountEur (currentValue: string) : decimal =
    match tryParseBotMaxAmount currentValue with
    | Some d -> min d hardBotMaxAmountEur
    | None -> defaultBotMaxAmountEur

/// Der Logtext für den Start: nennt den wirksamen Wert. Bei gedeckeltem oder ungültigem
/// Rohwert nennt er zusätzlich den gelesenen Rohwert. Ein nicht gesetzter Wert ist die
/// Vorgabe und braucht keinen Hinweis. Das Token kommt hier nie vor.
let botMaxAmountLogLine (currentValue: string) : string =
    let effective = resolveBotMaxAmountEur currentValue
    let eff = effective.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
    let raw = if isNull currentValue then "" else currentValue.Trim()
    if raw = "" then
        sprintf "Bot-Schnittstelle: Obergrenze je Buchung %s EUR (Vorgabe)." eff
    else
        match tryParseBotMaxAmount raw with
        | None ->
            sprintf "Bot-Schnittstelle: Obergrenze je Buchung %s EUR. %s=\"%s\" ist ungültig (nur Dezimalpunkt, positiv), die Vorgabe gilt." eff botMaxAmountVariable raw
        | Some d when d > hardBotMaxAmountEur ->
            sprintf "Bot-Schnittstelle: Obergrenze je Buchung %s EUR. %s=\"%s\" liegt über dem harten Deckel und wird gedeckelt." eff botMaxAmountVariable raw
        | Some _ ->
            sprintf "Bot-Schnittstelle: Obergrenze je Buchung %s EUR." eff
