---
id: ledger-kdnnz
title: "Spiegel erweitern: Prüfvermerk (approved und cleared) und Flags beobachten, Chip \"Zu prüfen\" und Kennzeichen \"Rückfrage Lena\" anzeigen"
status: done
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-b1xwe, design-system-001]
blocks: [ledger-r777c]
tags: [backend, frontend, kassenbuch, spiegel, scheibe, katalog-aenderung]
related_adrs: [0013, 0014, 0028, 0032]
related_research: []
prior_art: [ledger-m8r2v, ledger-e3k7p, ledger-p3n8k]
---

## Why
Das UX-Konzept braucht zwei Angaben, die der Spiegel heute nicht beobachtet: ob Roman eine
Buchung schon geprüft hat, und ob sie ein Flag trägt. Der Ereignis-Katalog
(`knowledge/contexts/ledger/README.md`, Abschnitt "Ereignisse") kennt Details,
Kategorisierung, Cleared und Löschung, aber weder `approved` noch Flags.

Schon die **lesende** Anzeige hängt daran: der Chip "Zu prüfen (n)" ist der Einstieg in
Romans häufigsten Handy-Ablauf, und das Kennzeichen "Rückfrage Lena" macht sichtbar, was
seine Frau sich ansehen soll. `ledger-r777c` (Prüfvermerk setzen) baut auf diesem Zustand auf.

## What
Festgelegt mit Roman am 2026-09-14, verbindlich in **ADR 0032**:

- **Geprüft = `approved` und (`cleared` ist Cleared oder Reconciled).** "Zu prüfen" ist
  jede nicht gelöschte Buchung mit `approved = false` oder `cleared = Uncleared`.
- **Kein Nachholen für den Bestand.** Gespeicherte Ereignisse ohne die neuen Felder gelten
  als `approved = true` und ohne Flag.
- **Flags roh im Log**, gedeutet im Lesepfad: Name nach Trim und ohne Groß-/Kleinschreibung
  gleich "Lena" ergibt *Rückfrage Lena*, jedes andere gesetzte Flag *Markiert*.
- **Neue Beobachtungen sind in der Historie sichtbar.**
- **Rein lesend.** Setzen von Prüfvermerk oder Flag aus BB ist `ledger-r777c` bzw. v2.

Arbeitsfolge für einen Worker:

1. **Beobachtungs-Contract** (`src/Server/YnabObservations.fs`): `YnabObservation` bekommt
   `Approved: bool`, `FlagColor: string option`, `FlagName: string option`, gelesen aus
   `approved`, `flag_color`, `flag_name`.
2. **Ereignis-Katalog** (`src/Server/Ledger/Events.fs`):
   - neu `EntryApprovalChangeObservedInYnab` (`OldApproved`/`NewApproved`) und
     `EntryFlagChangeObservedInYnab` (`OldFlagColor`/`OldFlagName`/`NewFlagColor`/
     `NewFlagName`), je Schema-Version 1, mit Codec, Typname und Version;
   - `EntryObservedInYnab` in **Schema-Version 2** mit `approved`, `flagColor`, `flagName`;
   - `EntryImportedFromComdirect` und `CashExpenseAdded` in **Schema-Version 2** mit
     `approved` (beim Schreiben `false`: der Push sendet kein `approved`, YNAB legt die
     Buchung unbestätigt an);
   - Version 1 aller drei bleibt lesbar und liefert `approved = true`, kein Flag.
3. **Domain** (`src/Server/Ledger/Domain.fs`): `Entry` bekommt `Approved`, `FlagColor`,
   `FlagName`. `apply` übernimmt sie aus den Entstehungs-Ereignissen und ändert sie nur über
   die beiden neuen Ereignisse. `observe` erkennt Prüfvermerk und Flag als eigene Gruppen,
   nach Cleared, und erzeugt bei unverändertem Stand kein Ereignis. Die Stellen, an denen
   heute Entstehungs-Payloads gebaut werden (`recordComdirectPush`, Quick Add in
   `CommandHandlers.fs`), setzen `Approved = false`.
4. **Persistenz** (`src/Server/Ledger/Persistence.fs`): Spalten `approved INTEGER NOT NULL
   DEFAULT 1`, `flag_color TEXT`, `flag_name TEXT` in der Zustandstabelle, Migration per
   `ALTER TABLE ... ADD COLUMN` nach dem bestehenden Muster; Upsert und Row-Mapping erweitert.
