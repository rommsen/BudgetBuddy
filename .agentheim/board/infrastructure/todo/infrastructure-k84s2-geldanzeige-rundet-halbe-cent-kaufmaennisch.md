---
id: infrastructure-k84s2
title: "Geldanzeige und Vorbelegung runden halbe Cent kaufmännisch über roundHalfUp"
status: todo
type: bug
context: infrastructure
created: 2026-11-07
completed:
depends_on: []
blocks: []
tags: [client, money, rundung, fable, bestandsreview, budget, ledger]
related_adrs: []
related_research: []
prior_art: [budgeting-q4v8n, budgeting-c4ve4]
---

## Why
Bestandsreview budgeting vom 2026-11-07, Gruppe 3, und der Rundungsbefund aus Bestandsreview ledger vom 2026-11-06, Gruppe 3 (`.agentheim/knowledge/reviews/2026-11-07-budgeting.md`, `.agentheim/knowledge/reviews/2026-11-06-ledger.md`). Roman hat am 2026-11-07 entschieden, beides als eine Task aufzunehmen. Die übrigen Befunde aus ledger Gruppe 3 stehen in ledger-vq2g8.

Was heute passiert: `Money.formatAmountText` (`src/Client/DesignSystem/Money.fs:126`) rechnet die Cent mit `System.Math.Round(abs amount * 100m, 0)`, `formatAmountForInput` (`src/Client/Types.fs:385`) mit `System.Math.Round(abs amount * 100m)`. Beide runden einen genauen halben Cent zur geraden Zahl. Beispiel mit erfundenen Werten: ein berechneter Betrag von 12,345 € steht als 12,34 €, 12,355 € als 12,36 €. Betroffen sind alle Beträge der Budget-Seite (`Month.fs:290`, `MonthStart.fs:79`, `Moves.fs:357` und `:473`, `TargetsView.fs:25`), die Kontostände im Kassenbuch und die Vorbelegung von Eingabefeldern (`AmountMath.fs:123`): wer einen vorbelegten Betrag bestätigt, schickt den abgerundeten Wert weiter. Gebuchte Beträge aus YNAB haben volle Cent und sind nicht betroffen, berechnete (Durchschnitte, Wochenbeträge, Anteile) schon.

**Korrektur am Review:** Beide Reviews sagen, der Fehler zeige sich nur im Fable-Kompilat. Das stimmt nicht. Auch unter .NET rundet `Math.Round` auf decimal ohne Modus zur geraden Zahl (`MidpointRounding.ToEven` ist der Standard). Am 2026-11-07 mit `dotnet fsi` geprüft: `Math.Round(1234.5m, 0)` ergibt 1234, `Math.Round(abs 12.345m * 100m, 0)` ergibt 1234. Der Regressionstest läuft deshalb unter .NET und ist dort vor dem Fix rot.

## What
Am Code vom 2026-11-07 (main 7423fb0) geprüft.

1. `formatAmountText` (`Money.fs:126`): `totalCents` über `Shared.Domain.roundHalfUp (abs amount * 100m)` (`src/Shared/Domain.fs:23`), wie `.claude/rules/fable-pitfalls.md` es für Code im Client vorschreibt. Der Kommentar darüber (`Money.fs:118` bis `:125`) nennt den kaufmännischen Weg.
2. `formatAmountForInput` (`Types.fs:385`): dieselbe Rechnung.
3. `SplitEditor.amountText` (`src/Client/Components/Ledger/SplitEditor.fs:45`): heute `ToString("0.00", InvariantCulture)`, ein eigener Weg neben dem Cent-Weg (ledger Gruppe 3, niedrig). Er nutzt `formatAmountForInput`; leer bei 0 bleibt.
4. `.claude/rules/fable-pitfalls.md`, Abschnitt "Math.Round auf decimal": ergänzt, dass `Math.Round` auf decimal ohne Modus auch unter .NET zur geraden Zahl rundet, und eine Grep-Zeile `grep -rn 'Math.Round' src/Client/` unter "Grep-Checks".

