---
id: ledger-fxt09
title: "Payee einer Buchung in BB ändern: Befehl mit Ereignis und Schreibweg nach YNAB, im Buchungs-Detail und auf der Karte beim Durchgehen, Rückgängig im Toast"
status: backlog
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-v6s7c]
tags: [etappe-3, backend, frontend, kassenbuch]
related_adrs: [0021, 0062, 0083, 0089, 0093]
related_research: []
prior_art: []
---

## Why
Im Sync-Ablauf ändert Roman heute den Payee einer Importzeile (Payee-Override mit YNAB-Payees, `TransactionRow`,
`categorizeTransaction` mit `payeeOverride`). Mit `banking-import-d0fey` fällt die Prüfsitzung weg, die Buchung
steht sofort im Kassenbuch. Das Kassenbuch kann den Payee nicht ändern. Roman hat entschieden, dass das
Durchgehen alle heutigen Prüf-Funktionen behält (2026-11-11, Entscheidung 3); Payee ändern kommt deshalb als
Kassenbuch-Befehl und wirkt im Buchungs-Detail wie beim Durchgehen.

## What
Ein Befehl *PayeeÄndern* nach dem Muster Memo (ADR 0062): Ereignis zuerst (ADR 0021 Punkt 5), dann der Push,
bei Fehlschlag kein Rollback, Hinweisleiste mit „Erneut senden“. Dazu die Bedienung im Buchungs-Detail und eine
Komponente, die `ledger-v6s7c` auf der Karte wiederverwendet.

**Server**
- `Ledger.CommandHandlers.setEntryPayee` (eine Buchung, neuer Payee als Text), `retryPayeeWriteBack`, API
  `LedgerApi.setEntryPayee`, `LedgerApi.retryPayeeWriteBack`. Läuft unter der Schreib-Sperre
  (`WriteGuard.exclusive`), der Spiegel-Auslöser erst nach der Freigabe.
- Ereignisse (Geld-Entscheidung, ADR 0083 Abschnitt 7 und 9): `EntryPayeeChangedInBb` (Alt und Neu, Actor
  `UserInBb`, Version 1), `EntryPayeeWriteBackFailed` (Grund und Baseline-Payee), `EntryPayeeWrittenBackToYnab`
  (ohne Payload, Actor `UserInBb` oder `YnabMirror`). `Entry.PayeeWriteBackFailure` wie
  `Entry.MemoWriteBackFailure`. Die Namen bestätigt der Worker mit dem `tactical-modeler`, falls er abweicht.
- Push: `PUT .../transactions/{id}` mit `"payee_id": null` und `payee_name` (YNAB findet oder legt den Payee an).
  Vorbild ist `YnabClient.buildTransferBody`, Fall `BackToCategory` (ADR 0089). Nur `payee_name` ohne
  `payee_id: null` ist riskant, YNAB behielte vermutlich die alte `payee_id`. Ein Probelauf hinter
  `RUN_INTEGRATION_TESTS` gegen das Budget „Test“ (Muster `ledger-m7a9q`) belegt das und zeigt, ob YNAB
  Schreibweise oder Leerzeichen angleicht; das Ergebnis steht im Outcome. Der Payee wird getrimmt, leer ist
  ungültig, höchstens 200 Zeichen (YNAB-Grenze für `payee_name`, Worker prüft gegen die OpenAPI und nennt die Zahl
  im Outcome).
- Abgelehnt (Fehler, kein Ereignis, kein schreibender YNAB-Aufruf): unbekannte Buchung, in YNAB gelöscht, nicht mit
  YNAB verknüpft (Noch nicht in YNAB, auch eine wartende Buchung nach ADR 0093), ein Übertrag (der Payee ist dort
  die Übertrags-Payee, ADR 0089), leerer Payee. Ziel gleich Ist (ohne Groß-/Kleinschreibung getrimmt gleich):
  kein Ereignis, kein Push, `PayeeUnchanged`. Abgeglichene Buchungen (`Reconciled`) bleiben änderbar, YNAB erlaubt
  das für den Payee, und er ändert keinen Saldo (Roman, 2026-11-11: erlauben).