5. **Shared und Lesepfad** (`src/Shared/Domain.fs`, `src/Server/Ledger/QueryHandlers.fs`):
   - `LedgerEntryView` bekommt `NeedsReview: bool` und `Flag: LedgerFlag`
     (`NoFlag | AskLena | Marked`), Deutung als reine Funktion;
   - `getLedgerEntriesPage` bekommt `OnlyNeedsReview` (Standard `false`) und liefert
     `NeedsReviewCount`, unabhängig von `OnlyNeedsReview`, im gewählten Kontofilter (Konto
     oder alle Konten), ohne gelöschte Buchungen; Muster wie `IncludeDeleted`/`DeletedCount`
     aus `ledger-p3n8k`;
   - `getEntryHistory` bekommt sprechende Sätze, z. B. "In YNAB genehmigt", "Genehmigung in
     YNAB zurückgenommen", "Flag „Lena“ in YNAB gesetzt", "Flag in YNAB entfernt".
6. **Spiegel-Lauf** (`src/Server/Ledger/CommandHandlers.fs`): Buchungen, bei denen sich nur
   Prüfvermerk oder Flag geändert hat, zählen im Lauf-Status als geändert.
7. **Anzeige** (Kassenbuch-Seite, Handy und PC): Chip "Zu prüfen (n)" über der Liste, in
   Konto- und Gesamtsicht, schaltet `OnlyNeedsReview` (Standard aus). Ungeprüfte Buchungen
   tragen einen kleinen Punkt am linken Zeilenrand; Kennzeichen *Rückfrage Lena* (violett)
   und *Markiert* (orange) in der Zeile. Nur mit Design-System-Komponenten, gemessen an
   `standards/frontend/styleguide.md`.

**Nicht in dieser Scheibe:** die übrige Filterleiste (`ledger-vgfw3`), die Statusspalte am
PC (`ledger-qns4j`), das Detail-Sheet (`ledger-q1hw2`), jede schreibende Geste
(`ledger-r777c`).

## Acceptance criteria
- [ ] `YnabObservation` trägt `Approved`, `FlagColor`, `FlagName`; ein Decoder-Test mit
      echtem YNAB-JSON (inkl. `flag_color: null`, `flag_name: null`) liest sie korrekt.
- [ ] `EntryApprovalChangeObservedInYnab` und `EntryFlagChangeObservedInYnab` existieren mit
      Encode/Decode-Roundtrip-Test.
- [ ] Ein gespeichertes `EntryObservedInYnab` in Schema-Version 1 (JSON ohne die neuen
      Felder) dekodiert zu `Approved = true`, ohne Flag, sonst unverändert (Regressionstest).
- [ ] Gespeicherte `EntryImportedFromComdirect` und `CashExpenseAdded` in Schema-Version 1
      falten zu `Approved = true` (Regressionstest: alte BB-Buchungen fallen nicht in
      "Zu prüfen").
- [ ] Neu geschriebene `EntryImportedFromComdirect`, `CashExpenseAdded` und
      `EntryObservedInYnab` tragen Schema-Version 2; eine neue Comdirect- oder
      Quick-Add-Buchung hat `Approved = false`.
- [ ] `observe` mit unverändertem `approved` und Flag erzeugt kein Ereignis (Idempotenz).
- [ ] `observe` mit geändertem `approved` erzeugt genau ein
      `EntryApprovalChangeObservedInYnab`, mit geändertem Flag (Farbe oder Name) genau ein
      `EntryFlagChangeObservedInYnab`, auch wenn sich zugleich andere Gruppen ändern.
- [ ] Nach Push (neue Buchung mit `Approved = false`) und erster Beobachtung mit
      `approved: false` entsteht kein Prüf-Ereignis.
- [ ] Der Replay-Pflichttest (ADR 0013) bleibt grün und deckt die neuen Ereignisse und
      beide Schema-Versionen ab.
- [ ] Die Migration fügt die Spalten einer bestehenden Zustandstabelle hinzu; bestehende
      Zeilen haben danach `approved = 1` und kein Flag.
- [ ] Die Flag-Deutung ist eine reine, getestete Funktion: kein Flag ergibt `NoFlag`;
      "Lena", "lena", " Lena " ergeben `AskLena`; jedes andere gesetzte Flag (auch nur eine
      Farbe ohne Namen) ergibt `Marked`.
- [ ] `NeedsReview` ist wahr genau bei `approved = false` oder `cleared = Uncleared`
      (Test über alle Kombinationen, Reconciled zählt als geprüft).
