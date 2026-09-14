---
id: ledger-kdnnz
title: "Spiegel erweitern: Prüfvermerk (approved und cleared) und Flags beobachten, Chip \"Zu prüfen\" und Kennzeichen \"Rückfrage Lena\" anzeigen"
status: todo
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
