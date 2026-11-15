---
id: ledger-pkrwd
title: "Payee einer Buchung leeren: im Detail den Payee entfernen, nach YNAB ohne Payee zurückschreiben"
status: todo
type: feature
context: ledger
created: 2026-11-15
completed:
depends_on: []
blocks: []
tags: [etappe-3, kassenbuch, payee, ynab-schreibweg]
related_adrs: [0021, 0062, 0083]
related_research: []
prior_art: [ledger-fxt09]
---

## Why
Roman will den Payee einer einzelnen Buchung leer machen können (2026-11-15), etwa wenn der Bankname nichts sagt oder ein
falscher Payee zugeordnet wurde und keiner passt. Heute geht nur Ändern: `normalizePayee` lehnt einen leeren Payee ab
(ledger-fxt09). Das Modell kann eine Buchung ohne Payee schon darstellen (`Entry.Payee: string option`).

## What
„Payee ändern“ (ledger-fxt09) bekommt den Zielzustand „ohne Payee“. Gleicher Schreibweg wie Payee ändern und Memo
(ADR 0062): Ereignis zuerst (ADR 0021 Punkt 5), dann der Push, bei Fehlschlag kein Rollback, Hinweisleiste mit
„Erneut senden“.

**Server**
- Der Befehl `setEntryPayee` nimmt als Ziel `string option`; `None` heißt „Payee entfernen“. `normalizePayee` bleibt für
  den Text (getrimmt, nicht leer, höchstens 200 Zeichen); ein leerer Text ist weiter ein Feldfehler, Leeren geht nur über
  `None`. Ziel gleich Ist (beide ohne Payee): `PayeeUnchanged`, kein Ereignis, kein Push.
- Ereignis: `EntryPayeeChangedInBb` bekommt `NewPayee: string option` (Feld `newPayee` darf `null` sein). Ältere
  Ereignisse mit Text lesen sich unverändert; ob `schema_version` steigt, folgt der bestehenden Regel im Code für
  Payload-Änderungen (Codec in `src/Server/Ledger/Events.fs`, Replay-Test mit einem Ereignis im alten Format).
  `EntryPayeeWriteBackFailed` und `EntryPayeeWrittenBackToYnab` bleiben, die Baseline ist schon `string option`.
- Push: `PUT .../transactions/{id}` mit `{"transaction":{"payee_id": null, "payee_name": null}}`
  (`YnabClient.buildPayeeBody` mit `None`). Ein Probelauf hinter `RUN_INTEGRATION_TESTS` gegen das Budget „Test“ (Muster
  ledger-fxt09, „Probelauf: der PUT des Payees …“) belegt, dass YNAB danach keinen Payee mehr hat und Kategorie, Betrag,
  `import_id`, `cleared`, `approved` unverändert sind. Lehnt YNAB das ab oder behält es den Payee, bricht der Worker ab
  (BOUNCED mit dem Ergebnis), statt einen Umweg zu bauen.
- Abgelehnt wie beim Ändern (kein Ereignis, kein schreibender Aufruf): unbekannt, in YNAB gelöscht, nicht mit YNAB
  verknüpft, Übertrag (`payeeLockReason`). Abgeglichene Buchungen bleiben änderbar.
- Spiegel (`Ledger.Domain.observe`, Baseline-Regel wie beim Payee): YNAB ohne Payee bei BB ohne Payee ist der BB-Stand
  (offener Fehlschlag endet mit `EntryPayeeWrittenBackToYnab`, Actor `YnabMirror`); Baseline lässt BB-Stand und
  Fehlschlag stehen; ein dritter Payee gilt.
- Historie: „Payee entfernt, vorher <alt>“; „Payee nicht in YNAB: <Grund>“ wie bisher.
- `import_id`, Comdirect-Reference, Regeln und Order-ID-Erkennung bleiben unberührt (sie lesen die Bankdaten).
- Validierung an der API-Grenze (`Server.Validation.validatePayeeChange`) mit `None` als gültigem Ziel.

**Client**
- Payee-Auswahl (`src/Client/DesignSystem/PayeePicker.fs`): unter den Vorschlägen eine Zeile „Payee entfernen“ (ruhiger
  Text, keine Fehlerfarbe), nur wenn die Buchung einen Payee hat. Ein Klick übernimmt sofort (§7.1) wie eine
  Vorschlagszeile; am PC per ↑↓ erreichbar und mit Enter wählbar (§7.9).
