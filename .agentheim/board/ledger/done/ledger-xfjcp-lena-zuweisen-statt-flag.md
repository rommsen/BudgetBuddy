---
id: ledger-xfjcp
title: "Lena fragen im Buchungs-Detail: setzt das YNAB-Flag, das Roman Lena genannt hat (Farbe aus den Settings), mit Kennzeichen, Filter-Chip, Zurücknehmen und Rückschreibe-Zustand"
status: done
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [design-system-001, ledger-q1hw2, ledger-k2rbn]
blocks: []
tags: [frontend, backend, kassenbuch, flags, v2, lena]
related_adrs: [0028, 0032, 0050, 0053, 0055, 0056, 0061]
related_research: []
prior_art: [ledger-r777c, ledger-q1hw2, ledger-nhwrt]
---

## Why
Roman setzt in YNAB ein Flag, das er "Lena" genannt hat, für Buchungen, zu denen er
nichts findet (UX-Session `ledger-xbrht`, 2026-09-12). Eigentlich will er "Lena fragen",
das Flag ist nur die Umsetzung. Lena nutzt YNAB über den Familien-Account und sieht die
Flags dort. Roman hat am 2026-10-24 die erste Scheibe festgelegt: Zuweisen, Kennzeichen,
Filter. Sicht "Für mich" und Benachrichtigung bleiben eigene Entwürfe.

## What
Heute schon gebaut (nicht Teil dieser Task): der Spiegel liest `flag_color`/`flag_name`,
`ReadModel.flagOf` deutet den Namen "Lena" als `AskLena`, das Kennzeichen "Rückfrage Lena"
steht in der Zeile (PC: "Lena?", Handy: voller Wortlaut) und im Detail, die Historie kennt
"Flag „Lena“ in YNAB gesetzt". Neu ist der Schreibweg und die Bedienung. Entscheidungen
in ADR 0056.

1. **Einstellung:** In den Settings wählt Roman die YNAB-Flag-Farbe, die er "Lena"
   genannt hat (`lena_flag_color`, sechs Farben). Vorbelegung: Farbe der jüngsten
   gespiegelten Buchung mit Flag-Name "Lena". Die Einstellung wird mit den übrigen
   Settings gespeichert.
2. **Aktion im Detail** (ADR 0053 Punkt 1): eine Zeile "Lena fragen" an der Flag-Stelle des
   Details. Hat die Buchung kein Flag oder ein anderes, steht dort "Lena fragen"; ist sie
   *Rückfrage Lena*, steht dort "Rückfrage zurücknehmen". Ohne gesetzte Farbe ist die
   Aktion deaktiviert mit dem Grund. In YNAB gelöschte Buchungen: Aktion aus.
3. **Befehle:** `setLenaFlag` (zuweisen oder zurücknehmen) und `restoreFlag`
   (Vorzustand). Antwort `FlagOutcome` mit `Previous: { Color; Name }`,
   `Result: Changed | NoChange`, `WriteBack: Sent | Failed of reason`. Ein Ereignis
   `EntryFlagChangedInBb` mit Alt/Neu je Befehl, danach der Push
   `PUT .../transactions/{tid}` mit nur `flag_color` (Wert oder `null`).
4. **Rückgängig** im Toast stellt Farbe und Name des Vorzustands her (auch ein anderes
   Flag wie *Markiert*); der Push sendet nur die Farbe.
5. **Rückschreibe-Zustand:** `EntryFlagWriteBackFailed`/`EntryFlagWrittenBackToYnab`,
   `FlagWriteBackFailure` an `LedgerEntryView`. Das Detail zeigt in der Hinweisleiste
   "Flag nicht in YNAB" mit Grund und "Erneut senden", getrennt von "Prüfvermerk nicht
   in YNAB".
6. **Filter:** Chip "Rückfrage Lena (n)" in der Filterleiste, Muster von "Zu prüfen (n)":
   `OnlyAskLena` in `LedgerEntryPageQuery` und `LedgerPayeeSuggestionQuery`,
   `AskLenaCount` in `LedgerEntryPage`. Und-Verknüpfung mit den übrigen Filtern; der
   Treffer prüft `flag_name` (getrimmt, ohne Groß-/Kleinschreibung) wie `flagOf`.
7. **Historie:** "Lena gefragt", "Rückfrage Lena zurückgenommen", "Flag in BB geändert"
   (anderes Flag als Vorzustand), Herkunft "In BB geändert". Eine Beobachtung aus YNAB
   danach erscheint als eigene Zeile (YNAB gewinnt beim nächsten Spiegel).
