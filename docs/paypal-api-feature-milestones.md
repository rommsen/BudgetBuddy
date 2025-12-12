# PayPal Transaktionsdetails Feature - Forschungsergebnisse & Milestone-Plan

## Forschungsergebnisse

### Ausgangssituation

PayPal-Transaktionen auf Bankkontoauszügen enthalten kryptische Referenzen:

```
011046773997197/PP.8078.PP/. Zalando Payments GmbH, Ihr Einkauf bei Zalando Payments GmbH
End-to-End-Ref.: 1046773997197
CORE / Mandatsref.: 5JJJ2253Q3P8Q
Gläubiger-ID: LU96ZZZ0000000000000000058
```

**Ziel:** Diese Referenzen nutzen, um automatisch Details aus PayPal zu holen.

### API-Analyse

#### PayPal Transaction Search API

**Endpoint:** `GET /v1/reporting/transactions`

**Suchbare Parameter:**
| Parameter | Beschreibung |
|-----------|--------------|
| `transaction_id` | PayPal's eigene 17-stellige ID (z.B. `93DJ2231ADD35672D`) |
| `start_date` / `end_date` | Zeitraum (max 31 Tage) |
| `transaction_amount` | Betragsrange `[500 TO 1000]` |
| `transaction_currency` | ISO-4217 Code |
| `transaction_status` | D (Denied), P (Pending), S (Success), V (Reversed) |

**NICHT suchbare Parameter (kritisch!):**
- Mandatsreferenz (z.B. `5JJJ2253Q3P8Q`)
- End-to-End-Referenz (z.B. `1046773997197`)
- SEPA-spezifische Felder
- Bank Statement Referenzen

#### Das Kernproblem

Die Referenznummern im Bank-Memo **korrespondieren NICHT** mit PayPal Transaction IDs:

| Bank-Memo Referenz | PayPal Transaction ID |
|--------------------|-----------------------|
| `1046773997197` (13 Zeichen) | `93DJ2231ADD35672D` (17 Zeichen) |
| `5JJJ2253Q3P8Q` (Mandatsref) | Nicht in API suchbar |
| `PP.8078.PP` | Interne PayPal-Referenz |

**Quelle:** PayPal Community bestätigt, dass SEPA-Abbuchungsreferenzen (PPWD-prefixed) keine direkte Beziehung zu PayPal Transaction IDs haben.

#### API-Einschränkungen

- Transaktionen erscheinen erst **nach 3 Stunden** in der API
- Max **31 Tage** pro API-Request
- Daten nur für **3 Jahre** verfügbar
- Partner Network erforderlich für Third-Party-Zugriff

### Response-Felder der Transaction Search API

```json
{
  "transaction_info": {
    "transaction_id": "93DJ2231ADD35672D",
    "transaction_initiation_date": "2024-01-15T10:30:00Z",
    "transaction_amount": { "currency_code": "EUR", "value": "-25.99" },
    "transaction_status": "S",
    "transaction_subject": "Zalando Order #12345",
    "invoice_id": "INV-12345",
    "custom_field": "...",
    "bank_reference_id": "..." // Nur für ACH (US), nicht SEPA!
  },
  "payer_info": {
    "account_id": "...",
    "email_address": "merchant@example.com",
    "payer_name": { "given_name": "Max", "surname": "Mustermann" }
  },
  "cart_info": {
    "item_details": [
      { "item_name": "Schuhe", "item_quantity": "1", "item_amount": {...} }
    ]
  }
}
```

### Fazit der Forschung

| Aspekt | Bewertung |
|--------|-----------|
| Direkte Suche nach Bank-Referenz | ❌ Nicht möglich |
| Suche nach Mandatsreferenz | ❌ Nicht möglich |
| Suche nach Datum + Betrag | ⚠️ Möglich, aber fehleranfällig |
| Alle Transaktionen laden & lokal matchen | ⚠️ Möglich, aber aufwändig |
| Deep Link zu PayPal Activity | ✅ Einfach umsetzbar |

---

## Option A: PayPal Activity Deep Link (Empfohlen als MVP)

### Beschreibung

Generiere einen direkten Link zur PayPal Activity-Seite mit vorgefiltertem Datum. Der User kann dann mit einem Klick zur richtigen Stelle in PayPal springen.

### Vorteile

