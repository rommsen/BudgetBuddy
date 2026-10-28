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

/// Die Obergrenze in Euro. Nicht gesetzt, nicht lesbar oder nicht positiv: die Vorgabe.
/// Akzeptiert Punkt als Dezimaltrenner (`500`, `250.50`).
let resolveBotMaxAmountEur (currentValue: string) : decimal =
    match System.Decimal.TryParse(
            (if isNull currentValue then "" else currentValue.Trim()),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture) with
    | true, d when d > 0m -> d
    | _ -> defaultBotMaxAmountEur
