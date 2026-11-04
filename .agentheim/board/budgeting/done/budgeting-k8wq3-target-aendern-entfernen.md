---
id: budgeting-k8wq3
title: "Target ändern und entfernen im Tab Target: Betrag und Datum, nur geänderte Felder nach YNAB, Abgleich mit dem gezeigten Stand, konkrete Meldung bei Ablehnung, Aufzeichnung als Target-Änderung in der Historie"
status: done
type: feature
context: budgeting
created: 2026-11-04
completed:
depends_on: [design-system-001, budgeting-t6p8z]
blocks: [budgeting-xp46a, budgeting-p2zr6]
tags: [budget, target, ynab-write]
related_adrs: [0065, 0072, 0074, 0080]
related_research: [ynab-reihenfolge-monate-target-schreiben-2026-11-02, ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-h2zfn, budgeting-c5w2n, budgeting-q7m2c]
---

## Why
Teil B von budgeting-t6p8z. Targets leben in YNAB (ADR 0072). Roman will Betrag und Datum in BB ändern und ein Target entfernen, ohne YNAB zu öffnen. Die Probe budgeting-q7m2c zeigt: Wer nur das geänderte Feld sendet, behält Tag, Rhythmus und Datum. Anlegen und der Wechsel der Art kommen mit budgeting-xp46a.

## What
Regeln: ADR 0072 Punkte 2, 3 und 6, `docs/ux/budget/konzept.md` Abschnitt Targets, ADR 0080 (Target-Änderungen festhalten und schreiben).

**Lesestand**
- `Target` bekommt `Kind` (DU aus `goal_type`: `Need | TargetBalance | TargetBalanceByDate | MonthlyFunding | Debt | OtherKind of string`), `Date: string option` ("YYYY-MM-DD" aus `goal_target_date`, als String wie der Monatsschlüssel), `NeedsWholeAmount: bool option` und `Day: int option` (`goal_day`). `NeedsWholeAmount` und `Day` werden nur gelesen: `Day` für die Warnung in budgeting-xp46a, `NeedsWholeAmount` für die Anzeige "Auffüllen bis" (F6). Der Goal-Decoder wird aus `monthTarget` gezogen, damit Monats- und Kategorie-Antwort ihn teilen.

**YNAB-Client**
- `setCategoryTargetWith (send) budgetId categoryId (patch: TargetPatch) : Async<YnabResult<...>>` und `setCategoryTarget` für `PATCH /budgets/{id}/categories/{id}`. Den PATCH-Sender aus `setAssigned` in einen gemeinsamen privaten Helfer ziehen.
- `TargetPatch` in `Shared.Budgeting` mit `FieldPatch<'T> = Keep | SetTo of 'T | Clear`: `GoalTarget: FieldPatch<int64>`, `TargetDate: FieldPatch<string>`, `Frequency: TargetFrequency option` (in B immer `None`; `goal_needs_whole_amount` kommt nur dazu, wenn Roman F6 anders entscheidet). `Keep` lässt das Feld weg, `Clear` sendet `null`.
- `buildTargetBody`: `{"category":{...}}` nur mit den gesetzten Feldern, `goal_target` per `Encode.int` als JSON-Zahl (CLAUDE.md), `Clear` als `null`, Datum "YYYY-MM-DD". Ein leerer Patch wird abgelehnt, nichts geht hinaus.
- Antworten: 200 liefert die Kategorie (`data.category`), BB liest daraus den neuen Stand. 400 wird eine Meldung mit YNABs `error.detail`, Rückfall auf den Body. 401 Unauthorized, 404 "Kategorie nicht gefunden" (nicht "Monat nicht offen", der Pfad hat keinen Monat), 429 Rate-Limit, sonst Netzfehler.

