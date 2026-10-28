---
id: ledger-b0b7q
title: "Quick Add für Bob: schmale HTTP-API unter /bot/ mit Vorschau und Bestätigung, Bearer-Token, Obergrenze 500 € und eigener Herkunft Bob (ADR 0066)"
status: doing
type: feature
context: ledger
created: 2026-10-27
completed:
depends_on: []
blocks: []
tags: [backend, api, quick-add, bob, sicherheit]
related_adrs: [0004, 0013, 0066]
related_research: []
prior_art: []
---

## Why
Bob (Hermes-Agent auf der Hetzner-VM, Repo `~/src/personal-agent`) soll Barbuchungen
anlegen können, so wie Roman es heute über Quick Add tut: "50 Euro heute an Anastasia,
Kategorie Haushaltshilfe, Barkonto". Bob zerlegt den Satz selbst in Felder, BB nimmt nur
strukturierte Daten an.

## What
Eigene HTTP-Routen unter `/bot/`, getrennt von Fable.Remoting (ADR 0066). Das Wire-Format
von Fable.Remoting ist ein internes Detail und darf nicht zum Vertrag mit Bob werden.

Ablauf in zwei Schritten, weil Bob die Buchung vor dem Schreiben Roman zur Bestätigung
zeigt:

1. `POST /bot/transactions/preview` mit `amount`, `direction` (`outflow`/`inflow`),
   `payee`, `category` (Name oder Id), `date`, `memo`. BB prüft, löst die Kategorie auf
   und legt eine Vorschau mit `preview_id` und Ablaufzeit (15 Minuten) an. Antwort: die
   aufgelöste Buchung (Konto, Betrag, Payee, Kategorie mit Name und Gruppe, Datum, Memo).
   Nichts geht an YNAB, nichts landet im Kassenbuch.
2. `POST /bot/transactions/{preview_id}/commit`. Bucht genau die Vorschau, über
   denselben Weg wie Quick Add (`Ledger.CommandHandlers.addCashExpense`, dann Push).
   Ein zweiter Commit derselben Vorschau bucht nicht noch einmal, sondern liefert das
   Ergebnis des ersten.
3. `GET /bot/categories`: Name, Gruppe, Id. Damit Bob Namen auflösen und Rückfragen
   stellen kann.

Schnitt nach Projektmuster: Validierung der Vorschau-Eingabe in `Validation.fs`,
Kategorie-Auflösung und Obergrenzen-Prüfung als reine Funktionen, Vorschau-Speicher
(In-Memory, TTL) und Handler im Server. `addCashExpense` bekommt den Actor als Parameter
(heute fest `QuickAdd`), damit Quick Add und Bob denselben Weg gehen.

## Acceptance criteria
- [ ] Ohne gültiges Bearer-Token antworten alle `/bot/*`-Routen mit 401. Das Token kommt
      aus der Umgebung (`.env`, Name z. B. `BOT_API_TOKEN`), nicht aus der Datenbank. Ist
      kein Token gesetzt, sind die Routen abgeschaltet (401 oder 404, nie offen).
- [ ] Unbekannte Pfade unter `/bot/` antworten mit 404 als JSON, nicht mit der
      `index.html` des SPA-Fallbacks.
- [ ] Kategorie-Auflösung: genau ein Treffer (Id oder Name, ohne Groß-/Kleinschreibung)
      wird übernommen. Kein oder mehrere Treffer ergeben 422 mit Kandidatenliste, BB rät
      nicht. Interne YNAB-Kategorien sind nicht wählbar.
- [ ] Das Konto ist immer das Quick-Add-Konto (ADR 0004). Die Eingabe hat kein Konto-Feld;
      ein mitgeschicktes wird ignoriert oder abgelehnt. Ohne eingerichtetes Quick-Add-Konto
      antwortet die Vorschau mit 422 und Grund.
