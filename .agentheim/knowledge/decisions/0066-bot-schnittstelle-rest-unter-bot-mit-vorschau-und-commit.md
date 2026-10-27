---
id: 0066
title: Bot-Schnittstelle als REST unter /bot/ mit Vorschau und Commit, getrennt von Fable.Remoting
scope: ledger
status: accepted
date: 2026-10-27
supersedes: []
superseded_by: []
related_tasks: [ledger-b0b7q]
related_adrs: [0004, 0013]
related_research: []
---

# ADR 0066: Bot-Schnittstelle als REST unter /bot/ mit Vorschau und Commit

## Context
Bob (Hermes-Agent auf einer Hetzner-VM) soll Barbuchungen anlegen wie Quick Add. Bob liest
Mails und Webseiten, eine Prompt-Injection ist realistisch. Alles, was Bob liest, geht an
die VM und an den LLM-Anbieter. BB hat keine eigene Anmeldung; die Fable.Remoting-API ist
für den eigenen Client gebaut und ihr Wire-Format ist ein internes Detail.

## Decision
1. **Eigene REST-Routen unter `/bot/`**, als Giraffe-Handler vor dem SPA-Fallback, getrennt
   von Fable.Remoting. Kein MCP-Server; kommen später Lesezugriffe dazu, lässt sich diese
   Schicht als MCP verpacken.
2. **Zwei Schritte: Vorschau, dann Commit.** Die Vorschau prüft und löst auf, schreibt aber
   nichts ins Kassenbuch und nichts nach YNAB. Der Commit bucht genau die Vorschau und ist
   idempotent über die `preview_id`.
3. **Vorschauen im Speicher**, 15 Minuten gültig. Nach einem Neustart verfallen sie; die
   Idempotenz gilt für die Lebenszeit des Prozesses.
4. **Bearer-Token aus der Umgebung** als zweite Schicht hinter dem Proxy-Stack im
   Homelab-Repo (Caddy lässt nur `/bot/*` durch). Das Token liegt nicht in der Datenbank.
5. **Enge Rechte:** nur Ausgaben aufs Quick-Add-Konto (ADR 0004), Betragsobergrenze per
   Umgebung (Vorgabe 500 €), keine Lese-Route für Buchungen oder Salden, nur Kategorien.
6. **Eigene Herkunft `Bob`** als neuer Fall des `Actor` (ADR 0013) und der `LedgerOrigin`.
   Additiv: alte Ereignisse lesen sich unverändert, die Payload-Schema-Versionen bleiben.

## Consequences
- Bob kann den Kassenbuch-Stand nicht sehen; Rückfragen laufen über Kategorien und die
  eigene Vorschau.
- Ein kompromittierter Bob kann höchstens Barausgaben bis zur Obergrenze anlegen, sichtbar
  an der Herkunft `Bob`.
- Ein Deploy zwischen Vorschau und Commit lässt den Commit mit 404 scheitern; Bob muss die
  Vorschau neu anlegen.
