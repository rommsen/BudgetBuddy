---
id: ledger-hg5m5
title: "Regel aus der Buchung: „Regel anlegen“ im Detail und beim Durchgehen, dazu ein Angebot nach einer Kategorie-Änderung, vorbelegt mit Payee und Kategorie"
status: done
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001-styleguide, ledger-sfab0, categorization-n3sxw]
blocks: []
tags: [etappe-3, frontend, kassenbuch, kategorie, regeln]
related_adrs: [0055, 0087]
related_research: []
prior_art: [ledger-k2rbn]
---

## Why
Bis zum Import direkt (banking-import-d0fey, 613eee26) legte Roman Regeln an der Importzeile im Sync an,
vorbelegt aus der Buchung. Mit dem SyncFlow ist das weggefallen. Entschieden (Roman, 2026-11-11): alle
Funktionen der heutigen Prüfung bleiben, „Regel aus der Buchung“ kommt mit Schnitt 6. Wer eine Kategorie von
Hand ändert, wiederholt das oft für denselben Payee.

## What
Zwei Einstiege in dasselbe Regel-Formular aus categorization-n3sxw, vorbelegt aus der Buchung (Roman,
2026-11-13: „Beides“):
1. Ein fester Eintrag „Regel anlegen“ im Buchungs-Detail und auf der Karte beim Durchgehen.
2. Ein Angebot im Buchungs-Detail, nachdem Roman die Kategorie einer Buchung mit Payee geändert hat.

Vorbelegung: Name = Payee, Wenn = Enthält, Suchen in = Payee, Muster = Payee, Dann Kategorie = Kategorie der
Buchung. Roman sieht und ändert alles vor dem Anlegen.

## Acceptance criteria

### Fester Eintrag
- [ ] Das Buchungs-Detail (PC Panel, Handy Sheet) hat unter den Feldern den Eintrag „Regel anlegen“
      (`Detail.forwardArrow`-Zeile oder gleich aussehend). Er öffnet das Regel-Formular aus
      categorization-n3sxw, das den Inhalt von Panel bzw. Sheet ersetzt und mit „Zurück“ zur Buchung führt
      (§7.15).
- [ ] Die Karte beim Durchgehen hat denselben Eintrag. Er öffnet das Formular am PC als `Modal` (Seite ohne
      Liste, wie „Neue Buchung“ auf der Übersicht), am Handy als Sheet. Nach Anlegen oder Abbrechen steht
      dieselbe Karte da, Fortschritt unverändert.
- [ ] Die Vorbelegung ist wie oben. Hat die Buchung keine Kategorie, eine Aufteilung oder ist sie ein
      Übertrag, bleibt „Dann Kategorie“ leer. Ohne Payee gibt es den Eintrag nicht.
- [ ] Nach „Regel anlegen“ meldet ein Toast „Regel angelegt“; die Regel steht vorn in der Liste des Tabs
      Regeln. Die Buchung selbst ändert sich nicht.
- [ ] Unter dem Formular steht „Gilt für neue Importe.“ (12 px, gedämpft): Regeln wirken nicht auf
      vorhandene Buchungen (README categorization).

### Angebot nach einer Kategorie-Änderung
- [ ] GIVEN eine Buchung mit Payee „Rewe“ WHEN Roman im Detail die Kategorie auf „Lebensmittel“ ändert und
      das Speichern gelingt THEN steht unter dem Feld Kategorie die Zeile „Regel für „Rewe“ anlegen?“ mit
      „Anlegen“ und einem Schließen-×. „Anlegen“ öffnet das vorbelegte Formular wie der feste Eintrag.
- [ ] GIVEN eine aktive Regel, die als erste auf den Payee passt, ordnet schon die neue Kategorie zu WHEN
      Roman die Kategorie ändert THEN erscheint kein Angebot. Das Urteil kommt vom Server (dieselbe
      Engine wie beim Import, `RulesEngine`), nicht aus einem Nachbau im Client.
- [ ] Kein Angebot bei Sammelaktionen (Kategorie für eine Auswahl), bei Aufteilung, Übertrag oder ohne Payee.
- [ ] × oder ein Wechsel zu einer anderen Buchung lässt das Angebot verschwinden; es kommt erst nach der
      nächsten Kategorie-Änderung wieder. „Rückgängig“ im Toast nimmt die Kategorie zurück und das Angebot weg.
- [ ] Der Toast nach der Änderung bleibt wie heute („Rückgängig“); das Angebot verdeckt ihn nicht und er
      verdeckt das Angebot nicht (§7.13).

### Tests und Gate
- [ ] Szenarien oben als Tests auf ihrer Heimat-Ebene (`update` für Angebot zeigen und verwerfen, Server
      für das Urteil „passt schon“).
