---
id: ledger-kt0wq
title: Walking Skeleton Kassenbuch: ein YNAB-Konto spiegeln, Buchungen mit Herkunft und Historie anzeigen
status: done
type: spike
context: ledger
created: 2026-09-06
depends_on: [ledger-e3k7p, ledger-m8r2v, ledger-q5w9c]
blocks: []
tags: [foundation, frontend, backend]
related_adrs: [0012, 0013, 0014, 0015, 0016]
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
- [x] Nach "Jetzt spiegeln" erscheinen alle Buchungen des gewählten Kontos aus YNAB in der
      Kassenbuch-Seite; Anzahl und Kontostand stimmen mit YNAB-Web überein.
- [x] Jede Buchung zeigt Herkunft "YNAB-Spiegel" und hat genau einen Historie-Eintrag
      (Erstbeobachtung) mit Zeitpunkt.
- [x] Roman ändert in YNAB-Web die Kategorie einer Buchung; nach dem nächsten Spiegel-Lauf
      zeigt dieselbe Buchung (gleiche Buchungs-Id) zwei Historie-Einträge, der neue mit der
      neuen Kategorie.
- [x] Roman löscht in YNAB-Web eine Buchung; nach dem Spiegel-Lauf ist sie im Kassenbuch
      als "in YNAB gelöscht" markiert, nicht verschwunden, mit Historie-Eintrag.
- [x] Ein zweiter Spiegel-Lauf ohne Änderung in YNAB erzeugt kein neues Ereignis
      (Idempotenz), und `server_knowledge` wurde persistiert.
- [x] Replay-Test: Zustandsmodell aus dem Ereignis-Log neu gebaut ergibt denselben Stand.
- [x] Die Seite nutzt ausschließlich DS-Komponenten und Tokens (Styleguide-Gate
      `design-system-001`); mobil lesbar.
- [x] `dotnet build`, `dotnet test`, `npm run build` grün; Tests nur gegen In-Memory-SQLite.

## Notes
Setzt die Entscheidungen A (Historie, `ledger-fsz0a`), B (Spiegel, `ynab-czk34`) und
D (Identität, `ledger-z5vw6`) voraus. C (Kontenmodell/Bewertung, `ledger-zm0zx`) ist
bewusst *nicht* Voraussetzung; das eine Konto darf im Spike ein minimales Konto-Record sein.

Der Spiegel-Timer aus B darf im Spike auf den manuellen Auslöser beschränkt bleiben; der
Timer ist Folge-Task.

Bestehende `sync_transactions` (pro SyncSession) bleiben unberührt; die Verschmelzung von
Sync-Pipeline und Kassenbuch (Import-Buchungen mit Herkunft Comdirect/Regel ins Kassenbuch)
ist der nächste Slice nach dem Skeleton.

## Aufteilung (2026-09-06, Work-Session mit Roman)
Der Skeleton ist in drei Slices aufgeteilt, die nacheinander laufen:
1. `ledger-e3k7p` Ereignis-Log, Zustandsmodell, `appendAndApply`, Replay-Test
2. `ledger-m8r2v` Spiegel-Lauf (Delta, Snapshot-Vergleich, Ereignisse, manueller Endpunkt)
3. `ledger-q5w9c` Kassenbuch-Seite (Buchungen, Herkunfts-Badge, Historie, "Jetzt spiegeln")

Dieser Task bleibt als **Abnahme auf dem echten Budget** stehen (Human-Gate): die
Acceptance Criteria oben werden von Roman gegen YNAB-Web geprüft (Anzahl und Kontostand,
Kategorie-Änderung, Löschung, zweiter Lauf ohne Ereignis). Kein eigener Code-Slice; falls
die Abnahme etwas findet, wird das als Bugfix-Task erfasst. Änderungen gegenüber dem
ursprünglichen Scope: Spiegel ist budgetweit (ADR 0014), die Seite zeigt ein Konto; ETF
ist kein BB-eigenes Konto (ADR 0016); Spiegel-Auslöser bleibt manuell, das Frische-
Kriterium ist Folge-Task.

## Outcome

Abgenommen von Roman am 2026-09-11 am echten Budget. Der Durchstich steht: Ereignis-Log,
Spiegel und Anzeige arbeiten zusammen.