- Spiegel (`observe`, Baseline-Logik wie beim Memo): Sieht ein Lauf den BB-Stand, entsteht kein
  `EntryDetailsChangeObservedInYnab`, ein offener Fehlschlag endet mit `EntryPayeeWrittenBackToYnab` (Actor
  `YnabMirror`). Sieht er die Baseline, bleiben BB-Payee und Fehlschlag. Sieht er etwas Drittes (auch wenn YNAB
  „Rewe“ auf einen vorhandenen Payee „REWE“ abbildet), ist das eine echte Änderung in YNAB und gilt.
- Historie: „Payee geändert auf <neu>, vorher <alt>“ und „Payee nicht in YNAB: <Grund>“.
- Die `import_id` und die Comdirect-Reference bleiben unverändert (Invariante Importschlüssel). Regeln und
  Order-ID-Erkennung lesen weiter die Bankdaten, nicht den geänderten Payee.

**Client**
- Buchungs-Detail: die Feldzeile Payee bekommt rechts neben dem Wert einen Pfeil `›`; ein Klick auf den Wert
  öffnet die Payee-Auswahl. Der Filter-Knopf der Zeile („alle zeigen“, `FilterByRowPayee`) bleibt.
- Payee-Auswahl: am Handy ersetzt sie den Inhalt des Detail-Sheets (§7.15, kein zweites Sheet), Schließen führt
  zurück ins Detail; am PC ein Dialog (`Modal`, Auswahl nach §7.6). Inhalt: Suchfeld mit dem aktuellen Payee
  vorbelegt, darunter die Payees aus `getLedgerPayeeSuggestions` (häufigste zuerst), die zur Eingabe passen, und als
  erste Zeile „„<Eingabe>“ übernehmen“, wenn die Eingabe kein vorhandener Payee ist. Ein Klick übernimmt sofort
  (§7.1). Am PC steht der Fokus in der Suche, ↑↓ und Enter nach §7.9; am Handy kein Autofokus. Baustein: die
  vorhandene Auswahl (`Input.searchableSelect` oder `PickerParts`); passt keiner, eine neue DS-Variante auf
  `/styleguide`.
- Toast „Payee geändert · <neu>“ mit „Rückgängig“ (derselbe Befehl mit dem alten Payee, §7.13). Ein abgelehnter
  Payee (leer, zu lang) steht als Feldfehler unter dem Suchfeld (`ErrorDisplay.inline'`), nicht als Toast
  (`.claude/rules/task-ui-vorgaben.md`).
- Hinweisleiste „Payee nicht in YNAB“ mit Grund und „Erneut senden“, getrennt von den übrigen Hinweisleisten.
- Die Auswahl ist eine eigene Komponente mit Props (Payee, Buchungs-Id, Rückruf), damit `ledger-v6s7c` sie auf der
  Karte öffnet, ohne das Detail zu öffnen.

## Acceptance criteria
Szenarien, jedes mit Test auf der Heimat-Ebene (Befehls-Handler mit Ports als Funktionswerten nach dem Vorbild
`MemoProbe` in `src/Tests/LedgerMemoTests.fs`, Uhr als Parameter, eindeutige Ids; Client-`update` unter .NET).
Die Fälle der Vorlage Memo werden je Ablehnungsregel und Grenze nachgebildet (Doktrin Punkt 4).
- [ ] GIVEN eine verknüpfte Buchung „REWE SAGT DANKE 4711“ WHEN `setEntryPayee` „Rewe“ THEN zuerst
      `EntryPayeeChangedInBb` (Alt, Neu, Actor `UserInBb`), danach genau ein PUT mit `payee_id` null und
      `payee_name` = „Rewe“ (Encoder-Test auf den Rumpf), der gelesene Zustand trägt „Rewe“.
- [ ] GIVEN der PUT scheitert WHEN `setEntryPayee` THEN das Ereignis bleibt, dazu `EntryPayeeWriteBackFailed` mit
      Grund und Baseline, das Detail zeigt „Payee nicht in YNAB“; „Erneut senden“ schickt den BB-Stand und schreibt
      bei Erfolg `EntryPayeeWrittenBackToYnab`.
- [ ] GIVEN ein Übertrag, eine in YNAB gelöschte, eine nicht verknüpfte, eine wartende Buchung (ADR 0093) oder ein
      leerer Payee WHEN `setEntryPayee` THEN Fehler mit konkretem Satz, kein Ereignis, kein schreibender Aufruf.