- [ ] Betrag über der Obergrenze (Vorgabe 500 €, per Umgebung konfigurierbar, z. B.
      `BOT_MAX_AMOUNT_EUR`) wird in der Vorschau mit 422 abgelehnt. Betrag 0 oder negativ
      ebenso; die Richtung kommt allein aus `direction`.
- [ ] Ungültige Eingaben (fehlendes Feld, falsches Datum, unbekannte `direction`) ergeben
      400 mit Feldnamen.
- [ ] Commit einer unbekannten Vorschau ergibt 404, einer abgelaufenen 410.
- [ ] Commit ist idempotent: zweimal dieselbe `preview_id` erzeugt genau eine Buchung, der
      zweite Aufruf liefert das Ergebnis des ersten. Auch zwei gleichzeitige Commits
      buchen nur einmal.
- [ ] Die Buchung trägt die Herkunft `Bob`: neuer `Actor`-Fall in
      `Server/Ledger/Events.fs` (samt JSON-Codec, additiv, Payload-Schema-Versionen
      unverändert) und neuer `LedgerOrigin`-Fall in `Shared/Domain.fs` mit Anzeigename.
      Sichtbar in Historie und Detail.
- [ ] Ein Push-Fehlschlag verhält sich wie bei Quick Add (`EntryPushToYnabFailed`, Actor
      `Bob`), die Commit-Antwort meldet ihn.
- [ ] Keine Lese-Route für Buchungen oder Salden. Bob sieht nur Kategorien und seine
      eigenen Vorschauen.
- [ ] Tests: Validierung, Kategorie-Auflösung, Obergrenze und TTL als Unit-Tests;
      die Routen (401, 404, 410, 422, Idempotenz) als Integrationstests über den
      Giraffe-Handler mit Test-Datenbank, nie gegen die Produktionsdatenbank. Ein Codec-
      Rundlauf-Test für `Actor.Bob`.
- [ ] `.env.example` und die Doku nennen die neuen Umgebungsvariablen.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Entscheidungen mit Roman (2026-10-27):
- REST plus Skill bei Bob, kein MCP-Server. Zwei, drei Operationen rechtfertigen keinen
  eigenen MCP-Server. Kommen später Lesezugriffe dazu, lässt sich diese Schicht als MCP
  verpacken.
- Bob zeigt die erkannte Buchung vor dem Schreiben zur Bestätigung. Dass Roman wirklich
  bestätigt hat, kann BB nicht prüfen; das regelt Bobs Skill. Die zweistufige API stellt
  sicher, dass genau das gebucht wird, was Roman gesehen hat, und deckt zugleich die
  Idempotenz ab (Bob wiederholt Aufrufe bei Timeouts).
- Beim Verfeinern (2026-10-27): Obergrenze 500 €. Vorschauen im Speicher mit 15 Minuten
  TTL; nach einem Neustart verfallen sie. `Bob` wird ein eigener `Actor`, keine Ausprägung
  von `QuickAdd`. `/bot/*` liegt in `Server.Api.webApp` vor dem SPA-Fallback
  (`MapFallbackToFile` in `Program.fs`); unbekannte `/bot/`-Pfade fängt ein eigener
  404-Handler ab. Festgehalten in ADR 0066.

Netzgrenze liegt im Homelab-Repo, nicht hier: BB hat keine eigene Anmeldung, und eine
Tailscale-ACL filtert nur Ports. Bob kommt deshalb über einen Proxy-Stack nach dem Muster
von `stacks/ha-mcp/` (Caddy lässt nur `/bot/*` durch, eigener Tag, ACL-Test), nie direkt
auf `budgetbuddy`. Das Bearer-Token hier ist die zweite Schicht. Der Proxy-Stack und Bobs
Skill sind nicht Teil dieses Tasks.

Bedrohungsbild: Bob liest Mails und Webseiten, eine Prompt-Injection ist realistisch.
Darum nur Schreiben aufs Barkonto, Obergrenze, eigene Herkunft und kein Lesezugriff auf
das Kassenbuch (alles, was Bob liest, geht an die Hetzner-VM und den LLM-Anbieter).
