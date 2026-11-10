# ledger — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `modeling` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 5
- **Todo:** 0
- **Doing:** 2
- **Done:** 116
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **ledger-m7a9q** — "Probelauf: Betrag eines Übertrags per Update in YNAB ändern" (spike) — `doing/ledger-m7a9q-probelauf-betrag-uebertrag.md`
- **ledger-q42pg** — "Betrag bearbeiten: leerer Betrag oder 0 als Meldung am Feld statt Toast" (bug) — `doing/ledger-q42pg-betrag-feldfehler-statt-toast.md`
<!-- doing-list:end -->

### Done (current-month entries live; older months archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
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
- **ledger-k4n2s** — "Kann-Sammlung ledger" (chore) — `backlog/ledger-k4n2s-kann-sammlung-ledger.md`
- **ledger-g47hp** — "Benachrichtigung für Lena bei neuer Rückfrage" (feature) — `backlog/ledger-g47hp-benachrichtigung-fuer-lena.md`
- **ledger-ayjzq** — "Sicht 'Für mich' für Lena in BB: alles, was ihr zugewiesen ist" (feature) — `backlog/ledger-ayjzq-sicht-fuer-mich.md`
- **ledger-hg5m5** — "Regel-Angebot nach einer Kategorie-Änderung im Kassenbuch: Anbieten, aus Payee und neuer Kategorie eine Kategorisierungsregel anzulegen" (feature) — `backlog/ledger-hg5m5-regel-angebot-nach-kategorie-aenderung.md`
- **ledger-nb4tv** — Geldmarkt-ETF als Konto anlegen und den monatlichen Abgleich klären (offenes v1-Kriterium) (decision) — `backlog/ledger-nb4tv-etf-konto-in-ynab-anlegen-und-abgleich-klaeren.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/ledger/INDEX.md`
