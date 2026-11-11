---
id: ledger-fxt09
title: "Payee einer Buchung in BB ändern: Befehl mit Ereignis und Schreibweg nach YNAB, im Buchungs-Detail und auf der Karte beim Durchgehen, Rückgängig im Toast"
status: done
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

## Outcome

Payee ändern läuft jetzt als Kassenbuch-Befehl und im Buchungs-Detail. `./verify.sh` endet mit `VERIFY: PASS` (dotnet 4812 grün, 15 übersprungen nur in den Listen „(requires .env)“, node 315 grün).

**Server**
- `Ledger.CommandHandlers.setEntryPayee` / `retryPayeeWriteBack` (`src/Server/Ledger/CommandHandlers.fs`), API `LedgerApi.setEntryPayee` / `retryPayeeWriteBack` (`src/Shared/Api.fs`, `src/Server/Api.fs`, Port `payeePush`). Unter `WriteGuard.exclusive`, erst `EntryPayeeChangedInBb`, dann der Push, bei Fehlschlag `EntryPayeeWriteBackFailed` mit Baseline, kein Rollback. Ein Spiegel-Auslöser nach dem Befehl gibt es wie beim Memo nicht.
- Ereignisse `EntryPayeeChangedInBb` (OldPayee optional, NewPayee), `EntryPayeeWriteBackFailed` (Reason, BaselinePayee), `EntryPayeeWrittenBackToYnab` (ohne Payload), alle Version 1 (`src/Server/Ledger/Events.fs`). Die Namen folgen der Task, kein Abgleich mit dem tactical-modeler nötig. `Entry.PayeeWriteBackFailure` (Spalte `payee_write_back_failure`), `LedgerEntryView.PayeeWriteBackFailure`.
- Regeln in `src/Shared/Domain.fs`: `normalizePayee` (getrimmt, leer und über 200 abgelehnt), `payeeKey` (getrimmt, ohne Groß-/Kleinschreibung, Grundlage für `PayeeUnchanged`), `payeeLockReason` (gelöscht, nicht mit YNAB verknüpft, Übertrag; abgeglichen bleibt änderbar). Server und Client lesen dieselben Funktionen. Prüfung an der API-Grenze: `Server.Validation.validatePayeeChange` (fehlende Buchung und Payee-Fehler gesammelt).
- **Grenze 200 Zeichen:** geprüft gegen YNABs OpenAPI (`https://api.ynab.com/papi/open_api_spec.yaml`, SaveTransaction `payee_name: maxLength: 200`, 2026-11-11).
- Push: `YnabClient.buildPayeeBody` / `sendPayee` / `updatePayee`, Rumpf genau `{"transaction":{"payee_id": null, "payee_name": "…"}}`.
- Spiegel (`Ledger.Domain.observe`): Baseline-Regel wie beim Memo. BB-Stand ergibt `EntryPayeeWrittenBackToYnab` (Actor `YnabMirror`) ohne `EntryDetailsChangeObservedInYnab`. Baseline lässt Payee und Fehlschlag stehen. Ein dritter Payee (auch „REWE“ statt „Rewe“) gilt. `EntryRestoredInBb` beendet einen offenen Payee-Fehlschlag wie die anderen.
- Historie: „Payee geändert auf <neu>, vorher <alt>“, „Payee nicht in YNAB: <Grund>“, „Payee in YNAB angekommen“.

**Probelauf gegen das Budget „Test“** (`src/Tests/YnabIntegrationTests.fs`, „Probelauf: der PUT des Payees ändert nur den Payee (Budget Test)“, Liste „(requires .env)“). Zweimal live ausgeführt: dafür stand `RUN_INTEGRATION_TESTS=true` kurz in der `.env` des Worktrees, danach wurde sie per Prüfsumme bestätigt wiederhergestellt. Ergebnis:
- Der PUT ändert `payee_name` und `payee_id`. Kategorie, Betrag, `import_id`, `cleared` und `approved` bleiben unverändert (Test grün).
- YNAB gleicht **weder Schreibweise noch Leerzeichen an**. „PROBELAUF FXT09 … NEU“ hat neben „Probelauf fxt09 … Neu“ einen neuen Payee mit eigener `payee_id` angelegt, „  Probelauf … Neu  “ ebenfalls, mit den Leerzeichen. Deshalb trimmt BB vor dem Senden. Eine Buchung, deren Payee sich nur in Groß-/Kleinschreibung unterscheidet, gilt in BB als unverändert (`PayeeUnchanged`). Eine reine Korrektur der Schreibweise geht in BB also nicht, nur in YNAB.
- Jeder Lauf hinterlässt im Budget „Test“ drei Probe-Payees. Die API kann Payees nicht löschen, die Buchung selbst wird gelöscht.

