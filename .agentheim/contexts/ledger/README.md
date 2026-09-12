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
  Bewertungs-Buchungen). Muss für gespiegelte Konten mit YNAB übereinstimmen. Wird in
  BB summiert, nicht per SQL-`SUM`: Beträge liegen als exakter Dezimal-Text, damit ein
  auditierbares Kassenbuch keine Gleitkomma-Drift zeigt (ADR 0017).
- **Gesamtsicht** — alle Konten zusammen, Buchungen und Stände (YNABs "All Accounts").
- **Fremd-Id-Verknüpfung** — eine Id aus einem fremden System (`ynab`, `importid`,
  `comdirect`), die auf genau eine Buchung zeigt. Nicht Teil der Buchungs-Id, sondern
  eine eigene Zeile je System und Wert; ein Wert gehört je System höchstens einer
  Buchung (ADR 0015). Der Spiegel matcht darüber YNAB-Id vor ImportId.
- **Beobachtung** — was der Spiegel in YNAB gesehen hat: der volle Snapshot einer
  YNAB-Buchung zum Zeitpunkt des Laufs plus ein `deleted`-Flag, rein datentragend und
  ohne Wertung (Contract aus `ynab-sync`, ADR 0014). Aus Beobachtungen macht **das
  Kassenbuch** die semantischen Delta-Ereignisse, nicht `ynab-sync` (ADR 0013): die reine
  Funktion `observe` (`src/Server/Ledger/Domain.fs`) vergleicht Snapshot gegen Zustand
  und liefert die Ereignisse, die daraus folgen — bei unveränderten Daten keine.
- **Spiegel-Lauf** — ein Durchgang des Spiegels: Konten nachziehen, Beobachtungen holen,
  je Beobachtung die Buchung suchen (YNAB-Id vor ImportId, ADR 0015), vergleichen und nur
  bei Unterschied Ereignisse schreiben. Ein Lauf schreibt ausschließlich Beobachtungen,
  Herkunft `YNAB-Spiegel`. Wiederholte Läufe mit denselben Daten erzeugen kein Ereignis;
  ein Lauf ohne gemerktes `server_knowledge` ist die Erstbefüllung und ebenso idempotent.
  Code: `src/Server/Ledger/CommandHandlers.fs` (`runMirror`, `mirrorYnabBudget`).
- **Lauf-Status** — Zeitpunkt und Ergebnis des zuletzt gelaufenen Spiegels
  (Anzahl neuer, geänderter und gelöschter Buchungen, sonst der Fehlertext). Wird immer
  festgehalten, auch wenn der Lauf abbricht: ein Fehler wird sichtbar gemacht, nicht still
  geschluckt (ADR 0014). Abrufbar über `LedgerApi.getLastMirrorRun`.
- **Unkategorisierte Split-Zeile** — eine Split-Zeile, die in YNAB weder eine Kategorie
  noch einen Übertrag trägt. Der Spiegel übernimmt sie als solche: sie zählt beim Betrag
  voll mit, wird nie weggelassen und bricht keinen Spiegel-Lauf ab. Beim Push bleibt sie
  kategorielos, BB selbst erzeugt sie nicht (ADR 0019, ergänzt ADR 0006).