**Domäne und Befehl**
- Endpoint `changeCategoryTarget: string -> TargetChangeRequest -> Async<Result<TargetChangeResult, string>>`. Der Request trägt `CategoryId`, `Shown: Target option` (der Stand, den der Client gezeigt hat) und `Wanted: TargetDraft option` (`None` heißt entfernen). `TargetDraft` hat Betrag, Datum und `Rhythm: KeepRhythm | SetRhythm of TargetFrequency`; in B ist nur `KeepRhythm` erlaubt, `SetRhythm` lehnt der Server mit "kommt mit budgeting-xp46a" ab. Ein `Shown = None` mit `Wanted = Some` (Anlegen) lehnt B ebenso ab.
- Reine Funktion `planTargetWrite : Target -> TargetDraft option -> Result<TargetPatch list, string>`: nur Felder, die zwischen gezeigtem und gewünschtem Stand abweichen. Betrag ändern ist nur `goal_target`, Datum setzen oder löschen nur `goal_target_date`, Entfernen ist `goal_target: null`. Nie `goal_frequency`, nie `goal_needs_whole_amount` (F6). Ohne Abweichung eine leere Liste. Betrag und Datum gelten für alle Arten (ADR 0072 Punkt 2).
- Ablauf unter einer Sperre je Kategorie (`TargetGuard`, gebaut wie die Sperre je Monat; das Target gilt für alle Monate):
  1. Monat aus YNAB lesen, Kategorie und Target suchen.
  2. Weicht das Target in YNAB vom `Shown` ab: nichts senden, nichts festhalten, Ergebnis `TargetChangedInYnab` mit dem frischen Stand.
  3. Steht YNAB schon auf `Wanted`: nichts senden, nichts festhalten, Ergebnis wie geschrieben.
  4. Sonst die Target-Änderung in `target_changes` festhalten (eine Transaktion), dann senden, das Ergebnis als Zeile in `target_change_results` anhängen (geschrieben oder fehlgeschlagen mit Grund).
- Kein Erneut-Senden. Bei Ablehnung steht die konkrete Meldung im Tab, die Eingabe bleibt stehen. Ein zweites Speichern vergleicht wieder gegen den gezeigten Stand und ist damit gefahrlos.
- **Nur im laufenden Monat** (Roman, F4): Ein Target gilt ab dem Monat, in dem man es ändert. Neue reine Funktion `targetEditAllowed : MonthKind -> bool` in `Shared.Budgeting`, wahr nur für `CurrentMonth`. Vergangene und künftige Monate lehnt die API-Grenze ab, ohne YNAB zu fragen. Das ist ein Nachtrag zu ADR 0074 Punkt 5 (dort "aktuell und künftig" für Ändern und Pausieren); Pausieren bleibt in künftigen Monaten erlaubt (budgeting-t6p8z). Der Monat wird an der Änderung gespeichert und ist damit immer der laufende.

**Persistenz**
- Tabellen `target_changes` (id, occurred_at, month, category_id, category_name, change_kind, alte und neue Werte: goal_target in Milliunits und date; nullbar, wo ein Target fehlen kann) und `target_change_results` (sequence, change_id, occurred_at, status, reason), append-only mit Triggern wie `budget_movements`, `CREATE TABLE IF NOT EXISTS`. Für jedes neue Optionsfeld in einem Row-Record einen `OptionHandler` registrieren, falls der Typ neu ist (heute `string` und `DateTime`; `int64 option` braucht eine eigene).

**Historie**
- `HistoryEntry` (aus budgeting-t6p8z) bekommt `TargetChanged`. Texte: "Target 50,00 € → 60,00 €", "Datum 01.06.2027", "Datum entfernt", "Target entfernt". Kein Betrag in der Spalte Change. Stand wie bei Bewegungen.

**Client (Tab Target)**
- Bei vorhandenem Target: Betragsfeld (Betragsrechnung wie Zugewiesen), Datum mit `DateField` (leer heißt ohne Datum). Rhythmus und, falls gesetzt, "Auffüllen bis" nur als Text, nicht änderbar in B.
- Knopf "Target speichern" (Orange als Hauptaktion des Tabs), aktiv nur bei Abweichung vom gezeigten Stand. "Target entfernen" als zerstörende Aktion mit Bestätigung nach Styleguide (`Button.danger`, `Modal.confirmDanger` am PC; am Handy das Gegenstück im Sheet, Werte aus `/styleguide`).
- Im vergangenen und im künftigen Monat nur lesend: Target, Rhythmus und Datum als Text, kein Formular, kein Entfernen. Die Bearbeitbarkeit kommt aus `targetEditAllowed`, die View ruft sie nur auf.
- Nach Erfolg: Target der Kategorie aus der Antwort setzen und den Monat still neu lesen (`rereadMonth`), weil YNAB den Fehlbetrag neu rechnet. Bis dahin ist der Fehlbetrag-Knopf gesperrt (Veralteter Monat). Historie wieder fällig.
- `TargetChangedInYnab`: Hinweis "Target wurde in YNAB geändert", Formular zeigt den frischen Stand, Eingabe verworfen.

