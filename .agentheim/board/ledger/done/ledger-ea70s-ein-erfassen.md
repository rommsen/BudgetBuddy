---
id: ledger-ea70s
title: "Ein Erfassen: Quick Add hört auf, ein eigener Ort zu sein; „+ Buchung“ in Kassenbuch und Übersicht mit kluger Vorbelegung"
status: done
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, design-system-k6kfw, categorization-n3sxw]
blocks: []
tags: [etappe-3, frontend, erfassen]
related_adrs: [0086, 0087, 0011]
related_research: []
prior_art: [ledger-at63w, ledger-s1weh, ledger-vf6z5]
---

## Why
Quick Add und „Neue Buchung“ sind zwei Wege für dasselbe. ADR 0087 Punkt 9: Quick Add ist kein Ort mehr, sein
Versprechen (wenige Tipps, kluge Vorbelegung) gilt für „+ Buchung“ in Kassenbuch und Übersicht. Dazu stapelt sich die
Kategorie-Auswahl an vier Stellen noch über einem Sheet (`CategoryPicker.sheetLayered`), Styleguide §7.15 verlangt, dass
sie den Inhalt ersetzt.

## What
- Die Seite Quick Add (`#/quickadd`, `src/Client/Views/QuickAddPage.fs`) und ihr Eintrag in der Navigation entfallen. Die
  Navigation zeigt nur noch die drei Orte (Übersicht, Kassenbuch, Budget).
- „+ Buchung“ (Formular „Neue Buchung“, `src/Client/Components/Ledger/Capture.fs` und `View.fs` `captureFields`) ist der
  eine Weg zu erfassen. Es übernimmt, was nur Quick Add konnte: Vorbelegung des Quick-Add-Kontos, Schutz einer angefangenen
  Eingabe vor dem Neuladen, Fokus im Betrag am PC.
- Die Kategorie-Auswahl ersetzt an allen vier Stellen den Inhalt des Sheets (`CategoryPicker.sheetContent`, eingeführt mit
  categorization-n3sxw), die gestapelten Picker verschwinden aus dem Design-System.
- Memo steht am Handy unter dem Datum (aus ledger-yhemh).

