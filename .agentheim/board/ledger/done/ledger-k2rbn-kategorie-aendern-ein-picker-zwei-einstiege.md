

## Verifier note (iteration 1)

**REASONS:**
- Check 5 (README-Abgleich): der Diff fügt die Ereignisse `EntryCategorizationWriteBackFailed` und `EntryCategorizationWrittenBackToYnab` hinzu (Events.fs: `eventTypeName`, `schemaVersion`, Codec). Das `readmeDelta` nennt sie nur unter `## Key events`. Der Abschnitt `## Ereignisse` der ledger-README ist der verbindliche Katalog ("Änderungen hier, nicht im Code allein"); seine Liste `**Entscheidungen**` führt das direkte Gegenstück `EntryPushToYnabFailed` (Payload, Version, Actor, gesetzter und aufgelöster Zustand, Historien-Satz), für die zwei neuen Ereignisse fehlt der Eintrag.
- Derselbe Abschnitt: der Bullet `EntryCategorizationChangeObservedInYnab` unter `**Beobachtungen**` sagt weiter "Entsteht nur, wenn sich die Kategorie-Identität ändert". Bei offenem Fehlschlag gibt `observe` das Ereignis nicht aus, wenn YNAB die Baseline zeigt, und `apply` beendet bei diesem Ereignis `CategorizationWriteBackFailure`.
- Nicht blockierend: ADR 0055 Punkt 2 sagt, ein erfolgreicher Push schreibt kein weiteres Ereignis; `pushCategory` schreibt bei offenem Fehlschlag zusätzlich `EntryCategorizationWrittenBackToYnab` (Actor `UserInBb`). Passt zu Punkt 4; im Katalog-Eintrag gleich benennen.
- Alle übrigen Prüfungen bestanden: Kriterien je Test belegt (38 Fälle), `dotnet build` 0 Warnungen, `dotnet test` 1704/7 übersprungen/0 Fehler, `npm run build`, `guard:classes`, `test:scripts` 155/157 mit 2 Ladetimeouts, Einzel-Wiederholung grün; Remoting kompatibel; Schreibregel-grep ohne Treffer.

**SUGGESTED_FIX:** Katalog `## Ereignisse` ergänzen: zwei Bullets unter `**Entscheidungen**` neben `EntryPushToYnabFailed`, und den Bullet `EntryCategorizationChangeObservedInYnab` um die Ausnahme bei offenem Fehlschlag (ADR 0055 Punkt 5) erweitern. Kein Code.

**ITERATION_HINT:** likely-fixable

## Outcome

Der Server-Teil von "Kategorie ändern" (ADR 0055) ist umgesetzt, im Muster von `changeReviewMark`/`retryReviewMarkWriteBack` aus ledger-r777c. Keine Client-Änderung nötig (der Client kompiliert unverändert, das neue Feld liegt nur am Server und im Vertrag).

**Vertrag** (`src/Shared/Domain.fs`, `src/Shared/Api.fs`): `CategoryChoice` (Id und Name), `CategoryChange`, `CategoryWriteBack`, `CategoryChangeOutcome`; `LedgerApi.setEntryCategory: Guid -> YnabCategoryId option -> Async<Result<CategoryChangeOutcome, string>>` und `retryCategorizationWriteBack`; `LedgerEntryView.CategorizationWriteBackFailure: string option`. Eine Antwort eines älteren Servers ohne das Feld ist für den Client `None`/fehlend; eine alte installierte PWA ignoriert das zusätzliche Feld in der Antwort. Das Feld kommt nirgends in einer Anfrage vor, ein Null-Fallback für eingehende Daten ist deshalb nicht nötig.

**Domäne und Log** (`src/Server/Ledger/Events.fs`, `Domain.fs`): Ereignisse `EntryCategorizationWriteBackFailed` (Reason, Baseline-Kategorie, Baseline-Splits) und `EntryCategorizationWrittenBackToYnab`, beide Version 1 mit Codec und Namens-Abbildung. `Entry.CategorizationWriteBackFailure`. `apply` behält bei offenem Fehlschlag die erste Baseline und ersetzt nur den Grund; `EntryRecategorized` allein beendet ihn nicht; `EntryCategorizationChangeObservedInYnab` beendet ihn. `observe` mit offenem Fehlschlag: gleich BB-Stand gibt `WrittenBackToYnab` (Actor `YnabMirror`), gleich Baseline gibt kein Ereignis, sonst das Beobachtungs-Ereignis. Bei Überschneidung (Baseline gleich BB-Stand) gilt "angekommen". Ohne offenen Fehlschlag bleibt YNAB-gewinnt unverändert.

**Persistenz** (`Persistence.fs`): neue Projektionsspalte `categorization_write_back_failure` (JSON über den Payload-Codec, additive `ALTER TABLE`-Migration, NULL heißt nichts offen).

**Push** (`src/Server/YnabClient.fs`): `buildCategoryBody` (nur `category_id`, `null` für ohne Kategorie), `sendCategory` (injizierbarer `HttpClient`, PUT `.../transactions/{id}`, Fehlerzuordnung wie `sendReviewMark`), `updateCategory`. Der Sammel-PATCH `updateCategoryBatch` aus ADR 0055 Punkt 3 gehört zur Sammelaktion (ledger-w29y6) und ist hier nicht gebaut.

