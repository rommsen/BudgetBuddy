# ledger

## Purpose
BudgetBuddys eigenes, dauerhaftes **Kassenbuch** über alle Konten. Hier bleibt jede
Buchung, mit ihrer Herkunft und ihrer vollständigen, auditierbaren Historie. Das
Kassenbuch zeigt Buchungen und Stände pro Konto und in der Gesamtsicht, auch für Konten,
die YNAB nicht führen kann (Wertpapierkonto mit Budget-Geld). Es ist das Gedächtnis, das
BB zum führenden System macht (ADR 0012), und wird schrittweise der Ort, an dem Buchungen
nachbearbeitet werden.

Was hier **nicht** passiert: Bank-Abruf (`banking-import`), Regel-Matching
(`categorization`), YNAB-API-Verkehr (`ynab-sync`), Budgetieren (`budgeting`, ADR 0063).

## Classification
**core** — ohne Kassenbuch gibt es weder Ownership noch Historie noch das ETF-Konto; die
Vision-Stufen v1 und v2 spielen fast vollständig hier.

## Actors
- **Roman** — liest pro Konto und gesamt, bearbeitet Buchungen nach (v2), pflegt die
  Stückzahl des Wertpapierkontos.
- **Frau** — liest mit (gemeinsames Konto, keine eigene Identität).
- **YNAB** — über `ynab-sync`: Quelle des Spiegels und Ziel des Pushs.
- **Kursquelle** — liefert den Kurs für die Bewertung (monatlich reicht).
- **Bob** (ledger-b0b7q): Hermes-Agent auf einer Hetzner-VM. Legt über die `/bot/`-Schnittstelle Barbuchungen auf dem Quick-Add-Konto an, ohne Lesezugriff auf Kassenbuch oder Salden (ADR 0066). Eigener Actor `Bob`, Herkunft `AddedByBob`, Anzeigename "Bob".

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
  *Bob* (Bot-Schnittstelle, ADR 0066), *Regel* (Auto-Kategorisierung), *Roman in BB*, *YNAB-Spiegel* (dahinter ING-Import,
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
  `comdirect`, `ynabsuperseded` für abgelöste YNAB-Ids nach einem Ersatz, ADR 0058), die auf genau eine Buchung zeigt. Nicht Teil der Buchungs-Id, sondern
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
  **Auslöser** (ADR 0014, `ledger-h4p6c`): manuell ("Jetzt spiegeln"), Frische-Kriterium
  (Seitenaufruf und PWA-Vordergrund auf der Kassenbuch-Seite prüfen den geladenen
  Lauf-Status gegen die 5-Minuten-Schwelle, `Components.Ledger.Types.isMirrorStale`, und
  starten bei Bedarf denselben Hintergrund-Lauf wie der manuelle Knopf) und Lauf nach Push
  (nach einem erfolgreich gesendeten Comdirect-Import oder Quick Add stößt der Server den
  Lauf selbst an, `Ledger.CommandHandlers.triggerMirrorInBackground`, erst nachdem
  `EntryLinkedToYnab` geschrieben ist, ADR 0021). Alle drei laufen durch dieselbe
  `mirrorYnabBudget`-Stelle; ihr **Einzel-Flug-Schutz** (`runMirrorGuarded`) sorgt dafür,
  dass zwei gleichzeitige Auslöser (zwei Tabs, oder ein Seitenaufruf zeitgleich mit einem
  Lauf-nach-Push) nur einen YNAB-Abruf ergeben und das Ergebnis teilen (kein Server-Timer,
  ADR 0014 lehnt ihn ausdrücklich ab).
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
    Konto (optional, `None` heißt alle Konten) plus Seitenzeiger und Seitengröße,
    dazu `IncludeDeleted` (ledger-p3n8k, Standard `false`). Rückgabe: die Buchungen
    der Seite, die Gesamtzahl der Treffer, die Summe über den **gesamten** Treffer
    (`Shared.Domain.sumMoney`), nicht die Seite (in F# gebildet, nie per SQL-`SUM`,
    ADR 0017), und `DeletedCount`, die Zahl der in YNAB gelöschten Buchungen im
    Filter, unabhängig von `IncludeDeleted`. Ohne `IncludeDeleted` bleiben gelöschte
    Buchungen aus Seite, Trefferzahl und Summe außen vor, sodass die sichtbare Liste
    sich zum Kontostand summiert; `DeletedCount` bleibt trotzdem bekannt, damit die
    Seite "N gelöschte Buchungen ausgeblendet" auch zeigen kann, während sie
    ausgeblendet sind. Ist `IncludeDeleted = true` gesetzt, zählen gelöschte
    Buchungen dagegen bewusst in Seite, Trefferzahl und Summe mit (Variante 2,
    Entscheidung Roman 2026-09-18, `ledger-d4kx8`); der Kontostand
    (`getLedgerAccountBalances` / `Shared.Domain.ledgerBalancesByAccount`) lässt sie
    davon unabhängig immer draußen, die Abweichung zwischen Summenzeile und
    Kontostand ist damit gewollt. Grundlage für den serverseitigen Such-Filter
    (`ledger-vgfw3`, erledigt) und die Summenzeile über der Liste. Die Seite folgt `query.Sort` (ADR 0051, `Sortierung (Kassenbuch)`); die Sortierung wirkt nur auf die Reihenfolge der Seite und geht in keinen Zählweg ein.
  - `getLedgerPayeeSuggestions` (ledger-vgfw3): die **Payee-Vorschläge** eines
    Filters, jeweils mit ihrer Anzahl innerhalb des aktuellen Treffers, nie über
    alle Buchungen und nie über die geladene Seite. Grundlage der Payee-Auswahl
    im Filter-Chip (ADR 0036). Teilt sich dieselben Such-Kriterien wie
    `getLedgerEntriesPage` (Kategorie, Zeitraum, Betrag, Memo, Payee selbst
    ausgenommen), baut sich den `EntryFilter` dafür aber bewusst separat auf
    statt `toEntryFilter` zu benutzen (zwei parallele Aufbauten, ADR 0036);
    `Payee` steht darin fest auf `None`, weil der Aufruf nie einen bereits
    gewählten Payee-Filter an sich selbst übergibt. Details zu Gruppierung und
    Faltung stehen beim Eintrag "Such-Filter (Kassenbuch)" unter Ubiquitous
    language.
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
  *YNAB-Spiegel*, *Comdirect-Import*, *Regel*, *Roman in BB*, *Quick Add*, *Bob*,
  *Order-ID-Abgleich* (`Shared.Domain.originLabel`). Die Herkunft einer Buchung ist der
  Actor ihres **ersten** Ereignisses und steht damit fest, sobald die Buchung entsteht.
  Eine in YNAB gelöschte Buchung trägt zusätzlich das Kennzeichen *in YNAB gelöscht*: sie
  ist in der Kassenbuch-Liste standardmäßig ausgeblendet (`ledger-p3n8k`), kommt über
  den Schalter gedämpft und durchgestrichen zurück und zählt beim Kontostand nie mit.
- **Prüfvermerk** — ob eine Buchung noch zu prüfen ist: `NeedsReview` ist wahr, wenn
  `Approved = false` oder `Cleared = Uncleared` (`Ledger.Domain.needsReview`, ADR 0032).
  Ungeprüfte Buchungen tragen einen Punkt am linken Zeilenrand (Handy) bzw. einen linken
  Rahmen (PC), unabhängig vom Flag.
- **Flag** — YNABs `flag_color`/`flag_name`, roh im Zustand gehalten, gedeutet im Lesepfad
  (`Ledger.QueryHandlers.ReadModel.flagOf`, ADR 0032): der Name "Lena" (getrimmt, ohne
  Beachtung der Groß-/Kleinschreibung) ergibt *Rückfrage Lena* (violett), jedes andere
  gesetzte Flag *Markiert* (orange), sonst kein Kennzeichen.
- **Zu prüfen** — der Chip "Zu prüfen (n)" über der Liste
  (`Components.Ledger.View.needsReviewChipLabel`): n ist die Zahl der ungeprüften, nicht
  gelöschten Buchungen im gewählten Kontofilter, unabhängig davon, ob der Filter aktiv ist.
  Antippen kippt `OnlyNeedsReview`.
- **Historie-Anzeige** (ledger-h9tq2, Entscheidung Roman 2026-09-18) — im
  Detail-Sheet steht die neueste Zeile oben. Gedreht wird ausschließlich in der
  Anzeige (`Components.Ledger.View.historyLinesForDisplay`); `getEntryHistory`
  und der Lesepfad bleiben aufsteigend, älteste Zeile zuerst (siehe oben,
  `getEntryHistory`). Die laufende Nummer je Zeile bleibt die aus dem
  Ereignis-Log und zählt in der Anzeige deshalb von oben nach unten abwärts.
  Diese Richtung gilt auch für die Detail-Sicht aus `ledger-q1hw2`, die sie
  übernimmt statt neu zu entscheiden.
- **Such-Filter (Kassenbuch)**: die fünf Kriterien Kategorie (Mehrfachauswahl), Zeitraum (von/bis, randtag-einschließend), Payee (exakt, ohne Groß-/Kleinschreibung, auch für Umlaute gefaltet), Betrag (von/bis) und Memo (Teiltext, ohne Groß-/Kleinschreibung, auch für Umlaute gefaltet) auf `LedgerEntryPageQuery`/`LedgerPayeeSuggestionQuery`. Gelten als Und-Verknüpfung über alle vier Zählwege (`loadEntriesPage`, `countEntries`, `loadAmountsForQuery`, `countDeletedEntries`) und `loadPayeeSuggestions` hinweg, gebündelt in `Ledger.Persistence.EntryFilter`/`buildFilterClause`. Payee und Memo vergleichen über die anwendungsdefinierte SQL-Funktion `bb_lower` (ADR 0037), nicht über eine SQLite-Kollation: `müller` findet `MÜLLER GMBH` ebenso wie `Müller GmbH`. Die Faltung bildet nur Groß-/Kleinschreibung ab, keine Diakritika-Entfernung: `mueller` findet `Müller` nicht, `STRASSE` findet `Straße` nicht. `countNeedsReviewEntries` (Chip "Zu prüfen") bleibt bewusst außen vor: fester Kontozähler, keine Trefferzahl der laufenden Suche. Der Kategorie-Filter trifft die einfache Kategorie und Split-Zeilen-Kategorien (ADR 0038): eine Buchung, deren einzige Kategorie in einem Split steckt, findet er jetzt auch, über eine abgeleitete Indextabelle (`ledger_entry_split_categories`), die in derselben Transaktion wie Log und Zustand geführt wird. Neben den fünf Kriterien wirkt der Chip "Ohne Kategorie" (`WithoutCategory`, ADR 0048) über dieselbe `buildFilterClause` auf alle Lesewege: per Und-Verknüpfung mit den übrigen Kriterien, aber per Oder in gemeinsamer Klammer mit einer nicht leeren Kategorienauswahl. Zeitraum vergleicht nur den Datumsanteil (erste 10 Zeichen des gespeicherten ISO-8601-Textes), Kind-unabhängig (ADR 0036). `loadPayeeSuggestions` gruppiert seit ledger-h4qtx über dieselbe Faltung (`Core.searchFold`) statt über die literale Schreibweise: `Rewe` und `REWE` erscheinen als ein Vorschlag mit der Summe beider Anzahlen, angezeigt wird die häufigste vorkommende Schreibweise, bei Gleichstand die lexikografisch kleinste nach Ordinalvergleich. Die Sortierung (`Sort` auf `LedgerEntryPageQuery`) ist kein Such-Filter: sie steht nicht in `EntryFilter` und geht in keinen der Zählwege ein (ADR 0051).
- **Kategorie-Stammdaten** (`ledger_categories`) — gespiegelte Kategorie-Id, Name und Gruppenname, nach dem Muster der Konten. Am Ende jedes Spiegel-Laufs, bestes Bemühen: weder ein fehlgeschlagener Abruf noch eine Ausnahme lässt den Lauf scheitern (ADR 0039). Auch ein fehlgeschlagenes Schreiben lässt den Lauf `Ok`, sichtbar nur auf stdout. Voller Abruf je Lauf, nur Upsert, nie Löschen: eine in YNAB gelöschte Kategorie behält ihre letzte Beschriftung. Code: `Ledger.Persistence.upsertCategories`, `Ledger.Persistence.loadCategoryGroups`. Der Lesepfad qualifiziert die Gruppe an die Buchung (`ledger-h6ptn`, ADR 0039): `Ledger.QueryHandlers.ReadModel.toEntryView` nimmt die Zuordnung Kategorie-Id -> Gruppenname als Parameter (einmal je Abfrage geladen, kein I/O in der Funktion selbst) und füllt `LedgerEntryView.CategoryGroupName`; fehlt die Zuordnung (Kategorie nie gespiegelt), bleibt das Feld `None`, nie ein erfundener Gruppenname. Die PC-Tabelle zeigt `categoryLabelQualified` ("Gruppe: Name"), die Handy-Zeile bleibt bei `categoryLabel` (nur der Blattname); die Historie schlägt nichts nach und bleibt beim Blattnamen. Alte Buchungen zeigen die heutige Gruppe, nicht die von damals: eine Umbenennung ist Korrektur der Einordnung, kein neuer Sachverhalt. Der Blattname wird seit ADR 0044 ebenfalls live aufgelöst: `Ledger.Persistence.loadCategoryNames` liefert die Zuordnung Kategorie-Id -> heutiger Name, `ReadModel.toEntryView` nimmt sie als neuen ersten Parameter `categoryNames` und setzt damit `CategoryName` (Kopf) und jedes `ToCategory`-Split-Ziel, mit Rückfall auf den gespeicherten Namen bei fehlendem oder leerem Treffer. Für ältere Ereignisse mit gleicher Kategorie-Id und anderem Namen lautet der Historien-Satz "Kategorie in YNAB umbenannt: A → B".
- **Split-Zeilen-Übertrag**: ein Übertrag, den YNAB als Zeile innerhalb einer
  Split-Buchung führt (im Kassenbuch `SplitTarget.ToTransfer`), nicht als zwei vollständige
  Buchungen. Eine Buchung kann null bis n solcher Zeilen tragen. Die Zeile wird über YNABs
  Subtransaction-Id adressiert, nicht über ihre Position. Nur die Split-Seite trägt die
  Fremd-Id der Gegenbuchung (`transfer_transaction_id` der Zeile), die Gegenbuchung selbst
  trägt keine Rückreferenz (ADR 0046).