**Vorbedingung ADR 0072 Punkt 6**
- Integrationstest in `src/Tests/YnabIntegrationTests.fs`, Liste "(requires .env)", hinter `RUN_INTEGRATION_TESTS`: sucht im Budget "Test" (wie die bestehenden Tests) die erste Kategorie mit NEED, Kadenz 1 und `goal_day` gesetzt, ohne Namen im Repo. Fehlt sie, scheitert der Test mit "Im Budget Test ein Monats-Target mit Tag von Hand anlegen". Sendet über die echte `setCategoryTarget` nur `goal_target` (alt plus 1,00 €), liest neu und erwartet gleichen Tag, gleiche Kadenz, gleiches `goal_needs_whole_amount` und gleiches Datum. Im `finally` den alten Betrag zurück. Schreibt nie in ein anderes Budget.
- **Beobachtung Vormonat:** Derselbe Test liest `goal_under_funded` der Kategorie im Vormonat vor der Änderung und nach der Änderung (vor dem Zurücksetzen). Er meldet beide Werte und das Ergebnis "Vormonat unverändert" oder "YNAB rechnet den Vormonat neu" als Testausgabe (`printfn`). Ein Unterschied macht den Test nicht rot, weil das eine Beobachtung ist. Ist der Vormonat nicht offen (404), meldet die Ausgabe das und die Beobachtung entfällt; die Prüfung des Tags bleibt davon unberührt.

## Acceptance criteria
**Domain und Encoder**
- [ ] (Domain) `planTargetWrite`: nur Betrag geändert ergibt genau `GoalTarget = SetTo`, sonst `Keep`. Nur Datum gesetzt ergibt `TargetDate = SetTo`, Datum gelöscht `Clear`. Betrag und Datum zugleich ergeben einen Patch mit beiden Feldern. Entfernen ergibt `GoalTarget = Clear` allein. Keine Abweichung ergibt eine leere Liste. `Frequency` ist in jedem Fall `None`.
- [ ] (Domain) `planTargetWrite`: Betrag und Datum bei TB, TBD und MF ergeben dieselben Patches wie bei NEED.
- [ ] (Encoder) `buildTargetBody`: `goal_target` ist eine JSON-Zahl, kein String (Regressionstest mit Kommentar wie beim Zuweisen); `Clear` ist `null`; `Keep` fehlt im Body; Datum "YYYY-MM-DD"; leerer Patch abgelehnt.
- [ ] (Decoder) Target mit `goal_type`, `goal_target_date`, `goal_needs_whole_amount`, `goal_day`, auch mit fehlenden Feldern und unbekanntem `goal_type` (`OtherKind`).
- [ ] (Decoder) `interpretTargetResponse`: 400 trägt YNABs `error.detail` in der Meldung, 404 heißt Kategorie nicht gefunden, 429 Rate-Limit, 200 liefert die Kategorie.