- [ ] `getLedgerEntriesPage` mit `OnlyNeedsReview = true` liefert nur ungeprüfte, nicht
      gelöschte Buchungen; Trefferzahl und Summe beziehen sich auf diesen Treffer.
- [ ] `NeedsReviewCount` ist bei `OnlyNeedsReview = true` und `false` gleich, folgt dem
      Kontofilter und zählt gelöschte Buchungen nicht (Test).
- [ ] Die Historie zeigt für beide neuen Ereignisse einen sprechenden Satz, nie den
      Ereignis-Typnamen (Test).
- [ ] Ein Spiegel-Lauf, der nur einen Prüfvermerk oder ein Flag ändert, zählt die Buchung
      im Lauf-Status als geändert (Test).
- [ ] Die Kassenbuch-Seite zeigt den Chip "Zu prüfen (n)" in Konto- und Gesamtsicht; Antippen
      filtert die Liste, erneutes Antippen hebt den Filter auf. Die Reducer-Logik dafür ist
      unter .NET getestet (ADR 0020).
- [ ] Ungeprüfte Zeilen tragen den Punkt, geflaggte das passende Kennzeichen, am Handy und am
      PC; per Screenshot beider Größen gegen den Styleguide geprüft.
- [ ] Der Ereignis-Katalog im BC-README ist um die beiden Ereignisse, die Schema-Versionen 2
      und die Vorgaben für alte Ereignisse ergänzt.
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Briefs: `docs/ux/kassenbuch/briefs.md`,
Ablauf 1 und 5 (dort mit den schreibenden Teilen, die hier nicht dazugehören). Verfeinert
am 2026-09-14 mit Roman; Entscheidungen in ADR 0032.

YNAB-Felder laut OpenAPI: `approved` (bool, nie null), `flag_color` (red, orange, yellow,
green, blue, purple oder null), `flag_name` (string oder null).

Hinweise für den Worker:
- Serena ist oft nicht verfügbar; `.fs`-Dateien dann direkt lesen.
- Reducer-Pfade, die `Client.Api` berühren, werfen unter .NET. Die Chip-Logik als reine
  Nachricht testen (ADR 0020).
- `Badge.defaultProps` wirft unter `dotnet test` (`design-system-bq4tz`); View-Code nicht
  unter .NET instanziieren.
- Tests nie gegen die echte Datenbank (ADR 0030, In-Memory über runsettings).
- Die Umbenennung "Flag setzen" zu "Lena fragen" und weitergedachte Formen liegen in
  `ledger-xfjcp`.

## Verifier note (iteration 1)

**VERDICT: FAIL** — `ITERATION_HINT: likely-fixable`

**REASONS (wörtliches Zitat des Verifier-Urteils):**
- Kriterium "Ungeprüfte Zeilen tragen den Punkt, geflaggte das passende Kennzeichen, am Handy und am PC; per Screenshot beider Größen gegen den Styleguide geprüft" ist nicht erfüllt. Ich werte das als echten FAIL-Grund, nicht als `[human-eye]`-Wartestand: das Kriterium trägt keinen `[human-eye]`-Marker, es liegt kein "manuell ausgeführt"-Vermerk vor (der Worker berichtet ehrlich, gar nicht ausgeführt zu haben), und die Begründung über schon geprüfte Bausteine ist genau der Proxy, den ich nicht akzeptieren darf. Das Gewicht kommt aus dem Diff selbst: die Handy-Zeile wurde in `src/Client/Components/Ledger/View.fs` (entryRow, ab Zeile ~203) in eine neue zweispaltige Flex-Struktur mit 2px-Punktspalte umgebaut, und die PC-Zeile wechselt von `Table.trClickable` auf `Table.row` mit `ClassName = "border-l-2 border-l-neon-pink"` (Zeile ~300). Beides ist Layout-Änderung an bestehenden Zeilen, nicht nur ein zusätzliches Badge; das Styleguide-Gate des BC-README ist dafür bindend.
- Kriterium "Die Migration fügt die Spalten einer bestehenden Zustandstabelle hinzu; bestehende Zeilen haben danach approved = 1 und kein Flag" ist nicht durch einen Test gedeckt, der beim Fehlen des Produktionscodes scheitern würde. `src/Tests/LedgerTests.fs:999` baut eine eigene `migrateColumn`-Funktion und wiederholt die drei `ALTER TABLE`-Anweisungen wörtlich im Testkörper (Zeilen 1027-1037, 1050). Der Test ist zwar sauber isoliert (eigene `:memory:`-Connection, eigene alte Tabelle, unabhängig von der Migrationsreihenfolge) und belegt das SQLite-Verhalten inklusive folgenlosem Zweitversuch, prüft aber eine Kopie statt `Ledger.Persistence.initializeLedgerSchema`. Streicht jemand die Migration in `src/Server/Ledger/Persistence.fs:147-163`, bleibt dieser Test grün, und genau der Fall (Romans bestehende Datenbank mit rund 14.000 Buchungen) fällt niemandem auf. Ursache ist, dass `initializeLedgerSchema ()` (Persistence.fs:78-80) fest an `Core.getConnection ()` hängt und sich nicht auf eine Testverbindung richten lässt.
- Schreibregel, geringfügig: `src/Tests/LedgerPageTests.fs:1012` trägt im neuen Doc-Kommentar "bereits geprueft" statt "geprüft". Das ist Fließtext im Kommentar, also der Bereich, den die Regel meint. Die anderen Fundstellen (`"pruefvermerk"` als Budget-/Beobachtungs-Id, `"Baecker"` als Payee in einer JSON-Fixture) sind Bezeichner und Testdaten, kein Prosa-Verstoß, und für sich allein kein FAIL-Grund. Gedankenstriche in hinzugefügten Zeilen: keine.

