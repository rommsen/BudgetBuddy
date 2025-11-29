# BankSync für YNAB - Produktspezifikation

## Vision

Eine Self-Hosted Web-Applikation, die Banktransaktionen aus verschiedenen Quellen (initial Comdirect) ausliest und mit manueller Bestätigung und intelligenter Kategorisierung nach YNAB überträgt. Der Fokus liegt auf Sicherheit (reguläres TAN-Verfahren), Transparenz und der Möglichkeit, unklare Transaktionen (Amazon, PayPal) vor dem Import aufzulösen.

## Kernkonzept

Der Benutzer startet einen "Sync-Flow", bei dem Transaktionen abgerufen, automatisch kategorisiert, bei Bedarf manuell angereichert und erst nach expliziter Bestätigung zu YNAB übertragen werden.

---

## Features nach Priorität

### Phase 1: Grundlagen

#### F1.1 - Comdirect-Anbindung
- OAuth-Flow mit regulärem TAN-Verfahren der Comdirect
- Abruf von Kontotransaktionen über die Comdirect API
- Sichere Speicherung von Session-Tokens (nicht dauerhaft, nur für aktive Session)

#### F1.2 - YNAB-Anbindung
- Authentifizierung via YNAB Personal Access Token
- Abruf von Budgets und Kategorien mit deren UUIDs
- Schreiben von Transaktionen zu einem ausgewählten Budget

#### F1.3 - Basis-Sync-Flow
- Manueller Start des Sync-Prozesses durch den Benutzer
- Anzeige aller neuen Transaktionen in einer Übersichtsliste
- Manuelle Zuweisung von Kategorien pro Transaktion
- Review-Schritt vor dem finalen Import
- Batch-Import aller bestätigten Transaktionen nach YNAB

#### F1.4 - Einstellungen und Credentials
- Sichere Eingabe und Speicherung von API-Keys (YNAB)
- Konfiguration des Comdirect-Kontos
- Auswahl des Ziel-Budgets in YNAB

---

### Phase 2: Automatische Kategorisierung

#### F2.1 - Regel-Engine
- Definition von Kategorisierungsregeln mit:
  - Pattern (RegEx oder einfacher Text-Match)
  - Feld, auf das gematcht wird (Empfänger, Verwendungszweck, etc.)
  - Ziel-Kategorie (YNAB UUID)
  - Optionaler Payee-Override für YNAB
- CRUD-Interface für Regeln
- Prioritätsreihenfolge bei mehreren Matches

#### F2.2 - Auto-Kategorisierung im Flow
- Automatische Anwendung der Regeln auf neue Transaktionen
- Visuelle Unterscheidung zwischen auto-kategorisierten und manuellen Einträgen
- Möglichkeit, Auto-Kategorisierung zu überschreiben
- Optional: Lernen aus manuellen Korrekturen (Regel-Vorschlag)

#### F2.3 - Regel-Import/Export
- Export der Regeln als JSON für Backup
- Import von Regel-Sets

---

### Phase 3: Externe Transaktions-Auflösung

#### F3.1 - Amazon-Integration
- Erkennung von Amazon-Transaktionen via Pattern
- Generierung eines Deep-Links zur Amazon-Bestellhistorie (gefiltert nach Datum/Betrag wenn möglich)
- Inline-Anzeige des Links im Sync-Flow
- Manuelle Eingabe der tatsächlichen Kategorie nach Prüfung

#### F3.2 - PayPal-Integration
- Erkennung von PayPal-Transaktionen
- Optional: PayPal API-Anbindung zum Abruf der Original-Transaktion
- Fallback: Deep-Link zur PayPal-Aktivitätsübersicht
- Anzeige des ursprünglichen Empfängers/Händlers im Flow

#### F3.3 - Generisches External-Link-System
- Konfigurierbare Link-Templates für andere Dienste
- Pattern-basierte Erkennung (z.B. "APPLE.COM" → App Store Käufe)
- Hinterlegung von hilfreichen URLs pro Pattern

---

### Phase 4: Erweiterungen

#### F4.1 - Weitere Banken
- Abstraktion des Bank-Adapters
- Vorbereitung für weitere PSD2-APIs
- Dokumentation für zukünftige Bank-Integrationen

#### F4.2 - Duplikat-Erkennung
- Abgleich mit bereits in YNAB vorhandenen Transaktionen
- Warnung bei potenziellen Duplikaten
- Option zum Überspringen oder Mergen

#### F4.3 - Transaktions-Historie
- Lokale Speicherung bereits synchronisierter Transaktionen
- Anzeige vergangener Sync-Läufe
- Möglichkeit, Kategorisierungen nachzuvollziehen

#### F4.4 - Split-Transaktionen
- Aufteilung einer Transaktion auf mehrere Kategorien
- Besonders relevant für Amazon-Bestellungen mit mehreren Artikeln

