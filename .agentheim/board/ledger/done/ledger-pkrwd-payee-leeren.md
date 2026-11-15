---
id: ledger-pkrwd
title: "Payee einer Buchung leeren: im Detail den Payee entfernen, nach YNAB ohne Payee zurückschreiben"
status: done
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

## Verifier note (iteration 1)
VERDICT: FAIL, ITERATION_HINT: likely-fixable. Stand nach Merge von `main` in den Branch (Conductor, vor der Prüfung).
REASONS: `./verify.sh` im Worktree: test:scripts rot, 347 von 348. `[flow:payee-aendern]` (scripts/ledger-payee.test.mjs:168): strict mode violation, `[data-toast-action="true"]` mit „Rückgängig" trifft 2 Elemente; der „Rückgängig"-Knopf des ersten Toasts „Payee geändert · Rewe Markt" steht noch im DOM, wenn „Payee entfernt" erscheint. Einzeln zweimal reproduziert, kein Flackern. Alles andere (Server, Client, Codec V1/V2, Replay, Spiegel, README_DELTA, „ohne Payee" als BB-Text) passt.
SUGGESTED_FIX: Den zweiten Tap auf den Toast „Payee entfernt" eingrenzen oder warten, bis der erste Toast weg ist; prüfen, ob der alte Toast nach seinem eigenen „Rückgängig" seine Aktion behalten soll (Ursache klären, nicht nur den Selektor anpassen). Danach `./verify.sh` bis `VERIFY: PASS`.

## Outcome

Payee entfernen läuft über den Befehl von „Payee ändern“. `./verify.sh` endet nach dem Merge von `main` mit `VERIFY: PASS` (dotnet 5148 grün, 16 übersprungen nur in den Listen „(requires .env)“, node 348 grün).

**Server**
- `setEntryPayee` nimmt `string option` (`src/Shared/Api.fs`, `src/Server/Ledger/CommandHandlers.fs`, Port `PayeePush = string -> string option -> ...`). `None` heißt „Payee entfernen“. Neue reine Regel `Shared.Domain.normalizePayeeTarget`: `None` ist gültig, ein Text läuft durch `normalizePayee`, ein leerer Text bleibt ein Fehler. `Server.Validation.validatePayeeChange` nimmt dasselbe Ziel. Ziel gleich Ist (beide ohne Payee, `payeeKey`): `PayeeUnchanged`. Sperren, Reihenfolge (Ereignis vor dem Push), Fehlschlag mit Baseline und `retryPayeeWriteBack` (sendet jetzt auch `None`) sind unverändert.
- Ereignis: `EntryPayeeChangedInBbPayload.NewPayee: string option`. Die Regel in `Events.schemaVersion` sagt: eine andere Payload-Form bekommt eine neue Version (ADR 0013 Punkt 7). Deshalb schreibt `EntryPayeeChangedInBb` jetzt Version 2 (`newPayee` darf `null` sein). Version 1 liest `decodePayeeChangedInBbV1` weiter, `newPayee` ist dort Pflicht und wird zu `Some`. `apply` setzt `Payee = p.NewPayee`.
- Push: `YnabClient.buildPayeeBody` / `sendPayee` / `updatePayee` nehmen `string option`. Bei `None` ist der Rumpf `{"transaction":{"payee_id": null, "payee_name": null}}`.
- Spiegel: Die Baseline-Regel in `Ledger.Domain.observe` vergleicht Optionen und trägt den Fall ohne Payee ohne Änderung.
- Historie (`ReadModel.describe`): „Payee entfernt, vorher <alt>“.

**Probelauf gegen das Budget „Test“** (`src/Tests/YnabIntegrationTests.fs`, „Probelauf: der PUT ohne Payee entfernt den Payee (Budget Test)“, Liste „YNAB Integration Tests (requires .env)“). Einmal live gelaufen. Dafür stand `RUN_INTEGRATION_TESTS=true` kurz in der `.env` des Worktrees. Danach wurde die Zeile entfernt und die Prüfsumme bestätigt (vorher und nachher gleich). Ergebnis: YNAB antwortet auf den PUT mit 200. Danach sind `payee_name` und `payee_id` beide leer. Kategorie, Betrag (-4560), `import_id`, `cleared` und `approved` bleiben unverändert. Die Probebuchung wurde gelöscht (DELETE 200). Der Lauf legt einen Probe-Payee „Probelauf pkrwd … Bank“ an, den die API nicht löschen kann.

**Client**
- `src/Client/DesignSystem/PayeePicker.fs`: neuer Fall `RemovePayee` und `pickerRows` (unter `options` die Zeile „Payee entfernen“, nur wenn `Current` einen Payee hat). Die Zeile ist ruhig gesetzt (`ListRoles.primarySize` und `text-muted`, keine Fehlerfarbe), `data-testid="payee-picker-remove"`. `payeeOf` und `Pick` tragen `string option`, `OnPick: string option -> unit`. ↑↓ erreichen die Zeile, Enter wählt sie nur, wenn sie markiert ist. Ohne Markierung wählt Enter nie „Payee entfernen“, damit eine leere Eingabe ein Feldfehler bleibt. Der Payee-Filter (`suggestedOptions`) kennt die Zeile nicht. Die Karte im Durchgehen nutzt dieselben `payeePickerProps`, dort gilt die Zeile also genauso.
- Reducer: `StartSavePayee` / `RunSetPayee` tragen `string option`, `payeeSaveStep` nimmt das Ziel. Toast „Payee entfernt“ (`payeeRemovedToastText`) mit „Rückgängig“ (`UndoPayee` mit dem alten Payee, derselbe Befehl). Scheitert der Push, kommt der Warn-Toast „Payee entfernt. Payee nicht in YNAB: <Grund>“, und die Hinweisleiste mit „Erneut senden“ bleibt wie bisher.
- Liste: Ohne Payee zeigten Handy-Zeile und PC-Tabelle den Platzhalter bisher in der Payee-Farbe. Jetzt steht er in `text-muted` (`View.payeeMissing`). Im Detail war die Payee-Zeile ohne Payee schon gedämpft, und `payeeFilterLabel` ließ „alle zeigen“ schon weg. Beides ist jetzt nur durch einen Test belegt.
- Styleguide-Demo „Payee-Auswahl“ zeigt die neue Zeile, mit „Gewählt: Ohne Payee“ nach dem Entfernen.

**Abweichung beim Text:** BB nutzt seit ledger-n3pqw für eine Buchung ohne Payee den Platzhalter „ohne Payee“ klein (`payeeLabel`). Er steht in Liste, Detail-Kopf und Detail-Zeile, und die Übersicht prüft „klein wie im Kassenbuch“ (`OverviewTodoTests`). Die Task schreibt „Ohne Payee“. Ich habe den bestehenden Text behalten, sonst wären Übersicht und Kassenbuch auseinandergelaufen. Will Roman die große Schreibweise, ist das eine Zeile in `payeeLabel` plus die Übersicht.

**Tests**
- `src/Tests/LedgerPayeeTests.fs`: API-Grenze mit `None`; Codec mit Version 1 im alten Format, Version 2 mit `newPayee: null`, Version 1 lehnt `null` ab, Rundreise; Replay aus einem Ereignis der Version 1 und einem Entfernen; Befehle für Entfernen (Ereignis Alt „Rewe“, Neu ohne, Reihenfolge, Push mit `None`, Historie), Fehlschlag mit Baseline „Rewe“ und Erneut senden mit `None`, `PayeeUnchanged` ohne Payee, jede Sperre (unbekannt, gelöscht, nicht verknüpft, Übertrag), abgeglichen, Rückgängig; Spiegel für BB-Stand, Baseline und dritten Payee; Rumpf mit `payee_name: null` gegen den Fake-Handler. Die bestehenden Tests sind auf `string option` umgestellt, die Schema-Version des geänderten Typs ist 2.
- `src/Tests/LedgerPayeeClientTests.fs`: Auswahl-Zeilen (`pickerRows`, nur mit Payee, auch bei neuer Eingabe), Tasten, `payeeSaveStep None`, `payeeMissing`, Reducer (Aufruf ohne Payee, Toast und Rückgängig, Warn-Toast, ohne Payee kein Aufruf).
- Gegenprobe: Ohne den Filter auf `RemovePayee` in `keyAction` wird „Tasten: „Payee entfernen“ ist per ↑↓ erreichbar …“ rot (lokal geprüft, danach wiederhergestellt).
- `RemotingEndpointNegativeTests.fs`: Die Zeile `setEntryPayee` hat einen dritten Fall (`None` bei unbekannter Buchung). `LedgerHeldDuplicateTests.fs` ist auf die neue Signatur umgestellt.
- Browser: `[flow:payee-aendern]` in `scripts/ledger-payee.test.mjs` ist erweitert (412 px, Touch). Nach dem Rückgängig des Änderns wird die Zeile „Payee entfernen“ angetippt, der Kopf zeigt „ohne Payee“, der Befehl geht mit `null`, der Toast „Payee entfernt“ erscheint, „Rückgängig“ setzt den alten Payee mit demselben Befehl. Es gibt keinen neuen Flow. Die Mock-Antwort trägt `CurrentPayee: null`. Neuer Sweep-Zustand `kassenbuch-detail-ohne-payee` (412 als Sheet, 1440 im Panel, `mockLedger(page, { withoutPayee: true })` mit einer Buchung `Payee: null`), grün.

**Iteration 2 (Verifier-FAIL nach dem Merge von `main`):** `[flow:payee-aendern]` fand beim zweiten „Rückgängig“ zwei Knöpfe. Ursache geklärt (DOM-Abzug im Test, danach entfernt): Der zweite Knopf gehört zum ersten Toast „Payee geändert · Rewe Markt“. Der ist nach seinem eigenen „Rückgängig“ als `Exiting` markiert und läuft noch seine Abgangsanimation (`Types.Toast.exitDurationMs` = 220 ms). Vom ersten Rückgängig bis „Payee entfernt“ vergehen im Test etwa 200 ms. Nach 1,5 s war er weg. Ein Fehler in der App ist das nicht: `ToastActionClicked` übergeht einen abgehenden Toast, ein zweiter Tipp macht also nicht zweimal rückgängig (Test in `src/Tests/ToastLifecycleTests.fs`). Vor dem Merge war der Test knapp langsamer als 220 ms und deshalb grün. Der Flow wartet jetzt nach dem ersten Rückgängig, bis der alte Toast aus dem DOM ist (`waitFor({ state: "detached" })`). Danach steht nur der Toast „Payee entfernt“ mit seinem Knopf da. Der Selektor ist unverändert. Dreimal einzeln grün, dann `./verify.sh` grün.

**README-Delta:** Den Bullet **Payee ändern** ersetzt das Delta. Unter `## Key events` und `## Key commands` hängt es je einen Bullet an. Das Gate (`check:readme-delta`) hat Exit 0. „Ganz nachlesen“:
- `## Key commands: PayeeÄndern`: muss nachgezogen werden. Der Bullet sagt „neuer Payee als Text“ und hat keinen fetten Kopf, ein replace erreicht ihn also nicht. Der Conductor kann „(eine Buchung, Ziel `string option`: Text oder `None` zum Entfernen, ledger-pkrwd)“ von Hand einsetzen.
- `## Key commands: PayeeEntfernen`: passt (neu). Das Gate meldet in Iteration 2 dieselben zwei Einträge (Zeile 408 und 412), Exit 0.
- Nicht in der Liste, aber veraltet: der Bullet *EntryPayeeChangedInBb* / … unter `## Key events` (Zeile 331, kursiver Kopf, per replace nicht erreichbar). Er sagt „`NewPayee` getrimmt“ und „Schema-Version 1“. Von Hand anpassen: „`NewPayee` getrimmt oder leer (Payee entfernt), `EntryPayeeChangedInBb` Version 2 seit ledger-pkrwd, die beiden anderen Version 1“.

**Neben der Skizze verglichen:** Für diese Task gibt es kein Prototyp-Element (Notes der Task). Die Zeile folgt dem Aufbau der Vorschlagszeilen in `PayeePicker`. Die Layout-Invarianten prüft der Sweep mit `kassenbuch-detail-ohne-payee` (412 und 1440) und den bestehenden Zuständen `kassenbuch-payee-auswahl`.

Offen: Roman sieht „Payee entfernen“ am Handy und am PC an.

**Integration (Conductor):** Verifier Iteration 2 PASS, eigenes `./verify.sh` im Worktree `VERIFY: PASS` (dotnet 5148, node 348). README-Delta angewendet (drei Ops `applied`, `check:readme-delta` Exit 0). „Ganz nachlesen“: `## Key commands: PayeeÄndern` nachgezogen („Ziel `string option`: Text oder `None` zum Entfernen“), `## Key commands: PayeeEntfernen` passt. Zusätzlich nachgezogen: der Key-events-Bullet *EntryPayeeChangedInBb* / … („`NewPayee` getrimmt oder leer“, „`EntryPayeeChangedInBb` Version 2 seit ledger-pkrwd“).
