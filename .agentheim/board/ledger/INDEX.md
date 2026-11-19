# ledger — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `modeling` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 11
- **Todo:** 0
- **Doing:** 2
- **Done:** 151
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **ledger-rb3xw** — "Begriffe und Texte im Kassenbuch: ein Name für den Lena-Filter, leerer Zustand „Keine Buchungen“ nach §7.12 mit „Abrufen“, Listen-Platzhalter ohne „Transaktionen“" (refactor) — `doing/ledger-rb3xw-kassenbuch-begriffe-und-leerer-zustand.md`
- **ledger-gy77x** — "Knöpfe im Kassenbuch nach DS: „+ Buchung“ als Button.fab, „Abrufen“ und „Jetzt spiegeln“ als Symbolknopf im Kopf, „Lena fragen“ auf der Karte wie im Detail, „Abbrechen“ ohne Füllung in „Betrag“ und im Split-Editor" (refactor) — `doing/ledger-gy77x-kassenbuch-knoepfe-nach-ds.md`
<!-- doing-list:end -->

### Done (current-month entries live; older months archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
- **ledger-pfhdq** — "Späte und gescheiterte Antworten im Kassenbuch: Available, Kontostände und Spiegel-Status tragen eine Lade-Nummer, ältere Antworten fallen weg, ein gescheitertes Nachladen der Kontostände bleibt mit Hinweis sichtbar (Components/Ledger/State.fs CategoriesLoaded, BalancesLoaded, MirrorStatusLoaded)" (bug) — `done/ledger-pfhdq-spaete-antworten-available-kontostaende-spiegel.md`
- **ledger-awf5b** — "Ein übernommener Betragsfilter mit Komma („12,50“) gilt nicht mehr als laufende Eingabe: isInputActive vergleicht die Betragsentwürfe als Wert statt als Text, der Update-Neustart beim Öffnen wird nicht mehr zurückgehalten (src/Client/State.fs isInputActive)" (bug) — `done/ledger-awf5b-betragsfilter-haelt-update-zurueck.md`
- **ledger-ef068** — "Kassenbuch-Liste nach DS: gelbe Kante am Anker in der Kompaktliste mit Maus, Tokens für Auswahl und Rahmen, Kopf „Schnellwahl“ als sectionTitle, „Alles geprüft“ und „Alles kategorisiert“ als Card.allDone" (bug) — `done/ledger-ef068-kassenbuch-ds-abweichungen.md`
- **ledger-y8xrr** — "Kassenbuch-Liste behält Seiten, Scrollstand und Auswahl auch nach eigenen Aktionen: Anlegen, Rückgängig nach dem Anlegen, Löschen, Wiederherstellen und „Geprüft“ für die Auswahl laden im Hintergrund nach statt neu aufzubauen (Components/Ledger/State.fs, LoadEntries an fünf Stellen)" (bug) — `done/ledger-y8xrr-nachladen-stand-und-alte-zahlen.md`
- **ledger-hme9k** — "Durchgehen und Detail ohne zweite Ebene: Payee und Memo im offenen Detail öffnen kein zweites Sheet oder zweiten Dialog, das Regel-Formular ersetzt den Inhalt desselben Detail-Sheets, × im Split-Editor am PC führt ins Detail zurück, „Details“ schließen im Durchgehen über denselben Helfer wie im Kassenbuch (Components/Ledger/View.fs, State.fs)" (bug) — `done/ledger-hme9k-durchgehen-detail-gestapelte-sheets.md`
- **ledger-b7k2q** — "Bob-Grenzen vollständig: Commit prüft das Datumsfenster am Berliner Tag des Commits neu, Vorschau begrenzt Rumpf auf 8192 Bytes und Kategorie auf 200 Zeichen, „kein Flag“ im gesendeten Rumpf belegt, API-Test des Datumsfensters auf einen Fall gekürzt (BotApi.fs bookWithinLimit, readBody, Validation.fs parseBotPreviewRequest)" (bug) — `done/ledger-b7k2q-bob-grenzen-flag-commit-rumpf.md`
- **ledger-npqcb** — "Filter-Sheet am Handy: Kategorie, Zeitraum und Sortieren ersetzen den Inhalt des Sheets statt ein zweites Sheet zu öffnen" (feature) — `done/ledger-npqcb-filter-sheet-ersetzt-inhalt.md`
- **ledger-pkrwd** — "Payee einer Buchung leeren: im Detail den Payee entfernen, nach YNAB ohne Payee zurückschreiben" (feature) — `done/ledger-pkrwd-payee-leeren.md`
- **ledger-zfj78** — "Detail einer Buchung ordnen (Sheet und Panel): Payee als Kopf, Betrag mit Prüfstand, Knopf Geprüft, Payee mit alle zeigen, Aufteilen und Lena als Knopfzeile, Historie aufklappbar in der Form von §7.11" (feature) — `done/ledger-zfj78-detail-ordnen.md`
- **ledger-cnaz3** — "Kassenbuch am PC ordnen: Kopf mit Konto und Saldo, Abrufen, n durchgehen und + Buchung; Filterleiste mit Suche; Tabelle ohne Status-Spalte und Prüf-Knopf; Panel immer da, ab zwei gewählten Buchungen als Zusammenfassung" (feature) — `done/ledger-cnaz3-kassenbuch-pc-ordnen.md`
- **ledger-d9z3n** — "Kassenbuch am Handy ordnen: eine Kontowahl als Feld mit Saldo, freie Suche statt Payee- und Memo-Filter, Filter-Knopf mit Sheet und gesetzte Filter als Chips; gilt unter 1024 px" (feature) — `done/ledger-d9z3n-kassenbuch-handy-ordnen.md`
- **ledger-sre0f** — "Zuletzt-Chips in „Neue Buchung“ aus den Buchungen des gewählten Kontos, fünf mit Memo" (bug) — `done/ledger-sre0f-zuletzt-chips-aus-dem-gewaehlten-konto.md`
- **ledger-pnjz3** — "Kassenbuch zeigt keine leeren Tagesblöcke, jeder Tag einmal und in der richtigen Reihenfolge" (bug) — `done/ledger-pnjz3-keine-leeren-tagesbloecke.md`
- **ledger-wj0b8** — "Einstellungen: Satz unter „Quick-Add-Konto“ lautet „Vorbelegt bei „+ Buchung“ ohne Kontofilter“" (chore) — `done/ledger-wj0b8-satz-quick-add-konto.md`
- **ledger-hg5m5** — "Regel aus der Buchung: „Regel anlegen“ im Detail und beim Durchgehen, dazu ein Angebot nach einer Kategorie-Änderung, vorbelegt mit Payee und Kategorie" (feature) — `done/ledger-hg5m5-regel-angebot-nach-kategorie-aenderung.md`
- **ledger-ea70s** — "Ein Erfassen: Quick Add hört auf, ein eigener Ort zu sein; „+ Buchung“ in Kassenbuch und Übersicht mit kluger Vorbelegung" (feature) — `done/ledger-ea70s-ein-erfassen.md`
- **ledger-ay81j** — "In BB erfasste Buchungen gehen ohne import_id an YNAB, der Spiegel verknüpft offene Erfassungen nach Konto, Tag, Betrag und Payee" (bug) — `done/ledger-ay81j-ynab-gleicht-barbuchungen-ab.md`
- **ledger-nnd2f** — "Der Spiegel legt nur noch zusammen, wenn YNAB einen Comdirect-Import mit einem Übertrag abgeglichen hat; eine verschluckte Barbuchung bleibt im Kassenbuch stehen (Ledger/Domain.fs mergeObservedInYnab)" (bug) — `done/ledger-nnd2f-zusammenlegen-nur-bei-uebertraegen.md`
- **ledger-nkwqb** — "Von YNAB abgeglichene Buchung bleibt im Kassenbuch doppelt: der Spiegel liest matched_transaction_id nicht und legt die zusammengelegte Buchung nicht still (Ledger/Domain.fs, Ledger/CommandHandlers.fs)" (bug) — `done/ledger-nkwqb-abgleich-in-ynab-und-uebertrag-als-duplikat.md`
- **ledger-f8bjm** — "Ersatz-POST mit verlorener Antwort: Wiederherstellen, Split- und Betrags-Ersatz scheitern danach dauerhaft an 409, der Spiegel legt die Ersatzbuchung als neue Buchung „YNAB-Spiegel“ an; die Ersatz-import_id muss vor dem POST im Kassenbuch stehen (Ledger/CommandHandlers.fs)" (bug) — `done/ledger-f8bjm-ersatz-post-verlorene-antwort.md`
- **ledger-dn4vy** — "Filter und Liste nach Styleguide: Betrag Von/Bis mit Zifferntastatur, Name und Fehlerzustand, Schnellwahl als Segmented, Payee-Vorschläge mit Tastatur, lesbare ausgegraute Zeile, Fokus am Prüfknopf, Lena-Grund als Text" (bug) — `done/ledger-dn4vy-filter-und-liste-styleguide.md`
- **ledger-nc708** — "Detail und Durchgehen halten ihren Stand: Historie an Ladevorgänge gebunden ohne Flackern, jede Änderung an der offenen Buchung lädt ihre Historie nach, gescheitertes „Lena fragen“ holt die Karte zurück (Components/Ledger/State.fs LoadHistory, HistoryLoaded, LenaFlagSet)" (bug) — `done/ledger-nc708-detail-und-durchgehen-halten-stand.md`
- **ledger-ng110** — "Split-Editor nach Styleguide: offener Rest gelb, zu viel verteilt rot, Zeilenfelder mit Fehlerzustand, Entfernen als removeIcon mit Sperre, Speicherfehler im offenen Editor statt Toast" (bug) — `done/ledger-ng110-eingaben-fehler-styleguide.md`
- **ledger-ct3cx** — "Kassenbuch-Liste behält Auswahl, Seiten und Scrollstand nach Spiegel-Lauf und Abruf: Nachladen im Hintergrund statt Neuaufbau, Filter entfernen überholt wartende Eingaben, ein Neuaufbau je Wechsel (Components/Ledger/State.fs resetEntries, AccountsLoaded, fetchExternalCmd)" (bug) — `done/ledger-ct3cx-kassenbuch-behaelt-stand.md`
- **ledger-vf6z5** — "„Neue Buchung“ nach Styleguide: Betrags- und Kontofehler am Feld mit Fokus, direkte Fehlertexte ohne „Bitte“, Kontoliste mit Spinner und ehrlichem Leer- und Fehlerzustand" (bug) — `done/ledger-vf6z5-neue-buchung-fehler-am-feld.md`
- **ledger-sjbvc** — "Sammellauf der Auswahl in einem Feld statt drei Bool-Flags: alle Sammelstarts und ihr Rückgängig prüfen einen Merker, StartReviewMarkBatch und StartUndoReviewMarkBatch sperren richtig, Aufzählung über die drei Sammelaktionen nach Doktrin Punkt 20" (refactor) — `done/ledger-sjbvc-sammellauf-ein-feld.md`
- **ledger-vfr4h** — "Schreib-Sperre, Spiegel-Auslöser nach Freigabe und Wartesperre für Verwerfen nach dem Erfassen, Übertrag, Löschen, Wiederherstellen und Ist neu durch Verhalten belegt; Wartesperre als erster Zweig in discardCapturedBooking und restoreDeletedEntry" (refactor) — `done/ledger-vfr4h-sperre-und-ausloeser-belegen.md`
- **ledger-smv1s** — "Zeilen-Sperre für Erneut senden, Rückgängig und Setzen einheitlich in update: ein Helfer für alle Start-Zweige je Buchung und Feld, Aufzählung je Sperrgruppe nach Doktrin Punkt 20, Deciding im Durchgehen an die Id gebunden" (bug) — `done/ledger-smv1s-doppelaufruf-sperren.md`
- **ledger-xqqnv** — "Vorhandene import_id heißt angekommen: Barbuchung, Erneut senden, Ist neu und Import-Sammelpush schreiben keinen Fehlschlag, wenn YNAB die import_id schon kennt; Sperre und Auslöser des Import-Pushs belegt (Ledger/CommandHandlers.fs)" (bug) — `done/ledger-xqqnv-schreibwege-einheitlich.md`
- **ledger-v6s7c** — "Durchgehen Karte für Karte unter der Übersicht: ungeprüfte Buchungen bestätigen mit Kategorie, Vorschlägen, Bestell-Link, Memo, Lena und Wischen, Duplikat-Karte für wartende Buchungen, Zu tun und „Abrufen“ führen dorthin" (feature) — `done/ledger-v6s7c-durchgehen-karte-fuer-karte.md`
- **ledger-r7h2w** — "Wartende Buchung (unsicheres Duplikat) übernehmen oder verwerfen: Befehle „Ist neu“, „Ist doppelt, verwerfen“ und Rückgängig mit Ereignissen, „Ist neu“ schickt sie nach YNAB" (feature) — `done/ledger-r7h2w-wartende-buchung-uebernehmen-oder-verwerfen.md`
- **ledger-fxt09** — "Payee einer Buchung in BB ändern: Befehl mit Ereignis und Schreibweg nach YNAB, im Buchungs-Detail und auf der Karte beim Durchgehen, Rückgängig im Toast" (feature) — `done/ledger-fxt09-payee-aendern-in-bb.md`
- **ledger-s1weh** — "Übersicht als Einstieg (Variante A): Zu tun mit einer Hauptaktion, Zähler an der Übersicht, „+ Buchung“, am PC zweispaltig" (feature) — `done/ledger-s1weh-uebersicht-als-einstieg.md`
- **ledger-q42pg** — "Betrag bearbeiten: leerer Betrag oder 0 als Meldung am Feld statt Toast" (bug) — `done/ledger-q42pg-betrag-feldfehler-statt-toast.md`
- **ledger-m7a9q** — "Probelauf: Betrag eines Übertrags per Update in YNAB ändern" (spike) — `done/ledger-m7a9q-probelauf-betrag-uebertrag.md`
- **ledger-mj3a9** — "BOT_MAX_AMOUNT_EUR: ein gesetzter, aber ungültiger Wert sperrt Bob, statt auf 500 € zurückzufallen" (bug) — `done/ledger-mj3a9-bot-betragsdeckel-faellt-sicher.md`
- **ledger-m7mye** — "Betrag einer Buchung im Detail bearbeiten, auch in YNAB" (feature) — `done/ledger-m7mye-betrag-bearbeiten-im-detail.md`
- **ledger-z6sya** — "Buchung löschen im Detail, auch in YNAB, mit Rückgängig" (feature) — `done/ledger-z6sya-buchung-loeschen-im-detail.md`
- **ledger-m7k2d** — "Split einer Barbuchung lässt sich nicht ändern: Ersatz-import_id wäre länger als 36 Zeichen" (bug) — `done/ledger-m7k2d-split-einer-barbuchung-l-sst-sich-nicht-ndern-ersatz-import.md`
- **ledger-at63w** — "Buchung im Kassenbuch erfassen für jedes Konto ohne Import, mit Vorbelegung (Konto, Datum, Zuletzt, Kategorie zum Payee)" (feature) — `done/ledger-at63w-buchung-im-kassenbuch-erfassen.md`
- **ledger-qvjvs** — "Buchung zum Übertrag machen: Konten im Kategorie-Picker des Kassenbuch-Details, Schreibweg nach YNAB" (feature) — `done/ledger-qvjvs-buchung-zum-uebertrag-machen.md`
- **ledger-p3vq8** — "addCashExpense: gelingt der Push und scheitert danach das Schreiben der Verknüpfung, legt der Spiegel eine zweite Buchung an" (bug) — `done/ledger-p3vq8-push-gelungen-verknuepfung-nicht-geschrieben.md`
- **ledger-r8h19** — "Aufräum-Lauf ledger: Quelltext-, Klassenstring- und Konstanten-Tests ersetzen oder löschen, Logik aus [layout]-Tests nach .NET, N+1 in Sammelbefehlen und Spiegel-Lauf" (chore) — `done/ledger-r8h19-aufraeum-lauf-test-altlasten-und-n-plus-1.md`
- **ledger-bk3q8** — "Quick Add und Bob über einen gemeinsamen Barbuchungs-Befehl, Push-Bau als reine Funktion; ein Kategorien-Ladefehler bucht nicht mehr still ohne Kategorie" (bug) — `done/ledger-bk3q8-barbuchung-und-push-bau-gemeinsam-und-getestet.md`
- **ledger-vq2g8** — "Kassenbuch zeigt bei gescheitertem Abruf 0,00 € oder lädt endlos; es soll Laden und Fehler sichtbar nennen" (bug) — `done/ledger-vq2g8-kassenbuch-nennt-ladefehler-statt-null-und-dauer-laden.md`
- **ledger-kfd5m** — "Schreib-Sperre: Quick Add, Bob, Erneut senden und Prüfvermerk sperren den Spiegel nicht aus, die meisten Sperren sind ungetestet" (bug) — `done/ledger-kfd5m-schreib-sperre-nachziehen-und-testen.md`
- **ledger-z71ka** — "Bot-Commit: entry_id auch nach gescheitertem Push, und ein abgebrochener Commit ist wiederholbar, ohne doppelt zu buchen" (bug) — `done/ledger-z71ka-bot-commit-entry-id-nach-push-fehler.md`
- **ledger-k9tm2** — "Kassenbuch-Ereignisse nach `events` überführen: Kopie in rowid-Reihenfolge mit Abbruchprüfung, `appendAndApply` und Ladepfade umstellen, `ledger_entry_events` danach entfernen" (feature) — `done/ledger-k9tm2-kassenbuch-ereignisse-nach-events-berf-hren-kopie-in-rowid-r.md`
- **ledger-r6wn3** — "Spiegel verknüpft eine über die ImportId gefundene Buchung mit `EntryLinkedToYnab` statt über `linkEntry` ohne Ereignis; alte Verknüpfungen ohne Ereignis bekommen einmalig ihr Ereignis nachgetragen" (bug) — `done/ledger-r6wn3-linkentry-schreibt-ledger-entry-links-ohne-ereignis-gegen-ad.md`
- **ledger-cy1j8** — "Bug: rebuildSplitIndexes löscht und füllt die Split-Indextabellen ohne Transaktion, parallele initializeLedgerSchema-Aufrufe werfen einen UNIQUE-Fehler" (bug) — `done/ledger-cy1j8-rebuild-split-indexes-rennen.md`
- **ledger-q4m7z** — "Quick-Add: BB-Validierungsfehler nicht als \"YNAB hat abgelehnt\" melden" (bug) — `done/ledger-q4m7z-quick-add-bb-validierungsfehler-nicht-als-ynab-hat-abgelehnt.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **ledger-zf6zg** — "„Neue Buchung“ nach DS: Kontowahl als Money.accountChoiceRow wie im Sheet „Konto“, Kopf des Panels wie der Kopf des Details, „Abbrechen“ ohne Füllung, Payee-Feld sagt „optional“" (refactor) — `backlog/ledger-zf6zg-neue-buchung-nach-ds.md`
- **ledger-pz028** — "Kassenbuch-State aufräumen: drei Antworten tragen Result, das Quick-Add-Konto kommt aus den Settings des Root statt aus einem eigenen Abruf, die toten Kategorien-Zweige fallen weg, „Weitere laden“ und Nachladen teilen einen Zustand statt LoadingMore (Components/Ledger/State.fs, Types.fs)" (refactor) — `backlog/ledger-pz028-aufraeumen-antworten-quickadd-ladezustand.md`
- **ledger-zqst5** — "Kassenbuch am Handy: Suche, Filter-Knopf und Chips haften oben beim Scrollen" (feature) — `backlog/ledger-zqst5-suche-und-filter-am-handy-haften.md`
- **ledger-fp4wn** — "Auswahl außerhalb des geladenen Fensters: Leiste und Panel zählen gewählte, nicht mehr geladene Buchungen mit" (bug) — `backlog/ledger-fp4wn-auswahl-ausserhalb-des-fensters-mitzaehlen.md`
- **ledger-yhemh** — "Neue Buchung am Handy: Memo steht neben dem Datum statt darunter" (bug) — `backlog/ledger-yhemh-memo-unter-datum-am-handy.md`
- **ledger-h7de3** — "Fehlertexte in Ereignissen: fehlendes Token als „nicht eingerichtet“, YNAB-Rohtext nur ins Log, Token- und Budget-Zugriff an einer Stelle" (bug) — `backlog/ledger-h7de3-fehlertexte-in-ereignissen.md`
- **ledger-t4rk8** — "Betrag eines Übertrags in BB ändern (Sperre nach Probelauf aufheben)" (feature) — `backlog/ledger-t4rk8-betrag-uebertrag-freigeben.md`
- **ledger-k4n2s** — "Kann-Sammlung ledger" (chore) — `backlog/ledger-k4n2s-kann-sammlung-ledger.md`
- **ledger-g47hp** — "Benachrichtigung für Lena bei neuer Rückfrage" (feature) — `backlog/ledger-g47hp-benachrichtigung-fuer-lena.md`
- **ledger-ayjzq** — "Sicht 'Für mich' für Lena in BB: alles, was ihr zugewiesen ist" (feature) — `backlog/ledger-ayjzq-sicht-fuer-mich.md`
- **ledger-nb4tv** — Geldmarkt-ETF als Konto anlegen und den monatlichen Abgleich klären (offenes v1-Kriterium) (decision) — `backlog/ledger-nb4tv-etf-konto-in-ynab-anlegen-und-abgleich-klaeren.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/ledger/INDEX.md`
