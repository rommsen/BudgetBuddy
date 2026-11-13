---
id: categorization-n3sxw
title: "Regeln ins Kassenbuch: Regeln als Tab neben den Buchungen statt eigener Seite"
status: todo
type: feature
context: categorization
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, design-system-k6kfw]
blocks: [ledger-hg5m5]
tags: [etappe-3, frontend, regeln]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
Regeln sind heute eine eigene Seite (`#/rules`), erreichbar nur über einen Textlink im Kopf des Kassenbuchs,
das Provisorium aus design-system-k6kfw. ADR 0087 Punkt 9 legt sie als Tab ins Kassenbuch, Punkt 16 das
Bearbeiten am PC ins Panel. Der Client der Regeln hat dazu die offenen Befunde aus dem Bestandsreview
categorization (aufgenommen aus categorization-krscs): verschluckte Fehler, Bool-Flags für Ladezustände,
späte Antworten ohne Bindung an die Anfrage, Seiteneffekte in update und View.

## What
Das Kassenbuch bekommt die Tabs „Buchungen“ und „Regeln“ (`Tabs.bar`). Der Tab „Regeln“ zeigt die Liste
der Regeln nach Priorität. Eine Regel öffnet am PC (ab 1024 px) im Panel rechts, am Handy im Sheet. Die
eigene Seite entfällt. Der Regel-Client wird dabei nach `elmish-state.md` und `semantic-messaging.md`
neu geschnitten.

Vorlage ist Prototyp Version 17 (`ledger()` mit `S.ltab==='regeln'`, `rulesView()`, Sheet `rule`, PC-Ansicht
mit `rhead`/`rrow`, Tastatur ab `keydown`). Für Worker und Verifier liegen die Dateien im Scratchpad:
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/6d389406-c915-4195-8b34-eccfb6980fcd/scratchpad/n3sxw/`
- `proto/prototyp-v17.html` (Prototyp, im Browser öffnen: Kassenbuch, Tab Regeln)
- `proto/handy-regeln-liste.png`, `proto/handy-regel-bearbeiten.png`, `proto/handy-regel-neu.png` (412 px)
- `proto/pc-regeln-liste.png`, `proto/pc-regel-bearbeiten.png`, `proto/pc-regel-neu.png` (1440 px)
- `bb/top-handy-390x844-rules.png`, `bb/top-pc-1440x900-rules.png` (BB heute, Entwicklungsdatenbank)
- `shots.mjs` (Playwright-Skript, das die Prototyp-Bilder erzeugt; zeigt die Klickpfade)

Liegt der Scratchpad nicht mehr vor: der Conductor liest den Prototyp neu (`Artifact` read) und legt die
Dateien wieder ab.

### Abgleich BB heute mit dem Prototyp (Conductor, 2026-11-13)
| Funktion | BB heute | Prototyp | Gilt |
|---|---|---|---|
| Ort | Seite `#/rules` | Tab im Kassenbuch | Prototyp |
| Bearbeiten | Modal (PC), Sheet (Handy) | Panel (PC), Sheet (Handy) | Prototyp |
| Art, Feld | Auswahlfelder | `Segmented` mit je 3 Optionen | Prototyp (§7.5: feste Menge ≤ 4) |
| Name | Pflicht | optional, leer heißt Muster | Prototyp |
| „Regel aktiv“ | nur beim Bearbeiten | auch beim Anlegen, Vorgabe an | Prototyp |
| Payee-Override | ja | fehlt | Prototyp, entfällt (Roman, 2026-11-13) |
| Export/Import JSON | ⋮-Menü | fehlt | Prototyp, entfällt (Roman) |
| Neu laden | Knopf am PC und im ⋮-Menü | fehlt | Prototyp, entfällt (Roman) |
| Löschen | zweistufig, ohne Rückgängig | sofort, Rückgängig im Toast | Prototyp (Roman, 2026-11-13) |
| Pflichtfelder | `Form.submitButton` gesperrt, nennt Fehlendes | Toast „Muster und Kategorie fehlen“ | BB |
| Laden, leer, Fehler | Skeleton, „Keine Regeln“, Fehler mit Retry | nicht gezeigt | BB |
| ↑↓ zwischen Regeln, Esc | nein | ja | Prototyp |