**Handler (In-Memory-SQLite, YNAB als Port)**
- [ ] GIVEN Target 50 € in YNAB, gezeigt 50 € WHEN ändern auf 60 € THEN eine Zeile in `target_changes`, danach ein PATCH nur mit `goal_target`, Ergebnis geschrieben. Die Änderung ist vor dem PATCH festgehalten (Port sieht die Zeile beim Aufruf).
- [ ] GIVEN YNAB lehnt mit 400 ab WHEN ändern THEN Ergebnis fehlgeschlagen mit YNABs Text, Antwort `Error` mit derselben konkreten Meldung.
- [ ] GIVEN YNAB steht inzwischen auf 55 €, gezeigt 50 € WHEN ändern THEN kein PATCH, keine Zeile, `TargetChangedInYnab` mit 55 €.
- [ ] GIVEN YNAB steht schon auf dem gewünschten Stand THEN kein PATCH, keine Zeile, Ergebnis geschrieben.
- [ ] GIVEN Entfernen WHEN YNAB nimmt an THEN PATCH `goal_target: null`, Historie zeigt "Target entfernt".
- [ ] GIVEN vergangener Monat THEN Fehler ohne YNAB-Aufruf. GIVEN künftiger Monat THEN Fehler ohne YNAB-Aufruf, keine Zeile. GIVEN `SetRhythm` oder Anlegen THEN Fehler ohne YNAB-Aufruf.
- [ ] GIVEN zwei Änderungen derselben Kategorie gleichzeitig THEN sie laufen nacheinander, die zweite sieht den Stand nach der ersten als Abweichung (Wächter-Test). Zwei Kategorien laufen unabhängig.
- [ ] Beide Tabellen sind append-only, `initializeBudgetingSchema` läuft zweimal ohne Fehler.
- [ ] `getCategoryHistory` liefert Target-Änderungen des gezeigten Monats zusammen mit Bewegungen und Pausen, neueste zuerst, mit jüngstem Ergebnis.

**Validation und API**
- [ ] (Domain) `targetEditAllowed`: `CurrentMonth` wahr, `PastMonth` und `FutureMonth` falsch.
- [ ] (Validation) `validateTargetChange` sammelt (laufender Monat als Parameter): ungültiger Monat, vergangener Monat, künftiger Monat, leere Kategorie-Id, Betrag negativ, Betrag nicht auf den Cent, Betrag über der Obergrenze, Datum nicht "YYYY-MM-DD", Währung nicht EUR.
- [ ] Zeile in `RemotingEndpointNegativeTests.fs` für `BudgetApi.changeCategoryTarget` (leere Kategorie, Betrag am Rand von decimal, vergangener und künftiger Monat: Fehler statt Ausnahme).
- [ ] Live-Anschlüsse des neuen Ports aufgelistet und belegt (Doktrin Punkt 3).

**Client-`update`**
- [ ] GIVEN künftiger oder vergangener Monat gezeigt THEN Tab Target ohne Formular und ohne Entfernen; keine Nachricht löst dort einen Aufruf aus (reine Funktion, beide Monatsarten je ein Fall).
- [ ] GIVEN Eingabe im Formular des laufenden Monats WHEN Wechsel in einen künftigen Monat THEN die Eingabe ist verworfen, nichts gesendet; zurück im laufenden Monat zeigt das Formular den geladenen Stand.
- [ ] GIVEN Formular ohne Abweichung THEN Speichern gesperrt (reine Funktion). GIVEN Abweichung WHEN Speichern THEN ein Aufruf mit `Shown` gleich dem geladenen Target; ein zweiter Tipp während des Aufrufs sendet nicht.
- [ ] GIVEN Erfolg THEN neues Target in der Kategorie, Monat still neu gelesen, Fehlbetrag-Knopf bis dahin gesperrt, Historie fällig.
- [ ] GIVEN `TargetChangedInYnab` THEN Formular auf frischem Stand, Hinweis sichtbar. GIVEN Fehler THEN Meldung, Eingabe bleibt.
- [ ] GIVEN Aufruf läuft WHEN Kategorie- oder Monatswechsel THEN die Antwort wirkt nur auf ihre Kategorie (Aufzählung der Reihenfolgen, Doktrin Punkt 20).

**Browser, Fixtures, Integration**
- [ ] Neuer Flow `target-aendern` in `FLOWS` ("Am Handy im Tab Target den Betrag eines Targets ändern und speichern.") mit genau einem `[flow:target-aendern]`-Test, API per `page.route` gemockt.
- [ ] Fixtures mit Target-Objekt (`scripts/budget-page.test.mjs`, `scripts/targets-apply.test.mjs`) tragen die neuen Felder.
- [ ] Integrationstest "monatlich mit Tag" wie oben, übersprungen ohne `RUN_INTEGRATION_TESTS`. Rot nur, wenn Tag, Kadenz, `goal_needs_whole_amount` oder Datum sich ändern; die Beobachtung des Vormonats steht in der Testausgabe und färbt den Test nicht.
- [ ] Tab Target am Handy im 500-px-Sheet bedienbar, Speichern ohne Scrollen erreichbar. [human-eye]
- [ ] README budgeting: **Target** um Art und Datum ergänzt, neuer Begriff **Target-Änderung**, Key command TargetÄndern, Key events TargetGeändert und TargetÄndernFehlgeschlagen, Open question "Tag und Rhythmus" nach dem Integrationslauf aktualisiert. Delta geprüft mit `npm run check:readme-delta`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Verfeinert am 2026-11-04 (Orchestrator mit tactical-modeler und architect).