- Kein API-Zugang erforderlich
- Sofort umsetzbar
- Keine Authentifizierung zu implementieren
- Funktioniert immer (kein API-Limit, keine Verzögerung)

### Nachteile

- User muss sich noch bei PayPal einloggen (2FA)
- Details nicht direkt in BudgetBuddy sichtbar

### Technische Umsetzung

PayPal Activity URL Format:
```
https://www.paypal.com/myaccount/transactions?filterDate=last90days
https://www.paypal.com/myaccount/transactions?freeTextSearch=SUCHBEGRIFF
https://www.paypal.com/activities/
```

---

## Option B: API-basiertes Transaction Matching

### Beschreibung

Implementiere PayPal API Integration, die:
1. Alle Transaktionen eines Zeitraums lädt
2. Lokal nach Datum + Betrag matcht
3. Details in BudgetBuddy anzeigt

### Vorteile

- Transaktionsdetails direkt in BudgetBuddy
- Kein Wechsel zu PayPal nötig
- Bessere UX nach initialer Einrichtung

### Nachteile

- Komplexe Implementierung (OAuth, Token-Refresh)
- API Credentials erforderlich (Developer Account)
- Matching-Logik fehleranfällig bei mehreren Transaktionen am Tag mit gleichem Betrag
- 3 Stunden Verzögerung bis Transaktionen in API erscheinen
- API Rate Limits

---

## Milestone-Plan

### Phase 1: Quick Win - PayPal Activity Deep Link

**Aufwand:** ~2-4 Stunden

#### Milestone 1.1: PayPal-Erkennung im Memo

**Ziel:** Erkenne PayPal-Transaktionen anhand des Memos

**Tasks:**
1. Regex-Pattern für PayPal-Memos erstellen
   - Pattern: `/PP\.\d+\.PP/` oder `PayPal` oder `PAYPAL`
   - Gläubiger-ID Pattern: `LU96ZZZ0000000000000000058` (PayPal Luxembourg)
2. `isPayPalTransaction` Funktion in `Shared/Domain.fs`
3. Unit Tests für verschiedene Memo-Formate

**Acceptance Criteria:**
- [ ] PayPal-Transaktionen werden zuverlässig erkannt
- [ ] Tests für alle bekannten Memo-Formate

#### Milestone 1.2: Deep Link Generation

**Ziel:** Generiere PayPal Activity Link mit Datumsfilter

**Tasks:**
1. `generatePayPalActivityLink` Funktion
   - Input: Transaction Date
   - Output: URL zur PayPal Activity Seite
2. Link-Button in Transaction Detail View
3. Öffne Link in neuem Tab

**Acceptance Criteria:**
- [ ] Button "In PayPal öffnen" bei PayPal-Transaktionen
- [ ] Link führt zur Activity-Seite mit korrektem Datumsbereich

#### Milestone 1.3: UI Integration

**Ziel:** Integration in bestehende Transaction Details

**Tasks:**
1. PayPal-Icon/Badge für erkannte Transaktionen
2. "Open in PayPal" Button
3. Optional: Tooltip mit Hinweis "Klicke um Details in PayPal zu sehen"

**Acceptance Criteria:**
- [ ] Visuell erkennbar welche Transaktionen PayPal sind
- [ ] Button funktioniert und öffnet PayPal

---

### Phase 2: API Integration (Optional, nach Phase 1)

**Aufwand:** ~2-3 Wochen

#### Milestone 2.1: PayPal Developer Setup

**Ziel:** API-Zugang einrichten