Vorlage ist Prototyp Version 17 (Sheet `capture`, `capAcct`; Kassenbuch und Übersicht, Handy und PC). Für Worker und Verifier
liegen die Dateien im Scratchpad:
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/6d389406-c915-4195-8b34-eccfb6980fcd/scratchpad/ea70s/`
- `proto/prototyp-v17.html` (Prototyp; „+ Buchung“ auf Übersicht oder Kassenbuch)
- `proto/{handy,pc}-{uebersicht-neue-buchung,kassenbuch-neue-buchung,neue-buchung-zuletzt,neue-buchung-konto}.png`
- `bb/{handy-390x844,pc-1440x900}-quickadd.png` (Quick Add heute)
- `shots.mjs` (Klickpfade im Prototyp)
Liegt der Scratchpad nicht mehr vor: der Conductor liest den Prototyp neu (`Artifact` read) und legt die Dateien wieder ab.

### Abgleich Quick Add, Neue Buchung und Prototyp (Conductor, 2026-11-13)
| Funktion | Quick Add | Neue Buchung heute | Prototyp | Gilt |
|---|---|---|---|---|
| Ort | eigene Seite, Navigation | Panel/Sheet im Kassenbuch, Modal/Sheet auf der Übersicht | Sheet bzw. Panel | Neue Buchung |
| Konto ohne Filter | fest Quick-Add-Konto | zuletzt genutzt | Bar | Quick-Add-Konto (Roman) |
| Übertrag, Kategorie zum Payee, Feldfehler, Heute/Gestern, Abbrechen | nein | ja | teils | Neue Buchung |
| Zuletzt-Chips | 5, aus YNAB, mit Memo | 3, aus dem BB-Log, mit Konto | 3, mit Konto | Neue Buchung |
| Richtung hält Tastatur offen | nein | ja (`KeepsFocus`) | – | Neue Buchung |
| Fokus im Betrag | am PC | nein | – | Quick Add (am PC) |
| Nach dem Anlegen | bleibt offen, leer | schließt, Toast mit Rückgängig | schließt, Toast | schließen (Roman) |
| Schutz vor Neuladen | ja | nein | – | Quick Add |
| Kategorie-Picker | `BottomSheet.categoryPickerLayered` ohne Vorschläge | `CategoryPicker`, am Handy gestapelt | ersetzt den Inhalt | Prototyp |
| Datum und Memo am Handy | getrennte Spalten | nebeneinander | nebeneinander | untereinander (Roman, ledger-yhemh) |

## Acceptance criteria

### Quick Add entfällt
- [ ] Die Navigation zeigt am Handy (Leiste unten), von 768 bis 1023 px (Rail) und ab 1024 px (Seitenleiste) nur Übersicht,
      Kassenbuch und Budget; der Eintrag „Quick Add“ ist weg (`src/Client/DesignSystem/Navigation.fs`, Styleguide §5
      Navigation ohne den Satz „samt Quick Add bis `ledger-ea70s`“).
- [ ] `#/quickadd` führt auf die Übersicht und öffnet dort „Neue Buchung“ (ein altes Lesezeichen geht nicht ins Leere).
- [ ] `QuickAddPage.fs`, der Quick-Add-Zustand im Root (`QuickAdd`, `QuickAddTemplates`, `QuickAddCategories` und ihre
      Nachrichten) und die Endpunkte, die nur die Seite brauchte (`YnabApi.addManualTransaction`,
      `getRecentQuickAddTemplates`), sind entfernt samt Tests (`src/Tests/QuickAddTests.fs`, Zeilen der Negativtabelle).
      Bleiben: die Einstellung Quick-Add-Konto (`setQuickAddAccount`, `ynab_quickadd_account_id`), die Bob und die
      Vorbelegung brauchen, und der Actor `QuickAdd` für bestehende Ereignisse. Bob (`/bot/`) ist unverändert.
- [ ] Vor dem Entfernen ist geprüft, dass kein anderer Aufrufer die Endpunkte nutzt (grep über `src/`, `scripts/`).

### Neue Buchung mit kluger Vorbelegung
- [ ] GIVEN im Kassenbuch ist ein Konto gefiltert WHEN Roman „+ Buchung“ tippt THEN ist dieses Konto vorbelegt.
- [ ] GIVEN kein Konto gefiltert (Kassenbuch „Alle Konten“ oder Übersicht) und ein Quick-Add-Konto ist eingestellt und
      buchbar WHEN „+ Buchung“ THEN ist das Quick-Add-Konto vorbelegt (Roman, 2026-11-13).
- [ ] GIVEN kein Quick-Add-Konto eingestellt oder es ist nicht buchbar WHEN „+ Buchung“ THEN gilt die heutige Regel
      (zuletzt genutztes Konto, nie das Import-Konto).
- [ ] Ein Zuletzt-Chip belegt das Konto weiter wie heute (das Konto des Chips).
- [ ] Am PC (feiner Zeiger) steht beim Öffnen der Fokus im Betrag; am Handy kein Autofokus.
- [ ] Eine angefangene Eingabe in „Neue Buchung“ zählt zu `isInputActive` (Schutz vor dem Neuladen wie bei Quick Add).
      GIVEN ein Betrag ist eingetippt WHEN die App neu laden will THEN greift derselbe Schutz wie heute bei Quick Add.
- [ ] Nach „Buchung anlegen“ schließt das Formular wie heute, Toast mit Konto, Betrag und „Rückgängig“ (Roman).

### Kategorie-Auswahl ersetzt den Inhalt (§7.15, aus design-system-taytp)
- [ ] An allen vier Stellen ersetzt die Auswahl am Handy den Inhalt des Sheets und führt nach Wahl oder Schließen zurück
      (`CategoryPicker.sheetContent`): Neue Buchung, Kategorie im Buchungs-Detail, Ziel einer Split-Zeile, Geld verschieben
      im Budget (`src/Client/Components/Budget/MovesView.fs`).