Die alte Formulierung "Budget-Bewegung neuer Herkunft" ist ersetzt: `budget_movements` trägt altes und neues Zugewiesen (NOT NULL), "Zuletzt verwendet" und `windowMayHaveGrown` lesen diese Tabelle. Eine Target-Änderung ist deshalb eine eigene Aufzeichnung, die in der Historie wie eine Bewegung erscheint (F3).

**Nach dem Bau, durch Roman (F5):** Ein grüner Lauf des Integrationstests gegen das Budget Test ist mit der Ausgabe zum Vormonat vermerkt, bevor BB in Romans Budget ein Target schreibt. Der Worker ruft keine Live-Dienste auf.

Unbelegt bleiben "alle 3 Monate mit Datum" (4 Targets) und MF (18). Dort stützt sich B auf den Analogschluss aus ADR 0072. Der Integrationstest belegt nur "monatlich mit Tag".

Offen: ob YNAB ein `goal_target` unter 10 Milliunits rundet, ist nicht belegt. Die Prüfung auf den Cent umgeht die Frage.

`goal_creation_month` ging in der Probe mit der ersten Schreibung auf den laufenden Monat. Die Wirkung auf Vormonate ist ungeprüft, der Integrationstest beobachtet sie. **Ändert YNAB den Vormonat, entscheidet Roman vor dem ersten Einsatz, wie BB alte Monate zeigt.**

**Entscheidungen (Roman, 2026-11-04):** F3 a (Target-Änderungen in der Historie, eigene Tabellen), F4 abweichend von der Empfehlung: Ändern, Entfernen und Anlegen nur im laufenden Monat, künftige Monate zeigen das Target nur an, kein Hinweis "gilt für die Kategorie". F5 a (Integrationstest, kein Schalter). F6 a (kein Umschalter Set aside/Refill). F7 a (Knopf "Target speichern"). `docs/ux/budget/konzept.md` (Tabelle im Abschnitt Monate) und die README-Zeile **Monatsart** ziehen beim Übernehmen nach: "Target ändern" nur aktuell, "pausieren" aktuell und künftig.

## Outcome

Target ändern und entfernen ist gebaut (ADR 0072, ADR 0080). `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3664 grün, 11 opt-in übersprungen, node 290 grün, Build und beide Wächter grün). `npm run check:readme-delta -- --bc budgeting` endet mit Exit 0. Kein Live-Aufruf, `RUN_INTEGRATION_TESTS` nicht gesetzt.

**Shared** (`src/Shared/Budgeting.fs`, `src/Shared/Api.fs`): `Target` trägt `Kind: TargetKind` (`Need | TargetBalance | TargetBalanceByDate | MonthlyFunding | Debt | OtherKind`), `Date: string option`, `NeedsWholeAmount: bool option`, `Day: int option`. Neu: `targetEditAllowed` (nur `CurrentMonth`), `FieldPatch<'T>` (`Keep | SetTo | Clear`), `TargetFrequency`, `TargetPatch`, `RhythmChoice` (`KeepRhythm | SetRhythm`), `TargetDraft` (`NewGoal`, `NewDate`, `NewRhythm`), `TargetChangeRequest` (`CategoryId`, `Shown`, `Wanted`), `TargetChangeResult` (`TargetWritten | TargetChangedInYnab`, je mit dem Target danach), Texte `TargetEditMonthText`, `TargetRhythmLaterText`, `TargetCreateLaterText`, `euroText`, `dateText`. Historie: `TargetChangeKind` (`TargetEdited | TargetRemoved`), `HistoryTargetChange` (Felder `GoalBefore/After`, `DateBefore/After`, `ChangeKind`, um Kollisionen der Record-Inferenz mit `TargetDraft` und `Target` zu vermeiden), `HistoryEntry.TargetChanged`, `targetChangeText`. Endpoint `BudgetApi.changeCategoryTarget`.

