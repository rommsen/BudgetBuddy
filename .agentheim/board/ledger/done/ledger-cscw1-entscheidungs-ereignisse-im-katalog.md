---
id: ledger-cscw1
title: "Ereignis-Katalog: Entscheidungs-Ereignisse für Comdirect-Import, Regel, Korrektur, Quick Add und YNAB-Verknüpfung"
status: done
type: feature
context: ledger
created: 2026-09-11
completed: 2026-09-11
depends_on: []
blocks: [ledger-qvt84, ledger-tdnpk]
tags: [ledger, events, foundation]
related_adrs: [0006, 0013, 0015, 0019, 0021]
related_research: []
prior_art: [ledger-e3k7p]
---

## Why
Der Ereignis-Katalog (`ledger/README.md`, "Ereignisse") reserviert die Namen
`EntryImportedFromComdirect`, `EntryCategorizedByRule`, `EntryRecategorized` und
`CashExpenseAdded` bereits, aber ohne Payload (`ReservedDecisionEventName` in
`src/Server/Ledger/Events.fs`). Ohne Payloads kann das Kassenbuch keine BB-Entscheidung
aufnehmen, und genau das verlangt das v1-Kriterium der Vision ("merkt sich zusätzlich, was
BB selbst entschieden hat"). ADR 0021 verlangt zusätzlich ein eigenes
Verknüpfungs-Ereignis `EntryLinkedToYnab`, das bisher nicht im Katalog steht.

## What
1. Fünf Ereignistypen in `LedgerEventBody` aufnehmen (Schema-Version 1; `ValuationRecorded`
   bleibt reserviert und ist nicht Teil dieses Tasks):
   - `EntryImportedFromComdirect { AccountId: LedgerAccountId; ImportId: string;
     ComdirectReference: string; Date: DateTime; Amount: Money; Payee: string option;
     Memo: string option; Cleared: ClearedState }`. Trägt bewusst keine Kategorie: jede
     Buchung entsteht kategorielos, die Kategorisierung folgt als eigenes Ereignis in
     derselben Sequenz.
   - `EntryCategorizedByRule { RuleName: string; Category: CategoryRef option;
     Splits: TransactionSplit list; PayeeOverride: string option }`. Actor `Rule RuleId`,
     nur auf einer noch unkategorisierten Buchung zulässig.
   - `EntryRecategorized { OldCategory/NewCategory: CategoryRef option;
     OldSplits/NewSplits: TransactionSplit list; OldPayee/NewPayee: string option }`.
     Actor `UserInBb`.
   - `CashExpenseAdded { AccountId: LedgerAccountId; Date: DateTime; Amount: Money;
     Payee: string option; Memo: string option; Category: CategoryRef option;
     Cleared: ClearedState }`. Actor `QuickAdd`.
   - `EntryLinkedToYnab { YnabTransactionId: string }`. Entsteht aus einer Push-Antwort
     (Actor `ComdirectImport` bzw. `QuickAdd`) oder wenn der Spiegel eine unverknüpfte
     Buchung über die ImportId findet (Actor `YnabMirror`).
2. `apply` in `Ledger/Domain.fs` erweitern. Invarianten:
   - Genau eines der drei Entstehungs-Ereignisse (`EntryObservedInYnab`,
     `EntryImportedFromComdirect`, `CashExpenseAdded`) darf die erste Zeile einer Buchung
     sein; ein zweites Entstehungs-Ereignis auf derselben Buchung schlägt fehl. Heute
     überschreibt `apply` bei `EntryObservedInYnab` still, das wird mit behoben.
   - Kategorie und Splits schließen sich aus, die `mkSplits`-Invarianten gelten auch hier.
   - `EntryLinkedToYnab` setzt die `ynab`-Verknüpfung. Ob ein Aufruf für eine bereits
     verknüpfte Buchung ein Fehler ist oder still idempotent bleibt, entscheidet der
     Aufrufer (`appendAndApply`-Fassade), nicht `apply`.
3. Codecs in `Ledger.Events.Codec` nach bestehendem Muster.
4. `ledger/README.md`, Abschnitt "Ereignisse": Unterabschnitt "Entstehung" und die fünf
   Ereignisse mit Payload-Kurzbeschreibung ergänzen. Textvorschlag:

   > **Entstehung:** Eine Buchung entsteht mit genau einem von `EntryObservedInYnab`,
   > `EntryImportedFromComdirect`, `CashExpenseAdded`.
   >
   > **Entscheidungen** (BB hat etwas festgelegt):
   > - `EntryImportedFromComdirect`: Comdirect-Buchung beim Push angelegt, ohne Kategorie.
   > - `EntryCategorizedByRule`: eine Regel hat kategorisiert; die RuleId steht im Actor.
   > - `EntryRecategorized`: Roman hat in BB kategorisiert oder umkategorisiert.
   > - `CashExpenseAdded`: Quick-Add-Buchung angelegt, vor dem Push.
   > - `ValuationRecorded`: weiterhin reserviert.
   >
   > **Verknüpfung:** `EntryLinkedToYnab`: YNAB-Id bekannt geworden, aus der Push-Antwort
   > oder vom Spiegel über die ImportId.
5. Replay-Test (Pflicht nach ADR 0013) für alle neuen Ereignistypen.

## Acceptance criteria
- [x] Alle fünf Ereignistypen sind in `Events.fs`, `Domain.fs` (`apply`) und im Codec
      implementiert.
- [x] Der Replay-Test rekonstruiert den Zustand für Buchungen, die mit jeder der drei
      Entstehungsarten beginnen und danach kategorisiert und verknüpft werden.
- [x] Zwei Entstehungs-Ereignisse auf derselben Buchungs-Id schlagen fehl (Test).
- [x] `ledger/README.md`, Abschnitt "Ereignisse", ist aktualisiert.
- [x] `dotnet build` und `dotnet test` grün.

## Notes
Reine Fundament-Arbeit; das Verdrahten in den Push-Fluss übernehmen `ledger-qvt84`
(Import) und `ledger-tdnpk` (Quick Add). Payload-Entwurf vom tactical-modeler
(2026-09-11), Reihenfolge und `EntryLinkedToYnab` aus ADR 0021. Kann parallel zu
`categorization-gdrx8` laufen.

## Outcome
Die fünf Ereignistypen sind exakt nach dem Payload-Entwurf in `LedgerEventBody`
aufgenommen (Schema-Version 1 je Typ): `EntryImportedFromComdirect`,
`EntryCategorizedByRule`, `EntryRecategorized`, `CashExpenseAdded`, `EntryLinkedToYnab`.
`ReservedDecisionEventName` trägt jetzt nur noch `ValuationRecorded`.

**`apply` (`src/Server/Ledger/Domain.fs`):** ein neuer `isCreationEvent`-Filter erkennt die
drei Entstehungs-Ereignisse (`EntryObservedInYnab`, `EntryImportedFromComdirect`,
`CashExpenseAdded`); trifft eines davon auf eine schon existierende Buchung, schlägt
`apply` laut fehl, bevor der jeweilige Ereignis-Zweig überhaupt läuft. Das behebt den im
Task beschriebenen stillen Überschreib-Bug bei einer zweiten `EntryObservedInYnab`.
`EntryCategorizedByRule` prüft zusätzlich, dass die Buchung noch unkategorisiert ist
(Category `None` und Splits leer), sonst ebenfalls ein lauter Fehler.
`EntryCategorizedByRule` und `EntryRecategorized` teilen sich eine kleine
`requireCategoryXorSplits`-Prüfung (Kategorie und Splits schließen sich aus, ADR
0006/0019). `EntryLinkedToYnab` ändert `Entry` selbst nicht (die YNAB-Id lebt in der
Fremd-Id-Tabelle, ADR 0015), verlangt aber eine vorangehende Buchung. `linksFrom` stiftet
bei `EntryImportedFromComdirect` die ImportId- und Comdirect-Verknüpfung, bei
`EntryLinkedToYnab` die YNAB-Verknüpfung; `CashExpenseAdded` stiftet keine (Quick Add hat
keine ImportId, ADR 0004).

**Mirror-Semantik unangetastet:** `observe` (`Ledger/Domain.fs`) erzeugt `EntryObservedInYnab`
ohnehin nur im `None`-Zweig (unbekannte Buchung); eine erneute Beobachtung einer
existierenden Buchung läuft immer über die Change-Ereignisse. Die neue Invariante in
`apply` bricht diesen Pfad also nicht; `LedgerMirrorTests.fs` (681 Zeilen, unverändert)
blieb komplett grün.

**Codecs** (`Ledger.Events.Codec`) für alle fünf Payloads nach bestehendem
Handschrift-Muster (kein Auto-Codec), `encodePayload`/`decodePayload` erweitert.

**QueryHandlers.fs:** die Erweiterung von `LedgerEventBody` machte `ReadModel.describe`
(Historien-Satz je Ereignis) unvollständig; fünf neue, kurze deutsche Sätze ergänzt
(z. B. „Comdirect-Import angelegt: …“, „Regel „…“ hat kategorisiert: …“), sonst hätte
`dotnet build` mit einer Warnung (FS0025) nicht mehr grün gewesen sein können.

**Tests** (`src/Tests/LedgerTests.fs`, 14 neue `testCase`, alle vorher rot geschrieben
und beobachtet): Entstehung ohne Kategorie (Comdirect) bzw. mit optionaler Kategorie
(Quick Add), Regel kategorisiert eine unkategorisierte Buchung, PayeeOverride, Regel
schlägt auf bereits kategorisierter Buchung fehl, Kategorie/Splits-XOR bei der Regel,
Recategorized übernimmt neue Werte, `EntryLinkedToYnab` lässt `Entry` unverändert und
verlangt eine bestehende Buchung, die Zwei-Entstehungs-Invariante über alle neun
Typ-Paarungen (verschachtelte Schleife über die drei Entstehungsarten), `linksFrom` für
Comdirect-Import/Verknüpfung/Quick-Add, sowie zwei Persistence-Tests: der Pflicht-Replay-
Test über alle drei Entstehungsarten (kategorisiert und verknüpft, vergleicht
`replayAll()` gegen `loadAllEntries()`) und ein Rollback-Test, der zeigt, dass
`appendAndApply` ein zweites Entstehungs-Ereignis vollständig zurücknimmt (kein Log-Eintrag
danach). Die bestehenden Typname/Schema-Version- und JSON-Roundtrip-Tests wurden um die
fünf neuen Payloads erweitert.

**README** (`ledger/README.md`, Abschnitt "Ereignisse"): "Entstehung" und "Verknüpfung"
als eigene Absätze ergänzt, "Entscheidungen" von reinen Namen auf Payload-Kurzbeschreibung
je Ereignis umgestellt (Textvorschlag aus dem Task übernommen und um die
Kategorie/Splits-Ausschluss-Notiz ergänzt).

**Verifikation:** `dotnet build BudgetBuddy.sln` grün, 0 Warnungen. `dotnet test`
(`src/Tests/Tests.fsproj`) grün: 736 bestanden, 7 übersprungen (externe Integrationstests,
unverändert), 0 Fehler.

**Werkzeug-Hinweis:** Serena-MCP-Tools standen in dieser Session nicht zur Verfügung (nur
Read/Write/Edit/Bash/Agent), deshalb wurden die `.fs`-Dateien mit den gewöhnlichen
Werkzeugen bearbeitet, wie schon in `ledger-e3k7p`.

### Nachtrag Iteration 2: mkSplits-Invariante nachgezogen
Der Verifier hat zu Recht bemängelt, dass `requireCategoryXorSplits` nur die eine Hälfte
der im Task geforderten Invariante prüfte (Kategorie und Splits schließen sich aus), nicht
aber die zweite Hälfte ("die `mkSplits`-Invarianten gelten auch hier"). Gesetzte Splits bei
`EntryCategorizedByRule` (`p.Splits`) und `EntryRecategorized` (`p.NewSplits`) liefen ohne
Prüfung auf Zeilenzahl, Summe oder Währung durch.

**Fix** (`src/Server/Ledger/Domain.fs`): `requireCategoryXorSplits` bekommt zusätzlich den
Buchungsbetrag (`Money`) und ruft bei nicht-leeren Splits `Shared.Domain.mkSplits total
splits` auf; bei `Error` scheitert `apply` laut mit `invalidOp` (Fehlertext trägt den
`SplitError`). Beide Aufrufstellen übergeben `e.Amount` (die schon aufgelöste Buchung aus
`requireExisting()`), da eine Split-Summe gegen den *aktuellen* Buchungsbetrag geprüft
werden muss, nicht gegen einen Feldwert aus dem Payload selbst.

**Tests** (`src/Tests/LedgerTests.fs`, 5 neue `testCase`, rot geschrieben und beobachtet:
vor dem Fix testweise die `mkSplits`-Prüfung entfernt, die vier negativen Fälle liefen rot,
danach wieder hergestellt und grün): je Ereignis (`EntryCategorizedByRule`,
`EntryRecategorized`) ein Fall mit falscher Summe (`splitLines` summiert auf -43,10 EUR
gegen eine Buchung über -17,40 EUR bzw. -42,50 EUR) und ein Fall mit nur einer Split-Zeile
(`TooFewLines`), dazu ein positiver Fall, der zeigt, dass strukturell gültige Splits
weiterhin übernommen werden.

**README** (`ledger/README.md`, Abschnitt "Ereignisse"): der Satz zu Kategorie/Splits nennt
jetzt explizit die `mkSplits`-Regeln (mindestens zwei Zeilen, vorzeichenrichtige Summe,
eine Währung) und dass `apply` sie über `mkSplits` prüft.

**Verifikation nach dem Fix:** `dotnet build BudgetBuddy.sln` grün, 0 Warnungen. `dotnet
test` grün: 736 bestanden (5 mehr als Iteration 1), 7 übersprungen, 0 Fehler.

## Diary entry

## 2026-09-11 12:50 - Ereignis-Katalog: Entscheidungs-Ereignisse im Kassenbuch

**What I did:**
Fünf Ereignistypen für BBs eigene Entscheidungen (`EntryImportedFromComdirect`,
`EntryCategorizedByRule`, `EntryRecategorized`, `CashExpenseAdded`) und die
YNAB-Verknüpfung (`EntryLinkedToYnab`, ADR 0021) im Kassenbuch-Ereignis-Katalog ergänzt,
inklusive `apply`-Invarianten (genau eine Entstehung je Buchung, Kategorie/Splits
schließen sich aus, Regel nur auf unkategorisierter Buchung) und JSON-Codecs.

**Files Added:**
- keine (nur bestehende Ledger-Dateien erweitert)

**Files Modified:**
- `src/Server/Ledger/Events.fs` - fünf neue Payload-Records, `LedgerEventBody` erweitert,
  `ReservedDecisionEventName` auf `ValuationRecorded` reduziert, Typname/Schema-Version
  und Codecs ergänzt.
- `src/Server/Ledger/Domain.fs` - `apply` um die fünf neuen Ereignis-Zweige und die
  Entstehungs-/Kategorie-Invarianten erweitert, `linksFrom` um Comdirect-Import- und
  YNAB-Verknüpfung ergänzt.
- `src/Server/Ledger/QueryHandlers.fs` - `ReadModel.describe` um fünf deutsche
  Historien-Sätze für die neuen Ereignisse ergänzt (sonst unvollständiges Pattern-Match).
- `src/Tests/LedgerTests.fs` - 14 neue Testfälle plus Erweiterung zweier bestehender
  (Typname/Schema-Version, JSON-Roundtrip).
- `.agentheim/contexts/ledger/README.md` - Abschnitt "Ereignisse": "Entstehung" und
  "Verknüpfung" ergänzt, "Entscheidungen" mit Payload-Kurzbeschreibung statt nur Namen.

**Files Deleted:**
- keine

**Rationale:**
Ohne Payloads konnte das Kassenbuch keine BB-Entscheidung aufnehmen; das v1-Vision-
Kriterium verlangt genau das. ADR 0021 verlangt zusätzlich ein eigenes
Verknüpfungs-Ereignis, damit Replay die YNAB-Id-Zuordnung aus dem Log herleiten kann.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 731 bestanden, 7 übersprungen (extern), 0 Fehler (`dotnet test`
  `src/Tests/Tests.fsproj`).
- Issues: keine. Die Mirror-Tests (`LedgerMirrorTests.fs`) blieben unverändert grün, weil
  `observe` eine zweite `EntryObservedInYnab` ohnehin nie erzeugt.

---

## 2026-09-11 13:20 - Nachtrag: mkSplits-Invariante für EntryCategorizedByRule/EntryRecategorized

**What I did:**
Verifier-Fund aus Iteration 1 behoben: `apply` prüfte bei `EntryCategorizedByRule` und
`EntryRecategorized` bisher nur, dass Kategorie und Splits sich ausschließen, nicht aber
die `mkSplits`-Invarianten (mindestens zwei Zeilen, vorzeichenrichtige Summe gleich dem
Buchungsbetrag, eine Währung). Beide Zweige rufen jetzt `Shared.Domain.mkSplits` auf und
lehnen einen Verstoß laut ab.

**Files Added:**
- keine

**Files Modified:**
- `src/Server/Ledger/Domain.fs` - `requireCategoryXorSplits` um den Buchungsbetrag und
  einen `mkSplits`-Aufruf bei gesetzten Splits erweitert; beide Aufrufstellen
  (`EntryCategorizedByRule`, `EntryRecategorized`) übergeben `e.Amount`.
- `src/Tests/LedgerTests.fs` - fünf neue Testfälle (Summe falsch und einzeilig je
  Ereignis, plus ein positiver Fall für gültige Splits), rot beobachtet vor dem Fix.
- `.agentheim/contexts/ledger/README.md` - Satz zu Kategorie/Splits nennt jetzt die
  `mkSplits`-Regeln namentlich.

**Files Deleted:**
- keine

**Rationale:**
Der Task verlangt ausdrücklich, dass die `mkSplits`-Invarianten auch für die
Entscheidungs-Ereignisse gelten (ADR 0006/0019); ohne die Prüfung hätte `apply` eine
Buchung mit strukturell ungültigen Splits (falsche Summe, nur eine Zeile) klaglos
übernommen.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 736 bestanden (5 mehr als Iteration 1), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`).
- Issues: keine. Vor dem Fix testweise die `mkSplits`-Prüfung entfernt: genau die vier
  negativen neuen Tests liefen rot, danach wiederhergestellt und grün.

---

## Verifier note (iteration 1)

**REASONS:**
- Kriterium 1 ("Alle fünf Ereignistypen sind in Events.fs, Domain.fs (`apply`) und im Codec implementiert") ist nur teilweise erfüllt. `## What` Punkt 2 nennt als Invariante von `apply`: "Kategorie und Splits schließen sich aus, die `mkSplits`-Invarianten gelten auch hier." Umgesetzt ist nur die erste Hälfte: `requireCategoryXorSplits` (src/Server/Ledger/Domain.fs, etwa Zeilen 111 bis 116) prüft nur "Kategorie und Splits gleichzeitig gesetzt". Die Zweige `EntryCategorizedByRule` und `EntryRecategorized` (etwa Zeilen 193 bis 211) prüfen die `mkSplits`-Regeln nie (mindestens 2 Zeilen, vorzeichenbehaftete Summe gleich Buchungsbetrag, eine Währung). `Shared.Domain.mkSplits` (src/Shared/Domain.fs:161) existiert und ist direkt aufrufbar. So wie der Code steht, nimmt `apply` z. B. `EntryCategorizedByRule { Category = None; Splits = splitLines }` auf der Comdirect-Buchung über -17,40 EUR an, obwohl die Zeilen -43,10 EUR ergeben, und ebenso einen Split mit nur einer Zeile. Das `## Outcome` des Workers nennt nur den gegenseitigen Ausschluss.
- Kein Test deckt diese Invariante ab. Das README (Abschnitt "Ereignisse", Zeilen 183 bis 184) nennt ebenfalls nur den Ausschluss, passend zur Lücke im Code.
- Nicht blockierend: die fünf neuen Historien-Sätze in `ReadModel.describe` (src/Server/Ledger/QueryHandlers.fs, `describeImportedFromComdirect` bis `describeLinkedToYnab`) sind reine Funktionen ohne Tests. Die Änderung selbst ist nötig und im Scope (sonst unvollständiger Match, FS0025). Übrige Checks ohne Befund: Build 0 Warnungen, 731 bestanden, 7 übersprungen, 0 fehlgeschlagen; `LedgerMirrorTests` unverändert grün; kein INDEX oder Protokoll angefasst; `## Diary entry` vorhanden.

**SUGGESTED_FIX:** In `apply` bei gesetzten Splits (`EntryCategorizedByRule`: `p.Splits`, `EntryRecategorized`: `p.NewSplits`) mit `mkSplits e.Amount splits` prüfen und bei `Error` laut mit `invalidOp` scheitern. Je Ereignis einen roten Test ergänzen (Summe passt nicht, nur eine Zeile) und im README-Abschnitt "Ereignisse" die `mkSplits`-Regel im Satz zu Kategorie und Splits nennen.

**ITERATION_HINT:** likely-fixable