**SUGGESTED_FIX:** Die Migration aus `initializeLedgerSchema` in eine eigene Funktion mit Connection-Parameter ziehen (z. B. `migrateLedgerEntryColumns (conn: SqliteConnection)`), aus `initializeLedgerSchema` aufrufen und den Test in LedgerTests.fs:999 diese Produktionsfunktion gegen seine alt geformte Tabelle fahren lassen statt der kopierten SQL-Zeilen; danach die Kassenbuch-Liste mit mindestens einer ungeprüften und einer geflaggten Buchung (Lena und ein anderes Flag) an Handy- und PC-Breite gegen den Styleguide ansehen und das Ergebnis im Outcome festhalten, oder, falls das Screenshot-Werkzeug weiterhin fehlt, das Kriterium mit Roman als `[human-eye]` markieren, damit die Lücke eine festgehaltene Entscheidung statt eines Versäumnisses ist.

**Der Rest der Aufgabe ist geprüft und grün:** `dotnet test` Exit 0 mit 870 bestanden / 0 Fehler / 7 übersprungen, `npm run build` Exit 0, alle übrigen 17 Kriterien mit passenden Tests belegt (V1-Regressionstests gegen handgeschriebenes Altbestands-JSON, Idempotenz und Gruppierung in `observe`, Erstbeobachtung nach Push ohne Prüf-Ereignis, Replay-Pflichttest um beide neuen Ereignisse erweitert und mit Log-Zeilen in Version 1 und 2, `NeedsReviewCount` nach dem `DeletedCount`-Muster, Wahrheitstabelle für `needsReview`, reine Flag-Deutung, sprechende Historien-Sätze, Chip-Reducer ohne Api-Berührung), Scope auf 14 Quell- und Testdateien beschränkt, kein `.agentheim/`-Pfad im Diff, keine ADR-Verletzung gegenüber 0013/0014/0028/0032.

**Conductor-Anmerkung zum Screenshot-Kriterium:** Beim ersten Dispatch existierte das Werkzeug tatsächlich nicht, `design-system-wa6wz` lief zeitgleich. Das ist inzwischen anders: `scripts/screenshot-route.mjs` ist fertig, verifiziert und integriert. Für Iteration 2 wird der Hauptstand in diesen Worktree gemergt, damit das Werkzeug zur Verfügung steht.

## Outcome

Der Spiegel beobachtet jetzt Prüfvermerk und Flags (ADR 0032), rein lesend. Chip
"Zu prüfen (n)" und die beiden Kennzeichen stehen; das Setzen aus BB bleibt `ledger-r777c`.

**Contract und Ereignisse.** `YnabObservation` (`src/Server/YnabObservations.fs`) trägt
`Approved`, `FlagColor`, `FlagName`. `Ledger.Events` bekommt
`EntryApprovalChangeObservedInYnab` und `EntryFlagChangeObservedInYnab`, je mit
Encode/Decode und Schema-Version 1. Die drei Entstehungs-Ereignisse
(`EntryObservedInYnab`, `EntryImportedFromComdirect`, `CashExpenseAdded`) schreiben
Schema-Version 2; Version 1 bleibt über eigene Decoder lesbar und liefert
`Approved = true`, kein Flag, kein Nachholen für den Bestand. `recordComdirectPush` und
`addCashExpense` setzen `Approved = false`, weil der Push kein `approved` sendet.

