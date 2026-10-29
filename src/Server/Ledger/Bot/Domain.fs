/// Die Fachregeln der Bot-Schnittstelle (ADR 0066, 0067, 0068): Richtung und geprüfte Eingabe
/// einer Bob-Buchung, Datumsfenster, Tagesgrenze, Berliner Kalendertag und Kategorie-Auflösung.
///
/// Rein, ohne Uhr und ohne Zugriff auf die Zeitzonendatenbank: `today`, der Zeitpunkt und die
/// Zeitzone Europe/Berlin kommen als Parameter. Die Zeitzone wird einmal beim Start aufgelöst
/// (`Server.ServerConfig.tryResolveBerlinZone`, Abbruch mit Hinweis in `Program.fs`).
/// Die reine Eingabeprüfung (Form, Typen, Pflichtfelder) bleibt in `Server.Validation`.
module Ledger.Bot.Domain

open System
open Shared.Domain

// ============================================
// Bob-Buchung
// ============================================

/// Die Richtung einer Bot-Buchung. Kommt allein aus `direction`, nie aus dem Vorzeichen
/// des Betrags.
type BotDirection =
    | BotOutflow
    | BotInflow

/// Die geprüfte Eingabe der Vorschau. `AmountEur` ist positiv und in Euro (nicht in
/// YNAB-Milliunits). Es gibt bewusst kein Konto-Feld: das Konto ist immer das Quick-Add-Konto
/// (ADR 0004).
type BotPreviewInput = {
    AmountEur: decimal
    Direction: BotDirection
    Payee: string
    /// Rohtext aus der Anfrage: Id oder Name, aufgelöst durch `resolveBotCategory`.
    Category: string
    Date: DateTime
    Memo: string option
}

// ============================================
// Datumsfenster (ADR 0068, Punkt 3)
// ============================================

/// Wie weit das Buchungsdatum einer Bob-Buchung höchstens zurückliegen darf (ADR 0068,
/// Punkt 3). Das Fenster reicht von heute minus 31 Tage bis heute.
let botBackdateDays = 31

/// Prüft das Buchungsdatum gegen das Fenster. `today` ist der Berliner Kalendertag (nur sein
/// Datum zählt). Der Fehler ist die Meldung zum Feld `date`.
let checkBotBookingDate (today: DateTime) (date: DateTime) : Result<unit, string> =
    if date.Date > today.Date then Error "Das Datum liegt in der Zukunft"
    elif date.Date < today.Date.AddDays(float -botBackdateDays) then
        Error $"Das Datum darf höchstens {botBackdateDays} Tage zurückliegen"
    else Ok ()

// ============================================
// Berliner Kalendertag
// ============================================

/// Der Berliner Offset zu einem UTC-Zeitpunkt aus der übergebenen Zeitzone.
let berlinOffsetAt (berlinZone: TimeZoneInfo) (utc: DateTime) : TimeSpan =
    berlinZone.GetUtcOffset(DateTime.SpecifyKind(utc.ToUniversalTime(), DateTimeKind.Utc))

/// Der Berliner Kalendertag (ohne Uhrzeit) zu einem UTC-Zeitpunkt.
let berlinDayOf (berlinZone: TimeZoneInfo) (utc: DateTime) : DateTime =
    (utc.ToUniversalTime() + berlinOffsetAt berlinZone utc).Date

/// Beginn (einschließlich) und Ende (ausschließlich) des Berliner Kalendertags, der den
/// UTC-Zeitpunkt enthält, als UTC-Zeitpunkte.
let berlinDayBoundsUtc (berlinZone: TimeZoneInfo) (utc: DateTime) : DateTime * DateTime =
    let day = berlinDayOf berlinZone utc
    let toUtc (localMidnight: DateTime) =
        let guess = DateTime.SpecifyKind(localMidnight, DateTimeKind.Utc)
        DateTime.SpecifyKind(localMidnight - berlinOffsetAt berlinZone (guess.AddHours -1.5), DateTimeKind.Utc)
    toUtc day, toUtc (day.AddDays 1.0)

// ============================================
// Tagesgrenze (ADR 0068, Punkt 2)
// ============================================

/// Tagesgrenze je Kalendertag (Europe/Berlin), Summe der Beträge ohne Vorzeichen in Euro
/// und Anzahl gebuchter Bob-Commits (ADR 0068, Punkt 2). Feste Werte im Code.
let botDailyMaxSumEur = 500m
let botDailyMaxCount = 10

/// Prüft die Tagesgrenze: `usedSumEur` und `usedCount` sind die heute schon gebuchten
/// Bob-Buchungen. Die Meldung nennt Summe und Anzahl des Tages.
let checkBotDailyLimit (usedSumEur: decimal) (usedCount: int) (amountEur: decimal) : Result<unit, string> =
    if usedCount + 1 > botDailyMaxCount || usedSumEur + amountEur > botDailyMaxSumEur then
        let inv (d: decimal) = d.ToString("0.00", Globalization.CultureInfo.InvariantCulture)
        Error
            $"Tagesgrenze für Bob erreicht: heute sind schon {inv usedSumEur} EUR in {usedCount} Buchungen gebucht, erlaubt sind höchstens {inv botDailyMaxSumEur} EUR und {botDailyMaxCount} Buchungen je Kalendertag"
    else Ok ()

// ============================================
// Kategorie-Auflösung
// ============================================

/// Kategorien, die Bob wählen darf: nicht die internen YNAB-Kategorien, nicht versteckte.
let selectableBotCategories (categories: YnabCategory list) : YnabCategory list =
    categories
    |> List.filter (fun c -> c.GroupName <> internalMasterGroupName && not c.Hidden)

/// Ergebnis der Kategorie-Auflösung. BB rät nicht: bei keinem oder mehreren Treffern
/// kommt eine Kandidatenliste statt einer Wahl.
type BotCategoryResolution =
    | BotCategoryResolved of YnabCategory
    | BotCategoryUnknown of candidates: YnabCategory list
    | BotCategoryAmbiguous of candidates: YnabCategory list

/// Löst `query` (Id oder Name, ohne Beachtung der Groß-/Kleinschreibung) gegen die wählbaren
/// Kategorien auf. Genau ein Treffer wird übernommen. Ohne Treffer sind die Kandidaten die
/// Kategorien, deren Name den Suchtext enthält (oder umgekehrt).
let resolveBotCategory (categories: YnabCategory list) (query: string) : BotCategoryResolution =
    let selectable = selectableBotCategories categories
    let q = query.Trim()
    let byId =
        match Guid.TryParse q with
        | true, g -> selectable |> List.filter (fun c -> c.Id = YnabCategoryId g)
        | _ -> []
    let byName () =
        selectable |> List.filter (fun c -> String.Equals(c.Name.Trim(), q, StringComparison.OrdinalIgnoreCase))
    match byId with
    | [ one ] -> BotCategoryResolved one
    | _ ->
        match byName () with
        | [ one ] -> BotCategoryResolved one
        | [] ->
            let lower = q.ToLowerInvariant()
            let close =
                selectable
                |> List.filter (fun c ->
                    let name = c.Name.ToLowerInvariant()
                    lower <> "" && (name.Contains lower || lower.Contains name))
            BotCategoryUnknown close
        | many -> BotCategoryAmbiguous many