8. **Nicht in dieser Task:** der Leisten-Einstieg für viele Buchungen (Entwurf
   `ledger-y54gr`), die Sicht "Für mich" (`ledger-ayjzq`), die Benachrichtigung (`ledger-g47hp`).

## Acceptance criteria
- [ ] Domain-Test: `setLenaFlag` auf einer Buchung ohne Flag schreibt ein
      `EntryFlagChangedInBb` mit Alt = keins, Neu = konfigurierte Farbe und Name "Lena";
      `flagOf` deutet den Zustand danach als `AskLena`.
- [ ] Domain-Test: Zuweisen auf einer Buchung, die schon `AskLena` ist, und Zurücknehmen
      auf einer ohne Flag erzeugen weder Ereignis noch Push (`NoChange`).
- [ ] Domain-Test: Zuweisen auf einer Buchung mit anderem Flag (*Markiert*) überschreibt;
      `restoreFlag` mit dem Vorzustand aus der Antwort stellt Farbe und Name wieder her.
- [ ] Domain-Test: ohne gesetzte `lena_flag_color` lehnt der Befehl ab, es entsteht kein
      Ereignis; eine in YNAB gelöschte Buchung wird abgelehnt.
- [ ] Domain-Test der Reihenfolge: das Ereignis steht im Log, bevor der Push läuft; ein
      erfolgreicher Push erzeugt kein weiteres Ereignis.
- [ ] Domain-Test Fehlschlag: scheitert der Push (Netz, 4xx, 429) oder fehlt die
      YNAB-Verknüpfung, folgt `EntryFlagWriteBackFailed` mit Grund, der Flag-Stand in BB
      bleibt; `retryFlagWriteBack` mit Erfolg schreibt `EntryFlagWrittenBackToYnab`; ohne
      offenen Fehlschlag tut er nichts.
- [ ] Spiegel-Test: sieht ein Lauf bei offenem Fehlschlag YNAB auf dem BB-Stand, endet der
      Zustand (Actor `YnabMirror`); liefert YNAB ein anderes Flag, gilt YNABs Stand, der
      Fehlschlag endet, die Beobachtung steht in der Historie.
- [ ] YnabClient-Test: der Body enthält nur `flag_color` (`"purple"` bzw. `null`), weder
      `flag_name` noch `approved`/`cleared`; die Route ist
      `PUT /budgets/{id}/transactions/{tid}`; Fehlerzuordnung wie `sendReviewMark`.
- [ ] Ereignis-Test: Codec Roundtrip für die drei neuen Ereignistypen, Namens-Abbildung und
      Schema-Version 1.
- [ ] Abfrage-Test: `OnlyAskLena` liefert nur Buchungen mit Flag-Name "Lena" (getrimmt,
      ohne Groß-/Kleinschreibung), nicht die mit anderem Flag oder nur Farbe;
      `AskLenaCount` zählt unabhängig vom Chip; Und-Verknüpfung mit Konto und
      "Zu prüfen".
- [ ] Reducer-Test: Chip "Rückfrage Lena" kippt `OnlyAskLena` und lädt ab Seite 0 neu; eine
      Buchung, die bei aktivem Chip zurückgenommen wird, bleibt ausgegraut in der Liste
      stehen mit dem Hinweis, dass sie nicht mehr zum Filter passt (Tafel 5.8, Roman
      2026-10-25), nach dem Muster der Kategorie (`applyFilterEffect`, `filteredOutNote`,
      sfab0/w29y6); erst das nächste Laden nimmt sie heraus.
- [ ] Ansichts-Test: das Detail zeigt "Lena fragen" bzw. "Rückfrage zurücknehmen", ist ohne
      Farbe deaktiviert mit Grund, zeigt "Flag nicht in YNAB" mit "Erneut senden" bei
      gesetztem `FlagWriteBackFailure`; Zeile und Detail zeigen das Kennzeichen "Rückfrage
      Lena".
- [ ] Settings-Test: die Farbe wird gespeichert und gelesen, die Vorbelegung nimmt die
      Farbe der jüngsten "Lena"-Buchung.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.
- [ ] Wortlaut, Platz der Zeile im Detail und die Farbe des Hinweises passen zum Styleguide
      und zu den übrigen Detail-Zeilen. [human-eye]