- Toast „Payee entfernt“ mit „Rückgängig“ (derselbe Befehl mit dem alten Payee, §7.13).
- Detail ohne Payee: die Feldzeile Payee zeigt „Ohne Payee“ in `text-muted`, der Pfeil `›` öffnet die Auswahl wie
  bisher; der Filter-Knopf „alle zeigen“ fehlt, solange kein Payee da ist.
- Listenzeile ohne Payee (Handy und PC): „Ohne Payee“ in `text-muted` an der Stelle des Payees, keine Lücke. Prüfen, ob
  das heute schon so ist (Buchungen aus YNAB ohne Payee); wenn ja, nur belegen.
- Die Karte im Durchgehen nutzt dieselbe Auswahl, „Payee entfernen“ gilt dort genauso.

## Acceptance criteria
- [ ] GIVEN eine mit YNAB verknüpfte Buchung mit Payee „Rewe“, WHEN im Detail „Payee entfernen“, THEN entsteht
      `EntryPayeeChangedInBb` (Alt „Rewe“, Neu ohne), die Buchung hat keinen Payee, der Push sendet `payee_id: null` und
      `payee_name: null`, die Historie zeigt „Payee entfernt, vorher Rewe“.
- [ ] GIVEN der Push scheitert, THEN bleibt die Buchung ohne Payee, `EntryPayeeWriteBackFailed` mit Baseline „Rewe“, die
      Hinweisleiste „Payee nicht in YNAB“ mit „Erneut senden“ erscheint; „Erneut senden“ sendet wieder `null`.
- [ ] GIVEN eine Buchung ohne Payee, WHEN „Payee entfernen“ (per API), THEN `PayeeUnchanged`, kein Ereignis, kein Push;
      in der Auswahl fehlt die Zeile.
- [ ] Gesperrt wie beim Ändern: in YNAB gelöscht, nicht verknüpft, Übertrag (je ein Test, kein Ereignis, kein Aufruf).
- [ ] Ein leerer Text bleibt ein Feldfehler unter dem Suchfeld; Leeren nur über die Zeile „Payee entfernen“.
- [ ] Toast „Payee entfernt“ mit „Rückgängig“; Rückgängig setzt „Rewe“ mit demselben Befehl.
- [ ] Spiegel: YNAB ohne Payee nach Fehlschlag ergibt `EntryPayeeWrittenBackToYnab` (Actor `YnabMirror`); Baseline
      „Rewe“ lässt BB ohne Payee und den Fehlschlag stehen; ein dritter Payee gilt.
- [ ] Codec: ein `EntryPayeeChangedInBb` im alten Format (Text) liest sich unverändert, eins mit `newPayee: null` ebenso
      (Replay-Test).
- [ ] Probelauf gegen das Budget „Test“ hinter `RUN_INTEGRATION_TESTS` (Liste „(requires .env)“), Ergebnis im Outcome.
- [ ] Detail und Listenzeile ohne Payee zeigen „Ohne Payee“ in `text-muted`; „alle zeigen“ fehlt dann.
- [ ] Browser: `[flow:payee-aendern]` in `scripts/ledger-payee.test.mjs` um „Payee entfernen“ erweitern (412 px, Touch:
      Zeile antippen, Toast, Rückgängig), kein neuer Flow. Sweep-Zustand „kassenbuch-detail-ohne-payee“ bei 412 und 1440
      px.
- [ ] Fable.Remoting: geänderte Signatur von `setEntryPayee` in `src/Shared/Api.fs`; Fixtures in `scripts/*.mjs` und
      `RemotingEndpointNegativeTests.fs` im selben Schritt mitziehen.
- [ ] README ledger: Befehl und Ereignis um „Payee entfernen“ ergänzt.

## Notes
Entschieden (Roman, 2026-11-15): nur die einzelne Buchung, kein Sammel-Leeren, keine Pflege der Payee-Liste.
Vorlage ist ledger-fxt09 (Payee ändern): gleicher Befehl, gleiche Ereignisse, gleiche Komponente.
Kein Prototyp-Element dafür; die Zeile folgt dem Aufbau der Vorschlagszeilen in `PayeePicker`.
