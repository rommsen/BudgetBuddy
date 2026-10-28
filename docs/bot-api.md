# Bot-Schnittstelle für Bob (`/bot/`)

Entscheidung: ADR 0066 (ledger-b0b7q). Eigene REST-Routen, getrennt von Fable.Remoting.
Bob legt Barbuchungen in zwei Schritten an: Vorschau, dann Commit.

## Umgebungsvariablen

| Variable | Bedeutung |
|---|---|
| `BOT_API_TOKEN` | Bearer-Token aller `/bot/`-Routen. Ohne Wert sind die Routen abgeschaltet (401). Erzeugen mit `openssl rand -hex 32`. |
| `BOT_MAX_AMOUNT_EUR` | Obergrenze einer Buchung in Euro. Optional, Vorgabe 500. Nur Dezimalpunkt (`250.50`), "500,00" und "1,000" sind ungültig und ergeben die Vorgabe. Der Code deckelt bei 500 EUR, die Umgebung darf nur absenken. Der wirksame Wert steht beim Start im Log. |

Beide stehen in `.env.example` und werden in `compose.yaml` an den Container durchgereicht.

## Routen

Alle Routen verlangen `Authorization: Bearer <BOT_API_TOKEN>`, sonst 401. Unbekannte Pfade
unter `/bot/` antworten 404 als JSON.

- `POST /bot/transactions/preview` mit `amount` (Zahl in Euro, positiv, höchstens zwei
  Nachkommastellen), `direction` (`outflow` oder `inflow`), `payee`, `category` (Name oder Id),
  `date` (`JJJJ-MM-TT`), optional `memo`. Ein Konto-Feld gibt es nicht: gebucht wird immer auf
  das Quick-Add-Konto. Antwort 200 mit `preview_id`, `expires_at` (15 Minuten) und der
  aufgelösten Buchung. Fehler: 400 mit `fields` (Feldnamen), 422 bei Betrag über der Grenze,
  Betrag 0 oder negativ, fehlendem Quick-Add-Konto sowie keiner oder mehrdeutiger Kategorie
  (mit `candidates`).
- `POST /bot/transactions/{preview_id}/commit` bucht genau die Vorschau. 404 unbekannt,
  410 abgelaufen. Ein zweiter Aufruf bucht nicht noch einmal und liefert das erste Ergebnis.
  Die Antwort nennt `entry_id`, `pushed_to_ynab` und bei einem Fehlschlag `push_error`.
- `GET /bot/categories` liefert `id`, `name`, `group` der wählbaren Kategorien (ohne interne und
  versteckte).

Die Herkunft der Buchung ist `Bob`. Es gibt keine Lese-Route für Buchungen oder Salden.
Vorschauen liegen im Arbeitsspeicher und verfallen beim Neustart.
