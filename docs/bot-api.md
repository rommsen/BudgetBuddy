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
  `date` (`JJJJ-MM-TT`, von heute minus 31 Tage bis heute, Europe/Berlin; sonst 400 mit Feld
  `date`), optional `memo`. Ein Konto-Feld gibt es nicht: gebucht wird immer auf
  das Quick-Add-Konto. Antwort 200 mit `preview_id`, `expires_at` (15 Minuten) und der
  aufgelösten Buchung. Fehler: 400 mit `fields` (Feldnamen), 422 bei Betrag über der Grenze,
  Betrag 0 oder negativ, erreichter Tagesgrenze (Code `daily_limit_reached`), fehlendem Quick-Add-Konto sowie keiner oder mehrdeutiger Kategorie
  (mit `candidates`).
- `POST /bot/transactions/{preview_id}/commit` bucht genau die Vorschau. 404 unbekannt,
  410 abgelaufen, 422 mit `daily_limit_reached`, wenn die Tagesgrenze inzwischen erreicht ist
  (mehrere offene Vorschauen können zusammen darüber liegen). Ein zweiter Aufruf bucht nicht noch einmal und liefert das erste Ergebnis.
  Die Antwort nennt `entry_id`, `pushed_to_ynab` und bei einem Fehlschlag `push_error`.
- `GET /bot/categories` liefert `id`, `name`, `group` der wählbaren Kategorien (ohne interne und
  versteckte). Vorschau und diese Route teilen einen Kategorien-Cache.

## Kategorien-Cache und Drosselung

Die YNAB-Kategorien für die Bot-Schnittstelle bleiben 5 Minuten im Speicher. Vorschau und
`GET /bot/categories` teilen den Cache, ein Abruf kostet also höchstens eine YNAB-Anfrage je
5 Minuten. Schlägt der Abruf ohne gültigen Cache fehl, bleibt es bei 502 `ynab_unavailable`;
Fehler werden nicht gecacht. Quick Add und Sync nutzen den Cache nicht.

Auf `/bot/` sind höchstens 10 Anfragen pro Minute erlaubt, gezählt über alle Bot-Anfragen vor
der Anmeldung, auch die mit 401 (so wird das Raten des Tokens gebremst). Darüber antwortet die
Schnittstelle 429 mit Code `rate_limited` im üblichen JSON-Fehlerformat und dem Kopf
`Retry-After` in Sekunden. Abgewiesene Anfragen zählen nicht mit. Die Zählung liegt im
Speicher, ein Neustart setzt sie zurück.

## Tagesgrenze

Je Kalendertag (Europe/Berlin) höchstens 500 EUR Summe (Beträge ohne Vorzeichen, Ausgaben und
Einnahmen zusammen) und höchstens 10 gebuchte Bob-Commits. Gezählt werden die im Kassenbuch
gespeicherten Buchungen mit Herkunft `Bob` nach dem Zeitpunkt des Commits, ein Neustart setzt
nichts zurück. Geprüft wird in der Vorschau und noch einmal im Commit, dort unter einer
Sperre, damit gleichzeitige Commits die Grenze nicht gemeinsam überschreiten. Die Meldung
nennt Summe und Anzahl des Tages. Die Werte stehen als Konstanten im Code (`Validation.fs`).
Es gibt keine Benachrichtigung und kein Flag: Bob-Buchungen stehen in YNAB unbestätigt.

Die Herkunft der Buchung ist `Bob`. Es gibt keine Lese-Route für Buchungen oder Salden.
Vorschauen liegen im Arbeitsspeicher und verfallen beim Neustart.