- [ ] Domain-Test (Baseline, ADR 0056 Nachtrag): nach gescheitertem Push trifft eine
      Beobachtung mit dem alten Flag (Baseline) ein; Flag "Lena" und Fehlschlag bleiben in BB
      stehen. Eine Beobachtung mit dem BB-Stand beendet den Fehlschlag, eine mit einem
      dritten Flag gewinnt.
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; ein neuer DS-Baustein erscheint in
      `/styleguide` (`src/Client/Components/Styleguide/View.fs`); `npm run guard:tokens` grün
      (Wächter aus `design-system-wfhxj`, sobald integriert).
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Notes
Annahme für den Bau: die YNAB-OpenAPI kennt beim Schreiben nur `flag_color`; der Worker
prüft das gegen die aktuelle OpenAPI, bevor er den Body fixiert (Abweichung: Task zurück
nach backlog mit Notiz).

Dateien, die ein Worker anfasst: `src/Shared/Domain.fs`, `src/Shared/Api.fs`,
`src/Server/Ledger/Events.fs`, `Domain.fs`, `CommandHandlers.fs`, `QueryHandlers.fs`,
`Persistence.fs`, `src/Server/YnabClient.fs`, `src/Server/Api.fs`,
`src/Client/Components/Ledger/{Types,State,View}.fs`, `src/Client/Components/Settings/*`,
Tests. Überschneidung mit `ledger-k2rbn` (Kategorie ändern, parallele ADRs 0054/0055):
dieselben Dateien, dieselbe Hinweisleiste im Detail. Nicht gleichzeitig in zwei
Worktrees bauen, oder die Hinweisleiste vorher vereinheitlichen. Ein gemeinsamer
Mechanismus "Feld nach YNAB schreiben" (Hinweis je Feld, Erneut senden, Zustandsereignisse)
ist naheliegend, aber nicht Voraussetzung dieser Task (ADR 0056, Punkt 7).

Verfeinert am 2026-10-24 (Sammel-Modeling): Ausgangslage im Code geprüft (Spiegel und
Kennzeichen vorhanden), Schreibweg und Zuordnung in ADR 0056, Acceptance criteria
ergänzt, Leisten-Einstieg und spätere Iterationen als Entwürfe abgespalten.

Captured via `quick-capture` on 2026-09-12. Der Prototyp aus `ledger-xbrht` zeigt Punkt 1
als "Lena fragen" (Brief 5 in `docs/ux/kassenbuch/briefs.md`).

Abgleich mit ADR 0055 (Conductor, 2026-10-24): der Rückschreibe-Fehlschlag trägt eine
Baseline wie beim Kategorie-Schreibweg (ADR 0056, Nachtrag). Deshalb hängt die Task an
`ledger-k2rbn`: sie übernimmt dessen Baseline-Regel in `observe` als Muster, und beide
fassen dieselben Dateien an (`Ledger/Events.fs`, `CommandHandlers.fs`, `YnabClient.fs`,
Client Ledger), was eine gleichzeitige Arbeit in zwei Worktrees ohnehin ausschließt.

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).

## Outcome

Lena fragen im Kassenbuch-Detail nach ADR 0056 (mit Baseline-Nachtrag) ist gebaut, von der Einstellung bis zum Filter.

**Server.** Drei neue Ereignisse (`EntryFlagChangedInBb`, `EntryFlagWriteBackFailed` mit `BaselineFlagColor/Name`, `EntryFlagWrittenBackToYnab`, Schema-Version 1) in `Ledger/Events.fs` mit Codec, `apply`-Fällen und `Entry.FlagWriteBackFailure` (Spalte `flag_write_back_failure`, Migration in `Ledger/Persistence.fs`). `observe` übernimmt die Baseline-Regel der Kategorie: gleich BB-Stand beendet den Fehlschlag (Actor YnabMirror), gleich Baseline lässt ihn offen, anderer Stand gewinnt. Befehle `setLenaFlag`, `restoreFlag`, `retryFlagWriteBack` in `Ledger/CommandHandlers.fs` (Ereignis zuerst, Push danach, unter `WriteGuard`). `Ynab.buildFlagBody/sendFlag/updateFlag` senden nur `flag_color` (Wert oder null) per `PUT .../transactions/{tid}`. Abfragen: `OnlyAskLena` in `LedgerEntryPageQuery` und `LedgerPayeeSuggestionQuery`, `AskLenaCount` in `LedgerEntryPage`, Historien-Sätze "Lena gefragt", "Rückfrage Lena zurückgenommen", "Flag in BB geändert", "Flag nicht in YNAB: ...", "Flag in YNAB angekommen". Einstellung `lena_flag_color` über `SettingsApi.getLenaFlagSettings/saveLenaFlagColor` (Vorschlag aus der jüngsten gespiegelten Lena-Buchung).