- **Split-Übertrags-Verknüpfung**: die aufgelöste Beziehung einer Split-Zeile zur
  Kassenbuch-Buchung ihrer Gegenseite: `Entry.SplitTransferCounterparts`, eine `Map` von
  Subtransaction-Id auf Buchungs-Id. Unabhängig von `Entry.TransferEntryId` (Übertrag auf
  Buchungsebene); beide schließen sich nach YNABs Regel aus, das Kassenbuch erzwingt es
  nicht (ADR 0046).
- **Ohne Kategorie (Chip, Kassenbuch-Filterleiste)**: Filter auf Buchungen ohne Ziel (ADR 0048). Ein Übertrag ist ein Ziel wie eine Kategorie: eine einfache Buchung ist ein Treffer, wenn `category_id`, `transfer_account_id` und `transfer_entry_id` leer sind; eine Split-Buchung, wenn keine Zeile ein Ziel hat (jede Zeile `Uncategorized`). Eine Split-Buchung mit teilweiser Lücke ist bewusst kein Treffer. In YNAB gelöschte Buchungen folgen nur `IncludeDeleted`. `Entry.TransferAccountId` projiziert YNABs `transfer_account_id` roh in `ledger_entries.transfer_account_id` (einmaliger Backfill aus `EntryObservedInYnab`, wenn die Spalte neu angelegt wird, das Log wird nur gelesen); die abgeleitete Indextabelle `ledger_entry_split_transfers` (Konto-Ids der `ToTransfer`-Zeilen, `Ledger.Domain.splitTransferAccountIds`) wird mit `ledger_entry_split_categories` in derselben Transaktion geführt (`writeSplitIndexes`, Neuaufbau `rebuildSplitIndexes`, ehemals `rebuildSplitCategoryIndex`). Reiner Toggle im Client (`Filters.WithoutCategory`, `ToggleWithoutCategory`), kein Teil von `hasAnyFilter` und von "Alle Filter löschen" unberührt.
- **Prüfvermerk nicht in YNAB**: der offene Fehlschlag beim Zurückschreiben eines in BB gesetzten Prüfvermerks (`Entry.ReviewMarkWriteBackFailure`, ADR 0050): der PUT an YNAB scheiterte oder die Buchung ist noch nicht mit YNAB verknüpft. Der Prüfvermerk bleibt in BB gesetzt und `NeedsReview` zählt nach dem BB-Stand weiter. Die Zeile trägt den Hinweis mit dem Grund und der Schaltfläche *Erneut senden* (`retryReviewMarkWriteBack`). Der Zustand endet mit `EntryReviewMarkWrittenBackToYnab` (Erneut senden hatte Erfolg, oder der Spiegel sieht YNAB auf dem BB-Stand), durch ein Beobachtungs-Ereignis von YNAB (`EntryApprovalChangeObservedInYnab`/`EntryClearedStateChangeObservedInYnab`, der YNAB-Stand gilt) oder durch eine neue Geste auf der Buchung.
- **Lokal entfernte Zeilen (LocallyRemoved)**: Zahl der Zeilen, die der Client aus den schon geladenen Seiten genommen hat (z. B. geprüft bei aktivem Chip Zu prüfen). Die Seitenanfrage trägt sie mit, der Server beginnt die Seite bei Page * PageSize - LocallyRemoved, damit keine Buchung an der Seitengrenze fehlt.
- **Wisch geprüft**: Handy-Geste im Kassenbuch (ledger-th36h): Wisch nach rechts auf einer zu prüfenden Karte setzt `setReviewMark Reviewed`; der Toast "<Payee> geprüft" bietet "Rückgängig" (`restoreReviewMark` mit dem Vorzustand aus der Antwort). Wisch nach links bleibt unbelegt, auf einer geprüften Karte gibt es keine Geste.
- **Auswahl**: eine Menge über den Treffer des Kassenbuchs, im Client als `Picked` (gewählte geladene Zeilen), `AllExcept` (ganzer Treffer außer den abgewählten geladenen Zeilen) oder `NoSelection` gehalten (ledger-r1d2f). Anzahl und Summe rechnet der Client aus `TotalCount` und `TotalAmount` des Treffers (`selectionTotals`), in YNAB gelöschte Buchungen zählen nicht zur Summe. Konto-, Filter- oder Chipwechsel leert die Auswahl, eine Zeile, die die Liste verlässt, fällt aus ihr. Der Auswahl-Modus am Handy (langes Drücken) und die Kästchen am PC schalten sie; die Auswahl-Leiste trägt die Sammelaktionen **Geprüft für die Auswahl** (ledger-nhwrt, die einzige primäre), **Kategorie setzen** (ledger-w29y6) sowie **Lena fragen** und **Rückfrage zurücknehmen** (ledger-y54gr).
- **Fokuszeile (keyboardCursor)**: die Zeile der Kassenbuch-Liste, auf der die Tastatur am PC steht (`FocusedRow`). Pfeil auf/ab und J/K bewegen sie, am Listenrand bleibt sie stehen. G setzt auf ihr den Prüfvermerk `Reviewed` und springt auf die nächste Zeile, Umschalt+G setzt `Unreviewed`. Fällt ihre Zeile aus der Liste, rückt der Fokus nach (am Ende auf die neue letzte Zeile, nach einem Neuladen auf die erste). Die Leertaste nimmt die Fokuszeile in die Auswahl oder heraus (wie ein Klick auf ihr Kästchen, mit Anker). Bei laufender Auswahl (am PC: nicht leer, am Handy: Auswahl-Modus) sind G, die Prüf-Schaltfläche an der Zeile und der Wisch aus, Enter und ein Klick auf die Zeile öffnen kein Detail, und eine beginnende Auswahl schließt ein offenes Detail; der einzige Prüfweg ist "Geprüft" in der Leiste. Eingabefelder und offene Popover fangen die Tasten ab. Enter öffnet das Buchungs-Detail der Fokuszeile. Esc schließt genau eine Stufe: erst das Popover, dann das Detail, dann die Auswahl. Ein offenes Seitenpanel folgt der Fokuszeile und sperrt die Listentasten nicht, ein offenes Sheet sperrt sie (ledger-q1hw2, ledger-w4vte, ADR 0053).
- **Push-Fehlschlag**: der offene Fehlschlag, eine Buchung nach YNAB zu senden (`Entry.PushToYnabFailure`, `LedgerPushFailure` mit Grund und Zeitpunkt, `LedgerEntryView.PushToYnabFailure`, ledger-rynk5, ADR 0021 Punkt 2). Entsteht durch `EntryPushToYnabFailed` im Comdirect-Import-Push, in Quick Add und beim erneuten Senden aus dem Detail (`retryPushToYnab`, Actor `UserInBb`; Netz, Timeout, YNAB-Fehler, Duplikat, nicht auswertbare Antwort), aufgelöst durch `EntryLinkedToYnab`, gleich ob aus der Push-Antwort, aus "Erneut senden" oder vom Spiegel. Ein zweiter Fehlschlag ersetzt den ersten. Kein Rollback: die Buchung bleibt unverknüpft stehen.
- **LinkedToYnab**: die Buchung trägt eine YNAB-Verknüpfung (`Entry.LinkedToYnab`, `LedgerEntryView.LinkedToYnab`). Vom Spiegel gesehene Buchungen sind es von Anfang an, BB-geführte ab `EntryLinkedToYnab`. Spiegelt die Fremd-Id-Tabelle (ADR 0015) im Zustand, Spalte `linked_to_ynab` mit Backfill beim Anlegen.
- **Geprüft für die Auswahl**: die primäre Aktion der Auswahl-Leiste (ledger-nhwrt, Handy Reihe 2 neben Alle und Auswahl aufheben, PC rechts). Sie setzt `Reviewed` für jede Buchung der Auswahl (`setReviewMarkBatch`), hebt die Auswahl auf und zeigt den Toast "n Buchungen geprüft" mit den Zahlen ohne Änderung und nicht in YNAB. Ein Rückgängig stellt den Vorzustand des ganzen Stapels her; danach lädt die Liste neu.
- **Sortierung (Kassenbuch)**: `LedgerSort` (`Field`: `SortByDate | SortByAmount | SortByPayee | SortByCategory | SortByAccount`, `Direction`: `SortAsc | SortDesc`, Standard `LedgerSort.defaultSort` = Datum absteigend) als Pflichtfeld `Sort` auf `LedgerEntryPageQuery` (ADR 0051, ledger-r72re). Die Sortierung ist kein Such-Filter: sie steht nicht in `EntryFilter` und wirkt nur auf `loadEntriesPage` (Parameter `sort`), also auf die Reihenfolge der Seite von `getLedgerEntriesPage`, nie auf `countEntries`, `loadAmountsForQuery`, `countDeletedEntries`, `countNeedsReviewEntries` oder `loadPayeeSuggestions`. `TotalCount`, `TotalAmount`, `DeletedCount` und `NeedsReviewCount` sind damit für jede Sortierung gleich. Datum, Payee und Konto sortieren in SQL (`ORDER BY`, Payee über `bb_lower`, Konto über eine korrelierte Teilabfrage auf `ledger_accounts`), Betrag und Kategorie sowie jeder Betragsfilter in F# (`loadAllMatching`, dann Vergleich, dann `safePage`): der Betrag ist Text (ADR 0017), der Kategorie-Name entsteht erst im Lesepfad. Schlüssel: Betrag als vorzeichenbehafteter `decimal`; Payee und Konto als gefalteter Text (`Core.searchFold`), ordinal verglichen (Umlaute hinter `z`); Kategorie mit vier festen Rängen (eigene Kategorie, Split, Übertrag, ohne Kategorie), nur Rang 0 sortiert nach dem gefalteten, voll qualifizierten Namen `Gruppe: Name` und kehrt sich mit der Richtung um. Der Name kommt aus `Ledger.Persistence.liveCategoryName`/`categoryGroupOf` (dieselben Funktionen nutzt `ReadModel.toEntryView`), zusammengesetzt von `qualifiedCategoryName`. Leere Werte (Payee leer, Kategorie-Ränge 1 bis 3) stehen in beiden Richtungen am Ende. Tiebreaker: bei Datum `id` in der Richtung des Datums, sonst `date DESC, id DESC`. `resolveReviewMarkBatchSelection` liest das `Sort` der Anfrage nicht und ruft `loadEntriesPage` mit `LedgerSort.defaultSort` auf (ADR 0051 Punkt 3). Kein Index (Messung bei 14.000 Buchungen: größter Median 645 ms, `scripts/measure-ledger-sort.fsx`).
- **Zellmenü**: zurückgebaut mit ledger-q1hw2 (ADR 0053): `cellMenuPopover`, `Model.CellMenu`, `OpenCellMenu`/`CloseCellMenu`, die Auslöser in Payee- und Kategorie-Zelle und der Aktionsblock im alten `historySheet` gibt es nicht mehr, die Zellen sind reiner Text und ein Klick ist ein Zeilenklick. Geblieben: `FilterByRowPayee` und `FilterByRowCategory` sind die Nachrichten der Filter-Knöpfe im Buchungs-Detail (Payee wie `SelectPayeeFilter`, Kategorie als Auswahl aus genau dieser Id ohne "Ohne Kategorie", ADR 0048) und lassen das Detail offen. `payeeMenuLabel` und `categoryMenuLabel` sind die Bedingung, ob ein Knopf da ist (kein Payee, keine Kategorie, Split, Übertrag ohne Kategorie: keiner). `LedgerEntryView.CategoryId: Guid option` bleibt (gefüllt in `ReadModel.toEntryView`, kein Ereignis, kein Schema).
- **Sortierung im Client (Kassenbuch)**: `Model.Sort: LedgerSort` (Standard `LedgerSort.defaultSort`, nicht gespeichert, übersteht Konto- und Filterwechsel und "Alle Filter löschen"). `SetSort field` dreht die Richtung beim aktiven Feld, sonst gilt `sortStartDirection` (Datum absteigend, Betrag aufsteigend, Payee, Kategorie, Konto aufsteigend); der Wechsel läuft wie ein Filterwechsel über `resetEntries` (Seite 0, Auswahl leer). `effectiveSort model` liefert `defaultSort`, wenn `SortByAccount` bei einem einzelnen Konto gewählt ist; `buildPageQuery` und die Köpfe lesen `effectiveSort`. Tagesgruppen und Tagessummen nur bei `usesDayGroups` (Sortierung nach Datum, beide Richtungen), sonst flache Liste ohne Tagesköpfe, in der Handy-Zeile mit Datum vorn in Zeile 2. PC-Köpfe Datum, Konto, Payee, Kategorie, Betrag sind `Table.calmSortableHeaderCell` (aria-sort, Button darin), Memo und Status nicht sortierbar (ADR 0051, ledger-bn913).
- **Sortier-Chip**: letzter Eintrag der Filterleiste, nur wenn die Liste als Kompaktliste (Handy-Liste) rendert. Zeigt die wirksame Sortierung kurz ("Datum ↓", "Betrag ↑"), ist nur bei Abweichung von `LedgerSort.defaultSort` aktiv gefärbt, hat kein "×" und gehört weder zu den Filter-Chips noch zu `hasAnyFilter`. Ein Tipp öffnet das Sortier-Sheet (`OpenSortSheet`); dessen Zeilen (`sortOptions`) setzen Feld und Richtung zugleich (`SetSortExactly`).
- **Buchungs-Detail**: die eine Ansicht zu einer Buchung (ADR 0053): Seitenpanel (420 px) am PC, BottomSheet am Handy, in der Kompaktliste und wo neben dem Panel keine Tabellenstufe mehr passt. Kopf mit Betrag und "Konto · Datum", Hinweisleiste (in YNAB gelöscht, Noch nicht in YNAB, Prüfvermerk, Kategorie, Flag oder Memo nicht in YNAB), Felder (Payee, Kategorie, Split, Memo, danach bei Amazon- oder PayPal-Buchungen die Link-Zeile (ledger-rjhwp); Memo mit Knopf Memo hinzufügen bzw. Bearbeiten und Eingabefeld; die Kategorie steht im Sheet am Handy kurz, am PC qualifiziert), Geprüft-Schalter, Herkunft, Zeitleiste. Die Filter-Knöpfe sitzen rechts in den Feldzeilen von Payee und Kategorie, bündig untereinander (Payee als eigene Feldzeile seit 2026-10-21). Die Link-Zeile zeigt je Eintrag aus `LedgerEntryView.ExternalLinks` einen Link in neuem Tab (Amazon-Bestellung "Bestellung <id>" mit Deep-Link, ohne Bestellnummer die Bestellübersicht "Amazon Orders", PayPal "PayPal Activity"); ohne Treffer fehlt die Zeile, die Liste zeigt nie einen Link. Ersetzt das Zellmenü und das Historie-Sheet.
- **Kategorie nicht in YNAB**: der offene Fehlschlag beim Zurückschreiben einer in BB gesetzten Kategorie (`Entry.CategorizationWriteBackFailure`: Grund und Baseline, ADR 0055, `ledger-k2rbn`). Die Baseline ist der Stand, den YNAB vermutlich noch hat (Kategorie und Splits vor der ersten gescheiterten Änderung); bei weiteren Änderungen mit offenem Fehlschlag bleibt die erste Baseline. Kein Rollback, die Kategorie bleibt in BB gesetzt. `observe` vergleicht den beobachteten YNAB-Stand mit Baseline und BB-Stand (`categorizationIdentity`): gleich Baseline heißt kein Ereignis, gleich BB-Stand heißt `EntryCategorizationWrittenBackToYnab`, jeder andere Stand ist eine echte Änderung in YNAB und beendet den Fehlschlag. Ohne offenen Fehlschlag gewinnt YNAB jeden abweichenden Stand. Die Leseform ist `LedgerEntryView.CategorizationWriteBackFailure` (nur der Grund). Beim Ersetzen einer Split-Buchung trägt der Fehlschlag zusätzlich `YnabDeletedTransactionId` ("in YNAB gelöscht, Neuanlage offen", ADR 0058, ADR 0059).
- **Schreib-Sperre**: `Ledger.CommandHandlers.WriteGuard.exclusive` ist ein gemeinsamer Wächter für den Spiegel-Lauf (`runMirrorGuarded`) und die Kategorie- und Split-Befehle `setEntryCategory`, `setEntryCategoryBatch`, `setEntrySplits` und `retryCategorizationWriteBack` (ADR 0055, Punkt 6). Ein Befehl läuft nie während eines Spiegel-Laufs und wartet höchstens auf dessen Ende, damit ein Lauf zwischen Ereignis und PUT die BB-Kategorie nicht zurückdreht.
- **Available frisch halten** (ledger-kwwxz): Der Kassenbuch-Client merkt sich den Zeitpunkt des letzten erfolgreichen Kategorien-Abrufs (`CategoriesLoadedAt`) und lädt `getCategories` beim Seitenaufruf, beim PWA-Vordergrund und beim Öffnen des Pickers nur nach, wenn der Stand älter als zwei Minuten ist und kein Abruf läuft. Ein Fehler lässt den alten Stand stehen. Nach einer Kategorieänderung schreibt `shiftAvailable` den Betrag lokal fort (nur Buchungen im laufenden Monat), `categoryChangeHint` liefert die Zeilen der Karte "Budget nach dem Wechsel" (alt, neu), `availableAgeLabel` die Fußzeile des Pickers. Der nächste Abruf überschreibt (ADR 0055 Punkt 8).
- **Split-Zeilen-Gruppen** (`LedgerEntryView.SplitCategoryGroupNames`, `ledger-b7qmx`, ADR 0045): `Map<Guid, string>` je Buchung: Kategorie-Id einer `ToCategory`-Split-Zeile auf ihren Gruppennamen. `ReadModel.toEntryView` füllt sie aus derselben Gruppen-Zuordnung wie `CategoryGroupName`; eine Kategorie ohne bekannte oder mit leerer Gruppe hat keinen Eintrag, Übertrag- und offene Zeilen nie. Das Detail setzt "Gruppe: Name" über die gemeinsame Client-Funktion `qualify` zusammen (dieselbe Regel wie `categoryLabelQualified`); die Listenzeile bleibt bei "N Split-Zeilen".
- **Kategorie ändern im Detail** (ledger-sfab0, ADR 0055): der Pfeil rechts an der Kategorie-Zeile des Buchungs-Details (auch ein Klick auf die Zeile) öffnet den Kategorie-Picker (`CategoryPicker.popoverInPanel` am PC, `sheetLayered` am Handy). Die Auswahl läuft über `StartSetEntryCategory`, `RunSetEntryCategory`, `EntryCategorySet` (Zustand je Buchung in `Model.CategoryRows`, wie `ReviewMarkRows`). Nach `CategoryChanged` schreibt der Client Available mit `shiftAvailable` fort, zeigt die Karte "Budget nach dem Wechsel" (`Model.CategoryCard`, etwa 6 Sekunden, endet mit Detail, Wechsel, Esc und nächster Änderung) und den Toast "Kategorie geändert: <Name>" mit "Rückgängig" (`UndoCategory`, derselbe Schreibweg mit `Previous`, zweites `EntryRecategorized`, keine neue Karte). `CategoryUnchanged` erzeugt weder Karte noch Toast. Gesperrt (Pfeil gedämpft mit Hinweistext, `categoryLockFor`): in YNAB gelöscht, Übertrag ohne Kategorie. Bei einer Split-Buchung öffnet der Pfeil den Split-Editor (`CategoryOpensSplitEditor`, ledger-pafkp), gesperrt nur mit den Sperren des Split-Schreibwegs (`CategoryLockedSplit`). Ein offener Fehlschlag zeigt die Hinweisleiste "Kategorie nicht in YNAB" mit "Erneut senden" (`categoryHintFor`, `retryCategorizationWriteBack`), auch nach dem Neuladen. Die Vorschläge zum Payee liefert die Abfrage `getPayeeCategorySuggestions` (Payee ohne Groß-/Kleinschreibung, nicht in YNAB gelöscht, Kategorie gesetzt, höchstens drei, häufigste zuerst, bei Gleichstand die jüngste Buchung, ohne die aktuelle Kategorie); der Client ruft sie einmal je Öffnung, ein Fehler lässt den Abschnitt weg.
- **Zeile bleibt stehen (ausgegraut, Neu laden)** (ledger-sfab0, ADR 0055 Nachtrag): Eine Kategorieänderung sortiert die Zeile nie um. Fällt sie aus dem aktiven Filter (`rowMatchesFilters`: Kategorie-Auswahl oder Chip "Ohne Kategorie"), bleibt sie ausgegraut mit dem Vermerk "passt nicht mehr zum Filter" stehen (`Model.FilteredOut`): `LocallyRemoved` steigt um eins, Treffer und Summe sinken sofort, die Zeile ist nie gewählt (`selectionForRows`), Rücknahme kehrt alles um, Neuladen, Filter- und Sortierwechsel leeren die Menge. Ändert sich der Schlüssel der aktiven Sortierung (`SortByCategory`), setzt der Client `OrderStale`: Hinweisleiste "Reihenfolge oder Treffer haben sich geändert. Neu laden", das Nachladen ruht, Treffer und Summe laden neu (`RunRefreshTotals`), "Neu laden" (`LoadEntries`) löscht das Merkmal, eine Rücknahme nicht.
- **Split-Schreibweg** (ADR 0058, `ledger-kxme4`): der Befehl `Ledger.CommandHandlers.setEntrySplits` setzt die Zeilen einer Buchung in BB und zieht YNAB nach. Zwei oder mehr Zeilen teilen auf oder ersetzen die Zeilen, genau eine Zeile (Kategorie oder unkategorisiert, voller Betrag) hebt den Split auf. Das Aufteilen einer in YNAB unaufgeteilten Buchung ist ein PUT mit `category_id` null und `subtransactions`; Ändern und Aufheben einer in YNAB aufgeteilten Buchung sind DELETE und POST (`YnabClient.deleteTransaction`, `createReplacement`, YNAB lässt `subtransactions` einer Split-Buchung nicht ändern). Welcher Weg gilt, entscheidet die Baseline des Fehlschlags, also der Stand, den YNAB vermutlich hält.
- **Abgelöste YNAB-Id** (ADR 0058, Punkt 5): nach einem Ersatz trägt die Buchung eine neue YNAB-Id; die alte bleibt als Verknüpfung `ynabsuperseded` (`LinkSystem.YnabSupersededSystem`) erhalten. Der Spiegel findet die Buchung damit weiter über die alte Id, aber deren Lösch-Beobachtung setzt `DeletedInYnab` nicht (`isSupersededDeletion`); `Ledger.CommandHandlers.currentYnabTransactionId` wählt die aktuelle Id. Dasselbe gilt für die gelöschte Id eines offenen Fehlschlags "Neuanlage offen" (`CategorizationWriteBackFailure.YnabDeletedTransactionId`).
- **Ersatz-import_id** (ADR 0058, Punkt 2): eine gelöschte Buchung sperrt ihre `import_id` in YNAB weiter (409), die Ersatzbuchung bekommt deshalb die aktuelle `import_id` plus `:r<n>` (`replacementImportId`, n zählt die bisherigen Ersätze plus 1; die aktuelle ist die mit der höchsten Zahl, `currentImportId`). Ohne `import_id` keine neue; länger als 36 Zeichen wird abgelehnt. Eine aufgeteilte Buchung mit einer `import_id`, die nicht mit `BB:` beginnt, lässt sich nicht ändern oder aufheben, nur aufteilen.
- **Kategorien warten auf Settings**: Zustand im Kassenbuch-Modell (`CategoriesAwaitSettings`): `LoadCategories` kam, bevor die Settings `Success` waren. `Categories` steht auf `Loading`, `SettingsLoaded (Ok)` mit Budget startet den Abruf, `SettingsLoaded (Error)` macht `Categories` zu `Failure`.
- **Ladezustand des Pickers** (`PickerLoadState`: bereit, lädt, fehlgeschlagen): Zustand der Kategorien aus Sicht des Kategorie-Pickers (ledger-dmw83). Ohne Liste ersetzt "Kategorien werden geladen" (mit Spinner) die Zeilen, auch "Ohne Kategorie"; nach einem Fehler steht "Kategorien konnten nicht geladen werden" mit "Erneut versuchen" (löst `LoadCategories` aus). Mit vorhandener Liste (Nachladen) bleibt der Picker unverändert. Das Kassenbuch leitet ihn aus `Ledger.Categories` ab (`categoryLoadState`).
- **Listen-Bedienung (`ListInteraction`)**: wie die Liste bedient wird (ADR 0053 Punkt 6): `TableRows` (Tabelle ab `md`), `CompactMouse` (Kompaktliste mit feinem Zeiger ab `md`: Kästchen, Umschalt-Klick, Hover-Schaltfläche, sichtbare Markierung, alle Tasten, keine Gesten) und `CompactTouch` (Kompaktliste mit Finger oder unter `md`: langes Drücken, Wisch). `listInteractionFor` entscheidet aus Layout, `pointer: fine` und Fensterbreite; die Ansicht meldet das Ergebnis mit `ListInteractionChanged` ins Modellfeld, das der Fußzeilen-Hinweis liest (nur bei `TableRows` und `CompactMouse`).
- **Kategorie setzen für die Auswahl** (ledger-w29y6): zweite Sammelaktion der Auswahl-Leiste neben "Geprüft" (sekundär, Teal-Rand). Sie öffnet denselben Kategorie-Picker wie das Detail, aber ohne markierte Zeile (`CategoryPicker.selectionPopover` nach oben über der Leiste am PC, `selectionSheet` mit der Unterzeile "n Buchungen · Summe" am Handy). Während einer der beiden Sammelaufrufe läuft, sind beide Aktionen gesperrt. Der Toast nennt Zahl der geänderten und übersprungenen Buchungen ("n Buchungen nach X verschoben, m übersprungen (Split, Übertrag, gelöscht)") und trägt ein "Rückgängig" für alle. Nach dem Befehl laden die Kategorien unbedingt neu (Available wird nicht fortgeschrieben).
- **Split-Editor (Kassenbuch)** (ledger-pafkp, ADR 0058, ADR 0006): das Formular im Buchungs-Detail zum Aufteilen, Ändern und Aufheben einer Aufteilung. Das Feld Split im Detail trägt einen festen Knopf, "Aufteilen" bei "Kein Split", "Ändern" bei einer aufgeteilten Buchung; der Pfeil an der Kategorie einer Split-Buchung öffnet denselben Editor (`OpenSplitEditor`). Der Entwurf (`Components.Ledger.SplitEditor.SplitEditorState`, `Model.SplitEditor`) hält je Zeile Ziel (Kategorie, "Ohne Kategorie" als `Uncategorized` oder Übertrag auf ein offenes Budget-Konto), Betrag als positiven Text und Memo. Der Rest ist Gesamtbetrag minus Summe (`Shared.Domain.splitRemainder`), Speichern gilt nur bei Rest 0,00 € (`mkSplits`), genau eine Zeile mit vollem Betrag hebt die Aufteilung auf, eine leere Liste und eine einzelne Übertrags-Zeile gehen nie hin. Die Zeilen gehen mit dem Vorzeichen der Buchung an `setEntrySplits`; die Zeile bleibt in der Liste an ihrem Platz und ungeprüft. "Rückgängig" im Toast (`UndoSplit`) schickt über denselben Weg die Zeilen vor dem Speichern (war die Buchung unaufgeteilt: eine Zeile der alten Kategorie über den vollen Betrag) und fehlt, wenn der Rückweg wegen einer fremden `import_id` gesperrt wäre. Die Kontenliste der Übertrag-Ziele kommt von `getBudgetAccounts` (offene Budget-Konten, ohne das eigene Konto).
- **Split-Sperren (Lesepfad)** (ledger-pafkp, ADR 0058 Punkt 2, 6, 7; ADR 0060): `LedgerEntryView.SplitLocks: SplitLocks option` (`CannotSplitReason`, `CannotChangeReason`, `ReplacementPending`); `None` heißt nichts gesperrt und nichts offen, auch für Antworten eines älteren Servers. Abgeleitet von `Ledger.CommandHandlers.splitLocksOf` nach denselben Regeln wie der Befehl: in YNAB gelöscht, abgeglichen, Übertrag zwischen Budget-Konten und Tracking-Konto sperren Aufteilen und Ändern, eine `import_id` ohne Präfix `BB:` sperrt nur Ändern und Aufheben. Das Tracking-Konto liefert eine gemerkte YNAB-Kontenliste statt der Projektion (fünf Minuten, `Server.Api.budgetAccounts`); ohne Liste bleibt es bei den lokalen Sperren, der Befehl prüft beim Speichern live. `ReplacementPending` heißt "in YNAB gelöscht, Neuanlage offen": die Hinweisleiste im Detail trägt dann den Titel "In YNAB gelöscht, Neuanlage offen" mit "Erneut senden" (`retryCategorizationWriteBack`). Nach einem gescheiterten Versand holt der Client die Buchung mit `getLedgerEntry` neu.
- **Prüf-Zustand der Liste**: die Liste bei aktivem Chip Zu prüfen oder Ohne Kategorie. Die Summenzeile steht auch dort (Prädikat `hasSummaryFilter`, `hasAnyFilter` bleibt unverändert), am PC mit dem Filter im Klartext (`filterPlainText`: Konto, Chips, Kategorien, Zeitraum, Payee, Memo, Betrag). Ist die Liste leer und nur ein Chip aktiv, steht dort Alles geprüft bzw. Alles kategorisiert statt Jetzt spiegeln. Der Kategorie-Filter zeigt Zuletzt verwendet (die gemeinsame Liste, siehe Zuletzt verwendete Kategorien) und sperrt Übernehmen, solange der Entwurf dem aktiven Filter gleicht.
- **Konto-Wechsler** (ledger-ybmqc): Am Handy steht im Kopf des Kassenbuchs der Kontoname (oder "Alle Konten") als Titel mit Chevron. Ein Tipp öffnet ein BottomSheet "Konto wechseln" mit "Alle Konten" zuoberst und jedem Konto mit Stand, das aktive ist markiert; die Wahl wechselt die Sicht, schließt das Sheet und leert die Auswahl. Die Kartenleiste bleibt am Handy stehen. Die zuletzt gewählte Sicht merkt sich das Gerät in `localStorage`; ein gemerktes Konto, das es nicht mehr gibt, fällt auf das YNAB-Standardkonto zurück.
- **Lena fragen** (ledger-xfjcp): die Aktion im Buchungs-Detail (`setLenaFlag`, ADR 0056). Sie setzt das YNAB-Flag, das Roman "Lena" genannt hat (Farbe aus der Einstellung `lena_flag_color`, Name "Lena"), oder nimmt die Rückfrage zurück ("Rückfrage zurücknehmen", nur bei *Rückfrage Lena*, ADR 0061). Ein Ereignis `EntryFlagChangedInBb` mit Alt und Neu, danach der Push mit nur `flag_color`. "Rückgängig" (`restoreFlag`) stellt Farbe und Name des Vorzustands her. Ohne gesetzte Farbe gesperrt, bei in YNAB gelöschten Buchungen aus.
- **Flag nicht in YNAB** (ledger-xfjcp): der offene Fehlschlag beim Zurückschreiben eines in BB gesetzten Flags (`Entry.FlagWriteBackFailure`: Grund und Baseline-Farbe und -Name, ADR 0056). Das Flag bleibt in BB gesetzt, das Detail zeigt die Hinweisleiste mit Grund und "Erneut senden" (`retryFlagWriteBack`), getrennt von "Prüfvermerk nicht in YNAB" und "Kategorie nicht in YNAB". Endet durch Erfolg, durch einen Spiegel-Lauf, der YNAB auf dem BB-Stand sieht, oder durch eine beobachtete Änderung. Ein beobachteter Stand gleich der Baseline lässt ihn offen.
- **Rückfrage Lena (Chip)** (ledger-xfjcp): der Chip "Rückfrage Lena (n)" in der Filterleiste (`Filters.OnlyAskLena`, `OnlyAskLena` in der Seitenanfrage, `AskLenaCount` in der Seite). Er zeigt nur Buchungen mit Flag-Name "Lena" (getrimmt, ohne Groß-/Kleinschreibung), Und-Verknüpfung mit den übrigen Filtern, n unabhängig vom Chip. Eine bei aktivem Chip zurückgenommene Buchung bleibt ausgegraut stehen (`filteredOutNote`), bis die Liste neu lädt.
- **Erneut senden (Noch nicht in YNAB)** (ledger-cks06): Aktion der orangen Hinweisleiste "Noch nicht in YNAB" im Detail. `retryPushToYnab` pusht genau diese eine nicht verknüpfte Buchung sofort über den Einzel-Push der Neuanlage (mit der `import_id`, falls die Buchung eine trägt). Erfolg schreibt `EntryLinkedToYnab` und stößt den Spiegel an, Fehlschlag schreibt `EntryPushToYnabFailed` mit neuem Grund und Zeitpunkt, beides mit Actor `UserInBb`. Eine verknüpfte oder in YNAB gelöschte Buchung wird abgelehnt, ohne Push. Der Spiegel-Lauf pusht nichts, er liest nur aus YNAB. Die Liste kennzeichnet die Buchung mit "Nicht in YNAB" (Handy-Zeile und PC-Statusspalte, Rangfolge gelöscht vor nicht in YNAB vor den übrigen Zuständen).
- **Lena fragen für die Auswahl** (ledger-y54gr): die dritte und vierte Sammelaktion der Auswahl-Leiste, "Lena fragen (n)" und "Rückfrage zurücknehmen (n)" (beide sekundär, Handy eigene Reihe 3, PC rechts vor "Kategorie setzen"). Je Buchung gilt die Regel des Details (`setLenaFlag`): schon *Rückfrage Lena* ist bei "Lena fragen" unverändert, "Rückfrage zurücknehmen" trifft nur *Rückfrage Lena* und lässt ein anderes Flag stehen. Je geänderter Buchung ein `EntryFlagChangedInBb`, danach ein Sammel-PATCH mit nur `id` und `flag_color` (`setLenaFlagBatch`). Der Fehlschlag steht je Buchung (`Entry.FlagWriteBackFailure`, nicht verknüpfte Buchungen und nicht gespeicherte Ids eingeschlossen), ein Netzfehler trifft den ganzen Stapel. Ein "Rückgängig" im Toast stellt Farbe und Name jeder Buchung her (`restoreFlagBatch`). Ohne gesetzte Flag-Farbe ist "Lena fragen" gesperrt und nennt den Grund (Tooltip, am Handy als Zeile). Bei aktivem Chip "Rückfrage Lena" bleiben zurückgenommene Zeilen ausgegraut stehen.
- **Memo bearbeiten** (ledger-fa1fp): die Aktion im Buchungs-Detail (`setEntryMemo`, ADR 0062). Setzt, ändert oder leert das Memo: getrimmt, leer heißt kein Memo, höchstens 500 Zeichen (`Shared.Domain.normalizeMemo`). Ein Ereignis `EntryMemoChangedInBb` mit Alt und Neu, danach der Push mit nur `memo`. "Rückgängig" schickt über denselben Befehl das alte Memo. Bei in YNAB gelöschten Buchungen aus.
- **Memo nicht in YNAB** (ledger-fa1fp): der offene Fehlschlag beim Zurückschreiben eines in BB gesetzten Memos (`Entry.MemoWriteBackFailure`: Grund und Baseline-Memo, ADR 0062). Das Memo bleibt in BB, das Detail zeigt die Hinweisleiste mit Grund und "Erneut senden" (`retryMemoWriteBack`). Endet durch Erfolg, durch einen Spiegel-Lauf, der YNAB auf dem BB-Stand sieht, oder durch eine beobachtete Änderung. Ein beobachteter Stand gleich der Baseline lässt ihn offen.
- **Zuletzt verwendete Kategorien** (ledger-d4msf): eine gemeinsame Liste (`Model.RecentCategories`, höchstens 5, neueste zuerst, je Gerät in localStorage) für den Kategorie-Filter und den Kategorie-Picker. Jede vergebene Kategorie rückt nach vorn: im Filter bei Übernehmen, im Picker bei der Einzelwahl (`StartSetEntryCategory`) und bei Kategorie setzen für die Auswahl (`StartCategoryBatch`), je über `rememberPickedCategory`. Der Picker zeigt sie als Abschnitt Zuletzt verwendet (`CategoryPickerProps.Recent`, `recentCategories`) unter Vorgeschlagen; eine Kategorie darf in beiden stehen, die aktuelle und versteckte fehlen, ohne Einträge und während einer Suche fehlt der Abschnitt. Der Split-Editor führt den Abschnitt nicht und zählt nicht mit.
- **Barabhebung (Split-Editor)** (ledger-s8t3s): Knopf neben "Zeile hinzufügen" im Split-Editor des Kassenbuchs (`SplitEditor.startCashback`, `Msg.StartSplitCashback`). Er setzt die erste Zeile ohne Ziel auf einen Übertrag aufs Quick-Add-Konto (ADR 0004, `Model.QuickAddAccountId`, geladen beim Öffnen des Editors aus den Einstellungen; Ziel über `SplitEditor.cashbackTarget` aus den wählbaren Übertrag-Zielen) oder hängt eine solche Zeile an. Zeilen, Ziele und Beträge bleiben, die Beträge bleiben leer und der Ausgleich geht über "Rest". Ohne Quick-Add-Konto bleibt das Ziel leer und die Konto-Auswahl der Zeile geht auf. Gespeichert wird über `setEntrySplits`, ohne neuen Befehl.
- **Externer Link (ExternalLink)** (ledger-rjhwp): Beschriftung und Adresse zu einer Buchung, im Lesepfad aus Payee und aktuellem Memo erkannt (`Server.RulesEngine.detectSpecialLinksFromText`, dieselbe Regel wie im Sync) und als `LedgerEntryView.ExternalLinks` ausgeliefert. Reines Read-Model-Feld ohne Ereignis und Schema; nur das Buchungs-Detail zeigt es. Ein in BB bearbeitetes Memo ist die Grundlage der Erkennung.
- **Bot-Schnittstelle** (ledger-b0b7q): Eigene REST-Routen unter `/bot/` (`Server.BotApi`), vor dem SPA-Fallback und getrennt von Fable.Remoting (ADR 0066). `POST /bot/transactions/preview`, `POST /bot/transactions/{preview_id}/commit`, `GET /bot/categories`. Jede Route verlangt ein Bearer-Token aus `BOT_API_TOKEN`; ohne gesetztes Token sind die Routen abgeschaltet (401). Unbekannte Pfade unter `/bot/` antworten 404 als JSON. Es gibt keine Lese-Route für Buchungen oder Salden.
- **Bot-Vorschau (Preview)** (ledger-b0b7q): Eine geprüfte und aufgelöste, noch nicht gebuchte Bob-Buchung mit `preview_id` und Ablaufzeit (15 Minuten), im Arbeitsspeicher (`BotPreviewStore`) und nach einem Neustart verfallen. Schreibt nichts ins Kassenbuch und nichts nach YNAB. Die Eingabe hat kein Konto-Feld (Konto ist immer das Quick-Add-Konto, ADR 0004), die Richtung kommt allein aus `direction`, Betrag in Euro positiv, Obergrenze `BOT_MAX_AMOUNT_EUR` (Vorgabe 500 EUR, harter Deckel 500 EUR im Code, nur Dezimalpunkt). Die Kategorie löst `Validation.resolveBotCategory` genau auf (Id oder Name ohne Groß-/Kleinschreibung), bei keinem oder mehreren Treffern 422 mit Kandidaten; interne und versteckte Kategorien sind nicht wählbar.
- **Bot-Commit** (ledger-b0b7q): Bucht genau eine Bot-Vorschau über `Ledger.CommandHandlers.addCashExpense` mit Actor `Bob` (derselbe Weg wie Quick Add, dann Push, `EntryPushToYnabFailed` bei Fehlschlag). Idempotent über die `preview_id`: auch gleichzeitige Aufrufe buchen einmal und teilen sich das Ergebnis, auch nach Ablauf der Gültigkeit. Unbekannte Vorschau 404, abgelaufene 410. Die Antwort nennt `pushed_to_ynab` und `push_error`; bei gescheitertem Push fehlt `entry_id`.
- **Harter Deckel der Bot-Obergrenze** (ledger-ybm5x): Im Code gilt höchstens 500 EUR je Bot-Buchung (`hardBotMaxAmountEur`). `BOT_MAX_AMOUNT_EUR` darf nur absenken und wird nur mit Dezimalpunkt gelesen ("500,00" und "1,000" sind ungültig und ergeben die Vorgabe 500 EUR). Der wirksame Wert steht beim Start im Log, bei Deckelung oder ungültigem Rohwert mit diesem Rohwert (ADR 0068 Punkt 1).

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
- *EntryReviewMarkChangedInBb* / *EntryReviewMarkWriteBackFailed* / *EntryReviewMarkWrittenBackToYnab*: Prüfvermerk in BB gesetzt, entfernt oder wiederhergestellt (ein Typ mit Alt und Neu für `approved` und `cleared`, Actor `UserInBb`, Version 1), Zurückschreiben nach YNAB gescheitert (Payload `Reason`), offener Fehlschlag beendet (ohne Payload, Actor `UserInBb` oder `YnabMirror`). ADR 0050.
- *EntryPushToYnabFailed*: Push einer Buchung nach YNAB gescheitert (Payload `Reason`, Schema-Version 1, Actor `ComdirectImport`, `QuickAdd`, `Bob` bzw. `UserInBb` (Erneut senden, ledger-cks06); der Zeitpunkt ist der Ereignis-Zeitstempel). Setzt `Entry.PushToYnabFailure`, aufgelöst durch `EntryLinkedToYnab` (ledger-rynk5).
- *EntryCategorizationWriteBackFailed* / *EntryCategorizationWrittenBackToYnab*: das Zurückschreiben der Kategorie nach YNAB ist gescheitert (Grund, Baseline-Kategorie, Baseline-Splits) oder angekommen (Actor `UserInBb` nach Erfolg oder erneutem Senden, Actor `YnabMirror`, wenn der Spiegel den BB-Stand sieht). Beide Version 1, ADR 0055. Die Änderung selbst ist `EntryRecategorized` (Actor `UserInBb`, `OldPayee = NewPayee`, Splits leer). Historie: "Kategorie nicht in YNAB: <Grund>" und "Kategorie in YNAB angekommen".
- *EntryReplacedInYnab*: eine Split-Buchung wurde in YNAB gelöscht und neu angelegt (neue YNAB-Id, neue `import_id`, ADR 0058, ADR 0059). Version 1, Actor `UserInBb`. Die alte YNAB-Id gilt als abgelöst, ihre Lösch-Beobachtung setzt `DeletedInYnab` nicht. Historie: "In YNAB neu angelegt (Split-Buchung ersetzt)".
- *EntryFlagChangedInBb* / *EntryFlagWriteBackFailed* / *EntryFlagWrittenBackToYnab*: Flag in BB gesetzt, zurückgenommen oder wiederhergestellt (ein Typ mit Alt und Neu), das Zurückschreiben nach YNAB gescheitert (Grund, Baseline-Farbe, Baseline-Name) oder angekommen (ADR 0056, Schema-Version 1).
- *EntryMemoChangedInBb* / *EntryMemoWriteBackFailed* / *EntryMemoWrittenBackToYnab*: Memo in BB gesetzt, geändert, geleert oder wiederhergestellt (ein Typ mit Alt und Neu), das Zurückschreiben nach YNAB gescheitert (Grund, Baseline-Memo) oder angekommen (ADR 0062, Schema-Version 1).

