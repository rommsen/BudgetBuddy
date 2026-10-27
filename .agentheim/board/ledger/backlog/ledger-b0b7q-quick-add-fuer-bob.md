---
id: ledger-b0b7q
title: "Quick Add für Bob: schmale HTTP-API mit Vorschau und Bestätigung"
status: backlog
type: feature
context: ledger
created: 2026-10-27
completed:
depends_on: []
blocks: []
tags: [backend, api, quick-add, bob, sicherheit]
related_adrs: [0004]
related_research: []
prior_art: []
---
Ziel-Lage: backlog. Grundsatzentscheidungen mit Roman am 2026-10-27 getroffen (siehe
Notes), Feinschnitt beim Verfeinern.

## Why
Bob (Hermes-Agent auf der Hetzner-VM, Repo `~/src/personal-agent`) soll Barbuchungen
anlegen können, so wie Roman es heute über Quick Add tut: "50 Euro heute an Anastasia,
Kategorie Haushaltshilfe, Barkonto". Bob zerlegt den Satz selbst in Felder, BB nimmt nur
strukturierte Daten an.

## What
Eigene HTTP-Routen unter `/bot/`, getrennt von Fable.Remoting. Das Wire-Format von
Fable.Remoting ist ein internes Detail und darf nicht zum Vertrag mit Bob werden.

Ablauf in zwei Schritten, weil Bob die Buchung vor dem Schreiben Roman zur Bestätigung
zeigt:

1. `POST /bot/transactions/preview` mit `amount`, `direction` (`outflow`/`inflow`),
   `payee`, `category` (Name oder Id), `date`, `memo`. BB prüft, löst die Kategorie auf
   und legt eine Vorschau mit `preview_id` und Ablaufzeit an. Antwort: die aufgelöste
   Buchung (Konto, Betrag, Payee, Kategorie mit Name und Gruppe, Datum, Memo).
   Nichts geht an YNAB, nichts landet im Kassenbuch.
2. `POST /bot/transactions/{preview_id}/commit`. Bucht genau die Vorschau, über
   denselben Weg wie Quick Add (`Ledger.CommandHandlers.addCashExpense`, dann Push).
   Ein zweiter Commit derselben Vorschau bucht nicht noch einmal, sondern liefert das
   Ergebnis des ersten.
3. `GET /bot/categories`: Name, Gruppe, Id. Damit Bob Namen auflösen und Rückfragen
   stellen kann.

## Acceptance criteria
- [ ] Ohne gültiges Bearer-Token antworten alle `/bot/*`-Routen mit 401. Das Token kommt
      aus der Umgebung (`.env`), nicht aus der Datenbank.
- [ ] Kategorie-Auflösung: genau ein Treffer wird übernommen. Kein oder mehrere Treffer
      ergeben 422 mit Kandidatenliste, BB rät nicht.
- [ ] Das Konto ist immer das Quick-Add-Konto (ADR 0004). Bob kann es nicht wählen.
- [ ] Betrag über einer Obergrenze (Wert beim Verfeinern festlegen, konfigurierbar) wird
      in der Vorschau abgelehnt.
- [ ] Commit einer abgelaufenen oder unbekannten Vorschau ergibt 404 bzw. 410.
- [ ] Commit ist idempotent: zweimal dieselbe `preview_id` erzeugt eine Buchung.
- [ ] Die Buchung trägt eine eigene Herkunft `Bob` (neuer Actor neben `QuickAdd`), sichtbar
      in Historie und Detail.
- [ ] Ein Push-Fehlschlag verhält sich wie bei Quick Add (`EntryPushToYnabFailed`), die
      Commit-Antwort meldet ihn.
- [ ] Keine Lese-Route für Buchungen oder Salden. Bob sieht nur Kategorien und seine
      eigenen Vorschauen.

## Notes
Entscheidungen mit Roman (2026-10-27):
- REST plus Skill bei Bob, kein MCP-Server. Zwei, drei Operationen rechtfertigen keinen
  eigenen MCP-Server. Kommen später Lesezugriffe dazu, lässt sich diese Schicht als MCP
  verpacken.
- Bob zeigt die erkannte Buchung vor dem Schreiben zur Bestätigung. Dass Roman wirklich
  bestätigt hat, kann BB nicht prüfen; das regelt Bobs Skill. Die zweistufige API stellt
  sicher, dass genau das gebucht wird, was Roman gesehen hat, und deckt zugleich die
  Idempotenz ab (Bob wiederholt Aufrufe bei Timeouts).

Offen beim Verfeinern:
- Wo liegen Vorschauen (Speicher mit TTL reicht, oder SQLite)? Nach einem Neustart
  verfallen sie, das ist akzeptabel.
- Wird `Bob` ein neuer `Actor` im Ereignisschema oder eine Ausprägung von `QuickAdd`?
  Schema-Version beachten (`Server/Ledger/Events.fs`).
- Wie verhält sich `/bot/*` gegenüber dem SPA-Fallback in `Program.fs`?

Netzgrenze liegt im Homelab-Repo, nicht hier: BB hat keine eigene Anmeldung, und eine
Tailscale-ACL filtert nur Ports. Bob kommt deshalb über einen Proxy-Stack nach dem Muster
von `stacks/ha-mcp/` (Caddy lässt nur `/bot/*` durch, eigener Tag, ACL-Test), nie direkt
auf `budgetbuddy`. Das Bearer-Token hier ist die zweite Schicht.

Bedrohungsbild: Bob liest Mails und Webseiten, eine Prompt-Injection ist realistisch.
Darum nur Schreiben aufs Barkonto, Obergrenze, eigene Herkunft und kein Lesezugriff auf
das Kassenbuch (alles, was Bob liest, geht an die Hetzner-VM und den LLM-Anbieter).