**Client**
- DS-Komponente `src/Client/DesignSystem/PayeePicker.fs` (neue Variante, auf `/styleguide` als „Payee-Auswahl“). Props: Payee, Vorschläge, Feldfehler, Rückrufe `OnPick`/`OnClose`; die Karte in `ledger-v6s7c` kann sie ohne Detail öffnen. Reine Bausteine `options` und `keyAction`. Suche vorbelegt mit dem heutigen Payee, „„<Eingabe>“ übernehmen“ als erste Zeile bei neuer Eingabe, Klick übernimmt sofort, Zeilen halten den Fokus im Suchfeld (`onMouseDown preventDefault`), am PC Fokus und Markierung in der Suche, ↑↓/Enter/Esc nach §7.9.
- Detail (`src/Client/Components/Ledger/View.fs`): die Payee-Zeile ist klickbar und trägt rechts den Pfeil `›` (`Detail.forwardArrow`, `detail-payee-arrow`, gesperrt mit Grund im Tooltip). Der Filter-Knopf „alle zeigen“ bleibt. Am Handy ersetzt die Auswahl den Inhalt des Detail-Sheets, am PC mit Panel ein `Modal`. Hinweisleiste `PayeeNotice` („Payee nicht in YNAB“, Erneut senden) nach allen anderen Leisten.
- Reducer (`State.fs`, `Types.fs`): `OpenPayeeEdit`, `StartSavePayee` (Feldfehler bei leer oder zu lang, ebenso eine Ablehnung durch den Server), Toast „Payee geändert · <neu>“ mit Rückgängig (`UndoPayee` → derselbe Befehl mit dem alten Payee), `StartRetryPayee`. Die Payees kommen aus `getLedgerPayeeSuggestions` ohne Filter (`payeeChoicesQuery`, über `Retry.reading`). Esc schließt erst die Auswahl, der Fokus kehrt auf `detail-payee-arrow` zurück.

**Tests**
- `src/Tests/LedgerPayeeTests.fs`: Regeln, Validierung (alle Fehler gesammelt), Codec/Version/Replay mit allen drei Ereignissen, Befehle mit `PayeeProbe` (jede Ablehnungsregel und Grenze der Vorlage Memo, abgeglichen änderbar, Reihenfolge, Rückgängig, Fehlschlag, Baseline, Erneut senden), Comdirect-Import (import_id und Reference bleiben, zweiter Abruf derselben Reference: `AlreadyImported`, keine zweite Buchung, Payee bleibt der geänderte), Schreib-Sperre, Spiegel mit Baseline, Historie, Encoder und Fake-Handler.
- `src/Tests/LedgerPayeeClientTests.fs`: Auswahl-Zeilen und Tasten, Sperre, Schritt, Hinweis, Reducer samt Rückgängig über denselben Befehl.
- `RemotingEndpointNegativeTests.fs`: Zeilen für `setEntryPayee` (leer, unbekannt) und `retryPayeeWriteBack` (unbekannt). `RetryTests.fs`: Inventar um den zweiten `getLedgerPayeeSuggestions`-Aufruf ergänzt.
- Browser: `scripts/ledger-payee.test.mjs` mit `[flow:payee-aendern]` (412 px, Touch: die Auswahl ersetzt das Sheet, kein Autofokus, Tippen und Übernehmen lösen keinen Blur aus, Toast, Rückgängig) und `[fokus]` am PC bei 1440 px (Fokus in der Suche, Esc zurück auf den Pfeil der Payee-Zeile). Bei 1280 px zeigt BB das Detail als Sheet, kein Panel, deshalb läuft der Fokus-Test bei 1440. Gegenprobe: ohne das Zurücksetzen des Fokus wird `[fokus]` rot (lokal geprüft, danach wiederhergestellt). Flow `payee-aendern` in `FLOWS`. Sweep-Zustände `kassenbuch-payee-auswahl` (412) und `kassenbuch-payee-auswahl-pc` (1280 als Sheet, 1440 als Dialog), beide grün.
- Fixtures: `PayeeWriteBackFailure: null` in `buildEntryFixture` (`scripts/ledger-filter-bar.mjs`) und `scripts/ledger-status-column.mjs`, das Feld in allen `LedgerEntryView`- und `Entry`-Literalen der Tests.

**Nebenwirkung Build:** Das App-Bündel ist über 2 MiB gewachsen (2,12 MB), und vite-plugin-pwa bricht den Build dann ab. `vite.config.js` setzt `workbox.maximumFileSizeToCacheInBytes` auf 4 MiB. Die parallelen Tasks dieser Welle stoßen wahrscheinlich auf dieselbe Grenze. Beim Merge darf der Eintrag nur einmal stehen.

**Neben der Skizze verglichen:** Die Skizze (Prototyp Version 17) hat in der Payee-Zeile nur „alle zeigen“. Payee ändern ist die in der Task genannte Abweichung (BB kann es heute im Sync). Sonst ist das Detail unverändert. Screenshots:
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/ledger-fxt09/bb-detail-handy-412.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/ledger-fxt09/bb-payee-auswahl-handy-412.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/ledger-fxt09/bb-detail-pc-1440.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/ledger-fxt09/bb-payee-auswahl-pc-1440.png`

Offen: das Kriterium [human-eye] (Roman sieht Payee ändern am Handy und am PC an).

**README-Delta:** Die Abschnitte `## Ereignisse` (endet mit Fließtext) und `## Key commands` (Bullets ohne fetten Kopf) erreicht das Delta-Gate nur per append bzw. gar nicht per replace. Die Einzelheiten der drei Ereignisse stehen deshalb im Bullet unter `## Key events`. Der Conductor kann drei Bullets in `## Ereignisse` nach `EntryAmountWrittenBackToYnab` von Hand ergänzen. Im Bullet *BuchungÄndern* (v2) unter Key commands kann er „der Payee auch (*PayeeÄndern*, ledger-fxt09)“ ergänzen. Der Satz „BuchungGeändert — Kategorie, Split, Memo, Payee geändert“ stimmt jetzt ohne Änderung.

**Für die Kann-Sammlung ledger:**
- Das App-Bündel teilen (Code-Splitting), statt die Precache-Grenze weiter anzuheben.
- Probe-Payees im Budget „Test“ sammeln sich je Lauf des Probelaufs an.
- Eine reine Korrektur der Schreibweise des Payees ist in BB nicht möglich (`payeeKey` ohne Groß-/Kleinschreibung), nur in YNAB.