- [ ] Die Eingaben im Formular darunter bleiben dabei erhalten (Betrag, Payee, Split-Zeilen, Betrag beim Verschieben).
- [ ] `CategoryPicker.sheetLayered`, `BottomSheet.categoryPicker` und `BottomSheet.categoryPickerLayered` sind aus dem
      Design-System entfernt, `/styleguide` zeigt sie nicht mehr; Styleguide §2, §5 und §7.15 nennen keine Abweichung
      „bis Etappe 3“ mehr.
- [ ] Der Schließen-Knopf aller `BottomSheet`s hat 48 px Trefferfläche am Handy und nutzt ein Icon aus `Icons`
      (kein Inline-SVG; Funde `BottomSheet.fs:110`, `:186` aus taytp).
- [ ] Ein Account-Picker in `BottomSheet` (`BottomSheet.fs:586`), der danach keinen Aufrufer mehr hat, ist entfernt; hat er
      noch einen, sind seine Zeilen fokussierbar und kürzen lange Namen mit Ellipse und `title`.

### Datum und Memo (aus ledger-yhemh)
- [ ] Am Handy (unter 768 px) steht Memo unter dem Datum, ab dem Panel bzw. Dialog am PC nebeneinander wie in der Skizze,
      solange der Layout-Sweep dort keine Verletzung meldet; sonst auch dort untereinander (im Outcome nennen).
- [ ] Regressionstest als Zustand im Layout-Sweep bei 412 px (Doktrin Punkt 8), mit Kommentar zum Bug.

### Tests und Gate
- [ ] Die Seite Quick Add hatte keine Browser-Tests. Der vorhandene `[flow:buchung-erfassen]` bleibt der eine Flow fürs
      Erfassen; seine Zusicherungen passen zur neuen Vorbelegung. Die Sweep-Zustände `kassenbuch-erfassen*` und
      `uebersicht-erfassen` laufen weiter, dazu ein Zustand mit offener Kategorie-Auswahl im Sheet der Neuen Buchung
      (412 px), im Buchungs-Detail und beim Geld verschieben.
- [ ] Vorbelegung, Schutz vor Neuladen und Umleitung von `#/quickadd` sind unter .NET getestet (reine Funktionen bzw.
      `update`), nicht im Browser.
- [ ] Neue oder entfernte Pflichtfelder in Records aus `src/Shared/` sind in den Fixtures der `scripts/*.test.mjs`
      nachgezogen; `RemotingEndpointNegativeTests` passt zu den verbliebenen Endpunkten.
- [ ] Seite neben der Skizze verglichen: Screenshots von BB (412 px Touch und 1440 px: Übersicht und Kassenbuch mit offener
      Neuen Buchung, Kontoauswahl, Kategorie-Auswahl im Sheet) neben `proto/*.png`; Abweichungen nur die unten genannten.
      Im Outcome stehen die Pfade.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Romans Blick am Handy und am PC: Erfassen ohne Quick Add fühlt sich mindestens so schnell an wie vorher. [human-eye]

## Abweichungen vom Prototyp
- (a) Auf der Übersicht öffnet „Neue Buchung“ ab 1024 px als Modal, nicht als Panel (Roman, 2026-11-11, Styleguide §7.6).
- (b) Datum mit „Heute“ und „Gestern“, Konto mit Saldo, Kategorie mit Konten als Ziel (Übertrag), Feldfehler am Feld,
      „Rückgängig“ im Toast: BB kann es, der Prototyp nicht (CLAUDE.md „gilt BB“).
- (c) Am Handy Memo unter dem Datum statt daneben (Roman, ledger-yhemh).
- (d) Tabs, Umschalter, Knöpfe und Sheet-Kopf sind die DS-Bausteine wie überall (Roman, 2026-11-13, aus categorization-n3sxw).