**Server:** `YnabClient`: Goal-Decoder `Decoders.goalTarget` aus `monthTarget` gezogen, geteilt mit `categoryTargetDecoder` (`data.category`); `targetKind`; gemeinsamer privater `patchSender` für `setAssigned` und `setCategoryTarget`; `categoryUrl`, `buildTargetBody` (`goal_target` per `Encode.int`, `Clear` als `null`, `Keep` fehlt, leerer Patch abgelehnt), `interpretTargetResponse` (400 mit `error.detail`, Rückfall auf den Body; 404 `CategoryNotFound`; 429; 401), `setCategoryTargetWith`, `setCategoryTarget`. `Domain`: `targetChangeSupported`, `planTargetWrite`, `targetChangeKindOf/Text/OfText`, `mergeHistoryWithTargetChanges`. `CommandHandlers`: `YnabPort.SetTarget`, `TargetGuard` (Sperre je Kategorie), `changeTarget` (Monat, Rhythmus und Anlegen vor dem Lesen abgelehnt; frisch lesen; Abweichung ergibt `TargetChangedInYnab` ohne Zeile; leere Planung ergibt geschrieben ohne Zeile; sonst festhalten in einer Transaktion, dann senden, Ergebnis je Schritt; kein Erneut-Senden). `Persistence`: Tabellen `target_changes` und `target_change_results` mit Index und je zwei Triggern, `appendTargetChanges`, `appendTargetChangeResult`, `queryTargetChanges`/`loadTargetChanges`; `OptionHandler<int64>` neu registriert (`src/Server/Persistence.fs`). `QueryHandlers.HistoryPort.LoadTargetChanges`, `categoryHistory` mischt drei Quellen. `Validation.validateTargetChange` sammelt Monat (ungültig, vergangen, künftig), leere Kategorie, Betrag negativ, nicht auf den Cent, über 1.000.000 €, nicht EUR, Datum nicht "YYYY-MM-DD". `Api.changeCategoryTargetWith` und die Zeile in `budgetApiWith`, `livePort.SetTarget`.

**Client:** neues Modul `src/Client/Components/Budget/TargetEdit.fs` (rein: `targetTabMode`, `syncTargetForm`, `targetFormState`, `canSaveTarget`, `startTargetSave`, `askRemoveTarget`, `startTargetRemove`, `withTarget`, `applyTargetAnswered`, `targetNoticeText`, `refillText`), neue View `src/Client/Components/Budget/TargetEditView.fs` (Betragsfeld mit Rechnung wie Zugewiesen, `DateField`, Rhythmus und "Auffüllen bis" als Text, "Target speichern" als `Button.Primary`, "Target entfernen" als `Button.Danger`, am PC `Modal.confirmDanger`, am Handy Bestätigungsblock im Sheet). `Model.TargetForm` und `Model.TargetCall`, Nachrichten `TargetAmountChanged`, `TargetDateChanged`, `SaveTarget`, `AskRemoveTarget`, `ConfirmRemoveTarget`, `CancelRemoveTarget`, `TargetChangeAnswered`. Nach einer Antwort mit Ergebnis liest der Client den Monat still neu (`rereadMonth`, `MonthStale` sperrt den Fehlbetrag-Knopf) und die Historie ist wieder fällig. Ein Fehler steht im Tab, die Eingabe bleibt; kam er, während eine andere Kategorie gezeigt wird, als Warn-Toast. In `InspectorView.fs` nur der Tab Target (`targetTab`, neues `targetStatus`), der Tab Historie ist unberührt. `Modal.confirmDanger` hat jetzt "Abbrechen" statt "Cancel" (vorher ohne Aufrufer).