**Tasks:**
1. PayPal Developer Account erstellen (https://developer.paypal.com)
2. App registrieren (Sandbox + Live)
3. Client ID + Secret generieren
4. Transaction Search Permission aktivieren (Achtung: bis zu 9h Wartezeit!)

**Acceptance Criteria:**
- [ ] Sandbox Credentials vorhanden
- [ ] Live Credentials vorhanden
- [ ] Transaction Search Permission aktiv

#### Milestone 2.2: OAuth Implementation

**Ziel:** PayPal OAuth 2.0 Token-Management

**Tasks:**
1. `PayPalAuth` Modul in `Server/`
   - `getAccessToken: unit -> Async<Result<string, string>>`
   - Token-Caching (gültig für ~9 Stunden)
   - Automatic Token Refresh
2. Credentials-Storage (verschlüsselt in Settings)
3. Unit Tests mit Mock-Responses

**API Endpoint:**
```http
POST https://api-m.paypal.com/v1/oauth2/token
Authorization: Basic {base64(client_id:secret)}
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
```

**Acceptance Criteria:**
- [ ] Token wird korrekt abgerufen
- [ ] Token wird gecached
- [ ] Refresh funktioniert automatisch

#### Milestone 2.3: Transaction Search Client

**Ziel:** PayPal Transaction Search API Client

**Tasks:**
1. `PayPalClient` Modul
   - `searchTransactions: DateRange -> Async<Result<PayPalTransaction list, string>>`
   - Pagination Support (max 500 pro Request)
   - Date Range Splitting (max 31 Tage)
2. Response Types in `Shared/PayPalTypes.fs`
3. Error Handling (Rate Limits, Auth Errors)

**API Endpoint:**
```http
GET https://api-m.paypal.com/v1/reporting/transactions
  ?start_date=2024-01-01T00:00:00-0000
  &end_date=2024-01-31T23:59:59-0000
  &fields=all
  &page_size=500
Authorization: Bearer {access_token}
```

**Acceptance Criteria:**
- [ ] Transaktionen werden korrekt abgerufen
- [ ] Pagination funktioniert
- [ ] Lange Zeiträume werden automatisch gesplittet

#### Milestone 2.4: Transaction Matching Logic

**Ziel:** Matche Bank-Transaktionen mit PayPal-Transaktionen

**Tasks:**
1. Matching-Algorithmus:
   ```fsharp
   matchPayPalTransaction:
     bankTransaction: Transaction ->
     paypalTransactions: PayPalTransaction list ->
     PayPalTransaction option
   ```
2. Matching-Kriterien:
   - Datum (±1 Tag Toleranz wegen Buchungsverzögerung)
   - Betrag (exakt)
   - Optional: Händlername im Memo
3. Confidence Score für Matches
4. Tests mit realen Testdaten

**Acceptance Criteria:**
- [ ] Matching funktioniert für >90% der Fälle
- [ ] Keine False Positives bei mehreren Transaktionen am Tag
- [ ] Confidence Score hilft bei unsicheren Matches

#### Milestone 2.5: UI für PayPal Details

**Ziel:** Zeige PayPal-Details in Transaction Detail View

**Tasks:**
1. PayPal Details Panel:
   - Händlername
   - Original-Beschreibung
   - Item Details (wenn verfügbar)
   - PayPal Transaction ID
   - Status
2. "Sync PayPal" Button zum manuellen Refresh
3. Loading State während API-Abruf
4. Error Handling UI

**Acceptance Criteria:**
- [ ] PayPal-Details werden angezeigt
- [ ] User kann manuell refreshen
- [ ] Errors werden user-friendly angezeigt

#### Milestone 2.6: Settings & Credentials Management

**Ziel:** User kann PayPal-Credentials konfigurieren

**Tasks:**
1. Settings-Seite für PayPal:
   - Client ID Input
   - Client Secret Input (masked)
   - "Test Connection" Button
   - Enable/Disable Toggle
2. Credentials verschlüsselt speichern
3. Validation der Credentials beim Speichern

**Acceptance Criteria:**
- [ ] Credentials können eingegeben werden
- [ ] Test-Funktion zeigt ob Verbindung funktioniert
- [ ] Credentials sind sicher gespeichert

---

## Empfehlung

**Start mit Phase 1 (Deep Link):**
- Schnell umsetzbar (~2-4h)
- Sofortiger Mehrwert
- Kein API-Overhead
- Gute UX für moderate Nutzung (20-100 Transaktionen/Monat)

**Phase 2 nur wenn:**
- Phase 1 nicht ausreicht
- Du bereit bist, PayPal Developer Account einzurichten
- Du die Komplexität des Matching-Problems akzeptierst

---

## Quellen

- [PayPal Transaction Search API Reference](https://developer.paypal.com/docs/api/transaction-search/v1/)
- [PayPal Transaction Search Integration Guide](https://developer.paypal.com/docs/transaction-search/)
- [PayPal REST API Specifications (GitHub)](https://github.com/paypal/paypal-rest-api-specifications)
- [SEPA End-to-End-ID Explained](https://www.jam-software.com/sepa-transfer/end-to-end-id.shtml)
- [PayPal Transaction ID Format](https://www.paypalobjects.com/en_US/vhelp/paypalmanager_help/transaction_id_format.htm)