## Key commands
- *SpiegelÜbernehmen* — Ergebnis eines Spiegel-Laufs aus `ynab-sync` einarbeiten.
- *BuchungÜbernehmen* — aus der Sync-Pipeline (Comdirect + Kategorie) oder Quick Add.
  Quick Add: `Ledger.CommandHandlers.addCashExpense` (Actor als Parameter: `QuickAdd` für die
  eigene Seite, `Bob` für die Bot-Schnittstelle, ledger-b0b7q) schreibt zuerst `CashExpenseAdded`
  (mit diesem Actor), ruft danach den Push auf (`YnabClient.createManualTransaction`,
  liefert die YNAB-Transaction-Id aus der Antwort des Einzel-Posts) und verknüpft bei
  Erfolg mit `EntryLinkedToYnab` (ADR 0021, ADR 0015). Scheitert der Push, bleibt die Buchung ohne Verknüpfung stehen, kein Rollback;
  der Fehlschlag steht danach als `EntryPushToYnabFailed` (Grund, Actor `QuickAdd` bzw. `Bob`) im
  Log und in `Entry.PushToYnabFailure`. Ohne ein gespiegeltes Kassenbuch-Konto
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
  Fehlermeldung ab (Entscheidung konsistent mit Quick Add). Push-Fehler, YNAB-Duplikat-Ablehnung oder eine nicht auswertbare Antwort lassen
  die Kassenbuch-Buchung unverknüpft stehen, kein Rollback; jede geschriebene oder
  wiedergefundene, nicht verknüpfte Buchung des Versuchs bekommt ein
  `EntryPushToYnabFailed` (Grund, Actor `ComdirectImport`), `AlreadyImported` keins. Scheitert das Kassenbuch-Schreiben selbst (z. B.
  eine Invariante von `apply`), bleibt das nicht still: `importToYnab` schließt die Session
  nicht ab und meldet den Fehler (`Ledger.CommandHandlers.summarizeLedgerWriteFailures`).