**Tests (51 neu):** `src/Tests/TargetChangeTests.fs` (Planung je Fall und je Art, Vorprüfung, `targetEditAllowed`, Texte der Historie, Encoder mit Regressionskommentar zur JSON-Zahl, Decoder mit fehlenden Feldern und `OtherKind`, `interpretTargetResponse`; Abläufe gegen In-Memory-SQLite und Fake-YNAB: Zeile vor dem PATCH, 400 mit derselben Meldung, `TargetChangedInYnab`, schon auf Wunsch, Entfernen mit Historie, vergangen/künftig/Rhythmus/Anlegen ohne YNAB-Aufruf, unbekannte Kategorie, Wächter derselben Kategorie, zwei Kategorien unabhängig, gemischte Historie mit jüngstem Ergebnis; Persistenz append-only und zweimaliges Schema, unbekannte Art; Grenze, Live-Anschluss, Berliner Uhr über `budgetApiWith`). `src/Tests/TargetChangeClientTests.fs` (Sichtbarkeit je Monatsart ohne Aufruf, Verwerfen beim Monatswechsel, Speichern gesperrt ohne Abweichung, ein Aufruf mit `Shown`, Entfernen nur nach Bestätigung, Erfolg, `TargetChangedInYnab`, Fehler, Toast, Aufzählung aller Reihenfolgen bis Tiefe 5 aus Tipp, Kategorie, Monat und Antwort mit eigenem Orakel). Zeile `BudgetApi.changeCategoryTarget` in `RemotingEndpointNegativeTests.fs`. Integrationstest in `src/Tests/YnabIntegrationTests.fs` ("Ein Monats-Target mit Tag behält Tag, Kadenz und Datum, wenn nur goal_target gesendet wird (Budget Test)") mit `printfn`-Beobachtung des Vormonats. `scripts/target-change.test.mjs`: `[flow:target-aendern]` (neu in `FLOWS`) und ein `[layout]`-Test, dass "Target speichern" im 500-px-Sheet ohne Scrollen im Bild liegt. Fixtures in `budget-page.test.mjs` und `targets-apply.test.mjs` tragen die neuen Target-Felder; übrige Tests auf die neuen Felder und `SetTarget` gezogen.

**Live-Anschlüsse des neuen Ports (Doktrin Punkt 3):**
- `Api.livePort.SetTarget = YnabClient.setCategoryTarget token budgetId categoryId patch`: Strukturwächter `findMissingApiWirings` um das Muster erweitert, mit Gegenbeweis "Target ohne YNAB" (`StructureGuardTests.fs`); live belegt nur im Integrationstest hinter `RUN_INTEGRATION_TESTS`.
- `budgetApiWith.changeCategoryTarget` (Einzeiler auf `changeCategoryTargetWith` mit `today ()` und `utcNow ()`): Beleg "Über die BudgetApi: ändern mit Uhr und Monatsart aus Berlin".
- `liveBudgetApi`: Beleg "Der Live-Anschluss liest Token und Budget für Target ändern aus den Settings".
- `budgetApi` in `Api.webApp` unverändert, `initializeBudgetingSchema` in `Program.fs` legt die neuen Tabellen beim Start an.

**Gegenprobe, ehrlich:** Die Typänderung an `Target` musste auf einmal fallen, Tests und Produktion entstanden danach. Rot belegt je Mutante einzeln und zurückgenommen: `changeTarget` ohne Vergleich mit `Shown` (zwei Ablauftests rot), `withTarget` ohne Monatsvergleich zusammen mit `ownForm` immer wahr (Reihenfolgen rot). Die Mutante "`TargetCall` wird bei jeder Antwort frei" überlebte, sie ist gleichwertig, weil nie zwei Aufrufe zugleich laufen. Die Aussage gilt nur für diese Mutanten.

**Offen:** Der grüne Lauf des Integrationstests gegen das Budget Test mit der Ausgabe zum Vormonat steht aus (F5, Roman). `[human-eye]` für das Sheet nicht per Screenshot angesehen, der `[layout]`-Test misst nur die Lage des Knopfs. `docs/ux/budget/konzept.md` (Tabelle Monate) ist nicht angepasst, das ist Doku außerhalb der Quell- und Testdateien. Die Open question zu "Tag und Rhythmus" hat keinen Bullet-Anker (kein fetter Kopf), deshalb ein neuer Bullet im README-Delta statt eines replace.