## Acceptance criteria

### Ort und Route
- [ ] Das Kassenbuch hat oben die Tabs „Buchungen“ und „Regeln“ (`Tabs.bar`, `role=tablist`), am Handy unter
      dem Kopf, am PC unter Titel und Untertitel (Prototyp `pc-regeln-liste.png`, Tabs höchstens 320 px breit).
- [ ] Der Tab „Regeln“ hat eine eigene Route (Vorschlag `#/ledger/rules`). `#/rules` führt dorthin, ein alter
      Lesezeichen-Link geht also nicht ins Leere. Die Navigation markiert dabei „Kassenbuch“.
- [ ] Der Textlink „Regeln“ im Kopf des Kassenbuchs und die Seite „Kategorisierungsregeln“ samt „Zurück“
      entfallen.
- [ ] GIVEN Buchungen mit gewähltem Konto, gesetztem Filter und gescrollter Liste WHEN Roman zu „Regeln“ und
      zurück zu „Buchungen“ wechselt THEN stehen Konto, Filter, Auswahl und Scrollstand wie vorher.

### Liste
- [ ] Über der Liste steht „Regeln werden von oben nach unten geprüft. Die erste passende, aktive Regel
      gewinnt.“ (12 bis 14 px, `text-secondary`).
- [ ] PC: Tabelle mit den Spalten #, Name, Muster (Mono), Art („Enthält · Payee“), Kategorie, Status
      („aktiv“ in Grün, „inaktiv“ gedämpft) und den Pfeilen ↑↓ rechts. Die gewählte Regel hat Fläche
      `bg-hover` und Rand im Akzent wie die gewählte Buchung. Untertitel im Kopf „N Regeln, M aktiv“,
      rechts „+ Neue Regel“ als Hauptknopf.
- [ ] Handy: Karte mit einer Zeile je Regel: Nummer links, Name (inaktive mit „· inaktiv“ gedämpft),
      darunter „Enthält „rewe“ in Payee“ und „→ Lebensmittel“, rechts übereinander ↑ und ↓. Unter der Liste
      „+ Neue Regel“ als breiter Knopf ohne Füllung. Kein Knopf „+ Buchung“ auf dem Tab Regeln.
- [ ] Die Pfeile ändern die Reihenfolge sofort (optimistisch), am Rand gesperrt. Scheitert das Speichern,
      steht die alte Reihenfolge wieder da und ein Toast nennt es.
- [ ] GIVEN Roman hat eine Regel verschoben WHEN eine ältere Ladeantwort danach ankommt THEN bleibt die neue
      Reihenfolge stehen (Ladeversion oder Sperre, Befund `State.fs:52` aus krscs).
- [ ] Laden zeigt ein Skeleton, eine leere Liste „Keine Regeln“ mit „Neue Regel“, ein Ladefehler die
      Ursache mit „Erneut versuchen“ (`ErrorDisplay`), nie eine leere Liste mit falschem Hinweis.

### Panel und Sheet
- [ ] PC: Klick auf eine Regel öffnet sie rechts im Panel „Regel bearbeiten“, „+ Neue Regel“ das Panel
      „Neue Regel“. Ohne Auswahl zeigt das Panel „Regel wählen oder eine neue anlegen.“ Nach dem Anlegen
      zeigt das Panel die neue Regel (§7.6). Breite wie das Panel der Buchungen; die 30 % kommen mit
      design-system-gcpws.
- [ ] Handy: dieselben Inhalte im `BottomSheet`, Fuß mit „Abbrechen“ und „Speichern“ bzw. „Regel anlegen“.
- [ ] Felder in dieser Reihenfolge: Name (Platzhalter „z. B. Rewe“), Wenn (`Segmented` Enthält, Exakt, Regex),
      Suchen in (`Segmented` Payee & Memo, Payee, Memo), Muster (Mono), Dann Kategorie (`CategoryPicker`,
      am Handy ersetzt die Auswahl den Inhalt des Sheets, §7.15), Regel aktiv (Schalter, beim Anlegen an), Muster testen, beim Bearbeiten „Regel löschen“ (rot umrandet).