- *BuchungÄndern* (v2): Kategorie / Split in BB ändern, Push folgt. Das Memo ist gebaut (`setEntryMemo`, ADR 0062).
- *ÜbertragZusammenführen* (v2).
- *KontoBewerten* — Kurs holen, Bewertungs-Buchung schreiben.
- *StückzahlPflegen* — Stückzahl eines Wertpapierkontos setzen.
- *MirrorYnabBudget*: ein Spiegel-Lauf über das ganze Budget. Drei Auslöser (manuell,
  Frische-Kriterium, Lauf nach Push), ein Einzel-Flug-Schutz gegen doppelte Läufe (ADR
  0014, `ledger-h4p6c`, siehe **Spiegel-Lauf** oben).
- *BuchungÜbernehmen (Force-Import)* — der "trotzdem importieren"-Knopf für von YNAB als Duplikat abgelehnte Buchungen (`Server.Api.forceImportDuplicates`, ledger-fq3kd) läuft über denselben Kassenbuch-zuerst-Push-Verknüpfung-Pfad wie der normale Import-Push (`Ledger.CommandHandlers.pushComdirectImports`), sendet aber mit einer neuen ImportId (`prepareTransactions ... forceNewImportId = true`, nur für YNABs Dedup). Damit die schon vom normalen Push geschriebene Kassenbuch-Buchung wiederverwendet statt ein zweites Mal angelegt wird, trägt jeder `ComdirectImportCandidate` jetzt optional `ExistingEntryImportId`: gesetzt auf die alte, deterministische ImportId (`Shared.Domain.generateImportId`), sucht `pushComdirectImports` die bestehende Buchung darüber (statt über die ImportId im vorbereiteten Request), sendet aber mit Letzterer. `None` (Normalfall): Suche und Versand teilen sich dieselbe ImportId, wie bisher. Existiert (Randfall) noch keine Buchung für die alte ImportId, entsteht genau eine, und zwar unter der alten, gesuchten ImportId (nicht der neuen Force-ImportId), damit ein zweiter Force-Import-Versuch sie wiederfindet, statt erneut eine zweite Buchung anzulegen.
- *PrüfvermerkSetzen*: `Ledger.CommandHandlers.setReviewMark` (Geprüft/Ungeprüft), `restoreReviewMark` (Rückgängig mit dem Vorzustand) und `retryReviewMarkWriteBack` (ADR 0050). Kassenbuch zuerst (`EntryReviewMarkChangedInBb`), dann der Push (`YnabClient.updateReviewMark`: `PUT .../transactions/{id}` mit nur `approved` und gegebenenfalls `cleared`, nie `reconciled`); Reconciled bleibt unberührt, Ziel gleich Ist erzeugt kein Ereignis und keinen Push (`NoChange`), in YNAB gelöschte Buchungen werden abgelehnt. Ein gescheiterter Push schreibt `EntryReviewMarkWriteBackFailed`, kein Rollback. Die Antwort `ReviewMarkOutcome` trägt den Vorzustand für Rückgängig.
- *PrüfvermerkFürAuswahlSetzen*: `Ledger.CommandHandlers.setReviewMarkBatch` (Geprüft für eine ganze Auswahl) und `restoreReviewMarkBatch` (ein Rückgängig für den Stapel), API `setReviewMarkBatch`/`restoreReviewMarkBatch` (ledger-nhwrt, ADR 0050). Der Server löst die Auswahl selbst auf (`ReviewMarkBatchSelection`: `BatchPicked` mit Ids, oder `BatchAllExcept` mit Treffer-Anfrage und Ausnahmen, auch nie geladene Buchungen). Je geänderter Buchung ein eigenes `EntryReviewMarkChangedInBb`, danach EIN Sammel-PATCH (`YnabClient.updateReviewMarkBatch`: `PATCH .../transactions` mit `{"transactions":[{id, approved, cleared?}]}`, `cleared` nie `reconciled`; die OpenAPI nennt keine Obergrenze je Anfrage). Ziel gleich Ist ist `NoChange` (kein Ereignis, nicht im PATCH), in YNAB gelöschte oder nicht verknüpfte Buchungen scheitern ohne Ereignis (`FailedCount`). Scheitert der PATCH, trägt jede Buchung darin `EntryReviewMarkWriteBackFailed` mit demselben Grund; nennt die Antwort `transaction_ids`, gelten nur die dort fehlenden als gescheitert. Ohne geänderte Buchung geht kein PATCH hinaus. Die Antwort `ReviewMarkBatchOutcome` trägt je geänderter Buchung den Vorzustand für das gemeinsame Rückgängig.
- *KategorieÄndern*: `Ledger.CommandHandlers.setEntryCategory` (eine Buchung, `YnabCategoryId option`, `None` heißt ohne Kategorie) und `retryCategorizationWriteBack` (ADR 0055, `ledger-k2rbn`). Reihenfolge wie beim Prüfvermerk: erst `EntryRecategorized` per `appendAndApply`, dann der Push (`YnabClient.updateCategory`, PUT mit nur `category_id`). Abgelehnt werden (Fehler, kein Ereignis): unbekannte oder in YNAB gelöschte Buchung, Übertrag ohne Kategorie und ohne Split-Zeilen, Split-Buchung, unbekannte Kategorie-Id, eine Buchung, deren offener Fehlschlag noch einen Split als Baseline trägt (ADR 0059 Punkt 3). Ziel gleich Ist (`categorizationIdentity`): kein Ereignis, kein Push, `CategoryUnchanged`. Das Rückgängig im Toast ist derselbe Befehl mit `Previous` aus der Antwort. Läuft unter der Schreib-Sperre.
- *SplitSetzen*: `Ledger.CommandHandlers.setEntrySplits` (eine Buchung, `TransactionSplit list`) und `retryCategorizationOrSplitWriteBack` ("Erneut senden" für Kategorie und Split, die API-Methode `retryCategorizationWriteBack` ruft es, ADR 0058, `ledger-kxme4`). Reihenfolge wie bei `KategorieÄndern`: erst `EntryRecategorized` (mit `NewSplits`, beim Aufheben mit `NewCategory`), dann der Push; Fehlschlag mit Baseline wie ADR 0055. Abgelehnt werden (Fehler, kein Ereignis, kein schreibender YNAB-Aufruf): leere Liste, einzelne Übertrags-Zeile, Summe ungleich Betrag, unbekannte Kategorie, in YNAB gelöscht, nicht mit YNAB verknüpft, Übertrag zwischen Budget-Konten, Tracking-Konto (Prüfung über `GET .../accounts`), `Reconciled`, Ändern oder Aufheben mit fremder `import_id`. Ziel gleich Ist: `CategoryUnchanged`. Läuft unter der Schreib-Sperre.
- *KategorieFürAuswahlSetzen*: `Ledger.CommandHandlers.setEntryCategoryBatch` (Kategorie oder ohne Kategorie für eine ganze Auswahl, ledger-w29y6, ADR 0050/0055), API `setEntryCategoryBatch`. Die Auswahl löst `resolveReviewMarkBatchSelection` auf (`BatchPicked` oder `BatchAllExcept`, auch nie geladene Buchungen). Eine unbekannte Ziel-Kategorie lehnt den ganzen Befehl ab (Fehler, kein Ereignis). Je geänderter Buchung ein eigenes `EntryRecategorized` (Actor `UserInBb`), danach EIN Sammel-PATCH (`YnabClient.updateCategoryBatch`: `PATCH .../transactions` mit `{"transactions":[{id, category_id}]}`, `null` für ohne Kategorie). Übersprungen werden ohne Ereignis und ohne PATCH die Fälle von `categoryNotChangeable` (in YNAB gelöscht, mit Split-Zeilen, Split in YNAB noch nicht angepasst, Übertrag ohne Kategorie, `CategorySkipReason`), nicht mehr vorhandene Buchungen und Ziel gleich Ist (`UnchangedCount`). Eine nicht verknüpfte Buchung bekommt Ereignis plus `EntryCategorizationWriteBackFailed` (nicht im PATCH). Scheitert der PATCH, trägt jede Buchung darin einen Fehlschlag mit Baseline aus `categoryBaseline` (bei offenem Fehlschlag bleibt die erste); von YNAB nicht genannte Ids werden einzeln Fehlschlag (`categoryNotSavedReason`). Erfolg bei offenem früheren Fehlschlag schreibt `EntryCategorizationWrittenBackToYnab`. Läuft unter der Schreib-Sperre. Die Antwort `CategoryBatchOutcome` nennt je geänderter Buchung die vorherige Kategorie; das Rückgängig im Toast ruft denselben Befehl je Gruppe gleicher vorheriger Kategorie auf.
- *LenaFragen*: `Ledger.CommandHandlers.setLenaFlag` (`AssignLena` oder `WithdrawLena`, Farbe aus `lena_flag_color`), `restoreFlag` (Vorzustand) und `retryFlagWriteBack`, API `setLenaFlag`, `restoreFlag`, `retryFlagWriteBack` (ADR 0056, `ledger-xfjcp`). Ereignis zuerst, dann `Ynab.updateFlag` (PUT mit nur `flag_color`), unter der Schreib-Sperre. Die Einstellung liegt in `SettingsApi.getLenaFlagSettings` und `saveLenaFlagColor` (Vorschlag: Farbe der jüngsten gespiegelten Buchung mit Flag-Name "Lena").
- *PushErneutSenden*: `Ledger.CommandHandlers.retryPushToYnab` (ledger-cks06), API `LedgerApi.retryPushToYnab: Guid -> Async<Result<PushToYnabWriteBack, string>>`. "Erneut senden" am Hinweis "Noch nicht in YNAB": lädt die Buchung und pusht sie über den injizierten Einzel-Push (`EntryPush`, in `Server.Api` über `YnabClient.createTransactionWith`; trägt die Buchung eine `import_id`, geht sie mit, sonst keine). Abgelehnt ohne Push (`Error`) wird eine unbekannte, in YNAB gelöschte oder schon verknüpfte Buchung. Erfolg schreibt `EntryLinkedToYnab` (Actor `UserInBb`), stößt den Spiegel an und liefert `PushToYnabSent`. Ein Fehlschlag schreibt `EntryPushToYnabFailed` (Actor `UserInBb`, neuer Grund und Zeitpunkt ersetzen den alten) und liefert `PushToYnabFailed reason`. Kein Rollback, die Buchung bleibt unverknüpft stehen.
- *LenaFragenFürAuswahl*: `Ledger.CommandHandlers.setLenaFlagBatch` (Auswahl wie `setReviewMarkBatch`, Ziel `AssignLena` oder `WithdrawLena`, Farbe aus `lena_flag_color`; ohne gültige Farbe wird `AssignLena` für den ganzen Stapel abgelehnt) und `restoreFlagBatch` (Vorzustand je Buchung), API `setLenaFlagBatch`, `restoreFlagBatch` (ledger-y54gr, ADR 0050/0056). Ein `EntryFlagChangedInBb` je geänderter Buchung, dann EIN `Ynab.updateFlagBatch` (PATCH mit `id` und `flag_color`), unter der Schreib-Sperre. Antwort `FlagBatchOutcome` (`Changed` mit Vorzustand und Ergebnis je Buchung, `UnchangedCount`, `FailedCount` für gelöschte oder unbekannte Buchungen).

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