## Notes
Verfeinert 2026-11-13 mit Roman: ohne Filter immer das Quick-Add-Konto (Bar), nach dem Anlegen schließen, alle vier
gestapelten Picker in diesem Schnitt. Nimmt auf: design-system-taytp (Kategorie-Picker vereinheitlichen; seine Funde zum
SyncFlow und zum Split im Import sind mit dem SyncFlow in 613eee26 entfallen, die zu Quick Add mit dieser Task) und
ledger-yhemh (Memo unter Datum). Der Fund aus taytp zu ADR 0011 (positive Available-Beträge grün laut ADR, neutral in Code
und Styleguide) ist eine Doku-Frage und geht in die Kann-Sammlung design-system.
Bestand (Explore, 2026-11-13): Quick Add `QuickAddPage.fs`, Root `src/Client/State.fs:273-293, 827-855, 1112-1137`,
Server `src/Server/Api.fs:412-449, 721-727`, `Ledger/CommandHandlers.fs:1215-1249` (`addQuickAdd`, Actor `QuickAdd`).
Neue Buchung `Components/Ledger/Capture.fs`, `State.fs:3217-3445`, `View.fs:3794-4275`; Vorbelegung des Kontos
`Domain.fs:1291`, `Capture.fs:114-132`, `Ledger/State.fs:1172`. Gestapelte Picker: `Ledger/View.fs:1745` (Detail), `:2135`
(Split), `:4185` (Neue Buchung), `Budget/MovesView.fs:236`, Styleguide-Demos `Styleguide/View.fs:1488, 1892`.
Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen
die Muster aus dem Styleguide neu geschrieben, je Flow einer. Quick Add hatte keine.

## Outcome

„+ Buchung“ ist der eine Weg zu erfassen (ADR 0087 Punkt 9). `./verify.sh`: VERIFY: PASS (dotnet 4977 grün, node 339 grün, Build, beide Wächter).

**Quick Add entfällt**
- Navigation zeigt nur Übersicht, Kassenbuch, Budget (`src/Client/DesignSystem/Navigation.fs`, Leiste unten jetzt `grid-cols-3`; `Types.Place` ohne `QuickAddPlace`, `Page` ohne `QuickAdd`). Styleguide §5 Navigation ohne den Satz „samt Quick Add bis ledger-ea70s“.
- `#/quickadd`: `Routing.parseUrl ["quickadd"]` = Übersicht, `Routing.opensCapture` erkennt den alten Pfad; `UrlChanged` öffnet dann die Neue Buchung (`CaptureFromOverview`) und ersetzt die URL durch `/` (`src/Client/State.fs`), auch beim Start (`init`).
- Entfernt: `src/Client/Views/QuickAddPage.fs`, Root-Zustand `QuickAdd`/`QuickAddTemplates`/`QuickAddCategories` samt Nachrichten und Ladeweg, `QuickAddFormState`/`buildQuickAddRequest`/`applyTemplateToForm` (`Types.fs`), CSS `.qa-*` und `.layer-2`; Server `YnabApi.addManualTransaction`, `getRecentQuickAddTemplates`, `Server.Api.QuickAddPorts`/`addManualTransactionWith`/`quickAddPortsOn`, `Ledger.CommandHandlers.addQuickAdd`, `Shared.Domain.QuickAddTemplate`/`recentQuickAddTemplates`/`amountFromMilliunits`. Vorher per grep über `src/` und `scripts/` geprüft: kein weiterer Aufrufer (Bob nutzt `addCashBookingWith` und `resolveQuickAddAccount`, unverändert). Bleiben: `setQuickAddAccount`, `ynab_quickadd_account_id`, Actor `QuickAdd`, `/bot/`.
- Tests: `src/Tests/QuickAddTests.fs` gelöscht; die Fälle für weiter genutzten Code (`manualTransactionMilliunits`, `validateManualTransaction`, `buildManualTransactionBody`, `parseAmountInput`, `formatAmountForInput`) stehen unverändert in `src/Tests/ManualTransactionTests.fs`. Gelöscht mit dem Code: die Quick-Add-Fälle in `LedgerCashBookingTests.fs` (Kategorie-Auflösung von `addQuickAdd`, Adapter `addManualTransactionWith`, Einzel-Post von Quick Add; der Einzel-Post ohne import_id bleibt über den Fall „Neue Buchung sendet über den echten Einzel-Post“ belegt), Zeilen der Negativtabelle, Wiring-Zeilen und Gegenbeweise in `StructureGuardTests.fs`, `RetryTests`-Inventar, `AppReadinessTests`, Routing-Zeilen. Diese Tests sind entfallen, weil ihr Produktionscode entfiel, nicht weil ein anderer Test sie abdeckt (Doktrin Punkt 8, Umzugsfall trifft nicht zu).