- [ ] GIVEN derselbe Payee mit anderen Leerzeichen am Rand WHEN `setEntryPayee` THEN `PayeeUnchanged`, kein Ereignis,
      kein Push.
- [ ] GIVEN ein offener Fehlschlag WHEN der Spiegel YNAB auf dem BB-Stand sieht THEN `EntryPayeeWrittenBackToYnab`
      mit Actor `YnabMirror` und kein `EntryDetailsChangeObservedInYnab`; sieht er die Baseline, bleibt der
      Fehlschlag offen; sieht er einen dritten Payee, gilt `EntryDetailsChangeObservedInYnab` mit diesem.
- [ ] Probelauf gegen das Budget „Test“ hinter `RUN_INTEGRATION_TESTS` in der Liste „(requires .env)“: der PUT
      ändert den Payee und lässt Kategorie, Betrag, `import_id` und Prüfvermerk unverändert.
- [ ] GIVEN ein Comdirect-Import mit geändertem Payee WHEN der nächste Abruf dieselbe Reference liefert THEN keine
      zweite Buchung und der Payee in BB bleibt der geänderte.
- [ ] Rückgängig im Toast schickt den alten Payee über denselben Befehl (Client-`update`).
- [ ] Replay: Das Log einer Buchung mit allen drei neuen Ereignissen ergibt denselben Zustand (bestehender
      Replay-Test um die Typen erweitert).
- [ ] `src/Tests/RemotingEndpointNegativeTests.fs` hat je eine Zeile für `setEntryPayee` und `retryPayeeWriteBack`
      (unbekannte Id, ungültige Eingabe).
- [ ] Validierung an der API-Grenze (`src/Server/Validation.fs`): leer und über der Grenze, beide Fehler gesammelt.
- [ ] Neue Pflichtfelder in `LedgerEntryView` (`PayeeWriteBackFailure`) sind in den Fixtures von
      `scripts/*.test.mjs` nachgezogen (`scripts/ledger-filter-bar.mjs` `buildEntryFixture` und jede weitere
      Fundstelle).
- [ ] Browser-Tests: ein neuer Flow `payee-aendern` in `FLOWS` („Im Buchungs-Detail am Handy den Payee ändern und
      rückgängig machen.“) mit genau einem `[flow:payee-aendern]`-Test bei 412 px; ein `[fokus]`-Test am PC: beim
      Öffnen steht der Fokus in der Suche, Esc führt zurück ins Detail auf die Payee-Zeile. Der Sweep bekommt die
      Zustände „Payee-Auswahl“ am Handy (412 px) und am PC (1280 px).
- [ ] Eingabe mit Touch bei 412 px: die Tastatur bleibt beim Tippen in der Suche und beim Übernehmen offen, bis die
      Auswahl schließt.
- [ ] README ledger: Begriffe **Payee ändern** und **Payee nicht in YNAB**, die drei Ereignisse, Key command
      *PayeeÄndern*; der Satz „BuchungGeändert — Kategorie, Split, Memo, Payee geändert“ stimmt danach.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [ ] Roman hat das Ändern des Payees am Handy (412 px, Touch) und am PC angesehen. [human-eye]

## Notes
Abweichung vom Prototyp Version 17: Das Detail im Prototyp hat in der Payee-Zeile nur „alle zeigen“, Payee ändern
fehlt. Grund: BB kann es heute im Sync-Ablauf, das bleibt, bis Roman es streicht (CLAUDE.md, Ablauf).

Payee-Vorschläge kommen aus dem Kassenbuch (`getLedgerPayeeSuggestions`), nicht wie im Sync aus der YNAB-Payee-Liste
(`YnabApi.getPayees`). Grund: dieselbe Quelle wie Filter und Erfassen, kein YNAB-Aufruf beim Öffnen. Eine Folge:
ein Payee, den es nur in YNAB gibt, aber in keiner Buchung im Kassenbuch, erscheint nicht als Vorschlag, lässt sich
aber eintippen.

Aufteilen, Kategorie und Memo ändern den Payee nicht. Eine Regel aus der Buchung (`categorization-n3sxw`) liest den
Bank-Payee.
