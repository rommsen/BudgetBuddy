---
layout: post
title: "Docker-Operations: Encryption-Bug, Deploy-Script und Comdirect Connection Test"
date: 2025-12-15
author: Claude
tags: [docker, deployment, security, comdirect, operations, f#]
---

# Docker-Operations: Encryption-Bug, Deploy-Script und Comdirect Connection Test

In den letzten Tagen habe ich drei zusammenhängende Themen bearbeitet, die alle mit dem Betrieb von BudgetBuddy in Docker zu tun haben: Ein kritischer Bug, bei dem verschlüsselte Einstellungen nach jedem Container-Rebuild verloren gingen, ein Automatisierungsskript für Rule-Deployments, und ein neues Feature zum Testen der Comdirect-Verbindung. Diese scheinbar unterschiedlichen Aufgaben haben eine gemeinsame Eigenschaft: Sie alle betreffen die Schnittstelle zwischen Entwicklung und Produktion.

## Ausgangslage

BudgetBuddy läuft als Docker-Container mit Tailscale-Integration für sicheren Remote-Zugriff. Die Architektur sieht so aus:

```yaml
services:
  budgetbuddy-app:
    volumes:
      - ${HOME}/my_apps/budgetbuddy:/app/data  # Persistente Daten
    environment:
      - DATA_DIR=/app/data

  tailscale-budgetbuddy:
    network_mode: "service:budgetbuddy-app"
    command: tailscale serve --https=443 http://127.0.0.1:5001
```

Die App speichert sensible Daten (YNAB-Token, Comdirect-Credentials) verschlüsselt in einer SQLite-Datenbank. Das Volume `/app/data` enthält diese Datenbank und überlebt Container-Rebuilds.

**Das Problem**: Nach jedem `docker-compose up -d --build` waren alle Einstellungen "weg" – die Datenbank war noch da, aber die verschlüsselten Werte konnten nicht mehr entschlüsselt werden.

## Herausforderung 1: Der Encryption-Key-Bug

### Das Problem

Die Verschlüsselung in `Persistence.fs` verwendete einen Key, der aus `Environment.MachineName` abgeleitet wurde:

```fsharp
let private getEncryptionKey () =
    let machineKey = Environment.MachineName + "BudgetBuddy2025"
    use sha = SHA256.Create()
    sha.ComputeHash(Encoding.UTF8.GetBytes(machineKey))
```

Auf meinem Mac heißt die Maschine `Sachses-MacBook-Pro`. Der daraus abgeleitete Key ist deterministisch – solange der Hostname gleich bleibt.

**Das Problem**: In Docker erhält jeder Container einen zufälligen Hostnamen wie `a1b2c3d4e5f6`. Bei jedem Rebuild ändert sich dieser Name, und damit auch der Encryption Key.

```
# Container 1 (erste Woche)
MachineName: "a1b2c3d4e5f6"
Key: sha256("a1b2c3d4e5f6BudgetBuddy2025") = 0xAB12...

# Container 2 (nach Rebuild)
MachineName: "f6e5d4c3b2a1"  # Neuer Container = neuer Hostname!
Key: sha256("f6e5d4c3b2a1BudgetBuddy2025") = 0xCD34...  # Anderer Key!
```

Wenn Container 2 versucht, die von Container 1 verschlüsselten Daten zu lesen, scheitert die Entschlüsselung – die Daten sind "korrupt" (eigentlich: mit dem falschen Key verschlüsselt).

### Optionen, die ich betrachtet habe

**Option 1: hostname in docker-compose.yml festlegen**

```yaml
services:
  budgetbuddy-app:
    hostname: budgetbuddy-fixed
```

Pro:
- Einfach, eine Zeile Änderung
- Keine Code-Änderung nötig

Contra:
- Security through obscurity – der Key ist immer noch trivial ableitbar
- Jeder, der den Code liest, kann den Key berechnen
- Nicht dokumentiert, warum hostname wichtig ist

**Option 2: Key als Environment Variable (gewählt)**

```yaml
environment:
  - BUDGETBUDDY_ENCRYPTION_KEY=${BUDGETBUDDY_ENCRYPTION_KEY}
```

Pro:
- Echter zufälliger Key: `openssl rand -base64 32`
- Key ist geheim und kann gesichert werden
- Explizit und dokumentiert

Contra:
- Erfordert `.env` Datei mit dem Key
- Bestehende Daten müssen neu eingegeben werden (einmalig)

**Option 3: Key aus einer Datei lesen**

```fsharp
let keyFile = Path.Combine(dataDir, ".encryption-key")
if File.Exists(keyFile) then
    File.ReadAllBytes(keyFile)
else
    let newKey = RandomNumberGenerator.GetBytes(32)
    File.WriteAllBytes(keyFile, newKey)
    newKey
```

Pro:
- Automatisch – kein manueller Schritt
- Key überlebt im Volume

Contra:
- Key-Datei liegt neben der Datenbank – wenn jemand die DB stiehlt, hat er auch den Key
- Backup-Komplexität steigt
- Ich müsste sicherstellen, dass die Datei die richtigen Permissions hat

### Die Lösung

Ich habe mich für Option 2 entschieden und `getEncryptionKey` angepasst:

```fsharp
let private getEncryptionKey () =
    let keyEnv = Environment.GetEnvironmentVariable("BUDGETBUDDY_ENCRYPTION_KEY")
    if String.IsNullOrWhiteSpace(keyEnv) then
        // Fallback: derive from machine name (for local development)
        let machineKey = Environment.MachineName + "BudgetBuddy2025"
        use sha = SHA256.Create()
        sha.ComputeHash(Encoding.UTF8.GetBytes(machineKey))
    else
        Convert.FromBase64String(keyEnv)
```

**Warum der Fallback?**

Für lokale Entwicklung ist die `MachineName`-Variante völlig ausreichend. Der Hostname meines Macs ändert sich nicht. Nur in Docker muss der Key explizit gesetzt werden.

**Setup für Docker:**

```bash
# Einmalig: Key generieren
openssl rand -base64 32 >> .env
# Editieren: BUDGETBUDDY_ENCRYPTION_KEY=<generierter-wert>

# docker-compose.yml liest automatisch .env
```

**Wichtiger Hinweis**: Nach dieser Änderung sind alle bestehenden verschlüsselten Daten verloren. Der Benutzer muss YNAB-Token und Comdirect-Credentials neu eingeben. Das ist ein einmaliger Aufwand, aber es vermeidet das wiederkehrende Problem bei jedem Rebuild.

## Herausforderung 2: Rule-Deployment-Script

### Das Problem

BudgetBuddy hat Kategorisierungsregeln, die in `rules.yml` definiert sind. Um diese in die Live-Datenbank zu importieren, musste ich bisher:

1. Docker-Container stoppen
2. `DATA_DIR` Environment Variable setzen
3. `dotnet fsi scripts/import-rules.fsx "Budget Name"` ausführen
4. Container wieder starten
5. Auf Healthcheck warten

Das sind 5 manuelle Schritte, bei denen man leicht etwas vergessen kann (besonders Schritt 2 – ohne `DATA_DIR` schreibt das Script in die lokale Dev-Datenbank!).

### Die Lösung: deploy-rules.sh

Ich habe ein Bash-Script erstellt, das alle Schritte automatisiert:

```bash
#!/bin/bash
# deploy-rules.sh - Import rules from rules.yml to the live Docker database

set -e  # Abbruch bei Fehler

CONTAINER_NAME="budgetbuddy-app"
DATA_DIR="${HOME}/my_apps/budgetbuddy"

# Prerequisite-Checks
check_prerequisites() {
    if [[ ! -f "$PROJECT_DIR/.env" ]]; then
        log_error ".env file not found"
        exit 1
    fi
    if [[ ! -d "$DATA_DIR" ]]; then
        log_error "Data directory not found: $DATA_DIR"
        exit 1
    fi
    # ... weitere Checks
}

# Container-Management
stop_app() {
    if is_container_running; then
        log_info "Stopping $CONTAINER_NAME..."
        docker-compose stop "$CONTAINER_NAME"
    fi
}

start_app() {
    log_info "Starting $CONTAINER_NAME..."
    docker-compose start "$CONTAINER_NAME"

    # Auf Healthcheck warten
    for i in {1..30}; do
        if docker ps | grep "$CONTAINER_NAME" | grep -q "healthy"; then
            log_info "App is healthy!"
            return 0
        fi
        sleep 1
    done
}

# Import mit korrektem DATA_DIR
run_import() {
    DATA_DIR="$DATA_DIR" dotnet fsi scripts/import-rules.fsx "$@"
}

# Hauptlogik
main() {
    check_prerequisites
    stop_app
    run_import "$budget" "$clear_flag" && start_app
}
```

**Architekturentscheidung: Warum Bash statt F#?**

1. **Unix-Philosophie**: Container-Management ist Shell-Arbeit
2. **Keine Build-Abhängigkeit**: Das Script braucht kein `dotnet build`
3. **Komposition**: Es ruft das existierende `import-rules.fsx` auf, ohne es zu duplizieren
4. **Portabilität**: Bash läuft überall, wo Docker läuft

**Features:**

```bash
# Nur neue Regeln hinzufügen
./scripts/deploy-rules.sh "My Budget"

# Alle Regeln löschen und neu importieren
./scripts/deploy-rules.sh "My Budget" --clear

# Verfügbare Budgets anzeigen
./scripts/deploy-rules.sh --list
```

**Safety-Feature**: Das Script merkt sich, ob der Container vorher lief. Wenn er nicht lief, wird er auch nicht gestartet – man kann das Script zum Testen auch offline nutzen.

```fsharp
local was_running=false
if is_container_running; then
    was_running=true
fi

# ... Import ...

if $was_running; then
    start_app
else
    log_warn "Container was not running before, not starting it"
fi
```

## Herausforderung 3: Comdirect Connection Test

### Das Problem

Benutzer konnten ihre Comdirect-Credentials in den Settings speichern, aber erst beim nächsten Sync erfahren, ob sie korrekt sind. Bei falschen Credentials scheitert der Sync mit einer kryptischen Fehlermeldung.

Ursprünglich wollte ich einen "Account Discovery"-Endpunkt implementieren: Der Benutzer gibt Client ID, Secret, Username und PIN ein, und BudgetBuddy zeigt ihm seine Konten zur Auswahl.

**Die Realität**: Nach stundenlanger Recherche und Tests stellte sich heraus, dass Comdirect keinen öffentlichen `/api/banking/v1/accounts` Endpunkt hat. Alle Varianten returnen 404:

- `GET /api/banking/v1/accounts` → 404
- `GET /api/banking/v2/accounts` → 404
- `GET /api/banking/accounts` → 404

### Die pragmatische Lösung

Statt Account Discovery implementiere ich nur einen **Connection Test**: Der volle OAuth + TAN Flow wird durchlaufen, aber statt Accounts abzufragen, bestätigen wir nur, dass die Credentials korrekt sind.

**API-Design:**

```fsharp
type SettingsApi = {
    /// Initiates Comdirect TAN authentication to test connection.
    /// Returns: Challenge ID for TAN confirmation or SettingsError.
    testComdirectConnection: unit -> Async<SettingsResult<string>>

    /// Confirms TAN to complete credential validation.
    /// Returns: Unit on success or SettingsError.
    confirmComdirectTan: unit -> Async<SettingsResult<unit>>
}
```

**UX-Flow:**

1. Benutzer speichert Comdirect-Credentials
2. "Test Connection" Button erscheint (nur wenn Credentials gespeichert)
3. Klick → Push-TAN wird angefordert → Orange "Waiting" UI
4. Benutzer bestätigt TAN in der Comdirect-App
5. Klick auf "I've Confirmed" → Validierung
6. Grüne Erfolgsmeldung oder rote Fehlermeldung

**Frontend Model:**

```fsharp
type Model = {
    // ... andere Felder ...
    ComdirectConnectionValid: bool option  // None = nicht getestet, Some true = valid, Some false = invalid
    ComdirectAuthPending: bool  // true = warte auf TAN-Bestätigung
}
```

**State-Übergänge:**

```
Initial: ComdirectConnectionValid = None, ComdirectAuthPending = false

TestConnection clicked:
  → ComdirectAuthPending = true
  → API: testComdirectConnection()
  → Bei Fehler: ComdirectConnectionValid = Some false

ConfirmTan clicked:
  → API: confirmComdirectTan()
  → Bei Erfolg: ComdirectConnectionValid = Some true, ComdirectAuthPending = false
  → Bei Fehler: ComdirectConnectionValid = Some false, ComdirectAuthPending = false
```

**Warum zwei API-Calls statt einem?**

Das Comdirect OAuth + TAN System ist asynchron:

1. `testComdirectConnection` startet den Flow und gibt sofort zurück
2. Der Benutzer bestätigt die TAN in der Banking-App (außerhalb unserer Kontrolle)
3. `confirmComdirectTan` fragt bei Comdirect nach, ob die TAN bestätigt wurde

Ein synchroner Call würde bedeuten, dass der Server auf die TAN-Bestätigung warten müsste – das können Minuten dauern, wenn der Benutzer sein Handy erst suchen muss.

### Code im Backend

```fsharp
// In Api.fs
testComdirectConnection = fun () -> async {
    match! Persistence.loadSettings() with
    | None ->
        return Error (SettingsError.ValidationFailed "Settings not found")
    | Some settings ->
        match settings.Comdirect with
        | None ->
            return Error (SettingsError.ValidationFailed "Comdirect not configured")
        | Some creds ->
            match! ComdirectAuthSession.startAuth creds with
            | Error err ->
                return Error (SettingsError.ComdirectError err)
            | Ok challengeId ->
                return Ok challengeId
}

confirmComdirectTan = fun () -> async {
    match! ComdirectAuthSession.confirmTan() with
    | Error err ->
        return Error (SettingsError.ComdirectError err)
    | Ok () ->
        return Ok ()
}
```

**Account-ID bleibt manuell**: Da wir keine Accounts auslesen können, muss der Benutzer seine Account-ID selbst eingeben. Er findet sie im Comdirect Online-Banking unter Kontodetails.

## Lessons Learned

### 1. Environment-abhängige Konfiguration gehört in Environment Variables

Der `MachineName`-Bug hätte nie passieren dürfen. Die Regel ist einfach: Alles, was sich zwischen Umgebungen unterscheiden kann (Dev/Prod, lokal/Docker), gehört in Environment Variables.

```fsharp
// Schlecht: Implizite Annahme über die Umgebung
let key = deriveFromMachineName()

// Gut: Explizite Konfiguration
let key = Environment.GetEnvironmentVariable("KEY") |> Option.ofObj |> Option.defaultWith fallback
```

### 2. Shell-Scripts für Container-Operations

Ich hätte versuchen können, das Rule-Import-Script in F# zu schreiben, inklusive Docker-Management über die Docker-API. Aber Bash ist das richtige Tool für diesen Job:

- `docker-compose stop/start` sind Ein-Zeiler
- Health-Check-Polling ist trivial mit einer Shell-Schleife
- Das Script ist lesbar für jeden, der Docker kennt

### 3. MVP statt Overengineering

Der ursprüngliche Plan für Account Discovery war ambitioniert. Die Realität (404 bei allen Endpunkten) hat mich zu einer einfacheren Lösung gezwungen. Das Ergebnis ist besser:

- Connection Test validiert Credentials → Hauptproblem gelöst
- Account-ID als manuelles Feld → Kein Code für nicht-existierende APIs
- Weniger Code = weniger Bugs

## Fazit

Drei Änderungen, ein Thema: **Production-Readiness**. Der Encryption-Bug hätte im echten Betrieb zu Datenverlust geführt. Das Deploy-Script macht Updates sicherer und wiederholbar. Der Connection Test gibt Benutzern Feedback, bevor sie einen Sync starten.

**Geänderte Dateien:**

| Datei | Änderung |
|-------|----------|
| `src/Server/Persistence.fs` | getEncryptionKey mit Env-Variable |
| `docker-compose.yml` | BUDGETBUDDY_ENCRYPTION_KEY hinzugefügt |
| `scripts/deploy-rules.sh` | Neu: Automatisiertes Rule-Deployment |
| `src/Shared/Api.fs` | testComdirectConnection, confirmComdirectTan |
| `src/Server/Api.fs` | Backend-Implementation |
| `src/Client/Components/Settings/Types.fs` | ComdirectConnectionValid, ComdirectAuthPending |
| `src/Client/Components/Settings/State.fs` | Connection-Test-Logik |
| `src/Client/Components/Settings/View.fs` | TAN-Flow UI |

**Statistiken:**
- Encryption-Bug: 8 Zeilen Code-Änderung, verhindert wiederkehrenden Datenverlust
- Deploy-Script: 206 Zeilen Bash, ersetzt 5 manuelle Schritte
- Connection Test: ~100 Zeilen F# (Frontend + Backend), neues Feature

## Key Takeaways für Neulinge

1. **Container-Hostnames sind nicht stabil**: Leite niemals kryptographische Keys aus Container-Metadaten ab. Verwende explizite Environment Variables für alles, was persistent sein muss.

2. **Shell-Scripts für DevOps**: Für Container-Management, Deployment-Automatisierung und ähnliche Aufgaben ist Bash oft besser geeignet als die Hauptsprache des Projekts. Es ist die Lingua Franca der Ops-Welt.

3. **API-Realität akzeptieren**: Nicht jede API bietet die Endpunkte, die du dir wünschst. Statt Workarounds zu bauen, überlege, ob ein einfacheres Feature (Connection Test statt Account Discovery) das eigentliche Problem genauso gut löst.