- [ ] Ein Browser-Test `[flow:regel-aus-buchung]` (Handy 412 px mit Touch: Kassenbuch, Buchung öffnen,
      „Regel anlegen“, Vorbelegung sichtbar, anlegen, zurück im Detail); `FLOWS` in
      `scripts/browser-usage.mjs` ergänzt.
- [ ] Neue Pflichtfelder in Records aus `src/Shared/` sind in den Fixtures der `scripts/*.test.mjs` nachgezogen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Romans Blick am Handy und am PC auf Eintrag, Angebot und Formular. [human-eye]

## Abweichungen
- Der Prototyp (Version 17) zeigt keinen Weg von der Buchung zur Regel. Diese Funktion gab es in BB im
  SyncFlow, sie gilt (CLAUDE.md „gilt BB“) und folgt im Aussehen dem Formular aus categorization-n3sxw und
  den Mustern `Detail` und §7.15.
- Roman wählte „Angebot im Toast“. Der Toast hat genau eine Aktion, nach einer Kategorie-Änderung ist das
  „Rückgängig“, und ein Toast ist Rückmeldung, kein Angebot (§7.13). Das Angebot steht deshalb als Zeile im
  Detail unter der Kategorie; es läuft nicht nach ein paar Sekunden ab.

## Notes
Verfeinert 2026-11-13 aus Schnitt 6 (categorization-n3sxw), mit Roman: zwei Tasks, diese hängt am Tab
Regeln und nutzt dessen Formular. Keine Anzeige, wie viele vorhandene Buchungen betroffen wären (Regeln
wirken nur auf neue Importe); doppelte Regeln verhindert das Urteil „passt schon“.
Bausteine: `Detail` (Kassenbuch-Detail, `src/Client/Components/Ledger/`), Durchgehen (`Page.Review`,
`ReviewCard`), `RulesApi.createRule`. Für das Urteil braucht es einen lesenden Aufruf (etwa „erste passende
aktive Regel für Payee und Memo“), über `Retry.reading`, Inventar in `src/Tests/RetryTests.fs`.
Stil: Optik aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`.

## Outcome

Regel aus der Buchung, zwei Einstiege in dasselbe Formular aus categorization-n3sxw.

**Server** (`src/Server/RulesEngine.fs`): reine Funktion `ruleCategoryFor rules payee memo`: die Kategorie der ersten passenden aktiven Regel über `compileRules` und `classify` (dieselbe Engine wie beim Import), ohne Payee oder bei ungültiger Regel keine. `Ledger.QueryHandlers.ruleCategoryForPayee` delegiert jetzt dorthin. Neuer lesender Endpoint `RulesApi.ruleCategoryFor: string * string -> Async<YnabCategoryId option>` (`src/Shared/Api.fs`, `src/Server/Api.fs`, Einzeiler über `Persistence.Rules.getAllRules`). Kein neues Pflichtfeld in einem Record aus `src/Shared/`, Fixtures unberührt.

**Regel-Tab** (`src/Client/Components/Rules/`): `PanelOrigin` (`FromRulesTab | FromEntry entryId`) im `Panel`, `RuleDraft.fromEntry`, Nachricht `EntryRuleRequested`. Aus einer Buchung schließt das Formular nach dem Anlegen in jeder Hülle (`afterSave` mit Herkunft); `followEntry` schließt es, wenn das Kassenbuch eine andere oder keine Buchung zeigt. `View.entryForm` liefert Titel, Zurück, Inhalt und Fuß für Panel, Sheet (mit Kategorie-Auswahl als Sheet-Inhalt, §7.15) und Dialog, darunter „Gilt für neue Importe.“ (12 px, gedämpft). Der Tab selbst ignoriert ein Formular aus einer Buchung.

**Kassenbuch** (`src/Client/Components/Ledger/`): `ruleSeedFor`, `rulePayeeOf`, `ruleCategoryOf`, `detailShows`, `RuleOffer` (`Checking`/`Shown`, je Buchung und Kategorie), `ruleOfferStays`, `ruleOfferFor`, `ruleOfferText`. Nachrichten `RuleFromEntryRequested`, `RunCheckRuleOffer` (über `Retry.reading Api.rules.ruleCategoryFor`), `RuleOfferChecked`, `RuleOfferDismissed`; ExternalMsg `OpenRuleForEntry`. `EntryCategorySet` startet die Frage nur bei `CategoryChanged` mit offenem Detail (`ruleOfferCheckFor`); `StartSetEntryCategory` und `StartUndoCategory` nehmen das Angebot weg; die Hülle von `update` hält es nur, solange `ruleOfferStays` gilt. View: Feldzeile „Regel anlegen“ mit `Detail.forwardArrow` vor „HISTORIE“, Angebot-Zeile unter der Kategorie (nach der Karte „Budget nach dem Wechsel“), `detailPanelOrRuleForm`/`detailSheetOrRuleForm` als Hüllen um die unveränderten `detailPanelView`/`detailSheetView` (wegen ledger-ea70s nur Ergänzungen), `view` und `ReviewPage` bekommen `EntryRuleFormSlot`. Auf der Karte des Durchgehens steht „Regel anlegen“ als Textlink links neben „Details“ (neu `ReviewCard.linksWithDetails`, auch auf `/styleguide`). Über dem offenen Formular gelten die Tasten des Durchgehens nicht. Root (`src/Client/State.fs`, `src/Client/View.fs`): `OpenRuleForEntry` schickt `EntryRuleRequested` und `LoadCategories`; nach jeder Kassenbuch-Nachricht `followEntry` mit der Buchung im Detail, `UrlChanged` schließt das Formular aus einer Buchung.

**Tests** (25 neu): `src/Tests/RulesEngineTests.fs` (5: erste Regel gewinnt, inaktive übersprungen, Memo, kein Treffer, ohne Payee); `src/Tests/LedgerRuleOfferClientTests.fs` (12: Vorbelegung und Fälle ohne Kategorie, Aufteilung, Übertrag, ohne Payee; Frage nach der Änderung, „passt schon“, andere Kategorie der Regel, Netzfehler, kein Angebot ohne Payee, bei entfernter Kategorie, auf der Karte ohne Details, bei Aufteilung, Übertrag, Sammelaktion; „Anlegen“ im Angebot; dazu die Aufzählung aller Reihenfolgen von Antwort, ×, Rückgängig und Wechsel der Buchung mit Orakel nach Doktrin Punkt 20); `src/Tests/RulesListTests.fs` (6: Vorbelegung, Entwurf bleibt, nach dem Anlegen vorn und Formular zu, Abbrechen, `followEntry`, Root öffnet und schließt mit Detail und Seitenwechsel); `RemotingEndpointNegativeTests.fs` Zeile für `ruleCategoryFor`; `RetryTests.fs` Inventar um `ruleCategoryFor`. `LedgerCategoryChangeClientTests.fs`: der Zeitgeber-Test ignoriert neben `LoadHistory` jetzt auch `RunCheckRuleOffer`. Browser: `scripts/regel-aus-buchung.test.mjs` mit `[flow:regel-aus-buchung]` (412 px Touch: Kassenbuch, Bauhaus öffnen, „Regel anlegen“, Name/Muster „Bauhaus“ und „Lebensmittel“ sichtbar, anlegen, zurück im Detail, Toast), `FLOWS` ergänzt. Layout-Sweep (`scripts/layout-states.mjs`): neue Zustände `kassenbuch-regel-angebot` (Handy, Angebot und Toast verdecken einander nicht), `kassenbuch-regel-formular` (Handy), `kassenbuch-regel-panel` (1440), `durchgehen-regel` (412, 1280); `mockLedger` antwortet auf `ruleCategoryFor` mit `null`.

**Gate:** `./verify.sh` zeigt `VERIFY: PASS` (dotnet 5036 grün, node 339 grün).

**README-Delta:** `npm run check:readme-delta -- --bc ledger` Exit 0, „Ganz nachlesen: keine Einträge“.

**Screenshots** unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/hg5m5-shots/`: `handy-regel-formular.png` (Sheet, vorbelegt), `handy-angebot.png` (Angebot unter der Kategorie, Toast mit Rückgängig), `pc-regel-panel.png` (Panel mit „Zurück“, 1440), `pc-durchgehen-regel.png` (Dialog), `handy-durchgehen-regel.png` (Sheet).