**Actor:** `YnabMirror | ComdirectImport | Rule of RuleId | UserInBb | QuickAdd | Bob |
OrderIdMatch of orderId: string`. `Bob` (ADR 0066, ledger-b0b7q) ist der Urheber der Buchungen
aus der Bot-Schnittstelle `/bot/`, Anzeigename "Bob" (`Shared.Domain.originLabel AddedByBob`),
additiv ohne Schema-Änderung. Der Fall `OrderIdMatch` (ADR 0025) markiert eine Kategorie aus
dem Order-ID-Abgleich beim Abruf (`categorization-gdrx8`, `ReviewActor.ByOrderIdMatch`):
fachlich eine Umkategorisierung (Ereignis `EntryRecategorized`), nur mit einem anderen
Urheber als Roman. Anzeigename "Order-ID-Abgleich" (`Shared.Domain.originLabel
MatchedByOrderId`).

**Beobachtungen** (der Spiegel hat in YNAB etwas gesehen):
- `EntryObservedInYnab`: Erstbeobachtung, voller Snapshot (Konto, YNAB-Id, ImportId, Datum,
  Betrag, Payee, Memo, Kategorie mit Name, Splits, Cleared, Übertrag).
- `EntryDetailsChangeObservedInYnab`: Datum, Betrag, Payee, Memo oder Konto geändert (alt/neu). Bei offenem Memo-Fehlschlag (ADR 0062): zeigt YNAB das BB-Memo, entsteht `EntryMemoWrittenBackToYnab` (Actor `YnabMirror`); zeigt YNAB die Baseline, bleibt das Memo und der Fehlschlag; jeder andere Stand ist eine echte Änderung, die den Fehlschlag in `apply` beendet.
- `EntryCategorizationChangeObservedInYnab`: Kategorie oder Splits geändert (alt/neu). Entsteht nur, wenn sich die Kategorie-Identität ändert (`Ledger.Domain.categorizationIdentity`, ADR 0044): Kopf über die `CategoryId`, `ToCategory`-Ziele ohne Namen. Eine reine Umbenennung in YNAB ist keine Umkategorisierung und erzeugt kein Ereignis; die Payload trägt bei einem Ereignis weiter die vollen Namen. Ohne offenen Kategorie-Fehlschlag gewinnt YNAB jeden abweichenden Stand. Ausnahme bei offenem Fehlschlag (ADR 0055 Punkt 5): zeigt YNAB die Baseline, entsteht kein Ereignis und der Fehlschlag bleibt; zeigt YNAB den BB-Stand, entsteht `EntryCategorizationWrittenBackToYnab` (Actor `YnabMirror`); jeder andere Stand ist eine echte Änderung in YNAB, dann entsteht dieses Ereignis und `apply` beendet `Entry.CategorizationWriteBackFailure`.
- `EntryClearedStateChangeObservedInYnab`: Cleared-Status geändert (alt/neu); eigenes
  Ereignis, damit die Anzeige dieses Rauschen ausblenden kann.