- [ ] Ein leerer Name wird beim Speichern zum Muster.
- [ ] Ohne Muster oder Kategorie ist der Knopf gesperrt und nennt, was fehlt (`Form.submitButton`).
      Ein ungültiges Regex nennt der Server als Feldfehler am Muster.
- [ ] Am Handy mit Touch bei 412 px: der Wechsel von Name zu Muster und von Muster zu „Muster testen“ lässt
      die Tastatur offen, ein Tipp auf einen `Segmented`-Knopf schließt sie nicht (`Segment.KeepsFocus`).

### Muster testen
- [ ] Unter dem Feld steht ohne Eingabe „Text eingeben, um das Muster zu prüfen.“, sonst „Passt“ (Grün)
      oder „Passt nicht“ (Rot); ein ungültiges Muster nennt den Grund, ein Netzfehler hat eine eigene
      Meldung. Beides ist nie „Passt nicht“ (README categorization, Muster-Test).
- [ ] GIVEN eine Testanfrage für Eingabe A läuft WHEN Roman Muster, Art, Feld oder Text ändert und danach
      die Antwort für A kommt THEN zeigt das Ergebnis nichts aus A (Antwort an die Anfrage gebunden).
- [ ] `testRule` läuft über `Retry.reading` wie an anderen lesenden Stellen; das Inventar in
      `src/Tests/RetryTests.fs` ist nachgezogen (Entscheidung aus categorization-h2w8k: gleich behandeln).

### Löschen mit Rückgängig
- [ ] „Regel löschen“ löscht sofort, schließt Panel bzw. Sheet und zeigt den Toast „Regel gelöscht“ mit
      „Rückgängig“. Der Toast verdeckt keine Bedienelemente (ADR 0087 Punkt 22).
- [ ] GIVEN eine gelöschte Regel an Stelle 3 WHEN Roman „Rückgängig“ wählt THEN steht sie mit allen Feldern
      (auch Payee-Override und aktiv/inaktiv) wieder an Stelle 3. Das ist ein Aufruf an den Server; scheitert
      er, fehlt die Regel und ein Toast nennt es. Kein Zwischenstand, in dem sie an falscher Stelle steht.

### Was entfällt (Roman, 2026-11-13)
- [ ] Kein ⋮-Menü, kein „Neu laden“, kein Export und Import, kein Feld Payee-Override in der Oberfläche.
      `exportRules` und `importRules` fallen aus `RulesApi` samt Handlern und Tests.
- [ ] GIVEN eine Regel trägt schon einen Payee-Override WHEN Roman sie im Panel ändert und speichert THEN
      bleibt der Override unverändert (`PayeeOverrideChange.Keep`). Engine und Speicherung bleiben wie sie sind.

### Tastatur am PC (§7.8, ADR 0087 Punkt 18)
- [ ] ↓ ohne Auswahl öffnet die erste, ↑ die letzte Regel im Panel; danach ↑↓ die nächste bzw. vorige.
      In einem Textfeld des Panels gehören die Pfeile dem Cursor.
- [ ] Esc schließt das Panel; ist ein Picker oder Menü offen, schließt Esc nur das.
- [ ] Unten die Tastenleiste „↑ ↓ Regel · Esc schließen“ (`ReviewCard.keyBar`/`kbd` oder gleich aussehend).
- [ ] Die Tastenlogik ist über `update` bzw. die reine Tastenfunktion getestet. Der generische Tastentest
      über alle Listen kommt mit design-system-gcpws.

### Regel-Client (aus categorization-krscs)
- [ ] Laden, Speichern, Löschen und Muster-Test sind `RemoteData` bzw. ein DU, kein Bool und kein
      Anzeigetext mit Emoji.
- [ ] Anlegen und Ändern sind getrennte Nachrichten, der Editor ist ein DU (`NewRule` | `Editing of Rule`).
- [ ] Nachrichten heißen nach der Absicht (`PatternEntered`, `CategoryChosen` …), tote Nachrichten
      (`ToggleRuleEnabled`, `RuleToggled`, `ImportRulesStart`, `CancelConfirmDelete`) sind weg.
