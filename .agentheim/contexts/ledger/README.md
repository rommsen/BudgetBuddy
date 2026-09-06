# ledger

## Purpose
BudgetBuddys eigenes, dauerhaftes **Kassenbuch** über alle Konten. Hier bleibt jede
Buchung, mit ihrer Herkunft und ihrer vollständigen, auditierbaren Historie. Das
Kassenbuch zeigt Buchungen und Stände pro Konto und in der Gesamtsicht, auch für Konten,
die YNAB nicht führen kann (Wertpapierkonto mit Budget-Geld). Es ist das Gedächtnis, das
BB zum führenden System macht (ADR 0012), und wird schrittweise der Ort, an dem Buchungen
nachbearbeitet werden.

Was hier **nicht** passiert: Bank-Abruf (`banking-import`), Regel-Matching
(`categorization`), YNAB-API-Verkehr (`ynab-sync`), Budgetieren (noch nirgends in BB).

## Classification
**core** — ohne Kassenbuch gibt es weder Ownership noch Historie noch das ETF-Konto; die
Vision-Stufen v1 und v2 spielen fast vollständig hier.

## Actors
- **Roman** — liest pro Konto und gesamt, bearbeitet Buchungen nach (v2), pflegt die
  Stückzahl des Wertpapierkontos.
- **Frau** — liest mit (gemeinsames Konto, keine eigene Identität).
- **YNAB** — über `ynab-sync`: Quelle des Spiegels und Ziel des Pushs.
- **Kursquelle** — liefert den Kurs für die Bewertung (monatlich reicht).

## Ubiquitous language
- **Kassenbuch** (Ledger) — das dauerhafte Buch aller Buchungen über alle Konten.
- **Konto** — ein Ort, an dem Geld liegt. Arten: **Bankkonto** (Comdirect, ING), **Bar**,
  **Wertpapierkonto mit Budget-Geld** (Geldmarkt-ETF im ING-Depot). Jedes Konto ist
  On-Budget oder nicht. Depots als Vermögen sind kein Konto im Kassenbuch (künftiger
  Context `wealth`).
- **Buchung** — ein Eintrag: Datum, Betrag, Konto, Payee, Memo, Kategorie, ggf. Splits.
  Trägt Herkunft und Historie. Entspricht der `SyncTransaction` nach dem Durchlauf und
  der `YnabTransaction` im Spiegel, ist aber BBs eigene, bleibende Form.
- **Herkunft** — woher eine Buchung oder Änderung stammt: *Comdirect-Import*, *Quick Add*,
  *Regel* (Auto-Kategorisierung), *Roman in BB*, *YNAB-Spiegel* (dahinter ING-Import,
  Änderung in YNAB, YNABs Transfer-Erkennung). Jede Änderung hat genau eine Herkunft.
- **Historie** — die vollständige, unveränderliche Folge aller Änderungen an einer Buchung,
  je mit Zeitpunkt und Herkunft. **Auditierbar** heißt: jede Zahl im Kassenbuch lässt sich
  bis zum Ursprung zurückverfolgen; nichts wird überschrieben, nur ergänzt.
- **Spiegel** — das laufende Nachführen von YNABs Zustand ins Kassenbuch (Mechanik in
  `ynab-sync`, Ergebnis hier). "Spiegel mit Gedächtnis": das Kassenbuch kennt zusätzlich
  BBs eigene Entscheidungen und kann beides vergleichen.
- **Abweichung** — Unterschied zwischen dem, was BB für eine Buchung entschieden hat, und
  dem, was YNAB heute zeigt. Wird sichtbar gemacht, nie still überschrieben. Gehört dem
  Kassenbuch, entsteht an der Naht zu `ynab-sync`.
- **Führung** — pro Fähigkeit: **BB-geführt** (BB entscheidet, YNAB wird nachgezogen) oder
  **YNAB-geführt** (YNAB entscheidet, BB spiegelt). Heute BB-geführt: Import,
  Kategorisierung, Quick Add. Heute YNAB-geführt: Nachbearbeitung, Überträge, ING,
  Budgetieren. v2 holt die Nachbearbeitung nach BB.
- **Übertrag** — zwei Buchungen auf zwei Konten, die eine Bewegung sind (Comdirect 1000
  raus, ING 1000 rein). Keine Budget-Wirkung. Heute erkennt YNAB das; v2 soll es BB können.