- `EntryDeletionObservedInYnab`: in YNAB gelöscht; die Buchung bleibt mit Flag.
- `EntryApprovalChangeObservedInYnab`: `approved` in YNAB geändert (alt/neu). Eigenes
  Ereignis, wie beim Cleared-Status; in der Gruppen-Reihenfolge nach Cleared (ADR 0032).
- `EntryFlagChangeObservedInYnab`: `flag_color`/`flag_name` in YNAB geändert (alt/neu),
  roh. Die Deutung (*Rückfrage Lena*, *Markiert*, kein Flag) sitzt im Lesepfad, nicht im
  Ereignis (ADR 0032). Letzte Gruppe in der Reihenfolge.
- `EntryTransferCounterpartObservedInYnab`: die Gegenbuchung eines Übertrags ist
  gefunden. YNAB führt beide Seiten über `transfer_transaction_id`, der Spiegel hat
  diese Fremd-Id über `findEntryByLink` (System `ynab`) auf die eigene Buchungs-Id
  aufgelöst (ADR 0015). Payload: Buchungs-Id der Gegenbuchung und die
  YNAB-Transaction-Id, über die aufgelöst wurde (sie macht die Historie-Zeile
  nachprüfbar, ohne die Gegenbuchung zu laden). Setzt `Entry.TransferEntryId`. Anders
  als die Bullets darüber entsteht es nicht aus der Änderungserkennung, sondern in einem
  späteren Schritt des Spiegel-Laufs, nachdem die Beobachtung geschrieben ist; die
  Gruppen-Reihenfolge (ADR 0032) betrifft es deshalb nicht. Die Verknüpfung ist
  wechselseitig, der Umschlag trägt aber genau eine Buchungs-Id: deshalb **zwei**
  Ereignisse, eins je Seite, mit demselben Zeitpunkt und in einem
  `appendAndApply`-Aufruf, also einer Transaktion.
