---
id: ledger-awf5b
title: "Ein übernommener Betragsfilter mit Komma („12,50“) gilt nicht mehr als laufende Eingabe: isInputActive vergleicht die Betragsentwürfe als Wert statt als Text, der Update-Neustart beim Öffnen wird nicht mehr zurückgehalten (src/Client/State.fs isInputActive)"
status: doing
type: bug
context: ledger
created: 2026-11-19
completed:
depends_on: []
blocks: []
tags: [bestandsreview, frontend, tests]
related_adrs: [0057, 0095]
related_research: []
prior_art: [infrastructure-bnbdc]
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G3 (Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`), Teil 4 von 4.
Teil 1 ist ledger-y8xrr (Liste behält Stand nach eigenen Aktionen), Teil 2 ledger-pfhdq (späte und gescheiterte
Antworten), Teil 3 ledger-pz028 (Aufräumen). Unabhängig von den anderen Teilen.

`isInputActive` (`src/Client/State.fs:385`) entscheidet nach ADR 0057, ob eine neue Version sofort lädt oder nur den
Hinweis „Neue Version verfügbar.“ zeigt. Es vergleicht die Betragsentwürfe als Text mit `formatAmountDraft` des
Filters (389, 403, 404). Roman tippt Komma: „12,50“ wird als 12,50 übernommen, `formatAmountDraft` liefert „12.50“,
der Vergleich sagt „ungleich“. Solange der Betragsfilter steht, gilt das als laufende Eingabe, und die App lädt eine
neue Version beim Zurückholen nie von selbst.

## What
- In `isInputActive` vergleichen die beiden Betragsfelder über `parseAmountFilterText` (Types.fs:131) statt als Text:
  leer gegen keine Grenze ist gleich, ein lesbarer Wert gegen dieselbe Grenze ist gleich (Dezimalvergleich, 12,5 = 12,50),
  ein unlesbarer Text („abc“) gilt als laufende Eingabe (er steht als Feldfehler am Feld). Eine reine Hilfsfunktion
  in `Components/Ledger/Types.fs` neben `amountFilterValue` (etwa `amountDraftMatches`), `isInputActive` ruft sie.
- Die Suche (`SearchDraft`) vergleicht weiter als Text; dort gibt es keine Umformung.

## Acceptance criteria
- [ ] Regressionstest, vor dem Fix rot (Expecto, `src/Tests/PwaTests.fs` in der Liste „State.isInputActive“): GIVEN
  Kassenbuch mit `Filters.AmountMin = Some 12.5m` und `AmountMinDraft = "12,50"`, THEN `isInputActive` ist falsch.
  Derselbe Fall für `AmountMaxDraft`, dazu „12,5“ gegen 12.5m und „ 12,50 “ (Leerzeichen).
- [ ] Regressionstest, vor dem Fix rot, über das echte Root-`update`: GIVEN Betragsfilter übernommen über
  `LedgerMsg (SetAmountMinDraft "12,50")` und danach das `CommitAmountFilter` mit derselben Version, WHEN
  `PwaUpdateWaiting` (Vorbild der Tests um `PwaTests.fs:161`), THEN wird `Pwa.Reloading` wahr und es steht kein
  Hinweis-Toast.
- [ ] Intakt (Expecto, `PwaTests.fs`): ein abweichender Wert („5“ bei Filter 12,5) und ein unlesbarer Text („abc“) bei
  gesetzter Grenze gelten als laufende Eingabe; leerer Entwurf ohne Grenze nicht. Die bestehenden Fälle
  „Kassenbuch: AmountMinDraft weicht ab“ und „AmountMaxDraft weicht ab“ bleiben grün.
- [ ] Die Hilfsfunktion bekommt eigene Fälle für jede Regel oben (leer, gleich mit Komma und Punkt, anderer Wert,
  unlesbar), Expecto in `src/Tests/LedgerFilterButtonTests.fs` oder bei den Tests zu `amountFilterValue`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteil (geprüft an HEAD f98a8ce6):
- Befund 5 (`src/Client/State.fs:403`): bestätigt, Zeilen 389, 403, 404. `parseAmountFilterText` liest „12,50“ als
  12.50m, `formatAmountDraft 12.50m` ergibt „12.50“; jede Eingabe mit Komma bleibt dadurch „ungleich“. Der bestehende
  Test `PwaTests.fs:255` baut den Entwurf mit `formatAmountDraft` und trifft den Fall deshalb nicht.
- Kein sichtbarer UI-Teil, deshalb ohne `design-system-001-styleguide`.