---

## Technische Rahmenbedingungen

### Deployment
- Self-hosted als Docker-Container
- Läuft im lokalen Netzwerk oder via Tailscale erreichbar
- Keine Cloud-Abhängigkeit für sensible Daten

### Sicherheit
- Keine dauerhafte Speicherung von Bank-Credentials
- TAN-Verfahren wird bei jedem Sync durchlaufen
- YNAB-Token verschlüsselt gespeichert
- HTTPS-only

### Datenhaltung
- SQLite für Regeln, Einstellungen und Historie
- Keine Speicherung von Transaktionsdaten länger als für den aktiven Flow nötig

### Vorgeschlagener Tech-Stack
- Backend: F# mit Giraffe
- Frontend: F# Elmish.React mit Feliz
- Datenbank: SQLite
- Containerisierung: Docker
- Netzwerk: Tailscale-kompatibel

---

## Benutzerfluss (Hauptszenario)

1. Benutzer öffnet App und startet "Neuer Sync"
2. Comdirect-Login mit TAN-Bestätigung
3. App ruft neue Transaktionen ab
4. Regel-Engine kategorisiert automatisch wo möglich
5. Benutzer sieht Liste mit Farbcodierung:
   - **Grün**: Auto-kategorisiert
   - **Gelb**: Braucht Aufmerksamkeit (Amazon, PayPal, etc.)
   - **Rot**: Keine Regel, manuelle Eingabe nötig
6. Bei gelben Einträgen: Externe Links werden angezeigt, Benutzer prüft und wählt Kategorie
7. Review aller Transaktionen
8. Bestätigung und Batch-Import zu YNAB
9. Erfolgsmeldung mit Zusammenfassung

---

## Datenmodell (Konzept)

### Transaction
```
- id: string (unique)
- date: DateTime
- amount: decimal
- payee: string
- memo: string
- sourceAccount: string
- rawData: JSON (original API response)
- status: Pending | Categorized | Imported
- categoryId: string? (YNAB UUID)
- matchedRule: string? (Rule ID)
- requiresAttention: bool
- externalLinks: string[]
```

### Rule
```
- id: string
- name: string
- pattern: string
- patternType: Regex | Contains | Exact
- targetField: Payee | Memo | Combined
- categoryId: string (YNAB UUID)
- payeeOverride: string?
- priority: int
- enabled: bool
```

### SyncSession
```
- id: string
- startedAt: DateTime
- completedAt: DateTime?
- transactionCount: int
- importedCount: int
- status: InProgress | Completed | Failed
```

---

## API-Endpunkte (Konzept)

### Auth & Config
- `GET /api/config` - Aktuelle Konfiguration abrufen
- `PUT /api/config` - Konfiguration aktualisieren
- `POST /api/comdirect/auth` - Comdirect OAuth starten
- `POST /api/comdirect/tan` - TAN-Bestätigung

### Sync Flow
- `POST /api/sync/start` - Neuen Sync starten
- `GET /api/sync/{id}/transactions` - Transaktionen des aktuellen Syncs
- `PUT /api/sync/{id}/transactions/{txId}` - Transaktion kategorisieren
- `POST /api/sync/{id}/import` - Batch-Import nach YNAB

### Rules
- `GET /api/rules` - Alle Regeln abrufen
- `POST /api/rules` - Neue Regel erstellen
- `PUT /api/rules/{id}` - Regel aktualisieren
- `DELETE /api/rules/{id}` - Regel löschen
- `POST /api/rules/export` - Regeln exportieren
- `POST /api/rules/import` - Regeln importieren

### YNAB
- `GET /api/ynab/budgets` - Verfügbare Budgets
- `GET /api/ynab/categories/{budgetId}` - Kategorien eines Budgets

---

## Offene Fragen / Zu klären

1. **Comdirect API**: Welche genauen Scopes und Endpoints werden benötigt? Gibt es Rate Limits?
2. **TAN-Flow**: Wie wird der TAN-Dialog in der Web-UI abgebildet? Redirect oder Inline?
3. **Verschlüsselung**: Welcher Algorithmus für die lokale Token-Speicherung?
4. **PayPal API**: Ist eine PayPal Business Account nötig oder reicht Personal?
5. **Amazon**: Gibt es eine offizielle API oder nur Scraping/Deep-Links?

---

## Nicht im Scope (bewusst ausgeklammert)

- Automatische Hintergrund-Syncs (immer manuell mit TAN)
- Multi-User-Support (Single-User-App)
- Mobile App (nur Web, responsive)
- Bank-Aggregator-Services (FinTS, etc.) - nur direkte Bank-APIs
- Budgetplanung in der App selbst (dafür wird YNAB genutzt)