- `EntrySplitTransferCounterpartObservedInYnab`: die Gegenbuchung einer
  Übertrags-Split-Zeile ist gefunden (ADR 0046). Liegt auf der Buchung mit der Split-Zeile.
  Payload: YNABs Subtransaction-Id der Zeile, Buchungs-Id der Gegenbuchung, die
  YNAB-Transaction-Id, über die aufgelöst wurde, und der Betrag der Zeile (für den
  Historien-Satz, weil zwei Übertrags-Zeilen zum selben Konto sich sonst nicht
  unterscheiden). Schema-Version 1, Actor `YnabMirror`. Setzt einen Eintrag in
  `Entry.SplitTransferCounterparts` (letztes Ereignis gewinnt je Schlüssel; eine
  Gegenbuchung gehört zu höchstens einer Zeile derselben Buchung, ein anderer Schlüssel mit
  derselben Gegenbuchung wird entfernt; Selbstverknüpfung wirft). Die Gegenbuchung bekommt
  das bestehende `EntryTransferCounterpartObservedInYnab`. Wie dieses entsteht es nicht aus
  der Änderungserkennung, sondern in einem Nachlauf des Spiegel-Laufs.

Mehrere Gruppen in einer Beobachtung ergeben mehrere Ereignisse (Details, Kategorisierung,
Cleared). Unveränderte Daten ergeben kein Ereignis.

**Entstehung:** Eine Buchung entsteht mit genau einem von `EntryObservedInYnab`,
`EntryImportedFromComdirect`, `CashExpenseAdded`. Ein zweites Entstehungs-Ereignis auf
derselben Buchung ist ein kaputtes Log und wird von `apply` laut abgelehnt.

**Prüfvermerk und Flags in Schema-Version 2** (ADR 0032): `EntryObservedInYnab` trägt seit
Version 2 zusätzlich `approved`, `flagColor`, `flagName`; `EntryImportedFromComdirect` und
`CashExpenseAdded` tragen `approved`, beim Schreiben immer `false` (der Push sendet kein
`approved`, YNAB legt die Buchung unbestätigt an). **Version 1 aller drei bleibt lesbar**
und liefert `approved = true`, kein Flag: kein Nachholen für den Bestand, alte BB-Buchungen
fallen nicht in "Zu prüfen".

**Entscheidungen** (BB hat etwas festgelegt):
- `EntryImportedFromComdirect`: Comdirect-Buchung beim Push angelegt, ohne Kategorie. Die
  Kategorisierung folgt als eigenes Ereignis in derselben Sequenz. Seit ynab-sync-d9e51
  optional `flagColor` und `flagName` (Schema-Version 2, Schlüssel nur bei Rückfrage Lena
  beim Import, Name "Lena"): die Buchung entsteht sofort als *Rückfrage Lena*, ohne
  zusätzliches `EntryFlagChangedInBb`. Ein Ereignis ohne die Felder liest sich als kein Flag
  (ADR 0064). Historie: "Lena gefragt beim Import".
- `EntryCategorizedByRule`: eine Regel hat kategorisiert; die RuleId steht im Actor. Nur
  auf einer noch unkategorisierten Buchung zulässig.
- `EntryRecategorized`: Roman hat in BB kategorisiert oder umkategorisiert (Actor
  `UserInBb`), oder eine Kategorie stammt aus dem Order-ID-Abgleich beim Abruf (Actor
  `OrderIdMatch`, ADR 0025): fachlich dieselbe Kategorisierungs-Änderung, nur mit einem
  anderen Urheber. Historien-Satz bei Aufteilung: "In BB umkategorisiert: Aufgeteilt in <Ziel> <Betrag>, ..."
  (Ziel und Betrag je Zeile, ledger-fa1fp).
- `CashExpenseAdded`: Quick-Add-Buchung angelegt, vor dem Push (Actor `QuickAdd`, bei der Bot-Schnittstelle `Bob`, ADR 0066), darf
  sofort eine Kategorie tragen.
- `EntryPushToYnabFailed`: der Push der Buchung nach YNAB ist gescheitert (ADR 0021 Punkt
  2, ledger-rynk5). Payload: `Reason`, ein kurzer lesbarer Satz mit der Fehlerart (bei
  Limit und Zugang mit HTTP-Status). Schema-Version 1, Actor `ComdirectImport`, `QuickAdd`, `Bob` bzw.
  `UserInBb` (Erneut senden, ledger-cks06). Setzt `Entry.PushToYnabFailure` (Grund und Ereignis-Zeitstempel, ein zweiter
  Fehlschlag ersetzt den ersten); `EntryLinkedToYnab` löst ihn auf, auch mit Actor
  `YnabMirror` oder `UserInBb`. Historien-Satz: "Push nach YNAB gescheitert: <Grund>".
- `EntryCategorizationWriteBackFailed`: das Zurückschreiben der Kategorie nach YNAB ist
  gescheitert oder war nicht möglich (ADR 0055 Punkt 4, ledger-k2rbn). Payload: `Reason`
  (kurzer lesbarer Satz, ohne YNAB-Verknüpfung "noch nicht in YNAB verknüpft"),
  `BaselineCategory` und `BaselineSplits` (der Stand, den YNAB vermutlich noch hat) und
  `YnabDeletedTransactionId` (Option: beim Ersetzen einer Split-Buchung ist das DELETE
  gelungen und das POST gescheitert, die Buchung fehlt in YNAB; bleibt über weitere
  Fehlschläge erhalten, "Erneut senden" ruft dann nur das POST; fehlt das Feld im
  gespeicherten JSON, gilt `None`, ADR 0059).
  Schema-Version 1, Actor `UserInBb`. Setzt `Entry.CategorizationWriteBackFailure`; bei
  einem schon offenen Fehlschlag bleibt die erste Baseline, nur der Grund wird ersetzt.
  Aufgelöst durch `EntryCategorizationWrittenBackToYnab` oder durch eine beobachtete
  Änderung (`EntryCategorizationChangeObservedInYnab`). Historien-Satz: "Kategorie nicht
  in YNAB: <Grund>".
- `EntryCategorizationWrittenBackToYnab`: der offene Kategorie-Fehlschlag ist beendet.
  Kein Payload, Schema-Version 1. Actor `UserInBb` nach einem erfolgreichen Push oder
  "Erneut senden" bei offenem Fehlschlag, Actor `YnabMirror`, wenn der Spiegel in YNAB den
  BB-Stand sieht. Löscht `Entry.CategorizationWriteBackFailure`. Erweitert ADR 0055 Punkt 2
  ("ein erfolgreicher Push erzeugt kein weiteres Ereignis") in dem Sinn, den Punkt 4
  nahelegt: nur bei einem offenen Fehlschlag schreibt ein Erfolg dieses Ereignis, sonst
  nicht. Historien-Satz: "Kategorie in YNAB angekommen".
- `EntryFlagChangedInBb`: das Flag wurde in BB geändert (ADR 0056 Punkt 2). Payload `OldFlagColor`,
  `OldFlagName`, `NewFlagColor`, `NewFlagName` (roh). Ein Typ für "Lena fragen", "Rückfrage
  zurücknehmen" und "Rückgängig". Schema-Version 1, Actor `UserInBb`. Setzt `Entry.FlagColor/FlagName`
  und leert `FlagWriteBackFailure`. Historien-Sätze: "Lena gefragt", "Rückfrage Lena zurückgenommen",
  sonst "Flag in BB geändert".
- `EntryFlagWriteBackFailed`: das Zurückschreiben des Flags nach YNAB ist gescheitert (ADR 0056 Punkt 5
  und Nachtrag). Payload `Reason`, `BaselineFlagColor`, `BaselineFlagName` (die `Old*`-Werte der ersten
  gescheiterten Änderung; bei einem schon offenen Fehlschlag bleibt die erste Baseline). Schema-Version 1,
  Actor `UserInBb`. Setzt `Entry.FlagWriteBackFailure`. Historien-Satz: "Flag nicht in YNAB: <Grund>".
- `EntryFlagWrittenBackToYnab`: der offene Flag-Fehlschlag ist beendet. Kein Payload, Schema-Version 1.
  Actor `UserInBb` nach Erfolg bei offenem Fehlschlag oder "Erneut senden", Actor `YnabMirror`, wenn der
  Spiegel YNAB auf dem BB-Stand sieht. Historien-Satz: "Flag in YNAB angekommen".
- `EntryMemoChangedInBb`: das Memo wurde in BB geändert (ADR 0062). Payload `OldMemo`, `NewMemo`
  (getrimmt, `None` für kein Memo). Ein Typ für Setzen, Ändern, Leeren und "Rückgängig".
  Schema-Version 1, Actor `UserInBb`. Setzt `Entry.Memo` und leert `MemoWriteBackFailure`.
  Historien-Sätze: "Memo gesetzt: <neu>", "Memo geändert: <alt> → <neu>", "Memo entfernt: <alt>".
- `EntryMemoWriteBackFailed`: das Zurückschreiben des Memos nach YNAB ist gescheitert (ADR 0062).
  Payload `Reason`, `BaselineMemo` (der `OldMemo`-Wert der ersten gescheiterten Änderung; bei einem
  schon offenen Fehlschlag bleibt die erste Baseline). Schema-Version 1, Actor `UserInBb`. Setzt
  `Entry.MemoWriteBackFailure`. Historien-Satz: "Memo nicht in YNAB: <Grund>".
- `EntryMemoWrittenBackToYnab`: der offene Memo-Fehlschlag ist beendet. Kein Payload, Schema-Version 1.
  Actor `UserInBb` nach Erfolg bei offenem Fehlschlag oder "Erneut senden", Actor `YnabMirror`, wenn der
  Spiegel YNAB auf dem BB-Stand sieht. Historien-Satz: "Memo in YNAB angekommen".
- `EntryReplacedInYnab`: eine Split-Buchung ist in YNAB gelöscht und unter neuer Id
  angelegt worden (ADR 0058 Punkt 2 und 5, ADR 0059, ledger-kxme4). Payload:
  `OldYnabTransactionId`, `NewYnabTransactionId`, `NewImportId` (Option, die alte
  `import_id` plus `:r<n>`). Schema-Version 1, Actor `UserInBb`. `linksFrom` stiftet die
  neue YNAB-Id, die neue `import_id` und hält die alte YNAB-Id als `ynabsuperseded` fest;
  `apply` leert `Entry.SplitTransferCounterparts` (der Spiegel zieht sie nach, ADR 0046).
  Folgt auf `EntryRecategorized`; war ein Fehlschlag offen, folgt danach
  `EntryCategorizationWrittenBackToYnab`. Historien-Satz: "In YNAB neu angelegt
  (Split-Buchung ersetzt)".
- `ValuationRecorded`: weiterhin reserviert, ohne Payload (Bewertung des
  Wertpapierkontos).

Bei `EntryCategorizedByRule` und `EntryRecategorized` schließen sich Kategorie und Splits
aus, wie bei den Beobachtungs-Ereignissen (ADR 0006/0019). Gesetzte Splits müssen zusätzlich
die `mkSplits`-Invarianten aus ADR 0006 erfüllen (mindestens zwei Zeilen, vorzeichenrichtige
Summe gleich dem Buchungsbetrag, eine Währung); `apply` prüft das über `mkSplits` und lehnt
einen Verstoß laut ab.

**Verknüpfung:** `EntryLinkedToYnab` (Payload: YNAB-Transaction-Id). Die YNAB-Id ist
bekannt geworden, aus der Push-Antwort (Actor `ComdirectImport`, `QuickAdd` bzw. `Bob`, beim erneuten Senden aus dem Detail `UserInBb`) oder vom
Spiegel über die ImportId (Actor `YnabMirror`, ADR 0021). Setzt `Entry.LinkedToYnab` und löst einen offenen `Entry.PushToYnabFailure` auf; die
Verknüpfung selbst liegt in der Fremd-Id-Tabelle (ADR 0015). Ob ein Aufruf auf eine
bereits verknüpfte Buchung ein Fehler oder still idempotent ist, entscheidet der Aufrufer
(`appendAndApply`-Fassade), nicht `apply`.