- **Bewertung** — Wert eines Wertpapierkontos mit Budget-Geld: **Stückzahl mal Kurs**.
  Stückzahl wird von Hand gepflegt (v1), Kurs kommt von der Kursquelle. Eine
  Bewertungsänderung wird als Buchung (Kursgewinn/-verlust) sichtbar, damit der Kontostand
  stimmt und die Historie vollständig bleibt.
- **Kontostand** — Summe der Buchungen eines Kontos (bei Wertpapierkonten: inkl.
  Bewertungs-Buchungen). Muss für gespiegelte Konten mit YNAB übereinstimmen.
- **Gesamtsicht** — alle Konten zusammen, Buchungen und Stände (YNABs "All Accounts").

## Aggregates
- **Konto** — schützt: Art und On-Budget-Eigenschaft; ein Wertpapierkonto hat eine
  Stückzahl und ein Papier.
- **Buchung** — schützt: die Historie ist nur ergänzbar; jede Änderung hat Zeitpunkt und
  Herkunft; Splits-Invarianten wie in `ynab-sync` (`mkSplits`).
- **Übertrag** (v2) — schützt: genau zwei Buchungen, Beträge spiegelbildlich, zwei
  verschiedene Konten.

Tactical Modeling (Aggregate-Grenzen, Historie-Mechanik) folgt in den Tasks; Klärung der
Historie-Technik in der Decision-Task des Architekten.

## Key events
- *BuchungErfasst* — eine neue Buchung liegt im Kassenbuch (mit Herkunft).
- *BuchungGeändert* — Kategorie, Split, Memo, Payee geändert (mit Herkunft).
- *AbweichungErkannt* — YNAB zeigt etwas anderes als BB entschieden hat.
- *ÜbertragZusammengeführt* — zwei Buchungen als eine Bewegung erkannt (v2).
- *KontoBewertet* — Bewertungs-Buchung für ein Wertpapierkonto erzeugt.

## Key commands
- *SpiegelÜbernehmen* — Ergebnis eines Spiegel-Laufs aus `ynab-sync` einarbeiten.
- *BuchungÜbernehmen* — aus der Sync-Pipeline (Comdirect + Kategorie) oder Quick Add.
- *BuchungÄndern* (v2) — Kategorie / Split / Memo in BB ändern, Push folgt.
- *ÜbertragZusammenführen* (v2).
- *KontoBewerten* — Kurs holen, Bewertungs-Buchung schreiben.
- *StückzahlPflegen* — Stückzahl eines Wertpapierkontos setzen.

## Relationships with other contexts
- **Downstream of:** `banking-import` + `categorization` (kategorisierte Buchungen aus dem
  Sync-Durchlauf werden hier dauerhaft).
- **Partnership mit:** `ynab-sync` in beide Richtungen (Push BB-geführter Entscheidungen
  hinaus, Spiegel YNAB-geführter Änderungen herein). Abweichung entsteht an dieser Naht.
- **Anticorruption layer zu:** Kursquelle (extern, klein).
- **Upstream of (künftig):** `wealth`, Reports, `budgeting`.
- **Customer of:** `design-system`. **Styleguide-Gate:** jede UI-Task in diesem Context
  hängt an `design-system-001` (`depends_on`), gemessen an `standards/frontend/styleguide.md`.
- Voller Kontext-Überblick: `../../context-map.md`.

## Open questions
- **Historie-Technik** — *gelöst (ADR 0013, ledger-fsz0a):* append-only Ereignis-Log pro
  Buchung als Quelle der Wahrheit, Zustandsmodell synchron in derselben Transaktion
  mitgeschrieben, reiner `apply`-Replay als Pflichttest. SQLite bleibt, kein Postgres.
- **Spiegel-Mechanik:** Delta-Sync über YNABs `server_knowledge`, Intervall, Umgang mit
  gelöschten Buchungen, Rate-Limit (~200 req/h). → Decision-Task (Architekt, in `ynab-sync`).
- **Kursquelle** für den Geldmarkt-ETF: welche, wie robust, was bei Ausfall?
- **Bar- und Quick-Add-Konto:** ein Konto oder zwei im Kassenbuch?
- **Quick Add** später direkt ins Kassenbuch (BB-geführt ohne Umweg über YNAB)?
- **Identität der Buchung** — *gelöst (ADR 0015, ledger-z5vw6):* eigene Id
  (`Guid.CreateVersion7()`), Fremd-Ids (YNAB, ImportId, Comdirect) als Verknüpfungen in
  eigener Tabelle; Spiegel matcht YNAB-Id vor ImportId, sonst neue Buchung als Spiegel.