## Acceptance criteria
- [ ] Kaputt, Domain-Ebene unter .NET (`src/Tests/MoneyFormatTests.fs`), vor dem Fix rot: `formatAmountText true true 12.345m "EUR"` ergibt "+12,35 €", `formatAmountText false true -12.345m "EUR"` ergibt "-12,35 €", `formatAmountText false true 0.005m "EUR"` ergibt "0,01 €". Der Kommentar nennt den Bug (halber Cent zur geraden Zahl gerundet, Bestandsreviews budgeting Gruppe 3 und ledger Gruppe 3).
- [ ] Kaputt (`src/Tests/QuickAddTests.fs`, Liste `formatAmountForInput`), vor dem Fix rot: `formatAmountForInput 12.345m` ergibt "12,35", `formatAmountForInput 0.005m` ergibt "0,01".
- [ ] Intakt: 12,355 € steht als "12,36 €"; volle Cent, Tausenderpunkte, Millionen, negative Beträge, `ShowSign` und `ShowCurrency` wie bisher (bestehende Tests in `MoneyFormatTests.fs` und `QuickAddTests.fs` grün). Knapp unter dem halben Cent (12,3449 €) bleibt "12,34 €".
- [ ] `SplitEditor.amountText` (`src/Tests/SplitEditorTests.fs` oder `LedgerSplitEditorClientTests.fs`, je nachdem, wo der Editor heute getestet wird): 12,345 ergibt "12,35", 0 ergibt "", ein negativer Betrag erscheint ohne Vorzeichen. Bestehende Tests grün.
- [ ] Kein neuer `[fable]`-Test, Begründung im Outcome: der Fehler ist unter .NET rot (Doktrin Punkt 1 und 5: was unter .NET prüfbar ist, bekommt keinen Browser-Test). Dass `roundHalfUp` im Fable-Kompilat gleich rechnet, belegt schon der `[fable]`-Test "Ein wöchentlicher Vorschlag steht auf vollen Euro, ein Ø auf 50 Cent wird aufgerundet" (`scripts/target-check.test.mjs:390`, über `wholeEuroText`); der Rest von `formatAmountText` ist Ganzzahlrechnung auf `int64`.
- [ ] `grep -rn 'Math.Round' src/Client/` findet nach der Änderung nichts mehr. Die Grep-Zeile steht in `.claude/rules/fable-pitfalls.md`, der Abschnitt "Math.Round auf decimal" nennt das Verhalten unter .NET.
- [ ] Bestehende Tests, die einen zur geraden Zahl gerundeten Wert erwarten, sind angepasst und im Outcome einzeln genannt (je Datei und Testname), mit dem Hinweis, dass sie den Bug festschrieben. Browser-Wächter mit festen Beträgen (`scripts/*.test.mjs`) bleiben grün oder stehen ebenso in der Liste.
- [ ] Keine Änderung an Typen in `src/Shared`, keine Fixture-Falle.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Nicht dazu
- Kontostand in den Settings per `sprintf "%s (%.2f %s)"` (`src/Client/Components/Settings/View.fs:297`): ledger-vq2g8 stellt ihn auf `Money.formatAmountText` und bekommt die Rundung von hier mit.
- `formatAmountForEdit` im Import (`src/Client/Components/SyncFlow/Types.fs:103`): Punkt als Dezimalzeichen mit Absicht, anderer Kontext (banking-import), kein Befund im Review.
- Milliunits: `toMilliunits` (`src/Shared/Budgeting.fs:228`, läuft nur unter .NET), `manualTransactionMilliunits` und die Milliunits beim Import (`src/Shared/Domain.fs:775`, `:816`). Sie rechnen aus vollen Cent und treffen keinen halben Milliunit.
- Ein Strukturwächter gegen `Math.Round` im Client: die Grep-Zeile in der Rule reicht (Doktrin Punkt 7, kein Wächter für eine gerade gefixte Stelle).

## Notes
- `Shared.Budgeting.euroText` (`src/Shared/Budgeting.fs:635`) rechnet schon über `roundHalfUp`. Nach dem Fix zeigen Budget-Texte aus Shared und die DS-Anzeige denselben Cent.
- Die Rundung betrifft auch das Kassenbuch (Kontostände, Beträge in Listen). ledger-vq2g8 verweist für diesen Teil auf diese Task.
- Keine echten Beträge in Tests, Doku oder Commits.