**Übertrags-Verknüpfung:** Die Buchung-zu-Buchung-Verknüpfung aus ADR 0015 entsteht
nicht beim Entstehungs-Ereignis. `EntryObservedInYnab` trägt nur YNABs Fremd-Ids
(`TransferAccountId`, `TransferYnabTransactionId`), und die Gegenbuchung ist beim ersten
Sehen oft noch nicht angelegt. Aufgelöst wird sie im Spiegel-Lauf, nachdem die
Beobachtung geschrieben ist: findet der Lookup die Gegenseite, schreibt der Lauf
`EntryTransferCounterpartObservedInYnab` für beide Buchungen, sonst nichts. Zeigt eine
Seite bereits auf die andere, entsteht kein weiteres Ereignis (Idempotenz wie bei allen
Beobachtungen: unveränderte Daten ergeben kein Ereignis). `apply` selbst setzt das Feld
ohne Rückfrage auf den Wert der Payload: eine spätere Umverknüpfung ist in YNAB möglich
und darf keinen Lauf abbrechen. Laut abgelehnt wird nur die Verknüpfung einer Buchung
mit sich selbst, die in keinem gültigen Log vorkommt. Der Spiegel-Lauf baut ein solches
Ereignis inzwischen gar nicht erst (ledger-v5hkt): `linkTransferCounterpart` prüft die
aufgelöste Gegenbuchungs-Id vor dem Bau des Ereignisses, überspringt den Selbstbezug wie
den Fall „Gegenbuchung nicht gefunden“ (kein Ereignis, kein abgebrochener Lauf),
schreibt eine Warnzeile über `printfn` und läuft weiter; der `invalidOp` daneben deckt
seitdem nur noch einen Programmierfehler im Erzeuger ab, keinen Datenfehler mehr. Das
Aufheben eines Übertrags in YNAB ist heute nicht modelliert: die Verknüpfung bleibt dann
stehen, bis die zusammengefasste Übertrags-Sicht in v2 kommt.

**Split-Übertrags-Verknüpfung (Zeitpunkt):** Anders als `linkTransferCounterpart` läuft `linkSplitTransferCounterparts` in einem Nachlauf nach der Beobachtungs-Schleife von `runMirror` (vor `saveServerKnowledge`). Nur die Split-Seite trägt die Fremd-Id; läge die Gegenbuchung im selben Batch hinter der Split-Buchung, fände ein Aufruf innerhalb der Schleife sie nicht, und kein künftiges Delta brächte beide erneut vorbei. Kardinalität 0..n je Buchung. Gelöschte Split-Zeilen behalten ihre Verknüpfung (wie der Übertrag auf Buchungsebene). Die Zähler des Laufs bleiben unberührt (ADR 0046).

**Konten** haben in v1 keine Historie (reiner Zustand: Id, Name, Art, Verknüpfung,
geschlossen). **Kontostand** ist eine Query über die nicht gelöschten Buchungen.

**Migration bestehender Zustandstabellen** (ADR 0032, ADR 0038):
`Ledger.Persistence.migrateLedgerEntryColumns` fügt `approved`, `flag_color` und
`flag_name` einer vor diesen Spalten angelegten Tabelle nach; bestehende Zeilen bekommen
`approved = 1` und kein Flag. Ebenso `transfer_account_id` (ADR 0048): greift das
`ALTER TABLE` neu, trägt `backfillTransferAccountId` die Spalte einmalig aus den
`EntryObservedInYnab`-Ereignissen nach (das Log wird nur gelesen). Die Funktion nimmt die Connection als Parameter, damit sie
sowohl aus `initializeLedgerSchema` als auch aus einem Test gegen eine isolierte
Verbindung laufen kann; ein zweiter Aufruf bleibt folgenlos (SQLite kennt kein
`ADD COLUMN IF NOT EXISTS`, die Migration fängt den Duplikat-Fehler ab). Direkt danach
läuft `Ledger.Persistence.rebuildSplitIndexes` (vormals `rebuildSplitCategoryIndex`): sie leert
die abgeleiteten Indextabellen `ledger_entry_split_categories` und `ledger_entry_split_transfers`
und baut sie aus dem Bestand von `ledger_entries` neu auf (Kandidaten über
`WHERE splits <> '[]'`), damit der Kategorie-Filter Split-Zeilen-Kategorien und der Chip
"Ohne Kategorie" Split-Übertrags-Zeilen findet (ADR 0038, ADR 0048). Nimmt ebenfalls die
Connection als Parameter, ist idempotent durch ihre Form und liest wie schreibt das
Ereignis-Log nicht.

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

**Einstieg und Navigation.** Das Kassenbuch öffnet im zuletzt gewählten Konto (je Gerät gemerkt, auch nach Neustart; ein verschwundenes Konto fällt auf das YNAB-Standardkonto zurück). Am Handy wechselt der Titel mit Chevron (Konto-Wechsler) die Sicht, zusätzlich zur Kartenleiste. Konto ist
fachlich ein **Filter**, nicht ein eigener Modus: der Wechsel zwischen Gesamtsicht und
Konto lässt alle anderen Filter stehen, eine laufende Auswahl leert er, weil sie sich auf einen
Treffer bezieht. Oben die Kontostände
(Handy: waagerecht scrollbare Leiste; PC: linke Spalte, "Alle Konten" zuoberst), darunter
die sticky Filterleiste, darunter die Liste. Erster Chip ist immer "Zu prüfen (n)".

**Liste.** Am Handy Datum als Gruppenkopf, je Zeile Payee, Kategorie, Betrag, darunter
klein Memo und nur die abweichenden Kennzeichen. Am PC eine Tabelle mit Sticky-Kopf
(Auswahl, Datum, Konto in der Gesamtsicht, Payee, Kategorie, Memo, Betrag, Status). Bei zu
wenig Breite fällt zuerst Memo weg, dann in der Gesamtsicht Konto (das Kürzel steht dann
klein vor dem Payee), darunter kommt die kompakte Liste. Das entscheidet `visibleColumns`
aus der gemessenen Tabellenbreite (ADR 0052); die Filter-Chrome bleibt bei
`isDesktopViewport()`. In der
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

**Gelöschte Buchungen** sind standardmäßig ausgeblendet und kommen über einen Schalter
zurück, gedämpft und durchgestrichen (`Money.transactionDeleted`) und ohne Wirkung auf
den Kontostand: der bleibt immer die Summe der nicht gelöschten Buchungen
(`ledger-p3n8k`, umgesetzt als eigener Zweig in `getLedgerEntriesPage`, nicht als
Client-Filter auf der geladenen Seite, sonst stimmten Seitengröße und Trefferzahl nicht
mehr). Sind welche ausgeblendet, steht darüber "N gelöschte Buchungen ausgeblendet" samt
Schalter; ohne welche steht dort nichts. Blendet Roman sie ein, zählen sie bewusst in
Trefferzahl und Summe der Liste mit (Variante 2, Entscheidung Roman 2026-09-18,
`ledger-d4kx8`): die Zeile über der Liste weicht dann absichtlich vom Kontostand ab und
sagt das auch, im Stil "13 Buchungen, davon 1 in YNAB gelöscht". Der heutige Schalter ist
eine schlichte Zeile über der Liste, kein Filter-Chip; die Filter-Chip-Leiste selbst
ist weiter offen (`ledger-j8xq4`).

**Große Listen.** Der Bestand liegt bei rund 14.000 Buchungen. Die Liste lädt seitenweise
nach (`getLedgerEntriesPage`, 100 Buchungen je Seite, Beobachter am Listenende statt
Button — ledger-b1xwe); Trefferzahl und Summe rechnet der Server über den gesamten
Treffer, nie über den geladenen Ausschnitt. Kontostände kommen aus dem Zustand
(`getLedgerAccountBalances`), nie aus der Liste. Konto ist ein Filter
(`AllAccounts | OneAccount`), "Alle Konten" ist ein eigener, gleichberechtigter Wert,
kein Schalter.

**Was das Konzept voraussetzt und heute nicht gibt** (Scheiben-Material für
`ledger-s8m3w`):
- **Prüfvermerk und Flags im Spiegel** *gelöst (ADR 0032, `ledger-kdnnz`):* Der
  Ereignis-Katalog oben kennt `approved` und Flags; `EntryApprovalChangeObservedInYnab`
  und `EntryFlagChangeObservedInYnab` beobachten Änderungen, die drei
  Entstehungs-Ereignisse tragen den Prüfstand in Schema-Version 2. Die lesende Anzeige
  steht: Chip "Zu prüfen (n)", Punkt am linken Zeilenrand, Kennzeichen *Rückfrage Lena*
  und *Markiert*. Das Setzen aus BB heraus bleibt `ledger-r777c`.
- **Available-Betrag je Kategorie.** Der Picker soll je Kategorie den verfügbaren Betrag
  zeigen und beim Wechsel beide Seiten (alt hoch, neu runter), wie die YNAB-App. Der
  Spiegel führt heute keine Budget-Zahlen mit.
- **Filter-Chips und Summenzeile über dem Treffer** *gelöst (`ledger-j8xq4`):* Sticky
  Chip-Leiste (`Client.DesignSystem.ChipBar`, `.ledger-filter-bar` in `styles.css`, auf
  beiden Geräten unter der fixen Navbar) über den fünf Such-Filtern plus dem
  bestehenden Chip "Zu prüfen"; am Handy wandern aktive Chips nach vorn
  (`Components.Ledger.Types.orderChipsForDisplay`), am PC bleibt die Reihenfolge fest.
  Kategorie ist Mehrfachauswahl mit Click-Commit (Styleguide 7.1, ADR 0005): ein
  Entwurf (`CategoryDraft`) wirkt erst auf "Übernehmen", `YnabCategory.Hidden` (aus
  YNABs `hidden`-Feld) hält versteckte Kategorien eingeklappt. Zeitraum-Schnellwahl
  trägt Von/Bis sichtbar ein (`quickDateRange`); Payee filtert per Vorschlag sofort;
  Memo/Betrag sind Suchfelder mit Verzögerung. Am PC hängt die Auswahl als Popover
  unter dem Chip (`Client.DesignSystem.FilterPopover`, Esc/Klick daneben schließen
  ohne zu filtern), am Handy im bestehenden `BottomSheet`; welche Chrome gerendert
  wird, entscheidet `Viewport.isDesktopViewport()` zur Renderzeit (ADR 0043), belegt
  durch einen zweiten Playwright-Kontext im 390x844-Viewport. Die Summenzeile
  (`Components.Ledger.View.summaryRow`) erscheint, sobald ein Filter aktiv ist, und
  zeigt Trefferzahl/Summe über den ganzen Treffer (`model.TotalCount`/`model.TotalAmount`,
  unverändert vom Server). Der sticky Tabellenkopf der PC-Tabelle (`Table.fs`,
  `.table-sticky-thead thead`) bekommt seinen Versatz über `--ledger-filter-bar-height`,
  eine CSS-Variable, die `Components.Ledger.View.FilterBarColumn` per `ResizeObserver`
  auf die tatsächlich gerenderte Höhe der Filterleiste setzt, statt auf eine feste
  Schätzung: so kollidieren beide sticky Elemente auch dann nicht, wenn die Leiste bei
  aktivem Filter zweizeilig umbricht (`md:flex-wrap`) oder während eines laufenden
  Spiegel-Laufs (ADR 0014) den Knoten wechselt, weil `ChipBar.bar` dafür einen
  Callback-Ref statt eines Objekt-Refs annimmt: er trennt und beobachtet bei jedem
  Knotenwechsel neu, nicht nur beim ersten Mount. Die Variable fällt außerhalb
  dieser Spalte auf `0px` zurück, jede andere sticky Tabelle im Design-System bleibt
  vom dokumentierten `Table.Sticky`-Vertrag unverändert erfasst.

## Open questions
- **Historie-Technik** — *gelöst (ADR 0013, ledger-fsz0a):* append-only Ereignis-Log pro
  Buchung als Quelle der Wahrheit, Zustandsmodell synchron in derselben Transaktion
  mitgeschrieben, reiner `apply`-Replay als Pflichttest. SQLite bleibt, kein Postgres.
- **Spiegel-Mechanik**: *gelöst (ADR 0014, umgesetzt in ledger-m8r2v und ledger-h4p6c):*
  Delta-Sync über `server_knowledge` je Budget, Löschung als Flag statt physischem
  Löschen, 429 bricht den Lauf ab und steht im Lauf-Status. Konten werden je Lauf
  vollständig geholt (ADR 0018). Alle drei Auslöser (manuell, Frische-Kriterium, Lauf
  nach Push) und der serverseitige Einzel-Flug-Schutz stehen (`ledger-h4p6c`).
- **Kursquelle** — *gelöst (ADR 0016, ledger-zm0zx):* Schnittstelle mit Handeingabe und späterem automatischem Abruf, konkrete Quelle als Research-Spike im Backlog.
- **Bar- und Quick-Add-Konto** — *gelöst (ADR 0016):* ein Konto "Bar", Quick Add ist Herkunft.
- **Quick Add** direkt ins Kassenbuch? *Gelöst (ADR 0021, 2026-09-11):* erst Kassenbuch, dann Push, dann Verknüpfung (`ledger-tdnpk`).
- **Identität der Buchung** — *gelöst (ADR 0015, ledger-z5vw6):* eigene Id
  (`Guid.CreateVersion7()`), Fremd-Ids (YNAB, ImportId, Comdirect) als Verknüpfungen in
  eigener Tabelle; Spiegel matcht YNAB-Id vor ImportId, sonst neue Buchung als Spiegel.