**Befehle** (`src/Server/Ledger/CommandHandlers.fs`): `setEntryCategory`, `retryCategorizationWriteBack`, `describeCategoryPushError`, `CategoryPush`. Beide laufen unter `WriteGuard.exclusive` (neues internes SemaphoreSlim), das auch `runMirrorGuarded` um den Lauf legt (Einzel-Flug bleibt erhalten). Ein Erfolg bei offenem früheren Fehlschlag schreibt zusätzlich `WrittenBackToYnab` (Actor `UserInBb`), weil der gesendete Stand dann in YNAB steht; ohne offenen Fehlschlag erzeugt ein Erfolg kein Ereignis. `retry` lehnt eine Buchung mit Split-Zeilen ab, damit nie versehentlich `null` gesendet wird. Kategorien werden gegen `ledger_categories` geprüft (Name daraus). `Server/Api.fs` verdrahtet beides über `categoryPush ()`.

**Lesepfad** (`QueryHandlers.fs`): `CategorizationWriteBackFailure` in der Sicht, Historie-Sätze "Kategorie nicht in YNAB: <Grund>" und "Kategorie in YNAB angekommen"; `describeRecategorized` unverändert.

**Tests** (`src/Tests/LedgerCategoryChangeTests.fs`, 38 neue Fälle, in `Tests.fsproj` mitten eingefügt): apply, Replay, Codec, observe mit offenem Fehlschlag (Baseline, BB-Stand, dritte Kategorie, Umbenennung, YNAB gewinnt ohne Fehlschlag), Lesemodell, Befehle gegen In-Memory-SQLite mit Fake-Push (Änderung vor Push, Rücknahme auch von und nach ohne Kategorie, Ziel gleich Ist, null, Fehlschlag Netz/400/429, nicht verknüpft, Ablehnungen, Retry, Projektions-Rundreise), Schreib-Sperre (der Test wird rot, wenn der Wächter in `runMirrorGuarded` fehlt; geprüft durch kurzes Zurückdrehen), PUT-Vertrag gegen Fake-Handler. In acht bestehenden Testdateien kam nur das neue Pflichtfeld `CategorizationWriteBackFailure = None` in die Records.

**Prüfläufe aus dem Worktree:** `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 1704 bestanden, 7 übersprungen, 0 Fehler (1711 gesamt); `npm run build` ok; `npm run guard:classes` ok; `npm run test:scripts` im zweiten Lauf 157/157 grün. Der erste Lauf hatte 12 Fehler in `ledger-detail.test.mjs` und `ledger-mirror-buttons.test.mjs` ("Route /ledger lädt nicht", 30 s Timeout) bei hoher Rechnerlast durch parallele Worker; beide Dateien einzeln (12/12) und der volle Lauf danach waren grün. `npm run guard:tokens` existiert in diesem Worktree noch nicht (design-system-wfhxj läuft parallel), es gibt keine UI-Änderung.

**README von Hand:** keine Prosa-Absätze betroffen. `npm run check:readme-delta` lief mit dem oben stehenden Delta ohne Ablehnung.

### README von Hand

Zwei Änderungen am Katalog `## Ereignisse` der ledger-README (kein README_DELTA, weil `append` am Abschnittsende landen würde).

**Änderung 1: zwei neue Bullets unter `**Entscheidungen**`, direkt nach dem Bullet `EntryPushToYnabFailed`.**

(a) Einfügen NACH dieser Zeile (letzte Zeile des Bullets, kommt genau einmal vor):

```
  `YnabMirror`. Historien-Satz: "Push nach YNAB gescheitert: <Grund>".
```

(b) Neuer Text, unmittelbar danach:

```
- `EntryCategorizationWriteBackFailed`: das Zurückschreiben der Kategorie nach YNAB ist
  gescheitert oder war nicht möglich (ADR 0055 Punkt 4, ledger-k2rbn). Payload: `Reason`
  (kurzer lesbarer Satz, ohne YNAB-Verknüpfung "noch nicht in YNAB verknüpft"),
  `BaselineCategory` und `BaselineSplits` (der Stand, den YNAB vermutlich noch hat).
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
```

**Änderung 2: Bullet `EntryCategorizationChangeObservedInYnab` unter `**Beobachtungen**` ersetzen.**

(a) Diese Zeile ersetzen (kommt genau einmal vor):

```
- `EntryCategorizationChangeObservedInYnab`: Kategorie oder Splits geändert (alt/neu). Entsteht nur, wenn sich die Kategorie-Identität ändert (`Ledger.Domain.categorizationIdentity`, ADR 0044): Kopf über die `CategoryId`, `ToCategory`-Ziele ohne Namen. Eine reine Umbenennung in YNAB ist keine Umkategorisierung und erzeugt kein Ereignis; die Payload trägt bei einem Ereignis weiter die vollen Namen.
```

(b) Neuer Text:

```
- `EntryCategorizationChangeObservedInYnab`: Kategorie oder Splits geändert (alt/neu). Entsteht nur, wenn sich die Kategorie-Identität ändert (`Ledger.Domain.categorizationIdentity`, ADR 0044): Kopf über die `CategoryId`, `ToCategory`-Ziele ohne Namen. Eine reine Umbenennung in YNAB ist keine Umkategorisierung und erzeugt kein Ereignis; die Payload trägt bei einem Ereignis weiter die vollen Namen. Ohne offenen Kategorie-Fehlschlag gewinnt YNAB jeden abweichenden Stand. Ausnahme bei offenem Fehlschlag (ADR 0055 Punkt 5): zeigt YNAB die Baseline, entsteht kein Ereignis und der Fehlschlag bleibt; zeigt YNAB den BB-Stand, entsteht `EntryCategorizationWrittenBackToYnab` (Actor `YnabMirror`); jeder andere Stand ist eine echte Änderung in YNAB, dann entsteht dieses Ereignis und `apply` beendet `Entry.CategorizationWriteBackFailure`.
```