**Kluge Vorbelegung**
- `LedgerBookingDefaults.QuickAddAccountId` (neues Pflichtfeld, `src/Shared/Domain.fs`): der Server löst `ynab_quickadd_account_id` über `resolveQuickAddAccountWith` zum gespiegelten Kassenbuch-Konto auf (`getLedgerBookingDefaultsWith`, `src/Server/Ledger/QueryHandlers.fs`), sonst `None`. Fixture `scripts/ledger-capture-fixture.mjs` nachgezogen (Quick-Add-Konto Bar, zuletzt genutzt Tagesgeld).
- `defaultBookingAccount filter quickAdd lastUsed selectable`: Filter, sonst Quick-Add-Konto, sonst zuletzt genutzt, nur buchbare Konten, nie das Import-Konto. Das Formular hält den Filter beim Öffnen fest (`CaptureState.FilterAccount`); `OpenCaptureUnfiltered` (Übersicht) ignoriert einen Kontofilter des Kassenbuchs. Zuletzt-Chip belegt das Konto des Chips.
- Fokus im Betrag am PC (`Input.TextInputProps.AutoFocus = Viewport.isFinePointer ()`), am Handy nicht.
- `Capture.inputStarted` (Betrag, Payee, Memo, selbst gewähltes Ziel, laufendes Anlegen) zählt zu `isInputActive`.
- Nach „Buchung anlegen“ unverändert: schließt, Toast mit Konto, Betrag, „Rückgängig“.

**Kategorie-Auswahl ersetzt den Inhalt (§7.15)**
- Neue Buchung (`captureSheetView`, auch die Konto-Auswahl wie Skizze `capAcct`, Unterzeile „Für die neue Buchung“), Kategorie im Buchungs-Detail und Ziel einer Split-Zeile (`detailSheetView`), Geld verschieben (`InspectorView.inspectorSheet` mit `MovesView.counterpartPickerProps`). Schließen führt zurück, die Eingaben stehen im Modell und bleiben.
- Entfernt: `CategoryPicker.sheetLayered`, `BottomSheet.categoryPicker`, `categoryPickerLayered`, `BottomSheet.viewLayered`. `/styleguide` §19 zeigt jetzt `CategoryPicker.sheet` (Karte `components-categorypicker` bleibt für den Export), Split-Demo mit `sheet`. `styleguide.md` §2, §5, §7.15 (Stacking) ohne „bis Etappe 3“.
- Schließen-Knopf aller BottomSheets: `Icons.x` statt Inline-SVG, Trefferfläche 48 px (`.sheet-close`). Ein Account-Picker in `BottomSheet.fs` existiert nicht mehr (nichts zu entfernen).
- Sweep-Funde behoben: „Versteckt (n)“ im CategoryPicker und `Money.accountListRowWith` haben am Handy 48 px (`TouchTargets.mobileMinHeight md:min-h-0`). Die Ausnahme für Invariante 5 im Zustand `kassenbuch-erfassen-konten-laden` (Schleier der zweiten Ebene) ist entfallen.