**Für Roman, nicht selbst entschieden bzw. abweichend:**
- Karte im Durchgehen: „Regel anlegen“ ist ein Textlink links neben „Details“, keine eigene Zeile. Eine eigene Zeile (52 px) schob bei der Amazon-Karte am Handy „Passt, weiter“ aus dem Bild (Sweep `durchgehen-amazon`, Invariante 7).
- Am Handy führt im Sheet der Schließen-Knopf (×) zur Buchung zurück, wie bei Payee und Betrag; einen beschrifteten „Zurück“-Knopf gibt es nur im Panel am PC.
- Antwortet der Server auf die Frage „passt schon?“ nicht, erscheint kein Angebot (der feste Eintrag bleibt).
- Sweep-Ausnahme in `kassenbuch-regel-panel` @ 1440, Invariante 6, `[data-testid="ledger-detail-panel"] und div`: schon vor dieser Task ragt bei jedem offenen Detail-Panel nach dem Scrollen die haftende Filterleiste 4 × 12 px unter den Panel-Kopf (mit ausgeschalteter neuer Zeile nachgemessen). Zeile für die Kann-Sammlung ledger.
- [human-eye] Romans Blick am Handy und am PC steht aus.

Integration (Conductor): mit ledger-ea70s textlich konfliktfrei zusammengeführt; `./verify.sh` auf dem zusammengeführten Stand VERIFY: PASS (dotnet 5001, node 340). Die Sweep-Ausnahme zur haftenden Filterleiste steht als Zeile in der Kann-Sammlung ledger.
