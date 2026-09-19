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