**Datum und Memo**: am Handy untereinander (`grid-cols-1 md:grid-cols-2`), ab 768 px nebeneinander; der Sweep meldet im Dialog am PC nichts. Regressionszustand `kassenbuch-erfassen-memo-unter-datum` (412 px) mit Kommentar zum Bug ledger-yhemh; gegen den alten Stand (`grid grid-cols-2`) gebaut war er rot („Memo oben 1391, Datum unten 1475“).

**Tests**: .NET: Vorbelegung (`LedgerBookingCaptureTests`, `LedgerCaptureClientTests`), Umleitung und Filter auf der Übersicht (`OverviewTodoTests`, `RoutingTests`), Schutz vor dem Neuladen (`PwaTests`), Auswahl-Rundweg erhält Eingaben. Gegenprobe: vier Mutanten (Quick-Add-Zweig in `defaultBookingAccount`, `inputStarted`, Umleitung `opensCapture`, `OpenCaptureUnfiltered` mit Filter) machten je mindestens einen Test rot. Browser: `[fokus]` in `scripts/ledger-capture.test.mjs` (rot mit `AutoFocus = false`), `[flow:buchung-erfassen]` prüft das vorbelegte Quick-Add-Konto; Sweep-Zustände `kassenbuch-erfassen-kategorie`, `kassenbuch-erfassen-konto`, `kassenbuch-detail-kategorie` (412 px), `budget-picker` prüft am Handy genau ein Sheet. `move-money.test.mjs` und `navigation.test.mjs` angepasst. `.claude/rules/css-animation-safety.md`: toter Pfad `src/Client/Views/**` entfernt (Harness-Wächter).

**Neben der Skizze verglichen** (BB-Screenshots in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/ea70s-shots/`: `handy-kassenbuch-neue-buchung.png`, `handy-uebersicht-neue-buchung.png`, `handy-neue-buchung-zuletzt.png`, `handy-kassenbuch-konto.png`, `handy-uebersicht-konto.png`, `handy-kassenbuch-kategorie.png`, `handy-uebersicht-kategorie.png`, `pc-kassenbuch-neue-buchung.png`, `pc-uebersicht-neue-buchung.png`, `pc-neue-buchung-zuletzt.png`, `pc-kassenbuch-konto.png`, `pc-uebersicht-konto.png`, `pc-kassenbuch-kategorie.png`, `pc-uebersicht-kategorie.png`, dazu `handy-detail-kategorie.png`, `handy-geld-verschieben-kategorie.png`, `handy-split-ziel.png`; Skizze in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/6d389406-c915-4195-8b34-eccfb6980fcd/scratchpad/ea70s/proto/`). Abweichungen nur die genannten: (a) Übersicht am PC als Dialog; (b) Konto mit Saldo, Heute/Gestern, Kategorie mit Übertrag, Feldfehler, Rückgängig; (c) Memo unter Datum am Handy; (d) DS-Bausteine, darunter der Sheet-Kopf „Konto wählen“ (wie „Kategorie wählen“) statt „Konto“ der Skizze.

**README-Delta** (Gate `readme-delta-check`, Exit 0): „Barbuchung“ und „Buchung erfassen (Neue Buchung)“ ersetzt. Ganz nachlesen: BuchungÜbernehmen (README Key commands) nachgezogen, vom Conductor direkt im Bullet statt über einen Nachtrag-Bullet (der alte Satz zu `addQuickAdd` ist raus); BuchungErfassen passt. Außerdem nachgezogen: `sheetLayered` in „Kategorie ändern im Detail“ und „Übertrag in BB setzen“ (ledger), Kategorie-Picker (design-system), `getRecentQuickAddTemplates` bei `Retry.reading` (infrastructure).

Hinweis: beim Umbenennen der Testdatei lief versehentlich einmal `git rm --cached src/Tests/QuickAddTests.fs`; der Index ist mit `git restore --staged` auf den Ausgangsstand zurückgesetzt (keine gestagte Änderung im Worktree). Offen: [human-eye] Romans Blick am Handy und am PC.