- [ ] Kein `Cmd.OfAsync.perform` und kein `JS.eval` im Regel-Modul; die Toasts sind deutsch.

### Tests und Gate
- [ ] `scripts/rules-page-mobile.test.mjs` fliegt raus. Neu, je Flow einer, gegen die Muster aus dem
      Styleguide: `[flow:regel-anlegen]` (Handy 412 px mit Touch: Kassenbuch, Tab Regeln, „Neue Regel“,
      Kategorie wählen, anlegen, Regel steht vorn in der Liste). `FLOWS` in `scripts/browser-usage.mjs`
      beschreibt ihn neu, `regeln-importieren` fällt raus.
- [ ] Der Layout-Sweep (`scripts/layout-states.mjs`) zeigt die Zustände `regeln`,
      `regeln-panel` (PC) und `regeln-sheet-picker` (Handy) auf der neuen Route; `KNOWN_ROUTES` in
      `scripts/screenshot-route.mjs` ist nachgezogen.
- [ ] Neue Pflichtfelder in Records aus `src/Shared/` sind in den Fixtures der `scripts/*.test.mjs` nachgezogen.
- [ ] Seite neben der Skizze verglichen: Screenshots von BB (Handy 412 px, PC 1440 px: Liste, Regel
      bearbeiten, Neue Regel) neben `proto/*.png`; Abweichungen nur die unten genannten. Im Outcome stehen
      die Pfade der Screenshots.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Romans Blick am Handy und am PC: der Tab sieht aus wie im Prototyp. [human-eye]

## Abweichungen vom Prototyp
- (a) Der gesperrte Knopf mit Hinweis auf fehlende Pflichtfelder bleibt aus BB statt des Toasts im
  Prototyp: ein Fehler zur Eingabe steht am Feld, nie als Toast (`.claude/rules/task-ui-vorgaben.md`).
- (b) Laden, leer und Fehler wie heute in BB; der Prototyp zeigt sie nicht.
- (c) Panelbreite wie heute im Kassenbuch, bis design-system-gcpws die 30 % für alle Panels umsetzt.

## Notes
Verfeinert 2026-11-13 mit Roman: zwei Tasks (dieser Tab, danach ledger-hg5m5 „Regel aus der Buchung“, hängt
hiervon ab und nutzt dasselbe Formular). Löschen mit Rückgängig wie bei der Buchung (Roman). Payee-Override, Export/Import und „Neu laden“ braucht
Roman nicht, sie fallen weg (Roman, 2026-11-13). Der Override bleibt in Daten und Engine; wer einen trägt,
behält ihn.
Aufgenommen: categorization-krscs (alle Befunde oben unter „Regel-Client“; der Befund
`SyncFlow/Types.fs:72` und categorization-m7tq3 (`InlineRuleTestVersion`) sind mit dem SyncFlow in
613eee26 entfallen; die Bindung des Muster-Tests an die Anfrage deckt das Kriterium unter „Muster testen“).
Regeln bleiben Zustand, keine Ereignisse (ADR 0083).
API heute: `RulesApi` in `src/Shared/Api.fs:115-155` (`getAllRules`, `createRule`, `updateRule`,
`deleteRule`, `reorderRules`, `exportRules`, `importRules`, `testRule`; Export und Import fallen). Für Rückgängig braucht es einen
Aufruf, der eine Regel an einer Stelle wieder anlegt; der Worker wählt die Form (eigener Endpoint oder
`createRule` mit Position), Validierung in `src/Server/Validation.fs`.
Heutige Regel-Seite: `src/Client/Components/Rules/` (View.fs 375-610 Formular, 648-700 Menü, das entfällt), Route
`Page.Rules` in `src/Client/Types.fs:144, 196, 231, 246, 265, 283`, Link im Kassenbuch
`src/Client/Components/Ledger/View.fs:4484-4496`. Bausteine: `Tabs.bar`/`Tabs.panel`, `Detail.panel`,
`BottomSheet`, `Segmented`, `CategoryPicker`.
Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus
und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