**Was belegt ist**
- **Umfang und Salden.** Nach dem vollständigen Neuaufbau trägt das Kassenbuch 13.864
  Buchungen, 13.866 Ereignisse, 15.698 Fremd-Id-Verknüpfungen und 7 Konten. Die Id-Mengen
  rechnen sich exakt gegen YNAB auf: 13.863 gemeinsame lebende Buchungen, dazu die eine im
  Abnahmetest gelöschte, die das Kassenbuch behält und YNAB nicht mehr führt. Die
  Saldo-Differenzen bei Girokonto und Bar zum Prüfzeitpunkt waren ausschließlich die 33
  YNAB-Buchungen vom 07. bis 10.09., die nach dem letzten Spiegel-Lauf entstanden sind;
  ihre Summen entsprachen den Differenzen auf den Cent. Nichts Altes fehlte, nichts war
  doppelt.
- **Herkunft und Erstbeobachtung.** Jede Buchung trägt das Badge "YNAB-Spiegel"; eine
  frisch gespiegelte Buchung hat genau ein Ereignis (`EntryObservedInYnab`).
- **Kategorie-Änderung.** An einer Buchung nachgewiesen: `EntryObservedInYnab` (20:04:36),
  `EntryCategorizationChangeObservedInYnab` (20:06:02) — zwei Historie-Zeilen auf derselben
  Buchungs-Id.
- **Löschung.** Dieselbe Buchung: `EntryDeletionObservedInYnab` (20:06:30). Sie bleibt in
  der Liste, trägt das Kennzeichen "in YNAB gelöscht" und zählt nicht im Kontostand.
- **Idempotenz.** Ein Lauf ohne Änderung in YNAB meldete "0 neu, 0 geändert, 0 gelöscht";
  `ledger_mirror_server_knowledge_<budgetId>` ist persistiert.
- **Replay.** Durch den Pflicht-Test aus ADR 0013 abgedeckt (`replayAll` gegen die
  Zustandstabellen), nicht von Hand geprüft.
- **Oberfläche und Gates.** Nur DS-Komponenten und Tokens (vom Verifier geprüft);
  `dotnet build` ohne Warnungen, `dotnet test` grün, `npm run build` grün.

**Zwei Fehler, die erst diese Abnahme gefunden hat, beide behoben**
1. **Die Seite lud beim Direktaufruf nichts** (Commit `68b9f58`). `init` schrieb die Seite
   aus der URL schon in `CurrentPage` und schickte danach `UrlChanged` für dieselbe Seite;
   dessen Gleichheits-Guard griff immer. Über die Navigation lief alles, per F5 oder Deep
   Link nichts. Betroffen war auch `Rules`. Zusätzlich lud ein Spiegel-Lauf nur Buchungen
   und Lauf-Status nach, nicht die Konten — auf einem leeren Kassenbuch passierte damit
   nach dem Erstlauf gar nichts.
2. **Die Erstbefüllung holte nur zwölf Monate** (Commit `3d655c0`). YNABs
   `GET /budgets/{id}/transactions` liefert ohne `since_date` nur ein rollendes Fenster:
   2508 Buchungen ab 2025-09-08 statt 13.863 ab 2018-12-29. Drei von sieben Kontoständen
   waren dadurch falsch, weil die Anfangssalden der älteren Konten außerhalb des Fensters
   lagen. ADR 0014 fordert für die Erstbefüllung einen Vollabruf; "ohne `last_knowledge`"
   allein ist gegenüber dieser API keiner.

Beide waren ohne echtes Budget unsichtbar: jeder Testdatensatz liegt innerhalb der letzten
zwölf Monate, und Reducer-Tests prüfen den Spiegel mit bereits gewähltem Konto. Genau dafür
ist das Human-Gate da.

**Was die Abnahme bewusst offen lässt**
- `ledger-p3n8k`: gelöschte Buchungen stehen gleichberechtigt in der Liste und lassen den
  Kontostand falsch wirken. Anzeige-Thema, gehört nicht in den Durchstich.
- Wird eine Buchung in YNAB gelöscht und neu importiert, entstehen zwei unverbundene
  Buchungen. Der Ereignis-Katalog kennt kein Ereignis, das eine Buchung als Ersatz einer
  gelöschten ausweist. Modellierung mit Roman, nicht Fehlersuche.
- `ledger-h4p6c` (automatischer Spiegel-Auslöser) und `ledger-t7v2q` (Übertrags-Verknüpfung)
  bleiben wie geplant Folge-Arbeit.