**Domain.** `Entry` trägt `Approved`, `FlagColor`, `FlagName`. `observe` vergleicht
Prüfvermerk und Flag als eigene Gruppen (Reihenfolge: Kopfdaten, Kategorisierung, Cleared,
Genehmigung, Flag) und erzeugt bei unverändertem Stand kein Ereignis.
`Ledger.Domain.needsReview` ist die reine Regel: `not Approved || Cleared = Uncleared`.

**Persistenz.** Drei neue Spalten, angelegt im `CREATE TABLE` und für bestehende Datenbanken
über `Ledger.Persistence.migrateLedgerEntryColumns`. Diese Funktion nimmt die Connection als
Parameter, wird von `initializeLedgerSchema` aufgerufen und vom Test gegen eine eigene,
isoliert aufgebaute alte Tabelle gefahren. Iteration 1 hatte die SQL-Anweisungen im
Testkörper kopiert, sodass ein Entfernen der echten Migration unbemerkt geblieben wäre;
der Worker hat die Korrektur per Sabotage-Probe belegt (Funktionskörper geleert, Test
scheiterte mit "no such column: approved", danach wiederhergestellt).

**Lesepfad.** `LedgerFlag` (`NoFlag | AskLena | Marked`) in `Shared.Domain`,
`LedgerEntryView` trägt `NeedsReview` und `Flag`. `getLedgerEntriesPage` reicht
`OnlyNeedsReview` durch und liefert `NeedsReviewCount` nach dem Muster von
`DeletedCount`/`IncludeDeleted` aus `ledger-p3n8k`. Die Historie bekommt sprechende Sätze.

**Anzeige.** Chip "Zu prüfen (n)" über der Liste in Konto- und Gesamtsicht, Punkt am linken
Zeilenrand am Handy, linker Rahmen am PC, Kennzeichen violett für *Rückfrage Lena* und
orange für *Markiert*. Die Reducer-Logik ist nach ADR 0020 geschnitten, der Proxy-Aufruf
liegt hinter `RunLoadEntriesPage`.

**Verifikation.** `dotnet test` Exit 0 mit 871 bestanden, 0 Fehlern, 7 übersprungen
(Integrationstests ohne `.env`), `npm run build` Exit 0. Der Verifier hat die Migration
gegen die Produktionsfunktion nachgeprüft und beide Screenshots selbst angesehen: am Handy
sitzt der Punkt in einer eigenen schmalen Spalte links vom Payee und drängt das Datum
nicht, weil dieses in der Zeile darunter steht; am PC trägt nur die ungeprüfte Zeile den
rosa linken Rahmen, die Kennzeichen erscheinen violett und orange passend zur Farbsemantik
des Styleguides, der Chip "Zu prüfen (1)" sitzt gefüllt über der Tabelle.

Die Screenshots entstanden gegen eine eigens aufgebaute Wegwerf-Datenbank in einem
temporären Verzeichnis, nie gegen die Entwicklungsdatenbank; deren Stand blieb unverändert.
Möglich wurde das erst, weil `scripts/screenshot-route.mjs` aus `design-system-wa6wz`
während dieses Laufs fertig wurde und in den Worktree gemergt werden konnte. In Iteration 1
war das Kriterium mangels Werkzeug und Testdaten nicht erfüllbar.

**Nebenbefund, als `ledger-p9k3w` im Backlog:** Bei Full-Page-Aufnahmen einer Route mit
scrollbarem Inhalt erscheint die inaktive BottomSheet-Leiste am unteren Bildrand, weil
`.bottom-sheet` sich per `top: calc(var(--vv-top, 0px) + var(--vvh, 100dvh))` positioniert
und Chromium die Seite für den Screenshot auf volle Scroll-Höhe vergrössert. Betrifft das
Werkzeug, nicht diese Aufgabe.

**Nicht in diesem Slice:** die übrige Filterleiste (`ledger-vgfw3`), die Statusspalte am PC
(`ledger-qns4j`), das Detail-Sheet (`ledger-q1hw2`), jede schreibende Geste
(`ledger-r777c`).