- **Sichten** (Lesepfad der Kassenbuch-Seite) — die semantischen Lesefunktionen des
  Kassenbuchs. Code: `src/Server/Ledger/QueryHandlers.fs`, Endpunkte in `LedgerApi`.
  Gelesen werden die Zustandstabellen; nur die Historie fasst das Ereignis-Log an.
  - `getLedgerAccounts` — alle Konten samt Vorauswahl der Seite: das gespiegelte
    YNAB-Standardkonto (`ynab_default_account_id`), sonst das erste.
  - `getLedgerEntriesForAccount` — die Buchungen eines Kontos, neueste zuerst, auf
    einmal geladen. Bleibt bestehen, bis nichts mehr auf sie zeigt (ledger-b1xwe);
    die Kassenbuch-Seite nutzt seit `ledger-b1xwe` `getLedgerEntriesPage` statt ihrer.
  - `getLedgerEntriesPage` (ledger-b1xwe) — eine **Seite** der Buchungsliste: ein
    Konto (optional, `None` heißt alle Konten) plus Seitenzeiger und Seitengröße.
    Rückgabe: die Buchungen der Seite, die Gesamtzahl der Treffer und die Summe über
    den **gesamten** Treffer (`Shared.Domain.sumMoney`), nicht die Seite — in F#
    gebildet, nie per SQL-`SUM` (ADR 0017). Fundament für die künftigen Filter
    (`ledger-vgfw3`) und die Summenzeile über der Liste.
  - `getLedgerAccountBalances` (ledger-b1xwe) — die **Kontostände** aller Konten in
    einem Aufruf, für die Kontostände-Leiste. Der Stand ist die Summe der nicht in
    YNAB gelöschten Buchungen je Konto (`Shared.Domain.ledgerBalancesByAccount`),
    unabhängig davon, welche Seite der Liste gerade geladen ist — er kommt nie aus
    dem sichtbaren Ausschnitt. `ledgerBalance` (Buchungsliste eines geladenen
    Kontos) bleibt daneben bestehen und rechnet dasselbe, nur aus bereits geladenen
    Buchungen statt aus einer eigenen Abfrage.
  - `getEntryHistory` — die **Historie** einer Buchung, älteste Zeile zuerst: laufende
    Nummer, Zeitpunkt, Herkunft und ein sprechender Satz je Ereignis ("Kategorie in YNAB
    geändert: Lebensmittel → Restaurant"), nie ein Ereignis-Typname (ADR 0013). Die alten
    und neuen Namen stehen in der Payload, deshalb braucht der Satz kein Nachschlagen.
  - `getLastMirrorRun` — der **Lauf-Status** (siehe oben).
- **Herkunfts-Badge** — die Herkunft einer Buchung als Kennzeichen in der Anzeige:
  *YNAB-Spiegel*, *Comdirect-Import*, *Regel*, *Roman in BB*, *Quick Add*,
  *Order-ID-Abgleich* (`Shared.Domain.originLabel`). Die Herkunft einer Buchung ist der
  Actor ihres **ersten** Ereignisses und steht damit fest, sobald die Buchung entsteht.
  Eine in YNAB gelöschte Buchung trägt zusätzlich das Kennzeichen *in YNAB gelöscht*: sie
  bleibt sichtbar und zählt nur beim Kontostand nicht mit.

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
  Quick Add: `Ledger.CommandHandlers.addCashExpense` schreibt zuerst `CashExpenseAdded`
  (Actor `QuickAdd`), ruft danach den Push auf (`YnabClient.createManualTransaction`,
  liefert die YNAB-Transaction-Id aus der Antwort des Einzel-Posts) und verknüpft bei
  Erfolg mit `EntryLinkedToYnab` (ADR 0021, ADR 0015). Scheitert der Push, bleibt die
  Buchung ohne Verknüpfung stehen, kein Rollback. Ohne ein gespiegeltes Kassenbuch-Konto
  für das konfigurierte Quick-Add-Konto (Spiegel nie gelaufen) lehnt `Server.Api` mit
  einer Fehlermeldung ab, statt ein zweites "Bar"-Konto zu erfinden (ADR 0016).
  Comdirect-Import-Push (`Server.Api.importToYnab`, ledger-qvt84): `YnabClient` trennt
  **Vorbereiten** (`prepareTransactions`: Skipped- und Zukunfts-Filter, Transfer-Payee-
  Auflösung, ImportId-Vergabe) und **Senden** (`sendPrepared`: postet genau die
  vorbereitete Liste, liest die YNAB-Transaction-Ids je gesendeter Buchung über die
  ImportId aus der Antwort, über die reine Funktion `buildCreateResult`). Je vorbereiteter
  Buchung schreibt `Ledger.CommandHandlers.pushComdirectImports` zuerst
  `EntryImportedFromComdirect` (Actor `ComdirectImport`, Payee und Memo wie sie tatsächlich
  an YNAB gehen, aus dem vorbereiteten Request, nicht die rohen Bank-Felder, sonst sähe der
  nächste Spiegel-Lauf ein falsches Kopfdaten-Ereignis) und die Kategorisierungs-Ereignisse
  aus der Review-Spur (`recordComdirectPush`, `categorization-gdrx8`:
  `EntryCategorizedByRule` für `ByRule`, `EntryRecategorized` für `ByUser` und für
  `ByOrderIdMatch` mit Actor `OrderIdMatch`, ADR 0025; trägt ein Schritt Kategorie UND
  mindestens zwei Split-Zeilen zugleich (möglich, weil Roman nach dem Aufteilen über den
  Kategorie-Chip "Aufgeteilt" noch eine Kategorie wählt), gewinnen die Splits, dieselbe
  Regel wie beim Push selbst). Existiert schon eine Buchung mit derselben ImportId, prüft
  `findEntryByLink` (System `importid`) zusätzlich, ob sie bereits mit YNAB verknüpft ist:
  nur dann gilt sie als `AlreadyImported` (kein zweites Entstehen, kein erneuter Versand,
  ADR 0021 Punkt 3, wird als `Imported` markiert). Ohne diese Verknüpfung (ein vorheriger
  Versuch scheiterte zwischen Schreiben und Verknüpfen) läuft dieselbe Buchung erneut durch
  Senden und Verknüpfen, sonst würde ein zweiter Klick auf "Import" nach einem
  Netzwerkfehler die Session abschließen, ohne dass die Buchung je YNAB erreicht. Gesendet
  wird nur, was geschrieben wurde; nach Erfolg verknüpft `EntryLinkedToYnab`. Ohne ein
  gespiegeltes Kassenbuch-Konto für das Import-Konto lehnt `Server.Api` mit einer
  Fehlermeldung ab (Entscheidung konsistent mit Quick Add). Push-Fehler, YNAB-Duplikat-
  Ablehnung oder eine nicht auswertbare Antwort lassen die Kassenbuch-Buchung unverändert
  und unverknüpft stehen, kein Rollback. Scheitert das Kassenbuch-Schreiben selbst (z. B.
  eine Invariante von `apply`), bleibt das nicht still: `importToYnab` schließt die Session
  nicht ab und meldet den Fehler (`Ledger.CommandHandlers.summarizeLedgerWriteFailures`).
- *BuchungÄndern* (v2) — Kategorie / Split / Memo in BB ändern, Push folgt.
- *ÜbertragZusammenführen* (v2).
- *KontoBewerten* — Kurs holen, Bewertungs-Buchung schreiben.
- *StückzahlPflegen* — Stückzahl eines Wertpapierkontos setzen.
- *MirrorYnabBudget* — ein Spiegel-Lauf über das ganze Budget. In v1 nur manuell
  ausgelöst ("Jetzt spiegeln"); Frische-Kriterium und Lauf nach Push sind Folge-Arbeit
  (ADR 0014).

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

## Ereignisse
Die Sprache der Historie (ADR 0013). Verbindlicher Katalog, modelliert mit Roman am
2026-09-06; Änderungen hier, nicht im Code allein.

**Umschlag:** Buchungs-Id, laufende Nummer je Buchung, Zeitpunkt (UTC), Actor, Ereignistyp,
Schema-Version, Payload (JSON).

**Actor:** `YnabMirror | ComdirectImport | Rule of RuleId | UserInBb | QuickAdd |
OrderIdMatch of orderId: string`. Der letzte Fall (ADR 0025) markiert eine Kategorie aus
dem Order-ID-Abgleich beim Abruf (`categorization-gdrx8`, `ReviewActor.ByOrderIdMatch`):
fachlich eine Umkategorisierung (Ereignis `EntryRecategorized`), nur mit einem anderen
Urheber als Roman. Anzeigename "Order-ID-Abgleich" (`Shared.Domain.originLabel
MatchedByOrderId`).

**Beobachtungen** (der Spiegel hat in YNAB etwas gesehen):
- `EntryObservedInYnab`: Erstbeobachtung, voller Snapshot (Konto, YNAB-Id, ImportId, Datum,
  Betrag, Payee, Memo, Kategorie mit Name, Splits, Cleared, Übertrag).
- `EntryDetailsChangeObservedInYnab`: Datum, Betrag, Payee, Memo oder Konto geändert (alt/neu).
- `EntryCategorizationChangeObservedInYnab`: Kategorie oder Splits geändert (alt/neu).
- `EntryClearedStateChangeObservedInYnab`: Cleared-Status geändert (alt/neu); eigenes
  Ereignis, damit die Anzeige dieses Rauschen ausblenden kann.
- `EntryDeletionObservedInYnab`: in YNAB gelöscht; die Buchung bleibt mit Flag.

Mehrere Gruppen in einer Beobachtung ergeben mehrere Ereignisse (Details, Kategorisierung,
Cleared). Unveränderte Daten ergeben kein Ereignis.

**Entstehung:** Eine Buchung entsteht mit genau einem von `EntryObservedInYnab`,
`EntryImportedFromComdirect`, `CashExpenseAdded`. Ein zweites Entstehungs-Ereignis auf
derselben Buchung ist ein kaputtes Log und wird von `apply` laut abgelehnt.

**Entscheidungen** (BB hat etwas festgelegt):
- `EntryImportedFromComdirect`: Comdirect-Buchung beim Push angelegt, ohne Kategorie. Die
  Kategorisierung folgt als eigenes Ereignis in derselben Sequenz.
- `EntryCategorizedByRule`: eine Regel hat kategorisiert; die RuleId steht im Actor. Nur
  auf einer noch unkategorisierten Buchung zulässig.
- `EntryRecategorized`: Roman hat in BB kategorisiert oder umkategorisiert (Actor
  `UserInBb`), oder eine Kategorie stammt aus dem Order-ID-Abgleich beim Abruf (Actor
  `OrderIdMatch`, ADR 0025): fachlich dieselbe Kategorisierungs-Änderung, nur mit einem
  anderen Urheber.
- `CashExpenseAdded`: Quick-Add-Buchung angelegt, vor dem Push (Actor `QuickAdd`), darf
  sofort eine Kategorie tragen.
- `ValuationRecorded`: weiterhin reserviert, ohne Payload (Bewertung des
  Wertpapierkontos).

Bei `EntryCategorizedByRule` und `EntryRecategorized` schließen sich Kategorie und Splits
aus, wie bei den Beobachtungs-Ereignissen (ADR 0006/0019). Gesetzte Splits müssen zusätzlich
die `mkSplits`-Invarianten aus ADR 0006 erfüllen (mindestens zwei Zeilen, vorzeichenrichtige
Summe gleich dem Buchungsbetrag, eine Währung); `apply` prüft das über `mkSplits` und lehnt
einen Verstoß laut ab.

**Verknüpfung:** `EntryLinkedToYnab` (Payload: YNAB-Transaction-Id). Die YNAB-Id ist
bekannt geworden, aus der Push-Antwort (Actor `ComdirectImport` bzw. `QuickAdd`) oder vom
Spiegel über die ImportId (Actor `YnabMirror`, ADR 0021). Ändert `Entry` selbst nicht, nur
die Fremd-Id-Verknüpfung (ADR 0015); ob ein Aufruf auf eine bereits verknüpfte Buchung ein
Fehler oder still idempotent ist, entscheidet der Aufrufer (`appendAndApply`-Fassade), nicht
`apply`.

**Konten** haben in v1 keine Historie (reiner Zustand: Id, Name, Art, Verknüpfung,
geschlossen). **Kontostand** ist eine Query über die nicht gelöschten Buchungen.

**Code-Layout** (souls-Muster): `src/Server/Ledger/Events.fs`, `Ledger/Domain.fs` (`apply`,
`observe`), `Ledger/Persistence.fs` (`appendAndApply`, Loads, Replay), später
`Ledger/CommandHandlers.fs` und `Ledger/QueryHandlers.fs`.

## Oberfläche (ledger-xbrht, 2026-09-12)
Das Kassenbuch ist eine **Arbeitsfläche**, kein Bericht. Maßstab: die Arbeit, für die Roman
heute YNAB öffnet, geht hier mindestens so schnell. Scheiterkriterium der Vision: BB wird
langsamer oder umständlicher als YNAB-Web. Entworfen für das ganze Ziel, gebaut in Scheiben
(`ledger-s8m3w`). Briefs je Ablauf mit Romans YNAB-Referenzweg:
`docs/ux/kassenbuch/briefs.md`.

**Geräte.** Etwa 70 % Handy, 30 % PC, beide gleichwertig bedienbar. Am Handy überwiegt
Durchsehen und Prüfen, am PC das Nachbearbeiten. Filter und Summen gehören auf beide
Geräte; in YNAB nutzt Roman den Filter am Handy heute nie, weil er dort zu umständlich ist.

**Einstieg und Navigation.** Das Kassenbuch öffnet im zuletzt gewählten Konto. Konto ist
fachlich ein **Filter**, nicht ein eigener Modus: der Wechsel zwischen Gesamtsicht und
Konto lässt alle anderen Filter und eine laufende Auswahl stehen. Oben die Kontostände
(Handy: waagerecht scrollbare Leiste; PC: linke Spalte, "Alle Konten" zuoberst), darunter
die sticky Filterleiste, darunter die Liste. Erster Chip ist immer "Zu prüfen (n)".

**Liste.** Am Handy Datum als Gruppenkopf, je Zeile Payee, Kategorie, Betrag, darunter
klein Memo und nur die abweichenden Kennzeichen. Am PC eine Tabelle mit Sticky-Kopf
(Auswahl, Datum, Konto in der Gesamtsicht, Payee, Kategorie, Memo, Betrag, Status). In der
Gesamtsicht trägt jede Zeile ihr Konto.

**Prüfen ist eine Geste.** Wisch nach rechts am Handy, Taste G oder die Auswahl-Leiste am
PC. Genau eine Geste, kein zweiter Schritt: in YNAB braucht Roman zwei Wische an einem
anderen Ort als dem, an dem die zu prüfenden Buchungen stehen. Wisch nach links bleibt
bewusst frei, damit die Geste eindeutig bleibt.

**Kategoriewechsel sortiert nie um und prüft nie mit.** In YNAB verschwindet eine Buchung
aus dem Blickfeld, wenn sie beim Kategorisieren nebenbei geprüft und umsortiert wird.

**Bearbeiten, Historie, Flags sitzen im Detail**: am Handy ein BottomSheet, am PC ein
Seitenpanel rechts, damit die Liste sichtbar bleibt. Kategorie über den Picker (auch direkt
aus der Tabellenzelle), Split als Zeilenliste mit Rest-Button wie im Split-Editor der
Import-Prüfung, Memo, Geprüft-Schalter, Herkunft und Historie als Zeitleiste. Split wird
nur hier bearbeitet, nicht aus der Auswahl heraus.

**Rückfrage an Lena** heißt in der Oberfläche "Lena fragen", nicht "Flag setzen": fachlich
weist Roman die Buchung einer Person zu, das YNAB-Flag ist die Umsetzung dahinter. Weiter
gedachte Formen (Benachrichtigung, eine Sicht "Für mich" für Lena) liegen in
`ledger-xfjcp`.

**Auswahl mit Summe.** Sobald ein Filter aktiv ist, steht über der Liste die Trefferzahl
und der Gesamtbetrag des **ganzen** Treffers. Erst wenn Roman eine Teilmenge markiert
(langes Drücken am Handy, Kästchen oder Shift-Klick am PC), zeigt die Auswahl-Leiste
Anzahl und Summe der Auswahl. Der häufige Fall (Kategorie wählen, Summe ablesen) braucht
damit kein Ankreuzen.

**Gelöschte Buchungen** sind standardmäßig ausgeblendet und kommen über einen Filter-Chip
zurück, ausgegraut und ohne Wirkung auf den Kontostand (`ledger-p3n8k`).

**Große Listen.** Der Bestand liegt bei rund 14.000 Buchungen. Die Liste lädt seitenweise
nach (`getLedgerEntriesPage`, 100 Buchungen je Seite, Beobachter am Listenende statt
Button — ledger-b1xwe); Trefferzahl und Summe rechnet der Server über den gesamten
Treffer, nie über den geladenen Ausschnitt. Kontostände kommen aus dem Zustand
(`getLedgerAccountBalances`), nie aus der Liste. Konto ist ein Filter
(`AllAccounts | OneAccount`), "Alle Konten" ist ein eigener, gleichberechtigter Wert,
kein Schalter.

**Was das Konzept voraussetzt und heute nicht gibt** (Scheiben-Material für
`ledger-s8m3w`):
- **Prüfvermerk und Flags im Spiegel.** Der Ereignis-Katalog oben kennt weder `approved`
  noch Flags. Schon die lesende Anzeige "noch nicht geprüft" oder "Rückfrage Lena" braucht
  eine Erweiterung von Snapshot und Beobachtungs-Ereignissen.
- **Available-Betrag je Kategorie.** Der Picker soll je Kategorie den verfügbaren Betrag
  zeigen und beim Wechsel beide Seiten (alt hoch, neu runter), wie die YNAB-App. Der
  Spiegel führt heute keine Budget-Zahlen mit.
- **Filter-Chips und Summenzeile über dem Treffer.** `getLedgerEntriesPage` liefert
  Trefferzahl und Summe bereits (ledger-b1xwe), aber die Seite zeigt heute weder
  Filter-Chips noch die Summenzeile an — das ist `ledger-vgfw3`.

## Open questions
- **Historie-Technik** — *gelöst (ADR 0013, ledger-fsz0a):* append-only Ereignis-Log pro
  Buchung als Quelle der Wahrheit, Zustandsmodell synchron in derselben Transaktion
  mitgeschrieben, reiner `apply`-Replay als Pflichttest. SQLite bleibt, kein Postgres.
- **Spiegel-Mechanik** — *gelöst (ADR 0014, umgesetzt in ledger-m8r2v):* Delta-Sync über
  `server_knowledge` je Budget, Löschung als Flag statt physischem Löschen, 429 bricht den
  Lauf ab und steht im Lauf-Status. Konten werden je Lauf vollständig geholt (ADR 0018).
  Offen bleibt allein der automatische Auslöser (Frische-Kriterium, Lauf nach Push).
- **Kursquelle** — *gelöst (ADR 0016, ledger-zm0zx):* Schnittstelle mit Handeingabe und späterem automatischem Abruf, konkrete Quelle als Research-Spike im Backlog.
- **Bar- und Quick-Add-Konto** — *gelöst (ADR 0016):* ein Konto "Bar", Quick Add ist Herkunft.
- **Quick Add** direkt ins Kassenbuch? *Gelöst (ADR 0021, 2026-09-11):* erst Kassenbuch, dann Push, dann Verknüpfung (`ledger-tdnpk`).
- **Identität der Buchung** — *gelöst (ADR 0015, ledger-z5vw6):* eigene Id
  (`Guid.CreateVersion7()`), Fremd-Ids (YNAB, ImportId, Comdirect) als Verknüpfungen in
  eigener Tabelle; Spiegel matcht YNAB-Id vor ImportId, sonst neue Buchung als Spiegel.