**Client.** Flag-Zeile im Detail (`flagField`, neuer DS-Baustein `Detail.fieldActionButton` und `fieldLockHint`, in `/styleguide` gezeigt), Hinweisleiste `FlagNotice`, Chip `AskLenaChip` mit `Filters.OnlyAskLena` und `AskLenaCount`, Reducer-Fälle in `Ledger/State.fs` (zurückgenommene Buchung bleibt bei aktivem Chip ausgegraut über `applyFilterEffect`), Toast `UndoFlag`, Settings-Karte "Kassenbuch: Lena fragen".

**Hinweise.** OpenAPI konnte ich nicht online prüfen; der Body folgt ADR 0056 (nur `flag_color`), `flag_name` und `approved` gehen nie hinaus (Test). Abweichung von ADR 0056 Punkt 2 im Detail: `WithdrawLena` auf einem fremden Flag ist NoChange (ADR 0061). Bestehende Tests, Fixtures und Playwright-Mocks erhielten die neuen Pflichtfelder (`FlagWriteBackFailure`, `OnlyAskLena`, `AskLenaCount`, `ChipActivity.AskLena`); die Chip-Reihenfolge-Erwartungen und die Chip-Zahl (8) wurden angepasst. Die [human-eye]-Kriterien (Optik) sind nicht bestätigt, nur über Design-System-Bausteine gebaut.

**Läufe.** `dotnet build` 0 Warnungen, `dotnet test` 2127 bestanden, 7 übersprungen, 0 Fehler (vorher 2064 gesamt, 70 neue .NET-Tests), `npm run build` grün, `npm run guard:classes` grün, `node --test --test-concurrency=1 scripts/*.test.mjs` 221 von 221 grün (davon 5 neue in `scripts/ledger-lena.test.mjs`).

Wichtige Dateien: `/Users/romansachse/src/BudgetBuddy/.worktrees/ledger-xfjcp/src/Server/Ledger/CommandHandlers.fs`, `/Users/romansachse/src/BudgetBuddy/.worktrees/ledger-xfjcp/src/Server/Ledger/Events.fs`, `/Users/romansachse/src/BudgetBuddy/.worktrees/ledger-xfjcp/src/Client/Components/Ledger/State.fs`, `/Users/romansachse/src/BudgetBuddy/.worktrees/ledger-xfjcp/src/Tests/LedgerLenaFlagTests.fs`, `/Users/romansachse/src/BudgetBuddy/.worktrees/ledger-xfjcp/src/Tests/LedgerLenaFlagClientTests.fs`.

### README von Hand

Im Abschnitt `## Ereignisse` hinter dem Bullet `- \`EntryCategorizationWrittenBackToYnab\`: ...` (endet mit `Historien-Satz: "Kategorie in YNAB angekommen".`) und vor `- \`EntryReplacedInYnab\`` einfügen (neue Zeilen, kein alter Satz ändert sich):

- `EntryFlagChangedInBb`: das Flag wurde in BB geändert (ADR 0056 Punkt 2). Payload `OldFlagColor`, `OldFlagName`, `NewFlagColor`, `NewFlagName` (roh). Ein Typ für "Lena fragen", "Rückfrage zurücknehmen" und "Rückgängig". Schema-Version 1, Actor `UserInBb`. Setzt `Entry.FlagColor/FlagName` und leert `FlagWriteBackFailure`. Historien-Sätze: "Lena gefragt", "Rückfrage Lena zurückgenommen", sonst "Flag in BB geändert".
- `EntryFlagWriteBackFailed`: das Zurückschreiben des Flags nach YNAB ist gescheitert (ADR 0056 Punkt 5 und Nachtrag). Payload `Reason`, `BaselineFlagColor`, `BaselineFlagName` (die `Old*`-Werte der ersten gescheiterten Änderung; bei einem schon offenen Fehlschlag bleibt die erste Baseline). Schema-Version 1, Actor `UserInBb`. Setzt `Entry.FlagWriteBackFailure`. Historien-Satz: "Flag nicht in YNAB: <Grund>".
- `EntryFlagWrittenBackToYnab`: der offene Flag-Fehlschlag ist beendet. Kein Payload, Schema-Version 1. Actor `UserInBb` nach Erfolg bei offenem Fehlschlag oder "Erneut senden", Actor `YnabMirror`, wenn der Spiegel YNAB auf dem BB-Stand sieht. Historien-Satz: "Flag in YNAB angekommen".
