---
id: ledger-kt0wq
title: Walking Skeleton Kassenbuch: ein YNAB-Konto spiegeln, Buchungen mit Herkunft und Historie anzeigen
status: todo
type: spike
context: ledger
created: 2026-09-06
depends_on: [ledger-fsz0a, ynab-czk34, ledger-z5vw6, design-system-001]
blocks: []
tags: [foundation, frontend, backend]
related_adrs: [0012]
related_research: []
prior_art: []
---

## Why
Der bestehende Stack läuft; was es nicht gibt, ist ein Beweis, dass Historie-Speicher,
Spiegel und Anzeige zusammen funktionieren. Dieser Spike ist der erste Prototyp der neuen
Architektur: dünn im Feature, dick in der Architektur. Er ersetzt für diese Revision den
reservierten `infrastructure-001-walking-skeleton` (das Projekt ist reif, der Skeleton
gilt dem Kassenbuch, nicht dem Stack; deshalb im `ledger` und mit gewöhnlicher Id).

## What
Der kleinste End-to-End-Slice:
1. Ein von Roman gewähltes YNAB-Konto (Comdirect-Konto) wird per Delta-Spiegel ins
   Kassenbuch geholt (Erstbefüllung + ein Folge-Lauf).
2. Jede Buchung landet als Ereignis-Log + Zustandsmodell mit Herkunft "YNAB-Spiegel" und
   eigener Buchungs-Id, Fremd-Id verknüpft.
3. Eine neue Seite "Kassenbuch" zeigt die Buchungen des Kontos (Datum, Payee, Betrag,
   Kategorie, Herkunfts-Badge) und pro Buchung ihre Historie.
4. Kein Push, keine Bearbeitung, keine Bewertung, kein zweites Konto. Nur Beweis.

## Acceptance criteria
- [ ] Nach "Jetzt spiegeln" erscheinen alle Buchungen des gewählten Kontos aus YNAB in der
      Kassenbuch-Seite; Anzahl und Kontostand stimmen mit YNAB-Web überein.
- [ ] Jede Buchung zeigt Herkunft "YNAB-Spiegel" und hat genau einen Historie-Eintrag
      (Erstbeobachtung) mit Zeitpunkt.
- [ ] Roman ändert in YNAB-Web die Kategorie einer Buchung; nach dem nächsten Spiegel-Lauf
      zeigt dieselbe Buchung (gleiche Buchungs-Id) zwei Historie-Einträge, der neue mit der
      neuen Kategorie.
- [ ] Roman löscht in YNAB-Web eine Buchung; nach dem Spiegel-Lauf ist sie im Kassenbuch
      als "in YNAB gelöscht" markiert, nicht verschwunden, mit Historie-Eintrag.
- [ ] Ein zweiter Spiegel-Lauf ohne Änderung in YNAB erzeugt kein neues Ereignis
      (Idempotenz), und `server_knowledge` wurde persistiert.
- [ ] Replay-Test: Zustandsmodell aus dem Ereignis-Log neu gebaut ergibt denselben Stand.
- [ ] Die Seite nutzt ausschließlich DS-Komponenten und Tokens (Styleguide-Gate
      `design-system-001`); mobil lesbar.
- [ ] `dotnet build`, `dotnet test`, `npm run build` grün; Tests nur gegen In-Memory-SQLite.

## Notes
Setzt die Entscheidungen A (Historie, `ledger-fsz0a`), B (Spiegel, `ynab-czk34`) und
D (Identität, `ledger-z5vw6`) voraus. C (Kontenmodell/Bewertung, `ledger-zm0zx`) ist
bewusst *nicht* Voraussetzung; das eine Konto darf im Spike ein minimales Konto-Record sein.

Der Spiegel-Timer aus B darf im Spike auf den manuellen Auslöser beschränkt bleiben; der
Timer ist Folge-Task.

Bestehende `sync_transactions` (pro SyncSession) bleiben unberührt; die Verschmelzung von
Sync-Pipeline und Kassenbuch (Import-Buchungen mit Herkunft Comdirect/Regel ins Kassenbuch)
ist der nächste Slice nach dem Skeleton.
